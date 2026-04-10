---
phase: 6
slug: rapid-city
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-10
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | None (no xUnit/NUnit/MSTest in project) |
| **Config file** | none |
| **Quick run command** | `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` |
| **Full suite command** | Build + SIMUL_MODE manual execution |
| **Estimated runtime** | ~30 seconds (build only) |

---

## Sampling Rate

- **After every task commit:** Run `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
- **After every plan wave:** Build success + SIMUL_MODE execution for manual RC verification
- **Before `/gsd-verify-work`:** Full build + all RC requirements manual confirmation
- **Max feedback latency:** 30 seconds (build time)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | RC-03 | — | N/A | build | `msbuild ...` | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | RC-03 | — | N/A | build | `msbuild ...` | ❌ W0 | ⬜ pending |
| 06-02-01 | 02 | 1 | RC-01, RC-02 | — | N/A | build | `msbuild ...` | ❌ W0 | ⬜ pending |
| 06-02-02 | 02 | 1 | RC-04 | — | N/A | build | `msbuild ...` | ❌ W0 | ⬜ pending |
| 06-03-01 | 03 | 2 | RC-05 | — | N/A | build | `msbuild ...` | ❌ W0 | ⬜ pending |
| 06-03-02 | 03 | 2 | RC-06 | — | N/A | manual | SIMUL_MODE UI check | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] No test framework — build success serves as automated gate
- [ ] RC-03 verification: SIMUL_MODE + SimulImagePath test images for measurement algorithms
- [ ] RC-05 verification: Phase 5 INI recipe sample for legacy format rejection testing

*Existing test infrastructure absent. Build success + SIMUL_MODE manual verification as substitute.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Datum Multi-Datum execution | RC-02 | Requires camera grab + Datum finding flow | SIMUL_MODE: load recipe with 2+ Datums, run inspection, verify Datum transforms in log |
| 6-type measurement algorithms | RC-03 | Each algorithm needs specific test image | Load test images via SimulImagePath, run FAI measurement, verify mm values in result table |
| Legacy INI format rejection | RC-05 | Requires Phase 5 format INI file | Attempt to load Phase 5 recipe, verify rejection dialog appears |
| UI tree Seq > Datum + Shot > FAI > Measurement | RC-06 | Visual UI verification | Run app, load recipe, navigate tree, verify node hierarchy |
| Result table Measurement-per-row | RC-06 | Visual UI verification | Run inspection, verify each Measurement shows as separate row |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
