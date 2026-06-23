---
phase: 49-protocol-v1-judgment-engine
plan: 01
subsystem: control-protocol-v1
tags: [protocol-v1, judgment-engine, cycle-state, enum, foundation]
requires: []
provides:
  - "ECycleResult enum { Buffer, Pass, Fail }"
  - "ShotConfig.ZIndex (z_index↔Shot 매핑)"
  - "InspectionSequence 사이클 상태 멤버 4개 (m_bCycleHasNG/m_bCycleDatumFailed/m_nCurrentZIndex/m_nLastZIndex)"
  - "ComputeLastZIndex 헬퍼 (레시피 z_index 최댓값)"
  - "ResetCycleState 헬퍼 (Index 0 수신 시 사이클 리셋)"
affects:
  - "49-02 (판정 흐름이 이 enum·멤버·헬퍼를 소비)"
tech-stack:
  added: []
  patterns:
    - "ParamBase reflection 자동 직렬화 (ZIndex public int, INI 키=프로퍼티명)"
    - "헝가리언 멤버 접두사 m_ + if/else only (D-10 control-sequence-coding-guideline)"
    - "정의-소비 분리 (49-01 정의 / 49-02 소비) — 파일 경합 회피 wave 격리"
key-files:
  created: []
  modified:
    - "WPF_Example/TcpServer/VisionResponsePacket.cs (ECycleResult enum)"
    - "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs (ZIndex 프로퍼티)"
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (사이클 멤버 4 + 헬퍼 2)"
decisions:
  - "D-07: enum 신설 = ECycleResult 1개만 (CycleState 라이프사이클 enum 미도입)"
  - "D-01: ShotConfig.ZIndex 로 z_index↔Shot 매핑, 누락 키 0 폴백 = 의도된 안전값"
  - "D-02: 사이클 상태 = InspectionSequence 멤버 (신규 클래스 미도입, _failedDatums 와 동일 lifecycle)"
  - "D-03: 마지막 Index = 레시피 Shot z_index 최댓값 (ComputeLastZIndex, 이 시퀀스 소유 Shot 한정)"
  - "D-08: ResetCycleState = Index 0 수신 시 클린 슬레이트, 호출 후 m_nLastZIndex 재산출 의무"
  - "[deviation] CS0414 #pragma 일시 억제 — 정의만 단계라 read 미연결, 49-02 가 제거 (Rule 3)"
metrics:
  duration_min: 3
  completed: "2026-06-23"
  tasks: 3
  files: 3
---

# Phase 49 Plan 01: P/F/B 판정 엔진 상태 토대 Summary

제어 프로토콜 v1.0 멀티샷 사이클 판정 엔진의 **상태 토대**를 정의: `ECycleResult { Buffer, Pass, Fail }` enum, `ShotConfig.ZIndex` 매핑 필드, `InspectionSequence` 사이클 누적 상태 멤버 4개 + z_index 최댓값 산출/리셋 헬퍼 2개. 판정 흐름(소비)은 49-02 가 채운다.

## What Was Built

### Task 1 — ECycleResult enum (commit 653bc92)
`VisionResponsePacket.cs` 의 `EVisionResultType` 바로 아래에 형제 enum `ECycleResult { Buffer=0, Pass=1, Fail=2 }` 추가. PROTO-05 enum 요구 충족 (D-07). 정의만 — IsBuffer/Result 매핑 소비는 49-02.

### Task 2 — ShotConfig.ZIndex (commit 9d547da)
`ShotConfig.cs` 의 `OwnerSequenceName` 아래에 `public int ZIndex { get; set; } = 0` 추가. ParamBase reflection 자동 직렬화 (INI 키 "ZIndex"). 기존 레시피 키 부재 → 0 로드 = Datum/Idx0 폴백 (하위 호환). "ZIndex 미설정 레시피 + 측정 Index 수신 = 매칭 0건 → 49-02 빈 B + PrintErrLog" 엣지케이스 주석으로 못박음 (D-01).

### Task 3 — InspectionSequence 사이클 상태 멤버 + 헬퍼 (commit e41fdc3)
`_alignFailedDatums` 아래에 사이클 누적 멤버 4개(`m_bCycleHasNG`/`m_bCycleDatumFailed`/`m_nCurrentZIndex`/`m_nLastZIndex`) 추가 — `_failedDatums` 와 동일 lifecycle (D-02). `ComputeOverallResult` 아래에 헬퍼 2개:
- `ComputeLastZIndex(recipeManager)` — 이 시퀀스 소유 Shot(`OwnerSequenceName == Name`) 중 z_index 최댓값 = 마지막 Index (D-03). 30줄 한도·헝가리언·if/else only 준수, 삼항/null병합 0건.
- `ResetCycleState()` — Index 0 수신 시 클린 슬레이트 (D-08). 본문에 "호출 후 m_nLastZIndex 재산출 의무" 주석 못박음 (0>=0 오판 방지).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking acceptance criterion] CS0414 "field assigned but never used" 4건 → #pragma 일시 억제**
- **Found during:** Task 3 빌드
- **Issue:** 본 plan 은 멤버를 **정의만** 하고 read 는 49-02 가 연결한다. ResetCycleState 가 write 만 하고 호출처가 없어 4개 멤버에 CS0414(할당되었으나 미사용) 발생 — Task 3 acceptance "0 new warnings" 와 충돌.
- **Fix:** 4개 멤버 선언을 `#pragma warning disable CS0414` / `restore` 로 한정 감싸고, "49-02 read 연결 시 제거" 주석 부착. 동작 무변경(behavior-neutral). 빌드 후 InspectionSequence CS0414 4건 소거 확인.
- **Files modified:** WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- **Commit:** e41fdc3

(`ComputeLastZIndex`/`ResetCycleState` 는 미사용 private 메서드지만 C# 기본 설정에서 경고를 발생시키지 않아 별도 조치 불필요.)

## Build Result

- `msbuild DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Rebuild` → **0 errors**, `DatumMeasurement.exe` 생성 (WPF_Example/bin/x64/Debug/).
- 경고: CS0618 ×10 (Top/BottomSequence 폐기 — Phase 33 baseline), CS0162 ×2 (VirtualCamera unreachable — baseline), MSB3884 ×2 (ruleset 파일 — baseline). **신규 경고 0** (CS0414 4건은 #pragma 로 소거).

## Acceptance Criteria

- ECycleResult enum 3 멤버 (Buffer/Pass/Fail) — PASS (grep 1)
- ShotConfig.ZIndex default 0 + "ZIndex 미설정" 엣지케이스 주석 — PASS
- m_bCycleHasNG/m_bCycleDatumFailed 각 ≥2 (선언+리셋 대입) — PASS (각 2)
- ComputeLastZIndex / ResetCycleState 헬퍼 존재 + "재산출 필요" 주석 — PASS
- 삼항/null병합 신규 0건 — PASS (if/else only)
- msbuild Debug/x64 PASS, 0 errors, 0 new warnings — PASS

## Known Stubs

본 plan 은 의도적 "정의만" 단계다. 다음 항목은 49-02 가 연결한다 (스텁 아님 — 계획된 분리):
- `ECycleResult` → `TestResultPacket.IsBuffer`/`Result` 매핑 미연결 (49-02)
- 사이클 멤버 4개 read 미연결 → AddResponse 판정 흐름 (49-02)
- `ComputeLastZIndex`/`ResetCycleState` 호출처 미연결 (49-02)
- `#pragma CS0414` 는 49-02 가 read 추가 시 제거 예정

## Self-Check: PASSED

- WPF_Example/TcpServer/VisionResponsePacket.cs — FOUND
- WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs — FOUND
- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — FOUND
- commit 653bc92 — FOUND
- commit 9d547da — FOUND
- commit e41fdc3 — FOUND
