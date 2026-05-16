//260413 hbk Phase 6: Multi-Algorithm 구조를 위한 Measurement 추상 기반 클래스 (D-14)
using System;
using System.Collections.Generic; //260422 hbk Phase 7: List<T> (D-01)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Models; //260422 hbk Phase 7: EdgeInspectionOverlay (D-01)

namespace ReringProject.Sequence
{
    /// <summary>
    /// FAI 하위에서 실행되는 개별 측정의 추상 기반 클래스.
    /// 파생 클래스가 측정 유형별 ROI/알고리즘을 구현하고 TryExecute로 결과(mm 또는 deg)를 반환한다.
    /// DatumRef는 빈 문자열이면 무보정(D-10 하위 호환), 그 외에는 Sequence 레벨에서 해석된다.
    /// </summary>
    public abstract class MeasurementBase : ParamBase //260413 hbk
    {
        [Category("Measurement|Reference")]
        public string DatumRef { get; set; } = ""; //260413 hbk 빈 문자열=무보정

        [Category("Measurement|Info")]
        public string MeasurementName { get; set; } = ""; //260413 hbk

        [Category("Measurement|Tolerance")]
        public double NominalValue { get; set; } //260413 hbk

        [Category("Measurement|Tolerance")]
        public double TolerancePlus { get; set; } //260413 hbk

        [Category("Measurement|Tolerance")]
        public double ToleranceMinus { get; set; } //260413 hbk

        [PropertyTools.DataAnnotations.Browsable(false)]
        public double LastMeasuredValue { get; set; } //260413 hbk 휘발성, INI 저장 제외

        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastJudgement { get; set; } //260413 hbk true=OK

        //260517 hbk CO-23-01: HasResult 판정 기준 분리 — 0.0 결과값도 정상 측정으로 표시하기 위해
        //  LastMeasuredValue != 0 대신 별도 플래그 사용. EvaluateJudgement에서 true, ClearResult에서 false 설정.
        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastHasResult { get; set; } //260517 hbk CO-23-01

        [PropertyTools.DataAnnotations.Browsable(false)]
        public abstract string TypeName { get; } //260413 hbk MeasurementFactory 키

        protected MeasurementBase(object owner) : base(owner) { } //260413 hbk

        /// <summary>
        /// 측정을 실행한다. datumTransform은 DatumFindingService.TryFindDatum 결과(hom_mat2d)로
        /// null/empty이면 identity. 결과 단위는 측정 유형별(길이=mm, 각도=deg).
        /// </summary>
        public abstract bool TryExecute( //260413 hbk //260422 hbk Phase 7: out overlays 추가 (D-01)
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays);

        /// <summary>
        /// 공차 판정: lower = Nominal + ToleranceMinus (음수 허용), upper = Nominal + TolerancePlus.
        /// LastMeasuredValue/LastJudgement/LastHasResult를 갱신하고 결과를 반환한다.
        /// </summary>
        public bool EvaluateJudgement(double value) //260413 hbk
        {
            LastMeasuredValue = value;
            LastHasResult = true; //260517 hbk CO-23-01: 측정 성공 마킹 (0.0 도 정상 결과)
            double lower = NominalValue + ToleranceMinus;
            double upper = NominalValue + TolerancePlus;
            if (lower > upper)
            {
                double tmp = lower; lower = upper; upper = tmp;
            }
            LastJudgement = (value >= lower) && (value <= upper);
            return LastJudgement;
        }

        public void ClearResult() //260413 hbk
        {
            LastMeasuredValue = 0;
            LastJudgement = false;
            LastHasResult = false; //260517 hbk CO-23-01: 미측정 상태 복원
        }
    }
}
