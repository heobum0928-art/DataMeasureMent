using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HalconDotNet;
using ReringProject.Halcon.Services;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.UI
{
    /// <summary>
    /// 브러시 패널의 상태와 동작. 호스트(Datum / Bottom Align / Tray Align)는 훅 2개만 채우면
    /// 저장·재생성·상태문구는 전부 여기서 처리된다.
    ///
    /// 모달 대화상자를 쓰지 않는다 — 칠할 때마다 팝업이 뜨면 못 쓴다. 결과는 상태 문구로만 알린다.
    /// </summary>
    public class PatternBrushMaskViewModel : INotifyPropertyChanged
    {
        private const double BrushRadiusMinPx = 5.0;
        private const double BrushRadiusMaxPx = 200.0;
        private const double BrushRadiusDefaultPx = 20.0;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string szName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(szName));
            }
        }

        private MainResultViewerControl _viewer;
        private string _statusText = "브러시 마스킹 대기";

        // 호스트가 채우는 훅. null 이면 저장/재생성을 건너뛰고 상태 문구만 남긴다.
        public Func<IList<string>> ModelPathsProvider { get; set; }
        public Func<string> ModelRegenerator { get; set; }

        public void Attach(MainResultViewerControl viewer)
        {
            Detach();
            _viewer = viewer;
            if (_viewer == null)
            {
                return;
            }
            _viewer.BrushStrokeCompleted -= OnBrushStrokeCompleted;
            _viewer.BrushStrokeCompleted += OnBrushStrokeCompleted;
            _viewer.BrushRadiusPx = BrushRadiusPx;
            UpdateViewerBrushMode();
            RefreshStatus();
        }

        public void Detach()
        {
            if (_viewer == null)
            {
                return;
            }
            _viewer.BrushStrokeCompleted -= OnBrushStrokeCompleted;
            _viewer.StopBrushMasking();
            _viewer = null;
        }

        /// <summary>
        /// 시스템 설정과 양방향. 설정 저장은 기존 설정 화면 담당 — 여기서는 세션 내 즉시 적용만 한다.
        /// 끄면 칠하기/지우개도 함께 꺼진다 — 반영되지도 않을 마스크를 칠하게 두면 안 된다.
        /// </summary>
        public bool IsMaskEnabled
        {
            get { return SystemSetting.Handle.UsePatternBrushMask; }
            set
            {
                SystemSetting.Handle.UsePatternBrushMask = value;
                if (value == false)
                {
                    _isBrushActive = false;
                    _isEraseMode = false;
                    UpdateViewerBrushMode();
                    Raise(nameof(IsBrushActive));
                    Raise(nameof(IsEraseMode));
                }
                Raise();
                RefreshStatus();
            }
        }

        private bool _isBrushActive;
        /// <summary>칠하기 모드. 지우개와 상호배타 — 켜면 지우개가 꺼진다.</summary>
        public bool IsBrushActive
        {
            get { return _isBrushActive; }
            set
            {
                if (_isBrushActive == value)
                {
                    return;
                }
                // 옵션이 꺼져 있으면 칠할 수 없다. 체크박스를 원래대로 되돌린다.
                if (value == true && IsMaskEnabled == false)
                {
                    SetStatus("먼저 [브러시 마스킹 사용] 을 체크하세요 — 꺼진 상태에서는 칠할 수 없습니다");
                    Raise();
                    return;
                }

                _isBrushActive = value;
                if (_isBrushActive == true)
                {
                    _isEraseMode = false;   // 상호배타
                    Raise(nameof(IsEraseMode));
                }
                UpdateViewerBrushMode();
                Raise();
                RefreshStatus();
            }
        }

        private bool _isEraseMode;
        /// <summary>지우개 모드. 칠하기와 상호배타 — 켜면 칠하기가 꺼진다.</summary>
        public bool IsEraseMode
        {
            get { return _isEraseMode; }
            set
            {
                if (_isEraseMode == value)
                {
                    return;
                }
                if (value == true && IsMaskEnabled == false)
                {
                    SetStatus("먼저 [브러시 마스킹 사용] 을 체크하세요 — 꺼진 상태에서는 지울 수 없습니다");
                    Raise();
                    return;
                }

                _isEraseMode = value;
                if (_isEraseMode == true)
                {
                    _isBrushActive = false;   // 상호배타
                    Raise(nameof(IsBrushActive));
                }
                UpdateViewerBrushMode();
                Raise();
                RefreshStatus();
            }
        }

        // 칠하기/지우개 중 하나라도 켜져 있으면 뷰어 브러시 모드가 살아 있어야 한다.
        //  둘 다 꺼지면 모드를 끈다(마스크 자체는 지우지 않는다).
        private void UpdateViewerBrushMode()
        {
            if (_viewer == null)
            {
                return;
            }
            bool bAnyActive = _isBrushActive || _isEraseMode;
            if (bAnyActive == true)
            {
                _viewer.StartBrushMasking();
            }
            else
            {
                _viewer.StopBrushMasking();
            }
            _viewer.IsBrushEraseMode = _isEraseMode;
        }

        private double _brushRadiusPx = BrushRadiusDefaultPx;
        public double BrushRadiusPx
        {
            get { return _brushRadiusPx; }
            set
            {
                if (_viewer != null)
                {
                    // 뷰어가 클램프한 값을 되읽어 슬라이더와 실제 값이 어긋나지 않게 한다.
                    _viewer.BrushRadiusPx = value;
                    _brushRadiusPx = _viewer.BrushRadiusPx;
                }
                else
                {
                    double dValue = value;
                    if (dValue < BrushRadiusMinPx) { dValue = BrushRadiusMinPx; }
                    if (dValue > BrushRadiusMaxPx) { dValue = BrushRadiusMaxPx; }
                    _brushRadiusPx = dValue;
                }
                Raise();
                RefreshStatus();
            }
        }

        public string StatusText { get { return _statusText; } }

        /// <summary>지금 상태를 한 문장으로 알려준다.</summary>
        public void RefreshStatus()
        {
            string szMode = "대기";
            if (IsEraseMode == true)
            {
                szMode = "지우개";
            }
            else if (IsBrushActive == true)
            {
                szMode = "칠하기";
            }

            string szMask = "마스크 없음";
            if (_viewer != null)
            {
                if (_viewer.HasBrushMask == true)
                {
                    szMask = "마스크 있음";
                }
            }

            string szWarn = "";
            bool bDisabled = SystemSetting.Handle.UsePatternBrushMask == false;
            if (bDisabled == true)
            {
                szWarn = "  ⚠ 옵션이 꺼져 있어 모델에 반영되지 않습니다";
            }

            SetStatus(szMode + " · " + szMask + " · 굵기 " + BrushRadiusPx.ToString("F0") + "px" + szWarn);
        }

        private void SetStatus(string szText)
        {
            _statusText = szText;
            Raise(nameof(StatusText));
        }

        private void OnBrushStrokeCompleted(object sender, EventArgs e)
        {
            SaveAndRegenerate();
        }

        // 칠하기 1획이 끝난 시점에만 호출된다. 파일 쓰기 + 모델 재생성이라 자국마다 하면 안 된다(D-74-04).
        private void SaveAndRegenerate()
        {
            if (_viewer == null)
            {
                return;
            }

            IList<string> paths = null;
            if (ModelPathsProvider != null)
            {
                paths = ModelPathsProvider();
            }
            bool bNoPath = (paths == null) || (paths.Count == 0);
            if (bNoPath == true)
            {
                SetStatus("칠했지만 대상 모델 경로가 없습니다 — 패턴 ROI/슬롯을 먼저 선택하세요");
                return;
            }

            HObject region = _viewer.CloneBrushMaskRegion();
            int nSaved = 0;
            int nFailed = 0;
            string szLastError = null;
            try
            {
                foreach (string szModelPath in paths)
                {
                    if (string.IsNullOrEmpty(szModelPath))
                    {
                        continue;
                    }
                    if (region == null)
                    {
                        // 마스크가 비었다 = 지우개로 다 지웠다. 파일도 같이 지워 고아를 남기지 않는다.
                        PatternMaskService.DeleteMask(szModelPath);
                        nSaved = nSaved + 1;
                        continue;
                    }
                    string szError;
                    bool bOk = PatternMaskService.TrySaveMask(szModelPath, region, out szError);
                    if (bOk == true)
                    {
                        nSaved = nSaved + 1;
                    }
                    else
                    {
                        nFailed = nFailed + 1;
                        szLastError = szError;
                    }
                }
            }
            finally
            {
                if (region != null) { try { region.Dispose(); } catch { } }
            }

            if (nFailed > 0)
            {
                SetStatus("마스크 저장 실패 " + nFailed.ToString() + "건: " + szLastError);
                return;
            }

            // 마스크와 모델이 항상 일치하도록 즉시 재생성한다(D-74-04). 저장이 먼저여야 한다 —
            //  TryCreateModel 이 디스크의 마스크 파일을 읽기 때문이다.
            if (ModelRegenerator == null)
            {
                SetStatus("마스크 저장 완료 " + nSaved.ToString() + "건 (재생성 훅 없음)");
                return;
            }

            string szRegenError = ModelRegenerator();
            if (string.IsNullOrEmpty(szRegenError))
            {
                SetStatus("마스크 반영 + 모델 재생성 완료 (" + nSaved.ToString() + "개 경로)");
            }
            else
            {
                SetStatus("마스크는 저장했지만 모델 재생성 실패: " + szRegenError);
                Logging.PrintErrLog((int)ELogType.Error, "[PatternMask] 모델 재생성 실패: " + szRegenError);
            }
        }

        /// <summary>화면 자국과 디스크 파일을 함께 지우고 재생성한다(고아 마스크 방지).</summary>
        public void ClearMask()
        {
            if (_viewer != null)
            {
                _viewer.ClearBrushMask();
            }

            IList<string> paths = null;
            if (ModelPathsProvider != null)
            {
                paths = ModelPathsProvider();
            }
            int nDeleted = 0;
            if (paths != null)
            {
                foreach (string szModelPath in paths)
                {
                    if (string.IsNullOrEmpty(szModelPath))
                    {
                        continue;
                    }
                    bool bDeleted = PatternMaskService.DeleteMask(szModelPath);
                    if (bDeleted == true)
                    {
                        nDeleted = nDeleted + 1;
                    }
                }
            }

            if (ModelRegenerator != null)
            {
                string szRegenError = ModelRegenerator();
                if (string.IsNullOrEmpty(szRegenError) == false)
                {
                    SetStatus("마스크 파일 " + nDeleted.ToString() + "개 삭제, 모델 재생성 실패: " + szRegenError);
                    return;
                }
            }
            SetStatus("마스크 초기화 완료 (파일 " + nDeleted.ToString() + "개 삭제)");
        }

        /// <summary>대상(Datum 선택 / Align 슬롯)이 바뀔 때 호스트가 부른다.</summary>
        public void ReloadMaskFromDisk()
        {
            if (_viewer == null)
            {
                return;
            }
            _viewer.ClearBrushMask();

            IList<string> paths = null;
            if (ModelPathsProvider != null)
            {
                paths = ModelPathsProvider();
            }
            if (paths == null)
            {
                RefreshStatus();
                return;
            }

            foreach (string szModelPath in paths)
            {
                if (string.IsNullOrEmpty(szModelPath))
                {
                    continue;
                }
                // 옵션이 꺼져 있어도 '이미 칠해 둔 것'은 화면에 보여준다 — 그래야 사용자가 상태를 안다.
                //  TryLoadMask 는 옵션 게이트를 타므로 여기서는 파일 존재만 보고 직접 읽는다.
                bool bHas = PatternMaskService.HasMask(szModelPath);
                if (bHas == false)
                {
                    continue;
                }
                HObject loaded = null;
                try
                {
                    HOperatorSet.ReadRegion(out loaded, PatternMaskService.ResolveMaskPath(szModelPath));
                    _viewer.SetBrushMaskRegion(loaded);
                }
                catch (Exception ex)
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[PatternMask] 마스크 표시 로드 실패: " + ex.Message);
                }
                finally
                {
                    if (loaded != null) { try { loaded.Dispose(); } catch { } }
                }
                break;
            }
            RefreshStatus();
        }
    }
}
