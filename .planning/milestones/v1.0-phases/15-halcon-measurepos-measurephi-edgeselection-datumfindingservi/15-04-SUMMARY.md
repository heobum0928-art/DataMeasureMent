---
phase: 15
plan: 04
status: partial
type: execute
created: 2026-04-29
---

# Plan 15-04 Summary — UAT (Partial Sign-off + 2 Gap Carry-over to Phase 16)

## Outcome

Phase 15 의 핵심 목표 (HALCON MeasurePos 정합성 — measurePhi 4-way 명시 매핑 + EdgeSelection 데이터 모델) 는 **달성**. 사용자 UAT 에서 12 시나리오 중 5 PASS, 4 FAIL, 6 not_tested.

| Test | 결과 | 비고 |
|------|------|------|
| 1~4 | PASS | TwoLineIntersect 4-way (LtoR/RtoL/TtoB/BtoT) — Phase 15 핵심 회귀 해결 |
| 5 | FAIL | Circle 알고리즘 패턴 자체 결함 — Phase 16 carry-over (Gap-1) |
| 6 | not_tested | Test 5 선행 결함 |
| 7~8 | not_tested | 사용자 보고 누락 — measurePhi 동일 메커니즘이라 PASS 추정 |
| 9 | PASS | EdgeSelection First/Last/All 의도대로 |
| 10~12 | FAIL | Datum AlgorithmType binding refresh 누락 — Phase 16 carry-over (Gap-2) |
| 13~14 | not_tested | Test 5/12 선행 결함 |
| 15 | not_tested | Phase 16 종료 후 통합 회귀로 보충 |

## What Phase 15 Delivered

- Plan 15-01: DatumConfig 6 ROI `_EdgeSelection` 프로퍼티 + EnsurePerRoiDefaults fallback (commits 02a9b5e, d4c13df, 3215cd0, abaa2cf)
- Plan 15-02: AppendEdgePointsFromStrip 4-way measurePhi 명시 매핑 + 9-site selection wiring (fe9925a, 05033ea, 5fac0c8, ecc4837)
- Plan 15-03: TryFindCircleByPolarSampling selection 인자화 + Phase 14-04 rectPhi=thetaRad 보존 (dbde085, b8e3a60, c800894)
- Plan 15-04: 15-UAT.md 골격 + partial sign-off (a42ee94)
- 빌드 PASS, 신규 warning 0
- BtoT/TtoB 0 edges 결함 직접 원인 제거 — 사용자 실측 확인 (Test 4 PASS)

## Carry-over to Phase 16

### Gap-1: Circle 알고리즘 strip 패턴 재설계
- **현재**: ROI 원을 Rectangle 로 변환 후 원 센터 기준 제자리 회전
- **의도**: 원 ROI → 왼쪽 반지름 끝점으로 strip 이동 → 원호 포함 작은 사각형 → 원 센터 기준 1°/10° (사용자 설정) 회전 → 안→밖 or 밖→안 에지 검출 → 360° 누적 → fit_circle_contour_xld
- **영향 파일**: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (TryFindCircleByPolarSampling)
- **Phase 14-04 D-13 재검토 필요**: `rectPhi = thetaRad` 회전 의도가 새 strip 패턴과 정합한지 spec 단계에서 결정

### Gap-2: Datum AlgorithmType PropertyGrid binding refresh 누락
- **증상**: 레시피 첫 로드 시엔 Datum 1/2/3 선택 시 AlgorithmType (TwoLineIntersect/CTH/VTH) 정상 전환. ROI 이동/생성 후엔 Datum 변경해도 AlgorithmType combobox 갱신 안 됨.
- **가설**: Phase 12-03/13-04 의 `RaisePropertyChanged("")` + `RefreshParamEditor()` 패턴이 AlgorithmType binding 까지 안 닿음 (또는 Datum 선택 핸들러가 ROI 편집 모드 진입/종료 후 AlgorithmType 재바인딩 안 함).
- **영향 파일**: WPF_Example/Custom/UI/InspectionListView.xaml.cs, WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs, WPF_Example/Custom/UI/MainView.xaml.cs

## Next Steps

1. `/gsd-add-phase` → Phase 16 신설 (datum-circle-redesign + algorithm-type-binding-fix)
2. `/gsd-spec-phase 16` 또는 `/gsd-discuss-phase 16` → Gap-1/Gap-2 spec 정의
3. `/gsd-plan-phase 16` → plan 작성
4. `/gsd-execute-phase 16` → 실행
5. Phase 16 완료 후 Test 6, 7, 8, 13, 14, 15 통합 회귀 검증
