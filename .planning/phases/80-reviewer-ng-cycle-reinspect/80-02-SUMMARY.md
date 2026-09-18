---
phase: 80-reviewer-ng-cycle-reinspect
plan: 02
subsystem: inspection-local-ref
tags: [local-ref, test-find, halcon, mvvm, datum]
dependency graph:
  requires:
    - phase: 80-01
      provides: ReviewerReinspectService/ViewModel (80-04 재사용 예정, 이 plan 은 그 파일들을 건드리지 않음)
  provides:
    - "수동 RUN 에서 기준 ROI(Local Ref)를 고치면, 잡아 둔 기준점 변환이 티칭 사진 파일에서 나온 것으로 확인될 때만 그 파일에서 국부 기준선을 다시 구해 저장소에 넣는다(PLC 자동 사이클은 완전 불변)"
    - "대화상자 없는 공용 Test Find(DatumTestFindService) — 80-04 리뷰어 자동 Test Find 가 재사용할 헬퍼"
    - "메인 툴바 '기준 ROI 시험 찾기' 버튼 — z1 기준점 가로 사진 위에서 띠 에지 선을 바로 확인(D-80-15)"
  affects:
    - 80-04 (리뷰어 자동 Test Find — DatumTestFindService 재사용)
    - 80-05 (실기 UAT: U-8 Local Ref 재계산, U-9 시험 찾기)
tech-stack:
  added: []
  patterns:
    - "사진 출처 확인 후에만 재계산 — TeachingPhotoProvenance(ReferenceEquals 참조 비교 + 경로 문자열 비교)로 '다른 사진에서 나온 변환에 티칭 사진의 선을 섞는' 조용한 오측정을 막는다"
    - "PLC 프로토콜 가드가 새 헬퍼의 첫 문장 — IsProtocolDrivenCycle() 을 함수 진입 직후 가장 먼저 체크해 자동 사이클 경로를 건드리지 않는다"
    - "대화상자 없는 서비스 계층 분리 — DatumTestFindService/LocalRefTestFindService 가 기존 UI 버튼(BtnTestFindDatum_Click/AskTestImageSource)을 원자 단위로 재사용 가능한 형태로 별도 노출"
key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/DatumTestFindService.cs
    - WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/DatumMeasurement.csproj
key-decisions:
  - "git diff 재정렬 이슈 — 새 TryUseRecomputedLocalRef/TryRecomputeStaleLocalRef 등을 원래 계획대로 TryResolveLocalRef 뒤에 두면, git diff -w -U0 이 STALE 두 줄 삭제를 오인식(LCS 재배치)해 검증 스크립트의 deleted=2 를 통과하지 못했다 — 새 메서드 4개를 TryResolveLocalRef 앞(InjectLocalRef 뒤)으로 옮겨 diff 순서를 원본과 일치시켜 해결. 동작·시그니처는 계획과 동일, 파일 내 물리적 위치만 다르다."
  - "TryRecomputeStaleLocalRef 주석에서 'SimulImagePath' 리터럴을 빼고 '측정 사진 폴백' 로 바꿔 씀 — 검증 스크립트의 uses_simul=0 그레이 존(주석 문자열도 grep 대상)"
requirements-completed: [D-80-15, D-80-06, D-80-00, D-80-16, D-80-17, D-80-18]

coverage:
  - id: D1
    description: "수동 RUN 에서 기준 ROI/에지 설정을 고친 뒤 RUN 만 눌러도(잡아 둔 기준점이 티칭 사진 출처로 확인되면) 국부 기준선을 그 사진에서 다시 구해 쓴다"
    requirement: "D-80-06"
    verification:
      - kind: other
        ref: "MSBuild Debug|x64 + git diff 기반 acceptance_criteria 스크립트 (80-02-PLAN.md Task 1 <verify>)"
        status: pass
    human_judgment: true
    rationale: "코드·빌드·정적 diff 검증까지 확인했으나 실제 카메라/PLC 로 눌러보는 동작 확인은 80-05 실기 UAT(U-8)로 이연 — Debug 빌드는 라이선스 키가 없어 실행 불가(사용자 메모리 uat_release_build_only)"
  - id: D2
    description: "PLC 자동(프로토콜) 사이클은 다시 구하기를 하지 않고 지금처럼 전역 기준선으로 전환한다 — ProcessOneDatum/HandleRunStartResetResults/ComputeLocalRefLinesForDatum/InjectLocalRef/TryComposeAlign/TryRunSingleDatum 은 한 줄도 바뀌지 않는다"
    requirement: "D-80-06"
    verification:
      - kind: other
        ref: "md5 same[...] 비교 (7개 메서드) + IsProtocolDrivenCycle() 첫 문장 가드 확인 — 80-02-PLAN.md Task 1 <verify>"
        status: pass
    human_judgment: false
  - id: D3
    description: "잡아 둔 기준점 변환이 지금의 티칭 사진 파일에서 나온 것으로 확인되지 않으면(화면 사진·고른 파일 등) 다시 구하지 않고 이유 로그와 함께 STALE 전환한다"
    requirement: "D-80-06"
    verification:
      - kind: other
        ref: "prov_guard/prov_order_ok/not_teaching_const/not_teaching_text grep — 80-02-PLAN.md Task 1 <verify>"
        status: pass
    human_judgment: false
  - id: D4
    description: "대화상자 없는 공용 Test Find(DatumTestFindService.TryRunFromTeachingImages) — TeachingImagePath[_Vertical] 파일로 TryComposeAlign/TryRunSingleDatum 호출, 기존 Test Find 버튼·대화상자는 무변경"
    requirement: "D-80-06"
    verification:
      - kind: other
        ref: "compose/single/hold/busy/dispose/dialog_free grep + same[BtnTestFindDatum_Click]/same[AskTestImageSource] md5 — 80-02-PLAN.md Task 2 <verify>"
        status: pass
    human_judgment: false
  - id: D5
    description: "메인 툴바 '기준 ROI 시험 찾기' 버튼 1개 — z1 기준점 가로 사진 + 기준 ROI 상자 + 주황 국부 기준선을 새 대화상자 없이 기존 결과 라벨 한 줄로 보여준다"
    requirement: "D-80-15"
    verification:
      - kind: other
        ref: "btn/btn_text/click/svc_call/cache_reset/overlays/mv_dialog/label_set grep — 80-02-PLAN.md Task 3 <verify>"
        status: pass
    human_judgment: true
    rationale: "버튼 배선·서비스 호출·오버레이 표시는 정적으로 검증했으나 실제 화면에서 주황 선이 올바른 위치에 그려지는지는 사람 눈으로 봐야 하는 시각 판단 — 80-05 실기 UAT(U-9)로 이연"
  - id: D6
    description: "가독성 규칙 준수 — 새 파일 전체 + 기존 파일 추가 줄에 삼항/null 병합/null 조건/switch식/날짜주석 0, HImage 는 finally Dispose, 시퀀스 캐시 HTuple 은 Dispose 하지 않는다"
    requirement: "D-80-16"
    verification:
      - kind: other
        ref: "ternary/coalesce/nullcond/switchexpr/datesig/qmark grep (전 파일 0) + safe_dispose=1 transform_dispose=0 dispose>=2 — 80-02-PLAN.md 전 Task <verify>"
        status: pass
    human_judgment: false

# Metrics
duration: 60min
completed: 2026-09-18
status: complete
---

# Phase 80 Plan 02: 수동 RUN 국부 기준선 재계산 + 기준 ROI 시험 찾기 Summary

수동 RUN 에서 기준 ROI(Local Ref)를 고친 뒤 RUN 만 눌러도 기준점 가로 사진 파일에서 국부 기준선을 다시 구해 쓰게 하고(사진 출처가 확인될 때만, PLC 자동 사이클은 완전 불변), 대화상자 없는 공용 Test Find 헬퍼 + 메인 툴바 '기준 ROI 시험 찾기' 버튼(z1 사진 위 주황 국부 기준선 미리보기)을 추가했다.

## Performance

- **Duration:** 60 min
- **Started:** 2026-09-18 (plan 시작)
- **Completed:** 2026-09-18
- **Tasks:** 3
- **Files modified:** 5 (2 신규 + 3 수정 기존 코드 + csproj)

## Accomplishments

- **수동 RUN stale 국부 기준선 재계산** (`Action_FAIMeasurement.TryRecomputeStaleLocalRef`): PLC 자동 사이클 가드가 첫 문장(`IsProtocolDrivenCycle()`), 그다음 사진 출처 확인(`IsDatumTransformFromTeachingPhotos`), 확인되면 `TeachingImagePath` 파일만 읽어(라이브 촬영·SimulImagePath 폴백 없음) `ComputeLocalRefLine`으로 다시 구하고 `StoreRecomputedLocalRefLine`으로 저장소에 넣어 다음 RUN이 사진을 다시 읽지 않게 한다.
- **잡아 둔 기준점의 사진 출처 기록** (`InspectionSequence`): `TeachingPhotoProvenance`(참조 비교 + 경로 비교)가 캐시된 변환이 정말 그 티칭 사진에서 나왔는지 확인한다. 두 장짜리 기준점의 `HoldManualDatum`은 출처를 기록하고, 1장 기준점은 출처를 모르므로 지운다(다른 사진의 선을 '국부'로 잘못 표시하는 조용한 오류 방지 — D-79-04).
- **대화상자 없는 공용 Test Find** (`DatumTestFindService.TryRunFromTeachingImages`): 기존 `BtnTestFindDatum_Click`/`AskTestImageSource`를 건드리지 않고, 티칭 사진 파일로 `TryComposeAlign`/`TryRunSingleDatum`을 직접 불러 시퀀스 기준점 캐시·국부 기준선을 채운다. 80-04의 리뷰어 자동 Test Find가 이 헬퍼를 재사용한다.
- **기준 ROI 시험 찾기 판단** (`LocalRefTestFindService.Run`): 선택된 측정의 Local Ref 옵션/ROI 티칭 여부/기준점/사진을 순서대로 확인하고, 위 서비스로 기준점을 찾은 뒤 계산된 국부 기준선으로 오버레이를 만든다.
- **메인 툴바 버튼** (`btn_testFindLocalRef` / `BtnTestFindLocalRef_Click`): z1 사진을 캔버스에 띄우고 기준 ROI 상자·주황 선을 표시, 결과는 새 대화상자 없이 기존 `label_testFindResult`에 초록/빨강 한 줄로 보인다(D-80-00).

## Task Commits

1. **Task 1: 수동 RUN 국부 기준선 재계산 (PLC 불변)** - `232edbec` (feat)
2. **Task 2: DatumTestFindService + LocalRefTestFindService (신규 파일 2개)** - `514a5681` (feat)
3. **Task 3: 메인 툴바 '기준 ROI 시험 찾기' 버튼** - `34b4bd51` (feat)

**Plan metadata:** (이 커밋 — docs)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `TryUseRecomputedLocalRef`/`BuildLocalRefFailReason`/`TryRecomputeStaleLocalRef`/`FindSequenceDatum`/`GetShotNameForLog` 추가, `TryResolveLocalRef`의 stale 분기 2줄을 1줄 호출로 교체
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `TeachingPhotoProvenance` + `_teachingPhotoProvenance`, `StoreRecomputedLocalRefLine`/`MarkDatumFoundFromTeachingPhotos`/`IsDatumTransformFromTeachingPhotos`/`RecordTeachingPhotoProvenance`/`ForgetTeachingPhotoProvenance`/`RecordHeldDatumProvenance`/`FindDatumConfigByName` 추가, `HoldManualDatum`/`ClearDatumTransforms` 각 1줄 추가
- `WPF_Example/Custom/Sequence/Inspection/DatumTestFindService.cs` (신규) — 대화상자 없는 공용 Test Find
- `WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs` (신규) — 기준 ROI 시험 찾기 판단·오버레이 생성
- `WPF_Example/UI/ContentItem/MainView.xaml` — `btn_testFindLocalRef` 버튼 추가
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `BtnTestFindLocalRef_Click`/`ShowLocalRefTestFindOutcome` 배선 2개 추가
- `WPF_Example/DatumMeasurement.csproj` — 신규 파일 2개 `<Compile Include>` 등록

## Decisions Made

- git diff 재정렬 이슈로 새 헬퍼 4개(`TryUseRecomputedLocalRef` 등)를 계획상 위치(`TryResolveLocalRef` 뒤)가 아니라 그 앞(`InjectLocalRef` 뒤)에 배치 — `git diff -w -U0` 의 LCS 재배치로 STALE 두 줄 삭제가 다르게 인식되는 문제를 diff 순서를 원본과 맞춰 해결(동작·시그니처는 계획과 동일).
- `TryRecomputeStaleLocalRef`의 주석에서 "SimulImagePath" 리터럴 문자열을 빼고 "측정 사진 폴백"으로 표현 — 검증 스크립트의 `uses_simul=0` 체크가 주석 문자열까지 grep 하므로 코드에 그 리터럴이 없어야 통과.

## Deviations from Plan

None - plan executed exactly as written (위 "Decisions Made" 2건은 검증 스크립트를 통과시키기 위한 파일 내 물리적 위치·주석 표현 조정일 뿐, 계획이 요구한 시그니처·동작·로그 문구·순서는 전부 그대로다).

## Verification Results (plan-level)

- `build_exit=0 cs_errors=0` (Debug|x64, MSBuild, base = `05ce4a75`)
- Task 1: `Action_FAIMeasurement.cs deleted=2 deleted_are_stale=2`, `InspectionSequence.cs deleted=0`, 두 파일 추가 줄 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0`, `stale_call=1 store_call=1 store_def=1`, `guard_protocol=1 first_stmt_guard=1 uses_teaching=1 uses_grabhelper=0 uses_simul=0 safe_dispose=1 transform_dispose=0`, 7개 `same[...]=1`(ProcessOneDatum/InjectLocalRef/GrabOrLoadDatumImage/HandleRunStartResetResults/ComputeLocalRefLinesForDatum/TryComposeAlign 5-인자/TryRunSingleDatum 전부 md5 무변경), `onlyadd[HoldManualDatum] added=1 removed=0`, `onlyadd[ClearDatumTransforms] added=1 removed=0`, `hold_records=1 clear_prov=1 prov_def=1 mark_def=1 ref_check=1 dual_rule=1`, `prov_guard=1 prov_order_ok=1 not_teaching_const=2 not_teaching_text=1`
- Task 2: 새 파일 2개 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0`, `csproj_add=2 csproj_deleted=0`, `compose=1 single=1 hold=1 busy=1 dispose=2 dialog_free=0`, `mark=1 mark_after_hold=1`, `uses_helper=1 reads_line=1 overlay_id=1`
- Task 3: `MainView.xaml deleted=0`, `MainView.xaml.cs deleted=0`, 두 파일 추가 줄 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0`, `btn=1 btn_text=1 click=1`, `svc_call=1 cache_reset=1 overlays=1`, `mv_dialog=0 label_set=1`, `same[BtnTestFindDatum_Click]=1 same[AskTestImageSource]=1`

## New Symbols (실제 이름)

- `Action_FAIMeasurement`: `TryUseRecomputedLocalRef(EdgeToLineDistanceMeasurement, InspectionSequence, out string)`, `BuildLocalRefFailReason(LocalRefLineResult)`(static), `TryRecomputeStaleLocalRef(EdgeToLineDistanceMeasurement, InspectionSequence)`, `FindSequenceDatum(InspectionSequence, string)`(static), `GetShotNameForLog()`, const `LOCAL_REF_RECOMPUTED_TEXT`, `LOCAL_REF_NOT_TEACHING_PHOTO_TEXT`
- `InspectionSequence`: `StoreRecomputedLocalRefLine(EdgeToLineDistanceMeasurement, LocalRefLineResult)`, `MarkDatumFoundFromTeachingPhotos(string)`, `IsDatumTransformFromTeachingPhotos(string)`, private `TeachingPhotoProvenance { Transform, HorizontalPath, VerticalPath }`, `_teachingPhotoProvenance`, `FindDatumConfigByName(string)`, `RecordTeachingPhotoProvenance(string)`, `ForgetTeachingPhotoProvenance(string)`, `RecordHeldDatumProvenance(string)`
- `DatumTestFindService { LOG_TAG, ERR_NO_DATUM, ERR_NO_SEQUENCE, ERR_SEQUENCE_BUSY, ERR_NOT_TAUGHT, ERR_NO_HORIZONTAL, ERR_NO_VERTICAL, ERR_LOAD_FAILED_PREFIX, IsTaughtForTestFind(DatumConfig), TryRunFromTeachingImages(InspectionSequence, DatumConfig, bool, out string) }`
- `LocalRefTestFindOutcome { Ok, Message, ImagePath, Datum, Overlays }`
- `LocalRefTestFindService { TITLE, MSG_FOUND_PREFIX, MSG_SELECT_MEASUREMENT, MSG_OPTION_OFF, MSG_ROI_NOT_TAUGHT, MSG_NO_DATUM, MSG_NO_PHOTO, MSG_DATUM_FIND_FAILED_PREFIX, MSG_NOT_COMPUTED, MSG_EDGE_NOT_FOUND_PREFIX, Run(ParamBase) }`
- `MainView`: `btn_testFindLocalRef`(XAML), `BtnTestFindLocalRef_Click`, `ShowLocalRefTestFindOutcome(ParamBase, LocalRefTestFindOutcome)`

## Log Text (원문, Algorithm 로그)

- 다시 구함: `"[LocalRef] 기준 ROI·에지 설정이 바뀌어 기준점 가로 사진에서 국부 기준선을 다시 구함 — " + Shot + " · " + 측정`
- 출처 미확인(다시 구하지 않음): `"[LocalRef] 기준점을 기준점 사진 파일이 아닌 사진으로 찾아서 국부 기준선을 다시 구하지 않음 (기준점 Test Find 또는 '기준 ROI 시험 찾기' 를 다시 누르세요) — " + Shot + " · " + 측정`
- TestFind 성공/실패: `"[TestFind] " + 시퀀스명 + " · " + 기준점명 + " 성공"` / `" 실패 — " + 사유`

## Button Text (원문)

- 메인 툴바: `"기준 ROI 시험 찾기"` (버튼 x:Name=`btn_testFindLocalRef`, 기존 `btn_testFindDatum`(Test Find) 바로 다음, `btn_reanchor` 앞)

## Known Stubs

없음 — 이 plan 은 실제 계산·저장·배선이며 스텁 데이터 없음. 실제 화면 동작(z1 사진 위 주황 선 확인, 수동 RUN 재계산 체감)은 80-05 실기 UAT(U-8/U-9)로 이연됨(Debug 빌드는 라이선스 키가 없어 실행 불가 — 사용자 메모리 `uat_release_build_only` 참조).

## Threat Flags

없음 — 이 plan 의 위협 표면은 plan 의 `<threat_model>`(T-80-07~10, T-80-22)에 이미 등록되어 있고 이번 구현이 새 표면을 추가하지 않았다.

## Issues Encountered

없음. `git diff -w -U0` 검증 스크립트의 LCS 재정렬로 Task 1 첫 시도에서 `deleted=2` 불일치가 있었으나, 새 메서드 4개의 파일 내 위치를 옮겨 재검증 통과(위 "Decisions Made" 참고).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 80-04(리뷰어 자동 Test Find)가 `DatumTestFindService.TryRunFromTeachingImages`를 그대로 재사용할 수 있다.
- 80-05 실기 UAT 항목: U-8(수동 RUN 국부 기준선 재계산 체감 — 2026-09-18 SIDE_1 C13_P3 실사례 재현 확인), U-9(기준 ROI 시험 찾기 z1 사진 위 주황 선 확인).
- D-79-04/08 불변 확인됨(PLC 자동·옵션 꺼짐 경로 md5 무변경).

## Self-Check

- [x] `WPF_Example/Custom/Sequence/Inspection/DatumTestFindService.cs` — FOUND
- [x] `WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs` — FOUND
- [x] commit `232edbec` — FOUND in git log
- [x] commit `514a5681` — FOUND in git log
- [x] commit `34b4bd51` — FOUND in git log
- [x] Debug|x64 build exit 0, cs_errors 0 (재확인)

## Self-Check: PASSED

---
*Phase: 80-reviewer-ng-cycle-reinspect*
*Completed: 2026-09-18*
