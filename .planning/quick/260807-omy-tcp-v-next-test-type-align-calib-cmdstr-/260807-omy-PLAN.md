---
phase: quick-260807-omy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/TcpServer/ResourceMap.cs
  - WPF_Example/TcpServer/VisionRequestPacket.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/TcpServer/VisionResponsePacket.cs
autonomous: true
requirements: [PROTO-VNEXT-01, PROTO-VNEXT-02, PROTO-VNEXT-03]
must_haves:
  truths:
    - "$TEST 의 Type 필드에 '0' 이 오면 TOP 시퀀스로, '1' 이 오면 Bottom(PC1 Side 슬롯)으로, '2'~'5' 가 오면 Phase 63 의 SIDE_1~4 와 동일한 슬롯으로 라우팅된다"
    - "$TEST Type 이 비었거나 숫자가 아니거나 0~5 범위 밖이면 기존과 동일하게 Site 정수 폴백 라우팅이 일어난다 (오라우팅 없음)"
    - "$ALIGN_CALIB 의 두 번째 필드가 0/1/2/3 이면 각각 START/STEP/END/ABORT 동작이 수행된다"
    - "$ALIGN_CALIB CmdStr 이 1(STEP) 일 때만 응답에 StepNo 필드가 붙는다"
    - "$ALIGN_CALIB CmdStr 이 숫자가 아니면 어떤 동작도 수행하지 않고 FAIL 응답만 나간다 (START 로 오인식되지 않는다)"
    - "$RESULT 응답이 RESULT:site;Type;P|F|B 3필드로만 나가고 count/개별 FAI 목록이 실리지 않는다"
    - "내부 FAICount/FAIResults/m_bCycleHasNG 판정 데이터는 그대로 남아 UI·로컬저장이 계속 소비한다"
    - "v2.6(UseProtocolV1=false) 경로와 $ALIGN_TEST/$PREP/$RESET/$LIGHT/$SITE_STATUS 는 동작이 바뀌지 않는다"
  artifacts:
    - path: "WPF_Example/Custom/TcpServer/ResourceMap.cs"
      provides: "TryResolveSlotByType 숫자 코드 기반 슬롯 해석"
      contains: "TYPE_CODE_TOP"
    - path: "WPF_Example/TcpServer/VisionRequestPacket.cs"
      provides: "AlignCalibPacket.CMD_CODE_* 단일 진실 원천 상수"
      contains: "CMD_CODE_STEP"
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "ProcessAlignCalib 숫자 코드 분기 + 비숫자 조기 반환 가드"
      contains: "AlignCalibPacket.CMD_CODE_START"
    - path: "WPF_Example/TcpServer/VisionResponsePacket.cs"
      provides: "축약된 BuildResultMessageV1 + 숫자 STEP 판정"
      contains: "AlignCalibPacket.CMD_CODE_STEP"
  key_links:
    - from: "WPF_Example/Custom/TcpServer/ResourceMap.cs"
      to: "testPacket.Type"
      via: "TryResolveSlotByType 숫자 파싱"
      pattern: "Int32\\.TryParse\\(szType"
    - from: "WPF_Example/Custom/SystemHandler.cs"
      to: "AlignCalibPacket.CMD_CODE_*"
      via: "nCmd 정수 비교"
      pattern: "nCmd == AlignCalibPacket\\.CMD_CODE_"
    - from: "WPF_Example/TcpServer/VisionResponsePacket.cs"
      to: "AlignCalibPacket.CMD_CODE_STEP"
      via: "BuildAlignCalibMessage bIsStep 판정"
      pattern: "CMD_CODE_STEP"
---

<objective>
제어팀과 합의된 v-next 프로토콜 3건을 코드에 반영한다. 구버전 텍스트 형식과의 하위호환은 불필요한 클린 컷오버다.

1. `$TEST` Type 필드: 텍스트 토큰("TOP"/"BOTTOM"/"SIDE_1~4") → 숫자 코드("0"~"5")
2. `$ALIGN_CALIB` CmdStr 필드: 텍스트("START"/"STEP"/"END"/"ABORT") → 숫자 코드("0"~"3")
3. `$RESULT` 응답: `site;Type;P/F/B;count;id=val=judge,...` → `site;Type;P/F/B` (count + 항목목록 제거)

Purpose: 실제 공장 검사 장비의 PLC/핸들러 통신 규격 전환. 오라우팅이나 무응답은 라인 정지로 직결되므로 회귀 0 이 최우선이다.
Output: 4개 파일 수정, MSBuild Debug/x64 PASS.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

**프로젝트 코딩 규약 (이 plan 에 직접 적용되는 것만):**
- C# 7.2 / .NET Framework 4.8 — C# 8.0+ 문법(switch expression, nullable ref) 금지
- 헝가리언 접두사: `sz`(string), `n`(int), `b`(bool), `d`(double)
- 삼항연산자 `?:` / null 병합 `??` 금지 — `if/else` + bool 변수화
- 수정하는 파일의 기존 brace 스타일을 따를 것 (ResourceMap/SystemHandler/VisionResponsePacket = Allman, VisionRequestPacket 클래스 선언부 = K&R)
- 변경 지점에 `//260807 hbk quick-260807-omy` 주석을 남길 것
- 매직넘버 금지 — 숫자 코드는 반드시 명명 상수 경유
</context>

<interfaces>
<!-- 아래는 현재 코드베이스에서 추출한 실제 내용이다. 실행자는 이걸 근거로 수정하면 되고, 별도 탐색이 필요 없다. -->

**1) WPF_Example/Custom/TcpServer/ResourceMap.cs (130-164번째 줄) — 현재 상태**

```csharp
private const string TYPE_TOKEN_TOP = "TOP";            //260624 hbk Phase 63
private const string TYPE_TOKEN_BOTTOM = "BOTTOM";      //260624 hbk Phase 63
private const string TYPE_TOKEN_SIDE_PREFIX = "SIDE_";  //260624 hbk Phase 63 SIDE_1~4 공통 접두

//260624 hbk Phase 63 PROTO-Type: Type 토큰 → ESite 슬롯. 인식 실패 시 false 반환(호출부가 Site 폴백).
//  TOP→Top 슬롯, BOTTOM→Side 슬롯(PC1 Side 슬롯=BOTTOM 자원), SIDE_*→Top 슬롯(PC2 양 슬롯 SIDE 동일).
// T-63-08/T-63-09 mitigation: 등록된 ESite.Top/Side 슬롯만 산출 → KeyNotFoundException/오라우팅 회피.
private bool TryResolveSlotByType(string szType, out ESite eSlot)
{
    eSlot = ESite.Top;
    bool bHasType = !string.IsNullOrEmpty(szType);
    if (!bHasType)
    {
        return false;
    }
    bool bIsTop = szType == TYPE_TOKEN_TOP;
    if (bIsTop)
    {
        eSlot = ESite.Top;
        return true;
    }
    bool bIsBottom = szType == TYPE_TOKEN_BOTTOM;
    if (bIsBottom)
    {
        eSlot = ESite.Side;
        return true;
    }
    bool bIsSide = szType.StartsWith(TYPE_TOKEN_SIDE_PREFIX);
    if (bIsSide)
    {
        eSlot = ESite.Top;
        return true;
    }
    return false;
}
```

**확정된 기존 동작 (읽어서 확인 완료 — 새로 설계하지 말고 이걸 그대로 숫자판으로 옮길 것):**
`SIDE_1`~`SIDE_4` 는 **전부 `ESite.Top` 슬롯**으로 간다 (`StartsWith` 단일 분기, 뒤 숫자를 읽지 않음). PC2 에서는 Top/Side 두 슬롯이 모두 SIDE 자원(`MapPc2Resources`, 106-116번째 줄)이라 어느 쪽이든 동일하다. 따라서 숫자 2/3/4/5 는 모두 `ESite.Top` 이 정답이다.

유일한 호출부는 `SetIdentifier` 의 v1.0 분기(199-210번째 줄)뿐이며, `bUseV1 == true` 일 때만 실행된다. v2.6 경로(211-215번째 줄)는 이 함수를 호출하지 않는다.

`using System;` 는 이미 4번째 줄에 있다 (`Int32.TryParse` 사용 가능).

**2) WPF_Example/TcpServer/VisionRequestPacket.cs — 현재 상태 (관련 부분만)**

```csharp
//260625 hbk v3.0: ALIGN_CALIB 수신 파서.
//  dataList[0]=BOTTOM(고정), [1]=CmdStr(START/STEP/END/ABORT). AlignFace 제거.
private static bool TryParseAlignCalibFields(string[] dataList, AlignCalibPacket alignPacket)
{
    bool bHasFields = dataList != null && dataList.Length >= 2;
    if (!bHasFields) { return false; }
    alignPacket.AlignTarget = dataList[0];  // BOTTOM (고정)
    alignPacket.CmdStr = dataList[1];       // START/STEP/END/ABORT
    return true;
}

public class AlignCalibPacket : VisionRequestPacket {
    public string AlignTarget { get; set; } = "";   //260624 hbk 라우팅 대상(BOTTOM 고정)
    public string CmdStr      { get; set; } = "";   //260625 hbk v3.0: START/STEP/END/ABORT

    public AlignCalibPacket() : base(VisionRequestType.AlignCalib) {
    }
}
```

`ParseTypeField`(357-365번째 줄)는 원시 문자열을 그대로 돌려주고 `null`/빈값만 `""` 로 정규화한다 — **수정 불필요**. `TestPacket.Type` 도 `string` 프로퍼티 그대로 유지한다.

**3) WPF_Example/Custom/SystemHandler.cs — ProcessAlignCalib 현재 분기 (625-772번째 줄)**

```csharp
string szCmd = packet.CmdStr;

bool bIsStart = string.Equals(szCmd, "START", StringComparison.OrdinalIgnoreCase);   // 640번째 줄
bool bIsStep  = string.Equals(szCmd, "STEP",  StringComparison.OrdinalIgnoreCase);   // 663번째 줄
bool bIsEnd   = string.Equals(szCmd, "END",   StringComparison.OrdinalIgnoreCase);   // 731번째 줄
bool bIsAbort = string.Equals(szCmd, "ABORT", StringComparison.OrdinalIgnoreCase);   // 761번째 줄

Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 알 수 없는 CmdStr: {0}", szCmd);  // 770번째 줄 (최종 폴백)
return resultPacket;
```

각 `bIsXxx` 는 선언 직후 `if (bIsXxx) { ...본문...; return resultPacket; }` 형태로 소비된다. `resultPacket.IsPass` 기본값은 `false`(636번째 줄) 이고 성공 분기에서만 `true` 로 덮인다.
`resultPacket.CmdStr = packet.CmdStr;`(635번째 줄) — 원문 echo, **유지**.

**4) WPF_Example/TcpServer/VisionResponsePacket.cs — 현재 상태 (관련 부분만)**

```csharp
public const char MSG_RESULT_HEADER_SEP = ';';   // 헤더 구분자 (site/판정/count 사이)
public const char MSG_RESULT_ITEM_SEP   = ',';   // 항목 간 구분자
public const char MSG_RESULT_INNER_SEP  = '=';   // 항목 내부 구분자 (id=val=judge)

private static string BuildResultMessageV1(TestResultPacket testPacket)     // 282번째 줄
{
    string szMsg = "";
    szMsg += CMD_SEND_TEST;                       // "RESULT"
    szMsg += VisionServer.MSG_CMD_SEPERATOR;      // ':'
    szMsg += testPacket.Site.ToString();
    szMsg += MSG_RESULT_HEADER_SEP;               // ';'
    szMsg += testPacket.Type;                     //260624 hbk Phase 63 Type echo (빈값이면 빈 토큰)
    szMsg += MSG_RESULT_HEADER_SEP;               // ';'  //260624 hbk Phase 63
    szMsg += MapCycleJudgement(testPacket);       // P|F|B
    szMsg += MSG_RESULT_HEADER_SEP;               // ';'      ← 제거 대상
    szMsg += testPacket.FAICount.ToString();      // count     ← 제거 대상
    szMsg += MSG_RESULT_HEADER_SEP;               // ';'      ← 제거 대상
    szMsg += BuildFaiItemsV1(testPacket);         // 항목목록   ← 제거 대상
    return szMsg;
}

private static string BuildAlignCalibMessage(AlignCalibResultPacket packet)  // 405번째 줄
{
    ...
    szMsg += packet.CmdStr;                         // START/STEP/END/ABORT   ← 412번째 줄, echo 유지
    bool bIsStep = packet.CmdStr == "STEP";         //                        ← 413번째 줄, 수정 대상
    if (bIsStep)
    {
        szMsg += VisionServer.MSG_CONTENTS_SEPERATOR; // ','
        szMsg += packet.StepNo.ToString();           // N
    }
    ...
}
```

`MapFaiJudgement`(319번째 줄)와 `BuildFaiItemsV1`(330번째 줄)은 **오직 `BuildResultMessageV1` 에서만** 호출된다 (전체 저장소 grep 확인 완료 — 다른 호출부 0건).
`MSG_RESULT_INNER_SEP` 는 `BuildAlignItems`(398번째 줄)가 계속 쓰므로 **유지**. `MSG_RESULT_ITEM_SEP` 는 `BuildFaiItemsV1` 이 유일 사용처였으므로 이번 변경 후 미사용이 되지만 `public const` 라 컴파일 경고가 없고, **상수는 그대로 둔다**(주석만 갱신).
`MapCycleJudgement`(301번째 줄)는 **무변경**. v2.6 블록(193-257번째 줄)도 **무변경**.
`using System;` 는 1번째 줄에 있고, `AlignCalibPacket` 은 동일 네임스페이스 `ReringProject.Network` 라 별도 using 불필요.
</interfaces>

<scope_boundaries>
**절대 건드리지 않는 것 (이 목록에 걸리는 수정이 diff 에 있으면 실패):**

- v2.6 레거시 경로 전체 — `InitializeV26`, `TryParseTestFieldsV26`, `Convert` 의 v2.6 Test 블록(193-257번째 줄)
- `$ALIGN_TEST` — `TryParseAlignTestFields`, `ProcessAlignTest`, `BuildAlignResultMessage`. **코드 재확인 완료: 변경 없음이 정답.** 근거 두 가지:
  1. `AlignTarget`("TRAY"/"BOTTOM")은 이번 규격 변경 대상이 아니며, `VisionRequestPacket.cs:402` / `Custom/SystemHandler.cs:329` / `VisionResponsePacket.cs:364` 세 곳이 계속 문자열 "BOTTOM" 으로 비교한다 — 그대로 둔다.
  2. 중간 Mode 필드 `dataList[2]` 는 `TryParseAlignTestFields` 안에서 `// dataList[2]=모드(skip)`(400번째 줄) 주석만 있고 **어떤 변수에도 대입되지 않는다.** `AlignTestPacket` 에 대응 프로퍼티조차 없다. 따라서 이 필드의 값이 텍스트든 숫자든 코드는 영향을 받지 않는다 → 태스크 불필요.
- `$ALIGN_CALIB` 의 `AlignTarget` 필드("BOTTOM" 고정 텍스트) — 유지
- `$PREP` / `$PREP_ACK` / `$RESET` / `$RESET_ACK` / `$LIGHT` / `$SITE_STATUS` / `$ALIVE` / `$RECIPE` 전부
- 내부 판정 로직 — `m_bCycleHasNG`, `ApplyCycleJudgement`, `MapCycleJudgement`, `ClassifyFai`, `InspectionSequence` 전반
- 내부 데이터 구조 — `TestResultPacket.FAIResults` / `FAICount` / `FAIResultData` / 엑셀 export / `CycleResultSerializer`
- `TestPacket.Type` / `AlignCalibPacket.CmdStr` 의 **프로퍼티 타입**(둘 다 `string` 유지 — 담기는 값의 의미만 바뀐다)
- `ParseTypeField` 본문, `TryParseAlignCalibFields` 의 대입 2줄(주석만 갱신 허용)
- `Test/*.py` 모의 스크립트 (v2.6 시대 산물, 범위 밖)

**`$ALIGN_CALIB` 에 v1/v2.6 게이트를 새로 넣지 말 것.** 이 명령은 v3.0 시대에 신설되어 애초에 v2.6 규격에 존재하지 않는다. `ProcessAlignCalib` / `BuildAlignCalibMessage` 는 현재 `UseProtocolV1` 분기가 없고, 그대로 두는 것이 맞다.
</scope_boundaries>

<tasks>

<task type="auto">
  <name>Task 1: $TEST Type 필드 — 텍스트 토큰 → 숫자 코드 (ResourceMap 라우팅)</name>
  <files>WPF_Example/Custom/TcpServer/ResourceMap.cs</files>
  <action>
`TryResolveSlotByType(string szType, out ESite eSlot)`(137번째 줄) 을 숫자 코드 기반으로 교체한다. 라우팅 **결과**는 Phase 63 과 100% 동일해야 한다 — 새로 설계하지 말 것.

**(a) 상수 교체 (130-132번째 줄):** `TYPE_TOKEN_TOP` / `TYPE_TOKEN_BOTTOM` / `TYPE_TOKEN_SIDE_PREFIX` 3개 `private const string` 을 삭제하고, 그 자리에 4개 `private const int` 를 선언한다: `TYPE_CODE_TOP = 0`, `TYPE_CODE_BOTTOM = 1`, `TYPE_CODE_SIDE_MIN = 2`, `TYPE_CODE_SIDE_MAX = 5`. `SIDE_MIN`/`SIDE_MAX` 에는 각각 "SIDE_1", "SIDE_4" 임을 밝히는 짧은 꼬리 주석을 단다. 옛 상수 이름 문자열(`TYPE_TOKEN_...`)을 주석에도 남기지 말고, 히스토리는 "텍스트 토큰" 이라는 서술로 표현한다 (검증 게이트가 이름으로 0건을 확인한다).

**(b) 함수 본문 교체:** 기존 `bHasType` 빈값 가드는 그대로 둔다. 그 다음 `int nCode = 0; bool bIsNumeric = Int32.TryParse(szType, out nCode);` 로 파싱하고 **`if (!bIsNumeric) { return false; }` 를 반드시 파싱 직후, 어떤 코드 비교보다 먼저** 배치한다. 이유: `Int32.TryParse` 는 실패 시 out 파라미터에 0 을 넣는데 0 은 TOP 코드이므로, 가드를 빼먹으면 쓰레기 문자열이 전부 TOP 으로 오라우팅된다. 이 순서가 이 태스크에서 가장 중요한 정확성 요건이다.

이어서 기존과 동일한 3단 `if` 구조를 유지하되 비교만 정수로 바꾼다: `bool bIsTop = nCode == TYPE_CODE_TOP;` → `eSlot = ESite.Top; return true;` / `bool bIsBottom = nCode == TYPE_CODE_BOTTOM;` → `eSlot = ESite.Side; return true;` / `bool bIsSide = nCode >= TYPE_CODE_SIDE_MIN && nCode <= TYPE_CODE_SIDE_MAX;` → `eSlot = ESite.Top; return true;`. 마지막은 기존 그대로 `return false;`.

여기서 `bIsBottom` 이 `ESite.Side` 로 가는 것은 오타가 아니다 — PC1 의 Side 슬롯에 BOTTOM 자원이 매핑되어 있기 때문이며(`MapPc1Resources`, 93-103번째 줄), Phase 63 원본 동작 그대로다. `bIsSide` 가 `ESite.Top` 인 것도 원본 그대로다(PC2 는 양 슬롯이 동일 SIDE 자원).

범위 밖 정수(예: 6, -1)는 기존 미인식 토큰과 똑같이 `false` 를 돌려주고, 호출부(203-206번째 줄)의 `ResolveSiteSlot(testPacket.Site)` Site 폴백이 처리한다. **폴백 경로는 손대지 않는다.**

**(c) 헤더 주석 갱신:** 함수 위 주석에 v-next 매핑(0=TOP / 1=BOTTOM / 2~5=SIDE_1~4)과 "파싱 실패 시 0 이 들어오므로 비숫자 가드가 코드 비교보다 앞서야 한다"는 경고를 남긴다. 기존 `T-63-08/T-63-09 mitigation` 주석 줄은 유효하므로 보존한다.

`SetIdentifier`(166번째 줄 이하)와 `ResolveSiteSlot`(120번째 줄), `InitializeV1`/`InitializeV26`/`MapPc1Resources`/`MapPc2Resources` 는 **한 줄도 수정하지 않는다.**
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && echo "=== [G1] 옛 텍스트 토큰 상수 전멸 (기대 0, 기준선 6) ===" && grep -c "TYPE_TOKEN" WPF_Example/Custom/TcpServer/ResourceMap.cs; echo "=== [G2] 신규 숫자 상수 4종 각 2건(선언+사용) 기대 ===" && for T in TYPE_CODE_TOP TYPE_CODE_BOTTOM TYPE_CODE_SIDE_MIN TYPE_CODE_SIDE_MAX; do printf "%s " "$T"; grep -v '^\s*//' WPF_Example/Custom/TcpServer/ResourceMap.cs | grep -c "$T"; done; echo "=== [G3] 비숫자 가드가 코드비교보다 먼저인지 (행번호 오름차순이어야 함) ===" && grep -n "Int32.TryParse(szType\|!bIsNumeric\|nCode == TYPE_CODE_TOP" WPF_Example/Custom/TcpServer/ResourceMap.cs; echo "=== [G4] SIDE 범위비교 1건 기대 ===" && grep -c "nCode >= TYPE_CODE_SIDE_MIN && nCode <= TYPE_CODE_SIDE_MAX" WPF_Example/Custom/TcpServer/ResourceMap.cs; echo "=== [G5] 미접촉 증명: 아래 5줄 원문 그대로 잡혀야 함 ===" && grep -n "private ESite ResolveSiteSlot(int nSite)" WPF_Example/Custom/TcpServer/ResourceMap.cs && grep -n "eSlot = ResolveSiteSlot(testPacket.Site)" WPF_Example/Custom/TcpServer/ResourceMap.cs && grep -n "Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_BOTTOM)" WPF_Example/Custom/TcpServer/ResourceMap.cs && grep -n "Add(EResource.Sequence, ESite.Top,  SequenceHandler.SEQ_SIDE)" WPF_Example/Custom/TcpServer/ResourceMap.cs && grep -n "testPacket.Identifier  = Find(EResource.Sequence, eSlot)" WPF_Example/Custom/TcpServer/ResourceMap.cs; echo "=== [G6] 컴파일 (스크래치 OutDir — 실제 bin/obj 미접촉) ===" && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-omy-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-omy-scratch/obj/" //v:minimal //nologo 2>&1 | grep -iE "error CS|Build succeeded" | head -20</automated>
  </verify>
  <done>
- G1 = 0 (옛 토큰 상수/사용/주석 전부 제거)
- G2 = TYPE_CODE_TOP 2, TYPE_CODE_BOTTOM 2, TYPE_CODE_SIDE_MIN 2, TYPE_CODE_SIDE_MAX 2
- G3 출력에서 `Int32.TryParse(szType` 행번호 < `!bIsNumeric` 행번호 < `nCode == TYPE_CODE_TOP` 행번호 (가드 우선 순서 증명)
- G4 = 1
- G5 5줄 모두 원문 그대로 출력 (폴백 경로 · PC1/PC2 매핑 · 호출부 미접촉)
- G6 `Build succeeded`, 신규 `error CS` 0건
  </done>
</task>

<task type="auto">
  <name>Task 2: $ALIGN_CALIB CmdStr — 텍스트 → 숫자 코드 (상수 + 처리 + 응답 3파일 일관 수정)</name>
  <files>WPF_Example/TcpServer/VisionRequestPacket.cs, WPF_Example/Custom/SystemHandler.cs, WPF_Example/TcpServer/VisionResponsePacket.cs</files>
  <action>
CmdStr 소비처가 **3곳**이므로 한 곳이라도 빠지면 응답 포맷이 깨진다. 아래 (a)→(b)→(c) 순서대로 전부 수정한다.

**(a) 단일 진실 원천 상수 신설 — `VisionRequestPacket.cs` 의 `AlignCalibPacket` 클래스(593-599번째 줄):**
클래스 본문 맨 위(`AlignTarget` 프로퍼티 앞)에 `public const int` 4개를 선언한다: `CMD_CODE_START = 0`, `CMD_CODE_STEP = 1`, `CMD_CODE_END = 2`, `CMD_CODE_ABORT = 3`. `public` 인 이유는 `Custom/SystemHandler.cs` 와 `VisionResponsePacket.cs` 두 파일이 같은 상수를 참조해야 하기 때문이다 — 상수를 파일마다 복제하지 말 것(불일치 시 STEP 응답이 조용히 깨진다).

같은 클래스의 `CmdStr` 프로퍼티는 **타입 `string` 그대로 두고 꼬리 주석만** v-next 숫자 문자열("0"~"3")로 갱신한다. `AlignTarget` 프로퍼티는 무변경.
`TryParseAlignCalibFields`(416-423번째 줄)는 대입 로직 무변경, 415번째 줄과 421번째 줄의 꼬리 주석만 숫자 코드 설명으로 갱신한다.

**(b) `Custom/SystemHandler.cs` 의 `ProcessAlignCalib`(625-772번째 줄):**
`string szCmd = packet.CmdStr;`(638번째 줄) 바로 아래에 정수 파싱과 **비숫자 조기 반환 가드**를 추가한다: `int nCmd = 0; bool bCmdIsNumeric = Int32.TryParse(szCmd, out nCmd);` 후 `if (!bCmdIsNumeric)` 이면 `Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 숫자가 아닌 CmdStr: {0}", szCmd);` 를 남기고 `return resultPacket;`. 이 시점 `resultPacket.IsPass` 는 636번째 줄에서 이미 `false` 이므로 기존 미인식 커맨드와 동일하게 FAIL 응답이 나간다. **이 가드가 없으면 `TryParse` 실패 시 `nCmd` 가 0 이 되어 쓰레기 입력이 START 로 오인식되고 `PickerCal.Reset()` 이 실행된다 — 캘리브 누적 데이터가 날아간다.**

그 다음 4개 `string.Equals(...)` 비교를 정수 비교로 교체한다. `bIsStart`(640번째 줄) → `nCmd == AlignCalibPacket.CMD_CODE_START`, `bIsStep`(663번째 줄) → `... CMD_CODE_STEP`, `bIsEnd`(731번째 줄) → `... CMD_CODE_END`, `bIsAbort`(761번째 줄) → `... CMD_CODE_ABORT`. **각 `if` 블록의 본문(PickerCal 호출, Grab, ROI, 뷰어 콜백, 로깅, try/finally, SIMUL_MODE 블록)은 한 줄도 건드리지 않는다.** 변수 선언 4줄만 바뀐다.

770번째 줄의 최종 폴백 로그(`알 수 없는 CmdStr`)와 `return resultPacket;` 은 그대로 두어 범위 밖 정수(4 이상, 음수)를 계속 잡는다. `resultPacket.CmdStr = packet.CmdStr;`(635번째 줄) 원문 echo 도 유지한다.

`string.Equals`/`StringComparison.OrdinalIgnoreCase` 가 이 파일에서 완전히 사라지므로(현재 4건 전부 ALIGN_CALIB 용), 게이트가 0 건을 확인한다.

**(c) `VisionResponsePacket.cs` 의 `BuildAlignCalibMessage`(405-430번째 줄):**
412번째 줄 `szMsg += packet.CmdStr;` 은 **유지**(수신 숫자 코드를 그대로 echo — 꼬리 주석만 갱신). 413번째 줄 `bool bIsStep = packet.CmdStr == "STEP";` 을 정수 비교로 교체한다: `int nCmdCode = 0;` / `bool bCmdIsNumeric = Int32.TryParse(packet.CmdStr, out nCmdCode);` / `bool bIsStep = false;` / `if (bCmdIsNumeric) { bIsStep = nCmdCode == AlignCalibPacket.CMD_CODE_STEP; }`. 삼항연산자나 한 줄 압축 대신 이 4줄 if 형태를 쓴다(프로젝트 규약: 조건 bool 변수화 + if/else).

`if (bIsStep)` 블록 내부(StepNo 부착), `IsPass` OK/NG 분기, `AlignTarget` echo 는 무변경. 404번째 줄 헤더 주석의 예시 문자열을 숫자 규격으로 갱신한다(예: `$ALIGN_CALIB:BOTTOM,1,N,OK@`).

이 파일의 `BuildResultMessageV1` / `BuildAlignResultMessage` / `BuildAlignItems` / v2.6 블록은 이 태스크에서 손대지 않는다 (Task 3 소관).
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && echo "=== [G1] 상수 선언 4건 (VisionRequestPacket.cs) ===" && grep -v '^\s*//' WPF_Example/TcpServer/VisionRequestPacket.cs | grep -c "public const int CMD_CODE_"; echo "=== [G2] CmdStr 프로퍼티 여전히 string (2줄 출력) ===" && grep -n "public string CmdStr" WPF_Example/TcpServer/VisionRequestPacket.cs && grep -n "public string CmdStr" WPF_Example/TcpServer/VisionResponsePacket.cs; echo "=== [G3] SystemHandler 옛 문자열비교 전멸 (기대 0, 기준선 4) ===" && grep -c "string.Equals" WPF_Example/Custom/SystemHandler.cs; echo "=== [G4] SystemHandler 정수비교 4건 ===" && grep -v '^\s*//' WPF_Example/Custom/SystemHandler.cs | grep -c "nCmd == AlignCalibPacket.CMD_CODE_"; echo "=== [G5] 비숫자 가드가 첫 코드비교보다 앞선 행 (행번호 오름차순이어야 함) ===" && grep -n "bool bCmdIsNumeric = Int32.TryParse(szCmd\|!bCmdIsNumeric\|nCmd == AlignCalibPacket.CMD_CODE_START" WPF_Example/Custom/SystemHandler.cs; echo "=== [G6] 응답측 STEP 판정 상수 경유 1+ / 문자열 STEP 비교 0 ===" && grep -v '^\s*//' WPF_Example/TcpServer/VisionResponsePacket.cs | grep -c "AlignCalibPacket.CMD_CODE_STEP"; grep -c 'CmdStr == "STEP"' WPF_Example/TcpServer/VisionResponsePacket.cs; echo "=== [G7] 미접촉 증명: ALIGN_TEST Mode skip / AlignTarget / echo / PickerCal 본문 ===" && grep -n "bool bIsBottom = alignPacket.AlignTarget == \"BOTTOM\"" WPF_Example/TcpServer/VisionRequestPacket.cs && grep -n "dataList\[2\]=모드(skip)" WPF_Example/TcpServer/VisionRequestPacket.cs && grep -n "resultPacket.CmdStr      = packet.CmdStr;" WPF_Example/Custom/SystemHandler.cs && grep -c "EthernetVisionHandler.Handle.PickerCal" WPF_Example/Custom/SystemHandler.cs; echo "=== [G8] 컴파일 ===" && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-omy-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-omy-scratch/obj/" //v:minimal //nologo 2>&1 | grep -iE "error CS|Build succeeded" | head -20</automated>
  </verify>
  <done>
- G1 = 4 (START/STEP/END/ABORT 상수 선언)
- G2 양쪽 파일 모두 `public string CmdStr` 출력 (프로퍼티 타입 변경 없음)
- G3 = 0 (`string.Equals` 4건 전부 제거)
- G4 = 4 (정수 비교 4건)
- G5 출력 행번호가 `Int32.TryParse(szCmd` < `!bCmdIsNumeric` < `CMD_CODE_START` 순 (가드 우선 증명)
- G6 첫 숫자 1 이상, 둘째 숫자 0
- G7 앞 3줄 모두 출력되고 `PickerCal` 참조 건수 = 8 (ProcessAlignCalib 본문 미접촉)
- G8 `Build succeeded`, 신규 `error CS` 0건
  </done>
</task>

<task type="auto">
  <name>Task 3: $RESULT 응답 단순화 — count + 개별 FAI 항목목록 제거</name>
  <files>WPF_Example/TcpServer/VisionResponsePacket.cs</files>
  <action>
와이어 형식을 `RESULT:site;Type;P|F|B;count;id=val=judge,...` 에서 `RESULT:site;Type;P|F|B` 로 줄인다. **오직 TCP 로 나가는 문자열 직렬화만** 줄이는 것이고, 내부 데이터는 손대지 않는다.

**(a) `BuildResultMessageV1`(282-297번째 줄):** 마지막 4줄(`MSG_RESULT_HEADER_SEP` 부착 → `testPacket.FAICount.ToString()` → `MSG_RESULT_HEADER_SEP` 부착 → `BuildFaiItemsV1(testPacket)`)을 삭제한다. 남는 마지막 구성 요소는 `MapCycleJudgement(testPacket)` 이고 그 뒤 곧바로 `return szMsg;` 다. 결과적으로 `MSG_RESULT_HEADER_SEP` 부착이 4회에서 2회로 준다. 앞부분(`CMD_SEND_TEST` / `MSG_CMD_SEPERATOR` / `Site` / Type echo)은 **한 글자도 바꾸지 않는다** — 구분자를 `,` 로 바꾸지 말고 `;` 를 유지한다.

**(b) 죽은 코드 제거:** `MapFaiJudgement`(319-327번째 줄)와 `BuildFaiItemsV1`(330-349번째 줄) 두 `private static` 메서드를 삭제한다. (a) 이후 저장소 전체에서 호출부가 0 이 되기 때문이다(사전 grep 확인 완료). 삭제 후에도 옛 메서드 이름을 주석에 남기지 말 것 — 게이트가 이름으로 0건을 확인한다.

**(c) 유지할 것 (삭제하지 말 것):**
`MSG_RESULT_ITEM_SEP` 상수(80번째 줄)는 이번 변경으로 사용처가 사라지지만 `public const char` 라 컴파일 경고를 만들지 않으므로 **선언은 남기고 꼬리 주석만** "v-next $RESULT 항목목록 폐기로 현재 미사용" 취지로 갱신한다. `MSG_RESULT_INNER_SEP`(81번째 줄)는 `BuildAlignItems`(398번째 줄)가 계속 쓰므로 그대로 둔다. `MapCycleJudgement`(301-316번째 줄) 본문, `TestResultPacket` 클래스의 `FAIResults` / `FAICount` / `IsBuffer` / `Type` 프로퍼티, `FAIResultData` 클래스 전체는 **전부 유지**한다 — UI·엑셀 export·`CycleResultSerializer` 가 계속 소비한다.

**(d) 헤더 주석 갱신:** `BuildResultMessageV1` 위 주석 3줄(279-281번째 줄)의 예시 형식을 `$RESULT:site;Type;P|F|B@` 로 갱신하고, "count/항목목록은 v-next 에서 와이어에서만 제거되었으며 내부 FAICount/FAIResults 는 그대로 살아있다"는 문장을 명시한다. Datum 샷(FAICount=0)일 때 옛 형식이 만들던 trailing `;` 도 함께 사라진다는 점을 한 줄로 남긴다.

**(e) 절대 금지:** v2.6 Test 직렬화 블록(193-257번째 줄)은 `FAICount` 와 `FAIResults` 를 계속 사용한다. 이 블록을 "일관성" 명목으로 함께 줄이지 말 것 — `UseProtocolV1=false` 레거시 경로이며 이번 컷오버 대상이 아니다.

이 태스크의 마지막 빌드는 **실제 `bin/x64/Debug` 로 나가는 Debug/x64 빌드**다. 앱(`DatumMeasurement.exe`)이 실행 중이면 파일 잠금으로 MSB3027 이 나므로, 빌드 전에 앱이 떠 있지 않은지 확인하고 떠 있으면 사용자에게 종료를 요청한다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && echo "=== [G1] 죽은 메서드 전멸 — 아래 두 grep 은 아무 것도 출력하지 않아야 PASS ===" && grep -rn "BuildFaiItemsV1" WPF_Example/ --include=*.cs; grep -rn "MapFaiJudgement" WPF_Example/ --include=*.cs; echo "--- (출력 없으면 PASS) ---"; echo "=== [G2] HEADER_SEP 코드행 3 기대 (선언1 + 부착2 / 기준선 5) ===" && grep -v '^\s*//' WPF_Example/TcpServer/VisionResponsePacket.cs | grep -c "MSG_RESULT_HEADER_SEP"; echo "=== [G3] FAICount 코드행 3 기대 (v2.6 2건 + 프로퍼티선언 1건 / 기준선 5) ===" && grep -v '^\s*//' WPF_Example/TcpServer/VisionResponsePacket.cs | grep -c "FAICount"; echo "=== [G4] ITEM_SEP 코드행 1 기대 (선언만 잔존 / 기준선 2) ===" && grep -v '^\s*//' WPF_Example/TcpServer/VisionResponsePacket.cs | grep -c "MSG_RESULT_ITEM_SEP"; echo "=== [G5] 유지 대상 5줄 모두 원문 그대로 출력되어야 함 ===" && grep -n "szMsg += MapCycleJudgement(testPacket);" WPF_Example/TcpServer/VisionResponsePacket.cs && grep -n "private static string MapCycleJudgement" WPF_Example/TcpServer/VisionResponsePacket.cs && grep -n "public int FAICount => FAIResults.Count;" WPF_Example/TcpServer/VisionResponsePacket.cs && grep -n "msg += testPacket.FAICount.ToString();" WPF_Example/TcpServer/VisionResponsePacket.cs && grep -n "szItems += MSG_RESULT_INNER_SEP;" WPF_Example/TcpServer/VisionResponsePacket.cs; echo "=== [G6] Type echo 유지 ===" && grep -n "szMsg += testPacket.Type;" WPF_Example/TcpServer/VisionResponsePacket.cs; echo "=== [G7] 최종 실빌드 Debug/x64 (앱 종료 상태에서) ===" && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo 2>&1 | grep -iE "error CS|MSB3027|Build succeeded" | head -20; echo "=== [G8] 4개 파일만 변경되었는지 ===" && git diff --stat -- WPF_Example/</automated>
  </verify>
  <done>
- G1 두 grep 모두 무출력 (`BuildFaiItemsV1` / `MapFaiJudgement` 저장소 전체 0건 — 주석 포함)
- G2 = 3, G3 = 3, G4 = 1
- G5 5줄 모두 출력 (`MapCycleJudgement` 호출·정의, `FAICount` 프로퍼티, v2.6 count 부착줄, `BuildAlignItems` 의 INNER_SEP 전부 생존)
- G6 출력됨 (Type echo 와 `;` 구분자 유지)
- G7 `Build succeeded`, `error CS` 0건, `MSB3027` 0건
- G8 변경 파일이 정확히 4개: ResourceMap.cs / VisionRequestPacket.cs / SystemHandler.cs / VisionResponsePacket.cs
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| PLC/핸들러 → TCP `VisionServer` | 외부 장비가 보내는 `$TEST` / `$ALIGN_CALIB` 문자열. 신뢰 불가 입력이 파싱을 거쳐 시퀀스 라우팅과 캘리브 상태 머신에 도달한다. |
| 비전 → PLC/핸들러 (`$RESULT` / `$ALIGN_CALIB` 응답) | 응답 포맷이 어긋나면 PLC 파서가 실패하거나 ACK 대기로 라인이 정지한다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-omy-01 | Tampering | `ResourceMap.TryResolveSlotByType` | mitigate | `Int32.TryParse` 실패 시 out 값이 0(=TOP)이 되는 함정 — 비숫자 가드를 코드 비교보다 **먼저** 배치해 쓰레기 입력의 TOP 오라우팅 차단. Task 1 G3 게이트가 행번호 순서로 강제 검증. |
| T-omy-02 | Tampering | `ProcessAlignCalib` | mitigate | 동일 함정. 비숫자 CmdStr 이 START(0)로 오인식되면 `PickerCal.Reset()` 이 실행돼 캘리브 누적이 소실된다. 파싱 직후 조기 반환 가드 + Task 2 G5 행번호 게이트. |
| T-omy-03 | Denial of Service | `TryResolveSlotByType` 범위 밖 코드 | mitigate | 6 이상/음수는 `false` 반환 → 기존 `ResolveSiteSlot` Site 폴백이 처리. 미등록 슬롯 산출이 없어 `KeyNotFoundException`(T-63-08/09) 재발 없음. |
| T-omy-04 | Denial of Service | `BuildAlignCalibMessage` STEP 판정 누락 | mitigate | 3곳 중 응답측만 텍스트 비교로 남으면 STEP 응답에서 StepNo 가 조용히 빠져 PLC 파서가 실패한다. Task 2 를 3파일 단일 태스크로 묶고 G6 게이트가 `CmdStr == "STEP"` 0건을 강제. |
| T-omy-05 | Information Disclosure | `$RESULT` 개별 측정값 제거 | accept | 이번 변경은 와이어에서 정보를 **줄이는** 방향이라 노출 위험이 감소한다. 내부 저장(엑셀/로컬)은 유지되며 접근 경로는 기존과 동일. |
| T-omy-06 | Repudiation | ALIGN_CALIB 실패 추적 | mitigate | 비숫자 입력 시 신규 Error 로그(`숫자가 아닌 CmdStr`)와 기존 범위 밖 로그(`알 수 없는 CmdStr`)를 분리 유지해 PLC 오작동 원인을 로그로 구분 가능. |
| T-omy-SC | Tampering | 패키지 설치 | N/A | 이번 변경은 신규 npm/pip/cargo/NuGet 패키지 설치가 **0건**이다. `packages.config` 무변경. 공급망 검증 대상 없음. |
</threat_model>

<verification>
**정적 검증 (각 태스크 `<verify>` 게이트로 자동 수행):**
1. 옛 텍스트 토큰(`TYPE_TOKEN_*`, `string.Equals` ALIGN_CALIB 4건, `CmdStr == "STEP"`) 잔존 0건
2. 신규 명명 상수(`TYPE_CODE_*` 4종, `CMD_CODE_*` 4종) 선언 + 사용 존재
3. 두 파싱 지점 모두 **비숫자 가드가 코드 비교보다 앞선 행번호** (0 = TOP/START 오인식 방지)
4. 죽은 메서드(`BuildFaiItemsV1` / `MapFaiJudgement`) 저장소 전체 0건
5. 미접촉 증명 — v2.6 블록, `ResolveSiteSlot` 폴백, `MapCycleJudgement`, `FAICount` 프로퍼티, ALIGN_TEST Mode skip 주석, `PickerCal` 호출 건수 전부 원문 유지
6. MSBuild Debug/x64 `Build succeeded`, `error CS` 0건, 변경 파일 정확히 4개

**실기 UAT (이 plan 범위 밖 — 실행 후 사용자/오케스트레이터가 별도 수행):**
- `$TEST:1,0,...@` → TOP 시퀀스 기동 / `$TEST:2,1,...@` → BOTTOM 기동 / `$TEST:1,2,...@` → SIDE 기동
- `$TEST:1,9,...@`(범위 밖) → Site 폴백 라우팅, 무응답 없음
- `$ALIGN_CALIB:BOTTOM,0@` → OK, `,1@` → `$ALIGN_CALIB:BOTTOM,1,N,OK@`(StepNo 포함), `,2@`/`,3@` → OK, `,XYZ@` → FAIL 응답 + Error 로그
- 사이클 완료 시 `$RESULT:1;0;P@` 형태(3필드)로만 송신되는지 TCP 캡처 확인
- UI 검사결과 리스트와 엑셀 export 에 개별 측정값이 여전히 표시되는지 확인 (내부 데이터 보존 증명)
</verification>

<success_criteria>
- `$TEST` Type 라우팅이 숫자 0~5 로 동작하고, 그 결과 슬롯이 Phase 63 텍스트 토큰판과 정확히 일치한다 (0→Top, 1→Side, 2~5→Top)
- 비숫자/범위 밖 Type 은 `false` → Site 폴백으로 안전 처리되며 TOP 으로 오라우팅되지 않는다
- `$ALIGN_CALIB` CmdStr 0/1/2/3 이 START/STEP/END/ABORT 로 동작하고, 상수는 `AlignCalibPacket.CMD_CODE_*` 한 곳에만 정의된다
- 비숫자 CmdStr 이 START 로 오인식되어 `PickerCal.Reset()` 을 실행하는 일이 없다
- STEP(1) 응답에만 StepNo 가 붙는다
- `$RESULT` 가 `RESULT:site;Type;P|F|B` 로 나가고 구분자는 `;` 그대로다
- 내부 `FAICount`/`FAIResults`/판정 로직/`MapCycleJudgement` 및 v2.6 경로 전체가 무변경이다
- `$ALIGN_TEST`/`$PREP`/`$RESET`/`$LIGHT`/`$SITE_STATUS`/`$ALIVE` 무변경 (`$ALIGN_TEST` 는 코드 재확인 결과 변경 불필요로 확정 — `scope_boundaries` 에 근거 기록)
- MSBuild Debug/x64 0 errors, 변경 파일 4개
</success_criteria>

<output>
Create `.planning/quick/260807-omy-tcp-v-next-test-type-align-calib-cmdstr-/260807-omy-SUMMARY.md` when done.

SUMMARY 에 반드시 기록할 것:
- 변경 전/후 와이어 형식 예시 3쌍 ($TEST / $ALIGN_CALIB 요청·응답 / $RESULT)
- `$ALIGN_TEST` 변경 불필요 판정 근거 (Mode 필드 미사용 코드 위치)
- 삭제된 죽은 코드 목록과 그 근거 (호출부 0건)
- 실기 UAT 미수행분 (위 `<verification>` 의 UAT 항목) — 사용자 승인 대기 상태로 명시
</output>
