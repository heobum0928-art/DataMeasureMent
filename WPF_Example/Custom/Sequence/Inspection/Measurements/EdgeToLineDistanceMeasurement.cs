//260512 hbk Phase 23 ALG-01 — Datum-relative Y 거리 측정 (D-06)
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
    //260517 hbk Phase 23.1 D-09 — ICustomTypeDescriptor 추가 (PropertyGrid 에서 EdgeSelection 숨김 — D-08 고정값 사용자 노출 차단)
    public class EdgeToLineDistanceMeasurement : MeasurementBase, //260512 hbk Phase 23 ALG-01
        System.ComponentModel.ICustomTypeDescriptor, //260517 hbk Phase 23.1 D-09
        IDatumOriginConsumer //260519 hbk Phase 31 D-03 — 소급 구현 (Action_FAIMeasurement 하드코딩 제거 전제조건)
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

        //260517 hbk l5e-03 — 측정 거리 축 선택: datum 어느 기준선까지의 거리를 잴지.
        //  "Y" = datum 수평선(x축)까지 수직거리 (+Y 위쪽 양수, D-02) — 수평 에지 측정용.
        //  "X" = datum 수직선(y축)까지 거리 (+X 오른쪽 양수) — 수직 에지 측정용.
        [Category("Edge")] //260517 hbk l5e-03
        [System.ComponentModel.Description("측정 거리 축 — Y: datum 수평선까지, X: datum 수직선까지")] //260517 hbk l5e-03
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260517 hbk l5e-03
        public string MeasureAxis { get; set; } = "Y"; //260517 hbk l5e-03
        [PropertyTools.DataAnnotations.Browsable(false)] //260517 hbk l5e-03
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } } //260517 hbk l5e-03

        //260517 hbk — datum 교점 좌표 runtime 주입 전용 프로퍼티 (Action_FAIMeasurement 가 TryExecute 직전 주입).
        //  DatumConfig.DetectedOrigin* 패턴과 동일하게 처리: 런타임 transient, PropertyGrid 미표시, JSON 직렬화 제외.
        //  ParamBase INI reflection 은 public double 을 0 으로 직렬화하나 — DatumConfig 와 동일하게 수용.
        [System.ComponentModel.Browsable(false)] //260517 hbk
        [PropertyTools.DataAnnotations.Browsable(false)] //260517 hbk
        [Newtonsoft.Json.JsonIgnore] //260517 hbk
        public double DatumOriginRow { get; set; } //260517 hbk — datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260517 hbk
        [PropertyTools.DataAnnotations.Browsable(false)] //260517 hbk
        [Newtonsoft.Json.JsonIgnore] //260517 hbk
        public double DatumOriginCol { get; set; } //260517 hbk — datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260517 hbk
        [PropertyTools.DataAnnotations.Browsable(false)] //260517 hbk
        [Newtonsoft.Json.JsonIgnore] //260517 hbk
        public double DatumAngleRad { get; set; } //260517 hbk — datum 1차(수평) 기준선 각도(rad). 미주입 시 0.
        //260519 hbk Phase 31 hotfix#3 — IDatumOriginConsumer 2차 각도. EdgeToLineDistance 인라인 투영은 현행 유지(carry-over),
        //  속성만 구현 — X축 = 실제 datum 수직선 전환은 후속 carry-over (Phase 23.1 사인오프 회귀 회피).
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3

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
            //260517 hbk — 실패 경로용 초기값 (성공 경로는 아래에서 채움)
            overlays = new List<EdgeInspectionOverlay>(); //260512 hbk Phase 23 ALG-01

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

            //260517 hbk l5e-03 — 측정값 = 에지 중점에서 datum 기준선에 내린 수선의 길이 (HALCON projection_pl).
            //  MeasureAxis="Y": datum 수평선(x축, 각도 θ)까지 수직거리 — D-02 +Y 위쪽 양수.
            //  MeasureAxis="X": datum 수직선(y축, 각도 θ+90°)까지 거리 — +X 오른쪽 양수.
            //  datum 기준선은 교점(DatumOriginRow/Col)을 지나고 각도는 DatumAngleRad(=DatumConfig.DetectedRefAngle,
            //  수평 결합선 Atan2(Δrow,Δcol)). ※ l5e-01 의 -(pRow-DatumOriginRow) 는 image row 축 단순 차분이라
            //  datum 회전 시 수직거리와 불일치 → projection_pl 정사영으로 교체.
            //  레거시/무보정(datum 미주입) 폴백은 아래 else 블록(AffineTransPoint2d, Y 기준) 무변경.
            bool datumOriginInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0); //260517 hbk
            double footRow = pRow; //260517 hbk l5e-03 — projection foot. 미주입/실패 시 에지점 자신(거리 0). overlay 에서 재사용.
            double footCol = pCol; //260517 hbk l5e-03
            bool footOk = false; //260517 hbk l5e-03
            if (datumOriginInjected) //260517 hbk l5e-03 — 정상 경로: projection_pl 로 datum 기준선까지 수직거리
            {
                bool measureX = (MeasureAxis == "X"); //260517 hbk l5e-03 — null/""/"Y" → false (레거시 INI·미설정 안전)
                double sinT = System.Math.Sin(DatumAngleRad); //260517 hbk l5e-03
                double cosT = System.Math.Cos(DatumAngleRad); //260517 hbk l5e-03
                if (cosT < 0.0) { sinT = -sinT; cosT = -cosT; } //260517 hbk l5e-03 — Atan2 방향(좌→우/우→좌) 무관 부호 일관성: datum x축 cosθ≥0 정규화
                double axisR1, axisC1, axisR2, axisC2; //260517 hbk l5e-03 — projection_pl 대상 직선의 2점 (교점 ±200px, 길이는 직선 정의에 무관)
                if (measureX) //260517 hbk l5e-03 — datum 수직선(y축): 방향벡터 (cosθ,-sinθ), 각도 θ+90°
                {
                    axisR1 = DatumOriginRow - 200.0 * cosT; //260517 hbk l5e-03
                    axisC1 = DatumOriginCol + 200.0 * sinT; //260517 hbk l5e-03
                    axisR2 = DatumOriginRow + 200.0 * cosT; //260517 hbk l5e-03
                    axisC2 = DatumOriginCol - 200.0 * sinT; //260517 hbk l5e-03
                }
                else //260517 hbk l5e-03 — datum 수평선(x축): 방향벡터 (sinθ,cosθ), 각도 θ
                {
                    axisR1 = DatumOriginRow - 200.0 * sinT; //260517 hbk l5e-03
                    axisC1 = DatumOriginCol - 200.0 * cosT; //260517 hbk l5e-03
                    axisR2 = DatumOriginRow + 200.0 * sinT; //260517 hbk l5e-03
                    axisC2 = DatumOriginCol + 200.0 * cosT; //260517 hbk l5e-03
                }
                try //260517 hbk l5e-03
                {
                    HTuple prRow, prCol; //260517 hbk l5e-03
                    HOperatorSet.ProjectionPl(pRow, pCol, axisR1, axisC1, axisR2, axisC2, out prRow, out prCol); //260517 hbk l5e-03 — 에지 중점을 datum 기준선에 정사영
                    footRow = prRow.D; //260517 hbk l5e-03
                    footCol = prCol.D; //260517 hbk l5e-03
                    footOk = true; //260517 hbk l5e-03
                }
                catch //260517 hbk l5e-03
                {
                    // projection 실패 시 foot=에지점 유지 → 측정값 0, FAI-DistLine skip //260517 hbk l5e-03
                }
                double signedPx; //260517 hbk l5e-03 — 수선의 발→에지점 변위의 부호 있는 거리 성분
                if (measureX) //260517 hbk l5e-03 — +X 오른쪽 양수: datum x축 방향 (sinθ,cosθ) 성분
                {
                    signedPx = (pRow - footRow) * sinT + (pCol - footCol) * cosT; //260517 hbk l5e-03
                }
                else //260517 hbk l5e-03 — +Y 위쪽 양수(D-02): datum x축 up-normal (-cosθ,sinθ) 성분
                {
                    signedPx = (pRow - footRow) * (-cosT) + (pCol - footCol) * sinT; //260517 hbk l5e-03
                }
                resultValue = signedPx * pixelResolution; //260517 hbk l5e-03
            }
            else //260517 hbk — 레거시/무보정 폴백: AffineTransPoint2d (DatumRef 빈 문자열 또는 구버전 호출 경로)
            {
                //260512 hbk Phase 23 ALG-01 — Datum-relative Y 좌표 추출 + D-02 부호 반전 (image row → +Y 위쪽 양수)
                double datumRow = pRow; //260517 hbk
                try //260517 hbk
                {
                    HTuple tRow, tCol; //260517 hbk
                    HOperatorSet.AffineTransPoint2d(datumTransform, pRow, pCol, out tRow, out tCol); //260517 hbk
                    datumRow = tRow.D; //260517 hbk
                }
                catch //260517 hbk
                {
                    // transform 실패 시 image-row 좌표 사용 (TryFitLine 패턴 일관성, RESEARCH Pitfall 2) //260517 hbk
                }
                resultValue = -datumRow * pixelResolution; //260517 hbk — D-02 +Y 부호 (위쪽 양수)
            }

            // Phase 7-01 D-03 / Phase 23-01 의 의도적 '빈 리스트' overlay 정책을 이번에 뒤집음. //260517 hbk
            // Phase 23.1 UAT 시각 검증(측정값 vs SOP 도면 정확도)을 위해 검출 에지/거리선을 캔버스에 표시. //260517 hbk

            // 1) 검출 에지 라인 overlay (FAIEdgeMeasurementService.BuildOverlaysSingle 패턴) //260517 hbk
            overlays.Add(new EdgeInspectionOverlay //260517 hbk
            {
                RoiId = "FAI-Edge1", //260517 hbk — StartsWith("FAI-Edge") 충족 → HalconDisplayService 녹/적 분기 + Action_FAIMeasurement 판정 suffix(-OK/-NG) 자동 부여
                LineRow1 = pr1, //260517 hbk
                LineColumn1 = pc1, //260517 hbk
                LineRow2 = pr2, //260517 hbk
                LineColumn2 = pc2, //260517 hbk
                Points = new List<EdgeInspectionPoint> //260517 hbk
                {
                    new EdgeInspectionPoint { Row = pRow, Column = pCol } //260517 hbk — 에지 중점 1개 (이미 L95-96에 계산됨)
                }
            }); //260517 hbk

            // 2) 수직 드롭선 overlay: 수선의 발(projection foot, datum 기준선 위) → 에지 중점 //260517 hbk l5e-03
            bool originOk = false; //260517 hbk
            double originRow = 0.0; //260517 hbk
            double originCol = 0.0; //260517 hbk
            if (datumOriginInjected) //260517 hbk l5e-03 — 정상 경로: FAI-DistLine = 에지점→수선의 발 (수직 드롭)
            {
                originRow = footRow; //260517 hbk l5e-03 — projection foot (datum 기준선 위의 점). HomMat2dInvert 불필요.
                originCol = footCol; //260517 hbk l5e-03
                originOk = footOk; //260517 hbk l5e-03 — projection 실패 시 false → FAI-DistLine skip
            }
            else //260517 hbk — 레거시/무보정 폴백: HomMat2dInvert 경로 (datum 미주입 케이스에서 overlay 완전 소실 방지)
            {
                try //260517 hbk
                {
                    HTuple invMat; //260517 hbk
                    HOperatorSet.HomMat2dInvert(datumTransform, out invMat); //260517 hbk — datumTransform 역행렬: image→datum 역 = datum→image
                    HTuple oRow, oCol; //260517 hbk
                    HOperatorSet.AffineTransPoint2d(invMat, 0.0, 0.0, out oRow, out oCol); //260517 hbk — datum 원점(0,0)의 image 좌표
                    originRow = oRow.D; //260517 hbk
                    originCol = oCol.D; //260517 hbk
                    originOk = true; //260517 hbk
                }
                catch //260517 hbk
                {
                    // 역변환 실패 시 FAI-DistLine 만 skip — 에지 라인 overlay 와 측정값은 유지 //260517 hbk
                }
            }

            if (originOk) //260517 hbk
            {
                overlays.Add(new EdgeInspectionOverlay //260517 hbk
                {
                    RoiId = "FAI-DistLine", //260517 hbk — HalconDisplayService.cs:181 cyan(청록) 분기 충족, suffix 미부여
                    LineRow1 = originRow, //260517 hbk l5e-03 — 수선의 발 (projection foot, datum 기준선 위)
                    LineColumn1 = originCol, //260517 hbk
                    LineRow2 = pRow, //260517 hbk — 에지 중점 image 좌표
                    LineColumn2 = pCol, //260517 hbk
                    Points = new List<EdgeInspectionPoint> //260517 hbk — 양 끝점 X자 마커 (BuildOverlaysBoth FAI-DistLine 패턴)
                    {
                        new EdgeInspectionPoint { Row = originRow, Column = originCol }, //260517 hbk
                        new EdgeInspectionPoint { Row = pRow, Column = pCol } //260517 hbk
                    }
                }); //260517 hbk
            }

            return true;
        }

        //260517 hbk Phase 23.1 D-09 — PropertyGrid EdgeSelection 숨김 (D-08 고정값 사용자 노출 차단)
        //  PropertyTools.Wpf PropertyGrid 는 무인자 GetProperties() 만 호출 → 무인자 오버로드가 진짜 진입점.
        //  ParamBase INI 직렬화는 GetType().GetProperties() Reflection 경로 사용 → ICustomTypeDescriptor 영향 0.
        public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) //260517 hbk Phase 23.1 D-09
        {
            return BuildFilteredProperties(attributes);
        }
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() //260517 hbk Phase 23.1 D-09
        {
            return BuildFilteredProperties(null);
        }
        private System.ComponentModel.PropertyDescriptorCollection BuildFilteredProperties(System.Attribute[] attrs) //260517 hbk Phase 23.1 D-09
        {
            //260517 hbk Phase 23.1 D-09 — [Browsable(false)] ComboBox 소스 래퍼 강제 포함 (Phase 18 CO-01 패턴).
            //  EdgeSelectionList 도 화이트리스트에 포함 — hideFunc 로 숨기되, keepList 에 넣어야
            //  PropertyTools.Wpf 가 EdgeDirection/EdgePolarity 의 ComboBox 를 정상 렌더한다.
            var sourceNames = new System.Collections.Generic.HashSet<string>
            {
                nameof(EdgeDirectionList),
                nameof(EdgePolarityList),
                nameof(EdgeSelectionList),
                nameof(MeasureAxisList), //260517 hbk l5e-03 — MeasureAxis ComboBox 소스 강제 포함 (Browsable(false) 래퍼)
            };
            return DynamicPropertyHelper.FilterProperties(
                this, attrs,
                name => name == "EdgeSelection" || name == "EdgeSelectionList", //260517 hbk Phase 23.1 D-09 — EdgeSelection 행 + List 래퍼 둘 다 숨김
                sourceNames);
        }
        public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); } //260517 hbk Phase 23.1 D-09
        public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); } //260517 hbk Phase 23.1 D-09
        public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); } //260517 hbk Phase 23.1 D-09
        public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); } //260517 hbk Phase 23.1 D-09
        public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); } //260517 hbk Phase 23.1 D-09
        public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); } //260517 hbk Phase 23.1 D-09
        public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); } //260517 hbk Phase 23.1 D-09
        public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); } //260517 hbk Phase 23.1 D-09
        public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); } //260517 hbk Phase 23.1 D-09
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; } //260517 hbk Phase 23.1 D-09
    }
}
