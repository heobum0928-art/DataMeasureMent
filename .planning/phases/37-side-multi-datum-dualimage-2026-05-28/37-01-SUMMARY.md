---
phase: 37-side-multi-datum-dualimage-2026-05-28
plan: 01
subsystem: inspection-sequence
tags: [datum, lenient, dualimage, robustness]
requires:
  - InspectionSequence.TryRunDatumPhase (기존 strict 구현)
  - DatumFindingService.TryFindDatum (1-image + 2-image 오버로드)
provides:
  - lenient TryRunDatumPhase 1-image 오버로드 (부분 성공 = true)
  - lenient TryRunDatumPhase 2-image 오버로드 (per-datum DualImage 판단 유지)
affects:
  - Plan 02 호출부 (부분 성공 = true 반환, 실패 datum 은 _datumTransforms 미저장 계약)
tech-stack:
  added: []
  patterns:
    - "datum find 실패 = continue+log (abort 아님) — T-37-01 DoS mitigation"
    - "성공 datum 만 _datumTransforms 저장 (부분 채움)"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
decisions:
  - "D-37-03: datum find 실패 시 return false → continue+log 로 전환, 항상 true(부분성공 포함) 반환"
  - "D-37-04: 2-image 오버로드의 per-datum AlgorithmType 분기를 loop 안에 유지 (DatumConfigs[0] 단일 판단 아님 — 이미 충족되어 있어 abort 만 제거)"
  - "Logging/ELogType using 신규 추가 (ReringProject.Utility / ReringProject.Setting)"
metrics:
  duration_min: 6
  completed: 2026-05-28
  tasks: 2
  files: 1
---

# Phase 37 Plan 01: TryRunDatumPhase Lenient 전환 Summary

`InspectionSequence.TryRunDatumPhase` 두 오버로드(1-image, 2-image)를 strict(한 datum 실패 시 전체 abort)에서 lenient(실패 datum 만 skip+log, 성공만 저장, 항상 true 반환)로 전환하여 Side 다중 datum 독립 검출의 실행 루프 토대를 마련.

## What Was Built

- **1-image 오버로드** (`TryRunDatumPhase(HImage image, out string error)`): foreach 안 datum-find 실패 분기를 `return false` → `datum.LastFindSucceeded = false` + `Logging.PrintLog(ELogType.Error, ...)` + `continue` 로 교체. image == null 가드(진짜 호출 오류)는 `return false` 유지. 메서드는 항상 `return true` 로 종료.
- **2-image 오버로드** (`TryRunDatumPhase(HImage image1, HImage image2, out string error)`): per-datum `AlgorithmTypeEnum == VerticalTwoHorizontalDualImage` 분기를 loop 안에 그대로 보존(D-37-04 충족)하면서, 실패 분기만 `return false` → skip+log+continue 로 교체. image1/image2 null 가드는 유지.
- **using 절**: `using ReringProject.Setting;` (ELogType) + `using ReringProject.Utility;` (Logging) 추가.

## Decisions Made

- **D-37-03**: 부분 성공 = true. 실패 datum 은 `_datumTransforms` 에 미저장 → Plan 02 측정부가 해당 DatumRef 미해결을 식별. T-37-01 (DoS) mitigation 직접 구현.
- **D-37-04**: 2-image 오버로드는 이미 per-datum AlgorithmType 분기를 하고 있었으므로 구조 보존만 함. DatumConfigs[0] 단일 판단 제거 요구는 기존 코드가 이미 충족.
- 시그니처(`out string error`) 무변경 → 기존 호출부 컴파일 호환 (Plan 02 변경 없이도 빌드 통과).

## Deviations from Plan

None - plan executed exactly as written.

## Verification

- msbuild Debug/x64 PASS — 0 새 error, 0 새 warning. baseline 경고만 잔존 (MSB3884 ruleset, CS0618 deprecated Top/Bottom 마이그레이션 x6, CS0162 unreachable). `DatumMeasurement.exe` 산출 확인.
- 1-image + 2-image 오버로드 모두: datum-find 실패 분기에 `continue;` 존재, `return false` 제거(null 가드 제외), `return true` 종료.
- 2-image: per-datum `AlgorithmTypeEnum == VerticalTwoHorizontalDualImage` 분기 loop 안 유지 확인.
- 변경/추가 라인 전부 `//260528 hbk Phase 37` 주석 보유.

## Threat Model Compliance

- T-37-01 (DoS, mitigate): datum find 실패를 abort 대신 continue+log 처리 — 구현 완료.
- T-37-02 (Tampering, accept): 실패 datum transform 미저장 → Plan 02 측정별 처리에 위임 (현 plan 범위 밖).

## Commits

- bb6df4d: fix(37-01): TryRunDatumPhase 1-image lenient 전환 (D-37-03)
- d861cee: fix(37-01): TryRunDatumPhase 2-image lenient 전환 (D-37-03/D-37-04)

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: .planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-01-SUMMARY.md
- FOUND: commit bb6df4d
- FOUND: commit d861cee
