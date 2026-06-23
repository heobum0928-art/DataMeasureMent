//260623 hbk Phase 58
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.IO;

namespace ReringProject.Device {

    /// <summary>
    /// Hikvision GigE 정렬 카메라 독립 래퍼 (DeviceHandler 등록 없음).
    /// HikCamera 를 composition 으로 보유하며 Connect/Grab/Live/Stop/Close 만 노출.
    /// Phase 58 AV-02 — 기존 Grabber 카메라 경로 무수정.
    /// </summary>
    public class EthernetAlignCamera {

        private const int DEFAULT_WIDTH = 5120;     // MV-CH250-90GM 플레이스홀더 (Open 후 실 해상도로 덮어씀)
        private const int DEFAULT_HEIGHT = 5120;
        private const string ALIGN_FALLBACK_IMAGE_PATH = @"D:\align_test.bmp"; // D-04 SIMUL/실패 폴백

        private HikCamera _hikCamera = null;    // composed instance (HikCamera 미수정, DeviceHandler 미등록)
        private string _cameraIp = null;

        /// <summary>실 카메라가 열려 있으면 true. HikCamera 인스턴스 없거나 Open 미완료면 false.</summary>
        public bool IsOpen {
            get { return (_hikCamera != null) && _hikCamera.IsOpen; }
        }

        /// <summary>
        /// 지정한 IP 의 Hikvision GigE 카메라에 연결.
        /// 실패 시 false 반환 (예외 throw 없음).
        /// </summary>
        /// <param name="ip">카메라 IP 주소 (예: "192.168.1.100")</param>
        /// <returns>연결 성공 여부</returns>
        public bool Connect(string ip) {
            try {
                _cameraIp = ip;

                DisplayConfig config = new DisplayConfig();
                DeviceInfo info = new DeviceInfo(
                    ECameraType.HIK,
                    ECaptureImageType.Gray8,
                    ETriggerSource.Software,
                    ip,
                    DEFAULT_WIDTH,
                    DEFAULT_HEIGHT,
                    false,
                    false);

                _hikCamera = new HikCamera(config, info);

                int deviceCount = HikCamera.EnumerateDevice(ip);
                if (deviceCount == 0) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Connect: no device found for {0}", ip);
                    return false;
                }

                bool bOpened = _hikCamera.Open(ip);
                return bOpened;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Connect failed: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 카메라에서 단일 이미지를 소프트웨어 트리거로 취득.
        /// 미연결 또는 Grab 실패 시 D:\align_test.bmp 폴백 이미지를 반환.
        /// 반환된 HImage 는 호출자가 Dispose() 책임.
        /// </summary>
        /// <returns>취득 이미지(HImage). 폴백도 실패하면 null.</returns>
        public HImage Grab() {
            try {
                if (IsOpen) {
                    HImage img = _hikCamera.GrabHalconImage();
                    if (img != null) {
                        return img;
                    }
                }
                return LoadFallbackImage();
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Grab failed: {0}", ex.Message);
                return LoadFallbackImage();
            }
        }

        /// <summary>
        /// 연속 스트리밍(라이브) 시작.
        /// Phase 61 에서 뷰어에 표시 예정.
        /// </summary>
        /// <returns>스트림 시작 성공 여부</returns>
        public bool Live() {
            try {
                if (!IsOpen) {
                    return false;
                }
                return _hikCamera.StartStream();
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Live failed: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>연속 스트리밍 중지.</summary>
        public void Stop() {
            try {
                if (_hikCamera != null) {
                    _hikCamera.StopStream();
                }
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Stop failed: {0}", ex.Message);
            }
        }

        /// <summary>카메라 연결 해제 및 핸들 해제.</summary>
        public void Close() {
            try {
                if (_hikCamera != null) {
                    _hikCamera.Close();
                    _hikCamera = null;
                }
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Close failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// D:\align_test.bmp 를 읽어 HImage 로 반환.
        /// 컬러 이미지이면 Gray8 으로 변환.
        /// 반환된 HImage 는 호출자가 Dispose() 책임.
        /// </summary>
        private HImage LoadFallbackImage() {
            try {
                if (!File.Exists(ALIGN_FALLBACK_IMAGE_PATH)) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] fallback image missing: {0}", ALIGN_FALLBACK_IMAGE_PATH);
                    return null;
                }
                HImage loaded = new HImage();
                loaded.ReadImage(ALIGN_FALLBACK_IMAGE_PATH);
                if (loaded.CountChannels().I > 1) {
                    HImage gray = loaded.Rgb1ToGray();
                    loaded.Dispose();
                    return gray;
                }
                return loaded;
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] LoadFallbackImage failed: {0}", ex.Message);
                return null;
            }
        }
    }
}
