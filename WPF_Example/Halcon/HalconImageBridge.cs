using System;
using System.Runtime.InteropServices;
using HalconDotNet;
using Mat = OpenCvSharp.Mat;
using MatType = OpenCvSharp.MatType;

namespace ReringProject.Halcon
{
    public static class HalconImageBridge
    {
        public static HImage Clone(HImage image)
        {
            if (image == null)
            {
                return null;
            }

            return image.CopyImage();
        }

        public static Mat ToMat(HImage image)
        {
            if (image == null)
            {
                return null;
            }
            int channelCount = image.CountChannels().I;
            if (channelCount == 1)
            {
                string imageType;
                int width;
                int height;
                IntPtr sourcePtr = image.GetImagePointer1(out imageType, out width, out height);
                MatType matType;
                if (imageType == "uint2") matType = MatType.CV_16UC1;
                else matType = MatType.CV_8UC1;
                var mat = new Mat(height, width, matType);
                int bytesPerPixel;
                if (matType == MatType.CV_16UC1) bytesPerPixel = 2;
                else bytesPerPixel = 1;
                int byteCount = checked(width * height * bytesPerPixel);
                byte[] data = new byte[byteCount];
                Marshal.Copy(sourcePtr, data, 0, byteCount);
                Marshal.Copy(data, 0, mat.Data, byteCount);
                return mat;
            }
            if (channelCount == 3)
            {
                IntPtr redPtr;
                IntPtr greenPtr;
                IntPtr bluePtr;
                string imageType;
                int width;
                int height;
                image.GetImagePointer3(out redPtr, out greenPtr, out bluePtr, out imageType, out width, out height);
                var blue = new byte[checked(width * height)];
                var green = new byte[checked(width * height)];
                var red = new byte[checked(width * height)];
                Marshal.Copy(bluePtr, blue, 0, blue.Length);
                Marshal.Copy(greenPtr, green, 0, green.Length);
                Marshal.Copy(redPtr, red, 0, red.Length);
                var mat = new Mat(height, width, MatType.CV_8UC3);
                byte[] interleaved = new byte[checked(width * height * 3)];
                for (int i = 0, j = 0; i < blue.Length; i++, j += 3)
                {
                    interleaved[j] = blue[i];
                    interleaved[j + 1] = green[i];
                    interleaved[j + 2] = red[i];
                }
                Marshal.Copy(interleaved, 0, mat.Data, interleaved.Length);
                return mat;
            }
            throw new NotSupportedException(string.Format("Unsupported HALCON channel count: {0}", channelCount));
        }        public static HImage FromMat(Mat image)
        {
            if (image == null || image.Empty())
            {
                return null;
            }

            using (var normalized = NormalizeForHalcon(image))
            {
                var data = new byte[checked((int)(normalized.Total() * normalized.ElemSize()))];
                Marshal.Copy(normalized.Data, data, 0, data.Length);
                var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    var ptr = handle.AddrOfPinnedObject();
                    var normalizedType = normalized.Type();
                    if (normalizedType == MatType.CV_8UC1)
                    {
                        var halconImage = new HImage();
                        halconImage.GenImage1("byte", normalized.Width, normalized.Height, ptr);
                        return halconImage;
                    }

                    if (normalizedType == MatType.CV_16UC1)
                    {
                        var halconImage = new HImage();
                        halconImage.GenImage1("uint2", normalized.Width, normalized.Height, ptr);
                        return halconImage;
                    }

                    if (normalizedType == MatType.CV_8UC3)
                    {
                        var halconImage = new HImage();
                        halconImage.GenImageInterleaved(ptr, "bgr", normalized.Width, normalized.Height, -1, "byte", normalized.Width, normalized.Height, 0, 0, -1, 0);
                        return halconImage;
                    }

                    if (normalizedType == MatType.CV_8UC4)
                    {
                        var halconImage = new HImage();
                        halconImage.GenImageInterleaved(ptr, "bgrx", normalized.Width, normalized.Height, -1, "byte", normalized.Width, normalized.Height, 0, 0, -1, 0);
                        return halconImage;
                    }

                    throw new NotSupportedException(string.Format("Unsupported Mat type: {0}", normalizedType));
                }
                finally
                {
                    handle.Free();
                }
            }
        }

        private static Mat NormalizeForHalcon(Mat image)
        {
            if (!image.IsContinuous())
            {
                return image.Clone();
            }

            var imageType = image.Type();
            if (imageType == MatType.CV_8UC1 ||
                imageType == MatType.CV_8UC3 ||
                imageType == MatType.CV_8UC4 ||
                imageType == MatType.CV_16UC1)
            {
                return image.Clone();
            }

            if (image.Channels() == 1)
            {
                var convertedGray = new Mat();
                image.ConvertTo(convertedGray, MatType.CV_8UC1);
                return convertedGray;
            }

            if (image.Channels() == 3)
            {
                var convertedBgr = new Mat();
                image.ConvertTo(convertedBgr, MatType.CV_8UC3);
                return convertedBgr;
            }

            if (image.Channels() == 4)
            {
                var convertedBgrx = new Mat();
                image.ConvertTo(convertedBgrx, MatType.CV_8UC4);
                return convertedBgrx;
            }

            throw new NotSupportedException(string.Format("Unsupported channel count: {0}", image.Channels()));
        }
    }
}
