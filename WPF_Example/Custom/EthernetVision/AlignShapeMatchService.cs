//260624 hbk Phase 59
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
    /// TrayAlign = X/Y 오프셋, BottomAlign = X/Y/Theta. PatternMatchService 는 무수정.
    /// AV-03 (teach→.shm→find) + AV-04 (Tray X/Y / Bottom X/Y/Theta).
    /// </summary>
    public class AlignShapeMatchService {

        // D-01: Shape 엔진 고정 (PatternMatchService 의 "Shape" 분기 사용)
        private const string ENGINE = "Shape";

        // D-03: 최소 스코어. NumLevels(4)/MinContrast(10)은 PatternMatchService 내부 const — 재선언 불필요.
        private const double MIN_SCORE = 0.5;

        // D-03: 모드별 angle extent 기본값(deg). 런타임/UAT 튜닝 가능 const.
        private const double TRAY_ANGLE_EXTENT_DEG   = 10.0;   // Tray = 위치 위주, 작은 범위
        private const double BOTTOM_ANGLE_EXTENT_DEG = 45.0;   // Bottom = Theta 산출, 넓은 범위

        // D-05: px→mm. EthernetPixelResolution 단위 = μm/px → /1000 = mm/px
        private const double UM_PER_MM = 1000.0;

        // D-04: 이더넷 전용 하위 폴더명 + 사이드카 확장자
        private const string ETHERNET_ALIGN_FOLDER = "ETHERNET_ALIGN";
        private const string REF_POSE_EXT          = ".json";

        // 레시피명 미설정 시 폴백 폴더명
        private const string DEFAULT_RECIPE_NAME = "DEFAULT";

        // 전체 이미지 검색용 — ROI 중심(0,0) + 거대 len → TryFindPose 내부 클램프로 전 영역 커버
        private const double FULL_SEARCH_LEN = 99999.0;

        private readonly PatternMatchService _matcher;

        public AlignShapeMatchService() {
            _matcher = new PatternMatchService();   // composition — D-01 (PatternMatchService 무수정 재사용)
        }

        // ─── 경로 헬퍼 ───────────────────────────────────────────────────────────

        // D-04: {RecipeSavePath}\{CurrentRecipeName}\ETHERNET_ALIGN\{Tray|Bottom}.shm
        // RecipeFileHelper.GetPatternModelFilePath 패턴 직접 적용 — Directory.CreateDirectory 포함.
        private string GetShmPath(EEthernetVisionMode mode) {
            string recipePath = SystemSetting.Handle.RecipeSavePath;
            string recipeName = SystemSetting.Handle.CurrentRecipeName;
            if (string.IsNullOrEmpty(recipeName)) {
                recipeName = DEFAULT_RECIPE_NAME;
            }

            string folder = Path.Combine(recipePath, recipeName, ETHERNET_ALIGN_FOLDER);
            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }

            string modeFileName;
            if (mode == EEthernetVisionMode.Bottom) {
                modeFileName = "Bottom";
            }
            else {
                modeFileName = "Tray";
            }
            return Path.Combine(folder, modeFileName + PatternMatchService.EXTENSION_SHAPE_MODEL);
        }

        // ─── 사이드카 JSON 저장/로드 ──────────────────────────────────────────────

        // D-04: 레퍼런스 포즈 사이드카 json 저장 (CycleResultSerializer.Save 패턴)
        private bool TrySaveRefPose(string jsonPath, double refRow, double refCol,
            double refAngleDeg, double angleExtentDeg, out string error) {
            error = null;
            try {
                AlignRefPose refPose = new AlignRefPose();
                refPose.RefRow         = refRow;
                refPose.RefCol         = refCol;
                refPose.RefAngleDeg    = refAngleDeg;
                refPose.AngleExtentDeg = angleExtentDeg;
                refPose.Engine         = ENGINE;

                string json = JsonConvert.SerializeObject(refPose, Formatting.Indented);
                File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex) {
                error = "TrySaveRefPose: " + ex.Message;
                return false;
            }
        }

        // D-04: 사이드카 json 로드 (TypeNameHandling.None — RCE 방지. 실패 시 null)
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

        // D-07: 템플릿(.shm + ref json) 둘 다 존재해야 사용 가능
        public bool HasTemplate(EEthernetVisionMode mode) {
            if (mode == EEthernetVisionMode.None) {
                return false;
            }
            string shmPath  = GetShmPath(mode);
            string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);
            return File.Exists(shmPath) && File.Exists(jsonPath);
        }

        // D-07: 파일 존재 = 로드 가능 의미론 (실제 read 는 Run 내부 지연 수행)
        public bool TryLoadTemplate(EEthernetVisionMode mode) {
            return HasTemplate(mode);
        }

        // ─── 티칭 ────────────────────────────────────────────────────────────────

        // D-07: TryTeach = TryCreateModel + TryFindRefPose + 사이드카 JSON 저장.
        // ROI 파라미터는 Phase 61 UI 가 전달 — 이 서비스는 ROI 드로잉을 모름.
        public bool TryTeach(
            HImage img,
            double roiRow, double roiCol, double roiPhi,
            double roiLen1, double roiLen2,
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
                string shmPath  = GetShmPath(mode);
                string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);

                double angleExtentDeg;
                if (mode == EEthernetVisionMode.Bottom) {
                    angleExtentDeg = BOTTOM_ANGLE_EXTENT_DEG;
                }
                else {
                    angleExtentDeg = TRAY_ANGLE_EXTENT_DEG;
                }

                // Step 1: create_shape_model + write_shape_model (PatternMatchService 위임)
                string createErr;
                bool bCreated = _matcher.TryCreateModel(
                    img, roiRow, roiCol, roiPhi, roiLen1, roiLen2,
                    ENGINE, angleExtentDeg, shmPath, out createErr);
                if (!bCreated) {
                    error = "TryCreateModel: " + createErr;
                    return false;
                }

                // Step 2: 동일 이미지에서 find → 레퍼런스 포즈 (deg) 산출
                double refRow, refCol, refAngleDeg, refScore;
                string findErr;
                bool bRef = _matcher.TryFindRefPose(
                    img, ENGINE, shmPath, MIN_SCORE,
                    out refRow, out refCol, out refAngleDeg, out refScore, out findErr);
                if (!bRef) {
                    error = "TryFindRefPose: " + findErr;
                    return false;
                }

                // Step 3: 사이드카 JSON 저장
                bool bSaved = TrySaveRefPose(jsonPath, refRow, refCol, refAngleDeg, angleExtentDeg, out error);
                if (!bSaved) {
                    return false;
                }

                Logging.PrintLog((int)ELogType.Camera,
                    "[ALIGN_SVC] teach OK ({0}): ref=({1:F1},{2:F1}) angle={3:F2} score={4:F3}",
                    mode, refRow, refCol, refAngleDeg, refScore);
                return true;
            }
            catch (Exception ex) {
                error = "TryTeach exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] TryTeach exception: {0}", ex.Message);
                return false;
            }
        }

        // ─── 런타임 위치보정 ─────────────────────────────────────────────────────

        // D-07: Run = read .shm + ref json → find → offset(px→mm). 실패 시 Found=false (예외 throw 없음, D-06).
        public AlignResult Run(HImage img, EEthernetVisionMode mode) {
            if (img == null || mode == EEthernetVisionMode.None) {
                AlignResult notFound = new AlignResult();
                notFound.Found = false;
                return notFound;
            }

            try {
                string shmPath  = GetShmPath(mode);
                string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);

                AlignRefPose refPose = LoadRefPose(jsonPath);
                if (refPose == null) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] ref pose json missing: {0}", jsonPath);
                    AlignResult noRef = new AlignResult();
                    noRef.Found = false;
                    return noRef;
                }

                // 전체 이미지 검색 (margin 0, downsample 1). TryFindPose 가 검색영역을 이미지 경계로 클램프.
                double curRow, curCol, curAngleDeg, curScore;
                string findErr;
                bool bFound = _matcher.TryFindPose(
                    img, ENGINE, shmPath,
                    0.0, 0.0,
                    FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                    0.0, MIN_SCORE, 1.0,
                    out curRow, out curCol, out curAngleDeg, out curScore, out findErr);

                if (!bFound) {
                    string sErr;
                    if (findErr == null) {
                        sErr = "";
                    }
                    else {
                        sErr = findErr;
                    }
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] find failed ({0}): {1}", mode, sErr);
                    AlignResult miss = new AlignResult();
                    miss.Found = false;
                    return miss;
                }

                // D-05: offset = cur − ref (px), px→mm
                double dRow = curRow - refPose.RefRow;
                double dCol = curCol - refPose.RefCol;
                double resMm = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;

                bool bBottom = (mode == EEthernetVisionMode.Bottom);

                AlignResult result = new AlignResult();
                result.Found     = true;
                result.Score     = curScore;
                result.OffsetXmm = dCol * resMm;   // Col → X (UAT 에서 부호 확정)
                result.OffsetYmm = dRow * resMm;   // Row → Y
                if (bBottom) {
                    result.ThetaDeg = curAngleDeg - refPose.RefAngleDeg;
                    result.HasTheta = true;
                }
                else {
                    result.ThetaDeg = 0.0;
                    result.HasTheta = false;
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[ALIGN_SVC] run OK ({0}): off=({1:F4},{2:F4})mm theta={3:F3} score={4:F3}",
                    mode, result.OffsetXmm, result.OffsetYmm, result.ThetaDeg, result.Score);
                return result;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] Run exception: {0}", ex.Message);
                AlignResult err = new AlignResult();
                err.Found = false;
                return err;
            }
        }
    }
}
