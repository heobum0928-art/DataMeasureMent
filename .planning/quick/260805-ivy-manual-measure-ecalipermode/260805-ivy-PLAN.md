---
phase: quick-260805-ivy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ContentItem/ECaliperMode.cs
  - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
  - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml
  - WPF_Example/DatumMeasurement.csproj
autonomous: true
requirements: [IVY-01, IVY-02, IVY-03, IVY-04]

must_haves:
  truths:
    - "우클릭 메뉴 Manual Measure 아래에 '측정 축 고정' 서브메뉴가 있고, 자유/수평/수직 중 정확히 하나에만 체크 표시가 뜬다"
    - "수평 고정 상태에서 두 번째 점을 어디에 찍어도 끝점의 Y(row)가 시작점 Y와 같아지고, 표시 거리가 |ΔX| 와 일치한다"
    - "수직 고정 상태에서 두 번째 점의 X(column)가 시작점 X와 같아지고, 표시 거리가 |ΔY| 와 일치한다"
    - "자유(Free) 모드는 기존과 100% 동일한 유클리드 거리를 낸다 (기본값 = Free, 회귀 0)"
    - "시작점만 찍힌 상태에서 축 모드를 바꾸면 진행 중 시작점이 지워지고 'Select first point' 안내로 돌아간다 (Manual Measure 모드 자체는 유지)"
    - "메인 뷰(MainView halconViewer)와 Align 뷰(_alignViewer) 양쪽 모두 동일하게 동작한다 — MainResultViewerControl 한 클래스만 수정했기 때문"
    - "Debug|x64 MSBuild error 0, 신규 NuGet 0, 수정 파일은 4개(ECaliperMode.cs 신규 / MainResultViewerControl.xaml.cs / MainResultViewerControl.xaml / DatumMeasurement.csproj)뿐"
  artifacts:
    - path: "WPF_Example/UI/ContentItem/ECaliperMode.cs"
      provides: "ECaliperMode enum (Free/Horizontal/Vertical), namespace ReringProject.UI"
      contains: "public enum ECaliperMode"
    - path: "WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs"
      provides: "_manualMeasureAxisMode 필드 + ApplyManualMeasurePoint 축 스냅 + SetManualMeasureAxisMode + 3 클릭 핸들러 + UpdateContextMenuState 체크 동기화"
      contains: "_manualMeasureAxisMode"
    - path: "WPF_Example/UI/ContentItem/MainResultViewerControl.xaml"
      provides: "ViewerContextMenu 내 '측정 축 고정' 서브메뉴 3항목"
      contains: "MeasureAxisMenuItem"
    - path: "WPF_Example/DatumMeasurement.csproj"
      provides: "신규 .cs 파일의 Compile Include 등록 (classic csproj — 자동 포함 없음)"
      contains: "UI\\ContentItem\\ECaliperMode.cs"
  key_links:
    - from: "WPF_Example/DatumMeasurement.csproj"
      to: "WPF_Example/UI/ContentItem/ECaliperMode.cs"
      via: "<Compile Include> 항목 (없으면 컴파일 자체가 안 됨)"
      pattern: "Compile Include=\"UI\\\\ContentItem\\\\ECaliperMode.cs\""
    - from: "ApplyManualMeasurePoint else 분기"
      to: "_manualMeasureAxisMode"
      via: "imagePoint 대입 직전 if/else 축 스냅"
      pattern: "_manualMeasureAxisMode == ECaliperMode.Horizontal"
    - from: "MainResultViewerControl.xaml MeasureAxis*MenuItem Click"
      to: "SetManualMeasureAxisMode(ECaliperMode)"
      via: "3개 전용 클릭 핸들러"
      pattern: "SetManualMeasureAxisMode\\(ECaliperMode\\."
    - from: "UpdateContextMenuState()"
      to: "MeasureAxisFree/Horizontal/VerticalMenuItem.IsChecked"
      via: "메뉴 열릴 때(OpenContextMenu) 라디오 체크 상태 재동기화"
      pattern: "MeasureAxisFreeMenuItem.IsChecked"
---

<objective>
`MainResultViewerControl` 의 Manual Measure(두 점 클릭 → 픽셀 거리 표시) 기능에 **수평/수직 축 고정 모드**를 추가한다.

지금은 두 번째 점에 제약이 없어 자유 방향 거리만 나온다. 축을 고정하면 끝점 좌표 중 한 축을 시작점 값으로 강제 치환해서, 사용자가 손으로 정확히 같은 행/열을 찍지 않아도 순수 수평 거리 / 순수 수직 거리를 얻을 수 있다.

Purpose: 레시피·측정 파이프라인과 완전히 무관한 순수 UI 계측 유틸리티 개선. 메인 검사 뷰와 Align 뷰가 같은 클래스를 공유하므로 한 클래스 수정으로 두 화면 모두 반영된다.
Output: `ECaliperMode.cs` 신규 1개 + `MainResultViewerControl` 2파일 수정 + csproj 등록 1줄.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260805-ivy-manual-measure-ecalipermode/260805-ivy-CONTEXT.md
@CLAUDE.md

수정 대상 (현재 상태, 라인 번호 전부 실측 검증 완료 2026-08-05):
@WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
@WPF_Example/UI/ContentItem/MainResultViewerControl.xaml

## 코딩 규칙 (프로젝트 lock — 위반 시 리뷰 반려)

- **삼항 연산자 `?:` 절대 금지.** 전부 `if` / `else` 로 푼다. (단, `x == y` 같은 비교식을 bool 변수/프로퍼티에 그대로 대입하는 것은 삼항이 아니므로 허용)
- **C# 7.2 전용.** switch expression, nullable reference type, record, target-typed new 등 8.0+ 문법 금지.
- 신규 지역 변수를 만들 경우 헝가리언 접두(`b`/`n`/`sz` 등)를 붙인다. 단 **CONTEXT.md 에서 이름이 확정된 식별자(`_manualMeasureAxisMode`, `ECaliperMode`)는 지정된 이름 그대로** 쓴다.
- 주석은 **비자명한 "왜"** 만 남긴다. `//YYMMDD hbk` 날짜 주석 규칙은 2026-06-11 폐기 — 절대 새로 달지 말 것.
- 기존 파일의 브레이스 스타일을 그대로 따른다 (`MainResultViewerControl.xaml.cs` = Allman).

## 검증된 현재 코드 (이 상태를 전제로 편집한다)

### 1) 필드 선언부 — `MainResultViewerControl.xaml.cs:88-97`

```csharp
        private bool _manualToolsEnabled = true;
        private bool _manualMeasureMode;
        private bool _crosshairEnabled;
        private double _imageWidth;
        private double _imageHeight;
        private Point _panStartPoint;
        private Rect _panStartImagePart;
        private Point _lastMouseImagePoint;
        private Point? _manualMeasureStartPoint;
        private Point? _manualMeasureEndPoint;
```

### 2) 핵심 로직 — `MainResultViewerControl.xaml.cs:1669-1682`

```csharp
        private void ApplyManualMeasurePoint(Point imagePoint)
        {
            if (!_manualMeasureStartPoint.HasValue || (_manualMeasureStartPoint.HasValue && _manualMeasureEndPoint.HasValue))
            {
                _manualMeasureStartPoint = imagePoint;
                _manualMeasureEndPoint = null;
            }
            else
            {
                _manualMeasureEndPoint = imagePoint;
            }

            Render();
        }
```

### 3) 메뉴 상태 동기화 — `MainResultViewerControl.xaml.cs:1705-1719` (앞부분)

```csharp
        private void UpdateContextMenuState()
        {
            if (ManualMeasureMenuItem == null || ClearMeasureMenuItem == null)
            {
                return;
            }

            var isImageLoaded = CurrentImage != null;
            CrosshairMenuItem.IsEnabled = _manualToolsEnabled && isImageLoaded;
            CrosshairMenuItem.IsCheckable = true;
            CrosshairMenuItem.IsChecked = _crosshairEnabled;
            ManualMeasureMenuItem.IsEnabled = _manualToolsEnabled && isImageLoaded;
            ManualMeasureMenuItem.IsCheckable = true;
            ManualMeasureMenuItem.IsChecked = _manualMeasureMode;
            ClearMeasureMenuItem.IsEnabled = isImageLoaded && (_manualMeasureStartPoint.HasValue || _manualMeasureEndPoint.HasValue || _manualMeasureMode);
```

### 4) 기존 Clear 핸들러 — `MainResultViewerControl.xaml.cs:1503-1510` (신규 핸들러 삽입 위치 기준점)

```csharp
        private void ClearMeasureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _manualMeasureStartPoint = null;
            _manualMeasureEndPoint = null;
            _manualMeasureMode = false;
            UpdateContextMenuState();
            Render();
        }
```

### 5) 거리 계산 — `MainResultViewerControl.xaml.cs:1888-1893` — **절대 수정 금지**

```csharp
        private static double GetDistance(Point start, Point end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }
```

좌표가 이미 축 정렬되면 deltaX 또는 deltaY 가 0 이 되어 자동으로 올바른 거리가 나온다. (CONTEXT LOCKED)

### 6) 상태 리셋 — `MainResultViewerControl.xaml.cs:2037-2043`

```csharp
        private void ResetManualToolState()
        {
            _crosshairEnabled = false;
            _manualMeasureMode = false;
            _manualMeasureStartPoint = null;
            _manualMeasureEndPoint = null;
        }
```

### 7) 좌표계 (중요)

`GetMouseState()` 는 `new Point(col, row)` 를 만든다 → **X = column, Y = row**.
따라서 **수평 고정 = 같은 row = Y 고정**, **수직 고정 = 같은 column = X 고정**. (CONTEXT 와 일치)

### 8) csproj 는 classic 스타일 (packages.config)

신규 `.cs` 파일은 **자동 포함되지 않는다.** `<Compile Include>` 를 직접 추가하지 않으면 "형식 또는 네임스페이스 이름 'ECaliperMode' 를 찾을 수 없습니다" 컴파일 에러가 난다.

현재 `WPF_Example/DatumMeasurement.csproj:324`:
```xml
    <Compile Include="UI\ContentItem\IMainView.cs" />
```

### 9) enum 파일 관례 (`WPF_Example/Custom/Sequence/Inspection/EImageSource.cs` 원문)

```csharp
namespace ReringProject.Sequence
{
    public enum EImageSource
    {
        Horizontal = 0,  // 가로축 (TeachingImagePath). 기본값.
        Vertical   = 1   // 세로축 (TeachingImagePath_Vertical, DualImage 전용).
    }
}
```

1파일 1enum, using 없음, 짧은 인라인 주석. `MainResultViewerControl` 은 `namespace ReringProject.UI` 이므로 ECaliperMode 도 **`ReringProject.UI`** 에 두면 using 추가가 불필요하다.
</context>

<tasks>

<task type="auto">
  <name>Task 1: ECaliperMode enum 신규 + csproj 등록 + 축 모드 필드 추가</name>
  <files>
WPF_Example/UI/ContentItem/ECaliperMode.cs (신규),
WPF_Example/DatumMeasurement.csproj,
WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
  </files>
  <action>
**(1) `WPF_Example/UI/ContentItem/ECaliperMode.cs` 를 신규 생성한다.** 아래 내용 그대로 (CONTEXT LOCKED — 멤버 3개, 이름 변경 금지):

```csharp
namespace ReringProject.UI
{
    // Manual Measure(캘리퍼)에서 두 번째 점에 걸 축 제약.
    // 좌표계 주의: 뷰어 Point 는 X=column, Y=row 이다.
    public enum ECaliperMode
    {
        Free       = 0,  // 자유 방향 (기존 동작)
        Horizontal = 1,  // 수평 고정 — 끝점 Y(row)를 시작점 Y로 강제
        Vertical   = 2   // 수직 고정 — 끝점 X(column)를 시작점 X로 강제
    }
}
```

namespace 는 반드시 `ReringProject.UI` — 그래야 `MainResultViewerControl.xaml.cs` 에 using 을 새로 추가할 필요가 없다.

**(2) `WPF_Example/DatumMeasurement.csproj` 324번째 줄 바로 아래에 Compile 항목을 추가한다.**

```xml
    <Compile Include="UI\ContentItem\IMainView.cs" />
    <Compile Include="UI\ContentItem\ECaliperMode.cs" />
```

경로 구분자는 반드시 백슬래시(`\`). classic csproj 라 이 줄이 없으면 빌드가 깨진다.
csproj 는 이미 다른 작업으로 `M` 상태이므로 기존 변경분을 되돌리지 말고 **이 한 줄만 추가**한다.

**(3) `MainResultViewerControl.xaml.cs` 97번째 줄(`private Point? _manualMeasureEndPoint;`) 바로 다음에 필드를 추가한다.** (CONTEXT: 88-97 근처, 이름 LOCKED)

```csharp
        private Point? _manualMeasureEndPoint;
        private ECaliperMode _manualMeasureAxisMode = ECaliperMode.Free;
```

기본값 `Free` 가 곧 기존 동작이므로 이 시점까지는 런타임 거동 변화가 0 이다.

이 태스크에서 **다른 어떤 파일도 건드리지 않는다.**
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && \
echo "=== [1] enum 파일 (각 1) ===" ; \
grep -Fc "namespace ReringProject.UI" WPF_Example/UI/ContentItem/ECaliperMode.cs ; \
grep -Fc "public enum ECaliperMode" WPF_Example/UI/ContentItem/ECaliperMode.cs ; \
grep -Fc "Free       = 0," WPF_Example/UI/ContentItem/ECaliperMode.cs ; \
grep -Fc "Horizontal = 1," WPF_Example/UI/ContentItem/ECaliperMode.cs ; \
grep -Fc "Vertical   = 2" WPF_Example/UI/ContentItem/ECaliperMode.cs ; \
echo "=== [2] csproj 등록 (1) ===" ; \
grep -Fc 'Compile Include="UI\ContentItem\ECaliperMode.cs"' WPF_Example/DatumMeasurement.csproj ; \
echo "=== [3] 필드 (1) ===" ; \
grep -Fc "private ECaliperMode _manualMeasureAxisMode = ECaliperMode.Free;" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
echo "=== [4] 빌드 ===" ; \
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /nologo /v:minimal 2>&1 | tail -15
    </automated>
  </verify>
  <done>
- [1] 5줄 전부 `1`
- [2] `1` (csproj 등록 누락 시 다음 태스크가 전부 컴파일 실패)
- [3] `1`
- [4] msbuild 출력에 `error` 0건 (기존 warning 은 허용)
  </done>
</task>

<task type="auto">
  <name>Task 2: 축 스냅 로직 + SetManualMeasureAxisMode + 3개 클릭 핸들러</name>
  <files>WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs</files>
  <action>
**(1) `ApplyManualMeasurePoint` (1669-1682) 의 `else` 분기를 아래로 교체한다.** (CONTEXT LOCKED: `imagePoint` 대입 **직전** if-else 축 스냅)

교체 전:
```csharp
            else
            {
                _manualMeasureEndPoint = imagePoint;
            }
```

교체 후:
```csharp
            else
            {
                // 끝점 좌표 한 축을 시작점 값으로 치환해 축을 고정한다.
                // GetDistance 는 손대지 않는다 — 한쪽 delta 가 0 이 되어 자동으로 축 거리가 된다.
                if (_manualMeasureAxisMode == ECaliperMode.Horizontal)
                {
                    imagePoint.Y = _manualMeasureStartPoint.Value.Y;
                }
                else if (_manualMeasureAxisMode == ECaliperMode.Vertical)
                {
                    imagePoint.X = _manualMeasureStartPoint.Value.X;
                }

                _manualMeasureEndPoint = imagePoint;
            }
```

- `Point` 는 값 타입 struct 이고 `imagePoint` 는 값 파라미터이므로 로컬 수정이 호출자에 영향을 주지 않는다.
- `Free` 는 두 조건 모두 거짓 → 기존 동작 그대로 (회귀 0).
- `if` 문 앞뒤 나머지 줄(첫 분기, `Render();`)은 **한 글자도 바꾸지 않는다.**

**(2) `ClearMeasureMenuItem_Click` (1503-1510) 바로 다음에 아래 4개 멤버를 추가한다.**

```csharp
        private void MeasureAxisFreeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetManualMeasureAxisMode(ECaliperMode.Free);
        }

        private void MeasureAxisHorizontalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetManualMeasureAxisMode(ECaliperMode.Horizontal);
        }

        private void MeasureAxisVerticalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetManualMeasureAxisMode(ECaliperMode.Vertical);
        }

        private void SetManualMeasureAxisMode(ECaliperMode mode)
        {
            if (_manualMeasureAxisMode == mode)
            {
                // 체크 가능 MenuItem 은 클릭 시 WPF 가 IsChecked 를 제멋대로 토글한다.
                // 같은 모드를 다시 눌러도 체크가 풀리지 않도록 상태를 되돌린다.
                UpdateContextMenuState();
                return;
            }

            _manualMeasureAxisMode = mode;

            // 축 기준이 바뀌면 이미 찍힌 시작점의 기준이 무의미해지므로 진행 중 측정만 버린다.
            // _manualMeasureMode / _crosshairEnabled 는 건드리지 않는다 (측정 모드 유지 — ResetManualToolState 전체 호출 금지).
            if (_manualMeasureStartPoint.HasValue && !_manualMeasureEndPoint.HasValue)
            {
                _manualMeasureStartPoint = null;
                _manualMeasureEndPoint = null;
            }

            UpdateContextMenuState();
            Render();
        }
```

**주의 — 아직 XAML 에 메뉴가 없으므로 이 3개 핸들러는 이 시점엔 호출되지 않는다. 정상이다.** 빌드는 통과해야 한다 (미사용 private 메서드는 경고를 내지 않음). XAML 배선은 Task 3.

**(3) 손대지 않는 것 (명시적 금지):**
- `GetDistance` (1888-1893) — 한 글자도 수정 금지
- `ResetManualToolState` (2037-2043) — `_manualMeasureAxisMode` 를 여기서 리셋하지 **않는다.** 축 모드는 이미지 교체와 무관한 사용자 선호값으로 유지한다.
- `BuildTransientMessages` (1842-1866) — 안내 문구 변경 없음
- `ManualMeasureMenuItem_Click`, `ClearMeasureMenuItem_Click` 본문 — 변경 없음
- 다른 어떤 파일도 열지 않는다
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && \
echo "=== [1] 축 스냅 (각 1) ===" ; \
grep -Fc "if (_manualMeasureAxisMode == ECaliperMode.Horizontal)" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "imagePoint.Y = _manualMeasureStartPoint.Value.Y;" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "else if (_manualMeasureAxisMode == ECaliperMode.Vertical)" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "imagePoint.X = _manualMeasureStartPoint.Value.X;" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
echo "=== [2] 핸들러+세터 (각 1) ===" ; \
grep -Fc "private void MeasureAxisFreeMenuItem_Click(object sender, RoutedEventArgs e)" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "private void MeasureAxisHorizontalMenuItem_Click(object sender, RoutedEventArgs e)" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "private void MeasureAxisVerticalMenuItem_Click(object sender, RoutedEventArgs e)" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "private void SetManualMeasureAxisMode(ECaliperMode mode)" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
echo "=== [3] 불변 가드 (1 / 1 / 0 / 0) ===" ; \
grep -Fc "return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "_manualMeasureEndPoint = imagePoint;" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -c "_manualMeasureAxisMode = ECaliperMode.Free;" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs | awk '{print $1-1}' ; \
grep -c " ? .* : " WPF_Example/UI/ContentItem/ECaliperMode.cs ; \
echo "=== [4] 빌드 ===" ; \
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /nologo /v:minimal 2>&1 | tail -15
    </automated>
  </verify>
  <done>
- [1] 4줄 전부 `1`
- [2] 4줄 전부 `1`
- [3] 순서대로 `1`(GetDistance 원문 보존) / `1`(끝점 대입은 여전히 딱 한 곳) / `0`(필드 초기화 1곳 외에 Free 재대입 없음 = ResetManualToolState 미오염) / `0`
- [4] msbuild `error` 0건
  </done>
</task>

<task type="auto">
  <name>Task 3: 컨텍스트 메뉴 서브메뉴 XAML + 체크 상태 동기화</name>
  <files>
WPF_Example/UI/ContentItem/MainResultViewerControl.xaml,
WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
  </files>
  <action>
**(1) `MainResultViewerControl.xaml` 의 `ManualMeasureMenuItem`(33-35줄)과 `ClearMeasureMenuItem`(36-38줄) 사이에 서브메뉴를 삽입한다.**

삽입 후 해당 구간이 정확히 이렇게 되어야 한다:

```xml
                <MenuItem x:Name="ManualMeasureMenuItem"
                          Header="Manual Measure"
                          Click="ManualMeasureMenuItem_Click"/>
                <MenuItem x:Name="MeasureAxisMenuItem"
                          Header="측정 축 고정">
                    <MenuItem x:Name="MeasureAxisFreeMenuItem"
                              Header="자유"
                              Click="MeasureAxisFreeMenuItem_Click"/>
                    <MenuItem x:Name="MeasureAxisHorizontalMenuItem"
                              Header="수평 고정"
                              Click="MeasureAxisHorizontalMenuItem_Click"/>
                    <MenuItem x:Name="MeasureAxisVerticalMenuItem"
                              Header="수직 고정"
                              Click="MeasureAxisVerticalMenuItem_Click"/>
                </MenuItem>
                <MenuItem x:Name="ClearMeasureMenuItem"
                          Header="Clear Measure"
                          Click="ClearMeasureMenuItem_Click"/>
```

- `IsCheckable` / `IsChecked` 는 XAML 에 쓰지 않는다 — 전부 코드비하인드에서 세팅해 단일 소스로 유지한다.
- 나머지 메뉴 항목/Separator 는 **한 줄도 건드리지 않는다.**

**(2) `MainResultViewerControl.xaml.cs` 의 `UpdateContextMenuState()` 안, `ClearMeasureMenuItem.IsEnabled = ...` 줄(1719) 바로 다음에 아래 블록을 추가한다.**

```csharp
            if (MeasureAxisMenuItem != null
                && MeasureAxisFreeMenuItem != null
                && MeasureAxisHorizontalMenuItem != null
                && MeasureAxisVerticalMenuItem != null)
            {
                MeasureAxisMenuItem.IsEnabled = _manualToolsEnabled && isImageLoaded;
                MeasureAxisFreeMenuItem.IsCheckable = true;
                MeasureAxisHorizontalMenuItem.IsCheckable = true;
                MeasureAxisVerticalMenuItem.IsCheckable = true;
                MeasureAxisFreeMenuItem.IsChecked = (_manualMeasureAxisMode == ECaliperMode.Free);
                MeasureAxisHorizontalMenuItem.IsChecked = (_manualMeasureAxisMode == ECaliperMode.Horizontal);
                MeasureAxisVerticalMenuItem.IsChecked = (_manualMeasureAxisMode == ECaliperMode.Vertical);
            }
```

- 비교식을 bool 프로퍼티에 그대로 대입하는 것이므로 삼항 연산자가 아니다 (규칙 위반 아님).
- null 가드는 기존 `ManualMeasureMenuItem == null` 조기 반환과 같은 방어 패턴이다.
- 이 블록은 `UpdateContextMenuState` 의 EditRoi/Redraw 블록보다 **앞**(ClearMeasure 줄 직후)에 둔다.
- 기존 줄은 어느 것도 수정/삭제하지 않고 **추가만** 한다.

**(3) 배선 확인:** `OpenContextMenu()`(1980-1986)가 메뉴를 띄우기 전에 `UpdateContextMenuState()` 를 호출하므로 우클릭할 때마다 체크 표시가 자동 동기화된다. 이 메서드는 수정하지 않는다.
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && \
echo "=== [1] XAML 서브메뉴 (각 1) ===" ; \
grep -Fc 'x:Name="MeasureAxisMenuItem"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'x:Name="MeasureAxisFreeMenuItem"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'x:Name="MeasureAxisHorizontalMenuItem"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'x:Name="MeasureAxisVerticalMenuItem"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'Click="MeasureAxisFreeMenuItem_Click"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'Click="MeasureAxisHorizontalMenuItem_Click"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'Click="MeasureAxisVerticalMenuItem_Click"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
echo "=== [2] 기존 메뉴 보존 (각 1) ===" ; \
grep -Fc 'Click="ManualMeasureMenuItem_Click"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'Click="ClearMeasureMenuItem_Click"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
grep -Fc 'Click="CrosshairMenuItem_Click"' WPF_Example/UI/ContentItem/MainResultViewerControl.xaml ; \
echo "=== [3] 체크 동기화 (각 1) ===" ; \
grep -Fc "MeasureAxisFreeMenuItem.IsChecked = (_manualMeasureAxisMode == ECaliperMode.Free);" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "MeasureAxisHorizontalMenuItem.IsChecked = (_manualMeasureAxisMode == ECaliperMode.Horizontal);" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "MeasureAxisVerticalMenuItem.IsChecked = (_manualMeasureAxisMode == ECaliperMode.Vertical);" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
grep -Fc "MeasureAxisMenuItem.IsEnabled = _manualToolsEnabled && isImageLoaded;" WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs ; \
echo "=== [4] 영향 범위 (4줄만) ===" ; \
git status --porcelain -- WPF_Example ; \
echo "=== [5] 빌드 ===" ; \
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /nologo /v:minimal 2>&1 | tail -15
    </automated>
  </verify>
  <done>
- [1] 7줄 전부 `1`
- [2] 3줄 전부 `1` (기존 메뉴 배선 무손상)
- [3] 4줄 전부 `1`
- [4] `git status` 출력에 나타나는 WPF_Example 파일이 정확히 4개: `?? WPF_Example/UI/ContentItem/ECaliperMode.cs`, `M WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`, `M WPF_Example/UI/ContentItem/MainResultViewerControl.xaml`, `M WPF_Example/DatumMeasurement.csproj` (csproj 는 이 작업 이전부터 `M` 이었음). 그 외 파일이 하나라도 있으면 FAIL
- [5] msbuild `error` 0건
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (없음) | 이 변경은 로컬 WPF 컨텍스트 메뉴 → 인메모리 `Point` 필드 경로만 건드린다. 네트워크(TCP `VisionServer`)·파일 I/O·레시피 INI·측정 파이프라인 어디에도 새 입력 경로를 만들지 않는다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-ivy-01 | Tampering | `ApplyManualMeasurePoint` 축 스냅이 검사 판정값에 영향 | accept | Manual Measure 는 화면 표시 전용 유틸리티다. 결과 저장·공차 판정·Export 경로 어디에서도 `_manualMeasureStartPoint/_manualMeasureEndPoint` 를 읽지 않는다(Task 3 verify [4] 의 영향 범위 4파일 가드로 확인). |
| T-ivy-02 | Denial of Service | 축 모드 전환 시 상태 불일치로 렌더 예외 | mitigate | `SetManualMeasureAxisMode` 가 진행 중(시작점만 있는) 측정을 명시적으로 폐기하고, `_manualMeasureStartPoint.Value` 접근은 기존 `HasValue` 분기(`else` 가지) 안에서만 일어난다 — 새 null 역참조 경로 없음. |
</threat_model>

<verification>
## 자동 (실행자 수행)

각 태스크 `<verify>` 의 grep + msbuild 를 순서대로 통과해야 한다. Task 3 verify [4] 의 "수정 파일 4개" 가드가 회귀 0 의 1차 방어선이다.

## 사용자 실기 확인 (실행 완료 후, 앱 재빌드/재기동 필요)

1. 메인 화면에서 이미지를 띄우고 뷰어 우클릭 → **측정 축 고정** 서브메뉴에 `자유`만 체크되어 있는지 확인
2. `Manual Measure` 체크 → 임의의 두 점 클릭 → 기존과 같은 대각선 거리 표시 (자유 모드 회귀 0 확인)
3. `측정 축 고정 → 수평 고정` 선택 → 두 점 클릭 시 **선이 완전한 수평**으로 그려지고 거리 = 두 점의 가로 차이
4. `수직 고정` 선택 → 두 점 클릭 시 **선이 완전한 수직**, 거리 = 세로 차이
5. 시작점 하나만 찍은 상태에서 축 모드를 바꾸면 시작점이 사라지고 `Manual Measure: Select first point` 안내로 복귀
6. Align 탭 뷰어에서도 1~4 가 동일하게 동작 (같은 컨트롤 공유)
7. 이미지를 바꿔 로드해도 선택한 축 모드는 유지됨 (의도된 동작 — 사용자 선호값)
</verification>

<success_criteria>
- [ ] `ECaliperMode.cs` 신규 생성, `namespace ReringProject.UI`, 멤버 정확히 3개(Free/Horizontal/Vertical)
- [ ] csproj 에 `<Compile Include="UI\ContentItem\ECaliperMode.cs" />` 1줄 등록
- [ ] `_manualMeasureAxisMode` 필드가 `ECaliperMode.Free` 로 초기화되어 선언됨
- [ ] `ApplyManualMeasurePoint` else 분기에서 끝점 대입 직전 Horizontal→Y 고정 / Vertical→X 고정 스냅 적용
- [ ] `GetDistance` 원문 100% 보존 (수정 금지 계약)
- [ ] `ResetManualToolState` 원문 보존 (축 모드는 리셋 대상 아님)
- [ ] 컨텍스트 메뉴에 3-way 라디오형 서브메뉴 존재 + `UpdateContextMenuState` 에서 IsChecked 단독 동기화
- [ ] 축 모드 전환 시 "시작점만 있는" 진행 중 측정만 폐기, `_manualMeasureMode` 는 유지
- [ ] 삼항 연산자 `?:` 신규 도입 0건, C# 8.0+ 문법 0건, `//YYMMDD hbk` 주석 신규 0건
- [ ] Debug|x64 MSBuild error 0
- [ ] 수정 파일 정확히 4개 — 그 외 파일 변경 0 (회귀 0)
</success_criteria>

<output>
완료 후 `.planning/quick/260805-ivy-manual-measure-ecalipermode/260805-ivy-SUMMARY.md` 를 생성한다.
</output>
