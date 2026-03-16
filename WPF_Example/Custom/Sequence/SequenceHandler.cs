using System;
using ReringProject.Define;
using ReringProject.Device;

namespace ReringProject.Sequence {
    public sealed partial class SequenceHandler {

        public const string SEQ_TOP = "TOP";
        public const string SEQ_SIDE = "SIDE";
        public const string SEQ_BOTTOM = "BOTTOM";

        public const string ACT_CALIB = "Calibration";
        public const string ACT_INSPECT = "Inspect";
        public const string ACT_SCAN = "SCAN";

        public const int Top_Alg_Index = 0;
        public const int Side_Alg_Index = 1;
        public const int Bottom_Alg_Index = 2;

        public const int Inspection_Model_Index = 0;
        public const int Calibration_Model_Index = 1;

        private void RegisterSequences() {
            SequenceBuilder.RegisterSequence(
                new TopSequence(ESequence.Top, SEQ_TOP, Top_Alg_Index, DeviceHandler.CAMERA_TOP, LightHandler.LIGHT_TOP),
                new TopSequence(ESequence.Side, SEQ_SIDE, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_SIDE),
                new BottomSequence(ESequence.Bottom, SEQ_BOTTOM, Bottom_Alg_Index, DeviceHandler.CAMERA_BOTTOM, LightHandler.LIGHT_BOTTOM)
            );
        }

        private void RegisterActions() {
            SequenceBuilder.RegisterAction(
                new TopCalibrationAction(EAction.Top_Calibration, ACT_CALIB, Top_Alg_Index, Calibration_Model_Index),
                new TopInspectionAction(EAction.Top_Inspection, ACT_INSPECT, Top_Alg_Index, Inspection_Model_Index),
                new TopCalibrationAction(EAction.Side_Calibration, ACT_CALIB, Side_Alg_Index, Calibration_Model_Index),
                new TopInspectionAction(EAction.Side_Inspection, ACT_INSPECT, Side_Alg_Index, Inspection_Model_Index),
                new BottomCalibrationAction(EAction.Bottom_Calibration, ACT_CALIB, Bottom_Alg_Index, Calibration_Model_Index),
                new BottomInspectionAction(EAction.Bottom_Inspection, ACT_INSPECT, Bottom_Alg_Index, Inspection_Model_Index)
            );
        }

        private void InitializeSequences() {
            SequenceBuilder seq;

            seq = SequenceBuilder.CreateSequence(ESequence.Top);
            seq.AddAction(EAction.Top_Inspection);
            RegisterSequence(seq);

            seq = SequenceBuilder.CreateSequence(ESequence.Side);
            seq.AddAction(EAction.Side_Inspection);
            RegisterSequence(seq);

            seq = SequenceBuilder.CreateSequence(ESequence.Bottom);
            seq.AddAction(EAction.Bottom_Inspection);
            RegisterSequence(seq);
        }
    }
}


