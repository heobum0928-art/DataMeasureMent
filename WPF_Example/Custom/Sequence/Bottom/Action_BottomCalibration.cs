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

    public class BottomCalibrationContext : ActionContext {
        public bool[] bFoundCircle = new bool[10];
        public double[] CircleCenter_X = new double[10];
        public double[] CircleCenter_Y = new double[10];
        public double[] Radius = new double[10];
        public double[] dRadmm = new double[10];
        public double[] dDistX = new double[10];
        public double[] dDistY = new double[10];
        public double[] CenterOffsetXmm = new double[10];
        public double[] CenterOffsetYmm = new double[10];
        public int PickerNum { get; set; }
        public double[] X_Picker = new double[10];
        public double[] Y_Picker = new double[10];
        public EVisionResultType CalibrationResult { get; set; }
        public System.Windows.Rect ROI_Rect { get; set; }
        public Mat mask = new Mat(2048, 2448, MatType.CV_8UC1);
        public EVisionResultType[] CalibrationResultArray { get; set; } = new EVisionResultType[10];
        public string strID { get; set; }
        public string strParamBaseName { get; set; }
        public string strActionBaseName { get; set; }
        public int GrabCount { get; set; }

        public BottomCalibrationContext(ActionBase source) : base(source) { }

        public override void Clear() {
            CalibrationResult = EVisionResultType.NG;
            PickerNum = 0;
            GrabCount = 1;
            for (var i = 0; i < 10; i++) {
                bFoundCircle[i] = false;
                CircleCenter_X[i] = 0;
                CircleCenter_Y[i] = 0;
                Radius[i] = 0;
                dRadmm[i] = 0;
                dDistX[i] = 0;
                dDistY[i] = 0;
                CenterOffsetXmm[i] = 0;
                CenterOffsetYmm[i] = 0;
                X_Picker[i] = 0;
                Y_Picker[i] = 0;
                CalibrationResultArray[i] = EVisionResultType.NotExist;
            }
            base.Clear();
        }
    }

    public class BottomCalibrationParam : CameraSlaveParam, IHalconTeachingProvider, IOfflineImageParam {
        private readonly string _jobName;
        private string _latestImagePath;
        private TeachingJob _teachingJob;

        [Category("Bottom Calibration")]
        [Rectangle, Converter(typeof(UI.RectConverter))]
        public System.Windows.Rect ROI { get; set; }

        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        public ModelFinderViewModel ModuleModel { get; set; } = new ModelFinderViewModel("Teaching", EAlgorithmType.ModelFinder);

        [DisplayName("Process Name")]
        [ReadOnly(true)]
        public string ProcessName { get; set; }

        [DisplayName("Circle Radius")]
        public double CircleRadius { get; set; }

        [DisplayName("Circle X")]
        [ReadOnly(true)]
        public double Circle_X { get; set; }

        [DisplayName("Circle Y")]
        [ReadOnly(true)]
        public double Circle_Y { get; set; }

        [DisplayName("Radius")]
        [ReadOnly(true)]
        public double Radius { get; set; }

        [DisplayName("BaseX")]
        [ReadOnly(true)]
        public double BaseX { get; set; }

        [DisplayName("BaseY")]
        [ReadOnly(true)]
        public double BaseY { get; set; }

        [DisplayName("Picker Count")]
        public int Grab_Count { get; set; } = 1;

        public BottomCalibrationParam(object owner, int algIndex, int modelIndex) : base(owner) {
            _jobName = owner is ActionBase action ? action.Name : "BottomCalibration";
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
            var savedImagePath = HalconTeachingHelper.SaveTempImage(SequenceName + "_" + ActionName, image);
            if (string.IsNullOrWhiteSpace(savedImagePath)) {
                return;
            }

            _latestImagePath = savedImagePath;
            if (_teachingJob != null) {
                _teachingJob.ImagePath = _latestImagePath;
            }
        }

        public string GetLatestImagePath() {
            return _latestImagePath;
        }

        public void SetLatestImagePath(string imagePath) {
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

    public class BottomCalibrationAction : ActionBase {
        private readonly RoiLineIntersectionAlgorithm _algorithm = new RoiLineIntersectionAlgorithm();
        private BottomCalibrationContext pMyContext;
        private BottomCalibrationParam pMyParam;
        private VirtualCamera pCamera;

        public BottomCalibrationAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            Context = new BottomCalibrationContext(this);
            pMyContext = Context as BottomCalibrationContext;
            Param = new BottomCalibrationParam(this, algIndex, modelIndex);
            pMyParam = Param as BottomCalibrationParam;
        }

        public override void OnLoad() {
            pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
            if (pCamera != null && pCamera.Properties != null) {
                pCamera.Properties.ApplyFromParam(pMyParam);
                pCamera.SetSoftwareTriggerMode();
            }
            base.OnLoad();
        }

        public override ActionContext Run() {
            if (pCamera == null && (string.IsNullOrWhiteSpace(pMyParam.GetLatestImagePath()) || !File.Exists(pMyParam.GetLatestImagePath()))) {
                pMyContext.CalibrationResult = EVisionResultType.NG;
                FinishAction(EContextResult.Error);
                return Context;
            }

            if (pCamera != null) {
                SystemHandler.Handle.Lights.ApplyLight(pMyParam);
            }
            var image = SystemHandler.Handle.Devices.GrabHalconImage(pMyParam);
            pMyParam.PutImage(image);
            pMyContext.ResultHalconImage?.Dispose();
            pMyContext.ResultHalconImage = image == null ? null : image.CopyImage();
            pMyContext.strActionBaseName = Name;
            pMyContext.GrabCount = Math.Max(1, Math.Min(10, pMyParam.Grab_Count));

            var imagePath = pMyParam.GetLatestImagePath();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) {
                pMyContext.CalibrationResult = EVisionResultType.NG;
                pMyContext.CalibrationResultArray[0] = pMyContext.CalibrationResult;
                FinishAction(EContextResult.Error);
                return Context;
            }

            RoiLineInspectionResult result;
            if (!_algorithm.TryRun(imagePath, pMyParam.TeachingJob?.Rois, out result) || !result.HasIntersection) {
                pMyContext.CalibrationResult = pMyParam.TeachingJob?.Rois?.Any() == true ? EVisionResultType.NG : EVisionResultType.TECHING;
                pMyContext.CalibrationResultArray[0] = pMyContext.CalibrationResult;
                FinishAction(EContextResult.Fail);
                return Context;
            }

            HTuple widthValue;
            HTuple heightValue;
            using (var loadedImage = new HImage(imagePath)) {
                loadedImage.GetImageSize(out widthValue, out heightValue);
            }

            var centerXmm = ((result.IntersectionColumn - (widthValue.D / 2.0)) * pMyParam.PixelToUM_Offset / 1000.0) + pMyParam.TeachingJob.OutputOffsetX;
            var centerYmm = ((result.IntersectionRow - (heightValue.D / 2.0)) * pMyParam.PixelToUM_Offset / 1000.0) + pMyParam.TeachingJob.OutputOffsetY;
            var radmm = Math.Abs(centerXmm) + Math.Abs(centerYmm);

            for (var i = 0; i < pMyContext.GrabCount; i++) {
                pMyContext.bFoundCircle[i] = true;
                pMyContext.CircleCenter_X[i] = result.IntersectionColumn;
                pMyContext.CircleCenter_Y[i] = result.IntersectionRow;
                pMyContext.CenterOffsetXmm[i] = centerXmm;
                pMyContext.CenterOffsetYmm[i] = centerYmm;
                pMyContext.dDistX[i] = centerXmm;
                pMyContext.dDistY[i] = centerYmm;
                pMyContext.dRadmm[i] = radmm;
                pMyContext.CalibrationResultArray[i] = EVisionResultType.OK;
            }

            pMyContext.CalibrationResult = EVisionResultType.OK;
            pMyContext.ROI_Rect = new System.Windows.Rect(
                pMyParam.ROI.Left,
                pMyParam.ROI.Top,
                Math.Max(1.0, pMyParam.ROI.Width),
                Math.Max(1.0, pMyParam.ROI.Height));
            pMyParam.Circle_X = result.IntersectionColumn;
            pMyParam.Circle_Y = result.IntersectionRow;
            pMyParam.BaseX = centerXmm;
            pMyParam.BaseY = centerYmm;
            pMyParam.Radius = radmm;
            pMyContext.ResultImagePath = imagePath;
            pMyContext.InspectionOverlays = result.Overlays.Select(overlay => overlay.Clone()).ToList();
            FinishAction(EContextResult.Pass);
            return Context;
        }
    }
}









