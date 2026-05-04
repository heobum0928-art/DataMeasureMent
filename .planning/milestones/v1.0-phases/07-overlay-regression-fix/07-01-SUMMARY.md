---
phase: 07-overlay-regression-fix
plan: 01
subsystem: halcon-measurement
tags: [wpf, halcon, measurement, overlay, signature-refactor]
requires:
  - ReringProject.Halcon.Models.EdgeInspectionOverlay (unchanged)
  - ReringProject.Halcon.Algorithms.FAIEdgeMeasurementService.TryMeasure (unchanged)
provides:
  - MeasurementBase.TryExecute 6-parameter abstract signature (out List<EdgeInspectionOverlay> overlays)
  - EdgePairDistanceMeasurement forwards FAIEdgeMeasurementService.result.Overlays via out parameter
  - 5 non-edge measurements return empty overlay lists (visualization deferred)
affects:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs (intentional break — Plan 02 rewrites caller)
tech_stack:
  added: []
  patterns:
    - out-parameter rollout across abstract base + 6 concrete derivations
    - "null-safe: overlays pre-initialized to empty list before every return path"
key_files:
  created:
    - .planning/phases/07-overlay-regression-fix/07-01-BUILD.log
    - .planning/phases/07-overlay-regression-fix/07-01-SUMMARY.md
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/PointToPointDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs
decisions:
  - "D-01 signature verbatim: out List<EdgeInspectionOverlay> overlays appended as 6th parameter"
  - "D-02 honored: overlays initialized to non-null empty list before every short-circuit return"
  - "D-03 honored: 5 non-edge measurements return empty list — visualization deferred"
  - "D-09/D-10 honored: EdgePair forwards FAIEdgeMeasurementService.result.Overlays as-is"
  - "D-11/D-12/D-13 honored: EdgeInspectionOverlay model, HalconDisplayService, FAIEdgeMeasurementService untouched"
  - "Plan 01 scoped exception: Action_FAIMeasurement.cs call-site CS7036 accepted — Plan 02 rewrites the Measure loop"
metrics:
  duration_seconds: 214
  duration_minutes: 4
  tasks_completed: 4
  files_modified: 7
  completed: "2026-04-22T08:11:22Z"
---

# Phase 7 Plan 01: MeasurementBase TryExecute overlay return path Summary

One-liner: Added `out List<EdgeInspectionOverlay> overlays` to MeasurementBase and propagated the signature across all 6 derived measurement classes, with EdgePairDistanceMeasurement forwarding `FAIEdgeMeasurementService.result.Overlays` and the other 5 returning empty lists pending deferred visualization implementations.

## Tasks Completed

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | MeasurementBase abstract 시그니처에 out overlays 추가 | df4e24a | MeasurementBase.cs |
| 2 | EdgePairDistanceMeasurement override에 result.Overlays 전달 | 3e73191 | EdgePairDistanceMeasurement.cs |
| 3 | 5종 Measurement override — 빈 overlay 리스트 반환 | c426415 | PointToLineDistanceMeasurement.cs, PointToPointDistanceMeasurement.cs, LineToLineAngleMeasurement.cs, LineToLineDistanceMeasurement.cs, CircleDiameterMeasurement.cs |
| 4 | 빌드 검증 — Debug x64 | 7787265 | 07-01-BUILD.log |

## Success Criteria (plan <success_criteria>)

- [x] 6개 파생 Measurement 클래스가 새 abstract 시그니처와 일치하여 override 누락 오류 없음 — CS0534 count: 0
- [x] EdgePair는 FAIEdgeMeasurementService 산출 overlay를 out 파라미터로 전달 가능한 상태 — `if (result.Overlays != null) overlays = result.Overlays;`
- [x] 5종은 빈 리스트로 Phase 7 스코프 최소화 원칙 준수 (D-03, D-11)
- [x] Plan 02가 Action_FAIMeasurement 루프를 재작성할 수 있는 시그니처 기반 완성

## Verification Results

- [x] MeasurementBase.cs abstract TryExecute에 `out List<EdgeInspectionOverlay> overlays` 존재 — L53
- [x] 6개 파생 클래스가 전부 6-파라미터 override — grep confirms `out List<EdgeInspectionOverlay> overlays` in all 6 files
- [x] EdgePair만 `overlays = result.Overlays` 전달 (EdgePairDistanceMeasurement.cs:94), 5종은 `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트
- [x] EdgeInspectionOverlay.cs, FAIEdgeMeasurementService.cs, FAIEdgeMeasurementResult.cs, HalconDisplayService.cs 파일 수정 없음 — `git diff --name-only HEAD~4 HEAD` excludes these
- [x] 빌드 로그에서 CS0534(override 미구현) 0건
- [x] 모든 수정 라인에 `//260422 hbk` 주석 부착 — CLAUDE.md memory convention

## Build Evidence

`.planning/phases/07-overlay-regression-fix/07-01-BUILD.log` captured. Key findings:

- `error CS0534`: **0**
- Errors in MeasurementBase + 6 derived class files: **0**
- Remaining error — exactly one, at the expected Plan 02 boundary:
  ```
  Custom/Sequence/Inspection/Action_FAIMeasurement.cs(157,55): error CS7036:
  'MeasurementBase.TryExecute(HImage, HTuple, double, out double, out string,
   out List<EdgeInspectionOverlay>)'의 필수 매개 변수 'overlays'에 해당하는 인수가 없습니다.
  ```
  This is the predicted Plan 01 scoped exception — caller-site signature mismatch in the Measure loop, to be fixed in Plan 02 along with overlay accumulation.

## Decisions Made (mirror 07-CONTEXT.md)

- **D-01** Signature extension: new parameter appended at the end (C# 7.2 compatible).
- **D-02** Failure-path safety: overlays pre-initialized to `new List<EdgeInspectionOverlay>()` before the first `return false`, so callers always receive a non-null list.
- **D-03** Deferred 5-class visualization: empty list reply keeps DataGrid numeric flow intact while leaving canvas visualization to future phases.
- **D-09/D-10** EdgePair overlay path: `result.Overlays` (FAI-Edge1/Edge2/DistLine) forwarded unmodified; judgement suffixing deferred to Plan 02's Measure loop.
- **D-11/D-12/D-13** Scope discipline: model/service files untouched, validated via `git diff --name-only HEAD~4 HEAD`.

## Deviations from Plan

None — plan executed exactly as written.

The plan's Task 4 already anticipated the Action_FAIMeasurement.cs CS7036 error as a scoped Plan 01 boundary; it is documented in the build log and Plan 02's mandate.

## Deferred Issues

- **Action_FAIMeasurement.cs:157 caller-site 6th argument** — handed off to Plan 02 Task 2 as specified in 07-01-PLAN.md L293-296.
- **5-class overlay visualization** (PointToLine/PointToPoint/LineToLineAngle/LineToLineDistance/CircleDiameter) — empty lists today; each requires additional EdgeInspectionOverlay model fields (Circle/Arc) and is deferred per 07-CONTEXT.md `<deferred>`.

## Known Stubs

None. Empty overlay lists in the 5 non-edge measurements are documented design (D-03), not placeholder stubs — the values still flow correctly to DataGrid; only canvas overlays are absent by intent.

## Self-Check: PASSED

File existence:
- FOUND: WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/PointToPointDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineAngleMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs
- FOUND: .planning/phases/07-overlay-regression-fix/07-01-BUILD.log

Commit existence (verified via git log --oneline -5):
- FOUND: df4e24a (Task 1)
- FOUND: 3e73191 (Task 2)
- FOUND: c426415 (Task 3)
- FOUND: 7787265 (Task 4)
