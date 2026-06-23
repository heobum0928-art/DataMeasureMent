---
status: partial
phase: 58-config-camera-a-2026-06-23
source: [58-VERIFICATION.md]
started: 2026-06-23
updated: 2026-06-23
---

## Current Test

[awaiting human testing]

## Tests

### 1. INI round-trip — [ETHERNET_VISION] 저장·로드 영속성
expected: PropertyGrid(설정 창)에서 EthernetVisionMode/EthernetCameraIp/EthernetExposure/EthernetPixelResolution 값을 변경 → 앱 종료 → `Setting.ini` 의 `[ETHERNET_VISION]` 섹션에 값이 기록됨 → 앱 재시작 후 동일 값으로 로드됨. 특히 int-backing `EthernetVisionModeValue` 가 enum(None/Tray/Bottom) 과 정합되게 저장/복원되는지 확인.
result: [pending]

### 2. 미존재 키 기본값 8.652 복원
expected: `Setting.ini` 에서 `[ETHERNET_VISION]` 섹션(또는 PixelResolution 키)을 삭제 → 앱 재시작 → `EthernetPixelResolution` 이 0 이 아닌 8.652 로 복원됨 (AfterLoad → RestoreEthernetVisionDefault 실제 호출 확인). 기존 PcRole 기본값 복원도 회귀 없이 동작.
result: [pending]

### 3. SIMUL/폴백 Grab — D:\align_test.bmp
expected: EthernetVisionMode=Tray(또는 Bottom) + 실 이더넷 카메라 미연결 상태에서 `EthernetAlignCamera.Grab()` 호출 시 예외 없이 `D:\align_test.bmp` 가 로드된 HImage 반환. (Phase 61 UI 완성 전에는 직접 진입점이 없어 임시 테스트 훅 또는 Phase 61 이후 확인 권장.)
result: [pending]

### 4. Grabber 무영향 E2E — 이더넷 실패해도 검사 정상
expected: 이더넷 카메라 초기화가 실패/미연결인 상태에서 앱 기동 → Grabber(Top/Bottom/Side) 검사 시퀀스가 SIMUL TCP 검사 명령에 정상 응답 (이더넷 init 의 try-catch 격리로 검사 경로 무영향). EthernetVisionMode=None 일 때 연결 시도조차 안 함도 함께 확인.
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps

> 비고: 4건 모두 코드 결함이 아니라 "SIMUL_MODE 환경 + Phase 61 UI 미완성" 으로 인한 런타임 확인 항목.
> 코드 레벨 must-have 4/4 는 검증 완료(58-VERIFICATION.md, status=human_needed). 빌드 msbuild Debug/x64 PASS, anti-goal(Grabber 무수정) 확인, 코드리뷰 clean.
> 검증자 권고: Phase 61(TabControl UI) 완성 후 Tray/Bottom 탭에서 Grab/Live 를 실제 구동하며 1~3 을 일괄 UAT 하는 것이 효율적. 4(Grabber 무영향)는 지금 SIMUL 로도 확인 가능.
