# Phase 36: Datum DualImage 설계 보강 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-28
**Phase:** 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28
**Areas discussed:** 좌표계 모델 + anchor 산출, ExpectedAngleDeg / Tolerance UX, Test Find 시각화 fallback, INI 직렬화 + 하위호환

---

## 영역 선택

| Option | Description | Selected |
|--------|-------------|----------|
| 좌표계 모델 + anchor 산출 | CO-34.1-09 근본원인 | ✓ |
| ExpectedAngleDeg / Tolerance UX | 검증 입력 + PASS/FAIL 표시 | ✓ |
| Test Find 시각화 fallback | DetectedOrigin 화면 밖/캔버스 분기 | ✓ |
| INI 직렬화 + 하위호환 | 신규 필드 + Test 4 종결 | ✓ |

**User's choice:** 4 영역 모두

---

## Area 1 — 좌표계 모델 + anchor 산출

### Q1.1 DualImage 두 이미지 관계?

| Option | Description | Selected |
|--------|-------------|----------|
| 동일 카메라 + 다른 조명/Z (변환 0) | Phase 27 케이스 패턴, sensor frame 동일 | ✓ |
| 다른 카메라 또는 다른 시점 (변환 필요) | fixture 이동/재장착 케이스 | |
| 둘 다 허용 (런타임 설정) | DatumConfig 토글 | |

**User's choice:** 동일 카메라 + 다른 조명/Z

### Q1.2 anchor 모델 역할?

| Option | Description | Selected |
|--------|-------------|----------|
| 명시적 SameFrame 계약 + 가드 | anchor/transform 도입 안 함 | ✓ |
| Anchor (Row,Col) 메타만 저장 | 시각화 기준점 한정 | |
| Full 변환 행렬 (over-engineering) | HomMat2d 입력 | |

**User's choice:** 명시적 SameFrame 계약 + 가드

### Q1.3 SameFrame 가드 레벨?

| Option | Description | Selected |
|--------|-------------|----------|
| 런타임 이미지 크기 검증 | 진입부 W×H 일치 | ✓ |
| 설계 주석만 | 런타임 가드 0 | |
| 크기 + DatumConfig 메타 필드 | 둘 다 | |

**User's choice:** 런타임 이미지 크기 검증

---

## Area 2 — ExpectedAngleDeg / AngleTolerance UX

### Q2.1 적용 범위?

| Option | Description | Selected |
|--------|-------------|----------|
| DualImage 전용 | PropertyGrid Hide 분기로 한정 | ✓ |
| 모든 algorithm 공통 | 광범위 검증 가능, 회귀 표면 ↑ | |
| 공통 + sentinel 회피 | 공통 선언 + 0 sentinel off | |

**User's choice:** DualImage 전용

### Q2.2 PropertyGrid PASS/FAIL 표시 방법?

| Option | Description | Selected |
|--------|-------------|----------|
| DetectedAngleDeg 셀 배경 색상 | 기존 ReadOnly 필드 활용 | ✓ |
| 신규 ReadOnly Status 필드 추가 | 명시적이지만 줄 1개 더 | |
| MainView 외부 toast/상태바 | 가시성 ↑, XAML 변경 ↑ | |

**User's choice:** DetectedAngleDeg 셀 배경 색상

### Q2.3 기본값?

| Option | Description | Selected |
|--------|-------------|----------|
| Tolerance 1.0° + Expected [-180,180] | atan2 출력과 일치 | ✓ |
| Tolerance 0.5° + Expected [0,360] | 정밀도 ↑, 범위 정규화 필요 | |
| Tolerance 0 (게이트 off) | 명시적 활성화 | |

**User's choice:** Tolerance 1.0° + Expected [-180,180]

### Q2.4 PASS/FAIL 평가 시점?

| Option | Description | Selected |
|--------|-------------|----------|
| Test Find 성공 직후 자동 | DatumFindingService write-back 직후 | ✓ |
| Teach 도 평가 + Expected auto-suggest | Teach UX 증가 | |
| 수동 Validate 버튼 | 추가 UI 요소 | |

**User's choice:** Test Find 성공 직후 자동 평가

---

## Area 3 — Test Find 시각화 fallback

### Q3.1 DualImage 상태에서 어느 캔버스에 그릴지?

| Option | Description | Selected |
|--------|-------------|----------|
| 현재 표시 중 이미지 캔버스 | SameFrame 가정으로 좌표 유효 | ✓ |
| 가로축 캔버스에만 (1개 고정) | 명확 분리, 세로 토글 시 origin 미표시 | |
| 두 캔버스 동시 표시 (PiP/분할) | 레이아웃 변경 필요 | |

**User's choice:** 현재 표시 중인 이미지 캔버스에 그림

### Q3.2 OFF-SCREEN 처리?

| Option | Description | Selected |
|--------|-------------|----------|
| 중앙 fallback 십자 + 좌표 텍스트 | 명확한 OFF-SCREEN 라벨 | ✓ |
| 가장자리 클램프 + 화살표 | 공간감 ↑, 오해 가능성 | |
| 둘 다 (인디케이터 + 중앙 좌표) | 정보량 ↑, 구현 부담 ↑ | |

**User's choice:** 화면 중앙 fallback 십자 + 좌표 텍스트 레이블

### Q3.3 Angle 시각화 보강?

| Option | Description | Selected |
|--------|-------------|----------|
| Origin 십자에 각도 화살표 | DetectedRefAngle 실선 + Expected 점선 | ✓ |
| Detected angle 텍스트만 | 수치만 표시 | |
| 시각화 안 함 (PropertyGrid 충분) | 캔버스 영역 절약 | |

**User's choice:** Origin 십자에 angle 화살표

---

## Area 4 — INI 직렬화 + 하위호환

### Q4.1 직렬화 패턴?

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 22 IMG-01 / D-34-11 패턴 1:1 답습 | DatumConfig 공동 필드, EnsurePerRoiDefaults 정규화 | ✓ |
| DualImage 수식자 prefix | 명시적 algorithm 소속 | |
| [DatumAngleValidation] 별도 섹션 | ParamBase 파이프라인 변경 부담 | |

**User's choice:** Phase 22 IMG-01 패턴 1:1 답습

### Q4.2 INI 하위호환 sentinel?

| Option | Description | Selected |
|--------|-------------|----------|
| Expected==0.0 OR Tolerance==0.0 → off | L915 패턴과 동일 | ✓ |
| 별도 IsAngleValidationEnabled bool | 명시적 toggle, 줄 1개 ↑ | |
| Tolerance > 0 → 항상 검증 | Expected=0° 도 유효, 의도치 않은 검증 위험 | |

**User's choice:** Expected==0.0 또는 Tolerance==0.0 → 검증 off

### Q4.3 Test 4 UAT 계획?

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 34.1 Test 4 일정 동일 클론 | 6+2 필드 라운드트립 | ✓ |
| 1-image 3종 + DualImage 포함 종합 | UAT 시간 ↑, 회귀 광범위 | |

**User's choice:** Phase 34.1 Test 4 일정 동일 클론

---

## Claude's Discretion

- `AngleValidationStatus` 구체 타입 (enum 3값 / bool? / 두 필드)
- 색상 배지 메커니즘 (DataTrigger / IValueConverter / 코드비하인드)
- 각도 화살표 정확한 길이/스타일 픽셀값
- 각도 차이 wrap-around 계산 공식

## Deferred Ideas

- per-image transform / HomMat2d 모드 (Phase 27 또는 v1.2)
- Teach 후 ExpectedAngleDeg 자동 제안 (auto-populate)
- MainView toast / 외부 상태바 PASS/FAIL 표시
- TeachingImagePath/Vertical [Browse...] 버튼 UX (별도 v1.1 phase 후보)
