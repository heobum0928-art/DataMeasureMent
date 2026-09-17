---
phase: 78-reviewer-ng-cause-analysis
plan: 02
subsystem: ui
tags: [wpf, halcon, reviewer, cycle-json, ng-cause-analysis, rule-engine]

requires:
  - phase: 78-reviewer-ng-cause-analysis (78-01)
    provides: "NgCauseResult/NgCauseHistory/NgCauseAnalyzer 골격(R0/R1~R4/R6/RX), NGA-07 DTO 계약(ZCandidateScoreDto/DatumDiagnosticDto/ZRangeStartIndex/ZRangeEndIndex)"
provides:
  - "R9(공차 경계 흔들림)·R8(초점 범위 끝)·R7(한 곳만 벗어남)·R5(기준점 흔들림) 규칙 (CycleResultDto.cs NgCauseAnalyzer)"
  - "여러 규칙 동시 발동 시 고정 순서(R5강→R9→R6→R8→R7→R5약) 발동 목록 조립 → 대표 1개 + 함께 의심(SuspectCodes/SuspectText)"
  - "ResolveZRangeEnd — cycle.json 기록값·검사 당시 tick z 번호만 사용(현재 레시피 참조 0, PR-4)"
  - "R5 묶음 계산(TryFindDistLineUnit/BuildR5Group/ComputeR5Movements/Median) + 기준점 진단 근거(BuildDatumEvidenceText)"
affects: [78-03, 78-04, 78-05, 78-06, 78-07]

tech-stack:
  added: []
  patterns:
    - "규칙 1건 = RuleHit(Code/CauseText/EvidenceText/ActionText) private class, 발동 목록에 순서대로 추가 → 첫 항목이 대표, 나머지가 SuspectCodes/SuspectText"
    - "R8/R9/R7/R5 전부 cycle.json DTO 값만 읽는 순수 정적 메서드 — 78-01 과 동일 원칙(파일 I/O·전역 싱글턴·레시피 참조 0)"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs

key-decisions:
  - "R9/R7 근거 문구의 '측정 {0}' 은 측정명이 아니라 측정값(F4) — 78-01 의 R0_EVIDENCE_FORMAT 관례('측정 {0}'={값})를 그대로 따름. R9 근거 {3}(경계를 넘은 방향)은 SIDE_BIG/SMALL_TEXT('큰'/'작은', R6 전용 '~쪽' 어미)와 문법이 안 맞아 별도 R9_DIR_ABOVE_TEXT('위로')/R9_DIR_BELOW_TEXT('아래로') const 를 신설(PLAN 미명시, 계획 표의 '{3}' 정의만 있고 값은 실행자 재량)"
  - "R5 이동량 부호 정렬: 스펙 문장 '단위벡터·기준축 내적이 0 이상이면 −편차'의 '단위벡터'는 그 멤버 자신의 발끝→에지 단위벡터(기준축이 아님)로 해석 — r5_opposite_geometry 사례(서로 다른 오버레이 방향인데 같은 물리적 이동으로 묶여야 함)로 이 해석이 유일하게 기대값과 일치함을 확인"
  - "R5Member 는 계획의 5개 필드(Measurement/Deviation/UnitRow/UnitCol/HalfWidth)만 유지하고, 부호 정렬된 이동량은 별도 List<double>(ComputeR5Movements)로 계산 — Movement 필드를 추가하지 않아 Artifacts 표의 필드 목록과 정확히 일치"

patterns-established: []

requirements-completed: [NGA-01, NGA-05]

coverage:
  - id: D1
    description: "R9(공차 경계 흔들림)·R8(초점 범위 끝)·R7(한 곳만 벗어남) 규칙이 판정되고, 여러 규칙 동시 발동 시 고정 순서(R9→R6→R8→R7)로 대표 1개 + 함께 의심이 조립된다"
    requirement: "NGA-01"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe <binDir> synthetic (repo 밖 probe) — r9_inside/r9_outside/r9_prev_ng/r9_zero_width/r6_min3/r6_two_only/r6_on_nominal/r6_fill_forward/r8_recorded/r8_not_end/r8_fallback_tick/r8_manual_skip/r8_recorded_wins/r7_lone/r7_suppressed_by_r6/order_r6_r8/order_r9_r8/order_same_time/precision_display/history_null 20개 전부 PASS"
        status: pass
    human_judgment: false
  - id: D2
    description: "R5(기준점 흔들림) 규칙이 같은 기준선(FAI-DistLine)을 쓰는 측정 묶음의 공통 이동으로 판정되고, 기준점 진단 값이 있으면 근거에 각도·패턴 점수가 붙는다"
    requirement: "NGA-01"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe <binDir> synthetic — r5_strong/r5_weak/r5_not_similar/r5_opposite_sign/r5_opposite_geometry/r5_single/r5_other_type/r5_below_ratio/r5_above_ratio/r5_datum_enrich/r5_datum_none 11개 전부 PASS"
        status: pass
    human_judgment: false
  - id: D3
    description: "20260915/20260916 실데이터에서 R5·R6·R8 이 CONTEXT 성공 기준 3(09-15 z1 기준점 흔들림, 09-16 보정/티칭 치우침·z5 끝값)대로 판정된다"
    requirement: "NGA-01"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe rows — 20260916 164530363_cycle C13_P1 대표 R6+함께 의심 R8(22건), 20260915 135946717_cycle C13_P3 대표 R5(R5 34건/R8 476건/R6 1765건, 전부 임계값 이상)"
        status: pass
    human_judgment: true
    rationale: "probe 는 저장소 밖 스크래치 코드로 커밋되지 않아 CI 에서 재실행되지 않는다 — 화면에 실제로 보이는지·문구가 읽히는지는 78-07 UAT U-1 에서 사람이 확인한다(A-78-E1 연장)"
  - id: D4
    description: "옛 cycle.json(20260601·20260811, 신규 필드 없음)에서 R5·R8 이 한 번도 나오지 않고, 쓰는 중인 20260917 폴더를 probe 로 읽어도 예외가 없다"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe rows D:/Data/Result/20260601 D:/Data/Result/20260811 D:/Data/Result/20260915 D:/Data/Result/20260916 D:/Data/Result/20260917 — old_r8r5=0, exceptions=0 전체 5개 날짜"
        status: pass
    human_judgment: false

duration: 약 26분
completed: 2026-09-17
status: complete
---

# Phase 78 Plan 02: NG 원인 R5/R7/R8/R9 규칙 + 대표/함께 의심 조립 Summary

**R9(공차 경계 흔들림)·R8(초점 범위 끝)·R7(한 곳만 벗어남)·R5(기준점 흔들림) 규칙과 고정 순서 발동 목록 조립을 CycleResultDto.cs 에 추가하고, 09-15/09-16 실데이터에서 R5·R6·R8 판정을 확인한 2커밋**

## Performance

- **Duration:** 약 26분
- **Started:** 2026-09-17T17:59(추정, 78-01 마지막 커밋 직후)
- **Completed:** 2026-09-17T18:24:56+09:00
- **Tasks:** 2 (둘 다 auto+tdd)
- **Files modified:** 1 (CycleResultDto.cs)

## Base / 커밋 해시

- **base-78-02 (편집 전 HEAD):** `1d3bd63e2fc8b6dddf0b68ccfaadc95f6c04b00c`
- **Task 1 커밋:** `42a20b46` — feat(78-02): NG 원인 R9·R8·R7 규칙과 대표/함께 의심 조립
- **Task 2 커밋:** `696bc7b8` — feat(78-02): NG 원인 R5 기준점 흔들림 규칙

## 빌드 결과

Debug|x64 (Release 금지, D:\Data 읽기만) — 두 커밋 시점 모두 `msbuild_exit=0`, errors 0.

## Probe synthetic 출력 원문 (누적 44사례, 마지막 실행 — Task 2 커밋 후)

```
case=r1_no_image expect=True/True/R1 got=True/True/R1 PASS
case=r2_datum_fail expect=True/True/R2 got=True/True/R2 PASS
case=r2_align_fail expect=True/True/R2 got=True/True/R2 PASS
case=r2_ref_missing expect=True/True/R2 got=True/True/R2 PASS
case=r3_measure_fail expect=True/True/R3 got=True/True/R3 PASS
case=r3_long_error expect=True/True/R3 got=True/True/R3 PASS
case=r4_zindex expect=True/True/R4 got=True/True/R4 PASS
case=r4_cross_z_not_ng expect=False/True/R4 got=False/True/R4 PASS
case=rx_unknown expect=True/True/RX got=True/True/RX PASS
case=pending_not_ng expect=False/False/ got=False/False/ PASS
case=ok_not_ng expect=False/False/ got=False/False/ PASS
case=null_measurement expect=False/False/ got=False/False/ PASS
case=null_cycle expect=False/False/ got=False/False/ PASS
case=r9_inside expect=R9 got=R9 PASS
case=r9_outside expect=R0 got=R0 PASS
case=r9_prev_ng expect=R0 got=R0 PASS
case=r9_zero_width expect=R0 got=R0 PASS
case=r6_min3 expect=R6 got=R6 PASS
case=r6_two_only expect=R0 got=R0 PASS
case=r6_on_nominal expect=R0 got=R0 PASS
case=r6_fill_forward expect=R6 got=R6 PASS
case=r8_recorded expect=R8 got=R8 PASS text=ok
case=r8_not_end expect=R0 got=R0 PASS
case=r8_fallback_tick expect=R8 got=R8 PASS text=ok
case=r8_manual_skip expect=R0 got=R0 PASS
case=r8_recorded_wins expect=R0 got=R0 PASS
case=r7_lone expect=R7 got=R7 PASS
case=r7_suppressed_by_r6 expect=R6 got=R6 PASS
case=order_r6_r8 expect=R6+R8 got=R6+R8 PASS
case=order_r6_r8_suspecttext text=ok
case=order_r9_r8 expect=R9+R8 got=R9+R8 PASS
case=order_same_time expect=R9 got=R9 PASS
case=precision_display expect=R9 got=R9 PASS text=ok
case=history_null expect=R0 got=R0 PASS
case=r5_strong expect=R5 got=R5 PASS
case=r5_weak expect=R6+R5 got=R6+R5 PASS
case=r5_not_similar expect=R0 got=R0 PASS
case=r5_opposite_sign expect=R0 got=R0 PASS
case=r5_opposite_geometry expect=R5 got=R5 PASS
case=r5_single expect=R0 got=R0 PASS
case=r5_other_type expect=R0 got=R0 PASS
case=r5_below_ratio expect=R6 got=R6 PASS
case=r5_above_ratio expect=R6+R5 got=R6+R5 PASS
case=r5_datum_enrich expect=R5 got=R5 PASS text=ok
case=r5_datum_none expect=R5 got=R5 PASS text=ok
synthetic_fail=0
```

(Task 1 커밋 시점 단독 실행도 `cases=20 textmissing=0`, `synthetic_fail=0` — 13(78-01)+20 = 33개.)

## Probe rows 출력 원문 (5개 날짜, Task 2 커밋 후)

```
date=20260601 cycles=8 loadnull=0 rows=322 ng=140 codes=R0:136,R1:0,R2:0,R3:0,R4:0,R5:0,R6:0,R7:4,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260811 cycles=411 loadnull=0 rows=10275 ng=338 codes=R0:0,R1:0,R2:338,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260915 cycles=1112 loadnull=0 rows=10008 ng=1992 codes=R0:32,R1:0,R2:159,R3:33,R4:0,R5:2,R6:1765,R7:0,R8:1,R9:0,RX:0 suspects=R5:32,R6:0,R7:0,R8:475,R9:0 newfields=0 exceptions=0
date=20260916 cycles=24 loadnull=0 rows=216 ng=36 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:36,R7:0,R8:0,R9:0,RX:0 suspects=R5:3,R6:0,R7:0,R8:22,R9:0 newfields=0 exceptions=0
date=20260917 cycles=328 loadnull=0 rows=2952 ng=789 codes=R0:0,R1:0,R2:0,R3:5,R4:0,R5:0,R6:784,R7:0,R8:0,R9:0,RX:0 suspects=R5:30,R6:0,R7:0,R8:52,R9:0 newfields=0 exceptions=0
```

named rows:
```
20260916/164530363_cycle	SIDE_SHOT_1_C13-14	FAI_C13-14	C13_P1	NG	1	R6	R8
20260915/135946717_cycle	SIDE_SHOT_1_C13-14	FAI_C13-14	C13_P3	NG	1	R5	-
```

## Verify 판정 숫자 (acceptance criteria 그대로)

| 지표 | 값 | 기준 |
|---|---|---|
| `n16_c13p1` | `R6\|R8` | = `R6\|R8` |
| `n15_c13p3` | `R5` | = `R5` |
| `d16_ng` / `d16_r6` | 36 / 36 | = 36 / 36 |
| `d16_r8any` | 22 | ≥ 20 |
| `d15_r5any` | 34 | ≥ 20 |
| `d15_r8any` | 476 | ≥ 400 |
| `d15_r6` | 1765 | ≥ 1700 |
| `old_r8r5` (20260601+20260811) | 0 | = 0 |
| `d01_r7` | 4 | ≥ 1 |
| `exceptions_nonzero`(5개 날짜, 20260917 포함) | 0 | = 0 |
| `consts`(Task1: R9_BOUNDARY_RATIO/R7_LONE_NG_COUNT/R8_NO_SCORES_TEXT) | 3 | = 3 |
| `resolver`(ResolveZRangeEnd 존재) | 1 | = 1 |
| `recipe`(NgCauseAnalyzer 안 현재 레시피·설정·파일 참조) | 0 | = 0 |
| `consts`(Task2: R5_MIN_SHIFT_TOL_RATIO/R5_SIMILAR_FACTOR/R5_SUPPORTED_TYPE_NAME/DIST_LINE_ROI_ID) | 4 | = 4 |
| `median`(Median 메서드 존재) | 1 | = 1 |
| 추가 줄 하드룰(ternary/coalesce/nullcond/switchexpr/datesig/logic3/lambda) | 전부 0 | = 0 |
| `deleted`(base-78-02 → HEAD 누적) | 9 | ≤ 40 |

## 임계값 const 값 (A-78-A1 재확인용 — SIDE_1 표본으로만 맞춤, TOP/BOTTOM·다른 제품은 UAT/운영 중 확인)

- `R9_BOUNDARY_RATIO = 0.10`
- `R6_WINDOW_SIZE = 5`, `R6_MIN_SAMPLES = 3` (78-01)
- `R5_NEIGHBORS_EACH_SIDE = 2`, `R5_MIN_NEIGHBORS = 2`, `R5_MIN_SHARED_COUNT = 2`
- `R5_MIN_SHIFT_TOL_RATIO = 0.15`, `R5_SIMILAR_FACTOR = 2.0`
- `R7_LONE_NG_COUNT = 1`

## 새 심볼 목록 (Artifacts 표 78-02 행과 대조)

| 심볼 | Artifacts 표 대조 |
|---|---|
| `RuleHit`(Code/CauseText/EvidenceText/ActionText, private) | 표에 없던 내부 구현 세부(발동 목록 조립용) — 계획 텍스트에 명시적으로 등장하지는 않으나 "발동 목록·함께 의심 조립" 산출물에 포함 |
| `TryEvaluateR9`/`TryEvaluateR8`/`TryEvaluateR7`/`ResolveZRangeEnd`/`BuildCandidateScoreText`/`CountNgMeasurements`/`GetRuleLabel`/`BuildR0Result`/`ResolveExceedSide`/`FindAnchorIndex` | 일치 — `ResolveZRangeEnd` 시그니처는 표의 verify 양성 grep 대상과 정확히 일치 |
| `R5Member`(Measurement/Deviation/UnitRow/UnitCol/HalfWidth), `Median`, `TryFindDistLineUnit`, `CollectNeighborValues`, `BuildR5Group`, `ComputeR5Movements`, `BuildDatumEvidenceText`, `TryEvaluateR5` | 일치 — `Median` 시그니처는 표의 verify 양성 grep 대상과 정확히 일치 |
| `R9_BOUNDARY_RATIO`(0.10), `R5_NEIGHBORS_EACH_SIDE`(2), `R5_MIN_NEIGHBORS`(2), `R5_MIN_SHARED_COUNT`(2), `R5_MIN_SHIFT_TOL_RATIO`(0.15), `R5_SIMILAR_FACTOR`(2.0), `R5_SUPPORTED_TYPE_NAME`("EdgeToLineDistance"), `DIST_LINE_ROI_ID`("FAI-DistLine"), `R7_LONE_NG_COUNT`(1), R5/R7/R8/R9 문구·라벨 const | 일치 |

## Task Commits

1. **Task 1: 발동 목록 조립 + R9 경계 흔들림 + R8 초점 범위 끝 + R7 한 곳만** - `42a20b46` (feat, tdd)
2. **Task 2: R5 기준점 흔들림 + 실데이터 성공 기준 3 확인** - `696bc7b8` (feat, tdd)

**Plan metadata:** (이 커밋) - `docs(78-02): complete NG 원인 규칙 확장 plan`

## Files Created/Modified

- `WPF_Example/UI/ViewModel/CycleResultDto.cs` - NgCauseAnalyzer 에 R9/R8/R7/R5 규칙 + RuleHit 발동 목록 조립 + R5 묶음 계산 헬퍼(Median/TryFindDistLineUnit/CollectNeighborValues/BuildR5Group/ComputeR5Movements/BuildDatumEvidenceText) 추가

## Decisions Made

- R9/R7 근거 문구의 "측정 {0}" 은 78-01 의 R0_EVIDENCE_FORMAT 관례를 따라 측정값(F4)으로 해석(측정명이 아님). precision_display 사례(2.10004 → "측정 2.1000")로 확인.
- R9 경계를 넘은 방향 표시는 R6 전용 `SIDE_BIG_TEXT`/`SIDE_SMALL_TEXT`("큰"/"작은")를 재사용하지 않고 `R9_DIR_ABOVE_TEXT`("위로")/`R9_DIR_BELOW_TEXT`("아래로")를 새로 만들었다 — "경계를 큰 넘음"은 비문이라 "경계를 위로 넘음"으로 자연스럽게 맞춤(계획의 `{3}` 값 자체는 실행자 재량으로 남겨져 있었음, Rule 1 성격은 아니고 명시 안 된 빈칸 채움).
- R5 이동량 부호 정렬 공식의 "단위벡터"는 그 멤버 자신의 오버레이 단위벡터로 해석(기준축이 아님) — `r5_opposite_geometry` 합성 사례(서로 다른 오버레이 방향의 두 측정이 같은 물리적 기준점 이동으로 묶여야 하는 경우)로 이 해석만이 기대값(R5 발동)과 일치함을 확인했다. 반대로 해석하면 `r5_opposite_geometry`가 R0 이 되어 계획 표의 기대와 어긋난다.
- R5Member 클래스는 계획이 명시한 5개 필드(Measurement/Deviation/UnitRow/UnitCol/HalfWidth)만 유지하고, 부호 정렬된 "이동량"은 멤버에 필드로 얹지 않고 `ComputeR5Movements`가 반환하는 별도 `List<double>`로 계산했다 — Artifacts 표의 필드 목록과 코드가 정확히 일치하게 하기 위함.

## Deviations from Plan

None - 계획대로 실행. R9 방향 문구·R5 단위벡터 해석은 계획 표에 값/공식은 있었으나 마지막 구현 디테일(어느 쪽 벡터인지, 어떤 한국어 어미인지)이 실행자 재량으로 열려 있던 지점이라 "Decisions Made"에 기록했다(Deviation Rule 이 아님 — 계획을 벗어난 것이 아니라 계획이 비워 둔 빈칸을 채운 것).

## Issues Encountered

- 첫 synthetic 실행에서 R9/R6/R7 계열 사례가 전부 실패했다(`r9_inside`, `r6_min3` 등). 원인: probe 헬퍼(`EvaluateCurrent`)가 이력에 "이웃" 사이클만 추가하고 현재 사이클 자체를 `NgCauseHistory.AddCycle`에 등록하지 않아, `TryEvaluateR9`/`TryEvaluateR6`의 앵커 탐색(`FindAnchorIndex`)이 현재 사이클 키를 찾지 못했다. 실제 리뷰어 흐름(`ReviewerWindow.LoadCycleFolders`)은 날짜 폴더의 모든 cycle.json(현재 보고 있는 것 포함)을 이력에 등록하므로, probe 헬퍼에 `ctx.History.AddCycle(cycle)` 호출을 추가해 실제 흐름과 맞췄다(Rule 1 성격의 probe 자체 수정 — 저장소 코드 아님, 별도 커밋 없음).

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 78-03(Z 후보 점수·기준점 진단 값 실제 기록)이 채울 `ZCandidateScores`/`DatumDiagnostics` 를 R8/R5 근거가 이미 소비하도록 연결되어 있다 — 78-03 완료 후 재실행하면 `newfields`>0 이 되고 R8/R5 근거의 점수·각도 텍스트가 실측값으로 채워진다.
- CONTEXT 성공 기준 3(09-15/09-16 R5·R6·R8 판정)이 실데이터로 확인됨.
- 화면에 실제로 보이는지·문구가 비전 초보에게 읽히는지는 78-07 UAT U-1 대기(A-78-E1 연장).
- A-78-A1(임계값이 SIDE_1 표본 기준) 은 그대로 유지 — TOP/BOTTOM 적용은 UAT/운영 중 확인.
- 블로커 없음.

---
*Phase: 78-reviewer-ng-cause-analysis*
*Completed: 2026-09-17*

## Self-Check: PASSED

- FOUND: `.planning/phases/78-reviewer-ng-cause-analysis/78-02-SUMMARY.md`
- FOUND commit: `42a20b46` (Task 1)
- FOUND commit: `696bc7b8` (Task 2)
- FOUND commit: `cebfb840` (plan metadata)
- FOUND: `WPF_Example/UI/ViewModel/CycleResultDto.cs`
