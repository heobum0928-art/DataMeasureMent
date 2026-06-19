---
phase: 57-pattern-roi-ux-datum-align-hardening
verified: 2026-06-19T09:05:00Z
status: human_needed
score: 5/5 must-haves verified (code-level); 5 items require UAT
overrides_applied: 0
human_verification:
  - test: "#6 off 레시피 회귀 0 — stale leveling 키 포함 기존 레시피 INI 로드"
    expected: "로드 크래시 없음, leveling 미사용 동작 변화 없음, MoveZ→DatumPhase 측정 흐름 정상"
    why_human: "실데이터 INI 로드 + 측정 사이클 런타임 동작은 앱 실행 필요 (bin gitignored, SIMUL_MODE)"
  - test: "#4 Side VerticalTwoHorizontalDualImage align-enabled 정확도"
    expected: "가로축 패턴매칭 단일 transform 이 가로(Horizontal_A/B)/세로(Vertical) 검출 ROI 모두에 적용되어 DetectedOrigin/기준선 정확 검출"
    why_human: "텔레센트릭 실 Side DualImage 레시피 + 실 이미지로만 검증 가능한 시각/수치 정확도"
  - test: "#4/#5 DualImage align 실패 시 lenient NG"
    expected: "패턴매칭 실패해도 abort 없이 측정 진행, 해당 측정 ALIGN_FAIL 로 NG 처리"
    why_human: "런타임 실패 주입 + Excel/UI 결과 라벨 확인 필요"
  - test: "#3 datum 기준선 slate blue 시각 확인 (14208px 대이미지)"
    expected: "magenta 기준선이 slate blue 로 표시, 길이/관통 유지, origin 십자와 단일색 통일, datum 무관 yellow 불변"
    why_human: "HALCON 렌더 화면 시각 확인 (SetColor swallow 함정 — 실제 표시 확인 필수)"
  - test: "#2 패턴 ROI 토글 ON/OFF 동작"
    expected: "chk_overlayPattern ON → 패턴1/패턴2 ROI cyan 박스 결과화면 렌더, OFF → 숨김 + 즉시 재렌더"
    why_human: "WPF 체크박스 인터랙션 + HALCON 렌더 시각 확인"
---

# Phase 57: 패턴 ROI UX & Datum 정렬 보강 Verification Report

**Phase Goal:** 패턴매칭 정렬(ALIGN)의 티칭 UX·시각화·견고성을 보강한다. 패턴 ROI 입력을 명확/안전하게 하고, datum 시각화 색상 중복을 정리하며, 매칭 실패 시 검사가 멈추지 않게 하고, 미사용 leveling 잔재를 제거한다.
**Verified:** 2026-06-19T09:05:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | (#6) leveling 멤버/메서드/INI 키가 코드에 0건 | ✓ VERIFIED | Grep across WPF_Example: all "leveling" matches are removal-marker comments only (`//260619 hbk Phase 57 #6 ... 폐기`). Zero live references to `IsLevelingReference`/`LevelingEnabled`/`TryGetLevelingAngle`/`TryComputeLevelingAngle`/`EStep.Level`. |
| 2 | (#6) 측정 상태머신 MoveZ→DatumPhase 직결 (Level step 부재) | ✓ VERIFIED | `Action_FAIMeasurement.cs:30-38` EStep enum has no `Level` member (Init/MoveZ/DatumPhase/Grab/Measure/End). MoveZ case ends with `Step = (int)EStep.DatumPhase;` (`:76`). |
| 3 | (#4) DualImage datum 이 IsPatternAlignEnabled 시 패턴매칭 align 수행 | ✓ VERIFIED | `Action_FAIMeasurement.cs:109-120` DualImage 분기 `if (datum.IsPatternAlignEnabled)` → `TryComposeAlign(datum, imgH, imgV, modelPath, ...)`. deferred 게이트 해제됨. |
| 4 | (#4) 단일 rigid transform 이 가로축(Horizontal_A/B)+세로축(Vertical) ROI 모두에 적용 | ✓ VERIFIED | `TryComposeAlign` 5-arg overload (`InspectionSequence.cs:454`) sets single `detectSvc.AlignPreTransform`, calls 2-image `TryFindDatum(refImage, refImageVertical, ...)` (`:542`). `TryFindLine` now consumes `AlignPreTransform` (`DatumFindingService.cs:1630-1654`, enlarged AABB) mirroring `TryExtractEdgePoints` (`:1804`). alignRot=0 → byte-identical 비-align 복원. |
| 5 | (#5) DualImage align 실패 시 abort 없이 MarkAlignFailed → NG(ALIGN_FAIL) | ✓ VERIFIED | `Action_FAIMeasurement.cs:119` `MarkAlignFailed(datum.DatumName)`. DatumPhase 종료 무조건 `Step = (int)EStep.Grab;` (`:182`). Measure 루프 (`:253-263`) `IsDatumFailed` → `LastSkipReason = IsAlignFailed ? "ALIGN_FAIL" : "DATUM_FAIL"` + `LastJudgement=false` + continue. No throw/FinishAction(Error) in datum-fail paths. |
| 6 | (#3) datum 기준선 slate blue 단일색 (magenta 제거, 좌표 불변) | ✓ VERIFIED | `HalconDisplayService.cs:347` `SetColor(window, "slate blue")` (RenderDatumFindResult baseline, was magenta). origin 십자 `:311` already slate blue. GetPart/DispLine 좌표 로직 무변경. Remaining magenta at `:887/:899` is RenderDatumOverlay teach palette — correctly out of scope. |
| 7 | (#1) 패턴1/패턴2 버튼 인접 배치 | ✓ VERIFIED | `MainView.xaml`: btn_drawPatternRoi(`:181`) < btn_drawPatternRoi2(`:204`) < btn_createPatternModel(`:226`) — 패턴1→패턴2→모델생성 순. |
| 8 | (#1) 패턴2 미설정 모델 생성 시 경고+override (OK=진행/Cancel=중단), 패턴1 하드 블록 유지 | ✓ VERIFIED | `MainView.xaml.cs:2802-2805` 패턴1 하드 블록 (CustomMessageBox.Show + return). `:2809-2818` 패턴2 `ShowConfirmation(... OKCancel)`; `confirm != MessageBoxResult.OK` → return (중단), else 진행(override). |
| 9 | (#2) 패턴 ROI 토글 체크박스 + 게이트 + 렌더 (SetDatumOverlayVisible 미러) | ✓ VERIFIED | `MainView.xaml:320` chk_overlayPattern (IsChecked=True, Checked/Unchecked=Chk_overlayPattern_Changed). `MainView.xaml.cs:2203-2205` handler → `SetPatternRoiOverlayVisible`. `MainResultViewerControl.xaml.cs:586` field, `:623` setter (`= visible; Render();`), `:846-859` render gate → `RenderResultRoiBoxes(..., "cyan", 2)`. |

**Score:** 9/9 supporting truths verified at code level → maps to 5/5 plan must-have requirement sets (#1–#6).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Action_FAIMeasurement.cs` | EStep.Level 제거 + DualImage align 배선 | ✓ VERIFIED | EStep enum no Level; MoveZ→DatumPhase; DualImage IsPatternAlignEnabled branch with TryComposeAlign 5-arg + MarkAlignFailed |
| `InspectionSequence.cs` | leveling 멤버 제거 + 5-arg TryComposeAlign | ✓ VERIFIED | leveling members removed (comments only); 4-arg delegates to 5-arg (`:445/454`); DualImage 2-image detect branch (`:542`) |
| `DatumFindingService.cs` | TryGetLevelingAngle 제거 + TryFindLine AlignPreTransform 소비 | ✓ VERIFIED | TryGetLevelingAngle removed; AlignPreTransform property (`:29`); TryFindLine consumes it (`:1630`, enlarged AABB `:1644-1654`) |
| `DatumConfig.cs` | IsLevelingReference 제거 (PatternRoi* 보존) | ✓ VERIFIED | IsLevelingReference removed (comment marker `:40`); PatternRoi* fields preserved (used in render gate) |
| `InspectionMasterParam.cs` | LevelingEnabled PropertyGrid 제거 | ✓ VERIFIED | removal marker `:34` |
| `InspectionRecipeManager.cs` | leveling INI save/preserve/load 키 제거 | ✓ VERIFIED | 3 키 접근 제거 (markers `:96/111/136`) |
| `PatternMatchService.cs` | stale TryGetLevelingAngle doc 참조 갱신 | ✓ VERIFIED | doc comment updated to ALIGN (`:16`) — Rule 1 auto-fix |
| `HalconDisplayService.cs` | slate blue recolor (RenderDatumFindResult) | ✓ VERIFIED | `:347` slate blue; coords unchanged |
| `MainView.xaml` | 패턴 버튼 재배치 + chk_overlayPattern | ✓ VERIFIED | button order + checkbox `:320` |
| `MainView.xaml.cs` | 패턴2 경고 + 토글 핸들러 | ✓ VERIFIED | ShowConfirmation `:2810`; Chk_overlayPattern_Changed `:2203` |
| `MainResultViewerControl.xaml.cs` | SetPatternRoiOverlayVisible + 렌더 게이트 | ✓ VERIFIED | field/setter/gate `:586/623/846` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| Action_FAIMeasurement DualImage 분기 | InspectionSequence.TryComposeAlign | IsPatternAlignEnabled 게이트 | ✓ WIRED | `:109` gate → `:112` TryComposeAlign(datum, imgH, imgV, ...) |
| TryComposeAlign | 2-image TryFindDatum | DualImage 인지 분기 | ✓ WIRED | `InspectionSequence.cs:542` |
| DatumFindingService.TryFindLine | AlignPreTransform | ROI 이동 + alignRot 회전 enlarged AABB | ✓ WIRED | `:1630-1654` (mirror of TryExtractEdgePoints `:1804`) |
| RenderDatumFindResult 기준선 | "slate blue" | magenta → slate blue recolor | ✓ WIRED | `:347` |
| InvokeCreatePatternModel | 단일 패턴 경고 | PatternRoi2_Length 판정 → ShowConfirmation OK/Cancel | ✓ WIRED | `:2809-2818` |
| MainView.chk_overlayPattern | MainResultViewerControl.SetPatternRoiOverlayVisible | Checked/Unchecked 핸들러 | ✓ WIRED | xaml `:320` → handler `:2205` → setter `:623` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| MainResultViewerControl pattern render | `_resultDatumOverlays` (List<DatumConfig>) | `SetResultDatumOverlays(datums)` (`:637-643`) from node-selection caller; reads real `PatternRoi*`/`PatternRoi2*` coords | Yes (same source as existing datum-overlay toggle) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build output exists | `ls bin/x64/Debug/DatumMeasurement.exe` | exists, 2026-06-19 17:18 (matches phase work) | ✓ PASS |
| Phase commits exist | `git log --oneline` | 40ffe36, d10c884, e4464c3, a179c22, 25f1b71, c079b4f, 4eeb71b, cf97c5b, 9a290a7 all present | ✓ PASS |
| MSBuild Debug/x64 Rebuild | (executor-claimed in all 5 SUMMARYs) | error CS 0 / 신규 warning 0 per SUMMARYs; .exe regenerated; not independently re-run here | ? SKIP (no re-run — but .exe present + code review clean) |

Note: This is a .NET Framework 4.8 + HALCON WPF project; MSBuild + HALCON runtime cannot be re-invoked reliably in this sandbox. Build correctness is corroborated by: (1) .exe present post-work, (2) code review found 0 critical/0 warning with no dangling references to removed symbols, (3) all grep checks for removed leveling symbols return comments-only.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| #6 | 57-01 | leveling 완전 제거 (코드+상태+INI, off 회귀 0) | ✓ SATISFIED (code) / ? UAT (off 회귀) | Truths 1,2; removal grep |
| #4 | 57-02 | Side DualImage 단일 공유 transform align | ✓ SATISFIED (code) / ? UAT (정확도) | Truths 3,4; TryFindLine/TryComposeAlign |
| #5 | 57-02 | 매칭 실패 lenient (ALIGN_FAIL→NG, abort 없음) | ✓ SATISFIED (code) / ? UAT (런타임) | Truth 5 |
| #3 | 57-03 | Datum slate blue 통일 (recolor only) | ✓ SATISFIED (code) / ? UAT (시각) | Truth 6 |
| #1 | 57-04 | 패턴 버튼 인접 + 단일 패턴 경고+override | ✓ SATISFIED (code) | Truths 7,8 |
| #2 | 57-05 | 패턴 ROI 표시/숨김 토글 | ✓ SATISFIED (code) / ? UAT (시각) | Truth 9 |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | — | — | No stubs, TODO/FIXME, empty returns, or hardcoded-empty render data introduced. Net-deletion phase (306 del / 154 ins per review). |

Code review (57-REVIEW.md) found 0 critical / 0 warning / 2 info. IN-01 (pattern toggle no one-time load sync — consistent today, future-maintenance note) and IN-02 (duplicate datum-list traversal — acceptable) are both non-blocking with no change required.

### Human Verification Required

1. **#6 off 레시피 회귀 0** — Load an existing recipe INI that still contains stale `LevelingEnabled`/`IsLevelingReference` keys. Expected: no load crash, measurement flow MoveZ→DatumPhase normal, leveling-off behavior unchanged. (Code path confirms ParamBase.Load ignores stale keys + explicit reads removed; runtime confirmation pending.)
2. **#4 DualImage align accuracy** — Run a telecentric Side VerticalTwoHorizontalDualImage recipe with align enabled. Expected: single transform from horizontal-image pattern match applied to both horizontal (A/B) and vertical detection ROIs; DetectedOrigin/baseline correct.
3. **#4/#5 DualImage align-fail lenient** — Force a pattern match failure on a DualImage datum. Expected: no abort; measurement proceeds; affected measurement flagged ALIGN_FAIL/NG in Excel/UI.
4. **#3 slate blue baseline visual** — View datum detection result on a large (14208px) image. Expected: baseline renders slate blue (not magenta), full-image length retained, unified with origin cross; datum-unrelated yellow unchanged. (SetColor-swallow trap means visual confirmation required.)
5. **#2 pattern ROI toggle** — Toggle chk_overlayPattern in result viewer. Expected: ON → pattern1/pattern2 cyan ROI boxes render; OFF → hidden with immediate re-render; existing datum/measure toggles unregressed.

### Gaps Summary

No code-level gaps. All 5 scope items (#1–#6) have their delivering code, wiring, and data flow verified in the actual codebase — not merely SUMMARY claims:

- Independent grep confirms every removed leveling symbol leaves only removal-marker comments (zero live references), and the EStep state machine routes MoveZ→DatumPhase directly with no Level case.
- The load-bearing #4 correctness change — `TryFindLine` consuming `AlignPreTransform` with an enlarged AABB mirroring `TryExtractEdgePoints` — is present and structurally identical (alignRot=0 reproduces the prior axis-aligned bbox, so the non-align path regresses 0).
- The 4-arg→5-arg `TryComposeAlign` delegation preserves the single-image caller, and the DualImage 2-image detect branch is wired.
- #3 recolor touches only the RenderDatumFindResult baseline (magenta→slate blue at :347); the teach-palette magenta at :887/:899 is correctly left untouched.
- #1 button reorder and pattern2 warning+override, and #2 toggle (checkbox→handler→setter→gated cyan render over the real `_resultDatumOverlays` source) are all present and wired.

Status is **human_needed** (not passed) because the phase produces runtime/visual behavior — recipe-load regression, telecentric align accuracy, lenient NG runtime behavior, and HALCON rendering color/visibility — that cannot be confirmed without running the app on real/SIMUL data (bin gitignored, HALCON runtime). The code delivers every promised capability; the 5 listed items remain to be confirmed by UAT, consistent with each SUMMARY's "UAT 잔여" note.

---

_Verified: 2026-06-19T09:05:00Z_
_Verifier: Claude (gsd-verifier)_
