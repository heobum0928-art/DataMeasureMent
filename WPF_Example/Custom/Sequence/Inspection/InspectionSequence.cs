//260409 hbk Phase 5: 동적 FAI 검사 시퀀스 (D-07)
//260413 hbk Phase 6: Fixture 역할 확장 — DisplayName + Multi-Datum (D-01, D-04, D-09, D-10)
using System;
using System.Collections.Generic;
using System.Windows;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Network;
using ReringProject.Setting; //260528 hbk Phase 37 — ELogType (datum find 실패 skip 로그)
using ReringProject.UI;
using ReringProject.Utility; //260528 hbk Phase 37 — Logging.PrintLog (datum find 실패 skip 로그)

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

        //260413 hbk Phase 6: Fixture DisplayName — 사용자 편집 가능 (D-01)
        public string DisplayName { get; set; } = "";

        //260413 hbk Phase 6: Multi-Datum — Fixture 레벨 Datum 소유 (D-04)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

        //260413 hbk Phase 6: 런타임 transform 캐시 (D-09)
        private readonly Dictionary<string, HTuple> _datumTransforms = new Dictionary<string, HTuple>();

        //260529 hbk Phase 39 WF-01 D-01 — datum 검출 실패 datum 이름 집합 (per-FAI gate 신호). _datumTransforms 와 동일 lifecycle.
        private readonly HashSet<string> _failedDatums = new HashSet<string>(); //260529 hbk Phase 39 WF-01 D-01

        public InspectionSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;
            Context = new InspectionSequenceContext(this);
            pMyContext = Context as InspectionSequenceContext;
            //260417 hbk Phase 6-04 UAT: CameraMasterParam → InspectionMasterParam 교체 (DisplayName 편집 UI 노출, D-01)
            Param = new InspectionMasterParam(this);
            pMyParam = Param as CameraMasterParam;
            DefaultCamera = defaultCamera;
            DefaultLight = defaultLight;
            //260601 hbk Phase 40 CO-40-01 — 수동(UI) 검사 완료 시에도 cycle.json 영속화.
            //  AddResponse() 는 RequestPacket(TCP/host) 경로에서만 호출 → Start(EAction) 수동 검사(RequestPacket=null)는 미저장이었음.
            //  Finish() 의 OnFinish 는 트리거 무관 발화 → 핸들러에서 RequestPacket==null(수동) 일 때만 저장(TCP 경로 중복 방지).
            OnFinish += HandleManualCyclePersist;
        }

        //260409 hbk Phase 5: 종합 판정 + FAI별 결과 TCP 전송 (D-07)
        //260529 hbk Phase 39 WF-02 D-03/D-05/D-06 — 3-state cycle hierarchy + FAIResults P/F/N 분기.
        //  계층: 검출실패(NotExist 'N') > NG ('X') > OK ('O'). datum-skip 1건이라도 있으면 cycle = NotExist.
        //  D-08: TestResultPacket 신규 필드 추가 안 함. FAIResults[i] 표현 레벨에서 끝남. wire 호환 100%.
        //  D-10: EVisionResultType.NotExist (기존 v2.6 enum) 재사용. v2.7 ECycleResult 도입 안 함.
        protected override void AddResponse() {
            if (RequestPacket == null) return;

            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;

            // 종합 판정: 3-state hierarchy (D-03)
            bool anyDatumSkip = false; //260529 hbk Phase 39 WF-02 D-03 — fai.WasDatumSkipped 1건이라도 있으면 true → cycle=NotExist
            bool allPass = true;
            var responsePacket = new TestResultPacket {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                IsDynamicFAI = true,
            };

            foreach (var shot in recipeManager.Shots) {
                foreach (var fai in shot.FAIList) {
                    //260529 hbk Phase 39 WF-02 D-06 — FAIResults[i] 3-state (P/F/N) 분기.
                    //  Plan 01: Action_FAIMeasurement 가 datum-skip 시 fai.WasDatumSkipped=true + IsPass=false 동시 설정.
                    //  계층 우선순위: WasDatumSkipped → NotExist, !IsPass → NG, 그 외 → OK.
                    EVisionResultType faiResultCode; //260529 hbk Phase 39 WF-02 D-06
                    if (fai.WasDatumSkipped) //260529 hbk Phase 39 WF-02 D-06
                    {
                        faiResultCode = EVisionResultType.NotExist; //260529 hbk Phase 39 WF-02 D-06 — 'N'
                        anyDatumSkip = true; //260529 hbk Phase 39 WF-02 D-03 — cycle aggregation
                    }
                    else if (!fai.IsPass) //260529 hbk Phase 39 WF-02 D-06
                    {
                        faiResultCode = EVisionResultType.NG; //260529 hbk Phase 39 WF-02 D-06 — 'F'
                        allPass = false; //260529 hbk Phase 39 WF-02 D-06 — 기존 동작 보존
                    }
                    else //260529 hbk Phase 39 WF-02 D-06
                    {
                        faiResultCode = EVisionResultType.OK; //260529 hbk Phase 39 WF-02 D-06 — 'P'
                    }
                    responsePacket.FAIResults.Add(new FAIResultData( //260529 hbk Phase 39 WF-02 D-06 — Plan 02 Task 1 신규 ctor 호출
                        fai.FAIName ?? "FAI", //260529 hbk Phase 39 WF-02 D-06
                        faiResultCode, //260529 hbk Phase 39 WF-02 D-06
                        fai.MeasuredValue //260529 hbk Phase 39 WF-02 D-06
                    )); //260529 hbk Phase 39 WF-02 D-06
                }
            }

            //260529 hbk Phase 39 WF-02 D-03/D-05 — cycle 계층 판정: 검출실패 > NG > OK.
            if (anyDatumSkip) responsePacket.Result = EVisionResultType.NotExist; //260529 hbk Phase 39 WF-02 D-03/D-05 — 'N' (TestResultPacket.GetResultString 가 자동 매핑)
            else if (!allPass) responsePacket.Result = EVisionResultType.NG; //260529 hbk Phase 39 WF-02 D-03 — 'X'
            else responsePacket.Result = EVisionResultType.OK; //260529 hbk Phase 39 WF-02 D-03 — 'O'
            pMyContext.ResultInfo = responsePacket.Result;

            //260601 hbk Phase 40 OUT-01/OUT-02 — cycle 완료 → 구조화 JSON 영속화 (리뷰어/xlsx 공통 토대, D-01/D-02/D-03)
            //  BuildDto 는 동기 스냅샷 (Shots 가 이미 채워진 시점), SaveAsync 가 비동기 파일 쓰기 — AddResponse 스레드 블로킹 최소화.
            //  직렬화 예외가 TCP 응답(ResponseQueue.Enqueue)을 차단하지 않도록 try/catch 격리 (RESEARCH Pitfall 4, T-40-03).
            try
            {
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    responsePacket.Result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName);
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }

            ResponseQueue.Enqueue(responsePacket);
        }

        //260601 hbk Phase 40 CO-40-01 — 수동(UI) 검사 완료 핸들러 (OnFinish 구독, 생성자에서 1회 등록).
        //  TCP 경로(RequestPacket!=null)는 AddResponse 에서 이미 저장하므로 여기선 수동 경로만 처리 → 중복 저장 방지.
        //  종합판정은 ComputeOverallResult (AddResponse 의 anyDatumSkip>NG>OK 계층과 동일) 로 재집계 후 BuildDto/SaveAsync.
        private void HandleManualCyclePersist(SequenceContext context) {
            if (RequestPacket != null) return; //260601 hbk Phase 40 CO-40-01 — TCP 경로는 AddResponse 에서 저장됨
            try
            {
                var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
                EVisionResultType result = ComputeOverallResult(recipeManager);
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName);
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] 수동 cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }
        }

        //260601 hbk Phase 40 CO-40-01 — 종합판정 집계 (read-only, 패킷 미생성). AddResponse 의 3-state 계층과 동일 우선순위.
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
            if (anyDatumSkip) return EVisionResultType.NotExist; //260601 hbk Phase 40 CO-40-01 — 검출실패 최우선
            if (!allPass) return EVisionResultType.NG; //260601 hbk Phase 40 CO-40-01 — NG
            return EVisionResultType.OK; //260601 hbk Phase 40 CO-40-01 — OK
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

        //260413 hbk Phase 6: Fixture API — DisplayName 폴백 (D-01)
        public string GetDisplayName() {
            return string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
        }

        //260413 hbk Phase 6: Multi-Datum add (D-04)
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

        //260413 hbk Phase 6: Datum phase 실행 — 모든 DatumConfig 순회 (D-09)
        public bool TryRunDatumPhase(HImage image, out string error) {
            error = null;
            _datumTransforms.Clear();

            if (DatumConfigs.Count == 0) {
                return true; // D-10: Datum 미설정 Fixture는 무보정 pass-through
            }

            if (image == null) {
                error = "image is null";
                return false;
            }

            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform;
                string datumError;
                if (!service.TryFindDatum(image, datum, out transform, out datumError)) { //260528 hbk Phase 37 D-37-03 — datum 실패 시 abort 안 함, 성공만 저장
                    datum.LastFindSucceeded = false; //260528 hbk Phase 37 D-37-03
                    Logging.PrintLog((int)ELogType.Error, "[DatumPhase] Datum '" + (datum.DatumName ?? "") + "' find 실패 (skip): " + (datumError ?? "")); //260528 hbk Phase 37 D-37-03
                    continue; //260528 hbk Phase 37 D-37-03 — 다음 datum 계속, 이 datum 은 _datumTransforms 미저장
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                _datumTransforms[datum.DatumName ?? ""] = transform;
            }
            return true;
        }
        //260527 hbk Phase 34 D-34-14 정정 — VerticalTwoHorizontalDualImage 전용 2-image 오버로드. image1=가로축, image2=세로축. _datumTransforms 채움 규약은 1-image 오버로드와 동일 (T-34-03-08 해소).
        public bool TryRunDatumPhase(HImage image1, HImage image2, out string error) {
            error = null;
            _datumTransforms.Clear();
            if (DatumConfigs.Count == 0) return true;
            if (image1 == null || image2 == null) { error = "image1 or image2 is null"; return false; }
            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform; string datumError; bool ok; //260528 hbk Phase 37 D-37-04 — per-datum DualImage 판단 유지 (DatumConfigs[0] 단일 판단 아님)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) ok = service.TryFindDatum(image1, image2, datum, out transform, out datumError); //260528 hbk Phase 37 D-37-04
                else ok = service.TryFindDatum(image1, datum, out transform, out datumError); //260528 hbk Phase 37 D-37-04
                if (!ok) { datum.LastFindSucceeded = false; Logging.PrintLog((int)ELogType.Error, "[DatumPhase] Datum '" + (datum.DatumName ?? "") + "' find 실패 (skip): " + (datumError ?? "")); continue; } //260528 hbk Phase 37 D-37-03 — abort 제거, skip
                datum.LastFindSucceeded = true; datum.CurrentTransform = transform; _datumTransforms[datum.DatumName ?? ""] = transform; //260528 hbk Phase 37 D-37-03
            }
            return true;
        }

        //260528 hbk Phase 37 D-37-05 — DatumPhase loop 시작 전 1회 호출 (per-datum 누적 전 초기화)
        public void ClearDatumTransforms() {
            _datumTransforms.Clear();
            _failedDatums.Clear(); //260529 hbk Phase 39 WF-01 D-01 — _datumTransforms 와 동일 lifecycle
            //260529 hbk Phase 39 hotfix CO-39-02 — RuntimeDetectFailed 도 일괄 리셋 (이전 사이클 잔여 신호 제거)
            foreach (var d in DatumConfigs) //260529 hbk Phase 39 hotfix CO-39-02
            {
                if (d != null) d.RuntimeDetectFailed = false; //260529 hbk Phase 39 hotfix CO-39-02
            }
        }

        //260529 hbk Phase 39 WF-01 D-01 — 검출 실패 datum 기록. Action_FAIMeasurement.EStep.DatumPhase 실패 분기에서 호출.
        //  null/empty 가드: PATTERNS.md 옵션 A 분석. _failedDatums 단일 set 에 idempotent add.
        public void MarkDatumFailed(string datumName) //260529 hbk Phase 39 WF-01 D-01
        {
            if (!string.IsNullOrEmpty(datumName)) _failedDatums.Add(datumName); //260529 hbk Phase 39 WF-01 D-01
        }

        //260529 hbk Phase 39 WF-01 D-01 — per-FAI gate 조회. Measurement.DatumRef 가 실패 datum 이름과 일치하면 true.
        //  빈 DatumRef = 무보정 의도이므로 게이트 무관 (false 반환 — TryGetDatumTransform identity fallback 경로로 진행).
        public bool IsDatumFailed(string datumRef) //260529 hbk Phase 39 WF-01 D-01
        {
            return !string.IsNullOrEmpty(datumRef) && _failedDatums.Contains(datumRef); //260529 hbk Phase 39 WF-01 D-01
        }

        //260528 hbk Phase 37 D-37-05 — datum 1개 검출 후 _datumTransforms 누적 저장 (Clear 안 함). 실패 시 false 반환하되 호출부는 abort 안 함 (lenient).
        public bool TryRunSingleDatum(DatumConfig datum, HImage imageH, HImage imageV, out string error) {
            error = null;
            if (datum == null) { error = "datum is null"; return false; } //260528 hbk Phase 37 D-37-05
            var service = new DatumFindingService(); //260528 hbk Phase 37 D-37-05
            HTuple transform; //260528 hbk Phase 37 D-37-05
            string datumError; //260528 hbk Phase 37 D-37-05
            bool ok; //260528 hbk Phase 37 D-37-05
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260528 hbk Phase 37 D-37-05 — datum별 DualImage 판단 (mixed 허용)
                if (imageH == null || imageV == null) { error = "DualImage 이미지 null"; datum.LastFindSucceeded = false; return false; } //260528 hbk Phase 37 D-37-05
                ok = service.TryFindDatum(imageH, imageV, datum, out transform, out datumError); //260528 hbk Phase 37 D-37-05
            } else { //260528 hbk Phase 37 D-37-05 — 1-image datum
                if (imageH == null) { error = "이미지 null"; datum.LastFindSucceeded = false; return false; } //260528 hbk Phase 37 D-37-05
                ok = service.TryFindDatum(imageH, datum, out transform, out datumError); //260528 hbk Phase 37 D-37-05
            }
            if (!ok) { datum.LastFindSucceeded = false; error = datumError; return false; } //260528 hbk Phase 37 D-37-05
            datum.LastFindSucceeded = true; //260528 hbk Phase 37 D-37-05
            datum.CurrentTransform = transform; //260528 hbk Phase 37 D-37-05
            _datumTransforms[datum.DatumName ?? ""] = transform; //260528 hbk Phase 37 D-37-05 — 누적 저장
            return true; //260528 hbk Phase 37 D-37-05
        }

        //260413 hbk Phase 6: DatumRef → transform 조회 (D-10)
        public bool TryGetDatumTransform(string datumRef, out HTuple transform) {
            if (string.IsNullOrEmpty(datumRef)) {
                HOperatorSet.HomMat2dIdentity(out transform);
                return true;
            }
            return _datumTransforms.TryGetValue(datumRef, out transform);
        }
    }
}
