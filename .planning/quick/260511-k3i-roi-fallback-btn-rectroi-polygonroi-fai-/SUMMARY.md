---
quick_id: 260511-k3i
slug: roi-fallback-btn-rectroi-polygonroi-fai
date: 2026-05-11
status: complete
type: quick
commits:
  - 92f8c73
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
uat_status: PASS (사용자 4 시나리오 2026-05-11)
---

## What changed

`WPF_Example/UI/ContentItem/MainView.xaml.cs` 의 두 ROI 진입 핸들러를 트리 선택 우선 + dataGrid fallback 으로 재구성.

- **`RectRoiButton_Click` (L1060)** — L1066-1080 의 FAI 해석 블록 교체. 1차 `mParentWindow.inspectionList.SelectedParam as FAIConfig` → 2차 `dataGrid_faiResults.SelectedItem` → 둘 다 null 인 경우에만 "FAI를 먼저 선택하세요." 모달.
- **`PolygonRoiButton_Click` (L1233)** — L1239-1253 동일 패턴. 모달 타이틀만 `"Polygon ROI"` 유지. `_polygonPoints.Clear()` 및 이후 hint/이벤트 구독 라인 미변경.

## Why

신규 FAI 추가 직후에는 Measurement 가 0개라서 `dataGrid_faiResults` 가 비어 있다. 기존 핸들러는 `dataGrid_faiResults.SelectedItem` 만 확인했기 때문에, 트리(`InspectionListView`)에서 해당 FAI 가 명확히 선택돼 있어도 ROI 드로잉 진입을 거부했다. 트리 선택을 1차 소스로 채택하여 신규 FAI 케이스를 커버하고, dataGrid 경로는 fallback 으로 보존하여 기존 행 클릭 워크플로우의 회귀를 0 으로 유지.

## Verification

### Build (Claude-verifiable)
- msbuild `Debug/x64` PASS (VS 2022 Community MSBuild).
- 0 Error.
- 3 Warning (MSB3884 ruleset 파일 누락, CS0162 `VirtualCamera.cs:266`, CS0219 `VisionAlgorithmService.cs:64`) — 모두 baseline (Phase 22 와 동일), 변경 파일과 무관.

### Static (Claude-verifiable)
- `//260511 hbk` 마커 4건 (Rect L1066/L1071 + Polygon L1239/L1244) — 플랜 기대치 ≥4 충족.
- `mParentWindow.inspectionList.SelectedParam as FAIConfig` 정확히 2회 (Rect L1069 + Polygon L1242) — 플랜 기대치 일치.
- `"Polygon ROI"` 타이틀 정확히 1회 유지 (L1249) — Rect/Polygon 모달 구분 보존.
- `FindSelectedCircleMeasurement()` 호출 (L1136) + 메서드 정의 (L1191) 미변경 — Circle 경로 회귀 0.
- `CircleRoiButton_Click` / `CommitRectRoi` / `CompletePolygon` / `CommitCircleRoi` / `FindFAIByName` 본문 미변경.

### Runtime UAT (사용자 검증 — 2026-05-11 PASS)
| 시나리오 | 절차 | 기대 | 결과 |
|----------|------|------|------|
| A | 새 FAI 추가 (Measurement 0) → 트리에서 해당 FAI 선택 → `btn_rectRoi` 클릭 → 드래그 | 드로잉 모드 진입, 모달 안 뜸, ROI 커밋 성공 | PASS |
| B | 새 FAI 추가 → 트리에서 해당 FAI 선택 → `btn_polygonRoi` 클릭 → 3점 + 우클릭 | Polygon 드로잉 진입, 커밋 성공 | PASS |
| C | 기존 FAI (Measurement 1+) → 트리 미선택 + dataGrid 행 선택 → ROI 버튼 클릭 | 기존대로 동작 (회귀 없음) | PASS |
| D | 트리 + dataGrid 둘 다 비선택 → ROI 버튼 클릭 | "FAI를 먼저 선택하세요." 모달 정상 노출 | PASS |

## Constraints honored

- C# 7.2 (no switch expressions, no `is not`, no nullable refs, no target-typed new).
- K&R brace style (opening brace 같은 줄) — 파일 기존 스타일 일치.
- 명시적 null 체크 `if (mParentWindow != null && mParentWindow.inspectionList != null)` — L635 / L1421 패턴 일치 (`?.` 체인 미사용).
- 모든 변경 라인에 `//260511 hbk` 마커 (feedback_comment_convention.md 준수).
- 단일 atomic commit `92f8c73`.

## Out of scope (carry-over)

- ROI Edit/Move 모드 재설계 — `project_roi_edit_mode_deferred.md` 별도 carry-over.
- CO-22-01 (Datum↔FAI PropertyGrid 즉시 전환) — STATE.md Pending Todos 등록됨.
