---
phase: 260805-mze
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: true
requirements: [MZE-01]

must_haves:
  truths:
    - "BOTTOM 일괄검사가 실행 중일 때 TOP 일괄검사 버튼을 누르면 '시퀀스가 이미 실행 중입니다.' 메시지가 뜨고 실행이 차단된다 (프로세스 크래시 없음)"
    - "어떤 시퀀스도 실행 중이 아닐 때 일괄검사 버튼을 누르면 기존과 동일하게 정상 실행된다"
    - "단일 RUN(Btn_start_Click)은 서로 다른 물리 카메라를 쓰는 시퀀스 간 동시 실행이 계속 가능하다 (Phase 69 동작 무회귀)"
    - "일괄검사 진입부의 lazy-rebuild 게이트는 Phase 69 상태(GetSequenceState(seqID) == Idle) 그대로 남아 있다"
  artifacts:
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "Btn_batchRun_Click 의 전역 IsIdle 차단 게이트 (Phase 69 이전 원문 복원)"
      contains: "if (!SystemHandler.Handle.Sequences.IsIdle) {"
  key_links:
    - from: "InspectionListView.xaml.cs :: Btn_batchRun_Click"
      to: "SequenceHandler.IsIdle"
      via: "전역 IsIdle 프로퍼티 직접 읽기 (StateAll == Idle)"
      pattern: "!SystemHandler\\.Handle\\.Sequences\\.IsIdle"
    - from: "InspectionListView.xaml.cs :: Btn_start_Click"
      to: "SequenceHandler.TryGetBlockingSequence"
      via: "시퀀스 단위 차단 판정 — 변경 금지, 유지되어야 함"
      pattern: "TryGetBlockingSequence"
---

<objective>
Phase 69(commit `ca88862`)가 `Btn_batchRun_Click`(일괄검사 버튼)의 차단 게이트를 전역 `Sequences.IsIdle` → 시퀀스 단위 `TryGetBlockingSequence`로 바꾼 것을 **그 한 지점만** 원문으로 되돌린다.

Purpose: `_batchService` / `_batchShots` / `_batchAccumulated` 는 시퀀스별로 분리되지 않은 **단일 공용 필드**다. 시퀀스 단위 판정이 "차단 안 함"으로 정확히 통과시킨 뒤 `_batchService = new BatchRunService();` 가 아직 실행 중인 다른 시퀀스의 참조를 덮어써 프로세스 크래시가 발생한다(사용자 실기 재현 확인: BOTTOM 일괄검사 도중 TOP 일괄검사 시작). 공용 필드를 시퀀스별로 분리하는 근본 수정 전까지, 크로스-시퀀스 일괄검사 동시 실행 자체를 전역 게이트로 막아 안전하게 만든다.

Output: `InspectionListView.xaml.cs` 1개 파일, 게이트 블록 1개 치환.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260805-mze-btn-batchrun-click-isidle/260805-mze-CONTEXT.md
@CLAUDE.md

<interfaces>
<!-- 실행자가 코드베이스를 탐색할 필요 없도록 필요한 계약을 여기에 전부 넣는다. -->

WPF_Example/Sequence/SequenceHandler.cs:108 — 복원 대상이 호출할 프로퍼티. 현재도 존재함(Phase 69는 순수 추가였고 IsIdle/StateAll 무변경).
```csharp
public bool IsIdle {
    get {
        return StateAll == EContextState.Idle;
    }
}
```

WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:32-35 — 크래시 원인이 되는 단일 공용 필드(이번 범위에서 **수정하지 않음**).
```csharp
private List<CycleResultDto> _batchAccumulated = new List<CycleResultDto>();
private BatchRunService _batchService;
private List<ShotConfig> _batchShots;
```
</interfaces>

<current_state>
2026-08-05 시점 `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` 실파일 확인 결과 (git diff 신뢰가 아닌 라이브 파일 직접 확인):

| 위치 | 라인 | 현재 상태 | 이번 조치 |
|------|------|-----------|-----------|
| `Btn_start_Click` 차단 체크 | 387-395 | `TryGetBlockingSequence` | **변경 금지 (유지)** |
| `Btn_start_Click` lazy-rebuild 게이트 | 455 | `seqHandler.GetSequenceState(seq.ID) == EContextState.Idle` | **변경 금지 (유지)** |
| `Btn_batchRun_Click` 차단 체크 | **548-557** | `TryGetBlockingSequence` | **← 이번 치환 대상 (유일)** |
| `Btn_batchRun_Click` lazy-rebuild 게이트 | 578-579 | `GetSequenceState(seqID) == EContextState.Idle` | **변경 금지 (유지)** |
| 범위 밖 `IsIdle == false` 4곳 | 980, 985, 1000, 1155 | 전역 `IsIdle == false` 형태 | **변경 금지 (유지)** |

라인 번호는 참고용이다. 실행자는 **문자열 매칭**으로 대상 블록을 찾아 치환하고, 라인 번호로 편집하지 말 것.
</current_state>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Btn_batchRun_Click 차단 게이트를 전역 IsIdle 원문으로 복원</name>

  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>

  <action>
`Btn_batchRun_Click` 메서드 내부에서 아래 **정확히 이 텍스트**(2026-08-05 라이브 파일 기준, 들여쓰기 12칸)를 찾는다. `Btn_start_Click` 에도 유사한 `TryGetBlockingSequence` 블록이 있으므로, 반드시 `CustomMessageBox.Show("일괄 검사",` (첫 인자가 `"일괄 검사"`) 인 쪽 — 즉 `Btn_batchRun_Click` 안의 블록만 대상으로 한다. `Btn_start_Click` 쪽은 첫 인자가 `"Error"` 이며 절대 건드리지 않는다.

**찾을 텍스트 (BEFORE):**
```csharp
            //260805 hbk Phase 69 D-01/D-03: 단일 RUN(Btn_start_Click)과 동일한 시퀀스 단위 판정을 쓴다.
            string sBlockingSeqName;
            if (SystemHandler.Handle.Sequences.TryGetBlockingSequence(seqID, out sBlockingSeqName)) {
                CustomMessageBox.Show("일괄 검사",
                    string.Format(
                        "실행할 수 없습니다 — '{0}' 시퀀스가 아직 Idle 이 아닙니다.\n(자기 자신이거나, 같은 물리 카메라를 공유하는 시퀀스입니다.)",
                        sBlockingSeqName),
                    MessageBoxImage.Error);
                return;
            }
```

**치환할 텍스트 (AFTER):**
```csharp
            // 일괄검사는 _batchService/_batchShots/_batchAccumulated 를 시퀀스별로 분리하지 않은 단일 공용 필드로
            // 관리한다. 시퀀스 단위 판정(TryGetBlockingSequence)으로 크로스-시퀀스 동시 실행을 허용하면, 뒤이은
            // _batchService = new BatchRunService() 가 아직 실행 중인 다른 시퀀스의 참조를 덮어써 크래시가 난다.
            // 공용 필드를 시퀀스별로 분리하기 전까지는 전역 IsIdle 게이트를 유지한다.
            if (!SystemHandler.Handle.Sequences.IsIdle) {
                CustomMessageBox.Show("일괄 검사", "시퀀스가 이미 실행 중입니다.", MessageBoxImage.Error);
                return;
            }
```

치환 규칙:
- `if` 문 4줄은 Phase 69 이전 **원문 그대로**다(`git show ca88862` diff 의 `-` 줄과 문자 단위로 일치). 메시지 문자열 `"시퀀스가 이미 실행 중입니다."` 를 임의로 바꾸지 말 것.
- 앞의 주석 4줄은 **동일 변경을 다시 시도하는 회귀를 막기 위한 "왜" 설명**이며 동작에 영향이 없다. 날짜/이니셜 접두사(`//YYMMDD hbk`) 는 붙이지 않는다(해당 규칙 폐기됨).
- 지역 변수 `string sBlockingSeqName;` 선언은 이 블록 안에서만 쓰이므로 함께 제거된다. `Btn_batchRun_Click` 의 나머지 부분에서 이 변수를 참조하지 않는지 확인하고, 참조가 남아 있으면 중단하고 보고할 것.
- 들여쓰기는 공백 12칸(기존 블록과 동일). 탭 사용 금지.

**절대 건드리지 말 것 (CONTEXT LOCKED):**
1. `Btn_start_Click` 의 `TryGetBlockingSequence` 체크 — 사용자 실기 Test 1 로 안전 확인됨.
2. `Btn_start_Click` 의 `seqHandler.GetSequenceState(seq.ID) == EContextState.Idle` rebuild 게이트.
3. `Btn_batchRun_Click` 내부의 `GetSequenceState(seqID) == EContextState.Idle` lazy-rebuild 게이트 — 위 치환된 전역 게이트가 앞에서 먼저 return 시키므로 크로스-시퀀스 상황에서 도달 자체가 불가능하다. 그대로 둔다.
4. `SequenceHandler.TryGetBlockingSequence` / `FindBlockingSequenceName` / `SharesCameraDevice` / `TryCollectSequenceCameras` — `SequenceHandler.cs` 는 이번 변경에서 열지도 수정하지도 않는다.
5. `_batchService` / `_batchShots` / `_batchAccumulated` 의 시퀀스별 분리(근본 수정) — 이번 범위 밖.
6. 파일 내 다른 `IsIdle == false` 4곳(980/985/1000/1155 부근) — 무수정.

**코딩 컨벤션 (필수):**
- 삼항 연산자 `?:` 금지 — `if`/`else` 사용. (이번 치환 코드에는 삼항이 없어야 함)
- 헝가리언 표기(신규 지역 변수 도입 시). 단 이번 작업은 신규 변수를 도입하지 않는다.
- C# 7.2 문법만 사용(switch expression / nullable reference types 금지).
- 파일 기존 스타일(K&R, 여는 중괄호 같은 줄) 유지.
- 회귀 0: 이 파일에서 위 블록 외 어떤 줄도 수정하지 않는다.
  </action>

  <verify>
    <automated>
cd "C:/Info/Project/DataMeasurement" && F=WPF_Example/UI/ControlItem/InspectionListView.xaml.cs && echo "A_TryGetBlockingSequence=$(grep -c 'TryGetBlockingSequence' $F) EXPECT_1" && echo "B_globalIsIdleGate=$(grep -c '(!SystemHandler.Handle.Sequences.IsIdle)' $F) EXPECT_1" && echo "C_batchMsgRestored=$(grep -c '시퀀스가 이미 실행 중입니다' $F) EXPECT_1" && echo "D_batchRebuildGate=$(grep -c 'GetSequenceState(seqID) == EContextState.Idle' $F) EXPECT_1" && echo "E_startRebuildGate=$(grep -c 'GetSequenceState(seq.ID) == EContextState.Idle' $F) EXPECT_1" && echo "F_orphanBlockingVar=$(grep -c 'sBlockingSeqName' $F) EXPECT_3"
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && git diff --stat -- WPF_Example/UI/ControlItem/InspectionListView.xaml.cs && echo "--- 위 변경은 파일 1개여야 하고, git diff 는 아래 블록 1곳만 보여야 한다 ---" && git diff -U0 -- WPF_Example/UI/ControlItem/InspectionListView.xaml.cs | grep -c '^@@' && echo "HUNK_COUNT_ABOVE_MUST_BE_1"
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "CS_LIST_ABOVE_MUST_BE_EMPTY"
    </automated>
  </verify>

  <done>
- `Btn_batchRun_Click` 이 `if (!SystemHandler.Handle.Sequences.IsIdle)` 전역 게이트로 차단하고, 메시지가 `"시퀀스가 이미 실행 중입니다."` 다.
- 파일 내 `TryGetBlockingSequence` 호출이 정확히 1개 남았다(= `Btn_start_Click` 의 것).
- 파일 내 `sBlockingSeqName` 등장이 정확히 3개다(= `Btn_start_Click` 의 선언 1 + out 인자 1 + string.Format 인자 1). 4개 이상이면 `Btn_batchRun_Click` 잔재가 남은 것.
- `Btn_batchRun_Click` 의 lazy-rebuild 게이트(`GetSequenceState(seqID) == EContextState.Idle`)와 `Btn_start_Click` 의 게이트(`GetSequenceState(seq.ID) == EContextState.Idle`)가 각각 1개씩 그대로 있다.
- `git diff` hunk 가 1개이고 변경 파일이 `InspectionListView.xaml.cs` 단 1개다.
- msbuild Debug/x64 빌드에서 `error CS` 0건, 신규 `warning CS` 0건.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| UI 이벤트 스레드 → 시퀀스 워커 스레드 | 사용자 클릭이 `_batchService` / `_batchShots` / `_batchAccumulated` (단일 공용 필드) 를 쓰고, 실행 중인 시퀀스 워커 스레드가 같은 참조를 읽는다. 네트워크/외부 입력 경계는 없음(로컬 UI 전용). |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mze-01 | Tampering (공유 가변 상태 덮어쓰기) | `InspectionListView.Btn_batchRun_Click` → `_batchService` / `_batchShots` / `_batchAccumulated` | mitigate | 크로스-시퀀스 일괄검사 진입 자체를 전역 `Sequences.IsIdle` 게이트로 차단해, 실행 중인 `BatchRunService` 참조가 새 인스턴스로 교체되는 경로를 제거한다(본 플랜 Task 1). |
| T-mze-02 | Denial of Service (프로세스 크래시) | 일괄검사 실행 경로 전체 | mitigate | T-mze-01 과 동일 게이트. 사용자 실기 재현 시나리오(BOTTOM 일괄검사 중 TOP 일괄검사 시작)가 차단 메시지로 귀결되어야 함 — `<verification>` 의 UAT 1 로 확인. |
| T-mze-03 | Tampering (TOCTOU 잔여 창) | `IsIdle` 확인 시점 ~ `_batchService` 대입 시점 사이 | accept | 단일 사용자 로컬 데스크톱 앱이고 두 클릭 모두 동일 WPF UI 스레드에서 순차 처리되므로, 두 클릭 사이에 다른 스레드가 이 필드를 대입할 경로가 없다. 락 도입은 근본 수정(필드 시퀀스별 분리)과 함께 별도 작업으로 남긴다. |
| T-mze-04 | Elevation of Privilege | — | n/a | 권한 경계 변화 없음. 게이트를 **더 엄격하게** 되돌리는 변경이므로 접근 확대 방향의 위험이 없다. |
</threat_model>

<verification>
## 자동 검증 (executor 수행)
Task 1 의 `<automated>` 3개 전부 통과.

## 사람 UAT (executor 범위 밖 — 사용자 실기 확인)
1. **크래시 방지 (핵심):** BOTTOM SHOT 체크 → 일괄검사 시작 → 실행 중에 TOP SHOT 체크 → 일괄검사 클릭 → `"시퀀스가 이미 실행 중입니다."` 메시지가 뜨고 **앱이 살아 있어야** 한다(이전엔 크래시).
2. **정상 경로 무회귀:** 아무 것도 실행 중이 아닐 때 일괄검사 → 기존과 동일하게 진행/완료/Export 동작.
3. **Phase 69 무회귀(단일 RUN):** 물리 카메라가 다른 두 시퀀스에서 단일 RUN 을 동시에 → 여전히 둘 다 실행됨(Test 1 재확인).
</verification>

<success_criteria>
- `Btn_batchRun_Click` 의 차단 게이트가 Phase 69 이전 전역 `IsIdle` 원문으로 복원됨.
- `Btn_start_Click` 의 `TryGetBlockingSequence` 및 양쪽 lazy-rebuild 게이트는 Phase 69 상태 그대로 유지됨(무수정).
- 변경 파일 1개 / diff hunk 1개 / msbuild Debug/x64 error CS 0.
- 근본 수정(공용 필드 시퀀스별 분리)은 후속 작업으로 SUMMARY 에 carry-over 로 명시됨.
</success_criteria>

<output>
완료 후 `.planning/quick/260805-mze-btn-batchrun-click-isidle/260805-mze-SUMMARY.md` 를 생성한다.

SUMMARY 에 반드시 포함할 carry-over 항목:
> **CO-mze-01 (open):** `InspectionListView` 의 `_batchService` / `_batchShots` / `_batchAccumulated` 가 시퀀스별로 분리되지 않은 단일 공용 필드다. `Dictionary<ESequence, ...>` 등으로 재설계하면 크로스-시퀀스 일괄검사 동시 실행을 다시 허용할 수 있고, 그때 `Btn_batchRun_Click` 게이트를 `TryGetBlockingSequence` 로 되돌릴 수 있다. 그 전까지 전역 `IsIdle` 게이트는 의도된 제약이므로 제거 금지.
</output>
