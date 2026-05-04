# Phase 10 Verification: Datum 정확성 결함 수정

**Phase:** 10-datum-defects
**Requirements:** ALG-05 (Datum 정확성 보강)
**Verified:** 2026-04-23
**Method:** Code review + SIMUL_MODE runtime smoke test (no unit-test framework in project — per 10-CONTEXT.md D-10)

## Success Criteria Mapping (from ROADMAP §Phase 10)

| # | Success Criterion | Warning | Status | Evidence |
|---|-------------------|---------|--------|----------|
| 1 | DatumFindingService 평행선 검출이 올바른 isOverlapping 분기를 사용하여 무한 좌표를 걸러낸다 | WR-01 | Resolved (10-01) | §WR-01 below |
| 2 | FAIEdgeMeasurementService hom_mat2d 회전 추출이 올바른 행렬 인덱스를 사용 | WR-03 | Resolved (10-01) | §WR-03 below |
| 3 | Datum 실패 경로에서 LastFindSucceeded가 false로 리셋된다 | WR-05 | Structurally resolved (Phase 6) | §WR-05 below |

## WR-01: IntersectionLl 평행선 가드

### Issue (from 04-REVIEW.md §WR-01)

`HOperatorSet.IntersectionLl`의 기존 가드는 `isOverlapping.I == 1`만 검사했다. 이 플래그는 "두 선이 동일(collinear/identical)"한 경우에만 1로 세팅되며, 평행이지만 겹치지 않는 경우에는 `isOverlapping == 0`인 상태로 교점 좌표가 `±Infinity`로 반환된다. 따라서 기존 코드는 평행선 케이스를 걸러내지 못한 채 `curRow/curCol = ±∞`를 아래 변환 계산(`HomMat2dTranslate`, `HomMat2dRotate`)에 주입하여 NaN 오염을 유발할 수 있었다.

### Fix applied (plan 10-01)

Three sites modified. Before / after:

**Site 1 — `DatumFindingService.TryFindDatum` (DatumFindingService.cs line 75-87)**

Before:
```csharp
if (isOverlapping.I == 1) { error = "Lines are parallel, no intersection"; return false; }
```

After (verbatim from current file, lines 75-87):
```csharp
                // IntersectionLl: isOverlapping==1 means lines are the SAME (collinear). //260423 hbk WR-01
                // For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity. //260423 hbk WR-01
                if (isOverlapping.I == 1) //260423 hbk WR-01
                {
                    error = "Lines are collinear (identical), no unique intersection"; //260423 hbk WR-01
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) || //260423 hbk WR-01
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D)) //260423 hbk WR-01
                {
                    error = "Lines are parallel, intersection is at infinity"; //260423 hbk WR-01
                    return false;
                }
```

**Site 2 — `DatumFindingService.TryTeachDatum` (DatumFindingService.cs line 171-183)**

Before:
```csharp
if (isOverlapping.I == 1) { error = "Lines are parallel, no intersection"; return false; }
```

After (verbatim from current file, lines 171-183):
```csharp
                // IntersectionLl: isOverlapping==1 means lines are the SAME (collinear). //260423 hbk WR-01
                // For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity. //260423 hbk WR-01
                if (isOverlapping.I == 1) //260423 hbk WR-01
                {
                    error = "Lines are collinear (identical), no unique intersection"; //260423 hbk WR-01
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) || //260423 hbk WR-01
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D)) //260423 hbk WR-01
                {
                    error = "Lines are parallel, intersection is at infinity"; //260423 hbk WR-01
                    return false;
                }
```

**Site 3 — `VisionAlgorithmService.IntersectLines` (VisionAlgorithmService.cs line 274-282, static utility)**

Before:
```csharp
if (isOverlapping.I == 1) return false;
intRow = iRow.D;
intCol = iCol.D;
return true;
```

After (verbatim from current file, lines 274-282 — signature and error-less contract preserved):
```csharp
                if (isOverlapping.I == 1) return false; //260423 hbk WR-01 collinear
                if (double.IsInfinity(iRow.D) || double.IsInfinity(iCol.D) || //260423 hbk WR-01 parallel guard
                    double.IsNaN(iRow.D) || double.IsNaN(iCol.D)) //260423 hbk WR-01
                {
                    return false; //260423 hbk WR-01
                }
                intRow = iRow.D;
                intCol = iCol.D;
                return true;
```

### Verification

- **Code review:** The collinear (`isOverlapping.I==1`) and parallel (`IsInfinity/IsNaN` on `curRow`/`curCol`) cases are now handled by two distinct branches with two distinct log messages (per D-04). The old generic `"Lines are parallel, no intersection"` string has been removed at both DatumFindingService sites. The `IntersectLines` static utility preserves its `bool` + `out` signature with no `error` string (D-04).
- **SIMUL_MODE runtime smoke test (D-10):**
  - Use `SIMUL_MODE` Debug/x64 build (symbol enabled per `DatumMeasurement.csproj`).
  - Load a recipe containing a DatumConfig whose two lines can be teased into near-parallel configuration (adjust `Line2_Phi` to match `Line1_Phi` ± 1°).
  - Run Datum teaching + runtime find; confirm `Logging` output now contains `"Lines are parallel, intersection is at infinity"` or `"Lines are collinear (identical), no unique intersection"` instead of the previous generic `"Lines are parallel, no intersection"` or silent infinity propagation.

## WR-03: hom_mat2d 회전 추출 인덱스

### Issue (from 04-REVIEW.md §WR-03)

Halcon `hom_mat2d`는 9-요소 행-우선 행렬 `[h00, h01, h02, h10, h11, h12, h20, h21, h22]`이다. 순수 회전 행렬의 회전각은 `atan2(h10, h00)`으로 복원되며, 인덱스 0과 3이 이에 해당한다. 기존 코드는 `Math.Atan2(-transform[1].D, transform[0].D)` (인덱스 1 = h01)을 사용했는데, `HomMat2dRotate`가 비-원점 중심으로 합성된 경우 h01은 translation 성분과 섞여 오염된다. 따라서 회전각이 병진 변위에 의해 잘못 추출되어 FAI ROI의 회전 보정이 부정확해질 수 있었다.

### Fix applied (plan 10-01)

File: `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` line 66-70

Before:
```csharp
// Extract rotation component: transform[0]=cos(theta), transform[1]=-sin(theta)
double rotAngle = Math.Atan2(-transform[1].D, transform[0].D);
roiPhi = fai.ROI_Phi + rotAngle;
```

After (verbatim from current file, lines 66-70):
```csharp
                        // hom_mat2d layout: [h00, h01, h02, h10, h11, h12, h20, h21, h22] //260423 hbk WR-03
                        // Indices 0 (h00=cos θ) and 3 (h10=sin θ) are translation-invariant; //260423 hbk WR-03
                        // index 1 (h01) is contaminated when HomMat2dRotate uses a non-origin center. //260423 hbk WR-03
                        double rotAngle = Math.Atan2(transform[3].D, transform[0].D); //260423 hbk WR-03
                        roiPhi = fai.ROI_Phi + rotAngle;
```

### Verification

- **Code review:** Indices 0 (h00) and 3 (h10) are the translation-invariant rotation components of a Halcon `hom_mat2d`; the corrected expression is mathematically identical to the old one for pure rotations about the origin, and correct for rotations composed with translation/non-origin rotation centers (per D-06). The hom_mat2d layout comment is preserved inline for future maintainers.
- **SIMUL_MODE runtime smoke test (D-10):** Run an inspection with a Datum that introduces a measurable translation + rotation offset, confirm FAI ROI center (reported via debug overlay or `Logging`) aligns with the rotated expected location within 1 pixel.

## WR-05: LastFindSucceeded 리셋 (Phase 6 에서 이미 해결됨)

### Issue (from 04-REVIEW.md §WR-05)

Original location `Action_FAIMeasurement.cs:96` retained a stale `LastFindSucceeded = true` across failed inspections because the failure branch did not reset the flag. This could cause downstream consumers (UI, result dispatch) to treat a failed Datum as successful on the next cycle.

### Structural resolution

Phase 6 refactor moved Datum execution out of `Action_FAIMeasurement` into `InspectionSequence.TryRunDatumPhase`. The current implementation already performs the required reset on the failure branch. Verbatim from `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` lines 143-171:

```csharp
        //260413 hbk Phase 6: Datum phase 실행 — 모든 DatumConfig 순회 (D-09)
        public bool TryRunDatumPhase(HImage image, out string error) {
            error = null;
            _datumTransforms.Clear();

            if (DatumConfigs.Count == 0) {
                return true; // D-10: Datum 미설정 Fixture는 무보정 pass-through
            }

            if (image == null) {
                error = "image is null";
                return false;
            }

            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform;
                string datumError;
                if (!service.TryFindDatum(image, datum, out transform, out datumError)) {
                    error = $"Datum '{datum.DatumName}' failed: {datumError}";
                    datum.LastFindSucceeded = false;
                    return false;
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                _datumTransforms[datum.DatumName ?? ""] = transform;
            }
            return true;
        }
```

**Key line:** `datum.LastFindSucceeded = false;` at `InspectionSequence.cs:163` is executed on the failure branch before `return false`. The stale `true` scenario described in 04-REVIEW.md §WR-05 cannot occur in current code — every failure path in the Datum phase now explicitly resets the flag on the failing `DatumConfig` before returning.

### Decision (10-CONTEXT.md D-07, D-08)

No additional defensive code inserted. WR-05 is closed as **structurally resolved by Phase 6 refactor**. Per user decision recorded in D-08, belt-and-suspenders defensive writes at other sites are explicitly out of scope for Phase 10.

## Runtime Smoke Test Procedure (SIMUL_MODE)

1. Build Debug/x64 (SIMUL_MODE symbol enabled — see `DatumMeasurement.csproj`).
2. Launch `DatumMeasurement.exe`.
3. Load a recipe with at least one `ShotConfig` containing a `DatumConfig`.
4. **Success-path test:** run inspection with well-separated Line1 / Line2 edges. Expected:
   - `Logging.Trace` shows Datum find succeeded.
   - `datum.LastFindSucceeded == true` after the run.
   - FAI ROI is shifted per the computed transform (overlay matches expected rotated location).
5. **Parallel-lines failure test (WR-01 exercise):** edit `DatumConfig` so `Line2_Phi ≈ Line1_Phi` (delta < 1°). Expected:
   - `Logging.Error` contains `"Lines are parallel, intersection is at infinity"` or `"Lines are collinear (identical), no unique intersection"`.
   - `datum.LastFindSucceeded == false` after the failed run (WR-05 exercise).
   - Inspection returns early from `TryRunDatumPhase` with error `Datum '<name>' failed: ...`.
6. **Rotation-offset test (WR-03 exercise):** run an inspection with a Datum configured to introduce a rotation + translation offset. Confirm the FAI ROI center reported in debug overlay aligns with the rotated expected location within 1 pixel (the old buggy `-transform[1]` extraction would under/over-rotate when the transform includes translation).
7. Record outcome + log excerpts in this document under "Runtime Results" when the test is performed.

## Runtime Results

_TBD — run before phase close._

The SIMUL_MODE smoke test is a human-run procedure. The developer will execute steps 4–6 above and append observed log excerpts and pass/fail verdicts here before Phase 10 is marked closed.

---

**Status:** WR-01, WR-03 code-resolved (plan 10-01 commits `c7e741b`, `9395303`, `559da6b`); WR-05 structurally resolved (Phase 6 `InspectionSequence.TryRunDatumPhase`, `InspectionSequence.cs:163`). All three ROADMAP §Phase 10 success criteria satisfied pending the runtime smoke-test sign-off above.
