---
phase: 35-side-bottom-uat-and-phase33-completion
plan: 03
status: partial
created: 2026-05-27
updated: 2026-05-27
total: 6
passed: 5
failed: 0
blocked: 0
carry_over: 1
pending: 0
sign_off_reviewer: 사용자 (heobum)
sign_off_date: 2026-05-27
---

# Phase 35 UAT — Side/Bottom 실측 UAT + Phase 33 마이그레이션 보강

본 UAT 는 Phase 35 의 6 sign-off 조건 (CONTEXT.md) 및 Phase 33 의 4 carry-over UAT (Test 2~5) 를 통합 검증한다.

**Side 시퀀스 (Test 4)** 는 사용자 실측 fixture 가 **VerticalTwoHorizontal 듀얼 티칭 이미지 (수직 ROI image1 + 수평 ROI 2개 image2)** 를 요구하여 Phase 34 (CO-33-05) 완료 전 검증 불가 확정 → **Phase 34 carry-over**. Phase 35 sign-off 는 partial (Side 1건 carry-over) 로 처리.

---

## Test 1 — msbuild Debug/x64 Rebuild
- **Result**: PASS (자동)
- **Detail**:
  - exit code 0, 0 Error(s)
  - 신규 warning category = 0
  - Baseline warning 유지: 5 × CS0618 (Phase 33 의도된 deprecation) + 1 × CS0162 (VirtualCamera SIMUL_MODE 도달 불가) + 1 × MSB3884 (ruleset)
  - Plan 35-01 (17ccc91) + Plan 35-02 (11a6f61) + hotfix (1b0894b) 통합 빌드 PASS

## Test 2 — Top 단일-Load SIMUL UAT (Phase 23.1 회귀)
- **Result**: PASS
- **Detail**:
  - 기존 Top recipe (Phase 23.1 sign-off 시 검증된 A1~A5 recipe) 로드
  - Top 시퀀스 노드 → Datum / Shot 정상 표시
  - 단일-Load 시나리오 검사 1회 실행 → A1~A5 측정값 + OK/NG 판정 표시 정상
  - **Phase 23.1 sign-off 시점 결과와 동일** (회귀 0)
  - Plan 35-02 의 OwnerSequenceName 폴백 (D-B1) 정상 — 기존 INI (키 부재) 의 모든 Shots 자동 TOP 폴백 확인

## Test 3 — Top 다중-Load SIMUL UAT (CO-33-02 hotfix 핵심 검증)
- **Result**: PASS (Plan 35-01 hotfix 로 해소)
- **Detail**:
  - 다중-Load 시나리오: Datum Load → Datum 티칭 → Shot Load → Shot 티칭 → Datum 재방문 → Datum 재Load → Shot 재방문 → Shot 재Load → 검사
  - Datum 위치 검출 PASS + 측정값 정상 표시
  - 사용자 보고 회귀 (Top 도 다중-Load 시나리오에서 Datum 못 찾음) 해소 확인
  - Plan 35-01 의 HalconViewerControl 두 LoadImage 오버로드 일관성 + DisplayDatumImage 자동 캔버스 전환 동작 확인
- **가설 검증**: Plan 35-01 의 hotfix 가 **CO-33-02 / CO-33-03 의 단일 root cause** 라는 가설이 Top 에서 PASS — Bottom 도 Test 5 에서 동일하게 PASS 하여 가설 확정

## Test 4 — Side 시퀀스 Datum + FAI SIMUL UAT (Phase 33 Test 2 재수행)
- **Result**: CARRY_OVER → Phase 34
- **Detail**:
  - 사용자 실측 fixture 는 Side Datum 을 **VerticalTwoHorizontal 듀얼 티칭 이미지** 로 검출 (수직 ROI = image1, 수평 ROI 2개 = image2)
  - 현재 코드 의 `DatumConfig.TeachingImagePath` 는 단일 이미지 가정 — ROI 3개가 1장 위에 있어야 동작
  - Phase 35 CONTEXT.md 의 Out-of-scope 명시: "Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 (CO-33-05) → Phase 34"
  - Phase 34 의 `TeachingImagePath_Vertical` 신규 필드 + DatumFindingService 듀얼-이미지 분기 완성 후 재검증 필요
- **Carry-over**: → Phase 34 (CO-33-05)

## Test 5 — Bottom 시퀀스 SIMUL UAT (Datum + FAI + Shot 재로드)
- **Result**: PASS
- **Detail**:
  - **Bottom Shot 재로드 (CO-33-06 핵심):** Bottom Datum + Shot + FAI 셋업 후 Save → 앱 재시작 → 동일 recipe Load → Bottom 시퀀스 트리에 Shot/FAI 정상 복원 PASS
  - **Bottom Shot 노드 클릭 → Start (CO-35-01 hotfix):** "There is no action to run" 에러 없이 정상 검사 실행
  - **Bottom Datum 검출:** 1장 이미지 모드 (TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal-단일) 에서 정상 검출
  - **Bottom FAI 측정:** mm + OK/NG 정상 표시
- **검증된 hotfix**: Plan 35-02 (OwnerSequenceName 아키텍처) + 1b0894b (TryLoadNewFormat 3 시퀀스 호출 + ResolveRunnableAction 로컬 인덱스)

## Test 6 — INI 라운드트립 (Top + Bottom, Phase 33 Test 5 재수행)
- **Result**: PASS
- **Detail**:
  - Save → 앱 재시작 → Load 시 Top + Bottom 모두 정상 복원 (Test 5 검증 시 동시 수행)
  - `[FORMAT]` Version=6 + `[FIXTURE]` (Top) + `[FIXTURE_BOTTOM]` + `[FIXTURE_BOTTOM_DATUM_0]` + `[SHOTS]` Count + 각 `[SHOT_n]` + `[SHOT_n_CAM]` 섹션 정상
  - `OwnerSequenceName=TOP` / `OwnerSequenceName=BOTTOM` 키 자동 직렬화 동작 확인 (Plan 35-02 D-A1)
  - Side INI 라운드트립 (`[FIXTURE_SIDE]` 포함) 은 Phase 34 후 재검증 필요

---

## Summary

- **Total**: 6 / **Passed**: 5 / **Carry-over**: 1 / **Failed**: 0 / **Blocked**: 0
- **Phase 35 status**: PARTIAL sign-off — Side (Test 4) 만 Phase 34 carry-over, 나머지 5건 PASS

### Hotfix during UAT

UAT Wave 3 진행 중 사용자 보고로 2건 회귀 발견 → 단일 commit 으로 hotfix 적용 (`1b0894b`):

| ID | 증상 | Root cause | Fix |
|---|---|---|---|
| **CO-35-01** | Bottom Shot 노드 클릭 → "There is no action to run" 에러 | `InspectionListView.ResolveRunnableAction` 의 글로벌 `RecipeManager.Shots.IndexOf` 가 Plan 35-02 의 시퀀스별 로컬 actionIdx 와 불일치 | `ComputeLocalShotIndex` helper 신규 — RebuildInspectionActions 의 actionIdx 부여 로직과 1:1 대응 |
| **CO-35-02** | Bottom Shot 셋팅 후 앱 재시작 → Datum 외 전부 사라짐 (트리에 안 보임) | `SequenceHandler.TryLoadNewFormat` 가 `RebuildInspectionActions(Top)` 만 호출 → Side/Bottom 시퀀스의 ActionCount = 0 → 트리 빌드 시 Shot 누락 | TryLoadNewFormat 에서 Top/Side/Bottom 3 시퀀스 모두 RebuildInspectionActions 호출 |

두 hotfix 모두 Plan 35-02 의 명시적 **Part D 영역** (`<verification>` 의 "트리 재구축 시 OwnerSequenceName 필터링 — Plan 35-03 UAT 결과로 hotfix") 에 해당.

### CO-33-02 단일 root cause 가설 확정

Plan 35-01 의 광범위 캐시 hotfix (HalconViewerControl 두 오버로드 일관성 + DisplayDatumImage 자동 캔버스 전환) 가 **Top + Bottom 동시 해소** 확인. Side 는 다른 root cause (CO-33-05 dual-image) 로 별도 처리 필요.

→ Phase 35 의 핵심 가설 (CO-33-02 / CO-33-03 = 캐시 stale 단일 root cause) **확정**.

### CO-33-06 (per-sequence Shot ownership) 해소

Plan 35-02 의 `OwnerSequenceName` 아키텍처 + hotfix CO-35-02 (TryLoadNewFormat 3 시퀀스 호출) 로 **Bottom Shot 재로드 시 정상 복원** 검증. v1.1 의 Phase 24 (검사 워크플로우 end-to-end) prerequisite 충족.

---

## Carry-over

| ID | 항목 | 처리 |
|---|---|---|
| **CO-33-05 / Phase 34** | Side Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 (수직 ROI image1 + 수평 ROI 2개 image2) | Phase 34 신설 (별도 phase) |
| **Phase 33 Test 2 (Side)** | Side 시퀀스 Datum + FAI SIMUL — Phase 34 완료 후 재검증 | → Phase 34 sign-off 와 함께 |
| **Phase 33 Test 5 (Side INI)** | `[FIXTURE_SIDE]` INI 라운드트립 — Side 실측 검증의 일환 | → Phase 34 |

---

## Phase 33 retro 부분 sign-off

Phase 35 sign-off 시 Phase 33 의 carry-over 4건 (CO-33-02 / CO-33-03 / CO-33-04 / CO-33-06) 중 3건 해소:

- **CO-33-02 (이미지 갱신 회귀)**: Plan 35-01 hotfix 로 해소 (Test 3 PASS)
- **CO-33-03 (Datum 검출 실패)**: Top + Bottom 해소 (Test 3 + 5 PASS). Side 는 dual-image 의존 → Phase 34
- **CO-33-04 (Side/Bottom 실측 UAT)**: Bottom + INI 라운드트립 PASS, Side carry-over → Phase 34
- **CO-33-06 (Bottom Shot 재로드)**: Plan 35-02 + hotfix CO-35-02 로 해소 (Test 5 PASS)

Phase 33 status 는 **계속 partial** 유지 — Side Test 2 가 Phase 34 carry-over 인 한 fully signed_off 불가.

---

## v1.1 잔여

- **Phase 34** — Datum 듀얼 티칭 이미지 (CO-33-05) — **next**
- Phase 24 — 검사 워크플로우 end-to-end (Phase 35 prerequisite 충족 — Top/Bottom 모두 검증)
- Phase 25 — 결과 분석 & Export
- Phase 27 — Side Inspection 확장 (Phase 999.1 흡수)

---

*Phase 35 partial sign-off — 2026-05-27, 사용자 (heobum)*
