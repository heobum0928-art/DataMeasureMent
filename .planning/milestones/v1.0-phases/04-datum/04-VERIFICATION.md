---
phase: 04-datum
verified: 2026-04-09T12:00:00Z
status: gaps_found
score: 10/13 must-haves verified
overrides_applied: 0
gaps:
  - truth: "HalconDisplayService renders Datum Line1/Line2 ROI overlays on canvas"
    status: partial
    reason: "RenderDatumOverlay method exists and is substantive, but it is never called from any call site (MainView, HalconViewerControl, MainResultViewerControl). The method is orphaned — wired into no rendering pipeline."
    artifacts:
      - path: "WPF_Example/Halcon/Display/HalconDisplayService.cs"
        issue: "RenderDatumOverlay(line 314) is defined but zero callers found anywhere in the codebase"
    missing:
      - "Call site: RenderDatumOverlay must be invoked from HalconViewerControl or MainResultViewerControl or MainView when a Datum node is selected or when the canvas is refreshed"

  - truth: "Datum node appears in InspectionListView tree under each Action/Shot node"
    status: partial
    reason: "Datum node IS created during CreateSequenceNode (tree load/rebuild). However, AddShotToSequence (the runtime UI path for creating a new Shot) inserts only a FAI child node into the tree — it does not insert the Datum child node. Shots created at runtime via the Add button will display in the tree without a Datum node until the next full RebuildTree call."
    artifacts:
      - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
        issue: "AddShotToSequence (lines 367-399) builds shotNode.Children with only faiChildNode — no Datum child node is added, inconsistent with CreateSequenceNode logic"
    missing:
      - "In AddShotToSequence, after 'shotNode.Children.Add(faiChildNode)', add a Datum child node: var datumNode = new CompositeNode { Name = \"Datum\", NodeType = ENodeType.Datum, ParamData = shot.Datum, ... }; shotNode.Children.Add(datumNode) — insert before faiChildNode to match CreateSequenceNode ordering"

  - truth: "ALG-05 (Datum 보정) requirement is defined and satisfied"
    status: failed
    reason: "ALG-05 does not exist in REQUIREMENTS.md. The requirements file defines ALG-01 through ALG-04 only. Phase 4 plans declare 'requirements: [ALG-05]' but this ID is absent from the requirements traceability table. The REQUIREMENTS.md traceability table maps SEQ-01..SEQ-04 to Phase 4, not ALG-05. The requirement ID is orphaned — present in plan frontmatter but defined nowhere."
    artifacts:
      - path: ".planning/REQUIREMENTS.md"
        issue: "ALG-05 not present anywhere in the file; traceability table maps Phase 4 to SEQ-01..SEQ-04, not ALG-05"
    missing:
      - "Either: add ALG-05 (Datum 보정) to REQUIREMENTS.md v1 requirements section and traceability table, OR correct the plan frontmatter to reference the actual requirement ID covering Datum (currently none is defined)"
---

# Phase 4: Datum 기준좌표계 Verification Report

**Phase Goal:** 사용자가 Datum(기준 에지 Line 2개)을 티칭하면, 런타임에서 제품 위치/회전 편차를 자동 보정하여 FAI ROI가 정확한 위치에서 측정된다
**Verified:** 2026-04-09
**Status:** gaps_found
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DatumConfig stores Line1/Line2 ROI (Rectangle2 format) and reference origin/angle | VERIFIED | `DatumConfig.cs` lines 16-46: Line1_Row/Col/Phi/Length1/Length2, Line2_*, RefOriginRow/Col/AngleRad all present as auto-serializable properties |
| 2 | ShotConfig owns a DatumConfig instance as Datum property | VERIFIED | `ShotConfig.cs` line 24-26: `[Browsable(false)] public DatumConfig Datum { get; set; }` present |
| 3 | InspectionRecipeManager saves DatumConfig to [SHOT_N_DATUM] INI section and loads it back | VERIFIED | `InspectionRecipeManager.cs` lines 85-90 (Save) and 128-133 (Load): `SHOT_{s}_DATUM` pattern confirmed |
| 4 | Existing recipes without DATUM section load without error (backward compatible) | VERIFIED | `InspectionRecipeManager.cs` line 130: `if (loadFile.ContainsSection(datumSection))` guards creation; missing section leaves `Datum` as auto-created (non-null from AddShot) but unconfigured (IsConfigured=false) |
| 5 | DatumFindingService finds two lines, computes intersection and angle, returns hom_mat2d transform | VERIFIED | `DatumFindingService.cs`: TryFindLine (GenMeasureRectangle2→MeasurePos→FitLineContourXld), IntersectionLl, HomMat2dTranslate, HomMat2dRotate — full pipeline present |
| 6 | DatumConfig.IsConfigured=false returns identity transform (no-op) | VERIFIED | `DatumFindingService.cs` lines 31-34: `if (config == null || !config.IsConfigured) { return true; }` with identity pre-initialized at line 28 |
| 7 | Action_FAIMeasurement calls FindDatum before the FAI measurement loop | VERIFIED | `Action_FAIMeasurement.cs` lines 80-98: DatumFindingService.TryFindDatum called before `foreach (var fai in ShotParam.FAIList)` at line 100 |
| 8 | Datum failure causes all child FAIs to be marked NG without measuring | VERIFIED | `Action_FAIMeasurement.cs` lines 86-94: `fai.ClearResult()` loop + `pMyContext.AllPass = false` + `Step = EStep.End; break` |
| 9 | FAIEdgeMeasurementService applies hom_mat2d transform to ROI before MeasurePos | VERIFIED | `FAIEdgeMeasurementService.cs` lines 57-74: AffineTransPoint2d applied to ROI_Row/Col, rotation extracted and applied to roiPhi and measurePhi |
| 10 | Unconfigured Datum (IsConfigured=false or Datum=null) results in identity transform (Phase 3 behavior preserved) | VERIFIED | `Action_FAIMeasurement.cs` line 82: `datumTransform = null`; `FAIEdgeMeasurementService.cs` original overload delegates to `TryMeasure(image, fai, null, out result)`. Both code paths preserve Phase 3 ROI behavior |
| 11 | Selecting Datum node shows DatumConfig in PropertyGrid | VERIFIED | `InspectionListView.xaml.cs` lines 200-208: `if (itemParam is ParamBase)` → `SetParam()` — DatumConfig inherits ParamBase so PropertyGrid binding is automatic. Line 217: `ENodeType.Datum` branch disables CRUD buttons and calls ClearResults |
| 12 | HalconDisplayService renders Datum Line1/Line2 ROI overlays on canvas | FAILED | `RenderDatumOverlay` exists (line 314) and is fully implemented with DispRectangle2 + origin cross. However, grep across all .cs files finds ZERO callers. The method is orphaned — it is never invoked from any rendering pipeline |
| 13 | Datum node appears in InspectionListView tree under each Action/Shot node | PARTIAL | `CreateSequenceNode` correctly adds Datum node (lines 73-84 of InspectionListViewModel.cs). BUT `AddShotToSequence` in InspectionListView.xaml.cs (lines 377-394) builds the runtime-inserted Shot node with only a FAI child — no Datum child node is added. New Shots created via the UI add button have no Datum tree node until RebuildTree is called. |

**Score:** 10/13 truths verified (11 if partial on #13 is counted as passing)

---

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | SEQ-01..04 requirements listed against Phase 4 in traceability table | Phase 5 | Phase 5 goal: "검사 시작 버튼 한 번으로 전체 Shot 순회 Grab과 FAI 측정이 자동 실행되고, 최종 판정 결과가 TCP로 호스트에 전송된다" covers SEQ-01..04 |

Note: The REQUIREMENTS.md traceability table incorrectly maps SEQ-01..04 to Phase 4. These are actually Phase 5 items. This is a documentation inconsistency, not a code gap.

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | Datum data model with Line ROI fields, reference values, and ParamBase serialization | VERIFIED | 17 serializable fields + 2 runtime-only. `class DatumConfig : ParamBase` confirmed |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | Halcon-based datum finding with line fitting, intersection, and hom_mat2d computation | VERIFIED | TryFindDatum, TryTeachDatum, TryFindLine all implemented with correct Halcon pipeline |
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | DatumConfig ownership on ShotConfig | VERIFIED | `public DatumConfig Datum { get; set; }` at line 26. ClearAllResults resets Datum state |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | Datum INI save/load in SHOT_N_DATUM sections | VERIFIED | Save (line 85-90), Load (lines 128-133), AddShot auto-creates Datum (line 24) |
| `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` | TryMeasure overload accepting HTuple transform | VERIFIED | Line 34: `public bool TryMeasure(HImage image, FAIConfig fai, HTuple transform, out FAIEdgeMeasurementResult result)`. Original overload delegates to it with null |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | FindDatum call before FAI loop | VERIFIED | Lines 80-98: DatumFindingService instantiated, TryFindDatum called, failure handled, transform passed to TryMeasure |
| `WPF_Example/UI/ControlItem/Node.cs` | ENodeType.Datum enum value | VERIFIED | Line 16: `Datum, //260409 hbk Phase 4: Datum node type (D-09)` and ImageSource case at line 45 |
| `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` | Datum tree node creation | VERIFIED | Lines 73-84: Datum CompositeNode created with `ENodeType.Datum`, `ParamData = shot.Datum`, added before FAI nodes |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | Datum node selection handling for PropertyGrid | VERIFIED | Lines 216-223: `ENodeType.Datum` branch present, disables CRUD buttons, PropertyGrid binding via existing SetParam pattern |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | Datum Line ROI overlay rendering | PARTIAL | `RenderDatumOverlay` is fully implemented (lines 314-367) but has no callers — orphaned method |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| ShotConfig.cs | DatumConfig.cs | Datum property | WIRED | `public DatumConfig Datum` confirmed; ClearAllResults accesses Datum.CurrentTransform |
| InspectionRecipeManager.cs | DatumConfig.cs | Save/Load calls | WIRED | `shot.Datum.Save(saveFile, datumSection)` and `shot.Datum.Load(loadFile, datumSection)` confirmed |
| DatumFindingService.cs | DatumConfig.cs | TryFindDatum parameter | WIRED | `DatumConfig config` parameter with `config.IsConfigured`, `config.Line1_Row`, etc. |
| Action_FAIMeasurement.cs | DatumFindingService.cs | TryFindDatum call | WIRED | `datumService.TryFindDatum(image, ShotParam.Datum, out datumTransform, out datumError)` at line 85 |
| Action_FAIMeasurement.cs | FAIEdgeMeasurementService.cs | TryMeasure with transform | WIRED | `service.TryMeasure(image, fai, datumTransform, out r)` at line 103 |
| FAIEdgeMeasurementService.cs | HOperatorSet.AffineTransPoint2d | ROI coordinate transform | WIRED | Line 62: `HOperatorSet.AffineTransPoint2d(transform, fai.ROI_Row, fai.ROI_Col, out transRow, out transCol)` |
| InspectionListViewModel.cs | Node.cs | ENodeType.Datum | WIRED | Line 77: `NodeType = ENodeType.Datum` in CreateSequenceNode |
| HalconDisplayService.cs | (MainView/caller) | RenderDatumOverlay call site | NOT_WIRED | No caller found anywhere in the codebase |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| Action_FAIMeasurement.cs | datumTransform (HTuple) | DatumFindingService.TryFindDatum → Halcon HomMat2dTranslate/Rotate | Yes — computed from real image edge detection | FLOWING |
| FAIEdgeMeasurementService.cs | roiRow/roiCol/roiPhi | AffineTransPoint2d applied to fai.ROI_* using datumTransform | Yes — transform is either real or null (identity fallback) | FLOWING |
| HalconDisplayService.RenderDatumOverlay | datum.Line1_Row etc. | DatumConfig fields (from PropertyGrid/INI) | Yes — method reads real fields | HOLLOW (method never called) |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — no runnable entry points without Halcon hardware or SIMUL_MODE images. All verifications are static code analysis.

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| ALG-05 | 04-01-PLAN.md, 04-02-PLAN.md | Datum 보정 | FAILED | ALG-05 does not exist in REQUIREMENTS.md. The ID is referenced in plan frontmatter but is not defined anywhere in the requirements document. SEQ-01..04 are what REQUIREMENTS.md maps to Phase 4, but those are properly Phase 5 items. |

**Orphaned requirements from REQUIREMENTS.md traceability table mapped to Phase 4:**
- SEQ-01, SEQ-02, SEQ-03, SEQ-04 are listed as "Phase 4 | Pending" in the traceability table — but Phase 4 plans do not claim these IDs, and the roadmap Phase 5 covers them. This is a documentation error in REQUIREMENTS.md traceability, not a code gap.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `DatumFindingService.cs` | 75, 163 | `if (isOverlapping.I == 1)` to detect parallel lines | Warning | WR-01 from code review: `IntersectionLl` sets isOverlapping=1 for collinear lines, not parallel. Parallel lines produce infinity coordinates that are not caught — silently stored as RefOriginRow/Col or passed to transform, causing garbage measurements. Not a blocker for most real-world usage but a correctness defect. |
| `FAIEdgeMeasurementService.cs` | 67 | `Math.Atan2(-transform[1].D, transform[0].D)` | Warning | WR-03 from code review: hom_mat2d rotation extraction uses incorrect element index. `transform[1]` is h01 which equals -sin(theta) only for pure rotation; for a composed translate+rotate matrix, index 3 (h10=sin(theta)) should be used. Causes systematic ROI angle bias proportional to translation offset. |
| `Action_FAIMeasurement.cs` | 88-94 | Datum failure branch does not set `LastFindSucceeded = false` | Warning | WR-05 from code review: stale `true` value persists across inspections when Datum find fails after a previous success |
| `InspectionListView.xaml.cs` | 164-166 | `for` loop returns on first iteration unconditionally | Info | WR-04 from code review: dead loop — always returns `sequence[i].ID` on i=0. Works correctly but misleading |
| `HalconDisplayService.cs` | 314-367 | `RenderDatumOverlay` has no callers | Blocker | Canvas overlay for Datum is never triggered. Must-have truth #12 (canvas overlay rendering) is not achieved at runtime |

---

### Human Verification Required

#### 1. Datum Tree Node Visibility at Runtime

**Test:** Load a recipe with Shots, open InspectionListView in editable mode, verify that each Shot node has a "Datum" child node appearing before FAI nodes in the tree
**Expected:** Datum node visible as first child under each Shot/Action node with the layout.png icon
**Why human:** Tree node rendering requires running the WPF application

#### 2. Datum PropertyGrid Binding

**Test:** Click the Datum node in the tree, verify that PropertyGrid shows DatumConfig fields (Line1 ROI group, Line2 ROI group, Reference group, Edge Detection group, Status group) and that edits persist
**Expected:** All 17 serializable DatumConfig fields visible and editable
**Why human:** PropertyGrid display requires running the UI

#### 3. Datum Canvas Overlay (CRITICAL — currently not wired)

**Test:** After fixing the RenderDatumOverlay call-site gap, select a Datum node and verify that Line1/Line2 rectangles appear on the canvas in blue, and the reference origin cross appears in magenta when IsConfigured=true
**Expected:** Two Rectangle2 ROI overlays visible; magenta cross at RefOriginRow/Col when configured
**Why human:** Canvas rendering requires visual inspection; also requires the orphan gap to be fixed first

#### 4. Datum Missing from New Shot Added via UI Button

**Test:** Click Add Shot button, verify the newly added Shot node in the tree has a Datum child node
**Expected:** Datum node appears immediately under the new Shot (before FAI node)
**Why human:** Tree insertion behavior requires running the UI; also requires the AddShotToSequence gap to be fixed first

---

### Gaps Summary

**3 gaps blocking full goal achievement:**

**Gap 1 — RenderDatumOverlay not wired (Blocker):** The Datum canvas overlay method (`HalconDisplayService.RenderDatumOverlay`) is fully implemented but has zero callers. The 6th success criterion ("InspectionListView 트리에 Datum 노드가 표시되고 PropertyGrid에서 편집 가능하다") partially passes (tree + PropertyGrid work), but the canvas overlay component of Phase 4's display story (D-12) is completely inert. Fix: add a call site in `HalconViewerControl` or `MainResultViewerControl` when `ENodeType.Datum` is selected.

**Gap 2 — AddShotToSequence missing Datum node (Warning):** The `CreateSequenceNode` path correctly adds a Datum child node. The `AddShotToSequence` runtime path (used when clicking the Add Shot button) builds the tree node manually and omits the Datum child. Newly created Shots display without a Datum tree node until RebuildTree is invoked. Fix: add a Datum CompositeNode to `shotNode.Children` in `AddShotToSequence`, matching the pattern in `CreateSequenceNode`.

**Gap 3 — ALG-05 requirement ID not in REQUIREMENTS.md (Documentation):** Both plans declare `requirements: [ALG-05]` but this ID does not exist anywhere in `REQUIREMENTS.md`. The requirement for Datum coordinate correction has no formal definition. This does not affect runtime behavior but breaks requirements traceability. Fix: add `**ALG-05**: Datum 기준좌표계로 제품 위치/회전 편차를 자동 보정한다` to the requirements document and update the traceability table.

**Code-review warnings that affect correctness (not blocking phase goal, but real defects):**
- WR-01: `IntersectionLl` parallel-line detection logic is inverted (infinity coords not caught)
- WR-03: hom_mat2d rotation extraction uses wrong element index (systematic angle bias)
- WR-05: `LastFindSucceeded` not reset in Datum failure path

---

_Verified: 2026-04-09_
_Verifier: Claude (gsd-verifier)_
