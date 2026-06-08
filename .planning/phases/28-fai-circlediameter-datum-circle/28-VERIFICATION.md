---
phase: 28-fai-circlediameter-datum-circle
verified: 2026-05-08T00:00:00Z
status: passed
score: 6/6 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: null
  previous_score: null
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 — Verification Report

**Phase Goal:** FAI 의 `CircleDiameterMeasurement` 에 Datum `CircleTwoHorizontal` 의 폴라 샘플링 검출 경로와 `Circle_RadialDirection` (Inward/Outward) 파라미터를 선택적으로 적용한다. 사용자가 명시적으로 RadialDirection 을 선택할 때만 폴라 알고리즘이 호출되고, 미선택 시(기본값) 기존 `VisionAlgorithmService.TryFindCircle` (단순 fit) 호출이 그대로 유지된다.

**Verified:** 2026-05-08
**Status:** SIGNED_OFF (passed)
**Re-verification:** No — initial verification (post sign-off compliance check)

---

## Goal Achievement

### Observable Truths (Acceptance Criteria)

| #   | Truth (AC)                                                                                                              | Status     | Evidence                                                                                                                                                                                                                                |
| --- | ----------------------------------------------------------------------------------------------------------------------- | ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AC-1 | `CircleDiameterMeasurement.Circle_RadialDirection` 필드 존재 + PropertyGrid 콤보 ▼ (Inward/Outward) 노출         | ✓ VERIFIED | `CircleDiameterMeasurement.cs:29-30, 37-38` declares `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` + `Circle_RadialDirection { get; set; } = ""` + `Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } }`. UAT Test 1 SIMUL_MODE PASS (사용자 시각 확인 2026-05-08). |
| AC-2 | 빈 `Circle_RadialDirection` → fit 경로 (`TryFindCircle`) 호출, polar 미호출                                           | ✓ VERIFIED | `CircleDiameterMeasurement.cs:58` `if (string.IsNullOrEmpty(Circle_RadialDirection))` → line 61 `svc.TryFindCircle(...)` with v1.0 byte-identical args. Grep counts: `string.IsNullOrEmpty(Circle_RadialDirection)` = 1, `TryFindCircle\b` = 1. |
| AC-3 | `Inward`/`Outward` → polar 경로 (`TryFindCircleByPolarSampling`) 호출, fit 미호출                                     | ✓ VERIFIED | `CircleDiameterMeasurement.cs:70-92` `else` 분기 → `svc.TryFindCircleByPolarSampling(...)` line 78 with mapped polarity + 4 FAI default consts. Grep count: `TryFindCircleByPolarSampling` = 1.                                       |
| AC-4 | Datum CTH ↔ FAI 검출 직경 동등성 (`\|D_fai − D_datum\| ≤ 0.001 mm`)                                                  | ✓ VERIFIED (code-inspection consensus) | Three-way single source: 모두 `EdgeOptionLists.MapRadialDirectionToHalconPolarity` 호출 (DatumFindingService.cs:200, :730 + CircleDiameterMeasurement.cs:75). Default 상수 `FaiCircle*` (10.0/0.02/0.02/"First") = `DatumConfig.Circle_*` defaults. UAT Test 2 코드 검증 PASS (사용자 합의 2026-05-08). |
| AC-5 | v1.0 INI 회귀 0 (RadialDirection 키 없음 → 빈 문자열 → fit 경로 → 결과 동일)                                       | ✓ VERIFIED (code-inspection consensus) | `Circle_RadialDirection { get; set; } = ""` default + `string.IsNullOrEmpty` 분기 → 기존 `TryFindCircle` 호출 (인자열 byte-identical to v1.0, commit `432adb2` `git diff -U10` 검증). UAT Test 3 코드 검증 PASS (사용자 합의 2026-05-08). |
| AC-6 | msbuild Debug/x64 PASS, 신규 error/warning 0                                                                          | ✓ VERIFIED | UAT Test 4 자동 PASS 2026-05-08T19:41:38: exit 0, new CS errors = 0, new CS warnings = 0. Pre-existing 2 distinct: `VirtualCamera.cs:266 CS0162` + `VisionAlgorithmService.cs:64 CS0219` (baseline 잔존, REQ-28-06 budget 5건 이하). DatumMeasurement.exe 생성됨. |

**Score:** 6/6 truths verified (4/4 UAT tests PASS)

### Required Artifacts

| Artifact                                                                                | Expected                                                                          | Status      | Details                                                                                                                                            |
| --------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs`                             | `MapRadialDirectionToHalconPolarity` helper + 4 polar defaults                  | ✓ VERIFIED  | Lines 29-45: 4 const fields (`FaiCirclePolarStepDeg=10.0`, `FaiCircleRectL1Ratio=0.02`, `FaiCircleRectL2Ratio=0.02`, `FaiCircleEdgeSelection="First"`) + helper method. Grep counts: helper signature = 1, FaiCircle* consts = 4. |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs`      | `Circle_RadialDirection` field + List wrapper + `TryExecute` branch              | ✓ VERIFIED  | Lines 29-30 field, lines 37-38 wrapper, lines 57-93 if/else branch. ICustomTypeDescriptor grep = 0 (REQ-28-05).                                  |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`                                  | 2 inline polarity ternaries replaced with helper calls (line 200 + line 730)     | ✓ VERIFIED  | Both lines confirmed via direct read. Grep `EdgeOptionLists.MapRadialDirectionToHalconPolarity\(config\.Circle_RadialDirection\)` = 2 matches. Inline ternary grep = 0. |
| `.planning/phases/28-fai-circlediameter-datum-circle/28-UAT.md`                         | UAT.md with 4 tests + signed_off frontmatter                                    | ✓ VERIFIED  | `status: signed_off`, `passed: 4`, `failed: 0`, `pending: 0`, `signed_off_date: 2026-05-08`. 4 Test sections present.                            |

### Key Link Verification (Wiring)

| From                                                          | To                                                                  | Via                                                          | Status  | Details                                                                                                                                          |
| ------------------------------------------------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------ | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CircleDiameterMeasurement.TryExecute` (polar branch)         | `VisionAlgorithmService.TryFindCircleByPolarSampling`               | direct call (D-01)                                           | ✓ WIRED | Line 78 `svc.TryFindCircleByPolarSampling(...)` with full 17-arg signature, return-checked, `out` consumed. Polarity mapped via helper.        |
| `CircleDiameterMeasurement.TryExecute` (fit branch)           | `VisionAlgorithmService.TryFindCircle`                              | direct call (preserved)                                      | ✓ WIRED | Line 61 `svc.TryFindCircle(...)` byte-identical to v1.0 (commit `432adb2` `git diff -U10` verified). REQ-28-04 회귀 0.                          |
| `CircleDiameterMeasurement.Circle_RadialDirectionList`        | `EdgeOptionLists.RadialDirections`                                  | `[ItemsSourceProperty]` + `[Browsable(false)]` getter         | ✓ WIRED | Line 38 `get { return EdgeOptionLists.RadialDirections; }`. Phase 17 D-02 single source, Phase 18 CO-01 verified pattern.                       |
| `DatumFindingService.TryFindCircleTwoHorizontal:200`          | `EdgeOptionLists.MapRadialDirectionToHalconPolarity`                | static helper call (replaces inline ternary)                | ✓ WIRED | Confirmed via direct read line 200. Variable `circlePolarity` flows into line 206 `TryFindCircleByPolarSampling` argument 9 unchanged.        |
| `DatumFindingService.TryTeachCircleTwoHorizontal:730`         | `EdgeOptionLists.MapRadialDirectionToHalconPolarity`                | static helper call (replaces inline ternary)                | ✓ WIRED | Confirmed via direct read line 730. Variable `circlePolarity` flows into line 737 `TryFindCircleByPolarSampling` argument 9 unchanged.        |
| `CircleDiameterMeasurement` polar branch                      | `EdgeOptionLists.MapRadialDirectionToHalconPolarity`                | static helper call (single-source, 3-way DRY)               | ✓ WIRED | Line 75. Three-way single source achieved (Datum find + Datum teach + FAI polar all share one helper).                                          |

### Data-Flow Trace (Level 4)

| Artifact                                | Data Variable                                            | Source                                                                  | Produces Real Data | Status     |
| --------------------------------------- | -------------------------------------------------------- | ----------------------------------------------------------------------- | ------------------ | ---------- |
| `CircleDiameterMeasurement.TryExecute`  | `foundRadius` (drives `resultValue = foundRadius * 2.0 * pixelResolution`) | Returned via `out` from either `TryFindCircle` or `TryFindCircleByPolarSampling` (HALCON Circle 검출) | Yes — HALCON 코드 경로 | ✓ FLOWING  |
| `CircleDiameterMeasurement.TryExecute`  | `polarity` (polar branch only)                           | `EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection)` — single source helper, byte-identical to Datum inline ternary | Yes (deterministic mapping) | ✓ FLOWING  |
| `CircleDiameterMeasurement.TryExecute`  | `stepDeg`, `rectL1Ratio`, `rectL2Ratio`, `selection` (polar branch only) | `EdgeOptionLists.FaiCircle*` consts (10.0/0.02/0.02/"First") = Datum CTH defaults | Yes (compile-time const) | ✓ FLOWING  |
| `Circle_RadialDirectionList` getter      | List<string>                                             | `EdgeOptionLists.RadialDirections` (Phase 17 D-02 single source: `["Inward","Outward"]`) | Yes (existing list) | ✓ FLOWING  |

### Behavioral Spot-Checks

Skipped end-to-end runtime spot checks because Phase 28 is a code-integration phase whose runtime behaviour is exercised through the WPF UI + camera pipeline. The equivalent spot checks are subsumed by:

- UAT Test 1 (PropertyGrid 콤보 SIMUL UAT — 사용자 직접 확인) — PASS
- UAT Test 4 (msbuild Debug/x64 자동 검증) — PASS

| Behavior                                                | Command / Source                                                       | Result                  | Status |
| ------------------------------------------------------- | ---------------------------------------------------------------------- | ----------------------- | ------ |
| msbuild Debug/x64 produces DatumMeasurement.exe         | UAT Test 4 자동 수행 (28-UAT.md line 162-173)                          | exit 0, .exe mtime 갱신 | ✓ PASS |
| Helper symbol callable from CircleDiameterMeasurement   | Static grep — `EdgeOptionLists.MapRadialDirectionToHalconPolarity` in CircleDiameterMeasurement.cs:75 | 1 match                 | ✓ PASS |
| Helper symbol callable from DatumFindingService         | Static grep — `EdgeOptionLists.MapRadialDirectionToHalconPolarity` in DatumFindingService.cs:200/730 | 2 matches               | ✓ PASS |
| ICustomTypeDescriptor not introduced                    | Static grep — `ICustomTypeDescriptor` in CircleDiameterMeasurement.cs  | 0 matches               | ✓ PASS |
| No leftover inline polarity ternary in DatumFindingService | Static grep — `string.Equals(config.Circle_RadialDirection.*"Outward".*"negative"` | 0 matches               | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan(s)             | Description                                                                                  | Status     | Evidence                                                                                                                                          |
| ----------- | -------------------------- | -------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| REQ-28-01   | 28-01, 28-02               | `Circle_RadialDirection` 필드 + PropertyGrid 콤보 (Inward/Outward) 단일 소스 노출            | ✓ SATISFIED | EdgeOptionLists.cs lines 27 (RadialDirections) + CircleDiameterMeasurement.cs lines 29-30/37-38 (field + wrapper). Commit `578cab6`. UAT Test 1 PASS. |
| REQ-28-02   | 28-01, 28-02, 28-03        | TryExecute 분기 (빈 → fit / Inward,Outward → polar) + 3-way single-source polarity mapping | ✓ SATISFIED | CircleDiameterMeasurement.cs:57-93 (branch). DatumFindingService.cs:200/730 (helper calls). EdgeOptionLists.cs:38-45 (helper). Commits `432adb2`, `84affbb`, `a894c36`. |
| REQ-28-03   | 28-01, 28-02, 28-03        | FAI ↔ Datum CTH 검출 직경 동등성 (≤ 0.001 mm)                                              | ✓ SATISFIED | Default 일치 (FaiCircle* = DatumConfig.Circle_* defaults) + helper byte-identical → 수학적 동등성. UAT Test 2 코드 검증 PASS (사용자 합의).             |
| REQ-28-04   | 28-02                      | INI 하위호환 (RadialDirection 키 없음 → 빈 문자열 → fit 경로 → v1.0 결과 동일)             | ✓ SATISFIED | `Circle_RadialDirection { get; set; } = ""` default + fit-path 인자열 byte-identical (commit `432adb2` `git diff -U10`). UAT Test 3 PASS.           |
| REQ-28-05   | 28-02                      | PropertyGrid 정적 노출 (ICustomTypeDescriptor 미도입)                                       | ✓ SATISFIED | CircleDiameterMeasurement.cs grep `ICustomTypeDescriptor` = 0 matches. 모든 필드 항상 표시.                                                       |
| REQ-28-06   | 28-01, 28-02, 28-03, 28-04 | msbuild Debug/x64 PASS, 신규 error/warning 0                                                 | ✓ SATISFIED | 모든 plan task 후 msbuild PASS. UAT Test 4 자동 PASS (2026-05-08): new CS errors = 0, new CS warnings = 0, pre-existing 2 distinct unchanged.       |

### Anti-Patterns Found

| File                                                                                | Line  | Pattern                                                            | Severity | Impact                                                                                                                                                       |
| ----------------------------------------------------------------------------------- | ----- | ------------------------------------------------------------------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs`  | 52    | `overlays = new List<EdgeInspectionOverlay>();` (empty list return) | ℹ️ Info  | Phase 7-01 D-03 의도된 stub (5종 Measurement overlay 미구현). Phase 28 범위 밖. Pre-existing baseline, not introduced by Phase 28.                          |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` | 61    | `overlays = new List<EdgeInspectionOverlay>();` (empty list return) | ℹ️ Info  | Phase 7-01 D-03 anti-goal stub. Phase 28 범위 밖. Carry-over로 backlog.                                                                                      |
| `WPF_Example/Device/Camera/VirtualCamera.cs`                                        | 266   | `CS0162` 도달할 수 없는 코드                                        | ℹ️ Info  | Pre-existing baseline (Phase 28 무관). REQ-28-06 budget 5건 이하 잔존 OK.                                                                                  |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`                           | 64    | `CS0219` 사용되지 않은 `scanHorizontal` 로컬                        | ℹ️ Info  | Pre-existing baseline (Phase 28 무관). REQ-28-06 budget 5건 이하 잔존 OK.                                                                                  |

No Phase 28-introduced anti-patterns. All flagged items are pre-existing baseline or deliberate stubs (Phase 7-01 D-03).

### Human Verification Required

None — all 4 UAT tests already executed with PASS verdicts:
- Test 1: 사용자 SIMUL_MODE UAT (2026-05-08)
- Test 2: 사용자 합의 코드 검증 (2026-05-08) — mathematical equivalence
- Test 3: 사용자 합의 코드 검증 (2026-05-08) — fit-path byte-identical
- Test 4: Plan 04 executor 자동 msbuild (2026-05-08)

Phase already SIGNED_OFF per UAT.md frontmatter (`status: signed_off`, `signed_off_date: 2026-05-08`).

### Carry-Over Items (Phase 28 범위 밖)

| # | Item                                                                                                      | Disposition | Rationale                                                                                                                                       |
|---|-----------------------------------------------------------------------------------------------------------|-------------|-------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 | `PointToLineDistanceMeasurement.cs:61` — `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트 반환 (ROI 시각화 미구현) | Backlog     | Phase 7-01 D-03 anti-goal 의도된 stub. Phase 28 SPEC out-of-scope ("다른 Measurement 클래스 알고리즘 변경 금지"). 28-04-SUMMARY § Carry-over Items 명시 기록. v1.1 후속 phase 또는 별도 quick task로 처리 가능. Phase 28 sign-off 차단 안 함. |

### Gaps Summary

**No gaps.** Phase 28 goal fully achieved:
- 6/6 Acceptance Criteria PASS (4/4 UAT tests PASS — 1 SIMUL UAT + 2 code-inspection consensus + 1 auto msbuild)
- 6/6 Requirements (REQ-28-01..REQ-28-06) SATISFIED
- All 5 key links WIRED (3-way single-source polarity helper achieved)
- All 4 artifacts present and substantive
- Build verification PASS (0 new errors, 0 new warnings)
- ICustomTypeDescriptor not introduced (REQ-28-05 honored — avoids Phase 17 D-02 deviation)
- Phase 17 D-02 단일 소스 패턴 + Phase 18 CO-01 콤보 바인딩 패턴 재사용으로 일관성 유지

**Transparency note (Tests 2/3 code-inspection PASS):** SIMUL_MODE empirical UAT was substituted with code-inspection per user's explicit consent (28-04-SUMMARY § Decisions Made). The substitution is documented in 28-UAT.md Tests 2/3 § "근거 (코드 검증)" with cited evidence from Plans 01-03 SUMMARY (default-equality + helper byte-identical proofs). The mathematical equivalence is deterministic — empirical UAT would only re-confirm the same result.

**Phase 28 SIGNED_OFF 2026-05-08.** Next milestone task per ROADMAP.md = Phase 20 (코드 스타일 정리, QUAL-02 + QUAL-04). Backlog: PointToLineDistance ROI 시각화 미구현 (Phase 28 carry-over).

---

_Verified: 2026-05-08_
_Verifier: Claude (gsd-verifier)_
