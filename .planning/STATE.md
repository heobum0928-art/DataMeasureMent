---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: Phases
status: unknown
stopped_at: Phase 67 context gathered
last_updated: "2026-07-07T01:09:44.535Z"
last_activity: "2026-07-02 - Quick task 260702-lx0: Action_FAIMeasurement.cs EStep.Measure Extract Method 리팩토링 (코드 완료, SIMUL $TEST 회귀 확인 대기)"
progress:
  total_phases: 15
  completed_phases: 14
  total_plans: 49
  completed_plans: 46
  percent: 94
---

> **v1.2 는 닫지 않음 (열어둔 채 병행).** v1.2 carry-over: Phase 41 HW UAT 중단 · Phase 51 Wave 2 (일괄검사 UI) · Phase 52(레벨링 폐기) · Phase 53 캘리브 육안 UAT pending. v1.3 와 독립적으로 추후 재개 가능.

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-04 for v1.1)

**Core value:** Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 + Datum 자동 보정 수행
**Current focus:** Phase 66 — ring7-coax-align-2026-06-26

## Current Position

Phase: 66
Plan: Not started
Last activity: 2026-07-02 - Quick task 260702-lx0: Action_FAIMeasurement.cs EStep.Measure Extract Method 리팩토링 (코드 완료, SIMUL $TEST 회귀 확인 대기)

**Phase 61.1 hotfix F4 (2026-06-25, commit 316497b):** 2차 실측서 Align 검출 에지 polyline 이 패턴1 끝점→패턴2 시작점을 대각선으로 잘못 연결하는 버그 발견. 점 추출/polyline 방식 폐기, AlignShapeMatchService.Run 이 두 패턴 contour 를 affine_trans_contour_xld + concat_obj 로 단일 XLD 생성 → AlignResult.DetectedContourXld(HObject, 소유권 뷰어 이전) → MainResultViewerControl.SetAlignContourXld(교체/clear/Dispose 시 HObject.Dispose, 에지 토글 게이트) → HalconDisplayService.RenderAlignContourXld(window.DispObj). EdgeContourRows/Cols/BuildEdgeOverlays/AlignEdge polyline 분기 전부 제거. 빌드 Debug/x64 PASS, 검사(MainView) 회귀 0. UAT Test 2 재실측 대기(재티칭 후 ROI 크기 + 대각선 無 확인).

**사용자 작업 계획 2026-06-23 (C→B→A):**

  - C 정리: ✅ .claude worktrees 8.8GB 삭제 / 죽은코드 10파일 삭제 / 레벨링(Phase 52) 폐기 / Phase 49·51 완료처리 (진행 중)
  - B QUAL-01 리팩토링: 헝가리언+if/else+함수분할+가독성(신입 이해 가능) — 측정경로 광범위, 파일/모듈 단위 분할 phase 필요 (미시작)
  - A Phase 53 캘리브레이션: 텔레센트릭 체커보드 mm/px → ShotConfig.PixelResolution. 레퍼런스(QCellInspector CCalibration)는 CCTV 와핑이라 그대로 X, mm/px만 차용. 실 체커보드 이미지 없음(인터넷 다운로드본만) → 왜곡검증 보류 (미시작)

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

- Total plans completed: 53
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
| 48 | 4 | - | - |
| 49 | 3 | - | - |
| 51 | 2 | - | - |
| 53 | 3 | - | - |
| 58 | 3 | - | - |
| 61.1 | 4 | - | - |
| 65 | 4 | - | - |
| 66 | 3 | - | - |

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
| Phase 43-startup-delay-separation P43-01 | 45 | 4 tasks | 3 files |
| Phase 43.1-startup-perceived-speed P43.1-01 | — | 3/4 tasks (Task 4 checkpoint pending) | 4 files |
| Phase 52 P01 | 8 | 2 tasks | 3 files |
| Phase 52 P02 | 5 | 2 tasks | 2 files |
| Phase 52 P03 | 9 | 2 tasks | 2 files |
| Phase 57-pattern-roi-ux-datum-align-hardening P01 | 10 | 3 tasks | 7 files |
| Phase 57-pattern-roi-ux-datum-align-hardening P03 | 1 | 2 tasks | 1 files |
| Phase 57-pattern-roi-ux-datum-align-hardening P04 | 8 | 3 tasks | 2 files |
| Phase 57-pattern-roi-ux-datum-align-hardening P02 | 15 | 3 tasks | 3 files |
| Phase 57-pattern-roi-ux-datum-align-hardening P05 | 6 | 3 tasks | 3 files |
| Phase 57.1-pattern-roi-verification-safety P01 | 6 | 1 tasks | 1 files |
| Phase 57.1-pattern-roi-verification-safety P02 | 8 | 2 tasks | 1 files |
| Phase 57.1-pattern-roi-verification-safety P03 | 6 | 2 tasks | 2 files |
| Phase 57.1 P57.1-04 | 4 | 1 tasks | 1 files |
| Phase 57.1 P57.1-05 | 5 | 1 tasks | 1 files |
| Phase 57.1 P57.1-06 | 5 | 1 tasks | 1 files |
| Phase 57.1 P57.1-07 | 4 | 2 tasks | 2 files |
| Phase 57.1 P57.1-08 | 4 | 1 tasks | 1 files |
| Phase 57.1 P57.1-09 | 5 | 3 tasks | 4 files |
| Phase 57.1 P57.1-10 | 12 | 2 tasks | 13 files |
| Phase 57.1 P57.1-11 | 8 | 1 tasks | 1 files |
| Phase 48-protocol-v1-test-result-site-material P01 | 30 | 2 tasks | 3 files |
| Phase 48 P02 | 20 | 2 tasks | 3 files |
| Phase 48 P03 | 168 | 1 tasks | 1 files |
| Phase 48-protocol-v1-test-result-site-material P04 | 25 | 3 tasks | 6 files |
| Phase 49 P01 | 3 | 3 tasks | 3 files |
| Phase 49 P03 | 5 | 1 tasks | 1 files |
| Phase 49 P02 | 3 | 2 tasks | 1 files |
| Phase 53 P01 | 5 | 3 tasks | 2 files |
| Phase 53 P02 | 6 | 3 tasks | 3 files |
| Phase 53 P03 | 9 | 2 tasks | 2 files |
| Phase 58-config-camera-a-2026-06-23 P01 | 10 | 2 tasks | 3 files |
| Phase 58-config-camera-a-2026-06-23 P02 | 5 | 2 tasks | 2 files |
| Phase 58-config-camera-a-2026-06-23 P03 | 4 | 3 tasks | 4 files |
| Phase 59-vision-algorithm-b-2026-06-23 P01 | 5 | 1 tasks | 3 files |
| Phase 59-vision-algorithm-b-2026-06-23 P02 | 4 | 2 tasks | 2 files |
| Phase 59-vision-algorithm-b-2026-06-23 P03 | 10 | 3 tasks | 1 files |
| Phase 63 P01 | 8 | 2 tasks | 1 files |
| Phase 63 P02 | 6 | 2 tasks | 1 files |
| Phase 63 P03 | 7 | 2 tasks | 1 files |
| Phase 63 P04 | 8 | 2 tasks | 3 files |
| Phase 63 P05 | 8 | 2 tasks | 1 files |
| Phase 60-calibration-bottom-c-2026-06-23 P02 | 15 | 1 tasks | 1 files |
| Phase 61-ui-tabcontrol-d-2026-06-23 P01 | 149 | 2 tasks | 2 files |
| Phase 61-ui-tabcontrol-d-2026-06-23 P02 | 210 | 2 tasks | 2 files |
| Phase 61 P03 | 2 | 3 tasks | 4 files |
| Phase 61.1-align-offline-loader-result-viz-2026-06-25 P01 | 15 | 2 tasks | 2 files |
| Phase 61.1-align-offline-loader-result-viz-2026-06-25 P02 | 25 | 2 tasks | 4 files |
| Phase 61.1 P03 | 20 | 2 tasks | 4 files |
| Phase 61.1 P04 | 20min | 2 tasks | 2 files |
| Phase 65-bottom-4jig-face-align-2026-06-25 P01 | 15 | 2 tasks | 3 files |
| Phase 65-bottom-4jig-face-align-2026-06-25 P02 | 4 | 2 tasks | 2 files |
| Phase 65 P03 | 10 | 1 tasks | 1 files |
| Phase 65 P04 | 10 | 1/2 tasks (Task 1 PASS, Task 2 UAT 대기) | 1 files |
| Phase 66-ring7-coax-align-2026-06-26 P01 | 10 | 4 tasks | 2 files |
| Phase 66 P02 | 8 | 4 tasks | 3 files |
| Phase 66-ring7-coax-align-2026-06-26 P66-03 | 15 | 3 tasks | 4 files |

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
- D-03(43-01): LoginManager 생성자 동기 Load() 제거 → Preload() 백그라운드 thread 기동 (RawImageSaveService 패턴 차용, Load() 본문 무수정)
- D-01/D-02(43-01): [STARTUP] READY = Step 7 CollectRecipe 직후 단일 기준 지표 — Before/After 30% 비교용. 실측 55% 단축 (578ms avg vs 1285ms Before)
- CO-43-01(43-01): 18~20s 흰 화면은 Initialize() 외부(cold JIT + native DLL 로딩 + XAML inflation, MainWindow.ctor 내 Show() 이전) — 별도 신규 phase 에서 해결 결정
- [Phase 52-01]: LevelingEnabled = FIXTURE 섹션 수동 키 직렬화(1/0). InspectionSequence 가 ParamBase 미상속이라 자동 직렬화 불가 → DisplayName/DatumCount 패턴 수동 키. IsLevelingReference 는 DatumConfig ParamBase reflection 자동 직렬화(_DATUM_{d}) — Save/Load 코드 0.
- [Phase 52-01]: 레벨링 각도 캐시(_levelingAngleRad/_levelingComputed + Set/Reset/getter)는 interface-first 멤버 정의+리셋만 제공. 실제 1회 산출은 Plan 02, 소비는 Plan 03. ClearDatumTransforms 에서 ResetLeveling 호출로 datum transform 동일 lifecycle.
- [Phase 52-01]: 신규 INI bool 토글 회귀 0 패턴 — Save ?1:0 / Load .ToBool() 키 미존재 false 폴백. CameraRole 비활성 시퀀스 보존 = PreserveFixtureFromExisting 섹션 통째 복사로 자동(추가 코드 0), 신규레시피 분기만 기본값 명시.
- [Phase 52-02] 변환경로 1순위 확정: HHomMat2D.HomMat2dRotate + HImage.AffineTransImage 인스턴스 메서드 — msbuild Debug/x64 PASS, fallback 미사용 (변환경로 1개만 잔존). TryGetLevelingAngle = TryFindVerticalTwoHorizontal 수평 피팅 구간만 재사용 (Math.Atan2 각도, IntersectionLl/hom_mat2d/DetectedOrigin 제외). 둘 다 실패/근사0 시 무회전·false 폴백 throw 0.
- [Phase 52-03] TryComputeLevelingAngle = LevelingComputed 캐시 가드 우선 (시퀀스당 1회, D-03), 기준 미지정/실패 false+0.0 무회전 폴백(lenient)
- [Phase 57-02]: TryComposeAlign 4-arg → 5-arg(HImage refImageVertical) 위임 오버로드 — 단일이미지 호출처 무변경(회귀 0). ④단계 검출이 DualImage datum + refImageVertical!=null 이면 2-image TryFindDatum, 그 외 1-image. 단일 DatumFindingService 인스턴스에 AlignPreTransform 1회 set → 가로(TryExtractEdgePoints)/세로(TryFindLine) 두 검출 모두 동일 alignRigid 소비 (D-01 단일 공유 transform).
- [Phase 57-02]: TryFindLine 에 TryExtractEdgePoints 와 동형의 AlignPreTransform 소비 이식 — sanity clamp 직후 roiRow/Col 이동 + alignRot 추출, bbox 를 alignRot 반영 enlarged AABB 로 교체. AppendEdgePointsFromStrip 이미 alignRot optional 인자 보유 → byte-identical 미러, alignRot=0 시 회귀 0.
- [Phase 57-02]: #5 lenient 는 이미 구현(EStep.DatumPhase 실패 분기 abort 0, 종료 무조건 Step=Grab, Measure 루프 IsDatumFailed → ClearResult+ALIGN_FAIL/DATUM_FAIL+continue). DualImage 분기에 MarkAlignFailed 추가만, 코드 변경 없이 검증 — abort 0 확인.
- [Phase 52-03] EStep.Level 을 MoveZ-DatumPhase 사이 삽입 — 회전 이미지가 Datum 검출+측정 둘 다 입력. DatumPhase(1-image+DualImage)·Grab 동일 -LevelingAngleRad 동일 회전중심 적용(좌표계 정합), taught ROI 미변환(방식 a), off/미산출 pass-through 회귀 0
- [Phase 57-01]: leveling 완전 제거 — EStep.Level/LevelingEnabled/TryComputeLevelingAngle/TryGetLevelingAngle(2 오버로드)/IsLevelingReference/INI 키 3곳 전수 제거. MoveZ→DatumPhase 직결. 옛 INI stale 키는 ParamBase.Load 무시(D-14). ALIGN(AlignPreTransform/DualImage) 무손상. 빌드 PASS 신규 warning 0.
- [Phase 57-05]: #2 패턴 ROI 표시/숨김 토글 — SetDatumOverlayVisible 미러. MainResultViewerControl 에 `_patternRoiOverlayVisible` 게이트 + `SetPatternRoiOverlayVisible` setter + RenderNow 게이트(_resultDatumOverlays 순회 → PatternRoi/PatternRoi2 Length1/2>0 sentinel → double[]{row,col,phi,l1,l2} → RenderResultRoiBoxes "cyan",2). MainView 에 chk_overlayPattern 체크박스 + Chk_overlayPattern_Changed 핸들러(측정/Datum 토글 옆). 패턴=cyan, datum=orange, 측정=green, 기준선=slate blue 구분(D-15). RenderResultRoiBoxes 시그니처 IList<double[]> 무변경. 빌드 Debug/x64 PASS 신규 warning 0.
- [Phase 57.1-01]: D-03/D-01 — InspectionList_SelectionChanged Datum 분기(ENodeType.Datum)에 ShowResultDatumOverlays(단일-원소 List<DatumConfig>) 호출 추가(RestoreDatumOverlayFromTeach 직후, force-rebind 전). _resultDatumOverlays 채워져 MainResultViewerControl cyan 패턴 ROI 게이트(:846) 충족 → Datum 노드 단독 선택서도 cyan 보정 ROI 가시(Top/Side/Bottom 무차별). ShowResultDatumOverlays 헬퍼 재사용(신규 경로 미도입), SetDatumOverlay 편집 채널 무수정(공존), MainResultViewerControl/MainView/HalconDisplayService 무수정 — 렌더 게이트 이미 정상, 입력만 안정화. 빌드 Debug/x64 PASS 0 errors 신규 warning 0. 회귀 0(Measurement/FAI/Action 분기 동일 경로). SIMUL UAT 대기.
- [Phase 57.1-02]: D-02 진단 산출물 — [ALIGN-ROI] Trace 로그(transform 분기 try 내부, fai.FAIName+ROI_Phi/rotAngle/roiPhi deg) + length1=phi방향 반장축/length2=수직 규약 주석. 측정 산출(AffineTransPoint2d/AABB/MeasurePos) 무변경, length swap 0건 (D-02 LOCKED). msbuild PASS 신규 warning 0.
- [Phase 57.1-03]: 패턴 버튼 안전장치 D-04 — 비-Datum 클릭 가드 메시지 3종 통일("Datum 티칭 존을 먼저 선택하세요"), 비활성화 경로(default-disable→Datum 분기만 재활성) grep 회귀 확증. Teach Datum :2321 무변경. 코드 동작 무변경(메시지 본문+주석만).
- [Phase 57.1-07]: 측정 보정 ROI 표시 박스 length1/length2 를 HALCON disp_rectangle2 규약(length1=열/가로, length2=행/세로 = cyan 패턴 ROI 동일)에 맞춰 교정 — 측정(FAIEdgeMeasurementService SmallestRectangle2+measurePhi) 무변경, 표시만 90° 정상화
- [Phase 57.1-07]: cyan 패턴 ROI center 를 CurrentTransform 으로 AffineTransPoint2d 변환 + phi += Atan2(-t[1],t[0]) — datum/측정 ROI 동일 규약, transform 무효 시 공칭 폴백(회귀 0)
- [Phase 57.1-08]: TryFitLine EdgeTrimCount 의미를 '개수'→'양끝 각 %(0~49 clamp)' 로 재해석 + 위치축 정렬(scanHorizontal→row,else→col, TupleSortIndex+TupleSelect) 후 양끝 removeEach=(int)(edgeCount*pct/100) 절사. 가드 edgeCount>=4 + (edgeCount-2*removeEach)>=2 로 들쭉날쭉 해소. 정렬/절사 allRows/allCols 가 FitLineContourXld+collectedEdges 로 전달(overlay 마젠타 절사 후 반영).
- [Phase 57.1-10]: trim 의미 변경(양끝 각 %, 57.1-08/09)에 맞춰 측정 13 + Datum 6 trim 필드에 [DisplayName("...Edge Trim (%)")] 만 추가 — PropertyTools 라벨 override 는 ParamBase reflection/Newtonsoft 직렬화(프로퍼티명 기준)에 무영향이라 INI 하위호환 완전 보존. DualImage 만 ROI 구분 'Point/Line Edge Trim (%)' 차등. 빌드 0 errors 신규 warning 0.
- Phase 48-01: v1.0 분기 TestType=0(Default) 유지 — ResourceMap Inspection 흐름 정상. Site 매핑은 Wave 2 Plan 02 PcRole 기반으로 처리
- Phase 48-01: sentinel SENTINEL_NO_MATERIAL=-1 채택 (자재번호 미수신 표준값, Wave 2 Plan 04 전파 체인 >= 0 조건으로 유효값 판별)
- Phase 48-01: AfterLoad() partial 후크 패턴 — base Load() 끝에서 호출, Custom 구현 없으면 컴파일러가 제거 (null 안전)
- ESite 슬롯 재사용 설계: framework ResourceMap Dictionary<ESite> 강결합으로 ESiteV1 신규 enum 불가 → ESite.Top=Site1/ESite.Side=Site2 슬롯 재사용, 자원은 PcRole 런타임 결정 (Phase 48-02)
- TcpServer 생성자에서 포트 결정 분기: base 생성자가 mListener.Start()를 즉시 실행하므로 VisionServer 생성자 본문(base 이후)에서 포트 변경 불가 → TcpServer 생성자 내 bUseV1 분기로 ServerPortV1(7701) vs ServerPort(2505) 결정 (Phase 48-02)
- UseProtocolV1=true 분기에서 BuildResultMessageV1 직렬화 — v1.0 3단 구분자(;/,/=) + IsBuffer 최우선 B 판정 매핑 (Phase 49가 채울 자리)
- 자재번호 파일명 위치: FAI 뒤 seg 앞 (_M{번호} 토큰) — 자재번호가 FAI 와 가깝게 식별되도록 우선 배치 (48-04)
- xlsx 테이블 헤더 오프셋 5→6: 자재번호 행(행 4) 삽입으로 1행 이동, hr 변수로 데이터 루프 자동 이동 (48-04)
- [Phase 49-01]: ECycleResult { Buffer, Pass, Fail } enum 신설 (D-07) — CycleState 라이프사이클 enum 미도입, 상태는 InspectionSequence 멤버 bool 로 표현
- [Phase 49-01]: ShotConfig.ZIndex (default 0) z_index↔Shot 매핑 (D-01) — ParamBase 자동직렬화, 누락키 0 폴백=의도된 안전값(Datum/Idx0)
- [Phase 49-01]: 사이클 상태 = InspectionSequence 멤버 4개 + ComputeLastZIndex(레시피 z_index 최댓값, 시퀀스 소유 Shot 한정, D-03) + ResetCycleState(Index 0 수신 리셋, D-08). 정의만 — 소비는 49-02. CS0414 #pragma 일시억제(Rule 3)
- [Phase 49-03]: TcpServer.EncodingType/ApplyEncoding static→instance (CO-48-01/D-09) — 중첩 클래스 ConvertMessage 2곳 Parent.EncodingType 한정, VisionServer 무변경, 인코딩 동작 회귀 0. CO-48-01 종결.
- [Phase 53-01]: CheckerboardCalibrationService — saddle_points_sub_pix 코너 검출 + round(axis/pitch) 격자 버킷팅 + median 간격으로 mm/px 직접 산출(D-07). MmPerPixel 단일 적용/X·Y 리포트(D-02). 중앙↔외곽 편차% + IsDistortionWarn 게이트(D-05). caltab/undistort 미도입(D-07/D-08). 계약(CalibrationResult 7프로퍼티 + TryCalibrate 2오버로드)을 plan 02 CalibrationWindow 가 소비.
- 53-02: CalibrationWindow 검출 입력 = CalibrationViewer.CurrentImage 재사용 (별도 _currentImage 미생성, 중복 HImage·이중 dispose 회피)
- 53-02: 캘리브 결과 외부 노출 = LastResult 프로퍼티 + ApplyRequested 이벤트 (반영/저장 wiring 은 plan 03)
- EEthernetVisionMode namespace = ReringProject.Setting (same as SystemSetting, no extra using needed)
- int-backing property pattern (EthernetVisionModeValue): INI reflection Load switch has no enum case
- RestoreEthernetVisionDefault() only guards PixelResolution (None=0 and empty IP are acceptable missing-key results)
- EthernetAlignCamera: HikCamera composed as private field (no inheritance, no DeviceHandler registration) per D-01; all public methods try-catch isolated; Grab falls back to D:\align_test.bmp
- EthernetVisionHandler placed in namespace ReringProject (top-level); Initialize() mode-gate returns immediately for None mode; Camera stays null in None mode; SystemHandler insertion after all Grabber Steps 1-8 (after [SYSTEM] Initialized log)
- AlignResult (D-05) and AlignRefPose (D-04) placed in namespace ReringProject root (top-level) for zero-import access from Plan 02/03 EthernetVision layer
- AlignRefPose.Engine bare { get; set; } — no field initializer; Plan 02 always sets it before Newtonsoft.Json serialize
- AlignShapeMatchService composes PatternMatchService (_matcher field, D-01) — no reimplementation, no modification of PatternMatchService.cs
- Full-image TryFindPose: roiRow/Col=0, len=99999, marginPx=0, downsample=1.0 — TryFindPose clamps to image bounds for whole-frame search
- ThetaDeg = curAngleDeg - refAngleDeg (degrees minus degrees, no rad re-conversion — PatternMatchService already returns degrees)
- EthernetVisionHandler.Matcher = stateless AlignShapeMatchService, Initialize() try 블록 최상단 생성 + catch null 가드로 모든 경로(None/connected/fail/exception) non-null 보장 (D-02)
- [Phase 63-01]: V1 TEST Type 필드 삽입(TEST_FIELD_TYPE=1) → 자재번호/z_index +1 시프트 V1 한정, V26 파서 dataList[0/1/2] 무변경(회귀 0). ALIGN_TEST/ALIGN_CALIB 수신 통합 — 페이로드=target 토큰 단일필드 가정(Phase 62 모델 미확정).
- [Phase 63-02]: TestResultPacket.Type echo 는 BuildResultMessageV1(V1) 한정 + Type 빈값 ;; 자리 보존 (count/판정 인덱스 어긋남 방지); Align 응답은 가변 List<AlignResultItem> 로 Tray(2)/Bottom(3) 수용, v2.6 Test 블록 무변경(회귀 0)
- TOP→ESite.Top, BOTTOM→ESite.Side(PC1 Side슬롯=BOTTOM 자원), SIDE_*→ESite.Top(PC2 양 슬롯 동일 SIDE)
- Type 미인식/빈값 → false → ResolveSiteSlot(Site) 기존 폴백 보존 (T-63-10 회귀 0)
- Type echo 3곳 = 객체 초기화자 1줄 추가 (AddResponse/BuildDatumShotResponse/BuildScopedResponse), 집계 로직 무변경
- ProcessAlignTest/ProcessAlignCalib = Phase 62 미확정 → ack 골격(IsPass=true 고정), 실 측정 연계는 Phase 62 확정 시
- AlignCalibPacket(응답측) → AlignCalibResultPacket 개명 — 수신측 동명 충돌 방지 (Rule 3)
- Phase 63 Plan 05: 빌드 검증 전용 plan — AlignCalibResultPacket 개명은 Plan 04(b76af74)에서 이미 완료. 추가 코드 변경 없음. MSB3884 경고 1개는 Phase 49 baseline 기존
- D-04(Phase60): PickerCenterRow/Col stored in [ETHERNET_VISION] INI as machine-level HW cal result; default 0.0 = uncalibrated sentinel. RestorePickerCenterDefault() is a no-op guard documenting intent.
- D-05 (Phase 60-02): Bottom align correction expressed as HomMat2dRotate about calibrated picker center; uncalibrated (0,0) returns input offset unchanged (Phase 59 fallback)
- TrayVisionView 2-ROI 슬롯: DrawRoi1→StartRectangleDrawing(슬롯1), DrawRoi2→CommitActiveRectangle(슬롯1확정)+슬롯2시작, Teach→CommitActiveRectangle(슬롯2확정)
- TrayVisionView Live 스트림: Camera.Live() 호출만, 실제 프레임 push 루프는 Camera 내부 위임
- BottomVisionView AttachSharedViewer: CircleDrawingCompleted -= then += 중복구독 방지
- BottomVisionView CalAddStepButton_Click: LoadImage before Dispose (LoadImage clones internally)
- Phase 61-03: MainView 재부모화만(Grid→TabItem), x:Name 보존 — MainWindow.xaml.cs 참조 무손상, 코드 0줄 수정
- Phase 61-03: D-03 단일 공유 MainResultViewerControl — EthernetVisionMode 배타로 align 탭 한 번에 하나만 실재, 다중 HWindowControlWPF 회피
- AlignResult 확장 방식 = 필드 추가(out 파라미터 없음) — 하위호환 최대화, Plan 62 TCP 소비 회귀 0
- TryExtractDetectedContour stride 다운샘플(EDGE_CONTOUR_MAX_POINTS=400) — T-61.1-01 메모리 폭주 차단
- 로더 폴더 마지막 위치 = static _lastImageFolder (탭 전환/뷰 재생성에도 유지)
- LOADER_IMAGE_EXTS const = .bmp;.png;.jpg;.jpeg;.tif;.tiff 확장자 화이트리스트
- 검출 십자를 두 패턴 midpoint 단일 DatumConfig 객체로 구현(SetDatumFindResultOverlay 소비)
- SetResultRoiOverlays(null, datumRects): ROI/에지 체크박스가 독립 게이트(datum/_measurement) 토글
- Phase 61.1-04: DatumConfig(this) 빌드 에러 — Plan 03 생성자 인수 누락, 허용파일 내 수정(anti-goal 0변경)
- EBottomAlignSlot TryTeach slot 파라미터: out+기본값 혼합 불가 → 2-오버로드 분리 (기존 호출자 하위호환 보장)
- BuildJsonPath: Replace 전체 치환 방지 → EndsWith 마지막 _1 제거 (2D_SIDE_1 토큰 내부 오치환 방지)
- ProcessAlignTest BOTTOM 경로: stub(echo) → 실측 grab+Matcher.Run(Bottom,slot)+pose 채움. TRAY 회귀 0, AlignFace OOB=NG 안전 거부(T-65-01)
- Phase 66 Plan 01: Ring7Light_Enabled/Brightness 프로퍼티 추가(D-01), Ring7→LIGHT_RING7 점등 매핑(D-02), CoaxLight_* [Browsable(false)] 숨김(D-03), 점등/소등 TurnOffShotLights 대칭
- TrySaveCoax load-merge-save: 기존 TrySaveRefPose 시그니처 무변경, 별도 public API로 동축값만 갱신(티칭 임계경로 회귀 0)
- partial class 파일별 using 독립: Custom/SystemHandler.cs에 using ReringProject.Device 별도 명시 필요
- Phase 66 Plan 03: airspace-safe WPF 동축 컨트롤(좌측 패널)으로 Bottom/Tray Align 창 — HALCON HWND 위 오버레이 없음
- Phase 66 Plan 03: D-07 티칭=런타임 조명 일치 — Grab/Teach/Run 직전 ApplyCoaxLight() 공통 자동 적용 패턴 확립

### Quick Tasks Completed

| ID | Date | Description | Commits | Status |
|----|------|-------------|---------|--------|
| 260629-eti | 2026-06-29 | $RESULT TCP 응답 측정(Measurement) 단위 전환 (Vision_Protocol_v1.1 B안=측정점 분리). AddFaiResult를 fai.Measurements 순회로 재작성 — 측정마다 id=val=judge 1항목(다측정 FAI=FAIName_P{n}, 단측정=FAIName) + ClassifyMeasurement 헬퍼 신규(측정 단위 NotExist/NG/OK + m_bCycleHasNG 누적). 기존 FAI당 1값(P1만 전송) 은폐 결함 제거 → 전 측정값/판정 전송. 와이어 빌더(BuildResultMessageV1/BuildFaiItemsV1)/사이클 P·F·B/FAIConfig/ClassifyFai 무변경(회귀 0). msbuild Debug/x64 PASS. | 1eae9ed | 빌드 PASS · 핸들러 파서 B포맷 동기화 + 실측 UAT 대기 |
| 260626-e3x | 2026-06-26 | 트리 SelectionChanged StartAt 재진입 크래시 수정 — 트리 클릭 시 "Cannot call StartAt when content generation is in progress"+뒤섞임 크래시. 원인=TreeListBox(가상화) 생성 도중 SelectionChanged 가 동기로 ItemsSource/SelectedObject 재교체(ClearResults/PropertyGrid rebind/재티칭)→generator 재진입(레시피경로는 이미 Dispatcher 마샬링됨=교차스레드 아님). 수정=무거운 선택 처리 본문 Dispatcher.BeginInvoke(Background) 지연→생성 패스 종료 후 실행, 지연 시 SelectedItem 재조회. msbuild PASS. | efd5894 | 빌드 PASS · 재현기부재 실환경 클릭 UAT 대기 |
| 260626-dbd | 2026-06-26 | ComputeProjectionDistance signed화 (감사 A-01) — Math.Sqrt 절대값(부호 소실)을 EdgeToLineDistance 동일 signed 공식(축별 sinθ≥0/cosθ≥0 정규화)으로 교체. 위임 5종(ArcEdgeDistance/CircleCenterDistance/ArcLineIntersect/CompoundCenterB·C) 일괄 해결 = InvertSign 영구NG·반대편 불량은폐 복원. 타입별 의도 점검(전부 signed 설계, XML doc 계약 정합) 후 진행. EdgeToLineDistance/measureY 무영향. msbuild PASS. | d0eedc9 | 빌드 PASS · 실측 UAT(항목별 부호 vs nominal·InvertSign 토글) 대기 |
| 260625-lo5 | 2026-06-25 | CTH datum 수직 기준선각 직교 수정 — `vertPhi=(π/2)+dθ`(틸트 같으면 π/2 고정) → `curAngle+π/2`(검출 수평선 직교)로 변경. measureX projection_pl 투영축이 안 기울어져 X축 거리가 순수 가로거리로 붕괴(A7 ~0.12mm 오차, 공차 ±0.05mm 초과)하던 버그 해소. Find+Teach 동일 직교 규약 통일(teach pose origin ~72px 어긋남 동반 수정). CTH 한정, 타 datum/Y축 회귀 0. msbuild Debug/x64 PASS. | a442b2b | 빌드 PASS · SIMUL 실측 UAT + 공칭 재확인 대기 |
| 260623-mao | 2026-06-23 | 체커보드 캘리브 검출 시각화 강화 — 검출 saddle 코너를 CalibrationResult(CornerRows/Cols)로 노출 + HalconDisplayService `Calib-Corners` cyan DispCross 배치 렌더 분기(FAI-EdgeRaw 미러, 새 HWindow 경로 0) + CalibrationWindow ShowCornerOverlay(SetInspectionOverlays 재사용)로 검출 코너 십자 오버레이 + 왜곡 리포트 보강(중앙부 px↔외곽부 px·종합/X/Y 편차%·로드/촬상/실패 클리어). 회귀 가드: FAI/Group/Datum 분기·MmPerPixel/IsDistortionWarn 무수정. msbuild Debug/x64 0 errors. | 7cff5a2, 7c88d56, 8daa972 | 빌드 PASS · SIMUL 코너마커 육안 UAT 대기 |
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
| 260616-hmc | 2026-06-16 | 무효 SimulImagePath SHOT 의 NO_IMAGE measurement 를 명확한 NG("NO IMAGE") 라벨로 일관 표기 — 선행 simul-shot-cascade 디버그 수정(de7773f, image=null→NG)의 후속. 판정 라벨 4곳(ReviewMeasurementRow / ExcelExportService / ReviewerWindow 불량필터·포커스 / RepeatMeasurementStats)이 DATUM_FAIL 만 인식하던 것을 NO_IMAGE 분기 추가로 보완(기존엔 "—" dash 로 표시). LastSkipReason="NO_IMAGE"(밑줄) ↔ JudgeText="NO IMAGE"(공백). C# 7.2 if/else 체인. msbuild Debug/x64 0 errors. | 11c359e, c512839 | SIMUL UAT 대기 (무효 경로 SHOT → "NO IMAGE" 라벨 + 불량필터 노출 육안 확인) |
| 260617-cq2 | 2026-06-17 | 일괄 검사 완료 후 체크 SHOT 전체 측정결과 그리드 표시 — Phase 51 설계 갭(BatchRunService 가 dataGrid_faiResults 미갱신, 결과 리스트박스에 안 나옴) 해소. InspectionViewModel.ShowMeasurementsForShots(List<ShotConfig>) 신규(OnActionSelected 단일-shot 평탄화의 다중-shot 확장) + Btn_batchRun_Click 체크 SHOT 수집(_batchShots) → OnBatchComplete 에서 그리드 펼침. 행이 live MeasurementBase 래핑이라 LastMeasuredValue/판정 즉시 반영. FAIName 순수 유지 → MainView SourceMeasurement ReferenceEquals 매칭(MainView.xaml.cs:292) ROI 하이라이트 회귀 0. msbuild Debug/x64 0 errors. | 44a9575 | **UAT PASS (사용자 2026-06-17, 체크 다중 SHOT 일괄 검사 → 결과 그리드 전체 측정값/판정 표시 확인)** |
| 260618-o2m | 2026-06-18 | Phase 54 ALIGN-01 carry-over#1 — datum 검출 strip θ 회전(TryExtractEdgePoints): AlignPreTransform 의 회전각 atan2(T[3],T[0]) 추출 → measurePhi 가산 + enlarged AABB(측정 ROI FAIEdgeMeasurementService 미러링, datum 규약 L1=col/L2=row 보존). 축정렬 strip 이 틸트 datum 에지 비스듬히 샘플 → 검출각 0.1-0.2° 편차 → 먼 측정점 NG 가 원인. alignRot=0(비-align/TryFindLine) 무변경(회귀 0). +TryComposeAlign 확증로그(datumDetectRotDeg vs patternThetaDeg). msbuild Debug/x64 0 errors. | 9248473, a719073 | UAT 1차 NG → 확증로그로 strip 회전 부호 반대 발견(datumDetectRotDeg=-1.0 vs patternThetaDeg=+0.997, 크기일치·부호반대) → 부호 핫픽스 a719073(measurePhi -= alignRot) → **근본원인 확정 = align 아님, ~0.5% 캘리브레이션**: 원본(회전X) 이미지서도 A1=20.77 vs 공칭 20.681(+0.43%) 재현 → 측정값 vs 공칭 ~0.5% 불일치(pixelRes 0.00265 or 공칭값 캘리). 큰측정(20mm)만 공차초과 NG·작은건 OK(비율). align(strip θ/부호/패턴transform) 검증됨·별개. 임시 진단([ETLD]/[ALIGN-CHK]/패턴테스트) revert. 다음=공칭 출처확인→pixelRes ~0.5%↓ 보정. 상세 memory project_phase54_align_progress |
| 260623-jvm | 2026-06-23 | MainView.xaml.cs 매직넘버 const화 (QUAL-01 §5, 안전 LOW). 함정 최다 파일(3097줄)이라 매우 보수적. 명확·완전동치 4개만: MaxPolygonPoints=20, MinPolygonPoints=3, MinCalibrationPixelDistance=1.0, MessageDisplaySeconds=3.0 (폴리곤 표시 문자열도 const 통일·출력 동일). **회피: 180/Math.PI(부동소수 연산순서 변경 ULP 위험), 캘리브 ==1/==2(의미어색), SetPolygonDraft "red"/"blue"(SetColor함정), 공개 API/이벤트핸들러/Dispatcher/색상문자열, 117줄+함수분리, >0 ROI비교(상태성).** msbuild Debug/x64 0 errors. | c97edcf | 빌드 PASS · 동작 동치(정적 검증) |
| 260623-jnn | 2026-06-23 | MainResultViewerControl.xaml.cs 매직넘버 const화 (QUAL-01 §5, 안전 LOW). Explore 스코프 분석 선행 — 파일이 이미 대부분 const화 상태라 안전 수확 적음. 명확·무위험 3개만: MinViewPartSize=20.0, PanMarginScale=0.75, PolygonMinVertices=3 (리터럴→const 1:1, 값·분기 불변). **의도 회피(위험): RenderNow(139줄)/HMouseDown·Move·Up(117~142줄) 함수분리, RenderEditHandles SetColor("yellow"), 공개 API/이벤트/Dispatcher/HALCON 직접호출. 보류: 다의적 0.5·1.0·(0,0)·line-width2·색상문자열.** msbuild Debug/x64 0 errors. | 2bf6e49 | 빌드 PASS · 동작 동치(정적 검증) |
| 260623-jd6 | 2026-06-23 | HalconViewerControl.xaml.cs 매직넘버 const화 (QUAL-01 §5, 안전 LOW 범위) — 산재 매직넘버를 PascalCase private const 6종으로 추출: MinDraftRoiSize=20.0, DraftDefaultHalfSize=60.0, CornerHitThreshold=10.0, PanMarginScale=0.75, RoiClickTolerancePixels=3.0, MinViewPartSize=20.0. 기존 지역 const(halfSize/cornerHit) 클래스 승격. 다의적 1.0(9곳)·기존 minSize 5.0 은 의도적 보류(오분류 방지). 리터럴→const 1:1 치환만(값·분기 불변). 공개 API/HOperatorSet·HalconWindow 호출/이벤트핸들러/try-catch/상태플래그 전부 diff 미등장. 스코프 Explore 분석 선행(HIGH/MED/LOW 구분). msbuild Debug/x64 0 errors. | 70b6cc1 | 빌드 PASS · 동작 동치(정적 검증) |
| 260623-itv | 2026-06-23 | Ini.cs 리팩토링 (QUAL-01) — IniValue 좌표 파서 3종(TryParseCircle/Line/Rect) 컨벤션 적용: 매직넘버(필드개수 3/4·인덱스 0~3) → private const 7개(CIRCLE/LINE/RECT_FIELD_COUNT, FIELD_X/Y/W/H), 지역변수 점진 헝가리언(strArray→szParts, 공유 value 중간변수 제거 → dX/dY/dW/dH 직접 out). 공개 API/operator/IDictionary 명시 구현/Save·Load 직렬화/예외 문자열/#if JS 블록 전부 불변(diff=파서 3종 내부+const+헤더 주석 1줄에만 국한, 기능 동치). msbuild Debug/x64 0 errors(신규 warning 0). | fc74fe1 | 빌드 PASS · 동작 동치(정적 검증) |
| 260625-v30 | 2026-06-25 | 프로토콜 v3.0 반영 — $ALIGN_TEST [0]=target,[1]=MaterialNo,[2]=skip,BOTTOM→[3]=AlignFace / $ALIGN_RESULT 포맷(구분자';'→',',MaterialNo echo,OK|NG,Name=val) / $ALIGN_CALIB [0]=BOTTOM,[1]=CmdStr(AlignFace제거), ack=BOTTOM,CMD,OK(STEP→N 삽입) / $ALIVE 신설(AlivePacket/AliveResponsePacket). msbuild Debug/x64 0 errors. | 62d074c | 빌드 PASS |
| 260619-cnm | 2026-06-19 | per-shot 측정 보정계수 CorrectionFactor — 비전측정↔현미경공칭 ~0.5% 캘리브 간극을 PixelResolution(1회 캘리브 후 고정) 불변으로 둔 채 별도 per-shot 보정계수로 흡수(mm = pixelDist × PixelResolution × CorrectionFactor). CameraSlaveParam.CorrectionFactor(기본 1.0, ParamBase INI 자동직렬화·키미존재 1.0 폴백) + GetEffectivePixelResolution() 메서드(미직렬화→PixelResolution 저장값 불변). Action_FAIMeasurement:265 단일주입을 GetEffectivePixelResolution() 로 전환(14종 측정 일괄) + 보정 ±2% 초과 가드레일 경고(정상 0.5%=0.995 미발동). EdgePairDistance:74 재도출 경로도 동일 메서드. 각도 2종 미영향. 회귀 0(기본 1.0). 에이전트 패널 3인 per-shot 합의(균일·등방→배율오차, 표본부족 per-FAI 과적합). msbuild Debug/x64 컴파일 0 errors(exe-copy만 앱잠금). **UAT 1차 FAIL(기본값인데 전 측정 0)→핫픽스 20c9b6f**: ParamBase.Load 누락키→ToDouble()=0 이 CorrectionFactor 초기값 1.0 클로버 → CameraSlaveParam.Load 키부재 시 1.0 복원 + GetEffectivePixelResolution ≤0 클램프. | d6c95a7, 20c9b6f | **UAT PASS (2026-06-19)** — 재빌드 후 기본값 측정 정상복귀(회귀0) + CorrectionFactor 0.995 적용 시 측정값 보정 확인. signed_off. |
| 260702-i7o | 2026-07-02 | Tray/Bottom Align 비전 안전 가드 2건 — ①BottomVisionView.CalResetButton_Click 에 예/아니오 확인(CustomMessageBox.ShowConfirmation) 게이트 추가, "아니오" 시 PickerCal.Reset() 미호출·부수효과 0. ②TrayVisionView/BottomVisionView RunButton_Click 에 Matcher.HasTemplate 모델 존재 가드 추가, 모델 없으면 "모델이 없습니다" 경고(CustomMessageBox.Show) 후 Matcher.Run 미호출. 기존 CustomMessageBox 재사용, 신규 의존성 0. 삼항 미사용(if-else), //260702 hbk 주석, K&R 브레이스 유지. msbuild Debug/x64 PASS. | 4d55825, 1b65aa4 | 빌드 PASS · 실사용 버튼 클릭 UAT 대기 |
| 260702-ja4 | 2026-07-02 | Tray/Bottom Align 비전 ROI1/ROI2 재드로잉 확인 가드(260702-i7o 후속) — TrayVisionView/BottomVisionView 의 DrawRoi1/DrawRoi2Button_Click 4곳에 기존 ROI(_roi1/_roi2 != null)가 있을 때만 CustomMessageBox.ShowConfirmation("ROI 재드로잉", ...) 삽입, "아니오" 시 초기화/StartRectangleDrawing 전부 미실행(기존 ROI 유지). 최초 티칭(_roi1/_roi2==null) 경로는 확인 없이 기존과 동일(회귀 0). 신규 의존성 0, 삼항 미사용, //260702 hbk 주석, K&R 브레이스 유지. msbuild Debug/x64 PASS. | 0896a71, 021bdc7 | 빌드 PASS · 실사용 버튼 클릭 UAT 대기 |
| 260702-lx0 | 2026-07-02 | Action_FAIMeasurement.cs `case EStep.Measure`(약 216줄) 순수 Extract Method 리팩토링 — 7개 private 헬퍼로 분리(MarkMeasurementDatumSkipped/ResolveDatumTransform/InjectDatumOrigin/TryExecuteMeasurement/ApplyOverlaySuffixAndAccumulate/AggregateFaiResult/MarkAllMeasurementsNoImage). sharedSrc try/finally.Release·capSaver null skip·DualImage dispose 순서·measuredCount++(datum-fail continue 포함)·overlay FAI-Edge* suffix·NO_IMAGE 캐스케이드 faiHadMeas 분기 등 behavioral_equivalence_invariants 전부 보존(로직 재배열 0). 기존 유일 삼항(LastSkipReason)만 if-else 전개, 신규 삼항 0건. Debug/x64 빌드 PASS(양 커밋 후 각각 확인) + 정적 diff 리뷰로 verbatim 이식 확인. plan-checker 2회 통과(1차 4건 사소 이슈 수정 후 재검증 PASS). | d6a1823, 29344df | **코드 완료 · checkpoint pending — SIMUL $TEST P/F/B 판정 동치 실측 재확인 필요(사용자)** |

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

- 2026-06-24: Phase 63 added — TCP 프로토콜 Type 필드 반영 및 Align TCP 통합. 디팜스테크 v3.0 엑셀 스펙(D:\…v3_3.xlsx, Type 모델+다이어그램 셀 재작성 완성)을 코드 반영. Grabber $TEST/$RESULT 에 Type(site=PC#/Type=대상) + Align 커맨드($ALIGN_TEST/CALIB/RESULT) TCP 통합을 한 phase 로(같은 TCP 경계 파일). 만질 파일=VisionRequestPacket/VisionResponsePacket/ResourceMap/SystemHandler/InspectionSequence. Phase 59/60(Custom/EthernetVision/)과 무겹침 → 병렬 가능. ※ gsd-sdk phase.add CLI 가 phase_number 를 54 로 오산정(기존 Phase 54 ALIGN-01 충돌) → **수동 63 보정**(반복 버그, Phase 31/32/57.1 동일). 다음 = /gsd-plan-phase 63.
- 2026-06-22: Phase 57.1 inserted after Phase 57 (URGENT) — 패턴 ROI 검증 & 안전장치. Phase 57 UAT 피드백 4항목: ① Top/Bottom 패턴매칭 보정 적용/육안 확인(cyan ROI 표시) ② gen_rectangle2 length1/length2 장축·baseline 회전각 진단(analytic 회전은 정상 — swap 코드 수정 금지, 진단/시각화로 확증) ③ 패턴 ROI 시각화 렌더 조건 안정화(_resultDatumOverlays Datum 노드 선택 시에도 채우기) ④ 패턴 ROI 버튼 비-Datum 노드 비활성화 + 알림 메시지박스 안전장치. 조사 = Explore 3건 + 직접 코드 확인(PatternMatchService/FAIEdgeMeasurementService/MainView·MainResultViewerControl). ※ gsd-sdk phase.insert CLI "Phase 57 not found" 버그 재현 → 수동 삽입. 다음 = /gsd-plan-phase 57.1 (또는 #2 진단 먼저 discuss).
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
- 2026-06-18: Phase 54 added — Datum 패턴매칭 위치보정 (ALIGN-01). 자재 X,Y(+tilt) 변위 정렬 — Datum 패턴매칭으로 x,y, line-fit 으로 정밀 θ 하이브리드, ROI 좌표변환(무 warp), per-Datum 매칭(Side=4회), `_datumTransforms[DatumName]` 합성, 실패=MarkDatumFailed ALIGN_FAIL NG. Phase 52(레벨링) 흡수·대체(이미지회전 폐기). 분석문서 = .planning/ALIGN-01-pattern-align-analysis.md. Open discuss = 매칭 엔진 Shape vs NCC(포커싱 불량 대비 defocus-robust) vs per-Datum 선택형. 다음 = /gsd-discuss-phase 54.
- 2026-06-19: Phase 57 added — 패턴 ROI UX & Datum 정렬 보강 (Phase 54~56 ALIGN 후속 UAT 피드백 6항목). ① Pattern ROI1/2 버튼 나란히+2개 필수 안전장치 ② Pattern ROI 표시/숨김 토글 ③ Datum 색상 통일=slate blue만(magenta 기준선+legacy yellow 제거, 사용자 결정) ④ Side datum 4-ROI 세로축 별도 매칭(설계는 discuss서) ⑤ 매칭 에러 시 측정 진행(lenient) ⑥ leveling reference 제거(IsLevelingReference/LevelingEnabled, 미사용·사용자 결정). 조사 = Explore 3건(leveling 소비경로/Side ROI 구조/패턴버튼·datum 색상). ※ gsd-sdk phase.add CLI 또 phase_number 오산정(54, 기존 54~56 충돌) → 수동 보정 (57). 다음 = /gsd-discuss-phase 57 (#4 gray area).

## Session Continuity

Last session: --stopped-at
Stopped at: Phase 67 context gathered
Resume file: --resume-file
Next action: Phase 65 Plan 03 — ProcessAlignTest 슬롯별 Matcher.Run 배선 (D-06/D-07)

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

**Planned Phase:** 67 (양산 이력 통계 분석 (STAT-01)) — 3 plans — 2026-07-07T01:09:44.516Z
