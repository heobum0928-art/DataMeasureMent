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
        //260630 hbk Phase 60 사각형 ROI 전환: 미설정 시 전 이미지 커버 기본값 (row2/col2)
        private const double CALIB_SEARCH_MAX_DEFAULT = 99999.0;

        // 피커센터 캘 스텝당 회전각(deg) 기본값. 360/각도 = 필요 스텝 수(10도 → 36스텝).
        private const double PICKER_CAL_STEP_ANGLE_DEFAULT = 10.0;
        private const double PICKER_CAL_FULL_TURN_DEG = 360.0;

        // Align 정합 검증 보관 상한 기본값. 매직넘버 금지.
        private const int ALIGN_VERIFY_KEEP_DAYS_DEFAULT = 180;
        private const int ALIGN_VERIFY_IMAGE_KEEP_DAYS_DEFAULT = 30;
        private const string ALIGN_VERIFY_SAVE_PATH_DEFAULT = @"D:\Data\AlignVerify";
        // WR-03 fix //260624 hbk: 피커센터 미캘 판정 임계 — AlignShapeMatchService.PICKER_CENTER_ZERO_EPS 와 동일.
        // 두 판정 기준을 단일 소스로 통일. AlignShapeMatchService 는 이 public const 를 참조.
        public const double PICKER_CENTER_ZERO_EPS = 1e-6; //260624 hbk Phase 60

        // D 드라이브가 없는 PC(예: 노트북)에 배포할 때 이 두 값만 INI 로 다른 드라이브를 가리키게 할 수 있도록
        // 설정화. 이전에는 DeviceHandler.SimulatedImagePath / EthernetAlignCamera.ALIGN_FALLBACK_IMAGE_PATH 가
        // 각각 D:\1.bmp, D:\align_test.bmp 로 코드에 고정돼 있어 INI 로 바꿀 방법이 없었다.
        // 260818 hbk [Category("...")] 를 이 파일의 using System.ComponentModel; 때문에 System.ComponentModel.CategoryAttribute
        //  로 잘못 쓰면 안 됨 — base SystemSetting.Load()/Save() 는 PropertyTools.DataAnnotations.CategoryAttribute 만 인식해서
        //  그룹이 항상 [Default]로 새는 실사용 버그로 확인됨(이 파일의 CameraRoleValue/ETHERNET_VISION 항목들도 원래 이 문제가
        //  있었음 — 여기선 새 프로퍼티 2개만 완전정규화로 고치고, 기존 항목은 이번 범위 밖이라 손대지 않는다).
        [PropertyTools.DataAnnotations.Category("Path|Simul")]
        [PropertyTools.DataAnnotations.AutoUpdateText]
        public string SimulatedImagePath { get; set; } = @"D:\1.bmp";

        [PropertyTools.DataAnnotations.Category("Path|Simul")]
        [PropertyTools.DataAnnotations.AutoUpdateText]
        public string AlignFallbackImagePath { get; set; } = @"D:\align_test.bmp";

        // Align 정합 검증(①/② 증거) 저장 설정.
        //  이 파일은 using System.ComponentModel; 을 들고 있어 짧은 [Category(...)] 는
        //  System.ComponentModel.CategoryAttribute 로 잡힌다. SystemSetting.Load()/Save() 는
        //  PropertyTools.DataAnnotations.CategoryAttribute 만 인식하므로 그룹이 조용히 [Default] 로 샌다.
        //  → 아래 어트리뷰트는 전부 완전정규화로 쓴다.
        [PropertyTools.DataAnnotations.Category("Path|AlignVerify")]
        [PropertyTools.DataAnnotations.DirectoryPath]
        [PropertyTools.DataAnnotations.AutoUpdateText]
        public string AlignVerifySavePath { get; set; } = ALIGN_VERIFY_SAVE_PATH_DEFAULT;

        [PropertyTools.DataAnnotations.Category("Path|AlignVerify")]
        public int AlignVerifyKeepDays { get; set; } = ALIGN_VERIFY_KEEP_DAYS_DEFAULT;

        [PropertyTools.DataAnnotations.Category("Path|AlignVerify")]
        public int AlignVerifyImageKeepDays { get; set; } = ALIGN_VERIFY_IMAGE_KEEP_DAYS_DEFAULT;

        // 판정 임계값 2종 — 기본값 0.0 = "미설정 = 판정 없음".
        //  실측 산포가 쌓이기 전에는 값을 넣지 않는다. 잘못 잡은 임계는 정상품을 버린다.
        //  0 = 미설정이며, 이 경우 화면은 숫자만 보여주고 정상/벗어남 판정을 하지 않는다.
        [PropertyTools.DataAnnotations.Category("Path|AlignVerify")]
        public double AlignVerifyResidualLimitMm { get; set; } = 0.0;

        [PropertyTools.DataAnnotations.Category("Path|AlignVerify")]
        public double AlignVerifySeatLimitMm { get; set; } = 0.0;

        // 피커센터 캘리브레이션에서 한 스텝마다 피커를 몇 도 돌리는지. 검사용 각도범위와는 별개다.
        //  10 → 36스텝, 20 → 18스텝, 30 → 12스텝. 화면이 이 값으로 필요 스텝 수를 계산해 보여준다.
        [PropertyTools.DataAnnotations.Category("Path|AlignVerify")]
        public double PickerCalStepAngleDeg { get; set; } = PICKER_CAL_STEP_ANGLE_DEFAULT;

        /// <summary>360/스텝각 = 한 바퀴에 필요한 스텝 수. 값이 이상하면 기본값 기준으로 돌려준다.</summary>
        [PropertyTools.DataAnnotations.Browsable(false)]
        public int PickerCalRequiredSteps {
            get {
                double dAngle = PickerCalStepAngleDeg;
                if (dAngle <= 0.0) {
                    dAngle = PICKER_CAL_STEP_ANGLE_DEFAULT;
                }
                return (int)System.Math.Round(PICKER_CAL_FULL_TURN_DEG / dAngle);
            }
        }

        partial void AfterLoad()
        {
            RestorePcRoleDefault();
            RestoreEthernetVisionDefault(); //260623 hbk Phase 58
            RestorePickerCenterDefault(); //260624 hbk Phase 60
            RestoreCalibSearchDefault(); //260630 hbk Phase 60 (Row2/Col2 기본값 복원)
            RestoreDataPathDefaults(); //260723 hbk: 신규 경로 프로퍼티 3종 — 기존 배포 INI엔 키가 없어 문자열 case가 null로 로드하는 문제 방어
            RestoreAlignVerifyDefaults();
        }

        // 신규 프로퍼티라 기존 배포 PC 의 Setting.ini 에는 이 키가 없다.
        //  reflection Load 는 키 부재 시 string=null / int=0 을 그대로 덮어쓴다 → C# 초기값이 날아간다.
        private void RestoreAlignVerifyDefaults()
        {
            if (string.IsNullOrEmpty(AlignVerifySavePath))
            {
                AlignVerifySavePath = ALIGN_VERIFY_SAVE_PATH_DEFAULT;
            }
            if (PickerCalStepAngleDeg <= 0.0)
            {
                PickerCalStepAngleDeg = PICKER_CAL_STEP_ANGLE_DEFAULT;
            }
            bool bKeepDaysMissing = AlignVerifyKeepDays <= 0;
            if (bKeepDaysMissing)
            {
                AlignVerifyKeepDays = ALIGN_VERIFY_KEEP_DAYS_DEFAULT;
            }
            bool bImageKeepDaysMissing = AlignVerifyImageKeepDays <= 0;
            if (bImageKeepDaysMissing)
            {
                AlignVerifyImageKeepDays = ALIGN_VERIFY_IMAGE_KEEP_DAYS_DEFAULT;
            }
            // 임계값 2종은 0 = 미설정이 곧 올바른 초기값이므로 복원하지 않는다.
        }

        //260723 hbk: AccountDbFilePath/CameraConfigPath/DisplayConfigFilePath 는 이번에 새로 추가된 프로퍼티라
        //  기존에 이미 돌아가던 모든 PC의 Setting.ini 에는 이 키가 없다. reflection Load 의 "String" case 는
        //  키 부재 시 null 을 그대로 SetValue 해버려(PcRole 의 int 0 폴백과 동일한 계열 문제, 문자열판)
        //  C# 기본값이 null 로 덮어써진다 — 방치하면 이번 배포에서 ACCOUNT_FILE 등이 null 이 되는 회귀 발생.
        //  SimulatedImagePath/AlignFallbackImagePath 도 260818 에 새로 추가된 프로퍼티라 동일한 문제가 있어
        //  같은 방식으로 방어한다.
        private void RestoreDataPathDefaults()
        {
            if (string.IsNullOrEmpty(AccountDbFilePath))
            {
                AccountDbFilePath = @"D:\Data\account.db";
            }
            if (string.IsNullOrEmpty(CameraConfigPath))
            {
                CameraConfigPath = @"D:\Data\CameraConfig";
            }
            if (string.IsNullOrEmpty(DisplayConfigFilePath))
            {
                DisplayConfigFilePath = @"D:\Data\DisplayConfig.ini";
            }
            if (string.IsNullOrEmpty(SimulatedImagePath))
            {
                SimulatedImagePath = @"D:\1.bmp";
            }
            if (string.IsNullOrEmpty(AlignFallbackImagePath))
            {
                AlignFallbackImagePath = @"D:\align_test.bmp";
            }
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

        //260630 hbk Phase 60 사각형 ROI 전환: 구 INI 에 CalibSearchRow2/Col2 키 부재 시 0 으로 로드 → 99999 로 복원 (전 이미지 커버).
        private void RestoreCalibSearchDefault()
        {
            bool bRow2Missing = CalibSearchRow2 <= 0.0;
            if (bRow2Missing)
            {
                CalibSearchRow2 = CALIB_SEARCH_MAX_DEFAULT;
            }
            bool bCol2Missing = CalibSearchCol2 <= 0.0;
            if (bCol2Missing)
            {
                CalibSearchCol2 = CALIB_SEARCH_MAX_DEFAULT;
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

        //260630 hbk Phase 60 사각형 ROI 전환: 피커캘 STEP 검색 ROI(사각형). TCP $ALIGN_CALIB:BOTTOM,STEP@ 수신 시 Grab→TryAddStep 에 전달.
        // 기본값 Row1=0/Col1=0/Row2=99999/Col2=99999 → 전 이미지 커버 (HALCON GenRectangle1 이 이미지 도메인 내부로 클립).
        [Category("ETHERNET_VISION")]
        public double CalibSearchRow1 { get; set; } = 0.0;

        [Category("ETHERNET_VISION")]
        public double CalibSearchCol1 { get; set; } = 0.0;

        [Category("ETHERNET_VISION")]
        public double CalibSearchRow2 { get; set; } = 99999.0;

        [Category("ETHERNET_VISION")]
        public double CalibSearchCol2 { get; set; } = 99999.0;
    }
}
