using ReringProject.Define;
using ReringProject.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Device {
    /// <summary>
    /// 시스템에서 사용되는 카메라 이름 해상도, 방향 등을 정의합니다.
    /// </summary>
    public sealed partial class DeviceHandler {
        public const string CAMERA_TOP = "CAM_TOP";
        public const string CAMERA_SIDE = "CAM_SIDE";
        public const string CAMERA_BOTTOM = "CAM_BOTTOM";

        public const int WIDTH_TOP = 2448;
        public const int HEIGHT_TOP = 2048;

        public const int WIDTH_SIDE = 2448;
        public const int HEIGHT_SIDE = 2048;

        public const int WIDTH_BOTTOM = 2448;
        public const int HEIGHT_BOTTOM = 2048;


        //260602 hbk Phase 41 — CXP ViewWorks 128MP 해상도 (실물 도착 후 MdigInquire M_SIZE_X/Y 로 확정, RESEARCH Open Q3)
        public const int WIDTH_CXP  = 16544;   // TBD: 실측 후 교정 (VNP-604MX 기준 추정값)
        public const int HEIGHT_CXP = 9200;   // TBD: 실측 후 교정 (VNP-604MX 기준 추정값)

        public const bool REVERSE_X_TOP = false;
        public const bool REVERSE_Y_TOP = false;

        public const bool REVERSE_X_SIDE = false;
        public const bool REVERSE_Y_SIDE = false;

        public const bool REVERSE_X_BOTTOM = false;  // 02.06
        public const bool REVERSE_Y_BOTTOM = false;

        
        public const ERotateAngleType ROTATE_TOP = ERotateAngleType._0;
        public const ERotateAngleType ROTATE_SIDE = ERotateAngleType._0;
        public const ERotateAngleType ROTATE_BOTTOM = ERotateAngleType._0;

        //Common size
        public const int MAX_WIDTH = WIDTH_BOTTOM;
        public const int MIN_WIDTH = WIDTH_TOP;
        public const int DEFAULT_WIDTH = WIDTH_TOP;
        
        public const int MAX_HEIGHT = HEIGHT_BOTTOM;
        public const int MIN_HEIGHT = HEIGHT_TOP;
        public const int DEFAULT_HEIGHT = HEIGHT_TOP;

        public const int MIN_ROI_WIDTH = 50;
        public const int MAX_ROI_WIDTH = MAX_WIDTH;

        public const int MIN_CIRCLE_RADIUS = 50;
        public const int MAX_CIRCLE_RADIUS = MAX_WIDTH;

        public const int MIN_ROI_HEIGHT = 50;
        public const int MAX_ROI_HEIGHT = MAX_HEIGHT;

        public const double TICK_EXPOSURE = 0.1;
        public const double MIN_EXPOSURE = 10;
        public const double MAX_EXPOSURE = 100000;

        public const double TICK_GAIN = 0.1;
        public const double MIN_GAIN = 0;
        public const double MAX_GAIN = 95.0;

        public const double TICK_GAMMA = 0.1;
        public const double MIN_GAMMA = 0;
        public const double MAX_GAMMA = 3.99998474121094;

        public const string FILTER_IMAGE = "tiff Files(*.tiff)|*.tiff|bmp Files(*.bmp)|*.bmp|jpg Files(*.jpg)|*.jpg|jpeg Files(*.jpeg)|*.jpeg|png Files(*.png)|*.png|All Files(*.*)|*.*";
        public const string EXTENSION_IMAGE = ".tiff";

        public const string EXTENSION_SAVE_IMAGE = ".jpg";
        public const string FILTER_SAVE_IMAGE = "jpg Files(*.jpg)|*.jpg";

        public const string FILTER_MODEL = "mmf Files(*.mmf)|*.mmf";
        public const string EXTENSION_MODEL = ".mmf";

        //260618 hbk Phase 54 ALIGN-01 HALCON shape/ncc 모델 확장자 (D-07b) — 기존 .mmf(MIL) 미재사용
        //260709 hbk 병합 충돌(--theirs 파일 전체 덮어쓰기)로 유실되어 복원
        public const string EXTENSION_SHAPE_MODEL = ".shm";
        public const string EXTENSION_NCC_MODEL = ".ncm";

        public const string EXTENSION_CALIBRATION = ".cal";

        /// <summary>
        /// 이 함수에서 카메라를 정의합니다.
        /// 함수는 시스템 초기화 시점에 호출됩니다.
        /// </summary>
        private void RegisterRequiredDevices() {
            //260602 hbk Phase 41 — D-03 PC별 CXP 1대 + 역할(시퀀스) 설정. HIK 3대 고정 → 역할 분기.
            //260609 hbk Phase 41 — SIMUL/실 HW 통일: 항상 CameraRole 기반 등록(#if 분기 제거).
            //  시뮬도 실 동작을 그대로 재현(카메라 수·역할·코드 경로 동일). 다른 역할 테스트는 CameraRole 설정 변경 후 재시작.
            //  SequenceHandler.IsSequenceActive 와 정책이 1:1 동기화되어야 함(미등록 카메라 시퀀스 미생성 → OnCreate Error 차단, CO-41-02).
            ECameraRole role = SystemSetting.Handle.CameraRole;

            if (role == ECameraRole.TopBottom) {
                // PC1: CXP 카메라 1대 — Top + Bottom 시퀀스 담당 (D-02)
                RegisterCxpCamera(CAMERA_TOP, REVERSE_X_TOP, REVERSE_Y_TOP, ROTATE_TOP);
                RegisterCxpCamera(CAMERA_BOTTOM, REVERSE_X_BOTTOM, REVERSE_Y_BOTTOM, ROTATE_BOTTOM);
            }
            else { // ECameraRole.Side — PC2
                // PC2: CXP 카메라 1대 — Side 시퀀스 담당 (D-02)
                RegisterCxpCamera(CAMERA_SIDE, REVERSE_X_SIDE, REVERSE_Y_SIDE, ROTATE_SIDE);
            }
        }

        //260604 hbk Phase 41 CO-41-02 — CXP 카메라 1대 등록 헬퍼(역할/SIMUL 분기 공통). Gray8 + Software trigger + CXP 해상도 고정.
        private void RegisterCxpCamera(string cameraName, bool reverseX, bool reverseY, ERotateAngleType rotate) {
            SetRequiredDevice(
                ECameraType.MIL,
                ECaptureImageType.Gray8,
                ETriggerSource.Software,
                cameraName,
                WIDTH_CXP,
                HEIGHT_CXP,
                reverseX,
                reverseY,
                rotate);
        }

        // quick-260813-jnh: 미러 조합은 (X,Y) 불리언 2개 = 최대 4가지뿐이라, 레시피를 스캔하지 않고 앱 시작 시
        //  4가지 역할을 전부 정적 등록해 둔다. 레시피 로드 타이밍(카메라 초기화보다 한참 뒤)과 _roleInfoMap 의
        //  stale 역할 문제를 동시에 회피하는 유일한 저위험 설계.
        public const string MIRROR_ROLE_SUFFIX_X  = "#MX";
        public const string MIRROR_ROLE_SUFFIX_Y  = "#MY";
        public const string MIRROR_ROLE_SUFFIX_XY = "#MXY";

        // 미러 플래그 → grab 역할 식별자. 둘 다 꺼짐이면 기존 식별자를 그대로 돌려준다(회귀 0의 근거).
        public static string BuildGrabRoleIdentifier(string szBaseDeviceName, bool bMirrorX, bool bMirrorY) {
            if (string.IsNullOrEmpty(szBaseDeviceName)) return szBaseDeviceName;
            if (bMirrorX && bMirrorY) return szBaseDeviceName + MIRROR_ROLE_SUFFIX_XY;
            if (bMirrorX)             return szBaseDeviceName + MIRROR_ROLE_SUFFIX_X;
            if (bMirrorY)             return szBaseDeviceName + MIRROR_ROLE_SUFFIX_Y;
            return szBaseDeviceName;
        }

        // 기준 역할(무미러)로부터 미러 3조합의 DeviceInfo 클론을 만든다. 기준값(REVERSE_X_SIDE 등)의 논리 반대가 미러다.
        private List<DeviceInfo> BuildMirrorRoleInfos(DeviceInfo baseInfo) {
            List<DeviceInfo> mirrorInfos = new List<DeviceInfo>();
            if (baseInfo == null || string.IsNullOrEmpty(baseInfo.Identifier)) return mirrorInfos;
            mirrorInfos.Add(CloneRoleInfo(baseInfo, MIRROR_ROLE_SUFFIX_X, !baseInfo.ReverseX, baseInfo.ReverseY));
            mirrorInfos.Add(CloneRoleInfo(baseInfo, MIRROR_ROLE_SUFFIX_Y, baseInfo.ReverseX, !baseInfo.ReverseY));
            mirrorInfos.Add(CloneRoleInfo(baseInfo, MIRROR_ROLE_SUFFIX_XY, !baseInfo.ReverseX, !baseInfo.ReverseY));
            return mirrorInfos;
        }

        // DeviceInfo 생성자가 TriggerSource 를 대입하지 않는 기존 결함이 있어(:28-39) 클론 후 명시 복사한다.
        //  원본 생성자는 다른 호출부 회귀 위험 때문에 고치지 않는다.
        private DeviceInfo CloneRoleInfo(DeviceInfo baseInfo, string szSuffix, bool bReverseX, bool bReverseY) {
            DeviceInfo clone = new DeviceInfo(baseInfo.CamType, baseInfo.ImageType, baseInfo.TriggerSource,
                baseInfo.Identifier + szSuffix, baseInfo.Width, baseInfo.Height, bReverseX, bReverseY, baseInfo.RotateAngle);
            clone.TriggerSource = baseInfo.TriggerSource;
            return clone;
        }
    }
}
