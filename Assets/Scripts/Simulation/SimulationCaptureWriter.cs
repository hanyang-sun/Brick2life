using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Brick2Life.Simulation
{
    public static class SimulationCaptureWriter
    {
        // A completed JSON is the publication marker for its PNG. Existing files are never overwritten.
        public static string Save(string directory, SimulationCapture frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            Directory.CreateDirectory(directory);
            string imagePath = Path.Combine(directory, frame.FrameId + ".png");
            string jsonPath = Path.ChangeExtension(imagePath, ".json");
            var created = new List<string>();
            try
            {
                WriteNewFile(imagePath + ".tmp", frame.CopyRgbPng(), created);
                WriteNewFile(jsonPath + ".tmp", Encoding.UTF8.GetBytes(frame.GroundTruthJson), created);
                File.Move(imagePath + ".tmp", imagePath); created.Add(imagePath);
                File.Move(jsonPath + ".tmp", jsonPath); created.Add(jsonPath);
                return imagePath;
            }
            catch
            {
                foreach (string path in created)
                {
                    try { File.Delete(path); }
                    catch (Exception error) { Debug.LogWarning("保存失败后的文件清理未完成：" + path + "；" + error.Message); }
                }
                throw;
            }
        }

        private static void WriteNewFile(string path, byte[] bytes, List<string> created)
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { created.Add(path); file.Write(bytes, 0, bytes.Length); }
        }
    }
}
