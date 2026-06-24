//260624 hbk Phase 61: RegisterCustomUI — 탭 Visibility 게이트 + 공유 뷰어 attach (AV-07/AV-08)
using System;
using System.Windows;
using System.Windows.Controls;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject {
    public partial class MainWindow
    {
        // D-03: 단일 공유 MainResultViewerControl — align 뷰 전용 (MainView 내부 뷰어와 무관)
        private MainResultViewerControl _alignViewer;

        /// <summary>
        /// Window_Loaded 시 호출. 공유 뷰어 생성 + 초기 탭 Visibility 게이트 적용.
        /// </summary>
        public void RegisterCustomUI()
        {
            //260624 hbk Phase 61 — 공유 뷰어 한 번만 생성
            _alignViewer = new MainResultViewerControl();
            RefreshEthernetVisionTabs();
        }

        /// <summary>
        /// EthernetVisionMode 읽어 Tray/Bottom 탭 Visibility 게이트.
        /// Loaded(RegisterCustomUI) + 설정창 닫힌 후 호출(D-04).
        /// 전 로직 try-catch — 예외 시 Logging 만, UI 무중단 (T-61-05).
        /// </summary>
        public void RefreshEthernetVisionTabs()
        {
            //260624 hbk Phase 61 — 탭 게이트 + 공유 뷰어 attach
            try
            {
                EEthernetVisionMode mode = SystemSetting.Handle.EthernetVisionMode;

                bool bTray   = (mode == EEthernetVisionMode.Tray);
                bool bBottom = (mode == EEthernetVisionMode.Bottom);

                // [Tray 비전] 탭 Visibility
                if (bTray)
                {
                    tabTray.Visibility = Visibility.Visible;
                }
                else
                {
                    tabTray.Visibility = Visibility.Collapsed;
                }

                // [Bottom 비전] 탭 Visibility
                if (bBottom)
                {
                    tabBottom.Visibility = Visibility.Visible;
                }
                else
                {
                    tabBottom.Visibility = Visibility.Collapsed;
                }

                // 공유 뷰어 attach: 활성 align 뷰에 단일 _alignViewer 부착
                DetachAlignViewer();

                if (bTray)
                {
                    trayVisionView.AttachSharedViewer(_alignViewer);
                }
                else if (bBottom)
                {
                    bottomVisionView.AttachSharedViewer(_alignViewer);
                }
                // else(None) — 어디에도 부착 안 함
            }
            catch (Exception ex)
            {
                Logging.PrintLog((int)ELogType.Error, "[Phase 61] RefreshEthernetVisionTabs 오류: " + ex.Message);
            }
        }

        /// <summary>
        /// _alignViewer 가 현재 부착된 Border 에서 detach.
        /// WPF 단일 부모 제약 — 재부모화 전에 반드시 호출(T-61-05).
        /// </summary>
        private void DetachAlignViewer()
        {
            //260624 hbk Phase 61 — 뷰어 이전 부모 해제
            if (_alignViewer == null)
            {
                return;
            }

            Border parentBorder = _alignViewer.Parent as Border;
            if (parentBorder != null)
            {
                parentBorder.Child = null;
            }
        }
    }
}
