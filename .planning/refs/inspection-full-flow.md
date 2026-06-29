# 검사 전체 흐름 분석 — TCP 수신 → 결과 송신

> 작성일: 2026-06-29  
> 목적: $TEST 수신부터 $RESULT 송신까지 단계별 코드 경로 파악

---

## 전체 흐름 요약

```
$TEST 수신
  → ProcessTest()
    → seq.StartAll()
      → Action_FAIMeasurement (Top/Bottom/Side 병렬)
          → DatumPhase (위치보정)
          → Grab (이미지 취득)
          → Measure (FAI 측정)
      → InspectionSequence.AddResponse()
          → 결과 집계 + P/F/B 판정
          → ResponseQueue 등록
  → MainRun() PopResponse()
    → Server.SendPacket()
$RESULT 송신
```

---

## 1. TCP 수신 & 라우팅

**파일:** `Custom/SystemHandler.cs`

```
MainRun() — 1ms 폴링 루프
  │
  ├─ [recv] Server.GetRecvPacket(i) → VisionRequestPacket
  │   └─ RequestType == Test → ProcessTest(packet.AsTest())
  │
  └─ [send] Sequences[i].PopResponse() → Server.SendPacket(response)

ProcessTest()
  ├─ IsRecipeReady 가드                     ← 레시피 미로드 시 거부
  ├─ packet.TestID = _lastPrepZIndex 주입   ← $PREP 때 저장한 z_index
  └─ IsDynamicFAIMode 분기
      ├─ true  → seq.StartAll(packet)
      └─ false → Sequences.Start(packet)
```

---

## 2. 시퀀스 시작

**파일:** `Custom/Sequence/Inspection/InspectionSequence.cs`

```
StartAll(packet)
  └─ OnStart 이벤트 → HandleRunStartResetResults()
      └─ 모든 Action(Shot)의 Measurement.ClearResult()
          ← 이전 사이클 stale 결과 제거
```

Top / Bottom / Side 시퀀스가 **병렬**로 각자 Action 루프 진입.

---

## 3. Action_FAIMeasurement.Run()

**파일:** `Custom/Sequence/Inspection/Action_FAIMeasurement.cs`

### 3-1. EStep.Init

```
ShotParam.ClearAllResults()   ← Shot 단위 결과 초기화
```

### 3-2. EStep.MoveZ

```
Delay(DelayMs)   ← Z축 이동 대기 (ms 단위 타이머)
```

### 3-3. EStep.DatumPhase ★ 위치보정

```
parentSeq.ClearDatumTransforms()
  ├─ _datumTransforms.Clear()
  ├─ _failedDatums.Clear()
  └─ _alignFailedDatums.Clear()

DatumConfigs 순회
  └─ 각 Datum:
      ├─ GrabOrLoadDatumImage(datum)        단일 이미지 Datum
      │   또는 TryGrabOrLoadDualDatumImages()  DualImage Datum
      │
      ├─ [IsPatternAlignEnabled == true]
      │   └─ parentSeq.TryComposeAlign(datum, img)
      │       → Shape 매칭 → datum transform 생성 (이동/회전 행렬)
      │
      ├─ [IsPatternAlignEnabled == false]
      │   └─ parentSeq.TryRunSingleDatum(datum, img)
      │       → 기존 HALCON 에지 검출 경로
      │
      └─ 실패 → MarkDatumFailed(datum.DatumName)
               또는 MarkAlignFailed()
```

### 3-4. EStep.Grab ★ 이미지 취득

```
DeviceHandler.GrabHalconImage(ShotParam)
  ├─ SIMUL_MODE: SimulImagePath 에서 HImage 로드
  └─ REAL:       카메라 라이브 Grab
ShotParam.SetImage(img)
```

### 3-5. EStep.Measure ★ FAI 측정

```
ShotParam.FAIList 순회
  └─ 각 FAI:
      └─ fai.Measurements 순회
          │
          ├─ per-FAI gate: IsDatumFailed || IsAlignFailed
          │   └─ true → meas.ClearResult()
          │              LastSkipReason = "DATUM_FAIL" | "ALIGN_FAIL"
          │              LastJudgement = false → skip
          │
          ├─ TryGetDatumTransform(meas.DatumRef)
          │   └─ datum 검출 transform 반환 (없으면 identity)
          │
          ├─ meas.TryExecute(image, transform, pixRes)
          │   └─ MeasurementBase 구현체 실행
          │       └─ ROI → HALCON 에지 측정 → 측정값(mm) + XLD overlay
          │
          ├─ meas.EvaluateJudgement(resultValue)
          │   └─ LastMeasuredValue 설정
          │      LastJudgement = (min <= value <= max)
          │
          └─ overlay suffix: "-OK" / "-NG"

      FAI 집계 (Measurements 루프 종료 후)
      ├─ fai.IsPass = faiAllPass
      ├─ fai.MeasuredValue = Measurements[0].LastMeasuredValue
      ├─ fai.WasDatumSkipped = (Measurements 중 DATUM_FAIL 존재)
      ├─ fai.LastOverlays = 누적 XLD
      └─ QueueFaiCapture()   ← 이미지 비동기 저장
```

### 3-6. EStep.End

```
FinishAction(AllPass ? EContextResult.Pass : EContextResult.Fail)
  └─ InspectionSequence.AddResponse() 호출 트리거
```

---

## 4. 결과 집계 & 판정

**파일:** `Custom/Sequence/Inspection/InspectionSequence.cs`

```
AddResponse()
  └─ UseProtocolV1 분기
      ├─ true  → AddResponseV1Cycle()   ← Protocol V1 다중샷 엔진
      └─ false → 기존 v2.6 경로 (전체 Shot 일괄 집계)
```

### 4-1. AddResponseV1Cycle() — Index 0 (Datum 샷)

```
ParseCurrentZIndex()           ← RequestPacket.TestID → m_nCurrentZIndex

Index == 0:
  ResetCycleState()
    ├─ m_bCycleHasNG = false
    └─ m_bCycleDatumFailed = false

  m_nLastZIndex = ComputeLastZIndex(recipeManager)
  m_bCycleDatumFailed = DetectDatumFailure()

  BuildDatumShotResponse()
    ├─ Datum 실패 없음 → TestResultPacket(IsBuffer=true, FAIResults 빈 배열)
    └─ Datum 실패 있음 → TestResultPacket(IsBuffer=false, Result=NG) 즉시 실패

  PersistAndEnqueueV1(packet)
    └─ ResponseQueue.Enqueue()
```

### 4-2. AddResponseV1Cycle() — Index >= 1 (측정 샷)

```
bIsLastIndex = (m_nCurrentZIndex >= m_nLastZIndex)

BuildScopedResponse(recipeManager, nZIndex, bIsLastIndex)
  │
  ├─ AggregateIndexFais(recipeManager, nZIndex, packet)
  │   └─ Shot.ZIndex == nZIndex 인 FAI만 집계
  │       └─ AddFaiResult(packet, fai)
  │           └─ fai.Measurements 순회
  │               └─ ClassifyMeasurement(meas)
  │                   ├─ DATUM_FAIL / ALIGN_FAIL → 'N' (NotExist)  + m_bCycleHasNG
  │                   ├─ LastJudgement == false   → 'F' (NG)        + m_bCycleHasNG
  │                   └─ 기타                     → 'P' (OK)
  │               packet.FAIResults.Add(id, eCode, value)
  │               id 규칙:
  │                 단일 Measurement → "FAIName"
  │                 다중 Measurement → "FAIName_P1", "FAIName_P2", ...
  │
  └─ ApplyCycleJudgement(packet, bIsLastIndex, nMatchedShots)
      ├─ 중간 Index (bIsLastIndex == false)
      │   └─ IsBuffer=true, Result='B' → 버퍼 응답
      └─ 마지막 Index (bIsLastIndex == true)
          ├─ bCycleFail = m_bCycleHasNG || m_bCycleDatumFailed || (nMatchedShots==0)
          ├─ 실패 → IsBuffer=false, Result='F'
          └─ 성공 → IsBuffer=false, Result='P'
```

### 4-3. FAI 단위 분류 (v2.6 호환)

```
ClassifyFai(fai)
  ├─ fai.WasDatumSkipped → 'N' (NotExist)
  ├─ fai.IsPass == false → 'F' (NG)
  └─ 기타               → 'P' (OK)
```

---

## 5. 영속화 & 큐 등록

```
PersistAndEnqueueV1(recipeManager, packet)
  ├─ CycleResultSerializer.BuildDto(recipeManager, Result, ...)
  │   └─ 측정 결과 JSON 구조화 (Index별 스냅샷)
  ├─ CycleResultSerializer.SaveAsync(cycleDto)
  │   └─ cycle.json 비동기 파일 쓰기
  └─ ResponseQueue.Enqueue(packet)
```

---

## 6. TCP $RESULT 송신

**파일:** `Custom/SystemHandler.cs`

```
MainRun()
  └─ Sequences[i].PopResponse()
      └─ ResponseQueue.Dequeue() → TestResultPacket
          ├─ Site
          ├─ Type
          ├─ Result ('P' / 'F' / 'B')
          ├─ IsBuffer
          └─ FAIResults[] → id=value=judge,...
  └─ Server.SendPacket(response)

Wire 포맷:
  $RESULT:site;Type;P|F|B;count;id=val=judge,...@
```

---

## 단계별 파일 & 메서드 요약

| 단계 | 파일 | 메서드 |
|------|------|--------|
| TCP 수신 | `Custom/SystemHandler.cs` | `MainRun()`, `ProcessTest()` |
| 시퀀스 시작 | `Custom/Sequence/Inspection/InspectionSequence.cs` | `StartAll()`, `HandleRunStartResetResults()` |
| Datum 검출 | `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | `EStep.DatumPhase` |
| 패턴 정렬 | `Custom/Sequence/Inspection/InspectionSequence.cs` | `TryComposeAlign()`, `TryRunSingleDatum()` |
| 이미지 Grab | `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | `EStep.Grab` |
| FAI 측정 | `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | `EStep.Measure` |
| 측정 알고리즘 | `Halcon/Algorithms/MeasurementAlgorithm.cs` (및 MeasurementBase 구현체) | `TryExecute()` |
| FAI 판정 | `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | `EvaluateJudgement()` |
| 결과 집계 | `Custom/Sequence/Inspection/InspectionSequence.cs` | `AddResponseV1Cycle()`, `AddFaiResult()` |
| 측정 분류 | `Custom/Sequence/Inspection/InspectionSequence.cs` | `ClassifyMeasurement()` |
| 사이클 판정 | `Custom/Sequence/Inspection/InspectionSequence.cs` | `ApplyCycleJudgement()` |
| 영속화 | `Custom/Sequence/Inspection/InspectionSequence.cs` | `PersistAndEnqueueV1()` |
| TCP 송신 | `Custom/SystemHandler.cs` | `MainRun()`, `Server.SendPacket()` |

---

## 판정 코드

| 코드 | 의미 | 조건 |
|------|------|------|
| `'P'` | Pass (정상) | 전 측정 판정 통과 |
| `'F'` | Fail (불량) | 1건 이상 NG 또는 Datum 실패 |
| `'B'` | Buffer (진행 중) | 마지막 Index가 아닌 중간 응답 |
| `'N'` | NotExist | Datum/Align 검출 실패로 측정 불가 |

---

## 주요 데이터 구조

```
ShotConfig
  └─ FAIConfig[]
      └─ MeasurementBase[]
          ├─ DatumRef (참조할 Datum 이름)
          ├─ Min / Max (공차)
          ├─ LastMeasuredValue (측정값 mm)
          ├─ LastJudgement (true=OK / false=NG)
          └─ LastSkipReason ("DATUM_FAIL" / "ALIGN_FAIL" / "")

TestResultPacket
  ├─ Site, Type
  ├─ Result (EVisionResultType.OK/NG/NotExist)
  ├─ IsBuffer
  └─ FAIResults[]
      ├─ Id    ("FAIName" 또는 "FAIName_P1")
      ├─ ECode ('P' / 'F' / 'N')
      └─ Value (측정값 mm, 문자열)
```
