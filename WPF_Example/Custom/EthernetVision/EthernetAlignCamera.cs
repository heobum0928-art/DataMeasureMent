//260623 hbk Phase 58
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.IO;

namespace ReringProject.Device {

    /// <summary>
    /// Hikvision GigE 정렬 카메라 독립 래퍼 (DeviceHandler 등록 없음).
    /// HikCamera 를 composition 으로 보유하며 Connect/Grab/Live/Stop/Close 만 노출.
    /// Phase 58 AV-02 — 기존 Grabber 카메라 경로 무수정.
    /// </summary>
    public class EthernetAlignCamera {

        private const int DEFAULT_WIDTH = 5120;     // MV-CH250-90GM 플레이스홀더 (Open 후 실 해상도로 덮어씀)
        private const int DEFAULT_HEIGHT = 5120;
        // D-04 SIMUL/실패 폴백. 260818 hbk D 드라이브 없는 PC 대응 — SystemSetting.AlignFallbackImagePath(INI) 로 설정화(기본값은 여전히 D:\align_test.bmp).
        private static readonly string[] IMAGE_EXTENSIONS = { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff" };

        private HikCamera _hikCamera = null;    // composed instance (HikCamera 미수정, DeviceHandler 미등록)
        private string _cameraIp = null;

        //260630 hbk — SIMUL 캘 테스트: 폴더 순차 이미지. null=폴백 단일이미지 모드.
        private string[] _simulImagePaths = null;
        private int _simulImageIndex = 0;

        /// <summary>
        /// SIMUL 캘 테스트용 폴더 등록. 이미지 파일 목록을 알파벳순 정렬 후 저장.
        /// </summary>
        public void LoadSimulFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                _simulImagePaths = null;
                return;
            }
            var extensions = new System.Collections.Generic.HashSet<string>(
                IMAGE_EXTENSIONS, StringComparer.OrdinalIgnoreCase);
            var paths = System.IO.Directory.GetFiles(folder);
            var sorted = System.Array.FindAll(paths,
                p => extensions.Contains(Path.GetExtension(p)));
            System.Array.Sort(sorted, StringComparer.OrdinalIgnoreCase);
            _simulImagePaths = sorted;
            _simulImageIndex = 0;
            Logging.PrintLog((int)ELogType.Camera,
                "[ETHERNET] SimulCalib 폴더 등록: {0}장 ({1})", sorted.Length, folder);
        }

        /// <summary>
        /// SIMUL 캘 순차 인덱스를 0 으로 리셋 (TCP START 수신 시 호출).
        /// </summary>
        public void ResetSimulIndex()
        {
            _simulImageIndex = 0;
        }

        /// <summary>실 카메라가 열려 있으면 true. HikCamera 인스턴스 없거나 Open 미완료면 false.</summary>
        public bool IsOpen {
            get { return (_hikCamera != null) && _hikCamera.IsOpen; }
        }

        /// <summary>
        /// 지정한 IP 의 Hikvision GigE 카메라에 연결.
        /// 실패 시 false 반환 (예외 throw 없음).
        /// </summary>
        /// <param name="ip">카메라 IP 주소 (예: "192.168.1.100")</param>
        /// <returns>연결 성공 여부</returns>
        public bool Connect(string ip) {
            try {
                _cameraIp = ip;

                // 260623 hbk Phase 58 review-fix WR-01/IN-01:
                // HikCamera.EnumerateDevice 는 공유 정적 DeviceList 를 Clear() 후 재빌드.
                // Grabber 카메라 열거와 충돌 방지를 위해 장치 확인을 HikCamera 인스턴스 생성 전에 수행.
                // 또한 SystemHandler 는 Grabber 카메라(DeviceHandler) 초기화 이후 마지막에 이 Connect 를
                // 호출하므로, Grabber 열거가 끝난 뒤에 실행됨 — 동시 열거 금지.
                // 260724 hbk 버그 수정: EnumerateDevice(string) 의 인자는 장치 "타입" 필터("USB"/"GIGE" 부분
                //  문자열 매치)이지 IP/이름 매처가 아니다(DeviceHandler.cs:90 의 무인자 호출이 정상 사용례).
                //  여기서 실제 IP/카메라이름 문자열을 그대로 넘기면 "USB"/"GIGE" 어느 쪽도 안 걸려 필터가 0이 되고,
                //  실제 카메라가 연결되어 있어도 EnumDevices 가 0건을 반환했다(실기 재현: 종일 "no device found").
                //  GigE 전용 카메라이므로 "GIGE" 로 고정 — 이후 Open(ip) 가 DeviceList 를 이름/IP/FriendlyName
                //  으로 검색(HikCamera.GetDeviceIndex)하므로 실제 매칭은 거기서 이뤄진다.
                int deviceCount = HikCamera.EnumerateDevice("GIGE");   // enumerate FIRST — DeviceList 재빌드
                if (deviceCount == 0) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Connect: no device found for {0}", ip);
                    return false;
                }
                // 260724 hbk 임시 진단 로그 — 실제 검색된 장치 목록(이름/IP) 확인용. 원인 확정 후 제거 검토.
                for (int di = 0; di < deviceCount; di++) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] found[{0}]: name={1} ip={2} friendly={3}",
                        di, HikCamera.GetDeviceUserDefinedName(di), HikCamera.GetDeviceIpAddress(di), HikCamera.GetDeviceFriendlyName(di));
                }

                DisplayConfig config = new DisplayConfig();
                DeviceInfo info = new DeviceInfo(
                    ECameraType.HIK,
                    ECaptureImageType.Gray8,
                    ETriggerSource.Software,
                    ip,
                    DEFAULT_WIDTH,
                    DEFAULT_HEIGHT,
                    false,
                    false);

                _hikCamera = new HikCamera(config, info);  // construct AFTER count confirmed

                bool bOpened = _hikCamera.Open(ip);
                return bOpened;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Connect failed: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 카메라에서 단일 이미지를 소프트웨어 트리거로 취득.
        /// 미연결 또는 Grab 실패 시 SystemSetting.AlignFallbackImagePath(기본값 D:\align_test.bmp) 폴백 이미지를 반환.
        /// 반환된 HImage 는 호출자가 Dispose() 책임.
        /// </summary>
        /// <returns>취득 이미지(HImage). 폴백도 실패하면 null.</returns>
        // 260724 hbk 임시 진단 — Logging.PrintLog 는 비동기라 크래시 직전 메시지가 유실될 수 있어 동기 기록.
        private static void SyncDiag(string msg) {
            try {
                System.IO.File.AppendAllText(@"D:\Data\Camera\crash_diag.log",
                    string.Format("{0} {1}\r\n", DateTime.Now.ToString("HH:mm:ss.fff"), msg));
            }
            catch { }
        }

        public HImage Grab() {
            SyncDiag("EthernetAlignCamera.Grab() 진입");
            try {
                SyncDiag("IsOpen=" + IsOpen);
                if (IsOpen) {
                    SyncDiag("_hikCamera.GrabHalconImage() 호출 직전(호출 스레드 그대로, 진단코드 최소화)");
                    HImage img = _hikCamera.GrabHalconImage();
                    SyncDiag("_hikCamera.GrabHalconImage() 반환됨");
                    if (img != null) {
                        return img;
                    }
                }
                return LoadFallbackImage();
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Grab failed: {0}", ex.Message);
                return LoadFallbackImage();
            }
        }

        /// <summary>
        /// 연속 스트리밍(라이브) 시작.
        /// Phase 61 에서 뷰어에 표시 예정.
        /// </summary>
        /// <returns>스트림 시작 성공 여부</returns>
        public bool Live() {
            try {
                if (!IsOpen) {
                    return false;
                }
                return _hikCamera.StartStream();
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Live failed: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>연속 스트리밍 중지.</summary>
        public void Stop() {
            try {
                if (_hikCamera != null) {
                    _hikCamera.StopStream();
                }
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Stop failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Live 스트리밍 중 최근 수신 프레임을 재트리거 없이 가져온다(뷰어 주기적 갱신용).
        /// 아직 프레임을 못 받았으면 null. 반환된 HImage 는 호출자가 Dispose() 책임.
        /// </summary>
        public HImage PeekLastImage() {
            try {
                return _hikCamera?.LastHalconImage;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] PeekLastImage failed: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>카메라 연결 해제 및 핸들 해제.</summary>
        public void Close() {
            try {
                if (_hikCamera != null) {
                    _hikCamera.Close();
                    _hikCamera = null;
                }
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Close failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// SystemSetting.AlignFallbackImagePath(기본값 D:\align_test.bmp)를 읽어 HImage 로 반환.
        /// 컬러 이미지이면 Gray8 으로 변환.
        /// 반환된 HImage 는 호출자가 Dispose() 책임.
        /// </summary>
        private HImage LoadFallbackImage() {
            // 260623 hbk Phase 58 review-fix WR-02:
            // loaded 를 try 외부에 선언하여 CountChannels() 등 HALCON 연산이 예외를 던질 경우
            // finally 블록에서 반드시 Dispose — 성공 경로(null 로 세팅)는 no-op.
            HImage loaded = null;
            try {
                // 260630 hbk — SIMUL 캘: 폴더 등록 시 순차 이미지 반환. 마지막 도달 시 마지막 이미지 반복.
                // 260818 hbk SystemHandler.Handle 대신 SystemSetting.Handle 직접 참조 — DeviceHandler.AddVirtualCamera 에서
                //  겪은 것과 같은 순환 초기화 NullReferenceException 을 원천 차단(SystemSetting.Handle 은 SystemHandler
                //  생성자보다 먼저 완전히 만들어짐).
                string imagePath = SystemSetting.Handle.AlignFallbackImagePath;
                bool bHasSimul = _simulImagePaths != null && _simulImagePaths.Length > 0;
                if (bHasSimul)
                {
                    int idx = _simulImageIndex;
                    if (idx >= _simulImagePaths.Length)
                    {
                        idx = _simulImagePaths.Length - 1; // 마지막 이미지 반복
                    }
                    imagePath = _simulImagePaths[idx];
                    _simulImageIndex = idx + 1;
                    Logging.PrintLog((int)ELogType.Camera,
                        "[ETHERNET] SimulCalib Grab [{0}/{1}]: {2}",
                        idx + 1, _simulImagePaths.Length, Path.GetFileName(imagePath));
                }
                if (!File.Exists(imagePath)) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] fallback image missing: {0}", imagePath);
                    return null;
                }
                loaded = new HImage();
                loaded.ReadImage(imagePath);
                if (loaded.CountChannels().I > 1) {
                    HImage gray = loaded.Rgb1ToGray();
                    loaded.Dispose();
                    loaded = null;  // finally no-op
                    return gray;
                }
                HImage result = loaded;
                loaded = null;  // finally no-op — 호출자가 Dispose 책임
                return result;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] LoadFallbackImage failed: {0}", ex.Message);
                return null;
            }
            finally {
                loaded?.Dispose();  // loaded 가 null 이 아니면(= 예외 발생) 누수 방지 Dispose
            }
        }
    }
}
