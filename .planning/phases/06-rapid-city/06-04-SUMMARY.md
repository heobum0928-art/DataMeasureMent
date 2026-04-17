---
phase: 06-rapid-city
plan: 04
type: summary
status: completed
date: 2026-04-17
---

# Plan 06-04 — UI 트리 재구성 + Measurement 단위 결과 테이블

## 변경 요약

- **Node.cs**: `ENodeType.Measurement` 추가, ImageSource case 추가 (chart 아이콘 재사용).
- **InspectionListViewModel.cs**:
  - `CreateSequenceNode` 재설계 — Sequence 노드는 `InspectionSequence.GetDisplayName()` 사용.
  - Datum 노드를 Sequence 직접 자식으로 추가 (Action과 형제, D-25).
  - FAI 노드 하위에 Measurement 노드 자동 생성 (D-24).
  - 헬퍼 추가: `AddDatumNode`, `AddMeasurementNode` (트리 in-place 갱신).
- **InspectionListView.xaml.cs**:
  - SelectionChanged에 `ENodeType.Measurement` case 추가 — PropertyGrid 자동 바인딩(SetParam).
  - Datum 노드 선택 시 CRUD 버튼 활성화.
  - `Btn_AddFAI_Click` 확장:
    - Sequence → Yes/No 다이얼로그(Shot 또는 Datum).
    - Datum → 형제 Datum 추가 (`InspectionSequence.AddDatum`).
    - FAI → Measurement 추가 (`MeasurementFactory.GetTypeNames` 안내 + 이름 입력).
    - Measurement → 형제 Measurement 추가.
  - `Btn_RemoveFAI_Click` 확장: Datum / Measurement 삭제 분기 + 확인 다이얼로그.
- **MeasurementResultRow.cs (신규)**: `MeasurementBase` 래핑 — FAIName / MeasurementName / TypeName / DatumRef / 공차 / 측정값 / 판정 + `Refresh()`.
- **InspectionViewModel.cs**: `FAIResults` → `MeasurementResults`로 교체. `OnFAISelected` / `OnActionSelected`가 `fai.Measurements` 순회로 행 생성.
- **MainView.xaml**: DataGrid 컬럼을 FAI / Measurement / Type / DatumRef / Nominal / Tol+ / Tol- / 측정값 / 판정으로 변경.
- **MainView.xaml.cs**: `MeasurementResults` 바인딩, `FindFAIByName` 헬퍼 추가, ROI 드로잉(Rect/Polygon)/Calibration 핸들러를 `MeasurementResultRow.FAIName` 기반으로 마이그레이션.
- **DatumMeasurement.csproj**: `MeasurementResultRow.cs` Compile Include.

## 빌드 결과

`msbuild Debug/x64` — 성공 (기존 경고 4건 그대로, 신규 에러 0건).

## 다음 단계

Task 3 (사람-검증 체크포인트): Phase 6 전체 UI 시각 검증 — 사용자 승인 후 Phase 6 완료 처리.
