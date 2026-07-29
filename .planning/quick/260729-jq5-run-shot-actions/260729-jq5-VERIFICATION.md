---
phase: quick-260729-jq5
verified: 2026-07-29T00:00:00Z
status: passed
score: 6/6 must-haves verified
overrides_applied: 0
---

# Quick Task 260729-jq5: RUN/일괄 검사 Shot 실행 아이덴티티 스캔 전환 Verification Report

**Task Goal:** RUN 버튼/일괄 검사가 트리에서 선택한 Shot 이 아닌 엉뚱한 Shot 을 실행하던 서수(ordinal) 의존 결함을,
`SequenceBase.Actions[]` 를 `Action_FAIMeasurement.ShotParam` 아이덴티티로 스캔하는 방식으로 교체해 제거한다.
`ComputeLocalShotIndex` 는 완전 삭제.

**Verified:** 2026-07-29
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Shot 노드 선택 RUN → 삭제/순서변경 후에도 반드시 그 Shot 이 실행됨 (서수 폴백 없음) | ✓ VERIFIED | `InspectionListView.xaml.cs` L427-477 `ResolveRunnableAction` Shot 분기, 서수 기반 `shotIdx`/`ActionCount` 비교 없음 — `ResolveActionIndexByShot` 아이덴티티 스캔만 사용, 실패 시 `return false` (L455), 대체 경로 없음 |
| 2 | UI에서 Shot 추가 직후 RUN (Actions[] 미재구축) 시 지연 동기화 후 정상 실행 | ✓ VERIFIED | L445-454: 1차 스캔 실패 + `seqHandler.IsIdle` → `EnableDynamicFAIMode()` + `RebuildInspectionActions(seq.ID)` 후 재스캔, 성공 시 `return true` |
| 3 | 일괄 검사가 체크된 Shot 들의 실제 Actions[] 인덱스를 오름차순으로 StartSubset(경유 StartBatch)에 전달 | ✓ VERIFIED | L569-570 `sortedPairs = resolvedPairs.Where(p => p.Item2 >= 0).OrderBy(p => p.Item2)`, `indices = sortedPairs.Select(p => p.Item2)`, `StartBatch(inspSeq, indices)` (L586) — `StartBatch`/`StartSubset` 시그니처 무변경 확인 |
| 4 | `_batchShots` 는 해석 성공 인덱스와 1:1 정렬 유지 | ✓ VERIFIED | L571 `batchShots = sortedPairs.Select(p => p.Item1)` — `indices`와 동일한 정렬된 `sortedPairs`에서 파생되므로 항상 인덱스 정렬 정합 |
| 5 | `ComputeLocalShotIndex` 코드베이스에 존재하지 않음 | ✓ VERIFIED | `grep -rn "ComputeLocalShotIndex" --include=*.cs .` → 0건 (exit 1) |
| 6 | Debug\|x64 MSBuild 빌드 에러 0 | ✓ VERIFIED | `MSBuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild` → 0 errors (20 warning lines = 2x 10 pre-existing CS0618 obsolete-API warnings in `Sequence_Top.cs`/`SequenceHandler.cs` L69,71,73, unrelated to changed lines; no warnings from changed hunks) |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | `ResolveActionIndexByShot` 헬퍼 + `ResolveRunnableAction`/`Btn_batchRun_Click` 수정 | ✓ VERIFIED | Helper at L483-491 mirrors `InspectionSequence.FindActionIndicesByZIndex` pattern; K&R brace style matches file convention |
| `WPF_Example/Custom/Sequence/SequenceHandler.cs` | 낡은 상호참조 주석 갱신 (로직 불변) | ✓ VERIFIED | Only L104 comment changed (`git show 987eac6` diff), `RebuildInspectionActions` execution code untouched |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `InspectionListView.ResolveRunnableAction` | `SequenceBase.Actions[i]` (`Action_FAIMeasurement.ShotParam`) | `ReferenceEquals(faiAct.ShotParam, target)` | ✓ WIRED | L488, matches pattern exactly |
| `InspectionListView.Btn_batchRun_Click` | `BatchRunService.StartBatch(InspectionSequence, List<int>)` | 아이덴티티 스캔으로 얻은 오름차순 인덱스 | ✓ WIRED | L586 `StartBatch(inspSeq, indices)` — indices derived from identity-scan + ascending sort |

### Additional Adversarial Checks (beyond plan must_haves)

| # | Check | Status | Evidence |
|---|-------|--------|----------|
| 1 | `ComputeLocalShotIndex` fully removed from `WPF_Example/` | ✓ VERIFIED | grep 0 matches repo-wide |
| 2 | `ResolveRunnableAction` Shot branch has no ordinal fallback after rebuild+rescan miss | ✓ VERIFIED | L454 falls straight to `return false;` — no `shotIdx < seq.ActionCount` or similar ordinal path anywhere in the branch |
| 3 | Lazy-sync path preserved (`EnableDynamicFAIMode()` + `RebuildInspectionActions(seq.ID)` guarded by `IsIdle`, followed by rescan) | ✓ VERIFIED | L446-453; batch path mirrors same guard at L562-566 |
| 4 | Non-Shot Action-node branch / Sequence-node branch unchanged | ✓ VERIFIED | `git diff 87d9cbf..HEAD` shows no `-`/`+` lines touching L459-467 (Action) or L470-474 (Sequence) blocks — confirmed by direct file read, content identical to plan's documented pre-existing behavior |
| 5 | `Btn_batchRun_Click` calls `RebuildInspectionActions` at most once; `_batchShots` stays index-aligned | ✓ VERIFIED | Single call site at L565, gated by `resolvedPairs.Any(p => p.Item2 < 0) && IsIdle`; `_batchShots`/`indices` derived from same `sortedPairs` list |
| 6 | Negative-index safety: `seq[idx]` only used after `idx >= 0` check | ✓ VERIFIED | L440/450 both wrapped in `if (idx >= 0)`; L486 uses loop variable `i` bounded `0..seq.ActionCount` (always ≥0) |
| 7 | Cross-Z / protocol paths untouched: `FindActionIndicesByZIndex`, `FindShotByZIndex`, `StartSubset`, `Custom/SystemHandler.ProcessTest`, `RebuildInspectionActions` execution logic | ✓ VERIFIED | `git diff 87d9cbf..HEAD -- InspectionSequence.cs SequenceBase.cs Custom/SystemHandler.cs BatchRunService.cs` → empty diff (no changes at all to these 4 files); `SequenceHandler.cs` diff is single comment line only |
| 8 | C# 7.2 compliance (no switch expressions, nullable refs, records) | ✓ VERIFIED | No `switch (` expression-bodied syntax found; `is` pattern matching (`node.Param is ShotConfig shotCfg`) is valid C# 7.0 syntax, compiles clean under project's C# 7.2 setting; build succeeded with 0 errors confirming compiler acceptance |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full Debug\|x64 rebuild | `MSBuild ... -t:Rebuild -p:Configuration=Debug -p:Platform=x64` | 0 errors, 10 pre-existing CS0618 warnings (unrelated files/lines) x2 (dual TargetFramework build passes) | ✓ PASS |
| `ComputeLocalShotIndex` removal | `grep -rn "ComputeLocalShotIndex" --include=*.cs .` | no matches (exit 1) | ✓ PASS |
| Unused `mgr` variable removed | `grep -n "\bmgr\b" InspectionListView.xaml.cs` | no matches | ✓ PASS |
| `System.Linq`/`System.Collections.Generic` usings preserved | `grep -n "^using" InspectionListView.xaml.cs` | both present (L3, L6) | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| JQ5-01 | 01-PLAN.md | Shot 실행 아이덴티티 스캔 전환 | ✓ SATISFIED | `ResolveActionIndexByShot` + `ResolveRunnableAction` Shot branch |
| JQ5-02 | 01-PLAN.md | 일괄 검사 인덱스 해석 + 정렬 정합 | ✓ SATISFIED | `Btn_batchRun_Click` rewrite, `sortedPairs` |
| JQ5-03 | 01-PLAN.md | `ComputeLocalShotIndex` 완전 삭제 + 낡은 주석 정리 | ✓ SATISFIED | grep 0 matches; both stale comments (InspectionListView L1084-area, SequenceHandler L104) updated |

### Anti-Patterns Found

None. No `TODO`/`FIXME`/`XXX`/`TBD`/`HACK`/`PLACEHOLDER` markers introduced. No empty handlers, no hardcoded empty returns, no stubbed logic in the modified sections.

### Human Verification Required

None required for goal achievement — all must-haves are statically verifiable via code inspection, grep, and compiler output. The plan itself notes actual hardware (HW) UAT re-run of the reproduction scenario (SHOT_E1-4 select → SHOT_B1-4 execute) is deferred to a future HW session; this is a follow-up UAT activity, not a blocker for this code-change verification, and SIMUL_MODE-only automated testing cannot exercise the real camera/Z-motor path per CLAUDE.md constraints. No human-check items were found deferred in the PLAN via `<verify><human-check>` blocks — the plan used only `<automated>` verification blocks.

### Gaps Summary

No gaps found. All 6 must-have truths verified, all 2 artifacts verified at exists/substantive/wired levels, both key links wired, all 8 adversarial checks passed, build succeeds with 0 errors and no new warnings, `ComputeLocalShotIndex` fully removed with no ordinal fallback remaining anywhere in the Shot resolution paths, and all cross-Z/protocol code paths confirmed byte-for-byte unchanged via git diff across the full commit range.

---

*Verified: 2026-07-29*
*Verifier: Claude (gsd-verifier)*
