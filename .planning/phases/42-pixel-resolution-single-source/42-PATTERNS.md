# Phase 42: 픽셀분해능 런타임 단일소스 - Pattern Map

**Mapped:** 2026-06-15
**Files analyzed:** 6 (all refactors of existing files, no new file creation)
**Analogs found:** 6 / 6

---

## File Classification

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Action_FAIMeasurement.cs:269,284` | action/controller | request-response | `Action_FAIMeasurement.cs:42` (ShotParam pattern) | exact — same file |
| `FAIConfig.cs:78-80` | model/param | CRUD | `DatumConfig.cs:265` (`[Browsable(false)]` static hide) | exact |
| `EdgePairDistanceMeasurement.cs:38-40` | model/param | CRUD | `DatumConfig.cs:265` (same static hide) | exact |
| `EdgePairDistanceMeasurement.cs:86-87` | service/measurement | request-response | `EdgePairDistanceMeasurement.cs:63` (Owner chain pattern) | exact — same file |
| `CameraSlaveParam.cs:25` | model/param | — | — | canonical source, no change |
| `MainView.xaml.cs:2187-2208` | UI/controller | request-response | same method, read-only reference | exact — same file |

---

## Pattern Assignments

### 1. D-01: `Action_FAIMeasurement.cs:269,284` — pixelResolution 소스 교체

**Change site:** Lines 269 and 284 — `fai.PixelResolutionX` → `ShotParam.PixelResolution`

**Current code (lines 260-291):**
```csharp
// DualImage 경로 (line 269)
ok = meas.TryExecute(image, transform, fai.PixelResolutionX, out resultValue, out measError, out measOverlays);

// 1-image 경로 (line 284)
ok = meas.TryExecute(image, transform, fai.PixelResolutionX, out resultValue, out measError, out measOverlays);
```

**ShotParam access pattern (analog: lines 42, 195 — same file):**
```csharp
// ShotParam 프로퍼티 — Action 이 직접 보유 (Owner chain walk 불필요)
public ShotConfig ShotParam => Param as ShotConfig;

// 사용 예 (EStep.Measure 루프, line 195)
foreach (var fai in ShotParam.FAIList) {
    // fai 는 루프 변수 — Owner == ShotParam (ShotConfig.AddFAI 에서 new FAIConfig(this, ...))
}
```

**Pattern to copy:**
- `ShotParam`은 `Param as ShotConfig`로 Action이 직접 보유한다. `fai.Owner as ShotConfig` walk 불필요.
- 루프 진입 전 `double pixRes = ShotParam.PixelResolution;` 로 1회 캡처 후 두 호출부에 전달한다.
- `fai.PixelResolutionX` 는 두 곳 모두 동일 교체 대상.

**Concrete action:**
`Action_FAIMeasurement.cs:195` 의 `foreach (var fai in ShotParam.FAIList)` 진입 직전(또는 `EStep.Measure` case 진입부)에 아래를 추가:
```csharp
double pixRes = ShotParam != null ? ShotParam.PixelResolution : 1.0;
```
그 후 line 269, 284의 `fai.PixelResolutionX` 를 `pixRes` 로 교체.

---

### 2. D-04: `FAIConfig.cs:78-80` — PixelResolutionX/Y PropertyGrid 숨김

**Change site:** Lines 76-80

**Current code:**
```csharp
// Calibration: camera-level calibration 은 CameraSlaveParam 에 저장되지만,
//  FAIConfig 도 RoiDefinition 호환을 위해 PixelResolution 을 보유한다.
[Category("Calibration")]
public double PixelResolutionX { get; set; } = 1.0;  // mm/pixel
public double PixelResolutionY { get; set; } = 1.0;  // mm/pixel
```

**Analog: `DatumConfig.cs:263-265` — 정적 Browsable(false) 패턴 (ICustomTypeDescriptor 없이 무조건 숨김):**
```csharp
// Circle 알고리즘은 EdgeDirection 대신 RadialDirection (Inward/Outward) 사용 → 영구 hide.
//  IsHiddenForAlgorithm CTH 분기 동적 hide 가 dynamic enumeration 경로 차이로 불안정 → 정적 Browsable(false) 로 확정.
[PropertyTools.DataAnnotations.Browsable(false)]
[System.ComponentModel.Description("...")]
[ItemsSourceProperty(nameof(Circle_EdgeDirectionList))]
public string Circle_EdgeDirection   { get; set; } = "";
```

**FAIConfig가 이미 ICustomTypeDescriptor를 구현하고 있으나** PixelResolutionX/Y는 조건부 숨김이 아니라 무조건 숨김이므로 정적 `[Browsable(false)]` 가 올바른 선택이다. DatumConfig 선례와 동일 결론.

**[Category("Calibration")] 처리:** 숨김 적용 후 `[Category("Calibration")]` 어트리뷰트는 그대로 유지해도 무방하나, FAIConfig 의 ICustomTypeDescriptor.GetProperties 가 이미 PropertyTools 필터 경로이므로 어트리뷰트 잔존이 INI 직렬화에 무영향임을 확인 (ParamBase는 Reflection 경로 사용).

**Pattern to copy:**
```csharp
// FAIConfig.cs:76-80 변경 후
// Calibration: INI 호환 잔존 저장용. 소비 없음 — Shot 단일소스(D-01). PropertyGrid 숨김.
[PropertyTools.DataAnnotations.Browsable(false)]
public double PixelResolutionX { get; set; } = 1.0;  // mm/pixel — INI 키 보존(D-07)
[PropertyTools.DataAnnotations.Browsable(false)]
public double PixelResolutionY { get; set; } = 1.0;  // mm/pixel — INI 키 보존(D-07)
```

**주의:** `[Category("Calibration")]` 제거 — Browsable(false) 이면 어트리뷰트 불필요. INI 직렬화는 ParamBase Reflection 경로이므로 Browsable 영향 없음.

---

### 3. D-05: `EdgePairDistanceMeasurement.cs:38-40` — PixelResolutionX/Y PropertyGrid 숨김

**Change site:** Lines 38-40

**Current code:**
```csharp
[Category("EdgePair|Calibration")]
public double PixelResolutionX { get; set; } = 1.0;
public double PixelResolutionY { get; set; } = 1.0;
```

**Analog: 동일 파일 lines 33-36 — 이미 적용된 Browsable(false) 패턴:**
```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
```

`EdgePairDistanceMeasurement` 는 ICustomTypeDescriptor 미구현 클래스이므로 `[PropertyTools.DataAnnotations.Browsable(false)]` 정적 어트리뷰트가 유일한 선택이다.

**Pattern to copy:**
```csharp
// EdgePairDistanceMeasurement.cs:38-40 변경 후
// INI 호환 잔존 저장용 — D-06 재배선 후 TryExecute 에서 소비 안 함.
[PropertyTools.DataAnnotations.Browsable(false)]
public double PixelResolutionX { get; set; } = 1.0;
[PropertyTools.DataAnnotations.Browsable(false)]
public double PixelResolutionY { get; set; } = 1.0;
```

---

### 4. D-06: `EdgePairDistanceMeasurement.cs:86-87` — temp FAIConfig 에서 pixelResolution 파라미터 사용

**Change site:** Lines 72-89 (temp FAIConfig 구성부)

**Current code:**
```csharp
// 래퍼용 임시 FAIConfig 구성 — FAIEdgeMeasurementService 재사용.
// ROI는 Owner에서, Edge/Calibration 파라미터는 self에서 가져온다.
var temp = new FAIConfig(Owner)
{
    ROI_Row       = ownerFai.ROI_Row,
    ROI_Col       = ownerFai.ROI_Col,
    ROI_Phi       = ownerFai.ROI_Phi,
    ROI_Length1   = ownerFai.ROI_Length1,
    ROI_Length2   = ownerFai.ROI_Length2,
    EdgeThreshold = EdgeThreshold,
    Sigma         = Sigma,
    EdgeDirection = EdgeDirection,
    EdgeSelection = EdgeSelection,
    EdgeSampleCount = EdgeSampleCount,
    EdgeTrimCount   = EdgeTrimCount,
    EdgePolarity    = EdgePolarity,
    PixelResolutionX = PixelResolutionX,   // line 86 — 교체 대상
    PixelResolutionY = PixelResolutionY,   // line 87 — 교체 대상
    FAIName = MeasurementName
};
```

**Owner 체인 analog (line 63 — 동일 파일):**
```csharp
// ROI 단일 소스: Owner(FAIConfig)에서 직접 참조 — 중복 저장 제거
var ownerFai = Owner as FAIConfig;
if (ownerFai == null)
{
    error = "Owner is not FAIConfig";
    return false;
}
```

`Owner`(FAIConfig).Owner == ShotConfig (ShotConfig.AddFAI → `new FAIConfig(this, ...)` 패턴, `ShotConfig.cs:149`).

**Pattern to copy:**
```csharp
// temp FAIConfig 구성 — PixelResolution 은 파라미터에서 (shot 단일소스 경유)
var ownerShot = ownerFai.Owner as ShotConfig;
double resolvedPixelRes = (ownerShot != null) ? ownerShot.PixelResolution : pixelResolution;

var temp = new FAIConfig(Owner)
{
    // ... (ROI, Edge 파라미터 동일) ...
    PixelResolutionX = resolvedPixelRes,   // 파라미터 경유 shot 단일소스
    PixelResolutionY = resolvedPixelRes,   // X=Y 정방형 가정 (D-09)
    FAIName = MeasurementName
};
```

`pixelResolution` 파라미터는 D-01 Rewire 후 `ShotParam.PixelResolution` 에서 오므로 `ownerShot != null` 분기는 방어 코드이다. 두 경로 모두 같은 값을 가리킨다.

---

### 5. 단일소스 기준값 — `CameraSlaveParam.cs:22-25` (변경 없음, 참조용)

**현재 코드 (lines 22-25):**
```csharp
[Category("General|AOI")]
public double PixelToUM_Offset { get; set; }
[System.ComponentModel.Description("mm/pixel calibration factor for this camera")]
public double PixelResolution { get; set; } = 1.0;  //260408 hbk mm/pixel (per D-12)
```

`ShotConfig extends CameraSlaveParam` → `ShotParam.PixelResolution` 이 모든 D-01/D-06 교체의 최종 단일소스. 이 필드는 PropertyGrid에 `[Category("General|AOI")]` 로 노출되어 있어 Shot 노드에서 편집 가능하다. **변경 없음.**

---

### 6. D-02 편집 진입점 — `MainView.xaml.cs:2187-2208` (변경 없음, 참조용)

**현재 코드 (lines 2187-2208):**
```csharp
/// <summary>Applies mm/pixel calibration to the current camera's CameraSlaveParam and all FAIs (per D-12).</summary>
private void ApplyCalibrationResult(double mmPerPixel) {
    var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
    FAIConfig anchorFai;
    if (selectedRow != null) anchorFai = FindFAIByName(selectedRow.FAIName);
    else                     anchorFai = null;
    if (anchorFai != null) {
        var shot = anchorFai.Owner as ShotConfig;
        if (shot == null) {
            CustomMessageBox.Show("샷 정보를 찾을 수 없습니다.", "캘리브레이션");
            return;
        }

        // CameraSlaveParam is the shot itself (ShotConfig extends CameraSlaveParam)
        shot.PixelResolution = mmPerPixel;

        // Also update all FAIs under this shot for RoiDefinition compatibility
        foreach (FAIConfig fai in shot.FAIList) {
            fai.PixelResolutionX = mmPerPixel;
            fai.PixelResolutionY = mmPerPixel;
        }
    }
}
```

**D-03 확정(다음 검사 자동 반영)에 따라 이 메서드는 변경 불필요.** `shot.PixelResolution` 이미 단일소스로 기록 중. FAI 각도도 D-01 Rewire 완료 후 소비 경로가 없어지므로 잔존 업데이트는 무해(INI 라운드트립 호환). **변경 없음.**

---

## Shared Patterns

### PropertyGrid 숨김: `[PropertyTools.DataAnnotations.Browsable(false)]`
**Source:** `DatumConfig.cs:265`, `EdgePairDistanceMeasurement.cs:33-36`
**Apply to:** `FAIConfig.cs:78-80`, `EdgePairDistanceMeasurement.cs:38-40`

이 프로젝트의 PropertyTools PropertyGrid에서 프로퍼티를 무조건 숨기는 패턴은 **정적 어트리뷰트**이다.
- `[PropertyTools.DataAnnotations.Browsable(false)]` — PropertyTools PropertyGrid 경로
- `[System.ComponentModel.Browsable(false)]` — WinForms 호환 경로 (선택적 병기)
- ICustomTypeDescriptor.IsHiddenForAlgorithm 패턴은 **조건부(알고리즘 타입별) 숨김** 전용으로 무조건 숨김에는 과도하다.

INI 직렬화는 `ParamBase.Save()/Load()` 가 `GetType().GetProperties()` System.Reflection 경로를 사용하므로 `[Browsable(false)]` 어트리뷰트와 무관하다. 필드 유지(INI 키 호환) + UI 숨김은 정적 어트리뷰트만으로 충분하다.

### Owner 체인 패턴
**Source:** `EdgePairDistanceMeasurement.cs:63`, `ShotConfig.cs:149`
**Apply to:** `EdgePairDistanceMeasurement.cs:86-87`

```
MeasurementBase.Owner == FAIConfig
FAIConfig.Owner       == ShotConfig  (ShotConfig.AddFAI → new FAIConfig(this, ...))
```
체인 walk: `var ownerFai = Owner as FAIConfig;` → `var ownerShot = ownerFai.Owner as ShotConfig;`

### 코멘트 컨벤션
**Source:** `CLAUDE.md` + `CameraSlaveParam.cs:25`
코드 수정 시 `//260615 hbk` 형식 주석 필수.

---

## No Analog Found

해당 없음. 모든 변경 사이트에 대해 동일 파일 또는 인접 파일의 명확한 analog가 존재한다.

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Sequence/Param/`, `WPF_Example/UI/ContentItem/`
**Files scanned:** 8 (Action_FAIMeasurement.cs, EdgePairDistanceMeasurement.cs, FAIConfig.cs, DatumConfig.cs, DynamicPropertyHelper.cs, CameraSlaveParam.cs, InspectionRecipeManager.cs, MainView.xaml.cs)
**Pattern extraction date:** 2026-06-15
