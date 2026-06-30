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
        private const double CALIB_SEARCH_RADIUS_DEFAULT = 99999.0; //260630 hbk Phase 60: 미설정 시 전 이미지 커버
        // WR-03 fix //260624 hbk: 피커센터 미캘 판정 임계 — AlignShapeMatchService.PICKER_CENTER_ZERO_EPS 와 동일.
        // 두 판정 기준을 단일 소스로 통일. AlignShapeMatchService 는 이 public const 를 참조.
        public const double PICKER_CENTER_ZERO_EPS = 1e-6; //260624 hbk Phase 60

        partial void AfterLoad()
        {
            RestorePcRoleDefault();
            RestoreEthernetVisionDefault(); //260623 hbk Phase 58
            RestorePickerCenterDefault(); //260624 hbk Phase 60
            RestoreCalibSearchDefault(); //260630 hbk Phase 60
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

        //260630 hbk Phase 60: 구 INI 에 CalibSearchRadius 키 부재 시 0 으로 로드 → 99999 로 복원 (전 이미지 커버).
        private void RestoreCalibSearchDefault()
        {
            bool bRadiusMissing = CalibSearchRadius <= 0.0;
            if (bRadiusMissing)
            {
                CalibSearchRadius = CALIB_SEARCH_RADIUS_DEFAULT;
            }
        }

        //260624 hbk Phase 60 — D-04: 피커센터 기본값 0 = 미캘 상태(정상값). reflection Load 가
        // 누락 키를 0 으로 로드하는 것이 곧 올바른 미캘 의미이므로 복원 불필요.
        // WR-03 fix //260624 hbk: == 0.0 → PICKER_CENTER_ZERO_EPS 임계 비교로 통일
        //   (AlignShapeMatchService 와 동일 기준 — INI 라운드트립 부동소수 오차 허용).
        // IN-02 fix //260624 hbk: 빈 if 블록 제거 — 복원 불필요 이유를 메서드 주석으로 명시.
        // PickerCenterRow/Col 기본값 0 = 미캘 상태(정상 초기값).
        // 향후 비-0 머신 기본값 도입 시 이 메서드에서 복원 로직 추가.
        private void RestorePickerCenterDefault()
        {
            // 미캘 판정: |row|, |col| 모두 PICKER_CENTER_ZERO_EPS 이하 → 복원 불필요.
            // (0,0 이 올바른 미캘 초기값이므로 별도 복원 없음.)
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

        //260630 hbk Phase 60: 피커캘 STEP 검색 ROI. TCP $ALIGN_CALIB:BOTTOM,STEP@ 수신 시 Grab→TryAddStep 에 전달.
        // 기본값 Row=0/Col=0/Radius=99999 → 전 이미지 커버 (HALCON ReduceDomain 이 이미지 도메인 내부로 클립).
        [Category("ETHERNET_VISION")]
        public double CalibSearchRow { get; set; } = 0.0;

        [Category("ETHERNET_VISION")]
        public double CalibSearchCol { get; set; } = 0.0;

        [Category("ETHERNET_VISION")]
        public double CalibSearchRadius { get; set; } = 99999.0;
    }
}
