using System.Collections.Generic;
using System.IO;
using ReringProject.Halcon.Models;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    // Phase 80 D-80-15: '기준 ROI 시험 찾기' 버튼 1개의 결과 — 화면 표시(사진·상자·주황 선)와 결과 문구.
    public class LocalRefTestFindOutcome
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public string ImagePath { get; set; }
        public DatumConfig Datum { get; set; }
        public List<EdgeInspectionOverlay> Overlays { get; set; } = new List<EdgeInspectionOverlay>();
    }

    /// <summary>
    /// Phase 80 D-80-15: 기준 ROI(Local Ref) 티칭 시 기준점 가로 사진(z1) 위에서 띠 에지 선을 바로 확인한다.
    /// D-79-05 와 같은 사진(그 측정 기준점의 가로 사진)·같은 찾기 로직(런타임 기준점 찾기가 계산하는
    /// ComputeLocalRefLinesForDatum 경로, DatumTestFindService 를 통해)을 쓴다. 새 대화상자는 만들지 않는다
    /// (D-80-00) — 결과 한 줄은 MainView 의 기존 결과 라벨에 표시된다.
    /// </summary>
    public static class LocalRefTestFindService
    {
        public const string TITLE = "기준 ROI 시험 찾기";
        public const string MSG_FOUND_PREFIX = "띠 에지를 찾았습니다 — 에지 세기 ";
        public const string MSG_SELECT_MEASUREMENT = "측정을 먼저 고르세요";
        public const string MSG_OPTION_OFF = "국부 기준이 꺼져 있습니다 (Local Ref 탭)";
        public const string MSG_ROI_NOT_TAUGHT = "기준 ROI 가 아직 없습니다 (Local Ref 탭)";
        public const string MSG_NO_DATUM = "이 측정의 기준점을 찾지 못했습니다";
        public const string MSG_NO_PHOTO = "기준점 가로 사진이 없습니다";
        public const string MSG_DATUM_FIND_FAILED_PREFIX = "기준점을 먼저 찾지 못했습니다: ";
        public const string MSG_NOT_COMPUTED = "국부 기준선이 계산되지 않았습니다";
        public const string MSG_EDGE_NOT_FOUND_PREFIX = "기준 ROI 에서 띠 에지를 찾지 못했습니다: ";
        private const string EDGE_SCORE_FORMAT = "F1";

        public static LocalRefTestFindOutcome Run(ParamBase selectedParam)
        {
            LocalRefTestFindOutcome outcome = new LocalRefTestFindOutcome();

            EdgeToLineDistanceMeasurement etld = selectedParam as EdgeToLineDistanceMeasurement;
            if (etld == null)
            {
                outcome.Message = TITLE + " — " + MSG_SELECT_MEASUREMENT;
                return outcome;
            }
            if (!etld.IsLocalRefEnabled)
            {
                outcome.Message = TITLE + " — " + MSG_OPTION_OFF;
                return outcome;
            }
            bool bRoiTaught = etld.LocalRef_Length1 > 0 && etld.LocalRef_Length2 > 0;
            if (!bRoiTaught)
            {
                outcome.Message = TITLE + " — " + MSG_ROI_NOT_TAUGHT;
                return outcome;
            }

            InspectionSequence seq = FindOwnerSequence(etld);
            DatumConfig datum = FindDatumInSequence(seq, etld.DatumRef);
            if (datum == null)
            {
                outcome.Message = TITLE + " — " + MSG_NO_DATUM;
                return outcome;
            }
            bool bNoPhoto = string.IsNullOrEmpty(datum.TeachingImagePath) || !File.Exists(datum.TeachingImagePath);
            if (bNoPhoto)
            {
                outcome.Message = TITLE + " — " + MSG_NO_PHOTO;
                return outcome;
            }

            // 여기부터는 실패해도 사진·상자를 보여 준다 — 사용자가 상자를 옮길 수 있게(D-80-15).
            outcome.ImagePath = datum.TeachingImagePath;
            outcome.Datum = datum;

            string szError;
            bool bFound = DatumTestFindService.TryRunFromTeachingImages(seq, datum, false, out szError);
            if (!bFound)
            {
                outcome.Message = TITLE + " — " + MSG_DATUM_FIND_FAILED_PREFIX + szError;
                LogOutcome(etld, outcome.Message);
                return outcome;
            }

            LocalRefLineResult result;
            bool bHasLine = seq.TryGetLocalRefLine(etld, out result);
            if (!bHasLine)
            {
                outcome.Message = TITLE + " — " + MSG_NOT_COMPUTED;
                LogOutcome(etld, outcome.Message);
                return outcome;
            }
            if (!result.Found)
            {
                outcome.Message = TITLE + " — " + MSG_EDGE_NOT_FOUND_PREFIX + result.Error;
                LogOutcome(etld, outcome.Message);
                return outcome;
            }

            outcome.Ok = true;
            outcome.Overlays.Add(BuildRefLineOverlay(result));
            outcome.Message = TITLE + " — " + MSG_FOUND_PREFIX + result.EdgeScore.ToString(EDGE_SCORE_FORMAT);
            Logging.PrintLog((int)ELogType.Algorithm, EdgeToLineDistanceMeasurement.LOCAL_REF_LOG_TAG
                + "시험 찾기 — 중점 row=" + result.MidRow.ToString("F2") + " col=" + result.MidCol.ToString("F2")
                + " 에지세기=" + result.EdgeScore.ToString(EDGE_SCORE_FORMAT));
            return outcome;
        }

        private static void LogOutcome(EdgeToLineDistanceMeasurement etld, string szMessage)
        {
            Logging.PrintLog((int)ELogType.Algorithm, EdgeToLineDistanceMeasurement.LOCAL_REF_LOG_TAG + "시험 찾기 — " + szMessage);
        }

        // 이름으로 DatumConfig 조회 — InjectDatumOrigin 과 같은 규칙. seq 가 null 이면 null.
        private static DatumConfig FindDatumInSequence(InspectionSequence seq, string szDatumName)
        {
            if (seq == null || seq.DatumConfigs == null || string.IsNullOrEmpty(szDatumName))
            {
                return null;
            }
            foreach (DatumConfig d in seq.DatumConfigs)
            {
                if (d != null && d.DatumName == szDatumName) { return d; }
            }
            return null;
        }

        // etld 를 참조하는 Shot 을 먼저 찾고, 그 Shot 을 소유한 InspectionSequence 를 찾는다(IsShotOwnedBySequence
        //  재사용). 측정의 Owner 체인이 시퀀스까지 직접 닿지 않으므로 Shot 을 경유해야 한다.
        private static InspectionSequence FindOwnerSequence(EdgeToLineDistanceMeasurement etld)
        {
            ShotConfig ownerShot = FindOwnerShot(etld);
            if (ownerShot == null) { return null; }
            return FindSequenceOwningShot(ownerShot);
        }

        private static ShotConfig FindOwnerShot(EdgeToLineDistanceMeasurement etld)
        {
            if (SystemHandler.Handle == null || SystemHandler.Handle.Sequences == null) { return null; }
            if (SystemHandler.Handle.Sequences.RecipeManager == null) { return null; }
            List<ShotConfig> lstShots = SystemHandler.Handle.Sequences.RecipeManager.Shots;
            if (lstShots == null) { return null; }
            foreach (ShotConfig shot in lstShots)
            {
                if (ShotOwnsMeasurement(shot, etld)) { return shot; }
            }
            return null;
        }

        private static bool ShotOwnsMeasurement(ShotConfig shot, EdgeToLineDistanceMeasurement etld)
        {
            if (shot == null || shot.FAIList == null) { return false; }
            foreach (FAIConfig fai in shot.FAIList)
            {
                if (fai == null || fai.Measurements == null) { continue; }
                foreach (MeasurementBase meas in fai.Measurements)
                {
                    if (ReferenceEqualsMeasurement(meas, etld)) { return true; }
                }
            }
            return false;
        }

        private static bool ReferenceEqualsMeasurement(MeasurementBase meas, EdgeToLineDistanceMeasurement etld)
        {
            return object.ReferenceEquals(meas, etld);
        }

        private static InspectionSequence FindSequenceOwningShot(ShotConfig ownerShot)
        {
            int nCount = SystemHandler.Handle.Sequences.Count;
            for (int i = 0; i < nCount; i++)
            {
                InspectionSequence seq = SystemHandler.Handle.Sequences[i] as InspectionSequence;
                if (seq == null) { continue; }
                if (InspectionSequence.IsShotOwnedBySequence(ownerShot, seq.Name)) { return seq; }
            }
            return null;
        }

        // EdgeToLineDistanceMeasurement.cs:444-451 과 같은 모양 — 화면 주황 선(FAI-RefLine).
        private static EdgeInspectionOverlay BuildRefLineOverlay(LocalRefLineResult result)
        {
            EdgeInspectionOverlay overlay = new EdgeInspectionOverlay();
            overlay.RoiId = EdgeToLineDistanceMeasurement.LOCAL_REF_OVERLAY_ROI_ID;
            overlay.LineRow1 = result.Row1;
            overlay.LineColumn1 = result.Col1;
            overlay.LineRow2 = result.Row2;
            overlay.LineColumn2 = result.Col2;
            return overlay;
        }
    }
}
