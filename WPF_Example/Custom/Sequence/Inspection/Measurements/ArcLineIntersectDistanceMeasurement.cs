//260521 hbk Phase 32 — I9/I10-redesign: 4-ROI 두 교점 평균 방식
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// EdgeA1/EdgeB1(교점1용) 과 EdgeA2/EdgeB2(교점2용) 4개 ROI 에서 각각 직선을 피팅하고,
    /// TryIntersectLines 로 교점1/교점2를 산출한다.
    /// 측정점 = 측정축 방향 좌표는 교점2(거리 끝점), 수직축 좌표는 두 교점 평균.
    /// 측정점을 Datum 기준선까지의 거리(mm)로 환산한다.
    /// I9/I10(SOP): 기본 MeasureAxis="X" → measurePointCol=교점2.Col, measurePointRow=(교점1.Row+교점2.Row)/2.
    //260521 hbk Phase 32 UAT — 단순 양축 평균 → 측정축은 교점2, 수직축만 평균으로 정정
    /// 어느 ROI 피팅 또는 교점 산출이 실패해도 false 반환 — 측정값 '—', 앱 무크래시 (T-32-14/T-32-15 mitigation).
    /// </summary>
    public class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260521 hbk Phase 32
    {
        public override string TypeName { get { return "ArcLineIntersectDistance"; } } //260521 hbk Phase 32

        // ── 교점1 ROI 필드 ─────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — 교점1 ROI 필드
        [Category("교점1|EdgeA1-ROI")] //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA1_Row { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA1_Col { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA1_Phi { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA1_Length1 { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA1_Length2 { get; set; } //260521 hbk Phase 32 I9/I10-redesign

        //260521 hbk Phase 32 I9/I10-redesign — 교점1 수평 에지 ROI
        [Category("교점1|EdgeB1-ROI")] //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB1_Row { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB1_Col { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB1_Phi { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB1_Length1 { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB1_Length2 { get; set; } //260521 hbk Phase 32 I9/I10-redesign

        // ── 교점2 ROI 필드 ─────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — 교점2 수직 에지 ROI
        [Category("교점2|EdgeA2-ROI")] //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA2_Row { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA2_Col { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA2_Phi { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA2_Length1 { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA2_Length2 { get; set; } //260521 hbk Phase 32 I9/I10-redesign

        //260521 hbk Phase 32 I9/I10-redesign — 교점2 수평 에지 ROI
        [Category("교점2|EdgeB2-ROI")] //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB2_Row { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB2_Col { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB2_Phi { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB2_Length1 { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB2_Length2 { get; set; } //260521 hbk Phase 32 I9/I10-redesign

        // ── 교점1 EdgeA1 Edge 파라미터 (수직 에지 — 수평 스캔) ─────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — EdgeA1 = 수직 에지 검출 → 스캔 방향 기본값 "LtoR"
        [Category("교점1|EdgeA1-Edge")] //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeA1_Threshold { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA1_Sigma { get; set; } = 1.0; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeA1_SampleCount { get; set; } = 20; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeA1_TrimCount { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeA1_Polarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeA1_Direction { get; set; } = "LtoR"; //260521 hbk Phase 32 I9/I10-redesign — 수직 에지 → 수평 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeA1_Selection { get; set; } = "All"; //260521 hbk Phase 32 I9/I10-redesign

        // ── 교점1 EdgeB1 Edge 파라미터 (수평 에지 — 수직 스캔) ─────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — EdgeB1 = 수평 에지 검출 → 스캔 방향 기본값 "TtoB"
        [Category("교점1|EdgeB1-Edge")] //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeB1_Threshold { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB1_Sigma { get; set; } = 1.0; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeB1_SampleCount { get; set; } = 20; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeB1_TrimCount { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeB1_Polarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeB1_Direction { get; set; } = "TtoB"; //260521 hbk Phase 32 I9/I10-redesign — 수평 에지 → 수직 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeB1_Selection { get; set; } = "All"; //260521 hbk Phase 32 I9/I10-redesign

        // ── 교점2 EdgeA2 Edge 파라미터 (수직 에지 — 수평 스캔) ─────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — EdgeA2 = 수직 에지 검출 → 스캔 방향 기본값 "LtoR"
        [Category("교점2|EdgeA2-Edge")] //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeA2_Threshold { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeA2_Sigma { get; set; } = 1.0; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeA2_SampleCount { get; set; } = 20; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeA2_TrimCount { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeA2_Polarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeA2_Direction { get; set; } = "LtoR"; //260521 hbk Phase 32 I9/I10-redesign — 수직 에지 → 수평 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeA2_Selection { get; set; } = "All"; //260521 hbk Phase 32 I9/I10-redesign

        // ── 교점2 EdgeB2 Edge 파라미터 (수평 에지 — 수직 스캔) ─────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — EdgeB2 = 수평 에지 검출 → 스캔 방향 기본값 "TtoB"
        [Category("교점2|EdgeB2-Edge")] //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeB2_Threshold { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        public double EdgeB2_Sigma { get; set; } = 1.0; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeB2_SampleCount { get; set; } = 20; //260521 hbk Phase 32 I9/I10-redesign
        public int EdgeB2_TrimCount { get; set; } = 10; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeB2_Polarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeB2_Direction { get; set; } = "TtoB"; //260521 hbk Phase 32 I9/I10-redesign — 수평 에지 → 수직 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260521 hbk Phase 32 I9/I10-redesign
        public string EdgeB2_Selection { get; set; } = "All"; //260521 hbk Phase 32 I9/I10-redesign

        //260521 hbk Phase 32 I9/I10-redesign — PropertyGrid ComboBox 옵션 래퍼 (4그룹 공유)
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260521 hbk Phase 32 I9/I10-redesign

        // ── MeasureAxis ───────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — I9/I10 = Datum C X 방향이므로 기본값 "X"
        [Category("Measurement|Measure")] //260521 hbk Phase 32 I9/I10-redesign
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260521 hbk Phase 32 I9/I10-redesign
        public string MeasureAxis { get; set; } = "X"; //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        public List<string> MeasureAxisList { get { return new List<string> { "X", "Y" }; } } //260521 hbk Phase 32 I9/I10-redesign

        // ── IDatumOriginConsumer transient 필드 ──────────────────────────────────────
        //260521 hbk Phase 32 I9/I10-redesign — datum 좌표 runtime 주입 전용. PropertyGrid 미표시, JSON 직렬화 제외.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32 I9/I10-redesign
        public double DatumOriginRow { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32 I9/I10-redesign
        public double DatumOriginCol { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32 I9/I10-redesign
        public double DatumAngleRad { get; set; } //260521 hbk Phase 32 I9/I10-redesign — datum 1차(수평) 기준선 각도
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32 I9/I10-redesign
        public double DatumAngle2Rad { get; set; } //260521 hbk Phase 32 I9/I10-redesign — datum 2차(수직) 기준선 각도. X축 측정 기준.
        //260521 hbk Phase 32 I9/I10-redesign — IDatumOriginConsumer 확장. ArcLineIntersect 미사용 (E2 전용) — 주입만 받음.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32 I9/I10-redesign
        public double DatumDetectedCircleRow { get; set; } //260521 hbk Phase 32 I9/I10-redesign
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 I9/I10-redesign
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32 I9/I10-redesign
        public double DatumDetectedCircleCol { get; set; } //260521 hbk Phase 32 I9/I10-redesign

        public ArcLineIntersectDistanceMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32 I9/I10-redesign

        public override bool TryExecute( //260521 hbk Phase 32 I9/I10-redesign
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260521 hbk Phase 32 I9/I10-redesign

            var svc = new VisionAlgorithmService(); //260521 hbk Phase 32 I9/I10-redesign

            // (1) EdgeA1 ROI — 교점1 수직 에지 직선 피팅 (EdgeSelection: 사용자 선택값, 기본 "All")
            double a1r1, a1c1, a1r2, a1c2; //260521 hbk Phase 32 I9/I10-redesign
            if (!svc.TryFitLine(image,
                EdgeA1_Row, EdgeA1_Col, EdgeA1_Phi, EdgeA1_Length1, EdgeA1_Length2,
                datumTransform,
                EdgeA1_SampleCount, EdgeA1_TrimCount, EdgeA1_Sigma, EdgeA1_Threshold, //260521 hbk Phase 32 I9/I10-redesign
                EdgeA1_Direction, EdgeA1_Polarity, //260521 hbk Phase 32 I9/I10-redesign
                out a1r1, out a1c1, out a1r2, out a1c2, out error,
                EdgeA1_Selection)) //260521 hbk Phase 32 I9/I10-redesign
            {
                return false;
            }

            // (2) EdgeB1 ROI — 교점1 수평 에지 직선 피팅
            double b1r1, b1c1, b1r2, b1c2; //260521 hbk Phase 32 I9/I10-redesign
            if (!svc.TryFitLine(image,
                EdgeB1_Row, EdgeB1_Col, EdgeB1_Phi, EdgeB1_Length1, EdgeB1_Length2,
                datumTransform,
                EdgeB1_SampleCount, EdgeB1_TrimCount, EdgeB1_Sigma, EdgeB1_Threshold, //260521 hbk Phase 32 I9/I10-redesign
                EdgeB1_Direction, EdgeB1_Polarity, //260521 hbk Phase 32 I9/I10-redesign
                out b1r1, out b1c1, out b1r2, out b1c2, out error,
                EdgeB1_Selection)) //260521 hbk Phase 32 I9/I10-redesign
            {
                return false;
            }

            // (3) EdgeA2 ROI — 교점2 수직 에지 직선 피팅
            double a2r1, a2c1, a2r2, a2c2; //260521 hbk Phase 32 I9/I10-redesign
            if (!svc.TryFitLine(image,
                EdgeA2_Row, EdgeA2_Col, EdgeA2_Phi, EdgeA2_Length1, EdgeA2_Length2,
                datumTransform,
                EdgeA2_SampleCount, EdgeA2_TrimCount, EdgeA2_Sigma, EdgeA2_Threshold, //260521 hbk Phase 32 I9/I10-redesign
                EdgeA2_Direction, EdgeA2_Polarity, //260521 hbk Phase 32 I9/I10-redesign
                out a2r1, out a2c1, out a2r2, out a2c2, out error,
                EdgeA2_Selection)) //260521 hbk Phase 32 I9/I10-redesign
            {
                return false;
            }

            // (4) EdgeB2 ROI — 교점2 수평 에지 직선 피팅
            double b2r1, b2c1, b2r2, b2c2; //260521 hbk Phase 32 I9/I10-redesign
            if (!svc.TryFitLine(image,
                EdgeB2_Row, EdgeB2_Col, EdgeB2_Phi, EdgeB2_Length1, EdgeB2_Length2,
                datumTransform,
                EdgeB2_SampleCount, EdgeB2_TrimCount, EdgeB2_Sigma, EdgeB2_Threshold, //260521 hbk Phase 32 I9/I10-redesign
                EdgeB2_Direction, EdgeB2_Polarity, //260521 hbk Phase 32 I9/I10-redesign
                out b2r1, out b2c1, out b2r2, out b2c2, out error,
                EdgeB2_Selection)) //260521 hbk Phase 32 I9/I10-redesign
            {
                return false;
            }

            // (5) 교점1 = TryIntersectLines(A1, B1). 평행/근접 시 false (T-32-15 mitigation)
            double int1Row, int1Col; //260521 hbk Phase 32 I9/I10-redesign
            if (!VisionAlgorithmService.TryIntersectLines(
                a1r1, a1c1, a1r2, a1c2,
                b1r1, b1c1, b1r2, b1c2,
                out int1Row, out int1Col)) //260521 hbk Phase 32 I9/I10-redesign
            {
                error = "교점1 산출 실패 (평행 또는 근접 에지)"; //260521 hbk Phase 32 I9/I10-redesign
                return false;
            }

            // (6) 교점2 = TryIntersectLines(A2, B2). 평행/근접 시 false (T-32-15 mitigation)
            double int2Row, int2Col; //260521 hbk Phase 32 I9/I10-redesign
            if (!VisionAlgorithmService.TryIntersectLines(
                a2r1, a2c1, a2r2, a2c2,
                b2r1, b2c1, b2r2, b2c2,
                out int2Row, out int2Col)) //260521 hbk Phase 32 I9/I10-redesign
            {
                error = "교점2 산출 실패 (평행 또는 근접 에지)"; //260521 hbk Phase 32 I9/I10-redesign
                return false;
            }

            // (7) 측정점 보정 — 측정축 방향 좌표는 교점2(거리 끝점), 수직축 좌표는 두 교점 평균.
            // MeasureAxis X(수평=Col): measurePointCol = 교점2.Col, measurePointRow = (교점1.Row+교점2.Row)/2
            // MeasureAxis Y(수직=Row): measurePointRow = 교점2.Row, measurePointCol = (교점1.Col+교점2.Col)/2
            //260521 hbk Phase 32 UAT — 단순 양축 평균 → 측정축은 교점2, 수직축만 평균으로 정정
            double measurePointRow, measurePointCol;
            if (MeasureAxis == "X")
            {
                measurePointCol = int2Col; //260521 hbk Phase 32 UAT — 측정축(X=Col)은 교점2 값
                measurePointRow = (int1Row + int2Row) / 2.0; //260521 hbk Phase 32 UAT — 수직축(Row)은 두 교점 평균
            }
            else
            {
                measurePointRow = int2Row; //260521 hbk Phase 32 UAT — 측정축(Y=Row)은 교점2 값
                measurePointCol = (int1Col + int2Col) / 2.0; //260521 hbk Phase 32 UAT — 수직축(Col)은 두 교점 평균
            }

            // (8) Datum 거리 — foot 반환 오버로드 사용 (overlay FAI-DistLine 용)
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260521 hbk Phase 32 I9/I10-redesign
            double footRow, footCol; //260521 hbk Phase 32 I9/I10-redesign
            bool footOk; //260521 hbk Phase 32 I9/I10-redesign
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                measurePointRow, measurePointCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk); //260521 hbk Phase 32 I9/I10-redesign

            // overlay — 알고리즘이 이미 계산한 변수만 재사용. HALCON 재호출 없음. //260521 hbk Phase 32 I9/I10-redesign
            // 교점1 에지 라인 2개
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-Edge1", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = a1r1, LineColumn1 = a1c1, //260521 hbk Phase 32 I9/I10-redesign
                LineRow2 = a1r2, LineColumn2 = a1c2, //260521 hbk Phase 32 I9/I10-redesign
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = (a1r1 + a1r2) / 2.0, Column = (a1c1 + a1c2) / 2.0 } //260521 hbk Phase 32 I9/I10-redesign
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-Edge2", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = b1r1, LineColumn1 = b1c1, //260521 hbk Phase 32 I9/I10-redesign
                LineRow2 = b1r2, LineColumn2 = b1c2, //260521 hbk Phase 32 I9/I10-redesign
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = (b1r1 + b1r2) / 2.0, Column = (b1c1 + b1c2) / 2.0 } //260521 hbk Phase 32 I9/I10-redesign
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            // 교점2 에지 라인 2개
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-Edge3", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = a2r1, LineColumn1 = a2c1, //260521 hbk Phase 32 I9/I10-redesign
                LineRow2 = a2r2, LineColumn2 = a2c2, //260521 hbk Phase 32 I9/I10-redesign
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = (a2r1 + a2r2) / 2.0, Column = (a2c1 + a2c2) / 2.0 } //260521 hbk Phase 32 I9/I10-redesign
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-Edge4", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = b2r1, LineColumn1 = b2c1, //260521 hbk Phase 32 I9/I10-redesign
                LineRow2 = b2r2, LineColumn2 = b2c2, //260521 hbk Phase 32 I9/I10-redesign
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = (b2r1 + b2r2) / 2.0, Column = (b2c1 + b2c2) / 2.0 } //260521 hbk Phase 32 I9/I10-redesign
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            // 교점1 마커 (점 마커: LineRow1==LineRow2, LineColumn1==LineColumn2)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-Intersection1", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = int1Row, LineColumn1 = int1Col, //260521 hbk Phase 32 I9/I10-redesign
                LineRow2 = int1Row, LineColumn2 = int1Col, //260521 hbk Phase 32 I9/I10-redesign — 점 마커 (길이 0 라인)
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col } //260521 hbk Phase 32 I9/I10-redesign
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            // 교점2 마커
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-Intersection2", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = int2Row, LineColumn1 = int2Col, //260521 hbk Phase 32 I9/I10-redesign
                LineRow2 = int2Row, LineColumn2 = int2Col, //260521 hbk Phase 32 I9/I10-redesign — 점 마커
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col } //260521 hbk Phase 32 I9/I10-redesign
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            // 보정된 측정점 마커 — 측정축=교점2, 수직축=두 교점 평균 (UAT 정정)
            //260521 hbk Phase 32 UAT — FAI-AvgPoint 마커를 보정 측정점(measurePointRow/Col) 으로 갱신
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
            {
                RoiId = "FAI-AvgPoint", //260521 hbk Phase 32 I9/I10-redesign
                LineRow1 = measurePointRow, LineColumn1 = measurePointCol, //260521 hbk Phase 32 UAT — 보정 측정점
                LineRow2 = measurePointRow, LineColumn2 = measurePointCol, //260521 hbk Phase 32 UAT — 점 마커 (길이 0 라인)
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                {
                    new EdgeInspectionPoint { Row = measurePointRow, Column = measurePointCol } //260521 hbk Phase 32 UAT
                }
            }); //260521 hbk Phase 32 I9/I10-redesign
            // Datum 거리선 — 보정 측정점 → 수선의 발 (footOk 가드)
            if (footOk) //260521 hbk Phase 32 I9/I10-redesign
            {
                overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-redesign
                {
                    RoiId = "FAI-DistLine", //260521 hbk Phase 32 I9/I10-redesign
                    LineRow1 = footRow, LineColumn1 = footCol, //260521 hbk Phase 32 I9/I10-redesign — 수선의 발
                    LineRow2 = measurePointRow, LineColumn2 = measurePointCol, //260521 hbk Phase 32 UAT — 보정 측정점
                    Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-redesign
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol }, //260521 hbk Phase 32 I9/I10-redesign
                        new EdgeInspectionPoint { Row = measurePointRow, Column = measurePointCol } //260521 hbk Phase 32 UAT
                    }
                }); //260521 hbk Phase 32 I9/I10-redesign
            }

            return true;
        }
    }
}
