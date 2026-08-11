using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using HalconDotNet;
using OpenCvSharp;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Services
{
    public interface IHalconTeachingProvider
    {
        IEnumerable<RoiDefinition> GetViewerRois();
    }

    public class TeachingStorageService
    {
        public void Save<T>(string path, T data)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = File.Create(path))
            {
                serializer.WriteObject(stream, data);
            }
        }

        public T Load<T>(string path)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = File.OpenRead(path))
            {
                return (T)serializer.ReadObject(stream);
            }
        }
    }

    public static class HalconTeachingHelper
    {
        public static TeachingJob LoadJob(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return new TeachingStorageService().Load<TeachingJob>(path);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveJob(string path, TeachingJob job)
        {
            if (string.IsNullOrWhiteSpace(path) || job == null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            new TeachingStorageService().Save(path, job);
        }

        public static string BuildFixedTeachingPath(string sourceName)
        {
            var key = NormalizeTeachingKey(sourceName);
            var recipePath = SystemHandler.Handle.Setting.RecipeSavePath;
            var recipeName = string.IsNullOrWhiteSpace(SystemHandler.Handle.Setting.CurrentRecipeName)
                ? "A"
                : SystemHandler.Handle.Setting.CurrentRecipeName;
            var directory = Path.Combine(recipePath, recipeName, key);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "teaching.json");
        }

        public static string GetTeachingDialogDirectory(string teachingFilePath)
        {
            if (string.IsNullOrWhiteSpace(teachingFilePath))
            {
                return Environment.CurrentDirectory;
            }

            var fullPath = Path.GetFullPath(teachingFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return Environment.CurrentDirectory;
            }

            Directory.CreateDirectory(directory);
            return directory;
        }
        public static string SaveTempImage(string key, HImage image)
        {
            if (image == null)
            {
                return null;
            }

            var safeKey = string.IsNullOrWhiteSpace(key) ? "halcon" : string.Concat(key.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
            if (string.IsNullOrWhiteSpace(safeKey))
            {
                safeKey = "halcon";
            }

            var directory = Path.Combine(Path.GetTempPath(), "DatumMeasurementViewer", safeKey);
            Directory.CreateDirectory(directory);

            CleanupTempImages(directory, 20);

            // 260811 hbk quick-debug(bottom-align-live-view-stutter): PNG(DEFLATE 압축) 저장이 CXP
            //  13376x9528(~1.27억 픽셀, mono ~127MB) 원본에서 저장+재로드(디코드) 양쪽 모두의 병목이 되어
            //  bmp(무압축, 무손실 유지)로 전환. 동일 클래스 문제를 이미 해결한 선례: VersionDefine.cs
            //  1.6.2.0 "검사Grab tact 개선"(MainView.xaml.cs GrabSaveAndDisplay, 저장 실측 ~320ms).
            //  이 경로(CalibrationWindow 라이브 촬상)는 그 선례와 달리 Task.Run 백그라운드 스레드가 없어
            //  grab→저장→재로드 전체가 UI 스레드에서 동기 실행되므로, PNG→BMP 전환이 저장 비용뿐 아니라
            //  재로드 시 디코딩 비용까지 함께 제거해 체감 개선 폭이 더 크다.
            var fileName = string.Format(
                "{0}_{1}.bmp",
                DateTime.Now.ToString("yyyyMMdd_HHmmssfff"),
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, fileName);
            image.WriteImage("bmp", 0, path);
            return path;
        }

        private static void CleanupTempImages(string directory, int keepCount)
        {
            try
            {
                // 260811 hbk quick-debug(bottom-align-live-view-stutter): 확장자를 "*.png" 로 하드코딩하면
                //  위 SaveTempImage 가 bmp 로 전환된 뒤 새로 생성되는 파일이 전혀 정리 대상에 안 잡혀 무제한
                //  누적됨(디스크 소진) — 이 디렉토리(DatumMeasurementViewer/<key>)는 이 메서드 전용 임시
                //  캐시라 다른 파일 타입이 섞일 일이 없으므로 확장자 무관하게 전부 대상으로 넓힘(레거시 *.png
                //  잔존 파일도 자연스럽게 함께 정리됨).
                var files = new DirectoryInfo(directory)
                    .GetFiles("*.*")
                    .OrderByDescending(file => file.CreationTimeUtc)
                    .ToList();

                for (var i = keepCount; i < files.Count; i++)
                {
                    files[i].Delete();
                }
            }
            catch
            {
                // Temp image cleanup should never block inspection.
            }
        }
        public static TeachingJob CreateDefaultJob(string jobName, System.Windows.Rect fallbackRect)
        {
            var job = new TeachingJob { JobName = jobName };
            if (!fallbackRect.IsEmpty && fallbackRect.Width > 0 && fallbackRect.Height > 0)
            {
                job.Rois.Add(RectToRoi(fallbackRect, "ROI 1"));
            }

            return job;
        }

        public static TeachingJob CloneJob(TeachingJob source)
        {
            if (source == null)
            {
                return null;
            }

            return new TeachingJob
            {
                JobName = source.JobName,
                ImagePath = source.ImagePath,
                OutputOffsetX = source.OutputOffsetX,
                OutputOffsetY = source.OutputOffsetY,
                OutputOffsetTheta = source.OutputOffsetTheta,
                Rois = source.Rois == null ? new List<RoiDefinition>() : source.Rois.Select(roi => roi.Clone()).ToList()
            };
        }

        public static System.Windows.Rect BuildBounds(IEnumerable<RoiDefinition> rois)
        {
            var list = rois == null ? new List<RoiDefinition>() : rois.Where(roi => roi != null && roi.IsTaught).ToList();
            if (!list.Any())
            {
                return System.Windows.Rect.Empty;
            }

            var left = list.Min(roi => Math.Min(roi.Column1, roi.Column2));
            var top = list.Min(roi => Math.Min(roi.Row1, roi.Row2));
            var right = list.Max(roi => Math.Max(roi.Column1, roi.Column2));
            var bottom = list.Max(roi => Math.Max(roi.Row1, roi.Row2));
            return new System.Windows.Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        public static RoiDefinition RectToRoi(System.Windows.Rect rect, string name)
        {
            return new RoiDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Row1 = rect.Top,
                Column1 = rect.Left,
                Row2 = rect.Bottom,
                Column2 = rect.Right,
                IsTaught = true
            };
        }
        private static string NormalizeTeachingKey(string sourceName)
        {
            string sourceNameSafe = sourceName;
            if (sourceNameSafe == null) sourceNameSafe = string.Empty;
            var text = sourceNameSafe.ToUpperInvariant();
            if (text.Contains("TOP"))
            {
                return "TOP";
            }

            if (text.Contains("SIDE"))
            {
                return "SIDE";
            }

            if (text.Contains("BOTTOM"))
            {
                return "BOTTOM";
            }

            return "COMMON";
        }
    }
}


