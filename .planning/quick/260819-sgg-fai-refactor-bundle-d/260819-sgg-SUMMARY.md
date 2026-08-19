---
phase: quick-260819-sgg
plan: 01
status: complete
subsystem: inspection
tags: [csharp, refactor, out-params-to-return-value, cross-z-capture]

requires:
  - phase: quick-260819-s05
    provides: "동일 파일 오늘자 Bundle C(RunDatumDualImageDetection/RunDatumSingleImageDetection 헬퍼) 결과물 위에서 진행"
provides:
  - "CrossZCaptureTickResult(Relevant/CaptureOk/Completed/CapturedRoleKey) 클래스 신설 — ProcessCrossZCaptureTick 의 4개 out 파라미터를 이름 있는 필드로 교체"
  - "ProcessCrossZCaptureTick 시그니처가 out 4개 → CrossZCaptureTickResult 리턴값 1개로 전환(원본 4개 return 지점/조건/부수효과 완전 보존)"
affects: [action-faimeasurement]

tech-stack:
  added: []
  patterns:
    - "out 다중 파라미터 대신 이름 있는 필드를 가진 소형 private class 리턴값 사용 — 파일 상단 ShotMeasureAccumulator 와 동일한 K&R+public 필드 스타일"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "quick-260819-hyk 가 오늘 이전 세션에서 6-경로 bool-매핑 표로 검증 완료한 switch(eGate) 블록을 재손상시키지 않기 위해, 편집 전 30줄 전체를 스크래치 파일로 스냅샷 후 편집 후 재추출해 diff 로 대조 — case HalfPending/BothReady 의 TakeCrossZRoleImageIfFirst 인자 표현식 2줄만 변경되고 나머지 28줄(case 라벨 5개, MarkCrossZHalfPending 호출, 모든 return 문, 주석, 중괄호)은 완전 byte-identical 함을 실측 확인"
  - "plan 의 verify 섹션에 있던 2개 grep 체크가 실제 코드와 불일치함을 발견(ResolveCrossZGate 반환형이 실제로는 bool 이 아니라 ECrossZGate, MarkCrossZHalfPending( 카운트가 실제로는 1 이 아니라 2(정의+호출)) — 둘 다 이번 편집이 건드리지 않은 기존 코드에 대한 plan 문서화 오류였고, git diff 로 해당 두 메서드 정의 라인(782/801)이 이번 커밋에 전혀 나타나지 않음을 확인해 회귀가 아님을 검증"

requirements-completed: [SGG-01]

duration: 약 10분
completed: 2026-08-19
---

# Quick 260819-sgg: FAI 리팩토링 Bundle D (ProcessCrossZCaptureTick out 4개 → CrossZCaptureTickResult) Summary

**`Action_FAIMeasurement.cs` 순수 시그니처 리팩토링 1건 — `ProcessCrossZCaptureTick` 의 이름 없는 `out` 파라미터 4개(bRelevant/bCaptureOk/bCompleted/szCapturedRoleKey)를 이름 있는 필드 4개짜리 `CrossZCaptureTickResult` 클래스 리턴값으로 교체. 유일한 호출부 `EvaluateCrossZGate` 는 정확히 3곳만 재배선(진입부 선언, ResolveCrossZGate 호출, case HalfPending/BothReady 의 TakeCrossZRoleImageIfFirst 인자). `wc -l` 최종 실측값(1777)과 정확히 일치, clean Rebuild error0/warning12(baseline). switch(eGate) 30줄 블록을 편집 전/후 diff 로 직접 대조해 지정된 2줄 외 byte-identical 확인.**

## Performance

- **Duration:** 약 10분 (커밋 1건, 검증+빌드+switch 블록 byte-diff 포함)
- **Started:** 2026-08-19T11:30:00Z
- **Completed:** 2026-08-19T11:45:00Z
- **Tasks:** 1/1 완료
- **Files modified:** 1

## Accomplishments

- `CrossZCaptureTickResult` 클래스 신설(`private class`, `public bool Relevant/CaptureOk/Completed`, `public string CapturedRoleKey`) — 파일 상단 `ShotMeasureAccumulator`(L60-67)와 동일한 K&R+public 필드 스타일
- `ProcessCrossZCaptureTick` 시그니처를 `out` 파라미터 0개, `CrossZCaptureTickResult` 반환으로 전환 — 원본과 동일한 4개 return 지점(진입 직후 null 가드, bRelevant 조기 return, capturedImage==null 조기 return, 정상 종료)/조건/부수효과(`parentSeq2.StoreCrossZImage` 등) 완전 보존, 내부 지역변수명은 `result`(호출부 `tickResult` 와 다른 스코프)
- `EvaluateCrossZGate` 호출부가 4개 지역변수 선언을 제거하고 `CrossZCaptureTickResult tickResult = new CrossZCaptureTickResult();` 로 all-default 선치화 — `bMisconfigured` 분기에서는 여전히 `ProcessCrossZCaptureTick` 호출 안 함(원본과 동치)
- `ResolveCrossZGate(tickResult.Relevant, tickResult.CaptureOk, tickResult.Completed)` + `TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage)`(HalfPending/BothReady 2곳) — `ResolveCrossZGate`/`TakeCrossZRoleImageIfFirst` 두 메서드 자신의 시그니처는 1글자도 변경 없음
- `switch (eGate)` 블록의 case 라벨 5개와 나머지 본문은 편집 전/후 30줄 스냅샷 diff 로 직접 대조해 지정된 2줄(HalfPending/BothReady 의 TakeCrossZRoleImageIfFirst 인자 표현식) 외 전부 byte-identical 확인 — 오늘 quick-260819-hyk 가 검증한 구역 무손상
- 신규 코드 삼항 `?:` 0건(if-else 만 사용), C# 7.2, public/internal 시그니처 변경 0건(EvaluateCrossZGate 자신의 시그니처 무변경)

## Task Commits

Each task was committed atomically:

1. **Task 1: ProcessCrossZCaptureTick out 4개 → CrossZCaptureTickResult 클래스 리턴값 전환** - `abc27e3` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `CrossZCaptureTickResult` 클래스 신설, `ProcessCrossZCaptureTick` out→리턴값 전환, `EvaluateCrossZGate` 호출부 3곳 재배선. 1771줄 → 1777줄(순증가 +6)

## Verification Results

| 검증 항목 | 기대값 | 실측값 | 결과 |
|---|---|---|---|
| `wc -l`(결정론적) | 1777 | 1777 | PASS |
| `ProcessCrossZCaptureTick(` 카운트 | 2(선언1+호출1) | 2 | PASS |
| `class CrossZCaptureTickResult` 선언 | 1 | 1 | PASS |
| `tickResult.` 필드읽기 | 7(Resolve 3+HalfPending 2+BothReady 2) | 7 | PASS |
| `out bool bRelevant/bCaptureOk/bCompleted/out string szCapturedRoleKey` | 0(4개 전부) | 0 | PASS |
| `TakeCrossZRoleImageIfFirst(` | 3(정의1+호출2, 무변경) | 3 | PASS |
| `ResolveCrossZGate(` | 2(정의1+호출1, 무변경) | 2 | PASS |
| `IsZIndexMisconfigured(` | 2(정의1+호출1, 무변경) | 2 | PASS |
| `bNonProtocolCycle` 전역 | 7(무변경) | 7 | PASS |
| switch case 라벨 5개 전부 | 각 1 | 각 1 | PASS |
| UTF-8 BOM | efbbbf | efbbbf | PASS |
| CRLF 오염 | 0 | 0 | PASS |
| 두 case 호출식 완전 동일 문자열 | 2 | 2 | PASS |
| 옛 지역변수 조합(`bCaptureOk, szCapturedRoleKey`) 잔존 | 0 | 0 | PASS |
| MSBuild Rebuild(Debug/x64, 스크래치 OutputPath) | error0/warning12 | error0/warning12(CS0618×10+CS0162×2) | PASS |
| 신규 CS0219/CS0168/CS0103/CS0161 | 0 | 0 | PASS |
| 커밋 대상 파일 수 | 1 | 1 | PASS |
| `git status` csproj | unstaged M | unstaged M | PASS |

### 추가 검증 — switch 블록 byte-diff (사용자 명시 요구, plan 의 grep 카운트만으론 부족하다고 판단)

편집 직전 `switch (eGate)` 블록 전체(원본 L692-721, 30줄)를 스크래치 파일로 스냅샷 저장 후, 4개 Edit 적용 완료 뒤(Edit A 의 -1줄 순감소로 블록이 L691-720 로 1줄 이동) 동일 30줄을 재추출해 `diff` 로 직접 대조:

```
24c24
<                         TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);
---
>                         TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);
28c28
<                         TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);
---
>                         TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);
```

diff 결과 2개 hunk(라인 24, 28)만 존재 — 각각 HalfPending/BothReady case 의 `TakeCrossZRoleImageIfFirst` 인자 표현식만 변경되고, 나머지 28줄(5개 case 라벨 전체, `MarkCrossZHalfPending` 호출, `MarkMeasurementZIndexMisconfigured`/`MarkMeasurementCrossZIncomplete`/`meas.ClearResult` 등 다른 case 본문, 모든 `return` 문, `default:` 부재, 주석, 중괄호)은 완전 byte-identical 함을 실측 확인. quick-260819-hyk 가 6-경로 bool-매핑 표로 검증한 구역이 이번 편집으로 손상되지 않았음을 확증.

### 커밋 위생

`git add`로 대상 파일 경로만 직접 지정(`git add -A`/`-a` 미사용), `git diff --cached --name-only` 로 정확히 1줄만 출력됨을 커밋 전 확인. 커밋 후 `git show --name-only --format='' HEAD` 도 1개 파일만 출력. `git status --porcelain` 확인 결과 `WPF_Example/DatumMeasurement.csproj` 는 커밋 전후 내내 ` M`(unstaged) 상태 유지, 한 번도 스테이징되지 않음. 커밋된 blob(`git show HEAD:파일경로`) 자체도 CRLF 0건/BOM 유지/1777줄로 working tree 와 완전히 일치 확인(git `core.autocrlf=true` 로 인한 "LF will be replaced by CRLF" 경고는 `git diff`/`git add` 시 통상적으로 뜨는 정보성 메시지일 뿐, 실제 커밋된 blob 은 LF 로 정상 저장됨을 위 확인으로 검증).

## Decisions Made

- Edit 도구를 사용해 4개 치환(Edit A/B/C/D) 전부 plan 의 old_string/new_string 을 그대로 적용 — 손으로 재구성하지 않음
- switch 블록 byte-diff 를 plan 의 grep 카운트 검증에 더해 추가로 수행(사용자가 이 bundle 을 "HIGHER RISK"로 명시 지정했으므로) — 스크래치 파일 2개(편집 전/후 스냅샷) 로 diff 대조해 2줄 외 완전 무변경임을 결정론적으로 확증
- plan verify 섹션의 `ResolveCrossZGate` 시그니처 문자열(반환형 `bool` 로 명시)과 `MarkCrossZHalfPending(` 카운트(1 기대) 2개 체크가 실제 코드와 불일치함을 발견 — `ResolveCrossZGate` 의 실제 반환형은 `ECrossZGate`(L782, 이번 편집 무관), `MarkCrossZHalfPending(` 실제 카운트는 2(정의 L801 + 호출 L715, 이번 편집 무관). `git diff` 로 782/801 두 라인이 이번 커밋 diff 에 전혀 등장하지 않음을 확인해 회귀가 아닌 plan 문서화 오류로 결론

## Deviations from Plan

None - 코드 로직/구조는 plan 대로 정확히 실행됨.

### 검증 스크립트 해석 조정 (코드 변경 아님)

**1. plan verify 섹션의 `ResolveCrossZGate`/`MarkCrossZHalfPending` grep 체크 2건이 기존 코드 상태와 불일치**
- **Found during:** Task 1 verify 단계
- **Issue:** plan 이 `private bool ResolveCrossZGate(bool bRelevant, bool bCaptureOk, bool bCompleted)`(반환형 bool) 문자열 매치와 `MarkCrossZHalfPending(` 카운트=1 을 기대했으나, 실제 코드는 각각 `private ECrossZGate ResolveCrossZGate(...)`(반환형 ECrossZGate)와 카운트=2(정의+호출)
- **Fix:** 코드는 변경하지 않음(이 두 메서드는 이번 plan 의 편집 대상이 아님). `git diff`로 782/801 라인이 이번 diff 에 없음을 확인, `grep -n`으로 실제 시그니처/카운트를 재확인해 plan 문서 작성 시점의 단순 오기임을 결론
- **Files modified:** 없음(검증 해석만 조정)
- **Verification:** `grep -n 'ResolveCrossZGate\|MarkCrossZHalfPending' Action_FAIMeasurement.cs` 로 실제 정의/호출 위치 재확인, `git diff -- 파일경로` 로 782/801 라인이 diff hunk 에 등장하지 않음(=편집 전후 완전 동일) 확인
- **Committed in:** 해당 없음(코드 변경 없는 검증 방법 조정)

---

**Total deviations:** 0건 코드 변경, 1건 검증 스크립트 해석 조정(plan 문서 오기 확인, 회귀 아님)
**Impact on plan:** 판정 로직/제어흐름 영향 없음. must_haves 의 하드 게이트(줄수/카운트/switch 블록 보존/빌드)는 전부 PASS.

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요. 정적 검증(grep 카운트+wc -l+빌드+switch 블록 byte-diff)만으로 회귀 0 결론 — 순수 out→리턴값 전환이라 판정 로직 접근 없음. plan 이 권고한 실기 UAT(크로스-Z 측정 2회 촬영 A/B 확인, 수동 RUN NG 처리 확인)는 선택사항이며 이번 세션에서는 미실행.

## Next Phase Readiness

- `Action_FAIMeasurement.cs` Bundle D(CrossZCaptureTickResult) 정리 완료, 후속 코드 작업 없음
- Blockers 없음
- 오늘 리팩토링 시리즈(fik/gf1/hyk/j6j/q9t/rle/s05/sgg) 전부 "동작 무변경" 검증됨, 파일 최종 1777줄

## Known Stubs

없음 - 순수 리팩토링(out→리턴값 전환)이며 신규 데이터 소스/바인딩/UI 변경 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. `tickResult` all-default 선치화(T-sgg-01, mitigate)는 `bMisconfigured` 분기 시 필드 4개가 원본 out 초기값(false/false/false/null)과 동치임을 grep+빌드로 확인. switch(eGate) 의 case HalfPending/BothReady 2줄만 정확히 치환(T-sgg-02, mitigate)은 위 byte-diff 로 하드 검증 완료.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
FOUND: .planning/quick/260819-sgg-fai-refactor-bundle-d/260819-sgg-SUMMARY.md
```

커밋 존재 확인:
```
FOUND: abc27e3 (Task 1)
```

---
*Phase: quick-260819-sgg*
*Completed: 2026-08-19*
