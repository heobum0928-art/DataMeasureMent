---
phase: quick-260810-olh
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/TcpServer/VisionResponsePacket.cs
  - WPF_Example/Custom/SystemHandler.cs
autonomous: true
requirements: [ALIGN-CALIB-STEPNO-01]

must_haves:
  truths:
    - "$ALIGN_CALIB 응답은 명령 종류(START/STEP/END/ABORT)와 무관하게 항상 N(현재 스텝 번호) 필드를 포함한다"
    - "성공(OK) 시 N 의 의미: START=0, STEP=실제 진행 스텝(1~36), END=99, ABORT=98"
    - "실패(NG) 시 N 은 어떤 명령이었든 관계없이 항상 97 — 이 결정은 BuildAlignCalibMessage 한 곳에서만 이뤄져 ProcessAlignCalib 의 여러 반환 지점이 개별로 챙길 필요가 없다"
    - "$ALIGN_RESULT, $RESULT, $PREP_ACK 등 다른 모든 응답 메시지 포맷은 1바이트도 안 바뀐다 — 이번 수정은 BuildAlignCalibMessage + ProcessAlignCalib 의 성공 분기 3곳(START/END/ABORT)으로 격리된다"
    - "Debug/x64 빌드가 신규 error CS 0건으로 통과한다"
  artifacts:
    - path: "WPF_Example/TcpServer/VisionResponsePacket.cs"
      provides: "BuildAlignCalibMessage 가 packet.IsPass 기준으로 N 필드를 항상 출력(성공=packet.StepNo, 실패=ALIGN_CALIB_NG_STEP_NO 상수=97)"
      contains: "ALIGN_CALIB_NG_STEP_NO"
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "ProcessAlignCalib 의 START/END/ABORT 성공 분기가 각각 StepNo=0/99/98 을 명시적으로 세팅"
      contains: "resultPacket.StepNo = 99"
  key_links:
    - from: "ProcessAlignCalib"
      to: "AlignCalibResultPacket.StepNo"
      via: "각 성공 분기가 명령별 고정값을 세팅"
      pattern: "resultPacket\\.StepNo = (0|99|98);"
    - from: "BuildAlignCalibMessage"
      to: "packet.IsPass"
      via: "성공/실패로 N 출력값을 분기(성공=StepNo, 실패=97 상수)"
      pattern: "bIsPass \\? packet\\.StepNo : ALIGN_CALIB_NG_STEP_NO"
---

<objective>
제어팀(PLC) 요청: `$ALIGN_CALIB` 응답이 지금은 STEP 명령일 때만 스텝 번호(N)가 붙고 START/END/ABORT 는 번호 없이 바로 OK/NG 만 나가서, PLC 쪽이 명령 종류마다 다른 파싱 로직을 짜야 한다. 모든 명령(START/STEP/END/ABORT)에 항상 N 필드를 붙이도록 통일하고, 성공/실패에 따라 의미를 아래처럼 고정한다(제어팀이 스펙 이미지로 확정, 사용자가 최종 예시로 재확인 완료):

- START 성공: `$ALIGN_CALIB:BOTTOM,0,0,OK@`
- STEP 성공(예: 5번째 단계): `$ALIGN_CALIB:BOTTOM,1,5,OK@` (N=1~36, 기존과 동일한 실제 진행 스텝 — 변경 없음)
- END 성공: `$ALIGN_CALIB:BOTTOM,2,99,OK@`
- ABORT 성공: `$ALIGN_CALIB:BOTTOM,3,98,OK@`
- 어떤 명령이든 실패(NG): 예) STEP 실패 `$ALIGN_CALIB:BOTTOM,1,97,NG@` — 명령 종류 무관하게 항상 97

Purpose: PLC 래더 로직이 응답 형식을 명령 종류로 분기하지 않고, 항상 같은 자리(N)의 숫자 하나만 보고 상태를 판단(0=시작/1~36=진행중/99=완료/98=취소/97=실패)할 수 있게 한다.

Output: `VisionResponsePacket.cs` 의 `BuildAlignCalibMessage`(응답 조립 — N 출력 여부/값 결정을 여기 한 곳으로 중앙화) + `Custom/SystemHandler.cs` 의 `ProcessAlignCalib`(START/END/ABORT 성공 분기 3곳에 StepNo 명시적 세팅) 수정 + Debug/x64 빌드 통과.

⚠ 설계 결정(중요): NG=97 규칙은 `ProcessAlignCalib` 의 실패 반환 지점(START/STEP/END/ABORT 4곳 + 알 수 없는 CmdStr 폴백 1곳, 총 5곳)마다 개별로 `StepNo=97` 을 세팅하지 않는다. 대신 `BuildAlignCalibMessage` 한 곳에서 `packet.IsPass` 를 보고 "성공이면 packet.StepNo 그대로, 실패면 무조건 97" 로 최종 출력값을 결정한다. 이렇게 하면 앞으로 실패 경로가 추가/변경돼도 자동으로 97 이 나가 빠뜨릴 위험이 없다(사용자와 이 설계로 진행하기로 확정 — "이것도 반영하자").
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md

프로젝트 규약(CLAUDE.md 발췌, 이 플랜에 직접 적용되는 것만):
- C# 7.2 / .NET Framework 4.8 — switch expression 등 C# 8+ 문법 금지
- 주석 태그 컨벤션: `//260810 hbk quick-260810-olh: <이유>` 형식

<interfaces>
<!-- 실행자가 코드베이스를 다시 탐색하지 않아도 되도록 필요한 기존 상태를 여기 박아둔다. -->

### 1. WPF_Example/TcpServer/VisionResponsePacket.cs — 상수 선언부 (65-66번째 줄 부근, 현재 상태)
```csharp
public const string RESULT_OK = "OK";
public const string RESULT_NG = "NG";
```
바로 아래에 새 상수 추가:
```csharp
public const string RESULT_OK = "OK";
public const string RESULT_NG = "NG";
public const int ALIGN_CALIB_NG_STEP_NO = 97;   //260810 hbk quick-260810-olh: ALIGN_CALIB 실패(NG) 시 명령 종류 무관 고정 N값(제어팀 요청)
```

### 2. WPF_Example/TcpServer/VisionResponsePacket.cs — BuildAlignCalibMessage 전체 (369-401번째 줄, 현재 상태)
```csharp
//260807 hbk quick-260807-omy v-next: $ALIGN_CALIB:BOTTOM,1,N,OK@ / STEP(1)이면 StepNo 필드 부착. CmdStr 은 숫자 코드("0"~"3").
private static string BuildAlignCalibMessage(AlignCalibResultPacket packet)
{
    string szMsg = "";
    szMsg += CMD_SEND_ALIGN_CALIB;
    szMsg += VisionServer.MSG_CMD_SEPERATOR;        // ':'
    szMsg += packet.AlignTarget;                    // BOTTOM
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
    szMsg += packet.CmdStr;                         //260807 hbk quick-260807-omy 수신 숫자 코드 echo(0=START/1=STEP/2=END/3=ABORT)
    int nCmdCode = 0;                                                    //260807 hbk quick-260807-omy
    bool bCmdIsNumeric = Int32.TryParse(packet.CmdStr, out nCmdCode);    //260807 hbk quick-260807-omy
    bool bIsStep = false;                                                //260807 hbk quick-260807-omy
    if (bCmdIsNumeric)                                                   //260807 hbk quick-260807-omy
    {
        bIsStep = nCmdCode == AlignCalibPacket.CMD_CODE_STEP;
    }
    if (bIsStep)
    {
        szMsg += VisionServer.MSG_CONTENTS_SEPERATOR; // ','
        szMsg += packet.StepNo.ToString();           // N
    }
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
    bool bIsPass = packet.IsPass;
    if (bIsPass)
    {
        szMsg += RESULT_OK;                         // "OK"
    }
    else
    {
        szMsg += RESULT_NG;                         // "NG"
    }
    return szMsg;
}
```

**교체 후 전체(정확히 이 코드로 교체)**:
```csharp
//260810 hbk quick-260810-olh: 제어팀 요청 — N(현재 스텝 번호) 필드를 명령 종류(START/STEP/END/ABORT) 무관하게
//  항상 출력한다. 성공 시 의미는 ProcessAlignCalib 가 세팅한 packet.StepNo 그대로(START=0/STEP=1~36/END=99/
//  ABORT=98), 실패(NG) 시엔 명령 종류 무관하게 항상 ALIGN_CALIB_NG_STEP_NO(97) — 이 실패 sentinel 결정을
//  여기 한 곳으로 중앙화해 ProcessAlignCalib 의 여러 실패 반환 지점(5곳)이 개별로 StepNo=97 을 챙길 필요가
//  없도록 한다(빠뜨림 방지, 근거: .planning/quick/260810-olh-align-calib-stepno-all-commands/).
private static string BuildAlignCalibMessage(AlignCalibResultPacket packet)
{
    string szMsg = "";
    szMsg += CMD_SEND_ALIGN_CALIB;
    szMsg += VisionServer.MSG_CMD_SEPERATOR;        // ':'
    szMsg += packet.AlignTarget;                    // BOTTOM
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
    szMsg += packet.CmdStr;                         // 수신 숫자 코드 echo(0=START/1=STEP/2=END/3=ABORT)

    bool bIsPass = packet.IsPass;
    int nOutStepNo = bIsPass ? packet.StepNo : ALIGN_CALIB_NG_STEP_NO;
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
    szMsg += nOutStepNo.ToString();                 // N

    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
    if (bIsPass)
    {
        szMsg += RESULT_OK;                         // "OK"
    }
    else
    {
        szMsg += RESULT_NG;                         // "NG"
    }
    return szMsg;
}
```
(`nCmdCode`/`bCmdIsNumeric`/`bIsStep` 로컬 변수와 `AlignCalibPacket.CMD_CODE_STEP` 참조는 더 이상 필요 없어 자연히 제거됨 — N 출력이 명령 종류 분기 없이 항상 실행되기 때문. `AlignCalibPacket.CMD_CODE_STEP` 상수 자체는 다른 파일(`Custom/SystemHandler.cs`)에서 계속 쓰이므로 상수 선언은 그대로 둔다, 이 메서드 안의 참조만 사라짐.)

### 3. WPF_Example/Custom/SystemHandler.cs — ProcessAlignCalib, START 분기 (687-708번째 줄, 현재 상태)
```csharp
bool bIsStart = nCmd == AlignCalibPacket.CMD_CODE_START;
if (bIsStart)
{
    EthernetVisionHandler.Handle.PickerCal.Reset();
#if SIMUL_MODE
    //260630 hbk — SIMUL: START 수신 시 이미지 순차 인덱스 리셋
    if (EthernetVisionHandler.Handle.Camera != null) {
        EthernetVisionHandler.Handle.Camera.ResetSimulIndex();
    }
#endif
    // 모델 로드 시도 (UI 티칭 완료 전제). 실패 시 경고만 — STEP 에서 자연 실패.
    string loadErr;
    bool bLoaded = EthernetVisionHandler.Handle.PickerCal.TryLoadModel(out loadErr);
    if (!bLoaded)
    {
        Logging.PrintLog((int)ELogType.Error,
            "[ALIGN_CALIB] START: 모델 로드 실패 ({0})", loadErr);
    }
    resultPacket.IsPass = true;
    Logging.PrintLog((int)ELogType.Trace, "[ALIGN_CALIB] START — 누적 초기화, model={0}", bLoaded);
    return resultPacket;
}
```
**변경**: `resultPacket.IsPass = true;` 바로 아래 줄에 `resultPacket.StepNo = 0;` 한 줄 추가(설명 주석: `//260810 hbk quick-260810-olh: N=0 고정(제어팀 요청, START 의미)`). 그 외 이 분기의 다른 로직(모델 로드, SIMUL_MODE 리셋 등)은 전혀 건드리지 않는다.

### 4. WPF_Example/Custom/SystemHandler.cs — ProcessAlignCalib, END 분기 (778-806번째 줄, 현재 상태)
```csharp
bool bIsEnd = nCmd == AlignCalibPacket.CMD_CODE_END;
if (bIsEnd)
{
    double dRow, dCol, dRad;
    string error;
    bool bOk = EthernetVisionHandler.Handle.PickerCal.TryComputePickerCenter(
        out dRow, out dCol, out dRad, out error);

    if (bOk)
    {
        resultPacket.IsPass = true;
        //260630 hbk — END 성공: 피커센터 즉시 저장 (비정상 종료 시 손실 방지)
        SystemSetting.Handle.Save();
        var endCb = EthernetVisionHandler.Handle.OnCalibEndViewer;
        if (endCb != null)
        {
            HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
            double r = dRow; double c = dCol; double rad = dRad;
            System.Windows.Application.Current.Dispatcher.Invoke(() => endCb(r, c, rad, vizXld));
        }
        Logging.PrintLog((int)ELogType.Trace,
            "[ALIGN_CALIB] END — 피커센터=({0:F2},{1:F2}) r={2:F2}", dRow, dCol, dRad);
    }
    else
    {
        Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] END 산출 실패: {0}", error);
    }
    return resultPacket;
}
```
**변경**: `if (bOk)` 블록 안, `resultPacket.IsPass = true;` 바로 아래 줄에 `resultPacket.StepNo = 99;` 한 줄 추가(설명 주석: `//260810 hbk quick-260810-olh: N=99 고정(제어팀 요청, END=완료 의미)`). `else` 블록(실패 로그)은 손대지 않는다 — 실패 시 N=97 은 BuildAlignCalibMessage 가 `packet.IsPass==false` 를 보고 자동 처리하므로 여기서 아무것도 할 필요 없다.

### 5. WPF_Example/Custom/SystemHandler.cs — ProcessAlignCalib, ABORT 분기 (808-815번째 줄, 현재 상태)
```csharp
bool bIsAbort = nCmd == AlignCalibPacket.CMD_CODE_ABORT;
if (bIsAbort)
{
    EthernetVisionHandler.Handle.PickerCal.Reset();
    resultPacket.IsPass = true;
    Logging.PrintLog((int)ELogType.Trace, "[ALIGN_CALIB] ABORT — 누적 초기화");
    return resultPacket;
}
```
**변경**: `resultPacket.IsPass = true;` 바로 아래 줄에 `resultPacket.StepNo = 98;` 한 줄 추가(설명 주석: `//260810 hbk quick-260810-olh: N=98 고정(제어팀 요청, ABORT=취소 의미)`).

### 6. STEP 분기 — 무수정 확인용 참고 (이미 올바름, 아무것도 바꾸지 않는다)
STEP 분기는 이미 성공 시 `resultPacket.StepNo = EthernetVisionHandler.Handle.PickerCal.StepCount;` 를 세팅하고 있음(대략 750번째 줄 부근, `TryAddStep` 성공 블록 안) — 이번 스펙(N=1~36 실제 진행 스텝)과 완전히 일치하므로 무수정. 실패 시에도 `BuildAlignCalibMessage` 가 `IsPass==false` 를 보고 97 을 출력하므로 이 분기의 실패 경로 역시 무수정.

### 7. 알 수 없는 CmdStr 폴백 (817-818번째 줄 부근) — 무수정 확인용 참고
```csharp
Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 알 수 없는 CmdStr: {0}", szCmd);
return resultPacket;
```
이 경로는 `resultPacket.IsPass` 를 한 번도 true 로 세팅하지 않으므로(메서드 최상단 `resultPacket.IsPass = false;` 기본값 그대로) `BuildAlignCalibMessage` 가 자동으로 N=97, NG 를 출력한다 — 무수정.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: ALIGN_CALIB 응답에 N(현재 스텝 번호) 필드 항상 포함 + 실패 시 97 통일</name>
  <files>WPF_Example/TcpServer/VisionResponsePacket.cs, WPF_Example/Custom/SystemHandler.cs</files>
  <action>
`<interfaces>` 섹션 1~5에 명시된 정확한 교체를 수행한다:
1. `VisionResponsePacket.cs` 65-66번째 줄(RESULT_OK/RESULT_NG 상수) 바로 아래에 `ALIGN_CALIB_NG_STEP_NO = 97` 상수 추가.
2. `VisionResponsePacket.cs` 의 `BuildAlignCalibMessage` 메서드 전체를 `<interfaces>` 섹션 2의 "교체 후 전체" 코드로 정확히 교체.
3. `Custom/SystemHandler.cs` 의 `ProcessAlignCalib` — START 분기에 `resultPacket.StepNo = 0;`, END 분기(`if(bOk)` 안)에 `resultPacket.StepNo = 99;`, ABORT 분기에 `resultPacket.StepNo = 98;` 각각 한 줄씩 추가(`<interfaces>` 섹션 3/4/5).

**절대 건드리지 않을 것**: STEP 분기(이미 올바름), 알 수 없는 CmdStr 폴백(이미 올바름), `$ALIGN_RESULT`/`$RESULT`/`$PREP_ACK`/`$RESET_ACK` 등 다른 모든 메시지 빌더, `AlignCalibResultPacket`/`AlignCalibPacket` 클래스 정의(StepNo 프로퍼티는 이미 존재, 손댈 필요 없음).
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && echo "=== [게이트1] 신규 상수 정확히 1건 ===" && grep -c "ALIGN_CALIB_NG_STEP_NO = 97" WPF_Example/TcpServer/VisionResponsePacket.cs && echo "=== [게이트2] BuildAlignCalibMessage 안에서 상수 사용 정확히 1건 ===" && grep -c "ALIGN_CALIB_NG_STEP_NO" WPF_Example/TcpServer/VisionResponsePacket.cs && echo "(기대: 상수 선언 1 + 사용 1 = 총 2건이어야 함, 위 grep -c 는 선언 포함 전체 카운트)" && echo "=== [게이트3] bIsStep 관련 로컬 변수가 BuildAlignCalibMessage 안에서 제거됐는지(더 이상 없어야 함) ===" && sed -n '/private static string BuildAlignCalibMessage/,/^        }/p' WPF_Example/TcpServer/VisionResponsePacket.cs | grep -c "bIsStep" && echo "(기대: 0)" && echo "=== [게이트4] SystemHandler.cs 에 StepNo=0/99/98 세 곳 모두 존재 ===" && grep -n "resultPacket.StepNo = 0;\|resultPacket.StepNo = 99;\|resultPacket.StepNo = 98;" WPF_Example/Custom/SystemHandler.cs && echo "=== [게이트5] 다른 메시지 빌더(BuildAlignItems/BuildAlignResultMessage 등) 무변경 확인 — ALIGN_RESULT 관련 grep 결과가 이번 세션 이전과 동일해야 함(참고용, 실행자가 직접 눈으로 diff 확인) ===" && git diff --stat -- WPF_Example/TcpServer/VisionResponsePacket.cs WPF_Example/Custom/SystemHandler.cs && echo "=== 컴파일 (스크래치 OutDir — 실제 bin/obj 미접촉, DatumMeasurement.exe 실행 중이라 잠금 회피) ===" && MSYS_NO_PATHCONV=1 "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" /p:Configuration=Debug /p:Platform=x64 "/p:OutputPath=C:\gsd-olh-scratch\bin\" "/p:BaseIntermediateOutputPath=C:\gsd-olh-scratch\obj\" /v:minimal /nologo 2>&1 | grep -iE "error CS|Build succeeded" | head -20</automated>
  </verify>
  <done>
- `ALIGN_CALIB_NG_STEP_NO = 97` 상수 선언 1건 존재.
- `BuildAlignCalibMessage` 안에 `bIsStep` 참조가 0건(완전히 제거됨).
- `Custom/SystemHandler.cs` 에 `resultPacket.StepNo = 0;` / `= 99;` / `= 98;` 세 줄 전부 존재.
- `git diff --stat` 결과가 딱 두 파일(VisionResponsePacket.cs, Custom/SystemHandler.cs)만 표시.
- `Build succeeded` 출력, 신규 `error CS` 0건.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 비전 PC → PLC(제어반) TCP 송신 | 이번 변경은 응답 포맷팅(직렬화) 전용 — 신뢰 경계를 새로 만들지 않는다. 입력 파싱(`ProcessAlignCalib` 의 CmdStr 파싱) 로직은 무변경. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-OLH-01 | Tampering | 실패(NG) 응답에서 N 값이 케이스에 따라 97 이 아닌 다른 값(예: 세팅 안 된 기본값 0)으로 새어나감 | mitigate | N 값 결정을 `BuildAlignCalibMessage` 한 곳(`bIsPass ? packet.StepNo : ALIGN_CALIB_NG_STEP_NO`)으로 중앙화 — `ProcessAlignCalib` 의 실패 반환 지점 5곳 중 단 한 곳도 개별로 챙길 필요가 없어 구조적으로 누락 불가능. |
| T-OLH-02 | Tampering | `$ALIGN_RESULT`/`$RESULT`/`$PREP_ACK` 등 무관한 메시지 포맷이 실수로 함께 바뀜 | mitigate | 수정 범위를 `BuildAlignCalibMessage`(1개 메서드) + `ProcessAlignCalib` 의 성공 분기 3곳(START/END/ABORT)으로 엄격히 격리. verify 게이트 5가 두 파일 외 변경 없음을 확인. |
| T-OLH-03 | Denial of Service | STEP 성공 분기(`resultPacket.StepNo = PickerCal.StepCount`)를 실수로 건드려 실제 진행 스텝 번호가 깨짐 | mitigate | `<interfaces>` 섹션 6에 "STEP 분기 무수정" 명시, verify 게이트에 STEP 관련 라인 대상 그레핑 없이 SystemHandler.cs 전체 diff 를 실행자가 눈으로 확인하도록 done 기준에 포함. |
</threat_model>

<verification>
정적 검증(Task 1 verify 에 포함, 실행자가 그대로 수행):
1. **신규 상수 존재** — `ALIGN_CALIB_NG_STEP_NO = 97` grep 1건.
2. **bIsStep 완전 제거 확인** — BuildAlignCalibMessage 메서드 범위 내 `bIsStep` grep 0건.
3. **3개 성공 분기 StepNo 세팅 확인** — START(=0)/END(=99)/ABORT(=98) 세 줄 모두 grep 로 존재 확인.
4. **변경 파일 범위 확인** — `git diff --stat` 이 정확히 두 파일만 표시.
5. **Debug/x64 빌드** — `Build succeeded`, 신규 `error CS` 0건(스크래치 OutDir, 실행 중인 DatumMeasurement.exe 의 실제 bin/obj 미접촉).

실기 확인(이 플랜 범위 밖 — PLC 통신 환경 필요, 사용자가 추후 직접 확인):
- START/STEP/END/ABORT 각각 성공 케이스 실측 응답이 예시(`$ALIGN_CALIB:BOTTOM,0,0,OK@` 등)와 정확히 일치하는지.
- 임의의 명령 실패를 유도해(예: 카메라 미연결 상태에서 STEP) NG 응답이 `$ALIGN_CALIB:BOTTOM,1,97,NG@` 형태로 나오는지.
</verification>

<success_criteria>
- `$ALIGN_CALIB` 응답이 START/STEP/END/ABORT 모든 명령에서 항상 N 필드를 포함한다.
- 성공 시 N = START:0 / STEP:실제 진행 스텝(1~36) / END:99 / ABORT:98.
- 실패(NG) 시 N = 97, 명령 종류 무관.
- 이 규칙이 `ProcessAlignCalib` 의 여러 실패 지점이 아니라 `BuildAlignCalibMessage` 단일 지점에서 결정되어, 향후 실패 경로 추가/변경 시에도 자동으로 지켜진다.
- `$ALIGN_RESULT`/`$RESULT`/`$PREP_ACK`/`$RESET_ACK` 등 다른 응답 포맷은 완전히 무변경.
- Debug/x64 빌드 신규 `error CS` 0건.
</success_criteria>

<output>
Create `.planning/quick/260810-olh-align-calib-stepno-all-commands/260810-olh-SUMMARY.md` when done.

커밋 시 이 두 파일만 스테이징한다 (현재 워킹트리에 사용자의 미커밋 `WPF_Example/DatumMeasurement.csproj`/`WPF_Example/SystemHandler.cs`(루트, Custom 아님) 변경이 있으므로 절대 함께 커밋하지 않는다):
```
git add WPF_Example/TcpServer/VisionResponsePacket.cs WPF_Example/Custom/SystemHandler.cs
```
</output>
</content>
