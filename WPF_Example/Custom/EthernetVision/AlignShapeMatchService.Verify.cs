using System;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// ① "보정 후 다시 재봄" — Align 이 낸 보정값대로 부품을 옮겼다고 가정한 이미지를 실제로 만들고
    /// 그 이미지에서 패턴 2개를 다시 매칭해 기준 자세와 얼마나 어긋나 있는지(잔여 offset/theta)를 낸다.
    /// 잔여가 0 에 가까우면 "비전 계산은 맞았다" 는 증거가 된다.
    ///
    /// Run() 은 한 줄도 건드리지 않는다. 이 partial 파일은 추가 기능만 들고 있다.
    /// </summary>
    public partial class AlignShapeMatchService {

        /// <summary>
        /// ① 자체 검출판 — 1차 검출을 직접 수행한다(TryFindPose 4회). 오프라인/단독 호출용.
        /// 실운전 경로는 아래 검출 재사용판을 쓴다.
        /// </summary>
        public AlignVerifyResult RunCorrectedRecheck(
            HImage img, EEthernetVisionMode mode, EBottomAlignSlot slot, out HImage correctedImage) {
            return RunCorrectedRecheck(img, mode, slot, false, 0.0, 0.0, 0.0, 0.0, out correctedImage);
        }

        /// <summary>
        /// ① 검출 재사용판 — Run() 이 이미 낸 1차 검출 좌표를 받아 그 2회를 건너뛴다(TryFindPose 2회).
        ///
        /// 이 메서드는 RunBottomAlign/RunTrayAlign 의 finally 에서 동기로 돈다 = PLC 응답이 나가기 전에
        /// 실행된다. 그래서 Run() 이 방금 같은 이미지에서 한 검출을 한 번 더 하는 낭비를 없앤다.
        /// bHasDetection 이 false 면 기존 자체검출 경로로 폴백한다(회귀 0).
        ///
        /// correctedImage 소유권은 호출자에게 있다. 재매칭 실패 시에도 non-null 일 수 있다
        /// (실패한 보정 이미지가 곧 NG 증거라 버리지 않는다).
        /// </summary>
        public AlignVerifyResult RunCorrectedRecheck(
            HImage img, EEthernetVisionMode mode, EBottomAlignSlot slot,
            bool bHasDetection, double dDet1Row, double dDet1Col, double dDet2Row, double dDet2Col,
            out HImage correctedImage) {

            System.Diagnostics.Stopwatch swVerify = System.Diagnostics.Stopwatch.StartNew();

            correctedImage = null;
            AlignVerifyResult result = new AlignVerifyResult();
            result.Verified = false;
            result.FailReason = "";

            HTuple homMat = null;
            HObject correctedObj = null;

            try {
                if (img == null) {
                    result.FailReason = "입력 이미지 없음";
                    return result;
                }
                if (mode == EEthernetVisionMode.None) {
                    result.FailReason = "모드 None";
                    return result;
                }

                string szShm1 = BuildShmPath(mode, 1, slot);
                string szShm2 = BuildShmPath(mode, 2, slot);
                string szJson = BuildJsonPath(mode, slot);
                bool bPathMissing = string.IsNullOrEmpty(szShm1)
                                 || string.IsNullOrEmpty(szShm2)
                                 || string.IsNullOrEmpty(szJson);
                if (bPathMissing) {
                    result.FailReason = "경로 미설정";
                    return result;
                }

                AlignRefPose refPose = LoadRefPose(szJson);
                if (refPose == null) {
                    result.FailReason = "기준 pose 없음";
                    return result;
                }

                // 1차 검출 — 넘겨받았으면 건너뛴다(택트).
                double f1Row, f1Col, f2Row, f2Col;
                if (bHasDetection == true) {
                    // Run() 이 방금 같은 이미지에서 낸 검출을 그대로 쓴다. TryFindPose 2회를 통째로 아낀다.
                    f1Row = dDet1Row;
                    f1Col = dDet1Col;
                    f2Row = dDet2Row;
                    f2Col = dDet2Col;
                }
                else {
                    // 폴백: 오프라인/단독 호출. 인자는 Run() 과 완전히 동일해야 결과가 비교 가능하다.
                    double f1AngleDeg, f1Score, f2AngleDeg, f2Score;
                    string szFindErr1, szFindErr2;

                    bool bFound1 = _matcher.TryFindPose(
                        img, ENGINE, szShm1,
                        0.0, 0.0, FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                        0.0, MIN_SCORE, 1.0,
                        out f1Row, out f1Col, out f1AngleDeg, out f1Score, out szFindErr1);
                    if (!bFound1) {
                        result.FailReason = "1차 검출 실패[1]";
                        return result;
                    }

                    bool bFound2 = _matcher.TryFindPose(
                        img, ENGINE, szShm2,
                        0.0, 0.0, FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                        0.0, MIN_SCORE, 1.0,
                        out f2Row, out f2Col, out f2AngleDeg, out f2Score, out szFindErr2);
                    if (!bFound2) {
                        result.FailReason = "1차 검출 실패[2]";
                        return result;
                    }
                }

                double dRuntimeBaselineRad = ComputeAngleLx(f1Row, f1Col, f2Row, f2Col);
                if (double.IsNaN(dRuntimeBaselineRad)) {
                    result.FailReason = "baseline 산출 실패";
                    return result;
                }

                // midpoint — Run() Step 4 와 동일 계산
                double dMidFRow = (f1Row + f2Row) / 2.0;
                double dMidFCol = (f1Col + f2Col) / 2.0;
                double dMidRRow = (refPose.Ref1Row + refPose.Ref2Row) / 2.0;
                double dMidRCol = (refPose.Ref1Col + refPose.Ref2Col) / 2.0;

                // 검출 자세(midF + 런타임 baseline) → 기준 자세(midR + RefBaselineRad) 강체변환
                HOperatorSet.VectorAngleToRigid(
                    dMidFRow, dMidFCol, dRuntimeBaselineRad,
                    dMidRRow, dMidRCol, refPose.RefBaselineRad,
                    out homMat);

                // 마지막 인자 AdaptImageSize 는 반드시 "false" — "true" 로 두면 이미지 원점이 이동해
                //  refPose 좌표계가 무효가 되고 잔여값이 통째로 틀린다.
                HOperatorSet.AffineTransImage(img, out correctedObj, homMat, "bilinear", "false");
                correctedImage = new HImage(correctedObj);
                correctedObj.Dispose();
                correctedObj = null;

                // 재매칭 — 1차와 완전히 동일한 인자
                double c1Row, c1Col, c1AngleDeg, c1Score;
                double c2Row, c2Col, c2AngleDeg, c2Score;
                string szReErr1, szReErr2;

                bool bRe1 = _matcher.TryFindPose(
                    correctedImage, ENGINE, szShm1,
                    0.0, 0.0, FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                    0.0, MIN_SCORE, 1.0,
                    out c1Row, out c1Col, out c1AngleDeg, out c1Score, out szReErr1);
                if (!bRe1) {
                    // correctedImage 는 Dispose 하지 않고 out 으로 넘긴다 — 실패한 보정 이미지가 곧 NG 증거다.
                    result.FailReason = "재검출 실패[1]";
                    return result;
                }

                bool bRe2 = _matcher.TryFindPose(
                    correctedImage, ENGINE, szShm2,
                    0.0, 0.0, FULL_SEARCH_LEN, FULL_SEARCH_LEN,
                    0.0, MIN_SCORE, 1.0,
                    out c2Row, out c2Col, out c2AngleDeg, out c2Score, out szReErr2);
                if (!bRe2) {
                    result.FailReason = "재검출 실패[2]";
                    return result;
                }

                double dCheckBaselineRad = ComputeAngleLx(c1Row, c1Col, c2Row, c2Col);
                if (double.IsNaN(dCheckBaselineRad)) {
                    result.FailReason = "재baseline 산출 실패";
                    return result;
                }

                double dMid2Row = (c1Row + c2Row) / 2.0;
                double dMid2Col = (c1Col + c2Col) / 2.0;
                double dRow = dMid2Row - dMidRRow;
                double dCol = dMid2Col - dMidRCol;
                double dResMm = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;

                // ApplyPickerCenterCorrection 은 호출하지 않는다.
                //  ① 은 검출+강체변환 자기일관성만 검증한다. 피커센터 재표현은 부호 규약(PICKER_ROTATION_SIGN)이
                //  아직 UAT 미확정이라 여기 넣으면 미확정 규약을 검증 결과로 오해하게 된다. 그 구간은 ②가 잡는다.
                result.ResidualOffsetXmm = dCol * dResMm;   // Col → X (Run() 규약과 동일)
                result.ResidualOffsetYmm = dRow * dResMm;   // Row → Y
                result.ResidualThetaDeg = (dCheckBaselineRad - refPose.RefBaselineRad) * 180.0 / Math.PI;
                result.ResidualDistanceMm = Math.Sqrt(
                    result.ResidualOffsetXmm * result.ResidualOffsetXmm
                  + result.ResidualOffsetYmm * result.ResidualOffsetYmm);
                result.Score = Math.Min(c1Score, c2Score);
                result.Verified = true;
                return result;
            }
            catch (Exception ex) {
                // TCP 스레드에서 호출된다 — 예외를 밖으로 내보내지 않는다.
                result.Verified = false;
                result.FailReason = "예외: " + ex.Message;
                if (correctedImage != null) {
                    try { correctedImage.Dispose(); } catch { }
                    correctedImage = null;
                }
                return result;
            }
            finally {
                if (correctedObj != null) { try { correctedObj.Dispose(); } catch { } }
                if (homMat != null) { try { homMat.Dispose(); } catch { } }

                swVerify.Stop();
                // elapsed 는 PLC 응답 경로에 얹은 실제 지연이다. 택트 문제가 났을 때 원인을 즉시 가르는 근거.
                try {
                    Logging.PrintLog((int)ELogType.Algorithm,
                        "[ALIGN_VERIFY] recheck ({0}/{1}) verified={2} residual=({3:F4},{4:F4})mm dist={5:F4}mm theta={6:F4} score={7:F3} reused={8} elapsed={9}ms reason={10}",
                        mode, slot, result.Verified,
                        result.ResidualOffsetXmm, result.ResidualOffsetYmm, result.ResidualDistanceMm,
                        result.ResidualThetaDeg, result.Score,
                        bHasDetection, swVerify.ElapsedMilliseconds, result.FailReason);
                }
                catch { }
            }
        }
    }
}
