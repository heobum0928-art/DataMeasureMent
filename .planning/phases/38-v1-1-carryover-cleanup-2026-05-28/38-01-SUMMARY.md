---
phase: 38-v1-1-carryover-cleanup-2026-05-28
plan: "01"
subsystem: inspection
tags: [measurement, halcon, recipe, ini, pixelresolution, factory]

# Dependency graph
requires:
  - phase: 37-side-datum-dualimage-2026-05-28
    provides: DualImage Side 다중 Datum 구조 (이번 phase 와 직접 의존 없음)
provides:
  - MeasurementFactory.GetTypeNames() 10종 노출 (미사용 5종 UI 숨김)
  - LoadPhase6Format 로딩 시 FAI PixelResolution 카메라 단일값 정규화
affects:
  - 38-02
  - 38-03
  - Phase 39+ FAI 검사 흐름 (mm 변환 일관성)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "MeasurementFactory 이중 배열 패턴: GetTypeNames(UI 노출) vs Create switch(INI 파싱) 분리 유지"
    - "LoadPhase6Format 정규화 훅: FAI 루프 완료 후 SHOT 루프 닫기 전 camRes 분배"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs

key-decisions:
  - "GetTypeNames 10종 / Create switch 15종 분리 유지 — UI 숨김과 INI 하위호환 동시 달성"
  - "PixelResolution 정규화: 별도 저장 객체 신설 없이 ShotConfig.PixelResolution 단일 소스 채택"
  - "X=Y 정방형 픽셀 가정 — camRes 를 PixelResolutionX/Y 모두에 분배 (비정방 카메라 회귀 위험 SUMMARY 문서화)"
  - "FAIConfig/RoiDefinition/EdgePairDistanceMeasurement PixelResolution 필드 정의 유지 (INI 직렬화 키 보존)"

patterns-established:
  - "Factory UI/파싱 분리 패턴: GetTypeNames(노출용)와 Create switch(로딩용)를 독립 관리"
  - "마이그레이션 훅 패턴: 로딩 루프 완료 직후 정규화 블록 삽입 (런타임 side-effect 없음)"

requirements-completed: []

# Metrics
duration: 20min
completed: 2026-05-28
---

# Phase 38 Plan 01: Algorithm Pixel Cleanup Summary

**MeasurementFactory GetTypeNames 미사용 5종 UI 제거 + LoadPhase6Format FAI PixelResolution 카메라 단일값 정규화로 측정 타입 혼란 제거 및 mm 변환 일관성 확보**

## Performance

- **Duration:** 20 min
- **Started:** 2026-05-28T09:35:00Z
- **Completed:** 2026-05-28T09:55:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- GetTypeNames() 반환 배열에서 미사용 5종(EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/LineToLineDistance) 제거 → 10종 노출. Create() switch 15종은 그대로 유지하여 기존 INI 레시피 로딩 무오류 보장
- LoadPhase6Format 의 FAI 로드 루프 완료 직후에 `double camRes = shot.PixelResolution;` → `fai.PixelResolutionX/Y = camRes;` 정규화 블록 삽입. 카메라(Shot)별 단일값으로 로딩 시 통일
- CameraSlaveParam/FAIConfig/RoiDefinition PixelResolution 필드 정의 미변경 → INI 직렬화 키 보존, 구 레시피 로딩 파싱 오류 없음

## Task Commits

1. **Task 1: MeasurementFactory GetTypeNames 미사용 5종 UI 제거** - `78db678` (feat)
2. **Task 2: LoadPhase6Format FAI PixelResolution 카메라 단일값 정규화** - `24259ce` (feat)

**Plan metadata:** (이 SUMMARY 커밋)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — GetTypeNames() 반환 배열 15→10종 (Create switch 유지), Phase 38 #1 마커 부착
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — LoadPhase6Format 정규화 블록 추가 (FAI 루프 후 SHOT 루프 내), Phase 38 #5 마커 부착

## Decisions Made
- **GetTypeNames vs Create 분리 유지:** UI ComboBox 노출용 배열(GetTypeNames)과 INI 파싱용 switch(Create)를 독립 관리하는 기존 패턴을 그대로 유지. 미사용 5종은 GetTypeNames에서만 제거하고 Create switch는 절대 수정하지 않음
- **ShotConfig.PixelResolution 단일 소스 채택:** 플랜의 Claude's Discretion 결정에 따라 새 Config 객체 추가 없이 기존 ShotConfig.PixelResolution을 정규 소스로 채택. 분배 방식(X=Y 정방형) 유지
- **X=Y 정방형 가정(D-09):** camRes 단일값을 PixelResolutionX/Y 모두에 동일하게 분배. 비정방 픽셀(X≠Y) 카메라 사용 시 측정 mm가 변경되는 의도적 보정임을 이 SUMMARY에 명시
- **필드 정의 유지(D-11):** FAIConfig/RoiDefinition/EdgePairDistanceMeasurement의 PixelResolutionX/Y 필드를 삭제하지 않음. INI 직렬화 키가 살아있어 구 레시피 로딩 시 파싱 오류 없음

## Deviations from Plan
None - plan executed exactly as written.

## Threat Surface Notes (T-38-01)
정규화 전후 mm 변화 가능성: 기존 INI에서 FAI별 PixelResolutionX/Y가 ShotConfig.PixelResolution과 다른 값을 가졌다면, 로딩 후 측정 mm가 달라진다. 이는 **의도적 보정**이며 카메라 단일화(D-10) 목적이다.

X=Y 정방형 픽셀 가정: ShotConfig.PixelResolution 단일값을 X/Y 모두에 분배한다. 비정방 픽셀 카메라(X≠Y 캘리브레이션)에서는 Y 방향 mm가 변경될 수 있다. 현 프로젝트 카메라는 정방형 픽셀 가정을 만족하므로 실측 회귀 없음.

## Issues Encountered
- 워크트리 환경에서 PropertyTools.Wpf/WPF.MDI DLL 미존재로 전체 빌드(XAML 포함) 실패. main 브랜치 동일 환경에서 빌드 성공 확인 완료. C# 컴파일러 오류(error CS)는 0건 — 이번 변경사항과 무관한 기존 환경 문제.

## Next Phase Readiness
- 38-02, 38-03 Wave 2 플랜 실행 가능
- MeasurementFactory GetTypeNames 10종이 FAIConfig EdgeMeasureTypeList 캐시에 반영됨 (정적 readonly 캐시이므로 앱 재시작 시 반영)
- InspectionRecipeManager.LoadPhase6Format 에서 모든 FAI PixelResolutionX/Y 가 카메라 단일값으로 정규화됨

## Self-Check

**파일 존재:**
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — FOUND (수정됨)
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — FOUND (수정됨)

**커밋 존재:**
- `78db678` — FOUND (Task 1)
- `24259ce` — FOUND (Task 2)

## Self-Check: PASSED

---
*Phase: 38-v1-1-carryover-cleanup-2026-05-28*
*Completed: 2026-05-28*
