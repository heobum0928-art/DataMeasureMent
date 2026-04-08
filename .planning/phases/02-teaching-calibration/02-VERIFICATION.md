---
phase: 02-teaching-calibration
verified: 2026-04-08T13:00:00Z
status: human_needed
score: 9/9 must-haves verified
re_verification: false
human_verification:
  - test: "Rect ROI 드래그 동작 확인"
    expected: "Rect ROI 버튼 클릭 후 캔버스에서 마우스를 드래그하면 파란 점선 사각형이 그려지고, 마우스를 떼면 FAIConfig.ROI_* 필드에 저장된다"
    why_human: "HWindowControlWPF 마우스 이벤트 체인 (StartRectangleDrawing → 드래그 → CommitActiveRectangle) 은 실제 WPF 런타임 없이 검증 불가"
  - test: "Polygon ROI 점 클릭 + 우클릭 완성 동작 확인"
    expected: "Polygon ROI 버튼 클릭 후 캔버스에서 점을 클릭하면 파란 점이 렌더링되고, 우클릭 시 폴리곤이 완성되어 FAIConfig.PolygonPoints에 세미콜론 구분 문자열로 저장된다"
    why_human: "마우스 좌표 → 이미지 좌표 변환(PointerInfoChanged)이 올바르게 동작하는지 실제 이미지 좌표 계산은 런타임 검증 필요"
  - test: "2점 캘리브레이션 플로우 확인"
    expected: "Calibrate 버튼 클릭 후 캔버스에서 2점을 클릭하면 mm 입력 다이얼로그가 표시되고, 값 입력 후 CameraSlaveParam.PixelResolution에 mm/pixel 값이 저장된다"
    why_human: "TextInputBoxWinidow 다이얼로그 ShowDialog() 호출과 입력값 파싱은 런타임 검증 필요"
  - test: "ROI 하이라이트 색상 확인"
    expected: "DataGrid에서 FAI 행 선택 시 해당 ROI가 노란색(linewidth 3)으로, 다른 ROI는 초록색(linewidth 2)으로 표시된다"
    why_human: "HalconDisplayService 색상 렌더링은 HWindow 런타임 없이 시각 확인 불가"
  - test: "에지 방향 화살표 표시 확인"
    expected: "선택된 ROI 중앙에 EdgeDirection 값에 따른 흰색 화살표(본선 + 화살머리 2선)가 표시된다"
    why_human: "DrawDirectionArrow의 수학 계산(Atan2/Sin/Cos)과 HWindow 렌더링 결과는 시각 확인 필요"
---

# Phase 02: Teaching + Calibration Verification Report

**Phase Goal:** 사용자가 캔버스에서 FAI ROI를 시각적으로 확인하고, ROI 설정을 저장/로드하며, 픽셀-mm 변환을 위한 캘리브레이션을 수행할 수 있다
**Verified:** 2026-04-08T13:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | FAI 선택 시 캔버스에 해당 FAI의 ROI가 노란색으로 하이라이트 표시된다 | ✓ VERIFIED | `MainView.FAIResults_SelectionChanged` → `halconViewer.UpdateDisplayState(rois, selectedRoiId, null, null)` 호출 확인; HalconDisplayService.Render에서 `roi.Id == selectedRoiId ? "yellow" : "green"` 분기 확인 |
| 2  | 미선택 FAI의 ROI는 초록색으로 표시된다 | ✓ VERIFIED | HalconDisplayService.Render 색상 분기 로직 동일 코드에서 확인 |
| 3  | ROI 중앙에 에지 검색 방향 화살표(흰색)가 표시된다 | ✓ VERIFIED | `DrawDirectionArrow` 메서드 존재, Render 루프에서 `roi.Id == selectedRoiId && roi.IsTaught` 조건으로 호출 확인 (line 47-50) |
| 4  | ROI가 설정되지 않은 FAI 선택 시 'ROI not set' 텍스트가 표시된다 | ✓ VERIFIED | `MainView.FAIResults_SelectionChanged`에서 `ROI_Length1 <= 0 || ROI_Length2 <= 0` 조건 시 `label_message.Content = "ROI not set"` 설정 확인 |
| 5  | PixelResolution 필드가 INI에 저장/로드된다 | ✓ VERIFIED | `CameraSlaveParam.PixelResolution` public property 확인 (line 26); ParamBase 하위 클래스의 public double property는 자동 INI 직렬화 |
| 6  | PolygonPoints 필드가 INI에 저장/로드된다 | ✓ VERIFIED | `FAIConfig.PolygonPoints` public string property 확인 (line 39); ParamBase 하위 클래스의 public string property는 자동 INI 직렬화 |
| 7  | 캔버스 상단에 Rect ROI / Polygon ROI / Calibrate 버튼이 있는 툴바가 표시된다 | ✓ VERIFIED | `MainView.xaml`에 `canvasToolbar` Border, `btn_rectRoi`, `btn_polygonRoi`, `btn_calibrate` 버튼 + `RoiToggleButtonStyle` 리소스 확인 |
| 8  | Rect ROI 드래그 후 FAIConfig에 저장된다 | ✓ VERIFIED | `RectRoiButton_Click` → `StartRectangleDrawing()` 호출; `CommitRectRoi()` → `CommitActiveRectangle()` → FAIConfig.ROI_* 필드 저장 (line 514-515) 확인 |
| 9  | Escape 키 입력 시 드로잉 모드가 취소되고 툴바 버튼이 idle 상태로 돌아간다 | ✓ VERIFIED | `MainView_PreviewKeyDown`에서 `Key.Escape` 감지 → `ExitCanvasMode()` 호출; `ExitCanvasMode()`에서 `_canvasMode = ECanvasMode.None`, `btn_rectRoi.IsChecked = false` 등 초기화 확인 |

**Score:** 9/9 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` | ToRoiDefinition(), PolygonPoints, PixelResolutionX/Y | ✓ VERIFIED | 모든 필드 및 메서드 존재 확인; sin/cos backward-compat 주석 포함 |
| `WPF_Example/Sequence/Param/CameraSlaveParam.cs` | PixelResolution (mm/pixel) | ✓ VERIFIED | `public double PixelResolution { get; set; } = 1.0` 확인 |
| `WPF_Example/UI/ViewModel/FAIResultRow.cs` | SourceFAI property | ✓ VERIFIED | `public FAIConfig SourceFAI => _fai;` 확인 |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | UpdateDisplayState 4-arg overload, _selectedRoiId 필드 | ✓ VERIFIED | 오버로드와 필드 모두 확인; RenderNow()에서 `_selectedRoiId` 전달 확인 |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | DrawDirectionArrow, RenderPolygon, RenderPolygonPoints | ✓ VERIFIED | 3개 메서드 모두 존재; Render 루프에서 DrawDirectionArrow 호출 확인 |
| `WPF_Example/UI/ContentItem/MainView.xaml` | canvasToolbar, Rect ROI/Polygon ROI/Calibrate 버튼 | ✓ VERIFIED | XAML에서 모든 요소 확인; RoiToggleButtonStyle 리소스 공유 패턴 적용 |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | ECanvasMode, FAIResults_SelectionChanged, GetCurrentFAIRois, ExitCanvasMode | ✓ VERIFIED | 모든 요소 존재; Polygon/Calibration 핸들러도 확인 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `FAIConfig.ToRoiDefinition()` | `RoiDefinition` | Rectangle2 bounding box 변환 (sin/cos ROI_Phi) | ✓ WIRED | 메서드 구현 및 `new RoiDefinition { ... }` 반환 확인 |
| `MainView.FAIResults_SelectionChanged` | `MainResultViewerControl.UpdateDisplayState` | FAI 선택 이벤트 → rois + selectedRoiId 전달 | ✓ WIRED | `halconViewer.UpdateDisplayState(rois, selectedRoiId, null, null)` 호출 확인 |
| `HalconDisplayService.Render` | `DrawDirectionArrow` | ROI 렌더링 루프에서 선택 ROI에 화살표 추가 | ✓ WIRED | `if (roi.Id == selectedRoiId && roi.IsTaught) DrawDirectionArrow(window, roi)` 확인 |
| `FAIResultRow.SourceFAI` | `FAIConfig` | DataGrid row → ToRoiDefinition() + ROI 필드 접근 | ✓ WIRED | `selectedRow.SourceFAI` 사용처 다수 확인 (SelectionChanged, CommitRectRoi 등) |
| `Rect ROI button click` | `StartRectangleDrawing` | MainView code-behind → halconViewer.StartRectangleDrawing() | ✓ WIRED | `halconViewer.StartRectangleDrawing()` 호출 확인 |
| `Polygon right-click complete` | `FAIConfig.PolygonPoints` | Point list → semicolon-delimited string | ✓ WIRED | `_editingFai.PolygonPoints = sb.ToString()` 확인 (StringBuilder 포맷: "x,y;x,y;...") |
| `Calibration 2-point click` | `CameraSlaveParam.PixelResolution` | pixel distance / mm input = mm/pixel | ✓ WIRED | `shot.PixelResolution = mmPerPixel` 확인 (line 685); FAIConfig.PixelResolutionX/Y도 동기화 확인 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `MainView.xaml.cs` GetCurrentFAIRois() | `dataGrid_faiResults.Items` | DataGrid ItemsSource (FAIResultRow 컬렉션) | DataGrid 바인딩 → `row.SourceFAI.ToRoiDefinition()` | ✓ FLOWING |
| `CameraSlaveParam.PixelResolution` | `mmPerPixel` 계산 | 2점 픽셀 거리 + TextInputBoxWinidow mm 입력 | 사용자 입력 기반 실수 계산 | ✓ FLOWING |
| `FAIConfig.PolygonPoints` | `_polygonPoints` 리스트 | HalconViewer 마우스 클릭 → PointerInfoChanged 좌표 | 런타임 마우스 이벤트 | ? RUNTIME_ONLY |

### Behavioral Spot-Checks

Step 7b: SKIPPED — WPF UI 컨트롤이 중심이며 HWindowControlWPF 없이는 실행 불가. 런타임 동작 확인은 Human Verification으로 위임.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| TCH-01 | 02-01, 02-02 | 캔버스에 FAI ROI 오버레이를 시각적으로 표시한다 (Edge 방향, 범위) | ✓ SATISFIED | HalconDisplayService.DrawDirectionArrow 구현; selectedRoiId 기반 yellow/green 색상 분기; MainView.FAIResults_SelectionChanged 와이어링 |
| TCH-02 | 02-01 | TeachingStorageService를 통해 ROI 데이터를 저장/로드한다 | ✓ SATISFIED (PARTIAL) | FAIConfig.PolygonPoints + PixelResolutionX/Y + CameraSlaveParam.PixelResolution이 ParamBase.Save/Load를 통해 INI 직렬화됨. TeachingStorageService 직접 연동은 Phase 3 이후; INI 기반 저장은 완료 |
| ALG-03 | 02-02 | 픽셀→mm 변환을 위한 캘리브레이션 기능을 제공한다 | ✓ SATISFIED | CalibrateButton_Click → 2점 클릭 → TextInputBoxWinidow mm 입력 → `shot.PixelResolution = mmPerPixel` 저장; FAIConfig.PixelResolutionX/Y 동기화 |

**Notes on TCH-02:** REQUIREMENTS.md 기술 내용은 "TeachingStorageService를 통해"이지만, Plan 02-01의 `requirements: [TCH-01, TCH-02]` 범위는 FAIConfig 필드의 INI 직렬화(ParamBase 기반)로 구현되었다. TeachingStorageService 직접 연동은 Phase 3의 알고리즘 연동 시 완성 예정. 현재 범위 내 INI 저장/로드 메커니즘은 완전히 구현됨.

**Orphaned Requirements:** 없음. Phase 2에 매핑된 TCH-01, TCH-02, ALG-03 모두 계획에서 클레임됨.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `MainView.xaml.cs` | ~583 | `_editingFai.PolygonPoints = sb.ToString()` — StringBuilder가 i=0 체크 없이 항상 ";" 구분자 시작 가능성 | ℹ️ Info | 포맷은 "x,y;x,y" 형식으로 i>0 조건 존재할 경우 문제 없음; 실제 코드 확인 결과 i>0 가드 존재 |
| `MainView.xaml.cs` | ~685 | `shot.PixelResolution = mmPerPixel` — shot이 ShotConfig 타입 캐스팅 실패 시 null 참조 위험 | ⚠️ Warning | 캐스팅 실패 시 예외 없이 건너뜀. 현재 DataGrid 소스가 항상 ShotConfig여서 실제 위험 낮음 |

### Human Verification Required

#### 1. Rect ROI 드래그 동작

**Test:** 애플리케이션 실행 후 MainView에서 FAI 행 선택 → Rect ROI 버튼 클릭 → 캔버스에서 마우스 드래그
**Expected:** 드래그 중 파란 점선 사각형 표시, 마우스 업 후 FAIConfig.ROI_Row/Col/Length1/Length2 업데이트, 캔버스에 초록 ROI 표시
**Why human:** HWindowControlWPF 마우스 이벤트와 이미지 좌표 변환은 WPF 런타임 없이 검증 불가

#### 2. Polygon ROI 점 클릭 + 우클릭 완성

**Test:** Polygon ROI 버튼 클릭 → 캔버스에서 3회 이상 좌클릭 → 우클릭
**Expected:** 클릭 시 파란 점 렌더링, 우클릭 시 폴리곤 완성 + FAIConfig.PolygonPoints에 "x,y;x,y;x,y" 형식 저장
**Why human:** PointerInfoChanged로 이미지 좌표 추적하는 방식은 실제 HWindow 인스턴스 필요

#### 3. 2점 캘리브레이션 플로우

**Test:** Calibrate 버튼 클릭 → 캔버스에서 2점 클릭 → mm 입력 다이얼로그에 값 입력 → 확인
**Expected:** CameraSlaveParam.PixelResolution 값이 mm / 픽셀거리로 업데이트됨
**Why human:** TextInputBoxWinidow 다이얼로그 Show/OK 플로우는 런타임 검증 필요

#### 4. ROI 시각적 하이라이트 품질

**Test:** DataGrid에서 여러 FAI 행을 순차 선택
**Expected:** 선택 ROI는 노란색 굵은 테두리, 비선택 ROI는 초록 얇은 테두리, 선택 ROI 중앙에 흰 화살표
**Why human:** 색상/굵기/화살표 방향 정확성은 시각 확인 필요

### Gaps Summary

자동화 검증 범위 내 갭 없음. 9/9 truths가 소스 코드 레벨에서 VERIFIED됨.

남은 불확실성은 모두 런타임 동작(WPF HWindow 렌더링, 마우스 이벤트 좌표 변환, 다이얼로그 플로우)에 관한 것으로, 코드 구조상 올바르게 구현되어 있으나 실제 UI 동작은 Human Verification이 필요하다.

TCH-02의 TeachingStorageService 직접 연동은 Plan 범위 밖(Phase 3 이후)이며, 계획된 INI 기반 저장/로드는 완전히 구현됨.

---

_Verified: 2026-04-08T13:00:00Z_
_Verifier: Claude (gsd-verifier)_
