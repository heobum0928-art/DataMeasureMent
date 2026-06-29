---
status: partial
phase: 66-ring7-coax-align-2026-06-26
source: [66-VERIFICATION.md]
started: 2026-06-29T00:00:00Z
updated: 2026-06-29T00:00:00Z
---

## Current Test

[BLOCKED — 조명(Ring7/동축) 하드웨어 설치 이후에만 테스트 가능. 설치 전까지 보류. (사용자 확인 2026-06-29)]

## Tests

### 1. 검사 PropertyGrid 동축(Coax) 숨김 확인
expected: 검사 탭 Shot>Light PropertyGrid 에서 Coax(CoaxLight_Enabled/Brightness)가 더 이상 보이지 않는다. Ring/Back/Bar/Ring7 4종만 노출. 기존 레시피 로드/저장 정상(Coax INI 키 보존).
result: [blocked — 조명 설치 대기]

### 2. Ring7 $PREP 점등/소등 대칭 확인
expected: $PREP Op=1 수신 시 Ring7Light_Enabled Shot 에서 RING7 점등 + 밝기 적용. $PREP Op=0(사이클 종료) 수신 시 RING/BACK/ALIGN_COAX/BAR/RING7 5종 전부 소등 — RING7 만 켜진 채 잔존하지 않음.
result: [blocked — 조명 설치 대기]

### 3. Bottom Align 창 동축 복원/즉시적용/저장
expected: 슬롯 ComboBox 전환 시 해당 슬롯 JSON 의 CoaxEnabled/CoaxLevel 이 체크박스/슬라이더에 복원된다. 체크/슬라이더 변경 시 LIGHT_ALIGN_COAX 가 즉시 반응(ON/OFF + 밝기)하고 슬롯 JSON 에 저장된다. 0~255 범위 클램프.
result: [blocked — 조명 설치 대기]

### 4. Tray Align 창 동축 복원
expected: Tray Align 창 진입(Loaded) 시 Tray.json 의 CoaxEnabled/CoaxLevel 이 체크박스/슬라이더에 표시된다. 변경 시 즉시 적용 + Tray.json 저장. 로드 중 연쇄 저장(에러 토스트) 없음.
result: [blocked — 조명 설치 대기]

### 5. Teach/Run/Grab 직전 동축 자동 점등 (티칭 조명 = 런타임 조명, D-07)
expected: Bottom/Tray Align 의 Teach/Run/Grab(실카메라) 직전에 ApplyCoaxLight 가 호출되어, 티칭 시 사용한 동축 조명 상태가 런타임 grab 시점에도 동일하게 적용된다.
result: [blocked — 조명 설치 대기]

## Summary

total: 5
passed: 0
issues: 0
pending: 0
skipped: 0
blocked: 5

## Gaps

- **BLOCKER (hardware):** 5개 항목 전부 Ring7/동축 조명 하드웨어 설치 이후에만 실측 가능. 조명 설치 완료 시 `/gsd-verify-work 66` 으로 재개. (코드/빌드/검증은 완료, 회귀 0.)
