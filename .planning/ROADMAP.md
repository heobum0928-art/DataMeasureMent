# Roadmap: DataMeasurement

## Overview

Phase 5에서 완성된 Shot-FAI 2계층 데이터 모델 위에 5개 단계로 실제 검사 시스템을 구축한다. UI 재설계로 TreeView 2계층 탐색 구조를 만들고, 티칭 워크플로우에서 ROI를 설정하며, Halcon 에지 측정 알고리즘으로 정밀 거리 측정을 수행하고, Datum 기준좌표계로 제품 위치 편차를 자동 보정하며, 검사 시퀀스와 TCP 통신으로 실제 운영 가능한 시스템을 완성한다.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: UI 재설계** - FAI-centric 트리 통합 + 단일 캔버스 + FAI 결과 테이블 + CRUD (completed 2026-04-07)
- [x] **Phase 2: 티칭 & 캘리브레이션** - ROI 시각화 + 저장/로드 + 픽셀-mm 캘리브레이션 (completed 2026-04-08)
- [x] **Phase 3: 에지 측정 알고리즘** - Halcon MeasurePos 거리 측정 + 공차 판정 + 결과 오버레이 (completed 2026-04-09)
- [x] **Phase 4: Datum 기준좌표계** - Datum Line 2개 → 교점/기준각 티칭 + 런타임 hom_mat2d ROI 보정 (completed 2026-04-10)
- [x] **Phase 5: 검사 시퀀스 & TCP** - Shot 순회 Grab + FAI 측정 + 종합 판정 + TCP 응답 (completed 2026-04-09)
- [ ] **Phase 6: Rapid City 확장** - Fixture/Multi-Datum 구조 + Multi-Algorithm 측정 + 조명 필드 + 새 INI 포맷

## Phase Details

### Phase 1: UI 재설계
**Goal**: 사용자가 기존 InspectionListView 트리에서 FAI 노드를 탐색하고, PropertyGrid로 FAI 속성을 편집하며, 캔버스에서 FAI 이미지를 보고, 결과 테이블을 확인하며, FAI를 추가/삭제/수정할 수 있다
**Depends on**: Nothing (first phase)
**Requirements**: UI-01, UI-02, UI-03, UI-04, UI-05
**Success Criteria** (what must be TRUE):
  1. InspectionListView 트리에서 Action 노드 아래에 FAI 노드가 표시된다
  2. FAI 노드 선택 시 PropertyGrid에 FAIConfig 속성이 표시되고 캔버스에 해당 FAI 이미지가 표시된다
  3. 검사 실행 후 FAI별 거리(mm)와 OK/NG 판정이 테이블 행으로 표시된다
  4. FAI 추가/삭제/수정 버튼이 동작하고 트리에 즉시 반영된다
  5. FAI 삭제 시 확인 다이얼로그가 표시된다
**Plans:** 2/2 plans complete
Plans:
- [x] 01-01-PLAN.md — FAIConfig->CameraSlaveParam 리팩터링 + ENodeType.FAI + InspectionRecipeManager FAI-centric CRUD
- [x] 01-02-PLAN.md — InspectionListView FAI 통합 + MainView 캔버스/테이블 + CRUD UI + 검증
**UI hint**: yes

### Phase 2: 티칭 & 캘리브레이션
**Goal**: 사용자가 캔버스에서 FAI ROI를 시각적으로 확인하고, ROI 설정을 저장/로드하며, 픽셀-mm 변환을 위한 캘리브레이션을 수행할 수 있다
**Depends on**: Phase 1
**Requirements**: TCH-01, TCH-02, ALG-03
**Success Criteria** (what must be TRUE):
  1. 캔버스에 FAI의 ROI 오버레이(에지 방향, 측정 범위)가 시각적으로 표시된다
  2. "저장" 실행 후 앱을 재시작해도 ROI 데이터가 유지된다
  3. 캘리브레이션 실행 후 픽셀-mm 변환 계수가 시스템에 적용된다
**Plans:** 2 plans
Plans:
- [x] 02-01-PLAN.md — 데이터 모델 확장 (FAIConfig.ToRoiDefinition + PolygonPoints + PixelResolution) + ROI 오버레이 렌더링 파이프라인 + FAI 선택 하이라이트
- [x] 02-02-PLAN.md — 캔버스 툴바 UI + Rect ROI 드래그 설정 + Polygon ROI 점 클릭 생성 + 2점 캘리브레이션 플로우
**UI hint**: yes

### Phase 3: 에지 측정 알고리즘
**Goal**: 사용자가 FAI ROI 내에서 Halcon MeasurePos로 에지 간 거리(mm)를 계산하고, 공차 기준 OK/NG 판정을 받으며, 측정 결과를 캔버스 오버레이로 확인할 수 있다
**Depends on**: Phase 2
**Requirements**: ALG-01, ALG-02, ALG-04
**Success Criteria** (what must be TRUE):
  1. FAI 측정 실행 시 ROI 내 에지 페어 거리가 mm 단위로 계산된다
  2. 계산된 거리가 FAIConfig의 Tolerance 범위 내이면 OK, 벗어나면 NG로 판정된다
  3. 측정 후 캔버스에 에지 위치, 거리 값, 판정 결과가 오버레이로 표시된다
**Plans:** 1/2 plans executed
Plans:
- [x] 03-01-PLAN.md — FAIEdgeMeasurementResult 모델 + FAIEdgeMeasurementService 측정 서비스 + Action_FAIMeasurement 스텁 교체
- [ ] 03-02-PLAN.md — HalconDisplayService FAI 오버레이 색상 + DisplayMessages 파이프라인 + FAIResultRow 갱신 + 시각 검증
**UI hint**: yes

### Phase 4: Datum 기준좌표계
**Goal**: 사용자가 Datum(기준 에지 Line 2개)을 티칭하면, 런타임에서 제품 위치/회전 편차를 자동 보정하여 FAI ROI가 정확한 위치에서 측정된다
**Depends on**: Phase 3
**Requirements**: ALG-05 (Datum 보정)
**Success Criteria** (what must be TRUE):
  1. ShotConfig에 DatumConfig가 소유되고 INI 레시피로 저장/로드된다
  2. Datum 티칭 시 Line 2개의 교점과 기준 각도가 저장된다
  3. 런타임 측정 시 현재 Datum과 기준 Datum의 차이로 hom_mat2d 변환이 계산된다
  4. 변환 행렬이 하위 FAI의 ROI에 적용되어 보정된 위치에서 에지 측정이 수행된다
  5. Datum 미설정 상태에서는 무보정(Identity)으로 기존 Phase 3 흐름이 그대로 동작한다
  6. InspectionListView 트리에 Datum 노드가 표시되고 PropertyGrid에서 편집 가능하다
**Plans:** 3 plans
Plans:
- [x] 04-01-PLAN.md — DatumConfig 모델 + DatumFindingService + ShotConfig 통합 + INI Save/Load
- [x] 04-02-PLAN.md — 측정 파이프라인 Datum 통합 + UI 트리 Datum 노드 + PropertyGrid + 캔버스 오버레이
- [x] 04-03-PLAN.md — Gap closure: RenderDatumOverlay 호출 연결 + AddShotToSequence Datum 노드 + ALG-05 요구사항 등록

### Phase 5: 검사 시퀀스 & TCP
**Goal**: 사용자가 검사 시작 버튼 한 번으로 전체 Shot 순회 Grab과 FAI 측정이 자동 실행되고, 최종 판정 결과가 TCP로 호스트에 전송된다
**Depends on**: Phase 4
**Requirements**: SEQ-01, SEQ-02, SEQ-03, SEQ-04
**Success Criteria** (what must be TRUE):
  1. 검사 시작 시 각 Shot 위치로 Z축 이동하며 순서대로 카메라 Grab이 실행된다
  2. 각 Shot의 모든 FAI에 대해 에지 측정이 자동으로 수행되고 결과가 테이블에 채워진다
  3. 전체 FAI 결과를 종합한 최종 OK/NG 판정이 산출된다
  4. 측정 완료 후 결과 데이터가 TCP 패킷으로 호스트에 전송된다
**Plans:** 2 plans
Plans:
- [x] 05-01-PLAN.md — 시퀀스 프레임워크 확장 (StartAll + ExecuteAction 다중 Action + MoveZ + IAxisController + SimulImagePath)
- [x] 05-02-PLAN.md — TCP 응답 FAI 동적 결과 + InspectionSequence 종합 판정 + UI 실시간 갱신

### Phase 6: Rapid City 확장
**Goal**: Rapid City Z-Stopper A8.1 제품 대응을 위해 Fixture/Multi-Datum 구조, 6종 측정 알고리즘, 조명 필드, 새 INI 포맷을 구현하여 75개+ FAI 검사를 지원한다
**Depends on**: Phase 5
**Requirements**: RC-01, RC-02, RC-03, RC-04, RC-05, RC-06
**Success Criteria** (what must be TRUE):
  1. Sequence가 Fixture(한 면)로 동작하며 List<DatumConfig>를 소유한다
  2. DatumConfig가 ShotConfig에서 Sequence 레벨로 승격되어 Multi-Datum을 지원한다
  3. MeasurementBase 파생 클래스 6종이 각각 TryExecute()로 측정을 수행한다
  4. ShotConfig에 Ring/Back/Coax/Side 조명 필드가 추가되고 INI로 저장/로드된다
  5. 새 INI 포맷으로 레시피 저장/로드가 동작하고, 기존 포맷은 안내 메시지를 표시한다
  6. UI 트리에서 Sequence > Datum + Shot > FAI > Measurement 구조를 탐색할 수 있다
  7. 결과 테이블이 Measurement 단위로 한 행씩 표시된다
**Plans:** 4 plans
Plans:
- [ ] 06-01-PLAN.md — MeasurementBase 6종 파생 클래스 + VisionAlgorithmService + MeasurementFactory + FAIConfig.Measurements
- [ ] 06-02-PLAN.md — Datum 승격 (ShotConfig -> Sequence) + InspectionSequence Fixture 구조 + DatumConfig 확장 + 조명 필드
- [ ] 06-03-PLAN.md — Action_FAIMeasurement Datum+Measurement 실행 흐름 재설계 + INI Phase 6 새 포맷
- [ ] 06-04-PLAN.md — UI 트리 Sequence > Datum + Shot > FAI > Measurement 재구성 + 결과 테이블 Measurement 단위
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5 -> 6

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. UI 재설계 | 2/2 | Complete | 2026-04-07 |
| 2. 티칭 & 캘리브레이션 | 2/2 | Complete | 2026-04-08 |
| 3. 에지 측정 알고리즘 | 2/2 | Complete | 2026-04-09 |
| 4. Datum 기준좌표계 | 3/3 | Complete | 2026-04-10 |
| 5. 검사 시퀀스 & TCP | 2/2 | Complete | 2026-04-09 |
| 6. Rapid City 확장 | 0/4 | Planning complete | - |
