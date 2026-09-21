---
phase: 80-reviewer-ng-cycle-reinspect
verified: 2026-09-21T01:47:38Z
status: human_needed
score: 20/22 must-haves verified
behavior_unverified: 2
overrides_applied: 0
human_verification:
  - test: "PLC $TEST 자동 검사가 리뷰어 사진 로드 상태에서 들어올 때 ReviewerReinspectService.Release(RELEASE_REASON_PLC_TEST) 가 실제로 원래 사진 경로·OfflineInspectMode 를 되돌리는지, 그리고 그 뒤 자동 검사 결과가 이 phase 전과 동일한지(U-6 ②, U-11 항목4)"
    expected: "PLC $TEST 수신 즉시 노란 상태 줄이 사라지고, 자동 검사가 실제 카메라로 정상 진행되어 phase 전과 같은 판정을 낸다"
    why_human: "실물 PLC 가동이 필요 — 코드 순서(Release 가 ForceOfflineInspectModeOffForAutoTest 보다 먼저)와 14개 기존 메서드 md5 무변경은 정적으로 확인했으나 런타임 동작은 장비에서만 확인 가능. 80-HUMAN-UAT.md 에 이미 PENDING 으로 기록됨"
  - test: "레시피 변경 시 ReviewerReinspectService.HandleRecipeChanged 가 실제로 자동 해제되는지(D-80-11 경로 ③), 그리고 정적 두 장짜리(크로스-Z 없는) 기준점이 '필요 사진 없음=완전'으로 처리되는 A-80-E3 케이스"
    expected: "레시피를 바꾸면 상태 줄이 사라지고 원래 경로로 복원되며, 정적 두 장짜리 기준점 NG 행에서도 자동 Test Find 가 정상 동작한다"
    why_human: "이번 UAT 에서 두 경로 모두 시험되지 않음(80-HUMAN-UAT.md 명시 PENDING) — 코드상 구독(`OnRecipeChanged += ReviewerReinspectService.HandleRecipeChanged`)과 `IsDatumPhotoSetComplete` 로직은 확인했으나 실제 해당 레시피·해당 기준점 종류로 실기 확인 필요"
---

# Phase 80: 리뷰어 NG 사이클 사진 한 번에 불러와 파라미터 수정·재검사 Verification Report

**Phase Goal:** 리뷰어에서 NG 로 확인한 검사를 고르면, 그 검사에 쓰인 사진 전부(해당 측정 Shot 사진 + 그 측정이 쓰는 기준점 가로·세로 사진 + 같은 자재 검사에서 찍은 다른 Shot 사진)를 한 번에 메인 화면으로 불러와, 파라미터를 고치고 바로 재검사할 수 있게 한다. 함께 처리: (1) 리뷰어 사이클 전체 보기 선 겹침 버그, (2) 수동 RUN 에서 기준 ROI 수정 후 Test Find 없이 RUN 만으로 국부 기준선 재계산(PLC 자동 경로 불변), (3) 기준 ROI 시험 찾기.

**Verified:** 2026-09-21 (retroactive, 코드베이스 직접 확인 — SUMMARY.md 주장을 그대로 신뢰하지 않음)
**Status:** human_needed
**Re-verification:** No — initial verification

이 phase 는 REQ-ID 가 없고 80-CONTEXT.md 의 D-80-00~19(+함께 처리 1~3)가 계약이다. 아래는 각 결정에 대해 실제 소스 코드를 직접 열어 확인한 결과다(SUMMARY.md 의 서술은 참고만 하고, grep/sed 로 코드 자체를 재확인함).

## 검증 방법 요약

- Debug|x64 빌드: `MSBuild.exe ... -clp:ErrorsOnly` → **오류 0** (재확인, 이번 세션에서 직접 실행)
- 가독성 5종 grep(삼항 `?:` · `??` · `?.` · `switch...=>` · `hbk`) — 신규 파일 4개 전체 + 기존 파일 13개의 phase 시작(`55a5085f^`) 대비 **더한 줄에서 전부 0** (이번 세션에서 직접 실행, SUMMARY 수치와 일치)
- csproj 등록 — `grep` 으로 4개 `<Compile Include>` 직접 확인
- 회귀 md5 — phase 시작(`55a5085f^`) 대비 14개 기존 메서드 전부 byte-identical 재확인(아래 표)
- `RepeatRunService.cs` 삭제 줄 — phase 진짜 시작(`b2b33104`, 80-CONTEXT.md 커밋의 부모) 대비 전체 phase 누적 **0**
- 핵심 배선 사슬(버튼→서비스→상태줄, LoadForRow 내부 순서, RUN 노드 해석, 오프라인 확인창 게이트, 저장 보호, PLC/레시피변경/종료 자동해제, 선 겹침 수정, Local Ref stale 재계산 가드) — grep/sed 로 실제 코드 본문을 열어 순서·존재 확인

## Goal Achievement — Decision Coverage (D-80-00~19 + 함께 처리 1~3)

| # | 결정 | 상태 | 근거(파일:라인 / 코드 발췌) |
|---|------|------|------------------------------|
| D-80-00 | UI 단순 — 새 버튼 3개·상태줄 1줄·대화상자 1개뿐, 새 창·설정 없음 | ✓ VERIFIED | `ReviewerWindow.xaml:238`(버튼), `MainView.xaml:484`(상태 줄), `MainView.xaml:314`(시험 찾기 버튼) 3개뿐. 80-05 누적감사 `ui_new_xaml=0 ui_new_window=0 ui_setting_changed=0 ui_new_buttons=3 ui_new_dialog_calls=1` 재확인(SystemSetting 파일에 새 필드 없음, grep 직접 확인) |
| D-80-01 | 리뷰어 버튼 1개, NG 원인 패널 바로 아래, 크게 | ✓ VERIFIED | `ReviewerWindow.xaml:238` `btn_applyRowToMain`, `Content="이 사진으로 파라미터 수정"`, `border_ngCause` 내부에 배치 확인 |
| D-80-02 | 비활성 + 이유 한 줄, VM 이 만들어 바인딩 | ✓ VERIFIED | `ReviewerWindow.xaml:243` `txt_applyRowReason Text="{Binding DisableReasonText}"`, VM `CanApply`/`DisableReasonText` 존재(`ReviewerReinspectViewModel.cs`) |
| D-80-03 | 확인창 없이 바로 | ✓ VERIFIED | `Button_ApplyRowToMain_Click`(ReviewerWindow.xaml.cs:347) 본문 1문장 `_reinspectVm.ApplySelection(...)`, 그 안에 `MessageBox`/`CustomMessageBox.ShowConfirmation` 호출 없음(코드 직접 확인) |
| D-80-04 | 리뷰어 창 열어 둔 채(최소화) 메인 앞으로, 메뉴로 다시 열면 복원 | ✓ VERIFIED | `MainWindow.OnReviewerPhotosLoaded`(:295) `WindowState.Minimized` → `Activate()`; `PopupView` Reviewer 케이스에 `WindowState.Normal` 복원 + `Activate()` 확인(grep) |
| D-80-05 | NG Shot+측정 노드 자동 선택, 재선택해도 다시 그림 | ✓ VERIFIED | `InspectionListView.SelectShotAndMeasurement`/`FindNodeByParam`(measurement→FAI→shot 폴백)/`SelectNodeDeferred`(`IsSelected=false`→`true`, `Dispatcher.BeginInvoke(Background,...)`) 코드 직접 확인 |
| D-80-06 | Test Find 까지만 자동(대화상자 없음), RUN 은 사용자 직접 | ✓ VERIFIED | `ReviewerReinspectService.cs` 에 `.Start(All)?\(` 매치 0(그 서비스는 시퀀스를 시작하지 않음), `RunAutoTestFind`→`DatumTestFindService.TryRunFromTeachingImages(seq, datum, true, ...)`(대화상자 없음, `AskTestImageSource` 미사용) 직접 확인 |
| D-80-07 | 같은 자재 전체(GroupIntoParts 와 같은 규칙) | ✓ VERIFIED | `SavedCycleRerunPlanner.BuildPartForSingleCycle`(RepeatRunService.cs:1263)이 `CollectAutoTicks`+`GroupIntoParts`(반복검사 재사용, `BuildPlan`/`ValidatePart`와 같은 헬퍼)로 부품을 만들고 `FindPartContainingCycle`로 고른 사이클이 속한 부품을 찾음 — 직접 코드 확인 |
| D-80-08 | Z 후보 전부, 없으면 선택 z 1장+안내 | ✓ VERIFIED | `ApplyPartPaths` 안 `shot.RerunZRangeImagePaths = BuildZRangeMap(part, shot.ShotName)`(:638), 상태 `IsZCandidateMissing`+`HINT_Z_MISSING` 존재 확인 |
| D-80-09 | 기준점 사진 없음 → 알림 1개 + Shot 사진만, 자동 Test Find 안 함 | ✓ VERIFIED | `bRunTestFind = bNeedsDatum && bDatumComplete`(불완전이면 false → `RunAutoTestFind`에 `bEligible=false`로 `NotRun`), `AlertPresenter(ALERT_TITLE_DATUM_MISSING, ALERT_TEXT_DATUM_MISSING)` 호출을 VM `ApplySelection`에서 직접 확인 |
| D-80-10 | 저장 = 파라미터만, 사진 경로는 원래 값 | ✓ VERIFIED | `MainWindow.SaveRecipe`(:322) `ReviewerReinspectService.RunWithOriginalPaths(() => ...SaveRecipe(...))` — `RunWithOriginalPaths` 본문이 `RestorePathsOnly`→`fnSave()`→`finally { ApplyPartPaths(s_activePart); }` 순서로 구현됨을 직접 확인 |
| D-80-11 | 해제 4경로(사용자/PLC/레시피변경/종료), 파라미터 유지 | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED (4경로 중 2경로만 실기 확인) | 코드상 4경로 전부 배선 확인: `BtnReviewerRelease_Click`→`ReleaseByUser()`, `Custom/SystemHandler.cs:120` `Release(RELEASE_REASON_PLC_TEST)`(ForceOfflineInspectModeOffForAutoTest 바로 앞), `MainWindow` ctor `OnRecipeChanged += HandleRecipeChanged`, `Window_Closing` `Release(RELEASE_REASON_SHUTDOWN)`(`mSystemHandler.Release()` 바로 앞). 그러나 80-HUMAN-UAT.md 는 [해제]·프로그램 재시작 2경로만 PASS 로 기록하고 PLC $TEST·레시피 변경 2경로는 **PENDING**(실기 미시험)으로 명시 — 인간검증 항목 1·2 참고 |
| D-80-12 | 눈에 띄는 상태 줄 + [해제] + 로그 | ✓ VERIFIED | `MainView.xaml:484` `panel_reviewerReinspect`(`DataTrigger IsActive`) + `btn_reviewerRelease`, `[ReviewerLoad]` 로그 문구를 `ReviewerReinspectService.cs` 안에서 직접 확인 |
| D-80-13 | OfflineInspectMode 메모리 전용 켜기/되돌리기 | ✓ VERIFIED | `LoadForRow` 안 `SystemSetting.Handle.OfflineInspectMode = true;`(직접 대입, `Setting.Save()` 호출 없음) 확인 |
| D-80-14 | JPG 안내 상태 줄 | ✓ VERIFIED | `IsJpgPath`/`newState.IsJpgPhoto` + VM `HINT_JPG` 텍스트 존재·`BuildStatusText`에서 사용 확인 |
| D-80-15 | 기준 ROI 시험 찾기 버튼(z1 사진+상자+선, 새 대화상자 없이 기존 라벨) | ✓ VERIFIED | `MainView.xaml:314` `btn_testFindLocalRef` → `BtnTestFindLocalRef_Click`(1문장 서비스 호출) → `LocalRefTestFindService.Run` → `ShowLocalRefTestFindOutcome`(기존 `label_testFindResult` 재사용, 새 `MessageBox`류 호출 0 — `mv_dialog=0` 재확인) |
| D-80-16 | 가독성 규칙 5종 0 | ✓ VERIFIED | 이번 세션에서 신규 파일 4개 전체 + 기존 파일 13개 더한 줄 grep 재실행 — 전부 0 (본문 "검증 방법 요약" 참고) |
| D-80-17 | MVVM — ReviewerWindow.xaml.cs/MainView.xaml.cs 핸들러는 배선 1문장, 로직은 서비스/VM | ✓ VERIFIED | `Button_ApplyRowToMain_Click`(1문장), `BtnReviewerRelease_Click`(1문장), `BtnTestFindLocalRef_Click`(서비스 호출+결과 전달 2문장) 직접 확인. `InspectionListView.xaml.cs`의 트리 탐색 로직은 이 파일의 기존 책임 범위(SetSelectionChange 선례)이며 D-80-17 이 명시한 두 파일(ReviewerWindow/MainView)에는 해당 없음 |
| D-80-18 | 새 .cs 파일 4개 csproj 등록 | ✓ VERIFIED | `DatumMeasurement.csproj:278-280,418` `<Compile Include>` 4건 직접 확인 |
| D-80-19 | 기준점 사진 일부만 있으면 "없음"과 동일 처리 | ✓ VERIFIED | `SavedCycleRerunPlanner.IsDatumPhotoSetComplete` — 필요한 역할 키(단일/H/V) 중 하나라도 경로 없음 또는 `File.Exists`false 면 `return false` — 코드 직접 확인 |
| 함께 처리 1 | 리뷰어 사이클 전체 보기 선 겹침 버그 수정 | ✓ VERIFIED | `ReviewerWindow.DisplayCycle`이 `cycle.Shots.SelectMany(...)` 대신 `ResolveCycleImagePath(cycle, out ownerShot)` + `CollectShotOverlays(ownerShot)`(단일 shot의 FAI만 순회, 다른 Shot 미포함) 사용을 코드로 직접 확인 |
| 함께 처리 2 | 수동 RUN Local Ref stale 재계산, PLC 불변 | ✓ VERIFIED | `TryRecomputeStaleLocalRef` 첫 문장이 `if (parentSeq2.IsProtocolDrivenCycle()) return null;`(PLC 즉시 폴백), 두 번째가 `IsDatumTransformFromTeachingPhotos` 출처 확인, `datum.TeachingImagePath`만 사용, `HImage`는 `finally`에서 Dispose. `TryResolveLocalRef`의 `bStale` 분기가 `TryUseRecomputedLocalRef`를 호출하도록 교체됨(기존 STALE 즉시 전환 2줄 삭제) — 전부 직접 확인. `ProcessOneDatum`/`InjectLocalRef`(InspectionSequence)/`HandleRunStartResetResults`/`ComputeLocalRefLinesForDatum` md5 무변경 재확인 |
| 함께 처리 3(=D-80-15) | 기준 ROI 시험 찾기 | ✓ VERIFIED | 위 D-80-15 와 동일 |

**Score:** 20/22 항목 VERIFIED, 2/22 PRESENT_BEHAVIOR_UNVERIFIED(D-80-11의 PLC/레시피변경 2경로만 — 코드는 존재·정확한 순서로 배선되었으나 실기 미확인)

## Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` | 신규, 정적 서비스(스냅샷/적용/복원/저장보호/자동해제) | ✓ VERIFIED | 1068줄, `LoadForRow`/`Release`/`RunWithOriginalPaths`/`HandleRecipeChanged` 등 전부 존재, csproj 등록, 가독성 0, 빌드 통과 |
| `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` | 신규 VM, 싱글턴 Instance | ✓ VERIFIED | 248줄, `Instance`/`CanApply`/`DisableReasonText`/`StatusText`/`AlertPresenter`/`MainNavigator` 전부 존재 |
| `WPF_Example/Custom/Sequence/Inspection/DatumTestFindService.cs` | 대화상자 없는 공용 Test Find | ✓ VERIFIED | 135줄, `TryRunFromTeachingImages` 전문 확인(위 발췌 참고), HImage try/finally Dispose |
| `WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs` | 기준 ROI 시험 찾기 판단·오버레이 | ✓ VERIFIED | 196줄, `Run(ParamBase)` 존재, 837479b8 로 실패 문구 단축 반영됨 |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)` | 버튼 1개 + 이유 한 줄, 배선만 | ✓ VERIFIED | 위 표 참고 |
| `WPF_Example/UI/ContentItem/MainView.xaml(.cs)` | 상태 줄 1줄 + [해제] + 시험찾기 버튼, 배선만 | ✓ VERIFIED | 위 표 참고 |
| `WPF_Example/DatumMeasurement.csproj` | 신규 파일 4개 Compile Include | ✓ VERIFIED | 4건 grep 확인 |

## Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `btn_applyRowToMain` | `ReviewerReinspectService.LoadForRow` | `Button_ApplyRowToMain_Click → VM.ApplySelection → LoadForRow → BuildPartForSingleCycle` | ✓ WIRED | 코드 직접 확인 |
| `panel_reviewerReinspect` | `ReviewerReinspectViewModel` | `DataContext = Instance`, `{Binding StatusText}`/`{Binding IsActive}` | ✓ WIRED | XAML 직접 확인 |
| `Custom/SystemHandler.cs MainRun $TEST` | `ReviewerReinspectService.Release` | PLC 자동 검사 전 자동 해제(`ForceOfflineInspectModeOffForAutoTest` 바로 앞) | ✓ WIRED (정적) / ⚠️ 실기 미확인 | 순서는 코드로 확인, 실제 PLC 가동 시 동작은 U-6/U-11 PENDING |
| `MainWindow.SaveRecipe` | `ReviewerReinspectService.RunWithOriginalPaths` | 저장 호출 감싸기 | ✓ WIRED | 코드 직접 확인 |
| `MainWindow.OnReviewerPhotosLoaded` | `InspectionListView.SelectShotAndMeasurement` | `result.LiveShot/LiveFai/LiveMeasurement` | ✓ WIRED | 코드 직접 확인 |
| `ReviewerWindow.DisplayCycle` | `ReviewerImagePathResolver.CollectShotOverlays(ownerShot)` | 전체보기 선 겹침 수정 | ✓ WIRED | 코드 직접 확인 — `SelectMany` 제거 확인 |
| `InspectionListView.Btn_start_Click` | `ResolveRunNodeForSelection` → `ResolveRunnableAction` | 측정/FAI 노드 → 부모 Shot(Action) 노드 | ✓ WIRED | 코드 직접 확인, `ResolveRunnableAction` md5 무변경 |

## 회귀 확인 — 14개 기존 메서드 md5 (base = `55a5085f^`)

| 메서드 | 결과 |
|---|---|
| `MainView.BtnTestFindDatum_Click` | SAME (126 lines) |
| `MainView.AskTestImageSource` | SAME (36 lines) |
| `Action_FAIMeasurement.ProcessOneDatum` | SAME (41 lines) |
| `Action_FAIMeasurement.InjectLocalRef` | SAME (16 lines) |
| `InspectionSequence.HandleRunStartResetResults` | SAME (76 lines) |
| `InspectionSequence.ComputeLocalRefLinesForDatum` | SAME (25 lines) |
| `RepeatRunService.BuildPlan` | SAME (58 lines) |
| `RepeatRunService.ValidatePart` | SAME (38 lines) |
| `RepeatRunService.ApplySavedCyclePart` | SAME (58 lines) |
| `RepeatRunService.BuildOverrideSnapshot` | SAME (37 lines) |
| `RepeatRunService.RestoreOverridePathsOnly` | SAME (27 lines) |
| `InspectionListView.ResolveRunnableAction` | SAME (53 lines) |
| `ReviewMeasurementRow.ResolveCycleImagePath(cycle)` | SAME (29 lines) |
| `ReviewMeasurementRow.ResolveRowImagePath` | SAME (14 lines) |

`RepeatRunService.cs` 삭제 줄 = 0 (phase 진짜 시작 `b2b33104` 대비, 전체 phase 누적) — 재확인.

## Requirements Coverage

REQ-ID 없음(80-CONTEXT.md D-80-00~19 가 계약). 위 "Decision Coverage" 표가 이 phase 의 요구사항 커버리지 표를 대체한다.

## Anti-Patterns Found

없음. `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` 류 마커를 4개 신규 파일 + 수정된 13개 파일의 phase 추가분에서 grep 했으나 매치 없음. `return null`/빈 컬렉션 스텁도 없음(모두 실제 계산·저장 로직).

## Human Verification Required

### 1. PLC 자동 검사 $TEST 경로 자동 해제 + 회귀 (D-80-11 경로②, U-6/U-11)

**Test:** 리뷰어 사진을 불러온 채 상태 줄이 보이는 상태에서 실제 PLC `$TEST` 신호를 보낸다(또는 mock_vision_client.py — 단, 화면의 "수동 트리거" 버튼은 이 경로를 타지 않음, 80-HUMAN-UAT.md:25 주의사항).
**Expected:** `$TEST` 수신 즉시 노란 상태 줄이 사라지고 원래 사진 경로·OfflineInspectMode 로 복원된 뒤, 자동 검사가 실제 카메라로 phase 이전과 동일하게 진행된다.
**Why human:** 실물 PLC/카메라 가동 필요. 코드 순서(`Release` 가 `ForceOfflineInspectModeOffForAutoTest` 보다 먼저)와 14개 메서드 md5 무변경은 정적으로 확인했으나 런타임 동작은 장비에서만 확인 가능 — 이미 80-HUMAN-UAT.md 에 PENDING 으로 기록되어 있다.

### 2. 레시피 변경 자동 해제 (D-80-11 경로③) + A-80-E3 정적 두 장짜리 기준점 완전성 가정

**Test:** 리뷰어 사진을 불러온 채 다른 레시피로 전환한다. 별도로, 크로스-Z 설정이 없는 정적 두 장짜리(가로+세로) 기준점을 쓰는 NG 행을 리뷰어에서 불러온다.
**Expected:** 레시피 전환 즉시 상태 줄이 사라지고 원래 경로로 복원된다. 정적 두 장짜리 기준점 NG 행은 "필요 사진 없음=완전"으로 판정되어 자동 Test Find 가 정상 동작한다.
**Why human:** 두 경로 모두 이번 UAT 에서 시험되지 않음(80-HUMAN-UAT.md 명시 PENDING/미확인). 코드상 구독(`OnRecipeChanged += HandleRecipeChanged`)과 `IsDatumPhotoSetComplete`(필요 역할 키만 검사) 로직은 확인했으나 해당 시나리오의 실기 확인이 필요하다.

## Gaps Summary

코드 결함은 발견되지 않았다 — 4개 신규 파일과 13개 수정 파일 모두 CLAUDE.md 가독성 규칙 5종을 통과하고, 14개 기존 메서드는 phase 시작 대비 byte-identical, `RepeatRunService.cs` 는 삭제 줄 0, Debug|x64 빌드는 오류 0이다. D-80-00~19 전 항목과 함께 처리 1~3 이 실제 소스에 정확히 구현되어 있음을 코드 레벨에서 직접 확인했다(SUMMARY.md 주장을 신뢰한 것이 아니라 grep/sed 로 코드 자체를 열어 재확인).

남은 문제는 전부 **물리 장비 가동이 필요한 미시험 항목**이며, 이는 phase 자체의 80-HUMAN-UAT.md/80-05-SUMMARY.md/STATE.md 에 이미 PENDING 으로 정직하게 기록되어 있다(은폐되지 않음):
1. PLC `$TEST` 자동 해제 경로의 실제 동작 및 기존 PLC 자동 검사와의 회귀 없음 확인 — 장비 자동 가동 시에만 가능.
2. 레시피 변경 시 자동 해제 경로 실기 확인 — 이번 UAT 에서 시험 안 됨.
3. A-80-E3(정적 두 장짜리 기준점을 "완전"으로 보는 가정) — 해당 케이스 미시험.
4. UAT 후속 수정 커밋 `837479b8`(시험 찾기 실패 문구 정리) — 코드에는 반영·커밋되어 있으나 장비 PC Release 재배포는 아직 안 됨(사용자가 다음 배포 시 수행할 운영 작업, 코드 문제 아님).

이 항목들은 코드 결함이 아니라 "실기 확인 대기"이므로 FAILED 로 분류하지 않았다. 다만 phase 의 명시적 요구사항인 D-80-11(해제 4경로)이 4경로 중 2경로만 실기 검증되었으므로, 전체 상태를 `passed` 로 표시하지 않고 `human_needed` 로 기록한다.

---

_Verified: 2026-09-21T01:47:38Z_
_Verifier: Claude (gsd-verifier)_
