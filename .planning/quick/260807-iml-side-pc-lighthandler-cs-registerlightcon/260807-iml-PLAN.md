---
phase: quick-260807-iml
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Device/LightHandler.cs
autonomous: true
requirements: [LIGHT-REMAP-8-5]

must_haves:
  truths:
    - "Controller index 0 (COM2) 이 8채널로 등록되고, 물리 채널 순서가 RING_CH1, RING_CH2, RING_CH3, RING_CH4, RING_CH5, RING_CH6, BACK, RING7 이다"
    - "Controller index 1 (COM3) 이 5채널로 등록되고, 물리 채널 순서가 BAR_1, BAR_2, BAR_3, BAR_4, ALIGN_COAX 이다"
    - "두 컨트롤러 각각 생성자에 넘긴 채널 갯수와 SetChannelNames 에 넘긴 이름 갯수가 정확히 일치한다 — VirtualLightController.SetChannelNames 는 MaxChannel 초과분을 예외 없이 조용히 break 로 버리므로, 불일치 시 이름이 무음 유실된다"
    - "13개 물리 채널 이름이 두 컨트롤러에 걸쳐 각각 정확히 1회씩만 등록된다 — 누락 0, 중복 0 (LightHandler.WarnOnDuplicateChannelNames 가 경고를 찍지 않고, TryFindChannel 이 13개 전부를 해석할 수 있다)"
    - "LightGroup 5종(RING/BACK/BAR/RING7/ALIGN_COAX) 등록 블록은 단 1바이트도 변하지 않는다 — 그룹은 컨트롤러/채널 인덱스가 아니라 채널 '이름' 으로 해석되므로 신규 배치에서도 그대로 유효하다"
    - "LIGHT_* 상수 15개의 이름과 문자열 리터럴 값은 전부 무변경이다 — 이번 작업은 '어느 컨트롤러에 붙는가' 만 바꾸는 재배치이지, 채널 이름 자체를 바꾸는 작업이 아니다"
    - "구 7채널/6채널 분할을 설명하는 주석이 파일 어디에도 남지 않는다 (XML doc 의 D-06/D-07 라인 + 상수 블록 헤더 2개 + 메서드 내 인라인 2개, 총 4+2 지점)"
    - "Debug/x64 재빌드가 신규 error CS 0 / 신규 warning CS 0 으로 통과한다"
  artifacts:
    - path: "WPF_Example/Custom/Device/LightHandler.cs"
      provides: "RegisterLightController() 의 8채널/5채널 컨트롤러 등록 + 신규 배치와 일치하는 상수 블록/문서 주석"
      contains: "new JPFLightController(0, 8)"
  key_links:
    - from: "JPFLightController 생성자 2번째 인자(채널 갯수)"
      to: "VirtualLightController.Channels 배열 크기 = MaxChannel"
      via: "Channels = new ChannelInfo[MaxChannel] — 이 크기가 SetChannelNames 의 무음 절단 경계선이다"
      pattern: "new JPFLightController\\((0, 8|1, 5)\\)"
    - from: "SetChannelNames 에 넘긴 논리 이름들"
      to: "LightHandler.TryFindChannel(name) / LightGroup.AddChannel(name)"
      via: "이름 기반 조회 — 그래서 Groups.Add 블록이 인덱스 변경에 영향받지 않는다"
      pattern: "Groups\\.Add\\(new LightGroup\\("
    - from: "LightHandler.Load() 의 light.ini ChannelNames override"
      to: "이번에 코드로 심은 기본 채널 이름"
      via: "Load() 는 키가 있으면 코드 기본값을 덮어쓴다 — 구 7/6 배치로 저장된 light.ini 가 남아있으면 런타임에 재배치가 무효화된다(본 계획 범위 밖, 사용자가 별도 처리)"
      pattern: "ChannelNames"
---

<objective>
SIDE PC 의 조명 채널 배선을 물리 재편성에 맞춰 코드에 반영한다.

`WPF_Example/Custom/Device/LightHandler.cs` 의 `RegisterLightController()` 에서 두 JPF 컨트롤러의 채널 갯수와 채널 이름 배치를 바꾼다:
- Controller A (Index=0, COM2): 7채널 → **8채널**. Ring 6분할 뒤에 `BACK` 과 `RING7` 이 합류하고, `ALIGN_COAX` 는 빠진다.
- Controller B (Index=1, COM3): 6채널 → **5채널**. `BACK` 과 `RING7` 이 빠지고, Bar 4채널 뒤에 `ALIGN_COAX` 가 합류한다.

Purpose: 형제 TOP/BOTTOM PC 체크아웃에 이미 적용된 동일 변경을 이 SIDE PC 저장소에 미러링하는 작업이다. 신규 기능이 아니라 **한 메서드 안의 데이터/설정 재매핑 + 그에 딸린 주석 정합화**다. 채널 이름도, 그룹 구성도, 호출부도 바뀌지 않는다 — 오직 "어느 물리 컨트롤러의 몇 번 채널이 어떤 논리 이름을 갖는가" 만 바뀐다.

Output: `WPF_Example/Custom/Device/LightHandler.cs` 1개 파일 수정 + Debug/x64 빌드 PASS.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

@WPF_Example/Custom/Device/LightHandler.cs
</context>

<interface_context>
수정 대상 파일이 의존하는 기존 API — 전부 **무수정**이며, 아래 계약을 이해한 상태로 편집할 것.

**`VirtualLightController` (`WPF_Example/Device/LightController/VirtualLightController.cs`)**
- `VirtualLightController(int index, int maxChannel = LightHandler.CHANNEL_LIMIT)` — 생성자가 `MaxChannel = maxChannel; Channels = new ChannelInfo[MaxChannel];` 로 **채널 배열 크기를 확정**한다. 이후 늘어나지 않는다.
- `VirtualLightController SetChannelNames(params string[] names)` — `for (i...) { if (i >= MaxChannel) break; Channels[i].Name = names[i]; }`. **초과분을 예외 없이 조용히 버린다.** 즉 갯수 인자보다 이름을 많이 넘기면 뒤쪽 이름이 무음 유실되고, 적게 넘기면 남는 채널이 기본명 `"Channel N"` 으로 남는다. 이 계획의 핵심 위험 지점이며, 정적 게이트로 방어한다.
- `int ChannelCount { get => Channels.Length; }` — `MaxChannel` 과 동일한 값.

**`JPFLightController` (`WPF_Example/Device/LightController/JPFLightController.cs`)**
- `JPFLightController(int index, int maxChannel = LightHandler.CHANNEL_LIMIT) : base(index, maxChannel)` — 위 계약을 그대로 위임한다.

**`LightHandler` (framework 측, `WPF_Example/Device/LightController/LightHandler.cs`)**
- `public const int CHANNEL_LIMIT = 8;` — **8이 상한이다.** Controller A 의 신규 8채널은 정확히 이 상한과 같아서 합법이며, 9 이상이었다면 `CmdTable` / `Execute()` 루프가 채널을 못 돈다.
- `TryFindChannel(string channelName)` — 이름으로 `(controllerIndex, channel)` 을 역조회한다. `SetOnOff`/`SetLevel` 이 전부 이 경로를 탄다. 이름이 없으면 에러 로그만 찍고 **무동작**한다.
- `LightGroup.AddChannel(...)` / `RebindChannels()` — 그룹은 **채널 이름** 으로 실제 채널을 찾는다. 그래서 컨트롤러/인덱스 재배치가 그룹 정의에 영향을 주지 않는다.

**호출부 조사 결과:** 저장소 전체에서 리터럴 컨트롤러/채널 인덱스로 조명을 직접 제어하는 호출부는 **0건**이다(전부 이름/그룹 경유). 따라서 이 재배치의 파급 범위는 이 파일 하나로 닫힌다.
</interface_context>

<tasks>

<task type="tracer">
  <name>Task 1: RegisterLightController 8채널/5채널 재배치 + 딸린 주석 정합화</name>
  <files>WPF_Example/Custom/Device/LightHandler.cs</files>

  <action>
파일 하나 안에서 **3개 지점**을 고친다. 상수 값·그룹 정의·타 파일은 건드리지 않는다.

**(1) 클래스 상단 LIGHT_* 상수 블록 재편성 (선언 순서 + 헤더 주석만)**

현재 상수는 "Controller A 소속" / "Controller B 소속" 두 덩어리로 주석 구분되어 선언되어 있는데, 재배치 후에는 그 구분이 사실과 어긋난다. 상수 **이름과 문자열 리터럴 값은 15개 전부 그대로 두고**, 소속만 맞게 두 덩어리로 다시 묶는다.

- 첫 번째 덩어리(Controller A 소속) = `LIGHT_RING_CH1` ~ `LIGHT_RING_CH6` 6줄에 이어 `LIGHT_BACK`, `LIGHT_RING7` 2줄을 옮겨 붙여 총 8줄.
- 두 번째 덩어리(Controller B 소속) = `LIGHT_BAR_1` ~ `LIGHT_BAR_4` 4줄에 이어 `LIGHT_ALIGN_COAX` 1줄을 옮겨 붙여 총 5줄. `LIGHT_ALIGN_COAX` 줄 끝의 기존 후행 주석은 그대로 따라간다.
- 각 덩어리 위의 헤더 주석 2개는 지금 붙어 있는 옛 Phase-64 날짜 스탬프를 버리고 `//260807 hbk` 스탬프로 다시 쓰되, 본문이 실제 신규 소속을 서술하게 한다. Controller A 는 COM2 8채널(Ring 6분할 + 면조명 + 링조명2), Controller B 는 COM3 5채널(바조명 4 + Align 동축).
- 세 번째 덩어리인 그룹 이름 상수(`LIGHT_RING`, `LIGHT_BAR`)와 그 헤더 주석은 **완전 무수정**이다. 이건 컨트롤러 소속과 무관한 논리 그룹명이다.

C# 은 필드 선언 순서에 의미를 두지 않으므로 이 재배치는 동작에 영향이 없다. 값 문자열을 단 한 글자라도 바꾸면 light.ini / 레시피와의 이름 계약이 깨지므로 절대 금지.

**(2) `RegisterLightController()` XML doc 주석의 D-06 / D-07 라인 갱신**

`<summary>` 블록 안 D-06 라인은 Controller A(Index=0)의 구성과 채널 수를, D-07 라인은 Controller B(Index=1)의 구성과 채널 수를 서술한다. 두 라인 모두 현재 구(舊) 분할 기준으로 쓰여 있으니, D-06 은 "Ring CH1~CH6 + Back + Ring7 = 여덟 채널", D-07 은 "Bar×4 + AlignCoax = 다섯 채널" 이 되도록 다시 쓴다. D-06/D-07 이라는 결정 ID 자체는 유지한다. 같은 블록의 D-08(Ring 6채널 RING 통합 그룹 동시 제어), D-09(LightGroup 5종) 라인은 신규 배치에서도 여전히 참이므로 **무수정**.

**(3) 메서드 본문의 두 `Controllers.Add(...)` 호출 교체**

사용자가 축자(verbatim)로 지정한 최종 형태로 교체한다. 줄바꿈 위치까지 아래 그대로 맞출 것 — 정적 게이트가 이 줄바꿈에 앵커링되어 있다.

- 첫 호출: 인라인 주석 `//260807 hbk Controller A(COM2) — Ring CH1~CH6 + 면조명(Back) + 링조명2(Ring7)` 아래, `Controllers.Add(new JPFLightController(0, 8)` 로 시작해 `.SetChannelNames(` 을 체이닝하고, 인자를 3줄로 끊어 1줄째 `LIGHT_RING_CH1, LIGHT_RING_CH2, LIGHT_RING_CH3,` / 2줄째 `LIGHT_RING_CH4, LIGHT_RING_CH5, LIGHT_RING_CH6,` / 3줄째 `LIGHT_BACK, LIGHT_RING7));` 로 닫는다. 채널 갯수 인자는 일곱에서 여덟로 올라가고, 이름은 여덟 개다.
- 둘째 호출: 인라인 주석 `//260807 hbk Controller B(COM3) — 바조명×4 + Align 동축` 아래, `Controllers.Add(new JPFLightController(1, 5)` 로 시작해 `.SetChannelNames(` 을 체이닝하고, 인자를 2줄로 끊어 1줄째 `LIGHT_BAR_1, LIGHT_BAR_2, LIGHT_BAR_3,` / 2줄째 `LIGHT_BAR_4, LIGHT_ALIGN_COAX));` 로 닫는다. 채널 갯수 인자는 여섯에서 다섯으로 내려가고, 이름은 다섯 개다.

두 호출 모두 **갯수 인자 = 이름 갯수** 라는 불변식을 만족해야 한다(`SetChannelNames` 무음 절단 방지).

**(4) 절대 건드리지 말 것 — `Groups.Add(...)` 블록**

메서드 후반의 `LightGroup` 등록 5줄 묶음(RING / BACK / BAR / RING7 / ALIGN_COAX)과 그 위의 설명 주석들은 **한 글자도 수정하지 않는다.** 사용자가 제시한 축자 코드 조각에는 이 블록이 `// Groups.Add(...) 5줄은 그대로 유지` 한 줄로 요약되어 있는데, 이건 "요약 주석으로 대체하라" 는 뜻이 **아니라** "이 부분은 diff 대상이 아니니 원문 유지" 라는 표기다. 축자 조각을 통째로 붙여넣어 이 5개 등록을 날리면 조명 그룹 제어 전체가 무음으로 죽는다 — 이 작업에서 가장 큰 단일 위험이다. 반드시 기존 5개 `Groups.Add(new LightGroup(...))` 호출과 그 주석을 원문 그대로 남길 것.
  </action>

  <verify>
    <automated>F=/c/code/DataMeasurement/WPF_Example/Custom/Device/LightHandler.cs; echo "G1(=1): $(grep -v '^[[:space:]]*//' "$F" | grep -c 'new JPFLightController(0, 8)')"; echo "G2(=1): $(grep -v '^[[:space:]]*//' "$F" | grep -c 'new JPFLightController(1, 5)')"; echo "G3(=0): $(grep -c 'JPFLightController(0, 7)\|JPFLightController(1, 6)' "$F")"; echo "G4(=5): $(grep -c 'Groups.Add(new LightGroup(' "$F")"; echo "G5(=0): $(grep -c '= 7채널\|= 6채널' "$F")"; echo "G6(=0): $(grep -c '260625 hbk Phase 64 LIGHT-01: Controller' "$F")"; echo "G7(=15): $(grep -c 'public const string LIGHT_' "$F")"; echo "G8a(=1): $(grep -A4 'new JPFLightController(0, 8)' "$F" | grep -c 'LIGHT_BACK, LIGHT_RING7))')"; echo "G8b(=1): $(grep -A4 'new JPFLightController(1, 5)' "$F" | grep -c 'LIGHT_BAR_4, LIGHT_ALIGN_COAX))')"</automated>
  </verify>

  <done>
9개 게이트가 전부 기대값과 일치한다: G1=1, G2=1, G3=0, G4=5, G5=0, G6=0, G7=15, G8a=1, G8b=1.

의미상으로는 — 컨트롤러 0이 8채널 arity 로 선언되고 그 이름 목록이 `LIGHT_BACK, LIGHT_RING7` 로 끝나며, 컨트롤러 1이 5채널 arity 로 선언되고 그 이름 목록이 `LIGHT_BAR_4, LIGHT_ALIGN_COAX` 로 끝난다. 구 arity 표기는 코드/주석 어디에도 없고, 구 분할을 설명하던 문서 주석과 Controller 소속 헤더 스탬프도 모두 사라졌다. `LIGHT_*` 상수는 여전히 15개 선언 그대로이고, `LightGroup` 등록은 정확히 5개로 보존되어 있다.
  </done>
</task>

<task type="auto">
  <name>Task 2: 채널 이름 유일성 정적 검증 + Debug/x64 빌드</name>
  <files>WPF_Example/Custom/Device/LightHandler.cs</files>
  <precondition>`DatumMeasurement.exe` 가 실행 중이면 안 된다 — 실행 중이면 msbuild 가 출력 DLL/EXE 파일 잠금으로 MSB3027/MSB3021 을 낸다. 빌드 전에 프로세스를 종료하거나, 아래 fallback 의 `/p:OutputPath` 우회를 쓴다.</precondition>

  <action>
Task 1 의 편집이 **의미적으로도** 안전한지 확인한 뒤 컴파일한다. 두 단계 모두 읽기 전용이며, 실패 시에만 Task 1 로 돌아가 수정한다.

**(1) 13개 물리 채널 이름 유일성 검사**

`RegisterLightController()` 안의 두 `SetChannelNames(...)` 인자 목록을 합쳤을 때, 13개 채널 상수(`LIGHT_RING_CH1`~`LIGHT_RING_CH6`, `LIGHT_BACK`, `LIGHT_RING7`, `LIGHT_BAR_1`~`LIGHT_BAR_4`, `LIGHT_ALIGN_COAX`)가 **각각 정확히 1회씩** 나타나야 한다. 누락되면 그 조명은 `TryFindChannel` 에서 영영 안 잡혀 무음 무동작이 되고, 중복되면 `WarnOnDuplicateChannelNames` 가 경고를 찍으면서 뒤쪽 등록이 조회 불가가 된다. 아래 verify 커맨드가 두 `Controllers.Add` 호출 구간만 잘라내 각 상수의 출현 횟수를 세고, 1이 아닌 항목을 전부 출력한다. 출력이 비어 있어야 통과.

**(2) Debug/x64 빌드**

저장소 루트에서 `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal` 를 돌린다. 이 저장소에서 확립된 표준 빌드 커맨드다. 신규 `error CS` 0건 / 신규 `warning CS` 0건이어야 한다.

빌드가 `MSB3027` 또는 `MSB3021`(파일이 다른 프로세스에 의해 사용 중)로 실패하면 앱이 떠 있는 것이다. 앱을 닫고 재시도하거나, 컴파일 가능 여부만 확인하면 되므로 `/p:OutputPath=C:\Users\admin\AppData\Local\Temp\claude\C--code-DataMeasurement\0da39e39-7e39-40eb-8182-41eca9b2accd\scratchpad\bin-iml\` 를 덧붙여 별도 출력 폴더로 우회한다.

코드 변경이 상수 재정렬 + 리터럴 인자 2개 + 주석뿐이라 컴파일 리스크는 낮지만, 상수 블록을 옮기는 과정에서 줄이 유실되면 여기서 `CS0103`(정의되지 않은 이름)으로 즉시 잡힌다 — 그게 이 빌드 게이트의 실질적 목적이다.
  </action>

  <verify>
    <automated>F=/c/code/DataMeasurement/WPF_Example/Custom/Device/LightHandler.cs; REG=$(awk '/Controllers\.Add\(new JPFLightController\(0,/,/^$/' "$F"; awk '/Controllers\.Add\(new JPFLightController\(1,/,/^$/' "$F"); for n in LIGHT_RING_CH1 LIGHT_RING_CH2 LIGHT_RING_CH3 LIGHT_RING_CH4 LIGHT_RING_CH5 LIGHT_RING_CH6 LIGHT_BACK LIGHT_RING7 LIGHT_BAR_1 LIGHT_BAR_2 LIGHT_BAR_3 LIGHT_BAR_4 LIGHT_ALIGN_COAX; do c=$(printf '%s\n' "$REG" | grep -o "\b$n\b" | grep -c .); [ "$c" = "1" ] || echo "FAIL $n=$c"; done; echo "uniqueness-check-done"</automated>
    <automated>cd /c/code/DataMeasurement && msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal 2>&1 | grep -E "error CS|warning CS|Build succeeded|Build FAILED" | tail -20</automated>
  </verify>

  <done>
유일성 검사가 `FAIL ...` 를 한 줄도 출력하지 않고 `uniqueness-check-done` 만 찍는다(13개 채널명이 두 컨트롤러 등록 구간에 각각 정확히 1회 등장).

msbuild 이 `Build succeeded` 로 끝나고 신규 `error CS` 0건 / 신규 `warning CS` 0건이다.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 코드 채널 배치 → 물리 조명 컨트롤러(COM2/COM3 시리얼) | 논리 채널 이름이 실제 점등되는 물리 채널을 결정한다. 잘못 매핑되면 예외 없이 "엉뚱한 조명이 켜지거나 아무것도 안 켜진다" — 검사 이미지 품질이 조용히 무너지는 무음 실패 경로다. |
| 코드 기본 채널명 → `light.ini` (`D:\Data\Light\light.ini`) ChannelNames override | `LightHandler.Load()` 가 ini 키가 존재하면 코드 기본값을 **덮어쓴다.** 즉 코드만 고쳐도 런타임 배치는 ini 가 최종 결정권을 가진다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-IML-01 | Tampering | `SetChannelNames` 무음 절단 (`if (i >= MaxChannel) break;`) | high | mitigate | 갯수 인자와 이름 갯수 일치를 Task 1 의 G1/G2/G8a/G8b 및 Task 2 의 13개 채널명 유일성 검사로 정적 강제 |
| T-IML-02 | Denial of Service | `Groups.Add(new LightGroup(...))` 5종 등록 유실 | critical | mitigate | 사용자 축자 조각의 요약 주석을 그대로 붙여넣으면 5개 그룹이 삭제되어 전 조명 그룹 제어가 무음 정지 — Task 1 action 에 명시 경고 + G4 게이트(정확히 5)로 차단 |
| T-IML-03 | Information Disclosure (오정보) | 구 7/6 분할을 설명하는 잔존 주석 | medium | mitigate | G5(구 채널 수 표기 0건) + G6(구 Controller 소속 헤더 스탬프 0건) 게이트 |
| T-IML-04 | Tampering | 구 배치로 저장된 `light.ini` 의 `ChannelNames` override 가 런타임에 신규 배치를 무효화 | high | transfer | **본 계획 범위 밖 — 사용자가 light.ini / Setting.ini 를 별도로 직접 처리 중.** 아래 `<verification>` 의 운영 인수인계 항목 참고. 코드로는 방어하지 않는다(구조 변경 금지 원칙, 260713-nse) |
| T-IML-05 | Elevation of Privilege | 신규 패키지 설치 | low | accept | 패키지 설치 없음 — npm/pip/cargo 작업 0건, 공급망 표면 미변경 |
</threat_model>

<verification>
## 자동 검증 (이 계획이 책임지는 범위)

1. Task 1 의 9개 정적 게이트 전부 기대값 일치
2. Task 2 의 13개 채널명 유일성 검사 무결
3. `msbuild ... /p:Configuration=Debug /p:Platform=x64` → `Build succeeded`, 신규 error/warning CS 0

## 범위 밖 — 운영 인수인계 (코드로 고치지 말 것)

**`light.ini` 동기화 (T-IML-04).** `LightHandler.Load()` 는 `D:\Data\Light\light.ini` 의 `[Controller0]`/`[Controller1]` 섹션에 `ChannelNames` 키가 있으면 **이번에 코드에 심은 기본 배치를 덮어쓴다.** 구 7채널/6채널 시절에 `Save()` 로 되저장된 ini 가 그대로 남아 있으면:
- `[Controller0]` 의 옛 7개 이름이 신규 8채널 중 앞 7개를 덮어써 `BACK`/`RING7` 자리에 옛 이름이 들어가고,
- `[Controller1]` 의 옛 6개 이름은 신규 `ChannelCount`(5)에서 잘려 앞 5개만 반영되어 `ALIGN_COAX` 자리에 엉뚱한 이름이 들어간다.

결과는 예외 없는 무음 오배선이다. **사용자가 이 파일을 이번 작업과 별도로 직접 처리하기로 확인했으므로, 이 계획에서는 코드/ini 어느 쪽도 손대지 않는다.** 실행 에이전트는 `light.ini`, `Setting.ini`, 그 밖에 `WPF_Example/Custom/Device/LightHandler.cs` 이외의 어떤 파일도 수정하지 않는다. SUMMARY 에 이 의존성만 기록해 사용자에게 넘긴다.

**실기 조명 점등 확인.** 실제 COM2/COM3 조명이 새 배치대로 켜지는지는 하드웨어 UAT 영역이며, 위 ini 동기화가 끝난 뒤에야 의미가 있다. 이번 quick task 의 완료 조건이 아니다.
</verification>

<success_criteria>
- [ ] Controller index 0 이 `new JPFLightController(0, 8)` 로, 8개 채널명(`RING_CH1`~`RING_CH6`, `BACK`, `RING7`)을 그 순서대로 등록한다
- [ ] Controller index 1 이 `new JPFLightController(1, 5)` 로, 5개 채널명(`BAR_1`~`BAR_4`, `ALIGN_COAX`)을 그 순서대로 등록한다
- [ ] 두 컨트롤러 모두 갯수 인자 == 이름 갯수 (무음 절단 없음)
- [ ] 13개 물리 채널명이 등록 구간에 각각 정확히 1회 (누락 0, 중복 0)
- [ ] `Groups.Add(new LightGroup(...))` 5개가 원문 그대로 보존 (G4=5)
- [ ] `public const string LIGHT_*` 선언 15개의 이름/값 무변경 (G7=15)
- [ ] 구 7/6 분할 서술 주석 전멸 (G5=0, G6=0)
- [ ] `WPF_Example/Custom/Device/LightHandler.cs` 외 수정 파일 0개
- [ ] Debug/x64 빌드 PASS, 신규 error CS 0 / 신규 warning CS 0
</success_criteria>

<output>
Create `.planning/quick/260807-iml-side-pc-lighthandler-cs-registerlightcon/260807-iml-SUMMARY.md` when done.

SUMMARY 에 반드시 포함할 것:
- 최종 채널 배치표 (컨트롤러 인덱스 / COM 포트 / 채널 번호 0-base / 논리 이름)
- 9개 정적 게이트 실측값 + 유일성 검사 결과 + msbuild 결과
- **미결 의존성 명시**: `light.ini` 의 `ChannelNames` override 가 구 배치로 남아 있으면 이 코드 변경이 런타임에 무효화된다 — 사용자 별도 처리 대기 중 (T-IML-04)
</output>
