# Phase 5: 검사 시퀀스 & TCP - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Phase Boundary

단일 TCP 검사 요청으로 전체 Shot을 순회하며 카메라 Grab과 FAI 에지 측정을 자동 실행하고, 종합 판정 결과(FAI별 개별 측정값 포함)를 TCP 패킷으로 호스트에 전송하는 자동 검사 파이프라인을 완성한다.

Z축 이동 인터페이스를 정의하고, SIMUL_MODE에서 Shot별 다른 이미지를 로드하여 E2E 테스트가 가능하도록 한다. UI는 Shot별 실시간 결과 갱신을 지원한다.

</domain>

<decisions>
## Implementation Decisions

### Shot 순회 실행 전략
- **D-01:** `SequenceBase`에 `StartAll()` 메서드를 추가한다. `EndActionIndex = Actions.Length - 1`로 설정하여 등록된 모든 Action을 순차 실행한다. 기존 `Start()`는 단일 Action 실행 유지.
- **D-02:** `ExecuteAction()`에서 `CurrentActionIndex < EndActionIndex` 일 때 `CurrentActionIndex++`로 다음 Action 진행. `CurrentActionIndex >= EndActionIndex`에서 `Finish()` 호출하는 기존 패턴 유지.
- **D-03:** `ProcessTest()`에서 `SequenceHandler.IsDynamicFAIMode` 체크 → true이면 `seq.StartAll()`, false이면 기존 `seq.Start(packet)` 호출. 변경 지점은 `Custom/SystemHandler.cs` 1곳만.

### TCP 응답 포맷
- **D-04:** FAI별 개별 결과를 동적 리스트로 전송한다. 기존 `TestResultPacket`에 FAI 결과 리스트를 추가.
- **D-05:** 패킷 프로토콜: `RESULT,Site,Type,TotalResult,FAICount,[FAI1_Result,FAI1_DistMm,...,FAIN_Result,FAIN_DistMm]` 형태. FAI 개수를 먼저 전송하고 그 수만큼 개별 결과 순차 나열.
- **D-06:** 기존 Bottom의 `visionResults[10]` 고정 배열 패턴은 유지하되, 동적 FAI 모드에서는 `List<VisionResponseListData>` 또는 유사 구조를 사용.
- **D-07:** `AddResponse()` 오버라이드에서 전체 Shot의 FAI 결과를 종합하여 최종 OK/NG 판정을 산출하고, FAI별 결과를 패킷에 채운다.

### Z축 이동 & SIMUL 처리
- **D-08:** `Action_FAIMeasurement.Run()`의 EStep에 `MoveZ` 스텝을 `Grab` 전에 추가한다. 순서: Init → MoveZ → Grab → Measure → End.
- **D-09:** Z축 모터 드라이버는 인터페이스(`IAxisController` 또는 유사)만 정의하고 구현은 빈 스텁으로 둔다. 실제 HW 연동은 별도 Phase.
- **D-10:** `SIMUL_MODE`에서 `ShotConfig.SimulImagePath`가 설정되어 있으면 해당 경로에서 이미지 로드. 미설정 시 기존 VirtualCamera 폴백(D:\1.bmp).
- **D-11:** `ShotConfig.DelayMs` — MoveZ 후 카메라 안정화 대기 시간. SIMUL_MODE에서는 무시(0ms).

### UI 결과 갱신 타이밍
- **D-12:** Shot별 실시간 UI 갱신. 각 `Action_FAIMeasurement` 완료 시 `OnActionChanged` 이벤트로 UI 테이블 갱신. 기존 SequenceBase 이벤트 구조 활용.
- **D-13:** 전체 시퀀스 완료(Finish) 시 최종 종합 판정 표시. 진행 중에는 현재까지의 Shot 결과만 표시.

### Claude's Discretion
- `StartAll()` 메서드의 정확한 시그니처 (TestPacket 파라미터 포함 여부)
- `ExecuteAction()`에서 CurrentActionIndex 증가 시점의 정확한 위치 (Finish 이전 vs CopyFrom 이후)
- MoveZ 스텝에서 IAxisController 인터페이스의 메서드 시그니처
- FAI 결과 동적 리스트의 정확한 클래스명과 TestResultPacket 내 필드명
- 종합 판정 로직: AllPass = 모든 FAI.IsPass (단순 AND) vs 가중치/임계 기준
- SimulImagePath 이미지 로드 시 HImage.ReadImage 사용 vs VirtualCamera 경로 교체

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 시퀀스 프레임워크 (핵심 수정 대상)
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` — StartAll() 추가, ExecuteAction() CurrentActionIndex 증가 로직 확인
- `WPF_Example/Custom/SystemHandler.cs` — ProcessTest() IsDynamicFAIMode 분기 추가 위치 (line 233)
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` — RebuildInspectionActions(), IsDynamicFAIMode 플래그

### 검사 액션 (수정 대상)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — MoveZ 스텝 추가, SimulImagePath 이미지 로드
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — ZPosition, DelayMs, SimulImagePath 필드
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — MeasuredValue, IsPass 결과 필드
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — Shots 리스트 관리

### TCP 통신 (수정 대상)
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — TestResultPacket 확장, FAI 결과 동적 리스트
- `WPF_Example/Custom/TcpServer/ResourceMap.cs` — 기존 매핑 확인 (Site → Sequence/Action)
- `WPF_Example/TcpServer/VisionRequestPacket.cs` — TestPacket 구조 확인

### 참고 시퀀스 (AddResponse 패턴)
- `WPF_Example/Custom/Sequence/Top/Sequence_Top.cs` — AddResponse() 오버라이드 패턴 참조
- `WPF_Example/Custom/Sequence/Bottom/Sequence_Bottom.cs` — visionResults[10] 배열 패턴 참조

### Phase 4 결과물 (Datum 통합)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — Datum 계산, Action_FAIMeasurement에서 이미 호출됨
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Datum 모델

### Phase 3 결과물 (에지 측정)
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` — TryMeasure(), Action_FAIMeasurement에서 호출
- `WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs` — 측정 결과 모델

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SequenceBase.Start(int actionIndex)`: CurrentActionIndex/EndActionIndex 설정 패턴 — StartAll()은 이 패턴 확장
- `TopSequence.AddResponse()`: TestResultPacket 생성 패턴 — 새 InspectionSequence의 AddResponse() 참조
- `visionResults[10]` (Bottom): FAI별 결과 리스트 전송 선례 — 동적 리스트로 확장
- `OnActionChanged` 이벤트: Action 완료 시 UI 갱신 — Shot별 실시간 갱신에 활용
- `ShotConfig.SimulImagePath`: SIMUL_MODE용 경로 필드 — 이미 존재, 활용만 하면 됨

### Established Patterns
- `EStep enum switch` 패턴: Action_FAIMeasurement.Run()에서 Init → Grab → Measure → End
- `FinishAction(EContextResult.Pass/Fail)`: Action 종료 시 결과 코드 설정
- `#if SIMUL_MODE` 컴파일 분기: DeviceHandler, VirtualCamera에서 사용
- `ConcurrentQueue<TestResultPacket> ResponseQueue`: 시퀀스→SystemHandler 응답 전달

### Integration Points
- `ProcessTest()` (Custom/SystemHandler.cs:233): TCP 검사 요청 진입점 — IsDynamicFAIMode 분기 추가
- `ExecuteAction()` (SequenceBase.cs:211): Action 완료 후 다음 Action 진행 로직 — CurrentActionIndex++ 추가
- `MainRun()` (Custom/SystemHandler.cs:16): ResponseQueue drain → Server.SendPacket — 기존 흐름 유지
- `RebuildInspectionActions()` (Custom/SequenceHandler.cs:63): Shot별 Action 등록 — OnLoad/OnCreate 호출 보장

### Key Constraint
- `SequenceBase.Start(TestPacket)`이 `GetIndexOf(actName)` 으로 Identifier2를 찾는데, Dynamic FAI 모드에서는 Shot명이 Action명이므로 "Inspect" 매칭 실패 가능. StartAll()은 actionIndex 매칭을 우회해야 함.

</code_context>

<specifics>
## Specific Ideas

### StartAll() 초안
```csharp
// SequenceBase.cs에 추가
public bool StartAll(TestPacket packet) {
    if (State != EContextState.Idle) return false;
    if (Actions == null || Actions.Length == 0) return false;

    RequestPacket = packet;
    CurrentActionIndex = 0;
    EndActionIndex = Actions.Length - 1;

    Context.Clear();
    Command = ESequenceCommmand.Start;
    IsFinished = false;

    OnStart?.Invoke(Context);
    return true;
}
```

### ExecuteAction 수정 초안
```csharp
// SequenceBase.cs ExecuteAction() 내부
else if (actionContext.State == EContextState.Finish) {
    CurAction.OnEnd();
    IsDoneBegin = false;
    Context.CopyFrom(actionContext);
    OnActionChanged?.Invoke(actionContext);

    if (CurrentActionIndex >= EndActionIndex) {
        Context.Result = actionContext.Result;
        Finish();
    } else {
        // 다음 Action으로 진행
        CurrentActionIndex++;
        CurAction = Actions[CurrentActionIndex];
    }
}
```

### ProcessTest 분기 초안
```csharp
// Custom/SystemHandler.cs ProcessTest()
private bool ProcessTest(TestPacket packet) {
    if (Sequences.IsDynamicFAIMode) {
        string seqName = ResourceMap.Find(EResource.Sequence, (ESite)packet.Site);
        SequenceBase seq = Sequences[seqName];
        if (seq == null) return false;
        return seq.StartAll(packet);
    }
    return Sequences.Start(packet);
}
```

### MoveZ 스텝 추가 초안
```csharp
// Action_FAIMeasurement EStep 확장
private enum EStep {
    Init,
    MoveZ,    // 신규
    Grab,
    Measure,
    End
}

case EStep.MoveZ:
    #if SIMUL_MODE
    // SIMUL: SimulImagePath가 있으면 이미지 교체 준비
    Step = (int)EStep.Grab;
    #else
    // 실 장비: Z축 이동 명령 (인터페이스 호출)
    // IAxisController?.MoveToPosition(ShotParam.ZPosition);
    // Thread.Sleep(ShotParam.DelayMs);
    Step = (int)EStep.Grab;
    #endif
    break;
```

</specifics>

<deferred>
## Deferred Ideas

- Z축 모터 드라이버 실제 구현 (HW 연동) — 별도 디바이스 Phase
- 검사 결과 로깅/히스토리 저장 (파일/DB) — v2 기능
- 검사 중 사용자 중단(Abort) 처리 — 현재 Stop()은 있으나 Shot 중간 중단 UX 미정의
- Shot 병렬 실행 (멀티 카메라 동시 Grab) — 현재는 순차 실행만
- 검사 결과 이미지 자동 저장 (NG 시) — RawImageSaveService 연동

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-tcp*
*Context gathered: 2026-04-09*
