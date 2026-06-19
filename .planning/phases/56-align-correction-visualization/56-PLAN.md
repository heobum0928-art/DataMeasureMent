---
phase: 56
slug: align-correction-visualization
title: "보정 ROI/Datum 시각화 (Phase 54 carry-over #2)"
date: 2026-06-19
status: in-progress
---

# Phase 56 — 보정 ROI/Datum 시각화

## 배경 / 요청
정렬(2-패턴) 검사 후 화면에서 **보정이 적용된 위치를 눈으로 확인**하고 싶음:
1. 측정 ROI — 티칭 원위치로 그려져 보정 확인 어려움
2. Datum 검출 ROI(원/사각 검색박스) — 동일
3. Datum 수평/수직선 — 아예 안 보임

검출 datum **origin(십자+각도화살표)은 이미 보정 표시됨**(RenderDatumFindResult, LastFindSucceeded). 측정 마커(LastOverlays)도 보정. 미보정 = ROI 검색박스(티칭 좌표) + datum 기준선 부재.

## 토글/색 (사용자 미지정 → 기본)
기존 오버레이 토글·색 규약에 얹음(Phase 40 방식). 색은 HALCON 유효명만(비표준명→SetColor 예외→silent 미표시 함정, [[feedback_halcon_setcolor_invalid_names]]).

## Wave 1 ✅ 완료 (8e8ea29)
**Datum 수평/수직 기준선 표시** — RenderDatumFindResult 에 추가. 검출(보정) origin 통과, DetectedRefAngle(수평)/DetectedRefAngle2(수직, 설정 시) 방향. 방향규약 = EdgeToLineDistance 측정축과 동일 (sinθ,cosθ). cyan 1px ±400px. 빌드 PASS. → 요청 #3 해소.

## Wave 2 — 측정/Datum ROI 보정 위치 박스 (남음)
**핵심**: RoiDefinition 은 축정렬 코너(Row1/Col1/Row2/Col2) or 원(center+radius). 회전 보정박스 = **4 코너를 datum transform 으로 변환 → PolygonPoints** (Render 가 이미 Polygon 렌더 지원: HalconDisplayService:103-110 + RenderPolygon + ParsePolygonPoints).

접근:
- ROI 수집부(`GetCurrentFAIRois`:285 / `CollectShotRois`:426 / `BuildPointRoiDefinitions`:324)에서 각 측정 ROI 의 **참조 datum(meas.DatumRef → DatumConfig.CurrentTransform)** 조회.
- CurrentTransform 있고 align 활성 시: 4 코너(또는 원 둘레 점들)를 `AffineTransPoint2d(transform, ...)` 변환 → PolygonPoints 문자열 → RoiDefinition.PolygonPoints 세팅.
- 미보정/transform 없음 → 기존 축정렬 박스(회귀 0).
- Datum 검출 ROI(원/사각)도 동일 변환.

주의(회귀): GetCurrentFAIRois/CollectShotRois/HighlightSelectedRoi 가 ROI 하이라이트(노란색)·이동(드래그) 과 공유 → 보정 변환은 **표시 전용 분기**로 격리, 편집/이동 경로는 티칭 좌표 유지. 다중 호출처(:861/905/950/2015/2096) 회귀 점검.

미정: 원 ROI 의 회전 표현(원은 회전 불변이라 center 만 변환 → 위치만 이동, 형태 동일). Polygon 변환은 rect/point ROI 중심.

## UAT
1. (Wave 1) tilt 검사 후 datum 수평/수직선이 검출 origin 통과해 기울어져 보이는지.
2. (Wave 2) 측정/Datum ROI 박스가 부품 틸트만큼 회전·이동해 실제 측정 위치에 겹치는지.
3. 비-align datum / 미보정 → 기존 표시 그대로(회귀 0).

## 환경
빌드 VS2022 Debug/x64. HALCON 색상명 유효성 주의. 하위에이전트 차단 → 인라인.
