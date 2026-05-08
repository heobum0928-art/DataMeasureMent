# Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-08
**Phase:** 28-fai-circlediameter-datum-circle
**Areas discussed:** 호출 경로/Polarity helper, Polar 파라미터 + Datum 동등성, PropertyGrid 카테고리·빈값 표시, EdgePolarity 우선순위

---

## Gray area selection

| Option | Selected |
|---|---|
| 호출 경로/Polarity helper | ✓ |
| Polar 파라미터 + Datum 동등성 | ✓ |
| PropertyGrid 카테고리·빈값 표시 | ✓ |
| EdgePolarity 우선순위 | ✓ |

**User's choice:** 4개 영역 모두 선택.

---

## 호출 경로/Polarity helper

### Q1. FAI에서 TryFindCircleByPolarSampling을 어떻게 호출할지?

| Option | Description | Selected |
|---|---|---|
| 직접 호출 (Recommended) | TryFindCircleByPolarSampling 이미 public. CircleDiameterMeasurement.TryExecute에서 직접 호출. 신규 파일 없이 최소 변경. | ✓ |
| 새 공유 wrapper 클래스 | FaiPolarCircleHelper static — FAI/Datum 양쪽에서 호출. YAGNI 위험. | |
| VisionAlgorithmService에 wrapper 메서드 추가 | TryFindCircleByPolar (RadialDirection 직접 받음). 레이어 경계 흐림. | |

**User's choice:** 직접 호출 (Recommended)

### Q2. Polarity 매핑("Inward"→"positive" / "Outward"→"negative") 코드는 어디에 둘지?

| Option | Description | Selected |
|---|---|---|
| EdgeOptionLists에 static 메서드 (Recommended) | MapRadialDirectionToHalconPolarity. 단일 소스 RadialDirections와 같은 곳에 매핑 함수 — 일관성. DatumFindingService 두 곳 + FAI 한 곳 모두 helper 호출로 교체 (3중 inline 제거). | ✓ |
| DatumFindingService에 private static helper | Datum 전용. FAI는 또 inline 동일 코드 → 중복 미해소. | |
| FAI 측 inline 처리 (재논의) | 3중 inline 재구현. 디버그 누적. | |

**User's choice:** EdgeOptionLists에 static 메서드 (Recommended)

---

## Polar 파라미터 + Datum 동등성

### Q3. FAI 측 PolarStepDeg/RectL1Ratio/RectL2Ratio/EdgeSelection default 값의 출처는?

| Option | Description | Selected |
|---|---|---|
| EdgeOptionLists에 const 추가 (Recommended) | FaiCirclePolarStepDeg=10.0, FaiCircleRectL1Ratio=0.02, FaiCircleRectL2Ratio=0.02, FaiCircleEdgeSelection="First" 상수. Datum default와 동일 → REQ-28-03 자동 보장. | ✓ |
| FAI 내부 hardcode | CircleDiameterMeasurement private const. Datum 변경 시 동기화 누락 위험. | |
| 0/sentinel 전달 (clamp 의존) | TryFindCircleByPolarSampling 내부 sanity clamp 의존. 단점: clamp 0.05 ≠ Datum default 0.02 → 동등성 깨짐 ⚠️. | |

**User's choice:** EdgeOptionLists에 const 추가 (Recommended)

### Q4. REQ-28-03 동등성 검증 범위는?

| Option | Description | Selected |
|---|---|---|
| Default 일치 + 사용자 UAT (Recommended) | Default가 Datum과 동일하므로 동일 ROI/Sigma/Threshold/RadialDirection 설정 시 결정적으로 동등. AC-4 SIMUL_MODE 수동 UAT 단일. | ✓ |
| Default 일치 + 자동 테스트 | xUnit 등 신규 도입 필요 — 스코프 초과. | |
| Default 일치 + 런타임 assert | DEBUG 빌드 어설션. 런타임 오버헤드 + Halcon 호출 중복. | |

**User's choice:** Default 일치 + 사용자 UAT (Recommended)

---

## PropertyGrid 카테고리·빈값 표시

### Q5. Circle_RadialDirection 필드는 어느 [Category]에 넣을까?

| Option | Description | Selected |
|---|---|---|
| [Category("Edge")] (Recommended) | EdgeThreshold/Sigma/EdgePolarity와 같은 그룹. polarity와 RadialDirection은 의미적으로 에지 검출 방향 파라미터. AC-1 충족. | ✓ |
| [Category("Circle\|ROI")] | Circle_Row/Col/Radius 그룹. 의미군 다름 (기하 vs 검출). | |
| [Category("Circle\|Edge")] 신규 | Circle 계열 에지 전용. 기존 [Category("Edge")] 도 건드려야 함 → SPEC out-of-scope 위반. | |

**User's choice:** [Category("Edge")] (Recommended)

### Q6. 콤보 UI 옵션 구성과 빈 값 처리는?

| Option | Description | Selected |
|---|---|---|
| RadialDirections (2옵션) 그대로 (Recommended) | EdgeOptionLists.RadialDirections = {Inward, Outward} 단일 소스. SPEC REQ-28-01 "단일 소스" 충족. 빈 값은 INI default/코드 default로만 — 콤보에서 직접 복귀 불가, INI 수동 편집 필요. | ✓ |
| FAI 전용 List 3옵션 | EdgeOptionLists.FaiRadialDirections = {"", "Inward", "Outward"}. 사용자가 자유롭게 "선택 안 함"으로 복귀 가능. 단일 소스 해석 다름. | |
| "(선택 안 함)" 라벨 + 매핑 | 콤보 List = {"(선택 안 함)", "Inward", "Outward"}. setter/getter 매핑 추가. 복잡도 증가. | |

**User's choice:** RadialDirections (2옵션) 그대로 (Recommended)

---

## EdgePolarity 우선순위

### Q7. RadialDirection 명시 시 EdgePolarity 필드는 어떻게 처리할까?

| Option | Description | Selected |
|---|---|---|
| RadialDirection이 이김 (Recommended) | polar 경로 polarity = MapRadialDirectionToHalconPolarity 단일 원천. EdgePolarity는 polar 호출에서 무시. RadialDirection 빈 값 시 → fit 경로 + EdgePolarity. Datum CTH 패턴과 동일 → REQ-28-03 동등성 자동 만족. | ✓ |
| EdgePolarity가 이김 | RadialDirection 명시 시도 EdgePolarity 사용. Datum 동등성 깨짐. | |
| EdgePolarity 필드 이름만 숨기기 | RadialDirection 명시 시 EdgePolarity hide. REQ-28-05 (ICustomTypeDescriptor 미구현·정적 노출) 위반. | |

**User's choice:** RadialDirection이 이김 (Recommended)

---

## Claude's Discretion

- D-03 helper 호출 시 `using` 절 vs fully-qualified name — DatumFindingService.cs 의 기존 using 일관성에 따라 planner/executor 결정.
- Circle_RadialDirection 콤보 표시 순서 (선언 순서 그대로) — 별도 attribute 강제 안 함.

## Deferred Ideas

- 사용자가 콤보에서 직접 빈 값으로 복귀하는 UX — v1.2 backlog
- 다른 FAI Measurement 클래스에 Datum 알고리즘 통합 — Phase 28 out of scope, v1.2 일반화 후보
- FAI 측 PolarStepDeg/Ratio 사용자 노출 — D-04 const가 못 커버할 시 v1.2 재검토
- 자동화된 FAI ↔ Datum 동등성 회귀 테스트 — v1.2 에서 SIMUL_MODE 기반 unit-test 인프라 도입 시 검토
