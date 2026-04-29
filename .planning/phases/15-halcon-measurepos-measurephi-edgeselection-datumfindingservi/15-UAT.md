---
phase: 15
status: pending
created: 2026-04-29
updated: 2026-04-29
total_tests: 15
passed: 0
pending: 15
---

# Phase 15 UAT — HALCON MeasurePos 정합성 (measurePhi 매핑 + EdgeSelection 명시 처리)

## 사전 준비
- VS 에서 DatumMeasurement.csproj Rebuild → bin/x64/Debug/DatumMeasurement.exe
- HALCON 24.11 + 카메라 SDK 설치 확인
- 기존 Setting.ini (Phase 14-05 사용된 레시피) 백업 후 로드

## 자동 검증 (Plan 15-01~03 에서 PASS 확인)
- [ ] 빌드 PASS, 신규 warning 0
- [ ] grep 검증: `grep -c "_EdgeSelection," DatumFindingService.cs` == 7
- [ ] grep 검증: `grep "measurePhi" DatumFindingService.cs` ≥ 4
- [ ] grep 검증: `grep "MeasurePos.*\"all\"" DatumFindingService.cs` 0 매치 (helper 안)
- [ ] INI 하위호환: 기존 레시피 로드 후 PropertyGrid 의 6 ROI EdgeSelection 모두 "First" 표시

## Tests

### Test 1 — TwoLineIntersect × LtoR (Line1 + Line2)
- 시나리오: Line1 ROI 그리고 EdgeDirection=LtoR, Line2 ROI 그리고 EdgeDirection=LtoR → btn_teachDatum
- 기대: 두 라인 모두 raw 에지점 + 검출 라인(cyan/magenta) 표시, 교점/각도 좌표 라벨 표시
- result: pending

### Test 2 — TwoLineIntersect × RtoL
- 시나리오: 동일하되 EdgeDirection=RtoL
- 기대: 검출 PASS, raw 점이 ROI 우→좌 방향으로 분포
- result: pending

### Test 3 — TwoLineIntersect × TtoB
- 시나리오: EdgeDirection=TtoB (수직 strip 슬라이스)
- 기대: 검출 PASS — Phase 15 핵심 회귀 (이전엔 자동 rp 가 의도와 부호 반전 가능)
- result: pending

### Test 4 — TwoLineIntersect × BtoT
- 시나리오: EdgeDirection=BtoT (수직 strip, polarity 부호 반전 의도)
- 기대: 검출 PASS — Phase 15 핵심 회귀 (실 데이터 UAT 에서 노출됐던 결함의 직접 검증)
- result: pending

### Test 5 — CircleTwoHorizontal × LtoR (Horizontal_A/B), Circle EdgeSelection=First
- 시나리오: Circle ROI + Horizontal_A + Horizontal_B 모두 LtoR, Circle_EdgeSelection=First
- 기대: 원 검출 + 360° polar 점 + 수평 2-ROI concat 라인 + 교점 표시
- result: pending

### Test 6 — CircleTwoHorizontal × Horizontal direction = RtoL/BtoT 혼합
- 시나리오: Horizontal_A=LtoR, Horizontal_B=RtoL (또는 BtoT 시도)
- 기대: 둘 다 정상 검출 (이전엔 BtoT 부호 반전으로 0 edges)
- result: pending

### Test 7 — VerticalTwoHorizontal × Vertical TtoB + Horizontal LtoR
- 시나리오: Vertical ROI EdgeDirection=TtoB, Horizontal_A/B=LtoR
- 기대: 수직 라인 검출 + 수평 concat 라인 + 교점
- result: pending

### Test 8 — VerticalTwoHorizontal × Vertical BtoT
- 시나리오: Vertical EdgeDirection=BtoT (수직 ROI 부호 반전 — Phase 15 핵심)
- 기대: 검출 PASS
- result: pending

### Test 9 — EdgeSelection First → Last → All 변경 회귀 (다중 에지 ROI 권장)
- 시나리오: TwoLineIntersect Line1 의 EdgeSelection 을 First → Last → All 로 변경하며 재티칭
- 기대: **최소 한 시나리오에서** First→Last 변경시 검출 라인 위치/raw 점 분포가 변화 확인 (단일 에지 ROI 는 First==Last 일 수 있음 — 주의)
- 사용자 가이드: **다중 에지 ROI** 사용 권장 — ROI 안에 여러 명암 전이가 있는 영역(예: 패턴 경계, 글자 영역, 두 평행 에지가 있는 부품) 을 선택하면 First/Last 차이 명확히 관찰 가능. 단일 에지만 있는 ROI 에서는 First/Last/All 결과가 모두 동일할 수 있음 — 이 경우 ROI 위치를 다중 에지 영역으로 옮긴 후 재시도.
- result: pending

### Test 10 — ROI 이동 후 자동 재티칭 (TwoLineIntersect)
- 시나리오: 티칭 완료 후 Line1 ROI 드래그 이동
- 기대: RoiMoveCompleted → InvokeTryTeachDatum 자동 호출 → 새 위치에서 재검출
- result: pending

### Test 11 — ROI 이동 후 자동 재티칭 (CircleTwoHorizontal)
- 시나리오: Circle ROI 또는 Horizontal_A ROI 드래그 이동
- 기대: 자동 재검출 + 새 위치 반영
- result: pending

### Test 12 — ROI 이동 후 자동 재티칭 (VerticalTwoHorizontal)
- 시나리오: Vertical ROI 드래그 이동
- 기대: 자동 재검출 + 새 위치 반영
- result: pending

### Test 13 — #1405 IntersectionLl carry-over 검증 (CTH 재티칭 후 IntersectionLl PASS)
- 시나리오: CircleTwoHorizontal 티칭 후 ROI 이동 + 재티칭 → IntersectionLl 호출 정상 (length-2 fit 인자 길이 불일치 없음)
- 기대: 예외 없음, 교점 좌표 출력
- 출처: 260429-c2e quick task — UAT 미완료 carry-over 4건 중 핵심 1건
- result: pending

### Test 14 — #1405 carry-over: VTH ConcatObj→TupleConcat 패턴 회귀
- 시나리오: VerticalTwoHorizontal 재티칭 → 교점 정상 출력
- 출처: 260429-c2e
- result: pending

### Test 15 — SIMUL_MODE Phase 14-05 동일 시나리오 재실행
- 시나리오: SIMUL 데이터로 3 알고리즘 btn_testFindDatum 통합 (Phase 14-05 UAT 시나리오 그대로 재실행)
- 기대: 모두 PASS (회귀 0)
- result: pending

## Summary
- Total: 15
- Passed: 0
- Pending: 15
- Failed: 0

## Sign-off
- 사용자: pending
- 일자: pending
- 비고: pending
