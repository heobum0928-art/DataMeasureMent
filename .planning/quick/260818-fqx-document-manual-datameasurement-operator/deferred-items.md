# Deferred Items — quick-260818-fqx

이 태스크 범위(9장 추가) 밖에서 발견했지만 손대지 않은 항목. `hard_constraints` 4번(1~8장 무변경)에 따라 수정하지 않았다.

## 1. 5-2절 "[Light] 버튼" 서술이 현재 코드와 어긋남

- **위치:** `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` 5-2절(260818-el1에서 이미 작성·출하된 내용)
- **현재 원고 문구:** "[Light] 버튼을 누르면 선택한 항목에 설정된 조명이 지정된 밝기로 켜집니다. 촬영 전 조명 상태를 미리 볼 때 사용합니다."
- **코드 확인 결과:** `WPF_Example/UI/ControlItem/InspectionListView.xaml`의 `button_light`는 `Visibility="Collapsed"`로 화면에서 숨겨져 있다(주석: "Shot/Datum 모두 PropertyGrid 에 채널별 Light 탭이 생겨 이 legacy 단일그룹 토글 버튼은 숨김"). 즉 이 버튼은 현재 화면에 보이지 않는다.
- **왜 고치지 않았는가:** 이번 태스크의 `hard_constraints` 4번이 1~8장을 "한 글자도, 줄바꿈 하나도" 수정하지 못하게 금지한다. 5장은 260818-el1에서 이미 완성·검증된 영역이라 이번 태스크의 쓰기 허용 범위 밖이다.
- **제안:** 5장을 다시 다루는 별도 quick 태스크(또는 다음 매뉴얼 개정)에서 5-2절 문구를 실제 화면(채널별 Light 탭)에 맞게 고칠 것을 권장한다.
