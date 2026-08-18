//260616 hbk Phase 51 BATCH-01 선택 SHOT 일괄 검사 실행 서비스 (RepeatRunService 파생 패턴, 코드 중복 최소화)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    /// <summary>
    //260616 hbk Phase 51: 트리에서 선택된 SHOT 인덱스 집합을 1사이클 일괄 실행하고 결과를 누적한다.
    /// RepeatRunService 와 동일한 Start → OnFinish → HandleFinish → 누적 패턴.
    /// 차이: N회 반복 대신 선택 SHOT 1사이클. SaveAsync 미호출 (InspectionSequence.HandleManualCyclePersist 위임 — 중복 저장 방지).
    /// 누적/Export 경로는 Phase 41.1 (Gage R&R) 가 재사용 가능하도록 RepeatRunService 패턴 정합 (D-08).
    /// </summary>
    public class BatchRunService
    {
        //260616 hbk Phase 51: 1사이클 완료 시 발화. arg = 누적된 CycleResultDto 목록 (수동 모드 = 1개).
        public event Action<List<CycleResultDto>> OnBatchComplete;

        //260616 hbk Phase 51: 사이클 완료마다 발화. (완료, 목표).
        public event Action<int, int> OnProgressChanged;

        /// <summary>자재번호 미지정 sentinel. CycleResultDto.IndexNumber 기본값과 동일.</summary>
        public const int MATERIAL_NOT_SET = -1;

        public bool IsRunning { get; private set; }
        public int CompletedCount { get; private set; }
        public int TargetCount { get; private set; }

        /// <summary>
        /// 이번 실행에 부여할 자재번호. StartBatch 호출 전에 설정한다.
        /// MATERIAL_NOT_SET(-1) 이면 기존 동작 그대로(미지정) — TCP $TEST 경로와 무관.
        /// </summary>
        public int MaterialIndexNumber { get; set; } = MATERIAL_NOT_SET;

        private InspectionSequence _seq;
        private List<int> _selectedIndices; //260616 hbk Phase 51: 실행 대상 로컬 SHOT 인덱스
        private List<CycleResultDto> _collected;
        private EventSequenceStateChanged _onFinishHandler;
        private readonly object _lock = new object();

        //260616 hbk Phase 51: 선택 SHOT 1사이클 일괄 검사 시작. IsRunning 또는 입력 부재 시 즉시 반환.
        public void StartBatch(InspectionSequence seq, List<int> selectedShotIndices)
        {
            if (IsRunning)
            {
                return;
            }

            if (seq == null || selectedShotIndices == null || selectedShotIndices.Count == 0)
            {
                return;
            }

            IsRunning = true;
            _seq = seq;
            _selectedIndices = selectedShotIndices;
            TargetCount = 1;
            CompletedCount = 0;
            _collected = new List<CycleResultDto>();

            _onFinishHandler = (ctx) => HandleFinish(ctx);
            _seq.OnFinish += _onFinishHandler;

            TriggerNext();
        }

        //260616 hbk Phase 51: 강제 중단. OnFinish 구독 해제 + IsRunning=false.
        public void Stop()
        {
            if (_seq != null && _onFinishHandler != null)
            {
                _seq.OnFinish -= _onFinishHandler;
            }

            IsRunning = false;
            _onFinishHandler = null;
            _seq = null;
            _selectedIndices = null;
        }

        //260616 hbk Phase 51: OnFinish 핸들러 — recipeManager 순회로 종합판정 산출 후 BuildDto 누적.
        //  SaveAsync 미호출 (InspectionSequence.HandleManualCyclePersist 가 packet==null 수동 경로에서 이미 저장 — 중복 방지).
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

                //260805 hbk Phase 70 WR-02: _seq 를 한 번만 읽어 로컬에 고정한다. 기존에는 null 체크와
                //  .Name 접근에서 _seq 를 두 번 읽어, 그 사이 다른 스레드(Stop())가 _seq=null 로 바꾸면
                //  seqName 이 null 로 빠지고 — IsShotOwnedBySequence 계약상 null 은 "전체 매칭" 이라 이번
                //  Phase 70 필터가 그 좁은 창에서 조용히 무력화될 위험이 있었다(TOCTOU). 로컬 1회 읽기로 차단.
                InspectionSequence seqRef = _seq;
                //260805 hbk Phase 70 D-02-2: 이 배치를 실제로 돌린 시퀀스 이름. 아래 종합판정 스코프와
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

                bool anySkip = false;
                bool allPass = true;
                //260805 hbk Phase 70 WR-01: 소유권 필터가 0건을 매칭하면 아래 초기값(anySkip=false/allPass=true)이
                //  그대로 남아 "측정 0건인데 OK" 를 조용히 반환한다 — 이 카운터로 그 빈 스코프를 잡는다.
                int nMatchedFaiCount = 0;
                foreach (var shot in recipeManager.Shots)
                {
                    //260805 hbk Phase 70 D-02-2: 이 시퀀스 소유 shot 만 종합판정에 포함.
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
                    try { Logging.PrintErrLog((int)ELogType.Error, "[Phase70] BatchRunService " + seqName + " 소유 shot/FAI 0건 — 종합판정 스킵 위험, NotExist 로 폴백"); } catch { }
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
                    recipeManager, resultType, DateTime.Now, recipeName, seqName, MaterialIndexNumber);

                //260616 hbk Phase 51: SaveAsync 미호출 — HandleManualCyclePersist 위임 (중복 저장 방지). 누적만.
                _collected.Add(dto);
                CompletedCount++;

                OnProgressChanged?.Invoke(CompletedCount, TargetCount);

                if (CompletedCount >= TargetCount)
                {
                    var finalList = new List<CycleResultDto>(_collected);
                    Stop();
                    OnBatchComplete?.Invoke(finalList);
                }
                else
                {
                    TriggerNext();
                }
            }
        }

        //260616 hbk Phase 51: 시퀀스 Idle 대기 후 StartSubset 호출. Background 우선순위로 이전 OnFinish 핸들러 선행 완료 보장.
        private void TriggerNext()
        {
            if (!IsRunning || _seq == null)
            {
                return;
            }

            if (_seq.State == EContextState.Idle)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        if (!IsRunning || _seq == null || _selectedIndices == null)
                        {
                            return;
                        }

                        if (_seq.State == EContextState.Idle)
                        {
                            _seq.StartSubset(_selectedIndices.ToArray(), null);
                        }
                        else
                        {
                            Task.Delay(50).ContinueWith(_ => TriggerNext());
                        }
                    }));
            }
            else
            {
                Task.Delay(50).ContinueWith(_ => TriggerNext());
            }
        }
    }
}
