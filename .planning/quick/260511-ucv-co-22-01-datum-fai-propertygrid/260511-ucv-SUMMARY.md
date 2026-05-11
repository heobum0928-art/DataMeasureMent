---
phase: quick-260511-ucv
plan: 01
type: execute
status: task_1_complete_task_2_pending_uat
wave: 1
requirements:
  - CO-22-01
files_modified:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
commits:
  - d6070e8  # fix(quick-260511-ucv): CO-22-01 Datum↔FAI PropertyGrid stale [root cause: A — e.Source asymmetric gate]
metrics:
  duration_min: 12
  lines_added: 10
  lines_removed: 3
  files_changed: 1
  build: PASS
  build_errors: 0
  build_warnings_new: 0
last_updated: "2026-05-11T08:00:00.000Z"
---

# Quick 260511-ucv — CO-22-01: Datum↔FAI PropertyGrid stale 해결 Summary

**One-liner:** Task 1 완료 — Root cause = `e.Source` 비대칭 게이트 (TreeListBox 내부 bubble 시 inner element). Fix = `sender` 기준 게이트로 교체. msbuild PASS, 신규 warning 0. Task 2 SIMUL UAT (5 scenarios) 사용자 검증 대기.

## Root Cause Analysis (Step A)

5 후보를 코드 정밀 검토 후 단 하나 선정.

| 후보 | 영역 | 판정 | 근거 |
|------|------|------|------|
| **A** | `e.Source` 게이트 실패 (L368-369) | **PASS — 선정** | TreeListBox 내부 (계층형 inner item / EditableTextBlock / 자식 ItemsControl 등) 가 발생시킨 routed `Selector.SelectionChanged` 가 bubble 될 때 `e.Source` 는 inner element 가 됨 → `source is TreeListBox` false → 함수 전체 skip. `OnParamEditorSelectionChanged` (L72-74) 는 `e.OriginalSource is ComboBox` 사용으로 inner element 분기를 처리하지만, `InspectionList_SelectionChanged` 는 `e.Source` 만 검사 — **비대칭**. Phase 16 D-09 force rebind 가 `ParamEditor.SelectedObject` 를 code-behind 로 직접 할당해 XAML 바인딩 (`InspectionListView.xaml:257` `SelectedObject="{Binding SelectedItem.Param,...}"`) 을 클리어한 이후, Datum→FAI 전환에서 본 핸들러가 skip 되면 XAML 바인딩도 force rebind 도 모두 실행되지 않아 PropertyGrid 가 직전 Datum 의 `PropertyDescriptorCollection` 을 유지 — 정확히 보고된 stale 동작과 일치. |
| B | NodeType 분기 누락 (Action/Sequence/else L476/L484/L490 force rebind 없음) | FAIL | Action/Sequence/else 분기에 force rebind 가 없는 것은 사실이나 — 사용자 보고 시나리오 (Datum↔FAI 직접 전환) 에서는 Datum 분기 (L421-430) 와 FAI 분기 (L465-473) 양쪽 다 force rebind 가 존재. Datum→Action→FAI 3-hop 경로 stale 은 본 후보로 설명되지만 메인 보고는 직접 전환 케이스 (UAT Scenario 1). |
| C | `_isRebinding` race | FAIL | `_isRebinding` 가드는 `OnParamEditorSelectionChanged` (L74) 에서만 검사. `InspectionList_SelectionChanged` 는 검사 안 함 → 이중 차단 race 불성립. force rebind try/finally 는 동기 경로만 보호하나 L79 `RemovedItems.Count == 0` 필터로 deferred 이벤트도 차단 완료. |
| D | PropertyTools.Wpf SelectedObject 동일 ref 캐시 | FAIL | 동일 Type 캐시는 plausible 하나, 명시적 `SelectedObject = null` → `SelectedObject = newInstance` 패턴이 동작하면 캐시 무효화됨 (Phase 17 D-10 AlgorithmType 변경 5-step 리셋 이 실제 동작 — 동일 Type/동일 인스턴스 간 dynamic filter 재적용 입증). FAI↔FAI 동일 Type 전환은 force rebind 만으로 정상 동작 → 캐시는 진짜 원인 아님. |
| E | `e.Handled` cascade | FAIL | `InspectionList_SelectionChanged` 는 `treeListBox_sequence.SelectionChanged` XAML 직접 등록. `OnParamEditorSelectionChanged` 는 `ParamEditor.AddHandler(Selector.SelectionChangedEvent, ..., handledEventsToo=true)` (L188) 로 별도 채널. 두 컨트롤 (TreeListBox vs PropertyGrid) 은 시각 트리에서 별개 컬럼 (XAML L165 Column 0 / L252 Column 2) → bubble 경로 분리. `e.Handled` 전파 불성립. |

**선정: 후보 A.** B/C/D/E 는 FAIL.

## Fix 구현 (Step B)

**변경 파일:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (단 1개)

**Diff 요약:** 10 라인 추가 / 3 라인 삭제

```diff
-            object source = e.Source;
-            if(source is TreeListBox) {
-                TreeListBox list = source as TreeListBox;
+            //260511 hbk CO-22-01 — sender 기준 게이트 (e.Source 비대칭 회피)
+            //  (7-라인 주석: 메커니즘 + 회귀 영향 0 근거 + Phase 17 OnParamEditorSelectionChanged 와의 의도된 비대칭)
+            if (!(sender is TreeListBox list)) return; //260511 hbk CO-22-01
+            if (!ReferenceEquals(sender, treeListBox_sequence)) return; //260511 hbk CO-22-01 — 동일 핸들러가 다른 트리에 attach 됐을 때 무관 호출 차단
+            {
                 if(list.SelectedItem is NodeViewModel) {
```

**메커니즘:**

- 원본: `e.Source` 가 inner item 일 때 게이트 fail → 함수 skip → stale.
- 변경: `sender` 는 항상 XAML 에 핸들러를 attach 한 본인 컨트롤 (`treeListBox_sequence`) → inner bubble 와 무관하게 항상 통과.
- `ReferenceEquals(sender, treeListBox_sequence)` 가드: 동일 핸들러가 우연히 다른 트리에 attach 되었을 때 무관 호출을 차단 (T-CO-22-01-02 DoS mitigation — 외부 입력 표면 0 유지).

**보존된 hbk 마커 (Phase 20 D-12 stacking 패턴):**

| Phase | 라인 수 |
|-------|---------|
| 260408 hbk (UI 초기화) | 4 |
| 260409 hbk Phase 4 (Datum) | 1 |
| 260410 hbk Phase 4 gap (Datum overlay) | 2 |
| 260413 hbk Phase 6 (Datum 노드 제거 주석) | 1 |
| 260417 hbk Phase 6 Plan 04 / 6-04 UAT | 13 |
| 260423 hbk Datum/Phase 11 | 7 |
| 260424 hbk Phase 12 / Gap-3 | 4 |
| 260426 hbk Phase 13-06/13-07 | 8 |
| 260429 hbk Phase 16 D-09/D-10/D-11/D-12 | 6 |
| 260503 hbk Phase 17 D-10/D-13/D-16 + bugfix | 15 |
| 260507 hbk Phase 19 패턴 (DynamicPropertyHelper 측, 본 파일 외) | — |
| 260508 hbk Phase 19 fix (Measurement/FAI force rebind) | 13 |
| 260509 hbk Phase 20 (operator conversion) | 6 |
| **260511 hbk CO-22-01 (신규)** | **3** |

기존 마커 1건도 손실 없음 — Phase 20 D-12 stacking 패턴 (위에 누적) 준수.

## Step C — msbuild 검증

```
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /m /v:m
```

**결과:** PASS

- 0 errors
- 0 신규 warnings
- 기존 baseline warnings 유지:
  - MSB3884 × 2 (`MinimumRecommendedRules.ruleset` 누락, 환경 문제 — 코드 무관)
  - CS0162 (`VirtualCamera.cs:266` 접근 불가 코드, 기존)
  - CS0219 (`VisionAlgorithmService.cs:64` `scanHorizontal` 미사용, 기존)
- 산출물: `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` 생성 확인

## 회귀 검증 매트릭스 (코드 변경 영향 분석)

| 보존 대상 | 검증 방법 | 결과 |
|-----------|-----------|------|
| Phase 17 D-09 ICustomTypeDescriptor (DatumConfig/FAIConfig dynamic filter) | DatumConfig.cs L586-607 / FAIConfig.cs L225-238 본문 무수정 (코드 diff 0) | PASS |
| Phase 19 fix — Datum/Measurement/FAI force rebind 3 분기 | InspectionListView.xaml.cs L421-430 / L445-453 / L465-473 본문 무수정 (게이트만 변경, 분기 코드 그대로) | PASS |
| Phase 17 D-10 AlgorithmType 5-step 리셋 | InspectionListView.xaml.cs L84-121 (`OnParamEditorSelectionChanged` 본문) 무수정 | PASS |
| Phase 13-06/07 자동 재티칭 (`TryTriggerDatumAutoReteach`) | InspectionListView.xaml.cs L49-60 무수정 | PASS |
| Phase 16 D-09 force rebind 패턴 | Datum 분기 (L421-430) 무수정 | PASS |
| Phase 11 D-15/D-18 Circle ROI 게이팅 | L505-516 무수정 | PASS |
| Phase 6 Plan 04 CRUD 핸들러 | L613-862 본문 무수정 | PASS |

**구조적 동등성 보증:** 변경된 게이트 (`e.Source is TreeListBox` → `sender == treeListBox_sequence`) 는 **정상 경로에서 동일하게 통과**한다 (`sender == treeListBox_sequence == e.Source` when WPF raises event normally from outer Selector). 이상 경로 (inner bubble) 에서만 동작이 변경되며, 그 경우 원본은 skip (stale 유발) / 새 코드는 정상 통과 (stale 회복). 정상 경로 0 회귀.

## Pending — Task 2 (SIMUL UAT 사용자 검증)

Plan Task 2 = `checkpoint:human-verify` (5 scenarios).

**사용자 검증 항목:**

1. **Scenario 1 — Datum ↔ FAI 직접 전환 (메인 버그):** Datum_1 → FAI_1 → Datum_1 → FAI_2 모두 즉시 PropertyGrid 갱신
2. **Scenario 2 — Datum ↔ Datum 전환 (동일 Type, 다른 AlgorithmType):** TwoLineIntersect ↔ CircleTwoHorizontal dynamic filter 적용
3. **Scenario 3 — FAI ↔ FAI 전환 (동일 Type, 다른 EdgeMeasureType):** EdgePairDistance ↔ CircleDiameter dynamic filter (또는 Measurement 노드로 대체 가능)
4. **Scenario 4 — Action/Sequence 경유 전환 (3-hop):** Datum_1 → SHOT_0 → FAI_1 / Sequence (Top) → Datum_1
5. **Scenario 5 — Phase 17 D-10 회귀 확인:** Datum AlgorithmType ComboBox 변경 시 dynamic filter 즉시 갱신 + LastTeachSucceeded=false 검출 도형 사라짐

**Resume signal:** `"approved 5/5 PASS"` 또는 `"FAIL scenario N: [관찰 내용]"`

## Self-Check: PASSED

- [x] `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — modified, build PASS
- [x] Commit `d6070e8` exists in worktree branch `worktree-agent-a5587eb25ea2356bb`
- [x] 변경 라인 모두 `//260511 hbk CO-22-01` 마커 (3건)
- [x] 기존 hbk 마커 손실 0
- [x] 변경 파일 = 1 (InspectionListView.xaml.cs 만)
- [x] Task 2 SIMUL UAT 자동 실행 안 함 (사용자 검증 영역)
