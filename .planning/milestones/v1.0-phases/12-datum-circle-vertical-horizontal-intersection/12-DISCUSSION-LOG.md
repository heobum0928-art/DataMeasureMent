# Phase 12: datum-circle-vertical-horizontal-intersection - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-23
**Phase:** 12-datum-circle-vertical-horizontal-intersection
**Areas discussed:** UI 티칭 흐름 + Phase 11 btn_teachDatum 처리, Halcon 호출 파이프라인, DatumConfig 필드 레이아웃 + AlgorithmType 타입, 실패 감지 + Error 문자열 + 임계값
**SPEC.md loaded:** yes (7 requirements, ambiguity 0.17)

---

## Gray Area Selection

| Area | Selected |
|------|----------|
| UI 티칭 흐름 + Phase 11 btn_teachDatum 처리 | ✓ |
| Halcon 호출 파이프라인 | ✓ |
| DatumConfig 필드 레이아웃 + AlgorithmType 타입 | ✓ |
| 실패 감지 + Error 문자열 + 임계값 | ✓ |

모든 4개 영역 선택됨. (SPEC.md가 locked이므로 WHAT/WHY 질문은 생성하지 않음 — HOW 결정만 논의.)

---

## UI 티칭 흐름 + Phase 11 btn_teachDatum 처리

### Q: btn_teachDatum이 아직 없는데 Phase 12가 이를 어떻게 처리할지?

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 12에서 btn_teachDatum 신규 추가 (TwoLineIntersect 포함 3-way 한 번에) | 3-way 알고리즘은 전체 상태머신 재설계이므로 한 번에 짓는 것이 구조적으로 깔끔 | ✓ |
| Phase 11 Plan 03b 먼저 (TwoLineIntersect UI 완성) → Phase 12에서 확장 | Phase 경계 존중, 두 번 고치는 부담 | |
| Phase 12에서 btn_teachDatum 없이 PropertyGrid 수동 실행 경로만 | SPEC "필요 최소 티칭 UI 연결" 위배 | |

**User's choice:** Phase 12에서 btn_teachDatum 신규 추가
**Notes:** Phase 11 Plan 03a까지만 완료(DatumConfig + RenderDatumOverlay). Phase 11 CONTEXT.md D-01..D-05 UI 설계는 Phase 12로 위임 이행.

---

### Q: 사용자가 어떤 알고리즘을 선택할지 어떻게 UI에 노출할까?

| Option | Description | Selected |
|--------|-------------|----------|
| PropertyGrid에서 EDatumAlgorithm enum 드롭다운 자동 표시 | Phase 4 EdgePolarity/EdgeDirection 패턴 일관, PropertyTools 자동 | ✓ |
| 캔버스 툴바에 AlgorithmType ComboBox 전용 버튼 | 시각적 돋보임, 툴바 공간 소비 | |
| btn_teachDatum 우클릭 메뉴 | 발견 가능성 낮음 | |

**User's choice:** PropertyGrid EDatumAlgorithm 자동 드롭다운
**Notes:** `[ItemsSourceProperty]` 어노테이션 불필요(enum 자동 렌더링).

---

### Q: EDatumTeachStep 상태머신 설계? (알고리즘별 단계 수 다름)

| Option | Description | Selected |
|--------|-------------|----------|
| enum 통합 + switch 분기 (Phase 13 재설계 예상) | Line1/Line2/Circle/Vertical/HorizontalA/HorizontalB/Done 단일 enum | ✓ |
| 가변 인덱스 + List<EDatumROIStep> (Phase 13 설계 선행) | Phase 13 CONTEXT D-12 구조 선채택 | |
| 알고리즘별 전용 메서드 분리 | 중복 코드 | |

**User's choice:** enum 통합 + switch 분기
**Notes:** Phase 13이 `DatumAlgorithmBase.GetROISteps()` 가변 배열로 추후 리팩터링 예정.

---

### Q: 마지막 ROI 드로잉 MouseUp 직후 TryTeachDatum 자동 호출?

| Option | Description | Selected |
|--------|-------------|----------|
| 자동 호출 (Phase 11 D-02 패턴 준수) | 단계 최소화, 기존 패턴 일관 | ✓ (default — not explicitly asked) |
| 별도 "Teach 실행" 버튼 | 사용자가 명시적 실행 | |
| MouseUp 후 CustomMessageBox 확인 | Phase 11 D-12에서 명시적으로 폐기 | |

**User's choice:** 자동 호출 (기본값 적용 — 질문 응답 누락)
**Notes:** Phase 11 CONTEXT D-02와 일관. 별도 Teach 버튼 없음.

---

## Halcon 호출 파이프라인

### Q: Circle 피팅 구현은 어떤 경로?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 VisionAlgorithmService.TryFindCircle 재사용 | 중복 구현 제거, Phase 11과 일관성 | ✓ |
| DatumFindingService에 전용 Circle 메서드 신규 작성 | 파라미터 독립, 코드 중복 | |
| VisionAlgorithmService 확장 → Datum이 인자로 전달 | 공유 + 관심사 보존, 시그니처 변경 리스크 | |

**User's choice:** 기존 VisionAlgorithmService.TryFindCircle 재사용

---

### Q: 수평 2-ROI concat 피팅 Halcon API 경로?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 TryFindLine(MeasurePos) 재사용 → XLD contour → ConcatObj → FitLineContourXld | 기존 DatumFindingService 패턴 일관성 | ✓ |
| EdgesSubPix → 두 XLD contour ConcatObj → FitLineContourXld | 2D 에지 정보, Circle과 일관 | |
| 두 ROI 각각 FitLineContourXld 후 평균 | SPEC Req 3 concat 요구 위반 | |

**User's choice:** 기존 TryFindLine(MeasurePos) 경로 재사용 → ConcatObj → FitLineContourXld 1회

---

### Q: 수직 ROI 피팅 기존 TryFindLine 재사용?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 TryFindLine 재사용 (Line1_* 필드 공유) | 파생 코드 최소, 시맨틱스 재해석 필요 | ✓ |
| Vertical_Row/Col/Phi/Length1/Length2 신규 필드 | 필드 의미 명확, INI 크기↑ | |
| 새 메서드 TryFindVerticalLine (방향각 검증 내장) | 중복 코드 | |

**User's choice:** 기존 TryFindLine 재사용 (Line1_* 시맨틱스 재해석)
**Notes:** Line1_*의 의미가 알고리즘별로 달라짐 → XML summary 주석 필수.

---

### Q: 교점 계산 Halcon 내장 API vs 수동 기하식?

| Option | Description | Selected |
|--------|-------------|----------|
| 두 경우 모두 HOperatorSet.IntersectionLl 사용 | 기존 TwoLineIntersect 경로 동일 패턴 | ✓ |
| CircleTwoHorizontal 수동 계산, VerticalTwoHorizontal IntersectionLl | SPEC Req 4(a) 수식과 1:1 매칭 | |
| 모두 수동 기하식 (ax+by+c=0 연립) | 기존 TwoLineIntersect 토글 필요 | |

**User's choice:** HOperatorSet.IntersectionLl 공통 사용
**Notes:** CircleTwoHorizontal의 수직 가상선은 (centerRow±1, centerCol) 2점으로 변환하여 IntersectionLl에 전달.

---

## DatumConfig 필드 레이아웃 + AlgorithmType 타입

### Q: AlgorithmType 필드 타입은 C# enum vs string?

| Option | Description | Selected |
|--------|-------------|----------|
| C# enum EDatumAlgorithm | 타입 안전성, Phase 13 동기, PropertyTools 자동 | ✓ |
| string ("TwoLineIntersect" 등) | ParamBase 자명, ItemsSourceProperty 필요 | |

**User's choice:** C# enum EDatumAlgorithm

---

### Q: Circle ROI 필드 명명?

| Option | Description | Selected |
|--------|-------------|----------|
| CircleROI_Row/Col/Radius (Phase 13 CONTEXT 정렬) | Phase 13 리팩터 리네임 불필요 | ✓ |
| Circle_Row/Col/Radius (CircleDiameterMeasurement 정렬) | 2-word 프리픽스 일관 | |
| Circle_ROI_Row/Col + CircleCenter_Row/Col (역할 명확 분리) | 필드 개수 증가 | |

**User's choice:** CircleROI_Row/Col/Radius + 휘발성 CircleCenter_Row/Col/Radius

---

### Q: 수평 2-ROI 필드 명명?

| Option | Description | Selected |
|--------|-------------|----------|
| Horizontal_A_Row/Col/Phi/Length1/Length2 + Horizontal_B_* (A/B prefix) | 병렬·비순서 명시 | ✓ |
| Horizontal1_* + Horizontal2_* (1/2 숫자 prefix) | Line1/Line2 패턴 일관 | |
| Line1/Line2 기존 필드 재해석 | 필드 의미 혼란 극심 | |

**User's choice:** Horizontal_A/B prefix (5×2=10 필드 신설)

---

### Q: 수직 ROI 필드? (Line1 재사용 vs 신규 Vertical_*)

| Option | Description | Selected |
|--------|-------------|----------|
| Line1_* 재사용 유지 (알고리즘별 시맨틱스) | INI 크기 동일, 주석 필수 | ✓ |
| Vertical_Row/Col/Phi/Length1/Length2 신규 | 필드 의미 명확, 필드 5개↑ | |

**User's choice:** Line1_* 재사용 유지
**Notes:** XML summary 주석으로 알고리즘별 Line1_* 의미 명시.

---

## 실패 감지 + Error 문자열 + 임계값

### Q: Error 문자열 prefix 포맷?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 패턴 유지 ("Line1: insufficient edge points" 스타일) | SPEC Acceptance 문구와 일치 | ✓ |
| 알고리즘 prefix 추가 ("[CircleTwoHorizontal] Circle fit: ...") | 디버깅 편의, SPEC 문구 불일치 | |
| 구조화된 오류 코드 + 메시지 ("E001_CIRCLE_FIT: ...") | 로그 분석 편의, UX 저하 | |

**User's choice:** 기존 패턴 유지
**Notes:** SPEC Acceptance 문구 "Circle fit failed", "Vertical line fit failed", "Horizontal line fit failed", "Intersection undefined" 그대로 사용.

---

### Q: 수평 라인 concat 피팅 최소 에지점 임계값?

| Option | Description | Selected |
|--------|-------------|----------|
| 10 (유효 피팅 최소 신뢰 수준) | 두 ROI 합산, tukey 로버스트 가정 | ✓ |
| 20 (보수적) | 노이즈 강건, 작은 ROI 오작동 가능 | |
| ROI 크기 기반 동적 임계값 | 적응적, 기존 경로와 불일치 | |

**User's choice:** 10 (두 ROI 합산 기준)

---

### Q: 교점 평행 판정 각도차 ε?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 Infinity/NaN 감지만 유지 | 기존 경로 통일, IntersectionLl 실측 | ✓ |
| 1° (극단적 평행만 거절) | 이중 검증 구조 | |
| 3° (엄격한 수직/수평 가정) | 스캐 필요 사양 부적합 | |

**User's choice:** 기존 Infinity/NaN 감지만 유지 (추가 각도 검사 X)

---

### Q: CircleTwoHorizontal 방향 정합성 위반 검사 구현?

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 12 MVP에서 미구현 (실패 모드 목록 등재만 + TODO 로그) | 운용 정책 미확립 → Phase 13으로 이월 | ✓ |
| DatumConfig에 ExpectedCircleAbove (bool) 추가 후 검사 | 운용 정책 확립 안 됨 | |
| 방향 정합성 검사 전면 삭제 (SPEC에서 Req 5(d) 제거 감안) | SPEC 변경 필요 | |

**User's choice:** Phase 12 MVP 미구현 (TODO 로그 + SPEC.md는 유지)
**Notes:** SPEC Acceptance Criteria 8번째 항목(방향 정합성 위반)은 Phase 12 완료 조건에서 제외, Phase 13 acceptance로 이월.

---

## Claude's Discretion

- `EDatumTeachStep` enum 파일 위치 (MainView private vs 별도)
- `GetNextStep` 메서드 위치 (MainView private vs helper 클래스)
- `label_drawHint` 단계별 안내 문구 세부
- 원 검출 결과 오버레이 RenderDatumOverlay 내 배치 순서
- AlgorithmType 변경 시 ROI 좌표 유지 vs 초기화 전략
- Plan 분할(1-01 데이터 모델 / 1-02 알고리즘 / 1-03 UI) 세부

## Deferred Ideas (Summary)

1. CircleTwoHorizontal 방향 정합성 위반 검사 (SPEC Req 5d → Phase 13 이월)
2. Phase 13 Strategy 패턴 추출
3. 직교 교정
4. Phase 11 Circle ROI 공유 리팩터
5. 런타임 TryFind 재검출
6. DatumConfig Vertical_* 전용 필드
7. 알고리즘 prefix 에러 접두사
8. 수평 concat 동적 임계값
9. 평행 판정 각도차 ε
10. Escape 키 단계 중단 UX
11. AlgorithmType 변경 시 ROI 초기화 전략
