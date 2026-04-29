---
phase: 15
status: partial
created: 2026-04-29
updated: 2026-04-29
total_tests: 15
passed: 5
pending: 0
not_tested: 8
failed: 2
gaps_carry_over_to: phase-16
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
- result: PASS (2026-04-29 user-confirmed — 4-way 매핑 의도대로 동작)

### Test 2 — TwoLineIntersect × RtoL
- 시나리오: 동일하되 EdgeDirection=RtoL
- 기대: 검출 PASS, raw 점이 ROI 우→좌 방향으로 분포
- result: PASS (2026-04-29 user-confirmed)

### Test 3 — TwoLineIntersect × TtoB
- 시나리오: EdgeDirection=TtoB (수직 strip 슬라이스)
- 기대: 검출 PASS — Phase 15 핵심 회귀 (이전엔 자동 rp 가 의도와 부호 반전 가능)
- result: PASS (2026-04-29 user-confirmed — measurePhi 명시 매핑으로 부호 결함 해결)

### Test 4 — TwoLineIntersect × BtoT
- 시나리오: EdgeDirection=BtoT (수직 strip, polarity 부호 반전 의도)
- 기대: 검출 PASS — Phase 15 핵심 회귀 (실 데이터 UAT 에서 노출됐던 결함의 직접 검증)
- result: PASS (2026-04-29 user-confirmed — BtoT 0 edges 결함 해결)

### Test 5 — CircleTwoHorizontal × LtoR (Horizontal_A/B), Circle EdgeSelection=First
- 시나리오: Circle ROI + Horizontal_A + Horizontal_B 모두 LtoR, Circle_EdgeSelection=First
- 기대: 원 검출 + 360° polar 점 + 수평 2-ROI concat 라인 + 교점 표시
- result: FAIL (2026-04-29 user-reported) — Circle 검출 알고리즘 패턴 자체가 의도와 다름. 현재: ROI 원을 Rectangle 로 변환 후 제자리 회전. 의도: 원 ROI → 왼쪽 반지름 끝점으로 strip 이동 → 원호 포함 작은 사각형 → 원 센터 기준 1°/10° 회전 → 안→밖 or 밖→안 에지 → 360° → fit_circle_contour_xld. 별도 phase-16 plan 으로 carry-over.

### Test 6 — CircleTwoHorizontal × Horizontal direction = RtoL/BtoT 혼합
- 시나리오: Horizontal_A=LtoR, Horizontal_B=RtoL (또는 BtoT 시도)
- 기대: 둘 다 정상 검출 (이전엔 BtoT 부호 반전으로 0 edges)
- result: not_tested — Test 5 Circle 결함이 선행 조건. Phase 16 Circle 재설계 후 재검증.

### Test 7 — VerticalTwoHorizontal × Vertical TtoB + Horizontal LtoR
- 시나리오: Vertical ROI EdgeDirection=TtoB, Horizontal_A/B=LtoR
- 기대: 수직 라인 검출 + 수평 concat 라인 + 교점
- result: not_tested — 사용자 보고 누락. measurePhi 매핑 동일 메커니즘이므로 PASS 추정 가능, Phase 16 회귀 시 보충 검증.

### Test 8 — VerticalTwoHorizontal × Vertical BtoT
- 시나리오: Vertical EdgeDirection=BtoT (수직 ROI 부호 반전 — Phase 15 핵심)
- 기대: 검출 PASS
- result: not_tested — Test 7 동일.

### Test 9 — EdgeSelection First → Last → All 변경 회귀 (다중 에지 ROI 권장)
- 시나리오: TwoLineIntersect Line1 의 EdgeSelection 을 First → Last → All 로 변경하며 재티칭
- 기대: **최소 한 시나리오에서** First→Last 변경시 검출 라인 위치/raw 점 분포가 변화 확인 (단일 에지 ROI 는 First==Last 일 수 있음 — 주의)
- 사용자 가이드: **다중 에지 ROI** 사용 권장 — ROI 안에 여러 명암 전이가 있는 영역(예: 패턴 경계, 글자 영역, 두 평행 에지가 있는 부품) 을 선택하면 First/Last 차이 명확히 관찰 가능. 단일 에지만 있는 ROI 에서는 First/Last/All 결과가 모두 동일할 수 있음 — 이 경우 ROI 위치를 다중 에지 영역으로 옮긴 후 재시도.
- result: PASS (2026-04-29 user-confirmed — First/Last/All 선택 가능 + 의도대로 동작)

### Test 10 — ROI 이동 후 자동 재티칭 (TwoLineIntersect)
- 시나리오: 티칭 완료 후 Line1 ROI 드래그 이동
- 기대: RoiMoveCompleted → InvokeTryTeachDatum 자동 호출 → 새 위치에서 재검출
- result: FAIL (2026-04-29 user-reported) — Datum 알고리즘 타입 binding 결함. 처음 레시피 로드 시엔 Datum 1/2/3 선택 시 알고리즘 타입(TwoLineIntersect/CTH/VTH) 정상 전환되나, ROI 이동/생성 후엔 Datum 변경해도 알고리즘 타입 갱신 안 됨. PropertyGrid AlgorithmType binding refresh 누락. 별도 phase-16 plan 으로 carry-over.

### Test 11 — ROI 이동 후 자동 재티칭 (CircleTwoHorizontal)
- 시나리오: Circle ROI 또는 Horizontal_A ROI 드래그 이동
- 기대: 자동 재검출 + 새 위치 반영
- result: FAIL (2026-04-29 user-reported) — Test 10 동일 결함 + Test 5 Circle 결함 중첩. Phase 16 carry-over.

### Test 12 — ROI 이동 후 자동 재티칭 (VerticalTwoHorizontal)
- 시나리오: Vertical ROI 드래그 이동
- 기대: 자동 재검출 + 새 위치 반영
- result: FAIL (2026-04-29 user-reported) — Test 10 동일 결함. Phase 16 carry-over.

### Test 13 — #1405 IntersectionLl carry-over 검증 (CTH 재티칭 후 IntersectionLl PASS)
- 시나리오: CircleTwoHorizontal 티칭 후 ROI 이동 + 재티칭 → IntersectionLl 호출 정상 (length-2 fit 인자 길이 불일치 없음)
- 기대: 예외 없음, 교점 좌표 출력
- 출처: 260429-c2e quick task — UAT 미완료 carry-over 4건 중 핵심 1건
- result: not_tested — Test 5 Circle 결함이 선행 조건. Phase 16 Circle 재설계 후 재검증.

### Test 14 — #1405 carry-over: VTH ConcatObj→TupleConcat 패턴 회귀
- 시나리오: VerticalTwoHorizontal 재티칭 → 교점 정상 출력
- 출처: 260429-c2e
- result: not_tested — Test 12 동일.

### Test 15 — SIMUL_MODE Phase 14-05 동일 시나리오 재실행
- 시나리오: SIMUL 데이터로 3 알고리즘 btn_testFindDatum 통합 (Phase 14-05 UAT 시나리오 그대로 재실행)
- 기대: 모두 PASS (회귀 0)
- result: not_tested — Phase 16 종료 후 통합 회귀로 보충.

## Summary
- Total: 15
- Passed: 5 (Test 1, 2, 3, 4, 9 — TwoLineIntersect 4-way + EdgeSelection)
- Failed: 4 (Test 5 Circle 알고리즘 패턴 / Test 10~12 Datum AlgorithmType binding)
- Not Tested: 6 (Test 6, 7, 8, 13, 14, 15 — Phase 16 carry-over)
- Phase 15 핵심 (measurePhi 4-way 매핑 + EdgeSelection 데이터 모델): PASS

## Carry-over to Phase 16

### Gap-1: Circle 알고리즘 패턴 재설계 (Test 5 FAIL 근본 원인)
- 현재 (Phase 14-04 / 15-03): ROI 원을 Rectangle 로 변환 후 원 센터 기준 제자리 회전
- 의도: 원 ROI 그리기 → 왼쪽 반지름 끝점으로 strip 이동 → 원호 포함 작은 사각형 strip → 원 센터 기준 1°/10° (사용자 설정) 회전 → 안→밖 or 밖→안 에지 → 360° 누적 → fit_circle_contour_xld
- 영향 파일: VisionAlgorithmService.TryFindCircleByPolarSampling
- Phase 14-04 의 `rectPhi = thetaRad` 결정 재검토 필요 (Phase 16 spec 단계)

### Gap-2: Datum AlgorithmType PropertyGrid binding refresh 누락 (Test 10~12 FAIL)
- 첫 레시피 로드 시: Datum 1/2/3 선택 시 알고리즘 타입 정상 전환
- ROI 이동/생성 후: Datum 변경해도 AlgorithmType combobox 갱신 안 됨
- 영향 파일: InspectionListView (Datum 선택 핸들러), DatumConfig (RaisePropertyChanged), MainView (RoiMoveCompleted 후 RefreshParamEditor)
- 가설: Phase 12-03/13-04 의 `RaisePropertyChanged("")` + `RefreshParamEditor()` 패턴이 AlgorithmType binding 까지 적용 안 됨

## Sign-off
- 사용자: heobum0928 (partial)
- 일자: 2026-04-29
- 비고: Phase 15 의 핵심 measurePhi 4-way 매핑 + EdgeSelection 데이터 모델 PASS. Circle 알고리즘 패턴 결함 + Datum AlgorithmType binding 결함 2건 Phase 16 carry-over.
