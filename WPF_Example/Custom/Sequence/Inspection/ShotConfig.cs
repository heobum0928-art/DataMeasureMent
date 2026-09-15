using System;
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Device;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class ShotConfig : CameraSlaveParam, IOfflineImageParam
    {

        // 260723 hbk: 실제 Z축 제어는 외부 PLC/핸들러가 전담(이 앱은 z_index 트리거만 받아 그 순간 캡처)이라
        //  이 값이 실제 물리 위치와 일치함을 이 앱이 보장 못 함 — PropertyGrid에서 숨김(INI 키/직렬화는 보존).
        //  DualImage datum의 ZIndexA/B(2개 캡처 인덱스)에 대응하는 물리 position 개념은 별도로 추가하지 않기로 결정(260723).
        [Browsable(false)]
        [Category("Shot|Setting")]
        public double ZPosition { get; set; }
        public int DelayMs { get; set; }

        [Category("Shot|Simulation")]
        // MainView Load 버튼/툴바 폴더 일괄 할당/검사Grab 이 채우고 SIMUL·오프라인 검사와 레시피 INI 가 읽는 값이다.
        //  System.ComponentModel.Browsable / Newtonsoft.Json.JsonIgnore 는 절대 추가 금지(직렬화가 끊겨 값이 소실된다).
        public string SimulImagePath { get; set; } = "";

        // 두 장짜리 측정이 있는 Shot 인지 — 아래 가로/세로 경로 칸의 표시 조건(VisibleBy). 속성창에는 안 보인다.
        //  ParamBase 가 INI 로 저장/로드해도 무해하도록 setter 는 비워 둔다(getter 전용이면 로드 시 오류 로그가 남는다).
        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool IsDualImageShot
        {
            get { return HasDualImageMeasurement(); }
            set { }
        }

        // 두 장짜리 Shot(E5 등) 전용: 측정 항목 안에 숨겨진 가로(점)/세로(선) 사진 경로를 Shot 속성창에서 보고 고칠 수 있게 한다.
        //  값은 이 Shot 의 모든 두 장짜리 측정(TeachingImagePath_Horizontal/_Vertical)에 그대로 배분된다.
        //  두 장짜리 측정이 없는 Shot 에서는 빈 칸으로 보이고, 입력해도 무시된다(INI 저장/로드 시 부작용 없음).
        [Category("Shot|Simulation")]
        [DisplayName("가로(점) 이미지 — 두 장짜리 Shot")]
        [VisibleBy(nameof(IsDualImageShot))]
        [InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)]
        [AutoUpdateText]
        public string DualImagePath_Horizontal
        {
            get
            {
                string szH;
                string szV;
                if (TryGetDualImagePaths(out szH, out szV)) { return szH; }
                return "";
            }
            set
            {
                if (HasDualImageMeasurement()) { SetDualImagePathForMeasurements(false, value); }
            }
        }

        [Category("Shot|Simulation")]
        [DisplayName("세로(선) 이미지 — 두 장짜리 Shot")]
        [VisibleBy(nameof(IsDualImageShot))]
        [InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)]
        [AutoUpdateText]
        public string DualImagePath_Vertical
        {
            get
            {
                string szH;
                string szV;
                if (TryGetDualImagePaths(out szH, out szV)) { return szV; }
                return "";
            }
            set
            {
                if (HasDualImageMeasurement()) { SetDualImagePathForMeasurements(true, value); }
            }
        }

        // IOfflineImageParam — MainView Load 버튼이 SHOT 노드 선택 시 경로 저장
        public string GetLatestImagePath()
        {
            return SimulImagePath;
        }

        public void SetLatestImagePath(string imagePath)
        {
            SimulImagePath = imagePath;
        }

        // 이 Shot 에 두 장(가로/세로)이 필요한 측정(DualImageEdgeDistance)이 하나라도 있는가.
        public bool HasDualImageMeasurement()
        {
            for (int i = 0; i < FAIList.Count; i++)
            {
                FAIConfig fai = FAIList[i];
                if (fai == null) { continue; }
                for (int j = 0; j < fai.Measurements.Count; j++)
                {
                    if (fai.Measurements[j] is DualImageEdgeDistanceMeasurement) { return true; }
                }
            }
            return false;
        }

        // 두 장짜리 Shot 의 현재 가로(점)/세로(선) 사진 경로. 첫 두 장짜리 측정의 값을 돌려준다(전 측정 동일 배분 전제).
        public bool TryGetDualImagePaths(out string szHorizontal, out string szVertical)
        {
            szHorizontal = "";
            szVertical = "";
            for (int i = 0; i < FAIList.Count; i++)
            {
                FAIConfig fai = FAIList[i];
                if (fai == null) { continue; }
                for (int j = 0; j < fai.Measurements.Count; j++)
                {
                    DualImageEdgeDistanceMeasurement dual = fai.Measurements[j] as DualImageEdgeDistanceMeasurement;
                    if (dual == null) { continue; }
                    szHorizontal = dual.TeachingImagePath_Horizontal;
                    szVertical = dual.TeachingImagePath_Vertical;
                    return true;
                }
            }
            return false;
        }

        // 검사Grab/Load(가로·세로 토글)로 얻은 사진 경로를 이 Shot 의 모든 두 장짜리 측정에 배분한다.
        //  가로 = PointROI 쪽(TeachingImagePath_Horizontal, 동시에 Shot 검사용 사진 SimulImagePath 도 갱신),
        //  세로 = LineROI 쪽(TeachingImagePath_Vertical). 속성창에서는 두 경로가 숨겨져 있어 이 길이 유일한 입력 경로다.
        public void SetDualImagePathForMeasurements(bool bVertical, string szPath)
        {
            if (!bVertical)
            {
                SimulImagePath = szPath;
            }
            for (int i = 0; i < FAIList.Count; i++)
            {
                FAIConfig fai = FAIList[i];
                if (fai == null) { continue; }
                for (int j = 0; j < fai.Measurements.Count; j++)
                {
                    DualImageEdgeDistanceMeasurement dual = fai.Measurements[j] as DualImageEdgeDistanceMeasurement;
                    if (dual == null) { continue; }
                    if (bVertical)
                    {
                        dual.TeachingImagePath_Vertical = szPath;
                    }
                    else
                    {
                        dual.TeachingImagePath_Horizontal = szPath;
                    }
                }
            }
        }

        [Browsable(false)]
        public List<FAIConfig> FAIList { get; private set; } = new List<FAIConfig>();

        // INotifyPropertyChanged 발화로 트리 헤더 즉시 갱신 (PropertyGrid 편집 → Tree)
        private string _shotName;
        [Category("Shot|Identity")]
        public string ShotName {
            get { return _shotName; }
            set {
                if (_shotName == value) return;
                _shotName = value;
                RaisePropertyChanged(nameof(ShotName));
            }
        }

        // per-sequence Shot ownership.
        //  값 = SequenceHandler.SEQ_TOP / SEQ_SIDE / SEQ_BOTTOM
        //  하위 호환: 빈 문자열 → ApplyShotDefaults 에서 SEQ_TOP 자동 폴백
        //  ParamBase reflection 자동 직렬화 (String case)
        [Category("Shot|Identity")]
        public string OwnerSequenceName { get; set; } = "";

        //260623 hbk Phase 49 PROTO-03/05 (D-01): 이 Shot 이 속한 $TEST z_index. 0=Datum 샷, 1+=측정 Index.
        //  AddResponse(49-02) 가 RequestPacket z_index 와 비교하여 해당 Index Shot 만 판정 집계(Index-scoped, D-01).
        //  ParamBase reflection 자동 직렬화 — INI 키 = "ZIndex". 기존 레시피엔 키 부재 → 0 로드(=Datum/Idx0 폴백, 하위 호환).
        //  ※ 엣지케이스: ZIndex 미설정 레시피(전 Shot=0)는 Index 0 만 매칭됨 → 측정 Index(1+) 수신 시 BuildScopedResponse 매칭 0건.
        //     이 경우 49-02 BuildScopedResponse 가 빈 B 응답 + PrintErrLog 경고를 남김(조용한 빈 B 금지). 운용 시 레시피 ZIndex 설정 필요.
        //  ※ INI 호환 위해 PascalCase 프로퍼티명 유지(ParamBase 키=프로퍼티명) — 헝가리언 예외(직렬화 필드, D-10 적용범위 밖).
        private int _zIndex = 0;
        public int ZIndex {
            get { return _zIndex; }
            set {
                if (_zIndex == value) { return; }
                _zIndex = value;
                RaisePropertyChanged(nameof(ZIndex));
                // Phase 77 D-77-08: 범위가 켜진 Shot 은 ZIndex 가 범위 시작이라 바꾸면 범위도 바뀐다 — 같은 다이얼로그로 알린다.
                if (ZIndexEnd != Z_RANGE_OFF) {
                    WarnZIndexEndChanged();
                }
            }
        }

        // Phase 77 SZF-01: 범위 기능 꺼짐 표식(옛 레시피의 키 부재 로드값과 동일 — 회귀 0).
        public const int Z_RANGE_OFF = 0;
        // Phase 77 SZF-01: 기준점(ZIndex) 0 번은 범위 시작으로 쓸 수 없다(Datum 폴백 index 와 충돌).
        public const int MIN_Z_RANGE_BASE_INDEX = 1;
        // Phase 77 SZF-01: SIDE 사진 1장 약 127~152MB — z 개수 상한(O-5 메모리 가드).
        public const int MAX_Z_RANGE_COUNT = 10;

        // Phase 77 D-77-07 ①⑧: 범위 = ZIndex ~ ZIndexEnd(둘 다 포함), 기준 Z = ZIndex.
        //  시작 번호 칸은 만들지 않는다 — 기존 ZIndex 를 그대로 범위 시작으로 쓴다.
        //  ParamBase reflection 자동 직렬화 — INI 키 = "ZIndexEnd". 키 부재(옛 레시피) → 0 로드 = 범위 꺼짐.
        private int _zIndexEnd = Z_RANGE_OFF;
        [Category("Shot|Identity")]
        [DisplayName("Z 범위 끝")]
        [System.ComponentModel.Description("PLC 가 ZIndex~Z 범위 끝 번호로 차례로 촬영해야 동작")]
        public int ZIndexEnd {
            get { return _zIndexEnd; }
            set {
                if (_zIndexEnd == value) return;
                _zIndexEnd = value;
                RaisePropertyChanged(nameof(ZIndexEnd));
                WarnZIndexEndChanged();
            }
        }

        // Phase 77 SZF-01/SZF-05: 범위 기능이 켜져 있는지 판정하는 단일 진입점 — 이 값이 false 면
        //  범위 관련 새 분기(저장·대기·선택·완성 index·마지막 index)가 전부 미도달이라 기존 동작과 같다.
        //  프로퍼티가 아니라 메서드다 — ParamBase 리플렉션은 공개 프로퍼티를 전부 INI 에 쓰므로
        //  bool 프로퍼티로 만들면 존재하지도 않는 파생 키가 레시피에 저장된다.
        public bool IsZRangeEnabled() {
            if (ZIndexEnd == Z_RANGE_OFF) { return false; }
            if (ZIndex < MIN_Z_RANGE_BASE_INDEX) { return false; }
            if (ZIndexEnd <= ZIndex) { return false; }
            int nZCount = ZIndexEnd - ZIndex + 1;
            if (nZCount > MAX_Z_RANGE_COUNT) { return false; }
            return true;
        }

        // Phase 77 D-77-06 ②: 저장 사이클 재검사 중에만 RepeatRunService 가 채우는 z→사진 경로다.
        //  null 이면 재검사가 아니라는 뜻이다(오프라인 폴더 규약을 그대로 따른다). 필드다 —
        //  런타임 전용 값이라 ParamBase 리플렉션 직렬화(INI)·PropertyGrid 붙여넣기 대상이 아니다.
        public Dictionary<int, string> RerunZRangeImagePaths = null;

        // Phase 77 D-77-07 ①⑤: ZIndexEnd 가 0(꺼짐)이 아닌데 IsZRangeEnabled() 가 false 면 오설정 —
        //  ZIndex 미설정/역순/상한 초과 중 하나로 조용히 꺼진 상태다. 런타임 tick 경고(Action_FAIMeasurement)
        //  와 편집 즉시 경고(WarnZIndexEndChanged) 가 공유한다.
        public bool IsZRangeMisconfigured() {
            bool bEndSet = ZIndexEnd != Z_RANGE_OFF;
            bool bDisabled = !IsZRangeEnabled();
            return bEndSet && bDisabled;
        }

        // Phase 77 D-77-07 ①⑤: 오설정 이유를 한국어 한 줄로. 가드 순서가 원인 우선순위다 — ZIndex 미설정이
        //  가장 근본적인 원인이라 먼저 확인한다. 겹침(다른 Shot·기준점)은 BuildZRangeConflictText 가 담당한다.
        public string BuildZRangeMisconfigText() {
            if (ZIndexEnd == Z_RANGE_OFF) { return string.Empty; }
            if (ZIndex < MIN_Z_RANGE_BASE_INDEX) {
                return "ZIndex 가 1 이상이어야 Z 범위를 쓸 수 있어 Z 범위 기능이 꺼집니다.";
            }
            if (ZIndexEnd <= ZIndex) {
                return "Z 범위 끝(" + ZIndexEnd + ")이 ZIndex(" + ZIndex + ") 이하라 Z 범위 기능이 꺼집니다.";
            }
            int nZCount = ZIndexEnd - ZIndex + 1;
            if (nZCount > MAX_Z_RANGE_COUNT) {
                return "Z 범위가 " + nZCount + "개로 최대 " + MAX_Z_RANGE_COUNT + "개를 넘어 Z 범위 기능이 꺼집니다.";
            }
            return string.Empty;
        }

        // quick-260813 선례(DatumConfig.WarnDatumZIndexChanged)와 동일한 관용구 — INI 로드·붙여넣기(리플렉션
        //  SetValue)에서는 경고를 억제한다(_suppressUserEditWarning). 저장은 절대 막지 않는다.
        private bool _suppressUserEditWarning;

        private const string Z_RANGE_DIALOG_TITLE = "Z 범위 확인";
        private const string Z_RANGE_SINGLE_IMAGE_HINT = "\n한 장만 쓰려면 Z 범위 끝을 0 으로 두세요.";

        // Phase 77 D-77-07 ①⑤ + D-77-08: Z 범위 끝(또는 범위가 켜진 Shot 의 ZIndex)을 사용자가 PropertyGrid 에서
        //  직접 바꾸면 항상 다이얼로그로 알린다 — 꺼짐·정상 범위는 정보, 오입력·겹침은 경고 + 한 장 안내.
        private void WarnZIndexEndChanged() {
            if (_suppressUserEditWarning) { return; }
            if (ZIndexEnd == Z_RANGE_OFF) {
                ReringProject.UI.CustomMessageBox.Show(Z_RANGE_DIALOG_TITLE, BuildZRangeOffText(),
                    System.Windows.MessageBoxImage.Information, true, false);
                return;
            }
            string szMisconfig = BuildZRangeMisconfigText();
            if (!string.IsNullOrEmpty(szMisconfig)) {
                ReringProject.UI.CustomMessageBox.Show(Z_RANGE_DIALOG_TITLE, szMisconfig + Z_RANGE_SINGLE_IMAGE_HINT,
                    System.Windows.MessageBoxImage.Warning, true, false);
                return;
            }
            string szConflict = string.Empty;
            InspectionSequence owner = Parent as InspectionSequence;
            if (owner != null) {
                szConflict = owner.BuildZRangeConflictText(this);
            }
            if (!string.IsNullOrEmpty(szConflict)) {
                ReringProject.UI.CustomMessageBox.Show(Z_RANGE_DIALOG_TITLE, BuildZRangeOnText() + "\n" + szConflict + Z_RANGE_SINGLE_IMAGE_HINT,
                    System.Windows.MessageBoxImage.Warning, true, false);
                return;
            }
            ReringProject.UI.CustomMessageBox.Show(Z_RANGE_DIALOG_TITLE, BuildZRangeOnText(),
                System.Windows.MessageBoxImage.Information, true, false);
        }

        // Phase 77 D-77-08: 정상 범위 알림 — 몇 장을 어느 번호로 찍어야 하는지 운영자가 바로 알게 한다.
        private string BuildZRangeOnText() {
            int nZCount = ZIndexEnd - ZIndex + 1;
            return "Z 범위 z" + ZIndex + "~z" + ZIndexEnd + " (" + nZCount + "장) — 측정마다 가장 선명한 Z 를 자동으로 고릅니다.\n"
                + "PLC 가 " + ZIndex + "~" + ZIndexEnd + " 번호로 차례로 촬영해야 동작합니다.";
        }

        // Phase 77 D-77-08: 꺼짐 알림 — 기본값 0 으로 되돌리면 예전처럼 한 장으로 측정한다.
        private string BuildZRangeOffText() {
            return "Z 범위 꺼짐 — ZIndex(" + ZIndex + ") 사진 1장으로 측정합니다.";
        }

        // Multi-Light — Ring/Back/Coax/Side 조명 필드 8개 (Ring/Bar 는 채널별 개별 제어로 대체, 아래 20개 참조)
        // 구 통합 필드 — PropertyGrid 에서 숨기되 삭제하지 않는다. Ring/Bar 채널별 키가 없는 구 레시피의 마이그레이션 소스(Load override)이자
        // 구버전 롤백 시 graceful downgrade 경로다. INI 키/직렬화는 그대로 유지된다.
        [Browsable(false)]
        [Category("Light|Ring")]
        public bool RingLight_Enabled { get; set; }
        [Browsable(false)]
        public int RingLight_Brightness { get; set; }

        // Ring 6채널 개별 밝기(0~255) + On/Off. 구 레시피(채널 키 없음)는 Load override 가 위 구 필드 값을 6채널 전부로 브로드캐스트한다.
        [Category("Light|Ring")]
        public bool RingLight_Enabled_1 { get; set; }
        // 슬라이더 드래그 시 옆 텍스트박스가 갱신되도록 PropertyChanged 발화 (ShotName과 동일 패턴 — auto-property는 PropertyGrid의
        // Slider/TextBox 두 바인딩 중 TextBox 쪽이 갱신 신호를 못 받아 값이 안 바뀌어 보임).
        private int _ringLightBrightness1;
        [Slidable(0, 255)]
        public int RingLight_Brightness_1 {
            get { return _ringLightBrightness1; }
            set {
                if (_ringLightBrightness1 == value) return;
                _ringLightBrightness1 = value;
                RaisePropertyChanged(nameof(RingLight_Brightness_1));
            }
        }
        public bool RingLight_Enabled_2 { get; set; }
        private int _ringLightBrightness2;
        [Slidable(0, 255)]
        public int RingLight_Brightness_2 {
            get { return _ringLightBrightness2; }
            set {
                if (_ringLightBrightness2 == value) return;
                _ringLightBrightness2 = value;
                RaisePropertyChanged(nameof(RingLight_Brightness_2));
            }
        }
        public bool RingLight_Enabled_3 { get; set; }
        private int _ringLightBrightness3;
        [Slidable(0, 255)]
        public int RingLight_Brightness_3 {
            get { return _ringLightBrightness3; }
            set {
                if (_ringLightBrightness3 == value) return;
                _ringLightBrightness3 = value;
                RaisePropertyChanged(nameof(RingLight_Brightness_3));
            }
        }
        public bool RingLight_Enabled_4 { get; set; }
        private int _ringLightBrightness4;
        [Slidable(0, 255)]
        public int RingLight_Brightness_4 {
            get { return _ringLightBrightness4; }
            set {
                if (_ringLightBrightness4 == value) return;
                _ringLightBrightness4 = value;
                RaisePropertyChanged(nameof(RingLight_Brightness_4));
            }
        }
        public bool RingLight_Enabled_5 { get; set; }
        private int _ringLightBrightness5;
        [Slidable(0, 255)]
        public int RingLight_Brightness_5 {
            get { return _ringLightBrightness5; }
            set {
                if (_ringLightBrightness5 == value) return;
                _ringLightBrightness5 = value;
                RaisePropertyChanged(nameof(RingLight_Brightness_5));
            }
        }
        public bool RingLight_Enabled_6 { get; set; }
        private int _ringLightBrightness6;
        [Slidable(0, 255)]
        public int RingLight_Brightness_6 {
            get { return _ringLightBrightness6; }
            set {
                if (_ringLightBrightness6 == value) return;
                _ringLightBrightness6 = value;
                RaisePropertyChanged(nameof(RingLight_Brightness_6));
            }
        }

        [Category("Light|Back")]
        public bool BackLight_Enabled { get; set; }
        private int _backLightBrightness;
        [Slidable(0, 255)]
        public int BackLight_Brightness {
            get { return _backLightBrightness; }
            set {
                if (_backLightBrightness == value) return;
                _backLightBrightness = value;
                RaisePropertyChanged(nameof(BackLight_Brightness));
            }
        }

        [Browsable(false)]   //260626 hbk Phase 66 D-03: 검사 PropertyGrid 에서 동축 숨김. INI 키/매핑 코드는 보존(하위호환). 동축 제어는 Align 창(Plan 03).
        [Category("Light|Coax")]
        public bool CoaxLight_Enabled { get; set; }
        private int _coaxLightBrightness;
        [Browsable(false)]   //260626 hbk Phase 66 IN-01: CoaxLight_Brightness 도 PropertyGrid 에서 숨김(동축 2필드 모두 숨김). INI 키 보존(하위호환).
        [Slidable(0, 255)]
        public int CoaxLight_Brightness {
            get { return _coaxLightBrightness; }
            set {
                if (_coaxLightBrightness == value) return;
                _coaxLightBrightness = value;
                RaisePropertyChanged(nameof(CoaxLight_Brightness));
            }
        }

        // 구 통합 필드 — Bar 4채널 개별 제어로 대체. 사유/보존 정책은 위 RingLight_Enabled 주석과 동일.
        [Browsable(false)]
        [Category("Light|Side")]
        public bool SideLight_Enabled { get; set; }
        [Browsable(false)]
        public int SideLight_Brightness { get; set; }

        // Bar 4채널 개별 밝기(0~255) + On/Off. 프로퍼티명 접두사는 INI 하위호환 위해 기존 SideLight_ 유지(물리 조명은 Bar).
        [Category("Light|Bar")]
        public bool SideLight_Enabled_1 { get; set; }
        private int _sideLightBrightness1;
        [Slidable(0, 255)]
        public int SideLight_Brightness_1 {
            get { return _sideLightBrightness1; }
            set {
                if (_sideLightBrightness1 == value) return;
                _sideLightBrightness1 = value;
                RaisePropertyChanged(nameof(SideLight_Brightness_1));
            }
        }
        public bool SideLight_Enabled_2 { get; set; }
        private int _sideLightBrightness2;
        [Slidable(0, 255)]
        public int SideLight_Brightness_2 {
            get { return _sideLightBrightness2; }
            set {
                if (_sideLightBrightness2 == value) return;
                _sideLightBrightness2 = value;
                RaisePropertyChanged(nameof(SideLight_Brightness_2));
            }
        }
        public bool SideLight_Enabled_3 { get; set; }
        private int _sideLightBrightness3;
        [Slidable(0, 255)]
        public int SideLight_Brightness_3 {
            get { return _sideLightBrightness3; }
            set {
                if (_sideLightBrightness3 == value) return;
                _sideLightBrightness3 = value;
                RaisePropertyChanged(nameof(SideLight_Brightness_3));
            }
        }
        public bool SideLight_Enabled_4 { get; set; }
        private int _sideLightBrightness4;
        [Slidable(0, 255)]
        public int SideLight_Brightness_4 {
            get { return _sideLightBrightness4; }
            set {
                if (_sideLightBrightness4 == value) return;
                _sideLightBrightness4 = value;
                RaisePropertyChanged(nameof(SideLight_Brightness_4));
            }
        }

        [Category("Light|Ring7")]   //260626 hbk Phase 66 D-01: Ring7 조명 추가(자유 조합) — Ring/Back/Bar/Ring7 4종
        public bool Ring7Light_Enabled { get; set; }   //260626 hbk Ring7 ON/OFF
        private int _ring7LightBrightness;
        [Slidable(0, 255)]
        public int Ring7Light_Brightness {
            get { return _ring7LightBrightness; }
            set {
                if (_ring7LightBrightness == value) return;
                _ring7LightBrightness = value;
                RaisePropertyChanged(nameof(Ring7Light_Brightness));
            }
        }

        // Thread-safe image buffer
        private readonly object _imageLock = new object();
        private HImage _image;

        /// <summary>
        /// 현재 buffer 에 HImage 가 보관되어 있는지 여부.
        /// _imageLock 으로 동기화되므로 임의 thread 에서 안전하게 호출 가능하다.
        /// </summary>
        [Browsable(false)]
        public bool HasImage {
            get { lock (_imageLock) { return _image != null; } }
        }

        public ShotConfig(object owner) : base(owner) {
        }

        public ShotConfig(object owner, string name) : base(owner) {
            ShotName = name;
        }

        // 안전장치: 구 레시피(채널 키 없음) 로드 시 Ring/Bar 전소등 회귀 방지.
        //  ParamBase.Load 는 INI 누락 키를 0/false 로 덮어쓴다 — 신규 채널 키가 없는 구 레시피는
        //  base.Load 직후 RingLight_Brightness_1~6/Enabled_1~6, SideLight_Brightness_1~4/Enabled_1~4 가 전부 0/false 로 로드된다.
        //  CameraSlaveParam.Load(CorrectionFactor 복원) 선례를 그대로 따라, 신규 키 부재 시에만 구 통합 필드 값을 채널 전체로 브로드캐스트한다.
        //  신규 키가 이미 있으면(채널별 저장된 레시피) 아무것도 하지 않는다 — ParamBase 가 채널별로 정확히 로드했기 때문.
        public override bool Load(IniFile loadFile, string groupName) {
            _suppressUserEditWarning = true; // Phase 77: INI 로드는 사용자 편집이 아니다 — 경고 억제
            bool result = base.Load(loadFile, groupName);
            _suppressUserEditWarning = false;

            IniSection sec;
            bool bHasSection = loadFile.TryGetSection(groupName, out sec) && sec != null;

            bool bHasRingChannelKeys = bHasSection && sec.ContainsKey("RingLight_Brightness_1");
            if (!bHasRingChannelKeys) {
                RingLight_Enabled_1 = RingLight_Enabled;
                RingLight_Enabled_2 = RingLight_Enabled;
                RingLight_Enabled_3 = RingLight_Enabled;
                RingLight_Enabled_4 = RingLight_Enabled;
                RingLight_Enabled_5 = RingLight_Enabled;
                RingLight_Enabled_6 = RingLight_Enabled;
                RingLight_Brightness_1 = RingLight_Brightness;
                RingLight_Brightness_2 = RingLight_Brightness;
                RingLight_Brightness_3 = RingLight_Brightness;
                RingLight_Brightness_4 = RingLight_Brightness;
                RingLight_Brightness_5 = RingLight_Brightness;
                RingLight_Brightness_6 = RingLight_Brightness;
            }

            bool bHasBarChannelKeys = bHasSection && sec.ContainsKey("SideLight_Brightness_1");
            if (!bHasBarChannelKeys) {
                SideLight_Enabled_1 = SideLight_Enabled;
                SideLight_Enabled_2 = SideLight_Enabled;
                SideLight_Enabled_3 = SideLight_Enabled;
                SideLight_Enabled_4 = SideLight_Enabled;
                SideLight_Brightness_1 = SideLight_Brightness;
                SideLight_Brightness_2 = SideLight_Brightness;
                SideLight_Brightness_3 = SideLight_Brightness;
                SideLight_Brightness_4 = SideLight_Brightness;
            }

            return result;
        }

        // Copy/Paste(InspectionListView.button_paste_Click) 시 base(CameraSlaveParam.CopyTo)가 다루지 않는
        //  ShotConfig 고유 필드를 보완 복사한다 — 기존엔 override 가 없어 조명 필드가 전혀 복사되지 않던 버그였다.
        //  ShotName/_image는 제외; FAIList는 CopyTo가 복사하지만 바로 아래 ClearFAIs로 비운다.
        public override bool CopyTo(ParamBase param) {
            ShotConfig target = param as ShotConfig;
            // 260723 hbk: DeviceName 도 복사 누락(신규 Shot 생성 후 Copy/Paste로 채우려 해도 안 채워지던 버그).
            //  base.CopyTo(PropertyArray/Exposure·Gain·Gamma 값 복사)보다 먼저 설정한다 — DeviceName setter 가
            //  PasteFromCamera 를 트리거해 카메라 실측값으로 PropertyArray 를 되읽어오므로, 순서가 바뀌면
            //  아래에서 복사한 Exposure/Gain 값이 그 실측값에 덮여 사라진다.
            if (target != null) target.DeviceName = DeviceName;
            if (target != null) target._suppressUserEditWarning = true; // Phase 77: 붙여넣기는 사용자 편집이 아니다 — 경고 억제
            bool result = base.CopyTo(param);
            if (target != null) target._suppressUserEditWarning = false;
            if (target == null) return result;

            // PixelResolution/CorrectionFactor 는 CameraSlaveParam 소유이나 CameraSlaveParam.CopyTo 가 복사하지 않으므로 여기서 보완.
            target.PixelResolution = PixelResolution;
            target.CorrectionFactor = CorrectionFactor;

            // Ring 6채널
            target.RingLight_Enabled_1 = RingLight_Enabled_1;
            target.RingLight_Brightness_1 = RingLight_Brightness_1;
            target.RingLight_Enabled_2 = RingLight_Enabled_2;
            target.RingLight_Brightness_2 = RingLight_Brightness_2;
            target.RingLight_Enabled_3 = RingLight_Enabled_3;
            target.RingLight_Brightness_3 = RingLight_Brightness_3;
            target.RingLight_Enabled_4 = RingLight_Enabled_4;
            target.RingLight_Brightness_4 = RingLight_Brightness_4;
            target.RingLight_Enabled_5 = RingLight_Enabled_5;
            target.RingLight_Brightness_5 = RingLight_Brightness_5;
            target.RingLight_Enabled_6 = RingLight_Enabled_6;
            target.RingLight_Brightness_6 = RingLight_Brightness_6;

            // Bar 4채널
            target.SideLight_Enabled_1 = SideLight_Enabled_1;
            target.SideLight_Brightness_1 = SideLight_Brightness_1;
            target.SideLight_Enabled_2 = SideLight_Enabled_2;
            target.SideLight_Brightness_2 = SideLight_Brightness_2;
            target.SideLight_Enabled_3 = SideLight_Enabled_3;
            target.SideLight_Brightness_3 = SideLight_Brightness_3;
            target.SideLight_Enabled_4 = SideLight_Enabled_4;
            target.SideLight_Brightness_4 = SideLight_Brightness_4;

            // 구 통합 필드(Ring/Side) + Back/Coax/Ring7 — 마이그레이션 소스/기존 그룹 필드도 함께 복사.
            target.RingLight_Enabled = RingLight_Enabled;
            target.RingLight_Brightness = RingLight_Brightness;
            target.BackLight_Enabled = BackLight_Enabled;
            target.BackLight_Brightness = BackLight_Brightness;
            target.CoaxLight_Enabled = CoaxLight_Enabled;
            target.CoaxLight_Brightness = CoaxLight_Brightness;
            target.SideLight_Enabled = SideLight_Enabled;
            target.SideLight_Brightness = SideLight_Brightness;
            target.Ring7Light_Enabled = Ring7Light_Enabled;
            target.Ring7Light_Brightness = Ring7Light_Brightness;

            // FAI 리스트 깊은 복사. 기존엔 의도적으로 제외되어 있어, Shot 을 복사해도 검사 항목이
            //  하나도 따라오지 않았다(사용자가 100개 넘는 항목을 손으로 다시 티칭해야 했던 원인).
            //  참조 공유가 아니라 새 FAIConfig 를 만들어 채운다 — 안 그러면 한쪽 편집이 다른 쪽에 번진다.
            //  FAIName 은 FAIConfig.CopyTo 의 제외 목록에 있으므로 AddFAI 인자로 직접 넘겨 보존한다.
            target.ClearFAIs();
            for (int i = 0; i < FAIList.Count; i++) {
                FAIConfig srcFai = FAIList[i];
                FAIConfig dstFai = target.AddFAI(srcFai.FAIName);
                srcFai.CopyTo(dstFai);
            }

            return true;
        }

        /// <summary>
        /// 입력된 HImage 를 내부 buffer 에 clone 하여 보관한다.
        /// 기존 _image 가 있으면 자동으로 Dispose 후 교체된다.
        /// 호출자는 입력 image 의 소유권을 그대로 보유하며, 호출 후에도 직접 Dispose 책임을 진다
        /// (이 메서드는 image.CopyImage() 만 보관하고 입력 본체는 건드리지 않는다).
        /// </summary>
        public void SetImage(HImage image) {
            lock (_imageLock) {
                if (_image != null) _image.Dispose();
                if (image != null) _image = image.CopyImage();
                else _image = null;
            }
        }

        /// <summary>
        /// 현재 buffer 의 HImage 를 clone 하여 반환한다.
        /// **호출자가 반환된 HImage 의 Dispose 책임을 진다** — using 블록 또는 try/finally 로 해제할 것.
        /// 정규 소비 패턴: <c>using (var img = shot.GetImage()) { ... }</c> 또는
        /// <c>HImage img = null; try { img = shot.GetImage(); ... } finally { if (img != null) img.Dispose(); }</c>.
        /// buffer 가 비어 있으면 null 을 반환하므로 사용 전 null 검사 필요.
        /// </summary>
        public HImage GetImage() {
            lock (_imageLock) {
                if (_image != null) return _image.CopyImage();
                return null;
            }
        }

        /// <summary>
        /// 내부 buffer 의 HImage 수명을 종료한다 (Dispose 후 null 로 재설정).
        /// BUF-02 lifetime 계약상 다음 3개 채널에서 호출되어야 한다:
        ///   (1) 레시피 변경 — Custom/SystemHandler.cs OnRecipeChanged subscriber 가
        ///       InspectionRecipeManager.ClearShots() 를 호출하여 모든 Shot 에 전파.
        ///   (2) 시퀀스 리셋 — Action_FAIMeasurement.cs EStep.Init 에서
        ///       ShotParam.ClearAllResults() 를 호출하여 도달 (Run 사이클 진입 시).
        ///   (3) 앱 종료 — SystemHandler.Release() 에서
        ///       Sequences.RecipeManager.ClearShots() 를 호출하여 도달.
        /// 멱등 (idempotent): 이미 비어 있는 buffer 에 호출해도 안전 (null-safe).
        /// </summary>
        public void ClearImage() {
            lock (_imageLock) {
                if (_image != null) _image.Dispose();
                _image = null;
            }
        }

        /// <summary>
        /// quick-260806-dsn Part B: 배치 사이클 완료 후 메모리 정리로 _image 가 비워진 뒤에도 화면 재현이 가능한지
        /// 판단하기 위한 디스크 폴백 경로 조회. FAIList 의 각 FAI가 보유한 원본 캡쳐 파일
        /// (FAIConfig.LastOriginImageFileName — CaptureImageSaveService 가 매 검사마다 overlay 없이 저장하는 원본,
        /// Action_FAIMeasurement.QueueFaiCapture 가 기록) 중 실제 존재하는 첫 경로를 반환한다.
        /// overlay 는 이 경로와 별개로 FAIConfig.LastOverlays 를 통해 항상 재현되므로(RenderStoredOverlaysForFai),
        /// 여기서는 원본(overlay 미포함) 이미지 경로만 반환하면 된다.
        /// 반환값이 null/빈 문자열이면 호출자는 _image 정리를 건너뛰어야 한다(재클릭 시 빈 화면 회귀 방지).
        /// </summary>
        public string ResolveFallbackImagePath() {
            if (FAIList == null) return null;
            foreach (var fai in FAIList) {
                if (fai == null) continue;
                string path = fai.LastOriginImageFileName;
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) return path;
            }
            return null;
        }

        // 빈값 폴백 + 향후 ShotConfig 신규 필드 default 정규화 단일 진입점.
        //  InspectionRecipeManager.LoadPhase6Format 의 shot.Load 다음에 호출.
        public void ApplyShotDefaults() {
            if (string.IsNullOrEmpty(OwnerSequenceName)) {
                OwnerSequenceName = "TOP"; // SequenceHandler.SEQ_TOP — 하위 호환 (모든 기존 Shots = Top 소속)
            }
            if (SimulImagePath == null) {
                SimulImagePath = "";
            }
        }

        public FAIConfig AddFAI(string name = null) {
            string faiName = name;
            if (faiName == null) faiName = $"FAI_{FAIList.Count}";
            var fai = new FAIConfig(this, faiName);
            FAIList.Add(fai);
            return fai;
        }

        public bool RemoveFAI(int index) {
            if (index < 0 || index >= FAIList.Count) return false;
            FAIList.RemoveAt(index);
            return true;
        }

        public void ClearFAIs() {
            FAIList.Clear();
        }

        /// <summary>
        /// Shot 의 모든 결과를 초기화한다 — image buffer Dispose + 각 FAI 결과 clear.
        /// BUF-02 lifetime 계약상 sequence reset 트리거 — Action_FAIMeasurement.cs
        /// EStep.Init 단계 (Run 사이클 진입 시 매번) 에서 호출된다.
        /// </summary>
        public void ClearAllResults() {
            ClearImage();
            foreach (var fai in FAIList) {
                fai.ClearResult();
            }
        }
    }
}
