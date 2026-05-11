---
quick_id: 260511-k3i
slug: roi-fallback-btn-rectroi-polygonroi-fai
date: 2026-05-11
status: complete
type: quick
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
autonomous: true
tags: [ui, roi, fai, regression-fix]
---

<objective>
신규 FAI 에서 ROI 버튼 (`btn_rectRoi`, `btn_polygonRoi`) 클릭 시 "FAI를 먼저 선택하세요" 모달이 뜨고 드로잉 모드로 진입하지 않는 회귀를 수정한다.

**Purpose:** 새로 추가한 FAI 는 Measurement 0개 → `dataGrid_faiResults` 가 비어 있고 `SelectedItem == null` → ROI 진입 거부. 트리 (`InspectionListView`) 에서 해당 FAI 가 선택돼 있음에도 무시되는 것이 근본 원인. 트리 선택을 1차 소스, dataGrid 선택을 fallback 으로 하여 두 경로 모두에서 ROI 드로잉이 가능하도록 한다.

**Output:** `MainView.xaml.cs` 의 `RectRoiButton_Click` (L1060) + `PolygonRoiButton_Click` (L1229) 두 핸들러의 FAI 해석 블록 수정. 단일 atomic commit.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

<interfaces>
<!-- 기존 코드베이스에서 검증된 API. 새 패턴 발명 금지. -->

From WPF_Example/UI/ControlItem/InspectionListView.xaml.cs L23:
```csharp
public ParamBase SelectedParam { get; private set; } = null;
```

From WPF_Example/UI/ContentItem/MainView.xaml.cs (기존 동일 패턴, L635-636 / L1421):
```csharp
DatumConfig datum = null;
if (mParentWindow != null && mParentWindow.inspectionList != null)
    datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
```

From WPF_Example/UI/ContentItem/MainView.xaml.cs L1066-1075 (수정 대상 - Rect):
```csharp
var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
//260509 hbk Phase 20 — ternary expanded
FAIConfig faiToEdit;
if (selectedRow != null) faiToEdit = FindFAIByName(selectedRow.FAIName);
else                     faiToEdit = null;
if (faiToEdit == null) {
    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Rect ROI");
    ExitCanvasMode();
    return;
}
_editingFai = faiToEdit;
```

From WPF_Example/UI/ContentItem/MainView.xaml.cs L1236-1246 (수정 대상 - Polygon, 위와 동일 구조):
```csharp
var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
//260509 hbk Phase 20 — ternary expanded
FAIConfig faiToEdit;
if (selectedRow != null) faiToEdit = FindFAIByName(selectedRow.FAIName);
else                     faiToEdit = null;
if (faiToEdit == null) {
    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Polygon ROI");
    ExitCanvasMode();
    return;
}
_editingFai = faiToEdit;
_polygonPoints.Clear();
```

`FindFAIByName(string)` 은 동일 파일 내 기존 helper. `FAIConfig` 는 `ParamBase` 의 하위 타입이므로 `SelectedParam as FAIConfig` 는 안전한 캐스트.
</interfaces>
</context>

<scope>
**In scope:**
- `RectRoiButton_Click` (L1060-1086) FAI 해석 블록
- `PolygonRoiButton_Click` (L1229-1260) FAI 해석 블록

**Out of scope (절대 손대지 말 것):**
- `CircleRoiButton_Click` (L1125-) — `FindSelectedCircleMeasurement()` 사용, 경로 다름
- `CommitRectRoi`, `CommitCircleRoi`, `CompletePolygon` 등 커밋 경로
- 트리 / dataGrid 선택 동기화 로직 (별도 회귀 위험)
- `FindFAIByName` 본체
- ROI Edit/Move 모드 (memory `project_roi_edit_mode_deferred.md` 참고 — deferred)
</scope>

<tasks>

<task type="auto">
  <name>Task 1: RectRoiButton_Click 에 트리 선택 우선 fallback 도입</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
`RectRoiButton_Click` (L1060 부근) 의 FAI 해석 블록을 다음 우선순위로 교체:

1. **1차:** `mParentWindow?.inspectionList?.SelectedParam as FAIConfig` — 트리에서 직접 FAI 노드가 선택된 경우 (신규 FAI 시나리오 커버). null 체크는 C# 7.2 호환 패턴 (`if (mParentWindow != null && mParentWindow.inspectionList != null)`) 으로 작성 — `?.` 체인 사용해도 무방하나 기존 파일의 L635 / L1421 패턴을 따를 것.
2. **2차 (fallback):** 기존 `dataGrid_faiResults.SelectedItem as MeasurementResultRow` → `FindFAIByName(selectedRow.FAIName)` — Measurement 행에서 클릭한 회귀 시나리오 보존.
3. 둘 다 null 인 경우에만 "FAI를 먼저 선택하세요." 모달 노출 후 `ExitCanvasMode(); return;`.

**구체적 교체 대상:** L1066-1075 의 `var selectedRow = ...` 부터 `_editingFai = faiToEdit;` (L1076) 직전까지.

**수정 패턴 예시 (이대로 적용):**
```csharp
//260511 hbk 신규 FAI(Measurement 0개) 회귀 — 트리 선택을 우선 사용, dataGrid 는 fallback
FAIConfig faiToEdit = null;
if (mParentWindow != null && mParentWindow.inspectionList != null)
    faiToEdit = mParentWindow.inspectionList.SelectedParam as FAIConfig;
if (faiToEdit == null) {
    //260511 hbk fallback — 기존 dataGrid 행 선택 경로 보존
    var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
    if (selectedRow != null) faiToEdit = FindFAIByName(selectedRow.FAIName);
}
if (faiToEdit == null) {
    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Rect ROI");
    ExitCanvasMode();
    return;
}
_editingFai = faiToEdit;
```

**제약:**
- 모든 변경 라인 (추가/수정) 에 `//260511 hbk` 주석 마커 (CLAUDE.md memory `feedback_comment_convention.md`).
- 기존 `//260417 hbk` / `//260509 hbk` 코멘트는 의미가 더는 정확하지 않으므로 제거하거나 새 마커 위로 이동시켜도 됨 — 단, 제거 시 그 자리에 새 `//260511 hbk` 주석으로 의도를 기록할 것.
- C# 7.2 — `?.` 체인은 사용 가능하나 본문은 위 패턴(명시적 null 체크) 유지하여 파일 내 기존 스타일과 일관성 확보.
- 8.0+ 기능 (switch expressions, `is not` 패턴, nullable reference types, target-typed `new()`) 사용 금지.
- 기존 brace 스타일 (K&R, 같은 줄 여는 중괄호) 유지 — 이 파일은 K&R.
- `_editingFai = faiToEdit;` 이후 (L1078~) 의 hint 라벨 / 이벤트 구독 / `StartRectangleDrawing()` 호출은 **수정하지 않음**.
  </action>
  <verify>
    <automated>
findstr /N /C:"260511 hbk" "WPF_Example\UI\ContentItem\MainView.xaml.cs"
    </automated>
    - `RectRoiButton_Click` 본문 영역(L1060 부근)에 `//260511 hbk` 마커가 최소 2개 (트리 fallback 진입 1, dataGrid fallback 1) 출현해야 함.
    - `mParentWindow.inspectionList.SelectedParam as FAIConfig` 문자열이 정확히 1회 새로 등장.
  </verify>
  <done>
    - `RectRoiButton_Click` 이 트리 우선 / dataGrid fallback 순서로 `faiToEdit` 를 해석.
    - 둘 다 null 인 경우에만 모달 노출.
    - `_editingFai` 할당 이후 흐름 (드로잉 시작, 이벤트 구독) 은 변경되지 않음.
    - 모든 변경 라인에 `//260511 hbk` 주석 존재.
  </done>
</task>

<task type="auto">
  <name>Task 2: PolygonRoiButton_Click 에 동일 트리 fallback 도입</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
`PolygonRoiButton_Click` (L1229 부근) 의 FAI 해석 블록을 Task 1 과 동일한 패턴으로 교체.

**구체적 교체 대상:** L1236-1245 의 `var selectedRow = ...` 부터 `_editingFai = faiToEdit;` (L1246) 직전까지. **L1247 의 `_polygonPoints.Clear();` 는 그대로 둠**.

**적용 패턴 (Rect 와 모달 타이틀만 다름):**
```csharp
//260511 hbk 신규 FAI(Measurement 0개) 회귀 — 트리 선택을 우선 사용, dataGrid 는 fallback
FAIConfig faiToEdit = null;
if (mParentWindow != null && mParentWindow.inspectionList != null)
    faiToEdit = mParentWindow.inspectionList.SelectedParam as FAIConfig;
if (faiToEdit == null) {
    //260511 hbk fallback — 기존 dataGrid 행 선택 경로 보존
    var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
    if (selectedRow != null) faiToEdit = FindFAIByName(selectedRow.FAIName);
}
if (faiToEdit == null) {
    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Polygon ROI");
    ExitCanvasMode();
    return;
}
_editingFai = faiToEdit;
```

**제약:**
- Task 1 과 **동일 제약** 적용 (`//260511 hbk` 마커, C# 7.2, K&R brace, fallback 순서 유지).
- 모달 타이틀은 반드시 `"Polygon ROI"` 유지 (Rect 와 구분).
- `_polygonPoints.Clear();` (L1247) 및 그 이후의 hint 라벨 / `label_pointCount` / 이벤트 구독 라인은 **수정하지 않음**.
  </action>
  <verify>
    <automated>
findstr /N /C:"Polygon ROI" "WPF_Example\UI\ContentItem\MainView.xaml.cs"
    </automated>
    - `PolygonRoiButton_Click` 본문 영역(L1229 부근)에 `//260511 hbk` 마커가 최소 2개 출현 (Task 1 의 마커 카운트와 합산하여 파일 전체 `//260511 hbk` 출현 ≥ 4).
    - `"Polygon ROI"` 타이틀 모달 호출이 변경 후에도 정확히 1회 유지됨.
  </verify>
  <done>
    - `PolygonRoiButton_Click` 이 Rect 와 동일한 우선순위 (트리 → dataGrid → 모달) 적용.
    - `_polygonPoints.Clear()` 이후 흐름 변경 없음.
    - 모든 변경 라인에 `//260511 hbk` 주석 존재.
  </done>
</task>

<task type="auto">
  <name>Task 3: msbuild Debug/x64 빌드 + 회귀 / scope 검증</name>
  <files>(no edits — build + grep verification only)</files>
  <action>
1. **msbuild Debug/x64 빌드** 실행. 신규 warning 0, error 0 확인.
2. **회귀/scope grep** 실행:
   - `RectRoiButton_Click` / `PolygonRoiButton_Click` 본문에 트리 fallback 진입 코드 존재.
   - `CircleRoiButton_Click` (L1125 부근) 은 **변경되지 않았어야 함** (`FindSelectedCircleMeasurement()` 호출 유지).
   - `CommitRectRoi` / `CompletePolygon` 본문 변경 없음.
3. **자료 매트릭스 (사용자 UAT 안내용 — 실제 실행은 사용자가 수행):**
   - (A) 새 FAI 추가 (Measurement 0) → 트리에서 해당 FAI 선택 → `btn_rectRoi` 클릭 → 드로잉 모드 진입, 모달 안 뜸. 드래그 → ROI 커밋 성공.
   - (B) 새 FAI 추가 → 트리에서 해당 FAI 선택 → `btn_polygonRoi` 클릭 → 드로잉 모드 진입. 3점 + 우클릭 → polygon 커밋 성공.
   - (C) 기존 FAI (Measurement 1+) → 트리 미선택 + dataGrid 행 선택 → `btn_rectRoi` / `btn_polygonRoi` 클릭 → 기존대로 동작.
   - (D) 트리 + dataGrid 둘 다 비선택 → 기존대로 "FAI를 먼저 선택하세요." 모달.
  </action>
  <verify>
    <automated>
"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" "WPF_Example\DatumMeasurement.csproj" /p:Configuration=Debug /p:Platform=x64 /v:minimal /nologo
    </automated>
    Build SUCCEEDED, 0 Error(s), 신규 Warning(s) 0 (기존 baseline 대비 증가 없음).

    추가 grep 검증:
```
findstr /N /C:"FindSelectedCircleMeasurement" "WPF_Example\UI\ContentItem\MainView.xaml.cs"
findstr /N /C:"260511 hbk" "WPF_Example\UI\ContentItem\MainView.xaml.cs"
```
    - `FindSelectedCircleMeasurement` 호출 (Circle 경로) 은 변경 전과 동일.
    - `//260511 hbk` 마커는 Rect (L1060 부근) + Polygon (L1229 부근) 영역에만 등장. Circle 영역 (L1125-1158) / Commit 본문에는 등장하지 않음.
  </verify>
  <done>
    - msbuild Debug/x64 0 Error / 신규 0 Warning.
    - Circle / Commit 경로 미변경 확인.
    - 회귀 매트릭스 4개 시나리오는 STATE 에 사용자 UAT 후보로 기록됨 (사용자가 직접 검증).
  </done>
</task>

</tasks>

<verification>
**Build (Claude-verifiable):**
- msbuild Debug/x64 PASS, 0 Error, 신규 0 Warning.

**Static (Claude-verifiable):**
- `MainView.xaml.cs` 의 `RectRoiButton_Click` / `PolygonRoiButton_Click` 양쪽에 트리-우선 fallback 코드 도입.
- `mParentWindow.inspectionList.SelectedParam as FAIConfig` 가 정확히 2회 추가 (Rect 1 + Polygon 1).
- `//260511 hbk` 주석 마커가 변경 라인마다 존재 (>= 4회).
- Circle 경로 (`FindSelectedCircleMeasurement`, `_editingCircleMeasurement`, `_editingCircleFaiName`) 미변경.
- Commit 경로 (`CommitRectRoi`, `CompletePolygon`, `CommitCircleRoi`) 미변경.

**Runtime (사용자 UAT — Claude 검증 불가, 보고서에 명시):**
- 시나리오 A: 새 FAI + 트리 선택 + btn_rectRoi → 드로잉 진입.
- 시나리오 B: 새 FAI + 트리 선택 + btn_polygonRoi → 드로잉 진입.
- 시나리오 C: 기존 FAI + dataGrid 행 선택 → 기존대로 동작 (회귀 없음).
- 시나리오 D: 트리/dataGrid 양쪽 미선택 → 기존 모달 정상 노출.
</verification>

<success_criteria>
- [ ] `RectRoiButton_Click` 트리-우선 fallback 적용 (Task 1).
- [ ] `PolygonRoiButton_Click` 트리-우선 fallback 적용 (Task 2).
- [ ] Circle 경로 + Commit 경로 미변경.
- [ ] msbuild Debug/x64 0 Error, 신규 0 Warning.
- [ ] 모든 변경 라인에 `//260511 hbk` 주석 존재.
- [ ] 단일 atomic commit (`fix(quick-260511-k3i): ROI 버튼 트리 선택 fallback`).
</success_criteria>

<output>
**Commit:** 단일 atomic commit
- 메시지: `fix(quick-260511-k3i): btn_rectRoi/btn_polygonRoi 트리 선택 fallback 으로 신규 FAI 회귀 수정`
- 변경 파일: `WPF_Example/UI/ContentItem/MainView.xaml.cs` 1개

**Summary 파일:** `.planning/quick/260511-k3i-roi-fallback-btn-rectroi-polygonroi-fai-/SUMMARY.md` 에 작성:
- 무엇을 바꿨는지 (Rect/Polygon 핸들러 두 곳)
- 왜 바꿨는지 (신규 FAI Measurement 0 → dataGrid 빈 상태 회귀)
- 빌드 / static 검증 결과
- 사용자 UAT 시나리오 4건 (PENDING 마크) — 사용자 수동 확인 항목으로 명시
</output>
