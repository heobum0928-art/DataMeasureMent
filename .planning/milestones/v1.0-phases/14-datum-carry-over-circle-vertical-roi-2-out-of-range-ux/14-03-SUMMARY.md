---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
plan: 03
status: complete
date: 2026-04-27
requirements: [SPEC-14-Req-3]
commits:
  - c9ccc42 feat(14-03): DatumConfig Vertical 그룹 13 신규 필드 + Category prefix + INI 마이그레이션
  - ecc5a6e feat(14-03): TryTeachVerticalTwoHorizontal Vertical_* 슬롯 교체
  - b72368f feat(14-03): MainView Datum.Vertical 분기 + 티칭 step Vertical case 분리
  - d2fffbf feat(14-03): HalconDisplayService Vertical raw 점 orange 색
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
---

# Plan 14-03 Summary — VerticalTwoHorizontal Vertical_* 슬롯 분리

## Goal Achieved

Phase 13 UAT Test 1 issue 2 carry-over closure: VerticalTwoHorizontal 알고리즘이 Line1_*
슬롯을 재사용하던 의미 혼동 (사용자가 PropertyGrid "Line1_EdgeThreshold" 를 봐야 수직 ROI
파라미터를 튜닝) 해결. 신규 Vertical 그룹 분리 + INI 하위호환 + 자동 재티칭 wiring 보존.

## What Was Built

### Task 1 — DatumConfig.cs (commit c9ccc42)
- 신규 Vertical 그룹 13 필드: 5 geometry (Row/Col/Phi/Length1/Length2), 6 edge
  (EdgeThreshold/Sigma/EdgeDirection/EdgeSampleCount/EdgeTrimCount/EdgePolarity), 2 raw
  HTuple (DetectedEdgeRows/Cols), 2 Browsable=false List 헬퍼.
- D-08 Category prefix labeling: 5 ROI 그룹 (Line1/Line2/Circle/Horizontal_A/Horizontal_B)
  카테고리에 알고리즘 태그 추가 — Line1/Line2 (TLI), Circle (CTH), Horizontal_A/B
  (CTH/VTH) + 신규 Vertical (VTH).
- EnsurePerRoiDefaults Vertical 마이그레이션 분기 (D-05/D-06):
  Vertical_* sentinel(==0/"") 시 Line1_* 값 1회 복사 (idempotent).
  Geometry 도 Vertical_Length1==0 sentinel 시 Line1_* 5 필드 1회 복사.
  Line1_* zero-out 안 함 (회귀 0).

### Task 2 — DatumFindingService.cs (commit ecc5a6e)
TryTeachVerticalTwoHorizontal 함수 안:
- TryFindLine 호출 12 인자 (Row/Col/Phi/Length1/Length2/Sigma/EdgeThreshold/EdgePolarity/
  EdgeDirection/EdgeSampleCount/EdgeTrimCount): config.Line1_* → config.Vertical_*.
- raw 점 write-back: Line1_DetectedEdgeRows/Cols → Vertical_DetectedEdgeRows/Cols.
- 진단 로그 레이블: "Line1(vertical)" → "Vertical".
- Line1Detected_RBegin/REnd/CBegin/CEnd (overlay 외삽용 begin/end 좌표) 보존.
- TryTeachTwoLineIntersect 의 Line1_* 본래 사용 보존 — 회귀 0.

### Task 3 — MainView.xaml.cs (commit b72368f)
W4 precheck 결과: EDatumTeachStep.Vertical 이미 정의 (MainView.xaml.cs:52). Branch A 적용.
- PublishDatumRoiCandidates VerticalTwoHorizontal 케이스: Datum.Line1 RoiId →
  Datum.Vertical, Line1_* 필드 → Vertical_*.
- ApplyDatumRoiDelta switch: case "Datum.Vertical" 신규 (Vertical_Row/Col 누적).
- ClearDatumRoiFields switch: case "Datum.Vertical" 신규 (Vertical 5 필드 → 0).
- HalconViewer_DatumRectCompleted: case Line1/Vertical 폴-스루 분리.
  Line1 case 보존, 신규 Vertical case 가 _editingDatum.Vertical_* 5 필드 write-back.
- enum / GetFirstStep / GetNextStep 변경 없음 (precheck 결과 Vertical step 이미 존재).

### Task 4 — HalconDisplayService.cs (commit d2fffbf)
- RenderDatumOverlay LastTeachSucceeded 분기에 한 줄 추가:
  datum.Vertical_DetectedEdge* → orange 색.
- 기존 5 ROI 색 (cyan/magenta/yellow/green/lime green) 과 시각 구분.

## Verification

- **Build:** `MSBuild Debug/x64` exit 0, 0 errors, 신규 warning 0 (4 task 각각 build).
- **SIMUL_MODE UAT (Task 5) — 7 시나리오 모두 PASS:**
  - PropertyGrid Categories — 11 카테고리 prefix 정확 표시 + Vertical (VTH) 그룹 6 edge 필드 노출.
  - INI 하위호환 — 기존 Phase 12/13 VerticalTwoHorizontal INI 로드 시 Vertical_* 가
    Line1_* 와 동일 값 자동 채워짐.
  - VerticalTwoHorizontal 티칭 — Vertical / HorizA / HorizB 3 ROI 차례 → 자동 재티칭 →
    검출 라인 + raw 점 (Vertical=orange, HorizA=green, HorizB=lime green) 정확 표시.
  - 자동 재티칭 wiring — Vertical_EdgeThreshold 값 변경 → 즉시 재티칭 발동 (Phase 13-06+13-07
    NotifyDatumParamMaybeChanged 라우팅 보존).
  - Vertical ROI 이동 — Datum.Vertical 분기 통과 → 자동 재티칭 → 새 위치 검출.
  - 회귀 없음 — TwoLineIntersect Line1/Line2 ROI 정상 + INI 저장/로드 무오류.

## Acceptance Criteria

Plan acceptance criteria (frontmatter `truths`):
- [x] DatumConfig Vertical 13 신규 필드 (Task 1)
- [x] PropertyGrid Vertical (VTH) 카테고리 6 edge 필드 노출 (UAT Scenario 2)
- [x] INI 하위호환 — Vertical_* 자동 채움 (UAT Scenario 3)
- [x] TryTeachVerticalTwoHorizontal Vertical_* 슬롯 사용 (Task 2)
- [x] MainView Datum.Vertical 4 분기 (PublishDatumRoiCandidates + ApplyDatumRoiDelta +
  ClearDatumRoiFields + DatumRectCompleted) (Task 3)
- [x] RenderDatumOverlay Vertical_DetectedEdge* orange (Task 4)
- [x] 자동 재티칭 wiring 보존 (UAT Scenario 5)

## Notable Deviations

- W4 precheck 결과 Branch A 적용: enum 변경 + GetFirstStep + GetNextStep 추가 작업 불필요
  (이미 정의되어 있음). DatumRectCompleted 의 Line1/Vertical 폴-스루 split 만 처리.
- Vertical 그룹 추가 위치: Line1 그룹 직후 (Line2 직전) 에 삽입 — VerticalTwoHorizontal
  알고리즘 컨텍스트의 첫 라인이라는 의미적 매핑.

## Requirements Mapping

- **SPEC-14-Req-3** (VerticalTwoHorizontal 의미 분리) — COVERED.

## Next

- Plan 14-04 (Wave 3, legacy TryFindCircle 360° polar-sampling) — 14-03 완료 후 진행.
- Plan 14-05 (Wave 4, btn_testFindDatum 통합 검증) — 14-04 후.

