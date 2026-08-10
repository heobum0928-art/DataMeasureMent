---
phase: quick-260807-lbu
plan: 01
subsystem: infra
tags: [systemhandler, offlineinspectmode, fail-safe-default, startup, settingini]

# Dependency graph
requires: []
provides:
  - "SystemHandler.Initialize() 진입부에 OfflineInspectMode 강제 OFF 블록 (fail-safe default)"
  - "실제로 켜져 있던 경우에만 Setting.ini 에 즉시 반영하는 조건부 Save 패턴"
affects: [offline-inspect-mode, tcp-test-path, settingwindow]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "앱 생애주기에서 가장 안전한 지점(Load 완료 직후, 런타임 코드가 Setting을 건드리기 전)에 위험한 영속 플래그를 fail-safe default로 리셋"
    - "조건부 Save (실제 리셋이 발생한 경우에만 디스크 쓰기) 로 정상 기동 시 회귀 표면을 0으로 유지"

key-files:
  created: []
  modified:
    - WPF_Example/SystemHandler.cs

key-decisions:
  - "AfterLoad() 대신 SystemHandler.Initialize() 에 리셋 배치 — AfterLoad()는 SettingWindow가 열릴 때마다 재실행되어, 실행 중 사용자가 켠 OfflineInspectMode를 설정 창을 다시 열기만 해도 꺼버리는 회귀를 유발하므로 명시적으로 기각"
  - "무조건 Save가 아니라 '실제로 껐을 때만' Save — SettingWindow 생성자가 열릴 때마다 Load()를 다시 하므로 디스크에 True가 남아있으면 설정 창을 여는 행위 자체가 리셋을 무효화함(구멍); 동시에 이미 false인 정상 기동에서는 디스크 쓰기 0회로 회귀 표면 최소화"
  - "Setting.Save() 를 try/catch 로 감싸 실패해도 앱 시작을 막지 않음 — 메모리 값은 이미 OFF이므로 안전 목적은 Save 성공 여부와 무관하게 이미 달성됨"

requirements-completed: [OFFLINE-RESET-01]

# Metrics
duration: 12min
completed: 2026-08-07
---

# Quick Task 260807-lbu: OfflineInspectMode 강제 OFF Summary

**앱을 새로 켤 때마다 SystemHandler.Initialize() 진입부에서 OfflineInspectMode를 무조건 false로 리셋하고, 실제로 켜져 있던 경우에만 Setting.ini에도 즉시 반영하는 fail-safe default 1개 블록 추가.**

## Performance

- **Duration:** 약 12분 (커밋 타임스탬프 기준)
- **Started:** 2026-08-07 (세션 시작)
- **Completed:** 2026-08-07T15:41:09+09:00 (커밋 b6e0021)
- **Tasks:** 2/2 auto 완료, 1개는 checkpoint:human-verify (사용자 실기 확인 대기, 아래 참고)
- **Files modified:** 1 (`WPF_Example/SystemHandler.cs`)

## Accomplishments
- `SystemHandler.Initialize()` 의 HALCON `SetSystem` 캐시 블록 직후 / `Stopwatch` 시작 직전에 `OfflineInspectMode` 강제 OFF 블록 삽입 — 이 값을 읽는 모든 코드(`Action_FAIMeasurement.cs` EStep.Grab/GrabOrLoadDatumImage, `InspectionListView.xaml.cs` RUN 확인 팝업, TCP `$TEST` 경로)보다 앞선 지점
- 실제로 켜져 있던 경우에만 `Setting.Save()` 를 try/catch 로 1회 호출해 `Setting.ini` 까지 즉시 동기화 — `SettingWindow` 생성자가 열릴 때마다 `Load()`를 재실행해 디스크의 `True`가 되살아나는 구멍을 차단
- 같은 파일에 있던 사용자의 미커밋 실험(`memory_allocator` 주석처리)을 커밋에서 완전히 분리 — 스테이징 전 임시 원복 → diff 게이트(0건 확인) → 커밋 → 재원복의 6단계 절차로 격리
- Debug/x64 정상 경로(스크래치 아님) Rebuild 성공, 신규 error CS 0건 / 신규 warning CS 0건

## Task Commits

Each task was committed atomically:

1. **Task 1: Initialize() 진입부에 OfflineInspectMode 강제 OFF 블록 추가 + Debug/x64 빌드** - 코드는 Task 2와 동시에 커밋됨 (아래 참고, 스테이징 절차상 별도 커밋 없이 바로 Task 2 절차로 병합)
2. **Task 2: 사용자 실험을 분리한 채 SystemHandler.cs 만 커밋** - `b6e0021` (fix) — `fix(quick-260807-lbu): 앱 시작 시 OfflineInspectMode 강제 OFF`
3. **Task 3: 실기 확인** - 코드 변경 없음, 사용자 확인 대기 (아래 "Task 3" 섹션 참고)

**Plan metadata:** 이 커밋(SUMMARY.md/STATE.md)은 오케스트레이터가 별도로 커밋함 — 이 실행자는 docs 커밋을 만들지 않음.

_Note: Task 1의 코드 작성과 Task 2의 스테이징-격리 절차가 같은 물리적 편집 내용을 다루므로 최종 커밋은 `b6e0021` 하나뿐이다. Task 1의 자동검증([1]~[12])은 커밋 전 워킹트리 상태에서 전부 통과 확인 후 Task 2로 진행했다._

## Files Created/Modified
- `WPF_Example/SystemHandler.cs` - `Initialize()` 진입부에 `if (Setting.OfflineInspectMode) { Setting.OfflineInspectMode = false; ... try { Setting.Save(); } catch { ... } }` 블록 22줄 추가

## Decisions Made

**1. Save 여부 결정 근거 (SettingWindow 구멍 → 조건부 Save 채택):**
`WPF_Example/UI/Setting/SettingWindow.xaml.cs:24-26` 의 생성자가 열릴 때마다 `pSetting.Load()` 를 호출해 `Setting.ini` 전체를 다시 읽는다. 만약 메모리 값만 false 로 리셋하고 디스크에 `True` 를 남겨두면, 사용자가 (심지어 "꺼졌는지 확인하려고") 설정 창을 여는 것만으로 디스크의 `True` 가 메모리로 되살아나 시작 시 리셋이 통째로 무효화된다. 그래서 **실제로 켜져 있어서 리셋이 발생한 경우에만** `Setting.Save()` 로 디스크까지 즉시 반영한다. 반대로 이미 false 인 정상 기동에서는 아무 조건도 만족하지 않아 `Save()` 자체가 호출되지 않으므로, 매 기동마다 불필요한 INI 전체 재기록이 생기지 않는다(회귀 표면 = 0). `Save()` 실패(파일 잠김/권한) 가능성에 대비해 try/catch 로 감쌌다 — 메모리 값은 이미 OFF 이므로 Save 가 실패해도 원래의 안전 목적(저장 이미지로 검사되는 사고 방지)은 이미 달성된 상태다.

**2. `AfterLoad()` 대안 기각 사유:**
`Custom/SystemSetting.cs:41-48` 의 `AfterLoad()` (→ `RestorePcRoleDefault()` 등)는 "Load 후처리 값 보정"의 기존 관례라 형식만 보면 더 자연스러워 보였다. 하지만 `AfterLoad()` 는 `Load()` 가 호출될 때마다 실행되고, `SettingWindow` 는 열릴 때마다 `Load()` 를 호출한다. 따라서 사용자가 실행 중에 `OfflineInspectMode` 를 직접 켜고 확인(OK)한 뒤, 나중에 설정 창을 다시 열면 그 순간 `AfterLoad()` 가 재실행되어 사용자의 ON 이 조용히 꺼져버린다. 이는 계획의 명시적 제약("실행 중 사용자가 직접 켜는 기능은 100% 그대로")을 정면 위반하므로 채택하지 않았다. `SystemHandler.Initialize()` (앱 시작 시 1회만 실행)에 넣는 것이 유일하게 안전한 지점이다.

## Deviations from Plan

None - plan executed exactly as written. 계획이 명시한 삽입 위치, 코드, 절차, 스타일(K&R, 공백 12칸, 삼항연산자 금지, `quick-260807-lbu:` 주석 접두)을 그대로 따랐다.

## Issues Encountered

**1. Task 2 (5) 해시 검증 결과: 불일치, 대체 판정으로 통과**

계획은 "사용자 실험 원복 후 `SystemHandler.cs` diff 해시가 baseline `c3cfe91472977903dd2ed061d6b088f92f58c207` 와 같아야 한다"고 명시했으나, 실측 해시는 `c06029cb76784edddb265a57cb24ce5942edf102` 로 baseline 과 달랐다.

계획에 명시된 대체 판정 절차(`git diff -- WPF_Example/SystemHandler.cs` 원문이 **hunk 1개**이고 `-`/`+` 각 1줄이 **둘 다 `memory_allocator` 줄**인지 확인)를 수행한 결과:
```diff
@@ -125,7 +125,7 @@ namespace ReringProject {
-                HOperatorSet.SetSystem("memory_allocator", "system");
+                //HOperatorSet.SetSystem("memory_allocator", "system");
```
정확히 hunk 1개, `-`/`+` 각 1줄이 모두 `memory_allocator` 줄임을 확인 — **실질 동등, 대체 판정으로 PASS**. (해시가 달랐던 이유: baseline은 우리 리셋 블록 삽입 전 diff 컨텍스트 라인 기준이었고, 커밋 후 남은 diff는 리셋 블록이 이미 커밋에 포함되어 diff 대상에서 빠지면서 hunk의 컨텍스트 라인 위치가 달라졌기 때문으로 추정 — 계획이 미리 예견해 대체 판정 절차를 마련해둔 정확히 그 케이스였다.)

**2. 동시 진행 중인 별도 작업 감지 (내 작업 범위 밖, 정보 공유 목적으로만 기록)**

작업 중 `git status --porcelain` 을 여러 차례 확인하는 과정에서, 이 저장소에 대해 **다른 quick task(`260807-lh7-reset-tcp-ack`, TCP ack 리셋 관련으로 추정)가 병행 진행 중**임을 발견했다: `WPF_Example/TcpServer/VisionRequestPacket.cs`/`VisionResponsePacket.cs`, `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`, `WPF_Example/Custom/SystemHandler.cs` 가 내 세션 도중 추가로 `M` 상태가 되는 것을 확인했다(이 파일들은 내가 편집한 적이 없다). `git add` 를 항상 `WPF_Example/SystemHandler.cs` 로 파일 단위 지정했기 때문에 내 커밋(`b6e0021`)에는 영향이 없으며, 계획이 명시한 무변경 대상 5개 파일과도 무관한 파일들이다. 이 발견은 내 작업의 실패나 이슈가 아니라 **저장소가 공유 워킹트리 상태에서 동시 세션에 노출돼 있다는 사실 자체**를 기록해두는 것으로, 필요 시 사용자가 세션 격리(워크트리 분리 등) 여부를 판단할 수 있도록 남긴다.

## User Setup Required

None - 코드 변경만으로 완결되며 외부 서비스/환경변수 설정 불필요.

## Task 3: 실기 확인 (checkpoint:human-verify) — 사용자 수동 확인 필요

이 실행 컨텍스트에서는 실제 앱을 종료→재시작하며 사람이 화면을 보고 확인하는 절차를 대신 수행할 수 없다(하드웨어/UI 육안 확인이 필수인 항목들). 아래 A~E 항목은 **사용자가 직접 확인해야 완료로 간주된다.** Task 1/2 의 정적 검증(빌드 통과, 코드 삽입 위치/내용 검증)은 위에서 전부 자동 완료했다.

| 항목 | 확인 내용 | 상태 |
|------|-----------|------|
| A | 실행 중 설정 창에서 `OfflineInspectMode` 를 켠 뒤 창을 다시 열어도 켜진 채 유지되는지 (기존 기능 무회귀) | requires manual verification by user |
| B | 프로그램을 껐다 켜면 `OfflineInspectMode` 가 자동으로 꺼져 있는지 (이번 수정의 핵심) | requires manual verification by user |
| C | `Setting.ini` 파일에 `OfflineInspectMode=False` 로 반영됐는지 | requires manual verification by user |
| D | Trace 로그에 `[STARTUP] OfflineInspectMode was ON in Setting.ini - forced OFF at startup.` 한 줄이 남았는지 | requires manual verification by user |
| E | RUN 버튼 확인 팝업(오프라인 검사 모드)이 기존과 동일하게 뜨는지 (UI 로직 무변경 확인) | requires manual verification by user |

PLAN.md 의 `<how-to-verify>` 섹션에 사용자용 상세 절차(1~13단계)가 그대로 남아 있으므로, 사용자가 준비되면 그 절차를 그대로 따라가면 된다. 이 실행은 위 A~E 승인 여부와 무관하게 **완료로 보고**하며(오케스트레이터 지시에 따름), 만약 사용자가 이후 문제를 보고하면 후속 quick task 로 수정한다.

## Next Phase Readiness
- 코드 변경(`b6e0021`) 은 완결 상태이며 추가 작업 불필요
- 사용자의 미커밋 실험 3건(`DatumMeasurement.csproj` SIMUL_MODE 제거, `Custom/Device/LightHandler.cs`, `SystemHandler.cs` 의 `memory_allocator` 주석처리)은 baseline 그대로 워킹트리에 보존됨
- Task 3 실기 승인만 남음 — 승인 시 별도 조치 불필요, 미승인(문제 보고) 시 후속 quick task 로 대응

---
*Phase: quick-260807-lbu*
*Completed: 2026-08-07*

## Self-Check: PASSED

- FOUND: `WPF_Example/SystemHandler.cs`
- FOUND: `.planning/quick/260807-lbu-offlineinspectmode-off/260807-lbu-SUMMARY.md`
- FOUND: commit `b6e0021` in `git log --oneline --all`
- Working tree post-execution: only the 3 pre-existing baseline experiments remain `M` (`WPF_Example/Custom/Device/LightHandler.cs`, `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/SystemHandler.cs` — the last one being solely the user's `memory_allocator` comment-out, confirmed via `git diff` showing exactly that 1-line hunk). No leftover uncommitted `OfflineInspectMode` changes.
