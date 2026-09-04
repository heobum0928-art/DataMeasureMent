---
phase: quick-260902-vsu
plan: 01
subsystem: ui
tags: [wpf, xaml, grid-layout, button-truncation]

requires: []
provides:
  - "검사목록 헤더(InspectionListView.xaml) 5컬럼을 비례(6*/2*/3*/3*/2*)에서 레시피명=* + 버튼4개=Auto 로 재설계"
  - "RUN 아이콘(128x128 원본, 크기 미지정) 을 20x20 으로 고정해 폭 잠식 제거"
affects: [ui, inspection-list-header]

tech-stack:
  added: []
  patterns:
    - "라벨이 잘리기 쉬운 버튼 그룹은 비례(N*) 대신 Auto 컬럼으로 폭을 콘텐츠 실측치에 맡긴다 — 해상도와 무관하게 잘림이 구조적으로 사라짐"
    - "고정 크기 미지정 아이콘 이미지는 StackPanel 내부에서 행 높이만큼 폭을 잠식할 수 있으므로 Width/Height 명시 필수"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml

key-decisions:
  - "비례 재배분(순수 N*) 대신 Auto 컬럼 채택 — 1280 해상도에서는 5개 컨트롤 필요폭 합계가 헤더 총폭의 약 95%라 비례 분모 반올림만으로도 재잘림이 발생하는 구조적 한계가 있었음"
  - "RUN 아이콘 Width/Height=20 고정 — 128x128 원본이 크기 미지정 상태로 28px 행 높이에 맞춰 Stretch=Uniform 되며 폭도 28px 를 먹던 근본 원인 제거"
  - "btn_start 내부 StackPanel HorizontalAlignment Left→Center — Auto 컬럼에서는 잘림이 사라져 정렬은 미관 문제가 되므로 나머지 3버튼(전부 Center)과 통일"
  - "라벨 버튼 3개에 Padding=4,0 추가 — Auto 컬럼은 기본 Padding 이 좁아 텍스트가 버튼 테두리에 바짝 붙으므로 최소 여백 복원"
  - "헤더 Grid 에 ClipToBounds=True 추가 — 스플리터를 극단적으로 좁히면 Auto 버튼 합계(264px)가 가용폭을 넘어 좌측 이미지 패널을 침범할 수 있어 헤더 경계에서 강제로 잘리도록 고정"
  - "1280 폭(우측 패널 약 319px)에서는 레시피명(FAI_1) 이 아슬아슬하게 걸릴 수 있음을 알려진 한계로 문서화 — 버튼 라벨 우선순위(브리프 지침)에 따라 레시피명 축소는 이번 범위에서 다루지 않음"

requirements-completed: [QUICK-260902-VSU]

coverage:
  - id: D1
    description: "헤더 Grid 컬럼이 레시피명=* + 버튼4개=Auto 로 변경되고 MinWidth 는 사용하지 않는다"
    requirement: "QUICK-260902-VSU"
    verification:
      - kind: unit
        ref: "Task 1 automated verify — grep 컬럼 정의: Width=\"*\" 1개, Width=\"Auto\" 4개, 비례(N*) 0개, MinWidth 0개"
        status: pass
    human_judgment: false
  - id: D2
    description: "RUN 아이콘이 20x20 으로 고정되고 내부 StackPanel 이 중앙정렬이며 라벨 버튼 3개에 Padding=4,0 이 적용된다"
    requirement: "QUICK-260902-VSU"
    verification:
      - kind: unit
        ref: "Task 1 automated verify — process.png 라인에 Width=\"20\"/Height=\"20\", HorizontalAlignment=\"Left\" 0개, Padding=\"4,0\" 3개, ClipToBounds=\"True\" 1개"
        status: pass
    human_judgment: false
  - id: D3
    description: "1920 최대화 + 스플리터 기본 위치에서 RUN 버튼이 'RUN' 세 글자로 온전히 보이고 일괄검사/일괄Export/... 라벨도 잘리지 않으며 레시피명 FAI_1 이 정상 표시된다"
    requirement: "QUICK-260902-VSU"
    verification: []
    human_judgment: true
    rationale: "실행자는 앱을 직접 실행하지 않는다(오케스트레이터 지시) — 실기 육안 확인은 오케스트레이터가 1920/1280 캡처로 직접 수행 예정 (플랜 Task 2, 아직 미실행)"
  - id: D4
    description: "1280 폭에서도 버튼 4개 라벨은 전부 온전하고(레시피명 축소는 문서화된 한계), 스플리터 극단 축소 시 버튼이 좌측 패널을 침범하지 않는다"
    requirement: "QUICK-260902-VSU"
    verification: []
    human_judgment: true
    rationale: "실기 육안 확인 필요 — 오케스트레이터가 Task 2 에서 직접 검증 예정, 이번 실행에서는 미수행"
  - id: D5
    description: "Debug|x64 빌드가 에러 0 으로 성공하고 변경 파일은 InspectionListView.xaml 단 1개이며 DatumMeasurement.csproj 는 커밋되지 않는다"
    requirement: "QUICK-260902-VSU"
    verification:
      - kind: other
        ref: "MSBuild.exe DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 → 0 error; git diff --cached --name-only → InspectionListView.xaml 단 1개"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-09-02
status: tasks_complete_pending_uat
---

# Quick Task 260902-vsu: 검사목록 헤더 RUN 버튼 잘림 제거 Summary

**검사목록 헤더 5컬럼을 비례(6*/2*/3*/3*/2*)에서 레시피명=* + 버튼4개=Auto 로 재설계하고, RUN 아이콘을 20x20 으로 고정해 "RUI" 로 잘리던 RUN 라벨을 구조적으로 제거했다. Task 2(1920/1280 실기 육안 UAT)는 오케스트레이터 확인 대기 상태다.**

## Performance

- **Duration:** 약 15분
- **Tasks:** 1/2 완료 (Task 2 는 checkpoint:human-verify — 오케스트레이터 확인 대기)
- **Files modified:** 1

## 채택한 조합과 근거

브리프의 후보 (a)(b)(c) 중 **(a′) Auto 컬럼**(비례 재배분이 아님) + **(b) 아이콘 20x20 명시** + **(c) 중앙정렬 교정** + **Padding 4,0** + **ClipToBounds** 조합을 채택했다.

- **비례 재배분(순수 `*`) 을 버린 이유**: 1280 헤더 총폭 317px 에 대해 5개 컨트롤 필요폭 합계가 약 301px 로 여유가 5%뿐이라, 16분모/32분모 반올림 오차만으로도 `일괄Export` 또는 레시피명이 다시 잘리는 구조적 한계가 있었다. `Auto` 는 버튼폭이 콘텐츠 실측치로 결정되므로 해상도와 무관하게 라벨이 잘리지 않는다.
- **아이콘 크기 명시가 필수인 이유**: 크기 미지정 128x128 이미지가 행 높이(28px)만큼 폭을 먹는 현재 구조는 `Auto` 로 바꿔도 그대로 남는다. `Width="20" Height="20"` 으로 고정해 폭을 예측 가능하게 만들었다.
- **`HorizontalAlignment="Left"` → `Center`**: `Auto` 컬럼에서는 잘림이 이미 사라지므로 정렬은 순수 미관 문제가 되고, 나머지 3개 버튼(모두 `Center`)과 일관성을 맞췄다.
- **Viewbox 는 채택하지 않음**: 글자 크기가 형제 버튼(FontSize 16)과 어긋나 헤더가 들쭉날쭉해지는 부작용이 있어 배제했다.
- **`Padding="4,0"` 추가**: `Auto` 는 텍스트를 기본 Padding 1px 로 바짝 감싸므로 좌우 4px 로 최소 여백을 복원했다. 8px 이상은 1280 에서 레시피명 예산을 잠식하므로 쓰지 않았다.
- **`ClipToBounds="True"`**: 스플리터를 극단적으로 당겨 패널이 Auto 합계(264px)보다 좁아지면 우측 버튼이 헤더 밖으로 나가 좌측 이미지 패널을 침범할 수 있어, 헤더 Grid 에서 잘리도록 고정했다.

## 폭 예산 — 변경 전 (플래너 실측/추정)

**1920 (헤더 478px, 1단위 29.9)**

| 컨트롤 | 컬럼폭 | 콘텐츠 가용 | 필요(추정) | 판정 |
|---|---|---|---|---|
| tb_RecipeName | 179 | 171 | 53 (`FAI_1`) | OK |
| btn_start `RUN` | 60 | 56 | 65 (아이콘28+텍스트37) | **-9 잘림** |
| btn_batchRun `일괄검사` | 90 | 82 | 64 | OK |
| btn_batchExport `일괄Export` | 90 | 82 | 82 | **±0 경계** |
| btn_RecipeSelect `...` | 60 | 52 | 15 | OK |

**1280 (헤더 317px, 1단위 19.8)**

| 컨트롤 | 컬럼폭 | 콘텐츠 가용 | 필요 | 판정 |
|---|---|---|---|---|
| tb_RecipeName | 119 | 111 | 53 | OK |
| btn_start | 40 | 36 | 65 | **-29 (R만 남음)** |
| btn_batchRun | 59 | 51 | 64 | **-13** |
| btn_batchExport | 59 | 51 | 82 | **-31** |
| btn_RecipeSelect | 40 | 32 | 15 | OK |

## 폭 예산 — 변경 후

버튼 desired width (아이콘 20, Padding 4,0, 테두리 2, Margin 포함):
`btn_start` 67 / `btn_batchRun` 78 / `btn_batchExport` 96 / `btn_RecipeSelect` 23 → **Auto 합계 264px**

| 창 폭 | 헤더 가용 | Auto 버튼 합 | RecipeName(`*`) 잔여 | 판정 |
|---|---|---|---|---|
| 1920 (패널 479) | 478 | 264 | **214** | 버튼 4개 전부 자연폭 → 잘림 0. 레시피명 여유 충분 |
| 1280 (패널 319) | 317 | 264 | **53** | 버튼 4개 전부 잘림 0. 레시피명 콘텐츠 약 45 vs 필요 47 → `FAI_1` 마지막 글자가 아슬아슬 |

**알려진 한계**: 창 폭 약 **1310px 미만**(우측 패널 약 326px 미만)부터는 레시피명 텍스트가 먼저 잘리기 시작한다. 버튼 라벨은 패널 264px 까지 온전하고, 그 아래에서는 우측 끝 버튼부터 클리핑된다. 1280 에서 레시피명까지 완벽히 살리려면 라벨 축약이나 폰트 축소가 필요한데, 브리프 우선순위(버튼 라벨 > 읽기전용 레시피명)에 따라 채택하지 않았다.

## 실기 UAT 결과

**미실행 — 오케스트레이터 확인 대기.** 실행 지시에 따라 실행자는 앱을 직접 실행하지 않았다(오케스트레이터가 1920/1280 캡처로 직접 확인 예정). PLAN 의 Task 2 (`checkpoint:human-verify`, gate="blocking")는 아래 6항목 × 2해상도 확인이 완료되어야 종료된다:

1. 1920 최대화 + 스플리터 기본 위치: RUN 3글자 온전 / 아이콘-텍스트 정렬 정상 / 일괄검사 4글자 온전 / 일괄Export 마지막 t까지 온전 / `...` 정상 / 레시피명 FAI_1 온전
2. 1280 폭: 버튼 4개 라벨 전부 온전 (레시피명 아슬아슬은 문서화된 한계)
3. 스플리터 극단 축소 시 버튼이 좌측 이미지 패널을 침범하지 않음
4. `...` 컨텍스트 메뉴 및 RUN 클릭 동작이 종전과 동일함(레이아웃만 변경, 동작 변화 없어야 함)

## Task Commits

Each task was committed atomically:

1. **Task 1: 검사목록 헤더 폭 예산 재설계 (컬럼 Auto + 아이콘 크기 명시 + 정렬 교정)** - `72efc25d` (fix)

Task 2 (checkpoint:human-verify, gate="blocking")는 미실행 — 오케스트레이터가 이어서 확인한다.

## Files Created/Modified
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` - 헤더 Grid 컬럼 5개(6*/2*/3*/3*/2* → */Auto/Auto/Auto/Auto), RUN 아이콘 20x20 고정, btn_start 내부 정렬 Left→Center, 라벨 버튼 3개 Padding=4,0, 헤더 Grid ClipToBounds=True

## Decisions Made
"채택한 조합과 근거" 절 참고. 요약: Auto 컬럼 + 아이콘 크기 명시 + 중앙정렬 + Padding + ClipToBounds 조합, 비례 재배분과 Viewbox 는 기각.

## Deviations from Plan

None - plan executed exactly as written. `read_first` 로 재확인한 라인번호(154-188)가 플래닝 시점 라인번호(155-184 부근)와 일치했고, 플랜에 명시된 5단계 편집을 그대로 적용했다.

## Issues Encountered
None.

## Known Stubs
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness

Task 1 커밋 완료, Debug|x64 빌드 에러 0, 변경 범위는 `InspectionListView.xaml` 단일 파일로 확인됨. `DatumMeasurement.csproj` 의 로컬 미커밋 변경(실HW 세팅)은 스테이징/커밋 대상에서 명시적으로 제외했다(`git add` 시 파일명 개별 지정, `git diff --cached --name-only` 로 매 커밋 전 확인).

**남은 작업:** Task 2(실기 UAT 체크포인트) — 오케스트레이터가 `bin/x64/Debug/DatumMeasurement.exe` 를 직접 실행해 1920/1280 캡처로 위 6항목을 확인한 뒤 승인 또는 어긋난 항목을 보고해야 이 quick task 가 최종 완료된다.

---
*Phase: quick-260902-vsu*
*Completed (Task 1 only): 2026-09-02*

## Self-Check: PASSED
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml
- FOUND: .planning/quick/260902-vsu-run/260902-vsu-SUMMARY.md
- FOUND commit: 72efc25d
