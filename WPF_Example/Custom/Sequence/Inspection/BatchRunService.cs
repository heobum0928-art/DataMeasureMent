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

        public bool IsRunning { get; private set; }
        public int CompletedCount { get; private set; }
        public int TargetCount { get; private set; }

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

                bool anySkip = false;
                bool allPass = true;
                foreach (var shot in recipeManager.Shots)
                {
                    foreach (var fai in shot.FAIList)
                    {
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

                EVisionResultType resultType;
                if (anySkip)
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
                string seqName = _seq != null ? _seq.Name : null;
                CycleResultDto dto = CycleResultSerializer.BuildDto(
                    recipeManager, resultType, DateTime.Now, recipeName, seqName);

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
