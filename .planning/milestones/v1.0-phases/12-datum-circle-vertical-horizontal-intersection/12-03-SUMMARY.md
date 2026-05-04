---
phase: 12
plan: 03
subsystem: datum-teaching-ui-3way
tags: [datum, ui, canvas-mode, state-machine, overlay, halcon, phase-12]
requires:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (Plan 01 — AlgorithmTypeEnum + Circle/Horizontal ROI fields)
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs (Plan 02 — 3-way TryTeachDatum dispatch)
  - WPF_Example/UI/ContentItem/MainView.xaml.cs (Phase 11 — halconViewer Circle/Rect drawing infrastructure)
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (Phase 11 — Datum node Grab/LoadImage enable)
provides:
  - MainView.btn_teachDatum ToggleButton (3-way Datum teach entry)
  - ECanvasMode.TeachDatum + EDatumTeachStep private enum state machine
  - GetFirstStep / GetNextStep switch by EDatumAlgorithm
  - HalconViewer_DatumRectCompleted / HalconViewer_DatumCircleCompleted step-dispatch + write-back to DatumConfig
  - InvokeTryTeachDatum auto-call on Done with success/fail label feedback
  - InspectionListView.RefreshParamEditor() — 외부 PropertyGrid 재바인딩 트리거 (UAT Gap-3 fix)
  - HalconDisplayService.RenderDatumOverlay algorithm-aware branches (Line2 TwoLineIntersect-only, CircleROI CircleTwoHorizontal-only, Horizontal A/B non-TwoLineIntersect)
  - HalconDisplayService.DrawRoiLabel / DrawRoiLabelAt ROI 식별 라벨 헬퍼 (UAT Gap-2 fix)
affects:
  - WPF_Example/UI/ContentItem/MainView.xaml (btn_teachDatum XAML)
  - WPF_Example/UI/ContentItem/MainView.xaml.cs (state machine + handlers)
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (Datum 노드 활성화 + RefreshParamEditor)
  - WPF_Example/Halcon/Display/HalconDisplayService.cs (overlay 분기 + 라벨)
tech-stack:
  added: []
  patterns:
    - "ECanvasMode 상태 머신 확장 (Calibration/CircleRoi 패턴 재사용)"
    - "CircleRoiButton_Click + CircleDrawingCompleted 이벤트 시그널링 재사용"
    - "ParamBase.RaisePropertyChanged('') + PropertyGrid.SelectedObject null→재할당 이중 신호 패턴 (자동 속성 INotifyPropertyChanged 미발동 회피)"
    - "Halcon WriteString + SetTposition 라벨 렌더 (Rectangle2 로컬 (-L1,-L2) 에 phi 회전 적용 후 외곽 상단 22px 바깥 배치)"
key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
decisions:
  - "btn_teachDatum 은 Datum 노드 선택 시에만 활성화 (InspectionListView L249/251/298); 선택 해제 시 btn_circleRoi 와 함께 비활성"
  - "EDatumTeachStep 은 MainView private enum — Phase 13 Strategy 리팩터 시 DatumAlgorithmBase.GetROISteps() 가변 배열로 교체 예정"
  - "TeachDatum 모드에서 Rect 드로잉은 phi=0 고정 (수직/수평 ROI 모두 축 정렬) — Phase 13 각도 가변 ROI 시 GetROISteps 에서 phi 지정 확장"
  - "Line1 재사용 (VerticalTwoHorizontal 의 수직 ROI 는 Line1_* 필드에 기록) — D-07/D-12 시맨틱스 유지, DatumConfig 필드 증설 없음"
  - "Done step 이 최종 step 이며 InvokeTryTeachDatum 직접 호출 — 별도 'Teach 실행' 버튼 없음 (D-04)"
  - "UAT Gap-2 (ROI 구분) — yellow ROI 라벨 (L1/L2/Circle/H-A/H-B/Vert) 을 RenderDatumOverlay 내부에 직접 추가. 별도 overlay 리스트/팔레트 변경 없이 DispRectangle2/DispCircle 호출 직후 WriteString 으로 붙임 → 기존 cyan/blue/magenta 팔레트 보존"
  - "UAT Gap-3 (write-back 미반영) — DatumConfig 의 자동 속성은 INotifyPropertyChanged 미발동. PropertyGrid XAML binding 이 한 번 평가 후 캐시 → 코드에서 필드 대입해도 재렌더 안 됨. RaisePropertyChanged(\"\") + InspectionListView.RefreshParamEditor() (SelectedObject null→재할당) 이중 신호로 강제 재바인딩. SetDatumOverlay 도 즉시 재호출하여 캔버스도 갱신"
  - "Gap-1 (TeachDatum 모드에서 그린 ROI 를 이후 드래그 이동) / Gap-4 (런타임 TryFindDatum 테스트 UI) 는 Phase 13 이월 — 본 Plan 범위 초과"
metrics:
  duration_minutes: 20
  completed_date: 2026-04-24
  tasks_total: 5
  tasks_completed: 5
  commits:
    - e3287c6 (Task 1 — btn_teachDatum XAML + EDatumTeachStep scaffolding)
    - f0c7668 (Task 2 — state machine + InspectionListView enable)
    - 3fe1119 (Task 3 — RenderDatumOverlay algorithm-aware branches)
    - 781e4be (Task 4 — UAT Gap-2/Gap-3 fix: ROI 라벨 + PropertyGrid 재바인딩)
  build: "msbuild Debug/x64 exit 0 — 신규 warning 0 (MainView.xaml.cs / HalconDisplayService.cs / InspectionListView.xaml.cs 대상)"
---

# Phase 12 Plan 03: Datum Teaching UI 3-way State Machine + UAT Gap Fix Summary

**One-liner:** `btn_teachDatum` 토글 + `ECanvasMode.TeachDatum` + `EDatumTeachStep` 상태머신 3-way (TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal) 로 Datum 티칭 워크플로우를 완성하고, `HalconDisplayService.RenderDatumOverlay` 를 알고리즘별 분기로 확장. UAT 2건(Gap-2 ROI 라벨 / Gap-3 PropertyGrid write-back 재바인딩) fix 적용, Gap-1/Gap-4 는 Phase 13 이월.

## Files Changed

| File | Change | Role |
| ---- | ------ | ---- |
| `WPF_Example/UI/ContentItem/MainView.xaml` | modified | `btn_teachDatum` ToggleButton 추가 (btn_circleRoi 옆, `RoiToggleButtonStyle` 재사용, IsEnabled=False 기본) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | modified (+~200 lines) | ECanvasMode.TeachDatum + EDatumTeachStep private enum + _datumTeachStep/_editingDatum state + TeachDatumButton_Click + GetFirstStep/GetNextStep switch + StartDatumTeachStep + HalconViewer_DatumRectCompleted + HalconViewer_DatumCircleCompleted + AdvanceDatumTeachStep + InvokeTryTeachDatum + ExitCanvasMode 확장; UAT Gap-3 write-back 재바인딩 호출 추가 |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | modified | Datum 노드 선택 시 btn_teachDatum.IsEnabled=true; 선택 해제 시 false 로 리셋; UAT Gap-3 RefreshParamEditor() public 헬퍼 추가 |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | modified | RenderDatumOverlay 에 AlgorithmType 분기 (Line2 TwoLineIntersect-only, CircleROI CircleTwoHorizontal-only, Horizontal A/B non-TwoLineIntersect 공용) + LastTeachSucceeded 하 Circle 검출 원 렌더 + UAT Gap-2 DrawRoiLabel/DrawRoiLabelAt 헬퍼 + 각 ROI 라벨 WriteString |

## Tasks Completed

| # | Task | Commit | Evidence |
| - | ---- | ------ | -------- |
| 1 | btn_teachDatum XAML + EDatumTeachStep scaffolding | e3287c6 | MainView.xaml btn_teachDatum / MainView.xaml.cs ECanvasMode.TeachDatum + EDatumTeachStep + state fields |
| 2 | State machine handlers + InspectionListView enable | f0c7668 | TeachDatumButton_Click + GetFirstStep/GetNextStep + StartDatumTeachStep + HalconViewer_DatumRectCompleted + HalconViewer_DatumCircleCompleted + InvokeTryTeachDatum + InspectionListView Datum 분기 btn_teachDatum.IsEnabled=true |
| 3 | RenderDatumOverlay algorithm-aware branches | 3fe1119 | HalconDisplayService.RenderDatumOverlay 분기 (Line2 gate, CircleROI, Horizontal A/B) + LastTeachSucceeded 하 Circle 검출 원 |
| 4 | UAT Gap-2/Gap-3 fix | 781e4be | DrawRoiLabel/DrawRoiLabelAt 헬퍼 + 각 ROI yellow 라벨; InspectionListView.RefreshParamEditor + MainView Datum 핸들러 재바인딩 |
| 5 | 마감 (SUMMARY + STATE + ROADMAP) | (최종 docs 커밋) | 본 파일 + STATE.md + ROADMAP.md 업데이트 |

## UAT Outcome

**사용자 UAT 결과 (2026-04-24):**

| # | 검증 항목 | 결과 | 조치 |
| - | --------- | ---- | ---- |
| Flow 1 | TwoLineIntersect 회귀 | PASS | 정상 동작 유지 (사용자 지적 없음) |
| Flow 4 | INI 하위호환 (기존 Phase 4/11 레시피 로드) | PASS | AlgorithmType 미존재 → TwoLineIntersect 폴백 확인 |
| Flow 2/3 | CircleTwoHorizontal / VerticalTwoHorizontal 티칭 | **FAIL → FIXED** | Gap-2 + Gap-3 fix 후 재검증 권장 |
| Gap-1 | ROI 드로잉 후 이동/편집 (Edit 모드) | **DEFERRED → Phase 13** | 본 Plan 범위 초과, Quick 260423-o53 FAI-only RoiMoveCompleted 경로를 Datum 경로로 확장하는 별도 작업 필요 |
| Gap-4 | 런타임 TryFindDatum 테스트 실행 UI | **DEFERRED → Phase 13** | SPEC Out-of-scope (런타임 재검출) — Phase 12 는 teaching-only |

## UAT Gap Fixes (이번 Plan 범위 내)

### Gap-2 — 수직/수평 ROI 시각 구분 불가 → yellow 라벨 추가

**증상:** Teach 단계 진행 중 / 완료 후 모든 Datum ROI 가 동일 cyan 직사각형으로 렌더되어 사용자가 수직/수평/라인 구분 불가.

**수정:**
- `HalconDisplayService.RenderDatumOverlay` 내 각 ROI `DispRectangle2`/`DispCircle` 호출 직후에 `DrawRoiLabel(...)` / `DrawRoiLabelAt(...)` 호출 추가.
- 라벨 규칙:
  - `TwoLineIntersect` → `"L1"`, `"L2"`
  - `CircleTwoHorizontal` → `"Circle"`, `"H-A"`, `"H-B"`
  - `VerticalTwoHorizontal` → `"Vert"`, `"H-A"`, `"H-B"`
- 색상 `"yellow"` (cyan/blue ROI 와 대비), 기존 `EnsureFontInitialized` + `SetTposition` + `WriteString` 호출 체인 재사용.
- `DrawRoiLabel` 은 Rectangle2 로컬 `(-L1, -L2)` 를 `phi` 로 회전 후 외곽 상단 22px 바깥에 배치 — `phi=0` 일 때 `(row-L1-22, col-L2)`. `DrawRoiLabelAt` 은 Circle 용(중심 위쪽 외곽 외부).
- 기존 cyan/blue/magenta 팔레트는 보존 (UAT Warning 5 스코프 가드).

**파일:** `WPF_Example/Halcon/Display/HalconDisplayService.cs` (L363-463 RenderDatumOverlay 분기 내 + L510-540 신규 헬퍼)

### Gap-3 — DatumConfig write-back 후 PropertyGrid 미갱신 (재시작 후 ROI 복원 실패 가능성)

**증상:** ROI 드래그 완료 이벤트에서 `_editingDatum.Line1_Row = centerRow` 등 필드에 기록해도 PropertyGrid 에 좌표가 표시되지 않음. 사용자가 "Save 가 안 된 줄" 착각하여 저장 생략 → 재시작 후 복원 실패.

**원인:**
- `DatumConfig` 의 ROI 필드는 C# 자동 속성(`public double X { get; set; } = 0;`)으로 INotifyPropertyChanged 미발동.
- `InspectionListView.xaml` 의 `ParamEditor.SelectedObject="{Binding SelectedItem.Param, ...}"` 바인딩은 소스 오브젝트가 변경 알림을 발동해야 재평가.
- 코드에서 필드만 대입하면 WPF Binding 이 재렌더 트리거 없음.

**수정 (이중 신호):**
- `ParamBase` 에 이미 존재하는 `RaisePropertyChanged(name)` 을 `""` 으로 호출 → 모든 프로퍼티 이름에 대해 변경 알림 강제 (PropertyGrid 가 INotifyPropertyChanged 리스너를 가지고 있는 경우 대응).
- `InspectionListView.RefreshParamEditor()` 신규 public 헬퍼 추가 — `ParamEditor.SelectedObject = null; ParamEditor.SelectedObject = current;` 로 PropertyGrid 강제 재생성 (리스너 유무 무관, 확실한 재렌더).
- 추가로 `halconViewer.SetDatumOverlay(_editingDatum, true)` 도 즉시 호출하여 캔버스 오버레이도 방금 그린 ROI 좌표로 갱신.

**호출 위치:**
- `MainView.HalconViewer_DatumRectCompleted` — switch 케이스 직후 (5가지 step 공통)
- `MainView.HalconViewer_DatumCircleCompleted` — CircleROI_* 대입 직후

**파일:** `WPF_Example/UI/ContentItem/MainView.xaml.cs` (L1138-1162 근처) + `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (L23-31 RefreshParamEditor)

## Deferred Issues (Phase 13 이월)

| Gap | 증상 | 이월 사유 |
| --- | ---- | -------- |
| Gap-1 | TeachDatum 모드에서 한 번 그린 ROI 를 이후 드래그 이동/크기 조정으로 편집 불가 (새로 그려야 덮어써짐) | 기존 Quick 260423-o53 가 FAIConfig 대상 `HalconViewer_RoiMoveCompleted` 를 구현했으나 DatumConfig 대상 경로 확장은 안 됨. Edit 모드 재설계 + Polygon 이동 + Rect 회귀 테스트와 묶어서 별도 Quick 또는 Phase 13 수행. 본 Plan 스코프 초과. |
| Gap-4 | 런타임 TryFindDatum (Grab 시점 Datum 재검출) 테스트 실행 UI | SPEC.md Out-of-scope — "런타임 TryFind 재검출" 명시적 제외. Phase 13 Strategy 리팩터 후 DatumAlgorithmBase.TryFind() 경로로 구현 예정. |
| Req 5d | CircleTwoHorizontal 방향 정합성 위반 (원 중심이 수평선 위/아래 기대 위반) 검출 | Phase 12 CONTEXT D-17 — 운용 정책 미확립. `// TODO: Phase 13 — 방향 정합성 검사` 리터럴 코멘트로 이월 마커 유지. |

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
→ DatumMeasurement -> bin/x64/Debug/DatumMeasurement.exe (exit 0)
```

Pre-existing warnings only:
- `VirtualCamera.cs(266,13)` CS0162
- `VisionAlgorithmService.cs(64,22)` CS0219
- `MSB3884` MinimumRecommendedRules.ruleset missing

**신규 warning 0** — MainView.xaml.cs / HalconDisplayService.cs / InspectionListView.xaml.cs 대상 zero new warnings 확인.

## Commits

- `e3287c6` — feat(phase-12-03): add btn_teachDatum + EDatumTeachStep scaffolding (Task 1)
- `f0c7668` — feat(phase-12-03): implement Datum teach state machine + InspectionListView enable (Task 2)
- `3fe1119` — feat(phase-12-03): RenderDatumOverlay algorithm-aware branches (Task 3)
- `781e4be` — fix(phase-12-03): Datum ROI write-back + overlay 라벨 (UAT Gap-2/Gap-3)
- (final docs commit) — docs(phase-12-03): complete plan — SUMMARY + STATE + ROADMAP

## Self-Check: PASSED

- [x] `WPF_Example/UI/ContentItem/MainView.xaml.cs` — MODIFIED (Datum state machine present)
- [x] `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — MODIFIED (RefreshParamEditor + Datum enable)
- [x] `WPF_Example/Halcon/Display/HalconDisplayService.cs` — MODIFIED (algorithm-aware branches + label helpers)
- [x] Commit `e3287c6` (Task 1) — FOUND in git log
- [x] Commit `f0c7668` (Task 2) — FOUND in git log
- [x] Commit `3fe1119` (Task 3) — FOUND in git log
- [x] Commit `781e4be` (Task 4 UAT fix) — FOUND in git log
- [x] Build Debug/x64 exit 0 — VERIFIED (DatumMeasurement.exe produced)
- [x] 신규 warning 0 — VERIFIED
- [x] `//260423 hbk` / `//260424 hbk Phase 12 Gap-X` comment convention — 신규 라인 준수
- [x] Deferred Gap-1/Gap-4/Req 5d 명시 — 위 "Deferred Issues" 섹션
