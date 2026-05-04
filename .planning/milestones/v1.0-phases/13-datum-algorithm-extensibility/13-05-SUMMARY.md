---
phase: 13-datum-algorithm-extensibility
plan: "05"
subsystem: Datum/Visualization
tags: [datum, visualization, edge-points, line-extension, ref-coords, htuple-runtime, dispcross, phase-13]
status: complete
updated: "2026-04-26"
dependency_graph:
  requires: [13-04]
  provides:
    - Gap-C-DatumDetectedLineExtension
    - Gap-RawEdgePointDisplay
    - Gap-RefCoordsTextDisplay
  affects:
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
tech_stack:
  added: []
  patterns:
    - "5 ROI x 2 volatile HTuple 필드 (DetectedEdgeRows/Cols) — runtime 전용, ParamBase reflection 자동 무시 (Phase 4 D-11 패턴 연장)"
    - "EXTEND_PX = 10000.0 + DrawExtendedLine(unit-vector × EXTEND_PX 양방향 외삽) — HALCON DispLine 자동 클리핑 활용, 30K~50K 이미지에서도 안전"
    - "RenderRawEdgePoints(window, rows, cols, color) — HTuple batch DispCross (size=6, angle=0), null/length 0 가드"
    - "ROI 별 색상 팔레트: Line1=cyan / Line2=magenta / Circle=yellow / HorizA=green / HorizB=lime"
    - "label_datumRefCoords WPF Label + UpdateDatumRefCoordsLabel(DatumConfig) 3 호출 지점 (선택 변경 / 티칭 성공 / ROI 이동 후 재티칭)"
    - "_datumRoiCandidates 를 hasSelectedRoi 판정에 포함 (Datum.* prefix 인식) — Plan 13-03 잠복 결함 해소"
key_files:
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
decisions:
  - "검출 라인 외삽은 unit-vector × EXTEND_PX(=10000.0) 양방향 push 후 HALCON DispLine 자동 클리핑 — width/height 조회 불필요, divide-by-zero 가드 (lenSq < 1e-9 skip)"
  - "raw edge point 저장은 DatumConfig.HTuple [Browsable(false)] auto-property — ParamBase switch-case (Int32/Double/String/Boolean/Rect/Line/Circle/PropertyItem[]/ModelFinderViewModel) 가 HTuple 미지원이라 INI/PropertyGrid 영향 0 (Phase 4 D-11 / Phase 11 D-11 / Phase 12 D-10 패턴)"
  - "raw 점 마커는 HOperatorSet.DispCross batch (size 6, angle 0) — 수십~수백 점 일괄 렌더 효율 + 기존 RenderCircleDraft 빨간 십자 6 px 시각 일치"
  - "ROI 별 색상 hardcoded (Line1 cyan / Line2 magenta / Circle yellow / HorizA green / HorizB lime) — PropertyGrid 노출은 out-of-scope, 향후 충돌 시 점 색상만 시프트"
  - "Circle raw 에지점 wiring 은 DatumConfig 필드 + RenderRawEdgePoints 호출 지점만 준비; VisionAlgorithmService.TryFindCircle 가 raw row/col 미반환이라 실제 데이터는 빈 HTuple — 노란 점 시각화는 다음 phase 이월 (Carry-over)"
  - "label_datumRefCoords 회색 fallback 문구 'Datum 미설정' — 사용자가 'Datum 노드 선택만 했고 아직 티칭 안 함' 상태를 즉시 인지하도록 Visibility=Visible 유지 (label_drawHint 와 동일 영역, Foreground=#FF888888)"
  - "CircleTwoHorizontal 만 라벨에 CircleCenter/Radius 추가 (AlgorithmTypeEnum + CircleDetected_Radius>0 이중 가드) — TwoLineIntersect/VerticalTwoHorizontal 은 RefOrigin/Angle 만 표시"
  - "Plan 13-03 의 Edit/Delete 메뉴 활성화 잠복 결함은 13-05 UAT 중 발견되어 hotfix 136de8e 로 13-05 안에 흡수 — UpdateContextMenuState hasSelectedRoi 가 _datumRoiCandidates 도 OR-체크하도록 1 라인 확장"
  - "Datum ROI 의 실제 resize (드래그 핸들 동작) + 자동 재티칭 + DatumConfig write-back 은 신규 사용자 요구사항 — 13-06 (또는 14-XX) 신규 plan 으로 분리 (Carry-over)"
commits:
  - 01e37e3  # feat(phase-13-05): Datum visualization — extended detected lines + raw edge points + ref coords label (Tasks 1-4)
  - 136de8e  # fix(phase-13-05): ROI Edit/Delete regression — UpdateContextMenuState now checks _datumRoiCandidates (Plan 13-03 잠복 결함)
metrics:
  duration: ~3hr (포함 UAT + Edit/Delete hotfix)
  completed_date: "2026-04-26"
---

# Phase 13 Plan 05: Datum Visualization Summary

**검출 라인 이미지 가장자리 외삽(EXTEND_PX=10000) + 5 ROI 색상별 raw 에지점 DispCross 마커 + RefOrigin/Angle/CircleCenter/Radius 텍스트 라벨 — Phase 13 시각화 묶음 완료**

## Performance

- **Duration:** ~3hr (1 메인 commit + 1 hotfix + UAT 15 시나리오 + 별도 Edit/Delete 결함 식별)
- **Started:** 2026-04-26 (Plan 13-04 fa91525 직후)
- **Completed:** 2026-04-26
- **Tasks:** 4 implementation + 1 UAT checkpoint
- **Files modified:** 6 (5 plan-targeted + 1 hotfix)

## Accomplishments

- **Gap-C (Datum detected line 외삽):** RenderDatumOverlay LastTeachSucceeded 분기의 Line1/Line2 DispLine 호출이 unit-vector × 10000 px 양방향 외삽 후 DispLine 으로 교체 — 라인 방향/위치가 한눈에 들어옴
- **Gap-RawEdgePointDisplay:** DatumConfig 에 5 ROI × 2 = 10 신규 volatile HTuple 필드 추가 + DatumFindingService 가 검출 직후 raw row/col write-back + HalconDisplayService 가 ROI 별 색상 cross 마커로 일괄 렌더 — 검출 신뢰도/outlier 진단 가능
- **Gap-RefCoordsTextDisplay:** MainView 에 label_datumRefCoords WPF Label 추가 + UpdateDatumRefCoordsLabel 메서드 + 3 호출 지점 — RefOrigin/Angle (+ CircleCenter/Radius) 가 캔버스 옆 즉시 가시화
- **Plan 13-03 잠복 결함 흡수 (hotfix 136de8e):** Datum ROI 가 hasSelectedRoi 판정에 포함되도록 UpdateContextMenuState 1 라인 확장 — Edit/Delete 컨텍스트 메뉴 활성화 복구

## Task Commits

1. **Tasks 1-4 (DatumConfig 10 HTuple + DatumFindingService write-back + HalconDisplayService EXTEND_PX/DrawExtendedLine/RenderRawEdgePoints + MainView label/method/3 호출):** `01e37e3` (feat)
2. **Plan 13-03 Edit/Delete 잠복 결함 hotfix:** `136de8e` (fix)

## Files Changed

| File | Type | Change | Commit |
|------|------|--------|--------|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model | +28 lines (10 volatile HTuple props × 2 pair, Browsable(false), VIZ-01 주석) | `01e37e3` |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | service | +49 lines (TryFindLine 시그니처 +2 out HTuple, 5 ROI write-back, TryTeach + TryFindDatum 양 경로) | `01e37e3` |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | display | +64/-4 (EXTEND_PX 상수, DrawExtendedLine helper, RenderRawEdgePoints helper, RenderDatumOverlay 외삽 교체 + 5 ROI 호출) | `01e37e3` |
| `WPF_Example/UI/ContentItem/MainView.xaml` | xaml | +5 lines (label_datumRefCoords, label_drawHint 동일 영역, Visibility="Collapsed" 기본) | `01e37e3` |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | code-behind | +37 lines (UpdateDatumRefCoordsLabel + 3 호출 지점: Datum 노드 선택 / 티칭 성공 / ROI 이동 후 재티칭) | `01e37e3` |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | hotfix | +6/-1 (UpdateContextMenuState hasSelectedRoi OR-check _datumRoiCandidates) | `136de8e` |

## Gap Coverage

| Gap | 상태 | 구현 매핑 |
|-----|------|-----------|
| **Gap-C-DatumDetectedLineExtension** | COVERED | `HalconDisplayService.DrawExtendedLine(window, r1, c1, r2, c2)` + `private const double EXTEND_PX = 10000.0` + `RenderDatumOverlay` LastTeachSucceeded 분기에서 DispLine → DrawExtendedLine 2 회 교체 (Line1/Line2) — HALCON 자동 클리핑으로 30K~50K 이미지 안전 |
| **Gap-RawEdgePointDisplay** | COVERED (Circle 부분 carry-over) | `DatumConfig.{Line1,Line2,Circle,Horizontal_A,Horizontal_B}_DetectedEdge{Rows,Cols}` 10 HTuple 필드 + `DatumFindingService` 5 ROI write-back + `HalconDisplayService.RenderRawEdgePoints` × 5 호출 (DispCross batch, size 6, angle 0) — Circle 만 `VisionAlgorithmService.TryFindCircle` raw 미반환으로 빈 HTuple 유지 (다음 phase 이월) |
| **Gap-RefCoordsTextDisplay** | COVERED | `MainView.xaml` label_datumRefCoords + `MainView.xaml.cs UpdateDatumRefCoordsLabel(DatumConfig)` + 3 호출 지점 (InspectionListView SelectedParam Datum 분기 / InvokeTryTeachDatum 성공 / HandleDatumRoiMove 말미) — CircleTwoHorizontal 일 때 CircleCenter/Radius append |

## ROI 색상 팔레트 (raw edge points)

| ROI | 색상 | 알고리즘 적용 | 비고 |
|-----|------|---------------|------|
| Line1 | `cyan` | TwoLineIntersect / VerticalTwoHorizontal | 가시화 OK |
| Line2 | `magenta` | TwoLineIntersect | 가시화 OK |
| Circle | `yellow` | CircleTwoHorizontal | **VisionAlgorithmService.TryFindCircle 가 raw row/col 미반환 — 빈 HTuple, 다음 phase 이월 (Carry-over)** |
| Horizontal_A | `green` | CircleTwoHorizontal / VerticalTwoHorizontal | 가시화 OK |
| Horizontal_B | `lime` | CircleTwoHorizontal / VerticalTwoHorizontal | 가시화 OK |

검출 라인 색상(yellow Line1 / cyan Line2)과 점 색상(cyan Line1 / magenta Line2) 이 일부 겹치지만 점/선 형태 차이로 시각 구분 가능 — 향후 충돌 발생 시 점 색상만 시프트하기로 결정 (PropertyGrid 노출은 out-of-scope).

## EXTEND_PX Rationale

`HOperatorSet.DispLine` 은 화면 밖 좌표를 자동으로 클리핑한다. 따라서 정확한 이미지 width/height 조회 없이 unit-vector × `10000.0` px 양쪽 외삽 → HALCON 이 알아서 가장자리에서 자른다. 30K~50K 픽셀 이미지에서도 충분한 마진 확보.

```csharp
double dr = r2 - r1, dc = c2 - c1;
double lenSq = dr * dr + dc * dc;
if (lenSq < 1e-9) return;            // degenerate guard (divide-by-zero 방지)
double len = Math.Sqrt(lenSq);
double ur = dr / len, uc = dc / len;
HOperatorSet.DispLine(window,
    r1 - ur * EXTEND_PX, c1 - uc * EXTEND_PX,
    r2 + ur * EXTEND_PX, c2 + uc * EXTEND_PX);
```

## HTuple Volatile 필드 패턴 (Phase 4 D-11 연장)

```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public HTuple Line1_DetectedEdgeRows { get; set; }
```

- **ParamBase 직렬화 자동 무시:** `ParamBase` 의 INI write switch-case 는 `Int32 / Double / String / Boolean / Rect / Line / Circle / PropertyItem[] / ModelFinderViewModel` 만 처리 → `HTuple` 은 unknown branch 로 fall-through 되어 INI 키 자체가 생성되지 않음
- **PropertyGrid 미노출:** `[Browsable(false)]` 로 사용자 편집 차단 — runtime 전용 진단 데이터
- **INI 영향 0:** 기존 Phase 4/11/12 INI 레시피 그대로 로드/저장 가능 (legacy round-trip 회귀 0)
- **메모리 비용:** ROI 1개당 검출 점 수십~수백 개 × 8 bytes(double) × 2 (rows + cols) — 무시 가능

## Build Result

- **Command:** `msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal`
- **Exit code:** 0
- **신규 warning (수정 6 파일 기준):** 0
- **기존 warning (CS0162 unreachable / CS0219 unused local):** 변동 없음 (이전 phase 와 동일 카운트)

## UAT Outcome (15 시나리오)

| # | 카테고리 | 시나리오 | 결과 | 비고 |
|---|----------|----------|------|------|
| 1 | 라인 외삽 (Gap-C) | TwoLineIntersect 티칭 — yellow Line1 + cyan Line2 이미지 양 끝까지 | **PASS** | |
| 2 | 라인 외삽 | CircleTwoHorizontal 의 Line1Detected/Line2Detected slot 외삽 | **PASS** | |
| 3 | 라인 외삽 | VerticalTwoHorizontal 외삽 | **PASS** | |
| 4 | Raw 점 (Gap-Raw) | TwoLineIntersect — cyan(Line1) + magenta(Line2) 점 분포 | **PASS** | |
| 5 | Raw 점 | CircleTwoHorizontal — Circle 노란 점 / HorizA green / HorizB lime | **PASS (HorizA/B)**, Circle은 빈 HTuple → 점 미표시 (carry-over) | |
| 6 | Raw 점 | VerticalTwoHorizontal — Line1(수직) cyan + HorizA green + HorizB lime | **PASS** | |
| 7 | Raw 점 | 13-04 EdgeThreshold 증가 → cyan 점 개수 감소 시각 확인 | **PASS** | per-ROI 파라미터와 raw 점 통합 검증 |
| 8 | Ref 좌표 (Gap-RefCoords) | Datum 노드 선택 → 라벨 표시 (미설정/TwoLineIntersect/CircleTwoHorizontal) | **PASS** | |
| 9 | Ref 좌표 | ROI 드래그 이동 → 재티칭 후 라벨 즉시 갱신 | **PASS** | |
| 10 | Ref 좌표 | Datum→FAI 노드 선택 → 라벨 Visibility=Collapsed | **PASS** | |
| 11 | 회귀 (Phase 13-01) | 방향 정합성 게이트 정상 발동 | **PASS** | |
| 12 | 회귀 (Phase 13-02) | btn_testFindDatum 정상 | **PASS** | |
| 13 | 회귀 (Phase 13-03) | ROI 이동/삭제 정상 + 라벨 갱신 | **PASS (이동/삭제)**, **Edit 메뉴 활성화 = hotfix 136de8e 후 PASS**, **Datum ROI 실제 resize 동작 = DEFERRED to next phase** | hotfix + carry-over 별도 trace |
| 14 | 회귀 (Phase 13-04) | per-ROI 파라미터 정상 동작 | **PASS** | |
| 15 | 회귀 (FAI 무관) | FAI/Measurement 흐름 회귀 0 | **PASS** | |

**전체 결과:** 15/15 APPROVED (Test 5 Circle 노란 점 미시현 + Test 13 Datum ROI resize 는 carry-over 로 분리, hotfix 136de8e 로 Edit/Delete 메뉴 활성화 복구 완료)

## Phase 13 누적 통합 (5 plan 완료)

| Plan | 범위 | 핵심 산출물 |
|------|------|-------------|
| 13-01 | 방향 정합성 게이트 (Req 5d) | DatumFindingService.ValidateHorizontalVerticalAngles + CircleTwoHorizontal/VerticalTwoHorizontal 검증 게이트 |
| 13-02 | btn_testFindDatum (Gap-4) | 현재 이미지 / LoadImage 분기 + RenderDatumFindResult 오버레이 + label_testFindResult |
| 13-03 | ROI 이동/삭제 (Gap-1 + Gap-A) | RoiMoveCompleted Datum 분기 + ApplyDatumRoiDelta + 자동 InvokeTryTeachDatum + ContextMenu Delete |
| 13-04 | per-ROI 파라미터 (per-ROI) | DatumConfig 5×6=30 신규 필드 + EnsurePerRoiDefaults + TryFindLine/TryExtractEdgePoints 시그니처 확장 + strip-loop MeasurePos |
| **13-05** | **시각화 (Gap-C + Gap-RawEdgePoints + Gap-RefCoords)** | **EXTEND_PX 외삽 + 10 HTuple 필드 + DispCross batch + label_datumRefCoords + Edit/Delete hotfix** |

Phase 13 success criteria (#1~#6) 모두 충족 — Strategy 패턴 추상화는 Phase 12 switch-디스패치로 이미 달성, 리팩터는 Deferred 로 합의된 대로.

## Carry-over to Next Phase (정직 기록)

1. **Datum ROI Edit (resize 핸들 동작 + 자동 재티칭)** — 13-05 UAT Test 13 중 사용자 신규 요구사항 식별
   - **요구:** edit 모드의 코너 작은 사각형으로 Datum ROI(Line1/Line2 Rect, Circle) 도 리사이즈하고 자동 재티칭
   - **재사용 자산:** Plan 13-03 의 `BuildDatumRectCandidate` 가 Datum Line ROI 를 `RoiShape.Rect`, Circle ROI 를 `RoiShape.Circle` 로 표현 → 기존 `GetEditHandles` / `ApplyResizeToTarget` Rect/Circle 분기 그대로 재사용 가능
   - **예상 변경:** `RenderEditHandles` + `MouseDown` hit-test 의 `_rois.FirstOrDefault` 검색을 `_datumRoiCandidates` 로 확장 (~2 라인) + Resize 완료 후 `DatumConfig` write-back (`HandleDatumRoiMove` 거울) + 자동 재티칭
   - **결정:** **신규 plan (13-06 또는 14-XX) 으로 분리** — 13-05 scope 외, 별도 사용자 합의

2. **Circle raw edge points 시각화** — VisionAlgorithmService.TryFindCircle 가 raw row/col HTuple 미반환
   - **현재:** `DatumConfig.Circle_DetectedEdgeRows/Cols` 필드 + RenderRawEdgePoints 호출 지점은 준비, 실제 데이터는 빈 HTuple
   - **필요 작업:** `VisionAlgorithmService.TryFindCircle` 시그니처 확장 (out HTuple edgeRows / edgeCols) + 호출부 write-back
   - **결정:** Circle 검출 알고리즘 측 별도 phase 이월 (Datum service 측 wiring 은 완료 상태)

## Self-Check

- [x] DatumConfig 에 10 신규 volatile HTuple 필드 (5 ROI × 2) + Browsable(false) 모두 적용
- [x] DatumFindingService.TryFindLine 시그니처 +2 out (HTuple)
- [x] DatumFindingService 5 ROI write-back (Line1/Line2/Circle/HorizA/HorizB) — TryTeach + TryFindDatum 양 경로
- [x] HalconDisplayService 에 EXTEND_PX + DrawExtendedLine + RenderRawEdgePoints
- [x] RenderDatumOverlay 의 Line1Detected/Line2Detected DispLine → DrawExtendedLine 교체
- [x] RenderDatumOverlay 의 LastTeachSucceeded 분기 말미에 5 ROI RenderRawEdgePoints 호출 (cyan/magenta/yellow/green/lime)
- [x] MainView.xaml 에 label_datumRefCoords 추가 (Visibility="Collapsed" 기본)
- [x] MainView.xaml.cs 에 UpdateDatumRefCoordsLabel + 3 호출 지점 (Datum 노드 선택 / 티칭 성공 / ROI 이동 후 재티칭)
- [x] CircleTwoHorizontal 일 때 라벨에 CircleCenter / Radius 추가
- [x] msbuild Debug/x64 exit 0 + 신규 warning 0
- [x] 메인 commit 1 + hotfix 1 (136de8e Edit/Delete 활성화)
- [x] Task 5 SIMUL_MODE UAT 15 시나리오 APPROVED (Edit 메뉴 활성화 = hotfix 후 PASS, Datum ROI resize = DEFERRED, Circle raw 점 = DEFERRED)
- [x] Plan 13-03 잠복 결함 (Edit/Delete 메뉴 비활성화) 13-05 안에 흡수
- [x] Carry-over 2 항목 명시 (Datum ROI resize / Circle raw 점)

## Self-Check: PASSED

- 6 개 수정 파일 모두 git 에 존재 (01e37e3 + 136de8e)
- 메인 commit 01e37e3 = 5 파일 / +179 lines / -4 lines 확인
- Hotfix commit 136de8e = MainResultViewerControl.xaml.cs / +6 / -1 확인
- 신규 warning 0 (msbuild Debug/x64 exit 0)

## Next Phase Readiness

- **Phase 13 (datum-algorithm-extensibility) 완료 (5/5 plan):** 방향 게이트 / btn_testFindDatum / ROI 이동·삭제 / per-ROI 파라미터 / 시각화 모두 close
- **다음 권장 1 — verifier 실행:** `/gsd-verify-work 13` — Phase 13 종료 시 누적 회귀 + truth coverage 확인
- **다음 권장 2 — 신규 phase spec:** `/gsd-spec-phase 14` (또는 `/gsd:plan-phase` 신규) — Datum ROI Edit (resize + 자동 재티칭) 신규 요구사항 정식 plan 화

---
*Phase: 13-datum-algorithm-extensibility*
*Plan: 05*
*Completed: 2026-04-26*
