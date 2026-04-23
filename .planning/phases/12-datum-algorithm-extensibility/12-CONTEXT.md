# Phase 12: datum-algorithm-extensibility - Context

**Gathered:** 2026-04-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 11에서 완성된 Datum 티칭 UI(2단계 고정 흐름: Line1→Line2) 위에서, Datum 알고리즘을 **Strategy 패턴**으로 확장 가능하게 리팩터링한다. 핵심 동기: 사각형 자재의 좌측 상단 원(Circle) 센터를 기준점으로 삼는 **CircleAndLine 방식**이 추가로 요구됨 — 원 센터 X로 수직선을 정의하고, 수평 ROI 2개로 에지 라인을 뽑아 교점을 Datum 원점으로 삼는다. 이 방식을 추가하면서 향후 3번째·4번째 알고리즘도 파일 1개만 추가하면 되는 구조를 만드는 것이 이 Phase의 목표다.

**Phase 11과의 경계:**
- Phase 11은 EDatumAlgorithm 없이 고정 2단계(Line1→Line2) 흐름으로 TwoLineIntersect를 구현한다.
- Phase 12는 그 구현을 `TwoLineIntersectDatum` 클래스로 **추출**하고, `CircleAndLineDatum`을 추가하며, MainView 티칭 흐름을 알고리즘이 `GetROISteps()`로 정의하는 가변 단계 구조로 전환한다.

**구체적으로 Phase 12가 추가하는 것:**
1. **EDatumAlgorithm enum** — DatumConfig에 알고리즘 선택 필드 (INI 직렬화)
2. **DatumAlgorithmBase** — `GetROISteps() / TryTeach() / TryFind()` 추상 기반 클래스
3. **TwoLineIntersectDatum** — Phase 11 기존 로직 추출, 동작 변화 없음
4. **CircleAndLineDatum** — Halcon 원 검출(EdgesSubPix→FitCircleContourXld) + Line1/Line2 교점
5. **DatumFindingService 디스패처** — AlgorithmType으로 올바른 구현체 선택
6. **MainView 가변 단계 흐름** — `GetROISteps()` 배열을 순회하며 Rect/Circle 드로잉 자동 선택
7. **HalconViewerControl.StartCircleDrawing** — Phase 11 HalconViewerControl에 Circle 드래그 API 추가(Phase 11이 Rect만 구현했을 경우)

**범위 외:**
- TwoLineIntersect 알고리즘 로직 자체 변경 — Phase 11 구현 그대로 추출
- TemplateMatch 기반 Datum, 3점 Datum 등 3번째 이상 알고리즘 구현 — 구조만 준비
- CircleDiameterMeasurement 로직 변경 — Phase 11에서 완료
- Datum 런타임 TryFind 알고리즘별 분기 확장 — TryFind는 Phase 12에서 동일 dispatcher 구조 적용하되, CircleAndLine의 TryFind 구현은 최소화(티칭 후 저장된 RefOrigin/RefAngle 재사용)

</domain>

<decisions>
## Implementation Decisions

### 알고리즘 선택 모델 (D-01 ~ D-04)
- **D-01: EDatumAlgorithm enum** — DatumConfig에 `public EDatumAlgorithm AlgorithmType { get; set; } = EDatumAlgorithm.TwoLineIntersect;` 추가. `ParamBase` 직렬화가 enum을 자동 지원하므로 INI에 string으로 저장. 기존 INI에 AlgorithmType 미존재 시 default TwoLineIntersect로 폴백 → 하위 호환 보장.
- **D-02: DatumAlgorithmBase 추상 클래스** — 다음 계약 정의:
  ```csharp
  public abstract class DatumAlgorithmBase
  {
      public abstract List<EDatumROIStep> GetROISteps();
      public abstract bool TryTeach(HImage image, DatumConfig config, out string error);
      public abstract bool TryFind(HImage image, DatumConfig config, out string error);
  }
  ```
  `EDatumROIStep { Rect, Circle }` — 각 단계에서 어떤 드로잉 모드를 사용할지 UI가 판단.
- **D-03: 파일 구조** — `WPF_Example/Halcon/Algorithms/Datum/` 폴더 신설:
  ```
  Datum/
    DatumAlgorithmBase.cs
    EDatumAlgorithm.cs          (enum 정의)
    EDatumROIStep.cs            (enum 정의)
    TwoLineIntersectDatum.cs
    CircleAndLineDatum.cs
  ```
- **D-04: TwoLineIntersectDatum** — Phase 11 `DatumFindingService.TryTeachDatum` 내부 로직을 그대로 이 클래스의 `TryTeach`로 이동. `GetROISteps()`는 `[Rect, Rect]` 반환. 동작 변화 없음. TwoLineIntersect의 `TryFind`도 기존 `TryFindDatum` 로직 추출.

### CircleAndLineDatum 알고리즘 (D-05 ~ D-09)
- **D-05: CircleAndLine 티칭 단계** — `GetROISteps()`는 `[Circle, Rect, Rect]` 반환 (Circle ROI → Line1 ROI → Line2 ROI).
  - **Step 1 (Circle):** 사용자가 원 검색 영역을 사각형 또는 원형으로 드래그 → DatumConfig의 `CircleROI_*` 필드에 저장.
  - **Step 2 (Line1 Rect):** 수평 에지 라인 ROI 1 → `Line1_*` 저장.
  - **Step 3 (Line2 Rect):** 수평 에지 라인 ROI 2 → `Line2_*` 저장.
  - Step 3 MouseUp 즉시 `TryTeach` 자동 호출 (Phase 11 동일 패턴).
- **D-06: 원 검출 알고리즘** — `HOperatorSet.EdgesSubPix`로 서브픽셀 윤곽 추출 → `HOperatorSet.SelectShapeXld`로 원호 형상 필터링 → `HOperatorSet.FitCircleContourXld(method:"algebraic")` → 중심 (CircleCenter_Row, CircleCenter_Col) 획득. 실패(윤곽 없음/반지름 범위 이탈) 시 `out error` 반환 후 false.
- **D-07: DatumConfig CircleROI 필드** — DatumConfig에 추가:
  ```csharp
  // CircleAndLine 알고리즘 전용 (TwoLineIntersect에서는 무시됨)
  public double CircleROI_Row    { get; set; }  // 검색 영역 중심 Y
  public double CircleROI_Col    { get; set; }  // 검색 영역 중심 X
  public double CircleROI_Radius { get; set; }  // 검색 영역 반지름
  // 휘발성 — 검출 결과
  [IgnoreDataMember] public double CircleCenter_Row;
  [IgnoreDataMember] public double CircleCenter_Col;
  [IgnoreDataMember] public double CircleDetected_Radius;
  ```
  CircleROI는 사각형 ROI(Line1_Row/Col/Phi/Length1/Length2 방식) 또는 원형 반지름 방식 중 선택 — **원형 반지름 방식** 권장(Circle 검색에 직관적). `CircleROI_Radius` 양수 여부로 ROI 설정 완료 판단.
- **D-08: CircleAndLine 교점 계산** — 원 센터 X = `CircleCenter_Col`이 수직선을 정의. Line1/Line2 두 ROI에서 각각 수평 에지 라인을 피팅 → 두 라인을 평균하거나 별도 교점으로 사용:
  - **옵션 A (권장):** Line1 + Line2 수평 에지 → 두 직선의 Y 절편 평균 → 수평 참조선 Y 확정 → 수직선 X=CircleCenter_Col과 교점 = RefOrigin.
  - **옵션 B:** Line1만 사용하여 수평선 → 수직선과 교점. Line2는 평행 검증용.
  옵션 A 권장 (더 강건). RefAngleRad = atan2(Line1/Line2 평균 기울기) 로 Datum 각도 설정.
- **D-09: CircleAndLine 오버레이** — 성공 시 `HalconDisplayService.RenderDatumOverlay` 확장:
  - 검출된 원: `HOperatorSet.DispCircle` (노란색)
  - 수직 참조선 (X=CircleCenter_Col, 전체 화면 높이): `HOperatorSet.DispLine` (노란색)
  - Line1 검출 직선 (청록색) + Line2 검출 직선 (청록색)
  - RefOrigin 교점: 빨간색 20px 십자 (기존 패턴 유지)
  `datum.LastTeachSucceeded && datum.AlgorithmType == CircleAndLine` 분기로 처리.

### DatumFindingService 디스패처 (D-10 ~ D-11)
- **D-10: DatumFindingService 리팩터링** — 기존 `TryTeachDatum` / `TryFindDatum` public 시그니처 **유지**. 내부에서 `DatumAlgorithmBase` 구현체를 선택하여 위임:
  ```csharp
  public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
  {
      var algorithm = CreateAlgorithm(config.AlgorithmType);
      return algorithm.TryTeach(image, config, out error);
  }
  private static DatumAlgorithmBase CreateAlgorithm(EDatumAlgorithm type)
  {
      switch (type)
      {
          case EDatumAlgorithm.CircleAndLine: return new CircleAndLineDatum();
          default: return new TwoLineIntersectDatum();
      }
  }
  ```
  호출부(MainView.xaml.cs) 시그니처 변경 없음.
- **D-11: 새 알고리즘 추가 절차** — 향후 추가 시: (1) `EDatumAlgorithm` enum에 값 추가, (2) `DatumAlgorithmBase` 파생 클래스 파일 1개 생성, (3) `CreateAlgorithm` switch에 case 1개 추가. UI는 자동 적응 (GetROISteps() 기반).

### MainView 가변 단계 흐름 (D-12 ~ D-14)
- **D-12: EDatumTeachStep 제거 → GetROISteps() 기반 인덱스** — Phase 11의 `EDatumTeachStep { Line1, Line2, Done }`를 `int _datumTeachStepIndex` + `List<EDatumROIStep> _datumTeachSteps`로 대체. 알고리즘이 반환한 단계 배열을 순회:
  ```csharp
  _datumTeachSteps = _currentAlgorithm.GetROISteps();  // [Rect, Rect] 또는 [Circle, Rect, Rect]
  _datumTeachStepIndex = 0;
  // 각 MouseUp: _datumTeachStepIndex++ → 마지막이면 TryTeachDatum 호출
  ```
- **D-13: 단계별 드로잉 모드 선택** — `_datumTeachSteps[_datumTeachStepIndex]` 값에 따라:
  - `EDatumROIStep.Rect` → `HalconViewerControl.StartRectangleDrawing()`
  - `EDatumROIStep.Circle` → `HalconViewerControl.StartCircleDrawing()`
  `label_drawHint`에 단계 안내: "Step 1/3: 원 검색 영역을 드래그하세요" 형식.
- **D-14: HalconViewerControl.StartCircleDrawing** — Phase 11에서 미구현 시 Phase 12에서 추가. 시그니처:
  ```csharp
  public void StartCircleDrawing();  // MouseDown=중심, MouseMove=미리보기, MouseUp=확정
  public event EventHandler<CircleDrawingCompletedEventArgs> CircleDrawingCompleted;
  // CircleDrawingCompletedEventArgs: CenterRow, CenterCol, Radius (이미지 좌표)
  ```
  완료 이벤트에서 `DatumConfig.CircleROI_Row/Col/Radius` 기록 후 다음 단계로 진행.

### PropertyGrid 알고리즘 선택 UI (D-15)
- **D-15: EDatumAlgorithm PropertyGrid 표시** — `DatumConfig.AlgorithmType` 이 `PropertyTools` PropertyGrid에서 자동으로 enum 드롭다운으로 렌더링됨(기존 EdgeDirection/EdgePolarity 패턴과 동일). 별도 [ItemsSourceProperty] 어노테이션 불필요. AlgorithmType 변경 시 사용자는 티칭을 재시작해야 하므로, 변경 감지 시 `IsConfigured = false`로 리셋하는 `INotifyPropertyChanged` 핸들러 추가 권장 (Claude discretion).

### 하위 호환 (D-16)
- **D-16:** 기존 Phase 4/6/11 INI 레시피에 `AlgorithmType` 필드 미존재 → `ParamBase.Load()` 기본값 `TwoLineIntersect` 유지. `CircleROI_*` 필드 미존재 → 0.0 기본값. CircleAndLine 알고리즘 사용 시 `CircleROI_Radius <= 0`이면 "Circle ROI를 먼저 설정하세요" 오류 반환.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 12 주요 수정 대상 (신규 파일)
- `WPF_Example/Halcon/Algorithms/Datum/DatumAlgorithmBase.cs` — 신규. abstract GetROISteps/TryTeach/TryFind 정의.
- `WPF_Example/Halcon/Algorithms/Datum/EDatumAlgorithm.cs` — 신규. enum { TwoLineIntersect, CircleAndLine }.
- `WPF_Example/Halcon/Algorithms/Datum/EDatumROIStep.cs` — 신규. enum { Rect, Circle }.
- `WPF_Example/Halcon/Algorithms/Datum/TwoLineIntersectDatum.cs` — 신규. Phase 11 DatumFindingService 로직 추출.
- `WPF_Example/Halcon/Algorithms/Datum/CircleAndLineDatum.cs` — 신규. EdgesSubPix→FitCircleContourXld + line intersection.

### Phase 12 주요 수정 대상 (기존 파일)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — AlgorithmType enum 필드 + CircleROI_Row/Col/Radius + CircleCenter_Row/Col/Radius 휘발성 필드 추가.
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — TryTeachDatum/TryFindDatum → CreateAlgorithm 디스패처 패턴으로 리팩터링. public 시그니처 유지.
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — RenderDatumOverlay에 CircleAndLine 오버레이 분기 추가 (검출 원 + 수직선 + 교점).
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — EDatumTeachStep 제거 → `_datumTeachStepIndex + _datumTeachSteps` 가변 구조. CircleDrawingCompleted 이벤트 핸들러 추가.
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — StartCircleDrawing / CommitActiveCircle / CircleDrawingCompleted API 추가 (Phase 11 미구현 시).

### 재사용 (수정 없음 예상)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — Phase 11에서 Datum 노드 Grab 활성화 완료. 변경 없음.
- `WPF_Example/UI/ContentItem/MainView.xaml` — btn_teachDatum 이미 Phase 11에서 추가됨. AlgorithmType은 PropertyGrid 자동 표시. 변경 없음 예상.
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 런타임 경로 변경 없음.

### Upstream context
- `.planning/phases/11-datum-teaching-ui-roi/11-CONTEXT.md` — Phase 11 Datum 티칭 UI 결정 (D-01..D-26). Phase 12는 이 위에서 알고리즘 추상화를 추가.
- `.planning/phases/04-datum/04-CONTEXT.md` — Datum 기반 설계 원칙.
- `.planning/phases/06-rapid-city/06-CONTEXT.md` — Multi-Datum 구조.
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 현재 TryTeachDatum 구현 (Phase 12에서 추출 대상).
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Phase 11 이후 상태 확인 필수.

</canonical_refs>

<code_context>
## Existing Code Insights

### Phase 11 이후 예상 상태 (Phase 12 시작 시점)
- `DatumFindingService.TryTeachDatum` — Phase 11에서 2단계(Line1→Line2) 로직으로 구현 완료. config.Line1Detected_*/Line2Detected_*/LastTeachSucceeded 기록.
- `MainView.xaml.cs` — `EDatumTeachStep { Line1, Line2, Done }` + `ECanvasMode.TeachDatum` 구현 완료.
- `DatumConfig` — SourceShotName + Line*Detected_* + LastTeachSucceeded 필드 완비.
- `HalconViewerControl` — StartRectangleDrawing 완비. StartCircleDrawing은 Phase 11 범위 외일 가능성 있음(Phase 12에서 추가).

### 기존 Strategy 패턴 참고
프로젝트 내 `MeasurementBase` 파생 클래스 패턴(6종: EdgePairDistance, CircleDiameter 등)이 동일 Strategy 구조. 이 패턴을 그대로 따름:
```csharp
// MeasurementBase 패턴 (Halcon/Algorithms/Measurements/)
public abstract class MeasurementBase
{
    public abstract bool TryExecute(HImage image, FAIConfig fai, DatumConfig datum,
        HomMat2D transform, double pixelResolution,
        out List<EdgeInspectionOverlay> overlays, out string resultSummary);
}
```
`DatumAlgorithmBase`는 이 패턴의 Datum 버전.

### EdgesSubPix + FitCircleContourXld 핵심 패턴
```csharp
// CircleAndLineDatum.TryTeach 내부
HOperatorSet.EdgesSubPix(roiImage, out HObject contours, "canny", sigma, low, high);
HOperatorSet.SelectShapeXld(contours, out HObject arcContours,
    "circularity", "and", 0.7, 1.0);  // 원호 형상만 선택
HOperatorSet.FitCircleContourXld(arcContours, "algebraic",
    -1, 0, 0, 3, 2,
    out HTuple rowC, out HTuple colC, out HTuple radius,
    out HTuple startPhi, out HTuple endPhi, out HTuple pointOrder);
// rowC[0], colC[0] = 원 센터
```
실패 조건: `rowC.Length == 0` (윤곽 없음) 또는 `Math.Abs(radius[0].D - expectedRadius) > tolerance` (반지름 범위 이탈).

### ParamBase 직렬화 제약 (유지)
- EDatumAlgorithm enum → INI에 string 자동 저장 (ParamBase.Save/Load가 enum.ToString/Enum.Parse 지원).
- double 필드 3개(CircleROI_Row/Col/Radius) 추가 → INI 자동 반영.
- 기존 INI에 미존재 시 기본값(0.0, TwoLineIntersect) 유지 → 하위 호환.

### C# 7.2 / .NET 4.8 제약
- nullable reference types, switch expressions, record 불가.
- switch(AlgorithmType) { case TwoLineIntersect: ... default: } 패턴 사용.
- List<EDatumROIStep> 반환 — .NET 4.8 지원.

### 빌드/검증
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
- 기존 TwoLineIntersect 동작 회귀 없음 확인 (Phase 11 SIMUL_MODE 시나리오 재현).
- CircleAndLine SIMUL_MODE 검증: 테스트 이미지에 명확한 원 + 수평 에지 존재해야 함.

</code_context>

<specifics>
## Specific Ideas

### EDatumAlgorithm enum 초안
```csharp
public enum EDatumAlgorithm
{
    TwoLineIntersect,   // Phase 11 기존 방식: Line1+Line2 교점
    CircleAndLine,      // Phase 12 신규: 원 센터 X + 수평선 교점
    // 향후 확장 가능
}
```

### EDatumROIStep enum 초안
```csharp
public enum EDatumROIStep
{
    Rect,    // 사각형 드래그 (StartRectangleDrawing)
    Circle,  // 원 드래그 (StartCircleDrawing)
}
```

### DatumAlgorithmBase 초안
```csharp
public abstract class DatumAlgorithmBase
{
    public abstract List<EDatumROIStep> GetROISteps();
    public abstract bool TryTeach(HImage image, DatumConfig config, out string error);
    public abstract bool TryFind(HImage image, DatumConfig config, out string error);
}
```

### TwoLineIntersectDatum 초안
```csharp
public class TwoLineIntersectDatum : DatumAlgorithmBase
{
    public override List<EDatumROIStep> GetROISteps()
        => new List<EDatumROIStep> { EDatumROIStep.Rect, EDatumROIStep.Rect };

    public override bool TryTeach(HImage image, DatumConfig config, out string error)
    {
        // Phase 11 DatumFindingService.TryTeachDatum 로직 그대로 이동
        // config.Line1_*/Line2_* ROI → 에지 → 교점 → RefOriginRow/Col/RefAngleRad
        error = "";
        return true;
    }
}
```

### CircleAndLineDatum 초안
```csharp
public class CircleAndLineDatum : DatumAlgorithmBase
{
    public override List<EDatumROIStep> GetROISteps()
        => new List<EDatumROIStep>
           { EDatumROIStep.Circle, EDatumROIStep.Rect, EDatumROIStep.Rect };

    public override bool TryTeach(HImage image, DatumConfig config, out string error)
    {
        error = "";
        // Step 1: 원 검출 → CircleCenter_Row/Col
        if (!TryDetectCircle(image, config, out error)) return false;
        // Step 2+3: Line1/Line2 수평 에지 피팅 → 평균 수평선
        if (!TryFitHorizontalLines(image, config, out error)) return false;
        // 교점: X = CircleCenter_Col, Y = 수평선의 Y
        config.RefOriginCol = config.CircleCenter_Col;
        config.RefOriginRow = /* 수평선 Y at X=CircleCenter_Col */;
        config.RefAngleRad  = /* 수평선 기울기 */;
        config.IsConfigured = true;
        config.LastTeachSucceeded = true;
        return true;
    }
}
```

### DatumFindingService 디스패처 초안
```csharp
public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
{
    return CreateAlgorithm(config.AlgorithmType).TryTeach(image, config, out error);
}

private static DatumAlgorithmBase CreateAlgorithm(EDatumAlgorithm type)
{
    switch (type)
    {
        case EDatumAlgorithm.CircleAndLine: return new CircleAndLineDatum();
        default: return new TwoLineIntersectDatum();
    }
}
```

### MainView 가변 단계 흐름 초안
```csharp
// 기존 EDatumTeachStep 제거 → 인덱스 기반
private List<EDatumROIStep> _datumTeachSteps;
private int _datumTeachStepIndex;

private void TeachDatumButton_Click(object sender, RoutedEventArgs e)
{
    var algorithm = DatumAlgorithmFactory.Create(_currentDatum.AlgorithmType);
    _datumTeachSteps = algorithm.GetROISteps();
    _datumTeachStepIndex = 0;
    _canvasMode = ECanvasMode.TeachDatum;
    StartNextDatumStep();
}

private void StartNextDatumStep()
{
    var step = _datumTeachSteps[_datumTeachStepIndex];
    var hint = $"Step {_datumTeachStepIndex + 1}/{_datumTeachSteps.Count}: " +
               (step == EDatumROIStep.Circle ? "원 검색 영역을 드래그하세요" : 
                _datumTeachStepIndex == 0 ? "Line1 ROI를 드래그하세요" : "Line2 ROI를 드래그하세요");
    label_drawHint.Content = hint;

    if (step == EDatumROIStep.Circle)
        halconViewerControl.StartCircleDrawing();
    else
        halconViewerControl.StartRectangleDrawing();
}

private void OnRectOrCircleCommitted(/* row, col, phi, len1, len2 or centerRow, centerCol, radius */)
{
    // 해당 단계 DatumConfig 필드 기록
    _datumTeachStepIndex++;
    if (_datumTeachStepIndex >= _datumTeachSteps.Count)
        InvokeTryTeachDatum();  // 마지막 단계 후 자동 실행
    else
        StartNextDatumStep();
}
```

### 예상 Plan 분할
- **Plan 01:** EDatumAlgorithm/EDatumROIStep enum + DatumAlgorithmBase + TwoLineIntersectDatum (Phase 11 로직 추출, 동작 변화 없음) + DatumFindingService 디스패처. 순수 리팩터링, 빌드 통과 + 기존 SIMUL_MODE 회귀 없음이 완료 조건.
- **Plan 02:** DatumConfig CircleROI 필드 + CircleAndLineDatum 구현 (EdgesSubPix→FitCircleContourXld + 수평선 교점) + HalconDisplayService CircleAndLine 오버레이 분기. CircleAndLine SIMUL_MODE 티칭 성공이 완료 조건.
- **Plan 03:** MainView 가변 단계 흐름 (EDatumTeachStep 제거 → 인덱스 기반) + HalconViewerControl.StartCircleDrawing + CircleDrawingCompleted 이벤트. SIMUL_MODE 양쪽 알고리즘(TwoLineIntersect/CircleAndLine) 전환 및 티칭 성공이 완료 조건.

3개 plan 구성. Plan 01 완료 후 Plan 02/03 병렬 가능 (Plan 02는 알고리즘, Plan 03은 UI).

</specifics>

<deferred>
## Deferred Ideas

- **TemplateMatch 기반 Datum** — 3번째 알고리즘. 구조만 준비, 구현은 별도 phase.
- **3점 기반 Datum (Three Point)** — 세 개의 원/마킹 좌표로 좌표계 정의. 구조만 준비.
- **CircleAndLine TryFind 알고리즘 분리** — Phase 12에서는 TryTeach 후 저장된 RefOrigin/RefAngle을 런타임에 재사용(TwoLineIntersect와 동일 TryFind). 원을 매번 검출하는 TryFind는 deferred.
- **AlgorithmType 변경 시 Wizard 재시작** — PropertyGrid에서 알고리즘 변경 시 ROI 초기화 + 재티칭 안내. IsConfigured 리셋은 구현하되, 별도 다이얼로그는 deferred.
- **CircleAndLine의 원 반지름 범위 설정** — 현재는 형상 기반 필터링. 사용자가 예상 반지름 ± 허용값 설정하는 파라미터는 deferred.
- **MeasurementBase.TryExecute의 Datum 알고리즘 분기** — 현재 Measurement는 알고리즘 무관하게 RefOrigin/RefAngle 변환만 소비. Measurement별 Datum 알고리즘 의존성은 없음 → 미적용.

</deferred>

---

*Phase: 12-datum-algorithm-extensibility*
*Context gathered: 2026-04-23*
