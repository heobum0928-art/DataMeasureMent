---
status: partial
phase: 61-ui-tabcontrol-d-2026-06-23
source: [61-VERIFICATION.md]
started: 2026-06-24
updated: 2026-06-24
---

## Current Test

[awaiting human testing — 58~61 일괄 런타임 UAT 예정]

## Tests

### 1. TabControl 탭 전환 동작 — 검사/Tray 비전/Bottom 비전 탭 클릭
expected: 각 탭 클릭 시 해당 뷰(MainView, TrayVisionView, BottomVisionView)가 정상 렌더링되고 기존 [검사] 탭 내 검사 기능이 회귀 없이 동작한다
result: [pending]

### 2. EthernetVisionMode=None 설정 시 탭 Visibility 게이트
expected: 설정창에서 EthernetVisionMode=None 으로 저장 후 닫으면 [Tray 비전][Bottom 비전] 탭이 Collapsed(비표시)된다
result: [pending]

### 3. EthernetVisionMode=Tray 설정 시 탭 게이트 + Grab 동작
expected: EthernetVisionMode=Tray 로 설정 후 [Tray 비전] 탭이 표시되고, Grab 버튼 클릭 시 이미지가 공유 뷰어(ViewerHostBorder)에 표시된다
result: [pending]

### 4. EthernetVisionMode=Bottom 설정 시 탭 게이트 + 피커센터 캘 UI 동작
expected: EthernetVisionMode=Bottom 으로 설정 후 [Bottom 비전] 탭이 표시되고, 초기화/ROI 지정/스텝 추가/계산 버튼이 각각 PickerCal 서비스에 위임하여 lbl_calStatus/lbl_pickerCenter 를 갱신한다
result: [pending]

### 5. 기존 검사 탭(MainView) 회귀 없음 — 재부모화 후 MainView 기능 전체
expected: TabControl 추가 후 [검사] 탭 내 MainView 의 ROI 편집/티칭/FAI 측정/결과 표시 등 기존 기능이 모두 정상 동작한다
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
