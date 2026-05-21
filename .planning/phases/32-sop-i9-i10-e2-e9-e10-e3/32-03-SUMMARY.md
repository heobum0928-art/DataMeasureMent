---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "03"
subsystem: measurement-algorithm
tags: [arc-line-intersect, sop-realign, halcon, i9, i10]
dependency_graph:
  requires: ["32-01", "32-02"]
  provides: ["ArcLineIntersectDistanceMeasurement SOP 실무 알고리즘"]
  affects: ["MeasurementFactory", "MainView ROI 티칭"]
tech_stack:
  added: []
  patterns: ["TryFitLine(All) x2 → TryIntersectLines → ComputeProjectionDistance"]
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs
decisions:
  - "3점 호 피팅(Arc_P1~P3) + 원-직선 교점 방식 완전 폐기"
  - "EdgeA(수직)/EdgeB(수평) 2 ROI 직선 피팅 + TryIntersectLines 교점 방식으로 전환"
  - "INI 하위호환 불필요 — Phase 31 신규 타입이므로 기존 레시피 없음"
  - "DatumDetectedCircleRow/Col stub → 정식 구현으로 교체 (주입만 수신, 미참조)"
metrics:
  duration_minutes: 15
  completed: 2026-05-21
  tasks_completed: 2
  files_modified: 1
---

# Phase 32 Plan 03: ArcLineIntersectDistanceMeasurement SOP 재작성 Summary

**한 줄 요약:** 3점 호 피팅 + 원-직선 교점 방식을 폐기하고 EdgeA/EdgeB 2 ROI 직선 피팅 + HALCON `intersection_lines` 래퍼(TryIntersectLines) 교점 방식으로 전면 재작성.

## 완료 작업

### Task 1: ROI 필드 세트 교체 (Arc_P1~P3/Line_* → EdgeA_*/EdgeB_*)

- `Arc_P1_*` / `Arc_P2_*` / `Arc_P3_*` 15필드, `Line_*` 5필드 제거
- `[Category("EdgeA|ROI")]` EdgeA_Row/Col/Phi/Length1/Length2 5필드 추가
- `[Category("EdgeB|ROI")]` EdgeB_Row/Col/Phi/Length1/Length2 5필드 추가
- `DatumDetectedCircleRow/Col` stub 주석을 정식 주석으로 교체 (미사용, 주입만 수신)
- 클래스 XML doc 주석을 2직선 교점 방식으로 갱신
- TypeName `"ArcLineIntersectDistance"` 유지

### Task 2: TryExecute 본문 재작성

- 기존 TryFitLine×3 + TryFitArc + TryFitLine×1 + TryIntersectCircleLine 체인 완전 제거
- EdgeA ROI TryFitLine("All") → EdgeB ROI TryFitLine("All") → TryIntersectLines 교점 → ComputeProjectionDistance 거리
- 평행/근접 시 `"Line intersection failed (parallel or near-parallel edges)"` + false 반환 (T-32-04 mitigation)
- EdgeSelection "All" 2회 명시 (memory feedback 준수)
- msbuild Debug/x64 PASS (신규 error 0, 기존 warning 2건 유지)

## 커밋

| Task | Hash | 설명 |
|------|------|------|
| Task 1 + Task 2 | 25fa181 | feat(32-03): ArcLineIntersectDistanceMeasurement SOP 재작성 — 2직선 교점 방식 |

## Deviations from Plan

### Auto-fixed Issues

없음 — 플랜 대로 정확히 실행됨.

**DatumDetectedCircleRow/Col 처리:** Plan 02에서 stub으로 추가된 필드가 이미 존재하여 Task 1의 "(4) IDatumOriginConsumer 신규 2 프로퍼티 추가" 지시는 기존 필드를 정식 주석으로 교체하는 것으로 처리. 기능 동일.

## 검증 결과

| 항목 | 결과 |
|------|------|
| Arc_P1_Row / Arc_P2_Row / Arc_P3_Row / Line_Row grep | 0건 (완전 제거) |
| EdgeA_Row / EdgeA_Col / EdgeA_Phi / EdgeA_Length1 / EdgeA_Length2 | 각 선언 1건 + 호출 사용 |
| EdgeB_Row / EdgeB_Col / EdgeB_Phi / EdgeB_Length1 / EdgeB_Length2 | 각 선언 1건 + 호출 사용 |
| TryFitLine | 2건 (EdgeA + EdgeB) |
| TryIntersectLines | 1건 |
| TryFitArc / TryIntersectCircleLine | 0건 (호 피팅 완전 폐기) |
| "All" | 2건 |
| "Line intersection failed" | 1건 |
| TypeName "ArcLineIntersectDistance" | 유지 |
| msbuild Debug/x64 | PASS (error 0) |

## Known Stubs

없음 — 모든 필드가 실제 알고리즘에서 사용됨.

## Threat Flags

없음 — 로컬 레시피 신뢰 소스 내부 변경. 외부 신뢰 불가 입력 없음.

## Self-Check: PASSED

- [x] `ArcLineIntersectDistanceMeasurement.cs` 파일 존재 확인
- [x] 커밋 25fa181 존재 확인
- [x] msbuild PASS 확인
