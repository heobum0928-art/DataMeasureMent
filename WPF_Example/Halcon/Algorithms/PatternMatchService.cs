//260618 hbk Phase 54 ALIGN-01
// PatternMatchService: HALCON Shape/NCC 패턴매칭 + rigid transform 산출 서비스.
// D-01: per-Datum 엔진 선택형 (Shape/NCC). D-01b: coarse x,y 전용, 정밀 θ는 line-fit.
// D-05: 보정 전 원본 grab 이미지 입력. D-06: reduce_domain 검색영역 제한.
// D-06a: 다운샘플 coarse 매칭 → x,y 스케일 복원. D-09: ref pose 기반 변위.
// 전 메서드: try/catch(return false) + HObject/HImage dispose 규약 준수.
using System;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// HALCON Shape/NCC 패턴매칭을 사용하여 모델 생성·저장·로드·검색영역 제한 find,
    /// ref/cur pose로부터 rigid transform(hom_mat2d)을 산출하는 서비스.
    /// Wave 2 통합단계(Action_FAIMeasurement DatumPhase 확장)가 호출하는 매칭 엔진.
    /// coarse x,y 전용 — 정밀 θ는 DatumFindingService.TryGetLevelingAngle(line-fit)이 담당.
    /// </summary>
    public class PatternMatchService
    {
        // Shape 모델 파일 확장자 (HALCON write_shape_model)
        public const string EXTENSION_SHAPE_MODEL = ".shm";

        // NCC 모델 파일 확장자 (HALCON write_ncc_model)
        public const string EXTENSION_NCC_MODEL = ".ncm";

        // 기본 다운샘플 비율 (D-06a). 1/2 해상도에서 coarse 매칭.
        // 152MP 등 고해상도 tact 대응. 호출부가 파라미터로 오버라이드 가능.
        public const double DEFAULT_DOWNSAMPLE_FACTOR = 2.0;

        // Shape 모델 기본 NumLevels (피라미드 레벨, 'auto' 대신 4로 충분히 coarse)
        private const int DEFAULT_NUM_LEVELS = 4;

        // 기본 Greediness (높을수록 빠르나 정밀도↓ — coarse find이므로 0.9 적용)
        private const double DEFAULT_GREEDINESS = 0.9;

        // NCC 기본 NumLevels
        private const int DEFAULT_NCC_NUM_LEVELS = 4;

        /// <summary>
        /// template ROI(Rect2)로 reduce_domain 한 영역에서 모델 생성 후 engine 별 파일 저장.
        /// engine "NCC" → create_ncc_model/write_ncc_model, 그 외 → create_shape_model/write_shape_model.
        /// angleExtentDeg = 0 → angle off (작은 range, D-01b). modelPath = 호출부 전달(GetPatternModelFilePath 결과).
        /// </summary>
        /// <param name="templateImage">티칭 이미지</param>
        /// <param name="roiRow">template ROI 중심 row</param>
        /// <param name="roiCol">template ROI 중심 col</param>
        /// <param name="roiPhi">template ROI 각도(rad)</param>
        /// <param name="roiLen1">template ROI half-length1(px)</param>
        /// <param name="roiLen2">template ROI half-length2(px)</param>
        /// <param name="engine">"NCC" 또는 "Shape"(기본)</param>
        /// <param name="angleExtentDeg">허용 각도 범위(deg). 0이면 0rad extent.</param>
        /// <param name="modelPath">저장 경로(.shm / .ncm)</param>
        /// <param name="error">오류 메시지(성공 시 null)</param>
        /// <returns>성공 여부</returns>
        public bool TryCreateModel(
            HImage templateImage,
            double roiRow, double roiCol, double roiPhi,
            double roiLen1, double roiLen2,
            string engine,
            double angleExtentDeg,
            string modelPath,
            out string error)
        {
            error = null;

            if (templateImage == null)
            {
                error = "templateImage is null";
                return false;
            }
            if (string.IsNullOrEmpty(modelPath))
            {
                error = "modelPath is null or empty";
                return false;
            }

            HObject rect = null;
            HObject reducedImage = null;
            HTuple modelId = null;

            try
            {
                // angleExtentDeg → rad 변환. 0 → extent 0 (각도 off, D-01b)
                double angleExtentRad = angleExtentDeg * Math.PI / 180.0;
                double angleStartRad = -angleExtentRad / 2.0;

                // Step 1: template ROI 생성 → reduce_domain
                HOperatorSet.GenRectangle2(out rect, roiRow, roiCol, roiPhi, roiLen1, roiLen2);
                HOperatorSet.ReduceDomain(templateImage, rect, out reducedImage);

                bool isNcc = string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase);

                if (isNcc)
                {
                    // NCC 모델 생성 (defocus 강, 회전 약 → 작은 angle range)
                    HOperatorSet.CreateNccModel(
                        reducedImage,
                        DEFAULT_NCC_NUM_LEVELS,
                        angleStartRad, angleExtentRad,
                        "auto",
                        "use_polarity",
                        out modelId);

                    // NCC 모델 파일 저장
                    HOperatorSet.WriteNccModel(modelId, modelPath);
                }
                else
                {
                    // Shape 모델 생성 (회전/조명/클러터 강, defocus 약)
                    // AngleStep='auto', Optimization='auto', Metric='use_polarity', Contrast='auto', MinContrast=10
                    HOperatorSet.CreateShapeModel(
                        reducedImage,
                        DEFAULT_NUM_LEVELS,
                        angleStartRad, angleExtentRad,
                        "auto",
                        "auto",
                        "use_polarity",
                        "auto",
                        10,
                        out modelId);

                    // Shape 모델 파일 저장
                    HOperatorSet.WriteShapeModel(modelId, modelPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (rect != null) { try { rect.Dispose(); } catch { } }
                if (reducedImage != null) { try { reducedImage.Dispose(); } catch { } }
                if (modelId != null)
                {
                    try
                    {
                        if (string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase))
                        {
                            HOperatorSet.ClearNccModel(modelId);
                        }
                        else
                        {
                            HOperatorSet.ClearShapeModel(modelId);
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 방금 생성한 모델로 templateImage 자체에서 find → ref pose 반환(D-09).
        /// 티칭 시 1회 호출. 런타임과 동일 연산이라 부호/좌표계 일관성 보장.
        /// </summary>
        /// <param name="templateImage">티칭 이미지</param>
        /// <param name="engine">"NCC" 또는 "Shape"</param>
        /// <param name="modelPath">모델 파일 경로</param>
        /// <param name="minScore">최소 매칭 점수(0~1)</param>
        /// <param name="refRow">ref pose row (출력)</param>
        /// <param name="refCol">ref pose col (출력)</param>
        /// <param name="refAngleDeg">ref pose 각도(deg, 출력)</param>
        /// <param name="refScore">매칭 점수 (출력)</param>
        /// <param name="error">오류 메시지(성공 시 null)</param>
        /// <returns>성공 여부</returns>
        public bool TryFindRefPose(
            HImage templateImage,
            string engine,
            string modelPath,
            double minScore,
            out double refRow,
            out double refCol,
            out double refAngleDeg,
            out double refScore,
            out string error)
        {
            refRow = refCol = refAngleDeg = refScore = 0;
            error = null;

            if (templateImage == null)
            {
                error = "templateImage is null";
                return false;
            }
            if (string.IsNullOrEmpty(modelPath))
            {
                error = "modelPath is null or empty";
                return false;
            }

            HTuple modelId = null;

            try
            {
                bool isNcc = string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase);

                if (isNcc)
                {
                    HOperatorSet.ReadNccModel(modelPath, out modelId);

                    HTuple row, col, angle, score;
                    HOperatorSet.FindNccModel(
                        templateImage, modelId,
                        -Math.PI, 2.0 * Math.PI,
                        minScore,
                        1,          // NumMatches=1
                        0.5,        // MaxOverlap
                        "true",     // SubPixel
                        DEFAULT_NCC_NUM_LEVELS,
                        out row, out col, out angle, out score);

                    if (row.TupleLength() == 0 || (score.TupleLength() > 0 && score[0].D < minScore))
                    {
                        error = "NCC ref find: no match above minScore=" + minScore.ToString("F3");
                        return false;
                    }

                    refRow = row[0].D;
                    refCol = col[0].D;
                    refAngleDeg = angle[0].D * 180.0 / Math.PI;
                    refScore = score.TupleLength() > 0 ? score[0].D : 0.0;
                }
                else
                {
                    HOperatorSet.ReadShapeModel(modelPath, out modelId);

                    //260618 hbk find_shape_model 출력은 Row,Column,Angle,Score 4개뿐 — acuity(5번째) 제거(CS1501 fix)
                    HTuple row, col, angle, score;
                    HOperatorSet.FindShapeModel(
                        templateImage, modelId,
                        -Math.PI, 2.0 * Math.PI,
                        minScore,
                        1,          // NumMatches=1
                        0.5,        // MaxOverlap
                        "least_squares",
                        DEFAULT_NUM_LEVELS,
                        DEFAULT_GREEDINESS,
                        out row, out col, out angle, out score);

                    if (row.TupleLength() == 0 || (score.TupleLength() > 0 && score[0].D < minScore))
                    {
                        error = "Shape ref find: no match above minScore=" + minScore.ToString("F3");
                        return false;
                    }

                    refRow = row[0].D;
                    refCol = col[0].D;
                    // find_shape_model angle = 반시계+rad (§5 부호 주의)
                    refAngleDeg = angle[0].D * 180.0 / Math.PI;
                    refScore = score.TupleLength() > 0 ? score[0].D : 0.0;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (modelId != null)
                {
                    try
                    {
                        if (string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase))
                        {
                            HOperatorSet.ClearNccModel(modelId);
                        }
                        else
                        {
                            HOperatorSet.ClearShapeModel(modelId);
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 모델 로드 후 검색영역(template ROI ± marginPx)으로 reduce_domain, 다운샘플에서 coarse find → x,y 획득 후 스케일 복원.
        /// minScore 미달 → false (호출부가 MarkDatumFailed, D-10). angle은 거칠어 측정에 쓰지 않음(정밀 θ는 line-fit, D-01b) — out으로 반환만.
        /// </summary>
        /// <param name="runtimeImage">보정 전 원본 grab 이미지(D-05)</param>
        /// <param name="engine">"NCC" 또는 "Shape"</param>
        /// <param name="modelPath">모델 파일 경로</param>
        /// <param name="roiRow">template ROI 중심 row</param>
        /// <param name="roiCol">template ROI 중심 col</param>
        /// <param name="roiLen1">template ROI half-length1(px)</param>
        /// <param name="roiLen2">template ROI half-length2(px)</param>
        /// <param name="marginPx">검색영역 확장 margin(px, D-06)</param>
        /// <param name="minScore">최소 매칭 점수</param>
        /// <param name="downsampleFactor">다운샘플 비율(D-06a). 1이하=원본. 기본 DEFAULT_DOWNSAMPLE_FACTOR=2.0</param>
        /// <param name="curRow">검출된 매칭 row (출력)</param>
        /// <param name="curCol">검출된 매칭 col (출력)</param>
        /// <param name="curAngleDeg">검출된 매칭 각도(deg, 거침 — 측정 미사용, 출력)</param>
        /// <param name="curScore">매칭 점수 (출력)</param>
        /// <param name="error">오류 메시지(성공 시 null)</param>
        /// <returns>성공 여부</returns>
        public bool TryFindPose(
            HImage runtimeImage,
            string engine,
            string modelPath,
            double roiRow, double roiCol,
            double roiLen1, double roiLen2,
            double marginPx,
            double minScore,
            double downsampleFactor,
            out double curRow,
            out double curCol,
            out double curAngleDeg,
            out double curScore,
            out string error)
        {
            curRow = curCol = curAngleDeg = curScore = 0;
            error = null;

            if (runtimeImage == null)
            {
                error = "runtimeImage is null";
                return false;
            }
            if (string.IsNullOrEmpty(modelPath))
            {
                error = "modelPath is null or empty";
                return false;
            }

            HObject searchRect = null;
            HObject reducedImage = null;
            HObject scaledImage = null;
            HTuple modelId = null;

            try
            {
                bool isNcc = string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase);

                //260618 hbk Phase 54 ALIGN-01 (CO-54-04): 검색영역 = ROI 중심 ± (len + margin) 으로 제한.
                //  전체 이미지 검색은 반복 feature 부품에서 false match(엉뚱한 instance) 유발 → margin 으로 catch 범위 한정.
                //  margin 은 "예상 최대 이동량 + 여유" 로 사용자 튜닝(PatternSearchMarginPx). 너무 크면 false match, 작으면 no match.
                HTuple imgW, imgH;
                HOperatorSet.GetImageSize(runtimeImage, out imgW, out imgH);
                double searchLen1 = roiLen1 + marginPx;
                double searchLen2 = roiLen2 + marginPx;
                double sr1 = roiRow - searchLen2; if (sr1 < 0.0) sr1 = 0.0;
                double sc1 = roiCol - searchLen1; if (sc1 < 0.0) sc1 = 0.0;
                double sr2 = roiRow + searchLen2; if (sr2 > imgH.D - 1.0) sr2 = imgH.D - 1.0;
                double sc2 = roiCol + searchLen1; if (sc2 > imgW.D - 1.0) sc2 = imgW.D - 1.0;
                HOperatorSet.GenRectangle1(out searchRect, sr1, sc1, sr2, sc2);
                HOperatorSet.ReduceDomain(runtimeImage, searchRect, out reducedImage);

                // 다운샘플 처리 (D-06a): downsampleFactor>1 이면 zoom_image_factor(1/factor)로 축소
                HObject findTarget = null;
                double scale = 1.0;
                bool usedZoom = false;

                if (downsampleFactor > 1.0)
                {
                    scale = 1.0 / downsampleFactor;
                    HOperatorSet.ZoomImageFactor(reducedImage, out scaledImage, scale, scale, "constant");
                    findTarget = scaledImage;
                    usedZoom = true;
                }
                else
                {
                    findTarget = reducedImage;
                }

                // 모델 로드 및 find
                HTuple rawRow, rawCol, rawAngle, rawScore;

                if (isNcc)
                {
                    HOperatorSet.ReadNccModel(modelPath, out modelId);

                    HOperatorSet.FindNccModel(
                        findTarget, modelId,
                        -Math.PI, 2.0 * Math.PI,
                        minScore,
                        1,
                        0.5,
                        "true",
                        DEFAULT_NCC_NUM_LEVELS,
                        out rawRow, out rawCol, out rawAngle, out rawScore);
                }
                else
                {
                    HOperatorSet.ReadShapeModel(modelPath, out modelId);

                    //260618 hbk find_shape_model 출력 4개 — acuity 제거(CS1501 fix)
                    HOperatorSet.FindShapeModel(
                        findTarget, modelId,
                        -Math.PI, 2.0 * Math.PI,
                        minScore,
                        1,
                        0.5,
                        "least_squares",
                        DEFAULT_NUM_LEVELS,
                        DEFAULT_GREEDINESS,
                        out rawRow, out rawCol, out rawAngle, out rawScore);
                }

                // 결과 검증
                if (rawRow.TupleLength() == 0)
                {
                    error = "no match found (empty result)";
                    return false;
                }

                double matchScore = rawScore.TupleLength() > 0 ? rawScore[0].D : 0.0;
                if (matchScore < minScore)
                {
                    error = "match score " + matchScore.ToString("F3") + " < minScore " + minScore.ToString("F3");
                    return false;
                }

                // 다운샘플 좌표 → 원본 스케일 복원
                curRow = rawRow[0].D;
                curCol = rawCol[0].D;
                if (usedZoom && scale > 0)
                {
                    curRow = curRow / scale;
                    curCol = curCol / scale;
                }

                // angle: 거칠어 측정에 쓰지 않음. find_shape_model angle = 반시계+rad (§5)
                curAngleDeg = rawAngle[0].D * 180.0 / Math.PI;
                curScore = matchScore;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (searchRect != null) { try { searchRect.Dispose(); } catch { } }
                if (reducedImage != null) { try { reducedImage.Dispose(); } catch { } }
                if (scaledImage != null) { try { scaledImage.Dispose(); } catch { } }
                if (modelId != null)
                {
                    try
                    {
                        if (string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase))
                        {
                            HOperatorSet.ClearNccModel(modelId);
                        }
                        else
                        {
                            HOperatorSet.ClearShapeModel(modelId);
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// ref pose - cur pose 변위로 rigid 행렬 산출. θ는 line-fit으로 따로 주입(D-02 ③, §3).
        /// vector_angle_to_rigid 사용: ref 위치에서 cur 위치로, 회전은 line-fit 정밀 thetaRad 적용.
        /// 부호 규약: curRow > refRow → 자재가 +row 방향(아래)으로 이동. transform 적용 시 ROI도 그만큼 이동.
        /// catch 시 identity transform + return false (lenient identity 폴백, §5).
        /// </summary>
        /// <param name="refRow">ref pose row (티칭 시 TryFindRefPose 결과)</param>
        /// <param name="refCol">ref pose col</param>
        /// <param name="refAngleDeg">ref pose 각도(deg, 티칭 시 결과)</param>
        /// <param name="curRow">cur pose row (런타임 TryFindPose 결과)</param>
        /// <param name="curCol">cur pose col</param>
        /// <param name="thetaRad">line-fit 정밀 θ(rad). 호출부가 별도 산출 후 전달(D-02 ③)</param>
        /// <param name="transform">산출된 hom_mat2d rigid transform (출력)</param>
        /// <param name="error">오류 메시지(성공 시 null)</param>
        /// <returns>성공 여부</returns>
        public bool TryBuildAlignRigid(
            double refRow, double refCol, double refAngleDeg,
            double curRow, double curCol, double thetaRad,
            out HTuple transform,
            out string error)
        {
            error = null;
            // lenient identity 초기화 (catch 시 반환 안전)
            HOperatorSet.HomMat2dIdentity(out transform);

            try
            {
                // ref pose 각도(deg) → rad 변환
                double refAngleRad = refAngleDeg * Math.PI / 180.0;

                // §3 권고: vector_angle_to_rigid(refRow, refCol, refAngleRad, curRow, curCol, curAngleRad)
                // ref pose (티칭) 에서 cur pose (런타임) 로 rigid transform 산출.
                // thetaRad = line-fit 정밀 θ를 cur 각도로 주입 (매칭 angle은 거칠어 미사용, D-01b).
                // 부호: D-09 — ref − cur 방향, 즉 dRow = refRow - curRow(자재 이동 역방향으로 ROI 보정).
                // vector_angle_to_rigid 내부 규약: from=(refRow,refCol,refAngle) → to=(curRow,curCol,curAngle).
                HOperatorSet.VectorAngleToRigid(
                    refRow, refCol, refAngleRad,
                    curRow, curCol, thetaRad,
                    out transform);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                // lenient identity 폴백 — 매칭 실패가 시퀀스 abort 하지 않음 (D-10, T-54-05)
                try { HOperatorSet.HomMat2dIdentity(out transform); } catch { }
                return false;
            }
        }
    }
}
