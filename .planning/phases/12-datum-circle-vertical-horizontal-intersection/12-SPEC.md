# Phase 12: datum-circle-vertical-horizontal-intersection — Specification

**Created:** 2026-04-23
**Ambiguity score:** 0.155 (gate: ≤ 0.20)
**Requirements:** 6 locked

## Goal

사용자가 Datum 노드를 티칭할 때 "Circle ROI 1개 + 수평 ROI 2개" 구성으로 **기준 원점(교점) + 기준각**을 산출해 `DatumConfig.RefOriginRow/Col/RefAngleRad`에 저장한다. 교점 정의: **원 중심에서 Y축(이미지 아래) 방향으로 내린 수직 가상선** ∩ **수평 2-ROI의 에지점을 하나의 집합으로 합쳐 FitLineContourXld로 피팅한 단일 연장선**.

## Background

현재 `DatumConfig`는 Line1/Line2 2개 ROI만 지원하며, `DatumFindingService.TryTeachDatum`은 두 직선의 교점으로 Datum을 산출한다 (Phase 4 완성, Phase 10/11에서 보강). Circle ROI 필드는 존재하지 않고, 수평 2-ROI를 하나의 연장선으로 결합하는 로직도 없다. 현실에서는 "원형 특징(구멍/핀) + 직선 에지(모서리/기준면)" 조합의 기준점이 자주 필요하며, 이를 기존 2-Line만으로는 표현할 수 없다.

추가로 사용자는 "Datum을 뽑는 방식은 앞으로 더 추가될 수 있다"고 확인했다. Phase 12는 **추가 알고리즘 1종의 구체 구현**이며, Phase 13(`datum-algorithm-extensibility`)에서 Strategy 패턴으로 추상화해 N종 확장을 가능케 하는 구조로 이어진다.

## Requirements

1. **DatumConfig 데이터 모델 확장**: Circle ROI + 수평 ROI 2개 필드를 표현할 수 있다.
   - Current: `DatumConfig`에 Line1/Line2 ROI 5필드 × 2 세트만 존재. Circle/수평2-ROI 필드 없음.
   - Target: `DatumConfig`에 `AlgorithmType` enum(`TwoLineIntersect` | `CircleTwoHorizontal`) + Circle ROI 필드(`CircleRow/CircleCol/CircleRadius`) + 수평 ROI 2개 필드(각 Row/Col/Phi/Length1/Length2) 추가. INI 미존재 시 기본값 `TwoLineIntersect`로 폴백.
   - Acceptance: 기존 Phase 4/11 INI 레시피를 로드할 때 `AlgorithmType` 미존재 → `TwoLineIntersect`로 해석되어 기존 동작이 그대로 재현된다. 새 `AlgorithmType=CircleTwoHorizontal` 필드로 저장된 INI를 다시 로드하면 Circle/수평2-ROI 필드가 왕복 보존된다.

2. **Circle 피팅 알고리즘 구현**: Circle ROI 내부에서 원의 중심과 반지름을 추정한다.
   - Current: `DatumFindingService`에 원 피팅 로직 없음. `VisionAlgorithmService.TryFindCircle`(Phase 6/11)은 FAI용 CircleDiameterMeasurement가 사용.
   - Target: `DatumFindingService`에 `CircleTwoHorizontal` 분기 추가. 내부에서 EdgesSubPix → FitCircleContourXld 호출로 `(centerRow, centerCol, radius)` 산출. 로버스트 실패 또는 residual 임계값 초과 시 명확한 error string 반환.
   - Acceptance: 유효한 원형 특징을 포함한 이미지에서 `(centerRow, centerCol, radius)`가 ±1px 이내로 산출된다. 원형 특징이 없는 이미지(평평한 영역)에서는 `TryTeachDatum`이 `false` + "Circle fit failed: …" error를 반환한다.

3. **수평 2-ROI 결합 라인 피팅**: 두 수평 ROI의 에지점을 하나의 집합으로 합쳐 단일 직선을 피팅한다.
   - Current: `DatumFindingService`는 단일 ROI 라인 피팅만 수행.
   - Target: ROI_A 및 ROI_B 각각에서 EdgesSubPix로 에지점 추출 → 두 XLD contour를 `ConcatObj`로 결합 → `FitLineContourXld` 1회 호출로 단일 라인 `(rBegin, cBegin, rEnd, cEnd)` 산출.
   - Acceptance: 같은 수평 에지에 위치한 두 ROI에서 결합 라인의 기울기 오차가 단일 ROI 결과보다 작거나 같다(노이즈 평균화 효과). 결합 후에도 에지점 개수가 임계값(예: 10) 미만이면 `false` + "Horizontal line fit failed: insufficient edges" error 반환.

4. **수직 가상선 × 수평 연장선 교점 계산**: 원 중심에서 Y 방향 수직선과 피팅된 수평 연장선의 교점을 `RefOrigin`으로 저장한다.
   - Current: 2-Line 교점 계산 로직은 존재. Circle 중심 기반 수직 가상선 × 수평선 교점 계산은 없음.
   - Target: 수직 가상선 = `col = centerCol` (모든 row). 수평선은 피팅 결과로부터 일반 직선식 `ax+by+c=0` 변환. 교점 Row = `(−a·centerCol − c) / b`. 교점 Col = `centerCol`. `RefOriginRow/Col`에 저장. `RefAngleRad`는 피팅된 수평선 방향각(Halcon `LineOrientation`)으로 저장.
   - Acceptance: 합성 이미지(원 + 수평선을 알고 있는 좌표로 배치)에서 `RefOriginRow/Col`이 이론 교점 ±1px 이내로 저장된다. 수평선이 완전한 수평(기울기 ε 미만)일 때 교점 계산은 정상 동작한다(수직선은 수직이므로 평행 문제 없음).

5. **실패 모드 감지 + 보고**: 4가지 실패 시나리오를 감지해 명시적 error로 보고한다.
   - Current: 2-Line 버전은 각 Line 피팅 실패 시 error 반환. Circle/2-ROI/방향 정합성 체크 없음.
   - Target: 아래 4개 실패 케이스를 `TryTeachDatum`이 감지하고 error string에 원인 기록:
     (a) Circle 피팅 실패 — FitCircleContourXld 실패 또는 residual 임계값 초과
     (b) 수평 라인 피팅 실패 — 결합 에지점 수 < 임계값
     (c) 교점 불정의 — 수평선 기울기 ε 미만일 때(정의상 발생하지 않으나 안전장치)
     (d) 방향 정합성 위반 — 원 중심 row > 피팅 수평선의 원 중심 col에서의 row (원이 수평선 아래에 위치)
   - Acceptance: 각 실패 케이스를 재현하는 합성 테스트 입력에서 `TryTeachDatum`이 `false` 반환 + 해당 원인 문자열이 error에 포함된다.

6. **티칭 성공 시 저장 + 검증 플래그**: 성공 시 `DatumConfig`에 결과 저장 + `LastTeachSucceeded=true`로 표시한다.
   - Current: `LastTeachSucceeded` 필드는 Phase 11에서 도입됨. 2-Line 경로에서는 사용됨.
   - Target: CircleTwoHorizontal 경로에서도 성공 시 `RefOriginRow`, `RefOriginCol`, `RefAngleRad` 저장 + `LastTeachSucceeded=true` + `IsConfigured=true` 설정. 기존 검출 라인 오버레이 필드(`Line1Detected_*`, `Line2Detected_*`)는 수평 결합선 정보로 재사용(Line1Detected=결합선, Line2Detected는 비워두거나 수직 가상선으로 기록 — 구현 단계에서 확정).
   - Acceptance: 티칭 성공 후 INI 저장 → 앱 재시작 → 레시피 로드 시 `RefOriginRow/Col/RefAngleRad`, `IsConfigured`, `AlgorithmType=CircleTwoHorizontal` 값이 왕복 보존된다.

## Boundaries

**In scope:**
- `DatumConfig`에 `AlgorithmType` enum + Circle ROI 3필드 + 수평 ROI 2세트(각 5필드) 추가
- `DatumFindingService.TryTeachDatum`의 `CircleTwoHorizontal` 분기 구현 (Circle 피팅 + 2-ROI 결합 라인 피팅 + 수직×수평 교점 + RefOrigin/RefAngle 저장)
- 4가지 실패 모드 감지 + error 문자열 반환
- 기존 `TwoLineIntersect` 경로의 하위호환(INI 미존재 필드 기본값 처리)
- 합성 입력 + SIMUL_MODE 이미지 검증
- 필요 최소 티칭 UI 연결(Phase 11 Datum 티칭 인프라에 "Circle" 드로잉 1회 + "수평 ROI" 드로잉 2회 단계 추가 — 기존 `btn_teachDatum` 흐름 재사용)

**Out of scope:**
- **Strategy 패턴 리팩터링 (EDatumAlgorithm/DatumAlgorithmBase 추출)** — Phase 13 범위. Phase 12는 `DatumFindingService`에 단순 `if (AlgorithmType == …)` 분기로 구현하고, Phase 13에서 추출한다.
- **직교 교정(orthogonality correction)** — 원에서 내린 "수직 가상선"은 엄격히 이미지 Y축이며, 수평선이 ε 이상 기울어져도 교점은 구해지나 90°는 아님. 이는 설계상 수용(보정하지 않음).
- **Phase 11 Circle ROI(CircleDiameterMeasurement)와의 공유 리팩터링** — Phase 11 Circle ROI는 FAI 측정용, Phase 12 Datum Circle은 별도 코드 경로. 공유 코드 추출은 out-of-scope.
- **런타임 TryFind 재검출** — 런타임에는 저장된 `RefOrigin/RefAngle`을 재사용하여 Transform 산출. Grab마다 Circle/수평 재검출은 수행하지 않음(Phase 13 CircleAndLine deferred 정책과 동일).
- **DatumConfig 완전 대체 / 기존 2-Line 모델 제거** — Phase 4/11 레시피와 공존해야 하므로 기존 구조를 유지하며 필드만 추가.

## Constraints

- **하위호환**: 기존 Phase 4/11 INI 레시피가 수정 없이 로드되어 기존 동작을 재현해야 함. `AlgorithmType` 미존재 시 `TwoLineIntersect`로 폴백.
- **Halcon API 일관성**: 원 피팅은 `EdgesSubPix → FitCircleContourXld`, 라인 결합 피팅은 `EdgesSubPix ×2 → ConcatObj → FitLineContourXld` — 프로젝트 표준 경로 사용(다른 SDK 도입 금지).
- **에지점 임계값**: 수평 라인 결합 후 최소 에지점 개수 임계값을 설정(plan 단계에서 구체 수치 확정). 실패 시 명확한 error 반환.
- **.NET Framework 4.8 + C# 7.2**: 프로젝트 표준 제약 준수. 새 enum은 `EDatumAlgorithm` 명명.

## Acceptance Criteria

- [ ] `DatumConfig.AlgorithmType` enum 필드가 존재하고 INI에 저장/로드된다 (왕복 보존)
- [ ] 기존 Phase 4/11 INI 레시피(`AlgorithmType` 미존재)를 로드하면 자동으로 `TwoLineIntersect`로 해석되어 기존 동작이 회귀 없이 재현된다
- [ ] `AlgorithmType=CircleTwoHorizontal` + 유효 합성 입력에서 `TryTeachDatum`이 `true` 반환 + `RefOriginRow/Col`이 이론 교점 ±1px, `RefAngleRad`가 이론 기울기 ±0.01 rad 이내로 저장된다
- [ ] Circle 피팅 실패 합성 입력에서 `TryTeachDatum`이 `false` + error에 "Circle fit failed" 포함
- [ ] 수평 라인 결합 에지점 부족 합성 입력에서 `TryTeachDatum`이 `false` + error에 "Horizontal line fit failed" 포함
- [ ] 원 중심이 수평선 위쪽(방향 정합성 위반)인 입력에서 `TryTeachDatum`이 `false` + error에 방향 정합성 위반 메시지 포함
- [ ] 성공 시 `LastTeachSucceeded=true`, `IsConfigured=true` 설정 + INI 왕복 보존
- [ ] `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` 빌드 성공 + 신규 경고 없음

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                                 |
|--------------------|-------|------|--------|-----------------------------------------------------------------------|
| Goal Clarity       | 0.90  | 0.75 | ✓      | MVP = 티칭+저장만, 런타임 재검출은 deferred                             |
| Boundary Clarity   | 0.92  | 0.70 | ✓      | Strategy 패턴/직교 교정/Phase 11 공유 등 4개 out-of-scope 명시           |
| Constraint Clarity | 0.75  | 0.65 | ✓      | INI 하위호환, Halcon 표준 경로, C# 7.2 준수                             |
| Acceptance Criteria| 0.75  | 0.70 | ✓      | 8개 pass/fail 항목, 합성 입력 기반 검증 기준 포함                        |
| **Ambiguity**      | 0.155 | ≤0.20| ✓      |                                                                       |

## Interview Log

| Round | Perspective          | Question summary                                   | Decision locked                                                                 |
|-------|----------------------|---------------------------------------------------|---------------------------------------------------------------------------------|
| 1     | Researcher           | Phase 13과의 역할 분담?                            | 12 = 구체 구현(하드코딩 분기), 13 = Strategy 리팩터링                            |
| 1     | Researcher           | 2 ROI → 1 연장선 기하?                             | 에지점들을 concat 후 FitLineContourXld 1회 피팅                                 |
| 1     | Researcher           | 기존 2-Line Datum과 공존?                          | 동일 DatumConfig에 AlgorithmType 필드 추가, 기존은 TwoLineIntersect 기본값       |
| 2     | Simplifier           | MVP 범위?                                          | 티칭 성공 + RefOrigin/RefAngle 저장만. 런타임 TryFind는 저장값 재사용            |
| 2     | Boundary Keeper      | 명시적 out-of-scope?                               | Strategy 패턴, 직교 교정, Phase 11 Circle 공유, 런타임 재검출 모두 제외          |
| 2     | Failure Analyst      | 감지해야 할 실패 시나리오?                          | Circle 피팅 실패 / 수평 라인 피팅 실패 / 교점 불정의 / 방향 정합성 4종          |

---

*Phase: 12-datum-circle-vertical-horizontal-intersection*
*Spec created: 2026-04-23*
*Next step: /gsd-discuss-phase 12 — implementation decisions (Halcon 호출 시퀀스, UI 티칭 단계, error 문자열 포맷 등)*
