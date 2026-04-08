---
status: partial
phase: 02-teaching-calibration
source: [02-VERIFICATION.md]
started: 2026-04-08T13:00:00+09:00
updated: 2026-04-08T13:00:00+09:00
---

## Current Test

[awaiting human testing]

## Tests

### 1. Rect ROI 드래그
expected: 캔버스에서 드래그하면 사각형 ROI가 그려지고 FAIConfig에 저장된다
result: [pending]

### 2. Polygon ROI 점 클릭 + 우클릭 완성
expected: 점을 클릭하면 다각형이 그려지고 우클릭으로 완성되어 FAIConfig.PolygonPoints에 저장된다
result: [pending]

### 3. 2점 캘리브레이션 플로우
expected: 2점 클릭 후 mm 입력 다이얼로그에서 값 입력 시 mm/pixel이 CameraSlaveParam.PixelResolution에 저장된다
result: [pending]

### 4. ROI 하이라이트 색상
expected: FAI 선택 시 해당 ROI 노란색, 미선택 ROI 초록색으로 표시된다
result: [pending]

### 5. 에지 방향 화살표
expected: 선택 ROI 중앙에 흰색 에지 검색 방향 화살표가 표시된다
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
