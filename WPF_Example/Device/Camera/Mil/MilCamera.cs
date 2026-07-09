using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Matrox.MatroxImagingLibrary;

namespace ReringProject.Device {
    // CXP 카메라 MIL Lite 10.0 grab 드라이버
    public class MilCamera : VirtualCamera, IDisposable {
        private MIL_ID MilApplication = MIL.M_NULL;
        private MIL_ID MilSystem      = MIL.M_NULL;
        private MIL_ID MilDigitizer   = MIL.M_NULL;
        private MIL_ID MilBuffer      = MIL.M_NULL;

#if !SIMUL_MODE
        // MIL 라이브(연속 grab) 스레드 제어
        private Thread _liveThread = null;
        private volatile bool _liveRunning = false;

        // 파생 클래스에서 GuiReadyForDisplay 를 직접 호출하려면 이벤트를 override 해야 한다.
        // (HikCamera L45 와 동일 — 그렇지 않으면 CS0070: 베이스 이벤트는 +=/-= 만 가능)
        public override event StateEvent GuiReadyForDisplay = null;
#endif

        public MilCamera(DisplayConfig config, DeviceInfo info) : base(config, info, ECameraType.MIL) {
            Properties = new VirtualCameraProperty();
            Properties.Width  = info.Width;
            Properties.Height = info.Height;
        }

        ~MilCamera() {
            Dispose();
        }

        public void Dispose() {
            Close();
        }

        public override bool Open(params object[] param) {
            try {
                // 1. Application 할당
                MIL.MappAlloc(MIL.M_DEFAULT, ref MilApplication);
                if (MilApplication == MIL.M_NULL) throw new Exception("MappAlloc failed");

                // 2. System 할당 — SIMUL_MODE 에서는 M_SYSTEM_HOST 사용 (Pitfall 1)
#if SIMUL_MODE
                MIL.MsysAlloc(MIL.M_DEFAULT, MIL.M_SYSTEM_HOST,
                              MIL.M_DEFAULT, MIL.M_DEFAULT, ref MilSystem);
#else
                // 실 HW: Rapixo CXP 보드를 명시 지정한다. M_SYSTEM_DEFAULT 는 이 PC 에서 Host(소프트웨어)
                // 시스템으로 잡혀 640x480 시뮬 디지타이저가 되는 문제가 있었다 (Intellicam 도 "Rapixo CXP 0"
                // 시스템을 명시 선택). 보드에 붙어야 자동 인식된 카메라 해상도(16544x9200)로 grab 된다.
                MIL.MsysAlloc(MIL.M_DEFAULT, "M_SYSTEM_RAPIXOCXP",
                              MIL.M_DEFAULT, MIL.M_DEFAULT, ref MilSystem);
#endif
                if (MilSystem == MIL.M_NULL) throw new Exception("MsysAlloc failed");

                // 3. Digitizer 할당 (M_DEFAULT — DCF 파일 미사용)
                MIL.MdigAlloc(MilSystem, MIL.M_DEV0, "M_DEFAULT",
                              MIL.M_DEFAULT, ref MilDigitizer);
                if (MilDigitizer == MIL.M_NULL) throw new Exception("MdigAlloc failed");

                // NOTE: 이 장비에서는 MdigControlFeature(Width/Height/PixelFormat) 쓰기가 불가하다
                //       ("Requested operation not supported"). 해상도/tap 기하는 반드시 DCF 로 설정해야 한다.
                //       DCF 준비 전까지는 M_DEFAULT 로 열어 카메라 현재 설정 그대로 grab 한다.

                // 4. Mono8 grab 버퍼 1회 할당 (Open 시 단일 할당, GrabHalconImage 에서 재사용 — Pitfall 5)
                //    버퍼 크기는 하드코딩 대신 디지타이저 실제값(MdigInquire)으로 잡는다.
                //    하드코딩 시 SIMUL_MODE 시뮬 디지타이저에 144MB non-paged(M_GRAB) 요구 →
                //    MbufAlloc2d Allocation error 발생. MdigProcess C# 예제 L62-66 패턴.
                MIL_INT bufW = MIL.MdigInquire(MilDigitizer, MIL.M_SIZE_X, MIL.M_NULL);
                MIL_INT bufH = MIL.MdigInquire(MilDigitizer, MIL.M_SIZE_Y, MIL.M_NULL);
                Logging.PrintLog((int)ELogType.Camera, "[INFO] {0} MIL grab size = {1} x {2}",
                                 Info.Identifier, (int)bufW, (int)bufH);
                MIL.MbufAlloc2d(MilSystem, bufW, bufH,
                                8 + MIL.M_UNSIGNED,
                                MIL.M_IMAGE + MIL.M_GRAB + MIL.M_PROC,
                                ref MilBuffer);
                if (MilBuffer == MIL.M_NULL) throw new Exception("MbufAlloc2d failed");

                // 회전 90/270 시 width/height 교환 (HikCamera.Open L404 동일)
                if (Info.RotateAngle == ERotateAngleType._90 ||
                    Info.RotateAngle == ERotateAngleType._270) {
                    int tmp = Properties.Height;
                    Properties.Height = Properties.Width;
                    Properties.Width = tmp;
                }

                IsOpen = true;
                CaptureMode = ECaptureModeType.Stop;
                return true;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.Open ({1})", Info.Identifier, e.Message);
                return false;
            }
        }

        public override void Close() {
            // MIL 역순 해제: Buffer → Digitizer → System → Application (Pitfall 6)
            if (MilBuffer     != MIL.M_NULL) { MIL.MbufFree(MilBuffer);       MilBuffer     = MIL.M_NULL; }
            if (MilDigitizer  != MIL.M_NULL) { MIL.MdigFree(MilDigitizer);    MilDigitizer  = MIL.M_NULL; }
            if (MilSystem     != MIL.M_NULL) { MIL.MsysFree(MilSystem);       MilSystem     = MIL.M_NULL; }
            if (MilApplication!= MIL.M_NULL) { MIL.MappFree(MilApplication);  MilApplication= MIL.M_NULL; }
            IsOpen = false;
        }

        public override HImage GrabHalconImage() {
#if SIMUL_MODE
            // D-11: SIMUL_MODE 에서는 base 파일 grab 경로(LastHalconImage) 로 폴백
            return LastHalconImage;
#else
            // 라이브 스트리밍 중에는 라이브 스레드가 MilBuffer 를 점유한다.
            // 여기서 또 MdigGrab 하면 같은 버퍼를 동시에 건드려 충돌하므로, 최신 프레임만 반환한다.
            if (CaptureMode == ECaptureModeType.Streaming) {
                return LastHalconImage;
            }

            try {
                // 단발 grab → 버퍼에서 독립 HImage(복사본) 획득
                HImage grabbed = GrabFromBuffer();
                if (grabbed == null) {
                    return null;
                }

                lock (Interlock) {
                    // HikCamera.OnGrabResult L472-473 동일 패턴 — 이전 프레임 해제 후 교체
                    if (LastGrabHalconImage != null) {
                        LastGrabHalconImage.Dispose();
                    }
                    LastGrabHalconImage = grabbed;
                }
                Interlocked.Increment(ref imageCount);
                return LastHalconImage;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.GrabHalconImage ({1})", Name, e.Message);
                Interlocked.Increment(ref errorCount);
                return null;
            }
#endif
        }

#if !SIMUL_MODE
        /// <summary>
        /// MilBuffer 로 단발 grab 한 뒤 host 메모리에서 독립 HImage(복사본)를 만들어 반환한다.
        /// 단발 grab(GrabHalconImage)과 라이브 루프(LiveLoop)가 공통으로 사용한다.
        /// MilBuffer 는 매 grab 마다 재사용되므로, 반드시 버퍼와 분리된 복사본을 반환해야 한다.
        /// </summary>
        private HImage GrabFromBuffer() {
            // 동기 단발 grab (free-run / 소프트 트리거)
            MIL.MdigGrab(MilDigitizer, MilBuffer);

            // 버퍼의 실제 크기를 조회해서 변환에 사용한다.
            // Info.Width/Height(WIDTH_CXP 등 TBD 상수)와 실제 카메라 해상도가 다르면 깨지므로,
            // 하드코딩 상수 대신 버퍼 기준으로 변환한다.
            MIL_INT bufW = MIL.M_NULL;
            MIL_INT bufH = MIL.M_NULL;
            MIL.MbufInquire(MilBuffer, MIL.M_SIZE_X, ref bufW);
            MIL.MbufInquire(MilBuffer, MIL.M_SIZE_Y, ref bufH);
            int width  = (int)bufW;
            int height = (int)bufH;

            // host 포인터 / 한 행의 바이트 수(pitch) 획득 (MbufPointerAccess 예제 패턴, Pitfall 2)
            MIL_INT hostPtr   = MIL.M_NULL;
            MIL_INT pitchByte = MIL.M_NULL;
            MIL.MbufControl(MilBuffer, MIL.M_LOCK, MIL.M_DEFAULT);
            MIL.MbufInquire(MilBuffer, MIL.M_HOST_ADDRESS, ref hostPtr);
            MIL.MbufInquire(MilBuffer, MIL.M_PITCH_BYTE,   ref pitchByte);

            // host 메모리에 매핑이 안 된 버퍼(보드 전용 메모리)면 변환 불가
            if (hostPtr == MIL.M_NULL) {
                MIL.MbufControl(MilBuffer, MIL.M_UNLOCK, MIL.M_DEFAULT);
                return null;
            }

            IntPtr ptr = new IntPtr((long)hostPtr); // Pitfall 3: 명시적 변환
            HImage sourceImage = null;
            if (pitchByte == width) {
                // 행 padding 이 없는 경우 — 포인터로 wrap 한 뒤 독립 복사본으로 분리한다.
                // (wrap 상태로 두면 UNLOCK/다음 grab 시 버퍼가 바뀌어 화면이 깨짐)
                HImage wrap = new HImage();
                wrap.GenImage1("byte", width, height, ptr);
                sourceImage = wrap.CopyImage();
                wrap.Dispose();
            }
            else {
                // pitch > width — 행 단위로 padding 을 제거하며 복사 (이미 독립 복사본)
                sourceImage = CreateImageFromPaddedBuffer(ptr, width, height, (int)pitchByte);
            }
            MIL.MbufControl(MilBuffer, MIL.M_UNLOCK, MIL.M_DEFAULT);

            // 회전 처리 (HikCamera.OnGrabResult L458-470 동일)
            HImage rotatedImage = sourceImage;
            if (Info.RotateAngle == ERotateAngleType._90) {
                rotatedImage = sourceImage.RotateImage(90.0, "constant");
                sourceImage.Dispose();
            }
            else if (Info.RotateAngle == ERotateAngleType._180) {
                rotatedImage = sourceImage.RotateImage(180.0, "constant");
                sourceImage.Dispose();
            }
            else if (Info.RotateAngle == ERotateAngleType._270) {
                rotatedImage = sourceImage.RotateImage(270.0, "constant");
                sourceImage.Dispose();
            }
            return rotatedImage;
        }
#endif

        /// <summary>
        /// MIL 버퍼에 pitch padding이 존재할 때 행 단위로 복사하여 연속 HImage를 생성한다.
        /// pitch > width 인 경우에만 호출. (Pitfall 4: 128MP 대용량 row-copy)
        /// </summary>
        private HImage CreateImageFromPaddedBuffer(IntPtr src, int width, int height, int pitchByte) {
            byte[] packed = new byte[width * height];
            unsafe {
                byte* s = (byte*)src;
                for (int row = 0; row < height; row++) {
                    Marshal.Copy((IntPtr)(s + (long)row * pitchByte), packed, row * width, width);
                }
            }
            HImage img = new HImage();
            GCHandle h = GCHandle.Alloc(packed, GCHandleType.Pinned);
            try {
                img.GenImage1("byte", width, height, h.AddrOfPinnedObject());
                // GenImage1 가 포인터 wrap일 수 있으므로 독립 복사본 반환
                HImage copy = img.CopyImage();
                img.Dispose();
                return copy;
            }
            finally {
                h.Free();
            }
        }

        /// <summary>
        /// MIL 동기 grab 방식에서는 MdigGrab 자체가 소프트웨어 트리거 단발이므로
        /// HIK 처럼 StartGrabbing 루프가 불필요. 상태값만 갱신한다.
        /// </summary>
        public override bool SetSoftwareTriggerMode(bool threading = false) {
            CaptureMode   = ECaptureModeType.Trigger;
            TriggerSource = ETriggerSource.Software;
            return true;
        }

        public override bool SetTriggerMode(ETriggerSource source, bool forcing = false, bool threading = false) {
            CaptureMode   = ECaptureModeType.Trigger;
            TriggerSource = source;
            return true;
        }

#if !SIMUL_MODE
        // MIL 라이브(연속 영상).
        //  HIK 는 SDK 가 프레임마다 OnGrabResult 콜백을 밀어주지만, MIL 동기 grab 방식에는 콜백이 없다.
        //  그래서 백그라운드 스레드에서 GrabFromBuffer 를 반복 호출하고, 프레임마다 GuiReadyForDisplay 로
        //  UI(DeviceSelector.OnImageReady)에 통지한다. (HikCamera.StartStream/OnGrabResult 대응)

        /// <summary>
        /// 라이브 시작 — 백그라운드 grab 스레드를 띄운다. (카메라 창에서 장치 선택 시 자동 호출)
        /// </summary>
        public override bool StartStream() {
            // 이미 스트리밍 중이면 중복 시작하지 않는다.
            if (CaptureMode == ECaptureModeType.Streaming) {
                return true;
            }
            // 카메라가 안 열려 있으면 grab 할 수 없다.
            if (!IsOpen) {
                return false;
            }

            _liveRunning = true;
            CaptureMode  = ECaptureModeType.Streaming;

            _liveThread = new Thread(LiveLoop);
            _liveThread.IsBackground = true;
            _liveThread.Name = Name + "_MilLive";
            _liveThread.Start();
            return true;
        }

        /// <summary>
        /// 라이브 정지 — grab 스레드를 멈추고 정리한다.
        /// </summary>
        public override void StopStream() {
            // 이미 멈춰 있으면 할 일 없음.
            if (CaptureMode == ECaptureModeType.Stop) {
                return;
            }

            // 루프 종료 신호 후 스레드가 끝날 때까지 잠깐 대기한다.
            _liveRunning = false;
            if (_liveThread != null) {
                if (_liveThread.IsAlive) {
                    _liveThread.Join(1000);
                }
                _liveThread = null;
            }

            CaptureMode = ECaptureModeType.Stop;
            base.StopStream(); // 마지막 프레임 정리(ClearLastFrame)
        }

        /// <summary>
        /// 라이브 grab 루프 — _liveRunning 동안 계속 grab 하여 최신 프레임을 갱신하고 UI 에 통지한다.
        /// </summary>
        private void LiveLoop() {
            while (_liveRunning) {
                try {
                    // 한 프레임 grab (버퍼와 분리된 독립 복사본)
                    HImage frame = GrabFromBuffer();
                    if (frame != null) {
                        lock (Interlock) {
                            // 이전 프레임 해제 후 교체
                            if (LastGrabHalconImage != null) {
                                LastGrabHalconImage.Dispose();
                            }
                            LastGrabHalconImage = frame;
                        }
                        Interlocked.Increment(ref imageCount);

                        // UI 에 "새 프레임 준비됨" 통지 (HikCamera.OnGrabResult L483-485 동일)
                        if (GuiReadyForDisplay != null) {
                            GuiReadyForDisplay(Name);
                        }
                    }
                }
                catch (Exception e) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.LiveLoop ({1})", Name, e.Message);
                    Interlocked.Increment(ref errorCount);
                    // 에러가 연속으로 나면 로그/CPU 폭주를 막기 위해 잠깐 쉰다.
                    Thread.Sleep(50);
                }

                // CPU 양보 (실제 프레임 속도는 MdigGrab 가 카메라 프레임을 기다리며 결정)
                Thread.Sleep(1);
            }
        }
#endif
    }
}
