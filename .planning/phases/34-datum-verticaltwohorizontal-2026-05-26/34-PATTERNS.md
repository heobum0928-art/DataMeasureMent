# Phase 34: Datum VerticalTwoHorizontal 듀얼 티칭 이미지 — Pattern Map

**Mapped:** 2026-05-27
**Files analyzed:** 5 (modify) + 0 (new)
**Analogs found:** 5 / 5 (모두 동일 파일 내 selfanalog — 기존 3-way switch / 분기 패턴 그대로 확장)

> **CONTEXT:** `.planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-CONTEXT.md`
> **RESEARCH:** 사용자 옵션으로 skip (CONTEXT 의 D-IDs 가 sole source-of-truth)
> **선행 계승 phase:** 12 (D-09 enum), 17 (ICustomTypeDescriptor 분기), 22 (IMG-01/02 dual-path), 23 (ALG-01 TeachingImagePath 자동 로드), 35 (CO-33-02 hotfix DisplayDatumImage)

---

## File Classification

| 변경 대상 파일 | 역할 | 데이터 흐름 | Self-analog 위치 | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` | enum (model) | 정적 식별자 | line 12 `VerticalTwoHorizontal` | exact (1-line 추가) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (필드) | model (param) | INI persist + UI binding | line 44 `TeachingImagePath` | exact |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (AlgorithmTypeOptions) | UI dropdown | static list | lines 78–84 `AlgorithmTypeList` | exact |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (INI 정규화) | model lifecycle | 1회 정규화 hook | line 563 `if (TeachingImagePath == null)` | exact (Phase 22) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (IsHiddenForAlgorithm) | ICustomTypeDescriptor | runtime field visibility | lines 667–687 switch 3-case | exact (Phase 17) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (TryFindDatum) | service (vision) | dispatch switch | lines 54–63 | exact |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (TryTeachDatum) | service (vision) | dispatch switch | lines 572–581 | exact |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (TryFindVerticalTwoHorizontal) | service (vision) | image→3 ROI→fit | lines 371–~470 | exact (본문 분리 변형 base) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (TryTeachVerticalTwoHorizontal) | service (vision) | image→3 ROI teach | lines 913–~1100 | exact (본문 분리 변형 base) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (GetAlgorithmSteps) | UI wizard | step sequence | lines 1911–1922 | exact |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (ValidateRoiPresence) | UI guard | precheck | lines 1015–1036 | exact |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (StartDatumTeachStep) | UI wizard | drawing event hook | lines 1960–2011 | exact (image swap hook 새 site) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (DisplayDatumImage) | UI | path → HImage 표시 | lines 145–160 | exact (CO-33-02 패턴) |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (GrabOrLoadDatumImage) | sequence/action | image source resolve | lines 257–288 | exact (D-34-13 한정 분기 추가 site) |

---

## Pattern Assignments

### 1) `EDatumAlgorithm.cs` (enum, 1-line 추가)

**Self-analog:** 현재 enum body lines 10–12.

**추가 패턴** (line 12 뒤에 신규 라인):

```csharp
//260423 hbk Phase 12 D-09 — Datum 알고리즘 식별자 (PropertyGrid enum 자동 드롭다운 대상)
public enum EDatumAlgorithm //260423 hbk Phase 12 D-09
{
    TwoLineIntersect,           //260423 hbk 기존 Phase 4 방식 (Line1∩Line2) — default(EDatumAlgorithm)
    CircleTwoHorizontal,        //260423 hbk Circle 센터 수직 가상선 ∩ 수평 2-ROI concat
    VerticalTwoHorizontal,      //260423 hbk 수직 ROI ∩ 수평 2-ROI concat
    VerticalTwoHorizontalDualImage, //260527 hbk Phase 34 D-34-03 — 가로축 이미지(H_A+H_B) + 세로축 이미지(V) 분리 (Side fixture)
}
```

**제약 (Phase 12 D-09 / ParamBase 직렬화):**
- 새 enum 값은 **끝에 append** — 기존 1-image 3종 INI 호환 위해 순서 보존.
- ParamBase 는 string 으로만 직렬화 → DatumConfig.AlgorithmType (string) 가 이 enum 이름과 1:1.

---

### 2) `DatumConfig.cs` — 필드 추가 (Phase 22 IMG-01 패턴)

**Self-analog:** line 42–44 `TeachingImagePath`.

**기존 필드 패턴** (line 42–44):

```csharp
//260511 hbk Phase 22 IMG-01 — Datum 티칭 시 사용한 기준 이미지 경로. INI 직렬화는 ParamBase reflection 이 자동 처리 (ParamBase.cs L325-339 Save 의 case "String", L385-395 Load 의 case "String"). 검사 실행 시 이미지는 별도 ShotConfig.SimulImagePath 사용 — 역할 분리 유지. 키 미존재 → EnsurePerRoiDefaults 에서 "" 정규화 (T2).
[Category("Datum|ImageSource")]
public string TeachingImagePath { get; set; } = "";
```

**복제 추가** (line 44 직후):

```csharp
//260527 hbk Phase 34 D-34-04 — 가로축 이미지(TeachingImagePath) 와 분리된 세로축 이미지 경로.
//  algorithm == VerticalTwoHorizontalDualImage 일 때만 의미가 있으며, 그 외 algorithm 에서는 INI 에 보존되지만 미사용.
//  INI 직렬화 = ParamBase reflection String case 자동. 키 미존재 → EnsurePerRoiDefaults 에서 "" 정규화 (D-34-11).
[Category("Datum|ImageSource")]
public string TeachingImagePath_Vertical { get; set; } = "";
```

**필드 위치 권고:** `TeachingImagePath` 바로 다음 줄 — PropertyGrid Category 그룹화 유지.

**주의 — IOfflineImageParam 인터페이스는 변경 0:** `GetLatestImagePath/SetLatestImagePath` (lines 51–62) 는 기존 `TeachingImagePath` 만 다룸. Vertical 경로는 별도 UI hook 필요 (Plan 의 Claude's Discretion).

---

### 3) `DatumConfig.cs` — AlgorithmTypeOptions 리스트 (Phase 12 패턴)

**Self-analog:** lines 74–85 `AlgorithmTypeList`.

**기존 패턴** (lines 78–84):

```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> AlgorithmTypeList
{
    get
    {
        return new List<string>
        {
            EDatumAlgorithm.TwoLineIntersect.ToString(),
            EDatumAlgorithm.CircleTwoHorizontal.ToString(),
            EDatumAlgorithm.VerticalTwoHorizontal.ToString(),
        };
    }
}
```

**1-line 추가** (line 82 뒤):

```csharp
            EDatumAlgorithm.VerticalTwoHorizontal.ToString(),
            EDatumAlgorithm.VerticalTwoHorizontalDualImage.ToString(), //260527 hbk Phase 34 D-34-05
        };
```

**제약 (D-34-05):** ToString() 그대로 노출 (한글 라벨 매핑 없음). 사용자가 PropertyGrid 드롭다운에서 enum 이름 그대로 본다.

---

### 4) `DatumConfig.cs` — INI 정규화 hook (Phase 22 IMG-01 패턴)

**Self-analog:** line 563 — `EnsurePerRoiDefaults()` 내부의 단일 라인 null 가드.

**기존 패턴** (line 563):

```csharp
if (TeachingImagePath == null) TeachingImagePath = ""; //260511 hbk Phase 22 IMG-01 — INI 키 미존재 시 null 가드 (ParamBase.Load String case 가 IniValue.Default → null 로 SetValue 가능 → 소비처 string.IsNullOrEmpty 가드 보완). 멱등성 보장 — 빈 문자열 아닌 사용자 셋업 값은 보존.
```

**1-line 추가** (line 563 직후):

```csharp
if (TeachingImagePath == null) TeachingImagePath = "";          //260511 hbk Phase 22 IMG-01
if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = ""; //260527 hbk Phase 34 D-34-11 — Phase 22 패턴 1:1 복제 (algorithm 무관 적용 — DualImage 가 아니어도 필드 존재)
```

**제약 (D-34-11/D-34-12):**
- algorithm 분기 없이 일률 정규화 — 1-image algorithm 으로 시작한 기존 recipe 의 INI 라운드트립도 보장 (키 미존재 → "" → 라운드트립 시 "" 그대로 저장).
- 멱등성: 사용자가 ""아닌 값을 셋업하면 그대로 보존.

---

### 5) `DatumConfig.cs` — ICustomTypeDescriptor 분기 (Phase 17 D-09 패턴)

**Self-analog:** lines 667–687 `IsHiddenForAlgorithm` switch — VerticalTwoHorizontal 케이스 (lines 680–684).

**기존 VTH 패턴** (lines 680–684):

```csharp
case EDatumAlgorithm.VerticalTwoHorizontal:
    if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
    if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
    if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
    return false;
```

**신규 case 추가** (line 684 뒤, default 전):

```csharp
//260527 hbk Phase 34 D-34-04 — DualImage 변형: VTH 와 노출 그룹 동일 (Vertical_* + Horizontal_A_*/B_* 노출). 차이 = TeachingImagePath_Vertical 추가 노출 (별도 hide 분기 불필요 — 기본 노출).
case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
    if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
    if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
    if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
    return false;
```

**역방향 hide (필요):** `TeachingImagePath_Vertical` 는 **DualImage 가 아닌** algorithm 에서 PropertyGrid 노출 금지 → 다른 3 case (TLI/CTH/VTH) 안에서 `if (name == "TeachingImagePath_Vertical") return true;` 추가.

```csharp
//260527 hbk Phase 34 D-34-04 — TeachingImagePath_Vertical 은 DualImage 전용. 다른 algorithm 에서는 hide.
case EDatumAlgorithm.TwoLineIntersect:
    if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34
    // ... 기존 라인 유지
case EDatumAlgorithm.CircleTwoHorizontal:
    if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34
    // ... 기존 라인 유지
case EDatumAlgorithm.VerticalTwoHorizontal:
    if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34
    // ... 기존 라인 유지
```

**Phase 18 CO-01 학습:** Phase 17 의 dynamic hide 가 일부 enumeration 경로에서 불안정했던 케이스가 있으므로(line 262 Circle_EdgeDirection 의 `[Browsable(false)]` 정적 강제 fallback), 안 되면 `[Browsable(false)]` 정적화 검토.

---

### 6) `DatumFindingService.cs` — TryFindDatum dispatch (lines 54–63)

**Self-analog:** TryFindDatum switch — exact 1-case 확장.

**기존 패턴** (lines 54–63):

```csharp
//260503 hbk Phase 17 hotfix#7 — TryFindDatum 알고리즘 분기 추가 (Phase 12 D-04 누락 정정).
switch (config.AlgorithmTypeEnum)
{
    case EDatumAlgorithm.CircleTwoHorizontal:
        return TryFindCircleTwoHorizontal(image, config, out transform, out error);
    case EDatumAlgorithm.VerticalTwoHorizontal:
        return TryFindVerticalTwoHorizontal(image, config, out transform, out error);
    case EDatumAlgorithm.TwoLineIntersect:
    default:
        return TryFindTwoLineIntersect(image, config, out transform, out error);
}
```

**중요 — 시그니처 변경 필요:** DualImage 케이스는 **이미지 2장**을 받아야 한다 (D-34-02). 현재 `TryFindDatum(HImage image, ...)` 1장 입력 → 두 가지 옵션:

**옵션 A (권장):** 신규 오버로드 추가
```csharp
case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
    return TryFindVerticalTwoHorizontalDualImage(imageHorizontal, imageVertical, config, out transform, out error);
```
→ TryFindDatum 호출처(Action_FAIMeasurement) 가 algorithm 보고 두 image 모두 로드 후 새 오버로드 호출.

**옵션 B:** 동일 시그니처 유지, `config.TeachingImagePath_Vertical` 를 service 내부에서 로드.
→ service 가 파일 I/O 갖는 것은 기존 패턴(image 외부 주입)과 어긋남 — 비권장.

**Planner 결정 필요:** 옵션 A vs B (CONTEXT D-34-13 의 "Action_FAIMeasurement.GrabOrLoadDatumImage 단일 지점" 과 정합되려면 → A 채택).

**새 함수 본문 패턴:** 기존 `TryFindVerticalTwoHorizontal` (lines 371–~470) 본문을 그대로 복제하되, Vertical 라인 검출(lines 386–400)은 `imageVertical` 입력, Horizontal_A/B (lines 403–434) 는 `imageHorizontal` 입력.

```csharp
private bool TryFindVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)
{
    error = null;
    HOperatorSet.HomMat2dIdentity(out transform);

    // Vertical 라인 검출 — imageVertical 사용 (D-34-01)
    HTuple verticalWidth, verticalHeight;
    imageVertical.GetImageSize(out verticalWidth, out verticalHeight);
    if (!TryFindLine(imageVertical, verticalWidth, verticalHeight,
                     config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
                     config.Vertical_Length1, config.Vertical_Length2,
                     ...))
    { error = "Vertical: " + lineError; return false; }

    // Horizontal A — imageHorizontal 사용
    HTuple horizontalWidth, horizontalHeight;
    imageHorizontal.GetImageSize(out horizontalWidth, out horizontalHeight);
    if (!TryExtractEdgePoints(imageHorizontal, horizontalWidth, horizontalHeight,
                              config.Horizontal_A_Row, ...))
    ...
    // Horizontal B — imageHorizontal 사용
    ...
    // 이후 fit + transform 빌드는 기존 TryFindVerticalTwoHorizontal 동일
}
```

**제약 (D-34-13):** TryFindDatum 의 기존 switch 본문은 **분기 case 추가만** — 기존 3 case 본문은 0 라인 변경.

---

### 7) `DatumFindingService.cs` — TryTeachDatum dispatch (lines 572–581)

**기존 패턴** (lines 572–581):

```csharp
switch (config.AlgorithmTypeEnum)
{
    case EDatumAlgorithm.CircleTwoHorizontal:
        return TryTeachCircleTwoHorizontal(image, config, out error);
    case EDatumAlgorithm.VerticalTwoHorizontal:
        return TryTeachVerticalTwoHorizontal(image, config, out error);
    case EDatumAlgorithm.TwoLineIntersect:
    default:
        return TryTeachTwoLineIntersect(image, config, out error);
}
```

**대칭 변경:** TryTeachDatum 도 2-image 오버로드 추가 (옵션 A) — 호출처는 MainView.InvokeTryTeachDatum.

**중요 — 티칭 호출처 영향 평가:**
- 기존 `InvokeTryTeachDatum()` (MainView.xaml.cs 의 호출처) 가 단일 `HImage img = halconViewer.CurrentImage` 만 잡음.
- DualImage 티칭 = 호출처에서 두 이미지(가로축 = TeachingImagePath 경로 로드 + 세로축 = TeachingImagePath_Vertical 경로 로드) 준비 후 새 오버로드 호출.
- StartDatumTeachStep 의 Vertical step 진입 시점에 `halconViewer.CurrentImage` 이 imageVertical 로 swap 된 상태 → "현재 표시 중인 이미지"를 사용하면 Vertical step 에서는 imageVertical, HorizontalA/B step 에서는 imageHorizontal 이 잡힐 수 있으나, **TryTeach 는 전체 ROI 동시 처리** → 두 이미지를 모두 파일에서 로드해야 함.

**Self-analog (TryTeachVerticalTwoHorizontal body, lines 913–1100):** 본문 90% 동일, ROI별 image 입력만 분기.

---

### 8) `MainView.xaml.cs` — GetAlgorithmSteps step 순서 (D-34-07)

**Self-analog:** lines 1911–1922.

**기존 패턴:**

```csharp
//260424 hbk Phase 12 D-03 — 알고리즘별 ROI 단계 시퀀스
private static EDatumTeachStep[] GetAlgorithmSteps(EDatumAlgorithm alg) {
    switch (alg) {
        case EDatumAlgorithm.TwoLineIntersect:
            return new[] { EDatumTeachStep.Line1, EDatumTeachStep.Line2 };
        case EDatumAlgorithm.CircleTwoHorizontal:
            return new[] { EDatumTeachStep.Circle, EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB };
        case EDatumAlgorithm.VerticalTwoHorizontal:
            return new[] { EDatumTeachStep.Vertical, EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB };
        default:
            return new EDatumTeachStep[0];
    }
}
```

**1-case 추가 (순서 다름 — D-34-07):**

```csharp
case EDatumAlgorithm.VerticalTwoHorizontalDualImage: //260527 hbk Phase 34 D-34-07
    // 순서: HorizontalA → HorizontalB → (자동 image 전환) → Vertical → Done.
    // 1-image VTH (Vertical 먼저) 와 의도적으로 다름 — 가로축 이미지부터 시작하는 게 사용자 워크플로우.
    return new[] { EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB, EDatumTeachStep.Vertical };
```

**제약 (D-34-07):** EDatumTeachStep enum 자체는 **변경 0** (line 59 그대로). 순서만 분기.

---

### 9) `MainView.xaml.cs` — ValidateRoiPresence (D-34-09 가드)

**Self-analog:** lines 1015–1036.

**기존 VTH 패턴 (lines 1028–1033):**

```csharp
case EDatumAlgorithm.VerticalTwoHorizontal:
    if (d.Vertical_Length1 <= 0)
        return "Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요.";
    if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0)
        return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
    break;
```

**신규 case 추가 (D-34-09 + D-34-10 메시지 패턴):**

```csharp
case EDatumAlgorithm.VerticalTwoHorizontalDualImage: //260527 hbk Phase 34 D-34-09/10
    if (d.Vertical_Length1 <= 0)
        return "Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요.";
    if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0)
        return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
    if (string.IsNullOrEmpty(d.TeachingImagePath))
        return "가로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 가로축 이미지를 Load 해주세요."; //260527 hbk Phase 34 D-34-10
    if (string.IsNullOrEmpty(d.TeachingImagePath_Vertical))
        return "세로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 세로축 이미지를 Load 해주세요."; //260527 hbk Phase 34 D-34-10
    break;
```

**호출처 (검사 시점 가드 — D-34-09):** ValidateRoiPresence 는 **티칭** 사전체크. **검사 시점** 가드는 `Action_FAIMeasurement.GrabOrLoadDatumImage` (또는 그 직후) 에서 별도 가드 필요 — Plan 의 액션 사항으로 분리.

---

### 10) `MainView.xaml.cs` — StartDatumTeachStep + 자동 이미지 전환 (D-34-06)

**Self-analog:** lines 1960–2011 (step 별 drawing event hook).

**기존 Vertical step 진입** (lines 1980–1986):

```csharp
case EDatumTeachStep.Vertical:
    label_drawHint.Content = "Step 1/3: 수직 ROI를 드래그하세요";
    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
    label_drawHint.Visibility = Visibility.Visible;
    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
    halconViewer.StartRectangleDrawing();
    break;
```

**자동 image 전환 hook 추가 위치 (D-34-06):** Vertical case 진입부에 `_editingDatum.AlgorithmTypeEnum == VerticalTwoHorizontalDualImage` 분기 → `halconViewer.LoadImage(TeachingImagePath_Vertical)`.

**Self-analog (DisplayDatumImage, lines 145–160):**

```csharp
//260527 hbk Phase 35 — CO-33-02 hotfix: Datum 노드 선택 시 TeachingImagePath 표시.
public void DisplayDatumImage(DatumConfig datum) {
    if (datum == null) return;
    string path = datum.TeachingImagePath;
    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
    try {
        halconViewer.LoadImage(path);
        label_message.Visibility = Visibility.Collapsed;
    } catch (Exception ex) {
        Logging.PrintErrLog((int)ELogType.Error, ex.Message);
    }
}
```

**추가 패턴 (Vertical step 진입 시 image swap — Phase 35 DisplayDatumImage 재사용):**

```csharp
case EDatumTeachStep.Vertical:
    //260527 hbk Phase 34 D-34-06 — DualImage 변형이면 진입 직전에 세로축 이미지로 자동 swap.
    if (_editingDatum != null
        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
        string vpath = _editingDatum.TeachingImagePath_Vertical;
        if (!string.IsNullOrEmpty(vpath) && System.IO.File.Exists(vpath)) {
            try { halconViewer.LoadImage(vpath); }
            catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
        } else {
            //260527 hbk Phase 34 D-34-08 — 빈 경로: 캔버스 클리어 + 안내. 저장은 차단하지 않음.
            label_drawHint.Content = "세로축 이미지를 Load 해주세요";
            label_drawHint.Foreground = new SolidColorBrush(Colors.Orange);
            label_drawHint.Visibility = Visibility.Visible;
            break; //드로잉 시작 안 함 (return 대신 break — switch 종료)
        }
    }
    label_drawHint.Content = "Step 3/3: 수직 ROI를 드래그하세요"; //260527 hbk Phase 34 — step 순서 변경에 따른 라벨 갱신
    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
    label_drawHint.Visibility = Visibility.Visible;
    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
    halconViewer.StartRectangleDrawing();
    break;
```

**HorizontalA/B step 라벨 라벨 갱신:**
- 기존 라벨 ("Step 2/3" / "Step 3/3") 은 1-image VTH 기준. DualImage 는 순서가 (HA → HB → V) 이므로 라벨이 ("Step 1/3" / "Step 2/3" / "Step 3/3 수직") 으로 분기 필요.
- 가장 간단한 구현 = case 진입부에서 algorithm 보고 라벨 분기.

---

### 11) `MainView.xaml.cs` — DisplayDatumImage 확장 (선택사항)

**현재 (lines 145–160):** `TeachingImagePath` 만 표시.

**Phase 34 확장 권고 (Plan Claude's Discretion):** 트리에서 Datum 노드 선택 시 algorithm 이 DualImage 이고 wizard 진행 중이 아니면 **둘 중 어느 이미지를 표시할지** 결정 필요. 가장 안전한 default = `TeachingImagePath` (가로축) — 사용자가 wizard 시작 시 자동 swap.

```csharp
public void DisplayDatumImage(DatumConfig datum) {
    if (datum == null) return;
    string path = datum.TeachingImagePath; //260527 hbk Phase 34 — 기본 가로축 (DualImage 도 동일 — wizard 가 Vertical step 진입 시 swap)
    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
    try {
        halconViewer.LoadImage(path);
        label_message.Visibility = Visibility.Collapsed;
    } catch (Exception ex) {
        Logging.PrintErrLog((int)ELogType.Error, ex.Message);
    }
}
```

→ 본 phase 에서 본문 변경 0 권고. swap 책임은 StartDatumTeachStep 단일 site.

---

### 12) `Action_FAIMeasurement.cs` — GrabOrLoadDatumImage (D-34-13 한정 변경)

**Self-analog:** lines 257–288.

**기존 구조** (lines 257–288):

```csharp
private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {
    if (ShotParam == null) return null;
    HImage image = null;
    string teachingPath = null;
    if (parentSeq != null && parentSeq.DatumConfigs != null && parentSeq.DatumConfigs.Count > 0) {
        teachingPath = parentSeq.DatumConfigs[0].TeachingImagePath;
    }
    #if SIMUL_MODE
    if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) {
        try { image = new HImage(teachingPath); } catch { image = null; }
    }
    if (image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
        try { image = new HImage(ShotParam.SimulImagePath); } catch { image = null; }
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

**확장 (D-34-13 한정 변경 — 본 함수 외 0 라인 수정):**

옵션 A (시그니처 변경 — 권장): 함수가 **두 image 반환**하도록 OUT 파라미터 추가 또는 별도 함수 신설.

```csharp
//260527 hbk Phase 34 D-34-13 — DualImage 변형용 두 이미지 동시 로드. 기존 GrabOrLoadDatumImage 는 unchanged.
private bool TryGrabOrLoadDualDatumImages(InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {
    imageHorizontal = null;
    imageVertical = null;
    if (ShotParam == null) return false;

    DatumConfig datum = null;
    if (parentSeq != null && parentSeq.DatumConfigs != null && parentSeq.DatumConfigs.Count > 0) {
        datum = parentSeq.DatumConfigs[0];
    }
    if (datum == null) return false;

    string pathH = datum.TeachingImagePath;
    string pathV = datum.TeachingImagePath_Vertical;

    //260527 hbk Phase 34 D-34-09 — 빈 경로 가드 (검사 시점)
    if (string.IsNullOrEmpty(pathV)) {
        Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 티칭 이미지 경로가 비어 있습니다 (DualImage).");
        return false;
    }

    try { imageHorizontal = new HImage(pathH); } catch { imageHorizontal = null; }
    try { imageVertical = new HImage(pathV); } catch { imageVertical = null; }
    return imageHorizontal != null && imageVertical != null;
}
```

**호출처 (Run() 또는 step Measure 분기 — line 82 부근):**
- algorithm == DualImage 이면 신규 함수 호출 → TryFindVerticalTwoHorizontalDualImage 새 오버로드.
- else 기존 GrabOrLoadDatumImage + TryFindDatum 경로 유지.

**제약 (D-34-13):**
- 같은 파일 안에서만 변경. 분기 site = `GrabOrLoadDatumImage` **단일 지점** + Run() 의 분기 1 site (algorithm 보고 신규 함수 호출).
- D-06 가드 외 InspectionSequence.cs/VisionResponsePacket.cs 변경 **0 라인** (D-34-14).
- verification 시 `git diff Action_FAIMeasurement.cs` 라인 카운트 검증 권고.

---

## Shared Patterns (모든 변경 파일 적용)

### Comment Convention (CLAUDE.md memory feedback_comment_convention)
**모든 신규/수정 라인에 `//YYMMDD hbk` 주석 필수.**
- YYMMDD = 작업일 (예: 260527).
- Phase D-ID 명시 (D-34-01 ~ D-34-15).
- 예: `//260527 hbk Phase 34 D-34-04 — DualImage 변형 세로축 경로`.

**Source:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` 전체 — 모든 줄에 phase/date hbk 주석 부착.

### Brace Style — Halcon 모듈
**Allman:** opening brace 가 다음 줄.

**Source:** `DatumFindingService.cs` lines 35–43, 568–582.

```csharp
public bool TryFindDatum(HImage image, DatumConfig config, out HTuple transform, out string error)
{
    error = null;
    HOperatorSet.HomMat2dIdentity(out transform);
    ...
}
```

→ DatumFindingService.cs 의 모든 신규 함수는 Allman.

### Brace Style — DatumConfig/MainView
**K&R:** opening brace 가 같은 줄.

**Source:** `DatumConfig.cs` lines 520–622 (`EnsurePerRoiDefaults`), MainView lines 1911–1922.

```csharp
public void EnsurePerRoiDefaults() {
    int fbThreshold;
    if (EdgeThreshold > 0) fbThreshold = EdgeThreshold;
    else                   fbThreshold = 20;
    ...
}
```

→ DatumConfig.cs / MainView.xaml.cs 의 신규 라인은 K&R.

### 한국어 사용자 메시지 (UI-SPEC Copywriting Contract — Phase 17)
**Source:** `MainView.xaml.cs` lines 1019, 1024, 1030, 1048, 1050 — 모든 사용자 표시 메시지는 한국어 + 액션 가이드 ("…다시 시도하세요" / "…Load 해주세요").

**적용 대상:** D-34-10 의 신규 에러 메시지 ("세로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 세로축 이미지를 Load 해주세요.").

### Phase 22 IMG-02 역할 분리
**Source:** DatumConfig.cs line 42 의 주석 + Action_FAIMeasurement.cs line 109.

> Datum 티칭 시 사용한 기준 이미지 경로. 검사 실행 시 이미지는 별도 ShotConfig.SimulImagePath 사용 — 역할 분리 유지.

→ `TeachingImagePath_Vertical` 도 **티칭 전용** 의미 유지. 검사 실행 이미지 source 는 InspectionImagePath (ShotConfig.SimulImagePath) 가 기본이나, DualImage 변형에서는 검사 시점에도 두 이미지 로드 (D-34-13 GrabOrLoadDatumImage 분기).

### Phase 12 D-09 enum 직렬화 제약
**Source:** EDatumAlgorithm.cs line 3 comment + DatumConfig.cs lines 65–67.

> ParamBase switch-case가 enum을 미지원하므로 DatumConfig.AlgorithmType은 string으로 저장/로드됨.

→ 신규 enum 값 INI 키 = "VerticalTwoHorizontalDualImage" (ToString()). AlgorithmTypeEnum 게터의 미지원 문자열 폴백(line 95) 도 그대로 활용.

---

## Algorithm 분기 — Self-analog Cross-Reference (요약 표)

| 분기 site | 파일 | 라인 | 기존 case 수 | 신규 case 시 변경 |
|---|---|---|---|---|
| AlgorithmTypeList | DatumConfig.cs | 80–82 | 3 | 1줄 append |
| IsHiddenForAlgorithm | DatumConfig.cs | 668–684 | 3 | 1 case 추가 + 기존 3 case 1줄씩 (TIP_Vertical hide) |
| TryFindDatum switch | DatumFindingService.cs | 56–62 | 3 | 1 case (신규 오버로드 호출) |
| TryTeachDatum switch | DatumFindingService.cs | 574–580 | 3 | 1 case |
| GetAlgorithmSteps | MainView.xaml.cs | 1913–1920 | 3 | 1 case |
| ValidateRoiPresence | MainView.xaml.cs | 1018–1034 | 3 | 1 case |
| StartDatumTeachStep label | MainView.xaml.cs | 1965–2010 | 6 step | Vertical / HorizontalA / HorizontalB case 안에 algorithm 분기 (라벨 + image swap) |
| Action_FAIMeasurement Run() | Action_FAIMeasurement.cs | ~82 | (분기 없음) | algorithm 보고 DualImage 분기 1 site |
| GrabOrLoadDatumImage | Action_FAIMeasurement.cs | 257–288 | (단일 경로) | 신규 함수 추가 (TryGrabOrLoadDualDatumImages) — 기존 unchanged |

---

## No Analog Found

| 항목 | 사유 | 처리 방향 |
|---|---|---|
| HalconViewerControl 의 step-driven image swap event | 현재 step change 핸들러는 _datumTeachStep 변수 갱신 시 StartDatumTeachStep 직접 호출만 함 — image swap event 가 없음 | StartDatumTeachStep 의 Vertical case 안에 직접 `halconViewer.LoadImage(path)` 호출 (Plan 권고). 새 이벤트 도입 불요. |
| 2-image teach 호출처 (InvokeTryTeachDatum) | 단일 image 가정 — 새 오버로드 도입 시 호출 site 신설 필요 | `MainView.InvokeTryTeachDatum` 안에 algorithm 분기 추가 — 신규 함수 `InvokeTryTeachDualDatum()` 분리 또는 inline 분기. Planner 결정. |
| 검사 시점 가드 메시지 표시 | ValidateRoiPresence 는 티칭 사전체크 — 검사 사이클 가드 분리 | Action_FAIMeasurement.Run() 안에 가드 (Logging.PrintErrLog + FinishAction(EContextResult.Error)). UI 알림은 SequenceContext 의 error 전파 경로 그대로. |

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig.cs, EDatumAlgorithm.cs, Action_FAIMeasurement.cs)
- `WPF_Example/Halcon/Algorithms/` (DatumFindingService.cs)
- `WPF_Example/UI/ContentItem/` (MainView.xaml.cs)
- `WPF_Example/Sequence/Param/` (CameraParam.cs — IOfflineImageParam)

**Files scanned:** 5 main + 3 reference.

**Pattern extraction date:** 2026-05-27.

**Files NOT modified (D-06/D-34-14 가드):**
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — 변경 0 라인
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — 변경 0 라인

---

*Phase: 34-datum-verticaltwohorizontal-2026-05-26 — PATTERNS map ready for /gsd-plan-phase.*
