---
phase: 79-side-local-strip-datum
plan: 04
subsystem: vision-measurement
tags: [versioning, regression-audit, halcon, csharp7.2]

requires:
  - phase: 79-01
    provides: "국부 기준선 전체 경로(피팅·저장소·주입·전환), OffRegressProbe/LocalRefProbe synthetic·ini·real1"
  - phase: 79-02
    provides: "cycle.json/CSV RefSource 기록, 결과 그리드·리뷰어 '기준' 열, LocalRefProbe records·display"
  - phase: 79-03
    provides: "MainView ROI 배선 7함수 subKey 분기, HalconDisplayService 주황 오버레이, RoiRegressProbe, LocalRefProbe roidefs·overlaycolor"
provides:
  - "VersionDefine.cs 1.7.50.0 changelog — 79-01~03 Phase 79 변경 전체를 쉬운 한국어로 요약, 기존 changelog 항목 삭제 0"
  - "Phase 79 누적 회귀 감사 증거 — 변경 파일 범위(scope_match=1), 삭제 범위, CLAUDE.md 하드룰 grep(전부 0), Rebuild 경고 증가 0, LocalRefProbe 전체 모드 PASS, OffRegressProbe·RoiRegressProbe 편집 전 exe 대비 비트 동일"
affects: [79-05-realab-uat]

tech-stack:
  added: []
  patterns:
    - "누적 감사는 79-01 시작 전 HEAD(base-79-01)를 유일한 비교 기준으로 삼아 전체 diff 범위·삭제 줄·하드룰을 한 번에 검증 — plan별 부분 검증(79-01~03 각 SUMMARY)의 합집합이 아니라 병합 후 재확인"
    - "OffRegressProbe/RoiRegressProbe 는 79-04 에서 재컴파일하지 않고 79-01/79-03 이 만든 exe 를 그대로 재사용 — '편집 전 exe' 기준을 phase 전체에서 고정"

key-files:
  created: []
  modified:
    - WPF_Example/VersionDefine.cs

key-decisions:
  - "changelog 문구는 79-01~03 SUMMARY 의 실제 상수·필드 이름(IsLocalRefEnabled, RefSource, LocalRef_Row/Col/Phi/Length1/Length2, [LocalRef] 전역 기준선으로 전환, 사용기준, FAI-RefLine)을 그대로 인용 — 새 표현으로 재서술하지 않아 코드-문서 불일치 위험을 없앰"
  - "BUILD_DATE 는 이 plan 실행일(2026-09-18)로 갱신 — 79-01~03 과 같은 날이라 changelog Date 필드도 동일"
  - "Task 2 는 코드 변경이 없으므로 별도 task 커밋을 만들지 않고, 감사 결과는 이 SUMMARY 커밋 한 번으로만 기록"

requirements-completed: [LSR-05]

coverage:
  - id: D1
    description: "VersionDefine.VERSION/BUILD_DATE 가 1.7.50.0/2026-09-18 로 갱신되고, 1.7.49.0 항목 위에 Phase 79 변경(5가지 핵심 동작)을 쉬운 한국어로 적은 [Version] 항목이 기존 항목 삭제 없이 하나 더 쌓인다"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "grep number=1 version=1 prev_kept=1 order=1, kw[Local Ref]=1 kw[IsLocalRefEnabled]=1 kw[RefSource]=1 kw[사용기준]=1 kw[[LocalRef] 전역 기준선으로 전환]=1 kw[기준점 가로 사진]=1 kw[기준값]=1, deleted=2 qmark=0 datesig=0"
        status: pass
    human_judgment: false
  - id: D2
    description: "phase 79 누적 감사 — 79-01 시작 전 HEAD 대비 변경 파일이 계획한 15개와 정확히 일치하고, 새 .cs 0, csproj Compile 항목 수 불변, CSV COLUMN_COUNT 14 유지"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "scope_match=1, newcs=0, compile_same=1, colcount14=1, colref16=1"
        status: pass
    human_judgment: false
  - id: D3
    description: "15개 파일의 추가 줄 전체에서 CLAUDE.md 하드룰(삼항·null 병합·null 조건·switch 식·날짜 서명·조건 3개 이상·물음표) 위반 0, 기존 줄 삭제는 계획한 3개 파일(12·1·2줄)뿐"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "hardrule_violations=0, deleted_unexpected=0 (EdgeToLineDistanceMeasurement.cs deleted=12, MeasurementHistoryCsvWriter.cs deleted=1, VersionDefine.cs deleted=2, 나머지 12개 파일 deleted=0)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Debug|x64 전체 Rebuild 가 성공하고, 79-01 편집 전 기준선 대비 새로 생긴 경고가 0개"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "msbuild_exit=0, warn_base=5 warn_head=5 warn_added=0"
        status: pass
    human_judgment: false
  - id: D5
    description: "LocalRefProbe 전체 모드(synthetic/real1/ini/records/display/roidefs/overlaycolor)가 새 스크래치 폴더에서 재컴파일 후 한 번에 통과하고, OffRegressProbe·RoiRegressProbe 가 각각 79-01/79-03 편집 전 exe 대비 비트 동일"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "probe_exit=0, synthetic_fail=0 ini_fail=0 records_fail=0 display_fail=0 roidefs_fail=0 overlaycolor_fail=0, real1 stripL anyfound=1 stripR anyfound=1 exceptions=0, fails=0, regress_diff=0 regress_total=198, roi_regress_diff=0"
        status: pass
    human_judgment: false

duration: 약 15분
completed: 2026-09-18
status: complete
---

# Phase 79 Plan 04: 버전 1.7.50.0 + 누적 회귀 감사 Summary

**VersionDefine.cs 에 1.7.50.0 changelog 를 쌓고, 79-01~03 이 만든 15개 파일 전체를 대상으로 변경 범위·삭제 범위·CLAUDE.md 하드룰·Rebuild 경고·probe 전체·편집 전 exe 비트 비교를 한 번에 감사해 전부 PASS 를 확인했다.**

## Performance

- **Duration:** 약 15분 (Task 1 버전 기록·빌드·커밋 → Task 2 누적 감사 5종 순차 실행)
- **Completed:** 2026-09-18T05:38:26Z
- **Tasks:** 2 (Task 1 버전 기록, Task 2 누적 회귀 감사 — 코드 변경 없음)
- **Files modified:** 1 (VersionDefine.cs)

## Accomplishments

- `VersionDefine.cs` 에 `[Version(Number = "1.7.50.0", Date = "2026-09-18", ...)]` 항목을 1.7.49.0 항목 위에 추가 — 79-01(국부 기준선 경로·전환·하위호환), 79-02(기록·표시), 79-03(ROI 배선·오버레이 색)의 핵심 동작 5가지를 실제 SUMMARY 의 상수·필드 이름 그대로 인용해 쉬운 한국어로 요약. 기존 changelog 항목(1.4.0.0~1.7.49.0)은 한 줄도 삭제되지 않음
- `VERSION`/`BUILD_DATE` 를 `"1.7.50.0"`/`"2026-09-18"` 로 갱신(이 두 줄만 수정, `deleted=2`)
- Phase 79 누적 회귀 감사 — 79-01 시작 전 HEAD(`0e4de450`) 대비 변경된 `WPF_Example` 파일이 계획한 15개와 정확히 일치(`scope_match=1`), 새 `.cs` 파일 0개, `csproj` `<Compile Include>` 항목 수 불변, `MeasurementHistoryCsvLoader.COLUMN_COUNT` 14 유지
- 15개 파일의 추가 줄 전체에 대해 CLAUDE.md 하드룰 grep(삼항·null 병합·null 조건·switch 식·날짜 서명·조건 3개 이상·물음표) 실행 — 위반 0건. 삭제는 계획한 3개 파일뿐(`EdgeToLineDistanceMeasurement.cs` 12줄, `MeasurementHistoryCsvWriter.cs` 1줄, `VersionDefine.cs` 2줄), 나머지 12개 파일은 삭제 0줄
- Debug|x64 전체 Rebuild 성공, 79-01 편집 전 기준선(`build-base.log`, 경고 5개) 대비 새로 생긴 경고 0개
- `LocalRefProbe`(재컴파일) 전체 7개 모드가 새 스크래치 폴더(`$H/audit79`)에서 한 번에 통과, `OffRegressProbe`·`RoiRegressProbe`(재컴파일 없이 79-01/79-03 편집 전 exe 그대로 재사용)가 각각 편집 전 exe 대비 측정값·ROI 배선 결과 비트 동일

## Task Commits

Each task was committed atomically:

1. **Task 1: 버전 1.7.50.0 기록 (VersionDefine [Version] 항목 쌓기)** - `aee7f913` (chore)
2. **Task 2: Phase 79 누적 회귀 감사** - 코드 변경 없음, 이 SUMMARY 커밋(아래)이 유일한 기록

**편집 전 기준(base-79-01, phase 79 전체 diff 비교 기준):** `0e4de45071e007ce40f7e4fbdfdfcdee0b7a1b56`
**편집 전 기준(base-79-04, 이 plan 시작 전 HEAD):** `a99b06ddc9ab3b7b9eb0228e962bfe8968c88454`

_TDD 없음 — Task 1 은 grep 기반 automated verify, Task 2 는 코드 변경이 없는 순수 감사라 probe/grep/빌드 재실행으로 검증했다._

## Files Created/Modified

- `WPF_Example/VersionDefine.cs` - `[Version(Number = "1.7.50.0", ...)]` changelog 항목 추가(기존 항목 유지), `VERSION`/`BUILD_DATE` 갱신

## Decisions Made

- changelog 문구는 79-01~03 SUMMARY 의 실제 상수·필드 이름(`IsLocalRefEnabled`, `RefSource`, `LocalRef_Row/Col/Phi/Length1/Length2`, `[LocalRef] 전역 기준선으로 전환`, `사용기준`, `FAI-RefLine`)을 그대로 인용해 새로 재서술하지 않았다 — 코드와 문서 표현이 어긋날 위험을 없앤다
- `BUILD_DATE` 는 이 plan 실행일(2026-09-18)로 갱신했다 — 79-01~03 도 같은 날 완료되어 changelog `Date` 필드도 `2026-09-18` 로 통일된다
- Task 2 는 코드를 한 줄도 바꾸지 않으므로 별도 task 커밋을 만들지 않았다 — 감사 결과는 이 SUMMARY 파일과 그 커밋 하나로만 남긴다

## Deviations from Plan

None - 계획대로 실행. Task 1·Task 2 모두 verify 명령이 1회에 전부 통과했고, 실패가 없어 "수정하지 않고 중단" 절차는 발동하지 않았다.

## Issues Encountered

없음. Debug 앱이 실행 중이지 않아 Rebuild 가 exe 잠금 없이 정상 완료됐다(`tasklist` 로 사전 확인). Git Bash 환경 메모(79-01~03 과 동일) — probe 실행 시 파일 인자는 `C:/...`(Windows 표기) 경로를 사용했다.

## Verify 출력 원문

### Task 1 — 버전 기록

```
msbuild_exit=0
number=1 version=1 prev_kept=1 order=1
kw[Local Ref]=1
kw[IsLocalRefEnabled]=1
kw[RefSource]=1
kw[사용기준]=1
kw[[LocalRef] 전역 기준선으로 전환]=1
kw[기준점 가로 사진]=1
kw[기준값]=1
deleted=2 qmark=0 datesig=0
commit_files=WPF_Example/VersionDefine.cs
```

### Task 2 — 변경 범위·삭제 범위

```
scope_match=1
got=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs WPF_Example/Halcon/Display/HalconDisplayService.cs WPF_Example/UI/ContentItem/MainView.xaml WPF_Example/UI/ContentItem/MainView.xaml.cs WPF_Example/UI/Reviewer/ReviewerWindow.xaml WPF_Example/UI/ViewModel/CycleResultDto.cs WPF_Example/UI/ViewModel/MeasurementResultRow.cs WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs WPF_Example/VersionDefine.cs
newcs=0 compile_same=1 colcount14=1 colref16=1
```

### Task 2 — 파일별 하드룰·삭제 수

```
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs deleted=12 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/Halcon/Display/HalconDisplayService.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/UI/ContentItem/MainView.xaml deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/UI/ContentItem/MainView.xaml.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/UI/Reviewer/ReviewerWindow.xaml deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/UI/ViewModel/CycleResultDto.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/UI/ViewModel/MeasurementResultRow.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0
WPF_Example/VersionDefine.cs deleted=2 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0

hardrule_violations=0
deleted_unexpected=0
```

### Task 2 — Rebuild 경고 비교

```
msbuild_exit=0
warn_base=5 warn_head=5 warn_added=0
```

(기준선 경고 5개는 전부 79-01 이전부터 있던 `CS0618`(TopSequence/BottomSequence/TopSideInspectionAction/BottomInspectionAction 사용 중단, Phase 33 마이그레이션 잔재)·`CS0169`(AlignShapeMatchService._matcher2 미사용 필드) — Phase 79 와 무관, 새로 늘지 않음)

### Task 2 — probe 전체 요약

```
synthetic_fail=0
ini_fail=0
records_fail=0
display_fail=0
roidefs_fail=0
overlaycolor_fail=0
real1 stripL anyfound=1 stripR anyfound=1 exceptions=0
fails=0
```

(각 모드 개별 사례 원문은 79-01/79-02/79-03 SUMMARY 의 "Probe 출력 원문" 절 참고 — 이 plan 은 같은 exe·같은 케이스를 새 스크래치 폴더에서 재실행해 재확인한 것으로 값 자체는 동일)

### Task 2 — 편집 전 exe 비트 비교

```
regress_diff=0 regress_total=198
roi_regress_diff=0
```

`regress_total=198` 은 79-01-SUMMARY 의 `regress-base.txt total=198`(합성 6 + main-snapshot.ini `EdgeToLineDistance` 96섹션 × 실사진 2장)과 동일 — Phase 79 전체(79-01~04)가 합쳐진 뒤에도 옵션 꺼진 측정·옛 레시피 값이 편집 전 exe 와 바이트 단위로 같음을 재확인했다(D-79-04). `roi_regress_diff=0` 은 79-03 이 만든 616케이스(main-snapshot.ini 96섹션 × 6연산 + 합성 4측정)가 여전히 편집 전 exe 와 같음을 뜻한다.

## New Symbols (79-01 Artifacts 표 79-04 행 대조)

| 심볼 | 파일 | 확인 |
|---|---|---|
| `[Version(Number = "1.7.50.0", ...)]` | VersionDefine.cs | O |
| `VERSION = "1.7.50.0"`, `BUILD_DATE = "2026-09-18"` | VersionDefine.cs | O |

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 79-05(realab UAT)는 이번 감사로 D-79-04(옵션 꺼짐·옛 레시피 회귀 0)와 D-79-08(Datum 알고리즘·Z 선택·프로토콜 무변경)의 자동 증거를 확보했으므로, 남은 것은 사람 판단(A-79-E1~E4, A-79-A1~A7, U-1~U-7)뿐이다
- 배포(Release 빌드, D:/Data 덮어쓰기)는 이 plan 범위 밖 — 79-05 UAT 승인 후 사용자 확인을 거쳐 별도 진행
- `RoiRegressProbe.exe`/`OffRegressProbe.exe`(79-01/79-03 편집 전 exe 고정 참조)는 이후에도 재컴파일하지 않고 그대로 재사용 가능(이번 79-04 감사에서도 재사용 확인)

## Self-Check: PASSED

수정 파일 1개(`WPF_Example/VersionDefine.cs`) 존재 확인, 커밋 해시(`aee7f913`) `git log --oneline --all` 에서 확인됨.

---
*Phase: 79-side-local-strip-datum*
*Completed: 2026-09-18*
