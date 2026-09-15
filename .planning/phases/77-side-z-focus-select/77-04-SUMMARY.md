---
phase: 77-side-z-focus-select
plan: 04
subsystem: vision-inspection
tags: [halcon, edge-measurement, z-focus-select, offline-inspect, repeat-run, tcp-plc]

# Dependency graph
requires:
  - phase: 77-side-z-focus-select/77-01
    provides: "EZRangeMode/ResolveZRangeMode/TryHandleZRangeMeasurement 뼈대 — ManualSingle/OfflineSelect 자리만 비워둔 상태"
  - phase: 77-side-z-focus-select/77-02
    provides: "동점 규칙 적용 후에도 LastSelectedZIndex/LastFitScore 가 채택된 결과 그대로 채워짐"
  - phase: 77-side-z-focus-select/77-03
    provides: "EdgeInspectionOverlay.SelectedZLabel 배선(값은 항상 null) — 이 plan 이 값을 채운다"
provides:
  - "Action_FAIMeasurement.ResolveZRangeMode 완성 — PLC(Sender 있음)만 Auto*, 수동 트리거/화면 RUN 은 ManualSingle, 오프라인·재검사(비프로토콜+파일읽기)는 OfflineSelect"
  - "Action_FAIMeasurement.LogZRangeManualNotice — 라이브 수동은 시퀀스 로그+Algorithm 로그로 안내만 남기고 1장 그대로 측정"
  - "Action_FAIMeasurement.LoadOfflineZRangeCandidates/ResolveOfflineZRangeImagePath/FindZRangeCandidateImage — 오프라인·재검사 z 별 후보 로드"
  - "Action_FAIMeasurement.SaveZRangeCandidateImageIfEnabled — SystemSetting.SaveZRangeCandidateImages 체크박스(기본 꺼짐) 켰을 때만 후보 z 사진 저장"
  - "Action_FAIMeasurement.AutoFillZRangeOfflineImage — AutoFillOfflineImages 켜짐 + 라이브일 때 z 별 오프라인 사진도 채움"
  - "Action_FAIMeasurement.ApplySelectedZLabel — 선택/기준 Z 결과의 오버레이 SelectedZLabel 채움(77-03 배선 완성)"
  - "CycleResultDto.ZRangeImages/ZRangeImageRecordDto, InspectionSequence._tickZRangeImages — cycle.json 후보 z 사진 기록"
  - "CaptureImageSaveService.BuildZRangeCandidateFileName(shotz_<seq>_<shot>_z<N>_<시각>), RecipeFiles.OFFLINE_SUFFIX_Z(shot_<Shot>_z<N>.bmp)"
  - "ShotConfig.RerunZRangeImagePaths, RepeatRunService.ZRangePhotoPaths/FillPartZRangePhotos/BuildRerunZRangeMap — 재검사 부품별 z→사진 경로 주입·원복"
affects: [77-05-side-z-focus-select, 77-06-side-z-focus-select]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "모드 판정 단일 함수(ResolveZRangeMode)의 가드 순서만으로 5가지 실행 경로(Off/AutoPending/AutoCompletion/ManualSingle/OfflineSelect)를 완전히 분리 — 각 모드의 실제 동작은 그 모드에 대응하는 전용 헬퍼(LogZRangeManualNotice/LoadOfflineZRangeCandidates/ExecuteZRangeSelection)로 위임"
    - "재검사 경로 우선 가드(ResolveOfflineZRangeImagePath) — RerunZRangeImagePaths != null 이면 그 사전만 쓰고 오프라인 폴더로 폴백하지 않아, 재검사 중 다른 부품 사진이 섞이는 사고(T-77-19)를 원천 차단"
    - "부품 적용마다 전부 원복 후 재주입(ApplySavedCyclePart/RestoreOverridePathsOnly) 관용구를 RerunZRangeImagePaths 에도 그대로 확장 — 기존 SimulImagePath/TeachingImagePath 원복 패턴과 동일 규칙"

key-files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs
    - WPF_Example/Utility/CaptureImageSaveService.cs
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Utility/RecipeFileHelper.cs
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs

key-decisions:
  - "M-1: 모드 판정 — PLC(Sender 있음) → Auto*, 수동 트리거(Sender 없음, protocol-driven) → ManualSingle, 비프로토콜 + 라이브 촬영 모드(화면 RUN, non-SIMUL, OfflineInspectMode 꺼짐) → ManualSingle, 비프로토콜 + 파일 읽기 모드(OfflineInspectMode 켜짐 또는 SIMUL 빌드, 재검사 포함) → OfflineSelect"
  - "M-2: 오프라인 파일 규약 shot_<ShotName>_z<N>.bmp — 기존 shot_<ShotName>.bmp(OFFLINE_PREFIX_SHOT) + 새 OFFLINE_SUFFIX_Z(\"_z\") 조합, 같은 폴더·확장자·Sanitize 규칙 재사용(RecipeFiles.BuildOfflineImagePath)"
  - "M-3: 재검사 경로 우선 — ShotConfig.RerunZRangeImagePaths 가 null 이 아니면(재검사 중) 그 사전만 쓰고 오프라인 폴더로 폴백하지 않는다(T-77-19 mitigate)"
  - "M-4: 후보 저장(SaveZRangeCandidateImageIfEnabled)은 라이브 촬영 + PLC 자동(Auto*) 에서만, 파일은 원본(origin) 폴더·포맷 규칙(BuildDatumFileName 과 같은 확장자)"
  - "M-5: 오프라인 미지원 측정 타입은 기준 Z 파일이 있으면 그 사진으로, 없으면 현재 사진으로 측정(각각 Algorithm/Error 로그)"

patterns-established:
  - "Pattern: 새 모드(OfflineSelect)가 기존 완성 게이트(AutoCompletion)의 실행 경로(ExecuteZRangeSelection/ExecuteZRangeBaseImageMeasurement)를 그대로 재사용하고, 후보 로더(EnsureZRangeCandidatesLoaded)만 데이터 소스(저장소 vs 파일)로 분기한다 — 선택 로직 중복 0"

requirements-completed: [SZF-02, SZF-03, SZF-04, SZF-05]

coverage:
  - id: D1
    description: "체크박스 SaveZRangeCandidateImages(기본 꺼짐)를 켠 라이브 PLC 자동 사이클에서만 후보 z 사진이 shotz_<seq>_<shot>_z<N>_<시각> 이름으로 원본 폴더에 저장되고 cycle.json ZRangeImages 에 ShotName·ZIndex·Path 로 기록된다(SZF-02/SZF-04)"
    requirement: "SZF-02"
    verification:
      - kind: unit
        ref: "grep savegate=1 livegate=1 enqueue=1 catchguard=1 savecall=1 record=1 attach=2 clear=1 setting=1 fname=1 prefix=1 dtolist=1 dtoclass=1 (77-04-PLAN.md Task 1 자동검증 스크립트, 이 세션 재실행 결과와 동일)"
        status: pass
    human_judgment: true
    rationale: "체크박스를 실제로 켜고 SIMUL TCP 자동 사이클을 돌려 disk 에 파일이 쌓이고 cycle.json 이 맞게 기록되는지는 런타임 증거가 필요하다 — 테스트 하네스가 없어 77-06 UAT 에서 확인한다."
  - id: D2
    description: "화면 RUN(라이브 빌드)·수동 트리거는 범위 Shot 을 현재 사진 1장으로 측정하고 선택하지 않으며, 시퀀스 로그·Algorithm 로그에 안내가 사이클당 1회 남는다. MainView.xaml.cs 는 무수정(SZF-05)"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep modes=3 offfirst=1 notice=1 noticeseq=1 forbidden=0 (Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "실제 라이브 RUN·수동 트리거 클릭 시 로그·상태 표시가 화면에 어떻게 보이는지는 앱을 띄워 사람이 확인해야 한다 — 77-06 UAT."
  - id: D3
    description: "오프라인 검사·저장 사진 재검사는 z 별 저장 사진(shot_<Shot>_z<N>.bmp 또는 재검사 주입 경로)으로 자동검사와 같은 선택 로직으로 측정하고, 없으면 1장 폴백 + 경고 로그를 남긴다(SZF-02 edge empty/missing)"
    requirement: "SZF-02"
    verification:
      - kind: unit
        ref: "grep offload=1 rerunfirst=1 suffixuse=2 suffix=1 rerunfield=1 partprop=1 fill=1 apply=1 restore=1 handled=1 validate=0 (Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "실제 오프라인 z 파일을 폴더에 두고 검사를 돌려 선택 로그([ZFocus])가 맞게 나오는지, 저장 사이클 재검사가 다른 부품 사진과 섞이지 않는지는 사람이 SIMUL/오프라인 사이클을 돌려 확인해야 한다 — 77-06 UAT."
  - id: D4
    description: "AutoFillOfflineImages 켜진 라이브 PLC 사이클은 z 별 오프라인 사진도 함께 채우고(Shot.SimulImagePath 는 불변), 선택/기준 Z 측정이 끝나면 그 오버레이 SelectedZLabel 에 z5 형태 라벨이 채워진다(SZF-04)"
    requirement: "SZF-04"
    verification:
      - kind: unit
        ref: "grep autofill=1 labelcalls=3 (Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "오프라인 폴더에 실제로 파일이 생기는지, 결과 화면 오버레이에 ' z5' 문구가 실제로 보이는지는 앱을 띄워 사람이 확인해야 한다 — 77-06 UAT."

# Metrics
duration: ~35min
completed: 2026-09-15
status: complete
---

# Phase 77 Plan 04: SIDE Z 범위 수동·오프라인·재검사 동작 Summary

**D-77-06 을 완성한다 — 화면 RUN·수동 트리거는 안내만 남기고 1장 그대로 측정, 오프라인 검사·저장 사이클 재검사는 z 별 저장 사진으로 자동검사와 동일한 선택 로직(없으면 1장+경고)을 태우며, 후보 z 사진 저장 체크박스(기본 꺼짐)와 cycle.json 기록·오프라인 z 자동채움·오버레이 선택 Z 라벨까지 전부 연결한다**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-09-15T13:38:40+09:00
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments

- `Action_FAIMeasurement.ResolveZRangeMode` 완성(M-1) — PLC 자동 사이클(Sender 있음)만 `Auto*`, 수동 트리거(Sender 없는 `$TEST`)·화면 RUN(비프로토콜+라이브 촬영)은 `ManualSingle`, 오프라인 검사·저장 사이클 재검사(비프로토콜+파일 읽기, `OfflineInspectMode` 켜짐 또는 SIMUL 빌드)는 `OfflineSelect` — 77-01/02 가 비워둔 두 자리를 채웠다
- `LogZRangeManualNotice` — 라이브 수동(RUN·수동 트리거)은 범위 Shot 을 선택 없이 1장으로 측정하고, 사이클당 1회 시퀀스 로그(`LogSeqStep("Measure", ...)`)와 Algorithm 로그(`[ZFocus]`) 두 곳에 "Z 범위 Shot — 수동은 Z 선택 안 함" 안내를 남긴다(D-77-06 ①). `MainView.xaml.cs` 는 무수정
- `LoadOfflineZRangeCandidates`/`ResolveOfflineZRangeImagePath`/`FindZRangeCandidateImage` — 오프라인·재검사 모드는 `EnsureZRangeCandidatesLoaded` 가 자동 사이클 저장소 대신 z 별 오프라인 파일에서 후보를 읽는다. 지원 측정은 `ExecuteZRangeSelection` 을 그대로 재사용(동점 규칙 포함), 미지원 측정은 기준 Z(`ShotParam.ZIndex`) 파일 1장으로 측정(D-77-06 ②, M-5). 후보 0 이면 `[ZFocus] 후보 사진 없음` Error 후 1장 폴백, 일부만 있으면 있는 것으로 선택하고 기존 `[ZFocus] 후보 누락` 로그(77-02)가 그대로 적용된다
- `ResolveOfflineZRangeImagePath`(M-3) — `ShotConfig.RerunZRangeImagePaths` 가 null 이 아니면(재검사 중) 그 사전만 쓰고 오프라인 폴더로 폴백하지 않는다(T-77-19 mitigate) — 재검사 중 다른 부품 사진이 섞이는 사고를 원천 차단
- `SaveZRangeCandidateImageIfEnabled` — `SystemSetting.SaveZRangeCandidateImages` 체크박스(기본 꺼짐, INI 키 누락 시 false 로드)를 켠 라이브 PLC 자동 사이클에서만 후보 z 사진을 `shotz_<seq>_<shot>_z<N>_<시각>` 이름으로 원본 폴더에 저장하고 `InspectionSequence.RecordTickZRangeImage` 를 통해 cycle.json `ZRangeImages` 에 기록한다(D-77-06 ③, T-77-17 mitigate — 용량 가드)
- `AutoFillZRangeOfflineImage` — `AutoFillOfflineImages` 가 켜진 라이브 PLC 사이클은 z 별 오프라인 사진(`shot_<Shot>_z<N>.bmp`)도 함께 채운다(D-77-06 ②/D-77-07 ⑦). `Shot.SimulImagePath` 는 건드리지 않는다
- `ApplySelectedZLabel` — 선택(`ExecuteZRangeSelection`) 또는 기준 Z(`ExecuteZRangeBaseImageMeasurement`) 측정이 끝나면 그 측정 오버레이 `SelectedZLabel` 에 `MeasurementBase.FormatSelectedZ` 값(예 `z5`)을 채운다 — 77-03 이 배선만 해두고 비워둔 값을 이 plan 이 채웠다(O-7, D-77-07 ⑥)
- `RepeatRunService`: `SavedCycleRerunPart.ZRangePhotoPaths`(Shot→z→경로), `FillPartZRangePhotos`(cycle.json `ZRangeImages` 수집), `BuildRerunZRangeMap`(부품별 새 사전 반환) — `ApplySavedCyclePart` 가 부품마다 `shot.RerunZRangeImagePaths` 를 주입하고, `RestoreOverridePathsOnly` 가 원복 시 null 로 해제한다. `IsMeasurementHandled` 는 `SkipReason.Z_RANGE_PENDING` 을 `CROSS_Z_INCOMPLETE` 와 같이 "처리됨"에서 제외 — 중간 z tick 의 원본 사진이 Shot 대표 사진으로 채택되지 않는다

## Task Commits

Each task was committed atomically:

1. **Task 1: 후보 z 사진 저장 체크박스(기본 꺼짐)와 cycle.json ZRangeImages 기록** - `5fba2496` (feat)
2. **Task 2: 수동·오프라인·재검사 모드 — 라이브 수동 안내, z 별 저장 사진으로 같은 선택, 오프라인 z 사진 자동채움, 재검사 경로 주입, 오버레이 선택 Z 라벨** - `d20ac23e` (feat)

**Plan metadata:** (this commit) `docs(77-04): complete SIDE Z 범위 수동·오프라인·재검사 plan`

_두 태스크 모두 `type="auto"` — RED/GREEN 분리 없음(TDD 플랜 아님)._

## Files Created/Modified

- `WPF_Example/Setting/SystemSetting.cs` - `SaveZRangeCandidateImages` 체크박스(기본 꺼짐)
- `WPF_Example/Utility/CaptureImageSaveService.cs` - `ZRANGE_CANDIDATE_PREFIX`/`ZRANGE_FILE_Z_SEGMENT`/`BuildZRangeCandidateFileName`
- `WPF_Example/UI/ViewModel/CycleResultDto.cs` - `CycleResultDto.ZRangeImages`, `ZRangeImageRecordDto`
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `_tickZRangeImages`, `RecordTickZRangeImage`/`TakeTickZRangeImagesSnapshot`, `ClearTickDatumImages` 확장, cycle.json 부착 2곳
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `ResolveZRangeMode` 모드 분기 완성, `LogZRangeManualNotice`/`_bZRangeNoticeLogged`, `SaveZRangeCandidateImageIfEnabled`, `AutoFillZRangeOfflineImage`, `LoadOfflineZRangeCandidates`/`ResolveOfflineZRangeImagePath`/`FindZRangeCandidateImage`, `ApplySelectedZLabel`, `TryHandleZRangeMeasurement` ManualSingle/OfflineSelect 분기, `EnsureZRangeCandidatesLoaded` 오프라인 분기, `StoreZRangeCandidateImage` 의 `parentSeq2`→`parentSeq` 이름 정리
- `WPF_Example/Utility/RecipeFileHelper.cs` - `RecipeFiles.OFFLINE_SUFFIX_Z`
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - `RerunZRangeImagePaths`(런타임 전용 필드)
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` - `SavedCycleRerunPart.ZRangePhotoPaths`, `FillPartZRangePhotos`, `BuildRerunZRangeMap`, `ApplySavedCyclePart`/`RestoreOverridePathsOnly` 주입·해제, `IsMeasurementHandled` Z_RANGE_PENDING 제외

## Decisions Made

계획 단계 결정(77-04-PLAN.md "계획 단계 결정" 표에서 이 plan 이 구현한 범위):

- **M-1** 모드 판정: PLC(Sender 있음) → `Auto*`, 수동 트리거(Sender 없음) → `ManualSingle`, 비프로토콜 + 라이브 촬영 모드 → `ManualSingle`, 비프로토콜 + 파일 읽기 모드(`OfflineInspectMode` 또는 SIMUL 빌드, 재검사 포함) → `OfflineSelect`
- **M-2** 오프라인 파일 규약 `shot_<ShotName>_z<N>.bmp`(기존 `shot_<ShotName>.bmp` + `_z<N>`), 같은 폴더·확장자·Sanitize 규칙 재사용
- **M-3** 재검사 경로 우선: `ShotConfig.RerunZRangeImagePaths` 가 null 이 아니면(재검사 중) 그 사전만 쓰고 오프라인 폴더로 폴백하지 않는다
- **M-4** 후보 저장은 라이브 촬영 + PLC 자동(`Auto*`) 에서만, 파일은 원본(origin) 폴더·포맷 규칙(`BuildDatumFileName` 과 같은 확장자)
- **M-5** 오프라인 미지원 타입은 기준 Z 파일이 있으면 그 사진으로, 없으면 현재 사진으로 측정(각각 로그)

**모드 판정표 (실행 시점 → `EZRangeMode`):**

| 실행 경로 | `IsProtocolDrivenCycle()` | `IsManualTriggerCycle()` | `IsLiveCaptureMode()` | 결과 모드 |
|---|---|---|---|---|
| PLC `$TEST`(Sender 있음), 중간 z | true | false | - | `AutoPending` |
| PLC `$TEST`(Sender 있음), `ZIndexEnd` tick | true | false | - | `AutoCompletion` |
| 수동 트리거 `$TEST`(Sender 없음) | true | true | - | `ManualSingle` |
| 화면 RUN(실기 카메라 빌드, `OfflineInspectMode` 꺼짐) | false | - | true | `ManualSingle` |
| 화면 RUN(SIMUL 빌드) | false | - | false | `OfflineSelect` |
| 화면 RUN(`OfflineInspectMode` 켜짐) | false | - | false | `OfflineSelect` |
| 저장 사이클 재검사(`RepeatRunService`, `OfflineInspectMode` 켜고 `StartAll(null)`) | false | - | false | `OfflineSelect`(+ `RerunZRangeImagePaths` 주입) |

(`ShotParam.IsZRangeEnabled()` 가 false 인 Shot 은 항상 `Off` — 위 표에 도달하지 않는다.)

**파일 이름 규약 2종 실제 예시:**

- 후보 z 사진 저장(`SaveZRangeCandidateImages` 켜짐, `CaptureImageSaveService.BuildZRangeCandidateFileName`): `ResultSavePath\Image\260915\1332\original\shotz_SIDE_C13-14_z5_133205123.bmp` (확장자는 `SystemSetting.OriginImageFormat`, 기본 `.jpg`)
- 오프라인 z 사진(`RecipeFiles.OFFLINE_PREFIX_SHOT` + `ShotName` + `RecipeFiles.OFFLINE_SUFFIX_Z` + z번호, `BuildOfflineImagePath`): `<ImageSavePath>\OfflineInspect\<레시피>\shot_C13-14_z5.bmp`(확장자 `.bmp` 고정)

**77-06 UAT 가 쓸 오프라인 z 파일 준비 방법:**

1. 범위 켠 Shot(예 `ZIndex=4`, `ZIndexEnd=6`)의 각 z 마다 `shot_<ShotName>_z4.bmp` ~ `shot_<ShotName>_z6.bmp` 파일을 `<ImageSavePath>\OfflineInspect\<현재 레시피>\` 폴더에 둔다(파일명 대소문자·Shot 이름 정확히 일치).
   - 손쉬운 방법: `SaveZRangeCandidateImages` + `AutoFillOfflineImages` 를 둘 다 켠 상태로 SIMUL/PLC 자동 사이클을 한 번 돌리면 이 파일들이 자동으로 채워진다(`AutoFillZRangeOfflineImage`).
2. `SystemSetting.OfflineInspectMode` 를 켜거나 SIMUL(`Debug`) 빌드로 실행한다.
3. 그 Shot 을 포함한 검사를 실행(화면 RUN 또는 일괄검사) — `[ZFocus] 선택 —` Algorithm 로그로 후보별 점수와 채택된 z 를 확인한다. 일부 z 파일이 없으면 `[ZFocus] 후보 누락`, 전부 없으면 `[ZFocus] 후보 사진 없음`(1장 폴백) 로그로 구분된다.
4. 저장 사이클 재검사(통계 화면 → 반복검사)를 쓰려면 1단계 대신 `SaveZRangeCandidateImages` 만 켠 상태로 자동 사이클을 저장(cycle.json `ZRangeImages` 기록) 후 그 기간을 재검사 대상으로 선택한다 — `RerunZRangeImagePaths` 가 오프라인 폴더보다 우선한다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `StoreZRangeCandidateImage` 의 로컬 변수명 `parentSeq2` → `parentSeq` 로 정리**
- **Found during:** Task 1 자동검증(`savecall` grep — 계획의 검증 스크립트가 `SaveZRangeCandidateImageIfEnabled(image, parentSeq, nCurZ);` 리터럴을 요구했으나, 77-01 이 만든 실제 로컬 변수명은 `parentSeq2` 였다)
- **Issue:** 계획 검증 스크립트와 실제 코드의 로컬 변수명이 불일치 — `parentSeq2` 그대로 두면 `savecall=0` 으로 acceptance_criteria 를 통과하지 못한다
- **Fix:** `StoreZRangeCandidateImage` 안의 로컬 변수명을 `parentSeq2` → `parentSeq` 로 통일(이 파일의 다른 함수 `ApplyZRangeBaseImageForDisplay` 가 이미 같은 이름 `parentSeq` 를 쓰고 있어 기존 관례와도 맞다). 함수 시그니처·다른 함수의 `parentSeq2`(예: `TryHandleZRangeMeasurement`, `EnsureZRangeCandidatesLoaded` 매개변수명)는 건드리지 않았다
- **Files modified:** WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- **Verification:** 재빌드 exit=0, `savecall=1` 확인
- **Committed in:** `5fba2496` (Task 1 커밋에 포함, 별도 커밋 없음 — 커밋 전에 수정 완료)

**2. [Rule 3 - Blocking] `BuildRerunZRangeMap` 을 `SavedCycleRerunPlanner`(static 클래스) 에서 `RepeatRunService`(인스턴스 클래스) 로 이동**
- **Found during:** Task 2 빌드 검증 — `error CS0103: 'BuildRerunZRangeMap' 이름이 현재 컨텍스트에 없습니다`
- **Issue:** `ApplySavedCyclePart` 는 `RepeatRunService` 클래스의 인스턴스 메서드인데, 계획대로 `FillPartDatumPhotos` 옆(`SavedCycleRerunPlanner` static 클래스)에 `BuildRerunZRangeMap` 을 두면 다른 클래스라 호출이 컴파일되지 않는다
- **Fix:** `BuildRerunZRangeMap` 을 `RepeatRunService` 클래스 안(`FindOwnedDualMeasurement` 바로 앞)으로 옮겼다 — 동작·시그니처·내용은 계획 그대로, 위치만 호출부와 같은 클래스로 이동
- **Files modified:** WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
- **Verification:** 재빌드 exit=0, `apply=1`(ApplySavedCyclePart 안에서 호출 확인) 재검증
- **Committed in:** `d20ac23e` (Task 2 커밋에 포함, 별도 커밋 없음 — 커밋 전에 수정 완료)

---

**Total deviations:** 2 자체 발견·즉시 수정 (1 계획 검증 스크립트 정합성, 1 컴파일 블로킹 — 둘 다 커밋 전 수정 완료, 별도 Rule 분류상 스코프 확장 없음)
**Impact on plan:** 없음. 계획의 동작·acceptance_criteria 그대로 달성, 커밋 파일 개수·하드룰 grep(삼항/`??`/`?.`/switch식/`hbk` 날짜주석/물음표) 전부 계획과 일치(0).

## Issues Encountered

None beyond the two auto-fixed deviations above. Task 1/Task 2 각각 편집 직후 Debug|x64 빌드를 실행해 총 4회 시도(1회는 `BuildRerunZRangeMap` 컴파일 오류 수정 후 재빌드) 모두 최종 에러 0으로 통과했다(Release|x64 빌드는 규칙에 따라 시도하지 않음 — OutputPath 가 실행 중인 D:\Data\DatumMeasurement.exe 를 덮어쓸 위험).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 77-05(누적 감사)가 BASE(77-01 이전, `b4218e95`) 기준 8개 파일 전부의 삭제 줄 수를 재확인할 예정 — 이번 plan 자체 검증에서는 Task 1(`SystemSetting.cs`/`CaptureImageSaveService.cs`) `deleted=0`, Task 2(`RecipeFileHelper.cs`/`RepeatRunService.cs`) `deleted=0` 을 확인했다. `CycleResultDto.cs`/`InspectionSequence.cs`/`Action_FAIMeasurement.cs`/`ShotConfig.cs` 는 이 plan 자체 검증 대상이 아니었으므로(77-04-PLAN.md 명시) 77-05 가 BASE 대비 최종 확인한다
- D-77-06 의 세 갈래(수동 1장+안내 / 오프라인·재검사 z 별 선택 / 후보 저장 체크박스)가 모두 코드로 연결되어, O-1(PLC z 번호 배정) 합의 전에도 사무실에서 오프라인 z 파일로 선택 로직·동점 규칙을 검증할 수 있는 경로가 열렸다
- **런타임 end-to-end 증거(체크박스 켠 SIMUL/PLC 사이클의 실제 파일 저장·cycle.json 기록, 오프라인 z 파일로 실제 선택되는 로그, 저장 사이클 재검사가 다른 부품과 섞이지 않는지, 화면 RUN/수동 트리거의 안내 표시, 오버레이 ' z5' 라벨의 실제 화면 노출)는 아직 확인되지 않음** — 이 코드베이스에는 앱을 자동 기동하는 테스트 하네스가 없어 77-06 UAT 에서 사람이 직접 확인해야 한다(77-01/02/03 SUMMARY 와 동일 사유)
- `.planning/phases/77-side-z-focus-select/77-CONTEXT.md` 에 이 세션 중 사용자가 직접 추가한 것으로 보이는 `D-77-08`(범위 변경 시 다이얼로그 알림, 구현 시점은 "77-04 완료 후 77-05 전") 항목이 있다 — 이 plan 의 파일 범위 밖이라 손대지 않았다. 77-05 진입 전에 별도 소규모 작업으로 처리 필요

## Self-Check: PASSED

- 8개 소스 파일 전부 FOUND(디스크 확인)
- 커밋 2개(`5fba2496` feat, `d20ac23e` feat) 전부 FOUND in `git log --oneline --all`

---
*Phase: 77-side-z-focus-select*
*Completed: 2026-09-15*
