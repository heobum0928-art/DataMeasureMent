---
phase: quick-260812-fq4
plan: 01
subsystem: vision-overlay
tags: [halcon, overlay, capture-render, cosmetic]

# Dependency graph
requires: []
provides:
  - "OverlayCaptureRenderer.cs 두께 상수 3종(LineThicknessRadius/DistLineThicknessRadius/DatumRingThickness)이 비율 유지한 채 x1.5 상향된 값 — 캡쳐 PNG 번인 오버레이가 더 굵게 렌더링됨"
affects: [capture-overlay-visual-thickness]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "값-only 상수 변경은 사용처를 전혀 건드리지 않고 선언부 한 곳만 수정 — 상수 개수/시그니처/호출 경로 불변으로 diff를 로직 0건으로 증명 가능"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs

key-decisions:
  - "세 값 전부 정확히 x1.5 배율 적용(2.0→3.0, 3.1→4.65, 6.0→9.0) — 값마다 다른 배율을 쓰면 겹쳐 그려질 때 선 종류를 구분하는 기존 상대 두께 관계(거리선이 일반선보다 굵고, Datum 링이 가장 두꺼움)가 깨지므로 비율 고정"
  - "HalconDisplayService.cs(SetLineWidth 계열, 실시간 HWindow 뷰)와 MainResultViewerControl.xaml.cs는 완전히 별개의 독립 두께 체계로 판단해 손대지 않음 — 이번 요청은 '캡쳐 오버레이' 한정"
  - "MarkerHalfSize/DatumCircleCenterCrossHalf/DatumOriginCrossHalf는 '두께'가 아니라 마커/십자의 '크기(반길이)'이므로 이번 요청 범위 밖으로 판단해 무변경 유지"
  - "innerR clamp(if (innerR < 1.0) innerR = 1.0;) 등 기존 로직은 plan 지시대로 전혀 건드리지 않음 — DatumRingThickness=9.0에서 반지름 10px 이하 작은 Datum 원은 링이 아닌 원반으로 보이는 부작용을 기존 clamp가 그대로 흡수"
  - "작업 시작 전부터 미커밋 상태였던 PickerCenterCalibrationService.cs(사용자 별건 실험)는 읽지도 고치지도 커밋에도 포함하지 않고 baseline(numstat 6 2, diff sha 73a89c28...) 그대로 보존"

requirements-completed: [FQ4-01]

# Metrics
duration: ~8min
completed: 2026-08-12
---

# Quick Task 260812-fq4: 캡쳐 오버레이 두께 3종 1.5배 상향 Summary

**`OverlayCaptureRenderer.cs`의 캡쳐 번인 오버레이 두께 상수 3개(LineThicknessRadius/DistLineThicknessRadius/DatumRingThickness)를 상대 비율 유지한 채 일괄 x1.5 상향 — 사용처/로직/다른 파일 전부 무변경인 값-only 변경**

## Performance

- **Duration:** ~8 min
- **Completed:** 2026-08-12
- **Tasks:** 1 of 1
- **Files modified:** 1

## Accomplishments

- `OverlayCaptureRenderer.cs` L166~173 두께 상수 3개를 정확히 x1.5 배율로 상향:

  | 상수 | 전 | 후 | 배율 |
  |------|-----|-----|------|
  | `LineThicknessRadius` | 2.0 | **3.0** | ×1.5 |
  | `DistLineThicknessRadius` | 3.1 | **4.65** | ×1.5 (= 3.1 × 1.5) |
  | `DatumRingThickness` | 6.0 | **9.0** | ×1.5 |

- `MarkerHalfSize`(8.0), `DatumCircleCenterCrossHalf`(12.0), `DatumOriginCrossHalf`(20.0) 등 마커/십자 "크기" 상수 3개는 완전히 무변경으로 유지 — 이번 요청은 "두께"에 한정.
- 사용처 7곳(L225 FAI-DistLine 거리선, L229 기타선, L257 FAI-Edge 측정선, L292 X 마커 dilation, L344 Datum 축선, L353 링 innerR 계산, L378 십자 dilation)은 단 한 줄도 수정하지 않음 — 상수라서 값 변경만으로 자동 전파됨.
- 다른 파일을 건드리지 않은 근거: 저장소 전체에서 이 세 상수명은 `OverlayCaptureRenderer.cs`에서만 선언·사용된다(중복 정의 0건). `HalconDisplayService.cs`의 `SetLineWidth` 계열과 `MainResultViewerControl.xaml.cs`는 화면 실시간 HWindow 렌더링 경로로, 캡쳐 오버레이와 완전히 독립된 별개의 두께 체계이며 이번 "캡쳐 한정" 요청 범위 밖으로 판단해 열지도 않음. `StatisticsWindow.xaml.cs`의 `pl.StrokeThickness = 1.5`는 WPF 차트 선으로 완전히 무관.
- 작업 시작 전부터 미커밋 상태였던 `PickerCenterCalibrationService.cs`(사용자 별건 실험)는 읽지도 고치지도 커밋에도 포함하지 않았고, baseline(numstat `6 2`, diff sha `73a89c282724fedf25b7dcf8919b09251578d789`)이 작업 전후 완전히 동일함을 확인.

## Task Commits

Each task was committed atomically:

1. **Task 1: 캡쳐 오버레이 두께 상수 3종 1.5배 상향** - `a40f2d4` (feat)

**Plan metadata:** 본 SUMMARY.md 및 STATE.md/ROADMAP.md는 오케스트레이터가 별도 커밋 (실행자는 커밋하지 않음).

_Note: 이 quick task는 TDD 대상이 아님(상수 값 변경, 조건문 없음) — RED/GREEN 게이트 해당 없음._

## Files Created/Modified

- `WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs` - 두께 상수 3종 값 변경(2.0→3.0 / 3.1→4.65 / 6.0→9.0) + 신규 주석 2줄(`quick-260812-fq4:` 접두, 왜 4.65인지) + `DistLineThicknessRadius` 꼬리 주석 이력 갱신(`2.6→3.1` → `2.6→3.1→4.65`). 그 외 6~7행 사용처, `innerR` clamp, 길이 상수 3개 전부 무변경.

## Verification Results

자동 검증(plan `<automated>` 커맨드) 결과, 전 항목 plan 기대값과 정확히 일치:

- **[A]** 구값 잔존: `2.0`/`3.1`/`6.0` 각 0건 — PASS
- **[B]** 신값: `3.0`/`4.65`/`9.0` 각 1건 — PASS
- **[C]** 길이 상수 3개(`MarkerHalfSize=8.0`, `DatumCircleCenterCrossHalf=12.0`, `DatumOriginCrossHalf=20.0`) 각 1건 유지, `private const double` 총 6건(상수 추가/삭제 없음) — PASS
- **[D]** 로직 라인 게이트: diff 내 `DilationCircle|GenCircle|GenRegionLine|Difference|innerR|PaintRegionColor|DrawLineAsRegion|DrawPointsAsRegion|DrawCrossAsRegion|Dispose|foreach|if (` 매치 **0건** — 값만 바뀌었다는 기계적 증명
- **[E]** `git diff --numstat` = `5  3` (신규 주석 2줄 + 값 3줄 교체) — plan 기대와 정확히 일치
- **[F]** `HalconDisplayService.cs` / `MainResultViewerControl.xaml.cs` — `git status --porcelain` 및 `git diff --stat` 모두 완전 공백(무변경)
- **[G]** `PickerCenterCalibrationService.cs` — `git diff --numstat` = `6 2`, `git diff | git hash-object --stdin` = `73a89c282724fedf25b7dcf8919b09251578d789` — 작업 전후 baseline과 100% 동일 (커밋 전/후 모두 재확인)
- **[H]** 변경 파일 = `OverlayCaptureRenderer.cs`(이번 커밋) + `PickerCenterCalibrationService.cs`(작업 전부터 미커밋, 커밋 미포함) 2개뿐
- **[I]** Debug/x64 정식 경로 빌드(`MSBuild.exe DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`) — **성공(exit code 0)**. 이번 세션에서는 실행 중인 앱이 산출물을 잠그지 않아 정식 경로가 그대로 통과했고 `bin\x64\Debug\DatumMeasurement.exe`가 정상 갱신됨(타임스탬프 확인). `error CS` 0건. 기존에 있던 `warning CS0618`(TopSequence/BottomSequence/TopInspectionAction/BottomInspectionAction obsolete)와 `warning CS0162`(VirtualCamera.cs 도달불가 코드) 6건은 이번 변경과 무관한 사전 경고로, 변경 파일(`OverlayCaptureRenderer.cs`) 관련 신규 warning은 0건.
- `git diff --diff-filter=D --name-only HEAD~1 HEAD` — 의도치 않은 삭제 없음.

## Decisions Made

- Plan이 지정한 대로 4줄 블록을 통째로 찾아 교체 실행 — 별도 아키텍처 결정 없음.
- 빌드 산출물 잠김에 대비한 스크래치 OutDir 검증 절차가 plan에 명시돼 있었으나, 이번 세션에서는 앱이 실행 중이 아니었는지 정식 경로 빌드가 잠김 없이 그대로 성공(exit 0) — 스크래치 폴백 불필요.

## Deviations from Plan

None - plan 원문 그대로 실행. 빌드가 plan이 예견한 잠김 시나리오 없이 정식 경로로 바로 성공한 것은 이탈이 아니라 plan이 명시한 "먼저 정상 경로 시도" 절차가 정상 종료된 경우다.

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요.

## User UAT 안내

이번 작업의 완료 조건에는 포함되지 않지만, 사용자가 직접 확인할 사항:

1. 검사를 1회 실행해 캡쳐 PNG를 열고 육안 확인:
   - 측정선(녹/적)·거리선(cyan)·X 마커·Datum 축선/십자가 확실히 굵어졌는지
   - Datum 검출 원 링이 두꺼워졌는지, 작은 원이 원반처럼 보이지 않는지
   - 너무 굵어서 정작 봐야 할 에지가 가려지지는 않는지
2. 굵기가 과하거나 부족하면 배율만 재조정하면 됨 — 다음 후보값:
   - ×1.25: `2.5 / 3.875 / 7.5`
   - ×2.0: `4.0 / 6.2 / 12.0`

**알려진 부작용(의도된 동작):** `DatumRingThickness = 9.0`에서 반지름 ≤ 10px인 작은 Datum 검출 원은 링이 아닌 꽉 찬 원반으로 보임(기존 `innerR` clamp가 예외 없이 이미 처리 중). 실기에서 거슬리면 별건으로 처리 요망.

## Next Phase Readiness

- 정식 빌드 산출물(`bin\x64\Debug\DatumMeasurement.exe`)이 이번 세션에서 이미 갱신됐으므로 사용자가 바로 앱을 실행해 UAT 가능.
- 다른 렌더링 경로(실시간 뷰, 통계 차트)는 완전히 무변경 확인됐으므로 후속 작업에서 회귀 걱정 없이 진행 가능.

---
*Phase: quick-260812-fq4*
*Completed: 2026-08-12*

## Self-Check: PASSED

- FOUND: WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs
- FOUND commit: a40f2d4 (Task 1)
