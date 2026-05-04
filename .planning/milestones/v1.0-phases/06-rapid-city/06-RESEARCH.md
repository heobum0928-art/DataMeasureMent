# Phase 6: Rapid City 확장 - Research

**Researched:** 2026-04-10
**Domain:** WPF + Halcon 에지 측정 알고리즘 확장, Shot-FAI 2계층 → Fixture-Datum-Shot-FAI-Measurement 3계층 구조 전환
**Confidence:** HIGH (모든 핵심 클래스 직접 열람, 코드베이스 검증)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Fixture = Sequence 재활용**
- D-01: 기존 Sequence 노드를 Fixture로 재활용. 새 노드 타입 추가 없음. Sequence 이름 사용자 편집 가능.
- D-02: Fixture 안에 Datum 노드 N개 + Shot(Action) 노드 N개. 같은 Fixture의 모든 Shot은 Fixture의 Datum 공유.
- D-03: 제품 한 개의 검사 = 여러 Fixture(Sequence)로 구성.

**Datum 승격 (Shot → Sequence)**
- D-04: DatumConfig는 Sequence(Fixture) 레벨로 승격. Sequence가 `List<DatumConfig>` 소유.
- D-05: Phase 4의 DatumConfig 클래스 자체 수정 최소화. 기존 필드 그대로 유지.
- D-06: 각 DatumConfig에 `DatumName` (string) 필드 추가. 기본값 "Datum_1", "Datum_2".

**Datum 이미지 소스**
- D-07: DatumConfig에 `ImageSourceMode` 속성 추가. `Dedicated` 또는 `ReuseFromShot`.
- D-08: 기본값 `Dedicated`.

**Datum 실행 흐름**
- D-09: 런타임 순서: Fixture 진입 → 각 DatumConfig 이미지 취득 → TryFindDatum → CurrentTransform → 하나라도 실패 시 Fixture 전체 NG → 성공 시 Shot 루프 → 각 FAI는 DatumRef Transform으로 ROI 보정 → 측정.
- D-10: Datum 미설정 Fixture는 Identity Transform pass-through (기존 Phase 3/4/5 호환).

**Multi-Light (조명)**
- D-11: ShotConfig(CameraSlaveParam)에 Ring/Back/Coax/Side 조명 필드 추가 (Enabled bool + Brightness int 0~100 각 4쌍).
- D-12: 하드웨어 연동은 Phase 6 범위 외. 값 저장만. SIMUL_MODE에서 무시.
- D-13: 한 Shot = Z + 조명 + 노출 최소 단위.

**측정 알고리즘 (Multi-Algorithm)**
- D-14: MeasurementBase 추상 클래스 도입. ParamBase 상속. 공통 필드: DatumRef, NominalValue, TolerancePlus, ToleranceMinus, LastMeasuredValue, LastJudgement. abstract `TryExecute()` 서명 사용.
- D-15: 파생 클래스 6개: EdgePairDistanceMeasurement, PointToLineDistanceMeasurement, PointToPointDistanceMeasurement, LineToLineAngleMeasurement, CircleDiameterMeasurement, LineToLineDistanceMeasurement.
- D-16: 각 파생 클래스는 자기 알고리즘에 필요한 ROI만 소유.
- D-17: INI 직렬화 시 `Type=` 필드로 타입 식별, factory 패턴으로 로드.

**Halcon 빌딩 블록**
- D-18: VisionAlgorithmService(또는 유사 이름) 신규 생성: FitLine, FindCircle, IntersectLines, DistancePointToLine, DistancePointToPoint, AngleLineLine, PerpendicularLineThroughPoint.
- D-19: FAIEdgeMeasurementService는 EdgePairDistanceMeasurement.TryExecute() 내부에서 래핑하여 재사용. 기존 코드 삭제 안 함.

**FAI 구조 변경**
- D-20: FAIConfig의 단일 측정 구조를 `List<MeasurementBase>`로 교체. 단일 측정 FAI(기존 호환)는 List 길이 1.
- D-21: FAI 결과 테이블은 MeasurementBase 단위로 한 행씩 표시.

**데이터 마이그레이션**
- D-22: 기존 Phase 1~5 INI 레시피 포맷 마이그레이션 없음. 기존 포맷 로드 시도 시 "새 레시피로 작성하세요" 안내 메시지.

**Side 카메라**
- D-23: Side 카메라는 별도 PC 독립 운영. SW 내부에 Side 전용 로직 추가 없음.

**UI 트리 구조**
- D-24: 트리 구조: Sequence(Fixture) > Datum "B" + Datum "C" (Sequence 직계 자식, Action과 형제) + Action(Shot) > FAI > Measurement.
- D-25: ShotConfig에서 DatumConfig 소유 제거. Sequence 레벨로 옮김.

### Claude's Discretion
- VisionAlgorithmService 내부의 Halcon 연산자 선택
- MeasurementBase 파생 클래스의 PropertyGrid 표시 세부
- Datum Dedicated 모드의 캡처 조건을 DatumConfig 내부에 둘지 별도 ShotConfig로 뺄지
- INI 직렬화에서 MeasurementBase factory의 구체적인 타입 매핑 방식
- 결과 테이블에서 Measurement 타입별 표시 포맷 (거리는 mm, 각도는 °, 직경은 mm)
- Sequence 이름 편집이 기존 UI에서 이미 지원되는지 (RESEARCH에서 확인)
- DatumConfig에 DatumName 필드 추가 시 Phase 4 INI 호환성 처리

### Deferred Ideas (OUT OF SCOPE)
- Pre-Datum (거친 정렬, pattern matching)
- 조명 하드웨어 실제 제어
- Datum A as Plane (3D 평면 기반)
- 3D/Laser 검사 전체
- 기존 Phase 1~5 INI 레시피 마이그레이션
- Datum 결과 시각화 오버레이
- MeasurementBase 파생 클래스 추가 (Perpendicular, ArcLength 등)
- Cpk/SPC 통계
- MES/Traceability 연동
- 복수 카메라 동시 제어
- Datum 티칭 wizard UI
- 측정 결과 이미지 자동 저장
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RC-01 | Sequence가 Fixture(한 면)로 동작하며 List<DatumConfig>를 소유한다 | InspectionSequence에 DatumConfigs 프로퍼티 추가. SequenceBase.Name은 private set이므로 DisplayName 필드 추가 필요. |
| RC-02 | DatumConfig가 Sequence 레벨로 승격되어 Multi-Datum을 지원한다 | ShotConfig.Datum 필드 제거, DatumConfig에 DatumName + ImageSourceMode 추가, InspectionRecipeManager 저장/로드 재설계. |
| RC-03 | MeasurementBase 파생 클래스 6종이 각각 TryExecute()로 측정을 수행한다 | FAIEdgeMeasurementService + MeasurementAlgorithm 재사용 패턴 확인. FAIConfig에 List<MeasurementBase> 추가. VisionAlgorithmService 신규 생성. |
| RC-04 | ShotConfig에 Ring/Back/Coax/Side 조명 필드가 추가되고 INI로 저장/로드된다 | CameraSlaveParam 상속 ShotConfig에 bool+int 필드 8개 추가. ParamBase.Save/Load 자동 직렬화 확인. |
| RC-05 | 새 INI 포맷으로 레시피 저장/로드가 동작하고, 기존 포맷은 안내 메시지를 표시한다 | InspectionRecipeManager.Save/Load 재설계. HasNewFormatData 분기 패턴 활용. |
| RC-06 | UI 트리에서 Sequence > Datum + Shot > FAI > Measurement 구조를 탐색할 수 있고, 결과 테이블이 Measurement 단위로 표시된다 | ENodeType에 Measurement 추가, InspectionListViewModel.CreateSequenceNode 재설계, FAIResultRow → MeasurementResultRow로 확장. |
</phase_requirements>

---

## Summary

Phase 6는 기존 Shot-FAI 2계층 구조를 Fixture-Datum-Shot-FAI-Measurement 3계층으로 확장하는 작업이다. 코드베이스를 직접 열람한 결과, Phase 4/5에서 이미 `DatumConfig`(단일)와 `ENodeType.Datum`이 구현되어 있으므로 Phase 6의 변경은 "Datum을 Shot에서 Sequence로 승격" + "FAI에 복수 Measurement 추가" + "6종 알고리즘 구현"이 핵심이다.

가장 큰 구조 변경은 3가지이다. 첫째, `ShotConfig.Datum`(단일) 제거 → `InspectionSequence.DatumConfigs`(List)로 이전. 둘째, `FAIConfig`에 `List<MeasurementBase>` 추가 (기존 에지 측정 필드는 `EdgePairDistanceMeasurement`으로 마이그레이션). 셋째, `InspectionRecipeManager`의 INI 포맷을 Sequence-Datum-Shot-FAI-Measurement 계층으로 재설계.

`SequenceBase.Name`이 `private set`이므로 사용자 편집 가능 이름을 위해서는 `InspectionSequence`에 별도 `DisplayName` 프로퍼티를 추가하는 방식이 적합하다. `InspectionListViewModel.CreateSequenceNode`에서 현재 `seq.Name`을 표시 이름으로 쓰고 있으므로 `DisplayName`으로 교체하면 된다.

**Primary recommendation:** Plan을 3개로 분할 — (01) MeasurementBase 6종 + VisionAlgorithmService, (02) Datum 승격 + Sequence DisplayName + Action_FAIMeasurement 재설계, (03) 조명 필드 + UI 트리 재구성 + 결과 테이블 + INI 새 포맷.

---

## Project Constraints (from CLAUDE.md)

| Directive | 상세 |
|-----------|------|
| .NET Framework 4.8 + WPF + Halcon 24.11 | 변경 불가 |
| C# 7.2 | nullable reference types, switch expressions, record 사용 불가 |
| abstract class + override | C# 1.0부터 지원 — MeasurementBase에 적용 가능 |
| ParamBase 상속 패턴 | INI 자동 직렬화 (Int32, Double, String, Boolean, Rect, Line, Circle) |
| HOperatorSet 호출은 try/catch(return false) 감싸기 | |
| HImage using 블록 또는 명시적 Dispose | |
| lock(_imageLock) 쓰레드 안전 이미지 버퍼 | |
| UPPER_SNAKE_CASE 상수, PascalCase 공개 메서드, camelCase 또는 _prefix 비공개 필드 | |
| 날짜 주석 //YYMMDD hbk 필수 | MEMORY.md에서 확인 |
| 기존 INI 포맷 하위 호환 (IsDynamicFAIMode 분기) | Phase 6: 기존 포맷 거부 + 안내 메시지 방식으로 변경 |

---

## Standard Stack

### Core
| 구성 요소 | 위치 | 목적 | 비고 |
|----------|------|------|------|
| HalconDotNet (HALCON 24.11) | C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\ | 에지 측정 알고리즘 | HOperatorSet.FitLineContourXld, AffineTransPoint2d, IntersectionLl 등 |
| ParamBase | WPF_Example/Sequence/Param/ParamBase.cs | INI 자동 직렬화 + DrawableSource 등록 | MeasurementBase 상속 기반 |
| CameraSlaveParam | WPF_Example/Sequence/Param/CameraSlaveParam.cs | ShotConfig 기반 — 조명 필드 추가 위치 | |
| FAIEdgeMeasurementService | WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs | EdgePairDistanceMeasurement에서 래핑 | 기존 코드 유지 |
| DatumFindingService | WPF_Example/Halcon/Algorithms/DatumFindingService.cs | TryFindDatum() → HomMat2D 반환 | 그대로 재사용 |
| InspectionSequence | WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs | Fixture 역할 담당. DatumConfigs 추가 위치 | |
| InspectionRecipeManager | WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs | 레시피 CRUD + INI 저장/로드 | Phase 6에서 포맷 재설계 |

### Supporting
| 구성 요소 | 위치 | 목적 |
|----------|------|------|
| MeasurementAlgorithm | WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs | TryInspectSingleEdgeInternal 패턴 — 에지 검출 참고용 |
| RoiDefinition | WPF_Example/Halcon/Models/RoiDefinition.cs | ROI 파라미터 전달 모델 |
| FAIEdgeMeasurementResult | WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs | EdgePairDistanceMeasurement 결과 모델 |
| ENodeType, Node, CompositeNode | WPF_Example/UI/ControlItem/Node.cs | UI 트리 노드 타입 — Measurement 타입 추가 대상 |
| InspectionListViewModel | WPF_Example/UI/ControlItem/InspectionListViewModel.cs | 트리 생성 로직 재설계 대상 |
| FAIResultRow | WPF_Example/UI/ViewModel/FAIResultRow.cs | Measurement 단위 결과 행으로 확장 대상 |
| InspectionViewModel | WPF_Example/UI/ViewModel/InspectionViewModel.cs | OnFAISelected / OnActionSelected — Measurement 반환으로 변경 대상 |
| IniFile | WPF_Example/Utility/ | INI 저장/로드 유틸리티 |

### Alternatives Considered
| 대신 | 사용 가능 | 트레이드오프 |
|------|----------|-------------|
| SequenceBase.Name private set 유지 | InspectionSequence에 DisplayName 추가 | SequenceBase 프레임워크 클래스 변경 최소화 권장 |
| FAIConfig 단일 Measurement 필드 유지 | List<MeasurementBase> 추가 | D-20 결정에 따라 List 교체 필수 |

---

## Architecture Patterns

### 현재 구조 (Phase 5 완료 시점)

```
InspectionSequence (SequenceBase)
  Param: CameraMasterParam
  Actions[]:
    Action_FAIMeasurement
      Param: ShotConfig (CameraSlaveParam)
        .Datum: DatumConfig (1개, Phase 4)
        .FAIList: List<FAIConfig>
          FAIConfig: ROI + 에지 파라미터 + 공차 + 결과
```

### Phase 6 이후 목표 구조

```
InspectionSequence (SequenceBase)
  Param: CameraMasterParam
  DisplayName: string (사용자 편집 가능)
  DatumConfigs: List<DatumConfig>  ← NEW
    DatumConfig "B": DatumName + ImageSourceMode + Line ROIs
    DatumConfig "C": DatumName + ImageSourceMode + Line ROIs
  _datumTransforms: Dictionary<string, HTuple>  ← 런타임 캐시 (volatile)
  Actions[]:
    Action_FAIMeasurement
      Param: ShotConfig (CameraSlaveParam)
        .RingLight_Enabled, .RingLight_Brightness  ← NEW
        .BackLight_Enabled, .BackLight_Brightness  ← NEW
        .CoaxLight_Enabled, .CoaxLight_Brightness  ← NEW
        .SideLight_Enabled, .SideLight_Brightness  ← NEW
        // .Datum 제거됨                           ← REMOVED
        .FAIList: List<FAIConfig>
          FAIConfig: FAIName + List<MeasurementBase>  ← NEW
            MeasurementBase (파생 6종)
              .DatumRef: string
              .NominalValue, .TolerancePlus, .ToleranceMinus
              .LastMeasuredValue, .LastJudgement
```

### 핵심 설계 결정: Datum 실행 흐름

**현재 (Phase 4/5):**
```csharp
// Action_FAIMeasurement.Run() → EStep.Measure
// ShotConfig 안에서 Datum을 꺼냄
DatumFindingService datumSvc = new DatumFindingService();
datumSvc.TryFindDatum(image, shot.Datum, out datumTransform, out error);
service.TryMeasure(image, fai, datumTransform, out result);
```

**Phase 6 이후:**
```csharp
// Step 1: Datum 단계 (새 EStep.Datum 추가 또는 Init 단계 확장)
// parentSequence에서 DatumConfigs 꺼냄
InspectionSequence parentSeq = GetParentSequence();
Dictionary<string, HTuple> transforms = new Dictionary<string, HTuple>();
foreach (var datum in parentSeq.DatumConfigs) {
    HTuple t;
    string err;
    if (!datumSvc.TryFindDatum(image, datum, out t, out err)) {
        // 전체 Fixture NG
        datumFailed = true;
        break;
    }
    transforms[datum.DatumName] = t;
}

// Step 2: Shot 루프
foreach (var fai in shot.FAIList) {
    foreach (var m in fai.Measurements) {
        HTuple transform;
        if (!string.IsNullOrEmpty(m.DatumRef) && transforms.TryGetValue(m.DatumRef, out transform)) {
            m.TryExecute(image, new HomMat2D(transform), shot.PixelResolutionX, out resultMm, out err);
        } else {
            // DatumRef 없거나 미설정 → identity (D-10 호환)
            m.TryExecute(image, identity, shot.PixelResolutionX, out resultMm, out err);
        }
    }
}
```

### INI 새 포맷 설계

```ini
[FORMAT]
Version=6                          ← 포맷 버전 식별자

[FIXTURE]
DisplayName=Top 면
DatumCount=2

[FIXTURE_DATUM_0]
DatumName=B
ImageSourceMode=Dedicated
IsConfigured=True
Line1_Row=...
; ... DatumConfig 필드 전체

[FIXTURE_DATUM_1]
DatumName=C
; ...

[SHOTS]
Count=2

[SHOT_0]
ShotName=SHOT_0
ZPosition=0
RingLight_Enabled=True
RingLight_Brightness=80
; ... 조명 8개 필드
FAICount=1

[SHOT_0_CAM]
; CameraSlaveParam 기존 필드

[SHOT_0_FAI_0]
FAIName=A1
MeasurementCount=2

[SHOT_0_FAI_0_MEAS_0]
Type=PointToLineDistance         ← factory 키
DatumRef=B
NominalValue=20.347
TolerancePlus=0.015
ToleranceMinus=-0.015
PointRoi_Row=1234
; ...

[SHOT_0_FAI_0_MEAS_1]
Type=EdgePairDistance
DatumRef=C
; ...
```

**기존 포맷 감지:** `iniFile.ContainsSection("FORMAT")` 없음 + `iniFile.ContainsSection("SHOTS")` 있음 → 구버전 → "새 레시피로 작성하세요" 메시지 후 return false.

### ParamBase 직렬화 — MeasurementBase 다형성

ParamBase.Save/Load는 리플렉션 기반이며 `Int32`, `Double`, `String`, `Boolean`, `Rect`, `Line`, `Circle` 타입을 자동 처리한다. [VERIFIED: 코드베이스 직접 열람]

MeasurementBase와 파생 클래스가 ParamBase를 상속하면 각 파생 클래스의 ROI 필드(Double, Int32, String)가 자동 직렬화된다. 단, 다형성(어떤 파생 클래스인지)은 자동 처리되지 않으므로 InspectionRecipeManager에서 수동으로 `Type=` 필드를 읽고 `MeasurementFactory.Create(typeName)`으로 인스턴스를 생성한 뒤 `instance.Load(iniFile, section)`을 호출해야 한다.

### Pattern 1: MeasurementBase 추상 클래스

```csharp
// Source: 코드베이스 DatumConfig.cs + ParamBase.cs 패턴 적용
// WPF_Example/Halcon/Algorithms/ 또는 WPF_Example/Custom/Sequence/Inspection/ 위치 권장
public abstract class MeasurementBase : ParamBase
{
    [Category("Measurement|Reference")]
    public string DatumRef { get; set; } = "";  // "" = 무보정 (D-10 호환)

    [Category("Measurement|Tolerance")]
    public double NominalValue { get; set; }
    public double TolerancePlus { get; set; }
    public double ToleranceMinus { get; set; }

    [Browsable(false)]
    public double LastMeasuredValue { get; set; }

    [Browsable(false)]
    public bool LastJudgement { get; set; }  // true = OK

    [Browsable(false)]
    public string MeasurementName { get; set; }  // 표시용

    public MeasurementBase(object owner) : base(owner) { }

    // 파생 클래스 구현 필수
    public abstract bool TryExecute(
        HImage image,
        HomMat2D datumTransform,    // identity이면 무보정
        double pixelResolution,     // mm/pixel
        out double resultMm,
        out string error);

    // 공차 판정
    public bool EvaluateJudgement(double valueMm)
    {
        LastMeasuredValue = valueMm;
        double lower = NominalValue + ToleranceMinus;  // ToleranceMinus는 음수
        double upper = NominalValue + TolerancePlus;
        LastJudgement = (valueMm >= lower) && (valueMm <= upper);
        return LastJudgement;
    }
}
```

### Pattern 2: VisionAlgorithmService — Halcon 연산자 선택

각 알고리즘에 권장 Halcon 연산자:

| 알고리즘 | Halcon 연산자 | 비고 |
|---------|-------------|------|
| 직선 피팅 | `HOperatorSet.FitLineContourXld` | MeasurementAlgorithm.cs에서 이미 사용 중 |
| 에지 검출 | `HOperatorSet.MeasurePos` + 샘플 스트립 | FAIEdgeMeasurementService 패턴 그대로 |
| 두 직선 교점 | `HOperatorSet.IntersectionLl` | DatumFindingService에서 이미 사용 중 |
| 원 검출 | `HOperatorSet.FitCircleContourXld` 또는 `HOperatorSet.HoughCircles` + `FitCircle` | 권장: FitCircleContourXld (서브픽셀 정밀도) |
| 점-선 거리 | 수학 계산 (cross product 공식) | Halcon에 DistancePl 연산자 없음 → 직접 구현 |
| 점-점 거리 | 유클리드 거리 공식 | |
| 두 직선 각도 | `HOperatorSet.AngleLl` 또는 수학 atan2 | AngleLl은 두 라인 벡터 각도 반환 |
| AffineTransPoint2d | `HOperatorSet.AffineTransPoint2d` | Datum transform 적용 — FAIEdgeMeasurementService에서 이미 사용 |
| HomMat2d Identity | `HOperatorSet.HomMat2dIdentity` | DatumFindingService에서 이미 사용 |

[VERIFIED: 코드베이스 직접 열람 — FAIEdgeMeasurementService.cs, DatumFindingService.cs, MeasurementAlgorithm.cs]

### Pattern 3: Factory 패턴 (C# 7.2 호환)

```csharp
// C# 7.2: switch expression 불가 → switch 문 사용
public static class MeasurementFactory
{
    public static MeasurementBase Create(string typeName, object owner)
    {
        switch (typeName)
        {
            case "EdgePairDistance":
                return new EdgePairDistanceMeasurement(owner);
            case "PointToLineDistance":
                return new PointToLineDistanceMeasurement(owner);
            case "PointToPointDistance":
                return new PointToPointDistanceMeasurement(owner);
            case "LineToLineAngle":
                return new LineToLineAngleMeasurement(owner);
            case "CircleDiameter":
                return new CircleDiameterMeasurement(owner);
            case "LineToLineDistance":
                return new LineToLineDistanceMeasurement(owner);
            default:
                return null;
        }
    }
}
```

### Pattern 4: UI 트리 Datum 노드 이동 (Sequence 레벨)

현재 `CreateSequenceNode`에서 Datum 노드는 Action(Shot) 하위에 있다 (Phase 4 구현). Phase 6에서는 Sequence 하위, Action과 같은 레벨 형제로 변경한다.

```csharp
// 현재 (Phase 4): actNode.Children.Add(datumNode)
// Phase 6 이후: seqNode.Children.Add(datumNode) — Action 노드 추가 전에 먼저 삽입

// seqNode 생성 후:
if (seq is InspectionSequence inspSeq) {
    foreach (var datum in inspSeq.DatumConfigs) {
        var datumNode = new CompositeNode {
            Name = datum.DatumName ?? "Datum",
            NodeType = ENodeType.Datum,
            ParamData = datum,
            SequenceName = seq.Name,
            SequenceID = seq.ID
        };
        seqNode.Children.Add(datumNode);
    }
}
// 이후 Action 노드 추가
```

### Anti-Patterns to Avoid

- **SequenceBase.Name 직접 수정 시도:** `private set`이므로 컴파일 에러. InspectionSequence에 `DisplayName` 별도 추가.
- **MeasurementBase에서 List<T> 또는 복합 타입 ParamBase 저장:** ParamBase.Save/Load는 List<T> 타입을 처리하지 않음. Measurement 개수는 InspectionRecipeManager에서 수동 관리.
- **FAIEdgeMeasurementService 삭제 시도:** D-19에서 래핑 재사용 결정. 삭제하면 기존 Phase 3/4/5 테스트 이미지 검증도 깨짐.
- **HomMat2D vs HTuple 혼용:** DatumFindingService는 `HTuple` 타입으로 transform을 반환한다. MeasurementBase TryExecute 시그니처에서 `HomMat2D` (HalconDotNet struct)를 쓰거나 `HTuple`을 쓰거나 일관성 유지 필요. HTuple로 통일하는 것이 기존 코드와 호환성 높음.
- **PropertyGrid에 Browsable(false)가 없는 LastMeasuredValue/LastJudgement:** 결과(휘발성) 필드는 반드시 `[Browsable(false)]` 추가 — 기존 FAIConfig 패턴 그대로.

---

## Don't Hand-Roll

| 문제 | 직접 구현 금지 | 대신 사용 | 이유 |
|------|-------------|----------|------|
| INI 직렬화 | 커스텀 파일 파서 | `ParamBase.Save/Load` + `IniFile` | 기존 프레임워크 패턴 |
| 에지 라인 검출 | 커스텀 픽셀 스캔 | `HOperatorSet.MeasurePos` + `FitLineContourXld` | MeasurementAlgorithm 기존 패턴 |
| Datum transform 행렬 | 직접 행렬 곱 | `HOperatorSet.AffineTransPoint2d` | 기존 FAIEdgeMeasurementService 패턴 |
| 원 검출 | 허프 변환 직접 구현 | `HOperatorSet.FitCircleContourXld` | 서브픽셀 정밀도, 기존 Halcon 패턴 |
| PropertyGrid 표시 | 커스텀 편집기 | `PropertyTools` `[Category]`, `[Browsable]` | 기존 DatumConfig/FAIConfig 패턴 |
| 두 직선 교점 | 연립방정식 코드 | `HOperatorSet.IntersectionLl` | DatumFindingService 기존 패턴 |

---

## Common Pitfalls

### Pitfall 1: SequenceBase.Name은 편집 불가
**What goes wrong:** Sequence 이름 편집을 위해 `SequenceBase.Name = ...`을 시도하면 컴파일 에러 발생.
**Why it happens:** `Name { get; private set; }` — 프레임워크 기반 클래스를 보호하기 위해 설계됨.
**How to avoid:** `InspectionSequence`에 `public string DisplayName { get; set; }` 별도 추가. UI 트리와 레시피 저장 시 `DisplayName` 사용. `Name`은 ESequence enum에서 오는 시스템 식별자로 유지.
**Warning signs:** 빌드 에러 `CS0272 — The property or indexer 'SequenceBase.Name' cannot be used in this context because the set accessor is inaccessible`.

### Pitfall 2: ParamBase.Load는 List<MeasurementBase>를 자동 처리하지 않음
**What goes wrong:** FAIConfig에 `List<MeasurementBase> Measurements` 프로퍼티를 추가하고 `fai.Load(iniFile, section)`을 호출하면 Measurements 리스트가 로드되지 않음.
**Why it happens:** ParamBase.Load의 switch는 `Int32`, `Double`, `String`, `Boolean`, `Rect`, `Line`, `Circle`만 처리. `List<MeasurementBase>` 타입은 `default: break`로 무시됨.
**How to avoid:** `FAIConfig.Load`를 override하거나, InspectionRecipeManager에서 FAI 로드 후 Measurement 개수(`MeasurementCount`)를 읽고 루프에서 `MeasurementFactory.Create` → `meas.Load`를 수동 호출.
**Warning signs:** 레시피 로드 후 FAI.Measurements 리스트가 비어있음.

### Pitfall 3: DatumConfig owner 파라미터 변경
**What goes wrong:** Phase 4에서 `DatumConfig(owner)`의 owner는 `ShotConfig`였다. Phase 6에서 Sequence 레벨로 이동하면 owner가 `InspectionSequence`가 된다.
**Why it happens:** `ParamBase.OwnerName`이 owner 타입에 따라 분기함 (ActionBase이면 Action 이름, SequenceBase이면 Sequence 이름).
**How to avoid:** `InspectionSequence.DatumConfigs`에 DatumConfig를 추가할 때 `new DatumConfig(this)`로 생성 (this = InspectionSequence). InspectionRecipeManager에서 DatumConfig 생성 시에도 올바른 owner 전달.
**Warning signs:** `DatumConfig.OwnerName`이 null 또는 잘못된 이름 반환.

### Pitfall 4: Action_FAIMeasurement에서 Datum을 꺼내는 방법
**What goes wrong:** `Action_FAIMeasurement`에서 Datum을 `ShotParam.Datum`으로 직접 접근하던 코드를 Phase 6에서 제거하면, parentSequence 참조 방법이 필요하다.
**Why it happens:** `ActionBase`에는 `ParentID (ESequence)` 프로퍼티가 있지만 직접 부모 Sequence 인스턴스 참조는 없다.
**How to avoid:** `SystemHandler.Handle.Sequences[Param.Parent]`로 부모 Sequence를 꺼내거나, `ActionBase.Param.Parent`가 SequenceBase를 참조하므로 `Param.Parent as InspectionSequence`로 캐스팅. 단, `Param.Parent`는 `AddAction()` 호출 시 설정됨을 확인.
**Warning signs:** `Param.Parent`가 null인 경우 → OnLoad에서 null 체크 필수.

### Pitfall 5: 조명 필드 INI 저장 시 기존 CameraSlaveParam 필드와 섹션 혼재
**What goes wrong:** ShotConfig가 CameraSlaveParam을 상속하므로 `shot.Save(saveFile, shotSection + "_CAM")`이 CameraSlaveParam의 모든 필드를 저장한다. 새 조명 필드(bool/int)도 같은 ParamBase.Save에서 처리되어 동일 섹션에 저장됨.
**Why it happens:** ParamBase.Save는 상속 포함 모든 public 프로퍼티를 저장. 새 필드도 자동 포함.
**How to avoid:** 조명 필드를 ShotConfig에 추가하면 자동으로 `_CAM` 섹션에 저장/로드됨 — 추가 코드 불필요. 단, 기존 레시피에 조명 필드가 없으면 Load 시 기본값(false, 0)이 적용됨 (ParamBase.Load try/catch 처리).
**Warning signs:** 없음 — 자동 처리됨.

### Pitfall 6: FAIResultRow가 FAIConfig를 직접 참조
**What goes wrong:** Phase 6에서 결과 테이블이 MeasurementBase 단위로 변경되면, `FAIResultRow(FAIConfig fai)` 생성자를 그대로 쓸 수 없다.
**Why it happens:** `FAIResultRow._fai`는 `FAIConfig`를 직접 참조. `MeasuredValue`, `IsPass` 등이 FAIConfig 필드에서 옴.
**How to avoid:** `MeasurementResultRow : Observable` 클래스 신규 생성 (MeasurementBase를 래핑). `InspectionViewModel`에 `OnFAISelected`를 업데이트하여 FAI.Measurements 루프에서 MeasurementResultRow를 생성. FAIResultRow는 기존 호환용으로 유지하거나 제거.

### Pitfall 7: HTuple vs HomMat2D 타입 불일치
**What goes wrong:** DatumFindingService.TryFindDatum은 `out HTuple transform`을 반환한다. MeasurementBase.TryExecute 파라미터 타입을 `HomMat2D`로 선언하면 타입 불일치 발생.
**Why it happens:** Halcon .NET API에서 hom_mat2d는 HTuple로도, HomMat2D struct로도 표현 가능.
**How to avoid:** TryExecute 파라미터를 `HTuple datumTransform`으로 통일 (기존 FAIEdgeMeasurementService 패턴 그대로). 또는 InspectionSequence._datumTransforms를 `Dictionary<string, HTuple>`로 선언.

---

## Code Examples

### Sequence 레벨 DatumConfigs 추가 (InspectionSequence)
```csharp
// WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
// Source: 코드베이스 직접 분석
public class InspectionSequence : SequenceBase {
    // ...기존 필드...

    //260410 hbk Phase 6: Sequence 레벨 Datum (D-04, RC-01)
    public string DisplayName { get; set; }  // 사용자 편집 가능 이름 (D-01)

    [Browsable(false)]  // PropertyGrid에서 숨김 — 편집은 트리 노드 인라인에서
    public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

    // 런타임 전용 — INI 저장 안 함
    private Dictionary<string, HTuple> _datumTransforms = new Dictionary<string, HTuple>();

    public DatumConfig AddDatum(string name = null) {
        string datumName = name ?? $"Datum_{DatumConfigs.Count + 1}";
        var datum = new DatumConfig(this) { DatumName = datumName };
        DatumConfigs.Add(datum);
        return datum;
    }

    public bool RemoveDatum(int index) {
        if (index < 0 || index >= DatumConfigs.Count) return false;
        DatumConfigs.RemoveAt(index);
        return true;
    }

    public bool TryRunDatumPhase(HImage image, out string error) {
        error = null;
        _datumTransforms.Clear();
        var datumSvc = new DatumFindingService();
        foreach (var datum in DatumConfigs) {
            HTuple transform;
            string datumError;
            if (!datumSvc.TryFindDatum(image, datum, out transform, out datumError)) {
                error = $"Datum '{datum.DatumName}': {datumError}";
                return false;
            }
            _datumTransforms[datum.DatumName] = transform;
        }
        return true;
    }

    public bool TryGetDatumTransform(string datumRef, out HTuple transform) {
        if (string.IsNullOrEmpty(datumRef)) {
            HOperatorSet.HomMat2dIdentity(out transform);
            return true;
        }
        return _datumTransforms.TryGetValue(datumRef, out transform);
    }
}
```

### MeasurementBase.TryExecute 호출 흐름 (Action_FAIMeasurement)
```csharp
// Source: 코드베이스 Action_FAIMeasurement.cs 분석, Phase 6 재설계
// EStep에 DatumPhase 추가
case EStep.DatumPhase:
    var parentSeq = Param.Parent as InspectionSequence;
    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
        using (var datumImage = GetDatumImage(parentSeq)) {
            string datumError;
            if (!parentSeq.TryRunDatumPhase(datumImage, out datumError)) {
                Logging.PrintLog((int)ELogType.Error, datumError);
                pMyContext.AllPass = false;
                Step = (int)EStep.End;
                break;
            }
        }
    }
    Step = (int)EStep.Measure;
    break;

case EStep.Measure:
    if (ShotParam != null) {
        bool allPass = true;
        using (var image = ShotParam.GetImage()) {
            if (image != null) {
                foreach (var fai in ShotParam.FAIList) {
                    foreach (var meas in fai.Measurements) {
                        HTuple transform;
                        parentSeq.TryGetDatumTransform(meas.DatumRef, out transform);
                        double resultMm;
                        string measError;
                        if (meas.TryExecute(image, transform, ShotParam.PixelResolution, out resultMm, out measError)) {
                            meas.EvaluateJudgement(resultMm);
                        } else {
                            meas.LastJudgement = false;
                        }
                        if (!meas.LastJudgement) allPass = false;
                    }
                }
            }
        }
        pMyContext.AllPass = allPass;
    }
    Step = (int)EStep.End;
    break;
```

### DatumConfig 변경사항 (DatumName + ImageSourceMode)
```csharp
// WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
// Source: 기존 DatumConfig.cs 직접 열람

public enum EImageSourceMode {
    Dedicated,       // 별도 캡처
    ReuseFromShot    // 지정 Shot 이미지 재사용
}

// DatumConfig 클래스에 추가:
//260410 hbk Phase 6: DatumName + ImageSourceMode (D-06, D-07)
[Category("Datum|Identity")]
public string DatumName { get; set; } = "Datum_1";

[Category("Datum|ImageSource")]
public string ImageSourceMode { get; set; } = "Dedicated";  // string으로 INI 저장 (enum은 ParamBase 미지원)
public string ReuseFromShotName { get; set; } = "";         // ReuseFromShot 모드 시 Shot 이름
```

### INI 포맷 버전 감지 로직 (InspectionRecipeManager)
```csharp
// Source: 기존 HasNewFormatData + Phase 6 확장
// 기존 포맷: [SHOTS] 있음, [FORMAT] 없음
// Phase 6 포맷: [FORMAT] Version=6 있음

public enum ERecipeFormatVersion {
    Unknown = 0,
    Phase5 = 5,   // [SHOTS] 있음, [FORMAT] 없음
    Phase6 = 6,   // [FORMAT] Version=6
}

private ERecipeFormatVersion DetectFormatVersion(IniFile iniFile) {
    if (iniFile.ContainsSection("FORMAT")) {
        int ver = iniFile["FORMAT"]["Version"].ToInt();
        if (ver >= 6) return ERecipeFormatVersion.Phase6;
    }
    if (iniFile.ContainsSection("SHOTS")) {
        return ERecipeFormatVersion.Phase5;
    }
    return ERecipeFormatVersion.Unknown;
}

public bool Load(IniFile loadFile) {
    var version = DetectFormatVersion(loadFile);
    if (version == ERecipeFormatVersion.Phase5 || version == ERecipeFormatVersion.Unknown) {
        // D-22: 기존 포맷 거부
        CustomMessageBox.Show("안내",
            "이 레시피는 이전 포맷(Phase 1~5)입니다.\n새 레시피로 작성하세요.",
            System.Windows.MessageBoxImage.Information);
        return false;
    }
    return LoadPhase6Format(loadFile);
}
```

---

## Critical Investigation: Sequence 이름 편집 지원 여부 확인

**질문 (Claude's Discretion):** Sequence 이름 편집이 기존 UI에서 이미 지원되는지?

**조사 결과:** [VERIFIED: 코드베이스 직접 열람]
- `SequenceBase.Name { get; private set; }` — `private set`. 외부에서 변경 불가.
- 기존 UI 트리(`InspectionListView`, `InspectionListViewModel`)에서 Sequence 이름 편집 UI는 없음.
- 결론: **지원되지 않음**. Phase 6에서 `InspectionSequence.DisplayName` 프로퍼티를 새로 추가하고, 트리 노드의 `Name` 표시와 INI 저장에서 `DisplayName`을 사용해야 한다.

**구현 방법 권장:**
- `InspectionListViewModel.CreateSequenceNode`에서 `seqNode.Name = (seq as InspectionSequence)?.DisplayName ?? seq.Name`으로 표시.
- INI `[FIXTURE]` 섹션에 `DisplayName` 저장.
- 트리 노드 선택 후 이름 편집은 `InspectionListView`의 ParamEditor(PropertyGrid)에서 DisplayName을 편집하거나, 별도 인라인 TextBox 추가.

---

## Runtime State Inventory

이 Phase는 기존 코드 구조 변경(refactor)을 포함하므로 런타임 상태를 점검한다.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | INI 레시피 파일 (기존 Phase 1~5 포맷, `[SHOTS]` 섹션 기반) | D-22: 기존 포맷 로드 시 안내 메시지 표시 후 return false. 마이그레이션 없음. |
| Live service config | 없음 — 외부 서비스 없음 | 없음 |
| OS-registered state | 없음 | 없음 |
| Secrets/env vars | 없음 | 없음 |
| Build artifacts | `bin/x64/Debug/` 컴파일 결과물 — ShotConfig.Datum 필드 제거 시 기존 INI 로드 코드에서 컴파일 에러 가능 | ShotConfig.Datum 제거 전 InspectionRecipeManager.Load에서 해당 섹션 로드 코드 먼저 제거. |

**ShotConfig.Datum 제거 영향 범위:**
- `InspectionRecipeManager.AddShot()`: `shot.Datum = new DatumConfig(shot)` 라인 제거
- `InspectionRecipeManager.Save()`: `SHOT_{s}_DATUM` 섹션 저장 코드 제거
- `InspectionRecipeManager.Load()`: `SHOT_{s}_DATUM` 섹션 로드 코드 제거
- `Action_FAIMeasurement.Run()` EStep.Measure: `shot.Datum` 접근 코드 → Sequence 레벨로 교체
- `ShotConfig.ClearAllResults()`: `Datum.CurrentTransform/LastFindSucceeded` 초기화 코드 제거
- `InspectionListViewModel.CreateSequenceNode()`: Action 하위 Datum 노드 생성 코드 → Sequence 하위로 이동

---

## Environment Availability

Step 2.6: 이 Phase는 기존 코드베이스 변경만 포함. 외부 의존성 추가 없음.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| HALCON 24.11 | VisionAlgorithmService (FitCircleContourXld 등) | 프로젝트 .csproj에 등록됨 | 24.11 Progress Steady | SIMUL_MODE (알고리즘 실행 없이 통과) |
| MSBuild 15.0 | 빌드 | 기존 프로젝트 구성 유지 | 15.0 | 없음 |

모든 새 Halcon 연산자(`FitCircleContourXld`, `AngleLl` 등)는 HALCON 24.11에 포함되어 있다. [ASSUMED — Halcon 24.11 릴리즈 노트 직접 확인 안 함. 24.11은 2024년 릴리즈이며 해당 연산자들은 Halcon 12 이상에서 지원되므로 LOW risk.]

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음 (프로젝트에 xUnit/NUnit/MSTest 없음) |
| Config file | 없음 |
| Quick run command | `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` (빌드 성공 = 기본 검증) |
| Full suite command | 빌드 + SIMUL_MODE 수동 실행 |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RC-01 | InspectionSequence.DatumConfigs 소유 + DisplayName 편집 | 단위(수동) | 빌드 성공 확인 | ❌ Wave 0 |
| RC-02 | DatumConfig Sequence 레벨 승격, Multi-Datum 동작 | 통합(수동) | SIMUL_MODE 실행 후 Datum 단계 로그 확인 | ❌ Wave 0 |
| RC-03 | MeasurementBase 6종 TryExecute() — 테스트 이미지 입력 시 올바른 결과 반환 | 단위(수동) | SIMUL_MODE + 테스트 이미지 + 로그 확인 | ❌ Wave 0 |
| RC-04 | ShotConfig 조명 필드 INI 저장/로드 라운드트립 | 단위(수동) | 레시피 저장 → 재로드 → PropertyGrid에서 값 확인 | ❌ Wave 0 |
| RC-05 | 새 포맷 저장/로드 동작, 기존 포맷 안내 메시지 | 통합(수동) | 기존 INI 로드 시도 → 안내 메시지 다이얼로그 확인 | ❌ Wave 0 |
| RC-06 | UI 트리 Sequence > Datum + Shot > FAI > Measurement 탐색 가능, 결과 테이블 Measurement 단위 | UI(수동) | 앱 실행 + 레시피 로드 + 트리 탐색 | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
- **Per wave merge:** 빌드 성공 + SIMUL_MODE 실행 후 각 RC 요구사항 수동 점검
- **Phase gate:** 모든 RC 요구사항 수동 확인 완료 후 `/gsd-verify-work`

### Wave 0 Gaps
- [ ] 공식 테스트 프레임워크 없음 — 빌드 성공을 자동화 게이트로 사용
- [ ] RC-03 검증용 테스트 이미지: `SIMUL_MODE`에서 `SimulImagePath`로 지정 가능 (기존 패턴)
- [ ] 기존 INI 레시피 샘플 파일 — 기존 포맷 거부 로직 검증용으로 Phase 1~5 레시피 파일이 있어야 함

*(기존 테스트 인프라가 없으므로 Wave 0 갭이 많음. 빌드 성공 + SIMUL_MODE 수동 검증으로 대체)*

---

## Security Domain

이 Phase는 로컬 파일 시스템 INI 읽기/쓰기만 추가. 네트워크, 인증, 암호화 없음. ASVS 검토 생략.

---

## State of the Art

| 이전 방식 | Phase 6 방식 | 변경 이유 |
|---------|------------|---------|
| ShotConfig.Datum (단일) | InspectionSequence.DatumConfigs (List) | Multi-Datum 지원 (D-04) |
| FAIConfig에 단일 에지 파라미터 | FAIConfig.List<MeasurementBase> | 복수 알고리즘 지원 (D-20) |
| Action 하위 Datum 노드 | Sequence 하위 Datum 노드 (Action과 형제) | 논리적 계층 반영 (D-25) |
| INI [SHOTS] 포맷 | INI [FORMAT]+[FIXTURE]+[SHOTS] 포맷 | Sequence/Datum 포함 확장 (D-22) |
| 에지 페어 거리만 측정 | 6종 알고리즘 (점-선, 점-점, 각도, 원, 선-선 거리) | RC 제품 75개 FAI 지원 |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | HOperatorSet.FitCircleContourXld가 HALCON 24.11에 포함됨 | Standard Stack, Code Examples | CircleDiameterMeasurement 구현 시 대체 연산자 필요 (HoughCircles + FitCircle 조합으로 대체 가능 — LOW risk) |
| A2 | HOperatorSet.AngleLl이 HALCON 24.11에 포함됨 | Code Examples | LineToLineAngleMeasurement에서 atan2 수동 계산으로 대체 가능 — LOW risk |
| A3 | ParamBase.Save/Load는 상속 클래스의 모든 public 프로퍼티를 처리함 (BindingFlags.Instance 확인) | Architecture Patterns | 조명 필드 자동 저장/로드에 영향. ParamBase.Save 코드를 직접 열람하여 확인함 → VERIFIED |

---

## Open Questions (RESOLVED)

1. **RESOLVED: Datum Dedicated 모드의 캡처 조건 저장 위치**
   - Decision: DatumConfig 내부에 단순 필드(DedicatedZPosition: double, DedicatedLightLevel: int) 추가. INI 구조 단순화. 조명 하드웨어 연동은 Phase 6 범위 외이므로 필드 저장만.
   - Adopted in: Plan 02 Task 1

2. **RESOLVED: FAIConfig 기존 에지 파라미터 처리**
   - Decision: D-22(기존 포맷 거부)에 따라 기존 에지 측정 필드는 `[Browsable(false)]`로 숨기고 유지. `EdgePairDistanceMeasurement`에서 내부적으로 재사용.
   - Adopted in: Plan 01 Task 2

3. **RESOLVED: Action_FAIMeasurement의 Datum 이미지 취득 시점**
   - Decision: 첫 번째 Shot Action의 EStep.Datum 단계에서 InspectionSequence.TryRunDatumPhase() 호출. 이미 실행된 경우 캐시된 _datumTransforms 재사용.
   - Adopted in: Plan 03 Task 1

---

## Sources

### Primary (HIGH confidence)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — DatumConfig 소유 현황, 조명 필드 추가 위치
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 기존 필드, DatumName 추가 대상
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — 기존 에지 파라미터, List<MeasurementBase> 추가 위치
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — INI 포맷 현황, Phase 6 재설계 대상
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — Datum 실행 흐름 현황
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — Fixture 역할, DatumConfigs 추가 위치
- `WPF_Example/Sequence/Param/ParamBase.cs` — Save/Load 직렬화 타입 목록
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` — ShotConfig 기반, 기존 필드 목록
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` — Name private set 확인
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` — 에지 측정 패턴, HTuple transform 적용 방식
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — TryFindDatum, IntersectionLl 사용 패턴
- `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs` — FitLineContourXld, MeasurePos 패턴
- `WPF_Example/UI/ControlItem/Node.cs` — ENodeType 현황
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — 트리 생성 로직, Datum 노드 위치
- `WPF_Example/UI/ViewModel/FAIResultRow.cs` — 결과 행 현황
- `WPF_Example/UI/ViewModel/InspectionViewModel.cs` — 결과 테이블 구성 현황

### Tertiary (LOW confidence — needs validation)
- A1: HOperatorSet.FitCircleContourXld HALCON 24.11 포함 여부 — 직접 API 문서 확인 안 함
- A2: HOperatorSet.AngleLl HALCON 24.11 포함 여부

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 모든 핵심 클래스 직접 열람
- Architecture: HIGH — 기존 코드 패턴 분석 기반, 변경점 명확
- Pitfalls: HIGH — 실제 코드에서 발견한 구체적 제약사항

**Research date:** 2026-04-10
**Valid until:** 2026-05-10 (코드베이스 기반, 안정적)
