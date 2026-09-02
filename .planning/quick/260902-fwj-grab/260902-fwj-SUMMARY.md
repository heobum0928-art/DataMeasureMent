---
phase: quick-260902-fwj
plan: 01
subsystem: ui/ethernet-vision-align
tags: [halcon, wpf, systemsetting, dispatchertimer, light-control]

requires: []
provides:
  - "SystemSetting.AlignCoaxAutoOffMs 설정(기본 3000ms, 0 이하 = 비활성)"
  - "Tray/Bottom 얼라인 화면 Grab 버튼(수동 촬영) 전용 1회성 동축 자동 소등 타이머"
affects: [tray-align, bottom-align, ethernet-vision-coax-light]

tech-stack:
  added: []
  patterns:
    - "DispatcherTimer 1회성 예약 패턴: Start 시 Cancel 후 재생성(연속 트리거 시 리셋), Tick 첫 줄에서 자기 자신 Cancel(중복 발화 방지) — 기존 _liveTimer 관용구를 그대로 재사용"
    - "finally 블록으로 성공/실패/예외 3경로에 동일 사후처리(조명 소등 예약) 통합 — 판단 기준은 '동작 성공 여부'가 아니라 '부수효과(조명 ON)가 이미 발생했는가'"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/SystemSetting.cs
    - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs

key-decisions:
  - "[Category(\"ETHERNET_VISION\")] 을 완전정규화(PropertyTools.DataAnnotations.Category)하지 않고 이웃 프로퍼티와 동일한 짧은 형태로 유지 — 이 파일은 using System.ComponentModel; 때문에 짧은 형태가 System.ComponentModel.CategoryAttribute 로 잡히지만, base Load/Save 의 group 변수가 sticky(인식되는 어트리뷰트를 만날 때만 갱신)라 오히려 안전하다. 완전정규화하면 group 이 실제로 바뀌어 리플렉션 순서상 뒤따르는 PickerCenterRow/Col(HW 캘 결과) 등 기존 저장값의 INI 섹션이 이동해 조용히 유실될 위험이 있었다."
  - "AfterLoad() 에 '0 이면 3000 으로 복원' 로직을 추가하지 않음 — 0 이하가 '자동 소등 비활성'의 정상 의미이므로 복원하면 사용자가 기능을 끌 방법이 사라져 요구사항과 정면 충돌한다. 실제 영향: 기존 PC 는 Setting.ini 에 키가 없어 처음엔 0(비활성)으로 로드되고, 설정 창에서 값을 한 번 넣어야 켜진다 — 의도된 하위호환 동작."
  - "소등 예약 판단 기준을 'Grab 성공'이 아니라 'ApplyCoaxLight() 가 이미 실행되어 조명이 켜졌는가'로 잡고, try/catch 뒤 finally 한 곳에만 StartCoaxAutoOffTimer() 를 배치 — img==null 취득실패·예외 두 경로 모두 조명은 이미 켜져 있으므로 성공 경로와 동일하게 예약해야 방치를 막는다."

requirements-completed: [QG-01, QG-02, QG-03]

coverage:
  - id: D1
    description: "Tray/Bottom 얼라인 화면에서 Grab 누르면 설정 시간(ms) 뒤 동축 조명이 자동으로 꺼진다"
    requirement: "QG-01"
    verification:
      - "grep으로 필드 1개 + StartCoaxAutoOffTimer/CancelCoaxAutoOffTimer/CoaxAutoOffTimer_Tick 3메서드 존재 확인(Tray/Bottom 동일)"
      - "Debug|x64 빌드 error CS 0"
    human_judgment: true
    rationale: "타이머 발화 후 실제 조명 하드웨어가 꺼지는지는 실기 확인 필요 — Task 4 실기 UAT 항목 1"
  - id: D2
    description: "연속 Grab 시 마지막 Grab 기준으로 소등 시각 리셋, Live 중에는 소등 안 됨, Stop 시 잔여 예약 정리, 0 이면 비활성"
    requirement: "QG-02"
    verification:
      - "grep -A2 finally 안에 StartCoaxAutoOffTimer 존재(1건, Tray/Bottom 각각)"
      - "grep -B2 'ApplyCoaxLight();$' 안에 CancelCoaxAutoOffTimer 존재(1건, Live 성공분기)"
      - "StartCoaxAutoOffTimer 본문의 nDelayMs<=0 게이트 코드 리뷰"
    human_judgment: true
    rationale: "코드 배선은 정적 검증을 마쳤으나 타이머 리셋 체감/Live 보호/0 설정 동작은 실기 확인 필요 — Task 4 실기 UAT 항목 2~4"
  - id: D3
    description: "자동 검사 사이클(Action_FAIMeasurement 등)과 티칭 경로의 조명 동작은 이번 변경 전후 동일"
    requirement: "QG-03"
    verification:
      - "git diff 로 ApplyCoaxLight() 본문 무변경 확인"
      - "git diff 로 TeachButton_Click/RunButton_Click 등 기존 호출부 무변경 확인"
      - "Action_FAIMeasurement.cs 등 자동 검사 경로 파일은 이번 diff에 포함되지 않음(files_modified 3개 한정)"
    human_judgment: true
    rationale: "정적으로는 무변경을 확인했으나 실제 검사 사이클 1회 실행으로 최종 확인을 권장 — Task 4 안내문 마지막 항목"

duration: ~15min
completed: 2026-09-02
status: complete
---

# Quick Task 260902-fwj: 얼라인 수동 Grab 후 동축 자동 소등(설정 딜레이) Summary

**Tray/Bottom 얼라인 화면의 수동 Grab 버튼으로 촬영한 뒤 `SystemSetting.AlignCoaxAutoOffMs`(기본 3000ms)가 지나면 동축 조명이 자동으로 꺼지도록, 1회성 `DispatcherTimer` + Start/Cancel/Tick 3메서드를 두 화면에 동일하게 배선.**

## Performance

- **Duration:** 약 15분
- **Completed:** 2026-09-02
- **Tasks:** 3 (Task 1/2 코드 작성 + Task 3 스타일게이트/빌드/커밋). Task 4(실기 육안 확인)는 하드웨어가 필요한 체크포인트로 보류.
- **Files modified:** 3

## Accomplishments

- `SystemSetting.cs` — `ALIGN_COAX_AUTO_OFF_MS_DEFAULT`(=3000) 상수와 `AlignCoaxAutoOffMs` 설정 프로퍼티를 `ETHERNET_VISION` 섹션 맨 끝(`CalibSearchCol2` 뒤)에 추가. `[Category("ETHERNET_VISION")]` 은 이웃과 동일한 짧은 형태 유지(F1), `AfterLoad()` 는 손대지 않음(F2).
- `TrayVisionView.xaml.cs` / `BottomVisionView.xaml.cs` 동일하게: `_coaxAutoOffTimer`(DispatcherTimer) 필드 + `StartCoaxAutoOffTimer()`/`CancelCoaxAutoOffTimer()`/`CoaxAutoOffTimer_Tick()` 3메서드를 `LiveTimer_Tick` 뒤에 추가.
- `GrabButton_Click` 의 `#else`(실HW) 분기 `try`에 `finally` 를 추가해 성공/취득실패/예외 3경로 모두에서 `StartCoaxAutoOffTimer()` 호출(F3).
- `LiveButton_Click` 성공 분기에서 `ApplyCoaxLight()` 직전에 `CancelCoaxAutoOffTimer()` 호출 — 직전 Grab 예약이 Live 도중 조명을 꺼버리는 것을 방지.
- `StopButton_Click` 의 `StopLiveTimer();` 다음 줄에 `CancelCoaxAutoOffTimer();` 추가 — 잔여 예약 정리(소등 자체는 기존 무조건 소등 로직 그대로).
- `#if SIMUL_MODE` 분기, `ApplyCoaxLight()` 본문, 티칭(`TeachButton_Click`)/검사(`RunButton_Click`) 경로, `_liveTimer` 로직은 전부 무변경.

## Task Commits

1. **Task 1+2: SystemSetting 설정 추가 + Tray/Bottom 타이머 배선** - `338014c3` (feat)
   - Task 3(스타일게이트+빌드) 검증을 통과한 뒤, 계획서 Task 3(c) 지시대로 3개 파일을 한 커밋으로 묶어 커밋함(Task 1/2는 커밋을 별도로 요구하지 않음).

**계획 문서 커밋:** 아래 self-check 이후 `.planning/quick/260902-fwj-grab/`(PLAN.md + 이 SUMMARY.md) + `.planning/STATE.md` 를 별도 `docs(quick-260902-fwj): ...` 커밋으로 기록(선례 260901-mc1/k7a와 동일 패턴).

## Files Created/Modified

- `WPF_Example/Custom/SystemSetting.cs` - `ALIGN_COAX_AUTO_OFF_MS_DEFAULT` 상수 + `AlignCoaxAutoOffMs` 설정 프로퍼티
- `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` - `_coaxAutoOffTimer` + 3메서드 + Grab/Live/Stop 배선
- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` - `_coaxAutoOffTimer` + 3메서드 + Grab/Live/Stop 배선(Tray와 동일 구조, 리팩토링으로 묶지 않음 — CLAUDE.md "이번에 손대는 지점에만" 원칙)

## Decisions Made

frontmatter의 `key-decisions` 참고. 요약하면: (1) `[Category]` 짧은 형태 유지로 기존 HW 캘값 유실 위험 회피, (2) `AfterLoad()` 미변경으로 "0=비활성" 요구사항 보존(기존 PC는 설정 창에서 한 번 값을 넣어야 켜짐), (3) 소등 예약을 `finally`에 두어 성공/실패/예외 3경로 전부 커버.

## Deviations from Plan

None — 계획서에 기록된 대로 실행했다. F1/F2/F3 결정을 뒤집지 않았고, 두 화면 공용 헬퍼로 리팩토링하지도 않았다.

구현 중 한 가지 자체 교정: 계획의 "`finally` 위에 근거 주석"이라는 표현을 처음에 "`finally` 블록 **안** 첫 줄에 주석"으로 잘못 배치했다가, Task 3(a) 검증 게이트(`grep -A2 'finally'` 로 `StartCoaxAutoOffTimer` 가 2줄 이내에 있는지 확인)를 실행하며 발견하고 커밋 전에 주석을 `finally` 키워드 **위**(catch 블록과 finally 사이)로 옮겼다. 최종 커밋된 코드는 계획과 정확히 일치하며, 이 교정은 커밋에 반영되지 않은 중간 상태라 별도 Rule 위반이 아니다.

## Issues Encountered

None.

## Known Stubs

None.

## Threat Flags

None — `threat_model`(T-FWJ-01/02/03)에 이미 식별된 표면 범위 안에서만 구현했다. 신규 네트워크/인증 경로 없음.

## Automated Verification (Tasks 1-3)

- `grep -c AlignCoaxAutoOffMs SystemSetting.cs` = 2, `PropertyTools.DataAnnotations.Category` 매치 = 0(F1 준수), `ALIGN_COAX_AUTO_OFF_MS_DEFAULT` 사용처 2곳(상수 선언 + 프로퍼티 초기값).
- Tray/Bottom 각각: `field=1 start=2 cancel=5`(요구 조건 field=1/start=2/cancel≥4 모두 충족).
- `grep -A2 finally | grep -c StartCoaxAutoOffTimer` = 1 (Tray/Bottom 각각).
- `grep -B2 'ApplyCoaxLight();$' | grep -c CancelCoaxAutoOffTimer` = 1 (Tray/Bottom 각각, Live 성공분기).
- 추가된 diff 라인 전체(3개 파일 합산 102줄) 대상 하드룰 grep: 삼항 0 / `??` 0 / `?.` 0 / switch식 0 / `hbk` 0.
- Debug|x64 MSBuild: `error CS` 0건(경고만 존재, 전부 기존 Phase 33 마이그레이션 obsolete 경고로 이번 변경과 무관). 사용자의 미커밋 csproj 실험(SIMUL_MODE 제거) 덕분에 이번 빌드가 실HW `#else` 분기를 실제로 컴파일해 핵심 변경 경로를 컴파일 레벨에서 검증했다.
- `git diff --cached --name-only` — 커밋 전 정확히 3개 파일만 스테이징 확인(`DatumMeasurement.csproj` 없음). 커밋 후 `git status --short`로 csproj 가 여전히 unstaged 미커밋 상태로 남아있음을 재확인, `git diff --diff-filter=D`로 의도치 않은 파일 삭제 없음 확인.

## Manual Verification Required (Task 4 — 실기 육안 확인, 하드웨어 필요)

이 항목은 자동화 실행자가 수행할 수 없다(실제 카메라/조명 하드웨어 필요). 사용자가 아래를 직접 확인해야 한다:

**먼저 설정부터 (중요):** 기존 PC 는 설정 파일에 이 항목이 없어 처음엔 **0(자동 소등 꺼짐)** 으로 읽힌다. 프로그램을 켜고 설정 창에서 `AlignCoaxAutoOffMs` 를 찾아 **3000**(=3초)을 넣고 저장한다. 항목이 안 보이면 여기서 멈추고 알려줄 것.

Tray 얼라인 화면에서 아래 4가지를 확인 → 끝나면 Bottom 얼라인 화면에서 동일하게 반복:

1. **시간 뒤 꺼지는가** — 동축 체크박스 켜고 밝기 올린 뒤 [Grab] 클릭 → 약 3초 뒤 동축 조명이 저절로 꺼져야 함.
2. **연속으로 누르면 밀리는가** — [Grab] 클릭 후 1~2초 뒤 다시 [Grab] 클릭 → 마지막 클릭 기준 3초 뒤에 꺼져야 함(중간에 깜빡 꺼지면 실패).
3. **Live 중에는 안 꺼지는가** — [Grab] 클릭 후 1초쯤 뒤 [Live] 클릭 → Live 화면이 떠 있는 동안 조명이 계속 켜져 있어야 함(3초쯤에 툭 꺼지면 실패). [Stop] 누르면 조명은 꺼져야 함(기존 동작).
4. **0 으로 두면 안 꺼지는가** — 설정값을 0으로 바꿔 저장한 뒤 [Grab] 클릭 → 조명이 계속 켜져 있어야 함(예전 그대로).

**추가 확인:** 평소 검사 사이클 한 번을 돌려서 검사 중 조명 동작이 예전과 동일한지 확인(이번 작업은 화면의 Grab 버튼만 건드림).

이 체크리스트가 통과할 때까지 플랜은 "완전 완료"로 간주하지 않으며, 사용자 확인 결과를 기다린다. 승인 시 STATE.md의 이번 항목 상태를 갱신할 것.

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/SystemSetting.cs` (AlignCoaxAutoOffMs 프로퍼티 존재)
- FOUND: `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` (_coaxAutoOffTimer + 3메서드 존재)
- FOUND: `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` (_coaxAutoOffTimer + 3메서드 존재)
- FOUND commit `338014c3` (Task 1+2 코드, Task 3 검증 통과 후 커밋)
- Build: `error CS` 0건 확인됨(로그: scratchpad/build.log)
- `git diff --cached --name-only` 및 커밋 후 `git status --short` 로 `DatumMeasurement.csproj` 미포함/미커밋 재확인
