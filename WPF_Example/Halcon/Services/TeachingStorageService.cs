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

        public static string BuildTeachingPath(ParamBase param, string logicalName)
        {
            var basePath = param.GetExternalFilePath(EExternalFileType.Model, logicalName);
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return null;
            }

            return Path.ChangeExtension(basePath, ".json");
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

            var fileName = string.Format(
                "{0}_{1}.png",
                DateTime.Now.ToString("yyyyMMdd_HHmmssfff"),
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, fileName);
            image.WriteImage("png", 0, path);
            return path;
        }

        private static void CleanupTempImages(string directory, int keepCount)
        {
            try
            {
                var files = new DirectoryInfo(directory)
                    .GetFiles("*.png")
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
            var text = (sourceName ?? string.Empty).ToUpperInvariant();
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


