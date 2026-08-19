---
phase: quick-260819-tcs
plan: 01
status: complete
subsystem: inspection
tags: [csharp, refactor, log-tag-constant, documentation-only]

requires:
  - phase: quick-260819-sxj
    provides: "동일 파일 오늘자 Bundle E(IsZIndexMisconfigured/IsDatumZIndexMisconfigured→IsCrossZIndexPairMisconfigured) 결과물 위에서 진행"
provides:
  - "LOG_TAG 상수 신설(`private const string LOG_TAG = \"[FAIMeasurement] \";`) — Logging.PrintLog 호출 17곳의 \"[FAIMeasurement] \" 리터럴 중복 제거, 런타임 출력 문자열 무변경"
  - "ECrossZGate enum 선언 위 문서주석 4줄 추가 — enum이 완전한 상태표가 아니라 상위 분류일 뿐임을 명시(로직 무변경)"
  - "IsCrossZDatumBothStored/TryReDetectCrossZDatumFromStore 인라인 미실행 결정(조사 완료, 코드 무변경) — 오늘 백로그 P3 항목 처리 완료 기록"
affects: [action-faimeasurement]

tech-stack:
  added: []
  patterns:
    - "반복되는 로그 태그 리터럴은 값(태그+공백)을 그대로 담은 private const로 추출하고 호출부는 LOG_TAG + \"...\" 로 이어붙여 런타임 문자열을 1글자도 바꾸지 않는 순수 표기 치환"
    - "인라인/enum 확장처럼 위험 대비 이득이 낮은 리팩토링 후보는 조사 후 '변경하지 않는다'는 판단 자체를 커밋 없이 문서로만 남기는 것도 유효한 완료 형태"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "TCS-01: 태그 뒤 공백을 상수 값 안에 포함(`\"[FAIMeasurement] \"`) — 호출부마다 공백을 손으로 입력할 필요를 없애 17곳 중 하나라도 공백 누락/중복되는 사고를 원천 차단"
  - "TCS-02: IsCrossZDatumBothStored/TryReDetectCrossZDatumFromStore 인라인 — 하지 않기로 결정, 코드 무변경(아래 Decisions Made 상세 참고)"
  - "TCS-03: ECrossZGate enum 확장 대신 문서주석 4줄만 추가 — switch(eGate) 재검증 위험을 오늘 세 번째로 또 감수하지 않음(아래 Decisions Made 상세 참고)"

requirements-completed: [TCS-01, TCS-02, TCS-03]

duration: 약 20분
completed: 2026-08-19
---

# Quick 260819-tcs: FAI 리팩토링 Bundle F (LOG_TAG 상수화 + ECrossZGate 문서주석 + P3 nit 3항목 마무리) Summary

**`Action_FAIMeasurement.cs`(오늘 9차례 리팩토링 완료 상태 위) 사용자 원 백로그 "우선순위 3 (nit) — 선택" 3항목을 처리하는 오늘의 마지막 bundle. 유일한 실제 코드 변경은 `private const string LOG_TAG = "[FAIMeasurement] ";` 신설과 `Logging.PrintLog` 호출 17곳의 `"[FAIMeasurement] "` 리터럴을 `LOG_TAG + "..."` 로 치환한 것(런타임 출력 문자열 1글자도 무변경, 표본 3곳 수기 대조 완료). `ECrossZGate` enum 선언 위에는 로직 무변경 문서주석 4줄만 추가했다. `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 인라인은 조사 후 미실행으로 결정(코드 무변경). 파일 1775줄→1781줄(+6), `git diff --numstat` add=23/del=17, clean Rebuild error0/warning12(baseline CS0618×10+CS0162×2) 확인, 커밋 `c7fbecd` 1개. 이로써 오늘 6-bundle 리팩토링 백로그(q9t/rle/s05/sgg/sxj/tcs) 전부 완료 — 사용자 원 요청 우선순위 2+3 백로그 전체가 마무리됐다.**

## Performance

- **Duration:** 약 20분 (커밋 1건, 정적 검증 6단계 + 빌드 포함)
- **Started:** 2026-08-19T12:00:00Z
- **Completed:** 2026-08-19T12:22:00Z
- **Tasks:** 1/1 완료
- **Files modified:** 1

## Accomplishments

- `private const string LOG_TAG = "[FAIMeasurement] ";` 신설 — 기존 `private const` 클러스터(`UNSET_ZINDEX`/`CROSS_Z_ROLE_SUFFIX_A/B`/`CROSS_Z_DATUM_KEY_PREFIX`) 바로 다음에 삽입, 이 구역 기존 스타일(들여쓰기 8칸) 그대로
- `Logging.PrintLog` 호출 17곳 전부 `sed`(순수 ASCII 기계적 치환)로 `LOG_TAG + "..."` 형태로 치환 — 신규 한글 타이핑 없음, 값(태그+공백)은 상수 안에만 존재하므로 호출부는 공백 없이 바로 이어붙여도 원본과 동일한 문자열 재구성
- `ECrossZGate` enum(L46 부근) 선언 바로 위에 문서주석 4줄 추가 — `NotMyTick`/`HalfPending` 내부 `bNonProtocolCycle` 이중분기 때문에 이 enum이 완전한 상태표가 아니라 상위 분류일 뿐임을 명시. `switch (eGate)` 블록 본문·`bNonProtocolCycle` 선언/사용은 1바이트도 건드리지 않음(byte-identical diff로 확인)
- `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` — 조사 후 인라인 미실행 결정, 이 두 함수는 이번 플랜에서 1바이트도 변경되지 않음(byte-identical diff로 확인)
- 신규 코드 삼항 `?:` 0건, C# 7.2, 파일 인코딩 손상 0건(UTF-8 BOM 유지, LF 유지, CRLF 오염 0건 — python3 바이트 카운트로 확인)

## Task Commits

Each task was committed atomically:

1. **Task 1: LOG_TAG 상수화(17곳) + ECrossZGate 문서주석 추가** - `c7fbecd` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `LOG_TAG` 상수 신설(+2줄: 안내주석1+선언1), `ECrossZGate` 문서주석 추가(+4줄), 17곳 `sed` 치환(줄수 무변화, 내용만 치환). 1775줄 → 1781줄(+6)

## 17곳 중 표본 3곳 before/after 텍스트 대조

| # | 원본(치환 전, base L273/742/1772) | 치환 후(LOG_TAG 참조) | 재구성된 최종 문자열 |
|---|---|---|---|
| 1 (Datum) | `"[FAIMeasurement] Datum '" + misName + "' ZIndexA=" + ...` | `LOG_TAG + "Datum '" + misName + "' ZIndexA=" + ...` | `[FAIMeasurement] Datum '<misName>' ZIndexA=...`(동일) |
| 2 (Measurement) | `"[FAIMeasurement] Measurement '" + measName + "' failed: " + measErrorStr` | `LOG_TAG + "Measurement '" + measName + "' failed: " + measErrorStr` | `[FAIMeasurement] Measurement '<measName>' failed: <err>`(동일) |
| 3 (SHOT) | `"[FAIMeasurement] SHOT '" + shotName + "' 검사 이미지 없음 — ..."` | `LOG_TAG + "SHOT '" + shotName + "' 검사 이미지 없음 — ..."` | `[FAIMeasurement] SHOT '<shotName>' 검사 이미지 없음 — ...`(동일) |

`LOG_TAG = "[FAIMeasurement] "`(닫는 대괄호 뒤 공백 1개 포함)이므로, 세 경우 모두 `LOG_TAG + "Datum '"` / `LOG_TAG + "Measurement '"` / `LOG_TAG + "SHOT '"` 로 이어붙인 결과가 원본과 1글자도 다르지 않다 — 태그 뒤 공백을 호출부마다 손으로 입력하지 않아도 되므로 공백 누락/중복 사고가 구조적으로 불가능하다.

## Verification Results

| 검증 항목 | 기대값 | 실측값 | 결과 |
|---|---|---|---|
| `wc -l`(결정론적) | 1781 | 1781 | PASS |
| `LOG_TAG` 전체 출현(선언1+호출17) | 18 | 18 | PASS |
| `LOG_TAG + "` 패턴 | 17 | 17 | PASS |
| 선언문 정확 일치(`private const string LOG_TAG = "[FAIMeasurement] ";`) | 1 | 1 | PASS |
| `[FAIMeasurement]` 리터럴 잔존(선언 자신만) | 1 | 1 | PASS |
| ECrossZGate 신규 문서주석 삽입 확인 | 1 | 1 | PASS |
| `private enum ECrossZGate {` 카운트 | 1 | 1 | PASS |
| `switch (eGate)` 블록 30줄 byte-identical(base L691~720 vs current L697~726) | 동일 | 동일(diff 빈 출력) | PASS |
| `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 16줄 byte-identical(base L1098~1113 vs current L1104~1119) | 동일 | 동일(diff 빈 출력) | PASS |
| `bNonProtocolCycle` 코드부(선언/사용 6곳, header 제외) byte-identical | 동일 | 동일(diff 빈 출력) — 아래 참고 | PASS(세부 내용 참고) |
| 표본 3곳 텍스트 재구성 | 원본과 동일 | 원본과 동일 | PASS |
| `git diff --numstat HEAD~1 HEAD` | add=23/del=17 | add=23/del=17 | PASS |
| 커밋 파일 수 | 1 | 1 | PASS |
| `git status` csproj | unstaged M | unstaged M | PASS |
| MSBuild `-t:Rebuild`(Debug/x64, 스크래치 OutputPath) | error0/warning12 | error0/warning12(CS0618×10+CS0162×2) | PASS |
| 신규 경고(CS0219/CS0168/CS0103/CS0161) | 0 | 0 | PASS |
| UTF-8 BOM | efbbbf | efbbbf | PASS |
| CRLF 오염(python3 바이트 카운트) | 0 | 0 | PASS |

### `bNonProtocolCycle` 검증 스크립트 해석 조정 (코드 변경 아님)

plan의 verify 스크립트 3번째 항목은 파일 전체에서 `bNonProtocolCycle` 문자열 출현 개수를 base/current 간 그대로 비교하도록 작성돼 있었다. 그런데 plan 자신의 Step 3 `new_string`(ECrossZGate 문서주석 4줄)이 설명 목적으로 `bNonProtocolCycle`이라는 단어를 2번 포함하고 있어(예: "bNonProtocolCycle 분기(...)까지 합치면", "두 번째 축(bNonProtocolCycle)은..."), 전체 파일 카운트는 base 7건 → current 9건으로 자연히 늘어난다. 이는 plan이 요구한 삽입 자체(Step 3)의 필연적 결과이며 실제 판정 로직과는 무관함을 확인하기 위해, 헤더/주석 영역(line ≤ 60)을 제외한 코드부만 별도로 diff했다 — 코드부의 `bNonProtocolCycle` 6곳(선언 1 + 대입 1 + `if` 1 + `MarkCrossZHalfPending` 호출 1 + 함수 시그니처 1 + 함수 내부 `if` 1)은 base와 current가 완전히 byte-identical(줄번호만 +6 시프트)임을 확인했다. 참고: plan 텍스트의 "4개소"라는 표현은 코드부 실제 개소(6곳)와도 다른데, 이는 plan 작성 시점의 사전 조사 서술 오차로 판단되며(quick-260819-sgg SUMMARY에도 유사한 plan 문서화 오차 기록이 있음) 실행 결과나 회귀 여부에는 영향이 없다.

### 커밋 위생

`git add`로 대상 파일 경로만 직접 지정(`git add -A`/`-a` 미사용), `git diff --cached --name-only`로 정확히 1줄만 출력됨을 커밋 전 확인. 커밋 후 `git show --name-only --format='' HEAD`도 1개 파일만 출력. `git status --porcelain` 확인 결과 `WPF_Example/DatumMeasurement.csproj`는 커밋 전후 내내 ` M`(unstaged) 상태 유지, 한 번도 스테이징되지 않음. 빌드는 스크래치 `OutputPath`(`$SCR/tcs-t1/`)로 실행해 잠금/프로세스 종료 이슈 없음, 빌드 후 `git status --porcelain`에 신규 untracked 파일 없음(작업 트리 오염 0건).

## Decisions Made

이 플랜의 objective에 기록된 오케스트레이터 사전 판단을 그대로 재사용(재검토하지 않음):

### TCS-01 (유일한 실제 코드 변경) — LOG_TAG 상수화

`"[FAIMeasurement]"` 리터럴이 박힌 `Logging.PrintLog` 호출 17곳(사용자 원 카운트와 정확히 일치)을 `private const string LOG_TAG = "[FAIMeasurement] ";`(태그 뒤 공백까지 포함) 로 추출, 17곳 전부 `LOG_TAG + "..."` 형태로 치환. 태그 뒤 공백을 상수 안에 넣기로 결정한 이유: 공백을 호출부마다 손으로 입력할 필요가 없어져 17곳 중 하나라도 공백을 빠뜨리거나 중복하는 사고를 원천 차단.

### TCS-02 — `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 인라인, 하지 않기로 결정, 코드 무변경

각각 외부 호출자 정확히 1곳(둘 다 `TryGrabOrLoadCrossZDatumImages`, base L1038/L1040), 각각 존재 이유를 설명하는 1문장 주석 보유. 인라인 시 이 설명 주석을 호출부(`if (!bRelevant) { ... }` 분기, 이미 비자명함)로 옮기거나 버려야 하는데 둘 다 손해다: 자기설명적 함수 이름이 사라지고, 이미 복잡한 호출부 분기에 설명 주석이 얹히면 가독성이 오히려 떨어진다. 사용자 원 요청이 "고려"(선택)로 명시됐으므로, 조사 결과 "인라인하지 않는 것이 맞다"는 판단 자체를 이 커밋 없는 결정으로 기록한다. 이 두 함수는 이번 플랜에서 1바이트도 건드리지 않았음을 byte-identical diff(16줄, base L1098-1113 = current L1104-1119)로 확인했다.

### TCS-03 — `ECrossZGate`의 `NotMyTick`/`HalfPending` 내부 `bNonProtocolCycle` 이중분기, 문서화만, enum 무변경

enum을 확장하면 오늘 세 번째(hyk→sgg→이번)로 `switch (eGate)` 블록 전체 제어흐름을 재검증해야 하는데, 순전히 문서적 가치를 위해 그 위험을 또 감수할 이유가 없다고 판단했다. 대신 `private enum ECrossZGate { ... }` 선언 바로 위에 "이 enum은 완전한 상태표가 아니라 상위 분류일 뿐"이라고 명시하는 주석 4줄만 추가했다. `switch (eGate)` 블록·`bNonProtocolCycle` 선언/사용·`IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` 세 구역은 오늘 세 번째 재검증 대신, 삽입 지점(파일 상단 46번째 줄 부근)이 이 세 구역과 물리적으로 떨어져 있음을 grep -n 라인번호 기반 byte-identical diff로 확인하는 방식으로 대체했다 — `switch (eGate)` 블록 30줄(base L691-720 = current L697-726)과 `bNonProtocolCycle` 코드부 6곳 전부 diff 결과 빈 출력(완전 일치)을 확인했다.

## Deviations from Plan

None - 코드 로직/구조는 plan 대로 정확히 실행됨. plan의 verify 스크립트 중 `bNonProtocolCycle` 전체-파일 카운트 비교 1건이 plan 자신의 문서주석 텍스트(Step 3 new_string)로 인해 예상과 다른 카운트를 보였으나, 이는 위 "검증 스크립트 해석 조정" 섹션에서 코드부만 별도 diff하여 byte-identical임을 확인했다 — 코드 변경이나 회귀가 아니다.

**Total deviations:** 0건 코드 변경, 1건 검증 스크립트 해석 조정(plan 자신의 신규 문서주석 텍스트가 전체-파일 문자열 카운트에 포함된 것, 회귀 아님)
**Impact on plan:** 판정 로직/제어흐름 영향 없음. must_haves의 하드 게이트(줄수/카운트/diff증명/numstat/빌드)는 전부 PASS.

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요. 정적 검증(카운트+표본 대조+byte-identical diff+numstat+빌드)만으로 회귀 0을 결론지었다 — 로그 텍스트 표기 방식만 바뀌었을 뿐 판정 로직은 전혀 건드리지 않았다. 실기 확인이 필요하면: Shot 1개 검사 후 로그 파일(Error 레벨)에서 `[FAIMeasurement]`로 시작하는 라인이 이전과 동일한 형태(태그+공백+본문)로 찍히는지 확인(선택사항, 이번 세션에서는 미실행).

## Next Phase Readiness

- `Action_FAIMeasurement.cs` 오늘 6-bundle 리팩토링 백로그(q9t/rle/s05/sgg/sxj/tcs) 전부 완료 — 사용자 원 요청 우선순위 2(필수) + 우선순위 3(nit, 선택) 백로그 전체 마무리
- 파일 최종 1781줄, 후속 코드 작업 없음
- Blockers 없음

## Known Stubs

없음 - 순수 리팩토링(상수화 + 문서주석)이며 신규 데이터 소스/바인딩/UI 변경 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. T-tcs-01(LOG_TAG 공백 누락 위험)은 상수 선언문 정확 일치(grep -cF) + 표본 3곳 텍스트 재구성 대조로 검증 완료. T-tcs-02(sed 자기참조 오염 위험)는 실행 순서(sed→상수삽입) 고정 + 단계별 카운트 확인으로 사전 차단, 결과적으로 발생하지 않음. T-tcs-03(ECrossZGate 문서주석 삽입 시 switch/bNonProtocolCycle 훼손 위험)은 byte-identical diff 2건(switch 30줄, bNonProtocolCycle 코드부 6곳)으로 검증 완료.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
FOUND: .planning/quick/260819-tcs-fai-refactor-bundle-f/260819-tcs-SUMMARY.md
```

커밋 존재 확인:
```
FOUND: c7fbecd (Task 1)
```

---
*Phase: quick-260819-tcs*
*Completed: 2026-08-19*
