---
status: partial
phase: 65-bottom-4jig-face-align-2026-06-25
source: [65-VERIFICATION.md, 65-04-PLAN.md]
started: 2026-06-26
updated: 2026-06-26
---

## Current Test

[작업자 실HW 테스트 대기 — 실 이더넷 카메라 · 실 PLC · 실 지그 필요]

## Tests

### 1. 6슬롯 면별 티칭 + 슬롯 컨텍스트 분리 (D-01/D-02/D-04)
steps: |
  1. DatumMeasurement.exe 실행 (EthernetVisionMode=Bottom, FAI_1 레시피)
  2. Bottom 비전 탭 → "면 슬롯" ComboBox에서 [3D] 3D_Top 선택
  3. "폴더 열기"로 3D_Top 면 이미지 로드 → 이전/다음으로 프레임 선택
  4. ROI 1 그리기 → ROI 2 그리기 → 티칭 저장
  5. 슬롯을 [3D] 3D_Bottom으로 전환 → 상태가 "티칭 없음"으로 바뀌는지, 다시 3D_Top 선택 시 "티칭 OK" 복원되는지 확인
  6. 6슬롯 전부 반복 티칭 (3D_Top / 3D_Bottom / 2D_TOP / 2D_BOTTOM / 2D_SIDE_1 / 2D_SIDE_2)
expected: 각 슬롯 티칭 후 lbl_teachStatus에 "[슬롯명] 티칭 OK (HasTemplate=True)" 표시. 슬롯 전환 시 티칭 상태/ROI가 슬롯별로 분리 유지.
result: [pending]

### 2. 슬롯별 모델 파일 18개 생성 + 파일명 오치환 검증 (Plan 01 버그픽스)
steps: |
  1. D:\Data\Recipe\FAI_1\ETHERNET_ALIGN\ 폴더 확인
  2. 슬롯당 3파일(_1.shm / _2.shm / .json) × 6슬롯 = 18파일 생성 확인
  3. 특히 Bottom_2D_SIDE_1.json 파일명이 Bottom_2D_SIDE_.json(언더바 뒤 잘림)이 아닌지 확인
expected: 18개 파일 생성. 2D_SIDE_1 / 2D_SIDE_2 슬롯의 .json 파일명이 토큰 손상 없이 정상 (BuildJsonPath EndsWith("_1") 수정 검증).
result: [pending]

### 3. 슬롯별 UI Run — 보정 pose 산출 (D-06)
steps: |
  1. 각 슬롯 선택 → 자재 안착 → "검사" 버튼
  2. lbl_result의 OffsetX / OffsetY / Theta / Score 표시 + 검출 ROI/에지 시각화 확인
expected: 각 슬롯에서 보정 pose(OffsetX/OffsetY/Theta) + Score 표시, 검출 윤곽 시각화 정상.
result: [pending]

### 4. PLC $ALIGN_TEST AlignFace 0~5 → $ALIGN_RESULT pose 정합 (D-03/D-06/D-07/D-08)
steps: |
  1. PLC가 $ALIGN_TEST:BOTTOM,<자재번호>,<모드>,<AlignFace 0~5>@ 송신
  2. 비전 응답 $ALIGN_RESULT 에 AlignFace echo + OK/NG + OffsetX/OffsetY/Theta 확인
  3. pose 값이 Test 3의 UI Run 값과 일치하는지 확인
  4. PLC가 보정값으로 자재를 ideal 위치에 안착시키는지 확인
  5. (음성 테스트) AlignFace=6 또는 미티칭 슬롯 송신 → IsPass=false, NG 응답 + pose=0 3필드 채워짐 + 로그 확인 (WR-01/02 수정 검증)
expected: AlignFace 0~5 정상 응답 pose가 UI Run과 일치하고 PLC가 보정 안착. OOB/미티칭은 NG + pose=0(필드 3개 유지)로 PLC 파서 오류 없음.
result: [pending]

### 5. 회귀 — Tray / 기존 단일 Bottom / MainView 검사 (D-09/D-10)
steps: |
  1. Tray 비전 탭: 기존 Tray 티칭/Run 정상(무변경) 확인
  2. 기존 Bottom 단일 경로(slot=None, Bottom_1/2.shm) 동작 회귀 확인
  3. MainView 검사 (Top/Side/Bottom 측정) 정상 동작 확인
expected: Tray·단일 Bottom·MainView 검사 전부 Phase 65 이전과 동일하게 동작 (회귀 0).
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
