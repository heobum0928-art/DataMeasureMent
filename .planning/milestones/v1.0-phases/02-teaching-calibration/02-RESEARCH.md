# Phase 2: 티칭 & 캘리브레이션 - Research

**Researched:** 2026-04-03
**Domain:** WPF ROI 오버레이, FAIConfig-RoiDefinition 브릿지, 픽셀-mm 캘리브레이션, TeachingStorageService INI 저장
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TCH-01 | 캔버스에 FAI ROI 오버레이를 시각적으로 표시한다 (Edge 방향, 범위) | HalconViewerControl.SetRois + UpdateDisplayState 이미 존재. FAIConfig → RoiDefinition 변환 헬퍼 신규 작성 필요. MainView.FAIResults_SelectionChanged stub을 채워서 선택 FAI 하이라이트. |
| TCH-02 | TeachingStorageService를 통해 ROI 데이터를 저장/로드한다 | FAIConfig.ROI_Row/Col/Phi/Length1/Length2는 INI에 ParamBase.Save/Load로 이미 저장. ROI 오버레이용 RoiDefinition은 TeachingStorageService(JSON)로 별도 저장하거나 FAIConfig 필드를 오버레이 전용으로 재사용. 두 경로 모두 기존 인프라로 구현 가능. |
| ALG-03 | 픽셀→mm 변환을 위한 캘리브레이션 기능을 제공한다 | RoiDefinition.PixelResolutionX/Y 이미 존재. TeachingWindow에서 수동 입력 패턴 이미 구현됨. CalibrationViewModel은 별도 ModelFinder 패턴용. FAIConfig 레벨의 PixelResolution 저장/로드 신규 추가 필요. |
</phase_requirements>

---

## Summary

Phase 2는 Phase 1이 완성한 3영역 레이아웃(TreeView | 캔버스 | 결과 테이블) 위에, FAI ROI 시각화와 픽셀-mm 캘리브레이션 기능을 추가한다. Phase 5에서 이미 정의된 `FAIConfig`(ROI 파라미터 5개: Row/Col/Phi/Length1/Length2)와 Phase 1에서 재사용 확정된 `HalconViewerControl`/`MainResultViewerControl`이 이 Phase의 핵심 연결 고리다.

구조적으로 세 가지 문제를 풀어야 한다. 첫째, `FAIConfig`의 `ROI_Row/Col/Phi/Length1/Length2` 필드를 화면에 그릴 수 있는 `RoiDefinition`으로 변환하는 브릿지 로직이 없다. 둘째, 결과 테이블에서 FAI 행을 선택했을 때 캔버스에서 해당 ROI가 하이라이트되어야 하는 이벤트 연결이 Phase 1에서 stub(`// Phase 2: ROI highlight...`)으로 남아 있다. 셋째, 픽셀-mm 변환 계수(`PixelResolutionX/Y`)가 `RoiDefinition` 수준에만 존재하고, `FAIConfig` 레벨에서 저장/로드 및 시스템 적용 경로가 아직 없다.

기존 자산(`TeachingWindow`, `HalconViewerControl.StartRectangleDrawing/CommitActiveRectangle`, `TeachingStorageService`, `HalconTeachingHelper.BuildFixedTeachingPath`)은 이미 완전히 동작하며, 이 Phase는 이것들을 FAIConfig/Shot 레이어에 연결하는 작업이다. 새로 작성해야 하는 코드량은 최소화할 수 있다.

**Primary recommendation:** `FAIConfig`에 `ToRoiDefinition()` 변환 메서드를 추가하고, `InspectionViewModel`에 선택된 FAI의 ROI를 캔버스에 전달하는 경로를 추가한다. 캘리브레이션은 `TeachingWindow`(또는 간단한 TextBox dialog)를 통해 `FAIConfig` 또는 `ShotConfig` 레벨의 `PixelResolutionX/Y`를 입력받고 INI로 저장한다.

---

## Standard Stack

### Core (이미 프로젝트에 존재)

| 컴포넌트 | 위치 | 목적 | Phase 2 활용 방식 |
|---------|------|------|-----------------|
| `HalconViewerControl` | `UI/ContentItem/HalconViewerControl.xaml.cs` | ROI 드로잉, 선택, 오버레이 렌더링 | `SetRois()`, `SetSelectedRoi()`, `StartRectangleDrawing()`, `CommitActiveRectangle()` |
| `MainResultViewerControl` | `UI/ContentItem/MainResultViewerControl.xaml.cs` | 검사 결과 뷰어 (MainView 캔버스) | `UpdateDisplayState(rois, overlays, messages)` — ROI 오버레이 전달 경로 |
| `HalconDisplayService` | `Halcon/Display/HalconDisplayService.cs` | HWindow에 ROI 사각형 렌더링 | 직접 호출 안함; MainResultViewerControl 내부에서 사용 |
| `RoiDefinition` | `Halcon/Models/RoiDefinition.cs` | ROI 좌표 + 에지 파라미터 컨테이너 | `FAIConfig.ToRoiDefinition()` 변환 타깃; `PixelResolutionX/Y` 포함 |
| `TeachingStorageService` | `Halcon/Services/TeachingStorageService.cs` | `DataContractJsonSerializer` 기반 JSON 저장/로드 | Shot-FAI 교습 파일 저장에 재사용 가능 |
| `HalconTeachingHelper` | `Halcon/Services/TeachingStorageService.cs` | 경로 계산, Job 생성, Job 저장/로드 헬퍼 | `BuildFixedTeachingPath(sourceName)` — Shot 단위 JSON 경로 계산 |
| `TeachingWindow` | `UI/Dialog/TeachingWindow.xaml.cs` | ROI 드로잉 dialog (HalconViewerControl 포함) | FAI 단위 ROI 교습 dialog로 재사용 가능 |
| `FAIConfig` | `Custom/Sequence/Inspection/FAIConfig.cs` | ROI 파라미터 + 에지 파라미터 + 공차 | `ROI_Row/Col/Phi/Length1/Length2` — INI 저장됨. 변환 메서드 추가 필요. |
| `ShotConfig` | `Custom/Sequence/Inspection/ShotConfig.cs` | Shot 컨테이너 | `CameraSlaveParam` 상속, `ParamBase.Save/Load`로 INI 저장됨 |
| `InspectionRecipeManager` | `Custom/Sequence/Inspection/InspectionRecipeManager.cs` | Shot/FAI CRUD + INI 저장 | `Save(IniFile)` / `Load(IniFile)` — 이미 FAIConfig 필드 저장 |
| `InspectionViewModel` | `UI/ViewModel/InspectionViewModel.cs` | 캔버스-트리-테이블 조정 ViewModel | FAI 선택 시 ROI 하이라이트 경로 추가 필요 |
| `MainView` | `UI/ContentItem/MainView.xaml.cs` | 3영역 코드-비하인드 | `FAIResults_SelectionChanged` stub 채우기 |

### 신규 작성 대상

| 컴포넌트 | 목적 | 규모 |
|---------|------|------|
| `FAIConfig.ToRoiDefinition()` | FAIConfig의 Halcon Rectangle2 파라미터 → `RoiDefinition` 좌표 변환 | ~30 LOC |
| `InspectionViewModel.SelectedFAIRoiId` | DataGrid 선택 FAI의 ROI ID를 캔버스에 전달 | ~10 LOC |
| `FAIConfig.PixelResolutionX/Y` 저장 | 캘리브레이션 계수를 INI에 저장/로드 (ParamBase.Save/Load에 자동 포함) | 필드 2개 추가, ~5 LOC |
| 캘리브레이션 입력 UI | 간단한 TextBox 입력 dialog 또는 FAI 속성 편집 UI | ~30 LOC XAML + CS |

---

## Architecture Patterns

### 데이터 흐름 구조

```
FAIConfig (ROI_Row, ROI_Col, ROI_Phi, ROI_Length1, ROI_Length2)
    ↓ ToRoiDefinition()
RoiDefinition (Row1, Col1, Row2, Col2, IsTaught=true)
    ↓ List<RoiDefinition>
MainResultViewerControl.UpdateDisplayState(rois, ...)
    ↓
HalconDisplayService.Render(window, image, rois, selectedRoiId)
    → 캔버스에 사각형 오버레이 표시
```

```
DataGrid FAIResults_SelectionChanged
    ↓ InspectionViewModel.SelectedFAIRoiId = fai.FAIName (ROI ID)
    ↓ MainView.halconViewer.UpdateDisplayState(allFaiRois, null, null)
           with selectedRoiId = 선택된 FAI의 ROI
```

### Pattern 1: FAIConfig → RoiDefinition 변환

**What:** `FAIConfig`의 Halcon `GenMeasureRectangle2` 파라미터(중심 row/col, phi, half-lengths)를 `RoiDefinition`의 직교 좌표(Row1/Col1/Row2/Col2)로 변환.

**When to use:** 캔버스 렌더링 시 매번. 결과를 캐싱하지 않음 — 파라미터 변경 시 자동 반영.

```csharp
// FAIConfig 내부 또는 헬퍼 메서드로 추가
public RoiDefinition ToRoiDefinition()
{
    // ROI_Phi=0이면 수평 측정 Rectangle2 → 직교 bounding box
    double sinPhi = Math.Sin(ROI_Phi);
    double cosPhi = Math.Cos(ROI_Phi);
    // 4개 코너 점 계산 후 bounding box
    double halfH = ROI_Length1;
    double halfW = ROI_Length2;
    double row1 = ROI_Row - Math.Abs(halfH * cosPhi) - Math.Abs(halfW * sinPhi);
    double row2 = ROI_Row + Math.Abs(halfH * cosPhi) + Math.Abs(halfW * sinPhi);
    double col1 = ROI_Col - Math.Abs(halfH * sinPhi) - Math.Abs(halfW * cosPhi);
    double col2 = ROI_Col + Math.Abs(halfH * sinPhi) + Math.Abs(halfW * cosPhi);

    return new RoiDefinition
    {
        Id = FAIName ?? "FAI",
        Name = FAIName ?? "FAI",
        Row1 = row1,
        Column1 = col1,
        Row2 = row2,
        Column2 = col2,
        IsTaught = (ROI_Length1 > 0 && ROI_Length2 > 0),
        EdgeDirection = "LtoR",  // 기본값; Phase 3에서 정교화
        PixelResolutionX = PixelResolutionX,
        PixelResolutionY = PixelResolutionY
    };
}
```

**주의:** `ROI_Length1`, `ROI_Length2`가 모두 0이면 `IsTaught = false`로 설정하여 캔버스에서 미교습 FAI임을 표시.

### Pattern 2: MainView에서 FAI 선택 → ROI 하이라이트 연결

**What:** `FAIResults_SelectionChanged`에서 선택된 FAI의 ROI를 하이라이트.

```csharp
// MainView.xaml.cs — FAIResults_SelectionChanged stub 채우기
private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    if (_viewModel == null) return;

    var selectedRow = dataGrid_faiResults.SelectedItem as FAIResultRow;
    if (selectedRow == null) {
        halconViewer.UpdateDisplayState(GetCurrentShotRois(), null, null);
        return;
    }

    // 선택된 FAI의 ID로 selected ROI 하이라이트
    // MainResultViewerControl은 selectedRoiId 파라미터 미지원
    // → UpdateDisplayState 호출 시 선택 FAI ROI를 별도 색상 처리는
    //   HalconDisplayService.Render의 selectedRoiId 경로 활용
    var rois = GetCurrentShotRois();
    halconViewer.UpdateDisplayState(rois, null, null);
    // 참고: MainResultViewerControl은 SetSelectedRoi 미지원
    // HalconViewerControl(교습 뷰어)은 SetSelectedRoi 지원
}

private IEnumerable<RoiDefinition> GetCurrentShotRois() {
    if (_viewModel?.SelectedShot == null) return new List<RoiDefinition>();
    return _viewModel.SelectedShot.ShotConfig.FAIList
        .Select(fai => fai.ToRoiDefinition())
        .Where(roi => roi.IsTaught)
        .ToList();
}
```

**중요 발견:** `MainResultViewerControl`(검사 결과 뷰어)은 `SetSelectedRoi()` 메서드가 **없다**. `HalconViewerControl`(교습 뷰어)에만 존재. FAI 선택 하이라이트를 구현하려면 두 방법 중 하나를 선택:
- **Option A (권장):** `MainResultViewerControl.UpdateDisplayState`에 선택 ROI를 첫 번째로 배치하고, 색상은 `HalconDisplayService`의 `selectedRoiId` 파라미터를 전달하도록 `MainResultViewerControl`에 오버로드 추가.
- **Option B (최소 코드):** 선택 하이라이트 없이 전체 ROI를 동일 색상으로 표시 (D-07 요구사항 연기는 Phase 1 CONTEXT에 없으므로 구현 필요).

**D-07 재확인:** Phase 1 CONTEXT.md D-07: "테이블 행 선택 시 캔버스에서 해당 FAI의 ROI가 하이라이트 표시된다." — 이는 Phase 2에서 구현해야 한다.

### Pattern 3: 픽셀-mm 캘리브레이션 저장

**What:** `FAIConfig`에 `PixelResolutionX/Y` 필드를 추가하고 INI에 자동 저장.

```csharp
// FAIConfig.cs에 추가
[Category("Calibration")]
public double PixelResolutionX { get; set; } = 1.0;  // mm/pixel
public double PixelResolutionY { get; set; } = 1.0;  // mm/pixel
```

`ParamBase.Save/Load`는 리플렉션 기반 자동 직렬화이므로 필드 선언만으로 INI 저장/로드가 즉시 활성화된다.

**캘리브레이션 UI 선택지:**
- **Option A (기존 TeachingWindow 재사용):** TeachingWindow는 `PixelResolutionXTextBox_LostFocus` 핸들러가 이미 있어서 `RoiDefinition.PixelResolutionX/Y`를 편집한다. TeachingWindow를 FAI별로 열어 ROI + 캘리브레이션 계수를 함께 편집하는 방식이 일관성이 높다.
- **Option B (간단한 dialog):** FAI 편집 dialog에 PixelResolutionX/Y TextBox 2개 추가.
- **Option C (PropertyTools PropertyGrid):** `FAIConfig`가 `ParamBase`를 상속하므로 `[Category("Calibration")]` 어노테이션만 추가하면 기존 설정 창에서 편집 가능.

### Pattern 4: TeachingWindow를 FAI 교습에 재사용

**What:** 기존 `TeachingWindow`를 FAI 단위 ROI 드로잉에 재사용.

```csharp
// MainView 또는 InspectionViewModel에서
var tw = new TeachingWindow();
tw.LoadImage(shotVm.ShotConfig.SimulImagePath); // 또는 현재 캔버스 이미지
var seedRoi = selectedFai.ToRoiDefinition();
tw.SetTeaching(new TeachingJob {
    Rois = new List<RoiDefinition> { seedRoi }
});
tw.TeachingApplied += (s, job) => {
    if (job.Rois.Count > 0) {
        var r = job.Rois[0];
        selectedFai.ROI_Row = (r.Row1 + r.Row2) / 2.0;
        selectedFai.ROI_Col = (r.Column1 + r.Column2) / 2.0;
        selectedFai.ROI_Phi = 0.0;
        selectedFai.ROI_Length1 = (r.Row2 - r.Row1) / 2.0;
        selectedFai.ROI_Length2 = (r.Column2 - r.Column1) / 2.0;
        // PixelResolutionX/Y는 TeachingJob의 Rois[0]에서 읽어옴
        selectedFai.PixelResolutionX = r.PixelResolutionX;
        selectedFai.PixelResolutionY = r.PixelResolutionY;
        RefreshCanvas();
        RecipeManager.Save(iniFile);
    }
};
tw.ShowDialog();
```

**제약:** TeachingWindow는 `HalconViewerControl`을 내부에 가지고 있어서 독립 Window로만 표시 가능. MainView 캔버스 내 인라인 편집은 이 Phase 스코프가 아님(v2 TCH-04에서 다룸).

### Anti-Patterns to Avoid

- **FAI 오버레이를 별도 JSON으로 저장:** `FAIConfig`의 `ROI_Row/Col/Phi/Length1/Length2`는 이미 INI에 저장된다. `TeachingJob` JSON을 추가로 만들면 저장 경로가 이중화되어 동기화 문제가 발생한다. INI 하나로 통일한다.
- **RoiDefinition을 FAIConfig와 분리해서 관리:** TeachingJob JSON으로 저장하는 방식은 `TopInspectionParam` 패턴(TeachingJob JSON + INI 이중화)을 복제하는 것이다. FAIConfig는 이미 INI에 저장되므로 별도 JSON 불필요.
- **MainResultViewerControl에 SetSelectedRoi 우회:** `UpdateDisplayState`를 매번 전체 ROI 리스트로 호출하는 것은 기존 패턴과 일치하므로 사용. 선택 하이라이트는 메서드 오버로드 추가로 해결.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ROI 드로잉 UI | 직접 Canvas 마우스 이벤트 구현 | `HalconViewerControl.StartRectangleDrawing + CommitActiveRectangle` | 드래그, 이동, 리사이즈, Halcon 드로잉 오브젝트 연동 이미 완전 구현 |
| ROI → 화면 렌더링 | `HObject` 직접 그리기 | `MainResultViewerControl.UpdateDisplayState(rois, ...)` | `HalconDisplayService.Render`가 색상, 선폭, 선택 하이라이트 처리 |
| JSON 직렬화 | 커스텀 파서 | `DataContractJsonSerializer` (`TeachingStorageService`) | 이미 `TeachingJob`에 사용 중 |
| INI 파라미터 저장 | 커스텀 INI 읽기/쓰기 | `ParamBase.Save(IniFile, section)` / `Load` | 리플렉션 기반 자동 직렬화 — 필드 선언만으로 동작 |
| 픽셀-mm 변환 로직 | 거리 계산 수식 | `RoiDefinition.PixelResolutionX * pixelDistance` | 이미 `RoiDefinition`에 필드 존재; Phase 3 ALG-01에서 측정 시 적용 |
| ROI 교습 dialog | 새 Window 작성 | `TeachingWindow` 재사용 | ROI 드로잉, 에지 파라미터 편집, PixelResolution 편집 모두 포함 |

---

## Common Pitfalls

### Pitfall 1: MainResultViewerControl에는 SetSelectedRoi가 없다

**What goes wrong:** HalconViewerControl의 `SetSelectedRoi(roiId)` 메서드를 MainResultViewerControl에서 호출하려 하면 컴파일 오류.

**Why it happens:** MainResultViewerControl(검사 결과 표시 전용)과 HalconViewerControl(교습 인터랙티브 뷰어)은 별개 클래스. MainResultViewerControl은 read-only 렌더링 전용이라 ROI 선택 상태 관리 API가 없다.

**How to avoid:** D-07 ROI 하이라이트는 `MainResultViewerControl.UpdateDisplayState` 오버로드에 `selectedRoiId` 파라미터를 추가하거나, `HalconDisplayService.Render` 호출 시 selectedRoiId를 전달하도록 `MainResultViewerControl`에 오버로드 추가.

**Warning signs:** `halconViewer.SetSelectedRoi(...)` 호출 시 컴파일 오류 — `halconViewer`가 `MainResultViewerControl` 타입이기 때문.

### Pitfall 2: FAIConfig ROI 파라미터는 Rectangle2 형식 (중심점 + half-lengths + 회전)

**What goes wrong:** `ROI_Row/Col`이 중심점, `ROI_Length1/2`가 반쪽 길이임을 모르고 `Row1/Col1`로 직접 사용하면 오버레이가 원래 위치와 절반 크기로 표시됨.

**Why it happens:** Halcon `GenMeasureRectangle2` API는 `(row, col, phi, halfLength1, halfLength2)` 시그니처. MeasurementAlgorithm.cs에서 동일 파라미터 사용.

**How to avoid:** `ToRoiDefinition()` 변환 시 반드시 `row1 = ROI_Row - ROI_Length1`, `col1 = ROI_Col - ROI_Length2` 형태로 변환 (phi=0 기준). phi != 0일 때는 회전 변환 필요.

**Warning signs:** FAI 오버레이가 실제 측정 위치보다 작게 표시되거나 이미지 좌상단에 치우쳐 보임.

### Pitfall 3: ParamBase.Save/Load는 public 프로퍼티만 직렬화

**What goes wrong:** `PixelResolutionX/Y`에 `[Browsable(false)]`만 붙이고 접근자를 `private set`으로 바꾸면 INI 저장이 안 됨.

**Why it happens:** `ParamBase`의 리플렉션 기반 직렬화는 `public get/set` 프로퍼티만 대상으로 함.

**How to avoid:** `PixelResolutionX/Y`를 `public double PixelResolutionX { get; set; } = 1.0;` 형태로 유지. UI에 표시하기 싫으면 `[Browsable(false)]`만 추가.

### Pitfall 4: FAI ROI 교습 중 캔버스 이미지 없는 경우

**What goes wrong:** `HalconViewerControl.StartRectangleDrawing()`은 `CurrentImage == null`이면 아무 동작 없이 리턴. 사용자에게 피드백 없음.

**Why it happens:** `HalconViewerControl`의 드로잉 모드는 이미지 로드 후에만 동작.

**How to avoid:** 교습 버튼 클릭 전 `ShotConfig.HasImage` 체크. 이미지 없으면 `CustomMessageBox`로 "이미지를 먼저 로드하거나 Grab하세요" 안내.

### Pitfall 5: INI 저장 타이밍 - 교습 완료 후 즉시 저장

**What goes wrong:** ROI 편집 후 앱 재시작 시 데이터 손실.

**Why it happens:** `InspectionRecipeManager.Save(IniFile)`가 명시적으로 호출되어야 저장됨. UI에서 자동 저장 없음.

**How to avoid:** 교습 dialog의 "적용" 버튼 콜백에서 `InspectionRecipeManager.Save(IniFile)` 호출. 저장 파일 경로는 `SystemHandler.Handle.Setting.RecipeSavePath + "/" + CurrentRecipeName + ".ini"` 패턴.

---

## Code Examples

### FAIConfig → RoiDefinition 변환 (phi=0 단순 케이스)

```csharp
// FAIConfig.cs에 추가 (phi != 0 일반 케이스 포함)
public RoiDefinition ToRoiDefinition()
{
    bool isTaught = ROI_Length1 > 0 && ROI_Length2 > 0;
    if (!isTaught)
    {
        return new RoiDefinition
        {
            Id = FAIName ?? "FAI",
            Name = FAIName ?? "FAI",
            IsTaught = false
        };
    }

    // Rectangle2: 중심(Row, Col), 회전(Phi), 반축(Length1=세로, Length2=가로)
    double sinPhi = Math.Sin(ROI_Phi);
    double cosPhi = Math.Cos(ROI_Phi);
    double dRow = Math.Abs(ROI_Length1 * cosPhi) + Math.Abs(ROI_Length2 * sinPhi);
    double dCol = Math.Abs(ROI_Length1 * sinPhi) + Math.Abs(ROI_Length2 * cosPhi);

    return new RoiDefinition
    {
        Id = FAIName ?? "FAI",
        Name = FAIName ?? "FAI",
        Row1 = ROI_Row - dRow,
        Column1 = ROI_Col - dCol,
        Row2 = ROI_Row + dRow,
        Column2 = ROI_Col + dCol,
        IsTaught = true,
        Sigma = Sigma,
        EdgeThreshold = (int)Threshold,
        EdgeDirection = "LtoR",
        PixelResolutionX = PixelResolutionX,
        PixelResolutionY = PixelResolutionY
    };
}
```

### MainView FAIResults_SelectionChanged 구현

```csharp
// MainView.xaml.cs — FAIResults_SelectionChanged stub 교체
private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    if (_viewModel?.SelectedShot == null) return;

    var allRois = _viewModel.SelectedShot.ShotConfig.FAIList
        .Select(fai => fai.ToRoiDefinition())
        .Where(roi => roi.IsTaught)
        .ToList();

    halconViewer.UpdateDisplayState(allRois, null, null);
    // D-07 하이라이트: MainResultViewerControl 오버로드 추가 후 selectedRoiId 전달
}
```

### FAI 교습 dialog 실행 패턴

```csharp
// MainView 또는 InspectionViewModel에서
private void OpenFAITeachingDialog(FAINodeViewModel faiVm, ShotNodeViewModel shotVm) {
    var tw = new TeachingWindow();

    // 현재 Shot 이미지 로드
    HImage img = shotVm.ShotConfig.GetImage();
    if (img != null) {
        string tempPath = HalconTeachingHelper.SaveTempImage(shotVm.Name, img);
        if (!string.IsNullOrWhiteSpace(tempPath))
            tw.LoadImage(tempPath);
        img.Dispose();
    }

    // 기존 ROI 시드로 초기화
    tw.SetTeaching(new TeachingJob {
        JobName = faiVm.Name,
        Rois = new System.Collections.Generic.List<RoiDefinition> {
            faiVm.FAIConfig.ToRoiDefinition()
        }
    });

    tw.TeachingApplied += (s, job) => {
        if (job?.Rois?.Count > 0) {
            var r = job.Rois[0];
            var fai = faiVm.FAIConfig;
            fai.ROI_Row = (r.Row1 + r.Row2) / 2.0;
            fai.ROI_Col = (r.Column1 + r.Column2) / 2.0;
            fai.ROI_Phi = 0.0;
            fai.ROI_Length1 = (r.Row2 - r.Row1) / 2.0;
            fai.ROI_Length2 = (r.Column2 - r.Column1) / 2.0;
            fai.PixelResolutionX = r.PixelResolutionX;
            fai.PixelResolutionY = r.PixelResolutionY;
            // MainView 캔버스 갱신
            RefreshCanvasForShot(shotVm);
        }
    };

    tw.ShowDialog();
}
```

### MainResultViewerControl에 selectedRoiId 오버로드 추가 예시

```csharp
// MainResultViewerControl.xaml.cs에 추가
public void UpdateDisplayState(
    IEnumerable<RoiDefinition> rois,
    IEnumerable<EdgeInspectionOverlay> overlays,
    IEnumerable<string> messages,
    string selectedRoiId = null)
{
    // ... 기존 UpdateDisplayState 로직 그대로 ...
    // Render() 호출 시 selectedRoiId를 _displayService.Render에 전달
    _selectedRoiId = selectedRoiId;
    Render(); // Render()가 _selectedRoiId를 HalconDisplayService.Render에 전달
}
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| TeachingWindow에서 직접 JSON 저장 | FAIConfig ROI 필드는 INI에 ParamBase.Save/Load로 저장 | JSON 별도 저장 불필요 |
| 고정 탭별 ROI (5탭 구조) | InspectionRecipeManager의 동적 Shot/FAI 구조 | Phase 2에서 FAI별 ROI 교습 가능 |
| 글로벌 단일 캘리브레이션 계수 | RoiDefinition 레벨의 PixelResolutionX/Y | FAI별 다른 캘리브레이션 가능 |

**현재 상태 (Phase 1 이후):**
- `FAIResults_SelectionChanged`: stub (Phase 2에서 채워야 함)
- `FAIConfig.PixelResolutionX/Y`: 미존재 (추가 필요)
- `FAIConfig.ToRoiDefinition()`: 미존재 (추가 필요)
- `TeachingWindow`: 완전 동작 (재사용 가능)
- `HalconViewerControl.StartRectangleDrawing`: 완전 동작

---

## Open Questions

1. **D-07 하이라이트 방법 선택**
   - What we know: MainResultViewerControl에 SetSelectedRoi 없음. HalconDisplayService.Render는 selectedRoiId 지원.
   - What's unclear: MainResultViewerControl에 오버로드 추가(코드 변경) vs. 선택 FAI만 별도 색상 없이 표시(간단하지만 D-07 미완족).
   - Recommendation: MainResultViewerControl에 selectedRoiId 파라미터 오버로드 추가. ~15 LOC, 기존 Render() 경로 재사용.

2. **캘리브레이션 UI 진입점**
   - What we know: TeachingWindow는 PixelResolutionX/Y 편집 UI 이미 있음. FAI별로 열기 가능.
   - What's unclear: "캘리브레이션 실행" 버튼을 어디에 배치할지 (툴바? 우클릭 컨텍스트 메뉴? TeachingWindow 내 별도 섹션?).
   - Recommendation: TreeView 툴바에 "교습(T)" 버튼 추가. 선택된 FAI가 있을 때 TeachingWindow를 열어서 ROI + 캘리브레이션 계수를 함께 설정. ALG-03 요구사항("캘리브레이션 실행 후 계수가 시스템에 적용")은 TeachingApplied 콜백에서 `FAIConfig.PixelResolutionX/Y` 갱신 + `InspectionRecipeManager.Save`로 충족.

3. **ShotConfig 레벨 공통 캘리브레이션 vs. FAI 레벨 개별 캘리브레이션**
   - What we know: `RoiDefinition.PixelResolutionX/Y`는 ROI별 설정. 실제 카메라는 Shot 단위로 동일 해상도를 가짐.
   - What's unclear: 모든 FAI가 동일 Shot 이미지에서 측정되므로 ShotConfig 레벨 공통 계수가 더 자연스러울 수 있음.
   - Recommendation: Phase 2에서는 FAIConfig 레벨로 구현 (변경 최소화). Phase 3 ALG-01 측정 구현 시 ShotConfig 레벨로 리팩터링 여부 결정.

---

## Environment Availability

Step 2.6: 이 Phase는 코드/설정 변경 전용 (외부 도구 의존성 없음). SKIPPED.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | 없음 — 프로젝트에 xUnit/NUnit/MSTest 없음 (CLAUDE.md 확인) |
| Config file | 해당 없음 |
| Quick run command | 수동 빌드: `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` |
| Full suite command | 동일 (테스트 프레임워크 없음) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | Note |
|--------|----------|-----------|-------------------|------|
| TCH-01 | FAI ROI 오버레이가 캔버스에 표시된다 | manual-only | — | WPF UI 렌더링은 자동화 불가; 시각 확인 필요 |
| TCH-01 | `FAIConfig.ToRoiDefinition()`가 올바른 좌표를 반환한다 | unit | 수동 ConsoleApp 또는 즉석 테스트 | phi=0 케이스: Row1 = ROI_Row - ROI_Length1 등 검증 |
| TCH-02 | 저장 후 재시작해도 ROI 데이터 유지 | manual-only | — | INI 파일 파싱 후 FAIConfig 값 확인 |
| ALG-03 | PixelResolutionX/Y 값이 INI에 저장/로드된다 | manual-only | — | INI 파일 열어 [SHOT_0_FAI_0] 섹션에 PixelResolutionX 키 확인 |
| ALG-03 | 캘리브레이션 후 값이 FAIConfig에 적용된다 | manual-only | — | TeachingWindow 적용 후 FAIConfig.PixelResolutionX 값 확인 |

### Sampling Rate

- **빌드 게이트:** `msbuild ... /p:Configuration=Debug` — 각 작업 커밋 전
- **기능 검증:** 수동 실행 — Shot 이미지 로드 → FAI ROI 표시 → 저장 → 재시작 → ROI 유지 확인
- **Phase gate:** 위 Success Criteria 3개 모두 수동 확인 후 `/gsd:verify-work`

### Wave 0 Gaps

자동화 테스트 프레임워크 미존재로 인해 Wave 0 테스트 파일 생성 없음. 기능 검증은 수동 확인으로 수행.

---

## Project Constraints (from CLAUDE.md)

- C# 7.2 — `switch expression`, `record`, nullable reference type 사용 불가
- .NET Framework 4.8 — `System.Text.Json` 사용 불가 (`DataContractJsonSerializer` 사용)
- Halcon 24.11 — `HOperatorSet.*` 호출은 `try/catch { return false; }` 래핑 필수
- `HImage`는 `using` 또는 `Dispose()` 필수
- private 필드: `_camelCase` (새 코드), `pPascalCase` (레거시 Action 클래스)
- 에러 전파: `FinishAction(EContextResult.Error)` 패턴, `throw` 금지
- `Browsable(false)` 어노테이션: `[PropertyTools.DataAnnotations.Browsable(false)]`
- INI 저장: `ParamBase.Save/Load` 리플렉션 방식 — `public` 프로퍼티만 직렬화됨
- `SIMUL_MODE` 조건부 컴파일 심볼 — Debug 빌드에서 활성화
- GSD 워크플로 — 파일 변경 전 GSD 커맨드로 진입

---

## Sources

### Primary (HIGH confidence)

- 직접 코드 검토: `WPF_Example/Halcon/Display/HalconDisplayService.cs` — `Render(window, image, rois, selectedRoiId)` 시그니처 확인
- 직접 코드 검토: `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — `StartRectangleDrawing`, `CommitActiveRectangle`, `SetSelectedRoi`, `SetRois` API 확인
- 직접 코드 검토: `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — `UpdateDisplayState` 시그니처 확인, `SetSelectedRoi` 부재 확인
- 직접 코드 검토: `WPF_Example/UI/Dialog/TeachingWindow.xaml.cs` — `PixelResolutionX/Y` 편집 UI 이미 포함 확인
- 직접 코드 검토: `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — `ROI_Row/Col/Phi/Length1/Length2` Rectangle2 파라미터 구조 확인
- 직접 코드 검토: `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs` — `GenMeasureRectangle2` 호출에서 `roi.Row1/Col1` 아닌 `SmallestRectangle2`로 중심 계산하는 패턴 확인
- 직접 코드 검토: `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `FAIResults_SelectionChanged` stub 확인
- 직접 코드 검토: `WPF_Example/Halcon/Models/RoiDefinition.cs` — `PixelResolutionX/Y` 필드 이미 존재 확인

### Secondary (MEDIUM confidence)

- CLAUDE.md 프로젝트 지침 — 코딩 컨벤션, 에러 처리 패턴
- `.planning/phases/01-ui/01-RESEARCH.md` — Phase 1 결정 사항 확인

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 모든 재사용 컴포넌트를 소스에서 직접 확인
- Architecture: HIGH — FAIConfig Rectangle2 파라미터, MainResultViewerControl API 직접 확인
- Pitfalls: HIGH — MainResultViewerControl.SetSelectedRoi 부재를 소스에서 직접 확인

**Research date:** 2026-04-03
**Valid until:** 2026-05-03 (안정적 코드베이스; Halcon API는 변경 없음)
