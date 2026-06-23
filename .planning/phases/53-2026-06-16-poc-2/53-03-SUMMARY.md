---
phase: 53-2026-06-16-poc-2
plan: 03
subsystem: UI (Calibration apply/persist wiring)
tags: [calibration, checkerboard, wpf, pixel-resolution, recipe-save]
requires:
  - CalibrationWindow.ImageGrabber / LastResult / ApplyRequested (plan 02)
  - CalibrationResult 계약 (plan 01)
  - InspectionRecipeManager.Shots / ShotConfig.PixelResolution (Phase 42 단일소스)
  - MainWindow.SaveRecipe (existingFile 보존 가드, 3faa91b)
provides:
  - MainView.OpenCheckerboardCalibrationWindow (public 진입점)
  - MainView.ApplyCheckerboardCalibration (활성 시퀀스 전체 shot 일괄 반영 + 저장)
  - MainView.GrabCalibrationImage (라이브 grab 델리게이트)
  - MainView btn_checkerboardCalibrate (UI 진입 버튼)
affects:
  - WPF_Example/UI/ContentItem/MainView.xaml (진입 버튼 추가 — files_modified 외 deviation)
tech-stack:
  added: []
  patterns:
    - "ApplyRequested 이벤트 구독 → 활성 시퀀스 전체 shot PixelResolution 일괄 set (D-03)"
    - "ShowConfirmation(OKCancel) 확인 게이트 (D-06)"
    - "MainWindow.SaveRecipe 재사용 (직접 INI 쓰기 금지, existingFile 보존)"
    - "TeachingWindow launch 패턴 차용 (ImageGrabber 주입 + try/finally 핸들러 해제)"
key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
decisions:
  - "활성 시퀀스 식별 = 선택 FAI(MeasurementResultRow) owner.OwnerSequenceName → 없으면 SequenceHandler.SEQ_TOP 폴백 (open question 1 해소)"
  - "D-06 확인 게이트 = CustomMessageBox.ShowConfirmation(OKCancel) MessageBoxResult.OK 비교 (Yes/No 전용 오버로드 없음 → OKCancel 채택)"
  - "진입점 = public OpenCheckerboardCalibrationWindow + MainView.xaml btn_checkerboardCalibrate 버튼 (MainWindow 메뉴 없음 → MainView 툴바에 1버튼 추가)"
  - "GrabCalibrationImage = 활성 시퀀스 첫 shot(ShotConfig=ICameraParam) grab → HalconTeachingHelper.SaveTempImage 경로, 실패 시 null (throw 금지)"
metrics:
  duration_min: 9
  completed: 2026-06-23
  tasks: 2
  files: 2
---

# Phase 53 Plan 03: 체커보드 캘리브 반영·저장 wiring Summary

MainView 에 체커보드 픽셀 캘리브 창 진입점을 추가하고, plan 02 의 `ApplyRequested` 이벤트를 구독해 산출 mm/px 를 **활성 시퀀스 전체 shot** 의 `PixelResolution` 에 일괄 반영(D-03)한 뒤 사용자 확인 모달(D-06)을 거쳐 `MainWindow.SaveRecipe()` 로 영속화한다. 기존 2점 캘리브(`FinishCalibration`/`ApplyCalibrationResult`)는 무수정 유지(회귀 0), 체커보드 경로는 신규 메서드로 분리.

## 구현 내용

### Task 1 — 활성 시퀀스 전체 shot 일괄 반영 + 저장 (47b7ce8)
- `ResolveActiveSequenceForCalibration()`: 선택 FAI(`dataGrid_faiResults.SelectedItem as MeasurementResultRow` → `FindFAIByName` → `fai.Owner as ShotConfig`)의 `OwnerSequenceName`, 없으면 `SequenceHandler.SEQ_TOP` 폴백.
- `ApplyCheckerboardCalibration(CalibrationResult result)`:
  - null 가드 → `result.MmPerPixel` 추출 → 활성 시퀀스 결정.
  - `result.IsDistortionWarn` 시 경고 라인(편차% 포함) 메시지 합성.
  - **D-06 게이트:** `CustomMessageBox.ShowConfirmation("캘리브레이션 적용", msg, MessageBoxButton.OKCancel)` → `MessageBoxResult.OK` 아니면 중단.
  - `recipeManager.Shots` 루프 → `owner != activeSeq` skip → `shot.PixelResolution = mmPerPixel`(Phase 42 단일소스) + `fai.PixelResolutionX/Y`(INI 호환).
  - **저장:** `Window.GetWindow(this) as MainWindow` → `mw.SaveRecipe()` (Running 가드 + existingFile 보존 내장).
  - 완료 모달 `"N개 SHOT 에 적용 + 저장 완료"`.
- 기존 `ApplyCalibrationResult(double mmPerPixel)` 무수정 (2점 캘리브 회귀 0).

### Task 2 — 캘리브 창 launch + grab 델리게이트 + 빌드 (84269ba)
- `public void OpenCheckerboardCalibrationWindow()`: `new CalibrationWindow { Owner = Window.GetWindow(this) }` → `ImageGrabber = GrabCalibrationImage` 주입 → `ApplyRequested += ApplyCheckerboardCalibration` → `ShowDialog()` → `finally` 에서 `ApplyRequested -=` (누수 해제).
- `GrabCalibrationImage()`: 활성 시퀀스 첫 shot(ShotConfig=ICameraParam) → `pDev.GrabHalconImage(camShot)` → `HalconTeachingHelper.SaveTempImage` 경로 반환. 실패 시 null (창이 안내). 전체 try/catch.
- `MainView.xaml` 툴바에 `btn_checkerboardCalibrate` 버튼 + `OpenCheckerboardCalibrationButton_Click` 핸들러 추가 → public 진입점 호출.
- `using ReringProject.Halcon.Algorithms;` 추가 (`CalibrationResult` 참조, Rule 3).
- Debug/x64 빌드 PASS (신규 error 0).

## Deviations from Plan

### Rule 2 - 누락 핵심 기능 (진입점 도달 가능화)
**1. [Rule 2] MainView.xaml 에 체커보드 캘리브 진입 버튼 추가 (files_modified 외)**
- **Found during:** Task 2
- **Issue:** 플랜은 `public OpenCheckerboardCalibrationWindow()` 만 노출하고 "MainWindow 메뉴 1줄 추가 or SUMMARY 기록"을 지시했으나, MainWindow.xaml 에 메뉴 자체가 없음(`mainView` 단일 호스트). public 메서드만으로는 호출처가 없어 기능이 도달 불가(dead code).
- **Fix:** MainView.xaml 툴바의 기존 `btn_calibrate`(2점) 옆에 `btn_checkerboardCalibrate` 버튼 1개 + `OpenCheckerboardCalibrationButton_Click` 핸들러 추가 → public 메서드 호출. 기존 2점 버튼 무수정.
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml, MainView.xaml.cs
- **Commit:** 84269ba

### Rule 3 - 블로킹 (using 누락)
**2. [Rule 3] using ReringProject.Halcon.Algorithms 추가**
- **Issue:** `CalibrationResult` 가 `ReringProject.Halcon.Algorithms` 네임스페이스인데 MainView.xaml.cs 미import → CS0246 컴파일 차단.
- **Fix:** using 1줄 추가.
- **Commit:** 47b7ce8

### D-06 게이트 시그니처 조정
- 플랜 pseudo-code 는 `CustomMessageBox.Show(..., Yes/No)` 를 암시했으나 `CustomMessageBox` 에 Yes/No 전용 오버로드 없음. `ShowConfirmation(title, message, MessageBoxButton)` → `MessageBoxResult` 가 실제 확인 게이트 API(기존 Phase 57 #1 사용처와 동일). `MessageBoxButton.OKCancel` 채택, `MessageBoxResult.OK` 비교. plan 02 [적용] 버튼이 1차 게이트, 본 확인 모달이 2차 게이트(2중 안전장치, T-53-02 mitigate).

## Commits

- `47b7ce8` feat(53-03): 활성 시퀀스 전체 shot 일괄 반영 + 확인 모달 + SaveRecipe (D-03/D-06)
- `84269ba` feat(53-03): 체커보드 캘리브 창 launch + ImageGrabber/ApplyRequested wiring + 진입 버튼

## Verification

- **Task 1 grep:** `private void ApplyCheckerboardCalibration(CalibrationResult result)`, `ResolveActiveSequenceForCalibration`, 루프 내 `shot.PixelResolution = mmPerPixel` + `fai.PixelResolutionX = mmPerPixel`, `owner != activeSeq) continue`, `mw.SaveRecipe()`, `ShowConfirmation(... OKCancel)` 매치. 기존 `ApplyCalibrationResult(double mmPerPixel)` 시그니처/본문 무변경 (diff 상 해당 메서드 라인 미변경 — 신규 메서드만 삽입).
- **Task 2 grep:** `new CalibrationWindow`, `window.ImageGrabber = GrabCalibrationImage`, `window.ApplyRequested += ApplyCheckerboardCalibration`, `public void OpenCheckerboardCalibrationWindow`, `ApplyRequested -=` 매치.
- **빌드:** MSBuild Debug/x64 PASS — exit 0, 신규 error 0 (기존 CS0618/CS0162/MSB3884 baseline 유지). `DatumMeasurement.exe` 생성 확인.

## Threat Surface

threat_model 의 T-53-02(데이터 손실) / T-53-03(오판정) 모두 본 plan 에서 mitigate 적용 확인:
- T-53-02: `ShowConfirmation` 확인 게이트 + `MainWindow.SaveRecipe`(existingFile 보존, 비활성 시퀀스 소실 가드) 경유.
- T-53-03: 메시지에 `IsDistortionWarn` 시 외곽 왜곡% 경고 라인 고지.

신규 surface 없음 (로컬 UI, 네트워크/인증/스키마 변경 0).

## Known Stubs

없음 — 반영·저장·진입점 전 계층 wiring 완료. 본 plan 으로 plan 02 `ApplyButton_Click` stub(이벤트 위임만) 해소.

## 잔여 (SIMUL 수동 UAT — verify-work 단계, 사용자 수행)

플랜 `<verification>` 6항목은 autonomous:false 정책상 human 검증 단계로 이관(self-approve 금지):
1. 진입 버튼([체커보드 캘리브]) → 창 열림, 라이브 버튼 비활성(SIMUL)
2. 체커보드 이미지 로드 → 칸 크기 입력 → [검출] → 리포트
3. [적용] → "활성 시퀀스 [TOP] 전체 SHOT 덮어쓰기" 확인 모달 → 예 → "N개 SHOT 적용 + 저장 완료"
4. 앱 재시작 → 해당 시퀀스 shot PixelResolution 산출값 유지 (영속)
5. 다른 시퀀스 Datum/데이터 소실 0 (existingFile 보존)
6. 기존 2점 캘리브 버튼 동작 회귀 0

## Self-Check: PASSED

- MainView.xaml.cs / MainView.xaml 존재 확인
- 53-03-SUMMARY.md 존재 확인
- 커밋 47b7ce8 / 84269ba 모두 git log 존재 확인
