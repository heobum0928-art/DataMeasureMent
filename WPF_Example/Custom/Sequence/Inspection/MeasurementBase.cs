using System;
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Models;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    /// <summary>
    /// FAI 하위에서 실행되는 개별 측정의 추상 기반 클래스.
    /// 파생 클래스가 측정 유형별 ROI/알고리즘을 구현하고 TryExecute로 결과(mm 또는 deg)를 반환한다.
    /// DatumRef는 빈 문자열이면 무보정(하위 호환), 그 외에는 Sequence 레벨에서 해석된다.
    /// </summary>
    public abstract class MeasurementBase : ParamBase
    {
        [Category("Measurement|Reference")]
        public string DatumRef { get; set; } = ""; // 빈 문자열=무보정

        // INotifyPropertyChanged 발화로 트리 헤더 즉시 갱신 (PropertyGrid 편집 → Tree, TypeName 폴백은 NodeViewModel.OnParamPropertyChanged 처리)
        private string _measurementName = "";
        [Category("Measurement|Info")]
        public string MeasurementName {
            get { return _measurementName; }
            set {
                if (_measurementName == value) return;
                _measurementName = value;
                RaisePropertyChanged(nameof(MeasurementName));
            }
        }

        [Category("Measurement|Tolerance")]
        public double NominalValue { get; set; }

        // 측정별 보정계수 — 비전측정값을 현미경 공칭에 트루업하는 곱셈 계수. per-Shot CorrectionFactor(전역 캘리브 간극)
        //  위에 한 겹 더 얹는 피처별 잔차 보정. 기본 1.0 = 무보정. 각도 측정 타입은 AppliesCorrectionFactor=false 로 미적용.
        //  ※ 반복성이 확보된 측정에만, 여러 부품 비전↔현미경 상관으로 뽑은 값을 넣을 것(1개로 뽑거나 산포 은폐 금지).
        //  운용 결정(사용자): 보정은 Shot 계수(CorrectionFactor) 단일 레이어로 관리하고, 안 맞는 포인트는 별도 Shot 으로 분리한다.
        //   → 이 측정별 계수는 PropertyGrid 에서 숨긴다([Browsable(false)]). 값/직렬화/EvaluateJudgement 적용 로직은 보존(전부 1.0=무보정,
        //     측정값 변화 0). 다시 노출하려면 [Browsable(false)] 만 제거하면 됨.
        [Category("Measurement|Tolerance")]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [System.ComponentModel.Description("측정별 보정계수(×). 비전측정 → 현미경 공칭 정합용. 1.0=무보정. 각도 측정엔 미적용(길이/거리/직경만).")]
        public double MeasCorrectionFactor { get; set; } = 1.0;

        // 이 측정 유형에 MeasCorrectionFactor(길이 스케일 보정)를 적용하는지. 각도 타입은 false 로 override(각도는 비율).
        [PropertyTools.DataAnnotations.Browsable(false)]
        protected virtual bool AppliesCorrectionFactor { get { return true; } }

        [Category("Measurement|Tolerance")]
        [System.ComponentModel.Description("상한 공차. 부호 무관하게 입력 (절대값 적용). 비대칭 공차 지원.")]
        public double TolerancePlus { get; set; }

        [Category("Measurement|Tolerance")]
        [System.ComponentModel.Description("하한 공차. 부호 무관하게 입력 (절대값 적용). 비대칭 공차 지원.")]
        public double ToleranceMinus { get; set; }

        //260616 hbk Phase 51 UAT: 절대값 판정/표시. 켜면 측정값을 |값|으로 처리 — 부호(방향)만 다른 경우 NG 오판 방지.
        //  예) 측정 -24, Nominal 24 → |-24|=24 로 판정·표시되어 OK. 기본 false = 기존 동작(회귀 0, INI 미존재 시 폴백 false).
        [Category("Measurement|Tolerance")]
        [System.ComponentModel.Description("절대값 판정. 켜면 측정값의 부호를 무시하고 |값|으로 판정·표시한다 (방향만 반대인 경우 NG 오판 방지).")]
        public bool UseAbsoluteValue { get; set; } = false;

        //260616 hbk Phase 51 UAT: 부호 반전. 켜면 측정값 부호를 뒤집어(-value) 판정·표시 — signed 거리에서 정상 쪽을 +로 읽음.
        //  절대값과 달리 반대쪽 오검출은 -로 남아 NG 로 잡힘(불량 은폐 안 함). 기본 false = 기존 동작(회귀 0, INI 미존재 시 폴백 false).
        [Category("Measurement|Tolerance")]
        [System.ComponentModel.Description("부호 반전. 켜면 측정값의 부호를 뒤집어(-value) 판정·표시한다. signed 거리에서 정상 쪽을 양수로 읽되 반대쪽 오검출은 음수로 남아 NG로 잡힌다.")]
        public bool InvertSign { get; set; } = false;

        [PropertyTools.DataAnnotations.Browsable(false)]
        public double LastMeasuredValue { get; set; } // 휘발성, INI 저장 제외

        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastJudgement { get; set; } // true=OK

        // HasResult 판정 기준 분리 — 0.0 결과값도 정상 측정으로 표시하기 위해
        //  LastMeasuredValue != 0 대신 별도 플래그 사용. EvaluateJudgement에서 true, ClearResult에서 false 설정.
        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastHasResult { get; set; }

        // measurement 단위 skip reason. null/"" = 정상 또는 측정 NG, "DATUM_FAIL" = datum 검출 실패로 skip.
        //  string 채택 근거: enum 신설 시 INI/직렬화 영향 검토 필요. string 은 ParamBase reflection 의 case "String" 경로 자동 호환.
        [PropertyTools.DataAnnotations.Browsable(false)]
        public string LastSkipReason { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public abstract string TypeName { get; } // MeasurementFactory 키

        protected MeasurementBase(object owner) : base(owner) { }

        /// <summary>
        /// 측정을 실행한다. datumTransform은 DatumFindingService.TryFindDatum 결과(hom_mat2d)로
        /// null/empty이면 identity. 결과 단위는 측정 유형별(길이=mm, 각도=deg).
        /// </summary>
        public abstract bool TryExecute(
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays);

        /// <summary>
        /// 공차 판정: lower = Nominal - Abs(ToleranceMinus), upper = Nominal + Abs(TolerancePlus).
        /// 공차 입력 부호와 무관하게 NominalValue 중심의 올바른 범위를 적용한다.
        /// LastMeasuredValue/LastJudgement/LastHasResult를 갱신하고 결과를 반환한다.
        /// </summary>
        public bool EvaluateJudgement(double value)
        {
            // 측정별 보정계수(현미경 정합 트루업) — 곱셈, 길이 타입만(각도 override 제외). 부호/절대값·판정·표시 전에 적용해
            //  LastMeasuredValue 가 보정값으로 기록되게 한다. 계수 ≤0(구 레시피 잔재 등)은 무보정(1.0)으로 안전 처리.
            if (AppliesCorrectionFactor && MeasCorrectionFactor > 0.0)
            {
                value = value * MeasCorrectionFactor;
            }
            //260616 hbk Phase 51 UAT: 부호 보정 — 반전(-value) 후 절대값(|value|) 순. signed 거리/방향 규약 맞춤. LastMeasuredValue 도 보정값으로 기록(표시/Export 일관).
            if (InvertSign) value = -value;
            if (UseAbsoluteValue) value = System.Math.Abs(value);
            LastMeasuredValue = value;
            LastHasResult = true; // 측정 성공 마킹 (0.0 도 정상 결과)
            double lower = NominalValue - System.Math.Abs(ToleranceMinus); // 부호 무관 절대값 처리
            double upper = NominalValue + System.Math.Abs(TolerancePlus);
            if (lower > upper)
            {
                double tmp = lower; lower = upper; upper = tmp;
            }
            LastJudgement = (value >= lower) && (value <= upper);
            return LastJudgement;
        }

        public void ClearResult()
        {
            LastMeasuredValue = 0;
            LastJudgement = false;
            LastHasResult = false; // 미측정 상태 복원
            LastSkipReason = null; // datum-skip subtype 리셋
        }

        // 하위호환: ParamBase.Load 는 INI 누락 double 키를 0 으로 덮어쓴다. 구 레시피엔 MeasCorrectionFactor 키가 없어
        //  0 으로 로드되면 EvaluateJudgement 에서 value×0=0 → 전 측정 0/NG(회귀). 키 부재 시에만 1.0(무보정) 복원한다.
        //  (CameraSlaveParam.Load 의 CorrectionFactor 복원과 동일 패턴. 키 존재=사용자 설정값이면 그대로 둠.)
        public override bool Load(IniFile loadFile, string groupName)
        {
            bool result = base.Load(loadFile, groupName);
            IniSection sec;
            if (!loadFile.TryGetSection(groupName, out sec) || sec == null || !sec.ContainsKey("MeasCorrectionFactor"))
            {
                MeasCorrectionFactor = 1.0;
            }
            return result;
        }
    }
}
