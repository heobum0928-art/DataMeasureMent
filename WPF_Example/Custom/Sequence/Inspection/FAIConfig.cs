using System;
using System.Collections.Generic; //260413 hbk Phase 6: Measurements 리스트
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Models;
using ReringProject.Utility;
//260423 hbk WR-RT-02 ComboBox 옵션 소스

namespace ReringProject.Sequence {

    //260507 hbk Phase 19 QUAL-03: ICustomTypeDescriptor 추가 — PropertyGrid 동적 노출용 (EdgeMeasureType 별 필터)
    public class FAIConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor {

        //260413 hbk Phase 6: Multi-Algorithm Measurements (D-20) — 수동 직렬화, ParamBase 자동 Save/Load 제외
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<MeasurementBase> Measurements { get; private set; } = new List<MeasurementBase>();

        //260413 hbk Phase 6: Factory를 통한 Measurement 추가
        public MeasurementBase AddMeasurement(string typeName)
        {
            var m = MeasurementFactory.Create(typeName, this);
            if (m != null) Measurements.Add(m);
            return m;
        }

        //260413 hbk Phase 6: 인덱스로 제거
        public bool RemoveMeasurement(int index)
        {
            if (index < 0 || index >= Measurements.Count) return false;
            Measurements.RemoveAt(index);
            return true;
        }

        //260413 hbk Phase 6: 전체 제거
        public void ClearMeasurements()
        {
            Measurements.Clear();
        }

        // ROI
        [Category("ROI")]
        public double ROI_Row { get; set; }
        public double ROI_Col { get; set; }
        public double ROI_Phi { get; set; }
        public double ROI_Length1 { get; set; }
        public double ROI_Length2 { get; set; }

        // Edge Measurement //260409 hbk
        //260507 hbk Phase 19 QUAL-03: EdgeMeasureType — 측정 알고리즘 선택 (INI 직렬화, 미존재 시 기본값 "EdgeToLineDistance")
        //  저장 타입: string (ParamBase.Save/Load switch가 string 지원)
        //  유효값: MeasurementFactory.GetTypeNames() 목록 (10종)
        //  미존재 INI 로드 시: property 기본값 "EdgeToLineDistance" 가 유지됨 (ParamBase.Load 가 INI 키 미존재 시 기본값 보존)
        //260528 hbk Phase 38 WR-02 — 기본값을 GetTypeNames() 노출 타입으로 정렬 (구 기본 "EdgePairDistance" 는 #1 정리로 콤보에서 제거됨 — 신규 FAI 콤보 빈값/불일치 방지)
        [Category("Edge|Measurement")] //260507 hbk Phase 19 QUAL-03
        [ItemsSourceProperty(nameof(EdgeMeasureTypeList))] //260507 hbk Phase 19 QUAL-03 — PropertyGrid 드롭다운 목록
        public string EdgeMeasureType { get; set; } = "EdgeToLineDistance"; //260507 hbk Phase 19 QUAL-03 //260528 hbk Phase 38 WR-02

        //260507 hbk Phase 19 QUAL-03 — EdgeMeasureType 드롭다운 옵션 (MeasurementFactory 단일 소스, 하드코딩 금지)
        //260508 hbk Phase 19 fix — 정적 readonly 캐시. 매번 new List 반환 시 PropertyTools.Wpf 가 콤보 ItemsSource 인식 실패.
        //  EdgeOptionLists.Directions 와 동일한 패턴 (정적 readonly List 단일 인스턴스).
        private static readonly List<string> _edgeMeasureTypeListCache = //260508 hbk Phase 19 fix
            new List<string>(MeasurementFactory.GetTypeNames()); //260508 hbk Phase 19 fix
        [PropertyTools.DataAnnotations.Browsable(false)] //260507 hbk Phase 19 QUAL-03
        public List<string> EdgeMeasureTypeList { //260507 hbk Phase 19 QUAL-03
            get { return _edgeMeasureTypeListCache; } //260508 hbk Phase 19 fix
        }

        public int EdgeThreshold { get; set; } = 10; //260409 hbk RoiDefinition 호환
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgeDirection { get; set; } = "LtoR"; //260409 hbk LtoR, RtoL, TtoB, BtoT
        public string EdgeSelection { get; set; } = "First"; //260409 hbk First, Last, Both
        public int EdgeSampleCount { get; set; } = 20; //260409 hbk 샘플 스트립 수
        public int EdgeTrimCount { get; set; } = 10; //260409 hbk 극값 제거 수
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgePolarity { get; set; } = "DarkToLight"; //260409 hbk DarkToLight, LightToDark

        //260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 래퍼 — 공용 소스 참조
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        //260408 hbk Calibration (per D-12, D-16: camera-level calibration stored in CameraSlaveParam,
        // but FAIConfig also carries PixelResolution for RoiDefinition compatibility)
        [Category("Calibration")]
        public double PixelResolutionX { get; set; } = 1.0;  //260408 hbk mm/pixel
        public double PixelResolutionY { get; set; } = 1.0;  //260408 hbk mm/pixel

        //260408 hbk Polygon ROI (per D-15: serialized as "x1,y1;x2,y2;x3,y3" string for INI storage)
        [Category("ROI")]
        public string PolygonPoints { get; set; } = "";  //260408 hbk

        //260420 hbk Phase 6-04: 공차/기준값은 MeasurementBase로 단일 소스화 — FAI 레벨 중복 필드 제거
        // (NominalValue / UpperTolerance / LowerTolerance / SetResult() deleted — 판정은 MeasurementBase.EvaluateJudgement)

        // Result (runtime, not saved) — Action_FAIMeasurement가 Measurement 집계 결과를 써주고, TCP 응답(FAIResultData)이 읽어간다
        [Browsable(false)]
        public double MeasuredValue { get; set; }

        [Browsable(false)]
        public bool IsPass { get; set; }

        //260526 hbk CO-31-01 — INotifyPropertyChanged 발화로 트리 헤더 즉시 갱신 (PropertyGrid 편집 → Tree)
        //260526 hbk CO-31-01 — [Browsable(false)] 제거 + [Category] 추가로 PropertyGrid 노출 (DatumName/MeasurementName 과 일관성).
        //  기존 Btn_RenameFAI_Click 버튼은 그대로 작동 — 양쪽 모두 동일 setter 호출.
        private string _faiName;
        [Category("FAI|Identity")]
        public string FAIName {
            get { return _faiName; }
            set {
                if (_faiName == value) return;
                _faiName = value;
                RaisePropertyChanged(nameof(FAIName));
            }
        }

        public FAIConfig(object owner) : base(owner) {
        }

        public FAIConfig(object owner, string name) : base(owner) {
            FAIName = name;
        }

        //260420 hbk Phase 6-04: SetResult() 제거 — 판정은 MeasurementBase.EvaluateJudgement, 집계는 Action_FAIMeasurement가 직접 수행

        public void ClearResult() {
            MeasuredValue = 0;
            IsPass = false;
        }

        //260408 hbk ToRoiDefinition() 추가
        /// <summary>
        /// Converts FAIConfig Rectangle2 params (center+half-lengths+phi) to RoiDefinition bounding box.
        /// NOTE on D-05 compatibility: ROI_Phi exists in legacy INI data from Rectangle2 era.
        /// ToRoiDefinition() uses sin/cos of ROI_Phi for backward compatibility with existing INI files.
        /// New ROI input via the Rect ROI button (Plan 02) always sets ROI_Phi=0.0 (Rectangle1 only),
        /// so D-05 "Rectangle2는 사용하지 않는다" is honored for all new user input.
        /// </summary>
        public RoiDefinition ToRoiDefinition()
        {
            //260423 hbk Phase 11 D-16 — Circle ROI 렌더링 (committed circle → RoiDefinition Shape=Circle)
            // Precedence: Circle takes priority over Rect/Polygon when present.
            //260519 hbk Phase 31 CO-23.1-02 — CircleDiameter + CircleCenterDistance 두 타입 모두 Circle_* ROI 보유
            double circleRow = 0, circleCol = 0, circleRadius = 0;
            //260519 hbk Phase 31 CO-23.1-02 — polar strip 시각화 파라미터 (CircleCenterDistance polar 모드만 > 0)
            double circleStepDeg = 0, circleL1Ratio = 0, circleL2Ratio = 0;
            bool hasCircle = false;
            foreach (var m in Measurements)
            {
                var c = m as CircleDiameterMeasurement;
                if (c != null && c.Circle_Radius > 0)
                {
                    circleRow = c.Circle_Row; circleCol = c.Circle_Col; circleRadius = c.Circle_Radius;
                    hasCircle = true; break;
                }
                var cc = m as CircleCenterDistanceMeasurement; //260519 hbk Phase 31 CO-23.1-02
                if (cc != null && cc.Circle_Radius > 0)
                {
                    circleRow = cc.Circle_Row; circleCol = cc.Circle_Col; circleRadius = cc.Circle_Radius;
                    //260519 hbk Phase 31 CO-23.1-02 — polar 모드(RadialDirection 비어있지 않음)일 때만 strip 파라미터 전달
                    if (!string.IsNullOrEmpty(cc.Circle_RadialDirection))
                    {
                        circleStepDeg = cc.Circle_PolarStepDeg;
                        circleL1Ratio = cc.Circle_RectL1Ratio;
                        circleL2Ratio = cc.Circle_RectL2Ratio;
                    }
                    hasCircle = true; break;
                }
            }

            bool hasRect = ROI_Length1 > 0 && ROI_Length2 > 0;
            //260509 hbk Phase 20 — !string.IsNullOrEmpty(PolygonPoints) 는 PolygonPoints null 도 안전하게 false 처리하므로 ?? 분해 불필요
            bool hasPolygon = !string.IsNullOrEmpty(PolygonPoints);
            bool isTaught = hasRect || hasPolygon || hasCircle; //260423 hbk Phase 11 D-16 — Circle 포함

            //260509 hbk Phase 20 — FAIName null fallback "FAI" (Id/Name 공용 — 3개 RoiDefinition 분기 재사용)
            string idValue = "FAI";
            string nameValue = "FAI";
            if (FAIName != null) { idValue = FAIName; nameValue = FAIName; }

            if (!isTaught)
            {
                return new RoiDefinition
                {
                    Id = idValue,    //260509 hbk Phase 20
                    Name = nameValue, //260509 hbk Phase 20
                    IsTaught = false
                };
            }

            //260423 hbk Phase 11 D-16 — Circle 우선 반환 (Rect/Polygon 필드와 무관)
            if (hasCircle)
            {
                return new RoiDefinition
                {
                    Id = idValue,    //260509 hbk Phase 20
                    Name = nameValue, //260509 hbk Phase 20
                    Shape = RoiShape.Circle,
                    CenterRow = circleRow,    //260519 hbk Phase 31 CO-23.1-02
                    CenterCol = circleCol,    //260519 hbk Phase 31 CO-23.1-02
                    Radius = circleRadius,    //260519 hbk Phase 31 CO-23.1-02
                    CirclePolarStepDeg = circleStepDeg, //260519 hbk Phase 31 CO-23.1-02 — strip 시각화
                    CircleRectL1Ratio = circleL1Ratio,  //260519 hbk Phase 31 CO-23.1-02
                    CircleRectL2Ratio = circleL2Ratio,  //260519 hbk Phase 31 CO-23.1-02
                    IsTaught = true,
                    PixelResolutionX = PixelResolutionX,
                    PixelResolutionY = PixelResolutionY
                };
            }

            double row1 = 0, col1 = 0, row2 = 0, col2 = 0;
            if (hasRect)
            {
                double sinPhi = Math.Sin(ROI_Phi);
                double cosPhi = Math.Cos(ROI_Phi);
                double dRow = Math.Abs(ROI_Length1 * cosPhi) + Math.Abs(ROI_Length2 * sinPhi);
                double dCol = Math.Abs(ROI_Length1 * sinPhi) + Math.Abs(ROI_Length2 * cosPhi);
                row1 = ROI_Row - dRow;
                col1 = ROI_Col - dCol;
                row2 = ROI_Row + dRow;
                col2 = ROI_Col + dCol;
            }

            //260509 hbk Phase 20 — Edge 파라미터 null fallback 임시변수 분해 (RoiDefinition initializer 깨끗하게 유지)
            string edgeDirectionValue = "LtoR";
            if (EdgeDirection != null) edgeDirectionValue = EdgeDirection;
            string edgeSelectionValue = "First";
            if (EdgeSelection != null) edgeSelectionValue = EdgeSelection;
            string edgePolarityValue = "DarkToLight";
            if (EdgePolarity != null) edgePolarityValue = EdgePolarity;
            string polygonPointsValue = "";
            if (PolygonPoints != null) polygonPointsValue = PolygonPoints;

            return new RoiDefinition //260409 hbk 에지 파라미터 전달
            {
                Id = idValue,    //260509 hbk Phase 20
                Name = nameValue, //260509 hbk Phase 20
                Row1 = row1,
                Column1 = col1,
                Row2 = row2,
                Column2 = col2,
                IsTaught = true,
                Sigma = Sigma,
                EdgeThreshold = EdgeThreshold, //260409 hbk
                EdgeDirection = edgeDirectionValue, //260509 hbk Phase 20
                EdgeSelection = edgeSelectionValue, //260509 hbk Phase 20
                EdgeSampleCount = EdgeSampleCount, //260409 hbk
                EdgeTrimCount = EdgeTrimCount, //260409 hbk
                EdgePolarity = edgePolarityValue, //260509 hbk Phase 20
                PixelResolutionX = PixelResolutionX,
                PixelResolutionY = PixelResolutionY,
                PolygonPoints = polygonPointsValue //260509 hbk Phase 20
            };
        }

        //260507 hbk Phase 19 QUAL-03 — PropertyGrid 동적 노출 (EdgeMeasureType 별 필터)
        //260508 hbk Phase 19 fix — PropertyTools.Wpf PropertyGrid 는 GetProperties() 무인자만 호출. 무인자 오버로드로 hide 로직 이전.
        //  ParamBase INI 직렬화는 GetType().GetProperties() Reflection 경로 사용 → ICustomTypeDescriptor 영향 0 (ParamBase.cs L75/L325/L370).
        //  GetProperties(Attribute[]) 는 외부 사용처 안전판 — 동일 본문으로 유지.
        public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) { //260508 hbk Phase 19 fix
            return BuildFilteredProperties(attributes); //260508 hbk Phase 19 fix
        }
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() { //260508 hbk Phase 19 fix — PropertyGrid 가 호출하는 진짜 진입점
            return BuildFilteredProperties(null); //260508 hbk Phase 19 fix
        }
        private System.ComponentModel.PropertyDescriptorCollection BuildFilteredProperties(System.Attribute[] attrs) { //260508 hbk Phase 19 fix
            var sourceNames = new System.Collections.Generic.HashSet<string> { //260508 hbk Phase 19 fix
                nameof(EdgeMeasureTypeList), //260507 hbk Phase 19 QUAL-03
                nameof(EdgeDirectionList), //260507 hbk Phase 18 CO-01 패턴 — EdgeDirectionList 강제 포함
                nameof(EdgePolarityList), //260507 hbk Phase 18 CO-01 패턴 — EdgePolarityList 강제 포함
            };
            //260518 hbk #4 — 동적 FAI 모드(자식 Measurement >= 1)에서 레거시 FAI-레벨 Edge 파라미터 숨김.
            //  각 Measurement 가 자기 파라미터를 보유하므로 FAI-레벨 Edge 값은 죽은 값 → 사용자 혼란 방지.
            bool hasDynamicMeasurements = Measurements != null && Measurements.Count > 0; //260518 hbk #4
            return DynamicPropertyHelper.FilterProperties(this, attrs,
                name => IsHiddenForEdgeMeasureType(name, EdgeMeasureType)
                        || (hasDynamicMeasurements && IsLegacyEdgeParam(name)), //260518 hbk #4
                sourceNames); //260508 hbk Phase 19 fix
        }
        public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); } //260507 hbk Phase 19 QUAL-03
        public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); } //260507 hbk Phase 19 QUAL-03
        public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); } //260507 hbk Phase 19 QUAL-03
        public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); } //260507 hbk Phase 19 QUAL-03
        public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); } //260507 hbk Phase 19 QUAL-03
        public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); } //260507 hbk Phase 19 QUAL-03
        public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); } //260507 hbk Phase 19 QUAL-03
        public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); } //260507 hbk Phase 19 QUAL-03
        public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); } //260507 hbk Phase 19 QUAL-03
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; } //260507 hbk Phase 19 QUAL-03

        //260507 hbk Phase 19 QUAL-03 — EdgeMeasureType 별 숨김 규칙:
        //   CircleDiameter: EdgeDirection/EdgePolarity/EdgeSelection/EdgeSampleCount/EdgeTrimCount/Sigma + 각 List 숨김
        //   그 외 모든 타입: 숨김 없음 (EdgePairDistance, PointToLineDistance 등)
        private static bool IsHiddenForEdgeMeasureType(string name, string edgeMeasureType) { //260507 hbk Phase 19 QUAL-03
            if (edgeMeasureType == "CircleDiameter") {
                if (name == "EdgeDirection"  || name == "EdgeDirectionList")  return true; //260507 hbk Phase 19 QUAL-03
                if (name == "EdgePolarity"   || name == "EdgePolarityList")   return true; //260507 hbk Phase 19 QUAL-03
                if (name == "EdgeSelection")                                   return true; //260507 hbk Phase 19 QUAL-03
                if (name == "EdgeSampleCount")                                 return true; //260507 hbk Phase 19 QUAL-03
                if (name == "EdgeTrimCount")                                   return true; //260507 hbk Phase 19 QUAL-03
                if (name == "Sigma")                                           return true; //260507 hbk Phase 19 QUAL-03
            }
            return false;
        }

        //260518 hbk #4 — 동적 FAI 모드에서 숨길 레거시 FAI-레벨 Edge 파라미터 이름 매칭.
        //  *List 이름(EdgeMeasureTypeList 등)은 매칭하지 않는다 — ItemsSource 화이트리스트 보존 (콤보 깨짐 회피).
        //  부모 프로퍼티가 숨겨지면 List 도 화면에 노출되지 않으므로 무관.
        private static bool IsLegacyEdgeParam(string name) { //260518 hbk #4
            if (name == "EdgeMeasureType")  return true; //260518 hbk #4
            if (name == "EdgeThreshold")    return true; //260518 hbk #4
            if (name == "Sigma")            return true; //260518 hbk #4
            if (name == "EdgeDirection")    return true; //260518 hbk #4
            if (name == "EdgeSelection")    return true; //260518 hbk #4
            if (name == "EdgeSampleCount")  return true; //260518 hbk #4
            if (name == "EdgeTrimCount")    return true; //260518 hbk #4
            if (name == "EdgePolarity")     return true; //260518 hbk #4
            return false;
        }
    }
}
