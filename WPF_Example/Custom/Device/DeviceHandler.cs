using ReringProject.Define;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Device {
    /// <summary>
    /// 시스템에서 사용되는 카메라 이름 해상도, 방향 등을 정의합니다.
    /// </summary>
    public sealed partial class DeviceHandler {
        public const string CAMERA_TOP = "CAM_TOP";
        public const string CAMERA_SIDE = "CAM_SIDE";
        public const string CAMERA_BOTTOM = "CAM_BOTTOM";

        public const int WIDTH_TOP = 2448;
        public const int HEIGHT_TOP = 2048;

        public const int WIDTH_SIDE = 2448;
        public const int HEIGHT_SIDE = 2048;

        public const int WIDTH_BOTTOM = 2448;
        public const int HEIGHT_BOTTOM = 2048;


        public const bool REVERSE_X_TOP = false;
        public const bool REVERSE_Y_TOP = false;

        public const bool REVERSE_X_SIDE = false;
        public const bool REVERSE_Y_SIDE = false;

        public const bool REVERSE_X_BOTTOM = true;  // 02.06
        public const bool REVERSE_Y_BOTTOM = false;

        
        public const ERotateAngleType ROTATE_TOP = ERotateAngleType._0;
        public const ERotateAngleType ROTATE_SIDE = ERotateAngleType._0;
        public const ERotateAngleType ROTATE_BOTTOM = ERotateAngleType._0;

        //Common size
        public const int MAX_WIDTH = WIDTH_BOTTOM;
        public const int MIN_WIDTH = WIDTH_TOP;
        public const int DEFAULT_WIDTH = WIDTH_TOP;
        
        public const int MAX_HEIGHT = HEIGHT_BOTTOM;
        public const int MIN_HEIGHT = HEIGHT_TOP;
        public const int DEFAULT_HEIGHT = HEIGHT_TOP;

        public const int MIN_ROI_WIDTH = 50;
        public const int MAX_ROI_WIDTH = MAX_WIDTH;

        public const int MIN_CIRCLE_RADIUS = 50;
        public const int MAX_CIRCLE_RADIUS = MAX_WIDTH;

        public const int MIN_ROI_HEIGHT = 50;
        public const int MAX_ROI_HEIGHT = MAX_HEIGHT;

        public const double TICK_EXPOSURE = 0.1;
        public const double MIN_EXPOSURE = 10;
        public const double MAX_EXPOSURE = 100000;

        public const double TICK_GAIN = 0.1;
        public const double MIN_GAIN = 0;
        public const double MAX_GAIN = 95.0;

        public const double TICK_GAMMA = 0.1;
        public const double MIN_GAMMA = 0;
        public const double MAX_GAMMA = 3.99998474121094;

        public const string FILTER_IMAGE = "tiff Files(*.tiff)|*.tiff|bmp Files(*.bmp)|*.bmp|jpg Files(*.jpg)|*.jpg|jpeg Files(*.jpeg)|*.jpeg|png Files(*.png)|*.png|All Files(*.*)|*.*";
        public const string EXTENSION_IMAGE = ".tiff";

        public const string EXTENSION_SAVE_IMAGE = ".jpg";
        public const string FILTER_SAVE_IMAGE = "jpg Files(*.jpg)|*.jpg";

        public const string FILTER_MODEL = "mmf Files(*.mmf)|*.mmf";
        public const string EXTENSION_MODEL = ".mmf";

        public const string EXTENSION_CALIBRATION = ".cal";

        /// <summary>
        /// 이 함수에서 카메라를 정의합니다. 
        /// 함수는 시스템 초기화 시점에 호출됩니다.
        /// </summary>
        private void RegisterRequiredDevices() {
            SetRequiredDevice(
                ECameraType.HIK, 
                ECaptureImageType.Gray8, 
                ETriggerSource.Software,
                CAMERA_TOP, 
                WIDTH_TOP, 
                HEIGHT_TOP,
                REVERSE_X_TOP,
                REVERSE_Y_TOP,
                ROTATE_TOP);

            SetRequiredDevice(
                ECameraType.HIK, 
                ECaptureImageType.Gray8, 
                ETriggerSource.Software,
                CAMERA_SIDE, 
                WIDTH_SIDE, 
                HEIGHT_SIDE,
                REVERSE_X_SIDE,
                REVERSE_Y_SIDE,
                ROTATE_SIDE
                );

            SetRequiredDevice(
                ECameraType.HIK, 
                ECaptureImageType.Gray8, 
                ETriggerSource.Hardware_Line0,
                CAMERA_BOTTOM, 
                WIDTH_BOTTOM, 
                HEIGHT_BOTTOM,
                REVERSE_X_BOTTOM,
                REVERSE_Y_BOTTOM,
                ROTATE_BOTTOM
                );
        }
    }
}
