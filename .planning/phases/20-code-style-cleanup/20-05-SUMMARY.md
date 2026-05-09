---
phase: 20
plan: 05
subsystem: UI / InspectionListView
tags:
  - code-style
  - operator-cleanup
  - QUAL-02
  - QUAL-04
dependency_graph:
  requires:
    - InspectionViewModel (ClearResults / OnFAISelected / OnActionSelected)
    - InspectionListViewModel (RootModel.ExpandAll / RebuildTree)
    - DatumConfig / FAIConfig / MeasurementBase (selection 분기)
  provides:
    - "InspectionListView.xaml.cs C# 7.2 명시적 if/else 표준 (D-01)"
  affects:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
tech_stack:
  added: []
  patterns:
    - "P-3 (?? → 임시변수 + if 분해)"
    - "P-4 (?: → 임시변수 + if 분해)"
    - "P-13 (_inspectionVm?.ClearResults() inline if-guard)"
    - "P-14 (RootModel?.ExpandAll() single-line if)"
key_files:
  created: []
  modified:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
decisions:
  - id: D-01
    rationale: "ROADMAP 엄격 해석 — `?:` / `??` / `?.` 모두 명시적 if/else 분해 (CONVENTIONS.md §2 의 1-depth 허용 규칙은 Phase 20 한정으로 대체)"
  - id: D-04
    rationale: "LINQ chain 끝의 `?.` 변환 제외 — 본 파일에 해당 패턴 없음 (Line 584 FirstOrDefault 결과는 `matched != null` 검사로 이미 분해되어 있음)"
  - id: D-05
    rationale: "Expression-bodied member 변환 제외 — 본 파일에 expression-bodied 멤버 없음"
  - id: D-12
    rationale: "변환 라인에 //260509 hbk Phase 20 단일 마커 (스택 X)"
  - id: D-13
    rationale: "변환되지 않는 라인의 기존 hbk 주석은 그대로 보존 (Phase 6/12/17/19 hbk 100% 보존)"
metrics:
  duration_min: 6
  completed_date: "2026-05-09"
  task_count: 2
  file_count: 1
  lines_changed: "26 insertions / 16 deletions"
requirements:
  - QUAL-02
  - QUAL-04
---

# Phase 20 Plan 05: InspectionListView 코드 스타일 정리 Summary

C# 7.2 명시적 if/else 표준 적용 — InspectionListView.xaml.cs 의 `?.` (9곳), `??` (1곳), `?:` (4곳) 을 임시변수 + null 체크 + 단일 라인 if 패턴으로 변환하여 v1.1 코드 품질 (QUAL-02 + QUAL-04) 일관성 확보.

## Scope

**File:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (단일 파일, 856 → 858 라인)

**Tasks:**

| Task | Operator class | Sites | Pattern | Commit |
|------|----------------|-------|---------|--------|
| 1 | `?.` (null-conditional) + `??` (null-coalescing) | 9 + 1 = 10 | P-13 / P-14 / P-3 | `c6e3dc1` |
| 2 | `?:` (ternary) | 4 | P-4 | `eb22f95` |

## Conversions

### Task 1 (commit `c6e3dc1`)

**`_inspectionVm?.ClearResults()` 8곳 (P-13 inline guard):**

| Original line | Context |
|---------------|---------|
| 401 | Datum 노드 선택 — selection 시 결과 클리어 |
| 448 | Measurement 노드 선택 — selection 시 결과 클리어 |
| 482 | Sequence 노드 선택 — selection 시 결과 클리어 |
| 488 | else (recipe-root) 노드 선택 |
| 773 | RemoveDatum 후 결과 클리어 |
| 794 | RemoveMeasurement 후 결과 클리어 |
| 812 | RemoveShot 후 결과 클리어 |
| 833 | RemoveFAI 후 결과 클리어 |

```csharp
// Before:
_inspectionVm?.ClearResults();

// After:
if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
```

> 사실 정정: 20-05-PLAN.md 추정 = 9곳, 실제 baseline grep = 8곳. 변환 후 grep `_inspectionVm\?\.` = 0 으로 의미적 동등.

**`ViewModel.RootModel?.ExpandAll()` line 234 (P-14 single-line):**

```csharp
// Before:
ViewModel.RootModel?.ExpandAll();

// After:
//260509 hbk Phase 20 — null-conditional → 명시적 if/else (D-01, P-14)
if (ViewModel.RootModel != null) ViewModel.RootModel.ExpandAll();
```

**`_pendingRecipeName ?? CurrentRecipeName` line 170-171 (P-3 임시변수):**

```csharp
// Before:
string recipeName = _pendingRecipeName
    ?? SystemHandler.Handle.Setting.CurrentRecipeName;

// After:
//260509 hbk Phase 20 — null 병합 연산자 → 명시적 if/else (D-01, P-3)
string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
if (_pendingRecipeName != null) recipeName = _pendingRecipeName;
```

### Task 2 (commit `eb22f95`)

**Ternary 4곳 변환 (P-4):**

| Line | Original |
|------|----------|
| 82 | `var datum = ParamEditor != null ? ParamEditor.SelectedObject as DatumConfig : null;` |
| 85 | `string newValue = combo != null ? combo.SelectedValue as string : null;` |
| 700 | `string defaultType = typeNames.Length > 0 ? typeNames[0] : "EdgePairDistance";` |
| 795 | `string.IsNullOrEmpty(measToRemove.MeasurementName) ? measToRemove.TypeName : measToRemove.MeasurementName` (string.Format inline) |

각 변환 = 임시변수 default 할당 + if (조건) 본값 할당 패턴. line 795 의 경우 string.Format 인자 안에 있던 ternary 를 `measDisplayName` 임시변수로 추출하여 string.Format 단순화.

> 사실 정정: 20-05-PLAN.md 추정 = 11곳, 실제 baseline grep = 4곳. plan 의 추정은 Korean string literal 안의 `?` (질문부호 — line 213, 521, 766, 786, 806, 824 의 `삭제합니까?` `?` `Are you sure...?`) 6곳을 ternary 로 잘못 인식한 결과로 보임. 변환 대상 4곳 모두 처리 완료.

## Comment Cleanup (D-08, D-09, D-13)

D-13 에 따라 변환되지 않은 라인의 기존 hbk 주석 100% 보존:

- Phase 6 Plan 04 hbk (D-24/D-25 Datum CRUD, FAI CRUD) — 6곳 보존
- Phase 11 D-15/D-18 Circle ROI 활성화 — 1곳 보존
- Phase 12 D-01 btn_teachDatum 활성화 — 2곳 보존
- Phase 13-06/13-07 ParamEditor LostFocus / SelectionChanged — 6곳 보존
- Phase 16 D-09/D-10/D-11/D-12 force rebind 정책 — 5곳 보존
- Phase 17 D-10/D-13/D-16 AlgorithmType 5-step 리셋 — 8곳 보존
- Phase 19 fix Measurement/FAI binding stale 방지 — 8곳 보존
- Quick 260508 ComboInputBox 강제 — 1곳 보존

전부 'why' (페이즈 결정 / 결함 회피 사유) 카테고리 — 변경 없음.

## Verification

### Operator counts (after)

```
vm = 0     # _inspectionVm?.
qq = 0     # ?? (null-coalescing, ?? = 제외)
rm = 0     # RootModel?.
qd_total = 0  # 모든 ?.
```

### Brace / paren balance

```
Brace balance: open=154 close=154 (delta=0)
Paren balance: open=428 close=428 (delta=0)
```

### C# 7.2 syntax (Roslyn parse-only)

```
PARSE_ERRORS_CS1xxx=0
```

(CS0518 System.Void / CS0246 type-not-found 은 reference resolution 단계 — 단일 파일 parse-check 에서는 정상; 본 파일은 syntax 검증 PASS.)

### Encoding

- BOM bytes (first3) = `EF-BB-BF` (UTF-8 BOM 보존)
- 한국어 주석 (Phase 6/12/13/17/19 hbk + Korean modal text) mojibake 없음 (Read 도구 inline 검증)

### msbuild

본 worktree 의 NuGet 패키지가 bin/x64/Debug 에 복원되지 않은 상태로 PropertyTools.Wpf / WPF.MDI XAML 마크업 컴파일러가 어셈블리를 찾지 못하여 MC3074 XAML 에러가 발생함. **이 에러는 무관 (pre-existing infrastructure issue) — 동일 에러가 본 plan 의 코드 변경 이전에도 존재함을 `git stash` + 빌드 재실행으로 재현 확인**. C# 코드 자체는 Roslyn parse-check 0 errors. msbuild 재검증은 Wave 3 (20-08 종합 검증 plan) 에서 NuGet restore 후 일괄 수행 권고.

## Deviations from Plan

### Information correction (사실 정정)

**1. `_inspectionVm?.` 사이트 카운트:** Plan 추정 9곳 → 실제 baseline grep 8곳. 변환 후 0 으로 의미적 동등 달성. (`replace_all` 로 일괄 처리하여 8 사이트 모두 일관 P-13 적용.)

**2. Ternary 사이트 카운트:** Plan 추정 11곳 → 실제 4곳. Plan 의 11곳 추정은 Korean string literal 의 `?` (질문부호) 를 ternary 로 잘못 분류한 결과. 4 ternary 모두 P-4 패턴 변환 완료.

**3. msbuild 검증 미수행:** 워크트리 NuGet 패키지가 bin/x64/Debug 에 복원되지 않아 XAML markup compilation (MC3074) 단계에서 실패. C# 코드 syntax 자체는 Roslyn parse-check 로 PARSE_ERRORS_CS1xxx=0 PASS 확인. 본 plan 의 변경이 일으킨 회귀가 아님은 `git stash` 검증으로 입증.

기타 deviation 없음 — 자동 수정 (Rule 1/2/3) 발생 0건, 아키텍처 변경 (Rule 4) 0건.

## Acceptance Criteria

| AC | Status | Evidence |
|----|--------|----------|
| 1. powershell grep `??` (`??=` 제외) = 0 | PASS | qq=0 |
| 2. powershell grep `?.` (LINQ tail 제외) = 0 | PASS | qd_total=0 |
| 3. msbuild Debug/x64 exit 0, 신규 warning 0 | DEFERRED | Worktree 인프라 이슈 (XAML MC3074, pre-existing). Roslyn parse-check PARSE_ERRORS_CS1xxx=0. Wave 3 (20-08) 에서 본격 빌드 검증 권고. |
| 4. 한국어 nuance 주석 보존 + mojibake 없음 (D-09) | PASS | UTF-8 BOM 보존, Read 검증 |
| 5. Phase 1 / Phase 12-03 / Phase 18 의도 주석 보존 | PASS | D-13 적용, 변환 라인 외 모든 hbk 마커 보존 |
| 6. C# 7.2 호환 (W1) | PASS | Roslyn `/langversion:7.2` parse PASS |

## Self-Check

- File `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`: FOUND
- Commit `c6e3dc1`: FOUND
- Commit `eb22f95`: FOUND

## Self-Check: PASSED
