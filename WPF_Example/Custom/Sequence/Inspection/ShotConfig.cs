using System;
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class ShotConfig : CameraSlaveParam, IOfflineImageParam
    {

        [Category("Shot|Setting")]
        public double ZPosition { get; set; }
        public int DelayMs { get; set; }

        [Category("Shot|Simulation")]
        public string SimulImagePath { get; set; } = "";

        // IOfflineImageParam — MainView Load 버튼이 SHOT 노드 선택 시 경로 저장
        public string GetLatestImagePath()
        {
            return SimulImagePath;
        }

        public void SetLatestImagePath(string imagePath)
        {
            SimulImagePath = imagePath;
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
        public int ZIndex { get; set; } = 0;

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
        [Slidable(0, 255)]
        public int RingLight_Brightness_1 { get; set; }
        public bool RingLight_Enabled_2 { get; set; }
        [Slidable(0, 255)]
        public int RingLight_Brightness_2 { get; set; }
        public bool RingLight_Enabled_3 { get; set; }
        [Slidable(0, 255)]
        public int RingLight_Brightness_3 { get; set; }
        public bool RingLight_Enabled_4 { get; set; }
        [Slidable(0, 255)]
        public int RingLight_Brightness_4 { get; set; }
        public bool RingLight_Enabled_5 { get; set; }
        [Slidable(0, 255)]
        public int RingLight_Brightness_5 { get; set; }
        public bool RingLight_Enabled_6 { get; set; }
        [Slidable(0, 255)]
        public int RingLight_Brightness_6 { get; set; }

        [Category("Light|Back")]
        public bool BackLight_Enabled { get; set; }
        public int BackLight_Brightness { get; set; }

        [Browsable(false)]   //260626 hbk Phase 66 D-03: 검사 PropertyGrid 에서 동축 숨김. INI 키/매핑 코드는 보존(하위호환). 동축 제어는 Align 창(Plan 03).
        [Category("Light|Coax")]
        public bool CoaxLight_Enabled { get; set; }
        [Browsable(false)]   //260626 hbk Phase 66 IN-01: CoaxLight_Brightness 도 PropertyGrid 에서 숨김(동축 2필드 모두 숨김). INI 키 보존(하위호환).
        public int CoaxLight_Brightness { get; set; }

        // 구 통합 필드 — Bar 4채널 개별 제어로 대체. 사유/보존 정책은 위 RingLight_Enabled 주석과 동일.
        [Browsable(false)]
        [Category("Light|Side")]
        public bool SideLight_Enabled { get; set; }
        [Browsable(false)]
        public int SideLight_Brightness { get; set; }

        // Bar 4채널 개별 밝기(0~255) + On/Off. 프로퍼티명 접두사는 INI 하위호환 위해 기존 SideLight_ 유지(물리 조명은 Bar).
        [Category("Light|Bar")]
        public bool SideLight_Enabled_1 { get; set; }
        [Slidable(0, 255)]
        public int SideLight_Brightness_1 { get; set; }
        public bool SideLight_Enabled_2 { get; set; }
        [Slidable(0, 255)]
        public int SideLight_Brightness_2 { get; set; }
        public bool SideLight_Enabled_3 { get; set; }
        [Slidable(0, 255)]
        public int SideLight_Brightness_3 { get; set; }
        public bool SideLight_Enabled_4 { get; set; }
        [Slidable(0, 255)]
        public int SideLight_Brightness_4 { get; set; }

        [Category("Light|Ring7")]   //260626 hbk Phase 66 D-01: Ring7 조명 추가(자유 조합) — Ring/Back/Bar/Ring7 4종
        public bool Ring7Light_Enabled { get; set; }   //260626 hbk Ring7 ON/OFF
        public int Ring7Light_Brightness { get; set; }   //260626 hbk Ring7 밝기 0~255

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
