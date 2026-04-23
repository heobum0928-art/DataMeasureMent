---
phase: 07-overlay-regression-fix
plan: 02
subsystem: halcon-measurement
tags: [wpf, halcon, measurement, overlay, action]
requires:
  - MeasurementBase.TryExecute 6-parameter signature (Plan 01)
  - EdgePairDistanceMeasurement.TryExecute forwarding result.Overlays (Plan 01)
  - ReringProject.Halcon.Models.EdgeInspectionOverlay (unchanged)
  - ReringProject.Halcon.Display.HalconDisplayService (unchanged — L127-137 color mapping)
provides:
  - Action_FAIMeasurement Measure loop accumulates per-Measurement overlays into pMyContext.InspectionOverlays
  - FAI-Edge* overlays receive -OK/-NG suffix based on meas.LastJudgement (green/red mapping restored)
  - FAI-DistLine overlay unchanged (cyan)
  - L190 blocker line (InspectionOverlays = new List<>()) removed — replaced with overlayAcc assignment
affects:
  - none (call-site-only change; downstream rendering path already in place from Phase 3)
tech_stack:
  added: []
  patterns:
    - shot-scoped overlay accumulation (List<T>.AddRange across FAI × Measurement nested loop)
    - "judgement suffix via StringComparison.OrdinalIgnoreCase + StartsWith(\"FAI-Edge\")"
    - null-safe suffix application (skip null overlay, skip empty RoiId)
key_files:
  created:
    - .planning/phases/07-overlay-regression-fix/07-02-BUILD.log
    - .planning/phases/07-overlay-regression-fix/07-02-SUMMARY.md
    - .planning/phases/07-overlay-regression-fix/07-02-UAT-shots/ (screenshot evidence directory)
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "D-04 honored: overlayAcc declared once outside loop, AddRange per Measurement"
  - "D-05 honored: pMyContext.InspectionOverlays = overlayAcc applied once after both loops"
  - "D-06 honored: StringComparison.OrdinalIgnoreCase used for FAI-Edge detection"
  - "D-07 honored: FAI-DistLine keeps its original RoiId — no suffix"
  - "D-08 honored: suffix uses meas.LastJudgement (authoritative), not resultValue polarity"
  - "D-09 honored: EdgePair forwards via out parameter; 5 non-edge measurements pass empty lists harmlessly"
  - "D-14 honored: SIMUL_MODE + D:\\1.bmp verified green/red edges + cyan DistLine (user-approved)"
  - "Anti-goal preserved: fai.IsPass / fai.MeasuredValue / EvaluateJudgement / DataGrid flow unchanged"
metrics:
  duration_minutes: 1
  tasks_completed: 3
  files_modified: 1
  completed: "2026-04-23T00:00:00Z"
---

# Phase 7 Plan 02: Action_FAIMeasurement Measure loop overlay accumulation Summary

One-liner: Rewrote Action_FAIMeasurement's Measure step to accumulate EdgeInspectionOverlays from each Measurement via the new Plan 01 out parameter, apply judgement-based -OK/-NG suffixes to FAI-Edge* overlays only, and replace the L190 blocker initializer with the accumulated list — restoring the Phase 3 canvas visualization (green/red edges + cyan DistLine) that Phase 6 had regressed.

## Tasks Completed

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Measure 루프 재작성 — overlay 누적 + suffix 부여 + 초기화 라인 제거 | 6662ea1 | Action_FAIMeasurement.cs |
| 2 | 전체 솔루션 Debug/x64 빌드 통과 검증 | b5a857e | 07-02-BUILD.log |
| 3 | SIMUL_MODE 육안 검증 — 에지 overlay 복구 확인 (human checkpoint) | (user-approved) | 07-02-UAT-shots/ |

## Success Criteria (plan <success_criteria>)

- [x] Gap I1 해소: Action_FAIMeasurement.cs:190의 overlay 초기화 블로커 제거
- [x] ALG-04 요구사항(시각화 회귀 복구) 충족
- [x] Phase 3 에지 overlay 규약(FAI-Edge*-OK/NG 녹적 + FAI-DistLine 청록) SIMUL_MODE에서 재현
- [x] 측정값 / 공차 판정 / DataGrid 흐름 변경 없음
- [x] Phase 9 VERIFICATION 작업의 사전조건 완성

## Verification Results

- [x] `pMyContext.InspectionOverlays = overlayAcc;` 1건 존재 (Action_FAIMeasurement.cs)
- [x] `pMyContext.InspectionOverlays = new List<EdgeInspectionOverlay>();` 0건 (L190 블로커 제거됨)
- [x] `var overlayAcc = new List<EdgeInspectionOverlay>();` 1건
- [x] `out measOverlays` TryExecute 호출부 존재
- [x] `StartsWith("FAI-Edge"` + `-OK` / `-NG` suffix 분기 존재
- [x] `overlayAcc.AddRange` 존재
- [x] `fai.IsPass = faiAllPass;` 보존
- [x] `fai.MeasuredValue = fai.Measurements[0].LastMeasuredValue;` 보존
- [x] 솔루션 Debug/x64 빌드 통과 — `error CS` 0건
- [x] `bin/x64/Debug/DatumMeasurement.exe` 갱신 (빌드 로그 확인)
- [x] SIMUL_MODE 육안 검증 사용자 승인 (approved)

## Build Evidence

`.planning/phases/07-overlay-regression-fix/07-02-BUILD.log` — Debug/x64 Rebuild 통과 (0 error).

## UAT Evidence

Human checkpoint (Task 3) — user approved on 2026-04-23:
- Green/red edge pair display restored on FAI-Edge1 / FAI-Edge2
- Cyan DistLine restored on FAI-DistLine
- Tolerance toggle (pass ↔ fail) switches edge color as expected
- DataGrid numeric values and OK/NG strings unchanged (anti-goal preserved)

## Decisions Made (mirror 07-CONTEXT.md)

- **D-04** Overlay accumulation: one shot-scoped `overlayAcc` list, AddRange per Measurement.
- **D-05** Single assignment: `pMyContext.InspectionOverlays = overlayAcc` after both loops.
- **D-06** Suffix detection: `RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)`.
- **D-07** DistLine stays bare: no suffix — cyan color path preserved.
- **D-08** Suffix source: `meas.LastJudgement` (authoritative judgement result), not resultValue.
- **D-09** EdgePair-only overlays today: 5 non-edge measurements pass empty lists harmlessly.
- **D-14** Verified under SIMUL_MODE with `D:\1.bmp` — user-approved.

## Deviations from Plan

None. Plan executed exactly as specified in 07-02-PLAN.md.

## Deferred Issues

- **5-class overlay visualization** — PointToLine / PointToPoint / LineToLineAngle / LineToLineDistance / CircleDiameter still return empty overlay lists (carried from Plan 01 D-03). Requires EdgeInspectionOverlay model extensions (Circle/Arc shapes) in a future phase.

## Known Stubs

None. Empty overlay lists from the 5 non-edge measurements remain intentional (Plan 01 D-03), not placeholder code.

## Self-Check: PASSED

File existence:
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND: .planning/phases/07-overlay-regression-fix/07-02-BUILD.log
- FOUND: .planning/phases/07-overlay-regression-fix/07-02-UAT-shots/

Commit existence (verified via git log):
- FOUND: 6662ea1 (Task 1 — rewrite Measure loop)
- FOUND: b5a857e (Task 2 — build verification)
- Task 3 — user-approved 2026-04-23 (human checkpoint, no commit)
