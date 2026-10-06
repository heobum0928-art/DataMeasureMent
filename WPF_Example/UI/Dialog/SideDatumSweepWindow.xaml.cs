using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    //261006 hbk Side Datum 초점 학습 사진 수집 현황 창(임시 도구, 보기 전용).
    //  들어온 장수를 Z_STEP_COUNT 로 나눠 회차·z 를 보여준다 — 순서와 횟수는 PLC 가 정한다.
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

        public SideDatumSweepWindow()
        {
            InitializeComponent();
            BuildRows();
            txtLast.Text = "PLC 신호로 Datum 가로 사진이 찍히면 자동으로 저장됩니다.";
            txtFolder.Text = "저장 폴더: " + SideDatumTrainCapture.SAVE_ROOT;
            SideDatumTrainCapture.Saved += OnSaved;
            Closed += (s, e) => { SideDatumTrainCapture.Saved -= OnSaved; };
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
                row.FontSize = 20;
                row.Margin = new Thickness(0, 4, 0, 4);
                panelRows.Children.Add(row);
                _dicRows[szDatum] = row;
                UpdateRow(szDatum, SideDatumTrainCapture.CountSaved(szDatum));
            }
            if (_dicRows.Count == 0)
            {
                TextBlock empty = new TextBlock();
                empty.FontSize = 14;
                empty.Text = "Side 시퀀스가 없습니다. 검사 모드가 Side 인지 확인하세요.";
                panelRows.Children.Add(empty);
            }
        }

        private void UpdateRow(string szDatum, int nCount)
        {
            TextBlock row;
            if (!_dicRows.TryGetValue(szDatum, out row))
            {
                return;
            }
            int nRound = 1;
            int nZ = 0;
            if (nCount > 0)
            {
                nRound = (nCount - 1) / SideDatumTrainCapture.Z_STEP_COUNT + 1;
                nZ = (nCount - 1) % SideDatumTrainCapture.Z_STEP_COUNT + 1;
            }
            row.Text = string.Format("{0}   회차 {1}   z {2} / {3}   (총 {4}장)",
                szDatum, nRound, nZ, SideDatumTrainCapture.Z_STEP_COUNT, nCount);
        }

        // 촬영 스레드에서 불린다 → 화면 스레드로 넘긴다
        private void OnSaved(string szDatum, int nCount, int nMaterialNo)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateRow(szDatum, nCount);
                txtLast.Text = string.Format("마지막 저장: {0}  #{1:0000}  자재 {2}  {3:HH:mm:ss}",
                    szDatum, nCount, nMaterialNo, DateTime.Now);
            }));
        }
    }
}
