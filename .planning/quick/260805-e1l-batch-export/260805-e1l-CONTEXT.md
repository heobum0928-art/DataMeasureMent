# Quick Task 260805-e1l: 일괄검사(Batch) export에 회차별 상세+캡쳐이미지 추가 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning

<domain>
## Task Boundary

`WPF_Example\UI\ControlItem\InspectionListView.xaml.cs:605-629` 의 `Btn_batchExport_Click`(일괄검사 export 버튼)이 지금 `ReringProject.Export.RepeatExcelExportService.Export(_batchAccumulated, recipeName, dlg.FileName)` 를 호출하는데, 이 함수는 `WPF_Example\UI\Reviewer\ReviewerWindow.xaml.cs:433` 의 반복검사(Gage R&R) export 버튼과 **완전히 동일한 함수**다. 즉 지금은 "일괄검사"와 "반복검사"가 코드/포맷을 100% 공유한다.

이 작업은 이 둘을 분리한다: 반복검사(Gage R&R, 같은 이미지 N회 반복 → 회차별 이미지가 전부 동일해 의미 없음)는 지금 포맷(집계 통계 2시트)을 그대로 유지하고, 일괄검사(Batch, 서로 다른 실물 부품 N개를 순서대로 검사 → 회차별 이미지가 서로 다름)에는 회차별 상세 행 + 캡쳐이미지를 새로 추가한다.

</domain>

<decisions>
## Implementation Decisions

### 분리 방식
- `RepeatExcelExportService.Export(...)` (기존 함수)는 시그니처/동작 변경 없이 그대로 둔다 — `ReviewerWindow.xaml.cs:433` 반복검사 버튼은 계속 이 함수를 호출한다 (회귀 0).
- 새 메서드(예: `RepeatExcelExportService.ExportBatch(...)` 또는 별도 클래스)를 추가해, 기존 2개 집계 시트("반복도 통계", "알고리즘 통계")는 그대로 만들고, 그 뒤에 "상세" 시트를 하나 더 추가한다 — 이 시트가 회차별(cycle별) × FAI별 측정 행 + 캡쳐이미지를 담는다.
- `InspectionListView.xaml.cs:621` 의 `Btn_batchExport_Click` 호출부만 새 메서드로 교체한다. 그 외 UI/버튼/다이얼로그 로직은 변경하지 않는다.

### 상세 시트 구성
- 컬럼 구성은 `ExcelExportService.cs`(quick 260805-d9y 에서 이미 구현됨)의 단일 cycle 포맷을 기준으로 하되, 맨 앞에 "회차"(cycle 순번, 1부터) 컬럼을 추가한다: 회차 | Shot | FAI | 측정명 | Nominal | Tol+ | Tol- | 측정값 | 판정 | 원본이미지 경로 | 캡쳐이미지 경로 | 캡쳐이미지.
- 이미지 삽입/비동기 레이스 대기 로직(폴링, 타임아웃, 예산 상한, JPEG 완결성 검사)은 `260805-d9y-excelexportservice` 작업에서 이미 `ExcelExportService.cs` 에 구현되어 있다. 이 로직을 복붙(중복)하지 말고 재사용한다 — 예: 해당 4개 private static 헬퍼(`LoadCaptureImageBytes`/`WaitForCaptureImage`/`TryReadCompleteJpeg`/`TryInsertCaptureImage`)를 `internal static` 으로 접근범위만 넓혀서 같은 네임스페이스(`ReringProject.Export`)의 `RepeatExcelExportService.cs` 에서 그대로 호출하는 방식을 권장한다. 단, 여러 cycle 을 다루므로 export 전체 대기 예산(`CAPTURE_WAIT_BUDGET_MS`)은 cycle 수에 비례해 늘어날 수 있다는 점을 감안해 설계할 것(무한정 UI 블로킹 금지 원칙은 동일하게 적용).
- 캡쳐 이미지가 아직 없는 행은 기존과 동일하게 빈 칸 + 경고 로그, export 자체는 실패시키지 않는다.

### Claude's Discretion
- 상세 시트 이름, 시트 순서(집계 시트 앞/뒤), 헬퍼 재사용 방식(internal static 전환 vs 별도 공유 클래스 추출)은 구현 재량.
- 여러 cycle × 여러 이미지를 다룰 때의 전체 대기 예산 상한값(cycle 수에 비례 스케일 or 고정 상한)은 구현 재량 — 단 "이미지가 하나도 없는 대량 batch를 export 해도 UI가 수 분간 멈추지 않는다"는 불변식은 반드시 지킬 것.

</decisions>

<specifics>
## Specific Ideas

- 관련 파일: `WPF_Example\UI\ControlItem\InspectionListView.xaml.cs`(605-629행, `Btn_batchExport_Click`, `_batchAccumulated`), `WPF_Example\Custom\Export\RepeatExcelExportService.cs`(기존 `Export` 함수, 시트1/시트2 구조), `WPF_Example\Custom\Export\ExcelExportService.cs`(재사용할 이미지 삽입 헬퍼 4종, quick 260805-d9y 커밋 656dc45).
- `_batchAccumulated` 는 `List<CycleResultDto>` 이며 각 cycle 은 이미 `FaiResultDto.OriginImageFileName`/`CaptureImageFileName` 절대경로를 보유하고 있다(quick 260805-d9y 조사에서 확인됨) — 경로 재조합 불필요.

</specifics>

<canonical_refs>
## Canonical References

- 이전 quick 작업(260805-d9y-excelexportservice)의 PLAN.md/interfaces 섹션 — ClosedXML 이미지 삽입 API, 비동기 레이스 대기 패턴이 이미 검증되어 있다. 그대로 재사용.

</canonical_refs>
