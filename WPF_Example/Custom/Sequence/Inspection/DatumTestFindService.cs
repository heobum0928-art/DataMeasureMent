using System;
using System.IO;
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Phase 80 D-80-06/D-80-15: 사진 출처 선택 대화상자(AskTestImageSource) 없이 기준점의 티칭 사진
    /// 파일(TeachingImagePath[_Vertical])로 런타임과 같은 기준점 찾기를 돌리는 공용 헬퍼. 비패턴
    /// 기준점도 TryRunSingleDatum 으로 시퀀스 기준점 캐시·국부 기준선을 채운다(RESEARCH.md Pattern 3).
    /// 기존 Test Find 버튼(BtnTestFindDatum_Click)과 그 사진 출처 선택 메서드는 이 서비스와 무관하게
    /// 한 줄도 바뀌지 않는다 — 이 서비스는 대화상자 없는 별도 경로다.
    /// </summary>
    public static class DatumTestFindService
    {
        public const string LOG_TAG = "[TestFind] ";
        public const string ERR_NO_DATUM = "기준점을 찾지 못했습니다";
        public const string ERR_NO_SEQUENCE = "기준점이 속한 검사 시퀀스를 찾지 못했습니다";
        public const string ERR_SEQUENCE_BUSY = "검사가 진행 중입니다 — 끝난 뒤 다시 누르세요";
        public const string ERR_NOT_TAUGHT = "기준점 티칭(라인핏 또는 패턴 모델 생성)이 끝나지 않았습니다";
        public const string ERR_NO_HORIZONTAL = "기준점 가로 사진(TeachingImagePath) 파일이 없습니다";
        public const string ERR_NO_VERTICAL = "기준점 세로 사진(TeachingImagePath_Vertical) 파일이 없습니다";
        public const string ERR_LOAD_FAILED_PREFIX = "기준점 사진을 읽지 못했습니다: ";

        // 기존 Test Find 버튼(MainView.xaml.cs:4486-4495)과 같은 판정 — 라인핏은 IsConfigured+LastTeachSucceeded,
        //  패턴 정렬은 모델 파일(.shm) 존재로 완료를 본다.
        public static bool IsTaughtForTestFind(DatumConfig datum)
        {
            if (datum == null) { return false; }
            bool bTaughtLineFit = datum.IsConfigured && datum.LastTeachSucceeded;
            if (bTaughtLineFit) { return true; }
            if (!datum.IsPatternAlignEnabled) { return false; }
            string szModelPath = InspectionSequence.ResolveDatumModelPath(datum, datum.OwnerName);
            bool bTaughtPattern = !string.IsNullOrEmpty(szModelPath) && File.Exists(szModelPath);
            return bTaughtPattern;
        }

        // 대화상자 없이 티칭 사진 파일로 기준점을 찾는다. 성공하면 시퀀스 기준점 캐시(_datumTransforms)와
        //  국부 기준선(_localRefLines)이 채워진다(TryComposeAlign/TryRunSingleDatum 내부에서 계산).
        //  bHoldForManualRun=true 면 기존 Test Find 버튼과 같은 의미로 다음 수동 RUN 이 이 기준점을 재사용한다.
        public static bool TryRunFromTeachingImages(InspectionSequence seq, DatumConfig datum, bool bHoldForManualRun, out string szError)
        {
            szError = null;
            if (datum == null)
            {
                szError = ERR_NO_DATUM;
                return false;
            }
            if (seq == null)
            {
                szError = ERR_NO_SEQUENCE;
                return false;
            }
            if (seq.State != EContextState.Idle)
            {
                szError = ERR_SEQUENCE_BUSY;
                return false;
            }
            if (!IsTaughtForTestFind(datum))
            {
                szError = ERR_NOT_TAUGHT;
                return false;
            }
            bool bDual = datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage;
            string szPathH = datum.TeachingImagePath;
            bool bNoHorizontal = string.IsNullOrEmpty(szPathH) || !File.Exists(szPathH);
            if (bNoHorizontal)
            {
                szError = ERR_NO_HORIZONTAL;
                return false;
            }
            string szPathV = datum.TeachingImagePath_Vertical;
            bool bNoVertical = bDual && (string.IsNullOrEmpty(szPathV) || !File.Exists(szPathV));
            if (bNoVertical)
            {
                szError = ERR_NO_VERTICAL;
                return false;
            }

            HImage imgH = null;
            HImage imgV = null;
            bool bOk = false;
            try
            {
                imgH = new HImage(szPathH);
                if (bDual)
                {
                    imgV = new HImage(szPathV);
                }
                if (datum.IsPatternAlignEnabled)
                {
                    string szModelPath = InspectionSequence.ResolveDatumModelPath(datum, datum.OwnerName);
                    bOk = seq.TryComposeAlign(datum, imgH, imgV, szModelPath, out szError); // 1장이면 imgV=null → 5-인자 오버로드가 1장 검출로 간다
                }
                else
                {
                    bOk = seq.TryRunSingleDatum(datum, imgH, imgV, out szError);
                }
            }
            catch (Exception ex)
            {
                szError = ERR_LOAD_FAILED_PREFIX + ex.Message;
                bOk = false;
            }
            finally
            {
                if (imgH != null) { try { imgH.Dispose(); } catch { } }
                if (imgV != null) { try { imgV.Dispose(); } catch { } }
            }

            if (bOk && bHoldForManualRun)
            {
                seq.HoldManualDatum(datum.DatumName); // 기존 Test Find 버튼과 같은 의미 — 다음 수동 RUN 이 재사용
            }
            if (bOk)
            {
                // 1장·2장 모두 TeachingImagePath 파일로 찾았으므로 출처를 확인된 것으로 기록한다(Task 1 의
                //  다시 구하기 조건). HoldManualDatum 이 1장 기준점의 기록을 지우므로 반드시 그 뒤에 부른다.
                seq.MarkDatumFoundFromTeachingPhotos(datum.DatumName);
            }

            if (bOk)
            {
                Logging.PrintLog((int)ELogType.Trace, LOG_TAG + seq.Name + " · " + datum.DatumName + " 성공");
            }
            else
            {
                Logging.PrintLog((int)ELogType.Trace, LOG_TAG + seq.Name + " · " + datum.DatumName + " 실패 — " + szError);
            }
            return bOk;
        }
    }
}
