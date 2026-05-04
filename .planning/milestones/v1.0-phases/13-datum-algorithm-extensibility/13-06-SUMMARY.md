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
hot_fix: 13-07 (commit 9d34426 — UAT-driven cascade + recovery fix; see addendum below)
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

---

## 13-07 Hot-Fix Addendum (2026-04-26)

**Trigger:** `/gsd-verify-work 13` 의 13-06 retest 6 시나리오 진행 중 발견된 회귀 2건. 사용자 합의로 즉시 hot-fix 적용 (별도 plan 파일 미생성, 13-06 SUMMARY 에 folded — Phase 12 UAT Gap-2/3 fix 12-03 SUMMARY folded 패턴과 동일).

**Commit:** `9d34426` `fix(phase-13-07): hot-fix 13-06 UAT regressions — cascade + recovery`

### Issue A: Test E 회귀 (major)

**Symptom:** Datum 노드에서 FAI 트리 노드를 클릭해도 PropertyGrid 가 Datum 에서 안 바뀜 (Datum 안 만져도 항상 실패 — 사용자 (b) 패턴).

**Root cause:** `Selector.SelectionChangedEvent` 가 PropertyGrid 내부 navigation Selector (카테고리 ListBox 등) 에서도 fire 되어 ParamEditor 까지 bubble. 사용자가 트리에서 FAI 클릭 → SelectedObject 가 Datum→FAI 로 전환되는 *중간 시점* 에 internal Selector.SelectionChanged 가 fire → 우리 핸들러가 invoke → SelectedObject 가 아직 Datum 인 상태에서 동기 Halcon TryTeachDatum (50~200ms) 호출 → UI 스레드 블록 → PropertyGrid 의 binding 전환이 중간에 깨짐 → FAI 표시 실패.

**Fix:**
- `OnParamEditorLostFocus` 에 `if (!(e.OriginalSource is TextBoxBase)) return;` 가드 추가 — 헤더/네비게이션 LostFocus 차단
- `OnParamEditorSelectionChanged` 에 `if (!(e.OriginalSource is ComboBox)) return;` 가드 추가 — PropertyGrid 내부 카테고리 ListBox 등 차단
- `TryTriggerDatumAutoReteach` 의 SelectedObject 검사 + Halcon 호출 부분을 `Dispatcher.BeginInvoke(DispatcherPriority.Background)` 로 wrap — binding 전환이 끝난 후 재검사. 정말 ComboBox 변경 시점에는 동일 시점에 binding 도 push 완료되어 Background 우선순위로 즉시 실행됨.

### Issue B: Test D recovery 갭 (minor)

**Symptom:** "되는걸 안되게" 변경(파라미터를 극단값으로) → 자동 재티칭 fail → LastTeachSucceeded=false. 그 후 "안되는걸 되게" (정상값으로 되돌림) → 자동 재티칭이 안 발동, ROI 를 손으로 클릭/이동해야 회복.

**Root cause:** `MainView.NotifyDatumParamMaybeChanged` 의 가드 `if (!datum.IsConfigured || !datum.LastTeachSucceeded) return;` 의 `LastTeachSucceeded` 조건이 회복 경로를 차단. fail 상태에서는 어떤 파라미터 변경도 자동 재티칭으로 이어지지 않음.

**Fix:** 가드를 `if (!datum.IsConfigured) return;` 로 완화 — ROI 가 그려져 있으면(IsConfigured=true) fail 상태(LastTeachSucceeded=false) 에서도 재시도 허용. fail→success 회복 경로 복구.

### Files Changed

| File | Change |
| ---- | ------ |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | +using System.Windows.Threading + 2 OriginalSource 필터 라인 + Dispatcher.BeginInvoke(Background) defer (+19 / -7) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | NotifyDatumParamMaybeChanged 의 LastTeachSucceeded 가드 라인 1줄 제거 + 주석 보강 (+4 / -2) |

### Build & Verification

- msbuild Debug/x64 exit 0
- 신규 warning 0 (기존 VirtualCamera CS0162 / VisionAlgorithmService CS0219 / MSB3884 동일)
- Retest 6 시나리오 (B/C/D/E/F/G) 전수 PASS — 13-UAT.md "13-06 + 13-07 Retest" 섹션 참조

### Self-Check

- [x] OriginalSource 필터 (`is TextBoxBase` / `is ComboBox`) 적용 — PropertyGrid 내부 Selector / 헤더 LostFocus 차단
- [x] `Dispatcher.BeginInvoke(DispatcherPriority.Background)` 로 binding 전환 후 재검사 안전 마진
- [x] `LastTeachSucceeded` 가드 제거 — fail→success 회복 경로 복구 (`IsConfigured` 만 유지)
- [x] FAI 편집 회귀 없음 (재검증) — `as DatumConfig` 캐스트가 여전히 1차 게이트
- [x] 미티칭 / 이미지 미로드 가드 정상 동작 (Test F/G PASS)
- [x] 주석 convention `//260426 hbk Phase 13-07` 신규/수정 라인 준수
- [x] 단일 commit (9d34426) — feat 가 아닌 fix 컨벤션 (UAT-driven hot-fix)
- [x] 13-UAT.md 갱신: Test 6 result issue → pass, Sub-test B/C/D/E/F/G PASS 섹션 추가, Summary count 4→5/3→2

### Self-Check: PASSED
