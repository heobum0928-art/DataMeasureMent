---
phase: 28
phase_name: FAI CircleDiameter + Datum Circle 알고리즘 통합
spec_date: 2026-05-08
status: spec_locked
ambiguity_score: 0.13
dimension_scores:
  goal: 0.90
  boundary: 0.85
  constraint: 0.85
  acceptance: 0.85
requirement_count: 6
related_phases: [19]
---

# Phase 28 — SPEC

## Goal

FAI 의 `CircleDiameterMeasurement` 에 Datum `CircleTwoHorizontal` 의 폴라 샘플링 검출 경로와 `Circle_RadialDirection` (Inward/Outward) 파라미터를 **선택적으로** 적용한다. 사용자가 명시적으로 RadialDirection 을 선택할 때만 폴라 알고리즘이 호출되고, 미선택 시(기본값) 기존 `VisionAlgorithmService.TryFindCircle` (단순 fit) 호출이 그대로 유지된다.

핵심 가치: Phase 18 CO-01 검증으로 안정화된 Datum 폴라 알고리즘을 FAI 측정에서도 사용 가능하게 하면서 기존 INI/측정 결과 회귀 0 을 보장.

## Why

Phase 19 UAT (2026-05-08) 에서 사용자가 명시적으로 요청: FAI 의 CircleDiameter 도 Datum 의 Circle 알고리즘 + 파라미터를 가져와야 한다. Phase 19 의 정적 노출(EdgeMeasureType 콤보) + 동적 hide 패턴은 검증 완료 → 그 위에 검출 알고리즘 통합을 얹는다.

## Requirements (Falsifiable)

### REQ-28-01 — Circle_RadialDirection 필드 추가
- **Current:** `CircleDiameterMeasurement` 에 RadialDirection 개념 없음 (Circle_Row/Col/Radius + EdgeThreshold/Sigma/EdgePolarity 6 필드만)
- **Target:** `Circle_RadialDirection` (string) 필드 추가, default = `""` (빈 문자열). PropertyGrid 에 콤보박스로 노출 (Inward / Outward 2 옵션 + 빈 값 = "선택 안 함")
- **Acceptance:** PropertyGrid 의 "Circle|ROI" 또는 "Edge" 카테고리에 Circle_RadialDirection 행이 콤보 ▼ 으로 표시됨. 콤보 옵션 = `EdgeOptionLists.RadialDirections` (Inward, Outward) 와 동일한 단일 소스 사용

### REQ-28-02 — 알고리즘 분기 (선택적 폴라)
- **Current:** `TryExecute` 가 `VisionAlgorithmService.TryFindCircle` 단일 호출
- **Target:** Circle_RadialDirection 값에 따라 분기:
  - 빈 문자열 (기본값) → 기존 `TryFindCircle` 호출 (변경 없음)
  - `"Inward"` 또는 `"Outward"` → Datum 의 `TryFindCircleByPolarSampling` (또는 등가 공유 helper) 호출, RadialDirection → Halcon polarity ("positive"/"negative") 매핑은 Datum CTH(`DatumFindingService.TryTeachCircleTwoHorizontal`) 의 매핑 규칙과 동일하게 적용
- **Acceptance:** grep 으로 두 경로 분기 코드 확인. 빈 문자열 입력 시 polar 호출 0 회 (기존 경로). Inward/Outward 입력 시 fit 호출 0 회 (폴라 경로 only)

### REQ-28-03 — Datum CTH 동등성 (회귀 0)
- **Current:** FAI CircleDiameter 와 Datum CTH 가 서로 다른 알고리즘 사용 → 동일 입력에서도 다른 검출점 가능
- **Target:** 동일 ROI (Circle_Row/Col/Radius) + 동일 이미지 + 동일 파라미터 (Sigma, EdgeThreshold, EdgePolarity, RadialDirection) 입력 시 FAI CircleDiameter (RadialDirection 명시) 와 Datum CTH 의 검출 직경 결과가 동일
- **Acceptance:** 사용자 UAT — Datum CTH 와 동일 ROI/이미지를 FAI 에 설정하고 CircleDiameter 실행 → `|FAI_diameter - Datum_diameter| ≤ 0.001 mm`. 동일 Halcon 코드 경로를 공유하므로 결정적

### REQ-28-04 — INI 하위호환
- **Current:** 기존 INI 레시피에 Circle_RadialDirection 키 없음
- **Target:** Circle_RadialDirection 미존재 INI 로드 시 default 빈 문자열 유지 → REQ-28-02 의 빈 문자열 분기 → 기존 `TryFindCircle` 호출 → 측정 결과 변동 0
- **Acceptance:** v1.0 시점의 INI 레시피 로드 → CircleDiameter 측정 결과가 v1.0 결과와 동일 (회귀 0). ParamBase.Save/Load 의 string case 분기로 자동 직렬화

### REQ-28-05 — PropertyGrid 정적 노출 (동적 hide 없음)
- **Current:** CircleDiameterMeasurement 가 ICustomTypeDescriptor 미구현
- **Target:** CircleDiameterMeasurement 는 ICustomTypeDescriptor 를 구현하지 않는다. Circle_RadialDirection 을 포함한 모든 필드는 항상 PropertyGrid 에 노출된다. 동적 hide 도입 안 함
- **Acceptance:** grep `ICustomTypeDescriptor` in CircleDiameterMeasurement.cs → 0 매치. PropertyGrid 표시 시 Circle_RadialDirection 항상 표시

### REQ-28-06 — 빌드 무결성
- **Current:** msbuild Debug/x64 PASS (Phase 19 fix 후 baseline)
- **Target:** Phase 28 변경 후 msbuild Debug/x64 PASS, 신규 error 0, 신규 warning 0 (pre-existing 5건 잔존 OK)
- **Acceptance:** msbuild 출력 grep 결과 신규 CS\\d+ 또는 MSB\\d+ 0건

## Boundaries

### In Scope
- CircleDiameterMeasurement.cs 의 단일 필드 추가 (Circle_RadialDirection)
- TryExecute 분기 로직 (빈 → fit / 명시 → polar)
- PropertyGrid 콤보 노출 (정적, 동적 hide 없음)
- INI 하위호환 (default 빈 string)
- Datum 폴라 helper 의 FAI 호출 경로 (직접 호출 또는 새 wrapper)
- EdgeOptionLists.RadialDirections 단일 소스 재사용

### Out of Scope (and why)
- **Circle_EdgeDirection / Circle_EdgeSelection 추가** — 사용자 결정 = 최소 스코프
- **RectL1Ratio / RectL2Ratio (strip cap)** — Datum 전용 파라미터, FAI 에 미필요 (사용자 결정)
- **CircleROI 또는 Rect strip ROI 입력 방식 변경** — 기존 Circle_Row/Col/Radius 3 필드 그대로 유지 (사용자 결정)
- **ICustomTypeDescriptor 동적 hide 도입** — REQ-28-05 (사용자 결정)
- **Test Find DetectedOrigin 시각화 (Datum 의 UX)** — FAI 에서는 별도 UX 없음 (out of scope)
- **다른 Measurement 클래스 (PointToLineDistance 등) 의 알고리즘 변경** — 28 은 CircleDiameter 만
- **Phase 19 UAT 의 측정 추가 모달 UX (이미 quick 260508-mcb 처리됨)** — Phase 28 와 무관

## Constraints

- **Tech**: .NET Framework 4.8, C# 7.2, Halcon 24.11. C# 8+ 기능 금지.
- **Compatibility**: ParamBase INI 직렬화는 string case 자동 처리 (line 337/385). RadialDirection 미존재 키 자동 폴백.
- **Architecture**: CircleDiameterMeasurement.cs 의 K&R 브레이스 스타일 유지. 신규 라인 `//260508 hbk Phase 28` 주석 필수.
- **Algorithm sharing**: Datum 의 TryFindCircleByPolarSampling 은 `DatumFindingService` 또는 `VisionAlgorithmService` 의 internal 일 가능성 — Phase 28 는 호출 경로를 만들기 위해 helper 를 public/internal 로 노출하거나 새 공유 wrapper 작성 가능 (구체 결정은 discuss-phase).
- **Verification**: SIMUL_MODE (D:\\1.bmp) 로 사용자 UAT.

## Acceptance Criteria

- [ ] **AC-1**: CircleDiameterMeasurement.Circle_RadialDirection 필드 존재. PropertyGrid 콤보 ▼ 표시 (Inward / Outward 2 옵션)
- [ ] **AC-2**: Circle_RadialDirection = "" → grep 으로 TryFindCircle 호출 1회 / TryFindCircleByPolarSampling 호출 0회 확인
- [ ] **AC-3**: Circle_RadialDirection = "Inward" 또는 "Outward" → grep 으로 TryFindCircleByPolarSampling 호출 1회 / TryFindCircle 호출 0회 확인
- [ ] **AC-4**: 동일 ROI/이미지로 Datum CTH (RadialDirection=Inward) 와 FAI CircleDiameter (RadialDirection=Inward) 실행 → 검출 직경 차이 ≤ 0.001 mm (사용자 UAT 검증)
- [ ] **AC-5**: v1.0 시점의 INI 레시피 (RadialDirection 키 없음) 로드 → CircleDiameter 결과 v1.0 동일 (회귀 0, 사용자 UAT)
- [ ] **AC-6**: msbuild Debug/x64 PASS, 신규 error/warning 0, 모든 신규 라인에 `//260508 hbk Phase 28` 주석

## Open Questions for /gsd-discuss-phase

- Datum 의 폴라 helper (`TryFindCircleByPolarSampling`) 가 현재 어떤 클래스의 어떤 visibility 인가? `internal` 이면 `public` 으로 승격할지, 새 공유 wrapper 클래스를 만들지.
- RadialDirection → Halcon polarity 매핑 코드는 어디에 둘지 (CircleDiameterMeasurement / VisionAlgorithmService / 신규 helper).
- EdgeOptionLists.RadialDirections 가 이미 정의되어 있음 (Phase 17 D-02) — FAI 도 같은 List 직접 참조 vs FAI 전용 [Browsable(false)] List 래퍼.

## Ambiguity Report

| Dimension | Score | Min | Status |
|---|---|---|---|
| Goal Clarity | 0.90 | 0.75 | ✓ |
| Boundary Clarity | 0.85 | 0.70 | ✓ |
| Constraint Clarity | 0.85 | 0.65 | ✓ |
| Acceptance Criteria | 0.85 | 0.70 | ✓ |

**Final ambiguity:** 0.13 (gate ≤ 0.20 ✓). All dimensions clear. Ready for /gsd-discuss-phase.
