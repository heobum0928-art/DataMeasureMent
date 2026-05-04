---
phase: 15
plan: 02
subsystem: Datum / Halcon Edge Measurement
tags: [datum, halcon, measurepos, measurephi, edge-selection, signature-change]
requires:
  - DatumFindingService.cs (TryFindLine + TryExtractEdgePoints + AppendEdgePointsFromStrip + 7 teach call sites + 2 runtime call sites)
  - DatumConfig.*_EdgeSelection (Plan 15-01 산출 — 6 ROI properties + EnsurePerRoiDefaults)
  - MeasurementAlgorithm.cs:130-178 (CANONICAL measurePhi + EdgeSelection 패턴)
  - FAIEdgeMeasurementService.cs:82-106 (CANONICAL direction → measurePhi 4-way)
provides:
  - AppendEdgePointsFromStrip(direction, selection, roiLabel) — 4-way explicit measurePhi mapping (TtoB=-π/2, BtoT=+π/2, RtoL=π, LtoR=0) + selection PascalCase→lower
  - TryFindLine(selection) signature — propagates selection through to MeasurePos
  - TryExtractEdgePoints(selection) signature — same
  - 9 caller sites wired with config.<ROI>_EdgeSelection (7 plan + 2 Rule 3 runtime)
  - Trace logging: dir + measurePhi (deg) + selection + edge count per strip
  - Diagnostic log on per-strip exception swallow (label + ex.Message)
affects:
  - TwoLineIntersect (TryFindDatum runtime + TryTeachTwoLineIntersect teach) — Line1, Line2
  - CircleTwoHorizontal (TryTeachCircleTwoHorizontal teach) — Horizontal_A, Horizontal_B
  - VerticalTwoHorizontal (TryTeachVerticalTwoHorizontal teach) — Vertical, Horizontal_A, Horizontal_B
tech-stack:
  added: []
  patterns:
    - "Explicit direction → measurePhi mapping (CANONICAL) replaces SmallestRectangle2 auto rp"
    - "PascalCase storage (DatumConfig) → lower-case Halcon API (selectionLower) translation at consumer boundary"
    - "Trace log every strip: dir/phi/sel/edge-count for BtoT/TtoB diagnostics"
    - "Atomic wave-2 — signature change + caller wiring + build verification in one plan"
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "All 9 TryFindLine/TryExtractEdgePoints call sites wired (not just 7) — 2 runtime sites in TryFindDatum auto-fixed under Rule 3 (blocking issue: signature change broke them)"
  - "selectionLower variable computed once at helper top, used as MeasurePos arg (mirrors FAIEdgeMeasurementService.cs:82-106 nested ternary refactored to switch-style)"
  - "Trace log includes measurePhi degrees (not radians) for human-readability — degrees match user's PropertyGrid conceptual model"
  - "Bare catch replaced with catch(Exception ex) + diagnostic Trace log — per-strip swallow policy preserved (return without rethrow)"
  - "Comment on TryFindLine doc-block (line 712 'manual effectivePhi 불필요') replaced with Phase 15 explanation — preserves history rationale while documenting correction"
metrics:
  duration: "~10 min"
  completed: "2026-04-29"
  tasks: 3
  files_modified: 1
  lines_added: 73
  lines_removed: 22
  commits: 3
---

# Phase 15 Plan 02: HALCON MeasurePos measurePhi + EdgeSelection — DatumFindingService 정합화 Summary

DatumFindingService 의 strip-loop helper `AppendEdgePointsFromStrip` 를 4-way 명시 measurePhi 매핑 + selection 인자화로 캐노니컬(MeasurementAlgorithm.cs:130-178 / FAIEdgeMeasurementService.cs:82-106)과 통일하고, 두 helper(TryFindLine, TryExtractEdgePoints) 시그니처에 selection 파라미터를 추가, 9 caller 사이트(7 plan + 2 Rule 3 runtime)에 `config.<ROI>_EdgeSelection` 을 wiring 하여 BtoT/TtoB 부호 결함 + EdgeSelection 미전파 문제를 단일 atomic wave-2 단위로 해결.

## What Changed

### Task 1: AppendEdgePointsFromStrip 시그니처 + measurePhi + selection 명시화 (commit `fe9925a`)

**Before:**
```csharp
private void AppendEdgePointsFromStrip(
    HImage image,
    double row1, double col1, double row2, double col2,
    HTuple imageWidth, HTuple imageHeight,
    double sigma, int threshold, string polarity,
    ref HTuple allRows, ref HTuple allCols)
{
    // SmallestRectangle2 → rp 자동 도출
    HOperatorSet.GenMeasureRectangle2(rr, rc, rp, rh, rw, ...);
    HOperatorSet.MeasurePos(..., polarity, "all", ...);  // 하드코딩
}
```

**After:**
```csharp
private void AppendEdgePointsFromStrip(
    HImage image,
    double row1, double col1, double row2, double col2,
    HTuple imageWidth, HTuple imageHeight,
    double sigma, int threshold, string polarity,
    string direction, string selection,        //260429 hbk Phase 15
    ref HTuple allRows, ref HTuple allCols,
    string roiLabel)                            //260429 hbk Phase 15
{
    // 4-way 명시 매핑
    double measurePhi;
    if (direction == "TtoB")      measurePhi = -Math.PI / 2.0;
    else if (direction == "BtoT") measurePhi = +Math.PI / 2.0;
    else if (direction == "RtoL") measurePhi = Math.PI;
    else                          measurePhi = 0.0;  // LtoR

    string selectionLower =
        selection == "Last" ? "last" :
        selection == "All"  ? "all"  : "first";

    HOperatorSet.GenMeasureRectangle2(rr, rc, measurePhi, rh, rw, ...);  // rp → measurePhi
    HOperatorSet.MeasurePos(..., polarity, selectionLower, ...);          // "all" → selectionLower

    // Trace log: dir + measurePhi(deg) + sel + edges
    // Bare catch → catch(Exception ex) + diagnostic Trace log
}
```

추가 변경:
- 잘못된 정당화 주석 (line 712, line 977 의 "SmallestRectangle2 가 strip Phi 자동 도출 → manual effectivePhi 불필요") 양쪽 모두 제거 + Phase 15 설명 주석으로 대체.
- 메서드 doc-block 에 CANONICAL ref 명시 (MeasurementAlgorithm.cs:130-178, FAIEdgeMeasurementService.cs:82-106, VisionAlgorithmService.cs:63-72).
- Trace log strip 단위로 dir / measurePhi(deg) / selection / edgeCount 노출 (BtoT/TtoB 부호 검증 핵심 진단 정보).

### Task 2: TryFindLine + TryExtractEdgePoints 시그니처 + 4 internal helper calls (commit `05033ea`)

두 helper 모두 동일 패턴으로 `string selection` 추가 + sanity clamp + 내부 AppendEdgePointsFromStrip 호출 4건 wiring:

| Helper | 시그니처 위치 | Sanity clamp | 내부 호출 |
|---|---|---|---|
| TryFindLine (line 717-727) | `direction` 다음, `sampleCount` 앞 | `if (string.IsNullOrEmpty(selection)) selection = "First";` | line 779-784, 793-798 (2 calls) |
| TryExtractEdgePoints (line 867-877) | 동일 | 동일 | line 919-924, 933-938 (2 calls) |

각 호출마다 `direction, selection,` + `roiLabel` 인자 추가.

### Task 3: 9 caller 사이트 wiring + 통합 빌드 검증 (commit `5fac0c8`)

| # | 호출 사이트 | 라인 | 추가 인자 | Plan? |
|---|---|---|---|---|
| 1 | TryFindDatum (runtime) Line1 | 62 | `config.Line1_EdgeSelection` | ❌ Rule 3 |
| 2 | TryFindDatum (runtime) Line2 | 84 | `config.Line2_EdgeSelection` | ❌ Rule 3 |
| 3 | TryTeachTwoLineIntersect Line1 | 203 | `config.Line1_EdgeSelection` | ✅ |
| 4 | TryTeachTwoLineIntersect Line2 | 226 | `config.Line2_EdgeSelection` | ✅ |
| 5 | TryTeachCircleTwoHorizontal H_A | 373 | `config.Horizontal_A_EdgeSelection` | ✅ |
| 6 | TryTeachCircleTwoHorizontal H_B | 395 | `config.Horizontal_B_EdgeSelection` | ✅ |
| 7 | TryTeachVerticalTwoHorizontal Vertical | 525 | `config.Vertical_EdgeSelection` | ✅ |
| 8 | TryTeachVerticalTwoHorizontal H_A | 549 | `config.Horizontal_A_EdgeSelection` | ✅ |
| 9 | TryTeachVerticalTwoHorizontal H_B | 571 | `config.Horizontal_B_EdgeSelection` | ✅ |

**삽입 위치:** 모든 사이트에서 `config.<ROI>_EdgeDirection,` 다음 / `config.<ROI>_EdgeSampleCount,` 앞 (Task 2 helper 시그니처 인자 순서 일치).

## Grep Evidence

```
$ grep -c "string direction, string selection" DatumFindingService.cs
3   # AppendEdgePointsFromStrip + TryFindLine + TryExtractEdgePoints

$ grep -c "if (string.IsNullOrEmpty(selection))" DatumFindingService.cs
2   # TryFindLine + TryExtractEdgePoints sanity clamps

$ grep -c "AppendEdgePointsFromStrip(" DatumFindingService.cs
5   # 4 calls (TryFindLine x2 + TryExtractEdgePoints x2) + 1 definition

$ grep -c "_EdgeSelection," DatumFindingService.cs
9   # 7 plan-mandated teach sites + 2 Rule 3 runtime sites

$ grep -c "Line1_EdgeSelection," DatumFindingService.cs
2   # 1 teach + 1 runtime (TryFindDatum)

$ grep -c "Line2_EdgeSelection," DatumFindingService.cs
2   # 1 teach + 1 runtime

$ grep -c "Vertical_EdgeSelection," DatumFindingService.cs
1   # VerticalTwoHorizontal teach only

$ grep -c "Horizontal_A_EdgeSelection," DatumFindingService.cs
2   # CTH + VTH teach paths

$ grep -c "Horizontal_B_EdgeSelection," DatumFindingService.cs
2   # CTH + VTH teach paths

$ grep -c "manual effectivePhi 불필요" DatumFindingService.cs
0   # 잘못된 정당화 주석 양쪽 모두 제거 확인

$ grep -c "GenMeasureRectangle2" DatumFindingService.cs | grep "measurePhi"
1   # helper 안 1 매치 (rr, rc, measurePhi, rh, rw)

$ grep -c "MeasurePos" DatumFindingService.cs | grep "selectionLower"
1   # helper 안 1 매치 (polarity, selectionLower)

$ grep -c "//260429 hbk" DatumFindingService.cs
47  # 전체 Phase 15 주석 (>= 16 expected)
```

## Build Verification

```
$ MSBuild.exe WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal
DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
```

**0 new warnings on DatumFindingService.cs.**

Pre-existing warnings (Phase 14 carry-over — not in scope of this plan):
- `Halcon\Algorithms\VisionAlgorithmService.cs(64,22): warning CS0219` — Phase 15-03 범위 (TryFindCircleByPolarSampling)
- `Device\Camera\VirtualCamera.cs(266,13): warning CS0162` — 별개 모듈
- `MSB3884: MinimumRecommendedRules.ruleset` — 프로젝트 메타 설정 (코드 영향 0)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Wired 2 additional runtime call sites**
- **Found during:** Task 3 (9 caller sites discovered via `grep "TryFindLine\("`, plan only listed 7)
- **Issue:** TryFindLine 시그니처 변경(Task 2)이 TryFindDatum 의 runtime 호출 2건(line 58, 79)을 함께 깨뜨림. Plan 범위에는 teach 7 사이트만 명시.
- **Fix:** TryFindDatum 의 Line1(line 62) + Line2(line 84) 호출에도 `config.Line1_EdgeSelection` / `config.Line2_EdgeSelection` 추가. 동일 인자 위치(`EdgeDirection,` 다음).
- **Rationale:** 빌드 회복 필수. Runtime 도 사용자가 teach 시점에 선택한 EdgeSelection 을 동일하게 따라야 함이 자연스럽다(별도 runtime 전용 EdgeSelection 필드 도입은 정당화 부재).
- **Files modified:** WPF_Example/Halcon/Algorithms/DatumFindingService.cs (line 62, 84)
- **Commit:** `5fac0c8` (Task 3 묶음에 흡수)

**2. [Style — discretion] Removed two instances of obsolete justification comment**
- **Found during:** Task 1 (grep verification revealed `manual effectivePhi 불필요` 가 line 712 doc-block + line 977 helper 본문 두 곳에 존재)
- **Issue:** Plan 은 line 977 1곳만 삭제 명시. 그러나 done criterion `grep "manual effectivePhi 불필요" DatumFindingService.cs` 0 매치를 만족하려면 line 712 doc-block 도 정리 필요. 또한 같은 잘못된 가정의 정당화 주석이므로 일관성 차원에서 둘 다 제거가 옳음.
- **Fix:** 두 곳 모두 Phase 15 설명 주석으로 대체. 양쪽 다 동일한 잘못된 가정의 정당화이므로 single fix.
- **Files modified:** WPF_Example/Halcon/Algorithms/DatumFindingService.cs (line 712-713 doc, line 978-980 helper header)
- **Commit:** `fe9925a` (Task 1 묶음에 흡수)

## TDD Gate Compliance

본 plan 은 `type: execute`, `tdd="false"` — TDD 게이트 적용 대상 아님. 회귀 검증은 Plan 15-04 UAT 에서 실데이터로 수행 예정.

## Compatibility Notes

- **Forward (BtoT/TtoB 부호 결함 해결):** 사용자가 PropertyGrid 에서 BtoT 선택 시 measurePhi=+π/2 명시 적용 → SmallestRectangle2 의 rp 자동 도출(strip 기하 만으로 도출, polarity 의도 무시)에 의존하던 부호 뒤집힘 결함 제거. 실데이터 UAT "Horizontal_A no edges found across 50 strips" 의 직접 원인 제거.
- **Forward (EdgeSelection 사용자 의도 전파):** 6 ROI 모두 사용자가 PropertyGrid 에서 First/Last/All 선택 → AppendEdgePointsFromStrip MeasurePos 까지 도달. 기존 "all" 하드코딩에 의존하던 후속 라인 피팅의 점 후보 풀이 사용자 의도대로 좁혀짐.
- **Backward:** Plan 15-01 의 EnsurePerRoiDefaults() 가 INI 부재 시 모든 6 ROI EdgeSelection 을 "First" 로 채움 → 본 plan 진입 시 helper 의 `if (string.IsNullOrEmpty(selection)) selection = "First";` sanity clamp 도 동일 기본값으로 fallback. 따라서 신규 INI 필드 미적용 환경에서도 기존 Phase 14 행동(`first` 의미)과 동등.
- **Behavior change scope:** TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal 3 알고리즘 모두 영향. Out-of-scope: VisionAlgorithmService.TryFindCircleByPolarSampling (Plan 15-03), MeasurementAlgorithm.cs / FAIEdgeMeasurementService.cs (이미 캐노니컬, 손대지 않음).
- **Plan 15-03 진입 조건:** (만약 존재한다면) DatumFindingService 의 `Circle_EdgeSelection` 적용은 본 plan 범위 아님 — Circle 검출은 TryFindCircleByPolarSampling 경유로 별도 wiring.

## Threat Flags

없음 — 본 plan 은 helper 시그니처/매핑 정합화 + 인자 wiring only. 새로운 trust boundary / network surface / file access 도입 없음. 보안 측면 영향 0.

## Self-Check: PASSED

**Files:**
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — FOUND, modified (3 commits diff: +73 lines, -22 lines).

**Commits:**
- `fe9925a` (Task 1 — AppendEdgePointsFromStrip 시그니처 + measurePhi + selection 명시화 + 정당화 주석 제거) — FOUND in `git log`.
- `05033ea` (Task 2 — TryFindLine + TryExtractEdgePoints 시그니처 + 4 internal helper calls) — FOUND in `git log`.
- `5fac0c8` (Task 3 — 9 caller sites wired + msbuild Debug/x64 PASS) — FOUND in `git log`.

**Build:** msbuild Debug/x64 PASS, 0 new warnings on DatumFindingService.cs.

**Done criteria 검증:**
- [x] AppendEdgePointsFromStrip 시그니처에 direction + selection + roiLabel 추가
- [x] 4-way direction → measurePhi 매핑 (TtoB=-π/2, BtoT=+π/2, RtoL=π, LtoR=0)
- [x] GenMeasureRectangle2 가 measurePhi 사용 (rp 아님)
- [x] MeasurePos 가 selectionLower 사용 ("all" 아님)
- [x] 잘못된 정당화 주석 2곳 모두 삭제 (grep 0 매치)
- [x] Trace 로그에 measurePhi(deg) + selection + roiLabel 포함
- [x] 빈 catch 블록에 진단 로그 추가 (per-strip swallow 정책 유지)
- [x] TryFindLine 시그니처에 string selection 추가
- [x] TryExtractEdgePoints 시그니처에 string selection 추가
- [x] 두 helper 모두 selection sanity clamp ("First" 기본)
- [x] AppendEdgePointsFromStrip 호출 4건 모두 direction + selection + roiLabel 인자 전달
- [x] 7 호출 사이트 모두 config.<ROI>_EdgeSelection 인자 전달 (+ 2 runtime Rule 3)
- [x] msbuild Debug/x64 PASS, 신규 warning 0
