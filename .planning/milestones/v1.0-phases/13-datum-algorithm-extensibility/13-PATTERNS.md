# Phase 13: datum-algorithm-extensibility (재정의: Datum UX 이월 3건) — Pattern Map

**Mapped:** 2026-04-24
**Files analyzed:** 5 modified (0 new)
**Analogs found:** 5 / 5 (100% — all patterns are in-repo, Phase 12 확장 양식 그대로 이어받음)

---

## File Classification

| 수정 대상 파일 | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `WPF_Example/UI/ContentItem/MainView.xaml` | XAML (툴바) | declarative layout | 같은 파일 L83-96 (`btn_circleRoi` / `btn_teachDatum` 기존 선언) | exact (same file, 같은 Style/Height/FontSize 사용) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | UI code-behind (state machine + event routing) | event-driven + request-response | 같은 파일 L987-1210 (`TeachDatumButton_Click`/`HalconViewer_DatumRectCompleted`/`InvokeTryTeachDatum` Phase 12 패턴) + L515-538 (`HalconViewer_RoiMoveCompleted` FAI 분기) | exact (Phase 12 직후 — 동일 brace style, 동일 주석 convention) |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | UI control (hit-test + event raising) | event-driven + mouse input | 같은 파일 L303-334 (`HitTestSelectedRoi`), L695-723 (Edit mode hit-test 분기), L917-950 (RoiMove delta 계산) | role-match (FAI 전제 → Datum 확장) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | Algorithm service (validation helper) | request-response (pure function) | 같은 파일 L308-314 (`MIN_HORIZONTAL_EDGES` 상수 + insufficient edges 에러 리터럴), L289-314 (horizontal fit 에러 메시지 패턴) | exact (같은 파일 내 Phase 12 에러 리터럴 스타일) |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | Rendering service (Halcon disp_*) | one-shot draw | 같은 파일 L350-508 (`RenderDatumOverlay`), L195-210 (`RenderCircleDraft`), L510-539 (`DrawRoiLabel`/`DrawRoiLabelAt`) | exact (같은 파일 내 Phase 11 D-11 `LastTeachSucceeded` 분기 연장) |

**Brace style observed:**
- `MainView.xaml.cs` = K&R (opening brace on same line) — `private void TeachDatumButton_Click(...) {`
- `MainResultViewerControl.xaml.cs` = Allman (opening brace own line) — `private RoiDefinition HitTestSelectedRoi(Point imagePoint)\n        {`
- `DatumFindingService.cs` = Allman
- `HalconDisplayService.cs` = Allman
- `MainView.xaml` = XAML (irrelevant)

Files 2/3/4/5 each have a single dominant style — **use the style of the file you edit, do not mix within one file** (CLAUDE.md convention).

---

## Pattern Assignments

### 1. `WPF_Example/UI/ContentItem/MainView.xaml` — `btn_testFindDatum` 버튼 + `label_testFindResult` 추가

**Analog:** same file, L83-96 (btn_circleRoi / btn_teachDatum 선언 패턴)

**Imports pattern** (XAML — UserControl.Resources 이미 선언되어 있음, 변경 불필요).

**툴바 버튼 선언 패턴** (L83-96 기존 btn_circleRoi, btn_teachDatum 복사 대상):
```xml
<!--260423 hbk Phase 11 D-15 Circle ROI 드로잉 토글-->
<ToggleButton x:Name="btn_circleRoi" Content="Circle ROI"
    Click="CircleRoiButton_Click"
    Style="{StaticResource RoiToggleButtonStyle}"
    IsEnabled="False"
    MinWidth="80" Height="28" Padding="12,4"
    FontSize="13" FontWeight="SemiBold" Margin="0,0,4,0"/>

<!--260424 hbk Phase 12 D-01 — Datum 티칭 토글 (TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal 3-way 상태머신 진입)-->
<ToggleButton x:Name="btn_teachDatum" Content="Teach Datum"
    Click="TeachDatumButton_Click"
    Style="{StaticResource RoiToggleButtonStyle}"
    IsEnabled="False"
    MinWidth="100" Height="28" Padding="12,4"
    FontSize="13" FontWeight="SemiBold" Margin="0,0,4,0"/>
```

**Phase 13 신규 추가 권장 구조** (btn_teachDatum 바로 다음 줄에 배치):
- `btn_testFindDatum` — `Button` (토글 아님, 클릭 즉시 실행) — `RoiToggleButtonStyle` 재사용 가능하나 토글 미동작 시에도 동일 시각 유지. 또는 `btn_calibrate` 스타일(L101-117 일반 Button)을 참고해 `Background="#2563EB"` + inline `Button.Template` 복사.
- `label_testFindResult` — 기존 `label_drawHint` (L120-122) 패턴을 그대로 복사. `label_pointCount` (L123-125) 와 같은 Column 배치 권장.

**hint label 패턴** (L120-122 — label_drawHint 복사 대상):
```xml
<!-- Phase 2: Drawing mode hint + point count (툴바 내 배치) -->
<Label x:Name="label_drawHint" Grid.Column="1" Visibility="Collapsed"
       HorizontalAlignment="Center" VerticalContentAlignment="Center"
       FontSize="13" Foreground="#FFAAAAAA"/>
```

**주의:** Phase 13 주석은 `//260424 hbk Phase 13 D-05` 형식 (XAML 주석은 `<!--260424 hbk Phase 13 D-05 ...-->`).

---

### 2. `WPF_Example/UI/ContentItem/MainView.xaml.cs` — Plan 02(btn_testFindDatum_Click) + Plan 03(RoiMoveCompleted Datum 분기)

**Analog (Plan 02 신규 버튼 핸들러):** same file, L987-1010 (`TeachDatumButton_Click` 진입 패턴) + L1174-1210 (`InvokeTryTeachDatum` DatumFindingService 호출 + label_drawHint 피드백) + L261-299 (`LoadAndDisplay` OpenFileDialog 경로)

**Analog (Plan 03 RoiMoveCompleted Datum 분기):** same file, L515-538 (`HalconViewer_RoiMoveCompleted` 현재 FAI 전제 구현)

#### 2.1 Plan 02: btn_testFindDatum_Click + LoadImage 다이얼로그

**기존 `TeachDatumButton_Click` 진입 패턴** (L987-1010 — 버튼 핸들러 + `InspectionListView.SelectedParam` Datum 해결):
```csharp
private void TeachDatumButton_Click(object sender, RoutedEventArgs e) {
    if (btn_teachDatum.IsChecked == true) {
        ExitCanvasMode();
        _canvasMode = ECanvasMode.TeachDatum;
        btn_teachDatum.IsChecked = true;

        //260424 hbk Phase 12 — InspectionListView.SelectedParam 으로 DatumConfig 해결 (btn_teachDatum 활성화 조건)
        var datum = mParentWindow?.inspectionList?.SelectedParam as DatumConfig;
        if (datum == null) {
            CustomMessageBox.Show("Datum 노드를 먼저 선택하세요.", "Teach Datum");
            ExitCanvasMode();
            return;
        }
        _editingDatum = datum;

        //260424 hbk Phase 12 D-03 — 알고리즘별 첫 단계 결정 후 StartDatumTeachStep
        _datumTeachStep = GetFirstStep(datum.AlgorithmTypeEnum);
        StartDatumTeachStep(_datumTeachStep);
    }
    else {
        //260424 hbk Phase 12 — 수동 해제 = 취소
        ExitCanvasMode();
    }
}
```

**기존 OpenFileDialog 패턴** (L261-299 `LoadAndDisplay` — Phase 13 LoadImage 경로에서 그대로 복사):
```csharp
public void LoadAndDisplay(ICameraParam param) {
    if (param == null) return;

    var dialog = new OpenFileDialog {
        Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files (*.*)|*.*"
    };
    if (dialog.ShowDialog() != true) {
        return;
    }

    try {
        halconViewer.LoadImage(dialog.FileName);
        halconViewer.UpdateDisplayState(ConvertParamRects(param as ParamBase), null, null);
        _lastRenderedImagePath = dialog.FileName;
        // ... (Phase 13에서는 halconViewer 이미지만 갱신, FAI param 의존부는 건너뜀)
    }
    catch (Exception ex) {
        Logging.PrintErrLog((int)ELogType.Error, ex.Message);
        // 실패 시 label_testFindResult 에 에러 표시
    }
}
```

**기존 DatumFindingService 호출 + label 피드백 패턴** (L1174-1210 `InvokeTryTeachDatum`):
```csharp
private void InvokeTryTeachDatum() {
    if (_editingDatum == null) { ExitCanvasMode(); return; }

    HImage img = halconViewer.CurrentImage; //260424 hbk Phase 12 — Phase 11 이미지 로드 이후 상태
    if (img == null) {
        label_drawHint.Content = "Datum 티칭 실패: 이미지가 없습니다. Grab 하세요";
        label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171")); // error red
        label_drawHint.Visibility = Visibility.Visible;
        _canvasMode = ECanvasMode.None;
        btn_teachDatum.IsChecked = false;
        _editingDatum = null;
        return;
    }

    var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
    string error;
    bool ok = svc.TryTeachDatum(img, _editingDatum, out error);
    if (ok) {
        label_drawHint.Content = "Datum 티칭 완료 — Recipe Save 권장";
        label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80")); // success green
        label_drawHint.Visibility = Visibility.Visible;
        halconViewer.SetDatumOverlay(_editingDatum, true);
    }
    else {
        label_drawHint.Content = "Datum 티칭 실패: " + (error ?? "unknown");
        label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171")); // error red
        label_drawHint.Visibility = Visibility.Visible;
    }

    _canvasMode = ECanvasMode.None;
    btn_teachDatum.IsChecked = false;
}
```

**Phase 13 Plan 02 신규 코드 지침:**
- `BtnTestFindDatum_Click(object sender, RoutedEventArgs e)` — `mParentWindow?.inspectionList?.SelectedParam as DatumConfig` 로 Datum 해결 (L994 패턴 복사).
- `datum.IsConfigured && datum.LastTeachSucceeded` 가드 (CustomMessageBox 에러 메시지 + early return, L996-999 복사).
- 2단계 UX: (a) 현재 `halconViewer.CurrentImage`(L1177 패턴) 이 있고 사용자가 "현재 이미지로 테스트" 선택 시 그대로 사용. (b) "다른 파일" 선택 시 L264-272 `OpenFileDialog` 패턴 복사 → `HImage` 로드 (`new HImage(path)` 또는 `halconViewer.LoadImage(path)` 후 `halconViewer.CurrentImage` 읽기).
- `svc.TryFindDatum(img, _editingDatum, out HTuple transform, out string error)` 호출 (DatumFindingService.cs L28 시그니처).
- 성공 시 `label_testFindResult.Content = "TryFind OK — RefOrigin=({row:F1}, {col:F1}), Angle={angle:F3} rad"` + LimeGreen; 실패 시 빨간색 + error 문자열.
- 성공 시 `HalconDisplayService.RenderDatumFindResult(...)` 호출 (패턴 5 참조).
- 실행 브래이스 = K&R (이 파일 스타일 유지).
- 주석 = `//260424 hbk Phase 13 D-05` 모든 신규 라인.

#### 2.2 Plan 03: HalconViewer_RoiMoveCompleted Datum 분기

**현재 구현** (L515-538 — FAI 전제):
```csharp
private void HalconViewer_RoiMoveCompleted(object sender, RoiMoveCompletedArgs e) {
    if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

    var fai = FindFAIByName(e.RoiId);
    if (fai == null) return;

    bool handledCircle = false;
    foreach (var m in fai.Measurements) {
        var circle = m as CircleDiameterMeasurement;
        if (circle != null) {
            circle.Circle_Row += e.DeltaRow;
            circle.Circle_Col += e.DeltaCol;
            handledCircle = true;
            break;
        }
    }
    if (!handledCircle) {
        fai.ROI_Row += e.DeltaRow;
        fai.ROI_Col += e.DeltaCol;
    }

    var rois = GetCurrentFAIRois();
    halconViewer.UpdateDisplayState(rois, e.RoiId, null, null);
}
```

**Phase 13 Plan 03 신규 코드 지침:**
- `_editingDatum != null` 혹은 `mParentWindow?.inspectionList?.SelectedParam as DatumConfig` 이 null 이 아니면 **Datum 분기 우선** (L517 FAI lookup 전에).
- `e.RoiId` 로 어느 ROI 인지 판정 — `"Datum.Line1"` / `"Datum.Line2"` / `"Datum.Circle"` / `"Datum.HorizontalA"` / `"Datum.HorizontalB"` / `"Datum.Vertical"` 같은 key 규약 수립 (CONTEXT.md specifics 스케치 참조).
- delta 반영:
  - Rect 계열 (Line1/Line2/Vertical/HorizontalA/HorizontalB): `_editingDatum.Line1_Row += e.DeltaRow; Line1_Col += e.DeltaCol;` (L1111-1115 write-back 패턴의 `+=` 버전).
  - Circle 계열 (Datum.Circle): `_editingDatum.CircleROI_Row += e.DeltaRow; CircleROI_Col += e.DeltaCol;` (CircleROI_Radius 불변, L1154-1156 참조).
- **D-03 즉시 재티칭:** 좌표 반영 직후 `InvokeTryTeachDatum()` 호출 (L1174 — 기존 함수 그대로 재사용, 별도 버튼 없이 자동 재계산 = CONTEXT D-03 "즉시 실행" 결정).
- Plan 02 L1141-1144 와 동일한 이중 신호도 추가:
  ```csharp
  try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
  mParentWindow?.inspectionList?.RefreshParamEditor();
  halconViewer.SetDatumOverlay(_editingDatum, true);
  ```
- early return 으로 기존 FAI 경로와 충돌 방지.

**주의:** CONTEXT L517 `if (fai == null) return;` 은 현재 FAI 없으면 silently return 이지만, Phase 13 에서는 Datum 분기 실패 시 FAI 분기 fall-through 하지 말고 Datum 전용 early return. 두 분기는 `e.RoiId` prefix 로 구분 (`"Datum."` vs FAI 이름) — 복잡도 증가하면 helper 분리.

---

### 3. `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — hit-test Datum 후보 인식

**Analog:** same file, L303-334 (`HitTestSelectedRoi` — `_rois` 목록에서 `_selectedRoiId` 하나만 대상), L695-723 (Edit mode hit-test 분기 — 핸들 우선 → 바디 히트 → 이동 시작), L917-950 (`RoiMoveCompleted` delta 계산)

**현재 hit-test 패턴** (L303-334):
```csharp
//260423 hbk ROI hit-test: 선택된 ROI 내부 클릭인지 판정
private RoiDefinition HitTestSelectedRoi(Point imagePoint)
{
    if (string.IsNullOrEmpty(_selectedRoiId))
    {
        return null;
    }

    var roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId);
    if (roi == null)
    {
        return null;
    }

    if (roi.Shape == RoiShape.Circle)
    {
        double dr = imagePoint.Y - roi.CenterRow;
        double dc = imagePoint.X - roi.CenterCol;
        if (Math.Sqrt(dr * dr + dc * dc) <= roi.Radius)
        {
            return roi;
        }
        return null;
    }

    if (imagePoint.Y >= roi.Row1 && imagePoint.Y <= roi.Row2 &&
        imagePoint.X >= roi.Column1 && imagePoint.X <= roi.Column2)
    {
        return roi;
    }
    return null;
}
```

**현재 MouseDown hit-test 분기** (L695-723 — Edit 모드 + 드로잉 모드 아님):
```csharp
//260423 hbk Edit 모드 전용: 핸들 히트 → 리사이즈 시작, 바디 히트 → 이동 시작
if (_isEditMode && !IsAnyDrawingModeActive() && HasImage)
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

    var hitRoi = HitTestSelectedRoi(mouseState.ImagePoint);
    if (hitRoi != null)
    {
        _isMovingRoi = true;
        _moveStartImagePoint = mouseState.ImagePoint;
        _movingRoiSnapshot = hitRoi.Clone();
        SetPanCursor(Cursors.SizeAll);
        PublishPointerInfo();
        return;
    }
}
```

**RoiMoveCompleted delta 계산 패턴** (L917-950 — MouseUp 델타 계산 + event raise):
```csharp
//260423 hbk ROI 이동 완료: 델타 계산 후 RoiMoveCompleted 발생
if (_isMovingRoi && _movingRoiSnapshot != null)
{
    double dr = 0;
    double dc = 0;
    var target = _rois.FirstOrDefault(r => r.Id == _movingRoiSnapshot.Id);
    if (target != null)
    {
        if (target.Shape == RoiShape.Circle)
        {
            dr = target.CenterRow - _movingRoiSnapshot.CenterRow;
            dc = target.CenterCol - _movingRoiSnapshot.CenterCol;
        }
        else
        {
            dr = target.Row1 - _movingRoiSnapshot.Row1;
            dc = target.Column1 - _movingRoiSnapshot.Column1;
        }
    }
    string movedId = _movingRoiSnapshot.Id;
    _isMovingRoi = false;
    _movingRoiSnapshot = null;
    SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
    if (Math.Abs(dr) > 0.5 || Math.Abs(dc) > 0.5)
    {
        RoiMoveCompleted?.Invoke(this, new RoiMoveCompletedArgs
        {
            RoiId = movedId,
            DeltaRow = dr,
            DeltaCol = dc
        });
    }
    return;
}
```

**Phase 13 Plan 03 신규 코드 지침 (selection-aware 확장):**
- **옵션 A (권장) — Datum ROI 목록 주입 API 추가:**
  - `public void SetDatumRoiCandidates(List<RoiDefinition> datumRois)` — `_rois` 와 병렬로 `_datumRoiCandidates` 필드 관리. MainView 가 Datum 노드 선택 상태에서 호출.
  - `HitTestSelectedRoi` 내부에서 `_rois` 우선 → null 이면 `_datumRoiCandidates` fallback. 각 Datum ROI 는 `RoiDefinition.Id = "Datum.Line1"` 등 prefix 로 구분.
  - Edit 모드 게이트(`_isEditMode && !IsAnyDrawingModeActive()`)는 현재 유지 — Datum 편집도 Edit 모드 진입 필요 (기존 ContextMenu Edit ROI 메뉴 재사용). **단 CONTEXT D-01 에 따르면 "별도 편집 모드 진입 UI 없음 — hit-test 기반 자동 선택"** 이므로, Datum 노드 선택 상태일 때는 Edit mode 없이도 hit-test 시도 허용. 판단 기준 = `_datumRoiCandidates.Count > 0` 이면 mode 무관.
- **옵션 B — Datum ROI 를 기존 `_rois` 에 합류:** MainView 가 `UpdateDisplayState` 로 Datum ROI 도 전달. 구분은 `Id` prefix + `IsTaught=false` 같은 플래그.
- **권장 경로 = 옵션 A** — MainResultViewerControl 의 기존 FAI 전제 hit-test 로직을 최소 침습 변경. `_datumRoiCandidates` 는 `_rois` 와 같은 방식으로 Clone 보관.
- **Datum ROI 의 `RoiDefinition` 생성:** Rectangle2 (Row/Col/Phi/Length1/Length2) → 기존 `RoiDefinition` 의 Rect bbox 로 변환 필요. phi=0 전제 (Phase 12 D-03 brace "TeachDatum 모드에서 Rect 드로잉은 phi=0 고정") 가정하면 `Row1 = Row - Length1, Row2 = Row + Length1, Column1 = Col - Length2, Column2 = Col + Length2`. Circle 은 `Shape = RoiShape.Circle, CenterRow = CircleROI_Row, CenterCol = CircleROI_Col, Radius = CircleROI_Radius`.
- **MouseUp → RoiMoveCompleted** 는 기존 L917-950 그대로 유지. RoiId 는 `"Datum.Line1"` 등 prefix 로 MainView 가 구분.
- Brace style = Allman (이 파일 스타일 유지).
- 주석 = `//260424 hbk Phase 13 D-02..D-04` 모든 신규 라인.

---

### 4. `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — `ValidateHorizontalVerticalAngles` helper + 호출 연결

**Analog:** same file, L17 (`const int MIN_HORIZONTAL_EDGES = 10` 상수 패턴), L308-314 (insufficient edges 에러 + `LastTeachSucceeded=false` 반환 패턴), L289-304 (horizontal fit 에러 리터럴 패턴), L377 (`// TODO: Phase 13 — 방향 정합성 검사` 이월 마커 — Phase 13 이 이 마커를 교체)

**상수 선언 패턴** (L16-17):
```csharp
//260423 hbk Phase 12 D-15 — 수평 2-ROI concat 최소 에지점 (신뢰 라인 피팅 기준)
private const int MIN_HORIZONTAL_EDGES = 10;
```

**기존 에러 리터럴 패턴** (L308-314 — CircleTwoHorizontal insufficient edges 가드):
```csharp
//260423 hbk Phase 12 D-15 — 수평 2-ROI concat 에지점 합계 임계값 검사
int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
if (totalEdges < MIN_HORIZONTAL_EDGES)
{
    config.LastTeachSucceeded = false;
    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")"; //260423 hbk Phase 12 D-14/D-15 SPEC AC literal (Req 5a)
    return false;
}
```

**기존 horizontal fit 에러 패턴** (L289-290, 304, 313, 332):
```csharp
error = "Horizontal line fit failed: " + edgeErrorA; //260423 hbk Phase 12 D-14 (Req 5a)
error = "Horizontal line fit failed: " + edgeErrorB; //260423 hbk Phase 12 D-14 (Req 5a)
error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
error = "Horizontal line fit failed: " + fitEx.Message; //260423 hbk Phase 12 D-14 (Req 5a)
```

**기존 collinear/parallel intersection 가드 패턴** (L344-356 — CircleTwoHorizontal 교점 검증):
```csharp
//260423 hbk Phase 12 D-14/D-16 — 교점 중첩/평행 감지 (기존 TwoLineIntersect 와 동일 로직)
if (isOverlapping.I == 1)
{
    config.LastTeachSucceeded = false;
    error = "Intersection undefined: lines are collinear"; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5b)
    return false;
}
if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
{
    config.LastTeachSucceeded = false;
    error = "Intersection undefined: lines are parallel"; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5b)
    return false;
}
```

**기존 Phase 13 이월 TODO 마커 위치** (L377 — 이 마커 교체 대상):
```csharp
// TODO: Phase 13 — 방향 정합성 검사 (운용 정책 확립 후 구현) //260423 hbk Phase 12 D-17 — Req 5d deferred
return true;
```

**Phase 13 Plan 01 신규 코드 지침:**

- **상수 2개 추가** (L17 MIN_HORIZONTAL_EDGES 아래):
  ```csharp
  //260424 hbk Phase 13 D-10 — Req 5d 방향 정합성 임계각 (고정값)
  private const double HORIZONTAL_TOLERANCE_DEG   = 15.0;
  private const double PERPENDICULAR_TOLERANCE_DEG = 5.0;
  ```

- **private static helper 추가** (CONTEXT specifics L226-254 스케치 따라):
  ```csharp
  //260424 hbk Phase 13 D-09..D-12 — 수평선 phi 방향 + 수평/수직 직각성 검증 게이트
  // CircleTwoHorizontal / VerticalTwoHorizontal 공통 사용. TwoLineIntersect 는 무효(호출 안 함).
  private static bool ValidateHorizontalVerticalAngles(
      double horizPhiRad, double vertPhiRad, out string error)
  {
      double horizDeg = Math.Abs(horizPhiRad * 180.0 / Math.PI);
      if (horizDeg > 90.0) horizDeg = 180.0 - horizDeg;  // [0, 90] normalize
      if (horizDeg > HORIZONTAL_TOLERANCE_DEG)
      {
          error = "Horizontal line orientation out of range: " +
                  (horizPhiRad * 180.0 / Math.PI).ToString("F1") +
                  "° (expected ±" + HORIZONTAL_TOLERANCE_DEG.ToString("F1") + "°)";
          return false;
      }
      double deltaDeg = Math.Abs((horizPhiRad - vertPhiRad) * 180.0 / Math.PI);
      while (deltaDeg >= 180.0) deltaDeg -= 180.0;
      double perpErr = Math.Abs(deltaDeg - 90.0);
      if (perpErr > PERPENDICULAR_TOLERANCE_DEG)
      {
          error = "Horizontal/Vertical perpendicularity violated: delta=" +
                  deltaDeg.ToString("F1") +
                  "° (expected 90°±" + PERPENDICULAR_TOLERANCE_DEG.ToString("F1") + "°)";
          return false;
      }
      error = null;
      return true;
  }
  ```

  **C# 7.2 제약:** `$"..."` interpolated string 대신 `+ concat` (CONTEXT specifics 스케치는 `$"..."` 사용했으나 프로젝트 전반 관례는 concat 또는 `string.Format`). `Math.Abs` / `Math.PI` 는 C# 7.2 가용.

- **CircleTwoHorizontal 연결** (L374-377 근처 — IsConfigured=true 직후, Line1Detected/Line2Detected 대입 이후):
  수평 phi = `Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D)` (이미 L361 RefAngleRad 로 계산됨).
  수직 phi = `Math.PI / 2.0` (수직 가상선은 col=const 이므로 수직 = 90°) — 또는 `Math.Atan2(1.0, 0.0)`.
  ```csharp
  //260424 hbk Phase 13 D-09..D-12 — Req 5d 방향 정합성 검증 (기존 TODO 마커 교체)
  double vertPhiCircle = Math.PI / 2.0; // 수직 가상선 phi (col=const → 90°)
  string angleError;
  if (!ValidateHorizontalVerticalAngles(config.RefAngleRad, vertPhiCircle, out angleError))
  {
      config.LastTeachSucceeded = false;
      error = angleError;
      return false;
  }
  return true;
  ```
  **기존 L377 TODO 주석 삭제** 후 위 블록으로 교체.

- **VerticalTwoHorizontal 연결** (L520-522 근처 — `LastTeachSucceeded = true` 직후):
  수평 phi = `config.RefAngleRad` (L508 에서 계산됨).
  수직 phi = `Math.Atan2(vrE - vrB, vcE - vcB)` (실제 검출된 수직선 방향, L485-488 IntersectionLl 인자 재사용).
  ```csharp
  //260424 hbk Phase 13 D-09..D-12 — Req 5d 방향 정합성 검증 (VerticalTwoHorizontal)
  double vertPhiDetected = Math.Atan2(vrE - vrB, vcE - vcB);
  string angleError;
  if (!ValidateHorizontalVerticalAngles(config.RefAngleRad, vertPhiDetected, out angleError))
  {
      config.LastTeachSucceeded = false;
      error = angleError;
      return false;
  }
  return true;
  ```

- **TwoLineIntersect 는 검증 안 함** (D-12 — 무효).

- Brace style = Allman.
- 주석 = `//260424 hbk Phase 13 D-09..D-12` 모든 신규 라인.
- **SPEC AC 에러 리터럴 준수:** CONTEXT L78 `"Horizontal line orientation out of range: phi=XX.X° (expected ±15°)"` / `"Horizontal/Vertical perpendicularity violated: angle=XX.X° (expected 90°±5°)"` — CONTEXT 스케치는 "phi=" / "angle=" prefix 를 사용했으나 helper 에서는 prefix 생략하고 값만 표시한다(코드 스케치와 일관). **SPEC 확정 리터럴이 확정되면 Plan 에서 조정.**

---

### 5. `WPF_Example/Halcon/Display/HalconDisplayService.cs` — `RenderDatumFindResult` (신규 메서드)

**Analog:** same file, L350-508 (`RenderDatumOverlay` — DatumConfig 입력, HWindow 에 disp_line/disp_circle/WriteString 으로 오버레이 렌더), L459-501 (`LastTeachSucceeded` 하 cross + Circle detection 오버레이 렌더 패턴), L195-210 (`RenderCircleDraft` — 독립 렌더 메서드 시그니처 패턴), L510-539 (`DrawRoiLabel`/`DrawRoiLabelAt` — WriteString 헬퍼)

**기존 독립 렌더 메서드 시그니처 패턴** (L195-210 — `RenderCircleDraft` 는 이미 `HWindow + primitives` 받는 무상태 메서드):
```csharp
public void RenderCircleDraft(HWindow window, double centerRow, double centerCol, double radius)
{
    try
    {
        window.SetColor("yellow");
        window.SetLineWidth(2);
        HOperatorSet.DispCircle(window, centerRow, centerCol, radius);

        // Center cross marker (red, 6px)
        window.SetColor("red");
        window.DispLine(centerRow - 6, centerCol, centerRow + 6, centerCol);
        window.DispLine(centerRow, centerCol - 6, centerRow, centerCol + 6);
    }
    catch
    {
        // Suppress display errors
    }
}
```

**기존 RenderDatumOverlay Success overlay 패턴** (L459-501 — LastTeachSucceeded 분기, 노란/빨간/시안 색상 팔레트):
```csharp
//260423 hbk Phase 11 D-11 — 검출 라인 2개 + 교점 오버레이 (TryTeachDatum 성공 시에만, 기존 cyan/blue/magenta 팔레트는 건드리지 않음)
if (datum.LastTeachSucceeded)
{
    // Line1 detected (yellow)
    HOperatorSet.SetColor(window, "yellow");
    HOperatorSet.SetLineWidth(window, 2);
    HOperatorSet.DispLine(window,
        datum.Line1Detected_RBegin, datum.Line1Detected_CBegin,
        datum.Line1Detected_REnd,   datum.Line1Detected_CEnd);

    // Line2 detected (cyan)
    HOperatorSet.SetColor(window, "cyan");
    HOperatorSet.DispLine(window,
        datum.Line2Detected_RBegin, datum.Line2Detected_CBegin,
        datum.Line2Detected_REnd,   datum.Line2Detected_CEnd);

    // Intersection cross (red, 20px half-length, line width 2) — UI-SPEC Datum overlay palette
    HOperatorSet.SetColor(window, "red");
    HOperatorSet.SetLineWidth(window, 2);
    const double crossHalf = 20.0;
    HOperatorSet.DispLine(window, datum.RefOriginRow - crossHalf, datum.RefOriginCol,
                                  datum.RefOriginRow + crossHalf, datum.RefOriginCol);
    HOperatorSet.DispLine(window, datum.RefOriginRow, datum.RefOriginCol - crossHalf,
                                  datum.RefOriginRow, datum.RefOriginCol + crossHalf);

    //260424 hbk Phase 12 D-13 — CircleTwoHorizontal 검출 원 오버레이 (노란 원 + 빨간 중심 십자)
    if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal
        && datum.CircleDetected_Radius > 0)
    {
        HOperatorSet.SetColor(window, "yellow");
        HOperatorSet.SetLineWidth(window, 2);
        HOperatorSet.DispCircle(window,
            datum.CircleCenter_Row, datum.CircleCenter_Col, datum.CircleDetected_Radius);
        // ... (중심 십자)
    }
}
```

**기존 라벨 헬퍼 패턴** (L510-539):
```csharp
//260424 hbk Phase 12 Gap-2 — Datum ROI 라벨 그리기
private void DrawRoiLabel(HWindow window, double row, double col, double phi,
    double length1, double length2, string label)
{
    double cosP = Math.Cos(phi);
    double sinP = Math.Sin(phi);
    double labelRow = row + (-length1) * cosP - (-length2) * sinP - 22;
    double labelCol = col + (-length1) * sinP + (-length2) * cosP;
    DrawRoiLabelAt(window, labelRow, labelCol, label);
}

private void DrawRoiLabelAt(HWindow window, double row, double col, string label)
{
    try
    {
        EnsureFontInitialized(window);
        HOperatorSet.SetColor(window, "yellow");
        HOperatorSet.SetTposition(window, row, col);
        HOperatorSet.WriteString(window, label);
    }
    catch
    {
        // Suppress display errors (기존 RenderDatumOverlay catch 관습 유지)
    }
}
```

**Phase 13 Plan 02 신규 메서드 지침:**

- **메서드 시그니처** (CONTEXT D-07 — 검출 RefOrigin 십자 + 좌표 숫자):
  ```csharp
  //260424 hbk Phase 13 D-07 — 런타임 TryFindDatum 성공 시 검출 RefOrigin 주황 십자 + 좌표 텍스트 오버레이
  //  기존 RenderDatumOverlay 의 teach-success 빨간 십자 와 시각적으로 구분되는 팔레트 (주황/점선 느낌).
  //  datum.RefOriginRow/Col 은 TryFindDatum 내부에서 업데이트됨 (CurrentTransform 적용 전 원시 검출값).
  public void RenderDatumFindResult(HWindow window, DatumConfig datum)
  {
      if (datum == null) return;

      try
      {
          // 주황 십자 (20px half, 3px 굵기 — 기존 teach 빨강 2px 와 차별화)
          HOperatorSet.SetColor(window, "orange");
          HOperatorSet.SetLineWidth(window, 3);
          const double crossHalf = 20.0;
          HOperatorSet.DispLine(window,
              datum.RefOriginRow - crossHalf, datum.RefOriginCol,
              datum.RefOriginRow + crossHalf, datum.RefOriginCol);
          HOperatorSet.DispLine(window,
              datum.RefOriginRow, datum.RefOriginCol - crossHalf,
              datum.RefOriginRow, datum.RefOriginCol + crossHalf);

          // 좌표 텍스트 (십자 오른쪽 위)
          EnsureFontInitialized(window);
          HOperatorSet.SetColor(window, "orange");
          HOperatorSet.SetTposition(window, datum.RefOriginRow - crossHalf - 20, datum.RefOriginCol + crossHalf + 5);
          HOperatorSet.WriteString(window, "TryFind (" + datum.RefOriginRow.ToString("F1") + ", " + datum.RefOriginCol.ToString("F1") + ")");
      }
      catch
      {
          // Suppress display errors (기존 RenderDatumOverlay catch 관습 유지)
      }
  }
  ```

- **호출 측 연결:** MainView.BtnTestFindDatum_Click 성공 분기에서
  ```csharp
  var displaySvc = new HalconDisplayService();
  halconViewer.[HWindow 직접 접근 경로] ... RenderDatumFindResult(window, datum);
  ```
  — 단 `halconViewer` 는 `MainResultViewerControl` 이고 HWindow 는 private (ViewerHost.HalconWindow). 기존 `halconViewer.SetDatumOverlay(_editingDatum, true)` 처럼 **MainResultViewerControl 에 `SetDatumFindResultOverlay(DatumConfig)` public 메서드 추가** 고려 — L512 SetDatumOverlay 패턴과 대칭.
  대안: MainResultViewerControl 의 Render() 내부에서 L605 근처 `if (_datumConfig != null) _displayService.RenderDatumOverlay(...)` 아래에 `if (_datumFindResult != null) _displayService.RenderDatumFindResult(...)` 추가 — SetDatumFindResultOverlay 가 플래그 세팅.

- **`label_testFindResult` 좌표 숫자는 XAML Label 에 별도 표시** (Halcon 오버레이 텍스트 + WPF Label 이중 표시 가능 — CONTEXT D-07 허용).

- Brace style = Allman (이 파일 스타일 유지).
- 주석 = `//260424 hbk Phase 13 D-07` 모든 신규 라인.

---

## Shared Patterns

### A. 주석 Convention — `//260424 hbk Phase 13 D-XX`

**Source:** CLAUDE.md + Phase 12 기 구현 (e.g., L1048 `//260424 hbk Phase 12`, L289 `//260423 hbk Phase 12 D-14`)
**Apply to:** 모든 신규/수정 라인 (파일 5개 전체)

```csharp
//260424 hbk Phase 13 D-05 — <1줄 intent>
// 다중 결정 참조 시: //260424 hbk Phase 13 D-09..D-12 — <intent>
```

XAML: `<!--260424 hbk Phase 13 D-05 — <intent>-->`

### B. 에러 처리 — `try { ... } catch { }` 실패 시 bool+out 반환

**Source:** `DatumFindingService.cs` L380-385 (CircleTwoHorizontal catch) + CLAUDE.md "Wrap all HOperatorSet.* calls in try { } catch { return false; }"
**Apply to:** `DatumFindingService.ValidateHorizontalVerticalAngles` (단, 이 helper 는 순수 수학 연산이므로 try/catch 불필요 — 숫자 NaN/Infinity 가드만).
**Apply to:** `HalconDisplayService.RenderDatumFindResult` (전체 body 를 `try { ... } catch { // Suppress display errors }` 로 감싸기 — L504-507 기존 패턴 복사).

### C. DatumConfig write-back 후 이중 신호

**Source:** `MainView.xaml.cs` L1141-1144 + `InspectionListView.xaml.cs` L26-31 (RefreshParamEditor)
**Apply to:** Plan 03 ROI 이동 완료 후 (write-back 직후).

```csharp
try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
mParentWindow?.inspectionList?.RefreshParamEditor();
halconViewer.SetDatumOverlay(_editingDatum, true);
```

필드 대입만 해서는 PropertyGrid 갱신 안 됨 (자동 속성 INotifyPropertyChanged 미발동). ParamBase.RaisePropertyChanged("") + ParamEditor.SelectedObject null→재할당 이중 신호 필수.

### D. ExitCanvasMode 확장

**Source:** `MainView.xaml.cs` L612-644
**Apply to:** Plan 02 btn_testFindDatum_Click — 취소 경로 있으면 ExitCanvasMode() 호출; Plan 03 RoiMoveCompleted 자동 재티칭 경로는 canvas mode 를 유지할 수도(Datum 재편집 반복) 해제할 수도(editing 세션 일회) — CONTEXT D-03 "매 이동마다 재계산" 기준으로 모드 유지 권장.

### E. CustomMessageBox.Show 가드 패턴

**Source:** `MainView.xaml.cs` L996-997 (Datum 노드 미선택 가드)
**Apply to:** Plan 02 BtnTestFindDatum_Click 초기 진입 — `datum == null || !datum.IsConfigured || !datum.LastTeachSucceeded` early return.

```csharp
if (datum == null || !datum.IsConfigured || !datum.LastTeachSucceeded) {
    CustomMessageBox.Show("Datum 티칭이 완료된 후 테스트 가능합니다.", "Datum Find 테스트");
    return;
}
```

### F. 이벤트 구독 unsubscribe-first (double-fire 방지)

**Source:** `MainView.xaml.cs` L1043-1044, L620-621
**Apply to:** Plan 02 btn_testFindDatum_Click 진입 시 (재호출 가능 버튼이라 unsubscribe 고려 없음이 기본) + Plan 03 RoiMoveCompleted Datum 분기 (한 번만 구독).

```csharp
// 구독 전에 항상 unsubscribe: //260424 hbk Phase 12
halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;
```

### G. InspectionListView.SelectedParam → DatumConfig 해결

**Source:** `MainView.xaml.cs` L994 (`mParentWindow?.inspectionList?.SelectedParam as DatumConfig`)
**Apply to:** Plan 02 BtnTestFindDatum_Click + Plan 03 RoiMoveCompleted Datum 분기 (editing 중이 아닐 때 fallback 으로 사용).

```csharp
var datum = _editingDatum ?? (mParentWindow?.inspectionList?.SelectedParam as DatumConfig);
```

---

## No Analog Found

없음 — Phase 13 수정 대상 5개 파일 모두 Phase 12 기 구현이 제공한 analog 가 존재한다. 신규 DTO/helper 파일도 불필요(Plan 01 의 ValidateHorizontalVerticalAngles 는 DatumFindingService 내부 private static; Plan 02 의 RenderDatumFindResult 는 HalconDisplayService 에 추가; Plan 03 의 hit-test 확장은 MainResultViewerControl 내부).

단 하나의 잠재적 불확실성:
- Plan 03 Gap-1 의 "selection-aware hit-test API" — MainResultViewerControl 가 Datum ROI 목록을 받는 방식(`SetDatumRoiCandidates(List<RoiDefinition>)` vs 기존 `_rois` 합류). Planner 가 CONTEXT D-04 의 "방식은 planner 재량" 기준으로 선택. 권장 = SetDatumRoiCandidates 신규 API (분리).

---

## Metadata

**Analog search scope:**
- `WPF_Example/UI/ContentItem/MainView.xaml` / `MainView.xaml.cs`
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- `WPF_Example/Halcon/Display/HalconDisplayService.cs`
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (read-only reference)
- `WPF_Example/MainWindow.xaml.cs` (SelectedParam 참조 패턴 검증)

**Files scanned:** 7 (primary) + git log for MainResultViewerControl 이력
**Pattern extraction date:** 2026-04-24

**Phase 13 작성 시 반드시 참조할 구조적 사실:**
- MainView.xaml.cs = **K&R brace** (opening brace same line). 모든 신규 메서드는 이 스타일 유지.
- MainResultViewerControl.xaml.cs / DatumFindingService.cs / HalconDisplayService.cs = **Allman brace**. 이 3개 파일 신규 코드는 Allman.
- C# 7.2: `$"..."` 는 사용 가능, `switch expression` / `record` / `nullable reference types` 금지. `out var` 는 C# 7.0 이므로 사용 가능.
- Plan 01 → Plan 02 → Plan 03 순서 직렬 실행 권장 (Plan 02/03 병렬 시 MainView.xaml.cs 병합 충돌 위험 — CONTEXT specifics L268).
