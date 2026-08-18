---
phase: quick-260818-ruh
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [RUH-01, RUH-02, RUH-03]

must_haves:
  truths:
    - "크로스-Z 게이트가 `private enum ECrossZGate` + 고전 `switch/case/break` 5-case 로 재구성되고, 원본 7갈래가 1:1 로 보존된다"
    - "갈래 #3(프로토콜 · !bRelevant)은 여전히 `measuredCount` 를 증가시키지 않고 `faiAllPass` 도 건드리지 않는다 — 갈래 #2 와 합쳐지지 않았다"
    - "갈래 #5(crossZRoleImage 인수) 부수효과가 원래 실행 시점(= !bCaptureOk 게이트 통과 후, !bCompleted 처리 직전)에 그대로 일어난다 — HalfPending/BothReady 두 case 의 '첫 줄'에서만 호출된다"
    - "부수효과 있는 호출 2개(IsZIndexMisconfigured → ProcessCrossZCaptureTick → IsProtocolDrivenCycle)의 호출 순서와 호출 횟수가 리팩토링 전과 동일하다 — 특히 Misconfigured 경로에서 ProcessCrossZCaptureTick 과 IsProtocolDrivenCycle 이 여전히 호출되지 않는다"
    - "상태 분류 함수 `ResolveCrossZGate(bool,bool,bool)` 는 완전히 순수하다 — 인자 3개 bool 외 어떤 상태도 읽거나 쓰지 않는다"
    - "파일 전역 불변 카운트가 리팩토링 전 실측치와 정확히 동일하다: measuredCount++ 8 / faiAllPass = false 8 / meas.ClearResult() 7 / MarkMeasurementCrossZIncomplete( 4 / SkipReason.NO_IMAGE 2 / TakeCrossZImageCopy(szCapturedRoleKey) 1 / parentSeq2.IsProtocolDrivenCycle() 1 / IsZIndexMisconfigured( 2 / ProcessCrossZCaptureTick 3 / MarkMeasurementZIndexMisconfigured 3"
    - "기존 WHY 주석 3계열(Phase 68 D-02a/D-05, 260729-e9q, 260729-hwb)이 삭제 0건으로 로직이 옮겨간 자리에 함께 이동한다"
    - "msbuild Debug|x64 가 성공하고 경고가 baseline 12줄(CS0618×10 + CS0162×2)과 동일하다 — 신규 CS0219/CS0168 0건"
    - "파일 전체 코드 삼항(?:) 0건 유지 — 정제 grep 결과가 기존 주석 1줄(L1229)만"
    - "Datum 게이트 2개(IsDatumFailed / IsDatumRefUnresolvable)와 ResolveDatumTransform 이후 실행/집계 경로는 1바이트도 바뀌지 않는다"
    - "Action_FAIMeasurement.cs 외 어떤 파일도 스테이징/커밋되지 않는다 — 특히 DatumMeasurement.csproj 의 로컬 미커밋 변경(OutputPath=D:\\Data\\, Release SIMUL_MODE)이 그대로 unstaged 로 남는다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "ECrossZGate enum + switch 5-case 게이트 + 순수 분류함수 ResolveCrossZGate + 부수효과 헬퍼 TakeCrossZRoleImageIfFirst + HalfPending 본문 헬퍼 MarkCrossZHalfPending"
      contains: "private enum ECrossZGate"
  key_links:
    - from: "ProcessOneMeasurement() 게이트 블록"
      to: "ResolveCrossZGate(bRelevant, bCaptureOk, bCompleted)"
      via: "ProcessCrossZCaptureTick 의 out 3개를 그대로 넘겨 enum 만 도출 (분류는 순수, 캡처는 switch 앞에 잔류)"
      pattern: "eGate = ResolveCrossZGate\\(bRelevant, bCaptureOk, bCompleted\\)"
    - from: "case ECrossZGate.HalfPending / case ECrossZGate.BothReady"
      to: "TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref crossZRoleImage)"
      via: "두 case 본문의 '첫 줄' 호출 — 원본 부수효과 위치(#4 게이트와 #6 게이트 사이) 재현"
      pattern: "^[[:space:]]*TakeCrossZRoleImageIfFirst\\(parentSeq2"
    - from: "case ECrossZGate.BothReady"
      to: "L618 ResolveDatumTransform 이하 공용 실행 경로"
      via: "break 로 switch 탈출 → if(bHasAnyZIndex) 블록 탈출 → fall-through (return 아님)"
      pattern: "break; // 완성 index"
---

<objective>
`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 의 `ProcessOneMeasurement()` 안
**크로스-Z 게이트 블록(L544–L617)** 을 중첩 if 에서 **명시적 상태 enum + 고전 switch** 로 재구성한다.

Purpose: 생산 라인 검사 판정 코드다. 사용자 원문(반복 강조) — **"제일중요한건 기존기능 영향 절대없게"**.
따라서 이 작업은 "코드 개선"이 아니라 **의미 보존 변환(behavior-preserving transformation)** 이다.
분기 조건 / 실행 순서 / 부수효과 발생 시점 / `measuredCount` 증감 / `faiAllPass` 갱신이
1비트라도 달라지면 실패다.

Output: 같은 파일 1개. enum 1개 + private 메서드 3개 신규, 게이트 블록이 switch 5-case 로 치환.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**착수 전 필수 확인 (30초). 다르면 즉시 중단하고 사용자에게 보고할 것 — 아래 모든 줄번호가 무효화된다:**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git log -1 --oneline -- $F        # 기대: 12fa8aa refactor(260818-ef5): DatumPhase/Measure 루프 본문을 헬퍼로 추출 …
git status --porcelain $F         # 기대: 출력 없음 (clean)
wc -l $F                          # 기대: 1669
sed -n '544,545p;548p;616,618p' $F
# 기대 출력:
#   //260722 hbk Phase 68 D-02a/D-05: 크로스-Z(ZIndexA/B 둘 다 -1 아님) 측정 게이트/캡처.
#   //  ZIndexA/B 둘 다 -1(미설정) 이면 이 블록 진입 안 함 → 기존 경로 그대로(D-07 회귀 0).
#               if (bHasAnyZIndex)
#                   // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
#               }
#               HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef); …
```

**⚠ 워킹트리 오염 주의 (이번 작업 최대 사고 위험):**
착수 시점 워킹트리에는 **이번 작업과 무관한 미커밋 변경 6개 + 미추적 디렉터리 3개**가 이미 떠 있다
(docx/md 문서 3, `EthernetAlignCamera.cs`, `Custom/SystemSetting.cs`, `Device/DeviceHandler.cs`,
그리고 **`WPF_Example/DatumMeasurement.csproj`**).
csproj 에는 **커밋하면 안 되는 로컬 설정**이 들어 있다 — Debug `OutputPath=D:\Data\`,
Release `DefineConstants` 의 `SIMUL_MODE`.
→ **`git add -A` / `git add .` / `git commit -a` 절대 금지.** 반드시 대상 파일 1개만 경로로 스테이징한다.
→ 따라서 "`git diff --name-only | wc -l` == 1" 같은 검증식도 **쓰면 안 된다** (착수 시점에 이미 6이다).
   대신 착수 전 `git status --porcelain` 스냅샷을 떠 두고 **차집합이 대상 파일 하나뿐**임을 확인한다.
</context>

<ground_rules>
## 이 플랜 전체에 적용되는 절대 규칙

### G-1. 허용되는 변환은 정확히 2종뿐
1. **텍스트 이동** (cut & paste + 균일 재들여쓰기) — 블록을 case 본문 / 새 private 메서드로 옮기기
2. **중첩 if → enum 분류 + switch 디스패치** (아래 §목표 코드에 적힌 형태 그대로)

이 2종 외 **어떤 편집도 금지**:
- 기존 지역변수 리네임 금지 (`parentSeq2` / `dualMeasForGate` / `bHasAnyZIndex` / `crossZRoleImage` 등 전부 유지)
- 조건식 "정리" 금지 — 특히 `bCaptureOk && crossZRoleImage == null && !string.IsNullOrEmpty(szCapturedRoleKey) && parentSeq2 != null`
  의 첫 항 `bCaptureOk` 는 **switch 구조상 이미 참임이 보장되지만 그대로 남긴다** (원문 verbatim 보존이 대조의 근거)
- 방어 코드 추가 / null 체크 추가 / 로그 추가 / 예외 처리 보강 금지
- 주석 삭제 금지 (§G-3)
- 범위 확장 금지 — Datum 게이트 2개(`IsDatumFailed`/`IsDatumRefUnresolvable`)와 `ResolveDatumTransform` 이후
  실행/집계 경로는 **무접촉**. `ProcessCrossZCaptureTick` / `MarkMeasurementCrossZIncomplete` /
  `IsZIndexMisconfigured` **본체도 무접촉**

### G-2. 코딩 컨벤션 (하드)
- **삼항 `?:` 금지** — if-else 만. 신규 삼항 0개
- **C# 7.2 only** — switch expression(`=>`) 금지, pattern matching switch(`case X x when …`) 금지.
  **반드시 고전 `switch (eGate) { case ECrossZGate.X: … return; }`**
- **헝가리언은 신규 지역변수/파라미터에만** (`b`/`n`/`sz`/`d`). 기존 이름 변경 금지
- **브레이스:** 신규 **메서드 선언은 K&R** (`private void Foo() {`) — 파일 39멤버 중 31개가 K&R.
  단 **옮겨오는 본문 안의 `if`/`else` 는 현재 이 구역이 쓰고 있는 Allman(`if (x)` 개행 `{`) 을 그대로 유지**한다.
  이 구역(Phase 68 크로스-Z 계열)은 파일 내 Allman 8개 구역에 속한다 — 스타일 통일하지 말 것(diff 노이즈 = 대조 방해)
- 주석은 비자명한 "왜"만. 신규 주석 접두는 `//260818 hbk`

### G-3. 보존 대상 주석 — 삭제 0건, 로직 따라 이동만
| 앵커 | 현재 위치 | 이동 후 위치 |
|------|-----------|--------------|
| `260722 hbk Phase 68 D-02a/D-05` (2줄) | L544–545 | **그 자리 유지** (`dualMeasForGate` 선언 위) |
| `260729 hbk quick-fix(260729-e9q)` (8줄, L561–568) | ProcessCrossZCaptureTick 직후 | `bNonProtocolCycle` 계산 바로 위 (else 블록 안) |
| `// 프로토콜: 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망, 무변경)` | L578 인라인 | `case NotMyTick:` 의 `return;` 인라인 |
| `260729 hbk quick-fix(260729-hwb)` 캡처 사본 (3줄, L591–593) | if 블록 안 | `TakeCrossZRoleImageIfFirst` 메서드 선언 위 |
| `260729 hbk quick-fix(260729-hwb)` T-HWB-01 (5줄, L605–609) | else 블록 안 | `MarkCrossZHalfPending` 의 else 블록 안 (그대로) |
| `// 프로토콜 Z1(비완성 index): 캡처만 — NG 아님, 미보고(Task4 index 게이트가 보장)` | L613 인라인 | `MarkCrossZHalfPending` 의 `measuredCount++` 인라인 |
| `// 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)` | L616 | `case BothReady:` 의 `break;` 인라인 |

검증: `260729-e9q` 2건 / `260729-hwb` 8건 / `Phase 68 D-02a/D-05` 1건 — **전후 동일하거나 증가**.

### G-4. 빌드 규칙
- 앱이 `D:\Data\` 에서 실행 중일 수 있다 → **프로세스 종료 절대 금지.** 스크래치 `OutputPath` 로 컴파일만 검증
- **`//p:` 금지, `-p:` 사용** (경로에 `/` 가 섞이면 Git Bash 가 `MSB1001` 로 죽는다)
- **경고 baseline = 12줄 (CS0618×10 + CS0162×2).** "경고 0" 을 통과 기준으로 쓰면 항상 거짓 실패
- **비-SIMUL(`#else`) 빌드는 이번엔 불필요.** 근거: 편집 구역(L544–617)에 `#if SIMUL_MODE` 가 0개다
  (`sed -n '517,668p' $F | grep -c '#if'` → 0 으로 착수 전 확인). ef5 때와 달리 조건부 컴파일 경계를
  가로지르지 않으므로 SIMUL 빌드 1종으로 충분하다. 굳이 돌리면 `obj/` 원복 리스크만 추가된다

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\ruh-simul\\" \
  -t:Rebuild -v:minimal -nologo
```
파일 잠김으로 실패하면 OutputPath 를 새 이름으로 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**

### G-5. Grep 규칙 (이 repo 에서 반복 실패한 유형)
- **모든 grep 에 대상 파일 경로를 명시**한다. 경로 없으면 stdin 대기로 멈춘다
- **개수 기준은 반드시 `^[[:space:]]*` 앵커로** 잡는다. 주석에 같은 문자열이 들어가면 무앵커 카운트는 영구 불일치한다
  (선례: `grep -c 'case EStep\.'` 가 9 를 리턴 — 실제 라벨 6 + 주석 3)
- 백슬래시 윈도우 경로를 grep 할 때는 `-F` 를 쓴다 (MSYS 가 경로를 변환해 항상 0 이 나온다)

### G-6. 셸 변수는 호출 사이에 살아남지 않는다
Bash 호출마다 셸이 새로 뜬다. `$F` / `$SCR` / `$BASE` 를 쓰는 **모든 블록의 첫 줄에서 다시 정의**할 것:
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=12fa8aa   # Task 1 착수 시점 HEAD (0단계에서 실측한 값으로 대체)
```
아래 verify 블록 중 `$SCR` 를 참조하는 것이 있다(위생 검증의 `ruh-git-baseline.txt`). 정의 없이 실행하면
경로가 빈 문자열이 되어 조용히 오탐한다.
</ground_rules>

<tasks>

<task type="auto">
  <name>Task 1: ECrossZGate enum 도입 + 게이트 블록을 switch 5-case 로 재구성 (7갈래 1:1 보존)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
**0단계 — 기준점 고정 (이후 모든 대조의 근거):**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
BASE=$(git rev-parse HEAD)                       # 12fa8aa 이어야 함
git status --porcelain > "$SCR/ruh-git-baseline.txt"   # 무관한 dirty 파일 스냅샷
sed -n '544,617p' $F > "$SCR/ruh-before-gate.txt"      # 원본 게이트 블록 보존
sed -n '517,668p' $F | grep -c '#if'             # 기대 0 → 비-SIMUL 빌드 불필요 근거
```
그리고 G-4 빌드를 **착수 전 상태에서 1회** 돌려 경고 줄을 `$SCR/ruh-baseline-warn.txt` 에 저장한다.
이후 모든 경고 비교는 기억이 아니라 이 파일 기준.

---

**1단계 — enum 선언 추가.** `private enum EStep { … }` 블록(L32–40) **바로 아래, 빈 줄 하나 두고** 삽입:

```csharp
        //260818 hbk 크로스-Z 게이트 상태 — ProcessOneMeasurement 게이트 블록 전용.
        //  프로토콜/비프로토콜 구분(NotMyTick 의 2갈래, HalfPending 의 2갈래)은 멤버로 쪼개지 않고
        //  case 안에서 bNonProtocolCycle if-else 로 처리한다 — 원본 if 구조와 1:1 로 남겨야
        //  리팩토링 전후 대조가 가능하기 때문이다.
        private enum ECrossZGate {
            Misconfigured,
            NotMyTick,
            CaptureFailed,
            HalfPending,
            BothReady
        }
```

---

**2단계 — 게이트 블록(L544–617)을 아래 코드로 치환.** 앞뒤 경계 확인:
- 치환 시작 = `//260722 hbk Phase 68 D-02a/D-05:` 줄
- 치환 끝 = `if (bHasAnyZIndex)` 블록을 닫는 `}` (L617)
- **L618 `HTuple transform = ResolveDatumTransform(...)` 부터는 손대지 않는다**

```csharp
            //260722 hbk Phase 68 D-02a/D-05: 크로스-Z(ZIndexA/B 둘 다 -1 아님) 측정 게이트/캡처.
            //  ZIndexA/B 둘 다 -1(미설정) 이면 이 블록 진입 안 함 → 기존 경로 그대로(D-07 회귀 0).
            var dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;
            bool bHasAnyZIndex = dualMeasForGate != null && (dualMeasForGate.ZIndexA != UNSET_ZINDEX || dualMeasForGate.ZIndexB != UNSET_ZINDEX);
            if (bHasAnyZIndex)
            {
                //260818 hbk 게이트 판정을 명시적 상태(ECrossZGate)로 뽑아 아래 switch 한 곳에서 처리한다.
                //  ⚠ 판정에 필요한 호출 중 ProcessCrossZCaptureTick 은 순수하지 않다(실제 캡처/저장 수행).
                //    그래서 분류 함수 안으로 숨기지 않고 switch '앞'에 그대로 둔다 —
                //    "IsZIndexMisconfigured 를 통과한 경우에만 캡처한다"는 원본 단락(short-circuit)
                //    순서와 호출 횟수를 눈에 보이게 보존하기 위함이다.
                //    순수한 것은 out 3개 bool → enum 변환뿐이고, 그 부분만 ResolveCrossZGate 로 뺐다.
                bool bRelevant = false;
                bool bCaptureOk = false;
                bool bCompleted = false;
                string szCapturedRoleKey = null;
                bool bNonProtocolCycle = false;
                ECrossZGate eGate;
                bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);
                if (bMisconfigured)
                {
                    eGate = ECrossZGate.Misconfigured;
                }
                else
                {
                    ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2, out bRelevant, out bCaptureOk, out bCompleted, out szCapturedRoleKey);
                    //260729 hbk quick-fix(260729-e9q): 비프로토콜 사이클(RUN 버튼/일괄검사,
                    //  RequestPacket==null)은 이 Shot 의 EStep.Measure 가 이번 tick 단 한 번뿐이고
                    //  GetExecutionZIndex() 도 항상 0 이라 크로스-Z 짝이 절대 완성되지 않는다 —
                    //  조용히 continue 하면 측정한 적 없는 항목이 PASS 로 집계된다(안전 결함).
                    //  프로토콜 사이클(RequestPacket!=null)은 다음 z tick 에서 짝이 완성되므로
                    //  기존 defer 동작을 그대로 유지한다(회귀 0 하드 요구).
                    //  parentSeq2==null 은 여기 도달 불가(IsZIndexMisconfigured 가 먼저 걸러냄)이나,
                    //  도달하더라도 짝 완성 경로가 없으므로 비프로토콜과 동일하게 NG 로 본다.
                    bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();
                    eGate = ResolveCrossZGate(bRelevant, bCaptureOk, bCompleted);
                }
                //260818 hbk default: 를 두지 않는다 — 5개 멤버를 전부 다루고 있고, default 를 추가하면
                //  감사되지 않은 6번째 경로가 생긴다. 멤버를 늘릴 일이 생기면 반드시 이 switch 도 함께 고칠 것.
                switch (eGate)
                {
                    case ECrossZGate.Misconfigured:
                        MarkMeasurementZIndexMisconfigured(meas);
                        faiAllPass = false;
                        measuredCount++;
                        return;
                    case ECrossZGate.NotMyTick:
                        if (bNonProtocolCycle)
                        {
                            MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2);
                            faiAllPass = false;
                            measuredCount++;
                        }
                        return; // 프로토콜: 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망, 무변경)
                    case ECrossZGate.CaptureFailed:
                        meas.ClearResult();
                        meas.LastSkipReason = SkipReason.NO_IMAGE;
                        meas.LastJudgement = false;
                        faiAllPass = false;
                        measuredCount++;
                        return;
                    case ECrossZGate.HalfPending:
                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref crossZRoleImage);
                        MarkCrossZHalfPending(meas, parentSeq2, bNonProtocolCycle, ref faiAllPass, ref measuredCount);
                        return;
                    case ECrossZGate.BothReady:
                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref crossZRoleImage);
                        break; // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
                }
            }
```

> **⚠ `case BothReady:` 는 `return` 이 아니라 `break` 다.** `break` 는 switch 만 빠져나가고
> `if (bHasAnyZIndex)` 블록도 자연히 끝나 L618 `ResolveDatumTransform` 으로 흘러간다 — 원본의
> "fall-through 로 공용 실행 경로 진입"과 동치. 여기를 `return` 으로 쓰면 크로스-Z 측정이
> **아예 실행되지 않는다**(치명적 회귀).

> **⚠ 부수효과 #5 를 두 case 에 각각 쓰는 것이 의도다.** 원본에서 이 문장은
> `if(!bCaptureOk){…return;}` **뒤**, `if(!bCompleted){…}` **앞**에 있다. 즉 실행되는 상태는
> HalfPending 과 BothReady 정확히 두 개뿐이다. switch 앞으로 끌어올리면 NotMyTick/Misconfigured
> 에서도 평가되어 **캡처 이미지가 뒤바뀌는 실사용 버그**가 된다. "중복이니 한 곳으로 합치자"는
> 리팩토링 유혹을 **명시적으로 거부**한다.

---

**3단계 — 신규 private 메서드 3개 추가.** 배치: `ProcessOneMeasurement` 본체 **바로 아래**
(= 현재 `FinalizeFaiTick` 선언 앞), `ResolveCrossZGate` → `TakeCrossZRoleImageIfFirst` →
`MarkCrossZHalfPending` 순서.

```csharp
        //260818 hbk 크로스-Z 게이트 상태 분류 — 순수 함수다(인자 3개 bool 외에는 아무것도 읽지 않고,
        //  아무것도 쓰지 않는다). 부수효과가 있는 IsZIndexMisconfigured / ProcessCrossZCaptureTick /
        //  IsProtocolDrivenCycle 호출은 일부러 호출부에 남겼다.
        //  판정 순서(bRelevant → bCaptureOk → bCompleted)는 원본 중첩 if 순서와 1:1 이다.
        private ECrossZGate ResolveCrossZGate(bool bRelevant, bool bCaptureOk, bool bCompleted) {
            if (!bRelevant) return ECrossZGate.NotMyTick;
            if (!bCaptureOk) return ECrossZGate.CaptureFailed;
            if (!bCompleted) return ECrossZGate.HalfPending;
            return ECrossZGate.BothReady;
        }

        //260729 hbk quick-fix(260729-hwb): 이번 tick 에서 실제로 캡처된 role 이미지의
        //  소유 사본을 받아둔다(같은 FAI 안에서 첫 캡처가 결정론적으로 이긴다).
        //  AggregateFaiResult 의 표시/저장 소스로 sharedSrc 대신 사용된다(아래).
        //260818 hbk ⚠ 부수효과 있음 — 원본에서 이 문장은 !bCaptureOk 게이트와 !bCompleted 게이트
        //  '사이'에 있었다. 그래서 HalfPending / BothReady 두 case 의 '첫 줄'에서만 호출한다.
        //  조건식은 원문 그대로 둔다(첫 항 bCaptureOk 는 호출 지점상 항상 참이지만 대조 근거로 유지).
        private void TakeCrossZRoleImageIfFirst(InspectionSequence parentSeq2, bool bCaptureOk, string szCapturedRoleKey, ref HImage crossZRoleImage) {
            if (bCaptureOk && crossZRoleImage == null && !string.IsNullOrEmpty(szCapturedRoleKey) && parentSeq2 != null)
            {
                crossZRoleImage = parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey);
            }
        }

        //260818 hbk HalfPending(A/B 중 한쪽만 모임) case 본문 — 원본 if(!bCompleted){…} 블록의 순수 이동.
        private void MarkCrossZHalfPending(MeasurementBase meas, InspectionSequence parentSeq2, bool bNonProtocolCycle, ref bool faiAllPass, ref int measuredCount) {
            if (bNonProtocolCycle)
            {
                MarkMeasurementCrossZIncomplete(meas, true, false, parentSeq2);
                faiAllPass = false;
            }
            else
            {
                //260729 hbk quick-fix(260729-hwb): 프로토콜 사이클(수동 Z트리거 포함)도
                //  짝이 아직 미완성인 tick 에서 faiAllPass 기본값 true 로 방치하지 않고
                //  CROSS_Z_INCOMPLETE 로 명시 표시한다(T-HWB-01). AddFaiResult 의 완성
                //  index 게이트가 미완성 index 를 애초에 보고 대상에서 제외하므로 PLC
                //  응답은 오염되지 않는다 — 영향 범위는 화면/캡처 파일명/cycle.json 뿐.
                MarkMeasurementCrossZIncomplete(meas, true, true, parentSeq2);
                faiAllPass = false;
            }
            measuredCount++; // 프로토콜 Z1(비완성 index): 캡처만 — NG 아님, 미보고(Task4 index 게이트가 보장)
        }
```

---

**4단계 — 리팩토링하지 않고 원형 유지한 것과 그 근거 (SUMMARY 에 그대로 옮겨 적을 것):**

| 원형 유지 대상 | 왜 손대지 않았나 |
|----------------|------------------|
| `ProcessCrossZCaptureTick` 호출을 switch 앞에 잔류 | 이 호출은 `StoreCrossZImage` 로 **실제 저장을 수행**하는 부수효과 함수다. 분류 함수 안으로 옮기면 "판정" 이름 뒤에 저장이 숨어 다음 사람이 자유롭게 호출 순서를 바꿀 위험이 생긴다 |
| `IsZIndexMisconfigured` 호출을 switch 앞에 잔류 | 원본은 이게 true 면 `ProcessCrossZCaptureTick` 을 **호출하지 않는다**. 이 단락 순서를 유지하려면 두 호출이 같은 if/else 안에 있어야 한다 |
| `bNonProtocolCycle` 을 else 블록 안에서만 계산 | 원본은 Misconfigured 경로에서 `IsProtocolDrivenCycle()` 을 호출하지 않는다. switch 앞으로 무조건 끌어올리면 호출 횟수가 늘어난다(현재 순수 함수라 관측 불가하지만, 무회귀 원칙상 호출 횟수도 보존) |
| 부수효과 #5 를 두 case 에 중복 호출 | 위 ⚠ 박스 참조. 한 곳으로 합치면 실행 상태 집합이 바뀐다 |
| 갈래 #2/#3 을 enum 멤버로 쪼개지 않음 | `NotMyTick` 안의 `if (bNonProtocolCycle)` 이 원본 구조와 시각적으로 1:1 이라 대조 비용이 가장 낮다. 멤버로 쪼개면 `bNonProtocolCycle` 계산 시점을 분류 함수로 끌고 들어가야 해 순수성이 깨진다 |
| 조건식 첫 항 `bCaptureOk` 유지 | 위 G-1 참조 (verbatim 보존) |

---

**5단계 — 빌드 + 정적 검증 (커밋 전).**
verify 블록 **1·2·3 과 SIMUL 빌드**를 여기서 실행한다.
verify 블록 **4(HYGIENE)는 여기서 실행하지 말 것** — `git show ... HEAD` 로 커밋 결과를 검사하므로 커밋 전에 돌리면 직전 커밋(`12fa8aa`)을 보고 오판한다.

**6단계 — 커밋. `git add -A` 금지, 대상 파일만:**
```bash
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only          # 정확히 1줄이어야 함
git commit -m "refactor(260818-ruh): 크로스-Z 게이트를 ECrossZGate enum + switch 로 재구성 (7갈래 1:1, 동작 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M" (unstaged) 여야 함
```

**7단계 — 커밋 후 위생 검증.** verify 블록 **4(HYGIENE)** 를 여기서 실행한다(블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
echo "== enum 선언 1개 ==" && [ "$(grep -cE '^[[:space:]]*private enum ECrossZGate \{' $F)" = "1" ] && \
echo "== case 라벨 정확히 5개 (앵커로 주석 배제) ==" && [ "$(grep -cE '^[[:space:]]*case ECrossZGate\.[A-Za-z]+:' $F)" = "5" ] && \
echo "== EStep case 6개 무회귀 ==" && [ "$(grep -cE '^[[:space:]]*case EStep\.[A-Za-z]+:' $F)" = "6" ] && \
echo "== 신규 메서드 3개 ==" && [ "$(grep -cE '^[[:space:]]*private ECrossZGate ResolveCrossZGate\(' $F)" = "1" ] && [ "$(grep -cE '^[[:space:]]*private void TakeCrossZRoleImageIfFirst\(' $F)" = "1" ] && [ "$(grep -cE '^[[:space:]]*private void MarkCrossZHalfPending\(' $F)" = "1" ] && \
echo "== 부수효과 #5 호출부 정확히 2곳(HalfPending/BothReady) ==" && [ "$(grep -cE '^[[:space:]]*TakeCrossZRoleImageIfFirst\(parentSeq2' $F)" = "2" ] && \
echo "== HalfPending 헬퍼 호출 1곳 ==" && [ "$(grep -cE '^[[:space:]]*MarkCrossZHalfPending\(meas' $F)" = "1" ] && \
echo "== BothReady 는 return 이 아니라 break ==" && [ "$(grep -cE '^[[:space:]]*break; // 완성 index' $F)" = "1" ] && \
echo "== C# 7.2: 고전 switch 만 ==" && [ "$(grep -c 'switch (eGate)' $F)" = "1" ] && [ "$(grep -cE 'ECrossZGate\.[A-Za-z]+ *=>|case ECrossZGate\.[A-Za-z]+ [a-zA-Z]+ *(:|when)' $F)" = "0" ] && echo "== 기존 expression-bodied 프로퍼티 1개는 baseline(L52 ShotParam => Param as ShotConfig) — 증가하지 않았는지 ==" && [ "$(grep -c '=> ' $F)" = "1" ] && \
echo "ALL STRUCTURE CHECKS PASS"
    </automated>
    <automated>
# 파일 전역 불변 카운트 — 착수 전 실측치와 정확히 동일해야 한다.
# 이 8개가 7갈래 side-effect 보존의 1차 기계 증거다. 하나라도 어긋나면 갈래가 합쳐졌거나 유실된 것.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
[ "$(grep -c 'measuredCount++' $F)" = "8" ] && \
[ "$(grep -c 'faiAllPass = false' $F)" = "8" ] && \
[ "$(grep -c 'meas.ClearResult()' $F)" = "7" ] && \
[ "$(grep -c 'MarkMeasurementCrossZIncomplete(' $F)" = "4" ] && \
[ "$(grep -c 'SkipReason.NO_IMAGE' $F)" = "2" ] && \
[ "$(grep -c 'TakeCrossZImageCopy(szCapturedRoleKey)' $F)" = "1" ] && \
[ "$(grep -c 'parentSeq2.IsProtocolDrivenCycle()' $F)" = "1" ] && \
[ "$(grep -c 'IsZIndexMisconfigured(' $F)" = "2" ] && \
[ "$(grep -cE '^[[:space:]]*ProcessCrossZCaptureTick\(dualMeasForGate' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private void ProcessCrossZCaptureTick\(' $F)" = "1" ] && \
[ "$(grep -c 'MarkMeasurementZIndexMisconfigured' $F)" = "3" ] && \
echo "INVARIANT COUNTS PASS"
    </automated>
    <automated>
# 갈래 #3 전용 가드: NotMyTick case 안에서 measuredCount++ 가 bNonProtocolCycle if 안에 정확히 1개.
# (합쳐서 무조건 증가시키면 여기서 잡힌다)
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
S=$(sed -n '/case ECrossZGate.NotMyTick:/,/case ECrossZGate.CaptureFailed:/p' $F) && \
[ "$(printf '%s\n' "$S" | grep -c 'if (bNonProtocolCycle)')" = "1" ] && \
[ "$(printf '%s\n' "$S" | grep -c 'measuredCount++')" = "1" ] && \
[ "$(printf '%s\n' "$S" | grep -c 'MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2)')" = "1" ] && \
echo "GATE-3 GUARD PASS"
    </automated>
    <automated>
# 주석 삭제 0건 + 삼항 0건 + 무관 파일 무접촉
# ⚠ 이 블록은 반드시 **커밋 이후에** 실행할 것 — git show HEAD 로 커밋 결과를 검사한다.
#    커밋 전에 돌리면 직전 커밋(12fa8aa)을 보게 되어 오판한다.
# ⚠ SCR 재정의 필수(블록마다 셸이 새로 뜬다 — 정의 없이 쓰면 조용히 오탐 FAIL).
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && \
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad" && \
[ "$(grep -c '260729-e9q' $F)" -ge 2 ] && [ "$(grep -c '260729-hwb' $F)" -ge 8 ] && [ "$(grep -c 'Phase 68 D-02a/D-05' $F)" -ge 1 ] && \
# 삼항 검출은 줄 단위 필터를 유지한다. -o(매치 단위)로 바꾸면 문자열 리터럴
#  ('? "") + "..검사이미지 로드 실패:')이 오탐으로 잡혀 기대값이 2가 된다 — 실측 확인함.
echo "== 코드 삼항 0건 (남는 1줄은 L1229 주석) ==" && [ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "1" ] && \
echo "== 커밋에 대상 파일만 ==" && [ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
echo "== csproj 로컬 변경이 unstaged 로 그대로 ==" && git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
echo "== 워킹트리 dirty 집합이 baseline 대비 대상 파일 하나만 줄었다 ==" && \
diff <(cut -c4- "$SCR/ruh-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[01]$' && \
echo "HYGIENE PASS"
    </automated>
    <automated>G-4 SIMUL 빌드 → 성공 + 경고가 $SCR/ruh-baseline-warn.txt 와 동일(12줄: CS0618×10 + CS0162×2). 신규 CS0219/CS0168(미사용 지역변수) 경고가 1건이라도 생기면 FAIL — out 변수 초기화나 미사용 변수 문제이므로 수정할 것</automated>
  </verify>
  <done>
`ECrossZGate` enum 5멤버 + switch 5-case + 신규 private 메서드 3개.
`BothReady` 가 `break`(return 아님)로 공용 실행 경로에 fall-through.
부수효과 #5 가 HalfPending/BothReady 두 case 의 첫 줄에서만 호출.
파일 전역 불변 카운트 10종 전부 착수 전과 동일, 코드 삼항 0건, 보존 주석 3계열 삭제 0건.
SIMUL 빌드 성공 + 경고 12줄 baseline 동일. 커밋 1개, 스테이징된 파일 정확히 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 2: 7갈래 1:1 대조 증거표 작성 + SUMMARY 기록</name>
  <files>.planning/quick/260818-ruh-z-enum-switch-100/260818-ruh-SUMMARY.md</files>
  <action>
"빌드 통과했으니 OK" 는 근거로 인정하지 않는다. `$SCR/ruh-before-gate.txt`(원본 74줄)와
현재 파일을 나란히 놓고 **갈래별로** 확인하고, 각 행에 **증거(현재 줄번호 + grep 결과)** 를 적는다.

**대조표 (SUMMARY 에 이 7행 + 부수효과 1행을 그대로 채운다):**

| # | 조건 | 원본 동작 | switch 후 위치 | measuredCount | faiAllPass | 증거 |
|---|------|-----------|----------------|---------------|------------|------|
| 1 | `IsZIndexMisconfigured` true | Mark…Misconfigured + false + ++ + return | `case Misconfigured:` | ++ | false | L___ |
| 2 | `!bRelevant` && 비프로토콜 | Mark…Incomplete(false,false) + false + ++ + return | `case NotMyTick:` if-참 | ++ | false | L___ |
| 3 | `!bRelevant` && 프로토콜 | **아무 상태변화 없이 return** | `case NotMyTick:` if-거짓 | **증가 안 함** | **무변경** | L___ + GATE-3 GUARD 결과 |
| 4 | `!bCaptureOk` | ClearResult+NO_IMAGE+Judgement=false+false+++return | `case CaptureFailed:` | ++ | false | L___ |
| 5 | 부수효과: `bCaptureOk && crossZRoleImage==null && !IsNullOrEmpty(key) && parentSeq2!=null` | `crossZRoleImage = TakeCrossZImageCopy(key)` | `TakeCrossZRoleImageIfFirst`, **HalfPending/BothReady 두 case 첫 줄** | — | — | 조건식 verbatim 일치 확인 + 호출부 2곳 |
| 6a | `!bCompleted` && 비프로토콜 | Mark…Incomplete(true,false) + false + ++ + return | `MarkCrossZHalfPending` if-참 | ++ | false | L___ |
| 6b | `!bCompleted` && 프로토콜 | Mark…Incomplete(true,true) + false + ++ + return | `MarkCrossZHalfPending` else | ++ | false | L___ |
| 7 | `bCompleted` | fall-through → ResolveDatumTransform | `case BothReady:` → `break` | — | — | `break; // 완성 index` |

**추가로 반드시 명시 확인할 4건 (표 아래 별도 문단):**
1. **`bHasAnyZIndex == false` 무진입 회귀 0** — `dualMeasForGate` / `bHasAnyZIndex` 선언 2줄이
   글자 하나 안 바뀌었고, 이후 L625 `if (bHasAnyZIndex)` / L637 분기가 여전히 이 두 변수를 읽는다
   (`diff <(sed -n '546,547p' <원본>) <(현재 해당 2줄)` → 차이 0)
2. **호출 순서/횟수 보존** — Misconfigured 경로에서 `ProcessCrossZCaptureTick` 과
   `IsProtocolDrivenCycle()` 이 호출되지 않는다는 것을 코드 구조(if/else)로 제시
3. **부수효과 #5 실행 상태 집합** — 원본에서 실행되는 상태가 {HalfPending, BothReady} 정확히 둘뿐임을
   원본 74줄 위에서 게이트별로 짚어 보이고, switch 후에도 동일함을 호출부 2곳으로 제시
4. **범위 밖 무변경** — Datum 게이트 2개(L527–543)와 L618 이후 실행/집계 경로가 diff 0인지
   `git diff $BASE -- $F` 의 hunk 범위가 (a) enum 삽입부 (b) 게이트 블록 (c) 신규 메서드 3개
   이 3덩어리뿐임을 확인

**SUMMARY 에 함께 기록:**
- Task 1 4단계의 "원형 유지 대상 + 근거" 표 6행 (사용자 요구: 리팩토링하지 않은 판단 근거 명시)
- 빌드 결과 (경고 12줄 baseline 동일 여부)
- **사용자 UAT 요청 3항목** (checkpoint 아님, SUMMARY 에 문구로 남긴다):
  1. 프로토콜 사이클 크로스-Z 측정: Z1 tick 에서 화면이 CROSS_Z_INCOMPLETE 로 표시되고,
     Z2 tick 에서 정상 측정값이 나오는가 (갈래 6b → 7)
  2. **캡처 이미지가 올바른 role 이미지인가** — 갈래 #5 부수효과의 실사용 검증. 저장된 FAI 캡처 PNG 가
     리팩토링 전과 동일한 이미지인지 확인 (여기가 이번 작업 최대 위험 지점)
  3. RUN 버튼(비프로토콜)으로 크로스-Z 항목 실행 시 PASS 로 조용히 집계되지 않고 NG 로 뜨는가 (갈래 2/6a)
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && S=.planning/quick/260818-ruh-z-enum-switch-100/260818-ruh-SUMMARY.md && \
[ -f "$S" ] && \
echo "== 7갈래 + 부수효과 8행 전부 존재 ==" && [ "$(grep -cE '^\| (1|2|3|4|5|6a|6b|7) \|' $S)" = "8" ] && \
echo "== 갈래 3 에 '증가 안 함' 명시 ==" && grep -q '증가 안 함' $S && \
echo "== 부수효과 두 case 첫 줄 명시 ==" && grep -q 'TakeCrossZRoleImageIfFirst' $S && \
echo "== UAT 3항목 ==" && grep -q 'UAT' $S && \
echo "== 원형 유지 근거 기록 ==" && grep -q '원형 유지' $S && \
echo "SUMMARY PASS"
    </automated>
    <automated>대조표 각 행에 "빌드 통과" 가 아닌 실제 증거(현재 줄번호 또는 grep 출력)가 적혀 있을 것. 근거 칸이 비었거나 "OK" 뿐인 행이 1개라도 있으면 FAIL</automated>
  </verify>
  <done>
SUMMARY.md 에 7갈래+부수효과 8행 대조표(행마다 줄번호/grep 증거), 원형 유지 6건 근거표,
빌드 경고 수, 사용자 UAT 3항목이 기록됨.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

순수 내부 구조 재배치로 **신규 trust boundary 없음**. 기존 경계(PLC/핸들러 ↔ TCP `VisionServer` → 시퀀스,
파일시스템 ↔ 레시피/교시 이미지)는 이 편집 구역 밖이며 입력 검증 지점이 이동하거나 제거되지 않는다.

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-ruh-01 | Tampering (판정 무결성) | 갈래 #2/#3 병합 → `measuredCount` 통계 왜곡, 측정 안 한 항목이 집계에 섞임 | mitigate | 파일 전역 `measuredCount++` == 8 불변 검증 + NotMyTick case 전용 GATE-3 GUARD grep(`if (bNonProtocolCycle)` 1개 / `measuredCount++` 1개) + 대조표 3행 명시 |
| T-ruh-02 | Tampering (판정 무결성) | `case BothReady:` 를 `return` 으로 잘못 쓰면 크로스-Z 측정이 아예 실행되지 않고 조용히 넘어감 | mitigate | `break; // 완성 index` 앵커 grep + 대조표 7행 + UAT 항목 1(Z2 tick 측정값 확인) |
| T-ruh-03 | Information Disclosure (결과 오표시) | 부수효과 #5 실행 시점 이동 → `crossZRoleImage` 미획득/오획득 → **저장·표시 캡처 이미지가 뒤바뀜** | mitigate | 조건식 verbatim 보존 + 호출부 앵커 grep == 2 + `TakeCrossZImageCopy(szCapturedRoleKey)` == 1 + UAT 항목 2(캡처 PNG 육안 대조) |
| T-ruh-04 | Tampering (부수효과 순서) | 분류 함수를 "순수하게 만들려고" `ProcessCrossZCaptureTick` 을 안으로 옮기면 Misconfigured 경로에서도 캡처/저장이 실행됨 | mitigate | 설계상 switch 앞에 잔류 고정 + `ProcessCrossZCaptureTick` 파일 카운트 3 유지 + 원형 유지 근거표에 명문화 |
| T-ruh-05 | Tampering (리포지토리) | `git add -A` 로 csproj 로컬 설정(OutputPath=D:\Data\, Release SIMUL_MODE)이 커밋됨 → 다른 PC 빌드 파손 | mitigate | 경로 지정 스테이징 강제 + 커밋 파일 수 == 1 검증 + csproj 가 여전히 unstaged ` M` 인지 사후 확인 |
| T-ruh-06 | Denial of Service (리소스) | `crossZRoleImage` 소유권 계약 파손 → HImage 네이티브 누수 | accept | `ref HImage` 로만 전달, Dispose 는 기존 `FinalizeFaiTick` finally 무접촉. 이번 편집이 Dispose 코드에 손대지 않으므로 신규 위험 없음 |
| T-ruh-07 | Elevation of Privilege | 해당 없음 — 권한/인증 코드 무접촉 | accept | 이 구역에 권한 판정 로직 없음 |
</threat_model>

<verification>
## 플랜 전체 완료 검증

**A. 구조 (자동)** — Task 1 verify 블록 1: enum 1 / case 5 / 신규 메서드 3 / 부수효과 호출부 2 /
`break; // 완성 index` 1 / EStep case 6 무회귀 / C# 8 문법 0건

**B. 불변 카운트 (자동)** — Task 1 verify 블록 2 의 10종 전부 착수 전 실측치와 일치

**C. 갈래 #3 전용 가드 (자동)** — Task 1 verify 블록 3

**D. 위생 (자동)** — 주석 3계열 삭제 0 / 코드 삼항 0 / 커밋 파일 1 / csproj unstaged 유지 /
워킹트리 dirty 집합 baseline 대비 대상 파일만 변동

**E. 컴파일 (자동)** — Debug|x64 SIMUL 성공 + 경고 12줄 baseline 동일, 신규 CS0219/CS0168 0건.
비-SIMUL 빌드는 편집 구역에 `#if` 가 0개이므로 생략(근거 기록 필수)

**F. 1:1 대조 (수동, SUMMARY 기록 필수)** — 7갈래 + 부수효과 8행, 행마다 줄번호/grep 증거

**G. 사용자 UAT (SUMMARY 요청 문구)** — 프로토콜 Z1→Z2 흐름 / **캡처 PNG 동일성** / RUN 버튼 NG 표시
</verification>

<success_criteria>
- 게이트가 `ECrossZGate` 5멤버 + 고전 switch 5-case 로 재구성되고 7갈래가 1:1 보존
- 갈래 #3 이 `measuredCount` 를 증가시키지 않음 (전역 카운트 8 + GATE-3 GUARD 로 이중 증명)
- 부수효과 #5 가 HalfPending/BothReady 두 case 첫 줄에서만, 원문 조건식 그대로 실행
- `case BothReady:` 가 `break` 로 공용 실행 경로에 fall-through
- 파일 전역 불변 카운트 10종 착수 전과 동일 / 코드 삼항 0건 / 보존 주석 삭제 0건
- Debug|x64 빌드 성공, 경고 12줄 baseline 동일, 신규 미사용변수 경고 0
- 변경·커밋 파일 정확히 1개 (`Action_FAIMeasurement.cs`), csproj 로컬 변경 무접촉
- SUMMARY 에 대조표 8행 + 원형 유지 근거 6행 + UAT 3항목 기록
</success_criteria>

<output>
완료 후 `.planning/quick/260818-ruh-z-enum-switch-100/260818-ruh-SUMMARY.md` 생성.
**반드시 포함:** 7갈래+부수효과 8행 대조표(증거 포함), 원형 유지 근거 6행표, 빌드 경고 수,
사용자 UAT 요청 3항목.
</output>
</content>
</invoke>
