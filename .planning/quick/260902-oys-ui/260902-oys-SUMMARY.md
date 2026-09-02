---
phase: 260902-oys-ui
plan: 01
subsystem: ui
tags: [wpf, xaml, toolbar, wrappanel, grid-layout]

requires: []
provides:
  - "검사(측정) 화면 상단 툴바에서 [브러시] 진입 버튼 숨김 (핸들러/패널/ViewModel 배선 원형 보존)"
  - "검사 툴바 Col0 WrapPanel + 컬럼 폭 재정의로 좁은 폭에서도 실제 줄바꿈 발생"
  - "MenuBar 범위 제외 판정과 임계 창 폭 산출값(1428px) 문서화"
affects: [ui, main-view-toolbar]

tech-stack:
  added: []
  patterns:
    - "진입점만 Visibility=Collapsed 로 숨기고 핸들러/패널/데이터 배선은 보존 (btn_polygonRoi 선례 재사용)"
    - "Grid Auto 컬럼은 PositiveInfinity 로 measure 되므로 WrapPanel 을 넣어도 접히지 않음 — Star(*) 컬럼 전환이 필수 전제조건"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml

key-decisions:
  - "btn_brushMask 는 Visibility=Collapsed 로만 숨기고 핸들러/brushPanel/ViewModel 배선은 전부 보존 — 되돌리기가 속성 1개 제거로 끝나고 Collapsed 요소도 XAML 로더가 정상 인스턴스화하여 NRE 가 없다"
  - "canvasToolbar Grid Col0=Auto→*, Col1=*→Auto, Col0 자식을 StackPanel→WrapPanel 로 세트 변경 — 셋 중 하나라도 빠지면 줄바꿈이 발생하지 않는다"
  - "MenuBar.xaml 은 코드 변경 없이 범위 제외 — 구조(스케일 vs 트렁케이션)와 배치 폭(ColumnSpan=3 전체 vs Col0 75* 만)이 다르고, 재계산한 임계 창 폭(~1428px)이 대상 장비 해상도(1920x1080, 단일 모니터)보다 충분히 낮다"
  - "Col0/Col1 자식 Margin 은 이번에 변경 보류 — 두 줄로 접혔을 때 행 간격이 실기 UAT 에서 불편한지 확인 후 별도 판단"

requirements-completed: [UI-BRUSH-HIDE, UI-TOOLBAR-WRAP, UI-MENUBAR-VERDICT]

coverage:
  - id: D1
    description: "검사(측정) 탭 상단 툴바에서 [브러시] 진입 버튼이 렌더되지 않는다"
    requirement: "UI-BRUSH-HIDE"
    verification:
      - kind: unit
        ref: "grep 'x:Name=\"btn_brushMask\"' 직후 8줄 내 Visibility=\"Collapsed\" 정확히 1개 (Task 1 automated verify)"
        status: pass
    human_judgment: true
    rationale: "화면 렌더 결과는 실기 UAT 로 최종 확인 필요 (아래 UAT 1)"
  - id: D2
    description: "정렬 비전(Bottom/Tray) 탭의 브러시 패널·마스크 저장·패턴 모델 재생성은 변경 전과 동일하게 동작한다"
    requirement: "UI-BRUSH-HIDE"
    verification:
      - kind: unit
        ref: "grep 로 BottomVisionView.xaml.cs / TrayVisionView.xaml.cs 의 brushPanel 은 MainView 와 별개 x:Name 인스턴스임을 확인, 두 파일 모두 무변경"
        status: pass
    human_judgment: true
    rationale: "런타임 동작(마스크 저장/모델 재생성)은 코드 정적 확인만으로 보증 불가 — UAT 2"
  - id: D3
    description: "창 폭이 좁아지면 툴바 버튼들이 다음 줄로 접히고 '체커보드 캘리브' 라벨이 잘리지 않고 전부 보인다"
    requirement: "UI-TOOLBAR-WRAP"
    verification:
      - kind: unit
        ref: "Task 2 automated verify — ColumnDefinitions *,Auto,Auto / WrapPanel 여닫는 태그 각 1개 / btn_checkerboardCalibrate 존재"
        status: pass
    human_judgment: true
    rationale: "실제 줄바꿈 시각 결과는 실기 UAT 필요 — UAT 3, 4"
  - id: D4
    description: "툴바 아래 HALCON 이미지 창이 툴바에 가려지거나 밀리지 않는다 (airspace 회귀 없음)"
    requirement: "UI-TOOLBAR-WRAP"
    verification:
      - kind: unit
        ref: "Task 2 automated verify — Border MinHeight=36 유지, 루트 Grid Row0 Height=Auto 무변경 확인"
        status: pass
    human_judgment: true
    rationale: "HWindowControlWPF 는 Win32 호스팅이라 실제 렌더 결과는 실기 확인 필요 — UAT 5"
  - id: D5
    description: "MenuBar.xaml 은 코드 변경 없이 범위에서 제외되며 판단 근거가 수치와 함께 기록된다"
    requirement: "UI-MENUBAR-VERDICT"
    verification:
      - kind: unit
        ref: "git diff --name-only -- WPF_Example/UI/MenuBar.xaml 결과 없음(무변경), 본 SUMMARY 의 'MenuBar 제외 판정' 절"
        status: pass
    human_judgment: false
  - id: D6
    description: "Debug|x64 빌드가 에러 0 으로 성공한다"
    verification:
      - kind: other
        ref: "MSBuild.exe DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 → exit 0, error_count=0"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-09-02
status: complete
---

# Quick Task 260902-oys: 검사 화면 브러시 숨김 + 툴바 자동 줄바꿈 Summary

**검사(측정) 화면 툴바에서 브러시 진입점을 숨기고, Col0 을 `*`+`WrapPanel` 로 바꿔 좁은 폭에서 실제 줄바꿈이 일어나도록 했다. MenuBar 는 구조가 다르고(스케일 vs 트렁케이션) 대상 해상도(1920x1080)에서 임계 창 폭(~1428px)보다 여유가 충분해 코드 변경 없이 범위에서 제외했다.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-09-02T13:16:00Z (추정 — 오케스트레이터 스폰 시각 기준)
- **Completed:** 2026-09-02T13:36:17Z

## Accomplishments

1. **UI-BRUSH-HIDE** — `btn_brushMask` ToggleButton 에 `Visibility="Collapsed"` 1개 속성만 추가. `btn_polygonRoi` 의 기존 선례(진입점만 숨기고 나머지 전부 보존)를 그대로 따랐다. `brushPanel` 요소, `BrushMaskToggleButton_Click` 핸들러, 저장소 내 유일한 `brushPanel.Visibility = Visibility.Visible` 전환 지점(`MainView.xaml.cs:4029`)은 전부 원형 보존.
2. **UI-TOOLBAR-WRAP** — `canvasToolbar` 내부 Grid 의 `ColumnDefinitions` 를 `Auto,*,Auto` → `*,Auto,Auto` 로 바꾸고, Col0 자식 컨테이너를 `StackPanel` → `WrapPanel` 로 교체. 자식 요소(버튼 14개 + `border_reanchorConfirm` + `Separator` + 체크박스 3개) 순서/속성은 한 글자도 변경하지 않음. `Border MinHeight="36"` 과 루트 Grid Row 0 `Height="Auto"` 는 그대로 유지.
3. **UI-MENUBAR-VERDICT** — `MenuBar.xaml` 은 무변경. 아래 "MenuBar 제외 판정" 절에 수치 근거 기록.
4. Debug|x64 빌드 exit 0, error 0 (경고는 baseline 과 동일, 게이트 대상 아님).

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainView.xaml` — 수정 (신규 파일 생성 없음)

## Task Commits

| Task | Commit | 내용 |
|------|--------|------|
| Task 1 | `ebf18190` | 검사 화면 브러시 진입점 숨김 (UI-BRUSH-HIDE) |
| Task 2 | `86090c6e` | 검사 툴바 자동 줄바꿈 — WrapPanel + 컬럼 폭 재정의 (UI-TOOLBAR-WRAP) |
| Task 3 | (본 SUMMARY 커밋에 포함) | MenuBar 범위 제외 판정 기록 + 빌드 게이트 통과 |

## Decisions Made

### 1. 브러시 진입점 보존 방식

`btn_brushMask` 를 `Visibility="Collapsed"` 로만 숨기고 핸들러/패널/ViewModel 배선은 손대지 않았다. 이유 두 가지:

1. **되돌리기가 속성 1개 제거로 끝난다** — 데이터/레시피 포맷에 영향 없음.
2. **`Visibility="Collapsed"` 요소도 XAML 로더가 정상 인스턴스화한다** — `brushPanel` 객체가 계속 살아 있으므로 `MainView.xaml.cs:121-124`(ViewModel 배선)와 `:2499`(`ReloadMaskFromDisk` null-guard 호출)가 변경 전과 동일하게 동작하고 NRE 가 발생하지 않는다.

### 2. `Auto` 컬럼 안의 WrapPanel 이 왜 접히지 않는가 — Col0 을 `*` 로 바꾼 근거

WPF `Grid` 는 `Auto` 컬럼을 `double.PositiveInfinity` 로 measure 한다. 그 안에 `WrapPanel` 을 넣으면 "사용 가능 폭이 무한"이라고 판단해 **절대 줄바꿈하지 않는다** — 패널 타입만 바꾸고 컬럼 정의를 그대로 두면 증상이 0% 개선된다. 반대로 `*`(Star) 컬럼은 `(사용 가능 폭 − Auto/픽셀 컬럼 합)` 을 비례 배분받아 **유한** 폭으로 measure 되므로 그때 비로소 WrapPanel 이 줄바꿈을 계산할 수 있다. Col1 을 동시에 `Auto` 로 내리지 않으면 `*` 컬럼이 둘이 되어 Col0/Col1 이 폭을 나눠 갖게 되고, 버튼 영역이 오히려 좁아져 줄바꿈이 더 자주 발생한다. 세 변경(Col0 `Auto→*`, Col1 `*→Auto`, 패널 `StackPanel→WrapPanel`)이 반드시 세트인 이유가 이것이다.

### 3. Col1 `Auto` 전환 트레이드오프

`label_drawHint`(Col1, 기본 `Visibility="Collapsed"`) / `label_testFindResult`(Col1, 기본 `Visibility="Collapsed"`) 는:

- **평소(Collapsed)**: `Auto` 컬럼 폭이 0 이므로 화면이 변경 전과 동일하고, 비어 있던 그 공간이 Col0 으로 넘어가 툴바 여유 폭이 늘어난다 (의도한 개선).
- **Visible 이 되는 순간(그리기 모드 힌트 표시 / TryFindDatum 결과 표시 시)**: 기존 `HorizontalAlignment="Center"` 가 컬럼 폭 = 콘텐츠 폭이 되는 `Auto` 컬럼에서는 사실상 무효가 되어, 툴바 가운데가 아니라 **버튼 블록 바로 오른쪽**에 붙어 표시된다. `Auto` 컬럼은 콘텐츠가 필요로 하는 폭을 그대로 주므로 **잘림은 없다.** 위치만 바뀐다 — 기능 손실이 아니므로 이번 스코프에서 허용.

### 4. 자식 `Margin` 미변경 보류

현재 툴바 자식들은 `Margin="0,0,4,0"`(아래 여백 0)이다. 두 줄로 접히면 위/아래 행이 시각적으로 붙어 보일 수 있으나, 여기에 아래 여백을 추가하면 **한 줄로 표시되는 평상시에도 툴바 높이가 상시 늘어나는 부작용**이 생긴다. 실측 없이 예방적으로 바꾸지 않고, 실기 UAT(아래 4번)에서 행 간격이 실제로 불편한지 확인한 뒤 별도 후속 작업으로 판단하기로 보류했다.

### 5. MenuBar 제외 판정 (수치 근거)

**구조가 다르다.** 검사 툴바는 가로 스택(현재는 WrapPanel)이라 폭이 모자라면 트렁케이션이 난다. MenuBar(`WPF_Example/UI/MenuBar.xaml:56-64`) 는 고정폭 컬럼 합계 `180+1+180+1+240+1 = 603px` 를 뗀 나머지를 마지막 컬럼(Col6, `100*`)이 전량 가져가고, Col6 내부(:94-101)도 `4*/1/4*/1/2*`, 그 안의 CONTROL 버튼 그리드(:162-169)도 기본값 `1*` 5등분이다. 즉 **잘리는 구조가 아니라 스케일되는 구조**다.

**배치 폭이 다르다.** `MainWindow.xaml:56` 에서 MenuBar 는 `Grid.ColumnSpan="3"` 로 **창 전체 폭**을 쓴다(`:24-27` 의 루트 Grid ColumnDefinitions `75*/5/25*` 전체). 검사 툴바(`MainView.xaml`)는 `MainWindow` Col0(`75*` of `75*/5/25*`) 안에서만 렌더되어 같은 창에서 약 3/4 폭만 받는다. 이 비대칭이 "1920x1080 에서 툴바만 잘리고 MenuBar 는 멀쩡한" 관측과 정확히 일치한다.

**첫 넘침 임계값.** 가장 먼저 넘칠 후보는 CONTROL 그룹(Col6 내부 `4*`)의 5등분 버튼 중 라벨이 가장 긴 '결과 리뷰어'(`MenuBar.xaml:212`, FontSize 12, 한글 5자 ≈ 66px)다.
- CONTROL 그룹 폭 ≈ `(4/10) × (Col6 − 2px)` (Col6 내부 `4*/1/4*/1/2*` 중 첫 `4*`, star 합계 10, 고정폭 2px 분리선 2개)
- 그 안의 버튼 하위 칸 폭 = `CONTROL그룹폭 / 5` ≈ `0.08 × Col6`
- 66px 확보 조건: `0.08 × Col6 ≥ 66` → `Col6 ≥ 825px`
- `Col6 = 창_폭 − 603` 이므로 → **창 폭 ≥ 약 1428px**

**대상 장비 대비 여유.** 오케스트레이터가 실측한 이 PC(`\\.\DISPLAY1`, 단일 모니터)는 1920×1080, WorkingArea 1920×1032. `Col6 = 1920 − 603 = 1317px`, CONTROL 버튼 하위 칸 ≈ `0.08 × 1317 ≈ 105px` — 66px 요구 대비 충분한 여유(약 1.6배). **재계산 결과 임계 창 폭(1428px)이 1920px 미만이므로 분기 조건(≥1920px 이면 판정 반전)에 해당하지 않는다.** 사용자의 "툴바만 수정" 결정과 일치.

**관측된 결함 없음 + 회귀 위험.** MenuBar Row 는 `Height="80"` 고정, 타이틀은 `FontSize="14pt"` 고정이며 파일 내 기존 주석(`:74`)에 "20pt 는 단어 중간 줄바꿈 발생" 사고 기록이 남아 있다. 대상 해상도에서 결함이 관측되지 않으므로 근거 없는 예방적 수정을 하지 않는다.

**결론:** `WPF_Example/UI/MenuBar.xaml` 무변경, 범위에서 제외.

## Deviations from Plan

None — plan executed exactly as written. 브러시 숨김(Task 1), 툴바 컬럼/패널 세트 변경(Task 2), MenuBar 제외 판정 + 빌드 게이트(Task 3) 모두 플랜대로 완료.

## Known Stubs

None.

## Threat Flags

None — 순수 WPF 프레젠테이션 레이어 변경(요소 Visibility 1건 + 패널 타입/컬럼 폭 정의)이며 신뢰 경계를 넘는 입력 처리, 네트워크 I/O, 파일 파싱, 권한 판정이 전혀 없다. 플랜의 `<threat_model>` 에 정의된 항목(T-oys-01~03, T-oys-SC) 외에 새로 발견된 표면 없음.

## Issues Encountered

None.

## Real-Machine UAT Required (사용자 별도 진행)

이 태스크는 정적 XAML 편집 + 빌드 게이트까지만 자동 검증했다. 아래 5건은 실제 장비에서 사용자가 확인해야 한다:

1. 검사 탭 툴바에 [브러시] 버튼이 보이지 않는다
2. Bottom 비전 / Tray 비전 탭의 브러시 기능(패널 열기, 마스크 저장, 패턴 모델 재생성)은 변경 전과 동일하게 동작한다
3. 검사 탭 툴바의 '체커보드 캘리브' 버튼이 잘리지 않고 전부 보인다(필요 시 두 줄로 접힘)
4. 창 폭을 줄였다 늘리면 툴바가 접혔다 펴지고, 한 줄로 돌아오면 높이도 원래(MinHeight 36px)대로 돌아온다
5. 툴바 아래 HALCON 이미지 창이 가려지거나 밀리지 않는다(airspace 회귀 없음)

## Next Phase Readiness

- 후속 후보(이번 스코프 아님): Col0/Col1 자식 `Margin` 조정 — UAT 4에서 두 줄 행 간격이 불편하다고 판단되면 별도 quick 태스크로 처리.
- MenuBar 는 현재 해상도(1920x1080)에서 여유가 충분하므로 별도 조치 불필요. 향후 더 낮은 해상도(예: 1600px 이하) 지원이 요구되면 이번 산출값(임계 ~1428px)을 재사용해 재평가.

## Self-Check: PASSED

- FOUND: `WPF_Example/UI/ContentItem/MainView.xaml`
- FOUND: commit `ebf18190` (Task 1)
- FOUND: commit `86090c6e` (Task 2)
- FOUND: `.planning/quick/260902-oys-ui/260902-oys-SUMMARY.md`
- 신규 추가 라인 중 날짜+약칭 형식(`YYMMDD hbk`) 주석 0건
- `WPF_Example/UI/MenuBar.xaml` 무변경 (이번 태스크 시작 커밋 `e5f47297` 대비 diff 없음)
- 누적 diff(`e5f47297`..HEAD, `WPF_Example/` 하위): `MainView.xaml` 단 하나만 변경됨
