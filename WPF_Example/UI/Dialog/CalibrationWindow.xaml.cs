//260623 hbk Phase 53: 체커보드 픽셀 캘리브 창 코드비하인드 (입력/검출/리포트/결과 노출)
using System;
using System.Collections.Generic;   //260623 hbk: 코너 오버레이 리스트
using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;   //260623 hbk: EdgeInspectionOverlay/Point

namespace ReringProject.UI
{
    public partial class CalibrationWindow : Window
    {
        private readonly CheckerboardCalibrationService _calibService = new CheckerboardCalibrationService();
        private CalibrationResult _lastResult;                 // [적용]이 소비 (plan 03)
        private static string _lastCellMm = "1.0";             // D-01 직전값 (앱 수명 내 기억, INI 비의존)

        // caller(MainView)가 카메라 grab 델리게이트 주입 → grab 후 imagePath 반환 (D-04 라이브)
        public Func<string> ImageGrabber { get; set; }

        // 외부 노출 (plan 03 launch wiring 이 참조)
        public CalibrationResult LastResult { get { return _lastResult; } }

        // [적용] 위임 이벤트 — plan 03 이 MainView 에서 구독해 반영+저장 (D-06 게이트)
        public event Action<CalibrationResult> ApplyRequested;

        public CalibrationWindow()
        {
            InitializeComponent();
            txt_cellMm.Text = _lastCellMm;
            txt_sigma.Text = CheckerboardCalibrationService.DefaultSaddleSigma.ToString(CultureInfo.InvariantCulture);
            txt_threshold.Text = CheckerboardCalibrationService.DefaultSaddleThreshold.ToString(CultureInfo.InvariantCulture);
            txt_warnPct.Text = CheckerboardCalibrationService.DistortionWarnThresholdPct.ToString(CultureInfo.InvariantCulture);
#if SIMUL_MODE
            btn_liveCapture.IsEnabled = false;
            btn_liveCapture.ToolTip = "SIMUL 모드에서는 이미지 로드만 가능합니다.";
#endif
            //260623 hbk Phase 53 WR-01: 창 종료 시 뷰어 HImage 해제 (TeachingWindow 패턴) — 누수 방지
            Closed += CalibrationWindow_Closed;
        }

        //260623 hbk Phase 53 WR-01: HalconViewerControl 네이티브 핸들 해제
        private void CalibrationWindow_Closed(object sender, EventArgs e)
        {
            Closed -= CalibrationWindow_Closed;
            CalibrationViewer.Dispose();
        }

        // (a) 이미지 로드 — OpenFileDialog → 뷰어 표시 (검출 입력은 CalibrationViewer.CurrentImage)
        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            try
            {
                CalibrationViewer.LoadImage(dialog.FileName);
            }
            catch (Exception ex)
            {
                CalibrationStatusTextBlock.Text = "이미지 로드 실패: " + ex.Message;
                return;
            }
            btn_apply.IsEnabled = false;   // 새 이미지 로드 시 직전 검출 무효화
            CalibrationViewer.SetInspectionOverlays(null);   //260623 hbk: 새 이미지 → 직전 코너 마커 제거
            CalibrationStatusTextBlock.Text = "로드: " + dialog.FileName;
        }

        // (b) 라이브 촬상 — ImageGrabber 주입 (SIMUL 은 버튼 비활성이라 진입 안 함, Pattern 4)
        private void GrabImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageGrabber == null)
            {
                CalibrationStatusTextBlock.Text = "라이브 촬상 불가 (grabber 미주입).";
                return;
            }
            string path = ImageGrabber();
            if (string.IsNullOrWhiteSpace(path))
            {
                CalibrationStatusTextBlock.Text = "라이브 촬상 실패.";
                return;
            }
            try
            {
                CalibrationViewer.LoadImage(path);
            }
            catch (Exception ex)
            {
                CalibrationStatusTextBlock.Text = "촬상 이미지 표시 실패: " + ex.Message;
                return;
            }
            btn_apply.IsEnabled = false;   // 새 프레임 → 직전 검출 무효화
            CalibrationViewer.SetInspectionOverlays(null);   //260623 hbk: 새 프레임 → 직전 코너 마커 제거
            CalibrationStatusTextBlock.Text = "라이브 촬상 완료.";
        }

        // (c) 검출 — 입력검증(V5) → 서비스 호출 → 리포트 + 왜곡 경고(D-05) + 적용 게이트(D-06)
        private void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            if (CalibrationViewer.CurrentImage == null)
            {
                CalibrationStatusTextBlock.Text = "이미지를 먼저 로드하세요.";
                return;
            }
            double mm, sigma, thr, warnPct;
            if (!double.TryParse(txt_cellMm.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out mm) || mm <= 0)
            {
                CustomMessageBox.Show("캘리브레이션", "칸 크기(mm)에 0보다 큰 숫자를 입력하세요.");
                return;
            }
            if (!double.TryParse(txt_sigma.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out sigma))
            {
                sigma = CheckerboardCalibrationService.DefaultSaddleSigma;
            }
            if (!double.TryParse(txt_threshold.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out thr))
            {
                thr = CheckerboardCalibrationService.DefaultSaddleThreshold;
            }
            if (!double.TryParse(txt_warnPct.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out warnPct))
            {
                warnPct = CheckerboardCalibrationService.DistortionWarnThresholdPct;
            }

            CalibrationResult result;
            string err;
            if (!_calibService.TryCalibrate(CalibrationViewer.CurrentImage, mm, sigma, thr, warnPct, out result, out err))
            {
                CalibrationStatusTextBlock.Text = "검출 실패: " + err;
                CalibrationViewer.SetInspectionOverlays(null);   //260623 hbk: 검출 실패 → 직전 코너 마커 제거
                btn_apply.IsEnabled = false;
                return;
            }

            _lastResult = result;
            _lastCellMm = txt_cellMm.Text;   // D-01 직전값 갱신
            //260623 hbk: 중앙/외곽 평균(px) + X/Y 축별 편차% + 종합 편차% 보강 리포트
            txt_report.Text = string.Format(CultureInfo.InvariantCulture,
                "1 px = {0:F5} mm (X {1:F5} / Y {2:F5})\n평균 간격 {3:F2} px · 코너 {4}개\n중앙부 {5:F2} px ↔ 외곽부 {6:F2} px\n편차 종합 {7:F2}% (X {8:F2}% / Y {9:F2}%)",
                result.MmPerPixel, result.MmPerPixelX, result.MmPerPixelY,
                result.MeanSpacingPx, result.CornerCount,
                result.CenterMeanPx, result.OuterMeanPx,
                result.CenterOuterDeviationPct, result.DeviationXPct, result.DeviationYPct);

            if (result.IsDistortionWarn)
            {
                lbl_distortionWarn.Text = string.Format(CultureInfo.InvariantCulture,
                    "[경고] 외곽 왜곡 {0:F2}% — undistort 검토 필요", result.CenterOuterDeviationPct);
                lbl_distortionWarn.Visibility = Visibility.Visible;
            }
            else
            {
                lbl_distortionWarn.Visibility = Visibility.Collapsed;
            }

            ShowCornerOverlay(result);    //260623 hbk: 검출 코너 cyan + 마커 표시
            btn_apply.IsEnabled = true;   // D-06: 검출 성공 후에만 적용 가능
            CalibrationStatusTextBlock.Text = "검출 완료.";
        }

        //260623 hbk: 검출 코너를 기존 오버레이 파이프라인(SetInspectionOverlays)으로 push. 새 HWindow 경로 발명 금지.
        private void ShowCornerOverlay(CalibrationResult result)
        {
            if (result == null || result.CornerRows == null || result.CornerCols == null
                || result.CornerRows.Length != result.CornerCols.Length || result.CornerRows.Length == 0)
            {
                CalibrationViewer.SetInspectionOverlays(null);
                return;
            }
            EdgeInspectionOverlay overlay = new EdgeInspectionOverlay();
            overlay.RoiId = "Calib-Corners";
            for (int i = 0; i < result.CornerRows.Length; i++)
            {
                EdgeInspectionPoint pt = new EdgeInspectionPoint();
                pt.Row = result.CornerRows[i];
                pt.Column = result.CornerCols[i];
                overlay.Points.Add(pt);
            }
            CalibrationViewer.SetInspectionOverlays(new List<EdgeInspectionOverlay> { overlay });
        }

        // (d) 적용 — 본 plan 은 위임만. 반영/저장 wiring 은 plan 03 (ApplyRequested 구독)
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            //260623 hbk Phase 53: 반영/저장 wiring 은 plan 03 (ApplyRequested 이벤트로 위임)
            if (_lastResult != null && ApplyRequested != null)
            {
                ApplyRequested(_lastResult);
            }
        }
    }
}
