using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Matrox.MatroxImagingLibrary;

namespace ReringProject.Device {
    //260602 hbk Phase 41 — CXP 카메라 MIL Lite 10.0 grab 드라이버
    public class MilCamera : VirtualCamera, IDisposable {
        private MIL_ID MilApplication = MIL.M_NULL;
        private MIL_ID MilSystem      = MIL.M_NULL;
        private MIL_ID MilDigitizer   = MIL.M_NULL;
        private MIL_ID MilBuffer      = MIL.M_NULL;

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
                MIL.MsysAlloc(MIL.M_DEFAULT, MIL.M_SYSTEM_DEFAULT,
                              MIL.M_DEFAULT, MIL.M_DEFAULT, ref MilSystem);
#endif
                if (MilSystem == MIL.M_NULL) throw new Exception("MsysAlloc failed");

                // 3. Digitizer 할당 (DCF 미사용 — M_DEFAULT)
                MIL.MdigAlloc(MilSystem, MIL.M_DEV0, "M_DEFAULT",
                              MIL.M_DEFAULT, ref MilDigitizer);
                if (MilDigitizer == MIL.M_NULL) throw new Exception("MdigAlloc failed");

                // 4. Mono8 버퍼 1회 할당 (Open 시 단일 할당, GrabHalconImage 에서 재사용 — Pitfall 5)
                MIL.MbufAlloc2d(MilSystem, Info.Width, Info.Height,
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
            try {
                // 소프트웨어 트리거 단발 동기 grab
                MIL.MdigGrab(MilDigitizer, MilBuffer);

                // host 포인터 획득 (MbufPointerAccess 예제 패턴, Pitfall 2)
                MIL_INT hostPtr   = MIL.M_NULL;
                MIL_INT pitchByte = MIL.M_NULL;
                MIL.MbufControl(MilBuffer, MIL.M_LOCK, MIL.M_DEFAULT);
                MIL.MbufInquire(MilBuffer, MIL.M_HOST_ADDRESS, ref hostPtr);
                MIL.MbufInquire(MilBuffer, MIL.M_PITCH_BYTE,   ref pitchByte);

                if (hostPtr == MIL.M_NULL) {
                    MIL.MbufControl(MilBuffer, MIL.M_UNLOCK, MIL.M_DEFAULT);
                    return null;
                }

                IntPtr ptr = new IntPtr((long)hostPtr); // Pitfall 3: 명시적 변환
                HImage sourceImage;
                if (pitchByte == Info.Width) {
                    // padding 없음 — GenImage1 직접 사용 (HikCamera.OnGrabResult L455-456 동일)
                    sourceImage = new HImage();
                    sourceImage.GenImage1("byte", Info.Width, Info.Height, ptr);
                }
                else {
                    // pitch > width — 행 단위 padding 제거 복사
                    sourceImage = CreateImageFromPaddedBuffer(ptr, Info.Width, Info.Height, (int)pitchByte);
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

                lock (Interlock) {
                    // HikCamera.OnGrabResult L472-473 동일 패턴 — null-conditional 대신 명시적 if (Phase 20 D-04 스타일)
                    if (LastGrabHalconImage != null) LastGrabHalconImage.Dispose();
                    LastGrabHalconImage = rotatedImage;
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
    }
}
