---
phase: quick-260818-ukh
verified: 2026-08-18T00:00:00Z
status: human_needed
score: 17/17 must-haves verified (정적)
overrides_applied: 0
verifier_independent_rerun: true
base: 14cf3f1
commits: [908d7a3, 2d067e7]
human_verification:
  - test: "정상 검사 1사이클 실행 후 [SEQ] Measure 완료 로그의 '측정 N개 (공차이탈 M개)' 숫자 확인"
    expected: "리팩토링 전과 동일한 N / M. 특히 N=0 또는 M=0 으로 떨어지면 안 됨"
    why_human: "ref 전달의 실사용 결과는 실행해야만 최종 확인 가능(정적으로는 시그니처/호출부 양쪽 ref 확인 완료 → 위험 매우 낮음)"
  - test: "[SEQ] Measure 완료 로그 끝의 '알고리즘: 타입×횟수' 집계 확인"
    expected: "'없음' 이 아니라 실제 타입×횟수가 표시됨"
    why_human: "dctAlgoUsed 집계가 헬퍼 경유로도 호출자에 반영되는지 실사용 확인"
  - test: "SIMUL 이미지 경로가 무효한 SHOT 1건 실행"
    expected: "전 항목 NG. PASS 로 새지 않음"
    why_human: "allPass=false / MarkAllMeasurementsNoImage 경로(else 분기)는 정적 실행 불가"
  - test: "일괄검사 수 회 반복 후 메모리 증가 추이"
    expected: "리팩토링 전과 동일. 지속 증가 없음"
    why_human: "sharedSrc.Release() try-finally 누수 동작은 런타임 관찰 필요"
---

# Quick 260818-ukh 검증 리포트 — ProcessOneMeasurement / RunMeasure Extract Method

**목표:** `Action_FAIMeasurement.cs` 에서 순수 Extract Method 2건. **동작 100% 보존.**
**Base:** `14cf3f1` → **HEAD:** `2d067e7`
**검증 방식:** SUMMARY 주장을 인용하지 않고, 모든 명령을 검증자가 **직접 재실행**했다.
**최종 판정:** **정적 회귀 0. 순수 Extract Method 2건 외 어떤 변화도 없음.**

---

## 0. 한눈에 보는 결론 (내일 아침 이것만 봐도 됨)

| 질문 | 답 | 근거 |
|------|----|------|
| 옮겨간 코드가 원본과 글자 하나라도 다른가? | **아니오** | diff 2건 모두 빈 출력 (§2) |
| 파일 어딘가에서 코드가 사라졌나? | **아니오 — 삭제된 실행코드 0줄** | 파일 전체 멀티셋 대조 (§3) |
| `ref` 를 빠뜨린 게 있나? | **아니오, 4/4 모두 정상** | 시그니처·호출부 양쪽 확인 (§4) |
| `using` / `try-finally` 가 깨졌나? | **아니오** | 48줄 통째 이동, 상대위치 불변 (§5) |
| 조기 return 이 들어갔나? | **아니오** | `if (ShotParam != null)` 호출부 잔류 (§6) |
| 로그 시점·포맷이 바뀌었나? | **아니오** | Stopwatch 객체 전달, 포맷 리터럴 바이트 동일 (§7) |
| 범위 밖(크로스-Z/Datum 게이트)을 건드렸나? | **아니오** | diff 에 미등장 + 앵커 카운트 동일 (§8) |
| 빌드는? | **성공, 경고 12줄 baseline 동일, 신규 경고 0** | §9 |
| csproj 로컬 설정(`SIMUL_MODE`)이 커밋됐나? | **아니오 — unstaged 그대로** | §10 |

---

## 1. 커밋 범위 — 파일 1개, 훅 4개

```
$ git log --oneline 14cf3f1..HEAD
2d067e7 refactor(260818-ukh): RunMeasure Shot 본문을 MeasureShotFaiList 로 추출 …
908d7a3 refactor(260818-ukh): 알고리즘 로그 조립부를 LogAndTallyAlgorithm 로 추출 …

$ git diff --name-only 14cf3f1 HEAD
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs      ← 1개뿐
```

커밋별 hunk 위치 (범위 확장이 없었다는 1차 증거):

| 커밋 | hunk | 위치 | 의미 |
|------|------|------|------|
| `908d7a3` | `@@ -638,24 +638,7 @@` | ALGO 로그 블록 | 18줄 잘라내고 호출 1줄 |
| `908d7a3` | `@@ -675,6 +658,34 @@` | ProcessOneMeasurement 직후 | 새 메서드 추가 |
| `2d067e7` | `@@ -455,54 +455,7 @@` | RunMeasure Shot 블록 | 48줄 잘라내고 호출 1줄 |
| `2d067e7` | `@@ -525,6 +478,68 @@` | RunMeasure 직후 | 새 메서드 추가 |

**총 4개 hunk. "잘라내기 2 + 붙여넣기 2" 이외의 hunk가 존재하지 않는다.**

신규 메서드는 각각 선언 1개 / 호출 1개뿐:

```
458:  MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced);
488:  private void MeasureShotFaiList(InspectionSequence parentSeq2,
656:  LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);
682:  private void LogAndTallyAlgorithm(MeasurementBase meas, bool bHasAnyZIndex, bool bOk,
```

---

## 2. 바이트 동치 증명 (검증자 직접 재실행)

### ① LogAndTallyAlgorithm — 18줄

```
$ diff <(git show 14cf3f1:$F | sed -n '641,658p') \
       <(sed -n '/측정 알고리즘을 탔는지/,/swMeasureExec.ElapsedMilliseconds);/p' $F | sed 's/\bbOk\b/ok/g')
diff1 exit=0          ← 출력 한 줄도 없음
anchor uniq: 1        ← '측정 알고리즘을 탔는지' 파일 내 유일 (앵커 유효)
range len: 18         ← 범위 길이 정확히 18줄
```

앵커 함정 회피 확인: 지시대로 `측정 알고리즘을 탔는지`(긴 형태)를 썼고 파일 내 1건이므로 290줄 오탐이 발생하지 않았다.

### ② MeasureShotFaiList — 48줄, **strict 4칸 dedent만** (공백 정규화 없음)

```
$ L=493   # 이동 후 using 시작 줄
$ diff <(git show 14cf3f1:$F | sed -n '458,505p' | sed 's/^    //') <(sed -n "493,540p" $F)
diff2(strict) exit=0  ← 출력 한 줄도 없음
```

> 이건 SUMMARY 보다 강한 검증이다. 선행 공백을 **전부** 지우는 느슨한 정규화가 아니라
> **정확히 4칸만** 줄인 상태에서 일치했다는 뜻 = 들여쓰기 구조까지 원본 그대로다.

**추출 전후 대조표**

| 추출 | 원본 위치(@14cf3f1) | 이동 후 위치 | 정규화 | diff |
|------|--------------------|--------------|--------|------|
| ① `LogAndTallyAlgorithm` | L641–658 (18줄) | L684–701 | `bOk`→`ok` 1건 | **빈 출력** |
| ② `MeasureShotFaiList` | L458–505 (48줄) | L493–540 | 4칸 dedent | **빈 출력** |

---

## 3. 파일 전체 멀티셋 대조 — "다른 데서 뭐가 지워졌을 가능성" 차단

부분 diff 만으로는 파일 다른 곳의 삭제를 못 잡는다. 1721줄 vs 1747줄 전체를 정렬해 대조했다.

```
$ diff <(git show 14cf3f1:$F | sed 's/^[[:space:]]*//' | sort) <(sed 's/^[[:space:]]*//' $F | sort)
deleted (<): 1
added   (>): 27
```

**삭제된 줄 — 전 파일 통틀어 딱 1줄:**

```
< if (ok) szAlgoResult = "OK";
```

이 줄은 바로 아래 `> if (bOk) szAlgoResult = "OK";` 로 대체된 **파라미터 리네임 그 줄 자체**다.

**추가된 27줄 내역 (검증자 실측 분류):**

| 분류 | 줄 수 |
|------|-------|
| 신규 설명 주석 | 13 |
| 신규 메서드 시그니처 | 7 |
| 호출부 | 2 |
| 빈 줄 | 2 |
| 닫는 `}` | 2 |
| `if (bOk) szAlgoResult = "OK";` (리네임 결과) | 1 |
| **합** | **27** |

순증 = 27 − 1 = **26** = 1747 − 1721 ✔ (수치 정합)

> **결론: 사라진 실행 코드는 0줄.** 이동한 66줄(18+48)은 삭제·추가 어느 쪽에도 나타나지 않았다
> = 멀티셋이 완전히 보존됐다 = 문장 유실·중복·순서변경이 없다.

---

## 4. `ref` 전수 검증 — 이 작업 최대 위험 (독립 열거)

### 4-1. 검증자가 직접 열거한 "블록 밖에서 온 변수" 전수

`MeasureShotFaiList` 본문(L493–540)이 참조하는 이름을 전부 뽑아 분류했다:

| 이름 | 출처 | 시그니처 반영 |
|------|------|---------------|
| `image` `capSaver` `sharedSrc` `datumSnapshot` `pixRes` `szSharedOriginPath` `fai` `faiAllPass` `faiOverlays` `crossZRoleImage` `meas` | **블록 내부 선언** | 불필요 ✔ |
| `ShotParam` | 클래스 프로퍼티 (L64) | 멤버 접근 ✔ |
| `SystemHandler.Handle` | static | ✔ |
| `BuildDatumCaptureSnapshot` `QueueSharedShotOrigin` `ProcessOneMeasurement` `FinalizeFaiTick` `MarkAllMeasurementsNoImage` | 같은 클래스 인스턴스 메서드 | ✔ |
| `parentSeq2` | RunMeasure 지역 (L448) | **파라미터** ✔ |
| `overlayAcc` | RunMeasure 지역 (L453) | **파라미터** ✔ |
| `dctAlgoUsed` | RunMeasure 지역 (L444) | **파라미터** ✔ |
| `allPass` | RunMeasure 지역 (L451) | **`ref bool`** ✔ |
| `measuredCount` | RunMeasure 지역 (L452) | **`ref int`** ✔ |
| `nMeasNg` | RunMeasure 지역 (L445) | **`ref int`** ✔ |
| `bShotDisplayImageReplaced` | RunMeasure 지역 (L456) | **`ref bool`** ✔ |

**누락 0건.** RunMeasure 의 나머지 지역변수(`nFaiCount`, `swMeasureTotal`)는 이동 블록에서 쓰이지 않으며 RunMeasure 에 남아 각각 L443 / L476 에서 계속 사용된다 → 죽은 변수 없음(CS0219 0건과 일치).

`LogAndTallyAlgorithm` 본문이 참조하는 외부 이름: `meas`(param) / `bHasAnyZIndex`(param) / `bOk`(param) / `dctAlgoUsed`(param) / `swMeasureExec`(param) / `ShotParam`(멤버) / `Logging`·`ELogType`(static). **누락 0건.**

### 4-2. 시그니처 ↔ 호출부 1:1 대조 (실제 코드 인용)

```csharp
// L488–492  시그니처
private void MeasureShotFaiList(InspectionSequence parentSeq2,
                                List<EdgeInspectionOverlay> overlayAcc,
                                Dictionary<string, int> dctAlgoUsed,
                                ref bool allPass, ref int measuredCount,
                                ref int nMeasNg, ref bool bShotDisplayImageReplaced) {

// L457–459  호출부
if (ShotParam != null) {
    MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced);
}
```

값형 4개 `ref` — **시그니처 4개 / 호출부 4개, 이름과 순서까지 일치.**
참조형 3개(`parentSeq2` / `overlayAcc` / `dctAlgoUsed`) — 블록 안에서 **재대입이 없으므로**(멀티셋 diff 가 본문 무변경을 보장) 값 전달이 정확한 선택.

### 4-3. ⚠ 위험 성격 정정 (SUMMARY 표현 보정)

SUMMARY 는 "`ref` 를 빠뜨려도 컴파일은 통과한다"고만 썼는데, 정확히는 이렇다:

- 시그니처에만 `ref` 있고 호출부에 없다 → **컴파일 에러 CS1620** (컴파일러가 잡음)
- **양쪽 다 `ref` 를 빠뜨린 경우에만** 조용히 통과한다 — 본문이 그 값 파라미터를 다시 `ref` 로
  `ProcessOneMeasurement` 에 넘길 수 있기 때문. 이때 호출자 카운터는 0 으로 남는다.

→ **본 검증에서 양쪽 다 `ref` 임을 직접 확인했으므로 이 위험은 정적으로 완전히 닫혔다.**
(그럼에도 §human_verification 1·3 을 남긴 이유는 실사용 이중 확인 목적이지, 미해결 위험이 있어서가 아니다.)

### 4-4. 이름 가림(shadowing) 위험 점검

파라미터 7개와 같은 이름의 **클래스 필드가 존재하면** 파라미터를 빠뜨려도 조용히 필드에 바인딩될 수 있다.
클래스 멤버 전수 확인 결과 해당 이름의 필드는 **0개**다(`AllPass` / `MeasuredCount` 는 PascalCase 이며 `pMyContext` 소속의 다른 것). 가림 위험 없음.

### 4-5. `dctAlgoUsed` 집계 생존

```
grep -cF 'dctAlgoUsed[szAlgoType]'   before=2  after=2  OK
```

```csharp
// L690–691 (헬퍼 안, 원본 그대로)
if (dctAlgoUsed.ContainsKey(szAlgoType)) dctAlgoUsed[szAlgoType]++;
else dctAlgoUsed[szAlgoType] = 1;
```
Dictionary 는 참조형이라 값 전달로도 RunMeasure 의 인스턴스가 갱신되고, 그 값을 L465–467 의
`foreach (var kv in dctAlgoUsed)` → L475 `[SEQ] … │ 알고리즘: {4}` 요약이 그대로 소비한다. **Shot 요약 로그는 계속 채워진다.**

---

## 5. `using` / `try-finally` 경계 보존

과거 실제 누수 버그(`260810 … round4 fix` = `try` 시작을 `sharedSrc` 생성 직후로 넓힌 수정) 구간이다.

| 구조 | 이동 후 줄 | @14cf3f1 줄 | `using` 기준 오프셋 | 판정 |
|------|-----------|-------------|---------------------|------|
| `using (var image = ShotParam.GetImage()) {` | 493 | 458 | 0 (기준) | ✔ |
| `//260810 … round4 fix` 주석 첫 줄 | 500 | 465 | +7 | 동일 ✔ |
| `try {` (sharedSrc 생성 **직후**) | 505 | 470 | +12 | 동일 ✔ |
| `foreach (var fai …)` | 518 | 483 | +25 | 동일 ✔ |
| `} finally { // 검사 루프 소유 ref 1 해제…` | 530 | 495 | +37 | 동일 ✔ |
| `if (sharedSrc != null) sharedSrc.Release();` | 531 | 496 | +38 | 동일 ✔ |
| `} else {` (image==null 분기) | 533 | 498 | +40 | 동일 ✔ |

- **중첩 관계:** `using` → `if(image!=null)` → `try` → `foreach` → `finally`. 원본과 동일.
- **`sharedSrc.Release()` 는 여전히 `finally` 안**에 있다 (L530–532).
- **`using` 이 쪼개지지 않고 통째로** 새 메서드 안으로 들어갔다 (여는 줄 493 / 닫는 줄 540, 메서드 닫는 `}` 는 541).
- 파일 전역 `try {` 12건 / `} finally {` 6건 — **before/after 동일** (경계 추가·삭제 0건).
- §2 의 strict diff 가 48줄 전체를 통과했으므로 이 중첩 보존은 **기계적으로 보장**된다.

---

## 6. 조기 return 미도입 (BLOCKER 항목)

```
grep -cE 'if \(ShotParam == null\) return;'   → 0
grep -cE '^\s*if \(ShotParam != null\) \{$'   before=4  after=4  OK
```

호출부 실제 코드 (L457–478) — `ShotParam == null` 이어도 아래가 전부 실행된다:

| 실행되어야 할 것 | 현재 줄 |
|------------------|---------|
| `pMyContext.AllPass = allPass;` / `MeasuredCount` / `InspectionOverlays` | 460–462 |
| `[SEQ] 완료 — 측정 N개 (공차이탈 M개) … │ 알고리즘: …` 요약 로그 | 475–477 |
| `Step = (int)EStep.End;` | **478** |

**조기 return 은 도입되지 않았다. 시퀀스가 End 로 못 넘어가는 회귀 없음.**

---

## 7. 로그 시점 · 포맷 불변

**(a) `swMeasureExec.ElapsedMilliseconds` 읽는 시점**

```
grep -cF 'swMeasureExec.ElapsedMilliseconds'                   → 1  (헬퍼 안 PrintLog 인자 위치, L701)
grep -cE '^\s*LogAndTallyAlgorithm\([^)]*ElapsedMilliseconds'   → 0  (호출부 선계산 없음)
```

호출부는 `Stopwatch` **객체 자체**를 넘긴다(L656). ms 를 미리 계산해 넘겼다면 시간값이 짧게 기록됐을 텐데 그렇지 않다.
`var swMeasureExec = Stopwatch.StartNew();` 선언도 원래 자리(L647, `ProcessOneMeasurement` 안)에 그대로 있다.

**(b) `[ALGO]` 포맷 리터럴**

```
grep -cF '"[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms"'   before=1  after=1  OK
```
`·` `→` 유니코드 포함 **한 글자도 바뀌지 않았다** (§2 의 diff 가 이 줄을 포함해 통과).
`LOG_GUIDE.md` / 화면 표시 의존 깨지지 않음.

---

## 8. 범위 밖 무접촉

diff 전체(§1)에 아래가 **한 줄도 나타나지 않았고**, 앵커 카운트도 동일하다:

| 앵커 | before | after |
|------|--------|-------|
| `case ECrossZGate.*:` (크로스-Z 게이트 5-case) | 5 | 5 |
| `case EStep.*:` | 6 | 6 |
| Datum 게이트 `IsDatumFailed` / `IsDatumRefUnresolvable` (L554 / L564) | diff 미등장 | diff 미등장 |
| `FinalizeFaiTick(fai,` 이하 집계·저장 경로 | 1 | 1 |
| 다른 모든 파일 | 변경 0 | 변경 0 |

### 불변 카운트 전후표 (검증자 재실측, 23종 전부 일치)

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
| 13 | `"[ALGO] … {5}ms"` 리터럴 | 1 | 1 |
| 14 | `case ECrossZGate.*:` | 5 | 5 |
| 15 | `case EStep.*:` | 6 | 6 |
| 16 | `if (ShotParam != null) {` | 4 | 4 |
| 17 | `try {` | 12 | 12 |
| 18 | `} finally {` | 6 | 6 |
| 19 | 주석 `capture-render-per-fai-slow) round4 fix` | 1 | 1 |
| 20 | 주석 `top-z1-measure-8sec-slow) fix` | 4 | 4 |
| 21 | 주석 `260616 hbk simul-shot-cascade` | 1 | 1 |
| 22 | 주석 `260619 hbk per-shot 보정계수` | 1 | 1 |
| 23 | 주석 `260729-hwb` | 8 | 8 |

**보존 대상 주석 5계열 삭제 0건 확인.**

---

## 9. 빌드 (검증자 직접 실행, Debug|x64 Rebuild, 스크래치 OutputPath)

```
MSBUILD EXIT=0
경고 12개
오류 0개

warning code histogram (wpftmp 이중 컴파일이라 줄 수는 ×2):
   4 warning CS0162   → 고유 2건
  20 warning CS0618   → 고유 10건
```

| 확인 항목 | 결과 |
|-----------|------|
| 빌드 성공 | ✔ EXIT=0, `DatumMeasurement.exe` 생성 |
| 경고 수 | **12개 = baseline(CS0618×10 + CS0162×2) 정확히 일치** |
| 오류 | **0개** |
| 신규 `CS0219`(미사용 지역변수) | **0** ← 추출 후 죽은 변수 안 남음 |
| 신규 `CS0168` | **0** |
| 신규 `CS0177`(out 미할당) / `CS0165`(미할당 사용) | **0** |
| 신규 `CS0206`(ref 인자 불가) | **0** |
| `Action_FAIMeasurement.cs` 관련 경고 | **0건** |
| 프로세스 강제종료 | **없음** — 스크래치 `OutputPath` 사용, `D:\Data\` 무접촉 |

경고 12건은 전부 `Sequence_Bottom.cs` / `Sequence_Top.cs` / `SequenceHandler.cs` / `VirtualCamera.cs` 로,
이번 편집 파일과 무관한 기존 baseline 이다.

---

## 10. 리포지토리 위생 — csproj 보호 (BLOCKER 항목)

```
$ git status --porcelain -- WPF_Example/DatumMeasurement.csproj
 M WPF_Example/DatumMeasurement.csproj          ← unstaged 로 그대로 ✔

$ git show --name-only --format= 908d7a3 | grep -c 'DatumMeasurement.csproj'  → 0
$ git show --name-only --format= 2d067e7 | grep -c 'DatumMeasurement.csproj'  → 0
```

**커밋된(HEAD) csproj 내용 확인 — 현장 배포 안전 확인:**

```
73:    <OutputPath>D:\Data\</OutputPath>          (Release|x64 — 원래부터 이 값)
74:    <DefineConstants>TRACE</DefineConstants>    ← SIMUL_MODE 없음 ✔ 안전
```

로컬 미커밋 변경(`Debug OutputPath=D:\Data\`, `Release DefineConstants=TRACE;SIMUL_MODE`)은
워킹트리에만 남아 있고 저장소에 들어가지 않았다. **현장 배포본이 시뮬레이션 모드로 나갈 위험 없음.**

각 커밋의 파일 목록은 정확히 `Action_FAIMeasurement.cs` 1개뿐이다.

---

## 11. 프로젝트 컨벤션

| 규칙 | 결과 |
|------|------|
| 삼항 `?:` 0건 | ✔ 정제 grep 결과 1줄, 그 1줄은 **주석**(L1307 `//260702 hbk 기존 삼항(?:) → if-else 로 전개…`) |
| C# 7.2 only | ✔ switch expression / pattern switch / record / NRT **0건**. `=> ` 카운트 before=1 after=1 (기존 `ShotParam => Param as ShotConfig` 1건, 증가 없음) |
| 헝가리언 (신규 파라미터만) | ✔ `bOk` / `bHasAnyZIndex` / `bShotDisplayImageReplaced` / `nMeasNg` / `szXxx`. 기존 이름 리네임은 `ok`→`bOk` 1건뿐(허용된 유일 예외) |
| K&R 브레이스 (신규 선언) | ✔ `private void MeasureShotFaiList(… ) {` / `private void LogAndTallyAlgorithm(… ) {` |
| 옮긴 본문 재포맷 금지 | ✔ strict 4칸 dedent diff 통과 = 재포맷 0 |
| 기존 상세 주석 삭제 0건 | ✔ 5계열 전부 카운트 동일 (§8 표 19–23) |
| 신규 주석 접두 `//260818 hbk` | ✔ |

---

## 12. 실행자 보고 편차 1건 — 검증 결과 **타당함 (plan 결함, 코드 결함 아님)**

실행자는 plan Task1 verify [2] 의 `grep -cF 'bOk' $F == 1` 이 구조적으로 성립 불가라고 보고했다.
검증자 직접 확인:

```
$ grep -nF 'bOk' $F
682:        private void LogAndTallyAlgorithm(MeasurementBase meas, bool bHasAnyZIndex, bool bOk,
696:            if (bOk) szAlgoResult = "OK";
→ 카운트 2
```

**판단: 실행자가 맞다.** 파라미터를 `bOk` 로 이름 지은 이상 (a) 시그니처 선언 줄과 (b) 본문 사용 줄에
필연적으로 2회 나타난다. 줄바꿈을 어떻게 해도 1 로 만들 수 없다. **plan 검증식의 오프바이원 결함이다.**

**중요 — 실행자가 이 수치를 맞추려고 코드를 왜곡했는지 확인:** 하지 않았다.
- 파라미터 이름은 `bOk` 그대로(plan §G-2 가 명시한 유일 허용 리네임)
- 본문은 §2 diff 로 원본과 바이트 동일
- 파일 전체 삭제 줄이 1줄(`if (ok)…`)뿐 = 이 리네임 외 어떤 변형도 없음

실제 의도였던 "본문 내 리네임 정확히 1건"은 `grep -cE '^\s*if \(bOk\) szAlgoResult'` → 1 로 충족된다.

---

## 13. Must-Have 검증표

| # | Must-Have | 판정 | 근거 |
|---|-----------|------|------|
| 1 | 18줄 → `LogAndTallyAlgorithm` 치환, `bOk` 외 바이트 동일 | ✓ | §2 diff1 exit=0 |
| 2 | `dctAlgoUsed` 집계 부수효과 보존 | ✓ | 인덱서 2건, §4-5 |
| 3 | `ElapsedMilliseconds` 읽는 시점 동일 | ✓ | §7(a), 선계산 grep 0 |
| 4 | `[ALGO]` 포맷 리터럴 무변경 | ✓ | §7(b) |
| 5 | 48줄 → `MeasureShotFaiList` 통째 이동, dedent 후 동일 | ✓ | §2 diff2(strict) exit=0 |
| 6 | 조기 return 미도입 | ✓ | §6 |
| 7 | `using` / `try-finally` 상대 위치·중첩 불변 | ✓ | §5 오프셋표 |
| 8 | 값형 4개 전부 `ref` (시그니처+호출부) | ✓ | §4-2 |
| 9 | 참조형 3개 값 전달 | ✓ | §4-1, 재대입 0 |
| 10 | 파라미터명 = 호출자 지역변수명, 토큰 변경 0 | ✓ | §2 strict diff |
| 11 | 파일 전역 앵커 카운트 불변 | ✓ | §8 표 23종 |
| 12 | 보존 주석 5계열 삭제 0건 | ✓ | §8 표 19–23 |
| 13 | 빌드 성공 + 경고 baseline 12줄, 신규 CS0219/0168 0 | ✓ | §9 |
| 14 | 삼항 0건 | ✓ | §11 |
| 15 | 범위 밖 무접촉 | ✓ | §8 |
| 16 | `Action_FAIMeasurement.cs` 외 커밋 0, csproj unstaged | ✓ | §10 |
| 17 | 파일 어디에서도 실행코드 유실 0 (추가 검증) | ✓ | §3 멀티셋, 삭제 1줄 |

**17/17 정적 검증 통과.**

---

## 14. 사람이 봐야 할 것 (내일 아침, 5분)

정적으로는 회귀 요소를 찾지 못했고 `ref` 위험도 §4 에서 닫혔다.
아래는 **미해결 위험이 아니라 실사용 이중 확인**이며, 우선순위 순이다.

### 1. 정상 검사 1사이클 — `[SEQ] Measure 완료` 숫자 (⭐ 최우선)
- **보는 곳:** 로그의 `[SEQ] 완료 — 측정 N개 (공차이탈 M개), 판정 OK/NG (…초) │ 알고리즘: …`
- **기대:** N / M 이 리팩토링 전과 동일. 특히 **N 이 0 이거나 M 이 항상 0 이면 즉시 롤백 신호**
- **왜 사람:** `measuredCount` / `nMeasNg` 의 `ref` 실사용 결과. 정적으로 확인했지만 이 값이 최종 증거다.

### 2. 같은 로그 끝의 `알고리즘:` 집계
- **기대:** `없음` 이 아니라 `EdgeToLineDistance×3, …` 같은 실제 값
- **왜 사람:** `dctAlgoUsed` 가 헬퍼 경유로도 호출자 딕셔너리를 갱신하는지 확인. `없음` 이면 값 전달이 안 먹은 것(이론상 불가하나 확정 증거).

### 3. SIMUL 이미지 경로 무효 SHOT 1건
- **기대:** 전 항목 NG. PASS 로 새지 않음
- **왜 사람:** `image == null` else 분기(`allPass = false` + `MarkAllMeasurementsNoImage`)는 정상 경로에서 실행되지 않아 정적으로만 확인됨. **불량 은폐로 직결되는 경로**라 1회는 확인 권장.

### 4. 일괄검사 수 회 반복 — 메모리 추이
- **기대:** 리팩토링 전과 동일. 지속 증가 없음
- **왜 사람:** `sharedSrc.Release()` 의 `try-finally` 누수 방지(260810 round4 fix)는 런타임 관찰이 유일한 검증법.

---

## 15. 참고 — 향후 유지보수 주의 (이번 회귀 아님, INFO)

`MeasureShotFaiList` 첫 줄이 `ShotParam.GetImage()` 인데 **null 체크는 호출부(L457)에 있다.**
지금은 호출처가 1곳뿐이라 동작이 완전히 동일하지만, 나중에 **두 번째 호출처를 추가할 때
`if (ShotParam != null)` 가드를 반드시 함께 가져가야 한다.** 안 그러면 `NullReferenceException` 이 난다.
(이번 작업에서 이 구조를 택한 것은 옳다 — 메서드 안으로 가드를 옮겼다면 조기 return 이 되어
§6 의 `Step = End` 경로가 깨졌을 것이다.)

---

_Verified: 2026-08-18 (검증자 전 명령 독립 재실행)_
_Verifier: Claude (gsd-verifier)_
