---
status: partial
phase: 13-datum-algorithm-extensibility
source:
  - 13-01-SUMMARY.md
  - 13-02-SUMMARY.md
  - 13-03-SUMMARY.md
  - 13-04-SUMMARY.md
  - 13-05-SUMMARY.md
started: 2026-04-26T02:08:15Z
updated: 2026-04-26T03:00:00Z
---

## Current Test

[testing complete — 1 blocked item outstanding]

## Tests

### 1. Cold Start + INI Migration
expected: 앱 새로 시작 + 기존 INI 로드 시 5 ROI Edge 그룹 6항목이 글로벌 fallback 값으로 자동 채워지고 legacy 그룹은 숨김 (Plan 13-04 EnsurePerRoiDefaults)
result: issue
reported: "1.circle ROI 이동 안됌 / 2. Vertical 파라미터들이 보이지 않음"
severity: major

### 2. Direction Validation Gate (Req 5d / Plan 13-01)
expected: CircleTwoHorizontal 또는 VerticalTwoHorizontal 알고리즘에서 Horizontal_A/B를 일부러 비스듬히(20°+) 그려 티칭하면 빨강 라벨에 "Horizontal line orientation out of range" 또는 "Horizontal/Vertical perpendicularity violated" 에러가 표시되며 LastTeachSucceeded=false 처리
result: blocked
blocked_by: prior-phase
reason: "User reported: 1) Circle 알고리즘 재설계 필요(center+radius 360° 에지 추출) — 별도 phase 이월 / 2) VerticalTwoHorizontal은 Vertical 에지 파라미터 자체가 없어 (Test 1의 Vertical 파라미터 누락 이슈와 동일) 회전 out-of-range 시나리오 실행 불가 / 3) TwoLineIntersect로 휘어진 각도를 줘도 datum origin 검출됨 — Plan 13-01 D-12 scope 제한(TwoLineIntersect는 검증 없음)이라 의도된 동작이지만 사용자 기대(어떤 알고리즘이든 휘어졌으면 fail)와 불일치 → UX 갭"

### 3. btn_testFindDatum Runtime Test (Plan 13-02)
expected: Datum 티칭 완료 후 "Datum Find 테스트" 버튼 클릭 → 3-way 선택(현재 이미지 / 다른 파일 / 취소). 성공 시 LimeGreen label에 "TryFind OK — RefOrigin=(...), Angle=... rad" 표시 + 주황 십자 오버레이. 미티칭 상태에서는 "Datum 티칭이 완료된 후 테스트 가능합니다." CustomMessageBox.
result: issue
reported: "TwoLineIntersect: 다이얼로그/녹색 좌표/주황 십자/빨강 에러 모두 정상 — 단 각도 틀어져도 out of range 안 뜸 / Circle, CircleTwoHorizontal, VerticalTwoHorizontal: 정상 시행 X"
severity: major

### 4. Datum ROI Drag-Move + Auto Re-Teach (Plan 13-03)
expected: 티칭 완료된 Datum ROI(Rect/Circle)를 마우스 드래그로 이동하면 DatumConfig Row/Col 필드에 delta가 반영되고 TryTeachDatum이 자동 재호출되어 검출 오버레이(십자/원/외삽 라인/raw 점/라벨)가 즉시 새 위치를 반영
result: pass
note: "TwoLineIntersect 1/2/3항목 전부 정상 (다른 알고리즘은 Test 1/3에서 별도 logged)"

### 5. Datum ROI Right-Click Delete + Re-Teach (Plan 13-03)
expected: Datum ROI 우클릭 → Delete 메뉴 활성화(hotfix 136de8e) → 클릭 시 해당 ROI 필드 0 reset + IsConfigured/LastTeachSucceeded=false → 오버레이에서 ROI 사라지고, 이후 btn_teachDatum으로 재티칭 가능
result: pass
note: "1/2/3 정상 + Datum 티칭 재진입 → L1/L2 새로 그리기 → 재티칭 정상"

### 6. Per-ROI Edge Parameter Effect (Plan 13-04)
expected: PropertyGrid에서 특정 ROI의 EdgeThreshold(예: Line1_EdgeThreshold)를 변경 → 재티칭 시 검출 라인/raw 점 분포가 변화. 다른 ROI는 영향 없음(per-ROI 독립). PhiDeg는 도(degree) 단위로 입력/표시.
result: issue
reported: "1~4 전부 정상 / 단 PropertyGrid 파라미터 변경 후 ROI를 살짝 이동해야만 검출 결과 갱신됨 (자동 재티칭이 파라미터 변경에 미반응)"
severity: minor

### 7. Detected Line Extension + Raw Edge Points + Ref Coords Label (Plan 13-05)
expected: Datum 티칭 성공 후 검출 라인(Line1/Line2)이 이미지 가장자리까지 연장되어 표시되고, 5 ROI별 raw 에지점이 색상 십자(Line1=cyan, Line2=magenta, HorizA=green, HorizB=lime; Circle은 빈 HTuple)로 분포. 캔버스 옆 label_datumRefCoords에 RefOrigin/Angle (CircleTwoHorizontal일 때 CircleCenter/Radius 추가) 텍스트 표시.
result: pass

### 8. FAI Regression
expected: FAI 검사 흐름(ROI 그리기/이동/삭제/측정)이 Phase 13 변경의 영향 없이 정상 동작 — Datum과 FAI 경로는 독립
result: pass

## Summary

total: 8
passed: 4
issues: 3
pending: 0
skipped: 0
blocked: 1

## Gaps

- truth: "Cold Start 후 PropertyGrid에서 5 ROI 그룹 6항목 모두 표시 + Circle ROI 드래그 이동 가능 + VerticalTwoHorizontal 알고리즘에서 수직 ROI 파라미터 표시"
  status: failed
  reason: "User reported: 1.circle ROI 이동 안됌 / 2. Vertical 파라미터들이 보이지 않음"
  severity: major
  test: 1
  artifacts: []
  missing: []

- truth: "btn_testFindDatum이 모든 알고리즘(TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal)에서 동작 + 각도 out-of-range 시 fail 처리"
  status: failed
  reason: "User reported: TwoLineIntersect만 정상(다이얼로그/녹색/주황/빨강 OK), 단 각도 틀어져도 out-of-range 미발동(D-12 scope 제한 — UX 갭). Circle/CircleTwoHorizontal/VerticalTwoHorizontal 알고리즘 자체가 정상 시행 불가 → btn_testFindDatum 검증 막힘."
  severity: major
  test: 3
  artifacts: []
  missing: []

- truth: "PropertyGrid에서 per-ROI 파라미터를 변경하면 즉시 자동 재티칭이 발동되어 검출 결과가 갱신되어야 함"
  status: failed
  reason: "User reported: 파라미터 변경 후 ROI를 살짝 이동시켜야만 검출 결과가 갱신됨. PropertyChanged 이벤트가 InvokeTryTeachDatum 트리거에 연결돼 있지 않아, 파라미터 변경만으로는 자동 재티칭이 발동되지 않는 UX 갭."
  severity: minor
  test: 6
  artifacts: []
  missing: []

## Routing Decision (2026-04-26)

사용자 합의: **옵션 2 — minor patch + major 분리**

### Phase 13-06 (이번 phase 내 patch)
- **Test 6 (minor)**: per-ROI PropertyGrid 파라미터 변경 시 자동 재티칭 트리거 연결
  - 변경 위치 후보: DatumConfig PropertyChanged 이벤트 → MainView InvokeTryTeachDatumForEdit 호출
  - 또는 PropertyGrid의 ValueChanged 이벤트에서 _editingDatum != null + LastTeachSucceeded일 때 재티칭 발동

### Phase 14 (별도 phase로 분리)
- **Test 1 issue 1/2 + Test 3 + Test 2 blocked + Carry-over Circle 재설계**:
  - **Vertical 에지 파라미터 그룹 신설** — VerticalTwoHorizontal 알고리즘에서 수직 ROI(현재 Line1 매핑)의 의미적 라벨링 + PropertyGrid 카테고리 분리. RoiId 규약/데이터 모델 영향.
  - **Circle ROI 이동 회귀 fix** — Plan 13-03에서 PASS였는데 후속 작업(13-04/05) 중 어디서 깨짐. _datumRoiCandidates publish 누락 가능성.
  - **Circle 알고리즘 재설계** — center+radius 기점 360° 에지 추출 방식으로 전환. VisionAlgorithmService.TryFindCircle raw 에지점 반환 확장 포함 (Plan 13-05의 Circle raw 점 carry-over도 동시 해결).
  - **CircleTwoHorizontal / VerticalTwoHorizontal 정상 시행 불가 원인 조사** — 13-04 strip-loop 패턴이 horizontal 알고리즘에 잘못 적용됐을 가능성 / per-ROI 파라미터 누락 / Phi 와이어링 결함.
  - **각도 out-of-range UX 갭** — TwoLineIntersect도 검증 게이트 적용 검토(D-12 scope 확장).
