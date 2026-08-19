---
phase: quick-260819-sxj
plan: 01
status: complete
subsystem: inspection
tags: [csharp, refactor, extract-method, cross-z-datum-zindex-gate]

requires:
  - phase: quick-260819-sgg
    provides: "동일 파일 오늘자 Bundle D(ProcessCrossZCaptureTick out→CrossZCaptureTickResult) 결과물 위에서 진행"
provides:
  - "IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq) 공용 private 헬퍼 신설 — IsZIndexMisconfigured/IsDatumZIndexMisconfigured 의 거의 동일한 20여줄 본문을 1개로 병합, bBothUnset 가드 보존"
  - "IsZIndexMisconfigured/IsDatumZIndexMisconfigured 는 시그니처·외부 호출부(각 1곳) 무변경, 본문만 위임 1줄로 축소"
affects: [action-faimeasurement]

tech-stack:
  added: []
  patterns:
    - "거의 동일한 두 메서드를 병합할 때, 더 엄격한 가드를 가진 쪽(Datum, bBothUnset)을 공용 로직의 기준으로 삼고 얇은 primitive-parameter 헬퍼로 승격 — 느슨한 쪽(Dual)은 가드 분기에 구조적으로 도달 불가능하므로 동작 무변화(behaviorally inert)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "공용 헬퍼는 DualImageEdgeDistanceMeasurement/DatumConfig 같은 구체 타입이 아닌 primitive(int, int, InspectionSequence) 파라미터로 설계 — 이래야 두 호출부의 서로 다른 타입에서 값만 뽑아 공통 위임이 가능해짐"
  - "가드 위치는 bSingleSet 분기 바로 다음, bSameValue 분기보다 먼저 — 이 순서가 깨지면 (-1,-1) 입력이 bSameValue 단계에서 먼저 true 로 오판정되므로, 순서 불변식 자체를 grep -n 줄번호 비교로 별도 검증"
  - "구조적 diff 증명 2건(기계적, 손 판독 아님) + 제어흐름 순서 불변식 + 5-케이스 진리표 수기 트레이스 3중 검증을 모두 실행 — 사용자가 이 bundle 을 오늘 백로그 최고 위험으로 명시 지정했기 때문"

requirements-completed: [SXJ-01]

duration: 약 15분
completed: 2026-08-19
---

# Quick 260819-sxj: FAI 리팩토링 Bundle E (IsZIndexMisconfigured/IsDatumZIndexMisconfigured → IsCrossZIndexPairMisconfigured 공용 헬퍼 병합) Summary

**`Action_FAIMeasurement.cs` 최고위험 병합 리팩토링 1건 — 거의 동일한 두 오설정 판정 함수 `IsZIndexMisconfigured`(L1380)와 `IsDatumZIndexMisconfigured`(L1455)를 공용 private 헬퍼 `IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)`로 병합했다. 유일한 실질 차이인 `bBothUnset` 가드(크로스-Z 미사용 -1/-1 Datum을 조기에 정상 처리하는 안전장치)는 새 헬퍼에 그대로 보존되고, 두 원본 함수는 자기 시그니처를 1글자도 바꾸지 않은 채 헬퍼로 위임하는 1줄짜리 얇은 래퍼로 축소됐다. 외부 호출부(`EvaluateCrossZGate` L675, `ProcessDatumDualImage` L269)는 완전히 무변경. 병합이 안전함을 구조적 diff 증명 2건(기계적 byte-diff) + 제어흐름 순서 불변식(`bSingleSet`<`bBothUnset`<`bSameValue`<`bAExists` 줄번호 비교) + 5-케이스 진리표 수기 트레이스로 3중 검증했다. 파일 최종 1775줄(1777→1775, 순감소 -2), clean Rebuild error0/warning12(baseline) 확인.**

## Performance

- **Duration:** 약 15분 (커밋 1건, 3중 검증 + 빌드 포함)
- **Started:** 2026-08-19T11:49:00Z
- **Completed:** 2026-08-19T12:04:00Z
- **Tasks:** 1/1 완료
- **Files modified:** 1

## Accomplishments

- `IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)` 공용 private 헬퍼 신설 — primitive 파라미터, `bBothUnset` 가드 포함, Allman 브레이스 스타일(이 구역 기존 스타일 그대로)
- `IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)` 본문을 `return IsCrossZIndexPairMisconfigured(dualMeas.ZIndexA, dualMeas.ZIndexB, parentSeq2);` 1줄 위임으로 축소 — 시그니처 무변경, 유일한 외부 호출부 `EvaluateCrossZGate` L675 무변경
- `IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)` 본문을 `return IsCrossZIndexPairMisconfigured(datum.ZIndexA, datum.ZIndexB, parentSeq);` 1줄 위임으로 축소 — 시그니처 무변경, 유일한 외부 호출부 `ProcessDatumDualImage` L269 무변경
- 구조적 diff 증명 2건 PASS: (1) 헬퍼 본문(21줄) == 원본 `IsDatumZIndexMisconfigured` 본문(HEAD L1457-1477, `datum.ZIndexA/B`→`zIndexA/zIndexB` 치환) byte-identical, (2) 헬퍼 본문에서 `bBothUnset` 가드 5줄 제외한 16줄 == 원본 `IsZIndexMisconfigured` 본문(HEAD L1382-1397, `dualMeas.ZIndexA/B`→`zIndexA/zIndexB`, `parentSeq2`→`parentSeq` 치환) byte-identical
- 제어흐름 순서 불변식 PASS — 실측 줄번호 `bSingleSet`(1388) < `bBothUnset`(1393) < `bSameValue`(1398) < `bAExists`(1402)
- 5-케이스 진리표 수기 트레이스 완료(아래 표) — (a) 케이스가 가드 없이는 `bSameValue=(-1==-1)=true` 로 오판정됐을 자리임을 실제 코드에서 직접 짚어 확인
- 신규 코드 삼항 `?:` 0건(if-else 만 사용), C# 7.2, 파일 인코딩 손상 0건(UTF-8 BOM 유지, LF 유지, CRLF 오염 0건 — python3 바이트 카운트로 재확인)

## Task Commits

Each task was committed atomically:

1. **Task 1: IsZIndexMisconfigured / IsDatumZIndexMisconfigured → IsCrossZIndexPairMisconfigured 공용 헬퍼로 병합** - `3aa0958` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `IsCrossZIndexPairMisconfigured` 공용 헬퍼 신설(L1383-1406), `IsZIndexMisconfigured`/`IsDatumZIndexMisconfigured` 본문을 위임 1줄로 축소. 1777줄 → 1775줄(순감소 -2)

## 🔴 5-케이스 진리표 — 실제 병합 코드(`IsCrossZIndexPairMisconfigured`, L1383-1406) 손 트레이스 결과

| # | 입력 | 기대 반환 | 실제 트레이스(코드 L1383-1406 기준) | 결과 |
|---|---|---|---|---|
| (a) | zIndexA=-1, zIndexB=-1 (둘 다 미설정) | **false** | L1385 `bAUnset=true`, L1386 `bBUnset=true` → L1387 `bSingleSet=(true!=true)=false` → L1388 `if(false)` skip → L1392 `bBothUnset=(true&&true)=true` → L1393 `if(true)` 진입 → **L1395 `return false` 로 조기 종료, `bSameValue` 단계(L1397)에 도달하지 않음** | PASS(false) |
| (b) | 정확히 하나만 -1 (예: zIndexA=-1, zIndexB=0) | **true** | L1385 `bAUnset=true`, L1386 `bBUnset=false` → L1387 `bSingleSet=(true!=false)=true` → L1388 `if(true)` 진입 → **L1390 `return true` 로 조기 종료** | PASS(true) |
| (c) | 둘 다 설정 + 동일값 (예: zIndexA=zIndexB=2) | **true** | bSingleSet=false, bBothUnset=false → 둘 다 skip → L1397 `bSameValue=(2==2)=true` → L1398 `if(true)` 진입 → **L1400 `return true`** | PASS(true) |
| (d) | 둘 다 설정 + 다른값 + 레시피에 둘 다 존재 | **false** | bSingleSet=false, bBothUnset=false, bSameValue=false → 전부 skip → L1402 `bAExists=true`, L1403 `bBExists=true` → L1404 `bBothExist=true&&true=true` → **L1405 `return !true = false`** | PASS(false) |
| (e) | 둘 다 설정 + 다른값 + 최소 하나 레시피에 미존재 | **true** | 위와 동일하게 진행하다 L1402/L1403 중 하나가 `false`(예: `bBExists=false`) → L1404 `bBothExist=true&&false=false` → **L1405 `return !false = true`** | PASS(true) |

⚠ (a)가 유일한 위험 케이스임을 직접 확인 — `bBothUnset` 가드(L1392-1396)가 `bSameValue` 계산(L1397)보다 먼저 배치되어 있어, 가드가 조기에 `return false`로 빠져나가므로 `bSameValue=(-1==-1)=true`가 되는 오판정 경로 자체에 도달하지 않는다. 실제 코드가 진리표와 완전히 일치함을 확인했다.

## 구조적 diff 증명 2건 결과

플래너가 지정한 헬퍼 시그니처 줄번호(실측 `S=1383`)를 기준으로 자동화 스크립트로 실행:

**증명(1): 헬퍼 본문(S+2~S+22, 21줄) == 원본 `IsDatumZIndexMisconfigured` 본문(base L1457-1477, `datum.ZIndexA`→`zIndexA`/`datum.ZIndexB`→`zIndexB` 치환)**
```
diff <실제 헬퍼 본문> <치환된 원본 Datum 본문>
결과: 빈 출력 (OK, byte-identical)
```

**증명(2): 헬퍼 본문에서 `bBothUnset` 가드 5줄(S+9~S+13) 제외한 16줄(S+2~S+8, S+14~S+22) == 원본 `IsZIndexMisconfigured` 본문(base L1382-1397, `dualMeas.ZIndexA`→`zIndexA`/`dualMeas.ZIndexB`→`zIndexB`/`parentSeq2`→`parentSeq` 치환)**
```
diff <실제 헬퍼 본문 - 가드 5줄> <치환된 원본 Dual 본문>
결과: 빈 출력 (OK, byte-identical)
```

**가드 5줄 자체가 실제로 그 위치(S+9~S+13 = L1392-1396)에 있는지 직접 대조**: `bool bBothUnset = bAUnset && bBUnset;` / `if (bBothUnset)` / `{` / `return false; // 미설정(-1/-1) — 게이트 미해당, 기존 static 경로(D-07)` / `}` — 빈 출력(OK, 완전 일치).

이 2건이 통과함으로써 "병합 로직 = Datum 버전 원문 그대로"이자 동시에 "= Dual 버전 원문 + 가드 5줄만 추가"라는 것이 텍스트 레벨에서 증명됐다 — 5-케이스 진리표 트레이스(의미 동치)와 함께 이중으로 확증.

## Verification Results

| 검증 항목 | 기대값 | 실측값 | 결과 |
|---|---|---|---|
| `wc -l`(결정론적) | 1775 | 1775 | PASS |
| `IsZIndexMisconfigured(` 카운트 | 2(선언1+호출1) | 2 | PASS |
| `IsDatumZIndexMisconfigured(` 카운트 | 2(선언1+호출1) | 2 | PASS |
| `IsCrossZIndexPairMisconfigured(` 카운트 | 3(신규 선언1+위임호출2) | 3 | PASS |
| `IsZIndexMisconfigured` 시그니처 문자열 | 무변경 | 무변경 | PASS |
| `IsDatumZIndexMisconfigured` 시그니처 문자열 | 무변경 | 무변경 | PASS |
| `EvaluateCrossZGate` 호출부(L675) | 무변경 | 무변경 | PASS |
| `ProcessDatumDualImage` 호출부(L269) | 무변경 | 무변경 | PASS |
| 헬퍼 시그니처(primitive 파라미터) | `int zIndexA, int zIndexB, InspectionSequence parentSeq` | 일치 | PASS |
| 구조적 diff 증명(1) 헬퍼==Datum본문(치환) | byte-identical | byte-identical | PASS |
| 구조적 diff 증명(2) (헬퍼-가드)==Dual본문(치환) | byte-identical | byte-identical | PASS |
| 가드 5줄 위치 직접 대조 | byte-identical | byte-identical | PASS |
| 제어흐름 순서(`bSingleSet`<`bBothUnset`<`bSameValue`<`bAExists`) | 오름차순 | 1388<1393<1398<1402 | PASS |
| 5-케이스 진리표 수기 트레이스 | 5/5 일치 | 5/5 일치 | PASS |
| MSBuild Rebuild(Debug/x64, 스크래치 OutputPath) | error0/warning12 | error0/warning12(CS0618×10+CS0162×2) | PASS |
| UTF-8 BOM | efbbbf | efbbbf | PASS |
| CRLF 오염(python3 바이트 카운트) | 0 | 0 | PASS |
| 커밋 대상 파일 수 | 1 | 1 | PASS |
| `git status` csproj | unstaged M | unstaged M | PASS |
| 저장소 전체(`WPF_Example`) 두 함수 호출부 grep | 각 정확히 1곳, 이 파일 외 0건 | 각 정확히 1곳, 이 파일 외 0건 | PASS |

### 커밋 위생

`git add`로 대상 파일 경로만 직접 지정(`git add -A`/`-a` 미사용), `git diff --cached --name-only`로 정확히 1줄만 출력됨을 커밋 전 확인. 커밋 후 `git show --name-only --format='' HEAD`도 1개 파일만 출력. `git status --porcelain` 확인 결과 `WPF_Example/DatumMeasurement.csproj`는 커밋 전후 내내 ` M`(unstaged) 상태 유지, 한 번도 스테이징되지 않음.

### 참고 — CR 카운트 검증 방법 조정

초기에 `grep -c $'\r' "$F"`로 CR 오염을 확인했을 때 1회 `1775`(전체 줄수와 동일)라는 이상값이 나왔으나, 동일 명령을 반복 실행하자 즉시 `0`으로 재현되어 해당 결과가 일시적 셸 확장 아티팩트였음을 확인(quick-260819-q9t SUMMARY에도 동일 유형의 `grep -c $'\r'` 허위양성 기록이 있음 — 이 환경에서 알려진 함정). 최종적으로 `python3 -c "open(...,'rb').read().count(b'\r')"`로 바이트 레벨 재확인해 `0`(CRLF 오염 없음)을 결정론적으로 확정했다. 코드 파일 자체는 전혀 손상되지 않았다 — 검증 스크립트 해석 문제였을 뿐.

## Decisions Made

- Edit 도구를 사용해 2개 치환(Edit A/B) 전부 plan의 old_string/new_string을 그대로 적용 — 손으로 재구성하지 않음
- 구조적 diff 증명 2건 + 제어흐름 순서 불변식 + 5-케이스 진리표 수기 트레이스, plan이 요구한 3중 검증을 전부 수행(사용자가 이 bundle을 오늘 백로그 최고 위험으로 명시 지정했으므로)

## Deviations from Plan

None - 코드 로직/구조는 plan 대로 정확히 실행됨.

### 검증 스크립트 해석 조정 (코드 변경 아님)

**1. `grep -c $'\r'`의 1회성 허위양성**
- **Found during:** Task 1 verify 단계(인코딩/위생 체크)
- **Issue:** `grep -c $'\r' "$F"`가 1회 `1775`(전체 줄수)를 반환 — CRLF 전면 오염처럼 보이는 값
- **Fix:** 코드는 변경하지 않음. 동일 명령을 즉시 재실행하자 `0`으로 재현, 이후 python3 바이트 카운트(`data.count(b'\r')`)로 `0`을 결정론적으로 재확인 — 이 환경의 `grep -c $'\r'` 셸 확장이 간헐적으로 신뢰할 수 없음을 확인(과거 q9t SUMMARY에도 유사 기록 존재)
- **Files modified:** 없음(검증 방법만 python3 바이트 카운트로 전환)
- **Verification:** `head -c 200 "$F" | od -c`로 직접 바이트 확인, `python3 -c "...count(b'\r')"` 결과 `0`, `git diff --stat`도 순수 텍스트 치환(28 insertions/30 deletions)만 표시해 바이너리/인코딩 이상 없음 확인
- **Committed in:** 해당 없음(코드 변경 없는 검증 방법 조정)

---

**Total deviations:** 0건 코드 변경, 1건 검증 스크립트 해석 조정(셸 확장 허위양성 확인, 회귀 아님)
**Impact on plan:** 판정 로직/제어흐름 영향 없음. must_haves의 하드 게이트(줄수/카운트/diff증명/순서불변식/진리표/빌드)는 전부 PASS.

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요. 정적 검증(diff 증명 2건+순서 불변식+진리표 트레이스+grep 카운트+빌드)만으로 회귀 0 결론 — 판정 로직 자체는 원본 텍스트 그대로 이동했을 뿐 새로 계산되지 않음. plan이 권고한 실기 UAT((1) 크로스-Z 미사용 일반 Datum 정상 검출 확인, (2) 존재하지 않는 z_index를 가리키는 DualImage 측정의 오설정 NG 확인)는 선택사항이며 이번 세션에서는 미실행.

## Next Phase Readiness

- `Action_FAIMeasurement.cs` Bundle E(IsCrossZIndexPairMisconfigured) 정리 완료, 후속 코드 작업 없음
- Blockers 없음
- 오늘 리팩토링 시리즈(fik/gf1/hyk/j6j/q9t/rle/s05/sgg/sxj) 전부 "동작 무변경" 검증됨, 파일 최종 1775줄

## Known Stubs

없음 - 순수 리팩토링(메서드 병합)이며 신규 데이터 소스/바인딩/UI 변경 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. T-sxj-01(bBothUnset 가드 누락 위험)은 구조적 diff 증명 2건+순서 불변식+5-케이스 진리표 트레이스 3중으로 하드 검증 완료. T-sxj-02(두 래퍼 시그니처/호출부 변경 위험)는 grep 카운트(선언+호출 각 2건, 병합 전과 동일) + 저장소 전체 grep(이 파일 외 참조 0건)으로 검증 완료.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
FOUND: .planning/quick/260819-sxj-fai-refactor-bundle-e/260819-sxj-SUMMARY.md
```

커밋 존재 확인:
```
FOUND: 3aa0958 (Task 1)
```

---
*Phase: quick-260819-sxj*
*Completed: 2026-08-19*
