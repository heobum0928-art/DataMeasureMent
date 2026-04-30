---
phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix
plan: 01
type: summary
wave: 1
status: complete
requirements: [R1, R2]
commits:
  - fa11033  # feat(16-01): RenderCircleStripOverlay + Circle 분기 pre-teach 시각화 (D-01/D-02/D-08)
  - d4897de  # feat(16-01): post-teach Circle viz redesign — light green + center cross 12px (D-04..D-08)
key-files:
  modified:
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
  diff_zero_verified:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
---

# Plan 16-01 Summary — Datum Circle 시각화 재작성

## 변경 라인 수
- HalconDisplayService.cs: +97 / -17 (Task 1 +75/-3, Task 2 +22/-14)
- 알고리즘 / 데이터 모델: 0/0/0 (D-22 보존)

## Task 1 — RenderCircleStripOverlay helper + Circle 분기 pre-teach 시각화 (R1)

**커밋**: fa11033

신규 private static helper `RenderCircleStripOverlay` (HalconDisplayService.cs 약 line 432-498) 추가:
- `VisionAlgorithmService.TryFindCircleByPolarSampling` (line 282-285) 의 strip 생성 식을 그대로 미러링
- canonical: `rectRow = ROI_Row - Radius * Sin(thetaRad)` / `rectCol = ROI_Col + Radius * Cos(thetaRad)` / `rectPhi = thetaRad`
- length1 = Radius * RectL1Ratio (반경 방향), length2 = Radius * RectL2Ratio (접선 방향)
- 렌더: 회색 thin line, fill 없음 (4 corner 좌표 직접 계산 → DispLine 4 회). stepDeg 1°~30° 가드.

Circle ROI 분기 (line 491-507) 끝에 `RenderCircleStripOverlay(window, datum)` 호출 추가 — pre-teach 시각화 z-order: ROI 경계 위.

`RenderRawEdgePoints` 시그니처에 `double size = 6.0` default 인자 추가 (Task 2 의 Circle 호출처 4.0 override 위함, 기존 5 ROI 호출은 인자 미지정 → 6.0 기본 → 회귀 0).

### Plan-text deviation (Rule 1)
Plan `<interfaces>` line 96-103 의 sin/cos 부호는 `+sin/-cos` 로 적혀 있으나, 실제 알고리즘 (`VisionAlgorithmService.cs` line 282-285) 은 `-sin/+cos` (화면 CCW 좌표계). Plan 의 stated intent 는 "이 strip 식이 알고리즘 canonical. Plan 16-01 의 시각화는 이 식을 그대로 미러링" + D-22 (알고리즘 diff 0 보존) — 따라서 본 구현은 plan-text 의 부호 표기가 아닌 실제 알고리즘 코드를 따라간다. 코드 주석에 명시.

## Task 2 — post-teach Circle viz redesign (R2)

**커밋**: d4897de

LastTeachSucceeded 분기의 CircleTwoHorizontal 검출 원 블록 재작성:
- 검출 원 색상: `yellow` → `light green` (D-05, 검출 성공 = 녹색 컨벤션)
- Center cross: `red` → `yellow`, half size 6px → 12px, line width 2 → 3 (D-06, 정밀 원 center 가 최종 목적)
- Circle raw edges 호출: `yellow` size=6 → `gray` size=4 (D-07, 검출 trace 용 — 다른 5 ROI 의 yellow/cyan/magenta/green/lime green/orange 와 시각 구분)
- z-order 정렬: Raw edges → 검출 원 → Center cross (top) — center 가 가려지지 않게 (D-08)

다른 5 ROI raw 색상 (Line1 cyan / Line2 magenta / Horizontal A green / Horizontal B lime green / Vertical orange) 회귀 0 — grep 검증 통과.

## 보존 (변경 0)
- VisionAlgorithmService.cs (Phase 14-04 D-13 `rectPhi=thetaRad`, Phase 15-03 EdgeSelection 분기) — git diff 0 라인
- DatumFindingService.cs — git diff 0 라인
- DatumConfig.cs (Plan 16-01 범위) — git diff 0 라인
- 기존 Phase 13-05 5 ROI raw 색상 — 회귀 0
- TwoLineIntersect / VerticalTwoHorizontal 시각화 경로 — 무영향 (CircleTwoHorizontal 분기 가드)

## 빌드 검증
- `msbuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`: PASS
- HalconDisplayService.cs 신규 warning: 0
- 기존 warning (VirtualCamera.cs:266 unreachable, VisionAlgorithmService.cs:64 unused) 본 phase 범위 외

## Plan 16-02 / 16-03 인계 메모
- 16-02 는 다른 파일 (InspectionListView.xaml.cs / MainView.xaml.cs) 이므로 Wave 1 병렬 — 이번 Wave 에서 함께 완료
- 16-03 UAT (Wave 2) 에서 사용자 SIMUL_MODE 또는 실 카메라 데이터로 시각 검증 필요:
  - 원 ROI 그린 직후 strip 사각형 stepCount 개 회색 표시
  - RectL1Ratio 0.05 → 0.20 변경 시 strip 크기 즉시 반영
  - btn_teachDatum 후 light green 검출 원 + 노란색 size=12 center cross + 회색 raw edges
  - z-order: center cross 가 검출 원 위에 표시
