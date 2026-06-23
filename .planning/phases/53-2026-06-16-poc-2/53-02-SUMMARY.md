---
phase: 53-2026-06-16-poc-2
plan: 02
subsystem: UI (Calibration Window)
tags: [calibration, checkerboard, wpf, window, mm-per-pixel]
requires:
  - CheckerboardCalibrationService.TryCalibrate (plan 01)
  - CalibrationResult 계약 (plan 01)
  - HalconViewerControl (이미지 표시 + CurrentImage)
provides:
  - CalibrationWindow (별도 WPF 창)
  - CalibrationWindow.ImageGrabber (Func<string> 주입점 — 라이브 grab)
  - CalibrationWindow.LastResult (CalibrationResult 외부 노출)
  - CalibrationWindow.ApplyRequested (Action<CalibrationResult> 적용 위임 이벤트)
affects:
  - WPF_Example/DatumMeasurement.csproj (Page/Compile 등록)
tech-stack:
  added: []
  patterns:
    - "별도 WPF Window = TeachingWindow 패턴 (2-column Grid + HalconViewerControl + StatusBar)"
    - "ImageGrabber Func<string> 주입 (caller=MainView 가 카메라 grab 델리게이트 제공)"
    - "검출 입력 = CalibrationViewer.CurrentImage (뷰어 보유 HImage 재사용, 중복 할당 회피)"
    - "결과 노출 = LastResult 프로퍼티 + ApplyRequested 이벤트 (반영/저장 wiring 은 plan 03)"
    - "#if SIMUL_MODE 라이브 버튼 비활성 (D-04)"
key-files:
  created:
    - WPF_Example/UI/Dialog/CalibrationWindow.xaml
    - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "D-01: 칸 크기(mm) 직전값 기억 = static 필드 _lastCellMm (앱 수명 내, INI 비의존). 검출 성공 시 갱신"
  - "D-04: SIMUL_MODE 라이브 촬상 버튼 비활성 + 안내 ToolTip"
  - "D-05: result.IsDistortionWarn 시 lbl_distortionWarn 빨강 라벨 표시"
  - "D-06: btn_apply 는 IsEnabled=False 시작 → 검출 성공 후에만 활성. 새 이미지 로드/촬상 시 재무효화"
  - "검출 입력은 CalibrationViewer.CurrentImage 재사용 (plan pseudo-code 의 별도 _currentImage=new HImage 대신) — 중복 HImage 할당·이중 dispose 회피"
metrics:
  duration_min: 6
  completed: 2026-06-23
  tasks: 3
  files: 3
---

# Phase 53 Plan 02: 체커보드 픽셀 캘리브 창 Summary

체커보드 픽셀 캘리브 전용 WPF Window(`CalibrationWindow`)를 TeachingWindow 패턴으로 신규 작성. 좌측에 칸 크기(mm) + saddle Sigma/Threshold/왜곡임계% 입력 + [이미지 로드]/[라이브 촬상]/[검출]/[적용] 버튼 + 리포트/왜곡 경고 라벨, 우측에 HalconViewerControl 을 배치. plan 01 의 `CheckerboardCalibrationService.TryCalibrate` 를 호출해 mm/px·X/Y·편차%를 산출·리포트하고, 산출 결과를 `LastResult` 프로퍼티 + `ApplyRequested` 이벤트로 외부에 노출(반영/저장은 plan 03 게이트).

## 다음 wave(plan 03 launch wiring)가 참조할 public 계약

### CalibrationWindow public 멤버
| 멤버 | 타입 | 용도 |
|------|------|------|
| `ImageGrabber` | `Func<string>` (get/set) | caller(MainView)가 카메라 grab 델리게이트 주입 → grab 후 imagePath 반환. 미주입 시 [라이브 촬상] 안내 후 무동작 |
| `LastResult` | `CalibrationResult` (get) | 마지막 검출 성공 결과. 검출 전/실패 시 null |
| `ApplyRequested` | `event Action<CalibrationResult>` | [적용] 클릭 시 `_lastResult` 비-null 이면 발화. plan 03 이 MainView 에서 구독해 활성 시퀀스 전체 shot PixelResolution 반영 + SaveRecipe (D-03/D-06) |

### 동작 흐름
- 생성자: txt_cellMm = 직전값(`_lastCellMm`), sigma/threshold/warnPct = 서비스 const 기본값. `#if SIMUL_MODE` → btn_liveCapture 비활성.
- [이미지 로드]/[라이브 촬상] → `CalibrationViewer.LoadImage(path)` (뷰어가 HImage 보유) → btn_apply 재무효화.
- [검출] → 입력검증(mm>0, V5) → `_calibService.TryCalibrate(CalibrationViewer.CurrentImage, ...)` → 성공 시 리포트 + (편차% 임계 초과면) 경고 라벨 + btn_apply 활성 + _lastCellMm 갱신.
- [적용] → `ApplyRequested(_lastResult)` 위임만 (반영/저장은 plan 03).

## 구현 메모

- 좌측 패널: GroupBox 3개(칸 크기 / 검출 튜닝 / 산출 리포트) + 버튼 WrapPanel. `CalibrationActionButtonStyle` 은 TeachingWindow 의 `TeachingActionButtonStyle` 통째 복사 후 rename.
- 검출 입력은 `CalibrationViewer.CurrentImage`(HalconViewerControl 이 LoadImage 시 보유·dispose 관리)를 직접 사용 → 별도 `_currentImage` 필드/HImage 중복 생성 불필요.
- `CustomMessageBox.Show` 시그니처는 `(title, message, ...)` 순서 → `Show("캘리브레이션", "칸 크기...")` 로 호출(plan pseudo-code 의 message-first 표기 교정).
- 모든 UI 텍스트 한국어, 코드 수정 라인 `//260623 hbk Phase 53` 주석.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - 블로킹/단순화] 검출 입력 소스를 별도 _currentImage → CalibrationViewer.CurrentImage 재사용**
- **Found during:** Task 2
- **Issue:** plan pseudo-code 는 로드/촬상마다 `_currentImage = new HImage(path)` 로 별도 HImage 를 보관하라고 명시. 그러나 `HalconViewerControl.LoadImage(string)` 가 이미 내부 `CurrentImage` 를 생성·보유·dispose 한다. 별도 필드를 두면 동일 이미지를 2번 디코딩하고 두 HImage 의 수명/dispose 를 따로 관리해야 해 누수·이중 dispose 위험.
- **Fix:** `_currentImage` 필드 제거, 검출 시 `CalibrationViewer.CurrentImage`(public getter) 를 TryCalibrate 입력으로 전달. null 가드 동일 유지.
- **Files modified:** WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
- **Commit:** 014df31

**2. [Rule 1 - 버그] CustomMessageBox.Show 인자 순서 교정**
- **Found during:** Task 2
- **Issue:** plan pseudo-code `CustomMessageBox.Show("칸 크기...", "캘리브레이션")` 는 (message, title) 순서이나 실제 시그니처는 `Show(string title, string message, ...)`. 그대로 두면 제목/본문이 뒤바뀌어 표시.
- **Fix:** `CustomMessageBox.Show("캘리브레이션", "칸 크기(mm)에 0보다 큰 숫자를 입력하세요.")` (title, message).
- **Files modified:** WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
- **Commit:** 014df31

## Commits

- `84ee05b` feat(53-02): CalibrationWindow.xaml 레이아웃 (TeachingWindow 패턴)
- `014df31` feat(53-02): CalibrationWindow 코드비하인드 — 검출/리포트/결과 노출
- `db6ae8e` chore(53-02): csproj CalibrationWindow Page/Compile 등록 + Debug/x64 빌드 검증

## Verification

- **Task 1 grep:** xaml 에 `x:Class="ReringProject.UI.CalibrationWindow"`, `CalibrationViewer`, `txt_cellMm/txt_sigma/txt_threshold/txt_warnPct`, `btn_loadImage/btn_liveCapture/btn_detect/btn_apply`(+Click), `lbl_distortionWarn`, `btn_apply ... IsEnabled="False"` 모두 매치.
- **Task 2 grep:** cs 에 `partial class CalibrationWindow : Window`(namespace `ReringProject.UI`), `_calibService.TryCalibrate(`, `#if SIMUL_MODE` + `btn_liveCapture.IsEnabled = false`, `double.TryParse` + `mm <= 0`, `result.IsDistortionWarn` + `lbl_distortionWarn.Visibility = Visibility.Visible`, `btn_apply.IsEnabled = true`, `public event Action<CalibrationResult> ApplyRequested`, `public Func<string> ImageGrabber` 모두 매치. `set_calibration_data|undistort|map_image` 토큰 0 (알고리즘 서비스 위임, UI 미존재).
- **Task 3:** MSBuild Debug/x64 PASS — exit 0, 신규 에러 0 (기존 CS0618/CS0162/MSB3884 baseline 유지). DatumMeasurement.exe 생성 확인.

## Known Stubs

- `ApplyButton_Click` 은 의도된 stub — `ApplyRequested` 이벤트 위임만 수행하고 실제 PixelResolution 반영/SaveRecipe 는 plan 03(D-06 게이트)이 MainView 에서 이 이벤트를 구독해 처리한다. 본 plan(02)은 입력·검출·리포트·결과 노출 계층(D-04/D-05/D-01)까지가 범위. wave 분할 의도이며 plan 03 이 해소.

## Threat Flags

없음 — 로컬 오프라인 UI. 입력은 사용자 숫자(double.TryParse 검증) + 로컬 이미지 파일만. 네트워크/인증/스키마 신규 surface 없음.

## 잔여 (UAT — 실행/sign-off 단계, plan 03 wiring 후)

SIMUL 수동 UAT 5건은 plan 03 launch-wiring(MainView → CalibrationWindow 진입 + ImageGrabber/ApplyRequested 연결) 완료 후 사용자가 수행:
1. 라이브 촬상 버튼 비활성 (D-04 SIMUL)
2. 체커보드 이미지 로드 → 우측 뷰어 표시
3. 칸 크기 입력 → [검출] → 리포트 표시
4. 외곽 왜곡 큰 이미지 → 1% 초과 시 빨강 경고 (D-05)
5. 검출 전 [적용] 비활성 → 검출 성공 후 활성 (D-06)

## Self-Check: PASSED

- CalibrationWindow.xaml 존재 확인
- CalibrationWindow.xaml.cs 존재 확인
- 53-02-SUMMARY.md 존재 확인
- 커밋 84ee05b / 014df31 / db6ae8e 모두 git log 존재 확인
