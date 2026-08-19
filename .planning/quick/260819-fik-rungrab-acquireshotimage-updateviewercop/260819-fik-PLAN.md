---
phase: quick-260819-fik
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [FIK-01, FIK-02, FIK-03]

must_haves:
  truths:
    - "`RunGrab`(HEAD `7708808` L379–433)의 촬영+하드웨어에러 28줄(L383–410)이 `AcquireShotImage()` 로, 표시사본 처리 11줄(L415–425)이 `UpdateViewerCopy(HImage image)` 로 통째 이동하고, 옮겨간 39줄은 선행 공백을 제거하면 원본과 **바이트 단위로 동일**하다(토큰 변경 0건)"
    - "파일 전체 라인 멀티셋 대조에서 **삭제된 줄이 0줄**이다 — `comm -23 <(base 정규화·정렬) <(after 정규화·정렬)` 가 빈 출력. 순수 추출이므로 추가만 있고 삭제는 없어야 한다"
    - "`ShotParam.SetImage(image);`(측정 소스 = 데이터 경로)와 `image.Dispose();`(소유권 종료)는 **`RunGrab` 에 잔류**한다 — 헬퍼 안으로 들어가면 early-return 등으로 '조건과 무관하게 항상 수행' 계약이 깨질 여지가 생긴다"
    - "`var swGrabTotal = Stopwatch.StartNew();` 는 `RunGrab` 에 잔류한다 — 헬퍼로 옮기면 tact 측정 구간이 달라져 `[SEQ] Grab` 로그 숫자가 바뀐다"
    - "`Step = (int)EStep.Measure;` 가 `if (ShotParam != null && !ShotParam.HasImage)` **바깥**에 그대로 남는다 — 이미지가 없어도 항상 실행되는 기존 동작 유지. 추출 후 RunGrab 본문 17줄에서 정확히 16번째 줄이어야 한다"
    - "`parentSeqForView` 해석의 `if (ShotParam != null) … else parentSeqForView = null;` 중복 방어가 원형 그대로 `UpdateViewerCopy` 안으로 따라 들어간다 — 바깥 if 가 이미 non-null 을 보장하더라도 제거하면 순수 이동이 아니다"
    - "조건부 컴파일 블록이 통째로 `AcquireShotImage` 안으로 들어간다 — 파일 전역 `#if SIMUL_MODE` 3 / `#else` 3 / `#endif` 3 (앵커 기준) 불변이고, 추출 후 `RunGrab` 본문에는 `#if` 가 0건, `AcquireShotImage` 안에 정확히 1쌍이다"
    - "**두 빌드 모두** PASS 한다 — Debug|x64(SIMUL_MODE ON) 경고 CS 12줄(CS0618×10 + CS0162×2) / 비-SIMUL(Release|x64 + `-p:DefineConstants=TRACE`) 경고 CS 10줄(CS0618×10, CS0162 0건), 양쪽 error CS 0. 실측 확정치이며 신규 CS0219/CS0168/CS0177/CS0165/CS0103 은 0건이어야 한다"
    - "비-SIMUL 빌드가 **진짜로** 비-SIMUL 이다 — 워킹트리 csproj 의 Release|x64 `DefineConstants` 에 로컬로 `SIMUL_MODE` 가 들어가 있으므로 반드시 명령줄 `-p:DefineConstants=TRACE` 로 덮어써야 하며, 그 증거로 `warning CS0162`(SIMUL 전용 도달불가 코드) 가 0건임을 확인한다"
    - "파일 전역 앵커 카운트가 착수 전 실측과 동일하다: `bIsLiveGrabAttempt` 3 / `swGrabTotal` 2 / `parentSeqForMirror` 2 / `parentSeqForHwErr` 4 / `parentSeqForView` 4 / `bSkipViewer` 2 / `IsViewerUpdateSkipped(` 3 / `MarkCycleHardwareError()` 2 / `LoadShotInspectionImage()` 4 / `image.CopyImage()` 2 / `pMyContext.ResultHalconImage` 7 / `HImage image = null;` 3 / `case EStep.` 6 / `case ECrossZGate.` 5"
    - "보존 주석 5계열이 삭제 0건으로 로직을 따라 이동한다 — `260811 hbk plc-spec-260811-alignment`(하드웨어 에러 E 판정) / `quick-260813-jnh`(MIL 미러) / `260810 hbk quick-260810-egx`(표시 사본 생략) / `260618 hbk Phase 54 ALIGN-01`(warp 폐기) / `260818 hbk [SEQ]`"
    - "코드 삼항 `?:` 0건 유지 — 정제 grep 결과가 기존 주석 1줄(L1307)뿐"
    - "범위 밖 무접촉: `RunDatumPhase` / `RunMeasure` / `MeasureShotFaiList` / `ProcessOneMeasurement` / `FinalizeFaiTick` / `GrabOrLoadDatumImage`(L817~, RunGrab 과 구조가 닮은 별개 메서드) / 다른 모든 파일"
    - "`Action_FAIMeasurement.cs` 외 어떤 파일도 스테이징/커밋되지 않는다 — 특히 `DatumMeasurement.csproj` 의 로컬 미커밋 변경(Debug OutputPath=D:\\Data\\, Release DefineConstants 의 SIMUL_MODE)이 그대로 unstaged 로 남는다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "private HImage AcquireShotImage() + private void UpdateViewerCopy(HImage image) 신규 2개, RunGrab 호출부 2줄"
      contains: "private HImage AcquireShotImage() {"
    - path: ".planning/quick/260819-fik-rungrab-acquireshotimage-updateviewercop/260819-fik-SUMMARY.md"
      provides: "바이트 동치 diff 출력 + 라인 멀티셋 대조 + 2-빌드(SIMUL/비-SIMUL) 경고 전후표 + RunGrab 최종 골격 + UAT 요청"
  key_links:
    - from: "RunGrab() (swGrabTotal 시작 직후)"
      to: "HImage image = AcquireShotImage();"
      via: "촬영(#if SIMUL_MODE 전체) + 하드웨어 에러 마킹을 통째로 옮기고 HImage(null 가능) 반환"
      pattern: "^[[:space:]]*HImage image = AcquireShotImage\\(\\);$"
    - from: "RunGrab() 의 if (image != null) 블록, ShotParam.SetImage 직후"
      to: "UpdateViewerCopy(image);"
      via: "표시사본만 이동 — SetImage/Dispose 는 호출부 잔류"
      pattern: "^[[:space:]]*UpdateViewerCopy\\(image\\);$"
    - from: "UpdateViewerCopy 본문"
      to: "pMyContext.ResultHalconImage (기존 Dispose → null 또는 CopyImage)"
      via: "bSkipViewer 분기 원형 유지"
      pattern: "^[[:space:]]*bool bSkipViewer = IsViewerUpdateSkipped\\(parentSeqForView\\);$"
---

<objective>
`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 의 `RunGrab`(L379–433)에서 **순수 Extract Method 2건**만 수행한다.

1. L383–410 (28줄, 촬영 `#if SIMUL_MODE` 블록 전체 + 하드웨어 에러 마킹) → `private HImage AcquireShotImage()`
2. L415–425 (11줄, 화면표시용 사본 처리) → `private void UpdateViewerCopy(HImage image)`

Purpose: 생산 라인 검사 판정 코드다. 사용자 원문(오늘 내내 반복) — **"판정 로직·검사 흐름·저장 결과는 단 하나도 바뀌면 안 된다"**, **"커밋 메시지 주장만 믿지 말고 커밋마다 동작 무변경을 코드로 직접 재확인"**.
이 작업은 "코드 개선"이 아니라 **의미 보존 변환(behavior-preserving transformation)** 이다.
분기 조건 / 실행 순서 / 부수효과 시점 / `#if` 경계 / 로그 포맷이 1비트라도 달라지면 실패다.

**직전 3개 quick(ruh/ukh/vih)과 결정적으로 다른 점: 추출 구역 안에 `#if SIMUL_MODE` / `#else` / `#endif` 가 들어 있다.**
따라서 Debug(SIMUL ON) 한쪽만 빌드하면 반대쪽이 깨진 걸 못 잡는다 — **2-빌드 검증이 필수**다.

Output: 같은 파일 1개. private 메서드 2개 신규 + 호출부 2줄. 커밋 2개(추출 1건당 1커밋) + SUMMARY 1개.
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
git rev-parse --short HEAD          # 기대: 7708808
git status --porcelain              # 기대: " M WPF_Example/DatumMeasurement.csproj" 단 1줄
git status --porcelain -- $F        # 기대: 출력 없음 (clean)
wc -l $F                            # 기대: 1747
sed -n '379p;383p;410p;411p;415p;425p;426p;432p' $F
# 기대 출력(순서대로):
#         private void RunGrab() {
#                 HImage image = null;
#                 }
#                 if (image != null) {
#                     //260810 hbk quick-260810-egx: 아래는 "표시 전용" 사본(127MP memcpy). 자동검사 중 표시를 끄면 생략한다.
#                     }
#                     image.Dispose(); // 누수 방지 — 조건과 무관하게 항상 수행.
#             Step = (int)EStep.Measure;
```

**현재 `RunGrab` 구조 (HEAD 7708808 실측)**

| 구역 | 줄 | 내용 | 처리 |
|------|----|------|------|
| 머리 | 380–382 | `if (ShotParam != null && !ShotParam.HasImage) {` + swGrabTotal 시작 | **잔류** |
| [A] | 383–402 | `HImage image = null;` / `bIsLiveGrabAttempt` / `#if SIMUL_MODE`…`#endif` (미러 해석 포함) | → `AcquireShotImage` |
| [B] | 403–410 | 실기 grab null → `MarkCycleHardwareError()` + 에러 로그 | → `AcquireShotImage` |
| [C1] | 411–414 | `if (image != null) {` + ALIGN-01 주석 + `ShotParam.SetImage(image);` | **잔류** |
| [C2] | 415–425 | 표시사본: parentSeqForView / bSkipViewer / 기존 Dispose / null 또는 CopyImage | → `UpdateViewerCopy` |
| [C3] | 426–427 | `image.Dispose();` + `}` | **잔류** |
| [D] | 428–432 | `[SEQ] Grab` 요약 로그 + `}` + `Step = (int)EStep.Measure;` | **잔류** |

**⚠ 이 파일에는 RunGrab 과 구조가 매우 닮은 `GrabOrLoadDatumImage`(L817–850)가 있다.**
거기에도 `#if SIMUL_MODE` / `MarkCycleHardwareError()` / `HImage image = null;` 가 있다 →
**단순 문자열 앵커가 유일하지 않다.** 그래서 이 플랜은 sed 범위 앵커를 쓰지 않고
**신규 메서드 선언줄 위치 + 고정 길이(28줄 / 11줄)** 로 diff 범위를 잡는다(§G-6).

**⚠ 워킹트리 오염 주의 (이번 작업 최대 사고 위험 2건):**
1. `WPF_Example/DatumMeasurement.csproj` 에 **커밋하면 안 되는 로컬 설정**이 떠 있다 —
   Debug `OutputPath=D:\Data\`(L43 부근), **Release|x64 `DefineConstants`=`TRACE;SIMUL_MODE`(L74)**.
   저장소에 들어가면 현장 배포본이 시뮬레이션 모드로 나간다.
   → **`git add -A` / `git add .` / `git commit -a` 절대 금지.** 대상 파일 1개만 경로로 스테이징한다.
   → **csproj 파일 자체를 수정하지 말 것.** 되돌리지도, 고치지도 않는다.
2. 그 L74 때문에 **Release 를 그냥 빌드하면 SIMUL 경로가 컴파일되는 가짜 비-SIMUL 검증**이 된다.
   → 반드시 명령줄 `-p:DefineConstants=TRACE` 로 덮어쓴다(§G-7).
</context>

<ground_rules>
## 이 플랜 전체에 적용되는 절대 규칙

### G-1. 허용되는 변환은 정확히 1종 — "잘라내서 새 메서드에 붙이기"
- 블록을 그대로 잘라 새 private 메서드 본문으로 옮기고, 원래 자리에 호출 1줄을 넣는다. 끝.
- **그 외 어떤 편집도 금지:**
  - 문장 순서 변경 / 조건식 정리 / if-else 병합 / 조기 return 도입 / 중복 null 체크 제거 금지
  - **기존 지역변수 리네임 금지** (신규 파라미터도 호출자 지역변수와 동일 이름 → 리네임 0건, §G-2)
  - `#if` / `#else` / `#endif` 경계 이동·분할·조건 변경 금지 (§G-3)
  - 방어 코드 / null 체크 / 로그 / 예외 처리 추가 금지
  - 주석 삭제 금지 (§G-4)
  - **범위 확장 금지** — `RunDatumPhase` / `RunMeasure` / `MeasureShotFaiList` / `ProcessOneMeasurement` /
    `FinalizeFaiTick` / `GrabOrLoadDatumImage` / `IsViewerUpdateSkipped` / 다른 파일 전부 **무접촉**
- **동작이 조금이라도 바뀔 것 같은 부분은 추출하지 말고 원형 유지하고, 그 판단 근거를 SUMMARY 에 적는다.**

### G-2. 경계 결정 (이미 확정 — 재설계 금지)
| 요소 | 어디로 | 이유 |
|------|--------|------|
| `var swGrabTotal = Stopwatch.StartNew();` | **RunGrab 잔류** | 헬퍼로 옮기면 tact 측정 구간이 달라져 `[SEQ] Grab` 로그 숫자가 바뀐다 |
| `ShotParam.SetImage(image);` | **RunGrab 잔류** | 측정 소스(데이터 경로)라 "ViewerCopy" 이름과 맞지 않는다 |
| `image.Dispose();` | **RunGrab 잔류** | 소유권 종료 — "조건과 무관하게 항상 수행" 계약이 호출부에 보여야 안전하다 |
| `parentSeqForView` 해석(3줄) | **UpdateViewerCopy 안** | 원형(`if (ShotParam != null) … else … = null;`)을 **글자 그대로** 옮기면 토큰 변경 0건이 되어 바이트 동치 증명이 성립한다. 파라미터로 뽑으면 호출부에서 그 3줄을 재작성해야 하고 중복 방어의 형태가 바뀐다 |
| `Step = (int)EStep.Measure;` | **RunGrab 잔류(바깥 if 밖)** | 이미지가 없어도 항상 실행되는 기존 동작 |

신규 파라미터는 `UpdateViewerCopy(HImage image)` 단 1개이고, 이름을 호출자 지역변수와 **동일**하게(`image`) 지어
옮겨간 본문의 **토큰 변경이 0건**이다. `ShotParam` / `pMyContext` 는 클래스 멤버 → 파라미터로 승격 금지.

### G-3. `#if SIMUL_MODE` 취급 — 이번 작업 최대 위험
- `#if` / `#else` / `#endif` **3줄은 항상 같은 메서드 안에 함께** 있어야 한다(전부 `AcquireShotImage` 로).
  하나라도 메서드 경계를 넘으면 컴파일이 깨지거나(CS1027) 한쪽 빌드에서만 조용히 다른 코드가 된다.
- `bIsLiveGrabAttempt` 는 **SIMUL 빌드에선 절대 true 가 되지 않는다** — 그래도 `#else` 밖(L406)의 조건식에서
  읽히므로 오늘도 경고가 없다. 추출 후에도 선언·대입·읽기가 **같은 메서드 안**에 있어야 이 상태가 유지된다.
  (선언만 호출부에 남기고 읽기를 헬퍼로 보내는 식의 분할은 **금지**.)
- **검증은 반드시 2-빌드**: Debug|x64(SIMUL ON) + 비-SIMUL(§G-7). 한쪽만 하면 반대쪽 회귀를 못 잡는다.

### G-4. 보존 대상 주석 — 삭제 0건, 로직 따라 이동만
| 앵커 | 현재 위치 | 이동 후 |
|------|-----------|---------|
| `260811 hbk plc-spec-260811-alignment` (L384 꼬리주석 + L403–405 3줄) | 하드웨어 에러 E 판정 스펙 | `AcquireShotImage` 안 동일 상대 위치 |
| `quick-260813-jnh` (L394) | MIL 미러 역추적 | `AcquireShotImage` 안 `#else` 분기 |
| `ShotParam.SimulImagePath = InspectionImagePath 역할` (L385) | 선언 아래 | `AcquireShotImage` 안 동일 위치 |
| `260810 hbk quick-260810-egx` (L415) | 표시사본 위 | `UpdateViewerCopy` 본문 **첫 줄** (메서드 선언 위가 아님 — §G-6 검증식이 본문 첫 줄을 요구한다) |
| `260618 hbk Phase 54 ALIGN-01` (L412–413) | SetImage 위 | **RunGrab 잔류** |
| `260818 hbk [SEQ]` (L381, L428) | tact / 요약 로그 | **RunGrab 잔류** |

### G-5. 코딩 컨벤션 (하드)
- **삼항 `?:` 금지** — if-else 만. 신규 삼항 0개
- **C# 7.2 only** — switch expression, pattern matching, nullable reference types, record, expression-bodied 신규 멤버 전부 금지
- 헝가리언(`b`/`n`/`sz`/`d`)은 **신규 식별자에만**. 기존 이름 변경 금지(바이트 동치가 깨진다).
  이번엔 신규 파라미터가 `HImage image` 1개뿐이고 형 접두를 붙이면 호출자 이름과 달라져 동치 증명이 깨지므로 **`image` 유지**(이 예외 사유를 SUMMARY 에 적는다)
- **신규 메서드 선언은 K&R** (`private HImage AcquireShotImage() {`) — 파일 우세 스타일.
  옮겨오는 본문 내부는 **원본 그대로**(재포맷·재정렬 금지, diff 노이즈 = 대조 방해)
- 신규 주석 접두 `//260819 hbk`, 비자명한 "왜"만

### G-6. 신규 주석 금칙어 (자기모순 검증 방지)
검증식이 파일 전역 카운트를 `==` 로 못박으므로, **새로 쓰는 주석에 아래 문자열을 넣지 말 것**:
- `AcquireShotImage` / `UpdateViewerCopy` (각각 "선언 1 + 호출 1 = 정확히 2" 를 요구한다.
  메서드 **선언 위**의 설명 주석에도 이름을 쓰지 말 것)
- `bIsLiveGrabAttempt` / `swGrabTotal` / `parentSeqForView` / `bSkipViewer` / `parentSeqForMirror` /
  `parentSeqForHwErr` / `IsViewerUpdateSkipped(` / `MarkCycleHardwareError()` / `LoadShotInspectionImage()` /
  `image.CopyImage()` / `pMyContext.ResultHalconImage` / `HImage image = null;`
- `#if` / `#else` / `#endif` 문자열
- `?` 뒤에 같은 줄에서 `:` 가 오는 형태 (삼항 검출 오탐)
- `=> ` (화살표 카운트 baseline 1 유지)

**diff 범위 규칙(앵커 대신 고정 길이):** 이 파일엔 닮은꼴 메서드 `GrabOrLoadDatumImage` 가 있어 문자열 앵커가 유일하지 않다.
따라서 신규 메서드 **선언줄 번호 L** 을 `grep -n` 으로 구하고 `L+1 .. L+N` 을 본문으로 잡는다.
→ **설명 주석은 반드시 선언줄 "위"에 쓰고, 본문 첫 줄은 원본 첫 줄이어야 한다.**

### G-7. 빌드 규칙 — **2-빌드 필수** (실측 baseline 확정, 2026-08-19)
- 앱이 `D:\Data\` 에서 실행 중일 수 있다 → **프로세스 종료 절대 금지.** 스크래치 `OutputPath` 로 컴파일만 검증
- **`//p:` 금지, `-p:` 사용** (`/` 섞이면 Git Bash 가 `MSB1001` 로 죽는다)
- **`-p:OutputPath="$SCR\\xxx\\"` 후행 백슬래시는 반드시 `\\`** — `\"` 로 끝내면 bash unexpected EOF 로 빌드가 아예 안 돈다(vih 실제 blocker)

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"

# (1) SIMUL 빌드 — 기대: error CS 0 / warning CS 12 (CS0618×10 + CS0162×2)
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\fik-t1-simul\\" \
  -t:Rebuild -v:minimal -nologo 2>&1 | tee "$SCR/fik-t1-simul.log" >/dev/null

# (2) 비-SIMUL 빌드 — ⚠ -p:DefineConstants=TRACE 없으면 가짜 검증이다(csproj L74 에 SIMUL_MODE 로컬 오염)
#     기대: error CS 0 / warning CS 10 (CS0618×10, CS0162 0건 ← SIMUL 이 정말 꺼졌다는 증거)
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Release -p:Platform=x64 -p:DefineConstants=TRACE \
  -p:OutputPath="$SCR\\fik-t1-nosimul\\" -t:Rebuild -v:minimal -nologo 2>&1 | tee "$SCR/fik-t1-nosimul.log" >/dev/null
```
판정:
```bash
[ "$(grep -cE 'error CS' "$SCR/fik-t1-simul.log")"   = "0"  ] &&
[ "$(grep -cE 'warning CS' "$SCR/fik-t1-simul.log")" = "12" ] &&
[ "$(grep -cE 'error CS' "$SCR/fik-t1-nosimul.log")"   = "0"  ] &&
[ "$(grep -cE 'warning CS' "$SCR/fik-t1-nosimul.log")" = "10" ] &&
[ "$(grep -c 'warning CS0162' "$SCR/fik-t1-nosimul.log")" = "0" ] &&
[ "$(grep -cE 'CS0219|CS0168|CS0177|CS0165|CS0103|CS1027|CS1028' "$SCR/fik-t1-simul.log" "$SCR/fik-t1-nosimul.log")" = "0" ]
```
파일 잠김으로 실패하면 OutputPath 를 새 이름으로 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**
Task2 에서는 로그 파일명을 `fik-t2-*` 로 바꿔 같은 판정을 반복한다.

### G-8. 셸 변수는 호출 사이에 살아남지 않는다
Bash 호출마다 셸이 새로 뜬다. `$F` / `$SCR` / `$BASE` / `$MSB` 를 쓰는 **모든 블록의 첫 줄에서 다시 정의**할 것:
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=7708808   # 착수 시점 HEAD — 모든 diff 대조의 유일한 기준점
```
정의 없이 실행하면 경로가 빈 문자열이 되어 **조용히 오탐**한다.

### G-9. Grep 규칙
- **모든 grep 에 대상 파일 경로 명시** (없으면 stdin 대기로 멈춤)
- 개수 기준은 `^[[:space:]]*` 앵커 또는 코드 토큰으로 좁힌다
- 신규 식별자 카운트는 **선언줄 포함**해서 센다 (`AcquireShotImage` = 선언 1 + 호출 1 = **2**)
- **삼항 검출은 줄 단위**: `grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l` → **1** (기존 주석 L1307 1줄).
  `-o`(매치 단위)로 바꾸면 문자열 리터럴에서 오탐한다
</ground_rules>

<tasks>

<task type="auto">
  <name>Task 1: RunGrab 의 촬영+하드웨어에러 28줄(L383–410) → AcquireShotImage() 추출</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
**0단계 — 기준점 고정 (Task 1·2 통틀어 1회만). 이후 모든 대조의 근거다.**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=$(git rev-parse --short HEAD)               # 7708808 이어야 함. 다르면 중단
[ -f "$SCR/fik-git-baseline.txt" ] || git status --porcelain > "$SCR/fik-git-baseline.txt"
# 라인 멀티셋 baseline (앞뒤 공백 제거 후 정렬) — "삭제된 줄 0" 증명용
sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort > "$SCR/fik-base-lines.txt"
sed -n '383,410p' $F > "$SCR/fik-before-acquire.txt"   # 28줄
sed -n '415,425p' $F > "$SCR/fik-before-viewer.txt"    # 11줄
wc -l "$SCR/fik-before-acquire.txt" "$SCR/fik-before-viewer.txt"   # 28 / 11
```
**착수 전 2-빌드(§G-7)를 지금 1회 돌려** `$SCR/fik-base-simul.log` / `$SCR/fik-base-nosimul.log` 로 저장하고
`error 0 / warning 12` · `error 0 / warning 10(CS0162 0)` 을 눈으로 확인한다.
(플래너가 2026-08-19 실측한 값이지만, 기억이 아니라 **이 파일**을 기준으로 비교한다.)

---

**1단계 — L383–410 (28줄) 을 잘라내고 그 자리에 호출 1줄을 넣는다.**

잘라내기 시작 = L383 `                HImage image = null;`
잘라내기 끝   = L410 `                }`  (하드웨어 에러 `if` 를 닫는 중괄호)
**L382 `var swGrabTotal = Stopwatch.StartNew();` 와 L411 `if (image != null) {` 은 그대로 둔다.**

그 자리에 넣을 1줄 (들여쓰기 16칸):
```csharp
                HImage image = AcquireShotImage();
```

**손으로 다시 타이핑하지 말 것.** 토큰 변경을 물리적으로 불가능하게 하려면 원본을 잘라 붙인다:
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
sed -n '383,410p' $F | sed 's/^    //' > "$SCR/fik-acquire-body.txt"   # 4칸 dedent(16→12)
```
`$SCR/fik-acquire-body.txt` 를 **그대로** 새 메서드 본문에 붙여넣는다.

---

**2단계 — 신규 메서드를 `RunGrab` 본체 닫는 `}` 바로 아래**(즉 `RunMeasure` 설명 주석 블록 **앞**)**에 추가한다.**

```csharp
        //260819 hbk Extract Method: RunGrab 의 촬영 구역을 그대로 옮긴 것(순수 이동, 동작 무변경).
        //  ⚠ 조건부 컴파일 3줄이 반드시 이 메서드 안에 함께 있어야 한다. 하나라도 경계를 넘으면
        //    한쪽 빌드에서만 조용히 다른 코드가 된다. Debug(SIMUL ON)/비-SIMUL 두 빌드로 검증한다.
        //  ⚠ 실기 grab 여부 플래그는 선언·대입·읽기가 전부 이 메서드 안에 있어야 한다 —
        //    쪼개면 SIMUL 빌드에서 "대입되지만 사용 안 됨" 경고(CS0219 계열)가 새로 생긴다.
        //  ⚠ tact Stopwatch 는 호출부(RunGrab)에 남겼다. 여기로 옮기면 측정 구간이 달라져 [SEQ] 로그 숫자가 바뀐다.
        private HImage AcquireShotImage() {
            <여기에 $SCR/fik-acquire-body.txt 28줄을 그대로 붙여넣는다. 토큰 변경 0건>
            return image;
        }
```
> `return image;` 는 이 추출로 새로 생기는 **유일한 실행문**이다. 원본 L383 이 `HImage image = null;` 로
> 초기화하므로 모든 경로에서 확정 할당된다(CS0165 없음).

**절대 하지 말 것:** `#if` 분기 재구성, `image == null && bIsLiveGrabAttempt` 조건 정리, 조기 return 도입,
로그 문자열 수정, `ShotParam` 파라미터 승격, 본문 재들여쓰기 이외의 편집.

---

**3단계 — 2-빌드 + 정적 검증 (커밋 전).** verify 블록 **1·2·3 + §G-7 2-빌드**를 여기서 실행한다.
verify 블록 **4(HYGIENE)는 여기서 실행하지 말 것** — `git show HEAD` 로 커밋 결과를 보므로 커밋 전엔 직전 커밋(`7708808`)을 보고 오판한다.

**4단계 — 커밋. `git add -A` 금지, 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only          # 정확히 1줄이어야 함
git commit -m "refactor(260819-fik): RunGrab 촬영 구역을 AcquireShotImage 로 추출 (순수 이동, 동작 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M" (unstaged)
```

**5단계 — 커밋 후 verify 블록 4(HYGIENE) 실행** (블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
# [1] 구조 — 신규 메서드 1개 + 호출부 1줄 + 조건부 컴파일 배치
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -cE '^[[:space:]]*private HImage AcquireShotImage\(\) \{$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*HImage image = AcquireShotImage\(\);$' $F)" = "1" ] && \
echo "== 신규 식별자는 선언+호출 정확히 2건 (주석에 이름 쓰지 않았다) ==" && \
[ "$(grep -cF 'AcquireShotImage' $F)" = "2" ] && \
echo "== 조건부 컴파일 파일 전역 불변 ==" && \
[ "$(grep -cE '^[[:space:]]*#if SIMUL_MODE$' $F)" = "3" ] && \
[ "$(grep -cE '^[[:space:]]*#else$' $F)" = "3" ] && \
[ "$(grep -cE '^[[:space:]]*#endif$' $F)" = "3" ] && \
echo "== #if 3줄이 AcquireShotImage 안에 함께 있다 (선언 다음 30줄 안) ==" && \
L=$(grep -nE '^[[:space:]]*private HImage AcquireShotImage\(\) \{$' $F | cut -d: -f1) && \
[ "$(sed -n "$((L+1)),$((L+30))p" $F | grep -cE '^[[:space:]]*#if SIMUL_MODE$')" = "1" ] && \
[ "$(sed -n "$((L+1)),$((L+30))p" $F | grep -cE '^[[:space:]]*#else$')" = "1" ] && \
[ "$(sed -n "$((L+1)),$((L+30))p" $F | grep -cE '^[[:space:]]*#endif$')" = "1" ] && \
echo "== 플래그 선언·대입·읽기가 전부 같은 메서드 안 (3건 모두 L+1..L+30) ==" && \
[ "$(sed -n "$((L+1)),$((L+30))p" $F | grep -cF 'bIsLiveGrabAttempt')" = "3" ] && \
[ "$(grep -cF 'bIsLiveGrabAttempt' $F)" = "3" ] && \
echo "== return image; 1건 추가, 선언 순서 RunGrab 앞 ==" && \
[ "$(sed -n "$((L+29))p" $F | sed 's/^[[:space:]]*//')" = "return image;" ] && \
[ "$(sed -n "$((L+30))p" $F | sed 's/^[[:space:]]*//')" = "}" ] && \
[ "$(grep -n 'private void RunGrab() {' $F | cut -d: -f1)" -lt "$L" ] && \
echo "T1 STRUCTURE PASS"
    </automated>
    <automated>
# [2] ⭐바이트 동치 증명 — 옮겨간 28줄이 선행공백 제거 후 원본(BASE L383-410)과 완전히 같은가
# ⚠ 이 파일엔 닮은꼴 GrabOrLoadDatumImage(L817~)가 있어 문자열 앵커가 유일하지 않다 →
#   신규 선언줄 위치 L 을 구해 L+1..L+28 고정 길이로 범위를 잡는다(주석은 선언 '위'에 있어야 성립).
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
BASE=7708808 && \
L=$(grep -nE '^[[:space:]]*private HImage AcquireShotImage\(\) \{$' $F | cut -d: -f1) && \
[ "$(grep -cE '^[[:space:]]*private HImage AcquireShotImage\(\) \{$' $F)" = "1" ] && \
diff <(git show $BASE:$F | sed -n '383,410p' | sed 's/^[[:space:]]*//') \
     <(sed -n "$((L+1)),$((L+28))p" $F | sed 's/^[[:space:]]*//') \
&& echo "T1 BYTE-EQUIV PASS (28 lines, diff empty)"
    </automated>
    <automated>
# [3] 라인 멀티셋(삭제 0) + 파일 전역 앵커 불변 카운트 + 보존 주석
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
echo "== 삭제된 줄 0 (순수 추출이므로 추가만 있어야 함) ==" && \
[ "$(comm -23 "$SCR/fik-base-lines.txt" <(sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort) | wc -l)" = "0" ] && \
echo "== 앵커 카운트 ==" && \
[ "$(grep -cF 'swGrabTotal' $F)" = "2" ] && \
[ "$(grep -cF 'parentSeqForMirror' $F)" = "2" ] && \
[ "$(grep -cF 'parentSeqForHwErr' $F)" = "4" ] && \
[ "$(grep -cF 'parentSeqForView' $F)" = "4" ] && \
[ "$(grep -cF 'bSkipViewer' $F)" = "2" ] && \
[ "$(grep -cF 'IsViewerUpdateSkipped(' $F)" = "3" ] && \
[ "$(grep -cF 'MarkCycleHardwareError()' $F)" = "2" ] && \
[ "$(grep -cF 'LoadShotInspectionImage()' $F)" = "4" ] && \
[ "$(grep -cF 'image.CopyImage()' $F)" = "2" ] && \
[ "$(grep -cF 'pMyContext.ResultHalconImage' $F)" = "7" ] && \
[ "$(grep -cE '^[[:space:]]*HImage image = null;$' $F)" = "3" ] && \
[ "$(grep -cE '^[[:space:]]*Step = \(int\)EStep\.Measure;$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*ShotParam\.SetImage\(image\);' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*image\.Dispose\(\);' $F)" = "1" ] && \
[ "$(grep -cF 'LogSeqStep("Grab", string.Format("검사 이미지 촬영 완료 ({0:F2}초)",' $F)" = "1" ] && \
echo "== 범위 밖 무회귀 ==" && \
[ "$(grep -cE '^[[:space:]]*case EStep\.[A-Za-z]+:' $F)" = "6" ] && \
[ "$(grep -cE '^[[:space:]]*case ECrossZGate\.[A-Za-z]+:' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*if \(ShotParam != null\) \{$' $F)" = "4" ] && \
echo "== 보존 주석 5계열 ==" && \
[ "$(grep -cF '260811 hbk plc-spec-260811-alignment' $F)" -ge 2 ] && \
[ "$(grep -cF 'quick-260813-jnh' $F)" -ge 2 ] && \
[ "$(grep -cF '260810 hbk quick-260810-egx' $F)" -ge 2 ] && \
[ "$(grep -cF '260618 hbk Phase 54 ALIGN-01' $F)" -ge 1 ] && \
echo "T1 INVARIANT PASS"
    </automated>
    <automated>
# [4] HYGIENE — ⚠ 반드시 **커밋 이후** 실행 (git show HEAD 로 커밋 결과 검사). SCR 재정의 필수.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
echo "== 코드 삼항 0건 (남는 1줄은 기존 주석 L1307) ==" && \
[ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "1" ] && \
echo "== C# 7.2: expression-bodied 증가 0 (baseline 1) ==" && [ "$(grep -c '=> ' $F)" = "1" ] && \
echo "== 커밋에 대상 파일만 ==" && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'Action_FAIMeasurement.cs' && \
[ "$(git show --name-only --format= HEAD | grep -c 'DatumMeasurement.csproj')" = "0" ] && \
echo "== csproj 로컬 변경이 unstaged 로 그대로 ==" && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
diff <(cut -c4- "$SCR/fik-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[0-2]$' && \
echo "T1 HYGIENE PASS"
    </automated>
    <automated>§G-7 **2-빌드 모두** 실행(`fik-t1-simul.log` / `fik-t1-nosimul.log`) → SIMUL: error 0 / warning CS 12. 비-SIMUL(`-p:DefineConstants=TRACE` 필수): error 0 / warning CS 10 / `warning CS0162` 0건. 양쪽 로그에서 CS0219·CS0168·CS0177·CS0165·CS0103·CS1027·CS1028 이 1건이라도 뜨면 즉시 중단</automated>
  </verify>
  <done>
`AcquireShotImage()` private 메서드 1개 신규 + `RunGrab` 안 호출 1줄(`HImage image = AcquireShotImage();`).
옮겨간 28줄이 들여쓰기 정규화 후 BASE(7708808) L383–410 과 **diff 0**(토큰 변경 0건), 신규 실행문은 `return image;` 1줄뿐.
`#if SIMUL_MODE`/`#else`/`#endif` 가 새 메서드 안에 1쌍으로 함께 있고 파일 전역 카운트 3/3/3 불변.
`bIsLiveGrabAttempt` 3건 전부 같은 메서드 안. `swGrabTotal` / `SetImage` / `Dispose` / `Step=Measure` 는 RunGrab 잔류.
라인 멀티셋 삭제 0. 앵커 카운트 전부 착수 전과 동일. 코드 삼항 0건.
**SIMUL(12경고) + 비-SIMUL(10경고, CS0162 0) 두 빌드 모두 error 0.** 커밋 1개, 스테이징 파일 정확히 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 2: 표시사본 11줄(L415–425) → UpdateViewerCopy(HImage image) 추출 + RunGrab 최종 골격 검증</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
**전제:** Task 1 커밋 완료. Task 1 편집으로 L415 이후 줄번호가 **밀렸다**
(28줄 삭제 + 호출 1줄 삽입 = -27). **현재 파일에서 BASE 줄번호를 그대로 쓰지 말 것.**
- diff 의 **BASE 쪽만** `git show $BASE:$F | sed -n '415,425p'` 로 고정 사용
- 현재 파일 쪽은 **앵커 grep 으로 위치를 구한다**

---

**1단계 — 현재 위치 확인 및 원본 조각 준비**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
V=$(grep -nF '//260810 hbk quick-260810-egx: 아래는 "표시 전용" 사본(127MP memcpy).' $F | cut -d: -f1)
echo "start=$V"                                  # 이 앵커는 파일에 1건이어야 한다
[ "$(grep -cF '//260810 hbk quick-260810-egx: 아래는 "표시 전용" 사본(127MP memcpy).' $F)" = "1" ] || echo "ANCHOR NOT UNIQUE -> STOP"
sed -n "${V},$((V+10))p" $F                      # 11줄: 주석1 + parentSeqForView 3 + bSkipViewer 1 + dispose 1 + if/else 4 + 닫는 }
sed -n "${V},$((V+10))p" $F | sed 's/^        //' > "$SCR/fik-viewer-body.txt"   # 8칸 dedent(20→12)
wc -l "$SCR/fik-viewer-body.txt"                 # 11
```
> 참고: 이 구역은 `if (image != null) {` 안(20칸 들여쓰기)이라 새 메서드 본문(12칸)으로 **8칸** dedent 한다.
> Task 1 의 4칸 dedent 와 값이 다르다 — 헷갈리지 말 것.

---

**2단계 — 그 11줄을 잘라내고 그 자리에 호출 1줄을 넣는다.**

잘라내기 시작 = `//260810 hbk quick-260810-egx: 아래는 "표시 전용" 사본(127MP memcpy).` 줄
잘라내기 끝   = 그로부터 10줄 뒤(= `else` 블록을 닫는 `}`)
**바로 위 `ShotParam.SetImage(image);` 줄과 바로 아래 `image.Dispose();` 줄은 그대로 둔다.**

그 자리에 넣을 1줄 (들여쓰기 20칸):
```csharp
                    UpdateViewerCopy(image);
```

---

**3단계 — 신규 메서드를 `AcquireShotImage` 본체 닫는 `}` 바로 아래에 추가한다.**

```csharp
        //260819 hbk Extract Method: RunGrab 의 화면표시용 사본 처리를 그대로 옮긴 것(순수 이동, 동작 무변경).
        //  ⚠ 측정 소스 설정(데이터 경로)과 원본 이미지 해제는 호출부에 남겼다 — 여기 들어오면
        //    "조건과 무관하게 항상 수행" 계약이 조기 return 등으로 깨질 여지가 생긴다.
        //  ⚠ 안쪽 null 재확인은 원형 그대로다. 바깥 호출부가 이미 non-null 을 보장하지만,
        //    지우면 순수 이동이 아니게 되므로 중복 방어를 유지한다.
        private void UpdateViewerCopy(HImage image) {
            <여기에 $SCR/fik-viewer-body.txt 11줄을 그대로 붙여넣는다. 토큰 변경 0건>
        }
```
> 파라미터 이름은 호출자 지역변수와 같은 `image` 다(§G-5 예외). 헝가리언 접두를 붙이면 본문 토큰이 바뀌어
> 바이트 동치 증명이 깨진다 — 이 예외 사유를 SUMMARY 에 적는다.

**절대 하지 말 것:** `if (ShotParam != null) … else parentSeqForView = null;` 를 삼항/한 줄로 정리,
`parentSeqForView` 를 파라미터로 승격, `bSkipViewer` 분기를 뒤집기, 기존 `Dispose()` 호출 위치 변경,
`pMyContext` 파라미터 승격.

---

**4단계 — 2-빌드 + 정적 검증 (커밋 전).** verify 블록 **1·2·3·4 + §G-7 2-빌드**(로그명 `fik-t2-*`) 실행.
verify 블록 **5(HYGIENE)는 커밋 이후**에만 실행.

**5단계 — 커밋. 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only          # 정확히 1줄
git commit -m "refactor(260819-fik): RunGrab 표시사본 처리를 UpdateViewerCopy 로 추출 (순수 이동, 동작 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M"
```

**6단계 — 커밋 후 verify 블록 5(HYGIENE) 실행** (블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
# [1] 구조 — 신규 메서드 1개 + 호출부 1줄
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -cE '^[[:space:]]*private void UpdateViewerCopy\(HImage image\) \{$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*UpdateViewerCopy\(image\);$' $F)" = "1" ] && \
echo "== 신규 식별자는 선언+호출 정확히 2건 ==" && \
[ "$(grep -cF 'UpdateViewerCopy' $F)" = "2" ] && \
[ "$(grep -cF 'AcquireShotImage' $F)" = "2" ] && \
V=$(grep -nE '^[[:space:]]*private void UpdateViewerCopy\(HImage image\) \{$' $F | cut -d: -f1) && \
echo "== 표시사본 본체 요소가 헬퍼 본문(V+1..V+11) 안에 1건씩 ==" && \
[ "$(sed -n "$((V+1)),$((V+11))p" $F | grep -cE '^[[:space:]]*bool bSkipViewer = IsViewerUpdateSkipped\(parentSeqForView\);$')" = "1" ] && \
[ "$(sed -n "$((V+1)),$((V+11))p" $F | grep -cE '^[[:space:]]*if \(pMyContext\.ResultHalconImage != null\) pMyContext\.ResultHalconImage\.Dispose\(\);$')" = "1" ] && \
[ "$(sed -n "$((V+1)),$((V+11))p" $F | grep -cE '^[[:space:]]*pMyContext\.ResultHalconImage = image\.CopyImage\(\);$')" = "1" ] && \
echo "== 파일 전역 카운트 불변 (⚠ ResultHalconImage dispose 는 닮은꼴 크로스-Z 경로에도 1건 있어 총 2건이 정상) ==" && \
[ "$(grep -cE '^[[:space:]]*bool bSkipViewer = IsViewerUpdateSkipped\(parentSeqForView\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*if \(pMyContext\.ResultHalconImage != null\) pMyContext\.ResultHalconImage\.Dispose\(\);$' $F)" = "2" ] && \
[ "$(grep -cE '^[[:space:]]*pMyContext\.ResultHalconImage = image\.CopyImage\(\);$' $F)" = "1" ] && \
echo "== 중복 방어(원형) 유지 — parentSeqForView if/else 2줄 ==" && \
[ "$(grep -cE '^[[:space:]]*if \(ShotParam != null\) parentSeqForView = ShotParam\.Parent as InspectionSequence;$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*else parentSeqForView = null;$' $F)" = "1" ] && \
echo "== 표시사본 헬퍼 안에 #if 0건 (조건부 컴파일은 AcquireShotImage 전용) ==" && \
[ "$(sed -n "$((V+1)),$((V+12))p" $F | grep -cE '#if|#else|#endif')" = "0" ] && \
[ "$(sed -n "$((V+12))p" $F | sed 's/^[[:space:]]*//')" = "}" ] && \
echo "T2 STRUCTURE PASS"
    </automated>
    <automated>
# [2] ⭐바이트 동치 증명 — 옮겨간 11줄이 선행공백 제거 후 BASE L415-425 와 완전히 같은가
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
BASE=7708808 && \
[ "$(grep -cE '^[[:space:]]*private void UpdateViewerCopy\(HImage image\) \{$' $F)" = "1" ] && \
V=$(grep -nE '^[[:space:]]*private void UpdateViewerCopy\(HImage image\) \{$' $F | cut -d: -f1) && \
diff <(git show $BASE:$F | sed -n '415,425p' | sed 's/^[[:space:]]*//') \
     <(sed -n "$((V+1)),$((V+11))p" $F | sed 's/^[[:space:]]*//') && \
echo "== Task1 구역도 여전히 동치인지 재확인(28줄) ==" && \
L=$(grep -nE '^[[:space:]]*private HImage AcquireShotImage\(\) \{$' $F | cut -d: -f1) && \
diff <(git show $BASE:$F | sed -n '383,410p' | sed 's/^[[:space:]]*//') \
     <(sed -n "$((L+1)),$((L+28))p" $F | sed 's/^[[:space:]]*//') \
&& echo "T2 BYTE-EQUIV PASS (11 + 28 lines, diff empty)"
    </automated>
    <automated>
# [3] ⭐RunGrab 최종 골격 — 잔류/순서/Step 위치를 위치값으로 못박는다 (본문 정확히 17줄)
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
G=$(grep -nE '^[[:space:]]*private void RunGrab\(\) \{$' $F | cut -d: -f1) && \
[ "$(grep -cE '^[[:space:]]*private void RunGrab\(\) \{$' $F)" = "1" ] && \
B=$(sed -n "$((G+1)),$((G+17))p" $F | sed 's/^[[:space:]]*//') && \
pos() { printf '%s\n' "$B" | grep -nE "$1" | cut -d: -f1 | tr '\n' ' ' ; } && \
echo "== 위치 고정 ==" && \
[ "$(pos '^var swGrabTotal = Stopwatch\.StartNew\(\);$')" = "3 " ] && \
[ "$(pos '^HImage image = AcquireShotImage\(\);$')" = "4 " ] && \
[ "$(pos '^if \(image != null\) \{$')" = "5 " ] && \
[ "$(pos '^ShotParam\.SetImage\(image\);')" = "8 " ] && \
[ "$(pos '^UpdateViewerCopy\(image\);$')" = "9 " ] && \
[ "$(pos '^image\.Dispose\(\);')" = "10 " ] && \
[ "$(pos '^LogSeqStep\("Grab",')" = "13 " ] && \
[ "$(pos '^swGrabTotal\.Elapsed\.TotalSeconds\)\);$')" = "14 " ] && \
[ "$(pos '^Step = \(int\)EStep\.Measure;$')" = "16 " ] && \
echo "== 마지막 줄이 메서드 닫는 중괄호 + 본문에 #if 0건 ==" && \
[ "$(printf '%s\n' "$B" | sed -n '17p')" = "}" ] && \
[ "$(printf '%s\n' "$B" | grep -cE '#if|#else|#endif')" = "0" ] && \
[ "$(printf '%s\n' "$B" | wc -l)" = "17" ] && \
echo "T2 RUNGRAB SHAPE PASS"
    </automated>
    <automated>
# [4] 라인 멀티셋(삭제 0) + 파일 전역 앵커 불변 카운트 + 보존 주석
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(comm -23 "$SCR/fik-base-lines.txt" <(sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort) | wc -l)" = "0" ] && \
[ "$(grep -cE '^[[:space:]]*#if SIMUL_MODE$' $F)" = "3" ] && \
[ "$(grep -cE '^[[:space:]]*#else$' $F)" = "3" ] && \
[ "$(grep -cE '^[[:space:]]*#endif$' $F)" = "3" ] && \
[ "$(grep -cF 'bIsLiveGrabAttempt' $F)" = "3" ] && \
[ "$(grep -cF 'swGrabTotal' $F)" = "2" ] && \
[ "$(grep -cF 'parentSeqForMirror' $F)" = "2" ] && \
[ "$(grep -cF 'parentSeqForHwErr' $F)" = "4" ] && \
[ "$(grep -cF 'parentSeqForView' $F)" = "4" ] && \
[ "$(grep -cF 'bSkipViewer' $F)" = "2" ] && \
[ "$(grep -cF 'IsViewerUpdateSkipped(' $F)" = "3" ] && \
[ "$(grep -cF 'MarkCycleHardwareError()' $F)" = "2" ] && \
[ "$(grep -cF 'LoadShotInspectionImage()' $F)" = "4" ] && \
[ "$(grep -cF 'image.CopyImage()' $F)" = "2" ] && \
[ "$(grep -cF 'pMyContext.ResultHalconImage' $F)" = "7" ] && \
[ "$(grep -cE '^[[:space:]]*HImage image = null;$' $F)" = "3" ] && \
[ "$(grep -cE '^[[:space:]]*case EStep\.[A-Za-z]+:' $F)" = "6" ] && \
[ "$(grep -cE '^[[:space:]]*case ECrossZGate\.[A-Za-z]+:' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*if \(ShotParam != null\) \{$' $F)" = "4" ] && \
[ "$(grep -cF '260811 hbk plc-spec-260811-alignment' $F)" -ge 2 ] && \
[ "$(grep -cF 'quick-260813-jnh' $F)" -ge 2 ] && \
[ "$(grep -cF '260810 hbk quick-260810-egx' $F)" -ge 2 ] && \
[ "$(grep -cF '260618 hbk Phase 54 ALIGN-01' $F)" -ge 1 ] && \
echo "T2 INVARIANT PASS"
    </automated>
    <automated>
# [5] HYGIENE — ⚠ 반드시 **커밋 이후** 실행. SCR 재정의 필수.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "1" ] && \
[ "$(grep -c '=> ' $F)" = "1" ] && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'Action_FAIMeasurement.cs' && \
[ "$(git show --name-only --format= HEAD | grep -c 'DatumMeasurement.csproj')" = "0" ] && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
echo "== 두 커밋 합쳐도 변경 파일은 대상 1개뿐 ==" && \
[ "$(git diff --name-only 7708808 HEAD | wc -l)" = "1" ] && \
diff <(cut -c4- "$SCR/fik-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[0-2]$' && \
echo "T2 HYGIENE PASS"
    </automated>
    <automated>§G-7 **2-빌드 모두** 실행(`fik-t2-simul.log` / `fik-t2-nosimul.log`) → SIMUL: error 0 / warning CS 12. 비-SIMUL(`-p:DefineConstants=TRACE` 필수): error 0 / warning CS 10 / `warning CS0162` 0건. 양쪽 로그에서 CS0219·CS0168·CS0177·CS0165·CS0103·CS1027·CS1028 0건</automated>
  </verify>
  <done>
`UpdateViewerCopy(HImage image)` private 메서드 1개 신규 + `RunGrab` 안 호출 1줄.
옮겨간 11줄이 들여쓰기 정규화 후 BASE L415–425 와 **diff 0**, Task1 의 28줄 동치도 재확인.
`parentSeqForView` 중복 방어 if/else 2줄 원형 유지, `bSkipViewer` 분기 무변경.
**RunGrab 최종 본문 17줄**에서 위치 고정 검증 통과 — `HImage image = AcquireShotImage();`(4) /
`ShotParam.SetImage`(8) / `UpdateViewerCopy(image);`(9) / `image.Dispose();`(10) / `Step = (int)EStep.Measure;`(16),
본문 `#if` 0건.
라인 멀티셋 삭제 0, 앵커 카운트 전부 착수 전과 동일, 코드 삼항 0건.
**SIMUL(12경고) + 비-SIMUL(10경고, CS0162 0) 두 빌드 모두 error 0.**
커밋 1개, `7708808..HEAD` 변경 파일 총 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 3: 동치 증명 SUMMARY 작성 (정적 검증만으로 무회귀 증명)</name>
  <files>.planning/quick/260819-fik-rungrab-acquireshotimage-updateviewercop/260819-fik-SUMMARY.md</files>
  <action>
"빌드 통과했으니 OK" 는 근거로 인정하지 않는다. 사용자 원문: **"커밋 메시지 주장만 믿지 말고
커밋마다 동작 무변경을 코드로 직접 재확인"**. 아래 7개 절을 **실제 명령 출력**으로 채운다(요약·의역 금지).

**① 바이트 동치 증명 (핵심)**
| 추출 | 원본 범위(@7708808) | dedent | 신규 실행문 | diff 결과 |
|------|---------------------|--------|-------------|-----------|
| ① AcquireShotImage | L383–410 (28줄) | 4칸 | `return image;` 1줄 | (붙여넣기: 비어 있어야 함) |
| ② UpdateViewerCopy | L415–425 (11줄) | 8칸 | 없음 | (붙여넣기: 비어 있어야 함) |

**② 라인 멀티셋 대조 — 삭제된 실행줄 0**
`comm -23` 출력이 비어 있음을 붙이고 "순수 추출이므로 추가만 있고 삭제는 0" 이라는 논리를 기술한다.
추가된 줄(신규 선언 2 + 설명 주석 + 중괄호 + 호출 2 + `return image;`) 수도 함께 제시한다.

**③ ⭐2-빌드 표 (이번 작업 고유 리스크)**
| 빌드 | 명령 | error CS | warning CS | CS0162 | 판정 |
|------|------|----------|-----------|--------|------|
| SIMUL (Debug\|x64) | (그대로) | 0 | 12 | 2 | |
| 비-SIMUL (Release\|x64 + `-p:DefineConstants=TRACE`) | (그대로) | 0 | 10 | 0 | |
- **왜 2-빌드가 필요했는지**: 추출 구역에 `#if SIMUL_MODE`/`#else`/`#endif` 가 들어 있어 한쪽 빌드만으로는
  반대쪽 회귀(CS0219 미사용 지역변수 / CS0103 미정의 / CS1027 지시문 불일치)를 못 잡는다.
- **왜 `-p:DefineConstants=TRACE` 가 필수였는지**: 워킹트리 csproj L74(Release\|x64)에 로컬로 `SIMUL_MODE` 가
  들어가 있어 그냥 빌드하면 "Release 인데 SIMUL 경로가 컴파일되는" **가짜 비-SIMUL 검증**이 된다.
  진짜로 꺼졌다는 증거 = 비-SIMUL 로그에서 `warning CS0162`(SIMUL 전용 도달불가 코드) 가 **0건**.
- 착수 전 baseline 로그(`fik-base-*.log`)와 착수 후(`fik-t2-*.log`) 수치가 동일함을 제시.

**④ 잔류 결정 4건 — "왜 헬퍼로 옮기지 않았는가"**
`swGrabTotal`(tact 구간 보존) / `ShotParam.SetImage`(데이터 경로) / `image.Dispose()`(소유권 종료,
"조건과 무관하게 항상 수행" 계약) / `Step = (int)EStep.Measure;`(바깥 if 밖, 이미지 없어도 실행) —
각각 옮겼을 때 무엇이 깨지는지 한 줄씩.

**⑤ RunGrab 최종 골격**
검증 [3]의 위치 고정 출력(본문 17줄, 각 요소의 줄 위치)을 그대로 붙인다.

**⑥ 원형 보존 항목**
- `parentSeqForView` 중복 방어 if/else 2줄을 **지우지 않은** 이유(바깥 if 가 non-null 을 보장해도 제거하면 순수 이동이 아님)
- 파라미터 이름을 `image` 로 둔 이유(헝가리언 접두를 붙이면 본문 토큰이 바뀌어 바이트 동치가 깨진다) — §G-5 예외로 명시
- 보존 주석 5계열 삭제 0건 (현재 줄번호와 함께)

**⑦ 사용자 UAT 요청 (실기)**
정적 증명이 커버하지 못하는 것 = **실행 시 동작**. 아래 3개만 확인 요청:
1. SIMUL 로 1 사이클 검사 → `[SEQ] Grab` 로그의 "검사 이미지 촬영 완료 (n.nn초)" 가 이전과 같은 형태로 찍히는지
2. 표시 사본 경로 — 화면에 검사 이미지가 이전과 동일하게 뜨는지(`DisableViewerDuringAutoInspect` ON/OFF 각 1회)
3. 실기: 카메라 grab 실패를 인위적으로 만들었을 때 `$RESULT` 가 F 가 아니라 **E** 로 나가는지(하드웨어 에러 경로)

마지막에 **커밋 2개 해시 + 각 커밋의 변경 파일 목록**을 붙인다.
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && \
S=.planning/quick/260819-fik-rungrab-acquireshotimage-updateviewercop/260819-fik-SUMMARY.md && \
[ -f "$S" ] && \
grep -qF '383' "$S" && grep -qF '415' "$S" && \
grep -qF 'DefineConstants=TRACE' "$S" && \
grep -qF 'CS0162' "$S" && \
grep -qF 'comm -23' "$S" && \
grep -qF '7708808' "$S" && \
[ "$(grep -c 'warning' "$S")" -ge 2 ] && \
echo "T3 SUMMARY PASS"
    </automated>
  </verify>
  <done>
SUMMARY 7개 절이 실제 명령 출력으로 채워짐 — 바이트 동치 diff 2건(빈 출력) / 라인 멀티셋 삭제 0 /
**2-빌드 전후표(SIMUL 12경고, 비-SIMUL 10경고·CS0162 0)** / 잔류 결정 4건 근거 / RunGrab 최종 골격 17줄 /
원형 보존 항목 / 실기 UAT 3건 요청 / 커밋 2개 해시.
  </done>
</task>

</tasks>

<verification>
1. 바이트 동치: `AcquireShotImage` 28줄 + `UpdateViewerCopy` 11줄, 정규화 후 BASE(7708808) 대비 diff 빈 출력
2. 라인 멀티셋: `comm -23` 삭제 0줄
3. RunGrab 최종 본문 17줄 위치 고정 통과, `Step = (int)EStep.Measure;` 16번째(바깥 if 밖) 유지
4. `#if SIMUL_MODE`/`#else`/`#endif` 파일 전역 3/3/3, 신규 메서드 안에 1쌍 함께
5. **2-빌드**: SIMUL error 0/warning 12, 비-SIMUL(`-p:DefineConstants=TRACE`) error 0/warning 10/CS0162 0
6. 신규 CS0219·CS0168·CS0177·CS0165·CS0103·CS1027·CS1028 0건
7. 코드 삼항 0건(주석 1줄만), `=> ` 1건
8. `git diff --name-only 7708808 HEAD` = 1파일, csproj unstaged 유지
</verification>

<success_criteria>
- `Action_FAIMeasurement.cs` 에 private 메서드 2개 신규 + 호출 2줄, 그 외 변경 0
- 옮겨간 39줄 전부 바이트 동치(토큰 변경 0건), 신규 실행문은 `return image;` 1줄뿐
- 두 빌드 모두 error 0 + 경고 baseline 동일
- 커밋 2개(추출 1건당 1개) + SUMMARY 1개, csproj 무접촉
</success_criteria>

<output>
완료 후 `.planning/quick/260819-fik-rungrab-acquireshotimage-updateviewercop/260819-fik-SUMMARY.md` 작성
</output>
