# Phase 1: UI 재설계 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-02
**Phase:** 01-UI 재설계
**Areas discussed:** TreeView 구조, 레이아웃 배치, 결과 테이블 형식, CRUD 인터페이스

---

## TreeView 구조

### Q1: TreeView에서 Shot/FAI 노드를 어떻게 표시할까요?

| Option | Description | Selected |
|--------|-------------|----------|
| Shot 만 표시 | TreeView에 Shot 목록만, FAI는 결과 테이블에 | |
| Shot > FAI 2계층 | Shot 펼치면 FAI 자식 노드 | ✓ |
| 기존 패턴 확장 | InspectionListView CompositeNode 패턴 확장 | |

**User's choice:** Shot > FAI 2계층

### Q2: TreeView에서 Shot 선택 시 동작?

| Option | Description | Selected |
|--------|-------------|----------|
| 캔버스 이미지 + FAI 목록 | Shot 선택 → 캔버스 + 결과 테이블 갱신 | ✓ |
| 캔버스 이미지만 | 캔버스만 갱신 | |
| 전체 갱신 | 캔버스 + 테이블 + 속성창 전부 | |

**User's choice:** 캔버스 이미지 + FAI 목록

---

## 레이아웃 배치

### Q3: 전체 레이아웃 배치?

| Option | Description | Selected |
|--------|-------------|----------|
| 좌: TreeView / 우상: 캔버스 / 우하: 테이블 | 좌측 TreeView, 우측 상하 분할 | ✓ |
| 좌: TreeView / 중: 캔버스 / 우: 테이블 | 3컬럼 수평 배치 | |
| 상: 캔버스 / 하좌: TreeView / 하우: 테이블 | 캔버스 최대화, 하단 분할 | |

**User's choice:** 좌: TreeView | 우상: 캔버스 | 우하: 테이블

### Q4: 크기 조절?

| Option | Description | Selected |
|--------|-------------|----------|
| GridSplitter 사용 | 좌우 + 상하 경계 드래그 가능 | ✓ |
| 고정 비율 | 25% / 75% 고정 | |
| Claude 판단 | 적절히 구현 | |

**User's choice:** GridSplitter 사용

---

## 결과 테이블 형식

### Q5: 테이블 컬럼?

| Option | Description | Selected |
|--------|-------------|----------|
| FAI 이름 | FAI 식별자 | ✓ |
| 거리(mm) | 측정된 에지 간 거리 | ✓ |
| Spec (Min/Max) | 공차 범위 | ✓ |
| 판정 (OK/NG) | 합불 판정, 색상 코딩 | ✓ |

**User's choice:** 4개 전체 선택

### Q6: 테이블 행 선택 시 동작?

| Option | Description | Selected |
|--------|-------------|----------|
| 캔버스 ROI 하이라이트 | 행 선택 → 해당 ROI 강조 | ✓ |
| 선택만 | 캔버스 변화 없음 | |
| Claude 판단 | 적절히 구현 | |

**User's choice:** 캔버스 ROI 하이라이트

---

## CRUD 인터페이스

### Q7: 버튼 위치?

| Option | Description | Selected |
|--------|-------------|----------|
| TreeView 상단 툴바 | +/−/편집 버튼, 선택 노드 기준 동작 | ✓ |
| 우클릭 컨텍스트 메뉴 | 노드 우클릭 시 메뉴 | |
| 둘 다 | 툴바 + 우클릭 | |

**User's choice:** TreeView 상단 툴바

### Q8: 편집 가능 속성?

| Option | Description | Selected |
|--------|-------------|----------|
| 이름만 | Name만 편집, ROI/Tolerance는 Phase 2~3 | ✓ |
| 이름 + 기본 설정 | Name + Z위치/Tolerance | |
| Claude 판단 | Phase 1 범위에 맞게 | |

**User's choice:** 이름만

### Q9: 삭제 시 확인?

| Option | Description | Selected |
|--------|-------------|----------|
| 확인 다이얼로그 | "삭제하시겠습니까?" 확인 후 삭제 | ✓ |
| 바로 삭제 | 확인 없이 즉시 | |
| Claude 판단 | 적절히 구현 | |

**User's choice:** 확인 다이얼로그

---

## Claude's Discretion

- MainResultViewerControl 재사용 여부
- CompositeNode/NodeViewModel 패턴 참고 여부
- TabControl → 단일 캔버스 교체 방식

## Deferred Ideas

None
