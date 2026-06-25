# 064-01 Summary — LightHandler 8채널 확장

**Status:** completed
**Completed:** 2026-06-25

## Changes
- WPF_Example/Device/LightController/LightHandler.cs: CHANNEL_LIMIT 4→8 (JPF-1208 8CH 대응)
- WPF_Example/Custom/Device/LightHandler.cs: 2대 컨트롤러 + 5그룹 등록 (전면 재작성)
  - Controller A (Index=0): Ring CH1~CH6 + AlignCoax = 7채널
  - Controller B (Index=1): Back + Bar×4 + Ring7 = 6채널
  - LightGroup 5종: RING / BACK / BAR / RING7 / ALIGN_COAX
- WPF_Example/Custom/Sequence/SequenceHandler.cs: defaultLight 3곳 교체
  - LIGHT_TOP → LIGHT_RING (Top 시퀀스)
  - LIGHT_SIDE → LIGHT_BAR  (Side 시퀀스)
  - LIGHT_BOTTOM → LIGHT_BACK (Bottom 시퀀스)
- WPF_Example/Custom/TcpServer/ResourceMap.cs: EResource.Light 6곳 교체
  - InitializeV26: Top→RING, Side→BAR, Bottom→BACK
  - MapPc1Resources: Top→RING, Side(BOTTOM자원)→BACK
  - MapPc2Resources: Top·Side 양쪽→BAR (SIDE 공유)

## Verification
- CHANNEL_LIMIT = 8 확인: WPF_Example/Device/LightController/LightHandler.cs:57
- LIGHT_TOP/LIGHT_SIDE/LIGHT_BOTTOM 잔재 없음: grep 결과 0건
- JPFLightController(0, 7) / JPFLightController(1, 6) 등록 확인

## Build
msbuild Debug/x64: PASS (경고만, 에러 없음)
DatumMeasurement.exe 생성: WPF_Example/bin/x64/Debug/DatumMeasurement.exe
