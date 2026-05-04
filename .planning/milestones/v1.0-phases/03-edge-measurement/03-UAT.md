---
status: complete
phase: 03-edge-measurement
source: [03-01-SUMMARY.md, 03-02-SUMMARY.md, 260409-e3v-SUMMARY.md]
started: "2026-04-09T01:20:00Z"
updated: "2026-04-09T01:35:00Z"
---

## Current Test

[testing complete]

## Tests

### 1. FAIConfig 에지 파라미터 PropertyGrid 표시
expected: FAI 노드 선택 시 PropertyGrid에 EdgeDirection, EdgeSelection, EdgeSampleCount, EdgeTrimCount, EdgePolarity 속성이 표시된다. EEdgeMeasureType/MeasureType는 더 이상 표시되지 않는다.
result: pass

### 2. FAIEdgeMeasurementService 에지 측정 실행
expected: FAI에 ROI가 설정되고 에지가 있는 이미지에서 시퀀스 ���사 실행 시, MeasurePos로 에지가 검출되고 mm 단위 거리가 계산된다.
result: pass

### 3. 공차 판정 (OK/NG)
expected: 측정된 거리가 NominalValue ± Tolerance 범위 내이면 OK, 벗어나면 NG로 판정된다. DataGrid FAIResultRow에 판정 결과가 표시된다.
result: pass

### 4. 캔버스 에지 오버레이 표��
expected: 측정 후 캔버스에 에지 위치 라인이 OK=green/NG=red로 표시되고, 에지 간 연결선이 cyan으로 표시된다.
result: pass

### 5. DisplayMessages 텍스트 표시
expected: 측정 후 화면 상단에 FAI별 측정 결과 텍스트(FAI명, 거리mm, OK/NG)가 yellow로 표시된다.
result: pass

### 6. EdgeSelection 동작
expected: Both=첫 번째+마지막 에지를 각각 라인 피팅하여 거리 측정. First=스캔 방향 첫 번째 에지 전이점으로 라인 피팅. Last=마지막 에지 전이점으로 라인 피팅.
result: pass

### 7. EdgeDirection 스캔 방향 동작
expected: LtoR=왼쪽→오른쪽, RtoL=오른쪽→왼쪽, TtoB=위→아래, BtoT=아래→위 스캔 방향 변경.
result: pass

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0

## Gaps

[none]
