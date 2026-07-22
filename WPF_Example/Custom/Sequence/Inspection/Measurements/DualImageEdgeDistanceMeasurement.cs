using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 두 별도 이미지에서 각각 ROI 에지를 추출해 거리(mm)를 측정한다.
    ///  - PointROI: TeachingImagePath(또는 ShotParam.SimulImagePath) 이미지 → fit_line 중점 1점 ("Point").
    ///  - LineROI:  TeachingImagePath_Vertical 이미지 → fit_line 결과 라인 자체 ("Line").
    /// 거리 = HALCON projection_pl(Point → Line) 의 수직 정사영 거리 × pixelResolution.
    /// 두 이미지 좌표계는 동일 평면 가정 (SameFrame 계약).
    /// Action_FAIMeasurement 가 EStep.Measure 진입 시 RuntimeImageA / RuntimeImageB 를 주입한다 — 본 TryExecute 는 인자 image 를 무시.
    /// </summary>
    public class DualImageEdgeDistanceMeasurement : MeasurementBase, IDatumOriginConsumer
    {
        public override string TypeName { get { return "DualImageEdgeDistance"; } }

        // 듀얼 이미지 — 가로축(점 ROI) 이미지는 ShotParam.SimulImagePath 재사용, 세로축(라인 ROI) 이미지는 별도 경로.
        // LineROI 이미지 경로 (Action_FAIMeasurement.TryGrabOrLoadFaiDualImages 가 로드)
        [Category("Image|DualImage")]
        [System.ComponentModel.Description("LineROI 검출용 별도 이미지 경로 — Bottom E5 패턴")]
        [DisplayName("세로축 티칭 이미지")]
        [InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)]
        [AutoUpdateText]
        public string TeachingImagePath_Vertical { get; set; } = "";

        // PointROI 검출용 가로축 이미지 경로. 명시 시 우선 사용, 빈 문자열/파일 부재 시 ShotConfig.SimulImagePath 로 fallback.
        [Category("Image|DualImage")]
        [System.ComponentModel.Description("PointROI 검출용 가로축 이미지 경로 — 명시 시 우선 사용, 빈 문자열/파일 부재 시 ShotConfig.SimulImagePath 로 fallback")]
        [DisplayName("가로축 티칭 이미지")]
        [InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)]
        [AutoUpdateText]
        public string TeachingImagePath_Horizontal { get; set; } = "";

        // 크로스-Z 듀얼이미지(PROTO-Z-CROSS) — PointROI/LineROI 를 서로 다른 z_index 라이브 캡처에서 얻을 때 사용.
        //  -1(기본) = 미설정 → 기존 정적 TeachingImagePath 경로 그대로 사용(회귀 0). 0 이상 = 해당 z_index 캡처 이미지 사용.
        [Category("Image|DualImage")]
        [System.ComponentModel.Description("PointROI(에지 A) 라이브 캡처 z_index. -1=미설정(기존 정적 이미지 경로 사용)")]
        [DisplayName("Point z_index (ZIndexA)")]
        public int ZIndexA { get; set; } = -1;

        [Category("Image|DualImage")]
        [System.ComponentModel.Description("LineROI(에지 B) 라이브 캡처 z_index. -1=미설정(기존 정적 이미지 경로 사용)")]
        [DisplayName("Line z_index (ZIndexB)")]
        public int ZIndexB { get; set; } = -1;

        // PointROI (점 형태, 1차 이미지 = ShotParam.SimulImagePath)
        [Category("PointROI|ROI")]
        public double PointROI_Row { get; set; }
        public double PointROI_Col { get; set; }
        public double PointROI_Phi { get; set; }
        public double PointROI_Length1 { get; set; }
        public double PointROI_Length2 { get; set; }

        [Category("PointROI|Edge")]
        public int PointROI_EdgeThreshold { get; set; } = 10;
        public double PointROI_Sigma { get; set; } = 1.0;
        public int PointROI_EdgeSampleCount { get; set; } = 20;
        //260622 hbk Phase 57.1: trim 의미가 양끝 각 %(비율)로 변경 → 라벨만 % 표기 (프로퍼티명/INI 키 보존)
        [DisplayName("Point Edge Trim (%)")]
        public int PointROI_EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string PointROI_EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string PointROI_EdgeDirection { get; set; } = "TtoB";
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string PointROI_EdgeSelection { get; set; } = "All";

        // LineROI (라인 형태, 2차 이미지 = TeachingImagePath_Vertical)
        [Category("LineROI|ROI")]
        public double LineROI_Row { get; set; }
        public double LineROI_Col { get; set; }
        public double LineROI_Phi { get; set; }
        public double LineROI_Length1 { get; set; }
        public double LineROI_Length2 { get; set; }

        [Category("LineROI|Edge")]
        public int LineROI_EdgeThreshold { get; set; } = 10;
        public double LineROI_Sigma { get; set; } = 1.0;
        public int LineROI_EdgeSampleCount { get; set; } = 20;
        //260622 hbk Phase 57.1: trim 의미가 양끝 각 %(비율)로 변경 → 라벨만 % 표기 (프로퍼티명/INI 키 보존)
        [DisplayName("Line Edge Trim (%)")]
        public int LineROI_EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string LineROI_EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string LineROI_EdgeDirection { get; set; } = "TtoB";
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string LineROI_EdgeSelection { get; set; } = "All";

        // ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        // IDatumOriginConsumer transient 필드 (3중 attribute로 직렬화/PropertyGrid 노출 차단)
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
        public double DatumAngleRad { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumAngle2Rad { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleRow { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public double DatumDetectedCircleCol { get; set; }

        // Action_FAIMeasurement 가 EStep.Measure 진입 시 주입.
        //   RuntimeImageA = PointROI 이미지 (TeachingImagePath / SimulImagePath)
        //   RuntimeImageB = LineROI 이미지 (TeachingImagePath_Vertical)
        // TryExecute 호출 후 Action_FAIMeasurement 가 dispose + 본 필드 null 리셋.
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public HImage RuntimeImageA { get; set; }
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public HImage RuntimeImageB { get; set; }

        public DualImageEdgeDistanceMeasurement(object owner) : base(owner) { }

        // 하위호환(D-07): ParamBase.Load 는 INI 누락 Int32 키를 0 으로 덮어쓴다(MeasCorrectionFactor 와 동일 함정).
        //  구 레시피엔 ZIndexA/ZIndexB 키가 없어 0 으로 로드되면 "z_index=0 명시"로 오인되어 크로스-Z 실행 스코프/캡처가
        //  오작동한다(T-68-03). 키 부재 시에만 -1(미설정) 복원한다 — base.Load 가 MeasCorrectionFactor 복원까지 위임 처리.
        public override bool Load(IniFile loadFile, string groupName)
        {
            bool result = base.Load(loadFile, groupName);
            IniSection sec;
            if (!loadFile.TryGetSection(groupName, out sec) || sec == null)
            {
                ZIndexA = -1;
                ZIndexB = -1;
                return result;
            }
            if (!sec.ContainsKey("ZIndexA")) ZIndexA = -1;
            if (!sec.ContainsKey("ZIndexB")) ZIndexB = -1;
            return result;
        }

        public override bool TryExecute(
            HImage image,                                       // 무시 — RuntimeImageA/B 사용
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>();

            // (0) Runtime 이미지 가드 — Action_FAIMeasurement 가 주입 실패 시 명시적 에러
            if (RuntimeImageA == null || RuntimeImageB == null)
            {
                error = "DualImage 이미지 미주입 (RuntimeImageA/B null)";
                return false;
            }

            var svc = new VisionAlgorithmService();

            // (1) PointROI — RuntimeImageA 에서 fit_line → midpoint
            double pa_r1, pa_c1, pa_r2, pa_c2;
            List<System.ValueTuple<double, double>> collectedEdgePoints = new List<System.ValueTuple<double, double>>();
            if (!svc.TryFitLine(RuntimeImageA,
                PointROI_Row, PointROI_Col, PointROI_Phi, PointROI_Length1, PointROI_Length2,
                datumTransform,
                PointROI_EdgeSampleCount, PointROI_EdgeTrimCount, PointROI_Sigma, PointROI_EdgeThreshold,
                PointROI_EdgeDirection, PointROI_EdgePolarity,
                out pa_r1, out pa_c1, out pa_r2, out pa_c2, out error,
                PointROI_EdgeSelection, collectedEdgePoints))
            {
                return false;
            }
            double pointRow = (pa_r1 + pa_r2) / 2.0;            // 라인 중점 — 폴백(수집점 없음/전투영실패)용으로 유지
            double pointCol = (pa_c1 + pa_c2) / 2.0;

            // (2) LineROI — RuntimeImageB 에서 fit_line → 라인 그대로 (시작점/끝점 사용)
            double lb_r1, lb_c1, lb_r2, lb_c2;
            if (!svc.TryFitLine(RuntimeImageB,
                LineROI_Row, LineROI_Col, LineROI_Phi, LineROI_Length1, LineROI_Length2,
                datumTransform,
                LineROI_EdgeSampleCount, LineROI_EdgeTrimCount, LineROI_Sigma, LineROI_EdgeThreshold,
                LineROI_EdgeDirection, LineROI_EdgePolarity,
                out lb_r1, out lb_c1, out lb_r2, out lb_c2, out error,
                LineROI_EdgeSelection))
            {
                return false;
            }

            // (3) projection_pl(point → line) — 수집 에지점(collectedEdgePoints) 각각을 기준선에 투영해
            //  per-point UNSIGNED 거리(기존 sqrt 공식 그대로)를 구하고 산술평균한다. 단일 중점 1점 투영이 아님.
            double footRow = pointRow;
            double footCol = pointCol;
            bool footOk = false;
            double sumDistPx = 0.0, sumFootRow = 0.0, sumFootCol = 0.0, sumPtRow = 0.0, sumPtCol = 0.0;
            int nPts = 0;
            foreach (var ep in collectedEdgePoints)
            {
                double er = ep.Item1, ec = ep.Item2;
                try
                {
                    HTuple prRow, prCol;
                    HOperatorSet.ProjectionPl(er, ec, lb_r1, lb_c1, lb_r2, lb_c2, out prRow, out prCol);
                    double fr = prRow.D;
                    double fc = prCol.D;
                    double d = System.Math.Sqrt((er - fr) * (er - fr) + (ec - fc) * (ec - fc));
                    sumDistPx += d;
                    sumFootRow += fr;
                    sumFootCol += fc;
                    sumPtRow += er;
                    sumPtCol += ec;
                    nPts++;
                }
                catch
                {
                    // 이 점 투영 실패 — skip, 나머지 점으로 계속
                }
            }
            if (nPts >= 1)
            {
                resultValue = (sumDistPx / nPts) * pixelResolution; // px 평균 후 × 해상도
                pointRow = sumPtRow / nPts;                          // 표시용 — 수집점 평균 (resultValue 수학과 무관)
                pointCol = sumPtCol / nPts;
                footRow = sumFootRow / nPts;
                footCol = sumFootCol / nPts;
                footOk = true;
            }
            else
            {
                // 폴백: collectedEdgePoints 비었거나 모든 투영 실패 — 기존 단일-중점 동작 그대로
                try
                {
                    HTuple prRow, prCol;
                    HOperatorSet.ProjectionPl(pointRow, pointCol, lb_r1, lb_c1, lb_r2, lb_c2, out prRow, out prCol);
                    footRow = prRow.D;
                    footCol = prCol.D;
                    footOk = true;
                }
                catch
                {
                    error = "projection_pl 실패";
                    return false;
                }
                double dRow = pointRow - footRow;
                double dCol = pointCol - footCol;
                double distPx = System.Math.Sqrt(dRow * dRow + dCol * dCol);
                resultValue = distPx * pixelResolution;             // mm
            }

            // (4) Overlay 생성 — RoiId 컨벤션 강제 (HalconDisplayService 분기 충족)
            //   FAI-Edge1 = PointROI 검출 라인 + 중점 마커 (녹/적 + suffix 자동)
            //   FAI-Edge2 = LineROI 검출 라인 (녹/적 + suffix 자동)
            //   FAI-DistLine = projection 거리선 (청록 고정, suffix 미부여)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = pa_r1, LineColumn1 = pa_c1,
                LineRow2 = pa_r2, LineColumn2 = pa_c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = pointRow, Column = pointCol }
                }
            });
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge2",
                LineRow1 = lb_r1, LineColumn1 = lb_c1,
                LineRow2 = lb_r2, LineColumn2 = lb_c2
            });
            if (footOk)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine",
                    LineRow1 = footRow, LineColumn1 = footCol,
                    LineRow2 = pointRow, LineColumn2 = pointCol,
                    Points = new List<EdgeInspectionPoint>
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol },
                        new EdgeInspectionPoint { Row = pointRow, Column = pointCol }
                    }
                });
            }

            return true;
        }
    }
}
