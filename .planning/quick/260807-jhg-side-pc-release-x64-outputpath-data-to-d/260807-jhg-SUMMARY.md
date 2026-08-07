---
phase: quick-260807-jhg
plan: 01
subsystem: infra
tags: [msbuild, csproj, release-x64, outputpath, deployment]

# Dependency graph
requires: []
provides:
  - "Release|x64 OutputPath 절대경로화 — 체크아웃 위치와 무관하게 D:\\Data\\ 로 결정론적 착지"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "빌드 산출물 경로는 상대경로 대신 절대경로로 고정해 체크아웃 드라이브/깊이 의존성 제거"

key-files:
  created: []
  modified:
    - "WPF_Example/DatumMeasurement.csproj"

key-decisions:
  - "인접한 선행 미커밋 변경(Release|x64 DefineConstants SIMUL_MODE 제거)을 되돌리지 않고 이번 OutputPath 변경과 co-commit — 사용자가 명시적으로 '같이 커밋(추천)' 선택"

patterns-established: []

requirements-completed: [BUILD-OUTPATH-ABS-01]

coverage:
  - id: D1
    description: "Release|x64 PropertyGroup 의 OutputPath 를 4단계 상대 부모경로에서 절대경로 D:\\Data\\ 로 교체"
    requirement: "BUILD-OUTPATH-ABS-01"
    verification:
      - kind: other
        ref: "grep -F 신규경로 1건 / 구경로 0건 / 나머지 3개 OutputPath 무변경(bin\\ 3건) / diff 정확히 2줄(삭제1+추가1) / DefineConstants TRACE 보존"
        status: pass
      - kind: other
        ref: "MSBuild.exe DatumMeasurement.csproj -t:Build -p:Configuration=Debug -p:Platform=x64 -v:minimal (Debug/x64 무회귀 빌드)"
        status: pass
    human_judgment: false

# Metrics
duration: 15min
completed: 2026-08-07
status: complete
---

# Quick Task 260807-jhg: Release|x64 OutputPath 절대경로화 Summary

**Release|x64 빌드 출력 경로를 4단계 상대 부모경로(`..\..\..\..\Data\`)에서 절대경로 `D:\Data\` 로 교체해, 리포지토리 체크아웃 드라이브/깊이와 무관하게 항상 실제 배포 폴더로 착지하도록 고정**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-07T04:53:00Z (approx)
- **Completed:** 2026-08-07T05:08:42Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- `WPF_Example/DatumMeasurement.csproj` 의 Release|x64 PropertyGroup `OutputPath` 를 `..\..\..\..\Data\` → `D:\Data\` 로 교체
- 정적 grep 검증 5건(신규경로 1건 / 구경로 0건 / 나머지 3개 OutputPath 무변경 / 1줄 diff / DefineConstants 보존) 전부 통과
- Debug|x64 재빌드 무회귀 확인(신규 CS 에러 0건, 기존 경고만 존재)

## Task Commits

Each task was committed atomically:

1. **Task 1: Release|x64 OutputPath 를 절대경로 D:\Data\ 로 교체** - `ca3b213` (fix)

**Plan metadata:** (docs 커밋은 오케스트레이터가 후속 단계에서 처리)

## Files Created/Modified
- `WPF_Example/DatumMeasurement.csproj` - Release|x64 PropertyGroup 의 `OutputPath` 값을 절대경로 `D:\Data\` 로 교체. 같은 PropertyGroup 의 `DefineConstants`(`TRACE;SIMUL_MODE` → `TRACE`, 선행 미커밋 변경) 도 같은 커밋에 실림.

## Decisions Made
- **Co-commit 승인:** 편집 대상 라인 바로 아래에 세션 시작 전부터 있던 미커밋 `DefineConstants` 변경(SIMUL_MODE 제거)이 존재했다. 오케스트레이터가 사용자에게 "같이 커밋" vs "분리" 여부를 물었고, 사용자는 "같이 커밋(추천)" 을 선택했다 — 두 변경 모두 Release|x64 스코프이고 실HW 테스트로의 전환이라는 같은 방향성이기 때문. 이에 따라 파일 단위 `git add` 로 두 변경을 한 커밋에 실었으며, 커밋 메시지에 두 변경 모두 명시했다.
- **`Action_TopInspection.cs` 미접촉 유지:** 이 파일에는 이번 작업/DefineConstants 변경 모두와 무관한 별도의 선행 미커밋 변경이 있어, 이번 작업 범위 밖으로 두고 스테이징하지 않았다(제약사항 준수).

## Deviations from Plan

None - plan executed exactly as written. (정확히 1개 task, 정확히 1줄 값 교체, `read_first`/`precondition` 지시대로 진행)

## Issues Encountered

- **Verify (e) 예상치와 실측치 불일치 (경미, 코드 결함 아님):** 계획의 `verify` 항목 (e) 는 `grep -c -F '<DefineConstants>TRACE</DefineConstants>'` 결과가 정확히 `1` 일 것으로 예상했으나 실측은 `2` 였다. 원인 확인: `WPF_Example/DatumMeasurement.csproj` 의 Release|AnyCPU PropertyGroup(73행 이전, Release|x64 와 무관) 도 이번 세션 이전부터 이미 `<DefineConstants>TRACE</DefineConstants>` 값을 갖고 있었다(git HEAD 에도 동일 — `git show HEAD:...csproj | grep` 로 확인). 즉 파일 전체에서 정확한 문자열 `TRACE` 를 가진 `DefineConstants` 가 원래 2곳(Release|AnyCPU + Release|x64) 존재했던 것이며, 계획 작성 시점에 이 사실이 누락되어 예상치가 `1` 로 잘못 산정된 것으로 보인다. 실제 검증 목표(Release|x64 의 `DefineConstants` 가 이번 편집으로 훼손되지 않고 `TRACE` 로 보존됨)는 `git diff` 로 별도 확인해 충족을 확정했다 — 이번 편집이 만든 diff 는 OutputPath 값 1줄뿐이며 DefineConstants 라인은 손대지 않았다(diff 컨텍스트로 확인). 코드/파일 수정 없이 검증 해석만 보정했으므로 auto-fix 로 분류하지 않는다.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Release|x64 재빌드는 이번 계획 범위 밖(out_of_scope 명시)이었으므로 실행하지 않았다. 다음 단계로 실제 Release|x64 빌드 시 `D:\Data\Setting.ini` 가 다른 레거시 프로그램 스키마를 담고 있을 수 있다는 알려진 후속 리스크가 있으며, 오케스트레이터가 이를 사용자에게 별도로 제기할 예정이다.
- `C:\Data\` 잔여 폴더/프로세스 정리는 이번 범위 밖이며 미처리 상태로 남아 있다.

---
*Phase: quick-260807-jhg*
*Completed: 2026-08-07*
