# Phase 68: Z축 교차(Cross-Z) Dual-Image 측정 지원 - Research

**Researched:** 2026-07-21
**Domain:** 내부 C# 상태머신/프로토콜 엔진 확장 (.NET Framework 4.8, WPF, Halcon 24.11) — 외부 라이브러리 조사 아님, 순수 코드베이스 정밀 조사
**Confidence:** HIGH (모든 파일을 이번 세션에 직접 재확인, 라인번호는 2026-07-21 기준 작업트리 최신 상태)

## Summary

이 phase는 3개의 독립적이지만 얽힌 변경을 요구한다: (1) v1.0 z_index 실행 스코프를 응답 집계 레벨(이미 있음, Phase 49)에서 **실행(grab) 레벨**로 확장, (2) InspectionSequence에 크로스-Z 값 보존 멤버 추가, (3) `DualImageEdgeDistanceMeasurement`/`DatumConfig`에 `ZIndexA`/`ZIndexB` 필드 추가 + 라이브 캡처 전환. 코드베이스를 정밀 재조사한 결과, CONTEXT.md의 결정(D-01~D-09)은 모두 유효하지만 **한 가지 중대한 아키텍처 함정**이 발견됐다: `SequenceBase.StartSubset`은 스파스(sparse) 선택이 아니라 **min-max 연속 구간**만 실행하며, Datum 검출(z_index=0)은 독립된 "Shot"이 아니라 **모든 Action의 `EStep.DatumPhase` 안에 내장**되어 있어 D-01을 문자 그대로 구현하면(오직 해당 z_index Shot만 실행) z_index=0 요청 시 실행할 Action이 0개가 되어 Datum 검출 자체가 멈추는 회귀가 발생한다. 이 두 가지는 Open Questions 섹션에 최우선으로 기록했다.

**Primary recommendation:** D-01 실행 스코프는 `Custom/SystemHandler.ProcessTest`(WPF_Example/Custom/SystemHandler.cs:200-220)에서 배선하되, z_index==0(Datum)과 z_index>=1(측정)을 **분리된 정책**으로 취급해야 한다 — 이 phase에서 반드시 사람이 결정해야 할 사항(Open Question #1)이다. ZIndexA/B 필드는 `MeasurementBase.Load` (WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:146-155)의 `MeasCorrectionFactor` sentinel-복원 패턴을 그대로 복제해 -1(미설정) 기본값을 보장한다. D-08 버그 수정은 `TryGrabOrLoadFaiDualImages`(Action_FAIMeasurement.cs:444-483)의 `pathA` 산출 로직에 `ShotParam.HasImage`/`GetImage()` 우선 분기 1개만 추가하면 된다.

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** v1.0(UseProtocolV1) 경로에서 `$TEST(z=N)` 도착 시, 그 z_index에 매핑된 Shot만 실행하도록 완전 수정한다. v2.6/legacy 경로는 무수정(회귀 0). `SequenceBase`에 이미 존재하는 부분실행 기능(`StartSubset`/`StartCore(first,last)`)을 재활용해서 새 그랩 메커니즘을 만들지 않는다. Phase 49 D-01(응답 집계 레벨에서만 구현되던 갭)을 실행 레벨에서 닫는다.
- **D-02:** 크로스-Z 값(Z1에서 찾은 에지 등)은 InspectionSequence 레벨의 사이클 공유 저장소(멤버 상태)에 보관한다. 신규 상태머신 클래스는 도입하지 않는다 — Phase 49 D-02(`_failedDatums`/`_datumTransforms`와 동일 lifecycle에 멤버 추가) 패턴 재사용.
- **D-03:** 리셋 시점 = z_index=0(사이클 시작) 수신 시 자동. Phase 49 D-08과 동일한 기존 리셋 지점에 편승. 별도 타이머/타임아웃 불필요.
- **측정/Datum 객체 자체에 직접 저장 방식은 채택하지 않음** — 레시피 전역 재사용 인스턴스라 사이클 경계 리셋 훅이 없고 다음 부품/사이클로 값이 새어나갈 위험.
- **D-04:** ZIndexA/ZIndexB 필드는 측정 자체(`DualImageEdgeDistanceMeasurement`)에 배치. 기존 `TeachingImagePath_Horizontal`/`_Vertical`과 같은 자리. Shot에는 두지 않는다.
- **D-05:** 잘못된 설정(ZIndexA==ZIndexB, 레시피에 존재하지 않는 index를 가리킴)은 실행(측정) 시점에 걸러낸다. `DatumRef` 미해결 패턴(`MarkMeasurementDatumRefMissing`, `SkipReason.DATUM_REF_MISSING`)과 동일하게 명시적 NG(이유 로그 포함) 처리 — `DatumConfig.SourceShotName`의 조용한 `Shots[0]` 폴백은 반면교사로만 참고.
- **D-06:** Datum 쪽(`VerticalTwoHorizontalDualImage`, Side/Bottom에서 이미 사용 중)도 이번 phase에 같이 포함. `DatumConfig`에도 ZIndexA/ZIndexB 추가 + `Action_FAIMeasurement.TryGrabOrLoadDualDatumImages`를 라이브 캡처로 교체.
- **D-07:** 기존 레시피(ZIndexA/ZIndexB 미설정)는 100% 그대로 동작(회귀 0) — 비어있으면 기존 `TeachingImagePath_Horizontal`/`_Vertical` 파일 로드 경로 그대로 사용.
- **D-08:** `DualImageEdgeDistanceMeasurement`의 imageA가 이미 grab된 라이브 이미지를 무시하고 항상 파일에서 재로드하는 버그 수정(`Action_FAIMeasurement.cs` `TryGrabOrLoadFaiDualImages` + `TryExecuteMeasurement`) — 같이 수정.
- **D-09 (코딩 규약):** `InspectionSequence.cs`/`Action_FAIMeasurement.cs`의 z_index 실행스코프·크로스-Z 상태 관련 신규/수정 코드는 `.planning/refs/control-sequence-coding-guideline.md`(LOCKED) 준수 — 헝가리언 표기법 + `if/else if/else`만(삼항·null병합 금지) + 조건식 변수화 + 매직넘버 상수화 + 함수 30줄 초과 분리. `DualImageEdgeDistanceMeasurement.cs`/`DatumConfig.cs` 같은 순수 측정/데이터 클래스는 기존 CLAUDE.md 파일 스타일(카멜케이스 등) 유지.

### Claude's Discretion

- 사이클 공유 저장소의 정확한 자료구조(예: `Dictionary<string, HeldEdgeValue>` 측정 식별자 키)와 필드명(헝가리언 접두사 준수 하에).
- `StartSubset` 호출을 어느 지점(`Custom/SystemHandler.ProcessTest` vs `SequenceHandler`)에 배선할지.
- 신규 `SkipReason` 상수명(`ZINDEX_MISCONFIGURED` 등).
- ParamBase INI 직렬화 시 ZIndexA/B 기본값(예: -1 = 미설정 sentinel) 및 Load 오버라이드 필요 여부.
- Datum/측정 두 곳에 ZIndexA/B를 각각 추가할지, 공유 헬퍼/인터페이스(`IZIndexPair` 류)로 추출할지.

### Deferred Ideas (OUT OF SCOPE)

- 물리 Z축 모터 제어(IAxisController 실제 구현) — PLC 담당, 이 프로젝트 범위 아님.
- v2.6(기본 프로토콜, UseProtocolV1=false) 경로 — z_index 개념 자체가 없어 무관.
- `ApplyPrepToSequences`의 별개 결함(동일 nZIndex에 TOP/BOTTOM 둘 다 매칭 Shot이 있으면 나중 순회 시퀀스가 항상 이김) — 별도 처리.
- v1.0 NG 누적이 index 0 기준으로만 리셋되는 취약점 — 이번 phase는 그 리셋 지점에 편승만, 근본 수정은 범위 밖.
- legacy `$LIGHT` 커맨드, 수동 UI grab, `ProcessAlignTest`(이더넷 비전) 경로의 레이스 — 이미 문서화됨, 별도.
- PROTO-06 통신 회귀 시험 — 제어팀 동기화 후 별도.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `$TEST(z=N)` 수신 → 실행 범위 결정 | Sequence 오케스트레이션 (`Custom/SystemHandler.ProcessTest`) | Sequence 엔진 (`SequenceBase.StartSubset/StartCore`) | TCP 진입점이 "무엇을 실행할지" 결정, 실제 실행 메커니즘은 프레임워크 계층이 소유 |
| z_index → Shot 매핑 조회 | Sequence 오케스트레이션 (`InspectionSequence.FindShotByZIndex`/`ComputeLastZIndex`/`AggregateIndexFais`) | — | Phase 49가 이미 이 계층에 구현 — ResourceMap(TCP 어댑터 계층)은 관여하지 않음(아래 State of the Art 참고) |
| Z1→Z2 크로스-Z 값 보존 | Sequence 오케스트레이션 (`InspectionSequence` 멤버 상태) | — | `_datumTransforms`/`_failedDatums`와 동일 lifecycle, 사이클 경계(z=0)에 결속 |
| Datum 검출(모든 z_index 공통) | Action 실행 계층 (`Action_FAIMeasurement.EStep.DatumPhase`) | Sequence (DatumConfigs 소유) | Datum은 독립 Shot이 아니라 매 Action 실행마다 내장 실행되는 phase — **z_index=0 실행 스코프 설계의 핵심 제약**(Open Question #1) |
| 측정 실행(에지 추출, 거리 계산) | Action 실행 계층 (`Action_FAIMeasurement.EStep.Measure` → `MeasurementBase.TryExecute`) | Halcon 알고리즘 계층 (`VisionAlgorithmService`) | 측정 로직 자체는 이번 phase 변경 없음 — ZIndexA/B는 "어떤 이미지를 넣을지"만 결정 |
| 라이브 이미지 취득(카메라 grab) | Device 계층 (`DeviceHandler.GrabHalconImage`, `VirtualCamera`) | Action 실행 계층 (grab 호출 시점/락 관리) | 신규 ZIndexA/B 라이브 캡처도 기존 grab 경로 재사용 — 신규 그랩 메커니즘 금지(D-01) |
| TCP RESULT B/P/F 직렬화 | TCP 어댑터 계층 (`VisionResponsePacket.BuildResultMessageV1`) | Sequence (`InspectionSequence.ApplyCycleJudgement`) | 직렬화(Phase 48)는 완성됨, 무엇을 B/P/F로 채울지만 Sequence 계층 책임(Phase 49/68) |

## Standard Stack

내부 아키텍처 확장 phase — 신규 외부 패키지/라이브러리 없음. `packages.config`의 기존 의존성(halcondotnet, OpenCvSharp4 등)만 사용하며 변경 없음.

## Package Legitimacy Audit

**해당 없음** — 이 phase는 신규 NuGet/외부 패키지를 설치하지 않는다. 전량 내부 C# 코드(`WPF_Example/Custom/Sequence/**`, `WPF_Example/Sequence/**`) 수정.

## Architecture Patterns

### System Architecture Diagram (z_index 실행 흐름, 현재 vs 필요 변경)

```
[TCP $PREP(z_index,Op=1)] → SystemHandler.ProcessPrep()
        │ (라인 707-740)
        ├─ _lastPrepZIndex = z_index 저장
        └─ ApplyPrepToSequences(z_index) → InspectionSequence.ApplyShotLights(z_index) [조명 세팅]

[TCP $TEST] → SystemHandler.ProcessTest()  (라인 200-220)
        │
        ├─ packet.TestID = _lastPrepZIndex.ToString()   ← z_index 주입
        │
        ├─ (오늘) if (Sequences.IsDynamicFAIMode) → seq.StartAll(packet)
        │          └─ ★ 항상 시퀀스 소유 Action 전부 실행 (z_index 무관) — D-01이 닫아야 할 갭
        │
        └─ (D-01 이후 목표) → z_index 기반으로 실행 범위 선택
                 │
                 ├─ z_index == 0 (Datum)  → ??? (Open Question #1 — 아래 참고)
                 │
                 └─ z_index >= 1 (측정)   → StartSubset(이 z_index에 매핑된 Action 인덱스, packet)
                          └─ SequenceBase.StartCore(first, last, packet)
                                   └─ MainExecute 루프가 first..last "연속 구간 전체"를 순차 실행
                                        (Array.Sort 로 min/max만 취함 — 스파스 실행 아님!)

각 실행되는 Action(=Shot).Run() 내부 (Action_FAIMeasurement.cs:58-341):
  EStep.Init → EStep.MoveZ → EStep.DatumPhase (DatumConfigs 전체 재검출 + 이 Shot 이미지 grab, 같은 lock)
             → EStep.Measure (이 Shot의 FAI/Measurement 순회, DatumRef 게이트)
             → EStep.End → FinishAction()

마지막 Action 종료(CurrentActionIndex>=EndActionIndex) → SequenceBase.Finish()
  → AddResponse() → (v1.0) InspectionSequence.AddResponseV1Cycle()
        ├─ z_index==0 → HandleDatumIndexResponse (리셋 + Datum 실패감지 + 빈 B/즉시 F)
        └─ z_index>=1 → BuildScopedResponse (이 z_index 매칭 Shot의 FAI만 집계) → ApplyCycleJudgement (B/P/F)
```

### Recommended Project Structure

신규 파일 없음. 수정 대상만:

```
WPF_Example/Custom/SystemHandler.cs                                    # ProcessTest — 실행 스코프 필터 배선 지점
WPF_Example/Custom/Sequence/SequenceHandler.cs                         # RebuildInspectionActions — Actions[] 순서/z_index 정렬 필요 여부 검토
WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs           # 크로스-Z 저장소 멤버 + z_index→ActionIndices 헬퍼
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs        # ZIndexA/B 라이브 캡처 + D-08 버그 수정 + DatumPhase↔Measure 스킵 로직(선택 시)
WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs  # ZIndexA/B 필드 추가
WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs                  # ZIndexA/B 필드 추가 + ICustomTypeDescriptor hide 룰
WPF_Example/Custom/Sequence/Inspection/SkipReason.cs                   # 신규 상수(예: ZINDEX_MISCONFIGURED)
WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs              # (참고용, 무수정) Load sentinel 패턴의 원본
```

### Pattern 1: Sentinel int 필드 하위호환 로드 (MeasCorrectionFactor 정확한 선례)

**What:** `ParamBase.Load`(WPF_Example/Sequence/Param/ParamBase.cs:377-380)는 리플렉션으로 모든 `int` 프로퍼티를 `loadFile[group][name].ToInt()`로 읽는다 — INI에 키가 없으면 IniFile 인덱서가 기본값(0)을 반환하므로, `ZIndexA`/`ZIndexB`에 `-1`(미설정) 기본값을 부여해도 **구 레시피 로드 시 프로퍼티 초기화자(-1)가 아니라 0으로 덮어써진다.**
**When to use:** ZIndexA/ZIndexB 같은 "0이 유효값과 헷갈리는 sentinel int" 필드에 반드시 적용.
**Example (정확한 기존 선례, 그대로 미러링 가능):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:143-155
// 하위호환: ParamBase.Load 는 INI 누락 double 키를 0 으로 덮어쓴다. 구 레시피엔 MeasCorrectionFactor 키가 없어
//  0 으로 로드되면 EvaluateJudgement 에서 value×0=0 → 전 측정 0/NG(회귀). 키 부재 시에만 1.0(무보정) 복원한다.
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);
    IniSection sec;
    if (!loadFile.TryGetSection(groupName, out sec) || sec == null || !sec.ContainsKey("MeasCorrectionFactor")) {
        MeasCorrectionFactor = 1.0;
    }
    return result;
}
```
`DualImageEdgeDistanceMeasurement`는 이미 `MeasurementBase`를 상속하므로, `ZIndexA`/`ZIndexB`를 위해 `Load`를 **재정의(override)**하고 `base.Load()` 호출 후 `!sec.ContainsKey("ZIndexA")` → `ZIndexA = -1` 복원(동일 패턴 2필드 반복)이 필요하다. `DatumConfig`도 동일 패턴 적용 필요(단, `DatumConfig`는 `MeasurementBase`가 아니라 `ParamBase` 직접 상속이므로 `MeasurementBase.Load`를 재사용할 수 없고 자체 `Load` 오버라이드가 필요 — `DatumConfig.cs`에 기존 `Load` 오버라이드가 있는지 확인 필요, line 867-868에 `TeachingImagePath` null 가드 로직이 있어 이미 `Load`류 정규화 메서드가 존재함을 시사).

### Pattern 2: 명시적 NG + SkipReason (DatumRef 미해결 선례 — D-05 근거)

**What:** 잘못된 참조/설정을 조용히 폴백하지 않고 명시적으로 NG 처리 + 로그.
**Example:**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:606-615
private void MarkMeasurementDatumRefMissing(MeasurementBase meas) {
    meas.ClearResult();
    meas.LastSkipReason = SkipReason.DATUM_REF_MISSING;
    meas.LastJudgement = false;
    string measName = meas.MeasurementName;
    if (measName == null) measName = meas.TypeName;
    string datumRef = meas.DatumRef;
    if (datumRef == null) datumRef = "";
    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' skipped — DatumRef '" + datumRef + "' 에 해당하는 Datum 이 레시피에 없음 (오타/개명/삭제 확인 필요, " + meas.LastSkipReason + ")");
}
```
게이트 호출 위치는 `EStep.Measure` 루프 안(`Action_FAIMeasurement.cs:271-287`) — `parentSeq2.IsDatumFailed(meas.DatumRef)` / `parentSeq2.IsDatumRefUnresolvable(meas.DatumRef)` 체크와 나란히, ZIndexA==ZIndexB 또는 존재하지 않는 index 참조 게이트를 같은 자리에 추가하는 것이 자연스럽다.

### Pattern 3: 조명 채널 반복 헬퍼 (`ApplyChannelLight`)로 코드 중복 축소

`ApplyShotLightsInternal`/`ApplyDatumLightsInternal`(InspectionSequence.cs:370-467)이 동일 6+4채널 반복 구조를 `ApplyChannelLight` 헬퍼(471-482)로 축소한 전례 — 신규 크로스-Z 캡처 헬퍼("이 z_index의 Shot 이미지를 가져오거나 grab")를 만들 때도 동일하게 공용 private 메서드로 추출해 A/B 양쪽에서 재사용할 것.

### Anti-Patterns to Avoid

- **`DatumConfig.SourceShotName`의 조용한 `Shots[0]` 폴백** (`InspectionSequence.ResolveDatumModelPath`, InspectionSequence.cs:917-923: `if (matched == null) matched = shots[0];`) — D-05가 명시적으로 배제한 패턴. ZIndexA/B는 매칭 실패 시 절대 임의 Shot으로 폴백하지 말 것.
- **StartSubset을 "스파스 선택"으로 오해** — 아래 Common Pitfalls #2 참고.
- **Datum을 "z_index=0의 Shot"으로 착각** — 아래 Common Pitfalls #1 참고. Datum은 Shot이 아니라 모든 Action 실행에 내장된 phase다.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| z_index 부분 실행 | 새 그랩/스레드 메커니즘 | `SequenceBase.StartSubset(int[], TestPacket)` (SequenceBase.cs:374-386) | D-01 명시 결정 — 단, 연속 구간 제약(Open Question #1) 인지 필수 |
| 크로스-Z 값 보존 | 신규 상태머신 클래스 | `InspectionSequence` 멤버 + `_datumTransforms` 패턴(InspectionSequence.cs:55,58,63-70) | D-02 명시 결정, 기존 사이클 리셋 훅(`ResetCycleState`, 486-492) 재사용 |
| INI 하위호환 sentinel 값 | 커스텀 직렬화 코드 | `ParamBase.Load` override + `ContainsKey` 가드(Pattern 1) | 이미 검증된 프로덕션 패턴(MeasCorrectionFactor) |
| PropertyGrid 알고리즘별 필드 숨김 | 신규 UI 프레임워크 코드 | `DatumConfig.ICustomTypeDescriptor`/`IsHiddenForAlgorithm`(DatumConfig.cs:982-1014) | 기존 DualImage 전용 필드(`TeachingImagePath_Vertical`) hide 로직과 동일 자리 |

**Key insight:** 이 phase의 모든 빌딩 블록(부분실행, 상태보존, sentinel 로드, 명시적 NG)은 Phase 49/37/54에서 이미 프로덕션 검증됐다. 새로 발명할 메커니즘은 원칙적으로 없다 — 유일한 진짜 신규 설계 결정은 "z_index=0(Datum)을 실행 스코프 필터에서 어떻게 다룰지"(Open Question #1)뿐이다.

## Common Pitfalls

### Pitfall 1 (CRITICAL): Datum(z_index=0)은 독립 Shot이 아니다 — D-01을 문자 그대로 구현하면 Datum 검출이 멈춘다

**What goes wrong:** "그 z_index에 매핑된 Shot만 실행"을 `shot.ZIndex == nZIndex`인 Action만 골라 `StartSubset`에 넘기는 방식으로 구현하면, z_index==0(Datum) 요청 시 해당하는 Shot이 **하나도 없다**(레시피의 실측 Shot들은 ZIndex=1,2,... 이며 Datum은 `InspectionSequence.DatumConfigs`라는 별도 리스트로 관리되지 시퀀스의 `Actions[]`/`ShotConfig`와 무관). `StartSubset`은 빈 배열을 받으면 즉시 `false`를 반환하고(SequenceBase.cs:377) 아무 Action도 실행되지 않는다 → Datum 검출(`EStep.DatumPhase`)이 한 번도 안 돌아 `_datumTransforms`가 비어 모든 측정이 identity fallback 또는 `IsDatumRefUnresolvable`로 NG.

**Why it happens:** `Action_FAIMeasurement.EStep.DatumPhase`(Action_FAIMeasurement.cs:81-233)는 **모든 Action(=모든 Shot) 실행마다 내장되어 재실행**되는 phase다 — Datum 전용 Action이 따로 없다. 현재(Phase 68 이전) `ProcessTest`가 항상 `StartAll`(전체 Shot 실행)을 호출하기 때문에 이 결합이 드러나지 않았을 뿐이다.

**How to avoid:** z_index==0과 z_index>=1을 **명시적으로 분리된 정책**으로 처리해야 한다. 두 가지 후보(Open Question #1에서 사람이 결정):
- **옵션 A (낮은 리스크):** z_index==0은 기존처럼 `StartAll`(전체 Shot 실행) 유지 — waste 제거는 z_index>=1에만 적용. Datum이 매 index마다 재검출되는 기존 중복(모든 Action이 각자 DatumConfigs 전체를 재검출)도 무변경.
- **옵션 B (완전한 waste 제거, 더 큰 blast radius):** `Action_FAIMeasurement`에 "이 실행이 Datum-only인지" 신호를 주입해 `EStep.DatumPhase` 이후 `EStep.Measure`를 건너뛰고 바로 `EStep.End`로 가게 하는 조건 분기 추가(현재 `Grab`처럼 dead-fallback이 아니라 실제 스킵). 최소 1개 Action은 여전히 실행되어야 한다(DatumPhase 트리거용) — 어떤 Action을 고를지(첫 번째 소유 Action?)도 결정 필요.

**Warning signs:** UAT 중 z_index=0 TCP 응답 후 z_index=1 요청에서 모든 측정이 `DATUM_FAIL`/`DATUM_REF_MISSING`으로 NG 처리되면 이 문제.

### Pitfall 2 (CRITICAL): `StartSubset`은 스파스 선택이 아니라 min-max 연속 구간 실행이다

**What goes wrong:** `SequenceBase.StartSubset(int[] actionIndices, packet)`(SequenceBase.cs:374-386)는 `actionIndices`를 정렬해 `first`/`last`만 취하고 `StartCore(first, last, packet)`을 호출한다. `MainExecute`의 진행 로직(SequenceBase.cs:236-242, `CurrentActionIndex++`가 `EndActionIndex`까지 매 tick 순차 증가)은 **first부터 last까지 사이의 모든 Action을 실행**하며 `actionIndices` 배열 자체를 다시 참조하지 않는다. 즉 `StartSubset(new[]{2,5}, packet)`은 인덱스 2,3,4,5를 **전부** 실행한다(2와 5만이 아니라).

**Why it happens:** `Actions[]` 배열은 `SequenceHandler.RebuildInspectionActions`(Custom/Sequence/SequenceHandler.cs:99-130)가 `RecipeManager.Shots` 리스트를 **순회 순서 그대로**(OwnerSequenceName 필터만 적용, ZIndex 정렬 없음) Action으로 변환해 만든다. `InspectionRecipeManager.AddShot`(InspectionRecipeManager.cs:40)은 항상 리스트에 append만 하며, 코드베이스 전체에서 Shot 재정렬/삽입 기능은 발견되지 않았다(`Shots.Insert`/`ReorderShot`/`MoveShot` 0건). 따라서 같은 z_index를 가진 Shot들이 `Actions[]` 배열에서 **인접하다는 보장이 전혀 없다** — 사용자가 UI에서 Shot을 추가한 순서에 전적으로 의존한다.

**How to avoid:** 아래 중 하나를 planner가 명시적으로 선택해야 한다(Open Question #2):
- Shot이 z_index 순서로 인접하도록 레시피 편집 관례를 강제/검증(런타임 경고만, 실행 안전은 보장 못 함).
- `RebuildInspectionActions`가 Shot을 `ZIndex` 기준 2차 정렬 키로 정렬해 `Actions[]`를 구성 — 같은 z_index Shot들이 항상 연속 블록이 되도록 구조적으로 보장(EAction ID 재배치가 다른 곳에서 인덱스로 참조되지 않는지 확인 필요 — 조사 결과 EAction ID는 이 루프 내에서만 재생성되며 레시피에 영속화되지 않음, `BatchRunService`의 `_selectedIndices`도 UI ListView `SelectedItems` 기반이라 재정렬 자체와 무관해 보이나 검증 권고).
- `StartSubset`을 쓰지 않고 진짜 스파스 실행을 지원하는 새 진입점을 만든다(D-01의 "새 그랩 메커니즘 금지" 원칙과 정면 충돌하므로 비권장, 마지막 대안).

**Warning signs:** 특정 레시피에서 z_index=1 요청 시 z_index=2에 속한 Shot까지 재촬영/재측정되고 TCP 응답에는 안 잡히는(측정은 수행되지만 `AggregateIndexFais`가 걸러냄) — 조용한 성능 낭비로만 보이고 기능 버그로는 안 보일 수 있어 발견이 어렵다.

### Pitfall 3: `TryGrabOrLoadFaiDualImages`의 라이브 이미지 무시 버그 (D-08, 재확인됨)

**What goes wrong:** `imageA`(PointROI 이미지) 경로 결정 로직(Action_FAIMeasurement.cs:456-463)이 `dualMeas.TeachingImagePath_Horizontal` 파일 존재 여부만 체크하고, 없으면 곧바로 `ShotParam.SimulImagePath`(디스크 파일)로 폴백한다 — **`ShotParam.HasImage`/`GetImage()`(이미 `EStep.DatumPhase`에서 grab되어 `ShotParam.SetImage()`로 저장된 라이브 이미지, ShotConfig.cs:244-245,354-380)를 전혀 확인하지 않는다.**

**Why it happens:** 이 메서드는 애초에 "정적 티칭 이미지 2장" 모델로 작성됐고, 라이브 grab 통합(EStep.Grab→DatumPhase 병합, 2026-07-21 shared-lighthandler-race 수정)이 나중에 별도로 이뤄지면서 DualImage 경로는 갱신되지 않았다.

**How to avoid:** `pathA` 산출 우선순위를 `ShotParam.HasImage` (라이브, 최우선) → `TeachingImagePath_Horizontal` (명시 경로) → `ShotParam.SimulImagePath` (폴백)으로 재정렬. `ShotParam.GetImage()`는 복사본을 반환하므로(`using` 패턴 권장, ShotConfig.cs:365-366 주석 참고) `imageA`로 그대로 대입 가능 — 단 `TryGrabOrLoadFaiDualImages`가 반환하는 `imageA`는 호출부(`TryExecuteMeasurement`, line 685-689)에서 `finally`로 Dispose되므로 소유권 이전 규약과 일치.

### Pitfall 4: `OfflineInspectMode`가 `DebugManualZTrigger`(수동 Z 테스트 브리지)까지 차단한다

**What goes wrong:** `ProcessTest`(Custom/SystemHandler.cs:208-211)는 `Setting.OfflineInspectMode == true`면 무조건 `$TEST`를 거부한다. `DebugManualZTrigger`(Custom/SystemHandler.cs:749-779, SIMUL/수동지그 z_index 테스트용 임시 브리지)도 내부적으로 `ProcessTest(testPacket)`를 그대로 호출하므로(773행) **동일하게 차단된다.**

**Why it happens:** 둘 다 같은 `ProcessTest` 진입점을 공유 — 의도적 설계("주의: ProcessPrep/ProcessTest 는 프로덕션 TCP 경로 — 시그니처/로직 변경 금지, 호출만 한다", 748행 주석)이지만 이 phase의 UAT 시나리오(크로스-Z 캡처, 저장 이미지 기반 재현)와 충돌 가능.

**How to avoid:** 이 phase의 크로스-Z 기능 UAT는 `OfflineInspectMode=false` 상태에서 `DebugManualZTrigger` 또는 실제 SIMUL_MODE TCP 시뮬레이터(`Test/mock_vision_client.py`)로 수행해야 한다. SIMUL_MODE 빌드에서는 `EStep.DatumPhase`/`Measure`의 이미지 취득이 이미 무조건 `ShotParam.SimulImagePath`를 쓰므로(Action_FAIMeasurement.cs:210-211 `#if SIMUL_MODE`) `OfflineInspectMode`를 켤 필요가 없다 — 오히려 켜면 `$TEST` 자체가 막힌다.

## Code Examples

### z_index → 매핑 Action 인덱스 조회 (신규, ComputeLastZIndex/FindShotByZIndex 패턴 미러)

기존 `FindShotByZIndex`(InspectionSequence.cs:303-327)는 첫 매칭 1개만 반환한다. D-01 실행 스코프 필터는 **여러 개**의 매칭 Shot이 있을 수 있으므로(Vision-Protocol-v1.0.md 예시: BOTTOM Idx1=14개 FAI, 이들은 보통 여러 Shot으로 나뉠 수 있음 — 실제로는 확인 필요, 현재 레시피 구조상 Shot:FAI = 1:N이라 Idx당 여러 Shot이 있을 수도 1개일 수도 있음) 목록을 반환하는 신규 헬퍼가 필요:

```csharp
// 신규 패턴 제안 — FindShotByZIndex(InspectionSequence.cs:303-327)의 다중 매칭 버전.
// Actions[] 인덱스(SequenceBase.Actions, protected)를 반환해야 하므로 SequenceBase 쪽에
// public 프로퍼티/접근자가 없다면 InspectionSequence 내부에서 Actions 순회 필요 —
// SequenceBase.Actions 접근 제한자 확인 필요(Open Question #3).
private List<int> FindActionIndicesByZIndex(int nZIndex) {
    var result = new List<int>();
    if (Actions == null) return result;
    for (int i = 0; i < Actions.Length; i++) {
        var faiAct = Actions[i] as Action_FAIMeasurement;
        bool bMatch = faiAct != null && faiAct.ShotParam != null
            && faiAct.ShotParam.OwnerSequenceName == Name
            && faiAct.ShotParam.ZIndex == nZIndex;
        if (bMatch) result.Add(i);
    }
    return result;
}
```

### ZIndexA/B 필드 추가 위치 (DualImageEdgeDistanceMeasurement.cs, D-04 근거)

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs:22-37 인근에 추가
// 기존 TeachingImagePath_Horizontal/_Vertical과 같은 Category("Image|DualImage").
// -1 = 미설정(정적 파일 경로 폴백, D-07). ParamBase.Load Int32 케이스가 키 부재 시 0으로 덮으므로
// MeasurementBase.Load override 필요(Pattern 1 참고).
[Category("Image|DualImage")]
[System.ComponentModel.Description("PointROI(에지 A) 라이브 캡처 z_index. -1=미설정(기존 정적 이미지 경로 사용).")]
[DisplayName("Point z_index (ZIndexA)")]
public int ZIndexA { get; set; } = -1;

[Category("Image|DualImage")]
[System.ComponentModel.Description("LineROI(에지 B) 라이브 캡처 z_index. -1=미설정(기존 정적 이미지 경로 사용).")]
[DisplayName("Line z_index (ZIndexB)")]
public int ZIndexB { get; set; } = -1;
```

### D-08 수정 스케치 (TryGrabOrLoadFaiDualImages, 우선순위 재정렬)

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:456-463 대체안
string pathA;
bool bHasLiveImage = ShotParam.HasImage; // 신규: EStep.DatumPhase가 이미 grab한 라이브 이미지 우선
if (bHasLiveImage) {
    // imageA는 이 메서드가 반환 — 호출부(TryExecuteMeasurement)가 finally에서 Dispose하므로
    // ShotParam.GetImage()의 복사본을 그대로 넘겨도 소유권 규약 일치.
    // (구체적 배선은 ZIndexA 값에 따라 "이 Shot 자신의 이미지"인지 "다른 z_index의 보관된 스냅샷"인지 분기 필요 — Open Question #4)
} else if (!string.IsNullOrEmpty(dualMeas.TeachingImagePath_Horizontal) && File.Exists(dualMeas.TeachingImagePath_Horizontal)) {
    pathA = dualMeas.TeachingImagePath_Horizontal;
} else {
    pathA = ShotParam.SimulImagePath;
}
```

## State of the Art

| Old Approach (연구 전 가정) | Current Approach (재조사 확정) | When Changed | Impact |
|--------------------------|-------------------------------|---------------|--------|
| "z_index↔Shot 매핑은 Phase 48의 ResourceMap이 제공"(phase 설명 문구) | `ResourceMap.cs`는 Site→Sequence/Action/Camera/Light **이름**만 매핑한다(TCP 어댑터 계층). 실제 z_index↔Shot 매핑은 `ShotConfig.ZIndex`(Phase 49, 49-01) + `InspectionSequence.FindShotByZIndex`/`ComputeLastZIndex`/`AggregateIndexFais`(Phase 49, 49-02)가 담당 — Phase 48이 아니라 Phase 49의 기여물. | 이번 세션 재확인 | planner는 "ResourceMap 확장" 방향으로 잘못 계획하지 않도록 주의 |
| "$TEST 1건 = 그 z_index Shot만 실행"이 이미 부분 구현됐을 가능성 | `ProcessTest`(Custom/SystemHandler.cs:200-220)는 지금도 **무조건 `seq.StartAll(packet)`** — z_index 무관 전체 Shot 실행. Phase 49의 z_index 필터링은 응답 집계(`AddResponseV1Cycle`→`BuildScopedResponse`) 레벨에서만 작동, 실행 레벨은 100% 미필터링 상태. | 2026-06-23(Phase 49) 이후 계속 이 상태 | D-01의 실행 스코프 gap은 CONTEXT.md 서술대로 진짜 존재 — 확인됨 |
| "EStep.Grab이 여전히 존재" | 2026-07-21 `shared-lighthandler-race` 수정으로 `EStep.Grab`은 도달 불가능한 방어적 폴백(Action_FAIMeasurement.cs:235-240)만 남고, 실제 grab은 `EStep.DatumPhase` 블록(81-233, `LightHandler.Handle.GrabSyncLock`으로 감쌈)에 병합됨. | 2026-07-21 | 신규 grab 호출(ZIndexA/B 라이브 캡처)은 이 병합 블록(94-229) 안, 같은 lock 범위에 있어야 함(CONTEXT.md 경고와 일치, 재확인됨) |

**Deprecated/outdated:** 없음 — 모든 기존 패턴이 현재도 유효.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `StartSubset` 재정렬 시 EAction ID 재배치가 다른 곳에서 인덱스 기반으로 참조되지 않는다(레시피 영속화 없음, BatchRunService의 UI 선택 인덱스와만 연관) | Pitfall 2 / Code Example 1 | 만약 어딘가 EAction enum 값이 영속 저장되거나 고정 인덱스로 참조된다면 Actions[] 재정렬이 다른 회귀 유발 — grep으로 낮은 확신 하에 확인, 완전한 negative-claim 검증은 못함 |
| A2 | 한 z_index당 여러 Shot이 매칭될 수 있다(1:N) — Vision-Protocol-v1.0.md의 "BOTTOM Idx1=14개 FAI"가 여러 Shot으로 나뉘는지, 단일 Shot 안에 14개 FAI가 있는지 확인 안 됨 | Code Example 1 | 만약 항상 1:1(Idx당 Shot 1개)이면 `FindActionIndicesByZIndex`가 다중 매칭 지원할 필요 없이 기존 `FindShotByZIndex` 확장만으로 충분 — 설계 단순화 가능 |
| A3 | `ShotParam.GetImage()` 반환 이미지가 ZIndexA/B 크로스-Z 캡처 시점의 "그 Shot 자신의" 이미지와 "다른 z_index Shot의 보관된 스냅샷" 두 케이스를 구분 없이 재사용 가능하다고 가정 | Code Example 3 | 실제로는 크로스-Z 저장소(D-02)가 이미지 자체가 아니라 "이미 추출된 에지 값"만 보관하는지, 원본 이미지까지 보관하는지가 설계상 갈림길 — Claude's Discretion 항목이라 planner가 결정해야 함, 여기서는 구조만 제시 |

## Open Questions

1. **[MOST CRITICAL] z_index=0(Datum) 요청 시 실행 스코프를 어떻게 처리할 것인가?**
   - What we know: Datum 검출은 독립 Shot이 아니라 모든 Action의 `EStep.DatumPhase`에 내장. `shot.ZIndex==0`인 Shot은 일반적으로 존재하지 않는다(실측 레시피 예시 SHOT_E5의 ZIndex=0은 "아직 미설정"인 것으로 추정 — Assumptions와 별개로 확인 필요).
   - What's unclear: (a) z_index=0에서 여전히 `StartAll`(전체 실행, 옵션 A)을 쓸지, (b) Datum-only 실행을 위해 Action_FAIMeasurement에 조건 분기를 추가할지(옵션 B, blast radius 큼), (c) 아니면 완전히 다른 제3의 설계.
   - Recommendation: **planner가 사용자에게 명시적으로 확인**받을 것. D-01의 "새 그랩 메커니즘 금지" + "minimal blast radius" 원칙을 감안하면 옵션 A(z_index=0은 전체 실행 유지)가 더 안전하지만, 그러면 D-01의 waste-elimination 목표가 index=0에는 적용되지 않는다는 트레이드오프를 사용자가 인지해야 한다.

2. **[CRITICAL] `StartSubset`의 min-max 연속구간 제약을 D-01 구현이 어떻게 만족시킬 것인가?**
   - What we know: 같은 z_index Shot들이 `Actions[]`에서 인접하다는 보장이 코드 어디에도 없다(Shot 추가 순서 = append-only, 재정렬 기능 없음).
   - What's unclear: 이 리스크를 (a) 런타임 검증+경고로 관측만 할지, (b) `RebuildInspectionActions`에 ZIndex 정렬을 추가해 구조적으로 보장할지.
   - Recommendation: planner가 (b)를 채택한다면 Assumption A1(EAction 인덱스 비영속) 검증을 실행 계획의 첫 verification 단계로 넣을 것.

3. **`SequenceBase.Actions` 배열의 접근 제한자 확인 필요**
   - What we know: `InspectionSequence`는 `SequenceBase`를 상속하며 `HandleRunStartResetResults`(InspectionSequence.cs:219-241)가 이미 `Actions`를 순회하는 것으로 보아 최소 `protected` 이상 접근 가능.
   - What's unclear: `EndActionIndex`/`CurrentActionIndex`가 `protected set`(SequenceBase.cs:44-45)인 것도 확인됐으나, z_index→ActionIndices 헬퍼가 `Actions.Length`에 안전하게 접근 가능한지 실제 컴파일로 재확인 필요(연구 단계에서는 소스만 확인, 컴파일 안 함).
   - Recommendation: 구현 첫 task에서 즉시 빌드로 검증.

4. **크로스-Z 저장소는 "값"을 저장하는가 "이미지"를 저장하는가?**
   - What we know: D-02는 "크로스-Z 값(Z1에서 찾은 에지 등)"을 보존한다고 명시 — "값"이라는 단어 선택.
   - What's unclear: `DualImageEdgeDistanceMeasurement`의 현재 알고리즘(TryExecute, DualImageEdgeDistanceMeasurement.cs:132-242)은 PointROI와 LineROI 두 에지를 **한 번의 TryExecute 호출 안에서 함께** projection_pl로 거리 계산한다 — 즉 "Z1에서 점 A를 찾아 저장 → Z2에서 점 B를 찾고 저장된 A와 함께 거리 계산"이라는 **2단계 실행 모델로 알고리즘 자체를 리팩토링**해야 하는지, 아니면 "Z1과 Z2 각각에서 이미지만 캡처해 저장했다가 완성 시점에 기존 TryExecute를 한 번에 호출"하는 것인지에 따라 구현 난이도가 크게 다르다. 후자(이미지 캡처만 지연)가 기존 `TryExecute` 알고리즘을 재사용할 수 있어 더 간단해 보인다.
   - Recommendation: planner가 "이미지 캡처를 완성 z_index까지 지연시키고, 완성 시점에 기존 TryExecute를 그대로 호출"하는 방식을 우선 검토할 것 — `RuntimeImageA`/`RuntimeImageB`(DualImageEdgeDistanceMeasurement.cs:121-128)에 "Z1에서 캡처해 보관해둔 이미지"와 "Z2에서 방금 캡처한 이미지"를 각각 주입하면 알고리즘 코드 변경이 0에 가까워진다. 이 경우 크로스-Z 저장소는 "값"이 아니라 "HImage(또는 그 바이트/복사본)"을 보관해야 하므로 D-02 서술("값")과 약간의 뉘앙스 차이가 있다 — 사용자 확인 권장.

5. **REQUIREMENTS.md에 이 capability에 대한 REQ-ID가 없다**
   - What we know: `.planning/REQUIREMENTS.md`의 v1.2 Traceability 표에 PROTO-01~06(Phase 48/49/50)은 있으나 "요구 3-2 교차-Z 측정"에 대응하는 REQ-ID는 없다. `Vision-Protocol-v1.0.md`도 이를 "신규 영역(기존 phase 밖)"으로만 서술.
   - What's unclear: 신규 REQ-ID(예: `PROTO-07`)를 추가할지, 기존 PROTO 계열에 편입할지, 아니면 REQUIREMENTS.md 밖의 별도 작업으로 유지할지.
   - Recommendation: **planner/사용자가 결정** — 이 연구는 결정하지 않는다(gap만 보고).

6. **`DatumConfig`의 `Load` 오버라이드 존재 여부 미확인**
   - What we know: `DatumConfig.cs:867-868`에 `TeachingImagePath`/`TeachingImagePath_Vertical` null 정규화 코드가 존재(어떤 메서드 안인지는 이번 조사에서 오프셋 900줄 이후를 못 봄 — `EnsurePerRoiDefaults` 같은 멱등 메서드일 가능성).
   - What's unclear: `DatumConfig`가 `ParamBase.Load`를 오버라이드해서 sentinel 복원 패턴을 이미 쓰고 있는지, 아니면 별도 정규화 메서드(`EnsurePerRoiDefaults` 등 호출 시점 의존)를 쓰는지 — ZIndexA/B의 -1 sentinel 복원을 어느 메서드에 얹을지 결정하려면 이 확인이 필요.
   - Recommendation: 구현 시작 전 `DatumConfig.cs` 867번 줄 주변 및 `Load`/`EnsurePerRoiDefaults` 전체를 재확인할 것(이 연구에서는 예산상 생략).

## Environment Availability

해당 없음 — 이 phase는 외부 툴/서비스/런타임 의존성을 추가하지 않는다(기존 Halcon 24.11, .NET Framework 4.8, HIK/Basler SDK 환경 그대로 사용). SIMUL_MODE 컴파일 심볼과 `OfflineInspectMode` 런타임 설정이 테스트 경로에 영향을 주는 부분은 Common Pitfalls #4에 기술.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | **없음** — 프로젝트에 xUnit/NUnit/MSTest 미설치(`packages.config` 확인, CLAUDE.md 명시). `Test/` 디렉터리는 Python mock TCP 클라이언트/서버 스크립트(`mock_vision_client.py`, `mock_vision_server.py`)만 존재. |
| Config file | 없음 |
| Quick run command | `msbuild DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build` (0 errors/0 new warnings = 최소 합격선, Phase 49 전례와 동일) |
| Full suite command | 동일 — 자동화된 assertion 기반 스위트가 없으므로 "빌드 PASS + 사람 UAT" 조합이 이 프로젝트의 검증 관례(Phase 49-02-SUMMARY.md의 UAT-1~7 패턴 참고) |

### Phase Requirement → Test Map

이번 phase에 formal REQ-ID가 없으므로(Open Question #5), CONTEXT.md의 결정(D-01~D-09) 단위로 매핑:

| Decision | Behavior | Test Type | Automated Command | 비고 |
|--------|----------|-----------|-------------------|------|
| D-01 | z_index=N `$TEST` → 매핑 Shot만 실행 | 수동 UAT (`DebugManualZTrigger` 또는 mock TCP client) | 없음(msbuild 빌드 검증만 자동) | z_index=0/1/2 각각 grab 횟수 로그 카운트 확인 |
| D-02/D-03 | 크로스-Z 상태 보존/리셋 | 수동 UAT | 없음 | Z1→Z2 두 번 `$TEST` 시퀀스 후 최종 측정값 확인 + 다음 부품 z=0 재수신 후 잔류값 없음 확인 |
| D-04/D-05/D-07 | ZIndexA/B 필드 + 하위호환 | 빌드 검증(그레드 확인) + 수동 UAT | msbuild | SHOT_E5(D:\Data\Recipe\FAI_1\main.ini) 기존 레시피로 회귀 0 확인 |
| D-08 | imageA 라이브 재사용 버그 수정 | 수동 UAT(로그 확인 — "파일에서 재로드" 로그가 라이브 grab 시 사라지는지) | 없음 | |

### Sampling Rate

- **Per task commit:** msbuild Debug/x64 빌드 PASS(0 errors, 0 new warnings)
- **Per wave merge:** 전체 재빌드 + Human UAT 후보 목록 작성(Phase 49-02-SUMMARY.md `## Human UAT Candidates` 섹션 형식 재사용)
- **Phase gate:** `/gsd:verify-work` 전 최소 1회 SIMUL_MODE 수동 z_index 시퀀스 UAT

### Wave 0 Gaps

- 자동화 테스트 프레임워크 자체가 프로젝트에 없음 — 신규 도입은 이 phase 범위 밖(QUAL-01/별도 검토 대상). 기존 관례(빌드 PASS + Human UAT 후보 목록)를 그대로 따를 것.
- `Test/mock_vision_client.py`가 z_index 파라미터를 이미 지원하는지 미확인 — UAT 준비 단계에서 확인 필요.

## Security Domain

`security_enforcement`는 config.json에 명시 없음(기본 활성) — 그러나 이 시스템은 폐쇄 산업망(PC1/PC2 TCP, 외부 인터넷 미연결) 내부 프로토콜이며 인증/세션 계층이 설계상 존재하지 않는다(기존 `LoginManager`는 UI 로그인 전용, TCP 프로토콜과 무관).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | TCP는 신뢰된 PC1/PC2 폐쇄망 — 인증 계층 없음(기존 설계, 이 phase가 바꾸지 않음) |
| V3 Session Management | No | Persistent TCP, 세션 개념 없음 |
| V4 Access Control | No | 해당 없음 |
| V5 Input Validation | Yes | z_index 파싱은 이미 `ParseCurrentZIndex`(InspectionSequence.cs:496-518)가 `int.TryParse` + 음수/미수신 정규화로 방어. ZIndexA/B도 동일하게 실행 시점 범위 검증(D-05) 필요 — 신규 코드가 이 패턴을 따를 것. |
| V6 Cryptography | No | 해당 없음 |

### Known Threat Patterns for 이 스택

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| 잘못된/악의적 z_index 값(범위 밖, 음수)으로 인한 배열 인덱스 예외 | Denial of Service | `ParseCurrentZIndex` 기존 가드 재사용(TryParse+음수 정규화) — ZIndexA/B도 동일 가드 필수, 예외 시 앱 크래시 방지(TCP 스레드 크래시 방지 원칙, `RunBottomAlign` 주석 T-65-06 참고) |

## Sources

### Primary (HIGH confidence — 이번 세션 직접 파일 읽기로 검증)

- `WPF_Example/Sequence/Sequence/SequenceBase.cs` (lines 44-45, 220-399) — StartSubset/StartCore/MainExecute 연속구간 실행 확인
- `WPF_Example/Custom/SystemHandler.cs` (lines 1-90, 195-232, 700-830) — ProcessTest/ProcessPrep/DebugManualZTrigger/ApplyPrepToSequences
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` (전체 178줄) — RebuildInspectionActions Shot→Action 순서 확인
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (전체 1142줄) — 크로스-Z 관련 전 메서드 재확인
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (전체 777줄) — EStep 상태머신, D-08 버그 재확인, lock 순서 재확인
- `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` (전체 245줄)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` (전체 158줄) — Load sentinel 패턴 원본
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (lines 1-50, 940-1014) — ImageSource 카테고리, ICustomTypeDescriptor hide 로직
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` (lines 17-58, 236-402) — ZIndex 필드, GetImage/HasImage
- `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` (전체 13줄)
- `WPF_Example/Sequence/Param/ParamBase.cs` (lines 290-410) — Int32 Load 리플렉션 확인
- `WPF_Example/TcpServer/ResourceMap.cs` (전체 227줄) — z_index 매핑 부재 확인(State of the Art 정정)
- `WPF_Example/TcpServer/VisionRequestPacket.cs` (lines 319-340, 533-540) — TestID/IndexNumber 필드
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` (lines 40, 47) — Shot append-only 확인
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (line 2012 근방) — DualImage 검증 메시지 위치
- `.planning/phases/68-.../68-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md`(1-201줄), `.planning/refs/Vision-Protocol-v1.0.md`, `.planning/refs/control-sequence-coding-guideline.md`, `.planning/phases/49-.../49-CONTEXT.md`+`49-01/02/03-SUMMARY.md`, `.planning/phases/37-.../37-CONTEXT.md`

### Secondary (MEDIUM confidence)

없음 — 이 연구는 전량 코드베이스 직접 읽기(Primary)로 수행됐다. 외부 웹 조사 불필요(내부 아키텍처 작업).

### Tertiary (LOW confidence)

- Assumption A2(z_index당 Shot 1:1 vs 1:N) — 실제 레시피 구조 통계를 직접 세지 않음, `Vision-Protocol-v1.0.md`의 "Idx1 BACK_LIGHT 14개(FAI)" 서술에서 유추만 함.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A — 신규 외부 의존성 없음
- Architecture: HIGH — 관련 파일 전체(InspectionSequence.cs, Action_FAIMeasurement.cs, SequenceBase.cs, SequenceHandler.cs)를 이번 세션에 직접 재확인, 라인번호 최신
- Pitfalls: HIGH (Pitfall 1,2,4) / MEDIUM (Pitfall 3, D-08 재확인이나 수정안은 미검증 스케치) — Pitfall 1/2는 코드 정독으로 직접 도출한 신규 발견(CONTEXT.md에 없던 내용), 구현 전 사용자 확인 강력 권장

**Research date:** 2026-07-21
**Valid until:** 이 phase 착수 전까지 유효(작업트리가 계속 변경 중 — "동일 세션 중 미커밋 변경 다수"라는 이번 요청의 전제와 일치, 실제 구현 시작 직전 관련 파일 재확인 권장, 특히 InspectionSequence.cs/Action_FAIMeasurement.cs)
