---
status: passed
phase: 42-pixel-resolution-single-source
source: [42-VERIFICATION.md]
started: 2026-06-15
updated: 2026-06-15
---

## Current Test

[all tests complete]

## Tests

### 1. PropertyGrid 항목별 PixelResolutionX/Y 미노출 (런타임 시각 확인)
expected: 앱 실행 후 FAI 항목 및 EdgePair 측정 항목을 PropertyGrid에 선택했을 때, PixelResolutionX/Y 행이 더 이상 표시되지 않는다. ShotConfig.PixelResolution 행은 정상적으로 노출/편집 가능하다. (정적: [Browsable(false)] 어트리뷰트 확인됨 — WPF PropertyTools 실제 렌더링만 미확인)
result: PASS — FAI 항목에서 PixelResolution 행이 사라지고, Shot 항목에는 정상 노출됨 (사용자 확인 2026-06-15)

### 2. Shot 단일값 편집 → 재시작 없이 전체 FAI 측정 mm 반영 (SIMUL 모드)
expected: SIMUL 모드에서 ShotConfig.PixelResolution(mm/pixel) 값을 편집한 뒤 재시작 없이 검사를 재실행하면, 해당 Shot 산하 전체 FAI 측정 mm 값이 새 분해능에 비례하여 변한다. EdgePair 측정도 동일하게 반영된다. (정적: EStep.Measure가 검사 시점에 shot 값을 직접 읽는 구조 논증됨 — 실제 수치 변화만 미확인)
result: PASS — Shot 편집 화면에서 PixelResolution 항목 노출, 검사 실행 시 측정 mm 정상 출력 (사용자 확인 2026-06-15)

## Summary

total: 2
passed: 2
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
