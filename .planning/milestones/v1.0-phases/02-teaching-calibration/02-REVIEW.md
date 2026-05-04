---
status: issues_found
phase: 02-teaching-calibration
depth: standard
files_reviewed: 7
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
reviewed: 2026-04-08
---

# Code Review: Phase 02 (teaching-calibration)

## Summary

7 files reviewed at standard depth. No critical issues. 3 warnings and 2 informational items found.

## Findings

### WR-01: DispatcherTimer 미해제 — MainView.xaml.cs:659

**Severity:** warning
**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`
**Line:** 659

`FinishCalibration()`에서 `DispatcherTimer`를 생성하고 3초 후 정지하지만, `timer.Stop()` 후 `timer = null` 처리가 없다. 단발성이므로 GC가 수거하겠지만, 연속 캘리브레이션 시 타이머가 겹칠 수 있다 (이전 타이머가 아직 Tick하기 전에 새로 생성).

**Recommendation:** 클래스 필드로 `_calibrationTimer`를 두고, 새 타이머 생성 전 기존 타이머를 `Stop()` 처리.

### WR-02: Polygon 마우스 이벤트 중복 구독 가능성 — MainView.xaml.cs:545-546

**Severity:** warning
**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`
**Lines:** 545-546

`PolygonRoiButton_Click`에서 `MouseLeftButtonDown += HalconViewer_PolygonMouseDown`을 호출하기 전에 `ExitCanvasMode()`로 구독 해제하지만, `ExitCanvasMode()` 후 바로 `btn_polygonRoi.IsChecked = true`를 설정하고 다시 `+=`를 호출한다. 빠른 연속 클릭 시 `ExitCanvasMode` → `+=` 순서에 따라 중복 구독이 될 수 있다.

**Recommendation:** `+=` 전에 항상 `-=`를 방어적으로 호출 (이미 `ExitCanvasMode`에서 하지만, 타이밍 문제 방지용).

### WR-03: RoiDefinition 참조 누수 — MainResultViewerControl.xaml.cs:150-151

**Severity:** warning  
**File:** `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`
**Line:** 150-151

`UpdateDisplayState`에서 `rois.Select(roi => roi.Clone())`으로 복사하지만, `_rectDraftRoi`는 `RenderNow`에서 직접 참조한다. 외부에서 `_rectDraftRoi`의 필드를 변경하면 렌더링에 영향을 줄 수 있다. 다만 현재 코드에서는 항상 새 인스턴스를 할당하므로 실질적 위험은 낮다.

**Recommendation:** 현재 패턴 유지 (low risk).

### IR-01: FAIConfig.ToRoiDefinition() 에지 방향 기본값 — FAIConfig.cs:112

**Severity:** info
**File:** `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs`
**Line:** 112

`ToRoiDefinition()`에서 `EdgeDirection = "LtoR"`를 하드코딩한다. FAIConfig에 EdgeDirection 필드가 없어서 기본값을 사용하는 것은 맞지만, 향후 Phase에서 EdgeDirection을 FAIConfig에 추가할 때 이 라인을 업데이트해야 한다.

**Recommendation:** Phase 3 이후에 EdgeDirection 필드 추가 시 연동 필요.

### IR-02: DrawDirectionArrow 화살표 각도 상수 — HalconDisplayService.cs:232-233

**Severity:** info
**File:** `WPF_Example/Halcon/Display/HalconDisplayService.cs`
**Lines:** 232-233

화살표 꼭지 각도에 `2.5` 라디안(≈143°)을 사용한다. 시각적으로 의도된 값이지만, `Math.PI * 5/6` (≈150°) 같은 명시적 표현이 가독성에 좋다.

**Recommendation:** 동작에 문제 없음. 가독성 개선은 선택적.

## Files Reviewed

| File | Lines | Issues |
|------|-------|--------|
| FAIConfig.cs | 119 | 1 info |
| HalconDisplayService.cs | 254 | 1 info |
| CameraSlaveParam.cs | 240 | 0 |
| MainResultViewerControl.xaml.cs | 938 | 1 warning |
| MainView.xaml | ~200 | 0 |
| MainView.xaml.cs | 697 | 2 warnings |
| FAIResultRow.cs | 50 | 0 |

---
*Reviewed: 2026-04-08 | Depth: standard*
