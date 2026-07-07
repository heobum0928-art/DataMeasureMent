---
phase: quick-260702-lx0
plan: 01
subsystem: inspection
tags: [refactor, extract-method, action-faimeasurement, measurement-engine]
dependency-graph:
  requires: []
  provides: [Action_FAIMeasurement.EStep.Measure orchestration + 7 private helpers]
  affects: [WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs]
tech-stack:
  added: []
  patterns: [Extract Method refactor, K&R method shells with verbatim-preserved Allman inner blocks]
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "기존 삼항(LastSkipReason 판정)은 헬퍼 이동 시 if-else 로 전개(동치 유지), 신규 삼항 0건 유지"
  - "sharedSrc 생성/Release try-finally 경계는 case EStep.Measure 본문에 그대로 유지, 헬퍼는 파라미터로만 전달받음"
metrics:
  duration: "~25min (Task 1 + Task 2) + human-verify checkpoint approved 2026-07-07"
  completed: 2026-07-02
---

# Phase quick-260702-lx0 Plan 01: Action_FAIMeasurement.cs EStep.Measure Extract Method Summary

`case EStep.Measure`(약 216줄, 인라인 로직)를 7개 의미 단위 private 헬퍼로 순수 Extract Method 리팩토링 — 로직 재배열/조건 병합 없이 오케스트레이션 흐름만 남김.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | 측정 루프 내부(per-measurement) 로직 Extract Method | d6a1823 | Action_FAIMeasurement.cs |
| 2 | Shot 단위(per-shot) 로직 Extract Method + case 재배선 + 빌드 검증 | 29344df | Action_FAIMeasurement.cs |
| 3 | checkpoint:human-verify (SIMUL $TEST 회귀 확인) | **APPROVED — 사용자 검증 완료 (2026-07-07)** | — |

## What Was Built

`case EStep.Measure`를 아래 7개 private 헬퍼 호출 중심 오케스트레이션으로 축소:

**Task 1 (per-measurement, commit d6a1823):**
1. `MarkMeasurementDatumSkipped(MeasurementBase meas, InspectionSequence parentSeq2)` — datum/align 실패로 인한 measurement skip 처리 (ClearResult/LastSkipReason/LastJudgement/로그). 원본 삼항(`IsAlignFailed(...) ? "ALIGN_FAIL" : "DATUM_FAIL"`)을 if-else 로 전개.
2. `HTuple ResolveDatumTransform(InspectionSequence parentSeq2, string datumRef)` — datum transform 해석, fixture 미존재/미지정 시 identity fallback(HomMat2dIdentity try/catch 보존).
3. `InjectDatumOrigin(MeasurementBase meas, InspectionSequence parentSeq2)` — IDatumOriginConsumer 에 검출 datum origin/각도/원중심 주입 (Allman 브레이스 블록 배치 그대로 보존).
4. `bool TryExecuteMeasurement(MeasurementBase meas, HImage image, HTuple transform, double pixRes, out double resultValue, out string measError, out List<EdgeInspectionOverlay> measOverlays)` — DualImage/1-image 분기 포함 measurement 실행. DualImage finally 에서 imgA/imgB Dispose + RuntimeImageA/B=null 리셋 순서 보존.

**Task 2 (per-shot, commit 29344df):**
5. `ApplyOverlaySuffixAndAccumulate(MeasurementBase meas, List<EdgeInspectionOverlay> measOverlays, List<EdgeInspectionOverlay> overlayAcc, List<EdgeInspectionOverlay> faiOverlays)` — FAI-Edge* overlay 에 판정 suffix(-OK/-NG) 부여 + Shot/FAI overlay 누적. `overlayAcc.AddRange` + `faiOverlays.AddRange` 순서 보존.
6. `AggregateFaiResult(FAIConfig fai, bool faiAllPass, List<EdgeInspectionOverlay> faiOverlays, SharedHImage sharedSrc, List<DatumCaptureOverlay> datumSnapshot)` — FAIConfig legacy 필드(IsPass/MeasuredValue) 집계 + wasSkip(DATUM_FAIL/ALIGN_FAIL) 판정 + QueueFaiCapture 호출. Measurements.Count>0 분기 vs 0개 분기(ClearResult+LastOverlays.Clear()) 보존.
7. `MarkAllMeasurementsNoImage(ref int measuredCount)` — image==null NO_IMAGE 캐스케이드 처리. faiHadMeas 분기(IsPass=false/MeasuredValue=0/LastOverlays.Clear() vs ClearResult()+LastOverlays.Clear()) 보존.

`sharedSrc` 생성(capSaver null 체크 포함)·Release try/finally 경계, `datumSnapshot`/`pixRes`/CorrectionFactor 가드, `allPass`/`measuredCount`/`overlayAcc` 누적, Step 전이(`pMyContext.AllPass/MeasuredCount/InspectionOverlays` 대입 후 `Step = EStep.End`)는 모두 `case EStep.Measure` 본문에 그대로 유지 — 헬퍼로 쪼개지 않음(behavioral_equivalence_invariants 준수).

## Verification

- Debug/x64 msbuild 빌드: Task 1 커밋 후 PASS, Task 2 커밋 후 PASS (오류 0, 기존 무관 경고만 존재 — CS0618 obsolete API 경고, CS0162 unreachable code 경고는 이 파일과 무관한 사전 존재 경고).
- diff 검토: 이동한 코드 블록은 verbatim(원본 텍스트 그대로 이식), 신규 삼항(?:) 0건 — 유일한 기존 삼항(LastSkipReason)은 if-else 로 전개.
- 수정/추가 라인 모두 `//260702 hbk` 주석 부여 완료.
- 경계 체크리스트(모두 확인):
  - `sharedSrc` try/finally.Release 위치 → case 본문에 유지 (line 245-288)
  - `capSaver == null` 시 CopyImage 생략 → 그대로 유지 (line 236)
  - DualImage finally Dispose + RuntimeImageA/B=null 순서 → TryExecuteMeasurement 헬퍼 내부에 보존
  - `measuredCount++` (datum-fail continue 케이스 포함) → 순서 유지
  - overlay FAI-Edge* 한정 suffix 부여 → ApplyOverlaySuffixAndAccumulate 헬퍼에 보존
  - NO_IMAGE 캐스케이드 fai 분기(faiHadMeas) → MarkAllMeasurementsNoImage 헬퍼에 보존
- 다른 case(Init/MoveZ/DatumPhase/Grab/End) 및 기존 헬퍼(GrabOrLoadDatumImage, TryGrabOrLoadDualDatumImages, TryGrabOrLoadFaiDualImages, BuildDatumCaptureSnapshot, QueueFaiCapture) 무수정 — git diff 범위가 `case EStep.Measure` 블록 및 신규 헬퍼 추가 영역에만 국한됨을 확인.
- `git diff --diff-filter=D` 확인 결과 두 커밋 모두 의도치 않은 파일 삭제 없음.

## Task 3: Checkpoint — Approved

사용자가 리팩토링 전/후 SIMUL `$TEST` 결과를 비교 검증하여 **동일함을 확인, approved** (2026-07-07). 회귀 없음 — quick-260702-lx0 최종 완료.

## Deviations from Plan

None — plan executed exactly as written for Task 1 and Task 2. The only interpretive choice made was where to preserve the `//260616 hbk simul-shot-cascade` explanatory comment (kept at the original call-site position immediately before `allPass = false;` rather than duplicating it inside `MarkAllMeasurementsNoImage`), which does not affect runtime behavior.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND: commit d6a1823 (Task 1)
- FOUND: commit 29344df (Task 2)
- Build verified PASS after both commits (Debug/x64, 0 errors)
