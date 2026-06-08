---
phase: 37-side-multi-datum-dualimage-2026-05-28
plan: 02
subsystem: inspection-sequence
tags: [datum, dualimage, multi-datum, per-datum-loop, lenient]
requires:
  - InspectionSequence.TryRunDatumPhase (Plan 01 lenient 오버로드)
  - InspectionSequence.TryGetDatumTransform (이름 참조)
  - DatumFindingService.TryFindDatum (1-image + 2-image 오버로드)
  - DatumConfig.TeachingImagePath / TeachingImagePath_Vertical / AlgorithmTypeEnum
provides:
  - InspectionSequence.TryRunSingleDatum (Clear 안 함, datum 1개 누적 저장)
  - InspectionSequence.ClearDatumTransforms (loop 전 1회 초기화)
  - Action_FAIMeasurement.EStep.DatumPhase per-datum loop (DatumConfigs[0] 단일 분기 제거)
  - per-datum 이미지 로드 헬퍼 (TryGrabOrLoadDualDatumImages(DatumConfig) + GrabOrLoadDatumImage(DatumConfig))
affects:
  - Plan 03 시각화/UAT (datum 4개 각자 검출된 _datumTransforms 누적 결과를 소비)
tech-stack:
  added: []
  patterns:
    - "per-datum loop: DatumConfigs 전체 순회, datum 마다 자기 이미지로 검출 — DatumConfigs[0] 단일 가정 제거"
    - "누적 검출: loop 전 ClearDatumTransforms 1회, datum 마다 TryRunSingleDatum 으로 _datumTransforms 누적 (Clear 안 함)"
    - "lenient 호출부: datum 취득/검출 실패 = continue+log, FinishAction(Error) 전면 제거 — T-37-03 DoS mitigation"
    - "HImage finally Dispose: DualImage imgH/imgV + 1-image img — T-37-04 리소스 누수 방지"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
decisions:
  - "D-37-02: 두 datum-이미지 로드 헬퍼를 datum 인자 기반으로 변경 (TryGrabOrLoadDualDatumImages(DatumConfig) + GrabOrLoadDatumImage(DatumConfig) 신규 오버로드). DatumConfigs[0] 한정 제거."
  - "D-37-04: EStep.DatumPhase 가 datum 마다 AlgorithmTypeEnum 을 판단 (mixed DualImage/1-image 허용). TryRunSingleDatum 도 datum별 분기."
  - "D-37-05: 누적을 위해 InspectionSequence 에 Clear 안 하는 TryRunSingleDatum + 별도 ClearDatumTransforms 추가. loop 전 1회 Clear → datum 마다 누적."
  - "D-37-03: datum 부분 실패 = skip+log, action abort 없음. EStep.DatumPhase 의 FinishAction(EContextResult.Error) 4곳 전면 제거 → 항상 EStep.Grab 진행."
  - "D-37-06: EStep.Measure 무변경 — 측정은 ShotParam.GetImage() (자기 Shot 이미지), transform 은 TryGetDatumTransform(meas.DatumRef) 이름 참조. '측정 이미지 ≠ datum 이미지' 구조 기존 충족 확인."
  - "Task 2(D): 무참조 GrabOrLoadDatumImage(InspectionSequence parentSeq) 제거 (회귀 0 — 신규 datum-인자 오버로드가 전 호출부 대체)."
metrics:
  duration_min: 4
  completed: 2026-05-28
  tasks: 2
  files: 2
---

# Phase 37 Plan 02: 다중 Datum 독립 처리 (EStep.DatumPhase per-datum loop) Summary

`Action_FAIMeasurement.EStep.DatumPhase` 와 datum-이미지 로드 헬퍼를 `DatumConfigs[0]` 단일 분기에서 `DatumConfigs` 전체 per-datum loop 로 재작성하여, datum 4개 시나리오에서 각 datum 이 자기 TeachingImagePath(_Vertical) 이미지로 독립 검출되어 `_datumTransforms` 를 누적 채우도록 함. datum 부분 실패는 action 을 abort 하지 않는 lenient 호출부(FinishAction(Error) 전면 제거).

## What Was Built

- **Task 1 — datum-이미지 로드 헬퍼 per-datum 인자화**
  - `TryGrabOrLoadDualDatumImages` 시그니처를 `(InspectionSequence parentSeq, ...)` → `(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical)` 로 변경. `parentSeq.DatumConfigs[0]` 참조 제거, 진입 가드를 `if (datum == null)` 로 교체, `datum.TeachingImagePath` / `datum.TeachingImagePath_Vertical` 를 인자 datum 에서 읽음. pathH/pathV File.Exists 2-step 가드 + HImage try/catch + 실패 시 양쪽 Dispose 로직 유지.
  - `GrabOrLoadDatumImage(DatumConfig datum)` 1-image 오버로드 신규 추가 (TeachingImagePath 우선 → SimulImagePath 폴백 → GrabHalconImage 최종, SIMUL_MODE 분기).
- **Task 2 — InspectionSequence 누적 경로 + EStep.DatumPhase loop**
  - `InspectionSequence.ClearDatumTransforms()` — `_datumTransforms.Clear()` 만. loop 전 1회 호출용.
  - `InspectionSequence.TryRunSingleDatum(DatumConfig datum, HImage imageH, HImage imageV, out string error)` — Clear 안 함, datum 1개만 검출하여 `_datumTransforms[datum.DatumName ?? ""] = transform` 누적 저장. `AlgorithmTypeEnum == VerticalTwoHorizontalDualImage` 분기로 2-image / 1-image find 선택 (mixed 허용). 실패 시 `LastFindSucceeded=false` + false 반환(호출부 lenient).
  - `EStep.DatumPhase` 전체 교체: `parentSeq.ClearDatumTransforms()` (loop 전 1회) → `foreach (var datum in parentSeq.DatumConfigs)` → datum별 AlgorithmTypeEnum 판단 → DualImage 는 `TryGrabOrLoadDualDatumImages(datum,...)` + `TryRunSingleDatum(datum, imgH, imgV)`, 1-image 는 `GrabOrLoadDatumImage(datum)` + `TryRunSingleDatum(datum, img, null)`. 취득/검출 실패는 log+continue(skip). HImage 는 finally 에서 Dispose. 항상 `Step = EStep.Grab`.
  - 무참조 `GrabOrLoadDatumImage(InspectionSequence parentSeq)` 제거.

## EStep.Measure 무변경 검증 (D-37-06)

`EStep.Measure` 는 한 줄도 변경하지 않았다. read_first 로 확인: 측정은 `using (var image = ShotParam.GetImage())` 로 자기 Shot 이미지를 쓰고, datum transform 은 `parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)` 로 이름 참조한다. 이 두 줄이 "측정 이미지 ≠ datum 이미지" 분리(D-37-06)를 이미 구현하며 본 plan 의 datum loop 변경과 독립적으로 유지된다.

## Deviations from Plan

None - plan executed exactly as written. (Task 2(D) 의 무참조 헬퍼 제거 조건이 충족되어 plan 명시대로 `GrabOrLoadDatumImage(InspectionSequence)` 를 제거함 — 호출부 0 grep 확인.)

## Verification

- msbuild Debug/x64 (DatumMeasurement.sln) **PASS** — 0 error. 경고는 baseline 만 잔존 (MSB3884 ruleset, CS0618 deprecated Top/Bottom 마이그레이션, CS0162 unreachable VirtualCamera). 신규 warning 0. `DatumMeasurement.exe` 산출 확인.
- EStep.DatumPhase: `foreach (var datum in parentSeq.DatumConfigs)` loop 존재(L84), `DatumConfigs[0]` 참조 0, `FinishAction(EContextResult.Error)` 0, `parentSeq.ClearDatumTransforms()` loop 전 1회(L83).
- DualImage 경로 imgH/imgV finally Dispose, 1-image 경로 img finally Dispose 확인.
- InspectionSequence: `TryRunSingleDatum`(L197) 본문에 `_datumTransforms.Clear()` 없음(누적), `_datumTransforms[datum.DatumName ?? ""] = transform` 있음, datum별 `AlgorithmTypeEnum == VerticalTwoHorizontalDualImage` 분기 존재. `ClearDatumTransforms`(L192) 존재.
- 변경/추가 라인 전부 `//260528 hbk Phase 37` 주석 보유.

## Threat Model Compliance

- T-37-03 (DoS, mitigate): 누락/잘못된 datum 티칭 이미지 경로 → File.Exists 2-step 가드 + HImage try/catch + skip(continue)+log, crash 없이 다음 datum/측정 진행. FinishAction(Error) 제거로 abort 차단. 구현 완료.
- T-37-04 (DoS 리소스 누수, mitigate): DualImage imgH/imgV + 1-image img 를 finally 에서 Dispose — datum loop 반복 시 네이티브 핸들 누수 방지. 구현 완료.
- T-37-05 (Tampering, accept): 부분 채워진 _datumTransforms 의 미해결 DatumRef 는 EStep.Measure 기존 identity fallback(L176)으로 식별 — 현 plan 범위 밖, 기존 로직 유지.

신규 threat surface 없음 (in-process 액션 스텝 머신 + 시퀀스 누적 경로만 수정, 로컬 파일 입력만).

## Commits

- 1f72b2d: feat(37-02): datum-이미지 로드 헬퍼 per-datum 인자화 (D-37-02)
- 52bc5a8: feat(37-02): EStep.DatumPhase 다중 datum loop + 누적 검출 경로 (D-37-04/05)

## Self-Check: PASSED
