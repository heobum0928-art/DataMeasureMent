---
phase: quick-260517-ijg
plan: "01"
subsystem: VisionAlgorithmService
tags: [halcon, edge-measurement, strip-loop, CO-23-01]
dependency_graph:
  requires: [DatumFindingService.TryFindLine (CANONICAL 레퍼런스)]
  provides: [TryFitLine strip-loop 구현, AppendStrip private 헬퍼]
  affects: [EdgeToLineDistanceMeasurement, PointToLineDistanceMeasurement, PointToPointDistanceMeasurement, LineToLineAngleMeasurement, LineToLineDistanceMeasurement]
tech_stack:
  added: []
  patterns: [strip-loop MeasurePos 누적, SmallestRectangle2 per-strip center, FitLineContourXld tukey]
key_files:
  modified:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
decisions:
  - "AppendStrip 헬퍼를 VisionAlgorithmService 내부 private 으로 작성 (옵션 a: 단순함 우선, DatumFindingService 무수정 원칙 준수)"
  - "rPhi 회전은 strip region 회전 대신 measurePhi 로 흡수 (축 정렬 strip + 회전된 측정 축)"
  - "polarity CANONICAL clamp 미도입 — LightToDark→negative 기존 매핑 유지 (caller 규약 차이로 회귀 위험)"
metrics:
  started: "2026-05-17"
  completed: "2026-05-17"
  task1_commit: a14f229
---

# Quick 260517-ijg: TryFitLine strip-loop MeasurePos 누적 재작성 (CO-23-01)

**한 줄 요약:** VisionAlgorithmService.TryFitLine 을 단일 MeasurePos 1회 → stripCount(기본 20) 회 strip-loop MeasurePos 누적으로 재작성하여 "insufficient edge points (1)" 오류 구조적 차단.

## Objective

UAT 에서 발견된 EdgeToLineDistance / PointToLine / PointToPoint / LineToLineAngle / LineToLineDistance 측정값 '—' 미표시의 구조적 원인을 제거한다 (CO-23-01 #2).

근본 원인: 기존 `TryFitLine` 은 ROI 전체에 대해 `GenMeasureRectangle2 + MeasurePos` 를 **단 1회** 호출했다. 단일 MeasurePos 는 측정 축 1개를 따라서만 에지를 반환하므로 `FitLineContourXld` 입력이 1~2점(collinear) → "insufficient edge points (1)" → `TryFitLine` false → 측정값 '—'.

## Task 1: TryFitLine strip-loop 재작성 — COMPLETED (commit a14f229)

### 변경 내용

**교체 (L88~127 단일 MeasurePos 블록 → strip-loop):**
- bounding box 산출: `rRow/rCol` 중심의 축 정렬 박스 (halfW=roiLength1, halfH=roiLength2)
- `stripCount` 산출: sampleCount > 0 이면 사용, 아니면 기본 20 (CANONICAL 패턴 동일)
- `scanHorizontal` 분기: TtoB/BtoT → col 분할, 그 외 → row 분할
- strip-loop: `AppendStrip` 헬퍼 stripCount 회 호출 + `allRows/allCols` 누적
- `trimCount` 적용: 누적 후 양 끝 trimCount 개 제거 (CANONICAL 패턴 동일)
- edge 개수 게이트: edgeCount < 2 이면 `"insufficient edge points (N) across M strips"` 반환
- `GenContourPolygonXld(allRows, allCols) + FitLineContourXld` 로 직선 피팅

**보존 (무수정):**
- 진입부 null 가드 (L29-36)
- datumTransform 변환 블록 + `rPhi` 산출 (L42-59)
- `image.GetImageSize` (L61-62)
- direction → `measurePhi` 4-way 매핑 + `measurePhi += (rPhi - roiPhi)` 회전 보정 (L64-75)
- polarity → `pol` 매핑 ("LightToDark"→"negative", else→"positive") (L78-86)
- selection → `measureSel` 3분기 (L87-100, Phase 23 ALG-01 기존 코드)
- TryFitLine 시그니처 (무변경 → 5개 caller 무수정 컴파일 통과 검증됨)

**추가:**
- `private void AppendStrip(...)` 헬퍼: CANONICAL AppendEdgePointsFromStrip 동등 구현
  - GenRectangle1 → SmallestRectangle2(rr/rc/rh/rw 추출, rp 미사용) → GenMeasureRectangle2(measurePhi) → MeasurePos → TupleConcat
  - strip 실패(빈 결과 / 예외) swallow
  - 헬퍼 위치: VisionAlgorithmService 내부 private (옵션 a)

**제거:**
- 메서드 레벨 `HTuple measureHandle = null;` 변수 (strip 헬퍼가 strip별 관리)
- `finally` 블록의 `measureHandle` 해제 라인

### 코드 마커

수정/추가 모든 라인에 `//260517 hbk` 마커 부여. 기존 `//260413 hbk`, `//260509 hbk Phase 20`, `//260512 hbk Phase 23 ALG-01` 마커 전량 보존 (Phase 20 D-12 stacking 패턴).

### 빌드 결과

```
msbuild Debug/x64 Rebuild
  0 errors
  신규 warning 0 (기존 2건 유지: MSB3884 ruleset + CS0162 VirtualCamera.cs — Phase 21 baseline)
  DatumMeasurement.exe 생성 완료
```

## Task 2: SIMUL_MODE 측정값 표시 육안 검증 — AWAITING

사용자가 Debug/x64 SIMUL_MODE 에서 EdgeToLineDistance 계열 측정 항목을 실행하여 측정값이 '—' → mm 숫자로 전환되는지 확인이 필요합니다.

**검증 절차:**
1. Debug/x64 SIMUL_MODE 로 DatumMeasurement.exe 실행
2. EdgeToLineDistance (또는 PointToLine/LineToLine 계열) 측정 항목이 포함된 레시피 로드
3. Datum 티칭/검증이 정상인 SHOT 의 검사 1회 실행
4. 검사 결과 그리드에서 측정값 컬럼 확인:
   - 기대: mm 숫자 표시 (더 이상 '—' 아님)
   - 기대: Trace 로그에 "insufficient edge points (1)" 오류 없음
5. (회귀 확인) Datum 보정 적용된 측정값이 이전 대비 급격한 부호/스케일 변화 없음

## Deviations from Plan

없음 — 계획대로 정확히 실행됨.

## Known Stubs

없음.

## Threat Flags

없음 — 기존 메서드 내부 로직 교체만 수행. 새로운 네트워크 엔드포인트, 파일 접근, 스키마 변경 없음.

## Self-Check

- [x] `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` 수정됨 — FOUND
- [x] 커밋 a14f229 존재 — FOUND
- [x] `TryFitLine` 시그니처 무변경 — 검증됨 (기존 선언 L18-27 byte-identical)
- [x] `AppendStrip` private 헬퍼 존재 — FOUND (L193)
- [x] strip-loop 구조: stripCount 루프 + AppendStrip 호출 + allRows/allCols 누적 — FOUND (L125-146)
- [x] datumTransform 변환 + rPhi 회전 보정 보존 — FOUND (L43-58, L74)
- [x] `//260517 hbk` 마커 부여, 기존 hbk 마커 보존 — VERIFIED

## Self-Check: PASSED
