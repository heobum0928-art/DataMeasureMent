using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ReringProject.Export;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.UI
{
    /// <summary>
    /// 결과 리뷰어 비모달 Window. Ookii 폴더 다이얼로그로 날짜 폴더를 선택 → cycle 목록 표시 →
    /// cycle 선택 시 cycle.json 역직렬화 → 이미지 + overlay 재렌더 + 측정표 표시 (OUT-01 D-08/D-09).
    /// 라이브 MainView 방해 없는 비모달 별도 Window — ShowDialog 가 아닌 Show() 로 열림.
    /// T-40-08: null/손상 cycle.json → CycleResultSerializer.Load 가 null 반환 → DisplayCycle(null) → 빈 상태.
    /// T-40-09: ResultImagePath → File.Exists 가드 후 LoadImage 호출.
    /// </summary>
    public partial class ReviewerWindow : Window
    {
        private CycleResultDto _currentCycle;

        //260612 hbk Phase 41.1 OUT-03 반복도 실행 서비스 (UI 레이어 소유 인스턴스)
        private RepeatRunService _repeatService;
        private List<CycleResultDto> _repeatCycles;

        // DualImage 전환 버튼이 참조할 현재 선택 행
        private ReviewMeasurementRow _selectedRow;

        // '불량만 보기' 필터의 원본(전체) 행. 필터는 이 위에서 추려 ItemsSource 로 적용.
        private List<ReviewMeasurementRow> _allRows = new List<ReviewMeasurementRow>();

        // 좌측 cycle 목록의 원본(전체) 항목. '불량만 보기' 필터는 이 위에서 추려 ItemsSource 로 적용.
        private List<CycleListItem> _allCycleItems = new List<CycleListItem>();

        // Phase 78 NGA-01: 날짜 폴더 cycle.json 이력 — 추세 원인 규칙(R6) 입력.
        private NgCauseHistory _ngCauseHistory = new NgCauseHistory();

        // Phase 78 NGA-03: 리뷰어가 마지막으로 연 날짜 폴더 — NG 누적 엑셀 대상.
        private string _loadedDateFolder;

        // Phase 80 D-80-17: 버튼 활성·이유 계산은 전부 VM 이 한다 — 이 code-behind 는 배선만.
        private readonly ReviewerReinspectViewModel _reinspectVm = ReviewerReinspectViewModel.Instance;

        public ReviewerWindow()
        {
            InitializeComponent();
            panel_reinspect.DataContext = _reinspectVm;
            _reinspectVm.AlertPresenter = ShowReinspectAlert; // Phase 80 D-80-09/17: 알림 문구는 VM 이 준다
            _reinspectVm.EvaluateSelection(null, null); // 싱글턴 VM 재사용 — 창을 다시 열 때 이전 선택 상태가 남지 않게
        }

        // Phase 80 D-80-09: 기준점 사진 없음 알림 — 이 phase 의 유일한 새 대화상자(D-80-00).
        private void ShowReinspectAlert(string szTitle, string szMessage)
        {
            CustomMessageBox.Show(szTitle, szMessage, MessageBoxImage.Warning);
        }

        private void Button_LoadFolder_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg =
                new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.Multiselect = false;
            dlg.SelectedPath = SystemHandler.Handle.Setting.ResultSavePath;
            if ((bool)dlg.ShowDialog())
            {
                LoadCycleFolders(dlg.SelectedPath);
            }
        }

        // 날짜 폴더 스캔: cycle.json 존재하는 하위 폴더만 수집
        private void LoadCycleFolders(string dateFolderPath)
        {
            try
            {
                _loadedDateFolder = dateFolderPath;

                // Directory.Exists 가드 → 없는 폴더 → 빈 목록, 크래시 없음
                if (string.IsNullOrEmpty(dateFolderPath) || !Directory.Exists(dateFolderPath))
                {
                    _allCycleItems = new List<CycleListItem>();
                    _ngCauseHistory = new NgCauseHistory();
                    listBox_cycles.ItemsSource = null;
                    return;
                }

                NgCauseHistory history = new NgCauseHistory();
                var items = Directory.GetDirectories(dateFolderPath)
                    .Where(d => File.Exists(Path.Combine(d, "cycle.json")))
                    .OrderByDescending(d => d)  // 최신 순
                    .Select(d =>
                    {
                        var dto = CycleResultSerializer.Load(Path.Combine(d, "cycle.json"));
                        history.AddCycle(dto);
                        // 손상 cycle.json → Load 가 null 반환 → DisplayText 폴더명 폴백
                        string display = ReviewerListLabelBuilder.Build(dto);
                        if (string.IsNullOrEmpty(display))
                        {
                            display = Path.GetFileName(d);
                        }
                        bool bIsNg = ReviewerListLabelBuilder.IsFailTick(dto);
                        bool bIsIntermediate = ReviewerListLabelBuilder.IsIntermediateTick(dto);
                        return new CycleListItem { FolderPath = d, DisplayText = display, IsNg = bIsNg, IsIntermediate = bIsIntermediate };
                    })
                    .ToList();

                _allCycleItems = items;
                _ngCauseHistory = history;
                ApplyCycleListFilter();
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[Reviewer] LoadCycleFolders: " + ex.Message);
                }
                catch { }
            }
        }

        // '불량만 보기'·'중간 단계도 보기' 두 체크 상태로 좌측 목록을 거른다. ItemsSource 만 바꾸고
        // 선택 항목은 건드리지 않는다 — SelectionChanged 로 우측 표/이미지가 튀는 것을 방지한다.
        private void ApplyCycleListFilter()
        {
            bool bFailOnly = chk_failOnly.IsChecked == true;
            bool bShowIntermediate = chk_showIntermediate.IsChecked == true;
            List<CycleListItem> visible = _allCycleItems.Where(i => ReviewerListLabelBuilder.IsListItemVisible(i.IsIntermediate, i.IsNg, bShowIntermediate, bFailOnly)).ToList();
            listBox_cycles.ItemsSource = visible;
        }

        private void CycleList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var item = listBox_cycles.SelectedItem as CycleListItem;
            if (item == null)
            {
                return;
            }

            string jsonPath = Path.Combine(item.FolderPath, "cycle.json");
            // 손상/악성 JSON → null (TypeNameHandling.None in CycleResultSerializer)
            _currentCycle = CycleResultSerializer.Load(jsonPath);
            DisplayCycle(_currentCycle);
        }

        // cycle 결과 재렌더: 측정표 + 이미지 + overlay
        private void DisplayCycle(CycleResultDto cycle)
        {
            // 새 cycle 표시 시 DualImage 전환 버튼 숨김 (행 클릭 전까지 비노출)
            panel_dualToggle.Visibility = Visibility.Collapsed;
            _selectedRow = null;
            halconViewer.SetHighlightMeasurementName(null);   // 전체 보기 = 특정 측정 강조 해제
            _reinspectVm.EvaluateSelection(cycle, null); // Phase 80 D-80-02: 새 cycle 표시 시 버튼 상태 갱신

            if (cycle == null || cycle.Shots == null)
            {
                halconViewer.SetInspectionOverlays(new List<EdgeInspectionOverlay>());
                _allRows = new List<ReviewMeasurementRow>();
                dataGrid_measurements.ItemsSource = null;
                return;
            }

            // 측정표: 전 Shot/FAI/Measurement flatten → ReviewMeasurementRow
            var rows = new List<ReviewMeasurementRow>();
            foreach (var shot in cycle.Shots)
            {
                if (shot.FAIs == null)
                {
                    continue;
                }
                foreach (var fai in shot.FAIs)
                {
                    if (fai.Measurements == null)
                    {
                        continue;
                    }
                    foreach (var m in fai.Measurements)
                    {
                        rows.Add(new ReviewMeasurementRow(shot, fai, m, cycle, _ngCauseHistory));
                    }
                }
            }
            // 필터 원본 보관, ItemsSource 는 ApplyRowFilter 가 설정
            _allRows = rows;

            // Phase 80 함께 처리 1: 화면에 띄운 사진을 낸 Shot 의 선만 그린다 — 전 Shot 선을 한 사진에 겹쳐
            //  그리던 문제. 순서: LoadImage → SetInspectionOverlays
            ShotResultDto ownerShot;
            string szCycleImagePath = ReviewerImagePathResolver.ResolveCycleImagePath(cycle, out ownerShot); // Phase 78 NGA-06: 실제 촬영 원본 우선
            if (!string.IsNullOrEmpty(szCycleImagePath))
            {
                halconViewer.LoadImage(szCycleImagePath);
            }

            halconViewer.SetInspectionOverlays(ReviewerImagePathResolver.CollectShotOverlays(ownerShot));

            ApplyRowFilter();
        }

        // '불량만 보기' 체크 시 NG/DETECT FAIL 행만 표시 + 첫 불량 자동 포커스.
        // 자동 포커스는 ItemsSource 직후 동기 선택이 무시되는 문제로 Dispatcher.BeginInvoke(Background) 지연.
        private void ApplyRowFilter()
        {
            if (_allRows == null)
            {
                dataGrid_measurements.ItemsSource = null;
                return;
            }

            bool failOnly = chk_failOnly.IsChecked == true;
            List<ReviewMeasurementRow> visible;
            if (failOnly)
                visible = _allRows.Where(r => r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL" || r.JudgeText == "NO IMAGE" || r.JudgeText == ReviewMeasurementRow.JUDGE_MEASURE_FAIL).ToList(); //260616 hbk NO_IMAGE 불량 포함
            else
                visible = _allRows;
            dataGrid_measurements.ItemsSource = visible;

            // 첫 불량 행 자동 선택 → SelectionChanged 가 해당 FAI 이미지/overlay 로 포커스 (행 생성 후 지연 실행)
            var firstFail = visible.FirstOrDefault(r => r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL" || r.JudgeText == "NO IMAGE" || r.JudgeText == ReviewMeasurementRow.JUDGE_MEASURE_FAIL); //260616 hbk NO_IMAGE 불량 포함
            if (firstFail != null)
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        dataGrid_measurements.SelectedItem = firstFail;
                        dataGrid_measurements.ScrollIntoView(firstFail);
                    }),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void ChkFailOnly_Changed(object sender, RoutedEventArgs e)
        {
            ApplyCycleListFilter();
            ApplyRowFilter();
        }

        // '중간 단계도 보기' 체크 시 기준점·Z 범위 대기 tick 을 회색으로 좌측 목록에 되돌린다.
        private void ChkShowIntermediate_Changed(object sender, RoutedEventArgs e)
        {
            ApplyCycleListFilter();
        }

        // 측정 행 클릭 시 해당 측정이 속한 FAI 의 이미지 + overlay 만 표시 (decluttering).
        // cycle 선택 = 전체 보기 / 행 선택 = 단일 FAI 집중 보기.
        // DualImage 측정이면 가로축/세로축 전환 버튼 노출 (기본 가로축).
        private void MeasurementGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var row = dataGrid_measurements.SelectedItem as ReviewMeasurementRow;
            if (row == null)
            {
                return;
            }
            _selectedRow = row;
            _reinspectVm.EvaluateSelection(_currentCycle, row); // Phase 80 D-80-02: 행 선택 시 버튼 상태 갱신

            if (row.Source != null && row.Source.IsDualImage)
            {
                panel_dualToggle.Visibility = Visibility.Visible;
                ShowAxisImage(true); // 기본 가로축
            }
            else
            {
                panel_dualToggle.Visibility = Visibility.Collapsed;
                // 일반 측정: 해당 FAI 의 Shot 이미지 로드 (순서: LoadImage → SetInspectionOverlays)
                string imgPath = ReviewerImagePathResolver.ResolveRowImagePath(row.OwnerShot, row.OwnerFai); // Phase 78 NGA-06
                if (!string.IsNullOrEmpty(imgPath))
                {
                    halconViewer.LoadImage(imgPath);
                }
                ApplyFaiOverlays(row);
            }
        }

        // DualImage 가로축/세로축 이미지 전환. horizontal=true → 가로축, false → 세로축.
        private void ShowAxisImage(bool horizontal)
        {
            if (_selectedRow == null || _selectedRow.Source == null)
            {
                return;
            }

            string path;
            if (horizontal)
                path = _selectedRow.Source.HorizontalImagePath;
            else
                path = _selectedRow.Source.VerticalImagePath;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                halconViewer.LoadImage(path);
            }
            // overlay 는 FAI 단위로 동일 적용 (이미지별 overlay 귀속은 미저장 — 현재 데이터 모델 한계, 양 이미지 공통 표시)
            ApplyFaiOverlays(_selectedRow);
        }

        // 선택 FAI 의 overlay 만 표시 (REPLACE — SetInspectionOverlays = Clear + AddRange)
        private void ApplyFaiOverlays(ReviewMeasurementRow row)
        {
            List<EdgeInspectionOverlay> faiOverlays;
            if (row != null && row.OwnerFai != null && row.OwnerFai.LastOverlays != null)
                faiOverlays = row.OwnerFai.LastOverlays;
            else
                faiOverlays = new List<EdgeInspectionOverlay>();
            halconViewer.SetInspectionOverlays(faiOverlays);
            ApplyMeasurementNameLabel(row);
        }

        // 선택 행의 측정 이름을 화면 라벨로 표시(체크박스 ON 일 때만).
        //  한 FAI 에 측정이 여러 개(F9_P1/P2/P3)라 overlay 가 섞여 표시되는데, 그중 어느 선이
        //  방금 클릭한 항목인지 구분할 방법이 없어 추가했다.
        private void ApplyMeasurementNameLabel(ReviewMeasurementRow row)
        {
            // InitializeComponent 진행 중 방어: XAML 에서 체크박스(헤더, Row0)가 뷰어(Row1)보다 먼저 생성되는데,
            //  IsChecked="True" 초기값이 그 시점에 Checked 이벤트를 발화시켜 이 메서드가 불린다 —
            //  그때 halconViewer 필드는 아직 대입 전(null)이라 그대로 두면 창이 열릴 때마다 NullReference 가 난다.
            //  생성이 끝난 뒤 DisplayCycle/행 클릭 경로에서 정상적으로 다시 호출되므로 여기서는 조용히 빠져나간다.
            if (halconViewer == null || chk_showMeasName == null)
            {
                return;
            }
            bool bShowName = chk_showMeasName.IsChecked == true;
            if (!bShowName || row == null)
            {
                halconViewer.SetHighlightMeasurementName(null);
                return;
            }
            halconViewer.SetHighlightMeasurementName(row.MeasurementName);
        }

        private void ChkShowMeasName_Changed(object sender, RoutedEventArgs e)
        {
            ApplyMeasurementNameLabel(_selectedRow);
        }

        private void Button_AxisHorizontal_Click(object sender, RoutedEventArgs e)
        {
            ShowAxisImage(true);
        }

        private void Button_AxisVertical_Click(object sender, RoutedEventArgs e)
        {
            ShowAxisImage(false);
        }

        private void Button_ApplyRowToMain_Click(object sender, RoutedEventArgs e)
        {
            // Phase 80 D-80-01/03: 확인창 없이 바로 불러온다 — 로직은 VM·서비스
            _reinspectVm.ApplySelection(_currentCycle, _selectedRow);
        }

        //260615 hbk Quick 260615-dx7 이미지 폴더 반복 검사 버튼 핸들러 (고정 50회 → 폴더 N장 순회)
        // 지원 이미지 확장자
        private static readonly string[] RepeatImageExtensions =
            { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        private void Button_RepeatRun_Click(object sender, RoutedEventArgs e)
        {
            if (_repeatService != null && _repeatService.IsRunning)
            {
                _repeatService.Stop();
                lbl_repeatProgress.Text = "중단됨";
                btn_repeatRun.Content = "이미지 폴더 반복 검사";
                return;
            }

            // 이미지 폴더 선택 (날짜 폴더 열기와 동일한 Ookii 다이얼로그 패턴)
            var folderDlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            folderDlg.Multiselect = false;
            folderDlg.SelectedPath = SystemHandler.Handle.Setting.ResultSavePath;
            if (folderDlg.ShowDialog() != true)
            {
                return;
            }

            string folder = folderDlg.SelectedPath;
            List<string> imagePaths;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                CustomMessageBox.Show("반복 검사", "선택한 폴더가 존재하지 않습니다.", MessageBoxImage.Warning);
                return;
            }

            imagePaths = Directory.GetFiles(folder)
                .Where(f => RepeatImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (imagePaths.Count == 0)
            {
                CustomMessageBox.Show("반복 검사",
                    "선택한 폴더에 이미지(bmp/jpg/png/tif)가 없습니다.", MessageBoxImage.Warning);
                return;
            }

            //260818 Phase 72 D-05: 자재번호는 정수만 허용 — 시트명/파일명으로 흘러가는 자유 텍스트 차단.
            int nMaterialIndex = RepeatRunService.MATERIAL_NOT_SET;
            string szMaterial = txt_materialIndex.Text;
            if (!string.IsNullOrWhiteSpace(szMaterial))
            {
                if (!int.TryParse(szMaterial.Trim(), out nMaterialIndex))
                {
                    CustomMessageBox.Show("반복 검사", "자재번호는 숫자만 입력하세요.", MessageBoxImage.Warning);
                    return;
                }
            }

            InspectionSequence activeSeq = null;
            var seqHandler = SystemHandler.Handle.Sequences;
            if (seqHandler != null)
            {
                for (int i = 0; i < seqHandler.Count; i++)
                {
                    var s = seqHandler[i];
                    var inspSeq = s as InspectionSequence;
                    if (inspSeq != null)
                    {
                        activeSeq = inspSeq;
                        break;
                    }
                }
            }

            if (activeSeq == null)
            {
                CustomMessageBox.Show("반복 검사", "활성 검사 시퀀스를 찾을 수 없습니다.", MessageBoxImage.Warning);
                return;
            }

            //260818 Phase 72 D-05: 누적 체크 시 이전 실행 결과를 보존해야 자재 2종 열 분리를 검증할 수 있다.
            bool bAccumulate = chk_repeatAccumulate.IsChecked == true;
            if (!bAccumulate)
            {
                _repeatCycles = null;
            }

            List<CycleResultDto> prevCycles = _repeatCycles;

            btn_repeatExport.IsEnabled = false;
            btn_cpkReportExport.IsEnabled = false;
            btn_repeatRun.Content = "중단";
            lbl_repeatProgress.Text = "진행 중: 0/" + imagePaths.Count;

            _repeatService = new RepeatRunService();
            _repeatService.MaterialIndexNumber = nMaterialIndex;
            _repeatService.OnProgressChanged += (current, total) =>
            {
                Dispatcher.Invoke(() =>
                {
                    lbl_repeatProgress.Text = "진행 중: " + current + "/" + total;
                });
            };
            _repeatService.OnRepeatComplete += (cycles) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (bAccumulate && prevCycles != null && prevCycles.Count > 0)
                    {
                        var merged = new List<CycleResultDto>(prevCycles);
                        if (cycles != null)
                        {
                            merged.AddRange(cycles);
                        }

                        _repeatCycles = merged;
                    }
                    else
                    {
                        _repeatCycles = cycles;
                    }

                    int nTotal = 0;
                    if (_repeatCycles != null)
                    {
                        nTotal = _repeatCycles.Count;
                    }

                    lbl_repeatProgress.Text = "완료: " + nTotal + "회 (누적)";
                    btn_repeatRun.Content = "이미지 폴더 반복 검사";
                    btn_repeatExport.IsEnabled = nTotal > 0;
                    btn_cpkReportExport.IsEnabled = nTotal > 0;
                });
            };

            _repeatService.StartFromImages(activeSeq, imagePaths);
        }

        //260612 hbk Phase 41.1 OUT-03/OUT-04 반복도 엑셀 export 핸들러
        private void Button_RepeatExport_Click(object sender, RoutedEventArgs e)
        {
            if (_repeatCycles == null || _repeatCycles.Count == 0)
            {
                CustomMessageBox.Show("반복도 export", "반복 실행 완료 후 사용하세요.", MessageBoxImage.Warning);
                return;
            }

            string initialDir = SystemHandler.Handle.Setting.ResultSavePath;
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName ?? "";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = "repeat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx",
                InitialDirectory = initialDir
            };

            if (dlg.ShowDialog() == true)
            {
                bool ok = ReringProject.Export.RepeatExcelExportService.Export(
                    _repeatCycles, recipeName, dlg.FileName);
                string msg;
                if (ok)
                {
                    msg = "저장 완료:\n" + dlg.FileName;
                }
                else
                {
                    msg = "export 실패 (로그 확인)";
                }

                MessageBoxImage icon;
                if (ok)
                {
                    icon = MessageBoxImage.Information;
                }
                else
                {
                    icon = MessageBoxImage.Error;
                }

                CustomMessageBox.Show("반복도 엑셀 export", msg, icon);
            }
        }

        /// CPK 데이터 리포트(RAW DATA(1) + 1Cav 세부치수_Cpk) export.
        /// 버튼 클릭 핸들러 = UI/STA 스레드이므로 차트 Canvas 렌더가 안전하다.
        private void Button_CpkReportExport_Click(object sender, RoutedEventArgs e)
        {
            if (_repeatCycles == null || _repeatCycles.Count == 0)
            {
                CustomMessageBox.Show("CPK 리포트 export", "반복 실행 완료 후 사용하세요.", MessageBoxImage.Warning);
                return;
            }

            string initialDir = SystemHandler.Handle.Setting.ResultSavePath;
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName ?? "";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = "cpk_report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx",
                InitialDirectory = initialDir
            };

            if (dlg.ShowDialog() == true)
            {
                bool ok = ReringProject.Export.CpkReportExportService.ExportCpkReport(
                    _repeatCycles, recipeName, dlg.FileName);

                string msg;
                if (ok)
                {
                    msg = "저장 완료:\n" + dlg.FileName;
                }
                else
                {
                    msg = "export 실패 (로그 확인)";
                }

                MessageBoxImage icon;
                if (ok)
                {
                    icon = MessageBoxImage.Information;
                }
                else
                {
                    icon = MessageBoxImage.Error;
                }

                CustomMessageBox.Show("CPK 리포트 export", msg, icon);
            }
        }

        // Phase 78 NGA-03: 연 날짜 폴더의 NG 를 누적 엑셀에 추가 — 판단·문구는 서비스가 만든다
        private void Button_NgAccumExport_Click(object sender, RoutedEventArgs e)
        {
            string szOutputPath = NgAccumulationExportService.BuildOutputPath(SystemHandler.Handle.Setting.ResultSavePath);
            NgAccumExportOutcome outcome = NgAccumulationExportService.AppendDateFolder(_loadedDateFolder, szOutputPath);
            CustomMessageBox.Show(NgAccumulationExportService.MESSAGE_TITLE, outcome.Message, outcome.Icon);
        }

        // Align 정합 조회 창 열기. 이 화면의 자재번호 입력란 값을 초기값으로 복사해 넘긴다
        //  (txt_materialIndex 는 "이미지 폴더 반복 검사" 입력란이라 용도는 그대로 둔다).
        private void Button_AlignVerify_Click(object sender, RoutedEventArgs e)
        {
            AlignVerifyWindow win = new AlignVerifyWindow();
            win.Owner = this;
            if (txt_materialIndex != null)
            {
                win.SetInitialMaterial(txt_materialIndex.Text);
            }
            win.Show();
        }
    }

    /// <summary>ListBox 각 항목 — FolderPath(역직렬화 시 경로), DisplayText(시각·종합판정).</summary>
    public class CycleListItem
    {
        public string FolderPath { get; set; }

        public string DisplayText { get; set; }

        /// <summary>이 tick 이 불량(NG)인지 — 목록 글자색/'불량만 보기' 필터용. ReviewerListLabelBuilder.IsFailTick 로 계산.</summary>
        public bool IsNg { get; set; }

        /// <summary>중간 단계(기준점·Z 범위 대기) tick 인지 — 목록 기본 숨김·회색 표시용. ReviewerListLabelBuilder.IsIntermediateTick 로 계산.</summary>
        public bool IsIntermediate { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
