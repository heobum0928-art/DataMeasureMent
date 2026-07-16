using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// EdgeA1/EdgeB1(좌 교점용) 과 EdgeA2/EdgeB2(우 교점용) 4개 ROI 에서 각각 직선을 피팅해 좌 교점(int1)/우 교점(int2)을 산출한다.
    /// 좌·우 교점을 잇는 직선(L_cross)과 datum 기준선(L_datum, 휜 각도 반영)의 교차점 P 를 구하고,
    /// 최종 측정값 = P 와 우측 교점(int2) 사이의 거리(mm). 오른쪽=양수.
    /// (좌 교점이 L_cross 기울기→P 위치에 실제 기여. datum 이 휘어도 L_datum 각도가 P 를 자동 보정.)
    /// I9/I10(SOP): 기본 MeasureAxis="X" → L_datum=DatumAngle2Rad(2차 기준선).
    /// 어느 ROI 피팅 또는 교점/교차점 산출이 실패해도 false 반환 — 측정값 '—', 앱 무크래시.
    /// </summary>
    public class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "ArcLineIntersectDistance"; } }

        // 교점1 ROI 필드
        [Category("교점1|EdgeA1-ROI")]
        public double EdgeA1_Row { get; set; }
        public double EdgeA1_Col { get; set; }
        public double EdgeA1_Phi { get; set; }
        public double EdgeA1_Length1 { get; set; }
        public double EdgeA1_Length2 { get; set; }

        // 교점1 수평 에지 ROI
        [Category("교점1|EdgeB1-ROI")]
        public double EdgeB1_Row { get; set; }
        public double EdgeB1_Col { get; set; }
        public double EdgeB1_Phi { get; set; }
        public double EdgeB1_Length1 { get; set; }
        public double EdgeB1_Length2 { get; set; }

        // 교점2 수직 에지 ROI
        [Category("교점2|EdgeA2-ROI")]
        public double EdgeA2_Row { get; set; }
        public double EdgeA2_Col { get; set; }
        public double EdgeA2_Phi { get; set; }
        public double EdgeA2_Length1 { get; set; }
        public double EdgeA2_Length2 { get; set; }

        // 교점2 수평 에지 ROI
        [Category("교점2|EdgeB2-ROI")]
        public double EdgeB2_Row { get; set; }
        public double EdgeB2_Col { get; set; }
        public double EdgeB2_Phi { get; set; }
        public double EdgeB2_Length1 { get; set; }
        public double EdgeB2_Length2 { get; set; }

        // 교점1 EdgeA1 = 수직 에지 검출 → 스캔 방향 기본값 "LtoR"
        [Category("교점1|EdgeA1-Edge")]
        public int EdgeA1_Threshold { get; set; } = 10;
        public double EdgeA1_Sigma { get; set; } = 1.0;
        public int EdgeA1_SampleCount { get; set; } = 20;
        public int EdgeA1_TrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgeA1_Polarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeA1_Direction { get; set; } = "LtoR"; // 수직 에지 → 수평 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeA1_Selection { get; set; } = "All";

        // 교점1 EdgeB1 = 수평 에지 검출 → 스캔 방향 기본값 "TtoB"
        [Category("교점1|EdgeB1-Edge")]
        public int EdgeB1_Threshold { get; set; } = 10;
        public double EdgeB1_Sigma { get; set; } = 1.0;
        public int EdgeB1_SampleCount { get; set; } = 20;
        public int EdgeB1_TrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgeB1_Polarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeB1_Direction { get; set; } = "TtoB"; // 수평 에지 → 수직 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeB1_Selection { get; set; } = "All";

        // 교점2 EdgeA2 = 수직 에지 검출 → 스캔 방향 기본값 "LtoR"
        [Category("교점2|EdgeA2-Edge")]
        public int EdgeA2_Threshold { get; set; } = 10;
        public double EdgeA2_Sigma { get; set; } = 1.0;
        public int EdgeA2_SampleCount { get; set; } = 20;
        public int EdgeA2_TrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgeA2_Polarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeA2_Direction { get; set; } = "LtoR"; // 수직 에지 → 수평 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeA2_Selection { get; set; } = "All";

        // 교점2 EdgeB2 = 수평 에지 검출 → 스캔 방향 기본값 "TtoB"
        [Category("교점2|EdgeB2-Edge")]
        public int EdgeB2_Threshold { get; set; } = 10;
        public double EdgeB2_Sigma { get; set; } = 1.0;
        public int EdgeB2_SampleCount { get; set; } = 20;
        public int EdgeB2_TrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgeB2_Polarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeB2_Direction { get; set; } = "TtoB"; // 수평 에지 → 수직 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string EdgeB2_Selection { get; set; } = "All";

        // PropertyGrid ComboBox 옵션 래퍼 (4그룹 공유)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        // I9/I10 = Datum C X 방향이므로 기본값 "X"
        [Category("Measurement|Measure")]
        [ItemsSourceProperty(nameof(MeasureAxisList))]
        public string MeasureAxis { get; set; } = "X";
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> MeasureAxisList { get { return new List<string> { "X", "Y" }; } }

        // 두 교점 중 어느 것을 측정점으로 사용할지: Far(기본, INI 하위호환) 또는 Close
        [Category("Measurement|Measure")]
        [System.ComponentModel.Description("교점 선택 — Far: Datum 수직선 기준 더 먼 점(기본), Close: 더 가까운 점")]
        [ItemsSourceProperty(nameof(IntersectionPointSelectionList))]
        public string IntersectionPointSelection { get; set; } = "Far"; // default Far = INI 회귀 0
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> IntersectionPointSelectionList { get { return new List<string> { "Far", "Close" }; } }

        // IDatumOriginConsumer transient 필드 — datum 좌표 runtime 주입 전용. PropertyGrid 미표시, JSON 직렬화 제외.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumOriginCol { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngleRad { get; set; } // datum 1차(수평) 기준선 각도
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; } // datum 2차(수직) 기준선 각도. X축 측정 기준.
        // IDatumOriginConsumer 확장. ArcLineIntersect 미사용 (E2 전용) — 주입만 받음.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        public ArcLineIntersectDistanceMeasurement(object owner) : base(owner) { }

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
            overlays = new List<EdgeInspectionOverlay>();

            var svc = new VisionAlgorithmService();

            // (1) EdgeA1 ROI — 교점1 수직 에지 직선 피팅 (EdgeSelection: 사용자 선택값, 기본 "All")
            double a1r1, a1c1, a1r2, a1c2;
            if (!svc.TryFitLine(image,
                EdgeA1_Row, EdgeA1_Col, EdgeA1_Phi, EdgeA1_Length1, EdgeA1_Length2,
                datumTransform,
                EdgeA1_SampleCount, EdgeA1_TrimCount, EdgeA1_Sigma, EdgeA1_Threshold,
                EdgeA1_Direction, EdgeA1_Polarity,
                out a1r1, out a1c1, out a1r2, out a1c2, out error,
                EdgeA1_Selection))
            {
                return false;
            }

            // (2) EdgeB1 ROI — 교점1 수평 에지 직선 피팅
            double b1r1, b1c1, b1r2, b1c2;
            if (!svc.TryFitLine(image,
                EdgeB1_Row, EdgeB1_Col, EdgeB1_Phi, EdgeB1_Length1, EdgeB1_Length2,
                datumTransform,
                EdgeB1_SampleCount, EdgeB1_TrimCount, EdgeB1_Sigma, EdgeB1_Threshold,
                EdgeB1_Direction, EdgeB1_Polarity,
                out b1r1, out b1c1, out b1r2, out b1c2, out error,
                EdgeB1_Selection))
            {
                return false;
            }

            // (3) EdgeA2 ROI — 교점2 수직 에지 직선 피팅
            double a2r1, a2c1, a2r2, a2c2;
            if (!svc.TryFitLine(image,
                EdgeA2_Row, EdgeA2_Col, EdgeA2_Phi, EdgeA2_Length1, EdgeA2_Length2,
                datumTransform,
                EdgeA2_SampleCount, EdgeA2_TrimCount, EdgeA2_Sigma, EdgeA2_Threshold,
                EdgeA2_Direction, EdgeA2_Polarity,
                out a2r1, out a2c1, out a2r2, out a2c2, out error,
                EdgeA2_Selection))
            {
                return false;
            }

            // (4) EdgeB2 ROI — 교점2 수평 에지 직선 피팅
            double b2r1, b2c1, b2r2, b2c2;
            if (!svc.TryFitLine(image,
                EdgeB2_Row, EdgeB2_Col, EdgeB2_Phi, EdgeB2_Length1, EdgeB2_Length2,
                datumTransform,
                EdgeB2_SampleCount, EdgeB2_TrimCount, EdgeB2_Sigma, EdgeB2_Threshold,
                EdgeB2_Direction, EdgeB2_Polarity,
                out b2r1, out b2c1, out b2r2, out b2c2, out error,
                EdgeB2_Selection))
            {
                return false;
            }

            // (5) 교점1 = TryIntersectLines(A1, B1). 평행/근접 시 false
            double int1Row, int1Col;
            if (!VisionAlgorithmService.TryIntersectLines(
                a1r1, a1c1, a1r2, a1c2,
                b1r1, b1c1, b1r2, b1c2,
                out int1Row, out int1Col))
            {
                error = "교점1 산출 실패 (평행 또는 근접 에지)";
                return false;
            }

            // (6) 교점2 = TryIntersectLines(A2, B2). 평행/근접 시 false
            double int2Row, int2Col;
            if (!VisionAlgorithmService.TryIntersectLines(
                a2r1, a2c1, a2r2, a2c2,
                b2r1, b2c1, b2r2, b2c2,
                out int2Row, out int2Col))
            {
                error = "교점2 산출 실패 (평행 또는 근접 에지)";
                return false;
            }

            //260716 hbk ALI-01 측정 재정의(사용자 확정): ① 좌·우 두 교점을 잇는 직선(L_cross)과 datum 기준선(L_datum)의
            //  교차점 P를 intersection_ll(=TryIntersectLines)로 구하고, ② P와 우측 교점(int2)의 거리를 최종 측정값으로 한다(오른쪽=+).
            //  (기존: 측정축=교점2 col, 수직축=두 교점 Row 평균 → 좌 교점이 사실상 무의미. IntersectionPointSelection 폐기.)
            //  좌 교점이 L_cross 기울기·위치→P 를 결정하고, datum 이 휘어도 L_datum 각도(GetDatumAxisLine)가 P 를 자동 보정한다.
            double measureLineAngle;
            if (MeasureAxis == "X")
                measureLineAngle = DatumAngle2Rad; // X 측정 = datum 2차 기준선
            else
                measureLineAngle = DatumAngleRad;  // Y 측정 = datum 1차 기준선

            // L_datum 시작·끝점 (datum 원점 지나는 기준선, 방향 (sinθ,cosθ)). 길이는 교점 계산에만 쓰이므로 충분히 크게.
            double ldR1, ldC1, ldR2, ldC2;
            VisionAlgorithmService.GetDatumAxisLine(
                DatumOriginRow, DatumOriginCol, measureLineAngle, 4000.0,
                out ldR1, out ldC1, out ldR2, out ldC2);

            // 측정점 P = L_cross(좌교점 int1 ↔ 우교점 int2) ∩ L_datum. 평행이면 실패 → 측정 '—'.
            double measurePointRow, measurePointCol;
            if (!VisionAlgorithmService.TryIntersectLines(
                int1Row, int1Col, int2Row, int2Col,   // L_cross
                ldR1, ldC1, ldR2, ldC2,                // L_datum
                out measurePointRow, out measurePointCol))
            {
                error = "교점라인-datum 교차점 산출 실패 (평행)";
                return false;
            }

            // (8) 최종 측정값 = 교차점 P 와 우측 교점(int2) 사이의 실제 거리(mm). P·int2 는 둘 다 L_cross 위 점 —
            //  좌 교점이 L_cross 기울기→P 위치→세그먼트 길이에 실제 기여하고, datum 휜 각도는 이미 P 위치에 반영됨.
            //  부호: 우측 교점이 datum 기준선의 '오른쪽'(col+ 법선 방향)이면 +, 왼쪽이면 - (오른쪽=양수 규약).
            double segDrow = int2Row - measurePointRow;
            double segDcol = int2Col - measurePointCol;
            double distPx = System.Math.Sqrt(segDrow * segDrow + segDcol * segDcol);
            // datum 기준선의 오른쪽(col+) 법선 = (-cosθ, sinθ). 저장각 θ/θ+π 무관하게 col+ 향하도록 정규화(col성분≥0).
            double normR = -System.Math.Cos(measureLineAngle);
            double normC = System.Math.Sin(measureLineAngle);
            if (normC < 0.0) { normR = -normR; normC = -normC; }
            double sideSign = (segDrow * normR + segDcol * normC) >= 0.0 ? 1.0 : -1.0;
            resultValue = distPx * sideSign * pixelResolution;

            // overlay 거리선: 우측 교점 int2 → 교차점 P (실제 측정 세그먼트)
            double footRow = int2Row, footCol = int2Col;
            bool footOk = true;

            // overlay — 알고리즘이 이미 계산한 변수만 재사용. HALCON 재호출 없음.
            // 교점1 에지 라인 2개
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = a1r1, LineColumn1 = a1c1,
                LineRow2 = a1r2, LineColumn2 = a1c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = (a1r1 + a1r2) / 2.0, Column = (a1c1 + a1c2) / 2.0 }
                }
            });
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge2",
                LineRow1 = b1r1, LineColumn1 = b1c1,
                LineRow2 = b1r2, LineColumn2 = b1c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = (b1r1 + b1r2) / 2.0, Column = (b1c1 + b1c2) / 2.0 }
                }
            });
            // 교점2 에지 라인 2개
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge3",
                LineRow1 = a2r1, LineColumn1 = a2c1,
                LineRow2 = a2r2, LineColumn2 = a2c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = (a2r1 + a2r2) / 2.0, Column = (a2c1 + a2c2) / 2.0 }
                }
            });
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge4",
                LineRow1 = b2r1, LineColumn1 = b2c1,
                LineRow2 = b2r2, LineColumn2 = b2c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = (b2r1 + b2r2) / 2.0, Column = (b2c1 + b2c2) / 2.0 }
                }
            });
            // 교점1 마커 (점 마커: LineRow1==LineRow2, LineColumn1==LineColumn2)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Intersection1",
                LineRow1 = int1Row, LineColumn1 = int1Col,
                LineRow2 = int1Row, LineColumn2 = int1Col, // 점 마커 (길이 0 라인)
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col }
                }
            });
            // 교점2 마커
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Intersection2",
                LineRow1 = int2Row, LineColumn1 = int2Col,
                LineRow2 = int2Row, LineColumn2 = int2Col, // 점 마커
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col }
                }
            });
            //260716 hbk 좌우 교점 잇는 선(L_cross) 오버레이 — 두 교점을 지나는 직선을 화면에 표시(교차점 시각 검증용).
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-CrossLine",
                LineRow1 = int1Row, LineColumn1 = int1Col, // 좌 교점
                LineRow2 = int2Row, LineColumn2 = int2Col, // 우 교점
                Points = new List<EdgeInspectionPoint>()
            });
            // 측정점 마커 = L_cross ∩ L_datum 교차점 P
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-AvgPoint",
                LineRow1 = measurePointRow, LineColumn1 = measurePointCol, // 교차점 P
                LineRow2 = measurePointRow, LineColumn2 = measurePointCol, // 점 마커 (길이 0 라인)
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = measurePointRow, Column = measurePointCol }
                }
            });
            // Datum 거리선 — 보정 측정점 → 수선의 발 (footOk 가드)
            if (footOk)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine",
                    LineRow1 = footRow, LineColumn1 = footCol, // 수선의 발
                    LineRow2 = measurePointRow, LineColumn2 = measurePointCol, // 보정 측정점
                    Points = new List<EdgeInspectionPoint>
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol },
                        new EdgeInspectionPoint { Row = measurePointRow, Column = measurePointCol }
                    }
                });
            }

            return true;
        }
    }
}
