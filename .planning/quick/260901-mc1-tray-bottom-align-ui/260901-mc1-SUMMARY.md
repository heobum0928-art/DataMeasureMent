---
phase: quick-260901-mc1
plan: 01
subsystem: ui
tags: [halcon, wpf, ethernet-vision, calibration, tcp, systemsetting]

requires: []
provides:
  - "Tray/Bottom 정렬 화면 이미지 저장 버튼(btn_saveImage, AlignCapture 하위 bmp 저장)"
  - "Bottom 피커 캘리브(Cal 모델 티칭/스텝 추가)가 실HW 빌드에서도 폴더 로더 오프라인 영상으로 동작"
  - "피커센터 계산 결과에 편심원 피팅 잔차(RMS/최대, µm+px)를 표시"
  - "find_shape_model 최소 Score 가 운영자 조절 가능한 SystemSetting 값으로 전환"
affects: [ethernet-vision, bottom-align-calibration, picker-center]

tech-stack:
  added: []
  patterns:
    - "TryResolveCalSourceImage: CurrentImagePath 존재 여부로 오프라인/라이브 이미지 소스를 런타임 판별 + bOwnsImage 소유권 플래그"
    - "PickerCenterCalibrationService 6-out 오버로드 + 4-out 얇은 래퍼로 하위호환 유지"
    - "SystemSetting Category(\"Path|AlignVerify\") + Restore*Default() 가드 패턴으로 신규 설정값 도입"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/UI/TrayVisionView.xaml
    - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
    - WPF_Example/Custom/UI/BottomVisionView.xaml
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
    - WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/Custom/SystemSetting.cs

key-decisions:
  - "오프라인 소스 판별은 별도 토글 없이 _viewer.CurrentImagePath 비어있지 않음 AND CurrentImage != null 조합으로만 판단(LoadImage(string) 만 경로를 채움)"
  - "IsOpen 게이트로 카메라 폴백 정지이미지가 조용히 누적되는 것을 원천 차단"
  - "OpenFolderButton_Click 의 LoadSimulFolder 는 실HW 로 열지 않는다(카메라 유실 시 TCP STEP 이 낡은 폴더 이미지를 조용히 소비할 위험)"
  - "피팅 잔차는 표시 전용 — 반경 가드(MIN/MAX_RADIUS_PX) 외의 새 실패 분기를 추가하지 않음(임계값 실측 데이터 부재)"
  - "find_shape_model FindMinScore 를 SystemSetting.PickerCalFindMinScore 설정값으로 전환하면서 등급 산정 기준(FindMinScore 정적 프로퍼티)도 동일 값을 따라가도록 통일"

requirements-completed: [ALIGNUI-01, ALIGNUI-02, ALIGNUI-03]

coverage:
  - id: D1
    description: "Tray/Bottom 이미지 저장 버튼 — 현재 화면 영상을 bmp 로 저장, 이미지 없으면 안내만"
    requirement: "ALIGNUI-01"
    verification: []
    human_judgment: true
    rationale: "실제 카메라/화면 조작과 파일 저장 다이얼로그 확인이 필요 — Task 5 실기 UAT 항목 1~3"
  - id: D2
    description: "Bottom 피커 캘리브가 실HW 빌드에서 폴더 로더 오프라인 영상으로 티칭/스텝/자동넘김 수행"
    requirement: "ALIGNUI-02"
    verification: []
    human_judgment: true
    rationale: "실제 회전 지그 영상 세트와 실HW 빌드 실행이 필요 — Task 5 실기 UAT 항목 4~10"
  - id: D3
    description: "피커센터 계산 결과에 피팅 잔차(RMS/최대, µm+px)가 표시되고 잔차가 커도 계산은 성공 유지"
    requirement: "ALIGNUI-03"
    verification: []
    human_judgment: true
    rationale: "6장 이상 누적 후 실제 계산 버튼 클릭 결과 확인 필요 — Task 5 실기 UAT 항목 8"
  - id: D4
    description: "find_shape_model 최소 Score 를 캘 패널 콤보에서 운영자가 조절 가능(SystemSetting.PickerCalFindMinScore)"
    verification: []
    human_judgment: true
    rationale: "화면 콤보 조작 + 저장/재시작 후 값 유지 여부는 실기 확인이 필요 — 플랜 범위 밖 후속 요청이라 별도 UAT 항목 없음, Task 5 UAT 6~8 수행 시 함께 확인 권장"

duration: 45min
completed: 2026-09-01
status: complete
---

# Quick Task 260901-mc1: Tray/Bottom 정렬 화면 이미지 저장 + Bottom 오프라인 캘리브 + 피팅 잔차 표시 Summary

**실HW 빌드에서 정렬 영상 저장 → 폴더로 불러오기 → 그 영상으로 피커 캘리브 → 편심원 피팅 잔차(µm/px)를 숫자로 확인하는 흐름을 코드로 완성. find_shape_model 최소 Score 도 설정값으로 전환.**

## Performance

- **Duration:** 약 45분 (Task 2~6, API 중단 재개 포함. Task 1 은 이전 세션에서 이미 커밋 완료)
- **Completed:** 2026-09-01
- **Tasks:** 6 (Task 1 이전 세션 완료 확인, Task 2/3/4/6 이번 세션 실행, Task 5 는 실기 UAT 체크포인트로 보류)
- **Files modified:** 7

## Accomplishments

- Tray/Bottom 두 화면에 [이미지 저장] 버튼 — `AlignCapture` 하위 bmp 무압축 저장, 이미지 없으면 크래시 없이 안내만 (이전 세션 완료, 이번 세션에서 재확인)
- `TryResolveCalSourceImage` 신설로 Bottom 피커 캘리브(`CalTeachModelButton_Click`/`CalAddStepButton_Click`)를 `#if SIMUL_MODE` 컴파일 분기에서 런타임 판단으로 전환 — `bOwnsImage` 로 뷰어 소유(Dispose 금지) vs 호출자 소유(Dispose 책임)를 명확히 구분
- 오프라인 이미지로 스텝을 잡을 때만 자동으로 다음 이미지로 넘어가도록 배선(라이브 grab 스텝은 자동 넘김 대상 아님)
- `PickerCenterCalibrationService.ComputeFitResiduals` 신설(순수 C# 산술, 신규 HALCON 호출 없음) + `TryComputePickerCenter` 6-out 오버로드(잔차 RMS/최대, px). 기존 4-out 은 얇은 래퍼로 하위호환 유지, 반경 가드·성공/실패 계약 무변경
- Bottom 계산 버튼이 잔차를 µm(px 병기)로 저장 확인창 + `lbl_calStatus` 에 표시(`ToCircularityScore` + `FIT_SCORE_MIN` 로 기호 등급만 부여, `GradeBrush` 색칠은 하지 않음). TCP `$ALIGN_CALIB` END 로그에도 잔차 병기
- (추가 요청) `PickerCenterCalibrationService.FIND_MIN_SCORE` 하드코딩을 `SystemSetting.PickerCalFindMinScore` 로 전환 — 캘 패널에 `cmb_calFindMinScore` 콤보 추가, 등급 산정 기준(`FindMinScore` 정적 프로퍼티)도 동일 값을 따라가도록 통일

## Task Commits

Task 1 은 이전 세션에서 이미 완료·커밋되어 있었다(이번 세션 시작 시 git 으로 확인):

0. **Task 1: Tray/Bottom 두 뷰에 [이미지 저장] 버튼 추가** - `2427471c` (feat, 이전 세션)

이번 세션에서 실행:

1. **Task 2: Bottom 피커 캘리브 소스를 컴파일 분기에서 런타임 판단으로 전환** - `4311d7d4` (feat)
2. **Task 3: 피커센터 편심원 피팅 잔차(RMS/최대) 산출 및 표시** - `292eb8a8` (feat)
3. **Task 6 (추가 요청): find_shape_model 최소 Score 를 운영자 설정값으로 전환** - `d028d126` (feat)
4. **하드룰 준수 정리(Task 4 검증 중 발견)** - `2e8333de`, `65789b1b` (style)
5. **Task 4: 하드룰 검증 + Debug|x64 빌드** - 커밋 없음(검증/빌드 전용 태스크, 위반 발견 시 즉시 style 커밋으로 수정)

_Note: Task 5(실기 UAT)는 gate="blocking" 체크포인트로 보류 — 아래 "Next Phase Readiness" 참고._

## Files Created/Modified

- `WPF_Example/Custom/UI/TrayVisionView.xaml` / `.cs` - 이미지 저장 버튼(Task 1, 이전 세션)
- `WPF_Example/Custom/UI/BottomVisionView.xaml` - 이미지 저장 버튼(Task 1) + 캘 최소 Score 콤보(Task 6)
- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` - `TryResolveCalSourceImage`, 캘 티칭/스텝 런타임 전환, 잔차 표시, 최소 Score 콤보 배선
- `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` - `ComputeFitResiduals`, 6-out `TryComputePickerCenter`, `FindMinScore` 가 설정값을 읽도록 전환
- `WPF_Example/Custom/SystemHandler.cs` - TCP `$ALIGN_CALIB` END 로그에 잔차 병기(6-out 호출로 전환)
- `WPF_Example/Custom/SystemSetting.cs` - `PickerCalFindMinScore` 신규 설정 프로퍼티 + 복원 가드

## Decisions Made

- 오프라인/라이브 소스 판별에 별도 설정 토글을 두지 않고 `_viewer.CurrentImagePath` 비어있지 않음 + `CurrentImage != null` 조합만으로 판단 — `LoadImage(string)`(폴더 로더) 만 경로를 채우고 `LoadImage(HImage)`(Grab/Live)는 null 로 지우는 기존 계약을 그대로 활용
- `EthernetAlignCamera.IsOpen` 을 명시적으로 확인해 카메라 미연결 시 조용히 폴백 정지이미지가 캘 데이터에 섞이는 것을 차단
- `OpenFolderButton_Click` 의 `LoadSimulFolder` 는 실HW 에서 절대 열지 않음 — 근거를 코드 주석으로 남김(등록 해제 경로가 없어 카메라 유실 시 TCP STEP 이 낡은 이미지를 조용히 소비할 위험)
- 피팅 잔차는 표시 전용으로 확정 — 반경 가드 외 새 실패 분기 없음. 임계값은 현장 실측 산포가 쌓이기 전까지 미설정(0.80 은 표시 등급 전용 1차 기준)
- `PickerCalFindMinScore` 도입 시 `PickerCalStepAngleDeg` 선례(같은 Category, `RestoreAlignVerifyDefaults` 가드, 즉시 `Save()` 없이 런타임 프로퍼티만 갱신)를 그대로 미러링

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] 신규 주석에 날짜+hbk 표기(CLAUDE.md 하드룰 위반)가 섞임**
- **Found during:** Task 4 (하드룰 검증)
- **Issue:** Task 2/3/6 작성 중 습관적으로 `260901 hbk` 형태의 날짜 주석을 여러 곳에 추가함 — 2026-06-11 정책 전환 이후 신규 날짜 주석은 금지
- **Fix:** 전량 `quick-mc1` 라벨로 교체. 기존 레거시 `260630 hbk` 주석과 같은 줄에 이어붙인 1건은 별도 줄로 분리해 검증 grep 이 레거시 표기를 신규로 오인하지 않게 함. 코드 로직 변경 없음
- **Files modified:** BottomVisionView.xaml.cs, BottomVisionView.xaml, PickerCenterCalibrationService.cs, SystemHandler.cs, SystemSetting.cs
- **Verification:** `git diff -U0 2427471c..HEAD | grep -E '//[0-9]{6} '` → 0건
- **Committed in:** `2e8333de`, `65789b1b`

---

**Total deviations:** 1 auto-fixed (Rule 1 — 자체 작성 코드의 하드룰 위반, 즉시 수정)
**Impact on plan:** 코드 로직 변경 없이 주석만 정리. 스코프 밖 영향 없음.

## Additional Scope (사용자 요청 — Task 6)

플랜 범위 밖으로, 실기 사용 중 사용자가 추가 요청한 항목:

- `PickerCenterCalibrationService.FIND_MIN_SCORE`(하드코딩 0.5)를 `SystemSetting.PickerCalFindMinScore` 로 전환해 캘 패널에서 운영자가 조절 가능하게 함(지그 검출 실패 시 현장에서 낮춰볼 수 있도록)
- `FIND_GREEDINESS`(0.7)/`FIND_MAX_OVERLAP`(0.5)은 요청 범위 밖이라 그대로 둠
- 등급 산정 기준으로 쓰이는 `FindMinScore` 정적 프로퍼티도 동일 설정값을 반환하도록 통일(호출부: `BottomVisionView.xaml.cs`, `SystemHandler.cs` 무변경 — 값의 출처만 바뀜)
- UI: 캘 패널에 `cmb_calFindMinScore` 콤보(0.3~0.7, 0.1 단위) 추가, `PickerCalStepAngleDeg` 콤보와 동일한 배선/저장 관용구

## Issues Encountered

- 세션 도중 API 오류로 중단됐다가 오케스트레이터 지시로 재개 — 재개 시점에 git 상태(Task 1 만 커밋됨)를 먼저 확인 후 Task 2 부터 이어서 실행
- Task 4 검증 과정에서 자체 작성 코드의 CLAUDE.md 날짜 주석 하드룰 위반을 발견해 즉시 수정(위 Deviations 참고)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Task 5(실기 UAT, gate="blocking")는 미실행 상태로 보류.** 아래 절차를 실제 하드웨어에서 수행해 승인해야 이번 Quick 작업이 최종 완료된다:
  1. Tray/Bottom 화면에서 [이미지 저장] 동작 확인(영상 있음/없음 두 경우)
  2. Bottom 화면에서 폴더로 저장 영상을 불러와 Cal 모델 티칭 → 스텝 추가(자동 넘김 포함) → 계산까지 끝까지 수행
  3. 계산 결과 저장 확인창/상태 라벨에 피팅 잔차(µm/px)가 표시되고, 잔차가 커도 계산이 실패로 처리되지 않는지 확인
  4. 카메라 라이브 grab 으로 스텝을 잡을 때는 폴더 이미지가 아닌 라이브 영상이 쓰이고 자동 넘김이 발생하지 않는지 확인
  5. 카메라 미연결 + 폴더 미오픈 상태에서 [스텝 추가] 시 안내만 나오고 데이터가 누적되지 않는지 확인
  6. (추가 요청) 캘 패널의 최소 Score 콤보 조작이 실제 검출 임계값에 반영되는지, 값이 재시작 후에도 유지되는지 확인
- 전체 절차는 `260901-mc1-PLAN.md` Task 5 `<how-to-verify>` 참고
- Debug|x64 빌드는 error CS 0건으로 통과 확인됨(빌드 자체는 UAT 선행 조건 충족)

---
*Phase: quick-260901-mc1*
*Completed: 2026-09-01*

## Self-Check: PASSED

All modified files exist on disk (7/7) and all commit hashes (2427471c, 4311d7d4, 292eb8a8, d028d126, 2e8333de, 65789b1b) verified present in git log. Debug|x64 build confirmed 0 `error CS`.
