---
phase: 49-protocol-v1-judgment-engine
plan: 02
subsystem: control-protocol-v1
tags: [protocol-v1, judgment-engine, z-index, cycle, b-p-f, datum, core]
requires:
  - "49-01: ECycleResult enum / ShotConfig.ZIndex / m_bCycle* 멤버 4 / ComputeLastZIndex / ResetCycleState"
  - "48-03: TestResultPacket.IsBuffer hook / BuildResultMessageV1 / MapCycleJudgement (IsBuffer 최우선 직렬화)"
provides:
  - "InspectionSequence.AddResponseV1Cycle — z_index 멀티샷 사이클 B/P/F 판정 엔진"
  - "Index 0 Datum 빈응답(RESULT:site;B;0;) + Datum 실패 즉시 F(UseProtocolV1 한정)"
  - "중간 Index = B(IsBuffer=true, NG 누적) / 마지막 Index = 종합 P/F 1회"
  - "ZIndex 매칭 0건 시 PrintErrLog 경고 (조용한 빈 B 금지)"
affects:
  - "50 (실 핸들러 통신 회귀 — 이 엔진의 wire-level 동작 검증)"
  - "49-HUMAN-UAT.md (7 UAT 시나리오 — 실 핸들러/SIMUL 검증)"
tech-stack:
  added: []
  patterns:
    - "헝가리언(m_/b/n/sz) + if/else only + 함수 30줄 분리 (D-10 control-sequence-coding-guideline)"
    - "매직넘버 상수화 (DATUM_Z_INDEX const)"
    - "v1.0/v2.6 공존 분기 — UseProtocolV1 게이트, v2.6 경로 무변경 보존"
    - "IsBuffer/Result 만 채움 — 직렬화(48-03) 재구현 금지"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (CS0414 pragma 제거 + 판정 엔진 11 메서드)"
decisions:
  - "D-01: Index-scoped only — shot.OwnerSequenceName==Name && shot.ZIndex==nZIndex 인 Shot 만 집계, 전체 재검사 금지"
  - "D-02: m_bCycleHasNG 멤버 누적 — ClassifyFai 가 NG/검출실패 시 set, 리셋 전까지 유지"
  - "D-03: 마지막 Index = ComputeLastZIndex 최댓값, m_nCurrentZIndex >= m_nLastZIndex"
  - "D-04: Datum 실패 즉시 F = UseProtocolV1 분기에서만 (BuildDatumShotResponse bUseV1 가드)"
  - "D-06: Index 0 정상 = 빈 응답 RESULT:site;B;0; (FAIResults 비움 → FAICount=0)"
  - "D-08: Index 0 수신 시 ResetCycleState() 후 ComputeLastZIndex 1회 재산출 (이중 호출 제거)"
  - "BLOCKER 1: ZIndex 매칭 0건 → PrintErrLog 경고, 빈 B 유지(폴백 재검사 미채택, D-01 정합)"
  - "BLOCKER 2: AddResponseV1Cycle(10줄)/BuildScopedResponse(11줄) <=30줄, ComputeLastZIndex 경로별 1회"
  - "[commit 단위] Task 1+2 단일 commit — Task 1 진입부가 Task 2 AddResponseV1Cycle 참조 → 분리 시 미컴파일 (deviation)"
metrics:
  duration_min: 3
  completed: "2026-06-23"
  tasks: 2
  files: 1
---

# Phase 49 Plan 02: P/F/B 판정 엔진 (핵심) Summary

제어 프로토콜 v1.0 의 **핵심** — `InspectionSequence.AddResponse` 를 z_index 멀티샷 사이클 판정 엔진으로 확장. Index 0(Datum 샷) 빈응답/즉시F, 중간 Index B(NG 누적), 마지막 Index 종합 P/F 1회를 구현. 직렬화(48-03)는 재구현 없이 IsBuffer/Result 만 채움. v2.6 경로는 UseProtocolV1=false 게이트로 무변경 폴백.

## What Was Built (commit 8e97aa3)

### 진입 분기 (Task 1)
`AddResponse` 라인 99 `if (RequestPacket == null) return;` 직후 `bool bUseV1Cycle = ...UseProtocolV1;` → true 면 `AddResponseV1Cycle()` 위임 후 return, false 면 기존 v2.6 전체-Shot 블록(라인 ~107~183) **무변경 폴백**.

### 신규 헬퍼/메서드 11종
- **ParseCurrentZIndex** — RequestPacket.TestID(z_index 문자열, "-1"=미수신) → int. TryParse + `>= 0` 가드 → 비정수/음수/미수신 모두 0(Datum 폴백) 정규화 (T-49-03 mitigation).
- **BuildDatumShotResponse** — Index 0 응답. 정상 → IsBuffer=true + OK(빈 B). UseProtocolV1 && m_bCycleDatumFailed → IsBuffer=false + NG(즉시 F).
- **DetectDatumFailure** — DatumConfigs 순회, IsDatumFailed 1건이라도 → true.
- **AddResponseV1Cycle** (본문 10줄) — 진입점. ParseCurrentZIndex → Index 0 면 HandleDatumIndexResponse 위임, 아니면 측정 경로(ComputeLastZIndex 1회 + bIsLastIndex + BuildScopedResponse + PersistAndEnqueueV1).
- **HandleDatumIndexResponse** — D-08 리셋 + ComputeLastZIndex 재산출(1회) + DetectDatumFailure + BuildDatumShotResponse + 영속화.
- **BuildScopedResponse** (본문 11줄) — 패킷 생성 → AggregateIndexFais → WarnIfEmptyScope → ApplyCycleJudgement → pMyContext.ResultInfo.
- **AggregateIndexFais** — OwnerSequenceName==Name && ZIndex==nZIndex 인 Shot 의 FAI 만 집계(D-01), 매칭 Shot 수 반환.
- **AddFaiResult / ClassifyFai** — FAI 3-state(검출실패 'N' / NG 'F' / OK 'P'). 'N'/'F' 시 m_bCycleHasNG=true 누적(D-02).
- **WarnIfEmptyScope** — BLOCKER 1: 빈 결과 && 매칭 0건 → PrintErrLog 경고. 폴백 재검사 미채택, 빈 B 유지(D-01 정합).
- **ApplyCycleJudgement** — 중간 Index → IsBuffer=true + OK(B). 마지막 Index → IsBuffer=false + (m_bCycleHasNG||m_bCycleDatumFailed ? F : P).
- **PersistAndEnqueueV1** — 기존 v2.6 try/catch CycleResultSerializer.BuildDto/SaveAsync/ResponseQueue.Enqueue 동일 이식.

### CS0414 pragma 제거
49-01 이 4 멤버에 임시로 감싼 `#pragma warning disable/restore CS0414` 2줄 제거. 멤버가 엔진에서 genuinely read 됨 → 빌드 후 CS0414 재발 없음 확인.

### 매직넘버 상수화
`private const int DATUM_Z_INDEX = 0;` 추가 (D-10).

## Deviations from Plan

### 1. [commit 단위] Task 1 + Task 2 단일 commit
- **Found during:** Task 1 빌드 시점
- **Issue:** Task 1 진입부가 `AddResponseV1Cycle()`(Task 2 정의)를 호출 → Task 1 단독으로는 미컴파일. task_commit_protocol "각 task 빌드 후 commit" 충돌.
- **Fix:** Task 1+2 를 단일 atomic commit(8e97aa3)으로 통합 — 동일 단일 파일 + 빌드 가능 최소 단위. 두 task 작업 모두 commit 메시지에 명시.
- **Files modified:** WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- **Commit:** 8e97aa3

(Rules 1-3 자동 수정 없음 — 코드 결함/누락/블로킹 이슈 0건.)

## Build Result

- `msbuild DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build` → **0 errors**, `DatumMeasurement.exe` 생성 (WPF_Example/bin/x64/Debug/).
- 경고: CS0618 ×5(Top/Bottom 폐기 — Phase 33 baseline), CS0162 ×1(VirtualCamera unreachable — baseline), MSB3884 ×1(ruleset — baseline) per csproj. **신규 경고 0**. **CS0414 ×4 GONE** (pragma 제거 + read 연결 후 재발 없음 확인).

## Acceptance Criteria

- Task 1: ParseCurrentZIndex / BuildDatumShotResponse / DetectDatumFailure + bUseV1Cycle 진입 분기 — PASS (grep)
- Task 2: AddResponseV1Cycle / HandleDatumIndexResponse / BuildScopedResponse / ClassifyFai / PersistAndEnqueueV1 — PASS (grep)
- BLOCKER 1: WarnIfEmptyScope PrintErrLog(nMatchedShots==0) — PASS
- BLOCKER 2-a: ComputeLastZIndex 경로별 1회(388 측정 / 400 Index0, 295=주석) — PASS
- BLOCKER 2-c: AddResponseV1Cycle 본문 10줄(≤30) — PASS
- D-10 헬퍼: BuildScopedResponse 본문 11줄(≤30), ClassifyFai 분리 — PASS
- IsBuffer true(중간) / false(마지막·즉시F) 양쪽 존재 — PASS
- ResetCycleState() Index 0 분기 호출(HandleDatumIndexResponse) — PASS
- v2.6 회귀 0: anyDatumSkip ≥ 4 (실제 7) 보존 — PASS
- 삼항/null병합 신규 0건 — PASS
- CS0414 GONE, msbuild Debug/x64 0 errors / 0 new warnings — PASS

## Human UAT Candidates (→ 49-HUMAN-UAT.md)

코드 검증으로 대체 불가 — 실 핸들러/SIMUL $TEST 시나리오 필요:
- **UAT-1:** $TEST z_index=0 정상 → `RESULT:site;B;0;` byte 확인
- **UAT-2:** $TEST z_index=0 Datum 실패 → 즉시 `RESULT:site;F;...` (UseProtocolV1=true)
- **UAT-3:** 중간 Index NG 발생 → 응답 `B`, 사이클 계속 진행
- **UAT-4:** 마지막 Index → 누적 NG 있으면 `F`, 없으면 `P` 1회
- **UAT-5:** 다음 자재 Index 0 재수신 → 이전 NG 미잔류(리셋 확인)
- **UAT-6:** UseProtocolV1=false 회귀 — v2.6 응답 포맷 무변경
- **UAT-7:** ZIndex 미설정(전부 0) 레시피서 측정 Index 1+ 수신 → 빈 B + PrintErrLog 경고 로그 확인

## Self-Check: PASSED

- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — FOUND
- commit 8e97aa3 — FOUND
- AddResponseV1Cycle 본문 10줄 / BuildScopedResponse 11줄 (≤30) — VERIFIED
- ComputeLastZIndex 경로별 1회 (실호출 388/400) — VERIFIED
- CS0414 빌드 출력 0건 — VERIFIED
