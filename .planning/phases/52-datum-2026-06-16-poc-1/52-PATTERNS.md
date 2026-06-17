# Phase 52: 이미지 수평 보정 (Datum 에지 기반 회전 정렬) - Pattern Map

**Mapped:** 2026-06-17
**Files analyzed:** 5 (수정 4 + 신규 0~1)
**Analogs found:** 5 / 5 (모두 동일 코드베이스 내 직접 재사용 분석 대상)

> **CONTEXT 핵심 결정 재확인:**
> - D-01 회전각 = 시퀀스당 지정 "레벨링 기준" Datum 1개의 수평 2-ROI concat 피팅 라인 + 각도
> - D-02 grab 직후 전처리 회전 (좌표보정 아님, 실제 이미지 회전)
> - D-03 시퀀스당 1회 산출 → 전 SHOT 공유
> - D-04 시퀀스 단위 토글 + INI 저장, 기본 off
>
> **재량(Discretion):** 회전 구현(rotate_image vs affine_trans_image+hom_mat2d), 회전중심/경계처리, 사전검출패스, angle_lx 부호규약 — researcher/planner 확정.

---

## 핵심 API 사실 확인 (planner 필독)

코드베이스 전수 grep 결과, **참조 HDevelop 스크립트의 API 일부는 현 코드에 선례가 없다**:

| HDevelop API | 코드베이스 선례 | 비고 |
|--------------|----------------|------|
| `angle_lx` | **없음 (0건)** | 프로젝트는 라인 각도를 `Math.Atan2(rEnd-rBegin, cEnd-cBegin)`(radian) 로 산출. `DatumFindingService` L516/698/937 의 `curAngle` 가 정확히 이 패턴. → 레벨링 각도는 `angle_lx` 호출 대신 기존 `Math.Atan2` 산출각 재사용이 일관적. |
| `fit_line_contour_xld` | **있음** — `HOperatorSet.FitLineContourXld(contour,"tukey",-1,0,5,2, out rB,cB,rE,cE,nr,nc,df)` (`DatumFindingService` L487) | 수평 2-ROI concat 피팅 = 그대로 재사용. |
| `gen_contour_polygon_xld` | **있음** — `HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols)` (L482) | 동일. |
| `get_contour_xld` / `UnionContours` 2개 | 동등 = `TupleConcat` 로 A+B 에지점 결합 후 GenContourPolygonXld (L480-482) | HDevelop 의 Union→fit→get→polygon→fit 2-pass 단순화 경로 = 현 코드 1-pass concat-fit 과 등가. planner 가 2-pass 재현 필요성 판단. |
| `affine_trans_image` (HImage) | **없음 (0건)** | 신규. hom_mat2d 점변환(`AffineTransPoint2d`)만 존재. |
| `rotate_image` (HImage) | **부분** — `HImage.RotateImage(90/180/270, "constant")` (카메라 드라이버만). **임의각 불가** (HALCON rotate_image 는 90 배수만). | 임의각 레벨링 회전 = `affine_trans_image` + `hom_mat2d_rotate` 신규 필요. |
| `hom_mat2d_identity/translate/rotate` | **있음** — `HOperatorSet.HomMat2dIdentity/Translate/Rotate` (`DatumFindingService` L191-193) | 회전행렬 빌드 = 그대로 재사용. |
| `vector_angle_to_rigid` | **없음 (0건)** | 미사용. hom_mat2d_rotate(angle, centerRow, centerCol) 로 충분. |

**결론:** 각도 산출은 100% 기존 자산 재사용. 이미지 회전 적용(`affine_trans_image`)만 신규 1개. `RotateImage(90/180/270)` 는 임의각 불가하므로 레벨링에 부적합.

---

## File Classification

| 수정/신규 File | Role | Data Flow | Closest Analog | Match Quality |
|----------------|------|-----------|----------------|---------------|
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | service (algorithm) | transform | (self) `TryFindVerticalTwoHorizontal` L407 | exact — 각도 산출 메서드 추가 |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | service (algorithm) | transform / image-I/O | (self) `AffineTransformPoint` L1136 | role-match — 이미지 회전 유틸 신규 |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | action (state machine) | request-response | (self) `EStep.Grab`/`EStep.DatumPhase` | exact — 회전 전처리 step 삽입 |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model (param) | config (INI) | (self) `MeasurementBase.InvertSign` 패턴 | exact — per-datum bool 플래그 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | service (recipe I/O) | file-I/O (INI) | (self) FIXTURE `DisplayName`/`DatumCount` 직렬화 L94-95 | exact — 시퀀스 토글 키 추가 |

---

## Pattern Assignments

### `DatumFindingService.cs` (service, transform) — 레벨링 각도 산출

**Analog:** (self) `TryFindVerticalTwoHorizontal` (L407-579) — 수평 2-ROI concat 피팅 전 구간.

**재사용 핵심 (D-01 의 "기존 수평 피팅 재사용·중복 구현 금지" 직접 충족):**

수평 2-ROI 에지점 추출 → concat → 라인 피팅 (L455-495):
```csharp
// Horizontal A/B → TryExtractEdgePoints (ROI별 raw 에지점 추출)
HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
HTuple hrB, hcB, hrE, hcE, nr, nc, df;
HOperatorSet.FitLineContourXld(
    contour, "tukey", -1, 0, 5, 2,
    out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
```

**각도 산출 패턴 (L516) — angle_lx 등가, 이것이 레벨링 각도의 정답 소스:**
```csharp
// 수평선 대비 라인 각도 (radian). HDevelop angle_lx 와 동일 의미.
double curAngle = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
```
- planner 노트: 수평선(0°) 대비 기울기 = `curAngle`. 레벨링 회전각 = `-curAngle`(또는 부호규약 확정). **angle_lx 신규 호출 불필요.**
- 신규 메서드 권장 시그니처: `public bool TryGetLevelingAngle(HImage image, DatumConfig config, out double angleRad, out string error)` — 본문은 `TryFindVerticalTwoHorizontal` 의 수평 피팅 구간만 떼어내 `curAngle` 반환. (intersection/transform 빌드 L497~ 는 레벨링엔 불필요.)

**hom_mat2d 회전행렬 빌드 패턴 (L191-193, 회전중심 명시):**
```csharp
HOperatorSet.HomMat2dIdentity(out mat);
HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);
// HomMat2dRotate(mat, angle, centerRow, centerCol, out) — 회전중심 지정 가능
```

**에러 핸들링 패턴 (전 메서드 공통, L769-778):**
```csharp
catch (Exception ex) {
    error = ex.Message;
    HOperatorSet.HomMat2dIdentity(out transform); // 실패 시 identity 폴백
    return false;
}
finally {
    if (contour != null) { try { contour.Dispose(); } catch { } } // XLD dispose
}
```

---

### `VisionAlgorithmService.cs` (service) — 이미지 회전 적용 (신규 유틸)

**Analog:** (self) `AffineTransformPoint` (L1136-1151) — static 유틸 + identity 가드 + try/catch swallow 패턴.

**기존 점-변환 유틸 (구조 템플릿으로 복제):**
```csharp
public static void AffineTransformPoint(
    HTuple homMat2D, double row, double col,
    out double transRow, out double transCol)
{
    transRow = row; transCol = col;
    if (homMat2D == null || homMat2D.Length == 0) return; // 무보정 폴백
    try {
        HTuple tr, tc;
        HOperatorSet.AffineTransPoint2d(homMat2D, row, col, out tr, out tc);
        transRow = tr.D; transCol = tc.D;
    }
    catch { } // swallow → 원본 좌표 유지
}
```

**신규 이미지 회전 유틸 (planner 작성 대상, 위 구조 따름):**
- 시그니처 권장: `public static HImage RotateImageByAngle(HImage src, double angleRad, ...)` 또는 `bool TryRotate(...)`.
- 내부 = `gen_image1` 크기 조회 → `hom_mat2d_identity` → `hom_mat2d_rotate(angle, centerRow, centerCol)` → `affine_trans_image(src, out rotated, mat, interpolation, adaptImageSize)`.
- **angle_lx/rotate_image 선례 없음** — `affine_trans_image` 채택. `HImage.RotateImage(deg,"constant")` 는 90 배수 전용이라 레벨링 부적합 (HikCamera.cs L460 참조).
- 경계처리(재량 D): `affine_trans_image` 의 `adapt_image_size='true'` 로 잘림 방지 vs 'false' 고정크기 — measure ROI taught 좌표 정합 영향. planner 확정.

**카메라 드라이버 회전 선례 (참고 — 임의각 불가 케이스 확인용):** `HikCamera.cs` L458-470 / `BaslerCamera.cs` L562-574 / `MilCamera.cs` L187-199 — 모두 90/180/270 만. 레벨링에 직접 차용 불가, "왜 affine 인가" 근거.

---

### `Action_FAIMeasurement.cs` (action, request-response) — 회전 전처리 삽입 지점

**Analog:** (self) `EStep` 상태머신 (L30-37) + `EStep.Grab` (L149-181) + `EStep.DatumPhase` (L79-147).

**현 step 순서 (L62-394):** `Init → MoveZ → DatumPhase → Grab → Measure → End`

**삽입 위치 결정 (D-02/D-03):**
- D-02 "grab 직후 전처리 회전, 회전 이미지로 Datum+측정 전체 진행" → 레벨링 각도 산출은 **현 DatumPhase 보다 먼저** 또는 Grab 직후 별도 패스 필요. 회전 이미지가 DatumPhase 검출 + Measure 둘 다의 입력이어야 함.
- D-03 "시퀀스당 1회 산출, 전 SHOT 공유" → 레벨링 각도는 시퀀스 멤버(InspectionSequence)에 캐시, SHOT 마다 재산출 금지.
- planner 결정: `EStep.Level`(신규) 을 DatumPhase 앞에 삽입하거나, 기준 Datum 1차검출→각도→회전을 시퀀스 단위 1회 수행.

**Grab step 의 이미지 로드/회전 삽입 패턴 (L160-178) — 회전을 이 직후에:**
```csharp
if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
    try { image = new HImage(ShotParam.SimulImagePath); }
    catch (Exception ex) { image = null; Logging.PrintLog(...); }
}
// ... 회전 전처리 삽입 후보 지점 (image != null 직후) ...
if (image != null) {
    ShotParam.SetImage(image);
    if (pMyContext.ResultHalconImage != null) pMyContext.ResultHalconImage.Dispose();
    pMyContext.ResultHalconImage = image.CopyImage();
    image.Dispose();
}
```
- **주의 (CLAUDE.md 제약):** 회전된 HImage 는 원본 dispose 후 교체 (HikCamera.cs L461 `sourceImage.Dispose()` 패턴 동일). `using`/`try/finally` 로 누수 차단.

**시퀀스/transform 캐시 접근 패턴 (L80-85, 224):**
```csharp
InspectionSequence parentSeq = ShotParam.Parent as InspectionSequence;
// parentSeq 가 DatumConfigs + _datumTransforms 소유 → 레벨링 각도/회전상태도 여기 캐시 적합
```

**lenient 에러 패턴 (DatumPhase L92-100):** 레벨링 실패 시 abort 금지 — log + 무회전(identity) 폴백으로 측정 진행. 기준 Datum 검출 실패해도 SHOT skip 아닌 무보정 폴백 권장(D-04 기본 off 정신과 일관).

---

### `DatumConfig.cs` (model, config-INI) — per-datum 기준 플래그 (옵션 A)

**Analog:** (self) `DatumName`/`TeachingImagePath` (L17-44) + `MeasurementBase.InvertSign` (별도 파일 L52).

**bool 플래그 영속 선례 (정확한 복제 대상 — D-04 기본 off + 회귀 0):**
```csharp
// MeasurementBase.cs L48-52 (선례 InvertSign)
[Category("Measurement|Tolerance")]
[System.ComponentModel.Description("부호 반전. 켜면 측정값 부호를 뒤집어...")]
public bool InvertSign { get; set; } = false;  // ← = false 기본값 = 회귀 0, INI 미존재 시 폴백 false
```

**적용 (기준 Datum 지정을 per-datum 플래그로 둘 경우):**
```csharp
// DatumConfig 에 추가 — ParamBase reflection 이 Boolean case 자동 직렬화 (신규 Save/Load 코드 0)
[Category("Datum|Leveling")]
[System.ComponentModel.Description("레벨링 기준. 켜면 이 Datum 의 수평 에지로 이미지 회전 정렬한다. 시퀀스당 1개만 지정.")]
public bool IsLevelingReference { get; set; } = false;
```
- INI 직렬화는 `DatumConfig : ParamBase` 의 reflection 경로가 자동 처리 (DatumConfig.cs L10-11 주석 명시: "double/int/string/bool 자동 직렬화").
- string 경로(`TeachingImagePath` L36-38)도 동일 — 기준을 이름 참조로 둘 수도 있으나 per-datum bool 이 더 단순.

**대안(옵션 B):** 시퀀스 단위 토글(D-04) + 기준 datum **이름** 을 InspectionRecipeManager FIXTURE 섹션에 둠 (아래 참조). CONTEXT D-01 "지정 플래그는 시퀀스 레벨링 토글과 같은 자리" → **옵션 B 가 CONTEXT 의도에 더 부합** (토글+기준이 같은 자리). planner 가 A/B 확정.

---

### `InspectionRecipeManager.cs` (service, file-I/O INI) — 시퀀스 레벨링 토글 + 기준 datum (옵션 B)

**Analog:** (self) `SaveFixtureForSequence` (L85-100) / `LoadFixtureForSequence` (L124-142) — FIXTURE 섹션 수동 키 직렬화.

**시퀀스 단위 키 직렬화 선례 (DisplayName — 정확한 복제 대상):**
```csharp
// SAVE (L92-95)
string displayName = seq.GetDisplayName();
if (displayName == null) displayName = "";
saveFile[sectionPrefix]["DisplayName"] = displayName;
saveFile[sectionPrefix]["DatumCount"] = seq.DatumConfigs.Count;

// LOAD (L131-135)
string displayName = loadFile[sectionPrefix]["DisplayName"].ToString();
if (displayName == null) displayName = "";
seq.DisplayName = displayName;
int datumCount = loadFile[sectionPrefix]["DatumCount"].ToInt();
```

**적용 (D-04 시퀀스 토글 + D-01 기준 datum 이름 — 같은 FIXTURE 섹션):**
```csharp
// SAVE 추가
saveFile[sectionPrefix]["LevelingEnabled"] = seq.LevelingEnabled ? 1 : 0; // 기본 off
saveFile[sectionPrefix]["LevelingDatumName"] = seq.LevelingDatumName ?? "";

// LOAD 추가 — 키 미존재 시 .ToBool()/.ToString() 폴백으로 off/"" (회귀 0)
seq.LevelingEnabled = loadFile[sectionPrefix]["LevelingEnabled"].ToBool();
seq.LevelingDatumName = loadFile[sectionPrefix]["LevelingDatumName"].ToString() ?? "";
```
- **회귀 0 핵심:** 기존 INI 에 키 미존재 → `IniFile[...]["..."].ToBool()` = false (DatumCount L113 `.ToInt()` 미존재→0 폴백과 동일 메커니즘). D-04 "INI 미존재 시 폴백 off" 직접 충족.
- **비활성 시퀀스 보존 (L105-121 PreserveFixtureFromExisting):** CameraRole 전환 시 섹션 통째 복사 경로 — 신규 키도 자동 보존됨(섹션 단위 복사라 추가 코드 0). MEMORY `project_recipe_datum_loss_camerarole` 손실버그 회귀 주의 — 신규 키가 이 보존 경로 통과하는지 verify.
- `InspectionSequence` 에 `public bool LevelingEnabled { get; set; }` + `public string LevelingDatumName { get; set; } = ""` 멤버 추가 필요 (DisplayName L46 와 같은 자리).

---

## Shared Patterns

### HImage 수명 관리 (회전 시 필수)
**Source:** `HikCamera.cs` L458-473, CLAUDE.md "HImage objects must be disposed"
**Apply to:** Action_FAIMeasurement 회전 삽입, VisionAlgorithmService 회전 유틸
```csharp
HImage rotatedImage = sourceImage.RotateImage(90.0, "constant");
sourceImage.Dispose();  // 원본 즉시 해제 후 교체
// XLD contour 는 finally { try { contour.Dispose(); } catch { } } (DatumFindingService L777)
```

### bool/string 플래그 INI 영속 (기본 off → 회귀 0)
**Source:** `MeasurementBase.InvertSign`/`UseAbsoluteValue` (L46-52) — ParamBase reflection 자동 직렬화
**Apply to:** DatumConfig per-datum 플래그 (옵션 A). `= false` 기본값 + `[Category]` + `[Description]` 3종 세트.

### 수동 INI 키 직렬화 (FIXTURE 시퀀스 레벨)
**Source:** `InspectionRecipeManager.SaveFixtureForSequence`/`LoadFixtureForSequence` (L85-142)
**Apply to:** 시퀀스 토글 + 기준 datum 이름 (옵션 B). `.ToBool()`/`.ToInt()` 미존재 폴백 = 회귀 0.

### 알고리즘 실패 = identity/무보정 폴백 (abort 금지)
**Source:** `DatumFindingService` catch L769-778 (identity transform), `Action_FAIMeasurement.DatumPhase` lenient L92-145
**Apply to:** 레벨링 각도 산출 실패 / 기준 Datum 미검출 → 무회전으로 측정 진행 + log. throw 금지(CLAUDE.md "Never throw from Run()").

### 한글 변경 주석 규약 (MEMORY 필수)
**Apply to:** 모든 신규/수정 라인 — `//YYMMDD hbk <설명>` (예: `//260617 hbk Phase 52 레벨링`).

---

## No Analog Found

| Item | Role | Reason |
|------|------|--------|
| `affine_trans_image`(HImage 임의각 회전) | image transform | 코드베이스 0건. `HImage.RotateImage` 는 90 배수 전용. planner 가 HALCON `affine_trans_image` + `hom_mat2d_rotate` 로 신규 작성. 점-변환 `AffineTransPoint2d`(VisionAlgorithmService L1146) 가 가장 가까운 친척이나 이미지가 아닌 점 대상. |
| `angle_lx` 직접 호출 | angle calc | 0건. 프로젝트 관용 = `Math.Atan2`. HDevelop 의 angle_lx 는 Math.Atan2 산출각으로 대체 가능(등가). |

---

## Metadata

**Analog search scope:** `WPF_Example/Halcon/Algorithms/`, `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Device/Camera/`, `WPF_Example/Sequence/Param/`
**Files scanned:** DatumFindingService.cs, VisionAlgorithmService.cs, Action_FAIMeasurement.cs, DatumConfig.cs, MeasurementBase.cs, InspectionSequence.cs, InspectionRecipeManager.cs, HikCamera/BaslerCamera/MilCamera.cs, ParamBase.cs
**API grep:** angle_lx(0) / rotate_image(카메라90배수만) / affine_trans_image(0) / hom_mat2d_*(다수 재사용) / vector_angle_to_rigid(0)
**Pattern extraction date:** 2026-06-17
