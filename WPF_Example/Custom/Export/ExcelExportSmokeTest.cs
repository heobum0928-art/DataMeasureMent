//260601 hbk Phase 40 OUT-02 — ClosedXML 런타임 로드 smoke test (의존성 검증 후 제거 가능)
using ClosedXML.Excel;
using System;
using System.IO;

namespace ReringProject.Export
{
    public static class ExcelExportSmokeTest
    {
        /// <summary>
        /// ClosedXML + 전이 의존성(.NET 4.8 런타임)이 정상 로드되는지 검증한다.
        /// true = XLWorkbook 생성 + xlsx 저장 성공 (SixLabors.Fonts net48 로드 확인).
        /// false + error = 어셈블리 로드 실패 메시지.
        /// </summary>
        public static bool TryCreateWorkbook(out string error)
        {
            error = null;
            try
            {
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Smoke");
                    ws.Cell(1, 1).Value = "OK";
                    string tmp = Path.Combine(Path.GetTempPath(), "closedxml_smoke.xlsx");
                    wb.SaveAs(tmp);
                    bool ok = File.Exists(tmp);
                    try { File.Delete(tmp); } catch { }
                    return ok;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
