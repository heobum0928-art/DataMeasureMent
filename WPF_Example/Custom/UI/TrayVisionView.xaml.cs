//260624 hbk Phase 61: TrayVisionView 코드비하인드 — Tray 비전 thin facade (AV-08)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;   //260807 hbk Live 폴링 타이머(DispatcherTimer)
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;   //260824 hbk 픽셀 캘리브레이션: CalibrationResult
using ReringProject.Halcon.Models;
using ReringProject.Halcon.Services;     //260824 hbk 픽셀 캘리브레이션: HalconTeachingHelper.SaveTempImage
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;             //260824 hbk 픽셀 캘리브레이션: Logging
using TeachDiag   = ReringProject.Halcon.Algorithms.TeachDiagnostics;   //quick-260812: 표시 전용 헬퍼(별칭 = 이름충돌 회피)
using ETeachGrade = ReringProject.Halcon.Algorithms.ETeachGrade;

namespace ReringProject.Custom.UI {

    /// <summary>
    /// Tray 비전 뷰 코드비하인드. Phase 58/59 서비스(EthernetVisionHandler.Camera/Matcher)에 위임하는
    /// thin facade. HALCON 뷰어를 소유하지 않고 외부 주입 공유 MainResultViewerControl 을 사용 (D-03).
    /// 전 서비스 호출 try-catch — 예외 시 상태 라벨 갱신만, throw 금지 (D-05).
    /// </summary>
    public partial class TrayVisionView : UserControl {

        // Tray 전용 모드 상수 (이 뷰는 항상 Tray 모드로 서비스 호출)
        private const EEthernetVisionMode VIEW_MODE = EEthernetVisionMode.Tray;

        // 최소 ROI 크기 임계 (px) — 너무 작은 ROI 는 티칭 불가
        private const double MIN_ROI_HALF_LENGTH = 1.0;

        //260625 hbk Phase 61.1 오프라인 이미지 로더 상태
        private const string LOADER_IMAGE_EXTS = ".bmp;.png;.jpg;.jpeg;.tif;.tiff";  // 지원 확장자
        private List<string> _loadedImagePaths = new List<string>();
        private int _loadedImageIndex = -1;   // -1 = 미로드
        private static string _lastImageFolder = null;   // 폴더 마지막 위치 기억 (static — 탭 전환에도 유지)

        // 이미지 저장 상태
        private const string SAVE_IMAGE_PREFIX = "TrayAlign";
        private const string SAVE_IMAGE_SUBFOLDER = "AlignCapture";
        private const string SAVE_IMAGE_TIMESTAMP_FORMAT = "yyyyMMdd_HHmmss";
        private const string SAVE_IMAGE_EXTENSION = ".bmp";
        private const string SAVE_IMAGE_FORMAT = "bmp";
        private static string _lastSaveFolder = null;   // 저장 마지막 폴더 기억 (static — 탭 전환에도 유지)

        // D-03: 외부 주입 공유 뷰어 (소유하지 않음 — MainWindow 가 관리)
        private MainResultViewerControl _viewer;

        //260807 hbk Live 모드 뷰어 주기 갱신 타이머 — 카메라 최대 4.5fps 라 200ms(5fps) 폴링으로 충분.
        //  재트리거(Grab) 없이 PeekLastImage()로 최근 스트리밍 프레임만 읽어와 뷰어에 반영.
        private DispatcherTimer _liveTimer;

        // quick-260902-fwj — Grab 버튼(수동 촬영) 전용 동축 자동 소등 타이머. 1회성 — Tick 에서
        //  즉시 자기 자신을 정지한다. 자동 검사 사이클/티칭 경로와는 무관하다.
        private DispatcherTimer _coaxAutoOffTimer;

        // 2-ROI 티칭 슬롯: DrawRoi1→DrawRoi2 순서로 슬롯 채움
        private RoiDefinition _roi1;
        private RoiDefinition _roi2;

        // 현재 ROI 드로잉 진행 중인 슬롯 인덱스 (1 또는 2, 0=미진행)
        private int _drawingSlot;

        //260626 hbk WR-02: 동축 UI 로드 중 이벤트 연쇄 저장 차단 플래그. true 이면 CoaxSlider_ValueChanged/CoaxCheckBox_Changed 즉시 return.
        private bool _isLoadingCoax = false;

        //260824 hbk 픽셀 캘리브레이션(거리 캘리브 — 2점 클릭 + 실측 mm 입력). 최소 픽셀 거리는
        //  MainView.MinCalibrationPixelDistance 와 동일 기준.
        private const double MIN_CALIB_PIXEL_DISTANCE = 1.0;
        private readonly List<System.Windows.Point> _calibrationPoints = new List<System.Windows.Point>();
        private bool _isCalibratingDistance = false;   // ROI 드로잉과 클릭 핸들러 충돌 방지 가드

        // quick-260903-dpy — 피커센터 캘 (Bottom AV-05 이식). 편심원 피팅 원형도 최소 점수(0~1).
        //  Bottom 의 FIT_SCORE_MIN 과 동일 값 — 등급 표시 전용, 판정 로직에는 쓰이지 않는다.
        private const double FIT_SCORE_MIN = 0.80;

        // 캘 검색 ROI(사각형 드로잉으로 수거). EthernetVisionHandler.Handle.PickerCal 이 Bottom/Tray
        //  공용 단일 인스턴스라(Task 4) 검색 ROI 좌표도 SystemSetting.CalibSearchRow1/Col1/Row2/Col2
        //  공용 값을 그대로 쓴다 — PC 당 EthernetVisionModeValue 가 하나라 동시 사용 충돌이 없다.
        private RoiDefinition _calRoiRect = null;
        private bool _calRoiSet = false;
        private bool _isCalRoiDrawing = false;   // 티칭 ROI 드로잉과 구분용 플래그

        public TrayVisionView() {
            InitializeComponent();
            Loaded += TrayVisionView_Loaded;
        }

        // ─── 공유 뷰어 계약 (Plan 61-03 이 소비) ────────────────────────────────

        /// <summary>
        /// 외부(MainWindow)가 공유 MainResultViewerControl 을 주입한다.
        /// ViewerHostBorder.Child 로 배치하여 airspace-safe 우측 컬럼에 표시.
        /// viewer 가 이전 부모에 부착되어 있을 경우 detach 는 MainWindow 책임.
        /// </summary>
        public void AttachSharedViewer(MainResultViewerControl viewer) {
            //260624 hbk Phase 61 — D-03 공유 뷰어 주입
            if (viewer == null) {
                return;
            }
            _viewer = viewer;
            ViewerHostBorder.Child = viewer;

            // Phase 74 브러시 마스킹 배선. Tray 는 슬롯이 없어 항상 None 경로를 쓴다.
            if (brushPanel != null) {
                brushPanel.ViewModel.ModelPathsProvider = () => EthernetVisionHandler.Handle.Matcher.GetModelPathsForMask(VIEW_MODE, EBottomAlignSlot.None);
                brushPanel.ViewModel.ModelRegenerator = RegenerateTeachSilent;
                brushPanel.ViewModel.Attach(viewer);
                brushPanel.ViewModel.ReloadMaskFromDisk();
            }
            // Phase 74: 드래그 종료 즉시 ROI 를 확정하기 위한 구독(중복 방지: -= 후 +=)
            _viewer.RectDrawingCompleted -= OnTeachRectDrawn;
            _viewer.RectDrawingCompleted += OnTeachRectDrawn;
            // quick-260903-dpy — 캘 ROI 사각형 드로잉 완료 구독(중복 방지: -= 후 +=).
            //  OnTeachRectDrawn 과 같은 이벤트를 같이 구독한다 — 각자 자기 게이트(_drawingSlot / _isCalRoiDrawing)
            //  로 자기 몫이 아니면 조용히 반환하므로 서로 간섭하지 않는다.
            _viewer.RectDrawingCompleted -= OnCalRectDrawn;
            _viewer.RectDrawingCompleted += OnCalRectDrawn;
            ShowTeachRoiOverlays(); // Phase 74: 뷰어 주입 시 기존 ROI 표시 복원
            _viewer.SetCenterCrossVisible(chk_showCenterCross.IsChecked == true); // Phase 74
            // Phase 74: 좌표/밝기 구독(중복 방지: -= 후 +=)
            _viewer.PointerInfoChanged -= OnViewerPointerInfoChanged;
            _viewer.PointerInfoChanged += OnViewerPointerInfoChanged;
            _viewer.SetPointerHudVisible(true);   // Phase 74: 좌표/밝기를 이미지 위에도 표시(WPF 라벨은 스크롤에 가린다)
            _viewer.SetInfoLabel("Tray Align"); // Phase 74: 어느 화면인지 이미지 위에 표시
            LoadCalStepAngleToUi(); // quick-260903-dpy — 저장된 캘 스텝 각도 반영(Bottom 과 공용 설정)
            UpdateCalButtonState(); // quick-260903-dpy — 뷰어 재주입 시에도 캘 버튼 활성 상태를 최신으로 갱신
        }

        // ─── 라이프사이클 ─────────────────────────────────────────────────────────

        private void TrayVisionView_Loaded(object sender, RoutedEventArgs e) {
            RefreshStatus();
            LoadTrayCoaxToUi(); //260626 hbk Phase 66 — Tray 동축값 복원(창 진입 시 Tray.json에서 CoaxEnabled/CoaxLevel 복원)
            LoadTeachParamsToUi(EthernetVisionHandler.Handle.Matcher.GetSlotRefPose(VIEW_MODE, EBottomAlignSlot.None)); // Phase 74
            UpdateCalButtonState(); // quick-260903-dpy — 피커센터 캘 버튼 초기 활성/비활성 + 진행 단계 라벨 반영
        }

        // ─── 카메라 핸들러 ────────────────────────────────────────────────────────

        private void GrabButton_Click(object sender, RoutedEventArgs e) {
#if SIMUL_MODE
            //260625 hbk Phase 61.1 F3 — SIMUL 모드: Grab = 파일 선택 다이얼로그 로드 (카메라 미사용)
            try {
                Ookii.Dialogs.Wpf.VistaOpenFileDialog dlg = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
                dlg.Filter = "이미지 파일|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|모든 파일|*.*";
                bool? bResult = dlg.ShowDialog();
                if (bResult == true) {
                    if (_viewer != null) {
                        _viewer.LoadImage(dlg.FileName);
                    }
                    lbl_status.Text = "로드: " + System.IO.Path.GetFileName(dlg.FileName);
                }
            }
            catch (Exception ex) {
                lbl_status.Text = "로드 오류: " + ex.Message;
            }
#else
            //260624 hbk Phase 61 — Camera null 가드
            if (EthernetVisionHandler.Handle.Camera == null) {
                lbl_status.Text = "미연결";
                return;
            }

            try {
                ApplyCoaxLight(); //260626 hbk Phase 66 — grab 직전 동축 자동 적용(D-07 Teach=Run=Grab 동일 조명)
                HImage img = EthernetVisionHandler.Handle.Camera.Grab();
                if (img == null) {
                    lbl_status.Text = "취득 실패 (폴백 없음)";
                    return;
                }

                if (_viewer != null) {
                    _viewer.LoadImage(img);   // LoadImage 가 내부 Clone — 즉시 Dispose 안전
                }
                img.Dispose();
                lbl_status.Text = "대기";
            }
            catch (Exception ex) {
                lbl_status.Text = "Grab 오류: " + ex.Message;
            }
            // 성공/취득실패/예외 세 경로 모두 위 ApplyCoaxLight() 가 이미 실행되어 조명이 켜져 있다.
            //  세 경로 전부에서 빠짐없이 소등을 예약한다.
            finally {
                StartCoaxAutoOffTimer();
            }
#endif
        }

        private void LiveButton_Click(object sender, RoutedEventArgs e) {
            if (EthernetVisionHandler.Handle.Camera == null) {
                lbl_status.Text = "미연결";
                return;
            }

            try {
                bool bOk = EthernetVisionHandler.Handle.Camera.Live();
                if (bOk) {
                    btn_live.Content = "Live On";   //260807 hbk 버튼 자체 글자 토글
                    //260807 hbk Live/Grab 상호 배타 — Live 중엔 Grab 금지, Live 버튼도 재클릭 방지(Stop 으로만 해제)
                    btn_grab.IsEnabled = false;
                    btn_live.IsEnabled = false;
                    // 직전 Grab 이 걸어둔 소등 예약이 Live 도중에 터져 조명을 꺼버리는 것을 막는다.
                    CancelCoaxAutoOffTimer();
                    // Live 화면도 Grab 과 같은 조명이어야 티칭/검사와 눈으로 비교가 된다(D-07 연장).
                    ApplyCoaxLight();
                    StartLiveTimer();
                }
                else {
                    btn_live.Content = "Live Off";
                    lbl_status.Text = "미연결";
                }
            }
            catch (Exception ex) {
                btn_live.Content = "Live Off";
                lbl_status.Text = "Live 오류: " + ex.Message;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e) {
            if (EthernetVisionHandler.Handle.Camera == null) {
                lbl_status.Text = "미연결";
                return;
            }

            try {
                StopLiveTimer();
                CancelCoaxAutoOffTimer();   // 잔여 소등 예약 정리 — 아래에서 이미 무조건 소등하므로 중복 방지.
                EthernetVisionHandler.Handle.Camera.Stop();
                // Live 를 껐으면 조명도 꺼야 한다 — UI 체크 상태와 무관하게 무조건 소등.
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
                btn_live.Content = "Live Off";   //260807 hbk 버튼 자체 글자 토글
                //260807 hbk Live 종료 — Grab/Live 버튼 재활성화
                btn_grab.IsEnabled = true;
                btn_live.IsEnabled = true;
                lbl_status.Text = "대기";
            }
            catch (Exception ex) {
                lbl_status.Text = "Stop 오류: " + ex.Message;
            }
        }

        /// <summary>260807 hbk Live 타이머 시작 — 이미 도는 중이면 재사용(중복 타이머 방지).</summary>
        private void StartLiveTimer() {
            if (_liveTimer != null) {
                return;
            }
            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _liveTimer.Tick += LiveTimer_Tick;
            _liveTimer.Start();
        }

        /// <summary>260807 hbk Live 타이머 정지 — Stop 클릭 또는 뷰 전환/언로드 시 반드시 호출.</summary>
        private void StopLiveTimer() {
            if (_liveTimer == null) {
                return;
            }
            _liveTimer.Stop();
            _liveTimer.Tick -= LiveTimer_Tick;
            _liveTimer = null;
        }

        /// <summary>260807 hbk 재트리거 없이 최근 스트리밍 프레임만 읽어와 뷰어에 반영.</summary>
        private void LiveTimer_Tick(object sender, EventArgs e) {
            if (EthernetVisionHandler.Handle.Camera == null || _viewer == null) {
                return;
            }
            HImage img = null;
            try {
                img = EthernetVisionHandler.Handle.Camera.PeekLastImage();
                if (img != null) {
                    _viewer.LoadImage(img);   // LoadImage 가 내부 Clone — 즉시 Dispose 안전
                }
            }
            catch {
                // Live 폴링 실패는 상태 라벨을 건드리지 않음 — 다음 틱에서 자연 복구 기대
            }
            finally {
                img?.Dispose();
            }
        }

        /// <summary>Grab 버튼 뒤 설정된 시간(ms)이 지나면 동축 조명을 끄도록 1회성 타이머를 예약한다.</summary>
        private void StartCoaxAutoOffTimer() {
            CancelCoaxAutoOffTimer();   // 연속 Grab 시 마지막 Grab 기준으로 소등 시각을 다시 계산한다.
            int nDelayMs = SystemSetting.Handle.AlignCoaxAutoOffMs;
            if (nDelayMs <= 0) {
                return;   // 0 이하 = 자동 소등 비활성(기존 동작 유지)
            }
            _coaxAutoOffTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(nDelayMs) };
            _coaxAutoOffTimer.Tick += CoaxAutoOffTimer_Tick;
            _coaxAutoOffTimer.Start();
        }

        /// <summary>대기 중인 동축 자동 소등 예약을 취소한다. 예약이 없으면 아무 일도 하지 않는다.</summary>
        private void CancelCoaxAutoOffTimer() {
            if (_coaxAutoOffTimer == null) {
                return;
            }
            _coaxAutoOffTimer.Stop();
            _coaxAutoOffTimer.Tick -= CoaxAutoOffTimer_Tick;
            _coaxAutoOffTimer = null;
        }

        /// <summary>예약된 시간이 지나 동축 조명을 끈다. 1회만 발화하고 스스로 정지한다.</summary>
        private void CoaxAutoOffTimer_Tick(object sender, EventArgs e) {
            CancelCoaxAutoOffTimer();   // 1회성 — 반복 소등 방지를 위해 자기 자신부터 먼저 정지한다.
            try {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
            }
            catch (Exception ex) {
                lbl_status.Text = "동축 소등 오류: " + ex.Message;
            }
        }

        // ─── 픽셀 캘리브레이션 핸들러 ────────────────────────────────────────────────
        //  SystemSetting.EthernetPixelResolution(μm/px) 단일 소스에 직접 쓴다 — 이 PC 는
        //  Tray/Bottom 모드가 배타적이라(EthernetVisionMode 1개) 값 충돌이 없다.
        //  기존 MainView 의 두 캘리브 방식(체커보드 지그 / 2점+실측mm)을 그대로 이식했다.

        // 체커보드 캘리브 — CalibrationWindow(기존, MainView 와 공유)를 열고 결과를 적용한다.
        private void OpenCheckerboardCalibrationButton_Click(object sender, RoutedEventArgs e) {
            var window = new CalibrationWindow { Owner = Window.GetWindow(this) };
            window.ImageGrabber = GrabEthernetCalibrationImage;
            window.ApplyRequested += ApplyEthernetCheckerboardCalibration;
            try {
                window.ShowDialog();
            }
            finally {
                window.ApplyRequested -= ApplyEthernetCheckerboardCalibration;
            }
        }

        // CalibrationWindow.ImageGrabber 델리게이트 — SIMUL 은 파일 선택(기존 GrabButton_Click 과 동일 관용구),
        //  실장비는 라이브 grab 후 임시 파일로 저장해 경로를 돌려준다(CalibrationWindow 계약: Func<string>).
        private string GrabEthernetCalibrationImage() {
#if SIMUL_MODE
            try {
                Ookii.Dialogs.Wpf.VistaOpenFileDialog dlg = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
                dlg.Filter = "이미지 파일|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|모든 파일|*.*";
                bool? bResult = dlg.ShowDialog();
                if (bResult == true) {
                    return dlg.FileName;
                }
                return null;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[캘리브] 이미지 선택 실패: " + ex.Message);
                return null;
            }
#else
            if (EthernetVisionHandler.Handle.Camera == null) {
                return null;
            }
            HImage grabbed = null;
            try {
                grabbed = EthernetVisionHandler.Handle.Camera.Grab();
                if (grabbed == null) {
                    return null;
                }
                return HalconTeachingHelper.SaveTempImage("EthernetCalibration_Tray", grabbed);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[캘리브] 라이브촬상 실패: " + ex.Message);
                return null;
            }
            finally {
                grabbed?.Dispose();
            }
#endif
        }

        // CalibrationWindow.ApplyRequested 핸들러 — 체커보드 결과(mm/px) → EthernetPixelResolution(μm/px) 반영.
        private void ApplyEthernetCheckerboardCalibration(CalibrationResult result) {
            if (result == null) return;
            double mmPerPixel = result.MmPerPixel;

            string warnLine;
            if (result.IsDistortionWarn) warnLine = string.Format("\n[경고] 외곽 왜곡 {0:F2}% — undistort 검토 권장", result.CenterOuterDeviationPct);
            else                         warnLine = "";

            string msg = string.Format(
                "Tray 픽셀 분해능을 1 px = {0:F5} mm 로 덮어씁니다.{1}\n적용하시겠습니까?",
                mmPerPixel, warnLine);
            MessageBoxResult confirm = CustomMessageBox.ShowConfirmation("캘리브레이션 적용", msg, MessageBoxButton.OKCancel);
            if (confirm != MessageBoxResult.OK) return;

            ApplyEthernetPixelResolutionMm(mmPerPixel);
            lbl_pixelCalibStatus.Text = string.Format("체커보드 캘리브 적용: 1px = {0:F5}mm (분해능 {1:F3}um/px)",
                mmPerPixel, SystemSetting.Handle.EthernetPixelResolution);
            CustomMessageBox.Show("캘리브레이션", string.Format("적용 + 저장 완료 (1 px = {0:F5} mm)", mmPerPixel));
        }

        // 거리 캘리브 — 뷰어에서 2점 클릭 → 실제 거리(mm) 입력 → mm/px 역산. MainView.CalibrateButton_Click 과 동일 흐름.
        private void CalibrateDistanceButton_Click(object sender, RoutedEventArgs e) {
            if (_viewer == null) {
                lbl_pixelCalibStatus.Text = "뷰어 미연결";
                return;
            }
            _isCalibratingDistance = true;
            _calibrationPoints.Clear();
            btn_calibrateDistance.Content = "Pick Point 1";
            lbl_pixelCalibStatus.Text = "캔버스에서 첫 번째 점을 클릭하세요";
            // 버튼을 두 번 이상 눌러 재진입해도(1점만 찍고 다시 누르는 등) 중복 구독이 쌓이지 않도록
            //  먼저 해제 후 구독한다 — 구독이 2개 쌓이면 클릭 1번에 핸들러가 2번 불려 같은 좌표가
            //  두 번 Add 되고, 그 두 "점"의 거리가 0이라 "너무 가깝습니다" 오류로 즉시 실패한다(실사용 재현).
            //  -= 는 구독 안 된 상태에서 호출해도 안전(no-op) — MainView.ExitCanvasMode 와 동일 관용구.
            _viewer.ImageLeftClicked -= Viewer_CalibrationDistanceMouseDown;
            _viewer.ImageLeftClicked += Viewer_CalibrationDistanceMouseDown;
        }

        private void Viewer_CalibrationDistanceMouseDown(object sender, MainViewerPointerChangedEventArgs e) {
            if (!_isCalibratingDistance) return;

            var pos = new System.Windows.Point(e.X, e.Y);
            _calibrationPoints.Add(pos);
            _viewer.SetCalibrationOverlay(_calibrationPoints);

            if (_calibrationPoints.Count == 1) {
                btn_calibrateDistance.Content = "Pick Point 2";
                lbl_pixelCalibStatus.Text = "캔버스에서 두 번째 점을 클릭하세요";
            }
            else if (_calibrationPoints.Count == 2) {
                _viewer.ImageLeftClicked -= Viewer_CalibrationDistanceMouseDown;
                _isCalibratingDistance = false;
                FinishCalibrateDistance();
            }
        }

        private void FinishCalibrateDistance() {
            var p1 = _calibrationPoints[0];
            var p2 = _calibrationPoints[1];
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double pixelDistance = Math.Sqrt(dx * dx + dy * dy);

            btn_calibrateDistance.Content = "거리 캘리브";

            if (pixelDistance < MIN_CALIB_PIXEL_DISTANCE) {
                CustomMessageBox.Show("두 점 사이의 거리가 너무 가깝습니다.", "캘리브레이션");
                lbl_pixelCalibStatus.Text = "캘리브 취소 (거리 부족)";
                return;
            }

            var calibPoints = new List<System.Windows.Point>(_calibrationPoints);
            _viewer.SetCalibrationOverlay(calibPoints,
                string.Format("{0:F1}px", pixelDistance),
                string.Format("가로 {0:F1}px", Math.Abs(dx)),
                string.Format("세로 {0:F1}px", Math.Abs(dy)));

            var dlg = new TextInputBoxWinidow(
                string.Format("두 점 사이의 실제 거리(mm)를 입력하세요:\n(픽셀 거리: {0:F1} px)\n\n[경고] 여기서 값을 입력하고 확인을 누르면 Tray 픽셀 분해능이 즉시 바뀝니다.", pixelDistance),
                "");
            dlg.Title = "실제 거리 입력";
            dlg.Owner = Window.GetWindow(this);

            if (dlg.ShowDialog() != true) {
                lbl_pixelCalibStatus.Text = "캘리브 취소";
                return;
            }
            double realMm;
            if (!double.TryParse(dlg.Text, out realMm) || realMm <= 0) {
                CustomMessageBox.Show("유효한 숫자를 입력하세요.", "캘리브레이션");
                lbl_pixelCalibStatus.Text = "캘리브 취소 (입력값 오류)";
                return;
            }

            double mmPerPixel = realMm / pixelDistance;
            ApplyEthernetPixelResolutionMm(mmPerPixel);

            string appliedTotalLabel = string.Format("{0:F3}mm ({1:F1}px)", realMm, pixelDistance);
            string appliedHLabel = string.Format("가로 {0:F3}mm", Math.Abs(dx) * mmPerPixel);
            string appliedVLabel = string.Format("세로 {0:F3}mm", Math.Abs(dy) * mmPerPixel);
            _viewer.SetCalibrationOverlay(calibPoints, appliedTotalLabel, appliedHLabel, appliedVLabel);

            lbl_pixelCalibStatus.Text = string.Format("거리 캘리브 적용: 1px = {0:F4}mm (분해능 {1:F3}um/px)",
                mmPerPixel, SystemSetting.Handle.EthernetPixelResolution);
            CustomMessageBox.Show("캘리브레이션 적용", string.Format("1px = {0:F4}mm 로 적용 + 저장했습니다.", mmPerPixel));
        }

        // mm/px → μm/px(EthernetPixelResolution 단위) 변환 후 반영 + 즉시 저장. 두 캘리브 방식의 공통 종착점.
        //  MainView 의 체커보드/거리 캘리브와 달리 즉시 SystemSetting.Save() 한다 — Tray/Bottom 은 shot 단위
        //  레시피가 아니라 PC 단위 설정값이라 "레시피 저장을 눌러야 반영" 이라는 사용자 개념 자체가 없다.
        private void ApplyEthernetPixelResolutionMm(double mmPerPixel) {
            const double UM_PER_MM = 1000.0;
            SystemSetting.Handle.EthernetPixelResolution = mmPerPixel * UM_PER_MM;
            SystemSetting.Handle.Save();
        }

        // ─── 티칭 핸들러 ─────────────────────────────────────────────────────────

        private void DrawRoi1Button_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — ROI 1 그리기 시작: 직전 슬롯1 내용 초기화 후 StartRectangleDrawing
            if (_viewer == null) {
                lbl_status.Text = "뷰어 미연결";
                return;
            }

            //260702 hbk 기존 ROI1이 있을 때만 재드로잉 확인 — '아니오' 시 초기화/드로잉 미실행
            if (_roi1 != null) {
                MessageBoxResult confirmRoi1 = CustomMessageBox.ShowConfirmation("ROI 재드로잉", "ROI 1을(를) 삭제하고 다시 그리시겠습니까?", MessageBoxButton.YesNo);
                if (confirmRoi1 != MessageBoxResult.Yes) {
                    return;
                }
            }

            _roi1 = null;
            _drawingSlot = 1;
            _isCalRoiDrawing = false;   // quick-260903-dpy — 티칭 ROI 드로잉 시작 시 캘 ROI 플래그 해제
            try {
                _viewer.StartRectangleDrawing();
                lbl_status.Text = "ROI 1 드래그 후 ROI 2 버튼을 클릭하세요";
            }
            catch (Exception ex) {
                lbl_status.Text = "ROI 1 그리기 오류: " + ex.Message;
            }
        }

        private void DrawRoi2Button_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — ROI 2 그리기: 슬롯 1 확정(CommitActiveRectangle) 후 슬롯 2 시작
            if (_viewer == null) {
                lbl_status.Text = "뷰어 미연결";
                return;
            }

            //260702 hbk 기존 ROI2가 있을 때만 재드로잉 확인 — '아니오' 시 슬롯1 확정/초기화/드로잉 전부 미실행
            if (_roi2 != null) {
                MessageBoxResult confirmRoi2 = CustomMessageBox.ShowConfirmation("ROI 재드로잉", "ROI 2을(를) 삭제하고 다시 그리시겠습니까?", MessageBoxButton.YesNo);
                if (confirmRoi2 != MessageBoxResult.Yes) {
                    return;
                }
            }

            try {
                // 슬롯 1 진행 중이었으면 확정
                if (_drawingSlot == 1) {
                    // 드래그 종료 시 이미 확정됐을 수 있다 — 그 경우 null 이 돌아오므로 덮어쓰지 않는다.
                    RoiDefinition committed1 = _viewer.CommitActiveRectangle();
                    if (committed1 != null) {
                        _roi1 = committed1;
                    }
                    ShowTeachRoiOverlays(); // Phase 74: 확정 후에도 ROI 가 보이게 유지
                }

                _roi2 = null;
                _drawingSlot = 2;
                _isCalRoiDrawing = false;   // quick-260903-dpy — 티칭 ROI 드로잉 시작 시 캘 ROI 플래그 해제
                _viewer.StartRectangleDrawing();
                lbl_status.Text = "ROI 2 드래그 후 티칭 저장을 클릭하세요";
            }
            catch (Exception ex) {
                lbl_status.Text = "ROI 2 그리기 오류: " + ex.Message;
            }
        }

        private void TeachButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 2-ROI 확정 + TryTeach 호출
            if (_viewer == null || _viewer.CurrentImage == null) {
                lbl_status.Text = "이미지 없음 — Grab 먼저";
                return;
            }

            try {
                // 슬롯 2 진행 중이었으면 확정
                if (_drawingSlot == 2) {
                    RoiDefinition committed2 = _viewer.CommitActiveRectangle();
                    if (committed2 != null) {
                        _roi2 = committed2;
                    }
                    ShowTeachRoiOverlays(); // Phase 74: 확정 후에도 ROI 가 보이게 유지
                }

                // 두 ROI 모두 유효한지 검증
                string validErr = ValidateRois();
                if (validErr != null) {
                    lbl_teachStatus.Text = TeachDiag.ToStatusLine(ETeachGrade.Weak, validErr);
                    lbl_teachStatus.Foreground = TeachDiag.GradeBrush(ETeachGrade.Weak);
                    return;
                }

                // ROI → TryTeach 파라미터 변환 (HALCON gen_rectangle2 규약)
                double r1, c1, phi1, l1_1, l1_2;
                RectToTeachParams(_roi1, out r1, out c1, out phi1, out l1_1, out l1_2);

                double r2, c2, phi2, l2_1, l2_2;
                RectToTeachParams(_roi2, out r2, out c2, out phi2, out l2_1, out l2_2);

                ApplyCoaxLight(); //260626 hbk Phase 66 — 티칭 직전 동축 자동 적용(D-07 티칭=런타임 조명 일치)
                string error;
                double dScore1, dScore2;   //quick-260812: 티칭이 이미 계산한 스코어 수신(등급 표시용)
                if (ApplyTeachParams(true) == false) {   // Phase 74: 값이 바뀌었으면 확인
                    lbl_teachStatus.Text = "티칭 취소 — 값 변경을 진행하지 않았습니다";
                    return;
                }
                bool bOk = EthernetVisionHandler.Handle.Matcher.TryTeach(
                    _viewer.CurrentImage,
                    r1, c1, phi1, l1_1, l1_2,
                    r2, c2, phi2, l2_1, l2_2,
                    VIEW_MODE,
                    out dScore1, out dScore2,
                    out error);

                if (bOk) {
                    bool bHas = EthernetVisionHandler.Handle.Matcher.HasTemplate(VIEW_MODE);
                    //quick-260812: 두 패턴 중 낮은 쪽 = 보수적 지표(런타임 검사와 같은 규칙)
                    double dMinScore = Math.Min(dScore1, dScore2);
                    ETeachGrade teachGrade = TeachDiag.ClassifyScore(dMinScore, AlignShapeMatchService.TeachMinScore);
                    lbl_teachStatus.Text = TeachDiag.ToStatusLine(teachGrade, "티칭 OK (HasTemplate=" + bHas + ", score " + dMinScore.ToString("F3") + ")");
                    lbl_teachStatus.Foreground = TeachDiag.GradeBrush(teachGrade);
                    if (brushPanel != null) {
                        brushPanel.ViewModel.ReloadMaskFromDisk();
                    }
                    ShowTeachedContour(); // Phase 74: 티칭 직후에도 모델 외곽선(녹색) 표시
                }
                else {
                    lbl_teachStatus.Text = TeachDiag.ToStatusLine(ETeachGrade.Bad, "티칭 실패: " + TeachDiag.ToKoreanMessage(error));
                    lbl_teachStatus.Foreground = TeachDiag.GradeBrush(ETeachGrade.Bad);
                }
                _drawingSlot = 0;
            }
            catch (Exception ex) {
                lbl_teachStatus.Text = TeachDiag.ToStatusLine(ETeachGrade.Bad, "티칭 예외: " + TeachDiag.ToKoreanMessage(ex.Message));
                lbl_teachStatus.Foreground = TeachDiag.GradeBrush(ETeachGrade.Bad);
            }
        }

        // 마스크가 바뀌었을 때 모달 없이 같은 ROI 로 다시 티칭한다(D-74-04).
        //  성공하면 null, 실패하면 오류 문자열을 돌려준다(ViewModel 계약).

        // Phase 74: 티칭 ROI(1/2)를 뷰어에 계속 보이게 한다.
        //  CommitActiveRectangle 은 확정과 동시에 draft 를 지우므로, 그대로 두면 그린 직후 ROI 가 사라진다.
        //  브러시로 "이 ROI 안의 어느 부분을 뺄지" 칠하려면 경계가 보여야 한다 — 안 보이면 눈 감고 칠하는 셈이다.
        //  detection 결과 오버레이와 같은 채널(orange)을 쓰므로 [검사] 를 돌리면 그 결과로 교체된다(의도된 동작).
        // Phase 74: 드래그를 마치는 즉시 티칭 ROI 를 확정한다.
        //  예전에는 다음 버튼을 누를 때까지 draft(빨간 사각형)로 남아 "그렸는데 주황색이 안 나온다" 가 됐다.
        private void OnTeachRectDrawn(object sender, EventArgs e) {
            if (_viewer == null) {
                return;
            }
            bool bTeachSlot = (_drawingSlot == 1) || (_drawingSlot == 2);
            if (bTeachSlot == false) {
                return;
            }
            try {
                RoiDefinition roi = _viewer.CommitActiveRectangle();
                if (roi == null) {
                    return;
                }
                if (_drawingSlot == 1) {
                    _roi1 = roi;
                    lbl_status.Text = "ROI 1 확정 — [ROI 2 그리기] 를 클릭하세요";
                }
                else {
                    _roi2 = roi;
                    lbl_status.Text = "ROI 2 확정 — [티칭 저장] 을 클릭하세요";
                }
                ShowTeachRoiOverlays();
            }
            catch {
                // 확정 실패는 기존 흐름(버튼 클릭 시 확정)으로 폴백된다.
            }
        }


        // Phase 74: 티칭 파라미터(각도범위/최소 Score) 입력을 서비스에 적용한다.
        //  비어 있거나 숫자가 아니면 0 → 서비스가 기본값(각도 Bottom 45/Tray 10, 스코어 0.5)을 쓴다.
        // Phase 74: 티칭 파라미터(각도범위/최소 Score)를 서비스에 적용한다.
        //  bAskOnChange=true 면 저장값과 다를 때 확인을 받는다(티칭은 모델을 다시 만드는 작업이라
        //  실수로 바꾸면 되돌리기 어렵다). 브러시 자동 재생성 경로는 모달 금지라 false 로 부른다.
        //  반환 false = 사용자가 취소.
        private bool ApplyTeachParams(bool bAskOnChange) {
            double dAngle = 0.0;
            double dScore = 0.0;
            if (txt_angleExtent != null) {
                double.TryParse(txt_angleExtent.Text, out dAngle);
            }
            if (txt_minScore != null) {
                double.TryParse(txt_minScore.Text, out dScore);
            }

            // 각도 하한 강제 — 입력이 하한보다 작으면 하한으로 올리고 화면에도 반영한다.
            double dAngleFloor = AlignShapeMatchService.MinAngleExtentDeg;
            if (dAngle > 0.0 && dAngle < dAngleFloor) {
                dAngle = dAngleFloor;
                if (txt_angleExtent != null) {
                    txt_angleExtent.Text = dAngle.ToString("F1");
                }
            }

            bool bScoreOutOfRange = (dScore < 0.0) || (dScore > 1.0);
            if (bScoreOutOfRange) {
                dScore = 0.0;   // 잘못된 입력은 기본값으로
            }

            if (bAskOnChange == true) {
                AlignRefPose saved = EthernetVisionHandler.Handle.Matcher.GetSlotRefPose(VIEW_MODE, EBottomAlignSlot.None);
                if (saved != null) {
                    double dSavedAngle = saved.AngleExtentDeg;
                    double dSavedScore = saved.MinScore;
                    double dNewAngle = EthernetVisionHandler.Handle.Matcher.ResolveAngleExtentDeg(VIEW_MODE, dAngle);
                    double dNewScore = EthernetVisionHandler.Handle.Matcher.ResolveMinScore(dScore);
                    bool bAngleChanged = (dSavedAngle > 0.0) && (Math.Abs(dSavedAngle - dNewAngle) > 0.001);
                    bool bScoreChanged = (dSavedScore > 0.0) && (Math.Abs(dSavedScore - dNewScore) > 0.001);
                    if (bAngleChanged || bScoreChanged) {
                        string szMsg = "티칭 값을 바꿔 모델을 다시 만듭니다." + Environment.NewLine + Environment.NewLine
                                     + "각도범위: " + dSavedAngle.ToString("F1") + " → " + dNewAngle.ToString("F1") + " deg" + Environment.NewLine
                                     + "최소 Score: " + dSavedScore.ToString("F2") + " → " + dNewScore.ToString("F2") + Environment.NewLine + Environment.NewLine
                                     + "진행할까요?";
                        MessageBoxResult confirm = CustomMessageBox.ShowConfirmation("티칭 값 변경", szMsg, MessageBoxButton.YesNo);
                        if (confirm != MessageBoxResult.Yes) {
                            return false;
                        }
                    }
                }
            }

            EthernetVisionHandler.Handle.Matcher.TeachAngleExtentDeg = dAngle;
            EthernetVisionHandler.Handle.Matcher.TeachMinScoreOverride = dScore;
            return true;
        }

        // 저장된 슬롯 JSON 의 값을 입력란에 되돌려 보여준다. 없으면 비워 둔다(= 기본값 사용).
        /// <summary>이 화면의 기본 각도범위(deg) 표시 문자열.</summary>
        private string ResolveDefaultAngleText() {
            double dDefault = EthernetVisionHandler.Handle.Matcher.ResolveAngleExtentDeg(VIEW_MODE, 0.0);
            return dDefault.ToString("F1");
        }

        private void LoadTeachParamsToUi(AlignRefPose refPose) {
            try {
                if (txt_angleExtent == null || txt_minScore == null) {
                    return;
                }
                if (refPose == null) {
                    // 저장값이 없으면 기본값을 그대로 보여준다 — 빈칸이면 무엇이 쓰이는지 알 수 없다.
                    txt_angleExtent.Text = ResolveDefaultAngleText();
                    txt_minScore.Text = AlignShapeMatchService.TeachMinScore.ToString("F2");
                    return;
                }
                if (refPose.AngleExtentDeg > 0.0) {
                    txt_angleExtent.Text = refPose.AngleExtentDeg.ToString("F1");
                }
                else {
                    txt_angleExtent.Text = ResolveDefaultAngleText();
                }
                if (refPose.MinScore > 0.0) {
                    txt_minScore.Text = refPose.MinScore.ToString("F2");
                }
                else {
                    txt_minScore.Text = AlignShapeMatchService.TeachMinScore.ToString("F2");
                }
            }
            catch {
                // 표시 실패는 티칭에 영향을 주지 않는다.
            }
        }


        // Phase 74: 이미지 중심 십자선 토글. 라이브/정지 화면 모두에서 가운데 위치를 눈으로 잡는 용도.
        private void ShowCenterCrossCheckBox_Changed(object sender, RoutedEventArgs e) {
            if (_viewer == null) {
                return;
            }
            bool bShow = (chk_showCenterCross.IsChecked == true);
            _viewer.SetCenterCrossVisible(bShow);
        }


        // Phase 74: 마우스 위치의 이미지 좌표와 밝기(Gray) 표시. 뷰어의 기존 PointerInfoChanged 를 그대로 쓴다.
        private void OnViewerPointerInfoChanged(object sender, MainViewerPointerChangedEventArgs e) {
            if (lbl_hoverInfo == null) {
                return;
            }
            try {
                string szGray = "-";
                if (e.GrayValue.HasValue) {
                    szGray = e.GrayValue.Value.ToString("F0");
                }
                lbl_hoverInfo.Text = "X: " + e.X.ToString("F0")
                                   + "  Y: " + e.Y.ToString("F0")
                                   + "  Gray: " + szGray;
            }
            catch {
                // 표시 실패는 다른 동작에 영향을 주지 않는다.
            }
        }


        // Phase 74: 거리 재기. 캘리브레이션 값은 건드리지 않고 두 점 사이 거리만 보여준다.
        //  캘리브와 같은 2점 클릭 방식이라 조작이 익숙하고, 오버레이도 같은 삼각형 표시를 쓴다.
        private bool _isMeasuringDistance;
        private readonly List<System.Windows.Point> _measurePoints = new List<System.Windows.Point>();

        private void MeasureDistanceButton_Click(object sender, RoutedEventArgs e) {
            if (_viewer == null) {
                lbl_pixelCalibStatus.Text = "뷰어 미연결";
                return;
            }
            _isMeasuringDistance = true;
            _measurePoints.Clear();
            btn_measureDistance.Content = "측정: 점1 클릭";
            lbl_pixelCalibStatus.Text = "거리 측정 — 첫 번째 점을 클릭하세요";
            // 재진입 시 중복 구독 방지(캘리브 경로와 동일 규약)
            _viewer.ImageLeftClicked -= Viewer_MeasureDistanceMouseDown;
            _viewer.ImageLeftClicked += Viewer_MeasureDistanceMouseDown;
        }

        private void Viewer_MeasureDistanceMouseDown(object sender, MainViewerPointerChangedEventArgs e) {
            if (!_isMeasuringDistance) {
                return;
            }

            System.Windows.Point pos = new System.Windows.Point(e.X, e.Y);
            _measurePoints.Add(pos);
            _viewer.SetCalibrationOverlay(_measurePoints);

            if (_measurePoints.Count == 1) {
                btn_measureDistance.Content = "측정: 점2 클릭";
                lbl_pixelCalibStatus.Text = "거리 측정 — 두 번째 점을 클릭하세요";
                return;
            }

            _viewer.ImageLeftClicked -= Viewer_MeasureDistanceMouseDown;
            _isMeasuringDistance = false;
            btn_measureDistance.Content = "거리 측정";
            FinishMeasureDistance();
        }

        private void FinishMeasureDistance() {
            const double UM_PER_MM = 1000.0;
            System.Windows.Point p1 = _measurePoints[0];
            System.Windows.Point p2 = _measurePoints[1];
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dPixel = Math.Sqrt(dx * dx + dy * dy);

            // EthernetPixelResolution 은 μm/px — 1000 으로 나눠야 mm/px 다. 헷갈리면 1000배 틀린다.
            double dMmPerPixel = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;
            bool bCalibrated = dMmPerPixel > 0.0;

            string szTotal;
            string szH;
            string szV;
            if (bCalibrated) {
                szTotal = string.Format("{0:F3}mm ({1:F1}px)", dPixel * dMmPerPixel, dPixel);
                szH = string.Format("가로 {0:F3}mm", Math.Abs(dx) * dMmPerPixel);
                szV = string.Format("세로 {0:F3}mm", Math.Abs(dy) * dMmPerPixel);
                lbl_pixelCalibStatus.Text = string.Format("측정 결과: {0:F3} mm  ({1:F1} px)  가로 {2:F3} / 세로 {3:F3} mm",
                    dPixel * dMmPerPixel, dPixel, Math.Abs(dx) * dMmPerPixel, Math.Abs(dy) * dMmPerPixel);
            }
            else {
                // 캘리브 전이면 mm 로 환산하지 않는다 — 0 을 곱해 0mm 로 보여주면 오독된다.
                szTotal = string.Format("{0:F1}px", dPixel);
                szH = string.Format("가로 {0:F1}px", Math.Abs(dx));
                szV = string.Format("세로 {0:F1}px", Math.Abs(dy));
                lbl_pixelCalibStatus.Text = string.Format("측정 결과: {0:F1} px (거리 캘리브 전 — mm 환산 불가)", dPixel);
            }

            List<System.Windows.Point> pts = new List<System.Windows.Point>(_measurePoints);
            _viewer.SetCalibrationOverlay(pts, szTotal, szH, szV);
        }

        private void ShowTeachRoiOverlays() {
            if (_viewer == null) {
                return;
            }
            try {
                List<double[]> rects = new List<double[]>();
                List<string> labels = new List<string>();
                AddTeachRoiRect(rects, labels, _roi1, "ROI 1");
                AddTeachRoiRect(rects, labels, _roi2, "ROI 2");
                // quick-260903-dpy — 캘리브레이션 검색 ROI 도 확정 후 사라지지 않도록 같이 표시한다
                //  (Bottom 의 동일 관용구). SetResultRoiOverlays 는 매번 전체를 덮어쓰므로 별도 메서드로
                //  분리하면 서로 지워버린다 — 반드시 이 한 메서드 안에서 같이 그린다.
                if (_calRoiSet == true) {
                    AddTeachRoiRect(rects, labels, _calRoiRect, "캘 검색 ROI");
                }
                _viewer.SetResultRoiOverlays(null, rects, labels);
            }
            catch {
                // 표시 실패는 티칭 흐름에 영향을 주지 않는다.
            }
        }

        private void AddTeachRoiRect(List<double[]> rects, List<string> labels, RoiDefinition roi, string szLabel) {
            if (roi == null) {
                return;
            }
            double row, col, phi, len1, len2;
            RectToTeachParams(roi, out row, out col, out phi, out len1, out len2);
            bool bValid = (len1 > 0.0) && (len2 > 0.0);
            if (bValid == false) {
                return;
            }
            rects.Add(new double[] { row, col, phi, len1, len2 });
            labels.Add(szLabel);
        }

        private string RegenerateTeachSilent() {
            if (_viewer == null) {
                return "뷰어 없음";
            }
            if (_viewer.CurrentImage == null) {
                return "이미지 없음 — Grab 먼저";
            }
            string szValid = ValidateRois();
            if (szValid != null) {
                return szValid;
            }

            double r1, c1, phi1, l1_1, l1_2;
            RectToTeachParams(_roi1, out r1, out c1, out phi1, out l1_1, out l1_2);
            double r2, c2, phi2, l2_1, l2_2;
            RectToTeachParams(_roi2, out r2, out c2, out phi2, out l2_1, out l2_2);

            ApplyTeachParams(false);   // Phase 74: 브러시 경로는 모달 금지 — 확인 없이 현재 값 사용

            string szError;
            double dScore1, dScore2;
            bool bOk = EthernetVisionHandler.Handle.Matcher.TryTeach(
                _viewer.CurrentImage,
                r1, c1, phi1, l1_1, l1_2,
                r2, c2, phi2, l2_1, l2_2,
                VIEW_MODE,
                out dScore1, out dScore2,
                out szError);
            if (bOk == true) {
                ShowTeachedContour(); // Phase 74: 마스크 반영 결과를 녹색 외곽선으로 즉시 보여준다
                return null;
            }
            return szError;
        }

        // Phase 74: 티칭된 모델의 외곽선을 녹색으로 표시한다.
        //  예전에는 [검사] 를 돌려야만 녹색이 보여서, 브러시로 뺀 영역이 실제로 모델에서
        //  빠졌는지 티칭 직후에 확인할 방법이 없었다.
        private void ShowTeachedContour() {
            if (_viewer == null) {
                return;
            }
            try {
                HObject xld;
                string szError;
                bool bOk = EthernetVisionHandler.Handle.Matcher.TryBuildTeachedContourXld(
                    VIEW_MODE, EBottomAlignSlot.None, out xld, out szError);
                if (bOk == true) {
                    _viewer.SetAlignContourXld(xld); // 소유권 이전 — 뷰어가 Dispose
                }
            }
            catch {
                // 표시 실패는 티칭 결과에 영향을 주지 않는다.
            }
        }

        // ─── 검사 핸들러 ─────────────────────────────────────────────────────────

        private void RunButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — Matcher.Run 호출 → AlignResult X/Y Offset + Score 표시 (Tray: Theta 없음)
            if (_viewer == null || _viewer.CurrentImage == null) {
                lbl_status.Text = "이미지 없음 — Grab 먼저";
                return;
            }

            // Phase 74: 검사 전에 브러시 작업을 끝낸다 — 마스크 자국이 검사 결과(녹색) 위를 덮지 않게.
            if (brushPanel != null) {
                brushPanel.ViewModel.IsBrushActive = false;
                brushPanel.ViewModel.IsEraseMode = false;
            }

            //260702 hbk 모델 미티칭 상태에서 검사 방지 — 안내 후 중단
            bool bHasModel = false; //260702 hbk 기본 false: 예외/미초기화 시 검사 차단
            try {
                bHasModel = EthernetVisionHandler.Handle.Matcher.HasTemplate(VIEW_MODE); //260702 hbk Tray 단일 경로 템플릿 확인
            }
            catch {
                bHasModel = false; //260702 hbk Matcher 예외 시 안전 차단
            }
            if (!bHasModel) {
                CustomMessageBox.Show("검사 불가", "모델이 없습니다. 먼저 티칭을 완료하세요.", MessageBoxImage.Warning); //260702 hbk 안내
                lbl_status.Text = "모델 없음"; //260702 hbk 상태 라벨 반영
                return;
            }

            try {
                lbl_status.Text = "검사중";
                ApplyCoaxLight(); //260626 hbk Phase 66 — 검사 직전 동축 자동 적용(D-07)
                AlignResult res = EthernetVisionHandler.Handle.Matcher.Run(_viewer.CurrentImage, VIEW_MODE);

                if (res.Found) {
                    lbl_result.Text = FormatAlignResult(res);
                    ApplyAlignVisualization(res);          //260625 hbk Phase 61.1 검출 시각화
                }
                else {
                    lbl_result.Text = "검출 실패";
                    ClearAlignVisualization();             //260625 hbk Phase 61.1 이전 오버레이 제거
                }
                // D-75-01 보강: 화면 [검사] 도 ① 기록을 남긴다.
                //  PLC 가 없는 셋업 기간에도 잔여 산포가 쌓여야 임계값을 정할 수 있다.
                //  자재번호는 -1(수동). 검출 성공=OK / 실패=NG 로만 적는다 —
                //  공차 P/F 는 PLC 사이클의 판단이라 여기서 흉내내지 않는다.
                SystemHandler.Handle.RecordAlignVerifyManual(
                    VIEW_MODE, EBottomAlignSlot.None, _viewer.CurrentImage, res, res.Found);

                lbl_status.Text = "대기";
            }
            catch (Exception ex) {
                lbl_result.Text = "검사 예외: " + ex.Message;
                lbl_status.Text = "대기";
            }
        }

        // ─── 체크박스 토글 핸들러 ─────────────────────────────────────────────────

        private void ShowRoiCheckBox_Changed(object sender, RoutedEventArgs e) {
            //260625 hbk Phase 61.1 보정 ROI(orange) = datumRects 채널 = _datumOverlayVisible 게이트
            if (_viewer == null) {
                return;
            }
            bool bShow = (chk_showRoi.IsChecked == true);
            try {
                _viewer.SetDatumOverlayVisible(bShow);
            }
            catch {
                // 뷰어 예외 무시 — UI 무중단
            }
        }

        private void ShowEdgeCheckBox_Changed(object sender, RoutedEventArgs e) {
            //260625 hbk Phase 61.1 에지(_inspectionOverlays) = _measurementOverlayVisible 게이트
            if (_viewer == null) {
                return;
            }
            bool bShow = (chk_showEdge.IsChecked == true);
            try {
                _viewer.SetMeasurementOverlayVisible(bShow);
            }
            catch {
                // 뷰어 예외 무시 — UI 무중단
            }
        }

        // ─── 동축 조명 핸들러 (260626 hbk Phase 66 D-04/D-05/D-07) ─────────────

        /// <summary>
        /// 현재 UI 동축값(chk_coaxEnabled + sld_coaxLevel)을 LIGHT_ALIGN_COAX 에 적용.
        /// Enabled=true: SetOnOff(true)+SetLevel. Enabled=false: SetOnOff(false)만.
        /// 예외 시 lbl_status 갱신만 — throw 금지(T-66-UI-01).
        /// </summary>
        private void ApplyCoaxLight() //260626 hbk Phase 66 D-06/D-07 — 현재 UI 동축값을 LIGHT_ALIGN_COAX 에 적용
        {
            try
            {
                bool bEnabled = (chk_coaxEnabled.IsChecked == true);   //260626 hbk 체크박스 상태
                int nLevel = (int)sld_coaxLevel.Value;                 //260626 hbk 슬라이더 밝기
                if (bEnabled)
                {
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);    //260626 hbk 동축 ON
                    LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, nLevel);  //260626 hbk 동축 밝기
                }
                else
                {
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);   //260626 hbk 동축 OFF
                }
            }
            catch (Exception ex)
            {
                lbl_status.Text = "동축 적용 오류: " + ex.Message;   //260626 hbk throw 금지 — 상태 라벨만
            }
        }

        //260626 hbk Phase 66 D-07 — 동축 체크박스 변경: 즉시 조명 적용 + Tray JSON 저장(수동 override)
        private void CoaxCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingCoax) return;   //260626 hbk WR-02: 로드 중 연쇄 저장 차단
            ApplyCoaxLight();        //260626 hbk 즉시 반영
            SaveTrayCoaxToJson();    //260626 hbk Tray JSON 갱신
        }

        //260626 hbk Phase 66 D-07 — 동축 슬라이더 변경: 라벨 갱신 + 즉시 적용 + Tray JSON 저장
        private void CoaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingCoax) return;   //260626 hbk WR-02: 로드 중 연쇄 저장 차단
            int nLevel = (int)e.NewValue;   //260626 hbk 새 밝기
            if (lbl_coaxLevel != null)
            {
                lbl_coaxLevel.Text = nLevel.ToString();   //260626 hbk 라벨 갱신(초기화 전 null 가드)
            }
            ApplyCoaxLight();
            SaveTrayCoaxToJson();
        }

        //260626 hbk Phase 66 D-05 — Tray 단일 동축값을 Tray.json 에 저장(slot=None = Tray 단일 경로, BuildJsonPath(Tray,None)=Tray.json).
        private void SaveTrayCoaxToJson()
        {
            try
            {
                bool bEnabled = (chk_coaxEnabled.IsChecked == true);   //260626 hbk 체크 상태
                int nLevel = (int)sld_coaxLevel.Value;                 //260626 hbk 밝기
                string error;
                bool bOk = EthernetVisionHandler.Handle.Matcher.TrySaveCoax(VIEW_MODE, EBottomAlignSlot.None, bEnabled, nLevel, out error);   //260626 hbk Tray 단일 — slot None
                if (!bOk)
                {
                    lbl_status.Text = "동축 저장 실패: " + error;   //260626 hbk
                }
            }
            catch (Exception ex)
            {
                lbl_status.Text = "동축 저장 오류: " + ex.Message;   //260626 hbk throw 금지
            }
        }

        //260626 hbk Phase 66 D-05 — Tray.json 동축값을 UI 에 복원. null(파일 없음/미티칭) → off/0.
        private void LoadTrayCoaxToUi()
        {
            _isLoadingCoax = true;   //260626 hbk WR-02: UI 값 설정 중 이벤트 연쇄 저장 차단 시작
            try
            {
                AlignRefPose refPose = EthernetVisionHandler.Handle.Matcher.GetSlotRefPose(VIEW_MODE, EBottomAlignSlot.None);   //260626 hbk Tray 단일 로드
                bool bEnabled = false;   //260626 hbk 기본값 off
                int nLevel = 0;          //260626 hbk 기본값 0
                if (refPose != null)
                {
                    bEnabled = refPose.CoaxEnabled;   //260626 hbk 저장된 동축 ON/OFF
                    nLevel = refPose.CoaxLevel;       //260626 hbk 저장된 동축 밝기
                }
                chk_coaxEnabled.IsChecked = bEnabled;   //260626 hbk UI 복원
                sld_coaxLevel.Value = nLevel;
                lbl_coaxLevel.Text = nLevel.ToString();
            }
            catch (Exception ex)
            {
                lbl_status.Text = "동축 복원 오류: " + ex.Message;   //260626 hbk throw 금지
            }
            finally
            {
                _isLoadingCoax = false;   //260626 hbk WR-02: 예외 발생 여부 무관하게 플래그 복원
            }
        }

        // ─── 시각화 헬퍼 (260625 hbk Phase 61.1) ────────────────────────────────

        /// <summary>
        /// Run 성공 시 보정 ROI 박스 + 에지 contour 를 MainResultViewerControl 에 전달.
        /// MainResultViewerControl.Render() 게이트 매핑:
        ///   datumRects(보정 ROI orange) → _datumOverlayVisible = [ROI 표시] 체크박스
        ///   _inspectionOverlays(에지 XLD contour 선) → _measurementOverlayVisible = [에지 표시] 체크박스
        ///260625 hbk Phase 61.1 — F1: 검출 십자 제거(에지를 contour 선으로 대체).
        /// 예외 시 throw 없이 결과 텍스트만 유지 (T-61.1-05 완화).
        /// </summary>
        private void ApplyAlignVisualization(AlignResult res) {
            if (_viewer == null) {
                return;
            }
            if (!res.HasDetection) {
                ClearAlignVisualization();
                return;
            }

            //260625 hbk Phase 61.1 — F1: 검출 십자(SetDatumFindResultOverlay) 제거. 에지는 XLD contour 선으로만 표시.

            try {
                // 1) 보정 ROI 박스: datumRects 채널(orange) — measRects=null 로 green 채널 미사용
                List<double[]> datumRects = res.DetectedRoiBoxes;
                if (datumRects == null) {
                    datumRects = new List<double[]>();
                }
                // Phase 74: 검출 박스에도 이름표를 붙인다. 박스만 있으면 어느 패턴인지 알 수 없고,
                //  라벨 없이 교체하면 티칭 때 보이던 "ROI 1/ROI 2" 가 검사 후 사라진 것처럼 보인다.
                List<string> datumLabels = new List<string>();
                for (int i = 0; i < datumRects.Count; i++) {
                    datumLabels.Add("ROI " + (i + 1).ToString());
                }
                _viewer.SetResultRoiOverlays(null, datumRects, datumLabels);
            }
            catch {
                // ROI 렌더 실패 무시
            }

            try {
                //260625 hbk Phase 61.1 F4 — 에지 = 검출 XLD object 직접 disp (대각선 버그 해소).
                //  점 polyline(BuildEdgeOverlays) 폐기. SetAlignContourXld 소유권 이전 → 뷰어가 dispose.
                _viewer.SetAlignContourXld(res.DetectedContourXld);
                res.DetectedContourXld = null;   // 소유권 이전 완료 — 중복 dispose 방지
            }
            catch {
                // 에지 렌더 실패 무시
            }
        }

        /// <summary>
        /// Run 실패(검출 없음) 또는 뷰 전환 시 이전 오버레이 제거.
        ///260625 hbk Phase 61.1 F4 — 에지는 SetAlignContourXld(null) 로 정리(XLD 채널).
        /// </summary>
        private void ClearAlignVisualization() {
            if (_viewer == null) {
                return;
            }
            try {
                _viewer.ClearDatumFindResultOverlay();
                _viewer.ClearResultRoiOverlays();
                _viewer.SetAlignContourXld(null);
            }
            catch {
                // 클리어 실패 무시
            }
        }

        // ─── 피커센터 캘 핸들러 (quick-260903-dpy — Bottom AV-05 이식, Tray 전용 회전중심) ──────
        //  EthernetVisionHandler.Handle.PickerCal 은 Bottom/Tray 공용 단일 인스턴스다(Task 4).
        //  모델 경로도 {Recipe}\ETHERNET_ALIGN\picker_cal.shm 로 모드 무관 고정이지만, 이 PC 는
        //  EthernetVisionModeValue 가 하나뿐이라 Bottom/Tray 동시 사용이 없어 충돌하지 않는다.
        //  모드별로 인스턴스/모델을 분리하는 구조 변경은 하지 않는다(불필요한 구조 변경 금지).

        // 캘 스텝당 피커 회전각(검사용 각도범위와 별개). 360/각도 = 필요 스텝 수.
        //  SystemSetting.PickerCalStepAngleDeg 는 Bottom 과 공용 설정이다(Task 1 범위 밖 — 새로 안 늘림).
        private void CalStepAngleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            try {
                ComboBoxItem item = cmb_calStepAngle.SelectedItem as ComboBoxItem;
                if (item == null) {
                    return;
                }
                double dAngle = 0.0;
                double.TryParse(item.Content.ToString(), out dAngle);
                if (dAngle <= 0.0) {
                    return;
                }
                SystemSetting.Handle.PickerCalStepAngleDeg = dAngle;
                RefreshCalStepInfo();
            }
            catch {
                // 선택 반영 실패는 캘 흐름을 막지 않는다.
            }
        }

        // 현재 스텝 각도로 필요한 스텝 수를 화면에 보여준다.
        private void RefreshCalStepInfo() {
            if (lbl_calStepInfo == null) {
                return;
            }
            int nSteps = SystemSetting.Handle.PickerCalRequiredSteps;
            lbl_calStepInfo.Text = "deg · " + nSteps.ToString() + "스텝";
        }

        // 저장된 스텝 각도를 콤보에 반영한다.
        private void LoadCalStepAngleToUi() {
            try {
                if (cmb_calStepAngle == null) {
                    return;
                }
                string szCurrent = SystemSetting.Handle.PickerCalStepAngleDeg.ToString("F0");
                foreach (object o in cmb_calStepAngle.Items) {
                    ComboBoxItem item = o as ComboBoxItem;
                    if (item == null) {
                        continue;
                    }
                    if (item.Content.ToString() == szCurrent) {
                        cmb_calStepAngle.SelectedItem = item;
                        break;
                    }
                }
                RefreshCalStepInfo();
            }
            catch {
                // 표시 실패는 캘 흐름을 막지 않는다.
            }
        }

        // 캘 ROI 사각형 완료 수거. 티칭 ROI 확정(_roi1/_roi2)은 OnTeachRectDrawn 이 전담하므로
        //  Bottom 의 else 분기(CommitTeachRoiOnDraw)에 대응하는 로직은 Tray 에 이식하지 않는다
        //  (같은 이벤트에 두 핸들러가 이미 구독돼 있어 중복 처리가 된다 — AttachSharedViewer 참고).
        //  _isCalRoiDrawing 이 false 면 이 이벤트는 캘 ROI 몫이 아니므로 관여하지 않는다.
        private void OnCalRectDrawn(object sender, EventArgs e) {
            if (!_isCalRoiDrawing) {
                return;
            }
            _isCalRoiDrawing = false;

            try {
                RoiDefinition roi = _viewer.CommitActiveRectangle();
                if (roi == null) {
                    lbl_calStatus.Text = "ROI 수거 실패";
                    return;
                }
                _calRoiRect = roi;
                _calRoiSet  = true;
                // TCP $ALIGN_CALIB STEP 경로 공유를 위해 SystemSetting 에도 동시 저장(Bottom 과 공용 값)
                SystemSetting.Handle.CalibSearchRow1 = roi.Row1;
                SystemSetting.Handle.CalibSearchCol1 = roi.Column1;
                SystemSetting.Handle.CalibSearchRow2 = roi.Row2;
                SystemSetting.Handle.CalibSearchCol2 = roi.Column2;
                double dW = roi.Column2 - roi.Column1;
                double dH = roi.Row2 - roi.Row1;
                ShowTeachRoiOverlays(); // 확정 후에도 캘 검색 ROI 가 보이게 유지(ShowTeachRoiOverlays 가 함께 그림)
                UpdateCalButtonState("검색 ROI 설정됨 (w=" + dW.ToString("F0") + " h=" + dH.ToString("F0") + ")");
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "ROI 수거 오류: " + ex.Message;
            }
        }

        private void CalResetButton_Click(object sender, RoutedEventArgs e) {
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            MessageBoxResult confirmReset = CustomMessageBox.ShowConfirmation(
                "캘 초기화", "캘리브레이션 모델/누적 데이터를 삭제하시겠습니까?", MessageBoxButton.YesNo);
            if (confirmReset != MessageBoxResult.Yes) {
                lbl_calStatus.Text = "초기화 취소";
                return;
            }

            try {
                EthernetVisionHandler.Handle.PickerCal.Reset();
                lbl_pickerCenter.Text = "";
                _calRoiSet = false;
                // 화면만 지우면 안 된다 — 저장된 검색 ROI 값이 남아 있으면 TCP $ALIGN_CALIB STEP 경로가
                //  계속 옛 ROI 를 쓴다. 실제로 지운다(Bottom 과 공용 값이라 Bottom 초기화와도 동일 효과).
                _calRoiRect = null;
                SystemSetting.Handle.CalibSearchRow1 = 0.0;
                SystemSetting.Handle.CalibSearchCol1 = 0.0;
                SystemSetting.Handle.CalibSearchRow2 = 0.0;
                SystemSetting.Handle.CalibSearchCol2 = 0.0;
                if (_viewer != null) {
                    _viewer.SetAlignContourXld(null);
                }
                ShowTeachRoiOverlays(); // 캘 ROI 해제를 화면에도 반영
                UpdateCalButtonState("누적 0 · 검색 ROI 삭제됨");
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "초기화 오류: " + ex.Message;
            }
        }

        // quick-260903-dpy — 수동 반복(자재를 손으로 놓고 찍기) 중 한 번만 잘못돼도 [초기화]로
        //  전부 다시 하지 않도록, 누적의 마지막 1개만 제거한다. Reset() 과 달리 확인 다이얼로그를
        //  두지 않는다 — 이 동작 자체가 "방금 실수를 되돌리는" 되돌리기 동작이라 나머지 스텝은 그대로 유지된다.
        private void CalRemoveLastStepButton_Click(object sender, RoutedEventArgs e) {
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            try {
                bool bRemoved = EthernetVisionHandler.Handle.PickerCal.TryRemoveLastStep();
                if (!bRemoved) {
                    UpdateCalButtonState("취소할 스텝이 없습니다");
                    return;
                }
                if (_viewer != null) {
                    // TryRemoveLastStep 이 서비스 내부 _vizXld 를 비웠다 — 화면 오버레이도 같이 정리한다.
                    _viewer.SetAlignContourXld(null);
                }
                int stepCount = EthernetVisionHandler.Handle.PickerCal.StepCount;
                UpdateCalButtonState("마지막 스텝 취소됨 · 누적 " + stepCount);
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "마지막 취소 오류: " + ex.Message;
            }
        }

        private void CalDrawRoiButton_Click(object sender, RoutedEventArgs e) {
            if (_viewer == null) {
                lbl_calStatus.Text = "뷰어 미연결";
                return;
            }

            try {
                _isCalRoiDrawing = true; // 사각형 완료 이벤트를 캘 ROI 로 처리할 플래그 세트
                _viewer.StartRectangleDrawing();
                lbl_calStatus.Text = "검색 ROI 를 드래그하세요";
            }
            catch (Exception ex) {
                _isCalRoiDrawing = false;
                lbl_calStatus.Text = "ROI 드로잉 오류: " + ex.Message;
            }
        }

        /// <summary>
        /// quick-260903-dpy — 사용자 결정: Tray 는 카메라 그랩을 우선한다(Bottom 의 TryResolveCalSourceImage
        /// 와 반대 순서). Bottom 은 뷰어에 파일 이미지가 열려 있으면 카메라를 아예 안 찍고 그 파일을
        /// 그대로 쓴다 — [폴더 열기]로 예전 사진을 열어둔 채 [Cal 모델 티칭]/[스텝 추가]를 누르면
        /// 경고 없이 낡은 사진으로 캘이 잡히는 결함이 있다(코드리뷰 지적, 미수정). Tray 에 그 결함을
        /// 그대로 이식하지 않기 위해 우선순위를 뒤집는다: 카메라가 열려 있으면 항상 Grab, 카메라를
        /// 못 쓸 때만 뷰어의 현재 이미지로 폴백(오프라인 테스트용). IsOpen 을 반드시 먼저 확인하는
        /// 이유는 EthernetAlignCamera.Grab() 이 카메라 미연결 시 AlignFallbackImagePath 정지 이미지를
        /// 조용히 돌려주기 때문 — 그걸 그대로 쓰면 운영자가 실패를 인지 못한 채 캘 데이터를 쌓는다.
        /// szSourceLabel 은 어느 소스를 썼는지 lbl_calStatus 에 표시하기 위한 값이다(재발 방지책).
        /// bOwnsImage: false 면 뷰어 소유(Dispose 금지), true 면 호출자 소유(Dispose 책임).
        /// </summary>
        private bool TryResolveCalSourceImage(out HImage img, out bool bOwnsImage, out string szSourceLabel) {
            img = null;
            bOwnsImage = false;
            szSourceLabel = "";

            EthernetAlignCamera cam = EthernetVisionHandler.Handle.Camera;
            bool bCameraReady = false;
            if (cam != null) {
                bCameraReady = cam.IsOpen;
            }
            if (bCameraReady) {
                HImage grabbed = cam.Grab();
                if (grabbed == null) {
                    lbl_calStatus.Text = "Grab 실패";
                    return false;
                }
                if (_viewer != null) {
                    _viewer.LoadImage(grabbed); // 뷰어가 내부 Clone — 원본 소유권은 이쪽에 남는다
                }
                img = grabbed;
                bOwnsImage = true;
                szSourceLabel = "라이브";
                return true;
            }

            // 카메라 미연결/미오픈 — 뷰어의 현재 이미지로 폴백(오프라인 테스트용).
            bool bViewerHasImage = (_viewer != null) && (_viewer.CurrentImage != null);
            if (bViewerHasImage) {
                img = _viewer.CurrentImage; // 뷰어 소유 — Dispose 금지
                bOwnsImage = false;
                szSourceLabel = "저장 이미지";
                return true;
            }

            lbl_calStatus.Text = "이미지 없음 — 카메라 연결을 확인하거나 [폴더 열기] 로 영상을 불러오세요";
            return false;
        }

        private void CalTeachModelButton_Click(object sender, RoutedEventArgs e) {
            if (!_calRoiSet) {
                lbl_calStatus.Text = "검색 ROI 미설정 — ROI(사각형) 지정 먼저";
                return;
            }
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            HImage img = null;
            bool bOwnsImage = false;
            string szSourceLabel;
            try {
                bool bResolved = TryResolveCalSourceImage(out img, out bOwnsImage, out szSourceLabel);
                if (!bResolved) {
                    return;
                }

                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryTeachModel(
                    img,
                    _calRoiRect.Row1, _calRoiRect.Column1,
                    _calRoiRect.Row2, _calRoiRect.Column2,
                    out error);

                if (bOk) {
                    UpdateCalButtonState("모델 티칭 완료 (" + szSourceLabel + ")");
                }
                else {
                    lbl_calStatus.Text = "모델 티칭 실패: " + error;
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "모델 티칭 오류: " + ex.Message;
            }
            finally {
                bool bShouldDispose = bOwnsImage && (img != null);
                if (bShouldDispose) {
                    try { img.Dispose(); } catch { }
                }
            }
        }

        private void CalAddStepButton_Click(object sender, RoutedEventArgs e) {
            if (!_calRoiSet) {
                lbl_calStatus.Text = "검색 ROI 미설정 — ROI(사각형) 지정 먼저";
                return;
            }
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            // 모델 미로드 시 파일에서 자동 로드 시도 (TCP START 경로와 동일)
            if (!EthernetVisionHandler.Handle.PickerCal.HasModel) {
                string loadErr;
                bool bLoaded = EthernetVisionHandler.Handle.PickerCal.TryLoadModel(out loadErr);
                if (!bLoaded) {
                    lbl_calStatus.Text = "모델 미로드 — [Cal 모델 티칭] 먼저 실행하세요";
                    return;
                }
                lbl_calStatus.Text = "모델 로드됨";
            }

            HImage img = null;
            bool bOwnsImage = false;
            string szSourceLabel;
            try {
                bool bResolved = TryResolveCalSourceImage(out img, out bOwnsImage, out szSourceLabel);
                if (!bResolved) {
                    return;
                }

                double foundRow, foundCol;
                double calScore;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryAddStep(
                    img,
                    _calRoiRect.Row1, _calRoiRect.Column1,
                    _calRoiRect.Row2, _calRoiRect.Column2,
                    out foundRow, out foundCol, out calScore, out error);

                if (bOk) {
                    ETeachGrade calGrade = TeachDiag.ClassifyScore(calScore, PickerCenterCalibrationService.FindMinScore);
                    string szStepDetail = TeachDiag.ToStatusLine(calGrade,
                        szSourceLabel + " · last=(" + foundRow.ToString("F1") + "," + foundCol.ToString("F1") + ")  score " + calScore.ToString("F3"));
                    if (_viewer != null) {
                        HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                        _viewer.SetAlignContourXld(vizXld); // 소유권 이전
                    }

                    // 저장 이미지(폴더 로더)로 스텝을 잡았을 때만 자동으로 다음 이미지로 넘어간다.
                    //  라이브 grab 스텝은 폴더 인덱스와 무관하므로 자동 넘김 대상이 아니다.
                    bool bUsedOfflineImage = (bOwnsImage == false);
                    if (bUsedOfflineImage) {
                        // LoadCurrentLoaderImage() 는 뷰어의 기존 CurrentImage 를 Dispose 한다.
                        // 이 지역 참조가 이후 dangling 참조가 될 수 없도록 자동 넘김 실행 직전에 끊는다.
                        img = null;
                        bool bHasNextImage = (_loadedImageIndex >= 0) && (_loadedImageIndex < _loadedImagePaths.Count - 1);
                        if (bHasNextImage) {
                            _loadedImageIndex = _loadedImageIndex + 1;
                            LoadCurrentLoaderImage();
                        }
                        else {
                            lbl_loaderStatus.Text = "마지막 이미지 도달";
                        }
                    }
                    UpdateCalButtonState(szStepDetail);
                }
                else {
                    lbl_calStatus.Text = "스텝 실패: " + error;
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "스텝 오류: " + ex.Message;
            }
            finally {
                bool bShouldDispose = bOwnsImage && (img != null);
                if (bShouldDispose) {
                    try { img.Dispose(); } catch { }
                }
            }
        }

        private void CalComputeButton_Click(object sender, RoutedEventArgs e) {
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            // PickerCenterCalibrationService.TryComputePickerCenter(수정 금지 대상)는 내부적으로
            //  SystemSetting.Handle.PickerCenterRow/Col(Bottom 값)에도 같은 결과를 대입한다 —
            //  Bottom/Tray 가 PickerCal 공용 인스턴스를 쓰기 때문(Task 4). Tray 화면에서 계산했다고
            //  Bottom 의 설정값이 (저장은 안 되더라도) 런타임에서 바뀌어 있는 상태로 남는 것은
            //  이 작업 범위 밖의 부수효과이므로, 호출 전후로 Bottom 값을 그대로 복원해 무효화한다.
            //  Tray 자신의 결과는 아래 out r/c 로 별도로 받아 TrayPickerCenterRow/Col 에 저장한다.
            double savedBottomPickerRow = SystemSetting.Handle.PickerCenterRow;
            double savedBottomPickerCol = SystemSetting.Handle.PickerCenterCol;

            try {
                double r, c, rad;
                double dRmsPx, dMaxPx;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryComputePickerCenter(
                    out r, out c, out rad, out dRmsPx, out dMaxPx, out error);

                if (bOk) {
                    // quick-260903-dpy — 잔차(RMS/최대)는 이 값을 믿어도 되는지 판단하는 유일한 근거라
                    //  중심좌표/반경과 함께 lbl_pickerCenter 에 항상 노출한다.
                    string szFitQuality = BuildFitQualityText(dRmsPx, dMaxPx);
                    lbl_pickerCenter.Text = BuildPickerCenterText(r, c, rad) + "  |  " + szFitQuality;
                    if (_viewer != null) {
                        HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                        _viewer.SetAlignContourXld(vizXld);
                    }
                    ETeachGrade fitGrade = TeachDiag.ClassifyScore(ToCircularityScore(dRmsPx, rad), FIT_SCORE_MIN);
                    string msg = string.Format(
                        "Tray 피커센터를 저장하시겠습니까?\n\nRow: {0:F2}  Col: {1:F2}  r: {2:F2}\n{3}", r, c, rad, szFitQuality);
                    MessageBoxResult dlgResult = MessageBox.Show(
                        msg, "피커센터 저장", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (dlgResult == MessageBoxResult.Yes) {
                        // Bottom 의 PickerCenterRow/Col 이 아니라 Tray 전용 값에 저장한다 — 안전장치(미캘=0)의 핵심.
                        SystemSetting.Handle.TrayPickerCenterRow = r;
                        SystemSetting.Handle.TrayPickerCenterCol = c;
                        SystemSetting.Handle.Save();
                        UpdateCalButtonState(TeachDiag.ToStatusLine(fitGrade, "피커센터 저장 완료 · " + szFitQuality));
                    }
                    else {
                        UpdateCalButtonState(TeachDiag.ToStatusLine(fitGrade, "저장 취소 (값은 런타임 유지, 재시작 시 초기화) · " + szFitQuality));
                    }
                }
                else {
                    UpdateCalButtonState("계산 실패: " + error);
                    lbl_pickerCenter.Text = "";
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "계산 오류: " + ex.Message;
            }
            finally {
                // Bottom 값 원복 — TryComputePickerCenter 의 부수효과를 여기서 무효화한다(위 주석 참고).
                SystemSetting.Handle.PickerCenterRow = savedBottomPickerRow;
                SystemSetting.Handle.PickerCenterCol = savedBottomPickerCol;
            }
        }

        // 반경 대비 잔차 비율을 0~1 점수로 뒤집어(원형도) ClassifyScore 에 통과시킨다.
        // 반경이 클수록 같은 절대 잔차가 덜 치명적이므로 비율이 맞는 척도다.
        private static double ToCircularityScore(double dRmsPx, double dRadiusPx) {
            if (dRadiusPx <= 0.0) {
                return 0.0;
            }
            double dRatio = dRmsPx / dRadiusPx;
            if (dRatio >= 1.0) {
                return 0.0;
            }
            return 1.0 - dRatio;
        }

        // 피커센터 계산결과에 화면(이미지) 중심 대비 실제 오프셋 거리(mm)를 붙여서 보여준다.
        //  판정/저장 로직은 손대지 않는다 — 표시 전용. _viewer.CurrentImage 가 없으면(오프라인 등)
        //  계산 불가하므로 기존 픽셀-only 문구로 조용히 폴백한다(throw 금지).
        private string BuildPickerCenterText(double r, double c, double rad) {
            const double UM_PER_MM = 1000.0; // µm/px → mm/px (AlignShapeMatchService.cs 와 동일 상수/변환)
            string pixelOnlyText = string.Format(
                "피커센터 ({0:F2},{1:F2}) r={2:F2}", r, c, rad);

            bool bNoImage = (_viewer == null) || (_viewer.CurrentImage == null);
            if (bNoImage) {
                return pixelOnlyText;
            }

            HTuple imageWidth;
            HTuple imageHeight;
            _viewer.CurrentImage.GetImageSize(out imageWidth, out imageHeight);
            double imgCenterCol = imageWidth.D / 2.0;
            double imgCenterRow = imageHeight.D / 2.0;

            double dRowPx = r - imgCenterRow; // 세로(수직) 오프셋
            double dColPx = c - imgCenterCol; // 가로(수평) 오프셋
            double totalPx = Math.Sqrt(dRowPx * dRowPx + dColPx * dColPx);

            double resMm = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;
            double totalMm = totalPx * resMm;
            double dRowMm = dRowPx * resMm;
            double dColMm = dColPx * resMm;

            return pixelOnlyText + string.Format(
                "  |  중심오프셋 {0:F3}mm (가로 {1:F3}mm, 세로 {2:F3}mm)",
                totalMm, dColMm, dRowMm);
        }

        // 편심원 피팅 잔차를 µm 주 단위(px 괄호 병기)로 표시.
        // EthernetPixelResolution 이 0 이하(미설정)면 0 나눗셈/무의미 값 방어로 px 만 담은 문구를 돌려준다.
        private string BuildFitQualityText(double dRmsPx, double dMaxPx) {
            double dPixelResolutionUmPerPx = SystemSetting.Handle.EthernetPixelResolution;
            bool bResolutionInvalid = (dPixelResolutionUmPerPx <= 0.0);
            if (bResolutionInvalid) {
                return string.Format("피팅잔차 RMS {0:F2}px · 최대 {1:F2}px", dRmsPx, dMaxPx);
            }

            double dRmsUm = dRmsPx * dPixelResolutionUmPerPx;
            double dMaxUm = dMaxPx * dPixelResolutionUmPerPx;
            return string.Format("피팅잔차 RMS {0:F1}µm({1:F2}px) · 최대 {2:F1}µm({3:F2}px)",
                dRmsUm, dRmsPx, dMaxUm, dMaxPx);
        }

        // quick-260903-dpy — 피커센터 캘 버튼(②③④ + 마지막 취소) 활성/비활성을 한 곳에서 일괄
        //  갱신한다. 잘못된 순서를 에러 메시지로 사후 통보하는 대신 애초에 못 누르게 막는 것이 목적.
        //  szDetail 이 있으면 방금 수행한 동작의 결과 문구 뒤에 현재 진행 단계 배너를 이어 붙인다.
        private void UpdateCalButtonState() {
            UpdateCalButtonState(null);
        }

        private void UpdateCalButtonState(string szDetail) {
            PickerCenterCalibrationService pickerCal = EthernetVisionHandler.Handle.PickerCal;

            bool bHasModel = false;
            int nStepCount = 0;
            int nMinSteps = 1;
            if (pickerCal != null) {
                bHasModel  = pickerCal.HasModel;
                nStepCount = pickerCal.StepCount;
                nMinSteps  = pickerCal.MinSteps;
            }

            bool bCanTeach      = _calRoiSet;
            bool bCanAddStep    = _calRoiSet && bHasModel;
            bool bCanRemoveLast = (nStepCount > 0);
            bool bCanCompute    = bHasModel && (nStepCount >= nMinSteps);

            if (btn_calTeachModel != null) {
                btn_calTeachModel.IsEnabled = bCanTeach;
            }
            if (btn_calAddStep != null) {
                btn_calAddStep.IsEnabled = bCanAddStep;
            }
            if (btn_calRemoveLastStep != null) {
                btn_calRemoveLastStep.IsEnabled = bCanRemoveLast;
            }
            if (btn_calCompute != null) {
                btn_calCompute.IsEnabled = bCanCompute;
            }

            if (lbl_calStatus == null) {
                return;
            }

            ETeachGrade stageGrade;
            string szStage = BuildCalStageStatus(bHasModel, nStepCount, nMinSteps, out stageGrade);

            bool bHasDetail = !string.IsNullOrEmpty(szDetail);
            if (bHasDetail) {
                lbl_calStatus.Text = szDetail + " · " + szStage;
            }
            else {
                lbl_calStatus.Text = szStage;
            }
            lbl_calStatus.Foreground = TeachDiag.GradeBrush(stageGrade);
        }

        // 현재 캘 진행 단계를 번호 배너로 만든다. stageGrade 는 lbl_calStatus 색 구분용 —
        //  아직 준비 안 됨(①②③)은 Weak(주황), 계산 가능(④, 최소 스텝 충족)은 Good(초록).
        //  이 등급은 표시 전용이며 검사 판정(P/F)과 무관하다(TeachDiagnostics 의 기존 계약과 동일).
        private string BuildCalStageStatus(bool bHasModel, int nStepCount, int nMinSteps, out ETeachGrade stageGrade) {
            if (!_calRoiSet) {
                stageGrade = ETeachGrade.Weak;
                return "① 검색 ROI 지정 필요";
            }
            if (!bHasModel) {
                stageGrade = ETeachGrade.Weak;
                return "② 모델 티칭 필요 (ROI 지정됨)";
            }
            bool bEnoughSteps = (nStepCount >= nMinSteps);
            if (!bEnoughSteps) {
                stageGrade = ETeachGrade.Weak;
                return "③ 스텝 추가 — 누적 " + nStepCount + " / 최소 " + nMinSteps;
            }
            stageGrade = ETeachGrade.Good;
            return "④ 계산 가능 — 누적 " + nStepCount + " / 최소 " + nMinSteps;
        }

        // ─── private 헬퍼 ────────────────────────────────────────────────────────

        /// <summary>
        /// RefreshStatus: IsInitialized 기반으로 초기 상태 라벨과 티칭 상태 라벨을 갱신.
        /// 생성자 Loaded 이벤트에서 1회 호출.
        /// </summary>
        private void RefreshStatus() {
            if (!EthernetVisionHandler.Handle.IsInitialized) {
                lbl_status.Text = "미연결";
            }
            else {
                lbl_status.Text = "대기";
            }

            bool bHasTemplate = false;
            try {
                bHasTemplate = EthernetVisionHandler.Handle.Matcher.HasTemplate(VIEW_MODE);
            }
            catch {
                // Matcher 초기화 전 예외 무시
            }

            if (bHasTemplate) {
                lbl_teachStatus.Text = TeachDiag.ToStatusLine(ETeachGrade.Good, "티칭 OK (HasTemplate=True)");
                lbl_teachStatus.Foreground = TeachDiag.GradeBrush(ETeachGrade.Good);
            }
            else {
                lbl_teachStatus.Text = TeachDiag.ToStatusLine(ETeachGrade.Weak, "티칭 없음");
                lbl_teachStatus.Foreground = TeachDiag.GradeBrush(ETeachGrade.Weak);
            }
        }

        /// <summary>
        /// ROI 2개가 모두 유효한지 검증.
        /// 유효하면 null 반환, 미흡 시 경고 문자열 반환.
        /// </summary>
        private string ValidateRois() {
            if (_roi1 == null) {
                return "ROI 1 미설정 — ROI 1 그리기 먼저";
            }
            if (_roi2 == null) {
                return "ROI 2 미설정 — ROI 2 그리기 먼저";
            }

            double halfW1 = (_roi1.Column2 - _roi1.Column1) / 2.0;
            double halfH1 = (_roi1.Row2 - _roi1.Row1) / 2.0;
            if (halfW1 < MIN_ROI_HALF_LENGTH || halfH1 < MIN_ROI_HALF_LENGTH) {
                return "ROI 1 이 너무 작습니다 — 다시 그리기";
            }

            double halfW2 = (_roi2.Column2 - _roi2.Column1) / 2.0;
            double halfH2 = (_roi2.Row2 - _roi2.Row1) / 2.0;
            if (halfW2 < MIN_ROI_HALF_LENGTH || halfH2 < MIN_ROI_HALF_LENGTH) {
                return "ROI 2 가 너무 작습니다 — 다시 그리기";
            }

            return null;
        }

        /// <summary>
        /// Rect ROI → HALCON gen_rectangle2 파라미터 변환.
        /// Length1 = Column 반폭(hwidth), Length2 = Row 반폭(hheight) 규약.
        /// </summary>
        private void RectToTeachParams(
            RoiDefinition roi,
            out double row, out double col, out double phi,
            out double len1, out double len2) {

            row  = (roi.Row1 + roi.Row2) / 2.0;
            col  = (roi.Column1 + roi.Column2) / 2.0;
            phi  = 0.0;
            len1 = (roi.Column2 - roi.Column1) / 2.0;  // Column 반폭
            len2 = (roi.Row2 - roi.Row1) / 2.0;        // Row 반폭
        }

        /// <summary>
        /// AlignResult → 결과 문자열 포맷 (Tray: X/Y Offset + Score, Theta 미표시).
        /// </summary>
        private string FormatAlignResult(AlignResult res) {
            return string.Format(
                "X: {0:F3} mm\nY: {1:F3} mm\nScore: {2:F3}",
                res.OffsetXmm,
                res.OffsetYmm,
                res.Score);
        }

        // ─── 오프라인 이미지 로더 핸들러 ─────────────────────────────────────────

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e) {
            //260625 hbk Phase 61.1 폴더 열기 → 이미지 목록 로드 → 인덱스 0 표시
            try {
                var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
                dlg.Multiselect = false;
                if (!string.IsNullOrEmpty(_lastImageFolder)) {
                    dlg.SelectedPath = _lastImageFolder;
                }

                if (dlg.ShowDialog() != true) {
                    return;
                }

                string folder = dlg.SelectedPath;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) {
                    lbl_loaderStatus.Text = "폴더 없음";
                    return;
                }

                _lastImageFolder = folder;

                var exts = new HashSet<string>(
                    LOADER_IMAGE_EXTS.Split(';'),
                    StringComparer.OrdinalIgnoreCase);

                _loadedImagePaths = Directory.GetFiles(folder)
                    .Where(f => exts.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (_loadedImagePaths.Count == 0) {
                    _loadedImageIndex = -1;
                    lbl_loaderStatus.Text = "이미지 없음 (bmp/png/jpg/tif)";
                    return;
                }

                _loadedImageIndex = 0;
                LoadCurrentLoaderImage();
            }
            catch (Exception ex) {
                lbl_loaderStatus.Text = "폴더 오류: " + ex.Message;
            }
        }

        private void PrevImageButton_Click(object sender, RoutedEventArgs e) {
            //260625 hbk Phase 61.1 이전 이미지로 인덱스 이동
            if (_loadedImagePaths.Count == 0) {
                lbl_loaderStatus.Text = "폴더 먼저 열기";
                return;
            }

            if (_loadedImageIndex > 0) {
                _loadedImageIndex = _loadedImageIndex - 1;
                LoadCurrentLoaderImage();
            }
            else {
                lbl_loaderStatus.Text = "첫 이미지";
            }
        }

        private void NextImageButton_Click(object sender, RoutedEventArgs e) {
            //260625 hbk Phase 61.1 다음 이미지로 인덱스 이동
            if (_loadedImagePaths.Count == 0) {
                lbl_loaderStatus.Text = "폴더 먼저 열기";
                return;
            }

            if (_loadedImageIndex < _loadedImagePaths.Count - 1) {
                _loadedImageIndex = _loadedImageIndex + 1;
                LoadCurrentLoaderImage();
            }
            else {
                lbl_loaderStatus.Text = "마지막 이미지";
            }
        }

        /// <summary>
        /// 현재 인덱스 이미지를 뷰어에 로드하고 상태 라벨을 갱신한다.
        /// _viewer.LoadImage(path) 호출 → CurrentImage 갱신 → 기존 Teach/Run 핸들러 자동 사용.
        /// 파일 I/O 실패 시 throw 없이 lbl_loaderStatus 갱신만 (T-61.1-03 완화).
        /// </summary>
        private void LoadCurrentLoaderImage() {
            if (_viewer == null) {
                lbl_loaderStatus.Text = "뷰어 미연결";
                return;
            }

            if (_loadedImageIndex < 0 || _loadedImageIndex >= _loadedImagePaths.Count) {
                return;
            }

            string path = _loadedImagePaths[_loadedImageIndex];
            try {
                _viewer.LoadImage(path);
            }
            catch (Exception ex) {
                lbl_loaderStatus.Text = "로드 오류: " + ex.Message;
                return;
            }

            lbl_loaderStatus.Text = string.Format(
                "{0}/{1}  {2}",
                _loadedImageIndex + 1,
                _loadedImagePaths.Count,
                Path.GetFileName(path));

            lbl_status.Text = "대기";
        }

        /// <summary>
        /// 화면에 보이는 현재 영상(_viewer.CurrentImage)을 bmp 로 저장한다.
        /// _viewer.CurrentImage 는 뷰어 소유이므로 여기서 Dispose 하지 않는다.
        /// </summary>
        private void SaveImageButton_Click(object sender, RoutedEventArgs e) {
            bool bViewerMissing = (_viewer == null);
            if (bViewerMissing) {
                lbl_loaderStatus.Text = "저장할 이미지가 없습니다 — Grab 또는 [폴더 열기] 로 영상을 먼저 띄우세요";
                return;
            }

            bool bNoCurrentImage = (_viewer.CurrentImage == null);
            if (bNoCurrentImage) {
                lbl_loaderStatus.Text = "저장할 이미지가 없습니다 — Grab 또는 [폴더 열기] 로 영상을 먼저 띄우세요";
                return;
            }

            string szInitialDir = null;
            if (!string.IsNullOrEmpty(_lastSaveFolder)) {
                szInitialDir = _lastSaveFolder;
            }
            else if (!string.IsNullOrEmpty(SystemSetting.Handle.ImageSavePath)) {
                szInitialDir = Path.Combine(SystemSetting.Handle.ImageSavePath, SAVE_IMAGE_SUBFOLDER);
            }

            if (!string.IsNullOrEmpty(szInitialDir) && !Directory.Exists(szInitialDir)) {
                try {
                    Directory.CreateDirectory(szInitialDir);
                }
                catch (Exception) {
                    szInitialDir = null;   // 생성 실패 시 다이얼로그 기본 위치에 맡긴다
                }
            }

            var dlg = new Ookii.Dialogs.Wpf.VistaSaveFileDialog();
            dlg.Filter = "BMP 이미지|*.bmp";
            dlg.DefaultExt = "bmp";
            dlg.AddExtension = true;
            dlg.OverwritePrompt = true;
            if (!string.IsNullOrEmpty(szInitialDir)) {
                dlg.InitialDirectory = szInitialDir;
            }

            dlg.FileName = SAVE_IMAGE_PREFIX + "_" + DateTime.Now.ToString(SAVE_IMAGE_TIMESTAMP_FORMAT) + SAVE_IMAGE_EXTENSION;

            bool? bResult = dlg.ShowDialog();
            if (bResult == true) {
                string szPath = dlg.FileName;
                try {
                    string szDir = Path.GetDirectoryName(szPath);
                    if (!string.IsNullOrEmpty(szDir) && !Directory.Exists(szDir)) {
                        Directory.CreateDirectory(szDir);
                    }

                    _viewer.CurrentImage.WriteImage(SAVE_IMAGE_FORMAT, 0, szPath);
                    _lastSaveFolder = Path.GetDirectoryName(szPath);
                    lbl_loaderStatus.Text = "저장 완료: " + Path.GetFileName(szPath);
                }
                catch (Exception ex) {
                    lbl_loaderStatus.Text = "저장 실패: " + ex.Message;
                }
            }
        }
    }
}
