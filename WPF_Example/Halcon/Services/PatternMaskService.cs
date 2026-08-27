using System;
using System.IO;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Halcon.Services
{
    /// <summary>
    /// 패턴 모델(.shm/.ncm) 생성 시 제외할 영역(브러시 마스크)의 저장/로드 담당(D-74-02).
    ///
    /// 마스크 경로는 <b>호출부가 이미 정규 헬퍼로 산출해 넘긴 modelPath 문자열에서만 파생</b>한다.
    /// 폴더 규약을 새로 만들지 않기 때문에 Datum(RecipeFiles.GetPatternModelFilePath) 이든
    /// Align(AlignShapeMatchService.BuildShmPath) 이든 무조건 모델과 같은 폴더에 떨어진다.
    /// Phase 73 에서 폴더 규약이 갈려 .shm 을 조용히 못 찾을 뻔한 사고가 있었다.
    ///
    /// ResolveMaskPath / TryLoadMask 는 <b>디렉터리를 절대 만들지 않는다.</b>
    /// Directory.CreateDirectory 는 TrySaveMask 안에서만 허용한다.
    /// </summary>
    public static class PatternMaskService
    {
        // HALCON write_region 기본 포맷(HOBJ). '.reg' 는 HALCON 12 이전 호환용 legacy 라 쓰지 않는다.
        public const string EXTENSION_PATTERN_MASK = ".mask.hobj";

        /// <summary>
        /// 모델 경로에서 마스크 경로를 파생한다. X.shm 과 X.ncm 은 같은 마스크 X.mask.hobj 를 공유한다
        /// (마스크는 ROI 에 속하지 엔진에 속하지 않는다). 실패 시 null.
        /// </summary>
        public static string ResolveMaskPath(string szModelPath)
        {
            bool bEmpty = string.IsNullOrEmpty(szModelPath);
            if (bEmpty == true)
            {
                return null;
            }
            string szDir = Path.GetDirectoryName(szModelPath);
            string szBase = Path.GetFileNameWithoutExtension(szModelPath);
            bool bBadPath = string.IsNullOrEmpty(szDir) || string.IsNullOrEmpty(szBase);
            if (bBadPath == true)
            {
                return null;
            }
            return Path.Combine(szDir, szBase + EXTENSION_PATTERN_MASK);
        }

        /// <summary>옵션 토글 상태.</summary>
        public static bool IsMaskEnabled()
        {
            return SystemSetting.Handle.UsePatternBrushMask;
        }

        /// <summary>
        /// 마스크 파일 존재 여부. <b>옵션 토글을 보지 않는다</b> —
        /// "마스크 있음/없음" 표시는 토글과 무관해야 사용자가 상태를 안다.
        /// </summary>
        public static bool HasMask(string szModelPath)
        {
            string szMaskPath = ResolveMaskPath(szModelPath);
            if (string.IsNullOrEmpty(szMaskPath))
            {
                return false;
            }
            return File.Exists(szMaskPath);
        }

        /// <summary>
        /// 마스크를 읽는다. true 면 호출자가 maskRegion 을 Dispose 할 책임을 진다.
        /// 옵션이 꺼져 있으면 파일 존재 여부조차 보지 않고 즉시 false.
        /// </summary>
        public static bool TryLoadMask(string szModelPath, out HObject maskRegion)
        {
            maskRegion = null;

            // 게이트 1: 옵션 OFF 면 파일 존재 여부조차 보지 않고 즉시 false.
            //  이 분기가 "토글 OFF = 기존 경로 그대로" 를 보장한다.
            bool bEnabled = SystemSetting.Handle.UsePatternBrushMask;
            if (bEnabled == false)
            {
                return false;
            }

            string szMaskPath = ResolveMaskPath(szModelPath);
            if (string.IsNullOrEmpty(szMaskPath))
            {
                return false;
            }
            bool bExists = File.Exists(szMaskPath);
            if (bExists == false)
            {
                return false;
            }

            try
            {
                HObject loaded;
                HOperatorSet.ReadRegion(out loaded, szMaskPath);

                HTuple hvCount;
                HOperatorSet.CountObj(loaded, out hvCount);
                bool bEmptyRegion = true;
                if (hvCount.Length > 0)
                {
                    if (hvCount[0].I > 0)
                    {
                        bEmptyRegion = false;
                    }
                }
                if (bEmptyRegion == true)
                {
                    try { loaded.Dispose(); } catch { }
                    return false;
                }

                maskRegion = loaded;
                Logging.PrintLog((int)ELogType.Trace, "[PatternMask] 마스크 로드: {0}", szMaskPath);
                return true;
            }
            catch (Exception ex)
            {
                // 마스크를 못 읽으면 마스크 없이 진행한다 — 모델 생성 자체를 막지 않는다.
                Logging.PrintErrLog((int)ELogType.Error, "[PatternMask] 마스크 로드 실패(마스크 없이 진행): " + szMaskPath + " — " + ex.Message);
                maskRegion = null;
                return false;
            }
        }

        /// <summary>마스크를 모델 파일 옆에 저장한다.</summary>
        public static bool TrySaveMask(string szModelPath, HObject maskRegion, out string szError)
        {
            szError = null;
            string szMaskPath = ResolveMaskPath(szModelPath);
            if (string.IsNullOrEmpty(szMaskPath))
            {
                szError = "마스크 경로 산출 실패 (modelPath 확인)";
                return false;
            }
            if (maskRegion == null)
            {
                szError = "maskRegion is null";
                return false;
            }
            try
            {
                // 여기서만 CreateDirectory 를 허용한다. 이 폴더는 모델 경로 헬퍼가 이미 만든 '모델 폴더'이며
                //  새 규약 폴더를 만드는 것이 아니다(Phase 73 의 조용한 모델 미탐지 사고 재발 방지).
                string szDir = Path.GetDirectoryName(szMaskPath);
                if (Directory.Exists(szDir) == false)
                {
                    Directory.CreateDirectory(szDir);
                }
                HOperatorSet.WriteRegion(maskRegion, szMaskPath);
                Logging.PrintLog((int)ELogType.Trace, "[PatternMask] 마스크 저장: {0}", szMaskPath);
                return true;
            }
            catch (Exception ex)
            {
                szError = ex.Message;
                Logging.PrintErrLog((int)ELogType.Error, "[PatternMask] 마스크 저장 실패: " + szMaskPath + " — " + ex.Message);
                return false;
            }
        }

        /// <summary>마스크 파일 삭제. 고아 마스크 정리 진입점.</summary>
        public static bool DeleteMask(string szModelPath)
        {
            string szMaskPath = ResolveMaskPath(szModelPath);
            if (string.IsNullOrEmpty(szMaskPath))
            {
                return false;
            }
            bool bExists = File.Exists(szMaskPath);
            if (bExists == false)
            {
                return false;
            }
            try
            {
                File.Delete(szMaskPath);
                Logging.PrintLog((int)ELogType.Trace, "[PatternMask] 마스크 삭제: {0}", szMaskPath);
                return true;
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[PatternMask] 마스크 삭제 실패: " + szMaskPath + " — " + ex.Message);
                return false;
            }
        }
    }
}
