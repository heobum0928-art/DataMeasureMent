using System;
using System.Collections.Generic;
using System.IO;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    // Phase 80 D-80-02: 리뷰어 행이 불러오기를 막는 사유. CanApply 는 None 일 때만 true 가 된다
    // (SequenceBusy·SavedCycleRerunActive 는 예외 — 버튼은 그대로 두고 이유만 보여준다, VM 이 처리).
    public enum EReviewerRowBlock
    {
        None,
        NoRow,
        NoPhoto,
        RecipeMismatch,
        SequenceNotFound,
        ShotNotFound,
        SequenceBusy,
        SavedCycleRerunActive
    }

    // Phase 80: LoadForRow 결과. 실패 시 Live* 는 전부 비어 있다.
    public class ReviewerReinspectLoadResult
    {
        public bool IsLoaded { get; set; }
        public EReviewerRowBlock BlockReason { get; set; }
        public string BlockDetail { get; set; }
        public ShotConfig LiveShot { get; set; }
        public FAIConfig LiveFai { get; set; }
        public MeasurementBase LiveMeasurement { get; set; }
    }

    // Phase 80 D-80-12: 메인 화면 상태 줄이 표시할 값. VM 이 문자열로 조립한다.
    public class ReviewerReinspectState
    {
        public bool IsActive { get; set; }
        public DateTime CycleTime { get; set; }
        public int IndexNumber { get; set; }
        public string ShotName { get; set; }
        public string MeasurementName { get; set; }

        public ReviewerReinspectState Clone()
        {
            ReviewerReinspectState clone = new ReviewerReinspectState();
            clone.IsActive = IsActive;
            clone.CycleTime = CycleTime;
            clone.IndexNumber = IndexNumber;
            clone.ShotName = ShotName;
            clone.MeasurementName = MeasurementName;
            return clone;
        }
    }

    /// <summary>
    /// Phase 80: 리뷰어에서 고른 NG 사이클의 사진을 메인 화면 레시피 객체(메모리)에 불러오고,
    /// 해제/저장/PLC 자동 검사/레시피 변경/프로그램 종료 시 원래 경로·OfflineInspectMode 로 되돌린다.
    /// RepeatRunService.SavedCycleOverrideSnapshot 과 같은 스냅샷/복원 방식을 쓰되, "버튼 한 번 = 부품 하나"
    /// 라 큐가 아니라 활성 스냅샷 1개만 갖는다(D-80-11: 원래 값은 처음 것 유지, 대체 시 원복 후 재주입).
    /// UI 스레드(불러오기·저장)와 MainRun 백그라운드 스레드(PLC 해제) 동시 접근을 s_lock 으로 보호한다.
    /// </summary>
    public static class ReviewerReinspectService
    {
        public const string LOG_TAG = "[ReviewerLoad] ";
        public const string RELEASE_REASON_USER = "사용자 해제";
        public const string RELEASE_REASON_PLC_TEST = "PLC 자동 검사 수신";
        public const string RELEASE_REASON_RECIPE_CHANGED = "레시피 변경";
        public const string RELEASE_REASON_SHUTDOWN = "프로그램 종료";
        private const string LOG_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";

        // Phase 80: RepeatRunService.SavedCycleOverrideSnapshot(:442-453)과 같은 사전 5개 구성.
        private sealed class ReviewerOverrideSnapshot
        {
            public readonly Dictionary<ShotConfig, string> ShotSimulImagePaths = new Dictionary<ShotConfig, string>();
            public readonly Dictionary<DatumConfig, string> DatumTeachingPaths = new Dictionary<DatumConfig, string>();
            public readonly Dictionary<DatumConfig, string> DatumTeachingPathsVertical = new Dictionary<DatumConfig, string>();
            public readonly Dictionary<DualImageEdgeDistanceMeasurement, string> DualHorizontalPaths = new Dictionary<DualImageEdgeDistanceMeasurement, string>();
            public readonly Dictionary<DualImageEdgeDistanceMeasurement, string> DualVerticalPaths = new Dictionary<DualImageEdgeDistanceMeasurement, string>();
            public readonly List<ShotConfig> OwnedShots = new List<ShotConfig>();
            public InspectionSequence Sequence;
            public bool OfflineBefore;
            public bool OfflineSetByReviewer;
        }

        private static readonly object s_lock = new object();
        private static ReviewerOverrideSnapshot s_snapshot;
        private static SavedCycleRerunPart s_activePart;
        private static ReviewerReinspectState s_state;
        private static readonly List<ShotConfig> s_lstBufferedShots = new List<ShotConfig>();

        public static event Action StateChanged;

        private static void RaiseStateChanged()
        {
            Action handler = StateChanged;
            if (handler != null)
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    try { Logging.PrintErrLog((int)ELogType.Error, LOG_TAG + "상태 변경 알림 실패: " + ex.Message); } catch { }
                }
            }
        }

        public static bool IsActive
        {
            get
            {
                lock (s_lock)
                {
                    return s_snapshot != null;
                }
            }
        }

        public static ReviewerReinspectState GetState()
        {
            lock (s_lock)
            {
                if (s_snapshot != null)
                {
                    return s_state.Clone();
                }
                ReviewerReinspectState idle = new ReviewerReinspectState();
                idle.IsActive = false;
                return idle;
            }
        }

        // Phase 80 D-80-02/17: 버튼 활성 여부 판단 — VM.EvaluateSelection 이 호출한다.
        public static EReviewerRowBlock CheckRow(CycleResultDto cycle, ShotResultDto shotDto, FaiResultDto faiDto, out string szDetail)
        {
            szDetail = "";
            if (shotDto == null)
            {
                return EReviewerRowBlock.NoRow;
            }

            string szImagePath = ReviewerImagePathResolver.ResolveRowImagePath(shotDto, faiDto);
            if (string.IsNullOrEmpty(szImagePath))
            {
                return EReviewerRowBlock.NoPhoto;
            }

            bool bCycleNull = cycle == null;
            string szCycleRecipe;
            if (bCycleNull)
            {
                szCycleRecipe = "";
            }
            else
            {
                szCycleRecipe = cycle.RecipeName;
            }
            bool bRecipeMismatch = bCycleNull;
            if (!bRecipeMismatch)
            {
                bRecipeMismatch = !string.Equals(szCycleRecipe, SystemHandler.Handle.Setting.CurrentRecipeName, StringComparison.Ordinal);
            }
            if (bRecipeMismatch)
            {
                szDetail = szCycleRecipe;
                return EReviewerRowBlock.RecipeMismatch;
            }

            string szOwnerName = shotDto.OwnerSequenceName;
            if (string.IsNullOrEmpty(szOwnerName))
            {
                szOwnerName = SequenceHandler.SEQ_TOP;
            }
            InspectionSequence seq = ResolveSequence(shotDto.OwnerSequenceName);
            if (seq == null)
            {
                szDetail = szOwnerName;
                return EReviewerRowBlock.SequenceNotFound;
            }

            ShotConfig liveShot = FindLiveShot(seq, shotDto.ShotName);
            if (liveShot == null)
            {
                szDetail = shotDto.ShotName;
                return EReviewerRowBlock.ShotNotFound;
            }

            return EReviewerRowBlock.None;
        }

        private static InspectionSequence ResolveSequence(string szOwnerName)
        {
            string szTargetName = szOwnerName;
            if (string.IsNullOrEmpty(szTargetName))
            {
                szTargetName = SequenceHandler.SEQ_TOP;
            }
            int nCount = SystemHandler.Handle.Sequences.Count;
            for (int i = 0; i < nCount; i++)
            {
                InspectionSequence seq = SystemHandler.Handle.Sequences[i] as InspectionSequence;
                if (seq == null)
                {
                    continue;
                }
                if (string.Equals(seq.Name, szTargetName, StringComparison.OrdinalIgnoreCase))
                {
                    return seq;
                }
            }
            return null;
        }

        private static ShotConfig FindLiveShot(InspectionSequence seq, string szShotName)
        {
            if (seq == null)
            {
                return null;
            }
            InspectionRecipeManager recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            if (recipeManager == null || recipeManager.Shots == null)
            {
                return null;
            }
            foreach (ShotConfig shot in recipeManager.Shots)
            {
                if (!InspectionSequence.IsShotOwnedBySequence(shot, seq.Name))
                {
                    continue;
                }
                if (string.Equals(shot.ShotName, szShotName, StringComparison.Ordinal))
                {
                    return shot;
                }
            }
            return null;
        }

        private static FAIConfig FindLiveFai(ShotConfig shot, string szFaiName)
        {
            if (shot == null)
            {
                return null;
            }
            foreach (FAIConfig fai in shot.FAIList)
            {
                if (string.Equals(fai.FAIName, szFaiName, StringComparison.Ordinal))
                {
                    return fai;
                }
            }
            return null;
        }

        // Phase 80: RepeatRunService.cs:805-813 과 같은 규칙 — 이름이 비면 TypeName 을 키로 쓴다.
        private static string BuildMeasurementKey(string szName, string szTypeName)
        {
            if (string.IsNullOrEmpty(szName))
            {
                return szTypeName;
            }
            return szName;
        }

        private static MeasurementBase FindLiveMeasurement(FAIConfig fai, string szKey)
        {
            if (fai == null)
            {
                return null;
            }
            foreach (MeasurementBase meas in fai.Measurements)
            {
                string szMeasKey = BuildMeasurementKey(meas.MeasurementName, meas.TypeName);
                if (string.Equals(szMeasKey, szKey, StringComparison.Ordinal))
                {
                    return meas;
                }
            }
            return null;
        }

        // Phase 80 D-80-03/07/11/12/13: 고른 사이클의 사진을 메인 레시피 객체에 넣는다. UI 스레드 전용
        // (버튼 클릭 → VM.ApplySelection 이 부른다).
        public static ReviewerReinspectLoadResult LoadForRow(CycleResultDto cycle, ShotResultDto shotDto, FaiResultDto faiDto, MeasurementResultDto measDto)
        {
            string szDetail;
            EReviewerRowBlock block = CheckRow(cycle, shotDto, faiDto, out szDetail);
            if (block != EReviewerRowBlock.None)
            {
                ReviewerReinspectLoadResult blocked = new ReviewerReinspectLoadResult();
                blocked.IsLoaded = false;
                blocked.BlockReason = block;
                blocked.BlockDetail = szDetail;
                return blocked;
            }

            InspectionSequence seq = ResolveSequence(shotDto.OwnerSequenceName);
            ShotConfig liveShot = FindLiveShot(seq, shotDto.ShotName);
            FAIConfig liveFai = null;
            if (faiDto != null)
            {
                liveFai = FindLiveFai(liveShot, faiDto.FAIName);
            }
            MeasurementBase liveMeas = null;
            if (measDto != null)
            {
                string szMeasKey = BuildMeasurementKey(measDto.MeasurementName, measDto.TypeName);
                liveMeas = FindLiveMeasurement(liveFai, szMeasKey);
            }

            bool bSequenceBusy = seq.State != EContextState.Idle;
            if (bSequenceBusy)
            {
                ReviewerReinspectLoadResult busy = new ReviewerReinspectLoadResult();
                busy.IsLoaded = false;
                busy.BlockReason = EReviewerRowBlock.SequenceBusy;
                busy.BlockDetail = "";
                return busy;
            }
            bool bRerunActive = RepeatRunService.IsSavedCycleRerunActive;
            if (bRerunActive)
            {
                ReviewerReinspectLoadResult rerunBusy = new ReviewerReinspectLoadResult();
                rerunBusy.IsLoaded = false;
                rerunBusy.BlockReason = EReviewerRowBlock.SavedCycleRerunActive;
                rerunBusy.BlockDetail = "";
                return rerunBusy;
            }

            InspectionRecipeManager recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            SavedCycleRerunPart part = SavedCycleRerunPlanner.BuildPartForSingleCycle(cycle, seq, recipeManager);

            int nAppliedShotCount;
            lock (s_lock)
            {
                bool bSequenceChanged = s_snapshot != null && !ReferenceEquals(s_snapshot.Sequence, seq);
                if (bSequenceChanged)
                {
                    ReviewerOverrideSnapshot oldSnapshot = s_snapshot;
                    RestorePathsOnly(oldSnapshot);
                    ClearReviewerBuffers();
                    oldSnapshot.Sequence.ClearDatumTransforms();
                    ReviewerOverrideSnapshot freshSnapshot = BuildSnapshot(seq, recipeManager);
                    freshSnapshot.OfflineBefore = oldSnapshot.OfflineBefore;
                    freshSnapshot.OfflineSetByReviewer = oldSnapshot.OfflineSetByReviewer;
                    s_snapshot = freshSnapshot;
                }
                else if (s_snapshot == null)
                {
                    s_snapshot = BuildSnapshot(seq, recipeManager);
                }
                else
                {
                    RestorePathsOnly(s_snapshot);
                }

                nAppliedShotCount = ApplyPartPaths(part);

                bool bOfflineOff = !SystemSetting.Handle.OfflineInspectMode;
                if (bOfflineOff)
                {
                    SystemSetting.Handle.OfflineInspectMode = true;
                    s_snapshot.OfflineSetByReviewer = true;
                }

                seq.ClearDatumTransforms();

                LoadShotBuffer(liveShot);

                s_activePart = part;
                ReviewerReinspectState newState = new ReviewerReinspectState();
                newState.IsActive = true;
                newState.CycleTime = cycle.InspectionTime;
                int nIndexNumber = cycle.IndexNumber;
                if (nIndexNumber == RepeatRunService.MATERIAL_NOT_SET)
                {
                    nIndexNumber = part.IndexNumber;
                }
                newState.IndexNumber = nIndexNumber;
                newState.ShotName = liveShot.ShotName;
                string szMeasName;
                if (measDto == null)
                {
                    szMeasName = "";
                }
                else
                {
                    szMeasName = BuildMeasurementKey(measDto.MeasurementName, measDto.TypeName);
                }
                newState.MeasurementName = szMeasName;
                s_state = newState;

                try
                {
                    string szLogLine = LOG_TAG + "불러옴 — " + seq.Name + " · " + cycle.InspectionTime.ToString(LOG_TIME_FORMAT)
                        + " · 자재 " + nIndexNumber + " · Shot 사진 " + nAppliedShotCount + "장 · OfflineInspectMode 켬=" + s_snapshot.OfflineSetByReviewer;
                    Logging.PrintLog((int)ELogType.Trace, szLogLine);
                }
                catch { }
            }

            RaiseStateChanged();

            ReviewerReinspectLoadResult result = new ReviewerReinspectLoadResult();
            result.IsLoaded = true;
            result.LiveShot = liveShot;
            result.LiveFai = liveFai;
            result.LiveMeasurement = liveMeas;
            return result;
        }

        // Phase 80: RepeatRunService.BuildOverrideSnapshot(:455-491) 와 같은 필드·같은 순서.
        private static ReviewerOverrideSnapshot BuildSnapshot(InspectionSequence seq, InspectionRecipeManager recipeManager)
        {
            ReviewerOverrideSnapshot snap = new ReviewerOverrideSnapshot();
            snap.Sequence = seq;
            snap.OfflineBefore = SystemSetting.Handle.OfflineInspectMode;

            foreach (ShotConfig shot in recipeManager.Shots)
            {
                if (!InspectionSequence.IsShotOwnedBySequence(shot, seq.Name))
                {
                    continue;
                }
                snap.OwnedShots.Add(shot);
                snap.ShotSimulImagePaths[shot] = shot.SimulImagePath;
                foreach (FAIConfig fai in shot.FAIList)
                {
                    foreach (MeasurementBase meas in fai.Measurements)
                    {
                        DualImageEdgeDistanceMeasurement dualMeas = meas as DualImageEdgeDistanceMeasurement;
                        if (dualMeas == null)
                        {
                            continue;
                        }
                        snap.DualHorizontalPaths[dualMeas] = dualMeas.TeachingImagePath_Horizontal;
                        snap.DualVerticalPaths[dualMeas] = dualMeas.TeachingImagePath_Vertical;
                    }
                }
            }

            foreach (DatumConfig datum in seq.DatumConfigs)
            {
                snap.DatumTeachingPaths[datum] = datum.TeachingImagePath;
                snap.DatumTeachingPathsVertical[datum] = datum.TeachingImagePath_Vertical;
            }

            return snap;
        }

        // Phase 80: RepeatRunService.RestoreOverridePathsOnly(:495-521) 와 같은 필드·같은 순서,
        // 끝에 소유 Shot 의 RerunZRangeImagePaths = null.
        private static void RestorePathsOnly(ReviewerOverrideSnapshot snap)
        {
            foreach (var pair in snap.ShotSimulImagePaths)
            {
                pair.Key.SimulImagePath = pair.Value;
            }
            foreach (var pair in snap.DatumTeachingPaths)
            {
                pair.Key.TeachingImagePath = pair.Value;
            }
            foreach (var pair in snap.DatumTeachingPathsVertical)
            {
                pair.Key.TeachingImagePath_Vertical = pair.Value;
            }
            foreach (var pair in snap.DualHorizontalPaths)
            {
                pair.Key.TeachingImagePath_Horizontal = pair.Value;
            }
            foreach (var pair in snap.DualVerticalPaths)
            {
                pair.Key.TeachingImagePath_Vertical = pair.Value;
            }
            foreach (ShotConfig shot in snap.OwnedShots)
            {
                shot.RerunZRangeImagePaths = null;
            }
        }

        // Phase 80 D-80-07: 부품의 Shot 사진을 소유 Shot 에 적용하고, 모든 소유 Shot 의 Z 후보 사전을
        // 새로 채운다(비어 있어도 1장 폴백으로 동작). 적용된 Shot 사진 수를 반환한다(로그용).
        private static int ApplyPartPaths(SavedCycleRerunPart part)
        {
            int nApplied = 0;
            foreach (ShotConfig shot in s_snapshot.OwnedShots)
            {
                string szShotPath;
                bool bHasPath = part.ShotPhotoPaths.TryGetValue(shot.ShotName, out szShotPath);
                if (bHasPath && ReviewerImagePathResolver.IsUsableImageFile(szShotPath))
                {
                    shot.SimulImagePath = szShotPath;
                    nApplied++;
                }
                shot.RerunZRangeImagePaths = BuildZRangeMap(part, shot.ShotName);
            }
            return nApplied;
        }

        // Phase 80: RepeatRunService.BuildRerunZRangeMap(:769-782) 와 같은 규칙 — 항상 새 사전을 반환한다.
        private static Dictionary<int, string> BuildZRangeMap(SavedCycleRerunPart part, string szShotName)
        {
            Dictionary<int, string> dicResult = new Dictionary<int, string>();
            Dictionary<int, string> dicShot;
            bool bHasShot = part.ZRangePhotoPaths.TryGetValue(szShotName, out dicShot);
            if (bHasShot)
            {
                foreach (var pair in dicShot)
                {
                    dicResult[pair.Key] = pair.Value;
                }
            }
            return dicResult;
        }

        // Phase 80: 메인 캔버스가 Shot 버퍼(_image)를 먼저 보기 때문에, 새 경로를 즉시 HImage 로 읽어 넣는다.
        private static void LoadShotBuffer(ShotConfig liveShot)
        {
            ClearReviewerBuffers();
            if (liveShot == null)
            {
                return;
            }
            if (!ReviewerImagePathResolver.IsUsableImageFile(liveShot.SimulImagePath))
            {
                return;
            }
            HImage img = null;
            try
            {
                img = new HImage(liveShot.SimulImagePath);
                liveShot.SetImage(img);
                s_lstBufferedShots.Add(liveShot);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, LOG_TAG + "Shot 이미지 로드 실패: " + ex.Message); } catch { }
            }
            finally
            {
                if (img != null) { try { img.Dispose(); } catch { } }
            }
        }

        private static void ClearReviewerBuffers()
        {
            foreach (ShotConfig shot in s_lstBufferedShots)
            {
                shot.ClearImage();
            }
            s_lstBufferedShots.Clear();
        }

        // Phase 80 D-80-11: 원래 경로로 되돌리는 4경로 공용 진입점 — [해제]/PLC 자동 검사/레시피 변경/프로그램 종료.
        public static void Release(string szReason)
        {
            ReleaseCore(szReason, false);
        }

        public static void ReleaseByUser()
        {
            ReleaseCore(RELEASE_REASON_USER, true);
        }

        // bClearDisplayState 는 이 plan 에서는 쓰지 않는다(80-03 이 화면 선 지우기에 쓸 예정).
        // 전체를 try/catch 로 감싸 절대 throw 하지 않는다(MainRun 스레드 보호).
        private static void ReleaseCore(string szReason, bool bClearDisplayState)
        {
            bool bReleased = false;
            try
            {
                lock (s_lock)
                {
                    if (s_snapshot == null)
                    {
                        return;
                    }
                    RestorePathsOnly(s_snapshot);
                    bool bShouldTurnOff = s_snapshot.OfflineSetByReviewer && SystemSetting.Handle.OfflineInspectMode;
                    if (bShouldTurnOff)
                    {
                        SystemSetting.Handle.OfflineInspectMode = false;
                    }
                    s_snapshot.Sequence.ClearDatumTransforms();
                    ClearReviewerBuffers();
                    try
                    {
                        Logging.PrintLog((int)ELogType.Trace, LOG_TAG + "해제 — " + szReason);
                    }
                    catch { }
                    s_snapshot = null;
                    s_activePart = null;
                    s_state = null;
                    bReleased = true;
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, LOG_TAG + "해제 처리 중 예외: " + ex.Message); } catch { }
            }
            if (bReleased)
            {
                RaiseStateChanged();
            }
        }

        // Phase 80 D-80-10: 저장하는 순간만 원래 사진 경로로 되돌리고, 저장 뒤(예외여도) 불러온 경로를 다시 넣는다.
        public static bool RunWithOriginalPaths(Func<bool> fnSave)
        {
            if (fnSave == null)
            {
                return false;
            }
            lock (s_lock)
            {
                if (s_snapshot == null)
                {
                    return fnSave();
                }
                RestorePathsOnly(s_snapshot);
                try
                {
                    bool bSaved = fnSave();
                    try
                    {
                        Logging.PrintLog((int)ELogType.Trace, LOG_TAG + "레시피 저장 — 사진 경로는 불러오기 전 값으로 저장, 파라미터는 저장됨");
                    }
                    catch { }
                    return bSaved;
                }
                finally
                {
                    ApplyPartPaths(s_activePart);
                }
            }
        }

        // Phase 80 D-80-11: OnRecipeChanged 는 새 레시피를 읽은 뒤 발화하므로 옛 Shot 객체의 경로 복원은
        // 무해하고, 꼭 필요한 것은 상태 해제와 OfflineInspectMode 복원이다.
        public static void HandleRecipeChanged(object sender, RecipeChangedEventArgs args)
        {
            Release(RELEASE_REASON_RECIPE_CHANGED);
        }
    }
}
