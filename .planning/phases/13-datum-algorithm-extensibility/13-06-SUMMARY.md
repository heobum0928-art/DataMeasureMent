---
phase: 13-datum-algorithm-extensibility
plan: 06
subsystem: ui-datum-propertygrid
type: gap-closure
wave: 1
depends_on: [13-04, 13-05]
status: complete
completed: 2026-04-26
tags: [datum, ux, propertygrid, auto-reteach, phase-13, gap-closure]
requirements:
  - Phase-13-UAT-Test6-AutoReteachOnParamChange
key-files:
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
metrics:
  duration_min: 5
  tasks: 2
  files_modified: 2
  commits: 3
---

# Phase 13 Plan 06: PropertyGrid Auto Re-Teach Gap Closure Summary

**One-liner:** Wired PropertyTools.Wpf.PropertyGrid `TextBoxBase.LostFocus` + `Selector.SelectionChanged` routed events on `InspectionListView.ParamEditor` into a new `MainView.NotifyDatumParamMaybeChanged(DatumConfig)` bridge that gates on `IsConfigured && LastTeachSucceeded && halconViewer.CurrentImage != null` and re-invokes `InvokeTryTeachDatumForEdit`, removing the UAT Test 6 (minor) workaround that required nudging the ROI to refresh detection after a per-ROI edge parameter change.

## Files Changed

| File | Change | New Members |
| ---- | ------ | ----------- |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | +11 lines | `public void NotifyDatumParamMaybeChanged(DatumConfig datum)` (4 guards: null / IsConfigured / LastTeachSucceeded / image-null) inserted directly above existing Phase 13 D-03 `InvokeTryTeachDatumForEdit` (L632) |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | +32 lines | `using System.Windows.Controls.Primitives;` import + `private void TryTriggerDatumAutoReteach()` helper + `OnParamEditorLostFocus` / `OnParamEditorSelectionChanged` handlers + 2 `ParamEditor.AddHandler(..., handledEventsToo: true)` registrations inside `ListView_Loaded` try block |

## UAT Test 6 (minor) Mapping

**Symptom (pre-13-06):** PropertyGrid 에서 Datum ROI 의 per-ROI 에지 파라미터(`Line1_EdgeThreshold`, `Line2_Sigma`, `Circle_EdgeDirection` 등 6×5=30 필드)를 수정해도 검출 라인/raw 점 분포가 갱신되지 않음. 사용자가 ROI 를 1 px 살짝 이동시켜야만 `HandleDatumRoiMove → InvokeTryTeachDatumForEdit` 경로로 재티칭됨 — UX 갭.

**Fix (Plan 13-06):**

```
사용자가 PropertyGrid TextBox 편집 → 다른 셀 클릭
    └─ WPF default UpdateSourceTrigger=LostFocus
        └─ binding push → DatumConfig.Line1_EdgeThreshold = 새 값
            └─ TextBoxBase.LostFocusEvent (routed) bubble 까지
                └─ InspectionListView.OnParamEditorLostFocus
                    └─ TryTriggerDatumAutoReteach()
                        └─ as DatumConfig 캐스트 (DatumConfig 만 통과, FAIConfig/MeasurementBase noop)
                            └─ mainView.NotifyDatumParamMaybeChanged(datum)
                                └─ 4 guard 통과 시 InvokeTryTeachDatumForEdit(datum)
                                    └─ DatumFindingService.TryTeachDatum + label_drawHint + SetDatumOverlay
                                        → 라인 외삽 + raw 점 + 좌표 라벨 즉시 갱신
```

ComboBox 경로는 동일하나 `Selector.SelectionChangedEvent` 가 트리거 (binding push 즉시).

## Approach Rationale

**Routing 옵션 2 채택 — PropertyGrid routed event 게이트:**
- ParamEditor 에 `TextBoxBase.LostFocus` + `Selector.SelectionChanged` routed event 2건만 등록 (handledEventsToo=true)
- 게이트는 `ParamEditor.SelectedObject as DatumConfig` 결과 + `IsConfigured + LastTeachSucceeded` — 활성 세션 변수 `_editingDatum` 의존 X (티칭 완료 후 일반 편집에도 동작)
- 변경 면적: 2 파일 / +43 라인 / 신규 public 1 + private 3
- 회귀 위험: FAIConfig/MeasurementBase 편집 시 `as DatumConfig` 가 null → 조기 return → MainView 호출 0건

**대안 배제 사유 — DatumConfig 36 auto-property → manual + RaisePropertyChanged 변환:**
- 36 필드 × set 블록 갱신 + INotifyPropertyChanged hook → 변경 면적 과대
- ParamBase 의 INI 직렬화는 reflection 기반이라 manual property 도 동작하나, getter/setter 6배 코드 증가 — 회귀 위험 증가
- `LastTeachSucceeded` 같은 비-PropertyGrid 노출 필드는 변환 불필요 → 모든 36 필드를 동일하게 처리할지 일부만 처리할지 분리 부담

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
→ exit 0
→ DatumMeasurement -> WPF_Example/bin/x64/Debug/DatumMeasurement.exe

신규 warning (MainView.xaml.cs / InspectionListView.xaml.cs 대상): 0
기존 warning (out of scope):
  - VirtualCamera.cs(266,13) CS0162 접근할 수 없는 코드 — Phase 13-06 scope 밖
  - VisionAlgorithmService.cs(64,22) CS0219 'scanHorizontal' 할당 미사용 — Phase 13-06 scope 밖
  - MSB3884 MinimumRecommendedRules.ruleset 미발견 — 빌드 환경 설정 (전 phase 동일)
```

## Verification (Automated grep checks)

| # | Check | Expected | Actual |
| - | ----- | -------- | ------ |
| 1 | `public void NotifyDatumParamMaybeChanged(DatumConfig datum)` in MainView.xaml.cs | 1 | 1 |
| 2 | `InvokeTryTeachDatumForEdit(datum)` in MainView.xaml.cs | ≥1 신규 (총 2; HandleDatumRoiMove 1 + 신규 1) | 2 |
| 3 | NotifyDatumParamMaybeChanged 메서드 내 `halconViewer.CurrentImage` | 1 | 1 |
| 4 | NotifyDatumParamMaybeChanged 메서드 내 `LastTeachSucceeded` | 1 | 1 |
| 5 | `using System.Windows.Controls.Primitives;` in InspectionListView.xaml.cs | 1 | 1 |
| 6 | `private void TryTriggerDatumAutoReteach()` | 1 | 1 |
| 7 | `private void OnParamEditorLostFocus(` | 1 | 1 |
| 8 | `private void OnParamEditorSelectionChanged(` | 1 | 1 |
| 9 | `ParamEditor.AddHandler(TextBoxBase.LostFocusEvent` | 1 | 1 |
| 10 | `ParamEditor.AddHandler(Selector.SelectionChangedEvent` | 1 | 1 |
| 11 | `mv.NotifyDatumParamMaybeChanged(datum)` | 1 | 1 |
| 12 | `ParamEditor.SelectedObject as DatumConfig` | 1 | 1 |
| 13 | `//260426 hbk Phase 13-06` 주석 in InspectionListView.xaml.cs | ≥4 | 5 |
| 14 | msbuild Debug/x64 exit 0 | 0 | 0 |

## Deviations from Plan

**Plan Adherence: All tasks executed as specified.**

- Task 1 inserted exactly above L632 Phase 13 D-03 comment with K&R brace style.
- Task 2 added single `using` import (`System.Windows.Controls.Primitives`) — `ReringProject.Sequence` already present at L10 (skipped per plan note).
- Helper + 2 handlers inserted directly after `RefreshParamEditor()` (before `_isControlLoaded`).
- `AddHandler` 2건 registered inside `ListView_Loaded` try block, after `ViewModel.RootModel.ExpandAll();` (per plan "L86 직전 또는 직후").

## Self-Check

- [x] Task 1 commit `483ecb7` (`feat(phase-13-06): add MainView.NotifyDatumParamMaybeChanged bridge`) exists
- [x] Task 2 commit `514fcbf` (`feat(phase-13-06): wire PropertyGrid LostFocus/SelectionChanged to Datum auto re-teach`) exists
- [x] `MainView.NotifyDatumParamMaybeChanged` public, K&R, 4 guards (null / IsConfigured / LastTeachSucceeded / image-null)
- [x] `InspectionListView.TryTriggerDatumAutoReteach` private helper + 2 routed event handlers (LostFocus / SelectionChanged)
- [x] `using System.Windows.Controls.Primitives;` 추가
- [x] `ParamEditor.AddHandler(..., handledEventsToo: true)` 2건 inside `ListView_Loaded` try block
- [x] DatumConfig.cs / DatumFindingService.cs / HalconDisplayService.cs / *.xaml untouched (verified via `git diff --stat`)
- [x] FAIConfig/MeasurementBase 편집 시 `as DatumConfig` null → 조기 return (회귀 위험 0, 코드 리뷰)
- [x] 주석 convention `//260426 hbk Phase 13-06` 모든 신규/수정 라인 준수 (1 in MainView + 5 in InspectionListView)
- [x] msbuild Debug/x64 exit 0, 신규 warning 0
- [x] C# 7.2 제약 준수 (`as` 캐스트, `var`, no switch expression / record / nullable refs)
- [x] K&R brace style 양쪽 파일 일관

## Self-Check: PASSED

## Phase 14 Cross-Reference

본 13-06 은 UAT minor 1건 (Test 6) 단독 갭 클로저다. UAT 에서 발견된 major 3건 + carry-over 는 Phase 14 신규 spec 으로 이관:

- **Test 1 (cold start)**: Circle ROI 이동 회귀 + Vertical 파라미터 그룹 미노출 — Phase 14
- **Test 3 (btn_testFindDatum)**: TwoLineIntersect 외 알고리즘 시행 불가 + 각도 out-of-range UX 갭 — Phase 14
- **Circle 알고리즘 재설계** (CircleTwoHorizontal / VerticalTwoHorizontal 시행 불가 원인 조사 포함) — Phase 14
- **Datum ROI Edit/Resize** (13-05 carry-over, 단순 이동 외 핸들 기반 resize) — Phase 14
- **Circle raw 점 시각화** (VisionAlgorithmService.TryFindCircle row/col 미반환 → 빈 HTuple) — Phase 14

Phase 13 (5/5 plans) 은 13-06 완료로 모든 plan + UAT minor 갭까지 closeout. 다음 액션: `/gsd-spec-phase 14` 또는 `/gsd-verify-work 13`.
