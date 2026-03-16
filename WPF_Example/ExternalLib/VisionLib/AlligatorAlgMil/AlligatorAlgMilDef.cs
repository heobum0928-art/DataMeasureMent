using System;
using System.Text;

namespace AlligatorAlgMil
{
    public unsafe class ALLIGATOR_ALG_MIL_DEFINE
    {
        public const int ALG_MAX_CNT                = 255;

        public const bool ALG_COLOR_MODE_GRAY       = false;
        public const bool ALG_COLOR_MODE_COLOR      = true;

        public const int ALG_COLOR_CH_SEL_ALL		= 0;
        public const int ALG_COLOR_CH_SEL_RED		= 1;
        public const int ALG_COLOR_CH_SEL_GREEN		= 2;
        public const int ALG_COLOR_CH_SEL_BLUE      = 3;

        // Model Find
        public const int ALG_MODEL_MAX_CNT			= 255;
        public const int MODEL_MAX_OCCURRENCES      = 1024;   // origin 32

        // Mattern Matching
        public const int PAT_MAX_OCCURRENCES        = 32;

        // Blob
        public const int BLOB_SEARCH_WHITE			= 0;
        public const int BLOB_SEARCH_BLACK			= 1;
        public const int BLOB_MAX_SEARCH_CNT        = 500;

        // Orientation
        public const int ORIENT_MAX_COUNT           = 32;

        // Edge Detection
        public const int EDGE_SEARCH_DIST_FOW       = 0;
        public const int EDGE_SEARCH_DIST_INV       = 1;
        public const int EDGE_SEARCH_DIST_CEN       = 2;
        public const bool EDGE_OPT_INSP_MEAN        = false;
        public const bool EDGE_OPT_INSP_VALUE       = true;
        public const int EDGE_OPT_SEARCH_POINT_ONE  = 0;
        public const int EDGE_OPT_SEARCH_POINT_ALL  = 1;
        public const int EDGE_OPT_SEARCH_POINT_MAX  = 2;
        public const int EDGE_RESULT_MAX_CNT        = 255;
        public const int EDGE_INPUT_X               = 0;
        public const int EDGE_INPUT_Y               = 1;

    }
}