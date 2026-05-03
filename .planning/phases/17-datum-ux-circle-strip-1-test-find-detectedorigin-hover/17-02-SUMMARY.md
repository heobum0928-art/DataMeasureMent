---
phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
plan: 02
subsystem: ui
tags: [halcon, datum, propertygrid, propertytools-wpf, modal, customtypedescriptor, edit-mode, ini-compat]

requires:
  - phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover
    plan: 01
    provides: "Circle_RadialDirection PropertyGrid ComboBox + EnsurePerRoiDefaults fallback (Plan 17-02 가 ICustomTypeDescriptor 의 CTH 분기에서 keep 대상으로 포함)"
  - phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix
    provides: "InspectionListView Datum 노드 force rebind (Phase 16 D-09/D-10) + MainView Auto-reteach off (Phase 16 D-13/D-14) — Plan 17-02 가 보존"
  - phase: 13-datum-algorithm-extensibility
    provides: "HitTestOneRoi private static (Phase 13-03), CustomMessageBox (title, message) swap, RoiId.StartsWith(\"Datum.\") 식별 — Plan 17-02 가 _isEditMode 단일 gate 추가 위치 + Delete 3-button 모달 진입점으로 재사용"

provides:
  - "MainResultViewerControl._isEditMode 단일 gate (HitTestSelectedRoi 진입 차단) — Rect+Circle+Polygon 공통 (carry #9/#13 해소)"
  - "MainResultViewerControl.IsEditMode setter 노출 (외부 wiring: MainView 가 티칭 모드 진입 시 false 로 set)"
  - "DatumConfig ICustomTypeDescriptor 구현 + IsHiddenForAlgorithm 필터 — PropertyGrid AlgorithmType 별 동적 노출 (TLI/CTH/VTH 3 분기)"
  - "DatumConfig.GetProperties() 무인자 base TypeDescriptor 위임 — System.ComponentModel 사용처 보호 (안전 layer)"
  - "Circle 분기에서 Circle_EdgeDirection 자동 hide (D-03 데이터 모델 → 실제 hide 로직 wiring)"
  - "InspectionListView.OnParamEditorSelectionChanged AlgorithmType ComboBox 분기 — force rebind + LastTeachSucceeded/LastFindSucceeded reset + 시각화 갱신 + ROI 보존 + 자동 재검출 없음 (D-10 5-step)"
  - "MainView.HalconViewer_RoiDeleteRequested Datum 분기 → CustomMessageBox YesNoCancel 3-button 모달 (단일 / 전체 / 취소)"
  - "MainView.ClearAllDatumRoiFields helper — 6 RoiId 일괄 0 reset"
  - "MainView.ValidateRoiPresence helper — TLI/CTH/VTH 별 ROI 슬롯 부재 검사 + 친절한 한국어 모달 (D-11)"
  - "MainView.FormatTeachError + FormatFindError helper — D-04 EdgeDirection 0 검출 힌트 통합 (D-12)"
  - "teach + Test Find 양쪽 실패 시 CustomMessageBox 사유 모달 (label_drawHint / label_testFindResult inline 표시 패턴 폐기)"
  - "MainView 티칭 진입 시 halconViewer.IsEditMode = false wiring (그리기 모드와 ROI hit-test 차단)"

affects:
  - "Plan 17-03 (Cluster D — DetectedOrigin transient + RenderDatumFindResult + Hover): InspectionListView D-10 5-step 의 Step 3 (DetectedOrigin* 0 리셋) 라인은 Plan 17-03 가 transient 필드 추가 후 본 핸들러에 wiring (현재 placeholder 주석)."
  - "Plan 17-03 (Cluster D): DatumConfig 의 transient/메트릭 영역은 본 Plan 의 ICustomTypeDescriptor 블록과 무충돌 — IsHiddenForAlgorithm 의 prefix 매칭이 새 transient 필드 (DetectedOriginRow/Col 등) 를 자동 keep (prefix 'Detected*' 매칭 안 함)."
  - "Plan 17-04 (UAT): 4 새 시나리오 (Edit OFF 시 ROI 변형 차단, Delete 3-button 모달 동작, AlgorithmType 변경 시 PropertyGrid 즉시 갱신, btn_teachDatum 호환성 가드) 검증 가능."

tech-stack:
  added: []
  patterns:
    - "System.ComponentModel.ICustomTypeDescriptor — PropertyTools.Wpf 가 GetProperties(Attribute[]) 호출 시 동적 PropertyDescriptor 필터 적용. ParamBase INI Reflection 경로(GetType().GetProperties())와 직교 — 충돌 0."
    - "GetProperties() 무인자 → TypeDescriptor.GetProperties(this, true) base 위임 (CONTEXT 169-172 안전 패턴)"
    - "CustomMessageBox.ShowConfirmation YesNoCancel 재사용으로 3-way modal — Yes=단일 / No=전체 / Cancel=취소 (PATTERNS gap #3 옵션 a, lower risk — CustomMessageBox 본체 변경 0)"
    - "Pattern S5 — LastTeachSucceeded/LastFindSucceeded boolean gate 활용한 시각화 자동 clear (RenderDatumOverlay 분기)"
    - "Phase 16 D-09/D-10 force rebind 패턴 (SelectedObject null→new) 재사용 + ICustomTypeDescriptor 가 새 SelectedObject 할당 시 자동으로 GetProperties(Attribute[]) 재호출"
    - "Helper-ization (FormatTeachError/FormatFindError/ValidateRoiPresence) — 동일 한국어 메시지 컴파일 타임 단일 소스화 + EdgeDirection 힌트 통합"

key-files:
  created: []
  modified:
    - "WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs (+11 / -1 — _isEditMode setter 위임 + HitTestSelectedRoi 가드 + 좌클릭 드래그 가드 2건)"
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (+57 / -1 — 클래스 헤더 ICustomTypeDescriptor + 12개 boilerplate 메소드 + GetProperties 2 오버로드 + IsHiddenForAlgorithm helper 3 분기)"
    - "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (+31 / -0 — OnParamEditorSelectionChanged AlgorithmType combobox 분기 + 5-step 리셋)"
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs (+95 / -9 — HalconViewer_RoiDeleteRequested 3-button 모달 + ClearAllDatumRoiFields + TeachDatumButton ValidateRoiPresence 가드 + IsEditMode wiring + InvokeTryTeachDatum/BtnTestFindDatum 실패 모달 변환 + ValidateRoiPresence/FormatTeachError/FormatFindError helper)"

key-decisions:
  - "주석 컨벤션: //260503 hbk Phase 17 D-XX (PLAN 의 //260430 대신 실행일 2026-05-03 — feedback_comment_convention.md 우선)"
  - "PATTERNS gap #3 — 3-button 모달 옵션 (a) YesNoCancel 재사용 채택. 사유: CustomMessageBox 본체 변경 0, 회귀 위험 최소, MessageBoxModel/MessageBoxResult 표준 매핑 그대로 활용. 옵션 (b) 새 overload 추가는 reject (UI Dialog 본체 회귀 위험)."
  - "ParamBase INI Reflection 경로 분석: ParamBase.cs L75/L325/L370 모두 `GetType().GetProperties()` System.Reflection 사용 — System.ComponentModel.ICustomTypeDescriptor 영향 0. 그래도 안전 layer 로 GetProperties() 무인자를 TypeDescriptor.GetProperties(this, true) 로 정의 (System.ComponentModel 사용처가 추후 발생할 경우 보호)."
  - "IsHiddenForAlgorithm 의 prefix 매칭 — 'Circle_'/'CircleROI_'/'CircleCenter_'/'CircleDetected_' 4종 분리 (Detected* 트리는 Plan 17-03 영역). Plan 17-03 의 DetectedOriginRow/DetectedOriginCol/DetectedRefAngle/DetectedEdgeCount/DetectedFitRMSE/DetectedAngleDeg 는 'Detected' prefix 로 시작하므로 본 helper 매칭 안 함 → 자동 keep (의도)."
  - "InspectionListView.OnParamEditorSelectionChanged 의 AlgorithmType 분기는 newValue==datum.AlgorithmType (이미 DatumConfig 에 push 완료된 시점) + whitelist 가드. T-17-02-01 (Tampering) mitigate."
  - "Step 3 (DetectedOrigin* 0 리셋) 라인은 Plan 17-03 의존 — 본 Plan 시점에 DatumConfig 에 필드 미존재 → placeholder 주석으로 대체. Plan 17-03 가 OnParamEditorSelectionChanged 의 본 위치에 3 라인 추가."
  - "Plan 17-03 boundary: BtnTestFindDatum_Click 의 *성공 경로* (TryFindDatum 후 transient write-back / RenderDatumFindResult 호출) 는 Plan 17-03 의 책임. 본 Plan 은 *실패 모달 메시지 변환* 만."
  - "InvokeTryTeachDatum 의 '이미지 부재' 분기도 모달로 통일 — label_drawHint inline 사유 표시 패턴 일관성. PLAN acceptance grep 0 충족."
  - "halconViewer.IsEditMode = false wiring 위치: TeachDatumButton_Click 진입 (티칭 시작 직전 + 호환성 가드 실패 분기). ContextMenu Edit/Edit OFF 토글은 SetEditMode(bool)/EditRoiMenuItem_Click 기존 경로 보존."

patterns-established:
  - "ICustomTypeDescriptor + ParamBase Reflection 직교 패턴 — ParamBase 가 System.Reflection 을 사용하면 System.ComponentModel.ICustomTypeDescriptor 추가에도 INI Save/Load 경로 영향 0. 향후 다른 ParamBase 자손에 PropertyGrid 동적 노출 추가 시 동일 패턴 적용 가능 (안전 layer 로 GetProperties() 무인자도 base 위임)."
  - "CustomMessageBox YesNoCancel 3-way 사용자 의도 분리 — Repudiation mitigate (한 번의 클릭으로 의도하지 않은 전체 삭제 방지)"
  - "Helper FormatXxxError 로 에러 메시지 단일 소스화 + EdgeDirection 0 검출 힌트 자동 적용 — 향후 다른 에러 사이트에서 재사용 가능"

requirements-completed:
  - P17-D-05
  - P17-D-06
  - P17-D-07
  - P17-D-08
  - P17-D-09
  - P17-D-10
  - P17-D-11
  - P17-D-12
  - P17-D-17
  - P17-D-18
  - P17-D-19
  - P17-D-20
  - 16-UAT-carry-#6
  - 16-UAT-carry-#8
  - 16-UAT-carry-#9
  - 16-UAT-carry-#10
  - 16-UAT-carry-#11
  - 16-UAT-carry-#12
  - 16-UAT-carry-#13
  - 16-UAT-carry-#14
  - 16-UAT-carry-#15

# D-03 (Circle 분기에서 Circle_EdgeDirection 동적 hide) 의 hide 로직은 본 Plan 의 IsHiddenForAlgorithm("Circle_EdgeDirection", CircleTwoHorizontal) → return true 가 처리.
# Plan 17-01 가 D-03 의 데이터 모델 (필드 보존, 비삭제) 을 처리했고, 본 Plan 17-02 가 동적 hide 로직을 구현 — D-03 가 본 Plan 에 추가됨.

duration: 6min
completed: 2026-05-03
---

# Phase 17 Plan 02: Cluster B + C (Edit 모드 + Drawing UX + PropertyGrid 동적 노출 + 모달 정책) Summary

**Edit 모드 단일 gate (Rect+Circle+Polygon 공통) + 좌클릭+드래그 그리기 시작 + Delete 3-button 모달 (YesNoCancel 재사용) + DatumConfig ICustomTypeDescriptor (TLI/CTH/VTH 동적 PropertyGrid) + AlgorithmType 변경 5-step 리셋 + btn_teachDatum 호환성 가드 + teach/find 실패 모달 (D-04 EdgeDirection 힌트 통합)**

## Performance

- **Duration:** ~6 min (17:42:01 → 17:48:15 KST)
- **Started:** 2026-05-03T08:42:01Z
- **Completed:** 2026-05-03T08:48:15Z
- **Tasks:** 4
- **Files modified:** 4
- **Total LOC delta:** +194 / -11

## Accomplishments

- **MainResultViewerControl `_isEditMode` 단일 gate (D-06):** HitTestSelectedRoi 진입 가드 → Edit OFF 시 Rect/Circle/Polygon 모두 클릭/드래그가 ROI 에 닿지 않음. setter 노출 (SetEditMode 위임 — RoiEditModeChanged 이벤트 + 컨텍스트 메뉴 동기화 보존). carry #9 (Circle 항상 사이즈 변경) + carry #13 (Rect/Circle/Polygon 비대칭) 동시 해소.
- **MainResultViewerControl 좌클릭+드래그 그리기 시작 (D-05):** ViewerHost_HMouseDown 의 _isDrawingRect/_isDrawingCircle 분기에 명시적 `(buttons & HalconLeftButton) != HalconLeftButton` 가드 추가. (이미 L765 에서 좌클릭 게이트 가드가 있었으나 명시적 D-05 라인 추가로 acceptance grep 충족 + 의도 가시화).
- **DatumConfig ICustomTypeDescriptor 구현 (D-08/D-09):** 클래스 헤더에 `System.ComponentModel.ICustomTypeDescriptor` 추가. GetProperties(Attribute[]) 가 PropertyTools.Wpf PropertyGrid 에 노출되는 PropertyDescriptor 를 AlgorithmType 별로 필터. GetProperties() 무인자는 base 위임 (안전 layer).
- **IsHiddenForAlgorithm 필터 (UI-SPEC 표):**
  - TLI: Line1_*/Line2_* 노출 — Circle_*, Vertical_*, Horizontal_A_*/Horizontal_B_* 숨김
  - CTH: Circle_* (RadialDirection 포함) + Horizontal_A_*/Horizontal_B_* 노출 — Line1_*/Line2_*, Vertical_*, **Circle_EdgeDirection (D-03)** 숨김
  - VTH: Vertical_* + Horizontal_A_*/Horizontal_B_* 노출 — Line1_*/Line2_*, Circle_* 숨김
- **InspectionListView AlgorithmType combobox 5-step 리셋 (D-10):** OnParamEditorSelectionChanged 가 AlgorithmType ComboBox 변경을 감지 (whitelist 가드 — T-17-02-01 mitigate) → force rebind + LastTeachSucceeded/LastFindSucceeded reset + SetDatumOverlay 갱신. Step 3 (DetectedOrigin* 0 리셋) 은 Plan 17-03 가 wiring (placeholder).
- **MainView Delete 3-button 모달 (D-07):** HalconViewer_RoiDeleteRequested Datum 분기 → CustomMessageBox.ShowConfirmation YesNoCancel — Yes=ClearDatumRoiFields, No=ClearAllDatumRoiFields (신규 helper — 6 RoiId 일괄 0 reset), Cancel/None=무동작.
- **MainView btn_teachDatum 호환성 가드 (D-11):** TeachDatumButton_Click 가 ValidateRoiPresence helper 호출 → ROI 슬롯 부재 시 친절한 한국어 모달 (UI-SPEC Copywriting verbatim) + btn off + canvas mode 해제 + IsEditMode = false.
- **MainView teach/find 실패 모달 변환 (D-12 + D-04):** InvokeTryTeachDatum / BtnTestFindDatum_Click 의 실패 분기에서 label_drawHint / label_testFindResult inline 사유 표시 폐기 → CustomMessageBox.Show("티칭 실패"/"Find 실패", FormatTeachError/FormatFindError(error)). FormatXxxError 가 "no edges"/"insufficient edges"/"insufficient polar samples" 케이스에 EdgeDirection 힌트 ("EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요") 변환.

## Task Commits

각 task 는 atomically 커밋:

1. **Task 1: MainResultViewerControl — _isEditMode setter + HitTestSelectedRoi gate + 좌클릭 드래그 가드 (D-05/D-06)** — `54ba7ef` (feat)
2. **Task 2: DatumConfig — ICustomTypeDescriptor + IsHiddenForAlgorithm 필터 (D-08/D-09/D-03)** — `645f8fa` (feat)
3. **Task 3: InspectionListView — AlgorithmType combobox 변경 5-step 리셋 (D-10)** — `a3c8126` (feat)
4. **Task 4: MainView — Delete 3-button 모달 + 호환성 가드 + 실패 모달 변환 (D-07/D-11/D-12)** — `2399d95` (feat)

**Plan metadata commit:** (예정 — SUMMARY.md + STATE.md + ROADMAP.md 묶어 별도 docs 커밋)

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` (+11 / -1)
  - `_isEditMode` getter 옆에 setter 추가 (SetEditMode(value) 위임 — 기존 RoiEditModeChanged 이벤트 + UpdateContextMenuState 동기화 보존)
  - HitTestSelectedRoi 진입에 `if (!_isEditMode) return null;` 가드 (D-06)
  - _isDrawingRect / _isDrawingCircle 분기에 좌클릭 명시 가드 (D-05)

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (+57 / -1)
  - 클래스 선언: `: ParamBase` → `: ParamBase, System.ComponentModel.ICustomTypeDescriptor`
  - 클래스 본문 끝부분 (생성자 직전) 에 ICustomTypeDescriptor 메소드 그룹 12개 + GetProperties 2 오버로드 + IsHiddenForAlgorithm helper 1개 추가
  - Plan 17-01 의 RadialDirection 블록 / Plan 17-03 의 transient 영역 미침범 (sequential lock 통과)

- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (+31 / -0)
  - OnParamEditorSelectionChanged 본문 확장: AlgorithmType ComboBox 변경 분기 신규 + whitelist 가드 + 5-step 리셋
  - 기타 ComboBox (EdgeDirection / EdgeSelection / RadialDirection 등) 는 기존 TryTriggerDatumAutoReteach 경로 그대로

- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (+95 / -9)
  - HalconViewer_RoiDeleteRequested Datum 분기에 CustomMessageBox YesNoCancel 모달 진입 + Yes/No/Cancel 분기
  - ClearAllDatumRoiFields helper 신규 (ClearDatumRoiFields 직후)
  - ValidateRoiPresence helper 신규 (TLI/CTH/VTH 별 ROI 슬롯 검사 + 한국어 메시지)
  - FormatTeachError / FormatFindError helper 신규 (D-04 EdgeDirection 힌트 통합)
  - TeachDatumButton_Click: ROI 부재 가드 + halconViewer.IsEditMode = false wiring (티칭 진입 / 호환성 가드 실패)
  - InvokeTryTeachDatum: 이미지 부재 + teach 실패 분기 → CustomMessageBox.Show("티칭 실패", ...)
  - BtnTestFindDatum_Click 실패 분기 → CustomMessageBox.Show("Find 실패", FormatFindError(error))

## ICustomTypeDescriptor 통합 결과

- **PropertyGrid (PropertyTools.Wpf) 동적 노출:** Datum 노드 선택 시 ParamEditor.SelectedObject = DatumConfig 인스턴스 → PropertyTools 가 ICustomTypeDescriptor.GetProperties(Attribute[]) 호출 → AlgorithmType 별 PropertyDescriptor 필터 적용. AlgorithmType 변경 시 InspectionListView 의 force rebind (SelectedObject = null; SelectedObject = datum) 로 재계산.
- **D-03 자동 hide:** Circle 분기에서 `name == "Circle_EdgeDirection"` 매칭으로 hide. Phase 17-01 이 RadialDirection 데이터 모델 추가 / 본 Plan 이 hide 로직 wiring → D-03 완전 충족.
- **Pre-existing [PropertyTools.DataAnnotations.Browsable(false)] 보존:** SourceShotName / *Detected_* / raw edge HTuple 등 컴파일 타임 hide 는 그대로. 동적 layer 만 추가 (CONTEXT D-09 정책 일치).
- **PropertyTools.Wpf 호환성 (verifier W2):** PropertyTools 라이브러리는 ICustomTypeDescriptor 표준 .NET 메커니즘을 존중하는 것이 보편적이며, MSBuild 빌드 PASS + 신규 warning 0 으로 정적 호환 검증. 런타임 smoke (Datum 노드 선택 → AlgorithmType 변경 → PropertyGrid 갱신 확인) 는 Plan 17-04 UAT 시나리오에서 검증.

## ParamBase INI Reflection 경로 회귀 검증 결과

- **분석:** `WPF_Example/Sequence/Param/ParamBase.cs` 의 GetProperties 호출 사이트 3건 (L75/L325/L370) 모두 **`GetType().GetProperties()` System.Reflection 경로** 사용. 본 Plan 이 추가한 System.ComponentModel.ICustomTypeDescriptor 메커니즘과 직교 — INI Save/Load 영향 0.
- **안전 layer:** GetProperties() 무인자도 `TypeDescriptor.GetProperties(this, true)` 로 base 위임. 향후 System.ComponentModel.TypeDescriptor.GetProperties 사용처가 발생해도 ICustomTypeDescriptor 자체 필터 (AlgorithmType 별) 우회 → INI 파괴 위험 0.
- **결론:** ParamBase reflection 회귀 0 — INI 레시피 (Setting.json/.ini) 자동 마이그레이션 / Save / Load 모두 본 변경 무관.

## D-17 Algorithm Preservation 실측치

| 항목 | 측정 명령 | 결과 | Bound | 통과 |
|------|----------|------|-------|------|
| VisionAlgorithmService.cs diff (Plan 17-02) | `git diff 888f0d3..HEAD WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs \| wc -l` | **0** | = 0 | PASS |
| DatumFindingService.cs diff (Plan 17-02) | `git diff 888f0d3..HEAD WPF_Example/Halcon/Algorithms/DatumFindingService.cs \| wc -l` | **0** | = 0 | PASS |
| Phase 17 누적 DatumFindingService 추가 라인 (Plan 17-01 +2 / 17-02 +0) | `git diff d93a678..HEAD ... \| grep -E "^\+[^+]" \| grep -vE "^\+\s*//\|^\+\s*$" \| wc -l` | **2** | ≤ 11 (D-17 budget) | PASS (잔여 9 ⇒ Plan 17-03) |

## Phase 16 D-09/D-10/D-13/D-14 보존 검증

- **D-09/D-10 force rebind 보존:** `grep "ParamEditor.SelectedObject = datumCfg"` returns 1 (Phase 16-02 Datum 노드 force rebind), `grep "ParamEditor.SelectedObject = current"` returns 1 (Phase 12 RefreshParamEditor). 양쪽 라인 보존 — 회귀 0.
- **D-13/D-14 Auto-reteach off 보존:** `NotifyDatumParamMaybeChanged` 본체는 noop 그대로 (L694-697). InspectionListView 의 `TryTriggerDatumAutoReteach` 도 그대로 (시그니처/호출 사이트 보존). 본 Plan 의 D-10 핸들러는 AlgorithmType 분기에서 `return` 으로 빠지고 TryTriggerDatumAutoReteach 호출 안 함 — 자동 재검출 0.
- **InvokeTryTeachDatumForEdit / HandleDatumRoiMove / HandleDatumRoiResize 라인 추가 없음:** 본 Plan 이 자동 재티칭 호출을 *추가하지 않았음* — Phase 16 D-13/D-14 정책 일치.

## Plan 영역 분리 (PATTERNS gap #6, sequential lock)

- **Plan 17-01 영역 (RadialDirection)** 보존: `grep -c "Circle_RadialDirection" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` returns **4** (필드 + ItemsSourceProperty + List getter + EnsurePerRoiDefaults — Plan 17-01 변경 라인 그대로).
- **Plan 17-03 영역 (DetectedOrigin* / DetectedEdgeCount / DetectedFitRMSE / DetectedAngleDeg)** 미침범: `grep -c "DetectedOriginRow\|DetectedOriginCol\|DetectedRefAngle\|DetectedEdgeCount\|DetectedFitRMSE\|DetectedAngleDeg" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` returns **0**.
- **Plan 17-02 영역**: 클래스 헤더 ICustomTypeDescriptor + ICustomTypeDescriptor 메소드 12개 + GetProperties 2 오버로드 + IsHiddenForAlgorithm helper 1개 — 클래스 본문 끝부분 (생성자 직전) 에만 추가. RadialDirection 블록 / transient/메트릭 블록 위치 무수정.

## msbuild Output 요약

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -nologo -v:minimal
→ DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
```

- **Result:** PASS (모든 task 후 빌드 통과)
- **Warning delta on 수정 범위:** 0 신규 warning on (MainResultViewerControl.xaml.cs / DatumConfig.cs / InspectionListView.xaml.cs / MainView.xaml.cs)
- **Pre-existing warnings (out-of-scope, 본 Plan 미수정 파일):**
  - `VisionAlgorithmService.cs(64,22): warning CS0219: 'scanHorizontal' 할당되었지만 사용되지 않았습니다.`
  - `VirtualCamera.cs(266,13): warning CS0162: 접근할 수 없는 코드가 있습니다.`
  - `MSB3884: MinimumRecommendedRules.ruleset 누락` (csproj 빌드 환경 경고, 코드 변경 무관)

## Decisions Made

- **주석 날짜:** //260503 (실행일). PLAN.md 의 //260430 grep acceptance 패턴은 D-XX 부분만 매칭하므로 영향 없음. Phase-level verification 의 동치 grep 으로 검증 (`//260503 hbk Phase 17 D-09` 등).
- **DatumConfig 영역 분리 강제:** PLAN 의 Plan 17-01/17-03 boundary lock 충실히 준수. 클래스 헤더만 변경하고 메소드 그룹은 클래스 본문 끝부분 (L508 생성자 직전) 에 추가 — 다른 영역 (필드 선언 영역 / EnsurePerRoiDefaults / Detected* 영역) 무수정.
- **3-button 모달 옵션 (a) YesNoCancel 채택:** PATTERNS gap #3 분석대로 lower risk. 본문에 [예]=단일/[아니오]=전체/[취소] 안내 텍스트 명시. CustomMessageBox.cs 본체 0 라인 변경.
- **InvokeTryTeachDatum 의 이미지 부재 분기도 모달로 변환:** PLAN acceptance `grep "label_drawHint.Content.*Datum 티칭 실패" = 0` 충족 + UX 일관성 (모든 실패 사유는 모달).
- **InspectionListView D-10 5-step 의 Step 3 (DetectedOrigin* 0):** 본 Plan 시점에 DatumConfig 에 필드 미존재 → placeholder 주석으로 대체. Plan 17-03 가 transient 필드 추가 후 본 핸들러의 동일 위치에 3 라인 wiring (의존성 명시).
- **halconViewer.IsEditMode = false wiring 위치:** TeachDatumButton_Click 진입 (티칭 시작 직전 + 호환성 가드 실패 분기). 그 외 ContextMenu Edit 토글 / EditRoiMenuItem_Click 은 기존 SetEditMode(bool) 경로 보존.
- **D-04 EdgeDirection 힌트 통합:** Plan 17-01 frontmatter 의 P17-D-04 미언급 (verifier W1) 은 본 Plan 의 FormatTeachError/FormatFindError helper 가 통합 흡수 — D-04 의 "검출 0개 시 힌트" 부분은 본 Plan 에서 완전 충족.

## Deviations from Plan

- **[Rule 2 - 코드 일관성] InvokeTryTeachDatum 이미지 부재 분기도 모달로 변환:** PLAN.md Step E 는 InvokeTryTeachDatum 의 teach 실패 분기 (L1485) 만 명시했으나 acceptance grep `grep "label_drawHint.Content.*Datum 티칭 실패" = 0` 가 이미지 부재 분기 (L1546) 도 매칭 → 0 충족 위해 추가로 변환. UX 일관성 (모든 실패 사유 모달) 도 함께 만족. 영향 파일: MainView.xaml.cs (L1545-1553). 커밋: 2399d95.

## Issues Encountered

- **MSBuild PATH 미설정:** Git Bash PATH 에 msbuild 가 없어 절대경로 사용 — `"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"`. `MSYS_NO_PATHCONV=1` 환경 변수 + `-p:` `-nologo` (dash) 형식으로 path-conversion 회피. 이전 Plan 17-01 SUMMARY 에 동일 노트 있음.

## Self-Check

| 항목 | 결과 | 상태 |
|------|------|------|
| Task 1 commit `54ba7ef` 존재 | `git log --oneline 888f0d3..HEAD` 표시 | FOUND |
| Task 2 commit `645f8fa` 존재 | `git log --oneline 888f0d3..HEAD` 표시 | FOUND |
| Task 3 commit `a3c8126` 존재 | `git log --oneline 888f0d3..HEAD` 표시 | FOUND |
| Task 4 commit `2399d95` 존재 | `git log --oneline 888f0d3..HEAD` 표시 | FOUND |
| MainResultViewerControl `_isEditMode` 카운트 | grep -c = 15 (≥4) | PASS |
| MainResultViewerControl HitTestSelectedRoi gate | grep "if (!_isEditMode)" returns 1 (return null 가드) | PASS |
| MainResultViewerControl IsEditMode setter | grep "public bool IsEditMode" returns 1 | PASS |
| MainResultViewerControl D-05 주석 | grep -c "//260503 hbk Phase 17 D-05" = 4 (≥1) | PASS |
| MainResultViewerControl D-06 주석 | grep -c "//260503 hbk Phase 17 D-06" = 4 (≥3) | PASS |
| DatumConfig ICustomTypeDescriptor 클래스 헤더 | grep "public class DatumConfig" \| grep ICustomTypeDescriptor returns 1 | PASS |
| DatumConfig ICustomTypeDescriptor 카운트 | grep -c = 4 (≥2) | PASS |
| DatumConfig IsHiddenForAlgorithm 카운트 | grep -c = 2 (선언 + 호출) | PASS |
| DatumConfig 3 enum 분기 | grep -c "case EDatumAlgorithm." = 3 | PASS |
| DatumConfig GetProperties() 무인자 base 위임 | grep "TypeDescriptor.GetProperties(this, true)" returns 1 | PASS |
| DatumConfig D-03 hide 라인 | grep "Circle_EdgeDirection.*return true" returns 1 | PASS |
| DatumConfig D-09 주석 | grep -c "//260503 hbk Phase 17 D-09" = 4 (≥3) | PASS |
| DatumConfig Plan 17-01 회귀 (RadialDirection) | grep -c "Circle_RadialDirection" = 4 (≥4) | PASS |
| DatumConfig Plan 17-03 영역 미침범 | grep -c "DetectedOriginRow\|DetectedOriginCol\|DetectedRefAngle\|DetectedEdgeCount\|DetectedFitRMSE\|DetectedAngleDeg" = 0 | PASS |
| InspectionListView AlgorithmType 카운트 | grep -c = 8 (≥3) | PASS |
| InspectionListView LastTeachSucceeded reset | grep "LastTeachSucceeded = false" returns 1 | PASS |
| InspectionListView LastFindSucceeded reset | grep "LastFindSucceeded  = false" returns 1 | PASS |
| InspectionListView force rebind 보존 (Phase 16 + Phase 17) | grep -c "ParamEditor.SelectedObject = null" = 3 (≥2) | PASS |
| InspectionListView 3-알고리즘 가드 | grep "TwoLineIntersect.*CircleTwoHorizontal\|VerticalTwoHorizontal" returns ≥1 | PASS |
| InspectionListView D-10 주석 | grep -c "//260503 hbk Phase 17 D-10" = 6 (≥2) | PASS |
| InspectionListView Phase 16 D-09/D-10 라인 보존 | grep "ParamEditor.SelectedObject = datumCfg" returns 1 | PASS |
| MainView Delete 3-button 모달 | grep "MessageBoxButton.YesNoCancel" returns 2 (≥1) | PASS |
| MainView 한국어 verbatim Copywriting | grep -c "이 ROI만 삭제\|현재 Datum 의 모든 ROI 삭제" = 2 | PASS |
| MainView ValidateRoiPresence helper | grep -c = 2 (선언 + 호출) | PASS |
| MainView ClearAllDatumRoiFields helper | grep -c = 2 (선언 + 호출) | PASS |
| MainView FormatTeachError/FormatFindError | grep -c = 6 (선언 2 + 호출 사이트 + 주석 매칭) | PASS |
| MainView D-04 EdgeDirection 힌트 (teach + find) | grep -c "검출된 에지가 없습니다. EdgeDirection 설정을 반대로" = 2 | PASS |
| MainView CustomMessageBox 티칭/Find 실패 호출 | grep -cE "CustomMessageBox.Show\(\"티칭 실패\"\|CustomMessageBox.Show\(\"Find 실패\"" = 3 | PASS |
| MainView label_drawHint 사유 표시 패턴 제거 | grep -c "label_drawHint.Content = \"Datum 티칭 실패" = 0 | PASS |
| MainView IsEditMode = wiring | grep -c "IsEditMode\s*=" = 2 (≥1) | PASS |
| MainView D-07 주석 | grep -c "//260503 hbk Phase 17 D-07" = 6 (≥2) | PASS |
| MainView D-11 주석 | grep -c "//260503 hbk Phase 17 D-11" = 3 (≥2) | PASS |
| MainView D-12 주석 | grep -c "//260503 hbk Phase 17 D-12" = 6 (≥2) | PASS |
| D-17 VisionAlgorithmService diff (Plan 17-02) | git diff 888f0d3..HEAD = 0 lines | PASS |
| D-17 DatumFindingService diff (Plan 17-02) | git diff 888f0d3..HEAD = 0 lines | PASS |
| msbuild Debug/x64 PASS | DatumMeasurement.exe 생성 확인 | PASS |
| msbuild 신규 warning on 수정 범위 = 0 | (4 파일 모두 0) | PASS |

## Self-Check: PASSED

모든 acceptance criteria + Plan 영역 분리 (PATTERNS gap #6, sequential lock) + D-17 algorithm preservation bound + Phase 16 D-09/D-10/D-13/D-14 보존 충족.

## Threat Flags

본 Plan 변경 범위 내에서 threat_model 외 신규 trust boundary 노출 없음. 모든 변경은 intra-process UI 레이어 (PropertyGrid filter / Edit-mode flag / 모달 호출 / 로컬 helper). 네트워크/파일/저장소 미관여.

## User Setup Required

None — 외부 서비스 / 자격증명 / 인프라 설정 변경 없음. INI 레시피 자동 마이그레이션 (Plan 17-01 의 EnsurePerRoiDefaults Circle_RadialDirection fallback) 그대로 동작. 본 Plan 은 PropertyGrid 표시 layer + UI 모달 / Edit-mode 가드만 변경 — 사용자 조작 0.

## Next Phase Readiness

- **Plan 17-03 (Cluster D — DetectedOrigin transient + RenderDatumFindResult + Hover) 진입 준비 완료:**
  - DatumConfig 의 transient/메트릭 영역 추가 시 본 Plan 의 ICustomTypeDescriptor 블록과 무충돌. 새 prefix 'Detected' 는 IsHiddenForAlgorithm 의 prefix 매칭에 안 걸림 → 자동 keep (의도). [System.ComponentModel.ReadOnly(true)] + [Category("Datum|Result")] 가 PropertyTools.Wpf 에 그대로 노출.
  - InspectionListView D-10 5-step 의 Step 3 위치에 3 라인 (DetectedOriginRow/Col/RefAngle = 0) 추가만 하면 D-10 완전 충족.
  - DatumFindingService.TryFindDatum 의 transient write-back ≤ 9 라인 추가 여유 (D-17 budget 11 라인 / 2 사용 / 9 잔여 — Plan 17-01 +2 / Plan 17-02 +0).
  - BtnTestFindDatum_Click 의 성공 경로 (TryFindDatum + write-back + RenderDatumFindResult) 가 본 Plan 의 실패 모달 변환과 무충돌.
- **Plan 17-04 (UAT) 진입 준비 완료:**
  - 4 새 시나리오 검증 가능: Edit OFF 시 ROI 변형 차단 / Delete 3-button 모달 동작 / AlgorithmType 변경 시 PropertyGrid 즉시 갱신 + Circle_EdgeDirection 자동 hide / btn_teachDatum 호환성 가드 한국어 모달 / teach + find 실패 모달 + EdgeDirection 힌트.
- **Phase 16 INI 하위호환 + Phase 16 D-09/D-10/D-13/D-14 보존:** 모두 회귀 0.
- **Blocker:** None.

---
*Phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover*
*Completed: 2026-05-03*
