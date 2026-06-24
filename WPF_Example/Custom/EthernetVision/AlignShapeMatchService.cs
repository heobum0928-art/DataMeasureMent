//260624 hbk Phase 59
//260624 hbk Phase 59 revision — 2-pattern + angle_lx baseline (D-03'/04'/05'/07')
using System;
using System.IO;
using HalconDotNet;
using Newtonsoft.Json;
using ReringProject.Halcon.Algorithms;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// D-01: AlignShapeMatchService — PatternMatchService 를 composition 으로 보유하여
    /// 이더넷 정렬용 Shape 모델 티칭(TryTeach) / 런타임 위치보정(Run) / 템플릿 존재확인(HasTemplate) 을 제공.
    /// D-03'/05': 2개 Shape 모델(TL _1.shm + BR _2.shm) 매칭 → angle_lx 로 Theta 산출.
    /// Tray = X/Y 오프셋, Bottom = X/Y/Theta. PatternMatchService 는 무수정.
    /// AV-03 (teach→.shm→find) + AV-04 (Tray X/Y / Bottom X/Y/Theta).
    /// </summary>
    public class AlignShapeMatchService {

        // D-01: Shape 엔진 고정 (PatternMatchService 의 "Shape" 분기 사용)
        private const string ENGINE = "Shape";

        // D-03: 최소 스코어. NumLevels(4)/MinContrast(10)은 PatternMatchService 내부 const — 재선언 불필요.
        private const double MIN_SCORE = 0.5;

        // D-03': 모드별 angle extent 기본값(deg). 런타임/UAT 튜닝 가능 const.
        private const double TRAY_ANGLE_EXTENT_DEG   = 10.0;   // Tray = 위치 위주, 작은 범위
        private const double BOTTOM_ANGLE_EXTENT_DEG = 45.0;   // Bottom = Theta 산출, 넓은 범위

        // D-05': px→mm. EthernetPixelResolution 단위 = μm/px → /1000 = mm/px
        private const double UM_PER_MM = 1000.0;

        // D-04': 이더넷 전용 하위 폴더명 + 파일명 접미사 + 사이드카 확장자
        private const string ETHERNET_ALIGN_FOLDER = "ETHERNET_ALIGN";
        private const string MODEL_SUFFIX_1        = "_1";   // TL 모델
        private const string MODEL_SUFFIX_2        = "_2";   // BR 모델
        private const string REF_POSE_EXT          = ".json";

        // 레시피명 미설정 시 폴백 폴더명
        private const string DEFAULT_RECIPE_NAME = "DEFAULT";

        // 전체 이미지 검색용 — ROI 중심(0,0) + 거대 len → TryFindPose 내부 클램프로 전 영역 커버
        private const double FULL_SEARCH_LEN = 99999.0;

        //260624 hbk Phase 60 — D-05: 피커센터 미캘 판정 임계 (|row|,|col| 모두 이 값 이하면 미캘).
        private const double PICKER_CENTER_ZERO_EPS = 1e-6;
        //260624 hbk Phase 60 — D-05: 회전중심 보정 부호/회전방향 (피커 컨트롤러 규약 — UAT 확정 전 기본 +1).
        private const double PICKER_ROTATION_SIGN = 1.0;

        private readonly PatternMatchService _matcher;

        public AlignShapeMatchService() {
            _matcher = new PatternMatchService();   // composition — D-01 (PatternMatchService 무수정 재사용)
        }

        // ─── 경로 헬퍼 ───────────────────────────────────────────────────────────

        // WR-01 fix: 경로 문자열만 계산 — IO 부작용 없음. 읽기 경로(HasTemplate/Run) 에서 사용.
        // modelIndex=1 → TL(_1.shm), modelIndex=2 → BR(_2.shm).
        private string BuildShmPath(EEthernetVisionMode mode, int modelIndex) {
            string recipePath = SystemSetting.Handle.RecipeSavePath;
            if (string.IsNullOrEmpty(recipePath)) {
                return null;
            }
            string recipeName = SystemSetting.Handle.CurrentRecipeName;
            if (string.IsNullOrEmpty(recipeName)) {
                recipeName = DEFAULT_RECIPE_NAME;
            }
            string folder = Path.Combine(recipePath, recipeName, ETHERNET_ALIGN_FOLDER);
            string modeFileName;
            if (mode == EEthernetVisionMode.Bottom) {
                modeFileName = "Bottom";
            }
            else {
                modeFileName = "Tray";
            }
            string suffix;
            if (modelIndex == 2) {
                suffix = MODEL_SUFFIX_2;
            }
            else {
                suffix = MODEL_SUFFIX_1;
            }
            return Path.Combine(folder, modeFileName + suffix + PatternMatchService.EXTENSION_SHAPE_MODEL);
        }

        // WR-01 fix: 쓰기 전용 헬퍼 — Directory.CreateDirectory 포함. TryTeach 에서만 호출.
        // D-04': {RecipeSavePath}\{CurrentRecipeName}\ETHERNET_ALIGN\{Tray|Bottom}_{1|2}.shm
        private string GetShmPath(EEthernetVisionMode mode, int modelIndex) {
            string path = BuildShmPath(mode, modelIndex);
            if (path == null) {
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        // D-04': ref json 경로 = _1.shm 옆 {Tray|Bottom}.json (모드 단위 1개)
        private string BuildJsonPath(EEthernetVisionMode mode) {
            string shm1 = BuildShmPath(mode, 1);
            if (shm1 == null) {
                return null;
            }
            // _1.shm → .json (접미사 제거 후 확장자 교체)
            string withoutExt = Path.Combine(
                Path.GetDirectoryName(shm1),
                Path.GetFileNameWithoutExtension(shm1).Replace(MODEL_SUFFIX_1, ""));
            return withoutExt + REF_POSE_EXT;
        }

        // ─── angle_lx 헬퍼 ───────────────────────────────────────────────────────

        // D-05': HALCON angle_lx(Row1,Col1,Row2,Col2) → rad. try-catch + HTuple dispose.
        // 실패 시 double.NaN 반환 — 호출자가 NaN 확인 후 Found=false 처리.
        private double ComputeAngleLx(double row1, double col1, double row2, double col2) {
            HTuple angleRad = null;
            try {
                HOperatorSet.AngleLx(row1, col1, row2, col2, out angleRad);
                return angleRad[0].D;
            }
            catch {
                return double.NaN;
            }
            finally {
                if (angleRad != null) {
                    try { angleRad.Dispose(); } catch { }
                }
            }
        }

        // ─── 사이드카 JSON 저장/로드 ──────────────────────────────────────────────

        // D-04': 레퍼런스 포즈 사이드카 json 저장 (2-center + baseline)
        private bool TrySaveRefPose(string jsonPath,
            double ref1Row, double ref1Col,
            double ref2Row, double ref2Col,
            double refBaselineRad,
            double angleExtentDeg,
            out string error) {
            error = null;
            try {
                AlignRefPose refPose = new AlignRefPose();
                refPose.Ref1Row        = ref1Row;
                refPose.Ref1Col        = ref1Col;
                refPose.Ref2Row        = ref2Row;
                refPose.Ref2Col        = ref2Col;
                refPose.RefBaselineRad = refBaselineRad;
                refPose.AngleExtentDeg = angleExtentDeg;
                refPose.Engine         = ENGINE;

                // IN-01 fix: 직렬화도 TypeNameHandling.None 명시 — 역직렬화 측 RCE 방지 설정과 대칭.
                JsonSerializerSettings saveSettings = new JsonSerializerSettings();
                saveSettings.TypeNameHandling = TypeNameHandling.None;
                saveSettings.Formatting = Formatting.Indented;
                string json = JsonConvert.SerializeObject(refPose, saveSettings);
                File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex) {
                error = "TrySaveRefPose: " + ex.Message;
                return false;
            }
        }

        // D-04': 사이드카 json 로드 (TypeNameHandling.None — RCE 방지. 실패 시 null)
        private AlignRefPose LoadRefPose(string jsonPath) {
            try {
                if (!File.Exists(jsonPath)) {
                    return null;
                }
                string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.TypeNameHandling = TypeNameHandling.None;
                return JsonConvert.DeserializeObject<AlignRefPose>(json, settings);
            }
            catch {
                return null;
            }
        }

        // ─── 템플릿 존재 확인 ────────────────────────────────────────────────────

        // D-07': 두 .shm + ref json 셋 모두 존재해야 사용 가능
        // WR-01 fix: BuildShmPath 사용 — 순수 존재 확인, IO 부작용 없음.
        public bool HasTemplate(EEthernetVisionMode mode) {
            if (mode == EEthernetVisionMode.None) {
                return false;
            }
            string shmPath1 = BuildShmPath(mode, 1);
            string shmPath2 = BuildShmPath(mode, 2);
            string jsonPath = BuildJsonPath(mode);
            if (shmPath1 == null || shmPath2 == null || jsonPath == null) {
                return false;
            }
            return File.Exists(shmPath1) && File.Exists(shmPath2) && File.Exists(jsonPath);
        }

        // D-07: 파일 존재 = 로드 가능 의미론 (실제 read 는 Run 내부 지연 수행)
        public bool TryLoadTemplate(EEthernetVisionMode mode) {
            return HasTemplate(mode);
        }

        // ─── 티칭 ────────────────────────────────────────────────────────────────

        // D-07': TryTeach = 2-ROI 입력 → TryCreateModel×2 + TryFindRefPose×2 + baseline angle_lx + 사이드카 JSON 저장.
        // ROI 파라미터는 Phase 61 UI 가 전달 — 이 서비스는 ROI 드로잉을 모름.
        public bool TryTeach(
            HImage img,
            double roi1Row, double roi1Col, double roi1Phi, double roi1Len1, double roi1Len2,
            double roi2Row, double roi2Col, double roi2Phi, double roi2Len1, double roi2Len2,
            EEthernetVisionMode mode,
            out string error)
        {
            error = null;

            if (img == null) {
                error = "img is null";
                return false;
            }
            if (mode == EEthernetVisionMode.None) {
                error = "mode is None";
                return false;
            }

            try {
                string shmPath1 = GetShmPath(mode, 1);
                string shmPath2 = GetShmPath(mode, 2);
                if (shmPath1 == null || shmPath2 == null) {
                    error = "RecipeSavePath 미설정";
                    return false;
                }
                string jsonPath = BuildJsonPath(mode);
                if (jsonPath == null) {
                    error = "jsonPath 산출 실패";
                    return false;
                }

                double angleExtentDeg;
                if (mode == EEthernetVisionMode.Bottom) {
                    angleExtentDeg = BOTTOM_ANGLE_EXTENT_DEG;
                }
                else {
                    angleExtentDeg = TRAY_ANGLE_EXTENT_DEG;
                }

                // Step 1: TL 모델 생성 (_1.shm)
                string createErr1;
                bool bCreated1 = _matcher.TryCreateModel(
                    img, roi1Row, roi1Col, roi1Phi, roi1Len1, roi1Len2,
                    ENGINE, angleExtentDeg, shmPath1, out createErr1);
                if (!bCreated1) {
                    error = "TryCreateModel[1]: " + createErr1;
                    return false;
                }

                // Step 2: BR 모델 생성 (_2.shm)
                string createErr2;
                bool bCreated2 = _matcher.TryCreateModel(
                    img, roi2Row, roi2Col, roi2Phi, roi2Len1, roi2Len2,
                    ENGINE, angleExtentDeg, shmPath2, out createErr2);
                if (!bCreated2) {
                    error = "TryCreateModel[2]: " + createErr2;
                    return false;
                }

                // Step 3: 동일 이미지에서 TL find → 레퍼런스 중심1 산출 (Row/Col 만 사용, 자체각 폐기)
                double r1Row, r1Col, r1AngleDeg, r1Score;
                string findErr1;
                bool bRef1 = _matcher.TryFindRefPose(
                    img, ENGINE, shmPath1, MIN_SCORE,
                    out r1Row, out r1Col, out r1AngleDeg, out r1Score, out findErr1);
                if (!bRef1) {
                    error = "TryFindRefPose[1]: " + findErr1;
                    return false;
                }

                // Step 4: 동일 이미지에서 BR find → 레퍼런스 중심2 산출
                double r2Row, r2Col, r2AngleDeg, r2Score;
                string findErr2;
                bool bRef2 = _matcher.TryFindRefPose(
                    img, ENGINE, shmPath2, MIN_SCORE,
                    out r2Row, out r2Col, out r2AngleDeg, out r2Score, out findErr2);
                if (!bRef2) {
                    error = "TryFindRefPose[2]: " + findErr2;
                    return false;
                }

                // Step 5: D-05' — 레퍼런스 baseline = angle_lx(Ref1, Ref2)
                double refBaselineRad = ComputeAngleLx(r1Row, r1Col, r2Row, r2Col);
                if (double.IsNaN(refBaselineRad)) {
                    error = "angle_lx 산출 실패 (두 중심 동일 위치 또는 HALCON 오류)";
                    return false;
                }

                // Step 6: 사이드카 JSON 저장 (두 중심 + baseline)
                bool bSaved = TrySaveRefPose(jsonPath,
                    r1Row, r1Col, r2Row, r2Col, refBaselineRad, angleExtentDeg, out error);
                if (!bSaved) {
                    return false;
                }

                Logging.PrintLog((int)ELogType.Camera,
                    "[ALIGN_SVC] teach OK ({0}): ref1=({1:F1},{2:F1}) ref2=({3:F1},{4:F1}) baseline={5:F4}rad",
                    mode, r1Row, r1Col, r2Row, r2Col, refBaselineRad);
                return true;
            }
            catch (Exception ex) {
                error = "TryTeach exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] TryTeach exception: {0}", ex.Message);
                return false;
            }
        }

        // ─── 런타임 위치보정 ─────────────────────────────────────────────────────

        // D-07': Run = 2-모델 find + angle_lx baseline → midpoint offset(px→mm) + Theta.
        // 실패 시 Found=false (예외 throw 없음, D-06).
        public AlignResult Run(HImage img, EEthernetVisionMode mode) {
            if (img == null || mode == EEthernetVisionMode.None) {
                AlignResult notFound = new AlignResult();
                notFound.Found = false;
                return notFound;
            }

            try {
                // WR-01 fix: Run = 읽기 경로 — BuildShmPath(IO 없음) 사용. 디렉터리 생성 불필요.
                string shmPath1 = BuildShmPath(mode, 1);
                string shmPath2 = BuildShmPath(mode, 2);
                string jsonPath = BuildJsonPath(mode);
                if (shmPath1 == null || shmPath2 == null || jsonPath == null) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] RecipeSavePath 미설정 ({0})", mode);
                    AlignResult noPath = new AlignResult();
                    noPath.Found = false;
                    return noPath;
                }

                AlignRefPose refPose = LoadRefPose(jsonPath);
                if (refPose == null) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] ref pose json missing: {0}", jsonPath);
                    AlignResult noRef = new AlignResult();
                    noRef.Found = false;
                    return noRef;
                }

                // Step 1: TL 모델 find (전체 이미지 검색, downsample=1.0)
                double f1Row, f1Col, f1AngleDeg, f1Score;
                string findErr1;
                bool bFound1 = _matcher.TryFindPose(
                    img, ENGINE, shmPath1,
                    0.0, 0.0,
                    FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                    0.0, MIN_SCORE, 1.0,
                    out f1Row, out f1Col, out f1AngleDeg, out f1Score, out findErr1);
                if (!bFound1) {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_SVC] find[1] failed ({0}): {1}", mode, findErr1 ?? "");
                    AlignResult miss = new AlignResult();
                    miss.Found = false;
                    return miss;
                }

                // Step 2: BR 모델 find
                double f2Row, f2Col, f2AngleDeg, f2Score;
                string findErr2;
                bool bFound2 = _matcher.TryFindPose(
                    img, ENGINE, shmPath2,
                    0.0, 0.0,
                    FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                    0.0, MIN_SCORE, 1.0,
                    out f2Row, out f2Col, out f2AngleDeg, out f2Score, out findErr2);
                if (!bFound2) {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_SVC] find[2] failed ({0}): {1}", mode, findErr2 ?? "");
                    AlignResult miss = new AlignResult();
                    miss.Found = false;
                    return miss;
                }

                // Step 3: D-05' — 런타임 baseline angle_lx + Theta
                double runtimeBaselineRad = ComputeAngleLx(f1Row, f1Col, f2Row, f2Col);
                if (double.IsNaN(runtimeBaselineRad)) {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_SVC] angle_lx 산출 실패 ({0})", mode);
                    AlignResult miss = new AlignResult();
                    miss.Found = false;
                    return miss;
                }
                double diffRad  = runtimeBaselineRad - refPose.RefBaselineRad;
                double thetaDeg = diffRad * 180.0 / Math.PI;

                // Step 4: midpoint offset (px → mm)
                double midFRow = (f1Row + f2Row) / 2.0;
                double midFCol = (f1Col + f2Col) / 2.0;
                double midRRow = (refPose.Ref1Row + refPose.Ref2Row) / 2.0;
                double midRCol = (refPose.Ref1Col + refPose.Ref2Col) / 2.0;
                double dRow    = midFRow - midRRow;
                double dCol    = midFCol - midRCol;
                double resMm   = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;

                bool bBottom = (mode == EEthernetVisionMode.Bottom);

                AlignResult result = new AlignResult();
                result.Found = true;
                result.Score = Math.Min(f1Score, f2Score);   // 두 스코어 중 낮은 값 = 보수적 지표

                if (bBottom) {
                    //260624 hbk Phase 60 — D-05: Bottom 은 피커센터 기준 강체보정 적용(미캘 시 폴백).
                    double corrRow, corrCol;
                    ApplyPickerCenterCorrection(dRow, dCol, thetaDeg, out corrRow, out corrCol);
                    result.OffsetXmm = corrCol * resMm;   // Col → X (UAT 에서 부호 확정)
                    result.OffsetYmm = corrRow * resMm;   // Row → Y
                    result.ThetaDeg = thetaDeg;
                    result.HasTheta = true;
                }
                else {
                    result.OffsetXmm = dCol * resMm;   // Tray = 미보정 midpoint offset (Phase 59 동작)
                    result.OffsetYmm = dRow * resMm;
                    result.ThetaDeg = 0.0;
                    result.HasTheta = false;
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[ALIGN_SVC] run OK ({0}): off=({1:F4},{2:F4})mm theta={3:F3} score1={4:F3} score2={5:F3}",
                    mode, result.OffsetXmm, result.OffsetYmm, result.ThetaDeg, f1Score, f2Score);
                return result;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] Run exception: {0}", ex.Message);
                AlignResult err = new AlignResult();
                err.Found = false;
                return err;
            }
        }

        //260624 hbk Phase 60 — D-05: AV-05 피커센터 기준 강체보정.
        // 부품은 피커센터를 중심으로 dθ 회전하므로, midpoint offset(dRow,dCol) 을 피커센터 기준
        // HomMat2dRotate(dθ, pickerRow, pickerCol) 로 재표현하여 보정 row/col 을 산출.
        // 피커센터 미캘(0,0) 시 → 입력 offset 그대로 반환(폴백 = Phase 59 동작, 회귀 0).
        // 부호/회전중심 규약은 피커 컨트롤러 기준 — UAT/통합 확정 (PICKER_ROTATION_SIGN 파라미터화).
        // TODO(Phase 61 UAT): PICKER_ROTATION_SIGN 및 회전 적용점 실 피커 기준 확정.
        // 실패 시 입력값 그대로 반환(throw 금지, D-06).
        private void ApplyPickerCenterCorrection(
            double dRow, double dCol, double thetaDeg,
            out double corrRow, out double corrCol)
        {
            corrRow = dRow;
            corrCol = dCol;

            double pickerRow = SystemSetting.Handle.PickerCenterRow;
            double pickerCol = SystemSetting.Handle.PickerCenterCol;
            bool bUncalibrated = (Math.Abs(pickerRow) <= PICKER_CENTER_ZERO_EPS)
                              && (Math.Abs(pickerCol) <= PICKER_CENTER_ZERO_EPS);
            if (bUncalibrated) {
                return;   // 폴백: 피커센터 미캘 → midpoint offset 그대로 (Phase 59 동작 유지)
            }

            HTuple homMat = null;
            HTuple rotMat = null;
            HTuple pointRow = null;
            HTuple pointCol = null;
            HTuple outRow = null;
            HTuple outCol = null;
            try {
                double thetaRad = PICKER_ROTATION_SIGN * thetaDeg * Math.PI / 180.0;
                // 피커센터를 회전중심으로 하는 강체 회전 행렬
                HOperatorSet.HomMat2dIdentity(out homMat);
                HOperatorSet.HomMat2dRotate(homMat, thetaRad, pickerRow, pickerCol, out rotMat);
                // 피커센터 + 현 offset 위치를 회전 변환 후, 피커센터 기준 잔여 offset 산출.
                pointRow = new HTuple(pickerRow + dRow);
                pointCol = new HTuple(pickerCol + dCol);
                HOperatorSet.AffineTransPoint2d(rotMat, pointRow, pointCol, out outRow, out outCol);
                corrRow = outRow[0].D - pickerRow;
                corrCol = outCol[0].D - pickerCol;
            }
            catch (Exception ex) {
                // 실패 시 폴백 — 입력 offset 그대로
                corrRow = dRow;
                corrCol = dCol;
                Logging.PrintLog((int)ELogType.Error,
                    "[ALIGN_SVC] ApplyPickerCenterCorrection exception (fallback): {0}", ex.Message);
            }
            finally {
                if (homMat != null) { try { homMat.Dispose(); } catch { } }
                if (rotMat != null) { try { rotMat.Dispose(); } catch { } }
                if (pointRow != null) { try { pointRow.Dispose(); } catch { } }
                if (pointCol != null) { try { pointCol.Dispose(); } catch { } }
                if (outRow != null) { try { outRow.Dispose(); } catch { } }
                if (outCol != null) { try { outCol.Dispose(); } catch { } }
            }
        }
    }
}
