//260624 hbk Phase 59
//260624 hbk Phase 59 revision — 2-pattern + angle_lx baseline (D-03'/04'/05'/07')
using System;
using System.Collections.Generic;
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

        //260625 hbk Phase 61.1 — 시각화 상수
        // 검출 위치 표시용 ROI 박스 반폭(px). DispRectangle2 len1/len2 에 사용. 표시 목적 고정 크기.
        private const double DETECTED_ROI_HALF_LEN = 60.0;

        // WR-03 fix //260624 hbk: 미캘 판정 임계 = SystemSetting.PICKER_CENTER_ZERO_EPS 단일 소스 참조.
        // AlignShapeMatchService 내 독립 선언 제거 → SystemSetting public const 사용으로 통일.
        //260624 hbk Phase 60 — D-05: 회전중심 보정 부호/회전방향 (피커 컨트롤러 규약 — UAT 확정 전 기본 +1).
        private const double PICKER_ROTATION_SIGN = 1.0;

        private readonly PatternMatchService _matcher;

        public AlignShapeMatchService() {
            _matcher = new PatternMatchService();   // composition — D-01 (PatternMatchService 무수정 재사용)
        }

        // ─── 경로 헬퍼 ───────────────────────────────────────────────────────────

        // WR-01 fix: 경로 문자열만 계산 — IO 부작용 없음. 읽기 경로(HasTemplate/Run) 에서 사용.
        // modelIndex=1 → TL(_1.shm), modelIndex=2 → BR(_2.shm).
        //260626 hbk slot 파라미터 추가 — mode==Bottom && slot!=None 이면 Bottom_{token}_N.shm, 그 외 기존 경로 (D-02/D-09)
        private string BuildShmPath(EEthernetVisionMode mode, int modelIndex, EBottomAlignSlot slot = EBottomAlignSlot.None) { //260626 hbk 6슬롯 면별 모델명 경로 빌드
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
                //260626 hbk slot!=None 이면 Bottom_{token}, None 이면 기존 "Bottom" 폴백 (D-09)
                if (slot != EBottomAlignSlot.None) {
                    string token = EBottomAlignSlotMap.ToFileToken(slot); //260626 hbk 슬롯 토큰 조합
                    if (!string.IsNullOrEmpty(token)) {
                        modeFileName = "Bottom_" + token; //260626 hbk 예: "Bottom_3D_Top" (D-02)
                    }
                    else {
                        modeFileName = "Bottom"; //260626 hbk 빈 토큰 방어 → 기존 폴백
                    }
                }
                else {
                    modeFileName = "Bottom"; //260626 hbk slot==None → 기존 Bottom 단일 경로 폴백 (D-09, 회귀 0)
                }
            }
            else {
                modeFileName = "Tray"; //260626 hbk Tray 경로 무변경 (D-10)
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
        // D-04': {RecipeSavePath}\{CurrentRecipeName}\ETHERNET_ALIGN\{Tray|Bottom[_{slot}]}_{1|2}.shm
        //260626 hbk slot 파라미터 추가 (기본 None = 기존 폴백, D-09)
        private string GetShmPath(EEthernetVisionMode mode, int modelIndex, EBottomAlignSlot slot = EBottomAlignSlot.None) { //260626 hbk 슬롯 경로 쓰기 헬퍼
            string path = BuildShmPath(mode, modelIndex, slot); //260626 hbk slot 전달
            if (path == null) {
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        // D-04': ref json 경로 = _1.shm 옆 {Tray|Bottom[_{slot}]}.json (모드+슬롯 단위 1개)
        //260626 hbk slot 파라미터 추가 — BuildShmPath(mode,1,slot) 경로에서 마지막 _1 만 제거 (D-02)
        //260626 hbk 주의: Replace("_1","") 는 토큰 내부 _1(예: 2D_SIDE_1_1)을 이중 치환. EndsWith 방식으로 마지막 _1 만 제거.
        private string BuildJsonPath(EEthernetVisionMode mode, EBottomAlignSlot slot = EBottomAlignSlot.None) { //260626 hbk 슬롯 json 경로 (토큰 내부 _1 오치환 방지)
            string shm1 = BuildShmPath(mode, 1, slot); //260626 hbk slot 전달
            if (shm1 == null) {
                return null;
            }
            // _1.shm → .json (접미사 마지막 _1 만 제거 — 토큰 내부 _1 오치환 방지)
            string baseName = Path.GetFileNameWithoutExtension(shm1); //260626 hbk 예: "Bottom_2D_SIDE_1_1"
            string trimmedName;
            if (baseName.EndsWith(MODEL_SUFFIX_1)) { //260626 hbk "_1" 로 끝나면 마지막 _1 만 제거
                trimmedName = baseName.Substring(0, baseName.Length - MODEL_SUFFIX_1.Length);
            }
            else {
                trimmedName = baseName; //260626 hbk 안전 폴백 — 실제로는 항상 _1 로 끝남
            }
            string withoutExt = Path.Combine(Path.GetDirectoryName(shm1), trimmedName);
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
            double roi1Len1, double roi1Len2,
            double roi2Len1, double roi2Len2,
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
                refPose.Roi1Len1 = roi1Len1;   //260625 hbk Phase 61.1 F2
                refPose.Roi1Len2 = roi1Len2;
                refPose.Roi2Len1 = roi2Len1;
                refPose.Roi2Len2 = roi2Len2;

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

        // ─── 동축 공개 API (D-05 Phase 66) ──────────────────────────────────────

        //260626 hbk Phase 66 D-05 — 슬롯/Tray 레퍼런스 JSON 을 mode+slot 으로 로드(공개 래퍼).
        //  private BuildJsonPath + LoadRefPose 위임. 파일 없음/예외 → null (호출자 null-guard).
        //  Plan 03 UI + SystemHandler.ApplyCoaxLightForSlot 가 이 메서드를 소비한다.
        public AlignRefPose GetSlotRefPose(EEthernetVisionMode mode, EBottomAlignSlot slot)
        {
            try
            {
                string jsonPath = BuildJsonPath(mode, slot);   //260626 hbk 슬롯/Tray json 경로
                if (jsonPath == null)
                {
                    return null;   //260626 hbk 경로 산출 실패(RecipeSavePath 미설정 등)
                }
                return LoadRefPose(jsonPath);   //260626 hbk 기존 private 로더 재사용(키 부재 → Coax false/0)
            }
            catch
            {
                return null;   //260626 hbk 예외 시 null — 호출자가 동축 off 폴백
            }
        }

        //260626 hbk Phase 66 D-05 — 동축값만 슬롯/Tray JSON 에 반영(티칭 데이터 보존 load-merge-save).
        //  기존 refPose 로드 → Coax 필드만 덮어쓰기 → 재저장. 미티칭(json 없음) 슬롯은 새 refPose 로 동축만 기록.
        //  Plan 03 UI(CheckBox/Slider 저장)가 이 메서드를 소비한다. throw 없음(UI 경로, 호출자 out error 처리).
        public bool TrySaveCoax(EEthernetVisionMode mode, EBottomAlignSlot slot,
            bool coaxEnabled, int coaxLevel, out string error)
        {
            error = null;
            try
            {
                string jsonPath = BuildJsonPath(mode, slot);   //260626 hbk 슬롯/Tray json 경로
                if (jsonPath == null)
                {
                    error = "jsonPath 산출 실패";   //260626 hbk RecipeSavePath 미설정
                    return false;
                }

                AlignRefPose refPose = LoadRefPose(jsonPath);   //260626 hbk 기존 티칭 데이터 로드(있으면 보존)
                if (refPose == null)
                {
                    refPose = new AlignRefPose();   //260626 hbk 미티칭 슬롯 — 동축만 기록(티칭 데이터는 추후 TryTeach 가 채움)
                }

                refPose.CoaxEnabled = coaxEnabled;   //260626 hbk 동축 ON/OFF 갱신
                int nClamped = coaxLevel;            //260626 hbk WR-01: 0~255 범위 클램프 — JSON 변조/비정상값이 LightHandler 에 전달되지 않도록 방어
                if (nClamped < 0)
                {
                    nClamped = 0;
                }
                if (nClamped > 255)
                {
                    nClamped = 255;
                }
                refPose.CoaxLevel = nClamped;        //260626 hbk 클램프된 값 저장

                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));   //260626 hbk 폴더 부재 대비(미티칭 슬롯 쓰기)

                JsonSerializerSettings saveSettings = new JsonSerializerSettings();   //260626 hbk 기존 TrySaveRefPose 와 동일 설정
                saveSettings.TypeNameHandling = TypeNameHandling.None;
                saveSettings.Formatting = Formatting.Indented;
                string json = JsonConvert.SerializeObject(refPose, saveSettings);
                File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = "TrySaveCoax: " + ex.Message;   //260626 hbk 직렬화/IO 실패
                return false;
            }
        }

        // ─── 템플릿 존재 확인 ────────────────────────────────────────────────────

        // D-07': 두 .shm + ref json 셋 모두 존재해야 사용 가능
        // WR-01 fix: BuildShmPath 사용 — 순수 존재 확인, IO 부작용 없음.
        //260626 hbk slot 파라미터 추가 (기본 None = 기존 폴백, D-09)
        public bool HasTemplate(EEthernetVisionMode mode, EBottomAlignSlot slot = EBottomAlignSlot.None) { //260626 hbk 슬롯별 템플릿 존재 확인
            if (mode == EEthernetVisionMode.None) {
                return false;
            }
            string shmPath1 = BuildShmPath(mode, 1, slot); //260626 hbk slot 전달
            string shmPath2 = BuildShmPath(mode, 2, slot); //260626 hbk slot 전달
            string jsonPath = BuildJsonPath(mode, slot);     //260626 hbk slot 전달
            if (shmPath1 == null || shmPath2 == null || jsonPath == null) {
                return false;
            }
            return File.Exists(shmPath1) && File.Exists(shmPath2) && File.Exists(jsonPath);
        }

        // ─── 티칭 ────────────────────────────────────────────────────────────────

        // D-07': TryTeach = 2-ROI 입력 → TryCreateModel×2 + TryFindRefPose×2 + baseline angle_lx + 사이드카 JSON 저장.
        // ROI 파라미터는 Phase 61 UI 가 전달 — 이 서비스는 ROI 드로잉을 모름.
        //260626 hbk slot 파라미터 추가 (기본 None = 기존 Bottom 단일 경로 폴백, D-09)
        public bool TryTeach(
            HImage img,
            double roi1Row, double roi1Col, double roi1Phi, double roi1Len1, double roi1Len2,
            double roi2Row, double roi2Col, double roi2Phi, double roi2Len1, double roi2Len2,
            EEthernetVisionMode mode,
            out string error)
        {
            return TryTeach(img,
                roi1Row, roi1Col, roi1Phi, roi1Len1, roi1Len2,
                roi2Row, roi2Col, roi2Phi, roi2Len1, roi2Len2,
                mode, EBottomAlignSlot.None, out error); //260626 hbk 기존 호출자 하위호환 — slot 기본 None (D-09, 회귀 0)
        }

        //260626 hbk slot 파라미터를 받는 신규 TryTeach 오버로드 (6슬롯 면별 티칭, D-02)
        public bool TryTeach(
            HImage img,
            double roi1Row, double roi1Col, double roi1Phi, double roi1Len1, double roi1Len2,
            double roi2Row, double roi2Col, double roi2Phi, double roi2Len1, double roi2Len2,
            EEthernetVisionMode mode,
            EBottomAlignSlot slot,   //260626 hbk 슬롯 파라미터 (mode 다음, out error 앞)
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
                string shmPath1 = GetShmPath(mode, 1, slot); //260626 hbk slot 전달
                string shmPath2 = GetShmPath(mode, 2, slot); //260626 hbk slot 전달
                if (shmPath1 == null || shmPath2 == null) {
                    error = "RecipeSavePath 미설정";
                    return false;
                }
                string jsonPath = BuildJsonPath(mode, slot); //260626 hbk slot 전달
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
                    r1Row, r1Col, r2Row, r2Col, refBaselineRad, angleExtentDeg,
                    roi1Len1, roi1Len2, roi2Len1, roi2Len2, out error);   //260625 hbk Phase 61.1 F2
                if (!bSaved) {
                    return false;
                }

                Logging.PrintLog((int)ELogType.Camera,
                    "[ALIGN_SVC] teach OK ({0}/{1}): ref1=({2:F1},{3:F1}) ref2=({4:F1},{5:F1}) baseline={6:F4}rad",
                    mode, slot, r1Row, r1Col, r2Row, r2Col, refBaselineRad); //260626 hbk 슬롯 로그 추가
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
        //260626 hbk slot 파라미터 추가 (기본 None = 기존 Bottom 단일 경로 폴백, D-09)
        public AlignResult Run(HImage img, EEthernetVisionMode mode, EBottomAlignSlot slot = EBottomAlignSlot.None) { //260626 hbk 슬롯별 런타임 보정 실행
            if (img == null || mode == EEthernetVisionMode.None) {
                AlignResult notFound = new AlignResult();
                notFound.Found = false;
                return notFound;
            }

            try {
                // WR-01 fix: Run = 읽기 경로 — BuildShmPath(IO 없음) 사용. 디렉터리 생성 불필요.
                string shmPath1 = BuildShmPath(mode, 1, slot); //260626 hbk slot 전달
                string shmPath2 = BuildShmPath(mode, 2, slot); //260626 hbk slot 전달
                string jsonPath = BuildJsonPath(mode, slot);     //260626 hbk slot 전달
                if (shmPath1 == null || shmPath2 == null || jsonPath == null) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] RecipeSavePath 미설정 ({0}/{1})", mode, slot); //260626 hbk 슬롯 로그
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
                    result.ThetaDeg = thetaDeg; //260630 hbk 비전 원값 그대로 전송 (피커 캘리브 없음)
                    result.HasTheta = true; //260630 hbk
                }

                //260625 hbk Phase 61.1 — 시각화 필드 채우기(검출 좌표/ROI박스/에지 contour).
                // 실패해도 result.Found/Offset 등 기존 반환은 유지(D-05 무중단). try-catch 전체 포괄.
                try {
                    // (a) 검출 좌표 대입 (HALCON 호출 없음 — 이미 산출된 값 재사용)
                    result.HasDetection = true;
                    result.DetectedRow1     = f1Row;
                    result.DetectedCol1     = f1Col;
                    result.DetectedRow2     = f2Row;
                    result.DetectedCol2     = f2Col;
                    result.DetectedCenterRow = midFRow;
                    result.DetectedCenterCol = midFCol;

                    // (b) 보정 ROI 박스 산출 (검출 중심 + 고정 표시 크기 — 시각화 목적)
                    double[] box1;
                    BuildDetectedRoiBox(f1Row, f1Col, f1AngleDeg, refPose.Roi1Len1, refPose.Roi1Len2, out box1);   //260625 hbk Phase 61.1 F2
                    result.DetectedRoiBoxes.Add(box1);

                    double[] box2;
                    BuildDetectedRoiBox(f2Row, f2Col, f2AngleDeg, refPose.Roi2Len1, refPose.Roi2Len2, out box2);   //260625 hbk Phase 61.1 F2
                    result.DetectedRoiBoxes.Add(box2);

                    //260625 hbk Phase 61.1 F4 — 검출 에지 XLD 산출 (점 변환 없이 두 패턴 concat).
                    //  두 패턴 모델 contour 를 각각 검출 pose 로 affine_trans_contour_xld → concat_obj 1개 HObject.
                    //  소유권은 result.DetectedContourXld 로 이전 → 뷰어가 Dispose. (점 추출/다운샘플 제거 = 대각선 버그 해소)
                    string xldErr;
                    HObject combinedXld;
                    bool bXld = TryBuildDetectedContourXld(
                        shmPath1, f1Row, f1Col, f1AngleDeg,
                        shmPath2, f2Row, f2Col, f2AngleDeg,
                        out combinedXld, out xldErr);
                    if (bXld) {
                        result.DetectedContourXld = combinedXld;
                    }
                    else {
                        Logging.PrintLog((int)ELogType.Error,
                            "[ALIGN_SVC] contour XLD 산출 실패 ({0}): {1}", mode, xldErr ?? "");
                    }
                }
                catch (Exception exViz) {
                    // 시각화 필드 산출 예외 — 기존 result 반환은 유지 (무중단)
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_SVC] 시각화 필드 산출 예외 ({0}): {1}", mode, exViz.Message);
                }

                bool bHasXld = (result.DetectedContourXld != null);
                Logging.PrintLog((int)ELogType.Trace,
                    "[ALIGN_SVC] run OK ({0}/{1}): off=({2:F4},{3:F4})mm theta={4:F3} score1={5:F3} score2={6:F3} contourXld={7}",
                    mode, slot, result.OffsetXmm, result.OffsetYmm, result.ThetaDeg, f1Score, f2Score,
                    bHasXld); //260626 hbk 슬롯 로그 추가
                return result;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] Run exception: {0}", ex.Message);
                AlignResult err = new AlignResult();
                err.Found = false;
                return err;
            }
        }

        //260625 hbk Phase 61.1 — 검출 위치 기반 표시용 ROI 박스 산출 (HALCON 호출 없음).
        // box = {row, col, phi_rad, len1, len2} — DispRectangle2/RenderResultRoiBoxes 규약.
        // F2 fix: 실제 티칭 ROI 크기(roiLen1=Col 반폭, roiLen2=Row 반폭) 반영. 0 이하면 DETECTED_ROI_HALF_LEN 폴백(구 레시피 하위호환).
        private void BuildDetectedRoiBox(
            double detRow, double detCol, double detAngleDeg,
            double roiLen1, double roiLen2,
            out double[] box)
        {
            double phi = detAngleDeg * Math.PI / 180.0;
            double len1 = roiLen1;
            double len2 = roiLen2;
            if (len1 <= 0.0) {
                len1 = DETECTED_ROI_HALF_LEN;
            }
            if (len2 <= 0.0) {
                len2 = DETECTED_ROI_HALF_LEN;
            }
            box = new double[] { detRow, detCol, phi, len1, len2 };
        }

        //260625 hbk Phase 61.1 F4 — 두 패턴 검출 contour XLD 를 concat 하여 단일 HObject 산출.
        // 점 추출(get_contour_xld) 없이 affine_trans_contour_xld 결과 XLD 를 그대로 보존 → window.DispObj.
        // 패턴1 XLD 와 패턴2 XLD 를 concat_obj 로 합쳐 잘못된 패턴간 연결선(대각선 버그) 자체가 발생하지 않음.
        // 성공 시 outXld(소유권=호출자) 반환. 실패 시 throw 없이 false + error. 부분 성공(한 패턴만) 도 그 XLD 반환.
        private bool TryBuildDetectedContourXld(
            string shmPath1, double det1Row, double det1Col, double det1AngleDeg,
            string shmPath2, double det2Row, double det2Col, double det2AngleDeg,
            out HObject outXld, out string error)
        {
            outXld = null;
            error  = null;

            HObject moved1 = null;
            HObject moved2 = null;

            try {
                string err1;
                bool bMoved1 = TryBuildMovedContour(shmPath1, det1Row, det1Col, det1AngleDeg, out moved1, out err1);
                if (!bMoved1) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] movedContour[1] 실패: {0}", err1 ?? "");
                }

                string err2;
                bool bMoved2 = TryBuildMovedContour(shmPath2, det2Row, det2Col, det2AngleDeg, out moved2, out err2);
                if (!bMoved2) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] movedContour[2] 실패: {0}", err2 ?? "");
                }

                // 둘 다 실패 → outXld null 로 false
                if (!bMoved1 && !bMoved2) {
                    error = "두 패턴 contour 변환 모두 실패";
                    return false;
                }

                // 한쪽만 성공 → 그 XLD 단독 반환 (소유권 이전, finally 에서 dispose 안 함)
                if (bMoved1 && !bMoved2) {
                    outXld = moved1;
                    moved1 = null;
                    return true;
                }
                if (!bMoved1 && bMoved2) {
                    outXld = moved2;
                    moved2 = null;
                    return true;
                }

                // 둘 다 성공 → concat. 합친 결과만 소유권 이전, 원본 moved1/moved2 는 finally 에서 dispose.
                HObject combined;
                HOperatorSet.ConcatObj(moved1, moved2, out combined);
                outXld = combined;
                return true;
            }
            catch (Exception ex) {
                error = "TryBuildDetectedContourXld: " + ex.Message;
                if (outXld != null) { try { outXld.Dispose(); } catch { } outXld = null; }
                return false;
            }
            finally {
                if (moved1 != null) { try { moved1.Dispose(); } catch { } }
                if (moved2 != null) { try { moved2.Dispose(); } catch { } }
            }
        }

        //260625 hbk Phase 61.1 F4 — 단일 .shm 모델 contour 를 검출 pose 로 affine 이동한 XLD 반환.
        // read_shape_model → get_shape_model_contours(level 1) → vector_angle_to_rigid → affine_trans_contour_xld.
        // 성공 시 outMoved 소유권을 호출자에게 이전(finally 에서 dispose 안 함). 실패 시 throw 없이 false.
        private bool TryBuildMovedContour(
            string shmPath, double detRow, double detCol, double detAngleDeg,
            out HObject outMoved, out string error)
        {
            outMoved = null;
            error    = null;

            HTuple modelId        = null;
            HObject modelContours = null;
            HObject movedContours = null;
            HTuple homMat         = null;

            try {
                HOperatorSet.ReadShapeModel(shmPath, out modelId);
                HOperatorSet.GetShapeModelContours(out modelContours, modelId, 1);

                double detAngleRad = detAngleDeg * Math.PI / 180.0;
                // 모델 원점(0,0,0) → 검출 pose 강체변환 행렬
                HOperatorSet.VectorAngleToRigid(
                    0.0, 0.0, 0.0,
                    detRow, detCol, detAngleRad,
                    out homMat);
                HOperatorSet.AffineTransContourXld(modelContours, out movedContours, homMat);

                outMoved = movedContours;
                movedContours = null;   // 소유권 이전 — finally 에서 dispose 금지
                return true;
            }
            catch (Exception ex) {
                error = "TryBuildMovedContour: " + ex.Message;
                if (movedContours != null) { try { movedContours.Dispose(); } catch { } }
                return false;
            }
            finally {
                if (modelId       != null) { try { modelId.Dispose();       } catch { } }
                if (modelContours != null) { try { modelContours.Dispose(); } catch { } }
                if (homMat        != null) { try { homMat.Dispose();        } catch { } }
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
            bool bUncalibrated = (Math.Abs(pickerRow) <= SystemSetting.PICKER_CENTER_ZERO_EPS)
                              && (Math.Abs(pickerCol) <= SystemSetting.PICKER_CENTER_ZERO_EPS);
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
