//260409 hbk Phase 5: 동적 FAI 검사 시퀀스 (D-07)
//260413 hbk Phase 6: Fixture 역할 확장 — DisplayName + Multi-Datum (D-01, D-04, D-09, D-10)
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

        public InspectionSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;
            Context = new InspectionSequenceContext(this);
            pMyContext = Context as InspectionSequenceContext;
            //260417 hbk Phase 6-04 UAT: CameraMasterParam → InspectionMasterParam 교체 (DisplayName 편집 UI 노출, D-01)
            Param = new InspectionMasterParam(this);
            pMyParam = Param as CameraMasterParam;
            DefaultCamera = defaultCamera;
            DefaultLight = defaultLight;
        }

        //260409 hbk Phase 5: 종합 판정 + FAI별 결과 TCP 전송 (D-07)
        protected override void AddResponse() {
            if (RequestPacket == null) return;

            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;

            // 종합 판정: 모든 FAI가 Pass여야 OK
            bool allPass = true;
            var responsePacket = new TestResultPacket {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                IsDynamicFAI = true,
            };

            foreach (var shot in recipeManager.Shots) {
                foreach (var fai in shot.FAIList) {
                    if (!fai.IsPass) allPass = false;
                    responsePacket.FAIResults.Add(new FAIResultData(
                        fai.FAIName ?? "FAI",
                        fai.IsPass,
                        fai.MeasuredValue
                    ));
                }
            }

            responsePacket.Result = allPass ? EVisionResultType.OK : EVisionResultType.NG;
            pMyContext.ResultInfo = responsePacket.Result;

            ResponseQueue.Enqueue(responsePacket);
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
