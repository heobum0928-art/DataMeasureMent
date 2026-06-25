# 064-04 Summary — ApplyShotLights() 구현

**Status:** completed
**Completed:** 2026-06-25

## Changes
- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs: FindShotByZIndex() + ApplyShotLights() + ApplyShotLightsInternal()

## Mapping
- RingLight → LIGHT_RING (6ch 통합 그룹)
- BackLight → LIGHT_BACK (단일)
- CoaxLight → LIGHT_ALIGN_COAX (단일)
- SideLight → LIGHT_BAR (4ch 통합 그룹)

## Build
msbuild Debug/x64: PASS
