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
        private HImage _latestHalconImage;
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
            if (owner is ActionBase ownerAction) { //260612 hbk Wave5
                _jobName = ownerAction.Name;
            } else {
                _jobName = "TopInspection";
            }
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
                    TeachingJob loaded = HalconTeachingHelper.LoadJob(TeachingFilePath); //260612 hbk Wave5
                    if (loaded == null) { //260612 hbk Wave5
                        _teachingJob = HalconTeachingHelper.CreateDefaultJob(_jobName, ROI);
                    } else {
                        _teachingJob = loaded;
                    }
                }
                return _teachingJob;
            }
        }
        public IEnumerable<RoiDefinition> GetViewerRois() {
            if (TeachingJob == null || TeachingJob.Rois == null) return Enumerable.Empty<RoiDefinition>(); //260612 hbk Wave5
            return TeachingJob.Rois;
        }

        public override bool Load(IniFile loadFile, string group) {
            var result = base.Load(loadFile, group);
            TeachingJob loadedJob = HalconTeachingHelper.LoadJob(TeachingFilePath); //260612 hbk Wave5
            if (loadedJob == null) { //260612 hbk Wave5
                _teachingJob = HalconTeachingHelper.CreateDefaultJob(_jobName, ROI);
            } else {
                _teachingJob = loadedJob;
            }
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
            if (_latestHalconImage != null) { //260612 hbk Wave5
                _latestHalconImage.Dispose();
            }
            if (image == null) { //260612 hbk Wave5
                _latestHalconImage = null;
            } else {
                _latestHalconImage = image.CopyImage();
            }
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
            if (_latestHalconImage != null) { //260612 hbk Wave5
                _latestHalconImage.Dispose();
            }
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
            System.Windows.Window mainWindow = null; //260612 hbk Wave5
            if (Application.Current != null) { //260612 hbk Wave5
                mainWindow = Application.Current.MainWindow;
            }
            var window = new TeachingWindow { Owner = mainWindow };
            window.ImageGrabber = GrabTeachingImage;
            if (!string.IsNullOrWhiteSpace(_latestImagePath) && File.Exists(_latestImagePath)) {
                window.LoadImage(_latestImagePath);
            }
            else if (_teachingJob != null && !string.IsNullOrWhiteSpace(_teachingJob.ImagePath) && File.Exists(_teachingJob.ImagePath)) {
                window.LoadImage(_teachingJob.ImagePath);
            }
            TeachingJob clonedOrDefault = HalconTeachingHelper.CloneJob(TeachingJob); //260612 hbk Wave5
            if (clonedOrDefault == null) { //260612 hbk Wave5
                clonedOrDefault = HalconTeachingHelper.CreateDefaultJob(_jobName, ROI);
            }
            window.SetTeaching(clonedOrDefault);
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
            IEnumerable<RoiDefinition> roisForBounds = null; //260612 hbk Wave5
            if (_teachingJob != null) { //260612 hbk Wave5
                roisForBounds = _teachingJob.Rois;
            }
            var bounds = HalconTeachingHelper.BuildBounds(roisForBounds);
            if (!bounds.IsEmpty) {
                ROI = bounds;
                ModuleModel.Master = bounds;
            }
        }
    }

    //260526 hbk Phase 33 — Action_FAIMeasurement 로 마이그레이션됨 (D-05)
    [System.Obsolete("Phase 33 — Action_FAIMeasurement 로 마이그레이션됨", false)]
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
            // 260317 queue raw input image save outside the inspection thread
            // image는 TryRun / GetImageSize 까지 사용되므로 try/finally로 감싸
            // 어느 경로로 return 되더라도 원본 HImage가 반드시 해제되도록 보장한다.
            try {
                QueueRawImageSave(image);                                               // 내부 CopyImage → 저장 완료 후 Dispose
                pMyParam.PutImage(image);                                               // 내부 CopyImage → 다음 PutImage 시 Dispose
                if (pMyContext.ResultHalconImage != null) { //260612 hbk Wave5
                    pMyContext.ResultHalconImage.Dispose();
                }
                if (image == null) { //260612 hbk Wave5
                    pMyContext.ResultHalconImage = null;
                } else {
                    pMyContext.ResultHalconImage = image.CopyImage(); // 내부 CopyImage → 다음 Run 시 Dispose
                }

                string imagePath = null;
                if (image == null) {
                    imagePath = pMyParam.GetLatestImagePath();
                    if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) {
                        pMyContext.InspectResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Error);
                        return Context;
                    }
                }

                RoiLineInspectionResult result;
                IEnumerable<RoiDefinition> teachingRois = null; //260612 hbk Wave5
                if (pMyParam.TeachingJob != null) { //260612 hbk Wave5
                    teachingRois = pMyParam.TeachingJob.Rois;
                }
                bool isSuccess; //260612 hbk Wave5
                if (image != null) { //260612 hbk Wave5
                    isSuccess = _algorithm.TryRun(image, teachingRois, out result);
                } else {
                    isSuccess = _algorithm.TryRun(imagePath, teachingRois, out result);
                }
                if (!isSuccess || !result.HasIntersection) {
                    bool hasRois = pMyParam.TeachingJob != null && pMyParam.TeachingJob.Rois != null && pMyParam.TeachingJob.Rois.Any(); //260612 hbk Wave5
                    if (hasRois) { //260612 hbk Wave5
                        pMyContext.InspectResult = EVisionResultType.NG;
                    } else {
                        pMyContext.InspectResult = EVisionResultType.TECHING;
                    }
                    FinishAction(EContextResult.Fail);
                    return Context;
                }

                HTuple widthValue;
                HTuple heightValue;
                if (image != null) {
                    image.GetImageSize(out widthValue, out heightValue);
                }
                else {
                    using (var loadedImage = new HImage(imagePath)) {
                        loadedImage.GetImageSize(out widthValue, out heightValue);
                    }
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
            finally {
                if (image != null) { //260612 hbk Wave5 — 원본 해제 - 모든 return/예외 경로에서 보장
                    image.Dispose();
                }
            }
        }

        private void QueueRawImageSave(HImage image) {
            if (image == null || SystemHandler.Handle.RawImageSaver == null) {
                return;
            }

            TestPacket requestPacket = null; //260612 hbk Wave5
            if (pMyParam.Parent != null) { //260612 hbk Wave5
                requestPacket = pMyParam.Parent.RequestPacket;
            }
            string testId = null; //260612 hbk Wave5
            if (requestPacket != null) { //260612 hbk Wave5
                testId = requestPacket.TestID;
            }
            string targetCode = null; //260612 hbk Wave5
            if (pMyParam.Parent != null) { //260612 hbk Wave5
                targetCode = pMyParam.Parent.TargetID;
            }
            SystemHandler.Handle.RawImageSaver.Enqueue(new RawImageSaveRequest {
                Image = image.CopyImage(),
                SequenceName = pMyParam.SequenceName,
                ActionName = pMyParam.ActionName,
                TestId = testId,
                TargetCode = targetCode
            });
        }
    }
}




