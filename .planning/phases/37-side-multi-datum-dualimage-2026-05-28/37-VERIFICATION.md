---
phase: 37-side-multi-datum-dualimage-2026-05-28
verified: 2026-05-28T09:30:00Z
status: passed
score: 9/9 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  note: initial verification
gaps: []
---

# Phase 37: Side 다중 Datum (4 DualImage / 8-image) 검사 구조 Verification Report

**Phase Goal:** Side 다중 Datum (4 DualImage / 8-image) 검사 구조 — datum 4개(각 VerticalTwoHorizontalDualImage, 2장=8장)가 각자 별도 이미지로 독립 검출되고(전부-성공 강제 제거 = lenient), 측정은 datum 이미지와 별개 이미지로 수행되며, 검사 실행 흐름·데이터모델(datum↔shot↔image 매핑)·UI 가 이를 지원한다. CO-36-06/07 흡수.
**Verified:** 2026-05-28T09:30:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | datum 4개 중 하나가 검출 실패해도 datum phase 가 전체를 abort 하지 않는다 (lenient) | ✓ VERIFIED | `TryRunDatumPhase` 1-image(L163-166) / 2-image(L185) 모두 실패 시 `continue;`+log, `FinishAction(Error)` 미호출. EStep.DatumPhase(L80-121) 에 `FinishAction(EContextResult.Error)` 0건, 실패 시 `continue;` 후 항상 `Step = EStep.Grab`(L119) |
| 2 | 성공한 datum 만 `_datumTransforms[name]` 에 저장된다 | ✓ VERIFIED | `TryRunSingleDatum` (InspectionSequence.cs:211-214) 실패 시 `return false` (미저장), 성공 시에만 `_datumTransforms[datum.DatumName ?? ""] = transform` |
| 3 | datum 마다 자기 AlgorithmType 으로 DualImage 여부 판단 (DatumConfigs[0] 단일 판단 제거) | ✓ VERIFIED | EStep.DatumPhase loop L86 `datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage` per-datum 분기. TryRunSingleDatum L204 동일 per-datum 분기. 전 파일 `DatumConfigs[0]` 참조 0건 (grep 확인) |
| 4 | EStep.DatumPhase 가 DatumConfigs 전체를 loop 하며 per-datum 이미지로 검출 누적 | ✓ VERIFIED | Action_FAIMeasurement.cs:84 `foreach (var datum in parentSeq.DatumConfigs)`, L83 loop 전 `ClearDatumTransforms()` 1회, datum 마다 `TryRunSingleDatum` 누적 |
| 5 | 각 DualImage datum 이 자기 TeachingImagePath/TeachingImagePath_Vertical 2장을 로드 | ✓ VERIFIED | `TryGrabOrLoadDualDatumImages(DatumConfig datum,...)` (L296) L303-304 `datum.TeachingImagePath` / `datum.TeachingImagePath_Vertical` 인자 datum 에서 읽음. DatumConfigs[0] 한정 제거됨 |
| 6 | 측정 이미지 분리: EStep.Measure 가 ShotParam.GetImage() 사용 + TryGetDatumTransform(meas.DatumRef) 이름 참조 (D-37-06) | ✓ VERIFIED | Action_FAIMeasurement.cs:160 `using (var image = ShotParam.GetImage())`, L166 `parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)`, L169 미해결 시 `HomMat2dIdentity` fallback. 측정 이미지 ≠ datum 티칭 이미지 구조 |
| 7 | datum 4개 DualImage 생성/티칭이 기존 UI 흐름으로 가능 (신규 UI 최소화) | ✓ VERIFIED | `AddDatum` (InspectionSequence.cs:131-137) 호출마다 새 DatumConfig 추가 + 자동 유니크 기본명 `Datum_{N}`. InspectionListView.xaml.cs 코드 변경 0 (D-37-07). UAT Test 1/2 PASS |
| 8 | 다중 Shot UI 이미지/ROI 해석 정확성 (RecipeManager.Shots 우선, 객체 참조) | ✓ VERIFIED | `FindFAIContainingMeasurement` (MainView.xaml.cs:1732-1766) RecipeManager.Shots 우선 탐색(L1739-1748) → pSeq fallback(L1750-1764), `ReferenceEquals` 객체 참조. 이름 round-trip 충돌 제거 |
| 9 | UAT signed_off (4/4 PASS) | ✓ VERIFIED | 37-UAT.md status: signed_off, user_tests 4/4 PASS, 중간 hotfix 2건(1c11c35/c6576e5) resolved |

**Score:** 9/9 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `InspectionSequence.cs` | lenient TryRunDatumPhase + TryRunSingleDatum/ClearDatumTransforms 누적 경로 | ✓ VERIFIED | 두 lenient 오버로드(L146/L175) + ClearDatumTransforms(L192) + TryRunSingleDatum(L197) 존재, 누적 저장 확인 |
| `Action_FAIMeasurement.cs` | per-datum loop EStep.DatumPhase + per-datum 이미지 로드 헬퍼 | ✓ VERIFIED | EStep.DatumPhase loop(L80-121), TryGrabOrLoadDualDatumImages(DatumConfig)(L296), GrabOrLoadDatumImage(DatumConfig)(L273). 무참조 InspectionSequence 오버로드 제거됨 |
| `MainView.xaml.cs` | multi-Shot 객체참조 해석 (UAT hotfix) | ✓ VERIFIED | FindFAIContainingMeasurement(L1732) RecipeManager.Shots 우선 + ReferenceEquals. HighlightSelectedRoi anchorFai 도 객체 참조(L389) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| EStep.DatumPhase | TryRunSingleDatum | datum loop 안 per-datum 누적 호출 (lenient) | ✓ WIRED | L94/L109 `parentSeq.TryRunSingleDatum(datum, ...)` 호출, 실패 시 log+continue (abort 없음) |
| EStep.Measure | ShotParam.GetImage() | 측정은 Shot 이미지, transform 은 이름 참조 | ✓ WIRED | L160 GetImage + L166 TryGetDatumTransform(meas.DatumRef) |
| TryRunSingleDatum | _datumTransforms | 성공 datum 만 누적 저장 (Clear 안 함) | ✓ WIRED | L214 누적 저장, 본문에 Clear() 없음 |
| AddDatumToSequence | InspectionSequence.AddDatum | 반복 호출로 datum N개 생성 | ✓ WIRED | AddDatum L131 호출마다 신규 DatumConfig, UAT 4-datum 확인 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| EStep.Measure transform | `_datumTransforms[DatumRef]` | TryRunSingleDatum (datum 검출) → DatumFindingService.TryFindDatum | ✓ 실제 검출 transform (실패 시 identity fallback) | ✓ FLOWING |
| EStep.Measure image | `ShotParam.GetImage()` | Shot SimulImagePath / grab | ✓ Shot 자기 이미지 (datum 이미지와 별개) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| msbuild Debug/x64 빌드 | (SUMMARY 기록 + UAT auto_checks) | 0 error, 신규 warning 0 | ✓ PASS (SUMMARY/UAT 기록) |
| 런타임 다중 datum 독립 실행 / 측정≠datum 이미지 / 예외 robustness | SIMUL UAT (사용자 수행) | 4/4 PASS | ✓ PASS (37-UAT.md) |

비고: 빌드/런타임은 사용자 SIMUL UAT 로 검증 완료(37-UAT.md signed_off). verifier 는 정적 코드 구조 검증 + SUMMARY/UAT 기록 확인으로 교차 확인.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| (none formal) | — | CO-36-06/07 흡수 phase, REQUIREMENTS.md 별도 ID 없음 | ✓ N/A | CO-36-06/07 종결 (37-UAT.md carry_over_resolves) |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| InspectionSequence.cs | 214 | `_datumTransforms[datum.DatumName ?? ""]` — 중복/빈 DatumName 시 silent overwrite (WR-01) | ⚠️ Warning | datum 이름 유일 전제에서는 미발현. AddDatum 자동 유니크 기본명(`Datum_{N}`) 생성. 수동 rename / INI 로드 시 중복 가능 → 잘못된 transform 참조 리스크. 정상 흐름 goal 달성에는 영향 없음 |
| InspectionSequence.cs | 175-189 | 2-image TryRunDatumPhase 오버로드에 `if (datum == null) continue;` 가드 없음 (WR-02) | ⚠️ Warning | 현재 production 무참조(dead code, IN-01). null datum 시 NRE 잠재. EStep.DatumPhase 의 실제 경로(L85)는 null 가드 보유 |
| InspectionSequence.cs | 146/175 | 두 TryRunDatumPhase 오버로드 production 무참조 (IN-01, dead code) | ℹ️ Info | EStep.DatumPhase 가 ClearDatumTransforms+TryRunSingleDatum 경로로 대체. 정리 권장이나 goal 영향 없음 |

### Human Verification Required

없음 — 모든 런타임/시각 검증은 37-UAT.md (signed_off, 4/4 PASS) 로 사용자가 이미 완료. 추가 인간 검증 항목 없음.

### Gaps Summary

gap 없음. Phase 37 의 9개 must-have(goal-derived truths + plan frontmatter truths)가 코드에서 전부 확인됐다:

- **lenient 다중 datum 독립 검출** — TryRunDatumPhase 두 오버로드 + TryRunSingleDatum/ClearDatumTransforms 누적 경로, EStep.DatumPhase per-datum loop. `FinishAction(Error)` 전면 제거, `DatumConfigs[0]` 참조 0건.
- **측정 이미지 분리 (D-37-06)** — EStep.Measure 의 ShotParam.GetImage() + TryGetDatumTransform(meas.DatumRef) 이름 참조 구조 보존·확인.
- **UI 다중 Shot 정확성** — FindFAIContainingMeasurement 의 RecipeManager.Shots 우선 + 객체 참조(ReferenceEquals)로 동일 FAI명 충돌·신규 Shot 미추종 해소.
- **UAT signed_off** — 4/4 PASS, CO-36-06/07 종결.

**Risk note (WR-01):** `_datumTransforms` 키가 `datum.DatumName` 으로, 중복/빈 이름 시 silent overwrite 가능. 단 AddDatum 이 유니크 기본명을 자동 부여하므로 **정상 운영 흐름(datum 이름 유일 전제)에서는 발현하지 않는다.** 사용자 수동 rename 또는 INI 로드로 동일 이름 2개를 만들면 후속 datum 이 앞 datum 의 transform 을 덮어써, 그림자 datum 을 참조하는 측정이 잘못된 transform 을 silent 하게 사용할 수 있다. 코드 리뷰 WR-01 로 식별됨. **goal 판정에는 영향 없음**(datum 이름 유일성 전제 충족 시 9/9 달성) — 향후 robustness 강화(중복 키 감지+log, 빈 DatumName 거부) 권장. WR-02/IN-01 (dead-code 오버로드 null 가드 부재)도 동일하게 production 경로 밖이라 goal 미영향.

---

_Verified: 2026-05-28T09:30:00Z_
_Verifier: Claude (gsd-verifier)_
