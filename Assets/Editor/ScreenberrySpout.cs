#if UNITY_EDITOR_WIN
using Brick2Life.Feedback;
using Brick2Life.Simulation;
using Klak.Spout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityApp = UnityEngine.Application;

namespace Brick2Life.Editor
{
    [InitializeOnLoad]
    internal static class ScreenberrySpout
    {
        private const string MenuPath = "Brick2Life/Screenberry/Send via Spout";
        private const string Preference = "Brick2Life.ScreenberrySpout.Enabled";
        private const string ProjectorSenderName = "Brick2Life Projector";
        private const string CameraSenderName = "Brick2Life Board RGB";
        private const string ResourcesPath = "Packages/jp.keijiro.klak.spout/Editor/SpoutResources.asset";

        private static SimulationProjectedLight light;
        private static ProjectionSimulator projection;
        private static SimulationCameraCapture cameraCapture;
        private static SpoutResources resources;
        private static SpoutSender projectorSender;
        private static SpoutSender cameraSender;
        private static RenderTexture frame;
        private static bool sending;
        private static bool originalRunInBackground;

        static ScreenberrySpout()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Refresh;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            SessionState.SetBool(Preference, !SessionState.GetBool(Preference, false));
            Refresh();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(Preference, false));
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Refresh();
            if (state == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        private static void Refresh()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Preference, false))
            {
                Stop();
                return;
            }
            if (sending) return;

            var api = SystemInfo.graphicsDeviceType;
            if (api != GraphicsDeviceType.Direct3D11 && api != GraphicsDeviceType.Direct3D12)
            {
                Debug.LogError("Screenberry Spout 需要 Unity 使用 Direct3D 11 或 12。");
                return;
            }
            resources = AssetDatabase.LoadAssetAtPath<SpoutResources>(ResourcesPath);
            if (resources == null)
            {
                Debug.LogError("找不到 KlakSpout 的 SpoutResources 资源。");
                return;
            }

            originalRunInBackground = UnityApp.runInBackground;
            UnityApp.runInBackground = true;
            sending = true;
            EditorApplication.update += SendFrame;
        }

        private static void SendFrame()
        {
            SendProjectorFrame();

            if (cameraCapture == null) cameraCapture = Object.FindFirstObjectByType<SimulationCameraCapture>();
            RenderTexture cameraOutput = cameraCapture != null ? cameraCapture.OutputTexture : null;
            if (cameraOutput != null && cameraOutput.IsCreated())
                Publish(ref cameraSender, CameraSenderName, cameraOutput);
            else
                ReleaseSender(ref cameraSender);
        }

        private static void SendProjectorFrame()
        {
            if (light == null)
            {
                light = Object.FindFirstObjectByType<SimulationProjectedLight>();
                projection = light != null ? light.GetComponent<ProjectionSimulator>() : null;
            }

            RenderTexture output = light != null ? light.OutputTexture : null;
            if (output != null && (frame == null || frame.width != output.width || frame.height != output.height))
            {
                ReleaseFrame();
                frame = new RenderTexture(output.width, output.height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Screenberry Projector Frame",
                    hideFlags = HideFlags.HideAndDontSave
                };
                frame.Create();
            }
            if (frame == null) return;

            Texture image = Texture2D.blackTexture;
            if (light != null && light.isActiveAndEnabled && projection != null &&
                projection.IsVisible && !ProjectionCaptureGate.IsSuppressed)
            {
                if (light.CalibrationPattern != null) image = light.CalibrationPattern;
                else if (output != null && projection.LastPlan != null) image = output;
            }
            RenderTexture previous = RenderTexture.active;
            try { Graphics.Blit(image, frame); }
            finally { RenderTexture.active = previous; }

            Publish(ref projectorSender, ProjectorSenderName, frame);
        }

        private static void Publish(ref SpoutSender sender, string name, RenderTexture texture)
        {
            if (sender == null)
            {
                var host = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                Object.DontDestroyOnLoad(host);
                sender = host.AddComponent<SpoutSender>();
                sender.SetResources(resources);
                sender.spoutName = name;
                sender.captureMethod = CaptureMethod.Texture;
                sender.sourceTexture = texture;
                host.SetActive(true);
                Debug.Log($"Screenberry Spout: {name} ({texture.width} × {texture.height})");
            }
            else sender.sourceTexture = texture;
        }

        private static void Stop()
        {
            if (!sending) return;
            EditorApplication.update -= SendFrame;
            UnityApp.runInBackground = originalRunInBackground;
            sending = false;
            ReleaseSender(ref projectorSender);
            ReleaseSender(ref cameraSender);
            ReleaseFrame();
            light = null;
            projection = null;
            cameraCapture = null;
        }

        private static void ReleaseSender(ref SpoutSender sender)
        {
            if (sender != null)
            {
                sender.gameObject.SetActive(false);
                Object.DestroyImmediate(sender.gameObject);
            }
            sender = null;
        }

        private static void ReleaseFrame()
        {
            if (frame == null) return;
            frame.Release();
            Object.DestroyImmediate(frame);
            frame = null;
        }
    }
}
#endif
