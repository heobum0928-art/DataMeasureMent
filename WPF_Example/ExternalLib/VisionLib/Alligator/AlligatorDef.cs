using System;
using System.Text;

namespace Alligator
{
    public unsafe class ALLIGATOR_DEFINE
    {
        // Viewer
        public const int VIEW_MAX_COUNT             = 64;
        public const bool VIEW_COLOR_MODE_COLOR     = true;
        public const bool VIEW_COLOR_MODE_GRAY      = false;

        // Draw Mode
        public const int DRAW_BASE_ALL              = 0;
        public const int DRAW_BASE_SMALL_CROSS		= 1;
        public const int DRAW_BASE_LARGE_CROSS		= 2;
        public const int DRAW_BASE_GRID				= 3;
        public const int DRAW_BASE_CENTER_X			= 4;
        public const int DRAW_BASE_CENTER_Y			= 5;
        public const int DRAW_BASE_PROFILE_X		= 6;
        public const int DRAW_BASE_PROFILE_Y		= 7;


        public const int DRAW_USER_ALL              = 0;
        public const int DRAW_USER_RECT				= 1;
        public const int DRAW_USER_LINE				= 2;
        public const int DRAW_USER_CIRCLE			= 3;
        public const int DRAW_USER_ELLIPSE			= 4;
        public const int DRAW_USER_CROSS			= 5;
        public const int DRAW_USER_TEXT             = 6;


        public const int DRAW_CTRL_ALL_IDX          = -1;
        public const int DRAW_CTRL_ALL              = 0;
        public const int DRAW_CTRL_RECT				= 1;
        public const int DRAW_CTRL_LINE				= 2;
        public const int DRAW_CTRL_CIRCLE			= 3;
        public const int DRAW_CTRL_ELLIPSE          = 4;
    }
}