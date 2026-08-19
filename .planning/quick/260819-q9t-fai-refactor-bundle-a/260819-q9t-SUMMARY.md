---
phase: quick-260819-q9t
plan: 01
status: complete
subsystem: inspection
tags: [csharp, refactor, dispose, halcon, dedup, dead-code]

requires:
  - phase: quick-260819-fik, quick-260819-gf1, quick-260819-hyk, quick-260819-j6j
    provides: "동일 파일 오늘자 Extract Method 리팩토링 결과물 — 이번 작업은 그 결과물 위에서 순수 기계적 중복제거만 수행"
provides:
  - "SafeDisposeImage(HImage) 헬퍼 신설 — Dispose try/catch 반복 14곳 통합"
  - "GetMeasurementDisplayName(MeasurementBase) 헬퍼 신설 — MeasurementName→TypeName 폴백 5곳 통합"
  - "null→\"\" 방어 15곳을 기존 관용구 X = X ?? \"\"; 로 통일"
  - "읽는 곳 0곳이던 미사용 필드 pCamera 완전 제거"
affects: [action-faimeasurement, cross-z-measurement]

tech-stack:
  added: []
  patterns:
    - "널 가드 헬퍼 추출 — 반복되는 if(X!=null){try{X.Dispose();}catch{}} / if(X==null)X=meas.TypeName 2줄 패턴을 private static 헬퍼로 통합, 삼항 연산자 미사용(if-else/?? 만 사용)"
    - "각 sed 정규식에 변수명 backreference(\\1/\\2) 를 걸어 오매치를 구조적으로 차단 — 플래너가 스크래치 git 저장소로 사전 실측한 값 그대로 재현"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "제외 대상(L752 szAlgoType 폴백 체인, BuildCrossZMeasurementKey 의 IsNullOrEmpty 검사, 무조건 초기화문 5곳)은 모양이 비슷해도 의미가 달라 plan 지시대로 절대 손대지 않음 — 정규식 자체가 이 5곳에 매치되지 않는 구조라 별도 예외처리 불필요했음(사전 확인으로 실증)"
  - "grep -c \\$'\\r' 이 이 Git Bash 환경에서 UTF-8 한글 파일에 대해 반복적으로 신뢰할 수 없는(허위양성 1748) 결과를 내 — xxd/perl 바이트 레벨 스캔(둘 다 CR=0 로 일치)을 대체 검증 수단으로 채택. 파일 자체의 CRLF 오염은 없음을 재확인"

requirements-completed: [Q9T-01, Q9T-02, Q9T-03, Q9T-04]

duration: 약 15분
completed: 2026-08-19
---

# Quick 260819-q9t: FAI 리팩토링 Bundle A (Dispose 헬퍼/MeasurementName 헬퍼/null 방어 통일/pCamera 제거) Summary

**`Action_FAIMeasurement.cs` 순수 기계적 중복제거 4건 — Dispose try/catch 14곳→`SafeDisposeImage` 헬퍼, MeasurementName 폴백 5곳→`GetMeasurementDisplayName` 헬퍼, null→"" 방어 15곳→기존 `?? ""` 관용구 통일, 미사용 `pCamera` 필드 제거. 3커밋 전부 numstat 플래너 사전실측값과 정확히 일치, 최종 clean Rebuild error0/warning12(baseline).**

## Performance

- **Duration:** 약 15분 (커밋 3건 2026-08-19T19:39~19:42 KST, 사전/사후 검증+빌드 포함)
- **Tasks:** 3/3 완료
- **Files modified:** 1

## Accomplishments

- `SafeDisposeImage(HImage)` 헬퍼 신설(null 가드+try/catch 내장) — 균일형 12곳 + 예외형 2곳(L829 `acc.CrossZRoleImage`, L1041 `capturedImage`) 전부 호출로 치환
- `GetMeasurementDisplayName(MeasurementBase)` 헬퍼 신설(`meas.MeasurementName ?? meas.TypeName`) — 5개 메서드(`RecordMeasurementResult`/`MarkMeasurementDatumSkipped`/`MarkMeasurementDatumRefMissing`/`MarkMeasurementZIndexMisconfigured`/`MarkMeasurementCrossZIncomplete`) 안 동일 2줄 폴백을 1줄 호출로 치환
- null→"" 방어 15곳(misName/datumName×4/dn×2/ae×2/derrStr×2/measErrorStr/datumRef×2/shotName)을 이 파일에 이미 존재하던 관용구 `X = X ?? "";` 로 통일 — 기존 6곳은 무변경
- 읽는 곳 0곳이던 `private VirtualCamera pCamera;` 필드 선언 + `OnLoad()` 안 대입문(가드 `if` 포함) 완전 삭제
- 제외 대상(L752 `szAlgoType` 폴백, `BuildCrossZMeasurementKey`의 `IsNullOrEmpty` 검사, 무조건 초기화문 5곳) 전부 무변경 확인
- 3개 Task 모두 `Action_FAIMeasurement.cs` 단 1개 파일만 커밋, `DatumMeasurement.csproj`(로컬 미커밋 오염)는 3커밋 전체에서 `git status` 상 unstaged `M` 유지 확인

## Task Commits

Each task was committed atomically:

1. **Task 1: SafeDisposeImage 헬퍼 신설 + Dispose try/catch 14곳 치환** - `327916a` (refactor)
2. **Task 2: GetMeasurementDisplayName 헬퍼 신설 + MeasurementName 폴백 5곳 치환 + pCamera 제거** - `6eba987` (refactor)
3. **Task 3: null→"" 방어 15곳을 기존 `?? ""` 스타일로 통일** - `6b8f35b` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - 헬퍼 2개 신설 + Dispose 14곳/MeasurementName 5곳 헬퍼 통합 + null→"" 15곳 스타일 통일 + pCamera 필드 제거. 1742줄 → 1744줄(순증가 +2)

## Verification Results (Task별 numstat + 빌드, 실측)

| Task | 커밋 | numstat (add/del) | 사전실측 기대값 | 일치 | 빌드 (error/warning) |
|---|---|---|---|---|---|
| 1 | `327916a` | 20 / 14 | 20 / 14 | PASS | 0 / 12 (baseline) |
| 2 | `6eba987` | 10 / 14 | 10 / 14 | PASS | 0 / 12 (baseline) |
| 3 | `6b8f35b` | 15 / 15 | 15 / 15 | PASS | 0 / 12 (baseline, 최종 `-t:Rebuild` clean 빌드) |
| **누적** | `4a15a12`→`6b8f35b` | **45 / 43** | **45 / 43** | **PASS** | warning CS 분해: CS0618×10 + CS0162×2 (baseline 그대로) |

최종 파일 라인수 `wc -l` = **1744**(1742+2, 사전실측 예상값과 정확히 일치). `git diff --name-only 4a15a12 HEAD` = `Action_FAIMeasurement.cs` 1개 파일뿐.

### Task별 카운트 검증(자기참조 오염 방지 포함)

- Task 1 종료: `SafeDisposeImage(` 리터럴 = **15**(선언1+호출14, PASS) / `.Dispose(); } catch { }` 잔존 = **1**(헬퍼 본문 자신만, PASS) / 가드형 인라인 잔존 = **1**(L523 `capSaver`/`SharedHImage`, Dispose 와 무관한 기존 코드 — plan 명시대로 정상)
- Task 2 종료: `GetMeasurementDisplayName(` 리터럴 = **6**(선언1+호출5, PASS) / 구형 폴백(`if (measName == null) measName = meas.TypeName;`) 잔존 = **0**(PASS) / `pCamera` 잔존 = **0**(PASS) / 제외 2곳(`meas.MeasurementName ?? szAlgoType`, `string.IsNullOrEmpty(measName)`) 각 count=1 무변경 확인
- Task 3 종료: 구형 null 체크 패턴 잔존 = **0**(PASS) / `?? ""` 총 카운트 = **21**(기존6+신규15, PASS) / 제외 5곳(`dNameForLog`/`dName`/`datumName`/`shotName` 무조건 초기화문, `else ownerSeqName = "";`) 각 count=1 무변경 확인

### 삼항 연산자 / 시그니처 무변경 확인

3커밋 누적 `git diff 4a15a12 HEAD -- Action_FAIMeasurement.cs` 의 추가된 줄 전체를 육안 검사 — 신규 코드에 삼항 `?:` **0건**(전부 if-else 또는 이 파일 기존 관용구인 `??` null-coalescing만 사용). public/internal 메서드 시그니처 변경 **0건**, 판정 로직·분기·실행 순서 변경 **0건**(순수 추출/치환/삭제만).

### 인코딩 보존

매 Task 편집 직후 확인:
- UTF-8 BOM(`ef bb bf`) 유지 — 3회 전부 PASS
- CRLF 오염 0건 — `xxd`/`perl -ne 'print if /\r/'` 바이트 레벨 스캔으로 3회 전부 CR=0 확인(아래 Issues Encountered 참고: `grep -c $'\r'` 자체가 이 환경에서 신뢰 불가로 판명되어 대체 수단 사용)
- L1041 꼬리주석(`Store 가 CopyImage 로 소유 클론 저장 — 원본은 여기서 즉시 해제`) 한글 원문 그대로 보존 확인

### 커밋 위생

매 Task 커밋 전 `git diff --cached --name-only` = 대상 파일 1줄만 출력 확인 후 커밋. 매 커밋 후 `git status --short` 로 `WPF_Example/DatumMeasurement.csproj` 가 계속 ` M`(unstaged) 상태인지 확인 — 3회 전부 PASS, 한 번도 스테이징되지 않음. 매 커밋 후 `git diff --diff-filter=D --name-only HEAD~1 HEAD` = 빈 출력(의도치 않은 삭제 파일 0건) 확인.

## Decisions Made

- 제외 대상 5곳(Q9T-02의 2곳, Q9T-03의 5곳)은 plan 이 지정한 정규식이 애초에 매치하지 않는 구조라 별도 예외 처리 없이 그대로 통과 — 사전 grep 재확인으로 실증
- `grep -c $'\r'`가 이 세션의 Git Bash 환경에서 이 UTF-8 한글 파일에 대해 반복적으로 허위양성(1748, 실제로는 CR 0건)을 내는 것을 발견 — `xxd`/`perl` 바이트 레벨 스캔을 대체 검증 수단으로 채택(둘 다 일관되게 CR=0). `git diff`가 출력한 `LF will be replaced by CRLF the next time Git touches it` 경고는 이 저장소의 전역 `core.autocrlf=true` 설정과 `HEAD`에 이미 LF로 커밋되어 있던 이 파일의 조합에서 나오는 기존(pre-existing) 조건임을 `git show HEAD:...`로 재확인 — 이번 작업으로 새로 생긴 문제 아님, scope 밖

## Deviations from Plan

None - plan 이 제공한 old_string/new_string 및 sed 정규식을 Edit 도구/Bash 그대로 사용, 전부 1회 매치로 성공. 카운트·numstat·빌드 결과가 plan의 사전실측값과 전부 정확히 일치하여 추가 판단이나 예외 처리가 필요한 지점이 없었음. 유일한 발견 사항은 코드가 아닌 검증 스크립트 신뢰성 문제(`grep -c $'\r'`, 위 Decisions 참조)이며 대체 수단으로 즉시 해결.

## Issues Encountered

**`grep -c $'\r'` 신뢰 불가(검증 스크립트 이슈, 코드 문제 아님):** Task 1 편집 직후 plan 명시 명령 `grep -c $'\r' "$F"` 를 실행하니 1748(거의 전체 줄) 이 반환되어 대량 CRLF 오염처럼 보였음. 즉시 `od`/`xxd` 바이트 레벨 스캔(`0d` 바이트 개수 직접 카운트)과 `perl -ne 'print if /\r/'` 로 교차검증한 결과 둘 다 일관되게 **0** — 실제 파일에는 CR 바이트가 전혀 없음을 확인. 여러 차례 재실행해도 `grep -c $'\r'`만 1748/0 을 오락가락해 이 특정 Git Bash 빌드의 grep 이 대용량 UTF-8(한글 멀티바이트) 파일에서 `$'\r'` 단일 제어문자 패턴 매칭 시 불안정한 것으로 결론. 이후 Task 2/3 에서는 `xxd`/바이트 스캔만 사용해 매번 CR=0 확인(위 Verification Results 참조). 파일 손상은 실제로 발생하지 않았음 — 이 세션의 "BOM/LF 손상 시 즉시 중단" 규칙에 따라 검증을 멈추지 않고 대체 수단으로 안전을 확인한 후 계속 진행.

## User Setup Required

None - 외부 서비스 설정 불필요. 정적 검증(카운트+numstat+빌드)만으로 회귀 0 결론 — 순수 텍스트 치환/추출이라 판정 로직 접근 없음. plan 이 권고한 실기 UAT(Shot 1개 검사 후 로그 비교)는 선택사항이며 이번 세션에서는 미실행.

## Next Phase Readiness

- `Action_FAIMeasurement.cs` 반복 코드 4종 정리 완료, 후속 코드 작업 없음
- 오늘 백로그 "우선순위 2" 중 리스크가 더 높은 항목들(구조 변경 필요)은 이번 Bundle A 범위 밖 — 별도 quick-task 또는 phase 로 이어갈 것
- Blockers 없음

## Known Stubs

없음 - 순수 리팩토링(추출/치환/삭제)이며 신규 데이터 소스/바인딩/UI 변경 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. Dispose 헬퍼는 기존과 동일하게 예외를 삼키는 방어적 정리 로직이고, MeasurementName 폴백/null 방어 통일도 표시 문자열 처리 범위 내 순수 리팩토링. pCamera 제거는 읽는 곳이 원래 0곳이던 죽은 필드 삭제.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
FOUND: .planning/quick/260819-q9t-fai-refactor-bundle-a/260819-q9t-SUMMARY.md
```

커밋 존재 확인:
```
FOUND: 327916a (Task 1)
FOUND: 6eba987 (Task 2)
FOUND: 6b8f35b (Task 3)
```

---
*Phase: quick-260819-q9t*
*Completed: 2026-08-19*
