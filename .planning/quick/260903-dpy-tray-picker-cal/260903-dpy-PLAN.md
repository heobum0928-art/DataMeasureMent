---
quick_id: 260903-dpy
slug: tray-picker-cal
date: 2026-09-03
status: planned
---

# Tray 피커 회전중심 캘리브레이션 추가

## 왜

Tray 는 비전이 알려준 위치를 피커가 보고 집는다. 카메라와 피커는 같은 Y축 위에
약 20cm 떨어져 있고, 피커는 부품 각도만큼 회전한다. **피커 회전중심이 흡착 노즐과
일치하지 않으면 회전할 때 노즐이 옆으로 밀려 부품을 놓친다.** 그 밀림량을 보정하려면
회전중심을 알아야 하는데, Tray 에는 그걸 구하는 수단이 없다.

Bottom 은 카메라가 위를 봐서 "부품을 든 채 돌리며 촬영"으로 구한다. Tray 는 Top 카메라라
든 상태를 못 본다. **대신 "놓고 재고 → 집어서 각도 바꿔 다시 놓고 재고"를 반복하면
부품 위치들이 같은 원을 그린다 — 기하학적으로 Bottom 과 동일하다.** 그래서 계산부
(원 피팅)를 그대로 재사용한다. 차이는 이미지를 모으는 작업 순서뿐이고 그건 운영 절차다.

사용자 결정(260903): **비전이 보정까지 해서 전송한다(Bottom 방식).**

## 안전장치 (가장 중요)

Tray 회전중심 미캘(0,0) 이면 보정을 건너뛰어 **현재 동작과 100% 동일**해야 한다.
즉 이 작업만으로는 `$ALIGN_RESULT` 값이 바뀌지 않고, **운영자가 캘을 수행한 뒤에만** 바뀐다.
(PLC 와 값이 어긋나는 사고를 막기 위한 필수 조건 — 이게 깨지면 이 작업은 실패다.)

## Task 1 — 설정 2개 추가

파일: `WPF_Example/Custom/SystemSetting.cs`

- `TrayPickerCenterRow` / `TrayPickerCenterCol` (double, 기본 `0.0` = 미캘) 추가.
  Bottom 의 `PickerCenterRow/Col`(:264 부근) 바로 옆에 같은 관용구로 둘 것.
- 미캘 판정은 기존 `SystemSetting.PICKER_CENTER_ZERO_EPS`(:51) 재사용. **새 상수 만들지 말 것.**
- 이 저장소는 reflection INI Load 가 누락 키를 0 으로 덮어쓰는 함정이 있으나,
  **이 두 값은 0 = 미캘이 곧 올바른 초기값**이라 복원 가드가 불필요하다.
  기존 `PickerCenterRow/Col` 이 같은 이유로 가드가 없다 — 그 근거를 주석으로 남길 것.
  (`AfterLoad()` 의 `RestoreAlignVerifyDefaults` 등에 추가하지 말 것.)

## Task 2 — 보정 함수 파라미터화 + Tray 적용

파일: `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`

- 현재 `ApplyPickerCenterCorrection(dRow, dCol, thetaDeg, out corrRow, out corrCol)`(:923)이
  `SystemSetting.Handle.PickerCenterRow/Col` 을 **직접 읽는다.** 회전중심을 인자로 받도록
  시그니처를 바꾼다(pickerRow/pickerCol 추가). 미캘 폴백 로직(EPS 비교 → 입력 그대로 반환)은
  함수 안에 그대로 유지.
- Bottom 호출부(:718)는 `PickerCenterRow/Col` 을 넘긴다 → **동작 완전 동일해야 함.**
- `Run()` 의 Tray 분기(:725~727, "Tray = 미보정 midpoint offset" 주석 자리)에서 동일하게
  보정을 적용하고 `TrayPickerCenterRow/Col` 을 넘긴다.
  `TRAY_OFFSET_X_SIGN` / `TRAY_THETA_SIGN` 곱은 **그대로 유지**(제거·순서변경 금지).
- 주석의 "피커 캘리브 없음" 문구는 이제 사실이 아니므로 정정할 것.

## Task 3 — Tray UI 이식

파일: `WPF_Example/Custom/UI/TrayVisionView.xaml` + `.xaml.cs`

Bottom 의 피커캘 GroupBox(`BottomVisionView.xaml` :208~282)를 Tray 로 이식한다.
- 버튼: 초기화 / 검색ROI 지정 / Cal 모델 티칭 / 스텝 추가 / 피커센터 계산
- 라벨: `lbl_calStatus`(상태), `lbl_pickerCenter`(중심 좌표 + 피팅 잔차)
- 캘 스텝 각도 콤보(`lbl_calStepInfo`, `CalStepAngleComboBox_SelectionChanged`,
  `RefreshCalStepInfo`, `LoadCalStepAngleToUi`)도 함께 이식 — 스텝 수 안내에 필요.

핸들러 이식 대상(`BottomVisionView.xaml.cs`): `OnCalRectDrawn`(:1439),
`CalResetButton_Click`(:1471), `CalDrawRoiButton_Click`(:1508),
`CalTeachModelButton_Click`(:1579), `CalAddStepButton_Click`(:1624),
`CalComputeButton_Click`(:1707), `BuildPickerCenterText`(:1773).
**계산 결과 저장만 `TrayPickerCenterRow/Col` 로 바꾼다.**

주의:
- Tray XAML 좌측 패널은 최근 다른 세션이 `ScrollViewer` 를 넣고 버튼을 추가한 상태다.
  **Grid.Row 배치를 깨지 말 것** — 새 GroupBox 는 기존 Row 뒤에 추가하고 RowDefinition 도 함께 늘린다.
- 🔒 **StaticResource 는 `TrayActionButtonStyle` 만 쓸 것.** 과거에 Tray 에서 Bottom 전용
  `BottomActionButtonStyle` 을 참조했다가 **빌드는 통과하고 앱 시작 시 XamlParseException 으로
  죽은 사고**가 있었다. StaticResource 는 컴파일 타임 검증이 안 된다.
  커밋 전 Tray XAML 이 참조하는 모든 StaticResource 키가 실제 정의돼 있는지 grep 확인 필수.

## Task 4 — 서비스는 건드리지 않는다

`WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` 는 **수정 금지.**
모델 티칭/검출/원피팅을 그대로 쓴다.

인스턴스 `EthernetVisionHandler.Handle.PickerCal` 은 공용 1개이고 모델 경로도
`{Recipe}\ETHERNET_ALIGN\picker_cal.shm` 로 모드 무관 고정이다. **PC 당
`EthernetVisionModeValue` 가 하나(이 PC=2=Bottom)라 Bottom/Tray 동시 사용이 없어 충돌하지 않는다.**
이 근거를 Tray 코드에 주석으로 남기고 **모드별 분리는 하지 말 것**(불필요한 구조 변경 금지).

## 하지 말 것

- TCP 프로토콜 변경 금지 — `$ALIGN_RESULT` 필드 구성 그대로.
- Bottom 피커캘 동작 변경 금지(회귀 0).
- 원 피팅 알고리즘 변경 금지.
- 180° 2점 전용 특수 경로 만들지 말 것 — 여러 각도 + 기존 원 피팅으로 통일.

## 코딩 규칙 (CLAUDE.md 필수 — 위반 시 회귀 간주)

삼항 `?:` 금지 → if/else · 이항 `??`/`??=` 금지 · null 조건 `?.`/`?[]` 금지 ·
C# 8 switch 식 금지(전통 switch 문만) · C# 7.2 문법만 · 긴 조건은 이름 있는 bool 로 선추출
(한 조건식에 `&&`/`||` 3개 이상 금지) · 중괄호는 한 줄 분기라도 생략 금지 ·
가드 절로 중첩 낮추고 if 중첩 3단계 이상이면 메서드 분리 · 헝가리언(b/n/sz/d/hv) ·
매직넘버 금지(named const) · **날짜 주석(`//YYMMDD hbk`) 신규 금지** — 비자명한 "왜"만 ·
HImage/HObject/HTuple 반드시 Dispose(finally + try/catch).

UI 는 Bottom 의 기존 code-behind 패턴을 그대로 이식하는 것이므로 그 일관성을 우선한다.

## 검증

1. Debug/x64 빌드: **에러 0**, 경고 **baseline 18줄 유지**
   (경고 0 을 기대하지 말 것 — 이 저장소 정상 baseline 이 18 이다).
2. 추가분 규칙 grep 전항목 0: 삼항 / `??` / `?.` / `switch.*=>` / `hbk`.
3. Tray XAML 의 모든 StaticResource 키가 정의돼 있는지 확인(앱 시작 크래시 예방).
4. **미캘(0,0) 상태에서 Tray 응답값이 변경 전과 동일**한지 코드 경로로 확인.
5. `WPF_Example/DatumMeasurement.csproj` — 신규 파일 없으면 손대지 않는다.
   **이 파일에는 이 PC 전용 로컬 설정(OutputPath=D:\Data, Release 의 SIMUL_MODE 강제)이
   미커밋 상태로 있다. 절대 stage/commit 하지 말 것.**
