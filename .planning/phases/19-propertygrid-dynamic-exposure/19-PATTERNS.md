# Phase 19: PropertyGrid 동적 노출 일반화 — Pattern Map

**Mapped:** 2026-05-07
**Files analyzed:** 4 (1 new, 3 modified)
**Analogs found:** 4 / 4

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs` | utility | transform | `DatumConfig.cs` GetProperties block (lines 555–585) | exact (extracted from) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model | CRUD | self (refactor only — existing GetProperties replaced with helper call) | exact |
| `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` | model | CRUD | `DatumConfig.cs` (ICustomTypeDescriptor 전체 패턴) | exact |
| `WPF_Example/DatumMeasurement.csproj` | config | — | `DatumMeasurement.csproj` lines 207–223 (Inspection 블록 끝) | exact |

---

## Pattern Assignments

### `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs` (utility, transform)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — GetProperties(Attribute[]) 블록 (lines 555–585) + 파일 헤더 관례

**Imports pattern** (DatumConfig.cs lines 1–6):
```csharp
using System.Collections.Generic;
using ReringProject.Utility;
// HalconDotNet / PropertyTools 불필요 — 헬퍼는 순수 TypeDescriptor 조작
```
DynamicPropertyHelper는 위 중 `System.Collections.Generic` 만 필요하다.
`HalconDotNet`, `PropertyTools`, `ReringProject.Utility` 의존 없음.

**Namespace 패턴** (DatumConfig.cs line 7):
```csharp
namespace ReringProject.Sequence {
```
같은 디렉터리(Custom/Sequence/Inspection/) 파일들은 모두 `namespace ReringProject.Sequence` 단일 네임스페이스를 사용한다. DynamicPropertyHelper도 동일.

**Class 선언 패턴** — static utility 선언 (MeasurementFactory.cs 참조):
```csharp
// MeasurementFactory.cs lines 8-9
public static class MeasurementFactory
{
    public static MeasurementBase Create(string typeName, object owner)
```
DynamicPropertyHelper도 `public static class DynamicPropertyHelper` + `public static` 메서드로 선언한다.

**Core pattern — FilterProperties 구현 원본** (DatumConfig.cs lines 555–585):
```csharp
// Phase 17 D-09 + Phase 18 CO-01 완성본
public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
    var all = System.ComponentModel.TypeDescriptor.GetProperties(this, attributes, true);
    var alg = AlgorithmTypeEnum;
    var keep = new List<System.ComponentModel.PropertyDescriptor>();
    foreach (System.ComponentModel.PropertyDescriptor pd in all) {
        if (IsHiddenForAlgorithm(pd.Name, alg)) continue;
        keep.Add(pd);
    }
    // Phase 18 CO-01: [Browsable(false)] 소스 프로퍼티 강제 추가
    var allNoFilter = System.ComponentModel.TypeDescriptor.GetProperties(this, true);
    var sourceNames = new System.Collections.Generic.HashSet<string> {
        nameof(AlgorithmTypeList),
        // ... 모든 *List 프로퍼티 이름
    };
    foreach (System.ComponentModel.PropertyDescriptor pd in allNoFilter) {
        if (sourceNames.Contains(pd.Name) && !keep.Exists(k => k.Name == pd.Name))
            keep.Add(pd);
    }
    return new System.ComponentModel.PropertyDescriptorCollection(keep.ToArray());
}
```

**DynamicPropertyHelper.FilterProperties 시그니처** (CONTEXT.md decisions 기준):
```csharp
/// <summary>
/// PropertyTools.Wpf PropertyGrid용 동적 필터링 헬퍼.
/// hideFunc이 true를 반환하는 프로퍼티를 제외하고, sourceNames 화이트리스트에 해당하는
/// [Browsable(false)] 소스 프로퍼티를 강제 포함한다 (Phase 18 CO-01 패턴).
/// </summary>
public static System.ComponentModel.PropertyDescriptorCollection FilterProperties(
    object obj,
    System.Attribute[] attrs,
    System.Func<string, bool> hideFunc,
    System.Collections.Generic.HashSet<string> sourceNames)
{
    var all  = System.ComponentModel.TypeDescriptor.GetProperties(obj, attrs, true);
    var keep = new System.Collections.Generic.List<System.ComponentModel.PropertyDescriptor>();
    foreach (System.ComponentModel.PropertyDescriptor pd in all) {
        if (hideFunc(pd.Name)) continue;
        keep.Add(pd);
    }
    // Phase 18 CO-01 — [Browsable(false)] ItemsSource 소스 프로퍼티 강제 포함
    var allNoFilter = System.ComponentModel.TypeDescriptor.GetProperties(obj, true);
    foreach (System.ComponentModel.PropertyDescriptor pd in allNoFilter) {
        if (sourceNames.Contains(pd.Name) && !keep.Exists(k => k.Name == pd.Name))
            keep.Add(pd);
    }
    return new System.ComponentModel.PropertyDescriptorCollection(keep.ToArray());
}
```

**파일 헤더 주석 패턴** (DatumConfig.cs line 1):
```csharp
//YYMMDD hbk Phase N: <설명> — <요구사항 ID>
```
예: `//260507 hbk Phase 19: DynamicPropertyHelper — FilterProperties 공통 헬퍼`

---

### `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (model, CRUD — 리팩토링)

**Analog:** self

**변경 범위:** GetProperties(Attribute[]) 메서드 내부만 교체 (lines 555–585). 나머지 클래스 구조 변경 없음.

**리팩토링 전 — 현재 GetProperties 본문** (DatumConfig.cs lines 555–585):
```csharp
public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
    var all = System.ComponentModel.TypeDescriptor.GetProperties(this, attributes, true);
    var alg = AlgorithmTypeEnum;
    var keep = new List<System.ComponentModel.PropertyDescriptor>();
    foreach (System.ComponentModel.PropertyDescriptor pd in all) {
        if (IsHiddenForAlgorithm(pd.Name, alg)) continue;
        keep.Add(pd);
    }
    var allNoFilter = System.ComponentModel.TypeDescriptor.GetProperties(this, true);
    var sourceNames = new System.Collections.Generic.HashSet<string> {
        nameof(AlgorithmTypeList),
        nameof(Circle_EdgeDirectionList), nameof(Circle_EdgePolarityList),
        nameof(Circle_EdgeSelectionList), nameof(Circle_RadialDirectionList),
        nameof(Horizontal_A_EdgeDirectionList), nameof(Horizontal_A_EdgePolarityList),
        nameof(Horizontal_A_EdgeSelectionList),
        nameof(Horizontal_B_EdgeDirectionList), nameof(Horizontal_B_EdgePolarityList),
        nameof(Horizontal_B_EdgeSelectionList),
        nameof(Line1_EdgeDirectionList), nameof(Line1_EdgePolarityList), nameof(Line1_EdgeSelectionList),
        nameof(Line2_EdgeDirectionList), nameof(Line2_EdgePolarityList), nameof(Line2_EdgeSelectionList),
        nameof(Vertical_EdgeDirectionList), nameof(Vertical_EdgePolarityList), nameof(Vertical_EdgeSelectionList),
    };
    foreach (System.ComponentModel.PropertyDescriptor pd in allNoFilter) {
        if (sourceNames.Contains(pd.Name) && !keep.Exists(k => k.Name == pd.Name))
            keep.Add(pd);
    }
    return new System.ComponentModel.PropertyDescriptorCollection(keep.ToArray());
}
```

**리팩토링 후 — 헬퍼 호출로 교체** (CONTEXT.md decisions 기준):
```csharp
public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
    var alg = AlgorithmTypeEnum;
    var sourceNames = new System.Collections.Generic.HashSet<string> {
        nameof(AlgorithmTypeList),
        nameof(Circle_EdgeDirectionList), nameof(Circle_EdgePolarityList),
        nameof(Circle_EdgeSelectionList), nameof(Circle_RadialDirectionList),
        nameof(Horizontal_A_EdgeDirectionList), nameof(Horizontal_A_EdgePolarityList),
        nameof(Horizontal_A_EdgeSelectionList),
        nameof(Horizontal_B_EdgeDirectionList), nameof(Horizontal_B_EdgePolarityList),
        nameof(Horizontal_B_EdgeSelectionList),
        nameof(Line1_EdgeDirectionList), nameof(Line1_EdgePolarityList), nameof(Line1_EdgeSelectionList),
        nameof(Line2_EdgeDirectionList), nameof(Line2_EdgePolarityList), nameof(Line2_EdgeSelectionList),
        nameof(Vertical_EdgeDirectionList), nameof(Vertical_EdgePolarityList), nameof(Vertical_EdgeSelectionList),
    };
    return DynamicPropertyHelper.FilterProperties(this, attributes, name => IsHiddenForAlgorithm(name, alg), sourceNames);
}
```

**보존 불변 항목:**
- `IsHiddenForAlgorithm(string name, EDatumAlgorithm alg)` static 메서드 — DatumConfig에 그대로 유지 (헬퍼 콜백으로 전달)
- `GetProperties()` 무인자 오버로드 (line 587–590) — 변경 없음
- ICustomTypeDescriptor 나머지 10개 stub 메서드 (lines 591–600) — 변경 없음

---

### `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` (model, CRUD — 확장)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`

**추가 필드 패턴 — AlgorithmType 드롭다운 필드 원본** (DatumConfig.cs lines 37–54):
```csharp
[Category("Datum|Algorithm")]
[ItemsSourceProperty(nameof(AlgorithmTypeList))]
public string AlgorithmType { get; set; } = "TwoLineIntersect";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> AlgorithmTypeList
{
    get
    {
        return new List<string>
        {
            EDatumAlgorithm.TwoLineIntersect.ToString(),
            // ...
        };
    }
}
```

**FAIConfig.EdgeMeasureType 필드 패턴** (DatumConfig.AlgorithmType 패턴 그대로 복사):
```csharp
// 추가 위치: FAIConfig.cs — EdgeThreshold 필드 직전 (Edge|Measurement Category 블록 앞)
[Category("Edge|Measurement")]
[ItemsSourceProperty(nameof(EdgeMeasureTypeList))]
public string EdgeMeasureType { get; set; } = "EdgePairDistance";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeMeasureTypeList
{
    get { return new List<string>(MeasurementFactory.GetTypeNames()); }
}
```
- 기본값: `"EdgePairDistance"` (CONTEXT.md: CircleDiameter 외 모든 타입은 숨김 없음)
- `MeasurementFactory.GetTypeNames()` 반환값이 유일한 유효 목록 소스 — 하드코딩 금지

**ICustomTypeDescriptor 선언 패턴** (DatumConfig.cs line 15):
```csharp
// 변경 전
public class FAIConfig : ParamBase {

// 변경 후
public class FAIConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor {
```

**GetProperties(Attribute[]) — FAIConfig 전용 구현 패턴** (DatumConfig.cs lines 555–586 + CONTEXT.md 숨김 규칙):
```csharp
public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
    var sourceNames = new System.Collections.Generic.HashSet<string> {
        nameof(EdgeMeasureTypeList),
        nameof(EdgeDirectionList),
        nameof(EdgePolarityList),
    };
    return DynamicPropertyHelper.FilterProperties(
        this, attributes,
        name => IsHiddenForEdgeMeasureType(name, EdgeMeasureType),
        sourceNames);
}
public System.ComponentModel.PropertyDescriptorCollection GetProperties() {
    return System.ComponentModel.TypeDescriptor.GetProperties(this, true);
}
```

**IsHiddenForEdgeMeasureType 패턴** (DatumConfig.IsHiddenForAlgorithm lines 606–626 참조):
```csharp
// CircleDiameter 선택 시 에지 방향/극성/선택/샘플 파라미터 숨김 (CONTEXT.md decisions)
private static bool IsHiddenForEdgeMeasureType(string name, string edgeMeasureType) {
    if (edgeMeasureType == "CircleDiameter") {
        if (name == "EdgeDirection"  || name == "EdgeDirectionList") return true;
        if (name == "EdgePolarity"   || name == "EdgePolarityList")  return true;
        if (name == "EdgeSelection")                                  return true;
        if (name == "EdgeSampleCount")                               return true;
        if (name == "EdgeTrimCount")                                 return true;
        if (name == "Sigma")                                         return true;
    }
    return false;
}
```

**ICustomTypeDescriptor 나머지 stub 10개** (DatumConfig.cs lines 591–600 — 그대로 복사):
```csharp
public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); }
public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); }
public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); }
public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); }
public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); }
public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); }
public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); }
public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); }
public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); }
public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; }
```

**INI 기본값 fallback 패턴** — EdgeMeasureType 미존재 시:
- property getter 기본값 `= "EdgePairDistance"` 로 충분 (ParamBase.Load 가 INI 키 미존재 시 기본값 유지하므로 별도 fallback 메서드 불필요)
- DatumConfig.AlgorithmType (line 39) 과 동일 패턴

---

### `WPF_Example/DatumMeasurement.csproj` (config — Compile ItemGroup 등록)

**Analog:** `WPF_Example/DatumMeasurement.csproj` lines 207–223 (Inspection 블록)

**삽입 위치 원본** (csproj lines 207–223):
```xml
    <Compile Include="Custom\Sequence\Inspection\DatumConfig.cs" />
    <Compile Include="Custom\Sequence\Inspection\EDatumAlgorithm.cs" />
    <Compile Include="Custom\Sequence\Inspection\EdgeOptionLists.cs" />
    <Compile Include="Custom\Sequence\Inspection\FAIConfig.cs" />
    <Compile Include="Custom\Sequence\Inspection\ShotConfig.cs" />
    <Compile Include="Custom\Sequence\Inspection\InspectionRecipeManager.cs" />
    <Compile Include="Custom\Sequence\Inspection\Action_FAIMeasurement.cs" />
    <Compile Include="Custom\Sequence\Inspection\InspectionSequence.cs" />
    <Compile Include="Custom\Sequence\Inspection\InspectionMasterParam.cs" />
    <Compile Include="Custom\Sequence\Inspection\MeasurementBase.cs" />
    <Compile Include="Custom\Sequence\Inspection\MeasurementFactory.cs" />
```

**추가할 항목 + 삽입 위치:**
```xml
    <Compile Include="Custom\Sequence\Inspection\DynamicPropertyHelper.cs" />
```
`MeasurementFactory.cs` 항목 바로 다음(line 217 다음)에 삽입하거나, `DatumConfig.cs` 직전에 삽입. 알파벳 순서나 논리적 의존 순서 중 기존 블록의 비알파벳 순서(추가 순서)를 따르는 패턴에 맞춰 MeasurementFactory 다음에 추가.

**경로 구분자:** 백슬래시 `\` (csproj 내 모든 Compile Include는 `\` 사용 — 절대 `/` 사용 금지)

---

## Shared Patterns

### ICustomTypeDescriptor 전체 구현 패턴
**Source:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` lines 555–600
**Apply to:** `FAIConfig.cs` (ICustomTypeDescriptor 신규 구현)

핵심 3요소:
1. `GetProperties(Attribute[])` — DynamicPropertyHelper.FilterProperties 위임
2. `GetProperties()` 무인자 — TypeDescriptor.GetProperties(this, true) 위임 (INI reflection 경로 보호)
3. 나머지 10개 stub — TypeDescriptor 위임 (복사)

### Phase 18 CO-01 allNoFilter + sourceNames 패턴
**Source:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` lines 568–584
**Apply to:** DynamicPropertyHelper.FilterProperties 내부 (헬퍼로 이전됨)

패턴 요약: `TypeDescriptor.GetProperties(obj, attrs, true)` 가 BrowsableAttribute 필터로 `[Browsable(false)]` 소스 프로퍼티를 제외한다. `TypeDescriptor.GetProperties(obj, true)` (필터 없음) 로 재조회 후 sourceNames 화이트리스트에 있는 항목만 명시 추가. 중복 추가 방지는 `keep.Exists(k => k.Name == pd.Name)` 으로 처리.

### INI 직렬화 비간섭 패턴
**Source:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` line 14 주석 + lines 587–589
**Apply to:** FAIConfig.GetProperties 구현

`ParamBase.Save/Load`는 `GetType().GetProperties()` System.Reflection 경로를 사용하므로 `ICustomTypeDescriptor.GetProperties(Attribute[])` 오버라이드는 INI 직렬화에 영향을 주지 않는다. 두 경로가 독립적임을 주석으로 명시해야 한다.

### 코드 주석 컨벤션
**Source:** MEMORY.md feedback_comment_convention.md
**Apply to:** 모든 새 코드 라인

모든 추가/수정 라인에 `//YYMMDD hbk Phase 19:` 또는 `//YYMMDD hbk Phase 19 <req-id>` 주석 필수.
예: `//260507 hbk Phase 19 — DynamicPropertyHelper FilterProperties 헬퍼`

### [ItemsSourceProperty] 드롭다운 연결 패턴
**Source:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` lines 38–54 (AlgorithmType + AlgorithmTypeList)
**Apply to:** FAIConfig.EdgeMeasureType + EdgeMeasureTypeList

`[ItemsSourceProperty(nameof(XxxList))]` 어트리뷰트 + `[Browsable(false)] public List<string> XxxList` getter 쌍. sourceNames HashSet에 `nameof(XxxList)` 반드시 포함.

### K&R 브레이스 스타일
**Source:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` 전체 (K&R 스타일 사용)
**Apply to:** DynamicPropertyHelper.cs, DatumConfig.cs 수정 부분, FAIConfig.cs ICustomTypeDescriptor 블록

`namespace`, `class`, `method` 의 여는 중괄호를 선언 줄 끝에 붙인다. FAIConfig.cs 기존 코드도 K&R 스타일이므로 일관성 유지.

---

## No Analog Found

해당 없음. 모든 파일에 대해 기존 코드베이스에서 정확한 아날로그를 발견했다.

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/DatumMeasurement.csproj`
**Files scanned:** 6 (DatumConfig.cs, FAIConfig.cs, MeasurementBase.cs, MeasurementFactory.cs, EdgeOptionLists.cs, DatumMeasurement.csproj)
**Pattern extraction date:** 2026-05-07
