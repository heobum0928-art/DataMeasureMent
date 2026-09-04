//260624 hbk Phase 60 — AV-05 picker center calibration (D-01/D-02 corrected/D-03)
//260630 hbk Phase 60 재작성 — ShapeModel 기반(find_shape_model) + 시각화 XLD 출력.
//  에지 감지(EdgesSubPix+FitCircleContourXld 지그원) 제거.
//  검출 흐름: TryTeachModel(1회) → TryLoadModel → TryAddStep×N → TryComputePickerCenter.
using System;
using System.Collections.Generic;
using System.IO;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// D-01: 피커 센터 캘리브레이션 — 상태형 누적 서비스.
    /// 피커가 전용 Cal 지그(원)를 픽업한 채 10°×36스텝 회전 →
    /// 각 스텝에서 find_shape_model 로 지그 중심 검출 → 편심원의 중심 = 피커 실제 회전중심.
    /// D-02(정정): 스텝별 중심 = find_shape_model RowCheck/ColumnCheck (에지 피팅 제거).
    /// fit_circle_contour_xld: gen_contour_polygon_xld(누적 중심) → 편심원 피팅 → 피커센터.
    /// 외부(Phase 61 UI / TCP $ALIGN_CALIB)가 스텝마다 TryAddStep 호출.
    /// 전 메서드 try-catch → false (D-06, Grabber 무영향).
    /// </summary>
    public class PickerCenterCalibrationService {

        // D-03: 편심원 피팅 최소 누적 점 수.
        private const int MIN_STEPS = 6;

        // D-03: 반경 정상 범위 가드(px).
        private const double MIN_RADIUS_PX = 1.0;
        private const double MAX_RADIUS_PX = 100000.0;

        // find_shape_model 파라미터.
        // quick-mc1 — MinScore 는 SystemSetting.Handle.PickerCalFindMinScore 로 운영자 조절 가능하게
        //  전환(지그 검출이 안 될 때 현장에서 낮춰볼 수 있어야 한다는 요구). Greediness/MaxOverlap 은 이번 범위 밖.
        private const double FIND_GREEDINESS  = 0.7;
        private const double FIND_MAX_OVERLAP = 0.5;

        // 시각화 십자 크기(px).
        private const double CROSS_SIZE = 20.0;

        // 캘 모델 파일명 (recipe_folder/ETHERNET_ALIGN/ 하위).
        private const string CAL_MODEL_FILENAME   = "picker_cal.shm";
        private const string ETHERNET_ALIGN_FOLDER = "ETHERNET_ALIGN";
        private const string DEFAULT_RECIPE_NAME   = "DEFAULT";

        // ShapeModel 캐시.
        private HTuple _modelId     = null;
        private bool   _modelLoaded = false;

        // 누적 지그 중심점 (절대 row/col).
        private readonly List<double> _rows = new List<double>();
        private readonly List<double> _cols = new List<double>();

        // 누적 시각화 XLD (스텝 십자 + Compute 후 원 + 중심 십자).
        private HObject _vizXld = null;

        // ─── 공개 속성 ──────────────────────────────────────────────────────────

        /// <summary>누적된 스텝 수.</summary>
        public int StepCount {
            get { return _rows.Count; }
        }

        /// <summary>ShapeModel 로드 여부.</summary>
        public bool HasModel {
            get { return _modelLoaded; }
        }

        /// <summary>
        /// quick-260903-dpy — 편심원 피팅에 필요한 최소 누적 스텝 수(읽기 전용).
        /// UI 가 [피커센터 계산] 버튼 활성화 조건을 판단할 때 MIN_STEPS 를 새로 하드코딩하지
        /// 않도록 노출한다. 알고리즘/판정 로직은 변경하지 않는다.
        /// </summary>
        public int MinSteps {
            get { return MIN_STEPS; }
        }

        /// <summary>
        /// quick-260812: 등급 산정용 최소 스코어(읽기 전용).
        /// quick-mc1 — 값의 출처가 하드코딩 상수에서 SystemSetting.Handle.PickerCalFindMinScore 로
        /// 바뀌었다. 임계값이 바뀌면 등급 기준도 같이 따라가야 일관성이 유지되므로 그대로 반영한다.
        /// </summary>
        public static double FindMinScore {
            get { return SystemSetting.Handle.PickerCalFindMinScore; }
        }

        // ─── 초기화/정리 ────────────────────────────────────────────────────────

        // D-01: 누적 초기화. 모델은 유지(재로드 불필요). 시각화 XLD 클리어.
        public void Reset() {
            _rows.Clear();
            _cols.Clear();
            ClearVizXld();
        }

        // 전체 정리 (모델 포함). 서비스 재사용 시 TryLoadModel 필요.
        public void FullReset() {
            Reset();
            ClearModel();
        }

        /// <summary>
        /// quick-260903-dpy — 수동 반복(자재를 손으로 놓고 찍기)중 마지막 스텝 1개만 취소한다.
        /// 10회 안팎을 손으로 반복하다 한 번 잘못 찍혀도 Reset() 으로 전부 되돌리지 않고 이어서
        /// 계속할 수 있게 한다. 누적이 비어 있으면 false. 원 피팅 알고리즘/기존 메서드 시그니처는
        /// 손대지 않는다 — _rows/_cols 마지막 원소만 제거한다.
        /// 시각화(_vizXld)는 스텝별 재구성 대상이 아니므로 함께 지운다 — 다음 TryAddStep 또는
        /// TryComputePickerCenter 호출 시 남은 점 기준으로 다시 그려진다.
        /// </summary>
        public bool TryRemoveLastStep() {
            bool bEmpty = (_rows.Count == 0);
            if (bEmpty) {
                return false;
            }
            _rows.RemoveAt(_rows.Count - 1);
            _cols.RemoveAt(_cols.Count - 1);
            ClearVizXld();
            return true;
        }

        // ─── 모델 경로 ──────────────────────────────────────────────────────────

        /// <summary>캘 모델 .shm 전체 경로 (레시피 폴더 하위 ETHERNET_ALIGN).</summary>
        public string GetCalibModelPath() {
            string recipePath = SystemSetting.Handle.RecipeSavePath;
            if (string.IsNullOrEmpty(recipePath)) {
                return null;
            }
            string recipeName = SystemSetting.Handle.CurrentRecipeName;
            if (string.IsNullOrEmpty(recipeName)) {
                recipeName = DEFAULT_RECIPE_NAME;
            }
            string folder = Path.Combine(recipePath, recipeName, ETHERNET_ALIGN_FOLDER);
            return Path.Combine(folder, CAL_MODEL_FILENAME);
        }

        // ─── 모델 티칭/로드 ─────────────────────────────────────────────────────

        /// <summary>
        /// 현재 이미지의 원형 ROI 내에서 ShapeModel 생성 후 저장.
        /// 기존 캐시 모델을 교체하고 _modelLoaded=true 로 전환.
        /// </summary>
        public bool TryTeachModel(HImage img,
            double roiRow1, double roiCol1, double roiRow2, double roiCol2,
            out string error) {
            error = null;
            if (img == null) {
                error = "img is null";
                return false;
            }
            string shmPath = GetCalibModelPath();
            if (string.IsNullOrEmpty(shmPath)) {
                error = "레시피 경로 미설정";
                return false;
            }

            HObject roiRegion  = null;
            HObject imgReduced = null;
            HTuple  newModelId = null;
            try {
                string folder = Path.GetDirectoryName(shmPath);
                if (!Directory.Exists(folder)) {
                    Directory.CreateDirectory(folder);
                }

                HOperatorSet.GenRectangle1(out roiRegion, roiRow1, roiCol1, roiRow2, roiCol2); //260630 hbk — 원형→사각형 ROI 전환
                HOperatorSet.ReduceDomain(img, roiRegion, out imgReduced);

                // 360° 전 회전 대응 모델 생성.
                HOperatorSet.CreateShapeModel(
                    imgReduced,
                    new HTuple("auto"),
                    new HTuple(0.0),
                    new HTuple(Math.PI * 2.0),
                    new HTuple("auto"),
                    new HTuple("auto"),
                    new HTuple("use_polarity"),
                    new HTuple("auto"),
                    new HTuple(10),
                    out newModelId);

                HOperatorSet.WriteShapeModel(newModelId, shmPath);

                // 기존 캐시 교체.
                ClearModel();
                _modelId     = newModelId;
                newModelId   = null; // 소유권 이전 완료
                _modelLoaded = true;

                Logging.PrintLog((int)ELogType.Camera,
                    "[PICKER_CAL] TryTeachModel: 모델 저장 → {0}", shmPath);
                return true;
            }
            catch (Exception ex) {
                error = "TryTeachModel exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[PICKER_CAL] TryTeachModel exception: {0}", ex.Message);
                return false;
            }
            finally {
                if (roiRegion  != null) { try { roiRegion.Dispose();  } catch { } }
                if (imgReduced != null) { try { imgReduced.Dispose(); } catch { } }
                if (newModelId != null) { try { newModelId.Dispose(); } catch { } }
            }
        }

        /// <summary>
        /// 파일에서 ShapeModel 로드 → 캐시. 이미 로드된 경우 즉시 true.
        /// </summary>
        public bool TryLoadModel(out string error) {
            error = null;
            if (_modelLoaded) {
                return true;
            }
            string shmPath = GetCalibModelPath();
            if (string.IsNullOrEmpty(shmPath)) {
                error = "레시피 경로 미설정";
                return false;
            }
            bool bFileExists = File.Exists(shmPath);
            if (!bFileExists) {
                error = "모델 파일 없음: " + shmPath;
                return false;
            }

            HTuple loadedId = null;
            try {
                HOperatorSet.ReadShapeModel(shmPath, out loadedId);
                ClearModel();
                _modelId     = loadedId;
                loadedId     = null;
                _modelLoaded = true;
                Logging.PrintLog((int)ELogType.Camera,
                    "[PICKER_CAL] TryLoadModel: 로드 완료 ← {0}", shmPath);
                return true;
            }
            catch (Exception ex) {
                error = "TryLoadModel exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[PICKER_CAL] TryLoadModel exception: {0}", ex.Message);
                return false;
            }
            finally {
                if (loadedId != null) { try { loadedId.Dispose(); } catch { } }
            }
        }

        // ─── 스텝별 중심 누적 ───────────────────────────────────────────────────

        /// <summary>
        /// 한 스텝: 원형 ROI 내 find_shape_model → 지그 중심 누적 + 시각화 XLD 갱신.
        /// foundRow/foundCol: 검출된 지그 중심(px). 실패 시 0.
        /// </summary>
        public bool TryAddStep(HImage img,
            double roiRow1, double roiCol1, double roiRow2, double roiCol2,
            out double foundRow, out double foundCol,
            out double dScore,   //quick-260812: 이미 계산된 검색 점수 노출(추가 HALCON 호출 0)
            out string error) {
            foundRow = 0.0;
            foundCol = 0.0;
            dScore   = 0.0;
            error    = null;

            if (!_modelLoaded) {
                error = "모델 미로드 — TryLoadModel 먼저 호출";
                return false;
            }
            if (img == null) {
                error = "img is null";
                return false;
            }

            HObject roiRegion  = null;
            HObject imgReduced = null;
            HTuple  rowCheck   = null;
            HTuple  colCheck   = null;
            HTuple  angleCheck = null;
            HTuple  score      = null;
            try {
                HOperatorSet.GenRectangle1(out roiRegion, roiRow1, roiCol1, roiRow2, roiCol2); //260630 hbk — 원형→사각형 ROI 전환
                HOperatorSet.ReduceDomain(img, roiRegion, out imgReduced);

                HOperatorSet.FindShapeModel(
                    imgReduced, _modelId,
                    new HTuple(0.0),
                    new HTuple(Math.PI * 2.0),
                    new HTuple(SystemSetting.Handle.PickerCalFindMinScore),
                    new HTuple(1),
                    new HTuple(FIND_MAX_OVERLAP),
                    new HTuple("least_squares"),
                    new HTuple(0),
                    new HTuple(FIND_GREEDINESS),
                    out rowCheck, out colCheck, out angleCheck, out score);

                bool bFound = rowCheck != null && rowCheck.Length > 0;
                if (!bFound) {
                    error = "검출 실패 — Score 미달 또는 모델 불일치";
                    return false;
                }

                double dRow = rowCheck[0].D;
                double dCol = colCheck[0].D;

                _rows.Add(dRow);
                _cols.Add(dCol);
                foundRow = dRow;
                foundCol = dCol;
                dScore   = score[0].D;   //quick-260812: finally 의 Dispose 전에 값만 복사

                AppendCrossToViz(dRow, dCol);

                Logging.PrintLog((int)ELogType.Camera,
                    "[PICKER_CAL] step {0}: center=({1:F2},{2:F2}) score={3:F3}",
                    _rows.Count, dRow, dCol, score[0].D);
                return true;
            }
            catch (Exception ex) {
                error = "TryAddStep exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[PICKER_CAL] TryAddStep exception: {0}", ex.Message);
                return false;
            }
            finally {
                if (roiRegion  != null) { try { roiRegion.Dispose();  } catch { } }
                if (imgReduced != null) { try { imgReduced.Dispose(); } catch { } }
                if (rowCheck   != null) { try { rowCheck.Dispose();   } catch { } }
                if (colCheck   != null) { try { colCheck.Dispose();   } catch { } }
                if (angleCheck != null) { try { angleCheck.Dispose(); } catch { } }
                if (score      != null) { try { score.Dispose();      } catch { } }
            }
        }

        // ─── 피커센터 산출 ──────────────────────────────────────────────────────

        /// <summary>
        /// 누적 지그 중심들로 gen_contour_polygon_xld → fit_circle_contour_xld → 피커센터(row,col)+radius + SystemSetting 저장.
        /// 성공 시 _vizXld 에 피팅 원 + 중심 십자 추가.
        /// </summary>
        public bool TryComputePickerCenter(out double row, out double col, out double radius, out string error) {
            double dResidualRmsPx;
            double dResidualMaxPx;
            return TryComputePickerCenter(out row, out col, out radius, out dResidualRmsPx, out dResidualMaxPx, out error);
        }

        /// <summary>
        /// quick-mc1 — 4-out 과 동일한 계산에 편심원 피팅 잔차(RMS/최대, px)를 추가로 반환한다.
        /// 잔차는 표시 전용이다 — 반환값·성공/실패 계약(반경 가드 포함)은 4-out 과 완전히 동일하며
        /// 잔차 값으로 새로 실패시키는 분기는 없다(임계값을 정할 실측 산포 데이터가 아직 없기 때문).
        /// </summary>
        public bool TryComputePickerCenter(out double row, out double col, out double radius,
            out double dResidualRmsPx, out double dResidualMaxPx, out string error) {
            row    = 0.0;
            col    = 0.0;
            radius = 0.0;
            dResidualRmsPx = 0.0;
            dResidualMaxPx = 0.0;
            error  = null;

            if (_rows.Count < MIN_STEPS) {
                error = "누적 점 부족: " + _rows.Count + " < " + MIN_STEPS;
                return false;
            }

            HObject contour   = null;
            HTuple  allRows   = null;
            HTuple  allCols   = null;
            HTuple  fitRow    = null;
            HTuple  fitCol    = null;
            HTuple  fitRad    = null;
            HTuple  startPhi  = null;
            HTuple  endPhi    = null;
            HTuple  pointOrder = null;
            try {
                allRows = new HTuple(_rows.ToArray());
                allCols = new HTuple(_cols.ToArray());

                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                // 편심원 피팅 → 피커 회전중심.
                HOperatorSet.FitCircleContourXld(contour, "algebraic", -1, 0, 0, 3, 2,
                    out fitRow, out fitCol, out fitRad, out startPhi, out endPhi, out pointOrder);

                if (fitRow == null || fitRow.Length == 0) {
                    error = "편심원 피팅 실패 (점 없음)";
                    return false;
                }

                double fittedRow = fitRow[0].D;
                double fittedCol = fitCol[0].D;
                double fittedRad = fitRad[0].D;

                bool bRadiusBad = (fittedRad < MIN_RADIUS_PX) || (fittedRad > MAX_RADIUS_PX)
                               || double.IsNaN(fittedRad) || double.IsInfinity(fittedRad);
                if (bRadiusBad) {
                    error = "피팅 반경 비정상: " + fittedRad.ToString("F2");
                    return false;
                }

                ComputeFitResiduals(fittedRow, fittedCol, fittedRad, out dResidualRmsPx, out dResidualMaxPx);

                // 피커센터 설정 (Save는 UI 확인 후 호출자 책임).
                SystemSetting.Handle.PickerCenterRow = fittedRow; //260630 hbk — Save 분리: UI 확인 후 저장
                SystemSetting.Handle.PickerCenterCol = fittedCol;

                row    = fittedRow;
                col    = fittedCol;
                radius = fittedRad;

                // 시각화: 피팅 원 + 중심 십자 추가.
                AppendCircleToViz(fittedRow, fittedCol, fittedRad);
                AppendCrossToViz(fittedRow, fittedCol);

                Logging.PrintLog((int)ELogType.Camera,
                    "[PICKER_CAL] computed: center=({0:F2},{1:F2}) r={2:F2} from {3} steps residual_rms={4:F2}px residual_max={5:F2}px",
                    fittedRow, fittedCol, fittedRad, _rows.Count, dResidualRmsPx, dResidualMaxPx);
                return true;
            }
            catch (Exception ex) {
                error = "TryComputePickerCenter exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[PICKER_CAL] compute exception: {0}", ex.Message);
                return false;
            }
            finally {
                if (contour    != null) { try { contour.Dispose();    } catch { } }
                if (allRows    != null) { try { allRows.Dispose();    } catch { } }
                if (allCols    != null) { try { allCols.Dispose();    } catch { } }
                if (fitRow     != null) { try { fitRow.Dispose();     } catch { } }
                if (fitCol     != null) { try { fitCol.Dispose();     } catch { } }
                if (fitRad     != null) { try { fitRad.Dispose();     } catch { } }
                if (startPhi   != null) { try { startPhi.Dispose();   } catch { } }
                if (endPhi     != null) { try { endPhi.Dispose();     } catch { } }
                if (pointOrder != null) { try { pointOrder.Dispose(); } catch { } }
            }
        }

        // ─── 시각화 XLD ─────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 _vizXld 의 복사본 반환 (호출자 소유 → SetAlignContourXld 에 전달).
        /// XLD 없으면 null.
        /// </summary>
        public HObject GetVisualizationXld() {
            if (_vizXld == null) {
                return null;
            }
            try {
                HObject clone;
                HOperatorSet.CopyObj(_vizXld, out clone, 1, -1);
                return clone;
            }
            catch {
                return null;
            }
        }

        // ─── private 헬퍼 ────────────────────────────────────────────────────────

        /// <summary>
        /// quick-mc1 — 누적점(_rows/_cols)이 피팅된 원에서 얼마나 벗어났는지(잔차)를 산출한다.
        /// 각 점의 중심거리에서 피팅 반경을 뺀 값이 그 점의 오차이고, 오차의 RMS 와 절대 최대값을 반환한다.
        /// fit_circle_contour_xld 를 다시 부르지 않고 이미 가진 값만으로 순수 C# 산술로 계산한다.
        /// </summary>
        private void ComputeFitResiduals(double dCenterRow, double dCenterCol, double dRadius,
            out double dRmsPx, out double dMaxPx) {
            dRmsPx = 0.0;
            dMaxPx = 0.0;

            int nCount = _rows.Count;
            if (nCount <= 0) {
                return;
            }

            double dSumSq = 0.0;
            for (int i = 0; i < nCount; i++) {
                double dDr = _rows[i] - dCenterRow;
                double dDc = _cols[i] - dCenterCol;
                double dDist = Math.Sqrt((dDr * dDr) + (dDc * dDc));
                double dErr = dDist - dRadius;
                double dAbsErr = Math.Abs(dErr);
                dSumSq = dSumSq + (dErr * dErr);
                if (dAbsErr > dMaxPx) {
                    dMaxPx = dAbsErr;
                }
            }
            dRmsPx = Math.Sqrt(dSumSq / nCount);
        }

        private void AppendCrossToViz(double row, double col) {
            try {
                HObject cross;
                HOperatorSet.GenCrossContourXld(out cross,
                    new HTuple(row), new HTuple(col),
                    new HTuple(CROSS_SIZE), new HTuple(0.0));
                MergeToVizXld(cross);
            }
            catch { }
        }

        private void AppendCircleToViz(double row, double col, double rad) {
            try {
                HObject circle;
                HOperatorSet.GenCircleContourXld(out circle,
                    new HTuple(row), new HTuple(col), new HTuple(rad),
                    new HTuple(0.0), new HTuple(Math.PI * 2.0),
                    new HTuple("positive"), new HTuple(1.0));
                MergeToVizXld(circle);
            }
            catch { }
        }

        private void MergeToVizXld(HObject xld) {
            if (xld == null) {
                return;
            }
            if (_vizXld == null) {
                _vizXld = xld;
            }
            else {
                HObject combined = null;
                try {
                    HOperatorSet.ConcatObj(_vizXld, xld, out combined);
                    try { _vizXld.Dispose(); } catch { }
                    try { xld.Dispose();     } catch { }
                    _vizXld = combined;
                    combined = null;
                }
                catch {
                    try { xld.Dispose(); } catch { }
                    if (combined != null) { try { combined.Dispose(); } catch { } }
                }
            }
        }

        private void ClearModel() {
            if (_modelId != null) {
                try { HOperatorSet.ClearShapeModel(_modelId); } catch { }
                try { _modelId.Dispose(); } catch { }
                _modelId = null;
            }
            _modelLoaded = false;
        }

        private void ClearVizXld() {
            if (_vizXld != null) {
                try { _vizXld.Dispose(); } catch { }
                _vizXld = null;
            }
        }
    }
}
