---
phase: quick-260716-ali
plan: 01
subsystem: inspection-measurement (ArcLineIntersectDistance 측정 재정의)
tags: [halcon, measurement, datum, intersection, sign-convention]

# Dependency graph
requires:
  - VisionAlgorithmService.GetDatumAxisLine (datum 수직선 2점 산출)
  - VisionAlgorithmService.TryIntersectLines (두 직선 교점, 평행/NaN 가드 내장)
  - ArcLineIntersectDistanceMeasurement (4-ROI 교점1/교점2 산출부는 무변경)
provides:
  - ArcLineIntersectDistance 측정 재정의 — "좌우 교점 라인 ∩ datum 수직선" 교차점 기준 X거리
  - 오버레이 재구성 — 교점라인 + 교차점 마커 + 거리선
affects: [I9/I10 등 MeasureAxis=X ArcLineIntersect 측정 전부. Y축은 대칭 처리 or 기존유지 결정]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "측정점 = 점 하나가 아니라 '좌우 두 교점을 잇는 직선'과 'datum 기준선(DatumAngle2Rad, 휜 각도 반영)'의 교차점. 좌측 교점이 직선 기울기에 실제 기여(기존엔 Y평균에만 쓰여 무의미했음)."
    - "부호: 교차점이 datum 수직선 오른쪽(col 증가)=양수. 두 직선 교점 방식은 ComputeProjectionDistance(점→선 투영)의 X축 부호반전 문제를 우회 — 교차점의 datum-로컬 좌표를 직접 계산해 부호 명확화."

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs

key-decisions:
  - "사용자 확정 측정 정의: 좌측 교점(int1)·우측 교점(int2)을 잇는 직선 L_cross. datum 원점(DatumOriginRow/Col)+DatumAngle2Rad 로 정의되는 수직 기준선 L_datum. 측정점 P = L_cross ∩ L_datum. 거리 = datum 원점 기준 P 까지의 X(수평, datum-로컬) 성분, 오른쪽=+."
  - "왜 교점 방식인가: 기존은 measurePointCol=int2Col(우측 교점 col만), measurePointRow=(int1+int2)/2(Y평균) → 좌측 교점이 사실상 무의미. 사용자 의도는 두 교점을 지나는 실제 부품 엣지라인이 datum 을 어디서 가로지르는가. datum 이 휘어도 L_datum 각도(DatumAngle2Rad)가 자동 보정."
  - "부호 계산: P를 datum 로컬 좌표로 변환. datum 수직선 방향(sinθ2,cosθ2), 그에 수직인 '오른쪽' 법선 = (dirC, -dirR) 정규화(수직 datum θ2≈π/2 → 법선≈(0,-1)? 재유도 필요). 실제로는 (P - datumOrigin)·(오른쪽 단위벡터). 구현 시 수직 datum 케이스로 수치검증(datum col=Cx, P col=Cx+Δ(오른쪽) → +Δ 나와야). InvertSign/UseAbsoluteValue(MeasurementBase)는 그대로 뒤에 적용됨."
  - "Y축(MeasureAxis=Y) 처리: I9/I10 은 전부 X. Y 케이스는 대칭(좌우→상하 교점 잇는 선 ∩ datum 수평선)으로 동일 패턴 적용하되, 실측 대상이 없으므로 회귀 최소화 위해 X와 동일 구조로 미러링만."
  - "오버레이: 기존 FAI-AvgPoint(측정점 마커)를 교차점 P로, FAI-DistLine 을 datum원점(또는 foot)→P 로. 좌우 교점 잇는 라인(L_cross) 오버레이 신규 추가해 시각적으로 '두 교점 지나는 선'이 보이게. 뷰어=측정 동일 좌표."
  - "TryIntersectLines 평행 가드: L_cross 와 L_datum 이 평행(둘 다 수직에 가까움)이면 교점 없음 → false 반환+error, 측정 '—'. 실무상 좌우 교점 잇는 선은 대략 수평, datum 수직선은 수직이라 평행 위험 낮음."

requirements: [ALI-01, ALI-02]

# Metrics
started: 2026-07-16
---

# Quick Task 260716-ali: ArcLineIntersect 측정을 "교점라인 ∩ datum" 교차점 기준으로 재정의

## 배경 / 문제
I9(ArcLineIntersectDistance, MeasureAxis=X)에서:
1. **부호 오류**: 우측 교점이 datum(왼쪽) 오른쪽인데 측정값이 -5.091 (오른쪽=+ 규약 위반). EdgeToLineDistance X축 24개가 InvertSign=True 로 우회 중인 것과 동일 증상 — ComputeProjectionDistance X공식이 오른쪽을 음수로 냄.
2. **좌측 교점 무의미**: 기존 코드는 X거리에 우측 교점 col만 쓰고, 좌측 교점은 Row 평균 안정화에만 사용 → 좌측 교점을 굳이 4-ROI 로 찾는 의미가 없음.

## 사용자 확정 의도
좌·우 두 교점을 **잇는 직선**이 **datum 수직 기준선**(휜 각도 DatumAngle2Rad 반영)을 **가로지르는 교차점**을 측정점 P로 삼고, datum 원점에서 P까지 X거리(오른쪽=양수)를 잰다.

## 목표
- **ALI-01**: TryExecute 의 (7)측정점 보정 + (8)거리 계산을 "L_cross ∩ L_datum 교차점 P → datum X거리(오른쪽=+)"로 교체. IntersectionPointSelection(Far/Close)·measurePointRow/Col 평균 로직 제거(또는 Y좌표 산출을 교차점으로 대체).
- **ALI-02**: 오버레이를 교차점 기준으로 재구성 + 좌우 교점 잇는 라인 신규 표시. 측정=뷰어 동일 좌표.

## 설계
### 측정 (ALI-01)
```
1. int1(좌), int2(우) = 기존 4-ROI 교점 산출 (무변경)
2. L_datum 2점 = GetDatumAxisLine(DatumOriginRow, DatumOriginCol, DatumAngle2Rad, halfLen)
3. P = TryIntersectLines(int1..int2 (L_cross),  L_datum 2점)
   - 실패(평행/NaN) → false + error "교점라인-datum 평행" → 측정 '—'
4. 부호있는 X거리(px):
   - datum 오른쪽 단위벡터 계산 후 (P - datumOrigin)·rightUnit
   - 수직 datum(θ2≈π/2) 케이스로 수치검증: 오른쪽 점 → +
   resultValue = signedRightPx * pixelResolution
5. (MeasurementBase.EvaluateJudgement 이 InvertSign/UseAbs/보정계수 적용)
```
※ 부호를 직접 계산하므로, 이 측정타입은 향후 InvertSign 불필요(기존 I9/I10 은 InvertSign=False 라 회귀 0).

### 오버레이 (ALI-02)
- FAI-Edge1~4(4 라인) 유지
- FAI-Intersection1/2(교점 마커) 유지
- **FAI-CrossLine 신규**: int1 → int2 (좌우 교점 잇는 선)
- FAI-AvgPoint → **FAI-CrossPoint**: 교차점 P
- FAI-DistLine: datumOrigin(또는 datum선 위 foot) → P

## 검증
- 실HW 빌드(Debug|x64)/SIMUL(Debug|AnyCPU) 0 errors.
- 수치검증(코드 주석): 수직 datum(θ2=π/2), datumOrigin col=Cx, L_cross 가 col=Cx+30 에서 교차 → resultValue = +30*pixRes.
- HUMAN-UAT: I9 재검사 → +5.05 부근(현재 -5.091), 뷰어에서 교차점/거리선이 우측 교점 근방 datum선상에 표시.

## 리스크
- IntersectionPointSelection(Far/Close), measurePointRow/Col 평균 제거 → 이 필드 참조하던 다른 코드 없는지 확인(측정 클래스 로컬이라 안전 예상).
- Y축 케이스: 실측 대상 없음. X와 동일 구조 미러링, datum 1차 각도(DatumAngleRad)+상하 교점 잇는 선. 회귀 주의.
- 기존 레시피 I9/I10 InvertSign=False 라 이 변경으로 부호가 뒤집히지 않음(교점 방식이 직접 +를 냄).
