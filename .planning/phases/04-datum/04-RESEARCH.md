# Phase 4: Datum System — Research

**Date:** 2026-04-09
**Source:** 04-CONTEXT.md (확정)
**Method:** 코드베이스 직접 읽기 + 구조 분석

---

## 1. FAIConfig.cs — DatumConfig 필드 추가 대상

**파일:** `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs`
**상속:** `FAIConfig : ParamBase`

### 현재 구조
- ROI: `ROI_Row`, `ROI_Col`, `ROI_Phi`, `ROI_Length1`, `ROI_Length2` (Rectangle2 중심+반길이+각도)
- Edge 파라미터: `EdgeThreshold`, `Sigma`, `EdgeDirection`, `EdgeSelection`, `EdgeSampleCount`, `EdgeTrimCount`, `EdgePolarity`
- Calibration: `PixelResolutionX`, `PixelResolutionY`
- Tolerance: `NominalValue`, `UpperTolerance`, `LowerTolerance`
- Runtime (비저장): `MeasuredValue`, `IsPass`, `FAIName` (`[Browsable(false)]`)
- `PolygonPoints` — string 직렬화 패턴 (`"x1,y1;x2,y2;..."`) — **DatumConfig의 Line ROI string 직렬화 선례**

### DatumConfig 추가 관련 판단

**DatumConfig는 FAIConfig와 형제가 아닌 ShotConfig 레벨 속성이다** (D-01).

→ DatumConfig 필드는 `ShotConfig`에 추가해야 한다. FAIConfig 자체는 변경 불필요.

ShotConfig (`WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`):
- `ShotConfig : CameraSlaveParam : ParamBase`
- `List<FAIConfig> FAIList` 소유
- 이미지 버퍼 (`_image`, `_imageLock`)
- **추가 위치:** `public DatumConfig Datum { get; set; }` 프로퍼티를 ShotConfig에 추가

### INI 저장 구조 영향

`InspectionRecipeManager.cs`의 Save/Load:
```
[SHOT_0]
  ShotName, ZPosition, DelayMs, SimulImagePath, FAICount
[SHOT_0_CAM]
  (CameraSlaveParam 필드들)
[SHOT_0_FAI_0]
  (FAIConfig 필드들)
```

→ DatumConfig 저장 위치: `[SHOT_0_DATUM]` 섹션 신규 추가.
→ `InspectionRecipeManager.Save()`에 `shot.Datum?.Save(saveFile, shotSection + "_DATUM")` 추가.
→ `InspectionRecipeManager.Load()`에 해당 섹션 로드 추가. 섹션 없으면(기존 레시피) → Datum 미설정 상태 유지 (하위호환).

---

## 2. Action_FAIMeasurement.cs — FindDatum 호출 삽입 위치

**파일:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`

### 현재 실행 흐름 (EStep 기반)
```
Init → ClearAllResults
Grab → GrabHalconImage → SetImage → CopyImage to context
Measure → foreach(fai in FAIList) { TryMeasure(image, fai) }
End → FinishAction(Pass/Fail)
```

### FindDatum 삽입 지점

**`EStep.Measure` 블록의 `foreach` 루프 직전** (line 78~79 사이):

```csharp
case EStep.Measure:
    if (ShotParam != null) {
        var service = new FAIEdgeMeasurementService();
        bool allPass = true;
        var overlays = new List<EdgeInspectionOverlay>();
        using (var image = ShotParam.GetImage()) {
            if (image != null) {
                // ▼▼▼ 여기에 Datum 삽입 ▼▼▼
                // HTuple transform = null;
                // if (ShotParam.Datum != null && ShotParam.Datum.IsConfigured) {
                //     var datumService = new DatumFindingService();
                //     string datumError;
                //     if (!datumService.TryFindDatum(image, ShotParam.Datum, out transform, out datumError)) {
                //         // Datum 실패 → 모든 FAI NG 처리
                //         foreach (var fai in ShotParam.FAIList) fai.ClearResult();
                //         pMyContext.AllPass = false;
                //         Step = (int)EStep.End;
                //         break;
                //     }
                // }
                // ▲▲▲ Datum 삽입 끝 ▲▲▲

                foreach (var fai in ShotParam.FAIList) {
                    // TryMeasure에 transform 파라미터 추가
```

### EStep 변경 필요 없음
Datum 계산은 Measure 스텝 내부에서 루프 전 1회 실행이므로 별도 EStep 추가 불필요.

### Context 확장
`FAIMeasurementContext`에 Datum 관련 상태를 추가할 수 있음:
- `bool DatumFound` — Datum 검출 성공 여부
- `string DatumError` — 실패 시 에러 메시지

---

## 3. FAIEdgeMeasurementService.cs — transform 파라미터 추가

**파일:** `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs`

### 현재 시그니처
```csharp
public bool TryMeasure(HImage image, FAIConfig fai, out FAIEdgeMeasurementResult result)
```

### 변경 방향

**옵션 A: 오버로드 추가 (하위호환 유지)**
```csharp
// 기존 (Datum 없음 = 무보정)
public bool TryMeasure(HImage image, FAIConfig fai, out FAIEdgeMeasurementResult result)

// 신규 (Datum 변환 적용)
public bool TryMeasure(HImage image, FAIConfig fai, HTuple transform, out FAIEdgeMeasurementResult result)
```

기존 메서드는 `transform = null`로 신규 메서드를 호출하는 래퍼로 변환.

### ROI 변환 삽입 위치

`TryMeasure` 내부의 ROI 바운딩 박스 계산 (line 75~82) **직전**:
```csharp
// 현재: fai.ROI_Row, fai.ROI_Col, fai.ROI_Phi, fai.ROI_Length1, fai.ROI_Length2 직접 사용
// 변경: transform != null이면 ROI 중심점(Row, Col)을 affine_trans_point로 변환,
//        Phi에 회전 성분 더하기
```

**구체적 변환 로직:**
```csharp
double roiRow = fai.ROI_Row;
double roiCol = fai.ROI_Col;
double roiPhi = fai.ROI_Phi;

if (transform != null) {
    HTuple transRow, transCol;
    HOperatorSet.AffineTransPoint2d(transform, fai.ROI_Row, fai.ROI_Col, out transRow, out transCol);
    roiRow = transRow.D;
    roiCol = transCol.D;

    // 회전 성분 추출: transform[0] = cos(θ), transform[1] = -sin(θ) → θ = atan2(-[1], [0])
    double rotAngle = Math.Atan2(-transform[1].D, transform[0].D);
    roiPhi = fai.ROI_Phi + rotAngle;
}
```

→ 이후 `sinPhi`, `cosPhi` 계산은 변환된 `roiPhi` 사용.
→ `top`, `bottom`, `left`, `right` 계산도 변환된 `roiRow`, `roiCol` 사용.
→ `TryMeasureBoth`, `TryMeasureSingle`에는 이미 변환된 바운딩 박스가 전달되므로 내부 변경 불필요.

### measurePhi 보정 여부
`measurePhi`는 `EdgeDirection`에서 결정되는 스캔 방향이다. Datum 회전이 크지 않다면(제품 편차 수준) 보정 불필요. 단, 정밀도가 요구되면 `measurePhi += rotAngle` 적용 가능 — Claude's Discretion에 해당.

---

## 4. ParamBase.cs — 직렬화 패턴 확인

**파일:** `WPF_Example/Sequence/Param/ParamBase.cs`

### 지원 타입 (Save/Load 리플렉션)
| 타입 | Save | Load |
|------|------|------|
| `Int32` | `saveFile[group][name] = (int)value` | `.ToInt()` |
| `Double` | `saveFile[group][name] = (double)value` | `.ToDouble()` |
| `String` | `saveFile[group][name] = (string)value` | `.ToString()` |
| `Boolean` | `saveFile[group][name] = (bool)value` | `.ToBool()` |
| `Rect` | `saveFile[group][name] = (Rect)value` | `.ToRect()` |
| `Line` | `saveFile[group][name] = (Line)value` | `.ToLine()` |
| `Circle` | `saveFile[group][name] = (Circle)value` | `.ToCircle()` |
| `PropertyItem[]` | 개별 프로퍼티 순회 | 개별 프로퍼티 순회 |
| `ModelFinderViewModel` | `.Save(file, group, name)` | `.Load(file, group, name)` |

### DatumConfig 직렬화 전략

**DatumConfig는 ParamBase를 상속**하므로 `Save(IniFile, group)` / `Load(IniFile, group)` 자동 적용.

DatumConfig의 모든 필드를 ParamBase가 지원하는 타입으로 선언하면 별도 직렬화 코드 불필요:
- Line ROI 좌표: `double` 필드 10개 (`Line1_Row`, `Line1_Col`, `Line1_Phi`, `Line1_Length1`, `Line1_Length2`, `Line2_*` 동일)
- 기준값: `double` 필드 3개 (`RefOriginRow`, `RefOriginCol`, `RefAngleRad`)
- 검출 파라미터: `int EdgeThreshold`, `string EdgePolarity`
- 상태: `bool IsConfigured`

→ **복합 string 직렬화 불필요.** ParamBase 리플렉션이 각 double/int/bool/string을 자동 처리.

### `[Browsable(false)]` 활용
런타임 전용 필드 (`CurrentTransform`, `LastFindSucceeded`)는 `[Browsable(false)]`로 PropertyGrid에서 숨기되, ParamBase의 리플렉션 대상에서 **타입이 HTuple이므로 자동 무시**됨 (switch-case에 HTuple 없음).

→ HTuple 타입 필드는 Save/Load에서 스킵되므로 별도 `[NonSerialized]` 불필요.

### Line 구조체 (UI용)와의 구분
`ParamBase`의 `Line` 타입은 `ReringProject.UI.Line` (X1, Y1, X2, Y2 — 2점 직선). `[LineAttribute]` 마커 → DrawableLine으로 렌더링.

DatumConfig의 Line ROI는 Halcon Rectangle2 형식(center + half-lengths + phi)이므로 UI `Line` 구조체와 **호환되지 않음**. double 필드 5개로 직접 저장하는 것이 올바름.

---

## 5. RoiDefinition.cs — Line ROI 재사용 가능성

**파일:** `WPF_Example/Halcon/Models/RoiDefinition.cs`

### 현재 구조
- Rectangle1 바운딩 박스: `Row1`, `Column1`, `Row2`, `Column2`
- Edge 파라미터: `Sigma`, `EdgeThreshold`, `EdgeDirection`, `EdgeSelection`, `EdgeSampleCount`, `EdgeTrimCount`, `EdgePolarity`
- `PixelResolutionX`, `PixelResolutionY`
- `PolygonPoints` (string)
- `IsTaught` (bool)

### Datum Line ROI와의 호환성

**부분 재사용 가능, 그러나 직접 사용은 부적절.**

이유:
1. `RoiDefinition`은 Rectangle1(Row1/Col1/Row2/Col2 — 축정렬 바운딩 박스) 기반
2. Datum Line ROI는 Rectangle2(Center + Phi + HalfLengths) 기반 — 회전 포함
3. `RoiDefinition`에는 `Phi` 필드가 없음
4. FAIConfig의 `ToRoiDefinition()` 변환이 Phi→AABB 확장을 수행하는 것은 에지 측정에서 허용되지만, Datum에서는 **Phi 자체가 필수** (Halcon `gen_measure_rectangle2`에 직접 사용)

**결론:** DatumConfig는 RoiDefinition을 사용하지 않고, 자체 double 필드 5개(`Row`, `Col`, `Phi`, `Length1`, `Length2`)를 Line1/Line2 각각에 대해 소유한다.

### Edge 파라미터 재사용
Datum의 `find_line`에 필요한 파라미터(`Sigma`, `EdgeThreshold`, `EdgePolarity`)는 FAIConfig/RoiDefinition과 동일한 이름·타입. DatumConfig에 동일 필드를 선언하되 기본값은 다를 수 있음 (Datum은 기준 에지를 찾으므로 더 관대한 threshold 가능).

---

## 6. ShotConfig.cs — DatumConfig 소유 구조

**파일:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`

### 현재 구조
```
ShotConfig : CameraSlaveParam : ParamBase
├── ZPosition, DelayMs, SimulImagePath
├── List<FAIConfig> FAIList
├── ShotName
└── _image (thread-safe buffer)
```

### DatumConfig 추가 방안
```csharp
[Browsable(false)]
public DatumConfig Datum { get; set; }
```

- `[Browsable(false)]`: PropertyGrid에서 ShotConfig 선택 시 Datum 필드가 나타나지 않도록 (Datum은 트리의 별도 노드에서 편집)
- **ParamBase 리플렉션 주의:** DatumConfig 타입은 ParamBase.Save/Load의 switch-case에 없으므로 자동 직렬화 대상 아님 → `InspectionRecipeManager`에서 명시적 Save/Load 필요 (기존 FAIList 패턴과 동일)

---

## 7. ENodeType + 트리 구조 — Datum 노드 추가

**파일:** `WPF_Example/UI/ControlItem/Node.cs`

### 현재 ENodeType
```csharp
public enum ENodeType {
    Recipe, Sequence, Action, SubSequence, FAI
}
```

### 변경
```csharp
public enum ENodeType {
    Recipe, Sequence, Action, SubSequence, FAI, Datum  // Datum 추가
}
```

### Node.ImageSource 확장
Datum 노드용 아이콘 경로 추가 (기존 Resource 폴더에서 적절한 아이콘 선택 또는 신규).

### InspectionListViewModel.CreateSequenceNode 확장

현재 트리 구축 (line 72~78):
```csharp
if (act.Param is ShotConfig shot) {
    foreach (FAIConfig fai in shot.FAIList) {
        var faiNode = new CompositeNode { ... ENodeType.FAI ... };
        actNode.Children.Add(faiNode);
    }
}
```

Datum 노드 추가 (FAI 루프 전):
```csharp
if (act.Param is ShotConfig shot) {
    // Datum 노드 (1개, 항상 생성)
    if (shot.Datum != null) {
        var datumNode = new CompositeNode {
            Name = "Datum",
            NodeType = ENodeType.Datum,
            ParamData = shot.Datum,
            ...
        };
        actNode.Children.Add(datumNode);
    }
    // FAI 노드들
    foreach (FAIConfig fai in shot.FAIList) { ... }
}
```

---

## 8. Halcon API 사용 패턴 확인

### FAIEdgeMeasurementService에서 이미 사용 중인 패턴
- `HOperatorSet.GenMeasureRectangle2` — Rectangle2 형식 ROI 생성
- `HOperatorSet.MeasurePos` — 에지 검출
- `HOperatorSet.FitLineContourXld` — 라인 피팅

### DatumFindingService에 필요한 Halcon 연산자
| 연산자 | 용도 | 비고 |
|--------|------|------|
| `GenMeasureRectangle2` | Line ROI에서 에지 검출 | FAIEdgeMeasurementService와 동일 |
| `MeasurePos` | 에지 포인트 수집 | 동일 |
| `FitLineContourXld` | 에지 → 직선 피팅 | 동일 |
| `IntersectionLl` | 두 직선 교점 | **신규** — Datum 전용 |
| `HomMat2dIdentity` | 변환 행렬 초기화 | **신규** |
| `HomMat2dTranslate` | 평행이동 적용 | **신규** |
| `HomMat2dRotate` | 회전 적용 | **신규** |
| `AffineTransPoint2d` | ROI 좌표 변환 | **신규** |

→ `find_line` 로직은 `FAIEdgeMeasurementService.TryMeasureSingle`의 "First" 에지 검출 로직을 거의 그대로 재사용 가능. 단, select="first"로 고정하고 단일 라인 피팅만 수행.

---

## 9. 하위호환성 검증

### 기존 레시피 (Datum 미사용)
- `InspectionRecipeManager.Load()`에서 `[SHOT_0_DATUM]` 섹션 없음 → `shot.Datum` = null 또는 `IsConfigured = false`
- `Action_FAIMeasurement`에서 `Datum == null || !Datum.IsConfigured` → transform 생략 → 기존 Phase 3 흐름 그대로 실행
- **검증 완료:** 비파괴적 확장 가능

### TryMeasure 시그니처 변경
오버로드 방식이므로 기존 `TryMeasure(image, fai, out result)` 호출 코드는 영향 없음.

---

## 10. 리스크 및 주의사항

### R-01: ParamBase 리플렉션과 DatumConfig
DatumConfig가 ParamBase를 상속하면 ShotConfig.Save/Load 시 리플렉션이 DatumConfig 타입 프로퍼티를 만남 → switch-case에서 `default: break` → **자동으로 무시됨**. 안전하지만, InspectionRecipeManager에서 명시적 Save/Load를 잊으면 데이터 손실.

### R-02: HTuple 수명 관리
`CurrentTransform` (HTuple)은 Datum 계산 후 FAI 측정 루프 동안만 유효. `using` 블록 또는 명시적 Dispose 필요.

### R-03: Datum 노드 자동 생성 시점
ShotConfig 생성 시 Datum을 즉시 생성할지, 사용자가 명시적으로 추가할지 — Claude's Discretion 항목. InspectionRecipeManager.AddShot() 시 자동 생성이 간단.

### R-04: measurePhi 보정
Datum 회전이 제품 편차 수준(< 1~2도)이면 measurePhi 보정 없이도 충분. 대형 회전(> 5도)이면 보정 필요. 초기 구현은 보정 포함으로 안전하게.

---

## 요약: 파일별 변경 규모

| 파일 | 변경 유형 | 규모 |
|------|-----------|------|
| `DatumConfig.cs` | **신규 생성** | ~80줄 (ParamBase 상속, double 필드 13개 + bool 1개 + HTuple 2개) |
| `DatumFindingService.cs` | **신규 생성** | ~120줄 (TryFindDatum + find_line 재사용 로직) |
| `ShotConfig.cs` | 수정 | +3줄 (`DatumConfig Datum` 프로퍼티) |
| `InspectionRecipeManager.cs` | 수정 | +15줄 (Datum Save/Load 섹션) |
| `Action_FAIMeasurement.cs` | 수정 | +15줄 (FindDatum 호출 + 실패 분기) |
| `FAIEdgeMeasurementService.cs` | 수정 | +20줄 (오버로드 + ROI 변환 로직) |
| `Node.cs` | 수정 | +3줄 (ENodeType.Datum + ImageSource) |
| `InspectionListViewModel.cs` | 수정 | +8줄 (Datum 노드 생성) |
| `InspectionListView.xaml.cs` | 수정 | +10줄 (Datum 노드 선택 시 PropertyGrid 바인딩) |
| `HalconDisplayService.cs` | 수정 | +10줄 (Datum Line ROI 오버레이 렌더링) |

**총 신규: 2파일 (~200줄), 수정: 8파일 (~85줄)**

---

*Phase: 04-datum*
*Research completed: 2026-04-09*
