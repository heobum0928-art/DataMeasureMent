//260409 hbk Phase 3: FAI 에지 측정 서비스
using System;
using System.Collections.Generic;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// FAIConfig의 ROI 내에서 Halcon MeasurePos로 에지 페어를 검출하고
    /// 픽셀/mm 거리를 계산하는 서비스.
    /// </summary>
    public class FAIEdgeMeasurementService
    {
        /// <summary>
        /// FAIConfig ROI 영역에서 에지 페어를 측정하여 거리(mm)를 산출한다.
        /// </summary>
        /// <param name="image">측정 대상 HImage (null 불가)</param>
        /// <param name="fai">ROI/에지/캘리브레이션 파라미터</param>
        /// <param name="result">측정 결과 (실패 시 null)</param>
        /// <returns>측정 성공 여부</returns>
        public bool TryMeasure(HImage image, FAIConfig fai, out FAIEdgeMeasurementResult result)
        {
            result = null;

            //260409 hbk null 체크 + ROI 유효성 검사
            if (image == null || fai == null)
            {
                return false;
            }

            if (fai.ROI_Length1 <= 0 || fai.ROI_Length2 <= 0)
            {
                return false;
            }

            HTuple measureHandle = null;

            try
            {
                //260409 hbk 이미지 크기 취득
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                //260409 hbk GenMeasureRectangle2: FAIConfig ROI 파라미터 직접 사용 (ToRoiDefinition 우회)
                HOperatorSet.GenMeasureRectangle2(
                    fai.ROI_Row,
                    fai.ROI_Col,
                    fai.ROI_Phi,
                    fai.ROI_Length1,
                    fai.ROI_Length2,
                    imageWidth,
                    imageHeight,
                    "nearest_neighbor",
                    out measureHandle);

                //260409 hbk MeasurePos: 양방향 에지, 모든 에지 추출
                double sigma = Math.Max(0.4, fai.Sigma);
                double threshold = Math.Max(1, fai.Threshold);

                HTuple rows, cols, amplitude, distance;
                HOperatorSet.MeasurePos(
                    image,
                    measureHandle,
                    sigma,
                    threshold,
                    "all",
                    "all",
                    out rows,
                    out cols,
                    out amplitude,
                    out distance);

                //260409 hbk 에지 개수 검사: 최소 2개 필요
                int edgeCount = rows.TupleLength();
                if (edgeCount < 2)
                {
                    return false;
                }

                //260409 hbk MeasureType에 따라 에지 인덱스 선택
                int idx1, idx2;
                SelectEdgeIndices(fai.MeasureType, edgeCount, out idx1, out idx2);

                //260409 hbk 에지 좌표 추출
                double edge1Row = rows[idx1].D;
                double edge1Col = cols[idx1].D;
                double edge2Row = rows[idx2].D;
                double edge2Col = cols[idx2].D;

                //260409 hbk 픽셀 거리 계산
                double pixelDist = Math.Sqrt(
                    Math.Pow(edge2Row - edge1Row, 2) +
                    Math.Pow(edge2Col - edge1Col, 2));

                //260409 hbk mm 변환: ROI_Phi 기반 축 선택
                double mmDist = CalculateMmDistance(
                    edge1Row, edge1Col, edge2Row, edge2Col,
                    pixelDist, fai.ROI_Phi,
                    fai.PixelResolutionX, fai.PixelResolutionY);

                //260409 hbk 오버레이 생성
                var overlays = BuildOverlays(
                    edge1Row, edge1Col, edge2Row, edge2Col,
                    fai.ROI_Row, fai.ROI_Length1, fai.ROI_Phi);

                //260409 hbk 결과 조립
                result = new FAIEdgeMeasurementResult
                {
                    Edge1Row = edge1Row,
                    Edge1Column = edge1Col,
                    Edge2Row = edge2Row,
                    Edge2Column = edge2Col,
                    DistancePixel = pixelDist,
                    DistanceMm = mmDist,
                    Overlays = overlays
                };

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                //260409 hbk MeasurePos handle 반드시 해제
                if (measureHandle != null)
                {
                    try
                    {
                        HOperatorSet.CloseMeasure(measureHandle);
                    }
                    catch
                    {
                        // CloseMeasure 실패는 무시
                    }
                }
            }
        }

        /// <summary>
        /// MeasureType에 따라 에지 인덱스 쌍을 선택한다.
        /// </summary>
        private static void SelectEdgeIndices(EEdgeMeasureType measureType, int edgeCount, out int idx1, out int idx2)
        {
            switch (measureType)
            {
                case EEdgeMeasureType.FirstToFirst:
                    //260409 hbk FirstToFirst: 첫 번째 에지 쌍 (idx 0, 1)
                    idx1 = 0;
                    idx2 = Math.Min(1, edgeCount - 1);
                    break;

                case EEdgeMeasureType.FirstToLast:
                    //260409 hbk FirstToLast: 첫 번째 ~ 마지막 에지
                    idx1 = 0;
                    idx2 = edgeCount - 1;
                    break;

                case EEdgeMeasureType.LastToFirst:
                    //260409 hbk LastToFirst: 마지막 ~ 첫 번째 (방향 반전)
                    idx1 = edgeCount - 1;
                    idx2 = 0;
                    break;

                case EEdgeMeasureType.LastToLast:
                    //260409 hbk LastToLast: 마지막 에지 쌍 (idx len-2, len-1)
                    idx1 = Math.Max(edgeCount - 2, 0);
                    idx2 = edgeCount - 1;
                    break;

                default:
                    idx1 = 0;
                    idx2 = Math.Min(1, edgeCount - 1);
                    break;
            }
        }

        /// <summary>
        /// ROI_Phi 기반으로 적절한 축의 해상도를 적용하여 mm 거리를 계산한다.
        /// Phi=0 (수평 스캔): X축 해상도 적용
        /// Phi=PI/2 (수직 스캔): Y축 해상도 적용
        /// 기타: 등방성 가정으로 PixelResolutionX 사용
        /// </summary>
        private static double CalculateMmDistance(
            double edge1Row, double edge1Col, double edge2Row, double edge2Col,
            double pixelDist, double roiPhi,
            double pixelResolutionX, double pixelResolutionY)
        {
            //260409 hbk Phi 기반 축 선택
            double absPhi = Math.Abs(roiPhi);
            double piHalf = Math.PI / 2.0;
            double tolerance = 0.1; // ~5.7도 허용 범위

            if (absPhi < tolerance)
            {
                //260409 hbk 수평 스캔 (Phi ~ 0): 에지 간 X축 거리
                return Math.Abs(edge2Col - edge1Col) * pixelResolutionX;
            }
            else if (Math.Abs(absPhi - piHalf) < tolerance)
            {
                //260409 hbk 수직 스캔 (Phi ~ PI/2): 에지 간 Y축 거리
                return Math.Abs(edge2Row - edge1Row) * pixelResolutionY;
            }
            else
            {
                //260409 hbk 일반 각도: 등방성 해상도 가정
                return pixelDist * pixelResolutionX;
            }
        }

        /// <summary>
        /// 에지 위치 마커 + 연결선 오버레이를 생성한다.
        /// </summary>
        private static List<EdgeInspectionOverlay> BuildOverlays(
            double edge1Row, double edge1Col, double edge2Row, double edge2Col,
            double roiCenterRow, double roiLength1, double roiPhi)
        {
            var overlays = new List<EdgeInspectionOverlay>();

            //260409 hbk ROI 범위에 걸친 에지 마커 라인 계산
            double markerHalf = roiLength1 > 0 ? roiLength1 : 20.0;
            double sinPhi = Math.Sin(roiPhi);
            double cosPhi = Math.Cos(roiPhi);

            //260409 hbk 에지 1 마커 (ROI 방향에 수직인 라인)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = edge1Row - markerHalf * cosPhi,
                LineColumn1 = edge1Col + markerHalf * sinPhi,
                LineRow2 = edge1Row + markerHalf * cosPhi,
                LineColumn2 = edge1Col - markerHalf * sinPhi,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = edge1Row, Column = edge1Col }
                }
            });

            //260409 hbk 에지 2 마커
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge2",
                LineRow1 = edge2Row - markerHalf * cosPhi,
                LineColumn1 = edge2Col + markerHalf * sinPhi,
                LineRow2 = edge2Row + markerHalf * cosPhi,
                LineColumn2 = edge2Col - markerHalf * sinPhi,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = edge2Row, Column = edge2Col }
                }
            });

            //260409 hbk 에지 간 연결선 (거리 측정 시각화)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-DistLine",
                LineRow1 = edge1Row,
                LineColumn1 = edge1Col,
                LineRow2 = edge2Row,
                LineColumn2 = edge2Col,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = edge1Row, Column = edge1Col },
                    new EdgeInspectionPoint { Row = edge2Row, Column = edge2Col }
                }
            });

            return overlays;
        }
    }
}
