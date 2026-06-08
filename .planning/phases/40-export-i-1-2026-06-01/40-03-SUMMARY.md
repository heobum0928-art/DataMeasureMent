---
phase: 40-export-i-1-2026-06-01
plan: "03"
subsystem: ui
tags: [reviewer, out-01, halcon-airspace, datagrid, cycle-json, per-sequence, stale-results]

# Dependency graph
requires:
  - phase: 40-export-i-1-2026-06-01
    plan: "01"
    provides: "CycleResultDto + CycleResultSerializer (cycle.json 영속화/역직렬화) — 리뷰어 데이터 소스"
provides:
  - "ReviewerWindow — 비모달 결과 리뷰어 (폴더 선택 + cycle 목록 + 이미지/overlay 재렌더 + 측정표)"
  - "ReviewMeasurementRow — cycle.json 측정 행 DTO"
  - "MainWindow EPageType.Reviewer + MenuBar 리뷰어 버튼 wiring (비모달 Show)"
  - "측정표 판정별 행 색상 + 선택 행 강조 + 첫 불량 자동 포커스 + '불량만 보기' 필터"
  - "런 시작 시 시퀀스 shot 결과 초기화 + 시퀀스별 cycle.json scoping"
affects:
  - 40-04 (ExcelExportService — 리뷰어가 export 버튼 자리 제공)

# Tech tracking
tech-stack:
  patterns:
    - "HWindowControlWPF airspace 회피 — 네이티브 HWND 위 WPF 오버레이 금지, 헤더(비-airspace) 영역 배치"
    - "DataGrid 자동 선택 Dispatcher.BeginInvoke(Background) 지연 — ItemsSource 직후 동기 SelectedItem 무시 회피"
    - "런 lifecycle 결과 초기화 — OnStart 이벤트 구독 (Start/StartAll 공통 발화)"
    - "cycle.json 시퀀스 scoping — BuildDto(ownerSequenceName) 로 실행 시퀀스 소유 shot 만 직렬화"

key-files:
  created:
    - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml"
    - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs"
    - "WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs"
  modified:
    - "WPF_Example/MainWindow.xaml.cs (EPageType.Reviewer + mReviewerWindow 비모달 Show)"
    - "WPF_Example/UI/MenuBar.xaml / MenuBar.xaml.cs (결과 리뷰어 버튼)"
    - "WPF_Example/DatumMeasurement.csproj (신규 파일 등록)"
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (OnStart 결과 초기화 + BuildDto Name 전달)"
    - "WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs (BuildDto ownerSequenceName scoping)"

key-decisions:
  - "리뷰어는 재검사하지 않음 — cycle.json 역직렬화 후 라이브 경로(HalconViewerControl)와 동일하게 재렌더만"
  - "비모달 별도 Window (Show, ShowDialog 아님) — 라이브 검사 방해 없음 (D-08)"
  - "DualImage 토글을 헤더로 이동 — HWindowControlWPF(HWND) airspace 로 이미지 위 WPF 오버레이가 가려지던 결함 (CO-40-04)"
  - "런 시작 결과 초기화 범위 = 실행 시퀀스 shot (Actions[].ShotParam) — Top/Bottom/Side 병렬 실행 간섭 방지 (CO-40-06)"
  - "cycle.json = 실행한 시퀀스 소유 shot 만 (시퀀스별 cycle) — 검사 안 한 다른 시퀀스 stale 제외 (CO-40-07)"
  - "종합판정/TCP 응답 시퀀스 scoping 은 미적용 — 사용자 결정 B (별도 phase/quick 이연, CO-40-08)"

requirements-completed: [OUT-01]

# Metrics
duration: ~3h (체크포인트 다중 UAT 반복 포함)
completed: 2026-06-01
---

# Phase 40 Plan 03: 결과 리뷰어 (ReviewerWindow) Summary

**메뉴 [결과 리뷰어] 버튼으로 여는 비모달 Window — 날짜 폴더 선택 → cycle 목록 → cycle 선택 시 cycle.json 역직렬화 → 이미지/overlay 재렌더 + 측정표 표시. 체크포인트 UAT 에서 4건 hotfix(CO-40-04~07) 반영 후 사용자 "리뷰어 정상 작동" 승인.**

## Performance

- **Duration:** 약 3시간 (Task 1-3 auto + human-verify 체크포인트 다회 반복)
- **Completed:** 2026-06-01
- **Tasks:** 3 auto (ReviewerWindow XAML/cs, 폴더-스캔-재렌더 로직, MainWindow/MenuBar wiring) + 1 checkpoint(human-verify) + UAT hotfix 4건

## Accomplishments

- ReviewerWindow(.xaml/.cs) + ReviewMeasurementRow 신규 — 비모달 리뷰어 기반 구조
- MainWindow EPageType.Reviewer + MenuBar 버튼 wiring (비모달 Show)
- cycle.json 역직렬화 → HalconViewerControl 재렌더(LoadImage → SetInspectionOverlays) + 측정표
- UAT 중 발견된 4건 결함 hotfix 반영, 사용자 "리뷰어 정상 작동" 승인 (2026-06-01)

## Task Commits

1. **Task 1+2: ReviewerWindow + ReviewMeasurementRow** — `7b71d55`
2. **Task 3: MainWindow EPageType.Reviewer + MenuBar wiring** — `952c529`
3. **checkpoint:human-verify** — 사용자 승인 (아래 UAT hotfix 포함, 2026-06-01)

## UAT Hotfixes (체크포인트 중 발견·수정)

| ID | 결함 | 원인 | 수정 | Commit |
|----|------|------|------|--------|
| CO-40-01 | 수동(UI) 검사 시 cycle.json 미저장 | AddResponse 는 TCP 경로만 | OnFinish 핸들러(HandleManualCyclePersist) | `dc3617e` |
| CO-40-02 | 패널 잘림 / overlay 겹쳐 보기 불편 | 고정 레이아웃 | GridSplitter 3분할 + 행 클릭 단일 FAI 포커스 | `511e7e1` |
| CO-40-03 | DualImage 가로/세로 미구분 | 단일 이미지만 | 측정 행 클릭 시 가로/세로 토글 | `fb3a03c` |
| **CO-40-04** | **DualImage 토글 안 보임** | **HWindowControlWPF(HWND) airspace — 이미지 위 WPF 오버레이 가림** | **토글을 중앙 헤더로 이동** | `2d42b5b` |
| **CO-40-05** | **불량 위치 식별 어려움** | **판정 색 구분/포커스 없음** | **행 배경색 + 선택 강조 + 첫 불량 자동 포커스 + '불량만 보기' 필터** | `2d42b5b` |
| **CO-40-06** | **안 돈 측정이 OK/NG 로 표시(stale)** | **실행 shot 만 초기화 + FAIConfig.ClearResult 가 Measurement.LastHasResult 미초기화** | **런 시작(OnStart) 시 이 시퀀스 shot 의 모든 Measurement.ClearResult** | `7ea7f3b` |
| **CO-40-07** | **검사 안 한 다른 시퀀스(TOP/SIDE) shot 이 cycle 에 표시** | **BuildDto 가 레시피 전체 shot 덤프** | **BuildDto(ownerSequenceName) — 실행 시퀀스 소유 shot 만 직렬화** | `7ea7f3b` |

## Decisions Made

1. **airspace 회피 (CO-40-04)**: `HalconViewerControl` 은 `HWindowControlWPF`(네이티브 HWND HwndHost) 호스팅 → 그 위 WPF 요소는 항상 가려짐. 토글을 Halcon 창 셀(Row1) 밖 헤더(Row0)로 이동. 증상 일치(이미지는 바뀌나 버튼만 안 보임)로 확정.

2. **자동 포커스 타이밍 (CO-40-05)**: ItemsSource 직후 동기 SelectedItem 지정은 DataGrid 행 컨테이너 미생성으로 무시됨 → `Dispatcher.BeginInvoke(Background)` 지연. 단, 데이터 특성상 첫 불량이 0번 행이면 시각 변화가 미미 → 선택 행 강조(굵게+파란 테두리) 추가 + '불량만 보기' 필터로 보완 (사용자 결정).

3. **결과 lifecycle (CO-40-06)**: 측정 런타임 결과(LastHasResult/Value/Judgement)는 INI 비영속(휘발성). 단일 shot 실행 시 나머지 shot 이 이전 런 잔여값을 유지하는 문제는 "검사 시작 시 일괄 초기화 부재" + "FAIConfig.ClearResult 가 하위 Measurement 미초기화" 복합. OnStart(Start/StartAll 공통)에 이 시퀀스 shot 의 Measurement 를 직접 ClearResult.

4. **시퀀스별 cycle (CO-40-07)**: 사용자 요구 "Top 검사면 Top, Bottom 검사면 Bottom만". BuildDto 가 실행 시퀀스(Name=TOP/SIDE/BOTTOM) 소유 shot 만 직렬화. shot.OwnerSequenceName 매칭(빈값 → "TOP" 폴백, SequenceHandler/ShotConfig 정책 일치).

5. **종합판정/TCP scoping 이연 (사용자 결정 B)**: AddResponse 집계는 여전히 recipeManager.Shots 전체 순회 → 오토 모드에서 다른 시퀀스 stale 이 TCP 응답/종합판정에 섞일 수 있음. 리뷰어 측정표는 시퀀스별 한정 완료. 종합판정/TCP 정합성은 CO-40-08 carry-over.

## Auto Mode 동작 확인 (사용자 Q)

- 실행 범위는 SIMUL/AUTO 모드가 아니라 **트리거 경로**가 결정:
  - 오토(호스트 TCP 테스트) → `ProcessTest` → `seq.StartAll` → 그 시퀀스 **전 shot 실행** (모두 값 표시)
  - 수동(UI Start 버튼) → `Start(actID)` → **단일 shot** (CO-40-06 으로 나머지는 '—')
- `#if SIMUL_MODE` 는 이미지 취득(파일 로드 vs 카메라 grab)만 분기, 실행 shot 범위와 무관.

## Carry-over

- **CO-40-08** (open → 별도 phase/quick): 오토 모드 종합판정/TCP 응답을 실행 시퀀스로 한정. 현재 `InspectionSequence.AddResponse` / `ComputeOverallResult` 가 recipeManager.Shots 전체를 순회 → 다른 시퀀스 stale 이 host 응답·cycle 종합판정에 포함될 수 있음. 사용자 결정 B 로 이연.

## Next Phase Readiness

- **Plan 04 (ExcelExportService, OUT-02)** 실행 가능 — 리뷰어가 export 버튼 자리(우측 패널) 제공, cycle.json(시퀀스별) 데이터 소스 확정.

## Self-Check: PASSED

- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)` FOUND
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` FOUND
- 커밋 `7b71d55`, `952c529`, `2d42b5b`, `7ea7f3b` FOUND
- msbuild Debug/x64 exit 0 (신규 error 0)
- 사용자 "리뷰어 정상 작동" 승인 (2026-06-01)

---
*Phase: 40-export-i-1-2026-06-01*
*Completed: 2026-06-01*
