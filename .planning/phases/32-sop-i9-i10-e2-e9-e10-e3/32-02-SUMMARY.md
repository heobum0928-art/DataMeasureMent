---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "02"
subsystem: Measurement
tags: [interface, datum, injection-channel, IDatumOriginConsumer]
dependency_graph:
  requires:
    - 32-01 (VisionAlgorithmService 헬퍼 3종)
  provides:
    - IDatumOriginConsumer DatumDetectedCircleRow/Col 주입 채널
    - DatumConfig.DetectedCircleRow/Col transient 필드
    - DatumFindingService CircleTwoHorizontal 원중심 write-back
  affects:
    - 32-03 (ArcLineIntersectDistanceMeasurement 재작성)
    - 32-04 (CompoundAngleMeasurement 재작성 — E2 실사용)
tech_stack:
  added: []
  patterns:
    - IDatumOriginConsumer 인터페이스 확장 (DatumAngle2Rad Phase 31 hotfix#3 패턴 동일)
    - 3중 attribute transient 필드 (Browsable×2 + JsonIgnore)
    - Action_FAIMeasurement dc!=null/else 양 분기 주입
key_files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/IDatumOriginConsumer.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs
decisions:
  - "재작성 대상 4종(ArcLineIntersect/CompoundAngle/CenterC/CenterB)에 stub 구현 추가 — Plan 에서는 Wave 2 해소 예정이었으나 즉시 빌드 차단(CS0535)이 발생하여 Rule 3 auto-fix 적용"
metrics:
  duration_minutes: 15
  completed_date: "2026-05-21"
  tasks: 3
  files_modified: 12
---

# Phase 32 Plan 02: DatumC 검출 원중심 주입 채널 신설 Summary

**한 줄 요약:** IDatumOriginConsumer에 DatumDetectedCircleRow/Col 2 프로퍼티를 추가하고, DatumConfig transient 필드 + DatumFindingService write-back + Action_FAIMeasurement 주입 블록을 완성하여 E2 CompoundAngle 재작성이 DatumC 검출 원중심을 주입받을 수 있는 채널을 구축했다.

## 완료된 작업

| Task | 이름 | 커밋 | 핵심 변경 |
|------|------|------|-----------|
| 1 | IDatumOriginConsumer 인터페이스 확장 | 39ec4de | DatumDetectedCircleRow/Col 2 프로퍼티 추가 |
| 2 | DatumConfig transient 필드 + write-back | 8832b9b | DetectedCircleRow/Col + DatumFindingService CTH write-back |
| 3 | Action_FAIMeasurement 주입 + 클래스 8종 구현 | 4e78275 | 양 분기 주입 2줄 + 측정 클래스 8종 인터페이스 구현 |

## 핵심 결정

- **재작성 4종 stub 추가 (Rule 3 auto-fix):** Plan에서는 ArcLineIntersect/CompoundAngle/CompoundCenterC/CompoundCenterB 4종을 Plan 03/04 재작성 시 함께 구현하도록 예정했으나, 빌드 실행 시 CS0535(인터페이스 멤버 미구현) 8건이 발생하여 즉시 빌드를 차단했다. Rule 3(블로킹 이슈 auto-fix)에 따라 stub 구현을 추가했다. 이 4종의 stub은 Plan 03/04 재작성 시 실제 구현으로 교체된다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] CS0535 빌드 차단 — 재작성 4종 stub 추가**
- **Found during:** Task 3 빌드 검증
- **Issue:** IDatumOriginConsumer 확장으로 ArcLineIntersectDistanceMeasurement / CompoundAngleMeasurement / CompoundCenterCDistanceMeasurement / CompoundCenterBDistanceMeasurement 4종에서 CS0535 오류 8건 발생 — 빌드 차단
- **Fix:** 4종에 3중 attribute + DatumDetectedCircleRow/Col stub 구현 추가 (TryExecute 본문 미수정 — Plan 03/04 재작성 시 교체 예정)
- **Files modified:** ArcLineIntersectDistanceMeasurement.cs, CompoundAngleMeasurement.cs, CompoundCenterCDistanceMeasurement.cs, CompoundCenterBDistanceMeasurement.cs
- **Commit:** 4e78275 (Task 3와 단일 커밋 통합)

## 빌드 결과

- msbuild Debug/x64: **error 0**, warning 2건 (MSB3884 ruleset 미존재, CS0162 unreachable code) — 기존 경고, Phase 32 신규 없음

## Known Stubs

| 파일 | 내용 | 이유 |
|------|------|------|
| ArcLineIntersectDistanceMeasurement.cs | DatumDetectedCircleRow/Col stub | Plan 03 전면 재작성 예정 |
| CompoundAngleMeasurement.cs | DatumDetectedCircleRow/Col stub | Plan 04 전면 재작성 예정 (E2 실사용 채널) |
| CompoundCenterCDistanceMeasurement.cs | DatumDetectedCircleRow/Col stub | Plan 04 전면 재작성 예정 |
| CompoundCenterBDistanceMeasurement.cs | DatumDetectedCircleRow/Col stub | Plan 04 전면 재작성 예정 |

## 주입 채널 흐름

```
DatumFindingService.TryFindCircleTwoHorizontal
  → config.DetectedCircleRow/Col = centerRow/centerCol
    → Action_FAIMeasurement.EStep.Measure
        → consumer.DatumDetectedCircleRow/Col = dc.DetectedCircleRow/Col
          → CompoundAngleMeasurement.TryExecute (Plan 04)
              → 대각선 라인 계산 (LargestRect 중심 ↔ DatumC 검출 원중심)
```

## Self-Check

파일 존재 확인:
- IDatumOriginConsumer.cs: FOUND (39ec4de)
- DatumConfig.cs: FOUND (8832b9b)
- DatumFindingService.cs: FOUND (8832b9b)
- Action_FAIMeasurement.cs: FOUND (4e78275)

커밋 존재 확인:
- 39ec4de: FOUND
- 8832b9b: FOUND
- 4e78275: FOUND

## Self-Check: PASSED
