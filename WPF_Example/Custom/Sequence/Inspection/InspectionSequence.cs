using System;
using System.Collections.Generic;
using System.Windows;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class InspectionSequenceContext : SequenceContext {
        public EVisionResultType ResultInfo { get; set; }

        public InspectionSequenceContext(InspectionSequence source) : base(source) { }

        public override void Clear() {
            ResultInfo = EVisionResultType.NG;
            base.Clear();
        }

        public override void CopyFrom(ActionContext actionContext) {
            base.CopyFrom(actionContext);
            Result = actionContext.Result;
            if (actionContext is FAIMeasurementContext faiContext) {
                // 각 Action의 AllPass가 false면 NG
                if (!faiContext.AllPass) {
                    ResultInfo = EVisionResultType.NG;
                }
            }
        }
    }

    public class InspectionSequence : SequenceBase {
        private readonly DeviceHandler pDevs;
        private readonly InspectionSequenceContext pMyContext;
        private readonly CameraMasterParam pMyParam;
        private readonly string DefaultCamera;
        private readonly string DefaultLight;

        // Fixture DisplayName — 사용자 편집 가능
        public string DisplayName { get; set; } = "";

        // Multi-Datum — Fixture 레벨 Datum 소유
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

        // 런타임 transform 캐시
        private readonly Dictionary<string, HTuple> _datumTransforms = new Dictionary<string, HTuple>();

        // datum 검출 실패 datum 이름 집합 (per-FAI gate 신호). _datumTransforms 와 동일 lifecycle.
        private readonly HashSet<string> _failedDatums = new HashSet<string>();

        public InspectionSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;
            Context = new InspectionSequenceContext(this);
            pMyContext = Context as InspectionSequenceContext;
            Param = new InspectionMasterParam(this);
            pMyParam = Param as CameraMasterParam;
            DefaultCamera = defaultCamera;
            DefaultLight = defaultLight;
            // 수동(UI) 검사 완료 시에도 cycle.json 영속화.
            //  AddResponse() 는 RequestPacket(TCP/host) 경로에서만 호출 → Start(EAction) 수동 검사(RequestPacket=null)는 미저장이었음.
            //  OnFinish 는 트리거 무관 발화 → 핸들러에서 RequestPacket==null(수동) 일 때만 저장(TCP 경로 중복 방지).
            OnFinish += HandleManualCyclePersist;
            // 런 시작 시 이 시퀀스 shot 의 모든 측정 결과 초기화 (안 돈 측정 stale 방지).
            //  OnStart 는 단일 Start(int) + StartAll 모두에서 발화 → 단일/전체 모두 런 시작에 일괄 초기화.
            OnStart += HandleRunStartResetResults;
        }

        // 종합 판정 + FAI별 결과 TCP 전송. 3-state cycle hierarchy + FAIResults P/F/N 분기.
        //  계층: 검출실패(NotExist 'N') > NG ('X') > OK ('O'). datum-skip 1건이라도 있으면 cycle = NotExist.
        //  TestResultPacket 신규 필드 추가 안 함 — FAIResults[i] 표현 레벨에서 끝남, wire 호환 100%.
        //  EVisionResultType.NotExist (기존 v2.6 enum) 재사용.
        protected override void AddResponse() {
            if (RequestPacket == null) return;

            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;

            // 종합 판정: 3-state hierarchy
            bool anyDatumSkip = false; // fai.WasDatumSkipped 1건이라도 있으면 true → cycle=NotExist
            bool allPass = true;
            var responsePacket = new TestResultPacket {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                IsDynamicFAI = true,
            };

            foreach (var shot in recipeManager.Shots) {
                foreach (var fai in shot.FAIList) {
                    // FAIResults[i] 3-state (P/F/N) 분기. datum-skip 시 fai.WasDatumSkipped=true + IsPass=false 동시 설정.
                    //  계층 우선순위: WasDatumSkipped → NotExist, !IsPass → NG, 그 외 → OK.
                    EVisionResultType faiResultCode;
                    if (fai.WasDatumSkipped)
                    {
                        faiResultCode = EVisionResultType.NotExist; // 'N'
                        anyDatumSkip = true; // cycle aggregation
                    }
                    else if (!fai.IsPass)
                    {
                        faiResultCode = EVisionResultType.NG; // 'F'
                        allPass = false;
                    }
                    else
                    {
                        faiResultCode = EVisionResultType.OK; // 'P'
                    }
                    string faiNameArg = fai.FAIName;
                    if (faiNameArg == null) faiNameArg = "FAI";
                    responsePacket.FAIResults.Add(new FAIResultData(
                        faiNameArg,
                        faiResultCode,
                        fai.MeasuredValue
                    ));
                }
            }

            // cycle 계층 판정: 검출실패 > NG > OK.
            if (anyDatumSkip) responsePacket.Result = EVisionResultType.NotExist; // 'N' (TestResultPacket.GetResultString 가 자동 매핑)
            else if (!allPass) responsePacket.Result = EVisionResultType.NG; // 'X'
            else responsePacket.Result = EVisionResultType.OK; // 'O'
            pMyContext.ResultInfo = responsePacket.Result;

            // cycle 완료 → 구조화 JSON 영속화 (리뷰어/xlsx 공통 토대).
            //  BuildDto 는 동기 스냅샷, SaveAsync 가 비동기 파일 쓰기 — AddResponse 스레드 블로킹 최소화.
            //  직렬화 예외가 TCP 응답(ResponseQueue.Enqueue)을 차단하지 않도록 try/catch 격리.
            try
            {
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    responsePacket.Result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName,
                    Name); // 이 시퀀스 소유 shot 만 cycle 에 포함
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }

            ResponseQueue.Enqueue(responsePacket);
        }

        // 수동(UI) 검사 완료 핸들러 (OnFinish 구독, 생성자에서 1회 등록).
        //  TCP 경로(RequestPacket!=null)는 AddResponse 에서 이미 저장하므로 여기선 수동 경로만 처리 → 중복 저장 방지.
        //  종합판정은 ComputeOverallResult (AddResponse 의 anyDatumSkip>NG>OK 계층과 동일) 로 재집계 후 BuildDto/SaveAsync.
        private void HandleManualCyclePersist(SequenceContext context) {
            if (RequestPacket != null) return; // TCP 경로는 AddResponse 에서 저장됨
            try
            {
                var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
                EVisionResultType result = ComputeOverallResult(recipeManager);
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName,
                    Name); // 이 시퀀스 소유 shot 만 cycle 에 포함
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] 수동 cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }
        }

        // 런 시작 시 이 시퀀스 소유 shot 의 모든 측정 결과를 초기화한다.
        //  배경: 기존 per-shot EStep.Init 의 ShotParam.ClearAllResults() 는 '실행되는 shot' 만 초기화 + FAIConfig.ClearResult() 가
        //        하위 Measurement 의 LastHasResult 를 안 지운다 → 단일 shot 실행 시 나머지 shot 의 측정이 이전 런 잔여값(stale)을 유지,
        //        cycle.json/트리/리뷰어에 '안 돈 측정'이 OK/NG 로 찍히던 문제.
        //  조치: 런 시작에 이 시퀀스의 Actions(=이 시퀀스 shot)의 모든 Measurement 를 ClearResult → 안 돈 측정은 미측정('—')으로 표시.
        //  범위: 이 시퀀스 shot 만 (Actions 순회) → Top/Bottom/Side 병렬 실행 간섭 없음. 실행되는 shot 은 Measure 루프가 다시 채운다.
        private void HandleRunStartResetResults(SequenceContext context) {
            try {
                if (Actions == null) return;
                foreach (var act in Actions) {
                    var faiAct = act as Action_FAIMeasurement;
                    if (faiAct == null) continue;
                    var shot = faiAct.ShotParam;
                    if (shot == null) continue;
                    foreach (var fai in shot.FAIList) {
                        if (fai == null) continue;
                        if (fai.Measurements != null) {
                            foreach (var m in fai.Measurements) {
                                if (m != null) m.ClearResult(); // LastHasResult=false → 리뷰어 '—'
                            }
                        }
                        fai.ClearResult();
                        if (fai.LastOverlays != null) fai.LastOverlays.Clear();
                    }
                }
            } catch (Exception ex) {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] run-start 결과 초기화 실패(무시): " + ex.Message); } catch { }
            }
        }

        // 종합판정 집계 (read-only, 패킷 미생성). AddResponse 의 3-state 계층과 동일 우선순위.
        private EVisionResultType ComputeOverallResult(InspectionRecipeManager recipeManager) {
            bool anyDatumSkip = false;
            bool allPass = true;
            if (recipeManager != null)
            {
                foreach (var shot in recipeManager.Shots)
                {
                    foreach (var fai in shot.FAIList)
                    {
                        if (fai.WasDatumSkipped) anyDatumSkip = true;
                        else if (!fai.IsPass) allPass = false;
                    }
                }
            }
            if (anyDatumSkip) return EVisionResultType.NotExist; // 검출실패 최우선
            if (!allPass) return EVisionResultType.NG;
            return EVisionResultType.OK;
        }

        public override void OnCreate() {
            pMyParam.LightGroupName = DefaultLight;
            pMyParam.DeviceName = DefaultCamera;

            var camera = pDevs[pMyParam.DeviceName];
            if (camera == null || camera.Properties == null) {
                CustomMessageBox.Show("Error", $"Camera {pMyParam.DeviceName} - Initialize Fail", MessageBoxImage.Error);
                IsInitialized = false;
                Context.State = EContextState.Error;
                return;
            }

            IsInitialized = true;
            base.OnCreate();
        }

        public override void OnLoad() {
            SystemHandler.Handle.Lights.ApplyLight(pMyParam);
            base.OnLoad();
        }

        public override void OnRelease() {
            IsInitialized = false;
            base.OnRelease();
        }

        // Fixture API — DisplayName 폴백
        public string GetDisplayName() {
            return string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
        }

        // Multi-Datum add
        public DatumConfig AddDatum(string name = null) {
            string datumName = string.IsNullOrEmpty(name) ? $"Datum_{DatumConfigs.Count + 1}" : name;
            var datum = new DatumConfig(this);
            datum.DatumName = datumName;
            DatumConfigs.Add(datum);
            return datum;
        }

        public bool RemoveDatum(int index) {
            if (index < 0 || index >= DatumConfigs.Count) return false;
            DatumConfigs.RemoveAt(index);
            return true;
        }

        // Datum phase 실행 — 모든 DatumConfig 순회
        public bool TryRunDatumPhase(HImage image, out string error) {
            error = null;
            _datumTransforms.Clear();

            if (DatumConfigs.Count == 0) {
                return true; // Datum 미설정 Fixture는 무보정 pass-through
            }

            if (image == null) {
                error = "image is null";
                return false;
            }

            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform;
                string datumError;
                if (!service.TryFindDatum(image, datum, out transform, out datumError)) { // datum 실패 시 abort 안 함, 성공만 저장
                    datum.LastFindSucceeded = false;
                    string datumName = datum.DatumName;
                    if (datumName == null) datumName = "";
                    string derr = datumError;
                    if (derr == null) derr = "";
                    Logging.PrintLog((int)ELogType.Error, "[DatumPhase] Datum '" + datumName + "' find 실패 (skip): " + derr);
                    continue; // 다음 datum 계속, 이 datum 은 _datumTransforms 미저장
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                string datumKey = datum.DatumName;
                if (datumKey == null) datumKey = "";
                _datumTransforms[datumKey] = transform;
            }
            return true;
        }
        // VerticalTwoHorizontalDualImage 전용 2-image 오버로드. image1=가로축, image2=세로축.
        //  _datumTransforms 채움 규약은 1-image 오버로드와 동일.
        public bool TryRunDatumPhase(HImage image1, HImage image2, out string error) {
            error = null;
            _datumTransforms.Clear();
            if (DatumConfigs.Count == 0) return true;
            if (image1 == null || image2 == null) { error = "image1 or image2 is null"; return false; }
            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform; string datumError; bool ok; // per-datum DualImage 판단 유지 (DatumConfigs[0] 단일 판단 아님)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) ok = service.TryFindDatum(image1, image2, datum, out transform, out datumError);
                else ok = service.TryFindDatum(image1, datum, out transform, out datumError);
                if (!ok) {
                    datum.LastFindSucceeded = false;
                    string datumName = datum.DatumName;
                    if (datumName == null) datumName = "";
                    string derr = datumError;
                    if (derr == null) derr = "";
                    Logging.PrintLog((int)ELogType.Error, "[DatumPhase] Datum '" + datumName + "' find 실패 (skip): " + derr);
                    continue; // abort 제거, skip
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                string datumKey = datum.DatumName;
                if (datumKey == null) datumKey = "";
                _datumTransforms[datumKey] = transform;
            }
            return true;
        }

        // DatumPhase loop 시작 전 1회 호출 (per-datum 누적 전 초기화)
        public void ClearDatumTransforms() {
            _datumTransforms.Clear();
            _failedDatums.Clear(); // _datumTransforms 와 동일 lifecycle
            // RuntimeDetectFailed 도 일괄 리셋 (이전 사이클 잔여 신호 제거)
            foreach (var d in DatumConfigs)
            {
                if (d != null) d.RuntimeDetectFailed = false;
            }
        }

        // 검출 실패 datum 기록. Action_FAIMeasurement.EStep.DatumPhase 실패 분기에서 호출.
        //  _failedDatums 단일 set 에 idempotent add.
        public void MarkDatumFailed(string datumName)
        {
            if (!string.IsNullOrEmpty(datumName)) _failedDatums.Add(datumName);
        }

        // per-FAI gate 조회. Measurement.DatumRef 가 실패 datum 이름과 일치하면 true.
        //  빈 DatumRef = 무보정 의도이므로 게이트 무관 (false 반환 — TryGetDatumTransform identity fallback 경로로 진행).
        public bool IsDatumFailed(string datumRef)
        {
            return !string.IsNullOrEmpty(datumRef) && _failedDatums.Contains(datumRef);
        }

        // datum 1개 검출 후 _datumTransforms 누적 저장 (Clear 안 함). 실패 시 false 반환하되 호출부는 abort 안 함 (lenient).
        public bool TryRunSingleDatum(DatumConfig datum, HImage imageH, HImage imageV, out string error) {
            error = null;
            if (datum == null) { error = "datum is null"; return false; }
            var service = new DatumFindingService();
            HTuple transform;
            string datumError;
            bool ok;
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { // datum별 DualImage 판단 (mixed 허용)
                if (imageH == null || imageV == null) { error = "DualImage 이미지 null"; datum.LastFindSucceeded = false; return false; }
                ok = service.TryFindDatum(imageH, imageV, datum, out transform, out datumError);
            } else { // 1-image datum
                if (imageH == null) { error = "이미지 null"; datum.LastFindSucceeded = false; return false; }
                ok = service.TryFindDatum(imageH, datum, out transform, out datumError);
            }
            if (!ok) { datum.LastFindSucceeded = false; error = datumError; return false; }
            datum.LastFindSucceeded = true;
            datum.CurrentTransform = transform;
            string datumKey = datum.DatumName;
            if (datumKey == null) datumKey = "";
            _datumTransforms[datumKey] = transform; // 누적 저장
            return true;
        }

        // DatumRef → transform 조회
        public bool TryGetDatumTransform(string datumRef, out HTuple transform) {
            if (string.IsNullOrEmpty(datumRef)) {
                HOperatorSet.HomMat2dIdentity(out transform);
                return true;
            }
            return _datumTransforms.TryGetValue(datumRef, out transform);
        }
    }
}
