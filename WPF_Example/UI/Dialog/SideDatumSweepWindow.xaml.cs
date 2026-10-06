using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    //261006 hbk Side Datum 초점 학습 사진 수집 창(임시 도구).
    //  수집 모드 켜기/끄기와 z 번호(가로 범위·세로) 설정, Datum 별 저장 장수 표시. 창을 닫으면 수집 모드가 꺼진다.
    public partial class SideDatumSweepWindow : Window
    {
        private static readonly string[] SIDE_SEQUENCES =
        {
            SequenceHandler.SEQ_SIDE_1,
            SequenceHandler.SEQ_SIDE_2,
            SequenceHandler.SEQ_SIDE_3,
            SequenceHandler.SEQ_SIDE_4,
        };

        private readonly Dictionary<string, TextBlock> _dicRows = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, int> _dicLastZ = new Dictionary<string, int>();
        private bool _bLoading = true;

        public SideDatumSweepWindow()
        {
            InitializeComponent();
            txtHStart.Text = SideDatumTrainCapture.HorizontalStartZ.ToString();
            txtHEnd.Text = SideDatumTrainCapture.HorizontalEndZ.ToString();
            if (SideDatumTrainCapture.VerticalZ == SideDatumTrainCapture.NO_VERTICAL)
            {
                txtV.Text = "";
            }
            else
            {
                txtV.Text = SideDatumTrainCapture.VerticalZ.ToString();
            }
            btnMode.IsChecked = SideDatumTrainCapture.IsActive;
            BuildRows();
            txtFolder.Text = "저장 폴더: " + SideDatumTrainCapture.SAVE_ROOT;
            SideDatumTrainCapture.Saved += OnSaved;
            Closed += Window_Closed;
            _bLoading = false;
            UpdateModeView();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SideDatumTrainCapture.Saved -= OnSaved;
            SideDatumTrainCapture.IsActive = false;   // 창을 닫으면 평소 검사로 돌아간다
        }

        private void BtnMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_bLoading)
            {
                return;
            }
            bool bTurnOn = btnMode.IsChecked == true;
            if (bTurnOn)
            {
                string szError;
                bool bValid = TryApplyZSettings(out szError);
                if (!bValid)
                {
                    _bLoading = true;
                    btnMode.IsChecked = false;
                    _bLoading = false;
                    txtLast.Text = szError;
                    return;
                }
            }
            SideDatumTrainCapture.IsActive = bTurnOn;
            UpdateModeView();
        }

        private bool TryApplyZSettings(out string szError)
        {
            szError = "";
            int nHStart;
            int nHEnd;
            int nV;
            bool bStartOk = int.TryParse(txtHStart.Text.Trim(), out nHStart);
            bool bEndOk = int.TryParse(txtHEnd.Text.Trim(), out nHEnd);
            bool bParsed = bStartOk && bEndOk;
            if (!bParsed)
            {
                szError = "가로 z 번호는 숫자로 넣어 주세요.";
                return false;
            }
            string szV = txtV.Text.Trim();
            if (string.IsNullOrEmpty(szV))
            {
                nV = SideDatumTrainCapture.NO_VERTICAL;   // 비우면 세로 없음
            }
            else
            {
                bool bVOk = int.TryParse(szV, out nV);
                if (!bVOk)
                {
                    szError = "세로 z 는 숫자로 넣거나 비워 두세요.";
                    return false;
                }
            }
            bool bRangeOk = nHStart >= 0 && nHEnd >= nHStart;
            if (!bRangeOk)
            {
                szError = "가로 z 범위가 올바르지 않습니다 (시작 ≤ 끝).";
                return false;
            }
            bool bOverlap = nV != SideDatumTrainCapture.NO_VERTICAL && nV >= nHStart && nV <= nHEnd;
            if (bOverlap)
            {
                szError = "세로 z 가 가로 범위와 겹칩니다.";
                return false;
            }
            SideDatumTrainCapture.HorizontalStartZ = nHStart;
            SideDatumTrainCapture.HorizontalEndZ = nHEnd;
            SideDatumTrainCapture.VerticalZ = nV;
            return true;
        }

        private void UpdateModeView()
        {
            bool bOn = SideDatumTrainCapture.IsActive;
            txtHStart.IsEnabled = !bOn;
            txtHEnd.IsEnabled = !bOn;
            txtV.IsEnabled = !bOn;
            if (bOn)
            {
                btnMode.Content = "■ 수집 모드 켜짐 — 누르면 끔";
                btnMode.Background = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
                btnMode.Foreground = Brushes.White;
                string szVertical = "세로는 찍지 않습니다";
                if (SideDatumTrainCapture.VerticalZ != SideDatumTrainCapture.NO_VERTICAL)
                {
                    szVertical = "z" + SideDatumTrainCapture.VerticalZ + " 가 오면 세로 사진을 저장합니다";
                }
                txtLast.Text = string.Format("Side 지그로 z{0}~{1} 이 오면 Datum 가로 사진을 저장합니다. {2}. 그 외 z 는 평소 검사.",
                    SideDatumTrainCapture.HorizontalStartZ, SideDatumTrainCapture.HorizontalEndZ, szVertical);
            }
            else
            {
                btnMode.Content = "□ 수집 모드 꺼짐 — 누르면 켬 (지금은 평소 검사)";
                btnMode.ClearValue(BackgroundProperty);
                btnMode.ClearValue(ForegroundProperty);
            }
        }

        private void BuildRows()
        {
            foreach (string szSeq in SIDE_SEQUENCES)
            {
                InspectionSequence seq = SystemHandler.Handle.Sequences[szSeq] as InspectionSequence;
                if (seq == null || seq.DatumConfigs.Count == 0)
                {
                    continue;
                }
                string szDatum = seq.DatumConfigs[0].DatumName;
                if (_dicRows.ContainsKey(szDatum))
                {
                    continue;
                }
                TextBlock row = new TextBlock();
                row.FontSize = 18;
                row.Margin = new Thickness(0, 3, 0, 3);
                panelRows.Children.Add(row);
                _dicRows[szDatum] = row;
                int nH;
                int nV;
                SideDatumTrainCapture.CountSaved(szDatum, out nH, out nV);
                UpdateRow(szDatum, nH, nV);
            }
            if (_dicRows.Count == 0)
            {
                TextBlock empty = new TextBlock();
                empty.FontSize = 14;
                empty.Text = "Side 시퀀스가 없습니다. 검사 모드가 Side 인지 확인하세요.";
                panelRows.Children.Add(empty);
            }
        }

        private void UpdateRow(string szDatum, int nHorizontal, int nVertical)
        {
            TextBlock row;
            if (!_dicRows.TryGetValue(szDatum, out row))
            {
                return;
            }
            string szLastZ = "-";
            int nLastZ;
            if (_dicLastZ.TryGetValue(szDatum, out nLastZ))
            {
                szLastZ = nLastZ.ToString();
            }
            row.Text = string.Format("{0}    마지막 z {1}    가로 {2}장 · 세로 {3}장",
                szDatum, szLastZ, nHorizontal, nVertical);
        }

        // 통신 스레드에서 불린다 → 화면 스레드로 넘긴다
        private void OnSaved(string szDatum, int nHorizontal, int nVertical, int nZ, int nMaterialNo)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _dicLastZ[szDatum] = nZ;
                UpdateRow(szDatum, nHorizontal, nVertical);
                txtLast.Text = string.Format("마지막 저장: {0}  z{1}  자재 {2}  {3:HH:mm:ss}",
                    szDatum, nZ, nMaterialNo, DateTime.Now);
            }));
        }
    }
}
