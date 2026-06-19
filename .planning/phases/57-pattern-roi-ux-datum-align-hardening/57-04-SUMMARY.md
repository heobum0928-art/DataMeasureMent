---
phase: 57-pattern-roi-ux-datum-align-hardening
plan: 04
subsystem: ui-pattern-teach
tags: [align, pattern-match, ux, datum, wpf-xaml]
requires:
  - "TryComposeAlign 단일 패턴 폴백 경로 (InspectionSequence.cs:490-514) — 변경 없음"
  - "CustomMessageBox.ShowConfirmation (UI/Dialog/CustomMessageBox.cs:68)"
provides:
  - "패턴1/패턴2 ROI 그리기 버튼 인접(나란히) 배치"
  - "패턴2 미설정 모델 생성 시 경고+override (단일 패턴 진행 허용)"
affects:
  - "WPF_Example/UI/ContentItem/MainView.xaml (버튼 레이아웃)"
  - "WPF_Example/UI/ContentItem/MainView.xaml.cs (InvokeCreatePatternModel)"
tech-stack:
  added: []
  patterns:
    - "CustomMessageBox.ShowConfirmation(MessageBoxButton.OKCancel) → MessageBoxResult.OK 긍정 분기"
    - "XAML 요소 순서 이동만으로 버튼 재배치 (속성/핸들러 무변경, 회귀 0)"
key-files:
  created: []
  modified:
    - "WPF_Example/UI/ContentItem/MainView.xaml"
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs"
decisions:
  - "ShowConfirmation 긍정 응답은 MessageBoxResult.OK (MessageBoxWindow.xaml.cs:86 OKCancel 확인) — 플랜 코드 그대로 채택, YesNo 대체 불필요"
  - "패턴1 가드(PatternRoi_Length1/2 <= 0)는 하드 블록 유지, 패턴2는 경고+override (D-06)"
  - "Task 3 빌드 검증은 source 변경 0 → 별도 commit 없음 (bin/obj gitignored)"
metrics:
  duration_min: 8
  completed: "2026-06-19"
  tasks: 3
  files: 2
---

# Phase 57 Plan 04: 패턴 ROI UX (#1 버튼 인접 + 단일 패턴 경고+override) Summary

패턴 매칭 ROI 입력 UX 를 정돈하고 안전장치를 추가했다: (1) 패턴1/패턴2 ROI 그리기 버튼을 UI 상 나란히 배치하고, (2) 패턴을 1개만 그린 채 모델 생성 시 경고 후 사용자 OK 시 단일 패턴으로 진행(override)하도록 했다. 패턴1 미확보 하드 블록과 단일 패턴 폴백 경로(InspectionSequence)는 무변경.

## What Was Done

### Task 1: MainView.xaml 패턴1/패턴2 버튼 인접 재배치 (a179c22)
- `btn_drawPatternRoi2`(패턴2) 요소 블록을 `btn_drawPatternRoi`(패턴1) 직후, `btn_createPatternModel`(패턴 모델 생성) 직전으로 이동.
- StackPanel 순서: btn_teachDatum → 패턴1 → 패턴2 → 패턴 모델 생성 → btn_swapHorizontal.
- 세 버튼의 x:Name / Click / Style / Background / Template / ToolTip / Margin 무변경 — 요소 순서만 이동.
- 이동 블록 위에 `//260619 hbk Phase 57 #1 ...` XAML 주석 추가.
- 검증: 라인번호 순서 패턴1(181) < 패턴2(204) < 모델생성(226) 확인.

### Task 2: InvokeCreatePatternModel 패턴2 미설정 경고+override (25f1b71)
- 패턴1 가드(`PatternRoi_Length1/2 <= 0` → 하드 블록) 무변경.
- 패턴1 가드 직후, modelPath 도출 직전에 패턴2 판정 삽입:
  - `datum.PatternRoi2_Length1 <= 0.0 || datum.PatternRoi2_Length2 <= 0.0` → `CustomMessageBox.ShowConfirmation("패턴 2 미설정", ..., MessageBoxButton.OKCancel)`.
  - `confirm != MessageBoxResult.OK` → `return;` (모델 생성 중단). OK → 단일 패턴 진행(override).
- ShowConfirmation 반환값 사전 확인: MessageBoxWindow.xaml.cs:86 에서 OKCancel 긍정 버튼 = `MessageBoxResult.OK` → 플랜 코드 그대로 사용 (YesNo 대체 불필요).
- `using System.Windows;` (MessageBoxResult/MessageBoxButton) 이미 존재 (line 7) — 추가 0.
- 패턴2 모델 생성 분기(:2826~)와 단일 패턴 alignMsg(:2845~) 무변경.

### Task 3: SIMUL_MODE Debug/x64 빌드 검증
- MSBuild Debug/x64 Rebuild + Build 수행.
- error CS / XAML markup error 0, 신규 warning CS 0 (잔존 warning 은 전부 Phase 33 baseline — CS0618 deprecated Sequence/Action, CS0162 VirtualCamera 도달불가, MSB3884 ruleset 누락).
- `DatumMeasurement.exe` 생성 확인.
- source 변경 없음 → 별도 commit 없음.

## Deviations from Plan

None - plan executed exactly as written. ShowConfirmation 반환 enum(OK)이 플랜 예상과 일치하여 분기 대체 불필요.

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ContentItem/MainView.xaml (버튼 순서 패턴1<패턴2<모델생성)
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs (ShowConfirmation 패턴2 경고)
- FOUND: commit a179c22 (Task 1)
- FOUND: commit 25f1b71 (Task 2)
- Build: 0 error CS / markup error, DatumMeasurement.exe 생성

## Verification Status

- [x] 패턴1/패턴2 버튼 인접 배치 (라인 181 < 204 < 226)
- [x] 패턴2 미설정 모델 생성 → 경고 → OK=override / Cancel=중단
- [x] 패턴1 미확보 하드 블록 유지
- [x] 단일 패턴 폴백 경로(InspectionSequence) 무변경
- [x] MSBuild Debug/x64 → error 0, 신규 warning 0
