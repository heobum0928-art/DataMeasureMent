# Phase 68: Z축 교차(Cross-Z) Dual-Image 측정 지원 - Context

**Gathered:** 2026-07-21
**Status:** Ready for planning

<domain>
## Phase Boundary

프로토콜 요구사항 3-2("Z1 정보 보유 → Z2에서 측정", `.planning/refs/Vision-Protocol-v1.0.md`)를 구현한다. PLC가 물리 Z축을 이동시키고 서로 다른 두 z_index로 각각 `$TEST`를 보내면, 이 소프트웨어는 (1) 그 z_index에 매핑된 Shot만 실행해서 (2) 첫 번째 Z에서 찾은 값을 사이클 안에서 보존해뒀다가 (3) 두 번째 Z 처리 시점에 같이 계산해서 하나의 측정 결과(예: 두 에지 사이 거리)를 만든다. 이 패턴은 FAI 측정 레벨(DualImageEdgeDistanceMeasurement)과 Datum 레벨(VerticalTwoHorizontalDualImage, Side/Bottom 사용 중) 둘 다 대상이다. 물리 Z축 모터 제어는 범위 밖(PLC가 함, IAxisController 그대로 미구현 유지).

**In scope:**
- v1.0(UseProtocolV1) 경로에서 `$TEST(z=N)` 도착 시 그 z_index에 매핑된 Shot만 실행(재구성) — Phase 49 D-01이 이미 결정했으나 실행 레벨에서 실제로 구현되지 않았던 갭을 닫음
- InspectionSequence 레벨 크로스-Z 상태 보존(사이클 공유 저장소) + z_index=0 수신 시 자동 리셋
- `DualImageEdgeDistanceMeasurement`에 ZIndexA/ZIndexB 필드 추가, 라이브 캡처로 두 이미지 소스 결정
- `DatumConfig`(VerticalTwoHorizontalDualImage)에도 동일하게 ZIndexA/ZIndexB 추가 + 라이브 캡처
- 기존 static teaching 파일(TeachingImagePath_Horizontal/_Vertical) 경로는 ZIndexA/B 미설정 시 그대로 유지(하위호환)
- 발견된 기존 버그 수정: `DualImageEdgeDistanceMeasurement`의 imageA가 이미 grab된 라이브 이미지를 무시하고 항상 파일에서 재로드하는 문제 (`Action_FAIMeasurement.cs` `TryGrabOrLoadFaiDualImages`)
- TCP `$RESULT` 보고: 크로스-Z 측정 항목은 완성되는(두번째) z_index 응답에만 담김 — 기존 B/P/F 3-state 그대로 재사용, 신규 프로토콜 상태 불필요

**Out of scope:**
- 물리 Z축 모터 제어(IAxisController 실제 구현) — PLC가 담당, 이 프로젝트 범위 아님
- v2.6(기본 프로토콜, UseProtocolV1=false) 경로 — z_index 개념 자체가 없어 무관, 무수정
- `ApplyPrepToSequences`의 별개 결함(동일 nZIndex에 TOP/BOTTOM 둘 다 매칭 Shot이 있으면 나중 순회 시퀀스가 항상 이김) — `shared-lighthandler-race` 디버그 세션에서 이미 발견/기록됨, 별도 처리
- v1.0 NG 누적이 index 0 기준으로만 리셋되는 취약점(별도 MEDIUM 리스크 항목, `auto-mode-risk-audit-260721` 메모리 참고) — 이번 phase에서 상태보존 리셋 타이밍은 그 기존 리셋 지점(z=0)에 편승하지만, 그 리셋 자체의 취약점 수정은 범위 밖
- legacy `$LIGHT` 커맨드, 수동 UI grab, `ProcessAlignTest`(이더넷 비전) 경로의 레이스 — 별도(이미 문서화됨)
- PROTO-06 통신 회귀 시험 — 제어팀 동기화 후 별도

</domain>

<decisions>
## Implementation Decisions

### A. z_index 실행 스코프
- **D-01:** v1.0(UseProtocolV1) 경로에서 `$TEST(z=N)`, **N>=1(측정 Index)** 도착 시, 그 z_index에 매핑된 Shot만 실행하도록 **완전 수정**한다. v2.6/legacy 경로는 무수정(회귀 0). `SequenceBase`에 이미 존재하는 부분실행 기능(`StartSubset`/`StartCore(first,last)`)을 재활용해서 새 그랩 메커니즘을 만들지 않는다. 이건 Phase 49 D-01("`$TEST` 1건은 그 z_index에 매핑된 Shot/FAI 그룹만 검사한다")이 이미 결정했으나 응답 집계 레벨(`AggregateIndexFais`)에서만 구현되고 실행(grab) 레벨에서는 실제로 구현되지 않았던 갭을 닫는 것 — 부수 효과로 관련없는 Shot이 매 `$TEST`마다 불필요하게 재촬영되던 기존 낭비(조명/하드웨어 부담)도 같이 해결됨.
- **D-01a (research 확정, z_index=0 예외):** `$TEST(z=0)`(Datum)는 **기존처럼 `StartAll`(전체 Shot 실행) 그대로 유지**한다. 근거: Datum 검출(`EStep.DatumPhase`)은 독립된 Shot이 아니라 **모든 Action의 실행 안에 내장**되어 매번 재수행되는 phase라서(`Action_FAIMeasurement.cs:81-233`), `shot.ZIndex==0`에 매핑되는 Shot이 일반적으로 존재하지 않는다(실제로 TOP 시퀀스는 ZIndex=0 Shot이 아예 없음 — SHOT은 1,2,3으로 시작). z=0에 실행 스코프 필터를 억지로 적용하면 매칭 Shot이 0개가 되어 Datum 검출 자체가 멈추는 회귀가 발생하므로 이 경우만 명시적으로 예외 처리한다. waste-elimination(D-01의 목표)은 z>=1에만 적용되고 z=0에는 적용 안 됨을 인지하고 넘어감 — 더 완전한 waste 제거(Datum-only 실행 스킵 로직 추가)는 blast radius가 커서 채택 안 함.
- **D-01b (research 확정, StartSubset 연속구간 보장):** `SequenceBase.StartSubset`은 스파스 선택이 아니라 **min-max 연속 구간**만 실행한다(`SequenceBase.cs:374-386`) — 같은 z_index를 가진 Shot들이 `Actions[]` 배열에서 반드시 인접해야 D-01이 안전하게 동작한다. 현재 `SequenceHandler.RebuildInspectionActions`(Custom/Sequence/SequenceHandler.cs)는 `RecipeManager.Shots` 리스트 순회 순서 그대로(append 순서, ZIndex 무관) Action을 생성하므로 인접을 보장 못 한다. **`RebuildInspectionActions`가 Shot을 `ZIndex` 기준으로 정렬해서(같은 시퀀스 소유 Shot 내에서) `Actions[]`를 구성하도록 수정** — 같은 z_index Shot들이 항상 연속 블록이 되도록 구조적으로 보장한다. 구현 첫 task에서 `EAction` ID가 다른 곳에 영속/고정 인덱스로 참조되지 않는지(레시피 비영속 확인 필요) 빌드로 검증할 것.

### B. Z1→Z2 상태 보존
- **D-02:** 크로스-Z 저장소는 **InspectionSequence 레벨의 사이클 공유 저장소**(멤버 상태)에 보관한다. 신규 상태머신 클래스는 도입하지 않는다 — Phase 49 D-02(`_failedDatums`/`_datumTransforms`와 동일 lifecycle에 멤버 추가) 패턴을 그대로 재사용.
- **D-02a (research 확정, "값"이 아니라 "이미지"를 저장):** 저장소에 담는 건 계산된 값이 아니라 **그 z_index에서 캡처한 이미지(HImage 또는 복사본)** 다. Z1 처리 시점엔 이미지만 캡처해서 저장소에 넣어두고, Z2(완성 index) 처리 시점에 두 이미지(Z1 저장분 + Z2 방금 캡처분)를 `DualImageEdgeDistanceMeasurement.RuntimeImageA`/`RuntimeImageB`에 그대로 주입해서 **기존 `TryExecute` 알고리즘을 변경 없이 한 번에 호출**한다. 이유: 측정 알고리즘 자체(에지 검출+projection_pl 거리계산)를 "2단계 실행"으로 리팩토링하는 것보다 압도적으로 작은 변경 — 알고리즘 코드는 그대로 두고 이미지 주입 시점/소스만 크로스-Z 저장소로 바꾸는 것.
- **D-03:** 리셋 시점 = **z_index=0(사이클 시작) 수신 시 자동**. Phase 49 D-08("사이클 상태 자동 리셋 = `$TEST z_index=0` 수신 시")과 동일한 기존 리셋 지점에 편승한다. 별도 타이머/타임아웃 불필요 — Z2가 영영 안 와도(PLC 중단/스킵) 다음 부품의 z=0 도착 시 자동으로 깨끗한 상태로 시작되므로 누수 위험 낮음.
- **측정/Datum 객체 자체에 직접 저장하는 방식은 채택하지 않음** — 그 객체들은 레시피 전역에서 재사용되는 인스턴스라 사이클 경계에서 자동으로 비워질 명확한 훅이 없고, 다음 부품/사이클로 값이 새어나갈 위험이 있음.

### C. ZIndexA/ZIndexB 필드
- **D-04:** ZIndexA/ZIndexB 필드는 **측정 자체**(`DualImageEdgeDistanceMeasurement`)에 배치한다. 기존 `TeachingImagePath_Horizontal`/`TeachingImagePath_Vertical`과 같은 자리 — 일관성 유지. Shot에는 두지 않는다(Shot은 자기 자신의 `ZIndex` 하나만 가지는 기존 구조 유지).
- **D-05:** 잘못된 설정(ZIndexA==ZIndexB, 레시피에 존재하지 않는 index를 가리킴)은 **저장 시점이 아니라 실행(측정) 시점**에 걸러낸다. `DatumRef` 미해결 패턴(`MarkMeasurementDatumRefMissing`, `SkipReason.DATUM_REF_MISSING`)과 동일하게 명시적 NG(이유 로그 포함)로 처리 — `DatumConfig.SourceShotName`이 이름 불일치 시 조용히 `Shots[0]`으로 폴백하던 나쁜 습관은 반면교사로만 참고하고 그대로 베끼지 않는다.

### D. 스코프 경계 — Datum 레벨 + 레거시 + 관련 버그
- **D-06:** Datum 쪽(`VerticalTwoHorizontalDualImage`, Side/Bottom에서 이미 사용 중 — Phase 37 확인됨)도 **이번 phase에 같이 포함**한다. `DatumConfig`에도 ZIndexA/ZIndexB 추가 + `Action_FAIMeasurement.TryGrabOrLoadDualDatumImages`를 라이브 캡처로 교체. 측정 레벨과 근본원인이 동일(고정 static 파일 재사용)하므로 분리할 이유가 없음.
- **D-07:** 기존 레시피(ZIndexA/ZIndexB 미설정, static 파일 경로만 있는 경우)는 **100% 그대로 동작**한다(회귀 0) — ZIndexA/B가 비어있으면 기존 `TeachingImagePath_Horizontal`/`_Vertical` 파일 로드 경로를 그대로 사용. SHOT_E5(`D:\Data\Recipe\FAI_1\main.ini` SHOT_8) 같은 기존 레시피가 살아있는 실사용 예시.
- **D-08:** 이번 세션 중 발견된 별개 버그 — `DualImageEdgeDistanceMeasurement`의 imageA가 이미 grab된 라이브 이미지(`ShotParam.GetImage()`)를 무시하고 `ShotParam.SimulImagePath`에서 항상 파일로 재로드하는 문제(`Action_FAIMeasurement.cs:462` 근방 `TryGrabOrLoadFaiDualImages` + `:678` `TryExecuteMeasurement`) — **이번 phase에서 같이 수정**한다. 어차피 같은 코드를 만지는 작업이라 분리할 이유 없음.

### 코딩 규약
- **D-09:** `InspectionSequence.cs`/`Action_FAIMeasurement.cs`의 z_index 실행스코프·크로스-Z 상태 관련 신규/수정 코드는 `.planning/refs/control-sequence-coding-guideline.md`(LOCKED, Phase 49 D-10과 동일 적용) 준수 — 헝가리언 표기법 + `if/else if/else`만(삼항·null병합 금지) + 조건식 변수화 + 매직넘버 상수화 + 함수 30줄 초과 분리. `DualImageEdgeDistanceMeasurement.cs`/`DatumConfig.cs` 같은 순수 측정/데이터 클래스는 기존 CLAUDE.md 파일 스타일(카멜케이스 등) 유지 — 제어/프로토콜 코드만 이 지침 대상.

### Claude's Discretion
- 사이클 공유 저장소의 정확한 자료구조(예: `Dictionary<string, HImage>` 측정 식별자 키, D-02a 반영해 이미지 보관)와 필드명(헝가리언 접두사 준수 하에) — planner 재량. Dispose lifecycle(다음 사이클 리셋 시 저장된 HImage도 반드시 Dispose)을 D-03 리셋 로직에 포함할 것.
- `StartSubset` 호출을 어느 지점(`Custom/SystemHandler.ProcessTest` vs `SequenceHandler`)에 배선할지 — planner 재량.
- 신규 `SkipReason` 상수명(`ZINDEX_MISCONFIGURED` 등) — planner 재량.
- ParamBase INI 직렬화 시 ZIndexA/B 기본값(예: -1 = 미설정 sentinel) 및 Load 오버라이드 필요 여부 — `MeasCorrectionFactor` 패턴(`MeasurementBase.cs:143-155`) 그대로 미러링. `DatumConfig`는 `ParamBase` 직접 상속이라 자체 `Load` 오버라이드 필요 여부 구현 착수 시 재확인(`DatumConfig.cs` 867행 근방에 이미 유사 정규화 코드 존재 — 그 메서드에 편승 가능한지 확인).
- Datum/측정 두 곳에 ZIndexA/B를 각각 추가할지, 공유 헬퍼/인터페이스(`IZIndexPair` 류)로 추출할지 — 중복 최소화 관점에서 planner 재량.
- REQUIREMENTS.md에 이 capability의 REQ-ID(예: `PROTO-07`)를 신설할지 — 이번 연구가 gap만 보고, 결정은 planner 착수 시 사용자에게 재확인 권장.
- `SequenceBase.Actions`/`EndActionIndex`/`CurrentActionIndex` 접근 제한자가 z_index→ActionIndices 신규 헬퍼에서 실제로 접근 가능한지 — 구현 첫 task에서 즉시 빌드 검증.
- z_index당 Shot이 항상 1:1인지 1:N인지 실제 레시피 전수 확인은 안 됐음(TOP은 1,2,3 각각 별개 Shot으로 1:1처럼 보임) — `FindActionIndicesByZIndex`류 헬퍼는 다중 매칭을 지원하도록 안전하게 설계할 것(1:1이어도 손해 없음).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 프로토콜 규격 (canonical spec — 요구 3-2의 단일 진실원)
- `.planning/refs/Vision-Protocol-v1.0.md` — §검사 시퀀스(Index 기반 멀티샷, Light ON→Z이동(PLC)→`$TEST`→`$RESULT`) / §판정(P/F/B 3-state) / §"사용자 4개 신규 요구 ↔ 프로토콜 매핑" 3번("Z축 2위치 시퀀스" — 요구 3-2 "Z1 정보 보유→Z2에서 측정"은 신규 영역, 영향도 LARGE로 명시).
- `.planning/refs/control-sequence-coding-guideline.md` — LOCKED 코딩지침(D-09 근거). 제어/프로토콜 신규 코드에 CLAUDE.md보다 우선.

### Phase 49 토대 (이번 phase가 재사용/확장하는 결정)
- `.planning/phases/49-protocol-v1-judgment-engine/49-CONTEXT.md` — **D-01**(z_index 실행 스코프 원 결정, 이번 phase가 실제로 구현), **D-02**(멤버 상태 패턴, D-02 재사용), **D-08**(z_index=0 자동 리셋 시점, D-03 재사용). Deferred 섹션에 "교차-Z 측정(요구 3-2)"이 이 phase 범위로 명시적으로 이연되어 있음.
- `.planning/phases/49-protocol-v1-judgment-engine/49-01-SUMMARY.md`, `49-02-SUMMARY.md`, `49-03-SUMMARY.md` — 실제 구현 세부(AggregateIndexFais, ApplyShotLights, ECycleResult 등) 확인용.

### Datum dual-image 선례
- `.planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-CONTEXT.md` — Side 시퀀스에서 `VerticalTwoHorizontalDualImage` 사용 확인(D-06 근거). 좌표계/정렬 관련 기존 결정 확인용.
- `.planning/phases/36-datum-dualimage-coord-anchor-angle-validation-2026-05-28/36-CONTEXT.md` — Datum DualImage 좌표계 anchor/offset 설계(회귀 방지용 참고).

### 최근 관련 수정 (락 순서 준수 필요)
- `.planning/debug/shared-lighthandler-race.md` — 2026-07-21 완료된 수정. `LightHandler.Handle.GrabSyncLock`(항상 바깥) → `cam.GrabLock`(항상 안쪽) 락 순서 확립, `Action_FAIMeasurement.EStep.Grab`이 `EStep.DatumPhase`로 병합됨. **이번 phase가 새 grab 호출(ZIndexA/B 라이브 캡처)을 추가할 때 이 락 순서를 그대로 지켜야 함** — 새 grab 지점을 이 lock 범위 밖에 두면 동일 레이스 재발 위험.

### 실사용 레시피 예시
- `D:\Data\Recipe\FAI_1\main.ini` — `SHOT_8`(ShotName=SHOT_E5, OwnerSequenceName=BOTTOM, ZIndex=0) + `SHOT_8_FAI_0_MEAS_0`/`MEAS_1`(TypeName=DualImageEdgeDistance, 현재 static `TeachingImagePath_Horizontal`/`_Vertical` 사용) — 하위호환 검증 기준(D-07).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SequenceBase.StartSubset` / `StartCore(first, last, packet)` (`WPF_Example/Sequence/Sequence/SequenceBase.cs`) — 부분 실행 기능 이미 존재, D-01의 실행스코프 필터링에 그대로 재활용 가능.
- `InspectionSequence._failedDatums` / `_datumTransforms` (요청 간 캐시 멤버) — D-02 크로스-Z 저장소의 lifecycle 모델.
- `MarkMeasurementDatumRefMissing` / `SkipReason.DATUM_REF_MISSING` (`Action_FAIMeasurement.cs`) — D-05 명시적 NG 처리의 정확한 선례.
- `ParamBase` 리플렉션 INI 직렬화 + `MeasCorrectionFactor`류 Load 오버라이드 패턴(`MeasurementBase.cs`) — 신규 ZIndexA/B 필드의 하위호환 저장 방식.

### Established Patterns (반면교사 포함)
- `DatumConfig.SourceShotName`(다른 Shot을 이름으로 참조하는 기존 유일 선례) — 카메라/조명만 상속하고 이미지는 안 넘기며, 이름 불일치 시 조용히 `Shots[0]`으로 폴백 — **이 폴백 습관은 D-05에서 명시적으로 배제**.
- `LightHandler.Handle.GrabSyncLock`(항상 바깥) → `cam.GrabLock`(항상 안쪽) — 신규 grab 호출 추가 시 반드시 준수해야 하는 락 순서(2026-07-21 확립).
- `Action_FAIMeasurement.cs` `EStep.DatumPhase`가 datum 검출 + Shot 라이브 grab을 병합해서 수행(구 `EStep.Grab` 폐기) — 신규 크로스-Z grab 로직도 이 병합된 흐름 안에 위치시켜야 tick 경계 레이스를 피함.

### Integration Points
- `Custom/SystemHandler.cs` `ProcessTest` — v1.0 실행스코프 필터(D-01)를 배선할 진입점 후보.
- `InspectionSequence.cs` — 신규 크로스-Z 멤버 상태 + 기존 `ResetCycleState()`(z=0 트리거) 연동 지점.
- `Action_FAIMeasurement.cs` `TryGrabOrLoadFaiDualImages`(측정용) / `TryGrabOrLoadDualDatumImages`(Datum용) — ZIndexA/B 기반 라이브 캡처로 교체될 두 함수, 현재 둘 다 static 파일 로드만 함(라이브 grab 분기 없음).
- `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` — ZIndexA/B 프로퍼티 추가 지점(D-04).
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 동일하게 ZIndexA/B 추가 지점(D-06).
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — PropertyGrid 노출 + 기존 DualImage 검증 메시지(예: L2011 "세로축 티칭 이미지 경로가 비어 있습니다") 근처에 ZIndexA/B 관련 안내 추가 여지.

</code_context>

<specifics>
## Specific Ideas

- 사용자 확정 개념(대화 중 여러 차례 명확화): "에지 A는 Z1(포지션 A)에서 찾고, 에지 B는 Z2(포지션 B)에서 찾아서 하나의 거리로 측정 — 전부 동일 Shot(예: SHOT_E5) 안에서" — 별도 Shot 두 개로 쪼개는 설계가 아니라, 하나의 측정/Datum이 두 z_index를 참조하는 설계.
- 실사용 확인: SHOT_E5(BOTTOM)가 `D:\Data\Recipe\FAI_1\main.ini`에 실제로 존재하고 현재 static teaching 파일(가로/세로 각각 09:30/09:34 촬영, 메인 shot 이미지는 10:16 — 서로 다른 시각 = 갱신 안 되고 있다는 증거)을 쓰고 있음이 확인됨.
- 물리 Z 이동은 100% PLC/핸들러 책임. 이 소프트웨어는 `$PREP`(조명 세팅) → `$TEST`(grab 트리거) 신호만 받아서 반응 — `IAxisController`는 그대로 미구현 stub로 둠(범위 밖, 별도 Phase 대상).
- 사용자가 제어 입장(PLC)에서 "z1일 때 결과값이 안 나가면 헷갈리지 않을까"라고 우려했으나, 기존 프로토콜의 B(Buffer) 상태 + 인덱스별 부분 FAI 항목 보고(Datum 샷의 `B;0;@`처럼 항목 0개 응답도 기존에 이미 있음) 패턴으로 완전히 흡수됨 — 신규 프로토콜 변경 불필요, 이 크로스-Z 측정 항목은 완성되는 index(z2) 응답에만 담기면 됨.

</specifics>

<deferred>
## Deferred Ideas

- **`ApplyPrepToSequences` 결정론적 결함**(동일 nZIndex에 TOP/BOTTOM 둘 다 매칭 Shot이 있으면 나중 순회 시퀀스가 항상 이김, 레이스 아님) — `.planning/debug/shared-lighthandler-race.md`에 이미 기록됨. 이번 phase 범위 밖, 별도 세션.
- **v1.0 NG 누적 index-0-only 리셋 취약점**(`auto-mode-risk-audit-260721` 메모리 — 인덱스 누락/순서바뀜 시 stuck-NG 또는 silent PASS 가능) — 별도 MEDIUM 리스크 항목으로 이미 식별됨, 이번 phase는 그 위에 편승만 하고 근본 수정은 안 함.
- **PROTO-06 통신 회귀 시험** — 제어팀(김민우 선임) 동기화 후, Phase 50 범위.
- **legacy `$LIGHT` 커맨드/수동 UI grab/`ProcessAlignTest`(이더넷 비전) 레이스** — `shared-lighthandler-race`에서 의도적으로 범위 밖 처리됨, 계속 범위 밖.

</deferred>

---

*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Context gathered: 2026-07-21*
