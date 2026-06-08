---
phase: 37-side-multi-datum-dualimage-2026-05-28
type: uat
status: signed_off
source:
  - 37-03-PLAN.md
started: 2026-05-28
updated: 2026-05-28
auto_checks:
  guard_files_changed: InspectionListView.xaml.cs 0 (Task 1 코드 변경 0, D-37-07)
  msbuild_debug_x64: pass
  msbuild_new_warnings: 0
user_tests:
  total: 4
  pass: 4
  fail: 0
  partial: 0
  pending: 0
uat_hotfixes:
  - "신규버그 A — Shot2 측정 선택 시 Shot1 이미지 표시 (commit 1c11c35) — resolved"
  - "신규버그 B — 세션 중 추가한 새 Shot 측정 이미지/ROI 미추종 (commit c6576e5) — resolved"
carry_over_resolves:
  - "CO-36-06 / CO-36-07 — Side 4-datum × DualImage(8-image) + 측정 별도 이미지 구조 — Phase 37 (Plan 01/02/03) 로 종결"
---

# Phase 37 UAT — Side 다중 Datum (4 DualImage / 8-image) 검사 구조

> **Sign-off: SIGNED_OFF (2026-05-28).** 사용자 SIMUL_MODE 검증 4항목 전 항목 PASS.
> UAT 도중 신규버그 2건(A: multi-Shot 동일 FAI명 충돌, B: 세션 중 새 Shot 미추종) 발견·수정 후 재검증 PASS.
> CO-36-06 / CO-36-07 (Side 다중 datum DualImage 구조) 종결.

## Results Summary (2026-05-28)

| Total | Passed | Issues | Pending |
|-------|--------|--------|---------|
| 4 | 4 | 0 | 0 |

| Test | 항목 | 결과 | 비고 |
|---|---|---|---|
| 1 | datum 독립 실행 | PASS | Datum 4개 + Shot 에서 side datum 1 참조 측정 정상 |
| 2 | 4-datum mixed 개별 검출 | PASS | datum 4개가 각자 자기 이미지로 검출, DatumConfigs[0] 단일 묶임 없음 |
| 3 | 측정 이미지 ≠ datum 이미지 (D-37-06) | PASS | Shot 이미지 로드 시 measurement 가 해당 이미지를 따라감 |
| 4 | 예외 robustness (T-37-06) | PASS | 빈/잘못된 datum 티칭 경로 시 crash 없이 skip+log |

## Auto Checks

| Check | Status | Detail |
|---|---|---|
| Task 1 guard (InspectionListView.xaml.cs 변경 0) | PASS | D-37-07 — 기존 AddDatum/PropertyGrid 흐름으로 4-datum DualImage 생성/티칭 가능, 코드 변경 0 (commit fd5bce1) |
| msbuild Debug/x64 | PASS | UAT 중 2 hotfix 후 재빌드 0 error, 신규 warning 0 (c6576e5) |

## User Tests (사용자 SIMUL UAT — 4 시나리오)

### Test 1 — datum 독립 실행
**Goal:** Side fixture 에 datum 4개(각 VerticalTwoHorizontalDualImage) 생성 후, 일부 datum 검출 실패 시에도 성공 datum 을 참조하는 측정이 정상 진행.
**Expected:** datum 일부가 검출 실패해도 검사가 abort 되지 않고, 성공한 datum 을 참조하는 측정은 측정값/판정이 정상 표시. 실패 datum 을 참조하는 측정만 개별 실패로 식별.
**Result:** **PASS** (2026-05-28 사용자 확인) — Datum 4개 + Shot 에서 side datum 1 참조 측정 정상 동작.

### Test 2 — 4-datum mixed 개별 검출
**Goal:** datum 4개가 각자 자기 TeachingImagePath(_Vertical) 이미지로 독립 검출되며 DatumConfigs[0] 하나에 묶이지 않음.
**Expected:** datum 4개가 각자 자기 이미지로 검출되며, 로그에 datum 별 검출/실패 메시지가 datum 수만큼 출현.
**Result:** **PASS** (2026-05-28 사용자 확인) — 4-datum 개별 검출 정상 (Plan 02 per-datum loop + ClearDatumTransforms/TryRunSingleDatum 누적 경로).

### Test 3 — 측정 이미지 ≠ datum 이미지 (D-37-06)
**Goal:** 측정은 Shot 의 SimulImagePath 이미지에서 수행되고, datum 보정은 datum 티칭 이미지로 검출된 transform 으로 적용 — 두 이미지가 다른 파일이어도 측정 정상.
**Expected:** Shot 이미지 로드 시 measurement 가 해당 Shot 이미지를 따라가며, datum transform 은 이름 참조로 분리 적용.
**Result:** **PASS** (2026-05-28 사용자 확인) — Shot 이미지 로드 시 measurement 가 해당 이미지를 따라감. (UAT 중 신규버그 A/B 발견·수정 후 PASS — 아래 Gaps 참조.)

### Test 4 — 예외 robustness (T-37-06 DoS mitigation)
**Goal:** 빈/잘못된 datum 티칭 경로가 있어도 앱이 crash 하지 않고 해당 datum 만 skip + 로그.
**Expected:** lenient skip — 누락 경로 datum 만 graceful 처리, 다른 datum/측정은 정상 진행, abort 없음.
**Result:** **PASS** (2026-05-28 사용자 확인) — 예외 경로 robustness 정상 (Plan 01 lenient TryRunDatumPhase + Plan 02 File.Exists 가드 + HImage finally Dispose).

## Gaps

UAT 도중 발견된 신규버그 2건 — 모두 동일 세션 내 수정 후 재검증 PASS (resolved).

### Gap A — multi-Shot 동일 FAI명 충돌 → Shot2 측정 선택 시 Shot1 이미지 표시
- **Status:** resolved (commit 1c11c35)
- **발견:** Test 3 (측정 이미지 ≠ datum 이미지) 검증 중.
- **Root cause:** Measurement 노드의 이미지/ROI 해석이 FAI 이름 round-trip(`FindFAIByName`)에 의존. 여러 Shot 의 FAI 이름이 같으면(기본 `FAI_0`) 첫 Shot 의 FAI 가 반환되어, Shot2 의 측정을 선택해도 Shot1 의 이미지가 표시됨.
- **Fix:** 이름 round-trip 대신 측정 객체 참조(`FindFAIContainingMeasurement`, `ReferenceEquals`)로 변경. `HighlightSelectedRoi` 의 `anchorFai` 해석도 동일 원인 함께 수정.
- **Files:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`

### Gap B — 세션 중 추가한 새 Shot 측정 이미지/ROI 미추종
- **Status:** resolved (commit c6576e5)
- **발견:** Gap A 수정 후 동일 Test 3 재검증 중 (세션 중 신규 Shot 추가 시나리오).
- **Root cause:** `AddShotToSequence` 가 새 Shot 을 `RecipeManager` 에만 추가하고 라이브 Action(`pSeq`)은 실행 시점에 지연 동기화. 세션 중 새 Shot 의 측정을 `pSeq` 에서 못 찾아 이전 Shot 의 이미지/ROI 가 남음(앱 재시작 후엔 정상이었음).
- **Fix:** `FindFAIContainingMeasurement` 가 `RecipeManager.Shots`(동적 FAI 단일 소스, 신규 Shot 즉시 반영)를 우선 탐색하고 `pSeq` 는 fallback 으로 두도록 변경.
- **Files:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- **Verify:** msbuild Debug/x64 PASS, 신규 warning 0.

## Notes

- 모든 검증은 사용자 SIMUL_MODE 화면 시각/동작 확인 한정. 자동 픽셀 비교 도구 없음.
- Task 1 (datum 4개 DualImage 생성/티칭 흐름)은 기존 `AddDatumToSequence → inspSeq.AddDatum` 반복 + `DatumConfig` ICustomTypeDescriptor 의 DualImage 시 TeachingImagePath + TeachingImagePath_Vertical 동시 노출로 코드 변경 없이 충족 (D-37-07).
- 두 hotfix 모두 Phase 37 의 다중 datum/multi-Shot 구조 도입으로 표면화된 기존 잠복 결함(동일 FAI명 + 지연 동기화)이었으며, 이름 기반 → 객체/단일소스 참조로 전환하여 해소.
