---
phase: 33-side-bottom-inspectionsequence-migration
status: partial
created: 2026-05-26
updated: 2026-05-26
total: 5
passed: 1
pending: 0
blocked: 2
not_tested: 2
---

# Phase 33 UAT — Side/Bottom InspectionSequence 마이그레이션

## Test 1 — msbuild Debug/x64 Rebuild
- **Result**: PASS (자동)
- **Detail**:
  - exit code 0, 0 Error(s)
  - 신규 warning 5건 모두 **CS0618 (의도된 deprecation)**:
    - Sequence_Top.cs:19 'TopSequence' deprecated (TopSequenceContext 내부 참조)
    - Sequence_Bottom.cs:30 'BottomSequence' deprecated (BottomSequenceContext 내부 참조)
    - SequenceHandler.cs:41 'TopInspectionAction' (RegisterActions placeholder)
    - SequenceHandler.cs:42 'TopInspectionAction' (RegisterActions placeholder Side)
    - SequenceHandler.cs:43 'BottomInspectionAction' (RegisterActions placeholder Bottom)
  - 기존 카테고리 baseline 유지 (MSB3884 ruleset, CS0162 unreachable in VirtualCamera.cs:266)
  - 신규 warning category = CS0618 외 0건

## Test 2 — Side 시퀀스 Datum + FAI SIMUL
- **Result**: BLOCKED
- **Detail**:
  - Side 시퀀스가 InspectionSequence 인스턴스로 등록됨 (구조적 검증 ✓)
  - Datum 노드 클릭 → 이미지 로드 (Z=1) → ROI 티칭 → 검사 실행 시 **Datum 위치 검출 실패**
  - 의심 원인: CO-33-02 이미지 갱신 회귀 또는 Bottom/Side 신규 흐름의 parentSeq 분기 갭
- **Carry-over**: → Phase 35 (Side/Bottom 실제 검사 UAT + 디버깅)

## Test 3 — Bottom 시퀀스 Datum + FAI SIMUL
- **Result**: BLOCKED
- **Detail**:
  - 사용자 보고: Datum Z=1 + Shot Z=2 이미지 로드 후 검사 시 Datum 위치를 못 찾음
  - 작업 순서 사용자 확인: Datum 노드 선택 → 이미지 로드(Z=1) → ROI 티칭 → 검사 → 실패
  - 추가 증상: Shot 노드에서 이미지 로드 후 Measurement 노드 이동 시 다른 이미지 표시 (CO-33-02)
- **Carry-over**: → Phase 35 (Side/Bottom 실제 검사 UAT + 디버깅)

## Test 4 — Top 회귀 SIMUL
- **Result**: NOT_TESTED
- **Detail**: 사용자 시간 제약 — Phase 35 에서 함께 수행 예정
- **D-06 자동 가드**:
  - `git diff` 확인: `InspectionSequence.cs` / `Action_FAIMeasurement.cs` / `VisionResponsePacket.cs` **변경 0 라인** ✓
  - SequenceHandler.cs L31 Top 라인 byte-identical 보존 ✓
  - InspectionRecipeManager.cs L113-124 (Top `[FIXTURE]` Save 블록) byte-identical 보존 ✓
  - InspectionRecipeManager.cs L181-194 (Top `[FIXTURE]` Load 블록) byte-identical 보존 ✓
  - 코드 레벨로 Top 회귀 0 가드 충족, SIMUL 실측 검증은 Phase 35 에서 수행

## Test 5 — INI 라운드트립
- **Result**: NOT_TESTED
- **Detail**: 사용자 시간 제약 — Phase 35 에서 함께 수행 예정
- **코드 레벨 검증**: grep 으로 다음 패턴 모두 확인 완료
  - `ResolveFixtureSequence(ESequence` ×1 (overload 정의)
  - `SaveFixtureForSequence` ×3 (정의 + Side/Bottom 호출 2건)
  - `LoadFixtureForSequence` ×3 (정의 + Side/Bottom 호출 2건)
  - `FIXTURE_SIDE` / `FIXTURE_BOTTOM` 섹션명 각 3회

## Summary

- **Total**: 5 / **Passed**: 1 / **Blocked**: 2 / **Not Tested**: 2
- **Phase 33 status**: PARTIAL sign-off (코드 변경 + 빌드 검증 PASS, 실측 UAT 는 Phase 35 로 이관)

## Carry-over

| ID | 항목 | 처리 |
|---|---|---|
| **CO-33-01** | Bottom Multi-Die 자동 FAI 매핑 (D-03 Option B = Die_i_X/Y/Angle 3 FAI) | **v1.2 이연** (Bottom 도메인 작업) |
| **CO-33-02** | 이미지 갱신 회귀 (Measurement/Edit 모드 stale cache) | **→ Phase 35** (디버그+hotfix) |
| **CO-33-03** | ~~Side/Bottom~~ Datum 검출 실패 — **2026-05-26 추가 보고: Top 도 동일 재현. 다중 이미지 Load 시나리오에서 Top/Side/Bottom 동시 차단** (baseline 회귀 또는 다중-Load 시나리오 신규 노출) | **→ Phase 35** Wave 1 최우선 |
| **CO-33-04** | Side/Bottom 실측 SIMUL UAT (Test 2/3 재검증 + Test 4 Top 회귀 + Test 5 INI 라운드트립) | **→ Phase 35** sign-off 조건 |
| **CO-33-05** | Phase 34 — Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 (가로축 2 ROI + 세로축 1 ROI 별도 이미지) | **→ Phase 34** (별도 신설) |
| **CO-33-06** | Bottom Shot 재로드 실패 — RecipeManager.Shots 글로벌 리스트에 per-sequence ownership 누락. ShotConfig.OwnerSequenceId 필드 + INI 직렬화 + 재로드 트리 분기 필요 (아키텍처 보강) | **→ Phase 35** (Phase 33 마이그레이션 미완성 보강) |

## Phase 35 신설 사유

Phase 33 의 SequenceHandler 마이그레이션 + INI 직렬화 자체는 코드 변경 atomic 으로 완료되었으며 D-06 회귀 가드 코드 레벨 충족. 하지만 Side/Bottom 의 **실제 검사 동작** 검증 중 CO-33-02 / CO-33-03 의 두 가지 잠재 원인이 노출되어 단순 hotfix 가 아닌 구조적 디버그가 필요. Phase 35 에서 Side/Bottom 실측 UAT + 디버그 + 필요한 hotfix 를 묶어 처리하는 것이 GSD atomic 원칙에 정합.
