---
quick_id: 260625-lo5
slug: circletwohorizontal-datum-perpendicular
status: incomplete
date: 2026-06-25
commits:
  - a442b2b
  - 90071e5
---

# Quick 260625-lo5 — CTH datum 수직 기준선각 직교 수정

## 문제 (확정 진단)
`MeasureAxis=X` 인 EdgeToLineDistance 측정이 부품 틸트를 보정하지 못하고 순수 가로(열)거리로 계산됨.

근본 원인: `DatumFindingService.cs` CircleTwoHorizontal(CTH) 검출에서 수직 기준각
`vertPhi = (π/2) + dθ` (dθ = curAngle − RefAngleRad, 티칭 이후 변화량). 부품이 티칭과
같은 틸트로 놓이면 dθ≈0 → `vertPhi = π/2`(정확히 이미지-수직)로 고정됨.
→ 소비처 measureX(useAngle2)의 projection_pl 투영축이 안 기울어짐 → 수선의 발이 center와
같은 row에 떨어져 거리 = `|pCol − OriginCol|` 순수 가로거리.

실측 영향(레시피 FAI_1 / A7): 에지가 datum 교점에서 row 6556px 떨어져 있어
6556 × tan(0.47°) × 0.002245 mm/px ≈ **0.12mm 오차 (공차 ±0.05mm의 2.4배)** → 불량 오판.

## 수정 (A안: 검출부 근본 수정)
`WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- **Find** `TryFindCircleTwoHorizontal` (line 340~399): `vertPhi = curAngle + π/2`
  (검출 수평선에 항상 직교). 단일 변수가 IntersectionLl(교점)·`DetectedRefAngle2`(X측정
  투영축)·`Line1Detected_*`(표시 수직선)에 일관 반영. `dAngle`은 HomMat2dRotate용으로 유지.
- **Teach** `TryTeachCircleTwoHorizontal` (line 1143~1192): 동일 직교 규약(teachVertPhi =
  teachHorizAngle + π/2)으로 교점·Line1Detected·각도검증 통일. 미수정 시 teach pose에서도
  origin이 ~72px 어긋나 ROI 정렬 회귀(circle center가 수평선에서 ~9000px 떨어져 영향 큼).

소비처(EdgeToLineDistanceMeasurement)·표시부(HalconDisplayService)·타 datum 타입 무변경.

## 수정 2 — measureX 부호 정규화 (90071e5)
후속 발견: X축 부호가 **틸트 방향에 따라 뒤집히는** 버그.
`WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs:160~`
- 근본: measureX(useAngle2) 투영축은 datum 수직선(angleSrc=DatumAngle2Rad≈π/2). 여기에
  `cosθ≥0` 정규화를 쓰면 cosθ=−sin(curAngle)이라 부호가 틸트 방향으로만 결정 → 틸트 방향이
  바뀌면 sinT·cosT 동시 반전 → 법선 통째 반전 → 같은 점 X 부호 뒤집힘.
- 수정: 정규화 축별 분리 — 수직축(useAngle2)은 `sinθ≥0`(sinθ2≈±1 안정), 수평축(measureY/폴백)은
  기존 `cosθ≥0` 유지. 거리 크기 무영향, 부호만 안정화.
- 실데이터(FAI_1) 증상: C1/C2 (P1/P2) MeasureAxis=X, 점은 모두 datum 수직선 좌측인데
  LastMeasuredValue 전부 음수(−2.74/−2.36), nominal 양수(+2.75/+2.35) → 부호로 NG.

## 검증
- ✅ MSBuild Debug/x64 빌드 PASS (수정 1·2 모두, 기존 경고만, DatumMeasurement.exe 생성)
- ⏳ **SIMUL 실측 UAT 사용자 수행 필요** (아래)

## 사용자 UAT 체크리스트 (실데이터)
1. FAI_1 레시피 로드 → TOP CircleTwoHorizontal(Top_Datum) Datum 재티칭.
2. X축 항목(A7/A8 등 MeasureAxis=X)의 datum 수직선 표시가 부품 틸트만큼 **기울어졌는지** 확인.
3. X축 측정값이 SOP 도면/현미경 공칭과 일치하는지 확인 (기존 ~0.12mm 오차 해소).
4. Y축 항목·타 시퀀스(Side/Bottom) 측정값 회귀 0 확인.
5. **공칭값 재확인**: X축 측정값이 변했으므로 기존 NominalValue가 옛(미보정) 기준이면
   재티칭/공칭 재설정 필요.

## 재티칭 영향 (중요)
X축(MeasureAxis=X) 측정값이 틸트 보정 적용으로 변함(= 더 정확). 기존 공칭은 옛 미보정
측정 기준일 수 있으므로 적용 후 실측 재확인 권장. Y축·검출 자체·타 datum 타입은 회귀 0.
