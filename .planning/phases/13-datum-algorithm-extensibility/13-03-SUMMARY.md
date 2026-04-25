---
phase: 13-datum-algorithm-extensibility
plan: "03"
subsystem: UI/Datum-RoiEdit
tags: [datum, ui, roi-edit, roi-delete, phase-13, gap-1, gap-a]
status: complete
updated: "2026-04-26"
dependency_graph:
  requires: [13-02]
  provides: [Gap-1-DatumRoiEdit, Gap-A-DatumRoiDelete]
  affects: [MainView, MainResultViewerControl]
tech_stack:
  added: []
  patterns:
    - "HalconViewer_RoiMoveCompleted Datum 분기 — K&R brace, StartsWith('Datum.') early return"
    - "HalconViewer_RoiDeleteRequested Datum 분기 — K&R brace, ClearDatumRoiFields 5-case switch"
    - "ApplyDatumRoiDelta 5-case switch — DeltaRow/DeltaCol 누적"
    - "SetDatumRoiCandidates/ClearDatumRoiCandidates — Allman brace, Datum 노드 선택 시 후보 publish"
    - "HitTestOneRoi private static helper — Rect bbox + Circle radius 판정"
    - "InvokeTryTeachDatumForEdit — DatumFindingService.TryTeachDatum 자동 재호출 + label_drawHint 피드백"
key_files:
  modified:
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
decisions:
  - "Datum ROI 이동/삭제 분기를 RoiId.StartsWith('Datum.')로 식별 — FAI 경로와 완전 독립"
  - "HitTestOneRoi를 private static helper로 추출하여 FAI + Datum 후보 양쪽 재사용"
  - "SetDatumRoiCandidates는 _isEditMode 무관 통과 — Datum 노드 선택만으로 드래그 허용"
  - "ClearDatumRoiFields: 어느 ROI든 삭제되면 IsConfigured/LastTeachSucceeded false — RenderDatumOverlay 그리기 가드 연동"
  - "PublishDatumRoiCandidates 3 지점 (TeachDatumButton_Click 진입 + InvokeTryTeachDatum 성공 + HandleDatumRoiMove 말미)"
  - "Plan 02 CustomMessageBox.Show 두 호출 (message, title) → (title, message) swap — 코드베이스 호출 컨벤션 정합"
  - "UAT 초기 발견 버그: InspectionList 선택 시 PublishDatumRoiCandidates 미호출 → hotfix e199093으로 해결"
metrics:
  duration: ~40min
  completed_date: "2026-04-26"
  tasks_completed: 3
  tasks_total: 3
  files_changed: 2
---

# Phase 13 Plan 03: Datum ROI Edit + Delete Summary

**One-liner:** 티칭 완료된 Datum ROI(Rect/Circle)를 마우스 드래그 이동 + 우클릭 Delete하면 DatumConfig 필드에 delta가 반영되고 TryTeachDatum이 자동 재호출되어 검출 오버레이가 즉시 갱신된다 (Gap-1 + Gap-A 완결).

---

## Files Changed

| File | Type | Description |
|------|------|-------------|
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | modified | `_datumRoiCandidates` 필드 + `SetDatumRoiCandidates`/`ClearDatumRoiCandidates` API + `HitTestOneRoi` helper + `HitTestSelectedRoi` fallback + MouseDown Datum 모드 통과 게이트 + Datum hit 시 `_selectedRoiId` set |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | modified | `HalconViewer_RoiMoveCompleted` Datum 분기 + `HalconViewer_RoiDeleteRequested` Datum 분기 + `IsCurrentNodeDatum` / `HandleDatumRoiMove` / `ApplyDatumRoiDelta` / `ClearDatumRoiFields` / `InvokeTryTeachDatumForEdit` / `PublishDatumRoiCandidates` / `BuildDatumRectCandidate` / `BuildDatumCircleCandidate` + Plan 02 CustomMessageBox 인자 순서 fix 2곳 |

---

## Gap Coverage Mapping

| Gap ID | 구현 위치 | 내용 |
|--------|-----------|------|
| Gap-1 (ROI Edit/Move) | `MainView.xaml.cs` `HalconViewer_RoiMoveCompleted` → `HandleDatumRoiMove` → `ApplyDatumRoiDelta` | Datum ROI 드래그 이동 시 DatumConfig Row/Col 필드에 delta 누적 + 자동 재티칭 + 오버레이 갱신 |
| Gap-1 (자동 재티칭) | `MainView.xaml.cs` `InvokeTryTeachDatumForEdit` | DatumFindingService.TryTeachDatum 재호출 + label_drawHint 녹색/빨강 피드백 |
| Gap-1 (후보 publish) | `MainView.xaml.cs` `PublishDatumRoiCandidates` + `MainResultViewerControl.xaml.cs` `SetDatumRoiCandidates` | DatumConfig → RoiDefinition 리스트 변환 + halconViewer 주입 (3 지점) |
| Gap-A (ROI Delete) | `MainResultViewerControl.xaml.cs` MouseDown Datum hit 시 `_selectedRoiId = hitRoi.Id` set | 기존 우클릭 컨텍스트 메뉴 Delete 인프라 재사용 — 신규 이벤트 없음 |
| Gap-A (Delete 처리) | `MainView.xaml.cs` `HalconViewer_RoiDeleteRequested` 최상단 Datum 분기 → `ClearDatumRoiFields` | Datum ROI 필드 0 reset + IsConfigured/LastTeachSucceeded false → RenderDatumOverlay 그리기 가드로 시각 제거 |

---

## Plan 02 CustomMessageBox Cleanup

Plan 02(`BtnTestFindDatum_Click` / `AskTestImageSource`)에서 `CustomMessageBox.Show`를 `(message, title)` 순서로 호출한 두 곳을 `(title, message)` 순서로 swap.

Before:
```csharp
CustomMessageBox.Show("Datum 티칭이 완료된 후 테스트 가능합니다.", "Datum Find 테스트");
CustomMessageBox.Show("이미지 로드 실패: " + ex.Message, "Datum Find 테스트");
```
After:
```csharp
CustomMessageBox.Show("Datum Find 테스트", "Datum 티칭이 완료된 후 테스트 가능합니다."); //260425 hbk Phase 13 cleanup
CustomMessageBox.Show("Datum Find 테스트", "이미지 로드 실패: " + ex.Message); //260425 hbk Phase 13 cleanup
```
코드베이스 다른 호출(`CustomMessageBox.Show("Error", "Camera Init Fail", ...)` 패턴)과 일치.

---

## New API Catalog

### MainResultViewerControl.xaml.cs (Allman brace)

| 멤버 | 종류 | 시그니처 | 설명 |
|------|------|----------|------|
| `_datumRoiCandidates` | private field | `List<RoiDefinition>` | Datum 노드 선택 시 MainView가 주입하는 ROI 후보 리스트 |
| `SetDatumRoiCandidates` | public method | `void SetDatumRoiCandidates(IList<RoiDefinition> datumRois)` | Datum ROI 후보 주입 — null이면 Clear |
| `ClearDatumRoiCandidates` | public method | `void ClearDatumRoiCandidates()` | Datum 노드 비선택 시 후보 clear |
| `HitTestOneRoi` | private static | `RoiDefinition HitTestOneRoi(RoiDefinition roi, Point imagePoint)` | Rect bbox + Circle radius 공통 hit 판정 |
| `HitTestSelectedRoi` (확장) | private | 기존 시그니처 유지 | FAI _rois null fallback 후 `_datumRoiCandidates` 검사 추가 |
| MouseDown 게이트 (확장) | — | `bool datumCandidatesPresent = (_datumRoiCandidates != null && _datumRoiCandidates.Count > 0)` | `_isEditMode || datumCandidatesPresent` 조건으로 Datum 드래그 허용 |

### MainView.xaml.cs (K&R brace)

| 메서드 | 시그니처 | 설명 |
|--------|----------|------|
| `IsCurrentNodeDatum` | `bool IsCurrentNodeDatum(out DatumConfig datum)` | 현재 선택 노드가 DatumConfig인지 판정 |
| `HandleDatumRoiMove` | `void HandleDatumRoiMove(DatumConfig datum, RoiMoveCompletedArgs e)` | delta 누적 + PropertyGrid 갱신 + 자동 재티칭 + 후보 publish |
| `ApplyDatumRoiDelta` | `void ApplyDatumRoiDelta(DatumConfig datum, RoiMoveCompletedArgs e)` | 5-case switch — RoiId prefix별 Row/Col 누적 |
| `ClearDatumRoiFields` | `void ClearDatumRoiFields(DatumConfig datum, string roiId)` | 5-case switch — 해당 ROI 필드 0 reset + IsConfigured/LastTeachSucceeded false |
| `InvokeTryTeachDatumForEdit` | `void InvokeTryTeachDatumForEdit(DatumConfig datum)` | DatumFindingService.TryTeachDatum 재호출 + label_drawHint 색상 피드백 |
| `PublishDatumRoiCandidates` | `void PublishDatumRoiCandidates(DatumConfig datum)` | DatumConfig → RoiDefinition 리스트 변환 + halconViewer.SetDatumRoiCandidates |
| `BuildDatumRectCandidate` | `static RoiDefinition BuildDatumRectCandidate(string id, double centerRow, double centerCol, double halfH, double halfW)` | Rectangle2 bbox RoiDefinition 생성 |
| `BuildDatumCircleCandidate` | `static RoiDefinition BuildDatumCircleCandidate(string id, double centerRow, double centerCol, double radius)` | Circle RoiDefinition 생성 |

---

## RoiId Prefix 규약 (Plan 03 최초 정의)

| RoiId | 적용 알고리즘 | DatumConfig 필드 |
|-------|--------------|-----------------|
| `"Datum.Line1"` | TwoLineIntersect Line1 OR VerticalTwoHorizontal 수직 ROI | `Line1_Row`, `Line1_Col`, `Line1_Length1`, `Line1_Length2` |
| `"Datum.Line2"` | TwoLineIntersect Line2 | `Line2_Row`, `Line2_Col`, `Line2_Length1`, `Line2_Length2` |
| `"Datum.Circle"` | CircleTwoHorizontal Circle ROI | `CircleROI_Row`, `CircleROI_Col`, `CircleROI_Radius` |
| `"Datum.HorizontalA"` | CircleTwoHorizontal + VerticalTwoHorizontal 공용 | `Horizontal_A_Row`, `Horizontal_A_Col`, `Horizontal_A_Length1`, `Horizontal_A_Length2` |
| `"Datum.HorizontalB"` | CircleTwoHorizontal + VerticalTwoHorizontal 공용 | `Horizontal_B_Row`, `Horizontal_B_Col`, `Horizontal_B_Length1`, `Horizontal_B_Length2` |

모든 Datum 분기 조건: `roiId.StartsWith("Datum.")` — FAI RoiId와 충돌 없음.

---

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
Output: DatumMeasurement.exe
Exit code: 0
수정 파일 대상 신규 warning: 0
기존 warning (scope 밖):
  - VirtualCamera.cs CS0162 (unreachable code)
  - VisionAlgorithmService.cs CS0219 (unused variable)
  - MSB3884 (MinimumRecommendedRules.ruleset not found)
```

---

## Commits

| Task | Hash | Message |
|------|------|---------|
| Task 1+2 (ROI move/delete + CustomMessageBox fix) | feca4fc | feat(phase-13-03): Datum ROI move/delete + auto re-teach + CustomMessageBox arg fix |
| Hotfix (UAT bug — InspectionList 선택 시 candidates 미publish) | e199093 | fix(phase-13-03): publish Datum ROI candidates on InspectionList selection |

---

## UAT Outcome

**결과: APPROVED** (초기 UAT에서 1건 버그 발견 → hotfix e199093 적용 → 재UAT 승인)

### 초기 UAT 버그 발견

| 발견 이슈 | 현상 | hotfix |
|-----------|------|--------|
| InspectionList에서 Datum 노드를 선택해도 ROI 후보가 publish되지 않아 드래그 불가 | `PublishDatumRoiCandidates` 호출이 `TeachDatumButton_Click` + `HandleDatumRoiMove` 에만 있고 InspectionList 선택 이벤트에 없음 | e199093: InspectionList `SelectionChanged` 핸들러에서 `PublishDatumRoiCandidates` 추가 |

### UAT 승인 항목 (Issue 1 — 이번 Plan 범위)

| # | 시나리오 | 결과 |
|---|---------|------|
| 1 | TwoLineIntersect Line1 드래그 이동 → 재티칭 OK | PASS |
| 2 | TwoLineIntersect Line2 드래그 이동 → 재티칭 OK | PASS |
| 3 | CircleTwoHorizontal Circle/HorizontalA/HorizontalB 이동 → 재티칭 OK | PASS |
| 4 | VerticalTwoHorizontal Line1(수직)/HorizontalA/HorizontalB 이동 → 재티칭 OK | PASS |
| 5 | HorizontalA를 멀리 이동 → 방향 정합성 게이트 발동 (Phase 13-01) → 빨강 label | PASS |
| 6 | Datum ROI 우클릭 → Delete → ROI 사라짐 + IsConfigured=false | PASS |
| 7 | ROI 삭제 후 btn_teachDatum으로 재티칭 가능 | PASS |
| 8 | FAI ROI 이동 회귀 없음 (FAI 경로 untouched) | PASS |
| 9 | FAI 우클릭 Delete 회귀 없음 | PASS |

### 이월 이슈 (UAT에서 발견 — 이번 Plan 범위 밖)

| 이슈 | 현상 | 이월 대상 |
|------|------|-----------|
| ROI 이동 후 DatumFindingService 에지 파라미터가 모든 ROI 동일값 | per-ROI EdgeThreshold/Sigma/EdgeDirection 등이 미구현 | Plan 13-04 |
| 에지 파라미터 미조정 시 "insufficient edges" 실패 가능 | per-ROI 파라미터 없어 특정 알고리즘 타입에서 재티칭 실패 | Plan 13-04 |

---

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Functionality] UAT 발견: InspectionList 선택 시 Datum ROI candidates 미publish**
- **Found during:** Task 3 (UAT)
- **Issue:** `PublishDatumRoiCandidates`가 `TeachDatumButton_Click` 진입과 `HandleDatumRoiMove` 말미에만 호출되어, InspectionList에서 Datum 노드를 선택해도 ROI 후보가 주입되지 않아 드래그 시작 불가.
- **Fix:** InspectionListView의 `SelectionChanged` 핸들러(또는 동등한 Datum 노드 선택 이벤트)에서 `PublishDatumRoiCandidates(datum)` 호출 추가.
- **Files modified:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- **Commit:** e199093

---

## 13-04 / 13-05 이월 항목

### Plan 13-04 이월 (per-ROI 에지 파라미터)

- `DatumConfig` 스키마 확장 — 5 ROI × 6 파라미터 = 30 신규 필드 (`EdgeDirection`, `EdgeThreshold`, `EdgeSampleCount`, `EdgeTrimCount`, `EdgePolarity`, `Sigma` per ROI)
- `EnsurePerRoiDefaults()` — 기존 Phase 4/11/12 INI 레시피 자동 마이그레이션 (INI 하위호환)
- `DatumFindingService.TryFindLine` / `TryExtractEdgePoints` 시그니처 확장 — per-ROI 파라미터 전달
- PropertyGrid 노출

### Plan 13-05 이월 (시각화)

- 검출 라인 이미지 가장자리까지 외삽 (이전 Gap-C, `EXTEND_PX` helper)
- Raw 검출 에지점 색상 점 마커 표시
- RefOrigin / Angle / CircleCenter / Radius 캔버스 텍스트 라벨 표시

---

## Known Stubs

없음 — 모든 신규 메서드가 실제 DatumConfig 필드 write-back, DatumFindingService 재호출, HalconDisplayService 오버레이 경로로 완전 구현됨.

---

## Self-Check

- [x] `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` 수정 존재 (`_datumRoiCandidates`, `SetDatumRoiCandidates`, `ClearDatumRoiCandidates`, `HitTestOneRoi`, `HitTestSelectedRoi` 확장, MouseDown 게이트 확장)
- [x] `WPF_Example/UI/ContentItem/MainView.xaml.cs` 수정 존재 (`IsCurrentNodeDatum`, `HandleDatumRoiMove`, `ApplyDatumRoiDelta`, `ClearDatumRoiFields`, `InvokeTryTeachDatumForEdit`, `PublishDatumRoiCandidates`, `BuildDatumRectCandidate`, `BuildDatumCircleCandidate`, Datum 분기 2건, Plan 02 CustomMessageBox fix)
- [x] Task 1+2 커밋 feca4fc 존재
- [x] Hotfix 커밋 e199093 존재
- [x] msbuild Debug/x64 exit 0
- [x] FAI RoiMoveCompleted/RoiDeleteRequested 기존 경로 한 줄도 변경 없음
- [x] Gap-1 (ROI Edit/Move) 완결
- [x] Gap-A (ROI Delete) 완결
- [x] Plan 02 CustomMessageBox 인자 순서 fix 2곳 완료
- [x] UAT 승인 (hotfix e199093 적용 후)
- [x] 13-04/13-05 이월 항목 문서화 완료

## Self-Check: PASSED
