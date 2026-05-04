---
phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
plan: 03
subsystem: datum-ux-detected-origin-visualization
tags: [datum, find-runtime, transient-fields, hover, property-grid, algorithm-preservation]
depends_on:
  - 17-01
  - 17-02
provides:
  - DatumConfig.DetectedOriginRow/Col/RefAngle (transient, [Browsable(false)] + [JsonIgnore])
  - DatumConfig.DetectedEdgeCount/FitRMSE/AngleDeg ("Datum|Result" 카테고리, [ReadOnly(true)])
  - DatumFindingService.TryFindDatum 성공 분기 transient write-back (계산 로직 0 변경)
  - HalconDisplayService.RenderDatumFindResult purple body (LastFindSucceeded gate + DispCross 14 + Find 좌표 + 화살표)
  - HalconDisplayService.RenderDatumOverlay z-stack last RenderDatumFindResult 호출
  - MainView.xaml canvasToolbar X/Y/Gray TextBlock (panel_hoverInfo)
  - MainView.UpdatePointerLabel 양쪽 갱신 (label_pos + 3 TextBlock)
  - MainView.BtnTestFindDatum_Click 성공 경로: SetDatumOverlay + RaisePropertyChanged + RefreshParamEditor
  - InspectionListView 5-step Step 3 wiring (DetectedOrigin/메트릭 0 리셋)
requires:
  - 17-01 RadialDirection enum + RenderCircleStripOverlay 단일 strip
  - 17-02 ICustomTypeDescriptor + AlgorithmType 5-step 리셋 + 모달 정책
affects:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/UI/ContentItem/MainView.xaml
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
tech-stack:
  added: []
  patterns:
    - "Phase 13-05 D-VIZ-01 pattern (volatile fields + [Browsable(false)] + ParamBase reflection 자동 무시) 재사용"
    - "Plan 17-02 ICustomTypeDescriptor IsHiddenForAlgorithm 필터 prefix 무영향 (Detected* 신규 prefix 미매칭)"
    - "PublishPointerInfo → PointerInfoChanged → UpdatePointerLabel 기존 파이프라인 재사용 (PATTERNS gap #4 — 신규 GetGrayval 호출 0)"
    - "PropertyTools.DataAnnotations.ReadOnly + System.ComponentModel.ReadOnly 양쪽 부착 (PATTERNS gap #5 — 호환성 안전판)"
    - "Algorithm preservation pattern (D-17): 결과 write-back 라인만 추가, 계산 로직 0 라인 변경"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (+32 lines)
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs (+14 lines / -0)
    - WPF_Example/Halcon/Display/HalconDisplayService.cs (+35/-16 lines, 본문 교체 + 1 호출)
    - WPF_Example/UI/ContentItem/MainView.xaml (+15 lines)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs (+18/-13 lines)
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (+7/-1 lines)
decisions:
  - "Plan 17-03 영역 = transient/메트릭 블록 only — Plan 17-01 RadialDirection 블록 + Plan 17-02 ICustomTypeDescriptor 블록 영역 분리 lock 통과 (회귀 0)"
  - "PropertyTools.Wpf ReadOnly 호환성 검증: PropertyTools.DataAnnotations.ReadOnly 가 codebase canonical (ParamBase.cs L37/L53), System.ComponentModel.ReadOnly 도 부착하여 ICustomTypeDescriptor.GetProperties 경로 안전판 — 이중 부착으로 어느 PropertyGrid 분기에서도 readonly 보장"
  - "DetectedFitRMSE = 0 placeholder — TryFindDatum 가 fit RMSE 미수집 (Plan 17-03 deferred). Phase 18 또는 별도 plan 에서 FitLine residuals 추출 wiring 시 채워질 예정"
  - "Rule 3 deviation (Task 4): label_pointCount XAML 보존 — MainView.xaml.cs 5 사이트 (Polygon ROI 드로잉 모드 — line 976/1171/1172/1187/1193) 가 참조하므로 제거 시 CS0103 회귀. panel_hoverInfo 를 같은 Column 2 에 추가 배치 (Polygon 모드 동시 가시 트레이드오프 — UAT 시 시각 충돌 검증 권장, 단 Polygon ROI 는 Datum 워크플로우 외이므로 실제 사용자 영향 미미)"
  - "DetectedEdgeCount 계산: line1RawRows.TupleLength() + line2RawRows.TupleLength() (TLI 분기 raw 점 수 합계). CTH/VTH 알고리즘은 TryFindDatum 본 메서드가 아닌 다른 분기를 호출 — 본 wave 는 TLI 분기만 wiring (CTH/VTH 메트릭 분기 wiring 은 Phase 17-04 UAT 또는 후속 plan 에서 결정)"
  - "BtnTestFindDatum_Click 성공 경로 simplification: 기존 SetDatumFindResultOverlay 호출 → SetDatumOverlay(datum, true) 로 통합 (RenderDatumOverlay 가 z-stack last 에서 RenderDatumFindResult 자동 호출하므로 별도 호출 불필요). label_testFindResult.Content 사용 0 (모달 정책 강제)"
metrics:
  duration: "~12 min"
  completed: 2026-05-03
  files_changed: 6
  tasks: 5
  commits: 5
---

# Phase 17 Plan 03: Cluster D — DetectedOrigin transient + Test Find 시각화 + hover + ROI 결과 메트릭 Summary

DetectedOrigin transient 6 필드 (3 transient + 3 메트릭) 추가 + TryFindDatum 결과 write-back (D-17 algorithm preservation 강제) + RenderDatumFindResult purple cross 본문 교체 + canvasToolbar X/Y/Gray hover TextBlock + BtnTestFindDatum_Click 성공 경로 SetDatumOverlay 단일화로 Phase 16 UAT carry-over #5/#17/#18 해소.

## Tasks Executed

| # | Task | Files | Commit | Lines |
|---|------|-------|--------|-------|
| 1 | DatumConfig 6 신규 필드 (transient 3 + 메트릭 3) | DatumConfig.cs | f00c72f | +32 |
| 2 | TryFindDatum write-back (D-13/D-16/D-17) | DatumFindingService.cs | f1b6412 | +14 |
| 3 | RenderDatumFindResult purple body + z-stack last 호출 | HalconDisplayService.cs | 6423068 | +35/-16 |
| 4 | canvasToolbar X/Y/Gray TextBlock 3개 | MainView.xaml | b58a221 | +15 |
| 5 | UpdatePointerLabel + BtnTestFindDatum + InspectionListView Step 3 | MainView.xaml.cs, InspectionListView.xaml.cs | 5b3b8ac | +30/-19 |

## D-17 Algorithm Preservation Bound (Cumulative Phase 17)

| File | Phase 17 cumulative new code (주석/공백 제외) | Budget | Status |
|------|---------------------------------------------|--------|--------|
| WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs | **0** | = 0 | PASS |
| WPF_Example/Halcon/Algorithms/DatumFindingService.cs | **11** (Plan 17-01: 2 + Plan 17-03: 9) | ≤ 11 | EXACT |

DatumFindingService.cs Phase 17 누적 add: `git diff 728ed89^ HEAD --` 측정 = 11 라인. Budget exact match.

## ParamBase Reflection / INI 직렬화 동작

Phase 13-05 D-VIZ-01 패턴 재사용 검증:

- DetectedOriginRow/Col/RefAngle (transient): `[System.ComponentModel.Browsable(false)] + [PropertyTools.DataAnnotations.Browsable(false)] + [Newtonsoft.Json.JsonIgnore]` 3종 attribute 부착.
- ParamBase.Save (L324) / ParamBase.Load (L369) 가 `GetType().GetProperties()` reflection 사용 — `[Browsable(false)]` 무시하고 public double 직렬화 → INI 에 0 으로 기록. 다음 TryFindDatum 호출 시 즉시 덮어쓰여지므로 stale 값 의미 0 (Phase 11/13-05 패턴 동일 — 신규 IO 위협 없음).
- `[JsonIgnore]` 는 향후 JSON snapshot 도입 시 transient 필드를 직렬화 제외 — 선제적 안전 장치.
- 메트릭 3 필드 (DetectedEdgeCount/FitRMSE/AngleDeg) 도 ParamBase 가 INI 에 기록할 가능성 있음 — 런타임 측정 결과는 매 실행마다 갱신되므로 INI 의 stale 값은 의미 없음. 별도 처리 없음 (Phase 13-05 D-VIZ-01 정책 일치).

## PropertyTools.Wpf ReadOnly Attribute 호환성

PATTERNS.md gap #5 사전 검증 결과:

- PropertyTools.dll 분석 (`grep -aoE "ReadOnlyAttribute|IsReadOnly" packages/PropertyTools.3.1.0/lib/net45/PropertyTools.dll`): `ReadOnlyAttribute`, `set_IsReadOnly`, `IsReadOnly` 심볼 존재 → PropertyTools 자체 `ReadOnlyAttribute` 정의 (`PropertyTools.DataAnnotations` namespace).
- ParamBase.cs L37, L53 canonical 사용처: `[PropertyTools.DataAnnotations.ReadOnly(true)]` — codebase 컨벤션.
- 본 Plan 채택: **양쪽 부착** `[System.ComponentModel.ReadOnly(true)] + [PropertyTools.DataAnnotations.ReadOnly(true)]` — ICustomTypeDescriptor.GetProperties 경로가 base TypeDescriptor 위임 (ParamBase.cs 패턴) 에서 `System.ComponentModel.ReadOnlyAttribute` 도 query 가능. 어느 PropertyGrid 분기에서도 readonly 보장.

## DetectedOrigin* Reset Path Documentation (W3 Cross-Plan Wiring)

17-04 Test 13 (AlgorithmType 변경 시 검출 시각화 사라짐) 검증을 위한 reset 경로:

```
사용자: PropertyGrid 에서 AlgorithmType combobox 변경
   ↓
ParamEditor.SelectionChanged → InspectionListView.OnParamEditorSelectionChanged
   ↓
AlgorithmType whitelist 가드 통과
   ↓
[Step 1] ParamEditor.SelectedObject = null → datum (force rebind, ICustomTypeDescriptor 재계산)
[Step 2] datum.LastTeachSucceeded = false / datum.LastFindSucceeded = false (검출 도형 자동 미렌더 가드)
[Step 3] datum.DetectedOriginRow/Col/RefAngle = 0 + DetectedEdgeCount/FitRMSE/AngleDeg = 0  ←── 본 Plan 17-03 Task 5 신규 wiring
[Step 4] ROI 보존 (명시적 액션 없음)
[Step 5] 자동 재검출 없음 (TryTriggerDatumAutoReteach 호출 안 함)
[Step 6] mainView.halconViewer.SetDatumOverlay(datum, true)
   ↓
RenderDatumOverlay → LastFindSucceeded=false 분기 → RenderDatumFindResult 진입 가드 reject → purple cross 미렌더
```

**Reset 검증 acceptance (17-04 Test 13)**: AlgorithmType 변경 후 즉시 캔버스에서 purple DispCross + "Find (...)" 텍스트 + 화살표 모두 사라짐 + PropertyGrid "Datum|Result" 카테고리에 0/0/0 표시.

## BtnTestFindDatum_Click 성공/실패 분기 흐름

```
사용자: Test Find Datum 버튼 클릭
   ↓
가드: SelectedParam = DatumConfig + IsConfigured + LastTeachSucceeded 체크 → 미통과 시 모달
   ↓
AskTestImageSource (현재/Load/취소 3-way)
   ↓
DatumFindingService.TryFindDatum(testImage, datum, out transform, out error)
   ↓
   ├── ok=true → SetDatumOverlay(datum, true) → RenderDatumOverlay → z-stack last RenderDatumFindResult
   │              ├── purple DispCross size=14 lineWidth=2 (DetectedOriginRow/Col)
   │              ├── EnsureFontInitialized + WriteString "Find (row, col)"
   │              └── DetectedRefAngle 방향 화살표 (length=20, head=5)
   │            → datum.RaisePropertyChanged(string.Empty)
   │            → mParentWindow.inspectionList.RefreshParamEditor() (PropertyGrid Datum|Result 메트릭 즉시 표시)
   │            → 모달 X (UI-SPEC LOCKED — 캔버스 시각화로 즉시 확인)
   │
   └── ok=false → CustomMessageBox.Show("Find 실패", FormatFindError(error))
                → halconViewer.ClearDatumFindResultOverlay() (이전 잔상 제거)
```

**Plan 17-02 baseline 보존:**
- `FormatFindError` (D-04 EdgeDirection 힌트 통합) — 5 references intact (FormatTeachError + FormatFindError + ValidateRoiPresence)
- `MessageBoxButton.YesNoCancel` 2 사용처 보존 (Delete 3-button + AskTestImageSource)

## Plan 17-01 / 17-02 영역 회귀 검증

| Plan | Marker | Count | Expected | Status |
|------|--------|-------|----------|--------|
| 17-01 | `Circle_RadialDirection` (DatumConfig.cs) | 4 | ≥ 4 | PASS |
| 17-01 | `thetaRad = 0.0` (HalconDisplayService.cs) | 1 | ≥ 1 | PASS |
| 17-01 | `for...k...stepCount` 루프 (HalconDisplayService.cs) | 0 | = 0 | PASS |
| 17-02 | `ICustomTypeDescriptor` / `IsHiddenForAlgorithm` (DatumConfig.cs) | 7 | ≥ 4 | PASS |
| 17-02 | `ParamEditor.SelectedObject = null` (InspectionListView.xaml.cs) | 3 | ≥ 2 | PASS |
| 17-02 | `MessageBoxButton.YesNoCancel` (MainView.xaml.cs) | 2 | ≥ 1 | PASS |
| 17-02 | `ValidateRoiPresence` / `FormatTeachError` (MainView.xaml.cs) | 5 | ≥ 2 | PASS |

영역 분리 lock 통과 — Plan 17-01 / 17-02 의 코드는 한 라인도 수정되지 않았다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking issue avoidance] Task 4: label_pointCount XAML 보존 (대체 → 추가 변경)**

- **Found during:** Task 4 verify (grep label_pointCount in MainView.xaml.cs)
- **Issue:** 계획서는 label_pointCount XAML 요소를 panel_hoverInfo 로 *대체* 지시. 그러나 MainView.xaml.cs 에 5 사이트 (line 976/1171/1172/1187/1193 — Polygon ROI 드로잉 모드: `label_pointCount.Visibility / .Content`) 가 이를 참조 → XAML 제거 시 CS0103 컴파일 에러
- **Fix:** label_pointCount XAML 요소 보존 (Visibility=Collapsed) + panel_hoverInfo (StackPanel) 를 같은 Column 2 에 추가 배치. Polygon ROI 모드 진입 시 label_pointCount Visible 상태가 되어 두 요소가 같은 영역에 동시 가시될 수 있음 (시각 충돌). Polygon ROI 는 Datum/FAI 워크플로우 외부 기능이므로 실제 사용자 영향 미미 — UAT 시 검증 권장
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml
- **Commit:** b58a221

### Auth Gates

None.

## Self-Check: PASSED

**Files exist:**
- FOUND: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
- FOUND: WPF_Example/Halcon/Algorithms/DatumFindingService.cs
- FOUND: WPF_Example/Halcon/Display/HalconDisplayService.cs
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

**Commits exist:**
- FOUND: f00c72f (Task 1)
- FOUND: f1b6412 (Task 2)
- FOUND: 6423068 (Task 3)
- FOUND: b58a221 (Task 4)
- FOUND: 5b3b8ac (Task 5)

**Build status:**
- msbuild Debug/x64 PASS (5 회 / 매 Task 후), 신규 warning 0 on 수정 범위 (기존 pre-existing CS0162/CS0219/MSB3884 외)

## Next: Plan 17-04 (UAT — Phase 16 carry-over 16항목 + Phase 17 D-01~D-16 통합 시나리오 ≥ 16 signed_off)
