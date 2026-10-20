using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brick2Life.Simulation
{
    /// <summary>Fixed, repeatable lighting experiments for the overhead RGB camera.</summary>
    [DisallowMultipleComponent]
    public sealed class SimulationLightingSettings : MonoBehaviour
    {
        public enum Layout { Left, Right, Dual, DirectionalReference }
        public enum Brightness { Dim, Standard, Bright }

        [SerializeField] private Light leftLight, rightLight, referenceLight;
        [SerializeField] private Layout layout = Layout.Dual;
        [SerializeField] private Brightness brightness = Brightness.Standard;
        [SerializeField] private bool shadowsEnabled = true;
        [Tooltip("Synthetic Unity intensity budget, not measured lux. Dual lights share this budget.")]
        [SerializeField, Min(0.01f)] private float standardIntensity = 3;
        [Tooltip("Left share of the fixed intensity budget in Dual mode only.")]
        [SerializeField, Range(0, 1)] private float leftLightShare = 0.5f;
        [SerializeField] private Color ambientColor = new Color(0.12f, 0.12f, 0.12f, 1);
        [Tooltip("Scales the base Flat ambient RGB; 0 removes ambient fill, 1 preserves the base color.")]
        [SerializeField, Range(0, 3)] private float ambientMultiplier = 1;

        public Layout CurrentLayout => layout;
        public Brightness CurrentBrightness => brightness;
        public bool ShadowsEnabled => shadowsEnabled;
        public float IntensityBudget => standardIntensity * Multiplier;
        public float LeftLightShare => leftLightShare;
        public float AmbientMultiplier => ambientMultiplier;
        public Color BaseAmbientColor => ambientColor;
        public event Action Changed;
        private float Multiplier => brightness == Brightness.Dim ? 0.5f : brightness == Brightness.Bright ? 1.5f : 1;
        private SavedState previous;

        private void OnEnable()
        {
            previous = new SavedState(leftLight, rightLight, referenceLight);
            ApplyConfiguration();
        }

        private void OnDisable()
        {
            previous?.Restore();
            previous = null;
        }

        public void SetLayout(int value)
        {
            if (!Enum.IsDefined(typeof(Layout), value)) return;
            layout = (Layout)value;
            ApplyConfiguration();
        }

        public void SetBrightness(int value)
        {
            if (!Enum.IsDefined(typeof(Brightness), value)) return;
            brightness = (Brightness)value;
            ApplyConfiguration();
        }

        public void ToggleShadows() { shadowsEnabled = !shadowsEnabled; ApplyConfiguration(); }

        public void SetLeftLightShare(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            leftLightShare = Mathf.Clamp01(value);
            ApplyConfiguration();
        }

        public void SetAmbientMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            ambientMultiplier = Mathf.Clamp(value, 0, 3);
            ApplyConfiguration();
        }

        public void ResetBalanceAndAmbient()
        {
            leftLightShare = 0.5f;
            ambientMultiplier = 1;
            ApplyConfiguration();
        }

        public void ApplyConfiguration()
        {
            if (leftLight == null || rightLight == null || referenceLight == null) return;
            float total = IntensityBudget;
            Configure(leftLight, layout == Layout.Left || layout == Layout.Dual, layout == Layout.Dual ? total * leftLightShare : total);
            Configure(rightLight, layout == Layout.Right || layout == Layout.Dual, layout == Layout.Dual ? total * (1 - leftLightShare) : total);
            // Directional intensity has different spatial behaviour; this is a separate reference.
            Configure(referenceLight, layout == Layout.DirectionalReference, Multiplier);
            RenderSettings.ambientMode = AmbientMode.Flat;
            // ambientIntensity only affects Skybox ambient, so Flat must scale the actual RGB.
            RenderSettings.ambientLight = new Color(ambientColor.r * ambientMultiplier,
                ambientColor.g * ambientMultiplier, ambientColor.b * ambientMultiplier, 1);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = 0;
            if (Application.isPlaying)
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowDistance = 60;
                QualitySettings.pixelLightCount = Mathf.Max(2, QualitySettings.pixelLightCount);
            }
            Changed?.Invoke();
        }

        private void Configure(Light light, bool active, float intensity)
        {
            light.enabled = active;
            light.color = Color.white;
            light.useColorTemperature = false;
            light.intensity = intensity;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = shadowsEnabled ? LightShadows.Soft : LightShadows.None;
        }

        private sealed class SavedState
        {
            private readonly AmbientMode mode = RenderSettings.ambientMode;
            private readonly Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
                ground = RenderSettings.ambientGroundColor;
            private readonly float ambient = RenderSettings.ambientIntensity, reflection = RenderSettings.reflectionIntensity;
            private readonly ShadowQuality shadows = QualitySettings.shadows;
            private readonly ShadowResolution resolution = QualitySettings.shadowResolution;
            private readonly float distance = QualitySettings.shadowDistance;
            private readonly int pixels = QualitySettings.pixelLightCount;
            private readonly LightState[] lights;
            public SavedState(params Light[] sources)
            {
                lights = new LightState[sources.Length];
                for (int i = 0; i < sources.Length; i++) lights[i] = new LightState(sources[i]);
            }
            public void Restore()
            {
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = ambient;
                RenderSettings.reflectionIntensity = reflection;
                QualitySettings.shadows = shadows;
                QualitySettings.shadowResolution = resolution;
                QualitySettings.shadowDistance = distance;
                QualitySettings.pixelLightCount = pixels;
                foreach (LightState light in lights) light.Restore();
            }
        }

        private sealed class LightState
        {
            private readonly Light light;
            private readonly bool enabled, temperature;
            private readonly Color color;
            private readonly float intensity;
            private readonly LightShadows shadows;
            private readonly LightRenderMode renderMode;
            public LightState(Light light)
            {
                this.light = light;
                if (light == null) return;
                enabled = light.enabled; temperature = light.useColorTemperature; color = light.color;
                intensity = light.intensity; shadows = light.shadows; renderMode = light.renderMode;
            }
            public void Restore()
            {
                if (light == null) return;
                light.enabled = enabled; light.useColorTemperature = temperature; light.color = color;
                light.intensity = intensity; light.shadows = shadows; light.renderMode = renderMode;
            }
        }
    }
}
