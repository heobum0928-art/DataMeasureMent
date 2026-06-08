---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "06"
subsystem: ui-roi-teaching + uat
tags: [UAT, MainView, ROI-teaching, ArcLineIntersect, CompoundAngle, CompoundCenter, CompoundShortAxis, Phase-32-final]
dependency_graph:
  requires:
    - "32-03: ArcLineIntersect EdgeA/EdgeB ROI 필드 재설계 (4-ROI 최종)"
    - "32-04: CompoundAngle / CompoundCenterC / CompoundCenterB Rect_* 필드 재작성"
    - "32-05: CompoundShortAxisDistance (E3) 신규 — 단축 환원 후 reference 알고리즘 (quick 260523-j72)"
    - "32-07: 5종 측정 overlay 시각화"
    - "32-08: ArcLineIntersect 4-ROI 재설계 + 측정점 보정"
  provides:
    - "Phase 32 신규 측정 타입 5종 + E3 의 ROI 티칭 UI 배선"
    - "Phase 32 최종 SIMUL UAT 사인오프 (사용자 approved 2026-05-23)"
  affects:
    - "MainView.xaml.cs FindSelectedRectMeasurement / CommitRectRoi / BuildPointRoiDefinitions / RectRoiButton_Click"
    - "InspectionListView.xaml.cs Rect ROI 화이트리스트"
tech_stack:
  added: []
  patterns:
    - "ArcLineIntersect 4-ROI 순차 드로잉 UX (RoiIndex 상태 머신, 1→4 순환)"
    - "as 캐스트 분기로 측정 타입별 ROI 좌표 write-back (CompoundAngle 패턴)"
    - "Shot 단위 ROI 수집 → 캔버스 갱신 (Measurement 노드 ROI 미표시 회귀 방지)"
key_files:
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
decisions:
  - "ArcLineIntersect 는 4-ROI 순차 드로잉 (Plan 32-08 4-ROI 재설계 채택). Datum 위저드 미적용 (CONTEXT.md 미해결#2)"
  - "E3 (CompoundShortAxisDistance) UAT 직전 reference 알고리즘으로 업그레이드 (quick 260523-j72) — UAT 새 6 오버레이 검증 포함"
  - "UAT 사용자 시각 검증 = 화이트리스트 6개 타입 측정값 + 판정 + 오버레이 + 실패 무크래시 모두 PASS"
metrics:
  completed_date: "2026-05-23"
  uat_approved_by: "user"
  tasks_completed: 2
  tasks_total: 2
  uat_outcome: PASS
status: complete
---

# Phase 32 Plan 06: MainView ROI 티칭 + Phase 32 최종 UAT Summary

**한 줄 요약:** Phase 32 신규 측정 타입 5종 + E3 의 MainView ROI 티칭 배선 + 최종 SIMUL UAT 사인오프 — 전 타입 측정값/판정/오버레이/실패케이스 PASS, Phase 32 완료 게이트 통과.

## 완료된 작업

| Task | 이름 | 커밋 | 비고 |
|------|------|------|------|
| 1 | E2/E3/E9/E10 Rect ROI 단일 배선 + ArcLineIntersect 4-ROI 순차 드로잉 | a1cdc27, 15fa8d8 | RectRoiButton_Click + CommitRectRoi + FindSelectedRectMeasurement + BuildPointRoiDefinitions 4 메서드 갱신 |
| 1+ | UAT 회귀 hotfix 4건 (Shot 이미지 동기화 + ROI 캔버스 갱신 + InspectionList 화이트리스트 + EdgeB ROI 렌더) | 9c482dd, 4ea5bcc, 88b5e05, 3c9a573 | UAT 진행 중 발견된 결함 즉시 수정 |
| 2 | SIMUL UAT 사용자 시각 검증 | (UAT — 코드 변경 없음) | 사용자 approved 2026-05-23 (E3 reference 알고리즘 quick 260523-j72 적용 후) |

## UAT 결과

**사용자 approved 2026-05-23 (전 항목 PASS):**

### 티칭 검증
- ✅ E2 / E3 / E9 / E10 — 단일 Rect ROI 드로잉 → Rect_Row/Col/Phi/Length1/Length2 PropertyGrid 기록
- ✅ ArcLineIntersect — 4회 순차 드로잉 (EdgeA1 / EdgeB1 / EdgeA2 / EdgeB2) → 4개 ROI 캔버스 렌더

### 측정값 + 공차 판정
- ✅ ArcLineIntersect (I9/I10) — 두 교점 측정점 (측정축=교점2, 수직축=평균) → Datum C X 거리(mm), SOP 기준 판정 정상 (32-08 측정점 보정 commit 30c478d 검증 포함)
- ✅ **E3 CompoundShortAxisDistance** — 단축 폭(mm), 공차 0.600±0.030 판정 정상 (af07972 reference 알고리즘 = LargestRect XLD + fit_line + contour intersection)
- ✅ E2 CompoundCenterC — Datum C ↔ rect center 거리
- ✅ E9 CompoundCenterB — Datum B ↔ rect center 거리
- ✅ E10 CompoundAngle — rect 장축 각도

### 오버레이 시각 확인 (E3 reference 알고리즘 6개 신규 포함)
- ✅ E3: FAI-LongEdge1 / FAI-LongEdge2 (LargestRect XLD 코너 직접) — 분석식 ±length2 가 아닌 실제 컨투어 좌표
- ✅ E3: FAI-MeasureLine (중심 통과 phi+π/2 측정선, CrossLen=500)
- ✅ E3: FAI-Intersection1 / FAI-Intersection2 (contour intersection subpixel 정확도)
- ✅ E3: FAI-DistLine (교점간 거리선)
- ✅ ArcLineIntersect: FAI-Edge1/2/3/4 + FAI-Intersection1/2 + FAI-AvgPoint + FAI-DistLine (32-07 + 32-08 통합)
- ✅ E2/E9/E10: 32-07 오버레이 정상 렌더

### 실패 케이스
- ✅ 빈 영역 ROI → 측정값 '—' + 앱 무크래시
- ✅ 평행 직선 / 노이즈 → 측정값 '—' + overlay 빈 리스트 (T-32-14/T-32-15 mitigation)

## Phase 32 종합 결과

**8 plans 모두 완료:**

| Plan | 내용 | 완료 |
|------|------|------|
| 32-01 | VisionAlgorithmService 공통 컨투어 알고리즘 (TryFindLargestContourRect) | ✅ |
| 32-02 | I9/I10 SOP 알고리즘 정합 (CompoundCenter*) | ✅ |
| 32-03 | ArcLineIntersect EdgeA/EdgeB Edge 파라미터 분리 | ✅ |
| 32-04 | CompoundAngle / CompoundCenterC / CompoundCenterB Rect_* 재작성 | ✅ |
| 32-05 | CompoundShortAxisDistance (E3) 신규 + reference 알고리즘 (quick 260523-j72) | ✅ |
| 32-06 | MainView ROI 티칭 배선 + 최종 SIMUL UAT | ✅ |
| 32-07 | 5종 측정 결과 overlay 시각화 | ✅ |
| 32-08 | ArcLineIntersect 4-ROI 재설계 + 측정점 보정 | ✅ |

## Carry-Over

None — Phase 32 의 SOP 재정합 목표 (I9/I10/E2/E9/E10 재작성 + E3 신규) 완료. 후속 작업 없음.

## Self-Check: PASSED

**Created files:** —
**Modified files (UI 배선):**
- ✅ WPF_Example/UI/ContentItem/MainView.xaml.cs (FindSelectedRectMeasurement + CommitRectRoi + BuildPointRoiDefinitions + RectRoiButton_Click + HalconViewer_RectDrawingCompleted)
- ✅ WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (Rect ROI 화이트리스트)

**Build:** msbuild Debug/x64 PASS (0 errors, baseline 외 신규 warnings 0)

**UAT:** 사용자 approved 2026-05-23 — 화이트리스트 5 타입(ArcLineIntersect/E2/E3/E9/E10) + E3 reference 알고리즘 6 오버레이 + 실패 케이스 모두 PASS
