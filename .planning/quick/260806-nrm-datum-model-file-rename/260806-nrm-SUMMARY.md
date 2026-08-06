---
phase: quick-260806-nrm
plan: 01
subsystem: vision-inspection-teaching
tags: [halcon, pattern-matching, datumconfig, inspectionsequence, file-io, propertygrid]

# Dependency graph
requires:
  - phase: 2026-07-10 티칭감사 (project_teaching_audit_260710)
    provides: carry-over #1 식별("모델파일 고아" — DatumName 개명 시 패턴 모델 파일이 따라가지 않음)
provides:
  - "DatumConfig.DatumName 세터에 옛→새 경로 계산 + File.Move 훅(패턴1/패턴2 페어)"
  - "오발동 억제 가드(_suppressModelRename) — ParamBase.Load 리플렉션 경로에서 리네임 비활성화"
  - "InitializeDatumName(세터 우회) — AddDatum 신규 Datum 초기화 전용, 기존 Datum 모델 탈취 방지"
affects: [side-inspection, top-inspection, bottom-inspection, propertygrid-teaching-ux]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "세터 내부에서 '대입 전 옛 상태 조회 → 대입 → 대입 후 새 상태 조회 → 부수효과' 순서로 라이브 재계산 리소스를 안전하게 이동시키는 패턴"
    - "리플렉션/초기화 경로가 프로퍼티 세터를 의도치 않게 때릴 때는 억제 플래그(try/finally) 또는 세터-우회 메서드로 분리"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

key-decisions:
  - "발주 요구사항 #7(1-arg 리졸버 사용 지시)을 정정 — 실제 코드에서 1-arg 오버로드는 OwnerName 을 쓰지 않고 SourceShotName 미매칭 시 전역 Shots[0] 로 폴백하며 호출부가 0건이다. 실제 전 호출부(11곳) 및 티칭 저장 경로가 쓰는 2-arg(datum, OwnerName) 로 구현했다. 1-arg 를 썼다면 티칭이 실제로 쓴 폴더와 다른 경로를 계산해 '옛 파일 없음 → 조용히 skip' 으로 버그를 그대로 재현했을 것이다."
  - "아키텍처 결정(발주 요구사항 #8, 정적 헬퍼를 InspectionSequence.cs 로 옮기는 폴백)은 불필요로 확정 — DatumConfig 가 같은 어셈블리/네임스페이스(ReringProject.Sequence)의 InspectionSequence.ResolveDatumModelPath(2) 를 직접 static 호출해도 빌드가 정상 통과했다(순환참조 컴파일 에러 없음)."

requirements-completed: []  # Task 3(checkpoint:human-verify)가 아직 미승인 — 승인 후 오케스트레이터가 TEACH-AUDIT-CO-01 을 마감 처리한다.

duration: ~25min (Task 1+2)
completed: 2026-08-06
---

# Quick 260806-nrm: DatumName 개명 시 패턴 모델 파일 자동 이동 Summary

**DatumConfig.DatumName 세터에 옛→새 경로 File.Move 훅을 추가해 개명 시 `.shm`/`.ncm`(+`_2` 페어)이 자동으로 따라가게 하고, INI 로드·AddDatum 두 오발동 경로는 억제 가드/세터-우회로 차단했다. Task 1~2(코드+빌드+정적검증+격리 하네스) 완료, Task 3(실기 PropertyGrid 검증)는 사람 승인 대기.**

## Performance

- **Duration:** ~25 min (Task 1+2)
- **Completed:** 2026-08-06
- **Tasks:** 2 of 3 완료 (Task 3 은 checkpoint:human-verify — 사람 승인 대기, 자동화 불가)
- **Files modified:** 2 (repo) + 1 (스크래치 격리 하네스, repo 밖)

## Accomplishments

- `DatumConfig.DatumName` 세터가 개명 시 패턴1(.shm/.ncm)·패턴2(_2 페어) 모델 파일을 새 이름 경로로 자동 이동시킨다.
- 이동 실패(충돌/잠김/권한)는 데이터를 파괴하지 않고 `[DatumRename]` Error 로그만 남기며, 이름 변경 자체는 항상 성립한다.
- INI 레시피 로드(`ParamBase.Load` 리플렉션 SetValue 경로)와 신규 Datum 추가(`AddDatum`) 두 오발동 경로를 각각 억제 플래그와 세터-우회 메서드로 차단했다.
- Debug/x64 빌드 신규 에러 0으로 통과(스크래치 OutputPath 폴백으로 확인 — 실제 `bin\x64\Debug\` 산출물이 실행 중인 프로세스에 잠겨 있어 원본 경로 복사는 실패했으나 컴파일 자체는 성공).
- 정적 회귀검증 7종 전부 `*_OK` — 오발동 경로 0건(직접 대입 없음), `_copyExclude` 유지, 2-arg 오버로드만 사용, 삼항/`??` 0건, JSON 역직렬화 경로 없음.
- 격리 콘솔 하네스(`MoveModelFileIfPresent` 본문 그대로 복사)로 파일이동 판정표 4케이스 전부 자동 검증 PASS.

## Task Commits

Each task was committed atomically:

1. **Task 1: DatumName 세터 모델파일 리네임 훅 + 오발동 억제 가드 구현** - `c8a1e1f` (fix)
2. **Task 2: 오발동 경로 정적 회귀검증 + 파일이동 판정표 격리 하네스 검증** - 코드 변경 없음(검증 전용, 스크래치 하네스는 저장소 밖). 커밋 없음.

**Plan metadata:** (오케스트레이터가 처리 예정 — 본 SUMMARY/STATE.md 는 이번 실행에서 커밋하지 않음)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - `DatumName` 세터에 리네임 훅, `_suppressModelRename` 억제 플래그, `InitializeDatumName`/`TryResolveModelPathQuiet`/`MoveModelFileIfPresent` 헬퍼 3개 추가, `Load` override 의 `base.Load` 구간을 억제 플래그로 감쌈
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `AddDatum` 의 `datum.DatumName = datumName;` 1줄을 `datum.InitializeDatumName(datumName);` 로 교체
- (repo 밖) `C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\6daecb8f-c376-47ac-89d1-018d55afefc3\scratchpad\nrm-verify\MoveHarness.cs` - 격리 검증용, 저장소에 추가하지 않음

## Decisions Made

1. **오버로드 정정 (발주 요구사항 #7):** 발주서는 1-arg 리졸버(`ResolveDatumModelPath(datum)`)를 지시했으나, 실제 코드를 읽어보니 1-arg 는 `OwnerName` 을 쓰지 않고 `SourceShotName` 으로 Shot 을 역추적하며 미매칭 시 **전역 `Shots[0]`** 으로 폴백한다(호출부 0건, 사실상 死코드). 반면 2-arg(`ResolveDatumModelPath(datum, ownerSeqName)`)는 미매칭 시 `ownerSeqName` 소유 Shot 으로 스코프를 좁히며, grep 결과 **실제 호출부 11곳 전부**와 티칭 시점 저장 경로(`MainView.xaml.cs:3661/3682`)가 이 2-arg 를 쓴다. 1-arg 를 썼다면 티칭이 실제로 쓴 폴더와 다른 경로를 계산해 "옛 파일 없음 → 조용히 skip" 으로 원래 버그를 그대로 재현했을 것이므로, **2-arg + `OwnerName`** 으로 구현했다.
2. **아키텍처 결정 결과 (발주 요구사항 #8):** `DatumConfig` → `InspectionSequence` 정적 메서드 직접 호출(같은 어셈블리, 같은 네임스페이스 `ReringProject.Sequence`)이 컴파일에 성공했다. 폴백안(정적 헬퍼를 `InspectionSequence.cs` 로 옮기고 `DatumConfig` 는 그것만 호출)은 불필요했다.
3. **발견된 추가 오발동 경로 2건(발주서에 없던 위험, 계획 단계 코드 조사로 명시화됨):**
   - **INI 로드 리플렉션** — `ParamBase.Load` 385~387행이 `case "String": prop.SetValue(this, sValue)` 로 `DatumName` 세터를 직접 때린다. 새 `DatumConfig` 인스턴스의 필드 초기값이 `"Datum_1"` 이므로, 억제 없이 로드하면 "Datum_1" → 저장된 실제 이름으로 리네임이 발동해 **1번 Datum 이 티칭해 둔 모델 파일을 훔쳐간다.** → `DatumConfig.Load` override 에서 `base.Load` 호출 구간만 `_suppressModelRename` 플래그로 감싸 차단.
   - **신규 Datum 추가(`AddDatum`)** — `InspectionSequence.cs:1746` 의 `datum.DatumName = datumName;` 도 동일 위험(새 객체 초기값 "Datum_1" → 지정 이름 변경이 개명으로 오인됨). → 세터 대신 `InitializeDatumName(name)`(리네임 로직 없이 필드만 대입 + PropertyChanged) 로 교체.
   - `DatumConfig.CopyTo` 는 사전 조사에서 이미 안전함이 확인됨(`_copyExclude` 에 `"DatumName"` 포함) — 이번에도 grep 으로 재확인만 하고 손대지 않았다.

## Deviations from Plan

None - 계획대로 정확히 실행됨(A/B/C 세 구간 모두 계획 문서의 코드 블록을 그대로 적용).

**빌드 검증 관련 참고사항(계획에서 이미 예견한 케이스):** `MSBuild.exe //t:Rebuild` 기본 산출물 경로(`bin\x64\Debug\DatumMeasurement.exe`)가 현재 실행 중인 Visual Studio/DatumMeasurement.exe 프로세스에 잠겨 있어 최종 복사 단계에서 `MSB3027`/`MSB3021` 오류가 발생했다. **계획의 지시대로 어떤 프로세스도 종료하지 않고**, `//p:OutputPath=` 스크래치 폴백으로 재빌드하여 컴파일 자체가 에러 0건으로 성공함을 확인했다(경고만 존재, 전부 이 변경과 무관한 기존 `[Obsolete]` 클래스 사용 경고).

## Known Residual Gaps (계획 범위 밖, 코드로 다루지 않음)

1. **`PatternEngine` 전환(Shape↔NCC) 후 개명** — 리졸버가 **현재 엔진**의 확장자만 계산하므로, "Shape 로 티칭 → NCC 로 전환 → 개명" 조합에서는 옛 `.shm` 파일이 남는다. 다른 트리거이므로 이번 수정 대상이 아니다.
2. **Datum 삭제 시 고아(`File.Delete` 미수행)** — 이번 범위 아님.

## Isolated Harness Results (Task 2B)

`MoveModelFileIfPresent` 본문을 그대로 복사한 콘솔 하네스(`csc.exe` 컴파일)로 4케이스 전부 자동 검증:

| # | 케이스 | 결과 |
|---|------|------|
| 1 | 옛 파일 존재, 새 경로 비어있음 | `MOVED_OK` — 새 파일 생성 + 옛 파일 삭제 확인 |
| 2 | 옛 파일 없음(티칭 전 Datum) | `SKIP_MISSING_OK` — 아무 파일도 생성 안 됨, 조용한 skip |
| 3 | 옛 파일 존재 + 새 경로에 이미 다른 파일 | `SKIP_COLLISION_OK` — 두 파일 모두 원본 내용 그대로 보존 |
| 4 | 옛 파일이 다른 핸들에 잠김(`FileShare.Read`, Delete 공유 없음) | `LOCKED_LOGGED_OK` — `File.Move` IOException 삼킴, 옛 파일 원본 보존 |

하네스 exit code 0, `_FAIL`/`HARNESS_FAILED` 미출력.

## Static Regression Verification Results (Task 2A)

| 항목 | 결과 |
|------|------|
| `.DatumName =` 직접 대입 0건(전체 코드베이스) | `NO_DIRECT_ASSIGN_OK` |
| `_copyExclude` 에 `"DatumName"` 여전히 존재 | `COPY_EXCLUDE_INTACT_OK` |
| `AddDatum` 이 `InitializeDatumName(datumName)` 사용 | `ADDDATUM_REWIRED_OK` |
| 리네임 호출이 2-arg 오버로드만 사용 | `TWO_ARG_OVERLOAD_OK` |
| `_suppressModelRename` 가드 4곳 이상 존재 | `SUPPRESS_GUARD_PRESENT_OK` |
| `DatumConfig` JSON 역직렬화 경로 없음 | `NO_JSON_DESERIALIZE_OK` |
| 신규 코드에 삼항/`??` 0건 | `TERNARY_OR_COALESCE_IN_NEW_CODE=0` |

## Issues Encountered

- `csc.exe` 를 절대경로 인자와 함께 직접 호출할 때 Git Bash(MSYS) 의 경로 자동변환이 `/out:`, `/nologo` 같은 단일 슬래시 스위치를 유닉스 경로로 오인해 깨졌다(예: `/nologo` → `C:/Program Files/Git/nologo`). 계획이 제시한 `//out:`/`//nologo` 이중 슬래시 형태는 MSBuild.exe 전용 이스케이프라 `csc.exe` 는 인식하지 못했다. **해결:** 하네스 디렉터리로 `cd` 한 뒤 상대경로 인자(`-out:MoveHarness.exe MoveHarness.cs`)로 컴파일해 우회 — 판정 로직/스위치 자체는 변경하지 않았고 셸 호출 방식만 조정했다.

## Next Phase Readiness

- Task 1(코드)·Task 2(정적검증+격리 하네스) 완료. Task 3(실기 PropertyGrid 개명 검증)은 사람 승인 필요 — 아래 "human-verify 지침" 참고.
- 승인 후 오케스트레이터가 `requirements.mark-complete TEACH-AUDIT-CO-01`, `state.advance-plan`, STATE.md/ROADMAP.md 갱신, 최종 docs 커밋을 처리한다.

---
*Plan: quick-260806-nrm*
*Task 1+2 completed: 2026-08-06*
*Task 3: PENDING human verification*
