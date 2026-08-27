//260618 hbk Phase 54 ALIGN-01
// PatternMatchService: HALCON Shape/NCC 패턴매칭 + rigid transform 산출 서비스.
// D-01: per-Datum 엔진 선택형 (Shape/NCC). D-01b: coarse x,y 전용, 정밀 θ는 line-fit.
// D-05: 보정 전 원본 grab 이미지 입력. D-06: reduce_domain 검색영역 제한.
// D-06a: 다운샘플 coarse 매칭 → x,y 스케일 복원. D-09: ref pose 기반 변위.
// 전 메서드: try/catch(return false) + HObject/HImage dispose 규약 준수.
using System;
using System.Collections.Generic;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// HALCON Shape/NCC 패턴매칭을 사용하여 모델 생성·저장·로드·검색영역 제한 find,
    /// ref/cur pose로부터 rigid transform(hom_mat2d)을 산출하는 서비스.
    /// Wave 2 통합단계(Action_FAIMeasurement DatumPhase 확장)가 호출하는 매칭 엔진.
    //260619 hbk Phase 57 #6 leveling 제거 — 폐기된 TryGetLevelingAngle 참조 제거 (θ는 ALIGN 2-패턴 baseline 각도가 담당)
    /// coarse x,y + θ는 ALIGN(패턴매칭 rigid transform)이 담당.
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

        // 마스크를 뺀 뒤 남은 ROI 면적의 최소 허용치(px). 이보다 작으면 create_*_model 이
        //  난해한 HALCON 예외를 내므로, 그 전에 사람이 읽을 수 있는 오류로 바꿔 돌려준다.
        private const double MIN_MASKED_ROI_AREA_PX = 100.0;

        // 캐시 동시성 보호용 락. Top/Side/Bottom 시퀀스가 각자 스레드에서 서로 다른(또는 같은) modelPath 로
        // 동시에 캐시에 접근할 수 있으므로, 딕셔너리 조회/삽입/제거는 전부 이 락 아래에서 수행한다.
        // FindNccModel/FindShapeModel(모델을 조회만 하는 호출) 자체는 이 락 밖에서 실행된다 — 같은 modelId 를
        // 여러 스레드가 동시에 Find 하는 것은 HALCON 문서상 안전한 사용 패턴(모델은 조회 중 read-only)이므로
        // 별도 직렬화는 하지 않는다.
        private static readonly object _cacheLock = new object();

        // modelPath → 로드된 모델 핸들 + Clear 시 어떤 오퍼레이터(NCC/Shape)를 써야 하는지 캐시.
        // static 인 이유: 호출부(TryComposeAlign, BtnTestFindDatum_Click 등)가 매 호출마다
        // new PatternMatchService() 를 새로 만들기 때문에, 인스턴스 필드로는 캐시가 전혀 재사용되지 않는다.
        private static readonly Dictionary<string, CachedModelEntry> _modelCache = new Dictionary<string, CachedModelEntry>();

        // 캐시 1건 = 로드된 modelId + 무효화(재티칭) 시 호출할 Clear 오퍼레이터 식별용 엔진 플래그.
        private sealed class CachedModelEntry
        {
            public HTuple ModelId;
            public bool IsNcc;
        }

        // modelPath 에 해당하는 모델을 캐시에서 재사용(hit)하거나, 없으면 1회만 Read 해서 캐시에 적재한다(lazy load, miss).
        // 반환된 HTuple 의 폐기(Clear) 책임은 더 이상 호출자에게 없다 — 캐시가 소유권을 가지며,
        // TryCreateModel 의 재티칭 무효화(InvalidateCache) 시점에만 Clear 된다.
        private static HTuple GetOrLoadModel(string modelPath, bool isNcc)
        {
            lock (_cacheLock)
            {
                CachedModelEntry entry;
                if (_modelCache.TryGetValue(modelPath, out entry))
                {
                    return entry.ModelId;
                }

                HTuple newModelId;
                if (isNcc)
                {
                    HOperatorSet.ReadNccModel(modelPath, out newModelId);
                }
                else
                {
                    HOperatorSet.ReadShapeModel(modelPath, out newModelId);
                }

                entry = new CachedModelEntry();
                entry.ModelId = newModelId;
                entry.IsNcc = isNcc;
                _modelCache[modelPath] = entry;
                return newModelId;
            }
        }

        // modelPath 로 캐시된 모델이 있으면 Clear 후 캐시에서 제거한다. 재티칭(TryCreateModel 성공) 직후
        // 반드시 호출해야 한다 — 그렇지 않으면 다음 TryFindPose 호출이 재티칭 이전의 stale 모델을 계속
        // 재사용하는 회귀가 발생한다.
        private static void InvalidateCache(string modelPath)
        {
            lock (_cacheLock)
            {
                CachedModelEntry entry;
                if (_modelCache.TryGetValue(modelPath, out entry))
                {
                    try
                    {
                        if (entry.IsNcc)
                        {
                            HOperatorSet.ClearNccModel(entry.ModelId);
                        }
                        else
                        {
                            HOperatorSet.ClearShapeModel(entry.ModelId);
                        }
                    }
                    catch { }
                    _modelCache.Remove(modelPath);
                }
            }
        }

        /// <summary>
        /// template ROI(Rect2)로 reduce_domain 한 영역에서 모델 생성 후 engine 별 파일 저장.
        /// engine "NCC" → create_ncc_model/write_ncc_model, 그 외 → create_shape_model/write_shape_model.
        /// angleExtentDeg = 0 → angle off (작은 range, D-01b). modelPath = 호출부 전달(GetPatternModelFilePath 결과).
        /// SystemSetting.UsePatternBrushMask 가 true 이고 modelPath 옆에 마스크 파일이 있으면 ROI 에서 마스크를 뺀다(D-74-03).
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
            HObject maskRegion = null;   // 브러시 마스크(D-74-02). 옵션 OFF 면 끝까지 null 이다.
            HTuple modelId = null;

            try
            {
                // angleExtentDeg → rad 변환. 0 → extent 0 (각도 off, D-01b)
                double angleExtentRad = angleExtentDeg * Math.PI / 180.0;
                double angleStartRad = -angleExtentRad / 2.0;

                // Step 1: template ROI 생성 → reduce_domain
                HOperatorSet.GenRectangle2(out rect, roiRow, roiCol, roiPhi, roiLen1, roiLen2);

                // 브러시 마스크 적용(D-74-02/03). TryLoadMask 는 옵션 OFF 또는 마스크 파일 없음이면 false 를
                //  돌려주므로, 이 분기에 들어가지 않으면 아래 ReduceDomain 은 기존과 완전히 동일한 rect 를 받는다(회귀 0).
                bool bMaskLoaded = ReringProject.Halcon.Services.PatternMaskService.TryLoadMask(modelPath, out maskRegion);
                if (bMaskLoaded == true && maskRegion != null)
                {
                    HObject maskedRect = null;
                    HOperatorSet.Difference(rect, maskRegion, out maskedRect);
                    try { rect.Dispose(); } catch { }
                    rect = maskedRect;   // finally 가 그대로 Dispose 하도록 같은 변수에 재대입

                    HTuple hvArea, hvCenterRow, hvCenterCol;
                    HOperatorSet.AreaCenter(rect, out hvArea, out hvCenterRow, out hvCenterCol);
                    double dRemainArea = 0.0;
                    if (hvArea.Length > 0)
                    {
                        dRemainArea = hvArea[0].D;
                    }
                    if (dRemainArea < MIN_MASKED_ROI_AREA_PX)
                    {
                        error = "브러시 마스크가 패턴 ROI 를 거의 전부 덮었습니다(남은 면적 "
                              + dRemainArea.ToString("F0") + "px). 마스크를 지우고 다시 시도하세요.";
                        return false;
                    }
                }

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

                // 재티칭 성공 — 같은 modelPath 로 캐시된 이전(stale) 모델이 있으면 즉시 무효화한다.
                // 이걸 빠뜨리면 다음 TryFindPose 호출이 재티칭 이전 모델을 계속 재사용하는 회귀가 발생한다.
                InvalidateCache(modelPath);

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
                if (maskRegion != null) { try { maskRegion.Dispose(); } catch { } }
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
        /// <param name="angleExtentDeg">Find 각도 검색범위(±도). 기본 180 = 전방위(기존 동작).</param>
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
            out string error,
            double angleExtentDeg = 180.0)
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

            // quick-260807: Find 각도 검색범위 = ±angleExtentDeg. 선택적 파라미터인 이유 =
            //  Align(AlignShapeMatchService) 호출부가 인자를 생략해도 기존 전방위(±180°) 검색을 그대로 유지해야 하기 때문.
            double findAngleExtentRad = angleExtentDeg * Math.PI / 180.0;

            try
            {
                bool isNcc = string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase);

                if (isNcc)
                {
                    HOperatorSet.ReadNccModel(modelPath, out modelId);

                    HTuple row, col, angle, score;
                    HOperatorSet.FindNccModel(
                        templateImage, modelId,
                        -findAngleExtentRad, 2.0 * findAngleExtentRad,
                        minScore,
                        1,          // NumMatches=1
                        0.5,        // MaxOverlap
                        "false",     // SubPixel
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
                        -findAngleExtentRad, 2.0 * findAngleExtentRad,
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
        /// <param name="angleExtentDeg">Find 각도 검색범위(±도). 기본 180 = 전방위(기존 동작).</param>
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
            out string error,
            double angleExtentDeg = 180.0)
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

                // quick-260807: ±angleExtentDeg → rad
                double findAngleExtentRad = angleExtentDeg * Math.PI / 180.0;

                if (isNcc)
                {
                    // 캐시 hit 이면 디스크 재읽기 없이 재사용, miss 면 1회 로드 후 캐시 적재(lazy load).
                    // 이 호출 이후 finally 에서 더 이상 Clear 하지 않는다 — 소유권이 캐시로 이전됨.
                    modelId = GetOrLoadModel(modelPath, true);

                    HOperatorSet.FindNccModel(
                        findTarget, modelId,
                        -findAngleExtentRad, 2.0 * findAngleExtentRad,
                        minScore,
                        1,
                        0.5,
                        "false",
                        DEFAULT_NCC_NUM_LEVELS,
                        out rawRow, out rawCol, out rawAngle, out rawScore);
                }
                else
                {
                    // 캐시 hit 이면 디스크 재읽기 없이 재사용, miss 면 1회 로드 후 캐시 적재(lazy load).
                    modelId = GetOrLoadModel(modelPath, false);

                    //260618 hbk find_shape_model 출력 4개 — acuity 제거(CS1501 fix)
                    HOperatorSet.FindShapeModel(
                        findTarget, modelId,
                        -findAngleExtentRad, 2.0 * findAngleExtentRad,
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
                // modelId 는 더 이상 여기서 Clear 하지 않는다 — 캐시(GetOrLoadModel)가 소유권을 가지며,
                // 재티칭(TryCreateModel -> InvalidateCache) 시점에만 Clear 된다. 매 호출마다 read+clear를
                // 반복하던 것이 이번 캐싱 작업(quick-260805-ojq)의 근본 수정 대상이었다.
            }
        }

    }
}
