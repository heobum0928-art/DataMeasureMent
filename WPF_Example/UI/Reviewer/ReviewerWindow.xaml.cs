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

        // DualImage 전환 버튼이 참조할 현재 선택 행
        private ReviewMeasurementRow _selectedRow;

        // '불량만 보기' 필터의 원본(전체) 행. 필터는 이 위에서 추려 ItemsSource 로 적용.
        private List<ReviewMeasurementRow> _allRows = new List<ReviewMeasurementRow>();

        public ReviewerWindow()
        {
            InitializeComponent();
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
                // Directory.Exists 가드 → 없는 폴더 → 빈 목록, 크래시 없음
                if (string.IsNullOrEmpty(dateFolderPath) || !Directory.Exists(dateFolderPath))
                {
                    listBox_cycles.ItemsSource = null;
                    return;
                }

                var items = Directory.GetDirectories(dateFolderPath)
                    .Where(d => File.Exists(Path.Combine(d, "cycle.json")))
                    .OrderByDescending(d => d)  // 최신 순
                    .Select(d =>
                    {
                        var dto = CycleResultSerializer.Load(Path.Combine(d, "cycle.json"));
                        // 손상 cycle.json → Load 가 null 반환 → DisplayText 폴더명 폴백
                        string display;
                        if (dto != null)
                            display = dto.InspectionTime.ToString("HH:mm:ss") + "  " + dto.OverallJudgement;
                        else
                            display = Path.GetFileName(d);
                        return new CycleListItem { FolderPath = d, DisplayText = display };
                    })
                    .ToList();

                listBox_cycles.ItemsSource = items;
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

            btn_exportExcel.IsEnabled = (_currentCycle != null);
        }

        // cycle 결과 재렌더: 측정표 + 이미지 + overlay
        private void DisplayCycle(CycleResultDto cycle)
        {
            // 새 cycle 표시 시 DualImage 전환 버튼 숨김 (행 클릭 전까지 비노출)
            panel_dualToggle.Visibility = Visibility.Collapsed;
            _selectedRow = null;

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
                        rows.Add(new ReviewMeasurementRow(shot, fai, m));
                    }
                }
            }
            // 필터 원본 보관, ItemsSource 는 ApplyRowFilter 가 설정
            _allRows = rows;

            // 기본 전체 보기: 첫 Shot 이미지 + 전 overlay (불량 자동 포커스 전 즉시 표시)
            // 순서 반드시: LoadImage → SetInspectionOverlays
            var firstShot = cycle.Shots.FirstOrDefault();
            if (firstShot != null
                && !string.IsNullOrEmpty(firstShot.ResultImagePath)
                && File.Exists(firstShot.ResultImagePath))  // 누락 이미지 시 overlay 만 렌더
            {
                halconViewer.LoadImage(firstShot.ResultImagePath);
            }

            // overlay 재렌더: 전 Shot/FAI overlay 합산 (REPLACE 의미 — SetInspectionOverlays = Clear + AddRange)
            var allOverlays = cycle.Shots
                .SelectMany(s =>
                {
                    var t = s.FAIs;
                    if (t == null) t = new List<FaiResultDto>();
                    return t;
                })
                .Where(f => f.LastOverlays != null)
                .SelectMany(f => f.LastOverlays)
                .ToList();
            halconViewer.SetInspectionOverlays(allOverlays);

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
                visible = _allRows.Where(r => r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL").ToList();
            else
                visible = _allRows;
            dataGrid_measurements.ItemsSource = visible;

            // 첫 불량 행 자동 선택 → SelectionChanged 가 해당 FAI 이미지/overlay 로 포커스 (행 생성 후 지연 실행)
            var firstFail = visible.FirstOrDefault(r => r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL");
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
            ApplyRowFilter();
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

            if (row.Source != null && row.Source.IsDualImage)
            {
                panel_dualToggle.Visibility = Visibility.Visible;
                ShowAxisImage(true); // 기본 가로축
            }
            else
            {
                panel_dualToggle.Visibility = Visibility.Collapsed;
                // 일반 측정: 해당 FAI 의 Shot 이미지 로드 (순서: LoadImage → SetInspectionOverlays)
                string imgPath;
                if (row.OwnerShot != null) imgPath = row.OwnerShot.ResultImagePath; else imgPath = null;
                if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
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
        }

        private void Button_AxisHorizontal_Click(object sender, RoutedEventArgs e)
        {
            ShowAxisImage(true);
        }

        private void Button_AxisVertical_Click(object sender, RoutedEventArgs e)
        {
            ShowAxisImage(false);
        }

        // 현재 선택된 cycle 을 xlsx 로 export. 저장 위치 = cycle 폴더 기본 + 사용자 지정.
        private void Button_ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCycle == null)
            {
                CustomMessageBox.Show("엑셀 export", "먼저 cycle 을 선택하세요.", MessageBoxImage.Warning);
                return;
            }

            // 저장 위치: 해당 cycle 폴더 기본, fallback ResultSavePath
            string initialDir;
            if (!string.IsNullOrEmpty(_currentCycle.CycleFolderPath) && Directory.Exists(_currentCycle.CycleFolderPath))
                initialDir = _currentCycle.CycleFolderPath;
            else
                initialDir = SystemHandler.Handle.Setting.ResultSavePath;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = "result_" + _currentCycle.InspectionTime.ToString("yyyyMMdd_HHmmss") + ".xlsx",
                InitialDirectory = initialDir
            };

            if (dlg.ShowDialog() == true)
            {
                bool ok = ExcelExportService.Export(_currentCycle, dlg.FileName);
                string okMessage;
                if (ok) okMessage = "저장 완료:\n" + dlg.FileName; else okMessage = "export 실패 (로그 확인)";
                MessageBoxImage okIcon;
                if (ok) okIcon = MessageBoxImage.Information; else okIcon = MessageBoxImage.Error;
                CustomMessageBox.Show("엑셀 export",
                    okMessage,
                    okIcon);
            }
        }
    }

    /// <summary>ListBox 각 항목 — FolderPath(역직렬화 시 경로), DisplayText(시각·종합판정).</summary>
    public class CycleListItem
    {
        public string FolderPath { get; set; }

        public string DisplayText { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
