---
status: complete
phase: 01-ui
source: [01-01-SUMMARY.md, 01-02-SUMMARY.md]
started: 2026-04-07T09:00:00Z
updated: 2026-04-07T09:30:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Tree FAI 노드 표시
expected: 앱 시작 시 트리에 Sequence > Action > FAI 계층이 보이고, 트리가 자동 펼침 상태
result: pass

### 2. FAI 선택 → PropertyGrid
expected: FAI 노드 선택 시 우측 PropertyGrid에 ROI, Edge Measurement, Tolerance 카테고리 표시
result: pass

### 3. FAI 선택 → 캔버스 이미지
expected: FAI 노드 선택 시 부모 Shot에 이미지가 없으면 "NO Image", 있으면 캔버스에 표시
result: issue
reported: "Top 검사 아래 Shot_0에서 로드한 이미지와 Inspect에서 로드한 이미지가 같다"
severity: minor

### 4. FAI 선택 → DataGrid 1행 표시
expected: FAI 노드 하나 선택 시 하단 DataGrid에 해당 FAI 1행만 표시
result: pass

### 5. Shot/Action 선택 → DataGrid 전체 FAI 표시
expected: Shot(Action) 노드 선택 시 하단 DataGrid에 해당 Shot의 모든 FAI가 행으로 표시
result: pass

### 6. Add FAI 버튼
expected: Shot 노드 선택 후 Add 버튼 → 이름 입력 → 트리에 새 FAI 노드 추가 (크래시 없음)
result: pass

### 7. Remove FAI 버튼
expected: FAI 노드 선택 후 Remove 버튼 → 확인 다이얼로그 → Yes 시 트리에서 FAI 제거
result: issue
reported: "FAI는 제거되지만 그 부모 SHOT 노드는 제거가 안됨"
severity: major

### 8. Rename FAI 버튼
expected: FAI 노드 선택 후 Rename 버튼 → 이름 수정 다이얼로그 → 확인 후 트리 노드 이름 변경
result: pass

### 9. Add Shot (Sequence 노드에서)
expected: Sequence 노드 선택 후 Add 버튼 → Shot 이름 입력 → Shot + FAI_0 추가 (크래시 없음)
result: pass

### 10. DataGrid 다크 테마 가독성
expected: DataGrid 헤더와 셀 텍스트가 다크 배경에서 흰색으로 명확히 읽힘
result: pass

### 11. MainView 레이아웃
expected: MainView가 상단 캔버스 + 하단 DataGrid 2단 구조. 좌측 TreeView 컬럼 없음.
result: pass

## Summary

total: 11
passed: 9
issues: 2
pending: 0
skipped: 0

## Gaps

- truth: "FAI 선택 시 부모 Shot 이미지가 독립적으로 표시되어야 함"
  status: failed
  reason: "User reported: Shot_0과 Inspect가 같은 이미지를 공유함 — Shot별 독립 이미지 버퍼 미사용"
  severity: minor
  test: 3
  artifacts: []
  missing: []

- truth: "Shot 노드도 삭제 가능해야 함"
  status: failed
  reason: "User reported: FAI는 제거되지만 부모 SHOT 노드는 제거 불가"
  severity: major
  test: 7
  artifacts: []
  missing: []
