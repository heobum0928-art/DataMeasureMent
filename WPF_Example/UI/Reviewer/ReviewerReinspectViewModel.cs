using System;
using System.ComponentModel;
using System.Windows;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    /// <summary>
    /// Phase 80 D-80-00/01/02/12/17: 리뷰어 버튼 활성·비활성 이유, 메인 화면 상태 줄 문자열을
    /// 만들어 바인딩만 하는 ViewModel. 로직은 ReviewerReinspectService 에 있다 — 이 클래스는 문구 조립과
    /// 서비스 호출 중계만 한다(MVVM, code-behind 는 배선만).
    /// 싱글턴 — 리뷰어 창(1개)과 메인 화면(1개)이 같은 상태를 공유해야 한다.
    /// </summary>
    public class ReviewerReinspectViewModel : INotifyPropertyChanged
    {
        private const string STATUS_PREFIX = "리뷰어 사진 사용 중 — ";
        private const string STATUS_TIME_FORMAT = "MM-dd HH:mm";
        private const string STATUS_MATERIAL_PREFIX = " 자재 ";
        private const string STATUS_MATERIAL_NONE = " 자재번호 없음";
        private const string REASON_NO_ROW = "측정 행을 먼저 고르세요";
        private const string REASON_NO_PHOTO = "이 검사의 사진 파일이 없어 불러올 수 없습니다";
        private const string REASON_RECIPE_MISMATCH_PREFIX = "지금 레시피와 다른 레시피의 검사입니다 — 검사 레시피: ";
        private const string REASON_SEQUENCE_NOT_FOUND_PREFIX = "이 PC 에 없는 검사입니다 — ";
        private const string REASON_SHOT_NOT_FOUND_PREFIX = "지금 레시피에 이 Shot 이 없습니다 — ";
        private const string REASON_SEQUENCE_BUSY = "검사가 진행 중입니다 — 끝난 뒤 다시 누르세요";
        private const string REASON_RERUN_ACTIVE = "저장 사진 재검사 중에는 쓸 수 없습니다";

        // Phase 80 D-80-09/19: 기준점 사진 짝이 안 맞을 때 보여줄 알림 문구 — 이 phase 의 유일한 새 대화상자.
        private const string ALERT_TITLE_DATUM_MISSING = "기준점 사진 없음";
        private const string ALERT_TEXT_DATUM_MISSING = "이 검사에는 짝이 맞는 기준점 사진이 없습니다 (수동 검사 기록이거나 사진 파일이 없음).\n"
            + "Shot 사진만 불러오고, 기준점 사진은 지금 것을 그대로 씁니다.\n기준점 Test Find 는 자동으로 하지 않습니다.";

        // Phase 80 D-80-08/12/14: 상태 줄에 덧붙이는 안내 문구.
        private const string HINT_JPG = "  (JPG 사진 — 실제 검사값과 조금 다를 수 있음)";
        private const string HINT_Z_MISSING = "  (Z 후보 사진 없음 — 고른 z 사진 1장으로 검사)";
        private const string HINT_DATUM_KEPT = "  (기준점 사진 없음 — 지금 기준점 사진 사용)";

        public static readonly ReviewerReinspectViewModel Instance = new ReviewerReinspectViewModel();

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string szName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(szName));
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; Raise("IsActive"); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value; Raise("StatusText"); }
        }

        private bool _canApply;
        public bool CanApply
        {
            get { return _canApply; }
            set { _canApply = value; Raise("CanApply"); }
        }

        private string _disableReasonText = REASON_NO_ROW;
        public string DisableReasonText
        {
            get { return _disableReasonText; }
            set { _disableReasonText = value; Raise("DisableReasonText"); }
        }

        // Phase 80 D-80-04: MainWindow 가 채워, 불러오기 성공 시 메인 화면을 앞으로 가져온다.
        public Action<ReviewerReinspectLoadResult> MainNavigator { get; set; }

        // Phase 80 D-80-09/17: ReviewerWindow 가 채워, 기준점 사진 없음 알림(제목, 문구)을 띄운다.
        public Action<string, string> AlertPresenter { get; set; }

        private ReviewerReinspectViewModel()
        {
            ReviewerReinspectService.StateChanged += OnServiceStateChanged;
        }

        // Phase 80 D-80-02: 행 선택이 바뀔 때마다 버튼 활성·이유를 갱신한다.
        public void EvaluateSelection(CycleResultDto cycle, ReviewMeasurementRow row)
        {
            if (row == null)
            {
                CanApply = false;
                DisableReasonText = REASON_NO_ROW;
                return;
            }
            string szDetail;
            EReviewerRowBlock block = ReviewerReinspectService.CheckRow(cycle, row.OwnerShot, row.OwnerFai, out szDetail);
            CanApply = block == EReviewerRowBlock.None;
            DisableReasonText = BuildReasonText(block, szDetail);
        }

        // Phase 80 D-80-03: 확인창 없이 바로 불러온다.
        public void ApplySelection(CycleResultDto cycle, ReviewMeasurementRow row)
        {
            if (row == null)
            {
                return;
            }
            ReviewerReinspectLoadResult result = ReviewerReinspectService.LoadForRow(cycle, row.OwnerShot, row.OwnerFai, row.Source);
            if (!result.IsLoaded)
            {
                DisableReasonText = BuildReasonText(result.BlockReason, result.BlockDetail);
                return;
            }
            DisableReasonText = "";

            // Phase 80 D-80-09: 기준점 사진 짝이 안 맞으면 메인 화면으로 넘어가기 전에 알림 1개.
            bool bShowAlert = result.IsDatumPhotoMissing && AlertPresenter != null;
            if (bShowAlert)
            {
                AlertPresenter(ALERT_TITLE_DATUM_MISSING, ALERT_TEXT_DATUM_MISSING);
            }

            Action<ReviewerReinspectLoadResult> navigator = MainNavigator;
            if (navigator != null)
            {
                navigator(result);
            }
            RefreshFromService();
        }

        public void ReleaseByUser()
        {
            ReviewerReinspectService.ReleaseByUser();
        }

        // Phase 80 D-80-11: PLC 해제는 MainRun 백그라운드 스레드에서 온다 — UI 스레드로 넘긴다.
        private void OnServiceStateChanged()
        {
            if (Application.Current == null)
            {
                return;
            }
            if (Application.Current.Dispatcher.CheckAccess())
            {
                RefreshFromService();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(RefreshFromService));
            }
        }

        private void RefreshFromService()
        {
            ReviewerReinspectState state = ReviewerReinspectService.GetState();
            IsActive = state.IsActive;
            if (state.IsActive)
            {
                StatusText = BuildStatusText(state);
            }
            else
            {
                StatusText = "";
            }
        }

        private string BuildStatusText(ReviewerReinspectState state)
        {
            string szMaterialPart;
            if (state.IndexNumber >= 0)
            {
                szMaterialPart = STATUS_MATERIAL_PREFIX + state.IndexNumber;
            }
            else
            {
                szMaterialPart = STATUS_MATERIAL_NONE;
            }
            string szText = STATUS_PREFIX + state.CycleTime.ToString(STATUS_TIME_FORMAT) + szMaterialPart;

            // Phase 80 D-80-08/12/14: JPG·Z 후보·기준점 안내를 상태 줄 끝에 순서대로 붙인다.
            if (state.IsJpgPhoto)
            {
                szText += HINT_JPG;
            }
            if (state.IsZCandidateMissing)
            {
                szText += HINT_Z_MISSING;
            }
            if (state.IsDatumPhotoKept)
            {
                szText += HINT_DATUM_KEPT;
            }
            return szText;
        }

        private string BuildReasonText(EReviewerRowBlock block, string szDetail)
        {
            switch (block)
            {
                case EReviewerRowBlock.None:
                    return "";
                case EReviewerRowBlock.NoRow:
                    return REASON_NO_ROW;
                case EReviewerRowBlock.NoPhoto:
                    return REASON_NO_PHOTO;
                case EReviewerRowBlock.RecipeMismatch:
                    return REASON_RECIPE_MISMATCH_PREFIX + szDetail;
                case EReviewerRowBlock.SequenceNotFound:
                    return REASON_SEQUENCE_NOT_FOUND_PREFIX + szDetail;
                case EReviewerRowBlock.ShotNotFound:
                    return REASON_SHOT_NOT_FOUND_PREFIX + szDetail;
                case EReviewerRowBlock.SequenceBusy:
                    return REASON_SEQUENCE_BUSY;
                case EReviewerRowBlock.SavedCycleRerunActive:
                    return REASON_RERUN_ACTIVE;
                default:
                    return "";
            }
        }
    }
}
