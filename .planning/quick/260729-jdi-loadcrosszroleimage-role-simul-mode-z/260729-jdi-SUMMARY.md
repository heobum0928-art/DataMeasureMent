---
phase: quick-260729-jdi
plan: 01
subsystem: fai-measurement
tags: [cross-z, dual-image, simul-mode, fai-measurement, bottom, teaching-image]
dependency-graph:
  requires: []
  provides: [LoadCrossZRoleImage-simul-gate-removed]
  affects: [Action_FAIMeasurement.ProcessCrossZCaptureTick]
tech-stack:
  added: []
  patterns: [preprocessor-gate-removal, policy-unification-with-existing-non-cross-z-path]
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "런타임 게이트(OfflineInspectMode 등) 추가하지 않음 — 비-크로스-Z 경로와 동일 정책 유지 (D-1)"
  - "ResolveFaiImageASource 재사용하지 않음 — 계약 차이로 인한 회귀 확대 방지, 대신 SIMUL 블록 본문을 그대로 게이트 밖으로 승격 (D-2)"
  - "Dispose 소유권 변경 없음 — 두 분기 모두 새 HImage 인스턴스 반환, 유일 소유자는 ProcessCrossZCaptureTick 의 using (D-3)"
  - "dualMeas null 방어 코드 추가하지 않음 — 유일 호출부가 null 아님을 보장 (D-4)"
metrics:
  duration: "~15 minutes (Task 1-2)"
  completed: "2026-07-29"
---

# Quick Task 260729-jdi: LoadCrossZRoleImage SIMUL_MODE 게이트 제거 Summary

크로스-Z(ZIndexA/ZIndexB) DualImage 측정이 `SIMUL_MODE` 가 정의되지 않은 빌드 구성(Debug|x64)에서 role별 교시 이미지를 무시하고 Shot 의 단일 오프라인 이미지를 두 role 모두에 쓰던 결함을 닫음.

## What Was Built

`Action_FAIMeasurement.LoadCrossZRoleImage`에서 `#if SIMUL_MODE` / `#else` / `#endif` 전처리 구조를 제거하고, 기존 SIMUL 블록 본문(role별 교시 경로 선택 → `File.Exists` 검사 → `new HImage(path)` → 예외 시 `ShotParam.GetImage()` 폴백)을 조건 없이 항상 실행되는 메서드 본문으로 승격했다. `#else` 의 무조건 `return ShotParam.GetImage();` 한 줄은 삭제했다.

이로써 크로스-Z 경로가 비-크로스-Z 경로(`ResolveFaiImageASource`/`TryGrabOrLoadFaiDualImages`)와 동일한 "경로가 설정되어 있으면 항상 그 파일 사용, 아니면 라이브 폴백" 정책으로 통일되었다. role 매핑(A→`TeachingImagePath_Horizontal`, B→`TeachingImagePath_Vertical`)은 한 글자도 바뀌지 않았다.

로그 문구에서 `SIMUL` 표현만 제거(동작 변경 아님): `"[FAI CrossZ] SIMUL role ..."` → `"[FAI CrossZ] role ..."`, 에러 로그도 동일하게 조정.

메서드 위 주석 블록과 `ProcessCrossZCaptureTick` 위 주석에서 "SIMUL_MODE" 한정어를 제거하고, 게이트를 벗긴 이유(빌드 구성 무관 결함 재현)와 정책 통일 근거를 기록하는 이력 주석(`//260729 hbk quick-fix(260729-jdi)`)을 추가했다.

## Tasks Completed

1. **Task 1: LoadCrossZRoleImage 의 #if SIMUL_MODE 게이트 제거** — commit `5cec861`
   - 파일: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
   - VERIFY.sh 전체 카운트 want 값과 일치 확인 (simul_gate_count=3, simul_log_left=0, live_fallbacks=2, role_a_map=3, role_b_map=2, no_csharp8_added=0 등)
   - diff hunk 범위가 `LoadCrossZRoleImage` 및 주변 주석에만 한정, `ResolveFaiImageASource`/`TryGrabOrLoadFaiDualImages` 는 diff 에 없음
   - `git diff --name-only` 소스 파일이 `Action_FAIMeasurement.cs` 하나이고 `DatumMeasurement.csproj` 무변경 확인

2. **Task 2: Debug|x64 빌드 + 바이너리 최신화** — 코드 변경 없음 (빌드 전용)
   - MSBuild `Debug|x64` (SIMUL_MODE 없는 구성) 로 빌드
   - 실행 중인 `DatumMeasurement.exe` 없음을 사전 확인 (tasklist) — MSB3021/3026/3027 발생 안 함
   - `error CS` 0, 신규 `warning CS` 0 (기존 CS0618 5건만 존재, 제외 대상)
   - `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` 수정 시각: **2026-07-29 14:12:21** (빌드 완료 직후, 방금 갱신된 바이너리 확인)

3. **Task 3: 실기 확인 — 크로스-Z 측정 복구 + 기존 동작 무회귀** — BLOCKED (human-verify checkpoint, 아래 참조)

## Deviations from Plan

None - Task 1/2 는 계획대로 정확히 실행됨.

## Auth Gates

None.

## Known Stubs

None.

## Threat Flags

None — 이번 수정은 기존 신뢰 경계(레시피 경로 → 파일 로드)를 그대로 유지하며, 게이트 제거로 두 빌드 구성 간 동작 비대칭만 제거했다. 신규 네트워크/인증/스키마 표면 없음.

## Self-Check

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — FOUND (수정됨)
- `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` — FOUND (2026-07-29 14:12:21 갱신)
- commit `5cec861` — FOUND (`git log --oneline` 확인)

## Self-Check: PASSED

## Next Steps — Task 3 (실기 확인, human-verify, blocking)

이 작업은 Task 3 checkpoint 에서 중단되었습니다. 사용자가 실제 장비에서 아래 순서를 직접 수행해야 합니다.

**빌드된 것:**

크로스-Z 측정이 z별로 서로 다른 교시 사진을 읽도록 고쳤습니다.

그동안 크로스-Z(ZIndexA/ZIndexB 를 쓰는 측정)에서는 첫 번째 z 든 두 번째 z 든 **샷에 저장된 사진 한 장만** 읽고 있었습니다. 그래서 가로 사진 기준으로 가르쳐 놓은 점(Point) 자리를 세로 사진에서 찾게 되어 에지를 하나도 못 찾고(`strips ok 0/20`) 측정값이 안 나왔습니다.

이제 크로스-Z 도 크로스-Z 를 안 쓰는 일반 측정과 **똑같은 규칙**으로 동작합니다: 첫 번째 z 는 가로 교시 사진, 두 번째 z 는 세로 교시 사진을 읽습니다. 교시 사진 경로를 안 넣어둔 기존 항목은 예전과 똑같이 라이브(샷) 사진을 씁니다.

**확인 방법 (순서대로):**

1. **새 프로그램으로 다시 시작**
   - 실행 중인 프로그램을 완전히 종료한 뒤, 방금 빌드된 것으로 다시 켜주세요.

2. **설정이 그대로인지만 확인 (바꾸지 마세요)**
   - BOTTOM `SHOT_E5` 의 ZIndex = 23
   - `E5_P1`, `E5_P2` 의 Point z index = 23, Line z index = 24
   - 두 항목의 가로/세로 교시 이미지 경로가 예전 그대로 들어 있는지

3. **첫 번째 z 트리거 (z = 23)**
   - 수동 Z트리거로 z=23 을 보냅니다.
   - 기대: 트리거가 정상 처리되고, `E5_P1`/`E5_P2` 판정이 `CROSS-Z INCOMPLETE`(아직 짝이 안 맞음) 로 보입니다. 여기서 값이 안 나오는 것은 정상입니다.

4. **두 번째 z 트리거 (z = 24)** ← 이번 수정의 핵심
   - 이어서 수동 Z트리거로 z=24 를 보냅니다.
   - 기대:
     - `E5_P1`, `E5_P2` 에 **실제 측정값이 나오고 OK 판정**
     - 값이 **30.5mm 근처** (크로스-Z 를 껐을 때 나왔던 30.543 / 30.537 과 비슷하면 성공)
     - 로그의 `[FitLine]` 이 `strips ok 20/20` 및 `50/50` (예전처럼 `0/20`, `0/50` 이면 실패)

5. **되돌리기 확인 — 크로스-Z 를 껐을 때 (예전 동작 그대로여야 함)**
   - `E5_P1`/`E5_P2` 의 Point z index / Line z index 를 **-1 / -1** 로 바꿉니다.
   - 수동 트리거를 한 번 돌립니다.
   - 기대: 예전과 똑같이 **30.543 / 30.537 OK** 가 그대로 나옵니다.
   - 확인 후 **다시 23 / 24 로 되돌려 주세요.**

6. **다른 항목 확인 — 크로스-Z 안 쓰는 BOTTOM 샷**
   - 크로스-Z 를 쓰지 않는 다른 BOTTOM 샷을 수동 트리거합니다.
   - 기대: 측정값과 판정이 예전과 완전히 동일합니다.

**Resume signal:** "approved" 라고 적어주시거나, 어느 번호에서 무엇이 달랐는지 알려주세요.
