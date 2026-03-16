using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace AlligatorAlgMil
{
    public unsafe class ALLIGATOR_ALG_MIL
    {
        //const string BASE_DLL_DIRECTORY = @"D:\Project\01.Document\075.Alligator\3.Technical\1.CODING\002.AlligatorAlgMil\x64\Debug\";
        const string BASE_DLL_DIRECTORY = "";
        /*
       Structure
       */
        // Model Structure
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct sCModelFindResult
        {
            public unsafe int nCnt;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dXPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dYPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dXWorldPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dYWorldPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dScore;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dAngle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public unsafe double[] dScale;

            public void Init()
            {
                nCnt = 0;
                dXPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dYPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dXWorldPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dYWorldPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dScore = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dAngle = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dScale = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                for (int i = 0; i < ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES; i++)
                {
                    dXPos[i] = 0.0;
                    dYPos[i] = 0.0;
                    dXWorldPos[i] = 0.0;
                    dYWorldPos[i] = 0.0;
                    dScore[i] = 0.0;
                    dAngle[i] = 0.0;
                    dScale[i] = 0.0;
                }
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct sCCircleFindResult
        {
            public int nCnt;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public double[] dXPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public double[] dYPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public double[] dXWorldPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public double[] dYWorldPos;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public double[] dScore;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES)]
            public double[] dRadius;

            public void Init()
            {
                nCnt = 0;
                dXPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dYPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dXWorldPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dYWorldPos = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dScore = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];
                dRadius = new double[ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES];

                for (int i = 0; i < ALLIGATOR_ALG_MIL_DEFINE.MODEL_MAX_OCCURRENCES; i++)
                {
                    dXPos[i] = 0.0;
                    dYPos[i] = 0.0;
                    dXWorldPos[i] = 0.0;
                    dYWorldPos[i] = 0.0;
                    dScore[i] = 0.0;
                    dRadius[i] = 0.0;
                }
            }
        };

        /************************ 
         * Mil Algorithm Common *
         ************************/
        // 1. Mil Algorithm Init
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_Init", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe bool agtAM_Init([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.Bool)] bool bColor,
            [MarshalAs(UnmanagedType.I4)] int nSrcImgWidth, [MarshalAs(UnmanagedType.I4)] int nSecImgHeight);

        // 2. Mil Algorithm Free
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_Free", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_Free([MarshalAs(UnmanagedType.I4)] int nAlgIdx);

        // 3. Mil Algorithm Put Image Data (Alligator Image -> AlligatorAlgMil Image)
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_PutImage", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_PutImage([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            IntPtr pPutImage);
        //[MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I1)] byte[] pPutImage);

        // 4. Mil Algorithm Get Image
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_GetImage", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe IntPtr agtAM_GetImage([MarshalAs(UnmanagedType.I4)] int nAlgIdx);

        // 5. Mil Algorithm Select Color Channel(All Ch, Red Ch, Green Ch, Blue Ch)
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SelectColorImageChannel", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_SelectColorImageChannel([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nColorSelect);

        /****************** 
         * Mil Model Find *
         ******************/

        // 6. Model Setting
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SetModel_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SetModel_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetIndexModel, 
            [MarshalAs(UnmanagedType.I4)] int nLeftROI, [MarshalAs(UnmanagedType.I4)] int nRightROI,
            [MarshalAs(UnmanagedType.I4)] int nTopROI, [MarshalAs(UnmanagedType.I4)] int nBottomROI);

        // 7. Model Save
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SaveModel_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_SaveModel_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetIndexModel,
            [MarshalAs(UnmanagedType.LPStr)] string strLoadModelSavePath);

        // 7. Model Load
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_LoadModel_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_LoadModel_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetIndexModel, 
            [MarshalAs(UnmanagedType.LPStr)] string strLoadModelSavePath);

        // 8. Find Model
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_FindModel", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_FindModel([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetIndexModel,
            out sCModelFindResult sGetResult);

        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_FindModel", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_FindModel_ROI_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetIndexModel,
             [MarshalAs(UnmanagedType.I4)] int nRoiLeft,
            [MarshalAs(UnmanagedType.I4)] int nRoiRight,
            [MarshalAs(UnmanagedType.I4)] int nRoiTop,
            [MarshalAs(UnmanagedType.I4)] int nRoiBottom,
            out sCModelFindResult sGetResult);

        // 18. Grid Calibration Model Associate
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationModelAssociate", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationModelAssociate([MarshalAs(UnmanagedType.I4)] int nAlgIdx, 
            [MarshalAs(UnmanagedType.I4)] int nModelIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx);

        // 18. Grid Calibration Model DeAssociate
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationModelDeAssociate", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationModelDeAssociate([MarshalAs(UnmanagedType.I4)] int nAlgIdx, 
            [MarshalAs(UnmanagedType.I4)] int nModelIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx);


        // 9. Find Circle
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_FindCircle", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_FindCircle([MarshalAs(UnmanagedType.I4)] int nAlgIdx, 
            [MarshalAs(UnmanagedType.R8)] double dCircleRadius, 
            out sCCircleFindResult ResultCircleFind);

        // 8. Find Model Config
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_FindConfigDialog", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_FindConfigDialog([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetIndexModel);

        // 9. Find Circle In ROI
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_FindCircle_ROI_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_FindCircle_ROI_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.R8)] double dCircleRadius,
            [MarshalAs(UnmanagedType.I4)] int nRoiLeft,
            [MarshalAs(UnmanagedType.I4)] int nRoiRight,
            [MarshalAs(UnmanagedType.I4)] int nRoiTop,
            [MarshalAs(UnmanagedType.I4)] int nRoiBottom,
            out sCCircleFindResult ResultCircleFind);

        /***************** 
         * Ransac Circle *
         *****************/
        // Add. Ransac Param Set
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SetRansacParam", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SetRansacParam([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.R8)] double dSearchRange,
            [MarshalAs(UnmanagedType.R8)] double dStep,
            [MarshalAs(UnmanagedType.I4)] int nBrightThreshold,
            [MarshalAs(UnmanagedType.I4)] int nSkipInitBright,
            [MarshalAs(UnmanagedType.I4)] int nDistThreshold,
            [MarshalAs(UnmanagedType.I4)] int nNoSample);

        // Add. Ransac Param Get
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_GetRansacParam", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_GetRansacParam([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.R8)] out double dSearchRange,
            [MarshalAs(UnmanagedType.R8)] out double dStep,
            [MarshalAs(UnmanagedType.I4)] out int nBrightThreshold,
            [MarshalAs(UnmanagedType.I4)] out int nSkipInitBright,
            [MarshalAs(UnmanagedType.I4)] out int nDistThreshold,
            [MarshalAs(UnmanagedType.I4)] out int nNoSample);

        // Add. Ransac Param Set
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SearchRansacCircle", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_SearchRansacCircle([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bSearchDir,
            [MarshalAs(UnmanagedType.I4)] int nPreCenterX,
            [MarshalAs(UnmanagedType.I4)] int nPreCenterY,
            [MarshalAs(UnmanagedType.I4)] int nPreRadius,
            [MarshalAs(UnmanagedType.R8)] out double dCircleCenterX,
            [MarshalAs(UnmanagedType.R8)] out double dCircleCenterY,
            [MarshalAs(UnmanagedType.R8)] out double dCircleRadius);

        /******************** 
         * Pattern Matching *
         ********************/
        // 10. Save Pattern Model
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SavePattern_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_SavePattern_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetPatIdx,
            [MarshalAs(UnmanagedType.I4)] int nLeftRoi, [MarshalAs(UnmanagedType.I4)] int nRightRoi, [MarshalAs(UnmanagedType.I4)] int nTopRoi, [MarshalAs(UnmanagedType.I4)] int nBottomRoi,
            [MarshalAs(UnmanagedType.LPStr)] string strPatternSavePath);

        // 11. Load Pattern Model
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_LoadPattern_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_LoadPattern_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetPatIdx,
            [MarshalAs(UnmanagedType.LPStr)] string strLoadPatternSavePath);

        // 12. Pattern Matching Inspection Setting
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SetPatInspVal", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SetPatInspVal([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetPatIdx,
            [MarshalAs(UnmanagedType.I4)] int nScore, [MarshalAs(UnmanagedType.R8)] double dAngle);

        // 12. Pattern Matching Inspection Setting
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_GetPatInspVal", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_GetPatInspVal([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetPatIdx,
            [MarshalAs(UnmanagedType.I4)] out int nScore, [MarshalAs(UnmanagedType.R8)] out double dAngle);


        // 12. Pattern Matching
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_PatternMatching", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_PatternMatching([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetPatIdx,
            [MarshalAs(UnmanagedType.R8)] out double dGetXPos, [MarshalAs(UnmanagedType.R8)] out double dGetYPos,
            [MarshalAs(UnmanagedType.R8)] out double dGetAngle, [MarshalAs(UnmanagedType.R8)] out double dGetScore);

        // 12. Pattern Matching Area
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_PatternMatchingArea_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_PatternMatchingArea_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSetPatIdx,
            [MarshalAs(UnmanagedType.I4)] int nAreaLeft, [MarshalAs(UnmanagedType.I4)] int nAreaRight, [MarshalAs(UnmanagedType.I4)] int nAreaTop, [MarshalAs(UnmanagedType.I4)] int nAreaBottom,
            [MarshalAs(UnmanagedType.R8)] out double dGetXPos, [MarshalAs(UnmanagedType.R8)] out double dGetYPos,
            [MarshalAs(UnmanagedType.R8)] out double dGetAngle, [MarshalAs(UnmanagedType.R8)] out double dGetScore);


        /******** 
         * Blob *
         ********/
        // 13. BlobSearch
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_BlobSearch_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_BlobSearch_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nBlobColor,
            [MarshalAs(UnmanagedType.I4)] int nAreaLeft, [MarshalAs(UnmanagedType.I4)] int nAreaRight, [MarshalAs(UnmanagedType.I4)] int nAreaTop, [MarshalAs(UnmanagedType.I4)] int nAreaBottom,
            [MarshalAs(UnmanagedType.I4)] int nThreshold, [MarshalAs(UnmanagedType.I8)] long nMinSearchPixelCnt, [MarshalAs(UnmanagedType.I8)] long nMaxSearchPixelCnt,
            [MarshalAs(UnmanagedType.I4)] out int nGetLeft, [MarshalAs(UnmanagedType.I4)] out int nGetRight, [MarshalAs(UnmanagedType.I4)] out int nGetTop, [MarshalAs(UnmanagedType.I4)] out int nGetBottom);


        /*************** 
         * Orientation *
         ***************/
        // 14. Orientation Search
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OrientSearch", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OrientSearch([MarshalAs(UnmanagedType.I4)] int nAlgIdx, [MarshalAs(UnmanagedType.I4)] int nSearchCnt,
            [MarshalAs(UnmanagedType.Bool)] bool bImgAlign);

        // 15. Orientation Result Info Get
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OrientResult", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OrientResult([MarshalAs(UnmanagedType.I4)] int nAlgIdx, 
            [MarshalAs(UnmanagedType.I4)] out int nResultCnt, 
            [MarshalAs(UnmanagedType.LPArray)] float[] fAngle, [MarshalAs(UnmanagedType.LPArray)] float[] fScore);
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_OrientSearch_4Division", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]

        // 02.20 Insert (new DLL)
        public static extern unsafe void agtAM_OrientSearch_4Division([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nSearchCnt,
            [MarshalAs(UnmanagedType.Bool)] bool bImgAlign);


        /*************** 
         * Calibration *
         ***************/
        // 16. Grid Calibration
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibration", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibration([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx,
            [MarshalAs(UnmanagedType.R8)] double dGridOffsetX,
            [MarshalAs(UnmanagedType.R8)] double dGridOffsetY,
            [MarshalAs(UnmanagedType.R8)] double dGridOffsetZ,
            [MarshalAs(UnmanagedType.I4)] int nRowNumber,
            [MarshalAs(UnmanagedType.I4)] int nColumnNumber,
            [MarshalAs(UnmanagedType.R8)] double dRowSpacing,
            [MarshalAs(UnmanagedType.R8)] double dColumnSpacing,
            [MarshalAs(UnmanagedType.I4)] int nOperation,
            [MarshalAs(UnmanagedType.I4)] int nGridType,
            [MarshalAs(UnmanagedType.R8)] out double dDistanceX,
            [MarshalAs(UnmanagedType.R8)] out double dDistanceY,
            [MarshalAs(UnmanagedType.R8)] out double dCalibrationAngle);


        // 17. Grid Calibration Save
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SaveGridCalibration", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_SaveGridCalibration([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx,
            [MarshalAs(UnmanagedType.LPStr)] string strLoadModelSavePath);

        // 18. Grid Calibration Load
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_LoadGridCalibration", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_LoadGridCalibration([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx,
            [MarshalAs(UnmanagedType.LPStr)] string strLoadModelSavePath);

        // 18. Grid Calibration Associate
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationAssociate", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationAssociate([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx);

        // 18. Grid Calibration DeAssociate
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationDeAssociate", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationDeAssociate([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx);

        // 18. Grid Calibration Transform Image
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationTransformImage", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationTransformImage([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx);
        
        // 18. Grid Calibration Transform Coordinates to world
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationTransformCoordinatesToWorld", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationTransformCoordinatesToWorld([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx,
            [MarshalAs(UnmanagedType.R8)] double dXCoordiate,
            [MarshalAs(UnmanagedType.R8)] double dYCoordinate,
            [MarshalAs(UnmanagedType.R8)] out double dXTransform,
            [MarshalAs(UnmanagedType.R8)] out double dYTransform);
        
        // 18. Grid Calibration Transform Coordinates to pixel
        [DllImport(BASE_DLL_DIRECTORY + "AlligatorAlgMil.dll", EntryPoint = "agtAM_GridCalibrationTransformCoordinatesToPixel", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtAM_GridCalibrationTransformCoordinatesToPixel([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.I4)] int nCalIdx,
            [MarshalAs(UnmanagedType.R8)] double dXCoordiate,
            [MarshalAs(UnmanagedType.R8)] double dYCoordinate,
            [MarshalAs(UnmanagedType.R8)] out double dXTransform,
            [MarshalAs(UnmanagedType.R8)] out double dYTransform);


        /****************** 
         * Edge Detection *
         ******************/
        // Edge Detection SetImage
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SetImage", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SetImage([MarshalAs(UnmanagedType.I4)] int nAlgIdx);

        // Mean Value Edge Detection
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_EdgeDetect_Mean", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_EdgeDetect_Mean([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bColorToGray,
            [MarshalAs(UnmanagedType.I4)] int nImageWidth,
            [MarshalAs(UnmanagedType.I4)] int nImageHeight);

        // Center Line Mean Value Edge Detection
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_EdgeDetect_Mean_CenterLine", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_EdgeDetect_Mean_CenterLine([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bSelWidthHeight,
            [MarshalAs(UnmanagedType.I4)] int nDist,
            [MarshalAs(UnmanagedType.I4)] int nInterval,
            [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos1,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos2);

        // Select Line Edge Detection
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_EdgeDetect_Mean_SelectLine", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_EdgeDetect_Mean_SelectLine([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bSelWidthHeight,
            [MarshalAs(UnmanagedType.I4)] int nDist,
            [MarshalAs(UnmanagedType.I4)] int nInterval,
            [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
            [MarshalAs(UnmanagedType.I4)] int nSelectLine,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos1,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos2);

        // Select Line In Area Edge Detection
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_EdgeDetect_Mean_SelectAreaLine_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_EdgeDetect_Mean_SelectAreaLine_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bSelWidthHeight,
            [MarshalAs(UnmanagedType.I4)] int nDist,
            [MarshalAs(UnmanagedType.I4)] int nInterval,
            [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
            [MarshalAs(UnmanagedType.I4)] int nSelectLine,
            [MarshalAs(UnmanagedType.I4)] int rtArea_L, [MarshalAs(UnmanagedType.I4)] int rtArea_R,
            [MarshalAs(UnmanagedType.I4)] int rtArea_T, [MarshalAs(UnmanagedType.I4)] int rtArea_B,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos1,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos2,
            [MarshalAs(UnmanagedType.I4)] out int nRetLine1Pos,
            [MarshalAs(UnmanagedType.I4)] out int nRetLine2Pos);

        // One Point Edge Detection - Left to Right or Top to Bottom
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OnePointEdgeDetect", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OnePointEdgeDetect([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bSelWidthHeight,
            [MarshalAs(UnmanagedType.I4)] int nDist,
            [MarshalAs(UnmanagedType.I4)] int nInterval,
            [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
            [MarshalAs(UnmanagedType.I4)] int nStartPosition,
            [MarshalAs(UnmanagedType.I4)] int nEndPosition, 
            [MarshalAs(UnmanagedType.I4)] int nSelectLine,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos);

        // One Point Edge Detection - Right to Left or Bottom to Top
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OnePointEdgeDetect_Rev", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OnePointEdgeDetect_Rev([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.Bool)] bool bSelWidthHeight,
            [MarshalAs(UnmanagedType.I4)] int nDist,
            [MarshalAs(UnmanagedType.I4)] int nInterval,
            [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
            [MarshalAs(UnmanagedType.I4)] int nStartPosition,
            [MarshalAs(UnmanagedType.I4)] int nEndPosition,
            [MarshalAs(UnmanagedType.I4)] int nSelectLine,
            [MarshalAs(UnmanagedType.I4)] out int nRetPos);


        // Morpology Open
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_MopholAreaOpen", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_MopholAreaOpen([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.I4)] int nWidth, [MarshalAs(UnmanagedType.I4)] int nHeight, [MarshalAs(UnmanagedType.I4)] int AreaSize);

        // Morpology Close
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_MopholAreaClose", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_MopholAreaClose([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.I4)] int nWidth, [MarshalAs(UnmanagedType.I4)] int nHeight, [MarshalAs(UnmanagedType.I4)] int AreaSize);


        // Barcode Reading Nomal
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_BarcodeNormalReading_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_BarcodeNormalReading_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.I4)] int nAreaL, [MarshalAs(UnmanagedType.I4)] int nAreaR, [MarshalAs(UnmanagedType.I4)] int nAreaT, [MarshalAs(UnmanagedType.I4)] int nAreaB,
           [MarshalAs(UnmanagedType.LPStr)] out string strGetCode,
           [MarshalAs(UnmanagedType.I4)] out int nGetAreaL, [MarshalAs(UnmanagedType.I4)] out int nGetAreaR, [MarshalAs(UnmanagedType.I4)] out int nGetAreaT, [MarshalAs(UnmanagedType.I4)] out int nGetAreaB);

        // Barcode Reading Matrix
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_BarcodeMatrixReading_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_BarcodeMatrixReading_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.I4)] int nAreaL, [MarshalAs(UnmanagedType.I4)] int nAreaR, [MarshalAs(UnmanagedType.I4)] int nAreaT, [MarshalAs(UnmanagedType.I4)] int nAreaB,
           [MarshalAs(UnmanagedType.Bool)] bool bCodeColor, [MarshalAs(UnmanagedType.LPStr)] out string strGetCode,
           [MarshalAs(UnmanagedType.I4)] out int nGetAreaL, [MarshalAs(UnmanagedType.I4)] out int nGetAreaR, [MarshalAs(UnmanagedType.I4)] out int nGetAreaT, [MarshalAs(UnmanagedType.I4)] out int nGetAreaB);



        // OCR LoadFile Dialog
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OcrLoadFileDialog", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OcrLoadFileDialog([MarshalAs(UnmanagedType.I4)] int nAlgIdx);

        // OCR LoadFile Direct
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OcrLoadFileDirect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OcrLoadFileDirect_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
            [MarshalAs(UnmanagedType.LPStr)] string strPath, [MarshalAs(UnmanagedType.LPStr)] string strFileName);
               
        // OCR Reading Matrix
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OcrReading_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OcrReading_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.I4)] int nAreaL, [MarshalAs(UnmanagedType.I4)] int nAreaR, [MarshalAs(UnmanagedType.I4)] int nAreaT, [MarshalAs(UnmanagedType.I4)] int nAreaB,
           [MarshalAs(UnmanagedType.I4)] int nSetScore, [MarshalAs(UnmanagedType.LPStr)] out string strGetReadString,
           [MarshalAs(UnmanagedType.I4)] out int nGetStringSize);

        // OCR Result State
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OcrReasultState", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OcrReasultState([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.R8)] out double dGetCharPositionX, [MarshalAs(UnmanagedType.R8)] out double dGetCharPositionY, [MarshalAs(UnmanagedType.R8)] out double dGetCharScore,
           [MarshalAs(UnmanagedType.R8)] out double dCharSizeX, [MarshalAs(UnmanagedType.R8)] out double dCharSizeY, [MarshalAs(UnmanagedType.R8)] out double dTotalScore);

        // OCR Add Font
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_OcrAddFontChar_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_OcrAddFontChar_CS([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.I4)] int nAreaL, [MarshalAs(UnmanagedType.I4)] int nAreaR, [MarshalAs(UnmanagedType.I4)] int nAreaT, [MarshalAs(UnmanagedType.I4)] int nAreaB,
           [MarshalAs(UnmanagedType.LPStr)] string strGetReadString);




        /* Recipe */
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupROI_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupROI_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.I4)] int nLeft, [MarshalAs(UnmanagedType.I4)] int nRight,
           [MarshalAs(UnmanagedType.I4)] int nTop, [MarshalAs(UnmanagedType.I4)] int nBottom);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupThreshold_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupThreshold_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.I4)] int nValue);

        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupPatternMatching_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupPatternMatching_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.LPStr)] string strPath);

        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupModelFind_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupModelFind_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.LPStr)] string strPath);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupOCR_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupOCR_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.LPStr)] string strFontName,
           [MarshalAs(UnmanagedType.LPStr)] string strFontPath,
           [MarshalAs(UnmanagedType.I4)] int nScore);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupLight_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupLight_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.LPStr)] string strPortNo,
           [MarshalAs(UnmanagedType.I4)] int nChannel,
           [MarshalAs(UnmanagedType.I4)] int nLightValue);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupBlob_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupBlob_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.Bool)] bool bBlobSearch,
           [MarshalAs(UnmanagedType.I4)] int nMinBlobCnt,
           [MarshalAs(UnmanagedType.I4)] int nMaxBlobCnt);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupEdge_Add", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupEdge_Add([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.Bool)] bool bSelEdgeLine,
           [MarshalAs(UnmanagedType.I4)] int nDist,
           [MarshalAs(UnmanagedType.I4)] int nInterval,
           [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
           [MarshalAs(UnmanagedType.I4)] int nSelectLine);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupROI_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupROI_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
           [MarshalAs(UnmanagedType.LPStr)] string strName,
           [MarshalAs(UnmanagedType.I4)] out int nLeft,
           [MarshalAs(UnmanagedType.I4)] out int nRight,
           [MarshalAs(UnmanagedType.I4)] out int nTop,
           [MarshalAs(UnmanagedType.I4)] out int nBottom);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupThreshold_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupThreshold_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.I4)] out int nValue);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupPatternMatching_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupPatternMatching_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] out string strPath);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupModelFind_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupModelFind_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] out string strPath);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupOCR_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupOCR_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] string strFontName,
          [MarshalAs(UnmanagedType.LPStr)] out string strFontPath,
          [MarshalAs(UnmanagedType.I4)] out int nScore);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupLight_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupLight_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] string strPortNo,
          [MarshalAs(UnmanagedType.I4)] out int nChannel,
          [MarshalAs(UnmanagedType.I4)] out int nLightValue);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupBlob_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupBlob_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.Bool)] out bool bBlobSearch,
          [MarshalAs(UnmanagedType.I4)] out int nMinBlobCnt,
          [MarshalAs(UnmanagedType.I4)] out int nMaxBlobCnt);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupEdge_Get", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupEdge_Get([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.Bool)] out bool bSelEdgeLine,
          [MarshalAs(UnmanagedType.I4)] out int nDist,
          [MarshalAs(UnmanagedType.I4)] out int nInterval,
          [MarshalAs(UnmanagedType.I4)] out int nValueMinMaxInterval,
          [MarshalAs(UnmanagedType.I4)] out int nSelectLine);



        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupROI_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupROI_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.I4)] int nLeft,
          [MarshalAs(UnmanagedType.I4)] int nRight,
          [MarshalAs(UnmanagedType.I4)] int nTop,
          [MarshalAs(UnmanagedType.I4)] int nBottom);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupThreshold_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupThreshold_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.I4)] int nValue);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupPatternMatching_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupPatternMatching_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] string strPath);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupModelFind_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupModelFind_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] string strPath);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupOCR_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupOCR_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] string strFontName,
          [MarshalAs(UnmanagedType.LPStr)] string strFontPath,
          [MarshalAs(UnmanagedType.I4)] int nScore);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupLight_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupLight_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.LPStr)] string strPortNo,
          [MarshalAs(UnmanagedType.I4)] int nChannel,
          [MarshalAs(UnmanagedType.I4)] int nLightValue);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupBlob_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupBlob_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.Bool)] out bool bBlobSearch,
          [MarshalAs(UnmanagedType.I4)] int nMinBlobCnt,
          [MarshalAs(UnmanagedType.I4)] int nMaxBlobCnt);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupEdge_Modify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupEdge_Modify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName,
          [MarshalAs(UnmanagedType.Bool)] out bool bSelEdgeLine,
          [MarshalAs(UnmanagedType.I4)] int nDist,
          [MarshalAs(UnmanagedType.I4)] int nInterval,
          [MarshalAs(UnmanagedType.I4)] int nValueMinMaxInterval,
          [MarshalAs(UnmanagedType.I4)] int nSelectLine);



        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupROI_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupROI_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupThreshold_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupThreshold_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupPatternMatching_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupPatternMatching_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupModelFind_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupModelFind_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupOCR_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupOCR_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);

        
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupLight_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupLight_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);

        
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupBlob_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupBlob_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);

        
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_RcpSetupEdge_Delete", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_RcpSetupEdge_Delete([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strName);


        /*Sequence*/
        // ADD가 되는 순차적으로 Seq가 진행 된다.
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqInit", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqInit([MarshalAs(UnmanagedType.I4)] int nAlgIdx);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqAdd", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqAdd([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqTitle,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqName,
          [MarshalAs(UnmanagedType.I4)] int nRcpIndex,
          [MarshalAs(UnmanagedType.LPStr)] string strRcpName,
          [MarshalAs(UnmanagedType.I4)] int nFunction);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqDel", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqDel([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqTitle,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqName);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqModify", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqModify([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqTitle,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqName,
          [MarshalAs(UnmanagedType.I4)] int nRcpIndex,
          [MarshalAs(UnmanagedType.LPStr)] string strRcpName,
          [MarshalAs(UnmanagedType.I4)] int nFunction);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqGetOne", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqGetOne([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqTitle,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqName,
          [MarshalAs(UnmanagedType.I4)] int nRcpIndex,
          [MarshalAs(UnmanagedType.LPStr)] string strRcpName,
          [MarshalAs(UnmanagedType.I4)] int nFunction);

        

        /*
        //테스트 필요함
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqGetAll", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqGetAll([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqTitle,
          [MarshalAs(UnmanagedType.I4)] int nDataCnt,
          [MarshalAs(UnmanagedType.Struct)] out sCSequenceData SeqData);


        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqAlgStart", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqAlgStart([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.LPStr)] string strSeqTitle);


        //테스트 필요함
        [DllImport(BASE_DLL_DIRECTORY+"AlligatorAlgMil.dll", EntryPoint = "agtAM_SeqResultGet", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtAM_SeqResultGet([MarshalAs(UnmanagedType.I4)] int nAlgIdx,
          [MarshalAs(UnmanagedType.Struct)] out sCSeqLastResultData structResultData);
        */
    }
}