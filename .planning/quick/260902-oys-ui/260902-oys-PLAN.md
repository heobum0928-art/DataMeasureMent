---
phase: 260902-oys-ui
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml
autonomous: true
requirements:
  - UI-BRUSH-HIDE
  - UI-TOOLBAR-WRAP
  - UI-MENUBAR-VERDICT
quick_id: 260902-oys

must_haves:
  truths:
    - "검사(측정) 탭 상단 툴바에 [브러시] 토글 버튼이 렌더되지 않는다 (UI-BRUSH-HIDE)"
    - "정렬 비전(Bottom 비전 / Tray 비전) 탭의 브러시 패널·마스크 저장·패턴 모델 재생성은 변경 전과 동일하게 동작한다 (UI-BRUSH-HIDE)"
    - "창(또는 좌측 캔버스 영역) 폭이 좁아지면 툴바 버튼들이 다음 줄로 접히고, 마지막 버튼 '체커보드 캘리브' 라벨이 잘리지 않고 전부 보인다 (UI-TOOLBAR-WRAP)"
    - "창 폭을 다시 넓혀 한 줄로 돌아오면 툴바 Border 높이가 원래(MinHeight 36px)로 복귀한다 (UI-TOOLBAR-WRAP)"
    - "툴바 아래 HALCON 이미지 창(Grid.Row=1)이 툴바에 가려지거나 밀리지 않는다 — airspace 회귀 없음 (UI-TOOLBAR-WRAP)"
    - "MenuBar.xaml 은 코드 변경 없이 범위에서 제외되며, 그 판단 근거가 수치와 함께 SUMMARY 에 기록된다 (UI-MENUBAR-VERDICT)"
    - "Debug|x64 빌드가 에러 0 으로 성공한다"
    - "WPF_Example/ 하위에서 이번 작업이 실제로 수정한 소스 파일은 MainView.xaml 단 하나다"
  artifacts:
    - "WPF_Example/UI/ContentItem/MainView.xaml (수정 — 신규 파일 생성 없음)"
    - ".planning/quick/260902-oys-ui/260902-oys-SUMMARY.md"
  key_links:
    - "btn_brushMask 에 Visibility=Collapsed → BrushMaskToggleButton_Click 이 도달 불가 → brushPanel 을 Visible 로 만드는 저장소 내 유일한 경로(MainView.xaml.cs:4029)가 차단된다"
    - "툴바 Grid 의 Col0 을 '*' 로 전환 → 그 안의 WrapPanel 이 유한 폭으로 measure 됨 → 줄바꿈이 실제로 발생한다 (Auto 였다면 PositiveInfinity 로 measure 되어 절대 접히지 않는다)"
    - "툴바 Border 의 MinHeight=36 + 루트 Grid Row 0 의 Height=Auto → 접힐 때만 세로로 자라고 한 줄로 복귀하면 36px 로 돌아온다 (고정 Height 로 되돌리면 brushPanel Row 가 0px 가 되어 HALCON airspace 침범 재발)"
    - "MenuBar 는 MainWindow.xaml:56 에서 ColumnSpan=3 로 창 전체 폭을 쓰고, 검사 툴바는 MainWindow Col0(75*) 만 써서 같은 창에서 약 3/4 폭만 받는다 — 이 비대칭이 '1920 에서 툴바만 잘림' 을 설명한다"
---

<objective>
검사(측정) 화면의 브러시 모드 진입점을 숨기고, 같은 화면 상단 툴바가 해상도/패널 폭에 따라 잘리는 문제를 자동 줄바꿈으로 해소한다.

Purpose: 운영자가 측정 화면에서 쓰지 않는 브러시 기능이 노출되어 혼동을 주고, 툴바 마지막 버튼('체커보드 캘리브')이 1920x1080 에서도 이미 잘려 클릭 대상을 식별할 수 없다.
Output: `WPF_Example/UI/ContentItem/MainView.xaml` 단일 파일 수정 + MenuBar 범위 제외 판정 기록.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@WPF_Example/UI/ContentItem/MainView.xaml
</context>

<scope_boundaries>
**절대 수정 금지 (회귀 위험 / 다른 작업과 충돌):**

| 파일 | 이유 |
|------|------|
| `WPF_Example/DatumMeasurement.csproj` | 이 PC 실HW 세팅(Debug\|x64 에서 SIMUL_MODE 제거) 미커밋 로컬 변경 보유. 스테이징/커밋 절대 금지. 이 제약 때문에 **신규 `.cs` / `.xaml` 파일 생성 불가** — classic MSBuild 는 `<Compile Include>` / `<Page Include>` 등록이 필요하다. 반드시 기존 파일 안에서 해결할 것. |
| `WPF_Example/Custom/UI/BottomVisionView.xaml(.cs)` | 정렬 비전 브러시 UI — 계속 사용한다 |
| `WPF_Example/Custom/UI/TrayVisionView.xaml(.cs)` | 정렬 비전 브러시 UI — 계속 사용한다 |
| `WPF_Example/Halcon/Services/PatternMaskService.cs` | 런타임 마스크 적용 경로 — 정렬 비전이 공유한다 |
| `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` | 런타임 마스크 적용 경로 — 정렬 비전이 공유한다 |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | 4,500줄 code-behind. CLAUDE.md 가 신규 로직 추가/리팩토링을 금지한다. 이번 작업은 XAML 만으로 끝난다. |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | 동시 진행 중인 quick 태스크 260902-ov6 소유 |
| `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` | 동시 진행 중인 quick 태스크 260902-ov6 소유 |
| `WPF_Example/Device/Camera/Mil/MilCamera.cs` | 동시 진행 중인 quick 태스크 260902-ov6 소유 |
| `WPF_Example/Device/DeviceHandler.cs` | 동시 진행 중인 quick 태스크 260902-ov6 소유 |

**CLAUDE.md 하드룰 (위반 = 회귀):** 삼항/null 병합/null 조건/C# 8 switch 식 전부 금지, 헝가리언 접두사, 매직넘버 금지, 날짜+작성자약칭 형식 주석 신규 생성 금지, C# 7.2 문법만, 파일별 기존 brace 스타일 유지.

**착수 전 필수:** 아래 모든 라인번호는 조사 시점(2026-09-02) 기준이다. 편집 직전에 `grep -n` 으로 반드시 재확인할 것.
</scope_boundaries>

<tasks>

<task type="tracer">
  <name>Task 1: 검사 화면 브러시 진입점 숨김 (UI-BRUSH-HIDE)</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml</files>
  <precondition>`WPF_Example/DatumMeasurement.csproj` 가 미커밋 상태로 수정되어 있다 — 이 파일을 스테이징하거나 되돌리지 말 것. 작업 시작 전 `git status --porcelain` 으로 확인한다.</precondition>
  <read_first>
    - `WPF_Example/UI/ContentItem/MainView.xaml` 약 :166 — `btn_polygonRoi` 선례. 진입점 버튼만 감추고 enum/렌더 분기/핸들러는 전부 보존하는 이 파일의 확립된 방식이다.
    - `WPF_Example/UI/ContentItem/MainView.xaml` 약 :258-264 — `btn_brushMask` 블록 (편집 대상).
  </read_first>
  <action>
`btn_brushMask` ToggleButton (조사 시점 약 :259, `grep -n 'x:Name="btn_brushMask"'` 로 재확인) 에 `Visibility="Collapsed"` 속성 **한 개만** 추가한다. `MinWidth`/`Height`/`Style`/`Click`/`ToolTip` 등 기존 속성과 바로 위 기존 주석 라인은 그대로 둔다.

같은 파일 약 :166 의 `btn_polygonRoi` 가 이미 동일한 방식(진입점만 `Visibility="Collapsed"`, 나머지 전부 보존)으로 처리되어 있으므로 그 형태를 그대로 따른다.

**핸들러·패널·ViewModel 배선은 전부 보존한다.** 즉 `MainView.xaml` 의 `PatternBrushPanel x:Name="brushPanel"`(약 :450) 요소, `MainView.xaml.cs` 의 `BrushMaskToggleButton_Click`(약 :4020-4034) / ViewModel 배선(약 :121-124) / `ReloadMaskFromDisk` 호출(약 :2499) 을 **삭제하거나 수정하지 않는다.** 이번 태스크에서 `MainView.xaml.cs` 는 열어 읽기만 하고 편집하지 않는다.

보존하는 이유 두 가지를 SUMMARY 에 기록할 것:
1. 되돌리기가 속성 1개 제거로 끝난다(데이터/레시피 호환성 무영향).
2. `Visibility="Collapsed"` 요소도 XAML 로더가 정상 인스턴스화하므로 `brushPanel` 객체가 계속 살아 있고, 약 :121-124 / :2499 의 null-guard 배선이 변경 전과 동일하게 동작한다 — NRE 가 발생하지 않는다.

**다른 경로로 패널이 열리지 않음을 반드시 확증할 것:** 저장소 전체에서 `brushPanel` 의 `Visibility` 를 `Visible` 로 쓰는 지점은 `MainView.xaml.cs:4029` 한 곳뿐이며, 그 코드는 `BrushMaskToggleButton_Click` 안에 있다. 버튼을 숨기면 도달 불가가 된다. 편집 전 이 사실을 직접 grep 으로 재확인하고, 만약 다른 경로가 새로 발견되면 **중단하고 보고**할 것(그 경로도 함께 막아야 하므로 계획 수정이 필요하다).

주석을 새로 추가한다면 한 줄로, 6자리 날짜 + 작성자 약칭 형식은 사용하지 말 것(CLAUDE.md 하드룰). 예: 왜 숨기는지와 정렬 비전은 계속 쓴다는 사실만 적는다.
  </action>
  <verify>
    <automated>
cd /c/code/DataMeasurement && \
BL=$(grep -n 'x:Name="btn_brushMask"' WPF_Example/UI/ContentItem/MainView.xaml | cut -d: -f1) && \
echo "btn_brushMask line=$BL" && \
test "$(sed -n "${BL},$((BL+8))p" WPF_Example/UI/ContentItem/MainView.xaml | grep -c 'Visibility="Collapsed"')" = "1" && \
test "$(grep -c 'x:Name="brushPanel"' WPF_Example/UI/ContentItem/MainView.xaml)" = "1" && \
test "$(grep -c 'BrushMaskToggleButton_Click' WPF_Example/UI/ContentItem/MainView.xaml.cs)" = "1" && \
test "$(grep -c 'brushPanel.Visibility = Visibility.Visible' WPF_Example/UI/ContentItem/MainView.xaml.cs)" = "1" && \
test "$(git diff --name-only -- WPF_Example/ | sort | tr '\n' '|')" = "WPF_Example/DatumMeasurement.csproj|WPF_Example/UI/ContentItem/MainView.xaml|" && \
test "$(git diff -U0 -- WPF_Example/UI/ContentItem/MainView.xaml | grep '^+' | grep -cE '[0-9]{6} hbk')" = "0" && \
echo TASK1_OK
    </automated>
  </verify>
  <done>
`btn_brushMask` 가 `Visibility="Collapsed"` 를 가진다. `brushPanel` 요소, `BrushMaskToggleButton_Click` 핸들러, 유일한 Visible 전환 지점(`MainView.xaml.cs:4029`)이 모두 원형 그대로 남아 있다. `WPF_Example/` 하위 변경 파일은 csproj(기존 로컬 변경) + MainView.xaml 뿐이다. 추가된 라인에 날짜+약칭 형식 주석이 없다.
  </done>
</task>

<task type="auto">
  <name>Task 2: 상단 툴바 자동 줄바꿈 — StackPanel → WrapPanel + 컬럼 폭 재정의 (UI-TOOLBAR-WRAP)</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml</files>
  <read_first>
    - `WPF_Example/UI/ContentItem/MainView.xaml` 약 :143-160 — `canvasToolbar` Border, 내부 Grid 의 RowDefinitions/ColumnDefinitions, Col0 가로 스택 패널 시작 태그. 위쪽 기존 주석에 "고정 높이면 브러시 패널 Row 가 0px 가 되어 HALCON 창 airspace 를 침범한다" 는 사고 기록이 있다 — 반드시 읽을 것.
    - `WPF_Example/UI/ContentItem/MainView.xaml` 약 :420 — Col0 패널의 닫는 태그.
    - `WPF_Example/UI/ContentItem/MainView.xaml` 약 :421-453 — Col1/Col2 자식들(`label_drawHint`, `label_pointCount`, `panel_hoverInfo`, `label_testFindResult`)과 `brushPanel`.
    - `WPF_Example/Custom/UI/BottomVisionView.xaml` :108 / :140 — 이 저장소의 기존 `WrapPanel` 사용 선례(읽기만, 수정 금지).
  </read_first>
  <action>
**원인:** Col0 의 패널이 `Orientation="Horizontal"` 가로 스택이라 줄바꿈을 하지 않는다. 폭이 모자라면 그대로 잘린다. 1920x1080 에서도 툴바는 창 전체 폭이 아니라 `MainWindow.xaml` Col0(`75*` of `75*` / `5px` / `25*`) 만 받으므로 이미 넘친다.

세 가지를 함께 바꾼다. **셋 중 하나라도 빠지면 줄바꿈이 일어나지 않는다.**

1. `canvasToolbar` 내부 Grid 의 `ColumnDefinitions`(조사 시점 약 :155-159) 를 다음 순서로 바꾼다: Col0 = `*`, Col1 = `Auto`, Col2 = `Auto`.

2. Col0 의 `Grid.Column="0"` 가로 스택 패널 여는 태그(약 :160) 를 `<WrapPanel Grid.Column="0" Orientation="Horizontal">` 로 교체하고, 대응하는 닫는 태그(약 :420, `chk_overlayPattern` 바로 다음 줄) 를 `</WrapPanel>` 로 교체한다. **자식 요소(버튼 14개 + `border_reanchorConfirm` + `Separator` + 체크박스 3개)의 순서·속성·주석은 한 글자도 바꾸지 않는다.** 여는/닫는 태그 두 줄만 교체하는 것이 이 단계의 전부다.

3. `RowDefinitions`(약 :151-154), Border 의 `MinHeight="36"`, 루트 Grid Row 0 의 `Height="Auto"` 는 **절대 건드리지 않는다.** 세로 성장은 이 조합이 이미 안전하게 처리한다.

**왜 Col0 을 `*` 로 바꿔야 하는가 (SUMMARY 에 반드시 기록):**
WPF Grid 는 `Auto` 컬럼을 `double.PositiveInfinity` 폭으로 measure 한다. 그 안에 `WrapPanel` 을 넣으면 사용 가능 폭이 무한이라고 판단해 **절대 접히지 않는다** — 패널만 바꾸고 컬럼 정의를 그대로 두면 증상이 하나도 개선되지 않는다. 반면 `*` 컬럼은 (사용 가능 폭 − Auto/픽셀 컬럼 합) 을 비례 배분받아 **유한** 폭으로 measure 되므로 그때 비로소 줄바꿈이 발생한다. 그리고 Col1 을 `Auto` 로 내리지 않으면 `*` 가 둘이 되어 폭을 나눠 갖게 되고 버튼 영역이 오히려 좁아진다.

**트레이드오프 (SUMMARY 에 반드시 기록):**
- Col1 의 `label_drawHint` / `label_testFindResult` 는 둘 다 기본 `Visibility="Collapsed"` 다. Collapsed 인 동안 `Auto` 컬럼 폭은 0 이므로 변경 전과 화면이 동일하고, 비어 있던 그 공간이 Col0 으로 넘어간다(의도한 개선).
- 이 둘이 Visible 이 되면 `HorizontalAlignment="Center"` 가 사실상 무효가 되어, 툴바 가운데가 아니라 **버튼 블록 바로 오른쪽**에 붙어 표시된다. `Auto` 컬럼은 내용 폭을 그대로 주므로 **잘림은 없다.** 위치만 바뀐다.
- Col2(`label_pointCount`, `panel_hoverInfo` 호버 좌표/밝기)는 `Auto` 유지이므로 위치·동작 불변이다.

**자식 `Margin` 은 이번에 바꾸지 않는다.** 현재 자식들은 `Margin="0,0,4,0"`(아래 여백 0)이라 두 줄로 접히면 행이 붙어 보인다. 여기에 아래 여백을 주면 한 줄일 때도 툴바가 상시 높아지는 부작용이 생기므로, 실기 UAT 에서 행 간격이 실제로 불편한지 확인한 뒤 별도로 판단한다. 이 보류 결정을 SUMMARY 에 적을 것.

주석을 추가한다면 6자리 날짜 + 작성자 약칭 형식은 사용하지 말 것(CLAUDE.md 하드룰).
  </action>
  <verify>
    <automated>
cd /c/code/DataMeasurement && \
CT=$(grep -n 'x:Name="canvasToolbar"' WPF_Example/UI/ContentItem/MainView.xaml | cut -d: -f1) && \
echo "canvasToolbar line=$CT" && \
test "$(sed -n "${CT},$((CT+18))p" WPF_Example/UI/ContentItem/MainView.xaml | grep -A3 '<Grid.ColumnDefinitions>' | tr -d ' \n')" = '<Grid.ColumnDefinitions><ColumnDefinitionWidth="*"/><ColumnDefinitionWidth="Auto"/><ColumnDefinitionWidth="Auto"/>' && \
test "$(grep -c '<WrapPanel Grid.Column="0" Orientation="Horizontal">' WPF_Example/UI/ContentItem/MainView.xaml)" = "1" && \
test "$(grep -c '</WrapPanel>' WPF_Example/UI/ContentItem/MainView.xaml)" = "1" && \
test "$(sed -n "${CT},$((CT+18))p" WPF_Example/UI/ContentItem/MainView.xaml | grep -c 'MinHeight="36"')" = "1" && \
test "$(grep -c 'x:Name="btn_checkerboardCalibrate"' WPF_Example/UI/ContentItem/MainView.xaml)" = "1" && \
test "$(grep -c 'x:Name="panel_hoverInfo"' WPF_Example/UI/ContentItem/MainView.xaml)" = "1" && \
test "$(grep -c 'x:Name="label_testFindResult"' WPF_Example/UI/ContentItem/MainView.xaml)" = "1" && \
test "$(git diff -U0 -- WPF_Example/UI/ContentItem/MainView.xaml | grep '^+' | grep -cE '[0-9]{6} hbk')" = "0" && \
echo TASK2_OK
    </automated>
  </verify>
  <done>
Col0 이 `*`, Col1/Col2 가 `Auto` 다. Col0 자식 컨테이너가 `WrapPanel` 이고 여는/닫는 태그가 각각 정확히 1개다. Border `MinHeight="36"` 이 유지된다. 툴바 자식 요소(체커보드 캘리브 버튼, 호버 정보 패널, Test Find 결과 라벨)가 하나도 유실되지 않았다. 추가된 라인에 날짜+약칭 형식 주석이 없다.
  </done>
</task>

<task type="auto">
  <name>Task 3: MenuBar 범위 제외 판정 기록 + Debug|x64 빌드/회귀 게이트 (UI-MENUBAR-VERDICT)</name>
  <files>.planning/quick/260902-oys-ui/260902-oys-SUMMARY.md</files>
  <precondition>MSBuild 이 `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` 에 존재한다 (PATH 에는 없다). 없으면 중단하고 보고할 것.</precondition>
  <read_first>
    - `WPF_Example/UI/MenuBar.xaml` :53-63 — 루트 Grid 의 Row/Column 정의 (읽기 전용).
    - `WPF_Example/UI/MenuBar.xaml` :90-101, :161-173 — Col6 내부 비례 컬럼과 CONTROL 그룹 5등분 (읽기 전용).
    - `WPF_Example/UI/MenuBar.xaml` :204-222 — CONTROL 그룹 중 라벨이 가장 긴 '결과 리뷰어'(FontSize 12) / '통계분석' 버튼 (읽기 전용).
    - `WPF_Example/MainWindow.xaml` :24-27, :56-57, :61 — MenuBar 는 ColumnSpan=3 (창 전체 폭), MainView 영역은 Col0 `75*` (읽기 전용).
  </read_first>
  <action>
**`WPF_Example/UI/MenuBar.xaml` 은 코드 변경 없이 범위에서 제외한다.** 아래 근거를 직접 재확인한 뒤 수치와 함께 SUMMARY 에 기록하는 것이 이 태스크의 산출물이다.

재확인할 근거 4가지:
1. **구조가 다르다.** 검사 툴바는 가로 스택이라 폭이 모자라면 하드 트렁케이션이 난다. MenuBar 는 고정폭 컬럼 합계 `180+1+180+1+240+1 = 603px` 를 뗀 나머지를 Col6 이 `100*` 로 전량 가져가고, Col6 내부도 `4*/1/4*/1/2*`, 그 안의 버튼 그리드도 전부 `*` 다. 즉 **잘리는 구조가 아니라 스케일되는 구조**다.
2. **배치 폭이 다르다.** MenuBar 는 `MainWindow.xaml` :56 의 DockPanel 에서 `Grid.ColumnSpan="3"` 로 **창 전체 폭**을 쓴다. 검사 툴바는 `MainWindow` Col0(`75*` of `75*`/`5px`/`25*`) 안에 있어 같은 창에서 약 3/4 폭만 받는다. 이 비대칭이 "1920x1080 스크린샷에서 툴바만 잘리고 MenuBar 는 멀쩡한" 관측과 정확히 일치한다.
3. **첫 넘침 임계값을 산출한다.** 가장 먼저 넘치는 요소는 CONTROL 그룹(Col6 내부 `4*`, 하위 5등분)의 '결과 리뷰어' 라벨(FontSize 12, 한글 5자 + 공백 ≈ 66px)이다. 하위 칸 폭 = `(4/10) × Col6 ÷ 5 = 0.08 × Col6`. 66px 확보 조건은 `Col6 ≥ 825px`, 즉 `창 폭 ≥ 약 1428px`. 1920 에서 Col6 = `1920 − 603 = 1317px` 로 충분한 여유가 있다. 실제 폰트 메트릭으로 재계산해 산출값을 SUMMARY 에 적을 것.
4. **관측된 결함이 없고 회귀 위험이 있다.** Row 는 `Height="80"` 고정, 타이틀은 `FontSize="14pt"` 고정이며 파일 주석에 "20pt 는 단어 중간 줄바꿈 발생" 사고 기록이 남아 있다. 대상 장비 해상도(1920x1080)에서 결함이 관측되지 않으므로 근거 없는 예방적 수정을 하지 않는다.

**분기 조건:** 재계산 결과 임계 창 폭이 1920px **이상**으로 나오면(= 대상 장비에서도 실제로 넘친다는 뜻) 위 판정이 뒤집힌다. 이때는 코드를 고치지 말고 **즉시 중단하고 산출값과 함께 보고**할 것 — 범위 확대는 사용자 확인이 필요하다.

이어서 전체 게이트를 실행한다: Debug|x64 빌드 에러 0, 금지 파일 미변경 확인. 빌드 경고는 게이트 대상이 아니다(이 저장소의 경고 baseline 은 SIMUL-ON 18줄 / SIMUL-OFF 16줄이며 '경고 0' 은 항상 거짓 실패다). MSBuild 는 MSYS 경로 변환 때문에 **대시 형식 인자**(`-p:` `-v:` `-nologo`)만 쓸 것.

마지막으로 SUMMARY 에 실기 UAT 항목 5건을 그대로 옮겨 적을 것(사용자가 별도 진행):
1. 검사 탭 툴바에 [브러시] 버튼이 보이지 않는다
2. Bottom 비전 / Tray 비전 탭의 브러시 기능은 그대로 동작한다
3. 검사 탭 툴바의 '체커보드 캘리브' 버튼이 잘리지 않고 전부 보인다(필요 시 두 줄로 접힘)
4. 창 폭을 줄였다 늘리면 툴바가 접혔다 펴지고, 한 줄로 돌아오면 높이도 원래대로 돌아온다
5. 툴바 아래 HALCON 이미지 창이 가려지거나 밀리지 않는다(airspace 회귀 없음)

`.planning/` 산출물 커밋 시 `WPF_Example/DatumMeasurement.csproj` 를 스테이징하지 말 것. 파일을 명시 지정해 `git add` 할 것.
  </action>
  <verify>
    <automated>
cd /c/code/DataMeasurement && \
MSB="/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" && \
test -f "$MSB" && \
LOG=/c/Users/admin/AppData/Local/Temp/oys-build.log && \
"$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$LOG" 2>&1; \
BUILD_RC=$?; tail -20 "$LOG"; \
echo "msbuild_exit=$BUILD_RC" && test "$BUILD_RC" = "0" && \
test "$(grep -cE 'error [A-Z]+[0-9]+' "$LOG")" = "0" && \
test "$(git diff --name-only -- WPF_Example/ | sort | tr '\n' '|')" = "WPF_Example/DatumMeasurement.csproj|WPF_Example/UI/ContentItem/MainView.xaml|" && \
test -f .planning/quick/260902-oys-ui/260902-oys-SUMMARY.md && \
test "$(grep -c '1428\|결과 리뷰어' .planning/quick/260902-oys-ui/260902-oys-SUMMARY.md)" -ge "1" && \
echo TASK3_OK
    </automated>
    <human-check>
실기 UAT 5건(위 action 목록)은 사용자가 별도 진행한다. 이 태스크는 UAT 항목이 SUMMARY 에 기록되었는지까지만 책임진다.
    </human-check>
  </verify>
  <done>
MenuBar 제외 판정이 임계 창 폭 산출값과 함께 SUMMARY 에 기록되었고 `WPF_Example/UI/MenuBar.xaml` 은 변경되지 않았다. Debug|x64 빌드가 exit 0, 에러 0 으로 성공한다. `WPF_Example/` 하위 변경 파일이 csproj(기존 로컬 변경) + MainView.xaml 뿐이며 csproj 는 스테이징되지 않았다. 실기 UAT 5건이 SUMMARY 에 기록되었다.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (없음 — 이번 변경 범위 내) | 순수 WPF 프레젠테이션 레이어 변경(요소 `Visibility` 1건 + 패널 타입/컬럼 폭 정의). 신뢰 경계를 넘는 입력 처리, 네트워크 I/O, 파일 파싱, 권한 판정이 전혀 없다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-oys-01 | Denial of Service | `canvasToolbar` 레이아웃 (MainView.xaml) | low | mitigate | 툴바가 세로로 무한 성장하면 HALCON 창(Grid.Row=1) 영역을 잠식해 측정 화면이 사실상 사용 불가가 될 수 있다. Row 0 `Height="Auto"` + Border `MinHeight="36"` 조합을 유지하고 자식 `Margin` 을 늘리지 않는 것으로 억제한다. Task 2 verify 가 `MinHeight="36"` 존재를 검사하고, UAT 5번이 airspace 회귀를 확인한다. |
| T-oys-02 | Tampering | 동시 진행 quick 태스크 260902-ov6 소유 파일 + 미커밋 csproj | medium | mitigate | 교차 수정 시 상대 작업 유실 또는 실HW 빌드 설정 파괴. `scope_boundaries` 금지 목록을 두고, Task 1/Task 3 verify 에서 `git diff --name-only` 결과가 정확히 {csproj, MainView.xaml} 임을 등가 비교로 검증한다. csproj 는 스테이징 금지. |
| T-oys-03 | Elevation of Privilege | `btn_brushMask` 숨김 | low | accept | 브러시 숨김은 보안 통제가 아니라 UI 정리다. 저장소 내 유일한 패널 개방 경로(`MainView.xaml.cs:4029`)가 이 버튼 뒤에 있어 UI 상 도달 불가가 되지만, 마스크 적용 런타임 경로(PatternMaskService / `SystemSetting.UsePatternBrushMask`)는 정렬 비전이 공유하므로 의도적으로 그대로 둔다. 권한 상승 위험 없음. |
| T-oys-SC | Tampering | npm/pip/cargo installs | n/a | accept | 이번 작업에 패키지 설치가 없다. 신규 의존성 0, 신규 파일 0(csproj 등록 불가 제약). 공급망 표면 변화 없음. |
</threat_model>

<verification>
1. Task 1~3 의 `<automated>` 게이트가 모두 통과한다 (`TASK1_OK` / `TASK2_OK` / `TASK3_OK`).
2. Debug|x64 빌드 exit 0, 에러 0. 경고는 게이트 대상이 아니다 (baseline SIMUL-ON 18줄 / SIMUL-OFF 16줄).
3. `git diff --name-only -- WPF_Example/` 결과가 정확히 `WPF_Example/DatumMeasurement.csproj` + `WPF_Example/UI/ContentItem/MainView.xaml` 두 줄이다. csproj 는 커밋에 포함되지 않는다.
4. 신규 `.cs` / `.xaml` 파일이 0개다 (csproj 수정 불가 제약).
5. `WPF_Example/UI/ContentItem/MainView.xaml` 에 추가된 라인에 날짜+작성자약칭 형식 주석이 없다.
6. 실기 UAT 5건이 SUMMARY 에 기록되었다 (사용자가 별도 진행).
</verification>

<success_criteria>
- `btn_brushMask` 가 `Visibility="Collapsed"` 이고, 핸들러/`brushPanel`/ViewModel 배선은 전부 원형 보존
- 툴바 Col0 이 `*` + `WrapPanel`, Col1/Col2 가 `Auto` — WrapPanel 이 유한 폭 제약을 받아 실제로 접힌다
- Border `MinHeight="36"` 과 Row 0 `Height="Auto"` 유지 (airspace 회귀 방지)
- MenuBar 제외 판정이 임계 창 폭 산출값과 함께 문서화되고 파일은 무변경
- Debug|x64 빌드 에러 0
- 금지 파일 10종 전부 무변경
</success_criteria>

<output>
Create `.planning/quick/260902-oys-ui/260902-oys-SUMMARY.md` when done.

SUMMARY 에 반드시 포함할 것:
- 브러시 진입점 보존 방식 채택 이유 2가지 (되돌리기 1줄 / Collapsed 요소도 인스턴스화되어 NRE 없음)
- `Auto` 컬럼 안의 WrapPanel 이 왜 접히지 않는지, Col0 을 `*` 로 바꾼 근거
- Col1 `Auto` 전환 트레이드오프 (`label_drawHint` / `label_testFindResult` 가 가운데 정렬 → 버튼 블록 우측 배치, 잘림은 없음)
- 자식 `Margin` 미변경 보류 결정과 그 이유
- MenuBar 제외 판정 + 임계 창 폭 산출값(약 1428px)과 대상 해상도 1920 대비 여유
- 실기 UAT 5건 (사용자 별도 진행)
</output>
