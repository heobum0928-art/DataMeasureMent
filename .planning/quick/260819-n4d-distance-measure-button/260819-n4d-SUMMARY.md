---
phase: quick-260819-n4d
plan: 01
status: complete
subsystem: ui
tags: [mainview, distance-measure, calibration, mm-conversion, read-only]

requires: []
provides:
  - "btn_measureDistance 툴바 버튼: 두 점 클릭으로 이미 설정된 PixelResolution 기반 실제 거리(mm)를 즉시 화면 표시 (read-only, 저장 없음)"
affects: [mainview-canvas-toolbar]

tech-stack:
  added: []
  patterns:
    - "ECanvasMode 상태머신 확장 패턴: 기존 Calibration 모드와 동일 구조(별도 enum 값 + 별도 포인트 리스트 필드)로 DistanceMeasure 모드 추가, ExitCanvasMode 에 cleanup 블록만 추가해 상태 격리"
    - "mm 환산은 shot.GetEffectivePixelResolution()(PixelResolution × CorrectionFactor) 단일소스 재사용 — 기존 측정 소비 경로(Action_FAIMeasurement, EdgePairDistanceMeasurement)와 동일 공식"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "Calibrate 와 거리측정을 하나로 합치지 않고 버튼 2개로 완전히 분리 (plan 의 AskUserQuestion 으로 이미 확정된 결정, 이번 실행에서 재논의 없이 그대로 따름)"
  - "새 XAML 주석에 원래 plan 원문(더블하이픈 '--') 그대로 쓰면 XML 주석 규칙 위반(MC3000 빌드 에러) — em dash(—)로 치환해 해결 (Rule 1 자동수정, 아래 Deviations 참조)"

requirements-completed: [QUICK-260819-N4D-01]

duration: 약 25분
completed: 2026-08-19
---

# Quick 260819-n4d: 거리측정 버튼 추가 (Calibrate 와 별도, read-only mm 표시) Summary

**메인 화면 툴바에 기존 Calibrate 버튼과 완전히 독립된 새 "거리측정" 버튼을 추가 — 두 점 클릭 시 이미 설정된 PixelResolution(보정계수 포함)으로 즉시 총/가로/세로 mm + 픽셀거리를 화면에 표시하는 read-only 기능**

## Performance

- **Duration:** 약 25분
- **Completed:** 2026-08-19
- **Tasks:** 2/3 자동 실행 완료(Task 1 구현, Task 2 검증+빌드), Task 3(checkpoint:human-verify)는 실행 앱을 띄워 직접 클릭해야 하는 실기 확인 — 이번 세션은 코드 실행 세션이라 스킵, 사람 확인 대기로 표시
- **Files modified:** 2

## Accomplishments

- `MainView.xaml` L351 부근에 `btn_measureDistance` 버튼 삽입 (`btn_calibrate`의 `</Button>` 직후, 체커보드 캘리브 버튼 앞) — `Click="MeasureDistanceButton_Click"`, 보라색(`#7C3AED`) 스타일, 기존 버튼과 동일 `ControlTemplate` 구조
- `MainView.xaml.cs`:
  - L46 `ECanvasMode` enum 에 `DistanceMeasure` 값 추가 (유일한 기존 줄 교체, 나머지 값은 그대로 보존)
  - L78 `_measurePoints` 필드 신규 삽입 (`_calibrationPoints` 바로 다음 줄)
  - L2626~2627 `ExitCanvasMode()` unsubscribe 블록에 `HalconViewer_MeasureMouseDown` 해제 삽입
  - L2651~2653 `ExitCanvasMode()` cleanup 블록에 거리측정 버튼 문구 리셋 + `_measurePoints.Clear()` 삽입 (오버레이는 기존 `ClearCalibrationOverlay()` 공용 호출이 이미 처리 — 재호출 없음)
  - L3205~3271 새 메서드 3개 삽입 (`ApplyCalibrationResult` 닫는 `}` 직후): `MeasureDistanceButton_Click` → `HalconViewer_MeasureMouseDown` → `FinishDistanceMeasure`
- `FinishDistanceMeasure`: 두 클릭 픽셀거리 계산 → `dataGrid_faiResults.SelectedItem` → `FindFAIByName` → `anchorFai.Owner as ShotConfig` 체인으로 shot 조회(전부 `ApplyCalibrationResult`와 동일한 null-safe if-else 패턴) → shot 을 못 구하면 픽셀거리만 `label_message` 에 표시(예외 없음) → shot 을 구하면 `shot.GetEffectivePixelResolution()`으로 총/가로/세로 mm 환산 후 표시, 3초 뒤 자동 사라짐(`DispatcherTimer`, 기존 `MessageDisplaySeconds` 상수 재사용)
- `PixelResolution`/`PixelResolutionX`/`PixelResolutionY` 에 대한 쓰기(write)는 코드 전체에서 0건 — read-only 확인(grep 으로 재검증: `FinishDistanceMeasure` 안에 `=` 대입 대상은 로컬 변수(`totalMm`/`dxMm`/`dyMm`)뿐)

## Task Commits

1. **Task 1+2 (구현 + 검증 + 빌드)** - `1088ad0` (feat) — Task 2는 코드 수정 없는 검증 전용이라 별도 커밋 없음(plan 명시대로 Task 1 커밋에 통합)

Task 3(실기 확인)은 사람 확인 대기 — 아래 "실기 확인 대기" 섹션 참조.

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainView.xaml` - 버튼 1개 순수 삽입. `git diff --numstat`: `19 insertions(+), 0 deletions(-)`
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - enum 값 1개(교체 1줄) + 필드 1개 + 메서드 3개 + `ExitCanvasMode` 확장 2곳. `git diff --numstat`: `95 insertions(+), 1 deletions(-)` (삭제 1줄 = enum 선언 교체 그 자체, 다른 삭제 없음)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - 빌드 버그] XAML 주석의 더블하이픈("--")이 XML 주석 규칙 위반**
- **Found during:** Task 2 S4 빌드 검증
- **Issue:** plan 이 지정한 XAML 주석 원문(`<!--...거리측정 -- 기존 Calibrate...-->`)을 그대로 삽입했더니 `MC3000: XML 주석 안에 '--' 기호를 사용할 수 없으며...` 빌드 에러 발생(XML 스펙상 주석 내 `--` 금지, plan 작성 시점에 놓친 부분)
- **Fix:** 주석 내 `--`(더블하이픈)을 이 파일의 기존 관례(예: L53 `... hotfix — 가로축...`)와 동일한 em dash(`—`)로 치환. 문구 의미·내용은 변경 없음
- **Files modified:** `WPF_Example/UI/ContentItem/MainView.xaml`
- **Commit:** `1088ad0` (같은 Task 1 커밋에 포함)

**2. [검증 스크립트 오류 — 코드 변경 아님] Task 1 grep 검증 `CS_ENUM` 항목 기대값 불일치**
- **Found during:** Task 1 정적 검증 실행
- **Issue:** plan 이 지정한 `grep -cF 'DistanceMeasure'` 명령의 기대값은 `5`였으나 실측 `90`. 원인은 파일에 이미 존재하던(이번 변경과 무관한) `ArcLineIntersectDistanceMeasurement`/`DualImageEdgeDistanceMeasurement`/`EdgeToLineDistanceMeasurement`/`ArcEdgeDistanceMeasurement`/`CompoundCenterCDistanceMeasurement`/`CompoundCenterBDistanceMeasurement`/`CompoundShortAxisDistanceMeasurement` 등 클래스명이 전부 `"DistanceMeasure"`를 부분 문자열로 포함하기 때문(`git show HEAD:...` 베이스라인 파일에서도 이미 85건 존재 확인). plan 의 절대값 기대치가 이 사전 존재 오탐을 계산에 넣지 못한 검증 스크립트 결함이지, 코드 결함이 아님
- **검증:** 편집 전 베이스라인 85건 → 편집 후 90건, 델타 정확히 +5(plan 의 근거 설명 5건: enum 선언/대입/가드/호출/선언 — 과 정확히 일치)
- **Fix:** 코드 수정 없음. 나머지 8/9 항목(`XAML_BTN=1 XAML_CLICK=1 CS_FIELD=9 CS_CLICKFN=1 CS_MOUSEDOWN=4 CS_FINISH=1 CS_GETEFF=3 CS_BTNCONTENT=3`)은 전부 plan 기대값과 정확히 일치 — 이 항목만 절대값 대신 델타로 재확인
- **Files modified:** 없음(검증 방법만 조정)
- **Commit:** 해당 없음(코드 변경 아님)

## Issues Encountered

없음 그 외. 빌드 산출물 잠김 없음(앱 미실행 상태, `D:\Data\DatumMeasurement.exe` 로 정상 출력) — 스크래치 OutDir 폴백 불필요.

## Verification Results

| # | 항목 | 결과 |
|---|---|---|
| 1 | Task 1 정적 검증 9종 | `XAML_BTN=1 XAML_CLICK=1 CS_ENUM=90(베이스라인 85 대비 델타 +5, plan 기대와 일치) CS_FIELD=9 CS_CLICKFN=1 CS_MOUSEDOWN=4 CS_FINISH=1 CS_GETEFF=3 CS_BTNCONTENT=3` — PASS (CS_ENUM 은 위 Deviations #2 참조, 나머지 8/9 plan 기대값과 정확히 일치) |
| 2 | S1 변경 범위 | `git status --porcelain -- WPF_Example` = 정확히 3줄(`DatumMeasurement.csproj`(사전 존재, 무관) + `MainView.xaml` + `MainView.xaml.cs`) | PASS |
| 3 | S2 순수 삽입 | `MainView.xaml.cs` 삭제 라인 정확히 1(enum 선언 교체 줄, plan 이 지정한 원문과 정확히 일치), `MainView.xaml` 삭제 라인 정확히 0 | PASS |
| 4 | S3 코딩 규칙 | 추가된 줄에서 삼항 연산자(`?`) 0건, 신규 `using` 0건 | PASS |
| 5 | S4 빌드 | `BUILD_RC=0 ERRORS=0 WARN_CS=12`(CS0618×10 + CS0162×2, baseline 정확 일치). 최초 빌드는 XAML 주석 `--` 문제로 `BUILD_RC=1 ERRORS=1`(MC3000) 실패 → Deviations #1 로 수정 후 재빌드 PASS. 스크래치 OutDir 폴백 미사용(잠김 없었음) | PASS |
| 6 | **기존 Calibrate 4개 메서드 byte-identical** | `CalibrateButton_Click`(11줄)/`HalconViewer_CalibrationMouseDown`(16줄)/`FinishCalibration`(48줄)/`ApplyCalibrationResult`(22줄) 전부 편집 전(`git show HEAD:...`) 대비 편집 후를 메서드 단위로 추출해 `diff` — **4개 전부 `IDENTICAL`**, 줄 수도 동일. `TextInputBoxWinidow` 다이얼로그 호출부 포함 무변경 | PASS |
| 7 | 커밋 위생 | `MainView.xaml`/`MainView.xaml.cs` 2개 파일만 `git add` 경로 명시 스테이징(`-A`/`-a` 미사용). 커밋 전/후 모두 `DatumMeasurement.csproj` unstaged(` M`) 유지 확인. 커밋 후 `git diff --diff-filter=D` 결과 빈 값(삭제 파일 없음) | PASS |
| 8 | Task 3(실기 확인, checkpoint:human-verify) | **PENDING** — 실행 중인 앱에서 직접 클릭해 확인해야 하는 단계라 이번 코드 실행 세션에서는 스킵. 사람 확인 대기(아래 섹션) | PENDING-HUMAN-VERIFICATION |

## 실기 확인 대기 (Task 3, checkpoint:human-verify — 미실행)

이 작업은 앱을 실제로 띄우고 캔버스에서 두 점을 클릭해봐야 확인 가능하며, 이번 세션(코드 실행 전담)에서는 실행하지 않았습니다. 실패로 간주하지 않고 **대기 상태**로 남깁니다.

**새 "거리측정" 버튼이 정확히 어떻게 동작하는지 (사용자 전달용):**

1. 툴바에서 기존 파란색 **Calibrate** 버튼 옆에 보라색 **거리측정** 버튼이 새로 생김
2. 클릭하면 "캔버스에서 첫 번째 점을 클릭하세요" 안내가 뜨고, 캔버스에서 임의의 두 점을 순서대로 클릭
3. 두 번째 점을 클릭하는 즉시(추가 입력 창 없이) 화면에 몇 초간 문구가 표시됨:
   - FAI 결과 그리드에서 항목을 선택해 놓은 상태라면: `거리: 12.345mm (가로 10.120mm, 세로 7.050mm)  |  픽셀거리: 145.3px` — 이미 설정되어 있는 픽셀분해능(보정계수 포함)으로 환산한 총 거리 + 가로 성분 + 세로 성분 + 원본 픽셀거리 4가지를 함께 보여줌
   - FAI 를 선택하지 않은 상태라면 mm 환산을 할 수 없으므로 조용히 폴백: `픽셀거리: 145.3px (FAI 미선택 -- mm 환산 불가)` — 에러나 빈 화면 없이 픽셀거리만 표시
4. 이 기능은 **아무 것도 저장하지 않음** — 순수 화면 표시(read-only). 기존 Calibrate 버튼(2점 클릭 후 실측 mm 값을 직접 입력해 보정값 자체를 재계산·저장하는 기능)은 이번 변경으로 전혀 건드리지 않았고, 코드 diff 로 4개 메서드가 byte-identical 함을 확인 완료(위 Verification 표 #6)

확인 방법(plan L523~531 그대로):
1. 앱을 다시 빌드/실행 → 메인 화면
2. FAI 결과 그리드에서 항목 하나 선택(분해능 설정된 FAI/Shot) → **거리측정** 클릭 → 두 점 클릭 → mm 문구 표시 확인, 값 상식성 확인(총거리 ≥ max(가로,세로))
3. FAI 선택 해제 상태로 다시 **거리측정** → 두 점 클릭 → 픽셀거리만 표시되는지(크래시 없음) 확인
4. 기존 **Calibrate** 버튼이 예전과 동일하게(2점 클릭 → mm 입력 다이얼로그 → "1 px = N.NNNN mm 적용됨" 토스트) 동작하는지 확인
5. 거리측정 1번째 점만 찍은 상태에서 ESC/다른 툴바 버튼 클릭 시 상태가 깔끔히 취소되고 Calibrate 상태와 섞이지 않는지 확인

## User Setup Required

None - 외부 서비스 설정 불필요. 다음 실행 세션에서 위 "실기 확인 대기" 5단계만 확인하면 됨.

## Next Phase Readiness

- 코드 변경/빌드 완료, 후속 코드 작업 없음
- Blockers 없음(실기 확인은 blocker 가 아니라 정상적인 대기 항목)

## Known Stubs

없음 - 표시 전용 버튼/메서드 추가이며 새 데이터 소스/바인딩 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. 로컬 UI 버튼/라벨 표시 로직만 추가(threat_model 의 T-n4d-01~04 모두 disposition 대로: mitigate 항목(T-n4d-01 크래시 가드, T-n4d-04 모드 격리)은 if-else null 가드 + 별도 enum/필드로 구현 완료, accept 항목(T-n4d-02/03)은 읽기 전용·로컬 표시라 해당 없음).

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/UI/ContentItem/MainView.xaml
FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
```

커밋 존재 확인:
```
FOUND: 1088ad0
```

---
*Phase: quick-260819-n4d*
*Completed: 2026-08-19*
