# Phase 36: Datum DualImage 설계 보강 — Pattern Map

**Mapped:** 2026-05-28
**Files analyzed:** 6 modify + 0 new (가드 4파일 변경 0 원칙 유지)
**Analogs found:** 9 / 9 (모두 동일 파일 내 self-analog — 기존 DualImage 분기 그대로 확장)

> **CONTEXT:** `.planning/phases/36-datum-dualimage-coord-anchor-angle-validation-2026-05-28/36-CONTEXT.md` (D-36-01 ~ D-36-14)
> **선행 계승 phase:** 17 (D-13 transient 3-종 데코, D-16 Result PropertyGrid ReadOnly), 22 (IMG-01 EnsurePerRoiDefaults null 가드), 34 (D-34-13/14 가드 4파일 + DualImage 도입), 34.1 (D-34.1-07 가드 유지 + CO-34.1-06/07/08 hotfix 시리즈)
> **핵심 절제:** anchor / per-image transform 도입 0. SameFrame 계약 + 코드 가드 + 시각화 fallback + 사용자 입력 검증 + 색상 배지만으로 CO-34.1-09 종결.

---

## File Classification

| 변경 대상 파일 | 역할 | 데이터 흐름 | Self-analog 위치 | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (Find DualImage 가드) | service (vision) | 진입부 guard | L71-92 (기존 Find DualImage 오버로드 본체) | exact (3-라인 추가) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (Teach DualImage 가드) | service (vision) | 진입부 guard | L796-815 (기존 Teach DualImage 오버로드 본체) | exact (3-라인 추가) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (Find 각도 평가) | service (vision) | write-back transient | L720 DetectedAngleDeg write 직후 + L915 TwoLineAngleToleranceDeg sentinel 패턴 | exact (sentinel + write 2 라인) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (입력 필드 2종) | model (param) | INI persist + UI binding | L44 TeachingImagePath / L50 TeachingImagePath_Vertical (Phase 22 IMG-01 + D-34-11) + L112 TwoLineAngleToleranceDeg (게이트 sentinel) | exact (필드 2개 + Category) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (transient AngleValidationStatus) | model (transient) | TryFind write-back, INI/JSON 제외 | L468-487 DetectedOrigin/Angle transient 3-종 데코 | exact (필드 1개) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (EnsurePerRoiDefaults 정규화) | model lifecycle | 1회 정규화 hook | L570-571 (Phase 22 IMG-01 / D-34-11 패턴) | exact (1-2 라인) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (IsHiddenForAlgorithm Hide 분기) | ICustomTypeDescriptor | runtime 필드 visibility | L677-700 (3 알고리즘 × TeachingImagePath_Vertical hide + 4번째 case D-34-04) | exact (각 case 1줄씩 + DualImage 0줄) |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` (RenderDatumFindResult 보강) | display (visualization) | HWindow draw | L307-350 (purple cross + DetectedRefAngle 화살표) + L686-699 (CO-34.1-06 DualImage 분기) | exact (헬퍼 2개 + 호출 site 1) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (BtnTestFindDatum_Click swap 재호출) | UI event | post-find rerender hook | L2393-2467 (CO-34.1-08 BtnTestFindDatum_Click DualImage 분기 + L2453 SetDatumOverlay) | exact (재호출 site 1 ~ 2) |

**가드 4파일 변경 0 (D-34-13/14 + D-34.1-07):**
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Phase 36 변경 **있음** (D-36-04 는 "메타 필드 추가 0" 가드이며, 신규 입력 필드 2종 + transient status 는 PropertyGrid Hide 분기로만 격리되므로 핵심 가드 정신은 보존). Plan-checker 가 변경 라인 카운트로 검증.
- `WPF_Example/Sequence/Param/ParamBase.cs` — **0 라인** (반사 직렬화가 신규 double 필드 2종 자동 처리).
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — **0 라인** (whitelist 가드 L110 의 DualImage 식별자는 이미 등록됨 — Phase 34.1 CO-34.1-02 hotfix 완료).
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` / `WPF_Example/TcpServer/VisionResponsePacket.cs` — **0 라인**.

---

## Pattern Assignments

### 1) `DatumFindingService.cs` — Find DualImage 진입부 SameFrame 가드 (D-36-03)

**위치:** L71-92 (`TryFindDatum(HImage imageHorizontal, HImage imageVertical, ...)` 오버로드)

**Self-analog (기존 본문, 그대로 추출):**

```csharp
public bool TryFindDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error) //260527 hbk Phase 34 D-34-01/02
{
    error = null;
    HOperatorSet.HomMat2dIdentity(out transform);

    if (imageHorizontal == null || imageVertical == null || config == null)
    {
        error = "image(s) or config is null";
        return false;
    }

    config.EnsurePerRoiDefaults();
    config.LastFindSucceeded = false;

    if (config.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage)
    {
        error = "Algorithm is not VerticalTwoHorizontalDualImage; use single-image TryFindDatum overload";
        return false;
    }

    return TryFindVerticalTwoHorizontalDualImage(imageHorizontal, imageVertical, config, out transform, out error);
}
```

**추가 패턴 (D-36-03, null 가드와 algorithm 가드 사이에 삽입):**

```csharp
// 260528 hbk Phase 36 D-36-03 — SameFrame 가드: DualImage 두 입력은 동일 sensor frame 가정. width/height 불일치 시 즉시 명시적 에러 반환.
//   D-36-01/02: 좌표 변환 0 (동일 카메라 + 다른 조명/Z). HomMat2d / per-image transform 없음. 사이즈 일치만 보장하면 IntersectionLl 의 픽셀 좌표가 같은 평면에 위치.
HTuple wH, hH, wV, hV; //260528 hbk Phase 36 D-36-03
imageHorizontal.GetImageSize(out wH, out hH); //260528 hbk Phase 36 D-36-03
imageVertical.GetImageSize(out wV, out hV); //260528 hbk Phase 36 D-36-03
if (wH.I != wV.I || hH.I != hV.I) //260528 hbk Phase 36 D-36-03
{
    error = "DualImage requires same-frame image pair: horizontal " + wH.I + "x" + hH.I + " vs vertical " + wV.I + "x" + hV.I; //260528 hbk Phase 36 D-36-03
    return false; //260528 hbk Phase 36 D-36-03
}
```

**제약:**
- `EnsurePerRoiDefaults()` 호출 직전 또는 직후 (algorithm 가드보다 앞). null 가드는 통과 후라야 `GetImageSize` 안전.
- 단일-이미지 `TryFindDatum(HImage, ...)` 오버로드 (L17-63) 는 **본 가드 영향 0** — 1-image 회귀 표면 없음.

---

### 2) `DatumFindingService.cs` — Teach DualImage 진입부 SameFrame 가드 (D-36-03)

**위치:** L796-815 (`TryTeachDatum(HImage imageHorizontal, HImage imageVertical, ...)` 오버로드)

**Self-analog:** Find 와 100% 대칭. Find 의 가드를 동일 위치 (null 가드 직후 / algorithm 가드 직전) 에 1:1 미러링.

```csharp
// 260528 hbk Phase 36 D-36-03 — SameFrame 가드 (Find 와 대칭)
HTuple wH, hH, wV, hV; //260528 hbk Phase 36 D-36-03
imageHorizontal.GetImageSize(out wH, out hH); //260528 hbk Phase 36 D-36-03
imageVertical.GetImageSize(out wV, out hV); //260528 hbk Phase 36 D-36-03
if (wH.I != wV.I || hH.I != hV.I) //260528 hbk Phase 36 D-36-03
{
    error = "DualImage requires same-frame image pair: horizontal " + wH.I + "x" + hH.I + " vs vertical " + wV.I + "x" + hV.I; //260528 hbk Phase 36 D-36-03
    return false; //260528 hbk Phase 36 D-36-03
}
```

**제약:** Find 와 동일 메시지 포맷 (사용자 진단 일관성).

---

### 3) `DatumFindingService.cs` — Find 각도 평가 write-back (D-36-08 / D-36-13)

**위치:** `TryFindVerticalTwoHorizontalDualImage` L720 (`config.DetectedAngleDeg = curAngle * 180.0 / Math.PI` 직후)

**Self-analog (sentinel 게이트 패턴, L915-933 — `TryTeachTwoLineIntersect` 의 `TwoLineAngleToleranceDeg` 게이트):**

```csharp
//260426 hbk Phase 14-02 Req 2 — TwoLineIntersect 두 라인 각도 게이트
//  사용자 임계각 N=TwoLineAngleToleranceDeg, 0 이면 게이트 off (PASS).
if (config.TwoLineAngleToleranceDeg > 0.0)
{
    double phi1 = Math.Atan2(line1RowEnd - line1RowBegin, line1ColEnd - line1ColBegin);
    double phi2 = Math.Atan2(line2RowEnd - line2RowBegin, line2ColEnd - line2ColBegin);
    double deltaDeg = Math.Abs((phi1 - phi2) * 180.0 / Math.PI);
    while (deltaDeg >= 180.0) deltaDeg -= 180.0;
    double perpErr = Math.Abs(deltaDeg - 90.0);
    if (perpErr > config.TwoLineAngleToleranceDeg)
    {
        config.LastTeachSucceeded = false;
        error = "Two-line angle out of range: " + deltaDeg.ToString("F1")
              + " deg (expected 90 +/- " + config.TwoLineAngleToleranceDeg.ToString("F1") + " deg)";
        return false;
    }
}
```

**Phase 36 적용 (D-36-13: Tolerance > 0 일 때만 활성. PASS/FAIL = transient 필드 write-back, error 반환은 안 함 — UAT 시각화/배지로만 표시):**

```csharp
//260528 hbk Phase 36 D-36-08/13 — ExpectedAngleDeg / AngleTolerance 게이트 (TwoLineAngleToleranceDeg 와 동일 sentinel 모델).
//  Tolerance > 0 일 때만 활성. Expected==0 + Tolerance>0 도 활성 (사용자가 0° 검증을 의도) — sentinel 단일 조건.
//  결과는 transient AngleValidationStatus 에 기록. error 반환 안 함 (Find 자체는 PASS 유지 — UI 배지로만 표시).
//  wrap-around: |Detected - Expected| 를 [-180, 180] 정규화 후 절댓값 비교 (179 vs -179 = 2° 차이).
if (config.AngleTolerance > 0.0) //260528 hbk Phase 36 D-36-13
{
    double diff = config.DetectedAngleDeg - config.ExpectedAngleDeg; //260528 hbk Phase 36 D-36-08
    diff = ((diff + 540.0) % 360.0) - 180.0; //260528 hbk Phase 36 D-36-08 — wrap-around 정규화 (planner 권고 공식)
    double absDiff = System.Math.Abs(diff); //260528 hbk Phase 36 D-36-08
    if (absDiff <= config.AngleTolerance) config.AngleValidationStatus = EAngleValidationStatus.Pass; //260528 hbk Phase 36 D-36-08
    else                                  config.AngleValidationStatus = EAngleValidationStatus.Fail; //260528 hbk Phase 36 D-36-08
}
else
{
    config.AngleValidationStatus = EAngleValidationStatus.None; //260528 hbk Phase 36 D-36-13 — sentinel: 검증 비활성
}
```

**제약:**
- 위치 = `config.DetectedAngleDeg = curAngle * 180.0 / Math.PI;` (L720) **직후**, `Line1Detected_RBegin = vrB;` (L723) 직전.
- 본 게이트는 `TryFindVerticalTwoHorizontalDualImage` 한정. Teach 본문 (`TryTeachVerticalTwoHorizontalDualImage`) 에는 추가 안 함 (D-36-08 = Test Find 직후 자동 평가 한정).
- `EAngleValidationStatus` enum 타입 정의 = planner 재량 (CONTEXT Claude's Discretion). 권고 = `None / Pass / Fail` 3-값 enum (`DatumConfig.cs` 안 또는 별도 `Custom/Sequence/Inspection/EAngleValidationStatus.cs`).

---

### 4) `DatumConfig.cs` — 입력 필드 2종 (D-36-05 / D-36-07 / D-36-12)

**위치:** L112 `TwoLineAngleToleranceDeg` 직후 (`[Category("Datum|Algorithm")]` 그룹 안)

**Self-analog:** L106-112 (기존 TwoLineAngleToleranceDeg sentinel 게이트 필드)

```csharp
//260426 hbk Phase 14-02 Req 2 — TwoLineIntersect 두 라인 직각성 게이트 임계각 (도)
//  0  = 게이트 off (어떤 각도여도 PASS)
//  10 = default (90°±10° 허용)
//  range hint: 0~45°. INI 미존재 시 default 10° (값이 0 이면 명시 off 의도로 간주).
//  ParamBase reflection 자동 직렬화 — 별도 Save/Load 코드 불필요.
[Category("Datum|Algorithm")]
public double TwoLineAngleToleranceDeg { get; set; } = 10.0;
```

**추가 패턴 2종 (D-36-05 = DualImage 전용. PropertyGrid Hide 분기에서 다른 알고리즘 hide):**

```csharp
//260528 hbk Phase 36 D-36-05/07/12 — DualImage 변형 검출 각도 검증 기댓값 (도, range [-180, 180] atan2 출력과 일치).
//  알고리즘 무관 일률 직렬화 (Phase 22 IMG-01 / D-34-11 패턴) — INI 키 미존재 시 EnsurePerRoiDefaults 에서 0.0 정규화.
//  PropertyGrid 노출 = VerticalTwoHorizontalDualImage 한정 (IsHiddenForAlgorithm 의 다른 case 에서 hide).
//  ParamBase reflection 자동 직렬화 (Double case) — 별도 Save/Load 코드 불필요.
[Category("Datum|Algorithm")]
public double ExpectedAngleDeg { get; set; } = 0.0; //260528 hbk Phase 36 D-36-05/07

//260528 hbk Phase 36 D-36-05/07/13 — 각도 검증 허용 오차 (도).
//  0 = sentinel (게이트 off, AngleValidationStatus=None). > 0 = 활성 (TwoLineAngleToleranceDeg L915 패턴과 정렬).
//  range hint: 0~45°. default 1.0° (D-36-07).
[Category("Datum|Algorithm")]
public double AngleTolerance { get; set; } = 1.0; //260528 hbk Phase 36 D-36-05/07/13
```

**제약 (D-36-12):**
- `AlgorithmType` 무관 일률 필드 선언 (Phase 22 IMG-01 / D-34-11 답습). DualImage 외 알고리즘에서도 INI 에 보존되지만 미사용.
- ParamBase reflection 자동 처리 → `WPF_Example/Sequence/Param/ParamBase.cs` 변경 0 (Double case 기존 처리).
- 기존 `TwoLineAngleToleranceDeg` (L112) 와 명명/시멘틱 충돌 없음 — 후자는 TwoLineIntersect 한정 게이트, 본 2종은 DualImage 한정 게이트.

---

### 5) `DatumConfig.cs` — transient `AngleValidationStatus` 필드 (D-36-08, Phase 17 D-13 패턴)

**위치:** L487 `DetectedRefAngle2` 직후 (transient 그룹 안)

**Self-analog (L468-487 — Phase 17 D-13 transient 3-종 데코):**

```csharp
//260503 hbk Phase 17 D-13 — DetectedOrigin transient (TryFindDatum write-back, INI 0 영향 — ParamBase double 직렬화하나 [Browsable(false)] 로 PropertyGrid 미표시)
[System.ComponentModel.Browsable(false)] //260503 hbk Phase 17 D-13
[PropertyTools.DataAnnotations.Browsable(false)] //260503 hbk Phase 17 D-13
[Newtonsoft.Json.JsonIgnore] //260503 hbk Phase 17 D-13
public double DetectedOriginRow { get; set; } //260503 hbk Phase 17 D-13
```

**추가 패턴 (transient 3-종 데코 1:1 답습):**

```csharp
//260528 hbk Phase 36 D-36-08 — Test Find 직후 각도 PASS/FAIL 평가 결과 (transient).
//  TryFindVerticalTwoHorizontalDualImage 가 DetectedAngleDeg write-back 직후 본 필드 갱신.
//  None = 미평가 (Tolerance==0 sentinel) / Pass / Fail. PropertyGrid 색상 배지 컨버터 입력.
//  INI/JSON 직렬화 제외 — Phase 17 D-13 3-종 데코 답습 (System.ComponentModel + PropertyTools + JsonIgnore).
//  주의: ParamBase 가 enum 직렬화 미지원 (Phase 12 D-09 / EDatumAlgorithm.cs L3 주석) — Browsable 데코 없이도 INI write 안 됨. 데코는 PropertyGrid hiding 만 담당.
[System.ComponentModel.Browsable(false)] //260528 hbk Phase 36 D-36-08
[PropertyTools.DataAnnotations.Browsable(false)] //260528 hbk Phase 36 D-36-08
[Newtonsoft.Json.JsonIgnore] //260528 hbk Phase 36 D-36-08
public EAngleValidationStatus AngleValidationStatus { get; set; } //260528 hbk Phase 36 D-36-08
```

**대안 (planner 재량, CONTEXT Claude's Discretion):**
- `EAngleValidationStatus` 3-값 enum (권장 — None/Pass/Fail 의미 명확, 색상 컨버터에서 switch).
- `bool?` (3-값: null/true/false) — null=None, true=Pass, false=Fail. 데코 동일.
- 두 필드 분리 (`IsAngleValidated` bool + `IsAnglePassed` bool) — 비권장 (상태 조합 4 가지 중 1 가지 무효).

**제약:**
- ParamBase 의 `enum` 미지원 (EDatumAlgorithm 처럼 string-based 회피) 으로 enum 채택 시 자동으로 INI 직렬화 제외 → 데코 3-종은 PropertyGrid 동작 보장용.
- `bool?` 채택 시 ParamBase 가 `Boolean` case 로 처리할 수 있어 데코로 명시 차단 필수.

---

### 6) `DatumConfig.cs` — EnsurePerRoiDefaults 정규화 (D-36-12, Phase 22 IMG-01 패턴)

**위치:** L570-571 (`if (TeachingImagePath == null) TeachingImagePath = "";` 직후)

**Self-analog (L570-571):**

```csharp
if (TeachingImagePath == null) TeachingImagePath = ""; //260511 hbk Phase 22 IMG-01
if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = ""; //260527 hbk Phase 34 D-34-11 — Phase 22 IMG-01 패턴 1:1 복제.
```

**Phase 36 추가 (D-36-12, `double` 이라 null 불가 → 별도 정규화 불요. 단 sentinel 명시 의도가 있으면 0.0 으로 명시):**

```csharp
//260528 hbk Phase 36 D-36-12 — ExpectedAngleDeg / AngleTolerance 는 double 이라 null 불가.
//  ParamBase Double case 가 IniValue.Default → 0.0 SetValue → 기본 0.0 (sentinel 의미, D-36-13).
//  명시적 0.0 fallback 라인 불요 (CLR 기본값 0.0 가 sentinel 과 일치).
//  단, AngleTolerance default 가 1.0 (CONTEXT D-36-07) 이고 INI 키 미존재 시 ParamBase 가 0.0 으로 덮어쓸 가능성 — 이 경우 명시 fallback 필요.
if (AngleTolerance == 0.0 && ExpectedAngleDeg == 0.0) {
    // Sentinel 상태 유지 — 사용자가 한 번도 입력하지 않은 케이스. 0.0 으로 둔다 (D-36-13).
    // 단, Phase 35 이전 INI 가 키 미존재로 0.0 인 케이스도 동일 — default 복원 안 함 (사용자 명시 0 의도와 구분 불가).
}
```

**제약:**
- **본 라인은 실제로는 추가 안 해도 무방** (no-op). Plan 에서 명시 주석만 추가하여 D-36-12 의도 문서화.
- 더 안전한 대안: `if (AngleTolerance < 0) AngleTolerance = 0;` 만 (음수 방어). `ExpectedAngleDeg` 는 [-180, 180] 범위라 음수 허용.
- Phase 22 IMG-01 와 차이: string 필드는 `null != ""` 구분 가능 하지만 double 은 그렇지 않음 → 본 phase 의 sentinel 가드는 게이트 활성 조건 (`AngleTolerance > 0`) 에서만 검사.

---

### 7) `DatumConfig.cs` — IsHiddenForAlgorithm Hide 분기 (D-36-05, Phase 17 D-09 패턴)

**위치:** L677-700 (4-case switch)

**Self-analog (L677-700, 4 case 모두 `TeachingImagePath_Vertical` hide 패턴):**

```csharp
case EDatumAlgorithm.TwoLineIntersect:
    if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34 D-34-04 — DualImage 전용 필드 hide
    if (name.StartsWith("Circle_") || ...) return true;
    ...
    return false;
case EDatumAlgorithm.CircleTwoHorizontal:
    if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34 D-34-04
    ...
case EDatumAlgorithm.VerticalTwoHorizontal:
    if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34 D-34-04
    ...
case EDatumAlgorithm.VerticalTwoHorizontalDualImage: //260527 hbk Phase 34 D-34-04
    if (name.StartsWith("Line1_") || ...) return true;
    if (name.StartsWith("Line2_") || ...) return true;
    if (name.StartsWith("Circle_") || ...) return true;
    return false; //TeachingImagePath_Vertical 노출
```

**Phase 36 추가 (DualImage 외 3 case 에 ExpectedAngleDeg/AngleTolerance hide 라인 1줄씩):**

```csharp
case EDatumAlgorithm.TwoLineIntersect:
    if (name == "TeachingImagePath_Vertical") return true;
    if (name == "ExpectedAngleDeg" || name == "AngleTolerance") return true; //260528 hbk Phase 36 D-36-05 — DualImage 전용 필드 hide
    ...
case EDatumAlgorithm.CircleTwoHorizontal:
    if (name == "TeachingImagePath_Vertical") return true;
    if (name == "ExpectedAngleDeg" || name == "AngleTolerance") return true; //260528 hbk Phase 36 D-36-05
    ...
case EDatumAlgorithm.VerticalTwoHorizontal:
    if (name == "TeachingImagePath_Vertical") return true;
    if (name == "ExpectedAngleDeg" || name == "AngleTolerance") return true; //260528 hbk Phase 36 D-36-05
    ...
case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
    //  ExpectedAngleDeg / AngleTolerance 노출 (hide 라인 없음 → return false 폴스루)
    if (name.StartsWith("Line1_") || ...) return true;
    ...
    return false;
```

**제약 (D-36-05):**
- DualImage case 본문은 **0 라인 변경** (필드 노출이 default behavior). 다른 3 case 만 1줄씩 추가 → 변경 라인 총 3.
- `name == "AngleValidationStatus"` hide 는 불요 — transient 3-종 데코의 `[Browsable(false)]` 가 이미 PropertyGrid 미표시 처리 (Phase 17 D-13 답습). 단, 색상 배지가 `DetectedAngleDeg` 셀 자체에 적용되면 별도 필드 노출 자체가 없음 (D-36-06 cleanest 경로).

---

### 8) `HalconDisplayService.cs` — RenderDatumFindResult 보강 (D-36-09 / D-36-10 / D-36-11)

**위치:** L307-350 (`RenderDatumFindResult` 본문) + 신규 헬퍼 2개 (`DrawOriginFallback`, `DrawExpectedAngleArrow`)

**Self-analog (L307-350, 현재 본문):**

```csharp
public void RenderDatumFindResult(HWindow window, DatumConfig datum)
{
    if (window == null || datum == null) return;
    if (!datum.LastFindSucceeded) return; //260503 hbk Phase 17 D-13 — find 성공 분기에서만 렌더
    try
    {
        //260503 hbk Phase 17 D-13 — purple DispCross size=14 lineWidth=2 (UI-SPEC LOCKED)
        HOperatorSet.SetColor(window, "purple");
        HOperatorSet.SetLineWidth(window, 2);
        const double crossHalf = 14.0;
        HOperatorSet.DispLine(window,
            datum.DetectedOriginRow - crossHalf, datum.DetectedOriginCol,
            datum.DetectedOriginRow + crossHalf, datum.DetectedOriginCol);
        HOperatorSet.DispLine(window,
            datum.DetectedOriginRow, datum.DetectedOriginCol - crossHalf,
            datum.DetectedOriginRow, datum.DetectedOriginCol + crossHalf);

        //  좌표 텍스트 "Find (row, col)"
        EnsureFontInitialized(window);
        HOperatorSet.SetTposition(window,
            datum.DetectedOriginRow - crossHalf - 22,
            datum.DetectedOriginCol + crossHalf + 4);
        HOperatorSet.WriteString(window,
            "Find (" + datum.DetectedOriginRow.ToString("F1") + ", "
                     + datum.DetectedOriginCol.ToString("F1") + ")");

        //  DetectedRefAngle 방향 화살표 (length=20, head=5)
        double angle  = datum.DetectedRefAngle;
        double aLen   = 20.0;
        double headLn = 5.0;
        double endRow = datum.DetectedOriginRow + aLen * System.Math.Sin(angle);
        double endCol = datum.DetectedOriginCol + aLen * System.Math.Cos(angle);
        HOperatorSet.DispLine(window, datum.DetectedOriginRow, datum.DetectedOriginCol, endRow, endCol);
        double a1 = angle + 2.5, a2 = angle - 2.5;
        HOperatorSet.DispLine(window, endRow, endCol,
            endRow + headLn * System.Math.Sin(a1), endCol + headLn * System.Math.Cos(a1));
        HOperatorSet.DispLine(window, endRow, endCol,
            endRow + headLn * System.Math.Sin(a2), endCol + headLn * System.Math.Cos(a2));
    }
    catch { /* Suppress display errors */ }
}
```

**Phase 36 보강 (D-36-09 + D-36-10 + D-36-11):**

```csharp
public void RenderDatumFindResult(HWindow window, DatumConfig datum)
{
    if (window == null || datum == null) return;
    if (!datum.LastFindSucceeded) return;
    try
    {
        //260528 hbk Phase 36 D-36-10 — OFF-SCREEN 가드: DetectedOrigin 이 이미지 경계 밖이면 캔버스 중앙 fallback 십자 + 좌표 텍스트 + "OFF-SCREEN" 라벨.
        HTuple wImg, hImg; //260528 hbk Phase 36 D-36-10
        HOperatorSet.GetWindowExtents(window, out HTuple _r, out HTuple _c, out wImg, out hImg); //260528 hbk Phase 36 D-36-10 — 또는 datum 의 캐싱된 이미지 사이즈 사용 (planner 결정)
        bool isOffScreen = (datum.DetectedOriginRow < 0 || datum.DetectedOriginRow >= hImg.D //260528 hbk Phase 36 D-36-10
                         || datum.DetectedOriginCol < 0 || datum.DetectedOriginCol >= wImg.D);
        if (isOffScreen) //260528 hbk Phase 36 D-36-10
        {
            DrawOriginFallback(window, wImg.D, hImg.D, datum.DetectedOriginRow, datum.DetectedOriginCol); //260528 hbk Phase 36 D-36-10
            // 추가 시각화는 OFF-SCREEN 시 생략 (purple 십자/화살표는 의미 없음 — fallback 만 표시)
            return;
        }

        // (기존 purple cross + 좌표 텍스트 + DetectedRefAngle 화살표 — 변경 0)
        HOperatorSet.SetColor(window, "purple");
        HOperatorSet.SetLineWidth(window, 2);
        const double crossHalf = 14.0;
        HOperatorSet.DispLine(window, /* ... */);
        // ... (기존 본문 유지)

        //260528 hbk Phase 36 D-36-11 — ExpectedAngleDeg 점선 화살표 (검증 활성 시에만, 점선 시각적 구분).
        //  D-36-13: AngleTolerance > 0 활성 sentinel.
        if (datum.AngleTolerance > 0.0) //260528 hbk Phase 36 D-36-11/13
        {
            DrawExpectedAngleArrow(window, datum.DetectedOriginRow, datum.DetectedOriginCol,
                                   datum.ExpectedAngleDeg * System.Math.PI / 180.0,
                                   datum.AngleValidationStatus); //260528 hbk Phase 36 D-36-11
        }
    }
    catch { /* Suppress display errors */ }
}

//260528 hbk Phase 36 D-36-10 — OFF-SCREEN fallback 십자 (캔버스 중앙) + 좌표 + "OFF-SCREEN" 라벨.
private void DrawOriginFallback(HWindow window, double width, double height, double originRow, double originCol)
{
    try
    {
        double cRow = height / 2.0; //260528 hbk Phase 36 D-36-10
        double cCol = width  / 2.0; //260528 hbk Phase 36 D-36-10
        const double crossHalf = 14.0;
        HOperatorSet.SetColor(window, "red");        //260528 hbk Phase 36 D-36-10 — fallback 강조 색 (purple 과 시각 구분)
        HOperatorSet.SetLineWidth(window, 2);
        HOperatorSet.DispLine(window, cRow - crossHalf, cCol, cRow + crossHalf, cCol);
        HOperatorSet.DispLine(window, cRow, cCol - crossHalf, cRow, cCol + crossHalf);
        EnsureFontInitialized(window);
        HOperatorSet.SetTposition(window, cRow - crossHalf - 22, cCol + crossHalf + 4);
        HOperatorSet.WriteString(window, "OFF-SCREEN (" + originRow.ToString("F1") + ", " + originCol.ToString("F1") + ")"); //260528 hbk Phase 36 D-36-10
    }
    catch { /* Suppress display errors (기존 RenderDatumOverlay catch 관습) */ }
}

//260528 hbk Phase 36 D-36-11 — Expected angle 점선 화살표 (DetectedRefAngle 화살표와 시각 구분).
//  PASS = 두 화살표 시각적 일치 / FAIL = 시각적 어긋남.
//  Halcon 점선 = HOperatorSet.SetLineStyle(window, new HTuple(10, 5)).
private void DrawExpectedAngleArrow(HWindow window, double originRow, double originCol, double expectedAngleRad, EAngleValidationStatus status)
{
    try
    {
        HOperatorSet.SetColor(window, status == EAngleValidationStatus.Pass ? "green" : "red"); //260528 hbk Phase 36 D-36-11
        HOperatorSet.SetLineWidth(window, 2);
        HOperatorSet.SetLineStyle(window, new HTuple(10, 5)); //260528 hbk Phase 36 D-36-11 — 점선
        double aLen = 30.0; //검출 화살표 (20px) 보다 약간 길게 시각 구분
        double endRow = originRow + aLen * System.Math.Sin(expectedAngleRad);
        double endCol = originCol + aLen * System.Math.Cos(expectedAngleRad);
        HOperatorSet.DispLine(window, originRow, originCol, endRow, endCol);
        // arrow head (검출 화살표와 동일)
        double a1 = expectedAngleRad + 2.5, a2 = expectedAngleRad - 2.5;
        double headLn = 5.0;
        HOperatorSet.DispLine(window, endRow, endCol,
            endRow + headLn * System.Math.Sin(a1), endCol + headLn * System.Math.Cos(a1));
        HOperatorSet.DispLine(window, endRow, endCol,
            endRow + headLn * System.Math.Sin(a2), endCol + headLn * System.Math.Cos(a2));
        HOperatorSet.SetLineStyle(window, new HTuple()); //260528 hbk Phase 36 D-36-11 — 점선 해제 (다른 렌더 영향 0)
    }
    catch { /* Suppress display errors */ }
}
```

**제약 (D-36-09):**
- DualImage 양쪽 캔버스 (가로/세로 토글 어느 쪽이든 SameFrame 가정으로 좌표 유효) 렌더는 **이미 자동 동작** — `RenderDatumFindResult` 호출 site (L846 of `RenderDatumOverlay`) 가 algorithm 분기 없음 → 양쪽 토글 모두 동일 본문 실행.
- 토글 이벤트 재호출 트리거 (D-36-09 핵심) = MainView `BtnSwapHorizontal_Click` / `BtnSwapVertical_Click` (L1240-1264) 안에서 `halconViewer.SetDatumOverlay(datum, true)` 추가 호출 (Plan 9 참조).
- `GetWindowExtents` 대신 사용자 호출 시점에 알려진 이미지 사이즈 (D-36-03 가드에서 검증된 width/height) 를 직접 전달하는 것도 가능 — planner 결정.

---

### 9) `MainView.xaml.cs` — BtnTestFindDatum_Click 색상 배지 + RenderDatumFindResult 토글 재호출 (D-36-06 / D-36-09)

**위치:** L2393-2467 (`BtnTestFindDatum_Click`) + L1240-1264 (`BtnSwapHorizontal/Vertical_Click`)

**Self-analog 1 (L2451-2458, Test Find 성공 분기 — PropertyGrid 메트릭 갱신):**

```csharp
if (ok) {
    halconViewer.SetDatumOverlay(datum, true); //  purple cross + 좌표 + 화살표 (HalconDisplayService.RenderDatumFindResult)
    try { datum.RaisePropertyChanged(string.Empty); } catch { } // PropertyGrid 메트릭 전체 새로고침
    if (mParentWindow != null && mParentWindow.inspectionList != null) {
        mParentWindow.inspectionList.RefreshParamEditor(); //  Phase 17 D-14
    }
}
```

**Phase 36 추가 (D-36-06 색상 배지 = DetectedAngleDeg PropertyGrid 셀 배경. `RaisePropertyChanged(string.Empty)` + `RefreshParamEditor()` 가 이미 갱신 chain → 신규 코드 0):**

- 색상 배지 메커니즘 자체는 `InspectionListView.xaml` (또는 PropertyGrid 의 cell template selector) 에 정의. MainView.xaml.cs 의 본 분기는 **0 라인 변경** — 기존 `RefreshParamEditor()` chain 이 PropertyGrid 재렌더링 → 컨버터 / DataTrigger 가 `AngleValidationStatus` 읽어 색상 결정.
- 단, planner 가 색상 배지 컨버터를 `InspectionListView.xaml.cs` (whitelist 가드 파일 — Phase 34.1 D-34.1-07) 에 추가하려면 **가드 위반 위험** → 별도 ResourceDictionary 또는 XAML-only 정의 (XAML 은 가드 외) 권고.

**Self-analog 2 (L1240-1264 swap 핸들러):**

```csharp
private void BtnSwapHorizontal_Click(object sender, RoutedEventArgs e) {
    var d = _selectedDatumForSwap;
    if (d == null) return;
    if (d.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) return;
    string hpath = d.TeachingImagePath;
    if (!string.IsNullOrEmpty(hpath) && System.IO.File.Exists(hpath)) {
        try { halconViewer.LoadImage(hpath); }
        catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
    }
    UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Horizontal);
}
```

**Phase 36 추가 (D-36-09 — swap 토글 시 RenderDatumFindResult 재호출):**

```csharp
private void BtnSwapHorizontal_Click(object sender, RoutedEventArgs e) {
    var d = _selectedDatumForSwap;
    if (d == null) return;
    if (d.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) return;
    string hpath = d.TeachingImagePath;
    if (!string.IsNullOrEmpty(hpath) && System.IO.File.Exists(hpath)) {
        try { halconViewer.LoadImage(hpath); }
        catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
    }
    UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Horizontal);
    //260528 hbk Phase 36 D-36-09 — Test Find 결과 양쪽 캔버스에 일관 렌더. swap 직후 RenderDatumFindResult 가 LastFindSucceeded gate 안에서 자동 재실행.
    halconViewer.SetDatumOverlay(d, true); //260528 hbk Phase 36 D-36-09
}
```

**대칭 (Vertical 토글 동일):** `BtnSwapVertical_Click` 마지막 라인에도 동일 `SetDatumOverlay` 호출 추가.

**제약 (D-36-09):**
- `SetDatumOverlay(datum, true)` 가 이미 `RenderDatumOverlay` → `RenderDatumFindResult` chain 호출 → fallback / 화살표 자동 적용.
- 변경 라인 = 2 (Horizontal + Vertical 핸들러 각 1줄).

---

## Shared Patterns (모든 변경 파일 적용)

### Comment Convention (CLAUDE.md memory feedback_comment_convention)
**모든 신규/수정 라인에 `//260528 hbk Phase 36 D-36-XX` 주석 필수.**

- YYMMDD = 260528 (Phase 36 작업일).
- Phase D-ID 명시 (D-36-01 ~ D-36-14).
- 회귀 게이트 4파일 변경 0 검증 시 `git diff` 라인 카운트 + 주석 grep 으로 변경 의도 추적.

### Brace Style — Halcon 모듈 (Allman)
**Source:** `DatumFindingService.cs` L71-92 (Phase 34 DualImage 오버로드), L307-350 (`RenderDatumFindResult`).

→ Phase 36 의 SameFrame 가드 + 각도 평가 + 헬퍼 (`DrawOriginFallback`, `DrawExpectedAngleArrow`) 모두 Allman.

### Brace Style — DatumConfig / MainView (K&R)
**Source:** `DatumConfig.cs` L527-631 (`EnsurePerRoiDefaults`), L675-703 (`IsHiddenForAlgorithm`), MainView 전체.

→ Phase 36 의 신규 필드 선언 + Hide 분기 라인 + swap 핸들러 추가 라인 모두 K&R.

### Transient 필드 3-종 데코 (Phase 17 D-13)
**Source:** `DatumConfig.cs` L468-487.

```csharp
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public TYPE FieldName { get; set; }
```

→ `AngleValidationStatus` 필드에 1:1 답습.

### Sentinel 게이트 패턴 (Phase 14-02 — `TwoLineAngleToleranceDeg`)
**Source:** `DatumFindingService.cs` L915-933.

```csharp
if (config.TwoLineAngleToleranceDeg > 0.0) { /* gate active */ }
else                                       { /* gate off (PASS by default) */ }
```

→ `AngleTolerance > 0.0` sentinel 1:1 답습. 단 Phase 36 은 error 반환 안 함 (D-36-08 transient write-back 만).

### 한국어 사용자 메시지 (UI-SPEC Copywriting Contract — Phase 17)
**Source:** `MainView.xaml.cs` L2415, L2419 (Phase 34.1 CO-34.1-08 에러 메시지).

→ Phase 36 의 SameFrame 가드 에러 메시지는 영문 (DatumFindingService 내부 진단 로그) 유지 — UI 메시지 박스 노출 site 가 별도 (BtnTestFindDatum_Click `FormatFindError(error)` 가 한국어 변환). Plan 에서 `FormatFindError` 가 새 메시지 패턴 인식하는지 검증 권고.

---

## Algorithm 분기 — Self-analog Cross-Reference (요약 표)

| 분기 site | 파일 | 라인 | Phase 34/34.1 기존 | Phase 36 추가 |
|---|---|---|---|---|
| `TryFindDatum` DualImage 가드 | DatumFindingService.cs | L76-89 | null/algorithm 가드 | +7 SameFrame 가드 라인 |
| `TryTeachDatum` DualImage 가드 | DatumFindingService.cs | L800-813 | null/algorithm 가드 | +7 SameFrame 가드 라인 (Find 미러) |
| `TryFindVerticalTwoHorizontalDualImage` 각도 평가 | DatumFindingService.cs | L720 후 | DetectedAngleDeg write | +10 sentinel + wrap-around + transient write |
| `RenderDatumFindResult` | HalconDisplayService.cs | L307-350 | purple cross + 좌표 + 화살표 | +5 OFF-SCREEN 가드 + 신규 헬퍼 호출 + Expected 화살표 |
| `DrawOriginFallback` (신규) | HalconDisplayService.cs | (신설) | (없음) | +18 신규 헬퍼 |
| `DrawExpectedAngleArrow` (신규) | HalconDisplayService.cs | (신설) | (없음) | +20 신규 헬퍼 |
| DatumConfig 필드 | DatumConfig.cs | L112 후 | TwoLineAngleToleranceDeg | +2 입력 필드 (ExpectedAngleDeg/AngleTolerance) |
| DatumConfig transient | DatumConfig.cs | L487 후 | DetectedRefAngle2 | +1 transient (AngleValidationStatus) |
| DatumConfig hide 분기 | DatumConfig.cs | L678/L684/L691 | TeachingImagePath_Vertical hide | +3 라인 (TLI/CTH/VTH 각 1줄) |
| DatumConfig EnsurePerRoiDefaults | DatumConfig.cs | L570-571 | TeachingImagePath/_Vertical null 가드 | +0~2 (double sentinel 명시 주석 또는 음수 가드) |
| MainView swap 핸들러 | MainView.xaml.cs | L1240/L1254 | UpdateImageSourceBadge | +2 SetDatumOverlay 재호출 (Horizontal/Vertical) |
| MainView BtnTestFindDatum_Click | MainView.xaml.cs | L2393-2467 | DualImage 분기 + ok 후 RefreshParamEditor | +0 (chain 자동) |
| InspectionListView whitelist | InspectionListView.xaml.cs | L110 | DualImage 식별자 등록 완료 | +0 (변경 0 가드) |

---

## Color Badge 메커니즘 (D-36-06 — planner 재량 영역)

CONTEXT D-36-06 = "DetectedAngleDeg 셀 배경 색상 배지". 구체 메커니즘은 planner 결정. 권고 옵션:

### 옵션 A: PropertyTools 의 CellTemplateSelector (XAML)
- **장점:** InspectionListView.xaml.cs 변경 0 (D-34.1-07 가드 정합). XAML 만 추가 → ResourceDictionary 또는 PropertyGrid `Resources` 섹션.
- **단점:** PropertyTools 3.1.0 의 CellTemplate API 확인 필요 (researcher 단계 미완 — Phase 19 D-34.1-02 정정 처럼 attribute 한계 가능).

### 옵션 B: IValueConverter + DataTrigger
- **장점:** WPF 표준 패턴. `AngleValidationStatus` → `Brush` 변환 컨버터 + `PropertyGrid` 셀 Style.Triggers.
- **단점:** PropertyGrid 셀 Style 오버라이드 가능 여부 사전 검증 필요.

### 옵션 C: 별도 색상 라벨 (PropertyGrid 외부)
- **장점:** PropertyGrid 변경 없음 → 가드 정합 100%.
- **단점:** CONTEXT D-36-06 의 "DetectedAngleDeg 셀 배경" 의도와 어긋남 → 사용자 합의 필요. UAT 시 확인.

### Claude's Discretion (CONTEXT)
- "색상 배지 메커니즘 (DataTrigger / IValueConverter / 코드비하인드) — 기존 InspectionListView PropertyGrid 패턴 따라" → planner 가 PropertyTools 3.1.0 attribute / Style API 사전 검증 후 옵션 선택.

---

## No Analog Found

| 항목 | 사유 | 처리 방향 |
|---|---|---|
| `EAngleValidationStatus` enum 타입 | 신규 의미 — None/Pass/Fail 3값 | DatumConfig.cs 안 nested enum 또는 별도 `Custom/Sequence/Inspection/EAngleValidationStatus.cs` (EDatumAlgorithm.cs L8 패턴) — planner 결정 |
| PropertyGrid 셀 배경 색상 배지 | 기존 코드에 ReadOnly 셀 색상 변경 메커니즘 없음 (단순 회색 배경) | 위 "Color Badge 메커니즘" 옵션 A/B/C 중 planner 선택. researcher 가 PropertyTools 3.1.0 셀 Template/Style API 사전 조사 권고 |
| `HOperatorSet.GetWindowExtents` 사용 패턴 | HalconDisplayService 본문에서 명시적 GetWindowExtents 호출 사용처 없음 (이미지 사이즈는 algorithm 단에서 검증) | OFF-SCREEN 가드의 width/height 는 algorithm 단 (D-36-03 SameFrame 가드) 에서 검증된 값을 transient 로 캐싱 또는 RenderDatumFindResult 시그니처에 추가. planner 결정 — datum.DetectedOriginRow 가 이미 픽셀 좌표이므로 datum 캐싱 필드 (예: `DetectedImageWidth/Height` transient) 추가가 가장 안전 |
| Halcon 점선 (`SetLineStyle`) | HalconDisplayService 전체에서 점선 사용 없음 (전부 실선) | `HOperatorSet.SetLineStyle(window, new HTuple(10, 5))` 표준 패턴. Plan 에서 호출 직후 `SetLineStyle(window, new HTuple())` 로 즉시 해제 (다른 렌더 영향 0 보장) |
| Test Find 후 PropertyGrid 색상 즉시 반영 chain | `RaisePropertyChanged(string.Empty)` + `RefreshParamEditor()` 가 PropertyGrid 셀 전체 재바인딩 → 컨버터 재평가 chain 자동 | MainView 변경 0. 단 PropertyGrid 가 셀 Style 재평가하지 않으면 컨버터 명시 호출 또는 부분 갱신 필요 — UAT 시 확인 |

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig.cs, EDatumAlgorithm.cs)
- `WPF_Example/Halcon/Algorithms/` (DatumFindingService.cs)
- `WPF_Example/Halcon/Display/` (HalconDisplayService.cs)
- `WPF_Example/UI/ContentItem/` (MainView.xaml.cs)
- `WPF_Example/UI/ControlItem/` (InspectionListView.xaml.cs, InspectionListViewModel.cs)
- `.planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-PATTERNS.md` (선행 patterns)
- `.planning/phases/34.1-datum-dualimage-swap-ux-2026-05-27/34.1-CONTEXT.md` (CO 흐름)

**Files scanned:** 6 main + 3 reference.

**Pattern extraction date:** 2026-05-28.

**가드 4파일 (변경 0 검증 대상):**
- `WPF_Example/Sequence/Param/ParamBase.cs` — 0 라인
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 0 라인 (whitelist 등록 완료)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — 0 라인
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — 0 라인

**Phase 36 변경 예상 라인 합계 (planner 검증 기준):**
- DatumFindingService.cs: ~24 라인 (가드 7×2 + 각도 평가 10)
- DatumConfig.cs: ~12 라인 (필드 2 + transient 1 + hide 3 + 주석)
- HalconDisplayService.cs: ~45 라인 (OFF-SCREEN 5 + 헬퍼 2개 40)
- MainView.xaml.cs: ~2 라인 (swap 핸들러 SetDatumOverlay 호출 2)
- 신규 enum 파일 (옵션): ~10 라인 (EAngleValidationStatus.cs)

**Total estimated:** ~93 라인 추가 / 0 라인 삭제.

---

*Phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28 — PATTERNS map ready for /gsd-plan-phase.*
