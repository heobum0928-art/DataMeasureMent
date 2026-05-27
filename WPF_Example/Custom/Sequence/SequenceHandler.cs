using System;
using System.Collections.Generic;
using System.IO;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Utility;

namespace ReringProject.Sequence {
    public sealed partial class SequenceHandler {

        public const string SEQ_TOP = "TOP";
        public const string SEQ_SIDE = "SIDE";
        public const string SEQ_BOTTOM = "BOTTOM";

        public const string ACT_INSPECT = "Inspect";
        public const string ACT_SCAN = "SCAN";

        public const int Top_Alg_Index = 0;
        public const int Side_Alg_Index = 1;
        public const int Bottom_Alg_Index = 2;

        public const int Inspection_Model_Index = 0;

        //260527 hbk Phase 35 — CO-33-06: ESequence ↔ SEQ_* 상수 매핑 단일 source (D-35-02-01)
        public static string ResolveSequenceName(ESequence seqId) {
            switch (seqId) {
                case ESequence.Top: return SEQ_TOP;
                case ESequence.Side: return SEQ_SIDE;
                case ESequence.Bottom: return SEQ_BOTTOM;
                default: return SEQ_TOP; // D-B1 폴백 — 예상치 못한 enum 값도 안전한 동작
            }
        }

        public InspectionRecipeManager RecipeManager { get; } = new InspectionRecipeManager(Handle);

        public bool IsDynamicFAIMode { get; private set; } = false;

        private void RegisterSequences() {
            //260409 hbk Phase 5: 동적 FAI 모드용 InspectionSequence (D-07)
            //260526 hbk Phase 33 — Side/Bottom 도 InspectionSequence 마이그레이션 (D-01)
            SequenceBuilder.RegisterSequence(
                new InspectionSequence(ESequence.Top, SEQ_TOP, Top_Alg_Index, DeviceHandler.CAMERA_TOP, LightHandler.LIGHT_TOP),
                new InspectionSequence(ESequence.Side, SEQ_SIDE, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_SIDE),
                new InspectionSequence(ESequence.Bottom, SEQ_BOTTOM, Bottom_Alg_Index, DeviceHandler.CAMERA_BOTTOM, LightHandler.LIGHT_BOTTOM)
            );
        }

        private void RegisterActions() {
            //260526 hbk Phase 33 — Side/Bottom placeholder; RebuildInspectionActions(Side|Bottom) 가 동적 FAI 모드 진입 시 Action_FAIMeasurement 로 교체
            SequenceBuilder.RegisterAction(
                new TopInspectionAction(EAction.Top_Inspection, ACT_INSPECT, Top_Alg_Index, Inspection_Model_Index),
                new TopInspectionAction(EAction.Side_Inspection, ACT_INSPECT, Side_Alg_Index, Inspection_Model_Index),
                new BottomInspectionAction(EAction.Bottom_Inspection, ACT_INSPECT, Bottom_Alg_Index, Inspection_Model_Index)
            );
        }

        private void InitializeSequences() {
            SequenceBuilder seq;

            seq = SequenceBuilder.CreateSequence(ESequence.Top);
            seq.AddAction(EAction.Top_Inspection);
            RegisterSequence(seq);

            seq = SequenceBuilder.CreateSequence(ESequence.Side);
            seq.AddAction(EAction.Side_Inspection);
            RegisterSequence(seq);

            seq = SequenceBuilder.CreateSequence(ESequence.Bottom);
            seq.AddAction(EAction.Bottom_Inspection);
            RegisterSequence(seq);
        }

        /// <summary>
        /// RecipeManager의 Shot 목록 기반으로 시퀀스의 Action을 재구축한다.
        /// </summary>
        //260527 hbk Phase 35 — CO-33-06: 시퀀스 소유 Shot 만 필터링 (D-A1 OwnerSequenceName)
        public void RebuildInspectionActions(ESequence seqId) {
            SequenceBase seq = this[seqId];
            if (seq == null) return;

            // CameraMasterParam의 기존 child 정리
            if (seq.Param is CameraMasterParam masterParam) {
                masterParam.ClearChildren();
            }

            //260527 hbk Phase 35 — CO-33-06: 시퀀스 매칭 키 (TOP/SIDE/BOTTOM)
            string targetSeqName = ResolveSequenceName(seqId);

            // Shot별로 Action_FAIMeasurement 생성
            var actions = new List<ActionBase>();
            //260527 hbk Phase 35 — CO-33-06: actionIdx 별도 사용 — 시퀀스별 로컬 0/1/2 인덱스로 EAction.FAI_Base + N 부여
            int actionIdx = 0;
            for (int i = 0; i < RecipeManager.ShotCount; i++) {
                ShotConfig shot = RecipeManager.Shots[i];
                //260527 hbk Phase 35 — CO-33-06: OwnerSequenceName 매칭만 추가 (빈값은 Top 폴백 — ApplyShotDefaults 가 보장)
                string shotOwner = string.IsNullOrEmpty(shot.OwnerSequenceName) ? SEQ_TOP : shot.OwnerSequenceName;
                if (shotOwner != targetSeqName) continue;
                EAction actionId = (EAction)((int)EAction.FAI_Base + actionIdx);
                string actionName = shot.ShotName ?? $"SHOT_{actionIdx}";
                var action = new Action_FAIMeasurement(actionId, actionName, shot);
                actions.Add(action);
                actionIdx++;
            }

            if (actions.Count > 0) {
                seq.AddAction(actions.ToArray());
            }
        }

        /// <summary>
        /// UI에서 Shot을 추가하고 Dynamic FAI 모드를 활성화한다.
        /// Sequence의 기존 Action을 Shot 기반 Action_FAIMeasurement로 교체한다.
        /// </summary>
        public ShotConfig CreateShot(ESequence seqId, string shotName = null) {
            ShotConfig shot = RecipeManager.AddShot(shotName);
            IsDynamicFAIMode = true;
            RebuildInspectionActions(seqId);
            return shot;
        }

        //260408 hbk UI에서 Shot 추가 시 Action 재구축 없이 IsDynamicFAIMode만 활성화
        public void EnableDynamicFAIMode() {
            IsDynamicFAIMode = true;
        }

        /// <summary>
        /// INI 파일에서 신규 SHOTS 포맷 로드 시도. 성공하면 IsDynamicFAIMode = true.
        /// </summary>
        public bool TryLoadNewFormat(IniFile loadFile) {
            if (!RecipeManager.HasNewFormatData(loadFile)) {
                IsDynamicFAIMode = false;
                return false;
            }

            RecipeManager.Load(loadFile);
            IsDynamicFAIMode = true;

            //260527 hbk Phase 35 — CO-33-06 hotfix (Plan 35-02 Part D): Top/Side/Bottom 모두 RebuildInspectionActions 호출
            //  이전 = Top 만 호출 → Side/Bottom Shot 이 INI 로드 후 seq.ActionCount=0 → 트리(InspectionListViewModel.CreateSequenceNode)에 안 보임
            //  RebuildInspectionActions 자체가 OwnerSequenceName 으로 필터링하므로 시퀀스별로 자기 소유 Shot 만 attach
            RebuildInspectionActions(ESequence.Top);
            RebuildInspectionActions(ESequence.Side);
            RebuildInspectionActions(ESequence.Bottom);
            return true;
        }

        /// <summary>
        /// 신규 SHOTS 포맷으로 INI에 저장.
        /// </summary>
        public bool SaveNewFormat(IniFile saveFile) {
            if (!IsDynamicFAIMode) return false;
            return RecipeManager.Save(saveFile);
        }
    }
}


