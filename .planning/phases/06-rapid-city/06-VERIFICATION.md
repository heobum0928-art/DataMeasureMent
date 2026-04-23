---
phase: 06-rapid-city
verified: 2026-04-23
status: verified
score: 6/6 must-haves verified
re_verification: true
quick_refs: ["260417-kzd"]
gap_closure: [I1]
cross_phase_refs: ["07-02-SUMMARY.md"]
---

# Phase 06: Rapid City — Verification Report

**Phase Goal:** Shot-FAI 2계층 구조를 Fixture(Sequence) > Datum + Shot(Action) > FAI > Measurement 4계층으로 확장하여, Sequence를 사용자 편집 가능 Fixture로 운영하고, Datum을 Sequence 레벨로 승격(Multi-Datum)하며, MeasurementBase 6종 파생 알고리즘과 새 INI 포맷(Version=6)으로 100개+ FAI/Measurement 운영을 가능하게 한다.

**Verified:** 2026-04-23
**Status:** verified
**Re-verification:** Yes — Phase 6 close 후 v1.0-MILESTONE-AUDIT.md(2026-04-22)에서 Gap I1(Action_FAIMeasurement.cs:190 overlay clear) 발견 → Phase 7-02 per-Measurement overlay 누적 구조로 복구되었으므로 본 보고서는 복구 후 최종 상태를 통합 검증한다. quick 260417-kzd UAT(2026-04-22 user-approved) 결과와 함께 정리한다.

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | RC-01: InspectionSequence가 Fixture 역할로 List<DatumConfig> + DisplayName을 소유한다 | ✓ VERIFIED | `InspectionSequence.cs:58-59` `Param = new InspectionMasterParam(this)` (quick 260417-kzd로 CameraMasterParam → InspectionMasterParam 교체); `InspectionSequence.DatumConfigs`/`AddDatum`/`RemoveDatum`/`TryRunDatumPhase`/`TryGetDatumTransform`/`GetDisplayName` 6종 API 확인 (06-02-SUMMARY.md Self-Check FOUND). `InspectionMasterParam.cs:12` `class InspectionMasterParam : CameraMasterParam` — `[Category("Fixture|Identity")] DisplayName` 노출 |
| 2  | RC-02: DatumConfig가 Sequence 레벨로 승격되고 Multi-Datum + DatumName/ImageSourceMode를 지원한다 | ✓ VERIFIED | `DatumConfig.cs`에 `DatumName` (default "Datum_1"), `ImageSourceMode` (default "Dedicated"), `ReuseFromShotName` 추가 (06-02-SUMMARY.md). `ShotConfig.Datum` 단일 소유 제거 + `ShotParam.Datum` 코드 참조 0건 (06-02-SUMMARY.md Self-Check FOUND). InspectionRecipeManager.cs Save/Load에서 `FIXTURE_DATUM_{d}` 섹션 별 ParamBase.Save/Load 라운드트립 (`InspectionRecipeManager.cs:105`, `:176`) |
| 3  | RC-03: MeasurementBase 추상 클래스 + 6종 파생 + VisionAlgorithmService 빌딩 블록이 존재한다 | ✓ VERIFIED | `MeasurementBase.cs` abstract `TryExecute(6-param)` + `EvaluateJudgement` (Plan 06-01 산출, Phase 7-01에서 6번째 out List<EdgeInspectionOverlay> 추가). 파생 6종: `EdgePairDistanceMeasurement` / `PointToLineDistanceMeasurement` / `PointToPointDistanceMeasurement` / `LineToLineAngleMeasurement` / `CircleDiameterMeasurement` / `LineToLineDistanceMeasurement` (모두 `WPF_Example/Custom/Sequence/Inspection/Measurements/`). `MeasurementFactory.Create(typeName, owner)` switch (file:10), `GetTypeNames()` (file:33). `VisionAlgorithmService.cs` 7개 public 메서드(TryFitLine/TryFindCircle/DistancePointToLine/DistancePointToPoint/AngleLineLine/IntersectLines/AffineTransformPoint) — 06-01-SUMMARY.md |
| 4  | RC-04: ShotConfig에 Ring/Back/Coax/Side 조명 8필드(bool Enabled + int Brightness)가 INI 자동 직렬화로 저장/로드된다 | ✓ VERIFIED | `ShotConfig.cs:26-39` 8필드 정의 (`RingLight_Enabled/Brightness`, `BackLight_*`, `CoaxLight_*`, `SideLight_*`). CameraSlaveParam 상속이므로 `[SHOT_{s}_CAM]` 섹션에서 ParamBase.Save/Load 자동 처리 (06-02-SUMMARY.md Design Notes; Pitfall 5 — 자동 직렬화 충분). 단, runtime 조명 컨트롤러 consumer는 미연결 (Backlog 섹션 참조) |
| 5  | RC-05: 새 INI 포맷 [FORMAT] Version=6 + Fixture-Datum-Shot-FAI-Measurement 계층으로 저장/로드 동작하고, 기존 포맷 로드 시 안내 메시지를 표시한다 | ✓ VERIFIED | `InspectionRecipeManager.cs:70-71` `ContainsSection("FORMAT")` + `Version`(int) detect; `:98` `saveFile["FORMAT"]["Version"] = CurrentFormatVersion(=6)`; `:105` `FIXTURE_DATUM_{d}`, `:138/220` `SHOT_{s}_FAI_{f}_MEAS_{m}`. `ERecipeFormatVersion` enum + `DetectFormatVersion` 분기 (Phase5/Unknown → CustomMessageBox.Show + return false, D-22). `HasNewFormatData`도 Phase 6 [FORMAT] Version=6에만 true (06-03-SUMMARY.md Design Notes) |
| 6  | RC-06: UI 트리에서 Sequence > Datum + Shot(Action) > FAI > Measurement 4계층을 탐색할 수 있고, 결과 테이블이 Measurement 단위로 표시된다 | ✓ VERIFIED | `Node.cs:48` `ENodeType.Measurement` case (chart 아이콘). `InspectionListViewModel.cs:101` `NodeType = ENodeType.Measurement` 트리 노드 자동 생성, `:136` `AddDatumNode(seqNode, datum)` Sequence 직계 자식, `:150` `AddMeasurementNode(faiNode, meas)`. `InspectionListView.xaml.cs:286` Measurement SelectionChanged → PropertyGrid 자동 바인딩, `:479/501` Btn_AddFAI 분기. `MeasurementResultRow.cs` (06-04-SUMMARY.md 신규) FAIName/MeasurementName/TypeName/DatumRef/Nominal/Tol±/측정값/판정 컬럼. `InspectionViewModel.OnFAISelected` → `fai.Measurements` 순회로 `MeasurementResults` 행 생성. quick 260417-kzd UAT(2026-04-22 user-approved)에서 Sequence/Shot 노드 Start 경로 동작 확인 |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` | abstract TryExecute + EvaluateJudgement + 공통 필드(DatumRef/Nominal/Tol±/LastMeasuredValue/LastJudgement) | ✓ VERIFIED | Plan 06-01 산출; Phase 7-01에서 6번째 out List<EdgeInspectionOverlay> 추가. `EvaluateJudgement` (file:59-70) Nominal±Tolerance 기준 LastJudgement |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` | typeName → 인스턴스 (6종) + GetTypeNames() | ✓ VERIFIED | Plan 06-01 산출. `Create(typeName, owner)` switch (file:10), default null. `GetTypeNames()` UI ComboBox 용 |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/*.cs` | 6종 파생 측정 클래스 | ✓ VERIFIED | Plan 06-01 산출. EdgePairDistance / PointToLineDistance / PointToPointDistance / LineToLineAngle / CircleDiameter / LineToLineDistance. EdgePairDistance는 D-19에 따라 FAIEdgeMeasurementService 래핑 |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | 7개 public 빌딩 블록 (FitLine/FindCircle/Distance/Angle/Intersect/AffineTrans) | ✓ VERIFIED | Plan 06-01 산출. 6종 파생 측정의 공통 Halcon 호출 래핑 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | DisplayName + DatumConfigs(List) + AddDatum/RemoveDatum + TryRunDatumPhase + TryGetDatumTransform | ✓ VERIFIED | Plan 06-02 산출. `Param = new InspectionMasterParam(this)` (file:58-59, quick 260417-kzd 교체). 6종 API |
| `WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs` | CameraMasterParam 상속 + DisplayName PropertyGrid 노출 + RaisePropertyChanged | ✓ VERIFIED | quick 260417-kzd 산출 (커밋 40ea796). `[Category("Fixture|Identity")] DisplayName` setter에서 `_insp.DisplayName` 갱신 |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | DatumName + ImageSourceMode + ReuseFromShotName 추가 (Phase 4 필드 유지) | ✓ VERIFIED | Plan 06-02 산출 — Phase 4 Line1/Line2/RefOrigin/CurrentTransform 필드 그대로 유지(D-05) |
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | Datum 단일 소유 제거 + Ring/Back/Coax/Side 8조명 필드 추가 | ✓ VERIFIED | Plan 06-02 산출. `ShotConfig.cs:26-39` 8조명 필드, `Datum` 프로퍼티 제거 + `ShotParam.Datum` 코드 참조 0건 |
| `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` | List<MeasurementBase> Measurements + AddMeasurement/RemoveMeasurement/ClearMeasurements | ✓ VERIFIED | Plan 06-01 산출 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | Phase 6 INI 포맷 ([FORMAT] Version=6, FIXTURE_DATUM_*, SHOT_*_FAI_*_MEAS_*), DetectFormatVersion, MeasurementFactory 로드 | ✓ VERIFIED | Plan 06-03 산출. `ERecipeFormatVersion` enum, `CurrentFormatVersion=6`, Save/Load 핵심 라인 (file:70/98/105/138/176/220/238) |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | EStep.DatumPhase 단계 + Measure 루프 (fai.Measurements × meas.TryExecute × overlayAcc 누적) | ✓ VERIFIED | Plan 06-03 산출(EStep.DatumPhase + Multi-Datum 루프) → Phase 7-02 보강(per-Measurement overlay 누적). `Action_FAIMeasurement.cs:139` `var overlayAcc = new List<EdgeInspectionOverlay>();`, `:185` `overlayAcc.AddRange(measOverlays);`, `:207` `pMyContext.InspectionOverlays = overlayAcc;` (line 190 빈 리스트 대입 제거 — Gap I1 해소) |
| `WPF_Example/UI/ControlItem/Node.cs` | ENodeType.Measurement enum + ImageSource case | ✓ VERIFIED | Plan 06-04 산출 (`Node.cs:48` chart 아이콘 case) |
| `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` | CreateSequenceNode 4계층 재설계 + AddDatumNode + AddMeasurementNode + HookSequenceDisplayNameUpdates | ✓ VERIFIED | Plan 06-04 + quick 260417-kzd 산출. `:101` Measurement 노드 생성, `:136/150` 헬퍼, `:177` HookSequenceDisplayNameUpdates (DisplayName 변경 시 트리 라벨 즉시 갱신) |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | SelectionChanged Measurement case + Btn_AddFAI/RemoveFAI 분기 + ResolveRunnableAction (Sequence/Shot Start 경로) | ✓ VERIFIED | Plan 06-04 + quick 260417-kzd 산출. `:286/408/570` Measurement 분기, `:479/501` Add 분기, `:154/175/537` ResolveRunnableAction (Sequence→seq[0], Shot→IndexOf, Idle 시 EnableDynamicFAIMode+RebuildInspectionActions로 지연 동기화) |
| `WPF_Example/UI/ViewModel/MeasurementResultRow.cs` | MeasurementBase 래핑 — FAIName/MeasurementName/TypeName/DatumRef/공차/측정값/판정 + Refresh() | ✓ VERIFIED | Plan 06-04 신규 산출 (csproj Compile Include 추가) |
| `WPF_Example/UI/ContentItem/MainView.xaml(.cs)` | DataGrid 컬럼 9개 (FAI/Measurement/Type/DatumRef/Nominal/Tol+/Tol-/측정값/판정) + MeasurementResults 바인딩 + FindFAIByName | ✓ VERIFIED | Plan 06-04 산출 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Recipe INI [FORMAT] Version=6 | Fixture-Datum-Shot-FAI-Measurement 4계층 트리 | `InspectionRecipeManager.LoadPhase6Format` (FIXTURE_DATUM_* → InspectionSequence.AddDatum + datum.Load; SHOT_*_FAI_*_MEAS_* → MeasurementFactory.Create + meas.Load + fai.Measurements.Add) → `InspectionListViewModel.CreateSequenceNode` 트리 빌드 (Sequence > Datum + Action > FAI > Measurement) | ✓ WIRED | InspectionRecipeManager.cs:176/220 (Load) → InspectionListViewModel.cs:CreateSequenceNode + AddDatumNode/AddMeasurementNode |
| Tree node (Sequence/Shot) Start click | StartSequence(seqID, actID) | `Btn_start_Click` → Idle 체크 → `ResolveRunnableAction(node, seq, out actID)` (Sequence→seq[0].ID; Shot(ShotConfig)→RecipeManager.Shots.IndexOf→seq[shotIdx].ID; 매핑 실패 시 EnableDynamicFAIMode+RebuildInspectionActions 지연 동기화 후 재시도; 일반 Action→node.ActionID) → SystemHandler.StartSequence | ✓ WIRED | InspectionListView.xaml.cs:154/175/537 (quick 260417-kzd 커밋 a44debd). UAT 5개 시나리오 통과 (2026-04-22 user-approved) |
| InspectionMasterParam.DisplayName setter | 트리 라벨 즉시 갱신 | `InspectionMasterParam.DisplayName.set` → `_insp.DisplayName = value` + `RaisePropertyChanged("DisplayName")` → `InspectionListViewModel.OnSequenceMasterPropertyChanged` (HookSequenceDisplayNameUpdates에서 PropertyChanged 구독) → `seqNode.Name = master.GetDisplayName()` | ✓ WIRED | InspectionListViewModel.cs:177-195 (quick 260417-kzd 커밋 40ea796) |
| Action_FAIMeasurement EStep.DatumPhase | InspectionSequence.TryRunDatumPhase | `ShotParam.Parent as InspectionSequence` → `parentSeq.TryRunDatumPhase(datumImage, out err)` → DatumConfigs 순회, DatumFindingService.TryFindDatum 호출, transforms 캐시; 실패 시 FinishAction(Error) | ✓ WIRED | Plan 06-03 산출. DatumConfigs 비면 D-10 pass-through (identity) |
| Action_FAIMeasurement EStep.Measure | meas.TryExecute(image, transform, pixelResolution, out value, out err, out overlays) | `ShotParam.FAIList` × `fai.Measurements` 중첩 루프; `parentSeq.TryGetDatumTransform(meas.DatumRef, out transform)` (빈 datumRef는 HomMat2dIdentity); `meas.TryExecute` 호출; 성공 시 `meas.EvaluateJudgement(value)` | ✓ WIRED | Plan 06-03 산출. Phase 7-01 D-01 6-param 시그니처 |
| measOverlays | pMyContext.InspectionOverlays | `var overlayAcc = new List<EdgeInspectionOverlay>()` (shot-scoped) → FAI-Edge* RoiId에 `meas.LastJudgement ? "-OK" : "-NG"` suffix → `overlayAcc.AddRange(measOverlays)` (per Measurement) → `pMyContext.InspectionOverlays = overlayAcc` (after both loops) | ✓ WIRED | Phase 7-02 산출 (Gap I1 해소). Action_FAIMeasurement.cs:139/180-185/207. line 190 빈 리스트 대입 제거 |
| ShotConfig 조명 8필드 | INI [SHOT_{s}_CAM] 섹션 | CameraSlaveParam 상속 + ParamBase.Save/Load 자동 직렬화 (bool/int 타입) | ✓ WIRED (저장/로드만) | Runtime 조명 컨트롤러 consumer 미연결 — Backlog 섹션 참조 |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| RC-01 | 06-02 | Sequence가 Fixture(한 면)로 동작하며 List<DatumConfig>를 소유하고 사용자 편집 가능한 DisplayName을 가진다 | ✓ SATISFIED | InspectionSequence.DatumConfigs/AddDatum/RemoveDatum/TryRunDatumPhase/TryGetDatumTransform/GetDisplayName + InspectionMasterParam.DisplayName(quick 260417-kzd로 PropertyGrid 노출) |
| RC-02 | 06-02 | DatumConfig가 Sequence 레벨로 승격되어 Multi-Datum을 지원한다 (DatumName, ImageSourceMode, ShotConfig.Datum 제거) | ✓ SATISFIED | DatumConfig.DatumName/ImageSourceMode/ReuseFromShotName + ShotConfig.Datum 제거 + InspectionRecipeManager FIXTURE_DATUM_* 섹션 라운드트립 |
| RC-03 | 06-01 | MeasurementBase 파생 클래스 6종이 각각 TryExecute()로 측정을 수행한다 (VisionAlgorithmService 빌딩 블록 포함) | ✓ SATISFIED | MeasurementBase + 6종 파생(EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/CircleDiameter/LineToLineDistance) + MeasurementFactory + VisionAlgorithmService |
| RC-04 | 06-02 | ShotConfig에 Ring/Back/Coax/Side 조명 필드 8개가 추가되고 INI로 저장/로드된다 | ✓ SATISFIED (저장/로드만) | ShotConfig.cs:26-39 8필드, ParamBase.Save/Load 자동 직렬화. Runtime 조명 컨트롤러 consumer 미연결 — D-12에 따라 Phase 6 범위 외, Backlog 이관 |
| RC-05 | 06-03 | 새 INI 포맷 [FORMAT] Version=6 + Fixture-Datum-Shot-FAI-Measurement 계층으로 저장/로드가 동작하고, 기존 포맷은 안내 메시지를 표시한다 (D-22) | ✓ SATISFIED | InspectionRecipeManager.ERecipeFormatVersion + DetectFormatVersion + LoadPhase6Format + SavePhase6Format + Phase5/Unknown 진입 시 CustomMessageBox.Show + return false |
| RC-06 | 06-04 | UI 트리에서 Sequence > Datum + Shot > FAI > Measurement 구조를 탐색할 수 있고, 결과 테이블이 Measurement 단위로 표시된다 | ✓ SATISFIED | Node.ENodeType.Measurement + InspectionListViewModel.CreateSequenceNode/AddDatumNode/AddMeasurementNode + InspectionListView.xaml.cs Measurement SelectionChanged + Btn_AddFAI/RemoveFAI 분기 + MeasurementResultRow + MainView DataGrid 9컬럼 + InspectionViewModel.MeasurementResults. quick 260417-kzd UAT(2026-04-22 user-approved)에서 Sequence/Shot Start 경로 + DisplayName 편집/저장-로드 왕복 5개 시나리오 통과 |

**Note on traceability:** RC-01..RC-06은 v1.0-MILESTONE-AUDIT.md(2026-04-22, lines 14-53)에서 "orphaned by definition"으로 분류된 Phase 6 내부 작업 항목으로, REQUIREMENTS.md v1 traceability 표에는 등재되어 있지 않다. 본 보고서는 09-CONTEXT D-05 범위 내에서 RC-01..RC-06이 Phase 6 산출물에서 모두 충족되었음을 코드 grep 증거로 확인한다. REQUIREMENTS.md v1 등재 여부는 별도 cleanup phase에서 결정한다 (09-CONTEXT.md Deferred 섹션 — traceability cleanup 이관).

### Notes — Phase 7 Regression Recovery Timeline

**Gap I1 (v1.0-MILESTONE-AUDIT.md lines 82-88, 200-211):** Phase 6의 Multi-Algorithm Measure 루프 도입 과정에서 Phase 3에서 구축된 per-Measurement edge overlay 가시화가 일시적으로 회귀(regression)한 후, Phase 7-02에서 복구된 이력을 다음과 같이 보존한다.

| Stage | Phase / Plan | Action | Outcome |
|-------|--------------|--------|---------|
| 1 | 03-02 | HalconDisplayService에 FAI-Edge*-OK/-NG green/red + FAI-DistLine cyan 색상 분기 추가, FAIEdgeMeasurementService → ActionContext.InspectionOverlays → MainView 파이프라인 구축 | 사용자 시각 검증 통과 (Phase 3 close) |
| 2 | 06-01 | MeasurementBase + 6종 파생 + MeasurementFactory 도입, EdgePairDistanceMeasurement는 D-19에 따라 FAIEdgeMeasurementService를 래핑하지만 result.Overlays를 ActionContext로 전달할 통로가 없음 | EdgePair 측정값은 정상이지만 overlays는 어디에도 흘러가지 못함 |
| 3 | 06-03 | Action_FAIMeasurement.EStep.Measure 루프를 fai.Measurements × meas.TryExecute로 재작성. 루프 종료 후 `Action_FAIMeasurement.cs:190`에 `pMyContext.InspectionOverlays = new List<EdgeInspectionOverlay>()` 무조건 대입 라인 존재 | Phase 3 overlays 완전 클리어 — DataGrid 측정값/판정은 정상이나 캔버스 녹/적/청록 가시화 사라짐 |
| 4 | v1.0-MILESTONE-AUDIT.md (2026-04-22) | Integration checker가 Action_FAIMeasurement.cs:190의 무조건 클리어를 Gap I1로 식별 (ALG-01/ALG-02/ALG-04 visualization 컴포넌트 영향) | I1 blocker로 등록 |
| 5 | 07-01 | MeasurementBase.TryExecute에 6번째 out parameter `List<EdgeInspectionOverlay> overlays` 추가 (D-01). EdgePairDistanceMeasurement만 result.Overlays 전달, 나머지 5종은 빈 리스트 (D-03/D-09) | 시그니처 통로 확보 |
| 6 | 07-02 | Action_FAIMeasurement.EStep.Measure 재작성: shot-scoped `var overlayAcc = new List<EdgeInspectionOverlay>()` (cs:139), Measurement별 `meas.LastJudgement ? "-OK" : "-NG"` suffix를 FAI-Edge* RoiId에 부여(cs:174-184), `overlayAcc.AddRange(measOverlays)` 누적(cs:185), `pMyContext.InspectionOverlays = overlayAcc` 단일 대입(cs:207). **Action_FAIMeasurement.cs:190의 빈 리스트 대입 라인 제거 — Gap I1 해소** | Phase 3 가시화 규약 완전 복구. SIMUL_MODE D:\1.bmp 육안 검증 사용자 승인 (2026-04-23) — 07-02-SUMMARY.md 참조 |

**최종 상태:** `Action_FAIMeasurement.cs:190` 라인은 더 이상 InspectionOverlays를 빈 리스트로 클리어하지 않으며, per-Measurement overlay 누적 구조가 활성화되어 있다. 본 보고서 status를 `verified`(re_verification: true)로 기록하는 근거이다 (07-02-SUMMARY.md cross-phase ref).

### Notes — quick UAT 260417-kzd (2026-04-22 user-approved)

Phase 6-04 close 후 사용자 UAT에서 두 가지 결함이 발견되어 quick 260417-kzd로 처리되었고, 2026-04-22에 5개 시나리오 모두 사용자 승인되었다 (`260417-kzd-SUMMARY.md`).

| Defect | Resolution | Commit |
|--------|-----------|--------|
| Sequence 노드 PropertyGrid에 DisplayName 필드 미노출 (CameraMasterParam에 해당 필드 없음) | `InspectionMasterParam : CameraMasterParam` 신규 — `[Category("Fixture|Identity")] DisplayName` setter에서 `_insp.DisplayName` 갱신 + `RaisePropertyChanged`. `InspectionSequence.cs:58-59` 생성자에서 `Param = new InspectionMasterParam(this)`. `InspectionListViewModel.HookSequenceDisplayNameUpdates` (file:177)로 PropertyChanged 구독 → 트리 라벨 즉시 갱신 | `40ea796` |
| Sequence/Shot 노드 Start 시 "There is no action to run" 또는 엉뚱한 Action 실행 (지연 동기화 실패) | `ResolveRunnableAction(NodeViewModel, SequenceBase, out EAction)` 헬퍼 신규 — Sequence 노드→`seq[0].ID`, Shot 노드(Param=ShotConfig)→`RecipeManager.Shots.IndexOf(shotCfg)`→`seq[shotIdx].ID`; 매핑 실패 시 Idle 조건에서 `EnableDynamicFAIMode + RebuildInspectionActions`로 지연 동기화 후 재시도. `Btn_start_Click` 단순화 → `StartSequence(seqID, actID)` | `a44debd` |

부수 정리: `40a7cca` (진단 다이얼로그 + Debug.WriteLine), `84b1bfb` (statusBar 진단 메시지 임시), `44523ad` (FAIConfig ↔ Measurement ROI 동기화 표면 패치), `abe8f55` (공차 단일 소스화 — `FAIConfig.NominalValue/Upper/LowerTolerance` 제거, 판정은 `MeasurementBase.EvaluateJudgement`만 사용). 병렬 quick `260417-ou8` (`5bfde87` ROI 단일 소스화)도 동일 UAT 창구에서 처리됨.

**UAT 통과 시나리오 (2026-04-22 user-approved):**
1. DisplayName 표시 / 편집 / 트리 갱신 / 저장-로드 왕복
2. Sequence 노드 Start → 첫 Action 실행
3. Shot 노드 Start (로드 상태) → 해당 Shot의 Action_FAIMeasurement 실행
4. UI 추가 Shot Start (지연 동기화로 동작)
5. 실행 중 재시작 차단 메시지

### Backlog — Runtime Lighting

Phase 6 INI 새 포맷 [SHOT_{s}_CAM] 섹션에는 Ring/Back/Coax/Side brightness 필드가 정의되어 INI로 저장/로드된다 (`ShotConfig.cs:26-39` — `RingLight_Enabled/Brightness`, `BackLight_*`, `CoaxLight_*`, `SideLight_*` 8필드, ParamBase 자동 직렬화). 그러나 Runtime 조명 컨트롤러(LightHandler / 시리얼 JPF·Pamtekbrands)로 brightness 값을 적용하는 consumer 와이어링은 미구현 상태이다.

`grep`으로 확인한 결과, `RingLight_Brightness`/`BackLight_Brightness`/`CoaxLight_Brightness`/`SideLight_Brightness` 식별자는 `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` 정의 외에 어떤 .cs 파일에서도 참조되지 않는다(consumer 0건). 즉, 현재는 "값 저장/로드만" 동작하며 Grab 직전에 LightHandler에 적용되는 경로는 없다 (06-02-SUMMARY.md Design Notes의 "실제 하드웨어 제어는 D-12 범위 외"와 일치). v1.0-MILESTONE-AUDIT.md tech_debt(06-rapid-city) 항목에도 동일하게 기록되어 있다.

이는 D-12(조명 하드웨어 연동은 Phase 6 범위 외, 값 저장만)의 의도된 결과이다. v1.0 milestone 범위 외이며 v2 / backlog로 이관한다. **본 phase(09)는 09-CONTEXT D-07에 따라 코드 변경 0건 원칙을 유지하므로, 본 보고서는 미연결 사실만 기록하고 수정하지 않는다.**

### Gaps Summary

- **Gap I1 — RESOLVED (Phase 7 recovery confirmed).** `Action_FAIMeasurement.cs:190`의 무조건 InspectionOverlays 클리어 라인이 Phase 7-02에서 제거되고 per-Measurement overlay 누적 구조(`overlayAcc.AddRange` + 단일 대입)로 교체되었다. Phase 3 가시화 규약(FAI-Edge*-OK/NG 녹/적, FAI-DistLine 청록)이 SIMUL_MODE 육안 UAT(2026-04-23 user-approved)에서 복구 확인되었다 (07-02-SUMMARY.md).
- **RC-01..RC-06 traceability registration — Deferred (cleanup phase).** RC-01..RC-06은 v1.0-MILESTONE-AUDIT.md에서 REQUIREMENTS.md 비등재 "orphaned" 분류로 식별되었으나, 본 phase(09) 범위(VERIFICATION 문서 보강)와 09-CONTEXT D-07(코드 변경 0건)을 넘어선다. 등재 여부는 별도 cleanup phase에서 결정한다.
- **Runtime lighting consumer wiring — Deferred (backlog / v2).** ShotConfig 조명 8필드가 INI 라운드트립까지는 동작하나, LightHandler로 brightness를 적용하는 consumer는 0건이다. D-12에 따라 Phase 6 범위 외이며, v2 / backlog로 이관한다.
- **5-class overlay visualization — Deferred (future phase).** PointToLine / PointToPoint / LineToLineAngle / LineToLineDistance / CircleDiameter는 Phase 7-01 D-03에 따라 빈 overlay 리스트를 반환한다. ALG-04 범위(에지 페어 시각화)는 충족되었으나, 5종 알고리즘 시각화는 EdgeInspectionOverlay 모델에 Circle/Arc shape 확장이 필요하므로 future phase로 이관됨 (07-02-SUMMARY.md Deferred Issues).

자동화 검증 범위 내(코드 grep + cross-phase summary 인용) **새로 발견된 갭은 없다.** 6/6 truths 가 VERIFIED. quick 260417-kzd UAT(2026-04-22 user-approved)와 Phase 7-02 SIMUL_MODE 육안 검증(2026-04-23 user-approved)으로 사용자 측 검증도 완료되어 있다.

---

_Verified: 2026-04-23_
_Verifier: Claude (gsd-executor, plan 09-03)_
_Re-verification trigger: v1.0-MILESTONE-AUDIT.md Gap G5 (missing 06-VERIFICATION.md) + Gap I1 (Phase 6 overlay regression — recovered in 07-02). Integrates: RC-01..RC-06 + quick 260417-kzd UAT + Phase 7 recovery timeline + Runtime lighting backlog (per 09-CONTEXT D-05)._
