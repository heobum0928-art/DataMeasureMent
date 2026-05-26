# Phase 33 Context — Side/Bottom InspectionSequence 마이그레이션

**Gathered:** 2026-05-26
**Status:** Ready for research and planning
**Discuss mode:** Interactive (4 회색지대 deep-dive)

<domain>
## Phase Boundary (ROADMAP에서 lock)

**Goal:** Side / Bottom 카메라 시퀀스를 레거시 `TopSequence` / `BottomSequence` 에서 신규 `InspectionSequence` 로 마이그레이션 — Side/Bottom 에서도 Multi-Datum + Dynamic FAI 구조 사용 가능하게 한다.

**Background (사용자 보고 2026-05-26):**
- "Side 와 Bottom 쪽 datum 형성이 안됨"
- 코드 조사 결과 `SequenceHandler.RegisterSequences()` 가 Top 만 `InspectionSequence` 사용, Side/Bottom 은 레거시 `TopSequence` / `BottomSequence` 사용 → DatumConfigs 필드 부재 → 구조적 결함

**아키텍처 컨텍스트 (사용자 명시 2026-05-26):**
- **현재 (2 PC):** PC1 = Top + Bottom 검사 / PC2 = Side1 + Side2 검사
- **향후 (4 PC):** PC1 = Top 전용 / PC2 = Bottom 전용 / PC3 = Side1 전용 / PC4 = Side2 전용
- Phase 33 은 **현재 2 PC 구조에 충실** — Multi-PC 호환과 Side1/Side2 분리는 Phase 27 으로 이연

**Success Criteria:**
1. Side / Bottom 시퀀스가 Datum + Multi-Datum + Dynamic FAI 구조 사용 가능
2. Side / Bottom 노드에서 Datum 추가 → 티칭 → FAI 측정이 정상 작동
3. INI Save/Load 라운드트립 유지 (Side/Bottom 기존 recipe 호환)
4. Top 검사 동작 회귀 0 (수동 검증)
5. msbuild Debug/x64 PASS, 신규 warning 0

</domain>

<locked_requirements>
## Requirements (이전 작업에서 lock)

- 회색지대 ①: Phase 33 스코프 = **(B) 표준** — Side/Bottom 마이그레이션 (현재 2 PC 구조 충실). Multi-PC 호환과 Side1/Side2 분리는 Phase 27 이연.
- 회색지대 ②: Bottom 도메인 처리 = **보존 + Datum 테이블 추가** (실 운영 중 가정).
  - BottomSequence 의 Multi-Die / Picker / Calibration 도메인 로직 보존
  - DatumConfigs 필드 + TryRunDatumPhase 만 추가
  - BottomInspectionAction 의 Multi-Die 처리 흐름 유지하되 DatumPhase 단계 추가
- 회색지대 ③: TCP 응답 형식 = **FAIResults[] (가변 길이) 통일**.
  - Top 의 InspectionSequence 패턴을 Side/Bottom 도 따름
  - Bottom Multi-Die 결과를 여러 FAI 로 변환 매핑 필요
  - 호스트가 새 형식 수용한다는 전제
- 회색지대 ④: 레거시 처분 + INI 하위호환 + Top 회귀 방지
  - **TopSequence / BottomSequence / TopInspectionAction / BottomInspectionAction 클래스 = Deprecate (파일 유지, instance 생성 안 함)**
  - **INI 자동 마이그레이션**: InspectionRecipeManager.LoadPhase6Format 가 기존 Side/Bottom INI 를 새 구조로 매핑
  - **Top 회귀 방지**: Phase 33 변경이 Top 의 InspectionSequence 인스턴스 / Action_FAIMeasurement / InspectionRecipeManager 동작에 영향을 주지 않도록 가드. Top 시퀀스 1자 변경 금지. 사용자가 Top 검사 SIMUL 1회 수동 검증으로 사인오프 시점에 확인.

</locked_requirements>

<decisions>
## Implementation Decisions

### D-01: Side/Bottom 시퀀스 클래스 교체 위치
**Decision:** `SequenceHandler.RegisterSequences()` L30-34 에서 인스턴스 생성을 교체.
```csharp
// 변경 전:
new TopSequence(ESequence.Side, ...)
new BottomSequence(ESequence.Bottom, ...)

// 변경 후:
new InspectionSequence(ESequence.Side, ...)
new InspectionSequence(ESequence.Bottom, ...)
```
**Why:** 가장 작은 변경 surface. TopSequence/BottomSequence 클래스 정의는 유지 (Deprecate), instance 생성만 차단.

### D-02: Bottom 도메인 보존 전략
**Decision:** Bottom 의 Multi-Die / Picker / Calibration 도메인을 보존하는 방식 = **InspectionSequence + 보조 컨텍스트**.
- InspectionSequence 로 교체 후, Bottom 의 Multi-Die 검사 결과를 InspectionSequence 의 Shot-FAI 구조로 매핑
- 각 Die = 1 FAI (또는 1 Shot 의 N FAI 그룹)
- Picker 정보는 FAI Context 의 metadata 로 보존
- Calibration 은 별도 Action (Action_BottomCalibration) 으로 유지 — InspectionSequence 흐름과 독립

**Alternative considered:** BottomSequence 를 보존하면서 DatumConfigs 만 mixin → 거절 사유: TCP 응답 형식 통일 (회색지대 ③) 와 충돌. Multi-Die 결과를 FAIResults[] 로 매핑해야 함.

### D-03: TCP 응답 매핑 — Bottom Multi-Die → FAIResults[]
**Decision:** Bottom 의 BottomDie Dictionary 의 각 entry 를 FAIResultData 로 변환.
- FAIName = `"Die_{index}"` 또는 사용자 정의 이름
- IsPass = Die.Judgment
- MeasuredValue = Die.CenterOffsetXmm (또는 다른 핵심 metric — 호스트 스펙 확인 필요)
- 향후 FAIResultData 에 X/Y/Angle 별도 필드 확장이 필요할 가능성

**Open question (planner 가 결정):** FAIResultData 구조가 X/Y/Angle 까지 표현 가능한지 vs 확장 필요한지.

### D-04: INI 하위호환 마이그레이션
**Decision:** `InspectionRecipeManager.LoadPhase6Format` 가 기존 Side/Bottom INI 를 자동 인식하고 새 구조로 매핑.
- 레거시 INI 식별: 특정 키 존재 (예: `[BOTTOM_DIE]`, `Picker_X[0]=` 등) → 레거시 모드
- 매핑 규칙: 레거시 BottomDie entry → ShotConfig + FAIConfig 변환
- 신규 INI 형식이 기본 — Save 시 항상 신규 형식으로 저장 (one-way migration)
- 사용자가 레거시 INI 로드 후 Save 시 자동으로 신규 형식 전환

**Edge case:** 레거시 INI 의 일부 필드 (예: ScreenCenter_X/Y) 가 신규 구조에 맞지 않으면 SystemSetting 또는 별도 메타데이터 로 분리.

### D-05: 레거시 클래스 Deprecate 표시
**Decision:** TopSequence / BottomSequence / TopInspectionAction / BottomInspectionAction 클래스에 `[Obsolete("Phase 33 — InspectionSequence/Action_FAIMeasurement 로 마이그레이션됨", false)]` 어트리뷰트 추가.
- 파일 유지 — git history 보존 + 향후 reference
- 컴파일 경고 발생 — 코드 어디서든 instance 화 시도하면 warning
- `false` parameter — error 아닌 warning (강제 break 회피)

**Alternative considered:** 파일 즉시 삭제 → 거절 사유: git blame / 향후 reference 가치 + Phase 27 의 Side 도메인 재사용 가능성.

### D-06: Top 회귀 방지 가드
**Decision:** 다음 파일은 Phase 33 에서 1자도 변경 금지:
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (Top 도 사용 중)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (Top 도 사용 중)
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — **단 한 부분만 예외**: LoadPhase6Format 의 레거시 INI 마이그레이션 분기 신규 추가 (D-04). 기존 신규 형식 로드 path 는 byte-identical.

**Verification 단계:** 사용자가 Phase 33 sign-off UAT 시 Top 검사 1회 SIMUL 실행 → 기존 동작과 동일 확인.

### D-07: Action 단의 DatumPhase 통합
**Decision:** Bottom 도 Action_FAIMeasurement 의 EStep.DatumPhase 패턴 사용.
- BottomInspectionAction 폐기 + Action_FAIMeasurement 로 일원화 시도
- 단, Multi-Die 검사 흐름은 별도 EStep (예: EStep.MultiDieInspect) 으로 추가
- 또는 Action_FAIMeasurement 의 Measure 단계가 Shot.FAIList 를 순회할 때 Multi-Die 데이터를 받아 처리

**Open question (researcher 가 조사):** Multi-Die 검사가 Shot-FAI 동적 구조에 자연스럽게 매핑되는지 vs Bottom 전용 Action 가 필요한지.

### Claude's Discretion (담당자가 결정)
- DatumPhase 가 BottomInspectionAction 의 어느 EStep 사이에 삽입될지 (Init / Grab / 기존 Inspect 단계 사이)
- 레거시 INI 의 fallback 정책 (예: 빈 필드는 기본값, 명백히 깨진 INI 는 사용자 경고)
- IsDynamicFAIMode 와 Side/Bottom 의 상호작용 (현재 IsDynamicFAIMode 는 UI 토글 — Side/Bottom 도 동일하게 사용 vs 자동 활성화)
- InspectionSequence 의 OnLoad / OnCreate 가 Side/Bottom 의 default light/camera 와 맞물리는지 검증

</decisions>

<specifics>
## Specific Code References

### 현재 코드 상태 (스카웃 결과 2026-05-26)

**SequenceHandler.cs L30-34 — 변경 대상:**
```csharp
new InspectionSequence(ESequence.Top, SEQ_TOP, Top_Alg_Index, DeviceHandler.CAMERA_TOP, LightHandler.LIGHT_TOP),
new TopSequence(ESequence.Side, SEQ_SIDE, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_SIDE),       // ← 변경
new BottomSequence(ESequence.Bottom, SEQ_BOTTOM, Bottom_Alg_Index, DeviceHandler.CAMERA_BOTTOM, LightHandler.LIGHT_BOTTOM)  // ← 변경
```

**SequenceHandler.cs L37-43 — Action 매핑 검토 필요:**
```csharp
new TopInspectionAction(EAction.Top_Inspection, ACT_INSPECT, Top_Alg_Index, Inspection_Model_Index),
new TopInspectionAction(EAction.Side_Inspection, ACT_INSPECT, Side_Alg_Index, Inspection_Model_Index),    // ← 검토
new BottomInspectionAction(EAction.Bottom_Inspection, ACT_INSPECT, Bottom_Alg_Index, Inspection_Model_Index)  // ← 검토
```

### 보존 대상 (변경 금지 — D-06)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (Top 사용)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (Top 사용)

### 보존 대상 (Deprecate — D-05)
- `WPF_Example/Custom/Sequence/Top/Sequence_Top.cs` (TopSequence 클래스)
- `WPF_Example/Custom/Sequence/Bottom/Sequence_Bottom.cs` (BottomSequence 클래스)
- `WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs` (TopInspectionAction)
- `WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs` (BottomInspectionAction)

### 도메인 차이 매트릭스

| 측면 | InspectionSequence | TopSequence | BottomSequence |
|------|--------------------|-----------|----------------|
| Param 타입 | InspectionMasterParam | CameraMasterParam | CameraMasterParam |
| DatumConfigs | ✅ List<DatumConfig> | ❌ | ❌ |
| RecipeManager | ✅ Shot-FAI 동적 | ❌ | ❌ |
| Context 복잡도 | Generic | 단순 (CenterOffset/Angle) | 복잡 (Multi-Die/Picker/Cal) |
| TCP 응답 | FAIResults[] | 단일 X/Y/Angle | visionResults[10] Multi-Die |
| Action LOC | 290 | 300 | 526 |

</specifics>

<canonical_refs>
## Canonical References

- **Phase 21 SIGNED_OFF** (2026-05-11): InspectionRecipeManager + ShotConfig lifetime 계약 lock (변경 금지)
- **Phase 22 SIGNED_OFF** (2026-05-11): DatumConfig.TeachingImagePath INI 직렬화 lock (변경 금지)
- **Phase 23 SIGNED_OFF** (2026-05-19): EdgeToLineDistance + GrabOrLoadDatumImage TeachingImagePath 분기 lock
- **Phase 23.1 SIGNED_OFF** (2026-05-19): 동적 FAI ROI 티칭 배선 lock
- **Phase 27 (planned)**: Side Inspection 확장 (LineToLineAngle + Side Fixture INI + PC2 분리 + Datum 2-image)
  - Phase 33 → Phase 27 인계 사항: Side1/Side2 분리, Multi-PC 호환 셋팅, Datum 2-image 구조

</canonical_refs>

<deferred>
## Deferred Ideas (Phase 33 스코프 밖)

### Phase 27 (Side Inspection 확장) 으로 이연
- **Side1 / Side2 분리** — 현재 ESequence.Side 1개 → 향후 ESequence.Side1, ESequence.Side2 분기
- **Multi-PC 호환 셋업** — Setting.ini 의 ActiveSequences 명시 / PC 식별 / 활성 시퀀스 동적 등록 구조
- **PC2 (Side) 전용 구성 분리** — 별도 process / 별도 Setting / 별도 TCP port
- **Side Fixture INI** — Side 전용 INI 구조 (D1/H5 측정 항목)
- **LineToLineAngle 측정 타입** — Datum A vs 직선 각도
- **Datum 2-image 구조** — DatumConfig.Line1/2_SourceShotName + TryFindDatum 다중 이미지 오버로드

### v1.2 또는 별도 phase 이연
- 호스트 시스템과의 TCP 프로토콜 스펙 검증 / 협의 — Phase 33 plan 단계에서 결정 권한 위임
- 레거시 INI 자동 마이그레이션 도구 (CLI 또는 UI) — 자동 변환이 어려운 케이스의 사용자 수동 도구
- 레거시 클래스 파일 완전 삭제 — Phase 33 sign-off 후 별도 quick fix 또는 v1.2 정리

</deferred>

<scope_guardrail_compliance>
## Scope Guardrail 준수

이 phase 는 "Side / Bottom 시퀀스에 Datum + Multi-Datum + Dynamic FAI 사용 가능하게 한다" 라는 ROADMAP-locked 목표를 명확히 함. 다음 시도는 거절됨:

- ❌ Side1 / Side2 분리 → Phase 27
- ❌ Multi-PC 호환 셋업 → Phase 27
- ❌ Datum 2-image 구조 → Phase 27
- ❌ LineToLineAngle 측정 → Phase 27
- ❌ 호스트 TCP 프로토콜 변경 → Phase 33 plan 시 결정

</scope_guardrail_compliance>

---

**Next steps:**
1. `/gsd-research-phase 33` 또는 `/gsd-plan-phase 33` — Plan 작성
2. Plan 의 권장 구성 (3 plans):
   - 33-01: SequenceHandler 교체 + InspectionSequence 인스턴스 생성 (Side/Bottom)
   - 33-02: Bottom Multi-Die → FAI 매핑 + TCP 응답 변환 + INI 자동 마이그레이션
   - 33-03: SIMUL UAT (Side Datum / Bottom Datum / Top 회귀 / msbuild) + sign-off
