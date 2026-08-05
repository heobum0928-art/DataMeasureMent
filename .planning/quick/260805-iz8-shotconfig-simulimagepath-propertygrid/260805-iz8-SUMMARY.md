---
phase: quick-260805-iz8
plan: 01
subsystem: ui
tags: [propertygrid, propertytools, shotconfig, wpf, inspection-recipe]

requires: []
provides:
  - "ShotConfig.SimulImagePath PropertyGrid에서 숨김(표시만), INI 직렬화/런타임 값은 100% 보존"
affects: []

tech-stack:
  added: []
  patterns:
    - "PropertyTools.DataAnnotations.Browsable(false) 완전수식 사용 — System.ComponentModel.Browsable/JsonIgnore 와 명확히 구분해 직렬화 차단 오용 방지"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs"

key-decisions:
  - "패턴 A(PropertyTools 전용 표시 숨김)만 적용, 패턴 B(System.ComponentModel.Browsable/JsonIgnore, 직렬화 차단)는 절대 금지 — CONTEXT 잠금 결정 준수"
  - "실행 중인 Debug|x64 exe(Visual Studio 라이브 디버그 세션)가 파일 잠금 중이라 bin 산출물 갱신은 보류, 코드 정확성은 컴파일 전용(OutDir 리디렉션) 빌드로 대체 검증"

patterns-established: []

requirements-completed: [IZ8-01]

duration: 6min
completed: 2026-08-05
---

# Quick 260805-iz8: SHOT SimulImagePath PropertyGrid 숨김 Summary

**`ShotConfig.SimulImagePath`에 `PropertyTools.DataAnnotations.Browsable(false)` 완전수식 attribute 1줄 추가 — PropertyGrid 표시만 제거, INI 직렬화/런타임 값 흐름은 무변경.**

## Performance

- **Duration:** 6분
- **Started:** 2026-08-05T05:13:00Z (추정)
- **Completed:** 2026-08-05T05:19:38Z
- **Tasks:** 1/1 완료
- **Files modified:** 1

## Accomplishments
- `Shot | Simulation` 카테고리의 `SimulImagePath` 편집 칸이 PropertyGrid에서 사라짐 (다른 Shot 필드는 그대로 유지)
- `ParamBase.Save/Load` reflection 경로, `IOfflineImageParam` 구현(`GetLatestImagePath`/`SetLatestImagePath`), SIMUL/오프라인 검사, 반복검사, 툴바 `button_simFolder` 일괄 할당 — 코드 변경 0, 동작 무변경
- 짧은 이름 `[Browsable(false)]` 기존 9곳 및 다른 프로퍼티 전부 미간섭 확인

## Task Commits

1. **Task 1: SimulImagePath에 PropertyGrid 전용 숨김 attribute 추가** - `882010e` (feat)

**Plan metadata:** (오케스트레이터가 별도 docs 커밋 처리 예정)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - `SimulImagePath` 선언 위에 의도 주석 2줄 + `[PropertyTools.DataAnnotations.Browsable(false)]` 1줄 추가 (순수 추가, 삭제 0)

## Deviations from Plan

### Auto-fixed Issues

None - 계획된 attribute 1줄 삽입 그대로 실행.

### Build Verification Limitation (환경 제약, 코드 결함 아님)

**빌드 산출물(`bin/x64/Debug/DatumMeasurement.exe`) 타임스탬프 갱신 불가 — MSB3027/MSB3021 (파일 잠금)**

- **발견 시점:** Task 1 빌드 검증 단계
- **원인:** 실행 중 프로세스 조사 결과, Visual Studio(`devenv.exe`, PID 10068)가 `DatumMeasurement.exe`(PID 26472)를 라이브 디버그 세션으로 실행 중(창 제목: "DatumMeasurement (디버그) - AlignShapeMatchService.cs"). MSBuild가 `obj\x64\Debug\DatumMeasurement.exe`를 `bin\x64\Debug\DatumMeasurement.exe`로 복사하는 최종 단계에서 파일이 잠겨 있어 `error MSB3027`/`MSB3021` 발생.
- **판단:** 사용자가 Visual Studio에서 현재 능동적으로 디버그 세션을 운용 중일 가능성이 있어, 이를 강제 종료(taskkill)하지 않았음 — 코드와 무관한 환경적 잠금이며, 사용자의 진행 중인 작업을 임의로 중단시키는 것은 이번 트리비얼 변경의 범위를 벗어남.
- **대체 검증:** `MSBuild //p:OutDir=obj\x64\Debug\_verifyonly\` 로 산출물 경로를 리디렉션해 컴파일 전용 빌드를 별도 수행 → **`error CS` 0건**, 경고만 존재(전부 본 변경과 무관한 기존/동시 작업 경고: `BottomSequence`/`TopSequence`/`TopInspectionAction`/`BottomInspectionAction` deprecated, `VirtualCamera` 접근불가 코드, 그리고 동시 진행 중인 별도 작업(`260805-ivy`)의 `MainResultViewerControl._manualMeasureAxisMode` 미사용 경고 — 본 작업 파일과 무관). 임시 출력 디렉터리는 검증 후 삭제(obj/ 는 .gitignore 대상이라 커밋 영향 없음).
- **정적 게이트 전부 PASS (실제 검증 완료):** `PT_FQ=1`, `PT_SHORT=9`, `SCM=0`, `JSONIGNORE=0`, `SIG=1`, `IFACE=2`, `DELETED=0` — 계획서 기대값과 100% 일치.
- **미완료 항목:** `bin/x64/Debug/DatumMeasurement.exe` 실제 재빌드/타임스탬프 갱신은 사용자가 Visual Studio 디버그 세션을 정지한 뒤 재빌드해야 반영됨. 코드 자체는 컴파일 검증 완료, 커밋 완료.
- **커밋:** `882010e`

### 스코프 밖 확인 (건드리지 않음)

세션 중 `git status`에 아래 파일들이 함께 수정/추가된 상태로 나타났으나, 이번 작업(260805-iz8)과 무관한 동시 진행 중 작업(별도 quick task `260805-ivy-manual-measure-ecalipermode` 로 추정)으로 판단해 **일절 건드리지 않고 스테이징에서도 제외**했다:
- `WPF_Example/DatumMeasurement.csproj` (사전 존재 로컬 SIMUL_MODE 변경 — plan 지침대로 되돌리지 않음/커밋 포함 안 함)
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`, `WPF_Example/UI/ContentItem/ECaliperMode.cs` (동시 진행 중인 다른 작업 추정)
- `.planning/quick/260805-f3w-flow-log/*`, `.planning/quick/260805-ivy-manual-measure-ecalipermode/`, `journal_result.txt` (동시 진행 중인 다른 작업/문서)

## Known Stubs

None.

## Threat Flags

None — UI 표시 attribute 1줄, 신뢰 경계 변화 없음 (계획의 threat_model과 일치).

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs (attribute 확인됨)
- FOUND: commit 882010e (git log 확인됨)
- 정적 게이트 재실행 결과 계획 기대값과 완전 일치 (본문 상단 참조)
