---
status: partial
phase: 59-vision-algorithm-b-2026-06-23
source: [59-VERIFICATION.md]
started: 2026-06-24
updated: 2026-06-24
---

## Current Test

[awaiting human testing]

## Tests

### 1. Teach 라운드트립 (Tray)
expected: Tray 모드 + ROI 지정으로 `AlignShapeMatchService.TryTeach()` 호출 → `{RecipeSavePath}\{recipe}\ETHERNET_ALIGN\Tray.shm` + `Tray.json`(레퍼런스 포즈) 실제 생성. HALCON create_shape_model + 파일 I/O 동작 확인.
result: [pending]

### 2. Teach 라운드트립 (Bottom) + 템플릿 격리
expected: Bottom 모드 티칭 → `Bottom.shm` + `Bottom.json` 생성. Tray 템플릿과 별도 파일로 격리(서로 덮어쓰지 않음).
result: [pending]

### 3. Run Tray (동일 이미지)
expected: 티칭한 동일 이미지로 `Run(img, Tray)` → `Found=true`, `OffsetXmm/OffsetYmm ≈ 0`, `HasTheta=false`. minScore(0.5) 이상.
result: [pending]

### 4. Run Bottom (이동/회전 이미지) — 부호/축 확정
expected: 일부러 이동·회전한 이미지로 `Run(img, Bottom)` → `OffsetXmm/OffsetYmm` 가 실제 이동량과 일치(mm), `ThetaDeg` 가 회전 각도차, `HasTheta=true`. **Col→OffsetX, Row→OffsetY 부호/축 매핑을 실 장비 좌표계 기준으로 확정**(D-05 가정 검증).
result: [pending]

### 5. 앱 재시작 후 .shm 로드
expected: 티칭 후 앱 재시작 → `HasTemplate(mode)` 가 여전히 true(.shm + .json 디스크 유지), `Run()` 이 재로드한 모델로 정상 매칭.
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps

> 비고: 5건 모두 코드 결함이 아니라 런타임(HALCON 실행 + 파일 I/O + 부호/축 실장비 확정) 확인 항목.
> 코드 레벨 must-have 4/4 검증 완료(59-VERIFICATION.md, human_needed). 빌드 msbuild Debug/x64 PASS, anti-goal(PatternMatchService/RecipeFileHelper/Grabber 무수정) 확인, 코드리뷰 clean(WR-01/02 + IN-01/02 수정).
> Phase 59 는 서비스 API 만 — teach/run 을 구동할 UI 진입점은 Phase 61. 검증자 권고: **Phase 61(TabControl UI) 완성 후 Phase 58+59 UAT 일괄 수행.** 특히 Test 4 의 부호/축 매핑은 실 장비 좌표 기준 확정 필요.
