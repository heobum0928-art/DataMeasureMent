using System.Collections.Generic;
using System;
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

    public class TopInspectionContext : ActionContext {
        public EVisionResultType InspectResult { get; set; }
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public double AngleDeg { get; set; }

        public TopInspectionContext(ActionBase source) : base(source) { }

        public override void Clear() {
            InspectResult = EVisionResultType.NG;
            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;
            AngleDeg = 0;
            base.Clear();
        }
    }

    public class TopInspectionParam : CameraSlaveParam, IHalconTeachingProvider, IOfflineImageParam {
        private readonly string _jobName;
        private string _latestImagePath;
        private TeachingJob _teachingJob;

        [Category("Top Inspection")]
        [Rectangle, Converter(typeof(UI.RectConverter))]
        public System.Windows.Rect ROI { get; set; }

        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        public ModelFinderViewModel ModuleModel { get; set; } = new ModelFinderViewModel("Teaching", EAlgorithmType.ModelFinder);

        [DisplayName("Score")]
        public double Score { get; set; }

        [DisplayName("Angle")]
        public double Angle { get; set; }

        public TopInspectionParam(object owner, int algIndex, int modelIndex) : base(owner) {
            _jobName = owner is ActionBase action ? action.Name : "TopInspection";
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

    public class TopInspectionAction : ActionBase {
        private readonly RoiLineIntersectionAlgorithm _algorithm = new RoiLineIntersectionAlgorithm();
        private TopInspectionContext pMyContext;
        private TopInspectionParam pMyParam;
        private VirtualCamera pCamera;

        public TopInspectionAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            Context = new TopInspectionContext(this);
            pMyContext = Context as TopInspectionContext;
            Param = new TopInspectionParam(this, algIndex, modelIndex);
            pMyParam = Param as TopInspectionParam;
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
                pMyContext.InspectResult = EVisionResultType.NG;
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
            var imagePath = pMyParam.GetLatestImagePath();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) {
                pMyContext.InspectResult = EVisionResultType.NG;
                FinishAction(EContextResult.Error);
                return Context;
            }

            RoiLineInspectionResult result;
            if (!_algorithm.TryRun(imagePath, pMyParam.TeachingJob?.Rois, out result) || !result.HasIntersection) {
                pMyContext.InspectResult = pMyParam.TeachingJob?.Rois?.Any() == true ? EVisionResultType.NG : EVisionResultType.TECHING;
                FinishAction(EContextResult.Fail);
                return Context;
            }

            HTuple widthValue;
            HTuple heightValue;
            using (var loadedImage = new HImage(imagePath)) {
                loadedImage.GetImageSize(out widthValue, out heightValue);
            }

            pMyContext.CenterOffsetXmm = ((result.IntersectionColumn - (widthValue.D / 2.0)) * pMyParam.PixelToUM_Offset / 1000.0) + pMyParam.TeachingJob.OutputOffsetX;
            pMyContext.CenterOffsetYmm = ((result.IntersectionRow - (heightValue.D / 2.0)) * pMyParam.PixelToUM_Offset / 1000.0) + pMyParam.TeachingJob.OutputOffsetY;
            pMyContext.AngleDeg = result.CrossAngleDeg + pMyParam.TeachingJob.OutputOffsetTheta;
            pMyContext.InspectResult = EVisionResultType.OK;
            pMyContext.ResultImagePath = imagePath;
            pMyContext.InspectionOverlays = result.Overlays.Select(overlay => overlay.Clone()).ToList();
            FinishAction(EContextResult.Pass);
            return Context;
        }
    }
}




