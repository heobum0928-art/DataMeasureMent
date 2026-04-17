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
using ReringProject.UI;

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
                if (!service.TryFindDatum(image, datum, out transform, out datumError)) {
                    error = $"Datum '{datum.DatumName}' failed: {datumError}";
                    datum.LastFindSucceeded = false;
                    return false;
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                _datumTransforms[datum.DatumName ?? ""] = transform;
            }
            return true;
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
