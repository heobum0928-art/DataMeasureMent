---
phase: quick-260911-fia
plan: 01
slug: saved-cycle-rerun
status: code-complete-uat-pending
date: 2026-09-11
subsystem: inspection-sequence
tags: [halcon, cycle-json, repeat-run, statistics-window, cpk]
requirements:
  - FIA-A-datum-photo-per-cycle
  - FIA-B-rerun-plan-from-saved-cycles
  - FIA-C-rerun-execution-with-restore
  - FIA-D-statistics-window-rerun-ui
  - FIA-E-no-change-guard
requirements-completed:
  - FIA-A-datum-photo-per-cycle
  - FIA-B-rerun-plan-from-saved-cycles
  - FIA-C-rerun-execution-with-restore
  - FIA-D-statistics-window-rerun-ui
  - FIA-E-no-change-guard
key-files:
  modified:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/Utility/CaptureImageSaveService.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml
key-decisions:
  - "저장 사진 재검사에 '현재 기준점 사진으로 대체(참고용)' 옵션을 넣지 않았다 — 기준점은 부품마다 좌표 원점을 다시 잡는 입력이라 다른 부품 사진과 섞으면 비교 불가한 값이 된다"
  - "재검사 결과 표 '변화' 칸 정렬 키는 변화의 절댓값(가장 많이 변한 항목을 위/아래 어느 쪽 정렬로도 바로 찾을 수 있게)"
  - "원래 평균은 재검사 시작 시점에 같은 기간·현재 레시피로 CSV 조회한 값(레시피 '전체' 선택 시에도 다른 레시피 값이 섞이지 않도록 현재 레시피로 명시 조회)"
  - "이미지 폴더 반복검사(StartFromImages)와 저장 사진 재검사(StartFromSavedCycles)는 시퀀스 이름 키의 정적 점유 표시로 상호 배제한다"
duration: ~2h
completed: 2026-09-11
---

# Quick 260911-fia: 저장 사진으로 재검사(CPK 비교) Summary

**PLC 자동 사이클마다 기준점 사진을 저장(cycle.json DatumImages)하고, 저장된 사이클을 부품 단위로 묶어 통계 창에서 "저장 사진으로 재검사" 한 번으로 파라미터 변경 전/후 CPK를 비교하는 기능.**

## Performance

- **Started:** 2026-09-11T02:2x (세션 시작, PLAN.md 읽기 직후)
- **Completed:** 2026-09-11T03:11:53Z
- **Tasks:** 4 (Task 1~4, Task 5는 비차단 체크포인트로 아래 별도 기록)
- **Files modified:** 8 (files_modified 프론트매터와 정확히 일치, `WPF_Example/DatumMeasurement.csproj` 미포함)

## Accomplishments

- **A**: PLC 자동(프로토콜) 사이클의 기준점(Datum) 촬영마다 `datum_<시퀀스>_<이름>[_H|_V]_<시각>.<jpg|bmp>` 사진을 `original` 폴더에 비동기 저장하고, 그 tick의 `cycle.json`에 `DatumImages`로 기록한다. 검출 성공/실패 무관, 수동 RUN/SIMUL/오프라인/캐시 재사용 tick은 저장하지 않는다.
- **B**: `SavedCycleRerunPlanner.BuildPlan`이 기간·레시피·시퀀스로 저장된 자동 cycle.json을 모아 기준점 tick 기준 부품 단위로 묶고, 부품마다 기준점(역할별)/Shot/두 장짜리(가로=ZIndexA tick, 세로=ZIndexB tick) 원본 사진 경로를 채우고, 사유별(기준점 없음/일부 없음/Shot 없음/Shot 누락/두 장짜리 없음/파일 없음)로 제외한다.
- **C**: `RepeatRunService.StartFromSavedCycles`가 경로를 스냅샷 → 부품마다 스냅샷 복원 후 해당 부품 값으로 덮어쓰기 → `ClearDatumTransforms()` → `StartAll(null)`로 실행하고, 완료/사용자 중단/시퀀스 OnStop·OnError/UI 스레드 예외/PLC 자동 검사 개입/통계 창 닫힘/프로그램 종료 **모든 경로**에서 단일 지점 `EndSavedCycleRerun`(멱등)을 거쳐 경로·`OfflineInspectMode`를 원복한다. 레시피 저장은 재검사 중(부품 사이 Idle 포함) 차단된다.
- **D**: 통계 창에 재검사 시퀀스 콤보/시작/중단/원래 통계로 버튼과 진행 상태, 결과 표(기존 색·나쁜 순 정렬·벗어난 양·요약 + 신규 "원래 평균"·"변화" 칸), CPK export가 지금 보고 있는 쪽(재검사면 재검사 사이클) 기준으로 동작하도록 추가.

## Task Commits

1. **Task 1: [A] 기준점 사진 사이클마다 저장 + DatumImages 기록** - `4ec955de` (feat)
2. **Task 2: [B] SavedCycleRerunPlanner (부품 단위 재검사 계획)** - `20369a51` (feat)
3. **Task 3a: [C] 재검사 실행 — 스냅샷/적용/트리거/종료 복원(RepeatRunService)** - `0f82e3b9` (feat)
4. **Task 3b: [C] 저장 차단 가드 + 종료 시 복원 훅(MainWindow)** - `b1308e40` (feat)
5. **Task 4: [D] 통계 창 재검사 UI** - `e07a398e` (feat)

빌드: 각 커밋 직전 `MSBuild ... -p:Configuration=Release -p:Platform=x64` 실행, 5회 모두 `error CS`/`error MC` **0건**.

## Files Created/Modified

- `WPF_Example/UI/ViewModel/CycleResultDto.cs` - `DatumImageRecordDto`(ROLE_SINGLE/H/V) + `CycleResultDto.DatumImages`
- `WPF_Example/Utility/CaptureImageSaveService.cs` - `BuildDatumFileName`(datum_ 파일명, 원본 포맷 확장자)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - tick 단위 기준점 사진 기록 수집/초기화, 두 BuildDto 지점에 DatumImages 첨부
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `ArchiveDatumImageForCycle`(자동 tick 기준점 사진 비동기 저장)
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` - `SavedCycleRerunPlanner`/`Plan`/`Part` + `StartFromSavedCycles` 실행/복원 전체
- `WPF_Example/MainWindow.xaml.cs` - 저장 차단 가드 + 종료 시 복원 훅
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` - `StatisticsRerunViewModel` + `StatRowPresenter.FillRerunComparison` + 배선
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml` - 재검사 패널 + 원래 평균/변화 칸

## Decisions Made

- **"현재 기준점 사진으로 대체(참고용)" 옵션 미도입**: 기준점은 부품마다 좌표 원점을 다시 잡는 입력이다. 다른 부품의 기준점 사진으로 대체하면 그 부품의 측정값 자체가 비교 불가능한 숫자가 되고, 옵션 분기가 늘어나는 비용에 비해 얻는 게 없다고 판단했다(Task 2 코드 주석에도 기록).
- **"변화" 칸 정렬 키 = 변화의 절댓값**: 방향(+/-) 무관하게 "가장 많이 변한 항목"을 헤더 클릭 한 번으로 찾을 수 있도록 했다.
- **원래 평균 = 재검사 시작 시점에 같은 기간·현재 레시피로 별도 CSV 조회**: 레시피 필터가 "전체"여도 재검사 자체는 항상 "현재 불러온 레시피"로만 도므로, 원래 통계도 그 레시피로 명시 조회해 다른 레시피 값이 비교 기준에 섞이지 않게 했다.
- **이미지 폴더 반복검사와 저장 사진 재검사 상호 배제**: `RepeatRunService`에 시퀀스 이름을 키로 하는 정적 점유 집합(`s_occupiedSequenceNames`)을 두고, 인스턴스별 `_bHoldsOccupancy` 플래그로 "점유하지 않은 인스턴스의 Stop()이 남의 점유를 실수로 해제"하는 것을 막았다.

## Deviations from Plan

### Auto-fixed / 판단 필요 항목

**1. [Rule 3 - Blocking] Task 3 커밋 분할 경계를 계획의 괄호 표기와 다르게 잡음**
- **Found during:** Task 3 착수 시
- **Issue:** 필수 수정 사항 #3은 "3a(스냅샷·Start·Apply·Trigger) / 3b(Finish·Stop·End·MainWindow 종료 훅)"로 나누라고 했지만, `TriggerNextSavedCyclePart`의 예외/실패 경로(필수 수정 #1)가 `EndSavedCycleRerun`을 직접 호출하고 `StartFromSavedCycles`의 `OnFinish` 구독이 `HandleSavedCycleFinish`를 참조하므로, 이 두 메서드를 3a에서 제외하면 3a가 컴파일되지 않는다.
- **Fix:** 3a 커밋(`0f82e3b9`)에 `EndSavedCycleRerun`/`RestoreSavedCycleOverrides`/`HandleSavedCycleFinish`/`RequestStopSavedCycleRerun`/`WaitIdleThenEndSavedCycle`까지 전부 포함해 RepeatRunService 쪽을 완결되게 만들고, 3b 커밋(`b1308e40`)은 MainWindow 전용(저장 차단 가드 + 종료 복원 훅)으로 두어 "두 커밋, 각각 빌드 확인"이라는 필수 수정의 **목적**(원자적 단계별 검증)은 그대로 지켰다.
- **Files modified:** `RepeatRunService.cs`(3a), `MainWindow.xaml.cs`(3b)
- **Verification:** 두 커밋 모두 직전 Release|x64 빌드 error CS/MC 0 확인 후 커밋.
- **Committed in:** `0f82e3b9`, `b1308e40`

**2. [문서화 필요 - 하드룰 grep 오탐] `hbk` 매치 4건은 새 주석이 아니라 순수 이동**
- **Found during:** Task 3a 커밋 전 grep 게이트
- **Issue:** 필수 수정 #3은 `HandleFinish`의 종합판정 구간을 "로직·로그 문구 한 글자도 바꾸지 않고" `BuildRunCycleDto`로 추출하며 "기존 WR-01/WR-02 주석은 함께 이동"하라고 명시했다. 이 이동은 diff 상 삭제+재추가로 보여 line-based hbk grep이 4건(WR-02 1, N5C-04 2, WR-01 1)을 잡는다.
- **Fix:** 없음(의도된 동작) — grep 결과를 그대로 두고 이 SUMMARY와 커밋 메시지(`0f82e3b9`)에 사유를 명시했다. 새로 작성한 주석에는 날짜 표기(hbk)를 전혀 쓰지 않았다(전부 `quick-260911-fia` 형식).
- **Files modified:** `RepeatRunService.cs`
- **Committed in:** `0f82e3b9`

**3. [Rule 1 - 방어적 견고성] HandleSavedCycleFinish 전체를 try/catch로 감쌈**
- **Found during:** Task 3a 작성 중
- **Issue:** 계획 텍스트는 `TriggerNextSavedCyclePart`(UI 스레드)만 명시적으로 try/catch 요구했지만, `HandleSavedCycleFinish`는 시퀀스 스레드에서 `OnFinish` 이벤트로 호출되며 내부에서 `BuildRunCycleDto`/`RemoveUnexpectedShots`/직렬화를 수행한다. 여기서 예외가 나면 시퀀스 스레드의 이벤트 파이프라인에 예외가 전파되어 재검사가 원복 없이 멈출 위험이 있다.
- **Fix:** 전체를 try/catch로 감싸 예외 시 `szEarlyEndReason`을 세팅해 `EndSavedCycleRerun`으로 보내도록 했다(필수 수정 #1의 취지를 이 경로에도 동일 적용).
- **Files modified:** `RepeatRunService.cs`
- **Committed in:** `0f82e3b9`

---

**Total deviations:** 3 (1 blocking-분할 경계 재해석, 1 문서화-grep 오탐, 1 방어적 견고성 추가)
**Impact on plan:** 전부 계획의 명시적 목적(원자적 검증, 주석 보존, 모든 종료 경로에서의 복원)을 지키기 위한 조정이며 범위 확장 없음.

## 종료 경로별 복원 자체 점검 (코드 추적, 필수 수정 #3)

| 종료 경로 | 트리거 | 복원 여부 |
|---|---|---|
| 완료(TargetCount 도달) | `HandleSavedCycleFinish` → `bShouldComplete` | `EndSavedCycleRerun(null)` → `RestoreSavedCycleOverrides()` — **복원됨** |
| 사용자 중단 | `RequestStopSavedCycleRerun` | Idle이면 즉시, 아니면 `WaitIdleThenEndSavedCycle` 대기 후 `EndSavedCycleRerun` — **복원됨** |
| 시퀀스 OnStop/OnError | `_onSavedCycleStopHandler`/`_onSavedCycleErrorHandler` | `EndSavedCycleRerun("시퀀스가 중단/오류로 멈춤")` — **복원됨** |
| 통계 창 닫힘 | `StatisticsRerunViewModel.OnWindowClosing` | `RequestStopSavedCycleRerun("통계 창 닫힘")` → 위 사용자 중단 경로와 동일 — **복원됨** |
| 프로그램 종료 | `MainWindow.Window_Closing` | `RestoreActiveSavedCycleOverridesForShutdown()` → `EndSavedCycleRerun("프로그램 종료")`, `SystemHandler.Release()`(Setting.Save 포함) **이전에** 호출 — **복원됨(저장 전 원복)** |
| UI 스레드 예외 | `RunSavedCycleTriggerOnUiThread` try/catch | `EndSavedCycleRerun("경로 적용 중 오류: ...")` — **복원됨** |
| PLC 자동 검사 개입 | `FindSavedCycleAbortReason`(다음 부품 시작 전) 또는 `HandleSavedCycleFinish`의 `bAutoCycleIntruded` | `EndSavedCycleRerun(사유)` — **복원됨** |

`EndSavedCycleRerun`은 `_bSavedCycleEnded` 플래그(락 보호)로 멱등 — 두 경로가 겹쳐 호출돼도 두 번째는 즉시 return.

## Known Stubs

없음. 모든 데이터 경로(기준점 사진 저장·재검사 계획·경로 치환·통계 집계·UI 바인딩)가 실제 소스에 연결되어 있다.

## Threat Flags

없음. 이번 변경은 계획의 `<threat_model>`(T-FIA-01~09)이 이미 다룬 표면(레시피/Setting 파일 경계, PLC 개입, cycle.json 로컬 산출물, 기준점 이미지 수명) 안에서만 이루어졌고, 새 네트워크 엔드포인트·인증 경로·신뢰 경계 변경은 없다.

## Issues Encountered

없음 — 5회 빌드 모두 최초 시도에서 error CS/MC 0.

## Manual Verification Required (실기 UAT 대기)

Task 5(비차단 체크포인트)에 따라 아래 8개 항목은 사용자가 실제 장비에서 확인해야 한다. 코드/빌드 작업은 여기서 종료하며, 대기하지 않는다.

- [ ] 1. PLC 자동 1사이클(TOP: 기준점 z=1 + Shot 2~4) 후 `D:\Data\Result\Image\<yyMMdd>\<HHmm>\original`에 `datum_TOP_<기준점이름>_<시각>.jpg`(설정이 BMP면 `.bmp`)가 생기는지. 그 z=1 tick의 `D:\Data\Result\<yyyyMMdd>\<HHmmss>_cycle\cycle.json`에 `"DatumImages": [{ "DatumName", "Role": "", "Path" }]`가 있고, z=2~4 tick은 빈 목록인지. BOTTOM(기준점 z=11)도 동일. 자동 검사 tact가 체감상 그대로인지(로그 `[CaptureSave] datum_` 줄 확인).
- [ ] 2. 수동 RUN 한 번 → 새 `datum_` 파일이 생기지 않는지.
- [ ] 3. TOP C1_P1 파라미터 변경(레시피 저장은 하지 않아도 됨) → 통계 창 → 기간 오늘, 레시피 전체 또는 FAI_1, 재검사 시퀀스 TOP → "저장 사진으로 재검사". 진행 "재검사 중 k/N 부품", 끝나면 상단 "재검사 결과 — 부품 N개(제외 M개: 사유)". 오늘 이전 사이클(기준점 사진 없음)은 제외 사유로 잡히는지.
- [ ] 4. 결과 표: 색/나쁜 순 정렬/벗어난 양/요약이 기존과 같은 방식이고 "원래 평균"·"변화" 칸이 보이는지, C1_P1 행의 변화 값이 파라미터 변경 방향과 맞는지, 행 클릭 시 차트가 재검사 값으로 그려지는지.
- [ ] 5. 재검사 결과 보는 중 "CPK 리포트 export" → 파일명 `cpk_rerun_...`이고 재검사 부품 수만큼 들어가는지. "원래 통계로" → 원래 표로 돌아오고 새 칸이 숨는지, 이때 export는 `cpk_report_...`(원래 CSV 기준).
- [ ] 6. 원복: 재검사 후 TOP Shot/기준점 노드의 이미지 경로(SimulImagePath/TeachingImagePath)가 재검사 전과 같은지, 설정의 OfflineInspectMode가 재검사 전 값인지. 재검사 도중 "중단" → 현재 부품이 끝난 뒤 멈추고 같은 원복이 되는지. 재검사 도중 레시피 저장 → "저장 사진 재검사 중에는 저장할 수 없습니다" 안내가 뜨는지.
- [ ] 7. (가능하면) 재검사 도중 PLC $TEST가 오면 재검사가 "PLC 자동 검사 수신" 사유로 멈추고 자동 검사는 실물 촬영으로 정상 응답하는지.
- [ ] 8. BOTTOM 재검사: E5(두 장짜리)가 부품마다 z=14 사진=가로, z=15 사진=세로로 측정되어 값이 나오는지(재검사 결과 표 E5 행 N = 부품 수).

### 재량 결정 (Task 5 기록 요구)

- "현재 기준점 사진으로 대체(참고용)" 옵션 생략 근거는 위 "Decisions Made" 참고.
- 변화 칸 정렬 = 변화 값의 절댓값(방향 무관, 크기 순).
- 원래 평균 = 같은 기간·현재 레시피로 별도 CSV 조회(레시피 "전체" 선택 시에도 재검사 자체는 현재 레시피 고정이므로 비교 기준도 동일 레시피).

### 잔여 위험 (Task 5 기록 요구)

- 재검사 중 레시피 로드(`LoadRecipe`)나 설정창에서 직접 저장하면 `OfflineInspectMode=ON`이 파일에 남을 수 있다(T-FIA-03, accept). PLC `$TEST` 수신 시 `ForceOfflineInspectModeOffForAutoTest`가 자동 검사를 항상 실물 촬영으로 강제하므로 판정 오염은 없고, 레시피 변경은 다음 부품 시작 전 `FindSavedCycleAbortReason`이 감지해 재검사를 중단시킨다.
- 같은 초(HHmmss)에 두 시퀀스의 cycle.json 폴더가 겹치면(이론상 폴더명이 `HHmmss_cycle`로 시퀀스 구분이 없음) 한 tick이 덮여 그 부품이 제외될 수 있다(REASON_FILE_MISSING 등으로 자연 제외되며, 판정 오염이 아니라 재검사 대상에서 빠지는 안전한 방향의 실패).
- 기존 반복검사(`RepeatRunService.Start`, 고정 횟수 반복)는 이번 점유(occupancy) 체크에 포함하지 않았다(필수 수정 #2가 명시한 "기존 StartFromImages 와 새 StartFromSavedCycles"만 대상). `Start()`와 저장 사진 재검사가 같은 시퀀스에서 동시에 걸리는 것은 이번 범위 밖의 기존 제약(T-FIA-08과 동일 계열)으로 남는다.

## Self-Check: PASSED

- 8개 수정 파일 전부 `FOUND` (디스크 존재 확인)
- 5개 태스크 커밋 해시(`4ec955de`, `20369a51`, `0f82e3b9`, `b1308e40`, `e07a398e`) 전부 `FOUND` (`git log` 확인)
- `git diff --name-only 4ec955de^..e07a398e` = files_modified 8개와 정확히 일치, `DatumMeasurement.csproj` 미포함
- 하드룰 grep 6종(추가 줄 기준, 전체 범위): 삼항 0 / `??` 0 / `?.` 0 / switch식 0 / `hbk` 4(순수 이동, 위 Deviations #2 참고) / 중괄호 없는 단문 if·else 0
- Release|x64 빌드 5회 전부 `error CS`/`error MC` 0건

## Next Phase Readiness

- 재검사 실행/복원/통계 UI 코드는 완결되어 있으나, 실기(카메라·PLC) 환경에서의 최종 확인이 필요하다 — 위 8개 UAT 항목 통과 전에는 현장 배포하지 말 것.
- STATE.md의 quick 표에 `260911-fia` 행 추가는 오케스트레이터가 처리한다(본 실행자는 STATE.md 커밋 대상 아님).

---
*Task: quick-260911-fia*
*Completed: 2026-09-11*
