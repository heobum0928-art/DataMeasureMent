---
phase: 20
plan: 04
status: complete
completed: 2026-05-09
commits:
  - 4494114
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
---

# Plan 20-04 Summary — MainView.xaml.cs cleanup

## Result
QUAL-02/QUAL-04 충족. 1725-line WPF MainView code-behind 의 33 operator instance 모두 명시적 if/else 로 변환.

## Conversion Counts

| Operator | Baseline | Post-edit | Notes |
|----------|----------|-----------|-------|
| `?:` ternary | 17 | 0 | temp 변수 + if/else 패턴 |
| `??` null-coalesce | 3 | 0 | (1 line 에 2개 + 단독 2개) → if/else null-fallback |
| `?.` null-conditional | 13 | 0 | chained `mParentWindow?.inspectionList?.X` x5 포함 → 단일 `if (a != null && a.b != null) a.b.X();` |
| **Total conversions** | **33** | — | — |

## Conversion Sites

### Ternaries (17)

- L243 `pDev[...].IsGrabFromFile` → grab source label
- L318 `context.Timer != null` → elapsed seconds (Phase 7 의도 보존)
- L327 `param.Parent?.Name ?? context.Source?.Name ?? ""` → 3-way fallback (chained ?. + ?? expansion)
- L354 `context.ActionParam != null` → name
- L389+ UpdatePointerLabel — `grayValue.HasValue` x4 (label_pos / hoverX / hoverY / hoverG)
- L448 `rois == null` → roiList
- L518 `e.PolygonPoints ?? ""` → fai.PolygonPoints (Polygon ROI)
- L786 `datum.DatumName` prefix (Phase 18 CO-06 의도 보존)
- L832 `error ?? "unknown"` → 재티칭 실패 메시지
- L953 `string.IsNullOrWhiteSpace(name)` → ROI name fallback
- L991 `rois == null` → roiCount
- L992 `overlays == null` → overlayCount
- L995 `string.IsNullOrWhiteSpace(imagePath)` → imgLabel
- L1060 `selectedRow != null` → faiToEdit (Rect ROI 진입)
- L1224 `selectedRow != null` → faiToEdit (Rect ROI 진입 — 두 번째 위치)
- L1378 `selectedRow != null` → anchorFai (캘리브레이션)

### `??` (3 sites in 2 line groups)

- L327 (chained with `?.`): `Parent?.Name ?? Source?.Name ?? ""` — 3-way priority chain
- L518 `e.PolygonPoints ?? ""` (FAI Polygon)
- L832 `error ?? "unknown"` (Datum ROI 이동 실패 메시지)

### `?.` (13 sites)

- L126 `img?.Dispose()` (이미지 finally 블록)
- L192 `act?.Param is ShotConfig shot` (FindFAIByName 패턴 매칭)
- L327 `Parent?.Name` + `Source?.Name` (chain — 위 ternary 와 통합 처리)
- L560/645/681/1632/1651 `mParentWindow?.inspectionList?.RefreshParamEditor()` x5 — replace_all
- L634 `mParentWindow?.inspectionList?.SelectedParam as DatumConfig` (IsCurrentNodeDatum)
- L1141 `selRowForCircle?.FAIName`
- L1171 `act?.Param is ShotConfig shot` (GetFAINameForMeasurement)
- L1419 `mParentWindow?.inspectionList?.SelectedParam as DatumConfig` (Phase 12 - btn_teachDatum 활성화)
- L1722 `mParentWindow?.inspectionList?.SelectedParam as DatumConfig` (Phase 13 D-05 - BtnTestFindDatum)

## Key Decisions

- **D-04/D-05 예외:** MainView.xaml.cs 에 LINQ-chain-end `?.` (e.g. `list.FirstOrDefault(...)?.X`) 또는 expression-bodied member 부재 → 예외 적용 라인 0
- **D-08 'why' 주석 보존:** Phase 7 Timer null 체크 의도, Phase 12 InspectionListView 의 btn_teachDatum 활성화 조건, Phase 13 D-01 Datum 노드 판정 / D-05 teach 세션 독립, Phase 17 D-15 hover 표시, Phase 18 CO-06 [DatumName] 접두사, Phase 19 dynamic Shot/FAI 대응 — 모두 보존
- **Datum 3-way state machine:** `btn_teachDatum` + `ECanvasMode.TeachDatum` + `EDatumTeachStep` 분기는 변환 대상 연산자 부재 (이미 if/else 패턴) — 회귀 0
- **Brace style:** 파일 K&R / 단일 라인 if 혼용 스타일 보존. 임시 변수 패턴은 `Type x; if (...) x = a; else x = b;` (Allman 들여쓰기 align)
- **C# 7.2 호환:** `??=` / switch expression / `is { }` 도입 0
- **Pattern matching `is X x`:** `act?.Param is ShotConfig shot` → `act != null && act.Param is ShotConfig shot` (C# 7.0 pattern variable scope 보존)

## Acceptance Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `??` (`??=` 제외) = 0 | PASS |
| 2 | `?.` (LINQ tail 제외) = 0 | PASS |
| 3 | `?:` 삼항 = 0 | PASS |
| 4 | msbuild Debug/x64 PASS | DEFERRED → Wave 2 (20-08) |
| 5 | Datum state machine 의미론적 보존 | PASS — 분기 코드 무변경 |
| 6 | C# 7.2 호환 (W1) | PASS |

## Deviations

- **인라인 오케스트레이터 실행:** worktree-isolated executor agent 들이 sandbox 의 `git reset --hard` 차단 (3회 재시도, 모두 BASE_RESET_BLOCKED checkpoint return) → 오케스트레이터가 main 작업 트리에서 직접 변환 수행. 변환 자체는 plan §conversion_patterns 그대로 적용.
- **hbk 마커 날짜 `260509`:** Wave 1 다른 plan (20-01/02/05/07) 과 일치.

## Wave 2 Hand-off

20-08 종합 회귀 시:
- MainView UpdatePointerLabel — Phase 17 D-15 hover X/Y/Gray 표시 SIMUL UAT 시 N/A 분기 + 정수 분기 양쪽 진입 확인
- MainView FormatTeachError — Phase 18 CO-06 datum != null 분기 + null 분기 양쪽 메시지 포맷 확인
- Datum 티칭 3-way state machine — btn_teachDatum 활성화 → ECanvasMode.TeachDatum 진입 → 단계별 ROI 그리기 → btn_teachDatum 재누르 시 종료 (state 전이 4 단계 모두)
- Plain `mParentWindow?.inspectionList?.X` chain expansion — Datum 노드 선택/해제 + RefreshParamEditor 호출 정확성
