---
status: partial
phase: 53-2026-06-16-poc-2
source: [53-VERIFICATION.md]
started: 2026-06-23
updated: 2026-06-23
---

## Current Test

[awaiting human testing]

## Tests

### 1. 캘리브 창 진입 + SIMUL 라이브 버튼 게이팅
expected: MainView 툴바 [체커보드 캘리브] 버튼 → 창 열림. SIMUL_MODE 빌드에서 [라이브 촬상] 버튼 비활성 + ToolTip 안내. [이미지 로드]만 가능.
result: [pending]

### 2. 이미지 로드 → 검출 → 리포트
expected: 체커보드 이미지(인터넷/실측) 로드 → 우측 뷰어 표시 → 칸 크기(mm) 입력 → [검출] → txt_report 에 1px=N mm + X/Y + 코너수 + 중앙↔외곽 편차% 표시. 코너<12 또는 검출 실패 시 한국어 에러.
result: [pending]

### 3. 왜곡 경고 라벨 (D-05)
expected: 외곽 왜곡이 큰 격자 이미지 → CenterOuterDeviationPct > 임계(1%) 시 lbl_distortionWarn 빨강 표시. 텔레센트릭 정상 이미지는 경고 없음.
result: [pending]

### 4. [적용] 확인 모달 + 일괄 반영 (D-03/D-06)
expected: 검출 전 [적용] 비활성 → 검출 성공 후 활성. [적용] → '활성 시퀀스 [TOP] 전체 SHOT 덮어쓰기' OKCancel 확인 모달 → OK → 'N개 SHOT 적용 + 저장 완료' 표시.
result: [pending]

### 5. 재시작 영속 + 회귀 0
expected: [적용] 후 앱 재시작 → 해당 시퀀스 shot PixelResolution 산출값 유지. 다른 시퀀스 Datum/데이터 소실 0 (SaveRecipe existingFile 보존, 3faa91b). 기존 2점 캘리브 버튼 동작 회귀 0.
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
