---
phase: 40-export-i-1-2026-06-01
plan: "04"
status: code_complete_uat_pending
requirements: [OUT-02]
date: "2026-06-09"
---

# 40-04 SUMMARY: 1회 검사 xlsx export (OUT-02)

## 구현 내용

**Task 1 — `WPF_Example/Custom/Export/ExcelExportService.cs` (신규)**
- `namespace ReringProject.Export`, `public static bool Export(CycleResultDto cycle, string outputPath)`.
- ClosedXML 0.105.0 `XLWorkbook` 기반. cycle/outputPath null 가드 → false.
- 메타 헤더(행 1~3): 모델명(RecipeName) / 검사일시(InspectionTime) / 종합판정(OverallJudgement).
- 테이블 헤더(행 5) 9컬럼: Shot/FAI/측정명/Nominal/Tol+/Tol-/측정값/판정/이미지.
- 1행=1측정: Shots→FAIs→Measurements 3중 순회. 측정값은 `LastHasResult` 기준(0.0 정상값, CO-23-01). 판정 3분기 `DATUM_FAIL → "DETECT FAIL" / LastHasResult → OK·NG / 미측정 → "-"` (ReviewMeasurementRow 로직 일치).
- 이미지 하이퍼링크(D-07): `shot.ResultImagePath` File.Exists 가드 + `Path.GetFullPath` 정규화 + `new Uri(abs).AbsoluteUri`(file:///) → `cell.SetHyperlink(new XLHyperlink(...))` (T-40-11 내부 경로만).
- `Columns().AdjustToContents()` 후 `SaveAs`. try/catch → `Logging.PrintErrLog((int)ELogType.Error, ...)` (T-40-12 크래시 0).
- csproj Compile 등록.

**Task 2 — 리뷰어 [엑셀 export] 버튼 wiring**
- `ReviewerWindow.xaml`: 좌측 패널 헤더 StackPanel 에 `btn_exportExcel`(IsEnabled=False 기본) 추가.
- `ReviewerWindow.xaml.cs`: `using ReringProject.Export;` + `Button_ExportExcel_Click` 핸들러 — `_currentCycle` null 가드 → `SaveFileDialog`(xlsx 필터, 파일명 `result_yyyyMMdd_HHmmss.xlsx`, InitialDirectory=cycle 폴더→ResultSavePath fallback) → `ExcelExportService.Export` → 결과 CustomMessageBox.
- `CycleList_SelectionChanged` 에서 `btn_exportExcel.IsEnabled = (_currentCycle != null)` 동기화.

## 검증

- msbuild Debug/x64 Build PASS (exit 0, 신규 error 0). 기존 baseline warning(CS0618/CS0612 등)만.
- 필드명/시그니처 실제 코드 대조 완료: CycleResultDto/ShotResultDto/FaiResultDto/MeasurementResultDto, CustomMessageBox.Show(title, msg, MessageBoxImage), SystemHandler.Handle.Setting.ResultSavePath.
- ClosedXML API 확인: `IXLCell.SetHyperlink(XLHyperlink)` 존재(ClosedXML.xml L4243).

## UAT 대기 (blocking checkpoint)

다음 사용자 육안 검증 필요 (40-04-PLAN Task 3):
1. SIMUL 빌드 실행 → 검사 1회 → 리뷰어에서 날짜 폴더 → cycle 선택(→ 버튼 활성)
2. [엑셀 export] → 저장 위치 지정 → 저장
3. 생성 xlsx 를 Microsoft Excel 에서 열기 → 정상 열림
4. 메타(모델명·검사일시·종합판정) + 1행=1측정 테이블(측정값 mm, 판정 OK/NG/DETECT FAIL) 확인
5. 이미지 "이미지 열기" 하이퍼링크 클릭 → 외부 뷰어로 결과 이미지 열림

## 파일

- WPF_Example/Custom/Export/ExcelExportService.cs (신규)
- WPF_Example/UI/Reviewer/ReviewerWindow.xaml (버튼)
- WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs (핸들러 + IsEnabled 동기화 + using)
- WPF_Example/DatumMeasurement.csproj (Compile 등록)
