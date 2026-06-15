---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: Phases
status: planning
stopped_at: Phase 42 context gathered
last_updated: "2026-06-15T02:28:09.349Z"
last_activity: 2026-06-15 - Quick 260615-dx7 완료 (반복 검사 입력 고정50회 → 이미지 폴더 N장 순회)
progress:
  total_phases: 7
  completed_phases: 6
  total_plans: 28
  completed_plans: 25
  percent: 89
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-04 for v1.1)

**Core value:** Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 + Datum 자동 보정 수행
**Current focus:** Phase --phase — 23.1

## Current Position

Phase: 39
Plan: Not started
Status: Ready to plan
Last activity: 2026-06-15 - Quick 260615-dx7 완료 (반복 검사 입력 고정50회 → 이미지 폴더 N장 순회)

**v1.2 우선순위 5단계 (POC 2026-06-30 기준):**

  1. WF-01/02 (검사 워크플로우 E2E) + OUT-01~04 (결과 분석/Export) — POC 시연 필수
  2. CO-38-01~04 (픽셀분해능/시작지연/실HW STARTUP) + CO-23-01 (A1~A5 UI) — v1.1 carry-over
  3. HW-01/02 (CXP 그래버 RAP 4G 4C12) — HW 도착 시 합류, 미도착 시 Simul 검증
  4. QUAL-01 (헝가리안 표기법 전체 리팩토링) — 시간 여유 시
  5. PROTO-01 (제어 프로토콜 v2.7) — POC 시연 이후, 제어팀(김민우 선임) 동기화 권장

**v1.1 종결 컨텍스트 (참고):**

- v1.1 shipped 2026-05-28 (git tag v1.1, 17 phases, 18~38 + 23.1/34.1)
- v1.1 충족 19/28, 이연 9 + 부분 1 — 위 v1.2 우선순위에 흡수
- v1.1 종결 시 Carry-over (open): CO-38-01~04, CO-23-01, CO-36-01 (PERPENDICULAR_TOLERANCE_DEG 하드코딩), CO-36-05 (Test 2/3/4/6/7 시각 UAT 미수행)

## Performance Metrics

**Velocity:**

- Total plans completed: 27
- Average duration: —
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 02 | 2 | - | - |
| 09 | 5 | - | - |
| 10 | 2 | - | - |
| 18 | 7 | - | - |
| 37 | 3 | - | - |
| 38 | 3 | - | - |
| 40.1 | 2 | - | - |
| 23.1 | 3 | - | - |

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
| Phase 31-datum-algorithm P01 | 5 | 3 tasks | 6 files |
| Phase 31-datum-algorithm P02 | 4 | 3 tasks | 5 files |
| Phase 31-datum-algorithm P03 | 8 | 3 tasks | 6 files |
| Phase 31-datum-algorithm P04 | 15 | 3 tasks | 2 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P01 | 10 | 2 tasks | 1 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P02 | 15 | 3 tasks | 12 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P03 | 15 | 2 tasks | 1 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P04 | 25 | 2 tasks | 3 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P05 | 131 | 2 tasks | 3 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P07 | 20 | 5 tasks | 5 files |
| Phase 32-sop-i9-i10-e2-e9-e10-e3 P08 | 25 | 3 tasks | 2 files |
| Phase 34.1 P01 | 7 | 3 tasks | 3 files |
| Phase 37 P01 | 6 | 2 tasks | 1 files |
| Phase 37 P02 | 4 | 2 tasks | 2 files |
| Phase 39.3 P01 | 12 | 4 tasks | 2 files |
| Phase 39.3 P03 | 2 | 1 tasks | 1 files |
| Phase 39.3 P02 | 5 | 5 tasks | 2 files |
| Phase 39.4-bottom-dualimage-manual-swap-2026-05-30 P01 | 111 | 1 tasks | 1 files |
| Phase 39.4-bottom-dualimage-manual-swap-2026-05-30 P02 | 360 | 1 tasks | 1 files |
| Phase 39.4-bottom-dualimage-manual-swap-2026-05-30 P03 | 15 | 2 tasks | 1 files |
| Phase 40-export-i-1-2026-06-01 P01 | 264 | 3 tasks | 4 files |
| Phase 40-export-i-1-2026-06-01 P40-02 | 45 | 3 tasks | 3 files |
| Phase 41 P01 | 135 | 2 tasks | 2 files |
| Phase 41-cxp-mil-lite-10-0-grab-hw-01-hw-02 P02 | 420 | 2 tasks | 2 files |
| Phase 41-cxp-mil-lite-10-0-grab-hw-01-hw-02 P03 | 181 | 3 tasks | 3 files |
| Phase 40.2-fai-2 P01 | 25 | 3 tasks | 5 files |
| Phase 40.2-fai-2 P02 | 30 | 2 tasks | 3 files |
| Phase 40.2-fai-2 P03 | 15 | 2 tasks | 2 files |

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
- D-03: IDatumOriginConsumer 인터페이스 — namespace ReringProject.Sequence, 3 double 멤버 (DatumOriginRow/Col/AngleRad)
- D-04: ComputeProjectionDistance static — EdgeToLineDistance L126~196 projection_pl 블록 이식, 신규 타입 6종 재사용
- Task 3(C) 교체 보류: EdgeToLineDistanceMeasurement 내부 projection_pl 블록 유지 (overlay footRow/footCol 회귀 방지), 신규 타입만 ComputeProjectionDistance 호출
- CircleCenterDistance(E8): TryFindCircle + ComputeProjectionDistance, MeasureAxis 기본값 Y (Datum B Y 방향)
- EdgeToLineAngle(D1/H5): TryFitLine(All) + AngleLineLine, Datum 기준선 ±200px 2점 구성, degree 반환
- ArcEdgeDistance(G시리즈): TryFitLine(All) 라인 중점 + ComputeProjectionDistance, MeasureAxis 기본값 X (Datum C X)
- ArcLineIntersectDistance(I9/I10): 3점 arc ROI TryFitLine('All') 중점 → TryFitArc → TryFitLine → TryIntersectCircleLine(D-10) → ComputeProjectionDistance, MeasureAxis='X'
- CompoundAngle(E2): TryComputeChainPoint(CL2/CL3/La/Lb→Pc) + AngleLineLine(CL1중심→Pc vs DatumB±200px), D-09 캡슐화 D-11 별도타입
- CompoundCenterCDistance(E9) MeasureAxis='X' / CompoundCenterBDistance(E10) MeasureAxis='Y' — D-07/D-11, Pitfall 8 방지
- CO-23.1-02: FindSelectedRectMeasurement 화이트리스트(Point_* 7종) + CommitRectRoi as 분기 일반화 — _editingMeasurement MeasurementBase 타입으로 확장
- CO-23.1-01: Option A(경로 레이블) 채택 — 하단 Border 2행 + UpdateImageSourceLabel(DatumConfig TeachingImagePath vs ShotConfig SimulImagePath 판별)
- IntersectionContoursXld HALCON 시그니처 = 3-out (iRow, iCol, isOverlapping) — TryIntersectContours 구현 시 2-out 오해→CS7036→Rule 1 즉시 수정
- 재작성 4종(ArcLineIntersect/CompoundAngle/CenterC/CenterB) IDatumOriginConsumer stub 추가 — CS0535 빌드 차단 Rule 3 auto-fix, Plan 03/04 재작성 시 교체
- 3점 호 피팅(Arc_P1~P3) + 원-직선 교점 방식 완전 폐기. EdgeA(수직)/EdgeB(수평) 2 ROI 직선 피팅 + TryIntersectLines 교점 방식으로 전환 (Phase 32 Plan 03)
- CL1~CL3/La/Lb 기하 체인 폐기 → TryFindLargestContourRect 단일 컨투어 알고리즘으로 E2/E9/E10 재작성 (Phase 32 Plan 04)
- E2 DatumC 원중심 미주입(0,0) 명시 error 반환 — T-32-07 mitigation, CircleTwoHorizontal datum 전제 안전 종결 (Phase 32 Plan 04)
- E3 TypeName = CompoundShortAxisDistance (CONTEXT.md 미해결#1 확정)
- IDatumOriginConsumer 미구현 — 단축 폭은 사각형 자체 기하, Datum 비의존 (32-05)
- 2 * min(length1,length2) 직접 계산 채택 — intersection_contours_xld 불필요, 수학적 등가 (32-05)
- overlay ADDITIVE 원칙: return true 직전 삽입, HALCON 재호출 없음, 이미 계산된 로컬 변수만 참조 (32-07)
- CompoundCenterC/B foot 오버로드 교체: 단일 오버로드→foot 반환 오버로드, 수치 결과 byte-identical, footOk 가드로 FAI-DistLine skip (32-07)
- 4-ROI ArcLineIntersect 설계 채택: 교점1(A1/B1)과 교점2(A2/B2) 평균점 → Datum C X 거리. SOP I9/I10 실무 알고리즘 일치 (Plan 32-08)
- [Phase 34.1-01]: EImageSource enum 단일 신규 파일 + DatumConfig 변경 0 가드 유지 (D-34.1-07)
- [Phase 34.1-01]: UpdateImageSourceBadge(EImageSource) 단일 헬퍼로 자동/수동 swap 3자 동시 전환 일원화 (D-34.1-15)
- [Phase 34.1-01]: PublishDatumRoiCandidates 진입부 isDualImage Visibility 동기화 + 새 노드 진입 가로축 리셋 (D-34.1-08/09)
- [Phase 37-01]: TryRunDatumPhase 두 오버로드 lenient 전환 (D-37-03 datum find 실패 = continue+log, 항상 true 부분성공 / D-37-04 2-image per-datum DualImage 판단 유지). 시그니처 무변경. Logging/ELogType using 추가. msbuild Debug/x64 PASS 0 new warning.
- [Phase 37-02]: EStep.DatumPhase 를 DatumConfigs[0] 단일 분기 → DatumConfigs 전체 per-datum loop 재작성 (D-37-04). InspectionSequence 에 누적 경로 TryRunSingleDatum(Clear 안 함) + ClearDatumTransforms 추가, loop 전 1회 Clear → datum 마다 자기 이미지로 검출하여 _datumTransforms 누적 (D-37-05). datum 부분 실패 = skip+log, FinishAction(Error) 전면 제거 lenient (D-37-03). 로드 헬퍼 datum 인자화 (D-37-02), EStep.Measure 무변경 D-37-06 기존 충족. msbuild Debug/x64 PASS 0 new warning.
- [Phase 37-03]: D-37-07 신규 UI 최소화 — 기존 AddDatumToSequence→inspSeq.AddDatum 반복 + DatumConfig ICustomTypeDescriptor DualImage 시 TeachingImagePath + TeachingImagePath_Vertical 동시 노출로 4-datum DualImage 생성/티칭 가능, InspectionListView.xaml.cs 코드 변경 0. 37-UAT.md SIGNED_OFF (4/4 PASS).
- [Phase 37-03 UAT hotfix A, 1c11c35]: Measurement 노드 이미지/ROI 해석을 FAI 이름 round-trip(FindFAIByName) → 측정 객체 참조(FindFAIContainingMeasurement, ReferenceEquals). 여러 Shot 의 FAI 이름 동일(기본 FAI_0) 시 첫 Shot FAI 반환 → Shot2 측정 선택 시 Shot1 이미지 표시되던 결함. HighlightSelectedRoi anchorFai 해석 동일 수정.
- [Phase 37-03 UAT hotfix B, c6576e5]: FindFAIContainingMeasurement 가 RecipeManager.Shots(동적 FAI 단일 소스, 신규 Shot 즉시 반영) 우선 탐색, pSeq fallback. AddShotToSequence 가 새 Shot 을 RecipeManager 에만 추가 → 라이브 Action(pSeq) 지연 동기화로 세션 중 새 Shot 측정 미추종(재시작 후 정상)하던 결함.
- [Phase 39-01]: 5 신규 인터페이스 — `_failedDatums` HashSet + `MarkDatumFailed`/`IsDatumFailed` (InspectionSequence) + `FAIConfig.WasDatumSkipped` + `MeasurementBase.LastSkipReason`. Phase 37 D-37-03 lenient 회귀 0 (TryGetDatumTransform 시그니처 + L119 Step=Grab + identity fallback 보존). 게이트는 EStep.Measure 안에서만 동작.
- [Phase 39-02]: TCP wire 3-state hierarchy — anyDatumSkip > NG > OK (cycle = O/X/N) + FAIResults[i] = P/F/N. EVisionResultType.NotExist 재사용 (v2.6 enum 추가 0 — D-10 가드). FAIResultData 신규 ctor `(string, EVisionResultType, double)` + 기존 bool ctor 보존 (외부 호출자 회귀 0).
- [Phase 39-03]: HALCON `DETECT FAIL` 적색 라벨 (RenderDatumOverlay) + DatumConfig.LastFindSucceeded INPC + HasDetectFail computed + NodeViewModel switch case. memory feedback_halcon_setcolor_invalid_names 준수 ("red" 표준명).
- [Phase 39 hotfix CO-39-01, 13e735e]: Action_FAIMeasurement 이미지 취득 실패 2 분기에서 `datum.LastFindSucceeded = false` 누락 — TryRunSingleDatum 미호출 경로에서 LastFindSucceeded 이전 상태 유지 → 라벨 조건 미충족. 2 분기에 1줄씩 추가.
- [Phase 39 hotfix CO-39-02, af3f608]: 사전 티칭 안 한 datum (IsConfigured=false) → 라벨 분기 미진입 + RefOrigin=0 → 화면 밖. `DatumConfig.RuntimeDetectFailed` 휘발성 INPC 신규 + 분기 조건 `(RuntimeDetectFailed || (IsConfigured && !LastFindSucceeded))` + 좌표 fallback (50, 50). 4 파일 변경.
- [Phase 39 hotfix CO-39-03, 8a3d2f6]: 사용자 UAT 요청 — 라벨이 Datum origin 위 → 이미지 우상단으로 이동. `GetPart(window)` 동적 좌표 + datum 이름 hash 기반 row stagger (6단계 25px) + 라벨 텍스트에 datum 이름 포함 ("DETECT FAIL: Datum_X").
- Phase 39.3 Plan 01: DualImage RectROI baseline (D-G1) — 4 UI 분기 (isRectRoiType / FindSelectedRectMeasurement / CommitRectRoi / BuildPointRoiDefinitions) 통합. Anti-Goal #1~#3 (DualImageEdgeDistanceMeasurement / MeasurementFactory / Action_FAIMeasurement) 변경 0. _currentImageSource default=Horizontal 의존성으로 Plan 02 미진입 빌드 안전.
- Phase 39.3 Plan 03: DualImage TeachingImagePath_Vertical 에 [InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)] + [AutoUpdateText] 2 attribute + using ReringProject.Device 추가 — ModelFinderViewModel L28-29 패턴 그대로. +3 라인 / 수정 0. Anti-Goal #1~#3 (DatumConfig / MeasurementFactory / Action_FAIMeasurement) 변경 0. msbuild Debug/x64 PASS 0 new warning. Browse 버튼 시각 동작 검증은 Plan 04 UAT 4 (Risk R4 가드 — fallback 4안 SUMMARY 등재).
- Phase 39.3-02: Datum-Measurement 양방향 mutex (PublishMeasurementDualImageSelection set 시 Datum unsubscribe+null / PublishDatumRoiCandidates 진입 시 Measurement null) 로 _selectedDatumForSwap 와 _selectedDualImageMeasurement 동시 non-null 0 보장
- [Phase 39.3 PARTIAL_SIGNED_OFF 2026-05-30]: Test 1-3 PASS (RectROI 활성 / Swap UX wiring / 슬롯 종속 RectROI) + 회귀 C PASS 추정 / Test 4 (Browse 버튼) + 회귀 A/B/D/E NOT_TESTED → Phase 39.4 흡수. CO-39.2-01-01 종결. Anti-Goal 10/10 ✅.
- [Phase 39.3 UAT 발견 결함 → CO-39.3-01]: Shot 이미지는 동일 Shot 의 다른 FAI 와 공유되는 공통 자원. 그러나 Phase 39.3 Horizontal swap 분기 (Plan 02 Task 02-04) 는 ShotConfig 이미지를 DualImage Measurement 의 "가로축 티칭 이미지" 로 단독 점유 → 작업자 인지 혼동. 실 카메라 grab 환경에서 혼동 심화 예상 → Phase 39.4 신설 합의 (DualImage 양측 명시 경로 + 수동 swap UX 재설계, Datum DualImage 패턴 일관화). 39.3 D-G4 anti-goal ("Action_FAIMeasurement 본문 변경 0") 은 39.4 의 새 contract 로 해제.
- [Phase 39.4 discuss 완료 2026-05-30, commit 8f85eba]: 4 결정 lock — (D-G1) Fallback 정책 = ShotConfig fallback (회귀 0, INI 자동 호환, ternary 한 줄) / (D-G2) Datum DualImage 일관화 = 별도 후속 phase 이관 (회귀 표면 격리, 39.4 = Measurement 만 집중) / (D-G3) PropertyGrid 라벨 = [DisplayName("가로축 티칭 이미지")] + [Category("Image|DualImage")] 조합 (PropertyTools 3.1.0 namespace 검증 plan-phase 에서 lock, fallback = [Description] tooltip) / (D-G4) Swap UX 하이라이트 = 배지 라벨에 소스 명시 ("가로축 (Measurement)" vs "가로축 (Shot fallback)"). Plan 구조 estimated 4 plans / 3 waves. 다음 = /gsd-plan-phase 39.4.
- D-G1 fallback 정책: TeachingImagePath_Horizontal 미설정 시 ShotConfig.SimulImagePath fallback (INI 회귀 0)
- D-G3 PropertyGrid 라벨: PropertyTools.DataAnnotations.DisplayName 확정 — 가로축/세로축 한글 라벨 양측 Browse 버튼 노출
- D-G1 fallback 정책: TeachingImagePath_Horizontal 명시+존재 → 명시 경로, 아니면 ShotParam.SimulImagePath (Phase 39.2 baseline 회귀 0)
- RT-4 옵션 A 채택: UpdateImageSourceBadge 안 배지 텍스트 single source of truth (D-G4 — Datum mutex 가드 자동 적용)
- CS0136 회피: hpathMeas 명명 (Datum 분기 hpath 와 outer scope 분리, Phase 39.3 vpathMeas 패턴 mirror)
- CycleResultSerializer를 ReringProject.Sequence 네임스페이스에 배치 (Custom/Sequence/Inspection 폴더 규칙 일치, Phase 40 D-01)
- TypeNameHandling.None 명시 보안 제어 적용 (T-40-02 RCE 방지, cycle.json 역직렬화 시 외부 타입 주입 불가)
- SixLabors.Fonts 1.0.0 채택 (net48) — ClosedXML 0.105.0 + .NET 4.8 에서 2.1.3 netstandard2.0 폴더 부재로 로드 불가. 1.0.0 으로 대체, runtime XLWorkbook PASS
- Microsoft.Bcl.HashCode 미설치 확정 — ClosedXML 0.105.0 전이 의존성에 포함 안 됨(ASSUMED 오판), App.config redirect 추가 불필요(Pitfall 2 미발생)
- [Phase 40.1-01]: overlay 토글 게이트는 RenderNow 표시 시점 분기만 적용 (데이터 무변형) — _measurementOverlayVisible/_datumOverlayVisible 2 플래그 + SetMeasurementOverlayVisible/SetDatumOverlayVisible setter(즉시 Render()). 측정 overlay OFF=빈 List<EdgeInspectionOverlay> 전달(rois/messages/draft 보존), Datum OFF=`_datumConfig != null && _datumOverlayVisible` 게이트. RenderNow 단일 게이트로 라이브+노드 재현 경로 동시 커버. SetInspectionOverlays/UpdateDisplayState/RenderDatumOverlay 본문 무변경.
- [Phase 40.1-01]: #4 Polygon = btn_polygonRoi Visibility=Collapsed 만 (UI 진입점 숨김). RoiShape.Polygon enum + HalconDisplayService Polygon 분기 + PolygonRoiButton_Click/CompletePolygon code-behind 전부 보존 (label_pointCount Phase 17 D-15 선례 동일, INI 데이터 호환).
- [Phase 40.1-01]: MainView.xaml.cs 핸들러는 파일 실제 스타일(K&R)을 따름 — 플랜의 Allman 명시보다 CLAUDE.md "편집 파일 스타일 따름" 우선.
- [Phase 40.1-02]: #3 트리 기본 펼침 = NodeViewModel.ExpandToShotLevel() 신규 — NodeType==Sequence 분기에서 자기만 펼치고 자식(Shot/Datum) IsExpanded=false 후 재귀 중단, 상위(루트)는 펼치고 자식으로 재귀. 라이브 경로 2곳(ListView_Loaded L205, OnLoadRecipe L263)의 ExpandAll → ExpandToShotLevel 교체. IsEditable setter(L168, 편집 모드) ExpandAll 무변경 + 선택/하이라이트 로직(InspectionList_SelectionChanged/HighlightSelectedRoi) 무변경 — 회귀 가드. msbuild Debug/x64 PASS 신규 warning 0 (커밋 b8cbaf6, human-verify 대기).
- [Phase 41-01]: MIL DLL HintPath = 절대 경로 (C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll), halcondotnet 선례 일치, Private=False (MIL 런타임 PC 설치).
- [Phase 41-01]: ECameraType.MIL enum 멤버 추가 — HIK 다음 줄, 기존 Virtual/Basler/HIK 보존. Plan 02 MilCamera.cs 컴파일 foundation.
- MilCamera.IsOpen/CaptureMode/TriggerSource 모두 protected set — 파생에서 직접 set 가능 (VirtualCamera.cs L80/L74/L76 확인)
- MIL MdigGrab 동기 grab → MbufInquire(M_HOST_ADDRESS) → new IntPtr((long)) → GenImage1("byte") 변환 패턴 (Phase 41-02)
- ECameraRole enum을 ReringProject.Setting namespace 에 배치 — Custom/SystemSetting.cs 단일 정의 (Phase 12 D-12 int 백킹 선례 적용)
- RegisterRequiredDevices HIK 3대 고정 → CameraRole(TopBottom/Side) 역할 분기 CXP 1대 등록으로 재구성 (D-03)
- [Phase 41 SIGNED_OFF 2026-06-09]: 실 HW(Matrox **RapixoCXP** 보드 + **VIEWORKS VP-152MX2-M16I0** 카메라, ≈152MP 2-connection) 도착 → 프로그램 단발 grab + 라이브 둘 다 PASS(UAT Test 6). 보드 도착 후로 보류됐던 "실 HW grab 검증" carry-over 종결, HW-02 런타임 VERIFIED. 빌드=Debug|x64(SIMUL_MODE off), MIL run-time 라이선스 → Ctrl+F5 실행(F5 디버깅은 dev 라이선스 필요). MilCamera.Open 은 DCF 없이 M_DEFAULT → 카메라 User Set(TriggerMode Off+Mono8) 의존, CXP 단독점유(Capture Works 종료 필요), non-paged 512MB=단일 Mono8 버퍼엔 충분. SIMUL Test 2~5 = CO-41-01/CO-41-02 사용자 동작확인 근거 PASS. CO-41-03(역할별 다중 카메라 부분 등록)은 다중 카메라 현재 미고려로 **out of scope**(사용자 2026-06-09). UAT 6/6 PASS, status=signed_off.
- [Phase 41 hotfix CO-41-01, a397039]: SIMUL_MODE 앱 기동 크래시(FileNotFoundException: Matrox.MatroxImagingLibrary, Version=10.10.614.1) 수정 — ①DeviceHandler case MIL 을 `#if SIMUL_MODE`→`AddVirtualCamera` 직접 폴백(SIMUL 은 new MilCamera 미컴파일 → Matrox 런타임 미로드, SDK/보드 비의존) ②csproj Matrox 참조 Private=False→True(관리 어셈블리 bin 복사 확인, 실 HW 빌드 런타임 로드 보장; 네이티브 MIL 런타임은 설치 PATH 해석). 컴파일 0 errors 재확인. 근본 원인 = Private=False(bin 미복사) + SIMUL 에서도 new MilCamera JIT 로 어셈블리 로드 시도. Test 2~5 런타임 검증은 사용자 요청으로 보류(나중에 앱 종료→재빌드 후 진행).
- 저장 경로 = ResultSavePath\Image\{yyMMdd}\{HHmm}\original|capture (GetLogSavePath 미사용, Plan 40.2-01 확정)
- 파일명 타이밍 = 동기 결정(BuildFileName) + 비동기 write 분리 (FAIConfig.LastOriginImageFileName write-back 패턴, Plan 40.2-01)
- HalconDisplayService stateful 판정: _isFontInitialized/_normalFontName 인스턴스 필드 + EnsureFontInitialized(window) 확인 → stateful→지역new 분기 채택 (OverlayCaptureRenderer)
- FAI-Edge 측정점 카운트: FAIEdgeMeasurementService BuildOverlaysBoth/Single 에서 FAI-Edge1/2 각각 별도 오버레이(Points 단일점) 확인 → 1점=1오버레이 구조, pointCount++ 방식 (OverlayCaptureRenderer.BuildMeasurePointSegment)
- faiDto 복사 위치: WasDatumSkipped 다음, LastOverlays 앞 — object initializer 순서 유지 (Plan 40.2-03)
- ExcelExportService 파일명 컬럼: FAI 레벨(measurement 루프 내 fai 참조), 같은 FAI 다중 measurement 동일 파일명 반복 표시 정상 (Plan 40.2-03)

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
| 260519-c08 | 2026-05-19 | Phase 23.1 UAT 2차 보충 3건 — #3-refresh Datum Load 후 PropertyGrid 즉시 갱신(RefreshParamEditor) / #6-a ROI 선택 하이라이트(진단 로그로 진짜 원인 확정 = 측정 ROI 복합키 'FAIName_측정명' vs FAIName exact 매칭 실패. 최종 모델: FAI 노드=하이라이트 없음, 측정 노드/결과행=단일 ROI 노란색) / #6-b 라벨 폰트 30% 축소. UAT 4라운드 반복 | 5a2a350, 79e483a, 51c441b, afb03d3, 33212cd, 5e3d2c5, 3c25579 | UAT PASS (사용자 3/3 2026-05-19) |
| 260523-j72 | 2026-05-23 | E3 단축↔장축 revert + 교점 기반 알고리즘 교체 — 32-05 commit 3343250 (단축→장축) 전면 revert + 사용자 reference HALCON 스크립트(LargestRect XLD → get_contour_xld → Edge len 비교 → fit_line_contour_xld('tukey') refined Phi → intersection_contours_xld(measureLine, LargestRect, 'all')) 일치화. VisionAlgorithmService.TryFindShortAxisIntersections 신규 추가, CompoundShortAxisDistanceMeasurement 측정 클래스 단순화(HOperatorSet 직접 호출 0), CrossLen 프로퍼티 신규(기본 500). 오버레이 6개(FAI-LongEdge1/2, FAI-MeasureLine, FAI-Intersection1/2, FAI-DistLine). | b3dd847, c95982d, af07972 | UAT PASS (사용자 approved 2026-05-23) |
| 260526-ilp | 2026-05-26 | CO-31-01 PropertyGrid 양방향 즉시 갱신 — 4 Param Name (DatumName/ShotName/FAIName/MeasurementName) 을 plain auto-property → backing field + RaisePropertyChanged 패턴 교체 + NodeViewModel ctor 에서 Param PropertyChanged 구독 + Node.Name 동기화 핸들러 추가. MeasurementName TypeName 폴백 보존. hotfix 35fe244: ShotName/FAIName [Browsable(false)] → [Category] (UAT 보고 시 PropertyGrid 노출 누락 발견). msbuild Debug/x64 PASS, 신규 warning 0. | daeb195, 66b20bc, 35fe244 | UAT PASS (사용자 4/4 2026-05-26) |
| 260526-kay | 2026-05-26 | EdgeSelection 차단 해제 3군 일괄 — EdgeToLineDistance(Phase 23.1 D-08/D-09 ICustomTypeDescriptor 8 메서드 + "All" 리터럴 제거) + EdgeToLineAngle/ArcEdgeDistance(EdgeSelection 필드 신규 + ItemsSource 노출). strip-loop(stripCount=20) 도입(2026-05-17) 후 First/Last 도 안전하게 라인 피팅 가능한 사실 활용. 기본값 "All" 유지 → INI 회귀 0. msbuild Debug/x64 PASS, 신규 warning 0. | 24b33b9, b33e141, 59ce666 | UAT PASS (사용자 3/3 2026-05-26) |
| 260611-e22 | 2026-06-11 | SettingWindow CameraRole(검사 모드 TopBottom↔Side) 변경 시 경고 확인 대화상자 — 생성자 원본값 보관 + Btn_ok_Click 에서 변경 감지 시 YesNo 경고(enum 이름 + 재시작 안내), 취소 시 원복/창유지, 확인 시 Save + 재시작 안내. 함부로 모드 변경 방지. Debug/x64 PASS, 신규 warning 0. | 06c62b7 | SIMUL UAT 대기 (설정창 경고 노출/원복/재시작안내 육안 확인) |
| 260611-dl | 2026-06-11 | **데이터 손실 버그 수정** — CameraRole 전환 후 레시피 저장 시 비활성 시퀀스 Datum 이 DatumCount=0 으로 덮어써져 영구 소실(Side 저장→Top/Bottom 소실, 대칭). 저장 직전 기존 레시피 read 후 비활성 시퀀스 FIXTURE Datum 보존(PreserveFixtureFromExisting). FAI_1 Top/Bottom 이미 소실(재티칭 필요), Side=4 백업(main.ini.bak_260611_side4). Debug/x64 PASS. | 3faa91b | **UAT PASS (사용자 2026-06-11, Side↔TopBottom 번갈아 전환 시 양방향 Datum 보존 확인)**. 메모리 project_recipe_datum_loss_camerarole |
| 260611-ov | 2026-06-11 | FAI/Shot 선택 시 해당 노드 ROI/Datum 만 표시 — ①FAI 클릭 시 Shot 전체 FAI ROI(CollectShotRois)→선택 FAI ROI+측정위치만(HighlightSelectedRoi FAI 분기). ②Shot Datum 표시 ResolveSequenceDatums(시퀀스 전체 Side=4)→ResolveDatumsForShot(그 Shot DatumRef 집합, 보통 1개). FAI 는 기존 ResolveDatumsForFai 유지. Datum 체크박스 게이트 유지. | 765195c | **UAT PASS (사용자 All Pass 2026-06-11)** |
| 260615-dx7 | 2026-06-15 | Phase 41.1 반복 검사 입력: 고정 이미지 50회 → 이미지 폴더 N장 순회. RepeatRunService.StartFromImages(seq, imagePaths) — 매 사이클 StartAll 직전 recipeManager.Shots SimulImagePath 를 imagePaths[CompletedCount] 로 교체(1사이클=이미지1장, 횟수=폴더 이미지수). _imagePaths null 분기로 기존 고정 Start 보존. ReviewerWindow 버튼→Ookii 폴더 다이얼로그(bmp/jpg/png/tif). msbuild Debug/x64 0 errors. | 18a656e | SIMUL UAT 대기 (폴더 선택→N장 검사→2시트 xlsx 육안 확인) |

### Pending Todos

| ID | Date | Description | Source | Status |
|----|------|-------------|--------|--------|
| CO-22-01 | 2026-05-11 | Datum 노드 ↔ FAI 노드 PropertyGrid 전환 동작 안 됨 — 트리 선택 시 즉시 갱신 안 됨. Phase 17 ICustomTypeDescriptor 와의 상호작용 가능성. 별도 quick task 로 재현/원인 추적 필요. | Phase 22 UAT carry-over | **resolved** (quick 260511-ucv, d6070e8 + 50f5405, UAT 5/5 PASS) |
| CO-40-08 | 2026-06-01 | 오토 모드 종합판정/TCP 응답을 실행 시퀀스로 한정. InspectionSequence.AddResponse / ComputeOverallResult 가 recipeManager.Shots 전체를 순회 → 다른 시퀀스 stale 이 host 응답·cycle 종합판정에 포함될 수 있음. 리뷰어 측정표는 CO-40-07 로 시퀀스별 한정 완료, 종합판정/TCP 만 남음. | Phase 40-03 UAT (사용자 결정 B 이연) | open → 별도 phase/quick |
| CO-41-01 | 2026-06-03 | SIMUL_MODE 앱 기동 시 FileNotFoundException(Matrox.MatroxImagingLibrary) 크래시 — csproj Private=False(DLL bin 미복사) + SIMUL 에서도 new MilCamera JIT. 핫픽스: DeviceHandler MIL case 를 SIMUL 직접 AddVirtualCamera 폴백 + csproj Private=True. | Phase 41 UAT (41-04 Test 2 FAIL) | **resolved** (a397039) · 런타임 PASS (2026-06-09 실 HW 기동 + CO-41-02 SIMUL 동작확인) |
| CO-41-03 | 2026-06-09 | 실 HW 역할별(CameraRole) 다중 카메라 부분 등록 경로 미검증 — Phase 41 은 "CXP 1대 공유"만 실측. | Phase 41 UAT Test 4/6 (sign-off) | **out of scope** — 다중 카메라 현재 미고려(사용자 2026-06-09). 향후 다중 구성 도입 시 재개. |

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

---

Items acknowledged and deferred at **v1.1 milestone close on 2026-05-28** (audit-open: 41 items):

| Category | Item | Status |
|----------|------|--------|
| requirement | WF-01/WF-02 (Phase 24 검사 E2E) | deferred → v1.2 |
| requirement | OUT-01~04 (Phase 25 Export) | deferred → v1.2 |
| requirement | QUAL-01 (헝가리안) | deferred → v1.2 |
| requirement | HW-01/HW-02 (CXP) | deferred → v1.2 (장비 후) |
| carry_over | CO-38-01 픽셀분해능 런타임 단일소스 | open → v1.2 |
| carry_over | CO-38-02/03 시작지연 LoginManager/SequenceHandler | open → v1.2 |
| carry_over | CO-38-04 실HW [STARTUP] 재측정 | open → v1.2 |
| carry_over | CO-23-01 A1~A5 측정값 UI 표시 | open → v1.2 |
| debug | manual-tools-locked-stuck | root_cause_identified |
| debug | phase-19-datumconfig-regression | fix_applied_pending_uat |
| uat_gap | Phase 23/33/35 | partial |
| uat_gap | Phase 32/34 | unknown |
| quick_task | 22 artifacts (260409~260526, missing 파일) | missing (commits in history) |

Note: WF/OUT/HW/QUAL-01 은 v1.2 재편 확정(사용자 2026-05-28). Quick-task 22건은 페어 artifact 없는 과거 commit. UAT partial/unknown 은 후속 phase(34.1/35/36/37)에서 대부분 흡수됨. 전부 v1.2 로 이월.

### Roadmap Evolution

- 2026-04-23: Phase 11 added — datum-teaching-ui-roi (WR-RT-01/03/04 묶음 예정, bugs.md 로드맵 기반)
- 2026-04-26: Phase 14 added — Datum carry-over (Circle 알고리즘 재설계 + Vertical 파라미터 그룹 + ROI 이동 회귀 + CircleTwoHorizontal/VerticalTwoHorizontal 정상화 + out-of-range UX 게이트). Phase 13 UAT 옵션 2 합의(commit d9b5cc8) 후속.
- 2026-04-29: Phase 15 added — HALCON MeasurePos 정합성 (DatumFindingService strip-loop 6 ROI + Circle polar).
- 2026-04-29: Phase 16 added — datum-circle-strip-redesign-algorithmtype-binding-fix (Phase 15 UAT carry-over).
- 2026-04-30: Phase 17 added — Datum 티칭/검증 UX 재설계 + Circle strip 1개 표시 + Test Find DetectedOrigin + 좌표 hover (Phase 16 UAT carry-over 16항목).
- 2026-05-04: v1.1 milestone started — Phases 18-26 defined (ROADMAP.md created).
- 2026-05-17: Phase 23.1 inserted after Phase 23: EdgeToLineDistance ROI 티칭 배선 + 다점 치수 지원 (URGENT) — SOP 도면 갭 대응 (ROI 측정별 티칭 미배선 + 다점 치수 P1/P2 미지원 + EdgeSelection 마이그레이션 + Datum 실측검증 + CO-23-01 재검증)
- 2026-05-19: Phase 31 added — Datum 기준 측정 알고리즘 확장 (E8 원중심→Datum거리 / D1 Datum 각도 / I9·I10 호∩라인 교점 / E2·E9·E10 CompoundAngle / ArcEdgeDistance). Phase 23.1 carry-over CO-23.1-01·02 흡수. ※ gsd-sdk phase.add CLI phase_number 오산정 → 수동 보정 (31).
- 2026-05-21: Phase 32 added — 측정 알고리즘 SOP 재정합. Phase 31 UAT 중 I9/I10/E2/E9/E10 알고리즘이 SOP 실무 방식과 불일치 확인 → ArcLineIntersect 2직선 교점 + E2/E3/E9/E10 공통 컨투어 알고리즘으로 재작성, E3 신규 타입 추가. Phase 31 UAT Test 3·4·5 이관. ※ gsd-sdk phase.add CLI phase_number 다시 오산정(1) → 수동 보정 (32).
- 2026-05-28: Phase 36 added — Datum DualImage 설계 보강. Phase 34.1 실측 페어 UAT 도중 CO-34.1-08 hotfix (BtnTestFindDatum_Click DualImage 분기 누락, 61d407a) + CO-34.1-09 신규 carry-over (좌표계 통합 부재 + 각도 검증 UX 부재 — IntersectionLl 이 두 이미지 픽셀 좌표를 변환 없이 같은 평면 처리). 스코프: 좌표계 anchor/offset + ExpectedAngleDeg/AngleTolerance + Test Find 시각화 강화. CO-34.1-01/02/09 흡수. ※ gsd-sdk phase.add CLI phase_number 또 오산정(1) → 수동 보정 (36).
- 2026-06-10: Phase 40.2 inserted after Phase 40: FAI별 측정 캡쳐 이미지 저장 + 엑셀 파일명 2컬럼 (URGENT). Phase 40 OUT-02 UAT 후속 — 검사 시점 FAI별로 원본+측정 오버레이 캡쳐 이미지를 PNG 저장(Result\Image\{YYMMDD}\{HHMM}\original|capture\), 엑셀은 하이퍼링크(3385f7c) 제거하고 원본/캡쳐 파일명 텍스트 2컬럼으로 교체. 현재 오버레이는 벡터로만 저장됨 → 헤드리스 HALCON 버퍼 윈도우 렌더+dump 필요.
- 2026-05-30: Phase 39.4 added — Bottom DualImage 수동 swap UX 재설계. Phase 39.3 PARTIAL_SIGNED_OFF 후 carry-over CO-39.3-01 흡수 — Shot 이미지 공통 자원이 DualImage 의 "가로축 티칭 이미지" 로 단독 점유되어 작업자 인지 혼동. 스코프: TeachingImagePath_Horizontal 신규 필드 + Action_FAIMeasurement.TryGrabOrLoadFaiDualImages 분기 교체 (RuntimeImageA 소스 변경, fallback ShotConfig) + MainView.BtnSwapHorizontal_Click Measurement 분기 교체 + Datum DualImage 패턴 일관화. 39.3 D-G4 anti-goal 은 39.4 의 새 contract 로 해제. status=seed (CONTEXT.md 작성, discuss-phase 대기).
- 2026-06-01: Phase 40.1 inserted after Phase 40 (URGENT) — 리뷰어/뷰어 UAT 후속 UI 3건: (2) 이미지 뷰어 overlay On/Off 토글(측정위치+Datum 수평/수직 라인) (3) 트리 기본 Shot만 표시(접기)+펼치기 (4) Polygon ROI 전부 숨김. Phase 40 UAT 중 발견. 별도 긴급 #1(bottom-shot-stale-roi)은 debug 로 선처리·커밋(01332c3). 다음 = /gsd-plan-phase 40.1.
- 2026-05-31: Phase 39.4 PARTIAL_SIGNED_OFF — Test 1~4 + Verify A 5/5 PASS (UAT mid-hotfix CO-39.4-01 = `6843c0d`, UpdateImageSourceBadge 의 RenderInspectionResultForNode 가 swap 직후 Shot 이미지로 덮어씌우는 회귀 fix). CO-39.3-01 종결. CO-39.4-02 carry-over (Verify B/D/E + INI 호환 회귀 smoke = 회귀 위험 LOW, 후속 phase 이월).
- 2026-06-02: Phase 40.1 SIGNED_OFF (4/4 PASS) + CO-40.1-01(c41a418 Datum 선택 자동재티칭) + CO-40.1-02(18a956b 측정/Shot 노드 Datum 기준선 표시).
- 2026-06-02: Phase 41 added — CXP 카메라 MIL Lite 10.0 grab 드라이버 통합 (HW-01/HW-02). MILESTONES.md "v1.2 Hardware Integration" 이연 백로그(구 Phase 29 SDK확정 + 30 드라이버통합) 활성화. HW 실물 도착 + SDK MIL Lite 10.0(PC 설치) 확정. VirtualCamera GrabHalconImage 통합. 다음 = /gsd-discuss-phase 41.

## Session Continuity

Last session: --stopped-at
Stopped at: Phase 42 context gathered
Resume file: --resume-file
Next action: Phase 41.1 UAT (Plan 41.1-03 checkpoint:human-verify) — msbuild Rebuild 확인 → 앱 실행(SIMUL_MODE) → 50회 반복 실행 → 반복도 엑셀 export → Sheet1/Sheet2 내용 확인. 완료 후 SUMMARY.md 3종 작성 + 페이즈 완료 처리.

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

**Completed Phase:** 34 (Datum VerticalTwoHorizontal 듀얼 티칭 이미지) — 4 plans — partial signed_off 2026-05-27T05:00:00Z (Test 1+5 PASS · Test 3 PARTIAL · Test 2/4 PENDING → Phase 34.1 일괄)

**Planned Phase:** 40.2 (FAI별 측정 캡쳐 이미지 저장 + 엑셀 파일명 2컬럼) — 4 plans — 2026-06-10T02:34:48.005Z
