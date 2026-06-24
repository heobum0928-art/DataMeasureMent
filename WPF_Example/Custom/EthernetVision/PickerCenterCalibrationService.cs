//260624 hbk Phase 60 — AV-05 picker center calibration (D-01/D-02 corrected/D-03)
using System;
using System.Collections.Generic;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// D-01: 피커 센터 캘리브레이션 — 상태형 누적 서비스.
    /// 피커가 전용 Cal 지그(깨끗한 원)를 픽업한 채 10°×36스텝 회전 → 각 스텝의 지그 원 중심이
    /// 편심원을 그림 → 그 편심원 중심 = 피커 실제 회전중심.
    /// D-02(정정): 스텝별 중심 = Cal 지그 원을 fit_circle_contour_xld 로 피팅한 중심 (2-패턴 아님).
    ///   → 이 서비스가 직접 에지→원 피팅 (VisionAlgorithmService.TryFindCircle 패턴 독립 복제, 무수정).
    /// fit_circle_contour_xld 2회 사용: ① 스텝별 지그 원, ② 36 지그 중심(편심) → 피커 중심.
    /// 외부(Phase 61 UI / PLC 트리거)가 스텝마다 TryAddStep 호출, ≥MIN_STEPS 후 TryComputePickerCenter.
    /// 비전은 회전을 제어하지 않음. 전 메서드 try-catch → false (D-06, Grabber 무영향).
    /// </summary>
    public class PickerCenterCalibrationService {

        // D-03: 편심원 피팅 최소 누적 점 수. 권장 36(=10°×36스텝). 일부 스텝 미검출 허용하되 안정 피팅 하한.
        private const int MIN_STEPS = 6;

        // D-03: 반경 정상 범위 가드(px). 피팅 결과가 이 범위 밖이면 비정상 → false.
        private const double MIN_RADIUS_PX = 1.0;
        private const double MAX_RADIUS_PX = 100000.0;

        // D-02: 지그 원 에지 추출 기본 파라미터 (TryFindCircle 패턴 — canny sigma / hysteresis threshold).
        private const double EDGE_SIGMA_DEFAULT = 1.0;
        private const int EDGE_THRESHOLD_DEFAULT = 30;

        // 누적 지그 중심점 (절대 row/col)
        private readonly List<double> _rows = new List<double>();
        private readonly List<double> _cols = new List<double>();

        // D-01: 누적 초기화.
        public void Reset() {
            _rows.Clear();
            _cols.Clear();
        }

        /// <summary>누적된 스텝 수.</summary>
        public int StepCount {
            get { return _rows.Count; }
        }

        // D-01/D-02: 한 스텝 이미지에서 검색 ROI(원) 내 Cal 지그 원을 fit_circle_contour_xld 로 피팅 → 중심 누적.
        // 기본 에지 파라미터 오버로드 (Phase 61 UI 가 ROI 만 전달).
        public bool TryAddStep(HImage img,
            double searchRow, double searchCol, double searchRadius,
            out string error) {
            return TryAddStep(img, searchRow, searchCol, searchRadius,
                EDGE_SIGMA_DEFAULT, EDGE_THRESHOLD_DEFAULT, out error);
        }

        // D-02 정정: 스텝별 지그 원 중심 추출 = 이 서비스 내부 에지→FitCircleContourXld (독립 구현).
        public bool TryAddStep(HImage img,
            double searchRow, double searchCol, double searchRadius,
            double sigma, int threshold,
            out string error) {
            error = null;
            if (img == null) {
                error = "img is null";
                return false;
            }

            HObject circleRegion = null;
            HObject imageReduced = null;
            HObject edges = null;
            HTuple fitRow = null;
            HTuple fitCol = null;
            HTuple fitRad = null;
            HTuple startPhi = null;
            HTuple endPhi = null;
            HTuple pointOrder = null;
            try {
                // 검색 ROI(원)로 도메인 축소 — 지그 sweep 커버 (또는 전체 이미지면 큰 radius)
                HOperatorSet.GenCircle(out circleRegion, searchRow, searchCol, searchRadius);
                HOperatorSet.ReduceDomain(img, circleRegion, out imageReduced);

                // 서브픽셀 에지 (TryFindCircle 과 동일 canny + hysteresis)
                HOperatorSet.EdgesSubPix(imageReduced, out edges,
                    "canny", Math.Max(0.4, sigma), Math.Max(1, threshold / 2), Math.Max(2, threshold));

                // ① fit_circle_contour_xld — 스텝별 지그 원
                HOperatorSet.FitCircleContourXld(edges, "atukey", -1, 2, 0, 5, 2,
                    out fitRow, out fitCol, out fitRad, out startPhi, out endPhi, out pointOrder);
                if (fitRow.Length == 0) {
                    error = "지그 원 피팅 실패 (에지/원 없음)";
                    return false;
                }
                // WR-02 fix //260624 hbk: 복수 원 검출 시 가시성 경고 — [0] 을 계속 사용.
                if (fitRow.Length > 1) {
                    Logging.PrintLog((int)ELogType.Error,
                        "[PICKER_CAL] TryAddStep: 복수 원 검출({0}) — [0] 사용", fitRow.Length);
                }

                double jigRow = fitRow[0].D;
                double jigCol = fitCol[0].D;
                _rows.Add(jigRow);
                _cols.Add(jigCol);

                Logging.PrintLog((int)ELogType.Camera,
                    "[PICKER_CAL] step {0} jig=({1:F2},{2:F2}) r={3:F2}",
                    _rows.Count, jigRow, jigCol, fitRad[0].D);
                return true;
            }
            catch (Exception ex) {
                error = "TryAddStep exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[PICKER_CAL] TryAddStep exception: {0}", ex.Message);
                return false;
            }
            finally {
                if (circleRegion != null) { try { circleRegion.Dispose(); } catch { } }
                if (imageReduced != null) { try { imageReduced.Dispose(); } catch { } }
                if (edges != null) { try { edges.Dispose(); } catch { } }
                if (fitRow != null) { try { fitRow.Dispose(); } catch { } }
                if (fitCol != null) { try { fitCol.Dispose(); } catch { } }
                if (fitRad != null) { try { fitRad.Dispose(); } catch { } }
                if (startPhi != null) { try { startPhi.Dispose(); } catch { } }
                if (endPhi != null) { try { endPhi.Dispose(); } catch { } }
                if (pointOrder != null) { try { pointOrder.Dispose(); } catch { } }
            }
        }

        // D-01/D-03: 누적 지그 중심들로 ② fit_circle_contour_xld atukey → 피커센터(row,col)+radius + SystemSetting 저장.
        public bool TryComputePickerCenter(out double row, out double col, out double radius, out string error) {
            row = 0.0;
            col = 0.0;
            radius = 0.0;
            error = null;

            if (_rows.Count < MIN_STEPS) {
                error = "누적 점 부족: " + _rows.Count + " < " + MIN_STEPS;
                return false;
            }

            HObject contour = null;
            HTuple allRows = null;
            HTuple allCols = null;
            HTuple fitRow = null;
            HTuple fitCol = null;
            HTuple fitRad = null;
            HTuple startPhi = null;
            HTuple endPhi = null;
            HTuple pointOrder = null;
            try {
                allRows = new HTuple(_rows.ToArray());
                allCols = new HTuple(_cols.ToArray());
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                // ② fit_circle_contour_xld — 누적 지그 중심(편심원) → 피커 회전중심
                HOperatorSet.FitCircleContourXld(contour, "atukey", -1, 2, 0, 5, 2,
                    out fitRow, out fitCol, out fitRad, out startPhi, out endPhi, out pointOrder);

                if (fitRow.Length == 0) {
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

                // D-04: 피커센터 저장 (머신 단위 HW 캘 → SystemSetting [ETHERNET_VISION])
                // WR-01 fix //260624 hbk: Save() 성공 후에만 out 파라미터 할당.
                // Save() 예외 시 catch 가 false 반환 — out 은 안전 기본값(0) 유지.
                SystemSetting.Handle.PickerCenterRow = fittedRow;
                SystemSetting.Handle.PickerCenterCol = fittedCol;
                SystemSetting.Handle.Save();

                row = fittedRow;
                col = fittedCol;
                radius = fittedRad;

                Logging.PrintLog((int)ELogType.Camera,
                    "[PICKER_CAL] computed: center=({0:F2},{1:F2}) r={2:F2} from {3} steps",
                    fittedRow, fittedCol, fittedRad, _rows.Count);
                return true;
            }
            catch (Exception ex) {
                error = "TryComputePickerCenter exception: " + ex.Message;
                Logging.PrintLog((int)ELogType.Error, "[PICKER_CAL] compute exception: {0}", ex.Message);
                return false;
            }
            finally {
                if (contour != null) { try { contour.Dispose(); } catch { } }
                if (allRows != null) { try { allRows.Dispose(); } catch { } }
                if (allCols != null) { try { allCols.Dispose(); } catch { } }
                if (fitRow != null) { try { fitRow.Dispose(); } catch { } }
                if (fitCol != null) { try { fitCol.Dispose(); } catch { } }
                if (fitRad != null) { try { fitRad.Dispose(); } catch { } }
                if (startPhi != null) { try { startPhi.Dispose(); } catch { } }
                if (endPhi != null) { try { endPhi.Dispose(); } catch { } }
                if (pointOrder != null) { try { pointOrder.Dispose(); } catch { } }
            }
        }
    }
}
