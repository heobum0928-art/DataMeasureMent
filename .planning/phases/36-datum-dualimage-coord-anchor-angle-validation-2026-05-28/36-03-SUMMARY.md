---
phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28
plan: 03
subsystem: ui
tags: [phase36, datum, dualimage, visualization, halcon-display, propertygrid, badge, xaml]

requires:
  - phase: 36-02
    provides: ExpectedAngleDeg/AngleTolerance 필드 + AngleValidationStatus transient + wrap-around validation gate
provides:
  - HalconDisplayService.DrawOriginFallback (OFF-SCREEN 캔버스 중앙 fallback 십자 + 좌표 + 라벨)
  - HalconDisplayService.DrawExpectedAngleArrow (Expected angle 점선 화살표, PASS=green / FAIL=red)
  - RenderDatumFindResult 의 OFF-SCREEN 가드 + AngleTolerance > 0 게이트
  - MainView swap 핸들러의 SetDatumOverlay 재호출 chain (양쪽 캔버스 시각화 일관)
  - MainView 캔버스 우상단 외부 Color Badge (옵션 C, PropertyGrid 변경 0)
affects: [36-04]

tech-stack:
  added: []
  patterns:
    - "HOperatorSet.GetWindowExtents 기반 OFF-SCREEN 가드 (HWindowControlWPF airspace 컨텍스트)"
    - "Halcon SetLineStyle (10, 5) 점선 활성 → 빈 HTuple 즉시 해제 (다른 렌더 영향 0)"
    - "XAML 외부 Border + DataTrigger + x:Static enum (xmlns:seq) — PropertyGrid 셀 override 회피 (가드 4파일 변경 0)"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml

key-decisions:
  - "옵션 C 채택 (D-36-06 갱신): PropertyGrid 셀 Style override 대신 캔버스 우상단 외부 Border 라벨. PropertyTools 3.1.0 셀 API 제약 회피 + 가드 4파일 변경 0 유지."
  - "OFF-SCREEN 가드는 RenderDatumFindResult 진입 직후 GetWindowExtents 로 즉시 판정 → 화면 밖이면 DrawOriginFallback 위임 후 return (purple 십자/화살표 미렌더)."
  - "Expected 화살표는 AngleTolerance > 0.0 시에만 렌더 (D-36-11/13). 1-image algorithm (INI 에 키 없음) 회귀 0 — ParamBase fallback 0.0."
  - "점선 SetLineStyle 활성 후 빈 HTuple 로 즉시 해제하여 후속 RenderXXX 헬퍼에 영향 0 (D-36-11)."

patterns-established:
  - "OFF-SCREEN fallback: 캔버스 중앙 red 십자 + 좌표 텍스트 + 'OFF-SCREEN' 라벨 (purple 정상 검출 십자와 시각 구분)"
  - "Expected angle 점선 화살표 (length=30, head=5): 검출 실선 화살표 (length=20) 보다 약간 길게 — 시각 구분"
  - "XAML 캔버스 우상단 배지 패턴: HorizontalAlignment=Right / VerticalAlignment=Top / Margin 으로 기존 배지와 stacked vertical 간격 (40px)"

requirements-completed: [SC-36-1, SC-36-3, D-36-06, D-36-09, D-36-10, D-36-11]

duration: 25min
completed: 2026-05-28
---

# Phase 36 Plan 03: Visualization + Color Badge Summary

**RenderDatumFindResult 의 OFF-SCREEN fallback + Expected 점선 화살표 헬퍼 신설 + MainView swap chain 재트리거 + 캔버스 우상단 외부 색상 배지 (옵션 C) — CO-34.1-09 "정확히 되었는지 안되었는지 판단 불가" 의 시각화 단 종결**

## Performance

- **Duration:** 25 min
- **Tasks:** 3
- **Files modified:** 3
- **Commits:** 4 (3 task + 1 summary metadata)

## Accomplishments
- HalconDisplayService 에 `DrawOriginFallback` + `DrawExpectedAngleArrow` 헬퍼 2개 신설 (Allman brace, 모든 라인 `//260528 hbk Phase 36 D-36-10/11` 주석)
- `RenderDatumFindResult` 본문 보강: OFF-SCREEN 가드 (GetWindowExtents 기반) + AngleTolerance > 0 게이트
- `BtnSwapHorizontal_Click` / `BtnSwapVertical_Click` 핸들러 각각에 `halconViewer.SetDatumOverlay(_selectedDatumForSwap, true)` 1줄씩 추가 → swap chain 재트리거
- MainView.xaml UserControl 루트에 `xmlns:seq="clr-namespace:ReringProject.Sequence"` 추가 + 캔버스 Grid Row 1 우상단에 `border_angleValidationBadge` 외부 Border (DataTrigger + x:Static enum 명시) 삽입
- 가드 4파일 변경 0 (ParamBase.cs / InspectionListView.xaml.cs / InspectionSequence.cs / VisionResponsePacket.cs)
- msbuild Debug/x64 PASS (exit 0), warnings = 14 (Phase 36 baseline 유지), errors = 0

## Task Commits

1. **Task 1: HalconDisplayService 헬퍼 + RenderDatumFindResult 보강** — `e76f985`
2. **Task 2: MainView swap chain 재트리거 (Horizontal + Vertical)** — `78e6b25`
3. **Task 3: MainView.xaml 색상 배지 (옵션 C)** — `6306967`

## Files Created/Modified
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — 헬퍼 2개 (~50 라인) + RenderDatumFindResult OFF-SCREEN/Expected 가드 (~15 라인)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — swap 핸들러 2개 각각 1줄 + 주석 (총 4 라인)
- `WPF_Example/UI/ContentItem/MainView.xaml` — xmlns:seq 1줄 + Color Badge Border 블록 (~45 라인)

## Decisions Made
- **옵션 C 채택 (D-36-06)**: PropertyGrid 셀 Style override 대신 캔버스 우상단 외부 Border 라벨. PropertyTools 3.1.0 셀 API 제약 회피 + 가드 4파일 변경 0 유지 우선.
- **OFF-SCREEN 가드 위치**: `try` 블록 첫 라인 (LastFindSucceeded gate 직후). GetWindowExtents 조회 O(1) → 매 Render 호출 1회 추가 비용 무시 가능.
- **Expected 화살표 length=30**: 검출 실선 화살표 (length=20) 보다 약간 길게 — 시각적 분리 강화.
- **점선 즉시 해제 (SetLineStyle 빈 HTuple)**: 다른 RenderXXX 헬퍼에 영향 0 — 헬퍼 본문 마지막 라인에서 release.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] worktree bin DLL 부재 (외부 reference)**
- **Found during:** Task 3 msbuild 직후
- **Issue:** Worktree 의 `WPF_Example/bin/x64/Debug/` 가 비어있어 PropertyTools, WPF.MDI, Basler.Pylon, MvCamCtrl.Net 등 외부 reference DLL 이 누락 → XAML 컴파일 시 MC3074 (네임스페이스 태그 없음) 오류 다발.
- **Fix:** main repo (`C:/Info/Project/DataMeasurement/WPF_Example/bin/x64/Debug/.`) 의 bin 내용을 worktree bin 으로 복사. NuGet packages.config classic-style 이라 Restore 만으로는 외부 DLL 복원 안 됨 (사전 빌드된 main bin 의존).
- **Files modified:** 없음 (bin 디렉토리 환경 복구만, 소스 변경 0)
- **Verification:** Rebuild 후 exit 0, errors=0, warnings=14 baseline 일치.
- **Committed in:** N/A (환경 복구, 코드 변경 0)

**2. [Rule 1 - Hygiene] XAML 코멘트 내 색상 코드 중복 grep 충돌**
- **Found during:** Task 3 acceptance criteria 검증
- **Issue:** 플랜 verify 가 `#43A047`/`#E53935` 각각 `-eq 1` 요구하나, XAML 코멘트에 색상 코드를 명시하여 grep count = 2 발생.
- **Fix:** XAML 코멘트의 `#43A047`/`#E53935` 색상 코드를 `green`/`red` 영문 단어로 치환. Setter 의 Background Value 만 hex 유지.
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml (코멘트 한 줄)
- **Verification:** 재검증 결과 pass=1 fail=1 (acceptance 충족).
- **Committed in:** 6306967 (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (1 missing critical 환경, 1 hygiene grep 충돌).
**Impact on plan:** 둘 다 환경/검증 충돌만 해소 — 플랜 코드 의도 그대로 실행됨. 스코프 변경 0.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 04 (Wave 4, UAT) 준비 완료. 시각 검증 + 실측 페어 (Cal_Image/DualImageTest/SIDE1_3-1_*) 로 OFF-SCREEN fallback / Expected 점선 화살표 / Color Badge 동작 확인 필요.
- BindingFailure 등 런타임 평가는 빌드 PASS 만으로 보장 안 됨 — Plan 04 UAT 에서 SIMUL 시각 확인 필수 (DataContext chain: halconViewer / InspectionListView SelectedItem).

---
*Phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28*
*Completed: 2026-05-28*
