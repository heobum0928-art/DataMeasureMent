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

        //260615 hbk Quick 260615-dx7 이미지 폴더 순회 모드. null = 기존 고정 이미지 반복 모드.
        private List<string> _imagePaths;

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
    }
}
