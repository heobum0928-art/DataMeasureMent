---
phase: 31-datum-algorithm
status: pending
created: 2026-05-19
---

# 31-UAT: Datum 기준 측정 알고리즘 확장 UAT

**Phase 31** 신규 측정 타입 7종(CircleCenterDistance/EdgeToLineAngle/ArcLineIntersectDistance/CompoundAngle/CompoundCenterCDistance/CompoundCenterBDistance/ArcEdgeDistance) + carry-over 2건 (CO-23.1-01/02) + 빌드 검증.

**SIMUL_MODE 실행 전 공통 준비 사항:**
- DatumMeasurement.exe 를 Debug/x64 SIMUL_MODE 로 실행 (SIMUL_MODE 심볼 자동 활성화)
- InspectionList 에서 Fixture 로드 → 해당 Datum 티칭(TryTeachDatum) → `DetectedOrigin` 좌표 획득 확인
- FAI 에 신규 Measurement 노드 추가 → PropertyGrid Type 드롭다운에서 신규 타입 선택
  - MeasurementFactory.GetTypeNames() 이 반환하는 14개 타입 모두 노출 확인 필요:
    `EdgeToLineDistance, PointToLineDistance, LineToLineAngle, LineToLineDistance, PointToPointDistance, CircleDiameter, EdgePairDistance, CircleCenterDistance, EdgeToLineAngle, ArcEdgeDistance, ArcLineIntersectDistance, CompoundAngle, CompoundCenterCDistance, CompoundCenterBDistance`

---

## Test 1: E8 — CircleCenterDistance (원중심 → Datum B Y 거리)

**목적:** CircleCenterDistanceMeasurement 가 Circle ROI 에서 원 중심을 피팅하고 Datum B 기준 Y 방향 거리(mm)를 정상 산출한다.

**실행 절차:**
1. SIMUL_MODE 에서 **Bottom Fixture #2** 이미지 로드 (E8이 있는 픽스처)
2. Datum 티칭 실행 → DetectedOrigin 확보 (Datum B/C 교점)
3. InspectionList 에서 FAI 노드 선택 → 측정 추가 → Type 드롭다운에서 **`CircleCenterDistance`** 선택 (7번째 이후 신규 타입 목록 노출 확인)
4. PropertyGrid 에서 `DatumRef = "Datum B"`, `MeasureAxis = "Y"` 설정
5. Circle ROI 버튼 클릭 → SIMUL 이미지에서 원(E8 해당 원) 위에 Circle ROI 드래그 티칭
6. Shot 측정 실행 → FAI 결과 확인

**기대 결과:**
- SOP 공차 예시: **20.201 ±0.030 mm**
- 측정값이 합리적 범위(예: 18~23 mm)의 숫자로 표시
- 판정(OK/NG) strip 색상 표시 — 공차 범위 내 = 녹색, 초과 = 적색

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 숫자(mm) 정상 | **PASS** |
| 판정 | PASS/FAIL 정상 | **PASS** |
| Overlay | 원 피팅 + 거리선 표시(FAI-DistLine) | **PASS** |
| PropertyGrid CircleCenterDistance 타입 노출 | 드롭다운에 표시 | **PASS** |
| Datum 기준선/교점 표시 | 가로·세로 기준선 + 교점 마커 | **PASS** (재검증) |

**UAT 이력:** 초기 검증에서 결함 3건 발견 → hotfix.
- 결함 A(결과 overlay 빈 리스트) / B(datum 기준선·교점 미표시) / C(RectL1·L2Ratio 12px cap 무반응) — commit 0071d50 수정.
- 재검증 시 datum 기준선이 ±200px 로 짧음 → 가로/세로 기준선 이미지 풀스팬으로 보강 — commit 3d8b4fc.
- 재검증 시 X축 수직 기준선이 실제 datum 수직선과 어긋남(Line1+90° 재구성 결함) → datum 2차(수직) 기준선 각도 검출·저장·사용으로 전환 — commit b526167.
- 측정값/판정/overlay/타입 노출 + X축 수직선 일치 PASS 확인 (사용자 2026-05-19).

---

## Test 2: D1/H5 — EdgeToLineAngle (Datum A 기준 직선 각도)

**목적:** EdgeToLineAngleMeasurement 가 Point ROI 에서 에지 직선을 피팅하고 Datum A 기준선과의 각도(degree)를 정상 산출한다.

**실행 절차:**
1. SIMUL_MODE 에서 **Side Fixture #3** 이미지 로드 (D1/H5 벽면 각도 측정용)
2. Datum 티칭 실행 → DetectedOrigin 확보 (Datum A 기준선 각도 정보 포함)
3. FAI 에 신규 Measurement 추가 → Type 드롭다운에서 **`EdgeToLineAngle`** 선택
4. PropertyGrid 에서 `DatumRef = "Datum A"` 설정
5. Rect(Point) ROI 버튼 클릭 → SIMUL 이미지에서 직선 에지(벽면 라인) 위에 ROI 배치 티칭
   - ROI 방향(Phi)을 에지 방향에 맞게 조정
   - EdgeSampleCount=20, EdgeTrimCount=10, EdgePolarity/Direction 기본값 유지
6. Shot 측정 실행 → FAI 결과 확인

**기대 결과:**
- SOP 공차 예시: **D1 단변1 90.000° +0.5°/-1.5°**, **H5 장변3 90.0° ±1.5°**
- 측정값이 합리적 범위(예: 88~92°)의 각도로 표시
- 판정(OK/NG) strip 색상 표시

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 각도(deg) 정상 | **PASS** |
| 판정 | PASS/FAIL 정상 | **PASS** |
| PropertyGrid EdgeToLineAngle 타입 노출 | 드롭다운에 표시 | **PASS** |
| Overlay 가시화 (사용자 추가 요청) | FAI-Edge1 라인 + 라인 분리 X 마커 + raw 에지점 + datum 라인 | **PASS** |

**UAT 이력:** 측정값/판정/타입 노출 PASS 후 사용자 시각 검증 피드백 4건 → hotfix.
- hotfix#5 (TryFitLine collectedEdges + FAI-EdgeRaw 노랑 작은 + 마커) — strip-loop 누적 raw 에지점 가시화.
- hotfix#6 (FAI-Edge* X 마커 색상 라인과 분리, 초기 white) — 검출 점 위치 시각 분리.
- hotfix#7 (라인 범위 축소: 상단부→datum 교점, IntersectionLl + 평행 폴백) — V자 형태로 각도 직관 강화.
- hotfix#8 (X 마커 색상 white → magenta) — 사용자 색상 변경 요청.
- 전체 PASS 확인 (사용자 2026-05-20, 단일 커밋 f0842e4).

---

## Test 3: I9/I10 — ArcLineIntersectDistance (호∩라인 교점 → Datum C X 거리)

**목적:** ArcLineIntersectDistanceMeasurement 가 3점 arc 피팅 + 라인 피팅 후 교점을 구하고 Datum C 기준 X 방향 거리(mm)를 정상 산출한다.

**실행 절차:**
1. SIMUL_MODE 에서 **Top Fixture #2** 이미지 로드 (I9/I10 호-라인 교점 측정용)
2. Datum 티칭 실행 → DetectedOrigin 확보
3. FAI 에 신규 Measurement 추가 → Type 드롭다운에서 **`ArcLineIntersectDistance`** 선택
4. PropertyGrid 에서 `DatumRef = "Datum C"`, `MeasureAxis = "X"` 설정 (기본값)
5. ROI 티칭 (4개):
   - **Arc P1 ROI**: Rect(Point) ROI → 호 위 첫 번째 점 위에 배치
   - **Arc P2 ROI**: Rect(Point) ROI → 호 위 두 번째 점 위에 배치
   - **Arc P3 ROI**: Rect(Point) ROI → 호 위 세 번째 점 위에 배치
   - **Line ROI**: Rect(Point) ROI → 교차할 직선 에지 위에 배치
   - ※ PropertyGrid 에 Arc_P1_*, Arc_P2_*, Arc_P3_*, Line_* 필드가 각각 표시됨
6. Shot 측정 실행 → FAI 결과 확인

**기대 결과:**
- SOP 공차 예시: **I9 5.053 +0.050/-0.000 mm**
- 교점 기준 X 거리(mm) 표시 + 판정 정상
- 호 피팅 실패 시 측정값 '—' + 오류 처리(크래시 없음)

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 거리(mm) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |
| 호 피팅 실패 안전 종결 | 크래시 없이 '—' 표시 | pending |

---

## Test 4: E2 — CompoundAngle (Datum B 기준 복합 각도)

**목적:** CompoundAngleMeasurement 가 다단계 기하 체인(CL1~CL3 원피팅 + La/Lb 라인피팅 → midline → 교점 Pa/Pb → 중점 Pc + CL1 중심 Pd → 직선 Ld → Datum B 기준 각도)을 수행하고 각도(degree)를 정상 산출한다.

**실행 절차:**
1. SIMUL_MODE 에서 **Bottom Fixture #2** 이미지 로드 (E2 복합 각도 측정용)
2. Datum 티칭 실행 → DetectedOrigin 확보
3. FAI 에 신규 Measurement 추가 → Type 드롭다운에서 **`CompoundAngle`** 선택
4. PropertyGrid 에서 `DatumRef = "Datum B"` 설정
5. ROI 티칭 (5개):
   - **CL1 ROI**: Circle ROI → 원 CL1(Pd 기준점 원) 위에 배치
   - **CL2 ROI**: Circle ROI → 원 CL2 위에 배치
   - **CL3 ROI**: Circle ROI → 원 CL3 위에 배치
   - **La ROI**: Rect(Point) ROI → 직선 La 에지 위에 배치
   - **Lb ROI**: Rect(Point) ROI → 직선 Lb 에지 위에 배치
   - ※ PropertyGrid 에 CL1_*/CL2_*/CL3_*/La_*/Lb_* 각 필드 그룹 표시됨
6. Shot 측정 실행 → FAI 결과 확인

**기대 결과:**
- SOP 공차 예시: **41.36° ±1.00°**
- 각도(deg) 표시 + 판정 정상
- 기하 체인 각 단계 실패 시 크래시 없이 '—' 처리

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 각도(deg) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |
| 체인 실패 안전 종결 | 크래시 없이 '—' 표시 | pending |

---

## Test 5: E9/E10 — CompoundCenterCDistance / CompoundCenterBDistance (복합 거리 mm)

**목적:** CompoundCenterCDistanceMeasurement(E9) 와 CompoundCenterBDistanceMeasurement(E10) 가 동일 기하 체인(CL2/CL3 + La/Lb → Pc 중점)으로 Datum C/B 기준 거리(mm)를 각각 정상 산출한다.

**실행 절차 (E9):**
1. SIMUL_MODE 에서 Bottom Fixture #2 이미지 로드
2. FAI 에 신규 Measurement 추가 → Type 드롭다운에서 **`CompoundCenterCDistance`** 선택
3. PropertyGrid: `DatumRef = "Datum C"`, `MeasureAxis = "X"` (기본값)
4. ROI 티칭 (4개): CL2/CL3 Circle ROI + La/Lb Rect(Point) ROI 배치
5. Shot 실행 → 결과 확인

**실행 절차 (E10):**
1. 위와 동일 Fixture
2. Type 드롭다운에서 **`CompoundCenterBDistance`** 선택
3. PropertyGrid: `DatumRef = "Datum B"`, `MeasureAxis = "Y"` (기본값)
4. CL2/CL3 + La/Lb ROI 티칭 (E9와 동일 ROI 재활용 가능)
5. Shot 실행 → 결과 확인

**기대 결과:**
- E9: Datum C 기준 X 방향 거리(mm) 표시 + 판정 정상
- E10: Datum B 기준 Y 방향 거리(mm) 표시 + 판정 정상
- Datum 방향이 서로 다르게(X vs Y) 산출됨 — 두 값이 동일하면 이상

| 항목 | 기대 | 결과 |
|------|------|------|
| E9 측정값 표시 | 거리(mm) Datum C X 기준 | pending |
| E10 측정값 표시 | 거리(mm) Datum B Y 기준 | pending |
| E9/E10 Datum 방향 구분 | 두 값이 다름 | pending |
| 판정 | PASS/FAIL 정상 | pending |

---

## Test 6: ArcEdgeDistance — G 시리즈 (Datum C X 거리)

**목적:** ArcEdgeDistanceMeasurement 가 G 시리즈(G1·G2·G5~G8·G11·G12) 호 에지에서 TryFitLine("All") + ComputeProjectionDistance 로 Datum C 기준 X 거리(mm)를 정상 산출한다.

**실행 절차:**
1. SIMUL_MODE 에서 **Bottom Fixture #2** 이미지 로드 (G 시리즈 호 에지 포함)
2. Datum 티칭 실행 → DetectedOrigin 확보
3. FAI 에 신규 Measurement 추가 → Type 드롭다운에서 **`ArcEdgeDistance`** 선택
4. PropertyGrid 에서 `DatumRef = "Datum C"`, `MeasureAxis = "X"` 설정 (기본값)
5. Rect(Point) ROI 버튼 클릭 → G 시리즈 호 에지(예: G2 해당 에지) 위에 ROI 배치 티칭
   - EdgeDirection/Polarity/SampleCount 기본값 유지
6. Shot 측정 실행 → FAI 결과 확인

**기대 결과:**
- SOP 공차 예시: **G2 22.162 ±0.030 mm**, **G8 11.132 ±0.020 mm**, **G11 24.362 ±0.020 mm**
- X 방향 거리(mm) 표시 + 판정 정상
- Overlay: FAI-Edge1(에지 라인) + FAI-DistLine(교점~에지 거리선) 표시

| 항목 | 기대 | 결과 |
|------|------|------|
| 측정값 표시 | 거리(mm) 정상 | pending |
| 판정 | PASS/FAIL 정상 | pending |
| Overlay FAI-Edge1 | 에지 라인 표시 | pending |
| Overlay FAI-DistLine | 거리선 표시 | pending |

---

## Test 7: CO-23.1-02 — 신규 타입 선택 시 Rect/Circle ROI 버튼 활성화

**목적:** 측정 타입별 ROI 버튼 일반화 — 신규 타입 선택 시 MainView 의 Rect ROI 버튼(또는 Circle ROI 버튼)이 활성화된다.

**실행 절차:**
1. InspectionList 에서 **Type = EdgeToLineAngle** 인 Measurement 노드 선택
2. MainView 상단 Rect ROI 버튼 활성화 여부 확인
3. **Type = ArcEdgeDistance** 측정 노드 선택 → Rect ROI 버튼 활성화 확인
4. **Type = CompoundAngle** 측정 노드 선택 → Rect ROI 버튼 활성화 확인
5. **Type = CircleCenterDistance** 측정 노드 선택 → Circle ROI 버튼 활성화 확인
6. **Type = EdgeToLineDistance** (기존 타입) 선택 → Rect ROI 버튼 활성화 확인 (회귀 없음)

**기대 결과:**
- 신규 Point_* ROI 보유 타입(EdgeToLineAngle/ArcEdgeDistance/ArcLineIntersectDistance/CompoundAngle/CompoundCenterCDistance/CompoundCenterBDistance) 선택 시 Rect ROI 버튼 활성화
- CircleCenterDistance 선택 시 Circle ROI 버튼 활성화
- 기존 타입(EdgeToLineDistance) 회귀 없음

| 항목 | 기대 | 결과 |
|------|------|------|
| EdgeToLineAngle → Rect ROI 버튼 | 활성화 | pending |
| ArcEdgeDistance → Rect ROI 버튼 | 활성화 | pending |
| CompoundAngle → Rect ROI 버튼 | 활성화 | pending |
| CircleCenterDistance → Circle ROI 버튼 | 활성화 | pending |
| EdgeToLineDistance → Rect ROI 버튼 (회귀) | 활성화 | pending |

---

## Test 8: CO-23.1-01 — 이미지 출처 레이블 표시 (듀얼 이미지 구분)

**목적:** Datum 티칭 모드에서 TeachingImagePath(티칭 기준 이미지)와 InspectionImagePath(검사/SIMUL 이미지)가 UI 에서 구분 표시된다.

**실행 절차:**
1. DatumConfig PropertyGrid 에서 `TeachingImagePath` 필드에 특정 이미지 파일 경로 입력 (검사 이미지와 다른 파일)
2. InspectionList 에서 Datum 노드 선택 → **Datum Load (Load 버튼)** 클릭
3. MainView 하단 이미지 출처 레이블(`txt_imageSourceLabel`) 표시 내용 확인:
   - TeachingImagePath 경로(파일명)가 레이블에 표시되어야 함
4. 측정 실행(Shot) 후 MainView 레이블이 SimulImagePath(ShotConfig 검사 이미지) 경로로 교체되는지 확인
5. TeachingImagePath 가 빈 문자열인 경우 → 레이블이 Collapsed(숨김) 되는지 확인 (T-31-12 폴백)

**기대 결과:**
- Datum Load 시: 하단 레이블에 TeachingImagePath 파일명 표시
- Shot 실행 시: 하단 레이블에 SimulImagePath 파일명으로 변경
- 빈 경로: 레이블 숨김(Collapsed)

| 항목 | 기대 | 결과 |
|------|------|------|
| Datum Load 시 TeachingImagePath 레이블 | 경로/파일명 표시 | pending |
| Shot 실행 시 SimulImagePath 레이블 | 경로/파일명으로 교체 | pending |
| 빈 TeachingImagePath | 레이블 Collapsed | pending |

---

## Test 9: BUILD — MSBuild Debug/x64 PASS

**목적:** Phase 31 신규 파일 + 수정 파일이 모두 포함된 상태에서 MSBuild Debug/x64 Rebuild 가 exit 0, 신규 error/warning 0 (Phase 21 baseline 2 warning — MSB3884 + CS0162 — 유지).

**시나리오:**
```
MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild
```

**기대 결과:** Build succeeded, 0 Error(s), warning 수 ≤ 2 (baseline)

**자동 검증 결과 (Task 1 실행 중 확인):**
- 빌드 실행 결과: exit code 0 (Build succeeded)
- DatumMeasurement.exe 생성 확인
- 경고: MSB3884(MinimumRecommendedRules.ruleset) + CS0162(VirtualCamera.cs unreachable code) = 2건 = Phase 21 baseline 동일
- 신규 error 0, 신규 warning 0

| 항목 | 기대 | 결과 |
|------|------|------|
| Build exit code | 0 | **PASS** |
| 신규 Error | 0 | **PASS** |
| Warning 수 | ≤ 2 (baseline) | **PASS (2건 = baseline)** |
