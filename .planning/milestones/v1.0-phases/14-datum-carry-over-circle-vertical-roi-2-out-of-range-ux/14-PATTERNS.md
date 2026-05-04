# Phase 14: Datum carry-over (Circle 알고리즘 재설계 + Vertical 파라미터 그룹 + ROI 이동 회귀 + 2종 알고리즘 정상화 + out-of-range UX 게이트) — Pattern Map

**Mapped:** 2026-04-26
**Files analyzed:** 6 modified (0 new) — additive only per Phase 13 success criterion 6
**Analogs found:** 6 / 6 (all in-repo; Phase 12/13 확장 양식 그대로 이어받음)

> Sub-phase 분할: 14-01 (Circle resize) / 14-02 (TwoLine angle gate) / 14-03 (Vertical group) / 14-04 (Polar Circle algo) / 14-05 (verify + fix). Plan 1:1 매핑.

---

## File Classification

| 수정 대상 파일 | Sub-phase | Role | Data Flow | Closest Analog | Match Quality |
|----------|-----------|------|-----------|----------------|---------------|
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | 14-01 | UI control (drag state machine + handle render) | event-driven + mouse input | same file `RenderEditHandles` (`:493-510`), `ApplyResizeToTarget` (`:415-458`), `_isResizingRoi` 상태 (`:865-875`, `:983-1010`) | exact (Circle resize 인프라 이미 존재 — wiring 만 누락) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | 14-01, 14-03 | UI code-behind (event routing) | event-driven | same file `HalconViewer_RoiGeometryChanged` (`:447-483`), `HandleDatumRoiMove` (`:567-577`), `ApplyDatumRoiDelta` (`:579-597`), `ClearDatumRoiFields` (`:599-630`), `PublishDatumRoiCandidates` (`:697-728`) | exact (Datum/FAI 분기 패턴 정착 — Vertical RoiId + Geometry handler Datum 분기 추가) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | 14-02, 14-03, 14-05 | Algorithm service | request-response (pure function) | same file `ValidateHorizontalVerticalAngles` (`:639-675`), `TryTeachTwoLineIntersect` (`:179-289`), `TryTeachVerticalTwoHorizontal` (`:468-637`), `TryTeachCircleTwoHorizontal` (`:292-464`), `AppendEdgePointsFromStrip` (`:948-996`) | exact (게이트 helper 호출 패턴 + Line1 → Vertical search/replace) |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | 14-04 | Algorithm service | request-response | same file `TryFindCircle` (`:120-200`) — 시그니처 보존, 옆에 신규 메서드 추가; `TryFitLine` GenMeasureRectangle2 + MeasurePos 패턴 (`:79-99`) | exact (legacy 보존 + polar 변종 신규) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | 14-02, 14-03, 14-04 | Model (PropertyGrid + INI persist) | configuration data | same file Line1 그룹 13 필드 (`:78-109`), `EnsurePerRoiDefaults` (`:326-374`), Volatile HTuple 필드 (`:298-321`) | exact (5 ROI 그룹 패턴 — Vertical 그룹은 Line1 복제) |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | 14-03 | Rendering service | one-shot draw | same file `RenderRawEdgePoints` (`:413-428`), `RenderDatumOverlay` LastTeachSucceeded raw 점 매핑 (`:585-590`) | exact (Vertical raw 점 색상 매핑 한 줄 추가) |

**Brace style observed:**
- `MainResultViewerControl.xaml.cs` = Allman
- `MainView.xaml.cs` = K&R
- `DatumFindingService.cs` = Allman
- `VisionAlgorithmService.cs` = Allman
- `DatumConfig.cs` = K&R
- `HalconDisplayService.cs` = Allman

**Style rule:** 파일별 일관성 유지 — 신규 코드는 해당 파일 스타일.

---

## Pattern Assignments

### Sub-phase 14-01: Circle ROI 이동 자동 재티칭 + N/S/E/W resize

**Files touched:** `MainResultViewerControl.xaml.cs`, `MainView.xaml.cs`

#### 1.1 핸들 렌더 (현재 _rois 만 — Datum 후보 미포함)

**Analog (현재 구현):** `MainResultViewerControl.xaml.cs:493-510`
```csharp
//260423 hbk Edit 모드 핸들 렌더링 — 메인 ROI 렌더 뒤에 호출
private void RenderEditHandles()
{
    if (!_isEditMode) return;
    var roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId && r.IsTaught);
    if (roi == null) return;
    var window = ViewerHost.HalconWindow;
    window.SetColor("cyan");
    window.SetLineWidth(2);
    foreach (var h in GetEditHandles(roi))
    {
        window.DispRectangle1(
            h.Pos.Y - HandleHalfSizeImage,
            h.Pos.X - HandleHalfSizeImage,
            h.Pos.Y + HandleHalfSizeImage,
            h.Pos.X + HandleHalfSizeImage);
    }
}
```

**Phase 14-01 변경 지침 (D-01):** `_isEditMode` 게이트 + `_rois` lookup 가 이미 이 함수의 본문. 두 가지 분기:
1. **Datum Circle 핸들도 보이게** — `_isEditMode == false` 이라도 `_datumRoiCandidates.Count > 0` 이면 `_datumRoiCandidates` 의 Circle ROI 도 핸들 렌더. early-return 가드 완화 + lookup fallback (현재 `_selectedRoiId` 전제 → Datum 은 `_selectedRoiId` 가 publish 시 set 안 됨, `MainResultViewerControl.xaml.cs:785-788` 참조 — Datum hit 시점에만 set).
2. **시각:** 사용자 명시 "동서남북 작은 사각형". 기존 `DispRectangle1` 6×6~10×10 그대로 사용 가능 (`HandleHalfSizeImage = 10.0`, `:134`). 색은 `"cyan"` 유지 또는 Datum 전용 `"yellow"` (Datum overlay 와 시각 일관). **D-01 의 "작은 사각형" 충족** — 추가 모양 변경 불필요.

**호출 측:** `Render()` 메서드 어딘가에서 `RenderEditHandles()` 호출 중 — 신규 함수로 분리해도 되고 현 함수 본문 확장도 됨.

#### 1.2 Resize 드래그 상태 머신 (이미 존재 — Datum 만 통과 안 함)

**Analog (현재 구현):** `MainResultViewerControl.xaml.cs:760-779` — Edit 모드에서 핸들 hit-test 시작 패턴
```csharp
// Edit 모드 전용 (FAI 리사이즈 핸들) — Datum 은 리사이즈 미지원
if (_isEditMode)
{
    var selectedRoi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId && r.IsTaught);
    if (selectedRoi != null)
    {
        var handleHit = HitTestEditHandle(selectedRoi, mouseState.ImagePoint);
        if (handleHit.Handle != ResizeHandle.None)
        {
            _isResizingRoi = true;
            _resizingHandle = handleHit.Handle;
            _resizingPolygonIndex = handleHit.PolyIndex;
            _resizingRoiSnapshot = selectedRoi.Clone();
            SetPanCursor(Cursors.SizeAll);
            PublishPointerInfo();
            return;
        }
    }
}
```

**기존 코멘트 명시:** `// Datum 은 리사이즈 미지원` — Phase 14-01 이 이 주석을 제거하고 Datum 분기 추가 대상.

**Phase 14-01 변경 지침:**
- 위 블록을 일반화: `if (_isEditMode || datumCandidatesPresent)` 가드 안에서 `selectedRoi` 결정 시 `_rois` → `_datumRoiCandidates` fallback. `:758` 의 `bool datumCandidatesPresent` 변수가 이미 선언되어 있어 그대로 사용 가능.
- `HitTestEditHandle(selectedRoi, …)` 는 Circle 의 경우 `GetEditHandles` (`:365-372`) 의 4 핸들을 반환 — **Circle resize 인프라는 이미 100% 동작**. wiring 만 누락.

**ApplyResizeToTarget Circle 분기 (이미 동작, 변경 불필요):** `:420-427`
```csharp
if (_resizingHandle == ResizeHandle.CircleRadius)
{
    double dx = imagePoint.X - snap.CenterCol;
    double dy = imagePoint.Y - snap.CenterRow;
    double newRadius = Math.Sqrt(dx * dx + dy * dy);
    target.Radius = Math.Max(1.0, newRadius);
    return;
}
```

**MouseMove 단계도 동작 (변경 불필요):** `:864-875` (resizing branch — `_rois` lookup 만 있으나 `_datumRoiCandidates` fallback 추가 필요)

#### 1.3 MouseUp → RoiGeometryChanged → MainView Datum 분기 (D-04 핵심)

**Analog (현재 RoiGeometryChanged FAI 분기):** `MainView.xaml.cs:447-483`
```csharp
//260423 hbk ROI 기하 변경(리사이즈/정점편집) → FAI 모델 좌표/크기 반영
private void HalconViewer_RoiGeometryChanged(object sender, RoiGeometryChangedArgs e) {
    if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

    var fai = FindFAIByName(e.RoiId);
    if (fai == null) return;

    if (e.Shape == RoiShape.Circle) {
        foreach (var m in fai.Measurements) {
            var circle = m as CircleDiameterMeasurement;
            if (circle != null) {
                circle.Circle_Row = e.CenterRow;
                circle.Circle_Col = e.CenterCol;
                circle.Circle_Radius = e.Radius;
                break;
            }
        }
    }
    // ... Polygon/Rect 분기
}
```

**MouseUp resize 완료 시 Args 발행:** `MainResultViewerControl.xaml.cs:982-1010`
```csharp
if (_isResizingRoi && _resizingRoiSnapshot != null)
{
    var target = _rois.FirstOrDefault(r => r.Id == _resizingRoiSnapshot.Id);
    // ... target == null 케이스 처리 (Datum 후보 lookup 추가)
    if (target != null)
    {
        RoiGeometryChanged?.Invoke(this, new RoiGeometryChangedArgs
        {
            RoiId = movedId, Shape = shape,
            CenterRow = target.CenterRow, CenterCol = target.CenterCol, Radius = target.Radius,
            // ...
        });
    }
}
```

**Phase 14-01 D-04 결정:** **단일 이벤트 확장** (RoiGeometryChanged 재사용) 채택. 신규 이벤트 X.

**MainView 신규 코드 지침:**
```csharp
private void HalconViewer_RoiGeometryChanged(object sender, RoiGeometryChangedArgs e) {
    if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

    //260426 hbk Phase 14-01 D-04 — Datum 분기 우선 (FAI lookup 전에)
    DatumConfig datum;
    if (e.RoiId.StartsWith("Datum.") && IsCurrentNodeDatum(out datum)) {
        HandleDatumRoiResize(datum, e);
        return;
    }
    // ... 기존 FAI 경로 untouched
}

//260426 hbk Phase 14-01 D-04 — Datum ROI resize 후처리 (Move 와 동일 흐름: write-back + 자동 재티칭 + publish)
private void HandleDatumRoiResize(DatumConfig datum, RoiGeometryChangedArgs e) {
    if (e.RoiId == "Datum.Circle" && e.Shape == RoiShape.Circle) {
        datum.CircleROI_Row = e.CenterRow;
        datum.CircleROI_Col = e.CenterCol;
        datum.CircleROI_Radius = e.Radius;
    }
    // (Rect 핸들도 향후 — Phase 14 scope 외, deferred)
    try { datum.RaisePropertyChanged(string.Empty); } catch { }
    mParentWindow?.inspectionList?.RefreshParamEditor();
    halconViewer.SetDatumOverlay(datum, true);
    InvokeTryTeachDatumForEdit(datum);
    PublishDatumRoiCandidates(datum);
    UpdateDatumRefCoordsLabel(datum);
}
```

**중요:** `HandleDatumRoiMove` (`:567-577`) 의 5-step 패턴 (write-back → RaisePropertyChanged → RefreshParamEditor → SetDatumOverlay → InvokeTryTeachDatumForEdit → PublishDatumRoiCandidates → UpdateDatumRefCoordsLabel) 그대로 복사. Move 와 Resize 의 유일한 차이는 **delta vs absolute** — Resize 는 `e.CenterRow/Col/Radius` 절대 좌표 직접 대입.

#### 1.4 Move 자동 재티칭 wiring fix (Test 1 issue 1)

**현재 동작 분석:** `HandleDatumRoiMove` (`:567-577`) 가 이미 `InvokeTryTeachDatumForEdit(datum)` 호출 중. Phase 13-07 `NotifyDatumParamMaybeChanged` (`:638-643`) 가드도 `IsConfigured` 만 — 정상 동작 조건 충족.

**Carry-over 원인 추정:** `_editingDatum` vs `SelectedParam` 불일치 + `IsCurrentNodeDatum` 가드 (`:562-565`) 가 `mParentWindow?.inspectionList?.SelectedParam` 만 확인 — _editingDatum 진입 후 publishing 이 비동기로 안 된 케이스 가능.

**Phase 14-01 변경 지침:**
- 1차 진단: `HalconViewer_RoiMoveCompleted` (`:528-559`) 로깅 추가 — `Logging.PrintLog((int)ELogType.Trace, "Datum.Circle move: dr=" + e.DeltaRow + " dc=" + e.DeltaCol + " IsConfigured=" + datum.IsConfigured)`.
- 2차 진단: `InvokeTryTeachDatumForEdit` (`:646-664`) 진입/종료 로깅. 실패 시 `error` 가 표시되는지 확인.
- Fix 후보: `Dispatcher.BeginInvoke(DispatcherPriority.Background, ...)` 로 `InvokeTryTeachDatumForEdit` 감싸기 (Phase 13-07 Fix A 패턴 — CONTEXT D-04 명시 권장).

**SPEC AC:** "Circle ROI 드래그 이동 → 검출 원/raw 점 새 위치 반영" — 1.4 진단 + fix 완료 시 PASS.

---

### Sub-phase 14-02: TwoLineIntersect 각도 out-of-range 게이트

**Files touched:** `DatumConfig.cs`, `DatumFindingService.cs`

#### 2.1 DatumConfig 신규 PropertyGrid 필드

**Analog:** `DatumConfig.cs:32-38` (AlgorithmType — Algorithm Category) + `DatumConfig.cs:246-250` (legacy Edge field default value 패턴)
```csharp
//260423 hbk Phase 12 D-09 — Datum 알고리즘 선택자
[Category("Datum|Algorithm")]
[ItemsSourceProperty(nameof(AlgorithmTypeList))]
public string AlgorithmType { get; set; } = "TwoLineIntersect";
```

**Phase 14-02 신규 필드 (D-08, AC):**
```csharp
//260426 hbk Phase 14-02 — TwoLineIntersect 두 라인 직각성 게이트 임계각 (도)
//  0  = 게이트 off (어떤 각도여도 PASS)
//  10 = default (90°±10° 허용)
//  range hint: 0~45°. INI 미존재 시 default 10° (값이 0 이면 명시 off 의도로 간주).
[Category("Datum|Algorithm")]
public double TwoLineAngleToleranceDeg { get; set; } = 10.0;
```

**INI 하위호환:** `ParamBase` reflection 이 자동 직렬화 — 기존 INI 에 키가 없으면 default 10° 채택, 회귀 0.

#### 2.2 DatumFindingService 게이트 호출

**Analog:** `DatumFindingService.cs:639-675` (`ValidateHorizontalVerticalAngles` static helper) + `:441-449` / `:614-622` 호출 사례
```csharp
//260424 hbk Phase 13 D-09..D-12 — Req 5d 방향 정합성 검증 (CircleTwoHorizontal: 수직 가상선 phi = PI/2 고정)
double vertPhiCircle = Math.PI / 2.0;
string angleError;
if (!ValidateHorizontalVerticalAngles(config.RefAngleRad, vertPhiCircle, out angleError))
{
    config.LastTeachSucceeded = false;
    error = angleError;
    return false;
}
return true;
```

**Phase 14-02 신규 코드 지침** — `TryTeachTwoLineIntersect` 끝부분 (`:267-281` `LastTeachSucceeded = true` 직전):
```csharp
//260426 hbk Phase 14-02 — Req 2 두 라인 각도 게이트 (TwoLineIntersect 한정, 기존 ValidateHorizontalVerticalAngles 와 별개 게이트)
//  사용자 임계각 N=TwoLineAngleToleranceDeg, 0 이면 게이트 off (PASS).
//  측정값 = |line1 phi - line2 phi| 를 [0,180) 정규화 후 90° 와의 편차.
if (config.TwoLineAngleToleranceDeg > 0.0)
{
    double phi1 = Math.Atan2(line1RowEnd - line1RowBegin, line1ColEnd - line1ColBegin);
    double phi2 = Math.Atan2(line2RowEnd - line2RowBegin, line2ColEnd - line2ColBegin);
    double deltaDeg = Math.Abs((phi1 - phi2) * 180.0 / Math.PI);
    while (deltaDeg >= 180.0) deltaDeg -= 180.0;
    double perpErr = Math.Abs(deltaDeg - 90.0);
    if (perpErr > config.TwoLineAngleToleranceDeg)
    {
        config.LastTeachSucceeded = false;
        error = "Two-line angle out of range: "
              + deltaDeg.ToString("F1")
              + " deg (expected 90 +/- "
              + config.TwoLineAngleToleranceDeg.ToString("F1")
              + " deg)"; //260426 hbk Phase 14-02 SPEC AC literal (Req 2)
        return false;
    }
}
//260423 hbk Phase 11 D-11 — 검출 라인 좌표 휘발성 저장 (오버레이용)
// ... (기존 저장 로직)
```

**위치 주의:** `LastTeachSucceeded = true` (`:279`) 와 `Line1Detected_*` 저장 (`:271-278`) **앞**에 배치. 게이트 실패 시 `LastTeachSucceeded=false` + early return → 오버레이 미저장 (이전 성공 잔류 가능 — D-13 설계는 fail 시 잔류 허용).

**SPEC AC literal 정합:** SPEC `"Two-line angle out of range: <측정값>° (expected 90°±<N>°)"`. 위 코드는 ASCII (deg, +/-) — Phase 13 helper 와 동일 컨벤션 (`DatumFindingService.cs:651-655`).

---

### Sub-phase 14-03: Vertical 에지 파라미터 그룹 신설

**Files touched:** `DatumConfig.cs`, `DatumFindingService.cs`, `MainView.xaml.cs`, `HalconDisplayService.cs`

#### 3.1 DatumConfig — Vertical 그룹 13 필드

**Analog (Line1 그룹 13 필드):** `DatumConfig.cs:78-109`
```csharp
//260409 hbk Phase 4: Line1 ROI (기준 X축 방향 에지 라인)
[Category("Datum|Line1 ROI")]
public double Line1_Row { get; set; } = 0;
public double Line1_Col { get; set; } = 0;
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1_Phi { get; set; } = 0;
public double Line1_PhiDeg { get { return Line1_Phi * 180.0 / System.Math.PI; } set { Line1_Phi = value * System.Math.PI / 180.0; } }
public double Line1_Length1 { get; set; } = 0;
public double Line1_Length2 { get; set; } = 0;

//260425 hbk Phase 13 D-PRP-02 — Line1 ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
[Category("Datum|Line1 Edge")]
public int    Line1_EdgeThreshold   { get; set; } = 0;
public double Line1_Sigma           { get; set; } = 0;
[ItemsSourceProperty(nameof(Line1_EdgeDirectionList))]
public string Line1_EdgeDirection   { get; set; } = "";
public int    Line1_EdgeSampleCount { get; set; } = 0;
public int    Line1_EdgeTrimCount   { get; set; } = 0;
[ItemsSourceProperty(nameof(Line1_EdgePolarity))]
public string Line1_EdgePolarity    { get; set; } = "";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> Line1_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> Line1_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
```

**Volatile HTuple raw 점 패턴:** `DatumConfig.cs:298-321`
```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public HTuple Line1_DetectedEdgeRows { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public HTuple Line1_DetectedEdgeCols { get; set; }
```

**Phase 14-03 신규 13 필드** (Line1 블록 완전 복제, prefix `Vertical_` 로 교체):
```csharp
//260426 hbk Phase 14-03 — Vertical ROI (VerticalTwoHorizontal 전용 수직 에지 라인 — 의미상 별도 그룹)
[Category("Datum|Vertical ROI")]
public double Vertical_Row { get; set; } = 0;
public double Vertical_Col { get; set; } = 0;
[PropertyTools.DataAnnotations.Browsable(false)]
public double Vertical_Phi { get; set; } = 0;
public double Vertical_PhiDeg
{
    get { return Vertical_Phi * 180.0 / System.Math.PI; }
    set { Vertical_Phi = value * System.Math.PI / 180.0; }
}
public double Vertical_Length1 { get; set; } = 0;
public double Vertical_Length2 { get; set; } = 0;

//260426 hbk Phase 14-03 — Vertical ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
[Category("Datum|Vertical Edge")]
public int    Vertical_EdgeThreshold   { get; set; } = 0;
public double Vertical_Sigma           { get; set; } = 0;
[ItemsSourceProperty(nameof(Vertical_EdgeDirectionList))]
public string Vertical_EdgeDirection   { get; set; } = "";
public int    Vertical_EdgeSampleCount { get; set; } = 0;
public int    Vertical_EdgeTrimCount   { get; set; } = 0;
[ItemsSourceProperty(nameof(Vertical_EdgePolarityList))]
public string Vertical_EdgePolarity    { get; set; } = "";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> Vertical_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> Vertical_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }

//260426 hbk Phase 14-03 — raw 검출 에지점 (volatile, INI 영향 0)
[PropertyTools.DataAnnotations.Browsable(false)]
public HTuple Vertical_DetectedEdgeRows { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public HTuple Vertical_DetectedEdgeCols { get; set; }
```

**총 13 필드:** 5 geometry (Row/Col/Phi/Length1/Length2) + 6 edge (Threshold/Sigma/Direction/SampleCount/TrimCount/Polarity) + 2 raw (Rows/Cols). **PhiDeg wrapper 는 별도 카운트 안 함** (Phi 의 view).

#### 3.2 EnsurePerRoiDefaults — Vertical 마이그레이션 (D-05, D-06)

**Analog:** `DatumConfig.cs:326-374` — sentinel(==0/"") 기반 idempotent 5-ROI 패턴
```csharp
public void EnsurePerRoiDefaults() {
    int    fbThreshold   = EdgeThreshold > 0 ? EdgeThreshold : 20;
    double fbSigma       = Sigma > 0 ? Sigma : 1.0;
    string fbDirection   = "LtoR";
    int    fbSampleCount = 20;
    int    fbTrimCount   = 10;
    string fbPolarity    = !string.IsNullOrEmpty(EdgePolarity) ? EdgePolarity : "all";

    // Line1
    if (Line1_EdgeThreshold == 0)        Line1_EdgeThreshold = fbThreshold;
    // ...
}
```

**Phase 14-03 신규 분기 (D-05 양쪽 채움 정책):**
```csharp
// Vertical — Line1_* 우선, sentinel 이면 글로벌 fallback (양쪽 다 채움 정책 D-05/D-06)
//260426 hbk Phase 14-03 — VerticalTwoHorizontal 알고리즘에서 Line1_* 슬롯 재사용했던 기존 INI 호환:
//  Vertical_* 가 sentinel 이고 Line1_* 가 의미값이면 1회 복사. Line1_* 는 zero-out 안 함 (회귀 위험 0).
if (Vertical_EdgeThreshold == 0)   Vertical_EdgeThreshold   = (Line1_EdgeThreshold > 0)    ? Line1_EdgeThreshold    : fbThreshold;
if (Vertical_Sigma == 0)           Vertical_Sigma           = (Line1_Sigma > 0)            ? Line1_Sigma            : fbSigma;
if (string.IsNullOrEmpty(Vertical_EdgeDirection))   Vertical_EdgeDirection   = !string.IsNullOrEmpty(Line1_EdgeDirection)   ? Line1_EdgeDirection   : fbDirection;
if (Vertical_EdgeSampleCount == 0) Vertical_EdgeSampleCount = (Line1_EdgeSampleCount > 0)  ? Line1_EdgeSampleCount  : fbSampleCount;
if (Vertical_EdgeTrimCount == 0)   Vertical_EdgeTrimCount   = (Line1_EdgeTrimCount > 0)    ? Line1_EdgeTrimCount    : fbTrimCount;
if (string.IsNullOrEmpty(Vertical_EdgePolarity))    Vertical_EdgePolarity    = !string.IsNullOrEmpty(Line1_EdgePolarity)    ? Line1_EdgePolarity    : fbPolarity;

// Geometry — Line1 의 위치도 1회 복사 (사용자가 Vertical 그룹만 보고 알고리즘 운용 가능하도록)
//   Vertical_Length1/Length2==0 이 sentinel
if (Vertical_Length1 == 0 && Line1_Length1 > 0) {
    Vertical_Row     = Line1_Row;
    Vertical_Col     = Line1_Col;
    Vertical_Phi     = Line1_Phi;
    Vertical_Length1 = Line1_Length1;
    Vertical_Length2 = Line1_Length2;
}
```

**Idempotency:** 두 번째 호출 시 Vertical_* 가 모두 의미값 → 분기 미진입. PASS.

#### 3.3 TryTeachVerticalTwoHorizontal — Line1_* → Vertical_* 슬롯 교체

**Analog (현재 `Line1_*` 재사용):** `DatumFindingService.cs:486-503`
```csharp
//260423 hbk Phase 12 D-07 — 수직 ROI 라인 피팅 (Line1_* 재사용)
if (!TryFindLine(
        image, imageWidth, imageHeight,
        config.Line1_Row, config.Line1_Col, config.Line1_Phi,
        config.Line1_Length1, config.Line1_Length2,
        config.Line1_Sigma, config.Line1_EdgeThreshold, config.Line1_EdgePolarity,
        config.Line1_EdgeDirection, config.Line1_EdgeSampleCount, config.Line1_EdgeTrimCount,
        out vrB, out vcB, out vrE, out vcE,
        out vertRawRows, out vertRawCols,
        out lineError,
        "Line1(vertical)"))
{ ... }
//260425 hbk Phase 13 D-VIZ-03 — 수직 라인 raw 점 DatumConfig 의 Line1_DetectedEdge* 에 기록 (Line1_* 슬롯 재사용)
config.Line1_DetectedEdgeRows = vertRawRows;
config.Line1_DetectedEdgeCols = vertRawCols;
```

**Phase 14-03 변경 (단순 search/replace `Line1_*` → `Vertical_*`):**
```csharp
//260426 hbk Phase 14-03 — Vertical_* 슬롯 (의미적 분리, Line1_* 재사용 종료)
if (!TryFindLine(
        image, imageWidth, imageHeight,
        config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
        config.Vertical_Length1, config.Vertical_Length2,
        config.Vertical_Sigma, config.Vertical_EdgeThreshold, config.Vertical_EdgePolarity,
        config.Vertical_EdgeDirection, config.Vertical_EdgeSampleCount, config.Vertical_EdgeTrimCount,
        out vrB, out vcB, out vrE, out vcE,
        out vertRawRows, out vertRawCols,
        out lineError,
        "Vertical")) //260426 hbk Phase 14-03 — 진단 로그 레이블 변경
{ ... }
//260426 hbk Phase 14-03 — 수직 라인 raw 점 Vertical_DetectedEdge* 에 기록
config.Vertical_DetectedEdgeRows = vertRawRows;
config.Vertical_DetectedEdgeCols = vertRawCols;
```

**Line1Detected_* 오버레이 필드 보존:** `:604-607` 의 `config.Line1Detected_RBegin = vrB; ...` 부분은 그대로 유지 (D-05 양쪽 채움 — Line1Detected_* 는 RenderDatumOverlay 의 검출 라인 외삽 그리기에 사용되며 Phase 14 scope 외).

#### 3.4 MainView — Datum.Vertical 분기 추가 (4 곳)

**Analog (PublishDatumRoiCandidates 알고리즘 분기):** `MainView.xaml.cs:719-726`
```csharp
case EDatumAlgorithm.VerticalTwoHorizontal:
    if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
        list.Add(BuildDatumRectCandidate("Datum.Line1", datum.Line1_Row, datum.Line1_Col, datum.Line1_Length1, datum.Line1_Length2));
    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", ...));
    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", ...));
    break;
```

**Phase 14-03 변경 — RoiId `"Datum.Line1"` → `"Datum.Vertical"`:**
```csharp
case EDatumAlgorithm.VerticalTwoHorizontal:
    //260426 hbk Phase 14-03 — Line1_* → Vertical_* 슬롯 교체 (RoiId 도 Datum.Vertical 로)
    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0)
        list.Add(BuildDatumRectCandidate("Datum.Vertical", datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Length1, datum.Vertical_Length2));
    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", ...));
    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", ...));
    break;
```

**ApplyDatumRoiDelta 분기:** `MainView.xaml.cs:579-597` — switch 케이스 1줄 추가
```csharp
case "Datum.Vertical":
    datum.Vertical_Row += e.DeltaRow; datum.Vertical_Col += e.DeltaCol; break;
```

**ClearDatumRoiFields 분기:** `MainView.xaml.cs:601-630` — switch 케이스 1블록 추가
```csharp
case "Datum.Vertical":
    datum.Vertical_Row = 0; datum.Vertical_Col = 0; datum.Vertical_Phi = 0;
    datum.Vertical_Length1 = 0; datum.Vertical_Length2 = 0;
    break;
```

**Datum 티칭 진입 step (별도 분기):** `StartDatumTeachStep` 같은 기존 함수에서 VerticalTwoHorizontal 의 첫 ROI 가 `Datum.Line1` 로 설정되어 있을 수 있음 — `MainView.xaml.cs:1431` `BtnTestFindDatum_Click` 인근 `_datumTeachStep`/`GetFirstStep` 로직 점검 후 Vertical 명명 변경 (planner 가 grep 으로 마지막 4번째 분기 위치 식별).

#### 3.5 HalconDisplayService — Vertical raw 점 색상 매핑

**Analog:** `HalconDisplayService.cs:585-590`
```csharp
//260425 hbk Phase 13 D-VIZ-05 — 5 ROI raw 검출 에지점 (있을 때만) — ROI 별 색상 구분
RenderRawEdgePoints(window, datum.Line1_DetectedEdgeRows,        datum.Line1_DetectedEdgeCols,        "cyan");
RenderRawEdgePoints(window, datum.Line2_DetectedEdgeRows,        datum.Line2_DetectedEdgeCols,        "magenta");
RenderRawEdgePoints(window, datum.Circle_DetectedEdgeRows,       datum.Circle_DetectedEdgeCols,       "yellow");
RenderRawEdgePoints(window, datum.Horizontal_A_DetectedEdgeRows, datum.Horizontal_A_DetectedEdgeCols, "green");
RenderRawEdgePoints(window, datum.Horizontal_B_DetectedEdgeRows, datum.Horizontal_B_DetectedEdgeCols, "lime green");
```

**Phase 14-03 신규 한 줄:**
```csharp
//260426 hbk Phase 14-03 — Vertical 그룹 raw 점 (Line1 cyan 과 시각 구분: orange 신규)
RenderRawEdgePoints(window, datum.Vertical_DetectedEdgeRows,     datum.Vertical_DetectedEdgeCols,     "orange");
```

**색상 선택:** `cyan/magenta/yellow/green/lime green` 모두 사용 중 — `orange` 가 미사용이고 Phase 13 D-07 TryFind 십자와 동일 색이지만 raw 점은 line width 1 cross 6px 이라 시각 충돌 적음. CONTEXT specifics 의 "cyan 재사용 또는 신규 색" 두 옵션 중 신규.

#### 3.6 PropertyGrid 알고리즘별 가시성 (D-07, D-08)

**Analog:** PropertyTools 3.1.0 의 `[PropertyTools.DataAnnotations.Browsable(false)]` attribute (`DatumConfig.cs:41,82,106,...`) — **정적** attribute. 런타임 토글 미직접 지원.

**Phase 14-03 D-08 1차 시도:** AlgorithmType 변경 시 `RaisePropertyChanged(string.Empty)` (`MainView.xaml.cs:570`, `:495` 패턴) + InspectionListView `RefreshParamEditor()` (`InspectionListView.xaml.cs:28`) 로 PropertyGrid 재바인딩. attribute 자체는 토글 X — fallback **wrapper view-model** 또는 **prefix 시각 구분** 재검토.

**Plan-phase researcher 액션 아이템 (D-08):**
1. PropertyTools 3.1.0 documentation 에서 `IDataErrorInfo` / `ICustomTypeDescriptor` 지원 여부 확인 — 지원 시 wrapper 채택.
2. 미지원 시 fallback: Category 이름에 prefix `[VTH]` / `[CTH]` / `[TLI]` 추가 — `[Category("Datum|[VTH] Vertical ROI")]` 식. INI 영향 0 (Category 는 PropertyGrid 표시용만).
3. 추가 fallback (사용자 승인 필요): CONTEXT deferred 에 명시.

#### 3.7 Phase 13-06+13-07 자동 재티칭 wiring 검증 (Vertical_* 신규 필드)

**Analog:** `MainView.xaml.cs:638-643` (`NotifyDatumParamMaybeChanged`)
```csharp
public void NotifyDatumParamMaybeChanged(DatumConfig datum) {
    if (datum == null) return;
    if (!datum.IsConfigured) return; //260426 hbk Phase 13-07 — LastTeachSucceeded 게이트 제거
    if (halconViewer == null || halconViewer.CurrentImage == null) return;
    InvokeTryTeachDatumForEdit(datum);
}
```

**검증:** 위 helper 는 `datum` 객체 단위로 동작 — 신규 `Vertical_EdgeThreshold` 변경도 `RaisePropertyChanged` 발생 시 자동 라우팅. **추가 코드 변경 없음** — Phase 14-03 acceptance criteria "Vertical_EdgeThreshold 변경 → 자동 재티칭" PASS 보장.

---

### Sub-phase 14-04: Circle polar-sampling 신규 알고리즘

**Files touched:** `VisionAlgorithmService.cs`, `DatumConfig.cs`

#### 4.1 DatumConfig — Circle polar 파라미터 3개

**Analog:** `DatumConfig.cs:144-149` (CircleROI_*) + `:152-160` (Circle_Edge*)
```csharp
//260423 hbk Phase 12 D-10 — Circle ROI (CircleTwoHorizontal 전용 검색 영역)
[Category("Datum|Circle ROI")]
public double CircleROI_Row    { get; set; } = 0;
public double CircleROI_Col    { get; set; } = 0;
public double CircleROI_Radius { get; set; } = 0;
```

**Phase 14-04 신규 3 필드** (Circle ROI 카테고리 안에 묶음):
```csharp
//260426 hbk Phase 14-04 — Circle polar-sampling 알고리즘 파라미터 (TryFindCircleByPolarSampling 전용)
[Category("Datum|Circle ROI")]
public double Circle_PolarStepDeg  { get; set; } = 10.0;  // 1~30°, 360/step = 점 개수
public double Circle_RectL1Ratio   { get; set; } = 0.05;  // 사각형 ROI 의 length1 = radius × ratio
public double Circle_RectL2Ratio   { get; set; } = 0.05;  // 사각형 ROI 의 length2 = radius × ratio
```

**INI 하위호환:** 키 미존재 시 default 10°/0.05/0.05 — `EnsurePerRoiDefaults` 마이그레이션 불필요 (default value 가 그대로 의미값). 사용자 0 입력 시:
- `Circle_PolarStepDeg <= 0` → `TryFindCircleByPolarSampling` 내부에서 sanity clamp 10° 로 fallback (TryFindLine `:710-715` 패턴과 동일).
- `Circle_RectL1/L2Ratio <= 0` → 0.05 로 clamp.

#### 4.2 VisionAlgorithmService.TryFindCircleByPolarSampling 신규 메서드

**Analog 1 (legacy TryFindCircle 시그니처):** `VisionAlgorithmService.cs:120-200` — 시그니처 보존, 옆에 신규 메서드 추가 (additive only)

**Analog 2 (GenMeasureRectangle2 + MeasurePos):** `VisionAlgorithmService.cs:79-99` — 파이프라인 패턴
```csharp
HOperatorSet.GenMeasureRectangle2(
    rRow, rCol, rPhi, roiLength1, roiLength2,
    imageWidth, imageHeight, "nearest_neighbor",
    out measureHandle);

HTuple rows, cols, amp, dist;
HOperatorSet.MeasurePos(image, measureHandle,
    Math.Max(0.4, sigma), Math.Max(1, threshold),
    pol, "all", out rows, out cols, out amp, out dist);

int edgeCount = rows.TupleLength();
if (edgeCount < 2) { error = "insufficient edge points"; return false; }

HOperatorSet.GenContourPolygonXld(out contour, rows, cols);
```

**Analog 3 (DatumFindingService.AppendEdgePointsFromStrip — strip-loop 누적 패턴):** `DatumFindingService.cs:948-996` — 한 사각형에서 MeasurePos 후 누적
```csharp
HOperatorSet.GenMeasureRectangle2(rr, rc, rp, rh, rw, imageWidth, imageHeight, "nearest_neighbor", out measureHandle);
HTuple edgeRows, edgeCols, amp, dist;
HOperatorSet.MeasurePos(image, measureHandle, sigma, threshold, polarity, "all", out edgeRows, out edgeCols, out amp, out dist);
if (edgeRows.TupleLength() <= 0 || edgeCols.TupleLength() <= 0) return;
HOperatorSet.TupleConcat(allRows, edgeRows, out allRows);
HOperatorSet.TupleConcat(allCols, edgeCols, out allCols);
```

**Analog 4 (FitCircleContourXld 호출):** `VisionAlgorithmService.cs:174-186`
```csharp
HOperatorSet.FitCircleContourXld(edges, "atukey", -1, 2, 0, 5, 2,
    out row, out column, out rad, out startPhi, out endPhi, out pointOrder);
if (row.Length == 0) { error = "no circle fitted"; return false; }
foundRow = row[0].D;
foundCol = column[0].D;
foundRadius = rad[0].D;
```

**External reference (DatumMeasure 외부 프로젝트):** `C:\Info\Project\DatumMeasure\DatumMeasure\Algorithms\MeasurementAlgorithm.cs:270-324` — strip-loop 패턴 원본. polar-sampling 변종 미발견 — Phase 14-04 는 **신규 회전 로직** + **기존 사각형 MeasurePos 재사용** 조합이 신규.

**Phase 14-04 신규 메서드 시그니처 (D-12, D-13 좌표계):**
```csharp
/// <summary>
/// 360° polar sampling 방식의 Circle 검출.
/// center+radius 기점에서 stepDeg 간격으로 회전하며, 각 각도 θ 에서 작은 사각형 ROI 의 MeasurePos 첫 에지점 1개 추출 → 누적 → FitCircleContourXld.
/// </summary>
//260426 hbk Phase 14-04 — D-12/D-13 좌표계: 화면 시점 CCW (0°=right, 90°=up, 180°=left, 270°=down).
//   rect 중심 row = centerRow - radius * sin(theta_rad)  (sin 앞 minus: 화면 위쪽 = row 감소)
//   rect 중심 col = centerCol + radius * cos(theta_rad)
//   rect phi = theta_rad (반경 방향 = rect length1 축; Halcon Rectangle2 phi 는 col+ 기준 — sin 부호로 보정)
//   주의: Halcon Rectangle2 phi 정의 SDK 문서 재확인 필수 (D-13 caveat).
public bool TryFindCircleByPolarSampling(
    HImage image,
    double centerRow, double centerCol, double radius,
    double stepDeg, double rectL1Ratio, double rectL2Ratio,
    double sigma, int threshold, string polarity,
    HTuple datumTransform,
    out double foundRow, out double foundCol, out double foundRadius,
    out HTuple edgeRows, out HTuple edgeCols,
    out string error)
{
    foundRow = foundCol = foundRadius = 0;
    edgeRows = new HTuple();
    edgeCols = new HTuple();
    error = null;

    if (image == null) { error = "image is null"; return false; }

    // Sanity clamp (TryFindLine `:710-715` 패턴 — sentinel/0 방어)
    if (stepDeg <= 0) stepDeg = 10.0;
    if (stepDeg > 30) stepDeg = 30.0;
    if (rectL1Ratio <= 0) rectL1Ratio = 0.05;
    if (rectL2Ratio <= 0) rectL2Ratio = 0.05;
    if (sigma < 0.4) sigma = 1.0;
    if (threshold <= 0) threshold = 20;
    if (string.IsNullOrEmpty(polarity)) polarity = "all";

    // Datum transform (legacy TryFindCircle `:142-153` 패턴)
    double cRow = centerRow, cCol = centerCol;
    if (datumTransform != null && datumTransform.Length > 0)
    {
        try
        {
            HTuple tRow, tCol;
            HOperatorSet.AffineTransPoint2d(datumTransform, centerRow, centerCol, out tRow, out tCol);
            cRow = tRow.D;
            cCol = tCol.D;
        }
        catch { }
    }

    HObject contour = null;
    try
    {
        HTuple imageWidth, imageHeight;
        image.GetImageSize(out imageWidth, out imageHeight);

        double halfL1 = radius * rectL1Ratio;
        double halfL2 = radius * rectL2Ratio;
        if (halfL1 < 1.0) halfL1 = 1.0;
        if (halfL2 < 1.0) halfL2 = 1.0;

        HTuple allRows = new HTuple();
        HTuple allCols = new HTuple();

        int stepCount = (int)Math.Round(360.0 / stepDeg);
        for (int i = 0; i < stepCount; i++)
        {
            double thetaDeg = i * stepDeg;
            double thetaRad = thetaDeg * Math.PI / 180.0;
            //260426 hbk Phase 14-04 D-13 — 화면 CCW 좌표계 (sin 앞 minus)
            double rectRow = cRow - radius * Math.Sin(thetaRad);
            double rectCol = cCol + radius * Math.Cos(thetaRad);
            double rectPhi = thetaRad; // 반경 방향 = rect length1 축

            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenMeasureRectangle2(
                    rectRow, rectCol, rectPhi, halfL1, halfL2,
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);

                HTuple eRows, eCols, amp, dist;
                HOperatorSet.MeasurePos(image, measureHandle,
                    sigma, threshold, polarity, "all",
                    out eRows, out eCols, out amp, out dist);

                if (eRows.TupleLength() > 0 && eCols.TupleLength() > 0)
                {
                    // 첫 에지점 1개만 누적 (D-EXT specifics)
                    HOperatorSet.TupleConcat(allRows, eRows[0], out allRows);
                    HOperatorSet.TupleConcat(allCols, eCols[0], out allCols);
                }
            }
            catch
            {
                // per-step 실패 swallow — 나머지 step 계속 (AppendEdgePointsFromStrip `:986-989` 관습)
            }
            finally
            {
                if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } }
            }
        }

        edgeRows = allRows;
        edgeCols = allCols;

        if (allRows.TupleLength() < 3)
        {
            error = "insufficient polar samples (" + allRows.TupleLength() + ")";
            return false;
        }

        // FitCircleContourXld (legacy TryFindCircle `:174-186` 패턴)
        HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

        HTuple row, column, rad, startPhi, endPhi, pointOrder;
        HOperatorSet.FitCircleContourXld(contour, "atukey", -1, 2, 0, 5, 2,
            out row, out column, out rad, out startPhi, out endPhi, out pointOrder);

        if (row.Length == 0) { error = "no circle fitted"; return false; }

        foundRow = row[0].D;
        foundCol = column[0].D;
        foundRadius = rad[0].D;
        return true;
    }
    catch (Exception ex)
    {
        error = ex.Message;
        return false;
    }
    finally
    {
        if (contour != null) { try { contour.Dispose(); } catch { } }
    }
}
```

**Caveat (D-13):** Halcon Rectangle2 phi 의 정확한 정의 (CW vs CCW, 라디안 vs 도) 는 plan-phase researcher 가 SDK doc 재확인. 현재 `rectPhi = thetaRad` 가정이 틀리면 sin 부호 또는 +π/2 보정 필요.

**Acceptance impact:**
- AC4: step=10° → 36 점 raw — `360.0 / 10.0 = 36`, 각 step 1점 → 36점 PASS.
- AC4: step=1° → 360 점 — 동일 로직.
- AC4: FitCircleContourXld center 정확도 — `atukey` robust fit + radius·5% 사각형으로 에지 노이즈에 강건.

#### 4.3 Legacy TryFindCircle 보존 (CircleDiameterMeasurement 호환)

**호출처 보존 검증:** `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs:49-56`
```csharp
if (!svc.TryFindCircle(image,
    Circle_Row, Circle_Col, Circle_Radius,
    datumTransform,
    Sigma, EdgeThreshold, EdgePolarity,
    out foundRow, out foundCol, out foundRadius, out error))
{ return false; }
```

**Phase 14-04 변경 없음.** `TryFindCircle` 시그니처 / 본문 untouched. Phase 13 ALG-04 회귀 0 보장.

---

### Sub-phase 14-05: CircleTwoH / VerticalTwoH 정상화 + 결함 fix

**Files touched:** `DatumFindingService.cs` (Circle 검출 호출 교체), 추가 결함 fix (조사 결과 의존)

#### 5.1 TryTeachCircleTwoHorizontal — Circle 검출 교체

**Analog (현재 호출):** `DatumFindingService.cs:306-321`
```csharp
//260423 hbk Phase 12 D-05 — Circle 피팅 (VisionAlgorithmService 재사용)
var visionSvc = new VisionAlgorithmService();
double centerRow, centerCol, radius;
string circleError;
//260425 hbk Phase 13 D-PRP-05 — Circle per-ROI 에지 파라미터 사용
if (!visionSvc.TryFindCircle(
        image,
        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
        null, // teaching-phase identity transform
        config.Circle_Sigma, config.Circle_EdgeThreshold, config.Circle_EdgePolarity,
        out centerRow, out centerCol, out radius,
        out circleError))
{
    config.LastTeachSucceeded = false;
    error = "Circle fit failed: " + circleError;
    return false;
}

config.CircleCenter_Row      = centerRow;
config.CircleCenter_Col      = centerCol;
config.CircleDetected_Radius = radius;
//260425 hbk Phase 13 D-VIZ-03 — Circle raw 점은 VisionAlgorithmService.TryFindCircle 가 미반환 → 빈 HTuple (향후 phase 이월)
config.Circle_DetectedEdgeRows = new HTuple();
config.Circle_DetectedEdgeCols = new HTuple();
```

**Phase 14-05 변경 (D-10 단순 verify 우선):**
```csharp
//260426 hbk Phase 14-05 D-10 — TryFindCircle → TryFindCircleByPolarSampling 교체 (raw 점 반환 + 360° 분포)
var visionSvc = new VisionAlgorithmService();
double centerRow, centerCol, radius;
HTuple circleEdgeRows, circleEdgeCols;
string circleError;
if (!visionSvc.TryFindCircleByPolarSampling(
        image,
        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
        config.Circle_PolarStepDeg, config.Circle_RectL1Ratio, config.Circle_RectL2Ratio,
        config.Circle_Sigma, config.Circle_EdgeThreshold, config.Circle_EdgePolarity,
        null, // teaching-phase identity transform
        out centerRow, out centerCol, out radius,
        out circleEdgeRows, out circleEdgeCols,
        out circleError))
{
    config.LastTeachSucceeded = false;
    error = "Circle fit failed: " + circleError; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5c) 보존
    return false;
}

config.CircleCenter_Row      = centerRow;
config.CircleCenter_Col      = centerCol;
config.CircleDetected_Radius = radius;
//260426 hbk Phase 14-05 — raw 점 직접 반환 — 13-05 D-VIZ-03 carry-over closure
config.Circle_DetectedEdgeRows = circleEdgeRows;
config.Circle_DetectedEdgeCols = circleEdgeCols;
```

**SPEC AC literal `"Circle fit failed: ..."` 보존** — error 포맷 변경 없음 (회귀 0).

#### 5.2 D-10/D-11 PASS path: 단순 verify

**검증 절차 (CONTEXT D-10):**
1. SIMUL_MODE 실행
2. btn_testFindDatum 클릭 (TwoLineIntersect 알고리즘) → AC12 PASS 확인
3. 알고리즘 CircleTwoHorizontal 로 변경 → btn_testFindDatum → AC13 PASS 확인 (raw 점 36개 360° 분포 + LimeGreen RefOrigin + CircleCenter + Radius 라벨)
4. 알고리즘 VerticalTwoHorizontal 로 변경 → btn_testFindDatum → AC14 PASS 확인 (Vertical/HorizA/HorizB raw 점 + LimeGreen RefOrigin)

**PASS 시:** UAT 기록 후 14-05 종료.

**FAIL 시 (D-11 contingency):**
1. 진단 로깅 추가 (`Logging.PrintLog((int)ELogType.Trace, ...)`) — TryTeachCircleTwoHorizontal 진입/Circle 검출/수평 fit/intersection/각도 게이트 각 단계.
2. 결함 식별 — Phi 와이어링 / per-ROI 파라미터 누락 / strip-loop 오적용 등 의심.
3. Fix 후 재 retest.

**FAIL contingency 작업 분량 추정:** plan-phase 가 진단 step 별 plan stub 작성, 실제 구현은 fail 시점에 결함 specific 추가.

---

## Shared Patterns

### A. 주석 Convention — `//260426 hbk Phase 14-XX`

**Source:** CLAUDE.md + Phase 13 기 구현 (`DatumFindingService.cs:639` `//260424 hbk Phase 13 D-09..D-12`)
**Apply to:** 모든 신규/수정 라인 (파일 6개 전체)

```csharp
//260426 hbk Phase 14-01 — <1줄 intent>
// 다중 결정 참조 시: //260426 hbk Phase 14-03 D-05/D-06 — <intent>
```

XAML: `<!--260426 hbk Phase 14-XX — <intent>-->`

### B. 에러 처리 — `try { ... } catch (Exception ex) { config.LastTeachSucceeded = false; error = ex.Message; return false; }`

**Source:** `DatumFindingService.cs:283-288, 452-457` + CLAUDE.md "Wrap all HOperatorSet.* calls in try { } catch { return false; }"
**Apply to:** `VisionAlgorithmService.TryFindCircleByPolarSampling` (전체 본문 wrapping). `HOperatorSet.CloseMeasure` 는 finally — `AppendEdgePointsFromStrip:991-994` 패턴.

### C. DatumConfig write-back 후 이중 신호

**Source:** `MainView.xaml.cs:570-572`
**Apply to:** Phase 14-01 `HandleDatumRoiResize` (write-back 직후).

```csharp
try { datum.RaisePropertyChanged(string.Empty); } catch { }
mParentWindow?.inspectionList?.RefreshParamEditor();
halconViewer.SetDatumOverlay(datum, true);
```

### D. Sanity Clamp (sentinel 0/"" 방어)

**Source:** `DatumFindingService.cs:709-715` (TryFindLine 진입부)
```csharp
if (sigma < 0.4) sigma = 1.0;
if (threshold <= 0) threshold = 20;
if (string.IsNullOrEmpty(polarity)) polarity = "all";
```

**Apply to:** Phase 14-04 `TryFindCircleByPolarSampling` 진입부 + Phase 14-02 `TwoLineAngleToleranceDeg` 가드 (`> 0.0` 체크 = off).

### E. EnsurePerRoiDefaults idempotency

**Source:** `DatumConfig.cs:326-374`
**Apply to:** Phase 14-03 Vertical 분기 추가 — sentinel(==0/"") 만 체크, 재호출 무영향.

### F. 빈 HTuple 안전 반환 (raw 점 0개 케이스)

**Source:** `DatumFindingService.cs:705-706` (TryFindLine 실패 분기 default) + `HalconDisplayService.cs:415-417` (RenderRawEdgePoints null/length-0 가드)
**Apply to:** Phase 14-04 `TryFindCircleByPolarSampling` 실패 분기 + Vertical_DetectedEdge* 미저장 케이스.

```csharp
edgeRows = new HTuple();  // length 0 안전
edgeCols = new HTuple();
```

### G. 알고리즘별 분기 switch 패턴

**Source:** `DatumFindingService.cs:166-176` (TryTeachDatum AlgorithmTypeEnum switch) + `MainView.xaml.cs:704-727` (PublishDatumRoiCandidates switch)
**Apply to:** 모든 신규 알고리즘별 분기. case enum 명시 + default fallback (TwoLineIntersect 또는 logging warn).

---

## No Analog Found

없음 — Phase 14 수정 대상 6개 파일 모두 Phase 12/13 기 구현이 제공한 analog 가 존재한다.

**잠재적 불확실성 (researcher 추가 검증 필요):**
1. **D-08 PropertyGrid 동적 가시성** — PropertyTools 3.1.0 의 `[Browsable]` 런타임 토글 가능성. 미지원 시 Category prefix fallback (deferred).
2. **D-13 Halcon Rectangle2 phi 부호** — sin 앞 minus / phi 자체 정의. SDK 문서 재확인 필수.
3. **14-01 Move 자동 재티칭 회귀 원인** — `_editingDatum` vs `SelectedParam` 불일치 가능성. 진단 로깅 우선.

---

## Metadata

**Analog search scope:**
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` (drag/resize 상태 머신)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (Datum/FAI 분기 라우팅)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (5 ROI 그룹 + EnsurePerRoiDefaults)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (3 알고리즘 + ValidateHorizontalVerticalAngles)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (TryFindCircle legacy + GenMeasureRectangle2)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` (RenderDatumOverlay + RenderRawEdgePoints)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` (legacy 호출처 보존 검증)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (SelectedParam / RefreshParamEditor)
- `C:\Info\Project\DatumMeasure\DatumMeasure\Algorithms\MeasurementAlgorithm.cs` (외부 strip-loop 참조 — polar 변종 미발견)

**Files scanned:** 9 (primary) + git log
**Pattern extraction date:** 2026-04-26

**Phase 14 작성 시 반드시 참조할 구조적 사실:**
- `MainView.xaml.cs` = K&R brace, 신규 Datum 분기는 K&R 유지
- `MainResultViewerControl.xaml.cs` / `DatumFindingService.cs` / `VisionAlgorithmService.cs` / `HalconDisplayService.cs` = Allman, 신규 코드 Allman
- `DatumConfig.cs` = K&R, 신규 Vertical 그룹 K&R
- C# 7.2: `out var` 사용 가능, `switch expression` / `record` 금지
- Plan 14-01 ↔ 14-02 ↔ 14-03 병렬 가능 (서로 다른 코드 섹션). 14-04 → 14-05 직렬 (14-05 가 14-04 호출). 14-03 → 14-05 직렬 (Vertical 그룹 사용).
- Phase 13-06+13-07 자동 재티칭 hot-fix (`NotifyDatumParamMaybeChanged` `IsConfigured` 단일 가드 + `Dispatcher.BeginInvoke(Background)` defer) 패턴 보존 — 14-01 Circle resize 후 자동 재티칭에도 동일 패턴 적용 권장.

---

*Phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux*
*Pattern map: 2026-04-26*
*Next step: gsd-planner — sub-phase 14-01..14-05 별 PLAN.md 생성*
