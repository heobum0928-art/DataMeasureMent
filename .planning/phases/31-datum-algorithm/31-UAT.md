---
phase: 31-datum-algorithm
status: pending
created: 2026-05-19
---

# 31-UAT: Datum 기준 측정 알고리즘 확장 UAT

**Phase 31** 신규 측정 타입 6종 + carry-over 2건 + 빌드 검증 시나리오.

---

## Test 1: E8 — CircleCenterDistance (원중심 → Datum B Y 거리)

**목적:** CircleCenterDistanceMeasurement 가 원 ROI 에서 원 중심을 피팅하고 Datum B 기준 Y 방향 거리(mm)를 정상 산출한다.

**시나리오:**
1. SIMUL_MODE 에서 Bottom Fixture 이미지 로드
2. FAI 에 `Type=CircleCenterDistance` 측정 1개 추가, Circle ROI 를 원 위에 티칭
3. DatumRef = "Datum B" 설정, MeasureAxis = "Y"
4. Shot 실행 → FAI 결과 확인

**기대 결과:** 공차 예 20.201 ±0.030 mm, 측정값 표시 + 판정(PASS/FAIL) 정상

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 숫자(mm) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |
| Overlay | 원 + 거리선 표시 | pending |

---

## Test 2: D1/H5 — EdgeToLineAngle (Datum A 기준 직선 각도)

**목적:** EdgeToLineAngleMeasurement 가 Point ROI 에서 직선을 피팅하고 Datum A 기준선과의 각도(degree)를 정상 산출한다.

**시나리오:**
1. SIMUL_MODE 에서 Side Fixture 이미지 로드
2. FAI 에 `Type=EdgeToLineAngle` 측정 1개 추가, Point ROI 를 직선 에지 위에 티칭
3. DatumRef = "Datum A" 설정
4. Shot 실행 → FAI 결과 확인

**기대 결과:** 각도(degree) 측정값 표시 + 판정 정상

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 각도(deg) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |

---

## Test 3: I9/I10 — ArcLineIntersectDistance (호∩라인 교점 → Datum C X 거리)

**목적:** ArcLineIntersectDistanceMeasurement 가 3점 arc 피팅 + 라인 피팅 후 교점을 구하고 Datum C 기준 X 방향 거리(mm)를 정상 산출한다.

**시나리오:**
1. SIMUL_MODE 에서 적합한 Fixture 이미지 로드
2. FAI 에 `Type=ArcLineIntersectDistance` 측정 1개 추가
3. Arc P1/P2/P3 ROI 3개 + Line ROI 1개 티칭
4. DatumRef = "Datum C", MeasureAxis = "X" 설정
5. Shot 실행 → FAI 결과 확인

**기대 결과:** 교점 기준 X 거리(mm) 표시 + 판정 정상

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 거리(mm) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |

---

## Test 4: E2 — CompoundAngle (Datum B 기준 각도)

**목적:** CompoundAngleMeasurement 가 다단계 기하 체인(CL1~CL3 + La+Lb → midline → 교점 → Pc → Ld)을 수행하고 Datum B 기준 각도(degree)를 정상 산출한다.

**시나리오:**
1. SIMUL_MODE 에서 적합한 Fixture 이미지 로드
2. FAI 에 `Type=CompoundAngle` 측정 1개 추가
3. CL1/CL2/CL3 원 ROI 3개 + La/Lb 라인 ROI 2개 티칭
4. DatumRef = "Datum B" 설정
5. Shot 실행 → FAI 결과 확인

**기대 결과:** 41.36° ±1.00°, 측정값(deg) 표시 + 판정 정상

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 각도(deg) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |

---

## Test 5: E9/E10 — CompoundCenterCDistance / CompoundCenterBDistance (거리 mm)

**목적:** CompoundCenterCDistanceMeasurement(E9) 와 CompoundCenterBDistanceMeasurement(E10) 가 동일 기하 체인으로 Datum C/B 기준 거리(mm)를 각각 정상 산출한다.

**시나리오:**
1. SIMUL_MODE 에서 적합한 Fixture 이미지 로드
2. E9: `Type=CompoundCenterCDistance`, DatumRef="Datum C", MeasureAxis="X"
3. E10: `Type=CompoundCenterBDistance`, DatumRef="Datum B", MeasureAxis="Y"
4. 각 타입에 CL2/CL3 원 ROI 2개 + La/Lb 라인 ROI 2개 티칭
5. Shot 실행 → 각 FAI 결과 확인

**기대 결과:** 거리(mm) 측정값 표시 + 판정 정상, E9/E10 Datum 방향 구분 정확

| 항목 | 기대 | 결과 |
|------|------|------|
| E9 측정값 표시 | 거리(mm) Datum C 기준 | pending |
| E10 측정값 표시 | 거리(mm) Datum B 기준 | pending |
| 판정 | PASS/FAIL 정상 | pending |

---

## Test 6: ArcEdgeDistance — G 시리즈 (Datum C X 거리)

**목적:** ArcEdgeDistanceMeasurement 가 G 시리즈(G1·G2·G5~G8·G11·G12) 에지에서 EdgeToLineDistance 와 동일 알고리즘으로 Datum C 기준 X 거리(mm)를 정상 산출한다.

**시나리오:**
1. SIMUL_MODE 에서 G 시리즈 에지가 포함된 Fixture 이미지 로드
2. FAI 에 `Type=ArcEdgeDistance` 측정 1개 추가, Point ROI 를 에지 위에 티칭
3. DatumRef = "Datum C", MeasureAxis = "X" 설정 (기본값)
4. Shot 실행 → FAI 결과 확인

**기대 결과:** X 방향 거리(mm) 표시 + 판정 정상

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 거리(mm) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |

---

## Test 7: CO-23.1-02 — 신규 타입 선택 시 Rect ROI 버튼 활성화

**목적:** 측정 타입별 Rect ROI 버튼 일반화 — 신규 타입(EdgeToLineAngle, ArcEdgeDistance 등) 선택 시도 MainView Rect ROI 버튼이 활성화된다.

**시나리오:**
1. InspectionList 에서 측정 타입이 EdgeToLineAngle 인 Measurement 노드 선택
2. MainView 의 Rect ROI 버튼 활성화 여부 확인
3. ArcEdgeDistance, CompoundAngle 등 다른 신규 타입으로 동일 테스트

**기대 결과:** 신규 타입 측정 노드 선택 시 Rect ROI 버튼 활성화됨

| 항목 | 기대 | 결과 |
|------|------|------|
| EdgeToLineAngle Rect ROI 버튼 | 활성화 | pending |
| ArcEdgeDistance Rect ROI 버튼 | 활성화 | pending |

---

## Test 8: CO-23.1-01 — 듀얼 이미지 표시

**목적:** Datum 티칭 모드에서 TeachingImagePath(티칭 기준 이미지)와 InspectionImagePath(검사 이미지)가 구분 표시된다.

**시나리오:**
1. DatumConfig 에 TeachingImagePath 경로 설정 (검사 이미지와 다른 파일)
2. Datum 티칭 모드 진입
3. 티칭 이미지와 검사 이미지가 UI 에서 구분 표시되는지 확인

**기대 결과:** 두 이미지 경로가 UI 에서 각각 식별 가능

| 항목 | 기대 | 결과 |
|------|------|------|
| TeachingImagePath 표시 | 별도 뷰어/레이블 정상 | pending |
| InspectionImagePath 표시 | 기존 뷰어 유지 | pending |

---

## Test 9: BUILD — MSBuild Debug/x64 PASS

**목적:** Phase 31 신규 파일 + 수정 파일이 모두 포함된 상태에서 MSBuild Debug/x64 Rebuild 가 exit 0, 신규 error/warning 0 (Phase 21 baseline 6 warning 유지).

**시나리오:**
```
MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild
```

**기대 결과:** Build succeeded, 0 Error(s), warning 수 ≤ baseline(6)

| 항목 | 기대 | 결과 |
|------|------|------|
| Build exit code | 0 | pending |
| 신규 Error | 0 | pending |
| Warning 수 | ≤ 6 (baseline) | pending |
