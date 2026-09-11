---
phase: 76-side-datum
plan: 02
subsystem: vision-inspection
tags: [halcon, datum, side-inspection, display, overlay, capture]

# Dependency graph
requires:
  - "76-01: DatumConfig.IsHorizontalOnlyActive() (단일 진입점)"
provides:
  - "HalconDisplayService.RenderDatumFindResult / RenderDatumDetectedOverlay — bDrawVertical 표시 게이트"
  - "EdgeInspectionOverlay.DatumCaptureOverlay.HideOriginVerticalArm — 캡처 스냅샷 플래그"
  - "Action_FAIMeasurement.BuildDatumCaptureSnapshot — H-only Datum 의 2차 축·원점 세로 팔 숨김 삽입"
  - "OverlayCaptureRenderer.DrawDatumRegions — HideOriginVerticalArm 분기(가로 팔만 vs 기존 십자)"
affects: [76-03-side-datum-uat]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "새 if 분기만 추가하고 기존 코드는 한 줄도 수정하지 않는 회귀-안전 설계(옵션 OFF = 기존 줄 삭제 0, git diff -w 기준)"
    - "표시 게이트는 76-01 의 IsHorizontalOnlyActive() 단일 진입점만 사용 — DetectedRefAngle2 값(sentinel)에 기대지 않음(Pitfall 1 회피)"
    - "캡처 경로는 값 복사 스냅샷(DatumCaptureOverlay) 으로 검사 스레드 → 캡처 워커 스레드 전달, HasAxis2=false 로 명시적 이중 방어"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs

key-decisions:
  - "A-76-E2 채택: D-76-06 '세로 기준 방향(원점 십자의 세로 팔 포함)' 을 결과화면 전체 길이 세로 기준선과 검출 원점 20px 십자의 세로 팔 둘 다로 해석해 둘 다 숨김. 티칭 원점 표시(마젠타 'Datum Origin' 십자, 빨강 교점 십자)와 세로 ROI 검색 사각형은 숨기지 않음(설정·티칭 기준 표시로 보고 범위 밖)"
  - "RenderDatumDetectedOverlay 의 SetColor(yellow)/SetLineWidth(2) 두 줄은 감싸지 않음 — Line2(청록) 가 이 굵기 2 를 이어 쓰므로 옮기면 옵션 ON 에서 가로선 굵기가 달라짐"
  - "Action_FAIMeasurement.cs 는 BuildDatumCaptureSnapshot 한 곳만 수정 — 세로 이미지 촬영/저장 경로(TryGrabOrLoadDualDatumImages 등)는 무변경(D-76-03)"

requirements-completed: [SDV-03, SDV-04]

coverage:
  - id: D1
    description: "옵션 ON Datum 의 결과 화면·Test Find 표시(RenderDatumFindResult)에서 전체 길이 세로 기준선이 그려지지 않고, 검출 원점 십자는 가로 팔만 그려진다 — 가로 기준선·각도 화살표·좌표 라벨·기대각 점선 화살표는 그대로"
    requirement: "SDV-03"
    verification:
      - kind: unit
        ref: "grep local=1/gates=2/varm_wrapped=1/harm_kept=1/vref_wrapped=1/href_kept=1/expected_kept=1 (Task 1)"
        status: pass
    human_judgment: true
    rationale: "실제 결과 화면·Test Find 에서 눈으로 세로선이 안 보이는지는 76-03 UAT U-6 에서 사용자가 확인"
  - id: D2
    description: "옵션 ON Datum 을 선택한 라이브 화면(RenderDatumDetectedOverlay)에서 세로 검출선(노랑)과 세로 에지점(주황)이 그려지지 않는다"
    requirement: "SDV-03"
    verification:
      - kind: unit
        ref: "grep do_local=1/do_gates=2/yellow_kept=1/line1_wrapped=1/line2_kept=1/vpts_wrapped=1/hpts_kept=1 (Task 2)"
        status: pass
    human_judgment: true
    rationale: "production 결과화면은 RenderDatumFindResult 만 호출하므로(align-enabled datum) 이 경로는 주로 편집/티칭 화면에서 관측됨 — 76-03 UAT 범위"
  - id: D3
    description: "옵션 ON Datum 이 들어간 저장 캡처 이미지에는 2차(세로) 기준선이 없고 검출 원점 십자는 가로 팔만 있다"
    requirement: "SDV-03"
    verification:
      - kind: unit
        ref: "grep capflag=1/snap_if=1/snap_axis2=1/snap_arm=1/snap_after_sentinel=1/af_calls=1/af_hunks=1/rend_if=1/rend_line=1/rend_left=1/rend_right=1/cross_in_else=1 (Task 2)"
        status: pass
    human_judgment: true
    rationale: "실제 저장된 JPEG 캡처 육안 확인은 76-03 UAT U-6"
  - id: D4
    description: "옵션 OFF Datum 과 TOP/BOTTOM Datum 의 화면·캡처 그리기는 이전과 같은 호출을 같은 순서로 실행한다 — 네 파일의 git diff -w 삭제 0"
    requirement: "SDV-04"
    verification:
      - kind: unit
        ref: "git diff -w BASE..HEAD 네 파일 모두 deleted=0, 하드룰 grep 5종(ternary/coalesce/nullcond/switchexpr/datesig) 0"
        status: pass
      - kind: unit
        ref: "MSBuild Debug|x64: errors=0, warn_codes={CS0169,CS0618}(76-01 과 동일 경고 집합, 신규 경고 0)"
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-09-11
status: complete
---

# Phase 76 Plan 02: SIDE Datum 세로선 끄기 옵션 — View 게이트 Summary

**옵션 ON SIDE Datum 은 결과 화면·Test Find·라이브 선택 화면·저장 캡처 네 표면 모두에서 세로 검출선·세로 에지점·세로 기준선·원점 십자 세로 팔을 그리지 않는다 — 게이트는 76-01 의 `IsHorizontalOnlyActive()` 단일 진입점만 사용.**

## Performance

- **Duration:** 45min
- **Started:** 2026-09-11T10:03:00Z (추정, 76-01 시작 시각 기준 이어서 실행)
- **Completed:** 2026-09-11T10:44:40Z
- **Tasks:** 2/2
- **Files modified:** 4 (HalconDisplayService.cs, EdgeInspectionOverlay.cs, Action_FAIMeasurement.cs, OverlayCaptureRenderer.cs) — 신규 `.cs` 파일 0, csproj 스테이징 0

## Accomplishments

- `HalconDisplayService.RenderDatumFindResult` — `bool bDrawVertical = !datum.IsHorizontalOnlyActive();` 게이트 추가. 원점 십자의 세로 팔(DispLine) 1곳과 세로 기준선 블록(vDirRow/vDirCol 계산 + DispLine, L457-478) 전체를 `if (bDrawVertical) { }` 로 감쌈. 가로 팔, 좌표 라벨, `DetectedRefAngle` 화살표, 가로 기준선, 기대각 점선 화살표는 무변경
- `HalconDisplayService.RenderDatumDetectedOverlay` — 동일한 `bDrawVertical` 게이트를 메서드 첫 줄에 추가. `Line1Detected` 외삽선(노랑, DualImage 에서는 세로 검출선) 호출과 `Vertical_DetectedEdgeRows/Cols` 원시 에지점(주황) 호출 2곳만 감쌈. `SetColor(yellow)`/`SetLineWidth(2)` 는 Line2 청록선이 굵기를 이어 쓰므로 감싸지 않음. 빨강 교점 십자(티칭 원점), Line2 청록선, 나머지 raw 점, CircleTwoHorizontal 원 블록은 그대로
- `EdgeInspectionOverlay.DatumCaptureOverlay` — `public bool HideOriginVerticalArm { get; set; }` 추가(기본 false, 메모리 전용 스냅샷 — 파일 직렬화 대상 아님)
- `Action_FAIMeasurement.BuildDatumCaptureSnapshot` — 기존 `DetectedRefAngle2 != 0.0` 센티널 블록 바로 다음에 `if (dc.IsHorizontalOnlyActive()) { cap.HasAxis2 = false; cap.HideOriginVerticalArm = true; }` 삽입. `HasAxis2 = false` 는 센티널에 기대지 않는 명시적 이중 방어. 파일의 다른 곳(세로 이미지 촬영/저장 경로)은 무변경 — 1개 hunk 로 확인(D-76-03)
- `OverlayCaptureRenderer.DrawDatumRegions` — 원점 십자 `if (d.HasOrigin) { }` 블록 안을 `if (d.HideOriginVerticalArm) { DrawLineAsRegion(가로 팔만) } else { DrawCrossAsRegion(기존 십자, 그대로) }` 로 분기. 2차 축 그리기(`d.HasAxis2`)는 이미 스냅샷 단계에서 false 라 자동으로 빠짐

## Task Commits

1. **Task 1 (tracer): 결과 화면·Test Find 표시(RenderDatumFindResult)에서 옵션 ON Datum 의 세로 기준선과 원점 십자 세로 팔이 사라진다** - `37c03dc6` (feat)
2. **Task 2: 라이브 선택 화면(RenderDatumDetectedOverlay)과 저장 캡처 이미지에서도 옵션 ON Datum 은 가로 요소만 그린다** - `e4ceab86` (feat)

**Plan metadata:** (본 커밋 — final commit 단계에서 기록)

## 숨긴 표시 요소 목록 (A-76-E2 해석 그대로)

| 표면 | 숨긴 것 | 숨기지 않은 것 |
|---|---|---|
| 결과 화면 / Test Find (`RenderDatumFindResult`) | 전체 길이 세로 기준선(원점 통과, `DetectedRefAngle2` 방향), 검출 원점 20px 십자의 세로 팔 | 가로 기준선, `DetectedRefAngle` 화살표, `Find (row, col)` 라벨, 기대각(`ExpectedAngleDeg`) 점선 화살표 |
| 라이브 선택 화면 (`RenderDatumDetectedOverlay`) | `Line1Detected_*` 외삽선(노랑, DualImage 에서는 세로 검출선), `Vertical_DetectedEdgeRows/Cols` 원시 에지점(주황) | 빨강 교점 십자(티칭 원점, `RefOrigin`), Line2 청록선, Line1/Line2/Circle/Horizontal_A/B raw 점, CircleTwoHorizontal 원 블록 |
| 저장 캡처 이미지 (`OverlayCaptureRenderer.DrawDatumRegions`) | 2차(세로) 기준선(`HasAxis2`), 검출 원점 십자의 세로 팔 | 1차(가로) 기준선(`HasAxis1`), 검출 원(녹색) + 중심 십자 |
| 세로 ROI 검색 사각형(편집 ROI, 결과 화면 주황 보정 박스), 티칭 원점 표시(마젠타 'Datum Origin' 십자) | — 범위 밖(설정·티칭 기준 표시로 해석) | 그대로 표시됨 |

## Flagged Assumption A-76-E2 (SDV-03, edge probe 'unclassified' — 미해결로 유지)

D-76-06 '세로 기준 방향(원점 십자의 세로 팔 포함)' 을 다음 두 가지를 모두 가리키는 것으로 해석해 **둘 다** 숨겼다:
(a) 결과 화면의 전체 길이 세로 기준선(원점을 지나는 큰 십자의 세로 팔), (b) 검출 원점 20px 십자의 세로 팔(화면과 저장 캡처 모두). 사용자 문구 '가로선과 가로 에지점만 보인다' 와 맞는 쪽을 택했다.
**숨기지 않은 것(설정·티칭 기준 표시로 보고 D-76-06 범위 밖으로 해석):** 티칭 원점 표시(마젠타 'Datum Origin' 십자 `RenderDatumRefOriginCross`, 빨강 교점 십자 — RefOrigin 위치),
세로 ROI 검색 사각형(편집 ROI, 결과 화면 주황 보정 박스).
**결과로 생기는 것:** 옵션 ON 에서 검출 원점의 가로 위치는 가로선 위 짧은 가로 표식과 'Find (row, col)' 라벨로만 보인다. 옵션 ON 상태로 티칭하면 세로 검출선(노랑)과 세로 에지점(주황)이
화면에 나오지 않으므로, 티칭 결과의 세로선을 눈으로 확인하려면 옵션을 잠시 끄고 본다(D-76-06 을 우선).
해석이 틀리면 76-03 UAT U-6 에서 사용자가 지적한다 — 모두 표시 전용 게이트라 되돌리기 쉽다(reversible).

## Deviations from Plan

None - plan executed exactly as written. 두 태스크 모두 계획된 `<action>` 순서·인터페이스·파일 범위 그대로 구현했고, 자동 검증 블록(`<verify><automated>`)의 모든 항목이 실제 코드 상태에서 통과했다. `RenderDatumDetectedOverlay` 의 `Vertical_DetectedEdgeRows` 호출은 감싸는 과정에서 원래 있던 정렬용 공백(다른 `RenderRawEdgePoints` 호출들과 인자 폭을 맞추던 패딩)이 한 줄짜리 호출로 정리되며 사라졌다 — `git diff -w`(공백 무시) 기준으로는 삭제 0 으로 집계되어 acceptance criteria 를 그대로 만족한다.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Verification Evidence

**BASE 커밋(76-01 plan 파일 커밋 시점):** `d716899bb0433944bebdd6527bde3d82c4367897`
**Task 1 커밋:** `37c03dc6`
**Task 2 커밋:** `e4ceab86`

**빌드(Task 1, Task 2 각각 재확인):** `msbuild_exit=0`, `errors=0`, `warn_codes=warning CS0169 warning CS0618`(76-01 시점과 동일 경고 집합, 신규 경고 종류 0)

**`git diff -w` 삭제 수(두 커밋 모두, 네 파일):**
- `WPF_Example/Halcon/Display/HalconDisplayService.cs`: deleted=0
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs`: deleted=0
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`: deleted=0
- `WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs`: deleted=0

**일반 `git diff`(공백 포함) 삭제 수:** `HalconDisplayService.cs` 만 20줄(Task 1 커밋 diffstat "28 insertions, 20 deletions") — 전부 기존 줄에 4칸 들여쓰기를 추가하며 생긴 줄 단위 치환(내용 무변경, `-w` 로 확인하면 0). 나머지 세 파일은 순수 추가만이라 일반 diff 삭제도 0.

**하드룰 grep 5종(추가 줄 한정, 네 파일 모두 0):** ternary(`\?[^?]*:`)=0, coalesce(`??`)=0, nullcond(`?.`)=0, switchexpr(`switch.*=>`)=0, datesig(`hbk`)=0

**Task 1 핵심 지표:** `local=1`, `gates=2`, `varm_wrapped=1`, `harm_kept=1`, `vref_wrapped=1`, `href_kept=1`, `expected_kept=1`

**Task 2 핵심 지표:** `do_local=1`, `do_gates=2`, `yellow_kept=1`, `line1_wrapped=1`, `line2_kept=1`, `vpts_wrapped=1`, `hpts_kept=1`, `capflag=1`, `snap_if=1`, `snap_axis2=1`, `snap_arm=1`, `snap_after_sentinel=1`, `af_calls=1`, `af_hunks=1`, `rend_if=1`, `rend_line=1`, `rend_left=1`, `rend_right=1`, `cross_in_else=1`

**변경 파일 목록(Task 2 커밋 시점, 76-01 두 파일 포함):** 정확히 `Action_FAIMeasurement.cs`, `DatumConfig.cs`, `DatumFindingService.cs`, `HalconDisplayService.cs`, `OverlayCaptureRenderer.cs`, `EdgeInspectionOverlay.cs` 여섯 파일 — 그 밖의 파일 0, csproj 미포함.

## Next Phase Readiness

- 76-03(실기 UAT + VersionDefine + HUMAN-UAT.md)이 착수 가능 — SDV-01~04 코드 구현이 모두 완료됨(76-01: 검출 경로, 76-02: 표시 게이트)
- 눈으로 보는 확인(결과 화면, Test Find, Datum 노드 선택, 저장 캡처 JPEG)은 76-03 UAT U-6 에서 사용자가 수행
- A-76-E1(false match 교차검증 상실, 76-01), A-76-E2(본 plan, 위) 둘 다 76-03 UAT 로 이관 — 모두 되돌리기 쉬운 항목
- 블로커 없음

---
*Phase: 76-side-datum*
*Completed: 2026-09-11*
