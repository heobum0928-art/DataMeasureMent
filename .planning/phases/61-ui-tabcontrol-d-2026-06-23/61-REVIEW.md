---
phase: 61-ui-tabcontrol-d-2026-06-23
reviewed: 2026-06-24T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - WPF_Example/Custom/UI/TrayVisionView.xaml
  - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
  - WPF_Example/Custom/UI/BottomVisionView.xaml
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
  - WPF_Example/Custom/UI/MainWindow.cs
  - WPF_Example/MainWindow.xaml
  - WPF_Example/MainWindow.xaml.cs
  - WPF_Example/DatumMeasurement.csproj
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
status: issues_found
---

# Phase 61: Code Review Report

**Reviewed:** 2026-06-24
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Phase 61 신규 파일(TrayVisionView, BottomVisionView, Custom/UI/MainWindow.cs) 및 변경된 MainWindow.xaml/MainWindow.xaml.cs, DatumMeasurement.csproj를 리뷰했다.

전체적으로 thin facade 설계 의도가 잘 지켜져 있으며, airspace 분리, WPF 단일 부모 제약, 이벤트 중복 구독 방지 로직이 모두 존재한다. 크리티컬 버그는 없다.

발견된 이슈는 3가지 Warning과 2가지 Info다:
- WR-01: `TrayVisionView.AttachSharedViewer`는 이전 `_viewer`의 `CircleDrawingCompleted` 구독을 해제하지 않아, `BottomVisionView`와 달리 뷰어 재부착 시 이벤트가 누적된다. (TrayVisionView는 CircleDrawingCompleted를 쓰지 않으므로 실제 영향은 없지만, `BottomVisionView.AttachSharedViewer`가 동일 객체를 여러 번 받으면 이전 `_viewer`에 걸린 구독이 해제되지 않는 구조적 결함이 있다.)
- WR-02: `ValidateRois`가 `halfW/halfH` 음수(Row2 < Row1 또는 Column2 < Column1) 케이스를 통과시킨다. 역방향 드래그 시 음수 값이 `< MIN_ROI_HALF_LENGTH(1.0)` 조건을 통과하여 HALCON에 음수 Length 파라미터가 전달될 수 있다.
- WR-03: `Release|x64` 빌드 설정에 `Prefer32Bit=true`가 남아있다. x64 강제 플랫폼에서 `Prefer32Bit`는 무효화되지만, 설정 불일치는 다른 빌드 설정과 일관성이 없고 혼란을 줄 수 있다.
- IN-01: `BottomVisionView`가 `Unloaded` 이벤트에서 `CircleDrawingCompleted` 구독을 해제하지 않는다. `_viewer`가 교체되지 않는 한 누수는 없지만, `RefreshEthernetVisionTabs`가 `None→Tray→Bottom` 순으로 여러 번 호출되면 `AttachSharedViewer`가 재호출되고 기존 `_viewer` 참조 없이 새 `_viewer`로 교체되어 이전 구독이 떠 있을 수 있다.
- IN-02: `Release|x64` 빌드에 `LangVersion` 태그가 없다. `Debug|AnyCPU`/`Debug|x64`는 명시적으로 `7.2`로 고정되어 있지만 `Release|x64`는 누락되어 있다(컴파일러 기본값에 의존).

---

## Warnings

### WR-01: BottomVisionView.AttachSharedViewer — 이전 _viewer 구독 해제 누락

**File:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs:56-67`

**Issue:** `AttachSharedViewer`는 새 `viewer`에 `CircleDrawingCompleted`를 구독(-= 후 +=)하지만, 이전 `_viewer`(교체 전 객체)에 걸린 구독을 해제하지 않는다. `RefreshEthernetVisionTabs`가 `BottomVisionView`에 동일 `_alignViewer`를 두 번 이상 전달하면 현재 `-=` 로직으로 중복 방지가 되지만, 만약 향후 `_alignViewer`가 재생성되어 다른 인스턴스가 전달될 경우 이전 인스턴스에 구독이 남는다. 현재 코드에서 `_alignViewer`는 `RegisterCustomUI`에서 단 1회 생성되므로 실제 누수는 없으나, 방어적으로 이전 `_viewer`에 대한 해제가 필요하다.

**Fix:**
```csharp
public void AttachSharedViewer(MainResultViewerControl viewer)
{
    if (viewer == null)
    {
        return;
    }
    // 이전 _viewer 구독 해제 (이전과 신규가 다른 인스턴스일 경우 대비)
    if (_viewer != null && !ReferenceEquals(_viewer, viewer))
    {
        _viewer.CircleDrawingCompleted -= OnCalCircleDrawn;
    }
    _viewer = viewer;
    ViewerHostBorder.Child = viewer;

    _viewer.CircleDrawingCompleted -= OnCalCircleDrawn;
    _viewer.CircleDrawingCompleted += OnCalCircleDrawn;
}
```

---

### WR-02: ValidateRois — 음수 halfW/halfH 통과 버그 (역방향 드래그)

**File:** `WPF_Example/Custom/UI/TrayVisionView.xaml.cs:286-298` / `WPF_Example/Custom/UI/BottomVisionView.xaml.cs:429-441`

**Issue:** `halfW = (Column2 - Column1) / 2.0`와 `halfH = (Row2 - Row1) / 2.0`을 계산한 뒤 `< MIN_ROI_HALF_LENGTH(1.0)` 로 검사한다. `RoiDefinition`은 `CommitActiveRectangle`이 드래그 방향 정규화를 보장하는지에 달려 있다. `MainResultViewerControl.CommitActiveRectangle`은 내부 `_rectDraftRoi`를 그대로 반환하므로, 역방향 드래그(Row2 < Row1)가 발생하면 `halfH`가 음수가 되어 `< 1.0` 조건을 통과한다. 이후 `RectToTeachParams`에서 음수 `len2`가 HALCON `TryTeach`에 전달되면 `gen_rectangle2`의 Length2(Half-height) 음수로 예외가 발생하거나 ROI가 0 크기로 처리된다.

**Fix:** 절댓값으로 검증하도록 수정:
```csharp
private string ValidateRois()
{
    if (_roi1 == null)
    {
        return "ROI 1 미설정 — ROI 1 그리기 먼저";
    }
    if (_roi2 == null)
    {
        return "ROI 2 미설정 — ROI 2 그리기 먼저";
    }

    double halfW1 = Math.Abs(_roi1.Column2 - _roi1.Column1) / 2.0;
    double halfH1 = Math.Abs(_roi1.Row2 - _roi1.Row1) / 2.0;
    if (halfW1 < MIN_ROI_HALF_LENGTH || halfH1 < MIN_ROI_HALF_LENGTH)
    {
        return "ROI 1 이 너무 작습니다 — 다시 그리기";
    }

    double halfW2 = Math.Abs(_roi2.Column2 - _roi2.Column1) / 2.0;
    double halfH2 = Math.Abs(_roi2.Row2 - _roi2.Row1) / 2.0;
    if (halfW2 < MIN_ROI_HALF_LENGTH || halfH2 < MIN_ROI_HALF_LENGTH)
    {
        return "ROI 2 가 너무 작습니다 — 다시 그리기";
    }

    return null;
}
```

`RectToTeachParams`도 동일하게 `Math.Abs` 적용 필요:
```csharp
len1 = Math.Abs(roi.Column2 - roi.Column1) / 2.0;
len2 = Math.Abs(roi.Row2 - roi.Row1) / 2.0;
```

같은 버그가 두 파일 모두에 존재한다.

---

### WR-03: Release|x64 빌드 설정 — Prefer32Bit=true 불일치

**File:** `WPF_Example/DatumMeasurement.csproj:80`

**Issue:** `Release|x64` PropertyGroup에 `<Prefer32Bit>true</Prefer32Bit>`가 설정되어 있다. `PlatformTarget=x64`와 `Prefer32Bit=true`는 동시 적용 시 `Prefer32Bit`가 무시되지만(x64 타겟은 항상 64비트), 다른 빌드 설정(`Debug|AnyCPU`: `Prefer32Bit=false`, `Debug|x64`: 태그 없음)과 일관성이 없다. 또한 x64 강제 빌드를 생산 배포용으로 사용하는 이 프로젝트에서 혼동을 줄 수 있다.

**Fix:**
```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
  <OutputPath>..\..\..\..\Data\</OutputPath>
  <DefineConstants>TRACE;SIMUL_MODE</DefineConstants>
  <Optimize>true</Optimize>
  <DebugType>pdbonly</DebugType>
  <PlatformTarget>x64</PlatformTarget>
  <!-- Prefer32Bit 제거 — x64 PlatformTarget 에서 무효, 혼동 방지 -->
  <ErrorReport>prompt</ErrorReport>
  <CodeAnalysisRuleSet>MinimumRecommendedRules.ruleset</CodeAnalysisRuleSet>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

---

## Info

### IN-01: BottomVisionView — Unloaded 이벤트 구독 해제 없음

**File:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs:43-46`

**Issue:** `Loaded` 이벤트를 구독하지만 `Unloaded`에서 `CircleDrawingCompleted` 구독을 해제하지 않는다. 현재 `_alignViewer`는 앱 생존 기간 동안 단일 인스턴스이므로 실제 누수가 발생하는 시나리오는 없다. 그러나 탭 전환 시 `RefreshEthernetVisionTabs`가 반복 호출되고, `AttachSharedViewer` 내의 `-= / +=` 패턴이 동일 인스턴스 대상이므로 안전하다. 향후 `_alignViewer` 재생성 정책이 바뀔 경우를 대비한 방어 코드로 `Unloaded` 핸들러 추가를 권장한다.

**Fix:**
```csharp
public BottomVisionView()
{
    InitializeComponent();
    Loaded += BottomVisionView_Loaded;
    Unloaded += BottomVisionView_Unloaded;
}

private void BottomVisionView_Unloaded(object sender, RoutedEventArgs e)
{
    if (_viewer != null)
    {
        _viewer.CircleDrawingCompleted -= OnCalCircleDrawn;
    }
}
```

---

### IN-02: Release|x64 빌드 설정 — LangVersion 태그 누락

**File:** `WPF_Example/DatumMeasurement.csproj:72-82`

**Issue:** `Debug|AnyCPU`(line 47)와 `Debug|x64`(line 60-70)는 `<LangVersion>7.2</LangVersion>`을 명시하지만, `Release|x64` PropertyGroup에는 해당 태그가 없다. MSBuild는 태그 부재 시 컴파일러 기본값(VS 버전별 상이)을 사용하므로 C# 7.2 초과 기능이 Release 빌드에서 컴파일될 수 있다. 현재 코드에 C# 8+ 구문이 없어 실질적 영향은 없으나, 언어 버전 고정 정책(`LangVersion=7.2`)과 일관성이 필요하다.

**Fix:**
```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
  ...
  <LangVersion>7.2</LangVersion>
  ...
</PropertyGroup>
```

---

_Reviewed: 2026-06-24_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
