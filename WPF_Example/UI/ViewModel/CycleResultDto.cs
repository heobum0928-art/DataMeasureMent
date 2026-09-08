using System;
using System.Collections.Generic;
using System.Text;
using ReringProject.Halcon.Models;
using ReringProject.Sequence; // SkipReason 상수 참조용

namespace ReringProject.UI
{
    /// <summary>
    /// 1회 검사 cycle 전체 결과를 담는 최상위 JSON 직렬화 DTO.
    /// 결과 리뷰어와 xlsx export 의 공통 단일 소스.
    /// </summary>
    public class CycleResultDto
    {
        // 검사 일시
        public DateTime InspectionTime { get; set; }

        public string RecipeName { get; set; }

        //260622 hbk Phase 48 PROTO-01: 자재번호 (TestPacket.IndexNumber 에서 전파됨). -1 = 미수신(sentinel).
        public int IndexNumber { get; set; } = -1;

        //260820 hbk 이 사이클이 PLC 프로토콜($TEST)로 시작됐는지(=자동), 아니면 화면 RUN/일괄검사/반복검사로
        //  시작됐는지(=수동) 구분. 판정 근거는 InspectionSequence.IsProtocolDrivenCycle() 단일 소스.
        //  배경: 셋업 중 티칭하며 누른 수동 RUN 결과가 같은 날짜 통계 CSV 에 그대로 섞여, 반복성/재현성 시험
        //  통계를 오염시킨다. 자재번호 -1 로는 구분이 안 된다 — PLC 가 자재번호를 'null'/비정수로 보내도
        //  똑같이 -1 이 되므로(VisionRequestPacket.ParseMaterialField), -1 은 "수동"이 아니라 "자재번호 없음"이다.
        public bool IsProtocolDriven { get; set; } = false;

        /// <summary>종합 판정. "OK" / "NG" / "DETECT_FAIL" (3-state hierarchy).</summary>
        public string OverallJudgement { get; set; }

        // cycle 폴더 절대 경로 (SaveAsync 에서 설정됨)
        public string CycleFolderPath { get; set; }

        /// <summary>tick 판정 상수 — 실제 측정된 항목만 본 판정.</summary>
        public const string TICK_OK = "OK";

        /// <summary>tick 판정 상수 — 실제 측정된 항목만 본 판정.</summary>
        public const string TICK_NG = "NG";

        /// <summary>
        /// 이 tick 에서 실제 측정된 항목만 본 판정("OK"/"NG"). 측정된 항목이 없으면 null.
        /// 옛 cycle.json 은 이 키가 없어 null 로 역직렬화되며, 이는 리뷰어의 OverallJudgement 폴백 신호로 쓰인다.
        /// </summary>
        public string TickJudgement { get; set; }

        /// <summary>이 tick 에서 결과가 있는 Shot 이름 목록. 옛 cycle.json 은 빈 리스트로 남는다(폴백은 Shots 순회).</summary>
        public List<string> MeasuredShotNames { get; set; } = new List<string>();

        /// <summary>이 tick 의 z 번호. -1 = 없음/수동(옛 cycle.json 포함).</summary>
        public int ZIndex { get; set; } = -1;

        // 측정 데이터 — Shot > FAI > Measurement 계층
        public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
    }

    /// <summary>Shot 단위 결과 DTO.</summary>
    public class ShotResultDto
    {
        public string ShotName { get; set; }

        public string OwnerSequenceName { get; set; }

        /// <summary>
        /// 측정에 사용된 소스 이미지 절대 경로 (ShotConfig.GetLatestImagePath() = SimulImagePath).
        /// 리뷰어 재로드 및 xlsx 하이퍼링크에 사용된다.
        /// </summary>
        public string ResultImagePath { get; set; }

        public List<FaiResultDto> FAIs { get; set; } = new List<FaiResultDto>();
    }

    /// <summary>FAI 단위 결과 DTO.</summary>
    public class FaiResultDto
    {
        public string FAIName { get; set; }

        public bool IsPass { get; set; }

        /// <summary>true 이면 datum 검출 실패로 측정 미실행 ("DETECT_FAIL" 분기).</summary>
        public bool WasDatumSkipped { get; set; }

        /// <summary>원본 이미지 파일명. 예: origin_Top_FAI_A1_P1P2_153012345.png (경로 미포함).</summary>
        public string OriginImageFileName { get; set; }
        /// <summary>측정 오버레이 캡쳐 이미지 파일명. 예: capture_Top_FAI_A1_P1P2_153012345.png (경로 미포함).</summary>
        public string CaptureImageFileName { get; set; }

        public List<MeasurementResultDto> Measurements { get; set; } = new List<MeasurementResultDto>();

        /// <summary>overlay 기하.</summary>
        public List<EdgeInspectionOverlay> LastOverlays { get; set; } = new List<EdgeInspectionOverlay>();
    }

    /// <summary>Measurement 단위 결과 DTO. MeasurementBase 의 runtime 결과 필드 전체를 복사한다.</summary>
    public class MeasurementResultDto
    {
        public string MeasurementName { get; set; }

        public string TypeName { get; set; }

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }

        public double LastMeasuredValue { get; set; }

        /// <summary>true = OK. false = NG 또는 미측정.</summary>
        public bool LastJudgement { get; set; }

        /// <summary>0.0 도 정상 결과로 구분하기 위한 플래그. false 이면 측정 미실행.</summary>
        public bool LastHasResult { get; set; }

        /// <summary>null = 정상 측정, "DATUM_FAIL" = datum 검출 실패로 skip.</summary>
        public string LastSkipReason { get; set; }

        /// <summary>LastSkipReason == MEASURE_FAIL 일 때의 원본 에러 문자열(절단됨). 그 외에는 null.</summary>
        public string LastErrorMessage { get; set; }

        /// <summary>true = DualImage 측정(가로축/세로축 2장). 리뷰어가 전환 버튼을 노출하는 신호.</summary>
        public bool IsDualImage { get; set; }

        /// <summary>가로축 티칭 이미지 경로 (DualImage). 미설정 시 Shot 이미지로 fallback.</summary>
        public string HorizontalImagePath { get; set; }

        /// <summary>세로축 티칭 이미지 경로 (DualImage).</summary>
        public string VerticalImagePath { get; set; }
    }

    /// <summary>
    /// 리뷰어 좌측 cycle 목록의 표시 문자열을 조립하는 순수 로직. UI 타입 참조 금지(ReviewerWindow code-behind 는
    /// 호출·바인딩만). 옛 cycle.json(TickJudgement/MeasuredShotNames/ZIndex 없음)도 크래시 없이 폴백 표시한다.
    /// </summary>
    public static class ReviewerListLabelBuilder
    {
        private const string SEP = "  ";
        private const string ARROW = " ← ";
        private const string OVERALL_PREFIX = " · 종합 ";
        private const string ITEM_SEP = ", ";
        private const string ETC_PREFIX = " 외 ";
        private const int MAX_NG_ITEMS = 3;

        /// <summary>측정 1건의 사유 표시 텍스트. ReviewMeasurementRow/ExcelExportService 라벨과 동일 규칙.</summary>
        private static string BuildReasonText(MeasurementResultDto m)
        {
            if (m.LastSkipReason == SkipReason.DATUM_FAIL)
            {
                return "DETECT FAIL";
            }
            else if (m.LastSkipReason == SkipReason.NO_IMAGE)
            {
                return "NO IMAGE";
            }
            else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
            {
                return ReviewMeasurementRow.JUDGE_MEASURE_FAIL;
            }
            else
            {
                return "공차이탈"; // LastHasResult && !LastJudgement
            }
        }

        /// <summary>이 측정 항목이 이 tick 에서 다뤄졌는지(Task 2 FillTickSummary 규칙과 동일).</summary>
        private static bool IsHandled(MeasurementResultDto m, out bool bNg)
        {
            bool bHasResult = m.LastHasResult;
            bool bHasReason = !string.IsNullOrEmpty(m.LastSkipReason) && m.LastSkipReason != SkipReason.CROSS_Z_INCOMPLETE;
            bNg = (bHasResult && !m.LastJudgement) || bHasReason;
            return bHasResult || bHasReason;
        }

        /// <summary>tick 판정 라벨 — TickJudgement 우선, 비어 있으면(옛 JSON) OverallJudgement 폴백.</summary>
        private static string ResolveTickJudgement(CycleResultDto dto)
        {
            if (!string.IsNullOrEmpty(dto.TickJudgement))
            {
                return dto.TickJudgement;
            }
            return dto.OverallJudgement;
        }

        public static bool IsFailTick(CycleResultDto dto)
        {
            if (dto == null)
            {
                return false;
            }
            string szTick = ResolveTickJudgement(dto);
            if (szTick == CycleResultDto.TICK_NG)
            {
                return true;
            }
            if (dto.OverallJudgement != CycleResultDto.TICK_OK)
            {
                return true;
            }
            return false;
        }

        public static string Build(CycleResultDto dto)
        {
            if (dto == null)
            {
                return "";
            }

            string szTick = ResolveTickJudgement(dto);

            StringBuilder sb = new StringBuilder();
            sb.Append(dto.InspectionTime.ToString("HH:mm:ss"));

            if (dto.ZIndex >= 0)
            {
                sb.Append(SEP);
                sb.Append("z=");
                sb.Append(dto.ZIndex.ToString("D2"));
            }

            if (!string.IsNullOrEmpty(szTick))
            {
                sb.Append(SEP);
                sb.Append(szTick);
            }

            List<string> lstShotNames;
            if (dto.MeasuredShotNames != null && dto.MeasuredShotNames.Count > 0)
            {
                lstShotNames = dto.MeasuredShotNames;
            }
            else
            {
                lstShotNames = new List<string>();
                if (dto.Shots != null)
                {
                    foreach (var shot in dto.Shots)
                    {
                        if (!string.IsNullOrEmpty(shot.ShotName))
                        {
                            lstShotNames.Add(shot.ShotName);
                        }
                    }
                }
            }
            if (lstShotNames.Count > 0)
            {
                sb.Append(SEP);
                sb.Append(string.Join(ITEM_SEP, lstShotNames));
            }

            if (szTick == CycleResultDto.TICK_NG)
            {
                List<string> lstNgItems = new List<string>();
                if (dto.Shots != null)
                {
                    foreach (var shot in dto.Shots)
                    {
                        if (shot.FAIs == null) continue;
                        foreach (var fai in shot.FAIs)
                        {
                            if (fai.Measurements == null) continue;
                            foreach (var m in fai.Measurements)
                            {
                                bool bNg;
                                bool bHandled = IsHandled(m, out bNg);
                                if (bHandled && bNg)
                                {
                                    string szMeasName = m.MeasurementName;
                                    if (string.IsNullOrEmpty(szMeasName)) szMeasName = m.TypeName;
                                    lstNgItems.Add(szMeasName + "(" + BuildReasonText(m) + ")");
                                }
                            }
                        }
                    }
                }
                if (lstNgItems.Count > 0)
                {
                    sb.Append(ARROW);
                    int nShowCount = lstNgItems.Count;
                    if (nShowCount > MAX_NG_ITEMS) nShowCount = MAX_NG_ITEMS;
                    List<string> lstShown = lstNgItems.GetRange(0, nShowCount);
                    sb.Append(string.Join(ITEM_SEP, lstShown));
                    int nRemain = lstNgItems.Count - nShowCount;
                    if (nRemain > 0)
                    {
                        sb.Append(ETC_PREFIX);
                        sb.Append(nRemain);
                    }
                }
            }

            bool bBothPresent = !string.IsNullOrEmpty(dto.TickJudgement) && !string.IsNullOrEmpty(dto.OverallJudgement);
            if (bBothPresent && dto.TickJudgement != dto.OverallJudgement)
            {
                sb.Append(OVERALL_PREFIX);
                sb.Append(dto.OverallJudgement);
            }

            return sb.ToString();
        }
    }
}
