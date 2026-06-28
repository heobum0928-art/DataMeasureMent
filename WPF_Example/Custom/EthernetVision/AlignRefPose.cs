//260624 hbk Phase 59 revision — 2-pattern + angle_lx baseline
namespace ReringProject {

    /// <summary>
    /// D-04' 레퍼런스 포즈 사이드카 JSON 스키마 (2-pattern 개정).
    /// 티칭 시 .shm 옆에 저장, Run() 에서 로드하여 offset/theta 산출.
    /// Ref1=TL 중심, Ref2=BR 중심, RefBaselineRad=angle_lx(Ref1,Ref2).
    /// 순수 POCO — Newtonsoft.Json (TypeNameHandling.None) 직렬화 대상.
    /// </summary>
    public class AlignRefPose {

        /// <summary>티칭 이미지에서 find 한 TL 모델 레퍼런스 row.</summary>
        public double Ref1Row { get; set; }

        /// <summary>티칭 이미지에서 find 한 TL 모델 레퍼런스 col.</summary>
        public double Ref1Col { get; set; }

        /// <summary>티칭 이미지에서 find 한 BR 모델 레퍼런스 row.</summary>
        public double Ref2Row { get; set; }

        /// <summary>티칭 이미지에서 find 한 BR 모델 레퍼런스 col.</summary>
        public double Ref2Col { get; set; }

        /// <summary>티칭 시 두 중심 사이 angle_lx(rad). Bottom Theta 기준 baseline.</summary>
        public double RefBaselineRad { get; set; }

        /// <summary>티칭 시 사용한 모드별 angle extent(deg). 진단/재현용 기록.</summary>
        public double AngleExtentDeg { get; set; }

        /// <summary>모델 엔진명("Shape"). 진단/검증용.</summary>
        public string Engine { get; set; }

        //260625 hbk Phase 61.1 F2 — 티칭 ROI 크기(반폭: Len1=Col 반폭, Len2=Row 반폭). 보정 ROI 박스 실제 크기 표시용. 구 레시피엔 0 → 60px 폴백.
        /// <summary>TL(ROI1) Col 반폭.</summary>
        public double Roi1Len1 { get; set; }
        /// <summary>TL(ROI1) Row 반폭.</summary>
        public double Roi1Len2 { get; set; }
        /// <summary>BR(ROI2) Col 반폭.</summary>
        public double Roi2Len1 { get; set; }
        /// <summary>BR(ROI2) Row 반폭.</summary>
        public double Roi2Len2 { get; set; }

        //260626 hbk Phase 66 D-05 — Align 동축 조명 저장. 키 부재(구 JSON) → false/0 자동 폴백(하위호환).
        /// <summary>Align 동축 조명 ON/OFF. 저장 시점 슬롯/Tray 설정값.</summary>
        public bool CoaxEnabled { get; set; }
        /// <summary>Align 동축 밝기 0~255. 저장 시점 설정값.</summary>
        public int CoaxLevel { get; set; }
    }
}
