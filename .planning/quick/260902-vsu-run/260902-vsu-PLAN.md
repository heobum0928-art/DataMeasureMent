---
phase: quick-260902-vsu
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml
autonomous: false
requirements: [QUICK-260902-VSU]

must_haves:
  truths:
    - "1920 최대화 + 스플리터 기본 위치에서 RUN 버튼에 'RUN' 세 글자가 온전히 보인다 (현재는 'RUI' 로 잘림)"
    - "같은 헤더의 '일괄검사' / '일괄Export' / '...' 버튼 라벨도 잘리지 않는다"
    - "레시피명(FAI_1)이 tb_RecipeName 텍스트박스에 온전히 표시된다"
    - "Debug|x64 빌드 에러 0 (XAML 파싱 오류 포함)"
    - "변경 파일은 InspectionListView.xaml 단 1개이며 DatumMeasurement.csproj 는 스테이징/커밋되지 않는다"
  artifacts:
    - "WPF_Example/UI/ControlItem/InspectionListView.xaml — 헤더 Grid 컬럼이 `*` + `Auto`×4 로 변경됨"
  key_links:
    - "헤더 Grid ColumnDefinition(Auto) ↔ 각 Button 의 desired width — Auto 가 콘텐츠 실측폭을 컬럼폭으로 삼아 잘림을 구조적으로 제거한다"
    - "btn_start 내부 Image 의 명시 Width/Height ↔ btn_start desired width — 크기 미지정 128×128 이미지가 행 높이만큼(약 28px) 폭을 잠식하던 근본 원인"
    - "MainWindow.xaml 루트 컬럼 75*/5/25* ↔ 우측 패널 총 가용폭 — 이번 작업에서 변경하지 않는다"
---

<objective>
검사목록 헤더(`InspectionListView.xaml`)의 RUN 버튼 텍스트 잘림을 폭 예산 재설계로 제거한다.

Purpose: 1920 최대화 + 스플리터 기본 위치(우측 패널 약 480px)에서 RUN 버튼이 "RUI" 로 잘리고, 같은 헤더의 `일괄Export` 도 경계에 걸려 있다. 헤더 5컬럼이 전부 비례(`*`) 폭이라 패널이 좁아질수록 모든 버튼이 동시에 잘린다.
Output: `WPF_Example/UI/ControlItem/InspectionListView.xaml` 헤더 Grid 1곳 수정 (순수 XAML).
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

@WPF_Example/UI/ControlItem/InspectionListView.xaml
</context>

<analysis>

## 원인 (플래너 실측 확인 완료)

- `WPF_Example/MainWindow.xaml` :24-28 — 루트 Grid `75* / 5 / 25*`. 1920 최대화에서 우측 패널 ≈ 479px, `Border` 1px×2 를 빼면 헤더 가용폭 ≈ **478px**.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` :157-163 — 헤더 Grid 5컬럼 `6* / 2* / 3* / 3* / 2*` (합 16). 1단위 = 478/16 ≈ **29.9px**.
- `btn_start` (:169-174) 의 `<Image Source="/resource/process.png">` 에 `Width`/`Height` 가 **없다**. 원본 PNG 는 **128×128** (IHDR 실측). 기본 `Stretch="Uniform"` + 가로 `StackPanel` 안에서 높이만 28px 로 제약되므로 **폭도 28px** 를 먹는다.
- `HorizontalAlignment="Left"` 라 부족분은 항상 오른쪽 끝에서 잘린다 → 마지막 글자 "N" 이 사라져 "RUI" 로 보인다.
- 코드비하인드(`InspectionListView.xaml.cs`)는 이 헤더 Grid 의 컬럼을 **전혀 조작하지 않는다** (grep 확인: `btn_RecipeSelect.IsEnabled`, `tb_RecipeName.Text`, `btn_batchExport.IsEnabled` 만 사용). → 순수 XAML 수정으로 종결 가능.
- 이 헤더는 프로젝트 내 **이 파일에만** 존재한다 (`btn_batchExport` grep 결과 1파일).

## 폭 예산 — 변경 전

필요폭은 Segoe UI / Malgun Gothic 평균 자폭 기반 **추정치**다 (한글 1자 ≈ 1.0em, `RUN`@18 ≈ 37px, `Export`@16 ≈ 50px). 버튼 chrome = 테두리 2 + 기본 Padding 2 ≈ 4px.

**1920 (헤더 478px, 1단위 29.9)**

| 컨트롤 | 컬럼 | 컬럼폭 | 콘텐츠 가용 | 필요(추정) | 판정 |
|---|---|---|---|---|---|
| tb_RecipeName | 6* | 179 | 171 | 53 (`FAI_1`@19) | OK |
| btn_start `RUN` | 2* | 60 | 56 | 65 (아이콘 28 + 텍스트 37) | **-9 잘림** |
| btn_batchRun `일괄검사` | 3* | 90 | 82 | 64 | OK |
| btn_batchExport `일괄Export` | 3* | 90 | 82 | 82 | **±0 경계** |
| btn_RecipeSelect `...` | 2* | 60 | 52 | 15 | OK |

**1280 (헤더 317px, 1단위 19.8)**

| 컨트롤 | 컬럼폭 | 콘텐츠 가용 | 필요 | 판정 |
|---|---|---|---|---|
| tb_RecipeName | 119 | 111 | 53 | OK |
| btn_start | 40 | 36 | 65 | **-29 (R 만 남음)** |
| btn_batchRun | 59 | 51 | 64 | **-13** |
| btn_batchExport | 59 | 51 | 82 | **-31** |
| btn_RecipeSelect | 40 | 32 | 15 | OK |

## 선택한 방향과 근거 — (a′) Auto 컬럼 + (b) 아이콘 크기 명시 + (c) 정렬 교정

브리프의 후보 (a)(b)(c) 조합을 채택하되, (a) 를 **비례 재배분 대신 `Auto`** 로 간다.

- **비례 재배분(순수 `*`)은 1280 을 구조적으로 못 푼다.** 1280 헤더 총폭 317px 에 대해 5개 컨트롤 필요합계가 약 301px 이라 여유가 5% 뿐이고, 16분모/32분모로 반올림하는 순간 `일괄Export` 또는 레시피명이 다시 잘린다. 반면 `Auto` 는 버튼폭이 **콘텐츠 실측치로 결정**되므로 해상도와 무관하게 라벨이 잘리지 않는다.
- **(b) 아이콘 크기 명시는 필수다.** 크기 미지정 128×128 이미지가 행 높이(28px)만큼 폭을 먹는 현재 구조는 `Auto` 로 바꿔도 그대로 남는다. `Width="20" Height="20"` 로 고정해 28 → 20 으로 8px 회수하고 폭을 예측 가능하게 만든다.
- **(c) `HorizontalAlignment="Left"` → `Center`.** `Auto` 컬럼에서는 잘림이 사라지므로 정렬은 순수 미관 문제가 되고, 나머지 3개 버튼(모두 `Center`)과 일관성을 맞춘다.
- **(d) Viewbox 는 채택하지 않는다.** 글자 크기가 형제 버튼(`일괄검사`/`일괄Export`, FontSize 16)과 어긋나 헤더가 들쭉날쭉해진다.
- **`Padding="4,0"` 추가.** `Auto` 는 텍스트를 기본 Padding 1px 로 바짝 감싸므로 현재(`일괄검사` 좌우 여백 약 9px)보다 답답해 보인다. 좌우 4px 로 최소 여백을 복원한다. 8px 이상은 1280 에서 레시피명 예산을 잠식하므로 쓰지 않는다.
- **`ClipToBounds="True"`.** 사용자가 스플리터를 극단적으로 당겨 패널이 Auto 합계(264px)보다 좁아지면 우측 버튼이 헤더 밖으로 나가 좌측 이미지 패널을 침범할 수 있다. 헤더 Grid 에서 잘리도록 고정한다.

## 폭 예산 — 변경 후

버튼 desired width (아이콘 20, Padding 4,0, 테두리 2, Margin 포함):
`btn_start` 67 / `btn_batchRun` 78 / `btn_batchExport` 96 / `btn_RecipeSelect` 23 → **Auto 합계 264px**

| 창 폭 | 헤더 가용 | Auto 버튼 합 | RecipeName(`*`) 잔여 | 판정 |
|---|---|---|---|---|
| 1920 (패널 479) | 478 | 264 | **214** | 버튼 4개 전부 자연폭 → 잘림 0. 레시피명 여유 충분 (필요 53) |
| 1280 (패널 319) | 317 | 264 | **53** | 버튼 4개 전부 잘림 0. 레시피명 콘텐츠 약 45 vs 필요 47 → `FAI_1` 마지막 글자가 아슬아슬 |

**한계 명시**: 창 폭 약 **1310px 미만**(우측 패널 약 326px 미만)부터는 레시피명 텍스트가 먼저 잘리기 시작한다. 버튼 라벨은 패널 264px 까지 온전하고, 그 아래에서는 우측 끝 버튼부터 클리핑된다. 1280 에서 레시피명까지 완벽히 살리려면 `일괄Export` 라벨 축약이나 폰트 축소가 필요한데, 브리프 우선순위(버튼 라벨 > 읽기전용 레시피명)와 과도한 축소 금지 지침에 따라 채택하지 않는다.

필요폭 수치는 전부 추정이다. **최종 판정은 Task 2 의 실기 육안 확인**이다.

</analysis>

<tasks>

<task type="tracer" tdd="false">
  <name>Task 1: 검사목록 헤더 폭 예산 재설계 (컬럼 Auto + 아이콘 크기 명시 + 정렬 교정)</name>
  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml</files>
  <read_first>
    편집 전 `sed -n '150,190p' WPF_Example/UI/ControlItem/InspectionListView.xaml` 로 **라인번호를 반드시 재확인**할 것. 아래 라인번호는 플래닝 시점 기준이다.
    - :155 헤더 Grid 시작 `<Grid Grid.Column="0" Grid.Row="0" Grid.ColumnSpan="3">`
    - :156 기존 주석 라인 (Phase 51 관련) — **손대지 말 것**
    - :157-163 `Grid.ColumnDefinitions` 5개
    - :167 `tb_RecipeName`
    - :169-174 `btn_start` + 내부 StackPanel/Image/TextBlock
    - :176-184 `btn_batchRun` / `btn_batchExport` / `btn_RecipeSelect`
  </read_first>
  <action>
헤더 Grid 1곳만 수정한다. 다른 영역, 다른 파일은 건드리지 않는다.

1. 헤더 Grid 여는 태그(:155)에 `ClipToBounds="True"` 를 추가한다. 극단적으로 좁은 스플리터 위치에서 버튼이 헤더 밖으로 나가 좌측 패널을 침범하는 것을 막는 안전장치다.

2. `Grid.ColumnDefinitions`(:157-163) 5개를 아래로 교체한다. 각 줄 끝의 설명 주석(`<!-- RecipeName -->` 등)은 어떤 컨트롤인지 식별용이므로 유지한다. **날짜/이니셜이 들어간 형식의 주석은 새로 만들지 않는다.**
   - 1번 컬럼(RecipeName): `Width="*"` — 남는 폭을 흡수
   - 2~5번 컬럼(btn_start / btn_batchRun / btn_batchExport / btn_RecipeSelect): 전부 `Width="Auto"` — 버튼 콘텐츠 실측폭이 컬럼폭이 되어 라벨 잘림이 구조적으로 사라진다
   `MinWidth` 은 절대 넣지 말 것. Grid 셀보다 큰 MinWidth 는 자식이 셀 밖으로 넘쳐 이웃 컨트롤과 겹치게 만든다.

3. `btn_start`(:169-174):
   - Button 태그에 `Padding="4,0"` 추가. `Margin` `FontSize` `Click` `x:Name` `Grid.Column` `Grid.Row` 는 현행 유지
   - 내부 `StackPanel` 의 `HorizontalAlignment="Left"` → `HorizontalAlignment="Center"` 로 교체하고 `VerticalAlignment="Center"` 를 추가
   - `Image` 에 `Width="20" Height="20" VerticalAlignment="Center"` 를 추가. `Source` 문자열은 대소문자 포함 **한 글자도 바꾸지 말 것**(현재 동작 중인 리소스 경로다)
   - `TextBlock Text="RUN"` 에 `VerticalAlignment="Center"` 추가

4. `btn_batchRun`(:176) 과 `btn_batchExport`(:180) Button 태그에 `Padding="4,0"` 을 추가한다. 두 버튼의 `FontSize="16"`, `IsEnabled="False"`(Export), `Click`, 내부 TextBlock 은 그대로 둔다.

5. `btn_RecipeSelect`(:183) 는 변경하지 않는다. `...` 는 폭 여유가 충분하다.

6. `tb_RecipeName`(:167) 은 변경하지 않는다. `*` 컬럼이 잔여폭을 자동으로 준다.

절대 금지: `MainWindow.xaml` 의 `75*/5/25*` 수정, `MenuBar.xaml`/`MainView.xaml` 수정, `DatumMeasurement.csproj` 수정·스테이징, 신규 `.cs`/`.xaml` 파일 생성, C# 로직 추가. C# 수정이 필요하다고 판단되면 **작업을 멈추고 보고**한다.
  </action>
  <verify>
    <automated><![CDATA[
set -e
F=WPF_Example/UI/ControlItem/InspectionListView.xaml
awk '/<!-- Recipe Name -->/,/<!-- Inspection Group List -->/' "$F" > /tmp/vsu_hdr.txt

# 1) 컬럼: * 1개 + Auto 4개, 비례(N*) 컬럼 0개
test "$(grep -c 'ColumnDefinition Width="\*"' /tmp/vsu_hdr.txt)" = "1"
test "$(grep -c 'ColumnDefinition Width="Auto"' /tmp/vsu_hdr.txt)" = "4"
test "$(grep -cE 'ColumnDefinition Width="[0-9]+\*"' /tmp/vsu_hdr.txt)" = "0"
test "$(grep -c 'ColumnDefinition MinWidth\|ColumnDefinition .*MinWidth' /tmp/vsu_hdr.txt)" = "0"

# 2) 아이콘 크기 명시 + 좌측정렬 제거 + Padding 3개
grep -q 'process.png' /tmp/vsu_hdr.txt
grep 'process.png' /tmp/vsu_hdr.txt | grep -q 'Width="20"'
grep 'process.png' /tmp/vsu_hdr.txt | grep -q 'Height="20"'
test "$(grep -c 'HorizontalAlignment="Left"' /tmp/vsu_hdr.txt)" = "0"
test "$(grep -c 'Padding="4,0"' /tmp/vsu_hdr.txt)" = "3"
grep -q 'ClipToBounds="True"' /tmp/vsu_hdr.txt

# 3) 하드룰 — diff 추가 라인에 날짜+이니셜 주석 신규 0
test "$(git diff -- "$F" | grep '^+' | grep -c 'hbk')" = "0"

# 4) 범위 — 변경 파일은 이 xaml 1개, 신규 소스파일 0개
test "$(git diff --name-only -- WPF_Example | grep -v 'DatumMeasurement.csproj' | tr -d ' ')" = "$F"
test "$(git status --short | grep -cE '^\?\? .*\.(cs|xaml)$')" = "0"

# 5) 빌드 — 에러 0 (경고는 baseline 존재하므로 게이트 아님)
"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  2>&1 | tee /tmp/vsu_build.log
grep -cE ' error ' /tmp/vsu_build.log && exit 1 || true
echo VERIFY_OK
]]></automated>
  </verify>
  <done>
    헤더 Grid 컬럼이 `*` + `Auto`×4 이고, `btn_start` 아이콘이 20×20 으로 고정되며 내부 StackPanel 이 `Center` 정렬이다. 세 라벨 버튼에 `Padding="4,0"` 이 적용됐다. Debug|x64 빌드 에러 0. 변경 파일은 `InspectionListView.xaml` 단 1개이고 `DatumMeasurement.csproj` 는 스테이징되지 않았다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>
    검사목록 헤더 5컬럼의 폭 배분을 비례(`6*/2*/3*/3*/2*`)에서 `레시피명=* + 버튼 4개=Auto` 로 바꾸고, RUN 아이콘(128×128 원본, 크기 미지정)을 20×20 으로 고정, 내부 정렬을 좌측→중앙으로 교정, 라벨 버튼 3개에 좌우 4px 여백을 넣었다. 버튼 폭이 콘텐츠 실측치로 결정되므로 해상도와 무관하게 라벨이 잘리지 않아야 한다.
  </what-built>
  <how-to-verify>
    `bin/x64/Debug/DatumMeasurement.exe` 실행 후 우측 검사목록 패널 헤더를 확인한다. **스플리터를 옮기지 말고 기본 위치 그대로** 볼 것.

    1. **1920 최대화 + 스플리터 기본 위치**
       - RUN 버튼에 **"RUN" 세 글자가 온전히** 보이는가? ("RUI" / "RU" 면 실패)
       - 아이콘이 텍스트를 가리거나 위아래로 어긋나 있지 않은가?
       - `일괄검사` 4글자가 온전한가?
       - `일괄Export` 가 마지막 `t` 까지 온전한가? (변경 전 경계에 걸려 있던 항목)
       - `...` 버튼이 정상인가?
       - 레시피명 텍스트박스에 `FAI_1` 이 온전히 보이는가?
    2. **창을 1280 폭으로 줄여서** 같은 6항목을 다시 확인한다.
       - 버튼 4개 라벨은 **전부 온전해야 한다**.
       - 레시피명은 마지막 글자가 아슬아슬하게 걸릴 수 있다 — 이는 PLAN 에 명시된 알려진 한계다 (창 폭 약 1310px 미만에서 레시피명이 먼저 잘린다).
    3. **스플리터를 좌우로 드래그**해 본다. 패널을 극단적으로 좁혔을 때 버튼이 좌측 이미지 패널 위로 튀어나오지 않는지 확인한다 (헤더 경계에서 잘려야 정상).
    4. `...` 버튼의 컨텍스트 메뉴와 RUN 버튼 클릭 동작이 종전과 동일한지 확인한다 (레이아웃만 바꿨으므로 동작 변화가 있으면 실패).
  </how-to-verify>
  <resume-signal>"approved" 또는 어긋난 항목(해상도 + 버튼 이름 + 보이는 글자)을 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (없음) | 순수 로컬 UI 레이아웃 변경. 신규 입력 경로·네트워크 경계·파일 I/O·패키지 설치 없음 |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-VSU-01 | Tampering | `DatumMeasurement.csproj` (실HW 로컬 미커밋 변경) | medium | mitigate | Task 1 verify 에서 변경 파일 목록을 xaml 1개로 강제, csproj 스테이징 차단 |
| T-VSU-02 | Denial of Service | 헤더 Grid 극단 축소 시 버튼이 이웃 패널 침범 | low | mitigate | 헤더 Grid `ClipToBounds="True"` |
| T-VSU-SC | Tampering | npm/pip/cargo installs | n/a | accept | 패키지 설치 없음 — 해당 없음 |
</threat_model>

<verification>
- `awk` 로 추출한 헤더 블록에서 컬럼 정의 `*`×1 / `Auto`×4 / 비례 0 / MinWidth 0
- `process.png` Image 라인에 `Width="20"` + `Height="20"`
- 헤더 블록 내 `HorizontalAlignment="Left"` 0개, `Padding="4,0"` 3개, `ClipToBounds="True"` 1개
- diff 추가 라인에 날짜+이니셜 주석 0
- `git diff --name-only -- WPF_Example` 결과가 csproj 제외 시 xaml 1개
- 신규 `.cs`/`.xaml` 미추적 파일 0
- MSBuild Debug|x64 에러 0
- 실기 육안: 1920/1280 양쪽에서 RUN·일괄검사·일괄Export·`...` 라벨 잘림 0
</verification>

<success_criteria>
- 1920 최대화 + 스플리터 기본 위치에서 RUN 버튼이 "RUN" 으로 온전히 보인다
- `일괄검사` / `일괄Export` / `...` 도 잘리지 않는다
- 레시피명 `FAI_1` 이 정상 표시된다
- 1280 에서도 버튼 4개 라벨이 온전하다 (레시피명 축소는 문서화된 한계)
- Debug|x64 빌드 에러 0
- 변경은 `InspectionListView.xaml` 단일 파일, C# 변경 0, 신규 파일 0, csproj 미커밋
</success_criteria>

<output>
Create `.planning/quick/260902-vsu-run/260902-vsu-SUMMARY.md` when done.

SUMMARY 에 반드시 포함할 것:
- 채택한 조합((a′) Auto 컬럼 + (b) 아이콘 20×20 + (c) 중앙정렬 + Padding 4,0 + ClipToBounds)과 비례 재배분을 버린 근거
- 변경 전/후 폭 예산 표 (1920 / 1280)
- 알려진 한계: 창 폭 약 1310px 미만에서 레시피명 텍스트가 먼저 잘린다
- 실기 UAT 결과 (1920 / 1280 각 6항목)
</output>
