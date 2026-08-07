---
phase: quick-260807-iml
plan: 01
subsystem: infra
tags: [light-controller, jpf-1208, device-config, side-pc]

# Dependency graph
requires: []
provides:
  - "SIDE PC LightHandler.RegisterLightController() 재배치: Controller A(COM2) 8채널, Controller B(COM3) 5채널"
affects: [light.ini 동기화(운영 인수인계), TOP/BOTTOM PC 동일 배치와의 정합]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Device/LightHandler.cs

key-decisions:
  - "상수 선언 순서만 소속 그룹(Controller A/B) 기준으로 재편, 문자열 리터럴 값과 이름은 15개 전부 무변경"
  - "Groups.Add(...) 5종 등록 블록은 이름 기반 조회이므로 컨트롤러 재배치에 영향받지 않아 원문 그대로 보존"
  - "light.ini ChannelNames override 동기화는 범위 밖으로 명시 — 사용자가 별도 처리(T-IML-04, transfer)"

patterns-established: []

requirements-completed: [LIGHT-REMAP-8-5]

coverage:
  - id: D1
    description: "Controller A(Index=0, COM2)가 8채널(RING_CH1~6, BACK, RING7)로, Controller B(Index=1, COM3)가 5채널(BAR_1~4, ALIGN_COAX)로 등록되도록 RegisterLightController() 재배치"
    requirement: "LIGHT-REMAP-8-5"
    verification:
      - kind: other
        ref: "정적 게이트 스크립트 (Task 1 <verify>, 9개 항목: G1~G8b)"
        status: pass
      - kind: other
        ref: "13개 채널명 유일성 검사 (Task 2 <verify> 첫 번째 automated 블록)"
        status: pass
      - kind: integration
        ref: "msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build"
        status: pass
    human_judgment: true
    rationale: "코드/빌드 검증은 자동으로 완결되지만, light.ini ChannelNames override 동기화(T-IML-04)가 사용자 별도 처리로 이월되어 있어 실제 물리 조명 점등 확인은 이 계획 범위 밖 — 하드웨어 UAT는 사용자 판단 필요"

# Metrics
duration: 15min
completed: 2026-08-07
status: complete
---

# Quick Task 260807-iml Summary

**SIDE PC LightHandler.RegisterLightController()의 두 JPF 컨트롤러 채널 배치를 물리 재편성(Controller A 7→8채널, Controller B 6→5채널)에 맞춰 재매핑**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-08-07T04:34:42Z
- **Tasks:** 2 (1 tracer + 1 auto)
- **Files modified:** 1

## Accomplishments
- `WPF_Example/Custom/Device/LightHandler.cs`의 `RegisterLightController()`에서 두 `Controllers.Add(...)` 호출을 신규 배치로 교체
- `LIGHT_*` 상수 15개의 선언 순서를 실제 컨트롤러 소속에 맞게 재편성 (값/이름은 무변경)
- XML doc D-06/D-07 라인을 신규 채널 수(8/5)와 구성으로 갱신
- 구 7채널/6채널 분할을 설명하던 잔존 주석(헤더 스탬프 4곳 + Phase 64 날짜 스탬프) 전부 제거
- 정적 게이트 9개 + 13개 채널명 유일성 검사 + Debug/x64 빌드로 3중 검증

## 최종 채널 배치표

| 컨트롤러 인덱스 | COM 포트 | 채널 번호(0-base) | 논리 이름 |
|---|---|---|---|
| 0 | COM2 | 0 | RING_CH1 |
| 0 | COM2 | 1 | RING_CH2 |
| 0 | COM2 | 2 | RING_CH3 |
| 0 | COM2 | 3 | RING_CH4 |
| 0 | COM2 | 4 | RING_CH5 |
| 0 | COM2 | 5 | RING_CH6 |
| 0 | COM2 | 6 | BACK |
| 0 | COM2 | 7 | RING7 |
| 1 | COM3 | 0 | BAR_1 |
| 1 | COM3 | 1 | BAR_2 |
| 1 | COM3 | 2 | BAR_3 |
| 1 | COM3 | 3 | BAR_4 |
| 1 | COM3 | 4 | ALIGN_COAX |

## Task Commits

Each task was committed atomically:

1. **Task 1: RegisterLightController 8채널/5채널 재배치 + 딸린 주석 정합화** - `8b07410` (feat)
2. **Task 2: 채널 이름 유일성 정적 검증 + Debug/x64 빌드** - 코드 변경 없음(검증 전용, 커밋 대상 없음)

**Plan metadata:** (오케스트레이터가 후속 단계에서 커밋)

## Files Created/Modified
- `WPF_Example/Custom/Device/LightHandler.cs` - `RegisterLightController()` 두 컨트롤러 등록 재배치, 상수 선언 순서 재편, XML doc D-06/D-07 갱신, 구 배치 설명 주석 전량 제거

## 검증 결과

**Task 1 — 9개 정적 게이트 실측값 (전부 기대값과 일치):**

| 게이트 | 기대값 | 실측값 |
|---|---|---|
| G1 (`new JPFLightController(0, 8)` 존재) | 1 | 1 |
| G2 (`new JPFLightController(1, 5)` 존재) | 1 | 1 |
| G3 (구 arity `(0, 7)`/`(1, 6)` 잔존) | 0 | 0 |
| G4 (`Groups.Add(new LightGroup(` 개수) | 5 | 5 |
| G5 (`= 7채널`/`= 6채널` 잔존 서술) | 0 | 0 |
| G6 (구 Controller 소속 헤더 스탬프 잔존) | 0 | 0 |
| G7 (`public const string LIGHT_` 선언 개수) | 15 | 15 |
| G8a (Controller A 마지막 이름 `LIGHT_BACK, LIGHT_RING7))`) | 1 | 1 |
| G8b (Controller B 마지막 이름 `LIGHT_BAR_4, LIGHT_ALIGN_COAX))`) | 1 | 1 |

**Task 2 — 유일성 검사:** 13개 물리 채널명(`LIGHT_RING_CH1`~`LIGHT_RING_CH6`, `LIGHT_BACK`, `LIGHT_RING7`, `LIGHT_BAR_1`~`LIGHT_BAR_4`, `LIGHT_ALIGN_COAX`)이 두 `Controllers.Add(...)` 등록 구간에 각각 정확히 1회씩 나타남 — `FAIL` 출력 0건, `uniqueness-check-done`만 출력.

**Task 2 — msbuild Debug/x64:**
```
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal
```
결과: Exit code 0, `DatumMeasurement -> C:\code\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe`. 신규 `error CS` 0건. 출력에 나타난 `warning CS` 7건은 전부 `Sequence_Top.cs`/`Sequence_Bottom.cs`/`SequenceHandler.cs`/`VirtualCamera.cs`의 기존 Obsolete/도달불가 코드 경고로, `LightHandler.cs`를 언급하는 항목 0건(이번 변경으로 인한 신규 warning 없음).

## Decisions Made
- 상수 선언 순서 재편성은 C#에서 필드 선언 순서가 동작에 영향을 주지 않으므로 순수 가독성/정합성 변경으로 처리 — 값/이름은 무변경
- `Groups.Add(...)` 5종 블록은 계획 지시대로 한 글자도 건드리지 않음 — 채널 이름 기반 조회이므로 컨트롤러 재배치와 무관하게 유효
- msbuild가 bash PATH에 없어 `/p:` 형태 인자가 Git Bash 경로 변환으로 깨지는 문제를 `-p:` 축약 표기로 우회 (동작 동일, 셸 호환성 이슈일 뿐)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- msbuild가 PATH에 없어 Visual Studio 설치 경로(`C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`)를 직접 탐색해 사용. `/p:` 형태 스위치가 Git Bash의 경로 변환으로 손상되어 `-p:` 축약 표기로 대체 — 둘 다 msbuild 표준 문법이며 동작은 동일.

## User Setup Required

None - no external service configuration required.

**미결 의존성 (T-IML-04, transfer, 범위 밖):** `LightHandler.Load()`는 `D:\Data\Light\light.ini`의 `[Controller0]`/`[Controller1]` 섹션에 `ChannelNames` 키가 있으면 이번에 코드로 심은 신규 배치를 런타임에 덮어쓴다. 구 7채널/6채널 시절에 `Save()`로 저장된 ini가 남아 있으면 `[Controller0]`은 앞 7개가 옛 이름으로 덮여 `BACK`/`RING7` 자리가 오배선되고, `[Controller1]`은 옛 6개 이름이 신규 5채널 한도에서 잘려 `ALIGN_COAX` 자리가 엉뚱한 이름으로 남는다 — 예외 없는 무음 오배선. **사용자가 `light.ini`/`Setting.ini`를 이 작업과 별도로 직접 처리하기로 확인**했으므로 이번 실행에서는 `WPF_Example/Custom/Device/LightHandler.cs` 외 어떤 파일도 손대지 않았다.

## Next Phase Readiness
- 코드/빌드 게이트 전부 통과, `WPF_Example/Custom/Device/LightHandler.cs` 외 수정 파일 0개
- 실기 조명 점등 확인은 `light.ini` 동기화가 끝난 뒤에야 의미가 있으므로 이번 quick task의 완료 조건에 포함하지 않음(하드웨어 UAT, 범위 밖)

---
*Quick task: 260807-iml*
*Completed: 2026-08-07*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Device/LightHandler.cs
- FOUND: commit 8b07410
