---
phase: quick-260729-hwb
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: false
requirements: [HWB-A, HWB-B, HWB-C]
tags: [cross-z, prep, light, zindex, fai-measurement, bottom, protocol-v1]

must_haves:
  truths:
    - "SHOT_E5(ZIndex=23) 가 소유한 크로스-Z 측정(ZIndexA=23/ZIndexB=24)이 있을 때 $PREP z_index=24 가 차단되지 않고 ACK OK 로 통과한다"
    - "SHOT_E5 의 ZIndex 를 24 로 바꿔도 대칭으로 z=23 이 통과한다 — 즉 레시피 설정 변경 없이 코드만으로 두 z 모두 통과한다"
    - "z_index 가 어떤 shot 의 own ZIndex 와 정확히 일치하면 $PREP 조명은 종전과 100% 동일한 shot 을 고른다(기존 경로 무변경)"
    - "크로스-Z 로만 매칭될 때 어느 shot 의 조명이 적용됐는지 Trace 로그에 shot 이름과 z_index 가 남는다"
    - "크로스-Z 실행 tick 에서 화면/저장 결과 이미지가 그 tick 에 실제 측정에 쓰인 role 이미지와 같다"
    - "크로스-Z 캡처가 없는 tick 과 비-크로스-Z Shot 의 결과 이미지/캡처 소스는 종전과 완전히 동일하다"
    - "프로토콜 사이클(수동 Z트리거 포함)의 미완성 tick 에서 크로스-Z 측정이 PASS 로 표시되지 않고 CROSS-Z INCOMPLETE(미완료)로 표시된다"
    - "완성 tick(두 번째 z)에서는 두 측정이 실제 측정값과 정상 판정으로 덮어써진다"
    - "미완성 tick 에서 그 측정이 PLC 로 NG 보고되지 않는다 — 완성 index 게이트(AddFaiResult)로 애초에 보고 대상이 아니다"
    - "4d435d9/b79ed15 가 추가한 SkipReason.CROSS_Z_INCOMPLETE, MarkMeasurementCrossZIncomplete, JudgeText/Excel 라벨 분기가 삭제되지 않고 재사용된다"
    - "FindActionIndicesByZIndex 와 DoesShotOwnCrossZIndex 의 본문은 한 줄도 바뀌지 않는다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "FindShotByZIndex 에 크로스-Z 폴백 매칭 추가 ($PREP 조명 경로가 $TEST 라우팅과 동일 규칙 인지)"
      contains: "DoesShotOwnCrossZIndex"
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "크로스-Z role 이미지의 화면/저장 반영 + 프로토콜 사이클 미완성 표시"
      contains: "MarkMeasurementCrossZIncomplete"
  key_links:
    - from: "InspectionSequence.FindShotByZIndex"
      to: "InspectionSequence.DoesShotOwnCrossZIndex"
      via: "정확일치 실패 시 2차 패스로 크로스-Z 소유 shot 조회 (FindActionIndicesByZIndex 와 동일 헬퍼 재사용)"
      pattern: "DoesShotOwnCrossZIndex\\(shot, nZIndex\\)"
    - from: "SystemHandler.ProcessPrep → ApplyPrepToSequences"
      to: "InspectionSequence.ApplyShotLights"
      via: "ApplyShotLights 가 true 를 반환해야 PrepAck.IsOk=true → DebugManualZTrigger 가 $TEST 로 진행"
      pattern: "ApplyShotLights\\(nZIndex\\)"
    - from: "Action_FAIMeasurement.ProcessCrossZCaptureTick"
      to: "pMyContext.ResultHalconImage / AggregateFaiResult 의 캡처 소스"
      via: "캡처된 role 키로 InspectionSequence.TakeCrossZImageCopy 사본을 받아 표시/저장 소스로 사용"
      pattern: "TakeCrossZImageCopy\\("
    - from: "Action_FAIMeasurement EStep.Measure 크로스-Z !bCompleted 분기"
      to: "SkipReason.CROSS_Z_INCOMPLETE 라벨(ReviewMeasurementRow/ExcelExportService)"
      via: "프로토콜 사이클에서도 MarkMeasurementCrossZIncomplete 호출 → 라벨 인프라 재사용"
      pattern: "MarkMeasurementCrossZIncomplete\\("
---

<objective>
크로스-Z(cross-Z) 3중 결함을 한 번에 닫는다.

(A) `$PREP` 조명 경로의 `FindShotByZIndex` 가 크로스-Z 를 모른 채 정확일치만 하기 때문에, 크로스-Z 짝의 한쪽 z 가 트리거 단계에서 통째로 차단된다(사용자 실기: BOTTOM z=24 "트리거 실패" 모달). `$TEST` 라우팅용 `FindActionIndicesByZIndex` 는 이미 크로스-Z 를 인지하므로, **동일 헬퍼(`DoesShotOwnCrossZIndex`)를 재사용해 두 리졸버의 규칙을 일치**시킨다.

(B) 크로스-Z tick 에서 화면 표시/저장 이미지가 항상 정적 `ShotConfig.SimulImagePath` 라, 실제 측정에 쓰인 role 이미지와 다른 사진이 보인다(사용자 실기: z=23 인데 세로 사진). 이미 role 이미지를 알고 있는 캡처 경로의 결과물을 표시/저장 소스로도 쓴다.

(C) 프로토콜 사이클(수동 Z트리거 포함)의 첫 z tick 에서 짝이 아직 미완성인데 `faiAllPass` 가 기본값 `true` 로 남아 초록 PASS 로 보인다. `4d435d9` 가 만든 CROSS_Z_INCOMPLETE 인프라를 **프로토콜 사이클에도 적용**해 "PASS 도 NG 도 아닌 미완료"로 표시한다(나머지 절반 마감).

Purpose: 실제 검사 장비에서 "측정하지 않은 항목이 초록 PASS 로 보이는" 안전 결함 제거 + 크로스-Z 를 레시피 설정 변경 없이 동작시키기.
Output: 소스 2개 수정, Debug/x64 빌드 통과, 실기 human-verify 통과.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

수정 대상 파일:
@WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
@WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

읽기 전용 참조(수정 금지):
@WPF_Example/Custom/SystemHandler.cs
@WPF_Example/Custom/Sequence/Inspection/SkipReason.cs

<planner_findings>
플래너가 fresh Read 로 확인한 사실(실행자는 이 사실을 다시 조사하지 말고 라인번호만 재확인할 것):

1. `FindShotByZIndex` (InspectionSequence.cs L393-417): `shot.OwnerSequenceName == Name && shot.ZIndex == nZIndex` 정확일치 1패스, 첫 매칭 반환, 없으면 null. 호출자는 `ApplyShotLights(int)` (L504-516) 단 하나.
2. `ApplyShotLights` → false → `ApplyPrepToSequences` (SystemHandler.cs L864-884) 가 false → `ProcessPrep` (L788-821) 가 `ackPacket.IsOk=false` → `DebugManualZTrigger` (L830-860) 가 "PREP 실패 — TEST 진행하지 않음" 으로 조기 return. **이것이 z=24 차단의 정확한 인과 사슬**이다.
3. `DoesShotOwnCrossZIndex` (L422-454) 는 shot 소유 FAI 의 `DualImageEdgeDistanceMeasurement.ZIndexA/ZIndexB` 를 검사한다. `FindActionIndicesByZIndex` (L463-499) 가 `bOwnZIndexMatch || bCrossZMatch` 로 사용 중. 둘 다 무수정 대상.
4. 프로토콜 응답(V1)은 `AddResponseV1Cycle` → `BuildScopedResponse` → `AggregateIndexFais` → `AddFaiResult` 경로이고, `AddFaiResult` (L1382-1420) 는 측정마다 `GetMeasurementCompletionZIndex(meas, shot) == nZIndex` 게이트를 통과한 것만 패킷에 담는다. 크로스-Z 측정의 완성 index 는 `max(ZIndexA, ZIndexB)` = 24. **따라서 z=23 tick 에서 이 측정을 NG/미완료로 표시해도 PLC 로는 아무것도 나가지 않는다**(사이클 NG 누적 `m_bCycleHasNG` 도 `ClassifyMeasurement` 안에서만 일어나며 그 함수는 게이트 통과분만 본다). 이것이 (C) 설계의 안전 근거다.
5. `fai.IsPass` 는 V1 응답 경로에서 읽히지 않는다(구 v2.6 `AddResponse` 만 읽음). 화면/캡처 파일명(OK/NG)/cycle.json 표시용이다.
6. 매 z tick 은 별도 `Start` 이므로 `HandleRunStartResetResults` (L226-244) 가 모든 측정의 `ClearResult()` 를 돌린다 → z=23 에서 찍은 CROSS_Z_INCOMPLETE 는 z=24 시작 시 자동 소거되고, z=24 에서 정상 측정값으로 덮인다. stale 라벨 위험 없음.
7. 크로스-Z 이미지 저장소(`m_dicCrossZImages`)는 z=0 수신(`BeginCrossZImageCycle`) 에서만 비워진다. 수동 Z트리거는 z=0 을 보내지 않으므로 z=23 의 role A 가 z=24 까지 살아남는다 — 짝 완성이 성립하는 이유.
8. `LoadCrossZRoleImage` (L1091-1118) 는 **SIMUL_MODE 에서만** role 별 교시 경로(`TeachingImagePath_Horizontal`=roleA / `TeachingImagePath_Vertical`=roleB)를 쓰고, 비-SIMUL 에서는 `ShotParam.GetImage()` 를 그대로 반환한다. 즉 (B) 의 이미지 불일치는 SIMUL/교시경로 설정 시에만 발생하며, 실장비 라이브 경로에서는 수정 후에도 내용이 동일하다(회귀 위험 낮음).
9. `MainView.xaml.cs` L2184-2198 `UpdateImageSourceLabel` 은 **트리 노드 선택 시 티칭/리뷰 브라우징 라벨**이지 라이브 검사 결과 표시 경로가 아니다(인자가 선택된 `DatumConfig`/`ShotConfig`). Task 2 에서 이 판단을 재확인한 뒤 처리한다(아래 action 참조).
</planner_findings>
</context>

<tasks>

<task type="auto">
  <name>Task 1: (A) $PREP 조명 리졸버를 크로스-Z 인지로 통일 — FindShotByZIndex</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
`FindShotByZIndex(int nZIndex)` (현재 L393-417 부근, 반드시 fresh Read 로 위치 확인) 만 수정한다. `DoesShotOwnCrossZIndex` 와 `FindActionIndicesByZIndex` 는 **한 글자도 건드리지 않는다**.

수정 방식은 **2-패스 폴백**이다. 순서가 곧 결정론적 우선순위 규칙이다.

- 1패스: 기존 루프를 그대로 둔다 — `recipeManager.Shots` 순회, `shot.OwnerSequenceName == Name && shot.ZIndex == nZIndex` 인 첫 shot 을 반환. 즉 **own-ZIndex 정확일치가 존재하면 반환값은 수정 전과 100% 동일**하다(회귀 0 의 구조적 보장).
- 2패스: 1패스가 아무것도 못 찾은 경우에만, 같은 `recipeManager.Shots` 를 같은 순서로 다시 순회해 `shot.OwnerSequenceName == Name && DoesShotOwnCrossZIndex(shot, nZIndex)` 인 첫 shot 을 반환한다.
- 2패스에서 매칭됐을 때는 반환 직전에 `Logging.PrintLog((int)ELogType.LightController, ...)` 로 Trace 성격의 한 줄을 남긴다. 반드시 포함할 값: 고정 태그 `[PREP CrossZ]`, 선택된 `shot.ShotName`, `shot.ZIndex`, 요청된 `nZIndex`, 시퀀스 `Name`. 이 로그가 체크포인트에서 사용자가 눈으로 확인할 증거다.
- 둘 다 실패하면 기존대로 `null` 반환(호출부 `ApplyShotLights` 의 "Shot not found" Error 로그 경로 유지 — 진짜 미설정 z 는 여전히 차단되어야 한다).

**다중 매칭 결정론 규칙(명시적 설계 결정, 주석으로 남길 것):** 같은 z 를 두 개 이상의 shot 이 크로스-Z 로 소유하는 구성이 레시피상 가능하다. 이때는 `recipeManager.Shots` 열거 순서의 첫 번째가 이긴다. 근거: 같은 순서를 `FindActionIndicesByZIndex` / `AggregateIndexFais` 가 이미 쓰고 있어 실행·집계·조명이 동일 순서를 공유하게 되고, 새 정렬 기준을 발명하지 않는다. own-ZIndex 정확일치를 항상 먼저 보는 이유는 기존 동작 보존이다.

**조명 의미론(명시적 설계 결정, 주석으로 남길 것):** 크로스-Z 로 매칭된 경우 **그 크로스-Z 측정을 소유한 shot 자신의 조명 설정**을 적용한다(`ApplyShotLightsInternal(shot)` 은 무수정 — 반환된 shot 이 달라질 뿐). role(A/B) 별로 다른 조명을 주는 기능은 현재 코드에 존재하지 않으며 이번 범위 밖이다. 이 한계는 SUMMARY 에 기록한다.

C# 7.2 문법만 사용하고 이 파일의 기존 스타일(Allman + `bool bXxx` 중간변수 + 삼항 금지)을 그대로 따른다. 주석에 `260729 hbk quick-fix(260729-hwb)` 식별자를 넣는다. 새 파일/새 클래스/새 필드를 만들지 않는다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && F=WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs && CF=$(grep -v '^[[:space:]]*//' "$F") && PC8='[$]"|=>|switch[[:space:]]*[{]'; echo "--- 바뀌어야 하는 값 ---"; echo "dosown_refs=$(echo "$CF" | grep -cF 'DoesShotOwnCrossZIndex(') (want 3, was 2)"; echo "prep_crossz_log=$(echo "$CF" | grep -cF '[PREP CrossZ]') (want 1, was 0)"; echo "--- 무변경이어야 하는 값 ---"; echo "dosown_def=$(echo "$CF" | grep -cF 'private bool DoesShotOwnCrossZIndex(ShotConfig shot, int nZIndex)') (want 1)"; echo "findactidx_def=$(echo "$CF" | grep -cF 'public List<int> FindActionIndicesByZIndex(int nZIndex)') (want 1)"; echo "findshot_def=$(echo "$CF" | grep -cF 'private ShotConfig FindShotByZIndex(int nZIndex)') (want 1)"; echo "applylights_def=$(echo "$CF" | grep -cF 'public bool ApplyShotLights(int nZIndex)') (want 1)"; echo "no_csharp8_added=$(git diff -U0 -- "$F" | grep '^+' | grep -cE "$PC8") (want 0)"; echo "--- scope: 아래에 InspectionSequence.cs 외 소스파일이 뜨면 FAIL (.planning/ 은 무시) ---"; git diff --name-only; echo "--- build: 'error CS' / 신규 'warning CS' 줄이 하나라도 있으면 FAIL. MSB3021/3026/3027(실행중 exe 파일잠금)은 컴파일 실패가 아니다 ---"; "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "CS_LIST_ABOVE_MUST_BE_EMPTY"</automated>
  </verify>
  <done>
- 위 게이트의 모든 `(want N)` 이 실제 출력과 일치한다. 플래너가 수정 전 상태에서 드라이런한 실측값: `dosown_refs=2` `dosown_def=1` `findactidx_def=1` `findshot_def=1` → `dosown_refs` 만 3 으로 늘고 나머지는 그대로여야 한다.
- `git diff --name-only` 의 소스파일이 `InspectionSequence.cs` 하나뿐이다(Task 2 전).
- `CS_LIST_ABOVE_MUST_BE_EMPTY` 위에 `error CS` / 신규 `warning CS` 줄이 없다.
- 읽기 재확인: 중괄호 균형이 맞고, 1패스 루프의 조건식이 수정 전과 동일하며, 2패스가 1패스 실패 뒤에만 실행된다.
  </done>
</task>

<task type="auto">
  <name>Task 2: (B) 크로스-Z role 이미지 표시/저장 + (C) 프로토콜 사이클 미완료 표시</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
한 파일 안의 두 수정이다. `4d435d9`/`b79ed15` 가 추가한 것은 **삭제·되돌리기 금지, 확장만** 한다.

**(C) 프로토콜 사이클 미완료 표시 — 먼저 한다(작고 위험이 낮음).**

1. `MarkMeasurementCrossZIncomplete(MeasurementBase meas, bool bRelevantTick)` (현재 L926 부근) 에 세 번째 파라미터 `bool bProtocolCycle` 을 추가한다. 상태 마킹부(`ClearResult()` / `LastSkipReason = SkipReason.CROSS_Z_INCOMPLETE` / `LastJudgement = false`)는 **그대로 둔다** — 바뀌는 것은 로그 문구뿐이다.
   - `bProtocolCycle == false`: 기존 e9q 문구를 한 글자도 바꾸지 않고 그대로 출력한다(비프로토콜 실행 안내).
   - `bProtocolCycle == true`: 별도 문구를 출력한다. 반드시 포함할 값: 고정 태그 `[FAIMeasurement]`, 측정명, `ZIndexA`, `ZIndexB`, 현재 tick 의 z(`parentSeq2.GetExecutionZIndex()` 값 — 필요하면 인자로 받아 넘긴다), 그리고 "짝이 되는 나머지 z 트리거 대기 중 — 아직 측정되지 않음(PASS 아님)" 취지의 설명. 로그 레벨은 기존과 동일하게 `ELogType.Error` 를 쓰되, 프로토콜 정상 흐름의 중간 상태이므로 문구에 "정상 흐름의 중간 상태" 임을 명시해 운영자가 고장으로 오인하지 않게 한다.
2. 기존 호출부 2곳(`!bRelevant` 분기, `!bCompleted` 분기)에 `false` 를 명시 전달해 e9q 동작을 보존한다.
3. `!bCompleted` 분기(현재 L361-370)에 프로토콜 경로를 추가한다: `bNonProtocolCycle` 이 false(=프로토콜 사이클)일 때도 `MarkMeasurementCrossZIncomplete(meas, true, true)` 를 호출하고 `faiAllPass = false` 로 둔다. `measuredCount++` 와 `continue` 는 유지한다.
4. `!bRelevant` 분기는 **프로토콜 경로에서 종전 그대로 무변경**(조용히 continue)이다. 이 tick 은 이 측정과 애초에 무관한 안전망 분기이므로 건드리면 다른 z 의 정상 결과를 훼손할 수 있다.

설계 근거(주석과 SUMMARY 에 남길 것): 미완성 tick 에서 `faiAllPass=false` 로 두는 것이 PLC 응답을 오염시키지 않는 이유는 `AddFaiResult` 의 `GetMeasurementCompletionZIndex(meas, shot) == nZIndex` 게이트가 이 측정을 미완성 index 에서 애초에 보고 대상에서 제외하기 때문이다(V1 응답은 `fai.IsPass` 를 읽지 않는다). 즉 이 변경의 영향 범위는 화면 표시 / 캡처 파일명 OK·NG / cycle.json 뿐이며, 방향은 "측정 안 한 것을 PASS 라 하지 않는다"는 안전측이다. 구 v2.6 응답 경로는 z_index 스코프 자체가 없어 크로스-Z 를 지원하지 않는 구성이며, 그 경우에도 거짓 PASS 보다 미완료 표시가 안전측이다.

**(B) 크로스-Z role 이미지를 화면/저장에 반영.**

5. `ProcessCrossZCaptureTick(...)` (현재 L1126 부근) 에 `out string szCapturedRoleKey` 를 추가한다. 캡처 성공 시(`bCaptureOk = true` 직전/직후) 그 tick 의 `roleKey` 를 넣고, 그 외 모든 조기 return 경로에서는 `null` 로 초기화된 채 나가게 한다. 기존 3개 out 파라미터의 의미와 `StoreCrossZImage` 호출은 그대로 둔다.
6. `EStep.Measure` 의 FAI 루프 안에 per-FAI 지역변수 `HImage crossZRoleImage = null;` 을 둔다(새 필드 금지 — 지역변수만). 크로스-Z 게이트에서 `bCaptureOk == true` 이고 `crossZRoleImage == null` 이면 `parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey)` 로 **소유 사본**을 받아 보관한다(같은 FAI 안에서 첫 캡처가 이긴다 — 결정론적 규칙).
7. 그 FAI 의 `AggregateFaiResult(fai, ...)` 호출 지점에서:
   - `crossZRoleImage == null` 이면 **종전과 완전히 동일하게** `sharedSrc` 를 넘긴다(비-크로스-Z 회귀 0).
   - `crossZRoleImage != null` 이면 `crossZRoleImage.CopyImage()` 로 만든 `SharedHImage` 를 대신 넘기고, 호출 직후 `finally` 에서 그 `SharedHImage.Release()` 를 호출한다(파일 L284-285 / L407-409 의 기존 `sharedSrc` 소유권 계약을 그대로 미러링 — 워커의 `AddRef` 와 독립).
   - Shot 단위로 **아직 표시 이미지를 덮지 않았다면**(shot 루프 지역 플래그/참조 1개로 판정) `pMyContext.ResultHalconImage` 를 dispose 하고 `crossZRoleImage.CopyImage()` 로 교체한다. Shot 전체에서 첫 크로스-Z 캡처가 화면을 차지한다(결정론적 규칙).
   - 그 FAI 처리가 끝나면 `crossZRoleImage` 를 dispose 하고 null 로 되돌린다. 예외가 나도 누수되지 않도록 기존 `try/finally` 구조 안에 배치한다.
   - 표시 이미지를 교체할 때 `Logging.PrintLog((int)ELogType.Trace, ...)` 로 한 줄 남긴다. 반드시 포함할 값: 고정 태그 `[FAI CrossZ IMG]`, Shot 이름, 측정명, role(A/B), 현재 z. 체크포인트 증거로 쓴다.
8. `LoadCrossZRoleImage` / `TryExecuteCrossZMeasurement` / `StoreCrossZImage` / `TakeCrossZImageCopy` 의 본문 로직은 수정하지 않는다.

**MainView 라벨 판단(명시적 결정 지점):** `WPF_Example/UI/ContentItem/MainView.xaml.cs` 의 `UpdateImageSourceLabel` (L2184-2198) 를 fresh Read 로 확인한다. 플래너 판단으로는 이것은 **트리 노드 선택 기반 티칭/리뷰 브라우징 라벨**이지 라이브 검사 결과 표시 경로가 아니다. 확인 결과가 그렇다면 **MainView.xaml.cs 는 수정하지 않고**, 그 근거(호출부와 인자 출처)를 SUMMARY 에 기록한다. 만약 실제로 라이브 검사 결과 이미지 라벨로 쓰이는 것이 확인되면, 그때만 크로스-Z role 경로를 표시하도록 확장하고 그 사실을 SUMMARY 에 남긴다. 어느 쪽이든 판단 근거 없이 조용히 넘어가지 않는다.

C# 7.2 문법만 사용한다. 이 파일은 K&R 과 Allman 이 구역별로 섞여 있으므로 **편집하는 각 구역의 기존 스타일을 그대로 따른다**. 주석에 `260729 hbk quick-fix(260729-hwb)` 식별자를 넣는다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && A=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && S=WPF_Example/Custom/Sequence/Inspection/SkipReason.cs && R=WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs && X=WPF_Example/Custom/Export/ExcelExportService.cs && CA=$(grep -v '^[[:space:]]*//' "$A") && PC8='[$]"|=>|switch[[:space:]]*[{]'; echo "--- 바뀌어야 하는 값 ---"; echo "markincomplete_calls=$(echo "$CA" | grep -cF 'MarkMeasurementCrossZIncomplete(meas') (want 3, was 2)"; echo "roleimg_log=$(echo "$CA" | grep -cF '[FAI CrossZ IMG]') (want 1, was 0)"; echo "takecopy_refs=$(echo "$CA" | grep -cF 'TakeCrossZImageCopy(') (want 5, was 4)"; echo "--- e9q/b79ed15 산출물 보존 (되돌리면 FAIL) ---"; echo "skipreason_const=$(grep -cF 'CROSS_Z_INCOMPLETE = \"CROSS_Z_INCOMPLETE\"' "$S") (want 1)"; echo "markincomplete_def=$(echo "$CA" | grep -cF 'private void MarkMeasurementCrossZIncomplete(') (want 1)"; echo "e9q_nonprotocol_msg=$(echo "$CA" | grep -cF '비프로토콜 실행(RUN 버튼/일괄검사)') (want 1)"; echo "review_label=$(grep -cF 'CROSS-Z INCOMPLETE' "$R") (want 1)"; echo "excel_label=$(grep -cF 'CROSS-Z INCOMPLETE' "$X") (want 1)"; echo "--- 무변경이어야 하는 값 ---"; echo "loadrole_def=$(echo "$CA" | grep -cF 'private HImage LoadCrossZRoleImage(bool bIsRoleA, DualImageEdgeDistanceMeasurement dualMeas)') (want 1)"; echo "proctick_def=$(echo "$CA" | grep -cF 'private void ProcessCrossZCaptureTick(') (want 1)"; echo "aggregate_def=$(echo "$CA" | grep -cF 'private void AggregateFaiResult(FAIConfig fai') (want 1)"; echo "nonprotocol_gate=$(echo "$CA" | grep -cF 'bool bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();') (want 1)"; echo "no_csharp8_added=$(git diff -U0 -- "$A" | grep '^+' | grep -cE "$PC8") (want 0)"; echo "--- scope: 소스파일은 InspectionSequence.cs + Action_FAIMeasurement.cs 둘뿐이어야 한다 (MainView.xaml.cs 는 위 action 의 판단 결과로 추가될 수 있으며 그 경우 SUMMARY 에 근거 필수) ---"; git diff --name-only; echo "--- build ---"; "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "CS_LIST_ABOVE_MUST_BE_EMPTY"</automated>
  </verify>
  <done>
- 위 게이트의 모든 `(want N)` 이 실제 출력과 일치한다. 플래너 드라이런 실측: `markincomplete_calls=2` `takecopy_refs=4` `markincomplete_def=1` `proctick_def=1` `aggregate_def=1` → 늘어야 하는 건 `markincomplete_calls`(3), `takecopy_refs`(5), `roleimg_log`(1) 셋뿐이다.
- e9q/b79ed15 보존 항목 5개(`skipreason_const`, `markincomplete_def`, `e9q_nonprotocol_msg`, `review_label`, `excel_label`)가 전부 1 이다. 하나라도 0 이면 직전 작업을 훼손한 것이므로 즉시 복구한다.
- `CS_LIST_ABOVE_MUST_BE_EMPTY` 위에 `error CS` / 신규 `warning CS` 줄이 없다.
- 읽기 재확인: `crossZRoleImage` 와 새 `SharedHImage` 가 모든 경로에서 정확히 한 번씩 dispose/Release 되고(`try/finally` 안), `crossZRoleImage == null` 인 경우의 `AggregateFaiResult` 인자가 수정 전과 동일하게 `sharedSrc` 다.
- **exe 파일잠금 주의(이 프로젝트에서 반복 발생):** `DatumMeasurement.exe` 가 실행 중이면 MSBuild 는 컴파일에 성공하고 `obj → bin` 복사에서 MSB3026/MSB3027/MSB3021 로 끝난다. 컴파일 실패는 아니지만 `bin/x64/Debug/DatumMeasurement.exe` 가 갱신되지 않는다. Task 3 로 넘기기 전에 실행 중인 DatumMeasurement.exe(및 Visual Studio 디버그 세션)를 닫고 빌드를 한 번 더 돌려 복사까지 성공시키고, 그 사실을 사용자에게 알린다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 재검증 — 두 Z 모두 통과 + 올바른 사진 + 가짜 PASS 제거 + 회귀 없음 (checkpoint)</name>
  <files>(코드 수정 없음 — 실행 중인 앱에서 육안/로그 확인만 수행)</files>
  <action>Task 2 빌드 산출물(Debug/x64 `DatumMeasurement.exe`)로 사용자가 아래 how-to-verify 를 수행하고 결과를 보고한다. 실행자는 사용자 응답 전까지 추가 코드 수정을 하지 않는다. 실패 항목이 보고되면 사용자가 붙여넣은 로그(`[PREP CrossZ]` / `[FAI CrossZ IMG]` / `CROSS_Z_INCOMPLETE`)를 근거로 원인을 분석해 보고하고, 새 범위 수정 전에 승인을 받는다.</action>
  <what-built>
카메라 아래에서 부품 높이(Z)를 두 번 바꿔 찍은 **사진 두 장을 짝지어 재는 측정**(크로스-Z)에서 세 가지가 잘못돼 있었습니다.

1. **한쪽 높이가 아예 막혀 있었습니다.** 프로그램 안에 "이 높이는 어느 촬영 항목 것인가"를 판단하는 곳이 두 군데 있는데, 한 곳만 "두 장 짝짓기"를 알고 다른 한 곳(조명을 켜는 준비 단계)은 몰랐습니다. 준비 단계가 먼저 실행되니, 모르는 쪽이 먼저 "그런 높이 없음"이라고 막아버려서 아는 쪽까지 가지도 못했습니다. 이제 두 곳이 같은 기준으로 판단합니다.

2. **화면에 엉뚱한 사진이 떴습니다.** 실제 계산은 올바른 사진으로 하고 있었는데, 화면에 보여주고 저장하는 사진만 항상 그 촬영 항목의 대표 사진 한 장을 쓰고 있었습니다. 이제 그 순간 실제로 잰 사진이 그대로 화면에 뜨고 저장됩니다.

3. **아직 재지도 않았는데 초록 합격으로 보였습니다.** 두 장이 다 모여야 계산이 되는데, 첫 번째 높이에서는 아직 한 장뿐이라 계산을 안 합니다. 그런데 화면에는 기본값인 합격(초록)이 떠 있었습니다. 이제 이 상태는 합격도 불합격도 아닌 **"CROSS-Z INCOMPLETE"(아직 못 잼)** 로 표시됩니다. 두 번째 높이까지 오면 실제 측정값과 정상 판정으로 바뀝니다.

**중요:** 이번 수정의 핵심은 **레시피 설정을 하나도 바꾸지 않고** 두 높이가 다 동작해야 한다는 것입니다. 그래서 아래 테스트에서 SHOT_E5 의 ZIndex 는 **23 그대로 두고** 진행해 주세요.
  </what-built>
  <how-to-verify>
아래 순서대로 확인해 주세요.

0. **제일 중요 — 새 프로그램으로 테스트하는지 확인.**
   지금 켜져 있는 DatumMeasurement 를 **완전히 닫고**(Visual Studio 로 디버깅 중이면 그것도 정지), 새로 빌드된 것을 다시 실행해 주세요. 안 닫으면 파일이 잠겨서 예전 프로그램이 그대로 켜지고, 고친 게 하나도 반영되지 않은 상태로 테스트하게 됩니다.

1. **설정은 그대로 두는지 확인 (바꾸지 마세요).**
   - 레시피 `FAI_1`, BOTTOM 의 `SHOT_E5` 를 엽니다.
   - `SHOT_E5` 자신의 **ZIndex 는 23 그대로** 둡니다. (예전에 24 로 바꿔서 테스트하셨다면 **23 으로 되돌려** 주세요.)
   - 측정 `E5_P1`, `E5_P2` 의 **ZIndexA=23 / ZIndexB=24** 가 그대로인지만 확인합니다.

2. **첫 번째 높이 — 수동 Z트리거 z_index = 23.**
   - 시퀀스 `BOTTOM`, z_index `23` 으로 수동 Z트리거를 실행합니다.
   - **기대 1**: 트리거가 성공한다(예전과 동일).
   - **기대 2**: 화면에 뜨는 사진이 **그 높이에서 실제로 잰 사진**이다. 예전처럼 엉뚱한(세로) 사진이면 실패입니다.
   - **기대 3**: `E5_P1`, `E5_P2` 의 판정이 **초록 PASS 가 아니라 `CROSS-Z INCOMPLETE`(아직 못 잼)** 으로 뜬다. 이게 이번 3번 수정의 핵심 증거입니다.
   - 로그에서 `[FAI CrossZ IMG]` 로 시작하는 줄을 찾아 주세요. Shot 이름 `SHOT_E5`, 측정 이름, role, z=23 이 찍혀 있어야 합니다.

3. **두 번째 높이 — 이어서 수동 Z트리거 z_index = 24.**
   - **기대 1**: **"트리거 실패 / 시퀀스: BOTTOM / z_index: 24" 모달이 더 이상 뜨지 않는다.** 이게 1번 수정의 핵심 증거입니다.
   - **기대 2**: 로그에 `[PREP CrossZ]` 로 시작하는 줄이 있고, 거기에 `SHOT_E5` 와 z=24 가 찍혀 있다(어느 촬영 항목의 조명을 썼는지 표시).
   - **기대 3**: `E5_P1`, `E5_P2` 에 **실제 측정값(mm)** 과 정상 판정(OK 또는 NG)이 뜬다. 더 이상 `CROSS-Z INCOMPLETE` 가 아니어야 합니다.
   - **기대 4**: 화면 사진이 그 높이에서 실제로 잰 사진이다.

4. **회귀 확인 1 — 짝짓기를 안 쓰는 보통 촬영 항목.**
   - 크로스-Z 를 쓰지 않는 BOTTOM 촬영 항목(예: `SHOT_E1`~`SHOT_E4` 또는 `SHOT_B1`~`SHOT_B4`) 중 하나를 골라, 그 항목의 ZIndex 로 수동 Z트리거를 실행합니다.
   - **기대**: 예전과 **똑같이** 동작한다. 사진도 판정도 예전 그대로여야 하고, `CROSS-Z INCOMPLETE` 같은 새 표시가 뜨면 안 됩니다.

5. **회귀 확인 2 — RUN 버튼 일반 검사.**
   - RUN 버튼으로 평소처럼 검사 1회를 돌립니다.
   - **기대 1**: 예전 동작 그대로다.
   - **기대 2**: `E5_P1`/`E5_P2` 같은 크로스-Z 측정은 (RUN 버튼으로는 두 높이를 못 도니까) 오늘 오전 수정대로 여전히 `CROSS-Z INCOMPLETE` 로 표시된다. 초록 PASS 로 뜨면 실패입니다.

문제가 있으면 `[PREP CrossZ]` / `[FAI CrossZ IMG]` / `CROSS_Z_INCOMPLETE` 가 들어간 로그 줄을 그대로 복사해서 알려 주세요. "트리거 실패" 모달이 뜨면 그 모달의 문구도 함께 알려 주세요.
  </how-to-verify>
  <resume-signal>"승인" 이라고 적어 주시거나, 실패한 항목 번호와 로그를 알려 주세요.</resume-signal>
  <verify>
    <human-check>0번(새 빌드 실행) 전제 하에 2번(z=23: 트리거 성공 + 올바른 사진 + CROSS-Z INCOMPLETE 표시) + 3번(z=24: 트리거 실패 모달 없음 + `[PREP CrossZ]` 로그 + 실제 측정값/정상 판정) + 4번(비-크로스-Z Shot 무변경) + 5번(RUN 버튼 무변경 + e9q 동작 유지) 네 가지가 모두 통과</human-check>
    <automated>MISSING — 이 프로젝트는 테스트 프레임워크가 없고, 이 항목은 실제 장비/레시피/조명 상태에서만 관측 가능하므로 자동화 불가. 구조·빌드 검증은 Task 1/2 게이트가 전담한다.</automated>
  </verify>
  <done>사용자가 2·3·4·5번 전부 통과를 확인했다. 실패 항목이 있으면 그 로그를 근거로 원인을 보고하고 승인 후에만 추가 수정한다.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| PLC/핸들러 → TCP `$PREP`/`$TEST` | 외부 장비가 보낸 z_index 가 조명 적용/실행 스코프를 결정 |
| 레시피 INI(`main.ini`) → 런타임 ShotConfig/Measurement | 운영자가 편집하는 ZIndex/ZIndexA/ZIndexB 가 매칭 로직 입력 |
| 검사 결과 → PLC 응답(TestResultPacket) | 판정이 실제 생산 합불에 직결 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-HWB-01 | Tampering(판정 위조) | `EStep.Measure` 크로스-Z 미완성 tick | mitigate | Task 2 (C): 미완성 측정을 `CROSS_Z_INCOMPLETE` + `faiAllPass=false` 로 명시 표시 — 미측정 항목의 거짓 PASS 제거 |
| T-HWB-02 | Information Disclosure(오표시) | 결과 화면/저장 이미지 | mitigate | Task 2 (B): 표시/저장 소스를 실제 측정에 쓰인 role 이미지로 교체 + `[FAI CrossZ IMG]` 로그로 추적 가능 |
| T-HWB-03 | Denial of Service(정상 트리거 차단) | `$PREP` → `FindShotByZIndex` | mitigate | Task 1 (A): 크로스-Z 소유 z 를 조명 단계에서 차단하지 않도록 2패스 폴백 |
| T-HWB-04 | Repudiation(어느 shot 조명인지 불명) | 크로스-Z 다중 매칭 | mitigate | Task 1: 폴백 매칭 시 `[PREP CrossZ]` 로 shot/z 기록, 선택 규칙은 `Shots` 열거 순서로 결정론적 고정 |
| T-HWB-05 | Tampering(잘못된 NG 를 PLC 로 송출) | 미완성 index 응답 | accept | `AddFaiResult` 의 완성 index 게이트가 미완성 index 에서 이 측정을 보고 대상에서 제외 — 코드 경로로 확인됨(planner_findings 4). 신규 코드 추가 없음 |
| T-HWB-SC | Tampering | npm/pip/cargo installs | n/a | 이번 작업은 패키지 설치가 없다(기존 소스 2파일 편집만) |
</threat_model>

<verification>
- Task 1/2 의 automated 게이트가 전부 통과(빌드 `error CS` 0, 불변 항목 카운트 유지).
- `git diff --name-only` 의 소스파일이 계획된 2개(예외적으로 MainView 판단 결과 3개, 근거 SUMMARY 필수)를 넘지 않는다.
- Task 3 human-verify 4항목 전부 통과.
</verification>

<success_criteria>
- 레시피를 **전혀 바꾸지 않고** BOTTOM z=23 / z=24 수동 Z트리거가 모두 성공한다.
- z=23 tick 에서 `E5_P1`/`E5_P2` 가 초록 PASS 가 아니라 `CROSS-Z INCOMPLETE` 로 표시된다.
- z=24 tick 에서 두 측정에 실제 측정값과 정상 판정이 표시된다.
- 크로스-Z tick 의 화면/저장 이미지가 실제 측정에 쓰인 role 이미지다.
- 비-크로스-Z Shot / RUN 버튼 경로 동작이 종전과 동일하다(4d435d9 의 RUN 경로 NG 처리 포함).
</success_criteria>

<output>
Create `.planning/quick/260729-hwb-z-3-prep-findshotbyzindex-cross-z-cross-/260729-hwb-SUMMARY.md` when done.

SUMMARY 에 반드시 기록할 것:
- 조명 의미론 결정(크로스-Z 매칭 시 소유 shot 자신의 조명 적용)과 그 한계(role 별 조명 미지원 — 범위 밖).
- 다중 매칭 결정론 규칙(own-ZIndex 정확일치 우선 → `Shots` 열거 순서 첫 번째) 과 근거.
- (C) 미완성 tick 을 NG 가 아니라 미완료로 다루는 설계 근거 + PLC 응답이 오염되지 않는 코드 경로 근거(완성 index 게이트).
- MainView `UpdateImageSourceLabel` 을 수정했는지/안 했는지와 그 판단 근거.
- (B) 는 SIMUL_MODE(role 별 교시 경로 설정 시)에서만 실제로 이미지가 달라진다는 사실.
</output>
</content>
</invoke>
