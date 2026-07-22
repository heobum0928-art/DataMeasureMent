---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
type: gap-analysis
source: multi-agent workflow (Ground → Design → Reconcile → Critique), 2026-07-22
status: needs-decisions
---

# Phase 68 Gap Analysis — Side (z=0,1=Datum / z=2+=측정) 배포 전 필수 수정

이 문서는 68-HUMAN-UAT.md 대화형 UAT 중 발견된 갭들을 10-agent 조사(Ground/Design/Reconcile/Critique)로
심화 검증한 결과다. 당초 3개 갭으로 시작했으나, 조사 결과 **최소 1개의 더 근본적인 구조적 버그**와
**권고 수정안 자체의 회귀 9건**이 추가로 발견되었다. 아래 순서대로 처리할 것을 권고한다.

## 우선순위 0 — 선행 필수 (이것 없이는 나머지가 전부 무의미)

### FIX-0: 사이클 리셋 타이밍 버그

**증상:** z=0에서 캡처한 크로스-Z Datum의 role A 이미지가, z=0 자신의 **응답을 만드는 시점**에 즉시 Dispose+Clear된다.

**근거:**
- `ResetCycleState()`(`InspectionSequence.cs:565-572`, `ClearCrossZImages()` 포함)는 `HandleDatumIndexResponse()`(`:775`) 안에서만 호출된다.
- `HandleDatumIndexResponse`는 `AddResponseV1Cycle`(`:754`)의 z==0 분기에서 호출되고, `AddResponse()`는 `SequenceBase.Finish()`(`SequenceBase.cs:459-465`) — 즉 **이번 z=0의 모든 Action 실행이 끝난 뒤** 호출된다.
- 결과: z=0 tick 동안 `StoreCrossZImage`로 저장된 role A 이미지가, 같은 z=0의 응답 생성 단계에서 바로 지워진다. z=1이 도착해도 합칠 role A가 없어 `TryTakeCompletedCrossZDatumImages`가 영원히 `bPending=true`를 반환한다.

**영향:** Side의 2위치 Datum은 이 버그 하나만으로 현재 코드에서 **구조적으로 검출 자체가 불가능**하다. GAP-1/2/3(아래)을 전부 고쳐도 이 버그가 안 고쳐지면 관측 가능한 동작 변화가 없다.

**수정 방향:** 사이클 리셋을 "z=0 응답 생성 시점"이 아니라 "z=0 `$TEST` 수신 즉시(그 tick의 Action 실행 시작 전)"로 이동. Phase 49 D-08 원문("z_index=0 수신 시 = 사이클 시작")의 "수신 시"를 문자 그대로 재해석하는 정정.

---

## 우선순위 1 — GAP-1: "존재하는 z_index" 판정 기준

**근본원인:** `DoesZIndexExistInRecipe`(`InspectionSequence.cs:669-672`)가 `FindShotByZIndex`(`:300-324`, `shot.ZIndex`만 매칭)에 위임된다. `DatumConfig.ZIndexA/ZIndexB`는 이 판정에 전혀 반영되지 않는다. Side(z=1을 own하는 Shot 없음)에서 `DoesZIndexExistInRecipe(1)=false` → `IsDatumZIndexMisconfigured`(`Action_FAIMeasurement.cs:840-843`)가 매 사이클 `ZINDEX_MISCONFIGURED`로 Datum을 하드 실패시킨다. **Side뿐 아니라 측정 레벨 크로스-Z도 동일 문제** — 68-HUMAN-UAT.md UAT 시나리오 2/2b용 SHOT_E5(ZIndexA=1/B=2, own ZIndex=0)도 이 판정에 걸려 시작조차 못 한다.

**권고안 (Critique 교정 반영):**
- `BuildDeclaredZIndexSet()`류 헬퍼로 "선언된 z_index 합집합"(Shot own ZIndex + 측정 ZIndexA/B + **Datum ZIndexA/B 신규 추가**)을 유니버스로 확장.
- **제거할 것:** 원래 권고안에 있던 규칙 "측정 완성 index(max(ZIndexA,ZIndexB))가 시퀀스 최대 shot.ZIndex를 넘으면 오설정" — 이건 **Plan 03 BLOCKER fix(완성 index는 shot.ZIndex와 독립)를 다시 깨뜨리는 blocking 회귀**로 Critique가 확인함. 이 규칙 없이 동일값/단일설정/미선언 3가지만 판정.
- GAP-2가 쓰는 "이 z_index가 크로스-Z Datum에 쓰이는가" 헬퍼와 **동일 소스 재사용 필수**(GAP-1/GAP-2를 별도로 구현하면 InspectionSequence.cs에 유사 순회 헬퍼가 4개로 늘어나 LOCKED 지침 위반).

**D-07 회귀 가드:** 신규 술어는 모두 `!= CROSS_Z_UNSET(-1)` 게이트를 최상단에 두어, ZIndexA/B 미설정 기존 레시피는 100% 기존 경로 유지.

---

## 우선순위 2 — GAP-2: 실행 스코프가 Datum 크로스-Z를 모름

**근본원인:** `FindActionIndicesByZIndex`(`:370-406`)는 `shot.ZIndex` + 측정 `ZIndexA/B`만 확인, `DatumConfig.ZIndexA/B`는 확인 안 함. Side z=1 → 매칭 0건 → `StartV1Scoped`가 `StartAll` 폴백 + 매 사이클 Error 로그. 근본적으로 "Datum 검출은 독립 Action이 아니라 모든 Action의 EStep.DatumPhase 안에 내장"되어 있어(`Action_FAIMeasurement.cs:88-226`), "z=1에 Datum만 캡처"를 표현할 실행 단위가 없다는 게 핵심 난점.

**권고안:** 최소 1개 Action(datum.SourceShotName의 Shot)만 실행하고, `EStep.DatumPhase` 이후 `EStep.Grab`/`EStep.Measure`를 건너뛰는 `IsDatumOnlyExecutionIndex` + `AdvanceAfterDatumPhase` 신설.

**필수 수정 (Critique blocking):**
- `IsDatumOnlyExecutionIndex`에 **`if (nZIndex == DATUM_Z_INDEX) return false;` 가드를 최상단에 명시** — 이게 없으면 Side의 z=0 자체가 "Datum 전용 index"로 오판되어 D-01a가 유지하기로 한 `StartAll` 전량 실행이 무너진다(원래 설계문에 이 가드가 빠져 있었음).

**남은 리스크(운영 규칙으로 관리):** 크로스-Z owning Shot에 **일반(비-크로스-Z) 측정이 같이 있으면**, 그 Shot이 own/ZIndexA/ZIndexB 세 시점 모두에서 실행되면서 일반 측정이 잘못된 물리 Z에서 재실행된다. `$RESULT`(와이어)는 오염되지 않지만(측정 완성-index 게이트가 보호), **cycle.json 스냅샷 + 저장 이미지 + 화면표시 + "Measurement failed" 에러로그가 매 사이클 오염/발생**한다. 회피책: 크로스-Z 측정은 전용 Shot에 격리 **하고 그 Shot의 own ZIndex를 완성 index(max)로 설정**(0으로 두면 z=0 StartAll에도 걸리고, v1.0에서 그 Shot의 일반 측정이 영원히 보고 안 됨 — UAT 지정 SHOT_E5가 정확히 이 상태이므로 실제 사용 전 확인 필요).

---

## 우선순위 3 — GAP-3: Datum 실패 "즉시 F"가 크로스-Z에 미적용

**근본원인:** `m_bCycleDatumFailed = DetectDatumFailure()`는 z==0에서 딱 1회 산출(`InspectionSequence.cs:778`)되고 재평가 없음. 크로스-Z Datum의 실제 검출은 완성 index(Side는 1)에서 일어나므로 z=0 시점엔 항상 미실패. z=1에서 실패해도 "즉시 F" 계약이 이행 안 됨.

**권고안:** `GetDatumCompletionZIndex`(측정 레벨 `GetMeasurementCompletionZIndex`와 동일 패턴)를 Datum에 도입, **완성 index의 응답 생성 시점**(기존과 동일하게 모든 Action DatumPhase 종료 후 = AddResponse 시점)에 실패를 평가.

**필수 수정 (Critique blocking):**
- 신규 `m_bImmediateFailSent` latch를 **기존 `BuildDatumShotResponse`의 z==0 즉시-F 분기(`:716-722`)에도 반드시 세팅**하도록 통일할 것 — 원래 권고안은 신규 중간-index 분기에서만 세팅해서, z=0에서 이미 F가 나간 뒤 후속 index가 계속 오면(프로토콜 위반이지만 코드가 막지 않음) **한 사이클에 F가 2번 나갈 수 있는 blocking 회귀**가 Critique에서 확인됨.

**⚠️ 코드만으로 결정 불가 — 제어팀 협의 필수:**
`Vision-Protocol-v1.0.md`는 "Datum(**Index 0**) 실패 시 즉시 F"라고 명시하며, Index Table 예시(SIDE_1/SIDE_2)도 전부 Datum 단일위치(Idx0)로만 기록되어 있다. **"2위치 Datum"(z=0,1 모두 Datum) 레이아웃 자체가 현재 프로토콜 문서에 없는 케이스**다. z=1(비-0 index)에서 F를 보내는 것을 PLC가 실제로 올바르게 해석하는지 코드/문서 어디에도 근거가 없다. **이건 엔지니어링 선택이 아니라 프로토콜 문서 개정 + 제어팀(PLC) 합의 사안**이다. 합의 전까지는 SystemSetting 플래그로 게이팅해 기본 OFF로 배포하거나 Side를 v1.0 경로에서 잠정 제외하는 것을 권고.

---

## 교차 이슈 — 세 갭을 다 고쳐도 남는 것

### CROSS-1: 크로스-Z Datum 기반 측정이 영원히 무보정(identity)으로 계산됨

FIX-0/GAP-1/2/3을 전부 적용해도, z=1에서 Datum 검출이 성공해도 **z=2 측정 시점에는 그 transform이 이미 사라져 있다**:
`ClearDatumTransforms()`(`Action_FAIMeasurement.cs:93`)가 **매 Action의 EStep.DatumPhase 진입마다** `_datumTransforms`를 전부 비우고, 크로스-Z Datum은 z=2 tick에서 `bRelevant=false`(자기 역할 아님)로 재검출을 스킵한다 → transform 없음 → `ResolveDatumTransform`이 identity로 조용히 폴백 → **무보정 측정이 정상 측정처럼 P/F를 낸다.**

**수정 방향:** `TryGrabOrLoadCrossZDatumImages`의 `!bRelevant` 조기반환을, "양 role 이미지가 저장소에 이미 있으면 그걸로 결정론적 재검출"로 바꿔 모든 소비 index에서 transform/실패신호가 복원되도록 한다. 비-크로스-Z Datum의 "매 index 재검출" 동작(회귀 0 대상)은 그대로 유지.

**이 수정 없이 GAP-3만 머지하면 "즉시 F"라는 반쪽 안전장치만 생기고, 핵심 기능(보정된 측정)은 여전히 깨진 채로 남는다.**

### CROSS-2: "마지막 Index" 진실원이 완성 index와 독립적

`ComputeLastZIndex`(`:268-295`, shot.ZIndex 최댓값만 봄)와 `GetMeasurementCompletionZIndex`(`:807-816`, shot.ZIndex와 무관하게 max(ZIndexA,ZIndexB) 반환 가능)가 서로 무관하다. 크로스-Z 완성 index가 시퀀스의 최대 shot.ZIndex를 넘으면 **한 사이클에 P/F가 2회 송신될 위험**이 있다 — Side 특수 레이아웃과 무관하게, 크로스-Z 측정이 하나만 있어도 발생 가능한 일반적 위험. 별도 항목으로 등록 — ComputeLastZIndex를 max(shot.ZIndex 전체, 모든 크로스-Z 완성 index)로 확장할지 결정 필요.

---

## 처리 순서 권고

1. **FIX-0**(리셋 타이밍) — 다른 모든 것의 전제조건
2. **CROSS-1**(Datum transform 수명) — GAP-2/3과 같은 wave에서 반드시 동반
3. **GAP-1 + GAP-2** — 같은 플랜/웨이브(헬퍼 공유 필수, 완성-index 오설정 규칙 제외)
4. **CROSS-2**(마지막 Index 진실원) — GAP-1/3과 조율 필요
5. **GAP-3** — 코드 수정은 준비하되, **"즉시 F" 부분은 제어팀 협의 완료 전까지 게이팅**
6. 전체 완료 후 68-05 UAT 재개(SHOT_E5의 own ZIndex 설정도 함께 재확인 — 혼합 Shot 오염 회피)
