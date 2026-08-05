using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Setting;
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

        // 이 PC(CameraRole)에서 활성화할 시퀀스 판단.
        //  SIMUL 은 전체 활성(단일 PC 전 시퀀스 테스트). 실 HW 는 PC1=Top/Bottom, PC2=Side 만 활성 →
        //  비활성 시퀀스를 아예 생성하지 않아, 카메라 미등록으로 인한 OnCreate Error(StateAll 비-Idle) 를 원천 차단.
        //  DeviceHandler.RegisterRequiredDevices 의 등록 정책과 1:1 동기화. TopBottom=Top/Bottom, Side=Side 만 활성.
        private static bool IsSequenceActive(ESequence seqId) {
            ECameraRole role = SystemSetting.Handle.CameraRole;
            if (role == ECameraRole.TopBottom)
                return seqId == ESequence.Top || seqId == ESequence.Bottom;
            return seqId == ESequence.Side;
        }

        //260805 hbk Phase 69 D-01: RUN 게이트를 "전역 IsIdle" 에서 "시퀀스 단위 + 물리 카메라 공유 시에만 상호배타" 로 좁힌다.
        //  기존 StateAll/IsIdle 은 등록된 시퀀스 중 하나라도 non-Idle 이면 전체를 non-Idle 로 본다 → TOP 이 도는 중
        //  놀고 있는 BOTTOM 도 RUN 이 막혔다(간헐적 "RUN 미동작"의 실제 원인).
        //  단, 실 HW TopBottom 역할에서는 CAM_TOP/CAM_BOTTOM 이 같은 MilCamera 인스턴스를 공유하고
        //  (DeviceHandler.cs sharedMil 경로) grab 경로에 lock 이 없다 → 그 조합만은 반드시 상호배타로 남겨야 한다.
        //  판정 근거는 역할 이름 문자열이 아니라 DeviceHandler 가 돌려주는 실제 카메라 객체의 참조 동일성이다
        //  (디바이스 등록 구조가 바뀌어도 안전성이 깨지지 않도록).
        //  StateAll/IsIdle 자체는 건드리지 않는다 — TCP/MainWindow/MainView 호출부 회귀 0.
        /// <summary>
        /// 지정한 시퀀스를 지금 RUN 해도 되는지 판정한다.
        /// </summary>
        /// <param name="eTargetSeqId">실행하려는 시퀀스</param>
        /// <param name="sBlockingSeqName">차단된 경우 원인 시퀀스 이름. 실행 가능하면 null.</param>
        /// <returns>차단되어야 하면 true</returns>
        public bool TryGetBlockingSequence(ESequence eTargetSeqId, out string sBlockingSeqName) {
            sBlockingSeqName = FindBlockingSequenceName(eTargetSeqId);
            if (sBlockingSeqName == null) {
                return false;
            }
            //260805 hbk Phase 69: 차단 사실을 남겨야 "왜 안 눌렸는지" 사후 추적이 된다(사용자 클릭 빈도라 로그 폭주 없음).
            Logging.PrintLog((int)ELogType.Trace, "[RUN-GATE] blocked: target={0}, busy={1}",
                ResolveSequenceName(eTargetSeqId), sBlockingSeqName);
            return true;
        }

        //260805 hbk Phase 69 D-01: 차단 원인 시퀀스 이름을 찾는다. 실행 가능하면 null.
        private string FindBlockingSequenceName(ESequence eTargetSeqId) {
            SequenceBase seqTarget = this[eTargetSeqId];
            if (seqTarget == null) {
                //이 PC(CameraRole)에 등록되지 않은 시퀀스 — 실행 대상 자체가 없으므로 차단한다.
                return ResolveSequenceName(eTargetSeqId);
            }

            //1) 자기 자신이 Idle 이 아니면 무조건 차단 (기존 동작 유지. Finish/Error 도 non-Idle 로 본다)
            if (seqTarget.State != EContextState.Idle) {
                return seqTarget.Name;
            }

            //2) 물리 카메라를 실제로 공유하는 다른 시퀀스가 non-Idle 이면 차단
            for (int i = 0; i < Count; i++) {
                SequenceBase seqOther = this[i];
                if (seqOther == null) {
                    continue;
                }
                if (ReferenceEquals(seqOther, seqTarget)) {
                    continue;
                }
                if (seqOther.State == EContextState.Idle) {
                    continue;
                }
                if (SharesCameraDevice(seqTarget, seqOther) == false) {
                    continue;
                }
                return seqOther.Name;
            }

            return null;
        }

        //260805 hbk Phase 69 D-01: 두 시퀀스가 같은 물리 카메라 객체를 쓰는지 참조 동일성으로 판정한다.
        //  해석 불가(이름 공백/미등록/DeviceHandler null)는 "공유한다"로 간주 — 안전한 쪽(fail-closed)으로만 틀리게 한다.
        private bool SharesCameraDevice(SequenceBase seqA, SequenceBase seqB) {
            List<VirtualCamera> listCamA = new List<VirtualCamera>();
            List<VirtualCamera> listCamB = new List<VirtualCamera>();

            if (TryCollectSequenceCameras(seqA, listCamA) == false) {
                return true;
            }
            if (TryCollectSequenceCameras(seqB, listCamB) == false) {
                return true;
            }

            for (int i = 0; i < listCamA.Count; i++) {
                for (int j = 0; j < listCamB.Count; j++) {
                    if (ReferenceEquals(listCamA[i], listCamB[j])) {
                        return true;
                    }
                }
            }
            return false;
        }

        //260805 hbk Phase 69 D-01: 시퀀스가 grab 에 쓰는 카메라 객체를 모은다.
        //  마스터 파라미터(CameraMasterParam.DeviceName) + 각 Action 의 ICameraParam.DeviceName 을 모두 본다 —
        //  자식 Action 이 마스터와 다른 DeviceName 을 가질 수 있기 때문(SequenceBase.cs L109-110 상속은 빈 값일 때만).
        //  하나라도 해석 실패하면 false 를 돌려 호출부가 fail-closed 로 처리하게 한다.
        private bool TryCollectSequenceCameras(SequenceBase seq, List<VirtualCamera> listOut) {
            if (seq == null) {
                return false;
            }
            DeviceHandler devs = SystemHandler.Handle.Devices;
            if (devs == null) {
                return false;
            }

            List<string> listNames = new List<string>();

            CameraMasterParam masterParam = seq.Param as CameraMasterParam;
            if (masterParam != null) {
                listNames.Add(masterParam.DeviceName);
            }
            for (int i = 0; i < seq.ActionCount; i++) {
                ActionBase act = seq[i];
                if (act == null) {
                    continue;
                }
                ICameraParam camParam = act.Param as ICameraParam;
                if (camParam == null) {
                    continue;
                }
                listNames.Add(camParam.DeviceName);
            }

            for (int i = 0; i < listNames.Count; i++) {
                string sName = listNames[i];
                if (string.IsNullOrEmpty(sName)) {
                    //빈 이름은 마스터에서 상속받는 자식 — 마스터 항목으로 이미 커버된다.
                    continue;
                }
                VirtualCamera cam = devs[sName];
                if (cam == null) {
                    //등록되지 않은 디바이스 이름 — 어느 물리 카메라인지 알 수 없으므로 해석 실패로 처리.
                    return false;
                }
                if (listOut.Contains(cam) == false) {
                    listOut.Add(cam);
                }
            }

            //하나도 못 모았으면 판정 근거가 없다 → 해석 실패.
            return listOut.Count > 0;
        }

        private void RegisterSequences() {
            //260409 hbk Phase 5: 동적 FAI 모드용 InspectionSequence (D-07)
            //260526 hbk Phase 33 — Side/Bottom 도 InspectionSequence 마이그레이션 (D-01)
            //260604 hbk Phase 41 CO-41-02 — 역할 활성 시퀀스만 등록(SIMUL 전체). 비활성 시퀀스 미생성 → 카메라 미등록 OnCreate Error 차단.
            var seqs = new List<SequenceBase>();
            if (IsSequenceActive(ESequence.Top))
                seqs.Add(new InspectionSequence(ESequence.Top, SEQ_TOP, Top_Alg_Index, DeviceHandler.CAMERA_TOP, LightHandler.LIGHT_RING)); //260625 hbk Phase 64 LIGHT-01
            if (IsSequenceActive(ESequence.Side))
                seqs.Add(new InspectionSequence(ESequence.Side, SEQ_SIDE, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_BAR)); //260625 hbk Phase 64 LIGHT-01
            if (IsSequenceActive(ESequence.Bottom))
                seqs.Add(new InspectionSequence(ESequence.Bottom, SEQ_BOTTOM, Bottom_Alg_Index, DeviceHandler.CAMERA_BOTTOM, LightHandler.LIGHT_BACK)); //260625 hbk Phase 64 LIGHT-01
            SequenceBuilder.RegisterSequence(seqs.ToArray());
        }

        private void RegisterActions() {
            //260526 hbk Phase 33 — Side/Bottom placeholder; RebuildInspectionActions(Side|Bottom) 가 동적 FAI 모드 진입 시 Action_FAIMeasurement 로 교체
            //260604 hbk Phase 41 CO-41-02 — 역할 활성 시퀀스의 Action 만 등록(InitializeSequences 의 AddAction 과 1:1 대응).
            var acts = new List<ActionBase>();
            if (IsSequenceActive(ESequence.Top))
                acts.Add(new TopInspectionAction(EAction.Top_Inspection, ACT_INSPECT, Top_Alg_Index, Inspection_Model_Index));
            if (IsSequenceActive(ESequence.Side))
                acts.Add(new TopInspectionAction(EAction.Side_Inspection, ACT_INSPECT, Side_Alg_Index, Inspection_Model_Index));
            if (IsSequenceActive(ESequence.Bottom))
                acts.Add(new BottomInspectionAction(EAction.Bottom_Inspection, ACT_INSPECT, Bottom_Alg_Index, Inspection_Model_Index));
            SequenceBuilder.RegisterAction(acts.ToArray());
        }

        private void InitializeSequences() {
            //260604 hbk Phase 41 CO-41-02 — 역할 활성 시퀀스만 생성/등록(SIMUL 전체). 비활성은 Sequences dict 미포함 → ExecOnCreate 대상 제외.
            if (IsSequenceActive(ESequence.Top)) {
                SequenceBuilder seqTop = SequenceBuilder.CreateSequence(ESequence.Top);
                seqTop.AddAction(EAction.Top_Inspection);
                RegisterSequence(seqTop);
            }
            if (IsSequenceActive(ESequence.Side)) {
                SequenceBuilder seqSide = SequenceBuilder.CreateSequence(ESequence.Side);
                seqSide.AddAction(EAction.Side_Inspection);
                RegisterSequence(seqSide);
            }
            if (IsSequenceActive(ESequence.Bottom)) {
                SequenceBuilder seqBottom = SequenceBuilder.CreateSequence(ESequence.Bottom);
                seqBottom.AddAction(EAction.Bottom_Inspection);
                RegisterSequence(seqBottom);
            }
        }

        /// <summary>
        /// RecipeManager의 Shot 목록 기반으로 시퀀스의 Action을 재구축한다.
        /// </summary>
        //260527 hbk Phase 35 — CO-33-06: 시퀀스 소유 Shot 만 필터링 (D-A1 OwnerSequenceName)
        //260722 hbk Phase 68 D-01b: 필터링 후 ShotConfig.ZIndex 오름차순 안정 정렬(동일 ZIndex 는 기존 append 순서 그대로 유지) 추가 —
        //  SequenceBase.StartSubset 이 min-max 연속구간만 실행하므로, 같은 z_index Shot 들이 Actions[] 에서 항상 연속 블록이어야
        //  크로스-Z(D-01) 부분실행이 안전하다. List<T>.Sort/Array.Sort 는 불안정 정렬이라 동일 ZIndex 내 순서가 보존 안 됨 —
        //  안정 정렬이 보장되는 LINQ OrderBy 만 사용한다.
        //260729 hbk quick-260729-jq5: 과거 InspectionListView 가 이 정렬 순서에 서수로 결합돼 있었으나(엉뚱한 Shot 실행
        //  결함) 이제 Actions[] 의 ShotParam 참조 동일성으로 조회한다 — UI 측 동시 수정 의무 해소.
        public void RebuildInspectionActions(ESequence seqId) {
            SequenceBase seq = this[seqId];
            if (seq == null) return;

            // CameraMasterParam의 기존 child 정리
            if (seq.Param is CameraMasterParam masterParam) {
                masterParam.ClearChildren();
            }

            //260527 hbk Phase 35 — CO-33-06: 시퀀스 매칭 키 (TOP/SIDE/BOTTOM)
            string targetSeqName = ResolveSequenceName(seqId);

            //260722 hbk Phase 68 D-01b: 이 시퀀스 소유 Shot 만 먼저 필터링(OwnerSequenceName 매칭, 빈값은 Top 폴백 — ApplyShotDefaults 가 보장)
            var ownedShots = new List<ShotConfig>();
            for (int i = 0; i < RecipeManager.ShotCount; i++) {
                ShotConfig shot = RecipeManager.Shots[i];
                string shotOwner = string.IsNullOrEmpty(shot.OwnerSequenceName) ? SEQ_TOP : shot.OwnerSequenceName;
                if (shotOwner != targetSeqName) continue;
                ownedShots.Add(shot);
            }
            //260722 hbk Phase 68 D-01b: ZIndex 오름차순 안정 정렬 — 동일 ZIndex Shot 은 위 필터링 순서(원래 append 순서) 그대로 유지(OrderBy 안정성 보장)
            List<ShotConfig> sortedShots = ownedShots.OrderBy(shot => shot.ZIndex).ToList();

            // Shot별로 Action_FAIMeasurement 생성 (정렬된 순서 그대로 EAction.FAI_Base + N 부여)
            //260527 hbk Phase 35 — CO-33-06: actionIdx 별도 사용 — 시퀀스별 로컬 0/1/2 인덱스로 EAction.FAI_Base + N 부여
            var actions = new List<ActionBase>();
            int actionIdx = 0;
            foreach (ShotConfig shot in sortedShots) {
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
        //260611 hbk existingFile = 덮어쓰기 전 디스크 레시피 (비활성 시퀀스 CameraRole Datum 보존용)
        public bool SaveNewFormat(IniFile saveFile, IniFile existingFile = null) {
            if (!IsDynamicFAIMode) return false;
            return RecipeManager.Save(saveFile, existingFile);
        }
    }
}


