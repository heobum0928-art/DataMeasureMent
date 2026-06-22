---
status: testing
phase: 57-pattern-roi-ux-datum-align-hardening
source: [57-01-SUMMARY.md, 57-02-SUMMARY.md, 57-03-SUMMARY.md, 57-04-SUMMARY.md, 57-05-SUMMARY.md]
started: 2026-06-21T23:52:47Z
updated: 2026-06-21T23:55:00Z
---

## Current Test
<!-- OVERWRITE each test - shows where we are -->

number: 2
name: Side DualImage align 정확도 (#4)
expected: |
  텔레센트릭 Side VerticalTwoHorizontalDualImage 레시피를 align 활성화로 검사.
  가로축 패턴매칭 단일 transform 이 가로(Horizontal_A/B)+세로(Vertical) 검출 ROI
  모두에 적용되어 DetectedOrigin/기준선이 정확히 검출됨.
awaiting: user response

## Tests

### 1. leveling 제거 off 회귀 (#6)
expected: stale leveling 키(LevelingEnabled/IsLevelingReference)가 남아있는 기존 레시피 INI 로드 시 크래시 없음, leveling 미사용 동작 변화 없음, 측정 흐름 MoveZ→DatumPhase 정상 진행
result: pass
note: leveling 키(LevelingEnabled/IsLevelingReference) 레시피에 존재하지 않음 — 로드 정상, 회귀 없음

### 2. Side DualImage align 정확도 (#4)
expected: 텔레센트릭 Side VerticalTwoHorizontalDualImage 레시피를 align 활성화로 검사. 가로축 패턴매칭 단일 transform 이 가로(Horizontal_A/B) + 세로(Vertical) 검출 ROI 모두에 적용되어 DetectedOrigin/기준선이 정확히 검출됨
result: [pending]

### 3. DualImage align 실패 lenient NG (#4/#5)
expected: DualImage datum 에 패턴매칭 실패를 강제 주입. abort 없이 측정 진행되고, 해당 측정이 ALIGN_FAIL 로 NG 처리되어 Excel/UI 결과에 표시됨
result: [pending]

### 4. Datum 기준선 slate blue 시각 (#3)
expected: 대이미지(14208px) datum 검출 결과 화면에서 기준선이 magenta 가 아닌 slate blue 로 표시됨. 길이/관통 유지, origin 십자와 단일색 통일, datum 무관 yellow 색은 불변
result: [pending]

### 5. 패턴 ROI 토글 ON/OFF (#2)
expected: 결과 뷰어에서 chk_overlayPattern 체크박스 토글. ON → 패턴1/패턴2 ROI 가 cyan 박스로 결과화면에 렌더, OFF → 즉시 숨김+재렌더. 기존 datum/measure 토글 회귀 없음
result: [pending]

## Summary

total: 5
passed: 1
issues: 0
pending: 4
skipped: 0

## Gaps

[none yet]
