---
quick_id: 260824-frt
slug: manual-cycle-trigger-rename
date: 2026-08-24
status: in-progress
---

# Quick 260824-frt: 수동 Z 트리거 → 수동 사이클 트리거 승격 (A안)

## 배경

`DebugManualZTrigger`는 Z축을 전혀 움직이지 않는다. PLC가 보낼 `$PREP(z_index)` +
`$TEST` 패킷을 코드로 만들어 실제 처리 경로(ProcessPrep→ProcessTest)로 흘려보내는
것이 전부다. `RunMoveZ()`는 SIMUL에서 건너뛰고 실장비에서도 DelayMs만 sleep한다.

따라서 현재 이름/문구가 실제 동작과 어긋나 있다:

- "IAxisController 전까지만 사용" → IAxisController와 무관 (사실 아님)
- "임시 테스트용" → 실제로는 PLC 장애 시 "비전 문제냐 PLC 문제냐"를 가르는 영구 진단 도구
- `Debug`/`Z` 접두사 → 디버그 전용도 아니고 Z를 움직이지도 않음

## 범위

동작 변화 0인 리네임 + 주석/문구 정정 + 권한 게이트 1개.

### Task 1 — 심볼 리네임 (순수 리네임)

| 현재 | 변경 후 |
|---|---|
| `SystemHandler.DebugManualZTrigger` | `TriggerInspectionCycleManually` |
| `panel_ManualZTrigger` | `panel_ManualCycleTrigger` |
| `combo_ManualZSeq` | `combo_ManualCycleSeq` |
| `txt_ManualZIndex` | `txt_ManualCycleZIndex` |
| `btn_ManualZTrigger` | `btn_ManualCycleTrigger` |
| `ManualZTriggerButton_Click` | `ManualCycleTriggerButton_Click` |
| `PopulateManualZSeqCombo` | `PopulateManualCycleSeqCombo` |
| 로그 태그 `[임시 수동Z트리거]` / `[임시 수동Z트리거 UI]` | `[수동 사이클 트리거]` |

### Task 2 — UI 문구 정정 (MainView.xaml)

- 패널 라벨: `[임시 테스트용 — POC 자동화(IAxisController) 전까지만 사용]`
  → `[PLC 미연결 진단 — 수동 사이클 트리거]`
- `z_index:` 입력칸에 ToolTip 추가 — 축 좌표가 아니라 조명/촬영 단계 번호임을 명시,
  크로스-Z는 0 → 지그 이동 → 1 순서로 두 번 실행.

### Task 3 — 주석 정정 (사실과 다른 서술 제거)

3파일의 `[임시 / TEMP] ... 삭제할 것` 서술을 제거하고 다음으로 교체:

- PLC 없이 검사 사이클을 로컬에서 발행하는 정식 진단 경로다.
- PLC 장애 시 "비전이냐 PLC냐"를 가르는 절개선 — PLC 연동 후에도 유지한다.
- zIndex는 축 좌표가 아니라 조명 선택 + Datum 단계 지정에 쓰인다.
- 크로스-Z Datum은 z=0, z=1 두 번 호출로 성립 (사이 지그 이동은 사람이 수행).
  프로토콜 경로이므로 z=0에서만 BeginCrossZImageCycle이 돌고 z=1은 저장소를 보존한다.
- 기존 문장 "ProcessPrep/ProcessTest 는 프로덕션 TCP 경로 — 시그니처/로직 변경 금지,
  호출만 한다"는 **그대로 유지**.

### Task 4 — ADMIN 권한 게이트 (유일한 동작 추가)

`ManualCycleTriggerButton_Click` 진입 직후, 시퀀스 선택 검사보다 앞에서 확인.
기존 관용구(`UI/Recipe/OpenRecipeWindow.xaml.cs:65`)를 그대로 따른다.

Visibility로 숨기지 않는다 — `MainView_Loaded` 시점 로그인 상태로 한 번만 숨기면
이후 로그인/로그아웃을 따라가지 못한다. 클릭 시점 검사가 정확하다.

## 제약 (프로젝트 상시 규칙)

- 삼항 연산자 `?:` 금지 → if-else
- 헝가리언 접두사 유지 (`b`/`n`/`sz`), 해당 파일 기존 스타일 우선
- 중괄호 스타일: `MainView.xaml.cs`=K&R, `Custom/SystemHandler.cs`=Allman — 섞지 말 것
- C# 7.2 문법만
- 회귀 0: ProcessPrep/ProcessTest/RunMoveZ/크로스-Z 로직 단 한 줄도 건드리지 않음
- 날짜 주석(`//YYMMDD hbk`) 규칙 폐기 — 새로 달지 않음

## 검증

1. `grep`로 구 심볼 잔존 0줄 (obj/ 제외)
2. Debug/x64 빌드 에러 0
   — 경고 12줄(CS0618×10 + CS0162×2)은 baseline, 통과 기준 아님
3. 빌드 산출물 잠김 시 프로세스 종료 금지 → 스크래치 OutDir 컴파일 검증 또는 잠김 보고
