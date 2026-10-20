using System;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class SimulationLightingSetup
{
    [MenuItem("Brick2Life/Simulation/Install lighting controls")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请退出 Play Mode 后配置场景光照。");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Brick2LifeSimulation")
            throw new InvalidOperationException("请先打开 Brick2LifeSimulation 场景。");
        Light reference = GameObject.Find("SimulationLight")?.GetComponent<Light>();
        Button template = Find<Button>("ColorButtonTemplate");
        Text description = Find<Text>("PreviewDescription");
        if (reference == null || template == null || description == null)
            throw new InvalidOperationException("仿真场景缺少原灯光或 UI 模板。");
        GameObject root = GameObject.Find("SimulationLighting");
        if (root == null) { root = new GameObject("SimulationLighting"); Undo.RegisterCreatedObjectUndo(root, "Install lighting"); }
        var settings = root.GetComponent<SimulationLightingSettings>();
        if (settings == null) settings = Undo.AddComponent<SimulationLightingSettings>(root);
        Light left = Spot(root.transform, "LeftWhiteLight", new Vector3(-3, 12, 5));
        Light right = Spot(root.transform, "RightWhiteLight", new Vector3(13, 12, 5));
        var config = new SerializedObject(settings);
        config.FindProperty("leftLight").objectReferenceValue = left;
        config.FindProperty("rightLight").objectReferenceValue = right;
        config.FindProperty("referenceLight").objectReferenceValue = reference;
        config.ApplyModifiedProperties();
        var controls = root.GetComponent<SimulationLightingControls>();
        if (controls == null) controls = Undo.AddComponent<SimulationLightingControls>(root);
        var ui = new SerializedObject(controls);
        ui.FindProperty("settings").objectReferenceValue = settings;
        ui.FindProperty("buttonTemplate").objectReferenceValue = template;
        ui.FindProperty("previewDescription").objectReferenceValue = description;
        ui.ApplyModifiedProperties();
        Undo.RecordObjects(new UnityEngine.Object[] { left, right, reference }, "Apply lighting preset");
        settings.ApplyConfiguration();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("仿真光照已配置：默认双侧白光、标准亮度、阴影开启。", root);
    }

    private static T Find<T>(string name) where T : Component
    {
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.name == name && item.gameObject.scene == EditorSceneManager.GetActiveScene()) return item;
        return null;
    }

    private static Light Spot(Transform parent, string name, Vector3 position)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject item = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(item, "Create experiment light");
            child = item.transform; child.SetParent(parent, false);
        }
        Light light = child.GetComponent<Light>();
        if (light == null) light = Undo.AddComponent<Light>(child.gameObject);
        Undo.RecordObjects(new UnityEngine.Object[] { light, child }, "Configure experiment light");
        child.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(5, 0, 5) - position));
        light.type = LightType.Spot; light.range = 40; light.spotAngle = 100; light.innerSpotAngle = 85;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.shadowStrength = 0.8f; light.shadowBias = 0.02f; light.shadowNormalBias = 0.02f;
        light.shadowNearPlane = 0.1f; light.shadowResolution = LightShadowResolution.High;
        return light;
    }
}
