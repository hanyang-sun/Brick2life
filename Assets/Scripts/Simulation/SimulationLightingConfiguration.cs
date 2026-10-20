using System;
using System.Collections.Generic;
using UnityEngine;

namespace Brick2Life.Simulation
{
    /// <summary>Actual scene lighting snapshot. Does not claim to describe every rendering/material setting.</summary>
    [Serializable]
    public sealed class SimulationLightingConfiguration
    {
        public const int CurrentSchemaVersion = 2;
        public const string IdPrefix = "lighting-v2-";
        public int schemaVersion = CurrentSchemaVersion;
        public string unityVersion, activeColorSpace, ambientMode;
        public Color ambientSkyColor, ambientEquatorColor, ambientGroundColor;
        public float ambientIntensity, reflectionIntensity;
        public string qualityShadows, shadowResolution;
        public float shadowDistance;
        public int pixelLightCount;
        public Preset[] controllers;
        public Source[] lights;

        [Serializable] public sealed class Preset
        {
            public string objectPath, layout, brightness;
            public bool enabled, active, shadowsRequested;
            public float syntheticIntensityBudget;
            public float leftLightShare, ambientMultiplier;
            public Color baseAmbientColor;
        }

        [Serializable] public sealed class Source
        {
            public string objectPath, type, renderMode, shadows, shadowResolution, cookieName;
            public bool enabled, active, useColorTemperature;
            public int cullingMask;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public Color color;
            public float intensity, range, spotAngle, innerSpotAngle, colorTemperature, bounceIntensity,
                shadowStrength, shadowBias, shadowNormalBias, shadowNearPlane;
        }

        public static SimulationLightingConfiguration SnapshotScene()
        {
            var sources = new List<Source>();
            // Include inactive/disabled and additional scene lights, exclude non-scene assets.
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!light.gameObject.scene.IsValid()) continue;
                Vector3 p = light.transform.position;
                Quaternion q = light.transform.rotation;
                sources.Add(new Source
                {
                    objectPath = ObjectPath(light.transform), type = light.type.ToString(),
                    enabled = light.enabled, active = light.gameObject.activeInHierarchy,
                    worldPosition = new Vector3(F(p.x), F(p.y), F(p.z)),
                    worldRotation = new Quaternion(F(q.x), F(q.y), F(q.z), F(q.w)),
                    color = C(light.color), intensity = F(light.intensity), range = F(light.range),
                    spotAngle = F(light.spotAngle), innerSpotAngle = F(light.innerSpotAngle),
                    useColorTemperature = light.useColorTemperature, colorTemperature = F(light.colorTemperature),
                    bounceIntensity = F(light.bounceIntensity), cullingMask = light.cullingMask,
                    renderMode = light.renderMode.ToString(), shadows = light.shadows.ToString(),
                    shadowResolution = light.shadowResolution.ToString(), shadowStrength = F(light.shadowStrength),
                    shadowBias = F(light.shadowBias), shadowNormalBias = F(light.shadowNormalBias),
                    shadowNearPlane = F(light.shadowNearPlane), cookieName = light.cookie == null ? "" : light.cookie.name
                });
            }
            sources.Sort((a, b) => string.CompareOrdinal(a.objectPath, b.objectPath));
            var presets = new List<Preset>();
            foreach (var settings in UnityEngine.Object.FindObjectsByType<SimulationLightingSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                presets.Add(new Preset
                {
                    objectPath = ObjectPath(settings.transform), enabled = settings.enabled,
                    active = settings.gameObject.activeInHierarchy, layout = settings.CurrentLayout.ToString(),
                    brightness = settings.CurrentBrightness.ToString(), shadowsRequested = settings.ShadowsEnabled,
                    syntheticIntensityBudget = F(settings.IntensityBudget),
                    leftLightShare = F(settings.LeftLightShare), ambientMultiplier = F(settings.AmbientMultiplier),
                    baseAmbientColor = C(settings.BaseAmbientColor)
                });
            presets.Sort((a, b) => string.CompareOrdinal(a.objectPath, b.objectPath));
            return new SimulationLightingConfiguration
            {
                unityVersion = Application.unityVersion, activeColorSpace = QualitySettings.activeColorSpace.ToString(),
                ambientMode = RenderSettings.ambientMode.ToString(), ambientSkyColor = C(RenderSettings.ambientSkyColor),
                ambientEquatorColor = C(RenderSettings.ambientEquatorColor), ambientGroundColor = C(RenderSettings.ambientGroundColor),
                ambientIntensity = F(RenderSettings.ambientIntensity), reflectionIntensity = F(RenderSettings.reflectionIntensity),
                qualityShadows = QualitySettings.shadows.ToString(), shadowResolution = QualitySettings.shadowResolution.ToString(),
                shadowDistance = F(QualitySettings.shadowDistance), pixelLightCount = QualitySettings.pixelLightCount,
                controllers = presets.ToArray(), lights = sources.ToArray()
            };
        }

        private static string ObjectPath(Transform transform)
        {
            // No instance IDs: preserve identity across reloads of the same scene hierarchy.
            string path = "";
            for (Transform current = transform; current != null; current = current.parent)
                path = "/" + current.name + "[" + current.GetSiblingIndex() + "]" + path;
            return transform.gameObject.scene.path + path;
        }
        private static Color C(Color value) => new Color(F(value.r), F(value.g), F(value.b), F(value.a));
        private static float F(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("光照配置含无效数值，无法保存采集参数。");
            return value == 0 ? 0 : value;
        }
    }
}
