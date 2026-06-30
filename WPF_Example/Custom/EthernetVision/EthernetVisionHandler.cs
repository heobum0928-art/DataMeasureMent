//260623 hbk Phase 58
using System;
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Setting;
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

        private EthernetVisionHandler() {
        }

        // D-04: 모드 게이트 + 지연 연결. None 이면 연결 시도조차 안 함. Tray/Bottom 이면 INI IP 로 연결.
        // 실패해도 throw 금지 — try-catch 로 격리, Grabber 무영향.
        public void Initialize() {
            try {
                //260624 hbk Phase 59 — D-02: Matcher 는 stateless → 모드/연결 결과 무관하게 항상 생성
                Matcher = new AlignShapeMatchService();
                //260624 hbk Phase 60 — D-01: PickerCal stateful → 모드/연결 결과 무관 항상 생성
                PickerCal = new PickerCenterCalibrationService();

                bool bModeOff = SystemSetting.Handle.EthernetVisionMode == EEthernetVisionMode.None;
                if (bModeOff) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] mode = None, skip connect");
                    IsInitialized = false;
                    return;
                }

                Camera = new EthernetAlignCamera();
                string camIp = SystemSetting.Handle.EthernetCameraIp;
                bool bConnected = Camera.Connect(camIp);

                IsInitialized = bConnected;
                if (bConnected) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connected: {0}", camIp);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connect failed (fallback active): {0}", camIp);
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
            }
        }
    }
}
