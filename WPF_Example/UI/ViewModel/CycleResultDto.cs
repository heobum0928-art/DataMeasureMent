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

        /// <summary>
        /// 자동 검사 tick 에서 새로 촬영·저장된 기준점 사진. 수동/캐시 재사용 tick/옛 cycle.json 은 빈 목록.
        /// </summary>
        public List<DatumImageRecordDto> DatumImages { get; set; } = new List<DatumImageRecordDto>();

        /// <summary>
        /// Phase 77 D-77-06 ③: 자동 검사 tick 에서 저장된 Z 범위 후보 사진 — 설정(SaveZRangeCandidateImages)이
        /// 꺼져 있거나 옛 cycle.json 이면 빈 목록.
        /// </summary>
        public List<ZRangeImageRecordDto> ZRangeImages { get; set; } = new List<ZRangeImageRecordDto>();

        /// <summary>Phase 78 NGA-07(D-78-08): 이 시퀀스 Datum 진단 값. 기록 전용, 옛 cycle.json 은 빈 목록.</summary>
        public List<DatumDiagnosticDto> DatumDiagnostics { get; set; } = new List<DatumDiagnosticDto>();

        // 측정 데이터 — Shot > FAI > Measurement 계층
        public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
    }

    /// <summary>기준점(Datum) 사진 1장의 기록. 크로스-Z 두 장짜리 기준점은 역할별로 2건.</summary>
    public class DatumImageRecordDto
    {
        public const string ROLE_SINGLE = "";
        public const string ROLE_HORIZONTAL = "H";
        public const string ROLE_VERTICAL = "V";

        public string DatumName { get; set; }

        public string Role { get; set; }

        public string Path { get; set; }
    }

    /// <summary>Z 범위 후보 z 사진 1장의 기록. Phase 77 D-77-06 ③.</summary>
    public class ZRangeImageRecordDto
    {
        public string ShotName { get; set; }

        public int ZIndex { get; set; }

        public string Path { get; set; }
    }

    /// <summary>Z 범위 Shot 에서 이 측정의 후보 z 하나를 실제로 측정한 결과(Phase 78 NGA-07, D-78-08). 평가 순서대로 기록.</summary>
    public class ZCandidateScoreDto
    {
        public int ZIndex { get; set; }

        public bool Ok { get; set; }

        public double Score { get; set; }
    }

    /// <summary>
    /// tick 저장 시점의 기준점 진단 값(기록 전용, 판정 무관 — D-78-08). IsDetectedThisTick=false 면 이전 tick 에서
    /// 검출된 값.
    /// </summary>
    public class DatumDiagnosticDto
    {
        public string DatumName { get; set; }

        public bool IsDetected { get; set; }

        public bool IsDetectedThisTick { get; set; }

        public DateTime DetectTime { get; set; }

        public double OriginRow { get; set; }

        public double OriginCol { get; set; }

        public double AngleDeg { get; set; }

        public int EdgeCount { get; set; }

        public double FitRmse { get; set; }

        public double AlignMatchScore { get; set; }

        public double AlignMatchRow { get; set; }

        public double AlignMatchCol { get; set; }

        public double AlignMatchAngleDeg { get; set; }

        public double Align2Score { get; set; }

        public double AlignThetaDeg { get; set; }
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

        /// <summary>Phase 78 NGA-07(D-78-08): 범위 꺼짐·옛 cycle.json = -1.</summary>
        public const int Z_RANGE_NONE = -1;

        /// <summary>검사 당시 Shot 의 Z 범위 시작. R8 판정은 현재 레시피가 아니라 이 값을 쓴다.</summary>
        public int ZRangeStartIndex { get; set; } = Z_RANGE_NONE;

        /// <summary>검사 당시 Shot 의 Z 범위 끝. R8 판정은 현재 레시피가 아니라 이 값을 쓴다.</summary>
        public int ZRangeEndIndex { get; set; } = Z_RANGE_NONE;

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

        /// <summary>범위 Shot 에서 이 측정이 채택한 z 번호. -1 = 범위 미적용·옛 JSON(Phase 77 SZF-04, D-77-07 ⑥).</summary>
        public int SelectedZIndex { get; set; } = MeasurementBase.SELECTED_Z_NONE;

        /// <summary>Phase 78 NGA-07(D-78-08): 후보 z 별 선명도, 평가 순서대로. 범위 미적용·옛 JSON = 빈 목록.</summary>
        public List<ZCandidateScoreDto> ZCandidateScores { get; set; } = new List<ZCandidateScoreDto>();

        /// <summary>Phase 79 LSR-04(D-79-07): 사용 기준 코드 — "Local" = 핀 옆 띠(국부), "LocalFallback" = 국부 실패로 전역 전환, null = 옵션 꺼짐(전역, 기존과 같음)·옛 JSON.</summary>
        public string RefSource { get; set; }
    }

    /// <summary>
    /// 리뷰어 좌측 cycle 목록에서 이 tick 이 어떤 종류인지 — 기본 표시 여부 분류용(Quick 260915-k5g R1).
    /// </summary>
    public enum EReviewerTickKind
    {
        /// <summary>측정 결과(또는 사유)가 있는 결과 줄 — 목록 기본 표시.</summary>
        Result,

        /// <summary>Z 범위 후보 사진만 모으는 중간 tick — 측정 결과 없음, 기본 숨김.</summary>
        ZRangePending,

        /// <summary>측정 결과가 전혀 없는 기준점 tick — 기본 숨김.</summary>
        DatumOnly
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

        // Quick 260915-k5g R1/R2: 중간 단계 tick 라벨용 상수
        private const string LABEL_Z_RANGE_PENDING = "대기";
        private const string LABEL_DATUM_ONLY = "기준점";
        private const string TIME_FORMAT = "HH:mm:ss";
        private const string Z_TICK_PREFIX = "z=";
        private const string Z_TICK_FORMAT = "D2";

        // Quick 260915-k5g R3: 판정 바로 뒤에 붙는 사용 Z 요약용 상수 — 줄 끝에 두면 "종합 OK z3" 로 잘못 읽힌다
        private const string USED_Z_PREFIX = "사용 ";
        private const string USED_Z_SEP = "·";

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
            if (m.LastSkipReason == SkipReason.Z_RANGE_PENDING) { bHasReason = false; }
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

        /// <summary>dto 의 Shot &gt; FAI &gt; Measurement 전체를 null 가드하며 한 리스트로 모은다(중간 단계 분류·사용 Z 요약 공용).</summary>
        private static List<MeasurementResultDto> CollectMeasurements(CycleResultDto dto)
        {
            List<MeasurementResultDto> lstResult = new List<MeasurementResultDto>();
            if (dto == null || dto.Shots == null)
            {
                return lstResult;
            }
            foreach (var shot in dto.Shots)
            {
                if (shot == null || shot.FAIs == null)
                {
                    continue;
                }
                foreach (var fai in shot.FAIs)
                {
                    if (fai == null || fai.Measurements == null)
                    {
                        continue;
                    }
                    foreach (var m in fai.Measurements)
                    {
                        if (m == null)
                        {
                            continue;
                        }
                        lstResult.Add(m);
                    }
                }
            }
            return lstResult;
        }

        /// <summary>이 측정에 실제 결과(값 또는 결과성 사유)가 있는지 — Z_RANGE_PENDING 은 결과로 치지 않는다(K-2).</summary>
        private static bool HasMeasuredResult(MeasurementResultDto m)
        {
            if (m.LastHasResult)
            {
                return true;
            }
            bool bHasReason = !string.IsNullOrEmpty(m.LastSkipReason) && m.LastSkipReason != SkipReason.Z_RANGE_PENDING;
            return bHasReason;
        }

        /// <summary>이 Shot 의 측정 중 Z 범위 후보 대기(Z_RANGE_PENDING) 사유가 하나라도 있는지.</summary>
        private static bool ShotHasZRangePending(ShotResultDto shot)
        {
            if (shot == null || shot.FAIs == null)
            {
                return false;
            }
            foreach (var fai in shot.FAIs)
            {
                if (fai == null || fai.Measurements == null)
                {
                    continue;
                }
                foreach (var m in fai.Measurements)
                {
                    if (m == null)
                    {
                        continue;
                    }
                    if (m.LastSkipReason == SkipReason.Z_RANGE_PENDING)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 이 tick 이 결과 줄인지, Z 범위 대기 중간 tick 인지, 기준점 전용 tick 인지 분류한다(K-1).
        /// 순서 고정: dto null·ZIndex&lt;0(수동/옛 JSON)·불량 tick 은 항상 Result, 그 다음 결과 유무 → 대기 유무 → 기준점.
        /// </summary>
        public static EReviewerTickKind ClassifyTick(CycleResultDto dto)
        {
            if (dto == null)
            {
                return EReviewerTickKind.Result;
            }
            if (dto.ZIndex < 0)
            {
                return EReviewerTickKind.Result;
            }
            if (IsFailTick(dto))
            {
                return EReviewerTickKind.Result;
            }

            List<MeasurementResultDto> lstMeasurements = CollectMeasurements(dto);
            bool bAnyResult = false;
            foreach (var m in lstMeasurements)
            {
                if (HasMeasuredResult(m))
                {
                    bAnyResult = true;
                    break;
                }
            }
            if (bAnyResult)
            {
                return EReviewerTickKind.Result;
            }

            bool bAnyPending = false;
            if (dto.Shots != null)
            {
                foreach (var shot in dto.Shots)
                {
                    if (ShotHasZRangePending(shot))
                    {
                        bAnyPending = true;
                        break;
                    }
                }
            }
            if (bAnyPending)
            {
                return EReviewerTickKind.ZRangePending;
            }

            return EReviewerTickKind.DatumOnly;
        }

        /// <summary>true = 목록 기본 숨김 대상(대기·기준점). probe 가 리플렉션으로 이 이름을 찾는다 — 시그니처 변경 금지.</summary>
        public static bool IsIntermediateTick(CycleResultDto dto)
        {
            EReviewerTickKind eKind = ClassifyTick(dto);
            if (eKind == EReviewerTickKind.Result)
            {
                return false;
            }
            return true;
        }

        /// <summary>'불량만 보기' ON 이면 불량 줄만, 중간 단계 줄은 '중간 단계도 보기' 를 체크했을 때만 보인다(K-4).</summary>
        public static bool IsListItemVisible(bool bIsIntermediate, bool bIsNg, bool bShowIntermediate, bool bFailOnly)
        {
            bool bHiddenByFailOnly = bFailOnly && !bIsNg;
            if (bHiddenByFailOnly)
            {
                return false;
            }
            bool bHiddenIntermediate = bIsIntermediate && !bShowIntermediate;
            if (bHiddenIntermediate)
            {
                return false;
            }
            return true;
        }

        /// <summary>대기/기준점 tick 의 회색 목록 문구 — 시각·z 번호·종류, 대기일 때만 대기 Shot 이름 목록(K-3).</summary>
        private static string BuildIntermediateLabel(CycleResultDto dto, EReviewerTickKind eKind)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(dto.InspectionTime.ToString(TIME_FORMAT));
            sb.Append(SEP);
            sb.Append(Z_TICK_PREFIX);
            sb.Append(dto.ZIndex.ToString(Z_TICK_FORMAT));
            sb.Append(SEP);
            if (eKind == EReviewerTickKind.ZRangePending)
            {
                sb.Append(LABEL_Z_RANGE_PENDING);
            }
            else
            {
                sb.Append(LABEL_DATUM_ONLY);
            }

            if (eKind == EReviewerTickKind.ZRangePending && dto.Shots != null)
            {
                List<string> lstPendingShots = new List<string>();
                foreach (var shot in dto.Shots)
                {
                    if (shot == null)
                    {
                        continue;
                    }
                    bool bShotPending = ShotHasZRangePending(shot);
                    bool bShotNameEmpty = string.IsNullOrEmpty(shot.ShotName);
                    bool bShotEligible = bShotPending && !bShotNameEmpty;
                    if (bShotEligible && !lstPendingShots.Contains(shot.ShotName))
                    {
                        lstPendingShots.Add(shot.ShotName);
                    }
                }
                if (lstPendingShots.Count > 0)
                {
                    sb.Append(SEP);
                    sb.Append(string.Join(ITEM_SEP, lstPendingShots));
                }
            }

            return sb.ToString();
        }

        /// <summary>판정 바로 뒤에 붙는 사용 Z 요약("사용 z3" / "사용 z3·z4"), 범위 미사용이면 빈 문자열 — 유효 판정은 MeasurementBase.FormatSelectedZ 단일 소스(K-5).</summary>
        private static string BuildUsedZSummary(CycleResultDto dto)
        {
            List<MeasurementResultDto> lstMeasurements = CollectMeasurements(dto);
            List<int> lstZ = new List<int>();
            foreach (var m in lstMeasurements)
            {
                string szZ = MeasurementBase.FormatSelectedZ(m.SelectedZIndex);
                if (string.IsNullOrEmpty(szZ))
                {
                    continue;
                }
                if (!lstZ.Contains(m.SelectedZIndex))
                {
                    lstZ.Add(m.SelectedZIndex);
                }
            }
            if (lstZ.Count == 0)
            {
                return string.Empty;
            }
            lstZ.Sort();
            List<string> lstZText = new List<string>();
            foreach (int nZ in lstZ)
            {
                lstZText.Add(MeasurementBase.FormatSelectedZ(nZ));
            }
            return USED_Z_PREFIX + string.Join(USED_Z_SEP, lstZText);
        }

        public static string Build(CycleResultDto dto)
        {
            if (dto == null)
            {
                return "";
            }

            EReviewerTickKind eKind = ClassifyTick(dto);
            if (eKind != EReviewerTickKind.Result)
            {
                return BuildIntermediateLabel(dto, eKind);
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

            string szUsedZ = BuildUsedZSummary(dto);
            if (!string.IsNullOrEmpty(szUsedZ))
            {
                sb.Append(SEP);
                sb.Append(szUsedZ);
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

    /// <summary>
    /// NG 원인 규칙 1건의 판정 결과. 화면 패널과 NG 누적 엑셀이 같은 결과를 쓴다(D-78-04).
    /// </summary>
    public class NgCauseResult
    {
        public bool IsNg { get; set; }

        public bool HasCause { get; set; }

        public string CauseCode { get; set; } = "";

        public string CauseText { get; set; } = "";

        public string EvidenceText { get; set; } = "";

        public string ActionText { get; set; } = "";

        public List<string> SuspectCodes { get; set; } = new List<string>();

        public string SuspectText { get; set; } = "";

        public static NgCauseResult Empty()
        {
            return new NgCauseResult();
        }

        public static NgCauseResult NotNg()
        {
            NgCauseResult result = new NgCauseResult();
            result.IsNg = false;
            result.HasCause = false;
            result.CauseText = NgCauseAnalyzer.NOT_NG_TEXT;
            return result;
        }
    }

    /// <summary>같은 (Shot,FAI,측정명) 이력의 사이클 1건 표본. 추세 규칙(R6/R9) 입력.</summary>
    public class NgHistorySample
    {
        public DateTime InspectionTime { get; set; }

        public string CycleKey { get; set; }

        public double Value { get; set; }

        public bool IsOk { get; set; }
    }

    /// <summary>
    /// 같은 날짜 폴더 cycle.json 이력을 측정 키별로 모은다. 파일 I/O 없음 — 리뷰어가 이미 읽은 dto 를
    /// AddCycle 로 누적할 뿐이다(RESEARCH Pitfall 3, D-78-03).
    /// </summary>
    public class NgCauseHistory
    {
        public const string KEY_SEPARATOR = "";
        public const string CYCLE_KEY_TIME_PREFIX = "T";

        private readonly Dictionary<string, List<NgHistorySample>> _dicSamples = new Dictionary<string, List<NgHistorySample>>();
        private readonly HashSet<string> _setCycleKeys = new HashSet<string>(StringComparer.Ordinal);
        private bool _bSorted = true;

        public static string ResolveCycleKey(CycleResultDto dto)
        {
            if (dto == null)
            {
                return "";
            }
            if (!string.IsNullOrEmpty(dto.CycleFolderPath))
            {
                return dto.CycleFolderPath.TrimEnd('\\', '/');
            }
            return CYCLE_KEY_TIME_PREFIX + dto.InspectionTime.Ticks.ToString();
        }

        public static string BuildMeasurementKey(string szShot, string szFai, string szMeas)
        {
            string szShotSafe = szShot;
            if (szShotSafe == null) { szShotSafe = ""; }
            string szFaiSafe = szFai;
            if (szFaiSafe == null) { szFaiSafe = ""; }
            string szMeasSafe = szMeas;
            if (szMeasSafe == null) { szMeasSafe = ""; }
            return szShotSafe + KEY_SEPARATOR + szFaiSafe + KEY_SEPARATOR + szMeasSafe;
        }

        public int SampleCount
        {
            get
            {
                int nTotal = 0;
                foreach (var kvp in _dicSamples)
                {
                    nTotal += kvp.Value.Count;
                }
                return nTotal;
            }
        }

        public void AddCycle(CycleResultDto dto)
        {
            if (dto == null)
            {
                return;
            }
            string szCycleKey = ResolveCycleKey(dto);
            if (_setCycleKeys.Contains(szCycleKey))
            {
                return;
            }
            _setCycleKeys.Add(szCycleKey);

            if (dto.Shots == null)
            {
                return;
            }
            foreach (var shot in dto.Shots)
            {
                if (shot == null || shot.FAIs == null)
                {
                    continue;
                }
                foreach (var fai in shot.FAIs)
                {
                    if (fai == null || fai.Measurements == null)
                    {
                        continue;
                    }
                    foreach (var m in fai.Measurements)
                    {
                        if (m == null || !m.LastHasResult)
                        {
                            continue;
                        }
                        string szKey = BuildMeasurementKey(shot.ShotName, fai.FAIName, m.MeasurementName);
                        List<NgHistorySample> lstSamples;
                        bool bHasList = _dicSamples.TryGetValue(szKey, out lstSamples);
                        if (!bHasList)
                        {
                            lstSamples = new List<NgHistorySample>();
                            _dicSamples[szKey] = lstSamples;
                        }
                        NgHistorySample sample = new NgHistorySample();
                        sample.InspectionTime = dto.InspectionTime;
                        sample.CycleKey = szCycleKey;
                        sample.Value = m.LastMeasuredValue;
                        sample.IsOk = m.LastJudgement;
                        lstSamples.Add(sample);
                    }
                }
            }
            _bSorted = false;
        }

        private static int CompareSamples(NgHistorySample a, NgHistorySample b)
        {
            int nTimeCompare = a.InspectionTime.CompareTo(b.InspectionTime);
            if (nTimeCompare != 0)
            {
                return nTimeCompare;
            }
            return string.CompareOrdinal(a.CycleKey, b.CycleKey);
        }

        public List<NgHistorySample> GetSamples(string szMeasurementKey)
        {
            if (!_bSorted)
            {
                foreach (var kvp in _dicSamples)
                {
                    kvp.Value.Sort(CompareSamples);
                }
                _bSorted = true;
            }
            List<NgHistorySample> lstSamples;
            bool bHasList = _dicSamples.TryGetValue(szMeasurementKey, out lstSamples);
            if (!bHasList)
            {
                return new List<NgHistorySample>();
            }
            return lstSamples;
        }
    }

    /// <summary>
    /// NG 원인 추정 규칙 엔진. CycleResultDto/NgCauseHistory 만 입력받는 순수 정적 클래스 — 파일 I/O·전역
    /// 싱글턴·레시피 참조 없음(P-2, D-78-03). 화면 패널과 NG 누적 엑셀이 이 클래스 하나만 호출한다(D-78-04).
    /// </summary>
    public static class NgCauseAnalyzer
    {
        public const string CODE_NONE = "";
        public const string CODE_R0 = "R0";
        public const string CODE_R1 = "R1";
        public const string CODE_R2 = "R2";
        public const string CODE_R3 = "R3";
        public const string CODE_R4 = "R4";
        public const string CODE_R5 = "R5";
        public const string CODE_R6 = "R6";
        public const string CODE_R7 = "R7";
        public const string CODE_R8 = "R8";
        public const string CODE_R9 = "R9";
        public const string CODE_UNKNOWN = "RX";

        public const string NOT_NG_TEXT = "이 항목은 NG 가 아니라 원인 분석 대상이 아닙니다";
        public const string PANEL_CAUSE_PREFIX = "추정 원인: ";
        public const string PANEL_EVIDENCE_PREFIX = "근거: ";
        public const string PANEL_ACTION_PREFIX = "확인할 일: ";
        public const string PANEL_SUSPECT_PREFIX = "함께 의심: ";
        public const string PANEL_LINE_SEPARATOR = "\n";
        public const string VALUE_FORMAT = "F4";
        public const string SIDE_BIG_TEXT = "큰";
        public const string SIDE_SMALL_TEXT = "작은";

        public const int R6_WINDOW_SIZE = 5;
        public const int R6_MIN_SAMPLES = 3;
        public const string R6_CAUSE_TEXT = "보정값 또는 티칭이 한쪽으로 치우쳐 있습니다";
        public const string R6_EVIDENCE_FORMAT = "최근 {0}회 평균 {1} · 기준 {2} (허용 {3} ~ {4}) · {0}회 모두 {5} 쪽";
        public const string R6_ACTION_TEXT = "Shot 보정값과 이 측정의 티칭 위치를 확인하세요";

        public const string R0_CAUSE_TEXT = "공차를 벗어났습니다 (규칙으로 원인을 특정하지 못함)";
        public const string R0_EVIDENCE_FORMAT = "측정 {0} · 허용 {1} ~ {2} · {3} 벗어남";
        public const string R0_ACTION_TEXT = "사진에서 측정 위치를 보고, 같은 자재로 다시 검사해 보세요";

        public const string RX_CAUSE_TEXT = "알 수 없는 사유로 측정하지 못했습니다";
        public const string RX_EVIDENCE_FORMAT = "사유 {0}";
        public const string RX_ACTION_TEXT = "같은 시각의 Error 로그를 확인하세요";

        public const string R1_CAUSE_TEXT = "사진이 안 찍혔습니다";
        public const string R1_EVIDENCE_TEXT = "사진이 없어 측정하지 못함 (사유 NO_IMAGE)";
        public const string R1_ACTION_TEXT = "카메라 연결과 PLC 촬영 신호(z 번호)를 확인하세요";

        public const string R2_CAUSE_TEXT = "기준점을 못 찾았습니다";
        public const string R2_EVIDENCE_DATUM_FAIL_TEXT = "기준선 검출 실패로 측정 안 함 (사유 DATUM_FAIL)";
        public const string R2_EVIDENCE_ALIGN_FAIL_TEXT = "기준 패턴 매칭 실패로 측정 안 함 (사유 ALIGN_FAIL)";
        public const string R2_EVIDENCE_REF_MISSING_TEXT = "측정이 가리키는 기준점 이름이 레시피에 없음 (사유 DATUM_REF_MISSING)";
        public const string R2_ACTION_TEXT = "자재가 제대로 놓였는지, 기준점 티칭과 조명을 확인하세요";
        public const string R2_ACTION_REF_MISSING_TEXT = "레시피에서 이 측정이 쓰는 기준점 이름을 확인하세요";

        public const string R3_CAUSE_TEXT = "측정할 선(에지)을 못 찾았습니다";
        public const string R3_EVIDENCE_FORMAT = "측정 실패 · {0}";
        public const string R3_NO_ERROR_TEXT = "오류 내용 없음";
        public const int EVIDENCE_ERROR_MAX_CHARS = 60;
        public const string R3_ACTION_TEXT = "사진에서 측정 위치(ROI)·조명·초점을 확인하세요";

        public const string R4_CAUSE_TEXT = "설정 오류로 측정하지 못했습니다";
        public const string R4_EVIDENCE_ZINDEX_TEXT = "z 번호 설정이 맞지 않아 측정 안 함 (사유 ZINDEX_MISCONFIGURED)";
        public const string R4_EVIDENCE_CROSS_Z_TEXT = "두 z 사진이 모두 필요한 측정인데 PLC 자동 검사가 아니라 측정 안 함 (사유 CROSS_Z_INCOMPLETE)";
        public const string R4_ACTION_ZINDEX_TEXT = "PLC z 번호와 레시피 Z 설정을 확인하세요";
        public const string R4_ACTION_CROSS_Z_TEXT = "PLC 자동 검사로 다시 확인하세요 (수동 실행은 두 장짜리 측정을 못 합니다)";

        // Phase 78 NGA-01(78-02 Task 1): R9 공차 경계 흔들림
        public const double R9_BOUNDARY_RATIO = 0.10;
        public const double PERCENT_SCALE = 100.0;
        public const string PERCENT_FORMAT = "F0";
        public const string R9_CAUSE_TEXT = "공차 경계에서 값이 흔들립니다";
        public const string R9_EVIDENCE_FORMAT = "측정 {0} · 허용 {1} ~ {2} · 경계를 {3} 넘음 (허용 폭의 {4}% 이내) · 직전 검사는 OK";
        public const string R9_DIR_ABOVE_TEXT = "위로";
        public const string R9_DIR_BELOW_TEXT = "아래로";
        public const string R9_ACTION_TEXT = "같은 자재로 반복 검사를 해서 값이 흔들리는지 확인하세요";

        // Phase 78 NGA-01(78-02 Task 1): R8 초점 범위 끝
        public const int MIN_VALID_Z_INDEX = 1;
        public const string R8_CAUSE_TEXT = "초점 범위가 부족할 수 있습니다";
        public const string R8_EVIDENCE_FORMAT = "가장 선명한 사진이 범위 끝 {0} 에서 나옴 {1} · {2}";
        public const string R8_RANGE_RECORDED_FORMAT = "(범위 {0}~{1})";
        public const string R8_RANGE_END_ONLY_FORMAT = "(범위 끝 {0})";
        public const string R8_RANGE_FALLBACK_TEXT = "(범위 끝 = 검사 당시 z 번호)";
        public const string R8_SCORES_PREFIX = "선명도 ";
        public const string R8_SCORE_ITEM_FORMAT = "{0}={1}";
        public const string R8_SCORE_FAILED_TEXT = "실패";
        public const string R8_SCORE_FORMAT = "F1";
        public const string R8_SCORE_ITEM_SEPARATOR = " ";
        public const string R8_NO_SCORES_TEXT = "후보별 선명도 기록 없음(이 기능 이전 데이터)";
        public const string R8_ACTION_TEXT = "PLC 에 Z 범위를 끝 쪽으로 더 넓혀 달라고 요청하세요";

        // Phase 78 NGA-01(78-02 Task 1): R7 한 곳만 벗어남
        public const int R7_LONE_NG_COUNT = 1;
        public const string R7_CAUSE_TEXT = "이 위치만 벗어났습니다 (실제 불량이나 이물 가능성)";
        public const string R7_EVIDENCE_FORMAT = "이번 검사 NG 는 이 측정 1개뿐 · 측정 {0} · 허용 {1} ~ {2}";
        public const string R7_ACTION_TEXT = "사진에서 이 위치를 보고 실물(이물·찍힘)을 확인하세요";

        // Phase 78 NGA-01(78-02 Task 1): 함께 의심 라벨
        public const string LABEL_R5 = "기준점 흔들림";
        public const string LABEL_R6 = "보정값/티칭 치우침";
        public const string LABEL_R7 = "한 곳만 벗어남";
        public const string LABEL_R8 = "초점 범위 부족";
        public const string LABEL_R9 = "공차 경계 흔들림";
        public const string SUSPECT_SEPARATOR = ", ";

        // Phase 78 NGA-01(78-02 Task 2): R5 기준점 흔들림
        public const int R5_NEIGHBORS_EACH_SIDE = 2;
        public const int R5_MIN_NEIGHBORS = 2;
        public const int R5_MIN_SHARED_COUNT = 2;
        public const double R5_MIN_SHIFT_TOL_RATIO = 0.15;
        public const double R5_SIMILAR_FACTOR = 2.0;
        public const string R5_SUPPORTED_TYPE_NAME = "EdgeToLineDistance";
        public const string DIST_LINE_ROI_ID = "FAI-DistLine";
        public const int DIST_LINE_FOOT_INDEX = 0;
        public const int DIST_LINE_EDGE_INDEX = 1;
        public const int DIST_LINE_MIN_POINTS = 2;
        public const string R5_CAUSE_TEXT = "기준점 위치가 흔들렸습니다";
        public const string R5_EVIDENCE_FORMAT = "같은 기준선을 쓰는 측정 {0}개가 이웃 검사보다 같은 방향으로 약 {1}mm 움직임 · {2}";
        public const string R5_ACTION_TEXT = "자재 안착과 기준점 촬영(촬영 전 대기시간)이 흔들리지 않는지 확인하세요";
        public const string R5_DATUM_FORMAT = "기준점 {0} 각도 {1}°";
        public const string R5_DATUM_SCORE_FORMAT = " · 패턴 점수 {0}";
        public const string ANGLE_FORMAT = "F3";
        public const string MATCH_SCORE_FORMAT = "F2";
        public const string R5_NO_DATUM_RECORD_TEXT = "기준점 기록 없음(이 기능 이전 데이터)";
        public const string R5_NO_DATUM_DETECTED_TEXT = "기준점 검출 기록 없음";

        /// <summary>대표/함께 의심 조립용 규칙 발동 결과 1건.</summary>
        private class RuleHit
        {
            public string Code;
            public string CauseText;
            public string EvidenceText;
            public string ActionText;
        }

        /// <summary>R5 묶음 멤버 1건 — 같은 기준선을 쓰는 측정의 이웃 대비 편차·기하·허용 폭.</summary>
        private class R5Member
        {
            public MeasurementResultDto Measurement;
            public double Deviation;
            public double UnitRow;
            public double UnitCol;
            public double HalfWidth;
        }

        /// <summary>P-1 NG 범위: 측정 null → false, Z_RANGE_PENDING·CROSS_Z_INCOMPLETE → false, 그 밖 사유 있으면 true, 사유 없으면 LastHasResult 이고 LastJudgement false 일 때만 true.</summary>
        public static bool IsNgMeasurement(MeasurementResultDto m)
        {
            if (m == null)
            {
                return false;
            }
            if (m.LastSkipReason == SkipReason.Z_RANGE_PENDING)
            {
                return false;
            }
            if (m.LastSkipReason == SkipReason.CROSS_Z_INCOMPLETE)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(m.LastSkipReason))
            {
                return true;
            }
            return m.LastHasResult && !m.LastJudgement;
        }

        private static void ResolveToleranceBand(MeasurementResultDto m, out double dLower, out double dUpper)
        {
            double dLowerLocal = m.NominalValue - Math.Abs(m.ToleranceMinus);
            double dUpperLocal = m.NominalValue + Math.Abs(m.TolerancePlus);
            if (dLowerLocal > dUpperLocal)
            {
                double dTmp = dLowerLocal;
                dLowerLocal = dUpperLocal;
                dUpperLocal = dTmp;
            }
            dLower = dLowerLocal;
            dUpper = dUpperLocal;
        }

        private static NgCauseResult BuildResult(bool bIsNg, string szCode, string szCause, string szEvidence, string szAction)
        {
            NgCauseResult result = new NgCauseResult();
            result.IsNg = bIsNg;
            result.HasCause = true;
            result.CauseCode = szCode;
            result.CauseText = szCause;
            result.EvidenceText = szEvidence;
            result.ActionText = szAction;
            return result;
        }

        private static bool TryEvaluateR6(NgCauseHistory history, string szKey, string szCycleKey, double dNominal, double dLower, double dUpper,
            out int nCount, out double dAverage, out bool bAboveUpper)
        {
            nCount = 0;
            dAverage = 0.0;
            bAboveUpper = false;

            List<NgHistorySample> lstSamples = history.GetSamples(szKey);
            int nAnchorIndex = -1;
            for (int i = 0; i < lstSamples.Count; i++)
            {
                if (lstSamples[i].CycleKey == szCycleKey)
                {
                    nAnchorIndex = i;
                    break;
                }
            }
            if (nAnchorIndex < 0)
            {
                return false;
            }

            int nStart = nAnchorIndex - R6_WINDOW_SIZE + 1;
            if (nStart < 0)
            {
                nStart = 0;
            }
            List<double> lstWindow = new List<double>();
            for (int i = nStart; i <= nAnchorIndex; i++)
            {
                lstWindow.Add(lstSamples[i].Value);
            }

            int nForwardIndex = nAnchorIndex + 1;
            while (lstWindow.Count < R6_MIN_SAMPLES && nForwardIndex < lstSamples.Count && lstWindow.Count < R6_WINDOW_SIZE)
            {
                lstWindow.Add(lstSamples[nForwardIndex].Value);
                nForwardIndex++;
            }

            if (lstWindow.Count < R6_MIN_SAMPLES)
            {
                return false;
            }

            bool bAllAboveNominalStrict = true;
            bool bAllBelowNominalStrict = true;
            double dSum = 0.0;
            foreach (double dValue in lstWindow)
            {
                dSum += dValue;
                if (dValue <= dNominal)
                {
                    bAllAboveNominalStrict = false;
                }
                if (dValue >= dNominal)
                {
                    bAllBelowNominalStrict = false;
                }
            }
            bool bAllOneSide = bAllAboveNominalStrict || bAllBelowNominalStrict;
            if (!bAllOneSide)
            {
                return false;
            }

            double dAvg = dSum / lstWindow.Count;
            bool bAvgOutOfRange = dAvg < dLower || dAvg > dUpper;
            if (!bAvgOutOfRange)
            {
                return false;
            }

            nCount = lstWindow.Count;
            dAverage = dAvg;
            bAboveUpper = bAllAboveNominalStrict;
            return true;
        }

        /// <summary>공차를 넘은 쪽의 허용 폭·넘은 양·방향(위/아래). 이미 공차 밖인 값에만 호출한다(P-6 표).</summary>
        private static void ResolveExceedSide(MeasurementResultDto m, double dLower, double dUpper,
            out double dHalfWidth, out double dOverAmount, out bool bAboveUpper)
        {
            bAboveUpper = m.LastMeasuredValue > dUpper;
            if (bAboveUpper)
            {
                dHalfWidth = Math.Abs(m.TolerancePlus);
                dOverAmount = m.LastMeasuredValue - dUpper;
            }
            else
            {
                dHalfWidth = Math.Abs(m.ToleranceMinus);
                dOverAmount = dLower - m.LastMeasuredValue;
            }
        }

        /// <summary>이력 표본 목록에서 이 사이클 키의 위치. 없으면 -1.</summary>
        private static int FindAnchorIndex(List<NgHistorySample> lstSamples, string szCycleKey)
        {
            for (int i = 0; i < lstSamples.Count; i++)
            {
                if (lstSamples[i].CycleKey == szCycleKey)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>R9 공차 경계 흔들림: 넘은 양이 허용 폭의 R9_BOUNDARY_RATIO 이내이고 직전 표본이 OK.</summary>
        private static RuleHit TryEvaluateR9(List<NgHistorySample> lstSamples, int nAnchorIndex, MeasurementResultDto m,
            double dLower, double dUpper)
        {
            bool bHasPrevSample = nAnchorIndex > 0 && nAnchorIndex < lstSamples.Count;
            if (!bHasPrevSample)
            {
                return null;
            }
            if (!lstSamples[nAnchorIndex - 1].IsOk)
            {
                return null;
            }
            double dHalfWidth;
            double dOverAmount;
            bool bAboveUpper;
            ResolveExceedSide(m, dLower, dUpper, out dHalfWidth, out dOverAmount, out bAboveUpper);
            if (dHalfWidth <= 0.0)
            {
                return null;
            }
            bool bWithinBoundary = dOverAmount <= (dHalfWidth * R9_BOUNDARY_RATIO);
            if (!bWithinBoundary)
            {
                return null;
            }
            string szDir;
            if (bAboveUpper)
            {
                szDir = R9_DIR_ABOVE_TEXT;
            }
            else
            {
                szDir = R9_DIR_BELOW_TEXT;
            }
            double dPercent = R9_BOUNDARY_RATIO * PERCENT_SCALE;
            string szEvidence = string.Format(R9_EVIDENCE_FORMAT, m.LastMeasuredValue.ToString(VALUE_FORMAT),
                dLower.ToString(VALUE_FORMAT), dUpper.ToString(VALUE_FORMAT), szDir, dPercent.ToString(PERCENT_FORMAT));
            RuleHit hit = new RuleHit();
            hit.Code = CODE_R9;
            hit.CauseText = R9_CAUSE_TEXT;
            hit.EvidenceText = szEvidence;
            hit.ActionText = R9_ACTION_TEXT;
            return hit;
        }

        /// <summary>R8 범위 끝: shot 기록값 우선, 없으면 자동 tick 의 cycle.ZIndex 로 대체(현재 레시피 참조 없음 — PR-4).</summary>
        private static int ResolveZRangeEnd(CycleResultDto cycle, ShotResultDto shot, MeasurementResultDto m, out bool bRecorded)
        {
            bool bShotRangeRecorded = shot != null && shot.ZRangeEndIndex >= MIN_VALID_Z_INDEX;
            if (bShotRangeRecorded)
            {
                bRecorded = true;
                return shot.ZRangeEndIndex;
            }
            bool bProtocolDriven = cycle != null && cycle.IsProtocolDriven;
            bool bCycleZValid = cycle != null && cycle.ZIndex >= MIN_VALID_Z_INDEX;
            bool bMeasZValid = m != null && m.SelectedZIndex >= MIN_VALID_Z_INDEX;
            bool bFallbackEligible = bProtocolDriven && bCycleZValid && bMeasZValid;
            if (bFallbackEligible)
            {
                bRecorded = false;
                return cycle.ZIndex;
            }
            bRecorded = false;
            return -1;
        }

        /// <summary>R8 근거의 후보별 선명도 정렬 보조 — z 오름차순, 같은 z 는 기록 순서(List.Sort 불안정성 회피).</summary>
        private class ScoredCandidate
        {
            public int OriginalIndex;
            public ZCandidateScoreDto Score;
        }

        private static int CompareScoredCandidate(ScoredCandidate a, ScoredCandidate b)
        {
            int nZCompare = a.Score.ZIndex.CompareTo(b.Score.ZIndex);
            if (nZCompare != 0)
            {
                return nZCompare;
            }
            return a.OriginalIndex.CompareTo(b.OriginalIndex);
        }

        private static string BuildCandidateScoreText(List<ZCandidateScoreDto> lstScores)
        {
            bool bEmpty = lstScores == null || lstScores.Count == 0;
            if (bEmpty)
            {
                return R8_NO_SCORES_TEXT;
            }
            List<ScoredCandidate> lstSorted = new List<ScoredCandidate>();
            for (int i = 0; i < lstScores.Count; i++)
            {
                ScoredCandidate sc = new ScoredCandidate();
                sc.OriginalIndex = i;
                sc.Score = lstScores[i];
                lstSorted.Add(sc);
            }
            lstSorted.Sort(CompareScoredCandidate);
            List<string> lstItems = new List<string>();
            foreach (ScoredCandidate sc in lstSorted)
            {
                string szZ = MeasurementBase.FormatSelectedZ(sc.Score.ZIndex);
                string szScoreText;
                if (sc.Score.Ok)
                {
                    szScoreText = sc.Score.Score.ToString(R8_SCORE_FORMAT);
                }
                else
                {
                    szScoreText = R8_SCORE_FAILED_TEXT;
                }
                lstItems.Add(string.Format(R8_SCORE_ITEM_FORMAT, szZ, szScoreText));
            }
            return R8_SCORES_PREFIX + string.Join(R8_SCORE_ITEM_SEPARATOR, lstItems.ToArray());
        }

        private static RuleHit TryEvaluateR8(CycleResultDto cycle, ShotResultDto shot, MeasurementResultDto m)
        {
            bool bRecorded;
            int nRangeEnd = ResolveZRangeEnd(cycle, shot, m, out bRecorded);
            bool bRangeEndValid = nRangeEnd >= MIN_VALID_Z_INDEX;
            if (!bRangeEndValid)
            {
                return null;
            }
            bool bSelectedIsEnd = m.SelectedZIndex == nRangeEnd;
            if (!bSelectedIsEnd)
            {
                return null;
            }
            string szRangeText;
            if (bRecorded)
            {
                bool bStartValid = shot != null && shot.ZRangeStartIndex >= MIN_VALID_Z_INDEX;
                if (bStartValid)
                {
                    szRangeText = string.Format(R8_RANGE_RECORDED_FORMAT, MeasurementBase.FormatSelectedZ(shot.ZRangeStartIndex),
                        MeasurementBase.FormatSelectedZ(nRangeEnd));
                }
                else
                {
                    szRangeText = string.Format(R8_RANGE_END_ONLY_FORMAT, MeasurementBase.FormatSelectedZ(nRangeEnd));
                }
            }
            else
            {
                szRangeText = R8_RANGE_FALLBACK_TEXT;
            }
            string szScoresText = BuildCandidateScoreText(m.ZCandidateScores);
            string szEvidence = string.Format(R8_EVIDENCE_FORMAT, MeasurementBase.FormatSelectedZ(nRangeEnd), szRangeText, szScoresText);
            RuleHit hit = new RuleHit();
            hit.Code = CODE_R8;
            hit.CauseText = R8_CAUSE_TEXT;
            hit.EvidenceText = szEvidence;
            hit.ActionText = R8_ACTION_TEXT;
            return hit;
        }

        /// <summary>사이클 전체(모든 Shot/FAI/측정, null 가드)에서 IsNgMeasurement 인 측정 수.</summary>
        private static int CountNgMeasurements(CycleResultDto cycle)
        {
            int nCount = 0;
            if (cycle == null || cycle.Shots == null)
            {
                return nCount;
            }
            foreach (var shot in cycle.Shots)
            {
                if (shot == null || shot.FAIs == null)
                {
                    continue;
                }
                foreach (var fai in shot.FAIs)
                {
                    if (fai == null || fai.Measurements == null)
                    {
                        continue;
                    }
                    foreach (var mm in fai.Measurements)
                    {
                        if (mm == null)
                        {
                            continue;
                        }
                        if (IsNgMeasurement(mm))
                        {
                            nCount++;
                        }
                    }
                }
            }
            return nCount;
        }

        /// <summary>R7 한 곳만 벗어남: 사이클 전체 NG 수가 1개이고 R5(강)·R6·R9 가 미발동일 때만.</summary>
        private static RuleHit TryEvaluateR7(CycleResultDto cycle, MeasurementResultDto m, double dLower, double dUpper,
            bool bR5StrongFired, bool bR6Fired, bool bR9Fired)
        {
            bool bSuppressed = bR5StrongFired || bR6Fired || bR9Fired;
            if (bSuppressed)
            {
                return null;
            }
            int nNgCount = CountNgMeasurements(cycle);
            bool bLoneNg = nNgCount == R7_LONE_NG_COUNT;
            if (!bLoneNg)
            {
                return null;
            }
            string szEvidence = string.Format(R7_EVIDENCE_FORMAT, m.LastMeasuredValue.ToString(VALUE_FORMAT),
                dLower.ToString(VALUE_FORMAT), dUpper.ToString(VALUE_FORMAT));
            RuleHit hit = new RuleHit();
            hit.Code = CODE_R7;
            hit.CauseText = R7_CAUSE_TEXT;
            hit.EvidenceText = szEvidence;
            hit.ActionText = R7_ACTION_TEXT;
            return hit;
        }

        /// <summary>발동 규칙 코드 → 함께 의심 라벨. 전통 switch(case 마다 return).</summary>
        private static string GetRuleLabel(string szCode)
        {
            switch (szCode)
            {
                case CODE_R5:
                    return LABEL_R5;
                case CODE_R6:
                    return LABEL_R6;
                case CODE_R7:
                    return LABEL_R7;
                case CODE_R8:
                    return LABEL_R8;
                case CODE_R9:
                    return LABEL_R9;
                default:
                    return szCode;
            }
        }

        private static NgCauseResult BuildR0Result(MeasurementResultDto m, double dLower, double dUpper)
        {
            string szOverText;
            if (m.LastMeasuredValue > dUpper)
            {
                szOverText = (m.LastMeasuredValue - dUpper).ToString(VALUE_FORMAT);
            }
            else
            {
                szOverText = (dLower - m.LastMeasuredValue).ToString(VALUE_FORMAT);
            }
            string szR0Evidence = string.Format(R0_EVIDENCE_FORMAT, m.LastMeasuredValue.ToString(VALUE_FORMAT),
                dLower.ToString(VALUE_FORMAT), dUpper.ToString(VALUE_FORMAT), szOverText);
            return BuildResult(true, CODE_R0, R0_CAUSE_TEXT, szR0Evidence, R0_ACTION_TEXT);
        }

        /// <summary>값 목록의 중앙값(복사 정렬, 홀수 = 가운데, 짝수 = 가운데 두 값 평균).</summary>
        private static double Median(List<double> lstValues)
        {
            List<double> lstSorted = new List<double>(lstValues);
            lstSorted.Sort();
            int nCount = lstSorted.Count;
            if (nCount == 0)
            {
                return 0.0;
            }
            bool bOdd = (nCount % 2) == 1;
            if (bOdd)
            {
                return lstSorted[nCount / 2];
            }
            int nHigh = nCount / 2;
            int nLow = nHigh - 1;
            return (lstSorted[nLow] + lstSorted[nHigh]) / 2.0;
        }

        /// <summary>fai.LastOverlays 에서 이 측정의 FAI-DistLine 오버레이 발끝→에지 단위벡터. 길이 0 은 제외(0 나눗셈 가드).</summary>
        private static bool TryFindDistLineUnit(FaiResultDto fai, string szMeasName, out double dUnitRow, out double dUnitCol)
        {
            dUnitRow = 0.0;
            dUnitCol = 0.0;
            if (fai == null || fai.LastOverlays == null)
            {
                return false;
            }
            foreach (var overlay in fai.LastOverlays)
            {
                if (overlay == null)
                {
                    continue;
                }
                if (overlay.RoiId != DIST_LINE_ROI_ID)
                {
                    continue;
                }
                if (overlay.MeasurementName != szMeasName)
                {
                    continue;
                }
                if (overlay.Points == null || overlay.Points.Count < DIST_LINE_MIN_POINTS)
                {
                    continue;
                }
                var foot = overlay.Points[DIST_LINE_FOOT_INDEX];
                var edge = overlay.Points[DIST_LINE_EDGE_INDEX];
                if (foot == null || edge == null)
                {
                    continue;
                }
                double dRow = edge.Row - foot.Row;
                double dCol = edge.Column - foot.Column;
                double dLength = Math.Sqrt((dRow * dRow) + (dCol * dCol));
                if (dLength <= 0.0)
                {
                    continue;
                }
                dUnitRow = dRow / dLength;
                dUnitCol = dCol / dLength;
                return true;
            }
            return false;
        }

        /// <summary>이력 표본에서 이 사이클 앞 R5_NEIGHBORS_EACH_SIDE 개 + 뒤 R5_NEIGHBORS_EACH_SIDE 개(범위 밖은 잘림, 자기 제외).</summary>
        private static List<double> CollectNeighborValues(List<NgHistorySample> lstSamples, string szCycleKey)
        {
            List<double> lstResult = new List<double>();
            int nAnchorIndex = FindAnchorIndex(lstSamples, szCycleKey);
            if (nAnchorIndex < 0)
            {
                return lstResult;
            }
            int nStart = nAnchorIndex - R5_NEIGHBORS_EACH_SIDE;
            if (nStart < 0)
            {
                nStart = 0;
            }
            int nEnd = nAnchorIndex + R5_NEIGHBORS_EACH_SIDE;
            if (nEnd >= lstSamples.Count)
            {
                nEnd = lstSamples.Count - 1;
            }
            for (int i = nStart; i <= nEnd; i++)
            {
                if (i == nAnchorIndex)
                {
                    continue;
                }
                lstResult.Add(lstSamples[i].Value);
            }
            return lstResult;
        }

        /// <summary>이 사이클·이 FAI 안에서 같은 기준선(FAI-DistLine)을 쓰는 EdgeToLineDistance 측정 묶음. 순서 = fai.Measurements.</summary>
        private static List<R5Member> BuildR5Group(CycleResultDto cycle, ShotResultDto shot, FaiResultDto fai, NgCauseHistory history)
        {
            List<R5Member> lstResult = new List<R5Member>();
            if (fai == null || fai.Measurements == null || shot == null)
            {
                return lstResult;
            }
            string szCycleKey = NgCauseHistory.ResolveCycleKey(cycle);
            foreach (var g in fai.Measurements)
            {
                if (g == null)
                {
                    continue;
                }
                if (!g.LastHasResult)
                {
                    continue;
                }
                if (g.TypeName != R5_SUPPORTED_TYPE_NAME)
                {
                    continue;
                }
                double dUnitRow;
                double dUnitCol;
                bool bHasUnit = TryFindDistLineUnit(fai, g.MeasurementName, out dUnitRow, out dUnitCol);
                if (!bHasUnit)
                {
                    continue;
                }
                string szKey = NgCauseHistory.BuildMeasurementKey(shot.ShotName, fai.FAIName, g.MeasurementName);
                List<NgHistorySample> lstSamples = history.GetSamples(szKey);
                List<double> lstNeighbors = CollectNeighborValues(lstSamples, szCycleKey);
                bool bEnoughNeighbors = lstNeighbors.Count >= R5_MIN_NEIGHBORS;
                if (!bEnoughNeighbors)
                {
                    continue;
                }
                double dNeighborMedian = Median(lstNeighbors);
                R5Member member = new R5Member();
                member.Measurement = g;
                member.Deviation = g.LastMeasuredValue - dNeighborMedian;
                member.UnitRow = dUnitRow;
                member.UnitCol = dUnitCol;
                member.HalfWidth = Math.Min(Math.Abs(g.TolerancePlus), Math.Abs(g.ToleranceMinus));
                lstResult.Add(member);
            }
            return lstResult;
        }

        /// <summary>묶음 첫 측정의 단위벡터를 기준 축으로, 각 멤버 이동량(부호 정렬) 계산.</summary>
        private static List<double> ComputeR5Movements(List<R5Member> lstGroup)
        {
            List<double> lstResult = new List<double>();
            if (lstGroup.Count == 0)
            {
                return lstResult;
            }
            double dRefRow = lstGroup[0].UnitRow;
            double dRefCol = lstGroup[0].UnitCol;
            foreach (R5Member member in lstGroup)
            {
                double dDot = (member.UnitRow * dRefRow) + (member.UnitCol * dRefCol);
                double dMovement;
                if (dDot >= 0.0)
                {
                    dMovement = -member.Deviation;
                }
                else
                {
                    dMovement = member.Deviation;
                }
                lstResult.Add(dMovement);
            }
            return lstResult;
        }

        /// <summary>R5 근거의 기준점 설명 — 기록 없음/검출 없음/각도·패턴 점수.</summary>
        private static string BuildDatumEvidenceText(CycleResultDto cycle)
        {
            bool bNoRecords = cycle == null || cycle.DatumDiagnostics == null || cycle.DatumDiagnostics.Count == 0;
            if (bNoRecords)
            {
                return R5_NO_DATUM_RECORD_TEXT;
            }
            DatumDiagnosticDto latest = null;
            foreach (DatumDiagnosticDto d in cycle.DatumDiagnostics)
            {
                if (d == null || !d.IsDetected)
                {
                    continue;
                }
                if (latest == null)
                {
                    latest = d;
                    continue;
                }
                if (d.DetectTime > latest.DetectTime)
                {
                    latest = d;
                }
            }
            if (latest == null)
            {
                return R5_NO_DATUM_DETECTED_TEXT;
            }
            string szText = string.Format(R5_DATUM_FORMAT, latest.DatumName, latest.AngleDeg.ToString(ANGLE_FORMAT));
            if (latest.AlignMatchScore > 0.0)
            {
                szText += string.Format(R5_DATUM_SCORE_FORMAT, latest.AlignMatchScore.ToString(MATCH_SCORE_FORMAT));
            }
            return szText;
        }

        /// <summary>R5 기준점 흔들림. history null 이면 null. 강/약 판정은 bStrong 으로.</summary>
        private static RuleHit TryEvaluateR5(CycleResultDto cycle, ShotResultDto shot, FaiResultDto fai, MeasurementResultDto m,
            double dLower, double dUpper, NgCauseHistory history, out bool bStrong)
        {
            bStrong = false;
            if (history == null)
            {
                return null;
            }
            List<R5Member> lstGroup = BuildR5Group(cycle, shot, fai, history);
            R5Member thisMember = null;
            foreach (R5Member member in lstGroup)
            {
                if (member.Measurement == m)
                {
                    thisMember = member;
                    break;
                }
            }
            if (thisMember == null)
            {
                return null;
            }
            bool bEnoughShared = lstGroup.Count >= R5_MIN_SHARED_COUNT;
            if (!bEnoughShared)
            {
                return null;
            }

            List<double> lstMovements = ComputeR5Movements(lstGroup);
            bool bAllPositive = true;
            bool bAllNegative = true;
            foreach (double dMove in lstMovements)
            {
                if (dMove <= 0.0)
                {
                    bAllPositive = false;
                }
                if (dMove >= 0.0)
                {
                    bAllNegative = false;
                }
            }
            bool bAllSameSign = bAllPositive || bAllNegative;
            if (!bAllSameSign)
            {
                return null;
            }

            double dMedianMove = Median(lstMovements);
            double dMinHalfWidth = double.MaxValue;
            foreach (R5Member member in lstGroup)
            {
                if (member.HalfWidth < dMinHalfWidth)
                {
                    dMinHalfWidth = member.HalfWidth;
                }
            }
            bool bShiftEnough = Math.Abs(dMedianMove) >= (R5_MIN_SHIFT_TOL_RATIO * dMinHalfWidth);
            if (!bShiftEnough)
            {
                return null;
            }

            double dMaxAbsMove = 0.0;
            double dMinAbsMove = double.MaxValue;
            foreach (double dMove in lstMovements)
            {
                double dAbs = Math.Abs(dMove);
                if (dAbs > dMaxAbsMove)
                {
                    dMaxAbsMove = dAbs;
                }
                if (dAbs < dMinAbsMove)
                {
                    dMinAbsMove = dAbs;
                }
            }
            bool bSimilarMagnitude = dMaxAbsMove <= (R5_SIMILAR_FACTOR * dMinAbsMove);
            if (!bSimilarMagnitude)
            {
                return null;
            }

            double dThisHalfWidth = Math.Min(Math.Abs(m.TolerancePlus), Math.Abs(m.ToleranceMinus));
            bool bStrongByMagnitude = Math.Abs(dMedianMove) >= dThisHalfWidth;
            double dAdjustedValue = m.LastMeasuredValue - thisMember.Deviation;
            bool bStrongByAdjusted = dAdjustedValue >= dLower && dAdjustedValue <= dUpper;
            bStrong = bStrongByMagnitude || bStrongByAdjusted;

            string szDatumText = BuildDatumEvidenceText(cycle);
            string szEvidence = string.Format(R5_EVIDENCE_FORMAT, lstGroup.Count, Math.Abs(dMedianMove).ToString(VALUE_FORMAT), szDatumText);
            RuleHit hit = new RuleHit();
            hit.Code = CODE_R5;
            hit.CauseText = R5_CAUSE_TEXT;
            hit.EvidenceText = szEvidence;
            hit.ActionText = R5_ACTION_TEXT;
            return hit;
        }

        private static NgCauseResult BuildOutOfToleranceResult(CycleResultDto cycle, ShotResultDto shot, FaiResultDto fai,
            MeasurementResultDto m, NgCauseHistory history, double dLower, double dUpper)
        {
            bool bR5Strong;
            RuleHit r5 = TryEvaluateR5(cycle, shot, fai, m, dLower, dUpper, history, out bR5Strong);
            RuleHit r9 = null;
            RuleHit r6 = null;
            if (history != null)
            {
                string szShotName;
                if (shot != null)
                {
                    szShotName = shot.ShotName;
                }
                else
                {
                    szShotName = null;
                }
                string szFaiName;
                if (fai != null)
                {
                    szFaiName = fai.FAIName;
                }
                else
                {
                    szFaiName = null;
                }
                string szKey = NgCauseHistory.BuildMeasurementKey(szShotName, szFaiName, m.MeasurementName);
                string szCycleKey = NgCauseHistory.ResolveCycleKey(cycle);
                List<NgHistorySample> lstSamples = history.GetSamples(szKey);
                int nAnchorIndex = FindAnchorIndex(lstSamples, szCycleKey);
                r9 = TryEvaluateR9(lstSamples, nAnchorIndex, m, dLower, dUpper);

                int nCount;
                double dAverage;
                bool bAboveUpper;
                bool bR6 = TryEvaluateR6(history, szKey, szCycleKey, m.NominalValue, dLower, dUpper, out nCount, out dAverage, out bAboveUpper);
                if (bR6)
                {
                    string szSide;
                    if (bAboveUpper)
                    {
                        szSide = SIDE_BIG_TEXT;
                    }
                    else
                    {
                        szSide = SIDE_SMALL_TEXT;
                    }
                    string szEvidence = string.Format(R6_EVIDENCE_FORMAT, nCount, dAverage.ToString(VALUE_FORMAT),
                        m.NominalValue.ToString(VALUE_FORMAT), dLower.ToString(VALUE_FORMAT), dUpper.ToString(VALUE_FORMAT), szSide);
                    r6 = new RuleHit();
                    r6.Code = CODE_R6;
                    r6.CauseText = R6_CAUSE_TEXT;
                    r6.EvidenceText = szEvidence;
                    r6.ActionText = R6_ACTION_TEXT;
                }
            }

            RuleHit r8 = TryEvaluateR8(cycle, shot, m);
            RuleHit r7 = TryEvaluateR7(cycle, m, dLower, dUpper, bR5Strong, r6 != null, r9 != null);

            List<RuleHit> lstFired = new List<RuleHit>();
            bool bR5Weak = r5 != null && !bR5Strong;
            if (r5 != null && bR5Strong) { lstFired.Add(r5); }
            if (r9 != null) { lstFired.Add(r9); }
            if (r6 != null) { lstFired.Add(r6); }
            if (r8 != null) { lstFired.Add(r8); }
            if (r7 != null) { lstFired.Add(r7); }
            if (bR5Weak) { lstFired.Add(r5); }

            if (lstFired.Count == 0)
            {
                return BuildR0Result(m, dLower, dUpper);
            }

            RuleHit primary = lstFired[0];
            NgCauseResult result = BuildResult(true, primary.Code, primary.CauseText, primary.EvidenceText, primary.ActionText);
            List<string> lstSuspectLabels = new List<string>();
            for (int i = 1; i < lstFired.Count; i++)
            {
                result.SuspectCodes.Add(lstFired[i].Code);
                lstSuspectLabels.Add(GetRuleLabel(lstFired[i].Code));
            }
            result.SuspectText = string.Join(SUSPECT_SEPARATOR, lstSuspectLabels.ToArray());
            return result;
        }

        /// <summary>사유 기반 규칙(R1~R4, RX) — 전통 if/else if 사슬, ReviewerListLabelBuilder.BuildReasonText 와 같은 스타일.</summary>
        private static NgCauseResult BuildReasonResult(MeasurementResultDto m)
        {
            if (m.LastSkipReason == SkipReason.NO_IMAGE)
            {
                return BuildResult(true, CODE_R1, R1_CAUSE_TEXT, R1_EVIDENCE_TEXT, R1_ACTION_TEXT);
            }
            else if (m.LastSkipReason == SkipReason.DATUM_FAIL)
            {
                return BuildResult(true, CODE_R2, R2_CAUSE_TEXT, R2_EVIDENCE_DATUM_FAIL_TEXT, R2_ACTION_TEXT);
            }
            else if (m.LastSkipReason == SkipReason.ALIGN_FAIL)
            {
                return BuildResult(true, CODE_R2, R2_CAUSE_TEXT, R2_EVIDENCE_ALIGN_FAIL_TEXT, R2_ACTION_TEXT);
            }
            else if (m.LastSkipReason == SkipReason.DATUM_REF_MISSING)
            {
                return BuildResult(true, CODE_R2, R2_CAUSE_TEXT, R2_EVIDENCE_REF_MISSING_TEXT, R2_ACTION_REF_MISSING_TEXT);
            }
            else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
            {
                string szErrorText = m.LastErrorMessage;
                if (string.IsNullOrEmpty(szErrorText))
                {
                    szErrorText = R3_NO_ERROR_TEXT;
                }
                else if (szErrorText.Length > EVIDENCE_ERROR_MAX_CHARS)
                {
                    szErrorText = szErrorText.Substring(0, EVIDENCE_ERROR_MAX_CHARS);
                }
                string szEvidence = string.Format(R3_EVIDENCE_FORMAT, szErrorText);
                return BuildResult(true, CODE_R3, R3_CAUSE_TEXT, szEvidence, R3_ACTION_TEXT);
            }
            else if (m.LastSkipReason == SkipReason.ZINDEX_MISCONFIGURED)
            {
                return BuildResult(true, CODE_R4, R4_CAUSE_TEXT, R4_EVIDENCE_ZINDEX_TEXT, R4_ACTION_ZINDEX_TEXT);
            }
            else
            {
                string szEvidence = string.Format(RX_EVIDENCE_FORMAT, m.LastSkipReason);
                return BuildResult(true, CODE_UNKNOWN, RX_CAUSE_TEXT, szEvidence, RX_ACTION_TEXT);
            }
        }

        /// <summary>원인 규칙 평가 진입점. cycle/m null → Empty, CROSS_Z_INCOMPLETE → NG 아님이지만 설명은 표시, 그 밖 NG 아니면 NotNg, 사유 있으면 사유 규칙, 없으면 값 이탈 규칙.</summary>
        public static NgCauseResult Analyze(CycleResultDto cycle, ShotResultDto shot, FaiResultDto fai, MeasurementResultDto m, NgCauseHistory history)
        {
            if (cycle == null || m == null)
            {
                return NgCauseResult.Empty();
            }
            if (m.LastSkipReason == SkipReason.CROSS_Z_INCOMPLETE)
            {
                return BuildResult(false, CODE_R4, R4_CAUSE_TEXT, R4_EVIDENCE_CROSS_Z_TEXT, R4_ACTION_CROSS_Z_TEXT);
            }
            if (!IsNgMeasurement(m))
            {
                return NgCauseResult.NotNg();
            }
            if (!string.IsNullOrEmpty(m.LastSkipReason))
            {
                return BuildReasonResult(m);
            }

            double dLower;
            double dUpper;
            ResolveToleranceBand(m, out dLower, out dUpper);
            return BuildOutOfToleranceResult(cycle, shot, fai, m, history, dLower, dUpper);
        }

        /// <summary>패널 3~4줄 조립. r null → 빈 문자열, 원인 없음 → CauseText 만, 있으면 원인/근거/확인할 일(+함께 의심).</summary>
        public static string BuildPanelText(NgCauseResult r)
        {
            if (r == null)
            {
                return "";
            }
            if (!r.HasCause)
            {
                return r.CauseText;
            }
            List<string> lstLines = new List<string>();
            lstLines.Add(PANEL_CAUSE_PREFIX + r.CauseText);
            lstLines.Add(PANEL_EVIDENCE_PREFIX + r.EvidenceText);
            lstLines.Add(PANEL_ACTION_PREFIX + r.ActionText);
            if (!string.IsNullOrEmpty(r.SuspectText))
            {
                lstLines.Add(PANEL_SUSPECT_PREFIX + r.SuspectText);
            }
            return string.Join(PANEL_LINE_SEPARATOR, lstLines);
        }
    }
}
