# Phase 6: Rapid City 확장 - Context

**Gathered:** 2026-04-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Rapid City Z-Stopper A8.1 제품 대응을 위해 기존 Shot-FAI 2계층 구조를 확장한다. 핵심 변경은 3가지이다:

1. **Fixture 개념 도입**: 기존 Sequence 노드를 Fixture(= 한 면의 검사 묶음)로 재활용한다. Datum은 Shot 레벨에서 Sequence(Fixture) 레벨로 승격되며, 같은 Fixture 안의 모든 Shot이 Datum을 공유한다.

2. **Multi-Datum**: 한 Fixture에 Datum이 여러 개(B, C 등) 존재할 수 있다. Phase 4의 DatumConfig 클래스는 그대로 유지하고, Sequence가 List<DatumConfig>를 소유한다.

3. **Multi-Algorithm 측정**: 기존 에지 페어 거리 측정(Phase 3) 외에 5가지 알고리즘(line fit, point-to-line distance, point-to-point distance, line-line angle, circle diameter)을 추가한다. MeasurementBase 추상 클래스 + 알고리즘별 파생 클래스 구조로 구현한다.

**3D/Laser 검사는 범위 외**이다. **Side 카메라는 별도 PC에서 같은 SW를 독립 운영하므로 SW 추가 작업 없음**. 기존 Phase 1~5의 INI 레시피 마이그레이션은 하지 않으며, 새 레시피만 지원한다.

### 참고 문서
- `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB` — Datum A/B/C 정의, Pre-Datum 절차, 측정 항목별 Datum 참조
- `CYG_Rapid_City_DFM-A8_0_Z_Stopper_V1_0` — FAI 75개 목록, Station/카메라 구성, CT 분해
- `010_RC_Varo_AOI_MSoP___Z_Stopper_20230428` — 이전 Varo 제품 Datum 패턴 참고

</domain>

<decisions>
## Implementation Decisions

### Fixture = Sequence 재활용
- **D-01:** 기존 Sequence 노드를 Fixture(한 면의 검사 묶음)로 재활용한다. 새 노드 타입을 추가하지 않는다. Sequence 이름을 사용자가 직접 편집할 수 있도록 한다 (예: "Top 면", "Bottom 면").
- **D-02:** Fixture(Sequence) 안에 Datum 노드 N개 + Shot(Action) 노드 N개가 들어간다. 같은 Fixture 안의 모든 Shot은 Fixture의 Datum을 공유한다.
- **D-03:** 제품 한 개의 전체 검사는 여러 Fixture(Sequence)로 구성된다. 예: Top 면, Bottom 면, Side Left, Side Right 등. 사람이 제품을 뒤집어가며 각 Fixture를 순서대로 실행한다.

### Datum 승격 (Shot → Sequence)
- **D-04:** DatumConfig는 더 이상 ShotConfig에 소속되지 않는다. Sequence(Fixture) 레벨로 승격한다. Sequence가 `List<DatumConfig>`를 소유한다.
- **D-05:** Phase 4에서 만든 DatumConfig 클래스 자체는 수정하지 않는다. 기존 필드(Line1_ROI, Line2_ROI, RefOriginRow/Col, RefAngleRad, CurrentTransform)를 그대로 사용한다.
- **D-06:** 각 DatumConfig에 `DatumName` (string) 필드를 추가한다. 사용자가 "B", "C" 등 이름을 지정한다. 기본값은 "Datum_1", "Datum_2".

### Datum 이미지 소스
- **D-07:** DatumConfig에 `ImageSourceMode` 속성을 추가한다. 두 가지 모드:
  - `Dedicated` — Datum 전용 캡처 조건(Z, 조명, 노출)을 별도 보유. 검사 시작 시 Datum 이미지를 먼저 캡처.
  - `ReuseFromShot` — 지정된 Shot의 이미지를 재사용. 추가 캡처 없음.
- **D-08:** 기본값은 `Dedicated`. Datum 기준선이 측정용 이미지에서도 잘 보이면 사용자가 `ReuseFromShot`으로 변경할 수 있다.

### Datum 실행 흐름
- **D-09:** 런타임 실행 순서:
  1. Fixture(Sequence) 진입
  2. Datum 단계: 각 DatumConfig에 대해 이미지 취득(Dedicated 또는 ReuseFromShot) → `DatumFindingService.TryFindDatum()` 호출 → `CurrentTransform` 계산
  3. 하나라도 실패 시: 해당 Fixture 전체 NG, 다음 Fixture로 넘어감
  4. 모두 성공 시: Shot 루프 → 각 FAI는 자기 `DatumRef`에 해당하는 Transform으로 ROI 보정 → 측정 수행
- **D-10:** Datum 미설정 Fixture는 무보정(Identity Transform) pass-through로 동작한다. 기존 Phase 3/4/5 흐름과 호환.

### Multi-Light (조명)
- **D-11:** ShotConfig(CameraSlaveParam)에 조명 제어 필드를 추가한다:
  - `RingLight_Enabled` (bool), `RingLight_Brightness` (int, 0~100)
  - `BackLight_Enabled` (bool), `BackLight_Brightness` (int, 0~100)
  - `CoaxLight_Enabled` (bool), `CoaxLight_Brightness` (int, 0~100)
  - `SideLight_Enabled` (bool), `SideLight_Brightness` (int, 0~100)
- **D-12:** 조명 제어의 실제 하드웨어 통신(시리얼, PLC 등)은 Phase 6 범위 외이다. ShotConfig에 값만 저장하고, 하드웨어 연동은 별도 Phase에서 구현한다. SIMUL_MODE에서는 조명 값이 무시된다.
- **D-13:** 한 Shot = "이 Z + 이 조명 + 이 노출로 한 번 찍는다"의 최소 단위이다. 조건이 1개라도 다르면 별도 Shot으로 분리한다.

### 측정 알고리즘 (Multi-Algorithm)
- **D-14:** MeasurementBase 추상 클래스를 도입한다. ParamBase를 상속하며 공통 필드를 보유한다:
  - `DatumRef` (string) — 어느 Datum을 사용할지 ("B", "C" 등). 빈 문자열이면 무보정.
  - `NominalValue`, `TolerancePlus`, `ToleranceMinus` — 공차
  - `LastMeasuredValue` — 최근 측정 결과
  - `LastJudgement` — OK/NG
  - `abstract double Execute(HImage image, HomMat2D datumTransform)` — 각 파생 클래스가 구현
- **D-15:** 알고리즘별 파생 클래스 6개:
  1. `EdgePairDistanceMeasurement` — 기존 Phase 3 에지 페어 거리 (기존 FAIEdgeMeasurementService 래핑)
  2. `PointToLineDistanceMeasurement` — 에지 점 → Datum 기준선 거리 (가장 많이 사용)
  3. `PointToPointDistanceMeasurement` — 두 에지 점 사이 거리
  4. `LineToLineAngleMeasurement` — 두 직선 사이 각도
  5. `CircleDiameterMeasurement` — 원 검출 + 직경
  6. `LineToLineDistanceMeasurement` — 두 평행선 사이 거리
- **D-16:** 각 파생 클래스는 자기 알고리즘에 필요한 ROI만 소유한다. 예: PointToLineDistanceMeasurement는 PointRoi 1개 + LineRoi 1개. CircleDiameterMeasurement는 CircleSearchRoi 1개.
- **D-17:** INI 직렬화 시 `Type=` 필드로 파생 클래스 타입을 식별한다. 로드 시 factory 패턴으로 올바른 클래스 인스턴스를 생성한다. Phase 1의 `IsDynamicFAIMode` 분기 패턴과 동일한 접근.

### Halcon 빌딩 블록
- **D-18:** Halcon 알고리즘 빌딩 블록 서비스 클래스를 신규 생성한다 (`VisionAlgorithmService` 또는 유사 이름):
  - `FitLine(HImage, RoiDefinition) → (row1, col1, row2, col2, angle)` — 에지 점에서 직선 fit
  - `FindCircle(HImage, RoiDefinition) → (centerRow, centerCol, radius)` — 원 검출
  - `IntersectLines(line1, line2) → (row, col)` — 두 직선 교점
  - `DistancePointToLine(point, line) → double` — 점-선 거리
  - `DistancePointToPoint(p1, p2) → double` — 점-점 거리
  - `AngleLineLine(line1, line2) → double` — 두 직선 각도
  - `PerpendicularLineThroughPoint(line, point) → line2` — 점을 통과하며 주어진 선에 수직인 직선
- **D-19:** 기존 `FAIEdgeMeasurementService`는 `EdgePairDistanceMeasurement.Execute()` 내부에서 호출되는 형태로 래핑한다. 기존 코드를 삭제하지 않고 재사용한다.

### FAI 구조 변경
- **D-20:** 기존 FAIConfig의 단일 측정 구조를 `List<MeasurementBase>`로 교체한다. 각 FAI가 여러 종류의 측정을 보유할 수 있다. 단일 측정 FAI(기존 호환)는 List 길이 1.
- **D-21:** FAI 결과 테이블은 MeasurementBase 단위로 한 행씩 표시한다. 기존 FAI별 1행에서 Measurement별 1행으로 변경.

### 데이터 마이그레이션
- **D-22:** 기존 Phase 1~5 INI 레시피 포맷의 마이그레이션은 하지 않는다. Phase 6 이후에는 새 포맷으로만 레시피를 생성/저장한다. 기존 포맷 INI를 로드 시도하면 "새 레시피로 작성하세요" 안내 메시지를 표시한다.

### Side 카메라
- **D-23:** Side 카메라는 별도 PC에서 같은 SW를 독립 운영한다. SW 내부에 Side 카메라 전용 로직은 추가하지 않는다. PROJECT.md의 "Side 카메라 — OUT OF SCOPE"를 "Side 카메라 — 별도 PC 독립 운영, SW 변경 불필요"로 변경한다.

### UI 트리 구조
- **D-24:** Phase 6 이후 InspectionListView 트리 구조:
```
Sequence "Top 면" (= Fixture, 사용자 이름 편집 가능)
├─ 📐 Datum "B" (DatumConfig, 사용자 이름 편집)
├─ 📐 Datum "C" (DatumConfig, 사용자 이름 편집)
├─ Action "Shot 1" (= Shot, ShotConfig: Z + 조명 + 노출)
│  └─ FAI "A1" (FAIConfig)
│     ├─ 측정 1 (PointToLineDistanceMeasurement, DatumRef="B")
│     └─ 측정 2 (EdgePairDistanceMeasurement, DatumRef="C")
├─ Action "Shot 2" (= Shot, ShotConfig: 다른 Z + 다른 조명)
│  └─ FAI "A4" (FAIConfig)
│     └─ 측정 3 (LineToLineAngleMeasurement, DatumRef="B")
└─ ...
```
- **D-25:** Datum 노드는 Sequence(Fixture) 노드의 직접 자식이다. Action(Shot) 노드와 같은 레벨의 형제이다. ShotConfig에서 DatumConfig 소유를 제거하고 Sequence 레벨로 옮긴다.

### Claude's Discretion
- VisionAlgorithmService 내부의 Halcon 연산자 선택 (measure_pos vs edges_sub_pix vs fit_line_contour_xld 등)
- MeasurementBase 파생 클래스의 PropertyGrid 표시 세부 (각 타입별 어떤 필드 노출할지)
- Datum Dedicated 모드의 캡처 조건을 DatumConfig 내부에 둘지 별도 ShotConfig로 뺄지
- INI 직렬화에서 MeasurementBase factory의 구체적인 타입 매핑 방식
- 결과 테이블에서 Measurement 타입별 표시 포맷 (거리는 mm, 각도는 °, 직경은 mm)
- Sequence 이름 편집이 기존 UI에서 이미 지원되는지 (RESEARCH에서 확인)
- DatumConfig에 DatumName 필드 추가 시 Phase 4 INI 호환성 처리 (기본값 fallback)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 4 결과물 (Datum — 확장 대상)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 단일 Datum 모델. DatumName 필드 추가 대상. 클래스 자체는 수정 최소화.
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — `TryFindDatum(image, datumConfig, out transform, out error)`. 그대로 재사용.
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — 현재 DatumConfig를 소유. **DatumConfig 소유를 제거하고 Sequence 레벨로 이전해야 함.**

### Phase 3 결과물 (에지 측정 — 래핑 대상)
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` — 기존 에지 페어 거리 측정. EdgePairDistanceMeasurement에서 래핑하여 재사용.
- `WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs` — 기존 결과 모델. MeasurementBase.LastMeasuredValue로 통합.

### Phase 5 결과물 (시퀀스 — 수정 대상)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — Shot 실행 + FAI 루프. **Datum을 Sequence에서 가져오도록 변경 필요.**
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — 레시피 CRUD + INI 저장/로드. **새 포맷 저장/로드 + 기존 포맷 거부 로직 추가.**

### Phase 1 결과물 (UI 트리 — 확장 대상)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 트리 CRUD. Sequence 레벨 Datum 노드 추가.
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — 트리 생성 로직. Datum 노드를 Sequence 하위에 생성.
- `WPF_Example/UI/ControlItem/Node.cs` — ENodeType. `Measurement` 타입 추가 가능.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — 결과 테이블 FAI→Measurement 단위 전환.

### 프레임워크 기반
- `WPF_Example/Sequence/Param/ParamBase.cs` — INI 직렬화. MeasurementBase가 상속.
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` — ShotConfig 기반. 조명 필드 추가 대상.
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` — Sequence 레벨 속성(List<DatumConfig>) 추가 위치 확인 필요.

### 참고 문서
- `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB` — Datum B/C 정의, 측정 항목별 Datum 참조, Pre-Datum 절차
- `CYG_Rapid_City_DFM-A8_0_Z_Stopper_V1_0` — FAI 75개 목록, 카메라/조명 사양, CT 분해
- `010_RC_Varo_AOI_MSoP___Z_Stopper_20230428` — 이전 Varo 제품 Datum 패턴

</canonical_refs>

<code_context>
## Existing Code Insights

### 현재 구조 (Phase 5 완료 시점)
```
Sequence
└─ Action (= Shot, ShotConfig: Z + 카메라 + Datum 1개)
   └─ FAI (FAIConfig: ROI + 단일 에지 페어 측정)
```

### Phase 6 이후 구조
```
Sequence (= Fixture, 사용자 이름 편집 가능)
├─ DatumConfig "B" (Sequence 소유)
├─ DatumConfig "C" (Sequence 소유)
└─ Action (= Shot, ShotConfig: Z + 조명 + 노출, Datum 미소유)
   └─ FAI (FAIConfig)
      └─ MeasurementBase (파생: PointToLineDist, Angle, Circle 등)
         └─ DatumRef = "B" or "C"
```

### 핵심 변경점
1. **ShotConfig에서 DatumConfig 소유 제거** → Sequence 레벨로 이전
2. **ShotConfig에 조명 필드 추가** (Ring/Back/Coax/Side + Brightness)
3. **FAIConfig의 측정 모델을 MeasurementBase 파생 클래스로 교체**
4. **Action_FAIMeasurement가 Datum을 Sequence에서 가져오도록 변경**
5. **InspectionRecipeManager의 INI 저장/로드를 새 포맷으로 재작성** (기존 포맷 마이그레이션 안 함)

### ParamBase 직렬화 — 다형성 처리
Phase 1의 IsDynamicFAIMode 패턴 참고:
```
[Measurement_0]
Type=PointToLineDistance       ← factory가 이 값으로 파생 클래스 결정
DatumRef=B
NominalValue=20.347
TolerancePlus=0.015
ToleranceMinus=-0.015
PointRoi_Row=1234
PointRoi_Col=5678
LineRoi_Row=900
...
```

### Datum 실행 흐름 변경
현재 (Phase 4/5):
```
Action_FAIMeasurement.Execute():
  shot.DatumConfig → FindDatum → transform
  for each FAI: TryMeasure(image, fai, transform)
```

Phase 6 이후:
```
Action_FAIMeasurement.Execute():
  parentSequence.DatumConfigs → 각각 FindDatum → Dictionary<string, HomMat2D>
  for each FAI:
    for each Measurement:
      transform = dictionary[measurement.DatumRef]
      measurement.Execute(image, transform)
```

### C# 7.2 제약 유지
- abstract class + override 사용 가능 (C# 1.0부터)
- Dictionary<string, HomMat2D> 사용 가능
- nullable reference types, switch expressions 사용 불가

### 빌드 커맨드
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`

</code_context>

<specifics>
## Specific Ideas

### MeasurementBase 클래스 초안
```csharp
abstract class MeasurementBase : ParamBase
{
    // 공통: Datum 참조
    string DatumRef { get; set; }         // "B", "C", "" (빈=무보정)

    // 공통: 공차
    double NominalValue { get; set; }
    double TolerancePlus { get; set; }
    double ToleranceMinus { get; set; }

    // 공통: 결과 (휘발성)
    double LastMeasuredValue { get; set; }
    bool   LastJudgement { get; set; }    // true=OK

    // 각 파생 클래스가 구현
    abstract bool TryExecute(HImage image, HomMat2D datumTransform,
                             double pixelResolution,
                             out double resultMm, out string error);
}
```

### 파생 클래스 예시: PointToLineDistanceMeasurement
```csharp
class PointToLineDistanceMeasurement : MeasurementBase
{
    // 이 알고리즘에 필요한 ROI만
    double PointRoi_Row, PointRoi_Col, PointRoi_Phi, PointRoi_L1, PointRoi_L2;
    double LineRoi_Row, LineRoi_Col, LineRoi_Phi, LineRoi_L1, LineRoi_L2;

    override bool TryExecute(HImage image, HomMat2D transform,
                              double pixelRes, out double resultMm, out string error)
    {
        // 1. transform으로 ROI 보정
        // 2. PointRoi에서 에지 점 검출
        // 3. LineRoi에서 직선 fit
        // 4. 점-선 거리(픽셀) → mm 변환
        // 5. resultMm 반환
    }
}
```

### Factory 패턴 초안
```csharp
static class MeasurementFactory
{
    static MeasurementBase Create(string typeName)
    {
        switch(typeName)
        {
            case "EdgePairDistance":       return new EdgePairDistanceMeasurement();
            case "PointToLineDistance":    return new PointToLineDistanceMeasurement();
            case "PointToPointDistance":   return new PointToPointDistanceMeasurement();
            case "LineToLineAngle":       return new LineToLineAngleMeasurement();
            case "CircleDiameter":        return new CircleDiameterMeasurement();
            case "LineToLineDistance":    return new LineToLineDistanceMeasurement();
            default: return null;
        }
    }
}
```

### Sequence에 DatumConfig 리스트 추가 초안
```csharp
// SequenceBase 또는 InspectionSequence에 추가
List<DatumConfig> DatumConfigs { get; set; } = new List<DatumConfig>();

// 런타임 캐시 (휘발성)
Dictionary<string, HomMat2D> DatumTransforms { get; set; }
```

### 조명 필드 초안 (ShotConfig/CameraSlaveParam에 추가)
```csharp
// CameraSlaveParam 또는 ShotConfig에 추가
bool RingLight_Enabled { get; set; }
int  RingLight_Brightness { get; set; }     // 0~100
bool BackLight_Enabled { get; set; }
int  BackLight_Brightness { get; set; }
bool CoaxLight_Enabled { get; set; }
int  CoaxLight_Brightness { get; set; }
bool SideLight_Enabled { get; set; }
int  SideLight_Brightness { get; set; }
```

### 예상 Plan 분할
- **Plan 01**: MeasurementBase + 파생 클래스 6개 + VisionAlgorithmService + Factory
- **Plan 02**: Datum 승격 (ShotConfig → Sequence) + Multi-Datum + Datum 실행 흐름 변경
- **Plan 03**: 조명 필드 추가 + UI 트리 재구성 + 결과 테이블 Measurement 단위 전환 + INI 새 포맷 저장/로드

</specifics>

<deferred>
## Deferred Ideas

- Pre-Datum (거친 정렬, pattern matching) — 정밀 Datum만으로 부족할 때 추가
- 조명 하드웨어 실제 제어 (시리얼/PLC 통신) — 현재는 값 저장만
- Datum A as Plane (3D 평면 기반) — 3D 범위 외
- 3D/Laser 검사 전체 — 범위 외
- 기존 Phase 1~5 INI 레시피 마이그레이션 — 필요 시 별도 Phase
- Datum 결과 시각화 오버레이 (기준 교점 + 축 화살표 캔버스 표시)
- MeasurementBase 파생 클래스 추가 (PerpendicularDistanceMeasurement, ArcLengthMeasurement 등)
- Cpk/SPC 통계 자동 계산 + 차트 — 데이터 출력 Phase에서
- MES/Traceability 연동 — 별도 Phase
- 복수 카메라 동시 제어 (Top+Side 한 PC) — 별도 PC 운영으로 불필요
- Datum 티칭 wizard UI (step-by-step 가이드) — 편의 기능
- 측정 결과 이미지 자동 저장 (NG 이미지 보관) — 별도 Phase

</deferred>

---

*Phase: 06-rapid-city*
*Context gathered: 2026-04-10*
