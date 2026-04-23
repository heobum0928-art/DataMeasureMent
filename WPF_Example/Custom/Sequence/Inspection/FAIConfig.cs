using System;
using System.Collections.Generic; //260413 hbk Phase 6: Measurements 리스트
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Models;
using ReringProject.Utility;
//260423 hbk WR-RT-02 ComboBox 옵션 소스

namespace ReringProject.Sequence {

    public class FAIConfig : ParamBase {

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
        [Category("Edge|Measurement")]
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

        [Browsable(false)]
        public string FAIName { get; set; }

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
            bool hasRect = ROI_Length1 > 0 && ROI_Length2 > 0;
            bool hasPolygon = !string.IsNullOrEmpty(PolygonPoints); //260408 hbk Polygon ROI 지원
            bool isTaught = hasRect || hasPolygon;
            if (!isTaught)
            {
                return new RoiDefinition
                {
                    Id = FAIName ?? "FAI",
                    Name = FAIName ?? "FAI",
                    IsTaught = false
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

            return new RoiDefinition //260409 hbk 에지 파라미터 전달
            {
                Id = FAIName ?? "FAI",
                Name = FAIName ?? "FAI",
                Row1 = row1,
                Column1 = col1,
                Row2 = row2,
                Column2 = col2,
                IsTaught = true,
                Sigma = Sigma,
                EdgeThreshold = EdgeThreshold, //260409 hbk
                EdgeDirection = EdgeDirection ?? "LtoR", //260409 hbk
                EdgeSelection = EdgeSelection ?? "First", //260409 hbk
                EdgeSampleCount = EdgeSampleCount, //260409 hbk
                EdgeTrimCount = EdgeTrimCount, //260409 hbk
                EdgePolarity = EdgePolarity ?? "DarkToLight", //260409 hbk
                PixelResolutionX = PixelResolutionX,
                PixelResolutionY = PixelResolutionY,
                PolygonPoints = PolygonPoints ?? "" //260408 hbk
            };
        }
    }
}
