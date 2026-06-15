using System;
using System.Collections.Generic;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Models;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    // ICustomTypeDescriptor: PropertyGrid 동적 노출용 (EdgeMeasureType 별 필터)
    public class FAIConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor {

        // 수동 직렬화, ParamBase 자동 Save/Load 제외
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<MeasurementBase> Measurements { get; private set; } = new List<MeasurementBase>();

        public MeasurementBase AddMeasurement(string typeName)
        {
            var m = MeasurementFactory.Create(typeName, this);
            if (m != null) Measurements.Add(m);
            return m;
        }

        public bool RemoveMeasurement(int index)
        {
            if (index < 0 || index >= Measurements.Count) return false;
            Measurements.RemoveAt(index);
            return true;
        }

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

        // Edge Measurement
        // EdgeMeasureType — 측정 알고리즘 선택. 저장 타입 string (ParamBase.Save/Load switch가 string 지원).
        //  유효값: MeasurementFactory.GetTypeNames() 목록. 미존재 INI 로드 시 property 기본값 유지
        //  (ParamBase.Load 가 INI 키 미존재 시 기본값 보존). 기본값은 GetTypeNames() 노출 타입과 일치해야 콤보 빈값/불일치 방지.
        [Category("Edge|Measurement")]
        [ItemsSourceProperty(nameof(EdgeMeasureTypeList))]
        public string EdgeMeasureType { get; set; } = "EdgeToLineDistance";

        // EdgeMeasureType 드롭다운 옵션 (MeasurementFactory 단일 소스, 하드코딩 금지).
        //  정적 readonly 캐시: 매번 new List 반환 시 PropertyTools.Wpf 가 콤보 ItemsSource 인식 실패.
        private static readonly List<string> _edgeMeasureTypeListCache =
            new List<string>(MeasurementFactory.GetTypeNames());
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeMeasureTypeList {
            get { return _edgeMeasureTypeListCache; }
        }

        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeDirection { get; set; } = "LtoR"; // LtoR, RtoL, TtoB, BtoT
        public string EdgeSelection { get; set; } = "First"; // First, Last, Both
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight"; // DarkToLight, LightToDark

        // PropertyGrid ComboBox 옵션 래퍼 — 공용 소스 참조
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        // Calibration: INI 호환 잔존 저장용. 소비 없음 — Shot 단일소스(D-01). PropertyGrid 숨김. //260615 hbk Phase 42 D-04/D-05
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double PixelResolutionX { get; set; } = 1.0;  // mm/pixel — INI 키 보존(D-07)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double PixelResolutionY { get; set; } = 1.0;  // mm/pixel — INI 키 보존(D-07)

        // Polygon ROI — INI 저장용으로 "x1,y1;x2,y2;x3,y3" 문자열로 직렬화
        [Category("ROI")]
        public string PolygonPoints { get; set; } = "";

        // 공차/기준값은 MeasurementBase로 단일 소스화 — FAI 레벨 중복 필드 제거
        // (NominalValue / UpperTolerance / LowerTolerance / SetResult() 삭제 — 판정은 MeasurementBase.EvaluateJudgement)

        // Result (runtime, not saved) — Action_FAIMeasurement가 Measurement 집계 결과를 써주고, TCP 응답(FAIResultData)이 읽어간다
        [Browsable(false)]
        public double MeasuredValue { get; set; }

        [Browsable(false)]
        public bool IsPass { get; set; }

        // datum-skip subtype 플래그. true 이면 FAI 가 측정 미실행(datum 검출 실패 원인).
        //  AddResponse 가 EVisionResultType.NotExist ('N') 매핑에 사용. INI 미직렬화 (runtime 전용).
        //  ClearResult / Action_FAIMeasurement EStep.Init ShotParam.ClearAllResults 에서 false 로 리셋.
        [Browsable(false)]
        public bool WasDatumSkipped { get; set; }

        // 검사 결과 overlay 저장. 노드 클릭 시 재 렌더용. 검사 시점에 Action_FAIMeasurement.EStep.Measure 가
        //  per-FAI 누적 write-back. ParamBase INI 직렬화 회피 (transient runtime 결과).
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public List<EdgeInspectionOverlay> LastOverlays { get; set; } = new List<EdgeInspectionOverlay>();

        // 검사 시점에 Action_FAIMeasurement 가 write-back 하는 캡쳐 파일명(transient).
        //  CycleResultSerializer.BuildDto 가 FaiResultDto 로 복사. INI/PropertyGrid 미노출 + JSON 직렬화 제외(LastOverlays 패턴 일치).
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public string LastOriginImageFileName { get; set; } = "";
        [System.ComponentModel.Browsable(false)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        [Newtonsoft.Json.JsonIgnore]
        public string LastCaptureImageFileName { get; set; } = "";

        // INotifyPropertyChanged 발화로 트리 헤더 즉시 갱신 (PropertyGrid 편집 → Tree).
        //  Btn_RenameFAI_Click 버튼도 동일 setter 호출.
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

        // SetResult() 제거 — 판정은 MeasurementBase.EvaluateJudgement, 집계는 Action_FAIMeasurement가 직접 수행

        public void ClearResult() {
            MeasuredValue = 0;
            IsPass = false;
            WasDatumSkipped = false;
        }

        /// <summary>
        /// Converts FAIConfig Rectangle2 params (center+half-lengths+phi) to RoiDefinition bounding box.
        /// NOTE on D-05 compatibility: ROI_Phi exists in legacy INI data from Rectangle2 era.
        /// ToRoiDefinition() uses sin/cos of ROI_Phi for backward compatibility with existing INI files.
        /// New ROI input via the Rect ROI button (Plan 02) always sets ROI_Phi=0.0 (Rectangle1 only),
        /// so D-05 "Rectangle2는 사용하지 않는다" is honored for all new user input.
        /// </summary>
        public RoiDefinition ToRoiDefinition()
        {
            // Circle ROI 렌더링 (committed circle → RoiDefinition Shape=Circle).
            //  Precedence: Circle takes priority over Rect/Polygon when present.
            //  CircleDiameter + CircleCenterDistance 두 타입 모두 Circle_* ROI 보유.
            double circleRow = 0, circleCol = 0, circleRadius = 0;
            // polar strip 시각화 파라미터 (CircleCenterDistance polar 모드만 > 0)
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
                var cc = m as CircleCenterDistanceMeasurement;
                if (cc != null && cc.Circle_Radius > 0)
                {
                    circleRow = cc.Circle_Row; circleCol = cc.Circle_Col; circleRadius = cc.Circle_Radius;
                    // polar 모드(RadialDirection 비어있지 않음)일 때만 strip 파라미터 전달
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
            bool hasPolygon = !string.IsNullOrEmpty(PolygonPoints);
            bool isTaught = hasRect || hasPolygon || hasCircle;

            // FAIName null fallback "FAI" (Id/Name 공용 — 3개 RoiDefinition 분기 재사용)
            string idValue = "FAI";
            string nameValue = "FAI";
            if (FAIName != null) { idValue = FAIName; nameValue = FAIName; }

            if (!isTaught)
            {
                return new RoiDefinition
                {
                    Id = idValue,
                    Name = nameValue,
                    IsTaught = false
                };
            }

            // Circle 우선 반환 (Rect/Polygon 필드와 무관)
            if (hasCircle)
            {
                return new RoiDefinition
                {
                    Id = idValue,
                    Name = nameValue,
                    Shape = RoiShape.Circle,
                    CenterRow = circleRow,
                    CenterCol = circleCol,
                    Radius = circleRadius,
                    CirclePolarStepDeg = circleStepDeg, // strip 시각화
                    CircleRectL1Ratio = circleL1Ratio,
                    CircleRectL2Ratio = circleL2Ratio,
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

            // Edge 파라미터 null fallback 임시변수 분해 (RoiDefinition initializer 깨끗하게 유지)
            string edgeDirectionValue = "LtoR";
            if (EdgeDirection != null) edgeDirectionValue = EdgeDirection;
            string edgeSelectionValue = "First";
            if (EdgeSelection != null) edgeSelectionValue = EdgeSelection;
            string edgePolarityValue = "DarkToLight";
            if (EdgePolarity != null) edgePolarityValue = EdgePolarity;
            string polygonPointsValue = "";
            if (PolygonPoints != null) polygonPointsValue = PolygonPoints;

            return new RoiDefinition
            {
                Id = idValue,
                Name = nameValue,
                Row1 = row1,
                Column1 = col1,
                Row2 = row2,
                Column2 = col2,
                IsTaught = true,
                Sigma = Sigma,
                EdgeThreshold = EdgeThreshold,
                EdgeDirection = edgeDirectionValue,
                EdgeSelection = edgeSelectionValue,
                EdgeSampleCount = EdgeSampleCount,
                EdgeTrimCount = EdgeTrimCount,
                EdgePolarity = edgePolarityValue,
                PixelResolutionX = PixelResolutionX,
                PixelResolutionY = PixelResolutionY,
                PolygonPoints = polygonPointsValue
            };
        }

        // PropertyGrid 동적 노출 (EdgeMeasureType 별 필터).
        //  PropertyTools.Wpf PropertyGrid 는 GetProperties() 무인자만 호출 → hide 로직을 무인자 오버로드에 둔다.
        //  ParamBase INI 직렬화는 GetType().GetProperties() Reflection 경로 사용 → ICustomTypeDescriptor 영향 없음.
        //  GetProperties(Attribute[]) 는 외부 사용처 안전판 — 동일 본문으로 유지.
        public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
            return BuildFilteredProperties(attributes);
        }
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() {
            return BuildFilteredProperties(null);
        }
        private System.ComponentModel.PropertyDescriptorCollection BuildFilteredProperties(System.Attribute[] attrs) {
            var sourceNames = new System.Collections.Generic.HashSet<string> {
                nameof(EdgeMeasureTypeList),
                nameof(EdgeDirectionList), // ItemsSource 화이트리스트 강제 포함
                nameof(EdgePolarityList),  // ItemsSource 화이트리스트 강제 포함
            };
            // 동적 FAI 모드(자식 Measurement >= 1)에서 레거시 FAI-레벨 Edge 파라미터 숨김.
            //  각 Measurement 가 자기 파라미터를 보유하므로 FAI-레벨 Edge 값은 죽은 값 → 사용자 혼란 방지.
            bool hasDynamicMeasurements = Measurements != null && Measurements.Count > 0;
            return DynamicPropertyHelper.FilterProperties(this, attrs,
                name => IsHiddenForEdgeMeasureType(name, EdgeMeasureType)
                        || (hasDynamicMeasurements && IsLegacyEdgeParam(name)),
                sourceNames);
        }
        public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); }
        public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); }
        public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); }
        public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); }
        public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); }
        public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); }
        public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); }
        public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); }
        public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); }
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; }

        // EdgeMeasureType 별 숨김 규칙:
        //   CircleDiameter: EdgeDirection/EdgePolarity/EdgeSelection/EdgeSampleCount/EdgeTrimCount/Sigma + 각 List 숨김
        //   그 외 모든 타입: 숨김 없음 (EdgePairDistance, PointToLineDistance 등)
        private static bool IsHiddenForEdgeMeasureType(string name, string edgeMeasureType) {
            if (edgeMeasureType == "CircleDiameter") {
                if (name == "EdgeDirection"  || name == "EdgeDirectionList")  return true;
                if (name == "EdgePolarity"   || name == "EdgePolarityList")   return true;
                if (name == "EdgeSelection")                                   return true;
                if (name == "EdgeSampleCount")                                 return true;
                if (name == "EdgeTrimCount")                                   return true;
                if (name == "Sigma")                                           return true;
            }
            return false;
        }

        // 동적 FAI 모드에서 숨길 레거시 FAI-레벨 Edge 파라미터 이름 매칭.
        //  *List 이름(EdgeMeasureTypeList 등)은 매칭하지 않는다 — ItemsSource 화이트리스트 보존 (콤보 깨짐 회피).
        //  부모 프로퍼티가 숨겨지면 List 도 화면에 노출되지 않으므로 무관.
        private static bool IsLegacyEdgeParam(string name) {
            if (name == "EdgeMeasureType")  return true;
            if (name == "EdgeThreshold")    return true;
            if (name == "Sigma")            return true;
            if (name == "EdgeDirection")    return true;
            if (name == "EdgeSelection")    return true;
            if (name == "EdgeSampleCount")  return true;
            if (name == "EdgeTrimCount")    return true;
            if (name == "EdgePolarity")     return true;
            return false;
        }
    }
}
