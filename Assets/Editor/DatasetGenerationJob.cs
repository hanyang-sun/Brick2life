using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Brick2Life.Editor
{
    [Serializable]
    internal sealed class DatasetRequest
    {
        public int schemaVersion = 1, layoutCount = 20, seed = 20261020, families = 1023, lightingCount = 5;
        public string outputDirectory, workDirectory;
    }

    [Serializable]
    internal sealed class DatasetManifest
    {
        public int schemaVersion = 1;
        public string generatorVersion, status = "running", startedAtUtc, finishedAtUtc, error;
        public int seed, plannedImages, completedImages, lightingCount, selectedFamilies;
        public string sourceSnapshotFile = "source-snapshot.json", boardId;
        public string[] palette;
        public int definitionVersion;
        public List<DatasetLayoutRecord> layouts = new List<DatasetLayoutRecord>();
        public List<DatasetSampleRecord> samples = new List<DatasetSampleRecord>();
    }

    [Serializable]
    internal sealed class DatasetLayoutRecord
    {
        public int layoutIndex, occupiedCells;
        public string layoutGroupId, family;
    }

    [Serializable]
    internal sealed class DatasetSampleRecord
    {
        public int sampleIndex, layoutIndex, occupiedCells, width, height, layoutVersion;
        public string frameId, layoutGroupId, family, lightingPreset, image, groundTruth;
        public string imageSha256, groundTruthSha256, cameraConfigurationId, lightingConfigurationId;
    }

    [Serializable]
    internal sealed class DatasetLauncherState
    {
        public string status, error, workDirectory, logPath;
        public int processId, exitCode;
    }

    internal static class DatasetJobFiles
    {
        public static T Read<T>(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return JsonUtility.FromJson<T>(reader.ReadToEnd());
        }

        public static void Write(string path, object value)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
