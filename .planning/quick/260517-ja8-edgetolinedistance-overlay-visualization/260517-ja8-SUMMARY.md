---
phase: quick-260517-ja8
plan: 01
subsystem: measurement-overlay
tags: [overlay, visualization, edge-detection, datum-transform, halcon]
dependency_graph:
  requires: [EdgeToLineDistanceMeasurement.TryExecute, VisionAlgorithmService.TryFitLine, datumTransform]
  provides: [FAI-Edge1 overlay, FAI-DistLine overlay, canvas visualization for EdgeToLineDistance]
  affects: [HalconDisplayService (render), Action_FAIMeasurement (suffix 부여), overlayAcc]
tech_stack:
  added: []
  patterns: [FAIEdgeMeasurementService.BuildOverlaysSingle canonical, HomMat2dInvert+AffineTransPoint2d datum origin, originOk guard pattern]
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
decisions:
  - "Phase 7-01 D-03 / Phase 23-01 의 의도적 빈 리스트 overlay 정책을 뒤집음 — Phase 23.1 UAT 시각 검증 목적"
  - "datumTransform 역변환 실패 시 FAI-DistLine skip, FAI-Edge1 + 측정값은 유지 (originOk guard)"
  - "교점 image 좌표 = HomMat2dInvert(datumTransform) → AffineTransPoint2d(0,0) — DatumConfig.DetectedOriginRow/Col 미사용 (transient 필드, populate 보장 없음)"
metrics:
  duration: ~15min
  completed_date: "2026-05-17"
  tasks_total: 2
  tasks_completed: 1
  tasks_awaiting: 1 (Task 2 checkpoint:human-verify)
---

# Quick 260517-ja8: EdgeToLineDistance Overlay 시각화 Summary

**One-liner:** TryExecute 측정 성공 경로에서 검출 에지 라인(FAI-Edge1) + Datum 원점→에지 중점 Y거리 선(FAI-DistLine) overlay 2개를 캔버스에 채움.

---

## Task 1: TryExecute 측정 성공 경로에서 overlay 채우기 — COMPLETE

**커밋:** `5c3d36b`
**파일:** `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` (1파일만 수정)

### 구현 내용

`TryExecute` 의 `resultValue = -datumRow * pixelResolution;` 직후, `return true;` 직전에 overlay 채우기 블록 삽입.

**FAI-Edge1 (검출 에지 라인):**
- `RoiId = "FAI-Edge1"` — `HalconDisplayService` 의 `StartsWith("FAI-Edge")` 분기 충족 → 판정 OK 시 녹색 / NG 시 적색
- `LineRow1/Col1/Row2/Col2 = pr1/pc1/pr2/pc2` — `TryFitLine` 반환 image 좌표 (역변환 불필요, coordinate_facts 1)
- `Points` 에 에지 중점 `(pRow, pCol)` 1개 → X자 마커

**FAI-DistLine (Y거리 선):**
- 교점(Datum 원점) image 좌표 = `HomMat2dInvert(datumTransform)` → `AffineTransPoint2d(invMat, 0.0, 0.0)` (coordinate_facts 2)
- `originOk` 플래그로 역변환 실패 시 `FAI-DistLine` skip — `FAI-Edge1` + 측정값 보존
- `RoiId = "FAI-DistLine"` → `HalconDisplayService.cs:181` cyan(청록) 분기, suffix 미부여
- `Points` 에 양 끝점(교점, 에지 중점) 2개 → X자 마커

### 기존 정책 뒤집기 근거

Phase 7-01 D-03 / Phase 23-01 에서 `EdgeToLineDistanceMeasurement` 의 overlay 를 의도적으로 빈 리스트로 설계했다 (다른 5종 measurement 도 동일). 이번에 뒤집은 이유:
- **Phase 23.1 UAT 목적:** 측정값(mm) 숫자만으로는 ROI 가 올바른 에지를 잡았는지 확인 불가. 캔버스에 검출 에지/거리선을 표시해야 SOP 도면 정확도 대조 가능.
- 기존 렌더 인프라(`HalconDisplayService`, `Action_FAIMeasurement` overlay 누적)를 그대로 사용하므로 코드 변경은 1파일뿐.

### 실패 경로 보존

- `datumTransform == null/empty` 가드(L70-74) → `return false` → overlays 빈 리스트 유지 (무수정)
- `TryFitLine false` 분기(L92-94) → `return false` → overlays 빈 리스트 유지 (무수정)

### 빌드 결과

```
msbuild Debug/x64 — PASS
Errors:   0
Warnings: 1 (MSB3884 MinimumRecommendedRules.ruleset — Phase 21 baseline, 신규 0)
Output:   DatumMeasurement.exe
```

### git diff 범위

`WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` 1파일만 수정.
다른 measurement 5종(EdgePair/PointToLine/PointToPoint/LineToLineAngle/LineToLineDistance) 무수정.

---

## Task 2: SIMUL UAT 시각 확인 — AWAITING (checkpoint:human-verify)

검출 에지 라인(녹/적) + Y거리 선(청록)이 캔버스에 표시되는지 SIMUL_MODE 육안 검증 필요.

---

## Deviations from Plan

없음 — 플랜 그대로 실행.

---

## Known Stubs

없음.

---

## Self-Check

- [x] `EdgeToLineDistanceMeasurement.cs` 수정 존재
- [x] 커밋 `5c3d36b` 존재
- [x] `overlays.Add` 2건 (FAI-Edge1, FAI-DistLine) 코드에 존재
- [x] TryExecute/TryFitLine 시그니처 무변경
- [x] msbuild Debug/x64 0 errors

## Self-Check: PASSED
