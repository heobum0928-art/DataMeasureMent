//260612 hbk Phase 41.1 OUT-03 50회 반복 실행 서비스
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    /// <summary>
    /// InspectionSequence 를 지정 횟수만큼 자동 반복 실행하고 결과를 누적한다.
    /// UI 레이어가 인스턴스를 소유한다 (static 금지 — 다중 동시 실행 방지 용이).
    /// Start() → OnFinish 구독 → 완료마다 HandleFinish() → TargetCount 도달 시 OnRepeatComplete 발화.
    /// </summary>
    public class RepeatRunService
    {
        public const int DEFAULT_REPEAT_COUNT = 50;

        /// <summary>자재번호 미지정 sentinel. CycleResultDto.IndexNumber 기본값과 동일.</summary>
        public const int MATERIAL_NOT_SET = -1;

        /// <summary>모든 반복이 완료되면 발화. arg = 누적된 CycleResultDto 전체 목록.</summary>
        public event Action<List<CycleResultDto>> OnRepeatComplete;

        /// <summary>각 반복 완료마다 발화. (현재 완료 횟수, 목표 횟수).</summary>
        public event Action<int, int> OnProgressChanged;

        public bool IsRunning { get; private set; }
        public int CompletedCount { get; private set; }
        public int TargetCount { get; private set; }

        /// <summary>
        /// 이번 실행에 부여할 자재번호. Start/StartFromImages 호출 전에 설정한다.
        /// MATERIAL_NOT_SET(-1) 이면 기존 동작 그대로(미지정) — TCP $TEST 경로와 무관.
        /// </summary>
        public int MaterialIndexNumber { get; set; } = MATERIAL_NOT_SET;

        private InspectionSequence _seq;
        private List<CycleResultDto> _collected;
        private EventSequenceStateChanged _onFinishHandler;
        private readonly object _lock = new object();

        //260615 hbk Quick 260615-dx7 이미지 폴더 순회 모드. null = 기존 고정 이미지 반복 모드.
        private List<string> _imagePaths;

        // quick-260911-fia Task 3 WARNING 수정: "이 시퀀스를 점유한 반복 실행" 정적 표시 — 이미지 폴더
        //  반복검사(StartFromImages)와 저장 사진 재검사(StartFromSavedCycles)가 서로 다른 RepeatRunService
        //  인스턴스로 동시에 같은 시퀀스를 돌리는 것을 막는다(사진 경로가 서로 덮어쓰며 섞이는 것 방지).
        //  키는 시퀀스 이름(정적, 인스턴스 참조 아님) — 인스턴스별 _bHoldsOccupancy 가 "이 인스턴스가 실제로
        //  점유했는가"를 기억해, 점유하지 않은 인스턴스의 Stop() 이 남의 점유를 실수로 해제하지 않게 한다.
        private static readonly object s_occupancyLock = new object();
        private static readonly HashSet<string> s_occupiedSequenceNames = new HashSet<string>();
        private bool _bHoldsOccupancy;

        private static bool TryAcquireSequenceOccupancy(InspectionSequence seq, out string szError)
        {
            szError = null;
            if (seq == null)
            {
                szError = "시퀀스가 지정되지 않았습니다";
                return false;
            }
            lock (s_occupancyLock)
            {
                if (s_occupiedSequenceNames.Contains(seq.Name))
                {
                    szError = "이 시퀀스는 이미 다른 반복 실행(이미지 폴더 반복검사 또는 저장 사진 재검사) 중입니다";
                    return false;
                }
                s_occupiedSequenceNames.Add(seq.Name);
                return true;
            }
        }

        private static void ReleaseSequenceOccupancy(InspectionSequence seq)
        {
            if (seq == null)
            {
                return;
            }
            lock (s_occupancyLock)
            {
                s_occupiedSequenceNames.Remove(seq.Name);
            }
        }

        /// <summary>
        /// 반복 실행을 시작한다. IsRunning=true 이면 중복 시작 방지로 즉시 반환.
        /// </summary>
        public void Start(InspectionSequence seq, int targetCount = DEFAULT_REPEAT_COUNT)
        {
            if (IsRunning)
            {
                return;
            }

            if (seq == null)
            {
                return;
            }

            IsRunning = true;
            _seq = seq;
            TargetCount = targetCount;
            CompletedCount = 0;
            _collected = new List<CycleResultDto>();

            _onFinishHandler = (ctx) => HandleFinish(ctx);
            _seq.OnFinish += _onFinishHandler;

            TriggerNext();
        }

        //260615 hbk Quick 260615-dx7
        /// <summary>
        /// 이미지 폴더 순회 모드로 반복 검사를 시작한다. imagePaths 길이만큼 사이클을 돌리며,
        /// 매 사이클 StartAll 직전에 활성 시퀀스의 모든 Shot SimulImagePath 를 imagePaths[CompletedCount] 로 교체한다.
        /// 1 사이클 = 이미지 1장. IsRunning 또는 입력 부재 시 즉시 반환.
        /// </summary>
        public void StartFromImages(InspectionSequence seq, List<string> imagePaths)
        {
            if (IsRunning)
            {
                return;
            }

            if (seq == null)
            {
                return;
            }

            if (imagePaths == null || imagePaths.Count == 0)
            {
                return;
            }

            // quick-260911-fia Task 3 WARNING 수정: 다른 반복 실행(저장 사진 재검사 등)이 이미 이 시퀀스를
            //  점유했으면 거부 — 사진 경로가 서로 덮어쓰며 섞이는 것을 막는다.
            string szOccupancyError;
            if (!TryAcquireSequenceOccupancy(seq, out szOccupancyError))
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[RepeatRunService] StartFromImages 거부: " + szOccupancyError); } catch { }
                return;
            }
            _bHoldsOccupancy = true;

            IsRunning = true;
            _seq = seq;
            _imagePaths = imagePaths;
            TargetCount = imagePaths.Count;
            CompletedCount = 0;
            _collected = new List<CycleResultDto>();

            _onFinishHandler = (ctx) => HandleFinish(ctx);
            _seq.OnFinish += _onFinishHandler;

            TriggerNext();
        }

        /// <summary>강제 중단. OnFinish 구독을 해제하고 IsRunning = false.</summary>
        public void Stop()
        {
            if (_seq != null && _onFinishHandler != null)
            {
                _seq.OnFinish -= _onFinishHandler;
            }

            // quick-260911-fia Task 3 WARNING 수정: 이 인스턴스가 실제로 점유를 획득했을 때만 해제한다
            //  (점유하지 않은 인스턴스의 Stop() 이 다른 인스턴스의 점유를 실수로 풀지 않도록).
            if (_bHoldsOccupancy)
            {
                ReleaseSequenceOccupancy(_seq);
                _bHoldsOccupancy = false;
            }

            IsRunning = false;
            _onFinishHandler = null;
            _seq = null;
            _imagePaths = null; //260615 hbk Quick 260615-dx7
        }

        //260615 hbk Quick 260615-dx7
        /// <summary>
        /// 폴더 순회 모드에서 현재 사이클(CompletedCount 인덱스)의 이미지를 활성 시퀀스의 모든 Shot 에 적용한다.
        /// _imagePaths == null (고정 모드) 이면 무동작 — 기존 동작 보존.
        /// </summary>
        private void ApplyCurrentImage()
        {
            if (_imagePaths == null)
            {
                return;
            }

            int idx = CompletedCount;
            if (idx < 0 || idx >= _imagePaths.Count)
            {
                return;
            }

            string path = _imagePaths[idx];
            var seqHandler = SystemHandler.Handle.Sequences;
            if (seqHandler == null)
            {
                return;
            }

            var recipeManager = seqHandler.RecipeManager;
            if (recipeManager == null)
            {
                return;
            }

            foreach (var shot in recipeManager.Shots)
            {
                shot.SimulImagePath = path;
            }
        }

        // quick-260911-fia Task 3(0): 순수 이동 리팩터 — HandleFinish 의 "소유 shot 종합판정 → BuildDto"
        //  구간을 그대로 추출한다(로직/로그 문구 무변경, WR-01/WR-02/N5C-04 주석 함께 이동).
        //  HandleSavedCycleFinish(Task 3b) 도 동일 로직을 재사용한다.
        private CycleResultDto BuildRunCycleDto(InspectionRecipeManager recipeManager, InspectionSequence seqRef, int nIndexNumber)
        {
            //260805 hbk Phase 70 WR-02: _seq 를 한 번만 읽어 로컬에 고정한다. 기존에는 null 체크와
            //  .Name 접근에서 _seq 를 두 번 읽어, 그 사이 다른 스레드(Stop())가 _seq=null 로 바꾸면
            //  seqName 이 null 로 빠지고 — IsShotOwnedBySequence 계약상 null 은 "전체 매칭" 이라 이번
            //  Phase 70 필터가 그 좁은 창에서 조용히 무력화될 위험이 있었다(TOCTOU). 로컬 1회 읽기로 차단.
            //260805 hbk Phase 70 N5C-04: 이 반복검사를 실제로 돌린 시퀀스 이름. 아래 종합판정 스코프와
            //  BuildDto 의 shot 스코프가 같은 기준을 쓰도록 한 곳에서만 산출한다.
            string seqName;
            if (seqRef != null)
            {
                seqName = seqRef.Name;
            }
            else
            {
                seqName = null; // 소유 시퀀스 미상 → IsShotOwnedBySequence 가 레거시 전역 동작 유지
            }

            // ComputeOverallResult 는 InspectionSequence private — recipeManager 직접 순회하여 EVisionResultType 산출
            bool anySkip = false;
            bool allPass = true;
            //260805 hbk Phase 70 WR-01: 소유권 필터가 0건을 매칭하면 아래 초기값(anySkip=false/allPass=true)이
            //  그대로 남아 "측정 0건인데 OK" 를 조용히 반환한다 — 이 카운터로 그 빈 스코프를 잡는다.
            int nMatchedFaiCount = 0;
            foreach (var shot in recipeManager.Shots)
            {
                //260805 hbk Phase 70 N5C-04: 이 시퀀스 소유 shot 만 종합판정에 포함.
                bool bOwnedByThisSeq = InspectionSequence.IsShotOwnedBySequence(shot, seqName);
                if (!bOwnedByThisSeq)
                {
                    continue;
                }
                foreach (var fai in shot.FAIList)
                {
                    nMatchedFaiCount++;
                    if (fai.WasDatumSkipped)
                    {
                        anySkip = true;
                    }
                    else if (!fai.IsPass)
                    {
                        allPass = false;
                    }
                }
            }

            bool bEmptyScope = nMatchedFaiCount == 0;
            EVisionResultType resultType;
            if (bEmptyScope)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase70] RepeatRunService " + seqName + " 소유 shot/FAI 0건 — 종합판정 스킵 위험, NotExist 로 폴백"); } catch { }
                resultType = EVisionResultType.NotExist;
            }
            else if (anySkip)
            {
                resultType = EVisionResultType.NotExist;
            }
            else if (!allPass)
            {
                resultType = EVisionResultType.NG;
            }
            else
            {
                resultType = EVisionResultType.OK;
            }

            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
            CycleResultDto dto = CycleResultSerializer.BuildDto(
                recipeManager, resultType, DateTime.Now, recipeName, seqName, nIndexNumber);
            return dto;
        }

        private void HandleFinish(SequenceContext ctx)
        {
            lock (_lock)
            {
                var seqHandler = SystemHandler.Handle.Sequences;
                if (seqHandler == null)
                {
                    return;
                }

                var recipeManager = seqHandler.RecipeManager;
                if (recipeManager == null)
                {
                    return;
                }

                InspectionSequence seqRef = _seq;
                CycleResultDto dto = BuildRunCycleDto(recipeManager, seqRef, MaterialIndexNumber);

                // 기존 경로 영속화 유지 (HandleManualCyclePersist 와 중복 저장 주의 — 반복 모드에서는 수동 경로이므로 OnFinish 가 1회 발화)
                CycleResultSerializer.SaveAsync(dto);

                _collected.Add(dto);
                CompletedCount++;

                OnProgressChanged?.Invoke(CompletedCount, TargetCount);

                if (CompletedCount >= TargetCount)
                {
                    var finalList = new List<CycleResultDto>(_collected);
                    Stop();
                    OnRepeatComplete?.Invoke(finalList);
                }
                else
                {
                    TriggerNext();
                }
            }
        }

        private void TriggerNext()
        {
            if (!IsRunning || _seq == null)
            {
                return;
            }

            if (_seq.State == EContextState.Idle)
            {
                // 260811 odo: SequenceContext 결과 이미지에 refcount 소유권 모델이 도입되면서(SetResultImageOwned/
                //  AcquireResultImage), 이 Background 우회의 "안전(크래시 방지)" 역할은 이제 상위 계층
                //  (SequenceContext)에서 구조적으로 이미 보장된다 — 더 이상 이 우회가 경합을 "해소"하는 게
                //  아니다. 그럼에도 코드는 그대로 유지한다: Dispatcher.BeginInvoke(Normal) 로 큐된
                //  OnSequenceFinish 핸들러(이미지 표시)가 Background 보다 먼저 실행되게 하는 "순서 보장"
                //  역할은 여전히 유효하고, 그 순서 보장이 수동 반복검사에서 표시 신선도를 지켜준다(먼저
                //  그려진 뒤에야 다음 사이클이 시작됨). 제거하면 크래시하지는 않지만 반복검사 표시가 드물게
                //  스킵되는 회귀만 생기므로 의도적으로 유지한다. 두 수정(여기 + SequenceContext)은 모순되지
                //  않는다 — 안전 책임과 순서 보장 책임이 분리된 것뿐이다.
                // 사이클이 누적될수록 Normal 큐가 밀려 50ms 만으로는 보장이 안 되므로 우선순위 기반으로 교체.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        if (!IsRunning || _seq == null)
                        {
                            return;
                        }

                        if (_seq.State == EContextState.Idle)
                        {
                            ApplyCurrentImage(); //260615 hbk Quick 260615-dx7 — 폴더 모드: 현재 사이클 이미지 적용
                            _seq.StartAll(null);
                        }
                        else
                        {
                            Task.Delay(50).ContinueWith(_ => TriggerNext());
                        }
                    }));
            }
            else
            {
                // 시퀀스가 아직 실행 중이면 짧은 지연 후 재시도
                Task.Delay(50).ContinueWith(_ => TriggerNext());
            }
        }

        // ==================== quick-260911-fia Task 3: 저장 사진으로 재검사 ====================
        //  기존 Start/StartFromImages(고정 반복/이미지 폴더 반복)와 완전히 별개인 새 실행 모드.
        //  부품마다 SavedCycleRerunPlanner 가 만든 경로로 레시피를 메모리에서만 바꿔 StartAll(null) 로 돌리고,
        //  모든 종료 경로(완료/사용자 중단/시퀀스 중단·오류/UI 스레드 예외/PLC 자동 검사 개입)에서 반드시
        //  EndSavedCycleRerun 단일 지점을 거쳐 원래 경로·OfflineInspectMode 로 되돌린다.

        private const int SAVED_CYCLE_IDLE_POLL_MS = 50;
        private const int SAVED_CYCLE_STOP_POLL_MS = 100;
        private const int SAVED_CYCLE_STOP_WAIT_MAX_MS = 120000;

        /// <summary>모든 저장 사진 재검사 종료(사유 null=정상 완료) 시 발화. arg2 = 수집된 CycleResultDto 복사본.</summary>
        public event Action<List<CycleResultDto>, string> OnSavedCycleRerunEnded;

        private static readonly object s_savedCycleLock = new object();
        private static RepeatRunService s_activeSavedCycleRun;

        /// <summary>현재 이 프로세스에서 저장 사진 재검사가 실행 중인지(다른 인스턴스 포함, 전역 단일 실행 게이트).</summary>
        public static bool IsSavedCycleRerunActive
        {
            get
            {
                lock (s_savedCycleLock)
                {
                    return s_activeSavedCycleRun != null;
                }
            }
        }

        /// <summary>
        /// 프로그램 종료(Window_Closing) 직전, 활성 재검사가 있으면 강제로 끝내 경로/OfflineInspectMode 를
        /// 원복한다 — SystemHandler.Release() 의 Setting.Save() 보다 반드시 먼저 호출돼야 한다.
        /// </summary>
        public static void RestoreActiveSavedCycleOverridesForShutdown()
        {
            RepeatRunService active;
            lock (s_savedCycleLock)
            {
                active = s_activeSavedCycleRun;
            }
            if (active != null)
            {
                active.EndSavedCycleRerun("프로그램 종료");
            }
        }

        private SavedCycleRerunPlan _savedCyclePlan;
        private SavedCycleOverrideSnapshot _savedCycleSnapshot;
        private InspectionSequence _savedCycleSeq;
        private EventSequenceStateChanged _onSavedCycleFinishHandler;
        private EventSequenceStateChanged _onSavedCycleStopHandler;
        private EventSequenceStateChanged _onSavedCycleErrorHandler;
        private volatile bool _bSavedCycleStopRequested;
        private string _szSavedCycleStopReason;
        private volatile bool _bSavedCycleEnded;

        // quick-260911-fia Task 3(3): 재검사 시작 시점의 경로/설정 스냅샷 — 모든 종료 경로에서 이 값으로 되돌린다.
        //  Dictionary 키를 객체 참조로 둔다(같은 이름의 Shot/측정이 있어도 정확히 그 인스턴스만 되돌리기 위함).
        private sealed class SavedCycleOverrideSnapshot
        {
            public readonly Dictionary<ShotConfig, string> ShotSimulImagePaths = new Dictionary<ShotConfig, string>();
            public readonly Dictionary<DatumConfig, string> DatumTeachingPaths = new Dictionary<DatumConfig, string>();
            public readonly Dictionary<DatumConfig, string> DatumTeachingPathsVertical = new Dictionary<DatumConfig, string>();
            public readonly Dictionary<DualImageEdgeDistanceMeasurement, string> DualHorizontalPaths = new Dictionary<DualImageEdgeDistanceMeasurement, string>();
            public readonly Dictionary<DualImageEdgeDistanceMeasurement, string> DualVerticalPaths = new Dictionary<DualImageEdgeDistanceMeasurement, string>();
            public readonly List<ShotConfig> OwnedShots = new List<ShotConfig>();
            public bool OfflineBefore;
            public bool OfflineSetByRerun;
            public string RecipeName;
        }

        private SavedCycleOverrideSnapshot BuildOverrideSnapshot(InspectionSequence seq, InspectionRecipeManager recipeManager)
        {
            var snap = new SavedCycleOverrideSnapshot();
            snap.RecipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
            snap.OfflineBefore = SystemSetting.Handle.OfflineInspectMode;

            foreach (var shot in recipeManager.Shots)
            {
                if (!InspectionSequence.IsShotOwnedBySequence(shot, seq.Name))
                {
                    continue;
                }
                snap.OwnedShots.Add(shot);
                snap.ShotSimulImagePaths[shot] = shot.SimulImagePath;
                foreach (var fai in shot.FAIList)
                {
                    foreach (var meas in fai.Measurements)
                    {
                        var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                        if (dualMeas == null)
                        {
                            continue;
                        }
                        snap.DualHorizontalPaths[dualMeas] = dualMeas.TeachingImagePath_Horizontal;
                        snap.DualVerticalPaths[dualMeas] = dualMeas.TeachingImagePath_Vertical;
                    }
                }
            }

            foreach (var datum in seq.DatumConfigs)
            {
                snap.DatumTeachingPaths[datum] = datum.TeachingImagePath;
                snap.DatumTeachingPathsVertical[datum] = datum.TeachingImagePath_Vertical;
            }

            return snap;
        }

        // 스냅샷 값으로 경로만 되돌린다(OfflineInspectMode 는 별도 — RestoreSavedCycleOverrides 가 처리).
        //  ApplySavedCyclePart(부품마다 "전부 원복 후 이번 부품 값으로 덮어쓰기")와 최종 종료 복원이 공유.
        private static void RestoreOverridePathsOnly(SavedCycleOverrideSnapshot snap)
        {
            foreach (var pair in snap.ShotSimulImagePaths)
            {
                pair.Key.SimulImagePath = pair.Value;
            }
            foreach (var pair in snap.DatumTeachingPaths)
            {
                pair.Key.TeachingImagePath = pair.Value;
            }
            foreach (var pair in snap.DatumTeachingPathsVertical)
            {
                pair.Key.TeachingImagePath_Vertical = pair.Value;
            }
            foreach (var pair in snap.DualHorizontalPaths)
            {
                pair.Key.TeachingImagePath_Horizontal = pair.Value;
            }
            foreach (var pair in snap.DualVerticalPaths)
            {
                pair.Key.TeachingImagePath_Vertical = pair.Value;
            }
        }

        /// <summary>
        /// 저장 사진 재검사를 시작한다. UI 스레드에서 호출 전제(StatisticsRerunViewModel 이 Dispatcher 위에서 호출).
        /// </summary>
        public bool StartFromSavedCycles(InspectionSequence seq, SavedCycleRerunPlan plan, out string szError)
        {
            szError = null;

            if (IsRunning)
            {
                szError = "이미 반복 실행 중입니다";
                return false;
            }
            lock (s_savedCycleLock)
            {
                if (s_activeSavedCycleRun != null)
                {
                    szError = "다른 저장 사진 재검사가 이미 실행 중입니다";
                    return false;
                }
            }
            bool bMissingArgs = seq == null || plan == null;
            if (bMissingArgs)
            {
                szError = "시퀀스 또는 재검사 계획이 없습니다";
                return false;
            }
            if (plan.Parts == null || plan.Parts.Count == 0)
            {
                szError = "재검사할 부품이 없습니다";
                return false;
            }
            if (seq.State != EContextState.Idle)
            {
                szError = "시퀀스가 실행 중입니다";
                return false;
            }
            var seqHandler = SystemHandler.Handle.Sequences;
            bool bNoRecipeManager = seqHandler == null || seqHandler.RecipeManager == null;
            if (bNoRecipeManager)
            {
                szError = "레시피 정보를 찾을 수 없습니다";
                return false;
            }

            // quick-260911-fia Task 3 WARNING 수정: 기존 이미지 폴더 반복검사와 상호 배제.
            string szOccupancyError;
            if (!TryAcquireSequenceOccupancy(seq, out szOccupancyError))
            {
                szError = szOccupancyError;
                return false;
            }
            _bHoldsOccupancy = true;

            var recipeManager = seqHandler.RecipeManager;
            _savedCycleSeq = seq;
            _savedCyclePlan = plan;
            _savedCycleSnapshot = BuildOverrideSnapshot(seq, recipeManager);
            _bSavedCycleStopRequested = false;
            _szSavedCycleStopReason = null;
            _bSavedCycleEnded = false;

            // 메모리에서만 ON — Setting.Save 호출 없음(레시피/설정 파일 영속화 금지, T-FIA-02).
            if (!SystemSetting.Handle.OfflineInspectMode)
            {
                SystemSetting.Handle.OfflineInspectMode = true;
                _savedCycleSnapshot.OfflineSetByRerun = true;
            }

            lock (s_savedCycleLock)
            {
                s_activeSavedCycleRun = this;
            }
            IsRunning = true;
            TargetCount = plan.Parts.Count;
            CompletedCount = 0;
            _collected = new List<CycleResultDto>();

            _onSavedCycleFinishHandler = (ctx) => HandleSavedCycleFinish(ctx);
            _onSavedCycleStopHandler = (ctx) => EndSavedCycleRerun("시퀀스가 중단/오류로 멈춤");
            _onSavedCycleErrorHandler = (ctx) => EndSavedCycleRerun("시퀀스가 중단/오류로 멈춤");
            seq.OnFinish += _onSavedCycleFinishHandler;
            seq.OnStop += _onSavedCycleStopHandler;
            seq.OnError += _onSavedCycleErrorHandler;

            TriggerNextSavedCyclePart();
            return true;
        }

        // OfflineInspectMode OFF(PLC 자동검사 강제) 또는 레시피/소유 Shot 변경(레시피 재로드) 감지 —
        //  둘 다 재검사를 즉시 중단해야 하는 사유다. 문제 없으면 null.
        private string FindSavedCycleAbortReason()
        {
            if (!SystemSetting.Handle.OfflineInspectMode)
            {
                return "PLC 자동 검사 수신으로 오프라인 모드가 꺼짐 — 재검사 중단";
            }

            bool bRecipeChanged = !string.Equals(SystemHandler.Handle.Setting.CurrentRecipeName, _savedCycleSnapshot.RecipeName, StringComparison.Ordinal);
            if (bRecipeChanged)
            {
                return "레시피가 바뀌어 재검사 중단";
            }

            var seqHandler = SystemHandler.Handle.Sequences;
            bool bNoRecipeManager = seqHandler == null || seqHandler.RecipeManager == null;
            if (bNoRecipeManager)
            {
                return "레시피가 바뀌어 재검사 중단";
            }
            foreach (var shot in _savedCycleSnapshot.OwnedShots)
            {
                if (!seqHandler.RecipeManager.Shots.Contains(shot))
                {
                    return "레시피가 바뀌어 재검사 중단";
                }
            }
            return null;
        }

        private void TriggerNextSavedCyclePart()
        {
            if (_bSavedCycleEnded || _savedCycleSeq == null)
            {
                return;
            }

            if (_savedCycleSeq.State == EContextState.Idle)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => RunSavedCycleTriggerOnUiThread()));
            }
            else
            {
                Task.Delay(SAVED_CYCLE_IDLE_POLL_MS).ContinueWith(_ => TriggerNextSavedCyclePart());
            }
        }

        // mandatory amendment(BLOCKER) 수정: UI 스레드(Dispatcher 콜백) 전체를 try/catch 로 감싼다 —
        //  예외가 나면 레시피 경로·OfflineInspectMode·IsSavedCycleRerunActive 가 원복되지 않은 채
        //  멈추는 것을 막는다. StartAll 이 false 를 반환하는 경우도 EndSavedCycleRerun 으로 보낸다.
        private void RunSavedCycleTriggerOnUiThread()
        {
            try
            {
                if (_bSavedCycleEnded || _savedCycleSeq == null)
                {
                    return;
                }
                if (_bSavedCycleStopRequested)
                {
                    EndSavedCycleRerun(_szSavedCycleStopReason);
                    return;
                }
                if (_savedCycleSeq.State != EContextState.Idle)
                {
                    Task.Delay(SAVED_CYCLE_IDLE_POLL_MS).ContinueWith(_ => TriggerNextSavedCyclePart());
                    return;
                }

                string szAbortReason = FindSavedCycleAbortReason();
                if (!string.IsNullOrEmpty(szAbortReason))
                {
                    EndSavedCycleRerun(szAbortReason);
                    return;
                }

                ApplySavedCyclePart(_savedCyclePlan.Parts[CompletedCount]);
                // Test Find 유지 캐시/이전 부품 기준점 캐시 해제 — 부품마다 새로 찾기(PLAN 확인된 코드 사실).
                _savedCycleSeq.ClearDatumTransforms();

                bool bStarted = _savedCycleSeq.StartAll(null);
                if (!bStarted)
                {
                    EndSavedCycleRerun("시퀀스 시작 실패");
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Rerun] 부품 시작 중 예외: " + ex.Message); } catch { }
                EndSavedCycleRerun("경로 적용 중 오류: " + ex.Message);
            }
        }

        // 먼저 스냅샷 값으로 전부 되돌린 뒤 이번 부품 값으로 덮어쓴다(부품 간 사진 잔류 방지).
        private void ApplySavedCyclePart(SavedCycleRerunPart part)
        {
            RestoreOverridePathsOnly(_savedCycleSnapshot);

            foreach (var shot in _savedCycleSnapshot.OwnedShots)
            {
                string szShotPath;
                if (part.ShotPhotoPaths.TryGetValue(shot.ShotName, out szShotPath))
                {
                    shot.SimulImagePath = szShotPath;
                }
            }

            foreach (var datum in _savedCycleSeq.DatumConfigs)
            {
                string szKeySingle = SavedCycleRerunPlanner.BuildDatumRoleKey(datum.DatumName, DatumImageRecordDto.ROLE_SINGLE);
                string szKeyHorizontal = SavedCycleRerunPlanner.BuildDatumRoleKey(datum.DatumName, DatumImageRecordDto.ROLE_HORIZONTAL);
                string szKeyVertical = SavedCycleRerunPlanner.BuildDatumRoleKey(datum.DatumName, DatumImageRecordDto.ROLE_VERTICAL);

                string szPathSingle;
                if (part.DatumPhotoPaths.TryGetValue(szKeySingle, out szPathSingle))
                {
                    datum.TeachingImagePath = szPathSingle;
                }
                string szPathHorizontal;
                if (part.DatumPhotoPaths.TryGetValue(szKeyHorizontal, out szPathHorizontal))
                {
                    datum.TeachingImagePath = szPathHorizontal;
                }
                string szPathVertical;
                if (part.DatumPhotoPaths.TryGetValue(szKeyVertical, out szPathVertical))
                {
                    datum.TeachingImagePath_Vertical = szPathVertical;
                }
            }

            foreach (var dual in part.DualPhotos)
            {
                DualImageEdgeDistanceMeasurement measRef = FindOwnedDualMeasurement(dual.ShotName, dual.FAIName, dual.MeasurementName);
                if (measRef == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(dual.HorizontalPath))
                {
                    measRef.TeachingImagePath_Horizontal = dual.HorizontalPath;
                }
                if (!string.IsNullOrEmpty(dual.VerticalPath))
                {
                    measRef.TeachingImagePath_Vertical = dual.VerticalPath;
                }
            }
        }

        private DualImageEdgeDistanceMeasurement FindOwnedDualMeasurement(string szShotName, string szFaiName, string szMeasKey)
        {
            foreach (var shot in _savedCycleSnapshot.OwnedShots)
            {
                if (!string.Equals(shot.ShotName, szShotName, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var fai in shot.FAIList)
                {
                    if (!string.Equals(fai.FAIName, szFaiName, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (var meas in fai.Measurements)
                    {
                        var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                        if (dualMeas == null)
                        {
                            continue;
                        }
                        string szKey;
                        if (string.IsNullOrEmpty(dualMeas.MeasurementName))
                        {
                            szKey = dualMeas.TypeName;
                        }
                        else
                        {
                            szKey = dualMeas.MeasurementName;
                        }
                        if (string.Equals(szKey, szMeasKey, StringComparison.Ordinal))
                        {
                            return dualMeas;
                        }
                    }
                }
            }
            return null;
        }

        // 이 tick 의 cycle.json 에서 그 부품(재검사 대상)에 없던 Shot(레시피에만 있고 자동 사이클에는 없던
        //  Shot)을 제거한다 — 옛 사진 값이 통계에 섞이는 것 방지.
        private static void RemoveUnexpectedShots(CycleResultDto dto, SavedCycleRerunPart part)
        {
            if (dto.Shots != null)
            {
                for (int i = dto.Shots.Count - 1; i >= 0; i--)
                {
                    if (!part.ShotPhotoPaths.ContainsKey(dto.Shots[i].ShotName))
                    {
                        dto.Shots.RemoveAt(i);
                    }
                }
            }
            if (dto.MeasuredShotNames != null)
            {
                for (int i = dto.MeasuredShotNames.Count - 1; i >= 0; i--)
                {
                    if (!part.ShotPhotoPaths.ContainsKey(dto.MeasuredShotNames[i]))
                    {
                        dto.MeasuredShotNames.RemoveAt(i);
                    }
                }
            }
        }

        // OnFinish(시퀀스 스레드). 정상 완료 tick 을 저장/집계하고 다음 부품으로 넘어가거나 재검사를 끝낸다.
        //  PLC 자동 검사 개입/사용자 중단은 결과를 버리고 즉시 EndSavedCycleRerun 으로 보낸다.
        private void HandleSavedCycleFinish(SequenceContext ctx)
        {
            string szEarlyEndReason = null;
            bool bShouldComplete = false;
            int nCompletedSnapshot = 0;
            int nTargetSnapshot = 0;

            try
            {
                lock (_lock)
                {
                    if (_bSavedCycleEnded)
                    {
                        return;
                    }

                    bool bAutoCycleIntruded = _savedCycleSeq.IsProtocolDrivenCycle() || !SystemSetting.Handle.OfflineInspectMode;
                    if (bAutoCycleIntruded)
                    {
                        szEarlyEndReason = "PLC 자동 검사가 들어와 재검사 중단";
                    }
                    else if (_bSavedCycleStopRequested)
                    {
                        szEarlyEndReason = _szSavedCycleStopReason;
                    }

                    bool bNormalTick = string.IsNullOrEmpty(szEarlyEndReason);
                    if (bNormalTick)
                    {
                        var seqHandler = SystemHandler.Handle.Sequences;
                        bool bNoRecipeManager = seqHandler == null || seqHandler.RecipeManager == null;
                        if (bNoRecipeManager)
                        {
                            szEarlyEndReason = "레시피 정보를 찾을 수 없음 — 재검사 중단";
                        }
                        else
                        {
                            var recipeManager = seqHandler.RecipeManager;
                            SavedCycleRerunPart part = _savedCyclePlan.Parts[CompletedCount];
                            CycleResultDto dto = BuildRunCycleDto(recipeManager, _savedCycleSeq, part.IndexNumber);
                            RemoveUnexpectedShots(dto, part);
                            CycleResultSerializer.SaveAsync(dto);
                            _collected.Add(dto);
                            CompletedCount++;
                            nCompletedSnapshot = CompletedCount;
                            nTargetSnapshot = TargetCount;
                            bShouldComplete = CompletedCount >= TargetCount;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Rerun] 부품 결과 처리 중 예외: " + ex.Message); } catch { }
                szEarlyEndReason = "결과 처리 중 오류: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(szEarlyEndReason))
            {
                EndSavedCycleRerun(szEarlyEndReason);
                return;
            }

            var progressHandler = OnProgressChanged;
            if (progressHandler != null)
            {
                progressHandler(nCompletedSnapshot, nTargetSnapshot);
            }

            if (bShouldComplete)
            {
                EndSavedCycleRerun(null);
            }
            else
            {
                TriggerNextSavedCyclePart();
            }
        }

        /// <summary>
        /// 사용자 중단 요청. 실행 중인 부품이 있으면 그 부품이 끝난 뒤(OnFinish/OnStop/OnError) 원복한다 —
        /// 부품 도중 사진이 섞이는 것을 막기 위해 즉시 원복하지 않는다. 이미 Idle 이면 즉시 종료.
        /// </summary>
        public void RequestStopSavedCycleRerun(string szReason)
        {
            _bSavedCycleStopRequested = true;
            _szSavedCycleStopReason = szReason;

            bool bIdleNow = _savedCycleSeq != null && _savedCycleSeq.State == EContextState.Idle;
            if (bIdleNow)
            {
                EndSavedCycleRerun(szReason);
            }
            else
            {
                WaitIdleThenEndSavedCycle();
            }
        }

        // 안전망 — 실행 중인 부품이 Idle 로 돌아올 때까지 폴링(최대 SAVED_CYCLE_STOP_WAIT_MAX_MS).
        //  정상적으로는 그 전에 OnFinish/OnStop/OnError 가 먼저 EndSavedCycleRerun 을 호출해 이 루프가
        //  중간에 빠져나온다(EndSavedCycleRerun 은 멱등이라 이중 호출 안전).
        private void WaitIdleThenEndSavedCycle()
        {
            Task.Run(() =>
            {
                int nWaitedMs = 0;
                while (!_bSavedCycleEnded)
                {
                    bool bSeqIdle = _savedCycleSeq == null || _savedCycleSeq.State == EContextState.Idle;
                    if (bSeqIdle)
                    {
                        break;
                    }
                    if (nWaitedMs >= SAVED_CYCLE_STOP_WAIT_MAX_MS)
                    {
                        try { Logging.PrintErrLog((int)ELogType.Error, "[Rerun] 중단 대기 타임아웃(" + nWaitedMs + "ms) — 강제 종료"); } catch { }
                        break;
                    }
                    Thread.Sleep(SAVED_CYCLE_STOP_POLL_MS);
                    nWaitedMs += SAVED_CYCLE_STOP_POLL_MS;
                }
                EndSavedCycleRerun(_szSavedCycleStopReason);
            });
        }

        // 모든 종료 경로(완료/사용자 중단/시퀀스 OnStop/OnError/통계 창 닫힘/프로그램 종료/UI 스레드 예외/
        //  PLC 자동 검사 개입)의 단일 지점. 락 안 bEnded 플래그로 멱등 — 두 번째 호출은 즉시 return.
        private void EndSavedCycleRerun(string szReason)
        {
            List<CycleResultDto> finalList;
            int nCompletedForLog;
            int nTargetForLog;

            lock (_lock)
            {
                if (_bSavedCycleEnded)
                {
                    return;
                }
                _bSavedCycleEnded = true;

                if (_savedCycleSeq != null)
                {
                    if (_onSavedCycleFinishHandler != null)
                    {
                        _savedCycleSeq.OnFinish -= _onSavedCycleFinishHandler;
                    }
                    if (_onSavedCycleStopHandler != null)
                    {
                        _savedCycleSeq.OnStop -= _onSavedCycleStopHandler;
                    }
                    if (_onSavedCycleErrorHandler != null)
                    {
                        _savedCycleSeq.OnError -= _onSavedCycleErrorHandler;
                    }
                }
                _onSavedCycleFinishHandler = null;
                _onSavedCycleStopHandler = null;
                _onSavedCycleErrorHandler = null;

                RestoreSavedCycleOverrides();

                if (_bHoldsOccupancy)
                {
                    ReleaseSequenceOccupancy(_savedCycleSeq);
                    _bHoldsOccupancy = false;
                }

                lock (s_savedCycleLock)
                {
                    if (s_activeSavedCycleRun == this)
                    {
                        s_activeSavedCycleRun = null;
                    }
                }

                IsRunning = false;
                nCompletedForLog = CompletedCount;
                nTargetForLog = TargetCount;

                if (_collected != null)
                {
                    finalList = new List<CycleResultDto>(_collected);
                }
                else
                {
                    finalList = new List<CycleResultDto>();
                }

                _savedCycleSeq = null;
            }

            try
            {
                Logging.PrintLog((int)ELogType.Trace, "[Rerun] 종료 — 완료 " + nCompletedForLog + "/" + nTargetForLog + ", 사유=" + szReason);
            }
            catch { }

            var endedHandler = OnSavedCycleRerunEnded;
            if (endedHandler != null)
            {
                endedHandler(finalList, szReason);
            }
        }

        // 스냅샷 경로 전부 원복 + (우리가 켰고 아직 켜져 있을 때만) OfflineInspectMode 원복.
        //  한 항목의 복원 실패가 나머지 복원을 막지 않도록 각각 try/catch.
        private void RestoreSavedCycleOverrides()
        {
            if (_savedCycleSnapshot == null)
            {
                return;
            }
            try
            {
                RestoreOverridePathsOnly(_savedCycleSnapshot);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Rerun] 경로 원복 실패(무시): " + ex.Message); } catch { }
            }
            try
            {
                bool bShouldTurnOff = _savedCycleSnapshot.OfflineSetByRerun && SystemSetting.Handle.OfflineInspectMode;
                if (bShouldTurnOff)
                {
                    SystemSetting.Handle.OfflineInspectMode = false;
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Rerun] OfflineInspectMode 원복 실패(무시): " + ex.Message); } catch { }
            }
        }
    }

    // quick-260911-fia Task 2: 크로스-Z 두 장짜리 측정 1건의 가로/세로 사진 경로(부품 단위).
    public class SavedCycleDualPhoto
    {
        public string ShotName { get; set; }
        public string FAIName { get; set; }
        public string MeasurementName { get; set; }
        public string HorizontalPath { get; set; }
        public string VerticalPath { get; set; }
    }

    // quick-260911-fia Task 2: 저장 사이클 재검사 계획의 부품(Part) 1건 — 기준점 tick 부터 다음 기준점 tick
    //  전까지의 모든 tick 을 묶고, 그 부품을 재현하는 데 필요한 사진 경로(기준점/Shot/두 장짜리)를 담는다.
    public class SavedCycleRerunPart
    {
        public DateTime StartTime { get; set; }
        public int IndexNumber { get; set; } = RepeatRunService.MATERIAL_NOT_SET;
        public List<CycleResultDto> Ticks { get; set; } = new List<CycleResultDto>();

        /// <summary>키 = SavedCycleRerunPlanner.BuildDatumRoleKey(기준점이름, Role). 값 = 사진 절대경로.</summary>
        public Dictionary<string, string> DatumPhotoPaths { get; set; } = new Dictionary<string, string>();

        /// <summary>키 = ShotName. 값 = 측정 원본 사진 절대경로.</summary>
        public Dictionary<string, string> ShotPhotoPaths { get; set; } = new Dictionary<string, string>();

        public List<SavedCycleDualPhoto> DualPhotos { get; set; } = new List<SavedCycleDualPhoto>();
    }

    // quick-260911-fia Task 2: 저장 사이클(cycle.json) 을 읽어 만든 부품 단위 재검사 계획.
    public class SavedCycleRerunPlan
    {
        public string SequenceName { get; set; }
        public string RecipeName { get; set; }
        public List<SavedCycleRerunPart> Parts { get; set; } = new List<SavedCycleRerunPart>();
        public int AutoTickCount { get; set; }
        public int ExcludedPartCount { get; set; }
        public Dictionary<string, int> ExclusionCounts { get; set; } = new Dictionary<string, int>();
        public List<string> ExclusionDetails { get; set; } = new List<string>();

        /// <summary>"제외 M개: 사유A 3, 사유B 1" 형식. 제외 0건이면 "제외 0개".</summary>
        public string BuildExclusionSummary()
        {
            if (ExcludedPartCount <= 0)
            {
                return "제외 0개";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("제외 ");
            sb.Append(ExcludedPartCount);
            sb.Append("개: ");
            bool bFirst = true;
            foreach (var pair in ExclusionCounts)
            {
                if (!bFirst)
                {
                    sb.Append(", ");
                }
                sb.Append(pair.Key);
                sb.Append(" ");
                sb.Append(pair.Value);
                bFirst = false;
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 저장된 cycle.json(자동 검사 tick) 을 부품(Part) 단위로 묶어 재검사 계획을 만든다.
    /// 재량 결정(SUMMARY 기록): "현재 기준점 사진으로 대체(참고용)" 옵션은 넣지 않는다 — 기준점은 부품마다
    /// 좌표 원점을 다시 잡는 입력이라 다른 부품 사진과 섞으면 결과가 비교 불가한 숫자가 되고, 옵션 분기만 늘어난다.
    /// </summary>
    public static class SavedCycleRerunPlanner
    {
        public const string REASON_NO_DATUM_TICK = "기준점 촬영 없이 시작된 사이클";
        public const string REASON_NO_DATUM_PHOTO = "기준점 사진 없음(저장 기능 이전 사이클)";
        public const string REASON_DATUM_ROLE_MISSING = "기준점 사진 일부 없음";
        public const string REASON_NO_SHOT = "측정 사진 없음";
        public const string REASON_SHOT_MISSING = "Shot 사진 누락";
        public const string REASON_DUAL_MISSING = "두 장짜리 측정 사진 없음";
        public const string REASON_FILE_MISSING = "사진 파일 없음";

        private const string CYCLE_FOLDER_PATTERN = "*_cycle";
        private const string CYCLE_JSON_FILE_NAME = "cycle.json";
        private const int DUAL_CROSS_Z_UNCONFIGURED = -1;

        /// <summary>DatumPhotoPaths/필요 역할 목록 공용 키. 예: "기준점A|H".</summary>
        public static string BuildDatumRoleKey(string szDatumName, string szRole)
        {
            return szDatumName + "|" + szRole;
        }

        public static SavedCycleRerunPlan BuildPlan(DateTime dtFrom, DateTime dtTo, string szRecipeName, InspectionSequence seq, InspectionRecipeManager recipeManager)
        {
            SavedCycleRerunPlan plan = new SavedCycleRerunPlan();
            bool bInvalidArgs = seq == null || recipeManager == null || string.IsNullOrEmpty(szRecipeName) || dtTo < dtFrom;
            if (bInvalidArgs)
            {
                return plan;
            }
            plan.SequenceName = seq.Name;
            plan.RecipeName = szRecipeName;

            List<CycleResultDto> lstTicks = CollectAutoTicks(dtFrom, dtTo, szRecipeName, seq);
            plan.AutoTickCount = lstTicks.Count;
            lstTicks.Sort(CompareByInspectionTime);

            List<SavedCycleRerunPart> lstCandidates = GroupIntoParts(lstTicks, seq, plan);

            foreach (var part in lstCandidates)
            {
                FillPartDatumPhotos(part);
                FillPartShotPhotos(part);
                FillPartDualPhotos(part, seq, recipeManager);
            }

            HashSet<string> setExpectedShots = new HashSet<string>();
            foreach (var part in lstCandidates)
            {
                foreach (string szShotName in part.ShotPhotoPaths.Keys)
                {
                    setExpectedShots.Add(szShotName);
                }
            }
            List<string> lstRequiredDatumKeys = ComputeRequiredDatumRoleKeys(seq);

            foreach (var part in lstCandidates)
            {
                string szReason = ValidatePart(part, setExpectedShots, lstRequiredDatumKeys);
                if (string.IsNullOrEmpty(szReason))
                {
                    plan.Parts.Add(part);
                }
                else
                {
                    RecordExclusion(plan, szReason, "부품 시작=" + part.StartTime.ToString("HH:mm:ss"));
                }
            }

            try
            {
                Logging.PrintLog((int)ELogType.Trace, "[Rerun] " + plan.SequenceName + "/" + plan.RecipeName
                    + " 자동tick=" + plan.AutoTickCount + " 부품=" + plan.Parts.Count + " " + plan.BuildExclusionSummary());
            }
            catch { }

            return plan;
        }

        // 지정 기간의 날짜 폴더를 순회하며 이 시퀀스/레시피의 자동(PLC 프로토콜) tick 만 수집한다.
        //  폴더 하나가 깨져 있어도(손상 JSON 등) 나머지 날짜 수집을 막지 않는다(격리).
        private static List<CycleResultDto> CollectAutoTicks(DateTime dtFrom, DateTime dtTo, string szRecipeName, InspectionSequence seq)
        {
            List<CycleResultDto> lstResult = new List<CycleResultDto>();
            DateTime dCurrent = dtFrom.Date;
            DateTime dLast = dtTo.Date;
            while (dCurrent <= dLast)
            {
                try
                {
                    string szDayDir = Path.Combine(SystemHandler.Handle.Setting.ResultSavePath, dCurrent.ToString("yyyyMMdd"));
                    if (Directory.Exists(szDayDir))
                    {
                        string[] arrCycleDirs = Directory.GetDirectories(szDayDir, CYCLE_FOLDER_PATTERN);
                        foreach (string szCycleDir in arrCycleDirs)
                        {
                            string szJsonPath = Path.Combine(szCycleDir, CYCLE_JSON_FILE_NAME);
                            CycleResultDto dto = CycleResultSerializer.Load(szJsonPath);
                            if (IsEligibleAutoTick(dto, szRecipeName, seq))
                            {
                                lstResult.Add(dto);
                            }
                        }
                    }
                }
                catch
                {
                    // 폴더 단위 예외 격리 — 하루치가 깨져도 나머지 날짜는 계속 수집한다.
                }
                dCurrent = dCurrent.AddDays(1);
            }
            return lstResult;
        }

        private static bool IsEligibleAutoTick(CycleResultDto dto, string szRecipeName, InspectionSequence seq)
        {
            if (dto == null)
            {
                return false;
            }
            bool bBasicMatch = dto.IsProtocolDriven && dto.ZIndex >= 0 && string.Equals(dto.RecipeName, szRecipeName, StringComparison.Ordinal);
            if (!bBasicMatch)
            {
                return false;
            }
            if (dto.Shots == null)
            {
                return false;
            }
            foreach (var shot in dto.Shots)
            {
                string szOwner;
                if (string.IsNullOrEmpty(shot.OwnerSequenceName))
                {
                    szOwner = SequenceHandler.SEQ_TOP;
                }
                else
                {
                    szOwner = shot.OwnerSequenceName;
                }
                if (string.Equals(szOwner, seq.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static int CompareByInspectionTime(CycleResultDto a, CycleResultDto b)
        {
            return a.InspectionTime.CompareTo(b.InspectionTime);
        }

        // 기준점 tick(dto.ZIndex == 이 시퀀스의 GetDatumZIndex()) 이 새 부품의 시작. 첫 기준점 tick 이전에
        //  나타난 tick 들은 어느 부품에도 속하지 않는다 — 있으면 1건("기준점 촬영 없이 시작된 사이클")으로 집계.
        private static List<SavedCycleRerunPart> GroupIntoParts(List<CycleResultDto> lstTicks, InspectionSequence seq, SavedCycleRerunPlan plan)
        {
            List<SavedCycleRerunPart> lstParts = new List<SavedCycleRerunPart>();
            int nDatumZ = seq.GetDatumZIndex();
            SavedCycleRerunPart currentPart = null;
            int nPreDatumTickCount = 0;
            foreach (var dto in lstTicks)
            {
                bool bIsDatumTick = dto.ZIndex == nDatumZ;
                if (bIsDatumTick)
                {
                    currentPart = new SavedCycleRerunPart();
                    currentPart.StartTime = dto.InspectionTime;
                    currentPart.IndexNumber = dto.IndexNumber;
                    lstParts.Add(currentPart);
                }
                if (currentPart == null)
                {
                    nPreDatumTickCount++;
                    continue;
                }
                currentPart.Ticks.Add(dto);
            }
            if (nPreDatumTickCount > 0)
            {
                RecordExclusion(plan, REASON_NO_DATUM_TICK, nPreDatumTickCount + "건");
            }
            return lstParts;
        }

        // 이 측정이 "다뤄진 것"인가(CycleResultSerializer.FillTickSummary 규칙과 동일 —
        //  CROSS_Z_INCOMPLETE 는 다른 z 대기 상태이므로 처리됨에서 제외). Shot/원본사진 채택에 사용.
        private static bool IsMeasurementHandled(MeasurementResultDto meas)
        {
            bool bHasResult = meas.LastHasResult;
            bool bHasReason = !string.IsNullOrEmpty(meas.LastSkipReason) && meas.LastSkipReason != SkipReason.CROSS_Z_INCOMPLETE;
            return bHasResult || bHasReason;
        }

        // 크로스-Z 두 장짜리 측정의 "그 tick 에 처리됨" 판정 — CROSS_Z_INCOMPLETE 도 포함(짝이 되는 z 에서
        //  실제로 그 역할 사진을 찍었다는 신호이기 때문). ③(c) 전용.
        private static bool IsMeasurementProcessedInTick(MeasurementResultDto meas)
        {
            bool bHasResult = meas.LastHasResult;
            bool bHasReason = !string.IsNullOrEmpty(meas.LastSkipReason);
            return bHasResult || bHasReason;
        }

        private static void FillPartDatumPhotos(SavedCycleRerunPart part)
        {
            foreach (var dto in part.Ticks)
            {
                if (dto.DatumImages == null)
                {
                    continue;
                }
                foreach (var img in dto.DatumImages)
                {
                    bool bInvalid = img == null || string.IsNullOrEmpty(img.DatumName) || string.IsNullOrEmpty(img.Path);
                    if (bInvalid)
                    {
                        continue;
                    }
                    string szKey = BuildDatumRoleKey(img.DatumName, img.Role);
                    if (!part.DatumPhotoPaths.ContainsKey(szKey))
                    {
                        part.DatumPhotoPaths[szKey] = img.Path;
                    }
                }
            }
        }

        // dto.MeasuredShotNames 가 있으면 그 목록, 없으면(옛 cycle.json) CycleResultSerializer.FillTickSummary
        // 와 동일 규칙(처리된 측정을 가진 Shot)으로 폴백한다.
        private static List<string> GetProcessedShotNames(CycleResultDto dto)
        {
            if (dto.MeasuredShotNames != null && dto.MeasuredShotNames.Count > 0)
            {
                return dto.MeasuredShotNames;
            }
            List<string> lstNames = new List<string>();
            if (dto.Shots == null)
            {
                return lstNames;
            }
            foreach (var shot in dto.Shots)
            {
                bool bShotHandled = false;
                if (shot.FAIs != null)
                {
                    foreach (var fai in shot.FAIs)
                    {
                        if (fai.Measurements == null)
                        {
                            continue;
                        }
                        foreach (var meas in fai.Measurements)
                        {
                            if (IsMeasurementHandled(meas))
                            {
                                bShotHandled = true;
                                break;
                            }
                        }
                        if (bShotHandled)
                        {
                            break;
                        }
                    }
                }
                if (bShotHandled && !string.IsNullOrEmpty(shot.ShotName))
                {
                    lstNames.Add(shot.ShotName);
                }
            }
            return lstNames;
        }

        private static void FillPartShotPhotos(SavedCycleRerunPart part)
        {
            foreach (var dto in part.Ticks)
            {
                if (dto.Shots == null)
                {
                    continue;
                }
                List<string> lstProcessedShots = GetProcessedShotNames(dto);
                foreach (string szShotName in lstProcessedShots)
                {
                    if (part.ShotPhotoPaths.ContainsKey(szShotName))
                    {
                        continue;
                    }
                    ShotResultDto shotDto = FindShotByName(dto.Shots, szShotName);
                    if (shotDto == null || shotDto.FAIs == null)
                    {
                        continue;
                    }
                    string szOrigin = ResolveShotOriginPath(shotDto);
                    if (!string.IsNullOrEmpty(szOrigin))
                    {
                        part.ShotPhotoPaths[szShotName] = szOrigin;
                    }
                }
            }
        }

        private static ShotResultDto FindShotByName(List<ShotResultDto> lstShots, string szShotName)
        {
            foreach (var shot in lstShots)
            {
                if (string.Equals(shot.ShotName, szShotName, StringComparison.Ordinal))
                {
                    return shot;
                }
            }
            return null;
        }

        // 처리된 측정이 있고 IsDualImage=false 인 측정을 가진 첫 FAI 우선, 없으면 처리된 측정이 있는 첫 FAI.
        private static string ResolveShotOriginPath(ShotResultDto shotDto)
        {
            string szFallback = null;
            foreach (var fai in shotDto.FAIs)
            {
                if (fai.Measurements == null)
                {
                    continue;
                }
                bool bHasProcessedMeasurement = false;
                bool bHasNonDualProcessedMeasurement = false;
                foreach (var meas in fai.Measurements)
                {
                    if (!IsMeasurementHandled(meas))
                    {
                        continue;
                    }
                    bHasProcessedMeasurement = true;
                    if (!meas.IsDualImage)
                    {
                        bHasNonDualProcessedMeasurement = true;
                    }
                }
                bool bTakeAsPrimary = bHasNonDualProcessedMeasurement && !string.IsNullOrEmpty(fai.OriginImageFileName);
                if (bTakeAsPrimary)
                {
                    return fai.OriginImageFileName;
                }
                bool bTakeAsFallback = bHasProcessedMeasurement && szFallback == null && !string.IsNullOrEmpty(fai.OriginImageFileName);
                if (bTakeAsFallback)
                {
                    szFallback = fai.OriginImageFileName;
                }
            }
            return szFallback;
        }

        // 현재 레시피(재검사가 적용될 레시피)에서 이 시퀀스 소유 Shot 의 크로스-Z 두 장짜리 측정마다
        //  ZIndexA/B tick 에서 실제 촬영된 역할별(가로/세로) 원본 사진 경로를 찾는다.
        private static void FillPartDualPhotos(SavedCycleRerunPart part, InspectionSequence seq, InspectionRecipeManager recipeManager)
        {
            foreach (var shot in recipeManager.Shots)
            {
                if (!InspectionSequence.IsShotOwnedBySequence(shot, seq.Name))
                {
                    continue;
                }
                foreach (var fai in shot.FAIList)
                {
                    foreach (var meas in fai.Measurements)
                    {
                        var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                        if (dualMeas == null)
                        {
                            continue;
                        }
                        bool bCrossZConfigured = dualMeas.ZIndexA != DUAL_CROSS_Z_UNCONFIGURED && dualMeas.ZIndexB != DUAL_CROSS_Z_UNCONFIGURED;
                        if (!bCrossZConfigured)
                        {
                            continue; // ZIndexA/B 미설정(정적) 측정은 교체 대상 아님
                        }
                        string szMeasKey;
                        if (string.IsNullOrEmpty(dualMeas.MeasurementName))
                        {
                            szMeasKey = dualMeas.TypeName;
                        }
                        else
                        {
                            szMeasKey = dualMeas.MeasurementName;
                        }
                        string szHorizontal = ResolveDualRolePath(part, shot.ShotName, fai.FAIName, szMeasKey, dualMeas.ZIndexA);
                        string szVertical = ResolveDualRolePath(part, shot.ShotName, fai.FAIName, szMeasKey, dualMeas.ZIndexB);
                        part.DualPhotos.Add(new SavedCycleDualPhoto
                        {
                            ShotName = shot.ShotName,
                            FAIName = fai.FAIName,
                            MeasurementName = szMeasKey,
                            HorizontalPath = szHorizontal,
                            VerticalPath = szVertical
                        });
                    }
                }
            }
        }

        private static string ResolveDualRolePath(SavedCycleRerunPart part, string szShotName, string szFaiName, string szMeasKey, int nRoleZIndex)
        {
            foreach (var dto in part.Ticks)
            {
                if (dto.ZIndex != nRoleZIndex)
                {
                    continue;
                }
                FaiResultDto faiDto = FindFai(dto, szShotName, szFaiName);
                if (faiDto == null || faiDto.Measurements == null)
                {
                    continue;
                }
                MeasurementResultDto measDto = FindMeasurement(faiDto, szMeasKey);
                if (measDto == null)
                {
                    continue;
                }
                if (IsMeasurementProcessedInTick(measDto))
                {
                    return faiDto.OriginImageFileName;
                }
            }
            return null;
        }

        private static FaiResultDto FindFai(CycleResultDto dto, string szShotName, string szFaiName)
        {
            if (dto.Shots == null)
            {
                return null;
            }
            foreach (var shot in dto.Shots)
            {
                if (!string.Equals(shot.ShotName, szShotName, StringComparison.Ordinal))
                {
                    continue;
                }
                if (shot.FAIs == null)
                {
                    return null;
                }
                foreach (var fai in shot.FAIs)
                {
                    if (string.Equals(fai.FAIName, szFaiName, StringComparison.Ordinal))
                    {
                        return fai;
                    }
                }
                return null;
            }
            return null;
        }

        private static MeasurementResultDto FindMeasurement(FaiResultDto faiDto, string szMeasKey)
        {
            foreach (var meas in faiDto.Measurements)
            {
                string szKey;
                if (string.IsNullOrEmpty(meas.MeasurementName))
                {
                    szKey = meas.TypeName;
                }
                else
                {
                    szKey = meas.MeasurementName;
                }
                if (string.Equals(szKey, szMeasKey, StringComparison.Ordinal))
                {
                    return meas;
                }
            }
            return null;
        }

        // 이 시퀀스의 기준점(Datum) 마다 재검사에 필요한 사진 역할 키를 계산한다. 크로스-Z 두 장짜리(가로/세로
        //  둘 다 설정)면 H/V 둘 다, 크로스-Z 정적(ZIndexA/B 미설정)이면 요구 없음(현재 티칭 경로 유지 — 재량
        //  결정, SUMMARY 기록), 그 외에는 단일(ROLE_SINGLE) 사진 1장.
        private static List<string> ComputeRequiredDatumRoleKeys(InspectionSequence seq)
        {
            List<string> lstRequired = new List<string>();
            if (seq.DatumConfigs == null)
            {
                return lstRequired;
            }
            foreach (var datum in seq.DatumConfigs)
            {
                bool bDualImageDatum = datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage;
                if (bDualImageDatum)
                {
                    bool bCrossZConfigured = datum.ZIndexA != DUAL_CROSS_Z_UNCONFIGURED && datum.ZIndexB != DUAL_CROSS_Z_UNCONFIGURED;
                    if (bCrossZConfigured)
                    {
                        lstRequired.Add(BuildDatumRoleKey(datum.DatumName, DatumImageRecordDto.ROLE_HORIZONTAL));
                        lstRequired.Add(BuildDatumRoleKey(datum.DatumName, DatumImageRecordDto.ROLE_VERTICAL));
                    }
                }
                else
                {
                    lstRequired.Add(BuildDatumRoleKey(datum.DatumName, DatumImageRecordDto.ROLE_SINGLE));
                }
            }
            return lstRequired;
        }

        // 부품 제외 규칙 — 사유 1개로 집계(먼저 걸리는 것 우선). 통과 시 null.
        private static string ValidatePart(SavedCycleRerunPart part, HashSet<string> setExpectedShots, List<string> lstRequiredDatumKeys)
        {
            if (part.DatumPhotoPaths.Count == 0)
            {
                return REASON_NO_DATUM_PHOTO;
            }
            foreach (string szKey in lstRequiredDatumKeys)
            {
                if (!part.DatumPhotoPaths.ContainsKey(szKey))
                {
                    return REASON_DATUM_ROLE_MISSING;
                }
            }
            if (part.ShotPhotoPaths.Count == 0)
            {
                return REASON_NO_SHOT;
            }
            foreach (string szShotName in setExpectedShots)
            {
                if (!part.ShotPhotoPaths.ContainsKey(szShotName))
                {
                    return REASON_SHOT_MISSING;
                }
            }
            foreach (var dual in part.DualPhotos)
            {
                bool bDualMissing = string.IsNullOrEmpty(dual.HorizontalPath) || string.IsNullOrEmpty(dual.VerticalPath);
                if (bDualMissing)
                {
                    return REASON_DUAL_MISSING;
                }
            }
            if (!AllAdoptedPathsExist(part))
            {
                return REASON_FILE_MISSING;
            }
            return null;
        }

        private static bool AllAdoptedPathsExist(SavedCycleRerunPart part)
        {
            foreach (var pair in part.DatumPhotoPaths)
            {
                if (!File.Exists(pair.Value))
                {
                    return false;
                }
            }
            foreach (var pair in part.ShotPhotoPaths)
            {
                if (!File.Exists(pair.Value))
                {
                    return false;
                }
            }
            foreach (var dual in part.DualPhotos)
            {
                if (!string.IsNullOrEmpty(dual.HorizontalPath) && !File.Exists(dual.HorizontalPath))
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(dual.VerticalPath) && !File.Exists(dual.VerticalPath))
                {
                    return false;
                }
            }
            return true;
        }

        private static void RecordExclusion(SavedCycleRerunPlan plan, string szReason, string szDetail)
        {
            plan.ExcludedPartCount++;
            int nCurrent;
            if (plan.ExclusionCounts.TryGetValue(szReason, out nCurrent))
            {
                plan.ExclusionCounts[szReason] = nCurrent + 1;
            }
            else
            {
                plan.ExclusionCounts[szReason] = 1;
            }
            plan.ExclusionDetails.Add(szReason + " — " + szDetail);
        }
    }
}
