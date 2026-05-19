# Phase 31 — SOP 참조 다이제스트 (Datum 기준 측정 알고리즘)

**작성:** 2026-05-19
**용도:** Phase 31 discuss-phase / plan-phase 입력 자료. 원본 47MB pptx 재파싱 없이 사용.

## 원본 문서

| 문서 | 경로 | 추출 결과 |
|------|------|-----------|
| Datum 정보 데크 | `C:\Info\Doc\2.디팜스테크\02_설계\SOP\Datum_정보_260511_2D.pptx` | 89슬라이드 (S1~S83 텍스트, S84~S89 이미지만) |
| 원본 MSOP | `C:\Info\Doc\2.디팜스테크\02_설계\SOP\260303_Rapicity_A8.1_Z-Stopper_MSOP_RevB_변경내역 표기.pdf` | 3,523줄 (pdftotext -layout) |

> pptx 데크는 MSOP(RevB)를 Datum 기준 2D 측정 관점으로 재정리한 것. 충돌 시 MSOP가 원본.

---

## 1. Datum 좌표계 구조

- **Pre-Datum** (각 Fixture): P1·P2 → L_X (X축), P3 → L_Y (P3 통과, L_X 직교). L_X ∩ L_Y = N (0,0) = OMM 물리 얼라인 기준점.
- **Datum B**: Top/Bottom View 하단 수평부의 접선 (수평 기준선) → **Y 방향 거리 기준**.
- **Datum C**: Datum B와 직교하고 B1 홀 센터를 통과하는 수직선 → **X 방향 거리 기준**.
- **Datum A**: Side View에서 자재 상단 수평부의 접선 → **각도 기준선**.
- Datum B/C 구성: A1·A2 2점으로 라인 L3 → B1 홀을 circle tool로 캡처(센터점) → B1 통과·L3 직교 라인 L4 = Datum C.

**Phase 23.1 EdgeToLineDistance**가 이미 구현한 것: projection_pl 정사영 수직거리 + MeasureAxis X/Y. Phase 31은 이 구조를 측정 대상(점/원중심/교점/직선)을 확장하는 방향으로 일반화.

---

## 2. Phase 31 신규 측정 타입 (SOP 절차 기준)

### E8 — 원중심 → Datum 거리
- **MSOP 절차** (S55, p.91): ① P1 이동 → circle tool로 원 CL1 취득 ② CL1 중심점 Pd 구성 ③ Datum B 기준 설정 ④ Datum B → Pd **Y 방향** 거리 측정.
- **공차 예:** 20.201 ±0.030
- **본질:** EdgeToLineDistance와 동일(projection_pl + MeasureAxis)하되 측정점이 **원피팅 중심점**. 원 ROI → HALCON 원 피팅 → 중심 (row,col) → 기존 거리 로직 재사용 가능.
- **FAI 항목:** E8 (Bottom). 유사 패턴: E1(Diameter, 기존 CircleDiameter).

### D1 — Datum 기준 각도 (벽면 각도)
- **MSOP 절차** (S73~S80, p.82·83·115): ① 2점으로 직선 구성(예: P1&P2 → LU) ② Datum A 기준 설정 ③ Datum A 기준 해당 직선의 **각도** 측정.
- **공차 예:** 6×90.000° +0.5°/-1.5° (단변1: LU/MU/MD/LD 4벽면), 90.000° +0.5°/-1.5° (단변2: RD/RU 2벽면), H5 장변3: 90.0° ±1.5° (MN 직선).
- **⚠ ROADMAP↔SOP 불일치:** ROADMAP은 D1을 "Datum 기준 각도" 신규 타입으로 기술. SOP 슬라이드 73·76·79는 D1/H5 알고리즘을 **`LineToLineAngle` (v1.1 신규)** 로 명시. → 신규 타입이 아니라 기존/계획된 각도 알고리즘 재사용일 수 있음. **discuss-phase에서 확정 필요.**
- **FAI 항목:** D1 (Side1 단변1/단변2), H5 (Side3 장변3).

### I9 / I10 — 호 ∩ 라인 교점 → Datum 거리
- **MSOP 절차** (S40~S41, p.121·122): ① P1~P3, P6~P8 캡처 ② **arc A1 = fit(P1~P3), arc A2 = fit(P6~P8)** ③ 라인 캡처 (I9: P5·P10 → Lb·Ld / I10: P4·P9 → La·Lc) ④ **교점 = arc ∩ line** (I9: Pb=A1∩Lb, Pd=A2∩Ld / I10: Pa=A1∩La, Pc=A2∩Lc) ⑤ Datum C → 교점들 **X 방향** 거리 측정.
- **공차 예:** I9 2×5.053 +0.050/-0.000
- **본질:** 신규 기하 연산 2종 — (a) 3점 호 피팅, (b) **호-라인 교점** (해 2개 가능 → 선택 규칙 필요).
- **FAI 항목:** I9, I10 (Top).

### CompoundAngle — E2 / E9 / E10 (복합 각도)
- **MSOP 절차** (S49·S56·S57, p.85·92·93): ① P1~P3 → circle tool로 원 CL1~CL3 ② P4·P5 → line tool로 La·Lb ③ **중간선 Lc = midline(La,Lb)** ④ **교점 Pa = Lc ∩ CL2, Pb = Lc ∩ CL3** ⑤ **중심점 Pc = center(Pa,Pb)** ⑥ (E2만) 중심점 Pd = center(CL1), 직선 Ld = line(Pc,Pd) ⑦ Datum 기준 설정 ⑧ Datum 기준 **각도** 측정.
- **공차 예:** E2 41.36° ±1.00°
- **본질:** Phase 31에서 가장 복잡. 다단계 기하 구성 체인(원피팅 ×3 → 미들라인 → 라인-원 교점 ×2 → 중점 → 라인 → 각도). E9/E10은 CL2~CL3만 사용(CL1 생략), 각각 Datum C/Datum B 기준.
- **⚠ SOP 슬라이드 56/57 텍스트 불일치:** S56 화살표는 "Datum C 기준 Pc **각도** 측정"인데 본문 9번은 "Datum C → Pc **거리** 측정". S57도 동일 혼선. **각도/거리 여부 discuss-phase에서 확정 필요.** (MSOP 기준 E2는 Angle.)
- **FAI 항목:** E2(Bottom, Angle 41.36°), E9, E10.

### ArcEdgeDistance — G 시리즈
- **MSOP 절차** (S60~S67): G1·G2·G5·G6·G7·G8·G11·G12 (8개) → 각 ① P1 캡처 ② Datum C 기준 설정 ③ Datum C → P1 **X 방향** 거리.
- **공차 예:** G2 22.162 ±0.030, G8 11.132 ±0.020, G11 24.362 ±0.020
- **⚠ 불일치:** SOP 슬라이드 본문은 단순 "포인트 P1 → Datum 거리"로 표기. 그러나 슬라이드 43은 "G1·G2·G5~G8·G11·G12 (8개) Datum C → X 방향 (**ArcEdgeDistance 알고리즘**)"으로 분류. → P1이 일반 에지점이 아닌 **호 에지 위의 점**일 가능성. 측정점 정의(호 위 어느 지점)와 ArcEdgeDistance가 EdgeToLineDistance와 어떻게 다른지 **discuss-phase에서 확정 필요.**
- **FAI 항목:** G1, G2, G5, G6, G7, G8, G11, G12 (Bottom).

---

## 3. 전체 FAI 인벤토리 (SOP 슬라이드 4·43·73·76·79 기준)

| View / Fixture | FAI 그룹 | 개수 | 측정 성격 |
|----------------|----------|------|-----------|
| Top #1 | A1~A23 | 23 | Datum B→Y / Datum C→X 거리 (기존 EdgeToLineDistance) |
| Top #1 | C1~C12 | 12 | Datum C→X 거리 (기존) |
| Top #2 | I9, I10 | 2 | **호∩라인 교점 → Datum C→X 거리 (신규)** |
| Top #2 | I3, I11~I14 | 5 | Datum B→Y / Datum C→X 거리 |
| Bottom #2 | B1~B4 | 4 | Datum B→Y 거리 (기존) |
| Bottom #2 | E1 | 1 | Diameter (기존 CircleDiameter) |
| Bottom #2 | E3 | 1 | Line→Line 거리 |
| Bottom #2 | E4 | 1 | Datum C→X 거리 |
| Bottom #2 | E5 | 1 | 구성직선 Py → 점 거리 |
| Bottom #2 | E6, E7 | 2 | Datum B→Y 거리 |
| Bottom #2 | **E8** | 1 | **원중심 → Datum B→Y 거리 (신규)** |
| Bottom #2 | **E2, E9, E10** | 3 | **CompoundAngle / 복합각도 (신규)** |
| Bottom #2 | F1, F2 | 2 | Datum C→X 거리 |
| Bottom #2 | **G1·G2·G5~G8·G11·G12** | 8 | **ArcEdgeDistance, Datum C→X (신규)** |
| Bottom #2 | I5~I8 | 4 | Datum B→Y 거리 |
| Side1 #3-1/#3-2 | **D1** | 1(단변1/단변2) | **Datum A 기준 벽면 각도 (LineToLineAngle)** |
| Side3 #4-2 | **H5** | 1 | **Datum A 기준 직선 MN 각도 (LineToLineAngle)** |
| Side3 #4-2 | C13, C14, F9 | 3 | Datum A→Y 거리 |

---

## 4. discuss-phase 확정 필요 그레이 에어리어

1. **D1 알고리즘 정체** — 신규 타입인가, 아니면 SOP 표기대로 `LineToLineAngle` (v1.1 신규)인가? H5도 동일 알고리즘 사용.
2. **CompoundAngle 각도/거리** — SOP 슬라이드 56/57 본문이 "거리"와 "각도"로 혼선. MSOP E2는 Angle. E9/E10 측정 성격 확정.
3. **ArcEdgeDistance 정의** — G 시리즈 측정점 P1이 호 위의 점인지, ArcEdgeDistance가 EdgeToLineDistance와 어떻게 다른지.
4. **호 피팅 연산자** — HALCON 3점/N점 원·호 피팅 연산자 선택 (I9/I10 arc, E2/E9/E10 circle).
5. **호-라인 교점 해 선택** — arc ∩ line 교점이 2개일 때 선택 규칙 (가까운 점 / ROI 내부 / 방향).
6. **기하 구성 체인의 데이터 모델** — CompoundAngle처럼 중간 산출물(midline, crosspoint, centerpoint)이 많은 측정을 FAIConfig/ShotConfig에 어떻게 표현할지.
7. **CO-23.1-01** — TeachingImagePath ≠ InspectionImagePath 시 뷰어 듀얼 이미지 표시.
8. **CO-23.1-02** — 측정 타입별 Rect ROI 버튼 활성화 일반화 (현재 EdgeToLineDistance·CircleDiameter만).

---

## 부록 — MSOP 원문 핵심 절차 인용

**FAI E2 (Angle 41.36° ±1.00°, Datum B):**
> 1. capture circles CL1~CL3 with circle tool / 2. capture lines La, Lb with line tool / 3. Construct a midline Lc with La, Lb / 4. Construct two crosspoints Pa(by Lc ∩ CL2), Pb(by Lc ∩ CL3) / 5. Construct a centerpoint Pc by Pa, Pb / 6. Construct a centerpoint Pd by CL1 / 7. Construct a line Ld by Pc, Pd / 8. Based on Datum B / 9. Measure the Angle from Datum B to Ld

**FAI I9 (Distance 2×5.053 +0.050/-0.000, Datum C):**
> 1. Capture points P1~P3, P6~P8 / 2. Construct arc A1 by P1~P3, A2 by P6~P8 / 3. capture lines Lb, Ld / 4. Construct crosspoint Pb by A1, Lb / 5. Construct crosspoint Pd by A2, Ld / 6. Measure dimension from Datum C to Pb, Pd in X direction
