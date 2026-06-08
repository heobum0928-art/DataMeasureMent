---
quick_id: 260526-ilp
slug: co-31-01-propertygrid-inotifypropertycha
type: quick
date: 2026-05-26
description: "CO-31-01 PropertyGrid 양방향 즉시 갱신 — 4 Param Name PropertyChanged 발화 + NodeViewModel Param 구독"
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  - WPF_Example/UI/ControlItem/NodeViewModel.cs

must_haves:
  truths:
    - "PropertyGrid 에서 DatumName/ShotName/FAIName/MeasurementName 수정 시 트리 헤더가 다음 UI 이벤트 사이클에 즉시 갱신된다"
    - "기존 Rename 버튼 (Btn_RenameFAI_Click) 흐름도 정상 작동 (회귀 없음)"
    - "MeasurementName 이 빈 문자열로 클리어되면 트리 헤더가 TypeName 폴백 (기존 InspectionListViewModel L100 로직과 일치)"
    - "msbuild Debug/x64 PASS, 신규 warning 0"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "DatumName setter PropertyChanged 발화"
    - path: "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs"
      provides: "ShotName setter PropertyChanged 발화"
    - path: "WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs"
      provides: "FAIName setter PropertyChanged 발화"
    - path: "WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs"
      provides: "MeasurementName setter PropertyChanged 발화"
    - path: "WPF_Example/UI/ControlItem/NodeViewModel.cs"
      provides: "Param PropertyChanged 구독 + Node.Name 동기화 핸들러"
  key_links:
    - from: "PropertyGrid Name 편집"
      to: "Tree 헤더 갱신"
      via: "Param.SetXxxName setter → RaisePropertyChanged → NodeViewModel.OnParamPropertyChanged → Node.Name 갱신 → RaisePropertyChanged('Name')"
      pattern: "INotifyPropertyChanged"
---

# Quick Task 260526-ilp: CO-31-01 PropertyGrid 양방향 즉시 갱신

## Background

Phase 31 sign-off UAT (2026-05-26) 중 Test 8 부수 발견:
- (a) Tree → PropertyGrid: 노드 선택 시 PropertyGrid 즉시 안 바뀜
- (b) PropertyGrid → Tree: Name 변경 시 트리 헤더에 즉시 반영 안 됨

**Root cause:** `DatumConfig.DatumName` / `ShotConfig.ShotName` / `FAIConfig.FAIName` / `MeasurementBase.MeasurementName` 4종이 모두 plain `{ get; set; }` auto-property — `INotifyPropertyChanged.PropertyChanged` 발화 없음.

**조사 결과:**
- `ParamBase` (L34) 가 이미 `INotifyPropertyChanged` 구현 + `RaisePropertyChanged(string)` helper 보유 (L113-117)
- 4 Param 클래스 모두 `ParamBase` 상속 — Name 프로퍼티 setter 만 `RaisePropertyChanged` 패턴으로 교체하면 됨
- Tree 의 NodeViewModel.Name 은 `Node.Name` 을 반환 (NodeViewModel.cs L109-117) — Param 의 Name 변경이 Node.Name 까지 전파되어야 함
- InspectionListViewModel L72/L93/L100 가 노드 생성 시 Param.Name → Node.Name 1회 복사 (단방향)

**Fix 접근:**
1. 4 Param 클래스 Name 프로퍼티 → backing field + RaisePropertyChanged 패턴
2. NodeViewModel ctor 에서 `Node.ParamData` 가 `INotifyPropertyChanged` 면 구독 + Name 류 프로퍼티 변경 시 Node.Name 동기화 + RaisePropertyChanged("Name")
3. MeasurementBase 케이스: `MeasurementName` 비어있으면 `TypeName` 폴백 (InspectionListViewModel L100 와 일치)

## Tasks

### Task 1: 4 Param 클래스 Name setter PropertyChanged 발화 패턴 교체

**Files:**
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:19` (DatumName)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:34` (ShotName)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:103` (FAIName)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:21` (MeasurementName)

**Action:** 각 Name 프로퍼티를 다음 패턴으로 교체:

```csharp
//260526 hbk CO-31-01 — INotifyPropertyChanged 발화로 트리 헤더 즉시 갱신
private string _xxxName = "<default>";
public string XxxName {
    get { return _xxxName; }
    set {
        if (_xxxName == value) return;
        _xxxName = value;
        RaisePropertyChanged(nameof(XxxName));
    }
}
```

기존 Category/Browsable attributes 보존. DatumConfig 의 `= "Datum_1"` 기본값, MeasurementBase 의 `= ""` 기본값 보존.

**Verify:** 각 파일에 `RaisePropertyChanged(nameof(XxxName))` 1 hit grep.

### Task 2: NodeViewModel — Param PropertyChanged 구독 + Node.Name 동기화

**File:** `WPF_Example/UI/ControlItem/NodeViewModel.cs`

**Action:**
1. `using ReringProject.Sequence;` import 추가 (DatumConfig/ShotConfig/FAIConfig/MeasurementBase 타입 식별)
2. Constructor (L194) 에 PropertyChanged 구독 추가
3. private handler `OnParamPropertyChanged(object sender, PropertyChangedEventArgs e)` 추가:
   - switch on e.PropertyName: DatumName/ShotName/FAIName/MeasurementName 분기
   - 각 case 에서 sender as Target → newName 추출
   - MeasurementBase 케이스: `string.IsNullOrEmpty(m.MeasurementName) ? m.TypeName : m.MeasurementName`
   - `this.Node.Name = newName; RaisePropertyChanged("Name");`

**Verify:**
- `OnParamPropertyChanged` 메서드 grep 1 hit
- Constructor 에 PropertyChanged 구독 라인 grep 1 hit

### Task 3: msbuild Debug/x64 검증

**Action:** Phase 21 baseline 와 동일한 명령:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
    'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' `
    -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:m -clp:Summary
```

**Verify:**
- Errors = 0
- Warnings 신규 = 0 (Phase 21 baseline 2: MSB3884 + CS0162 유지)

## Verification

- Task 1: 4 파일 각각 `RaisePropertyChanged(nameof(...))` 1 hit grep PASS
- Task 2: NodeViewModel.cs `OnParamPropertyChanged` 1 hit + `PropertyChanged +=` 1 hit grep PASS
- Task 3: msbuild Debug/x64 PASS, 신규 warning 0
- (사용자 UAT — quick task 후 별도) SIMUL_MODE 에서 PropertyGrid 의 DatumName/ShotName/FAIName/MeasurementName 수정 → 트리 헤더 즉시 갱신 확인

## Threat Model

| Threat ID | Category | Component | Disposition | Mitigation |
|-----------|----------|-----------|-------------|------------|
| T-Q-01 | Memory leak | NodeViewModel PropertyChanged 구독 후 미해제 | accept | NodeViewModel 와 Param 수명 동일 (recipe 재로드 시 둘 다 dispose). singleton/long-lived 아님. |
| T-Q-02 | INI hosting 회귀 | ParamBase Reflection 직렬화 (Save/Load) 가 backing field 가 아닌 public property 만 봄 | mitigate | private backing field 사용 — Reflection 은 public XxxName property 만 잡아냄, 그대로 직렬화. |
| T-Q-03 | Equality 검사 false negative | string == null vs "" 차이로 setter 가 redundant fire | accept | string equality 는 reference + content 둘 다 매치 — null/empty 모두 정상 동작. |
