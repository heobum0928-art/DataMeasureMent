---
quick_id: 260611-e22
slug: settingwindow-camerarole-warning
date: 2026-06-11
type: quick
status: in-progress
---

# Quick: SettingWindow CameraRole 변경 경고 확인

## 문제
설정 창에서 CameraRole(검사 모드 TopBottom↔Side)을 PropertyGrid 로 바꾸고 OK 하면 조용히 Save 됨 → 함부로 바뀌면 PC 검사 담당이 통째로 바뀌어 위험.

## 변경
대상: `WPF_Example/UI/Setting/SettingWindow.xaml.cs`
1. 생성자 Load() 직후 `pOriginalCameraRoleValue` 에 현재 CameraRoleValue 보관.
2. `Btn_ok_Click` 에서 Save 전, CameraRoleValue 가 원본과 다르면:
   - 경고 MessageBox(YesNo, Warning) — `[oldRole]→[newRole]`(ECameraRole enum 이름), "껐다 켜야 적용" 안내.
   - No: 원본값으로 원복 + return (창 유지, 저장 안 함).
   - Yes: Save → 재시작 안내 MessageBox(OK, Information) → Close.
3. CameraRole 미변경 시 기존대로 조용히 Save/Close.

## 검증
- Debug/x64 MSBuild exit 0, 신규 error 0.
- 변경 라인 //260611 hbk 주석.
