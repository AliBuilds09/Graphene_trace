using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MyProject.Models;

namespace MyProject.Controllers
{
    public static class CsvImportController
    {
        private static void EnsureCanViewClinicalData()
        {
            // Admin should NOT see clinical data unless intended
            if (MyProject.Models.Session.IsAdmin)
            {
                throw new UnauthorizedAccessException("Admin is not permitted to view clinical data.");
            }
            // Patients and Clinicians permitted. Optional assignment checks implemented elsewhere.
        }

        public static List<SensorData> LoadFramesFromFiles(IEnumerable<string> files)
        {
            EnsureCanViewClinicalData();
            var frames = new List<SensorData>();
            foreach (var file in files.Where(f => f != null))
            {
                try
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith("._", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) continue;
                    var sd = ParseFileToSensorData(file);
                    frames.Add(sd);
                }
                catch
                {
                    // skip malformed files
                }
            }
            return frames;
        }

        public static List<SensorData> LoadFramesFromFolder(string folderPath)
        {
            EnsureCanViewClinicalData();
            var frames = new List<SensorData>();
            if (!Directory.Exists(folderPath)) return frames;
            var files = Directory.EnumerateFiles(folderPath, "*.csv", SearchOption.TopDirectoryOnly)
                                 .Where(f => !Path.GetFileName(f).StartsWith("._", StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(f => f);
            foreach (var file in files)
            {
                try
                {
                    var sd = ParseFileToSensorData(file);
                    frames.Add(sd);
                }
                catch
                {
                    // skip malformed files
                }
            }
            return frames;
        }

        public static List<SensorData> LoadAllFrames()
        {
            EnsureCanViewClinicalData();
            var frames = new List<SensorData>();

            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var baseDir = Directory.GetParent(exeDir)?.FullName ?? exeDir;
            var currentDir = Directory.GetCurrentDirectory();
            var parentDir = Directory.GetParent(currentDir)?.FullName;

            var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                baseDir,
                Path.Combine(baseDir, "GTLB-Data"),
                currentDir
            };
            if (!string.IsNullOrEmpty(parentDir))
            {
                candidateDirs.Add(parentDir);
                candidateDirs.Add(Path.Combine(parentDir, "GTLB-Data"));
            }

            var csvFiles = new List<string>();
            foreach (var dir in candidateDirs.Where(Directory.Exists))
            {
                csvFiles.AddRange(
                    Directory.EnumerateFiles(dir, "*.csv", SearchOption.TopDirectoryOnly)
                        .Where(f => !Path.GetFileName(f).StartsWith("._", StringComparison.OrdinalIgnoreCase))
                );
            }

            foreach (var file in csvFiles.OrderBy(f => f))
            {
                try
                {
                    var sd = ParseFileToSensorData(file);
                    frames.Add(sd);
                }
                catch
                {
                    // skip malformed files
                }
            }

            return frames;
        }

        private static SensorData ParseFileToSensorData(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var parts = name.Split('_');
            var userId = parts.FirstOrDefault() ?? "unknown";
            DateTime ts = File.GetCreationTime(filePath);
            if (parts.Length > 1 && DateTime.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                ts = parsed;
            }

            var text = File.ReadAllText(filePath);
            var tokens = text
                .Replace("\r\n", ",")
                .Replace("\n", ",")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList();

            var values = new List<int>(1024);
            foreach (var t in tokens)
            {
                if (int.TryParse(t, out var v))
                {
                    // constrain to 1..255 range
                    if (v < 1) v = 1; if (v > 255) v = 255;
                    values.Add(v);
                }
            }

            // ensure 1024 values for 32x32 matrix
            if (values.Count < 1024)
            {
                values.AddRange(Enumerable.Repeat(1, 1024 - values.Count));
            }
            else if (values.Count > 1024)
            {
                values = values.Take(1024).ToList();
            }

            var matrix = new int[32, 32];
            for (int i = 0; i < 32; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    matrix[i, j] = values[i * 32 + j];
                }
            }

            return new SensorData
            {
                UserId = userId,
                Timestamp = ts,
                Matrix = matrix
            };
        }

        public static string DescribeFirstFrame(List<SensorData> frames)
        {
            if (frames.Count == 0) return "No frames loaded.";
            var f = frames[0];
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < 32; i++)
                for (int j = 0; j < 32; j++)
                {
                    var v = f.Matrix[i, j];
                    if (v < min) min = v; if (v > max) max = v;
                }
            return $"First frame -> UserID: {f.UserId}, Timestamp: {f.Timestamp:yyyy-MM-dd}, Size: 32x32, Min: {min}, Max: {max}";
        }
    }
}