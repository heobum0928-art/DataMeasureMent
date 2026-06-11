---
quick_id: 260611-e22
slug: settingwindow-camerarole-warning
date: 2026-06-11
type: quick
status: complete
commit: 06c62b7
---

# Quick SUMMARY: SettingWindow CameraRole 변경 경고 확인

## 변경
`WPF_Example/UI/Setting/SettingWindow.xaml.cs`
- 필드 `pOriginalCameraRoleValue` 추가 + 생성자 Load() 직후 현재 CameraRoleValue 보관.
- `Btn_ok_Click`: Save 전 CameraRoleValue 변경 감지 →
  - 변경 시: 경고 MessageBox(YesNo/Warning, `[oldRole]→[newRole]` enum 이름 + 재시작 안내).
    - No → 원본값 원복 + return(창 유지, 미저장).
    - Yes → Save + 재시작 안내 MessageBox(OK/Information) → Close.
  - 미변경 시: 기존대로 조용히 Save/Close.

## 검증
- Debug/x64 MSBuild EXIT=0, 신규 error/warning 0 (기존 CS0618/CS0162/MSB3884만).
- 변경 라인 //260611 hbk 주석 부착.

## 미수행 (런타임 UAT)
SIMUL 앱 기동 후 설정 창에서 CameraRole 변경→OK 경고 노출/원복/재시작안내 실제 동작은 사용자 육안 확인 필요.

## 참고 (알려진 한계)
취소(No) 시 backing 값은 원복되나 PropertyGrid 가 INotifyPropertyChanged 미발생이면 그리드 표시는 변경값으로 남을 수 있음(표시-실제 불일치). 재-OK 시 원본과 동일해 경고 없이 정상 저장되므로 기능상 안전. 표시 동기화가 필요하면 후속 처리.

## commit
06c62b7
