using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
using ReringProject.Halcon.Services;
namespace ReringProject.UI
{
    public partial class TeachingWindow : Window
    {
        private readonly TeachingStorageService _storageService = new TeachingStorageService();
        private readonly MeasurementAlgorithm _algorithm = new MeasurementAlgorithm();
        private readonly RoiLineIntersectionAlgorithm _lineAlgorithm = new RoiLineIntersectionAlgorithm();
        private readonly List<RoiDefinition> _rois = new List<RoiDefinition>();
        private bool _suppressParameterChange;
        private double _outputOffsetX;
        private double _outputOffsetY;
        private double _outputOffsetTheta;
        public TeachingWindow()
        {
            InitializeComponent();
            TeachingViewer.PointerChanged += TeachingViewer_PointerChanged;
            TeachingViewer.RoiClicked += TeachingViewer_RoiClicked;
            TeachingViewer.DraftRoiChanged += TeachingViewer_DraftRoiChanged;
            Closed += TeachingWindow_Closed;
            TeachingViewer.AllowWheelZoomWhileDrawing = true;
            EdgeDirectionComboBox.ItemsSource = new[] { "LtoR", "TtoB", "RtoL", "BtoT" };
            EdgePolarityComboBox.ItemsSource = new[] { "LightToDark", "DarkToLight" };
            EdgeSelectionComboBox.ItemsSource = new[] { "First", "Last", "All" };
            LineOrientationComboBox.ItemsSource = new[] { "Horizontal", "Vertical" };
        }
        public event EventHandler<TeachingJob> TeachingApplied;
        public Func<string> ImageGrabber { get; set; }
        public string CurrentImagePath { get; private set; }
        public void LoadImage(string imagePath)
        {
            CurrentImagePath = imagePath;
            TeachingViewer.LoadImage(imagePath);
            TeachingViewer.SetRois(_rois);
        }
        public void SetTeaching(TeachingJob teaching)
        {
            _rois.Clear();
            if (teaching != null)
            {
                _rois.AddRange(teaching.Rois.Select(roi => roi.Clone()));
                _outputOffsetX = teaching.OutputOffsetX;
                _outputOffsetY = teaching.OutputOffsetY;
                _outputOffsetTheta = teaching.OutputOffsetTheta;
            }
            RoiCountTextBox.Text = Math.Max(_rois.Count, 1).ToString();
            RoiListBox.ItemsSource = null;
            RoiListBox.ItemsSource = _rois;
            TeachingViewer.SetRois(_rois);
        }
        private void ApplyCountButton_Click(object sender, RoutedEventArgs e)
        {
            int count;
            if (!int.TryParse(RoiCountTextBox.Text, out count) || count <= 0)
            {
                return;
            }
            while (_rois.Count < count)
            {
                _rois.Add(new RoiDefinition { Id = Guid.NewGuid().ToString("N"), Name = "ROI " + (_rois.Count + 1) });
            }
            while (_rois.Count > count)
            {
                _rois.RemoveAt(_rois.Count - 1);
            }
            RoiListBox.ItemsSource = null;
            RoiListBox.ItemsSource = _rois;
            TeachingViewer.SetRois(_rois);
        }
        private void DrawRoiButton_Click(object sender, RoutedEventArgs e)
        {
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            if (roi != null)
            {
                TeachingViewer.StartRectangleDrawing(GetSeedRoiForDrawing(roi));
            }
        }
        private void CommitRoiButton_Click(object sender, RoutedEventArgs e)
        {
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            if (roi == null)
            {
                return;
            }
            var committed = TeachingViewer.CommitActiveRectangle(roi.Id, roi.Name, TeachingValueTextBox.Text);
            if (committed == null)
            {
                return;
            }
            committed.Sigma = roi.Sigma;
            committed.EdgeThreshold = roi.EdgeThreshold;
            committed.EdgeSampleCount = roi.EdgeSampleCount;
            committed.EdgeTrimCount = roi.EdgeTrimCount;
            committed.EdgeDirection = roi.EdgeDirection;
            committed.EdgePolarity = roi.EdgePolarity;
            committed.EdgeSelection = roi.EdgeSelection;
            committed.LineOrientation = roi.LineOrientation;
            committed.PixelResolutionX = roi.PixelResolutionX;
            committed.PixelResolutionY = roi.PixelResolutionY;
            _rois[_rois.FindIndex(item => item.Id == roi.Id)] = committed;
            RoiListBox.ItemsSource = null;
            RoiListBox.ItemsSource = _rois;
            TeachingViewer.SetRois(_rois);
            RoiListBox.SelectedItem = committed;
        }
        private void RunEdgeInspectButton_Click(object sender, RoutedEventArgs e)
        {
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            if (roi == null)
            {
                return;
            }
            EdgeInspectionOverlay overlay;
            if (_algorithm.TryInspectSingleEdge(CurrentImagePath, roi, out overlay))
            {
                TeachingViewer.SetInspectionOverlays(new[] { overlay });
            }
        }
        private void RunLineIntersectButton_Click(object sender, RoutedEventArgs e)
        {
            RoiLineInspectionResult result;
            if (_lineAlgorithm.TryRun(CurrentImagePath, _rois, out result))
            {
                TeachingViewer.SetInspectionOverlays(result.Overlays);
            }
        }
        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            LoadImage(dialog.FileName);
            TeachingStatusTextBlock.Text = string.Format("Loaded image: {0}", dialog.FileName);
        }
        private void GrabImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageGrabber == null)
            {
                TeachingStatusTextBlock.Text = "Grab is not available for this teaching target.";
                return;
            }
            var imagePath = ImageGrabber();
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                TeachingStatusTextBlock.Text = "Grab failed.";
                return;
            }
            LoadImage(imagePath);
            TeachingStatusTextBlock.Text = string.Format("Grabbed image: {0}", imagePath);
        }
        private void LoadTeachingButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Teaching Json (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            SetTeaching(_storageService.Load<TeachingJob>(dialog.FileName));
            RaiseTeachingApplied();
        }
        private void SaveTeachingButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "Teaching Json (*.json)|*.json", FileName = "teaching.json" };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            _storageService.Save(dialog.FileName, BuildTeachingJob());
        }
        private void ApplyToMainButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseTeachingApplied();
        }
        private void RoiListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            TeachingViewer.SetSelectedRoi(roi == null ? null : roi.Id);
            UpdateEdgeParameterInputs(roi);
            RoiCoordinateTextBlock.Text = roi == null ? "No ROI coordinates." : string.Format("R1:{0:0.0}, C1:{1:0.0}, R2:{2:0.0}, C2:{3:0.0}", roi.Row1, roi.Column1, roi.Row2, roi.Column2);
        }
        private void SigmaTextBox_LostFocus(object sender, RoutedEventArgs e) { UpdateSelectedDouble((roi, value) => roi.Sigma = value, SigmaTextBox, 1.0); }
        private void EdgeThresholdTextBox_LostFocus(object sender, RoutedEventArgs e) { UpdateSelectedInt((roi, value) => roi.EdgeThreshold = value, EdgeThresholdTextBox, 10); }
        private void EdgeSampleCountTextBox_LostFocus(object sender, RoutedEventArgs e) { UpdateSelectedInt((roi, value) => roi.EdgeSampleCount = value, EdgeSampleCountTextBox, 20); }
        private void EdgeTrimCountTextBox_LostFocus(object sender, RoutedEventArgs e) { UpdateSelectedInt((roi, value) => roi.EdgeTrimCount = value, EdgeTrimCountTextBox, 10); }
        private void EdgeDirectionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { UpdateSelectedString((roi, value) => roi.EdgeDirection = value, EdgeDirectionComboBox.SelectedItem as string); }
        private void EdgePolarityComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { UpdateSelectedString((roi, value) => roi.EdgePolarity = value, EdgePolarityComboBox.SelectedItem as string); }
        private void EdgeSelectionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { UpdateSelectedString((roi, value) => roi.EdgeSelection = value, EdgeSelectionComboBox.SelectedItem as string); }
        private void LineOrientationComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { UpdateSelectedString((roi, value) => roi.LineOrientation = value, LineOrientationComboBox.SelectedItem as string); }
        private void PixelResolutionXTextBox_LostFocus(object sender, RoutedEventArgs e) { UpdateSelectedDouble((roi, value) => roi.PixelResolutionX = value, PixelResolutionXTextBox, 1.0); }
        private void PixelResolutionYTextBox_LostFocus(object sender, RoutedEventArgs e) { UpdateSelectedDouble((roi, value) => roi.PixelResolutionY = value, PixelResolutionYTextBox, 1.0); }
        private void TeachingViewer_PointerChanged(object sender, ViewerPointerChangedEventArgs e)
        {
            TeachingStatusTextBlock.Text = string.Format("Row: {0:0.0}, Col: {1:0.0}, Gray: {2}", e.Row, e.Column, e.GrayValue.HasValue ? e.GrayValue.Value.ToString("0.0") : "-");
        }
        private void TeachingViewer_RoiClicked(object sender, RoiDefinition e)
        {
            RoiListBox.SelectedItem = _rois.FirstOrDefault(roi => roi.Id == e.Id);
        }
        private void TeachingViewer_DraftRoiChanged(object sender, RoiDefinition e)
        {
            if (e != null)
            {
                RoiCoordinateTextBlock.Text = string.Format("R1:{0:0.0}, C1:{1:0.0}, R2:{2:0.0}, C2:{3:0.0}", e.Row1, e.Column1, e.Row2, e.Column2);
            }
        }
        private void UpdateEdgeParameterInputs(RoiDefinition roi)
        {
            _suppressParameterChange = true;
            try
            {
                SigmaTextBox.Text = roi == null ? "1.0" : roi.Sigma.ToString("0.0###", CultureInfo.InvariantCulture);
                EdgeThresholdTextBox.Text = roi == null ? "10" : roi.EdgeThreshold.ToString(CultureInfo.InvariantCulture);
                EdgeSampleCountTextBox.Text = roi == null ? "20" : roi.EdgeSampleCount.ToString(CultureInfo.InvariantCulture);
                EdgeTrimCountTextBox.Text = roi == null ? "10" : roi.EdgeTrimCount.ToString(CultureInfo.InvariantCulture);
                EdgeDirectionComboBox.SelectedItem = roi == null ? "LtoR" : roi.EdgeDirection;
                EdgePolarityComboBox.SelectedItem = roi == null ? "DarkToLight" : roi.EdgePolarity;
                EdgeSelectionComboBox.SelectedItem = roi == null ? "First" : roi.EdgeSelection;
                LineOrientationComboBox.SelectedItem = roi == null ? "Horizontal" : roi.LineOrientation;
                PixelResolutionXTextBox.Text = roi == null ? "1.0" : roi.PixelResolutionX.ToString("0.0###", CultureInfo.InvariantCulture);
                PixelResolutionYTextBox.Text = roi == null ? "1.0" : roi.PixelResolutionY.ToString("0.0###", CultureInfo.InvariantCulture);
            }
            finally
            {
                _suppressParameterChange = false;
            }
        }
        private void UpdateSelectedInt(Action<RoiDefinition, int> setter, System.Windows.Controls.TextBox textBox, int defaultValue)
        {
            if (_suppressParameterChange) return;
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            if (roi == null) return;
            int value;
            if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) value = defaultValue;
            setter(roi, value);
            textBox.Text = value.ToString(CultureInfo.InvariantCulture);
        }
        private void UpdateSelectedDouble(Action<RoiDefinition, double> setter, System.Windows.Controls.TextBox textBox, double defaultValue)
        {
            if (_suppressParameterChange) return;
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            if (roi == null) return;
            double value;
            if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) value = defaultValue;
            setter(roi, value);
            textBox.Text = value.ToString("0.0###", CultureInfo.InvariantCulture);
        }
        private void UpdateSelectedString(Action<RoiDefinition, string> setter, string value)
        {
            if (_suppressParameterChange || string.IsNullOrWhiteSpace(value)) return;
            var roi = RoiListBox.SelectedItem as RoiDefinition;
            if (roi != null) setter(roi, value);
        }
        private RoiDefinition GetSeedRoiForDrawing(RoiDefinition target)
        {
            if (target == null)
            {
                return null;
            }
            if (target.IsTaught)
            {
                return target;
            }
            var targetIndex = _rois.FindIndex(roi => roi.Id == target.Id);
            if (targetIndex >= 0)
            {
                for (var i = targetIndex - 1; i >= 0; i--)
                {
                    if (_rois[i].IsTaught)
                    {
                        return _rois[i];
                    }
                }
            }
            return _rois.LastOrDefault(roi => roi.IsTaught);
        }
        private TeachingJob BuildTeachingJob()
        {
            return new TeachingJob
            {
                JobName = "DefaultJob",
                ImagePath = CurrentImagePath,
                Rois = _rois.Select(roi => roi.Clone()).ToList(),
                OutputOffsetX = _outputOffsetX,
                OutputOffsetY = _outputOffsetY,
                OutputOffsetTheta = _outputOffsetTheta
            };
        }
        private void RaiseTeachingApplied()
        {
            var handler = TeachingApplied;
            if (handler != null)
            {
                handler(this, BuildTeachingJob());
            }
        }

        private void TeachingWindow_Closed(object sender, EventArgs e)
        {
            Closed -= TeachingWindow_Closed;
            TeachingViewer.PointerChanged -= TeachingViewer_PointerChanged;
            TeachingViewer.RoiClicked -= TeachingViewer_RoiClicked;
            TeachingViewer.DraftRoiChanged -= TeachingViewer_DraftRoiChanged;
            TeachingViewer.Dispose();
        }
    }
}

