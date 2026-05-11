# Phase 22: image-dual-structure - Pattern Map

**Mapped:** 2026-05-11
**Files analyzed:** 3 modified, 0 new (UI 옵션 별도)
**Analogs found:** 3 / 3 (모두 동일 파일 내 기존 필드 패턴 활용 — analog quality "exact")

## 핵심 관찰

Phase 22 는 신규 파일 생성이 거의 없는 **속성 추가 + 경로 분리** 작업이다.
가장 강력한 analog 는 **이미 동일 코드베이스 안에 존재하는 동일 클래스의 기존 필드**다:

- `DatumConfig.TeachingImagePath` ← `ShotConfig.SimulImagePath` 패턴을 그대로 따른다
- INI 직렬화는 `ParamBase` reflection 경로(`Save`/`Load`)가 신규 string 필드를 **자동 처리** (별도 코드 불필요)
- "INI 키 미존재 시 빈 문자열 폴백" 은 `IniSection.this[name]` getter 가 미존재 키에 대해 `IniValue.Default` (Value=null) 를 반환 → `ParamBase.Load` 의 String case 가 그대로 set → 결과적으로 `null` 로 들어감. 따라서 **소비처에서 `?? ""` 또는 `string.IsNullOrEmpty()` 가드만 추가하면 회귀 0**

이 PATTERNS.md 는 위 3개 사실을 코드 라인으로 못박는 것이 목적이다.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (수정) | model | CRUD (INI persistence) | `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` (`SimulImagePath`) | exact (동일 패턴) |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` (수정 — 옵션) | service | CRUD (INI persistence) | 동일 파일의 Datum 저장/로드 블록 (L117-119, L188-193) | exact (자기 자신) |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (수정) | controller/action | request-response (simul image load) | 동일 파일의 `ShotParam.SimulImagePath` 사용 블록 (L111-117, L226-232) | exact (자기 자신, 필드 교체) |
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` (수정 후보) | model | CRUD | 자기 자신 — `SimulImagePath` → 의미상 `InspectionImagePath` 별칭/리네이밍 분기 검토 | role-match |
| (옵션) UI 변경 | view | request-response | PropertyGrid 자동 노출 (`[Category("Datum|ImageSource")]`) | exact (기존 패턴) |

> **중요**: 신규 `InspectionImagePath` 변수가 어디 살지는 CONTEXT.md 에 명시되지 않았다. 현재 코드의
> `ShotParam.SimulImagePath` 가 사실상 "검사 이미지 경로" 역할을 한다 (Action_FAIMeasurement L111, L226).
> 가장 회귀 0 인 분리 전략:
>   - **TeachingImagePath** 신규 필드 → `DatumConfig` 에 추가 (CONTEXT 명시)
>   - **InspectionImagePath** → 기존 `ShotConfig.SimulImagePath` 를 의미상 그대로 사용 (또는 리네이밍).
>     INI 키 그대로 유지 → 회귀 0. CONTEXT 5번 항목 "두 변수 분리 유지" 충족.
> 이 전략을 planner 가 채택할 경우 ShotConfig 변경은 0 (의미만 재정의) 또는 주석만 갱신.

---

## Pattern Assignments

### 1. `DatumConfig.cs` 에 `TeachingImagePath` 필드 추가 (controller-of-data, CRUD)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — `SimulImagePath` 필드 선언

**Imports pattern** (`DatumConfig.cs` L1-6 — 신규 import 불필요):
```csharp
//260409 hbk Phase 4: Datum 데이터 모델 — D-01, D-04, D-05, D-11
using System.Collections.Generic; //260423 hbk WR-RT-02 ComboBox 옵션 리스트
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Utility;

namespace ReringProject.Sequence {
```

**Field declaration pattern** (`ShotConfig.cs` L15-16 — 그대로 복제):
```csharp
[Category("Shot|Simulation")]
public string SimulImagePath { get; set; } = "";
```

**적용 시 DatumConfig 추가 위치** — `Datum|ImageSource` 카테고리 직후 또는 자체 카테고리:
```csharp
//260511 hbk Phase 22 IMG-01 — Datum 티칭 시 사용한 기준 이미지 경로.
//  INI 직렬화는 ParamBase reflection 경로가 자동 처리 (별도 Save/Load 코드 불필요 — ParamBase.cs L337/L385 String case).
//  INI 키 미존재 시 IniValue.Default (Value=null) → ParamBase.Load 가 null 로 set → 소비처에서 string.IsNullOrEmpty 가드.
//  검사 실행 시 이미지는 별도 InspectionImagePath (ShotConfig.SimulImagePath) 사용 — 역할 분리 유지.
[Category("Datum|ImageSource")]
public string TeachingImagePath { get; set; } = "";
```

**Why this works:**
- `DatumConfig : ParamBase` (L15) → `ParamBase.Save/Load` 의 String case 가 reflection 으로 자동 처리됨 (ParamBase.cs L325-339, L370-395)
- `[Category("Datum|ImageSource")]` 접두는 기존 `ImageSourceMode`/`ReuseFromShotName`/`SourceShotName` (L23/L27/L31) 와 동일 그룹 → PropertyGrid 자동 노출 (CONTEXT "가능하면 UI 추가" 충족)
- 기본값 `""` (빈 문자열) → 신규 INI 키 미존재 시에도 null 가능성 외 회귀 0

---

### 2. INI 직렬화 (service, CRUD) — **추가 코드 0줄**

**Analog:** `WPF_Example/Sequence/Param/ParamBase.cs` — `Save` / `Load` reflection 본문 (L324-430)

**Save 본문 패턴** (ParamBase.cs L324-367):
```csharp
public virtual bool Save(IniFile saveFile, string group) {
    PropertyInfo[] props = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
    foreach (var prop in props) {
        string name = prop.Name;
        string type = prop.PropertyType.Name;
        switch (type) {
            ...
            case "String":
                saveFile[group][name] = (string)prop.GetValue(this);
                break;
            ...
```

**Load 본문 패턴** (ParamBase.cs L369-429):
```csharp
public virtual bool Load(IniFile loadFile, string group) {
    PropertyInfo[] props = GetType().GetProperties();
    foreach (var prop in props) {
        string name = prop.Name;
        string type = prop.PropertyType.Name;
        try {
            switch (type) {
                ...
                case "String":
                    string sValue = loadFile[group][name].ToString();
                    prop.SetValue(this, sValue);
                    break;
                ...
```

**Caller 패턴** (InspectionRecipeManager.cs L117-119 — Datum Save):
```csharp
for (int d = 0; d < fixtureSeq.DatumConfigs.Count; d++) {
    string datumSection = $"FIXTURE_DATUM_{d}";
    fixtureSeq.DatumConfigs[d].Save(saveFile, datumSection);
}
```

**Caller 패턴** (InspectionRecipeManager.cs L188-193 — Datum Load):
```csharp
for (int d = 0; d < datumCount; d++) {
    string datumSection = $"FIXTURE_DATUM_{d}";
    if (!loadFile.ContainsSection(datumSection)) continue;
    var datum = fixtureSeq.AddDatum();
    datum.Load(loadFile, datumSection);
}
```

**적용 시 변경**: **0 줄**.
`DatumConfig.TeachingImagePath` 가 `public string { get; set; }` 인 한 위 reflection 경로가 자동 처리한다.
INI 파일의 `[FIXTURE_DATUM_0]` 섹션에 `TeachingImagePath=...` 키가 자동으로 추가/로드된다.

---

### 3. INI 키 미존재 폴백 — **자동 (별도 코드 불필요)**

**Analog 1:** `WPF_Example/Utility/Ini.cs` L961-975 — IniSection indexer
```csharp
public IniValue this[string name] {
    get {
        IniValue val;
        if (values.TryGetValue(name, out val)) {
            return val;
        }
        return IniValue.Default;   // ← 미존재 키 → Default (Value=null)
    }
    ...
}
```

**Analog 2:** `WPF_Example/Utility/Ini.cs` L312-317 — IniValue.ToString(default)
```csharp
public string ToString(string defaultStr = null) {
    if (defaultStr != null) {
        if (Value == null) return defaultStr;
    }
    return Value;
}
```

**Analog 3 (소비처 가드 패턴):** `InspectionRecipeManager.cs` L184 — 기존 코드의 `?? ""` 패턴
```csharp
fixtureSeq.DisplayName = loadFile["FIXTURE"]["DisplayName"].ToString() ?? "";
```

**Analog 4 (사전 폴백 패턴):** `DatumConfig.cs` L514 — sentinel "" 검출 후 default 부여
```csharp
if (string.IsNullOrEmpty(Circle_RadialDirection)) Circle_RadialDirection = "Inward"; //260503 hbk Phase 17 D-02 — sentinel "" → "Inward" fallback (INI 하위호환)
```

**적용 시 변경**: 두 가지 선택지 (planner 가 1개 선택):
- **(A) 무행동 (권장)**: `ParamBase.Load` 가 null 로 set → 소비처에서 `string.IsNullOrEmpty(datum.TeachingImagePath)` 또는 `datum.TeachingImagePath ?? ""` 가드 — `Action_FAIMeasurement.cs` 의 기존 `!string.IsNullOrEmpty(...) && File.Exists(...)` 가드 패턴 (L111, L226) 그대로 활용 가능.
- **(B) EnsureDefaults**: `DatumConfig.EnsurePerRoiDefaults()` 마지막에 `if (TeachingImagePath == null) TeachingImagePath = "";` 한 줄 추가 (Phase 17 D-02 패턴 그대로). 회귀 0 보강.

CONTEXT 의 success criterion #4 ("빈 문자열 폴백") 는 (A) 만으로 자동 충족된다. (B) 는 안전판.

---

### 4. Simul 검사 이미지 로드 경로 분리 (action, request-response)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 동일 파일 내 2개 경로

**Path #1 — DatumPhase 단계의 Datum 이미지 로드** (`Action_FAIMeasurement.cs` L222-240):
```csharp
//260413 hbk Phase 6: Datum 이미지 취득 — Dedicated만 우선 지원 (D-07, D-08)
// ReuseFromShot 모드는 향후 Plan 04 UI 작업과 함께 구현.
private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {
    if (ShotParam == null) return null;
    HImage image = null;
    #if SIMUL_MODE
    if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
        try {
            image = new HImage(ShotParam.SimulImagePath);
        } catch {
            image = null;
        }
    }
    if (image == null) {
        image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
    }
    #else
    image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
    #endif
    return image;
}
```

**Path #2 — Grab 단계의 검사 이미지 로드** (`Action_FAIMeasurement.cs` L106-130):
```csharp
case EStep.Grab:
    if (ShotParam != null && !ShotParam.HasImage) {
        HImage image = null;
        //260409 hbk Phase 5: SimulImagePath 이미지 로드 (D-10)
        #if SIMUL_MODE
        if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
            try {
                image = new HImage(ShotParam.SimulImagePath);
            } catch {
                image = null;
            }
        }
        if (image == null) {
            image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
        }
        #else
        image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
        #endif
        ...
```

**적용 시 변경 (planner 가 검토할 분리 전략)**:

Phase 22 의 핵심은 "Datum 찾기는 InspectionImagePath, 티칭은 TeachingImagePath" 분리이지만,
**런타임 검사 시점에는 둘 다 InspectionImagePath 를 사용한다** (CONTEXT 표: "Datum 찾기 + FAI 측정" 모두 InspectionImagePath).
`TeachingImagePath` 는 셋업/UI 단계에서만 참조 (재티칭 기준).

따라서 위 두 경로 (`Action_FAIMeasurement.cs` L107-123, L222-240) 는 **변경 없음** 이거나, "Simul → InspectionImagePath 라는 의미를 분명히" 하기 위해 **변수 alias / 주석 갱신** 정도.

만약 planner 가 `InspectionImagePath` 를 별도 변수로 도입하기로 결정한다면:
- `ShotConfig` 또는 `InspectionSequence` 에 `InspectionImagePath` 필드 추가 (현 `SimulImagePath` 와 별도)
- 위 2개 경로에서 `ShotParam.SimulImagePath` → `ShotParam.InspectionImagePath` 로 교체
- INI 호환: `SimulImagePath` 자동 직렬화 유지하면서 신규 `InspectionImagePath` 가 비어있을 때 `SimulImagePath` 로 폴백 (DatumConfig.cs L514 패턴)

**티칭 측 InvokeTryTeachDatum** (`MainView.xaml.cs` L1684-1724) — 현재는 `halconViewer.CurrentImage` 사용. Phase 22 가 "셋업 시 TeachingImagePath 의 이미지를 로드" 를 의도한다면 이 경로 보강이 필요할 수 있다 (단, CONTEXT 영향범위 4번 "UI 가능하면 추가" 수준 — 필수 아님).

---

### 5. 옵션 UI — PropertyGrid 자동 노출 (view, request-response)

**Analog:** `DatumConfig.cs` L22-27 — `Datum|ImageSource` 카테고리 패턴
```csharp
//260413 hbk Phase 6: 이미지 소스 모드 — "Dedicated" 또는 "ReuseFromShot" (D-07, D-08)
[Category("Datum|ImageSource")]
public string ImageSourceMode { get; set; } = "Dedicated";

//260413 hbk Phase 6: ReuseFromShot 모드일 때 재사용할 Shot 이름 (D-07)
[Category("Datum|ImageSource")]
public string ReuseFromShotName { get; set; } = "";
```

**적용 시**: `[Category("Datum|ImageSource")] public string TeachingImagePath { get; set; } = "";`
만으로 PropertyTools.Wpf PropertyGrid 가 자동 노출. **별도 XAML 변경 0**.

> Note: 파일 Browse 버튼 같은 더 풍부한 UI 는 CONTEXT 가 "가능하면" 으로 deferred. 일단 평문 string 입력 PropertyGrid 노출이면 success criterion 충족.

---

## Shared Patterns

### Comment header / change marker convention
**Source:** project-wide convention (예: `DatumConfig.cs` L1, L17, L18 ...)
**Apply to:** Phase 22 의 **모든** 변경 라인
```csharp
//260511 hbk Phase 22 IMG-01 — <한국어 요지>
```
- 날짜는 `YYMMDD` (오늘=260511)
- 작성자 이니셜 `hbk` 고정
- Phase 번호 + Requirement 코드(`IMG-01`/`IMG-02`) 명시
- (MEMORY: `feedback_comment_convention.md` 강제)

### ParamBase Save/Load reflection contract
**Source:** `ParamBase.cs` L324-430
**Apply to:** DatumConfig 신규 string/int/double/bool 필드 추가 시 **항상**
- public auto-property 로 선언만 하면 자동 직렬화됨
- `[PropertyTools.DataAnnotations.Browsable(false)]` 부착 시 PropertyGrid 만 숨김 — INI 직렬화는 여전히 발생 (DatumConfig.cs L388 주석 명시: "ParamBase reflection이 Browsable 무시하고 public double 직렬화")
- 직렬화 제외가 필요하면 `[Newtonsoft.Json.JsonIgnore]` + 타입을 `HTuple`/`bool[]` 같은 non-supported 로 (DatumConfig.cs L430-449 참조)

### INI value missing key → null pattern
**Source:** `Ini.cs` L961-975 + `ParamBase.cs` L385-395
**Apply to:** Phase 22 의 신규 string 필드 소비처
- Load 결과는 `null` 가능 → 소비 직전 `string.IsNullOrEmpty(...)` 가드 필수
- 또는 `EnsurePerRoiDefaults` 패턴 (DatumConfig.cs L514) 으로 사전 정규화

### SIMUL_MODE conditional compile gate
**Source:** `Action_FAIMeasurement.cs` L110, L121, L225, L236
**Apply to:** TeachingImagePath/InspectionImagePath 로직이 SIMUL 전용일 경우
```csharp
#if SIMUL_MODE
// SIMUL-only 분기 (파일 로드)
#else
// 실제 카메라 grab
#endif
```

---

## No Analog Found

해당 없음 — 모든 Phase 22 변경은 동일 코드베이스 내 1:1 analog 보유.

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig, ShotConfig, FAIConfig, InspectionRecipeManager, InspectionSequence, Action_FAIMeasurement)
- `WPF_Example/Sequence/Param/ParamBase.cs` (직렬화 contract)
- `WPF_Example/Utility/Ini.cs` (INI 읽기/쓰기 폴백 규약)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (TryTeachDatum 진입점 — UI side)

**Files scanned:** 8 (worktree 사본 제외)

**Pattern extraction date:** 2026-05-11

**Key insight for planner:**
Phase 22 의 코드 변경량은 매우 작다 — **`DatumConfig` 에 string 1줄 추가 + 주석** 만으로 success criteria 1, 4, 5 가 충족된다.
Success criterion 2 ("InspectionImagePath 별도 변수") 는 기존 `ShotConfig.SimulImagePath` 를 의미상 재해석 (또는 alias 추가) 으로 회귀 0 달성 가능. 추가 변수가 정말 필요한지 planner 가 CONTEXT 와 Phase 23 사용처를 보고 판단할 것.
