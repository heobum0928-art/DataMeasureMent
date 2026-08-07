---
phase: quick-260807-jx1
plan: 01
subsystem: WPF UI / TrayVisionView / Ethernet Align Camera
tags: [wpf, ui, camera, live-view, dispatcher-timer]
dependency-graph:
  requires: []
  provides:
    - "TrayVisionView Live 스트림 폴링 타이머"
  affects:
    - "WPF_Example/Custom/UI/TrayVisionView.xaml.cs"
tech-stack:
  added: []
  patterns:
    - "DispatcherTimer 200ms 폴링 (BottomVisionView 와 동일 패턴, PeekLastImage → LoadImage)"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/UI/TrayVisionView.xaml.cs"
decisions:
  - "BottomVisionView.xaml.cs 의 검증된 Live/Stop/타이머 구현을 재설계 없이 1:1 포팅 (신규 문자열/동작 임의 변경 금지)"
  - "Bottom 과 동일하게 Unloaded 이벤트 훅은 추가하지 않음 (parity gap, 잔여사항으로만 기록)"
metrics:
  duration: "~15분"
  completed: 2026-08-07
status: complete
---

# Phase quick-260807-jx1 Plan 01: TrayVisionView Live 스트림 폴링 타이머 이식 Summary

BottomVisionView 의 검증된 DispatcherTimer 200ms 폴링 패턴을 TrayVisionView 로 1:1 포팅하여 Live 버튼 클릭 시 실제로 화면이 갱신되도록 고쳤다.

## 근본원인 (1줄 요약)

카메라/네트워크/SDK 는 전부 정상 동작 중이었으나(`Camera.Live()` 로 HIK SDK 스트림만 켜질 뿐), 스트리밍 프레임을 뷰어로 끌어오는 타이머가 `TrayVisionView.LiveButton_Click` 에 아예 없어서 화면이 한 번도 갱신되지 않았다.

## What Changed

`WPF_Example/Custom/UI/TrayVisionView.xaml.cs` 단일 파일 수정 (BottomVisionView.xaml.cs line 8, 43-44, 293-377 의 대응 구현을 그대로 이식):

1. `using System.Windows.Threading;` 추가 (DispatcherTimer 네임스페이스)
2. `private DispatcherTimer _liveTimer;` 필드 추가 (설명 주석 포함)
3. `LiveButton_Click` 확장 — `Camera.Live()` 성공 시 `btn_live.Content = "Live On"` + `btn_grab.IsEnabled=false` + `btn_live.IsEnabled=false`(Live 중 Grab/재클릭 차단, 해제는 Stop 으로만) + `StartLiveTimer()` 호출. 실패/예외 시 `btn_live.Content` 를 "Live Off" 로 복원(기존 Tray 코드에 없던 버튼 글자 복원 로직 추가).
4. `StopButton_Click` 확장 — `StopLiveTimer()` 를 `Camera.Stop()` 보다 먼저 호출(역순 시 정지된 스트림을 계속 Peek 하는 틱이 남는 버그 방지) → `btn_live.Content = "Live Off"` → Grab/Live 버튼 재활성화 → 상태 라벨 "대기".
5. `StartLiveTimer()` / `StopLiveTimer()` / `LiveTimer_Tick()` 3개 메서드 신규 추가. `StartLiveTimer`는 중복 타이머 방지(`_liveTimer != null` 조기 return), `LiveTimer_Tick`은 `EthernetAlignCamera.PeekLastImage()` 결과를 `_viewer.LoadImage()` 로 전달하고 `finally` 에서 `img?.Dispose()` — 예외는 조용히 삼키고 상태 라벨을 건드리지 않아 다음 틱에서 자연 복구된다.

## 검증 결과 (S1~S4)

verify.sh 를 실행했으나 sandbox bash 의 `PATH` 에 `msbuild` 가 없어 S4 만 스크립트 자체는 exit 127 로 실패했다. S1~S3 는 스크립트 그대로 PASS. S4 는 실제 MSBuild.exe 경로(`C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`)를 직접 지정해 동일한 `/p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal /nologo` 인자로 재실행하여 PASS 확인함 — verify.sh 의 PATH 탐색 로직 문제이며 빌드 자체는 정상이다.

- **S1 (신규 심볼 존재)**: PASS — `using System.Windows.Threading;` / `DispatcherTimer _liveTimer` / `StartLiveTimer` / `StopLiveTimer` / `LiveTimer_Tick` / `PeekLastImage` 6개 토큰 전부 확인.
- **S2 (배선 확인)**: PASS — `LiveButton_Click` → `StartLiveTimer()`, `StopButton_Click` → `StopLiveTimer()`, `LiveTimer_Tick` → `PeekLastImage()` → `_viewer.LoadImage()` 전부 호출부 존재 확인.
- **S3 (보호 파일 무변경)**: PASS — `git diff --name-only` 에 BottomVisionView / EthernetAlignCamera / `*.xaml` 어느 것도 등장하지 않음. 저장소에 기존 미커밋 변경(`Action_TopInspection.cs`)이 있으나 이번 커밋에 포함하지 않았음(단일 파일 스코프 커밋 `97de921`).
- **S4 (빌드)**: PASS (수동 실행) — MSBuild Debug/x64, 0 errors, exit 0. Warning 8건 전부 `Sequence_Top.cs` / `Sequence_Bottom.cs` / `SequenceHandler.cs` / `VirtualCamera.cs` 의 기존 obsolete 경고로, `TrayVisionView.xaml.cs` 신규 warning 은 0건(baseline 대비 증가 없음).

## Commits

- `97de921` — fix(260807-jx1): TrayVisionView Live 버튼 스트림 폴링 타이머 이식 (`WPF_Example/Custom/UI/TrayVisionView.xaml.cs` 단일 파일)

## Deviations from Plan

None — plan executed exactly as written. Plan-specified verify.sh 의 msbuild PATH 탐색 실패는 sandbox 셸 구성 문제이며 계획/구현의 편차가 아니다(수동 경로 지정으로 동일 결과 재현 확인).

## Known Stubs

None.

## 잔여사항 (Parity Gap, 의도적)

Bottom 의 `StopLiveTimer` XML 문서 주석은 "뷰 전환/언로드 시에도 호출하라"고 적혀 있으나, Bottom 자체도 실제로 Unloaded 이벤트를 후킹하지 않고 `StopButton_Click` 에서만 호출한다. 이 플랜은 Bottom 과의 1:1 동작 일치가 목표이므로 Tray 에도 Unloaded 훅을 추가하지 않았다 — Bottom 과 동일한 기존 갭이며 신규 결함이 아니다.

## 사용자 실기 확인 (미완료 — 다음 단계)

SIDE PC 에서 Tray 비전 탭 Live 클릭 시 실제 화면 갱신 여부, Live 중 Grab/Live 버튼 비활성 여부, Stop 클릭 시 정지+버튼 재활성+상태 라벨 복귀 여부는 실제 SIDE PC 환경에서만 확인 가능(이 세션은 개발 PC). 사용자 UAT 대기.

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` (using/필드/3개 신규 메서드 확인)
- FOUND: commit `97de921` (`git log --oneline --all | grep 97de921` 확인)
