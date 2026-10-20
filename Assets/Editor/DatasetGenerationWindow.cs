using System;
using System.Diagnostics;
using System.IO;
using Brick2Life.Application;
using UnityEditor;
using UnityEngine;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Editor
{
    public sealed class DatasetGenerationWindow : EditorWindow
    {
        [SerializeField] private int layoutCount = DatasetPlan.DefaultLayoutCount, seed = DatasetPlan.DefaultSeed;
        [SerializeField] private DatasetLayoutFamilies families = DatasetLayoutFamilies.All;
        [SerializeField] private bool dailyLighting = true;
        [SerializeField] private string outputRoot;
        private string lastRun, launchError;
        private DatasetManifest manifest;
        private DatasetLauncherState launcher;
        private Process launchProcess;
        private Vector2 scroll;
        private double nextRefresh;
        private bool active;
        private static string Project => Path.GetFullPath(Path.Combine(UnityApplication.dataPath, ".."));
        private static string SessionKey => "Brick2Life.Dataset.LastRun." + Project;

        [MenuItem("Brick2Life/Dataset Generator")]
        public static void Open() => GetWindow<DatasetGenerationWindow>("测试集生成");

        private void OnEnable()
        {
            minSize = new Vector2(460, 390);
            if (string.IsNullOrEmpty(outputRoot)) outputRoot = Path.Combine(Project, "SimulationCaptures", "Datasets");
            lastRun = SessionState.GetString(SessionKey, "");
            EditorApplication.update += Refresh;
            Refresh();
        }
        private void OnDisable() { EditorApplication.update -= Refresh; }

        private void Refresh()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                if (launchProcess != null && launchProcess.HasExited)
                {
                    string error = launchProcess.StandardError.ReadToEnd();
                    launchProcess.StandardOutput.ReadToEnd();
                    if (launchProcess.ExitCode != 0) launchError = error;
                    launchProcess.Dispose(); launchProcess = null;
                }
                if (!string.IsNullOrEmpty(lastRun))
                {
                    string statePath = Path.Combine(lastRun, "launcher.json"), manifestPath = Path.Combine(lastRun, "manifest.json");
                    if (File.Exists(statePath)) launcher = DatasetJobFiles.Read<DatasetLauncherState>(statePath);
                    if (File.Exists(manifestPath)) manifest = DatasetJobFiles.Read<DatasetManifest>(manifestPath);
                }
                active = launchProcess != null || (launcher != null && launcher.status != "failed" && launcher.status != "exited"
                    && (manifest == null || manifest.status == "running"));
                if (!string.IsNullOrEmpty(launchError)) active = false;
                if (active && launcher != null && launcher.processId > 0 && launchProcess == null)
                {
                    bool running;
                    try { using (Process process = Process.GetProcessById(launcher.processId)) running = !process.HasExited; }
                    catch (ArgumentException) { running = false; }
                    if (!running) { active = false; launchError = "采集进程已退出但未完成，请查看日志：" + launcher.logPath; }
                }
            }
            catch (IOException) { /* Launcher may be publishing its next state. Read again on the next update. */ }
            catch (Exception error) { launchError = error.Message; }
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawControls();
            EditorGUILayout.EndScrollView();
        }

        private void DrawControls()
        {
            EditorGUILayout.LabelField("日常识别测试集", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("使用已保存的仿真场景，在独立后台工程采集。首次运行需要导入资源。", MessageType.Info);
            using (new EditorGUI.DisabledScope(active))
            {
                layoutCount = EditorGUILayout.IntField("不同布局数", layoutCount);
                seed = EditorGUILayout.IntField("随机种子", seed);
                families = (DatasetLayoutFamilies)EditorGUILayout.EnumFlagsField("布局类型", families) & DatasetLayoutFamilies.All;
                dailyLighting = EditorGUILayout.Toggle("五种日常光照", dailyLighting);
                outputRoot = EditorGUILayout.TextField("输出根目录", outputRoot);
                int lightingCount = dailyLighting ? 5 : 1;
                string validation = null;
                try { DatasetPlan.Validate(layoutCount, families, lightingCount); }
                catch (ArgumentException error) { validation = error.Message; }
                EditorGUILayout.LabelField("计划图片", validation == null ? $"{layoutCount} × {lightingCount} = {layoutCount * lightingCount} 张" : "参数无效");
                if (validation != null) EditorGUILayout.HelpBox(validation, MessageType.Warning);
                using (new EditorGUI.DisabledScope(validation != null))
                    if (GUILayout.Button("生成测试集", GUILayout.Height(30))) StartJob(lightingCount);
            }
            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(lastRun))
            {
                string status = manifest?.status ?? (active ? "准备资源 / 启动 Unity" : launcher?.status ?? "等待启动");
                EditorGUILayout.LabelField("状态", status);
                if (manifest != null)
                {
                    Rect bar = GUILayoutUtility.GetRect(20, 22);
                    EditorGUI.ProgressBar(bar, manifest.plannedImages == 0 ? 0 : (float)manifest.completedImages / manifest.plannedImages,
                        $"{manifest.completedImages} / {manifest.plannedImages}");
                    if (!string.IsNullOrEmpty(manifest.error)) EditorGUILayout.HelpBox(manifest.error, MessageType.Error);
                }
                EditorGUILayout.SelectableLabel(lastRun, EditorStyles.wordWrappedLabel, GUILayout.Height(38));
                using (new EditorGUI.DisabledScope(!Directory.Exists(lastRun)))
                    if (GUILayout.Button("打开输出目录")) EditorUtility.RevealInFinder(lastRun);
                using (new EditorGUI.DisabledScope(!active || !Directory.Exists(lastRun)))
                    if (GUILayout.Button("取消（保留已完成样本）")) File.WriteAllText(Path.Combine(lastRun, "cancel.request"), DateTime.UtcNow.ToString("O"));
            }
            if (!string.IsNullOrEmpty(launcher?.error)) EditorGUILayout.HelpBox(launcher.error, MessageType.Error);
            if (!string.IsNullOrEmpty(launchError)) EditorGUILayout.HelpBox(launchError, MessageType.Error);
        }

        private void StartJob(int lightingCount)
        {
            try
            {
                string script = Path.Combine(Project, "Tests", "GenerateDataset.ps1");
                if (!File.Exists(script)) throw new FileNotFoundException("Missing dataset launcher.", script);
                string run = Path.Combine(Path.GetFullPath(outputRoot), "dataset-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                var start = new ProcessStartInfo
                {
                    FileName = "powershell.exe", UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardError = true, RedirectStandardOutput = true,
                    Arguments = "-NoProfile -File " + Quote(script) + " -UnityPath " + Quote(EditorApplication.applicationPath)
                        + " -LayoutCount " + layoutCount + " -Seed " + seed + " -Families " + (int)families
                        + " -LightingCount " + lightingCount + " -RunDirectory " + Quote(run) + " -NoWait"
                };
                launchProcess = Process.Start(start);
                lastRun = run; SessionState.SetString(SessionKey, run);
                manifest = null; launcher = null; launchError = null; active = true;
            }
            catch (Exception error) { launchError = error.Message; }
        }
        private static string Quote(string value)
        {
            if (value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0) throw new ArgumentException("Paths cannot contain quotes or newlines.");
            return "\"" + value + "\"";
        }
    }
}
