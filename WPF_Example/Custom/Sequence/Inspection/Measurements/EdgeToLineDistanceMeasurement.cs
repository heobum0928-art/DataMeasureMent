using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI 에서 에지 라인을 피팅해 중점을 추출하고, datum 기준선까지의 거리(mm)를 리턴한다.
    /// MeasureAxis="Y": datum 수평선(x축)까지 수직거리 — +Y 위쪽 양수(D-02). 수평 에지 검출용.
    /// MeasureAxis="X": datum 수직선(y축)까지 거리 — +X 오른쪽 양수. 수직 에지 검출용.
    /// datum 기준선은 교점(DatumOriginRow/Col)을 지나며 각도는 DatumAngleRad(수평선 θ, 수직선 θ+90°).
    /// HALCON projection_pl 로 에지 중점을 기준선에 정사영해 수선의 발을 구하고 거리를 계산한다.
    /// 결과 단위: mm (pixelResolution 적용). Datum 1개(CTH) 가정 (D-01).
    /// </summary>
    public class EdgeToLineDistanceMeasurement : MeasurementBase,
        IDatumOriginConsumer
    {
        public override string TypeName { get { return "EdgeToLineDistance"; } }

        [Category("Point|ROI")]
        public double Point_Row { get; set; }
        public double Point_Col { get; set; }
        public double Point_Phi { get; set; }
        public double Point_Length1 { get; set; }
        public double Point_Length2 { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))] // default TtoB: 수평 에지 검출, Y거리 측정 의도
        public string EdgeDirection { get; set; } = "TtoB";
        // EdgeSelection 기본값 "All": EdgeToLineDistance 는 ROI 내 에지 분포 전체를 라인으로
        //  피팅해 중점을 구하므로 "All" 이 의미적으로 올바른 기본값. selection="first" 는 에지점 1개만
        //  반환 → FitLineContourXld 최소 2점 요구 미충족 → 측정 실패(UI '—').
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeSelection { get; set; } = "All";

        // PropertyGrid ComboBox 옵션 래퍼 (Browsable(false) 로 자체 노출 차단)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        // 측정 거리 축 선택: datum 어느 기준선까지의 거리를 잴지.
        //  "Y" = datum 수평선(x축)까지 수직거리 (+Y 위쪽 양수, D-02) — 수평 에지 측정용.
        //  "X" = datum 수직선(y축)까지 거리 (+X 오른쪽 양수) — 수직 에지 측정용.
        [Category("Edge")]
        [System.ComponentModel.Description("측정 거리 축 — Y: datum 수평선까지, X: datum 수직선까지")]
        [ItemsSourceProperty(nameof(MeasureAxisList))]
        public string MeasureAxis { get; set; } = "Y";
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } }

        // datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement 가 TryExecute 직전 주입).
        //  DatumConfig.DetectedOrigin* 패턴과 동일: 런타임 transient, PropertyGrid 미표시, JSON 직렬화 제외.
        //  ParamBase INI reflection 은 public double 을 0 으로 직렬화하나 DatumConfig 와 동일하게 수용.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginRow { get; set; } // datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginCol { get; set; } // datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngleRad { get; set; } // datum 1차(수평) 기준선 각도(rad). 미주입 시 0.
        // IDatumOriginConsumer 2차 각도. 인라인 투영은 현행 유지(carry-over), 속성만 구현 —
        //  X축 = 실제 datum 수직선 전환은 후속 carry-over.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; }
        // IDatumOriginConsumer 확장. 본 타입은 검출 원중심 미사용 (E2 만 사용) — 주입만 받고 미참조.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        public EdgeToLineDistanceMeasurement(object owner) : base(owner) { }

        public override bool TryExecute(
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            // 실패 경로용 초기값 (성공 경로는 아래에서 채움)
            overlays = new List<EdgeInspectionOverlay>();

            // D-11 Datum 찾기 실패 가드 (upstream gating 은 보조 이중 안전망)
            if (datumTransform == null || datumTransform.Length == 0)
            {
                error = "Datum not found";
                return false;
            }

            var svc = new VisionAlgorithmService();
            double pr1, pc1, pr2, pc2;
            // strip-loop(stripCount 기본 20)가 First/Last 도 strip 마다 1점씩 누적 → 라인 피팅 충분.
            //  edgeCount<2 안전 가드는 VisionAlgorithmService.TryFitLine 에 유지.
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                EdgeSelection))
            {
                return false;
            }
            double pRow = (pr1 + pr2) / 2.0;
            double pCol = (pc1 + pc2) / 2.0;

            // 측정값 = 에지 중점에서 datum 기준선에 내린 수선의 길이 (HALCON projection_pl).
            //  MeasureAxis="Y": datum 수평선(x축, 각도 θ)까지 수직거리 — D-02 +Y 위쪽 양수.
            //  MeasureAxis="X": datum 수직선(y축, 각도 θ+90°)까지 거리 — +X 오른쪽 양수.
            //  datum 기준선은 교점(DatumOriginRow/Col)을 지나고 각도는 DatumAngleRad(=DatumConfig.DetectedRefAngle,
            //  수평 결합선 Atan2(Δrow,Δcol)). 단순 row 차분은 datum 회전 시 수직거리와 불일치 → projection_pl 정사영 사용.
            //  레거시/무보정(datum 미주입) 폴백은 아래 else 블록(AffineTransPoint2d, Y 기준).
            bool datumOriginInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);
            double footRow = pRow; // projection foot. 미주입/실패 시 에지점 자신(거리 0). overlay 에서 재사용.
            double footCol = pCol;
            bool footOk = false;
            if (datumOriginInjected) // 정상 경로: projection_pl 로 datum 기준선까지 수직거리
            {
                // measureX 만 DatumAngle2Rad 사용 (실 datum 수직 기준선 각도). measureY 는 DatumAngleRad 유지.
                //  근본 원인: DatumAngleRad+90° 가정은 DetectedRefAngle2 ≠ DetectedRefAngle+90° 일 때 정사영 오차.
                //  폴백: DatumAngle2Rad==0 (TLI Datum 등 2차 기준선 미주입) 시 기존 로직 유지.
                bool measureX = (MeasureAxis == "X"); // null/""/"Y" → false (레거시 INI·미설정 안전)
                bool useAngle2 = measureX && (DatumAngle2Rad != 0.0);
                double angleSrc;
                if (useAngle2)
                {
                    angleSrc = DatumAngle2Rad; // 실 수직 기준선
                }
                else
                {
                    angleSrc = DatumAngleRad; // 1차 수평선
                }
                double sinT = System.Math.Sin(angleSrc);
                double cosT = System.Math.Cos(angleSrc);
                if (cosT < 0.0) { sinT = -sinT; cosT = -cosT; } // Atan2 방향 무관 부호 일관성: datum x축 cosθ≥0 정규화
                double axisR1, axisC1, axisR2, axisC2; // projection_pl 대상 직선의 2점 (교점 ±200px, 길이는 직선 정의에 무관)
                if (measureX) // datum 수직선(y축)
                {
                    if (useAngle2) // 실 수직 기준선: 방향벡터 (sinθ2,cosθ2) — measureY 와 같은 공식 (각도만 θ→θ2)
                    {
                        axisR1 = DatumOriginRow - 200.0 * sinT;
                        axisC1 = DatumOriginCol - 200.0 * cosT;
                        axisR2 = DatumOriginRow + 200.0 * sinT;
                        axisC2 = DatumOriginCol + 200.0 * cosT;
                    }
                    else // 폴백: 가상 수직선 (DatumAngleRad+90° 가정)
                    {
                        axisR1 = DatumOriginRow - 200.0 * cosT;
                        axisC1 = DatumOriginCol + 200.0 * sinT;
                        axisR2 = DatumOriginRow + 200.0 * cosT;
                        axisC2 = DatumOriginCol - 200.0 * sinT;
                    }
                }
                else // datum 수평선(x축): 방향벡터 (sinθ,cosθ), 각도 θ
                {
                    axisR1 = DatumOriginRow - 200.0 * sinT;
                    axisC1 = DatumOriginCol - 200.0 * cosT;
                    axisR2 = DatumOriginRow + 200.0 * sinT;
                    axisC2 = DatumOriginCol + 200.0 * cosT;
                }
                try
                {
                    HTuple prRow, prCol;
                    HOperatorSet.ProjectionPl(pRow, pCol, axisR1, axisC1, axisR2, axisC2, out prRow, out prCol); // 에지 중점을 datum 기준선에 정사영
                    footRow = prRow.D;
                    footCol = prCol.D;
                    footOk = true;
                }
                catch
                {
                    // projection 실패 시 foot=에지점 유지 → 측정값 0, FAI-DistLine skip
                }
                double signedPx; // 수선의 발→에지점 변위의 부호 있는 거리 성분
                if (measureX) // +X 오른쪽 양수
                {
                    if (useAngle2) // axis (sinθ2,cosθ2) 의 우측 법선 (cosθ2,-sinθ2)
                    {
                        signedPx = (pRow - footRow) * cosT - (pCol - footCol) * sinT;
                    }
                    else // 폴백: (sinθ, cosθ) 공식 (datum x축 방향 성분)
                    {
                        signedPx = (pRow - footRow) * sinT + (pCol - footCol) * cosT;
                    }
                }
                else // +Y 위쪽 양수(D-02): datum x축 up-normal (-cosθ,sinθ) 성분
                {
                    signedPx = (pRow - footRow) * (-cosT) + (pCol - footCol) * sinT;
                }
                resultValue = signedPx * pixelResolution;
            }
            else // 레거시/무보정 폴백: AffineTransPoint2d (DatumRef 빈 문자열 또는 구버전 호출 경로)
            {
                // Datum-relative Y 좌표 추출 + D-02 부호 반전 (image row → +Y 위쪽 양수)
                double datumRow = pRow;
                try
                {
                    HTuple tRow, tCol;
                    HOperatorSet.AffineTransPoint2d(datumTransform, pRow, pCol, out tRow, out tCol);
                    datumRow = tRow.D;
                }
                catch
                {
                    // transform 실패 시 image-row 좌표 사용 (TryFitLine 패턴 일관성)
                }
                resultValue = -datumRow * pixelResolution; // D-02 +Y 부호 (위쪽 양수)
            }

            // UAT 시각 검증(측정값 vs SOP 도면 정확도)을 위해 검출 에지/거리선을 캔버스에 표시.

            // 1) 검출 에지 라인 overlay (FAIEdgeMeasurementService.BuildOverlaysSingle 패턴)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1", // StartsWith("FAI-Edge") 충족 → HalconDisplayService 녹/적 분기 + Action_FAIMeasurement 판정 suffix(-OK/-NG) 자동 부여
                LineRow1 = pr1,
                LineColumn1 = pc1,
                LineRow2 = pr2,
                LineColumn2 = pc2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = pRow, Column = pCol } // 에지 중점 1개 (위에서 계산됨)
                }
            });

            // 2) 수직 드롭선 overlay: 수선의 발(projection foot, datum 기준선 위) → 에지 중점
            bool originOk = false;
            double originRow = 0.0;
            double originCol = 0.0;
            if (datumOriginInjected) // 정상 경로: FAI-DistLine = 에지점→수선의 발 (수직 드롭)
            {
                originRow = footRow; // projection foot (datum 기준선 위의 점). HomMat2dInvert 불필요.
                originCol = footCol;
                originOk = footOk; // projection 실패 시 false → FAI-DistLine skip
            }
            else // 레거시/무보정 폴백: HomMat2dInvert 경로 (datum 미주입 케이스에서 overlay 완전 소실 방지)
            {
                try
                {
                    HTuple invMat;
                    HOperatorSet.HomMat2dInvert(datumTransform, out invMat); // datumTransform 역행렬: image→datum 역 = datum→image
                    HTuple oRow, oCol;
                    HOperatorSet.AffineTransPoint2d(invMat, 0.0, 0.0, out oRow, out oCol); // datum 원점(0,0)의 image 좌표
                    originRow = oRow.D;
                    originCol = oCol.D;
                    originOk = true;
                }
                catch
                {
                    // 역변환 실패 시 FAI-DistLine 만 skip — 에지 라인 overlay 와 측정값은 유지
                }
            }

            if (originOk)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine", // HalconDisplayService cyan(청록) 분기 충족, suffix 미부여
                    LineRow1 = originRow, // 수선의 발 (projection foot, datum 기준선 위)
                    LineColumn1 = originCol,
                    LineRow2 = pRow, // 에지 중점 image 좌표
                    LineColumn2 = pCol,
                    Points = new List<EdgeInspectionPoint> // 양 끝점 X자 마커 (BuildOverlaysBoth FAI-DistLine 패턴)
                    {
                        new EdgeInspectionPoint { Row = originRow, Column = originCol },
                        new EdgeInspectionPoint { Row = pRow, Column = pCol }
                    }
                });
            }

            return true;
        }
    }
}
