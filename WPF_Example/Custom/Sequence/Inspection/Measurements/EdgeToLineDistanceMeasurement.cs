using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
using ReringProject.Utility;

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

        // Phase 79 LSR-01: 핀 옆 띠 기준(국부 기준선) 옵션 상수
        public const string LOCAL_REF_LOG_TAG = "[LocalRef] ";
        public const string LOCAL_REF_OVERLAY_ROI_ID = "FAI-RefLine";
        public const string LOCAL_REF_ROI_SUBKEY = "LocalRef";
        public const string LOCAL_REF_ERR_DISABLED = "국부 기준 옵션이 꺼져 있음";
        public const string LOCAL_REF_ERR_NOT_TAUGHT = "기준 ROI 가 티칭되지 않음 (LocalRef_Length1/Length2 가 0)";
        public const string LOCAL_REF_ERR_NO_IMAGE = "기준점 가로 사진이 없음";
        public const string LOCAL_REF_ERR_FIT_FAILED = "기준 ROI 에서 띠 에지를 찾지 못함";
        public const string LOCAL_REF_REASON_NO_DATUM = "이 측정에 기준점(DatumRef)이 지정되지 않음";
        public const string LOCAL_REF_REASON_NO_DATUM_ORIGIN = "기준점 원점이 주입되지 않음";
        public const string LOCAL_REF_REASON_NOT_COMPUTED = "이번 사이클에 기준점 가로 사진에서 국부 기준선을 구하지 않음 (Test Find 로 잡아 둔 기준점 재사용 등)";
        public const string LOCAL_REF_REASON_STALE = "기준점을 찾은 뒤 기준 ROI 또는 에지 설정이 바뀜 — 기준점을 다시 찾으면 반영됨";
        private const double AXIS_HALF_LENGTH_PX = 200.0;
        private const int LOCAL_REF_DEFAULT_THRESHOLD = 10;
        private const double LOCAL_REF_DEFAULT_SIGMA = 1.0;
        private const int LOCAL_REF_DEFAULT_SAMPLE_COUNT = 20;
        private const int LOCAL_REF_DEFAULT_TRIM_PERCENT = 10;
        private const string LOCAL_REF_DEFAULT_POLARITY = "DarkToLight";
        private const string LOCAL_REF_DEFAULT_DIRECTION = "TtoB";
        private const string LOCAL_REF_DEFAULT_SELECTION = EdgeOptionLists.EDGE_SELECTION_STRONGEST;
        private const string SETTINGS_KEY_SEPARATOR = "|";
        private const string SETTINGS_KEY_NUMBER_FORMAT = "R";

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
        //260622 hbk Phase 57.1: trim 의미가 양끝 각 %(비율)로 변경 → 라벨만 % 표기 (프로퍼티명/INI 키 보존)
        [DisplayName("Edge Trim (%)")]
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
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.MeasureSelections; } }

        // 측정 거리 축 선택: datum 어느 기준선까지의 거리를 잴지.
        //  "Y" = datum 수평선(x축)까지 수직거리 (+Y 위쪽 양수, D-02) — 수평 에지 측정용.
        //  "X" = datum 수직선(y축)까지 거리 (+X 오른쪽 양수) — 수직 에지 측정용.
        [Category("Edge")]
        [System.ComponentModel.Description("측정 거리 축 — Y: datum 수평선까지, X: datum 수직선까지")]
        [ItemsSourceProperty(nameof(MeasureAxisList))]
        public string MeasureAxis { get; set; } = "Y";
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } }

        // Phase 79 LSR-01/D-79-03/D-79-04: 핀 옆 띠 기준(국부 기준선) 옵션. 기본값 false(선언 없음) — 옛 레시피와
        //  옵션 꺼진 측정은 이 옵션이 생기기 전과 완전히 동일하게 동작한다.
        [Category("Local Ref|Option")]
        [DisplayName("국부 기준 사용 (핀 옆 띠)")]
        [System.ComponentModel.Description("켜면 이 측정의 0점이 전역 기준선(띠 두 지점을 이은 직선) 대신 기준 ROI 에서 찾은 핀 옆 띠가 됩니다. 기준 ROI 는 기준점이 가로선을 찾은 사진(SIDE 는 z1)에서 찾고, 핀은 지금처럼 측정 사진에서 잽니다. 0점이 바뀌어 값이 달라지므로 켠 뒤 기준값·공차를 확인하세요. 기준 ROI 를 못 찾으면 전역 기준선으로 자동 전환되고 로그가 남습니다.")]
        public bool IsLocalRefEnabled { get; set; }

        [Category("Local Ref|ROI")]
        [System.ComponentModel.Description("핀 바로 옆 띠 에지에 둡니다. 핀에 가장 가까운 창을 권장합니다(멀리 두면 다른 에지를 잡아 오히려 나빠질 수 있음). 좌표는 측정 Point ROI 와 같은 원본 좌표입니다.")]
        public double LocalRef_Row { get; set; }
        public double LocalRef_Col { get; set; }
        public double LocalRef_Phi { get; set; }
        public double LocalRef_Length1 { get; set; }
        public double LocalRef_Length2 { get; set; }

        [Category("Local Ref|Edge")]
        public int LocalRefEdgeThreshold { get; set; } = LOCAL_REF_DEFAULT_THRESHOLD;
        public double LocalRefSigma { get; set; } = LOCAL_REF_DEFAULT_SIGMA;
        public int LocalRefEdgeSampleCount { get; set; } = LOCAL_REF_DEFAULT_SAMPLE_COUNT;
        [DisplayName("Local Ref Edge Trim (%)")]
        public int LocalRefEdgeTrimCount { get; set; } = LOCAL_REF_DEFAULT_TRIM_PERCENT;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string LocalRefEdgePolarity { get; set; } = LOCAL_REF_DEFAULT_POLARITY;
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string LocalRefEdgeDirection { get; set; } = LOCAL_REF_DEFAULT_DIRECTION;
        [ItemsSourceProperty(nameof(EdgeSelectionList))]
        public string LocalRefEdgeSelection { get; set; } = LOCAL_REF_DEFAULT_SELECTION;

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

        // Phase 79 LSR-02: 기준점 검출 때 구해 둔 국부 기준선 — Action_FAIMeasurement 가 측정 직전 주입(필드라 INI·붙여넣기 제외)
        [Newtonsoft.Json.JsonIgnore]
        public LocalRefLineResult InjectedLocalRef;

        public EdgeToLineDistanceMeasurement(object owner) : base(owner) { }

        // Phase 77 SZF-03/D-77-07 ②: 이 측정은 에지 강도 점수로 Z 를 고를 수 있는 지원 타입이다.
        public override bool SupportsEdgeStrengthScore()
        {
            return true;
        }

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
            LastFitScore = 0.0; // Phase 77: 모든 실패 경로에서 이전 사이클 점수가 남지 않게 먼저 0으로
            // Phase 79 LSR-02/LSR-03: 국부 기준선 사용 여부를 입구에서 한 번 정한다 — 실패 return 보다 먼저라 Z 후보마다 같은 값
            bool bUseLocalRef = IsInjectedLocalRefUsable();
            LastRefSource = ResolveRefSourceCode(bUseLocalRef);

            // D-11 Datum 찾기 실패 가드 (upstream gating 은 보조 이중 안전망)
            if (datumTransform == null || datumTransform.Length == 0)
            {
                error = "Datum not found";
                return false;
            }

            var svc = new VisionAlgorithmService();
            var edgeScore = new EdgeStrengthScore(); // Phase 77 SZF-03: 에지 강도 점수 수집(opt-in)
            svc.EdgeScore = edgeScore;
            double pr1, pc1, pr2, pc2;
            List<System.ValueTuple<double, double>> collectedEdgePoints = new List<System.ValueTuple<double, double>>();
            // strip-loop(stripCount 기본 20)가 First/Last 도 strip 마다 1점씩 누적 → 라인 피팅 충분.
            //  edgeCount<2 안전 가드는 VisionAlgorithmService.TryFitLine 에 유지.
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                EdgeSelection, collectedEdgePoints))
            {
                return false;
            }
            LastFitScore = edgeScore.Average; // Phase 77: Z 선택에 쓰는 점수(재계산 없이 채택된 결과에 남긴다)
            double pRow = (pr1 + pr2) / 2.0; // 폴백(수집점 없음/전투영실패)·레거시경로용 중점
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
                //260625 hbk 부호 정규화 축 분리 (각도 방향 따라 X 부호 뒤집히는 버그 수정):
                //  measureX(useAngle2)는 datum 수직축(θ2≈π/2, cosθ≈0). 여기에 cosθ≥0 정규화를 쓰면 cosθ 부호가
                //  틸트 방향(curAngle 부호)으로만 결정 → 틸트 방향 바뀔 때 sinT·cosT 동시 반전 → 법선(cosθ,-sinθ) 통째 반전
                //  → 같은 위치 점의 X 부호가 틸트 방향 따라 뒤집힘. 수직축은 sinθ≥0(sinθ2≈±1 안정)으로 정규화해야 부호 안정.
                //  measureY/measureX 폴백은 datum 수평축(θ≈0, cosθ≈±1)이라 기존 cosθ≥0 유지.
                if (useAngle2) // 수직축: sinθ≥0 정규화 (틸트 방향 무관 법선 안정)
                {
                    if (sinT < 0.0) { sinT = -sinT; cosT = -cosT; }
                }
                else // 수평축: cosθ≥0 정규화 (기존)
                {
                    if (cosT < 0.0) { sinT = -sinT; cosT = -cosT; }
                }
                // Phase 79 LSR-02 (O-79-02 b): 위치만 국부 기준선 중점, 각도는 전역 그대로
                double dAxisOriginRow = DatumOriginRow;
                double dAxisOriginCol = DatumOriginCol;
                if (bUseLocalRef)
                {
                    dAxisOriginRow = InjectedLocalRef.MidRow;
                    dAxisOriginCol = InjectedLocalRef.MidCol;
                }
                double axisR1, axisC1, axisR2, axisC2; // projection_pl 대상 직선의 2점 (교점 ±200px, 길이는 직선 정의에 무관)
                if (measureX) // datum 수직선(y축)
                {
                    if (useAngle2) // 실 수직 기준선: 방향벡터 (sinθ2,cosθ2) — measureY 와 같은 공식 (각도만 θ→θ2)
                    {
                        axisR1 = dAxisOriginRow - AXIS_HALF_LENGTH_PX * sinT;
                        axisC1 = dAxisOriginCol - AXIS_HALF_LENGTH_PX * cosT;
                        axisR2 = dAxisOriginRow + AXIS_HALF_LENGTH_PX * sinT;
                        axisC2 = dAxisOriginCol + AXIS_HALF_LENGTH_PX * cosT;
                    }
                    else // 폴백: 가상 수직선 (DatumAngleRad+90° 가정)
                    {
                        axisR1 = dAxisOriginRow - AXIS_HALF_LENGTH_PX * cosT;
                        axisC1 = dAxisOriginCol + AXIS_HALF_LENGTH_PX * sinT;
                        axisR2 = dAxisOriginRow + AXIS_HALF_LENGTH_PX * cosT;
                        axisC2 = dAxisOriginCol - AXIS_HALF_LENGTH_PX * sinT;
                    }
                }
                else // datum 수평선(x축): 방향벡터 (sinθ,cosθ), 각도 θ
                {
                    axisR1 = dAxisOriginRow - AXIS_HALF_LENGTH_PX * sinT;
                    axisC1 = dAxisOriginCol - AXIS_HALF_LENGTH_PX * cosT;
                    axisR2 = dAxisOriginRow + AXIS_HALF_LENGTH_PX * sinT;
                    axisC2 = dAxisOriginCol + AXIS_HALF_LENGTH_PX * cosT;
                }
                // per-edge-point signed projection 평균: collectedEdgePoints 각각을 axis(axisR1..axisC2) 에 투영,
                //  부호식은 기존 3분기(measureX+useAngle2 / measureX 폴백 / Y)를 항별 그대로 재사용한다.
                double sumSignedPx = 0.0, sumFootRow = 0.0, sumFootCol = 0.0, sumPtRow = 0.0, sumPtCol = 0.0;
                int nPts = 0;
                foreach (var ep in collectedEdgePoints)
                {
                    double er = ep.Item1, ec = ep.Item2;
                    try
                    {
                        HTuple prRow, prCol;
                        HOperatorSet.ProjectionPl(er, ec, axisR1, axisC1, axisR2, axisC2, out prRow, out prCol);
                        double fr = prRow.D;
                        double fc = prCol.D;
                        double signedPtPx;
                        if (measureX) // +X 오른쪽 양수
                        {
                            if (useAngle2) // axis (sinθ2,cosθ2) 의 우측 법선 (cosθ2,-sinθ2)
                            {
                                signedPtPx = (er - fr) * cosT - (ec - fc) * sinT;
                            }
                            else // 폴백: (sinθ, cosθ) 공식 (datum x축 방향 성분)
                            {
                                signedPtPx = (er - fr) * sinT + (ec - fc) * cosT;
                            }
                        }
                        else // +Y 위쪽 양수(D-02): datum x축 up-normal (-cosθ,sinθ) 성분
                        {
                            signedPtPx = (er - fr) * (-cosT) + (ec - fc) * sinT;
                        }
                        sumSignedPx += signedPtPx;
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
                    resultValue = (sumSignedPx / nPts) * pixelResolution;
                    pRow = sumPtRow / nPts;   // 표시용 — 수집점 평균 (resultValue 수학과 무관)
                    pCol = sumPtCol / nPts;
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
            }
            else // 레거시/무보정 폴백: AffineTransPoint2d (DatumRef 빈 문자열 또는 구버전 호출 경로)
            {
                // per-point 평균 미적용 이유: AffineTransPoint2d 는 선형 사상 → per-point 평균 == 중점(pRow/pCol) 변환이 수학적으로 동일.
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

            // Phase 79 D-79-07: 국부 기준선(띠 에지) 표시 — 국부를 쓴 측정만
            if (bUseLocalRef)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = LOCAL_REF_OVERLAY_ROI_ID,
                    LineRow1 = InjectedLocalRef.Row1,
                    LineColumn1 = InjectedLocalRef.Col1,
                    LineRow2 = InjectedLocalRef.Row2,
                    LineColumn2 = InjectedLocalRef.Col2
                });
            }

            return true;
        }

        // Phase 79 LSR-02/LSR-03: 옵션 켬 + 주입값 있음 + 그 주입값이 성공(Found)한 결과일 때만 국부 기준을 쓴다.
        private bool IsInjectedLocalRefUsable()
        {
            if (!IsLocalRefEnabled)
            {
                return false;
            }
            if (InjectedLocalRef == null)
            {
                return false;
            }
            if (!InjectedLocalRef.Found)
            {
                return false;
            }
            bool bDatumOriginInjected = DatumOriginRow != 0.0 || DatumOriginCol != 0.0;
            return bDatumOriginInjected;
        }

        // Phase 79 LSR-04: 이번 실행이 어느 기준선으로 값을 냈는지 코드로 남긴다. 옵션 꺼짐이면 null(표시 빈칸, 기존과 동일).
        private string ResolveRefSourceCode(bool bUseLocalRef)
        {
            if (!IsLocalRefEnabled)
            {
                return null;
            }
            if (bUseLocalRef)
            {
                return MeasurementBase.REF_SOURCE_LOCAL;
            }
            return MeasurementBase.REF_SOURCE_FALLBACK;
        }

        /// <summary>
        /// 기준점을 찾은 가로 사진에서 기준 ROI 의 띠 에지 선을 1번 피팅한다 — 기준점 검출 직후 InspectionSequence 가 부른다.
        /// 항상 null 이 아닌 결과를 돌려준다.
        /// </summary>
        public LocalRefLineResult ComputeLocalRefLine(HImage imgHorizontal, HTuple datumTransform)
        {
            LocalRefLineResult result = new LocalRefLineResult();
            result.SettingsKey = BuildLocalRefSettingsKey();
            if (DatumRef == null)
            {
                result.DatumName = "";
            }
            else
            {
                result.DatumName = DatumRef;
            }
            if (!IsLocalRefEnabled)
            {
                result.Found = false;
                result.Error = LOCAL_REF_ERR_DISABLED;
                return result;
            }
            bool bTaught = LocalRef_Length1 > 0 && LocalRef_Length2 > 0;
            if (!bTaught)
            {
                result.Found = false;
                result.Error = LOCAL_REF_ERR_NOT_TAUGHT;
                return result;
            }
            if (imgHorizontal == null)
            {
                result.Found = false;
                result.Error = LOCAL_REF_ERR_NO_IMAGE;
                return result;
            }
            try
            {
                var svc = new VisionAlgorithmService();
                var edgeScore = new EdgeStrengthScore(); // 이 측정의 Z 선택 점수와는 별개의 새 점수 수집기
                svc.EdgeScore = edgeScore;
                double r1, c1, r2, c2;
                string szFitError;
                bool bFitOk = svc.TryFitLine(imgHorizontal,
                    LocalRef_Row, LocalRef_Col, LocalRef_Phi, LocalRef_Length1, LocalRef_Length2,
                    datumTransform,
                    LocalRefEdgeSampleCount, LocalRefEdgeTrimCount, LocalRefSigma, LocalRefEdgeThreshold,
                    LocalRefEdgeDirection, LocalRefEdgePolarity,
                    out r1, out c1, out r2, out c2, out szFitError,
                    LocalRefEdgeSelection);
                if (bFitOk)
                {
                    result.Found = true;
                    result.Row1 = r1;
                    result.Col1 = c1;
                    result.Row2 = r2;
                    result.Col2 = c2;
                    result.EdgeScore = edgeScore.Average;
                }
                else
                {
                    result.Found = false;
                    if (string.IsNullOrEmpty(szFitError))
                    {
                        result.Error = LOCAL_REF_ERR_FIT_FAILED;
                    }
                    else
                    {
                        result.Error = szFitError;
                    }
                }
            }
            catch (System.Exception ex)
            {
                result.Found = false;
                result.Error = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 국부 기준선을 구할 때 쓴 기준 ROI·에지 설정·기준점 이름을 한 줄로 — 주입 때 달라졌으면 옛 결과로 보고 전환한다.
        /// </summary>
        public string BuildLocalRefSettingsKey()
        {
            List<string> lstParts = new List<string>();
            lstParts.Add(LocalRef_Row.ToString(SETTINGS_KEY_NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRef_Col.ToString(SETTINGS_KEY_NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRef_Phi.ToString(SETTINGS_KEY_NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRef_Length1.ToString(SETTINGS_KEY_NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRef_Length2.ToString(SETTINGS_KEY_NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRefSigma.ToString(SETTINGS_KEY_NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRefEdgeThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRefEdgeSampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lstParts.Add(LocalRefEdgeTrimCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string szPolarity = LocalRefEdgePolarity;
            if (szPolarity == null)
            {
                szPolarity = "";
            }
            lstParts.Add(szPolarity);
            string szDirection = LocalRefEdgeDirection;
            if (szDirection == null)
            {
                szDirection = "";
            }
            lstParts.Add(szDirection);
            string szSelection = LocalRefEdgeSelection;
            if (szSelection == null)
            {
                szSelection = "";
            }
            lstParts.Add(szSelection);
            string szDatumRef = DatumRef;
            if (szDatumRef == null)
            {
                szDatumRef = "";
            }
            lstParts.Add(szDatumRef);
            return string.Join(SETTINGS_KEY_SEPARATOR, lstParts);
        }

        // Phase 79 LSR-01 하위호환: 옛 레시피엔 Local Ref 키가 없다 — 기본값이 0/false 가 아닌 7개만 키 없을 때 선언 기본값으로 되돌린다
        public override bool Load(IniFile loadFile, string groupName)
        {
            bool bResult = base.Load(loadFile, groupName);
            IniSection sec;
            bool bHasSection = loadFile.TryGetSection(groupName, out sec) && sec != null;
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefEdgeThreshold)))
            {
                LocalRefEdgeThreshold = LOCAL_REF_DEFAULT_THRESHOLD;
            }
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefSigma)))
            {
                LocalRefSigma = LOCAL_REF_DEFAULT_SIGMA;
            }
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefEdgeSampleCount)))
            {
                LocalRefEdgeSampleCount = LOCAL_REF_DEFAULT_SAMPLE_COUNT;
            }
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefEdgeTrimCount)))
            {
                LocalRefEdgeTrimCount = LOCAL_REF_DEFAULT_TRIM_PERCENT;
            }
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefEdgePolarity)))
            {
                LocalRefEdgePolarity = LOCAL_REF_DEFAULT_POLARITY;
            }
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefEdgeDirection)))
            {
                LocalRefEdgeDirection = LOCAL_REF_DEFAULT_DIRECTION;
            }
            if (IsKeyMissing(bHasSection, sec, nameof(LocalRefEdgeSelection)))
            {
                LocalRefEdgeSelection = LOCAL_REF_DEFAULT_SELECTION;
            }
            return bResult;
        }

        private static bool IsKeyMissing(bool bHasSection, IniSection sec, string szKey)
        {
            if (!bHasSection)
            {
                return true;
            }
            return !sec.ContainsKey(szKey);
        }
    }

    // Phase 79 LSR-02: 기준점 검출 1번에 대한 국부 기준선 결과 — 만든 뒤 고치지 않는다.
    public class LocalRefLineResult
    {
        public string DatumName { get; set; }
        public bool Found { get; set; }
        public double Row1 { get; set; }
        public double Col1 { get; set; }
        public double Row2 { get; set; }
        public double Col2 { get; set; }
        public double EdgeScore { get; set; }
        public string Error { get; set; }
        public string SettingsKey { get; set; }

        private const double MIDPOINT_DIVISOR = 2.0;

        public double MidRow
        {
            get { return (Row1 + Row2) / MIDPOINT_DIVISOR; }
        }

        public double MidCol
        {
            get { return (Col1 + Col2) / MIDPOINT_DIVISOR; }
        }
    }
}
