# Rapid City MSOP Datum 분석 노트

**작성일:** 2026-04-10
**대상 문서:** `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB_변경내역_표기.pdf`
**분석 범위:** Datum 정의 페이지 (p.21~p.38) + 측정 페이지 일부 (p.39~p.48)
**용도:** Phase 6 Datum 시스템 설계의 근거 문서. 06-CONTEXT.md와 함께 참조.

---

## 1. 측정 시스템 전제 조건

### 하드웨어 (확정)
- **카메라**: 152MP (Vieworks VP-152MX 등, 14192×10640, 픽셀 3.76 μm)
- **렌즈**: 텔레센트릭 고정 1.7x 배율 (예정)
- **분해능**: 약 2.2 μm/pix
- **FOV**: 약 31 × 23 mm (제품 26×21 + 마진)
- **분할 촬영**: 안 함 (제품 전체가 1 FOV에 들어옴)
- **다중 캡처**: Z 변경 + 조명 변경으로 한 면당 여러 장 OK
- **PC 구성**: Top PC + Side PC 분리, 동일 SW 독립 운영

### 공차 분포 (Rapid City Z-Stopper, 2D 항목)
- ±0.05 mm: 다수 (가장 느슨)
- ±0.03 mm: 다수
- ±0.02 mm: 일부
- ±0.015 mm: 일부
- ±0.01 mm: 소수 (가장 빡빡, 분해능 마진 없음 → 다중 캡처 + Z stacking 으로 보완)

### 텔레센트릭 렌즈의 의미
일반 렌즈는 Z 위치가 바뀌면 배율이 바뀌어 같은 점이 다른 픽셀 위치에 찍힘.
텔레센트릭 렌즈는 Z가 바뀌어도 배율이 그대로 유지되어 위치가 변하지 않고 초점만 바뀜.
→ Z stacking 다중 캡처를 위한 필수 조건. 측정 SW의 기본 가정.
잔여 telecentricity error 약 0.05° (Z 1mm당 약 1μm 위치 이동) 존재하지만 무시 가능 수준.

---

## 2. PDF 페이지 인덱스

### Datum 정의 페이지 (p.21 ~ p.38)

| 페이지 | Fixture | 단계 | 종류 | 2D 사용 여부 |
|---|---|---|---|---|
| p.21 | #1 Top view jig | Pre-Datum | 임시 정렬 (N1) | ❌ 무시 |
| p.22 | #1 Top | Datum A | Plane (3D) | ❌ 범위 외 |
| **p.23** | **#1 Top** | **Datum B,C** | **L3(X) + L4(Y) + N2(원점)** | ✅ **핵심** |
| p.24 | #2 Bottom view jig | Pre-Datum | 임시 정렬 | ❌ 무시 |
| p.25 | #2 Bottom | Datum A | Plane B (3D) | ❌ 범위 외 |
| **p.26** | **#2 Bottom** | **Datum B,C** | **L7(X) + L8(Y) + N3(원점)** | ✅ |
| p.27 | #3-1 Side1 Left | Pre-Datum | 임시 | ❌ |
| p.28 | #3-1 Side1 Left | Sub Datum-C1 | Sub Datum | ⚠️ 검토 필요 |
| **p.29** | **#3-1 Side1 Left** | **Datum A,B** | **L11(X) + L12(Y)** | ✅ |
| p.30 | #3-2 Side1 Right | Pre-Datum | 임시 | ❌ |
| p.31 | #3-2 Side1 Right | Sub Datum-C2 | Sub Datum | ⚠️ |
| **p.32** | **#3-2 Side1 Right** | **Datum A,B** | line + line | ✅ |
| p.33 | #4-1 Side2 Up | Pre-Datum | 임시 | ❌ |
| p.34 | #4-1 Side2 Up | Sub Datum-B1 | Sub Datum | ⚠️ |
| **p.35** | **#4-1 Side2 Up** | **Datum A,C** | **L19(X) + L20(Y)** | ✅ |
| p.36 | #4-2 Side2 Down | Pre-Datum | 임시 | ❌ |
| p.37 | #4-2 Side2 Down | Sub Datum-B2 | Sub Datum | ⚠️ |
| **p.38** | **#4-2 Side2 Down** | **Datum A,C** | line + line | ✅ |

### 측정 정의 페이지 시작 (p.39 ~)

| 페이지 범위 | FAI 그룹 | Fixture |
|---|---|---|
| p.39 ~ p.48 | A1 ~ A10 | Top |
| p.49 ~ p.61 (추정) | A11 ~ A23 | Top |
| 그 이후 | C, B, E, F, G, H, I 그룹 | 각 fixture |

---

## 3. Datum 만드는 절차 (p.23 Top fixture 기준)

### 5단계 절차

```
1. 점 A1, A2 잡기
   - 제품 아래쪽 가장자리(긴 변) 위의 두 점
   - 각 점은 ROI 안에서 에지 검출로 자동 찾음

2. A1, A2를 통과하는 직선 L3 구성
   - line fit
   - L3 = Datum B = X축 (가로 기준선)

3. 점 B1 잡기
   - 제품 좌측 상단의 둥근 구멍 중심
   - OMM의 circle tool (Halcon: find_circle 또는 fit_circle_contour_xld)
   - 원 가장자리 점 수십 개로 fitting → sub-pixel ~0.05px 정밀

4. 직선 L4 구성
   - B1을 통과하면서 L3에 수직인 직선
   - line fit이 아니라 기하학적 제약으로 결정 (수학 계산)
   - L4 = Datum C = Y축 (세로 기준선)

5. L3 ∩ L4 = N2
   - 두 직선의 교점
   - N2 = 측정 좌표계의 원점 (0, 0)
   - 이후 모든 측정은 N2 기준 mm 좌표
```

### 좌표 표 (p.23)

| 점 | X mm | Y mm | 의미 |
|---|---|---|---|
| A1 | 1.32 | 0 | L3 위, N2에서 오른쪽 1.32mm |
| A2 | 22.26 | 0 | L3 위, N2에서 오른쪽 22.26mm |
| B1 | 0 | 20.20 | L4 위, N2에서 위로 20.20mm |

**해석**:
- A1, A2의 Y=0 → 둘 다 X축(L3) 위 (line fit 결과로 당연)
- B1의 X=0 → Y축(L4) 위 (L4가 B1 통과로 정의되어 당연)
- A1의 X=1.32 ≠ 0 → A1은 N2가 아님. L4가 B1 통과인데 A1은 B1과 X 위치 다름.

### 표 좌표의 용도

표의 숫자는 **만드는 순서가 아니라 만든 후 환산된 결과값**이다.
실제 만드는 순서:
1. 픽셀 단위로 A1, A2, B1 검출
2. line fit, perpendicular line, intersection 계산
3. N2 픽셀 좌표 확정
4. **그 다음에** 모든 점의 mm 좌표 환산 (검증용)

용도:
- **검증**: 티칭이 잘 됐는지 확인 (1.32 ± 작은 오차 안에 들어오는지)
- **재현성**: 다른 장비/다른 사람이 같은 결과 내는지 비교
- **양산 모니터링**: 매 제품마다 1.32에서 크게 벗어나면 fixture 어긋남 경고

### 측정 조건 (p.23)

| 항목 | 값 |
|---|---|
| Fixture | #1 Top view jig |
| 장비 | OMM (Optical Measurement Machine) |
| 조명 | **Back light** (백라이트, 실루엣 강조) |
| 배율 | Zoom 3 (x150) — OMM 기준, 우리 SW는 무관 |

→ **백라이트 사용**: 외곽선과 구멍을 선명한 실루엣으로 잡기 위함. 우리 SW의 Datum 캡처도 백라이트가 유리하다는 힌트.

---

## 4. Pre-Datum (N1) vs Datum (N2)

### 알고리즘적 차이는 거의 없음

이전에 "거친 정렬 vs 정밀"로 과장 설명했으나, 실제 PDF를 보면 두 단계 모두 "두 직선의 교점을 원점으로" 하는 같은 패턴이다.

| 항목 | N1 (p.21) | N2 (p.23) |
|---|---|---|
| 알고리즘 | 두 직선 교점 | 두 직선 교점 (perpendicular through point 변형) |
| 정밀도 | 비슷 | 비슷 |
| 사용 feature | 큰 외곽 feature | 작은 정밀 feature |
| **OMM Zoom** | **x40** | **x150** |

### 진짜 차이는 OMM의 한계 때문

OMM은 줌 렌즈를 가진 측정기라서:
- 낮은 배율(x40): 시야는 넓지만 정밀도 낮음 → 거친 정렬 필요
- 높은 배율(x150): 정밀하지만 시야 좁음 → 처음부터 못 찾음, 거친 정렬이 먼저 필요

→ **2단계로 분리**할 수밖에 없음.

### 우리 SW에는 N1 불필요

텔레센트릭 고정 렌즈는 배율이 안 바뀌므로:
- 한 번에 N2(정밀) 만들면 끝
- N1 단계 없음
- Pre-Datum 페이지(p.21, 24, 27, 30, 33, 36)는 무시

**예외**: 만약 양산 중에 제품 위치 편차가 너무 커서 N2 ROI 안에 feature가 안 들어오면, 그때 Halcon shape-based matching (`find_shape_model`) 추가 검토. 현재는 deferred.

---

## 5. Fixture별 Datum 구조 정리 (2D만)

### Top fixture (#1, p.23)

```
Datum B = L3 (line fit, A1-A2)        ← X축
Datum C = L4 (perpendicular through B1)  ← Y축
원점 N2 = L3 ∩ L4
필요한 ROI: Line ROI 2개 + Circle ROI 1개
```

### Bottom fixture (#2, p.26) — 추정

```
Datum B = L7 (line fit)
Datum C = L8 (perpendicular through circle)
원점 N3 = L7 ∩ L8
필요한 ROI: Line ROI 2개 + Circle ROI 1개
```

### Side1 Left/Right fixture (#3-1, #3-2, p.29, p.32)

```
Datum A = L11/(L13 etc.) (line fit)    ← X축
Datum B = L12/(L14 etc.) (perpendicular through point)  ← Y축
+ Sub Datum-C1/C2 (별도 보조 Datum, 용도 미확인)
필요한 ROI: Line ROI 2개 + (Circle 또는 Point) ROI 1개
```

### Side2 Up/Down fixture (#4-1, #4-2, p.35, p.38)

```
Datum A = L19/L21 (line fit)           ← X축
Datum C = L20/L22 (perpendicular)       ← Y축
+ Sub Datum-B1/B2 (별도 보조 Datum, 용도 미확인)
필요한 ROI: Line ROI 2개 + Point/Circle ROI 1개
```

### 공통 관찰

- **모든 fixture가 "Line + Line + Perpendicular through Point/Circle" 패턴**
- **Side fixture에는 Sub Datum이 추가**로 있음 (의미는 측정 페이지를 봐야 확정)
- **Datum 이름이 fixture마다 다름** (Top/Bottom은 B,C / Side1은 A,B / Side2는 A,C)
- 같은 "Datum A"라도 fixture에 따라 의미가 다름 (Top은 plane=3D, Side는 line=2D)

---

## 6. 측정 페이지 패턴 (A1~A10 분석 결과)

### 측정 종류는 단 한 종류

A1~A10 모두 동일한 3단계 절차:

```
1. Capture points : P1 (또는 P1, P2)
2. Based on the Datum B (또는 C)
3. Calculate : Measure the dimension from Datum X to Pn in Y(또는 X) direction
```

→ **측정 종류 = "Datum 기준선에서 점까지의 거리"** 단일 패턴
→ 우리 SW의 `PointToLineDistanceMeasurement` 단일 클래스로 처리 가능

### Datum 종류와 측정 방향의 관계

- **Datum B (X축 기준선) 사용** → 거리는 **Y방향** (X축에 수직)
- **Datum C (Y축 기준선) 사용** → 거리는 **X방향** (Y축에 수직)

→ SW에서 방향은 Datum 종류에 따라 자동 결정 가능 (사용자 입력 불필요)

### 점 개수의 의미

- 점 1개 (P1): 단일 위치 측정
- 점 2개 (P1, P2): 같은 거리에 있어야 할 두 위치
  - "윗변이 X축과 평행한가" 검증
  - "사각형이 직각인가" 검증
  - SW에서 측정 2개로 분리 또는 1개로 평균 처리 (결정 필요)

### A1~A10 측정값 표

| 페이지 | FAI | Nominal | 공차 | Datum | 점 | 방향 | 의미 |
|---|---|---|---|---|---|---|---|
| p.39 | A1 | 20.681 | ±0.030 | B | P1, P2 | Y | 제품 윗변 양쪽 모서리, Datum B로부터 세로 길이 |
| p.40 | A2 | 19.771 | ±0.050 | B | P1 | Y | 안쪽 단차 위치 |
| p.41 | A3 | 18.500 | +0.050/−0.020 | B | P1, P2 | Y | 안쪽 변의 좌우 대칭 |
| p.42 | A4 | 2×2.180 | +0.020/−0.050 | B | P1, P2 | Y | 아래쪽 가장자리 단차 |
| p.43 | A5 | 1.079 | ±0.050 | B | P1, P2 | Y | 더 가까운 단차 |
| p.44 | A6 | 0.910 | ±0.050 | B | P1 | Y | 가장 안쪽 단차 (좌측) |
| p.45 | A7 | 10.175 | ±0.050 | C | P1 | X | 제품 가로 길이 일부 |
| p.46 | A8 | 0.532 | ±0.050 | C | P1 | X | 좌측 가장자리 단차 |
| p.47 | A9 | 0.834 | ±0.050 | B | P1 | Y | 우측 안쪽 단차 |
| p.48 | A10 | 0.979 | ±0.020 | B | P1, P2 | Y | 좌우 대칭 단차 (정밀, ±0.02) |

### A1 (p.39) 점 위치 상세

```
        ↑ Y축 (Datum C, L4)
        │
        │
   N2 ──┼─────────────────────→ X축 (Datum B, L3)
        │
        │  P1 ●            ● P2
        │  (0.385,          (9.370,
        │   20.681)          20.681)
```

- P1, P2는 제품 윗변의 양쪽 모서리 두 점
- 둘 다 Datum B(L3)로부터 Y방향 20.681mm 떨어져야 함
- 검사 의미: 제품 세로 길이 + 윗변이 Datum B와 평행한지 동시 확인

---

## 7. 06-CONTEXT.md에 미치는 영향

### 검증된 결정

✅ **D-04**: DatumConfig가 Line ROI 2개 가짐 — 부분 정확
✅ **D-06**: DatumName 필드를 string으로 둠 — 정확 (B, C, A, Sub-B1 등 다양)
✅ **D-15**: `PointToLineDistanceMeasurement` 단일 클래스로 충분 — A1~A10 검증됨
✅ **D-14**: `TolerancePlus`/`ToleranceMinus` 분리 — 정확 (비대칭 공차 존재)
✅ **D-17**: 다형성 INI 직렬화 (Type=) 패턴 — 정확
✅ **3D 무시 결정** — Datum A(plane) 무시 가능

### ⚠️ 수정이 필요한 결정

⚠️ **D-04 보완**: DatumConfig가 **Circle ROI도 필요**
- 현재: Line1_ROI + Line2_ROI 만 있음
- 필요: Line1_ROI + Line2_ROI + Circle_ROI (또는 Point_ROI)
- 이유: Datum C(Y축)가 line fit이 아니라 "circle 중심 통과 + 다른 line에 수직"으로 만들어짐

⚠️ **Sub Datum 처리**: Side fixture (#3-1, #3-2, #4-1, #4-2)에 Sub-C1, Sub-C2, Sub-B1, Sub-B2 별도 존재
- 현재: List<DatumConfig>로 표현 가능 (구조 변경 불필요)
- 미확인: Sub Datum의 정확한 용도 (측정 페이지 분석 후 확정)

⚠️ **Datum 만드는 알고리즘 빌딩 블록 추가**:
- `find_circle` / `fit_circle_contour_xld` (B1 같은 점 검출)
- `perpendicular_line_through_point` (L4 같은 직선 만들기, 수학 계산)
- 06-CONTEXT.md D-18 (VisionAlgorithmService) 에 이미 포함됨 ✓

### 그대로 유지

- D-01 (Datum이 Sequence 레벨)
- D-02 (FindDatum이 Sequence 진입 시 1회)
- D-05 (DatumConfig 클래스 자체는 보존)
- D-09 (런타임 실행 흐름)
- D-22 (마이그레이션 안 함)

---

## 8. 미해결 항목 (다음 분석에서 확인 필요)

### 측정 페이지 분석 잔여 (총 75개 중 10개 분석 완료)
- A11~A23 (p.49~61 추정) — Top fixture 나머지
- C1~C12 (Top fixture)
- I3, I9~I14 (Top fixture)
- B1~B4 (Bottom fixture)
- E1~E10 (Bottom fixture)
- F1~F2 (Bottom fixture)
- G1~G12 (Bottom fixture, 원호 에지로 표시 — 어려움)
- I5~I8 (Bottom fixture)
- C13, C14 (Side1)
- D1 (Side1)
- F9 (Side2)
- H5 (Side1)

### Datum 페이지 잔여 분석
- p.26 Bottom Datum B,C — Top과 같은 패턴인지 확인
- p.29 Side1 Left Datum A,B — Side는 Datum 이름이 다른 이유
- p.28, p.31, p.34, p.37 Sub Datum 페이지 — 정확한 용도

### 새 알고리즘 검토 필요 가능성
- **Angle 측정**: D1 (p.미확인) — 두 직선 사이 각도. 우리 SW에 `LineToLineAngleMeasurement` 필요 (06-CONTEXT D-15에 이미 있음 ✓)
- **Circle diameter 측정**: E1 (p.미확인) — 직경. `CircleDiameterMeasurement` 필요 (이미 있음 ✓)
- **Line-to-Line distance**: E3 등 — 두 평행선 거리. `LineToLineDistanceMeasurement` 필요 (이미 있음 ✓)
- **Point-to-Point distance**: E5 등 — 두 점 거리. `PointToPointDistanceMeasurement` 필요 (이미 있음 ✓)

### Side 카메라 운영 (확정됨)
- Top PC + Side PC 분리 운영
- 같은 SW를 두 PC에 설치, 독립 동작
- PC 간 통신 없음
- 06-CONTEXT.md D-23 그대로 유지

### 3D 시스템과의 연동 (확정됨)
- 우리 SW는 2D만 다룸
- 3D 측정은 별개 시스템
- 우리 SW에서 3D 데이터 송수신 0건
- 제품 ID(SN) 추적은 MES 레벨, 우리 SW는 자기 결과만 TCP/MES로 업로드

---

## 9. 다음 액션

### 즉시 (Phase 6 RESEARCH 단계)
1. 06-CONTEXT.md의 D-04에 **Circle ROI 추가** 결정 명시
2. p.26, p.29 Datum 페이지를 이미지로 분석하여 fixture 간 패턴 일관성 확인
3. Sub Datum 페이지(p.28 등)를 분석하여 용도 확정
4. 측정 페이지 A11~A23 분석하여 새 알고리즘 패턴 발견 여부 확인

### 중기 (Phase 6 PLAN 단계 전)
- 75개 측정 전체를 알고리즘별로 분류한 표 작성
- 각 측정이 어느 MeasurementBase 파생 클래스에 매핑되는지 확정
- DatumConfig의 Circle ROI 추가에 따른 Phase 4 호환성 영향 분석

### 장기 (Phase 6 실행 중)
- 양산 직전 실측 캘리브레이션으로 분해능 2.2 μm/pix 확인
- ±0.01 mm 항목 Cpk 측정 → 부족하면 다중 캡처 전략 확장
- Pre-Datum 필요성 재검토 (제품 위치 편차가 N2 ROI 범위를 벗어나는지)

---

## 부록: 용어 정리

| 용어 | 의미 |
|---|---|
| **MSOP** | Measurement Standard Operating Procedure (Apple/고객의 측정 표준 문서) |
| **OMM** | Optical Measurement Machine (Mitutoyo Quick Vision 등 표준 측정기) |
| **FAI** | First Article Inspection (제품 1개당 검사 항목) |
| **SPC** | Statistical Process Control (통계적 공정 관리, FAI의 부분집합) |
| **CTQ** | Critical to Quality (핵심 품질 항목) |
| **Datum** | 측정 기준 좌표계의 기준선/기준점 |
| **Pre-Datum** | 임시 거친 정렬용 Datum (OMM의 낮은 배율 단계) |
| **Sub Datum** | 보조 Datum (Side fixture에서만 등장, 용도 미확정) |
| **L3, L4, L7, L8 등** | line의 식별자 (1번째 fit된 직선 = L1, 2번째 = L2 식으로 누적 번호) |
| **N1, N2, N3 등** | 점의 식별자 (주로 직선 교점) |
| **CT** | Cycle Time (제품 1개 검사하는 데 걸리는 시간) |
| **UPH** | Units Per Hour (시간당 검사 가능 개수) |
| **DOF** | Depth of Field (피사계 심도, 텔레센트릭 렌즈는 보통 0.15~0.5 mm) |
| **Texture-less** | 표면이 균일해서 에지가 약한 부위 (검출 어려움) |

---

*분석 진행 상황 (2026-04-10):*
*- Datum 페이지: p.21~38 텍스트 분석 완료, p.23만 이미지 분석 완료*
*- 측정 페이지: A1~A10 (p.39~48) 텍스트 분석, p.39만 이미지 분석 완료*
*- 잔여: 측정 페이지 65개, Datum 페이지 이미지 7장*
