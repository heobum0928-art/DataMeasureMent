---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Quality + Workflow + Algorithm
status: executing
stopped_at: "23.1-03 Task 1+2 완료, Task 3 checkpoint:human-verify 대기"
last_updated: "2026-05-17T00:30:21.336Z"
last_activity: 2026-05-17
progress:
  total_phases: 8
  completed_phases: 6
  total_plans: 32
  completed_plans: 30
  percent: 94
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-04 for v1.1)

**Core value:** Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 + Datum 자동 보정 수행
**Current focus:** Phase 23.1 — edgetolinedistance-roi

## Current Position

Phase: 23.1 (edgetolinedistance-roi) — EXECUTING
Plan: 3 of 3
Status: Ready to execute
Next: 사용자 선택 — (a) v1.1 quick CO-23-01 디버깅 (A1~A5 측정값 미표시 — PixelResolutionX 0 또는 binding 단절 추적), (b) Phase 24 워크플로우 e2e, (c) Phase 25 결과 분석/Export
Last activity: 2026-05-18 - Completed quick task 260518-vxp: Phase 23.1 UAT 후속 4건 (Datum Load TeachingImagePath / FAI 레거시 Edge탭 숨김 / ROI 하이라이트+라벨 / Tol 절대값)

## Performance Metrics

**Velocity:**

- Total plans completed: 16
- Average duration: —
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 02 | 2 | - | - |
| 09 | 5 | - | - |
| 10 | 2 | - | - |
| 18 | 7 | - | - |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*
| Phase 01-ui P01 | 2 | 2 tasks | 4 files |
| Phase 01-ui P02 | 6 | 2 tasks | 7 files |
| Phase 01-ui P02 | 90 | 3 tasks | 7 files |
| Phase 03 P01 | 150 | 2 tasks | 4 files |
| Phase 03 P02 | 180 | 1 tasks | 4 files |
| Phase 07 P01 | 4 | 4 tasks | 7 files |
| Phase 09-verification-backfill P01 | 4 | 1 tasks | 1 files |
| Phase 09-verification-backfill P02 | 3 | 1 tasks | 1 files |
| Phase 09-verification-backfill P03 | 5 | 1 tasks | 1 files |
| Phase 09-verification-backfill P04 | 2 | 1 tasks | 1 files |
| Phase 09-verification-backfill P05 | 1 | 1 tasks | 1 files |
| Phase 11 P01 | 12 | 3 tasks | 3 files |
| Phase 11 P03a | 4 | 3 tasks | 3 files |
| Phase 12 P01 | 10 | 3 tasks | 3 files |
| Phase 12 P02 | 8 | 3 tasks | 1 files |
| Phase 12 P03 | 20 | 5 tasks | 5 files |
| Phase 15 P01 | 4 | 3 tasks | 2 files |
| Phase 15 P02 | 10 | 3 tasks | 1 files |
| Phase 15 P03 | 5 | 2 tasks | 2 files |
| Phase 17 P17-01 | 6 | 3 tasks | 4 files |
| Phase 17 PP17-02 | 6 | 4 tasks | 4 files |
| Phase 17 PP17-03 | 12 | 5 tasks | 6 files |
| Phase 17 P17-04 | 6 | 1 tasks | 1 files |
| Phase 18-carry-over-cleanup P01 | 10 | 1 tasks | 1 files |
| Phase 18 P02 | 15 | 2 tasks | 3 files |
| Phase 18-carry-over-cleanup P03 | 8 | 1 tasks | 1 files |
| Phase 19 P01 | 3 | 2 tasks | 3 files |
| Phase 19 P02 | 4 | 2 tasks | 1 files |
| Phase 28-fai-circlediameter-datum-circle P01 | 6 | 1 tasks | 1 files |
| Phase 28 P02 | 3 | 2 tasks | 1 files |
| Phase 28 PP03 | 2 | 2 tasks | 1 files |
| Phase 21 P21-01 | 25 | 2 tasks | 3 files |
| Phase 21 P21-02 | 3 | 3 tasks | 3 files |
| Phase 22 P22-01 | 2 | 2 tasks | 1 files |
| Phase 22 P22-02 | 10 | 4 tasks | 2 files |
| Phase 23 P01 | 12 | 3 tasks | 3 files |
| Phase 23 P02 | 2 | 3 tasks | 2 files |
| Phase 23.1 P01 | 3 | 2 tasks | 1 files |
| Phase 23.1 P02 | 8 | 3 tasks | 2 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Phase 5 (prior): Shot-FAI 2계층 데이터 모델 확정 (ShotConfig, FAIConfig, InspectionRecipeManager, Action_FAIMeasurement)
- Phase 5 (prior): CameraSlaveParam 상속으로 ShotConfig 구현 — 기존 프레임워크 호환
- Phase 5 (prior): IsDynamicFAIMode로 기존/신규 INI 포맷 자동 감지
- [Phase 01-ui]: SelectedNode typed as object for direct WPF TreeView.SelectedItem binding — no type converter needed in XAML
- [Phase 01-ui]: FAIResultRow.HasResult uses MeasuredValue > 0 sentinel (matches FAIConfig.ClearResult contract)
- [Phase 01-ui]: DisplayFAIImage uses fai.Owner cast to ShotConfig (FAIConfig has no image storage)
- [Phase 01-ui]: InspectionViewModel.AddFAI/RemoveFAI accept ShotConfig parameter explicitly (InspectionRecipeManager has no AddFAI)
- [Phase 01-ui]: FAI CRUD wired in InspectionListView (not MainView) — MainView is display-only, no tree logic
- [Phase 01-ui]: DataGrid dark theme requires explicit ColumnHeaderStyle + CellStyle — WPF parent Foreground not inherited by headers
- [Phase 01-ui]: Tree auto-expand in ListView_Loaded required for visibility in both editable and read-only modes
- [Phase 03]: FAIConfig ROI 직접 사용 (ToRoiDefinition 우회), ROI_Phi 기반 mm 변환
- [Phase 03]: RoiId -OK/-NG suffix를 Action_FAIMeasurement에서 SetResult 후 추가 (판정 시점 보장)
- [Quick 260409-e3v]: EEdgeMeasureType 삭제 → EdgeDirection/EdgeSelection/EdgeSampleCount/EdgeTrimCount/EdgePolarity로 교체 (MeasurementAlgorithm 패턴 일치)
- [Quick 260409-e3v]: FAIEdgeMeasurementService를 샘플 스트립 + FitLineContourXld 기반으로 재작성
- [Phase 07-01]: MeasurementBase.TryExecute 시그니처에 out List<EdgeInspectionOverlay> overlays 6번째 파라미터 추가 (D-01)
- [Phase 07-01]: EdgePairDistanceMeasurement만 FAIEdgeMeasurementService.result.Overlays 전달, 나머지 5종은 빈 리스트 (D-03, D-09)
- [Phase 07-01]: EdgeInspectionOverlay/HalconDisplayService/FAIEdgeMeasurementService 미수정 (D-11/D-12/D-13 anti-goal 준수)
- [Phase 07-01]: Action_FAIMeasurement.cs:157 CS7036 call-site 오류는 Plan 02 범위로 인계
- [Phase 07-02]: Measure 루프 overlay 누적 구조 — overlayAcc shot-scoped List + AddRange per Measurement (D-04, D-05)
- [Phase 07-02]: Judgement suffix 부여는 meas.LastJudgement 기준, FAI-Edge* 에만 적용 (D-06, D-07, D-08)
- [Phase 07-02]: L190 블로커 라인 (InspectionOverlays = new List<>()) 제거 → overlayAcc 대입으로 교체 (Gap I1 해소)
- [Phase 07-02]: SIMUL_MODE 육안 검증 사용자 승인 (2026-04-23) — 녹/적 에지 + 청록 DistLine 복구 확인
- [Phase 08-01]: REQUIREMENTS.md 동기화 — RC-01..RC-06 섹션 신설 + Traceability Status 10행 Complete 갱신 + 본문 체크박스 동기화 + 과도기 주석/Last-updated 정리 (단일 파일, 코드 무변경)
- [Phase 09-01]: Created 01-VERIFICATION.md backfill — 8/8 truths VERIFIED via code grep, status=verified (not human_needed) since 01-UAT.md is sign-off and unresolved tests are out-of-scope for UI-01..UI-05
- [Phase 09-02]: Created 03-VERIFICATION.md backfill — 8/8 truths VERIFIED, ALG-01/ALG-02/ALG-04 SATISFIED, ALG-04 row carries literal D-06 regression-and-recovery evidence string referencing 07-02-SUMMARY.md (G4 closed, zero code change)
- [Phase 09-03]: Created 06-VERIFICATION.md backfill — 6/6 RC truths VERIFIED via code grep, integrates quick 260417-kzd UAT (2026-04-22 user-approved) + Phase 7-02 per-Measurement overlay recovery timeline (Action_FAIMeasurement.cs:190 cleared) + Runtime lighting consumer 0-wiring backlog note (D-12); status=verified, gap_closure=[I1] (G5 closed, zero code change)
- [Phase 09-04]: Signed off 02-HUMAN-UAT.md in place per D-03 — frontmatter status partial→signed_off, updated:2026-04-23, all 5 tests carry result: PASS (2026-04-23 user-confirmed), Summary passed:5/pending:0; G7 Phase 2 half closed, zero code change (D-07)
- [Phase 09-05]: Created 05-HUMAN-UAT.md as born-signed_off file (file did not previously exist); promoted 05-VERIFICATION.md frontmatter human_verification[] 4 entries into 4 Test entries each marked result: PASS (2026-04-23 user-confirmed); Summary total:4/passed:4/pending:0; format mirrors 02-HUMAN-UAT.md; G7 Phase 5 portion closed — together with 09-04 fully closes audit Gap G7; zero code change (D-07)
- [Phase 11-01]: RoiShape enum placed as sibling type in RoiDefinition.cs (locality); RoiDefinition Shape/CenterRow/CenterCol/Radius added with backward-compat default; HalconDisplayService Circle branch short-circuits polygon check via continue; MainResultViewerControl follows file's actual Allman style over PATTERNS.md K&R guidance
- [Phase 11-03a]: DatumConfig SourceShotName + 8 volatile Line*Detected_* doubles + LastTeachSucceeded added with backward-compat defaults; TryTeachDatum line-coord writeback preserves signature (Option 2); RenderDatumOverlay gains ADDITIVE LastTeachSucceeded-gated branch — existing cyan/blue/magenta palette preserved (Warning 5 scope guard)
- [Phase 12-01]: EDatumAlgorithm enum placed in ReringProject.Sequence (co-located w/ DatumConfig for zero-import access); AlgorithmType stored as string (ParamBase can't serialize enum — switch-case L330-363 covers only Int32/Double/String/Boolean/Rect/Line/Circle/PropertyItem[]/ModelFinderViewModel); AlgorithmTypeEnum helper falls back to TwoLineIntersect on TryParse failure (legacy INI backward-compat); Rule 3 auto-fix — csproj Compile ItemGroup updated to register new file (blocking CS0246)
- [Phase 12-02]: DatumFindingService public TryTeachDatum becomes a switch(config.AlgorithmTypeEnum) dispatch; legacy Phase 4 body moved verbatim to private TryTeachTwoLineIntersect (error literals byte-identical — regression 0); new private TryExtractEdgePoints helper for raw edge tuples (used by 2-ROI horizontal concat path); CircleTwoHorizontal reuses VisionAlgorithmService.TryFindCircle (datumTransform=null for teaching identity); both new algorithms use GenContourPolygonXld×2 → ConcatObj → FitLineContourXld tukey for horizontal concat; MIN_HORIZONTAL_EDGES=10 threshold (D-15); public TryFindDatum (runtime) untouched per SPEC Out-of-scope; Req 5d (direction consistency) deferred to Phase 13 via literal TODO comment (D-17)
- [Phase 12-03]: MainView Datum 티칭 UI 3-way 상태머신 (btn_teachDatum + ECanvasMode.TeachDatum + EDatumTeachStep {Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done}) + GetFirstStep/GetNextStep switch + HalconViewer_DatumRect/CircleCompleted step별 DatumConfig write-back + Done 시 InvokeTryTeachDatum 자동 호출; InspectionListView Datum 노드 선택 시 btn_teachDatum 활성화; HalconDisplayService.RenderDatumOverlay 알고리즘별 분기 (Line2 rectangle은 TwoLineIntersect 에서만, CircleROI는 CircleTwoHorizontal 에서만, Horizontal A/B는 non-TwoLineIntersect 공용); LastTeachSucceeded 분기 하 CircleTwoHorizontal 검출 원 + 중심 십자 추가 렌더
- [Phase 13-04]: per-ROI 필드 sentinel 기본값 0/"" + EnsurePerRoiDefaults idempotent migration; legacy 글로벌 [Browsable(false)] + Category(legacy) INI 이중 저장; TryFindLine/TryExtractEdgePoints +3 params 모두 algorithmically active (5 hotfix 후); EdgeDirection-로 strip orientation (LtoR/RtoL=행슬라이스, TtoB/BtoT=열슬라이스); strip-loop MeasurePos 패턴 (SmallestRectangle2 per-strip Phi + TupleConcat 누적, hotfix fa91525); EdgeSampleCount = strip 개수(stripCount, default 20)로 재정의
- [Phase 15-02]: AppendEdgePointsFromStrip 4-way 명시 measurePhi 매핑(TtoB=-π/2/BtoT=+π/2/RtoL=π/LtoR=0) + selection 인자화(PascalCase→lower); SmallestRectangle2 자동 rp 의존 제거 (BtoT/TtoB 부호 결함 직접 원인 해결)
- [Phase 17-02]: DatumConfig ICustomTypeDescriptor (TLI/CTH/VTH 동적 PropertyGrid + Circle_EdgeDirection D-03 hide) + AlgorithmType 변경 5-step 리셋 + Delete 3-button 모달 + btn_teachDatum 호환성 가드 (ValidateRoiPresence 한국어 모달)
- [v1.1 roadmap]: 헝가리안 전체 리팩토링(QUAL-01) → Phase 26 마지막 배치 (다른 phase 와 merge 충돌 최소화 — 사용자 명시 결정)
- [v1.1 roadmap]: CO-02 (DatumConfig PropertyGrid Phase 17 Test 8 잔여) → Phase 19 QUAL-03 에 흡수 (동일 ICustomTypeDescriptor 작업 범위)
- [v1.1 roadmap]: WF-01/02 → Phase 24 (BUF Phase 21 + HW Phase 23 이후 배치 — end-to-end 에 버퍼+하드웨어 경로 포함)
- allNoFilter+sourceNames whitelist 패턴: DatumConfig.GetProperties Browsable(false) List<> 소스 프로퍼티 강제 포함으로 Circle_RadialDirection ItemsSource fallback 버그 수정 (CO-01)
- CO-04 컨텍스트 메뉴: IsTeachDatumMode + HitTestRoiAtPoint(Datum.*) 게이팅 — RoiRedrawRequested 이벤트로 ClearDatumRoiFields 위임 (Phase 18-02)
- [Phase 18-03]: FormatTeachError DatumConfig 인자 추가 — datum.DatumName 기반 "[DatumName] " 접두사, null/empty 가드 포함 (CO-06)
- [Phase 19-01]: DynamicPropertyHelper.FilterProperties 정적 헬퍼 추출 — DatumConfig.GetProperties 31줄→16줄 위임 호출 축약 (Phase 17 D-09 + Phase 18 CO-01 동작 회귀 0). T-19-02 mitigation으로 hideFunc null-guard, sourceNames null-safe 추가 (Rule 2 auto-fix x2)
- [Phase 19-02]: FAIConfig ICustomTypeDescriptor + EdgeMeasureType 동적 드롭다운 — DynamicPropertyHelper.FilterProperties 위임으로 CircleDiameter 시 6+2 hide. Task 1+2 단일 atomic commit 통합 (Rule 3 - blocking issue: ICustomTypeDescriptor 멤버 미구현 시 CS0535). DatumConfig 무수정으로 Phase 17/18 회귀 0 (CO-02 충족).
- [Phase 28-01]: EdgeOptionLists 에 MapRadialDirectionToHalconPolarity static helper + 4 FAI polar default consts (FaiCirclePolarStepDeg=10.0/RectL1Ratio=0.02/RectL2Ratio=0.02/EdgeSelection="First") 추가 — Wave 2 polar 분기 foundation. 값은 DatumConfig 의 CTH defaults 와 동일하여 REQ-28-03 동등성을 default 일치로 결정적 보장. fully-qualified System.StringComparison 사용으로 using 추가 0, 라인 1-27 무수정 (D-02/D-03/D-04)
- [Phase 28-02]: CircleDiameterMeasurement Circle_RadialDirection (string, default "") + Circle_RadialDirectionList wrapper [Category("Edge")] 추가 — TryExecute 가 string.IsNullOrEmpty(Circle_RadialDirection) 분기. 빈값 → 기존 TryFindCircle (args byte-identical → INI 회귀 0); Inward/Outward → TryFindCircleByPolarSampling 직접 호출 (D-01) with MapRadialDirectionToHalconPolarity + Plan 01 FaiCircle* defaults → Datum CTH 동등성 결정적. ICustomTypeDescriptor 미도입 (REQ-28-05). msbuild Debug/x64 PASS, 0 new errors/warnings.
- [Phase 28-03]: DatumFindingService.cs:200/:730 inline polarity ternary 2곳 → EdgeOptionLists.MapRadialDirectionToHalconPolarity 헬퍼 호출 교체 (D-03 service-layer cleanup). 3-way single source 달성 (Datum CTH find + teach + FAI CircleDiameter polar). Datum CTH 회귀 0 by 수학적 등가성 (Plan 01 helper byte-identical 증명). Line-for-line 1+/1- per task, msbuild Debug/x64 PASS 0 new errors/warnings, REQ-28-02/REQ-28-06 충족.
- [Phase 28-04]: 28-UAT.md sign-off (4/4 PASS) — Test 1 SIMUL UAT (PropertyGrid 콤보 Inward/Outward 시각 노출, 사용자 확인), Tests 2/3 코드 검증 (사용자 합의 — Plan 01 default-equality + Plan 02 fit-path args byte-identical + Plan 03 helper 3-way single source 인용), Test 4 자동 msbuild PASS. REQ-28-01 ~ REQ-28-06 모두 충족. PointToLineDistance ROI 시각화 미구현은 Phase 7-01 D-03 결정으로 Phase 28 범위 밖 carry-over.
- [Phase 20]: 14 파일 113 operator conversion (16 ?? + 33 ?: + 12 D-02 events + 39 ?: misc + 13 ?. misc) → 명시적 if/else; 'what' 주석 제거 / 'why' 주석 보존; hbk 마커 변환 라인만 260509 hbk Phase 20 으로 교체 (D-12), 비변환 라인 보존 (D-13); D-04/05 예외 (LINQ tail / expression-bodied) 적용 라인 0; msbuild Debug/x64 PASS + 신규 warning 0; W3 한국어 mojibake 0; 회귀 검증 경로 B (code-inspection fallback, W5 4 항목 정당화 — 의미적 동등 + msbuild + grep 매트릭스 + hbk baseline) 사용자 합의 (Phase 28 sign-off 와 동일 패턴). Wave 1 4 plan worktree mode 성공, 3 plan sandbox 차단 → 인라인 오케스트레이터 실행. QUAL-02 + QUAL-04 충족.
- Phase 21-01: BUF-02 lifetime XML doc — placed hbk marker on plain comment line above /// summary block (Phase 20 D-12 stacking-avoid pattern). 6 doc blocks + 7 markers across 3 files; behavior bytes preserved.
- [Phase 21-02]: D-02 channel #1 wire = Custom/SystemHandler.cs partial methods (Wire/Unwire/OnRecipeChanged_FlushBuffers); channel #3 = Release() ClearShots before Sequences.Dispose; AC#2 instrumentation = Logging.PrintLog as first statement of ClearShots (pre-dispose Shots.Count). 0 deviations, msbuild PASS.
- [Phase 22-01]: DatumConfig.TeachingImagePath public string ("" default) + [Category("Datum|ImageSource")] PropertyGrid 자동 노출 (L33-35); EnsurePerRoiDefaults null 가드 `if (TeachingImagePath == null) TeachingImagePath = ""` (L519) — `== null` 비교 채택 (NOT IsNullOrEmpty) 으로 사용자 클리어한 빈 문자열 보존. ParamBase reflection 자동 직렬화 → InspectionRecipeManager caller 코드 0 변경. ShotConfig 무수정 (디자인 lock-in (b): InspectionImagePath = ShotConfig.SimulImagePath 의미적 재해석).
- [Phase 22-02]: Action_FAIMeasurement.cs 코드 무수정 + 주석 2 라인만 추가 (L109 EStep.Grab, L226 GrabOrLoadDatumImage) — InspectionImagePath = ShotParam.SimulImagePath 역할 명시 + TeachingImagePath 와의 분리 못박음. msbuild Debug/x64 Rebuild PASS (0 errors, 6 warnings = Phase 21 baseline, 신규 0). 22-UAT.md 4/4 PASS (Test 1 시각 / Test 2 trust-based 코드 변경 0 근거 / Test 3 사용자 측 데이터 차이 해결 / Test 4 자동). Phase 20 D-12 marker stacking 패턴 준수 (기존 `//260409 hbk Phase 5` 보존 + 위에 `//260511 hbk Phase 22 IMG-02` 누적).
- [Phase 23-01]: TryFitLine signature 확장 — optional 'string selection = "all"' default param 채택으로 5 caller (PointToLine/PointToPoint/LineToLineAngle/LineToLineDistance × 2 호출씩 = 8건) 무수정 호환. MeasurePos 'all' 하드코딩 → measureSel 변수 3분기 (TryFindCircleByPolarSampling L249-264 패턴 차용). D-10 EdgeSelection 명시 (memory feedback) 충족.
- [Phase 23-01]: EdgeToLineDistanceMeasurement 신규 (Datum-relative Y 거리 측정, MeasurementFactory 7번째 algorithm). EdgeDirection default = TtoB (수평 에지 검출), EdgeSelection default = First (D-10), overlay = 빈 리스트 (PointToLineDistance 패턴, Phase 7-01 D-03). D-11 literal guard (datumTransform null/empty → 'Datum not found') 진입부 추가 — upstream gating 보조 이중 안전망. Y 부호 반전 = 클라이언트측 (-datumRow * pixelResolution, D-02).
- [Phase 23-02]: MeasurementFactory 7번째 case + GetTypeNames 7번째 원소 추가 — FAIConfig L60 ItemsSource 캐시 단일 소스로 INI Type dispatch + PropertyGrid 드롭다운 자동 노출 (D-13). 6 기존 case 무수정 (회귀 0).
- [Phase 23-02]: Action_FAIMeasurement.GrabOrLoadDatumImage 3-tier fallback chain — TeachingImagePath (우선) → SimulImagePath (회귀 0 baseline) → GrabHalconImage (최종). DatumConfigs[0] 첫 번째만 채택 (RESEARCH A6, D-01 CTH 1개). Pitfall 3 2-step 가드 (IsNullOrEmpty + File.Exists). Phase 22 IMG-02 marker (L226) 보존 — Phase 20 D-12 stacking 패턴.
- D-08: TryExecute 가 EdgeSelection 필드를 무시하고 리터럴 'All' 전달 — CO-23-01 #1 구조적 차단 (Phase 23.1-01)
- D-09: EdgeToLineDistanceMeasurement ICustomTypeDescriptor 구현 — PropertyGrid EdgeSelection 숨김, 사용자 재조작 원천 차단 (Phase 23.1-01)
- D-01(적용): 기존 Rect ROI 버튼 재사용 — ECanvasMode 신규 값 없음, _editingMeasurement != null 여부로 FAI/Measurement 분기 (Phase 23.1-02)
- D-03(적용): GetCurrentFAIRois 에서 Measurement Point ROI 추가 수집 — ToRoiDefinition 시그니처 무변경, FAI 노드 선택 시 다점 ROI 동시 렌더 (Phase 23.1-02)

### Quick Tasks Completed

| ID | Date | Description | Commits | Status |
|----|------|-------------|---------|--------|
| 260409-e3v | 2026-04-09 | Phase 3 에지 측정 파라미터 수정 (EEdgeMeasureType → EdgeDirection/Selection/SampleCount/TrimCount/Polarity) | 9599bbf, a65585f | |
| 260417-ou8 | 2026-04-17 | EdgePairDistanceMeasurement ROI 필드 제거 — FAIConfig 단일 소스화 (노란≠빨강 ROI 버그 구조적 제거) | 5bfde87 | |
| 260417-kzd | 2026-04-22 | Phase 6-04 UAT 잔여 결함 수정 — InspectionMasterParam DisplayName 편집 UI + Shot 실행 경로 매핑/지연 동기화 | 40ea796, a44debd, 40a7cca, 84b1bfb, 44523ad, abe8f55 | |
| 260423-hzt | 2026-04-23 | WR-RT-02 EdgeDirection/EdgePolarity ComboBox 처리 — PropertyGrid 자유 텍스트 → [ItemsSourceProperty] 드롭다운 (8 파일, string 유지로 INI 하위호환) | 5ff753a | |
| 260423-o53 | 2026-04-23 | 선택된 Rect/Circle ROI 마우스 드래그 이동 — hit-test + 이동 상태 머신 + RoiMoveCompleted 이벤트 → FAI 모델 좌표 반영 | f92be35 | |
| 260428-oqn | 2026-04-28 | VerticalTwoHorizontal Datum Vertical_* ROI 렌더 누락 수정 — RenderDatumOverlay Line1/Vertical 슬롯 분기 (W4-A 후속, "Vert" 라벨 + Horizon A/B 에지 가시화 동시 회복) | c6c15a4 | |
| 260429-c2e | 2026-04-29 | HALCON #1405 IntersectionLl 수정 — DatumFindingService CTH/VTH `ConcatObj→FitLine` 패턴을 `TupleConcat→단일 GenContourPolygonXld` (TryFindLine 833 라인 패턴) 으로 통일 | 311012a | Needs Review |
| 260430-hox | 2026-04-30 | Circle strip 12px cap + RectL1/L2Ratio default 0.05→0.02 — Phase 16 UAT FAIL (insufficient polar samples) root cause | 7ca39b6 | UAT pending |
| 260511-k3i | 2026-05-11 | ROI 버튼 트리 선택 fallback — 신규 FAI Measurement 0 케이스 대응 | 92f8c73 | UAT PASS (사용자 4 scenarios 2026-05-11) |
| 260511-ucv | 2026-05-11 | CO-22-01 — Datum↔FAI / SHOT PropertyGrid stale 해결: (A) InspectionList_SelectionChanged 게이트 e.Source→sender 교체 + (B) Action/Sequence/else 분기 force rebind 추가 | d6070e8, 50f5405 | UAT PASS (사용자 5/5 2026-05-11) |
| 260517-hvz | 2026-05-17 | ShotConfig 가 IOfflineImageParam 구현 — SHOT 노드 Load 시 SimulImagePath 자동 저장 (Load↔측정이미지 경로 미배선 갭 해소, 측정값 '—' 직접 원인 #1) | b01e60d | |
| 260517-ijg | 2026-05-17 | TryFitLine strip-loop MeasurePos 누적 재작성 — 단일 MeasurePos 1회 → sampleCount strip for-loop 누적 (라인 피팅 점 부족 = 측정값 '—' 근본 원인 #2). VisionAlgorithmService.AppendStrip 헬퍼 추가, 5 measure caller 자동 수혜 | a14f229 | UAT PASS (사용자 확인 2026-05-17 — 측정값+판정 표시) |
| 260517-ja8 | 2026-05-17 | EdgeToLineDistance overlay 시각화 — TryExecute 측정 성공 시 검출 에지 라인(FAI-Edge1, 녹/적) + 교점→에지 Y거리 선(FAI-DistLine, 청록) overlay 생성. Phase 7-01 D-03 빈 리스트 정책 의도적 뒤집기 | 5c3d36b | SIMUL UAT 대기 |
| 260517-l5e | 2026-05-17 | EdgeToLineDistance datum 기준 측정 수정 — ①datumTransform 오용→DatumConfig 교점 좌표 배선 ②UAT 피드백 반영 projection_pl 정사영 수직거리 ③MeasureAxis 로 X/Y 축 선택. 레거시 폴백 보존(회귀 0) | 74d0d15, 19e5663, e838542 | SIMUL UAT 대기 (실행 중 exe 잠금 MSB3027 — 앱 종료 후 재빌드 필요) |
| 260518-vxp | 2026-05-18 | Phase 23.1 UAT 후속 4건 — #3 Datum Load 가 DatumConfig.TeachingImagePath 에 기록(IOfflineImageParam 구현 + LoadAndDisplay 2-인자 오버로드) / #4 자식 Measurement 보유 FAI 의 레거시 Edge탭 PropertyGrid 숨김 / #6 ROI 선택 노란색 하이라이트 + Rect/Circle/Polygon 명칭 라벨 / #Tol EvaluateJudgement 공차 절대값 처리(부호 무관 입력). 빌드 Rebuild 0 errors | 2260353, 9ff516b, 1fcaed6 | SIMUL UAT 대기 |

### Pending Todos

| ID | Date | Description | Source | Status |
|----|------|-------------|--------|--------|
| CO-22-01 | 2026-05-11 | Datum 노드 ↔ FAI 노드 PropertyGrid 전환 동작 안 됨 — 트리 선택 시 즉시 갱신 안 됨. Phase 17 ICustomTypeDescriptor 와의 상호작용 가능성. 별도 quick task 로 재현/원인 추적 필요. | Phase 22 UAT carry-over | **resolved** (quick 260511-ucv, d6070e8 + 50f5405, UAT 5/5 PASS) |

### Blockers/Concerns

None yet.

## Deferred Items

Items acknowledged and deferred at v1.0 milestone close on 2026-05-04:

| Category | Item | Status |
|----------|------|--------|
| quick_task | 260409-e3v-phase-3 | missing |
| quick_task | 260417-kzd-phase-6-04-uat-displayname-ui-shot | missing |
| quick_task | 260417-ou8-edgepairdistancemeasurement-roi-faiconfi | missing |
| quick_task | 260423-ctx-roi-editmode-delete-contextmenu | missing |
| quick_task | 260423-hnd-roi-edit-handles-resize-vertex | missing |
| quick_task | 260423-hzt-wr-rt-02-edgedirection-edgepolarity-comb | missing |
| quick_task | 260423-lws-datum-grab-loadimage-datumconfig | missing |
| quick_task | 260423-o53-add-roi-move-drag | missing |
| quick_task | 260428-oqn-fix-verticaltwohorizontal-datum-vertical | missing |
| quick_task | 260429-c2e-fix-halcon-1405-in-datumfindingservice-u | missing |
| quick_task | 260430-hox-circle-strip-phase-16-uat-fail-root-caus | missing |
| uat_gap | Phase 02 / 02-HUMAN-UAT.md | signed_off |
| uat_gap | Phase 04 / 04-UAT.md | partial |
| uat_gap | Phase 05 / 05-HUMAN-UAT.md | signed_off |
| uat_gap | Phase 13 / 13-UAT.md | partial |
| uat_gap | Phase 15 / 15-UAT.md | partial |
| uat_gap | Phase 16 / 16-UAT.md | signed_off |
| uat_gap | Phase 17 / 17-UAT.md | partial |
| verification_gap | Phase 02 / 02-VERIFICATION.md | human_needed |
| verification_gap | Phase 04 / 04-VERIFICATION.md | gaps_found |
| verification_gap | Phase 05 / 05-VERIFICATION.md | human_needed |
| verification_gap | Phase 10 / 10-VERIFICATION-REPORT.md | human_needed |

Note: Quick task slugs are git commits without paired `.planning/quick/` artifacts (commits exist in history). UAT gaps are partial sign-offs (pending=0, but not all scenarios PASS). Verification gaps are documented but unresolved at milestone close. All carried over to v1.1 scope (project_v1_1_scope.md).

### Roadmap Evolution

- 2026-04-23: Phase 11 added — datum-teaching-ui-roi (WR-RT-01/03/04 묶음 예정, bugs.md 로드맵 기반)
- 2026-04-26: Phase 14 added — Datum carry-over (Circle 알고리즘 재설계 + Vertical 파라미터 그룹 + ROI 이동 회귀 + CircleTwoHorizontal/VerticalTwoHorizontal 정상화 + out-of-range UX 게이트). Phase 13 UAT 옵션 2 합의(commit d9b5cc8) 후속.
- 2026-04-29: Phase 15 added — HALCON MeasurePos 정합성 (DatumFindingService strip-loop 6 ROI + Circle polar).
- 2026-04-29: Phase 16 added — datum-circle-strip-redesign-algorithmtype-binding-fix (Phase 15 UAT carry-over).
- 2026-04-30: Phase 17 added — Datum 티칭/검증 UX 재설계 + Circle strip 1개 표시 + Test Find DetectedOrigin + 좌표 hover (Phase 16 UAT carry-over 16항목).
- 2026-05-04: v1.1 milestone started — Phases 18-26 defined (ROADMAP.md created).
- 2026-05-17: Phase 23.1 inserted after Phase 23: EdgeToLineDistance ROI 티칭 배선 + 다점 치수 지원 (URGENT) — SOP 도면 갭 대응 (ROI 측정별 티칭 미배선 + 다점 치수 P1/P2 미지원 + EdgeSelection 마이그레이션 + Datum 실측검증 + CO-23-01 재검증)

## Session Continuity

Last session: 2026-05-17T00:30:21.326Z
Stopped at: 23.1-03 Task 1+2 완료, Task 3 checkpoint:human-verify 대기
Resume file: None
Next action: 사용자 선택 — (a) CO-22-01 quick task (Datum↔FAI PropertyGrid 전환 UI 버그), (b) 다음 v1.1 phase (Phase 20 코드 스타일 / Phase 23 A시리즈 Simul), (c) 기타 backlog.

**v1.1 Phase Map:**

- Phase 18: Carry-over 정리 (CO-01, CO-03, CO-04, CO-05, CO-06) — signed_off 2026-05-07
- Phase 19: PropertyGrid 동적 노출 일반화 (QUAL-03, CO-02) — signed_off 2026-05-08
- Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 — signed_off 2026-05-08 (Phase 19 UAT 사용자 요청)
- Phase 20: 코드 스타일 정리 (QUAL-02, QUAL-04)
- Phase 21: 메모리 이미지 버퍼 (BUF-01, BUF-02) — signed_off 2026-05-11
- Phase 22: 이미지 이중화 구조 (IMG-01, IMG-02) — signed_off 2026-05-11 (재편 ce5764f, 원 HW-01 → v1.2 이연)
- Phase 23: A시리즈 Simul (재편 ce5764f, 원 HW-02 → v1.2 이연)
- Phase 24: 검사 워크플로우 end-to-end (WF-01, WF-02)
- Phase 25: 결과 분석 & Export (OUT-01, OUT-02, OUT-03, OUT-04)
- Phase 26: 헝가리안 전체 리팩토링 (QUAL-01)
- Phase 27: Side Inspection 확장 — 신설 2026-05-08 (LineToLineAngle + Side Fixture INI + PC2 분리)
- Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 — 신설 2026-05-08 (Phase 19 UAT 사용자 요청)

**Completed Phase:** 22 (image-dual-structure) — 2 plans — signed_off 2026-05-11T07:30:00Z (IMG-01+IMG-02, UAT 4/4 PASS)

**Planned Phase:** 23.1 (EdgeToLineDistance ROI 티칭 배선 + 다점 치수 지원) — 3 plans — 2026-05-16T23:56:09.229Z
