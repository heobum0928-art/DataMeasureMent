---
phase: 61-ui-tabcontrol-d-2026-06-23
plan: "03"
subsystem: UI
tags: [tabcontrol, integration, mainwindow, ethernet-vision, av-07, av-08]
dependency_graph:
  requires:
    - 61-01 (TrayVisionView + AttachSharedViewer 계약)
    - 61-02 (BottomVisionView + AttachSharedViewer + CircleDrawingCompleted 계약)
  provides:
    - MainWindow 3-탭 TabControl (MainView 재부모화)
    - EthernetVisionMode 탭 Visibility 게이트 (None/Tray/Bottom)
    - 단일 공유 MainResultViewerControl align 뷰 attach
  affects:
    - MainWindow.xaml (Contents 영역 구조 변경)
    - MainWindow.xaml.cs (Setting 종료 후 탭 재게이트)
    - Custom/UI/MainWindow.cs (RegisterCustomUI + RefreshEthernetVisionTabs)
tech_stack:
  added: []
  patterns:
    - TabControl + TabItem 재부모화 (MainView x:Name 보존)
    - WPF 단일 부모 제약 — detach(Border.Child=null) → attach 패턴
    - partial class MainWindow 확장 (RegisterCustomUI 채움)
key_files:
  created: []
  modified:
    - WPF_Example/MainWindow.xaml
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/Custom/UI/MainWindow.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "D-03 공유 뷰어: 단일 MainResultViewerControl 인스턴스, EthernetVisionMode 배타로 한 번에 하나의 align 뷰만 실재"
  - "D-04 탭 게이트: None→Tray/Bottom 둘 다 Collapsed, Tray→tabTray Visible, Bottom→tabBottom Visible"
  - "안티골: MainView 재부모화만(코드 0줄 수정), git diff 로 MainView/뷰어/Grabber 0변경 증명"
metrics:
  duration: "~2m"
  completed: 2026-06-24
  tasks_completed: 3
  files_changed: 4
---

# Phase 61 Plan 03: Integration — MainWindow TabControl + 탭 게이트 + 공유 뷰어 Summary

**One-liner:** MainWindow 에 3-탭 TabControl 추가해 MainView 재부모화 + EthernetVisionMode 게이트 + 단일 공유 MainResultViewerControl align 뷰 attach (AV-07/AV-08 통합 완료)

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | MainWindow.xaml 3-탭 TabControl 래퍼 | 1e3d845 | MainWindow.xaml |
| 2 | 탭 Visibility 게이트 + 공유 뷰어 attach | fb07ea1 | Custom/UI/MainWindow.cs, MainWindow.xaml.cs |
| 3 | csproj 등록 + msbuild 빌드 PASS + 안티골 증명 | 349eefe | DatumMeasurement.csproj |

## What Was Built

### Task 1 — MainWindow.xaml TabControl 래퍼

`WPF_Example/MainWindow.xaml` Contents Grid(Grid.Row=2, Grid.Column=0) 의 `<ui:MainView x:Name="mainView"/>` (line 70) 를 3-탭 TabControl 로 교체:

- `xmlns:customui="clr-namespace:ReringProject.Custom.UI"` 선언 추가 (Window 헤더)
- `<TabControl x:Name="tabMain" Grid.Row="0">` 로 래핑
  - `[검사]` TabItem: `<ui:MainView x:Name="mainView"/>` — **x:Name 보존** (MainWindow.xaml.cs 의 mainView 참조 무손상)
  - `[Tray 비전]` TabItem: `<customui:TrayVisionView x:Name="trayVisionView"/>`
  - `[Bottom 비전]` TabItem: `<customui:BottomVisionView x:Name="bottomVisionView"/>`
- GridSplitter(Row=1) + LogView(Row=2) **그대로 유지**
- TitleBar/MenuBar/InspectionListView/StatusBar **무변경**

### Task 2 — Custom/UI/MainWindow.cs + MainWindow.xaml.cs

**Custom/UI/MainWindow.cs** (`partial class MainWindow`) 의 빈 `RegisterCustomUI()` 채움:

- `private MainResultViewerControl _alignViewer` — 단일 공유 뷰어 필드 (D-03)
- `RegisterCustomUI()`: `_alignViewer = new MainResultViewerControl()` 한 번 생성 + `RefreshEthernetVisionTabs()` 호출
- `public void RefreshEthernetVisionTabs()` (≤30줄, try-catch):
  - `SystemSetting.Handle.EthernetVisionMode` 읽어 bool bTray/bBottom 계산
  - if-else 로 tabTray/tabBottom Visibility 게이트 (삼항 금지 준수)
  - `DetachAlignViewer()` → bTray 시 `trayVisionView.AttachSharedViewer(_alignViewer)`, bBottom 시 `bottomVisionView.AttachSharedViewer(_alignViewer)`, None 시 어디에도 미부착
  - catch(Exception ex) → `Logging.PrintLog((int)ELogType.Error, ...)` (UI 무중단 — T-61-05)
- `private void DetachAlignViewer()`: `_alignViewer.Parent as Border` 캐스팅 → `parentBorder.Child = null` (WPF 단일 부모 제약 해제)

**MainWindow.xaml.cs**: `case EPageType.Setting:` 의 `mModalWindow.ShowDialog()` 직후 `RefreshEthernetVisionTabs()` 추가 (D-04 모드 변경 즉시 탭 재게이트).

### Task 3 — csproj 등록 + 빌드 PASS + 안티골 증명

**DatumMeasurement.csproj 4 항목 추가**:

Page ItemGroup (기존 포맷, 백슬래시 경로):
```xml
<Page Include="Custom\UI\TrayVisionView.xaml">
  <SubType>Designer</SubType>
  <Generator>MSBuild:Compile</Generator>
</Page>
<Page Include="Custom\UI\BottomVisionView.xaml">
  <SubType>Designer</SubType>
  <Generator>MSBuild:Compile</Generator>
</Page>
```

Compile ItemGroup (DependentUpon):
```xml
<Compile Include="Custom\UI\TrayVisionView.xaml.cs">
  <DependentUpon>TrayVisionView.xaml</DependentUpon>
</Compile>
<Compile Include="Custom\UI\BottomVisionView.xaml.cs">
  <DependentUpon>BottomVisionView.xaml</DependentUpon>
</Compile>
```

**msbuild Debug/x64 빌드 결과:**
- 에러: **0**
- 경고: 기존 CS0618(Obsolete) × 5, CS0162(unreachable) × 1, MSB3884 × 2 — 전부 기존 코드 warning, Phase 61 신규 warning 없음
- 출력: `DatumMeasurement.exe` 생성 PASS

**안티골 git diff 증명:**
```
git diff --name-only HEAD~2 -- MainView.xaml MainView.xaml.cs MainResultViewerControl.xaml.cs Custom/EthernetVision
→ (빈 문자열) = PASS
```
- Grabber(Custom/Sequence/Device/SystemHandler) = 0 변경 PASS
- 변경 파일: MainWindow.xaml / MainWindow.xaml.cs / Custom/UI/MainWindow.cs / DatumMeasurement.csproj (화이트리스트 내)

## Deviations from Plan

None — 플랜대로 정확히 실행됨.

## Build Gate

| 항목 | 결과 |
|------|------|
| msbuild Debug/x64 | **에러 0 PASS** |
| 신규 에러 | 0 |
| 신규 경고 | 0 (기존 warning 그대로) |
| 출력 파일 | DatumMeasurement.exe |

## Anti-Goal Git Proof

| 파일 / 경로 | 변경 여부 |
|------------|----------|
| WPF_Example/UI/ContentItem/MainView.xaml | 0 변경 |
| WPF_Example/UI/ContentItem/MainView.xaml.cs | 0 변경 |
| WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs | 0 변경 |
| WPF_Example/Custom/EthernetVision/* | 0 변경 |
| WPF_Example/Custom/Sequence/** (Grabber) | 0 변경 |
| WPF_Example/Device/** (Camera) | 0 변경 |
| WPF_Example/SystemHandler.cs | 0 변경 |

## Known Stubs

없음 — 공유 뷰어 attach 로 데이터 흐름 완결. EthernetVisionMode=None 시 탭 Collapsed 은 의도된 동작.

## Threat Flags

없음 — 신규 네트워크/auth/파일 접근 경계 없음. T-61-05(RefreshEthernetVisionTabs try-catch) 적용 확인.
