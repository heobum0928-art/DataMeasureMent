using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alligator
{
    public unsafe class ALLIGATOR
    {
        /************************ 
         * Viewer Init, Setting *
         ************************/
        // 1. Alligator Initial
        [DllImport("Alligator.dll", EntryPoint = "agtInit", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtInit([MarshalAs(UnmanagedType.I4)] int nViewNum,  IntPtr hWnd, [MarshalAs(UnmanagedType.I4)] int nWndRectLeft, [MarshalAs(UnmanagedType.I4)] int nWndRectTop, [MarshalAs(UnmanagedType.I4)] int nWndRectRight, [MarshalAs(UnmanagedType.I4)] int nWndRectBottom);

        // 2. Alligator Free
        [DllImport("Alligator.dll", EntryPoint = "agtFree", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtFree([MarshalAs(UnmanagedType.I4)] int nViewNum);

        // 3. Alligator Viewer Setting 
        [DllImport("Alligator.dll", EntryPoint = "agtSetView", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtSetView([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nWidth, [MarshalAs(UnmanagedType.I4)] int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bColor);

        // 4. Get Viewer Image Data to BYTE*
        [DllImport("Alligator.dll", EntryPoint = "agtGetImageData", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe IntPtr agtGetImageData([MarshalAs(UnmanagedType.I4)] int nViewNum);

        // 5. Grab Image Data
        [DllImport("Alligator.dll", EntryPoint = "agtGrab", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        //public static extern unsafe int agtGrab([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I1)] byte[] pImage);
        public static extern unsafe int agtGrab([MarshalAs(UnmanagedType.I4)] int nViewNum, IntPtr pImage);

        // 6. Get Image Data Info
        [DllImport("Alligator.dll", EntryPoint = "agtGetViewInfo", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtGetViewInfo([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.Bool)] out bool bColor, [MarshalAs(UnmanagedType.I4)] out int nWidth, [MarshalAs(UnmanagedType.I4)] out int nHeight);


        /******************** 
         * Image Load, Save *
         ********************/
        // 7. Image File Load - Direct Load
        [DllImport("Alligator.dll", EntryPoint = "agtLoadImageDirect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtLoadImageDirect_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.LPStr)] string strImgFullPath);

        // 8. Image File Load - Open Dialog
        [DllImport("Alligator.dll", EntryPoint = "agtLoadImageDialog", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtLoadImageDialog([MarshalAs(UnmanagedType.I4)] int nViewNum);

        // 9. Image File Save - Direct Save
        [DllImport("Alligator.dll", EntryPoint = "agtSaveImageDirect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtSaveImageDirect_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.LPStr)] string strImgPath, [MarshalAs(UnmanagedType.LPStr)] string strImgFileName);

        // 10. Image File Save - Save Dialog
        [DllImport("Alligator.dll", EntryPoint = "agtSaveImageDialog", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtSaveImageDialog([MarshalAs(UnmanagedType.I4)] int nViewNum);

        // 11. Result Image File Save (Add Draw) - Direct Save
        [DllImport("Alligator.dll", EntryPoint = "agtSaveResultImageDirect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtSaveResultImageDirect_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.LPStr)] string strImgPath, [MarshalAs(UnmanagedType.LPStr)] string strImgFileName);

        // 12. Result Image File Save (Add Draw) - Save Dialog
        [DllImport("Alligator.dll", EntryPoint = "agtSaveResultImageDialog", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtSaveResultImageDialog([MarshalAs(UnmanagedType.I4)] int nViewNum);


        /***************** 
         * Zoom In / Out *
         *****************/
        // 13. ZoomIn ( Button Zoom )
        [DllImport("Alligator.dll", EntryPoint = "agtZoomIn", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtZoomIn([MarshalAs(UnmanagedType.I4)] int nViewNum);

        // 13. ZoomOut ( Button Zoom )
        [DllImport("Alligator.dll", EntryPoint = "agtZoomOut", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int agtZoomOut([MarshalAs(UnmanagedType.I4)] int nViewNum);


        /****************** 
         * Draw Base Mode *
         ******************/
        // 14. Base Draw Clear
        [DllImport("Alligator.dll", EntryPoint = "agtDrawClearBase", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawClearBase([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nDrawMode);

        // 15. Base Draw Large Cross Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseLargeCross_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseLargeCross_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 16. Base Draw Small Cross Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseSmallCross_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseSmallCross_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nLength, 
            [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 17. Base Draw Grid X, Y Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseGrid_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseGrid_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, 
            [MarshalAs(UnmanagedType.I4)] int nCntAxisX, [MarshalAs(UnmanagedType.I4)] int nCntAxisY, [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 18. Base Draw Center Line X Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseCenterX_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseCenterX_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 19. Base Draw Center Line Y Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseCenterY_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseCenterY_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 20. Base Draw Profile X Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseProfileX_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseProfileX_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, 
            [MarshalAs(UnmanagedType.I4)] int nPositionY, [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 21. Base Draw Profile Y Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawBaseProfileY_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawBaseProfileY_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, 
            [MarshalAs(UnmanagedType.I4)] int nPositionX, [MarshalAs(UnmanagedType.I4)] int nThick, 
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);


        /****************** 
         * Draw User Mode *
         ******************/
        // 22. User Draw Clear Mode
        [DllImport("Alligator.dll", EntryPoint = "agtDrawClearUser", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawClearUser([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nDrawMode);

        // 23. User Draw Rect
        [DllImport("Alligator.dll", EntryPoint = "agtDrawUserRect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawUserRect_CS([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.I4)] int nRectL, [MarshalAs(UnmanagedType.I4)] int nRectT, [MarshalAs(UnmanagedType.I4)] int nRectR, [MarshalAs(UnmanagedType.I4)] int nRectB, 
            [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 24. User Draw Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawUserLine_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawUserLine_CS([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.I4)] int nStartPosX, [MarshalAs(UnmanagedType.I4)] int nStartPosY, 
            [MarshalAs(UnmanagedType.I4)] int nEndPosX, [MarshalAs(UnmanagedType.I4)] int nEndPosY,
            [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 25. User Draw Circle
        [DllImport("Alligator.dll", EntryPoint = "agtDrawUserCircle_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawUserCircle_CS([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.I4)] int nCenterPosX, [MarshalAs(UnmanagedType.I4)] int nCenterPosY,
            [MarshalAs(UnmanagedType.R8)] double dR,
            [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 26. User Draw Ellipse
        [DllImport("Alligator.dll", EntryPoint = "agtDrawUserEllipse_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawUserEllipse_CS([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.I4)] int nCenterPosX, [MarshalAs(UnmanagedType.I4)] int nCenterPosY,
            [MarshalAs(UnmanagedType.I4)] int nLengthX, [MarshalAs(UnmanagedType.I4)] int nLengthY,
            [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 27. User Draw Cross Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawUserCross_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawUserCross_CS([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.I4)] int nCenterPosX, [MarshalAs(UnmanagedType.I4)] int nCenterPosY,
            [MarshalAs(UnmanagedType.I4)] int nLength,
            [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 28. User Draw Text
        [DllImport("Alligator.dll", EntryPoint = "agtDrawUserText_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawUserText_CS([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.LPStr)] string strText,
            [MarshalAs(UnmanagedType.I4)] int nPosX, [MarshalAs(UnmanagedType.I4)] int nPosY,
            [MarshalAs(UnmanagedType.I4)] uint nFont,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);


        /****************** 
         * Draw Ctrl Mode *
         ******************/
        // 29. Clear Ctrl Draw
        [DllImport("Alligator.dll", EntryPoint = "agtDrawClearCtrl", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawClearCtrl([MarshalAs(UnmanagedType.I4)] int nViewNum, 
            [MarshalAs(UnmanagedType.I4)] int nIndex, [MarshalAs(UnmanagedType.I4)] int nDrawMode);

        // 30. Ctrl Draw Rect
        [DllImport("Alligator.dll", EntryPoint = "agtDrawCtrlRect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawCtrlRect_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nIndex,
            [MarshalAs(UnmanagedType.I4)] int nRectL, [MarshalAs(UnmanagedType.I4)] int nRectT, [MarshalAs(UnmanagedType.I4)] int nRectR, [MarshalAs(UnmanagedType.I4)] int nRectB,
            [MarshalAs(UnmanagedType.LPStr)] string strText, [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 31. Ctrl Draw Line
        [DllImport("Alligator.dll", EntryPoint = "agtDrawCtrlLine_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawCtrlLine_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nIndex,
            [MarshalAs(UnmanagedType.I4)] int nStartPosX, [MarshalAs(UnmanagedType.I4)] int nStartPosY, 
            [MarshalAs(UnmanagedType.I4)] int nEndPosX, [MarshalAs(UnmanagedType.I4)] int nEndPosY,
            [MarshalAs(UnmanagedType.LPStr)] string strText, [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 32. Ctrl Draw Circle
        [DllImport("Alligator.dll", EntryPoint = "agtDrawCtrlCircle_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawCtrlCircle_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nIndex,
            [MarshalAs(UnmanagedType.I4)] int nCenterPosX, [MarshalAs(UnmanagedType.I4)] int nCenterPosY,
            [MarshalAs(UnmanagedType.R8)] double dR,
            [MarshalAs(UnmanagedType.LPStr)] string strText, [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 33. Ctrl Draw Ellipse
        [DllImport("Alligator.dll", EntryPoint = "agtDrawCtrlEllipse_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtDrawCtrlEllipse_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nIndex,
            [MarshalAs(UnmanagedType.I4)] int nCenterPosX, [MarshalAs(UnmanagedType.I4)] int nCenterPosY,
            [MarshalAs(UnmanagedType.I4)] int nLengthX, [MarshalAs(UnmanagedType.I4)] int nLengthY,
            [MarshalAs(UnmanagedType.LPStr)] string strText, [MarshalAs(UnmanagedType.I4)] int nThick,
            [MarshalAs(UnmanagedType.I4)] int nRed, [MarshalAs(UnmanagedType.I4)] int nGreen, [MarshalAs(UnmanagedType.I4)] int nBlue);

        // 34. Get Select Ctrl Draw Rect
        [DllImport("Alligator.dll", EntryPoint = "agtGetCtrlSelectIdxRect_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtGetCtrlSelectIdxRect_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nSelectIdx,
            [MarshalAs(UnmanagedType.I4)] out int nRectL, [MarshalAs(UnmanagedType.I4)] out int nRectT,
            [MarshalAs(UnmanagedType.I4)] out int nRectR, [MarshalAs(UnmanagedType.I4)] out int nRectB);

        // 35. Get Select Ctrl Draw Line
        [DllImport("Alligator.dll", EntryPoint = "agtGetCtrlSelectIdxLine_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtGetCtrlSelectIdxLine_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nSelectIdx,
            [MarshalAs(UnmanagedType.I4)] out int nStartPosX, [MarshalAs(UnmanagedType.I4)] out int nStartPosY,
            [MarshalAs(UnmanagedType.I4)] out int nEndPosX, [MarshalAs(UnmanagedType.I4)] out int nEndPosY);

        // 36. Get Select Ctrl Draw Circle
        [DllImport("Alligator.dll", EntryPoint = "agtGetCtrlSelectIdxCircle_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtGetCtrlSelectIdxCircle_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nSelectIdx,
            [MarshalAs(UnmanagedType.I4)] out int nCenterPosX, [MarshalAs(UnmanagedType.I4)] out int nCenterPosY,
            [MarshalAs(UnmanagedType.R8)] out double dR);

        // 37. Get Select Ctrl Draw Ellipse
        [DllImport("Alligator.dll", EntryPoint = "agtGetCtrlSelectIdxEllipse_CS", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtGetCtrlSelectIdxEllipse_CS([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.I4)] int nSelectIdx,
            [MarshalAs(UnmanagedType.I4)] out int nCenterPosX, [MarshalAs(UnmanagedType.I4)] out int nCenterPosY,
            [MarshalAs(UnmanagedType.I4)] out int nLengthX, [MarshalAs(UnmanagedType.I4)] out int nLengthY);

        // 38. Enable Focus Value
        [DllImport("Alligator.dll", EntryPoint = "agtEnableFocusValue", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtEnableFocusValue([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

        // 39. Get Focus Value
        [DllImport("Alligator.dll", EntryPoint = "agtGetFocusValue", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtGetFocusValue([MarshalAs(UnmanagedType.I4)] int nViewNum, [MarshalAs(UnmanagedType.R8)] out double dFocusValue);

        // 40. Profile Viewer Gray
        [DllImport("Alligator.dll", EntryPoint = "agtProfileDataGray", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtProfileDataGray([MarshalAs(UnmanagedType.I4)] int nViewNum, 
            [MarshalAs(UnmanagedType.Bool)] bool bEnable,
            [MarshalAs(UnmanagedType.Bool)] bool bAxis);

        // 41. Profile Viewer Color
        [DllImport("Alligator.dll", EntryPoint = "agtProfileDataColor", ExactSpelling = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void agtProfileDataColor([MarshalAs(UnmanagedType.I4)] int nViewNum,
            [MarshalAs(UnmanagedType.Bool)] bool bEnable,
            [MarshalAs(UnmanagedType.Bool)] bool bAxis);
    }
}