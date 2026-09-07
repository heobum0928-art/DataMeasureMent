---
phase: quick-260907-fkh
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/Device/DeviceSelectorModelView.cs
  - WPF_Example/UI/Device/DeviceSelector.xaml
  - WPF_Example/UI/Device/DeviceSelector.xaml.cs
  - WPF_Example/UI/Device/CanvasViewer.cs
  - WPF_Example/Device/Camera/VirtualCamera.cs
autonomous: false
requirements: [FKH-01, FKH-02, FKH-03, FKH-04]
must_haves:
  truths:
    - "카메라 창 미리보기 위에서 마우스 휠을 올리면 확대, 내리면 축소된다 (Ctrl 불필요)"
    - "휠 줌 전후로 커서 아래의 이미지 지점이 화면상 같은 자리에 남는다"
    - "십자/센터 사각형/원이 어떤 배율에서도 이미지 픽셀 좌표(CenterX/Y)에 해당하는 화면 위치에 그려진다"
    - "십자 선 두께가 배율과 무관하게 화면상 일정하다"
    - "라이브 프레임이 100ms 간격으로 갱신되는 동안 줌 배율과 스크롤 위치가 리셋되지 않는다"
    - "우측 상단 spin_zoom 값이 휠 줌과 함께 갱신된다 (DrawScale 단일 소스)"
  artifacts:
    - path: "WPF_Example/UI/Device/DeviceSelectorModelView.cs"
      provides: "PreviewZoomCalculator 순수 줌 계산 (새 배율 + 앵커 오프셋)"
      contains: "class PreviewZoomCalculator"
    - path: "WPF_Example/UI/Device/DeviceSelector.xaml.cs"
      provides: "ScrollViewer PreviewMouseWheel 배선 + 오프셋 적용"
      contains: "ScrollViewer_PreviewMouseWheel"
    - path: "WPF_Example/UI/Device/CanvasViewer.cs"
      provides: "이중 스케일 제거된 센터라인 렌더"
    - path: "WPF_Example/Device/Camera/VirtualCamera.cs"
      provides: "화면 두께 고정용 RenderCenterLine 오버로드"
      contains: "RenderCenterLine"
  key_links:
    - from: "WPF_Example/UI/Device/DeviceSelector.xaml"
      to: "DeviceSelector.xaml.cs ScrollViewer_PreviewMouseWheel"
      via: "PreviewMouseWheel 이벤트"
      pattern: "PreviewMouseWheel"
    - from: "DeviceSelector.xaml.cs 휠 핸들러"
      to: "DeviceSelectorModelView.DrawScale"
      via: "ModelView.DrawScale 세터 (pDevs.Config.DrawScale + ZoomValueChanged + PropertyChanged)"
      pattern: "ModelView.DrawScale"
    - from: "CanvasViewer.OnRender"
      to: "VirtualCamera.RenderCenterLine"
      via: "현재 DrawScale 을 두께 보정값으로 전달"
      pattern: "RenderCenterLine"
---

<objective>
카메라 창(DeviceSelector) 라이브 미리보기에 마우스 휠 줌인/아웃을 추가하고, 줌 후에도 십자표시가 이미지상 같은 위치에 정확히 표시되게 한다.

Purpose: 현재 배율 변경은 우측 상단 spin_zoom(SpinControl) 로만 가능하고, 배율을 올리면 센터 십자가 실제 위치의 배율배 지점으로 어긋난다(원인 아래 4번). 실기에서 센터 정렬을 눈으로 잡으려면 휠 줌 + 정확한 십자가 필요하다.
Output: 커서 고정 휠 줌 + 십자 정합/두께 수정.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md

@WPF_Example/UI/Device/DeviceSelector.xaml
@WPF_Example/UI/Device/DeviceSelector.xaml.cs
@WPF_Example/UI/Device/DeviceSelectorModelView.cs
@WPF_Example/UI/Device/CanvasViewer.cs
@WPF_Example/Device/Camera/VirtualCamera.cs
@WPF_Example/Device/DisplayConfig.cs
</context>

<current_structure>
실제 코드로 확인한 사실 (라인 번호는 착수 시점 기준, 추측 아님):

1. XAML `DeviceSelector.xaml:178-190`
   - `scrollViewer` (ScrollViewer, Horizontal/VerticalScrollBarVisibility=Auto)
     - `canvas_preview` (local:CanvasViewer, HorizontalAlignment=Left, VerticalAlignment=Top)
       - Canvas.Background = ImageBrush(Stretch=None, AlignmentX=Left, AlignmentY=Top) — 라이브 프레임
       - `image_foreground` (Image, Source 미설정 = 현재 아무것도 그리지 않음)
       - Canvas.RenderTransform = `scaleTransform` (ScaleTransform, ScaleX/Y = {Binding DrawScale}, CenterX/Y = 0)
   - Canvas 는 Left/Top 정렬이므로 스크롤 extent 원점 = 이미지 좌상단. 앵커 오프셋 계산이 단순해진다.

2. 배율 단일 소스
   - `DisplayConfig.DrawScale` (WPF_Example/Device/DisplayConfig.cs:43) — 세터가 `DrawScaleLowLimit(0.25)` ~ `DrawScaleHighLimit(2.0)` 밖의 값을 **조용히 무시**한다.
   - `DeviceSelectorModelView.DrawScale` (DeviceSelectorModelView.cs:76-85) 세터 = `pDevs.Config.DrawScale = value` → `Parent.ZoomValueChanged()` → `PropertyChanged("DrawScale")`.
   - 기존 배율 UI = `spin_zoom` (propTools:SpinControl, XAML:113) 이 `{Binding DrawScale}` 로 VM 에 물려 있다. 따라서 **휠 줌도 반드시 `ModelView.DrawScale` 세터를 거쳐야** SpinControl 표시가 같이 갱신된다.
   - `DeviceSelector.ZoomValueChanged()` (DeviceSelector.xaml.cs:303-315) 는 scaleTransform.ScaleX/Y, canvas_preview.Width/Height(= 해상도 x DrawScale), image_foreground.Width/Height 만 갱신하고 **스크롤 오프셋은 건드리지 않는다**. 요구사항 4 는 이 성질 덕에 이미 충족 — 깨뜨리지 말 것.
   - DrawScale 영속 저장 경로는 이번에 손대지 않는다 (DisplayConfig.cs:150 은 주석 처리된 상태 그대로 둔다).

3. 라이브 경로
   - `OnImageReady` (xaml.cs:215) → Dispatcher.BeginInvoke → `GetPreviewBitmapSource()` → `DisplayToBackground` (xaml.cs:192) → `canvas_preview.Background = new ImageBrush(frame)` + `ZoomValueChanged()`. `UPDATE_INTERVAL = 100`ms 스로틀.

4. 십자 어긋남의 원인 (확인됨)
   - `CanvasViewer.OnRender` (CanvasViewer.cs:26-34) 가 `dc.PushTransform(this.RenderTransform)` 후 `pCamera.RenderCenterLine(dc)` 를 호출한다. 그런데 `this.RenderTransform`(= scaleTransform) 은 WPF 가 이 Visual 전체(자기 OnRender 결과 포함)에 **이미 한 번 적용**한다. 결과적으로 센터라인만 배율이 **두 번** 곱해져 DrawScale=2.0 이면 십자가 실제 센터의 2배 좌표에 그려진다(대개 화면 밖 = "사라진다"). `dc.Pop()` 도 없다.
   - `VirtualCamera.RenderCenterLine(dc)` (VirtualCamera.cs:256-281) 는 CenterX/CenterY/해상도를 **이미지 픽셀 좌표 그대로** 사용한다. 즉 Canvas 로컬 좌표계와 동일하므로, PushTransform 을 제거하면 RenderTransform 이 한 번만 적용되어 정합이 맞는다.
   - 남는 문제는 펜 두께: `DrawPen` (VirtualCamera.cs:84 선언 / 146 생성, Fuchsia, Thickness 4, Dash) 도 함께 확대된다. **"화면 좌표로 다시 그리기" 대신 "두께 보정"** 을 선택한다. 근거: RenderCenterLine 은 `RuntimeResizer.cs:467,476`(MainView 계열 = 이번 무변경 대상)에서도 호출되므로, 기존 1-인자 시그니처와 좌표계를 그대로 두고 DeviceSelector 전용 오버로드에서 두께만 나누는 편이 회귀 위험이 가장 작다.

5. 신규 파일을 만들지 않는 이유
   - `WPF_Example/DatumMeasurement.csproj` 는 classic 형식이라 새 .cs 마다 `<Compile Include>` 추가가 필요하고, 현재 csproj 는 다른 세션이 이미 수정(git status M) 중이다. 따라서 줌 계산 헬퍼는 **기존 `DeviceSelectorModelView.cs` 안에** 추가한다 (csproj:389 에 이미 등록됨).
</current_structure>

<tasks>

<task type="auto">
  <name>Task 1: PreviewZoomCalculator (순수 줌 계산) 추가</name>
  <files>WPF_Example/UI/Device/DeviceSelectorModelView.cs</files>
  <action>
`DeviceSelectorModelView.cs` 의 `namespace ReringProject.UI` 안, `DeviceSelectorModelView` 클래스 뒤에 `public static class PreviewZoomCalculator` 를 추가한다. 새 파일을 만들지 말 것(위 current_structure 5번).

멤버 구성:
- `public const double ZOOM_STEP_FACTOR = 1.25;` — 휠 한 칸당 배수(매직넘버 금지 규칙).
- `public static double GetNextScale(double dCurrentScale, bool bZoomIn)`
  - `bZoomIn` 이면 `dNext = dCurrentScale * ZOOM_STEP_FACTOR`, 아니면 `dNext = dCurrentScale / ZOOM_STEP_FACTOR`. 삼항 금지, `if/else` 블록으로.
  - 기존 `DisplayConfig.DrawScaleLowLimit` / `DisplayConfig.DrawScaleHighLimit` 로 클램프(각각 별도 `if` 블록). 새 한계 상수를 만들지 말 것 — `DisplayConfig.DrawScale` 세터가 범위 밖 값을 조용히 무시하므로, 같은 한계로 클램프해야 계산값과 실제 표시값이 어긋나지 않는다.
  - 클램프된 배율을 반환.
- `public static void GetAnchoredOffset(double dOldScale, double dNewScale, double dOldOffsetX, double dOldOffsetY, double dCursorViewX, double dCursorViewY, out double dNewOffsetX, out double dNewOffsetY)`
  - 가드: `dOldScale` 이 0 이하이면 `dNewOffsetX = dOldOffsetX; dNewOffsetY = dOldOffsetY; return;` (중괄호 필수).
  - 커서 아래 이미지 픽셀 좌표: `dImageX = (dOldOffsetX + dCursorViewX) / dOldScale`, Y 동일.
  - 새 스크롤 오프셋: `dNewOffsetX = (dImageX * dNewScale) - dCursorViewX`, Y 동일.
  - 음수/최대 클램프는 하지 않는다. ScrollViewer.ScrollToHorizontalOffset 이 자체 클램프한다는 점을 주석 1줄로 근거만 남긴다.

CLAUDE.md 하드룰: 삼항 `?:` / `??` / `??=` / `?.` / switch 식 금지, 한 줄 분기도 중괄호, 헝가리언 접두사(b/n/sz/d), 매직넘버 금지, 날짜주석(`//YYMMDD hbk`) 신규 금지, C# 7.2. 이 파일의 기존 브레이스 스타일(K&R)을 유지한다. 주석은 "왜"만 한국어로 최소한.
  </action>
  <verify>
    <automated>git -C . diff -U0 -- WPF_Example/UI/Device/DeviceSelectorModelView.cs | grep "^+" | grep -v "^+++" > .planning/quick/260907-fkh-camera-live-wheel-zoom/t1.diff ; grep -cE "\?[^?]*:" .planning/quick/260907-fkh-camera-live-wheel-zoom/t1.diff ; grep -cF "??" .planning/quick/260907-fkh-camera-live-wheel-zoom/t1.diff ; grep -cF "?." .planning/quick/260907-fkh-camera-live-wheel-zoom/t1.diff ; grep -cE "switch.*=>" .planning/quick/260907-fkh-camera-live-wheel-zoom/t1.diff ; grep -cF "hbk" .planning/quick/260907-fkh-camera-live-wheel-zoom/t1.diff ; grep -c "class PreviewZoomCalculator" WPF_Example/UI/Device/DeviceSelectorModelView.cs</automated>
  </verify>
  <done>추가 라인 기준 5개 grep 게이트가 모두 0, `class PreviewZoomCalculator` 1건. GetNextScale/GetAnchoredOffset 두 메서드 존재.</done>
</task>

<task type="auto">
  <name>Task 2: 십자표시 정합 + 두께 보정 (이중 스케일 제거)</name>
  <files>WPF_Example/Device/Camera/VirtualCamera.cs, WPF_Example/UI/Device/CanvasViewer.cs</files>
  <action>
(a) `VirtualCamera.cs`
- 클래스에 `private const double CENTER_PEN_THICKNESS = 4;` 를 추가하고, 생성자(VirtualCamera.cs:146) 의 `new Pen(Brushes.Fuchsia, 4)` 를 이 const 로 바꾼다(동작 동일, 매직넘버 제거).
- 기존 `RenderCenterLine(DrawingContext dc)` (VirtualCamera.cs:256) 의 **시그니처와 좌표계는 그대로 둔다**. RuntimeResizer.cs:467,476 이 이 오버로드를 계속 쓴다(무변경 대상).
- 새 오버로드 `public virtual void RenderCenterLine(DrawingContext dc, double dViewScale)` 를 추가한다:
  - 본문은 기존 메서드와 동일한 좌표 계산(이미지 픽셀 좌표)을 쓰되, `DrawPen` 대신 로컬 펜을 만들어 쓴다: 두께 `CENTER_PEN_THICKNESS / dViewScale`, Brush Fuchsia, `DashStyle = DashStyles.Dash`. 배율 안에서 그리므로 두께를 배율로 나눠야 화면상 두께가 일정해진다는 근거를 주석 1줄로 남긴다.
  - 가드: `dViewScale` 이 0 이하이면 기존 `RenderCenterLine(dc)` 를 호출하고 return (중괄호 필수).
  - 코드 중복을 줄이려면 기존 1-인자 메서드가 `RenderCenterLine(dc, 1.0)` 로 위임하도록 리팩터해도 된다. 단, 이 경우 dViewScale=1.0 일 때 만들어지는 펜이 기존 DrawPen 과 **두께/브러시/DashStyle 이 완전히 동일**해야 한다(RuntimeResizer 표시 회귀 금지). 판단 근거를 SUMMARY 에 적을 것.
  - `Pen` 은 IDisposable 이 아니므로 별도 해제 불필요. HImage/HObject/HTuple 은 이 경로에 없다.

(b) `CanvasViewer.cs`
- `OnRender` 의 `dc.PushTransform(this.RenderTransform);` 를 **삭제**한다. 이것이 이중 스케일의 원인이다(current_structure 4번). Pop 도 없었으므로 함께 정리된다.
- `pCamera.RenderCenterLine(dc)` 를 `pCamera.RenderCenterLine(dc, dViewScale)` 로 바꾼다. `dViewScale` 은 `this.RenderTransform` 이 `ScaleTransform` 일 때 그 `ScaleX` 에서 얻는다: `ScaleTransform scale = this.RenderTransform as ScaleTransform;` 후 `if (scale != null) { dViewScale = scale.ScaleX; }` — null 조건 연산자 금지, 명시적 분기. 기본값은 `const double DEFAULT_VIEW_SCALE = 1.0;`.
- `if (pCamera == null) return;` 같은 기존 가드는 그대로 두되, 이번에 손대는 라인은 중괄호를 채운다.
- 파일 기존 브레이스 스타일(K&R) 유지, 하드룰 동일 적용.

무변경 확인: 카메라 grab/스트리밍 경로, MainView/RuntimeResizer 뷰어, 조명 패널은 건드리지 않는다.
  </action>
  <verify>
    <automated>git -C . diff -U0 -- WPF_Example/Device/Camera/VirtualCamera.cs WPF_Example/UI/Device/CanvasViewer.cs | grep "^+" | grep -v "^+++" > .planning/quick/260907-fkh-camera-live-wheel-zoom/t2.diff ; grep -cE "\?[^?]*:" .planning/quick/260907-fkh-camera-live-wheel-zoom/t2.diff ; grep -cF "??" .planning/quick/260907-fkh-camera-live-wheel-zoom/t2.diff ; grep -cF "?." .planning/quick/260907-fkh-camera-live-wheel-zoom/t2.diff ; grep -cE "switch.*=>" .planning/quick/260907-fkh-camera-live-wheel-zoom/t2.diff ; grep -cF "hbk" .planning/quick/260907-fkh-camera-live-wheel-zoom/t2.diff ; grep -c "PushTransform" WPF_Example/UI/Device/CanvasViewer.cs</automated>
  </verify>
  <done>추가 라인 grep 게이트 5개 모두 0. CanvasViewer.cs 에 `PushTransform` 0건. VirtualCamera 에 2-인자 RenderCenterLine 오버로드 존재, 1-인자 오버로드는 RuntimeResizer 용으로 유지.</done>
</task>

<task type="auto">
  <name>Task 3: 휠 줌 배선 (XAML 이벤트 + 오프셋 적용) 및 빌드</name>
  <files>WPF_Example/UI/Device/DeviceSelector.xaml, WPF_Example/UI/Device/DeviceSelector.xaml.cs</files>
  <action>
(a) `DeviceSelector.xaml:178` 의 `scrollViewer` 에 `PreviewMouseWheel="ScrollViewer_PreviewMouseWheel"` 를 추가한다. Preview 터널링 이벤트를 쓰는 이유: ScrollViewer 기본 세로 스크롤이 먼저 소비되지 않게 하기 위함(주석 대신 SUMMARY 에 근거 기록).

(b) `DeviceSelector.xaml.cs` 에 핸들러를 추가한다 (code-behind 는 배선 + ScrollViewer 오프셋 적용만, 계산은 Task 1 의 PreviewZoomCalculator 사용 — MVVM 규칙):

`private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)`
1. 가드: `if (pSelectedDevice == null) { e.Handled = true; return; }`, `if (pSelectedDevice.Properties == null) { e.Handled = true; return; }`
2. `double dOldScale = pDevs.Config.DrawScale;`
3. `bool bZoomIn = (e.Delta > 0);`
4. `double dNewScale = PreviewZoomCalculator.GetNextScale(dOldScale, bZoomIn);`
5. `if (dNewScale == dOldScale) { e.Handled = true; return; }` — 한계에 걸린 경우 오프셋도 건드리지 않는다.
6. 커서의 뷰포트 내 위치: `System.Windows.Point ptCursor = e.GetPosition(scrollViewer);` (Ctrl 조합 없이 휠만으로 동작 — Keyboard.Modifiers 검사하지 않는다)
7. `double dOldOffsetX = scrollViewer.HorizontalOffset; double dOldOffsetY = scrollViewer.VerticalOffset;`
8. `ModelView.DrawScale = dNewScale;` — 배율 단일 소스. 세터가 pDevs.Config.DrawScale 갱신 + ZoomValueChanged() + PropertyChanged 로 spin_zoom 까지 갱신한다. 여기서 scaleTransform/canvas 크기를 직접 만지지 말 것.
9. `double dAppliedScale = pDevs.Config.DrawScale;` — DisplayConfig 세터가 값을 무시했을 수 있으므로 실제 반영값을 다시 읽는다. `if (dAppliedScale == dOldScale) { e.Handled = true; return; }`
10. `scrollViewer.UpdateLayout();` — canvas_preview.Width/Height 변경이 extent 에 반영된 뒤라야 ScrollTo* 가 원하는 값으로 클램프된다.
11. `PreviewZoomCalculator.GetAnchoredOffset(dOldScale, dAppliedScale, dOldOffsetX, dOldOffsetY, ptCursor.X, ptCursor.Y, out double dNewOffsetX, out double dNewOffsetY);` (C# 7.2 out 변수 선언 가능)
12. `scrollViewer.ScrollToHorizontalOffset(dNewOffsetX); scrollViewer.ScrollToVerticalOffset(dNewOffsetY);`
13. `e.Handled = true;` — 기본 스크롤과 충돌 방지.

(c) `ZoomValueChanged()` (xaml.cs:303) 끝에 `canvas_preview.InvalidateVisual();` 한 줄을 추가한다. 스트리밍이 멈춘 상태(Background 미갱신)에서 배율만 바뀌면 CanvasViewer.OnRender 가 다시 호출되지 않아 십자가 옛 배율로 남기 때문이다. **스크롤 오프셋은 이 메서드에서 절대 건드리지 않는다** — DisplayToBackground 가 100ms 마다 호출하므로 줌/스크롤 위치가 리셋된다(요구사항 4).

(d) 빌드:
`"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build` (Bash 도구). 출력이 D:\Data 이고 그 exe 가 실행 중이면 MSB3027 복사 실패가 뜰 수 있다 — `error CS` 가 0 이면 컴파일 통과로 본다. **실행 중 프로세스 강제종료 금지.**

(e) 스테이징: `git add` 로 이번에 수정한 5개 파일만 명시 지정. `git add .` / `git add -A` 금지 (다른 세션이 같은 워킹트리에서 WPF_Example/DatumMeasurement.csproj 를 수정 중이다 — csproj 는 스테이징하지 말 것).

하드룰 동일 적용: 삼항/`??`/`?.`/switch 식 금지, 한 줄 분기도 중괄호, 헝가리언, 매직넘버 금지, 날짜주석 금지, C# 7.2, 파일 기존 K&R 스타일 유지.
  </action>
  <verify>
    <automated>git -C . diff -U0 -- WPF_Example/UI/Device/DeviceSelector.xaml.cs WPF_Example/UI/Device/DeviceSelector.xaml | grep "^+" | grep -v "^+++" > .planning/quick/260907-fkh-camera-live-wheel-zoom/t3.diff ; grep -cE "\?[^?]*:" .planning/quick/260907-fkh-camera-live-wheel-zoom/t3.diff ; grep -cF "??" .planning/quick/260907-fkh-camera-live-wheel-zoom/t3.diff ; grep -cF "?." .planning/quick/260907-fkh-camera-live-wheel-zoom/t3.diff ; grep -cE "switch.*=>" .planning/quick/260907-fkh-camera-live-wheel-zoom/t3.diff ; grep -cF "hbk" .planning/quick/260907-fkh-camera-live-wheel-zoom/t3.diff ; grep -c "PreviewMouseWheel" WPF_Example/UI/Device/DeviceSelector.xaml ; grep -c "ScrollToHorizontalOffset" WPF_Example/UI/Device/DeviceSelector.xaml.cs ; grep -c "InvalidateVisual" WPF_Example/UI/Device/DeviceSelector.xaml.cs</automated>
  </verify>
  <done>grep 게이트 5개 0, XAML 에 PreviewMouseWheel 1건, ScrollToHorizontalOffset 1건, InvalidateVisual 1건. MSBuild 출력에 `error CS` 0건(MSB3027 복사 실패는 허용). 수정 파일 5개만 스테이징됨.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 4: 실기 확인 (휠 줌 / 십자 위치 / 라이브 중 리셋)</name>
  <what-built>
DeviceSelector 미리보기 마우스 휠 줌(1.25배 스텝, 0.25~2.0 범위, 커서 지점 고정) + 센터 십자 이중 스케일 제거(정합) + 배율 무관 선 두께 고정.
  </what-built>
  <how-to-verify>
1. 프로그램 실행 → 카메라 창(DeviceSelector) 열기 → 카메라 선택해서 라이브가 나오는지 확인.
2. Display 탭에서 Center Line / Center Rect / Center Circle 을 켠다.
3. 배율 1.0 에서 십자 교차점이 이미지의 센터(설정한 CenterX/Y) 위에 있는지 눈으로 확인.
4. 이미지의 특정 특징점(예: 부품 모서리) 위에 마우스를 두고 휠을 위로 3~4칸 굴린다.
   - 확대되는가?
   - **커서 아래 특징점이 화면상 거의 그 자리에 남아 있는가?** (수 픽셀 오차는 허용)
   - 우측 상단 배율 숫자(spin_zoom) 가 같이 올라가는가?
5. 확대된 상태에서 십자 교차점이 여전히 이미지의 같은 지점(센터) 위에 있는가? 선 두께가 1.0 때와 비슷한가?
6. 스크롤바로 이동한 뒤에도 십자가 이미지의 같은 지점에 붙어 있는가?
7. 그 상태로 10초 이상 라이브를 그대로 두고 본다 — 배율이나 스크롤 위치가 저절로 원위치로 튀지 않는가?
8. 휠을 아래로 계속 굴려 최소 배율(0.25)에서 더 이상 줄지 않고 화면이 튀지 않는지, 위로 굴려 최대(2.0)에서도 같은지 확인.
9. 회귀 확인: 검사 화면(MainView) 뷰어의 센터 표시가 예전과 동일한지, 조명 패널이 정상인지 한 번 본다.
  </how-to-verify>
  <resume-signal>"approved" 라고 쓰거나 어긋난 항목(번호)과 증상을 알려주세요.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 없음 (신규) | 로컬 WPF UI 이벤트만 다룬다. 네트워크/파일/외부 입력 경계를 새로 건너지 않는다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-fkh-01 | Denial of Service | ScrollViewer_PreviewMouseWheel (UI 스레드) | mitigate | 핸들러는 산술 + ScrollTo 호출만. 루프/IO/락 없음. 배율은 GetNextScale 에서 0.25~2.0 클램프되어 무한 확대로 인한 메모리 폭주 없음 |
| T-fkh-02 | Tampering | 패키지 설치 | accept | 이번 작업은 npm/pip/cargo 설치가 없다. 신규 의존성 0 |
</threat_model>

<verification>
- Release|x64 MSBuild 에서 `error CS` 0건.
- 수정된 5개 파일의 **추가 라인**에 대해 `\?[^?]*:`, `??`, `?.`, `switch.*=>`, `hbk` grep 결과가 전부 0.
- 중괄호 없는 한 줄 분기(`if (...) x;`)를 새로 만들지 않았는지 diff 육안 확인.
- `WPF_Example/DatumMeasurement.csproj` 는 스테이징/커밋되지 않음 (다른 세션 작업분).
- 카메라 grab/스트리밍 코드(VirtualCamera grab 경로), MainView/RuntimeResizer 뷰어, 조명 패널 파일은 diff 에 등장하지 않음.
</verification>

<success_criteria>
- 미리보기 위 마우스 휠만으로(Ctrl 없이) 1.25배 단위 확대/축소, 0.25~2.0 클램프.
- 줌 전후 커서 아래 이미지 지점이 화면상 고정.
- 십자/사각형/원이 모든 배율에서 CenterX/Y 에 대응하는 화면 위치에 표시되고, 선 두께가 화면상 일정.
- 라이브 갱신(100ms) 중에도 배율/스크롤 위치 유지.
- spin_zoom 표시가 휠 줌과 동기화 (DrawScale 단일 소스 유지, 새 영속 저장 경로 없음).
- 실기 checkpoint 통과.
</success_criteria>

<output>
Create `.planning/quick/260907-fkh-camera-live-wheel-zoom/260907-fkh-SUMMARY.md` when done.
작업 중 만든 t1.diff / t2.diff / t3.diff 임시 파일은 SUMMARY 작성 후 삭제할 것.
</output>
