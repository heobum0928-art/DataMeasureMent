---
phase: 59-vision-algorithm-b-2026-06-23
plan: "03"
subsystem: EthernetVision / AlignShapeMatchService wiring
tags: [ethernet-vision, shape-matching, handler-wiring, build-verification, anti-goal]
dependency_graph:
  requires: [59-01, 59-02]
  provides: [EthernetVisionHandler.Matcher public property, msbuild-pass Phase-59]
  affects: [Phase 61 UI (teach/run calls), Phase 62 TCP (run calls)]
tech_stack:
  added: []
  patterns:
    - "Stateless service owned by singleton handler (EthernetVisionHandler.Matcher)"
    - "Null-guard creation in catch: if (Matcher == null) { ... }"
key_files:
  modified:
    - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
decisions:
  - "Matcher created as first statement in try{} to cover None/connected/connect-failed paths in a single creation point; catch null-guard covers exception-before-assignment race (D-02)"
  - "No using directive needed — AlignShapeMatchService is in namespace ReringProject, same as EthernetVisionHandler"
metrics:
  duration_minutes: 10
  completed_date: "2026-06-24"
  tasks_completed: 3
  tasks_total: 3
  files_modified: 1
  files_created: 0
requirements: [AV-03, AV-04]
---

# Phase 59 Plan 03: Handler Wiring + Build + Anti-Goal Summary

**One-liner:** EthernetVisionHandler에 stateless AlignShapeMatchService Matcher 프로퍼티 추가 — Initialize() 모든 경로(None/connected/fail/exception)에서 non-null 보장, msbuild Debug/x64 0 오류 통과, 금지 파일 무수정 증명.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | Matcher 프로퍼티 + Initialize() 생성 (D-02) | f59fa52 | EthernetVisionHandler.cs |
| 2 | msbuild Debug/x64 빌드 검증 | (build-only, 소스 변경 없음) | — |
| 3 | Anti-goal git 검증 | (verify-only, 소스 변경 없음) | — |

## Task 1: Matcher 프로퍼티 추가

`WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` 에 순수 추가 편집 3곳:

1. **프로퍼티 선언** (Camera 프로퍼티 바로 아래):
   ```csharp
   //260624 hbk Phase 59 — D-02: Shape matching align 서비스 (handler 소유, stateless). Mode 무관 항상 생성.
   public AlignShapeMatchService Matcher { get; private set; }
   ```

2. **try 블록 최상단** (mode 게이트 이전 — None/connected/connect-failed 모든 경로 커버):
   ```csharp
   //260624 hbk Phase 59 — D-02: Matcher 는 stateless → 모드/연결 결과 무관하게 항상 생성
   Matcher = new AlignShapeMatchService();
   ```

3. **catch 블록 내 null 가드** (예외가 첫 번째 생성 이전에 발생하는 경우 커버):
   ```csharp
   //260624 hbk Phase 59 — 예외 경로에서도 Matcher null 방지
   if (Matcher == null) {
       Matcher = new AlignShapeMatchService();
   }
   ```

기존 Phase 58 로직(mode 게이트/Camera 생성/Connect 호출/IsInitialized 할당/로그) 무수정 — 순수 추가 편집.

## Task 2: msbuild Debug/x64 빌드 결과

| 항목 | 결과 |
|------|------|
| Exit code | 0 (성공) |
| CS 오류 | 0 |
| CS 신규 경고 | 0 |
| MSBuild 툴 경고 (MinimumRecommendedRules.ruleset) | Phase 38 baseline 과 동일 — 신규 아님 |

Phase 59 신규 파일 3개 (AlignResult.cs, AlignRefPose.cs, AlignShapeMatchService.cs) 모두 컴파일 완료.

## Task 3: Anti-Goal 검증 결과

**기준 커밋:** dcf5f6c (Phase 59 문서 커밋 — 코드 변경 직전)

**Phase 59 전체 변경 파일 (dcf5f6c → HEAD):**

| 파일 | 분류 | 허용 여부 |
|------|------|----------|
| WPF_Example/Custom/EthernetVision/AlignResult.cs | 신규 (Plan 01) | 허용 |
| WPF_Example/Custom/EthernetVision/AlignRefPose.cs | 신규 (Plan 01) | 허용 |
| WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs | 신규 (Plan 02) | 허용 |
| WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs | 수정 (Plan 03, 순수 추가) | 허용 |
| WPF_Example/DatumMeasurement.csproj | 수정 (Plans 01/02, Compile 항목 추가) | 허용 |
| .planning/** | 플래닝 문서 | 해당 없음 |

**금지 파일 검증:**

```
git diff --name-only dcf5f6c HEAD | grep -E "PatternMatchService|RecipeFileHelper|HikCamera|DeviceHandler|VirtualCamera|SystemHandler|/Sequence/|Sequence_|Action_"
→ (출력 없음)
ANTIGOAL_PASS
```

**EthernetVisionHandler.cs diff 방향 검증:**

```
git diff dcf5f6c HEAD -- EthernetVisionHandler.cs | grep -E "^-" | grep -v "^---"
→ (출력 없음)
HANDLER_ADDONLY_PASS
```

삭제된 라인 0 — 순수 추가 편집 확인.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None — 이 플랜은 wiring/build/verification 전용. 서비스 로직은 Plan 02, UI 소비는 Phase 61, TCP 소비는 Phase 62.

## Threat Flags

없음 — T-59-05/T-59-06은 플랜 threat_model 에서 이미 등록 및 mitigate/accept 처리됨. 새로운 신뢰 경계 없음.

## Self-Check

- [x] `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` 존재 확인
- [x] 커밋 f59fa52 존재 확인
- [x] `public AlignShapeMatchService Matcher` 선언 1개 확인
- [x] `Matcher = new AlignShapeMatchService()` 2개 확인 (try 최상단 + catch null 가드)
- [x] msbuild EXIT_CODE=0, CS 오류 0, CS 신규 경고 0
- [x] ANTIGOAL_PASS (금지 파일 0건 수정)
- [x] HANDLER_ADDONLY_PASS (삭제 라인 0)

## Self-Check: PASSED
