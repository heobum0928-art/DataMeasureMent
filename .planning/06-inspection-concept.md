# Rapid City Z-Stopper — 검사 컨셉 정의서

**작성일**: 2026-04-17
**대상**: DataMeasurement Phase 6 (Rapid City AOI)
**용도**: Claude Code CLI 컨텍스트 — SW 설계/구현 기준 문서

---

## 1. 기본 전제

| 항목 | 내용 |
|---|---|
| 제품 | A8.1 Z-Stopper (Apple Rapid City) |
| 카메라 | 152MP, 3.76μm 픽셀, 텔레센트릭 고정 1.7x |
| 분해능 | 약 2.43μm/pix |
| FOV | 약 31×23mm (제품 전체 1샷 커버) |
| Top/Side | 별도 PC 독립 운영, 동일 SW |
| Z축 제어 | PLC 주도 (비전 SW는 트리거 대기만) — 현재 배제 |
| 기준 문서 | MSOP `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB` |

---

## 2. 검사 구조 (계층)

```
Fixture (면 단위)
├─ Datum (좌표계, Fixture당 1회 계산)
└─ Shot N개 (캡처 조건: 조명/노출)
   └─ FAI M개 (측정 항목)
      └─ ROI K개 + 알고리즘 1개
```

### 2-1. Fixture (면)

| Fixture | 페이지 | Datum | 커버 FAI |
|---|---|---|---|
| Top (#1) | p.23 | B, C | A1~A23, C1~C12, F6, F9 (35개) |
| Bottom (#2) | p.26 | B, C | B1~B4, E1~E10, F1~F8, G1~G12, H2~H3, I5~I8 (28개) |
| Side1-L (#3-1) | p.29 | A, B | D1 일부 |
| Side1-R (#3-2) | p.32 | A, B | D1 일부 |
| Side2-U (#4-1) | p.35 | A, C | C13, C14, H5 일부 |
| Side2-D (#4-2) | p.38 | A, C | C13, C14, H5 일부 |

### 2-2. Datum (좌표계 기준)

**Top/Bottom Datum 만드는 절차 (p.23 기준)**:
```
1. 점 A1, A2 잡기 (Line ROI 2개) → line fit → L3 (Datum B = X축)
2. 원 B1 잡기 (Circle ROI 1개) → fit_circle → 구멍 중심
3. B1 통과 + L3에 수직 → L4 (Datum C = Y축)
4. L3 ∩ L4 = N2 = 원점 (0, 0)
```

**Datum 특성**:
- Fixture 진입 시 **1회만 계산**
- 같은 Fixture의 모든 Shot/FAI가 공유
- 매 제품마다 새로 계산 (제품 위치 편차 보정)
- 필요 ROI: **Line ROI 2개 + Circle ROI 1개**

**Datum이 하는 일**:
```
Datum 계산 완료
    ↓
N2(원점) 픽셀 좌표 확정
    ↓
모든 FAI의 ROI 위치를 mm → 픽셀 변환 가능
    ↓
ROI 자동 배치
```

### 2-3. Shot (캡처 조건)

- **조명 종류**: Ring / Back / Coax / Side + 밝기
- **노출**: ms 단위
- Z축은 현재 배제 (PLC가 제어)
- 어느 FAI를 어느 Shot에 묶을지는 **실장비 조명 테스트 후** 결정

### 2-4. FAI (측정 항목)

- 각 FAI는 **ROI K개 + 알고리즘 1종** 으로 구성
- ROI 위치는 **MSOP 좌표 (mm) → Datum → 픽셀** 으로 자동 계산
- 측정 결과: **실측값(mm) vs 명목값(mm) ± 공차** → OK/NG

---

## 3. 검사 컨셉 핵심 흐름

```
[티칭 단계 — 1회]
사용자가 MSOP 좌표 입력
    ↓
Datum ROI 위치 설정 (Line 2 + Circle 1)
    ↓
각 FAI의 ROI 위치 확인/조정 (자동 배치 후 미세 조정)
    ↓
알고리즘 파라미터 설정 (에지 임계값 등)
    ↓
레시피 저장

[검사 단계 — 매 제품]
PLC 트리거 수신
    ↓
Fixture별 Datum 계산 (1회)
    ↓
Shot별 이미지 캡처
    ↓
FAI별 측정 실행
    ↓
결과 집계 → OK/NG
    ↓
PLC에 결과 송신
```

---

## 4. ROI 종류 (5가지)

| ROI 종류 | 설명 | 사용처 |
|---|---|---|
| **RectROI** | 직선 에지 검출용 직사각형 | A/B/C/E/F/I 대부분 |
| **LineROI** | 직선 fit용 (긴 직사각형) | Datum B/C, D1, H5, E3 |
| **CircleROI** | 원 검출용 | Datum B1, E1 |
| **ArcROI** | 원호 에지 검출용 | G1~G12 (원호 에지) |
| **MultiROI** | 복합 측정용 (여러 ROI 조합) | E2, E5, D1 |

---

## 5. 알고리즘 종류 (7가지)

### Algo 1: EdgeToLineDistance (가장 많이 사용)
```
ROI 1개 → measure_pos → 에지 점 1개
→ Datum 선까지 수직 거리
→ Nominal ± Tolerance 판정
```
- 해당 FAI: A 전체, B 전체, C 전체, E4/E6/E7/E8/E9/E10, F1/F2/F3, I5~I8
- ROI 수: 1~2개 (2x 항목은 2개)
- Halcon: `measure_pos`, `distance_pl`

### Algo 2: CircleDiameter
```
Circle ROI 1개 → fit_circle_contour_xld → 중심 + 반경
→ 직경 = 반경 × 2 → Nominal ± Tolerance 판정
```
- 해당 FAI: E1
- ROI 수: 1개
- Halcon: `fit_circle_contour_xld`

### Algo 3: LineToLineDistance
```
ROI 2개 → find_line → La, Lb
→ distance_ll(La, Lb) → Nominal ± Tolerance 판정
```
- 해당 FAI: E3
- ROI 수: 2개
- Halcon: `find_line`, `distance_ll`

### Algo 4: LineToLineAngle
```
ROI N개 → find_line → L1, L2...
→ angle_ll(DatumA, L1) → Nominal ± Tolerance 판정
```
- 해당 FAI: D1 (8 ROI), H5 (2 ROI)
- ROI 수: 2~8개
- Halcon: `find_line`, `angle_ll`

### Algo 5: CompoundAngle (E2 전용, 가장 복잡)
```
Circle ROI 1개 (CL1) → fit_circle → Pd
Line ROI 2개 (La, Lb) → find_line
    Lc = midline(La, Lb)
    Pc = midpoint on Lc (두 핀 중점)
    Ld = line(Pc, Pd)
→ angle(DatumB, Ld) → 41.36° ± 1.00° 판정
```
- 해당 FAI: E2
- ROI 수: 3개 (Circle 1 + Line 2)
- **주의**: CL2, CL3 원은 반쪽만 노출 → La, Lb 직선으로 우회
- Halcon: `fit_circle_contour_xld`, `find_line`, `angle_ll`

### Algo 6: ArcEdgeDistance (원호 에지, 허붐님 제안)
```
Line ROI 2개 → find_line → L1, L2
→ intersection_ll(L1, L2) → 이론 교점 P_theory
→ P_theory에서 측정 방향으로 스캔
→ 실제 에지 점 검출 (라운드 모서리 실제 에지)
→ Datum 선까지 거리 → Nominal ± Tolerance 판정
```
- 해당 FAI: G1~G12, 라운드 모서리 항목
- ROI 수: 2~3개
- 특징: 이론 교점(가상)이 아닌 실제 에지 위의 점을 측정
- Halcon: `find_line`, `intersection_ll`, `measure_pos`

### Algo 7: LineConstructDistance (E5 전용)
```
ROI 4개 → P1, P2로 가상 기준선 Py 구성
→ P3, P4까지 Py로부터 거리 측정
→ Nominal ± Tolerance 판정
```
- 해당 FAI: E5
- ROI 수: 4개
- 특징: Datum이 아닌 파생 직선이 기준
- Halcon: `find_line`, `distance_pl`

---

## 6. 측정 포인트 → ROI 위치 계산 원리

**MSOP 표의 좌표 (mm)를 ROI 픽셀 위치로 변환**:

```
MSOP: P1 = (X=-3.505, Y=19.771) [N2 기준 mm]
    ↓
픽셀 변환:
  P1_col = N2_col + (X / pixel_size_mm)
  P1_row = N2_row - (Y / pixel_size_mm)   ← Y축 반전 주의
    ↓
P1_pixel 위치에 ROI 중심 배치
    ↓
ROI 안에서 에지 검출
    ↓
에지에서 Datum 선까지 거리 = 측정값
```

**측정 방향 결정 규칙**:
- Datum B (X축) 기준 → **Y방향** 측정 (수직 거리)
- Datum C (Y축) 기준 → **X방향** 측정 (수평 거리)
- 표의 좌표에서 "측정값"은 Datum 방향의 좌표, "위치값"은 반대 방향의 좌표

---

## 7. 좌표 표 읽는 법

MSOP 측정 페이지 표 예시:
```
| Measurement Point | X mm  | Y mm   |
| P1                | 0.385 | 20.681 |
| P2                | 9.370 | 20.681 |
```

- **Datum: B, 방향: Y** → Y값이 측정값, X값이 위치값
- P1: "X=0.385 위치에서 에지를 잡아 Datum B까지 Y 거리 = 20.681mm 인지 확인"
- P2: "X=9.370 위치에서 에지를 잡아 Datum B까지 Y 거리 = 20.681mm 인지 확인"
- 두 점이 **같은 Y → 윗변이 Datum B와 평행한지** 동시 검증

---

## 8. 분해능 리스크 (2.43μm/pix 기준)

| 공차 | 1/10 요구 | 픽셀 | 상태 |
|---|---|---|---|
| ±0.050mm | 5μm | 20.6px | ✅ 안전 |
| ±0.030mm | 3μm | 12.3px | ✅ OK |
| ±0.020mm | 2μm | 8.2px | ⚠️ 경계 |
| ±0.010mm | 1μm | 4.1px | ❌ 위험 |

**위험 항목 (±0.010mm, 4.1px)**:
- C1, C3, C6, C8, C10, C12 (Top 슬롯)
- G1, G12 (Bottom 원호 에지)

**대응**: GRR 검증 후 고객과 협의. 1/10 rule은 절대 기준 아님 (GRR ≤ 30% 조건부 허용).

---

## 9. SW 범위 밖 항목

| FAI | 이유 |
|---|---|
| H1 (Thickness) | Spline Micrometer 별도 장비 |
| I1 (Flatness) | 3D plane fitting 필요 |
| H4, I3, I11~I14 | 스펙 미기재 — 고객 확인 필요 |

---

## 10. 고객 확인 필요 항목

1. **H1, I1**: AOI 측정 범위인지
2. **H4, I3, I11~I14**: 스펙 미기재
3. **C 그룹 (±0.010mm)**: GRR 기준 협의
4. **G 그룹 원호 에지**: 접선점 vs 이론 교점 중 어느 방식으로 측정하는지
5. **E2 (CL2, CL3)**: 반쪽 원 — La/Lb 직선 우회 방식 사용해도 되는지

---

## 11. Top면 FAI 전체 좌표 목록 (분석 완료)

### A 그룹 (23개)

| FAI | 페이지 | Datum | 방향 | 측정값(mm) | 점 수 | 부위 |
|---|---|---|---|---|---|---|
| A1 | p.39 | B | Y | 20.681 ±0.030 | 2 | 윗변 최상단 좌/우 |
| A2 | p.40 | B | Y | 19.771 ±0.050 | 1 | 좌측 clip 윗면 |
| A3 | p.41 | B | Y | 18.500 +0.050/−0.020 | 2 | clip 아랫면 좌/우 |
| A4 | p.42 | B | Y | 2.180 +0.020/−0.050 | 2 | 하단 탭 윗면 좌/우 |
| A5 | p.43 | B | Y | 1.079 ±0.050 | 2 | 탭 아랫면 좌/우 |
| A6 | p.44 | B | Y | 0.910 ±0.050 | 1 | 좌측 최하단 돌기 |
| A7 | p.45 | C | X | 10.175 ±0.050 | 1 | 중앙 상단 구조물 우측 |
| A8 | p.46 | C | X | −0.532 ±0.050 | 1 | 상단 구멍 좌측 |
| A9 | p.47 | B | Y | 0.834 ±0.050 | 1 | 우측 최하단 돌기 |
| A10 | p.48 | B | Y | 0.979 ±0.020 | 2 | 중앙 레일 하단 ⚠️ |
| A11 | p.49 | B | Y | 3.395 ±0.050 | 1 | 우측 clip 윗면 |
| A12 | p.50 | B | Y | 3.476 ±0.050 | 2 | 중앙 레일 하단부 |
| A13 | p.51 | B | Y | 3.850 ±0.050 | 2 | 중앙 내부 구조물 하단 |
| A14 | p.52 | B | Y | 16.831 ±0.050 | 2 | 중앙 내부 구조물 상단 |
| A15 | p.53 | B | Y | 17.204 ±0.050 | 2 | 중앙 레일 상단부 |
| A16 | p.54 | B | Y | 17.286 ±0.050 | 1 | 우측 clip 아랫면 (라운드) |
| A17 | p.55 | B | Y | 19.847 ±0.050 | 1 | 우측 clip 윗면 |
| A18 | p.56 | B | Y | 19.947 ±0.030 | 2 | 중앙 상단 2점 |
| A19 | p.57 | C | X | 0.401 +0.050/−0.020 | 1 | Datum C 우측 하단 ⚠️ |
| A20 | p.58 | C | X | 9.980 ±0.050 | 1 | 중앙 하단 구조물 우측 |
| A21 | p.59 | C | X | 21.017 +0.050/−0.020 | 1 | 우측 하단 구조물 ⚠️ |
| A22 | p.60 | C | X | 24.330 ±0.050 | 2 | 우측 내벽 상/하 |
| A23 | p.61 | C | X | 25.980 +0.020/−0.050 | 2 | 우측 외벽 상/하 ⚠️ |

### C 그룹 (12개, Top 슬롯)

| FAI | 페이지 | Datum | 방향 | 측정값(mm) | 비고 |
|---|---|---|---|---|---|
| C1 | p.66 | C | X | −2.750 +0.010/−0.030 | ❌ 위험 |
| C2 | p.67 | C | X | −2.350 +0.030/−0.010 | |
| C3 | p.68 | C | X | −1.494 +0.010/−0.030 | ❌ 위험 |
| C4 | p.69 | C | X | −1.094 +0.030/−0.010 | |
| C5 | p.70 | C | X | 10.301 +0.030/−0.010 | |
| C6 | p.71 | C | X | 10.701 +0.010/−0.030 | ❌ 위험 |
| C7 | p.72 | C | X | 11.557 +0.030/−0.010 | |
| C8 | p.73 | C | X | 11.957 +0.010/−0.030 | ❌ 위험 |
| C9 | p.74 | C | X | 24.072 +0.030/−0.010 | |
| C10 | p.75 | C | X | 24.472 +0.010/−0.030 | ❌ 위험 |
| C11 | p.76 | C | X | 25.328 +0.030/−0.010 | |
| C12 | p.77 | C | X | 25.728 +0.010/−0.030 | ❌ 위험 |

---

## 12. 미분석 항목 (다음 세션)

| 그룹 | 페이지 | 수량 |
|---|---|---|
| B (Bottom) | p.62~65 | 4개 |
| E1, E3~E10 (Bottom) | p.84~93 | 9개 |
| F1~F9 (Bottom/Top) | p.94~102 | 9개 |
| G1~G12 (Bottom 원호) | p.104~111 | 8개 |
| H2~H5 (Bottom/Side) | p.113~115 | 3개 |
| I5~I8 (Bottom) | p.117~120 | 4개 |
| C13~C14 (Side2) | p.78~81 | 2개 |
| D1 (Side1) | p.82~83 | 1개 |

---

## 13. 다음 세션 시작 메시지 (Claude Code CLI용)

```
06-inspection-concept.md 읽고 컨텍스트 파악해줘.
분석 완료: Top면 A1~A23, C1~C12, E2(알고리즘)
다음 작업: Bottom면 B1 (p.62) 이미지 분석 시작
PDF: /mnt/user-data/uploads/260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB_변경내역_표기.pdf
```
