---
phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
plan: "04"
subsystem: device-driver
tags: [mil, cxp, camera, uat, simul-mode, hw-verification]
dependency_graph:
  requires: [41-01, 41-02, 41-03 — Plan 01~03 통합 빌드]
  provides: [41-UAT.md signed_off, HW-01/HW-02 VERIFIED]
  affects: []
tech_stack:
  added: []
  patterns: []
key_files:
  created:
    - .planning/phases/41-cxp-mil-lite-10-0-grab-hw-01-hw-02/41-UAT.md
  modified: []
decisions:
  - "CO-41-01: SIMUL_MODE 에서 DeviceHandler MIL case를 #if SIMUL_MODE AddVirtualCamera 직접 폴백으로 교체 + csproj Private=True (Matrox DLL bin 복사)"
  - "CO-41-02: SIMUL은 역할과 무관하게 카메라 3대 전부 등록 + IsSequenceActive 헬퍼로 역할별 시퀀스 활성화 분리"
  - "실 HW 역할별 다중 카메라 부분 등록 경로는 현재 미고려(1대 공유만 검증) → CO-41-03 out of scope"
metrics:
  duration_seconds: ~
  completed_date: "2026-06-09"
  tasks_completed: 3
  files_modified: 1
requirements-completed: [HW-01, HW-02]
---

# Phase 41 Plan 04: SIMUL + 실 HW UAT Sign-off Summary

**Plan 01~03 통합 SIMUL/실HW 검증 완료 — CO-41-01/02 핫픽스 후 6/6 PASS, 실 HW(RapixoCXP + VP-152MX2) grab 동작 확인으로 HW-01/HW-02 최종 충족.**

## Tasks Completed

| Task | Name | Commit | 비고 |
|------|------|--------|------|
| 1 | 통합 빌드 + 정적 검증 + 41-UAT.md 스캐폴드 | 89a55e1 | 빌드 0 errors + SV-1~SV-12 전부 PASS |
| 2 | SIMUL 런타임 UAT — CO-41-01/02 핫픽스 적용 | a397039, b02b6c2 | Test 2 1차 FAIL → 핫픽스 2건 후 PASS |
| 3 | 41-UAT.md sign-off 기록 + 실 HW Test 6 | 41-UAT.md | 2026-06-09 실 HW grab 확인으로 최종 PASS |

## Verification Results

| Test | 내용 | 결과 |
|------|------|------|
| Test 1 | 통합 빌드 0 errors (SV-1~SV-12) | PASS |
| Test 2 | SIMUL_MODE 앱 기동 (IsInitializeFail 모달 없음) | PASS (CO-41-01 후) |
| Test 3 | SIMUL grab — TopBottom 역할 (CameraRoleValue=0) | PASS (CO-41-02 후) |
| Test 4 | SIMUL grab — Side 역할 (superseded by CO-41-02) | PASS (재정의) |
| Test 5 | 회귀 — Datum/FAI/결과 표시 Phase 40.1 baseline | PASS |
| Test 6 | 실 HW grab + 라이브 (RapixoCXP + VP-152MX2) | PASS (2026-06-09) |

## Hotfixes Applied

**CO-41-01 (a397039):** SIMUL_MODE 앱 기동 `FileNotFoundException(Matrox.MatroxImagingLibrary)` 크래시.
- DeviceHandler MIL case → `#if SIMUL_MODE AddVirtualCamera` 직접 폴백
- csproj `Private=False` → `True` (Matrox 관리 DLL bin 복사)

**CO-41-02 (b02b6c2):** SIMUL 샷 실행 시 "Sequence is already running" 오진단.
- SIMUL 은 카메라 3대 전부 등록 (`RegisterCxpCamera` 헬퍼 도입)
- `IsSequenceActive` 추가 — SIMUL 전체 / 실 HW 역할별 시퀀스 활성화 분리

## Real HW Notes (2026-06-09)

- 보드: **Matrox RapixoCXP** (2-connection CXP)
- 카메라: **VIEWORKS VP-152MX2-M16I0** (≈152MP, ≈14192×10640)
- 빌드: Debug|x64 (SIMUL_MODE off), 실행: Ctrl+F5 (run-time 라이선스)
- `MilCamera.Open()` = DCF 없이 M_DEFAULT → 카메라 User Set 의존 (TriggerMode Off + Mono8)
- CXP 단독 점유 필요 (Capture Works 종료 후 실행)

## Deviations from Plan

None — 빌드/정적/UAT 검증 계획대로 실행. CO-41-01/02는 SIMUL 런타임에서 발견된 핫픽스로 이 plan 범위 내 처리.

## Carry-overs

- **CO-41-03 (out of scope):** 실 HW 역할별 다중 카메라 부분 등록 경로 미검증 — 현재 "1대 공유"만 확인. 다중 카메라 미고려(사용자 2026-06-09).

## HW-01/HW-02 충족 상태

| 요구사항 | 상태 |
|----------|------|
| HW-01: MIL SDK 참조 + 빌드 성공 | SATISFIED (SV-11/12 + Test 1) |
| HW-02: MilCamera GrabHalconImage → HImage | VERIFIED (실 HW Test 6 + SIMUL Test 3/5) |

---
*Phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02*
*Completed: 2026-06-09*
