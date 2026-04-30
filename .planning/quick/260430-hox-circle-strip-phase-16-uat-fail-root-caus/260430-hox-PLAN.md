---
quick_id: 260430-hox
description: Circle strip 사각형 크기 대폭 축소 — Phase 16 UAT FAIL root cause
date: 2026-04-30
mode: quick
---

# Quick 260430-hox: Circle strip 사각형 크기 축소

## 배경

Phase 16 UAT (16-UAT.md) Test 1/3/5 모두 FAIL:
- Test 1: pre-teach 시각화 strip 사각형이 화면을 덮을 만큼 거대 (image 증거)
- Test 3: 검출 원/center cross strip 노이즈에 묻힘
- Test 5: `"Datum 검증 실패: Circle 0 failed: insufficient polar samples (1)"` — 36개 polar 각도 중 1개만 edge 검출

Root cause: `Circle_RectL1Ratio` / `Circle_RectL2Ratio` default 0.05 + cap 부재 → recipe 값 / 큰 radius 조합 시 strip 이 의미 없을 만큼 거대해져 MeasurePos edge 노이즈.

사용자 directive (verbatim): "ui적으로 circle도 반지름 부근 사각형 사이즈를 현격하게 줄여"

## 수정 대상 (3 파일)

원래 Phase 16 D-22 (알고리즘 보존) 부분 완화 — Phase 16 결함 수정이지 plan 변경 아님.

### 1. WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
- `Circle_RectL1Ratio` default 0.05 → **0.02** (radius 200 → half 4px = 총 8px strip)
- `Circle_RectL2Ratio` default 0.05 → **0.02**

### 2. WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (TryFindCircleByPolarSampling)
- line 269-272 (`halfL1`, `halfL2`) 에 절대 pixel cap 추가:
  - `halfL1 = Math.Min(radius * rectL1Ratio, 12.0)` (max 24px strip)
  - `halfL2 = Math.Min(radius * rectL2Ratio, 12.0)`
  - 기존 `< 1.0 → 1.0` floor 보존
- 효과: recipe 에 잘못 저장된 큰 ratio 도 안전하게 cap

### 3. WPF_Example/Halcon/Display/HalconDisplayService.cs (RenderCircleStripOverlay)
- line 456-460 동일 cap 적용 (viz/algo 식 일관성):
  - `length1 = Math.Min(radius * datum.Circle_RectL1Ratio, 12.0)`
  - `length2 = Math.Min(radius * datum.Circle_RectL2Ratio, 12.0)`
  - 기존 `< 1.0 → 1.0` floor 보존

## 변경 이유 라벨

- 모든 변경 라인에 `//260430 hbk Quick 260430-hox` 주석 추가
- D-22 부분 완화 사유 명시: "Phase 16 root cause fix — viz/algo 식 일치 유지"

## 검증

1. Build: `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` PASS
2. 사용자 수동 UAT 재실행 (16-UAT.md):
   - Test 1: strip 사각형 크기 현저히 축소 확인
   - Test 5: `insufficient polar samples` 에러 사라지고 검출 원 생성 확인

## 범위 밖 (Phase 17 carry-over 유지)

- 시각화 정책 재설계 (N개 → 1개 strip 표시)
- PropertyGrid UI 추가 (각도 step, edge 검출 방향)
- teach trigger UX 명확화

## must_haves

- DatumConfig.cs Circle_RectL1Ratio/RectL2Ratio default = 0.02
- VisionAlgorithmService.cs halfL1/halfL2 에 12.0 px cap
- HalconDisplayService.cs length1/length2 에 12.0 px cap
- 빌드 PASS, 신규 warning 0
- //260430 hbk Quick 260430-hox 주석 ≥ 5
