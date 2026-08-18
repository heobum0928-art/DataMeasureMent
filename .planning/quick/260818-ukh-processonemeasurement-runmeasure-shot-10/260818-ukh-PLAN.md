---
phase: quick-260818-ukh
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [UKH-01, UKH-02, UKH-03]

must_haves:
  truths:
    - "`ProcessOneMeasurement` 의 알고리즘 로그 조립부 18줄(HEAD L641–658)이 `LogAndTallyAlgorithm(...)` 단일 호출로 치환되고, 옮겨간 본문은 `bOk` 파라미터 리네임 1건을 제외하면 원본과 바이트 단위로 동일하다"
    - "`dctAlgoUsed` 집계 부수효과(`dctAlgoUsed[szAlgoType]++` / `= 1`)가 보존된다 — Dictionary 는 참조형이므로 값 전달로 호출자 딕셔너리가 그대로 갱신되고, 메서드 이름(`LogAndTallyAlgorithm`)이 '집계도 한다'는 사실을 드러낸다"
    - "`swMeasureExec.ElapsedMilliseconds` 를 **읽는 시점**이 원본과 동일하다 — Stopwatch 객체 자체를 넘기고 헬퍼 안 `Logging.PrintLog` 인자 위치에서 읽는다(호출 시점에 ms 를 미리 계산해 넘기지 않는다)"
    - "`[ALGO]` 로그 포맷 문자열이 한 글자도 바뀌지 않는다 — `\"[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms\"` 리터럴 1건 유지"
    - "`RunMeasure` 의 `if (ShotParam != null) { ... }` 안쪽 48줄(HEAD L458–505 = `using` 문 전체)이 `MeasureShotFaiList(...)` 로 통째 이동하고, 들여쓰기(선행 공백)를 제거하면 원본과 바이트 단위로 동일하다"
    - "조기 return 을 도입하지 않는다 — `if (ShotParam != null)` 은 그 자리에 그대로 남아 `ShotParam == null` 이어도 그 뒤 `pMyContext.AllPass` 기록 / `[SEQ]` 요약 로그 / `Step = End` 가 원본과 동일하게 실행된다"
    - "`using (var image = ShotParam.GetImage())` 와 `try { ... } finally { sharedSrc.Release(); }` 의 상대 위치·중첩 관계가 바뀌지 않는다 — 260810 round4 fix(try 시작을 sharedSrc 생성 직후로 넓힌 누수 수정)가 무효화되지 않는다"
    - "값형 지역변수 4개(`allPass` / `measuredCount` / `nMeasNg` / `bShotDisplayImageReplaced`)가 전부 `ref` 로 전달된다 — `ref` 를 빠뜨려도 **컴파일은 통과하므로**(값 파라미터도 ref 인자로 재전달 가능) grep 으로만 검출 가능한 최대 위험이며, 시그니처와 호출부 양쪽에서 검증된다"
    - "참조형 3개(`parentSeq2` / `overlayAcc` / `dctAlgoUsed`)는 재대입이 없으므로 `ref` 없이 값 전달한다"
    - "새 메서드의 파라미터 이름을 호출자 지역변수 이름과 **동일하게** 지어 옮겨간 본문에 토큰 변경이 0건이다(예외: Task1 의 `ok` → `bOk` 1건)"
    - "파일 전역 앵커 불변 카운트가 착수 전 실측치와 정확히 동일하다: `measuredCount++` 8 / `faiAllPass = false` 8 / `nMeasNg++` 1 / `allPass = false` 1 / `MarkAllMeasurementsNoImage(ref measuredCount)` 1 / `if (sharedSrc != null) sharedSrc.Release()` 1 / `ProcessOneMeasurement(meas,` 1 / `FinalizeFaiTick(fai,` 1 / `using (var image = ShotParam.GetImage()) {` 1 / `dctAlgoUsed[szAlgoType]` 2 / `swMeasureExec.ElapsedMilliseconds` 1 / `[ALGO] 포맷 리터럴` 1"
    - "기존 상세 주석 5계열(260810 round4 fix / 260807 top-z1 / 260729-hwb / 260616 simul-shot-cascade / 260619 per-shot 보정계수)이 삭제 0건으로 로직을 따라 이동한다"
    - "msbuild Debug|x64 가 성공하고 경고가 baseline 12줄(CS0618×10 + CS0162×2)과 동일하다 — 신규 CS0219/CS0168 0건"
    - "파일 전체 코드 삼항(?:) 0건 유지 — 정제 grep 결과가 기존 주석 1줄만"
    - "범위 밖 무접촉: 크로스-Z 게이트(ECrossZGate switch, L556–625) / Datum 게이트 2개(L539–555) / `FinalizeFaiTick` 이하 집계·저장 경로 / 다른 모든 파일"
    - "`Action_FAIMeasurement.cs` 외 어떤 파일도 스테이징/커밋되지 않는다 — 특히 `DatumMeasurement.csproj` 의 로컬 미커밋 변경(Debug OutputPath=D:\\Data\\, Release DefineConstants 의 SIMUL_MODE)이 그대로 unstaged 로 남는다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "private void LogAndTallyAlgorithm(...) + private void MeasureShotFaiList(...) 신규 2개, 호출부 2곳"
      contains: "private void MeasureShotFaiList("
    - path: ".planning/quick/260818-ukh-processonemeasurement-runmeasure-shot-10/260818-ukh-SUMMARY.md"
      provides: "바이트 동치 증명(정규화 diff 결과) + ref 전수표 + 불변 카운트 전후표 + UAT 요청"
  key_links:
    - from: "ProcessOneMeasurement() (측정 실행 직후)"
      to: "LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec)"
      via: "Stopwatch 객체를 넘겨 ms 읽는 시점을 원본 PrintTLog 인자 위치로 보존"
      pattern: "^[[:space:]]*LogAndTallyAlgorithm\\(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec\\);"
    - from: "RunMeasure() 의 if (ShotParam != null) 블록"
      to: "MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced)"
      via: "using 문 전체를 통째 이동 — 값형 4개는 ref, 참조형 3개는 값 전달"
      pattern: "^[[:space:]]*MeasureShotFaiList\\(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced\\);"
    - from: "MeasureShotFaiList 본문"
      to: "using (var image = ShotParam.GetImage()) { … try { … } finally { sharedSrc.Release(); } … }"
      via: "using / try-finally 가 통째로 새 메서드 안으로 따라 들어가며 상대 위치 불변"
      pattern: "^[[:space:]]*\\} finally \\{ // 검사 루프 소유 ref 1 해제"
---

<objective>
`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 에서 **순수 Extract Method 2건**만 수행한다.

1. `ProcessOneMeasurement` 의 알고리즘 로그 조립 + 집계 18줄(L641–658) → `LogAndTallyAlgorithm(...)`
2. `RunMeasure` 의 `if (ShotParam != null)` 안쪽 48줄(L458–505, `using` 문 전체) → `MeasureShotFaiList(...)`

Purpose: 생산 라인 검사 판정 코드다. 사용자 원문(오늘 반복 강조) — **"제일중요한건 기존기능 영향 절대없게"**.
이 작업은 "코드 개선"이 아니라 **의미 보존 변환(behavior-preserving transformation)** 이다.
분기 조건 / 실행 순서 / 부수효과 시점 / 카운터 증감 / 로그 포맷이 1비트라도 달라지면 실패다.
사용자가 지금 모바일이라 실기 확인이 불가능하다 → **정적 검증만으로 무회귀를 증명**해야 한다.

Output: 같은 파일 1개. private 메서드 2개 신규 + 호출부 2곳. 커밋 2개(추출 1건당 1커밋).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**착수 전 필수 확인 (30초). 하나라도 다르면 즉시 중단하고 사용자에게 보고 — 아래 모든 줄번호가 무효화된다:**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git rev-parse --short HEAD          # 기대: 14cf3f1
git status --porcelain              # 기대: " M WPF_Example/DatumMeasurement.csproj" 단 1줄 (+ 미추적 .planning/quick/* 디렉터리)
git status --porcelain -- $F        # 기대: 출력 없음 (clean)
wc -l $F                            # 기대: 1721
sed -n '457,458p;505,506p;641p;658p' $F
# 기대 출력:
#             if (ShotParam != null) {
#                 using (var image = ShotParam.GetImage()) {
#                 }
#             }
#             //260818 hbk 어떤 측정 알고리즘을 탔는지 — Shot 요약용 집계 + Algorithm 탭 상세 1줄
#                 szAlgoResult, swMeasureExec.ElapsedMilliseconds);
```

**⚠ 워킹트리 오염 주의 (이번 작업 최대 사고 위험):**
`WPF_Example/DatumMeasurement.csproj` 에 **커밋하면 안 되는 로컬 설정**이 떠 있다 —
Debug `OutputPath=D:\Data\`, Release `DefineConstants` 의 `SIMUL_MODE`.
저장소에 들어가면 **현장 배포본이 시뮬레이션 모드로 나간다.**
→ **`git add -A` / `git add .` / `git commit -a` 절대 금지.** 반드시 대상 파일 1개만 경로로 스테이징한다.
→ 착수 전 스냅샷: `git status --porcelain > "$SCR/ukh-git-baseline.txt"` (이미 존재하면 덮어쓰지 말 것).
</context>

<ground_rules>
## 이 플랜 전체에 적용되는 절대 규칙

### G-1. 허용되는 변환은 정확히 1종 — "잘라내서 새 메서드에 붙이기"
- 블록을 그대로 잘라 새 private 메서드 본문으로 옮기고, 원래 자리에 호출 1줄을 넣는다. 끝.
- **그 외 어떤 편집도 금지:**
  - 문장 순서 변경 / 조건식 정리 / if-else 병합 / 조기 return 도입 금지
  - 기존 지역변수 리네임 금지 (**유일한 예외 = Task 1 의 `ok` → 파라미터 `bOk` 1건**, 헝가리언 규칙 때문)
  - `using` / `try` / `finally` 경계 이동·재구성·분할 금지 (§G-3)
  - 방어 코드 / null 체크 / 로그 / 예외 처리 추가 금지
  - 주석 삭제 금지 (§G-4)
  - **범위 확장 금지** — 크로스-Z 게이트(`ECrossZGate` switch, L556–625) / Datum 게이트 2개(L539–555) /
    `FinalizeFaiTick` 이하 집계·저장 경로 / 다른 파일 전부 **무접촉**
- **동작이 조금이라도 바뀔 것 같은 부분은 추출하지 말고 원형 유지하고, 그 판단 근거를 SUMMARY 에 적는다.**

### G-2. 파라미터 이름 = 호출자 지역변수 이름 (동치 증명의 핵심 장치)
새 메서드의 파라미터를 호출자 지역변수와 **글자 그대로 같은 이름**으로 짓는다
(`parentSeq2`, `overlayAcc`, `dctAlgoUsed`, `allPass`, `measuredCount`, `nMeasNg`,
 `bShotDisplayImageReplaced`, `meas`, `bHasAnyZIndex`, `swMeasureExec`).
그러면 옮겨간 본문은 **토큰 변경 0건**이 되고, "들여쓰기만 제거한 diff 가 비어 있음"이 곧 바이트 동치 증명이 된다.
`ShotParam` / `pMyContext` 는 클래스 멤버이므로 인스턴스 메서드인 새 메서드에서 그대로 접근된다 — 파라미터로 만들지 말 것.
(예외: `ok` → `bOk`. 신규 파라미터라 헝가리언 규칙 적용. 이 1건은 검증식에서 정규화로 상쇄한다.)

### G-3. `ref` 전수 규칙 — 이 작업 최대 위험
| 변수 | 형 | 블록 안 취급 | 전달 방식 |
|------|----|--------------|-----------|
| `allPass` | bool (값형) | `allPass = false;` 대입 + `ref allPass` 재전달 | **`ref bool`** |
| `measuredCount` | int (값형) | `ref measuredCount` 재전달 ×2 | **`ref int`** |
| `nMeasNg` | int (값형) | `ref nMeasNg` 재전달 | **`ref int`** |
| `bShotDisplayImageReplaced` | bool (값형) | `ref bShotDisplayImageReplaced` 재전달 | **`ref bool`** |
| `parentSeq2` | InspectionSequence (참조형) | 읽기만 (재대입 없음) | 값 전달 |
| `overlayAcc` | List<EdgeInspectionOverlay> | Add 만 (재대입 없음) | 값 전달 |
| `dctAlgoUsed` | Dictionary<string,int> | 인덱서 갱신 (재대입 없음) | 값 전달 |

> **⚠ `ref` 를 빠뜨려도 컴파일은 통과한다.** C# 에서 값 파라미터도 그 자체가 변수라
> `ProcessOneMeasurement(..., ref measuredCount, ...)` 로 재전달할 수 있기 때문이다.
> 결과는 **호출자의 카운터가 조용히 0 으로 남는 런타임 회귀**다(측정 개수/공차이탈 개수 오보고).
> 따라서 **컴파일러는 이 결함을 못 잡는다 — 시그니처와 호출부 grep 이 유일한 방어선**이다.

### G-4. 보존 대상 주석 — 삭제 0건, 로직 따라 이동만
| 앵커 | 현재 위치 | 이동 후 |
|------|-----------|---------|
| `260810 hbk quick-debug(capture-render-per-fai-slow) round4 fix` (5줄, L465–469) | try 바로 위 | `MeasureShotFaiList` 안, try 바로 위 (상대 위치 그대로) |
| `260807 hbk quick-debug(top-z1-measure-8sec-slow) fix` (3줄, L479–481) | QueueSharedShotOrigin 위 | 동일 상대 위치 |
| `260729 hbk quick-fix(260729-hwb)` (L454–455 Shot 표시교체 / L486–488 crossZRoleImage) | 각각 선언 위 | L454–455 는 **RunMeasure 에 잔류**(선언이 남으므로), L486–488 은 새 메서드 안으로 이동 |
| `260616 hbk simul-shot-cascade` (3줄, L499–501) | else 블록 안 | `MeasureShotFaiList` 의 else 블록 안 |
| `260619 hbk per-shot 보정계수` (1줄, L473) | pixRes 위 | 새 메서드 안 동일 위치 |
| `260818 hbk 어떤 측정 알고리즘을 탔는지 …` (L641) | 로그 조립부 첫 줄 | `LogAndTallyAlgorithm` 본문 **첫 줄** (메서드 선언 위가 아님 — §G-6 검증식 때문) |

### G-5. 코딩 컨벤션 (하드)
- **삼항 `?:` 금지** — if-else 만. 신규 삼항 0개
- **C# 7.2 only** — switch expression(`=>`), pattern matching, nullable reference types, record, expression-bodied 신규 멤버 전부 금지
- 신규 파라미터만 헝가리언(`b`/`n`/`sz`/`d`). 기존 이름 변경 금지
- **신규 메서드 선언은 K&R** (`private void Foo(...) {`) — 파일 우세 스타일. 옮겨오는 본문 내부 스타일은 **원본 그대로 유지**(재포맷 금지, diff 노이즈 = 대조 방해)
- 신규 주석 접두 `//260818 hbk`, 비자명한 "왜"만

### G-6. 신규 주석 금칙어 (자기모순 검증 방지)
새로 쓰는 주석에 아래 문자열을 **넣지 말 것**. 검증식이 영구 실패한다.
- `측정 알고리즘을 탔는지` (§Task1 sed 범위 시작 앵커 — 파일 내 유일해야 하며 검증식이 `==1` 을 요구한다. 참고: 짧은 `알고리즘을 탔는지` 는 L369 "검출 알고리즘…" 과 L641 "측정 알고리즘…" 2건이라 앵커로 못 쓴다)
- `using (var image = ShotParam.GetImage())` (§Task2 sed 범위 시작 앵커)
- `?` 뒤에 같은 줄에서 `:` 가 오는 형태 (삼항 검출 오탐)
- `=> ` (화살표 카운트 baseline 1 유지)
그 외 앵커는 전부 `^[[:space:]]*` 로 잡으므로 `//` 로 시작하는 주석은 자동 배제된다.

### G-7. 빌드 규칙
- 앱이 `D:\Data\` 에서 실행 중일 수 있다 → **프로세스 종료 절대 금지.** 스크래치 `OutputPath` 로 컴파일만 검증
- **`//p:` 금지, `-p:` 사용** (`/` 섞이면 Git Bash 가 `MSB1001` 로 죽는다)
- **경고 baseline = 12줄 (CS0618×10 + CS0162×2).** "경고 0" 을 통과 기준으로 쓰면 항상 거짓 실패
- 비-SIMUL(`#else`) 빌드 불필요 — 편집 구역(L457–506, L641–658)에 `#if` 가 0개임을 착수 전 확인하고 근거로 기록

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\ukh-simul\\" \
  -t:Rebuild -v:minimal -nologo
```
파일 잠김으로 실패하면 OutputPath 를 새 이름으로 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**

### G-8. 셸 변수는 호출 사이에 살아남지 않는다
Bash 호출마다 셸이 새로 뜬다. `$F` / `$SCR` / `$BASE` 를 쓰는 **모든 블록의 첫 줄에서 다시 정의**할 것:
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=14cf3f1   # 착수 시점 HEAD — Task1/Task2 diff 대조의 유일한 기준점
```
정의 없이 실행하면 경로가 빈 문자열이 되어 **조용히 오탐**한다.

### G-9. Grep 규칙
- **모든 grep 에 대상 파일 경로 명시** (없으면 stdin 대기로 멈춤)
- 개수 기준은 `^[[:space:]]*` 앵커 또는 코드 토큰으로 좁힌다
- 백슬래시 윈도우 경로 grep 에는 `-F`
- **삼항 검출은 줄 단위**: `grep -nE '\?[^?:]*:' <path> | grep -vE '\?\?|\?\.' | wc -l` → **1** (기존 주석 1줄).
  `-o`(매치 단위)로 바꾸면 문자열 리터럴에서 오탐 2건이 나온다(실측 확인됨)
</ground_rules>

<tasks>

<task type="auto">
  <name>Task 1: ProcessOneMeasurement 알고리즘 로그 조립부(L641–658) → LogAndTallyAlgorithm 추출</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
**0단계 — 기준점 고정 (이후 모든 대조의 근거). Task 1·2 통틀어 1회만 실행:**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=$(git rev-parse --short HEAD)               # 14cf3f1 이어야 함. 다르면 중단
[ -f "$SCR/ukh-git-baseline.txt" ] || git status --porcelain > "$SCR/ukh-git-baseline.txt"
sed -n '641,658p' $F > "$SCR/ukh-before-algolog.txt"   # 18줄
sed -n '458,505p' $F > "$SCR/ukh-before-shotblock.txt" # 48줄
sed -n '450,510p;635,660p' $F | grep -c '#if'    # 기대 0 → 비-SIMUL 빌드 불필요 근거
```
그리고 **G-7 빌드를 착수 전 상태에서 1회** 돌려 경고 줄을 `$SCR/ukh-baseline-warn.txt` 에 저장한다.
이후 모든 경고 비교는 기억이 아니라 이 파일 기준.

---

**1단계 — L641–658 (18줄) 을 잘라내고 그 자리에 호출 1줄을 넣는다.**

치환 시작 = `//260818 hbk 어떤 측정 알고리즘을 탔는지 — Shot 요약용 집계 + Algorithm 탭 상세 1줄` (L641)
치환 끝   = `                szAlgoResult, swMeasureExec.ElapsedMilliseconds);` (L658)
**L659 `if (ok) {` 부터는 손대지 않는다. L632 `var swMeasureExec = Stopwatch.StartNew();` 도 그대로 둔다.**

그 자리에 넣을 1줄 (들여쓰기 12칸):
```csharp
            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);
```

---

**2단계 — 신규 메서드를 `ProcessOneMeasurement` 본체 닫는 `}` **바로 아래**, `ResolveCrossZGate` 선언 **앞**에 추가한다.**
본문은 잘라낸 18줄을 **들여쓰기 그대로**(12칸/16칸) 붙이고, `if (ok)` 한 곳만 `if (bOk)` 로 바꾼다.

```csharp
        //260818 hbk Extract Method: ProcessOneMeasurement 의 알고리즘 로그 조립부를 그대로 옮긴 것.
        //  ⚠ 이름에 Tally 가 붙은 이유 — 로그만 찍지 않는다. dctAlgoUsed(Shot 단위 알고리즘 사용 횟수)를
        //    **갱신하는 부수효과**가 있고, 이 집계값을 RunMeasure 끝의 [SEQ] Measure 요약 로그가 소비한다.
        //    Dictionary 는 참조형이라 값 전달로도 호출자 인스턴스가 그대로 갱신된다(ref 불필요).
        //  ⚠ Stopwatch 를 통째로 받는다 — ms 를 호출부에서 미리 계산해 넘기면 읽는 시점이 앞당겨져
        //    로그 숫자가 달라진다. 아래 PrintLog 인자 위치에서 읽어야 원본과 동일 시점이다.
        private void LogAndTallyAlgorithm(MeasurementBase meas, bool bHasAnyZIndex, bool bOk,
                                          Dictionary<string, int> dctAlgoUsed, Stopwatch swMeasureExec) {
            <여기에 원본 L641–658 을 그대로 붙여넣는다. `if (ok)` → `if (bOk)` 1건만 변경>
        }
```

**절대 하지 말 것:** 문장 순서 변경, `if/else` → 삼항, `szAlgoEntry`/`szAlgoType`/`szAlgoShotName`/`szAlgoResult`
리네임, 포맷 문자열 수정(`·` `→` 유니코드 문자 포함 한 글자도), `ShotParam` 을 파라미터로 승격.

---

**3단계 — 빌드 + 정적 검증 (커밋 전).** verify 블록 **1·2·3 + G-7 빌드**를 여기서 실행한다.
verify 블록 **4(HYGIENE)는 여기서 실행하지 말 것** — `git show HEAD` 로 커밋 결과를 검사하므로
커밋 전에 돌리면 직전 커밋(`14cf3f1`)을 보고 오판한다.

**4단계 — 커밋. `git add -A` 금지, 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only          # 정확히 1줄이어야 함
git commit -m "refactor(260818-ukh): 알고리즘 로그 조립부를 LogAndTallyAlgorithm 로 추출 (순수 이동, 동작 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M" (unstaged) 여야 함
```

**5단계 — 커밋 후 위생 검증.** verify 블록 **4(HYGIENE)** 를 여기서 실행한다(블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
# [1] 구조 — 신규 메서드 1개 + 호출부 1곳 + 범위 밖 무회귀
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -cE '^[[:space:]]*private void LogAndTallyAlgorithm\(' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*LogAndTallyAlgorithm\(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec\);' $F)" = "1" ] && \
echo "== Stopwatch 를 넘긴다(ms 선계산 금지) ==" && \
[ "$(grep -cE '^[[:space:]]*LogAndTallyAlgorithm\([^)]*ElapsedMilliseconds' $F)" = "0" ] && \
[ "$(grep -cF 'swMeasureExec.ElapsedMilliseconds' $F)" = "1" ] && \
echo "== 로그 포맷 리터럴 1건, 바이트 불변 ==" && \
[ "$(grep -cF '"[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms"' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*Logging\.PrintLog\(\(int\)ELogType\.Algorithm,' $F)" = "1" ] && \
echo "== dctAlgoUsed 집계 부수효과 보존 (인덱서 2건) ==" && \
[ "$(grep -cF 'dctAlgoUsed[szAlgoType]' $F)" = "2" ] && \
echo "== 범위 밖 크로스-Z switch 무회귀 ==" && \
[ "$(grep -cE '^[[:space:]]*case ECrossZGate\.[A-Za-z]+:' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*case EStep\.[A-Za-z]+:' $F)" = "6" ] && \
echo "== 선언 순서: ProcessOneMeasurement 가 LogAndTallyAlgorithm 보다 앞 ==" && \
[ "$(grep -n 'private void ProcessOneMeasurement' $F | cut -d: -f1)" -lt "$(grep -n 'private void LogAndTallyAlgorithm' $F | cut -d: -f1)" ] && \
echo "T1 STRUCTURE PASS"
    </automated>
    <automated>
# [2] ⭐바이트 동치 증명 — 옮겨간 18줄이 원본과 완전히 같은가 (bOk→ok 정규화 후 diff 가 비어야 함)
# ⚠ 앵커는 반드시 '측정 알고리즘을 탔는지'(검출 아님). 짧은 '알고리즘을 탔는지' 는 파일에 2건이며
#   앞쪽 매치(L369 "어떤 검출 알고리즘을 탔는지")부터 범위가 시작돼 18줄이 아니라 290줄이 잡힌다.
#   그래서 앵커 유일성(==1)과 범위 길이(==18)를 먼저 못박고 diff 로 넘어간다.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
BASE=14cf3f1 && \
[ "$(grep -cF '측정 알고리즘을 탔는지' $F)" = "1" ] && \
[ "$(sed -n '/측정 알고리즘을 탔는지/,/swMeasureExec.ElapsedMilliseconds);/p' $F | wc -l)" = "18" ] && \
[ "$(grep -cE '^[[:space:]]*if \(bOk\) szAlgoResult' $F)" = "1" ] && \
[ "$(grep -cF 'bOk' $F)" = "1" ] && \
diff <(git show $BASE:$F | sed -n '641,658p') \
     <(sed -n '/측정 알고리즘을 탔는지/,/swMeasureExec.ElapsedMilliseconds);/p' $F | sed 's/\bbOk\b/ok/g') \
&& echo "T1 BYTE-EQUIV PASS (18 lines, diff empty)"
    </automated>
    <automated>
# [3] 파일 전역 앵커 불변 카운트 — 착수 전 실측치와 정확히 동일
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -cE '^[[:space:]]*measuredCount\+\+;' $F)" = "8" ] && \
[ "$(grep -cE '^[[:space:]]*faiAllPass = false;' $F)" = "8" ] && \
[ "$(grep -cE '^[[:space:]]*if \(!meas\.LastJudgement\) nMeasNg\+\+;' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*allPass = false;' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*MarkAllMeasurementsNoImage\(ref measuredCount\);' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*if \(sharedSrc != null\) sharedSrc\.Release\(\);' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*ProcessOneMeasurement\(meas,' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*FinalizeFaiTick\(fai,' $F)" = "1" ] && \
[ "$(grep -cF 'using (var image = ShotParam.GetImage()) {' $F)" = "1" ] && \
echo "T1 INVARIANT COUNTS PASS"
    </automated>
    <automated>
# [4] HYGIENE — ⚠ 반드시 **커밋 이후** 실행 (git show HEAD 로 커밋 결과 검사). SCR 재정의 필수.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
echo "== 코드 삼항 0건 (남는 1줄은 기존 주석) ==" && \
[ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "1" ] && \
echo "== C# 7.2: expression-bodied 증가 0 (baseline 1) ==" && [ "$(grep -c '=> ' $F)" = "1" ] && \
echo "== 커밋에 대상 파일만 ==" && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'Action_FAIMeasurement.cs' && \
echo "== csproj 로컬 변경이 unstaged 로 그대로 ==" && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
[ "$(git show --name-only --format= HEAD | grep -c 'DatumMeasurement.csproj')" = "0" ] && \
echo "== 워킹트리 dirty 집합이 baseline 대비 대상 파일 하나만 변동 ==" && \
diff <(cut -c4- "$SCR/ukh-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[01]$' && \
echo "T1 HYGIENE PASS"
    </automated>
    <automated>G-7 SIMUL 빌드 → 성공 + 경고가 $SCR/ukh-baseline-warn.txt 와 동일(12줄: CS0618×10 + CS0162×2). 신규 CS0219/CS0168(미사용 지역변수) 경고가 1건이라도 생기면 FAIL</automated>
  </verify>
  <done>
`LogAndTallyAlgorithm` private 메서드 1개 신규 + `ProcessOneMeasurement` 안 호출 1줄.
옮겨간 18줄이 `bOk`→`ok` 정규화 후 원본과 **diff 0**(바이트 동치).
`swMeasureExec` 는 Stopwatch 객체로 전달되어 ms 읽는 시점이 원본 PrintLog 인자 위치 그대로.
`[ALGO]` 포맷 리터럴 1건 무변경, `dctAlgoUsed` 인덱서 2건 유지.
파일 전역 앵커 카운트 9종 착수 전과 동일, 코드 삼항 0건.
빌드 성공 + 경고 12줄 baseline 동일. 커밋 1개, 스테이징 파일 정확히 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 2: RunMeasure 의 if(ShotParam != null) 안쪽 48줄(L458–505) → MeasureShotFaiList 통째 이동</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
**전제:** Task 1 커밋 완료. Task 2 의 대상 구간(L458–505)은 Task 1 편집 구간(L641–658)보다 **앞**이므로
`$BASE=14cf3f1` 기준 줄번호가 여전히 유효하다.

---

**1단계 — 조기 return 불가 확인 (반드시 먼저 코드로 확인하고 SUMMARY 에 근거를 남긴다).**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
sed -n '506,526p' $F   # if(ShotParam!=null) 닫힌 뒤에 무엇이 실행되는지
```
`pMyContext.AllPass / MeasuredCount / InspectionOverlays` 기록, `[SEQ] Measure 완료` 요약 로그,
`Step = (int)EStep.End` 가 **`ShotParam == null` 이어도 반드시 실행되어야 한다.**
→ **`if (ShotParam == null) return;` 같은 조기 return 도입 금지.** `if (ShotParam != null) {` 은 그 자리에 그대로 둔다.

---

**2단계 — L458–505 (48줄, `using` 문 전체) 를 잘라내고 그 자리에 호출 1줄을 넣는다.**

치환 시작 = `                using (var image = ShotParam.GetImage()) {` (L458)
치환 끝   = 그 `using` 을 닫는 `                }` (L505)
**L457 `if (ShotParam != null) {` 과 L506 `}` 은 그대로 둔다. L506 이후도 손대지 않는다.**

그 자리에 넣을 1줄 (들여쓰기 16칸):
```csharp
                MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced);
```

---

**3단계 — 신규 메서드를 `RunMeasure` 본체 닫는 `}` **바로 아래**에 추가한다.**
본문 = 잘라낸 48줄을 **각 줄 앞 공백 4칸씩만 줄여서**(16→12칸 기준) 붙인다.
**토큰은 단 하나도 바꾸지 않는다** — 파라미터 이름을 호출자 지역변수와 동일하게 지었기 때문이다.

```csharp
        //260818 hbk Extract Method: RunMeasure 의 if(ShotParam != null) 안쪽 전체를 그대로 옮긴 것.
        //  ⚠ 조기 return 을 쓰지 않았다 — 호출부의 if 를 그대로 둬야 ShotParam==null 일 때도
        //    뒤이은 pMyContext.AllPass 기록 / [SEQ] 요약 로그 / Step=End 가 원본대로 실행된다.
        //  ⚠ 값형 4개는 반드시 ref 다(allPass/measuredCount/nMeasNg/bShotDisplayImageReplaced).
        //    ref 를 빠뜨려도 컴파일은 통과하지만 호출자 카운터가 조용히 0 으로 남는다.
        //    참조형 3개(parentSeq2/overlayAcc/dctAlgoUsed)는 재대입이 없어 값 전달로 충분하다.
        //  ⚠ using / try-finally 는 통째로 함께 이동했다. 상대 위치를 바꾸면 260810 round4 누수 수정이 무효화된다.
        private void MeasureShotFaiList(InspectionSequence parentSeq2,
                                        List<EdgeInspectionOverlay> overlayAcc,
                                        Dictionary<string, int> dctAlgoUsed,
                                        ref bool allPass, ref int measuredCount,
                                        ref int nMeasNg, ref bool bShotDisplayImageReplaced) {
            <여기에 원본 L458–505 를 4칸 dedent 해서 그대로 붙여넣는다. 토큰 변경 0건>
        }
```

**절대 하지 말 것:**
- `using` 을 `try/finally` 로 풀거나, `try` 시작 위치를 옮기거나, `finally` 를 분리
- `if (image != null) { … } else { … }` 구조 변경 / else 를 조기 return 으로 전환
- 안쪽 `foreach (var fai …)` / `foreach (var meas …)` 를 추가 메서드로 또 쪼개기 (이번 범위 밖)
- 파라미터 리네임 (동치 증명 장치가 깨진다)
- `ShotParam` / `pMyContext` 를 파라미터로 승격
- 람다/익명 메서드 도입 — **`ref` 파라미터는 람다 안에서 사용 불가**라 컴파일 에러가 난다
  (착수 전 확인: 원본 48줄에 람다 0건 — `sed -n '458,505p' $F | grep -cE '=>|delegate'` → 0)

---

**4단계 — 빌드 + 정적 검증 (커밋 전).** verify 블록 **1·2·3 + G-7 빌드** 실행.
verify 블록 **4(HYGIENE)는 커밋 이후**에만 실행.

**5단계 — 커밋. 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only          # 정확히 1줄
git commit -m "refactor(260818-ukh): RunMeasure Shot 본문을 MeasureShotFaiList 로 추출 (using/try-finally 통째 이동, 동작 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M"
```

**6단계 — 커밋 후 verify 블록 4(HYGIENE) 실행** (블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
# [1] 구조 + ⭐ref 전수 검증 (컴파일러가 못 잡는 결함 — 유일한 방어선)
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -cE '^[[:space:]]*private void MeasureShotFaiList\(InspectionSequence parentSeq2,' $F)" = "1" ] && \
echo "== 시그니처 ref 4개 ==" && \
[ "$(grep -cE '^[[:space:]]*ref bool allPass, ref int measuredCount,$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*ref int nMeasNg, ref bool bShotDisplayImageReplaced\) \{$' $F)" = "1" ] && \
echo "== 호출부 ref 4개 전부 ==" && \
[ "$(grep -cE '^[[:space:]]*MeasureShotFaiList\(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced\);' $F)" = "1" ] && \
echo "== 조기 return 미도입: 호출부 if 가 그대로 ==" && \
[ "$(grep -cE '^[[:space:]]*if \(ShotParam != null\) \{$' $F)" = "4" ] && \
[ "$(grep -cE '^[[:space:]]*if \(ShotParam == null\) return;' $F)" = "0" ] && \
echo "== using / try-finally 통째 이동, 상대 위치 불변 ==" && \
[ "$(grep -cF 'using (var image = ShotParam.GetImage()) {' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*\} finally \{ // 검사 루프 소유 ref 1 해제' $F)" = "1" ] && \
echo "== 람다 미도입 (ref 파라미터와 공존 불가) ==" && \
[ "$(grep -c '=> ' $F)" = "1" ] && \
echo "T2 STRUCTURE + REF PASS"
    </automated>
    <automated>
# [2] ⭐바이트 동치 증명 — 옮겨간 48줄이 들여쓰기 제거 후 원본과 완전히 같은가 (토큰 변경 0건이어야 함)
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
BASE=14cf3f1 && \
L=$(grep -nF 'using (var image = ShotParam.GetImage()) {' $F | cut -d: -f1) && \
[ "$(printf '%s\n' "$L" | wc -l)" = "1" ] && \
diff <(git show $BASE:$F | sed -n '458,505p' | sed 's/^[[:space:]]*//') \
     <(sed -n "${L},$((L+47))p" $F | sed 's/^[[:space:]]*//') \
&& echo "T2 BYTE-EQUIV PASS (48 lines, diff empty)"
    </automated>
    <automated>
# [3] 파일 전역 앵커 불변 카운트 + 보존 주석 5계열 삭제 0건
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -cE '^[[:space:]]*measuredCount\+\+;' $F)" = "8" ] && \
[ "$(grep -cE '^[[:space:]]*faiAllPass = false;' $F)" = "8" ] && \
[ "$(grep -cE '^[[:space:]]*if \(!meas\.LastJudgement\) nMeasNg\+\+;' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*allPass = false;' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*MarkAllMeasurementsNoImage\(ref measuredCount\);' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*if \(sharedSrc != null\) sharedSrc\.Release\(\);' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*ProcessOneMeasurement\(meas,' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*FinalizeFaiTick\(fai,' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*bool faiAllPass = true;' $F)" = "1" ] && \
[ "$(grep -cF 'dctAlgoUsed[szAlgoType]' $F)" = "2" ] && \
[ "$(grep -cF 'swMeasureExec.ElapsedMilliseconds' $F)" = "1" ] && \
[ "$(grep -cF '"[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms"' $F)" = "1" ] && \
echo "== 보존 주석 5계열 ==" && \
[ "$(grep -cF 'capture-render-per-fai-slow) round4 fix' $F)" -ge 1 ] && \
[ "$(grep -cF 'top-z1-measure-8sec-slow) fix' $F)" -ge 1 ] && \
[ "$(grep -cF '260616 hbk simul-shot-cascade' $F)" -ge 1 ] && \
[ "$(grep -cF '260619 hbk per-shot 보정계수' $F)" -ge 1 ] && \
[ "$(grep -cF '260729-hwb' $F)" -ge 8 ] && \
echo "== 범위 밖 무회귀 ==" && \
[ "$(grep -cE '^[[:space:]]*case ECrossZGate\.[A-Za-z]+:' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*case EStep\.[A-Za-z]+:' $F)" = "6" ] && \
echo "T2 INVARIANT COUNTS PASS"
    </automated>
    <automated>
# [4] HYGIENE — ⚠ 반드시 **커밋 이후** 실행. SCR 재정의 필수.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "1" ] && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'Action_FAIMeasurement.cs' && \
[ "$(git show --name-only --format= HEAD | grep -c 'DatumMeasurement.csproj')" = "0" ] && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
echo "== 두 커밋 합쳐도 변경 파일은 대상 1개뿐 ==" && \
[ "$(git diff --name-only 14cf3f1 HEAD | wc -l)" = "1" ] && \
diff <(cut -c4- "$SCR/ukh-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[01]$' && \
echo "T2 HYGIENE PASS"
    </automated>
    <automated>G-7 SIMUL 빌드 → 성공 + 경고가 $SCR/ukh-baseline-warn.txt 와 동일(12줄). 신규 CS0219/CS0168 0건. 특히 CS0177(out 미할당)/CS0165(미할당 사용)/CS0206(ref 인자 불가) 계열이 1건이라도 뜨면 즉시 중단</automated>
  </verify>
  <done>
`MeasureShotFaiList` private 메서드 1개 신규 + `RunMeasure` 안 호출 1줄.
옮겨간 48줄이 들여쓰기 정규화 후 원본과 **diff 0**(토큰 변경 0건).
값형 4개 전부 `ref`(시그니처·호출부 양쪽 grep 확인), 참조형 3개는 값 전달.
`if (ShotParam != null)` 이 호출부에 잔류 — 조기 return 미도입, L506 이후 경로 무변경.
`using` 1건 + `} finally { // 검사 루프 소유 ref 1 해제` 1건 유지, 상대 위치 불변.
파일 전역 앵커 카운트 12종 + 보존 주석 5계열 확인. 빌드 성공 + 경고 12줄 baseline 동일.
커밋 1개, 14cf3f1..HEAD 변경 파일 총 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 3: 동치 증명 SUMMARY 작성 (정적 검증만으로 무회귀 증명)</name>
  <files>.planning/quick/260818-ukh-processonemeasurement-runmeasure-shot-10/260818-ukh-SUMMARY.md</files>
  <action>
"빌드 통과했으니 OK" 는 근거로 인정하지 않는다. 사용자가 내일 아침까지 실기 확인을 못 하므로
**정적 증거만으로 무회귀를 증명**해야 한다. 아래 5개 절을 실제 명령 출력으로 채운다.

**① 바이트 동치 증명 (핵심)**
| 추출 | 원본 범위(@14cf3f1) | 정규화 방식 | diff 결과 |
|------|---------------------|-------------|-----------|
| ① LogAndTallyAlgorithm | L641–658 (18줄) | `bOk` → `ok` 치환 | (붙여넣기: 비어 있어야 함) |
| ② MeasureShotFaiList | L458–505 (48줄) | 선행 공백 전부 제거 | (붙여넣기: 비어 있어야 함) |

**② `ref` 전수표 (컴파일러가 못 잡는 최대 위험)**
7개 변수 각각에 대해 — 형 / 블록 안 취급(대입? ref 재전달?) / 채택한 전달 방식 / 근거를 적는다.
그리고 **"`ref` 를 빠뜨려도 컴파일은 통과한다"** 는 사실과, 그래서 grep 이 유일한 방어선이라는 점을 명시한다.
시그니처 grep 출력과 호출부 grep 출력을 그대로 붙인다.

**③ `using` / `try-finally` 무접촉 증명**
- `using (var image = ShotParam.GetImage())` 1건, `} finally { // 검사 루프 소유 ref 1 해제` 1건
- 260810 round4 fix 주석(try 를 sharedSrc 생성 직후로 넓힌 누수 수정)이 **try 바로 위**에 그대로 있는지
  현재 줄번호와 함께 제시
- 두 구조가 새 메서드 안으로 **함께** 들어갔고 중첩 관계가 동일함을 ①의 diff 0 이 기계적으로 보증한다는 논리 기술

**④ 부수효과 / 시점 보존**
- `dctAlgoUsed` 집계: 인덱서 2건 유지 + Dictionary 참조형이라 값 전달로 호출자 갱신 → RunMeasure 끝 `[SEQ]` 요약이 소비
- `swMeasureExec.ElapsedMilliseconds` **읽는 시점**: Stopwatch 객체 전달, `PrintLog` 인자 위치에서 읽음.
  "호출부에서 ms 를 미리 계산해 넘기지 않았다"는 것을 grep(`LogAndTallyAlgorithm\([^)]*ElapsedMilliseconds` == 0)으로 제시
- `[ALGO]` 포맷 리터럴 바이트 동일 (LOG_GUIDE.md / 화면 태그 의존)
- **조기 return 미도입** 근거: `ShotParam == null` 일 때도 `pMyContext.AllPass` / `[SEQ]` 요약 / `Step = End` 가
  실행되어야 하므로 호출부 `if` 를 유지했다 (현재 줄번호 제시)

**⑤ 불변 카운트 전후표 + 원형 유지 판단**
- 앵커 카운트 12종의 before(14cf3f1) / after 값을 실측해 표로. 하나라도 다르면 원인 규명 후 수정
- 빌드 경고 줄 수 (baseline 12 = CS0618×10 + CS0162×2) 및 신규 CS0219/CS0168 0건
- **추출하지 않고 원형 유지한 것 + 근거** (사용자 요구):
  | 원형 유지 대상 | 근거 |
  |----------------|------|
  | `if (ShotParam != null)` 를 호출부에 잔류 | 조기 return 으로 바꾸면 L506 이후(AllPass/요약로그/Step=End)가 스킵되어 동작이 바뀐다 |
  | `foreach (var fai …)` 루프 본문을 추가로 쪼개지 않음 | 이번 범위 밖. 쪼개면 `faiAllPass`/`faiOverlays`/`crossZRoleImage` per-FAI 수명 계약을 다시 검증해야 하는데 실기 확인이 불가능한 시점이다 |
  | `using` / `try-finally` 재구성 안 함 | 260810 round4 누수 수정(sharedSrc ref 2중 누수)이 무효화될 수 있다 |
  | `ShotParam` 을 파라미터로 승격하지 않음 | 클래스 멤버 접근이라 토큰 변경 0건 유지 = 바이트 동치 증명이 가능해진다 |
  | 크로스-Z 게이트 / Datum 게이트 / FinalizeFaiTick 이하 | 범위 밖 (G-1) |

**⑥ 사용자 UAT 요청 (checkpoint 아님 — SUMMARY 문구로 남긴다. 내일 아침 실기)**
1. 정상 검사 1사이클 — `[SEQ] Measure 완료 — 측정 N개 (공차이탈 M개)` 의 **N/M 숫자가 리팩토링 전과 동일**한가
   (= `measuredCount`/`nMeasNg` `ref` 누락 여부의 실사용 검증. 이번 작업 최대 위험 지점)
2. `[ALGO]` 로그 줄이 리팩토링 전과 **동일한 포맷/내용**으로 찍히고, `[SEQ]` 요약의 `알고리즘: 타입×횟수`
   집계가 비어 있지 않은가 (= `dctAlgoUsed` 집계 보존 검증)
3. SIMUL 이미지 경로가 무효한 SHOT — 여전히 전 항목 NG 로 뜨고 PASS 로 새지 않는가
   (= `allPass` `ref` + `MarkAllMeasurementsNoImage` 경로 검증)
4. 일괄검사 수 회 반복 — 크로스-Z 캡처 표시/저장 동작과 메모리 증가 추이가 리팩토링 전과 동일한가
   (= `bShotDisplayImageReplaced` `ref` + `sharedSrc.Release()` 누수 수정 무접촉 검증)
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && S=.planning/quick/260818-ukh-processonemeasurement-runmeasure-shot-10/260818-ukh-SUMMARY.md && \
[ -f "$S" ] && \
echo "== 바이트 동치 2건 명시 ==" && grep -qF 'LogAndTallyAlgorithm' $S && grep -qF 'MeasureShotFaiList' $S && \
echo "== ref 전수표 7변수 ==" && \
[ "$(grep -cF 'allPass' $S)" -ge 1 ] && [ "$(grep -cF 'measuredCount' $S)" -ge 1 ] && \
[ "$(grep -cF 'nMeasNg' $S)" -ge 1 ] && [ "$(grep -cF 'bShotDisplayImageReplaced' $S)" -ge 1 ] && \
[ "$(grep -cF 'overlayAcc' $S)" -ge 1 ] && [ "$(grep -cF 'dctAlgoUsed' $S)" -ge 1 ] && \
[ "$(grep -cF 'parentSeq2' $S)" -ge 1 ] && \
echo "== using/try-finally 무접촉 절 ==" && grep -qF 'round4' $S && \
echo "== Stopwatch 시점 보존 ==" && grep -qF 'ElapsedMilliseconds' $S && \
echo "== 조기 return 미도입 근거 ==" && grep -qF '조기 return' $S && \
echo "== 원형 유지 근거 ==" && grep -qF '원형 유지' $S && \
echo "== UAT 4항목 ==" && grep -qF 'UAT' $S && \
echo "== 빌드 경고 baseline 기록 ==" && grep -qF 'CS0618' $S && \
echo "SUMMARY PASS"
    </automated>
    <automated>SUMMARY 의 ① 바이트 동치 표 두 행에 **실제 diff 명령 출력**(비어 있음)이 붙어 있고, ⑤ 불변 카운트 표의 before/after 열이 실측값으로 채워져 있을 것. "PASS" 또는 "OK" 라는 단어만 적힌 행이 1개라도 있으면 FAIL</automated>
  </verify>
  <done>
SUMMARY.md 에 ① 바이트 동치 diff 결과 2건(둘 다 비어 있음), ② `ref` 전수표 7변수 + 시그니처/호출부 grep 출력,
③ using/try-finally 무접촉 증명, ④ 부수효과·시점 보존(dctAlgoUsed / ElapsedMilliseconds / [ALGO] 포맷 / 조기 return 미도입),
⑤ 불변 카운트 before-after 표 + 빌드 경고 수 + 원형 유지 5건 근거표, ⑥ 사용자 UAT 4항목이 기록됨.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

순수 내부 구조 재배치로 **신규 trust boundary 없음**. 기존 경계(PLC/핸들러 ↔ TCP `VisionServer` → 시퀀스,
파일시스템 ↔ 레시피/교시 이미지, 카메라 SDK ↔ `DeviceHandler`)는 이 편집 구역 밖이며
입력 검증 지점이 이동하거나 제거되지 않는다.

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-ukh-01 | Tampering (판정 무결성) | `MeasureShotFaiList` 파라미터에서 `ref` 누락 → 호출자 `measuredCount`/`nMeasNg`/`allPass` 가 0/true 로 남아 **측정 개수·공차이탈 수 오보고, NG 가 PASS 로 새어나감**. **컴파일러가 못 잡는다** | mitigate | 시그니처 앵커 grep 2줄(`ref bool allPass, ref int measuredCount,` / `ref int nMeasNg, ref bool bShotDisplayImageReplaced) {`) + 호출부 전체 인자열 앵커 grep 1건 + G-3 전수표 + UAT 항목 1·3 |
| T-ukh-02 | Tampering (판정 무결성) | 48줄 이동 중 문장 유실/순서 변경으로 특정 FAI 가 측정에서 누락 | mitigate | **바이트 동치 diff**(원본 L458–505 vs 이동본, 선행공백 제거 후 diff 0) + 앵커 불변 카운트 12종 |
| T-ukh-03 | Denial of Service (리소스) | `using`/`try-finally` 경계 재구성 → `sharedSrc` ref 누수 재발(260810 round4 fix 무효화). 일괄검사 메모리 폭증 이력 있음 | mitigate | `using` 1건 + `} finally { // 검사 루프 소유 ref 1 해제` 1건 앵커 grep + 바이트 동치 diff 가 중첩 관계까지 기계 보증 + G-1 금지 명문화 + UAT 항목 4 |
| T-ukh-04 | Repudiation (감사 추적) | `[ALGO]` 로그 포맷 변경 → LOG_GUIDE.md / 화면 태그 파싱 파손, 사고 추적 불가 | mitigate | 포맷 리터럴 `grep -cF` == 1 + 바이트 동치 diff |
| T-ukh-05 | Tampering (통계) | `dctAlgoUsed` 를 값 복사로 오해해 `ref` 를 붙이거나, 집계 문장을 로그와 분리 → `[SEQ]` 요약의 알고리즘 집계가 빈다 | mitigate | `dctAlgoUsed[szAlgoType]` 카운트 == 2 + 메서드명에 Tally 명시 + UAT 항목 2 |
| T-ukh-06 | Tampering (계측 왜곡) | `ElapsedMilliseconds` 를 호출부에서 선계산해 넘김 → 로그 시간값이 실제보다 짧게 기록되어 성능 회귀 진단이 오도됨 | mitigate | `LogAndTallyAlgorithm\([^)]*ElapsedMilliseconds` == 0 + `swMeasureExec.ElapsedMilliseconds` == 1(헬퍼 안 PrintLog 인자 위치) |
| T-ukh-07 | Tampering (리포지토리) | `git add -A` 로 csproj 로컬 설정(Debug OutputPath=D:\Data\, Release SIMUL_MODE)이 커밋됨 → **현장 배포본이 시뮬레이션 모드로 출고** | mitigate | 경로 지정 스테이징 강제 + 커밋 파일 수 == 1 + csproj 커밋 포함 0건 + `git diff --name-only 14cf3f1 HEAD` == 1 + 워킹트리 baseline 차집합 확인 |
| T-ukh-08 | Elevation of Privilege | 해당 없음 — 권한/인증 코드 무접촉 | accept | 이 구역에 권한 판정 로직 없음 |
</threat_model>

<verification>
## 플랜 전체 완료 검증

**A. 구조 (자동)** — 신규 메서드 2개 선언 + 호출부 2곳, 각 인자열 전체 앵커 일치

**B. ⭐바이트 동치 (자동, 이 플랜의 핵심 증거)**
- Task1: `diff <(git show 14cf3f1:$F | sed -n '641,658p')` vs 이동본(`bOk`→`ok` 정규화) → **빈 출력**
- Task2: `diff <(git show 14cf3f1:$F | sed -n '458,505p' | 공백제거)` vs 이동본 48줄(공백제거) → **빈 출력**

**C. `ref` 전수 (자동)** — 값형 4개 시그니처·호출부 양쪽 확인. **컴파일러가 못 잡는 유일한 항목**

**D. 부수효과·시점 (자동)** — `dctAlgoUsed[szAlgoType]` 2 / `swMeasureExec.ElapsedMilliseconds` 1 /
호출부 선계산 0 / `[ALGO]` 포맷 리터럴 1 / `} finally { // 검사 루프 소유 ref 1 해제` 1 / `using` 1

**E. 불변 카운트 (자동)** — 앵커 12종 전부 착수 전(14cf3f1) 실측치와 일치

**F. 범위 밖 무회귀 (자동)** — `case ECrossZGate.*:` 5 / `case EStep.*:` 6 / 보존 주석 5계열 삭제 0 /
`git diff --name-only 14cf3f1 HEAD` == 1

**G. 컴파일 (자동)** — Debug|x64 SIMUL 성공 + 경고 12줄 baseline 동일, 신규 CS0219/CS0168 0건.
비-SIMUL 빌드는 편집 구역에 `#if` 0개이므로 생략(근거 기록 필수)

**H. 위생 (자동)** — 코드 삼항 0 / `=> ` 1 / 커밋 파일 각 1개 / csproj unstaged 유지 /
워킹트리 dirty 집합 baseline 대비 대상 파일만 변동

**I. 사용자 UAT (SUMMARY 요청 문구, 내일 아침 실기)** — 측정 개수·공차이탈 수 동일 /
`[ALGO]` + 알고리즘 집계 정상 / 무효 이미지 SHOT NG 유지 / 일괄검사 메모리 추이 동일
</verification>

<success_criteria>
- 추출 2건 완료: `LogAndTallyAlgorithm`(18줄) + `MeasureShotFaiList`(48줄), 각각 호출 1줄로 치환
- **바이트 동치 diff 2건 모두 빈 출력** — 정규화(bOk→ok / 선행공백 제거) 외 변경 0
- 값형 4개 전부 `ref`, 참조형 3개는 값 전달 — 시그니처·호출부 양쪽 grep 확인
- `using` / `try-finally` 상대 위치 불변, 260810 round4 누수 수정 무효화 0
- `swMeasureExec.ElapsedMilliseconds` 읽는 시점 원본 유지 (Stopwatch 객체 전달)
- `[ALGO]` 포맷 리터럴 바이트 동일, `dctAlgoUsed` 집계 보존
- 조기 return 미도입 — `ShotParam == null` 경로에서 L506 이후가 원본대로 실행
- 앵커 불변 카운트 12종 착수 전과 동일 / 코드 삼항 0건 / 보존 주석 5계열 삭제 0건
- Debug|x64 빌드 성공, 경고 12줄 baseline 동일, 신규 미사용변수 경고 0
- 커밋 2개, 변경 파일 총 1개(`Action_FAIMeasurement.cs`), csproj 로컬 변경 unstaged 유지
- SUMMARY 에 동치 증명 6개 절 + 원형 유지 5건 근거 + UAT 4항목 기록
</success_criteria>

<output>
완료 후 `.planning/quick/260818-ukh-processonemeasurement-runmeasure-shot-10/260818-ukh-SUMMARY.md` 생성.
**반드시 포함:** ① 바이트 동치 diff 결과 2건, ② `ref` 전수표 7변수(+grep 출력),
③ using/try-finally 무접촉 증명, ④ 부수효과·시점 보존 4건, ⑤ 불변 카운트 before-after 표 + 빌드 경고 수
+ 원형 유지 5건 근거표, ⑥ 사용자 UAT 요청 4항목.
</output>
