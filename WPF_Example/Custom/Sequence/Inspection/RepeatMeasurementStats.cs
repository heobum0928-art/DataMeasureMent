//260612 hbk Phase 41.1 OUT-03 반복도 통계 계산
using System;
using System.Collections.Generic;
using System.Linq;
using ReringProject.UI;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 반복 측정 결과 1행 데이터. Shot/FAI/측정명 키에 대한 통계값을 보유한다.
    /// RepeatMeasurementStats.ComputeAll() 이 반환하는 Dictionary 의 값 타입.
    /// </summary>
    public class MeasurementStat
    {
        public string ShotName { get; set; }
        public string FAIName { get; set; }
        public string MeasurementName { get; set; }
        public string TypeName { get; set; }
        public int N { get; set; }
        public double Mean { get; set; }
        public double StdDev { get; set; }
        public double Range { get; set; }
        public double Cpk { get; set; }
        public double NominalValue { get; set; }
        public double TolerancePlus { get; set; }
        public double ToleranceMinus { get; set; }
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public int DetectFailCount { get; set; }
    }

    /// <summary>
    /// 복수 CycleResultDto 샘플을 누적하여 MeasurementKey 별 통계(Mean/StdDev/Range/Cpk)를 계산한다.
    /// OUT-03 반복도 통계 xlsx 의 데이터 계층. AddSample() → ComputeAll() 순서로 호출한다.
    /// </summary>
    public class RepeatMeasurementStats
    {
        private class KeyData
        {
            public string ShotName;
            public string FAIName;
            public string MeasurementName;
            public string TypeName;
            public List<double> Values = new List<double>();
            public int OkCount;
            public int NgCount;
            public int DetectFailCount;
            public double LastNominal;
            public double LastTolPlus;
            public double LastTolMinus;
        }

        private readonly Dictionary<string, KeyData> _data = new Dictionary<string, KeyData>();

        /// <summary>
        /// CycleResultDto 1회 결과를 내부 누적 버퍼에 추가한다.
        /// LastSkipReason=="DATUM_FAIL" 인 측정은 DetectFailCount 만 증가하고 값 목록에 미포함.
        /// LastHasResult=false 인 측정(비측정)은 무시한다.
        /// </summary>
        public void AddSample(CycleResultDto dto)
        {
            if (dto == null || dto.Shots == null)
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
                        if (m == null)
                        {
                            continue;
                        }

                        string key = (shot.ShotName ?? "") + "/" + (fai.FAIName ?? "") + "/" + (m.MeasurementName ?? "");

                        KeyData d;
                        if (!_data.TryGetValue(key, out d))
                        {
                            d = new KeyData
                            {
                                ShotName = shot.ShotName ?? "",
                                FAIName = fai.FAIName ?? "",
                                MeasurementName = m.MeasurementName ?? "",
                                TypeName = m.TypeName ?? ""
                            };
                            _data[key] = d;
                        }

                        // 마지막 공차값으로 항상 갱신 (최신 레시피 반영)
                        d.LastNominal = m.NominalValue;
                        d.LastTolPlus = m.TolerancePlus;
                        d.LastTolMinus = m.ToleranceMinus;

                        if (m.LastSkipReason == "DATUM_FAIL")
                        {
                            d.DetectFailCount++;
                        }
                        else if (m.LastHasResult)
                        {
                            d.Values.Add(m.LastMeasuredValue);
                            if (m.LastJudgement)
                            {
                                d.OkCount++;
                            }
                            else
                            {
                                d.NgCount++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 누적된 샘플로 각 MeasurementKey 의 통계를 계산하여 반환한다.
        /// Cpk = min((USL-mean)/(3σ), (mean-LSL)/(3σ)), σ=0 이면 PositiveInfinity.
        /// </summary>
        public Dictionary<string, MeasurementStat> ComputeAll()
        {
            var result = new Dictionary<string, MeasurementStat>();

            foreach (var kv in _data)
            {
                var d = kv.Value;
                int n = d.Values.Count;
                double mean = 0;
                double stddev = 0;
                double range = 0;
                double cpk = 0;

                if (n > 0)
                {
                    mean = d.Values.Sum() / n;

                    double minVal = d.Values[0];
                    double maxVal = d.Values[0];
                    double sumSq = 0;
                    foreach (var v in d.Values)
                    {
                        if (v < minVal)
                        {
                            minVal = v;
                        }

                        if (v > maxVal)
                        {
                            maxVal = v;
                        }

                        sumSq += (v - mean) * (v - mean);
                    }

                    range = maxVal - minVal;

                    if (n > 1)
                    {
                        stddev = Math.Sqrt(sumSq / (n - 1));
                    }

                    // Cpk = min((USL-mean)/(3*sigma), (mean-LSL)/(3*sigma))
                    double usl = d.LastNominal + d.LastTolPlus;
                    double lsl = d.LastNominal - Math.Abs(d.LastTolMinus);
                    if (stddev == 0)
                    {
                        cpk = double.PositiveInfinity;
                    }
                    else
                    {
                        double cpkUpper = (usl - mean) / (3 * stddev);
                        double cpkLower = (mean - lsl) / (3 * stddev);
                        cpk = Math.Min(cpkUpper, cpkLower);
                    }
                }

                result[kv.Key] = new MeasurementStat
                {
                    ShotName = d.ShotName,
                    FAIName = d.FAIName,
                    MeasurementName = d.MeasurementName,
                    TypeName = d.TypeName,
                    N = n,
                    Mean = mean,
                    StdDev = stddev,
                    Range = range,
                    Cpk = cpk,
                    NominalValue = d.LastNominal,
                    TolerancePlus = d.LastTolPlus,
                    ToleranceMinus = d.LastTolMinus,
                    OkCount = d.OkCount,
                    NgCount = d.NgCount,
                    DetectFailCount = d.DetectFailCount
                };
            }

            return result;
        }
    }
}
