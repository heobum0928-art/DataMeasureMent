//260624 hbk Phase 59
namespace ReringProject {

    /// <summary>
    /// D-04 레퍼런스 포즈 사이드카 JSON 스키마. 티칭 시 .shm 옆에 저장, Run() 에서 로드하여 offset = cur − ref 산출.
    /// 순수 POCO — Newtonsoft.Json (TypeNameHandling.None) 직렬화 대상.
    /// </summary>
    public class AlignRefPose {

        /// <summary>티칭 이미지에서 find 한 레퍼런스 매칭 row.</summary>
        public double RefRow { get; set; }

        /// <summary>티칭 이미지에서 find 한 레퍼런스 매칭 col.</summary>
        public double RefCol { get; set; }

        /// <summary>레퍼런스 매칭 각도(deg). Bottom Theta 기준.</summary>
        public double RefAngleDeg { get; set; }

        /// <summary>티칭 시 사용한 모드별 angle extent(deg). 진단/재현용 기록.</summary>
        public double AngleExtentDeg { get; set; }

        /// <summary>모델 엔진명("Shape"). 진단/검증용.</summary>
        public string Engine { get; set; }
    }
}
