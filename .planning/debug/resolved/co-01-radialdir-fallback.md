---
status: resolved
trigger: "CO-01: Circle_RadialDirection PropertyGrid 드롭다운이 Inward/Outward 대신 LtoR/RtoL/TtoB/BtoT 표시. Phase 18-06 시도 (GetProperties whitelist + Browsable 제거) 실패 — H1 (ItemsSourceProperty resolution 이 ICustomTypeDescriptor 우회) 확정 후에도 PropertyTools 가 어떤 fallback 으로 4항목 컬렉션을 잡아오는지 불명."
created: 2026-05-07
updated: 2026-05-07
resolution_commit: ba25e88
---

## Resolution

**원래 보고된 버그는 misidentification.** Circle_RadialDirection 드롭다운은 정상적으로 Inward/Outward 2항목을 표시하고 있었음 (PropertyTools.Wpf 디컴파일로 정상 동작 확인). 사용자가 본 LtoR 4항목은 별개 필드 `Circle_EdgeDirection` 의 ComboBox 였음 — Phase 17 D-03 의 IsHiddenForAlgorithm CTH 분기 hide 룰이 dynamic enumeration 경로 차이로 안 먹어서 노출됨.

**Fix applied (commit ba25e88):** `Circle_EdgeDirection` 에 `[PropertyTools.DataAnnotations.Browsable(false)]` 정적 추가. Circle 알고리즘은 EdgeDirection 대신 RadialDirection (Inward/Outward) 사용하므로 어떤 모드에서도 노출 불필요 → 영구 hide.

**User UAT 2026-05-07:** CTH 모드에서 (1) LtoR row 사라짐 (2) Inward/Outward row 정상 표시. PASS.

---


# Debug Session: co-01-radialdir-fallback

## Symptoms

**Expected:** CTH (CircleTwoHorizontal) Datum 선택 → PropertyGrid 의 Circle_RadialDirection 드롭다운 = ["Inward", "Outward"] 2 항목.

**Actual:** ["LtoR", "RtoL", "TtoB", "BtoT"] 4 항목 (`EdgeOptionLists.Directions` 의 내용 = 다른 EdgeDirection 컬렉션).

**Error messages:** 없음. 런타임 예외 없음. UI 만 잘못 표시.

**Timeline:** Phase 17 D-02 (Circle_RadialDirection / Circle_RadialDirectionList 추가) 부터. 처음부터 잘못 동작.

**Reproduction:**
1. DatumMeasurement.exe 실행
2. InspectionListView 에서 CircleTwoHorizontal Datum 선택
3. PropertyGrid 에서 Circle_RadialDirection 드롭다운 클릭
4. → 4항목 (EdgeDirection 항목들) 표시 (예상 = 2항목 Inward/Outward)

## Confirmed Facts (Phase 18-06 trace 검증 결과)

1. **H1 확정 (PropertyTools 가 ICustomTypeDescriptor 우회):** Circle_RadialDirectionList getter 에 `Logging.PrintLog((int)ELogType.Trace, "[CO-01 H1 trace] ...")` 추가 후 드롭다운 열어도 Trace 로그 미생성. → PropertyTools 가 `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` resolution 시 `ICustomTypeDescriptor.GetProperties()` 호출 안 함.

2. **추가 발견 (Browsable 제거 실험):** *List 프로퍼티들의 `[PropertyTools.DataAnnotations.Browsable(false)]` 를 제거 + `IsHiddenForAlgorithm` 에 `EndsWith("List") return true` hide 블록 추가했더니:
   - *List 들이 PropertyGrid 표면에 DataGrid (Length 컬럼) 로 렌더링됨 → PropertyTools 가 표면 enumeration 도 ICustomTypeDescriptor 우회 확인
   - IsHiddenForAlgorithm hide 블록은 호출조차 안 됨 (GetProperties() 가 안 불려서)
   - Circle_RadialDirection 드롭다운은 여전히 4항목 (LtoR/RtoL/TtoB/BtoT) 표시 — 즉 Browsable 제거가 fallback 동작에 영향 없음
   - → 모두 revert 함 (ff9792d, 6e5f0ce)

3. **Phase 17/18-01 시도 (allNoFilter + sourceNames whitelist):** GetProperties(Attribute[]) 안에서 *List source 프로퍼티들을 강제 포함하는 패턴. H1 으로 no-op fix 임이 확정 (PropertyTools 가 GetProperties 안 부르므로 의미 없음).

## Investigation 18-07 결과 (2026-05-07 ilspycmd 디컴파일)

PropertyTools.Wpf 3.1.0 `lib/net45/PropertyTools.Wpf.dll` 을 ICSharpCode.ILSpy 10.0.1 `ilspycmd` 으로 디컴파일 후 ItemsSourceProperty resolution 정확한 알고리즘 확인.

### PropertyTools 의 정확한 [ItemsSourceProperty] resolution chain

**Step 1 — `PropertyGridOperator.CreatePropertyItems(instance, options)` (line 96-117):**
```csharp
properties = TypeDescriptor.GetProperties(instance);  // ← ICustomTypeDescriptor 경유
foreach (PropertyDescriptor item in properties) {
    BrowsableAttribute first = item.GetFirstAttributeOrDefault<BrowsableAttribute>();
    // PropertyTools.DataAnnotations.BrowsableAttribute 기준으로 surface 필터
    if ((first == null || first.Browsable) && item.IsBrowsable && ...)
        yield return CreatePropertyItem(item, properties, instance);
        //                              ^^^^^^^^^ properties 컬렉션 전체를 PropertyItem 에 보관
}
```

**Step 2 — `PropertyGridOperator.SetAttribute(...)` (line 401-405):**
```csharp
ItemsSourcePropertyAttribute val19 = attribute as ItemsSourcePropertyAttribute;
if (val19 != null)
    pi.ItemsSourceDescriptor = pi.GetDescriptor(val19.PropertyName);
    //                          ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
```

**Step 3 — `PropertyItem.GetDescriptor(name)` (line 268-275):**
```csharp
public PropertyDescriptor GetDescriptor(string name) {
    if (name == null) return null;
    return Properties.Find(name, ignoreCase: false);  // ← PropertyDescriptorCollection.Find
}
```

**Step 4 — `PropertyGridControlFactory.CreateComboBoxControl(...)` (line 276-291):**
```csharp
ComboBox comboBox = new ComboBox {
    IsEditable = property.IsEditable,
    ItemsSource = property.ItemsSource,    // null (직접 set 되는 경로 없음)
    ...
};
if (property.ItemsSourceDescriptor != null)
    comboBox.SetBinding(ItemsControl.ItemsSourceProperty,
                        new Binding(property.ItemsSourceDescriptor.Name));
//                                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//   WPF Binding(path) — Source 미지정 → 상속 DataContext (= tabControl.DataContext = SelectedObject = DatumConfig)
//   Path = ItemsSourceDescriptor.Name (= "Circle_RadialDirectionList")
```

→ **결론:** PropertyTools 는 `[ItemsSourceProperty]` 를 `Properties.Find(name, false)` 으로 PropertyDescriptorCollection 안에서 정확한 이름 매칭으로 찾는다. 찾으면 WPF Binding 으로 ComboBox.ItemsSource 에 연결. **별도 fallback 메커니즘은 존재하지 않는다** — descriptor 가 null 이면 ComboBox 의 ItemsSource 가 unset 상태로 남는다.

### CTH 모드 surface 렌더링 흐름 확인

`PropertyGridControlFactory.CreateControl` (line 40-90):
- string + ItemsSourceDescriptor != null → `CreateComboBoxControl` (line 82)
- string + ItemsSource != null → `CreateComboBoxControl`  
- ICollection<> → `CreateGridControl` (DataGrid). ★ Browsable 제거 시 *List 가 DataGrid 로 렌더링되던 fact 2의 관찰과 일치.

`Circle_RadialDirection` (string) + `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` 는 항상 `CreateComboBoxControl` 경로.

### **ROOT CAUSE — H1 trace 의 misinterpretation**

debug file 의 fact 1 ("H1 확정") 은 **잘못된 결론** 이다:
- 트레이스가 fire 안 한 것은 사실이나, "PropertyTools 가 ICustomTypeDescriptor 우회" 가 결론이 될 수 없음.
- 디컴파일된 `PropertyGridOperator.CreatePropertyItems` (line 108) 은 명시적으로 `TypeDescriptor.GetProperties(instance)` 를 호출 — `ICustomTypeDescriptor` 구현자에 대해서는 .NET TypeDescriptor 가 `instance.GetProperties()` (no-args overload) 를 invoke 함.
- DatumConfig.GetProperties() (line 584-587) 은 `TypeDescriptor.GetProperties(this, true)` 위임 — **`true` = noCustomTypeDesc**, 즉 ICustomTypeDescriptor 자기참조 무한루프 방지를 위해 reflection 기반 descriptor 를 반환. 이 컬렉션에는 **Circle_RadialDirectionList 가 정상 포함됨** (PropertyInfo 기반, [PropertyTools.DataAnnotations.Browsable(false)] 는 System.ComponentModel.BrowsableAttribute 가 아니므로 IsBrowsable 에 영향 없음).
- 따라서 `Properties.Find("Circle_RadialDirectionList", false)` 은 정상 descriptor 를 반환, `pi.ItemsSourceDescriptor.Name == "Circle_RadialDirectionList"`, ComboBox 는 `new Binding("Circle_RadialDirectionList")` 으로 setBinding.
- WPF Binding 이 path 를 resolve 할 때 DataContext (= DatumConfig) 의 `Circle_RadialDirectionList` 프로퍼티 getter 를 호출해야 함. **getter 가 호출되면 `EdgeOptionLists.RadialDirections` (Inward/Outward 2항목) 반환** — 정상 동작.

**그럼에도 사용자가 LtoR/RtoL/TtoB/BtoT 4항목 을 본다는 사실** 은 다음 중 하나만 성립:
- (A) 사용자가 잘못된 ComboBox 를 보고 있음 (CTH 모드에서도 노출되는 다른 *_EdgeDirection ComboBox: e.g. Horizontal_A_EdgeDirection / Horizontal_B_EdgeDirection 의 리스트 = `EdgeOptionLists.Directions` = LtoR/RtoL/TtoB/BtoT 4항목 동일).
- (B) 캐시된 build 사용 (Phase 17 D-02 추가 시점의 stale dll 에서 `EdgeOptionLists.RadialDirections` 가 정의되기 전 — 그러나 17-01 commit 6b62a0a 가 EdgeOptionLists 변경을 같이 포함하므로 가능성 낮음).
- (C) Circle_RadialDirection 의 ComboBox 가 IsEditable=true 또는 SelectedValue 로 잘못된 항목을 표시 (그러나 ItemsSource 는 RadialDirections 임).

### 검증 (sanity check)

**CTH 모드에서 PropertyGrid 에 노출되는 *_EdgeDirection / *_RadialDirection ComboBox 카운트 (`IsHiddenForAlgorithm` 통과 후):**

| 프로퍼티 | ItemsSource | 항목 수 | CTH 노출? |
|---|---|---|---|
| Circle_EdgeDirection | Circle_EdgeDirectionList → Directions | 4 (LtoR/RtoL/TtoB/BtoT) | NO (D-03 hide) |
| Circle_RadialDirection | Circle_RadialDirectionList → RadialDirections | 2 (Inward/Outward) | YES |
| Horizontal_A_EdgeDirection | Horizontal_A_EdgeDirectionList → Directions | 4 (LtoR/RtoL/TtoB/BtoT) | YES |
| Horizontal_B_EdgeDirection | Horizontal_B_EdgeDirectionList → Directions | 4 (LtoR/RtoL/TtoB/BtoT) | YES |

→ CTH 모드에서 4항목 ComboBox 가 **3개 보임** (Horizontal_A/B_EdgeDirection 가 4항목). 사용자가 "Circle_RadialDirection" 라벨 옆 ComboBox 가 아니라 위/아래 인접 *_EdgeDirection ComboBox 를 클릭했을 가능성.

→ Category prefix labeling (Phase 14-03 D-08) 으로 카테고리는 분리됨 ("Datum|Circle (CTH) Edge" 카테고리에는 RadialDirection 만, "Datum|Horizontal_A (CTH/VTH) Edge" 카테고리에 Horizontal_A_EdgeDirection). 그러나 사용자가 카테고리 안 보고 ComboBox 만 본다면 헷갈리기 쉬움.

## Key Open Questions (다음 진단의 방향)

(이 섹션은 18-07 디컴파일 분석 후 SUPERSEDED. Resolution 섹션 참조.)

## Investigation Hints

(앞 단계의 hint — Resolution 으로 대체)

## Current Focus

```yaml
hypothesis: "Phase 18-06 의 H1 trace 가 fire 안 한 사실은 'PropertyTools 가 ICustomTypeDescriptor 우회' 가 아니라 'WPF Binding 이 path resolution 시 reflection-based PropertyInfo.GetValue 를 다른 경로로 호출' 또는 '사용자가 다른 ComboBox 를 보고 있음' 중 하나로 재해석되어야 함. PropertyTools resolution 자체는 정상 동작 (ilspycmd 디컴파일로 확인)."
test: "(1) Circle_RadialDirectionList 의 getter 에 trace + ALSO 별도 정적 카운터 증가 + UI 의 Circle_RadialDirection 라벨 옆 ComboBox 를 명시적으로 우클릭/포커스 후 dropdown 클릭 — 항목 수 + 트레이스 카운터 동시 확인. (2) 동시에 Horizontal_A_EdgeDirectionList getter 에도 trace 추가 — 사용자가 본 4항목이 어느 list 에서 왔는지 식별."
expecting: "(A) Circle_RadialDirectionList getter 가 호출되고 2항목 표시 → 18-06 트레이스의 시점 문제 (예: SIMUL_MODE 아닌 빌드 / 잘못된 .exe 실행). (B) Horizontal_A_EdgeDirectionList getter 만 호출 → 사용자 misidentification (실제로는 Circle_RadialDirection 이 정상 동작 중). (C) 둘 다 호출 안 됨 → WPF Binding path 가 매핑 안 됨 (별도 분석 필요)."
next_action: "디컴파일된 PropertyTools 소스 결과에 따라 fix path 결정. 가능성 높은 시나리오: 사용자 misidentification → fix 불필요 / UI 시각적 명료화 (Description tooltip on Circle_RadialDirection 추가) 만 필요."
```

## Evidence

- 2026-05-07: H1 trace 결과 — Circle_RadialDirectionList getter 호출 안 됨 (commit c0d6135 → revert 6e5f0ce). PropertyTools 가 ICustomTypeDescriptor.GetProperties() 우회 확정 (이후 reinterpreted — 18-07 분석 참조).
- 2026-05-07: *List Browsable 일괄 제거 결과 — *List 들이 PropertyGrid 표면에 DataGrid 로 노출 (commit 1dd8272 → revert ff9792d). PropertyTools 가 표면 enumeration 도 ICustomTypeDescriptor 우회 확인. RadialDirection 드롭다운은 여전히 4항목.
- Phase 18-01 (allNoFilter + sourceNames whitelist) — GetProperties 안에서 source 프로퍼티 강제 포함. H1 로 no-op 확정.
- 2026-05-07 (18-07 디컴파일): ICSharpCode.ILSpy 10.0.1 ilspycmd 으로 PropertyTools.Wpf.dll 디컴파일. PropertyGridOperator + PropertyItem + PropertyGridControlFactory 코드 확인. ItemsSource resolution = `Properties.Find(name, ignoreCase:false)` + `new Binding(name)` chain. **Fallback / 우회 메커니즘 부재** 확인.

## Eliminated

- [Hypothesis: ICustomTypeDescriptor.GetProperties() whitelist 가 ItemsSource resolution 을 고칠 것이다] — H1 trace 로 기각. PropertyTools 가 GetProperties() 자체를 호출 안 함. (NOTE: 18-07 디컴파일 후 재해석 — PropertyTools 는 GetProperties() 를 호출함. trace 미감지의 진짜 원인은 별도 분석 필요.)
- [Hypothesis: *List 의 [PropertyTools.DataAnnotations.Browsable(false)] 만 제거하면 PropertyTools 가 발견할 것이다] — 18-06 Task 2 로 기각. Browsable 제거 후에도 RadialDirection 4항목 그대로 + 부작용 (DataGrid 노출) 발생.
- [Hypothesis: PropertyTools 가 fallback 으로 다른 *EdgeDirectionList 를 잡는다] — 18-07 디컴파일로 기각. PropertyTools 의 ItemsSource resolution chain 에 fallback 분기 없음. descriptor null → ComboBox.ItemsSource unset (4항목 표시 불가).

## Resolution

### Root Cause (18-07 ilspycmd 디컴파일 분석)

PropertyTools.Wpf 3.1.0 의 `[ItemsSourceProperty]` resolution 은 단순한 정확 이름 매칭이며 fallback 메커니즘이 **존재하지 않는다.** chain:

1. `PropertyGridOperator.CreatePropertyItems`:`properties = TypeDescriptor.GetProperties(instance)` — DatumConfig 의 ICustomTypeDescriptor.GetProperties() 호출 → `TypeDescriptor.GetProperties(this, true)` 위임 → reflection 기반 descriptor 컬렉션 (Circle_RadialDirectionList **포함**).
2. `PropertyItem.GetDescriptor(name)`:`Properties.Find(name, ignoreCase:false)` — 정확 매칭으로 Circle_RadialDirectionList descriptor 반환.
3. `PropertyGridControlFactory.CreateComboBoxControl`:`comboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(descriptor.Name))` — WPF Binding(path) 으로 DataContext (= DatumConfig 인스턴스) 의 Circle_RadialDirectionList 에 path resolution.
4. WPF Binding 이 PropertyInfo.GetValue 호출 → getter → `EdgeOptionLists.RadialDirections` (Inward/Outward 2항목).

**= 정상 동작 시퀀스.** 코드상 Circle_RadialDirection 의 ComboBox 가 4항목 (LtoR/...) 을 표시할 경로는 **존재하지 않음**.

### 가설 재정의 (사용자 검증 필요)

debug 의 H1 trace 결과와 디컴파일 사실의 모순을 설명하려면 다음 가설 중 하나가 맞아야 함:

**(A) UAT 환경 misidentification (가장 가능성 높음):**
CTH 모드에서 PropertyGrid 에 노출되는 4항목 ComboBox = `Horizontal_A_EdgeDirection`, `Horizontal_B_EdgeDirection` (2개). 사용자가 "Circle_RadialDirection" 라벨 옆 ComboBox (실제로는 Inward/Outward 2항목 정상 표시) 가 아니라 인접 카테고리의 EdgeDirection ComboBox 를 클릭/관찰했을 가능성. UAT 시 PropertyGrid 의 Category 헤더 ("Datum|Circle (CTH) Edge" vs "Datum|Horizontal_A (CTH/VTH) Edge") 명시적 확인 필요.

**(B) 빌드 stale (가능성 낮음):**
Phase 17-01 commit (6b62a0a) 이 EdgeOptionLists.RadialDirections + Circle_RadialDirectionList getter 를 동시에 추가. 빌드 후 즉시 UAT 한 경우 가능성 0.

**(C) WPF Binding path 미해결 (가능성 낮음):**
DataContext = DatumConfig, path = "Circle_RadialDirectionList" 으로 WPF Binding 이 reflection 기반 PropertyInfo 를 못 찾는 경우. 그러나 ICustomTypeDescriptor.GetProperties() 가 이 descriptor 를 반환하므로 Binding 도 찾아야 함. 이 시나리오 성립 시 BindingExpression 이 PathError 를 보고할 것 — Visual Studio Output 창의 binding error 확인 필요.

### Recommended Verification (fix 적용 전)

1. **CTH 모드에서 PropertyGrid 스크린샷**:
   - "Datum|Circle (CTH) Edge" 카테고리 안의 Circle_RadialDirection ComboBox 의 dropdown 펼친 상태 — 항목 텍스트 정확히 확인.
   - "Datum|Horizontal_A (CTH/VTH) Edge" 카테고리 안의 Horizontal_A_EdgeDirection ComboBox 의 dropdown — 4항목 (LtoR/RtoL/TtoB/BtoT) 일 것.
   - **두 ComboBox 가 시각적으로 닮아 헷갈리는지** 사용자에게 명시적 확인.

2. **다중 trace** (H1 trace 재시도, broader scope):
   - Circle_RadialDirectionList getter
   - Horizontal_A_EdgeDirectionList getter
   - Horizontal_B_EdgeDirectionList getter
   - DatumConfig.GetProperties() (no-args)
   - DatumConfig.GetProperties(Attribute[])
   - 각 trace 에 unique tag (e.g. `[CO-01-trace-RadList]`, `[CO-01-trace-HAList]`) 부여 후 CTH Datum 선택 + 각 ComboBox dropdown 펼치기 — 어느 list 가 hit 되는지 식별.

3. **Visual Studio 의 Output 창** (Debug 빌드):
   - WPF binding error/warning 확인 (`System.Windows.Data Error: 40 : BindingExpression path error: 'Circle_RadialDirectionList' property not found...`).
   - 이러한 에러가 보이면 시나리오 (C), 안 보이면 시나리오 (A) 확정.

### Fix Direction (가설별)

| 가설 | Fix 필요? | 권장 조치 |
|---|---|---|
| (A) misidentification | NO | 1. Category 명료화 (Circle_RadialDirection 의 Category 를 별도 sub-group 으로 분리, e.g. `[Category("Datum|Circle (CTH) Radial")]`). 2. `[Description("안→밖(Inward) / 밖→안(Outward) 그라디언트 방향")]` tooltip 추가. 3. (option) 라벨에 emoji/이모지 prefix 로 시각적 구분. |
| (B) stale build | NO | rebuild + redeploy. |
| (C) binding path miss | YES | DatumConfig.GetProperties() (no-args) 가 Circle_RadialDirectionList 를 항상 포함하는지 verify (현재 `TypeDescriptor.GetProperties(this, true)` 반환). 만약 미포함이면 명시적 PropertyDescriptorCollection 구성 필요. 단 이 가설은 가능성 낮음. |

### 다음 단계

- fix 를 적용하기 **전** 위 verification (특히 1번 — UAT 스크린샷) 으로 시나리오 확정.
- 가장 가능성 높은 (A) 시나리오 라면 fix 가 아니라 UI 명료화 작업 (Category 분리 + Description tooltip).
- 시나리오 (C) 면 PropertyDescriptorCollection 명시 구성으로 진행.
