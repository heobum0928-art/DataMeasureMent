# Phase 18: Carry-over 정리 - Research

**Researched:** 2026-05-05
**Domain:** WPF/Halcon C# 결함 수정 (5 CO items)
**Confidence:** HIGH — all findings verified via codebase inspection

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** EdgeOptionLists.RadialDirections = {"Inward","Outward"} 리스트 자체는 정확하다.
- **D-02:** 버그 원인은 ICustomTypeDescriptor 필터링 후 PropertyTools.Wpf 에서 Directions (LtoR/RtoL/TtoB/BtoT 4개)로 잘못 해석되는 경로에 있다.
- **D-03:** INI 하위호환(EnsurePerRoiDefaults sentinel "" → "Inward" fallback) 유지.
- **D-04:** CO-03 코드 변경 없음 — 현재 IsConfigured 게이팅이 올바른 동작이다.
- **D-05:** 18-UAT.md Test 10을 재작성하여 IsConfigured 게이팅 동작을 spec으로 명문화한다.
- **D-06:** Acceptance에 `grep -c "ValidateRoiPresence" MainView.xaml.cs` 자동 검증 명령 추가.
- **D-07:** 노출 조건: `_canvasMode == ECanvasMode.TeachDatum` + 우클릭 위치 ROI hit-test 통과한 ROI가 있을 때만 메뉴 항목 표시.
- **D-08:** 레이블: "ROI 다시 그리기" (한국어).
- **D-09:** 동작: hit-test 통과한 ROI의 Length1/Length2/Radius를 0으로 리셋 — ClearDatumRoiFields 패턴 재사용. 자동 그리기 모드 진입 없음.
- **D-10:** 적용 대상: Datum ROI만 (FAI ROI는 좌클릭+드래그 재그리기로 이미 가능).
- **D-11:** 구현 위치: MainResultViewerControl.xaml ContextMenu에 항목 추가 + MainView.xaml.cs 우클릭 처리에서 hit-test 후 Visibility 제어.
- **D-12:** 적용 범위: Circle polar strip만 (CTH 알고리즘 전용). Horizontal A/B line strip은 이번 Phase 제외.
- **D-13:** 데이터 전달: DatumConfig.CircleStripSuccesses bool[] transient 필드 신설. [Browsable(false)], [JsonIgnore], [System.Text.Json.Serialization.JsonIgnore]. Phase 17 DetectedOriginRow transient 패턴 동일 방식.
- **D-14:** 갱신 시점: TryTeachCircleTwoHorizontal 완료 시에만 갱신. TryFindDatum(TryFindCircleTwoHorizontal) 시에는 갱신 없음.
- **D-15:** 색상 분기: CircleStripSuccesses[i] == true → "green" / false → "red" / 배열 null 또는 인덱스 범위 초과 → 기존 "gray" fallback.
- **D-16:** bool[] 크기 = stepCount (TryFindCircleByPolarSampling의 실제 strip 수).
- **D-17:** FormatTeachError(string err) 시그니처를 FormatTeachError(DatumConfig datum, string err)로 확장.
- **D-18:** 에러 메시지 앞에 "[{datum.Name}] " 접두사 추가.
- **D-19:** 기존 호출 사이트(CustomMessageBox.Show("티칭 실패", FormatTeachError(error)))에 datum 인자 추가.

### Claude's Discretion
- CO-01 버그의 정확한 root cause(PropertyTools 내부 바인딩 경로 vs ICustomTypeDescriptor 상호작용) — 연구자/실행자가 코드 탐색으로 결정.
- CO-04 hit-test 로직 구현 방식(기존 HitTestSelectedRoi 재사용 vs 별도 캔버스 좌표 변환) — 플래너가 결정.

### Deferred Ideas (OUT OF SCOPE)
- ArcEdgeDistance / CompoundAngle / LineConstructDistance 알고리즘 3종 → Phase 19.5 신규 Phase
- GR&R 엑셀 AIAG 표준 전체 — Phase 25 discuss 시 재검토
- Manual + Verify 워크플로우 (wizard 강제 단계 대신 자유 그리기 + Test 사이클) — v1.1 백로그
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CO-01 | DatumConfig.Circle_RadialDirection의 PropertyGrid ItemsSource를 Inward/Outward 두 값으로 제한한다 | ICustomTypeDescriptor.GetProperties 필터 결과에서 Browsable(false) List 프로퍼티가 누락 → PropertyTools 이름 조회 실패 root cause 확인. 수정 위치: DatumConfig.cs L545-554 |
| CO-03 | btn_teachDatum 알고리즘 호환성 가드의 사양을 명문화하고 spec으로 검증한다 | ValidateRoiPresence 코드 (L717-738) + IsConfigured 게이팅 (L1347-1362) 동작 확인. 18-UAT.md Test 10 재작성 대상 |
| CO-04 | ROI Length=0 escape hatch를 우클릭 메뉴로 노출한다 | MainResultViewerControl.xaml ContextMenu + UpdateContextMenuState 패턴 + HitTestRoiAtPoint + ClearDatumRoiFields 재사용 경로 확인 |
| CO-05 | 검출 strip 색상을 성공=녹색/실패=빨강으로 분기한다 | TryFindCircleByPolarSampling 루프 (VisionAlgorithmService L278-330) + TryTeachCircleTwoHorizontal (DatumFindingService L706-888) + RenderCircleStripOverlay (HalconDisplayService L457-516) 연결 경로 확인 |
| CO-06 | FormatTeachError 메시지에서 ROI label을 보존한다 | FormatTeachError 정의 (MainView.xaml.cs L741-749), 유일 호출 사이트 (L1605) 확인 |
</phase_requirements>

---

## Summary

Phase 18은 신규 기능 없이 5건의 결함만 수정하는 작은 정비 Phase다. 5건 모두 단일 파일 또는 2개 파일 이하 범위로 격리되어 있어 위험도가 낮다. 각 CO 항목의 root cause와 수정 경로가 코드 검사로 명확히 확인되었다.

CO-01은 ICustomTypeDescriptor.GetProperties(Attribute[])가 Browsable(false) 프로퍼티를 PropertyDescriptorCollection에서 제외할 때 PropertyTools.Wpf가 [ItemsSourceProperty] 이름 참조를 필터된 컬렉션에서만 찾아 실패하는 것이 root cause다. 수정법은 ICustomTypeDescriptor path의 GetProperties 필터에서 List<> 소스 프로퍼티를 제외하지 않는 것이다.

CO-04는 CONTEXT.md가 HalconViewerControl.xaml을 언급하지만 실제 Datum 티칭 캔버스는 `MainResultViewerControl`(xaml.cs의 `halconViewer`)이다. 이 차이가 구현 위치에 영향을 준다.

CO-05는 TryFindCircleByPolarSampling이 현재 per-strip 성공 여부를 반환하지 않는 구조이므로, 해당 정보를 TryTeachCircleTwoHorizontal에서 직접 수집해야 한다.

**Primary recommendation:** CO 항목들은 CO-01 → CO-04 → CO-05 → CO-06 → CO-03 순으로 구현. CO-03은 UAT 문서 수정만이므로 마지막에 배치 가능.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CO-01: PropertyGrid 콤보박스 바인딩 | DatumConfig.cs (Model) | — | ICustomTypeDescriptor 구현이 DatumConfig에 있음 |
| CO-03: 가드 spec 명문화 | 18-UAT.md (Spec doc) | — | 코드 변경 없음, 문서만 수정 |
| CO-04: 우클릭 메뉴 항목 | MainResultViewerControl.xaml (View) | MainView.xaml.cs (Code-behind) | ContextMenu는 View, 가시성 제어 로직은 Code-behind |
| CO-05: Strip 색상 분기 | DatumFindingService.cs (Algorithm) | DatumConfig.cs (Model) / HalconDisplayService.cs (Render) | 데이터 생성은 알고리즘, 전달은 Model 필드, 소비는 렌더러 |
| CO-06: 에러 메시지 포맷 | MainView.xaml.cs (Code-behind) | — | FormatTeachError가 MainView에만 정의됨 |

---

## CO-01: Circle_RadialDirection PropertyGrid ItemsSource Root Cause

### 확인된 Root Cause

[VERIFIED: codebase inspection]

`DatumConfig`는 `ICustomTypeDescriptor`를 구현하며, PropertyTools.Wpf는 PropertyGrid를 렌더링할 때 `ICustomTypeDescriptor.GetProperties(Attribute[])` (L545-554)를 호출한다.

`GetProperties(Attribute[])` 내부는:
```csharp
var all = TypeDescriptor.GetProperties(this, attributes, true);
// ... IsHiddenForAlgorithm 필터 ...
return new PropertyDescriptorCollection(keep.ToArray());
```

반환된 `PropertyDescriptorCollection`에는 `IsHiddenForAlgorithm` 기준으로 살아남은 항목만 포함된다. 그러나 `[Browsable(false)]`로 마킹된 프로퍼티들(ex: `Circle_RadialDirectionList`)은 `TypeDescriptor.GetProperties(this, attributes, true)` 호출 자체에서 이미 제외될 수 있다.

PropertyTools.Wpf는 `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]`를 읽고 `"Circle_RadialDirectionList"` 라는 이름의 프로퍼티를 **동일한 PropertyDescriptorCollection** 안에서 찾는다. `Circle_RadialDirectionList`가 Browsable(false)로 인해 컬렉션에서 빠지면, PropertyTools는 이름 조회에 실패하고 fallback으로 첫 번째 발견되는 `List<string>` 프로퍼티 (`EdgeOptionLists.Directions` = 4개 항목)를 사용하거나 에러 없이 잘못된 목록을 바인딩한다.

**결정적 증거:**
- `Circle_RadialDirectionList` L249: `[PropertyTools.DataAnnotations.Browsable(false)]` — 필터됨
- `Circle_RadialDirection` L239: `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` — 이름 참조가 필터된 컬렉션에서 못 찾음
- CTH 분기에서 `Circle_*` 프리픽스는 `IsHiddenForAlgorithm` 에서 숨기지 않음 (`Circle_EdgeDirection` 만 hide) — 따라서 `IsHiddenForAlgorithm` 자체가 문제는 아님
- 문제는 `attributes` 파라미터 기반 Browsable 필터 — `TypeDescriptor.GetProperties(this, attributes, true)` 호출 시 `BrowsableAttribute(false)` 항목이 제외됨

### 최소 수정 방법 (2가지 선택지)

**Option A (권장): GetProperties에서 Browsable(false) 소스 프로퍼티를 keep에 포함**

`IsHiddenForAlgorithm` 필터 이전에 `[ItemsSourceProperty]` 참조 대상 프로퍼티는 항상 keep에 포함시킨다:

```csharp
// DatumConfig.cs GetProperties(Attribute[]) 수정
foreach (System.ComponentModel.PropertyDescriptor pd in all) {
    if (IsHiddenForAlgorithm(pd.Name, alg)) continue;
    keep.Add(pd);
}
// ItemsSource 소스 프로퍼티는 Browsable=false 이므로 all 에 이미 없음
// → all 을 가져올 때 Browsable 필터 없이 가져와야 함
```

실제로 `TypeDescriptor.GetProperties(this, attributes, true)` 를 호출할 때 `attributes` 가 `[BrowsableAttribute(true)]` 조건을 포함하면 Browsable(false) 항목이 걸러진다. 수정: 소스 프로퍼티 이름들을 whitelist로 keep에 강제 추가:

```csharp
// //260505 hbk Phase 18 CO-01 — ItemsSource 소스 List<> 프로퍼티는 Browsable(false)이므로
// attributes 필터에서 제외됨 → PropertyTools [ItemsSourceProperty] 이름 조회 실패.
// 소스 프로퍼티를 명시적으로 재조회하여 keep 에 추가.
var allNoFilter = System.ComponentModel.TypeDescriptor.GetProperties(this, true); // Browsable 포함
var sourceNames = new System.Collections.Generic.HashSet<string> {
    nameof(AlgorithmTypeList),
    nameof(Circle_EdgeDirectionList), nameof(Circle_EdgePolarityList),
    nameof(Circle_EdgeSelectionList), nameof(Circle_RadialDirectionList),
    nameof(Horizontal_A_EdgeDirectionList), nameof(Horizontal_A_EdgePolarityList),
    nameof(Horizontal_A_EdgeSelectionList),
    nameof(Horizontal_B_EdgeDirectionList), nameof(Horizontal_B_EdgePolarityList),
    nameof(Horizontal_B_EdgeSelectionList),
    nameof(Line1_EdgeDirectionList), nameof(Line1_EdgePolarityList), nameof(Line1_EdgeSelectionList),
    nameof(Line2_EdgeDirectionList), nameof(Line2_EdgePolarityList), nameof(Line2_EdgeSelectionList),
    nameof(Vertical_EdgeDirectionList), nameof(Vertical_EdgePolarityList), nameof(Vertical_EdgeSelectionList),
};
foreach (System.ComponentModel.PropertyDescriptor pd in allNoFilter) {
    if (sourceNames.Contains(pd.Name) && !keep.Any(k => k.Name == pd.Name))
        keep.Add(pd);
}
```

**Option B (더 단순): TypeDescriptor.GetProperties 호출 시 attributes를 null 또는 빈 배열로 교체 후 Browsable 필터를 수동 적용**

```csharp
var all = System.ComponentModel.TypeDescriptor.GetProperties(this, true); // attributes 제거 → 전체
// ... Browsable(true) 항목 + 소스 프로퍼티 모두 포함 ...
```

**권장: Option A** — 기존 코드 구조 변경 최소화. `attributes` 파라미터 의미를 유지하면서 소스 프로퍼티만 whitelist로 추가.

### 검증 방법
- Build 후 PropertyGrid에서 CTH Datum 선택 → Circle_RadialDirection 콤보박스 옵션이 "Inward"/"Outward" 2개인지 육안 확인
- `grep -c "Circle_RadialDirectionList" DatumConfig.cs` > 0 (참조 보존 확인)

---

## CO-03: ValidateRoiPresence Spec 명문화

### 현재 코드 동작 (IsConfigured 게이팅)

[VERIFIED: MainView.xaml.cs L1347-1362]

```csharp
// TeachDatumButton_Click (L1330-)
if (datum.IsConfigured) {           // 재티칭(IsConfigured=true)일 때만 가드 실행
    string missingRoiMsg = ValidateRoiPresence(datum, datum.AlgorithmTypeEnum);
    if (missingRoiMsg != null) {
        CustomMessageBox.Show("티칭 실패", missingRoiMsg);
        // btn_teachDatum OFF + canvasMode = None + IsEditMode = false
        return;
    }
}
// IsConfigured=false (새 Datum) → wizard(StartDatumTeachStep)로 진행
```

`ValidateRoiPresence` (L717-738) 반환 메시지:
- TLI: `"Line1/Line2 ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요."`
- CTH Circle 없음: `"Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요."`
- CTH Horizontal 없음: `"Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요."`
- VTH Vertical 없음: `"Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요."`
- VTH Horizontal 없음: `"Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요."`

### Test 10 재작성 방향

Phase 17 UAT Test 10은 `IsConfigured=false` (새 Datum, ROI 미생성) 시나리오를 가정하여 `ValidateRoiPresence` 모달이 표시된다고 기술했다. 그러나 `IsConfigured=false`이면 가드가 스킵된다 — 이것이 Phase 17 결함이 아니라 올바른 동작임(D-04 결정).

CO-03 deliverable은 18-UAT.md Test 10 재작성:
- **시나리오 A (IsConfigured=false):** 새 Datum + ROI 미생성 → 모달 없이 wizard 진행. ROI 그리도록 안내받음.
- **시나리오 B (IsConfigured=true 재티칭, ROI 삭제 후):** 이미 티칭된 Datum의 ROI를 Delete한 후 btn_teachDatum 클릭 → ValidateRoiPresence 모달 표시.
- **자동 검증 명령:** `grep -c "ValidateRoiPresence" WPF_Example/UI/ContentItem/MainView.xaml.cs` → 최소 2 (정의 + 호출)

---

## CO-04: 우클릭 메뉴 구현 (ROI 다시 그리기)

### 중요 발견: 대상 컨트롤은 HalconViewerControl이 아닌 MainResultViewerControl

[VERIFIED: codebase inspection]

CONTEXT.md가 참조한 `HalconViewerControl.xaml` (L17-22)은 단순한 Zoom/Fit 전용 뷰어로 Datum 티칭 캔버스와 무관하다. 실제 Datum ROI 그리기, hit-test, 우클릭 메뉴는 모두 **MainResultViewerControl** (`halconViewer` 인스턴스)에 있다.

**실제 구현 위치:**
- XAML: `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml` — ContextMenu에 `"ROI 다시 그리기"` MenuItem 추가
- 코드: `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` `UpdateContextMenuState()` (L1418-1452) — Visibility/IsEnabled 제어
- 코드: `WPF_Example/UI/ContentItem/MainView.xaml.cs` — RoiDeleteRequested 이벤트 패턴 참고하여 새 이벤트 또는 델리게이트 연결

### 기존 우클릭 메뉴 구조

MainResultViewerControl.xaml (L16-40):
```xml
<ContextMenu x:Name="ViewerContextMenu">
    <MenuItem x:Name="EditRoiMenuItem" Header="Edit ROI" Click="EditRoiMenuItem_Click"/>
    <MenuItem x:Name="DeleteRoiMenuItem" Header="Delete ROI" Click="DeleteRoiMenuItem_Click"/>
    <Separator/>
    <MenuItem x:Name="CrosshairMenuItem" Header="Crosshair" .../>
    <Separator/>
    <MenuItem x:Name="ManualMeasureMenuItem" Header="Manual Measure" .../>
    <MenuItem x:Name="ClearMeasureMenuItem" Header="Clear Measure" .../>
    <Separator/>
    <MenuItem Header="Zoom In" .../> <MenuItem Header="Zoom Out" .../> 
    <Separator/>
    <MenuItem Header="Fit Image" .../>
</ContextMenu>
```

추가 위치: `DeleteRoiMenuItem` 바로 다음 (또는 Separator 전).

### HitTestRoiAtPoint 재사용

[VERIFIED: MainResultViewerControl.xaml.cs L340-367]

`HitTestRoiAtPoint(Point imagePoint)` 는 `_isEditMode` 게이트 없이 `_rois` + `_datumRoiCandidates` 모두에서 hit-test를 수행한다 (Phase 17 hotfix#5). 이 메서드를 CO-04 "ROI 다시 그리기" 가시성 결정에 재사용할 수 있다.

**동작 결정 로직 (UpdateContextMenuState 내부에 추가):**
```csharp
// //260505 hbk Phase 18 CO-04 — "ROI 다시 그리기" 메뉴 가시성 결정
// 조건: _canvasMode == ECanvasMode.TeachDatum + 우클릭 위치에 Datum ROI hit
if (RedrawRoiMenuItem != null) {
    bool isTeachMode = (_canvasMode == ECanvasMode.TeachDatum); // MainView에서 전달받은 상태
    var hitRoi = isTeachMode ? HitTestRoiAtPoint(_lastMouseImagePoint) : null;
    bool isDatumRoi = hitRoi != null && hitRoi.Id != null && hitRoi.Id.StartsWith("Datum.");
    RedrawRoiMenuItem.Visibility = isDatumRoi ? Visibility.Visible : Visibility.Collapsed;
}
```

### 아키텍처 고려사항

MainResultViewerControl은 `_canvasMode`를 직접 알지 못한다 (MainView의 private 필드). 두 가지 설계 방향:

**방향 A (권장):** MainResultViewerControl에 `IsTeachDatumMode bool` public 프로퍼티 추가. MainView의 TeachDatumButton_Click에서 true/false 설정. UpdateContextMenuState에서 이 프로퍼티를 참조.

**방향 B:** MainView에서 `ViewerContextMenu.Opened` 이벤트 구독 후 Visibility 직접 제어 (더 간단하지만 control 경계 침범).

**방향 A가 더 clean:** MainResultViewerControl이 자체적으로 상태 관리.

### ClearDatumRoiFields 연결

CO-04 클릭 핸들러는 `RoiDeleteRequested` 이벤트 패턴과 유사하게 새 이벤트 `RoiRedrawRequested` (또는 기존 이벤트 재사용)를 발생시키고, MainView.xaml.cs에서 구독하여 `ClearDatumRoiFields(datum, hitRoi.Id)` 를 호출:

```csharp
// MainView.xaml.cs 구독
halconViewer.RoiRedrawRequested += (s, roiId) => {
    //260505 hbk Phase 18 CO-04 — ROI 다시 그리기: Length 0 리셋 후 오버레이 갱신
    if (_editingDatum != null)
        ClearDatumRoiFields(_editingDatum, roiId);
    halconViewer.SetDatumOverlay(_editingDatum, false);
};
```

---

## CO-05: Strip 색상 분기 (성공=녹색, 실패=빨강)

### TryTeachCircleTwoHorizontal → TryFindCircleByPolarSampling 데이터 흐름

[VERIFIED: DatumFindingService.cs L706-888, VisionAlgorithmService.cs L278-330]

**현재 흐름:**
```
TryTeachCircleTwoHorizontal
  → visionSvc.TryFindCircleByPolarSampling(...)
      → 내부 루프: for i=0..stepCount-1
          → GenMeasureRectangle2 + MeasurePos per strip
          → 성공 시 allRows/allCols에 누적
          → 실패 시 swallow (continue)
      → out edgeRows, edgeCols (성공 누적만)
  → config.CircleCenter_Row/Col = centerRow/Col
  → config.Circle_DetectedEdgeRows = circleEdgeRows  (성공 점만)
  → TryTeachCircleTwoHorizontal returns true/false
```

**문제:** `TryFindCircleByPolarSampling`이 per-strip 성공/실패를 반환하지 않는다. `edgeRows`는 성공한 strip의 점만 누적되어 어느 index가 성공인지 알 수 없다.

### DatumConfig.CircleStripSuccesses bool[] 신설 위치

[VERIFIED: DatumConfig.cs L427-439 DetectedOriginRow/Col/RefAngle 패턴]

```csharp
// DatumConfig.cs — DetectedOriginRow 바로 다음 (L440 부근)에 추가
//260505 hbk Phase 18 CO-05 — Circle polar strip 별 검출 성공 여부 (TryTeachCircleTwoHorizontal write-back).
//  INI/JSON 직렬화 제외 (transient). RenderCircleStripOverlay 가 소비.
[System.ComponentModel.Browsable(false)] //260505 hbk Phase 18 CO-05
[PropertyTools.DataAnnotations.Browsable(false)] //260505 hbk Phase 18 CO-05
[Newtonsoft.Json.JsonIgnore] //260505 hbk Phase 18 CO-05
public bool[] CircleStripSuccesses { get; set; } //260505 hbk Phase 18 CO-05
```

### TryTeachCircleTwoHorizontal 수정 포인트

`visionSvc.TryFindCircleByPolarSampling` 호출은 per-strip 결과를 외부로 반환하지 않는다. 두 가지 접근:

**접근 A (권장):** `TryFindCircleByPolarSampling`에 `out bool[] stripSuccesses` 파라미터 추가.

```csharp
// VisionAlgorithmService.cs TryFindCircleByPolarSampling 시그니처 확장
public bool TryFindCircleByPolarSampling(
    ..., out bool[] stripSuccesses, out string error)
{
    stripSuccesses = new bool[stepCount];
    for (int i = 0; i < stepCount; i++) {
        try {
            // ... MeasurePos ...
            if (eRows.TupleLength() > 0) {
                stripSuccesses[i] = true;
                // ... accumulate ...
            }
        } catch {
            stripSuccesses[i] = false;
        }
    }
}
```

그 다음 `TryTeachCircleTwoHorizontal`에서:
```csharp
bool[] strips;
if (!visionSvc.TryFindCircleByPolarSampling(..., out strips, out circleError)) { ... }
config.CircleStripSuccesses = strips; //260505 hbk Phase 18 CO-05
```

**접근 B:** `TryTeachCircleTwoHorizontal`에서 직접 루프 구현 (코드 중복). 비권장.

### RenderCircleStripOverlay 수정 포인트

[VERIFIED: HalconDisplayService.cs L457-516]

현재 L484: `HOperatorSet.SetColor(window, "gray");` — 고정 색상.

수정:
```csharp
// //260505 hbk Phase 18 CO-05 — strip 별 성공/실패 색상 분기
// bool[] successes = datum.CircleStripSuccesses;
for (int i = 0; i < stepCount; i++) {
    // 색상 결정
    string stripColor = "gray"; // fallback
    if (successes != null && i < successes.Length)
        stripColor = successes[i] ? "green" : "red";
    HOperatorSet.SetColor(window, stripColor);
    // ... DispLine x4 ...
}
```

`SetColor`를 루프 외부에서 한번만 호출하던 방식에서 루프 내부 per-strip 호출로 변경. 이것이 주요 변경.

### 기존 `circle_DetectedEdgeRows` 오버레이와 충돌 없음

`RenderDatumOverlay` 내 Circle 에지점 렌더(cyan 점)와 strip 색상 변경은 독립적. `HOperatorSet.SetColor`는 이후 모든 draw 명령에 영향을 주므로 strip 루프 후 다시 색상을 원래 값으로 복원해야 한다 (또는 `SetColor` 시퀀스 보장). `RenderCircleStripOverlay`는 `RenderDatumOverlay`에서 호출되는 private 메서드이므로 caller에서 색상 상태를 복원해야 한다.

---

## CO-06: FormatTeachError 시그니처 확장

### 현재 상태

[VERIFIED: MainView.xaml.cs L741-749, L1603-1605]

**정의 (L741-749):**
```csharp
private static string FormatTeachError(string err) {
    if (err == null) err = "unknown";
    if (err.IndexOf("no edges", ...) >= 0
        || err.IndexOf("insufficient edges", ...) >= 0
        || err.IndexOf("insufficient polar samples", ...) >= 0) {
        return "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.";
    }
    return "티칭에 실패했습니다: " + err;
}
```

**유일한 호출 사이트 (L1605):**
```csharp
CustomMessageBox.Show("티칭 실패", FormatTeachError(error));
```

호출 사이트는 **1개** (L1605). `InvokeTryTeachDatumForEdit`(L788)는 `FormatTeachError`를 사용하지 않고 직접 `error`를 표시한다.

**FormatFindError**는 별도 메서드(L752-760) — CO-06 대상 아님, 변경 없음.

### 수정 계획

**시그니처 변경:**
```csharp
//260505 hbk Phase 18 CO-06 — datum 인자 추가 → 에러 메시지에 ROI label 접두사
private static string FormatTeachError(DatumConfig datum, string err) {
    if (err == null) err = "unknown";
    string prefix = (datum != null && !string.IsNullOrEmpty(datum.DatumName))
        ? "[" + datum.DatumName + "] " : "";
    if (err.IndexOf("no edges", ...) >= 0 || ...) {
        return prefix + "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.";
    }
    return prefix + "티칭에 실패했습니다: " + err;
}
```

CONTEXT.md D-18: `datum.Name` 사용. DatumConfig에는 `DatumName` 프로퍼티(L19)가 있으나 `Name`이라는 별도 프로퍼티는 보이지 않는다. 실행자는 `datum.DatumName`을 사용해야 한다.

**호출 사이트 수정 (L1605):**
```csharp
CustomMessageBox.Show("티칭 실패", FormatTeachError(_editingDatum, error)); //260505 hbk Phase 18 CO-06
```

`_editingDatum`이 이미 이 문맥에서 설정되어 있어 null이 아니다 (L1574 체크).

---

## Build Risk & Ordering

### 파일별 변경 범위

| CO | 파일 | 변경 규모 | 위험도 |
|----|------|-----------|--------|
| CO-01 | DatumConfig.cs | ~15 라인 추가 (GetProperties 수정) | LOW — 격리된 ICustomTypeDescriptor 메서드 |
| CO-03 | 18-UAT.md | Test 10 텍스트 수정 | 없음 (코드 변경 0) |
| CO-04 | MainResultViewerControl.xaml + .xaml.cs + MainView.xaml.cs | ~30 라인 (XAML 1 + cs 2파일) | LOW-MEDIUM — 이벤트 추가, 기존 UpdateContextMenuState 확장 |
| CO-05 | VisionAlgorithmService.cs + DatumConfig.cs + HalconDisplayService.cs | ~25 라인 (3파일) | MEDIUM — TryFindCircleByPolarSampling 시그니처 변경, 기존 호출자 2곳 갱신 필요 |
| CO-06 | MainView.xaml.cs | ~5 라인 (시그니처 + 1 호출) | LOW — 1개 호출 사이트 |

### TryFindCircleByPolarSampling 호출자 목록 (CO-05 위험 확인)

[VERIFIED: codebase search]

```
DatumFindingService.cs:
  L730: TryTeachCircleTwoHorizontal (teaching path) — CO-05 수정 대상
  L201: TryFindCircleTwoHorizontal (find path) — D-14: 갱신 없음 → out 파라미터 null 전달 또는 _ 무시
```

`TryFindCircleByPolarSampling`에 `out bool[] stripSuccesses` 추가 시, `TryFindCircleTwoHorizontal` (L201)도 호출자이므로 컴파일 에러 방지를 위해 해당 호출부도 갱신해야 한다. D-14 정책에 따라 TryFind 경로에서는 `config.CircleStripSuccesses = null` 또는 갱신 없음으로 처리.

### 항목 간 의존 관계

```
CO-01 → 독립 (DatumConfig.cs만)
CO-03 → 독립 (UAT 문서만)
CO-04 → 독립 (MainResultViewerControl + MainView)
CO-05 → 독립 (알고리즘 레이어 + DatumConfig + DisplayService)
CO-06 → 독립 (MainView.xaml.cs만)
```

5개 CO 항목은 서로 의존 관계 없음. 병렬 Wave 구성 가능.

### 권장 Plan 구성

```
Plan 18-01: CO-01 — DatumConfig.cs GetProperties Browsable 소스 프로퍼티 whitelist
Plan 18-02: CO-04 — MainResultViewerControl 우클릭 "ROI 다시 그리기" 메뉴
Plan 18-03: CO-05 — TryFindCircleByPolarSampling strip 성공/실패 + DatumConfig 필드 + RenderCircleStripOverlay 색상
Plan 18-04: CO-06 — FormatTeachError 시그니처 확장 (datum.DatumName 접두사)
Plan 18-05: CO-03 — 18-UAT.md Test 10 재작성 (코드 변경 0)

Wave 1: Plan 18-01, 18-02, 18-04 (독립적, 작음)
Wave 2: Plan 18-03 (알고리즘 레이어 관련, 별도 검증 필요)
Wave 3: Plan 18-05 (UAT 문서, 기술 구현 완료 후)
```

---

## Validation Architecture

> workflow.nyquist_validation 미설정 → 활성

### Test Framework
| Property | Value |
|----------|-------|
| Framework | MSBuild 15.0 수동 빌드 (프로젝트에 xUnit/NUnit 없음) |
| Config file | WPF_Example/DatumMeasurement.csproj |
| Quick run command | `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` |
| Full suite command | `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` (단일 빌드, 자동 테스트 없음) |

### Phase Requirements → Verification Map

| Req ID | 동작 | 검증 유형 | 명령 / 방법 |
|--------|------|-----------|-------------|
| CO-01 | Circle_RadialDirection PropertyGrid 콤보박스 2항목 | 런타임 육안 | CTH Datum 선택 → PropertyGrid Circle_RadialDirection 드롭다운 옵션 2개 확인 |
| CO-01 | 빌드 무결성 | 빌드 | msbuild Debug/x64 PASS |
| CO-03 | ValidateRoiPresence 코드 존재 | grep | `grep -c "ValidateRoiPresence" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 2 |
| CO-03 | IsConfigured=false 시 모달 미표시 | 런타임 | 새 Datum + ROI 미생성 + btn_teachDatum → 모달 없이 wizard 진행 |
| CO-03 | IsConfigured=true + ROI 삭제 시 모달 | 런타임 | 기존 Datum ROI Delete → btn_teachDatum → "Circle/Horizontal ROI 없습니다" 모달 |
| CO-04 | 우클릭 메뉴 항목 표시/숨김 | 런타임 | TeachDatum 모드 ON + Datum ROI 위 우클릭 → "ROI 다시 그리기" 항목 보임 |
| CO-04 | ROI Length=0 리셋 동작 | 런타임 | 클릭 후 해당 ROI 오버레이 사라짐 확인 |
| CO-05 | 성공 strip 녹색 | 런타임 | CTH 티칭 성공 후 오버레이에서 녹색 strip 확인 |
| CO-05 | 실패 strip 빨강 | 런타임 | 일부 edge 검출 실패 케이스에서 빨강 strip 확인 |
| CO-06 | 에러 메시지에 DatumName 포함 | 런타임 | 티칭 실패 시 모달에서 "[DatumName] ..." 형식 확인 |
| CO-06 | FormatTeachError 시그니처 변경 | grep | `grep -c "FormatTeachError" WPF_Example/UI/ContentItem/MainView.xaml.cs` ≥ 2 |

### Wave 0 Gaps
없음 — 이 Phase는 런타임 육안 검증 + 빌드 검증만 필요. 자동화 테스트 프레임워크 부재는 프로젝트 전반의 기존 방침.

---

## Common Pitfalls

### Pitfall 1: CO-01 TypeDescriptor.GetProperties attributes 파라미터 의미 오해
**What goes wrong:** `TypeDescriptor.GetProperties(this, attributes, true)` 에서 `attributes`가 null이면 모든 프로퍼티를 반환하므로 문제없다고 가정하고, Browsable(false) 항목이 왜 빠지는지 못 찾음.
**Why it happens:** PropertyTools PropertyGrid 내부에서 `GetProperties(new Attribute[] { BrowsableAttribute.Yes })` 형태로 호출할 수도 있고, 또는 ICustomTypeDescriptor.GetProperties(null)로 호출 후 내부에서 Browsable 필터링을 별도로 수행할 수 있다. 실제 PropertyTools 호출 방식에 따라 원인이 달라질 수 있다.
**How to avoid:** Option A whitelist 접근으로 소스 프로퍼티를 명시적으로 keep에 추가 — attributes 인자 값에 무관하게 안전.
**Warning signs:** PropertyGrid 콤보박스가 4개 항목 (Directions 목록) 또는 비어있으면 이 문제.

### Pitfall 2: CO-04 _canvasMode 공유
**What goes wrong:** `MainResultViewerControl`이 `MainView`의 `_canvasMode`를 직접 알 수 없어서 IsTeachDatumMode 상태 전달을 빠뜨림.
**Why it happens:** 두 클래스가 독립적 — MainResultViewerControl은 범용 뷰어이므로 DatumConfig 개념 없음.
**How to avoid:** D-11 결정대로 MainView.xaml.cs에서 TeachDatumButton_Click 시 `halconViewer.IsTeachDatumMode = true/false` 설정.

### Pitfall 3: CO-05 TryFindCircleTwoHorizontal 호출자 갱신 누락
**What goes wrong:** `TryFindCircleByPolarSampling`에 `out bool[] stripSuccesses` 추가 시 `TryFindCircleTwoHorizontal` (L201)도 호출하므로 컴파일 에러.
**Why it happens:** 검색으로 teach 경로만 찾고 find 경로를 놓침.
**How to avoid:** `TryFindCircleTwoHorizontal`에서는 `out _` (C# 7.0 discard 문법은 7.2에서 사용 가능) 또는 임시 변수로 수신 후 무시.

### Pitfall 4: CO-05 SetColor 상태 오염
**What goes wrong:** RenderCircleStripOverlay 루프 안에서 SetColor를 per-strip 변경하면, 루프 완료 후 HalconWindow 색상 상태가 마지막 strip 색상으로 남아 이후 렌더링에 영향을 줄 수 있음.
**Why it happens:** Halcon Window는 상태 머신 — SetColor는 전역 상태.
**How to avoid:** 루프 완료 후 `HOperatorSet.SetColor(window, "gray")` 또는 caller(`RenderDatumOverlay`)에서 복원.

### Pitfall 5: CO-06 datum.Name vs datum.DatumName
**What goes wrong:** D-18에서 `datum.Name` 으로 표기했으나 DatumConfig의 실제 프로퍼티는 `DatumName` (L19). `Name`은 없는 프로퍼티이므로 컴파일 에러.
**Why it happens:** CONTEXT.md의 예시가 추상적 표기를 사용.
**How to avoid:** `datum.DatumName`을 사용.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CO-05 strip 성공 추적 | 별도 추적 dict/list 구조 | bool[] (index = i in stepCount loop) | 루프 인덱스와 직접 대응, null check + bounds check로 안전 |
| CO-04 hit-test | 새 좌표 변환 로직 | `HitTestRoiAtPoint(Point)` (L340-367) 재사용 | 이미 Rect+Circle+Polygon + _datumRoiCandidates 포함 |
| CO-04 ROI 리셋 | 필드 개별 0 설정 | `ClearDatumRoiFields(datum, roiId)` (L667-701) 재사용 | 6개 RoiId 분기 + IsConfigured/LastTeachSucceeded 리셋 포함 |

---

## Environment Availability

Step 2.6: SKIPPED — Phase 18은 코드/문서 수정만, 외부 도구/서비스 의존 없음. 빌드에 필요한 Halcon, MSBuild, 카메라 SDK는 개발 환경에 기 설치된 것으로 가정 (Phase 17까지 성공적으로 빌드됨).

---

## Security Domain

> security_enforcement 설정 부재 → 기본 활성이나, Phase 18은 UI 메시지/색상/메뉴 표시 로직 수정만 포함. 인증, 네트워크, 파일 I/O, 암호화 관련 변경 없음.

| ASVS Category | Applies | Note |
|---------------|---------|------|
| V5 Input Validation | No | 에러 메시지 포맷만, 입력 검증 없음 |
| Other categories | No | 이번 Phase 범위 외 |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | PropertyTools.Wpf가 [ItemsSourceProperty] 이름을 GetProperties 반환 컬렉션에서 찾는다 | CO-01 | 만약 PropertyTools가 직접 reflection으로 조회하면 CO-01 버그 root cause가 다른 것 — 실행자가 빌드 후 동작 확인 필요 |
| A2 | DatumConfig.Name == datum.DatumName | CO-06 | CONTEXT.md는 datum.Name 사용, 코드에는 DatumName만 존재 — 다른 이름의 Name 프로퍼티가 없음을 확인함 |
| A3 | TryFindCircleByPolarSampling 호출자가 DatumFindingService.cs 내 2곳뿐 | CO-05 | 더 있으면 컴파일 에러 — grep으로 확인 후 갱신 필요 |

**A1은 실행 전 검증 권장:** `grep -r "ItemsSourceProperty" packages/PropertyTools*` 또는 PropertyTools 소스에서 이름 해석 경로 확인. 만약 PropertyTools가 직접 `Type.GetProperty(name)`으로 조회한다면 CO-01 버그는 다른 원인이다.

---

## Sources

### Primary (HIGH confidence)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` L239-249, L545-554, L427-440, L462-538 — CO-01, CO-05 직접 코드 검사
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` L717-738, L741-749, L1330-1376, L1574-1612 — CO-03, CO-06 직접 코드 검사
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml` L16-41 — CO-04 ContextMenu 구조
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` L307-367, L1418-1452 — CO-04 hit-test + UpdateContextMenuState
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` L457-516 — CO-05 RenderCircleStripOverlay
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` L706-888 — CO-05 TryTeachCircleTwoHorizontal
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` L214-330 — CO-05 TryFindCircleByPolarSampling 루프
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — CO-01 RadialDirections 정확성 확인

### Secondary (MEDIUM confidence)
- `.planning/phases/18-carry-over-cleanup/18-CONTEXT.md` — 결정 사항 전체
- `.planning/milestones/v1.0-phases/17-datum-ux-circle-strip-1-test-find-detectedorigin-hover/17-UAT.md` — Phase 17 carry-over 원인 및 Test 10 원문

---

## Metadata

**Confidence breakdown:**
- CO-01 root cause: MEDIUM — PropertyTools 내부 이름 해석 경로는 소스 없이 추론 (A1 가정)
- CO-03, CO-04, CO-06 구현 경로: HIGH — 코드 직접 확인
- CO-05 구현 경로: HIGH — 알고리즘 + 렌더러 루프 모두 확인
- Build risk assessment: HIGH — 변경 범위 모두 격리됨

**Research date:** 2026-05-05
**Valid until:** 2026-06-05 (코드베이스 변경 없으면 유효)
