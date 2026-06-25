---
status: partial
phase: 61-ui-tabcontrol-d-2026-06-23
source: [61-VERIFICATION.md]
started: 2026-06-24
updated: 2026-06-25
---

## Current Test

[1차 실측 완료 2026-06-25 — UI/매칭 PASS, 갭 2건 → Phase 61.1 보완]

## Tests

### 1. TabControl 탭 전환 동작 — 검사/Tray 비전/Bottom 비전 탭 클릭
expected: 각 탭 클릭 시 해당 뷰(MainView, TrayVisionView, BottomVisionView)가 정상 렌더링되고 기존 [검사] 탭 내 검사 기능이 회귀 없이 동작한다
result: PASS (2026-06-25 — 탭 정상 렌더, 전환 정상)

### 2. EthernetVisionMode=None 설정 시 탭 Visibility 게이트
expected: 설정창에서 EthernetVisionMode=None 으로 저장 후 닫으면 [Tray 비전][Bottom 비전] 탭이 Collapsed(비표시)된다
result: PASS (2026-06-25 — None/Tray/Bottom 게이트 전부, 설정창 닫는 즉시 반영 D-04)

### 3. EthernetVisionMode=Tray 설정 시 탭 게이트 + Grab 동작
expected: EthernetVisionMode=Tray 로 설정 후 [Tray 비전] 탭이 표시되고, Grab 버튼 클릭 시 이미지가 공유 뷰어(ViewerHostBorder)에 표시된다
result: PASS (2026-06-25 — Grab→뷰어 표시 + 티칭 HasTemplate=True + Run off=(0,0) score1=0.998/score2=0.990, 로그 ALIGN_SVC run OK Tray)

### 4. EthernetVisionMode=Bottom 설정 시 탭 게이트 + 피커센터 캘 UI 동작
expected: EthernetVisionMode=Bottom 으로 설정 후 [Bottom 비전] 탭이 표시되고, 초기화/ROI 지정/스텝 추가/계산 버튼이 각각 PickerCal 서비스에 위임하여 lbl_calStatus/lbl_pickerCenter 를 갱신한다
result: PARTIAL (2026-06-25 — Bottom 탭 게이트 + Run Theta(deg) 표시 PASS, 로그 run OK Bottom theta=0 score1=0.996/score2=0.990. 피커캘 버튼/상태/에러표시 동작하나 실값은 실 피커 36스텝 회전 필요 — 같은 이미지라 HALCON #3274 fit_circle 유효점 부족=예상됨)

### 5. 기존 검사 탭(MainView) 회귀 없음 — 재부모화 후 MainView 기능 전체
expected: TabControl 추가 후 [검사] 탭 내 MainView 의 ROI 편집/티칭/FAI 측정/결과 표시 등 기존 기능이 모두 정상 동작한다
result: PASS (2026-06-25 — [검사] 탭 정상, 회귀 없음)

## Summary

total: 5
passed: 4
issues: 0
pending: 0
skipped: 0
blocked: 0
partial: 1

## Gaps

### G-61.1-01: Offline 다중 이미지 로드 부재 (status: open → Phase 61.1)
현재 Grab 은 D:\align_test.bmp 한 장 고정 폴백(카메라 미연결). Offline 에서 이동/회전된 다른 이미지로 offset/theta 가 실제로 변하는지 검증 불가 + 캘 지그 이미지로 피커캘 부분검증 불가. 사용자 fail 지목.
보완: Tray/Bottom 뷰 툴바에 [폴더 열기] + [◀ 이전][다음 ▶] 이미지 로더 추가 (서비스 무수정 뷰 레벨, MainResultViewerControl.LoadImage 활용, 기존 Grab/폴백 유지). → Phase 61.1.

### G-61.1-02: 매칭 결과 이미지 시각화 부재 (status: open → Phase 61.1)
검사 결과가 좌측 텍스트(X/Y/Theta/Score)로만 표시되고 이미지 위 검출 위치 시각화 없음. AlignResult 에 검출좌표 없음 → 서비스(AlignShapeMatchService) 확장 필요(anti-goal 일부 해제) + HALCON 창 내부 오버레이(airspace). → Phase 61.1 (2순위).

> 비고: 59(Tray/Bottom 매칭)·60(피커캘) 의 런타임 항목은 본 61 UAT 에서 UI 경유로 매칭 동작 확인됨. offset/theta 부호·축 확정(59-4)과 피커캘 실값(60-1/2)은 G-61.1-01(이미지 로더) 보완 후 + 실 피커 HW 에서 재검증.
