//260624 hbk Phase 61: BottomVisionView 코드비하인드 — Bottom 비전 thin facade (AV-08)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HalconDotNet;
using ReringProject.Device;   //260626 hbk Phase 66 — LightHandler 참조(동축 ON/OFF+Level)
using ReringProject.Halcon.Models;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;

namespace ReringProject.Custom.UI {

    /// <summary>
    /// Bottom 비전 뷰 코드비하인드. Phase 58/59/60 서비스(EthernetVisionHandler.Camera/Matcher/PickerCal)에 위임하는
    /// thin facade. Tray 와 동일 Grab/Live/Stop + 2-ROI Teach + Run 에 Bottom 전용 추가:
    /// (1) 검사 결과 ThetaDeg 표시(HasTheta=true), (2) 피커센터 캘 패널(PickerCal.Reset/TryAddStep/TryComputePickerCenter).
    /// HALCON 뷰어를 소유하지 않고 외부 주입 공유 MainResultViewerControl 을 사용 (D-03).
    /// 전 서비스 호출 try-catch — 예외 시 상태 라벨 갱신만, throw 금지 (D-05).
    /// </summary>
    public partial class BottomVisionView : UserControl {

        // Bottom 전용 모드 상수 (이 뷰는 항상 Bottom 모드로 서비스 호출)
        private const EEthernetVisionMode VIEW_MODE = EEthernetVisionMode.Bottom;

        // 최소 ROI 크기 임계 (px) — 너무 작은 ROI 는 티칭 불가
        private const double MIN_ROI_HALF_LENGTH = 1.0;

        //260625 hbk Phase 61.1 오프라인 이미지 로더 상태
        private const string LOADER_IMAGE_EXTS = ".bmp;.png;.jpg;.jpeg;.tif;.tiff";  // 지원 확장자
        private List<string> _loadedImagePaths = new List<string>();
        private int _loadedImageIndex = -1;   // -1 = 미로드
        private static string _lastImageFolder = null;   // 폴더 마지막 위치 기억 (static — 탭 전환에도 유지)

        // D-03: 외부 주입 공유 뷰어 (소유하지 않음 — MainWindow 가 관리)
        private MainResultViewerControl _viewer;

        // 2-ROI 티칭 슬롯: DrawRoi1→DrawRoi2 순서로 슬롯 채움
        private RoiDefinition _roi1;
        private RoiDefinition _roi2;

        // 현재 ROI 드로잉 진행 중인 슬롯 인덱스 (1 또는 2, 0=미진행)
        // 주의: _drawingSlot 은 ROI 드로잉 순서(1/2), 면 슬롯(_selectedSlot) 과 다른 개념
        private int _drawingSlot;

        //260626 hbk Phase 65 Plan 02 — 6슬롯 면별 Align UI 필드 (D-01)
        // _selectedSlot: 현재 선택된 면 슬롯 (None = 미선택)
        private EBottomAlignSlot _selectedSlot = EBottomAlignSlot.None;
        // _slotRois: 슬롯별 확정된 ROI 쌍 보관. [0]=roi1, [1]=roi2. 슬롯 전환 시 복원에 사용.
        private Dictionary<EBottomAlignSlot, RoiDefinition[]> _slotRois = new Dictionary<EBottomAlignSlot, RoiDefinition[]>();

        // 캘 검색 ROI 슬롯 (Circle 드로잉으로 수거)
        private double _calSearchRow;
        private double _calSearchCol;
        private double _calSearchRadius;
        private bool _calRoiSet;

        //260626 hbk WR-02: 동축 UI 로드 중 이벤트 연쇄 저장 차단 플래그. true 이면 CoaxSlider_ValueChanged/CoaxCheckBox_Changed 즉시 return.
        private bool _isLoadingCoax = false;

        public BottomVisionView() {
            InitializeComponent();
            Loaded += BottomVisionView_Loaded;
        }

        // ─── 공유 뷰어 계약 (Plan 61-03 이 소비) ────────────────────────────────

        /// <summary>
        /// 외부(MainWindow)가 공유 MainResultViewerControl 을 주입한다.
        /// ViewerHostBorder.Child 로 배치하여 airspace-safe 우측 컬럼에 표시.
        /// viewer 가 이전 부모에 부착되어 있을 경우 detach 는 MainWindow 책임.
        /// CircleDrawingCompleted 이벤트도 여기서 구독 (중복 구독 방지: -= 후 +=).
        /// </summary>
        public void AttachSharedViewer(MainResultViewerControl viewer) {
            //260624 hbk Phase 61 — D-03 공유 뷰어 주입
            if (viewer == null) {
                return;
            }
            _viewer = viewer;
            ViewerHostBorder.Child = viewer;

            // CircleDrawingCompleted 구독 (중복 방지: -= 후 +=)
            _viewer.CircleDrawingCompleted -= OnCalCircleDrawn;
            _viewer.CircleDrawingCompleted += OnCalCircleDrawn;
        }

        // ─── 라이프사이클 ─────────────────────────────────────────────────────────

        private void BottomVisionView_Loaded(object sender, RoutedEventArgs e) {
            PopulateSlotComboBox(); //260626 hbk Phase 65 Plan 02 — 6슬롯 항목 채우기 (Loaded 시점)
            RefreshStatus();
        }

        /// <summary>
        /// 면 슬롯 ComboBox 에 6개 슬롯을 그룹 라벨과 함께 채운다.
        /// 3D 그룹 2개 먼저, 2D 그룹 4개 순서. Tag 에 EBottomAlignSlot enum 보관.
        /// 초기 선택 없음 — 작업자가 명시적으로 선택해야 티칭 가능.
        /// </summary>
        private void PopulateSlotComboBox() //260626 hbk Phase 65 Plan 02 — ComboBox 6슬롯 채우기 (D-01)
        {
            cmb_slot.Items.Clear();

            ComboBoxItem item3DTop = new ComboBoxItem();
            item3DTop.Content = "[3D] 3D_Top"; //260626 hbk 3D 그룹 첫 번째 슬롯
            item3DTop.Tag = EBottomAlignSlot.Slot3DTop;
            cmb_slot.Items.Add(item3DTop);

            ComboBoxItem item3DBottom = new ComboBoxItem();
            item3DBottom.Content = "[3D] 3D_Bottom"; //260626 hbk 3D 그룹 두 번째 슬롯
            item3DBottom.Tag = EBottomAlignSlot.Slot3DBottom;
            cmb_slot.Items.Add(item3DBottom);

            ComboBoxItem item2DTop = new ComboBoxItem();
            item2DTop.Content = "[2D] 2D_TOP"; //260626 hbk 2D 그룹 첫 번째 슬롯
            item2DTop.Tag = EBottomAlignSlot.Slot2DTop;
            cmb_slot.Items.Add(item2DTop);

            ComboBoxItem item2DBottom = new ComboBoxItem();
            item2DBottom.Content = "[2D] 2D_BOTTOM"; //260626 hbk 2D 그룹 두 번째 슬롯
            item2DBottom.Tag = EBottomAlignSlot.Slot2DBottom;
            cmb_slot.Items.Add(item2DBottom);

            ComboBoxItem item2DSide1 = new ComboBoxItem();
            item2DSide1.Content = "[2D] 2D_SIDE_1"; //260626 hbk 2D 그룹 세 번째 슬롯
            item2DSide1.Tag = EBottomAlignSlot.Slot2DSide1;
            cmb_slot.Items.Add(item2DSide1);

            ComboBoxItem item2DSide2 = new ComboBoxItem();
            item2DSide2.Content = "[2D] 2D_SIDE_2"; //260626 hbk 2D 그룹 네 번째 슬롯
            item2DSide2.Tag = EBottomAlignSlot.Slot2DSide2;
            cmb_slot.Items.Add(item2DSide2);

            // 초기 선택 없음 — 작업자가 명시 선택해야 티칭 가능 (T-65-04 가드 대비)
            cmb_slot.SelectedIndex = -1;
        }

        /// <summary>
        /// 면 슬롯 ComboBox 선택 변경 핸들러.
        /// _selectedSlot 갱신 → 슬롯별 ROI 복원 → RefreshStatus 호출.
        /// </summary>
        private void SlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) //260626 hbk Phase 65 Plan 02 — 슬롯 전환 핸들러
        {
            try {
                ComboBoxItem selectedItem = cmb_slot.SelectedItem as ComboBoxItem;
                if (selectedItem == null) {
                    _selectedSlot = EBottomAlignSlot.None; //260626 hbk 선택 없음 → None
                    lbl_slotStatus.Text = "슬롯 선택 필요";
                    return;
                }

                if (!(selectedItem.Tag is EBottomAlignSlot)) {
                    _selectedSlot = EBottomAlignSlot.None; //260626 hbk Tag 타입 불일치 방어
                    lbl_slotStatus.Text = "슬롯 선택 필요";
                    return;
                }

                EBottomAlignSlot newSlot = (EBottomAlignSlot)selectedItem.Tag;
                _selectedSlot = newSlot; //260626 hbk 현재 선택 슬롯 갱신

                // 슬롯별 저장된 ROI 복원 (없으면 null — 새 티칭 대기)
                if (_slotRois.ContainsKey(_selectedSlot)) {
                    RoiDefinition[] savedRois = _slotRois[_selectedSlot];
                    if (savedRois != null && savedRois.Length >= 2) { //260626 hbk WR-03: 배열 길이 가드 — OOB 방어
                        _roi1 = savedRois[0]; //260626 hbk 이 슬롯의 저장 ROI1 복원
                        _roi2 = savedRois[1]; //260626 hbk 이 슬롯의 저장 ROI2 복원
                    }
                    else {
                        _roi1 = null; //260626 hbk 배열 불완전 — 안전하게 null 처리 (WR-03)
                        _roi2 = null;
                    }
                }
                else {
                    _roi1 = null; //260626 hbk 저장된 ROI 없음 → 새 티칭 대기
                    _roi2 = null;
                }

                _drawingSlot = 0; //260626 hbk ROI 드로잉 슬롯 리셋 (슬롯 전환 시 드로잉 취소)

                string displayLabel = EBottomAlignSlotMap.ToDisplayLabel(newSlot);
                lbl_slotStatus.Text = "선택: " + displayLabel; //260626 hbk 선택 슬롯 라벨 표시

                LoadSlotCoaxToUi(); //260626 hbk Phase 66 — 슬롯 동축값 복원(슬롯 전환 시 JSON에서 CoaxEnabled/CoaxLevel 복원)
                RefreshStatus(); //260626 hbk 슬롯별 HasTemplate 상태 갱신
            }
            catch (Exception ex) {
                lbl_slotStatus.Text = "슬롯 전환 오류: " + ex.Message;
            }
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
                    lbl_status.Text = "LIVE";
                }
                else {
                    lbl_status.Text = "미연결";
                }
            }
            catch (Exception ex) {
                lbl_status.Text = "Live 오류: " + ex.Message;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e) {
            if (EthernetVisionHandler.Handle.Camera == null) {
                lbl_status.Text = "미연결";
                return;
            }

            try {
                EthernetVisionHandler.Handle.Camera.Stop();
                lbl_status.Text = "대기";
            }
            catch (Exception ex) {
                lbl_status.Text = "Stop 오류: " + ex.Message;
            }
        }

        // ─── 티칭 핸들러 ─────────────────────────────────────────────────────────

        private void DrawRoi1Button_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — ROI 1 그리기 시작: 직전 슬롯1 내용 초기화 후 StartRectangleDrawing
            if (_viewer == null) {
                lbl_status.Text = "뷰어 미연결";
                return;
            }

            _roi1 = null;
            _drawingSlot = 1;
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

            try {
                // 슬롯 1 진행 중이었으면 확정
                if (_drawingSlot == 1) {
                    _roi1 = _viewer.CommitActiveRectangle();
                }

                _roi2 = null;
                _drawingSlot = 2;
                _viewer.StartRectangleDrawing();
                lbl_status.Text = "ROI 2 드래그 후 티칭 저장을 클릭하세요";
            }
            catch (Exception ex) {
                lbl_status.Text = "ROI 2 그리기 오류: " + ex.Message;
            }
        }

        private void TeachButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 2-ROI 확정 + TryTeach 호출 (EEthernetVisionMode.Bottom)
            //260626 hbk Phase 65 Plan 02 — 슬롯 가드 + _selectedSlot 전달 (T-65-04, D-01)

            // T-65-04: 슬롯 미선택 시 조기 반환 — 의도치 않은 단일경로 덮어쓰기 방지
            if (_selectedSlot == EBottomAlignSlot.None) {
                lbl_teachStatus.Text = "면 슬롯을 먼저 선택하세요"; //260626 hbk 슬롯 미선택 가드
                return;
            }

            if (_viewer == null || _viewer.CurrentImage == null) {
                lbl_status.Text = "이미지 없음 — Grab 먼저";
                return;
            }

            try {
                // 슬롯 2 진행 중이었으면 확정
                if (_drawingSlot == 2) {
                    _roi2 = _viewer.CommitActiveRectangle();
                }

                // 두 ROI 모두 유효한지 검증
                string validErr = ValidateRois();
                if (validErr != null) {
                    lbl_teachStatus.Text = validErr;
                    return;
                }

                // ROI → TryTeach 파라미터 변환 (HALCON gen_rectangle2 규약)
                double r1, c1, phi1, l1_1, l1_2;
                RectToTeachParams(_roi1, out r1, out c1, out phi1, out l1_1, out l1_2);

                double r2, c2, phi2, l2_1, l2_2;
                RectToTeachParams(_roi2, out r2, out c2, out phi2, out l2_1, out l2_2);

                ApplyCoaxLight(); //260626 hbk Phase 66 — 티칭 직전 동축 자동 적용(D-07 티칭=런타임 조명 일치)
                string error;
                // 슬롯 오버로드 호출 — Plan 01 신규 오버로드(_selectedSlot 명시)
                bool bOk = EthernetVisionHandler.Handle.Matcher.TryTeach(
                    _viewer.CurrentImage,
                    r1, c1, phi1, l1_1, l1_2,
                    r2, c2, phi2, l2_1, l2_2,
                    VIEW_MODE, _selectedSlot, //260626 hbk 선택 슬롯 전달 (Plan 01 슬롯 오버로드)
                    out error);

                if (bOk) {
                    bool bHas = EthernetVisionHandler.Handle.Matcher.HasTemplate(VIEW_MODE, _selectedSlot); //260626 hbk 슬롯별 HasTemplate 확인
                    string slotLabel = EBottomAlignSlotMap.ToDisplayLabel(_selectedSlot);
                    lbl_teachStatus.Text = "[" + slotLabel + "] 티칭 OK (HasTemplate=" + bHas + ")"; //260626 hbk 슬롯 라벨 포함 메시지

                    // 티칭 성공 시 이 슬롯의 ROI 쌍을 영구 보관
                    _slotRois[_selectedSlot] = new RoiDefinition[] { _roi1, _roi2 }; //260626 hbk 슬롯별 ROI 보관 (슬롯 전환 후 복원용)
                }
                else {
                    lbl_teachStatus.Text = "티칭 실패: " + error;
                }
                _drawingSlot = 0;
            }
            catch (Exception ex) {
                lbl_teachStatus.Text = "티칭 예외: " + ex.Message;
            }
        }

        // ─── 검사 핸들러 ─────────────────────────────────────────────────────────

        private void RunButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — Matcher.Run 호출 → AlignResult X/Y + Theta(deg) + Score 표시 (Bottom: HasTheta=true)
            //260626 hbk Phase 65 Plan 02 — _selectedSlot 전달. None 이면 단일 경로 폴백(D-09)
            if (_viewer == null || _viewer.CurrentImage == null) {
                lbl_status.Text = "이미지 없음 — Grab 먼저";
                return;
            }

            try {
                lbl_status.Text = "검사중";
                ApplyCoaxLight(); //260626 hbk Phase 66 — 검사 직전 동축 자동 적용(D-07)
                // _selectedSlot None 이면 Plan 01 폴백(Bottom_1/2.shm 단일) 동작 — 회귀 0 보장
                AlignResult res = EthernetVisionHandler.Handle.Matcher.Run(_viewer.CurrentImage, VIEW_MODE, _selectedSlot); //260626 hbk 선택 슬롯 전달

                if (res.Found) {
                    lbl_result.Text = FormatAlignResult(res);
                    ApplyAlignVisualization(res);          //260625 hbk Phase 61.1 검출 시각화
                }
                else {
                    lbl_result.Text = "검출 실패";
                    ClearAlignVisualization();             //260625 hbk Phase 61.1 이전 오버레이 제거
                }
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
                _viewer.SetResultRoiOverlays(null, datumRects);
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

        // ─── 피커센터 캘 핸들러 ──────────────────────────────────────────────────

        private void OnCalCircleDrawn(object sender, CircleDrawCompletedArgs e) {
            //260624 hbk Phase 61 — CircleDrawingCompleted 이벤트 수거: 검색 ROI(원) 좌표 저장
            //260630 hbk Phase 60 — SystemSetting 에도 동시 저장 → TCP $ALIGN_CALIB STEP 공유
            _calSearchRow    = e.CenterRow;
            _calSearchCol    = e.CenterCol;
            _calSearchRadius = e.Radius;
            _calRoiSet       = true;
            SystemSetting.Handle.CalibSearchRow    = e.CenterRow;
            SystemSetting.Handle.CalibSearchCol    = e.CenterCol;
            SystemSetting.Handle.CalibSearchRadius = e.Radius;
            lbl_calStatus.Text = "검색 ROI 설정됨 (r=" + _calSearchRadius.ToString("F1") + ")";
        }

        private void CalResetButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 누적 초기화
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            try {
                EthernetVisionHandler.Handle.PickerCal.Reset();
                lbl_calStatus.Text = "누적 0";
                lbl_pickerCenter.Text = "";
                _calRoiSet = false;
                if (_viewer != null) {
                    _viewer.SetAlignContourXld(null); //260630 hbk — 오버레이 클리어
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "초기화 오류: " + ex.Message;
            }
        }

        private void CalDrawRoiButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 검색 ROI(원) 드로잉 시작. 좌표는 OnCalCircleDrawn 에서 수거.
            if (_viewer == null) {
                lbl_calStatus.Text = "뷰어 미연결";
                return;
            }

            try {
                _viewer.StartCircleDrawing();
                lbl_calStatus.Text = "검색 원 ROI 를 드래그하세요";
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "ROI 드로잉 오류: " + ex.Message;
            }
        }

        private void CalTeachModelButton_Click(object sender, RoutedEventArgs e) {
            //260630 hbk Phase 60 — Grab → ROI 내 ShapeModel 생성 → 저장 + 캐시 로드.
            if (!_calRoiSet) {
                lbl_calStatus.Text = "검색 ROI 미설정 — ROI(원) 지정 먼저";
                return;
            }
            if (EthernetVisionHandler.Handle.Camera == null) {
                lbl_calStatus.Text = "미연결";
                return;
            }
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            try {
                HImage img = EthernetVisionHandler.Handle.Camera.Grab();
                if (img == null) {
                    lbl_calStatus.Text = "Grab 실패";
                    return;
                }
                if (_viewer != null) {
                    _viewer.LoadImage(img);
                }

                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryTeachModel(
                    img, _calSearchRow, _calSearchCol, _calSearchRadius, out error);
                img.Dispose();

                if (bOk) {
                    lbl_calStatus.Text = "모델 티칭 완료";
                }
                else {
                    lbl_calStatus.Text = "모델 티칭 실패: " + error;
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "모델 티칭 오류: " + ex.Message;
            }
        }

        private void CalAddStepButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 한 스텝: Grab + find_shape_model → 중심 누적
            //260630 hbk Phase 60 — 시그니처 변경: out foundRow/foundCol + 시각화 XLD 갱신
            if (!_calRoiSet) {
                lbl_calStatus.Text = "검색 ROI 미설정 — ROI(원) 지정 먼저";
                return;
            }
            if (EthernetVisionHandler.Handle.Camera == null) {
                lbl_calStatus.Text = "미연결";
                return;
            }
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            try {
                HImage img = EthernetVisionHandler.Handle.Camera.Grab();
                if (img == null) {
                    lbl_calStatus.Text = "Grab 실패";
                    return;
                }
                if (_viewer != null) {
                    _viewer.LoadImage(img);
                }

                double foundRow, foundCol;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryAddStep(
                    img,
                    _calSearchRow, _calSearchCol, _calSearchRadius,
                    out foundRow, out foundCol, out error);
                img.Dispose();

                if (bOk) {
                    int stepCount = EthernetVisionHandler.Handle.PickerCal.StepCount;
                    lbl_calStatus.Text = "누적 " + stepCount + "  last=(" + foundRow.ToString("F1") + "," + foundCol.ToString("F1") + ")";
                    // 누적 십자 오버레이 갱신.
                    if (_viewer != null) {
                        HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                        _viewer.SetAlignContourXld(vizXld); // 소유권 이전
                    }
                }
                else {
                    lbl_calStatus.Text = "스텝 실패: " + error;
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "스텝 오류: " + ex.Message;
            }
        }

        private void CalComputeButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 누적 지그 중심 → 편심원 피팅 → 피커센터 산출 + 표시
            //260630 hbk Phase 60 — Compute 후 피팅원 + 중심 십자 오버레이 표시
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            try {
                double r, c, rad;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryComputePickerCenter(
                    out r, out c, out rad, out error);

                if (bOk) {
                    lbl_pickerCenter.Text = string.Format(
                        "피커센터 ({0:F2},{1:F2}) r={2:F2}", r, c, rad);
                    // 피팅 원 + 전 스텝 십자 + 중심 십자 오버레이 표시.
                    if (_viewer != null) {
                        HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                        _viewer.SetAlignContourXld(vizXld); // 소유권 이전
                    }
                }
                else {
                    lbl_calStatus.Text = "계산 실패: " + error;
                    lbl_pickerCenter.Text = "";
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "계산 오류: " + ex.Message;
            }
        }

        // ─── private 헬퍼 ────────────────────────────────────────────────────────

        /// <summary>
        /// RefreshStatus: IsInitialized 기반으로 초기 상태 라벨과 티칭/캘 상태 라벨을 갱신.
        /// 생성자 Loaded 이벤트에서 1회 호출.
        /// </summary>
        private void RefreshStatus() {
            if (!EthernetVisionHandler.Handle.IsInitialized) {
                lbl_status.Text = "미연결";
            }
            else {
                lbl_status.Text = "대기";
            }

            RefreshTeachStatus(); //260626 hbk Phase 65 Plan 02 — 슬롯별 티칭 상태 갱신 분리

            // 누적 스텝 수 표시 (PickerCal null 안전 처리)
            int stepCount = 0;
            if (EthernetVisionHandler.Handle.PickerCal != null) {
                stepCount = EthernetVisionHandler.Handle.PickerCal.StepCount;
            }
            lbl_calStatus.Text = "누적 " + stepCount;
        }

        /// <summary>
        /// 선택 슬롯의 HasTemplate 결과로 티칭 상태 라벨을 갱신한다.
        /// 슬롯 None 이면 단일 경로(Bottom) 상태 표시.
        /// 슬롯 선택 시 슬롯 라벨 포함 메시지로 표시.
        /// </summary>
        private void RefreshTeachStatus() //260626 hbk Phase 65 Plan 02 — 슬롯별 HasTemplate 라벨 갱신
        {
            bool bHasTemplate = false;
            try {
                bHasTemplate = EthernetVisionHandler.Handle.Matcher.HasTemplate(VIEW_MODE, _selectedSlot); //260626 hbk 슬롯 반영 HasTemplate
            }
            catch {
                // Matcher 초기화 전 예외 무시
            }

            if (_selectedSlot == EBottomAlignSlot.None) {
                // 슬롯 미선택: 단일 경로 상태 표시
                if (bHasTemplate) {
                    lbl_teachStatus.Text = "티칭 OK (단일 경로)"; //260626 hbk None=단일 경로 상태
                }
                else {
                    lbl_teachStatus.Text = "티칭 없음 (슬롯 선택 필요)"; //260626 hbk None=슬롯 선택 안내
                }
            }
            else {
                // 슬롯 선택: 슬롯 라벨 포함 상태 표시
                string slotLabel = EBottomAlignSlotMap.ToDisplayLabel(_selectedSlot);
                if (bHasTemplate) {
                    lbl_teachStatus.Text = "[" + slotLabel + "] 티칭 OK"; //260626 hbk 슬롯 라벨 포함 OK
                }
                else {
                    lbl_teachStatus.Text = "[" + slotLabel + "] 티칭 없음"; //260626 hbk 슬롯 라벨 포함 없음
                }
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
        /// AlignResult → 결과 문자열 포맷 (Bottom: X/Y Offset + Theta + Score).
        /// HasTheta=true 일 때 Theta 표시 (Bottom 은 항상 true).
        /// </summary>
        private string FormatAlignResult(AlignResult res) {
            if (res.HasTheta) {
                return string.Format(
                    "X: {0:F3} mm\nY: {1:F3} mm\nTheta: {2:F3} deg\nScore: {3:F3}",
                    res.OffsetXmm,
                    res.OffsetYmm,
                    res.ThetaDeg,
                    res.Score);
            }
            else {
                return string.Format(
                    "X: {0:F3} mm\nY: {1:F3} mm\nScore: {2:F3}",
                    res.OffsetXmm,
                    res.OffsetYmm,
                    res.Score);
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

        //260626 hbk Phase 66 D-07 — 동축 체크박스 변경: 즉시 조명 적용 + 슬롯 JSON 저장(수동 override)
        private void CoaxCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingCoax) return;   //260626 hbk WR-02: 로드 중 연쇄 저장 차단
            ApplyCoaxLight();       //260626 hbk 즉시 반영
            SaveSlotCoaxToJson();   //260626 hbk 슬롯 JSON 갱신
        }

        //260626 hbk Phase 66 D-07 — 동축 슬라이더 변경: 라벨 갱신 + 즉시 적용 + 슬롯 JSON 저장
        private void CoaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingCoax) return;   //260626 hbk WR-02: 로드 중 연쇄 저장 차단
            int nLevel = (int)e.NewValue;   //260626 hbk 새 밝기
            if (lbl_coaxLevel != null)
            {
                lbl_coaxLevel.Text = nLevel.ToString();   //260626 hbk 라벨 갱신(초기화 전 null 가드)
            }
            ApplyCoaxLight();
            SaveSlotCoaxToJson();
        }

        //260626 hbk Phase 66 D-05 — 현재 선택 슬롯 JSON 에 동축값 저장(TrySaveCoax = 티칭 데이터 보존 load-merge-save).
        //  슬롯 None(미선택)이면 저장 스킵 — 단일 경로 동축은 본 phase 범위 외(슬롯별 저장).
        private void SaveSlotCoaxToJson()
        {
            if (_selectedSlot == EBottomAlignSlot.None)
            {
                return;   //260626 hbk 슬롯 미선택 — 저장 대상 없음
            }
            try
            {
                bool bEnabled = (chk_coaxEnabled.IsChecked == true);   //260626 hbk 체크 상태
                int nLevel = (int)sld_coaxLevel.Value;                 //260626 hbk 밝기
                string error;
                bool bOk = EthernetVisionHandler.Handle.Matcher.TrySaveCoax(VIEW_MODE, _selectedSlot, bEnabled, nLevel, out error);   //260626 hbk 슬롯 JSON 동축 갱신
                if (!bOk)
                {
                    lbl_status.Text = "동축 저장 실패: " + error;   //260626 hbk 저장 실패 상태 표시
                }
            }
            catch (Exception ex)
            {
                lbl_status.Text = "동축 저장 오류: " + ex.Message;   //260626 hbk throw 금지
            }
        }

        //260626 hbk Phase 66 D-05 — 슬롯 JSON 의 동축값을 UI(chk/sld/lbl)에 복원. null/미티칭 → off/0.
        private void LoadSlotCoaxToUi()
        {
            _isLoadingCoax = true;   //260626 hbk WR-02: UI 값 설정 중 이벤트 연쇄 저장 차단 시작
            try
            {
                AlignRefPose refPose = null;   //260626 hbk 슬롯 동축값 로드 결과
                if (_selectedSlot != EBottomAlignSlot.None)
                {
                    refPose = EthernetVisionHandler.Handle.Matcher.GetSlotRefPose(VIEW_MODE, _selectedSlot);   //260626 hbk 슬롯 JSON 로드
                }
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
        /// _viewer.LoadImage(path) 호출 → CurrentImage 갱신 → 기존 Teach/Run/Cal 핸들러 자동 사용.
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
    }
}
