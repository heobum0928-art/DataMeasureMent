---
phase: 76-side-datum
plan: 01
subsystem: vision-inspection
tags: [halcon, datum, side-inspection, pattern-align, edge-detection]

# Dependency graph
requires: []
provides:
  - "DatumConfig.IsVerticalLineDisabled (bool, Datum|Algorithm, VerticalTwoHorizontalDualImage 전용 노출)"
  - "DatumConfig.IsHorizontalOnlyActive() — 옵션 효력 판정 단일 진입점"
  - "DatumFindingService.TryFindDualImageHorizontalOnly — 세로 검출 없이 가로선+패턴매칭만으로 Find"
  - "DatumFindingService.TryMapTaughtOriginColumn — AlignPreTransform 으로 티칭 원점을 이동해 원점 X 산출"
affects: [76-02-side-datum-view, 76-03-side-datum-uat]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "새 if 분기만 추가하고 기존 코드는 한 줄도 수정하지 않는 회귀-안전 설계(옵션 OFF = 기존 줄 삭제 0)"
    - "AlignPreTransform(AffineTransPoint2d) 으로 ROI/원점을 패턴매칭 결과로 이동 — TryFindCircleTwoHorizontal 선례 재사용"
    - "0.0 을 '미설정' sentinel 로 쓰는 기존 DetectedRefAngle2 관례를 그대로 유지"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs

key-decisions:
  - "D-76-05 원점 X 산출식 = 후보 (i): AffineTransPoint2d(AlignPreTransform, RefOriginRow, RefOriginCol) 의 열. Y = 그 열에서 가로 결합선의 행, 각도 = 가로 결합선 각도 (회전 지렛대 효과를 버리는 후보 (ii) RefOriginCol+dCol 대신 채택)"
  - "DetectedRefAngle2 = 0.0(미설정 표식) 유지 — 가로각+90도 근사값은 채택하지 않음(Action_FAIMeasurement.cs:1396 캡처 게이트가 0.0 을 '2차 축 없음'으로 이미 해석)"
  - "옵션 ON 에서 패턴매칭 실패/transform 없음/티칭 원점 없음은 모두 Datum 실패 — 매칭 없는 원점으로 조용히 폴백하지 않음(PR-2)"
  - "직각성 검증(PERPENDICULAR_TOLERANCE_DEG)은 H-only 경로에서 생략 — 세로선이 없어 정의되지 않음. 가로 방향 15도 검사(HORIZONTAL_TOLERANCE_DEG)는 유지"
  - "티칭 경로(TryTeachVerticalTwoHorizontalDualImage) 무변경 — 기존 RefOriginRow/Col/RefAngleRad 를 재티칭 없이 델타 기준점으로만 재사용(D-76-07)"

requirements-completed: [SDV-01, SDV-02, SDV-04]

coverage:
  - id: D1
    description: "IsVerticalLineDisabled 옵션이 DualImage(SIDE) Datum 속성창에만 노출되고 INI 에 저장/복원되며 키 부재 시 false"
    requirement: "SDV-01"
    verification:
      - kind: unit
        ref: "grep hide_total=3/hide_tli=1/hide_cth=1/hide_vth=1 (IsHiddenForAlgorithm 3-case 검증)"
        status: pass
      - kind: other
        ref: "INI bool 하위호환 — ParamBase.Load Boolean case + Ini.cs IniValue.ToBool(false) 코드 경로 검증(Load 오버라이드 추가 없이 안전, RESEARCH.md §8)"
        status: pass
    human_judgment: false
  - id: D2
    description: "옵션 ON DualImage Datum 의 Find(자동 사이클 + 수동 Test Find 공용 경로)가 세로 검출 없이 가로 결합선+패턴매칭 원점만으로 성공"
    requirement: "SDV-02"
    verification:
      - kind: unit
        ref: "grep branch=1/calls=1/novert=0/map=1/angle2=1 (분기 위치, H-only 세로 미호출, 원점 산출식, DetectedRefAngle2 sentinel)"
        status: pass
    human_judgment: true
    rationale: "실제 SIDE 카메라의 세로 흐림 상태에서 Find OK/좌우 밀림 추종은 76-03 SIDE PC 실기 UAT 에서만 확인 가능 — 이 plan 은 자동 검증만 수행"
  - id: D3
    description: "옵션 OFF 는 기존 줄 삭제 0 — TOP/BOTTOM 및 기존 SIDE 회귀 없음"
    requirement: "SDV-04"
    verification:
      - kind: unit
        ref: "git diff -w BASE..HEAD 두 파일 모두 deleted=0, 하드룰 grep 5종(ternary/coalesce/nullcond/switchexpr/datesig) 0"
        status: pass
      - kind: unit
        ref: "MSBuild Debug|x64: errors=0, warn_codes={CS0169,CS0618}(계획 시점 경고 집합과 동일, 신규 경고 0)"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-09-11
status: complete
---

# Phase 76 Plan 01: SIDE Datum 세로선 끄기 옵션 — 검출 경로 Summary

**SIDE Datum 별 `IsVerticalLineDisabled` 옵션 추가 — 켜면 세로선 검출을 아예 호출하지 않고, 가로 결합선(FitLineContourXld)과 패턴매칭(AlignPreTransform)만으로 원점 X/Y/각도를 산출해 Datum Find 를 성공시킨다.**

## Performance

- **Duration:** 35min
- **Started:** 2026-09-11T10:03:00Z (추정)
- **Completed:** 2026-09-11T10:38:39Z
- **Tasks:** 2/2
- **Files modified:** 2 (DatumConfig.cs, DatumFindingService.cs) — 신규 `.cs` 파일 0, csproj 스테이징 0

## Accomplishments
- `DatumConfig.IsVerticalLineDisabled`(bool, 기본 false, `Datum|Algorithm`) + `IsHorizontalOnlyActive()` 추가 — VerticalTwoHorizontalDualImage(SIDE 1~4) 에만 실제 효력, TOP/BOTTOM(CircleTwoHorizontal)은 알고리즘 다름으로 자동 보호
- `DatumFindingService.TryFindVerticalTwoHorizontalDualImage` 진입부에 옵션 ON 분기 1개만 삽입 — 기존 본문(기존 줄) 무변경, 옵션 OFF 는 바이트 단위로 기존 동작과 동일
- `TryMapTaughtOriginColumn` — `AlignPreTransform`(패턴매칭 보정)으로 티칭 원점(`RefOriginRow/Col`)을 이동해 원점 X 산출(D-76-05). transform 없음/변환 예외/티칭 안 됨 3가지 모두 명시적 실패, 원본 좌표 폴백 없음(PR-2)
- `TryFindDualImageHorizontalOnly` — 세로 ROI 라인 검출기를 호출하지 않고 가로 A/B 에지 검출 + `FitLineContourXld`(기존과 동일 tukey 상수)만으로 가로 결합선을 구해 원점 Y/각도 산출, `DetectedRefAngle2 = 0.0`(미설정 표식) 유지, 화면용 세로 흔적(Line1Detected_*, Vertical_DetectedEdgeRows/Cols) 초기화
- Task 2: PropertyGrid 노출을 DualImage 전용으로 한정(`IsHiddenForAlgorithm` 3-case), 티칭 원점 미설정 가드, 가로 방향 15도 검사, `AngleTolerance` Pass/Fail/None 배지 평가를 기존과 같은 규칙으로 H-only 경로에 이식
- `[Datum.Vertical] skipped`, `[Datum.HorizontalOnly] ok`, `[Datum.HorizontalOnly] fail` 로그(ELogType.Algorithm) 추가 — 운영자가 어떤 모드로 원점이 만들어졌는지 로그로 확인 가능(T-76-05 mitigate)

## Task Commits

1. **Task 1 (tracer): 옵션 ON SIDE Datum 한 개가 가로선 + 패턴매칭만으로 Find 성공** - `bf9942e5` (feat)
2. **Task 2: 옵션 노출을 DualImage Datum 으로 한정하고 H-only Find 오류 상태/각도 배지 평가** - `85925579` (feat)

**Plan metadata:** (본 커밋 — final commit 단계에서 기록)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - `IsVerticalLineDisabled` 옵션 + `IsHorizontalOnlyActive()` + `IsHiddenForAlgorithm` 3-case 숨김 추가(+19줄, 삭제 0)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` - H-only 상수 8개 + 분기 1개 + `TryMapTaughtOriginColumn`/`TryFindDualImageHorizontalOnly` 신규 private 메서드 + 가드 3종(패턴 transform 없음/티칭 안 됨/가로 방향 이탈) + 각도 배지(+289줄, 삭제 0)

## Decisions Made

**D-76-05 원점 X 산출식(계획 단계 결정, RESEARCH.md §5 근거):** 후보 (i) `AffineTransPoint2d(AlignPreTransform, RefOriginRow, RefOriginCol)` 의 열 채택. `TryFindCircleTwoHorizontal`(:259-272)의 ROI 중심 이동과 동일 규약이며, `alignRigid` 가 `RefMatch` 중심 회전을 포함하므로 순수 평행이동만 반영하는 후보 (ii)(`RefOriginCol + dCol`)는 회전 지렛대 효과(거리×sinθ)를 버려 오차가 생긴다. Y = 그 X 에서 가로 결합선을 평가한 행, 각도 = 가로 결합선 각도.

**DetectedRefAngle2 = 0.0 유지(계획 단계 결정 1):** 후보 "가로각+90도(직교 가정)" 대신 기존 "미설정" sentinel 0.0 을 그대로 씀. 결정적 근거는 `Action_FAIMeasurement.cs:1396` 의 캡처 렌더 게이트(`if (dc.DetectedRefAngle2 != 0.0) cap.HasAxis2 = true;`)가 이미 0.0 을 "2차 축 없음"으로 해석해 D-76-06(View 는 가로선만)을 자동 충족시킨다는 점 — 근사값을 넣으면 저장된 검사 이미지에 검증되지 않은 세로선이 다시 나타난다.

**계획 단계 결정 2·3(티칭 경로/기존 데이터 재사용):** 티칭 무변경(D-76-07). H-only Find 는 기존 `RefOriginRow/Col/RefAngleRad` 를 델타 기준점으로만 읽으므로 재티칭 불필요.

**계획 단계 결정 4(직각성 검증):** 가로 방향 15도 검사(`HORIZONTAL_TOLERANCE_DEG`)는 유지하되 직각성 검사(`PERPENDICULAR_TOLERANCE_DEG`, 세로각 필요)는 H-only 경로에서 생략 — 세로선이 없으므로 정의되지 않음.

**계획 단계 결정 5(오프라인 이미지 자동 채움):** 코드 변경 없음(현행 유지) — D-76-03 취지(세로 이미지를 포커스 분석용으로 남김)와 부합. 76-03 UAT 한계 절에 기록 예정.

**계획 단계 결정 6(패턴매칭 실패 시):** 기존과 동일하게 Datum 실패(`MarkAlignFailed`, lenient D-10) — 패턴매칭 실패는 `TryComposeAlign` 1단계에서 이미 `ALIGN_FAIL`. 이 plan 이 새로 막은 것은 transform 이 아예 전달되지 않는 경로(`IsPatternAlignEnabled=false`, seq 없는 폴백)의 "requires pattern align" 실패뿐.

## Deviations from Plan

None - plan executed exactly as written. 두 태스크 모두 계획된 `<action>` 순서·인터페이스·파일 범위 그대로 구현했고, 자동 검증 블록(`<verify><automated>`)의 모든 항목이 계획 시점 라인 번호와 일치하는 실제 코드 상태에서 통과했다(BASE 커밋 `d716899` 기준 소스 라인 번호 드리프트 없음).

## Issues Encountered

None. `HTuple` 이 `System.IDisposable` 을 구현하는지 사전에 halcondotnet.dll 리플렉션으로 확인(`IsIDisposable=True`, `HasDisposeMethod=True`) 후 계획이 지시한 대로 새 메서드의 임시 `HTuple` 전부를 `finally` 에서 개별 try-catch Dispose 했다 — 기존 코드(`TryFindCircleTwoHorizontal` 등)는 임시 `HTuple` 을 Dispose 하지 않는 관례였지만, 이는 PATTERNS.md 가 명시한 "새 코드는 하드룰을 따르고 기존 위반을 복제하지 않는다" 지침에 따라 신규 코드에서만 개선했다(기존 줄은 무변경).

## User Setup Required

None - no external service configuration required.

## Verification Evidence

**BASE 커밋(plan 파일 커밋 시점):** `d716899bb0433944bebdd6527bde3d82c4367897`
**Task 1 커밋:** `bf9942e5`
**Task 2 커밋:** `85925579`

**빌드(Task 1, Task 2 각각 재확인):** `msbuild_exit=0`, `errors=0`, `warn_codes=warning CS0169 warning CS0618`(계획 시점 경고 집합과 동일, 신규 경고 종류 0)

**`git diff -w` 삭제 수(두 커밋 모두):**
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`: deleted=0
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`: deleted=0

**하드룰 grep 5종(추가 줄 한정, 두 파일 모두 0):** ternary(`\?[^?]*:`)=0, coalesce(`??`)=0, nullcond(`?.`)=0, switchexpr(`switch.*=>`)=0, datesig(`hbk`)=0

**Task 1 핵심 지표:** `prop=1`, `rule=2`, `branch=1`, `calls=1`, `honly=1`, `novert=0`, `map=1`, `angle2=1`, `fitconst=5`, `skiplog=1`, `oklog=1`, `faillog=1`, `patternmsg=1`

**Task 2 핵심 지표:** `hide_total=3`, `hide_tli=1`, `hide_cth=1`, `hide_vth=1`, `orient=2`, `orientmsg=1`, `badge=3`, `taught=1`, `degconst=3`, `order_ok=1`

**변경 파일 목록(두 커밋 모두):** 정확히 `DatumConfig.cs` `DatumFindingService.cs` 두 파일 — `InspectionSequence.cs`/`Action_FAIMeasurement.cs`/`MainView.xaml.cs`/`FAIEdgeMeasurementService.cs`/`DatumMeasurement.csproj` 미포함, `git show --stat` 에 csproj 없음 확인.

## 새 심볼 목록 (76-02 가 재사용)

| 심볼 | 종류 | 파일 |
|---|---|---|
| `IsVerticalLineDisabled` | public bool 프로퍼티 | DatumConfig.cs |
| `IsHorizontalOnlyActive()` | public bool 메서드 — **옵션 효력 판정의 유일한 진입점, 76-02 가 그대로 재사용** | DatumConfig.cs |
| `TryMapTaughtOriginColumn` | private bool 메서드 | DatumFindingService.cs |
| `TryFindDualImageHorizontalOnly` | private bool 메서드 | DatumFindingService.cs |
| `DETECTED_REF_ANGLE2_UNSET`, `MIN_HORIZONTAL_LINE_SPAN_PX`, `RAD_TO_DEG`, `HORIZONTAL_FIT_ALGORITHM`, `HORIZONTAL_FIT_MAX_POINTS`, `HORIZONTAL_FIT_CLIP_END_POINTS`, `HORIZONTAL_FIT_ITERATIONS`, `HORIZONTAL_FIT_CLIP_FACTOR` | private const | DatumFindingService.cs |
| `DEG_FULL_TURN`, `DEG_HALF_TURN`, `DEG_QUARTER_TURN` | private const | DatumFindingService.cs |
| `[Datum.Vertical] skipped`, `[Datum.HorizontalOnly] ok`, `[Datum.HorizontalOnly] fail` | ELogType.Algorithm 로그 태그 | DatumFindingService.cs |
| `requires pattern align`, `Horizontal line too short`, `taught origin missing`, `Horizontal line orientation out of range` | Find 오류 문구 | DatumFindingService.cs |

## Flagged Assumption A-76-E1 (SDV-02, unclassified edge — 미해결로 유지)

자동 분류기가 SDV-02 의 경계 사례를 분류하지 못했다. 이 plan 이 직접 다루는 경계: 패턴매칭 실패(TryComposeAlign 1단계, 기존 그대로 ALIGN_FAIL) / 패턴 transform 없음(명시적 실패) / 티칭 원점 없음(RefOrigin 0,0 → 실패, Task 2) / 가로 에지 부족·피팅 실패 / 가로선 방향 15도 초과(Task 2) / 가로선 열 폭 1px 미만 / 세로·가로 이미지 크기 불일치(기존 SameFrame 가드).

**다루지 않는 것:** 패턴매칭이 "성공했지만 틀린 위치"(false match)일 때, 옵션 ON 은 세로선이라는 독립 교차검증이 없어 원점 X 가 틀린 매칭을 그대로 따른다. 가로 ROI 는 여전히 매칭으로 옮겨지므로 위아래로 크게 틀린 매칭은 가로 에지 실패로 걸러지지만, 가로 에지를 따라 좌우로만 틀린 매칭은 걸러지지 않는다. 측정값 자체는 원래부터 매칭만으로 정해지므로(§3, `InspectionSequence.cs:3044,3049`) 측정 영향은 옵션과 무관하다. **사용자 확인 필요 — 76-03 UAT U-3/U-5 에서 관찰할 것.**

## Threat Flags

없음 — 이 plan 의 신규 표면(INI 키, 검출 분기, 로그)은 phase 76-01-PLAN.md 의 `<threat_model>` STRIDE 등록부(T-76-01~06)에 이미 전량 기재되어 있고, 계획대로 mitigate 완료(각 항목의 자동 검증 지표가 대응 `<acceptance_criteria>` 에 매핑됨). 추가로 발견된 미등록 표면 없음.

## Next Phase Readiness

- 76-02(View 는 가로선만, D-76-06)가 착수 가능 — `IsHorizontalOnlyActive()`, `DetectedRefAngle2` sentinel(=0.0), `Line1Detected_*`/`Vertical_DetectedEdgeRows/Cols` 초기화(0.0/new HTuple())가 이미 이 plan 에서 완료되어 76-02 는 `HalconDisplayService.RenderDatumFindResult` 의 "수직 기준선" 블록(L450-470 근방)에 `if (!datum.IsHorizontalOnlyActive())` 게이트만 추가하면 된다.
- 76-03(실기 UAT + VersionDefine + HUMAN-UAT.md)은 이 plan 과 76-02 완료 후 SIDE PC 에서 진행.
- 블로커 없음. A-76-E1(false match 교차검증 상실)은 사용자 확인 필요 항목으로 76-03 UAT 에 이관.

---
*Phase: 76-side-datum*
*Completed: 2026-09-11*
