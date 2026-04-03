# Roadmap: DataMeasurement

## Overview

Phase 5에서 완성된 Shot-FAI 2계층 데이터 모델 위에 4개 단계로 실제 검사 시스템을 구축한다. UI 재설계로 TreeView 2계층 탐색 구조를 만들고, 티칭 워크플로우에서 ROI를 설정하며, Halcon 에지 측정 알고리즘으로 정밀 거리 측정을 수행하고, 검사 시퀀스와 TCP 통신으로 실제 운영 가능한 시스템을 완성한다.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: UI 재설계** - TreeView 2계층(Shot/FAI) + 단일 캔버스 + FAI 결과 테이블 + CRUD
- [ ] **Phase 2: 티칭 & 캘리브레이션** - ROI 시각화 + 저장/로드 + 픽셀-mm 캘리브레이션
- [ ] **Phase 3: 에지 측정 알고리즘** - Halcon MeasurePos 거리 측정 + 공차 판정 + 결과 오버레이
- [ ] **Phase 4: 검사 시퀀스 & TCP** - Shot 순회 Grab + FAI 측정 + 종합 판정 + TCP 응답

## Phase Details

### Phase 1: UI 재설계
**Goal**: 사용자가 Shot/FAI 2계층 구조를 탐색하고, 단일 캔버스에서 이미지를 보며, 결과 테이블을 확인하고, Shot/FAI를 추가·삭제·수정할 수 있다
**Depends on**: Nothing (first phase)
**Requirements**: UI-01, UI-02, UI-03, UI-04, UI-05
**Success Criteria** (what must be TRUE):
  1. TreeView에서 Shot 노드를 펼치면 해당 Shot의 FAI 목록이 표시된다
  2. TreeView에서 Shot을 선택하면 단일 캔버스에 해당 Shot의 이미지가 표시된다 (기존 5탭 없음)
  3. 검사 실행 후 FAI별 거리(mm)와 OK/NG 판정이 테이블 행으로 표시된다
  4. Shot 추가/삭제/수정 버튼이 동작하고 TreeView에 즉시 반영된다
  5. FAI 추가/삭제/수정 버튼이 동작하고 선택된 Shot 아래에 즉시 반영된다
**Plans:** 2 plans
Plans:
- [x] 01-01-PLAN.md — ViewModel 계층 생성 (ShotNodeViewModel, FAINodeViewModel, FAIResultRow, InspectionViewModel)
- [ ] 01-02-PLAN.md — MainView.xaml 3영역 레이아웃 + 코드비하인드 + csproj 업데이트 + UI 검증
**UI hint**: yes

### Phase 2: 티칭 & 캘리브레이션
**Goal**: 사용자가 캔버스에서 FAI ROI를 시각적으로 확인하고, ROI 설정을 저장/로드하며, 픽셀-mm 변환을 위한 캘리브레이션을 수행할 수 있다
**Depends on**: Phase 1
**Requirements**: TCH-01, TCH-02, ALG-03
**Success Criteria** (what must be TRUE):
  1. 캔버스에 FAI의 ROI 오버레이(에지 방향, 측정 범위)가 시각적으로 표시된다
  2. "저장" 실행 후 앱을 재시작해도 ROI 데이터가 유지된다
  3. 캘리브레이션 실행 후 픽셀-mm 변환 계수가 시스템에 적용된다
**Plans**: TBD
**UI hint**: yes

### Phase 3: 에지 측정 알고리즘
**Goal**: 사용자가 FAI ROI 내에서 Halcon MeasurePos로 에지 간 거리(mm)를 계산하고, 공차 기준 OK/NG 판정을 받으며, 측정 결과를 캔버스 오버레이로 확인할 수 있다
**Depends on**: Phase 2
**Requirements**: ALG-01, ALG-02, ALG-04
**Success Criteria** (what must be TRUE):
  1. FAI 측정 실행 시 ROI 내 에지 페어 거리가 mm 단위로 계산된다
  2. 계산된 거리가 FAIConfig의 Tolerance 범위 내이면 OK, 벗어나면 NG로 판정된다
  3. 측정 후 캔버스에 에지 위치, 거리 값, 판정 결과가 오버레이로 표시된다
**Plans**: TBD

### Phase 4: 검사 시퀀스 & TCP
**Goal**: 사용자가 검사 시작 버튼 한 번으로 전체 Shot 순회 Grab과 FAI 측정이 자동 실행되고, 최종 판정 결과가 TCP로 호스트에 전송된다
**Depends on**: Phase 3
**Requirements**: SEQ-01, SEQ-02, SEQ-03, SEQ-04
**Success Criteria** (what must be TRUE):
  1. 검사 시작 시 각 Shot 위치로 Z축 이동하며 순서대로 카메라 Grab이 실행된다
  2. 각 Shot의 모든 FAI에 대해 에지 측정이 자동으로 수행되고 결과가 테이블에 채워진다
  3. 전체 FAI 결과를 종합한 최종 OK/NG 판정이 산출된다
  4. 측정 완료 후 결과 데이터가 TCP 패킷으로 호스트에 전송된다
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. UI 재설계 | 0/2 | Planning complete | - |
| 2. 티칭 & 캘리브레이션 | 0/? | Not started | - |
| 3. 에지 측정 알고리즘 | 0/? | Not started | - |
| 4. 검사 시퀀스 & TCP | 0/? | Not started | - |
