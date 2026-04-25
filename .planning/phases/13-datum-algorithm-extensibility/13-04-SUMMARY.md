---
phase: 13-datum-algorithm-extensibility
plan: "04"
subsystem: Datum/EdgeParams
tags: [datum, edge-params, schema, per-roi, ini-compat, propertygrid, phase-13, hotfix-series]
status: complete
updated: "2026-04-26"
dependency_graph:
  requires: [13-03]
  provides: [Gap-PerRoiEdgeParams]
  affects:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
tech_stack:
  added: []
  patterns:
    - "5 ROI x 6 per-ROI auto-property + sentinel-0/empty EnsurePerRoiDefaults() idempotent migration"
    - "ParamBase reflection INI round-trip — new keys auto-ignored on old INI, legacy keys preserved for backward-compat"
    - "DatumFindingService TryFindLine/TryExtractEdgePoints +3 params (direction/sampleCount/trimCount) — algorithmically active after hotfix series"
    - "config.EnsurePerRoiDefaults() at all TryTeach*/TryFindDatum entry points — one-shot lazy migration"
    - "EdgeDirection -> Phi translation: TtoB/BtoT adds PI/2 and swaps Length1/Length2 for Halcon rectangle2 orientation"
    - "PhiDeg proxy property exposes Phi in degrees to PropertyGrid (degree input, radian stored)"
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "per-ROI 필드 sentinel 기본값 0/\"\" — ParamBase 기본값 폴백 + EnsurePerRoiDefaults 자동 채우기 (INI 미존재 키 -> 생성자 기본 0 -> migrate 시 글로벌 복제)"
  - "legacy 글로벌 EdgeThreshold/Sigma/EdgePolarity는 삭제 않고 [Browsable(false)] + Category(legacy) — INI 이중 저장 허용 (ParamBase reflection 자동)"
  - "EdgeDirection은 시그니처 계약 + 알고리즘 활성화 모두 완료 — TtoB/BtoT 방향은 Phi += PI/2 + Length1/Length2 swap으로 GenMeasureRectangle2에 반영 (hotfix e0f304e)"
  - "EnsurePerRoiDefaults는 idempotent — 이미 채워진 ROI는 재호출 시 무변경 (sentinel 0/\"\" 가 아니면 건드리지 않음)"
  - "EdgeDirection 글로벌 필드 미존재 -> hardcoded fallback \"LtoR\" (legacy 레시피 호환)"
  - "trimCount/sampleCount sanity clamp 추가 (hotfix 95a18a3) — 너무 큰 값이 Halcon MeasurePos 실패 유발 방지"
  - "PhiDeg PropertyGrid 프록시 (hotfix c2a3097) — DatumConfig.Phi는 radian 저장, 사용자는 도(degree) 입력"
  - "Length1/Length2 swap 버그는 Phase 12에서 잠재 — Phase 13 diagnostic logging으로 비로소 발견 (교훈)"
commits:
  - b4f5d3f  # feat: per-ROI Datum edge params (5 ROI x 6 fields) + INI compat + service wiring
  - b0582e6  # docs: preliminary SUMMARY (premature — UAT teach end-to-end not yet exercised)
  - 95a18a3  # fix: wire trimCount/sampleCount + diagnostic log + sanity clamps
  - c2a3097  # fix: expose Phi as PhiDeg in PropertyGrid (degree input)
  - 54e466a  # fix: correct Length1/Length2 swap in Datum teach + log geometry
  - e0f304e  # fix: wire EdgeDirection to GenMeasureRectangle2 Phi (TtoB/BtoT adds 90 deg + swaps Length1/Length2)
metrics:
  duration: ~4hr (including 4 UAT hotfix iterations)
  completed_date: "2026-04-26"
  tasks_completed: 3
  tasks_total: 3
  files_changed: 2
---

# Phase 13 Plan 04: per-ROI Datum Edge Parameters Summary

> **Note: Plan landed in 6 commits — 1 main implementation + 4 hotfixes during UAT.**
> The preliminary SUMMARY written at commit b0582e6 was premature — UAT had not yet exercised
> Datum teach end-to-end. This rewrite reflects the full hotfix series and final approval at e0f304e.

**One-liner:** DatumConfig에 5 ROI x 6 파라미터 = 30 신규 필드를 추가하고, EnsurePerRoiDefaults() sentinel 마이그레이션으로 기존 INI 하위호환을 보장하며, DatumFindingService 모든 호출부가 per-ROI 값을 참조하도록 와이어링했다 — 4번의 UAT 핫픽스를 거쳐 EdgeDirection이 Halcon GenMeasureRectangle2 Phi에 실제로 반영되고 티칭 end-to-end가 동작하는 상태로 최종 승인되었다.

---

## Files Changed

| File | Type | Description |
|------|------|-------------|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | modified | 30 신규 per-ROI 필드 (5 ROI x 6 param) + 10 dropdown getter (5 x 2) + EnsurePerRoiDefaults() + legacy 3 필드 [Browsable(false)] 처리 + PhiDeg degree-proxy PropertyGrid 노출 |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | modified | TryFindLine/TryExtractEdgePoints +3 파라미터 시그니처 확장 + 5 ROI 와이어링 + TryTeach*/TryFindDatum 진입부 EnsurePerRoiDefaults() 호출 + diagnostic logging + trimCount/sampleCount active + EdgeDirection -> Phi wiring |

---

## Commit History (Chronological)

| # | Hash | Type | Message |
|---|------|------|---------|
| 1 | b4f5d3f | feat | per-ROI Datum edge params (5 ROI x 6 fields) + INI compat + service wiring |
| 2 | b0582e6 | docs | preliminary SUMMARY (written before end-to-end UAT — overstated, see hotfixes below) |
| 3 | 95a18a3 | fix | wire trimCount/sampleCount + diagnostic log + sanity clamps (UAT: Line1 insufficient edges) |
| 4 | c2a3097 | fix | expose Phi as PhiDeg in PropertyGrid (degree input, radian stored internally) |
| 5 | 54e466a | fix | correct Length1/Length2 swap in Datum teach + log geometry |
| 6 | e0f304e | fix | wire EdgeDirection to GenMeasureRectangle2 Phi (TtoB/BtoT adds 90 deg + swaps L1/L2) |

---

## UAT Hotfix Series (4 Iterations)

### Hotfix 1 — trimCount/sampleCount inert + diagnostic log missing
**Commit:** 95a18a3

| Field | Detail |
|-------|--------|
| Root cause | `sampleCount` and `trimCount` were accepted in the new signature but the function body did not branch on them — both stayed inert (minimalist-hook plan note was misapplied as "never activate") |
| Symptom | Line1 returned 0 edges; user could not tell whether ROI was wrong or params were wrong because no diagnostic log existed |
| Fix | Activated `sampleCount > 0` branch in `MeasurePos` call; activated `trimCount > 0` edge-trim slice; added `Logging.PrintLog` diagnostic output showing ROI row/col/phi/length + detected edge count per ROI |
| Also | Added sanity clamps (sampleCount <= 200, trimCount < sampleCount/2) to prevent Halcon silent failure from extreme values |

### Hotfix 2 — Phi input in radians not user-friendly
**Commit:** c2a3097

| Field | Detail |
|-------|--------|
| Root cause | `DatumConfig.Line1_Phi` (and all other ROI Phi fields) stored radians; PropertyGrid exposed the raw radian value, making it impossible for a user to intuitively set horizontal/vertical angles |
| Symptom | User entered "1.5708" to mean 90 degrees; no visual feedback on whether the value was correct |
| Fix | Added `PhiDeg` proxy public properties (one per ROI) exposed in PropertyGrid with `[Category]`, storing in degrees; the underlying `Phi` field converts on get/set (`PhiDeg = Phi * 180 / Math.PI`) |

### Hotfix 3 — Length1/Length2 swap producing rotated measurement rectangle
**Commit:** 54e466a

| Field | Detail |
|-------|--------|
| Root cause | Phase 12 implementation of `TryTeachTwoLineIntersect` passed `Length1` as Halcon's second dimension parameter and `Length2` as the first — effectively rotating the measurement rectangle 90 degrees relative to user intent. This bug was latent (never triggered) until Phase 13's diagnostic logging revealed actual Halcon geometry |
| Symptom | After setting ROI phi=0 (horizontal edge), the measurement strip was oriented vertically — Halcon returned 0 or wrong-direction edges |
| Fix | Swapped `Length1`/`Length2` argument order in all `GenMeasureRectangle2` calls within `TryTeach*` methods; added geometry log showing the final row/col/phi/length1/length2 sent to Halcon |

### Hotfix 4 — EdgeDirection not wired to Halcon measurement phi
**Commit:** e0f304e

| Field | Detail |
|-------|--------|
| Root cause | `EdgeDirection` (LtoR/RtoL/TtoB/BtoT) was stored in per-ROI fields and passed to `TryFindLine` signature, but the function body did not translate it to Halcon `GenMeasureRectangle2`'s phi parameter. Horizontal directions (LtoR/RtoL) and vertical directions (TtoB/BtoT) require different phi orientations in Halcon |
| Symptom | Setting `Line1_EdgeDirection = "TtoB"` had no effect — rectangle orientation was purely determined by the stored phi field, not the logical direction intent |
| Fix | Added direction-to-phi translation logic: TtoB/BtoT adds PI/2 to phi and swaps Length1/Length2 to correctly orient the measurement strip for vertical edge detection; LtoR/RtoL leaves phi unchanged |
| Outcome | Datum teach succeeds end-to-end with EdgeDirection as the primary driver of measurement orientation |

---

## Per-ROI Parameter Catalog (5 ROI x 6 fields = 30)

| ROI | PropertyGrid Category | EdgeThreshold (int) | Sigma (double) | EdgeDirection (string) | EdgeSampleCount (int) | EdgeTrimCount (int) | EdgePolarity (string) |
|-----|----------------------|---------------------|----------------|------------------------|----------------------|--------------------|-----------------------|
| Line1 | `Datum\|Line1 Edge` | `Line1_EdgeThreshold` | `Line1_Sigma` | `Line1_EdgeDirection` | `Line1_EdgeSampleCount` | `Line1_EdgeTrimCount` | `Line1_EdgePolarity` |
| Line2 | `Datum\|Line2 Edge` | `Line2_EdgeThreshold` | `Line2_Sigma` | `Line2_EdgeDirection` | `Line2_EdgeSampleCount` | `Line2_EdgeTrimCount` | `Line2_EdgePolarity` |
| Circle | `Datum\|Circle Edge` | `Circle_EdgeThreshold` | `Circle_Sigma` | `Circle_EdgeDirection` | `Circle_EdgeSampleCount` | `Circle_EdgeTrimCount` | `Circle_EdgePolarity` |
| Horizontal A | `Datum\|Horizontal A Edge` | `Horizontal_A_EdgeThreshold` | `Horizontal_A_Sigma` | `Horizontal_A_EdgeDirection` | `Horizontal_A_EdgeSampleCount` | `Horizontal_A_EdgeTrimCount` | `Horizontal_A_EdgePolarity` |
| Horizontal B | `Datum\|Horizontal B Edge` | `Horizontal_B_EdgeThreshold` | `Horizontal_B_Sigma` | `Horizontal_B_EdgeDirection` | `Horizontal_B_EdgeSampleCount` | `Horizontal_B_EdgeTrimCount` | `Horizontal_B_EdgePolarity` |

**Algorithm activation status (post-hotfix):**
- `EdgeThreshold` / `Sigma` / `EdgePolarity` — algorithmically active from commit b4f5d3f (main impl)
- `EdgeSampleCount` / `EdgeTrimCount` — activated in hotfix 95a18a3 (previously inert)
- `EdgeDirection` — activated in hotfix e0f304e (Phi + L1/L2 swap translation)

**Sentinel defaults:** 모든 per-ROI 필드는 생성자 기본값 0 / `""` (sentinel). INI에 키가 없으면 ParamBase reflection이 생성자 기본값(0 / "") 유지 -> EnsurePerRoiDefaults() 진입 시 글로벌 값으로 채워짐.

**Dropdown getters (10개, [Browsable(false)]):**
- `<ROI>_EdgeDirectionList` -> `EdgeOptionLists.Directions` = ["LtoR", "RtoL", "TtoB", "BtoT"]
- `<ROI>_EdgePolarityList` -> `EdgeOptionLists.DatumPolarities` = ["all", "positive", "negative"]

---

## INI 하위호환 메커니즘

### EnsurePerRoiDefaults() Sentinel 로직

```csharp
// Hardcoded fallback (글로벌 EdgeThreshold/Sigma/EdgePolarity -> per-ROI 복제)
int    fbThreshold   = EdgeThreshold > 0 ? EdgeThreshold : 20;   // 글로벌 기본 20
double fbSigma       = Sigma > 0 ? Sigma : 1.0;                  // 글로벌 기본 1.0
string fbDirection   = "LtoR";                                    // legacy 글로벌에 EdgeDirection 없음
int    fbSampleCount = 20;
int    fbTrimCount   = 10;
string fbPolarity    = !string.IsNullOrEmpty(EdgePolarity) ? EdgePolarity : "all";

// 각 ROI별: sentinel(0/"")이면 fallback으로 채움 — 이미 값 있으면 무변경 (idempotent)
if (Line1_EdgeThreshold == 0) Line1_EdgeThreshold = fbThreshold;
// ... (5 ROI x 6 필드 동일 패턴)
```

### 하위호환 시나리오

| 시나리오 | INI 상태 | 동작 |
|----------|----------|------|
| Phase 12 이하 기존 INI 로드 | `EdgeThreshold=20, Sigma=1.0, EdgePolarity=all` 존재; per-ROI 키 없음 | ParamBase: 글로벌 채움, per-ROI = 0/"". EnsurePerRoiDefaults() 진입 시 5 ROI 모두 글로벌 값으로 복제 |
| 신규 INI 저장 후 재로드 | per-ROI 30 키 + 글로벌 3 키 모두 저장 (이중 저장) | 재로드 시 per-ROI 값 채워짐 -> EnsurePerRoiDefaults() sentinel 조건 미충족 -> 무변경 |
| 혼합 (일부 ROI만 편집) | `Line1_EdgeThreshold=50`만 존재, 나머지 키 없음 | Line1만 50 복원, 나머지 0 -> EnsurePerRoiDefaults()로 글로벌 값 채움 |

### Legacy 글로벌 필드 처리

기존 `EdgeThreshold` / `Sigma` / `EdgePolarity`는 삭제하지 않음 — INI 로드 경로 보존 및 EnsurePerRoiDefaults fallback 소스로 활용:

```csharp
//260425 hbk Phase 13 D-PRP-01 — Legacy 글로벌 에지 파라미터 (INI 하위호환 유지, PropertyGrid 노출 안 함)
[Category("Datum|Edge Detection (legacy)")]
[PropertyTools.DataAnnotations.Browsable(false)]
public int EdgeThreshold { get; set; } = 20;
[PropertyTools.DataAnnotations.Browsable(false)]
public double Sigma { get; set; } = 1.0;
[PropertyTools.DataAnnotations.Browsable(false)]
public string EdgePolarity { get; set; } = "all";
```

PropertyGrid에서 `Datum|Edge Detection (legacy)` 그룹은 `[Browsable(false)]`로 숨김.

---

## Service Wiring Map (3 Algorithms x per-ROI calls)

| 알고리즘 | 메서드 | Line1_* | Line2_* | Circle_* | Horizontal_A_* | Horizontal_B_* |
|----------|--------|---------|---------|----------|----------------|----------------|
| TwoLineIntersect | `TryTeachTwoLineIntersect` | TryFindLine (Line1) | TryFindLine (Line2) | — | — | — |
| CircleTwoHorizontal | `TryTeachCircleTwoHorizontal` | — | — | TryFindCircle (sigma/threshold) | TryExtractEdgePoints | TryExtractEdgePoints |
| VerticalTwoHorizontal | `TryTeachVerticalTwoHorizontal` | TryFindLine (수직 Line1) | — | — | TryExtractEdgePoints | TryExtractEdgePoints |
| Runtime (모든 알고리즘) | `TryFindDatum` | 위 매핑 동일 적용 | 위 매핑 동일 적용 | 위 매핑 동일 적용 | 위 매핑 동일 적용 | 위 매핑 동일 적용 |

**EnsurePerRoiDefaults() 호출 지점:** `TryTeachDatum` 진입부 + `TryFindDatum` 진입부 (각 1회, idempotent).

### TryFindLine 시그니처 (post-hotfix)

```csharp
//260425 hbk Phase 13 D-PRP-04 — per-ROI 에지 파라미터 적용 (direction/sampleCount/trimCount 추가)
private static bool TryFindLine(
    HImage image, HTuple imageWidth, HTuple imageHeight,
    double row, double col, double phi, double length1, double length2,
    double sigma, int threshold, string polarity,
    string direction, int sampleCount, int trimCount,    // Phase 13 추가 — 모두 algorithmically active
    out double rowBegin, out double colBegin, out double rowEnd, out double colEnd,
    out string error)
```

`TryExtractEdgePoints`도 동일 패턴으로 +3 파라미터. 초기 계획의 "minimalist hook" 설계는 hotfix 95a18a3/e0f304e에서 실제 알고리즘 활성화로 전환됨.

---

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
Output: DatumMeasurement.exe
Exit code: 0
수정 파일 대상 신규 warning: 0
기존 warning (scope 밖):
  - VirtualCamera.cs CS0162 (unreachable code)
  - VisionAlgorithmService.cs CS0219 (unused variable)
  - MSB3884 (MinimumRecommendedRules.ruleset not found)
```

---

## UAT Outcome

**최종 결과: APPROVED** (SIMUL_MODE Debug/x64 육안 검증, 5회 반복 끝 최종 승인 — 2026-04-26)

### UAT 승인 항목 (Task 3 — 12 시나리오 + 5 hotfix 반복)

| # | 시나리오 | 결과 |
|---|---------|------|
| 1 | 기존 INI 로드 -> 5 ROI 그룹에 글로벌 값(20/1.0/"all"/"LtoR"/20/10) 자동 채워짐 | PASS |
| 2 | EnsurePerRoiDefaults 재호출 시 이미 채워진 값 무변경 (idempotency) | PASS |
| 3 | PropertyGrid에서 `Line1_EdgeThreshold=50` 변경 -> Recipe Save -> 재시작 -> 50 복원 | PASS |
| 4 | PropertyGrid에 5 ROI Edge 그룹 (Line1/Line2/Circle/HorizontalA/HorizontalB) 각 6항목 노출 | PASS |
| 5 | `Datum\|Edge Detection (legacy)` 그룹 숨김 ([Browsable(false)] 확인) | PASS |
| 6 | EdgeDirection 드롭다운 [LtoR/RtoL/TtoB/BtoT], EdgePolarity 드롭다운 [all/positive/negative] | PASS |
| 7 | TwoLineIntersect: Line1 EdgeThreshold 변경 -> 검출 라인 변화. Line2 독립 유지 | PASS |
| 8 | CircleTwoHorizontal: Horizontal_A EdgeSampleCount 변경 -> 검출 robustness 변화. Horizontal_B 독립 | PASS |
| 9 | VerticalTwoHorizontal: Line1 EdgePolarity "all"->"positive"->"negative" -> 검출 결과 차이 | PASS |
| 10 | Phase 13-01 ValidateHorizontalVerticalAngles 게이트 정상 동작 (per-ROI 변경 후 회귀 없음) | PASS |
| 11 | Phase 13-02 btn_testFindDatum: TryFindDatum per-ROI 파라미터 사용 — 테스트 동작 정상 | PASS |
| 12 | Phase 13-03 ROI 드래그 이동 -> InvokeTryTeachDatumForEdit -> per-ROI 파라미터로 재티칭 OK | PASS |

**Hotfix 반복:** 시나리오 12 통과 전 4번의 hotfix 반복 (95a18a3 -> c2a3097 -> 54e466a -> e0f304e). 최종 승인은 e0f304e (EdgeDirection->Phi wiring) 이후.

---

## 13-05 이월 항목 (시각화)

| 항목 | 내용 | 이월 이유 |
|------|------|----------|
| 검출 라인 외삽 | `EXTEND_PX` helper + `DrawExtendedLine` — 라인을 이미지 가장자리까지 연장 | 13-04 scope 밖 (시각화 전용) |
| Raw 검출 에지점 마커 | 5 ROI별 raw 에지점 색상 점 표시 (`DatumConfig` volatile HTuple 필드 필요) | 13-04 scope 밖 |
| RefOrigin / Angle / CircleCenter / Radius 텍스트 | 캔버스 옆 좌표 숫자 라벨 (`label_datumRefCoords`) | 13-04 scope 밖 |

---

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] trimCount/sampleCount inert — newly plumbed params had no effect**
- **Found during:** UAT Task 3 (first iteration)
- **Issue:** The plan note "알고리즘 변경 최소화" was applied too conservatively — sampleCount and trimCount were received in the function signature but the body never branched on them
- **Fix:** Activated both params in MeasurePos / edge-trim logic with sanity clamps; added diagnostic logging
- **Files modified:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- **Commit:** 95a18a3

**2. [Rule 1 - Bug] Phi stored in radians, PropertyGrid shows unintelligible values**
- **Found during:** UAT Task 3 (second iteration)
- **Issue:** Users cannot intuitively set ROI orientation via radian values in PropertyGrid
- **Fix:** PhiDeg degree-proxy public properties added; PropertyGrid exposes degree values, internal storage remains radians
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`
- **Commit:** c2a3097

**3. [Rule 1 - Bug] Length1/Length2 swap — latent Phase 12 bug surfaced by Phase 13 diagnostic logging**
- **Found during:** UAT Task 3 (third iteration)
- **Issue:** Phase 12's `TryTeachTwoLineIntersect` passed Length1/Length2 in wrong order to Halcon GenMeasureRectangle2, rotating the measurement strip 90 degrees relative to intent. This was masked because Datum teach was never exercised end-to-end before Phase 13 added diagnostic logging showing the geometry sent to Halcon
- **Fix:** Swapped argument order in all GenMeasureRectangle2 calls + added geometry log
- **Files modified:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- **Commit:** 54e466a

**4. [Rule 1 - Bug] EdgeDirection not translated to Halcon Phi — direction field was effectively decorative**
- **Found during:** UAT Task 3 (fourth iteration)
- **Issue:** EdgeDirection (LtoR/RtoL/TtoB/BtoT) was plumbed through the call stack but TryFindLine body never translated it into GenMeasureRectangle2 phi. The plan's "minimalist hook" intent was misread as "never activate EdgeDirection in this plan"
- **Fix:** Added direction-to-phi translation: TtoB/BtoT adds PI/2 to phi and swaps Length1/Length2; LtoR/RtoL leaves phi unchanged
- **Files modified:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- **Commit:** e0f304e

---

## Lessons Learned

**1. Diagnostic logging is the highest-leverage addition in a Halcon integration plan.**
Without the diagnostic log added in 95a18a3, the user had no way to distinguish between "ROI is in the wrong position" and "params are passed but ignored" — both produce 0 detected edges. Adding a single `Logging.PrintLog` line showing row/col/phi/length1/length2/detected-edge-count per ROI cut debugging time from hours to minutes for hotfixes 3 and 4.

**2. "알고리즘 변경 최소화 원칙"이 오적용되면 plumbing-without-wiring 패턴을 만든다.**
The plan correctly said "minimize algorithm changes" as a scope guard against over-engineering. But it was misapplied to mean "receive params in the signature but never use them." The correct reading is: "don't add new algorithm modes (e.g. smoothing window, selection mode) — but DO wire the params you explicitly plumb." Three of the four hotfixes corrected plumbing-without-wiring errors.

**3. Phase 12에서 잠재했던 Length1/Length2 swap 버그는 Phase 13의 diagnostic logging이 없었으면 발견하지 못했을 것이다.**
The swap bug existed since Phase 12's initial `TryTeachTwoLineIntersect` implementation. It was invisible because Datum teach was never run against a real image with a known-good ground truth until Phase 13. Adding debug geometry output to the log made the Halcon-side geometry observable for the first time, immediately revealing the swap.

**4. UAT를 "PropertyGrid 에서 값 편집 가능 여부" 수준에서 멈추지 말고 "실제 알고리즘 결과가 파라미터 변화에 반응하는가"까지 검증해야 한다.**
The original UAT gate (Task 3 in the plan) checked PropertyGrid visibility and INI round-trip. Those all passed in the first iteration. The deeper verification — that changing EdgeDirection actually changes which edges Halcon detects — required four more hotfix cycles. UAT gates should include at least one "change param X, observe result Y changes" check per algorithmically-active parameter.

---

## Known Stubs

None — all per-ROI parameters (EdgeThreshold, Sigma, EdgePolarity, EdgeSampleCount, EdgeTrimCount, EdgeDirection) are algorithmically active as of commit e0f304e. The preliminary SUMMARY at b0582e6 incorrectly documented direction/sampleCount/trimCount as "minimalist hook — Phase 14+심화 예정"; this is no longer accurate.

---

## Self-Check

- [x] `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` 수정 존재 (30 per-ROI 필드 + 10 dropdown getter + EnsurePerRoiDefaults() + PhiDeg proxy + legacy [Browsable(false)])
- [x] `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` 수정 존재 (TryFindLine/TryExtractEdgePoints +3 파라미터 + 5 ROI 와이어링 + EnsurePerRoiDefaults() 호출 + EdgeDirection->Phi wiring + trimCount/sampleCount active + diagnostic logging)
- [x] 커밋 b4f5d3f 존재 (main impl)
- [x] 커밋 95a18a3 존재 (hotfix 1: trimCount/sampleCount + diagnostic log)
- [x] 커밋 c2a3097 존재 (hotfix 2: PhiDeg PropertyGrid)
- [x] 커밋 54e466a 존재 (hotfix 3: Length1/Length2 swap fix)
- [x] 커밋 e0f304e 존재 (hotfix 4: EdgeDirection->Phi wiring)
- [x] msbuild Debug/x64 exit 0
- [x] 신규 warning 0 (수정 2 파일 기준)
- [x] legacy config.EdgeThreshold / Sigma / EdgePolarity 직접 사용 잔존 0 (모두 per-ROI로 교체)
- [x] EnsurePerRoiDefaults() TryTeachDatum + TryFindDatum 진입부 각 1회 호출
- [x] UAT 12 시나리오 + 5 hotfix 반복 끝 APPROVED (최종 e0f304e)
- [x] 4건 deviations 문서화 완료
- [x] 교훈(Lessons Learned) 4건 기록 완료
- [x] 13-05 이월 항목 문서화 완료

## Self-Check: PASSED
