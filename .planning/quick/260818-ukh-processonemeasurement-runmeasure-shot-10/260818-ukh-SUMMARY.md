---
phase: quick-260818-ukh
plan: 01
subsystem: inspection-sequence
tags: [refactor, extract-method, behavior-preserving, fai-measurement]
requires: []
provides:
  - "Action_FAIMeasurement.LogAndTallyAlgorithm(private)"
  - "Action_FAIMeasurement.MeasureShotFaiList(private)"
affects:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
tech-stack:
  added: []
  patterns: [extract-method, ref-parameter-passing]
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "값형 4개(allPass/measuredCount/nMeasNg/bShotDisplayImageReplaced)는 ref, 참조형 3개(parentSeq2/overlayAcc/dctAlgoUsed)는 값 전달"
  - "조기 return 미도입 — 호출부 if (ShotParam != null) 를 그대로 유지"
  - "Stopwatch 객체를 그대로 전달해 ElapsedMilliseconds 읽는 시점을 원본과 동일하게 보존"
metrics:
  duration: "~35분"
  completed: 2026-08-18
---

# Quick 260818-ukh: ProcessOneMeasurement / RunMeasure Shot 본문 Extract Method — Summary

`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 에서 **순수 Extract Method 2건**만 수행했다.
로직 추가·조건 변경·순서 변경은 0건이며, 아래 §1 의 **바이트 동치 diff 2건이 모두 빈 출력**이라는 사실로 기계적으로 증명된다.

| 항목 | 값 |
|------|-----|
| 착수 baseline | `14cf3f1` |
| 커밋 | `908d7a3`(Task 1), `2d067e7`(Task 2) |
| 변경 파일 (14cf3f1..HEAD) | 1개 — `Action_FAIMeasurement.cs` 만 |
| 파일 줄 수 | 1721 → 1747 (+26 = 신규 주석 13 + 시그니처 7 + 호출부 2 + 빈줄 2 + 닫는 `}` 2) |
| 빌드 | Debug\|x64 SIMUL 성공, 경고 12줄(CS0618×10 + CS0162×2) baseline 동일 |

추출 대상:

1. `ProcessOneMeasurement` 의 알고리즘 로그 조립 + 집계 18줄(@14cf3f1 L641–658) → **`LogAndTallyAlgorithm(...)`**
2. `RunMeasure` 의 `if (ShotParam != null)` 안쪽 48줄(@14cf3f1 L458–505, `using` 문 전체) → **`MeasureShotFaiList(...)`**

---

## ① 바이트 동치 증명 (핵심 증거)

| 추출 | 원본 범위(@14cf3f1) | 정규화 방식 | diff 실제 출력 |
|------|---------------------|-------------|----------------|
| ① `LogAndTallyAlgorithm` | L641–658 (18줄) | `bOk` → `ok` 치환 | **(출력 없음 / exit 0)** |
| ② `MeasureShotFaiList` | L458–505 (48줄) | 선행 공백 전부 제거 | **(출력 없음 / exit 0)** |
| ②-strict `MeasureShotFaiList` | L458–505 (48줄) | **선행 4칸만 dedent** (공백 정규화 없음) | **(출력 없음 / exit 0)** |

실행한 명령과 실제 출력:

```
$ diff <(git show 14cf3f1:$F | sed -n '641,658p') \
       <(sed -n '/측정 알고리즘을 탔는지/,/swMeasureExec.ElapsedMilliseconds);/p' $F | sed 's/\bbOk\b/ok/g')
diffexit=0            ← 출력 한 줄도 없음

$ L=493   # 이동 후 using 시작 줄
$ diff <(git show 14cf3f1:$F | sed -n '458,505p' | sed 's/^[[:space:]]*//') \
       <(sed -n "493,540p" $F | sed 's/^[[:space:]]*//')
diffexit=0            ← 출력 한 줄도 없음

$ diff <(git show 14cf3f1:$F | sed -n '458,505p' | sed 's/^    //') <(sed -n "493,540p" $F)
strictexit=0          ← 4칸 dedent만 해도 완전 일치 = 토큰 변경 0건
```

앵커 유효성도 함께 못박았다(짧은 `알고리즘을 탔는지` 는 파일에 2건이라 못 쓴다):

```
grep -cF '측정 알고리즘을 탔는지'  → 1     (앵커 유일)
sed -n '/측정 알고리즘을 탔는지/,/swMeasureExec.ElapsedMilliseconds);/p' | wc -l → 18  (범위 길이 정확)
```

### ①-b 파일 전체 라인 멀티셋 대조 (추가 증거)

부분 범위 diff 만으로는 "다른 데서 뭔가 지워졌을 가능성"이 남는다. 그래서 **파일 전체 1721줄 vs 1747줄을
공백 제거 후 정렬해 멀티셋으로 대조**했다. 결과는 아래가 전부다:

```
$ diff <(git show 14cf3f1:$F | sed 's/^[[:space:]]*//' | sort) <(sed 's/^[[:space:]]*//' $F | sort)
```

| 방향 | 줄 수 | 내용 |
|------|-------|------|
| 추가(`>`) | 26 | 신규 주석 13줄 / 신규 시그니처 7줄 / 호출부 2줄 / 빈줄 2줄 / 닫는 `}` 2줄 / `if (bOk) szAlgoResult = "OK";` 1줄 |
| 삭제(`<`) | **1** | `if (ok) szAlgoResult = "OK";` — 위 `if (bOk)` 로 리네임된 바로 그 줄 |

즉 **파일 전체를 통틀어 사라진 실행 코드는 0줄**이고, 바뀐 실행 코드는 `ok` → `bOk` 파라미터 리네임 1줄뿐이다.
이동한 66줄(18+48)은 삭제/추가 양쪽에 나타나지 않았다 — 멀티셋이 보존됐다는 뜻, 즉 문장 유실·중복이 없다.

---

## ② `ref` 전수표 — 컴파일러가 못 잡는 최대 위험

> **⚠ `ref` 를 빠뜨려도 컴파일은 통과한다.** C# 에서 값 파라미터도 그 자체가 변수라
> `ProcessOneMeasurement(..., ref measuredCount, ...)` 처럼 다시 `ref` 로 넘길 수 있기 때문이다.
> 결과는 **호출자의 카운터가 조용히 0 으로 남는 런타임 회귀**(측정 개수·공차이탈 개수 오보고, NG 가 PASS 로 샘)다.
> 따라서 이 결함의 방어선은 컴파일러가 아니라 **아래 시그니처/호출부 grep + 내일 아침 UAT 항목 1·3** 뿐이다.

| 변수 | 형 | 블록 안 취급 | 전달 방식 | 근거 |
|------|----|--------------|-----------|------|
| `allPass` | `bool` (값형) | `allPass = false;` 대입 있음 | **`ref bool`** | 대입 결과가 호출자 `pMyContext.AllPass` 로 흘러가야 함 |
| `measuredCount` | `int` (값형) | `ref measuredCount` 로 2회 재전달(`ProcessOneMeasurement`, `MarkAllMeasurementsNoImage`) | **`ref int`** | 호출자 `pMyContext.MeasuredCount` / `[SEQ]` 요약의 "측정 N개" |
| `nMeasNg` | `int` (값형) | `ref nMeasNg` 재전달 | **`ref int`** | `[SEQ]` 요약의 "공차이탈 M개" |
| `bShotDisplayImageReplaced` | `bool` (값형) | `ref bShotDisplayImageReplaced` 재전달(`FinalizeFaiTick`) | **`ref bool`** | Shot 전체에서 크로스-Z 표시 이미지 1회 교체 규칙 유지 |
| `parentSeq2` | `InspectionSequence` (참조형) | 읽기만, 재대입 없음 | 값 전달 | 재대입이 없어 `ref` 불필요 |
| `overlayAcc` | `List<EdgeInspectionOverlay>` | `Add` 만, 재대입 없음 | 값 전달 | 참조형이라 값 전달로도 호출자 리스트가 갱신됨 |
| `dctAlgoUsed` | `Dictionary<string,int>` | 인덱서 갱신만, 재대입 없음 | 값 전달 | 참조형이라 값 전달로도 호출자 딕셔너리가 갱신됨 |

### 시그니처 실제 인용 (L488–492)

```csharp
        private void MeasureShotFaiList(InspectionSequence parentSeq2,
                                        List<EdgeInspectionOverlay> overlayAcc,
                                        Dictionary<string, int> dctAlgoUsed,
                                        ref bool allPass, ref int measuredCount,
                                        ref int nMeasNg, ref bool bShotDisplayImageReplaced) {
```

### 호출부 실제 인용 (L457–459)

```csharp
            if (ShotParam != null) {
                MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced);
            }
```

시그니처의 `ref` 4개와 호출부의 `ref` 4개가 **변수명 순서까지 1:1 로 일치**한다(눈으로 대조 가능하도록 위에 나란히 인용).
자동 검증 결과:

```
private void MeasureShotFaiList(InspectionSequence parentSeq2,                   → 1
ref bool allPass, ref int measuredCount,                                         → 1
ref int nMeasNg, ref bool bShotDisplayImageReplaced) {                           → 1
MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced);  → 1
```

`LogAndTallyAlgorithm` 은 `ref` 가 0개다(값형 인자 `bHasAnyZIndex` / `ok` 는 읽기 전용, `dctAlgoUsed` / `swMeasureExec` 는 참조형):

```
656:            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);
682:        private void LogAndTallyAlgorithm(MeasurementBase meas, bool bHasAnyZIndex, bool bOk,
683:                                          Dictionary<string, int> dctAlgoUsed, Stopwatch swMeasureExec) {
```

---

## ③ `using` / `try-finally` 무접촉 증명

과거 실제 누수 버그 이력(`260810 hbk quick-debug(capture-render-per-fai-slow) round4 fix` — `try` 시작을
`sharedSrc` 생성 직후로 넓혀 ref 2중 누수를 막은 수정)이 있는 구간이라 재구성을 일절 하지 않았다.

| 구조 | 개수 | 현재 줄번호 | @14cf3f1 줄번호 | using 기준 상대 오프셋 |
|------|------|-------------|-----------------|------------------------|
| `using (var image = ShotParam.GetImage()) {` | 1 | 493 | 458 | 0 (기준) |
| `//260810 … round4 fix` 주석 첫 줄 | 1 | 500 | 465 | **+7 (동일)** |
| `try {` (sharedSrc 생성 직후) | 1 | 505 | 470 | **+12 (동일)** |
| `} finally { // 검사 루프 소유 ref 1 해제…` | 1 | 530 | 495 | **+37 (동일)** |
| `if (sharedSrc != null) sharedSrc.Release();` | 1 | 531 | 496 | **+38 (동일)** |

round4 주석은 여전히 `try {` **바로 위**(500–504 → 505)에 붙어 있다.

중첩 관계 보존 논리: §1 의 ②-strict diff 가 **48줄을 한 글자도 안 틀리고 통과**했으므로,
`using` 의 여는 줄부터 닫는 줄까지 전체와 그 안의 `try` / `finally` / `else` 가 **원래의 상대 위치·중첩 그대로**
새 메서드 안으로 함께 들어갔음이 기계적으로 보장된다. 브레이스 짝이 하나라도 어긋났다면 48줄 범위가
`using` 을 닫지 못해 컴파일 에러가 났을 것이고, 순서가 바뀌었다면 diff 가 비지 않았을 것이다.

---

## ④ 부수효과 / 시점 보존

**(a) `dctAlgoUsed` 집계 보존**

```
grep -cF 'dctAlgoUsed[szAlgoType]'   → 2     (before 2 / after 2)
```
`if (dctAlgoUsed.ContainsKey(szAlgoType)) dctAlgoUsed[szAlgoType]++;` / `else dctAlgoUsed[szAlgoType] = 1;` 두 줄이
그대로 헬퍼 안에 있다. `Dictionary` 는 참조형이라 값 전달로도 호출자 인스턴스가 갱신되고,
그 값을 `RunMeasure` 끝의 `[SEQ] 완료 — … │ 알고리즘: {4}` 요약이 소비한다. 메서드 이름에 `Tally` 를 넣어
"로그만 찍는 게 아니라 집계도 한다"는 사실이 이름에 드러나게 했다.

**(b) `swMeasureExec.ElapsedMilliseconds` 읽는 시점 보존**

```
grep -cF 'swMeasureExec.ElapsedMilliseconds'                      → 1   (헬퍼 안 PrintLog 인자 위치)
grep -cE '^\s*LogAndTallyAlgorithm\([^)]*ElapsedMilliseconds'     → 0   (호출부 선계산 없음)
```
호출부는 `Stopwatch` **객체 자체**를 넘긴다. ms 를 호출부에서 미리 계산해 넘겼다면 읽는 시점이 앞당겨져
로그 시간값이 실제보다 짧게 기록됐을 텐데, 그렇게 하지 않았다. `var swMeasureExec = Stopwatch.StartNew();`
선언도 원래 자리(`ProcessOneMeasurement` 안)에 그대로 남아 있다.

**(c) `[ALGO]` 로그 포맷 리터럴 바이트 동일**

```
grep -cF '"[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms"'          → 1   (before 1 / after 1)
grep -cE '^\s*Logging\.PrintLog\(\(int\)ELogType\.Algorithm,'     → 1
```
`·` `→` 유니코드 문자 포함 한 글자도 바뀌지 않았다(§1 diff 가 이를 포함해 통과).

**(d) 조기 return 미도입**

```
grep -cE '^\s*if \(ShotParam != null\) \{$'   → 4   (before 4 / after 4)
grep -cE '^\s*if \(ShotParam == null\) return;' → 0
```
`if (ShotParam != null) {` 이 L457 에 그대로 남아 있어, `ShotParam == null` 이어도 그 뒤가 원본대로 실행된다:

| 실행되어야 하는 것 | 현재 줄번호 |
|--------------------|-------------|
| `pMyContext.AllPass = allPass;` / `MeasuredCount` / `InspectionOverlays` | 460–462 |
| `[SEQ] 완료 — 측정 N개 (공차이탈 M개) …` 요약 로그 | 475–477 |
| `Step = (int)EStep.End;` | 478 |

조기 return 으로 바꿨다면 이 3가지가 전부 스킵되어 시퀀스가 End 로 못 넘어갔을 것이다.

---

## ⑤ 불변 카운트 before-after 표

`14cf3f1`(before) 실측 vs 현재(after) 실측. **12종 전부 동일.**

| # | 앵커 | before | after |
|---|------|--------|-------|
| 1 | `measuredCount++;` | 8 | 8 |
| 2 | `faiAllPass = false;` | 8 | 8 |
| 3 | `if (!meas.LastJudgement) nMeasNg++;` | 1 | 1 |
| 4 | `allPass = false;` | 1 | 1 |
| 5 | `MarkAllMeasurementsNoImage(ref measuredCount);` | 1 | 1 |
| 6 | `if (sharedSrc != null) sharedSrc.Release();` | 1 | 1 |
| 7 | `ProcessOneMeasurement(meas,` | 1 | 1 |
| 8 | `FinalizeFaiTick(fai,` | 1 | 1 |
| 9 | `bool faiAllPass = true;` | 1 | 1 |
| 10 | `using (var image = ShotParam.GetImage()) {` | 1 | 1 |
| 11 | `dctAlgoUsed[szAlgoType]` | 2 | 2 |
| 12 | `swMeasureExec.ElapsedMilliseconds` | 1 | 1 |

범위 밖 무회귀 / 위생:

| 앵커 | before | after |
|------|--------|-------|
| `case ECrossZGate.*:` (크로스-Z 게이트 switch) | 5 | 5 |
| `case EStep.*:` | 6 | 6 |
| 코드 삼항 `?:` (남는 1줄은 기존 주석) | 1 | 1 |
| `=> ` (expression-bodied / 람다) | 1 | 1 |
| `if (ShotParam != null) {` | 4 | 4 |

보존 주석 5계열 — 삭제 0건(전부 로직을 따라 이동):

| 주석 계열 | after 개수 |
|-----------|-----------|
| `capture-render-per-fai-slow) round4 fix` | 1 |
| `top-z1-measure-8sec-slow) fix` | 4 |
| `260616 hbk simul-shot-cascade` | 1 |
| `260619 hbk per-shot 보정계수` | 1 |
| `260729-hwb` | 8 |

### 빌드

```
baseline(14cf3f1) 경고:      2 warning CS0162 / 10 warning CS0618   (합 12줄)
Task 1 후 경고:              2 warning CS0162 / 10 warning CS0618   → baseline 과 diff 빈 출력
Task 2 후 경고:              2 warning CS0162 / 10 warning CS0618   → baseline 과 diff 빈 출력
error CS*:                   0
CS0219 / CS0168 / CS0177 / CS0165 / CS0206 신규:  0
```

- 구성: `Debug|x64`, `-t:Rebuild`, 산출물은 스크래치 `OutputPath` 로 분리(현장 `D:\Data\` 무접촉, 프로세스 종료 없음).
- 신규 `CS0219`(미사용 지역변수) 0건 = 추출 후 원본 쪽에 죽은 변수가 남지 않았다는 뜻.
- 신규 `CS0177/CS0165` 0건 = `out`/미할당 사용 경로가 깨지지 않았다는 뜻.
- 비-SIMUL(`#else`) 빌드는 생략했다. 근거: 편집 구역(@14cf3f1 L450–510, L635–660)에 `#if` 가 **0개**임을 착수 전 실측(`grep -c '#if'` → 0).

### 원형 유지한 것 + 근거 (추출하지 않은 이유)

| 원형 유지 대상 | 근거 |
|----------------|------|
| `if (ShotParam != null)` 를 호출부에 잔류 | 조기 return 으로 바꾸면 L460 이후(AllPass 기록 / `[SEQ]` 요약 로그 / `Step = End`)가 스킵되어 동작이 바뀐다 |
| `foreach (var fai …)` 루프 본문을 추가로 쪼개지 않음 | 이번 범위 밖. 쪼개면 `faiAllPass` / `faiOverlays` / `crossZRoleImage` 의 per-FAI 수명 계약을 다시 검증해야 하는데, 지금은 실기 확인이 불가능한 시점이다 |
| `using` / `try-finally` 재구성 안 함 | 260810 round4 누수 수정(`sharedSrc` ref 2중 누수 방지)이 무효화될 수 있다 |
| `ShotParam` / `pMyContext` 를 파라미터로 승격하지 않음 | 클래스 멤버 접근이라 그대로 두면 옮긴 본문의 토큰 변경이 0건이 되고, 그래야 §1 의 바이트 동치 증명이 성립한다 |
| 크로스-Z 게이트(`ECrossZGate` switch) / Datum 게이트 2개 / `FinalizeFaiTick` 이하 집계·저장 경로 | 범위 밖(G-1). 앵커 카운트로 무접촉 확인 |

---

## ⑥ 사용자 UAT 요청 (내일 아침 실기)

정적 증거만으로는 `ref` 전달의 **실사용 결과**까지는 못 잡는다. 아래 4가지만 확인해 주시면 된다.

1. **정상 검사 1사이클** — `[SEQ] Measure 완료 — 측정 N개 (공차이탈 M개)` 의 **N / M 숫자가 리팩토링 전과 동일**한가.
   (= `measuredCount` / `nMeasNg` 의 `ref` 누락 여부 실사용 검증. **이번 작업 최대 위험 지점**)
2. **`[ALGO]` 로그** 가 리팩토링 전과 동일한 포맷·내용으로 찍히고, `[SEQ]` 요약 끝의 `알고리즘: 타입×횟수`
   집계가 "없음" 이 아닌 실제 값으로 나오는가. (= `dctAlgoUsed` 집계 보존 검증)
3. **SIMUL 이미지 경로가 무효한 SHOT** — 여전히 전 항목 NG 로 뜨고 PASS 로 새지 않는가.
   (= `allPass` 의 `ref` + `MarkAllMeasurementsNoImage` 경로 검증)
4. **일괄검사 수 회 반복** — 크로스-Z 캡처 표시/저장 동작과 메모리 증가 추이가 리팩토링 전과 동일한가.
   (= `bShotDisplayImageReplaced` 의 `ref` + `sharedSrc.Release()` 누수 수정 무접촉 검증)

---

## 리포지토리 위생 (csproj 로컬 설정 보호)

`WPF_Example/DatumMeasurement.csproj` 에 커밋하면 안 되는 로컬 설정(Debug `OutputPath=D:\Data\`,
Release `DefineConstants` 의 `SIMUL_MODE`)이 워킹트리에 떠 있었다. 저장소에 들어가면 현장 배포본이
시뮬레이션 모드로 나간다. `git add -A` / `git commit -a` 를 일절 쓰지 않고 대상 파일 1개만 경로로 스테이징했다.

| 확인 항목 | 결과 |
|-----------|------|
| Task 1 커밋 파일 수 / Task 2 커밋 파일 수 | 1 / 1 |
| 두 커밋에 `DatumMeasurement.csproj` 포함 여부 | 0건 |
| `git diff --name-only 14cf3f1 HEAD` | 1줄 (`Action_FAIMeasurement.cs`) |
| `git status --porcelain -- …csproj` | `" M"` — **여전히 unstaged 로 보존됨** (2 insertions / 2 deletions 그대로) |
| 워킹트리 dirty 집합 baseline 대비 변동 | 1줄(`.planning/quick/260818-ukh-…/` 미추적 디렉터리 추가분) |

---

## 플랜 대비 편차

### 1. [Rule 3 - 검증식 오프바이원] Task 1 verify [2] 의 `grep -cF 'bOk' == 1` 은 구조적으로 성립 불가

- **발견 시점:** Task 1 검증
- **내용:** 플랜은 `grep -cF 'bOk' $F` 가 `1` 이길 기대했다. 그런데 `bOk` 는 (a) 새 메서드 **시그니처 선언 줄**
  (`… bool bHasAnyZIndex, bool bOk,`) 과 (b) 본문의 `if (bOk) szAlgoResult = "OK";` 두 줄에 필연적으로 나타난다.
  파라미터를 `bOk` 로 이름 지은 이상 실측치는 **항상 2** 이며, 시그니처를 어떻게 줄바꿈해도 1 로 만들 수 없다.
- **조치:** 코드를 억지로 바꾸지 않았다(플랜 §G-2 가 `ok` → `bOk` 리네임을 명시하고, 하드 제약이 "검증 통과를 위해
  코드를 뜯어고치지 말라"고 지시). 대신 실제 의도인 **"본문 내 리네임이 정확히 1건"** 은 별도 앵커
  `grep -cE '^\s*if \(bOk\) szAlgoResult'` → **1** 로 확인했고, 리네임 외 변경이 0건이라는 사실은
  §1 의 바이트 동치 diff(빈 출력)와 §1-b 의 파일 전체 멀티셋 대조(삭제 1줄 = 그 리네임된 줄 자체)로 이중 증명했다.
- **코드 변경:** 없음. 검증식 해석만 정정.

그 외 편차 없음. Rule 1(버그) / Rule 2(누락 기능) / Rule 4(구조 변경) 해당 사항 0건 — 이번 작업은 의미 보존 변환이므로
어떤 자동 수정도 적용하지 않았다.

## Deferred Issues

없음.

## Known Stubs

없음.

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 존재, `private void MeasureShotFaiList(` 포함 확인
- `.planning/quick/260818-ukh-processonemeasurement-runmeasure-shot-10/260818-ukh-SUMMARY.md` — 본 문서
- 커밋 `908d7a3` — `git log` 확인됨
- 커밋 `2d067e7` — `git log` 확인됨 (현재 HEAD)
