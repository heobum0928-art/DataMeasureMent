using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using HalconDotNet;
using OpenCvSharp;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
using ReringProject.Halcon.Services;

using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.Utility;
using ReringProject.UI;

namespace ReringProject.Sequence {

    public class BottomDieInfo {
        public double DieCenter_X { get; set; }
        public double DieCenter_Y { get; set; }
        public double DieAngle { get; set; }
        public int ContourCount { get; set; }
        public double ContourArea { get; set; }
        public int ApexCount { get; set; }
        public bool Judgment { get; set; }
        public int newJudgment { get; set; }
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public Mat image { get; set; }
    }

    public class BottomInspectionContext : ActionContext {
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public double dAngle { get; set; }
        public int DieCenter_X { get; set; }
        public int DieCenter_Y { get; set; }
        public double Die_Area { get; set; }
        public double Die_Area_Min { get; set; }
        public double Die_Area_Max { get; set; }
        public EVisionResultType InspectResult { get; set; }
        public double ScreenCenter_X { get; set; }
        public double ScreenCenter_Y { get; set; }
        public string SeqName { get; set; }
        public double Cal_XBase { get; set; }
        public double Cal_YBase { get; set; }
        public double[] X_Picker_CalValue = new double[10];
        public double[] Y_Picker_CalValue = new double[10];
        public int CalSelector { get; set; }
        public double[] CenterOffsetXmmArray { get; set; } = new double[10];
        public double[] CenterOffsetYmmArray { get; set; } = new double[10];
        public double[] dAngleArray { get; set; } = new double[10];
        public double[] DieCenter_XArray { get; set; } = new double[10];
        public double[] DieCenter_YArray { get; set; } = new double[10];
        public EVisionResultType[] InspectResultArray { get; set; } = new EVisionResultType[10];
        public bool bFinish { get; set; }
        public string ProcessName { get; set; }
        public int GrabCount { get; set; }
        public string strParamBaseName { get; set; }
        public string strActionBaseName { get; set; }
        public string strID { get; set; }
        public Dictionary<int, BottomDieInfo> BottomDie = new Dictionary<int, BottomDieInfo>();
        public System.Windows.Rect ROI_Rect { get; set; }
        public System.Windows.Rect Inner_ROI_Rect { get; set; }
       // public Mat DrawResult = new Mat();
        public int PickerNum { get; set; }
        public EVisionResultType ModelFinderResult { get; set; }
        public Mat mask = new Mat(2048, 2448, MatType.CV_8UC1);
        public double Model_ArcLength { get; set; }
        public double Min_ArcLength { get; set; }
        public double Particle_Size { get; set; }

        public BottomInspectionContext(ActionBase source) : base(source) { }

        public override void Clear() {
            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;
            dAngle = 0;
            DieCenter_X = 0;
            DieCenter_Y = 0;
            Die_Area = 0;
            Die_Area_Min = 0;
            Die_Area_Max = 0;
            InspectResult = EVisionResultType.NG;
            ScreenCenter_X = 0;
            ScreenCenter_Y = 0;
            SeqName = null;
            Cal_XBase = 0;
            Cal_YBase = 0;
            CalSelector = 0;
            bFinish = false;
            ProcessName = "None";
            GrabCount = 1;
            BottomDie.Clear();
            PickerNum = 0;
            for (var i = 0; i < 10; i++) {
                X_Picker_CalValue[i] = 0;
                Y_Picker_CalValue[i] = 0;
                CenterOffsetXmmArray[i] = 0;
                CenterOffsetYmmArray[i] = 0;
                dAngleArray[i] = 0;
                DieCenter_XArray[i] = 0;
                DieCenter_YArray[i] = 0;
                InspectResultArray[i] = EVisionResultType.NotExist;
            }
            base.Clear();
        }
    }

    public class BottomInspectionParam : CameraSlaveParam, IHalconTeachingProvider, IOfflineImageParam {
        private readonly string _jobName;
        private string _latestImagePath;
        private HImage _latestHalconImage;
        private TeachingJob _teachingJob;

        [Category("Bottom Inspection")]
        [Rectangle, Converter(typeof(UI.RectConverter))]
        public System.Windows.Rect ROI { get; set; }

        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        public ModelFinderViewModel ModuleModel { get; set; } = new ModelFinderViewModel("Teaching", EAlgorithmType.ModelFinder);

        [DisplayName("Process Name")]
        public string ProcessName { get; set; }

        [DisplayName("Picker Count")]
        public int Grab_Count { get; set; } = 1;

        public enum CalibrationBase {
            ScreenCenter = 0,
            Picker = 1,
        }

        [DisplayName("Calibration Base")]
        public CalibrationBase CalBase { get; set; }

        // true: 소프트웨어 트리거 (개발/테스트용, PLC 없이 동작)
        // false: 하드웨어 트리거 Hardware_Line0 (실제 장비, PLC 신호 대기)
        [DisplayName("Debug Check (Software Trigger)")]
        public bool DebugCheck { get; set; } = false;

        public BottomInspectionParam(object owner, int algIndex, int modelIndex) : base(owner) {
            _jobName = owner is ActionBase action ? action.Name : "BottomInspection";
            Module = new ModelFinderView(ModuleModel);
            Module.RegisterNewButtonClick(OpenTeachingButton_Click);
            Module.RegisterEditButtonClick(OpenTeachingButton_Click);
            Module.RegisterLoadButtonClick(LoadTeachingButton_Click);
        }

        public string TeachingFilePath {
            get {
                return HalconTeachingHelper.BuildFixedTeachingPath(SequenceName ?? DeviceName ?? _jobName);
            }
        }


        public TeachingJob TeachingJob {
            get {
                if (_teachingJob == null) {
                    _teachingJob = HalconTeachingHelper.LoadJob(TeachingFilePath) ?? HalconTeachingHelper.CreateDefaultJob(_jobName, ROI);
                }
                return _teachingJob;
            }
        }
        public IEnumerable<RoiDefinition> GetViewerRois() {
            return TeachingJob?.Rois ?? Enumerable.Empty<RoiDefinition>();
        }

        public override bool Load(IniFile loadFile, string group) {
            var result = base.Load(loadFile, group);
            _teachingJob = HalconTeachingHelper.LoadJob(TeachingFilePath) ?? HalconTeachingHelper.CreateDefaultJob(_jobName, ROI);
            SyncTeachingBounds();
            return result;
        }

        public override bool Save(IniFile saveFile, string group) {
            if (_teachingJob != null) {
                HalconTeachingHelper.SaveJob(TeachingFilePath, _teachingJob);
            }
            return base.Save(saveFile, group);
        }

        public override void PutImage(HImage image) {
            _latestHalconImage?.Dispose();
            _latestHalconImage = image == null ? null : image.CopyImage();
            _latestImagePath = null;
            if (_teachingJob != null) {
                _teachingJob.ImagePath = null;
            }
        }

        public string GetLatestImagePath() {
            if (string.IsNullOrWhiteSpace(_latestImagePath) && _latestHalconImage != null) {
                _latestImagePath = HalconTeachingHelper.SaveTempImage(SequenceName + "_" + ActionName, _latestHalconImage);
                if (_teachingJob != null) {
                    _teachingJob.ImagePath = _latestImagePath;
                }
            }

            return _latestImagePath;
        }

        public void SetLatestImagePath(string imagePath) {
            _latestHalconImage?.Dispose();
            _latestHalconImage = null;
            _latestImagePath = imagePath;
            if (_teachingJob != null) {
                _teachingJob.ImagePath = _latestImagePath;
            }
        }

        private string GrabTeachingImage() {
            var image = SystemHandler.Handle.Devices.GrabHalconImage(this);
            PutImage(image);
            return GetLatestImagePath();
        }
        private void OpenTeachingButton_Click(object sender, RoutedEventArgs e) {
            var window = new TeachingWindow { Owner = Application.Current?.MainWindow };
            window.ImageGrabber = GrabTeachingImage;
            if (!string.IsNullOrWhiteSpace(_latestImagePath) && File.Exists(_latestImagePath)) {
                window.LoadImage(_latestImagePath);
            }
            else if (_teachingJob != null && !string.IsNullOrWhiteSpace(_teachingJob.ImagePath) && File.Exists(_teachingJob.ImagePath)) {
                window.LoadImage(_teachingJob.ImagePath);
            }
            window.SetTeaching(HalconTeachingHelper.CloneJob(TeachingJob) ?? HalconTeachingHelper.CreateDefaultJob(_jobName, ROI));
            window.TeachingApplied += (s, job) => {
                _teachingJob = HalconTeachingHelper.CloneJob(job);
                if (_teachingJob != null) {
                    _teachingJob.JobName = _jobName;
                    if (!string.IsNullOrWhiteSpace(job.ImagePath) && File.Exists(job.ImagePath)) {
                        _latestImagePath = job.ImagePath;
                    }
                    _teachingJob.ImagePath = _latestImagePath;
                    HalconTeachingHelper.SaveJob(TeachingFilePath, _teachingJob);
                    SyncTeachingBounds();
                }
            };
            window.ShowDialog();
        }

        private void LoadTeachingButton_Click(object sender, RoutedEventArgs e) {
            var teachingFilePath = TeachingFilePath;
            var dialog = new Microsoft.Win32.OpenFileDialog {
                Filter = "Teaching Json (*.json)|*.json",
                InitialDirectory = HalconTeachingHelper.GetTeachingDialogDirectory(teachingFilePath),
                FileName = Path.GetFileName(teachingFilePath)
            };
            if (dialog.ShowDialog() != true) {
                return;
            }

            var loadedJob = HalconTeachingHelper.LoadJob(dialog.FileName);
            if (loadedJob == null) {
                CustomMessageBox.Show("Fail to Load Teaching", dialog.FileName, MessageBoxImage.Error);
                return;
            }

            _teachingJob = loadedJob;
            if (_teachingJob != null) {
                _teachingJob.JobName = _jobName;
                HalconTeachingHelper.SaveJob(TeachingFilePath, _teachingJob);
            }
            SyncTeachingBounds();
        }

        private void SyncTeachingBounds() {
            var bounds = HalconTeachingHelper.BuildBounds(_teachingJob?.Rois);
            if (!bounds.IsEmpty) {
                ROI = bounds;
                ModuleModel.Master = bounds;
            }
        }
    }

    //260526 hbk Phase 33 — Action_FAIMeasurement 로 마이그레이션됨 (D-05)
    [System.Obsolete("Phase 33 — Action_FAIMeasurement 로 마이그레이션됨", false)]
    public class BottomInspectionAction : ActionBase {
        private readonly RoiLineIntersectionAlgorithm _algorithm = new RoiLineIntersectionAlgorithm();
        private BottomInspectionContext pMyContext;
        private BottomInspectionParam pMyParam;
        private VirtualCamera pCamera;

        // 스텝 정의
        // Init        : 트리거 모드 설정 및 초기화
        // Grab        : 소프트웨어 트리거 그랩 (DebugCheck=true)
        // TriggerReady: 하드웨어 트리거 대기 및 그랩 (DebugCheck=false)
        // End         : 전체 결과 취합 및 Pass/Fail 결정
        private enum EStep { Init = 0, Grab, TriggerReady, End }

        private int ImageGrabIndex = 0;

        public BottomInspectionAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            Context = new BottomInspectionContext(this);
            pMyContext = Context as BottomInspectionContext;
            Param = new BottomInspectionParam(this, algIndex, modelIndex);
            pMyParam = Param as BottomInspectionParam;
        }

        public override void OnLoad() {
            pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
            if (pCamera != null && pCamera.Properties != null) {
                pCamera.Properties.ApplyFromParam(pMyParam);
                // 트리거 모드는 Run()의 Init 스텝에서 설정
            }
            base.OnLoad();
        }

        public override ActionContext Run() {
            switch ((EStep)Step) {

                // ── Init ─────────────────────────────────────────────────────
                case EStep.Init:
                    if (pCamera == null) {
                        Logging.PrintLog((int)ELogType.Error, "{0} Camera Handle is null!", pMyParam.DeviceName);
                        pMyContext.InspectResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    ImageGrabIndex = 0;
                    pMyContext.GrabCount = Math.Max(1, Math.Min(10, pMyParam.Grab_Count));
                    pMyContext.ProcessName = pMyParam.ProcessName;

                    if (pMyParam.DebugCheck) {
                        // 소프트웨어 트리거 모드 (PLC 없이 테스트)
                        if (!pCamera.SetSoftwareTriggerMode()) {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Software Trigger mode Fail!", pCamera.Name);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        Step = (int)EStep.Grab;
                    } else {
                        // 하드웨어 트리거 모드 (실제 장비, PLC Line0 신호 대기)
                        if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0)) {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Hardware Trigger mode Fail!", pCamera.Name);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        Step = (int)EStep.TriggerReady;
                    }
                    break;

                // ── Grab (소프트웨어 트리거) ──────────────────────────────────
                case EStep.Grab: {
                    SystemHandler.Handle.Lights.ApplyLight(pMyParam);
                    var image = pCamera.GrabHalconImage();
                    try {
                        if (image == null) {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Image Grab Failed!", pMyParam.DeviceName);
                            pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.NG;
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        // 첫 번째 이미지만 원본 저장/표시용으로 보관
                        if (ImageGrabIndex == 0) {
                            QueueRawImageSave(image);                   // 내부 CopyImage → 저장 완료 후 Dispose
                            pMyParam.PutImage(image);                   // 내부 CopyImage → 다음 PutImage 시 Dispose
                            pMyContext.ResultHalconImage?.Dispose();
                            pMyContext.ResultHalconImage = image.CopyImage(); // 내부 CopyImage → 다음 Run 시 Dispose
                        }
                        if (!RunAlgorithm(image, null, ImageGrabIndex)) {
                            FinishAction(EContextResult.Fail);
                            break;
                        }
                        ImageGrabIndex++;
                        if (ImageGrabIndex >= pMyContext.GrabCount)
                            Step = (int)EStep.End;
                    } finally {
                        image?.Dispose(); // 원본 해제 - 모든 경로에서 보장
                    }
                    break;
                }

                // ── TriggerReady (하드웨어 트리거) ───────────────────────────
                case EStep.TriggerReady: {
                    // PLC Line0 신호를 기다려 이미지 수신 (최대 5초)
                    var image = pCamera.WaitForHalconTrigger(clone: true, timeOut: 5000);
                    try {
                        if (image == null) {
                            Logging.PrintLog((int)ELogType.Error, "{0} Hardware Trigger Timeout or Grab Failed!", pMyParam.DeviceName);
                            pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.NG;
                            // 하드웨어 트리거 재설정 후 에러 반환
                            pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        // 첫 번째 이미지만 원본 저장/표시용으로 보관
                        if (ImageGrabIndex == 0) {
                            QueueRawImageSave(image);                   // 내부 CopyImage → 저장 완료 후 Dispose
                            pMyParam.PutImage(image);                   // 내부 CopyImage → 다음 PutImage 시 Dispose
                            pMyContext.ResultHalconImage?.Dispose();
                            pMyContext.ResultHalconImage = image.CopyImage(); // 내부 CopyImage → 다음 Run 시 Dispose
                        }
                        if (!RunAlgorithm(image, null, ImageGrabIndex)) {
                            FinishAction(EContextResult.Fail);
                            break;
                        }
                        ImageGrabIndex++;
                        if (ImageGrabIndex >= pMyContext.GrabCount)
                            Step = (int)EStep.End;
                    } finally {
                        image?.Dispose(); // 원본 해제 - 모든 경로에서 보장
                    }
                    break;
                }

                // ── End ──────────────────────────────────────────────────────
                case EStep.End:
                    pMyContext.bFinish = true;
                    // ImageGrabIndex 개수 중 NG가 하나라도 있으면 Fail
                    int ngCount = 0;
                    for (int i = 0; i < ImageGrabIndex; i++) {
                        if (pMyContext.InspectResultArray[i] == EVisionResultType.NG)
                            ngCount++;
                    }
                    FinishAction(ngCount == 0 ? EContextResult.Pass : EContextResult.Fail);
                    break;
            }
            return Context;
        }

        // 알고리즘 실행 및 결과를 index 슬롯에 저장
        // 첫 번째 이미지(index=0)의 결과를 Context 대표값으로도 저장
        private bool RunAlgorithm(HImage image, string imagePath, int index) {
            HTuple widthValue, heightValue;
            if (image != null) {
                image.GetImageSize(out widthValue, out heightValue);
            } else {
                using (var loadedImage = new HImage(imagePath)) {
                    loadedImage.GetImageSize(out widthValue, out heightValue);
                }
            }

            if (index == 0) {
                pMyContext.ScreenCenter_X = widthValue.D / 2.0;
                pMyContext.ScreenCenter_Y = heightValue.D / 2.0;
                pMyContext.CalSelector = (int)pMyParam.CalBase;
                pMyContext.ROI_Rect = new System.Windows.Rect(
                    pMyParam.ROI.Left,
                    pMyParam.ROI.Top,
                    Math.Max(1.0, pMyParam.ROI.Width),
                    Math.Max(1.0, pMyParam.ROI.Height));
            }

            RoiLineInspectionResult result;
            bool isSuccess = image != null
                ? _algorithm.TryRun(image, pMyParam.TeachingJob?.Rois, out result)
                : _algorithm.TryRun(imagePath, pMyParam.TeachingJob?.Rois, out result);

            if (!isSuccess || !result.HasIntersection) {
                pMyContext.InspectResultArray[index] = pMyParam.TeachingJob?.Rois?.Any() == true
                    ? EVisionResultType.NG : EVisionResultType.TECHING;
                if (index == 0) pMyContext.InspectResult = pMyContext.InspectResultArray[index];
                return false;
            }

            var offsetX = ((result.IntersectionColumn - (widthValue.D / 2.0)) * pMyParam.PixelToUM_Offset / 1000.0) + pMyParam.TeachingJob.OutputOffsetX;
            var offsetY = ((result.IntersectionRow - (heightValue.D / 2.0)) * pMyParam.PixelToUM_Offset / 1000.0) + pMyParam.TeachingJob.OutputOffsetY;
            var angle = result.CrossAngleDeg + pMyParam.TeachingJob.OutputOffsetTheta;

            pMyContext.CenterOffsetXmmArray[index] = offsetX;
            pMyContext.CenterOffsetYmmArray[index] = offsetY;
            pMyContext.dAngleArray[index] = angle;
            pMyContext.DieCenter_XArray[index] = result.IntersectionColumn;
            pMyContext.DieCenter_YArray[index] = result.IntersectionRow;
            pMyContext.X_Picker_CalValue[index] = result.IntersectionColumn;
            pMyContext.Y_Picker_CalValue[index] = result.IntersectionRow;
            pMyContext.InspectResultArray[index] = EVisionResultType.OK;

            // 첫 번째 이미지 결과를 Context 대표값으로 저장
            if (index == 0) {
                pMyContext.CenterOffsetXmm = offsetX;
                pMyContext.CenterOffsetYmm = offsetY;
                pMyContext.dAngle = angle;
                pMyContext.DieCenter_X = (int)result.IntersectionColumn;
                pMyContext.DieCenter_Y = (int)result.IntersectionRow;
                pMyContext.Die_Area = pMyParam.ROI.Width * pMyParam.ROI.Height;
                pMyContext.InspectResult = EVisionResultType.OK;
                pMyContext.InspectionOverlays = result.Overlays.Select(o => o.Clone()).ToList();
                pMyContext.BottomDie[0] = new BottomDieInfo {
                    DieCenter_X = result.IntersectionColumn,
                    DieCenter_Y = result.IntersectionRow,
                    DieAngle = angle,
                    ContourCount = result.Overlays.Count,
                    ContourArea = pMyContext.Die_Area,
                    ApexCount = result.Overlays.Sum(item => item.Points.Count),
                    Judgment = true,
                    newJudgment = 1,
                    CenterOffsetXmm = offsetX,
                    CenterOffsetYmm = offsetY,
                    image = null
                };
            }
            return true;
        }

        private void QueueRawImageSave(HImage image) {
            if (image == null || SystemHandler.Handle.RawImageSaver == null) {
                return;
            }

            TestPacket requestPacket = pMyParam.Parent?.RequestPacket;
            SystemHandler.Handle.RawImageSaver.Enqueue(new RawImageSaveRequest {
                Image = image.CopyImage(),
                SequenceName = pMyParam.SequenceName,
                ActionName = pMyParam.ActionName,
                TestId = requestPacket?.TestID,
                TargetCode = pMyParam.Parent?.TargetID
            });
        }
    }
}









