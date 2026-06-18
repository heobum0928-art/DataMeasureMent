# Phase 54: Datum 패턴매칭 위치보정 (ALIGN-01) - Pattern Map

**Mapped:** 2026-06-18
**Files analyzed:** 6 (1 new + 5 modified)
**Analogs found:** 5 / 6 (PatternMatchService = greenfield HALCON matching, convention-only)

> 모든 경로는 절대경로. 발췌 인용은 발췌 시점 라인 — 편집 후 라인 이동 가능. 규약: C# 7.2 / .NET 4.8 / `HOperatorSet.*` try-catch 래핑 / `//YYMMDD hbk` 주석.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` (NEW) | service (HALCON algo) | transform (image→pose→rigid) | `DatumFindingService.cs` (구조/규약) + `VisionAlgorithmService.cs` (try/catch+dispose) | convention-only (매칭 analog 없음) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (MOD) | model (ParamBase + ICustomTypeDescriptor) | CRUD (INI 직렬화) | 자기 자신 — `AlgorithmType` 드롭다운 + `IsHiddenForAlgorithm` + `EnsurePerRoiDefaults` 미러 | exact (self-analog) |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (MOD) | sequence (transform 캐시/합성) | event-driven (per-Datum) | 자기 자신 — `_levelingAngleRad` 캐시 lifecycle + `TryRunSingleDatum` | exact (self-analog) |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (MOD) | action (EStep state machine) | request-response (step loop) | 자기 자신 — `EStep.Level`/`DatumPhase` 단계 + lenient 게이트 | exact (self-analog) |
| `WPF_Example/Custom/Device/DeviceHandler.cs` (MOD) | config (상수) | — | 자기 자신 — `EXTENSION_MODEL`/`FILTER_MODEL` 상수 미러 | exact (self-analog) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (MOD) | service (HALCON algo) | transform (line-fit) | 자기 자신 — `TryGetLevelingAngle` 재사용 (정밀 θ 소스) | exact (self-analog) |

> 보조 재사용(직접 수정 아님): `WPF_Example/Utility/RecipeFileHelper.cs`(`GetModelFilePath`/`Copy`/`Delete`), `WPF_Example/UI/ContentItem/MainView.xaml.cs`(티칭 UX — P5 UI 작업 시 수정).

---

## Pattern Assignments

### `PatternMatchService.cs` (NEW — service, transform)

**Analog:** 매칭 analog **없음** (코드베이스에 `create_shape_model`/`find_ncc_model` 등 HALCON 매칭 호출 전무, Wafer scan 은 MIL `.mmf` 라 비참조). 따라서 **구조·규약만** 복사하고 매칭 본문은 D-01/§3 설계대로 greenfield.

**복사 대상 1 — 클래스 구조/주석 규약** (from `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:5-13`):
```csharp
namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// ...빌딩 블록.
    /// 모든 Halcon 호출은 try { ... } catch { return false; } 패턴 (프로젝트 컨벤션).
    /// </summary>
    public class VisionAlgorithmService   // → PatternMatchService 동일 namespace/패턴
```

**복사 대상 2 — `Try*` + out 결과 + try/catch(return false) + finally dispose** (from `VisionAlgorithmService.cs:18-60`, `MeasurementAlgorithm.cs:21-31`):
- 메서드: `bool TryXxx(... out HTuple transform, out string error)` — `Try` 접두 + `out` 결과 패턴(CLAUDE.md Naming).
- `HObject`/`HImage` 는 메서드 진입부에서 `null` 선언 → `try` 본문 → `finally { if (x != null) { try { x.Dispose(); } catch { } } }` (`VisionAlgorithmService.cs:40,657-660` 패턴).
- 단명 `HImage` 는 `using (var image = new HImage(...))` 도 허용 (`MeasurementAlgorithm.cs:23`).
- `catch (Exception ex) { error = ex.Message; ...; return false; }` (`DatumFindingService.cs:214-218`).

**복사 대상 3 — rigid transform 산출/출력 형식** (from `DatumFindingService.cs:185-193`):
```csharp
// hom_mat2d 변환 빌드 (평행이동 + 회전)
HTuple mat;
HOperatorSet.HomMat2dIdentity(out mat);
HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);
```
- ALIGN 산출 권고(분석 §3)는 `vector_angle_to_rigid(refRow,Col,Angle, curRow,Col,Angle)` → 동일하게 `out HTuple` 1행렬 반환. 수동 translate+rotate 합성은 위 패턴 재사용 가능(Claude's Discretion D-52).
- **부호/좌표계 주의**(분석 §5 중리스크): `find_shape_model` angle 은 반시계+rad, 기존 코드 각도는 `Math.Atan2(rEnd-rBegin, cEnd-cBegin)`(`DatumFindingService.cs:181-183, 648`). ref pose − cur pose 변위 방향 캘리브레이션 SIMUL 검증 필수.

**greenfield 매칭 본문 (analog 없음, D-01/D-06 설계대로 신규 작성):**
- Shape: `create_shape_model`/`find_shape_model`/`write_shape_model`/`read_shape_model`.
- NCC: `create_ncc_model`/`find_ncc_model`/`write_ncc_model`/`read_ncc_model`.
- 검색영역 제한: `reduce_domain`(template ROI ± `PatternSearchMarginPx`, D-06).
- 다운샘플 coarse: `zoom_image_factor` 또는 피라미드 상위 레벨(D-06a) → x,y 획득 후 스케일 복원.
- ROI 좌표변환(무 warp): `AffineTransPoint2d` 재사용(아래 InspectionSequence/VisionAlgorithmService 채널).

---

### `DatumConfig.cs` (MOD — model, CRUD)

**Analog:** 자기 자신. `PatternEngine`(Shape|NCC) 선택자 + ALIGN 필드를 기존 `AlgorithmType` 드롭다운/필터/sentinel 패턴에 **미러**.

**복사 대상 1 — string-backed enum 드롭다운(ItemsSourceProperty)** (`DatumConfig.cs:73-103`):
```csharp
[Category("Datum|Algorithm")]
[ItemsSourceProperty(nameof(AlgorithmTypeList))]
public string AlgorithmType { get; set; } = "TwoLineIntersect";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> AlgorithmTypeList { get { return new List<string> { ... }; } }
```
→ `PatternEngine`(string, 기본 `"Shape"`) + `PatternEngineList = { "Shape", "NCC" }`. ParamBase 가 enum 직렬화 미지원이므로 **반드시 string 저장**(주석 L70-72, L530 근거).

**복사 대상 2 — bool per-Datum 토글** (`DatumConfig.cs:40-43`, `IsLevelingReference` 미러):
```csharp
//260618 hbk Phase 54 ALIGN-01 ...
[Category("Datum|PatternAlign")]
public bool IsPatternAlignEnabled { get; set; } = false;   // D-11 기본 false → off 회귀 0
```

**복사 대상 3 — sentinel 0/"" + double/int/string 영속 필드** (`DatumConfig.cs:105-123`, `ExpectedAngleDeg`/`AngleTolerance` 미러):
- 신규(D-07/D-09): `RefMatchRow`/`RefMatchCol`/`RefMatchAngleDeg`(double), `PatternMinScore`(double), `PatternAngleExtentDeg`(double, =0 → angle off 토글), `PatternSearchMarginPx`(double/int). **PatternModelPath 저장 금지**(D-07 이름 기반 재계산).
- `PatternRoi_*`(Row/Col/Phi/Length1/Length2) — `Line1_*` ROI 필드(`DatumConfig.cs:132-145`) 패턴 복사, `_PhiDeg` wrapper(L140-144) 포함.

**복사 대상 4 — ICustomTypeDescriptor 동적 hide** (`DatumConfig.cs:721-753` `IsHiddenForAlgorithm`):
```csharp
case EDatumAlgorithm.TwoLineIntersect:
    if (name == "TeachingImagePath_Vertical") return true;
    if (name.StartsWith("Circle_") ...) return true;
    ...
```
→ `PatternEngine=="NCC"` 일 때 Shape 전용 파라미터 hide(또는 그 반대). `BuildFilteredProperties`(L690-705) + `sourceNames` HashSet 에 `PatternEngineList` 추가. **주의(L13, L680-683):** ICustomTypeDescriptor 는 PropertyGrid hide 만; INI 직렬화는 Reflection 경로라 무관 — hide 해도 값은 보존(하위호환).

**복사 대상 5 — EnsurePerRoiDefaults 멱등 폴백** (`DatumConfig.cs:574-677`, 특히 L617-618 null 가드):
```csharp
if (TeachingImagePath == null) TeachingImagePath = ""; // INI 키 미존재 시 null 가드
```
→ D-11: `PatternMinScore`/`PatternAngleExtentDeg`/`PatternSearchMarginPx` 가 sentinel 0 이면 기본값 복원, `IsPatternAlignEnabled` 키 미존재 → false. **멱등성**(이미 의미값이면 미변경) 필수.

---

### `InspectionSequence.cs` (MOD — sequence, event-driven per-Datum)

**Analog:** 자기 자신. align rigid 를 `_datumTransforms[DatumName]` 에 **합성**하고 캐시 lifecycle 을 레벨링과 동일하게 관리.

**복사 대상 1 — transform 누적 저장 채널** (`InspectionSequence.cs:56, 375-406`):
```csharp
private readonly Dictionary<string, HTuple> _datumTransforms = ...;   // DatumName 키

public bool TryRunSingleDatum(DatumConfig datum, HImage imageH, HImage imageV, out string error) {
    ...
    _datumTransforms[datumKey] = transform; // 누적 저장
}
public bool TryGetDatumTransform(string datumRef, out HTuple transform) {
    if (string.IsNullOrEmpty(datumRef)) { HOperatorSet.HomMat2dIdentity(out transform); return true; }
    return _datumTransforms.TryGetValue(datumRef, out transform);
}
```
→ ALIGN: `TryRunSingleDatum` 내부(또는 신규 align 단계)에서 검출 transform 과 align rigid 를 `HomMat2dCompose` 로 합성 후 `_datumTransforms[datumKey]` 저장. **측정 라우팅 무수정**(meas.DatumRef → 자동 적용).

**복사 대상 2 — 캐시 lifecycle Set/Reset (레벨링 미러)** (`InspectionSequence.cs:61-67`):
```csharp
private double _levelingAngleRad = 0.0;
private bool _levelingComputed = false;
public void SetLevelingAngle(double angleRad) { _levelingAngleRad = angleRad; _levelingComputed = true; }
public void ResetLeveling() { _levelingAngleRad = 0.0; _levelingComputed = false; }
```
→ align 필요 캐시(예: per-Datum `_alignComputed`)도 동일 패턴. **`ClearDatumTransforms`(L350-359) 에 align 리셋 추가** — `_datumTransforms.Clear()` + `_failedDatums.Clear()` + `ResetLeveling()` 과 동일 lifecycle.

**복사 대상 3 — lenient 실패 신호** (`InspectionSequence.cs:361-373`):
```csharp
public void MarkDatumFailed(string datumName) { if (!string.IsNullOrEmpty(datumName)) _failedDatums.Add(datumName); }
public bool IsDatumFailed(string datumRef) { return !string.IsNullOrEmpty(datumRef) && _failedDatums.Contains(datumRef); }
```
→ D-10: 매칭 score<MinScore / 모델 로드 실패 시 `MarkDatumFailed(datum.DatumName)` 그대로 재사용(abort 없음).

**복사 대상 4 — line-fit 정밀 θ 소스 호출** (`InspectionSequence.cs:411-447` `TryComputeLevelingAngle`):
- D-02 ②단계: x,y 로 line-fit ROI 이동 후 `new DatumFindingService().TryGetLevelingAngle(refImage, refDatum, out computed, out err)` 호출 패턴(L435-444) 재사용. lenient 폴백(실패 시 false + 무회전, abort 금지) 동일.

---

### `Action_FAIMeasurement.cs` (MOD — action, state machine)

**Analog:** 자기 자신. `EStep.Level`/`DatumPhase` 단계에 매칭 삽입(D-04/D-05) + lenient 게이트(D-10).

**복사 대상 1 — EStep 단계 흐름** (`Action_FAIMeasurement.cs:30, 80-207`):
```
EStep { Init, MoveZ, Level, DatumPhase, Grab, Measure, ..., End }
case EStep.Level:      ... Step = (int)EStep.DatumPhase; break;
case EStep.DatumPhase: ... Step = (int)EStep.Grab; break;
```
→ D-04: 매칭은 `DatumPhase` per-datum 루프(L131-202) 안에서 `TryRunSingleDatum` **이전**에 삽입(원본 이미지 매칭 → x,y → line-fit θ → 합성). 새 단계 신설보다 기존 `DatumPhase` 확장 권고(분석 §3).

**복사 대상 2 — per-datum 루프 + 이미지 취득 + lenient skip** (`Action_FAIMeasurement.cs:131-202`):
```csharp
foreach (var datum in parentSeq.DatumConfigs) {
    if (datum == null) continue;
    HImage img = GrabOrLoadDatumImage(datum);   // D-05 보정 전 원본 grab 이미지
    if (img == null) { ... datum.RuntimeDetectFailed = true; parentSeq.MarkDatumFailed(datum.DatumName); continue; }
    try { ... if (!parentSeq.TryRunSingleDatum(...)) { ... parentSeq.MarkDatumFailed(datum.DatumName); } }
    finally { img.Dispose(); }
}
```
→ ALIGN: `IsPatternAlignEnabled` 가드 추가(false → 매칭 skip → identity 유지, D-11). 매칭은 이 `img`(보정 전 원본) 입력. **레벨링 `RotateImageByAngle`(L151/153/184/239) 호출은 폐기**(D-05/§8) — warp 0회.

**복사 대상 3 — 측정 게이트 + ALIGN_FAIL 클리어** (`Action_FAIMeasurement.cs:280-293`):
```csharp
if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef)) {
    meas.ClearResult();
    meas.LastSkipReason = "DATUM_FAIL";   // → ALIGN 은 "ALIGN_FAIL" (D-10)
    meas.LastJudgement = false;
    ...
    continue;
}
```
→ D-10: 매칭 실패 datum 의 측정은 `LastSkipReason = "ALIGN_FAIL"` + `LastJudgement=false`. 값 클리어+NG+사유 패턴 그대로(가짜 숫자 금지).

**복사 대상 4 — transform 적용(무수정 채널)** (`Action_FAIMeasurement.cs:294-302, 347/362`):
```csharp
HTuple transform;
if (parentSeq2 == null || !parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)) {
    try { HOperatorSet.HomMat2dIdentity(out transform); } catch { transform = new HTuple(); }
}
ok = meas.TryExecute(image, transform, pixRes, out resultValue, out measError, out measOverlays);
```
→ **이 블록 무수정** — align 합성이 `_datumTransforms` 에 반영되면 측정 자동 추종(D-03).

---

### `DeviceHandler.cs` (MOD — config 상수)

**Analog:** 자기 자신 (`WPF_Example/Custom/Device/DeviceHandler.cs:77-85`):
```csharp
public const string EXTENSION_IMAGE = ".tiff";
public const string FILTER_MODEL = "mmf Files(*.mmf)|*.mmf";
public const string EXTENSION_MODEL = ".mmf";   // MIL — 미재사용
public const string EXTENSION_CALIBRATION = ".cal";
```
→ D-07b 신규 상수 추가 (기존 `.mmf` 미재사용):
```csharp
//260618 hbk Phase 54 ALIGN-01 HALCON shape/ncc 모델 확장자 (D-07b)
public const string EXTENSION_SHAPE_MODEL = ".shm";
public const string EXTENSION_NCC_MODEL = ".ncm";
```
- `RecipeFileHelper.GetModelFilePath`(아래)는 `+ DeviceHandler.EXTENSION_MODEL` 하드코딩이므로, ALIGN 은 engine 별 확장자를 받는 오버로드/변형 경로 필요(D-07). **절대경로 저장 안 함** — recipeName/seqName/datumName/engine 으로 매 로드·저장 시 재계산.

---

### `DatumFindingService.cs` (MOD — service, line-fit)

**Analog:** 자기 자신. `TryGetLevelingAngle`(`DatumFindingService.cs:586-661`)을 정밀 θ 소스로 재사용. ALIGN ②단계(D-02): x,y 만큼 이동한 line-fit ROI 위에서 호출.
- 핵심: `FitLineContourXld`(L643-645) → `angleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D)`(L648). Shape angle(거침)이 아닌 이 line-fit 각도가 정밀 θ(분석 §8.2).
- **이 메서드 자체는 무수정 가능** — 호출부(InspectionSequence/Action)에서 이동된 ROI 좌표로 호출. 필요 시 ROI offset 인자 추가는 planner 재량.
- hom_mat2d 합성 시 `HomMat2dCompose`(코드베이스 미사용 신규 호출) — try/catch 래핑 필수.

---

## Shared Patterns

### HALCON 호출 try/catch 래핑 (전 신규 코드 적용)
**Source:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:214-218`, `VisionAlgorithmService.cs:46-59`
```csharp
try { /* HOperatorSet.* */ }
catch (Exception ex) { error = ex.Message; HOperatorSet.HomMat2dIdentity(out transform); return false; }
finally { if (contour != null) { try { contour.Dispose(); } catch { } } }
```
**Apply to:** `PatternMatchService` 전 메서드 + `InspectionSequence`/`DatumFindingService` 신규 HALCON 호출. CLAUDE.md: "Wrap all `HOperatorSet.*` calls in try/catch". `HImage` 는 dispose 필수.

### ROI 좌표변환 (무 warp 측정 — D-03)
**Source:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:45-54`
```csharp
if (datumTransform != null && datumTransform.Length > 0) {
    HTuple tRow, tCol;
    HOperatorSet.AffineTransPoint2d(datumTransform, roiRow, roiCol, out tRow, out tCol);
    rRow = tRow.D; rCol = tCol.D;
    double rotAngle = Math.Atan2(-datumTransform[1].D, datumTransform[0].D);
    rPhi = roiPhi + rotAngle;
}
```
**Apply to:** align rigid 적용은 이 기존 채널이 자동 수행 — 측정 ROI 중심(Row,Col)+각도(Phi)만 transform, 이미지 warp 없음. 신규 코드는 이 채널에 합성만 주입.

### 모델 파일 이름 기반 경로 재계산 (D-07)
**Source:** `WPF_Example/Utility/RecipeFileHelper.cs:86-93, 104-111`
```csharp
public string GetModelFilePath(string recipeName, string seqName, string actName, string propertyName) {
    string saveFile = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, recipeName, seqName, actName);
    saveFile += propertyName + DeviceHandler.EXTENSION_MODEL;   // ← ALIGN: engine 별 .shm/.ncm 로 분기
    string savePath = Path.GetDirectoryName(saveFile);
    if (Directory.Exists(savePath) == false) Directory.CreateDirectory(savePath);
    return saveFile;
}
```
**Apply to:** `.shm`/`.ncm` 모델 경로 — `GetModelFilePath` 패턴 미러(propertyName = datumName, 확장자 = engine 별). 레시피 폴더 하위 저장(D-07a)이라 `Copy`/`CopyFilesRecursively`(L122-149)·`Delete`(L113-119) 가 모델 파일 자동 동반 → stale 0. **DatumConfig 에 경로 미저장**.

### 패턴 ROI 티칭 UX (D-08 — P5 UI)
**Source:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:40-41, 2210-2265, 2494-2567`
```csharp
private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration }
private void TeachDatumButton_Click(...) { _canvasMode = ECanvasMode.TeachDatum; halconViewer.IsTeachDatumMode = true; ... }
private void InvokeTryTeachDatum() { ... ok = svc.TryTeachDatum(img, _editingDatum, out error); ... }
```
**Apply to:** 패턴 ROI(Rect) 그리기 = `RectRoi`/`TeachDatum` 모드 + write-back 패턴 재사용. ref pose 기록(D-09) = 티칭 시 1회 find 돌려 `RefMatchRow/Col/AngleDeg` 저장(`TryTeachDatum` 가 detected 값 write-back 하는 패턴, `DatumFindingService.cs:196-210` 미러).

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `PatternMatchService.cs` (HALCON shape/ncc 매칭 **본문**) | service | transform | 코드베이스에 `create_shape_model`/`find_ncc_model`/`reduce_domain`/`zoom_image_factor`/`vector_angle_to_rigid` 호출 전무. Wafer scan 은 MIL `.mmf`(비참조). `ModelFinderViewModel`(EAlgorithmType.PatternMatch)은 UI 골격만·HALCON 미구현(참고만). → 구조·try/catch·dispose 규약만 위 analog 에서 복사, 매칭 본문은 D-01/D-06/§3 설계대로 greenfield 작성. RESEARCH.md(HALCON 1D/2D measuring + matching) 참조. |

---

## Metadata

**Analog search scope:**
- `WPF_Example/Halcon/Algorithms/` (DatumFindingService, VisionAlgorithmService, MeasurementAlgorithm)
- `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig, InspectionSequence, Action_FAIMeasurement)
- `WPF_Example/Custom/Device/DeviceHandler.cs`, `WPF_Example/Utility/RecipeFileHelper.cs`
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (티칭 UX)

**Files scanned:** 9
**Pattern extraction date:** 2026-06-18
