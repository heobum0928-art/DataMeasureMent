//260612 hbk Phase 41.1 OUT-03 50회 반복 실행 서비스
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
    /// InspectionSequence 를 지정 횟수만큼 자동 반복 실행하고 결과를 누적한다.
    /// UI 레이어가 인스턴스를 소유한다 (static 금지 — 다중 동시 실행 방지 용이).
    /// Start() → OnFinish 구독 → 완료마다 HandleFinish() → TargetCount 도달 시 OnRepeatComplete 발화.
    /// </summary>
    public class RepeatRunService
    {
        public const int DEFAULT_REPEAT_COUNT = 50;

        /// <summary>모든 반복이 완료되면 발화. arg = 누적된 CycleResultDto 전체 목록.</summary>
        public event Action<List<CycleResultDto>> OnRepeatComplete;

        /// <summary>각 반복 완료마다 발화. (현재 완료 횟수, 목표 횟수).</summary>
        public event Action<int, int> OnProgressChanged;

        public bool IsRunning { get; private set; }
        public int CompletedCount { get; private set; }
        public int TargetCount { get; private set; }

        private InspectionSequence _seq;
        private List<CycleResultDto> _collected;
        private EventSequenceStateChanged _onFinishHandler;
        private readonly object _lock = new object();

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

        /// <summary>강제 중단. OnFinish 구독을 해제하고 IsRunning = false.</summary>
        public void Stop()
        {
            if (_seq != null && _onFinishHandler != null)
            {
                _seq.OnFinish -= _onFinishHandler;
            }

            IsRunning = false;
            _onFinishHandler = null;
            _seq = null;
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

                // ComputeOverallResult 는 InspectionSequence private — recipeManager 직접 순회하여 EVisionResultType 산출
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
                // Dispatcher.BeginInvoke(Normal) 로 큐된 OnSequenceFinish 핸들러(이미지 표시)가
                // Background 보다 먼저 실행되어 ResultHalconImage.Dispose() 경합이 해소된다.
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
    }
}
