---
phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix
plan: 02
type: summary
wave: 1
status: complete
requirements: [R3, R4]
commits:
  - c51b297  # feat(16-02): InspectionListView Datum SelectedObject force rebind (D-09/D-10)
  - a46d86d  # feat(16-02): MainView Auto-reteach off — HandleDatumRoiMove/Resize 자동 호출 삭제 (D-13)
key-files:
  modified:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
  diff_zero_verified:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
---

# Plan 16-02 Summary — Datum AlgorithmType binding 결함 + Auto-reteach off

## 변경 라인 수
- InspectionListView.xaml.cs: +13 / -0 (Task 1, force rebind 블록 추가)
- MainView.xaml.cs: +17 / -21 (Task 2, 자동 재티칭 블록 제거 + noop)
- 알고리즘 / 데이터 모델: 0/0/0 (D-22 보존)

## Task 1 — InspectionListView Datum SelectedObject force rebind (R3)

**커밋**: c51b297

`InspectionList_SelectionChanged` 의 Datum 분기 (`if (itemParam is DatumConfig datumCfg)` 블록) 안에 PropertyGrid SelectedObject 강제 null → datumCfg 두 단계 force rebind 추가 (line 354-365).

**근본 원인**: Phase 15 UAT Test 10/11/12 — ROI 편집 후 Datum 전환 시 AlgorithmType combobox 가 stale 상태 유지. `RaisePropertyChanged(string.Empty)` + `RefreshParamEditor()` 만으로는 string property 의 combobox binding 까지 닿지 않음.

**해법 (D-09 / D-10)**:
```csharp
if (ParamEditor != null) {
    ParamEditor.SelectedObject = null;     // 기존 binding 강제 해제
    ParamEditor.SelectedObject = datumCfg; // 새 인스턴스 재할당
}
```
편집 모드 (btn_teachDatum.IsChecked) 와 무관하게 매 노드 클릭마다 실행 (D-11). AlgorithmType combobox 변경 자체는 자동 재티칭 추가 안 함 (D-12, D-13 일관).

## Task 2 — MainView Auto-reteach off (R4)

**커밋**: a46d86d

세 메서드 직접 수술 (`Dispatcher.BeginInvoke` 자동 재티칭 블록 직접 삭제 + noop):

### HandleDatumRoiMove
- `Dispatcher.BeginInvoke(Background, () => { InvokeTryTeachDatumForEdit(...); ... })` 블록 → inline `PublishDatumRoiCandidates(datum); UpdateDatumRefCoordsLabel(datum);`
- D-13 verbatim: 자동 재티칭 자체 제거하므로 Phase 14-01 D-03 의 Background defer 패턴은 무의미해짐
- D-14: ROI 이동 후 LastTeachSucceeded 변경되지 않음 → 검출 원 / center 시각화는 stale 데이터 그대로 표시 (사용자가 mismatch 인지)

### HandleDatumRoiResize
- 동일 패턴. 자동 재티칭 호출 라인 삭제, inline 호출만 유지.

### NotifyDatumParamMaybeChanged
- 본문 noop (`return;`). 시그니처 보존 — 호출처 (`InspectionListView.TryTriggerDatumAutoReteach` 등) 회귀 방지.
- D-12 / D-13 일관성: PropertyGrid 파라미터 변경 (EdgeDirection / EdgePolarity / AlgorithmType / RectL1Ratio 등) 시 자동 재티칭 안 함.

## 보존 (변경 0)
- `InvokeTryTeachDatum` (수동 btn_teachDatum 트리거, line 1460) — D-15 / D-16 보존
- `InvokeTryTeachDatumForEdit` 메서드 본문 (line 705-728) — Reflection / 외부 호출 회귀 방지로 시그니처 / 본문 모두 보존, **호출처만 0**
- VisionAlgorithmService.cs / DatumFindingService.cs / DatumConfig.cs — git diff 0

## 빌드 검증
- `msbuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`: PASS
- InspectionListView.xaml.cs / MainView.xaml.cs 신규 warning: 0

## Plan 16-03 UAT 인계 메모

### Phase 15 UAT carry-over (이번 phase 에서 흡수)
- Test 5 / 10 / 11 / 12: AlgorithmType binding force rebind 로 해결 예상 — UAT 재실행
- Test 6 / 7 / 8: ROI 이동 / Datum 전환 시나리오 — Auto-reteach off 정책으로 동작이 명시적으로 변경됨 (이전 자동 재티칭 → 수동 btn_teachDatum)

### Phase 16 신규 UAT (4건)
- D-13: ROI 이동 5회 시 HALCON edge measurement 자동 호출 0회 (Trace 로그 검증)
- D-13: btn_teachDatum 1회 클릭 시 InvokeTryTeachDatum 1회 실행 (수동 트리거 보존)
- D-09 / D-10: ROI 이동 후 Datum 1 → 2 → 3 클릭 시 AlgorithmType combobox 매번 즉시 갱신
- D-14: ROI 이동 후 검출 원 / center 시각화는 stale 좌표 (사용자가 mismatch 인지)
