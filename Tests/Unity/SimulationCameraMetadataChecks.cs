using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Brick2Life.Simulation;
using UnityEditor;
using UnityEngine;

// Compiled only in the isolated Unity project created by RunSimulationCameraMetadataChecks.ps1.
public static class SimulationCameraMetadataChecks
{
    private static int assertions;
    private static Camera camera;
    private static SimulationCameraCapture capture;
    private static SimulationLayout layout;
    private static string output;

    [Serializable] private sealed class Result { public bool passed; public int assertions; public string error; }
    [Serializable] private sealed class Frame
    {
        public int schemaVersion, imageWidth, imageHeight, layoutVersion;
        public string frameId, cameraConfigurationId;
        public Configuration cameraConfiguration;
        public Cell[] cells;
        public Corner[] boardCorners;
    }
    [Serializable] private sealed class Configuration
    {
        public int schemaVersion, imageWidth, imageHeight;
        public string unityVersion, projectionType, matrixOrder, cameraSpaceConvention, gateFit;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public float verticalFieldOfViewDegrees, orthographicSize, aspect, nearClipPlane, farClipPlane;
        public bool usePhysicalProperties;
        public float focalLengthMillimeters;
        public Vector2 sensorSizeMillimeters, lensShift;
        public float[] worldToCameraMatrix, projectionMatrix;
    }
    [Serializable] private sealed class Cell { public int gridX, gridY; public bool occupied; public string colorId; }
    [Serializable] private sealed class Corner { public Vector3 worldPosition; public Vector2 imagePosition; }

    public static void Run()
    {
        output = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var result = new Result();
        try
        {
            Setup();
            Verify();
            result.passed = true;
        }
        catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
        result.assertions = assertions;
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(result, true));
        Debug.Log("CAMERA_METADATA_CHECKS: " + JsonUtility.ToJson(result));
        EditorApplication.Exit(result.passed ? 0 : 1);
    }

    private static void Setup()
    {
        camera = new GameObject("MetadataTestCamera").AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.SetPositionAndRotation(new Vector3(5, 12, 5), Quaternion.Euler(90, 0, 0));
        camera.orthographic = true;
        camera.orthographicSize = 5.5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 50;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.targetTexture = new RenderTexture(128, 128, 24);
        camera.targetTexture.Create();

        layout = new SimulationLayout();
        layout.TryAdd(3, 4, "red", out _);
        var editor = new GameObject("MetadataTestEditor").AddComponent<SimulationBrickEditor>();
        Set(editor, "layout", layout);
        capture = editor.gameObject.AddComponent<SimulationCameraCapture>();
        Set(capture, "sourceCamera", camera);
        Set(capture, "brickEditor", editor);

        var board = GameObject.CreatePrimitive(PrimitiveType.Plane);
        board.transform.position = new Vector3(5, 0, 5);
        board.GetComponent<Renderer>().sharedMaterial = Material(Color.gray);
        var brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brick.transform.position = new Vector3(3.5f, 0.5f, 4.5f);
        brick.GetComponent<Renderer>().sharedMaterial = Material(Color.red);
    }

    private static Material Material(Color color)
    {
        var material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        return material;
    }

    private static void Verify()
    {
        Frame first = Save("orthographic");
        Check(first.cameraConfiguration.projectionType == "orthographic", "Orthographic type was lost.");
        Check(first.cameraConfiguration.orthographicSize == 5.5f, "Orthographic half-height was lost.");
        Frame repeated = Save("repeated");
        Check(first.frameId != repeated.frameId, "Two captures reused the frame ID.");
        Check(first.cameraConfigurationId == repeated.cameraConfigurationId, "Identical configuration changed ID.");

        layout.TryAdd(8, 9, "blue", out _);
        Frame changedLayout = Save("layout-changed");
        Check(changedLayout.layoutVersion != first.layoutVersion, "Layout fixture did not change.");
        Check(changedLayout.cameraConfigurationId == first.cameraConfigurationId, "Layout changed camera ID.");
        Check(changedLayout.cells.Single(cell => cell.gridX == 8 && cell.gridY == 9).occupied, "GT lost a new brick.");

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(Save("french-culture").cameraConfigurationId == first.cameraConfigurationId, "Culture changed ID.");
        }
        finally { CultureInfo.CurrentCulture = previous; }

        camera.orthographicSize = 6;
        Check(Save("orthographic-size").cameraConfigurationId != first.cameraConfigurationId, "Size change reused ID.");
        camera.orthographicSize = 5.5f;
        Check(Save("restored-orthographic").cameraConfigurationId == first.cameraConfigurationId, "Restoring configuration did not restore ID.");

        camera.orthographic = false;
        camera.fieldOfView = 50;
        Frame perspective = Save("perspective");
        Check(perspective.cameraConfiguration.projectionType == "perspective", "Perspective type was lost.");
        Check(perspective.cameraConfiguration.verticalFieldOfViewDegrees == 50, "FOV was lost.");
        Check(perspective.cameraConfigurationId != first.cameraConfigurationId, "Projection change reused ID.");
        Check(!File.ReadAllBytes(Path.Combine(output, "orthographic.png")).SequenceEqual(
            File.ReadAllBytes(Path.Combine(output, "perspective.png"))), "Actual render did not change with projection.");

        camera.fieldOfView = 55;
        Check(Save("fov-changed").cameraConfigurationId != perspective.cameraConfigurationId, "FOV change reused ID.");
        camera.fieldOfView = 50;
        camera.transform.position += Vector3.right * 0.2f;
        Check(Save("position-changed").cameraConfigurationId != perspective.cameraConfigurationId, "Position change reused ID.");
        camera.transform.position -= Vector3.right * 0.2f;
        camera.transform.rotation = Quaternion.Euler(85, 0, 0);
        Check(Save("rotation-changed").cameraConfigurationId != perspective.cameraConfigurationId, "Rotation change reused ID.");
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);

        RenderTexture originalTarget = camera.targetTexture;
        camera.targetTexture = new RenderTexture(160, 96, 24);
        camera.targetTexture.Create();
        Frame resized = Save("resized");
        Check(resized.cameraConfigurationId != perspective.cameraConfigurationId, "Resolution change reused ID.");
        Check(resized.imageWidth == 160 && resized.imageHeight == 96, "Non-square size was lost.");
        camera.targetTexture = originalTarget;

        camera.nearClipPlane = 0.4f;
        Check(Save("clip-changed").cameraConfigurationId != perspective.cameraConfigurationId, "Clip change reused ID.");
        camera.nearClipPlane = 0.3f;
        Matrix4x4 projection = camera.projectionMatrix;
        projection.m02 += 0.03f;
        camera.projectionMatrix = projection;
        Check(Save("custom-projection").cameraConfigurationId != perspective.cameraConfigurationId, "Custom matrix reused ID.");
        camera.ResetProjectionMatrix();

        var clone = new GameObject("AnotherCameraInstance").AddComponent<Camera>();
        clone.CopyFrom(camera);
        clone.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        Set(capture, "sourceCamera", clone);
        Check(Save("second-instance", clone).cameraConfigurationId == perspective.cameraConfigurationId, "Instance identity affected configuration ID.");
        Set(capture, "sourceCamera", camera);
        UnityEngine.Object.DestroyImmediate(clone.gameObject);

        camera.usePhysicalProperties = true;
        camera.sensorSize = new Vector2(36, 24);
        camera.focalLength = 35;
        camera.lensShift = new Vector2(0.1f, 0.03f);
        Frame physical = Save("physical-properties");
        Check(physical.cameraConfiguration.usePhysicalProperties, "Physical flag was lost.");
        Check(physical.cameraConfiguration.focalLengthMillimeters == 35, "Focal length was lost.");
        Check(physical.cameraConfiguration.sensorSizeMillimeters == new Vector2(36, 24), "Sensor size was lost.");
        Check(physical.cameraConfiguration.lensShift == camera.lensShift, "Lens shift was lost.");
        camera.usePhysicalProperties = false;
        camera.lensShift = Vector2.zero;
        camera.fieldOfView = 50;

        RejectDuringRender(() => camera.fieldOfView += 1, "相机参数", "mid-render-fov");
        camera.fieldOfView = 50;
        RejectDuringRender(() => layout.TryAdd(1, 1, "green", out _), "积木状态", "mid-render-layout");
        RenderTexture replacement = new RenderTexture(128, 128, 24);
        replacement.Create();
        RejectDuringRender(() => camera.targetTexture = replacement, "相机参数", "mid-render-target");
        camera.targetTexture = originalTarget;

        MethodInfo finite = typeof(SimulationCameraCapture).GetMethod("Finite", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            bool rejected = false;
            try { finite.Invoke(null, new object[] { invalid }); }
            catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
            Check(rejected, "Invalid camera number was accepted.");
        }
        Check(!Directory.GetFiles(Path.Combine(output, "SimulationCaptures"), "*.tmp").Any(), "Temporary capture files leaked.");
    }

    private static Frame Save(string name, Camera active = null)
    {
        active = active ?? camera;
        string previousPath = capture.LastSavedPath;
        capture.SaveImage();
        string path = capture.LastSavedPath;
        Check(path != null && path != previousPath && File.Exists(path), "Capture did not publish a PNG: " + name);
        string jsonPath = Path.ChangeExtension(path, ".json");
        Check(File.Exists(jsonPath), "Capture did not publish JSON: " + name);
        Frame frame = JsonUtility.FromJson<Frame>(File.ReadAllText(jsonPath));
        Configuration config = frame.cameraConfiguration;
        Check(frame.schemaVersion == 3 && config != null && config.schemaVersion == 1, "Schema/version missing.");
        Check(frame.cameraConfigurationId.StartsWith("camera-v1-") && frame.cameraConfigurationId.Length == 74, "Invalid configuration ID.");
        Check(config.imageWidth == frame.imageWidth && config.imageHeight == frame.imageHeight, "Camera/frame size mismatch.");
        Check(config.worldPosition == active.transform.position && Quaternion.Angle(config.worldRotation, active.transform.rotation) < 0.001f, "World pose mismatch.");
        Check(config.worldToCameraMatrix.Length == 16 && config.projectionMatrix.Length == 16, "Matrix length mismatch.");
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                Check(Mathf.Abs(config.worldToCameraMatrix[row * 4 + column] - active.worldToCameraMatrix[row, column]) < 0.00001f, "View matrix order/value mismatch.");
                Check(Mathf.Abs(config.projectionMatrix[row * 4 + column] - active.projectionMatrix[row, column]) < 0.00001f, "Projection matrix order/value mismatch.");
            }
        Check(frame.cells.Length == 100 && frame.boardCorners.Length == 4, "Existing GT/corners changed.");
        var decoded = new Texture2D(2, 2);
        Check(decoded.LoadImage(File.ReadAllBytes(path)), "Could not decode captured PNG.");
        Check(decoded.width == config.imageWidth && decoded.height == config.imageHeight, "PNG/metadata size mismatch.");
        Check(decoded.GetPixels32().Count(pixel => pixel.r > 20 || pixel.g > 20 || pixel.b > 20) > 100, "Rendered PNG is empty.");
        UnityEngine.Object.DestroyImmediate(decoded);
        File.Copy(path, Path.Combine(output, name + ".png"));
        File.Copy(jsonPath, Path.Combine(output, name + ".json"));
        return frame;
    }

    private static void RejectDuringRender(Action change, string expectedWarning, string name)
    {
        string directory = Path.Combine(output, "SimulationCaptures");
        int before = Directory.GetFiles(directory).Length;
        bool invoked = false, warned = false;
        Camera.CameraCallback callback = current =>
        {
            if (current != camera || invoked) return;
            invoked = true;
            change();
        };
        Application.LogCallback log = (message, trace, type) => { if (type == LogType.Warning && message.Contains(expectedWarning)) warned = true; };
        Camera.onPreCull += callback;
        Application.logMessageReceived += log;
        try { capture.SaveImage(); }
        finally { Camera.onPreCull -= callback; Application.logMessageReceived -= log; }
        Check(invoked, "Render callback did not run: " + name);
        Check(warned, "Expected capture rejection was not reported: " + name);
        Check(Directory.GetFiles(directory).Length == before, "Rejected capture published files: " + name);
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
}
