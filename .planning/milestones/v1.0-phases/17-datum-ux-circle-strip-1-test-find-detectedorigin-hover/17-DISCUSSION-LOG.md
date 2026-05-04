# Phase 17: datum-ux-circle-strip-1-test-find-detectedorigin-hover - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-30
**Phase:** 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
**Areas discussed:** A. Circle 시각화 + EdgeDirection 정책, B. Edit 모드 + 그리기 UX, C. PropertyGrid 동적 노출 + 모달 정책, D. 신규 UI 기능 (DetectedOrigin + Hover + Preview)

---

## Cluster Selection

| Option | Description | Selected |
|--------|-------------|----------|
| A. Circle 시각화 + EdgeDirection 정책 | carry #1/2/3/16 — N개 strip → 1개 표시 / RadialDirection enum / EdgeDirection 정리 | ✓ |
| B. Edit 모드 + 그리기 UX | carry #8/9/13/14 — 좌클릭+드래그 / Edit 모드 일관성 / Delete 모달 | ✓ |
| C. PropertyGrid 동적 노출 + 모달 정책 | carry #6/10/11/12/15 — AlgorithmType 별 노출 / 즉시 갱신 / 모달 정책 | ✓ |
| D. 신규 UI 기능 | carry #5/17/18 — DetectedOrigin 시각화 / Hover 좌표 / 결과 메트릭 | ✓ |

**User's choice:** 4개 cluster 모두 선택

---

## Cluster A — Circle 시각화 + EdgeDirection 정책

### A.1 Strip 1개 선택 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 0° (3시 방향) 고정 1개 | thetaRad=0 단일 사각형. 구현 가장 간단. | ✓ |
| PolarStepDeg 한 스텝 회전 애니메이션 | 1개 사각형이 0°→360° 일정 주기 자동 회전. DispatcherTimer 필요. | |
| 마우스 hover 각도 추적 | 마우스 위치 기반 동적 각도. Cluster D hover 와 결합. | |

**User's choice:** 0° (3시 방향) 고정 1개

### A.2 RadialDirection 표현

| Option | Description | Selected |
|--------|-------------|----------|
| Circle_RadialDirection enum 신규 필드 | string sentinel + EnsurePerRoiDefaults fallback. INI 하위호환. | ✓ |
| Circle_EdgeDirection 재이용 | LtoR≡Outward, RtoL≡Inward 매핑. 의미 매핑 쿠아적. | |
| EdgePolarity 재이용 | positive/negative 의미 다름 (밝기 기울기). | |

**User's choice:** Circle_RadialDirection enum 신규 필드

### A.3 Circle_EdgeDirection 의미 정리

| Option | Description | Selected |
|--------|-------------|----------|
| Circle 분기에서 hide ([Browsable(false)] 동적) | ICustomTypeDescriptor 패턴. INI 보존. | ✓ |
| 노출하고 disable + tooltip | grayed-out + tooltip 안내. | |
| 노출 그대로 + tooltip만 | 사용자 혼란 가능성 높음. | |

**User's choice:** Circle 분기에서 hide ([Browsable(false)] 동적)

### A.4 EdgeDirection 슬롯×옵션 정책

| Option | Description | Selected |
|--------|-------------|----------|
| 모든 슬롯×모든 옵션 허용 + tooltip + 0개 힌트 | 자유도 + 안내. carry #16 의도. | ✓ |
| 슬롯별 의미 있는 방향만 노출 | Horizontal=LtoR/RtoL, Vertical=TtoB/BtoT 필터. | |

**User's choice:** 모든 슬롯×모든 옵션 허용 + tooltip + 0개 힌트

---

## Cluster B — Edit 모드 + 그리기 UX

### B.1 그리기 시작 점

| Option | Description | Selected |
|--------|-------------|----------|
| 좌클릭+드래그 시작부터 도형 그린다 | 표준 WPF 패턴. MouseLeftButtonDown→Move→Up. | ✓ |
| Hold-to-draw (스페이스바 + 드래그) | modifier 키 조합 명시적. | |
| 두 점 클릭 | 시작점 → 끝점. 드래그 아닌 클릭 패턴. | |

**User's choice:** 좌클릭+드래그 시작부터

### B.2 Edit 모드 일관성

| Option | Description | Selected |
|--------|-------------|----------|
| Edit 모드 안에서만 이동/리사이즈 (Rect+Circle+Polygon 공통) | 단일 _isEditMode gate. carry #13 의도. | ✓ |
| Drawing/Edit/View 3-state 분리 | toolbar 버튼 1개→2개. UI 복잡도 증가. | |
| Circle만 Rect 패턴으로 수렴 | carry #9 만 해결. | |

**User's choice:** Edit 모드 안에서만 이동/리사이즈 (공통)

### B.3 Delete ROI 모달

| Option | Description | Selected |
|--------|-------------|----------|
| 단일/전체 선택 모달 (현재 Datum 범위) | 3-button 컨텍스트 모달. carry #14 그대로. | ✓ |
| 단일 삭제 확인만 | 전체 삭제는 별도 Reset. | |
| 확인 없음 (즉시 삭제, undo) | undo 시스템 구현 비용. carry #14 위반. | |

**User's choice:** 단일/전체 선택 모달

---

## Cluster C — PropertyGrid 동적 노출 + 모달 정책

### C.1 동적 노출 구현 패턴

| Option | Description | Selected |
|--------|-------------|----------|
| ICustomTypeDescriptor 동적 [Browsable] | TypeDescriptor.GetProperties 시점 필터. PropertyTools 가 존중. | ✓ |
| AlgorithmType 별 wrapper 클래스 3개 | TLI/CTH/VTH 전용 wrapper. INI 충돌 우려. | |
| [Category] 그룹핑 (hide 안함) | 사용자가 접기. carry #11 의도와 충돌. | |

**User's choice:** ICustomTypeDescriptor 동적 [Browsable]

### C.2 AlgorithmType 변경 흐름

| Option | Description | Selected |
|--------|-------------|----------|
| PropertyGrid 즉시 갱신 + ROI/검출 결과 clear, btn_teachDatum 클릭 타이밍까지 검출 보류 | Phase 16 D-12/D-13 일관. ROI 보존. | ✓ |
| PropertyGrid 즉시 갱신 + ROI 도 clear | ROI 슬롯 다름 처리. 사용자 불편. | |
| 자동 재티칭 | Phase 16 D-13 위반. | |

**User's choice:** PropertyGrid 즉시 갱신 + ROI/검출 clear, 검출은 test 시점

### C.3 모달 정책

| Option | Description | Selected |
|--------|-------------|----------|
| 성공 시 모달 X (시각화만) / 실패 시 사유 모달 | teach + find 양쪽 동일. carry #6/#10. | ✓ |
| 모두 toast/snackbar | WPF 기본 toast 없음. 커스텀 컨트롤 필요. | |
| 모두 모달 | 성공 모달 빈도 높아 piling-up. | |

**User's choice:** 성공 시 모달 X / 실패 시 사유 모달

---

## Cluster D — 신규 UI 기능

### D.1 DetectedOrigin 시각화 구현 경로

| Option | Description | Selected |
|--------|-------------|----------|
| DatumConfig transient DetectedOriginRow/Col + DetectedRefAngle | volatile [Browsable(false)] 3개. TryFindDatum write-back. | ✓ |
| MainView _lastFindResult dynamic cache | INI 영향 0이지만 상태 추적 복잡. | |

**User's choice:** DatumConfig transient 필드 추가

### D.2 Hover 좌표/밝기 표시 위치

| Option | Description | Selected |
|--------|-------------|----------|
| MainView 상단 툴바 1줄 'X · Y · Gray' | TextBlock 3개 + MouseMove 핸들러. | ✓ |
| HalconViewer 자체 overlay (캠버스 구석) | 구현 복잡 (DispText 고정 + canvas absolute pos). | |
| Status bar (MainWindow 하단) | MainView ↔ MainWindow 커플링. carry #18 의도와 약간 충돌. | |

**User's choice:** MainView 상단 툴바 1줄

### D.3 결과 메트릭 PropertyGrid 노출 (carry #5)

| Option | Description | Selected |
|--------|-------------|----------|
| 결과 메트릭만 PropertyGrid 에 readonly 노출 | DetectedEdgeCount/FitRMSE/AngleDeg transient 필드. carry #5 핵심 흡수. | ✓ |
| 버튼 + 절단 스냅샷 미리보기 창 | 별도 Window. 본 phase 분량 과대. | |
| carry #5 전체를 deferred | carry-over 16 → 15 항목 감소. | |

**User's choice:** 결과 메트릭만 PropertyGrid 에 readonly 노출

---

## Claude's Discretion

다음 항목은 plan 단계에서 결정 — CONTEXT.md `<decisions>` 의 `### Claude's Discretion` 참조:

- DispCross size 정확값 (DetectedOrigin 시각화) — 12~16 사이
- DetectedOrigin 시각화 색상 — 기존 팔레트 (cyan=Line1, magenta=Line2, yellow, light green, gray) 와 충돌 회피
- btn_testFindDatum 신규 버튼 vs 기존 컨텍스트 메뉴 재활용
- AlgorithmType 변경 즉시 경고 모달 vs btn_teachDatum 시점 경고
- Hover 좌표 mm 단위 표시 추가 여부

## Deferred Ideas

CONTEXT.md `<deferred>` 참조 (carry #5 별도 미리보기 창, 3-state toolbar, mm hover, ROI 자동 마이그레이션, progress bar, DetectedOrigin mm 좌표, EdgeDirection tooltip 다국어).
