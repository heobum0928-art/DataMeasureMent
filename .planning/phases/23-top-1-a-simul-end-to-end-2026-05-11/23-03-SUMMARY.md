---
phase: 23
plan: 03
subsystem: uat
tags: [uat, sign-off, simul-mode, partial, carry-over, blocked]
requirements_completed: []
requirements_partial: [ALG-01]
provides:
  - "23-UAT.md partial sign-off (1 PASS / 1 FAIL / 3 blocked)"
  - "Phase 23 종료 (partial) + CO-23-01 (A1~A5 측정값 미표시) carry-over 등록"
affects:
  - ".planning/phases/23-top-1-a-simul-end-to-end-2026-05-11/23-UAT.md (MOD)"
dependency_graph:
  requires:
    - "Plan 23-01: EdgeToLineDistanceMeasurement + TryFitLine selection"
    - "Plan 23-02: MeasurementFactory dispatch + GrabOrLoadDatumImage TeachingImagePath 분기"
  provides:
    - "Phase 23 partial sign-off (ALG-01 wiring 완료, 측정 결과 표시 결함 carry-over)"
  affects:
    - "v1.1 quick task (CO-23-01 디버깅) — PixelResolutionX 0 또는 MeasurementResultRow binding 단절 추적"
    - "Phase 24 (워크플로우 e2e) — A1~A5 결과 표시 정상화 후 진행 권장"
tech_stack:
  added: []
  patterns:
    - "Partial sign-off (Phase 15/17 패턴) — passed:1 / failed:1 / blocked:3 / status: partial"
    - "Carry-over 명시화 — CO-23-01 (코드 결함) + CO-23-02 (UAT blocked-by 의존 시나리오)"
key_files:
  created: []
  modified:
    - ".planning/phases/23-top-1-a-simul-end-to-end-2026-05-11/23-UAT.md"
decisions:
  - "Phase 23 partial sign-off — SC#1 FAIL (A1~A5 측정값 미표시) + SC#2/3/4 blocked (SC#1 의존) + SC#5 PASS (msbuild)"
  - "CO-23-01 carry-over 등록 — 디버깅 미완료 (가능 원인: PixelResolutionX = 0 또는 UI binding/refresh 단절)"
  - "ALG-01 wiring 완료 (코드 통합 OK, 시퀀스 완주, Datum 동작) — UI 결과 표시 결함만 carry-over → 요구사항 status = Partial"
  - "Phase 23 종료 + Phase 24 (또는 v1.1 quick CO-23-01) 로 이동 — 사용자 확인 'Phase 23 partial sign-off — 현 상태 commit 후 버그 carry-over 등록' (2026-05-13)"
metrics:
  duration_minutes: 60
  completed_date: "2026-05-13"
  tasks_count: 6
  files_created: 1
  files_modified: 1
  commits_count: 1
---

# Plan 23-03 SUMMARY — UAT partial sign-off

## 결과 요약

| Scenario | Result | 비고 |
|----------|--------|------|
| SC#1 Simul end-to-end | ❌ FAIL | 측정값 미표시 (CO-23-01) |
| SC#2 OK/NG strip | ⏸ blocked | SC#1 의존 |
| SC#3 TeachingImagePath 분리 | ⏸ blocked | SC#1 의존 |
| SC#4 A6 확장성 | ⏸ blocked | SC#1 의존 |
| SC#5 msbuild PASS | ✅ PASS | build_23_w3.log: 0 err / 6 warn |

**Sign-off:** partial (heobum0928-art, 2026-05-13)

## 결함 (CO-23-01)

A1~A5 EdgeToLineDistance 측정값/판정 컬럼이 InspectionListView 에 `—` (값 없음) 으로 표시.

**관찰 사실:**
- 시퀀스 정상 완주 (Manual Tools Locked transient → 해제, Sequence Finished 도달)
- Datum CTH 정상 동작 (Datum.Line2 strip-loop 50 edges → trim 30 edges 정상 로그)
- Error 로그에 `[FAIMeasurement] Measurement '...' failed: ...` 메시지 부재 → `TryExecute` 가 `true` 리턴
- Error 로그의 `Property set method not found` 다발은 `MeasurementBase.TypeName` get-only 의 reflection 노이즈 (모든 Measurement 타입 공통, 본 결함과 무관)

**가능한 원인 후보 (디버깅 미완료):**
- (a) `FAIConfig.PixelResolutionX = 0` → `EdgeToLineDistanceMeasurement.TryExecute` L97 `resultValue = -datumRow * pixelResolution = 0` (조용히 0)
- (b) `MeasurementResultRow` (또는 InspectionListView 결과 컬럼) UI binding/refresh 단절

## ALG-01 충족 상태

- 코드 통합 (MeasurementFactory dispatch + EdgeToLineDistanceMeasurement) ✅
- Datum CTH + EdgeToLineDistance 시퀀스 완주 ✅
- A1~A5 측정값 표시 ❌ (CO-23-01)
- → **Partial** (요구사항 정의의 측정값 UI 표시 부분 결함)

## 다음 단계

1. v1.1 quick task 로 CO-23-01 디버깅 — `FAIConfig.PixelResolutionX` 값 확인 우선, 다음 binding 단절 추적
2. CO-23-01 해소 후 SC#2/3/4 재실행 (SC#1 회복 의존)
3. 또는 Phase 24 (워크플로우 e2e) 로 진행 + CO-23-01 별도 트랙

## 변경 파일

- 23-UAT.md (Actual/Result/Notes/Summary/Carry-overs/Sign-off 갱신)
- 23-03-SUMMARY.md (신규)
