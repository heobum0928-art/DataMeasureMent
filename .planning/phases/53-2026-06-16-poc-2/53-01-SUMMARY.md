---
phase: 53-2026-06-16-poc-2
plan: 01
subsystem: Halcon Algorithm (Calibration)
tags: [calibration, checkerboard, halcon, mm-per-pixel, telecentric]
requires:
  - HALCON saddle_points_sub_pix (halcondotnet 24.11)
provides:
  - CheckerboardCalibrationService.TryCalibrate (2 overloads)
  - CalibrationResult 계약 클래스
affects:
  - WPF_Example/DatumMeasurement.csproj (Compile 등록)
tech-stack:
  added: []
  patterns:
    - "Try 접두 + out 결과 + bool + try/catch return false (VisionAlgorithmService 차용)"
    - "순수 수학 = private static (Median/Mean/버킷팅)"
    - "round(value/pitchGuess) 격자 버킷팅 (telecentric 축정렬 가정)"
key-files:
  created:
    - WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "D-07: caltab/set_calibration_data 미사용 — saddle 코너 직접 산출"
  - "D-02: mm/px 단일 평균(가로·세로 median 평균) 적용, X/Y 는 리포트값"
  - "D-05: 중앙↔외곽 편차% 게이트 + IsDistortionWarn 플래그 (임계 const, 노출)"
  - "D-08: undistort 미구현 — 검증(편차%)까지만"
metrics:
  duration_min: 5
  completed: 2026-06-23
  tasks: 3
  files: 2
---

# Phase 53 Plan 01: 체커보드 픽셀 캘리브레이션 서비스 Summary

체커보드 HImage + 칸 크기(mm) → HALCON `saddle_points_sub_pix` 서브픽셀 코너 검출 → 행/열 격자 버킷팅 → median 간격 → 단일 mm/px + X/Y 리포트 + 중앙↔외곽 왜곡% + 경고 플래그를 반환하는 순수 알고리즘 서비스 신규 작성. 다음 wave(CalibrationWindow)가 소비할 계약(서비스 + 결과 클래스) 확정.

## 다음 wave 가 참조할 계약

### CalibrationResult (public 프로퍼티 7종)
| 프로퍼티 | 타입 | 의미 |
|---------|------|------|
| `MmPerPixel` | double | 단일 적용값 (D-02, PixelResolution 반영 대상) |
| `MmPerPixelX` | double | 리포트 참고값 (가로 median 기반) |
| `MmPerPixelY` | double | 리포트 참고값 (세로 median 기반) |
| `MeanSpacingPx` | double | 평균(가로·세로) 격자 간격(px) |
| `CenterOuterDeviationPct` | double | 중앙↔외곽 간격 편차% (D-05) |
| `IsDistortionWarn` | bool | 편차% 임계 초과 경고 (D-05 게이트) |
| `CornerCount` | int | 검출된 코너 수 |

### TryCalibrate 시그니처 2종 (CheckerboardCalibrationService, public instance)
```csharp
// 짧은 오버로드 — 기본 saddle/왜곡 임계 const 위임
public bool TryCalibrate(HImage image, double knownMmPerCell,
    out CalibrationResult result, out string error);

// 긴 오버로드 — saddle Sigma/Threshold + 왜곡 임계% 노출 (A2/A3 UAT 튜닝)
public bool TryCalibrate(HImage image, double knownMmPerCell,
    double saddleSigma, double saddleThreshold, double distortionWarnPct,
    out CalibrationResult result, out string error);
```

### 노출 상수 (public const)
- `DefaultSaddleSigma = 1.0`
- `DefaultSaddleThreshold = 5.0`
- `DistortionWarnThresholdPct = 1.0`
- `MinCornerCount = 12`

## 구현 메모

- 코너 검출: `HOperatorSet.SaddlePointsSubPix(image, "facet", sigma, threshold, out rows, out cols)` — try/catch return false. 코너 < `MinCornerCount` 시 한국어 에러 + false.
- 격자 피치 1차 추정 `EstimatePitchGuess`: 각 코너 최근접 이웃 거리의 median → 버킷팅 허용오차(pitch×0.5).
- 행/열 버킷팅: `round(axis / pitchGuess)` 인덱스 버킷 → 버킷 내 정렬 → 인접 차 수집. 누락 코너의 2×피치 점프는 `Median`이 자연 배제 (Anti-Pattern: mean 직접 사용 금지).
- 편차%: 각 간격 중점의 이미지 중심 거리 r 로 중앙(r≤diag×0.33)/외곽(r≥diag×0.66) 그룹 분리 → `abs(외곽평균-중앙평균)/중앙평균×100`. 한쪽 그룹 비면 0% 가드.
- `EdgeGap` internal struct(간격 + 중점 좌표)로 편차 반경 판정에 중점 좌표 캐리.

## Deviations from Plan

None - plan executed exactly as written.

(주: Task 2 acceptance 의 `set_calibration_data|...` grep==0 충족을 위해 Task 1 의 XML-doc 주석 문구 "caltab/set_calibration_data 미사용" → "풀 카메라 캘리브(HALCON 전용 점판) 미사용" 으로 표현만 조정. 동작/anti-pattern 부재는 동일.)

## Commits

- `5a1fba5` feat(53-01): CalibrationResult + 서비스 골격 + 상수 정의
- `ee07de5` feat(53-01): 코너검출 + 격자 median + mm/px + 외곽 편차% 구현
- `eebe205` chore(53-01): csproj 등록 + Debug/x64 빌드 검증

## Verification

- Debug/x64 MSBuild PASS — exit 0, 신규 에러/경고 0 (기존 CS0618/CS0162/MSB3884 baseline 유지).
- grep: `SaddlePointsSubPix` / `private static double Median` / `ComputeCenterOuterDeviationPct` / `IsDistortionWarn = devPct >` 매치, caltab/undistort 토큰 0.
- SIMUL UAT (실 검출 정확도/왜곡)는 plan 02/03 (UI 소비) 및 실측 체커보드 이미지 확보 후. 이 plan 은 UI 없음 — 빌드 검증으로 충분.

## Known Stubs

None — 본 plan 은 알고리즘 계층 완성. UI 미연결은 의도된 wave 분할(plan 02 CalibrationWindow 가 소비).

## Self-Check: PASSED

- CheckerboardCalibrationService.cs 존재 확인
- 53-01-SUMMARY.md 존재 확인
- 커밋 5a1fba5 / ee07de5 / eebe205 모두 git log 존재 확인
