# Quick Task 260818-ef5: Action_FAIMeasurement Run() 가독성 리팩토링 - Research

**Researched:** 2026-08-18
**Domain:** C# 7.2 / .NET Framework 4.8 — 기존 상태머신 메서드의 무회귀(behavior-preserving) 구조 리팩토링
**Confidence:** HIGH (전 항목 대상 파일 직접 grep/판독 + 빌드 실측으로 검증. 웹/외부 문서 의존 0)
**대상 파일:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (전체 1,646줄)
**대상 파일 git 상태:** clean (최신 커밋 `cb284f4 docs: Run() 상태 머신에 초보자용 단계별 설명 주석 추가`) [VERIFIED: git status/log]

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**최우선 원칙 — 기존 동작 100% 보존**
사용자 표현(반복 강조): "제일 중요한건 기존 프로그램 영향을 절대 주면 안돼". 생산 라인 검사 판정 코드이므로:
- 분기 조건, 실행 순서, side-effect 순서(조명 적용/대기 시점, Dispose 순서, MarkDatumFailed/MarkAlignFailed 호출 시점, Step 전이 조건) 전부 리팩토링 전후 완전 동일해야 함
- 순수 구조 재배치(메서드 추출, 삼항→if-else 표현 변환)만 허용. 로직/판정/타이밍 변경 절대 금지
- 사용자가 명시적으로 "여러 에이전트도 써도 돼"라고 승인 — plan-checker/verifier가 "각 case 블록의 분기 조건과 실행 순서가 리팩토링 전후 1:1 대응하는지"를 명시적 체크리스트로 확인할 것

**임시 진단 로그 정리 범위**
사용자 결정(AskUserQuestion): **완전 제거**. 대상 필드 — `[FaiTiming]` 자체 및 `light=`/`lightWait=`/`grab=`/`detect=`/`thread=`/`dbg=`/`sleep5=` 전부. 근거: 타이머 해상도 근본원인은 오늘 이미 확정 수정됨(커밋 `327cb73`/`369811c`). `Action_FAIMeasurement.cs` 안의 해당 필드만 대상 — `SystemHandler.cs`/`SequenceBase.cs`의 `[MemCacheWarmup]`/`[TimerRes]`는 이번 범위 밖(건드리지 않음).

**유지할 것**: 오늘 만든 `[SEQ]`/`[Datum]`/`[Measure]` 시퀀스 서사 로그(`LogSeqStep`/`LogSeqAlgo` 호출) — 이건 정리 대상이 아니고 사용자용 로그로 그대로 유지. tact(단계별 소요시간)는 이미 `[SEQ]` 로그 안에 포함되어 있으므로 정보 손실 없음.

**메서드 추출 단위**
사용자 원문 그대로 채택: `RunInit`/`RunMoveZ`/`RunDatumPhase`/`RunGrab`/`RunMeasure`/`RunEnd` 6개 + DatumPhase 내부 dual-image/1-image 두 분기를 각각 더 작은 헬퍼로(`TryDetectOneDatumDualImage`/`TryDetectOneDatumSingleImage` 류 이름 예시, 실제 이름은 플래너가 기존 명명 관례에 맞춰 확정).

### Claude's Discretion
- 추출된 메서드들의 정확한 시그니처(참조 전달 방식 등)와 파일 내 배치 순서
- DatumPhase 내부를 얼마나 더 잘게 쪼갤지의 구체적 경계(사용자는 "쪼갤 수 있으면 쪼갠다"로 방향만 제시)

### Canonical References (CONTEXT)
- 프로젝트 표준 컨벤션: 삼항연산자 금지, 헝가리언 표기법(bXxx/nXxx/szXxx/dXxx), C# 7.2(C# 8+ 문법 금지), Allman 스타일
- 기존 상세 WHY 주석(quick-260807, Phase 54/57/68 등)은 삭제 금지 — 추출된 메서드로 그대로 이동
- 오늘 커밋 `327cb73`, `369811c` — 타이머 버그가 이미 해결됐다는 근거

### Deferred / Out of Scope
- `SystemHandler.cs` / `SequenceBase.cs` 의 `[MemCacheWarmup]`/`[TimerRes]` 로그
</user_constraints>

---

## Summary

`Run()` 은 **L106–L697 (592줄)** 이며, 그 안의 `switch ((EStep)Step)` 는 **L107–L695**. 6개 case 중 실제 거대 블록은 두 개다 — **`case EStep.Measure` 가 268줄(코드 205줄)로 최대**, `case EStep.DatumPhase` 가 202줄(코드 153줄)로 그 다음이다. 최대 중첩 들여쓰기는 **52칸(13단계)**, `case EStep.Measure` 안의 `L518` 이다. [VERIFIED: 파일 직접 판독 + awk 집계]

이 리팩토링의 실질적 위험은 "메서드 추출" 자체가 아니라 **두 가지 지뢰**에 있다.
첫째, `//TEMP 계측` 주석이 붙은 Stopwatch 4개 중 **3개(`swDatumPhase`/`swGrabTotal`/`swMeasureTotal`) + 1개(`swMeasureExec`) 는 삭제 대상이 아니라 유지 대상**이다 — 각각 보존 대상인 `[SEQ]` 로그와 `[ALGO]` 로그가 소비하고 있다. 주석 문구만 보고 지우면 컴파일 에러(운 좋은 경우)거나 사용자 로그의 tact 필드 소실(운 나쁜 경우)이 된다.
둘째, `foreach` 루프 안의 `continue` 총 12개(DatumPhase 6개 / Measure 6개) 중 일부는 `try{}finally{}` 블록 **내부**에 있어, 추출 시 `continue → return` 으로 바꾸면 finally 의 Dispose 시점이 그대로 유지되는지 개별 확인이 필요하다.

**Primary recommendation:** case 블록 → private 메서드 추출은 **텍스트 이동(cut & paste) + `continue`→`return` + 지역변수 파라미터화**의 3가지 기계적 변환만 수행하고, 그 외 어떤 "정리"(중복 null 체크 제거, 변수명 개선, 비대칭 카운터 통일, 방어 코드 추가)도 하지 않는다. 특히 아래 §5 에 정리한 **의도적 비대칭 4건**은 "버그처럼 보이지만 보존해야 하는 현행 동작"이다.

---

## 사실 정정 (CONTEXT.md 대비)

플래너는 아래 3건을 CONTEXT 기재값 대신 사용할 것. [VERIFIED: 파일 직접 판독]

| # | CONTEXT.md 기재 | 실제 (검증됨) | 영향 |
|---|-----------------|---------------|------|
| C-1 | `Run()` 은 약 98~676줄, 580줄 | **L106–L697, 592줄** (L98–105 는 메서드 위 주석) | 편집 범위 지정 오류 방지 |
| C-2 | `case EStep.DatumPhase:` 가 가장 큼 (100줄 이상) / 약 154~330줄 | **`case EStep.Measure:` 가 최대 (L418–685, 268줄)**. DatumPhase 는 L145–346, 202줄 | 추출 우선순위·작업량 산정 |
| C-3 | "이 파일은 이미 Allman" | **K&R 우세**: 메서드 39개 중 K&R 31개 / Allman 8개(전부 Phase 68 크로스-Z 계열: L1235, 1256, 1286, 1318, 1432, 1462, 1497, 1537) | 신규 메서드 brace 스타일 결정 근거 → §6 |

---

## 1. Run() 구조 지도 (정확한 줄번호)

[VERIFIED: `cat -n` 직접 판독, awk 라인 집계]

| 구간 | 선행 주석 | 본문 줄번호 | 총 줄수 | 코드 줄수 | 블록 `{}` 유무 | 종료 방식 |
|------|-----------|-------------|---------|-----------|----------------|-----------|
| 메서드 헤더 주석 | L98–105 | — | 8 | 0 | — | (초보자용 개요 주석 — 이동/보존 대상) |
| `public override ActionContext Run() {` | — | **L106** | — | — | — | — |
| `switch ((EStep)Step) {` | — | **L107** | — | — | — | 닫힘 **L695** |
| `case EStep.Init:` | L108 | **L109–113** | 5 | 4 | 없음 | `break` L113 |
| `case EStep.MoveZ:` | L115–116 | **L117–132** | 16 | 11 | 없음 | `break` L132 |
| `case EStep.DatumPhase: {` | L134–144 | **L145–346** | 202 | 153 | **있음** (L145 `{` → L346 `}`) | `break` L345 |
| `case EStep.Grab:` | L348–349 | **L350–413** | 64 | 53 | 없음 | `break` L413 |
| `case EStep.Measure: {` | L415–417 | **L418–685** | **268** | **205** | **있음** (L418 `{` → L685 `}`) | `break` L684 |
| `case EStep.End:` | L687–688 | **L689–694** | 6 | 6 | 없음 | `break` L694 |
| `return Context;` | — | **L696** | — | — | — | — |
| `}` (메서드 끝) | — | **L697** | — | — | — | — |

**최대 중첩:** 들여쓰기 52칸 = 13단계, `L518` (`MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2);`) [VERIFIED: awk 최대 들여쓰기 산출]

**`Step` 의 정체 (중요):** `ActionBase.cs:22` — `public int Step { get => Context.CurrentStep; protected set => Context.CurrentStep = value; }`. 필드가 아니라 **`Context.CurrentStep` 프록시 프로퍼티**다. 즉 `Step = (int)EStep.Grab;` 는 `Context` 객체의 상태를 바꾸는 side-effect다. 추출된 인스턴스 메서드 안에서 그대로 대입해도 동작은 동일하지만, **지역 변수에 캐싱해서 조작하면 안 된다.** [VERIFIED: `WPF_Example/Sequence/Action/ActionBase.cs:22`]

**`FinishAction`:** `ActionBase.cs:81` — `public virtual void FinishAction(EContextResult result) { Context.Result = result; Context.State = EContextState.Finish; }`. `Context` 참조 자체는 바꾸지 않으므로 L696 `return Context;` 는 추출과 무관하게 안전. [VERIFIED]

---

## 2. `case EStep.DatumPhase:` 실행 순서 전개 (L145–346)

### 2-A. 진입부 (L145–155) — 항상 실행

| 순서 | 줄 | 동작 |
|------|-----|------|
| 1 | L149 | `swDatumPhase = Stopwatch.StartNew()` — **유지 대상** (§4 참조) |
| 2 | L150 | `int nDatumOk = 0, nDatumFail = 0, nDatumCached = 0` |
| 3 | L151–153 | `parentSeq = ShotParam?.Parent as InspectionSequence` (if-else로 이미 전개되어 있음) |
| 4 | L154–155 | `LogSeqStep("DatumPhase", "기준점 검출 — 등록 Datum {0}개")` ← 삼항 1개 (L155) |

### 2-B. `if (parentSeq != null && parentSeq.DatumConfigs.Count > 0)` (L156–319)

**`foreach (var datum in parentSeq.DatumConfigs)` — L159–310. per-datum 실행 순서:**

| 순서 | 줄 | 동작 | 비고 |
|------|-----|------|------|
| a | L160 | `if (datum == null) continue;` | 조명 미적용 |
| b | L168–169 | `bIsCrossZDatum` 계산 (`AlgorithmTypeEnum == VerticalTwoHorizontalDualImage && !(ZIndexA==-1 && ZIndexB==-1)`) | |
| c | L170–173 | `!bIsCrossZDatum && HasCachedDatumTransform(name)` → `nDatumCached++` → `continue` | **⚠ 조명 적용 전에 빠져나감** (quick-260807 최적화 핵심) |
| d | L176–178 | `ApplyDatumLights(datum)` + `msApplyDatumLights` 계측 | **계측 삭제, 호출 유지** |
| e | L182–184 | `LightHandler.Handle.WaitForPendingWrites()` + `msWaitForPendingWrites` 계측 | **계측 삭제, 호출 유지** |
| f | L185 | 분기: `AlgorithmTypeEnum == VerticalTwoHorizontalDualImage` ? DUAL : 1-IMAGE | |

**[DUAL 분기] L185–250**

| 순서 | 줄 | 동작 |
|------|-----|------|
| f1 | L188–196 | `IsDatumZIndexMisconfigured` 게이트 → true 시: Error 로그 → `RuntimeDetectFailed=true` → `MarkDatumFailed` → **`continue`**. **`nDatumFail` 미증가** |
| f2 | L202–204 | `imgH=null, imgV=null` 선언 → `try {` |
| f3 | L205–219 | `TryGrabOrLoadDualDatumImages(...)` false 시 두 갈래:<br>· `bPending==true` → **`continue`** (L207). Mark 호출 없음, 실패 아님<br>· `bPending==false` → Error 로그 → `LastFindSucceeded=false` → `RuntimeDetectFailed=true` → `MarkDatumFailed` → **`continue`** (L218). **`nDatumFail` 미증가** |
| f4a | L223–234 | `IsPatternAlignEnabled==true`: `ResolveDatumModelPath` → `TryComposeAlign(datum, imgH, imgV, modelPath, out alignErr)` 실패 시 Error 로그 → `RuntimeDetectFailed=true` → `MarkAlignFailed`. **`nDatumOk`/`nDatumFail` 미증가, `LogSeqAlgo` 미호출** |
| f4b | L235–246 | `else`: `TryRunSingleDatum(datum, imgH, imgV, out derr)` 실패 시 Error 로그 → `RuntimeDetectFailed=true` → `MarkDatumFailed`. **동일하게 카운터/LogSeqAlgo 없음** |
| f5 | L247–250 | `finally { imgH?.Dispose() (try/catch swallow); imgV?.Dispose() (try/catch swallow); }` |

> **DUAL 분기에는 `[FaiTiming] DatumDetail` 로그도, `LogSeqAlgo` 도, `nDatumOk/nDatumFail` 증가도 전혀 없다.** 이건 현행 사실이며 보존 대상이다.

**[1-IMAGE 분기] L251–309**

| 순서 | 줄 | 동작 |
|------|-----|------|
| g1 | L252–254 | `swDatumGrab` 시작 → `img = GrabOrLoadDatumImage(datum)` → `msDatumGrab` 계측 |
| g2 | L255–263 | `img == null` → Error 로그 → `LastFindSucceeded=false` → `RuntimeDetectFailed=true` → `MarkDatumFailed` → **`continue`**. **`nDatumFail` 미증가** (⚠ g4 실패와 비대칭) |
| g3 | L266–267 | `swDatumDetect` 시작 → `try {` |
| g4a | L268–284 | `IsPatternAlignEnabled==true`: `TryComposeAlign(datum, img, modelPath, out alignErr)`<br>· 실패 → Error 로그, `RuntimeDetectFailed=true`, `MarkAlignFailed`, **`nDatumFail++`**<br>· 성공 → **`nDatumOk++`**<br>· 이후 **성공/실패 무관 항상** `LogSeqAlgo("Datum", name, "TryComposeAlign(패턴매칭)")` (L284) |
| g4b | L285–300 | `else`: `TryRunSingleDatum(datum, img, null, out derr)`<br>· 실패 → Error 로그, `RuntimeDetectFailed=true`, `MarkDatumFailed`, **`nDatumFail++`**<br>· 성공 → **`nDatumOk++`**<br>· 이후 항상 `LogSeqAlgo("Datum", name, "TryRunSingleDatum/" + AlgorithmTypeEnum)` (L300) |
| g5 | L302–305 | **`[FaiTiming] stage=DatumDetail` 로그 — 삭제 대상** |
| g6 | L306–308 | `finally { img.Dispose(); }` — **try/catch 없이 직접 Dispose** (DUAL 의 f5 와 스타일 다름, 보존) |

**루프 종료 후 (여전히 `Count > 0` 블록 내부) — L311–318**

| 순서 | 줄 | 동작 |
|------|-----|------|
| h | L313–318 | `if (ShotParam != null) { parentSeq.ApplyShotLights(ShotParam.ZIndex); LightHandler.Handle.WaitForPendingWrites(); }` |

> **⚠ 이 조명 복귀 블록은 `DatumConfigs.Count > 0` 일 때만 실행된다.** Datum 이 하나도 없으면 `ApplyShotLights` 가 호출되지 않는다. 이 조건 위치를 옮기면 조명 상태가 바뀐다.

### 2-C. 블록 밖 후처리 (L320–345) — DatumConfigs 비어 있어도 항상 실행

| 순서 | 줄 | 동작 |
|------|-----|------|
| i | L328–329 | `int nCurZ = 0; bool bDatumOnly = false;` |
| j | L330–333 | `if (parentSeq != null) { nCurZ = GetExecutionZIndex(); bDatumOnly = ShouldSkipMeasurementAfterDatumPhase(nCurZ); }` |
| k | L335–336 | `LogSeqStep("DatumPhase", "완료 — 검출성공 {0} / 실패 {1} / 캐시재사용 {2} ({3:F2}초)")` — **`swDatumPhase` 소비 지점** |
| l | L337–339 | **`[FaiTiming] stage=Datum` 로그 — 삭제 대상** (삼항 1개 포함, 함께 소멸) |
| m | L340–344 | `if (bDatumOnly) Step = End; else Step = Grab;` (이미 if-else) |
| n | L345 | `break;` |

### 2-D. DatumPhase 공유 지역변수 → 추출 시그니처 권고

| 변수 | 선언 | 사용처 | 추출 시 전달 방식 |
|------|------|--------|-------------------|
| `swDatumPhase` | L149 | L336 ([SEQ]) | `RunDatumPhase()` 내부에 그대로 둔다 (하위 헬퍼에 전달 불필요) |
| `nDatumOk` / `nDatumFail` | L150 | L279/281/295/297 (1-IMAGE 만), L336 | 1-IMAGE 헬퍼에만 **`ref int`** |
| `nDatumCached` | L150 | L171, L336 | per-datum 헬퍼에 **`ref int`** |
| `parentSeq` | L151–153 | 루프 전역 + L314, L331–332 | 하위 헬퍼에 **값 전달** (`InspectionSequence parentSeq`) |
| `datum` | foreach 변수 | 루프 본문 전체 | 값 전달 |
| `nCurZ` | L328 | L332 인자로만 사용 | `RunDatumPhase()` 내부 유지 |
| `bDatumOnly` | L329 | L340 | `RunDatumPhase()` 내부 유지 |

**권고 시그니처 (3단 분해):**

```csharp
private void RunDatumPhase()                                                   // case 본문 전체
private void ProcessOneDatum(DatumConfig datum, InspectionSequence parentSeq,
                             ref int nDatumOk, ref int nDatumFail, ref int nDatumCached)   // foreach 본문
private void ProcessDatumDualImage(DatumConfig datum, InspectionSequence parentSeq)        // ref 없음 ← 비대칭이 시그니처로 문서화됨
private void ProcessDatumSingleImage(DatumConfig datum, InspectionSequence parentSeq,
                             ref int nDatumOk, ref int nDatumFail)
```

> `ProcessDatumDualImage` 에 카운터 `ref` 를 **일부러 넣지 않는 것**이 §5 D-1 비대칭을 구조적으로 고정하는 가장 좋은 방법이다. 나중에 누가 "카운터를 빼먹었네" 하고 추가하는 사고를 시그니처가 막는다.

---

## 3. 삼항연산자 전수 목록 (파일 전체)

[VERIFIED: `grep -nE '\?[^?:]*:'` + `??`/`?.` 제외 교차검증. 파일 내 `?.`(null 조건 연산자) 사용 **0건**]

**총 12개 표현 / 11개 줄.** 이 중 2개(L304, L339)는 `[FaiTiming]` 로그와 함께 통째로 삭제되므로 **실제 변환 대상은 10개**.

| # | 줄 | 표현 | 부작용 | 처리 |
|---|-----|------|--------|------|
| T-1 | L121 | `ShotParam != null ? ShotParam.ZIndex : -1` | 없음 (프로퍼티 읽기) | if-else 로컬 호이스팅 |
| T-2 | L121 | `ShotParam != null ? ShotParam.DelayMs : 0` | 없음 | if-else 로컬 호이스팅 |
| T-3 | L155 | `parentSeq != null ? parentSeq.DatumConfigs.Count : 0` | 없음 | if-else 로컬 호이스팅 |
| T-4 | L304 | `datum.DatumName != null ? datum.DatumName : "?"` | 없음 | **삭제** ([FaiTiming] DatumDetail 소멸) |
| T-5 | L339 | `ShotParam != null ? ShotParam.ShotName : "?"` | 없음 | **삭제** ([FaiTiming] stage=Datum 소멸) |
| T-6 | L420 | `ShotParam != null && ShotParam.FAIList != null ? ShotParam.FAIList.Count : 0` | 없음 | if-else 로컬 호이스팅 |
| T-7 | L453 | `ShotParam != null ? ShotParam.GetEffectivePixelResolution() : 1.0` | **없음** — `CameraSlaveParam.cs:110` 은 `PixelResolution * factor` 순수 계산 [VERIFIED] | if-else. **호출 횟수 1회 유지** |
| T-8 | L587 | `ShotParam != null ? ShotParam.ShotName : "?"` | 없음 | if-else 로컬 호이스팅 (`[ALGO]` 로그는 **보존**) |
| T-9 | L589 | `ok ? "OK" : "FAIL"` | 없음 | if-else 로컬 호이스팅 |
| T-10 | L677 | `allPass ? "OK" : "NG"` | 없음 | if-else 로컬 호이스팅 |
| T-11 | L678 | `sbAlgo.Length > 0 ? sbAlgo.ToString() : "없음"` | 없음 | if-else 로컬 호이스팅 |
| T-12 | L1303 | `parentSeq2 != null ? parentSeq2.GetExecutionZIndex() : UNSET_ZINDEX` | **없음** — `InspectionSequence.cs:1179` 은 `return ParseCurrentZIndex();` [VERIFIED] | if-else. `MarkMeasurementCrossZIncomplete` 내부 (Run() 밖) |

**전 항목 순수 값 계산 — if-else 전환 안전.** 단 T-7/T-12 는 메서드 호출을 포함하므로 **호출 횟수가 정확히 1회(또는 0회)로 유지되는 형태**로만 전개할 것 (조건 양쪽에서 호출하는 형태로 바뀌면 안 됨).

**⚠ 오탐 주의 (변환 금지):** `L75`, `L76`, `L92`, `L580` 의 `"?"` 는 **문자열 리터럴**이지 삼항이 아니다. `L1208` 의 `?:` 는 **주석 텍스트**다. 순진한 `grep '?'` 로는 16줄이 잡히므로 위 표만 근거로 삼을 것.

**이 파일의 삼항→if-else 선례:** `L1208` — `//260702 hbk 기존 삼항(?:) → if-else 로 전개(동치 유지, 신규 삼항 미도입)`. 이번 작업은 260702 에 이미 시작된 정리의 연장이다. [VERIFIED: 파일 판독]

---

## 4. 임시 진단 로그 정리 — 삭제/보존 판정표 ⭐핵심

### 4-A. `[FaiTiming]` 로그 호출 4곳 (전부 삭제)

[VERIFIED: `grep -n "FaiTiming"`]

| # | 줄 범위 | stage | 삭제할 부수 주석 |
|---|---------|-------|------------------|
| F-1 | **L302–305** | `DatumDetail` (light/lightWait/grab/detect/thread/dbg) | L302 |
| F-2 | **L337–339** | `Datum` (total) | L337 |
| F-3 | **L405–407** | `Grab` (acquire/displayCopy/total) | L352 (부분 — §4-C 참조) |
| F-4 | **L679–682** | `Measure` (measuredCount/measureExec/saveQueueEnqueue/total/thread/dbg/sleep5) | L660, L664, L682 인라인 |

`[FaiTiming]` 문자열은 프로젝트 내 다른 곳에서 **파싱/소비되지 않는다** — 유일한 외부 언급은 `SequenceBase.cs:344` 주석뿐. [VERIFIED: 프로젝트 전체 grep]

### 4-B. 지역변수 삭제/보존 판정 ⭐가장 실수하기 쉬운 부분

| 변수 | 선언 줄 | 소비처 | 판정 | 근거 |
|------|---------|--------|------|------|
| `swDatumPhase` | L149 | L336 **[SEQ]**, L339 [FaiTiming] | 🟢 **보존** | `LogSeqStep("DatumPhase", …{3:F2}초)` 의 tact 소스 |
| `nDatumOk`/`nDatumFail`/`nDatumCached` | L150 | L336 [SEQ] | 🟢 **보존** | [SEQ] 요약 전용, [FaiTiming] 무관 |
| `swDatumLight` | L176 | L178 만 | 🔴 삭제 | |
| `msApplyDatumLights` | L178 | L304 만 | 🔴 삭제 | |
| `swDatumLightWait` | L182 | L184 만 | 🔴 삭제 | |
| `msWaitForPendingWrites` | L184 | L304 만 | 🔴 삭제 | |
| `swDatumGrab` | L252 | L254 만 | 🔴 삭제 | |
| `msDatumGrab` | L254 | L304 만 | 🔴 삭제 | |
| `swDatumDetect` | L266 | L304 만 | 🔴 삭제 | 바로 뒤 `try {` (L267) 은 유지 |
| `swGrabTotal` | L353 | L407 [FaiTiming], **L410 [SEQ]** | 🟢 **보존** | `LogSeqStep("Grab", …{0:F2}초)` 의 tact 소스 |
| `msAcquire` | L354 (대입 L377) | L407 만 | 🔴 삭제 (선언+대입) | |
| `swAcquire` | L359 | L377 만 | 🔴 삭제 | |
| `msDisplayCopy` | L355 (대입 L402) | L407 만 | 🔴 삭제 (선언+대입) | |
| `swDisplayCopy` | L396 | L402 만 | 🔴 삭제 | L395 Dispose 와 L397 if 사이 |
| `swMeasureTotal` | L424 | L681 [FaiTiming], **L677 [SEQ]** | 🟢 **보존** | `LogSeqStep("Measure", …{3:F2}초)` 의 tact 소스 |
| `msMeasureExec` | L425 (누적 L577) | L681 만 | 🔴 삭제 (선언+누적) | |
| `msSaveQueue` | L426 (누적 L612, L625) | L681 만 | 🔴 삭제 (선언+누적 2곳) | |
| **`swMeasureExec`** | **L568** | L577 [FaiTiming], **L589 `[ALGO]`** | 🟢 **보존 ⚠ 최대 함정** | `//TEMP 계측` 인라인 주석이 붙어 있지만 **보존 대상 `[ALGO]` 로그(L586–589)가 소비**. 지우면 L589 컴파일 에러 |
| `swSaveQueue` | L610 | L612 만 | 🔴 삭제 | |
| `swSaveQueueCrossZ` | L623 | L625 만 | 🔴 삭제 | |
| `szMeasureShotName` | L661–663 | L681 만 | 🔴 삭제 (3줄) | |
| `dLastSleepMs` | L665 | L682 만 | 🔴 삭제 | |
| `measureSeq` | L666–668 | L668 만 (`dLastSleepMs` 전용) | 🔴 삭제 | |
| `dctAlgoUsed` | L421 | L584–585 누적, L672–674 [SEQ] | 🟢 **보존** | |
| `nMeasNg` | L422 | L592, L677 [SEQ] | 🟢 **보존** | |
| `sbAlgo` | L671–675 | L678 [SEQ] | 🟢 **보존** | |

### 4-C. `//TEMP 계측` 주석의 함정 — 문구만 보고 지우면 안 되는 3곳

`//TEMP 계측` 주석 **18곳** 중 아래 4곳은 **바로 아래에 보존 대상 Stopwatch 선언이 있다.** 주석만 삭제하면 정체불명의 Stopwatch 가 남으므로, **삭제가 아니라 "[SEQ]/[ALGO] tact 측정용" 으로 문구 교체**해야 한다. [VERIFIED: `grep -n "TEMP"`]

| 주석 줄 | 바로 아래 선언 | 조치 |
|---------|----------------|------|
| L146–148 | L149 `swDatumPhase` (보존) | 주석 문구를 "[SEQ] DatumPhase tact 측정" 으로 교체 |
| L352 | L353 `swGrabTotal` (보존) + L354–355 (삭제) | 주석 교체 + 삭제 변수 언급 제거 |
| L423 | L424 `swMeasureTotal` (보존) + L425–426 (삭제) | 주석 교체 + 삭제 변수 언급 제거 |
| L568 (인라인) | L568 `swMeasureExec` (보존) | 인라인 주석을 "[ALGO] 로그용 실행시간" 으로 교체 |

순수 삭제 가능한 `//TEMP` 주석: L176, L178, L182, L184, L252, L254, L266, L302, L337, L610, L623, L660, L664, L682(인라인).

### 4-D. 삭제 후 파급 검토

| 항목 | 결론 | 근거 |
|------|------|------|
| `using System.Diagnostics;` (L3) | **유지** | 보존 Stopwatch 4개(`swDatumPhase`/`swGrabTotal`/`swMeasureTotal`/`swMeasureExec`)가 계속 사용 |
| `using System.Text;` (L5) | **유지** | `sbAlgo` (StringBuilder) [SEQ] 로그용 |
| `System.Diagnostics.Debugger.IsAttached` | 파일에서 완전 소멸 (L305, L682 만) | 별도 조치 불필요 (완전한정명이라 using 무관) |
| `System.Threading.Thread.CurrentThread.ManagedThreadId` | 파일에서 완전 소멸 (L305, L682 만) | `System.Threading.Thread.Sleep`(L127) 은 유지 |
| `SequenceBase.LastSleepMs` | **건드리지 말 것** | `SequenceBase.cs:344–345` `public double LastSleepMs { get; private set; }`. 이 파일이 유일한 읽기 소비자였으나, **public auto-property 라 미사용 컴파일 경고가 발생하지 않음.** CONTEXT 상 SequenceBase.cs 는 범위 밖 [VERIFIED: grep 전체 + 빌드 실측] |
| `pCamera` 필드 (L43, 대입 L62) | **건드리지 말 것** | 읽기 소비자 0건인 기존 dead field. `[FaiTiming]` 과 무관 — 이번 스코프 밖 (scope creep 금지) |

---

## 5. 메서드 추출 시 제어흐름 위험 지점 ⭐핵심

### 5-A. `continue` 12곳 — 전부 `foreach` 다음 반복이지 case 탈출이 아님

[VERIFIED: Run() 범위 내 `continue`/`break` 전수 grep]

| # | 줄 | 소속 루프 | `try` 내부? | 추출 시 변환 | 특이사항 |
|---|-----|-----------|-------------|--------------|----------|
| K-1 | L160 | `foreach datum` | 아니오 | `return` | |
| K-2 | L172 | `foreach datum` | 아니오 | `return` | **`ApplyDatumLights` 이전** — 조명 미적용으로 빠짐 |
| K-3 | L195 | `foreach datum` | 아니오 | `return` | ZIndex 오설정 게이트 |
| K-4 | **L207** | `foreach datum` | **예** (L204 try / L247 finally) | `return` | `bPending` — Mark 호출 **없음**. finally 의 imgH/imgV Dispose 는 둘 다 null 이지만 실행됨 |
| K-5 | **L218** | `foreach datum` | **예** | `return` | finally Dispose 실행됨 |
| K-6 | L262 | `foreach datum` | 아니오 (try 는 L267 부터) | `return` | `img==null` — Dispose 불필요 |
| K-7 | L476 | `foreach meas` | 아니오 (바깥 L449 try 안이긴 함) | `return` | DatumFailed 게이트 |
| K-8 | L486 | `foreach meas` | 동일 | `return` | DatumRef 미해결 게이트 |
| K-9 | L500 | `foreach meas` | 동일 | `return` | ZIndex 오설정 |
| K-10 | L522 | `foreach meas` | 동일 | `return` | `!bRelevant` |
| K-11 | L531 | `foreach meas` | 동일 | `return` | `!bCaptureOk` |
| K-12 | L558 | `foreach meas` | 동일 | `return` | `!bCompleted` |

**규칙:** `foreach` **본문 전체**를 하나의 메서드로 추출할 때만 `continue → return` 이 1:1 동치다. **본문 일부만** 추출하면 `continue` 가 메서드 경계를 넘을 수 없어 컴파일 에러가 나거나, `bool` 반환값으로 우회하다 흐름이 미묘하게 바뀐다. **부분 추출 금지, 루프 본문 단위 추출만 허용.**

`break` 6곳(L113, L132, L345, L413, L684, L694)은 전부 `switch` 탈출이며 **추출 대상 메서드 안으로 들어가면 안 된다** — 호출부 `case` 에 남긴다.

### 5-B. 인스턴스 멤버 접근 — 추출해도 동작 동일하나 캐싱 금지

| 멤버 | 정체 | 추출 시 주의 |
|------|------|--------------|
| `Step` | `ActionBase:22` — **`Context.CurrentStep` 프록시 프로퍼티** | 인스턴스 메서드 안에서 대입 OK. **지역 변수 캐싱 금지** |
| `Context` | `ActionBase:24` 프로퍼티 | `return Context;` 는 L696 에 그대로 남긴다 |
| `pMyContext` | 필드 (L42, ctor 에서 대입) | L395/398/400/635/636/657/658/659/691 에서 사용. 파라미터화 불필요 |
| `ShotParam` | `Param as ShotConfig` **계산 프로퍼티** (L52) | 매 접근마다 `as` 캐스트 발생. 기존 코드가 반복 접근하고 있으므로 **로컬로 묶는 "최적화"도 하지 말 것** (동작은 같으나 diff 노이즈 + 리뷰 부담) |
| `FinishAction(...)` | `ActionBase:81` public virtual | `RunEnd()` 에서 호출 OK |

### 5-C. 리소스 수명 계약 — 절대 경계를 넘기면 안 되는 블록

| 대상 | 줄 | 계약 | 위반 시 |
|------|-----|------|---------|
| `using (var image = ShotParam.GetImage())` | L437–655 | `GetImage()` 는 **호출자 소유의 clone 을 반환** (`ShotConfig.cs:392` — `_image.CopyImage()`) [VERIFIED] | `image` 를 추출 메서드에 넘길 때 **callee 가 Dispose 하면 안 됨**. `using` 은 `RunMeasure()` 안에 남긴다 |
| `sharedSrc` (SharedHImage) | 생성 L442–443(**try 밖**) / `try` L449 / `finally` L645–647 `Release()` | 260810 round4 fix 로 **의도적으로 try 범위를 sharedSrc 생성 직후까지 넓힌 것** (L444–448 주석) | try 시작점을 되돌리면 refcount 누수 재발 |
| `crossZRoleImage` | 대입 L538 / `try` L608 / `finally` L641–643 `Dispose()+null` | per-FAI 소유 | 추출 시 **`ref HImage`** 필요 |
| `crossZSharedSrc` | L620–628 (중첩 try/finally) | 즉시 Release | 통째로 이동 |
| `imgH`/`imgV` | L202 / try L204 / finally L247–250 (각각 try-catch swallow) | | 통째로 이동 |
| `img` | L253 / try L267 / finally L306–308 (try-catch **없이** Dispose) | DUAL 과 스타일 다름 | 통째로 이동, 스타일 통일 금지 |

**⚠ 들여쓰기 함정:** `L449 try {` 의 본문(L450–644)은 **재들여쓰기가 안 되어 있어** `try` 와 같은 32칸 열에 있고, 닫는 `} finally {` 는 L645 다. 브레이스 매칭을 눈으로 하면 `foreach fai`(L460) 가 try 밖에 있는 것처럼 보인다 — **실제로는 try 안이다.** 실제 구조:

```
L436  if (ShotParam != null) {
L437      using (var image = ShotParam.GetImage()) {
L438          if (image != null) {
L441              capSaver / L442-443 sharedSrc      ← try 밖 (의도적)
L449              try {
L451                  datumSnapshot / L453 pixRes / L459 szSharedOriginPath
L460                  foreach (var fai in ShotParam.FAIList) {
L467                      foreach (var meas in fai.Measurements) { ... L607 }
L608                      try { ... } finally { crossZRoleImage 정리 }   ← L641-643
L644                  }
L645              } finally { sharedSrc.Release(); }                     ← L645-647
L648          } else { allPass = false; MarkAllMeasurementsNoImage(ref measuredCount); }   ← L648-654
L655      }
L656  }
```

### 5-D. 보존해야 할 "버그처럼 보이는" 의도적 비대칭 4건 ⭐

| # | 내용 | 줄 | 왜 고치면 안 되나 |
|---|------|-----|-------------------|
| D-1 | **DUAL 분기는 `nDatumOk`/`nDatumFail` 을 전혀 증가시키지 않는다** (1-IMAGE 만 증가) | L185–250 vs L268–298 | 카운터는 `[SEQ]` 로그 출력값. 통일하면 사용자가 보는 로그 숫자가 바뀜 = 동작 변경 |
| D-2 | **1-IMAGE 의 `img == null` 실패(L262)도 `nDatumFail` 미증가** — g4 실패만 증가 | L255–263 vs L279/295 | 동일 |
| D-3 | **`LogSeqAlgo` 는 1-IMAGE 분기에만 있고 DUAL 에는 없다** | L284, L300 | 동일 |
| D-4 | **조명 복귀(`ApplyShotLights`+`WaitForPendingWrites`)는 `DatumConfigs.Count > 0` 일 때만** | L313–318 (L156 블록 내부) | 조명 상태 = 물리 side-effect. 블록 밖으로 옮기면 Datum 0개 Shot 의 조명이 바뀜 |
| D-5 | `Step = (int)EStep.Measure;` (L412) 는 `if (ShotParam != null && !ShotParam.HasImage)` **블록 밖** — 이미지 유무 무관 항상 전이 | L350–413 | 안으로 넣으면 이미 이미지가 있는 Shot 이 Grab 에 갇힘 |
| D-6 | `pMyContext.AllPass/MeasuredCount/InspectionOverlays` 대입(L657–659)은 `if (ShotParam != null)` **밖** — ShotParam null 이면 `AllPass=true` 로 확정 | L656 vs L657 | 방어적으로 안으로 넣으면 판정이 바뀜 |
| D-7 | `case EStep.Measure` 의 로컬명은 `parentSeq2` (DatumPhase 는 `parentSeq`) | L427 vs L151 | 이름 변경은 동작 무영향이지만 **diff 노이즈 최소화 원칙상 그대로 유지 권장** |

### 5-E. 조건부 컴파일(`#if SIMUL_MODE`) 이동 위험 ⭐

Run() 안에 `#if SIMUL_MODE` 블록 2곳: **L122–129**(MoveZ 의 `Thread.Sleep(DelayMs)`), **L360–376**(Grab 의 `LoadShotInspectionImage` vs 실기 grab). 추출 시 `#if`/`#else`/`#endif` 를 **한 메서드 안에 통째로** 넣어야 하며 경계를 가로지르면 컴파일 자체가 깨진다.

**더 위험한 것:** 이 PC 의 빌드 구성은 `Debug|x64` 와 `Release|x64` **둘 다** `SIMUL_MODE` 를 정의한다 → **`#else`(실기 HW) 분기는 평소 빌드에서 아예 컴파일되지 않는다.** [VERIFIED: `DatumMeasurement.csproj` L64, L74 — 단, L74 의 `;SIMUL_MODE` 는 **미커밋 작업트리 변경**, HEAD 는 `TRACE` 뿐]

→ 검증 단계에서 **비-SIMUL 컴파일 체크를 반드시 별도로 돌려야 한다** (§검증 아키텍처 참조).

---

## 6. 이 파일의 실제 컨벤션 (근거 기반)

### 6-A. Brace 스타일 — **K&R 우세** (CONTEXT 기재와 다름)

[VERIFIED: 39개 멤버 선언 전수 확인]

- **K&R (`{` 같은 줄) 31개**: `Run`(L106), `LogSeqStep`(L73), `LoadShotInspectionImage`(L704), `GrabOrLoadDatumImage`(L719), `TryGrabOrLoadDualDatumImages`(L813), `BuildDatumCaptureSnapshot`(L1058), `TryExecuteMeasurement`(L1394), `AggregateFaiResult`(L1595) 등
- **Allman (`{` 다음 줄) 8개**: L1235, L1256, L1286, L1318, L1432, L1462, L1497, L1537 — **전부 Phase 68(260722) 크로스-Z 계열**

**권고:** 신규 추출 메서드는 **K&R** 을 쓴다. 근거 — (a) 파일 내 79% 가 K&R, (b) 추출 대상 코드가 전부 `Run()`(K&R) 에서 나오므로 본문 재들여쓰기 없이 그대로 옮길 수 있어 **diff 가 "이동"으로만 남고 리뷰 부담이 최소화**된다. CLAUDE.md 도 "Use the style of the file/module you are editing" 을 명시.

### 6-B. 메서드 네이밍 패턴 (파일 내 실증)

| 접두 | 반환 | 실제 사례 | 의미 |
|------|------|-----------|------|
| `Try…` | **`bool` + `out`** | `TryGrabOrLoadDualDatumImages`(out×3), `TryExecuteMeasurement`(out×3), `TryLoadStaticDualDatumImages`(out×2), `TryExecuteCrossZMeasurement`(out×3) | 성공/실패 + 결과 반출. **`bool` 반환이 없으면 `Try` 를 쓰지 않는다** |
| `Process…` | **`void`** (+ `out` 가능) | `ProcessCrossZCaptureTick`(void, out×4) | 상태 전이/부수효과 수행 |
| `Is…` | `bool` | `IsViewerUpdateSkipped`, `IsZIndexMisconfigured`, `IsDatumZIndexMisconfigured`, `IsCrossZDatumBothStored` | 순수 술어 |
| `Mark…` | `void` | `MarkMeasurementDatumSkipped`, `MarkMeasurementCrossZIncomplete`, `MarkAllMeasurementsNoImage(ref int)` | 결과 객체에 실패 표시 |
| `Build…` / `Resolve…` | 값 | `BuildCrossZDatumKey`, `ResolveDatumTransform`, `ResolveCrossZDatumRoleKeys(out×2)` | 값 산출 |
| `Load…`/`GrabOrLoad…` | `HImage` (실패 시 `null`) | `LoadShotInspectionImage`, `GrabOrLoadDatumImage` | |
| `Apply…`/`Queue…`/`Aggregate…` | `void` | `ApplyOverlaySuffixAndAccumulate`, `QueueFaiCapture`, `AggregateFaiResult` | |

**→ CONTEXT 의 예시 이름 `TryDetectOneDatumDualImage` 는 이 파일 관례에 어긋난다** (bool 반환이 아니므로). 관례 준수 대안: **`ProcessDatumDualImage` / `ProcessDatumSingleImage` / `ProcessOneDatum` / `ProcessOneMeasurement`**. `Run…` 접두(사용자 확정)는 신규 카테고리지만 기존 이름과 충돌 없고 `case EStep.X` ↔ `RunX()` 1:1 대응이 명확해 그대로 채택 권장.

**`ref` 누적자 선례:** `MarkAllMeasurementsNoImage(ref int measuredCount)` (L1623) — 카운터를 `ref` 로 넘기는 방식은 이미 이 파일의 확립된 패턴. 신규 튜플/새 클래스 도입 없이 `ref` 로 가는 근거.

### 6-C. 헝가리언 표기 — **혼재, 신규 코드만 적용**

[VERIFIED: Run() 내 지역변수 전수 확인]

- **접두 있음(신규, 260618~260818)**: `bIsCrossZDatum`, `bDatumOnly`, `bSkipViewer`, `bHasAnyZIndex`, `bShotDisplayImageReplaced`, `nDatumOk/nDatumFail/nDatumCached`, `nCurZ`, `nMeasNg`, `szShotRoleId`, `szAlgoType`, `szSharedOriginPath`, `dLastSleepMs`, `dctAlgoUsed`, `sbAlgo`, `swXxx`, `msXxx`
- **접두 없음(레거시)**: `parentSeq`, `parentSeq2`, `allPass`, `measuredCount`, `overlayAcc`, `image`, `img`, `imgH`, `imgV`, `pixRes`, `transform`, `ok`, `fai`, `meas`, `faiAllPass`, `faiOverlays`, `capSaver`, `sharedSrc`, `datumSnapshot`, `crossZRoleImage`, `finishResult`

**권고:** **새로 만드는 파라미터/지역변수만 헝가리언 적용. 기존 지역변수 이름은 절대 바꾸지 않는다.** 근거 — 이름 변경은 동작 무영향이지만 diff 를 "이동"에서 "수정"으로 바꿔 plan-checker/verifier 의 1:1 대조를 어렵게 만든다. 무회귀 최우선 원칙과 직접 충돌.

- 필드 접두 `p`: `pMyContext`, `pCamera` (레거시 포인터형 접두) — 신규 필드 만들 일 없음
- 상수: `UNSET_ZINDEX`, `CROSS_Z_ROLE_SUFFIX_A`, `CROSS_Z_DATUM_KEY_PREFIX` (UPPER_SNAKE), 예외 1건 `chAlgoMul` (L71, private const char)

---

## 7. Don't Hand-Roll / 하지 말 것

| 유혹 | 하지 말 것 | 대신 |
|------|-----------|------|
| 중복 null 체크 정리 (L453 `ShotParam != null` 는 L436 에서 이미 보장됨) | 제거하지 말 것 | 그대로 if-else 로만 전개 |
| 카운터 비대칭 통일 (§5-D D-1~D-3) | 절대 금지 | 현행 그대로 |
| `try/catch` 스타일 통일 (DUAL 은 swallow, 1-IMAGE 는 raw Dispose) | 금지 | 현행 그대로 |
| `parentSeq2` → `parentSeq` 리네임 | 권장하지 않음 | 현행 그대로 |
| 새 튜플/`ValueTuple`/레코드형 반환 | 금지 (C# 7.2 + 파일 관례) | `ref`/`out` 파라미터 |
| C# 8+ 문법 (switch expression, `??=`, nullable ref, `using` declaration) | 금지 | C# 7.2 문법만 |
| `pCamera` dead field 제거, `LastSleepMs` 제거 | 금지 (scope creep) | 그대로 둠 |
| `[ALGO]` 로그 제거 | 금지 | `[FaiTiming]` 만 제거 |
| 초보자용/WHY 주석 삭제 | 금지 (CONTEXT 명시) | 추출된 메서드로 함께 이동 |

---

## 8. 검증 아키텍처 (Validation Architecture)

> `.planning/config.json` 의 `workflow.nyquist_validation: true` [VERIFIED]. 단 이 프로젝트에는 **자동화 테스트 프레임워크가 존재하지 않는다** (xUnit/NUnit/MSTest 프로젝트 없음, `Test/` 의 Python 은 독립 mock 스크립트). 따라서 검증은 **컴파일 검증 + 정적 1:1 대조 + 사용자 UAT** 3단으로 구성한다.

### 8-A. 빌드 명령 (실측 검증 완료)

**⚠ 이 PC 의 `Debug|x64` `OutputPath` 는 미커밋 변경으로 `D:\Data\` 이다** (HEAD 는 `bin\x64\Debug\`). 앱이 `D:\Data\` 에서 실행 중이면 빌드가 파일 잠김으로 실패한다 → **프로세스 종료 금지 규칙**에 따라 반드시 스크래치 `OutputPath` 로 컴파일만 검증할 것. [VERIFIED: `git diff -- WPF_Example/DatumMeasurement.csproj`]

**① SIMUL 경로 컴파일 (기본):**
```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 \
  -p:OutputPath='<스크래치경로>\' -t:Build -v:minimal -nologo
```
**실측 baseline: 성공, 경고 12줄 (CS0618×10 + CS0162×2).** [VERIFIED: 이 세션에서 직접 실행]

**② 비-SIMUL(실기 HW `#else` 분기) 컴파일 — 이번 작업에 필수:**
```bash
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 \
  -p:OutputPath='<스크래치경로2>\' -p:DefineConstants="TRACE%3BDEBUG" \
  -t:Rebuild -v:minimal -nologo
```
**실측 baseline: 성공, 경고 10줄 (CS0618×10, CS0162 없음).** [VERIFIED: 이 세션에서 직접 실행]

> ⚠ **②를 돌린 뒤에는 반드시 ①을 `-t:Rebuild` 로 다시 돌려 `obj/x64/Debug/` 중간산출물을 SIMUL_MODE 로 원복할 것.** (classic csproj 는 DefineConstants 변경을 증분빌드에서 추적하지 못해, 사용자의 다음 빌드가 비-SIMUL 바이너리를 재사용할 위험이 있다. 이 세션에서 원복까지 수행 완료 — 12경고 확인.)
>
> ⚠ **`//p:` 스위치 금지** — 경로 값에 `/` 가 섞이면 Git Bash 가 변환하지 못해 `MSBUILD : error MSB1001` 이 난다. 반드시 `-p:` 를 쓸 것. 세미콜론은 `%3B` 로 이스케이프. [VERIFIED: 실패 재현 후 해결]

**❌ `Release|AnyCPU` 를 비-SIMUL 검증에 쓰지 말 것** — `AllowUnsafeBlocks` 가 없어 `MilCamera.cs(401): error CS0227` 로 **기존부터 빌드 실패**한다. [VERIFIED: 실행 후 실패 확인]

### 8-B. Requirements → 검증 매핑

| Req | 행위 | 검증 유형 | 명령/방법 |
|-----|------|-----------|-----------|
| R1 | 6개 case → private 메서드 추출 | 컴파일 | 8-A ① + ② |
| R2 | 삼항 10개 → if-else | 정적 grep | `grep -nE '\?[^?:]*:' <file> \| grep -vE '\?\?\|\?\.'` → **§3 표의 T-1~T-3, T-6~T-12 가 0건이 되고, 남는 건 L1208 주석 + `"?"` 리터럴 4곳뿐** |
| R3 | `[FaiTiming]` 완전 제거 | 정적 grep | `grep -c "FaiTiming" <file>` → **0** |
| R4 | `[SEQ]`/`[ALGO]` 로그 보존 | 정적 grep | `grep -c "LogSeqStep\|LogSeqAlgo" <file>` → **선행 8건 유지** (L93, 120, 154, 335, 409, 419, 676 호출 + 정의 2) / `grep -c "\[ALGO\]" <file>` → **1** |
| R5 | 동작 100% 보존 | **정적 1:1 대조 (수동)** | 8-C 체크리스트 |
| R6 | 실기 무회귀 | 사용자 UAT | 앱 실행 → 1 사이클 검사 → `[SEQ]` 로그의 tact 3종(DatumPhase/Grab/Measure 초)이 여전히 출력되는지 + 판정 결과 동일 |

### 8-C. 리팩토링 전후 1:1 대조 체크리스트 (plan-checker / verifier 용)

각 항목은 **before/after 코드를 나란히 놓고** 확인한다. "빌드 됐으니 OK" 는 근거로 인정하지 않는다.

- [ ] **호출 순서**: `ApplyDatumLights` → `WaitForPendingWrites` → (분기) → 검출 → `ApplyShotLights` → `WaitForPendingWrites` 순서 불변
- [ ] **조명 복귀 조건**: `ApplyShotLights` 가 여전히 `DatumConfigs.Count > 0` **AND** `ShotParam != null` 두 조건 아래에만 있는가 (D-4)
- [ ] **캐시 스킵 위치**: `HasCachedDatumTransform` 스킵이 여전히 `ApplyDatumLights` **이전**인가 (K-2)
- [ ] **`Mark*` 호출 지점 6곳**: L194/217/244(DatumFailed), L233/278(AlignFailed), L261/294(DatumFailed) 전부 동일 조건 아래 동일 순서로 남아 있는가
- [ ] **카운터 비대칭 4건 유지** (D-1/D-2/D-3): DUAL 분기에 `nDatumOk`/`nDatumFail` 이 **추가되지 않았는가**
- [ ] **`LogSeqAlgo` 위치**: 여전히 1-IMAGE 분기에만, 성공/실패 무관 항상 호출되는가 (L284, L300)
- [ ] **Step 전이 조건**: Init→MoveZ / MoveZ→DatumPhase / DatumPhase→(bDatumOnly? End : Grab) / Grab→Measure(무조건) / Measure→End / End→FinishAction — 6개 전이 전부 동일 (D-5)
- [ ] **Dispose 순서**: `imgH`→`imgV`(f5), `img`(g6), `crossZSharedSrc`(L627), `crossZRoleImage`(L642), `sharedSrc.Release()`(L646), `image`(using L655), `pMyContext.ResultHalconImage`(L395/L635) — 전부 동일 finally 안에 동일 순서
- [ ] **`try` 시작점**: `L449 try {` 가 여전히 `sharedSrc` 생성 **직후**인가 (260810 round4 fix 보존)
- [ ] **`using (var image = ...)`** 가 `RunMeasure()` 안에 남아 있고, 추출 메서드가 `image` 를 Dispose 하지 않는가
- [ ] **`#if SIMUL_MODE` 블록 2개**가 각각 한 메서드 안에 온전히 들어 있는가
- [ ] **`pMyContext.AllPass/MeasuredCount/InspectionOverlays` 대입**이 여전히 `if (ShotParam != null)` **밖**인가 (D-6)
- [ ] **보존 Stopwatch 4개** (`swDatumPhase`/`swGrabTotal`/`swMeasureTotal`/`swMeasureExec`) 가 살아 있고 각각의 소비 로그(L336/L410/L677/L589 상당)가 동일 포맷으로 출력되는가
- [ ] **WHY 주석 이동**: quick-260807(L136–141), Phase 54(L157–158, L264–265), Phase 57(L220–222), Phase 68(L186–187, L197–201, L321–327), 260810-egx(L390, L629–631), 260729-hwb(L433–434, L463–465, L505–512, L535–537, L549–553, L614–616), 260811(L378–380), 초보자용(L98–105, L108, L115–116, L142–144, L348–349, L415–417, L687–688) — **삭제 0건**

### 8-D. Wave 0 갭

없음 — 신규 테스트 인프라 도입 대상 아님(프로젝트 정책상 자동 테스트 프레임워크 미보유). 검증은 8-A 빌드 2종 + 8-C 정적 대조 + 사용자 UAT 로 충족.

---

## 9. 권고 추출 설계 (Claude's Discretion 영역)

### 9-A. 최종 메서드 목록

```csharp
// case 1:1 (사용자 확정 이름)
private void RunInit();                 // L109-112  → 4줄
private void RunMoveZ();                // L117-131  → 11줄
private void RunDatumPhase();           // L145-345  → §2 전체
private void RunGrab();                 // L350-412  → 53줄
private void RunMeasure();              // L418-684  → §5-C 스캐폴딩 유지
private void RunEnd();                  // L689-693  → 5줄

// DatumPhase 하위 (관례: Process… = void)
private void ProcessOneDatum(DatumConfig datum, InspectionSequence parentSeq,
                             ref int nDatumOk, ref int nDatumFail, ref int nDatumCached);
private void ProcessDatumDualImage(DatumConfig datum, InspectionSequence parentSeq);
private void ProcessDatumSingleImage(DatumConfig datum, InspectionSequence parentSeq,
                             ref int nDatumOk, ref int nDatumFail);

// Measure 하위 (선택 — 9-B 참조)
private void ProcessOneMeasurement(MeasurementBase meas, InspectionSequence parentSeq2,
                             HImage image, double pixRes,
                             ref HImage crossZRoleImage, ref bool faiAllPass,
                             ref int measuredCount, ref int nMeasNg,
                             List<EdgeInspectionOverlay> overlayAcc,
                             List<EdgeInspectionOverlay> faiOverlays,
                             Dictionary<string, int> dctAlgoUsed);
private void FinalizeFaiTick(FAIConfig fai, bool faiAllPass,
                             List<EdgeInspectionOverlay> faiOverlays, SharedHImage sharedSrc,
                             List<DatumCaptureOverlay> datumSnapshot, string szSharedOriginPath,
                             InspectionSequence parentSeq2,
                             ref HImage crossZRoleImage, ref bool bShotDisplayImageReplaced,
                             ref bool allPass);
```

`Run()` 최종 형태 (약 25줄):
```csharp
public override ActionContext Run() {
    switch ((EStep)Step) {
        case EStep.Init:       RunInit();       break;
        case EStep.MoveZ:      RunMoveZ();      break;
        case EStep.DatumPhase: RunDatumPhase(); break;
        case EStep.Grab:       RunGrab();       break;
        case EStep.Measure:    RunMeasure();    break;
        case EStep.End:        RunEnd();        break;
    }
    return Context;
}
```
> `case` 블록의 `{ }`(L145, L418)는 추출과 함께 사라지며, `EStep.End` 의 `EContextResult finishResult`(L690) 도 `RunEnd()` 지역변수가 되므로 switch 스코프 오염이 해소된다.

### 9-B. `ProcessOneMeasurement` 파라미터 11개 — 두 대안 비교

| 옵션 | 내용 | 장점 | 단점 |
|------|------|------|------|
| **A (권장)** | 위와 같이 `ref`×4 포함 11 파라미터 | 새 타입 0개. 순수 기계적 변환. `MarkAllMeasurementsNoImage(ref int)` 선례와 일관 | 파라미터 목록이 길다 |
| B | private sealed class `MeasureShotAccumulator` 도입 (allPass/measuredCount/nMeasNg/overlayAcc/dctAlgoUsed/bShotDisplayImageReplaced 보유) | 파라미터 4~5개로 축소, 가독성 우수 | **신규 타입 = 신규 코드**. 필드 초기화/전달 누락 시 조용한 판정 오류 가능 |

무회귀 최우선 원칙상 **A 권장.** 사용자가 가독성을 더 원하면 B 로 바꾸되, 그 경우 8-C 체크리스트에 "누적자 6필드가 원래 지역변수와 1:1 대응하고 초기값이 동일한가" 항목을 추가할 것.

### 9-C. 파일 내 배치 순서 권고

`Run()`(L106) 바로 아래에 `RunInit` → `RunMoveZ` → `RunDatumPhase` → `ProcessOneDatum` → `ProcessDatumDualImage` → `ProcessDatumSingleImage` → `RunGrab` → `RunMeasure` → `ProcessOneMeasurement` → `FinalizeFaiTick` → `RunEnd` 순으로 배치하고, 기존 L699 이후 헬퍼군(`LoadShotInspectionImage` 등)은 **한 줄도 이동시키지 않는다** (diff 최소화).

---

## 10. Assumptions Log

| # | 주장 | 섹션 | 틀렸을 때 위험 |
|---|------|------|----------------|
| A1 | 사용자 UAT 시나리오("1 사이클 검사 후 [SEQ] tact 3종 확인")가 적절한 수용 기준이다 | §8-B R6 | 실기 검증 범위 부족 — 사용자와 UAT 항목 합의 필요 |
| A2 | `case` 블록의 `{ }` 제거로 인한 switch 스코프 변화가 다른 case 와 이름 충돌을 만들지 않는다 (`finishResult` 만 switch 스코프에 존재하며 `RunEnd()` 로 이동) | §9-A | 충돌 시 컴파일 에러 — 즉시 발견됨(무해) |
| A3 | 미커밋 `csproj` 변경(OutputPath=`D:\Data\`, Release\|x64 SIMUL_MODE)은 사용자의 의도된 로컬 설정이며 이번 작업에서 건드리지 않는다 | §8-A | 되돌리면 사용자의 실행 환경이 깨짐 → **절대 커밋/수정 금지** |

**나머지 모든 사실 주장은 `[VERIFIED]`** — 파일 직접 판독, 프로젝트 전체 grep, 또는 이 세션의 MSBuild 실측으로 확인됨.

---

## 11. Open Questions

1. **`[ALGO]` 로그(L586–589)는 보존이 맞는가?**
   - 아는 것: CONTEXT 는 `[SEQ]`/`[Datum]`/`[Measure]` 보존, `[FaiTiming]` 제거만 명시. `[ALGO]` 는 `//260818 hbk` 로 오늘 추가됐고 `//TEMP` 마킹이 **없다**.
   - 불명확: 사용자가 `[ALGO]` 를 "임시"로 인지하는지.
   - 권고: **보존.** TEMP 마킹이 없고, `LogSeqAlgo`(보존 확정)의 측정 버전에 해당한다. 제거하면 `swMeasureExec` 도 함께 삭제 가능해지지만, 판단 근거가 없으므로 보존이 안전. 플래너가 불확실하면 사용자에게 1문장 확인.

2. **`ProcessOneMeasurement` 분해 깊이**
   - `case EStep.Measure` 는 268줄로 최대 블록이지만, 리소스 수명 계약(§5-C)이 가장 촘촘한 구간이기도 하다.
   - 권고: 9-A 의 2단 분해(ProcessOneMeasurement + FinalizeFaiTick)까지만. 그 이상 잘게 쪼개면 `sharedSrc`/`crossZRoleImage` refcount 계약이 여러 메서드에 흩어져 오히려 위험.

---

## Sources

### Primary (HIGH — 이 세션 직접 검증)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 전 1,646줄 판독 + 전수 grep (줄번호·구조·변수 사용처)
- `WPF_Example/Sequence/Action/ActionBase.cs:20-24, 71-83` — `Step`/`Context`/`FinishAction` 정의
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:251, 377-395` — `HasImage`/`SetImage`/`GetImage` 소유권 계약
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs:110-114` — `GetEffectivePixelResolution` 순수성
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:1179-1194` — `GetExecutionZIndex`/`IsProtocolDrivenCycle` 순수성
- `WPF_Example/Sequence/Sequence/SequenceBase.cs:340-345` — `LastSleepMs` 정의(public auto-property)
- `WPF_Example/DatumMeasurement.csproj:37-81` + `git diff` — 구성별 `DefineConstants`/`OutputPath`/`AllowUnsafeBlocks`
- **MSBuild 실측 3회** (Debug\|x64 SIMUL 12경고 성공 / DefineConstants override 비-SIMUL 10경고 성공 / Release\|AnyCPU CS0227 실패) — 이 세션 직접 실행
- `git status` / `git log` — 대상 파일 clean, 최신 커밋 `cb284f4`

### Secondary (MEDIUM)
- `.planning/quick/260818-ef5-.../260818-ef5-CONTEXT.md` — 사용자 결정 원문
- `CLAUDE.md` — 프로젝트 컨벤션(C# 7.2, 파일 스타일 추종, 에러 처리 패턴)
- `.planning/config.json` — `nyquist_validation: true`, `plan_check: true`, `verifier: true`

### Memory (CITED — 이 세션 재검증 완료)
- `reference_build_warning_baseline_12.md` — "경고 12줄 baseline, `//p:` + 경로 조합은 MSB1001" → **둘 다 이 세션에서 실측 재현·확인**
- `feedback_never_kill_process_for_build_lock.md` — 스크래치 OutDir 컴파일 검증 원칙 → §8-A 반영
- `feedback_no_ternary_if_else.md` — 삼항 금지, 헝가리언, 회귀 0

---

## Metadata

**Confidence breakdown:**
- Run() 구조/줄번호: **HIGH** — 파일 직접 판독 + awk 집계
- 진단 로그 삭제/보존 판정(§4): **HIGH** — 변수별 사용처 전수 grep
- 제어흐름 위험 지점(§5): **HIGH** — `continue`/`break`/try-finally 전수 확인
- 컨벤션(§6): **HIGH** — 39개 멤버 선언 전수 확인
- 빌드 검증(§8-A): **HIGH** — 3회 실측
- `[ALGO]` 보존 여부(§11 Q1): **MEDIUM** — 사용자 의도 미확인

**Research date:** 2026-08-18
**Valid until:** 대상 파일이 다시 커밋될 때까지 (줄번호가 전부 무효화됨). 플래너는 착수 전 `git log -1 -- <file>` 이 `cb284f4` 인지 확인할 것.
