---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: 08
subsystem: vision-measurement
tags: [halcon, edge-measurement, roi-teaching, arc-line-intersect, I9/I10, wpf]

# Dependency graph
requires:
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    plan: 03
    provides: "ArcLineIntersectDistanceMeasurement 2-ROI 초기 구현 (이 plan 에서 4-ROI 로 교체)"
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    plan: 06
    provides: "MainView ArcLineIntersect 2-ROI 순차 드로잉 배선 (이 plan 에서 4-ROI 로 확장)"
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    plan: 07
    provides: "overlay 인프라 (EdgeInspectionOverlay 구조, foot 오버로드 ComputeProjectionDistance)"
provides:
  - "ArcLineIntersectDistanceMeasurement 4-ROI 두 교점 평균 설계: EdgeA1/B1/A2/B2 ROI+Edge 파라미터 4그룹"
  - "TryExecute: TryFitLine 4회 + TryIntersectLines 2회 + avgRow/Col 평균점 + ComputeProjectionDistance foot 오버로드"
  - "overlay 8개: FAI-Edge1/2/3/4, FAI-Intersection1/2, FAI-AvgPoint, FAI-DistLine(footOk 조건부)"
  - "MainView 4-ROI 순차 드로잉: CommitRectRoi index 0→1→2→3, drawHint 4단계, BuildPointRoiDefinitions 4 RoiDefinition"
affects: [UAT-I9/I10, Phase32-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "4-ROI 순차 드로잉 상태머신: _editingMeasurementRoiIndex 0→3 확장, 각 단계에서 return + 재무장 패턴"
    - "두 교점 평균점 측정: TryIntersectLines 2회 호출, avgRow/Col = (int1+int2)/2 계산"
    - "overlay ADDITIVE 원칙: return true 직전에만 추가, HALCON 재호출 없음"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "4-ROI 설계 채택: 교점1(A1∩B1) + 교점2(A2∩B2) 평균점 → Datum C X 거리. SOP I9/I10 실무 알고리즘과 일치."
  - "INI 하위호환 불필요: Phase 32 사전-릴리스, 구 필드명(EdgeA_*/EdgeB_*) 완전 폐기."
  - "PropertyGrid 카테고리 접두사 Tab|Group 패턴: 교점1|EdgeA1-ROI, 교점2|EdgeB2-Edge 등 8개 카테고리."
  - "T-32-14 mitigation: RectRoiButton_Click 진입 시 _editingMeasurementRoiIndex = 0 초기화 — 기존 코드에서 이미 적용됨(L1320)."
  - "T-32-15 mitigation: 각 TryIntersectLines 실패 시 즉시 return false + 빈 overlay 유지."

patterns-established:
  - "다중 ROI 4단계 순차 드로잉: index 0/1/2 에서 return + RectDrawingCompleted 재무장 + StartRectangleDrawing, index 3(else) 에서 종결 흐름 진입"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-05-21
---

# Phase 32 Plan 08: ArcLineIntersect I9/I10 4-ROI 두 교점 평균 재설계 Summary

**ArcLineIntersectDistanceMeasurement 를 4-ROI(교점1 A1/B1 + 교점2 A2/B2) 두 교점 평균 방식으로 전면 재작성하고, MainView 에서 4회 순차 ROI 드로잉을 지원하는 배선 확장 완료**

## 성능 지표

- **소요 시간:** 약 25분
- **완료일:** 2026-05-21
- **수행 태스크:** 3
- **수정 파일:** 2

## 주요 성과

- 기존 2-ROI 단일 교점 설계(Plan 32-03)를 폐기하고 4-ROI 두 교점 평균 방식으로 완전 교체
- TryExecute: TryFitLine 4회 + TryIntersectLines 2회 + avgRow/Col 평균점 → ComputeProjectionDistance(foot 오버로드) 1회
- overlay 8개(FAI-Edge1/2/3/4, FAI-Intersection1/2, FAI-AvgPoint, FAI-DistLine) 완성
- MainView CommitRectRoi 4단계 인덱스 분기(0→EdgeA1, 1→EdgeB1, 2→EdgeA2, 3→EdgeB2) + drawHint 4단계 메시지
- BuildPointRoiDefinitions ArcLineIntersect 분기 4개 RoiDefinition 반환으로 확장
- msbuild Debug/x64 PASS, 신규 컴파일 오류 0건

## 태스크 커밋 목록

1. **Task 1: ArcLineIntersectDistanceMeasurement 4-ROI 전면 재작성** - `8c4b356` (feat)
2. **Task 2: MainView 4-ROI 순차 드로잉 확장** - `041055d` (feat)
3. **Task 3: 구 필드명 잔존 grep + 빌드 검증** - 별도 커밋 없음 (grep 0건 확인 + Build succeeded)

**Plan 메타데이터:** (아래 docs 커밋)

## 수정 파일 목록

- `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` — 4-ROI 전면 재작성: EdgeA1/B1/A2/B2 ROI+Edge 파라미터 4그룹(각 5+7=12필드), TryExecute 재작성(TryFitLine×4, TryIntersectLines×2, avgRow/Col, ComputeProjectionDistance foot 오버로드), overlay 8개
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — ArcLineIntersect 3곳 수정: (A) RectRoiButton_Click drawHint "교점1 수직 에지(EdgeA1)", (B) CommitRectRoi ali 분기 4단계, (C) BuildPointRoiDefinitions ali 분기 4 RoiDefinition

## 결정 사항

- **4-ROI 설계 채택:** SOP I9/I10 실무 요건(두 교점 평균) 반영. 단일 교점 방식은 재현성 저하 확인(UAT).
- **INI 하위호환 불필요:** Phase 32 사전-릴리스이므로 구 필드명(EdgeA_Row/EdgeB_Row) 완전 폐기.
- **PropertyGrid 카테고리:** `교점1|EdgeA1-ROI`, `교점1|EdgeA1-Edge` 등 Tab|Group 프리픽스 패턴 유지 (기존 UAT 결함 패턴 재현 방지).

## 계획 대비 편차

없음 — 계획대로 정확히 실행됨.

## 발생 이슈

없음.

## Known Stubs

없음 — 4-ROI 측정 알고리즘 완전 구현, 빈 리스트/0값 stub 없음.

## 다음 Phase 준비 상태

- ArcLineIntersectDistanceMeasurement I9/I10 4-ROI 두 교점 평균 설계 완성 — UAT 준비 완료
- MainView 4회 순차 ROI 드로잉 배선 완성 — 티칭 UX 준비 완료
- Phase 32 전체 6 plans 완료 — Phase 32 Sign-off UAT 대기

---
*Phase: 32-sop-i9-i10-e2-e9-e10-e3*
*Completed: 2026-05-21*
