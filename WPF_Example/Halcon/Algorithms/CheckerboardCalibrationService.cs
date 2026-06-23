//260623 hbk Phase 53: 체커보드 픽셀 캘리브레이션 서비스 (CAL-01)
using System;
using System.Collections.Generic;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// 체커보드(흑백 격자) 이미지를 입력받아 ① saddle_points_sub_pix 로 내부 코너를 서브픽셀 검출,
    /// ② 행/열 인접 간격을 median 통계로 추정, ③ 사용자 입력 칸 크기(mm)로 mm/px 직접 산출(D-07),
    /// ④ 중앙↔외곽 간격 편차%(D-05)를 계산하는 순수 알고리즘 서비스.
    /// 모든 Halcon 호출은 try { ... } catch { return false; } 패턴 (프로젝트 컨벤션).
    /// 순수 수학 연산은 static 메서드로 제공한다.
    /// caltab/set_calibration_data 미사용(D-07 LOCK), undistort 미구현(D-08 LOCK).
    /// </summary>
    public class CheckerboardCalibrationService
    {
        //260623 hbk Phase 53: saddle 검출 / 왜곡 임계 / 최소 코너 수 상수 (A2/A3 UAT 튜닝 대비 노출)
        public const double DefaultSaddleSigma = 1.0;
        public const double DefaultSaddleThreshold = 5.0;
        public const double DistortionWarnThresholdPct = 1.0;
        public const int MinCornerCount = 12;

        /// <summary>
        /// 체커보드 이미지로부터 mm/px 를 산출한다 (기본 saddle/왜곡 임계 사용).
        /// 긴 오버로드에 const 기본값을 위임한다.
        /// </summary>
        public bool TryCalibrate(HImage image, double knownMmPerCell, out CalibrationResult result, out string error)
        {
            //260623 hbk Phase 53: 짧은 오버로드 → 긴 오버로드 위임 (const 기본값)
            return TryCalibrate(image, knownMmPerCell, DefaultSaddleSigma, DefaultSaddleThreshold, DistortionWarnThresholdPct, out result, out error);
        }

        /// <summary>
        /// 체커보드 이미지로부터 mm/px 를 산출한다 (saddle Sigma/Threshold + 왜곡 임계% 노출, A2/A3 UAT 튜닝용).
        /// </summary>
        public bool TryCalibrate(HImage image, double knownMmPerCell, double saddleSigma, double saddleThreshold, double distortionWarnPct, out CalibrationResult result, out string error)
        {
            //260623 hbk Phase 53: 본문은 Task 2 에서 구현
            result = null;
            error = null;
            return false;
        }
    }

    /// <summary>
    /// 캘리브 산출 결과 구조체 — 다음 wave 의 CalibrationWindow 가 소비하는 계약.
    /// MmPerPixel 만 PixelResolution 에 적용(D-02 단일 평균), X/Y 는 리포트 참고값.
    /// </summary>
    //260623 hbk Phase 53: 캘리브 결과 계약 클래스
    public class CalibrationResult
    {
        public double MmPerPixel { get; set; }          // 단일 적용값 (D-02)
        public double MmPerPixelX { get; set; }         // 리포트 참고값
        public double MmPerPixelY { get; set; }         // 리포트 참고값
        public double MeanSpacingPx { get; set; }       // 평균(가로·세로) 격자 간격(px)
        public double CenterOuterDeviationPct { get; set; } // 중앙↔외곽 간격 편차% (D-05)
        public bool IsDistortionWarn { get; set; }      // 편차% 임계 초과 경고 (D-05 게이트)
        public int CornerCount { get; set; }            // 검출된 코너 수
    }
}
