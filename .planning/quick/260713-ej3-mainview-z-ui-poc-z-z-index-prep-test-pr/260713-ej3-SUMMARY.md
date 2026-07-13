---
phase: quick-260713-ej3
plan: 01
subsystem: ui
tags: [wpf, xaml, tcp-bridge, poc, manual-trigger]

# Dependency graph
requires:
  - phase: quick-260625 (v3.0 $PREP/$TEST split)
    provides: ProcessPrep(PrepPacket)/ProcessTest(TestPacket) 프로덕션 라우팅 경로
provides:
  - "DebugManualZTrigger(string seqName, int zIndex) — ProcessPrep→ProcessTest 순차 호출 임시 wrapper"
  - "MainView 하단 Row 4 임시 수동 Z축 트리거 UI 패널 (시퀀스 콤보 + z_index 입력 + 실행 버튼)"
affects: [IAxisController 실제 구현 phase — 이 wrapper+패널 통째로 삭제 대상]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TCP 패킷(PrepPacket/TestPacket)을 코드에서 직접 생성해 기존 private ProcessPrep/ProcessTest 경로로 흘려보내는 로컬 브리지 패턴"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "PREP 실패 시 TEST 를 진행하지 않고 즉시 false 반환 (조명/z_index 미준비 상태로 검사가 도는 오동작 방지)"
  - "TestPacket.TestID 는 세팅하지 않음 — ProcessTest 가 _lastPrepZIndex 로 덮어쓰므로 직접 세팅 시 오히려 혼란"
  - "UI 패널을 기존 이미지 뷰어(Row 1, HWindowControlWPF) 와 분리된 Row 4(WPF 전용 영역)에 배치 — airspace 문제 회피"

patterns-established: []

requirements-completed: [MANUAL-Z-TRIGGER]

# Metrics
duration: 20min
completed: 2026-07-13
---

# Quick Task 260713-ej3: 수동 Z축 트리거 UI (POC용) Summary

**Z축 수동 지그 ↔ 소프트웨어 사이 임시 브리지 — MainView 버튼 클릭으로 실제 $PREP+$TEST 내부 경로(ProcessPrep→ProcessTest)를 로컬에서 트리거**

## Performance

- **Duration:** ~20 min
- **Tasks:** 3 (2 코드 태스크 + 1 빌드검증 태스크)
- **Files modified:** 3

## Accomplishments
- `SystemHandler.DebugManualZTrigger(string seqName, int zIndex)` internal wrapper 추가 — PrepPacket 생성→ProcessPrep 호출(조명 적용+z_index 저장)→성공 시에만 TestPacket 생성→ProcessTest 호출(StartAll/Start 검사 트리거)
- MainView 하단(Row 4, Auto)에 시각적으로 구분되는 임시 경고색(다크앰버 배경+오렌지 테두리) 패널 추가: 시퀀스 콤보박스 + z_index TextBox + "수동 트리거 실행" 버튼
- MainView_Loaded 시 시퀀스 목록 자동 채움, 버튼 클릭 시 결과를 로그(`Logging.PrintLog`)와 `MessageBox`로 표시
- 코드/XAML 양쪽에 "임시/TEMP" + 삭제 조건(IAxisController 구현 완료 시) 한글 주석 명시

## Task Commits

Each task was committed atomically:

1. **Task 1: Custom/SystemHandler.cs 에 임시 wrapper 메서드 DebugManualZTrigger 추가** - `3b0c5ee` (feat)
2. **Task 2: MainView 하단에 임시 수동 Z 트리거 패널 추가 (XAML + 코드비하인드)** - `9157150` (feat)
3. **Task 3: Debug|x64 빌드 검증 + 로직 정적 재확인** - 코드 변경 없음(검증 전용), 커밋 없음

## Files Created/Modified
- `WPF_Example/Custom/SystemHandler.cs` - `DebugManualZTrigger` internal 메서드 추가 (ProcessPrep 정의 바로 아래, `ApplyPrepToSequences` 바로 위). 기존 `ProcessPrep`/`ProcessTest` 무변경.
- `WPF_Example/UI/ContentItem/MainView.xaml` - RowDefinitions 에 5번째 행(Auto) 추가 + Grid 자식 끝에 `panel_ManualZTrigger` Border(Grid.Row="4") 추가. 기존 4개 행/자식 요소 무변경.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `MainView_Loaded` 끝에 `PopulateManualZSeqCombo()` 호출 한 줄 추가 + 신규 메서드 `PopulateManualZSeqCombo()`, `ManualZTriggerButton_Click(object, RoutedEventArgs)` 추가.

## Decisions Made
- PREP 실패 시 TEST 를 진행하지 않고 즉시 false 반환 — 조명/z_index 미적용 상태로 검사가 도는 오동작을 막기 위함(if-else, 삼항 미사용).
- TestPacket 의 `TestID`/`TestType`/`IndexNumber`/`Type`/`Identifier2` 는 기본값 그대로 두고 `Identifier`만 세팅 — plan 의 `<interfaces>` 조사 결과(StartAll 은 이 필드들을 트리거 흐름에서 소비하지 않음)에 따름.
- UI 패널 위치를 이미지 뷰어(Row 1, HWindowControlWPF 호스팅)와 분리된 Row 4 에 배치 — HWND airspace 로 오버레이가 가려지는 기존 프로젝트 함정(halcon_hwnd_airspace)을 회피.

## Deviations from Plan

None - plan executed exactly as written. 모든 시그니처/필드는 plan 의 `<interfaces>` 블록에 이미 조사돼 있었고 재조사 없이 그대로 사용했다.

## Issues Encountered
- Git Bash 에서 MSBuild `/p:` 스타일 스위치가 경로로 오인식되어 `MSB1008`/`MSB1001` 오류 발생. `MSYS2_ARG_CONV_EXCL="*"` 환경변수 + `-p:`/`-t:`/`-v:`/`-nologo` 대시 스타일 스위치로 전환해 해결(빌드 스크립트 자체는 무변경, 실행 방식만 조정).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Debug|x64 빌드 PASS(에러 0, 신규 경고 0 — 기존 경고 6건은 이번 변경과 무관한 사전 존재 경고).
- 정적 회귀 검증: `ProcessPrep`/`ProcessTest`/`StartAll`/`Sequences.Start` 시그니처·본문 무변경 확인(diff 상 추가만 존재).
- 신규 코드에 삼항 연산자(`?:`) 없음 확인.
- 앱 실행/육안 UAT(SIMUL_MODE 로 버튼 클릭→로그 확인)는 수행하지 않음 — plan 상 "선택" 단계이며 빌드 PASS + 정적 재확인으로 완료 처리. 필요 시 사용자가 직접 SIMUL_MODE 로 앱을 띄워 버튼 동작을 확인할 수 있음.
- IAxisController 실제 구현 phase 진행 시 `DebugManualZTrigger` 메서드 전체(SystemHandler.cs) + `panel_ManualZTrigger` Border/RowDefinition(MainView.xaml) + `PopulateManualZSeqCombo`/`ManualZTriggerButton_Click`(MainView.xaml.cs) 를 통째로 삭제할 것.

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/SystemHandler.cs` (DebugManualZTrigger 포함)
- FOUND: `WPF_Example/UI/ContentItem/MainView.xaml` (panel_ManualZTrigger 포함)
- FOUND: `WPF_Example/UI/ContentItem/MainView.xaml.cs` (ManualZTriggerButton_Click 포함)
- FOUND commit `3b0c5ee`
- FOUND commit `9157150`
- Debug|x64 빌드 PASS 확인(에러 0)

---
*Phase: quick-260713-ej3*
*Completed: 2026-07-13*
