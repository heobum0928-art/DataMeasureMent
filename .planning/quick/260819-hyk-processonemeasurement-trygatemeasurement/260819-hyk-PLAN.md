---
phase: quick-260819-hyk
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [HYK-01, HYK-02, HYK-03]

must_haves:
  truths:
    - "`ProcessOneMeasurement` 이 **131줄 → 본문 20줄**(시그니처 6 + 본문 20 + 닫는 `}` 1 = **span 정확히 27줄**)로 줄고, `TryGateMeasurement`(bool) / `EvaluateCrossZGate`(bool + out 2) / `RecordMeasurementResult`(void) 3개가 신설된다. 착수 전 span 은 **130줄**(플래너 실측)."
    - "**이번 작업은 오늘 이전 3건(ukh/vih/ruh)과 달리 '순수 이동'이 아니다** — `return;`/`break;` 를 `return false;`/`return true;` 로 바꾸는 **의미 변환**이 들어간다. 검증의 핵심은 '6가지 종료 경로 × 반환값' 1:1 대응이며 아래 표가 유일한 진실 원본이다."
    - "**6-경로 매핑표 (이 작업의 유일한 실질 위험)** — ① `Misconfigured` → `return false;` ② `NotMyTick`(프로토콜/비프로토콜 두 갈래 모두) → `return false;` ③ `CaptureFailed` → `return false;` ④ `HalfPending` → `return false;` ⑤ `BothReady`(원본 `break;`) → **`return true;`** ⑥ `!bHasAnyZIndex`(원본은 `if` 블록 자체를 건너뜀) → **`return true;`** (메서드 맨 끝, `if` 블록 **밖**). 즉 **`return false` 4개 + `return true` 2개**, `break` 0개, 알몸 `return;` 0개."
    - "⑤ 를 `return false` 로 잘못 쓰면 **짝이 완성된 크로스-Z 측정이 아예 실행되지 않는** 치명적 회귀, ① 을 `return true` 로 잘못 쓰면 **설정오류 항목이 조용히 정상 측정을 시도**하는 회귀다. **두 경우 다 빌드는 통과한다** → `PIN1`~`PIN6` 검증이 유일한 방어선이다."
    - "**바이트 동치 증명 4구간** — HEAD `a57e744` 스냅샷에서 기계적으로 만든 기대파일과 신규 코드가 `diff` 빈 출력이어야 한다. ⟨EXP1⟩ 게이트 2개 = HEAD **L607–623**(17줄) + `return;`→`return false;` 2건 / ⟨EXP2⟩ 크로스-Z = HEAD **L626–693**(68줄) + 선언 2줄의 `var`·`bool` 제거 + `return;`×4→`return false;` + `break;`×1→`return true;` / ⟨EXP3⟩ 중간 실행부 = HEAD **L694–708**(15줄) **완전 무변경** / ⟨EXP4⟩ 마무리 = HEAD **L709–726**(18줄) **완전 무변경**. **들여쓰기는 전후 동일**(원본도 신규 메서드도 본문 기준 12칸) — 플래너가 드라이런으로 실측 확인했다."
    - "**case 본문은 단 한 글자도 안 바뀐다** — 로그 호출·`acc.FaiAllPass`/`acc.MeasuredCount` 대입·`TakeCrossZRoleImageIfFirst`/`MarkCrossZHalfPending`/`MarkMeasurementCrossZIncomplete` 호출·`meas.ClearResult()`·`SkipReason.NO_IMAGE` 전부 원본 그대로. 바뀌는 것은 **각 case 끝의 제어흐름 키워드 1단어뿐**이며 ⟨EXP2⟩ diff 가 이를 통째로 증명한다."
    - "`switch` 문 자체·`case` 5개의 **순서**·`default:` **미도입 원칙**이 그대로다 — `default:` **라벨** 0건. ⚠ 검증은 반드시 `grep -cE '^[[:space:]]*default:'` **엄격패턴**으로 센다. 평문 `grep -c 'default:'` 는 함께 옮겨진 주석 `//260818 hbk default: 를 두지 않는다` 를 오탐해 **항상 1** 이 나온다(플래너가 시뮬레이션에서 실제로 밟은 함정). 그 주석은 '5개 멤버 전부 다룬다'는 불변식 설명이라 추출 후에도 유효하므로 문구 그대로 함께 이동한다."
    - "`ref acc.CrossZRoleImage` / `ref acc.FaiAllPass` / `ref acc.MeasuredCount` 가 `EvaluateCrossZGate` 안에서도 그대로 컴파일된다 — `acc` 는 참조형 파라미터고 `ShotMeasureAccumulator` 6개 멤버가 **필드**(프로퍼티였다면 CS0206)라 추출 후에도 성립한다."
    - "`out` 2개는 `EvaluateCrossZGate` 본문 **첫 2줄에서 무조건 대입**되므로 6개 return 경로 전부에서 확정 대입이다 → **CS0177 0건**. 호출부도 `out` 반환 후 사용이라 **CS0165 0건**. 모든 경로가 값을 반환하므로 **CS0161 0건**."
    - "기존 상세 주석 **삭제 0건** — `// per-FAI gate: …`(3줄) / `//260716 hbk DatumRef 참조 불일치 게이트 …`(3줄) / `//260722 hbk Phase 68 D-02a/D-05 …`(2줄) / `//260818 hbk 게이트 판정을 명시적 상태(ECrossZGate)로 …`(6줄) / `//260729 hbk quick-fix(260729-e9q) …`(8줄) / `//260818 hbk default: 를 두지 않는다 …`(2줄) / `//260702 hbk Extract Method(Task1·Task2)` 꼬리주석 / `//260818 hbk [SEQ] 요약용 공차이탈 집계` 가 전부 살아 있다."
    - "`MeasureShotFaiList`(gf1 확정형) / `RunMeasure` / `FinalizeFaiTick` / `TakeCrossZRoleImageIfFirst` / `MarkCrossZHalfPending` / `ResolveCrossZGate` / `LogAndTallyAlgorithm` 은 **전문 0줄 diff**. `ProcessOneMeasurement` **시그니처 6줄(HEAD L598–603)도 0줄 diff** — `MeasureShotFaiList` 의 호출부 1줄도 무변경."
    - "빌드 PASS — `error CS` 0건, `warning CS` 가 착수 전 `t0.log` 수치와 **동일**(예상 12 = CS0618×10 + CS0162×2). 신규 `CS0161`/`CS0177`/`CS0165`/`CS0206`/`CS0219`/`CS0168`/`CS0103` 0건."
    - "삼항 `?:` 0건 — **두 각도로 센다**. ① 파일 전역 `?` 포함 줄 수가 착수 전 **13줄**(플래너 실측 — `?.`/`??`/문자열 안 물음표 포함, 전부 손대는 구간 밖)에서 그대로 유지된다. ② 손대는 4개 메서드(`ProcessOneMeasurement` + 신규 3개) 구간의 `?` 포함 줄 수가 **0**(착수 전 L598–727 실측 0). ①은 시그니처 **위** doc주석까지 잡고 ②는 본문을 잡는다 — 둘 다 필요하다."
    - "**단일 Debug|x64 빌드로 충분** — 손대는 구간(L596–740)에 전처리 지시문 **0건**(실측)이라 SIMUL/비-SIMUL 이 동일한 C# 텍스트를 컴파일한다. 매 Task 에서 이 전제를 재확인하고 1건이라도 나오면 2-빌드로 승격한다."
    - "`Action_FAIMeasurement.cs` **단 1개 파일만** 커밋된다 — 새 파일 0개(csproj 무변경), `DatumMeasurement.csproj` 의 로컬 미커밋 오염은 매 커밋 후에도 unstaged `M` 로 남는다."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "ProcessOneMeasurement 3분할 (TryGateMeasurement / EvaluateCrossZGate / RecordMeasurementResult)"
      contains: "private bool EvaluateCrossZGate"
  key_links:
    - from: "ProcessOneMeasurement"
      to: "TryGateMeasurement"
      via: "bool 게이트 — false 면 즉시 return (원본 return; 2곳과 동치)"
      pattern: "if \\(!TryGateMeasurement\\(meas, parentSeq2, acc\\)\\) return;"
    - from: "ProcessOneMeasurement"
      to: "EvaluateCrossZGate"
      via: "bool 게이트 + out 2개 — false 면 즉시 return, true 면 공용 실행 경로 진행"
      pattern: "if \\(!EvaluateCrossZGate\\(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex\\)\\) return;"
    - from: "ProcessOneMeasurement"
      to: "RecordMeasurementResult"
      via: "판정+로그+오버레이+카운터 마무리 (순수 이동, 제어흐름 0)"
      pattern: "RecordMeasurementResult\\(meas, bHasAnyZIndex, ok, resultValue, measError, measOverlays,"
---

<objective>
`Action_FAIMeasurement.ProcessOneMeasurement`(현재 **131줄**, HEAD `a57e744` L598–727)가 한 메서드에 몰아넣고 있는
6가지 책임 중 3덩어리를 분리한다.

1. **`TryGateMeasurement`** — Datum 검출실패 게이트 + DatumRef 참조깨짐 게이트 (원본 L604–623)
2. **`EvaluateCrossZGate`** — 크로스-Z 판정 전체(선언 2줄 + `if(bHasAnyZIndex)` + 5-case `switch`) (원본 L624–693)
3. **`RecordMeasurementResult`** — 판정 + 로그 + 오버레이 + 카운터 마무리 (원본 L709–726, **순수 이동**)

남는 `ProcessOneMeasurement` 은 "게이트 2개 → transform 계산/주입 → 실행 분기 → 마무리" 로 읽히는 **본문 20줄**이 된다.

Purpose: 오늘 종일 반복된 요구 — **"판정 로직·검사 흐름·저장 결과는 단 하나도 바뀌면 안 된다"** —
를 지키면서 가장 두꺼운 메서드를 읽을 수 있게 만드는 것.

Output: 파일 1개 수정, 새 파일 0개, `.csproj` 무변경. **회귀 0 이 하드 요구.**

⚠ **이전 3건(ukh/vih/ruh)과 결정적으로 다른 점**: 저 셋은 전부 "순수 이동"(제어흐름 불변)이었다.
이번엔 `return;`/`break;` → `return false;`/`return true;` 의 **의미 변환**이 들어간다.
하나만 뒤집혀도 **빌드는 멀쩡히 통과하고 판정만 조용히 깨진다.**
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

### 착수 시점 고정값 (플래너 실측)

| 항목 | 값 |
|---|---|
| HEAD | **`a57e744`** (`git rev-parse --short HEAD` 로 실측) |
| 워킹트리 | ` M WPF_Example/DatumMeasurement.csproj` **1건뿐** (커밋 금지 로컬 설정) |
| 대상 파일 | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **1799줄** |
| `ProcessOneMeasurement` | L598–727 (span **130줄**, 본문 **124줄**) |
| 신규 3개 이름 | 파일 내 현재 출현 **0건** |
| 전처리 지시문 (L596–740) | **0건** → 단일 빌드 근거 |
| `?` 포함 줄 (L598–727) | **0** |
| `?` 포함 줄 (파일 전역, 평문 카운트) | **13** (전부 L598–727 **밖**: `"?"` 문자열 리터럴 / `??` 연산자 / L1359 주석) |

### 원본 구간 좌표 (HEAD `a57e744` 절대 줄번호 — 스냅샷 `base.cs` 에 고정)

| 구간 | HEAD 줄 | 줄수 | 행선지 |
|---|---|---|---|
| `//260702 … Task3` 꼬리주석 | 597 | 1 | 제자리 |
| 시그니처 6줄 | 598–603 | 6 | **제자리, 0줄 diff** |
| `// per-FAI gate: …` 주석 | 604–606 | 3 | → `TryGateMeasurement` **본문 맨 위**(⟨EXP0A⟩) |
| 게이트 1 + `//260716` 주석 + 게이트 2 | **607–623** | **17** | → `TryGateMeasurement` 본문(⟨EXP1⟩) |
| `//260722 hbk Phase 68 D-02a/D-05 …` 주석 | 624–625 | 2 | → `EvaluateCrossZGate` **본문 맨 위**(⟨EXP0B⟩) |
| `dualMeasForGate` 선언 ~ `if(bHasAnyZIndex)` 닫는 `}` | **626–693** | **68** | → `EvaluateCrossZGate` 본문(⟨EXP2⟩) |
| `HTuple transform` ~ 실행 분기 닫는 `}` | **694–708** | **15** | **제자리, 0줄 diff**(⟨EXP3⟩) |
| `LogAndTallyAlgorithm(…)` ~ `acc.MeasuredCount++;` | **709–726** | **18** | → `RecordMeasurementResult` 본문(⟨EXP4⟩, 순수 이동) |
| 닫는 `}` | 727 | 1 | 제자리 |

### 🔴 6-경로 매핑표 — 이 작업의 유일한 실질 위험

`ProcessOneMeasurement` 에서 크로스-Z 구간을 빠져나가는 경로는 **6개**다. 추출 후 각각의 반환값:

| # | 원본 (ProcessOneMeasurement 안) | 원본 제어흐름 | → 추출 후 (`EvaluateCrossZGate` 안) | 의미 |
|---|---|---|---|---|
| ① | `case ECrossZGate.Misconfigured:` 본문 끝 | `return;` | **`return false;`** | 설정오류 — 측정 실행 **안 함** |
| ② | `case ECrossZGate.NotMyTick:` 본문 끝 (`if(bNonProtocolCycle){…}` **뒤**, 프로토콜이든 아니든 **항상** 도달) | `return; // 프로토콜: …` | **`return false; // 프로토콜: …`** (꼬리주석 원문 유지) | 이번 tick 무관 — 측정 실행 **안 함** |
| ③ | `case ECrossZGate.CaptureFailed:` 본문 끝 | `return;` | **`return false;`** | 캡처 실패 — 측정 실행 **안 함** |
| ④ | `case ECrossZGate.HalfPending:` 본문 끝 | `return;` | **`return false;`** | 짝 미완성 — 측정 실행 **안 함** |
| ⑤ | `case ECrossZGate.BothReady:` 본문 끝 | `break;` (switch 탈출 → `if` 탈출 → **L694 로 fall-through**) | **`return true; // 완성 index — …`** (꼬리주석 원문 유지) | **짝 완성 — 측정 실행함** |
| ⑥ | `if (bHasAnyZIndex)` 블록을 **아예 안 탐**(`!bHasAnyZIndex`, 일반 측정) | 블록 건너뛰고 **L694 로 진행** | 메서드 **맨 끝**(`if` 블록 **밖**) **`return true;`** | **일반 측정 — 측정 실행함** |

**요약: `return false` = 4개(①②③④) / `return true` = 2개(⑤⑥) / `break` 0개 / 알몸 `return;` 0개.**

⚠ ⑤ 를 `return false` 로 쓰면 **완성된 크로스-Z 측정이 실행 안 되는 치명적 회귀**.
⚠ ① 을 `return true` 로 쓰면 **설정오류 항목이 조용히 정상 측정을 시도**하는 회귀.
**둘 다 빌드는 통과한다.** `PIN1`~`PIN6` 검증이 유일한 방어선이다.

### 신규 3개 메서드 — 확정 시그니처 (grep 앵커로 그대로 쓰이므로 **1글자도 바꾸지 말 것**)

```csharp
        private bool TryGateMeasurement(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc) {
```
```csharp
        private bool EvaluateCrossZGate(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc,
                                        out DualImageEdgeDistanceMeasurement dualMeasForGate, out bool bHasAnyZIndex) {
```
```csharp
        private void RecordMeasurementResult(MeasurementBase meas, bool bHasAnyZIndex, bool ok,
                                             double resultValue, string measError, List<EdgeInspectionOverlay> measOverlays,
                                             List<EdgeInspectionOverlay> overlayAcc, List<EdgeInspectionOverlay> faiOverlays,
                                             Dictionary<string, int> dctAlgoUsed, Stopwatch swMeasureExec,
                                             ShotMeasureAccumulator acc) {
```

### 배치 위치 및 레이아웃 (고정 — 검증이 오프셋으로 걸린다)

세 메서드 모두 `ProcessOneMeasurement` 의 닫는 `}` **바로 다음**, 기존
`//260818 hbk Extract Method: ProcessOneMeasurement 의 알고리즘 로그 조립부를 그대로 옮긴 것.` 주석 **앞**에.
순서: **`TryGateMeasurement` → `EvaluateCrossZGate` → `RecordMeasurementResult`**.

**이동해 온 주석(⟨EXP0A⟩/⟨EXP0B⟩)은 메서드 시그니처 위가 아니라 본문 맨 위(`{` 다음 첫 줄)에 넣는다** —
원문 들여쓰기(12칸)가 그 자리에서 자연스럽고, 원문을 1바이트도 손대지 않아도 되기 때문이다.

| 메서드 | 레이아웃 (위→아래) | span |
|---|---|---|
| `TryGateMeasurement` | 새 doc주석(8칸, 선택) → **시그니처 1줄** → ⟨EXP0A⟩ 3줄 → ⟨EXP1⟩ 17줄 → `return true;` 1줄 → `        }` | **23** |
| `EvaluateCrossZGate` | 새 doc주석(8칸, 선택) → **시그니처 2줄** → ⟨EXP0B⟩ 2줄 → ⟨EXP2⟩ 68줄 → `return true;` 1줄 → `        }` | **74** |
| `RecordMeasurementResult` | 새 doc주석(8칸, 선택) → **시그니처 5줄** → ⟨EXP4⟩ 18줄 → `        }` | **24** |

(span 은 시그니처 첫 줄 ~ 닫는 `}` 까지의 줄 수. 새 doc주석은 시그니처 **위**라 span 에 안 들어간다.)

### `ProcessOneMeasurement` 최종 골격 (span **정확히 27줄**: 시그니처 6 + 본문 20 + `}` 1)

```csharp
        private void ProcessOneMeasurement(MeasurementBase meas, InspectionSequence parentSeq2,
                                     HImage image, double pixRes,
                                     ShotMeasureAccumulator acc,
                                     List<EdgeInspectionOverlay> overlayAcc,
                                     List<EdgeInspectionOverlay> faiOverlays,
                                     Dictionary<string, int> dctAlgoUsed) {
            if (!TryGateMeasurement(meas, parentSeq2, acc)) return;
            DualImageEdgeDistanceMeasurement dualMeasForGate;
            bool bHasAnyZIndex;
            if (!EvaluateCrossZGate(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex)) return;
            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef); //260702 hbk Extract Method(Task1)
            InjectDatumOrigin(meas, parentSeq2); //260702 hbk Extract Method(Task1)
            double resultValue;
            string measError;
            List<EdgeInspectionOverlay> measOverlays;
            bool ok;
            var swMeasureExec = Stopwatch.StartNew(); //260818 hbk 알고리즘 로그용 측정 실행시간
            if (bHasAnyZIndex)
            {
                ok = TryExecuteCrossZMeasurement(dualMeasForGate, parentSeq2, transform, pixRes, out resultValue, out measError, out measOverlays); //260722 hbk Phase 68 D-02a: 완성 index 크로스-Z 실행
            }
            else
            {
                ok = TryExecuteMeasurement(meas, image, transform, pixRes, out resultValue, out measError, out measOverlays); //260702 hbk Extract Method(Task1)
            }
            RecordMeasurementResult(meas, bHasAnyZIndex, ok, resultValue, measError, measOverlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);
        }
```

</context>

<guardrails>

### G-1. 절대 금지
- `git add -A` / `git commit -a` / `git checkout` / `git stash` / `git reset`
- `WPF_Example/DatumMeasurement.csproj` **수정 및 스테이징** (로컬 미커밋 설정이 떠 있다)
- 새 파일 생성 (csproj 를 건드리게 된다)
- 빌드 산출물 잠김 시 **프로세스 강제종료** (앱이 `D:\Data\` 에서 돌고 있을 수 있다)
- `switch` 에 `default:` 추가 / `case` 순서 변경 / `case` 본문 문장 수정
- 삼항 `?:` 신규 도입 (전부 `if-else`)
- **⟨EXP0A⟩~⟨EXP4⟩ 구간 안에 주석 줄 삽입** — 새 설명 주석은 **반드시 시그니처 위 8칸 들여쓰기**로만
- **`ProcessOneMeasurement` 본문 안에 새 주석 줄 추가**, 신규 호출 3줄에 **꼬리주석 붙이기** (span 27줄 & 정확일치 grep 이 깨진다)

### G-1b. 🔴 신규 주석 금칙어 (자기모순 acceptance 방지 — gf1 에서 실제로 터진 유형)
아래 문자열을 **새로 쓰는 주석에 절대 넣지 말 것.** 검증이 줄 단위 카운트로 세기 때문이다.

| 금칙 문자열 | 세는 검증 |
|---|---|
| `?` (물음표) | 삼항 검출 `grep -c '?'` = **13** 유지 + 4개 메서드 구간 = **0** |
| `TryGateMeasurement` | `grep -c` = 2 (선언 1 + 호출 1) |
| `EvaluateCrossZGate` | `grep -c` = 2 |
| `RecordMeasurementResult` | `grep -c` = 2 |

(`bHasAnyZIndex` / `dualMeasForGate` / `acc.` 는 **전부 정확일치(-F 전체줄) 또는 `xm` 범위 한정** 검증이라
새 주석에 들어가도 안전하지만, 안전마진을 위해 새 주석에서 쓰지 않는 편을 권한다.)

### G-2. 셸 상수 + 헬퍼 — **Bash 호출마다 반드시 재정의** (셸 상태는 호출 간 유지되지 않는다)
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/hyk/base.cs"
xm() { awk -v p="$1" '$0 ~ p {i=1} i {print} i && /^        \}$/ {exit}' "$2"; }
RC=0
eq()  { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1 | diff:"; echo "$2" | head -20; RC=1; fi; }
rgx() { if printf '%s' "$2" | grep -qE "$3"; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want~[$3]"; RC=1; fi; }
tr12(){ printf '%s' "$1" | sed 's|^[[:space:]]*||'; }
```
⚠ `xm` 앵커는 반드시 `'^        private void <이름>'` / `'^        private bool <이름>'` 형태(선행 공백 8칸 포함).
접두 공백을 빼면 **호출부 줄에 먼저 걸려 엉뚱한 메서드를 잘라낸다**(gf1 에서 실제로 밟은 함정).
⚠ verify 블록은 **`&&` 긴 체인 대신 `eq`/`dif`/`rgx` 를 한 줄씩** 쓰고 마지막에 `exit $RC` 한다
(주석 줄이 `&& \` 체인에 끼면 bash 가 문법 오류를 낸다).

### G-3. 빌드 규칙 — **SIMUL 단일 빌드** (근거: 손대는 구간 전처리 지시문 0건)
- `//p:` 금지, **`-p:` 사용** (`/` 섞이면 Git Bash 가 `MSB1001` 로 죽는다)
- `-p:OutputPath="$SCRW\\xxx\\"` **후행 백슬래시는 반드시 `\\`** (`\"` 로 끝내면 bash unexpected EOF)

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCRW="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCRW\\hyk-tN\\" \
  -t:Rebuild -v:minimal -nologo 2>&1 | tee "$SCR/hyk/tN.log" >/dev/null
```
판정 기준선은 **셸 변수로 들고 다니지 않는다** — 매번 `$SCR/hyk/t0.log` 에서 다시 센다:
```bash
WBASE=$(grep -cE 'warning CS' "$SCR/hyk/t0.log")
```
잠김 실패 시 `OutputPath` 이름만 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**

### G-4. 순서 — **verify(빌드 포함) 전부 PASS → commit → 커밋 이후 HYGIENE 확인**
```bash
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git commit -m "<메시지>"
git show --name-only --format= HEAD    # 딱 1개 파일
git status --porcelain                 # ' M WPF_Example/DatumMeasurement.csproj' 1줄만
```

</guardrails>

<tasks>

<task type="auto">
  <name>Task 0: baseline 캡처 + 기대파일 6종 기계 생성 (읽기 전용, 파일 수정 0)</name>
  <files>없음 — 스크래치에만 기록 (`$SCR/hyk/`)</files>
  <action>
**대상 파일을 수정하지 않는다.** 이후 3개 Task 의 모든 동치 증명이 여기서 만든 `exp*.txt` 에 걸려 있다.

```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
mkdir -p "$SCR/hyk"

# 1) HEAD 확인 + 대상 .cs 가 a57e744 와 동일한지 (plan 문서 커밋만큼 HEAD 가 앞설 수 있다)
git rev-parse --short HEAD
git diff a57e744 -- $F | head        # 빈 출력이어야 한다

# 2) 워킹트리 baseline
git status --porcelain

# 3) 착수 전 스냅샷 (모든 절대 줄번호의 유일한 기준)
git show a57e744:$F > "$SCR/hyk/base.cs"
SB="$SCR/hyk/base.cs"

# 4) 좌표 재확인 — <context> 표와 일치해야 한다
sed -n '598p;604p;607p;623p;624p;626p;627p;693p;694p;708p;709p;726p;727p' "$SB" | cat -n

# 5) 전처리 0건 전제(단일빌드 근거) / 삼항 baseline / 신규이름 미사용
echo "preproc 596-740 = $(awk 'NR>=596&&NR<=740' "$SB" | grep -c '^[[:space:]]*#')"      # 기대 0
echo "'?' L598-727    = $(awk 'NR>=598&&NR<=727' "$SB" | grep -c '?')"                   # 기대 0
echo "'?' file-wide   = $(grep -c '?' "$SB")"                                            # 기대 13
echo "newnames        = $(grep -c 'TryGateMeasurement\|EvaluateCrossZGate\|RecordMeasurementResult' "$SB")"  # 기대 0
echo "POM span        = $(awk '/^        private void ProcessOneMeasurement/{i=1} i{print} i&&/^        \}$/{exit}' "$SB" | wc -l)"  # 기대 130
```

### 기대파일 6종 생성 (**기계적 치환 — 손으로 편집 금지**)

```bash
cd /c/Info/Project/DataMeasurement
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/hyk/base.cs"

# EXP0A / EXP0B — 이동 대상 주석 (원문 그대로, 들여쓰기 12칸 포함)
sed -n '604,606p' "$SB" > "$SCR/hyk/exp0a.txt"
sed -n '624,625p' "$SB" > "$SCR/hyk/exp0b.txt"

# EXP1 — TryGateMeasurement 본문 17줄: 꼬리주석 return; 1건 + 알몸 return; 1건 → return false;
sed -n '607,623p' "$SB" \
 | sed 's|^\( *\)return;$|\1return false;|' \
 | sed 's|^\( *\)return; \(//.*\)$|\1return false; \2|' > "$SCR/hyk/exp1.txt"

# EXP2 — EvaluateCrossZGate 본문 68줄:
#   선언 2줄 var/bool 제거(out 파라미터가 되므로) + return;×4 → return false; + break;×1 → return true;
sed -n '626,693p' "$SB" \
 | sed 's|^            var dualMeasForGate = |            dualMeasForGate = |' \
 | sed 's|^            bool bHasAnyZIndex = |            bHasAnyZIndex = |' \
 | sed 's|^\( *\)return;$|\1return false;|' \
 | sed 's|^\( *\)return; \(//.*\)$|\1return false; \2|' \
 | sed 's|^\( *\)break; \(//.*\)$|\1return true; \2|' > "$SCR/hyk/exp2.txt"

# EXP3 — ProcessOneMeasurement 에 남는 중간 실행부 15줄 (무변경)
sed -n '694,708p' "$SB" > "$SCR/hyk/exp3.txt"

# EXP4 — RecordMeasurementResult 본문 18줄 (순수 이동·무변경)
sed -n '709,726p' "$SB" > "$SCR/hyk/exp4.txt"

# 생성물 자체 검산
echo "lines exp0a=$(wc -l < "$SCR/hyk/exp0a.txt") exp0b=$(wc -l < "$SCR/hyk/exp0b.txt") exp1=$(wc -l < "$SCR/hyk/exp1.txt") exp2=$(wc -l < "$SCR/hyk/exp2.txt") exp3=$(wc -l < "$SCR/hyk/exp3.txt") exp4=$(wc -l < "$SCR/hyk/exp4.txt")"
echo "exp1: false=$(grep -c 'return false;' "$SCR/hyk/exp1.txt") bare=$(grep -cE '^ *return;$' "$SCR/hyk/exp1.txt")"
echo "exp2: false=$(grep -c 'return false;' "$SCR/hyk/exp2.txt") true=$(grep -c 'return true;' "$SCR/hyk/exp2.txt") break=$(grep -cE '^ *break;' "$SCR/hyk/exp2.txt") bare=$(grep -cE '^ *return;$' "$SCR/hyk/exp2.txt") default=$(grep -cE '^[[:space:]]*default:' "$SCR/hyk/exp2.txt")"
echo "exp3+exp4 ctrlflow=$(cat "$SCR/hyk/exp3.txt" "$SCR/hyk/exp4.txt" | grep -cE '(^|[^a-zA-Z])(return|break|continue)[ ;]')"
```

**기대값 (플래너가 실제로 돌려 확인한 값):**
`exp0a=3 exp0b=2 exp1=17 exp2=68 exp3=15 exp4=18`
`exp1: false=2 bare=0` / `exp2: false=4 true=1 break=0 bare=0 default=0` / `exp3+exp4 ctrlflow=0`

⚠ `exp2` 의 `true=1` 은 **⑤ BothReady 하나뿐**이다. ⑥ `!bHasAnyZIndex` 의 `return true;` 는
`if` 블록 **밖**이라 이 68줄에 없고, Task 2 에서 손으로 1줄 추가한다 → 완성 후 메서드 전체는 `true=2`.

### 빌드 baseline
G-3 명령을 `OutputPath=…hyk-t0\\`, 로그 `$SCR/hyk/t0.log` 로 1회 실행.
`error CS` 가 0이 아니면 착수 전부터 깨진 상태 → **멈추고 보고.**
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/hyk/base.cs"
RC=0
eq()  { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }

eq "T0.1  대상 .cs = a57e744 (unstaged)" "$(git diff a57e744 -- $F | wc -l)" "0"
eq "T0.2  대상 .cs = a57e744 (staged)"   "$(git diff --cached a57e744 -- $F | wc -l)" "0"
eq "T0.3  워킹트리 오염 csproj 1건뿐"     "$(git status --porcelain)" " M WPF_Example/DatumMeasurement.csproj"
eq "T0.4  스냅샷 1799줄"                  "$(wc -l < "$SB")" "1799"
eq "T0.5  전처리 0건(단일빌드 근거)"       "$(awk 'NR>=596&&NR<=740' "$SB" | grep -c '^[[:space:]]*#')" "0"
eq "T0.6  삼항 baseline 구간 0"           "$(awk 'NR>=598&&NR<=727' "$SB" | grep -c '?')" "0"
eq "T0.7  삼항 baseline 파일 13"          "$(grep -c '?' "$SB")" "13"
eq "T0.8  신규이름 0건"                   "$(grep -c 'TryGateMeasurement\|EvaluateCrossZGate\|RecordMeasurementResult' "$SB")" "0"
eq "T0.9  POM 착수전 span 130"            "$(awk '/^        private void ProcessOneMeasurement/{i=1} i{print} i&&/^        \}$/{exit}' "$SB" | wc -l)" "130"

eq "T0.10 exp0a 3줄" "$(wc -l < "$SCR/hyk/exp0a.txt")" "3"
eq "T0.11 exp0b 2줄" "$(wc -l < "$SCR/hyk/exp0b.txt")" "2"
eq "T0.12 exp1 17줄" "$(wc -l < "$SCR/hyk/exp1.txt")" "17"
eq "T0.13 exp2 68줄" "$(wc -l < "$SCR/hyk/exp2.txt")" "68"
eq "T0.14 exp3 15줄" "$(wc -l < "$SCR/hyk/exp3.txt")" "15"
eq "T0.15 exp4 18줄" "$(wc -l < "$SCR/hyk/exp4.txt")" "18"

eq "T0.16 exp1 return false 2건"      "$(grep -c 'return false;' "$SCR/hyk/exp1.txt")" "2"
eq "T0.17 exp1 알몸 return 0건"        "$(grep -cE '^ *return;$' "$SCR/hyk/exp1.txt")" "0"
eq "T0.18 exp2 return false 4건(①②③④)" "$(grep -c 'return false;' "$SCR/hyk/exp2.txt")" "4"
eq "T0.19 exp2 return true 1건(⑤만)"   "$(grep -c 'return true;' "$SCR/hyk/exp2.txt")" "1"
eq "T0.20 exp2 break 0건"              "$(grep -cE '^ *break;' "$SCR/hyk/exp2.txt")" "0"
eq "T0.21 exp2 알몸 return 0건"        "$(grep -cE '^ *return;$' "$SCR/hyk/exp2.txt")" "0"
eq "T0.22 exp2 default 라벨 0건(엄격패턴)"  "$(grep -cE '^[[:space:]]*default:' "$SCR/hyk/exp2.txt")" "0"
eq "T0.23 exp2 case 5개"               "$(grep -c 'case ECrossZGate\.' "$SCR/hyk/exp2.txt")" "5"
eq "T0.24 exp2 첫줄 var 제거"          "$(head -1 "$SCR/hyk/exp2.txt")" "            dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;"
eq "T0.25 exp2 마지막줄 = if 닫는 }"    "$(tail -1 "$SCR/hyk/exp2.txt")" "            }"
eq "T0.26 🔴exp2 BothReady=return true" "$(grep -c 'return true; // 완성 index' "$SCR/hyk/exp2.txt")" "1"
eq "T0.27 exp3+exp4 제어흐름 0(순수이동)" "$(cat "$SCR/hyk/exp3.txt" "$SCR/hyk/exp4.txt" | grep -cE '(^|[^a-zA-Z])(return|break|continue)[ ;]')" "0"

eq "T0.28 빌드 baseline error CS 0"    "$(grep -cE 'error CS' "$SCR/hyk/t0.log")" "0"
echo "INFO WBASE(warning CS) = $(grep -cE 'warning CS' "$SCR/hyk/t0.log")"
eq "T0.29 파일 수정 0줄(대상 .cs 무변경 재확인)" "$(git diff -- $F | wc -l)" "0"
exit $RC
```
    </automated>
  </verify>
  <done>대상 `.cs` = `a57e744` 확인, 워킹트리 오염 csproj 1건뿐, 스냅샷 1799줄, POM span 130, 전처리 0건, 삼항 baseline(손댈 구간 0 / 파일 전역 13), 신규이름 0건, `exp0a/exp0b/exp1~exp4` 6종 생성 및 매핑 검산 통과(**exp2 = false 4 / true 1 / break 0 / bare 0 / default 0 / case 5**), 빌드 baseline `error CS`=0 및 `WBASE` 기록. **파일 수정 0줄.**</done>
</task>

<task type="auto">
  <name>Task 1: TryGateMeasurement 추출 (게이트 2개, return; → return false;)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 1-A. `ProcessOneMeasurement` 에서 HEAD L604–623(20줄) 삭제 → 호출 1줄로 교체

시그니처 닫는 `dctAlgoUsed) {` 다음 줄부터 게이트 2개가 끝나는 `}` 까지를 통째로 들어내고,
그 자리에 **정확히 이 1줄**을 넣는다 (**꼬리주석 금지**):

```csharp
            if (!TryGateMeasurement(meas, parentSeq2, acc)) return;
```

### 1-B. `ProcessOneMeasurement` 닫는 `}` 다음에 새 메서드 추가 (레이아웃 고정)

```csharp
        //260819 hbk quick-260819-hyk: ProcessOneMeasurement 의 초기 게이트 2개를 그대로 옮긴 것.
        //  원본에서 이 자리의 'return;' 2곳은 ProcessOneMeasurement 자체를 빠져나가는 문장이었다.
        //  여기서는 false 를 돌려주고, 호출부가 false 를 받으면 즉시 return 해서 동일한 탈출을 재현한다.
        //  게이트 본문(Mark 호출 / 누적 대입 / 시도회수 통계)은 한 글자도 바뀌지 않았다.
        private bool TryGateMeasurement(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc) {
            … exp0a.txt 3줄 (원문 그대로) …
            … exp1.txt 17줄 (원문 그대로) …
            return true; // 두 게이트 모두 통과 — 호출부는 측정 실행 경로로 계속 진행
        }
```

⚠ `exp0a.txt` / `exp1.txt` 는 **파일에서 복사만** 한다. 손 타이핑하면 diff 가 깨진다.
⚠ 새 doc주석에 **금칙어(G-1b)** 를 넣지 말 것 — 특히 `?` 와 `TryGateMeasurement` 문자열.
   (위 예시 주석은 금칙어 0건이 되도록 이미 조정돼 있다.)
⚠ `return true;` 줄에 꼬리주석은 허용, **별도 주석 줄 추가는 금지**(오프셋 검증이 깨진다).

### 1-C. 빌드
G-3 명령을 `OutputPath=…hyk-t1\\`, 로그 `$SCR/hyk/t1.log` 로 실행.

### 1-D. verify 전부 PASS 후 커밋 (G-4 순서)
```bash
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git commit -m "refactor(260819-hyk): ProcessOneMeasurement 초기 게이트 2개를 TryGateMeasurement 로 추출 (return→return false, 동작 무변경)"
```
커밋 **이후** HYGIENE 확인.
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/hyk/base.cs"
xm() { awk -v p="$1" '$0 ~ p {i=1} i {print} i && /^        \}$/ {exit}' "$2"; }
RC=0
eq()  { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1"; echo "$2" | head -20; RC=1; fi; }
rgx() { if printf '%s' "$2" | grep -qE "$3"; then echo "OK   $1"; else echo "FAIL $1 | got=[$2]"; RC=1; fi; }
SIGT='        private bool TryGateMeasurement(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc) {'
G1='            if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef))'

# ---- 앵커 유일성 가드 (자기모순 방지) ----
eq "T1.0a 시그니처 유일" "$(grep -cF "$SIGT" $F)" "1"
eq "T1.0b 게이트1 앵커 유일" "$(grep -cF "$G1" $F)" "1"
S1=$(grep -nF "$G1" $F | cut -d: -f1)

# ---- ⟨EXP1⟩ 바이트 동치 (return false 2건 포함) ----
dif "T1.1  EXP1 17줄 바이트동치" "$(sed -n "${S1},$((S1+16))p" $F | diff - "$SCR/hyk/exp1.txt")"
# ---- 게이트 통과 경로 = return true, 그 다음이 메서드 종료 ----
rgx "T1.2  통과경로 return true"  "$(sed -n "$((S1+17))p" $F | sed 's|^[[:space:]]*||')" '^return true;( //.*)?$'
eq  "T1.3  메서드 종료 }"          "$(sed -n "$((S1+18))p" $F)" "        }"
# ---- ⟨EXP0A⟩ 주석 3줄이 시그니처 직후(본문 맨 위)에 원문 그대로 ----
dif "T1.4  EXP0A 3줄 원문 유지"    "$(sed -n "$((S1-3)),$((S1-1))p" $F | diff - "$SCR/hyk/exp0a.txt")"
eq  "T1.5  EXP0A 직상단이 시그니처" "$(sed -n "$((S1-4))p" $F)" "$SIGT"
# ---- span / 호출부 ----
eq "T1.6  span 23줄"     "$(xm '^        private bool TryGateMeasurement' $F | wc -l)" "23"
eq "T1.7  호출부 1건"     "$(grep -cF '            if (!TryGateMeasurement(meas, parentSeq2, acc)) return;' $F)" "1"
eq "T1.8  신규이름 2회"   "$(grep -c 'TryGateMeasurement' $F)" "2"

# ---- ProcessOneMeasurement 나머지 구간 무변경 ----
dif "T1.9  POM 시그니처 6줄 0-diff" "$(sed -n '598,603p' "$SB" | diff - <(xm '^        private void ProcessOneMeasurement' $F | head -6))"
S2=$(grep -nF '            var dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;' $F | cut -d: -f1)
eq  "T1.10 크로스-Z 앵커 유일"       "$(grep -cF '            var dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;' $F)" "1"
dif "T1.11 크로스-Z 68줄 아직 원본"  "$(sed -n "${S2},$((S2+67))p" $F | diff - <(sed -n '626,693p' "$SB"))"
S3=$(grep -nF '            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef);' $F | cut -d: -f1)
eq  "T1.12 EXP3 앵커 유일"           "$(grep -cF '            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef);' $F)" "1"
dif "T1.13 EXP3 15줄 0-diff"         "$(sed -n "${S3},$((S3+14))p" $F | diff - "$SCR/hyk/exp3.txt")"
S4=$(grep -nF '            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);' $F | cut -d: -f1)
eq  "T1.14 EXP4 앵커 유일"           "$(grep -cF '            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);' $F)" "1"
dif "T1.15 EXP4 18줄 0-diff(인라인)" "$(sed -n "${S4},$((S4+17))p" $F | diff - "$SCR/hyk/exp4.txt")"

# ---- 주변 메서드 0-diff ----
for M in MeasureShotFaiList FinalizeFaiTick TakeCrossZRoleImageIfFirst MarkCrossZHalfPending RunMeasure LogAndTallyAlgorithm; do
  dif "T1.16 $M 0-diff" "$(xm "^        private void $M" $F | diff - <(xm "^        private void $M" "$SB"))"
done
dif "T1.17 ResolveCrossZGate 0-diff" "$(xm '^        private ECrossZGate ResolveCrossZGate' $F | diff - <(xm '^        private ECrossZGate ResolveCrossZGate' "$SB"))"

# ---- 위생 / 빌드 ----
eq "T1.18 삼항 파일 '?' 13 유지" "$(grep -c '?' $F)" "13"
eq "T1.18b 손댄 구간 '?' 0"      "$( { xm '^        private void ProcessOneMeasurement' $F; xm '^        private bool TryGateMeasurement' $F; } | grep -c '?')" "0"
eq "T1.19 전처리 0건 유지"            "$(awk 'NR>=596&&NR<=780' $F | grep -c '^[[:space:]]*#')" "0"
eq "T1.20 빌드 error CS 0"            "$(grep -cE 'error CS' "$SCR/hyk/t1.log")" "0"
eq "T1.21 빌드 warning = WBASE"       "$(grep -cE 'warning CS' "$SCR/hyk/t1.log")" "$(grep -cE 'warning CS' "$SCR/hyk/t0.log")"
eq "T1.22 신규 진단 0건"              "$(grep -cE 'CS0161|CS0177|CS0165|CS0206|CS0219|CS0168|CS0103|CS1027|CS1028' "$SCR/hyk/t1.log")" "0"

# ---- 커밋 이후 HYGIENE ----
eq "T1.23 커밋 파일 1개"   "$(git show --name-only --format= HEAD)" "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
eq "T1.24 csproj 미스테이징" "$(git status --porcelain)" " M WPF_Example/DatumMeasurement.csproj"
exit $RC
```
    </automated>
  </verify>
  <done>`TryGateMeasurement` 신설(span 23줄), 본문 17줄이 `exp1.txt` 와 **바이트 동치**(`return false` 2건), 통과경로 `return true;`, `// per-FAI gate:` 3줄 원문 그대로 본문 맨 위, 호출부 정확일치 1건·이름 2회, `ProcessOneMeasurement` 시그니처/크로스-Z 68줄/`EXP3`/`EXP4` 전부 0-diff, 주변 7개 메서드 0-diff, 삼항 0, 빌드 PASS(warning=`t0.log` 수치), 대상 1파일만 커밋.</done>
</task>

<task type="auto">
  <name>Task 2: EvaluateCrossZGate 추출 — 🔴 6-경로 bool 매핑 (이 작업 최고위험)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
🔴 **`<context>` 의 6-경로 매핑표를 옆에 띄워두고 작업할 것.** `return false` 4개 / `return true` 2개.

### 2-A. `ProcessOneMeasurement` 에서 크로스-Z 구간(HEAD L624–693, 70줄) 삭제 → 호출 3줄로 교체

`if (!TryGateMeasurement(...)) return;` 다음 줄부터 `if (bHasAnyZIndex)` 블록 닫는 `}` 까지를
통째로 들어내고, **정확히 이 3줄**을 넣는다 (**꼬리주석 금지**):

```csharp
            DualImageEdgeDistanceMeasurement dualMeasForGate;
            bool bHasAnyZIndex;
            if (!EvaluateCrossZGate(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex)) return;
```

### 2-B. `TryGateMeasurement` 닫는 `}` 다음에 새 메서드 추가 (레이아웃 고정)

```csharp
        //260819 hbk quick-260819-hyk: 크로스-Z 게이트 판정 전체를 그대로 옮긴 것.
        //  반환값 계약 — false 는 "이 tick 에 측정을 실행하지 않는다"(설정오류 / 무관tick / 캡처실패 /
        //  짝 미완성 = 4경로), true 는 "공용 실행 경로로 계속 진행한다"(짝 완성 1경로 + 크로스-Z 가
        //  아닌 일반 측정 1경로 = 2경로) 를 뜻한다. 원본에서 각각 return / fall-through 였던 것이다.
        //  case 본문(로그·누적·캡처 호출)은 한 글자도 바뀌지 않았다. 바뀐 것은 각 case 끝의
        //  제어흐름 키워드 1단어뿐이다.
        //  out 2개는 본문 첫 2줄에서 무조건 대입되므로 모든 반환 경로에서 확정 대입이다.
        private bool EvaluateCrossZGate(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc,
                                        out DualImageEdgeDistanceMeasurement dualMeasForGate, out bool bHasAnyZIndex) {
            … exp0b.txt 2줄 (원문 그대로) …
            … exp2.txt 68줄 (원문 그대로 복사) …
            return true; // 크로스-Z 가 아닌 일반 측정 — 원본에서 if 블록을 건너뛰던 경로와 동치
        }
```

⚠ **`exp2.txt` 는 반드시 파일에서 복사.** 손 타이핑 금지.
⚠ `exp2.txt` 안에는 이미 `return false;` **4개** + `return true;` **1개(⑤ BothReady)** 가 정확히 들어 있다.
   레이아웃의 마지막 `return true;`(⑥)는 그 **68줄 밖**, `if (bHasAnyZIndex)` 닫는 `}` **다음**에 손으로 1줄 추가하는 것이다.
⚠ `switch` 에 `default:` **추가 금지** — `//260818 hbk default: 를 두지 않는다` 주석이 `exp2.txt` 안에
   그대로 실려 옮겨지며, 그 불변식(5개 멤버 전부 다룸)은 추출 후에도 유효하다.
   (그 주석 때문에 평문 `grep -c 'default:'` 는 **항상 1** 이 나온다 — 검증은 `^[[:space:]]*default:` 엄격패턴을 쓴다.)
⚠ `ref acc.CrossZRoleImage` / `ref acc.FaiAllPass` / `ref acc.MeasuredCount` 는 그대로 컴파일된다
   (`acc` 는 참조형 파라미터, 멤버는 필드). 여기서 `CS0206` 이 나면 즉시 중단하고 보고.
⚠ 새 doc주석 **금칙어(G-1b)** — `?` 와 `EvaluateCrossZGate` 문자열 금지(위 예시는 이미 0건).

### 2-C. 빌드
G-3 명령을 `OutputPath=…hyk-t2\\`, 로그 `$SCR/hyk/t2.log` 로 실행.
`CS0161`(모든 코드 경로가 값을 반환하지 않음) / `CS0177`(out 미대입)이 나오면 **6-경로 중 하나를 빠뜨린 것**이다.

### 2-D. verify 전부 PASS 후 커밋 (G-4 순서)
```bash
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git commit -m "refactor(260819-hyk): 크로스-Z 게이트 블록을 EvaluateCrossZGate 로 추출 (6경로 bool 매핑, case 본문 무변경)"
```
커밋 **이후** HYGIENE 확인.
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/hyk/base.cs"
xm() { awk -v p="$1" '$0 ~ p {i=1} i {print} i && /^        \}$/ {exit}' "$2"; }
RC=0
eq()  { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1"; echo "$2" | head -20; RC=1; fi; }
rgx() { if printf '%s' "$2" | grep -qE "$3"; then echo "OK   $1"; else echo "FAIL $1 | got=[$2]"; RC=1; fi; }
L(){ sed -n "$1p" $F | sed 's|^[[:space:]]*||'; }
SIGE1='        private bool EvaluateCrossZGate(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc,'
SIGE2='                                        out DualImageEdgeDistanceMeasurement dualMeasForGate, out bool bHasAnyZIndex) {'
ANC='            dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;'

# ---- 앵커 유일성 가드 ----
eq "T2.0a 본문 첫줄 앵커 유일" "$(grep -cF "$ANC" $F)" "1"
eq "T2.0b 시그니처1 유일"      "$(grep -cF "$SIGE1" $F)" "1"
eq "T2.0c 시그니처2 유일"      "$(grep -cF "$SIGE2" $F)" "1"
S2=$(grep -nF "$ANC" $F | cut -d: -f1)

# ---- ⟨EXP2⟩ 바이트 동치 = case 본문 전부 원본 그대로임의 기계적 증명 ----
dif "T2.1  EXP2 68줄 바이트동치" "$(sed -n "${S2},$((S2+67))p" $F | diff - "$SCR/hyk/exp2.txt")"
# ---- ⑥ !bHasAnyZIndex → if 블록 밖 return true ----
eq  "T2.2  S2+67 = if 닫는 }"                  "$(sed -n "$((S2+67))p" $F)" "            }"
rgx "T2.3  🔴PIN6 !bHasAnyZIndex → return true" "$(L $((S2+68)))" '^return true;( //.*)?$'
eq  "T2.4  메서드 종료 }"                       "$(sed -n "$((S2+69))p" $F)" "        }"
# ---- ⟨EXP0B⟩ 주석 2줄이 시그니처 직후(본문 맨 위)에 원문 그대로 ----
dif "T2.5  EXP0B 2줄 원문 유지"  "$(sed -n "$((S2-2)),$((S2-1))p" $F | diff - "$SCR/hyk/exp0b.txt")"
eq  "T2.6  EXP0B 직상단 시그니처2" "$(sed -n "$((S2-3))p" $F)" "$SIGE2"
eq  "T2.7  시그니처1"              "$(sed -n "$((S2-4))p" $F)" "$SIGE1"

# ============ 🔴 PIN1~PIN5: case 별 반환값 1:1 대응 ============
eq "T2.8a Misconfigured case 유일" "$(grep -cF '                    case ECrossZGate.Misconfigured:' $F)" "1"
MC=$(grep -nF '                    case ECrossZGate.Misconfigured:' $F | cut -d: -f1)
eq "T2.8  🔴PIN1 Misconfigured → return false" "$(L $((MC+4)))" "return false;"

eq "T2.9a NotMyTick case 유일" "$(grep -cF '                    case ECrossZGate.NotMyTick:' $F)" "1"
NT=$(grep -nF '                    case ECrossZGate.NotMyTick:' $F | cut -d: -f1)
eq  "T2.9b NotMyTick +1 = if(bNonProtocolCycle)" "$(L $((NT+1)))" "if (bNonProtocolCycle)"
eq  "T2.9c NotMyTick +6 = if 닫는 }"             "$(L $((NT+6)))" "}"
rgx "T2.9  🔴PIN2 NotMyTick → return false (if 밖, 양 갈래 공통)" "$(L $((NT+7)))" '^return false; //'

eq "T2.10a CaptureFailed case 유일" "$(grep -cF '                    case ECrossZGate.CaptureFailed:' $F)" "1"
CF=$(grep -nF '                    case ECrossZGate.CaptureFailed:' $F | cut -d: -f1)
eq "T2.10 🔴PIN3 CaptureFailed → return false" "$(L $((CF+6)))" "return false;"

eq "T2.11a HalfPending case 유일" "$(grep -cF '                    case ECrossZGate.HalfPending:' $F)" "1"
HP=$(grep -nF '                    case ECrossZGate.HalfPending:' $F | cut -d: -f1)
eq "T2.11 🔴PIN4 HalfPending → return false" "$(L $((HP+3)))" "return false;"

eq "T2.12a BothReady case 유일" "$(grep -cF '                    case ECrossZGate.BothReady:' $F)" "1"
BR=$(grep -nF '                    case ECrossZGate.BothReady:' $F | cut -d: -f1)
eq  "T2.12b BothReady +1 = TakeCrossZRoleImageIfFirst" "$(L $((BR+1)))" "TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref acc.CrossZRoleImage);"
rgx "T2.12 🔴PIN5 BothReady 마지막 실행문 = return true" "$(L $((BR+2)))" '^return true; //'
eq  "T2.12c 🔴PIN5 그 줄에 return false 없음" "$(sed -n "$((BR+2))p" $F | grep -c 'return false;')" "0"
eq  "T2.12d BothReady +3 = switch 닫는 }"     "$(L $((BR+3)))" "}"

# ---- 6경로 총량 + switch 형태 보존 ----
eq "T2.13 총량 return false 4"  "$(xm '^        private bool EvaluateCrossZGate' $F | grep -c 'return false;')" "4"
eq "T2.14 총량 return true 2"   "$(xm '^        private bool EvaluateCrossZGate' $F | grep -c 'return true;')" "2"
eq "T2.15 break 0"              "$(xm '^        private bool EvaluateCrossZGate' $F | grep -cE '^ *break;')" "0"
eq "T2.16 알몸 return 0"        "$(xm '^        private bool EvaluateCrossZGate' $F | grep -cE '^ *return;$')" "0"
eq "T2.17 default 라벨 0(엄격패턴)" "$(xm '^        private bool EvaluateCrossZGate' $F | grep -cE '^[[:space:]]*default:')" "0"
eq "T2.18 case 5개"             "$(xm '^        private bool EvaluateCrossZGate' $F | grep -c 'case ECrossZGate\.')" "5"
eq "T2.19 case 순서 보존"       "$(xm '^        private bool EvaluateCrossZGate' $F | grep -oE 'case ECrossZGate\.[A-Za-z]+' | tr '\n' ',')" "case ECrossZGate.Misconfigured,case ECrossZGate.NotMyTick,case ECrossZGate.CaptureFailed,case ECrossZGate.HalfPending,case ECrossZGate.BothReady,"

# ---- span / 호출부 ----
eq "T2.20 span 74줄"  "$(xm '^        private bool EvaluateCrossZGate' $F | wc -l)" "74"
eq "T2.21 호출부 선언1" "$(grep -cF '            DualImageEdgeDistanceMeasurement dualMeasForGate;' $F)" "1"
eq "T2.22 호출부 선언2" "$(grep -cF '            bool bHasAnyZIndex;' $F)" "1"
eq "T2.23 호출부 게이트" "$(grep -cF '            if (!EvaluateCrossZGate(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex)) return;' $F)" "1"
eq "T2.24 신규이름 2회"  "$(grep -c 'EvaluateCrossZGate' $F)" "2"

# ---- Task1 산출물 + 잔여 구간 무변경 ----
G1='            if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef))'
S1=$(grep -nF "$G1" $F | cut -d: -f1)
eq  "T2.25 TryGateMeasurement span 23" "$(xm '^        private bool TryGateMeasurement' $F | wc -l)" "23"
dif "T2.26 EXP1 17줄 유지"              "$(sed -n "${S1},$((S1+16))p" $F | diff - "$SCR/hyk/exp1.txt")"
S3=$(grep -nF '            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef);' $F | cut -d: -f1)
dif "T2.27 EXP3 15줄 0-diff"            "$(sed -n "${S3},$((S3+14))p" $F | diff - "$SCR/hyk/exp3.txt")"
S4=$(grep -nF '            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);' $F | cut -d: -f1)
dif "T2.28 EXP4 18줄 0-diff(인라인)"    "$(sed -n "${S4},$((S4+17))p" $F | diff - "$SCR/hyk/exp4.txt")"
dif "T2.29 POM 시그니처 6줄 0-diff"     "$(sed -n '598,603p' "$SB" | diff - <(xm '^        private void ProcessOneMeasurement' $F | head -6))"

for M in MeasureShotFaiList FinalizeFaiTick TakeCrossZRoleImageIfFirst MarkCrossZHalfPending RunMeasure LogAndTallyAlgorithm; do
  dif "T2.30 $M 0-diff" "$(xm "^        private void $M" $F | diff - <(xm "^        private void $M" "$SB"))"
done
dif "T2.31 ResolveCrossZGate 0-diff" "$(xm '^        private ECrossZGate ResolveCrossZGate' $F | diff - <(xm '^        private ECrossZGate ResolveCrossZGate' "$SB"))"

# ---- 위생 / 빌드 ----
eq "T2.32 삼항 파일 '?' 13 유지" "$(grep -c '?' $F)" "13"
eq "T2.32b 손댄 구간 '?' 0"      "$( { xm '^        private void ProcessOneMeasurement' $F; xm '^        private bool TryGateMeasurement' $F; xm '^        private bool EvaluateCrossZGate' $F; } | grep -c '?')" "0"
eq "T2.33 전처리 0건 유지"            "$(awk 'NR>=596&&NR<=840' $F | grep -c '^[[:space:]]*#')" "0"
eq "T2.34 빌드 error CS 0"            "$(grep -cE 'error CS' "$SCR/hyk/t2.log")" "0"
eq "T2.35 빌드 warning = WBASE"       "$(grep -cE 'warning CS' "$SCR/hyk/t2.log")" "$(grep -cE 'warning CS' "$SCR/hyk/t0.log")"
eq "T2.36 신규 진단 0건"              "$(grep -cE 'CS0161|CS0177|CS0165|CS0206|CS0219|CS0168|CS0103|CS1027|CS1028' "$SCR/hyk/t2.log")" "0"

# ---- 커밋 이후 HYGIENE ----
eq "T2.37 커밋 파일 1개"   "$(git show --name-only --format= HEAD)" "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
eq "T2.38 csproj 미스테이징" "$(git status --porcelain)" " M WPF_Example/DatumMeasurement.csproj"
exit $RC
```
    </automated>
  </verify>
  <done>`EvaluateCrossZGate` 신설(span 74줄), 본문 68줄이 `exp2.txt` 와 **바이트 동치** → case 본문 전부 원본 그대로임이 기계적으로 증명됨. **PIN1~PIN6 6경로 전부 통과** — Misconfigured/NotMyTick/CaptureFailed/HalfPending=`return false`, BothReady=`return true`(같은 줄에 `return false` 없음, 다음 줄이 switch 닫는 `}`), `!bHasAnyZIndex`=`if` 블록 밖 `return true`. 총량 false4/true2/break0/알몸return0/default0, case 5개·순서 보존. 주석 2줄 원문 유지, 호출부 3줄·이름 2회, `EXP1`/`EXP3`/`EXP4`·POM 시그니처·주변 7개 메서드 0-diff, 삼항 0, 빌드 PASS, 1파일 커밋.</done>
</task>

<task type="auto">
  <name>Task 3: RecordMeasurementResult 추출 (순수 이동) + 최종 골격 확정</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
이 구간은 **제어흐름 문장이 0개**(Task 0 에서 `exp3+exp4 ctrlflow=0` 실측)라 **완전한 순수 이동**이다.

### 3-A. `ProcessOneMeasurement` 에서 마무리 18줄(`LogAndTallyAlgorithm(…)` ~ `acc.MeasuredCount++;`) 삭제 → 호출 1줄

**정확히 이 1줄**로 교체 (**꼬리주석 금지**):
```csharp
            RecordMeasurementResult(meas, bHasAnyZIndex, ok, resultValue, measError, measOverlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);
```

### 3-B. `EvaluateCrossZGate` 닫는 `}` 다음에 새 메서드 추가 (레이아웃 고정)

```csharp
        //260819 hbk quick-260819-hyk: ProcessOneMeasurement 의 마무리부(판정 / 실패로그 / 오버레이 누적 /
        //  카운터)를 그대로 옮긴 것. 이 구간에는 제어흐름 문장이 애초에 0개라 순수 이동이다.
        //  Stopwatch 를 통째로 받는 이유는 LogAndTallyAlgorithm 과 같다 — 호출부에서 ms 를 미리 읽으면
        //  로그에 찍히는 숫자가 달라진다.
        private void RecordMeasurementResult(MeasurementBase meas, bool bHasAnyZIndex, bool ok,
                                             double resultValue, string measError, List<EdgeInspectionOverlay> measOverlays,
                                             List<EdgeInspectionOverlay> overlayAcc, List<EdgeInspectionOverlay> faiOverlays,
                                             Dictionary<string, int> dctAlgoUsed, Stopwatch swMeasureExec,
                                             ShotMeasureAccumulator acc) {
            … exp4.txt 18줄 (원문 그대로 복사) …
        }
```

⚠ `exp4.txt` 는 **복사만** 한다. `Logging.PrintLog` 문자열, `//260702 hbk Extract Method(Task2)` /
   `//260818 hbk [SEQ] 요약용 공차이탈 집계` 꼬리주석까지 전부 원문 그대로.
⚠ 새 doc주석 **금칙어(G-1b)** — `?` 와 `RecordMeasurementResult` 문자열 금지(위 예시는 이미 0건).

### 3-C. 빌드
G-3 명령을 `OutputPath=…hyk-t3\\`, 로그 `$SCR/hyk/t3.log` 로 실행.

### 3-D. verify 전부 PASS 후 커밋 (G-4 순서)
```bash
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git commit -m "refactor(260819-hyk): 판정/로그/오버레이 마무리를 RecordMeasurementResult 로 추출 (순수 이동, ProcessOneMeasurement 131→20줄)"
```
커밋 **이후** HYGIENE 확인.
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
SB="$SCR/hyk/base.cs"
xm() { awk -v p="$1" '$0 ~ p {i=1} i {print} i && /^        \}$/ {exit}' "$2"; }
RC=0
eq()  { if [ "$2" = "$3" ]; then echo "OK   $1"; else echo "FAIL $1 | got=[$2] want=[$3]"; RC=1; fi; }
dif() { if [ -z "$2" ]; then echo "OK   $1"; else echo "FAIL $1"; echo "$2" | head -20; RC=1; fi; }
rgx() { if printf '%s' "$2" | grep -qE "$3"; then echo "OK   $1"; else echo "FAIL $1 | got=[$2]"; RC=1; fi; }
L(){ sed -n "$1p" $F | sed 's|^[[:space:]]*||'; }
SIGR='        private void RecordMeasurementResult(MeasurementBase meas, bool bHasAnyZIndex, bool ok,'
ANC4='            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);'
CALL3='            RecordMeasurementResult(meas, bHasAnyZIndex, ok, resultValue, measError, measOverlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);'

# ---- 앵커 유일성 가드 ----
eq "T3.0a 시그니처 유일" "$(grep -cF "$SIGR" $F)" "1"
eq "T3.0b EXP4 앵커 유일" "$(grep -cF "$ANC4" $F)" "1"
S4=$(grep -nF "$ANC4" $F | cut -d: -f1)

# ---- ⟨EXP4⟩ 바이트 동치 (순수 이동) ----
dif "T3.1  EXP4 18줄 바이트동치" "$(sed -n "${S4},$((S4+17))p" $F | diff - "$SCR/hyk/exp4.txt")"
eq  "T3.2  메서드 종료 }"        "$(sed -n "$((S4+18))p" $F)" "        }"
eq  "T3.3  span 24줄"            "$(xm '^        private void RecordMeasurementResult' $F | wc -l)" "24"
eq  "T3.4  제어흐름 0(순수이동 증명)" "$(xm '^        private void RecordMeasurementResult' $F | grep -cE '(^|[^a-zA-Z])(return|break|continue)[ ;]')" "0"
eq  "T3.5  호출부 1건"           "$(grep -cF "$CALL3" $F)" "1"
eq  "T3.6  신규이름 2회"         "$(grep -c 'RecordMeasurementResult' $F)" "2"

# ================= 최종 골격 =================
xm '^        private void ProcessOneMeasurement' $F > "$SCR/hyk/pom.txt"
eq  "T3.7  POM span 27줄 (착수전 130 → 27)" "$(wc -l < "$SCR/hyk/pom.txt")" "27"
dif "T3.8  POM 시그니처 6줄 0-diff"          "$(sed -n '598,603p' "$SB" | diff - <(head -6 "$SCR/hyk/pom.txt"))"
eq  "T3.9  본문1 = TryGateMeasurement 호출"  "$(sed -n '7p' "$SCR/hyk/pom.txt")" "            if (!TryGateMeasurement(meas, parentSeq2, acc)) return;"
eq  "T3.10 본문2 = dualMeasForGate 선언"     "$(sed -n '8p' "$SCR/hyk/pom.txt")" "            DualImageEdgeDistanceMeasurement dualMeasForGate;"
eq  "T3.11 본문3 = bHasAnyZIndex 선언"       "$(sed -n '9p' "$SCR/hyk/pom.txt")" "            bool bHasAnyZIndex;"
eq  "T3.12 본문4 = EvaluateCrossZGate 호출"  "$(sed -n '10p' "$SCR/hyk/pom.txt")" "            if (!EvaluateCrossZGate(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex)) return;"
dif "T3.13 본문5-19 = EXP3 15줄 0-diff"      "$(sed -n '11,25p' "$SCR/hyk/pom.txt" | diff - "$SCR/hyk/exp3.txt")"
eq  "T3.14 본문20 = 마무리 호출"             "$(sed -n '26p' "$SCR/hyk/pom.txt")" "$CALL3"
eq  "T3.15 POM 종료 }"                       "$(sed -n '27p' "$SCR/hyk/pom.txt")" "        }"

# ---- 신규 3개 메서드 최종 재확인 ----
G1='            if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef))'
S1=$(grep -nF "$G1" $F | cut -d: -f1)
eq  "T3.16 TryGateMeasurement span 23" "$(xm '^        private bool TryGateMeasurement' $F | wc -l)" "23"
dif "T3.17 EXP1 17줄 유지"              "$(sed -n "${S1},$((S1+16))p" $F | diff - "$SCR/hyk/exp1.txt")"
ANC2='            dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;'
S2=$(grep -nF "$ANC2" $F | cut -d: -f1)
eq  "T3.18 EvaluateCrossZGate span 74" "$(xm '^        private bool EvaluateCrossZGate' $F | wc -l)" "74"
dif "T3.19 EXP2 68줄 유지"              "$(sed -n "${S2},$((S2+67))p" $F | diff - "$SCR/hyk/exp2.txt")"
eq  "T3.20 🔴6경로 return false 4"      "$(xm '^        private bool EvaluateCrossZGate' $F | grep -c 'return false;')" "4"
eq  "T3.21 🔴6경로 return true 2"       "$(xm '^        private bool EvaluateCrossZGate' $F | grep -c 'return true;')" "2"
eq  "T3.22 break 0"                     "$(xm '^        private bool EvaluateCrossZGate' $F | grep -cE '^ *break;')" "0"
eq  "T3.23 default 라벨 0(엄격패턴)"      "$(xm '^        private bool EvaluateCrossZGate' $F | grep -cE '^[[:space:]]*default:')" "0"
BR=$(grep -nF '                    case ECrossZGate.BothReady:' $F | cut -d: -f1)
rgx "T3.24 🔴PIN5 재확인 BothReady=return true" "$(L $((BR+2)))" '^return true; //'

# ---- 기존 상세주석 삭제 0건 (원문 8종 전수) ----
i=0
for C in '// per-FAI gate: 해당 datum' '//260716 hbk DatumRef 참조 불일치 게이트' '//260722 hbk Phase 68 D-02a/D-05' '//260818 hbk 게이트 판정을 명시적 상태(ECrossZGate)' '//260729 hbk quick-fix(260729-e9q)' '//260818 hbk default: 를 두지 않는다' '//260702 hbk Extract Method(Task2)' '//260818 hbk [SEQ] 요약용 공차이탈 집계'; do
  i=$((i+1))
  if [ "$(grep -cF "$C" $F)" -ge 1 ]; then echo "OK   T3.25.$i 주석 생존: $C"; else echo "FAIL T3.25.$i 주석 소실: $C"; RC=1; fi
done

# ---- 주변 메서드 0-diff (전수) ----
for M in MeasureShotFaiList FinalizeFaiTick TakeCrossZRoleImageIfFirst MarkCrossZHalfPending RunMeasure LogAndTallyAlgorithm; do
  dif "T3.26 $M 0-diff" "$(xm "^        private void $M" $F | diff - <(xm "^        private void $M" "$SB"))"
done
dif "T3.27 ResolveCrossZGate 0-diff" "$(xm '^        private ECrossZGate ResolveCrossZGate' $F | diff - <(xm '^        private ECrossZGate ResolveCrossZGate' "$SB"))"
eq  "T3.28 MeasureShotFaiList 호출부 무변경" "$(grep -cF '                            ProcessOneMeasurement(meas, parentSeq2, image, pixRes, acc, overlayAcc, faiOverlays, dctAlgoUsed);' $F)" "1"

# ---- 위생 / 빌드 ----
eq "T3.29 삼항 파일 '?' 13 유지" "$(grep -c '?' $F)" "13"
eq "T3.29b 손댄 4개 메서드 '?' 0" "$( { xm '^        private void ProcessOneMeasurement' $F; xm '^        private bool TryGateMeasurement' $F; xm '^        private bool EvaluateCrossZGate' $F; xm '^        private void RecordMeasurementResult' $F; } | grep -c '?')" "0"
eq "T3.30 신규이름 TryGate 2회"       "$(grep -c 'TryGateMeasurement' $F)" "2"
eq "T3.31 신규이름 Evaluate 2회"      "$(grep -c 'EvaluateCrossZGate' $F)" "2"
eq "T3.32 전처리 0건 유지"            "$(awk 'NR>=596&&NR<=860' $F | grep -c '^[[:space:]]*#')" "0"
eq "T3.33 빌드 error CS 0"            "$(grep -cE 'error CS' "$SCR/hyk/t3.log")" "0"
eq "T3.34 빌드 warning = WBASE"       "$(grep -cE 'warning CS' "$SCR/hyk/t3.log")" "$(grep -cE 'warning CS' "$SCR/hyk/t0.log")"
eq "T3.35 신규 진단 0건"              "$(grep -cE 'CS0161|CS0177|CS0165|CS0206|CS0219|CS0168|CS0103|CS1027|CS1028' "$SCR/hyk/t3.log")" "0"

# ---- 커밋 이후 HYGIENE ----
eq "T3.36 커밋 파일 1개"     "$(git show --name-only --format= HEAD)" "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
eq "T3.37 csproj 미스테이징"  "$(git status --porcelain)" " M WPF_Example/DatumMeasurement.csproj"
eq "T3.38 코드 변경 범위 = 대상 1파일" "$(git diff --name-only a57e744 HEAD -- WPF_Example/)" "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
exit $RC
```
    </automated>
  </verify>
  <done>`RecordMeasurementResult` 신설(span 24줄, 제어흐름 0 = 순수 이동 증명), 본문 18줄 `exp4.txt` 바이트 동치. `ProcessOneMeasurement` **span 27줄**(착수 전 130 → 27, 본문 20줄) 확정 — 본문 구성이 게이트호출2 + 선언2 + `EXP3` 15줄 + 마무리호출1 로 줄 단위 검증됨. 6경로 매핑 최종 재확인(false4/true2/break0/default0, PIN5 BothReady=`return true`), 기존 상세주석 8종 전부 생존, 주변 7개 메서드 + `MeasureShotFaiList` 호출부 0-diff, 삼항 0, 빌드 PASS(warning=`t0.log` 수치), `a57e744..HEAD` 의 `WPF_Example/` 변경이 대상 1파일뿐.</done>
</task>

</tasks>

<verification>

### 전체 완료 판정 (Task 3 verify 가 이미 전수 포함)

1. **구조** — `ProcessOneMeasurement` span 27줄(본문 20), 신규 3개 span 23 / 74 / 24.
2. **🔴 6-경로 매핑** — `EvaluateCrossZGate` 안 `return false` 4 / `return true` 2 / `break` 0 / 알몸 `return;` 0 / `default:` 0.
   `PIN1`~`PIN6` 개별 줄 위치까지 고정 검증. **특히 `PIN5`(BothReady=`return true`)** 가 통과해야 완성된 크로스-Z 측정이 계속 실행된다.
3. **바이트 동치 4구간** — `exp1`(17) / `exp2`(68) / `exp3`(15) / `exp4`(18) 전부 `diff` 빈 출력.
   → **case 본문·로그 문자열·`acc.` 대입·`ref` 인자·꼬리주석이 전부 원본 그대로**임이 기계적으로 증명된다.
4. **경계 무변경** — `ProcessOneMeasurement` 시그니처 6줄, `MeasureShotFaiList` 호출부 1줄, 주변 7개 메서드 전문 0-diff.
5. **주석** — 기존 상세주석 8종 생존(삭제 0건). `default:` 주석은 `EvaluateCrossZGate` 로 함께 이동(불변식 여전히 유효).
6. **스타일/빌드** — 삼항 0(`?` 파일 전역 **13**줄 유지 + 손댄 4개 메서드 구간 **0**), `error CS` 0, `warning CS` = `t0.log` 수치, 신규 진단 0.
7. **위생** — `a57e744..HEAD` 의 `WPF_Example/` 변경이 대상 1파일뿐, `DatumMeasurement.csproj` 는 unstaged `M` 유지.

### 실패 시 대응
- **`CS0161`/`CS0177`** → 6-경로 중 하나를 빠뜨렸다. 매핑표로 되돌아가 `exp2.txt` 를 다시 복사할 것.
- **`CS0206`** → `ShotMeasureAccumulator` 멤버가 프로퍼티로 바뀐 것. 즉시 중단·보고(이 작업에서 건드릴 이유가 없다).
- **`diff` 가 비지 않음** → 손 타이핑했다는 뜻. `exp*.txt` 에서 다시 복사.
- **`grep -c '?'` 가 13 아님 / 구간 `?` 가 0 아님 / 신규이름 카운트가 2 아님** → 새 주석에 금칙어(G-1b)가 들어갔다. **주석을 고친다(검증식은 고치지 않는다).**
- **빌드 산출물 잠김** → `OutputPath` 이름만 바꿔 재시도. **프로세스 종료 절대 금지.** 그래도 안 되면 사용자에게 보고.
- **`git status` 에 csproj 외 항목** → 즉시 중단하고 보고. `git checkout` / `git stash` 금지.

</verification>

<success_criteria>
- `ProcessOneMeasurement` 131줄 → 본문 20줄(span 27), `TryGateMeasurement` / `EvaluateCrossZGate` / `RecordMeasurementResult` 3개 신설
- 6-경로 매핑 전수 검증 통과 (`return false` 4 / `return true` 2, `PIN1`~`PIN6`)
- 4구간 바이트 동치 `diff` 빈 출력 → **판정 로직·검사 흐름·저장 결과 무변경**
- 기존 상세주석 삭제 0건, 삼항 0건
- Debug|x64 빌드 PASS(`warning CS` = baseline), 신규 진단 0건
- 커밋 3건, 전부 `Action_FAIMeasurement.cs` 1파일만. `.csproj` 미커밋
</success_criteria>

<output>
완료 후 `.planning/quick/260819-hyk-processonemeasurement-trygatemeasurement/260819-hyk-SUMMARY.md` 작성.
포함 항목: 6-경로 매핑표 실측 결과(PIN1~PIN6), 4구간 diff 결과, span 변화(130→27 / 23 / 74 / 24), baseline 대비 warning 수, 커밋 3건 해시.
</output>
