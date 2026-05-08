---
phase: quick-260508-mcb
slug: 260508-mcb-measurement-combo
date: 2026-05-08
status: complete
commits: []
files_added:
  - WPF_Example/UI/Dialog/ComboInputBox.cs
  - WPF_Example/UI/Dialog/ComboInputBoxWindow.xaml
  - WPF_Example/UI/Dialog/ComboInputBoxWindow.xaml.cs
files_modified:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
  - WPF_Example/DatumMeasurement.csproj
related: phase-19
---

# Quick 260508-mcb — Measurement 추가 모달 콤보화

## Trigger
Phase 19 UAT 중 사용자 보고: 측정 타입 입력 모달이 자유 텍스트 입력으로 되어 있어 콤보박스로 교체 요청. Phase 19 closure 후 별도 quick task 로 처리하기로 결정.

## Symptom
`InspectionListView.AddMeasurementToFAI` 가 `TextInputBox.Show("Measurement 타입 입력 (사용 가능한 타입: ...)")` 로 자유 텍스트 입력. 사용자가 오타 입력 시 `MeasurementFactory.Create(typeName)` 가 null 반환 → CustomMessageBox 오류 모달.

## Fix
1. **신규 다이얼로그**: `ComboInputBoxWindow.xaml/.cs` — TextInputBoxWinidow 와 동일 레이아웃에 ComboBox 만 교체. KeyUp Enter, IsVisibleChanged Focus 동작 그대로.
2. **신규 헬퍼**: `ComboInputBox.cs` 정적 클래스 — TextInputBox 패턴과 동일한 시그니처 + items 인자 추가:
   ```csharp
   ComboInputBox.Show(string title, IEnumerable<string> items, string initialSelection, out string selectedText)
   ```
3. **콜사이트 변경**: `AddMeasurementToFAI` 가 ComboInputBox 호출, items = `MeasurementFactory.GetTypeNames()` 단일 소스.
4. **csproj 등록**: ComboInputBox.cs / ComboInputBoxWindow.xaml.cs (DependentUpon) / ComboInputBoxWindow.xaml (Page).

## Verification
- msbuild Debug/x64 PASS — 신규 error/warning 0 (pre-existing 5건만 잔존)
- 정적: `ComboInputBox.Show` 시그니처 + ComboBox SelectedItem 강제 → 자유 텍스트 입력 경로 제거됨
- 런타임: 사용자 UAT 필요 (FAI 노드 클릭 → "Add" 버튼 → 콤보박스 모달 → 6 옵션 드롭다운 → 선택 → measurement 추가)

## Notes
- `MeasurementFactory.GetTypeNames()` 단일 소스 정책 유지 (Phase 19 와 동일).
- TextInputBox 와 ComboInputBox 가 공존 — 자유 텍스트(이름 입력 등) 는 TextInputBox, 옵션 강제 입력은 ComboInputBox 로 역할 분리.
- 향후 다른 자유 텍스트 입력 모달 중 옵션이 정해진 곳(예: AlgorithmType 변경 다이얼로그가 별도로 있으면) 동일 패턴 적용 가능.

## Out-of-scope
- FAI CircleDiameter 측정에 Datum Circle 알고리즘 적용 — 별도 Phase 28 후보 (사용자 명시 요청).
- 다른 TextInputBox 사용처 일괄 교체 — 자유 텍스트 입력 위주이므로 그대로 유지.
