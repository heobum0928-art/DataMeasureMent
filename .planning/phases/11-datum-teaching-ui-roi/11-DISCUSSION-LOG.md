# Phase 11: datum-teaching-ui-roi - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-23
**Phase:** 11-datum-teaching-ui-roi
**Areas discussed:** Datum 티칭 UI 진입/버튼 구조, Datum 티칭 이미지 소스, Datum 티칭 결과 피드백, Circle ROI 드로잉/저장 모델, 티칭 워크플로 순서 가이드
**Mode:** `--all` (모든 gray area 자동 선택, 질문은 인터랙티브)

---

## Datum 티칭 UI 진입/버튼 구조

### Q1: Datum 노드 선택 상태에서 티칭을 어떻게 시작할지?

| Option | Description | Selected |
|--------|-------------|----------|
| 전용 Teach Datum 버튼 (Recommended) | 캔버스 툴바에 Datum 전용 버튼 신설. Datum 노드 선택 시에만 활성화. 가이드 단계(Line1→Line2→자동 TryTeachDatum) 순차 진행. | ✓ |
| 기존 Rect ROI 버튼 재사용 (Phase 4 D-12 원안) | Datum 노드 + Rect ROI 버튼 컨텍스트 분기. 단점: 같은 버튼이 FAI/Datum 컨텍스트에서 다르게 동작 → 혼동. | |
| Line1/Line2 버튼 분리 | Line1/Line2/Teach 3개 버튼. 명시적이지만 클릭 수 증가. | |

**User's choice:** 전용 Teach Datum 버튼
**Notes:** Phase 4 D-12 원안 폐기(D-25). 툴바에 Teach Datum 버튼 신설.

### Q2: Line1/Line2 ROI 다 그린 후 TryTeachDatum 호출은 어떻게 트리거되는가?

| Option | Description | Selected |
|--------|-------------|----------|
| 두 번째 ROI 확정 시 자동 호출 (Recommended) | Line2 MouseUp → 즉시 TryTeachDatum. 단계 최소. | ✓ |
| 명시적 Teach 버튼 클릭 | 두 ROI 그린 후 별도 Teach 버튼. 조정 여지 있지만 클릭 증가. | |
| 파라미터 수정 후 Teach | Sigma/EdgeThreshold/Polarity 조정 후 Teach. 초보자 진입 장벽. | |

**User's choice:** 두 번째 ROI 확정 시 자동 호출

### Q3: Datum이 이미 설정된 상태에서 재티칭하려면?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 ROI 초기화 후 재시작 (Recommended) | 확인 다이얼로그 후 Line1부터 다시. 단순 명확. | ✓ |
| ROI 유지, 기준값만 재계산 | 지그 고정 후 기준 이미지만 교체 시 유용. | |
| 개별 Line 수정 가능 | 유연하지만 UI 복잡. | |

**User's choice:** 기존 ROI 초기화 후 재시작

---

## Datum 티칭 이미지 소스

### Q1: Datum 티칭 시 사용할 이미지를 어디서 가져오는가?

| Option | Description | Selected |
|--------|-------------|----------|
| Grab 버튼 + ImageSourceMode 분기 (Recommended) | Dedicated=즉시 Grab / ReuseFromShot=지정 Shot 이미지 재사용. Datum 노드에서도 Grab 버튼 활성화. | ✓ |
| 항상 수동 Grab만 | ImageSourceMode 무시. Phase 6 디자인 무용지물. | |
| 파일에서 로드 옵션 추가 | Grab + 파일 로드 두 경로. 중복. | |

**User's choice:** Grab 버튼 + ImageSourceMode 분기

### Q2: ReuseFromShot 모드일 때 티칭 위치에서 사용할 Shot 지정?

| Option | Description | Selected |
|--------|-------------|----------|
| DatumConfig.SourceShotName 필드 신설 (Recommended) | PropertyGrid 드롭다운. INI 저장. | ✓ |
| Sequence 내 첫 번째 Shot 고정 | 단순하지만 유연성 없음. | |
| Phase 11 범위 외 → Dedicated만 지원 | ReuseFromShot UI deferred. | |

**User's choice:** DatumConfig.SourceShotName 필드 신설

### Q3: SIMUL_MODE에서 Datum 티칭용 이미지 경로?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 SimulImagePath 재사용 (Recommended) | Phase 5 Plan 01 SimulImagePath 그대로. SourceShotName 기반. | ✓ |
| 공용 D:\1.bmp 고정 | 가장 단순. | |
| Datum별 SimulImagePath 신규 전용 필드 | 필드 중복. | |

**User's choice:** 기존 SimulImagePath 재사용

---

## Datum 티칭 결과 피드백

### Q1: TryTeachDatum 성공 시 캔버스에 무엇을 보여주는가?

| Option | Description | Selected |
|--------|-------------|----------|
| 검출된 두 라인 + 교점 마커 (Recommended) | Line1/Line2 실제 검출 직선 + RefOrigin 십자. | ✓ |
| ROI 사각형만 유지 | 현재 수준 유지. 구현 부담 최소. | |
| 검출 라인만, 교점 생략 | 교점은 MSoP 패턴 핵심. | |

**User's choice:** 검출된 두 라인 + 교점 마커

### Q2: TryTeachDatum 실패 시 어떻게 알리는가?

| Option | Description | Selected |
|--------|-------------|----------|
| 인라인 상태 표시 (Recommended) | label_drawHint 빨간 텍스트, ROI 유지 → 재시도 용이. | ✓ |
| CustomMessageBox 팝업 | 모달 중단감. 연속 튜닝 방해. | |
| Logging + 조용히 실패 | 원인 파악 어려움. | |

**User's choice:** 인라인 상태 표시

### Q3: TryTeachDatum 성공 후 DatumConfig는 언제 INI에 저장?

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 Recipe 저장 필로우 (Recommended) | 메모리 반영 후 사용자 Save. FAI CRUD와 동일 패턴. | ✓ |
| Teach 성공 즉시 자동 저장 | 안전하지만 FAI 수정과 저장 시점 불일치 혼란. | |
| Dirty 플래그 + 종료 확인 | 플래그 관리 부담, 기존 구조 도입 안 됨. | |

**User's choice:** 기존 Recipe 저장 필로우

---

## Circle ROI 드로잉/저장 모델

### Q1: Circle ROI를 사용자가 캔버스에서 어떻게 그리는가?

| Option | Description | Selected |
|--------|-------------|----------|
| 중심 클릭 + 드래그로 반지름 (Recommended) | 첫 클릭=중심, 드래그=반지름. Rect 드로잉 패턴 유사. | ✓ |
| 경계 박스에 내접(Rect 재사용) | 신규 드로잉 코드 최소. 의도 혼선. | |
| 3점 클릭(원 위 3점) | Polygon 유사. 구현/조정 비직관. | |

**User's choice:** 중심 클릭 + 드래그로 반지름

### Q2: Circle ROI 파라미터를 RoiDefinition에 어떻게 보관?

| Option | Description | Selected |
|--------|-------------|----------|
| RoiShape enum + Center/Radius 필드 신설 (Recommended) | Shape enum(Rect/Polygon/Circle) + CenterRow/CenterCol/Radius. 명시적, 확장 가능. | ✓ |
| 경계 박스 재사용(Row1…Col2를 Circle bbox) | 신규 필드 없음. 타원 표현 불가. | |
| Polygon N점 근사 | 근사 오차. | |

**User's choice:** RoiShape enum + Center/Radius 필드 신설

### Q3: Phase 11에서 Circle ROI를 어디에 적용?

| Option | Description | Selected |
|--------|-------------|----------|
| CircleDiameterMeasurement만 (Recommended) | RC-03 전용. Datum은 Line 유지. 범위 명확. | ✓ |
| CircleDiameter + Datum Circle 유형 | Datum 계약(Line 2개) 확장. 스코프 크립. | |
| 모든 MeasurementBase 파생 + 미래 확장 | Point 의미 모호. 보편 정의 어려움. | |

**User's choice:** CircleDiameterMeasurement만

---

## 티칭 워크플로 순서 가이드 (WR-RT-04)

### Q1: Datum→ROI→알고리즘/테스트→저장 순서를 사용자에게 어떻게 알리는가?

| Option | Description | Selected |
|--------|-------------|----------|
| 노드별 상태 배지 + 상태바 힌트 (Recommended) | 트리 배지 색상 + 하단 상태바 "다음 단계". 강제 없음. | ✓ |
| 하드 블로킹 | Datum 미티칭 시 후속 버튼 비활성화. 수정 시 막힘. | |
| 위저드 UI | step-by-step 모달. 트리+PropertyGrid와 이질. | |
| 순수 문서화 | README/툴팁만. 시각적 가이드 없음. | |

**User's choice:** 노드별 상태 배지 + 상태바 힌트

### Q2: 테스트 실행 전 'FAI 측정 가능' 검증은 어떤 제약을 강제?

| Option | Description | Selected |
|--------|-------------|----------|
| Datum 미설정 + ROI 미설정만 검증 (Recommended) | 두 조건만 하드 검증. Tolerance는 튜닝 목적이라 허용. | ✓ |
| Datum+ROI+Tolerance 전부 필수 | 엄격. 초기 튜닝 불편. | |
| 검증 없음 | 현상 유지. UI 가이드 없음. | |

**User's choice:** Datum 미설정 + ROI 미설정만 검증

### Q3: FAI별 단독 테스트 실행 버튼은 Phase 11 범위?

| Option | Description | Selected |
|--------|-------------|----------|
| 포함 — 선택 FAI 단독 리허설/측정 (Recommended) | Test FAI 버튼. 튜닝 사이클 단축. | ✓ |
| 범위 외 → Datum + Circle만 | 스코프 축소. WR-RT-04 부분 만족. | |
| 기존 Shot 실행 단축 | Shot 단위만. FAI 튜닝 부족. | |

**User's choice:** 포함 — FAI 단독 리허설/측정

---

## Claude's Discretion

- label_drawHint 색상/폰트 스타일(에러=빨강/성공=초록 권장)
- Datum 상태 배지의 구체 아이콘 글리프 / XAML DataTrigger 구현 위치
- Circle ROI 드로잉 중 미리보기 원 색상
- RoiShape enum 파일 위치
- StartCircleDrawing 이벤트 시그니처
- Test FAI 버튼 XAML 배치
- Datum 검출 직선 오버레이 구체 Halcon 렌더링

## Deferred Ideas

(CONTEXT.md `<deferred>` 섹션 참조)
