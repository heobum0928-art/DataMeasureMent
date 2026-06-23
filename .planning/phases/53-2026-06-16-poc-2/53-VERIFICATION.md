---
phase: 53-2026-06-16-poc-2
verified: 2026-06-23T00:00:00Z
status: human_needed
score: 12/12 must-haves verified (code/build); UAT pending
overrides_applied: 0
human_verification:
  - test: "체커보드 캘리브 창 진입 + SIMUL 라이브 버튼 비활성 (D-04)"
    expected: "MainView 툴바 [체커보드 캘리브] 버튼 → 창 열림. SIMUL_MODE 빌드에서 [라이브 촬상] 버튼 비활성 + ToolTip 안내. [이미지 로드]만 가능."
    why_human: "WPF 창 표시/버튼 상태는 앱 실행 없이 검증 불가. #if SIMUL_MODE 분기는 코드 확인 완료(코드 PASS)."
  - test: "이미지 로드 → 칸 크기 입력 → [검출] → 리포트 표시 (mm/px·X/Y·평균간격·코너수·편차%)"
    expected: "체커보드 이미지(인터넷/실측) 로드 → 우측 뷰어 표시 → 칸 크기(mm) 입력 → [검출] → txt_report 에 1px=N mm + X/Y + 코너수 + 중앙↔외곽 편차% 표시. 코너<12 또는 검출 실패 시 한국어 에러."
    why_human: "실 검출 정확도/리포트 값은 실제 체커보드 이미지 + HALCON saddle_points_sub_pix 런타임 실행 필요. 실측 체커보드 이미지 미확보 → 정확도 검증 보류."
  - test: "외곽 왜곡 큰 이미지 → 1% 초과 시 빨강 경고 라벨 (D-05)"
    expected: "외곽 왜곡이 큰 격자 이미지 → CenterOuterDeviationPct > 임계(1%) 시 lbl_distortionWarn 빨강 표시. 텔레센트릭 정상 이미지는 경고 없음."
    why_human: "왜곡% 게이트 동작은 왜곡이 있는 실 이미지로만 시각 확인 가능. 산출 로직은 코드 PASS."
  - test: "[적용] 게이트 + 확인 모달 + 활성 시퀀스 전체 shot 반영 (D-03/D-06)"
    expected: "검출 전 [적용] 비활성 → 검출 성공 후 활성. [적용] → '활성 시퀀스 [TOP] 전체 SHOT 덮어쓰기' OKCancel 확인 모달 → OK → 'N개 SHOT 적용 + 저장 완료' 표시."
    why_human: "확인 모달 표시/사용자 OK 흐름 + 실제 PixelResolution 일괄 set 결과는 런타임 상태 변경. 코드 경로(루프/필터/SaveRecipe) PASS."
  - test: "재시작 영속 + 비활성 시퀀스 데이터 소실 0 + 2점 캘리브 회귀 0"
    expected: "[적용] 후 앱 재시작 → 해당 시퀀스 shot PixelResolution 산출값 유지. 다른 시퀀스 Datum/데이터 소실 0 (SaveRecipe existingFile 보존, 3faa91b). 기존 2점 캘리브 버튼 동작 회귀 0."
    why_human: "영속/재시작/회귀는 실행+재기동 사이클 필요. SaveRecipe 재사용(existingFile 보존 가드)과 기존 ApplyCalibrationResult 무수정은 코드/diff PASS."
---

# Phase 53: 픽셀 캘리브레이션 (체커보드) Verification Report

**Phase Goal:** 체커보드(격자 white/black) 기반 픽셀 캘리브레이션 — 라이브 정지/촬상 또는 이미지 로드로 격자 이미지를 입력받아 픽셀 해상도(mm/px)를 산출하고, 별도 캘리브 창에서 측정 PixelResolution 에 반영한다.
**Verified:** 2026-06-23
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth | Status | Evidence |
| --- | ----- | ------ | -------- |
| 1   | 체커보드 이미지를 saddle_points_sub_pix 로 코너 검출해 mm/px 산출 | ✓ VERIFIED | `CheckerboardCalibrationService.cs:59` `SaddlePointsSubPix(image,"facet",...)` → `:98` `mmPerPixel = knownMmPerCell / medGap` |
| 2   | 누락 코너에 강건한 median 기반 간격 통계 (단일 평균 + X/Y 리포트) | ✓ VERIFIED | `:253` `private static double Median` (정렬 후 중앙값), `:88-90` medGapX/Y → medGap 평균, `:99-100` X/Y 리포트값 |
| 3   | 중앙↔외곽 편차% 산출 + 임계 초과 경고 플래그 | ✓ VERIFIED | `:274` `ComputeCenterOuterDeviationPct`, `:113` `IsDistortionWarn = devPct > distortionWarnPct` |
| 4   | 신규 서비스 csproj 등록 + Debug/x64 빌드 통과 | ✓ VERIFIED | csproj `:541` `<Compile Include="Halcon\Algorithms\CheckerboardCalibrationService.cs"/>` · MSBuild Debug/x64 exit 0 (재실행 확인) |
| 5   | 별도 캘리브 창에서 칸 크기 입력 + 이미지 로드/라이브 촬상 | ✓ VERIFIED | `CalibrationWindow.xaml:81` txt_cellMm, `:106-107` btn_detect/btn_apply, `.cs:39` LoadImageButton_Click, `:60` GrabImageButton_Click |
| 6   | [검출] 시 mm/px + 외곽 편차% 리포트 + 임계 초과 경고 라벨 | ✓ VERIFIED | `CalibrationWindow.xaml.cs:115` `_calibService.TryCalibrate(...)` → `:124` txt_report, `:129-133` lbl_distortionWarn Visible |
| 7   | SIMUL_MODE 라이브 촬상 버튼 비활성 (이미지 로드만) | ✓ VERIFIED | `CalibrationWindow.xaml.cs:32-35` `#if SIMUL_MODE` btn_liveCapture.IsEnabled=false + ToolTip |
| 8   | 산출 결과를 외부(MainView)가 [적용]에 쓰도록 노출 | ✓ VERIFIED | `:20` `public CalibrationResult LastResult`, `:23` `public event Action<CalibrationResult> ApplyRequested`, `:148-150` ApplyButton_Click 발화 |
| 9   | MainView 에서 캘리브 창 열기 + 라이브 grab 델리게이트 주입 | ✓ VERIFIED | `MainView.xaml.cs:2396` `OpenCheckerboardCalibrationWindow` → `:2398` ImageGrabber=GrabCalibrationImage, `MainView.xaml:310` 진입 버튼 |
| 10  | [적용] 시 활성 시퀀스 전체 shot PixelResolution 일괄 반영 (D-03) | ✓ VERIFIED | `MainView.xaml.cs:2366-2380` Shots 루프 `owner != activeSeq) continue` → `shot.PixelResolution = mmPerPixel` + fai.PixelResolutionX/Y |
| 11  | 반영 후 SaveRecipe 로 영속화 (재시작 유지) | ✓ VERIFIED | `:2383-2384` `Window.GetWindow(this) as MainWindow` → `mw.SaveRecipe()` (MainWindow.xaml.cs:263 existingFile 보존) |
| 12  | 반영 전 사용자 확인 게이트 (D-06) | ✓ VERIFIED | `:2354-2355` `CustomMessageBox.ShowConfirmation(...,OKCancel)` → `if (confirm != MessageBoxResult.OK) return` (btn_apply 게이트 + 확인 모달 2중) |

**Score:** 12/12 truths verified at code/build level (런타임 UAT 별도 — human_verification)

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `Halcon/Algorithms/CheckerboardCalibrationService.cs` | 코너검출+mm/px+편차% 서비스 | ✓ VERIFIED | 393 라인, SaddlePointsSubPix/Median/ComputeCenterOuterDeviationPct 모두 존재, csproj 등록, MainView/CalibrationWindow 에서 소비 (WIRED) |
| `Halcon/Algorithms/...CalibrationResult` | 결과 계약 클래스 | ✓ VERIFIED | `:383` `class CalibrationResult` 7 프로퍼티 (MmPerPixel/X/Y/MeanSpacingPx/CenterOuterDeviationPct/IsDistortionWarn/CornerCount) |
| `UI/Dialog/CalibrationWindow.xaml` | 캘리브 창 레이아웃 | ✓ VERIFIED | x:Class 매치, CalibrationViewer(HalconViewerControl), btn_detect/btn_apply(IsEnabled=False), lbl_distortionWarn, txt_cellMm |
| `UI/Dialog/CalibrationWindow.xaml.cs` | 입력/검출/리포트/결과 노출 | ✓ VERIFIED | TryCalibrate 호출, ApplyRequested/LastResult/ImageGrabber 노출, MainView 에서 소비 (WIRED) |
| `UI/ContentItem/MainView.xaml.cs` | launch + 일괄 반영 + 저장 | ✓ VERIFIED | OpenCheckerboardCalibrationWindow/ApplyCheckerboardCalibration/GrabCalibrationImage, 진입 버튼 핸들러 (WIRED) |
| `UI/ContentItem/MainView.xaml` | 진입 버튼 (deviation) | ✓ VERIFIED | `:310` btn_checkerboardCalibrate + Click=OpenCheckerboardCalibrationButton_Click (plan 외 추가 — dead code 방지, Rule 2) |
| `DatumMeasurement.csproj` | Compile/Page 등록 | ✓ VERIFIED | `:541` 서비스 Compile, `:555/564` CalibrationWindow Compile(DependentUpon)+Page |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | --- | --- | ------ | ------- |
| CheckerboardCalibrationService.TryCalibrate | HOperatorSet.SaddlePointsSubPix | HALCON 코너 검출 | ✓ WIRED | `:59` 호출 + try/catch return false, 결과 rows/cols 로 격자 통계 |
| CalibrationWindow.DetectButton_Click | CheckerboardCalibrationService.TryCalibrate | CurrentImage + knownMm 전달 | ✓ WIRED | `:115` `_calibService.TryCalibrate(CalibrationViewer.CurrentImage, mm, sigma, thr, warnPct, ...)` |
| CalibrationWindow.ApplyButton_Click | ApplyRequested 이벤트 | 외부 위임 | ✓ WIRED | `:148-150` `_lastResult != null && ApplyRequested != null` → 발화 |
| MainView ApplyRequested 핸들러 | shot.PixelResolution | 활성 시퀀스 필터 루프 일괄 set | ✓ WIRED | `:2372` `owner != activeSeq) continue` → `:2374` set |
| MainView ApplyRequested 핸들러 | MainWindow.SaveRecipe | 반영 후 영속화 | ✓ WIRED | `:2384` `mw.SaveRecipe()` |
| MainView.OpenCheckerboardCalibrationWindow | new CalibrationWindow + ImageGrabber/ApplyRequested | launch wiring | ✓ WIRED | `:2397-2404` 생성+주입+구독+ShowDialog+finally 해제 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| CalibrationWindow.txt_report | result (CalibrationResult) | `_calibService.TryCalibrate(CalibrationViewer.CurrentImage,...)` — 실 HImage 입력 + saddle 코너 산출 | ✓ (런타임 이미지 의존) | ✓ FLOWING — 하드코딩/스텁 없음. 단 산출 정확도는 실 체커보드 이미지 UAT 필요 |
| MainView shot.PixelResolution | mmPerPixel | result.MmPerPixel ← TryCalibrate | ✓ FLOWING | 정적 폴백/하드코딩 0, recipeManager.Shots 실 데이터 소비 |
| CalibrationViewer.CurrentImage | HImage | LoadImage(path) 또는 ImageGrabber()→LoadImage | ✓ FLOWING | HalconViewerControl 이 보유·dispose, 검출 입력으로 재사용 (중복 할당 회피) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Debug/x64 빌드 컴파일 | MSBuild Debug/x64 //t:Build | exit 0, error 0, baseline MSB3884 만 | ✓ PASS |
| caltab/undistort 미사용 (D-07/D-08) | grep set_calibration_data\|find_caltab\|undistort\|map_image | 1건 (doc 주석 "undistort 미구현" 만 — HALCON 호출 0) | ✓ PASS |
| 빌드 산출물 존재 | ls bin/x64/Debug/DatumMeasurement.exe | 존재 (1.5MB) | ✓ PASS |
| 실 체커보드 검출 정확도 | (런타임 실행 필요) | — | ? SKIP → human |
| 적용/영속/재시작 | (런타임+재기동 필요) | — | ? SKIP → human |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ----------- | ----------- | ------ | -------- |
| CAL-01 | 53-01/02/03 | 체커보드 입력 → 픽셀 해상도(mm/px) 산출 → 측정 PixelResolution 적용 / 이미지 로드 모드 동작 | ✓ SATISFIED (code) / ? UAT | ROADMAP.md:208/247 정의(REQUIREMENTS.md 표 외 — POC 신규 #2). 알고리즘(plan 01)+창(plan 02)+반영/저장(plan 03) 전 계층 구현·빌드 PASS. UAT 동작 검증은 human |

**ORPHANED 없음.** CAL-01 은 세 plan 모두 `requirements:[CAL-01]` 로 선언하고 ROADMAP.md Phase 53 `**Requirements**: CAL-01` 와 일치. REQUIREMENTS.md 본문 표에는 별도 `**CAL-01**:` 항목이 없으나(POC 6월 신규로 ROADMAP 에만 정의), 누락/고아 아님 — Success Criteria 가 ROADMAP 섹션에 명시됨.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| CalibrationWindow.xaml.cs | (whole class) | HalconViewerControl 미dispose (WR-01) | ⚠️ Warning | 창 닫을 때마다 HALCON HImage 핸들 누수. TeachingWindow.xaml.cs:313 은 Dispose 함 — 컨벤션 누락. (53-REVIEW.md, advisory) |
| MainView.xaml.cs | 2433 | GrabCalibrationImage grabbed HImage 미dispose (WR-02) | ⚠️ Warning | 라이브 촬상마다 native HImage 1개 누수. SaveTempImage 는 borrow(소유권 미이전). (53-REVIEW.md, advisory) |
| CheckerboardCalibrationService.cs | 155-156 | doc 주석이 "1.5배" 절사 언급(실 코드엔 없음) (IN-01) | ℹ️ Info | 주석↔구현 불일치, 동작 영향 0 |
| CheckerboardCalibrationService.cs | 104,274 | centerMean/outerMean out-param 미소비 (IN-02) | ℹ️ Info | dead output, 동작 영향 0 |
| MainView.xaml.cs | 2298-2319 | 기존 2점 ApplyCalibrationResult 무선택 시 silent no-op (IN-03) | ℹ️ Info | 본 phase 도입 아님(인접), 신규 경로는 SEQ_TOP 폴백으로 처리 |
| CheckerboardCalibrationService.cs | 297-298 | 0.33/0.66 radial band 인라인 리터럴 (IN-04) | ℹ️ Info | 다른 튜너블은 const 노출 — 일관성 권고, 저우선 |

> WR-01/WR-02 는 메모리 핸들 누수로 기능 정확성에는 영향 없음(블로커 아님). 캘리브 창은 빈번 사용 UI 가 아니므로 즉시 위험 낮음. **goal 달성을 막지 않음** — advisory 로 기록, 후속 cleanup 권장.

### Human Verification Required

런타임/시각/재기동 의존 UAT 5건 (frontmatter `human_verification` 참조). 핵심:
1. 캘리브 창 진입 + SIMUL 라이브 버튼 비활성 (D-04)
2. 이미지 로드 → 칸 크기 → [검출] → 리포트 (mm/px·X/Y·코너수·편차%)
3. 외곽 왜곡 이미지 → 1% 초과 빨강 경고 (D-05)
4. [적용] 게이트 + 확인 모달 + 활성 시퀀스 전체 shot 반영 (D-03/D-06)
5. 재시작 영속 + 비활성 시퀀스 소실 0 + 2점 캘리브 회귀 0

> **참고:** 실측 체커보드 이미지 미확보 상태(MEMORY: 왜곡검증 보류). 텔레센트릭 전제로 mm/px 산출만 구현(D-07/D-08 LOCK) — 풀 카메라 캘리브/undistort 부재는 **의도된 범위**이며 gap 아님.

### Gaps Summary

**기능 gap 없음.** CAL-01 의 3계층(알고리즘 서비스 / 캘리브 창 / 반영·저장 wiring)이 모두 코드로 구현되고 csproj 등록 + Debug/x64 빌드 PASS(재검증 exit 0). 12/12 observable truth 가 코드/빌드 레벨에서 VERIFIED, 6/6 key link WIRED, caltab/undistort 토큰 0(D-07/D-08 준수), 기존 2점 캘리브 무수정(회귀 가드).

남은 것은 **런타임 동작 UAT**(창 표시·실 검출 정확도·왜곡 경고·적용 모달·재시작 영속·회귀)뿐이며, 자동 검증 불가(앱 실행 + 실/다운로드 체커보드 이미지 필요)이므로 human_verification 으로 분류한다. 따라서 status=human_needed (gaps_found 아님).

advisory: HALCON 핸들 누수 2건(WR-01 CalibrationWindow 뷰어 미dispose, WR-02 grabbed 이미지 미dispose) — 기능 정확성 무영향이나 후속 cleanup 권장.

---

_Verified: 2026-06-23_
_Verifier: Claude (gsd-verifier)_
