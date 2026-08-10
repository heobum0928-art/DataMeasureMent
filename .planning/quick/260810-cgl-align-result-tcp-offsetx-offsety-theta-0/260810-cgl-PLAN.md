---
phase: quick-260810-cgl
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/TcpServer/VisionResponsePacket.cs
autonomous: true
requirements: [ALIGN-FMT-01]

must_haves:
  truths:
    - "$ALIGN_RESULT 응답의 OffsetX/OffsetY/Theta 값은 양수/0/음수 모든 경우에 항상 선두 부호(+/-) 1글자를 갖는다 — 키엔스 PLC 의 고정폭 파싱이 값 길이 변동으로 깨지지 않는다"
    - "0 값도 반드시 '+0.000' 으로 부호가 붙는다 — '고정폭' 요구사항상 부호 없는 예외가 있으면 안 된다"
    - "$RESULT(TestResultPacket 계열: FAIResults/DistanceMm/Angle/X/Y) 의 숫자 포맷은 단 1바이트도 바뀌지 않는다 — 이번 수정은 BuildAlignItems 내부 딱 한 줄로 격리된다"
    - "Debug/x64 빌드가 신규 error CS 0건으로 통과한다"
  artifacts:
    - path: "WPF_Example/TcpServer/VisionResponsePacket.cs"
      provides: "BuildAlignItems 의 item.Value 포맷 문자열이 3-섹션 커스텀 숫자 포맷(양수;음수;0)으로 교체되어 부호가 항상 출력됨"
      contains: "+0.000;-0.000;+0.000"
  key_links:
    - from: "BuildAlignResultMessage"
      to: "BuildAlignItems(packet)"
      via: "szMsg += BuildAlignItems(packet)"
      pattern: "BuildAlignItems\\(packet\\)"
    - from: "BuildAlignItems"
      to: "item.Value.ToString"
      via: "for 루프 내 szItems 누적"
      pattern: "item\\.Value\\.ToString\\(\"\\+0\\.000;-0\\.000;\\+0\\.000\"\\)"
---

<objective>
펨텍 PLC팀 요청: 키엔스 PLC 가 `$ALIGN_RESULT` TCP 응답을 고정폭(fixed-width)으로 파싱하는데, 지금은 음수일 때만 `-` 가 붙고 양수/0 일 때는 부호가 아예 없어서 값의 전체 길이가 매번 달라져 PLC 파싱이 깨진다. `BuildAlignItems`(`WPF_Example/TcpServer/VisionResponsePacket.cs:364`) 의 `item.Value.ToString("0.000")` 한 줄을 C# 커스텀 숫자 포맷(양수;음수;0 3섹션)으로 바꿔 항상 부호 1글자가 붙도록 고친다.

Purpose: PLC 측 고정폭 파서가 `OffsetX`/`OffsetY`/`Theta` 필드 길이를 매번 동일하게 기대하는데, 현재는 부호 유무로 길이가 흔들려 파싱이 깨진다. 부호를 항상 고정하면 필드 길이가 일정해져 문제가 해결된다.

Output: `WPF_Example/TcpServer/VisionResponsePacket.cs` 의 `BuildAlignItems` 메서드 딱 한 줄 수정 + Debug/x64 빌드 통과.

⚠ 스코프 제한: 같은 파일 안에 동일한 `ToString("0.000")` 포맷이 220/234/236/238/248/250/252번째 줄에도 있으나, 이들은 전부 `$RESULT`/`TestResultPacket` 전용이라 절대 건드리지 않는다. `BuildAlignItems` 안의 364번째 줄(정확한 현재 줄 번호, 아래 확인됨) 딱 하나만 수정 범위다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md

프로젝트 규약(CLAUDE.md 발췌, 이 플랜에 직접 적용되는 것만):
- C# 7.2 / .NET Framework 4.8 — switch expression 등 C# 8+ 문법 금지 (이번 수정은 문자열 리터럴 변경뿐이라 해당 없음)
- 주석 태그 컨벤션: `//260810 hbk quick-260810-cgl: <이유>` 형식 (기존 `//260807 hbk quick-260807-lh7:` 패턴 미러)

<interfaces>
<!-- 실행자가 코드베이스를 다시 탐색하지 않아도 되도록 필요한 기존 상태를 여기 박아둔다. -->

WPF_Example/TcpServer/VisionResponsePacket.cs (현재 상태, 349-367번째 줄, `BuildAlignItems` 전체):
```csharp
//260625 hbk v3.0: Align 항목들을 Name=val,... 로 직렬화. per-item 판정 제거.
private static string BuildAlignItems(AlignResultPacket packet)
{
    string szItems = "";
    int nCount = packet.Items.Count;
    for (int i = 0; i < nCount; i++)
    {
        AlignResultItem item = packet.Items[i];
        bool bNeedsSeparator = i > 0;
        if (bNeedsSeparator)
        {
            szItems += VisionServer.MSG_CONTENTS_SEPERATOR; // ','
        }
        szItems += item.ItemName;                   // OffsetX / OffsetY / Theta
        szItems += MSG_RESULT_INNER_SEP;            // '='
        szItems += item.Value.ToString("0.000");    // val   <-- 이 줄(364번째)만 수정
    }
    return szItems;
}
```

수정할 줄 (364번째, 딱 한 줄):
- 기존: `szItems += item.Value.ToString("0.000");    // val`
- 변경: `szItems += item.Value.ToString("+0.000;-0.000;+0.000");    // val //260810 hbk quick-260810-cgl: 고정폭 파싱 위해 양수/0 도 '+' 부호 고정 (D-요청: 펨텍 PLC팀)`

C# 커스텀 숫자 포맷 3섹션 규칙(양수;음수;0) — PowerShell(.NET 동일 런타임)로 실측 검증 완료:
- `(12.34).ToString("+0.000;-0.000;+0.000")` → `"+12.340"`
- `(-12.34).ToString("+0.000;-0.000;+0.000")` → `"-12.340"`
- `(0.0).ToString("+0.000;-0.000;+0.000")` → `"+0.000"`
음수 섹션이 별도로 존재하면 .NET 은 기본 `-` 를 자동으로 붙이지 않으므로, 음수 섹션에도 리터럴 `-` 를 직접 명시해야 한다(이미 위 포맷 문자열에 포함됨).

호출부 (320-347번째 줄, `BuildAlignResultMessage` — 무수정, 참고용):
```csharp
private static string BuildAlignResultMessage(AlignResultPacket packet)
{
    ...
    szMsg += BuildAlignItems(packet);               // OffsetX=val,OffsetY=val[,Theta=val]
    return szMsg;
}
```

이 파일 안의 다른 `ToString("0.000")` 사용처(전부 `$RESULT`/`TestResultPacket` 전용, 절대 무수정 — 참고용):
- 220번째: `faiData.DistanceMm.ToString("0.000")`
- 234/236/238번째: `visionResults[i].Angle/X/Y.ToString("0.000")`
- 248/250/252번째: `testPacket.Angle/X/Y.ToString("0.000")`
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: BuildAlignItems 부호 고정 포맷 + Debug/x64 빌드 검증</name>
  <files>WPF_Example/TcpServer/VisionResponsePacket.cs</files>
  <action>
`BuildAlignItems` 메서드(현재 364번째 줄) 안의 `item.Value.ToString("0.000")` 딱 한 줄을 커스텀 숫자 포맷 문자열 `"+0.000;-0.000;+0.000"` 으로 교체한다. 이 포맷은 3섹션(양수;음수;0)이라 양수와 0 모두 `+`, 음수는 `-` 가 항상 붙는다(위 `<interfaces>` 실측 확인됨).

같은 줄 끝의 기존 `// val` 주석 뒤에 이유 주석을 덧붙인다: `//260810 hbk quick-260810-cgl: 고정폭 파싱 위해 양수/0 도 '+' 부호 고정 (펨텍 PLC팀 요청)`.

**절대 건드리지 않을 것**: 같은 파일의 220/234/236/238/248/250/252번째 줄 `ToString("0.000")` — 전부 `$RESULT`/`TestResultPacket` 전용, 이번 요청과 무관. `BuildAlignResultMessage`, `AlignResultItem`, `AlignResultPacket` 등 나머지 Align 관련 코드도 무수정 — 딱 포맷 문자열 리터럴 1곳만 바꾼다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && echo "=== [게이트1] 삭제줄 1 / 추가줄 1 기대 (한 줄만 수정) ===" && git diff --numstat -- WPF_Example/TcpServer/VisionResponsePacket.cs && echo "=== [게이트2] 신규 포맷 문자열 정확히 1건 ===" && grep -c '"+0.000;-0.000;+0.000"' WPF_Example/TcpServer/VisionResponsePacket.cs && echo "=== [게이트3] 기존 무관 ToString(\"0.000\") 7건 그대로 유지(주석 제외 카운트) ===" && grep -v '^\s*//' WPF_Example/TcpServer/VisionResponsePacket.cs | grep -o 'ToString("0.000")' | wc -l && echo "=== [게이트4] BuildAlignItems 메서드 시그니처/구조 무변경 확인 ===" && grep -n "private static string BuildAlignItems" WPF_Example/TcpServer/VisionResponsePacket.cs && echo "=== 컴파일 (스크래치 OutDir — 실제 bin/obj 미접촉) ===" && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-cgl-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-cgl-scratch/obj/" //v:minimal //nologo 2>&1 | grep -iE "error CS|Build succeeded" | head -20</automated>
  </verify>
  <done>
- `git diff --numstat` 결과가 삭제 1줄 / 추가 1줄 (딱 한 줄 교체 증거).
- `"+0.000;-0.000;+0.000"` 문자열이 파일에 정확히 1건 존재.
- 코드줄(주석 제외) 기준 `ToString("0.000")` (구 포맷)이 정확히 7건 — $RESULT 전용 라인 전부 원문 그대로 유지된 증거.
- `BuildAlignItems` 시그니처 원문 그대로 grep 에 잡힘.
- `Build succeeded` 출력, 신규 `error CS` 0건.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 비전 PC → PLC(키엔스) TCP 송신 | 이번 변경은 응답 포맷팅(직렬화) 전용 — 신뢰 경계를 새로 만들지 않는다. 입력 파싱/역직렬화 변경 없음. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-CGL-01 | Denial of Service | PLC 고정폭 파서가 가변 길이 필드로 오프셋을 잘못 읽어 이후 필드까지 밀림 | mitigate | 이번 수정 자체가 이 위협의 근본 수정(부호 고정 → 필드 길이 고정). Task 1 verify 가 포맷 문자열 존재를 게이트. |
| T-CGL-02 | Tampering | `$RESULT`(TestResultPacket) 계열 숫자 포맷이 실수로 함께 바뀌어 기존 PLC 파싱이 깨짐 | mitigate | 스코프를 `BuildAlignItems` 내부 리터럴 1곳으로 한정, verify 에서 무관 7곳 `ToString("0.000")` 원문 카운트 게이트로 회귀 차단. |
| T-CGL-SC | Tampering | npm/pip/cargo 패키지 설치 | n/a | 이 플랜은 외부 패키지를 일절 추가하지 않는다(신규 using/참조 0개). 공급망 표면 없음. |
</threat_model>

<verification>
정적 검증(Task 1 verify 에 포함, 실행자가 그대로 수행):
1. **한 줄만 수정 증명** — `git diff --numstat` 삭제 1 / 추가 1.
2. **신규 포맷 문자열 존재** — `"+0.000;-0.000;+0.000"` grep 1건.
3. **기존 무관 포맷 무변경 증명** — `$RESULT` 전용 7곳의 `ToString("0.000")` 이 코드줄 기준 그대로 7건.
4. **Debug/x64 빌드** — `Build succeeded`, 신규 `error CS` 0건(스크래치 OutDir, 실제 bin/obj 미접촉 — 현재 워킹트리에 사용자의 미커밋 csproj 변경이 있어 실제 출력 폴더를 건드리지 않는다).

실기 확인(선택, 이 플랜 범위 밖 — TCP 클라이언트가 없어 이번 세션에서는 불가):
- `$ALIGN_RESULT:TRAY,1,OK,OffsetX=+12.340,OffsetY=-12.340,Theta=+1.450@` 형태로 양수/음수/0 각각 실제 응답 확인.
</verification>

<success_criteria>
- `BuildAlignItems` 이 만드는 `$ALIGN_RESULT` 응답의 `OffsetX`/`OffsetY`/`Theta` 값이 양수/0/음수 모두 선두 부호(+/-) 1글자를 갖는다.
- 0 값도 `+0.000` 으로 부호가 붙는다.
- `$RESULT`(TestResultPacket) 계열 숫자 포맷은 완전히 무변경.
- Debug/x64 빌드 신규 `error CS` 0건.
</success_criteria>

<output>
Create `.planning/quick/260810-cgl-align-result-tcp-offsetx-offsety-theta-0/260810-cgl-SUMMARY.md` when done.

커밋 시 이 파일만 스테이징한다 (현재 워킹트리에 사용자의 미커밋 `WPF_Example/DatumMeasurement.csproj` 변경이 있으므로 절대 함께 커밋하지 않는다):
```
git add WPF_Example/TcpServer/VisionResponsePacket.cs
```
</output>
</content>
