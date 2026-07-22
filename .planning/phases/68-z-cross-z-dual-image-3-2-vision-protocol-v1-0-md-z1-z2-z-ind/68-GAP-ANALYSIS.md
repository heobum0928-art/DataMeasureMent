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

---

## 사후발견 — REGR-1: D-08 fix 자체의 회귀 (68-03 완료 후 UAT 중 발견, 2026-07-22)

**배경:** 68-03에서 구현한 D-08 버그수정(`TryGrabOrLoadFaiDualImages`, 커밋 `b28beca`)이 그 자체로 새 회귀를 도입했다. 68-03 완료 및 본 gap-analysis 작성 이후, 이 phase와 별개로 진행된 UAT 세션에서 발견됨.

**근본원인:** `b28beca`는 pathA(PointROI) 우선순위를 "`ShotParam.HasImage`(라이브 grab) 최우선 → `TeachingImagePath_Horizontal` → `SimulImagePath`" 순으로 재배치했다. 그러나 `EStep.Grab`(`Action_FAIMeasurement.cs:245-268`)은 SIMUL_MODE에서 항상, 그리고 non-SIMUL의 `SystemSetting.Handle.OfflineInspectMode`(이 사이트의 실제 운영 모드 — Z모터 없는 수동지그, `manual-jig-offline-inspect` 참고) 분기에서도 `LoadShotInspectionImage()` + `ShotParam.SetImage(image)`를 매 사이클 실행한다. 즉 `EStep.Measure` 시점엔 `ShotParam.HasImage`가 항상 `true`이므로, `b28beca`가 최우선으로 승격한 라이브 분기가 매번 이기고, 운영자가 측정별로 명시 지정한 `TeachingImagePath_Horizontal`은 이 사이트가 실제로 도는 두 모드 모두에서 도달 불가능한 죽은 코드가 된다.

**D-08 원래 의도와의 차이:** D-08(`68-CONTEXT.md`)의 실제 의도는 "명시 경로가 없을 때 이미 확보된 라이브 이미지를 무시하고 파일을 낭비적으로 재로드하는" 문제만 고치는 것이었다. 명시 경로를 override하려는 의도는 없었다 — 구현이 의도보다 한 단계 더 나갔다.

**증상 (UAT 중 실측):** DualImage 측정에서 `RuntimeImageA`/`RuntimeImageB`가 설정된 수평/수직 교시 이미지 쌍이 아니라 사실상 동일/오류 이미지로 귀결되어, 측정값이 산출되지 않음.

**수정:** `TryGrabOrLoadFaiDualImages`의 pathA 우선순위를 (1) `TeachingImagePath_Horizontal`(명시, 존재 시 최우선) → (2) `ShotParam.HasImage`/`GetImage()`(라이브, D-08 원 의도 보존) → (3) `ShotParam.SimulImagePath`(폴백) 순으로 재정렬. 우선순위 결정 로직은 `ResolveFaiImageASource` 헬퍼로 추출(제어 시퀀스 코딩 지침의 함수 30줄 제한 준수). `imageB`/`TeachingImagePath_Vertical`, 페어 Dispose 계약, cross-Z 캡처 경로(`ProcessCrossZCaptureTick`)는 무변경.

**커밋:** 이 회귀에 대해 사실상 동일한 수정이 두 건 연속 커밋되었다(동일 프롬프트의 병행 실행으로 추정) — `4198b1e`(1차, 3단 if/else-if/else 재정렬만) 이어서 `e429466`(최종 — 동일 재정렬 + `ResolveFaiImageASource` 헬퍼 추출로 30줄 지침 준수). 현재 HEAD 상태는 `e429466` 기준이며 기능적으로 올바름을 빌드+코드리뷰로 확인함. 두 커밋 모두 기록 보존(히스토리 재작성 안 함) — 정리가 필요하면 사용자 판단으로 별도 처리 권고.

**별개로 확인, 이번 수정 범위 아님:** cross-Z 캡처(`ProcessCrossZCaptureTick`)는 `TeachingImagePath_Horizontal`을 전혀 참조하지 않으므로 이 회귀와 무관 — 다만 `ShotParam.SimulImagePath`가 Shot당 고정 파일 1개뿐이라 SIMUL_MODE에서 ZIndexA/ZIndexB 두 시점에 구조적으로 서로 다른 이미지를 만들 수 없다는 기존 설계 갭(CROSS-1과는 별개)이 여전히 남아있음 — 사용자와 별도 논의 중.

---

## 사후발견 — GAP-4: SIMUL_MODE 크로스-Z role별 이미지 미구분 (측정 레벨, 수정 완료)

**배경:** REGR-1의 "별개로 확인" 각주에 남아있던 SIMUL_MODE 설계 갭을 사용자가 직접 디버거로 재확인(`imgA`/`imgB` 가 `TakeCrossZImageCopy` 로 취득한 시점에 동일 사진) 요청, 2026-07-22 별도 세션에서 수정.

**근본원인:** `ProcessCrossZCaptureTick`(`Action_FAIMeasurement.cs`, 측정 레벨 크로스-Z 캡처 tick)이 role A(ZIndexA tick)/role B(ZIndexB tick) 구분 없이 항상 `ShotParam.GetImage()` 를 호출했다. SIMUL_MODE 는 Shot 이 `SimulImagePath` 단일 고정 이미지만 가지므로(`EStep.Grab` 이 매 사이클 이 경로 하나만 로드), role A/B 가 물리적으로 서로 다른 Z 위치에서 촬영된 것처럼 동작해야 하는 이 기능의 전제가 SIMUL_MODE 에서는 구조적으로 성립할 수 없었다. 실장비(비-SIMUL)는 문제 없음 — PLC 가 실제 Z 를 이동시키므로 각 tick 의 라이브 grab 이 실제로 다르다.

**수정:** `LoadCrossZRoleImage(bool bIsRoleA, DualImageEdgeDistanceMeasurement dualMeas)` 헬퍼를 신설해 `ProcessCrossZCaptureTick` 의 `ShotParam.GetImage()` 호출을 대체. `#if SIMUL_MODE` 게이트 안에서만: role A → `dualMeas.TeachingImagePath_Horizontal`, role B → `dualMeas.TeachingImagePath_Vertical` (신규 필드 도입 없이 static DualImage 경로가 이미 쓰는 두 필드를 재사용)에서 로드. 경로 미설정/파일없음 또는 로드 실패 시 기존 `ShotParam.GetImage()` 라이브 폴백(회귀 0 — 이 두 경로를 설정 안 한 레시피는 기존과 동일하게 동작). 비-SIMUL 경로는 완전히 무변경(`#else` 분기, 항상 `ShotParam.GetImage()`).

**커밋:** `668ff9c` (`feat(68): SIMUL-mode per-role teaching images for cross-Z DualImage capture`)

**Datum 레벨 동일 갭 — 아직 미수정(범위 밖):** `TryGrabOrLoadCrossZDatumImages` → `CaptureAndStoreCrossZDatumImage` → `GrabOrLoadDatumImage` 경로도 동일한 한계를 가진다. `GrabOrLoadDatumImage(datum)` 는 `bIsRoleA` 인자 자체를 받지 않으므로 role A/B tick 모두 `datum.TeachingImagePath`(SIMUL 폴백 `ShotParam.SimulImagePath`)에서 동일하게 로드한다 — 즉 Datum 크로스-Z 도 SIMUL_MODE 에서 role A/B 가 항상 동일 이미지가 된다. 이번 수정 범위 밖(측정 레벨만 요청받음) — Datum 레벨까지 확장할지는 별도 결정 필요. 확장 시 `datum.TeachingImagePath`(role A)/`datum.TeachingImagePath_Vertical`(role B, `VerticalTwoHorizontalDualImage` 알고리즘이 이미 갖는 필드) 재사용이 유력한 설계.
