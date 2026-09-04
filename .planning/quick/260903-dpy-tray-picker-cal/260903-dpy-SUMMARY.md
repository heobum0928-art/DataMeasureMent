---
quick_id: 260903-dpy
slug: tray-picker-cal
date: 2026-09-03
status: complete
commits: e73e944, b447a94, ee652ea
---

# Tray 피커 회전중심 캘리브레이션 추가 — 완료

## 결과

Tray 정렬 화면에 피커센터 캘 패널을 이식하고, 계산된 회전중심으로 Tray 응답값을 보정하게 했다.

- `e73e944` 설정 2개(`TrayPickerCenterRow/Col`, 기본 0.0 = 미캘)
- `b447a94` `ApplyPickerCenterCorrection` 파라미터화 + Tray 분기 적용
- `ee652ea` Tray UI 이식 + 수동 반복 UX + 서비스 가산 멤버 2개

## 검증 (조정자가 직접 재확인)

| 항목 | 결과 |
|---|---|
| Debug/x64 빌드 | **ERR=0 / WARN=18** (baseline 유지) |
| 가독성 규칙 grep | 삼항 1 → **한글 문자열 안 `?`, 실제 연산자 아님**. 나머지 전부 0 |
| StaticResource | `TrayActionButtonStyle` 1종, **정의 확인** |
| 삭제 줄 | 7줄 — **전부 교체 자리**, 소실 기능 없음 |
| csproj | 미커밋 유지 ✓ |

### 미캘 폴백 (핵심 안전장치)

`ApplyPickerCenterCorrection` 진입부에서
`|pickerRow| <= PICKER_CENTER_ZERO_EPS && |pickerCol| <= PICKER_CENTER_ZERO_EPS` 이면
`corrRow=dRow, corrCol=dCol` 그대로 즉시 return.
`TrayPickerCenterRow/Col` 기본값이 0.0 이므로 **캘 수행 전까지 `$ALIGN_RESULT` 값은 종전과 동일**하다.
부호 상수 `TRAY_OFFSET_X_SIGN` / `TRAY_THETA_SIGN` 곱도 그대로 유지됨.

### Bottom 회귀 0

Bottom 분기는 이전에 함수 내부에서 읽던 `PickerCenterRow/Col` 을 **같은 값 그대로 인자로 전달**만 한다.
Bottom UI 파일은 미수정.

## 계획 대비 변경 (실행 중 사용자 지시로 반영)

1. **이미지 소스 순서를 Bottom 과 반대로** — 카메라 그랩 우선, 저장 이미지는 카메라 불가 시 폴백.
   Bottom 은 "파일 우선"이라 폴더로 열어둔 낡은 사진으로 캘이 잡히는 미수정 결함이 있다(260901 리뷰 지적).
   Tray 는 그 결함을 물려받지 않게 뒤집고, **사용한 소스를 상태 라벨에 표시**한다.
2. **순서 안내 UX** — 버튼 ①②③④ 번호 + 단계별 활성/비활성 게이팅 + 상시 진행 배너.
3. **`↺ 마지막 취소`** — 전 과정이 수동이고 ~10회 반복이라, 한 번 삐끗했을 때 처음부터 다시 하지 않도록.
   `PickerCenterCalibrationService` 에 `TryRemoveLastStep()` + `MinSteps` **가산만** 추가(피팅 알고리즘 무수정).
4. 각도 분포 안내문 + 잔차(RMS/최대) 상시 표시 + `lbl_calStatus` MinHeight(버튼 흔들림 방지).

실행자가 자체 발견·수정한 건: 초안의 별도 `ShowCalRoiOverlay()` 가 기존 `ShowTeachRoiOverlays()` 와
같은 비-가산 `SetResultRoiOverlays` 를 호출해 서로 ROI 를 지우는 문제 → 기존 메서드에 통합(Bottom 과 동일 패턴).

## 남은 일 (실기 UAT)

- 실제 Tray 피커로 캘 수행 → 잔차 확인 → 중심값 저장
- **캘 저장 후에는 `$ALIGN_RESULT` 값이 바뀐다 → PLC 측과 반드시 사전 합의할 것**
- 각도를 한쪽에 몰지 말고 고루 분포시킬 것(잔차로 확인 가능)
