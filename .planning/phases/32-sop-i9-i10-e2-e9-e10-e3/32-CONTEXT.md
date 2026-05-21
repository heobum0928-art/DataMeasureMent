---
phase: 32-sop-i9-i10-e2-e9-e10-e3
created: 2026-05-21
source: 사용자 대화 확정 (Phase 31 UAT 중)
---

# 32-CONTEXT: 측정 알고리즘 SOP 재정합

## 배경

Phase 31 에서 신규 측정 타입 7종을 구현했으나, UAT 진행 중 사용자 검증으로
**I9/I10/E2/E9/E10 5종의 알고리즘이 SOP 실무 방식과 불일치**함이 확인됨.
Phase 31 측정 클래스는 MSOP 원문 절차(31-SOP-REFERENCE.md)를 그대로 옮긴 것이나,
실제 현장 구현은 더 단순한 방식을 사용한다.

> **우선순위:** 사용자 확정 실무 알고리즘 > MSOP 원문 절차. 본 문서가 스펙 of record.

### Phase 31 UAT 결과 (이관 기준)

| Test | 타입 | 상태 | Phase 32 영향 |
|------|------|------|---------------|
| 1 | E8 CircleCenterDistance | ✅ PASS | 영향 없음 — 유지 |
| 2 | D1/H5 EdgeToLineAngle | ✅ PASS | 영향 없음 — 유지 |
| 6 | ArcEdgeDistance | ✅ PASS | 영향 없음 — 유지 |
| 9 | BUILD | ✅ PASS | — |
| 3 | I9/I10 ArcLineIntersect | 미수행 | **Phase 32 이관 — 알고리즘 재작성** |
| 4 | E2 CompoundAngle | 미수행 | **Phase 32 이관 — 알고리즘 재작성** |
| 5 | E9/E10 CompoundCenterC·B | 미수행 | **Phase 32 이관 — 알고리즘 재작성** |
| 7 | ROI 버튼 일반화 (CO-23.1-02) | 결함 2건 | #2 수정 완료(85bf2ee) / #1 CompoundAngle 다중 ROI 는 본 phase 재작성으로 해소 |
| 8 | 듀얼 이미지 레이블 (CO-23.1-01) | 미수행 | Phase 31 잔여 (별도) |

## 확정 스펙

### 1. ArcLineIntersect (I9 / I10) — 2직선 교점

**기존(폐기):** Arc_P1/P2/P3 3점 호 피팅 + Line ROI → 호∩라인 교점.

**신규:**
- ROI **2개** (Rect/Point) — 한쪽 수직 에지, 한쪽 수평 에지 (각 ROI Phi 로 방향 지정)
- 각 ROI 에서 기존 직선 알고리즘(`TryFitLine`)으로 직선 추출
- HALCON `intersection_lines` 로 두 직선의 교점 산출
- 교점 → Datum 기준 거리 (`ComputeProjectionDistance` — 기존 유지)
- 타입 명칭 `ArcLineIntersect` **유지** (사용자 결정)
- 기존 헬퍼 `TryFitArc` / `TryIntersectCircleLine` 본 타입 미사용 — 타 사용처 없으면 폐기 검토
- SOP 동일 알고리즘 적용: P4∩P1, P5∩P3, p6∩p9, p8∩p10 (각 측정 = ROI 한 쌍)

### 2. 공통 컨투어 알고리즘 (E2/E3/E9/E10 공유)

ROI **1개** (Rect) 에서 수행 — 사용자 제공 HALCON 스크립트:
```
reduce_domain(image, rectangle, imageReduced)
edges_sub_pix(imageReduced, edges, 'canny', 1, 20, 40)
union_adjacent_contours_xld(edges, unionContours, 700, 1, 'attr_keep')
smallest_rectangle2_xld(unionContours, row, col, phi, len1, len2)
shape_trans_xld(unionContours, rectXld, 'rectangle2')
area_center_xld(rectXld, area, rowC, colC, ptOrder)
tuple_max(area, maxArea) / tuple_find(area, maxArea, maxIdx)   ← 최대 면적 인덱스
select_obj(rectXld, largestRect, maxIdx + 1)                   ← 1-based
smallest_rectangle2_xld(largestRect, centerRow, centerCol, phi, length1, length2)
get_contour_xld(largestRect, rows, cols)                       ← 코너점
```
**산출물:** LargestRect 중심(centerRow/Col), 각도(phi), 장축/단축 길이(length1/length2),
코너점, shape_trans 된 사각형 XLD.

canny 파라미터(alpha=1, low=20, high=40), union 거리(700) 는 **PropertyGrid 사용자
편집 파라미터로 노출** (Q5 확정).

### 3. E2 (CompoundAngle) — 각도 1개

**기존(폐기):** CL1~3 원피팅 + La/Lb 라인 + midline + 교점 체인.

**신규:**
- ROI 1개 → 공통 컨투어 알고리즘 → LargestRect 중심
- **DatumC 티칭 시 검출되는 원(B1 홀)의 중심**을 측정에 주입 (신규 주입 채널 — Q3 (a) 확정)
- 대각선 라인 = LargestRect 중심 ↔ DatumC 검출 원중심
- **결과 = 대각선 라인과 DatumB 기준선 사이의 각도 1개**

### 4. E3 (신규 타입) — 단축 거리

SOP p.50: FAI E3 (Bottom #2), La/Lb 2직선 거리, **공차 0.600±0.030**.
- ROI 1개 → 공통 컨투어 알고리즘 → shape_trans 된 LargestRect
- 단축 거리 = LargestRect 단축 방향(PhiPerp) 선과 사각형의 교점 2개 사이 거리
  (`intersection_contours_xld` — 사용자 스크립트 참조)
- = 사각형의 짧은 변 폭 (SOP La↔Lb 간격과 등가)
- `MeasurementFactory` 신규 등록 필요. TypeName 확정 필요(아래 미해결).

### 5. E9 / E10 (CompoundCenterC / CompoundCenterB) — 거리

**기존(폐기):** CL2/CL3 원 + La/Lb 라인 체인.

**신규:**
- ROI 1개 → 공통 컨투어 알고리즘 → LargestRect 중심
- E9 = LargestRect 중심 → DatumC **X** 거리
- E10 = LargestRect 중심 → DatumB **Y** 거리

## 인프라 변경

- **VisionAlgorithmService**
  - 공통 컨투어 알고리즘 메서드 신설 (LargestRect 산출)
  - `intersection_lines` 래퍼 신설 (2직선 교점)
  - E3 단축 거리용 `intersection_contours_xld` 활용
- **DatumC 검출 원중심 주입 채널** — `IDatumOriginConsumer` 확장 또는 E2 전용 신규 채널.
  DatumConfig(CircleTwoHorizontal) 검출 원중심을 `Action_FAIMeasurement` 가 E2 에 주입.
- **PropertyGrid 파라미터 노출** — canny(alpha/low/high), union 거리 (E2/E3/E9/E10)
- **MeasurementFactory** — E3 신규 타입 등록
- **ROI 티칭 (MainView)**
  - ArcLineIntersect = Rect **2개** 티칭 (다중 ROI — Datum 위저드 패턴 축소 적용 또는 순차 2회 드로잉)
  - E2/E3/E9/E10 = Rect **1개** (기존 단일 ROI 경로 — `FindSelectedRectMeasurement`/`CommitRectRoi`/`BuildPointRoiDefinition` 배선)

## HALCON 함수 참조

함수 상세는 메모리 `halcon_1d_measuring.md` / `halcon_2d_measuring.md` 참조.
주요: `edges_sub_pix`, `union_adjacent_contours_xld`, `smallest_rectangle2_xld`,
`shape_trans_xld`, `area_center_xld`, `select_obj`, `intersection_lines`,
`intersection_contours_xld`, `get_contour_xld`, `reduce_domain`.

## 미해결 / 계획 시 확정 필요

1. **E3 TypeName** 확정 (예: `CompoundShortAxisDistance` 등)
2. **ArcLineIntersect 2 ROI 티칭 UX** — Datum 위저드(EDatumTeachStep) 축소 적용 vs 순차 2회 드로잉
3. **`intersection_lines` 평행/근접** 시 안전 종결 (측정값 '—', 크래시 없음)
4. **컨투어 알고리즘 실패 처리** — union/canny 가 사각형 0개 검출 시 안전 종결
5. **기존 측정 클래스 처리** — ArcLineIntersect/CompoundAngle/CompoundCenterC·B 의
   기존 ROI 필드(Arc_P1~P3, Cl1~3, La/Lb)를 제거할지 — INI 하위호환 불필요(Phase 31 신규 타입)
