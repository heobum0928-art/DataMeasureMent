using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.UI
{
    /// <summary>상세 그리드 1행. 표시 전용 — 계산은 ViewModel 이 끝내고 문자열만 담는다.</summary>
    public class AlignVerifyRowView
    {
        public string TimeText { get; set; } = "";
        public string KindText { get; set; } = "";
        public string TargetOrSequenceText { get; set; } = "";
        public string SlotOrDatumText { get; set; } = "";
        public string ValueText { get; set; } = "";
        public string JudgeText { get; set; } = "";
        public string ImageFileText { get; set; } = "";
    }

    /// <summary>
    /// Align 정합 조회 화면 상태. 표시 문자열을 전부 여기서 만들어 XAML 은 바인딩만 한다.
    ///
    /// 임계값이 0(미설정)인 동안에는 정상/벗어남 판정을 절대 표시하지 않는다.
    /// 실측 산포 없이 임계를 넣으면 정상품을 버리는 사고가 난다.
    /// </summary>
    public class AlignVerifyViewModel : INotifyPropertyChanged
    {
        private const string JUDGE_OK_TEXT = "정상";
        private const string JUDGE_OUT_TEXT = "벗어남";
        private const string JUDGE_NOT_SET_TEXT = "(판정 기준 미설정)";
        private const string NUM_FORMAT = "F4";
        private const string PX_FORMAT = "F2";
        private const int DEFAULT_RECENT_COUNT = 1000;
        private const int DEFAULT_RANGE_DAYS = -7;

        private const string KNOWN_LIMIT_TEXT =
            "※ SIDE 는 측면 촬영이라 앞뒤(깊이) 방향은 검증되지 않습니다. 좌우·높이만 확인됩니다.\n" +
            "※ ① 은 검출·강체변환의 자기일관성만 검증합니다. 피커센터 기준 재표현(부호 규약)은 ②로 확인합니다.";

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string szName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(szName));
            }
        }

        // ---- 입력 상태 ----

        private string _materialNoText = "";
        public string MaterialNoText
        {
            get { return _materialNoText; }
            set { _materialNoText = value; Raise("MaterialNoText"); }
        }

        private DateTime _dateFrom = DateTime.Today.AddDays(DEFAULT_RANGE_DAYS);
        public DateTime DateFrom
        {
            get { return _dateFrom; }
            set { _dateFrom = value; Raise("DateFrom"); }
        }

        private DateTime _dateTo = DateTime.Today;
        public DateTime DateTo
        {
            get { return _dateTo; }
            set { _dateTo = value; Raise("DateTo"); }
        }

        private int _recentCount = DEFAULT_RECENT_COUNT;
        public int RecentCount
        {
            get { return _recentCount; }
            set { _recentCount = value; Raise("RecentCount"); }
        }

        public List<int> RecentCountOptions { get; private set; } = new List<int> { 100, 500, 1000, 5000 };

        // ---- 출력 상태 (전부 완성된 표시 문자열) ----

        private string _materialHeaderText = "자재번호 미지정";
        public string MaterialHeaderText
        {
            get { return _materialHeaderText; }
            private set { _materialHeaderText = value; Raise("MaterialHeaderText"); }
        }

        private string _alignValueText = "-";
        public string AlignValueText
        {
            get { return _alignValueText; }
            private set { _alignValueText = value; Raise("AlignValueText"); }
        }

        private string _alignJudgeText = JUDGE_NOT_SET_TEXT;
        public string AlignJudgeText
        {
            get { return _alignJudgeText; }
            private set { _alignJudgeText = value; Raise("AlignJudgeText"); }
        }

        private string _seatValueText = "-";
        public string SeatValueText
        {
            get { return _seatValueText; }
            private set { _seatValueText = value; Raise("SeatValueText"); }
        }

        private string _seatJudgeText = JUDGE_NOT_SET_TEXT;
        public string SeatJudgeText
        {
            get { return _seatJudgeText; }
            private set { _seatJudgeText = value; Raise("SeatJudgeText"); }
        }

        private string _conclusionText = "";
        public string ConclusionText
        {
            get { return _conclusionText; }
            private set { _conclusionText = value; Raise("ConclusionText"); }
        }

        private string _trendAlignText = "";
        public string TrendAlignText
        {
            get { return _trendAlignText; }
            private set { _trendAlignText = value; Raise("TrendAlignText"); }
        }

        private string _trendSeatText = "";
        public string TrendSeatText
        {
            get { return _trendSeatText; }
            private set { _trendSeatText = value; Raise("TrendSeatText"); }
        }

        private string _statusText = "조회 전";
        public string StatusText
        {
            get { return _statusText; }
            private set { _statusText = value; Raise("StatusText"); }
        }

        private string _limitNoticeText = "";
        public string LimitNoticeText
        {
            get { return _limitNoticeText; }
            private set { _limitNoticeText = value; Raise("LimitNoticeText"); }
        }

        /// <summary>이 문구는 조건부가 아니라 항상 보인다. 오해를 막는 것이 목적이다.</summary>
        public string KnownLimitText { get { return KNOWN_LIMIT_TEXT; } }

        public ObservableCollection<AlignVerifySeatStat> SeatStats { get; private set; }
            = new ObservableCollection<AlignVerifySeatStat>();

        public ObservableCollection<AlignVerifyRowView> DetailRows { get; private set; }
            = new ObservableCollection<AlignVerifyRowView>();

        // ---- 동작 ----

        /// <summary>조회 1회. 실패해도 throw 하지 않고 StatusText 에 사유를 남긴다.</summary>
        public void ExecuteQuery()
        {
            try
            {
                int nMaterialNo = AlignVerifyRecord.NO_MATERIAL;
                int nParsed;
                bool bParsed = int.TryParse(_materialNoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out nParsed);
                if (bParsed)
                {
                    nMaterialNo = nParsed;
                }

                AlignVerifyQueryResult result = AlignVerifyCsvLoader.Query(DateFrom, DateTo, nMaterialNo, RecentCount);

                UpdateMaterialHeader(nMaterialNo);
                UpdateAlignSection(result);
                UpdateSeatSection(result);
                UpdateConclusion(result, nMaterialNo);
                UpdateTrend(result);
                UpdateSeatStats(result);
                UpdateDetailRows(result);

                StatusText = "조회 결과 " + result.TotalRowCount.ToString("N0", CultureInfo.InvariantCulture) + "행 (구간 내 전체)";
            }
            catch (Exception ex)
            {
                StatusText = "조회 실패: " + ex.Message;
                try { Logging.PrintErrLog((int)ELogType.Error, "[AlignVerifyViewModel] ExecuteQuery failed: " + ex.Message); } catch { }
            }
        }

        private void UpdateMaterialHeader(int nMaterialNo)
        {
            if (nMaterialNo < 0)
            {
                MaterialHeaderText = "자재번호 미지정";
                return;
            }
            MaterialHeaderText = "자재번호 " + nMaterialNo.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateAlignSection(AlignVerifyQueryResult result)
        {
            if (result.HasMaterialAlign == false)
            {
                AlignValueText = "-";
                AlignJudgeText = ResolveAlignJudge(false, 0.0);
                return;
            }

            AlignValueText = result.MaterialAlignDistanceMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture)
                           + " mm   theta " + result.MaterialAlignThetaDeg.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + "°";
            AlignJudgeText = ResolveAlignJudge(true, result.MaterialAlignDistanceMm);
        }

        private string ResolveAlignJudge(bool bHasValue, double dDistanceMm)
        {
            double dResidualLimit = SystemHandler.Handle.Setting.AlignVerifyResidualLimitMm;
            bool bResidualLimitSet = dResidualLimit > 0.0;
            if (bResidualLimitSet == false)
            {
                return JUDGE_NOT_SET_TEXT;
            }
            if (bHasValue == false)
            {
                return "-";
            }
            if (dDistanceMm <= dResidualLimit)
            {
                return JUDGE_OK_TEXT;
            }
            return JUDGE_OUT_TEXT;
        }

        private void UpdateSeatSection(AlignVerifyQueryResult result)
        {
            if (result.HasMaterialSeat == false)
            {
                SeatValueText = "-";
                SeatJudgeText = ResolveSeatJudge(false, 0.0);
                return;
            }

            if (result.MaterialSeatHasResolution == false)
            {
                // 해상도 0 을 곱해 0mm 로 보여주면 "편차 없음" 으로 오독된다.
                SeatValueText = "환산 불가(px 만) — 해상도 미상";
                SeatJudgeText = "-";
                return;
            }

            SeatValueText = result.MaterialSeatDeviationMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm";
            SeatJudgeText = ResolveSeatJudge(true, result.MaterialSeatDeviationMm);
        }

        private string ResolveSeatJudge(bool bHasValue, double dDeviationMm)
        {
            double dSeatLimit = SystemHandler.Handle.Setting.AlignVerifySeatLimitMm;
            bool bSeatLimitSet = dSeatLimit > 0.0;
            if (bSeatLimitSet == false)
            {
                return JUDGE_NOT_SET_TEXT;
            }
            if (bHasValue == false)
            {
                return "-";
            }
            if (dDeviationMm <= dSeatLimit)
            {
                return JUDGE_OK_TEXT;
            }
            return JUDGE_OUT_TEXT;
        }

        private void UpdateConclusion(AlignVerifyQueryResult result, int nMaterialNo)
        {
            double dResidualLimit = SystemHandler.Handle.Setting.AlignVerifyResidualLimitMm;
            double dSeatLimit = SystemHandler.Handle.Setting.AlignVerifySeatLimitMm;
            bool bResidualLimitSet = dResidualLimit > 0.0;
            bool bSeatLimitSet = dSeatLimit > 0.0;

            if (bResidualLimitSet == false || bSeatLimitSet == false)
            {
                LimitNoticeText = "판정 임계값이 설정되지 않았습니다. 실측 산포가 쌓인 뒤 설정에서 지정하세요.";
                ConclusionText = "판정 기준이 설정되지 않아 결론을 내지 않습니다. (설정 → Path|AlignVerify)";
                return;
            }

            LimitNoticeText = "";

            bool bNoData = (nMaterialNo >= 0) && (result.HasMaterialAlign == false) && (result.HasMaterialSeat == false);
            if (bNoData)
            {
                ConclusionText = "해당 자재번호의 기록이 없습니다";
                return;
            }

            bool bAlignOut = result.HasMaterialAlign && (result.MaterialAlignDistanceMm > dResidualLimit);
            if (bAlignOut)
            {
                ConclusionText = "Align 계산 자체가 기준에 못 미칩니다 (비전 쪽 점검)";
                return;
            }

            bool bSeatOut = result.HasMaterialSeat && result.MaterialSeatHasResolution
                         && (result.MaterialSeatDeviationMm > dSeatLimit);
            if (bSeatOut)
            {
                ConclusionText = "비전은 맞게 줬는데 놓는 위치가 틀어졌습니다 (피커 쪽 점검)";
                return;
            }

            ConclusionText = JUDGE_OK_TEXT;
        }

        private void UpdateTrend(AlignVerifyQueryResult result)
        {
            TrendAlignText = "① Align 계산   평균 "
                           + result.TrendAlignAvgMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm   최대 "
                           + result.TrendAlignMaxMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm   (n="
                           + result.TrendAlignCount.ToString(CultureInfo.InvariantCulture) + ")";

            TrendSeatText = "② 안착 위치   평균 "
                          + result.TrendSeatAvgMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm   최대 "
                          + result.TrendSeatMaxMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm   (n="
                          + result.TrendSeatCount.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private void UpdateSeatStats(AlignVerifyQueryResult result)
        {
            SeatStats.Clear();
            foreach (AlignVerifySeatStat stat in result.SeatStats)
            {
                SeatStats.Add(stat);
            }
        }

        private void UpdateDetailRows(AlignVerifyQueryResult result)
        {
            DetailRows.Clear();
            foreach (AlignVerifyRecord rec in result.MaterialRows)
            {
                AlignVerifyRowView row = new AlignVerifyRowView();
                row.TimeText = rec.RecordTime.ToString("yyyy-MM-dd HH:mm:ss");
                row.KindText = rec.Kind;
                row.JudgeText = rec.Judgement;
                row.ImageFileText = rec.ImageFileName;

                bool bIsAlign = string.Equals(rec.Kind, AlignVerifyRecord.KIND_ALIGN, StringComparison.Ordinal);
                if (bIsAlign)
                {
                    row.TargetOrSequenceText = rec.Target;
                    row.SlotOrDatumText = rec.SlotToken;
                    row.ValueText = BuildAlignRowValue(rec);
                }
                else
                {
                    row.TargetOrSequenceText = rec.SequenceName;
                    row.SlotOrDatumText = rec.DatumName;
                    row.ValueText = BuildSeatRowValue(rec);
                }

                DetailRows.Add(row);
            }
        }

        private string BuildAlignRowValue(AlignVerifyRecord rec)
        {
            if (rec.HasResidual == false)
            {
                return rec.FailReason;
            }
            double dDist = AlignVerifyCsvLoader.ComputeAlignDistanceMm(rec);
            return dDist.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm / theta "
                 + rec.ResidualThetaDeg.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + "°";
        }

        private string BuildSeatRowValue(AlignVerifyRecord rec)
        {
            double dPx, dMm;
            bool bHasMm = AlignVerifyCsvLoader.TryComputeSeatDeviation(rec, out dPx, out dMm);
            if (bHasMm)
            {
                return dMm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture) + " mm";
            }
            return "환산 불가(px 만): " + dPx.ToString(PX_FORMAT, CultureInfo.InvariantCulture) + "px";
        }
    }
}
