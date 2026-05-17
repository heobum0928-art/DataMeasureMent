//260512 hbk Phase 23 ALG-01 — Datum-relative Y 거리 측정 (D-06)
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI 에서 수평 에지 라인을 피팅하여 중점을 추출하고,
    /// datumTransform 으로 Datum-relative 좌표계로 변환한 후 row 좌표(부호 반전 = +Y 위쪽 양수)를
    /// "Datum B 까지 Y방향 거리" (mm) 로 리턴한다.
    /// 결과 단위: mm (pixelResolution 적용).
    /// Datum 1개(CTH) 가정 — origin = Circle center, Y축 = horizontal line (D-01).
    /// </summary>
    public class EdgeToLineDistanceMeasurement : MeasurementBase //260512 hbk Phase 23 ALG-01
    {
        public override string TypeName { get { return "EdgeToLineDistance"; } } //260512 hbk Phase 23 ALG-01

        [Category("Point|ROI")] //260512 hbk Phase 23 ALG-01
        public double Point_Row { get; set; }
        public double Point_Col { get; set; }
        public double Point_Phi { get; set; }
        public double Point_Length1 { get; set; }
        public double Point_Length2 { get; set; }

        [Category("Edge")] //260512 hbk Phase 23 ALG-01
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260512 hbk Phase 23 ALG-01 — ComboBox 처리
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260512 hbk Phase 23 ALG-01 — ComboBox 처리, default TtoB (수평 에지 검출, Y거리 측정 의도)
        public string EdgeDirection { get; set; } = "TtoB";
        //260517 hbk CO-23-01: EdgeSelection 기본값 "All" 로 변경 (was "First").
        //  MeasurePos selection="first" 는 단일 에지점 1개 반환 → FitLineContourXld 최소 2점 요구 미충족 →
        //  TryFitLine false 반환 → TryExecute false → 측정 실패(하지만 Error 로그만 남고 UI = '—').
        //  EdgeToLineDistance 는 ROI 내 에지 분포 전체를 라인으로 피팅해 중점을 구하는 알고리즘이므로
        //  "All" 이 의미적으로 올바른 기본값이다.
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260512 hbk Phase 23 ALG-01 — D-10 EdgeSelection 명시 (memory feedback)
        public string EdgeSelection { get; set; } = "All"; //260517 hbk CO-23-01 (was "First")

        //260512 hbk Phase 23 ALG-01 — PropertyGrid ComboBox 옵션 래퍼 (Browsable(false) 로 자체 노출 차단)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        public EdgeToLineDistanceMeasurement(object owner) : base(owner) { } //260512 hbk Phase 23 ALG-01

        public override bool TryExecute( //260512 hbk Phase 23 ALG-01
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260512 hbk Phase 23 ALG-01 — PointToLineDistance 패턴 (빈 리스트, Phase 7-01 D-03)

            //260512 hbk Phase 23 ALG-01 — D-11 Datum 찾기 실패 가드 (literal 구현, upstream gating 은 보조 이중 안전망)
            if (datumTransform == null || datumTransform.Length == 0)
            {
                error = "Datum not found";
                return false;
            }

            var svc = new VisionAlgorithmService();
            double pr1, pc1, pr2, pc2;
            //260512 hbk Phase 23 ALG-01 — TryFitLine selection 인자 전달 (D-10 EdgeSelection 명시)
            //260517 hbk Phase 23.1 D-08 — EdgeSelection "All" 고정 (CO-23-01 #1 구조적 차단).
            //  FitLineContourXld 는 라인 피팅에 최소 2개 에지점 요구. "First"/"Last" 는 MeasurePos 가
            //  단일 에지점 1개만 반환 → 라인 피팅 실패 → TryFitLine false → 측정 실패(UI '—').
            //  ICustomTypeDescriptor(D-09)가 PropertyGrid 에서 EdgeSelection 을 숨겨도 레거시 INI 의
            //  EdgeSelection=First 값이 로드될 수 있으므로, TryExecute 는 EdgeSelection 필드를 무시하고
            //  무조건 리터럴 "All" 을 전달한다.
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                "All")) //260517 hbk Phase 23.1 D-08 (was: EdgeSelection)
            {
                return false;
            }
            double pRow = (pr1 + pr2) / 2.0;
            double pCol = (pc1 + pc2) / 2.0;

            //260512 hbk Phase 23 ALG-01 — Datum-relative Y 좌표 추출 + D-02 부호 반전 (image row → +Y 위쪽 양수)
            double datumRow = pRow;
            try
            {
                HTuple tRow, tCol;
                HOperatorSet.AffineTransPoint2d(datumTransform, pRow, pCol, out tRow, out tCol);
                datumRow = tRow.D;
            }
            catch
            {
                // transform 실패 시 image-row 좌표 사용 (TryFitLine 패턴 일관성, RESEARCH Pitfall 2)
            }
            resultValue = -datumRow * pixelResolution; //260512 hbk Phase 23 ALG-01 — D-02 +Y 부호 (위쪽 양수)
            return true;
        }
    }
}
