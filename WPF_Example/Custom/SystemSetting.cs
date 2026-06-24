using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Setting {
    // PC별 CXP 카메라 역할 (TopBottom / Side)
    public enum ECameraRole {
        TopBottom = 0,   // PC1: Top + Bottom 시퀀스 담당
        Side      = 1,   // PC2: Side 시퀀스 담당
    }

    //project 별 설정 항목 추가.
    public partial class SystemSetting {
        // INI 직렬화용 int 백킹 프로퍼티 (SystemSetting.Save/Load switch(type) 가 Int32 지원)
        // enum 은 switch(type) 에 case 없으므로 D-12 AlgorithmType string 선례와 동일 패턴 적용
        [Category("System|Camera")]
        public int CameraRoleValue { get; set; } = 0;   // 0 = TopBottom (기본값)

        // 코드 사용용 enum 변환 프로퍼티 (직렬화 제외 — [Browsable(false)])
        [Browsable(false)]
        public ECameraRole CameraRole {
            get { return (ECameraRole)CameraRoleValue; }
            set { CameraRoleValue = (int)value; }
        }

        //260622 hbk Phase 48
        // PROTO-01: PcRole 기본값(1) 이 구 INI 에 키 부재 시 0 으로 로드되는 문제 방어
        // (reference_parambase_missing_key_zeroes_default.md — Int32 case 에서 0 덮어씀).
        // AfterLoad() = Load() 완료 직후 호출되는 partial 메서드 구현부.
        private const int PC_ROLE_DEFAULT = 1; //260622 hbk Phase 48
        private const double ETHERNET_PIXEL_RESOLUTION_DEFAULT = 8.652; //260623 hbk Phase 58

        partial void AfterLoad()
        {
            RestorePcRoleDefault();
            RestoreEthernetVisionDefault(); //260623 hbk Phase 58
            RestorePickerCenterDefault(); //260624 hbk Phase 60
        }

        // 260622 hbk Phase 48
        // PROTO-01: PcRole==0(구 INI 누락 로드) 이면 PC1 기본값(=1) 으로 복원.
        // D-00 준수: 헝가리언(bPcRoleMissing), if/else, 매직넘버 금지(PC_ROLE_DEFAULT).
        private void RestorePcRoleDefault()
        {
            bool bPcRoleMissing = PcRole == 0;
            if (bPcRoleMissing)
            {
                PcRole = PC_ROLE_DEFAULT;
            }
        }

        //260623 hbk Phase 58
        // AV-01: 구 INI 에 [ETHERNET_VISION] PixelResolution 키 부재 시 0 으로 로드되는 문제 방어 → 8.652 복원.
        private void RestoreEthernetVisionDefault()
        {
            bool bPixelResolutionMissing = EthernetPixelResolution <= 0.0;
            if (bPixelResolutionMissing)
            {
                EthernetPixelResolution = ETHERNET_PIXEL_RESOLUTION_DEFAULT;
            }
        }

        //260624 hbk Phase 60 — D-04: 피커센터 기본값 0 = 미캘 상태(정상값). reflection Load 가
        // 누락 키를 0 으로 로드하는 것이 곧 올바른 미캘 의미이므로 복원 불필요 — 의도 명시용 no-op 가드.
        // 향후 비-0 머신 기본값 도입 시 이 메서드에서 복원 로직 추가.
        private void RestorePickerCenterDefault()
        {
            bool bPickerCenterUncalibrated = (PickerCenterRow == 0.0) && (PickerCenterCol == 0.0);
            if (bPickerCenterUncalibrated)
            {
                // 미캘 상태 유지 — 별도 복원 없음 (0,0 = 정상 초기값)
            }
        }

        //260623 hbk Phase 58 — AV-01: [ETHERNET_VISION] INI section
        [Category("ETHERNET_VISION")]
        public int EthernetVisionModeValue { get; set; } = 0;   // 0 = None

        [Browsable(false)]
        public EEthernetVisionMode EthernetVisionMode {
            get { return (EEthernetVisionMode)EthernetVisionModeValue; }
            set { EthernetVisionModeValue = (int)value; }
        }

        [Category("ETHERNET_VISION")]
        public string EthernetCameraIp { get; set; } = "192.168.1.100"; //260623 hbk Phase 58

        [Category("ETHERNET_VISION")]
        //260623 hbk Phase 58: EthernetExposure 적용은 Phase 59/61 카메라 런타임 배선 시 (SetFloatValue ExposureTime) — 현재는 config 저장만
        public double EthernetExposure { get; set; } = 10000.0; //260623 hbk Phase 58

        [Category("ETHERNET_VISION")]
        public double EthernetPixelResolution { get; set; } = 8.652; //260623 hbk Phase 58

        //260624 hbk Phase 60 — D-04: AV-05 피커 회전중심 (머신 단위 HW 캘 결과, 레시피 아님). 0 = 미캘.
        [Category("ETHERNET_VISION")]
        public double PickerCenterRow { get; set; } = 0.0;

        [Category("ETHERNET_VISION")]
        public double PickerCenterCol { get; set; } = 0.0;
    }
}
