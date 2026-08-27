using System;
using System.Collections.Generic;
using HalconDotNet;
using ReringProject.Halcon.Algorithms;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Services
{
    /// <summary>
    /// Datum 패턴 모델의 경로 조회와 모달 없는 재생성.
    ///
    /// MainView.xaml.cs 는 4,400줄이 넘어 새 로직을 더 넣으면 안 된다.
    /// PatternMaskService 와 같은 폴더에 둬서 "패턴 모델 주변 서비스" 를 한곳에 모은다.
    /// </summary>
    public static class DatumPatternModelRegenService
    {
        /// <summary>
        /// 이 Datum 의 브러시 마스크가 붙을 모델 파일 경로. 패턴 2 가 설정돼 있으면 2개.
        /// 경로 조립을 직접 하지 않고 기존 단일 소스(ResolveDatumModelPath/2)만 쓴다 —
        /// 폴더 규약이 갈리면 모델을 조용히 못 찾는 사고가 난다(Phase 73 전례).
        /// </summary>
        public static IList<string> GetModelPathsForMask(DatumConfig datum)
        {
            List<string> list = new List<string>();
            if (datum == null)
            {
                return list;
            }

            string szPath1 = InspectionSequence.ResolveDatumModelPath(datum, datum.OwnerName);
            if (string.IsNullOrEmpty(szPath1) == false)
            {
                list.Add(szPath1);
            }

            bool bHasPattern2 = (datum.PatternRoi2_Length1 > 0.0) && (datum.PatternRoi2_Length2 > 0.0);
            if (bHasPattern2 == true)
            {
                string szPath2 = InspectionSequence.ResolveDatumModelPath2(datum, datum.OwnerName);
                if (string.IsNullOrEmpty(szPath2) == false)
                {
                    list.Add(szPath2);
                }
            }
            return list;
        }

        /// <summary>
        /// 모달 없이 패턴 1(+ 패턴 2) 모델을 다시 만들고 RefMatch 를 재기록한다(D-74-04).
        /// MainView.InvokeCreatePatternModel 의 계산 흐름을 그대로 따르되 CustomMessageBox 와
        /// Recipe Save 확인만 제거한 것이다. 성공하면 null, 실패하면 사람이 읽을 오류 문자열을 돌려준다.
        /// ※ .shm 은 즉시 디스크에 쓰이지만 RefMatch 는 메모리 값이라 사용자가 Recipe Save 를 해야 영속된다.
        /// </summary>
        public static string RegenerateSilent(DatumConfig datum, HImage templateImage)
        {
            if (datum == null)
            {
                return "Datum 을 먼저 선택하세요";
            }
            if (templateImage == null)
            {
                return "이미지가 없습니다. 먼저 Grab 또는 Load Image 를 수행하세요.";
            }

            // sentinel 0 이면 PatternAngleExtentDeg 가 0 이 되어 0° 전용 모델이 나온다(기존 hotfix 이유).
            datum.EnsurePerRoiDefaults();

            bool bRoiMissing = (datum.PatternRoi_Length1 <= 0.0) || (datum.PatternRoi_Length2 <= 0.0);
            if (bRoiMissing == true)
            {
                return "패턴 ROI(Rect) 를 먼저 그리세요.";
            }

            string szModelPath = InspectionSequence.ResolveDatumModelPath(datum, datum.OwnerName);
            if (string.IsNullOrEmpty(szModelPath))
            {
                return "모델 경로 도출 실패 (레시피/Shot 확인)";
            }

            PatternMatchService svc = new PatternMatchService();

            string szError;
            bool bCreated = svc.TryCreateModel(
                templateImage,
                datum.PatternRoi_Row, datum.PatternRoi_Col, datum.PatternRoi_Phi,
                datum.PatternRoi_Length1, datum.PatternRoi_Length2,
                datum.PatternEngine, datum.PatternAngleExtentDeg,
                szModelPath, out szError);
            if (bCreated == false)
            {
                return szError;
            }

            // downsampleFactor 는 반드시 1.0 — 티칭 시 원본 해상도로 확인하는 기존 규약.
            double dRow, dCol, dAngle, dScore;
            string szRefError;
            bool bRefOk = svc.TryFindPose(
                templateImage, datum.PatternEngine, szModelPath,
                datum.PatternRoi_Row, datum.PatternRoi_Col,
                datum.PatternRoi_Length1, datum.PatternRoi_Length2,
                datum.PatternSearchMarginPx, datum.PatternMinScore, 1.0,
                out dRow, out dCol, out dAngle, out dScore,
                out szRefError, datum.FindAngleExtentDeg);
            if (bRefOk == false)
            {
                return "기준 위치 기록 실패: " + szRefError;
            }
            datum.RefMatchRow = dRow;
            datum.RefMatchCol = dCol;
            datum.RefMatchAngleDeg = dAngle;

            // 패턴 2 실패는 전체 실패로 만들지 않는다 — 기존 InvokeCreatePatternModel 도
            //  단일 패턴 폴백으로 계속 진행한다. 다만 사용자는 알아야 하므로 경고 문자열로 돌려준다.
            string szPattern2Warning = "";
            bool bHasPattern2 = (datum.PatternRoi2_Length1 > 0.0) && (datum.PatternRoi2_Length2 > 0.0);
            if (bHasPattern2 == true)
            {
                szPattern2Warning = RegeneratePattern2(datum, templateImage, svc);
            }

            try { datum.RaisePropertyChanged(string.Empty); } catch { }

            if (string.IsNullOrEmpty(szPattern2Warning))
            {
                return null;
            }
            return "패턴 1 성공, 패턴 2 경고: " + szPattern2Warning;
        }

        /// <summary>패턴 2 모델 재생성 + RefMatch2 기록. 실패 시 경고 문자열, 성공 시 빈 문자열.</summary>
        private static string RegeneratePattern2(DatumConfig datum, HImage templateImage, PatternMatchService svc)
        {
            string szModelPath2 = InspectionSequence.ResolveDatumModelPath2(datum, datum.OwnerName);
            if (string.IsNullOrEmpty(szModelPath2))
            {
                return "모델2 경로 도출 실패";
            }

            string szError2;
            bool bCreated2 = svc.TryCreateModel(
                templateImage,
                datum.PatternRoi2_Row, datum.PatternRoi2_Col, datum.PatternRoi2_Phi,
                datum.PatternRoi2_Length1, datum.PatternRoi2_Length2,
                datum.PatternEngine, datum.PatternAngleExtentDeg,
                szModelPath2, out szError2);
            if (bCreated2 == false)
            {
                return szError2;
            }

            double dRow2, dCol2, dAngle2, dScore2;
            string szRefError2;
            bool bRefOk2 = svc.TryFindPose(
                templateImage, datum.PatternEngine, szModelPath2,
                datum.PatternRoi2_Row, datum.PatternRoi2_Col,
                datum.PatternRoi2_Length1, datum.PatternRoi2_Length2,
                datum.PatternSearchMarginPx, datum.PatternMinScore, 1.0,
                out dRow2, out dCol2, out dAngle2, out dScore2,
                out szRefError2, datum.FindAngleExtentDeg);
            if (bRefOk2 == false)
            {
                return "모델2 기준 위치 기록 실패: " + szRefError2;
            }

            datum.RefMatch2Row = dRow2;
            datum.RefMatch2Col = dCol2;
            return "";
        }
    }
}
