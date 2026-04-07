# Phase 2: 티칭 & 캘리브레이션 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-07
**Phase:** 02-teaching-calibration
**Areas discussed:** ROI 오버레이 표시, ROI 편집 방식, 캘리브레이션 범위, 저장 구조

---

## ROI 오버레이 표시

| Option | Description | Selected |
|--------|-------------|----------|
| 선택 FAI만 | 트리에서 FAI 선택 시 해당 ROI만 하이라이트 | ✓ |
| 전체 ROI + 선택 하이라이트 | Action의 모든 FAI ROI를 표시하고 선택만 하이라이트 | |
| 항상 전체 표시 | 이미지가 있으면 항상 모든 ROI 표시 | |

**User's choice:** 선택 FAI만
**Notes:** HalconDisplayService의 기존 selectedRoiId 패턴과 일치

### 에지 방향 표시

| Option | Description | Selected |
|--------|-------------|----------|
| 사각형 + 화살표 | ROI 사각형과 에지 검색 방향 화살표 표시 | ✓ |
| 사각형만 | ROI 영역만 표시 | |
| Claude 재량 | 기술적 난이도 고려하여 결정 | |

**User's choice:** 사각형 + 화살표

### ROI 색상

| Option | Description | Selected |
|--------|-------------|----------|
| 초록/노란 | 기본 초록, 선택 노란색 (기존 패턴) | ✓ |
| OK/NG 반영 | OK=초록, NG=빨강 반영 | |
| Claude 재량 | 기존 패턴 고려하여 결정 | |

**User's choice:** 초록/노란 (기존 패턴 유지)

### ROI 형태

| Option | Description | Selected |
|--------|-------------|----------|
| Rectangle2 (회전) | FAIConfig의 ROI_Phi 반영하여 회전 사각형 | |
| Rectangle1 (축 정렬) | 기존 DrawRectangleOutline 그대로 사용 | |

**User's choice:** Other — "Rectangle1 + 폴리곤. 마우스로 점을 하나씩 찍어서 우클릭으로 완성하는 폴리곤 ROI 추가"

### ROI 타입 병행

| Option | Description | Selected |
|--------|-------------|----------|
| Rectangle2 + 폴리곤 병행 | FAI별로 ROI 타입 선택 | |
| 폴리곤으로 통일 | 모든 ROI를 폴리곤으로 | |
| Phase 2는 Rectangle2만 | 폴리곤은 향후 | |

**User's choice:** Other — "Rectangle1 + 폴리곤으로"

### 폴리곤 측정 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 폴리곤은 시각화만 | 실제 측정은 내부 Rectangle1에서 수행 | ✓ |
| 바운딩 박스로 측정 | 폴리곤의 바운딩 사각형을 MeasurePos에 전달 | |
| Phase 3에서 결정 | 폴리곤-측정 연결은 Phase 3에서 | |

**User's choice:** 폴리곤은 시각화만

### 폴리곤 점 개수

| Option | Description | Selected |
|--------|-------------|----------|
| 3~20점 | 최소 삼각형, 최대 20점 | ✓ |
| 제한 없음 | 자유롭게 | |
| Claude 재량 | 기술적 제약 고려 | |

**User's choice:** 3~20점

### 측정값 오버레이

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 3에서 추가 | Phase 2는 ROI 시각화에 집중 | ✓ |
| Phase 2에서 자리만 마련 | 위치/포맷만 정의 | |

**User's choice:** Phase 3에서 추가

---

## ROI 편집 방식

### Rectangle1 편집

| Option | Description | Selected |
|--------|-------------|----------|
| 캔버스 드래그 | HalconViewerControl의 드래그 기능 활용 | ✓ |
| PropertyGrid 숫자 입력 | ROI_Row/Col 등 직접 입력 | |
| 둘 다 지원 | 드래그 + 미세 조정 | |

**User's choice:** 캔버스 드래그

### 편집 위치

| Option | Description | Selected |
|--------|-------------|----------|
| MainView 캔버스에서 직접 | 별도 다이얼로그 없이 직접 편집 | ✓ |
| TeachingWindow 다이얼로그 | 기존 TeachingWindow 재활용 | |
| Claude 재량 | 트레이드오프 분석 후 결정 | |

**User's choice:** MainView 캔버스에서 직접

### 폴리곤 편집

| Option | Description | Selected |
|--------|-------------|----------|
| 생성만 | 수정 시 삭제 후 재생성 | ✓ |
| 점 드래그 수정 | 꼭지점 이동 가능 | |
| 점 드래그 + 삭제 | 완전한 편집 | |

**User's choice:** 생성만

### 편집 모드 진입

| Option | Description | Selected |
|--------|-------------|----------|
| 버튼으로 모드 전환 | "ROI 설정" 버튼 토글 | ✓ |
| FAI 노드 더블클릭 | 트리 더블클릭 시 편집 모드 | |
| Claude 재량 | UI 패턴 분석 후 결정 | |

**User's choice:** 버튼으로 모드 전환

### ROI 타입 선택

| Option | Description | Selected |
|--------|-------------|----------|
| 버튼 2개 분리 | "Rect ROI" + "Polygon ROI" 별도 버튼 | ✓ |
| 드롭다운/콤보박스 | 타입 선택 후 하나의 버튼 | |
| Claude 재량 | UI 분석 후 결정 | |

**User's choice:** 버튼 2개 분리

### 버튼 위치

| Option | Description | Selected |
|--------|-------------|----------|
| 캔버스 상단 툴바 | 캔버스 영역 상단에 배치 | ✓ |
| 트리 영역 하단 | InspectionListView 하단에 FAI CRUD와 함께 | |
| Claude 재량 | UI 레이아웃 분석 후 결정 | |

**User's choice:** 캔버스 상단 툴바

---

## 캘리브레이션 범위

### 관리 레벨

| Option | Description | Selected |
|--------|-------------|----------|
| 카메라별 1개 | CameraSlaveParam에 PixelResolution 추가 | ✓ |
| 전역 1개 | SystemSetting에 저장 | |
| FAI별 개별 | FAIConfig에 필드 추가 | |

**User's choice:** 카메라별 1개

### 입력 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 수동 입력 | 알려진 해상도 직접 입력 | |
| 기준 오브젝트 측정 | 2점 클릭 + 실제 거리 입력 | ✓ |
| Halcon 캘리브레이션 | calibrate_cameras 함수 사용 | |

**User's choice:** 기준 오브젝트 측정

### 측정 흐름

| Option | Description | Selected |
|--------|-------------|----------|
| 2점 클릭 + mm 입력 | 2점 클릭 → 픽셀 거리 → mm 입력 → 자동 계산 | ✓ |
| Rectangle 드래그 + mm 입력 | 사각형 영역 드래그 후 크기 입력 | |
| Claude 재량 | 기술적 트레이드오프 분석 | |

**User's choice:** 2점 클릭 + mm 입력

---

## 저장 구조

### 저장 포맷

| Option | Description | Selected |
|--------|-------------|----------|
| INI만 | 기존 ParamBase.Save/Load 활용 | ✓ |
| INI + JSON 병행 | Rectangle은 INI, Polygon은 JSON | |
| JSON으로 통일 | TeachingStorageService 활용 | |

**User's choice:** INI만

### 폴리곤 INI 저장

| Option | Description | Selected |
|--------|-------------|----------|
| 직렬화 문자열 | "x1,y1;x2,y2;x3,y3" 형식 string 필드 | ✓ |
| 개별 필드로 저장 | Poly_X0, Poly_Y0... 각 점 별도 | |
| Claude 재량 | ParamBase 패턴 분석 후 결정 | |

**User's choice:** 직렬화 문자열

### 캘리브레이션 저장

| Option | Description | Selected |
|--------|-------------|----------|
| CameraSlaveParam INI | PixelResolution 필드 추가, 레시피별 관리 | ✓ |
| SystemSetting | Setting.ini에 전역 저장 | |
| Claude 재량 | 구조 분석 후 결정 | |

**User's choice:** CameraSlaveParam INI

---

## Claude's Discretion

- FAIConfig.ToRoiDefinition() 변환 메서드 세부 구현
- Polygon ROI 클래스의 데이터 모델 구조
- 캔버스 툴바 XAML 레이아웃
- 2점 클릭 캘리브레이션 UI 플로우 세부
- HalconDisplayService에 Polygon 렌더링 추가 방식

## Deferred Ideas

- 측정값 텍스트 오버레이 — Phase 3
- Polygon 점 드래그 수정/삭제 — v2 이후
- Rectangle2(회전) ROI — 현재 불필요
