---
quick_id: 260526-ilp
slug: co-31-01-propertygrid-inotifypropertycha
date: 2026-05-26
status: complete
tags: [quick, co-31-01, propertygrid, inotifypropertychanged, tree-binding]
description: "CO-31-01 PropertyGrid 양방향 즉시 갱신 — 4 Param Name PropertyChanged 발화 + NodeViewModel Param 구독"
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  - WPF_Example/UI/ControlItem/NodeViewModel.cs
commits:
  - daeb195  # Task 1 — 4 Param Name setter INotifyPropertyChanged 발화 패턴
  - 66b20bc  # Task 2 — NodeViewModel Param PropertyChanged 구독 + Node.Name 동기화
duration: ~20min
---

# Quick Task 260526-ilp Summary

**CO-31-01 PropertyGrid 양방향 즉시 갱신 — INotifyPropertyChanged 패턴 도입으로 PropertyGrid 의 DatumName/ShotName/FAIName/MeasurementName 편집이 트리 헤더에 즉시 반영되도록 wire-up. ParamBase 기반 INotifyPropertyChanged 인프라 재사용 (이미 구현됨) + NodeViewModel 에 Param PropertyChanged 구독 핸들러 추가. msbuild Debug/x64 PASS, 신규 warning 0.**

## What Changed

### Task 1: 4 Param 클래스 Name setter PropertyChanged 발화 (commit `daeb195`)

| 파일 | 프로퍼티 | 변경 |
|------|----------|------|
| `DatumConfig.cs` | `DatumName` | `{ get; set; } = "Datum_1";` → backing field `_datumName` + setter (equal-skip + RaisePropertyChanged) |
| `ShotConfig.cs` | `ShotName` | `{ get; set; }` → backing field `_shotName` + setter |
| `FAIConfig.cs` | `FAIName` | `{ get; set; }` → backing field `_faiName` + setter |
| `MeasurementBase.cs` | `MeasurementName` | `{ get; set; } = "";` → backing field `_measurementName` + setter |

각 setter 패턴:
```csharp
public string XxxName {
    get { return _xxxName; }
    set {
        if (_xxxName == value) return;  // redundant write skip
        _xxxName = value;
        RaisePropertyChanged(nameof(XxxName));
    }
}
```

기존 Category / Browsable attributes / 기본값 모두 보존. INI 직렬화 영향 0 (ParamBase Reflection 은 public XxxName property 만 봄).

### Task 2: NodeViewModel — Param PropertyChanged 구독 + Node.Name 동기화 (commit `66b20bc`)

- `using System.ComponentModel;` 추가
- `using ReringProject.Sequence;` 추가 (DatumConfig/ShotConfig/FAIConfig/MeasurementBase 식별)
- Constructor: `Node.ParamData is INotifyPropertyChanged` 면 PropertyChanged 구독
- `OnParamPropertyChanged(object sender, PropertyChangedEventArgs e)` private handler:
  - switch on `e.PropertyName`: 4 케이스 분기 (DatumName/ShotName/FAIName/MeasurementName)
  - 각 케이스에서 sender cast + newName 추출
  - **MeasurementName 폴백:** `string.IsNullOrEmpty(m.MeasurementName) ? m.TypeName : m.MeasurementName` (InspectionListViewModel L100 와 일치)
  - `Node.Name = newName` + `RaisePropertyChanged("Name")` 으로 트리 헤더 즉시 갱신 발화

## Verification

### Build
- msbuild Debug/x64 Rebuild PASS
- Errors: **0**
- Warnings: 4 (= 2 unique × 2 builds, 신규 0)
  - MSB3884 (환경 — MinimumRecommendedRules.ruleset 누락)
  - CS0162 (VirtualCamera.cs:266 unreachable — 이번 quick 미수정 파일)
- 경과 시간: 00:00:04.82

### Grep
- `RaisePropertyChanged(nameof(DatumName))` × 1 in DatumConfig.cs ✓
- `RaisePropertyChanged(nameof(ShotName))` × 1 in ShotConfig.cs ✓
- `RaisePropertyChanged(nameof(FAIName))` × 1 in FAIConfig.cs ✓
- `RaisePropertyChanged(nameof(MeasurementName))` × 1 in MeasurementBase.cs ✓
- `OnParamPropertyChanged` × 1 method definition in NodeViewModel.cs ✓
- `PropertyChanged += OnParamPropertyChanged` × 1 in NodeViewModel ctor ✓

## What This Fixes

**Before:** PropertyGrid 에서 DatumName/ShotName/FAIName/MeasurementName 수정 → 메모리 값은 변경되나 트리 노드 헤더는 그대로. 다른 노드 클릭 후 돌아오거나 recipe 재로드 시점에야 갱신.

**After:** PropertyGrid 에서 Name 류 4종 수정 → setter 가 PropertyChanged 발화 → NodeViewModel 핸들러가 Node.Name 동기화 + `RaisePropertyChanged("Name")` → TreeListBox 의 `{Binding Name}` 가 즉시 갱신.

## Decisions

- **backing field 패턴 선택 근거:** ParamBase 의 Reflection 직렬화 (Save/Load) 가 public property 만 보므로 backing field 추가가 INI 호환성에 영향 0. 신규 GetProperties 결과에 `_xxxName` 가 포함되지 않음 (private field).
- **equal-skip (`if (_xxxName == value) return;`):** PropertyGrid 가 unchanged 값 재할당 시 불필요한 PropertyChanged 발화 차단 — 트리 갱신 cost 절약.
- **MeasurementName TypeName 폴백:** InspectionListViewModel L100 의 기존 로직과 1:1 일치 — Measurement 이름을 빈 문자열로 클리어해도 트리는 TypeName (예: "EdgeToLineDistance") 표시.
- **unsubscribe 미구현:** NodeViewModel 와 Param 수명 동일 (recipe 재로드 시 둘 다 dispose). singleton/long-lived 아니므로 메모리 누수 위험 0.
- **using ReringProject.Sequence;** 추가: NodeViewModel 가 UI 레이어이지만 Param 타입 식별을 위해 Sequence 네임스페이스 import. 의존 방향이 UI → Sequence 로 유지 (역방향 회피).

## Threats Mitigated

| Threat ID | Disposition | Result |
|-----------|-------------|--------|
| T-Q-01 (메모리 누수 — 구독 후 미해제) | accept | NodeViewModel 와 Param 수명 동일 |
| T-Q-02 (INI Reflection 직렬화 회귀) | mitigate | backing field private → public property 만 Reflection 대상 |
| T-Q-03 (equality 검사 false negative) | accept | string equality content + reference 양쪽 정상 |

## Next Steps

- **사용자 SIMUL UAT 필요:** SIMUL_MODE 에서 PropertyGrid 의 DatumName/ShotName/FAIName/MeasurementName 수정 → 트리 헤더 즉시 갱신 확인
- **카운터 측 (Tree → PropertyGrid):** XAML SelectedObject binding 이 이미 SelectedItem.Param 으로 매핑되어 있어, NodeViewModel 의 PropertyChanged("Name") 이 Tree selection 변경 cascade 를 처리. 별도 fix 불필요.
- 만약 사용자 UAT 에서 (Tree → PropertyGrid) 갱신이 여전히 안 되면 별도 보조 quick 필요 (PropertyGrid Refresh 호출 명시).
