//260623 hbk Phase 58
using System;
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// 이더넷 정렬 카메라 독립 싱글턴 핸들러 (D-03).
    /// EthernetAlignCamera 인스턴스를 소유하며 모드 게이트 + 지연 연결(D-04)로 초기화.
    /// Phase 58 AV-02 — 기존 Grabber(DeviceHandler/HikCamera) 무수정.
    /// </summary>
    public sealed class EthernetVisionHandler {
        public static EthernetVisionHandler Handle { get; } = new EthernetVisionHandler();

        /// <summary>이더넷 정렬 카메라 인스턴스. Mode==None 이면 null 유지.</summary>
        public EthernetAlignCamera Camera { get; private set; }

        //260624 hbk Phase 59 — D-02: Shape matching align 서비스 (handler 소유, stateless). Mode 무관 항상 생성.
        public AlignShapeMatchService Matcher { get; private set; }

        //260624 hbk Phase 60 — D-01: 피커센터 캘 서비스 (handler 소유, stateful). Mode 무관 항상 생성.
        public PickerCenterCalibrationService PickerCal { get; private set; }

        /// <summary>Connect 성공 시 true. Mode==None 또는 연결 실패 시 false.</summary>
        public bool IsInitialized { get; private set; } = false;

        //260630 hbk — TCP ALIGN_CALIB STEP 완료 시 UI 뷰어 갱신 콜백.
        // (Grab 이미지, vizXld). BottomVisionView.AttachSharedViewer 에서 등록.
        public Action<HImage, HObject> OnCalibStepViewer { get; set; }

        //260630 hbk — TCP ALIGN_CALIB END 완료 시 UI 갱신 콜백.
        // (row, col, rad, vizXld). 라벨 + 뷰어 피팅원 표시. BottomVisionView.AttachSharedViewer 에서 등록.
        public Action<double, double, double, HObject> OnCalibEndViewer { get; set; }

        //quick-260812: TCP ALIGN_CALIB 실패를 화면에도 알리는 콜백(문구 1개).
        // 지금까지 자동경로 실패는 로그 파일에만 남아 운영자가 알 방법이 없었다.
        // BottomVisionView.AttachSharedViewer 에서 등록. UI 스레드 마샬링은 호출 측 책임(형제 콜백과 동일).
        public Action<string> OnCalibError { get; set; }

        private EthernetVisionHandler() {
        }

        // D-04: 모드 게이트 + 지연 연결. None 이면 연결 시도조차 안 함. Tray/Bottom 이면 INI IP 로 연결.
        // 실패해도 throw 금지 — try-catch 로 격리, Grabber 무영향.
        public void Initialize() {
            //quick-260807-htd: 예외 경로에서도 같은 알람을 띄우려면 모드/설정값이 try 밖에 살아 있어야 한다.
            bool bModeOn = false;
            string camIp = null;
            // 알람 문구를 Tray/Bottom 으로 가르려면 모드도 예외 경로까지 살아 있어야 한다(위와 같은 이유).
            EEthernetVisionMode activeMode = EEthernetVisionMode.None;
            try {
                //260624 hbk Phase 59 — D-02: Matcher 는 stateless → 모드/연결 결과 무관하게 항상 생성
                Matcher = new AlignShapeMatchService();
                //260624 hbk Phase 60 — D-01: PickerCal stateful → 모드/연결 결과 무관 항상 생성
                PickerCal = new PickerCenterCalibrationService();

                activeMode = SystemSetting.Handle.EthernetVisionMode;
                bool bModeOff = activeMode == EEthernetVisionMode.None;
                if (bModeOff) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] mode = None, skip connect");
                    IsInitialized = false;
                    return;
                }
                bModeOn = true;

                Camera = new EthernetAlignCamera();
                camIp = SystemSetting.Handle.EthernetCameraIp;
                bool bConnected = Camera.Connect(camIp);

                IsInitialized = bConnected;
                if (bConnected) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connected: {0}", camIp);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connect failed (fallback active): {0}", camIp);
                    ShowConnectFailAlarm(activeMode, camIp, null);
                }
            }
            catch (Exception ex) {
                IsInitialized = false;
                //260624 hbk Phase 59 — 예외 경로에서도 Matcher null 방지
                if (Matcher == null) {
                    Matcher = new AlignShapeMatchService();
                }
                //260624 hbk Phase 60 — 예외 경로에서도 PickerCal null 방지
                if (PickerCal == null) {
                    PickerCal = new PickerCenterCalibrationService();
                }
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Initialize error: {0}", ex.Message);
                //quick-260807-htd: 모드가 켜져 있었는데 예외로 죽은 것 = 사용자 입장에선 똑같은 "연결 실패"다.
                if (bModeOn) {
                    ShowConnectFailAlarm(activeMode, camIp, ex.Message);
                }
            }
        }

        //quick-260810-e1t: 프로그램 종료 시 이 카메라 연결을 끊는 진입점이 지금까지 없었다
        // (EthernetAlignCamera.Close() 를 호출하는 곳이 코드 전체에 단 한 곳도 없었음) → 앱이 꺼져도
        // 카메라 연결이 안 끊긴 채 남는 문제. Camera 는 Mode==None 이면 null 이므로 반드시 가드.
        // 절대 throw 하지 않는다 — Initialize() 와 동일한 방어적 컨벤션(SystemHandler.Release() 가
        // 앱 종료 경로에서 호출하므로 여기서 예외가 새면 다른 리소스 정리가 중단된다).
        public void Release() {
            try {
                if (Camera != null) {
                    Camera.Close();
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] camera closed on release");
                }
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Release error: {0}", ex.Message);
            }
        }

        //quick-260807-htd: 연결 실패가 로그에만 남아 사용자가 몰랐다 → 기존 카메라 실패 알림과 같은 수단으로 통일.
        // 스레드 마샬링을 여기서 하지 않는 이유: CustomMessageBox.Show 가 내부에서 이미
        // App.Current.Dispatcher.BeginInvoke 로 넘기므로 호출 스레드 무관하게 안전하다(이중 마샬링 금지).
        // isAutoClosing=false : 기본 7초 자동닫힘을 끈다. 알람은 사용자가 직접 닫아야 한다.
        private void ShowConnectFailAlarm(EEthernetVisionMode activeMode, string camIp, string exMessage) {
            try {
                string target = camIp;
                if (string.IsNullOrEmpty(target)) {
                    target = "(설정값 없음)";
                }
                string szCameraLabel = ResolveAlignCameraLabel(activeMode);
                string message = string.Format(
                    "{0} 정렬 카메라(이더넷 / Hik GigE)에 연결하지 못했습니다.\n\n" +
                    "설정값 : {1}\n" +
                    "(설정 창 > ETHERNET_VISION > EthernetCameraIp)\n\n" +
                    "확인할 것 : 카메라 전원 / 랜선 / IP 대역 / 다른 프로그램의 카메라 점유\n" +
                    "자세한 원인은 Camera 로그의 [ETHERNET] 항목에 있습니다.\n\n" +
                    "연결될 때까지 정렬은 폴백 이미지로 동작합니다. (일반 검사 기능은 영향 없음)",
                    szCameraLabel, target);
                if (string.IsNullOrEmpty(exMessage) == false) {
                    message = message + "\n\n예외 : " + exMessage;
                }
                CustomMessageBox.Show(szCameraLabel + " 카메라 연결 실패", message, System.Windows.MessageBoxImage.Error, true, false);
            }
            catch (Exception ex) {
                //알림 실패가 초기화를 막으면 안 된다 (CustomMessageBox 내부도 방어하지만 이중 방어)
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] connect fail alarm show error: {0}", ex.Message);
            }
        }

        // 알람 문구용 카메라 이름. Tray 를 쓰는 PC 에서 "BottomAlign 연결 실패" 가 뜨면
        //  운영자가 엉뚱한 장비를 확인하게 되므로 모드별로 갈라준다.
        private static string ResolveAlignCameraLabel(EEthernetVisionMode activeMode) {
            if (activeMode == EEthernetVisionMode.Tray) {
                return "TrayAlign";
            }
            if (activeMode == EEthernetVisionMode.Bottom) {
                return "BottomAlign";
            }
            return "정렬"; // 모드 ON 일 때만 알람이 뜨므로 실제로는 도달하지 않는 방어값
        }
    }
}
