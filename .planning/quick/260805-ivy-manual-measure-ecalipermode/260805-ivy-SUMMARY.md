---
phase: quick-260805-ivy
plan: 01
subsystem: ui
tags: [wpf, halcon-viewer, manual-measure, context-menu]

# Dependency graph
requires: []
provides:
  - "ECaliperMode enum (Free/Horizontal/Vertical) — namespace ReringProject.UI"
  - "MainResultViewerControl 축 고정 Manual Measure (수평/수직/자유) 기능"
  - "우클릭 컨텍스트 메뉴 3-way 라디오 서브메뉴 '측정 축 고정'"
affects: [align-viewer, main-viewer]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "MenuItem 다중 체크 상태는 코드비하인드 UpdateContextMenuState() 한 곳에서만 IsChecked 를 세팅 (XAML 에는 IsCheckable/IsChecked 미기재) — 단일 소스 유지"

key-files:
  created:
    - WPF_Example/UI/ContentItem/ECaliperMode.cs
  modified:
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml
    - WPF_Example/DatumMeasurement.csproj

key-decisions:
  - "축 스냅은 ApplyManualMeasurePoint 의 else 분기(끝점 대입 직전)에서만 적용 — GetDistance 는 원문 그대로 두어 delta 0 자동 효과로 순수 수평/수직 거리를 얻음"
  - "축 모드는 ResetManualToolState 대상에서 제외 — 이미지 재로드와 무관한 사용자 선호값으로 유지"
  - "축 모드 전환 시 시작점만 찍힌 진행 중 측정만 폐기, _manualMeasureMode(측정 모드 자체)는 유지"

patterns-established:
  - "체크 가능 서브메뉴 3-way 라디오는 WPF 의 자동 IsChecked 토글을 신뢰하지 않고, 클릭 핸들러가 전용 세터를 호출 → 세터가 상태 확정 후 UpdateContextMenuState() 로 강제 재동기화"

requirements-completed: [IVY-01, IVY-02, IVY-03, IVY-04]

# Metrics
duration: ~15min
completed: 2026-08-05
---

# Quick 260805-ivy: Manual Measure 축 고정(ECaliperMode) Summary

**MainResultViewerControl의 Manual Measure(두 점 클릭 거리 측정)에 수평/수직 축 고정 모드를 추가한 ECaliperMode enum 기반 확장 — 메인/Align 뷰 공용 클래스라 양쪽 화면에 동시 적용**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-08-05T05:21:49Z
- **Tasks:** 3 / 3
- **Files modified:** 4 (신규 1 + 수정 3)

## Accomplishments
- `ECaliperMode` enum(Free/Horizontal/Vertical) 신규 생성 및 classic csproj 등록
- `ApplyManualMeasurePoint` 의 끝점 대입 직전 축 스냅 로직 추가 — Horizontal 은 Y(row), Vertical 은 X(column)를 시작점 값으로 강제
- 우클릭 컨텍스트 메뉴에 "측정 축 고정" 서브메뉴(자유/수평 고정/수직 고정) 추가, `UpdateContextMenuState()` 에서 3-way 라디오 체크 상태 자동 동기화
- 축 모드 전환 시 진행 중(시작점만 있는) 측정만 폐기하고 측정 모드 자체는 유지
- `GetDistance` 원문 100% 보존 — 축 정렬된 좌표는 한쪽 delta 가 0이 되어 자동으로 순수 축 거리 산출

## Task Commits

Each task was committed atomically:

1. **Task 1: ECaliperMode enum 신규 + csproj 등록 + 축 모드 필드 추가** - `d5bc6e6` (feat)
2. **Task 2: 축 스냅 로직 + SetManualMeasureAxisMode + 3개 클릭 핸들러** - `9874d02` (feat)
3. **Task 3: 컨텍스트 메뉴 서브메뉴 XAML + 체크 상태 동기화** - `41f01cc` (feat)

_Plan metadata commit (SUMMARY.md/STATE.md) — handled by orchestrator, not this executor._

## Files Created/Modified
- `WPF_Example/UI/ContentItem/ECaliperMode.cs` - 신규. Free/Horizontal/Vertical 3-멤버 enum, namespace ReringProject.UI
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` - `_manualMeasureAxisMode` 필드, `ApplyManualMeasurePoint` 축 스냅, `SetManualMeasureAxisMode` + 3개 클릭 핸들러, `UpdateContextMenuState` 체크 동기화 블록 추가
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml` - `ManualMeasureMenuItem` 과 `ClearMeasureMenuItem` 사이에 "측정 축 고정" 서브메뉴(3항목) 삽입
- `WPF_Example/DatumMeasurement.csproj` - `<Compile Include="UI\ContentItem\ECaliperMode.cs" />` 1줄 추가 (classic csproj, 자동 포함 없음)

## Decisions Made
- 축 스냅 위치: `ApplyManualMeasurePoint` else 분기 내 `imagePoint` 대입 직전 (CONTEXT LOCKED 대로) — `Point` 가 값 타입이라 로컬 수정이 호출자에 영향 없음
- `GetDistance` 는 수정 금지 계약대로 완전 보존
- 축 모드는 `ResetManualToolState()` 대상에서 제외 (이미지 교체와 무관한 사용자 선호값)
- 같은 축 모드를 다시 클릭해도 WPF 의 자동 IsChecked 토글을 무시하고 `UpdateContextMenuState()` 로 되돌림

## Deviations from Plan

**1. [Rule 3 - Blocking] 빌드 검증 시 실행 중이던 DatumMeasurement.exe 프로세스 종료**
- **Found during:** Task 1 최초 msbuild 검증
- **Issue:** Visual Studio Insiders 디버그 세션에서 실행 중이던 `DatumMeasurement.exe`(PID 26472)가 출력 exe 파일을 잠가 MSB3027/MSB3021 복사 오류 발생 (실제 C# 컴파일 오류는 0건, `obj`→`bin` 복사 단계에서만 실패)
- **Fix:** `taskkill /PID 26472 /F` 로 잠금 프로세스 종료 후 재빌드 → 3개 태스크 전부 error 0건으로 통과
- **Files modified:** 없음 (환경 조치만)
- **Verification:** 재빌드 로그에 `error` 문자열 0건, `DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe` 출력 확인
- **Committed in:** 해당 없음 (코드 변경 아님)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** 코드 변경 없는 환경 조치. 계획 범위·산출물에 영향 없음.

## Issues Encountered
None (위 배포/환경 이슈 외 문제 없음)

## User Setup Required
None - 외부 서비스 설정 불필요. 재빌드/재기동 후 아래 사용자 실기 확인만 필요(계획 `<verification>` 사용자 실기 확인 섹션 참조):
1. 우클릭 → 측정 축 고정 서브메뉴에 `자유`만 체크 확인
2. `Manual Measure` → 두 점 클릭 → 기존과 동일한 대각선 거리 (회귀 0)
3. `수평 고정` → 완전한 수평선 + 거리 = 가로 차이
4. `수직 고정` → 완전한 수직선 + 거리 = 세로 차이
5. 시작점만 찍은 상태에서 축 모드 전환 → 시작점 소실, "Select first point" 안내 복귀
6. Align 탭 뷰어에서도 1~4 동일 동작 (같은 컨트롤 공유)

## Next Phase Readiness
- 코드 변경 완료, Debug|x64 빌드 error 0 확인됨. 사용자 실기 확인(6단계) 대기 상태로 종료.
- 레시피·측정 파이프라인·Export 경로 어디에도 새 입력 경로 없음 (threat_model T-ivy-01 accept 확인) — 순수 UI 유틸리티 변경.

---
*Quick task: 260805-ivy-manual-measure-ecalipermode*
*Completed: 2026-08-05*

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ContentItem/ECaliperMode.cs
- FOUND: WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
- FOUND: WPF_Example/UI/ContentItem/MainResultViewerControl.xaml
- FOUND: WPF_Example/DatumMeasurement.csproj
- FOUND: commit d5bc6e6 (Task 1)
- FOUND: commit 9874d02 (Task 2)
- FOUND: commit 41f01cc (Task 3)
