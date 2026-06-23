---
phase: quick-260623-mao
plan: 01
subsystem: calibration-visualization
tags: [halcon, calibration, overlay, distortion-report, poc]
requires:
  - HalconViewerControl.SetInspectionOverlays (기존 오버레이 파이프라인)
  - EdgeInspectionOverlay / EdgeInspectionPoint 모델
  - HalconDisplayService.Render DispCross 배치-점 패턴 (FAI-EdgeRaw)
provides:
  - CalibrationResult 코너 좌표(CornerRows/Cols) + 중앙/외곽 평균(px) + X/Y 편차% 노출
  - HalconDisplayService Calib-Corners cyan DispCross 렌더 분기
  - CalibrationWindow 코너 cyan 마커 오버레이 + 보강 왜곡 리포트
affects:
  - 체커보드 캘리브레이션 검출 결과 시각화 (POC 시연)
tech-stack:
  added: []
  patterns:
    - FAI-EdgeRaw 배치-점 DispCross 미러 (Calib-Corners)
    - SetInspectionOverlays(null) 클리어 패턴 (로드/촬상/실패)
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
decisions:
  - "코너 마커 색상 = cyan (표준 색상명) — SetColor 예외 swallow 결함 방지 (메모리 사실)"
  - "오버레이는 기존 SetInspectionOverlays → HalconDisplayService.Render (HWindow 내부 DispCross) 재사용 — 새 드로잉 경로 발명 0 (airspace 방지)"
  - "ComputeAxisDeviationPct 헬퍼 — 종합 ComputeCenterOuterDeviationPct 와 동일 반경 게이트(innerR=diag*0.33, outerR=diag*0.66) + AccumulateRadialGroups 재사용 + 0% 가드"
metrics:
  duration: 1
  completed: 2026-06-23
---

# Quick 260623-mao: 캘리브 코너 오버레이 + 왜곡 수치 보강 Summary

체커보드 캘리브레이션 검출 결과를 (1) 검출 saddle 코너를 CalibrationViewer 위에 cyan 십자(+) 마커로 표시하고 (2) 중앙/외곽 평균 간격(px) + X/Y 축별 편차% + 종합 편차%를 리포트에 보강한다. 디스플레이/리포팅 전용 — caltab/undistort 미도입(D-07/D-08 텔레센트릭 결정 준수).

## 무엇을 했나 (쉬운 설명)

POC 시연 시 "캘리브가 제대로 됐나?"를 사람이 즉시 눈으로 판단할 수 있게 두 가지를 추가했습니다.

1. **코너 마커** — [검출] 버튼을 누르면, 프로그램이 체커보드의 격자 교차점(코너)을 어디서 잡았는지 청록색(cyan) 작은 + 표시로 이미지 위에 그려줍니다. 코너를 엉뚱한 곳에서 잡았다면 바로 보입니다. 새 이미지를 로드하거나 검출에 실패하면 이전 마커는 자동으로 사라집니다.

2. **왜곡 수치 보강** — 기존에는 "중앙↔외곽 편차 %" 한 줄만 나왔는데, 이제 중앙부 평균 간격(px) ↔ 외곽부 평균 간격(px) 실제 값과, 가로(X)·세로(Y) 축을 나눈 편차%를 함께 보여줍니다. 어느 방향으로 얼마나 왜곡됐는지 더 구체적으로 읽을 수 있습니다.

기존 임계 초과 [경고] 라벨, mm/px 적용값 계산은 전혀 건드리지 않아 회귀(부작용)는 0입니다.

## Tasks Completed

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | CalibrationResult 코너 좌표 + 왜곡 상세 수치 노출 | 7cff5a2 | CheckerboardCalibrationService.cs |
| 2 | HalconDisplayService Calib-Corners cyan 배치 렌더 분기 | 7c88d56 | HalconDisplayService.cs |
| 3 | CalibrationWindow 코너 오버레이 push + 리포트 보강 + 빌드 검증 | 8daa972 | CalibrationWindow.xaml.cs |

## 구현 상세

### Task 1 — CalibrationService
- `CalibrationResult` 에 6 프로퍼티 추가: `CornerRows`/`CornerCols` (double[], length == CornerCount), `CenterMeanPx`/`OuterMeanPx`, `DeviationXPct`/`DeviationYPct`.
- `ComputeAxisDeviationPct(List<EdgeGap>, HImage)` private static 헬퍼 — 종합 로직을 단일 축 gaps 로 분리. 동일 반경 게이트 + `AccumulateRadialGroups` 재사용 + 한쪽 그룹 비면 0% 가드.
- `TryCalibrate` 가 검출 rows/cols HTuple → double[] 변환 + 신규 멤버 전부 채움. 기존 MmPerPixel/CenterOuterDeviationPct/IsDistortionWarn 계산 무변경.

### Task 2 — HalconDisplayService
- `Render` 의 inspectionOverlays foreach 에 `Calib-Corners` 분기 추가 — FAI-EdgeRaw 패턴 미러, Points → HTuple rows/cols 누적 → `SetColor("cyan")` + `DispCross(window, rRows, rCols, 6.0, 0.0)` → `continue` (라인/큰X skip).
- 배치 위치 = FAI-EdgeRaw 형제(FAI-Edge StartsWith 분기보다 앞). HOperatorSet 호출 try/catch swallow (기존 관습).

### Task 3 — CalibrationWindow
- `ShowCornerOverlay(CalibrationResult)` private 헬퍼 — null/length mismatch 가드 후 `SetInspectionOverlays(null)`, 정상 시 `EdgeInspectionOverlay(RoiId="Calib-Corners")` 1개에 코너 zip 하여 push.
- `DetectButton_Click` 성공 분기 끝에 `ShowCornerOverlay(result)` 호출 + 4줄 보강 리포트(중앙/외곽 px + X/Y 편차%).
- 로드(`LoadImageButton_Click`) / 촬상(`GrabImageButton_Click`) / 검출 실패 3곳에 `SetInspectionOverlays(null)` 클리어 추가.
- 필요 using 추가: `System.Collections.Generic`, `ReringProject.Halcon.Models`.

## Deviations from Plan

None - plan executed exactly as written.

## 검증

- grep: Task 1~3 산출물 모두 매치 (CheckerboardCalibrationService 15건 / HalconDisplayService Calib-Corners 분기 / CalibrationWindow 10건).
- **MSBuild Debug/x64 PASS** — exit 0, 오류 0개, 경고 1개(MSB3884 ruleset 누락 = 기존 baseline, 신규 0).
- 코너 마커 색상 = "cyan" (표준명) — SetColor swallow 결함 방지.
- 오버레이 경로 = SetInspectionOverlays → HalconDisplayService.Render (HWindow 내부 DispCross) — 새 드로잉 경로 0.
- 회귀 가드: FAI-Edge*/Group-*/Datum 렌더 분기 무수정, MmPerPixel/IsDistortionWarn 계산 무수정.

## 사람 확인 필요 (SIMUL 육안 UAT — 미수행)

자동 빌드는 PASS. 다음은 사용자가 별도 확인:
- (a) 체커보드 이미지 로드 → [검출] → 뷰어 위 cyan + 마커가 코너에 표시되는가.
- (b) 리포트에 중앙/외곽 px + X/Y 편차% 표기되는가.
- (c) 새 이미지 로드 시 직전 마커가 사라지는가.

## Known Stubs

None.

## Self-Check: PASSED

- CheckerboardCalibrationService.cs: FOUND (코너 6프로퍼티 + ComputeAxisDeviationPct)
- HalconDisplayService.cs: FOUND (Calib-Corners cyan DispCross 분기)
- CalibrationWindow.xaml.cs: FOUND (ShowCornerOverlay + 보강 리포트 + 3 클리어)
- Commit 7cff5a2: FOUND
- Commit 7c88d56: FOUND
- Commit 8daa972: FOUND
- MSBuild Debug/x64: exit 0, 0 errors
