# Phase 32 UAT Aggregated Result

**Phase:** 32-sop-i9-i10-e2-e9-e10-e3
**UAT 일자:** 2026-05-23
**검증자:** 사용자 (시각 검증, SIMUL_MODE)
**결과:** **PASS** — 전 항목 사용자 approved

## 검증 범위

Phase 32 의 8 plans 통합 결과 — 측정 알고리즘 SOP 재정합 (I9/I10/E2/E9/E10 재작성 + E3 신규) + UI 배선 + 오버레이 시각화 + 측정점 보정.

## 검증 항목 + 결과

### 1. ROI 티칭 UX

| 측정 타입 | ROI 개수 | 드로잉 방식 | 결과 |
|-----------|---------|-----------|------|
| ArcLineIntersect (I9/I10) | 4 (EdgeA1/EdgeB1/EdgeA2/EdgeB2) | 순차 4회 | ✅ PASS |
| E2 CompoundCenterC | 1 (Rect) | 단일 드로잉 | ✅ PASS |
| E3 CompoundShortAxisDistance | 1 (Rect) | 단일 드로잉 | ✅ PASS |
| E9 CompoundCenterB | 1 (Rect) | 단일 드로잉 | ✅ PASS |
| E10 CompoundAngle | 1 (Rect) | 단일 드로잉 | ✅ PASS |

### 2. 측정값 + 공차 판정

| 측정 타입 | 결과값 의미 | UAT 결과 |
|-----------|------------|---------|
| ArcLineIntersect | 측정점 → Datum C X 거리(mm), 측정축=교점2 + 수직축=두 교점 평균 (32-08 commit 30c478d 측정점 보정 검증) | ✅ PASS |
| E3 (CompoundShortAxisDistance) | 단축 폭(mm) = 두 교점 거리 (quick 260523-j72 af07972 reference 알고리즘), 공차 0.600±0.030 | ✅ PASS |
| E2 (CompoundCenterC) | Datum C ↔ rect center 거리(mm) | ✅ PASS |
| E9 (CompoundCenterB) | Datum B ↔ rect center 거리(mm) | ✅ PASS |
| E10 (CompoundAngle) | rect 장축 각도(deg) | ✅ PASS |

### 3. Overlay 시각화

**E3 (af07972 reference 알고리즘 신규 6 오버레이):**
- ✅ FAI-LongEdge1 / FAI-LongEdge2 — LargestRect XLD 코너 직접 (분석식 ±length2 가 아닌 contour 좌표)
- ✅ FAI-MeasureLine — 중심 통과 phi+π/2 측정선 (CrossLen=500)
- ✅ FAI-Intersection1 / FAI-Intersection2 — intersection_contours_xld 결과 (subpixel)
- ✅ FAI-DistLine — 교점간 거리선

**ArcLineIntersect (32-07 + 32-08 통합):**
- ✅ FAI-Edge1/2/3/4 — 4개 ROI fit 결과 라인
- ✅ FAI-Intersection1/2 — 교점 2개
- ✅ FAI-AvgPoint — 측정축=교점2 + 수직축=평균 보정 측정점
- ✅ FAI-DistLine — 측정점 → 수선의 발

**E2 / E9 / E10 (32-07 overlay):**
- ✅ 정상 렌더

### 4. 실패 케이스 (T-32-14/T-32-15 mitigation 검증)

| 시나리오 | 기대 동작 | UAT 결과 |
|----------|----------|---------|
| 빈 영역 ROI | 측정값 '—' + 앱 무크래시 | ✅ PASS |
| 평행/근접 직선 | 측정값 '—' + overlay 빈 리스트 | ✅ PASS |
| 노이즈 이미지 | 측정값 '—' + 앱 무크래시 | ✅ PASS |

## UAT 중 발견된 결함 + 즉시 수정 (32-06 hotfix 4건)

| 결함 | Root Cause | Fix Commit |
|------|-----------|-----------|
| InspectionList Rect 버튼 비활성화 (E3) | 화이트리스트에 E3 누락 | 15fa8d8 |
| Shot 노드 이미지 로드 시 FAI/Measurement 선택 시 이미지 회귀 | ShotConfig._image 버퍼 미동기화 | 9c482dd |
| Measurement/Shot 노드 선택 시 캔버스 이미지 미갱신 | 소유 Shot 이미지 재표시 누락 | 4ea5bcc |
| Measurement 노드 ROI 미표시 | CommitRectRoi 캔버스 갱신을 Shot 단위 ROI 수집으로 교체 | 88b5e05 |
| ArcLineIntersect EdgeB ROI 캔버스 렌더 누락 | BuildPointRoiDefinitions 다중 ROI 반환 미구현 | 3c9a573 |

## UAT 직전 알고리즘 업그레이드 (quick 260523-j72)

E3 (CompoundShortAxisDistance) — 사용자 reference HALCON 스크립트 검토 결과 분석식 알고리즘을 contour 기반으로 교체:
- `2 * max(length1, length2)` 스칼라 → 명시 교점 거리 (b3dd847, c95982d)
- 분석식 line-line intersection → HALCON `intersection_contours_xld(measureLine, LargestRect, 'all')` (af07972)
- fit_line_contour_xld('tukey') 로 refined Phi 산출
- CrossLen 프로퍼티 신규 (기본 500.0, PropertyGrid Measure 카테고리)

3개 신규 commit (b3dd847 / c95982d / af07972) 모두 UAT 에 포함됨.

## 결론

Phase 32 의 SOP 재정합 목표 (I9/I10/E2/E9/E10 재작성 + E3 신규) 완료. 후속 작업 없음.

**다음 단계:** Phase 32 verify_phase_goal → Phase 32 SIGNED_OFF 처리 → 다음 Phase 진행.
