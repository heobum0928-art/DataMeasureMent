---
phase: quick-260713-jqk
plan: 01
type: execute
wave: 1
depends_on: []
files_modified: [.planning/LIGHT-Z-MANUAL-VERIFICATION.md]
autonomous: true
requirements: [DOC-8-7]
must_haves:
  truths:
    - "LIGHT-Z-MANUAL-VERIFICATION.md에 '### 8-7. 조명 명령이 버튼 클릭부터 실제 LED까지 가는 전체 흐름' 섹션이 존재한다"
    - "8-7 섹션은 8-6 섹션 주의 문단 바로 뒤, '---' 구분선 앞에 위치한다"
    - "8-6 이전 섹션, 목차, '앞으로 정리해야 할 것' 섹션은 변경되지 않았다"
  artifacts:
    - path: ".planning/LIGHT-Z-MANUAL-VERIFICATION.md"
      provides: "8-7 조명 명령 흐름 추적 섹션 추가된 문서"
      contains: "### 8-7. 조명 명령이 버튼 클릭부터 실제 LED까지 가는 전체 흐름"
  key_links: []
---

<objective>
`LIGHT-Z-MANUAL-VERIFICATION.md`에 8-7 조명 명령 흐름 추적(코드 따라가기) 섹션을 추가한다.

Purpose: 조명 명령이 버튼 클릭부터 실제 LED까지 도달하는 전체 코드 경로를 문서화하여, 유지보수자가 파일을 순서대로 따라가며 흐름을 검증할 수 있게 한다.
Output: 8-7 섹션이 추가된 LIGHT-Z-MANUAL-VERIFICATION.md
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/LIGHT-Z-MANUAL-VERIFICATION.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: 8-7 섹션 삽입 후 커밋</name>
  <files>.planning/LIGHT-Z-MANUAL-VERIFICATION.md</files>
  <action>
순수 문서 삽입 작업이다. 코드 파일(.cs/.xaml)은 절대 건드리지 말 것.

Edit 도구로 아래 old_string(현재 598~600번째 줄)을 new_string으로 교체한다.

old_string:
```
**주의**: `Controller0`/`Controller1`이라는 섹션 이름과 `Port`/`Baudrate`라는 키 이름은 **정확히 이 철자 그대로**여야 코드가 읽습니다. Port 번호(3, 5)는 예시일 뿐이고, PC 2대 각각 자기 컴퓨터에서 실제 COM 번호를 확인해서 따로 파일을 만들어야 합니다(7번 섹션 STEP 2 참고).

---
```

new_string (아래 블록에서 8-7 섹션 전체를 주의 문단과 `---` 사이에 그대로 삽입 — 1바이트도 바꾸지 말 것):
```
**주의**: `Controller0`/`Controller1`이라는 섹션 이름과 `Port`/`Baudrate`라는 키 이름은 **정확히 이 철자 그대로**여야 코드가 읽습니다. Port 번호(3, 5)는 예시일 뿐이고, PC 2대 각각 자기 컴퓨터에서 실제 COM 번호를 확인해서 따로 파일을 만들어야 합니다(7번 섹션 STEP 2 참고).

### 8-7. 조명 명령이 버튼 클릭부터 실제 LED까지 가는 전체 흐름 (코드 따라가기)

버튼 하나 눌렀을 때, 실제로 코드가 어떤 파일들을 순서대로 거쳐서 진짜 조명까지 도달하는지 — 파일을 하나씩 열어보면서 순서대로 확인하는 방법입니다.

**① 트리거 발생** — `WPF_Example/Custom/SystemHandler.cs`, `ProcessPrep(PrepPacket packet)` (~700번째 줄)
파일 열어서 이 함수 찾기. `packet.Op != 0`이면 `_lastPrepZIndex` 저장 후 `ApplyPrepToSequences` 호출하는 줄 확인.

**② 시퀀스에 전파** — 같은 파일, `ApplyPrepToSequences(int nZIndex)` (~737번째 줄)
`Sequences`를 순회하면서 `InspectionSequence` 타입인 것마다 `inspSeq.ApplyShotLights(nZIndex)` 호출하는 for문 확인.

**③ Shot 찾기** — `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`, `ApplyShotLights(int nZIndex)` (~321번째 줄)
`FindShotByZIndex(nZIndex)`로 z_index에 맞는 Shot(레시피 설정)을 찾는 부분 확인.

**④ 조명값 적용 시도** — 같은 파일, `ApplyShotLightsInternal(ShotConfig shot)` (~350번째 줄)
`shot.RingLight_Enabled`/`Ring7Light_Enabled`/`BackLight_Enabled`/`SideLight_Enabled`를 하나씩 확인하며 `LightHandler.Handle.SetOnOff(...)`/`SetLevel(...)` 부르는 부분 확인.

**⑤ 그룹→채널 전개** — `WPF_Example/Device/LightController/LightHandler.cs`, `SetOnOff(string groupName, bool)` / `SetLevel(string groupName, int)` (175/188번째 줄)
그룹 이름(예: "RING")으로 `LightGroup`을 찾아서, 그 안 채널들 각각에 대해 `SetOnOff(index, channel, ...)`를 부르는 for문 확인.

**⑥ 명령 예약 (아직 실제로 안 나감)** — 같은 파일, `SetOnOff(int index, int channel, bool)` (271번째 줄 근처)
`CmdTable[index, channel]`에 값만 기록하고 바로 끝나는 것 확인 — 여기까진 시리얼 신호가 아직 안 나갑니다.

**⑦ 실제 전송 (별도 스레드)** — 같은 파일, `Execute()` (382번째 줄)
`mThread = new Thread(Execute)`(93번째 줄)로 시작되는 조명 전용 백그라운드 스레드. `while(!IsTerminated)` 루프 안에서 `CmdTable`을 훑다가 `Controllers[i].WriteOnOff(j, ...)`를 부르는 부분 확인 — 여기서 진짜 시리얼로 나갑니다.

**⑧ 물리 신호** — `WPF_Example/Device/LightController/JPFLightController.cs`, `WriteOnOff`/`WriteLevel`
`mPort.WriteLine("#A{채널}{값}&")` 형태로 실제 문자열이 시리얼 포트로 나가는 부분 확인. 이게 조명 컨트롤러 박스가 받는 진짜 명령입니다.

**왜 ⑥에서 바로 안 켜지고 ⑦을 거치나**: 시리얼 통신은 느려서(수십 ms), 검사 시퀀스나 버튼 클릭이 이걸 직접 기다리면 화면이 멈추거나 다른 검사가 밀립니다. 그래서 "명령을 적어놓기"(⑥)와 "실제로 보내기"(⑦⑧)를 분리해, 앞부분은 즉시 끝나고 뒷부분은 조용히 백그라운드에서 처리되게 만들었습니다 — 프린터에 "인쇄" 누르면 바로 다른 일을 할 수 있고 프린터가 알아서 뒤에서 인쇄하는 것과 같은 원리입니다.

**확인 방법**: 위 순서대로 파일들을 하나씩 열어서, 함수 이름으로 검색(Ctrl+F)해가며 실제로 이 순서대로 호출이 이어지는지 눈으로 따라가 보세요. 코드 에디터의 "정의로 이동"(F12) 기능을 쓰면 더 빠르게 따라갈 수 있습니다.

---
```

교체 후 git 커밋:
```bash
gsd-sdk query commit "docs(quick-260713-jqk): LIGHT-Z-MANUAL-VERIFICATION.md 8-7 조명 흐름 추적 섹션 추가" .planning/LIGHT-Z-MANUAL-VERIFICATION.md
```
  </action>
  <verify>
    <automated>grep -c "### 8-7. 조명 명령이 버튼 클릭부터 실제 LED까지 가는 전체 흐름" .planning/LIGHT-Z-MANUAL-VERIFICATION.md</automated>
  </verify>
  <done>8-7 섹션이 8-6 주의 문단 뒤 `---` 앞에 정확히 삽입되고, 이전 섹션/목차/'앞으로 정리해야 할 것'은 무변경. 커밋 완료.</done>
</task>

</tasks>

<verification>
- `### 8-7.` 헤더가 문서에 정확히 1회 존재
- 8-6 섹션 주의 문단 바로 뒤에 위치, 그 뒤로 `---` → `## 앞으로 정리해야 할 것` 순서 유지
- 목차 및 다른 섹션 변경 없음 (git diff가 단일 삽입 블록만 표시)
</verification>

<success_criteria>
- LIGHT-Z-MANUAL-VERIFICATION.md에 8-7 섹션이 지정된 위치에 삽입됨
- 삽입 내용이 제공된 원문과 1바이트도 다르지 않음
- 코드 파일 무변경, 문서 순수 추가만
- 커밋 완료
</success_criteria>

<output>
After completion, create `.planning/quick/260713-jqk-light-z-manual-verification-md-8-7/260713-jqk-SUMMARY.md`
</output>
