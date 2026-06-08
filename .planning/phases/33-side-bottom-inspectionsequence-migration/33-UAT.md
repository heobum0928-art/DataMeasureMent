---
phase: 33-side-bottom-inspectionsequence-migration
status: partial
created: 2026-05-26
updated: 2026-05-27
total: 5
passed: 4
pending: 0
blocked: 1
not_tested: 0
retro_partial_signoff: 2026-05-27 via Phase 35
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
- **Result**: BLOCKED → Phase 34 carry-over
- **Detail**:
  - Side 시퀀스가 InspectionSequence 인스턴스로 등록됨 (구조적 검증 ✓)
  - Phase 35 진행 중 사용자 확인: Side 실측 fixture 는 **VerticalTwoHorizontal 듀얼 티칭 이미지** (수직 ROI = image1, 수평 ROI 2개 = image2) 요구
  - 현재 코드 의 `DatumConfig.TeachingImagePath` 는 단일 이미지 가정 — Phase 34 (CO-33-05) 의 `TeachingImagePath_Vertical` 신규 필드 + DatumFindingService dual-image 분기 완성 후 검증 가능
- **Retro update (2026-05-27)**: Phase 35 35-UAT.md Test 4 = CARRY_OVER → Phase 34 . Side 검증은 Phase 34 sign-off 와 함께 수행

## Test 3 — Bottom 시퀀스 Datum + FAI SIMUL
- **Result**: PASS (retro via Phase 35, 2026-05-27)
- **Detail**: Phase 35 35-UAT.md Test 5 참조
  - Bottom Datum 검출 PASS (1장 이미지 모드)
  - Bottom FAI 측정 mm + OK/NG 정상
  - Bottom Shot 재로드 PASS (CO-33-06 / CO-35-02 해소)
  - CO-33-02 (이미지 갱신 회귀) 도 Plan 35-01 hotfix 로 동시 해소

## Test 4 — Top 회귀 SIMUL
- **Result**: PASS (retro via Phase 35, 2026-05-27)
- **Detail**: Phase 35 35-UAT.md Test 2 (단일-Load) + Test 3 (다중-Load CO-33-02) 참조
  - Top 단일-Load 시나리오: Phase 23.1 sign-off 시점과 byte-identical
  - Top 다중-Load 시나리오: Plan 35-01 hotfix 로 회귀 해소
- **D-06 자동 가드**: `InspectionSequence.cs` / `Action_FAIMeasurement.cs` / `VisionResponsePacket.cs` **변경 0 라인 보존** ✓ (Phase 35 의 모든 commit 통과)

## Test 5 — INI 라운드트립
- **Result**: PASS (retro via Phase 35, 2026-05-27, Top + Bottom 만)
- **Detail**: Phase 35 35-UAT.md Test 6 참조
  - `[FORMAT]` Version=6 + `[FIXTURE]` (Top) + `[FIXTURE_BOTTOM]` + `[FIXTURE_BOTTOM_DATUM_0]` + `[SHOTS]` Count + 각 `[SHOT_n]` + `[SHOT_n_CAM]` 섹션 정상
  - Plan 35-02 의 `OwnerSequenceName=TOP/BOTTOM` 키 자동 직렬화 동작 확인
  - Side `[FIXTURE_SIDE]` 라운드트립은 Phase 34 후 재검증 (Side carry-over 일환)

## Summary

- **Total**: 5 / **Passed**: 4 (1 original + 3 retro via Phase 35) / **Blocked**: 1 (Test 2 Side → Phase 34) / **Not Tested**: 0
- **Phase 33 status (2026-05-27 update)**: 계속 PARTIAL sign-off — Test 2 (Side) 가 Phase 34 carry-over 인 한 fully signed_off 불가. Phase 34 완료 후 Test 2 retro 업데이트 시 fully signed_off 전환.

## Carry-over

| ID | 항목 | 처리 |
|---|---|---|
| **CO-33-01** | Bottom Multi-Die 자동 FAI 매핑 (D-03 Option B = Die_i_X/Y/Angle 3 FAI) | **v1.2 이연** (Bottom 도메인 작업) |
| **CO-33-02** | 이미지 갱신 회귀 (Measurement/Edit 모드 stale cache) | ✅ **해소** via Phase 35 Plan 35-01 (17ccc91) — Test 3/5 PASS |
| **CO-33-03** | ~~Side/Bottom~~ Datum 검출 실패 — Top 도 동일 재현 | ⚠ **부분 해소** via Phase 35 Plan 35-01 — Top + Bottom PASS, Side → Phase 34 (dual-image) |
| **CO-33-04** | Side/Bottom 실측 SIMUL UAT (Test 2/3/4/5) | ⚠ **부분 해소** via Phase 35 — Test 3 (Bottom) / Test 4 (Top) / Test 5 (INI Top+Bottom) PASS, Test 2 (Side) → Phase 34 |
| **CO-33-05** | Phase 34 — Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 | **→ Phase 34** (별도 신설, Side carry-over 일환) |
| **CO-33-06** | Bottom Shot 재로드 실패 — per-sequence ownership 누락 | ✅ **해소** via Phase 35 Plan 35-02 (11a6f61) + hotfix CO-35-02 (1b0894b) — Test 5 PASS |

## Phase 35 신설 사유

Phase 33 의 SequenceHandler 마이그레이션 + INI 직렬화 자체는 코드 변경 atomic 으로 완료되었으며 D-06 회귀 가드 코드 레벨 충족. 하지만 Side/Bottom 의 **실제 검사 동작** 검증 중 CO-33-02 / CO-33-03 의 두 가지 잠재 원인이 노출되어 단순 hotfix 가 아닌 구조적 디버그가 필요. Phase 35 에서 Side/Bottom 실측 UAT + 디버그 + 필요한 hotfix 를 묶어 처리하는 것이 GSD atomic 원칙에 정합.
