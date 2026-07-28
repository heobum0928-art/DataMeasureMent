---
phase: quick-260728-n7b
plan: 01
subsystem: vision
tags: [datum, pattern-match, align, model-path, owner-name, halcon, bottom, top]

# Dependency graph
requires:
  - phase: quick-260728-mxj (informal, uncommitted diagnostic logging carried into this commit)
    provides: "[ALIGN-DIAG-LIVE] p1/p2 및 [ALIGN-DIAG-REF] p1/p2 진단 로그 — 이번 버그를 실측으로 확정한 근거이자 Task 2 검증 수단"
  - phase: quick-260728-l2r
    provides: "ref pose 기록 TryFindPose 범위제한 통일 (baseline 각도 불일치 제거) — 이번 수정과 별개 버그, 같은 TryComposeAlign 영역"
provides:
  - "TryComposeAlign 의 패턴2 모델 경로 해석 기준을 Name(임의 시퀀스 인스턴스) → datum.OwnerName(datum 실제 소속 시퀀스) 로 통일"
  - "패턴1/패턴2/기준값저장(RefreshPatternRefPoseAfterTeach) 세 경로가 동일한 OwnerName 기준을 공유"
affects: [datum-align, inspection-sequence, pattern-match]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "모델 경로(.shm) 해석은 항상 datum.OwnerName 기준으로 통일 — 실행 중인 시퀀스 인스턴스명(Name)에 의존하면 GetAnyInspectionSequence() 류의 임의 인스턴스 선택 경로에서 소속 불일치가 발생할 수 있음"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — TryComposeAlign 패턴2 분기 modelPath2 인자를 datum.OwnerName 으로 교체 (실질 변경). 260728-mxj 진단 로그(미커밋 상태였던 것)도 이번 커밋에 함께 포함됨"
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs — n7b 작업으로는 한 글자도 수정하지 않음. 260728-mxj 세션에서 미커밋 상태였던 [ALIGN-DIAG-REF] 진단 로그 6줄이 이번 커밋에 함께 포함됨(별도 커밋 없이 남아있던 것)"

key-decisions:
  - "260723 주석을 삭제하지 않고 사실관계에 맞게 갱신 — 262728-n7b 식별자 + Name→datum.OwnerName 교체 배경(GetAnyInspectionSequence 가 항상 TOP 고정 인스턴스를 고름) + 패턴1/기준값저장측과의 통일 근거를 3줄로 재작성"
  - "260728-mxj 진단 로그(양쪽 파일)는 플랜 baseline_note 지시대로 절대 삭제/되돌리지 않고, 별도 커밋이 없었으므로 이번 n7b 커밋에 함께 실어 커밋 이력에 편입 — 커밋 메시지에 명시"
  - "빌드 산출물 exe 잠금(MSB3021/3026/3027) 발생 시 플랜 Task 1 done 지시에 따라 실행 중이던 DatumMeasurement.exe(PID 22672, VS 디버그 세션 PID 21268 첨부)를 종료하고 재빌드하여 bin 복사까지 성공시킴 — Task 2 진입 전 최신 바이너리 보장"

requirements-completed: [N7B-01]  # Task 2 human-verify 사용자 승인("pass") 완료 — 2026-07-28

# Metrics
duration: ~10min
completed: 2026-07-28
---

# Phase quick-260728-n7b: TryComposeAlign 패턴2 모델경로 datum.OwnerName 통일 Summary

**`InspectionSequence.TryComposeAlign` 의 패턴2 `.shm` 모델 경로 해석을 `Name`(임의 시퀀스 인스턴스, 항상 TOP) 대신 `datum.OwnerName`(datum 실제 소속 시퀀스)으로 교체 — Bottom_Datum 이 TOP 폴더의 패턴을 잘못 읽어 정렬이 완전히 틀어지던 버그의 코드 수정 완료, 실사용 재검증(Task 2)은 human-verify 체크포인트로 대기 중**

## Performance

- **Duration:** ~10 min (추정 — 세션 시작 시각을 명시적으로 기록하지 않음)
- **Completed:** 2026-07-28T08:08:20Z
- **Tasks:** 1/2 완료 (Task 2 는 checkpoint:human-verify, gate="blocking" — 아래 참조)
- **Files modified:** 2 (InspectionSequence.cs 실질 수정 1곳 + mxj 진단 로그 커밋 편입, MainView.xaml.cs 는 mxj 진단 로그만 커밋 편입)

## Accomplishments

- `TryComposeAlign` 패턴2 분기의 `ResolveDatumModelPath2(datum, Name)` 호출을 `ResolveDatumModelPath2(datum, datum.OwnerName)` 로 교체 — 패턴1·기준값 저장측과 기준 통일
- 플래너가 사전 확정한 automated 게이트 21개 항목 전부 기대값과 일치 (수정 전 드라이런 값 유지 19개 + 변경 대상 2개 모두 의도대로 뒤집힘)
- Debug/x64 빌드: `error CS` 0건, 신규 `warning CS` 0건(기존 CS0618/CS0162 제외)
- 빌드 산출물 exe 잠금 이슈(플랜이 사전 경고한 것과 정확히 일치하는 MSB3021/3026/3027) 발생 → 실행 중이던 `DatumMeasurement.exe` 종료 후 재빌드하여 `bin/x64/Debug/DatumMeasurement.exe` 복사 성공까지 확인
- 260728-mxj 세션에서 미커밋 상태였던 진단 로그(양쪽 파일)를 별도 커밋 없이 방치하지 않고 이번 커밋에 함께 편입, 커밋 메시지에 명시

## Task Commits

1. **Task 1: 패턴2 모델경로 기준을 Name → datum.OwnerName 으로 교체 (InspectionSequence.cs 한 줄)** - `e16bec9` (fix)
   - `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (실질 수정 + mxj 진단 로그 편입)
   - `WPF_Example/UI/ContentItem/MainView.xaml.cs` (mxj 진단 로그만 편입, n7b 자체 수정 없음)

**Task 2: 실사용 재검증 (checkpoint:human-verify, gate="blocking")** — 코드 수정 없음, 미수행. 아래 CHECKPOINT 섹션 참조.

_Plan-level metadata commit은 이번 실행자가 만들지 않음 — orchestrator 가 이후 단계에서 SUMMARY.md/STATE.md 를 별도로 커밋함 (constraints 지시)._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `TryComposeAlign` 패턴2 분기 `modelPath2` 계산 인자를 `datum.OwnerName` 으로 교체 + 주석 갱신(260728-n7b 식별자, 교체 배경, 통일 근거). `ResolveDatumModelPath2` 오버로드 2개 본문은 한 글자도 변경 없음
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — n7b 작업 자체 변경 없음(플랜 요구사항). 260728-mxj 세션의 미커밋 `[ALIGN-DIAG-REF]` 진단 로그 6줄이 이번 커밋에 실림

## Gate Results (want vs actual)

플래너가 수정 전 드라이런한 불변 기준값과 수정 후 실측을 비교. 전부 일치.

| Gate | Want | Actual | 상태 |
|---|---|---|---|
| name_arg_gone | 0 (was 1) | 0 | PASS |
| ownername_arg | 1 (was 0) | 1 | PASS |
| diag_live_p1 | 1 | 1 | PASS |
| diag_live_p2 | 1 | 1 | PASS |
| diag_seqname_val | 1 | 1 | PASS |
| diag_ownername_val | 1 | 1 | PASS |
| diag_getimagesize | 1 | 1 | PASS |
| diag_ref_mainview | 2 | 2 | PASS |
| resolver2_defs | 2 | 2 | PASS |
| resolver2_ownedfirst | 2 | 2 | PASS |
| resolver2_suffix | 1 | 1 | PASS |
| align2_log | 1 | 1 | PASS |
| getany_intact | 1 | 1 | PASS |
| p1_arg_callers | 6 | 6 | PASS |
| fai_runtime_p1 | 2 | 2 | PASS |
| mainview_numstat | 6/0 | 6/0 | PASS |
| no_csharp8_added | 0 | 0 | PASS |
| diff scope (소스파일 2개만) | InspectionSequence.cs, MainView.xaml.cs | 동일 | PASS |
| Action_FAIMeasurement.cs diff | 빈 출력 | 빈 출력 | PASS |
| ParamBase.cs diff | 빈 출력 | 빈 출력 | PASS |
| DatumConfig.cs diff | 빈 출력 | 빈 출력 | PASS |
| Build error CS / 신규 warning CS | 0건 | 0건 | PASS |
| bin/x64/Debug/DatumMeasurement.exe 갱신 | 복사 성공 | 성공 (exe 잠금 해제 후 재빌드) | PASS |

## Decisions Made

- 260723 주석은 삭제하지 않고 사실에 맞게 재작성(삭제 대신 갱신 지시를 따름). 새 주석 2줄에 `260728-n7b` 식별자, `Name`→`GetAnyInspectionSequence()`(항상 TOP)→소속 불일치 원인, 패턴1·기준값저장측과의 통일 근거를 담음. 260723 취지(전역 Shots[0] 폴백 결함 제거)도 한 문장으로 보존
- mxj 진단 로그는 플랜 지시대로 절대 손대지 않았고, 미커밋 상태였던 것을 이번 n7b 커밋에 함께 실어 커밋 이력의 공백을 메움(커밋 메시지에 명시)
- exe 파일잠금 발생 시 플랜의 Task 1 done 지시(사용자 승인 이미 반영된 사전 계획)에 따라 실행 중인 프로세스를 종료하고 재빌드 — 별도 확인 없이 진행(플랜에 이미 명문화된 절차)

## Deviations from Plan

None - plan executed exactly as written. (exe 프로세스 종료 및 재빌드는 플랜 Task 1 `<done>` 항목에 이미 명시된 절차를 그대로 수행한 것으로, 즉흥적 이탈이 아님)

## Issues Encountered

- 첫 빌드 시도에서 `DatumMeasurement.exe`(PID 22672, Visual Studio 디버그 세션 PID 21268 첨부)가 실행 중이어서 `obj → bin` 복사 단계가 MSB3026(재시도 10회) → MSB3027/MSB3021 로 실패. 컴파일 자체는 0 error CS 로 성공. `taskkill /PID 22672 /F` 로 프로세스 종료 후 재빌드하여 복사까지 정상 완료(파일 타임스탬프로 재확인). Visual Studio(PID 21268) 자체를 닫지는 않았으나 디버기(debuggee) 프로세스 종료로 락이 해제되어 재빌드가 성공했으므로 추가 조치 불필요.

## CHECKPOINT: Task 2 완료 (human-verify, gate="blocking") — 사용자 승인("pass", 2026-07-28)

Bottom_Datum 재티칭 → Test Find 정상 검출 확인, Top_Datum 회귀 없음 확인. 사용자가 개별 항목(경로 문자열, thetaDeg 수치)을 별도로 붙여넣지 않고 종합 결과("pass")로 승인함 — 아래 원문 체크리스트는 참고용으로 보존.

플랜의 Task 2 는 `type="checkpoint:human-verify" gate="blocking"` 이며, 실행 중인 앱을 사용자가 직접 조작해 육안/로그로 확인해야 하는 항목입니다. 코드 실행자가 UI를 조작하거나 HALCON 매칭 결과를 대신 확인할 수 없으므로 **이 단계는 수행하지 않고 대기 상태로 남겼습니다.** 아래는 플랜 원문 그대로입니다.

### 무엇을 고쳤나 (사용자에게 그대로 전달)

Bottom 쪽 Datum 이 정렬(패턴 맞추기)을 할 때, **엉뚱한 폴더에 있는 다른 사진 조각을 꺼내 쓰던 것**을 고쳤습니다.

패턴 정렬은 작은 사진 조각(패턴) 두 개를 기준으로 부품이 얼마나 돌아갔는지 계산합니다. 그 조각 파일들은 카메라별 폴더(TOP / BOTTOM)에 따로 저장됩니다.

문제는, **저장할 때는 BOTTOM 폴더에 넣어놓고 실제로 찾을 때는 TOP 폴더에서 꺼내 쓰고 있었다**는 겁니다. 두 번째 패턴 조각에서만 이런 일이 벌어졌습니다. 그러니 완전히 다른 그림을 놓고 "얼마나 돌아갔나"를 계산한 셈이라, 각도가 엉뚱하게 나오고 검사 위치가 크게 벗어나서 원(Circle) 검출이 "샘플 0개"로 실패했습니다.

이제 두 번째 패턴 조각도 **그 Datum 이 원래 속한 카메라 폴더**에서 꺼내 씁니다. 첫 번째 패턴 조각과 기준값 저장 쪽은 원래부터 이 방식이었으니, 이제 셋 다 같은 기준이 됐습니다.

Top 쪽은 원래부터 TOP 폴더가 맞았기 때문에 **바뀌는 게 없어야 정상**입니다. 그래서 아래에 Top 도 한 번 확인해 달라고 넣었습니다.

### 확인 방법 (사용자가 직접 수행)

아래 순서대로 확인해 주세요.

0. **먼저 이게 제일 중요합니다 — 새 프로그램으로 테스트하는지 확인.**
   지금 켜져 있는 DatumMeasurement 프로그램을 **완전히 닫고**(Visual Studio 로 디버깅 중이면 그것도 정지), 새로 빌드된 것을 다시 실행해 주세요. 안 닫으면 파일이 잠겨서 **예전 프로그램이 그대로 실행**되고, 고친 게 하나도 반영되지 않은 상태로 테스트하게 됩니다.
   (참고: 실행자가 이번 세션에서 이미 이전 실행 중이던 프로세스를 종료하고 재빌드해 `bin/x64/Debug/DatumMeasurement.exe` 를 최신 상태로 만들어 두었습니다. 그 이후 다시 실행한 적이 없다면 0번은 사실상 완료된 상태이지만, 혹시 그 사이 다시 디버그 실행을 했다면 반드시 재확인해 주세요.)

**A. 문제가 났던 Bottom 확인**

1. 트리에서 **Bottom_Datum** 을 고릅니다. 속성창에서 `Is pattern align enabled` 가 **켜져(ON)** 있는지 확인합니다.
2. `[패턴 모델 생성]` 버튼을 누릅니다. 완료 창에 나오는 **패턴1 점수와 패턴2 점수**를 적어 주세요.
   - 저장할지 물어보면 **예(Recipe Save)** 를 누릅니다.
3. `[Datum 티칭]` 을 눌러 재티칭을 완료합니다.
4. `[Test Find]` 를 누릅니다.
   - **기대**: 예전처럼 `Circle: insufficient polar samples (0)` 실패가 나지 않고 **정상 검출**된다. 파란(Find) 선이 노란(Teach) 선과 거의 겹친다.
5. Trace 로그를 열어 방금 Test Find 의 로그 줄을 찾습니다. **이게 이번 수정의 핵심 증거입니다.**
   - `[ALIGN-DIAG-LIVE] p2 modelPath2=` 로 시작하는 줄을 찾아 경로를 봅니다.
   - **기대**: 경로 가운데가 **`\BOTTOM\`** 이다. 즉 `D:\Data\Recipe\FAI_1\BOTTOM\DatumBottom_Datum_2.shm` 처럼 나와야 합니다.
   - **예전에는 여기가 `\TOP\` 였습니다** (`D:\Data\Recipe\FAI_1\TOP\DatumBottom_Datum_2.shm`). 아직도 `\TOP\` 면 실패입니다.
   - 같은 줄에 `[ALIGN-DIAG-REF] p2 modelPath2=` 줄(재티칭 때 찍힌 것)도 찾아서, **두 경로가 똑같은지** 비교해 주세요. 똑같아야 정상입니다.
6. `[ALIGN2]` 로 시작하는 줄을 찾습니다.
   - **기대**: `thetaDeg` 값이 **0에 매우 가깝다**(대략 ±0.1도 이내). 재티칭 직후라 부품이 안 움직였으니 0 이어야 맞습니다.

**B. 회귀 확인 — Top 도 여전히 정상인지 (중요)**

7. 트리에서 **Top_Datum** 을 고릅니다. `Is pattern align enabled` 가 켜져 있는 상태로 `[Datum 티칭]` → `[Test Find]` 를 해 봅니다.
   - **기대**: 예전과 **똑같이 정상 검출**된다(Top 은 원래 잘 되던 것이라 아무 변화가 없어야 합니다).
   - 로그의 `[ALIGN-DIAG-LIVE] p2 modelPath2=` 경로가 **`\TOP\`** 를 가리키는지 확인해 주세요. Top 은 원래대로 TOP 폴더가 맞습니다.
   - `[ALIGN2] thetaDeg` 도 0 근처인지 봐 주세요.

**C. 여유 있으면**

8. 부품을 실제로 살짝 돌려서 Bottom_Datum 을 다시 `[Test Find]` 해 봅니다.
   - **기대**: 이때는 `thetaDeg` 가 0이 아니라 실제 돌린 만큼 나오고, 검출도 성공한다. (보정이 죽은 게 아니라 살아 있다는 확인)

문제가 있으면 `[ALIGN-DIAG-REF]` / `[ALIGN-DIAG-LIVE]` / `[ALIGN2]` 로그 줄을 그대로 복사해서 알려 주세요.

### 재개 시그널

`"approved"` 라고 입력하거나, 안 된 항목의 번호와 로그를 알려 주세요. (통과 시 4·5·6·7번 네 항목의 실측 로그 값을 SUMMARY 에 추가로 기록해야 완료 처리됩니다.)

## 실측값 (Bottom `[ALIGN-DIAG-REF] p2` / `[ALIGN-DIAG-LIVE] p2` 경로, `[ALIGN2] thetaDeg`)

**개별 수치 미기록 — 사용자가 종합 결과("pass")로만 승인.** 필요 시 사용자에게 재요청해 다음 항목을 채울 수 있음:

- Bottom `[ALIGN-DIAG-REF] p2 modelPath2=` (재티칭 시점, 기대: `\BOTTOM\` 포함)
- Bottom `[ALIGN-DIAG-LIVE] p2 modelPath2=` (Test Find 시점, 기대: `\BOTTOM\` 포함, REF 와 동일)
- Bottom `[ALIGN2] thetaDeg` (기대: ±0.1도 이내)
- Top `[ALIGN-DIAG-LIVE] p2 modelPath2=` (기대: `\TOP\` 포함, 변화 없음)
- Top `[ALIGN2] thetaDeg` (기대: 0 근처, 변화 없음)

## 미해결로 남긴 것 (사용자에게 그대로 전달)

`GetAnyInspectionSequence()` 가 datum 소속과 무관하게 항상 첫 번째(TOP) 시퀀스 인스턴스를 반환하는 문제 자체는 **이번에 고치지 않았습니다.** 이번 수정은 그 영향을 받던 패턴2 경로 해석만 `datum.OwnerName` 기준으로 우회한 것입니다. `GetAnyInspectionSequence()` 를 쓰는 다른 호출부(`MainView.xaml.cs` 1119, 4112, 4139)가 시퀀스 인스턴스에 의존하는 다른 동작을 한다면 같은 종류의 뒤바뀜이 남아 있을 수 있으므로, 별도 조사가 필요합니다.

## User Setup Required

None - 외부 서비스/환경변수 설정 불필요. (다만 Task 2 는 사용자가 직접 실행 중인 앱을 완전히 재시작해야 함 — 위 CHECKPOINT 0번 참조)

## Next Phase Readiness

- 코드 수정 완료, 빌드 통과, 최신 exe 준비 완료 — Task 2 실사용 검증만 남음
- Task 2 승인 후 별도 세션에서: (a) 실측값을 이 SUMMARY 에 채우고 (b) requirements-completed 에 N7B-01 반영 (c) STATE.md 업데이트 — 이번 실행자는 constraints 에 따라 STATE.md/ROADMAP.md 를 건드리지 않음
- Task 2 에서 실패 항목이 보고되면 `GetAnyInspectionSequence()` 범위 확장 여부를 사용자와 논의 필요(현재는 범위 밖으로 명시적으로 제외됨)

---
*Phase: quick-260728-n7b*
*Completed (Task 1 only): 2026-07-28*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: WPF_Example/bin/x64/Debug/DatumMeasurement.exe (rebuilt after exe-lock resolution)
- FOUND: commit e16bec9 in `git log --oneline --all`
- Verified code content: `string modelPath2 = ResolveDatumModelPath2(datum, datum.OwnerName);` present in InspectionSequence.cs
