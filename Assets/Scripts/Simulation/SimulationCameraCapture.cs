using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Brick2Life.Core;
using Brick2Life.Vision;
using UnityEngine;
using UnityEngine.UI;
using UnityApplication = UnityEngine.Application;

namespace Brick2Life.Simulation
{
    [DisallowMultipleComponent]
    public sealed class SimulationCameraCapture : MonoBehaviour
    {
        private const string FolderName = "SimulationCaptures";
        [SerializeField] private Camera sourceCamera;
        private SimulationBoardSession board;
        [SerializeField] private Text statusLabel;

        public string LastSavedPath { get; private set; }
        public void Bind(SimulationBoardSession session) => board = session ?? throw new ArgumentNullException(nameof(session));

        public void SaveImage()
        {
            try
            {
                SimulationCapture frame = CaptureFrame();
                string directory = UnityApplication.isEditor
                    ? Path.GetFullPath(Path.Combine(UnityApplication.dataPath, "..", FolderName))
                    : Path.Combine(UnityApplication.persistentDataPath, FolderName);
                string path = SimulationCaptureWriter.Save(directory, frame);
                LastSavedPath = path;
                if (statusLabel != null)
                    statusLabel.text = $"已保存 PNG + JSON\n{FolderName}/";
                Debug.Log("RGB 与 Ground Truth 已保存：" + path + "；" + Path.ChangeExtension(path, ".json"), this);
            }
            catch (Exception error)
            {
                if (statusLabel != null) statusLabel.text = "保存失败：" + error.Message;
                Debug.LogWarning("RGB 与 Ground Truth 保存失败：" + error.Message, this);
            }
        }

        // Programmatic capture returns the current result or throws; it does not depend on a UI status label or write files.
        public SimulationCapture CaptureFrame()
        {
            if (sourceCamera == null || sourceCamera.targetTexture == null || board == null || !board.IsInitialized)
                throw new InvalidOperationException("请配置顶置相机、RenderTexture 和独立棋盘会话。");
            DateTime capturedAt = DateTime.UtcNow;
            string frameId = "rgb_" + capturedAt.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
            RenderTexture target = sourceCamera.targetTexture;
            BoardSnapshot frozen = board.Snapshot();
            GroundTruth truth = Snapshot(frameId, capturedAt, frozen);
            byte[] png = ReadCurrentRgb();
            if (truth.layoutVersion != board.Version)
                throw new InvalidOperationException("拍摄期间积木状态发生变化，请重新保存。");
            if (sourceCamera.targetTexture != target || truth.cameraConfigurationId != ConfigurationId(SnapshotCamera(target)))
                throw new InvalidOperationException("拍摄期间相机参数发生变化，请重新保存。");
            if (truth.lightingConfigurationId != ConfigurationId(SimulationLightingConfiguration.SnapshotScene(), SimulationLightingConfiguration.IdPrefix))
                throw new InvalidOperationException("拍摄期间光照参数发生变化，请重新保存。");
            return new SimulationCapture(frameId, new DateTimeOffset(capturedAt), target.width, target.height,
                png, frozen, JsonUtility.ToJson(truth, true));
        }

        private GroundTruth Snapshot(string frameId, DateTime capturedAt, BoardSnapshot snapshot)
        {
            RenderTexture target = sourceCamera.targetTexture;
            var truth = new GroundTruth
            {
                frameId = frameId, imageFile = frameId + ".png",
                capturedAtUtc = capturedAt.ToString("O", CultureInfo.InvariantCulture),
                unityFrame = Time.frameCount, layoutVersion = (int)snapshot.Version,
                imageWidth = target.width, imageHeight = target.height,
                cameraConfiguration = SnapshotCamera(target),
                lightingConfiguration = SimulationLightingConfiguration.SnapshotScene(),
                cells = new GroundTruthCell[snapshot.Definition.CellCount]
            };
            truth.cameraConfigurationId = ConfigurationId(truth.cameraConfiguration);
            truth.lightingConfigurationId = ConfigurationId(truth.lightingConfiguration, SimulationLightingConfiguration.IdPrefix);
            truth.boardCorners = SnapshotBoardCorners(truth);
            for (int y = 0; y < snapshot.Definition.Height; y++)
                for (int x = 0; x < snapshot.Definition.Width; x++)
                {
                    BoardCell cell = snapshot.At(x, y);
                    truth.cells[snapshot.Definition.Index(x, y)] = new GroundTruthCell
                    {
                        gridX = x, gridY = y, occupied = cell.Occupancy == Occupancy.Occupied,
                        brickId = cell.BrickId ?? "", colorId = cell.ColorId ?? "", layer = 0
                    };
                }
            return truth;
        }

        private CameraConfiguration SnapshotCamera(RenderTexture target)
        {
            Vector3 position = sourceCamera.transform.position;
            Quaternion rotation = sourceCamera.transform.rotation;
            Rect rect = sourceCamera.rect;
            return new CameraConfiguration
            {
                unityVersion = UnityApplication.unityVersion,
                projectionType = sourceCamera.orthographic ? "orthographic" : "perspective",
                imageWidth = target.width, imageHeight = target.height,
                worldPosition = new Vector3(Finite(position.x), Finite(position.y), Finite(position.z)),
                worldRotation = new Quaternion(Finite(rotation.x), Finite(rotation.y), Finite(rotation.z), Finite(rotation.w)),
                verticalFieldOfViewDegrees = Finite(sourceCamera.fieldOfView),
                orthographicSize = Finite(sourceCamera.orthographicSize),
                aspect = Finite(sourceCamera.aspect),
                nearClipPlane = Finite(sourceCamera.nearClipPlane),
                farClipPlane = Finite(sourceCamera.farClipPlane),
                viewport = new CameraViewport
                {
                    x = Finite(rect.x), y = Finite(rect.y),
                    width = Finite(rect.width), height = Finite(rect.height)
                },
                usePhysicalProperties = sourceCamera.usePhysicalProperties,
                focalLengthMillimeters = Finite(sourceCamera.focalLength),
                sensorSizeMillimeters = new Vector2(Finite(sourceCamera.sensorSize.x), Finite(sourceCamera.sensorSize.y)),
                lensShift = new Vector2(Finite(sourceCamera.lensShift.x), Finite(sourceCamera.lensShift.y)),
                gateFit = sourceCamera.gateFit.ToString(),
                worldToCameraMatrix = MatrixValues(sourceCamera.worldToCameraMatrix),
                projectionMatrix = MatrixValues(sourceCamera.projectionMatrix)
            };
        }

        private static float Finite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("相机配置含无效数值，无法保存采集参数。");
            return value == 0 ? 0 : value;
        }

        private static float[] MatrixValues(Matrix4x4 matrix)
        {
            var values = new float[16];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    values[row * 4 + column] = Finite(matrix[row, column]);
            return values;
        }

        private static string ConfigurationId(object configuration, string prefix = "camera-v1-")
        {
            // Fixed schema and Unity serializer; frame/time/layout are intentionally outside this DTO.
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(configuration));
            using (SHA256 hash = SHA256.Create())
                return prefix + BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private BoardCorner[] SnapshotBoardCorners(GroundTruth truth)
        {
            // Boundary intersections, not cell centers; the order is independent of camera rotation.
            var intersections = new[] { new Vector2Int(0, 0), new Vector2Int(truth.gridWidth, 0),
                new Vector2Int(truth.gridWidth, truth.gridHeight), new Vector2Int(0, truth.gridHeight) };
            var corners = new BoardCorner[intersections.Length];
            for (int i = 0; i < intersections.Length; i++)
            {
                Vector2Int grid = intersections[i];
                Vector3 world = truth.worldOrigin + new Vector3(grid.x * truth.cellSize, 0, grid.y * truth.cellSize);
                Vector3 viewport = sourceCamera.WorldToViewportPoint(world);
                if (viewport.z < sourceCamera.nearClipPlane || viewport.z > sourceCamera.farClipPlane)
                    throw new InvalidOperationException("棋盘角点不在相机的有效深度范围内，请检查顶置相机。");
                corners[i] = new BoardCorner
                {
                    gridX = grid.x, gridY = grid.y, worldPosition = world,
                    // PNG row zero is at the top; integer coordinates denote pixel centers.
                    imagePosition = new Vector2(viewport.x * truth.imageWidth - 0.5f,
                        (1 - viewport.y) * truth.imageHeight - 0.5f)
                };
            }
            return corners;
        }

        [Serializable] private sealed class GroundTruth
        {
            public int schemaVersion = 3;
            public string frameId, imageFile, capturedAtUtc;
            public int unityFrame, layoutVersion, imageWidth, imageHeight;
            public string cameraConfigurationId;
            public CameraConfiguration cameraConfiguration;
            public string lightingConfigurationId;
            public SimulationLightingConfiguration lightingConfiguration;
            public int gridWidth = BoardDefinition.Size, gridHeight = BoardDefinition.Size;
            public Vector3 worldOrigin = Vector3.zero;
            public float cellSize = 1;
            public string coordinateConvention = "gridX=+worldX; gridY=+worldZ; layer=+worldY; image pixels originate at top-left";
            public string cellOrder = "cells[gridY * gridWidth + gridX]";
            public string boardCornerOrder = "boundary intersections: (0,0), (gridWidth,0), (gridWidth,gridHeight), (0,gridHeight); worldY=0";
            public string boardCornerPixelConvention = "top-left origin; integer pixel centers; x=viewportX*imageWidth-0.5; y=(1-viewportY)*imageHeight-0.5";
            public BoardCorner[] boardCorners;
            public GroundTruthCell[] cells;
        }

        [Serializable] private sealed class CameraConfiguration
        {
            public int schemaVersion = 1;
            public string unityVersion, projectionType;
            public int imageWidth, imageHeight;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public float verticalFieldOfViewDegrees, orthographicSize, aspect, nearClipPlane, farClipPlane;
            public CameraViewport viewport;
            public bool usePhysicalProperties;
            public float focalLengthMillimeters;
            public Vector2 sensorSizeMillimeters, lensShift;
            public string gateFit;
            public string matrixOrder = "row-major; values[row * 4 + column]";
            public string cameraSpaceConvention = "Unity worldToCameraMatrix: right=+X; up=+Y; forward=-Z; clip=projectionMatrix*worldToCameraMatrix*worldPoint";
            public float[] worldToCameraMatrix, projectionMatrix;
        }

        [Serializable] private sealed class CameraViewport
        {
            public float x, y, width, height;
        }

        [Serializable] private sealed class BoardCorner
        {
            public int gridX, gridY;
            public Vector3 worldPosition;
            public Vector2 imagePosition;
        }

        [Serializable] private sealed class GroundTruthCell
        {
            public int gridX, gridY, layer;
            public bool occupied;
            public string brickId = "", colorId = "";
        }

        // RGB and the public camera matrices only. Ground truth cells are not read.
        public SimulationObservationImage CaptureObservationImage()
        {
            if (sourceCamera == null || sourceCamera.targetTexture == null)
                throw new InvalidOperationException("请配置顶置相机和 RenderTexture。");
            RenderTexture target = sourceCamera.targetTexture;
            double[] view = Matrix(sourceCamera.worldToCameraMatrix);
            double[] projection = Matrix(sourceCamera.projectionMatrix);
            byte[] rgb = ReadTopDown(target);
            if (sourceCamera.targetTexture != target)
                throw new InvalidOperationException("拍摄期间相机目标纹理发生变化。");
            return new SimulationObservationImage(target.width, target.height, rgb, DateTimeOffset.UtcNow, view, projection);
        }

        public GridSample[][] BuildTopSamples(BoardDefinition definition, VisionCalibration calibration, int imageWidth, int imageHeight)
        {
            if (sourceCamera == null || sourceCamera.targetTexture == null)
                throw new InvalidOperationException("请配置顶置相机和 RenderTexture。");
            if (imageWidth != sourceCamera.targetTexture.width || imageHeight != sourceCamera.targetTexture.height)
                throw new InvalidOperationException("采样尺寸与相机画面不一致。");
            return CameraProjection.CreateSamples(definition, calibration, ProjectTop);
        }

        private bool ProjectTop(double x, double y, double z, out double pixelX, out double pixelY)
        {
            Vector3 viewport = sourceCamera.WorldToViewportPoint(new Vector3((float)x, (float)y, (float)z));
            int width = sourceCamera.targetTexture.width, height = sourceCamera.targetTexture.height;
            pixelX = viewport.x * width - 0.5;
            pixelY = (1.0 - viewport.y) * height - 0.5;
            return viewport.z >= sourceCamera.nearClipPlane && viewport.z <= sourceCamera.farClipPlane;
        }

        private static double[] Matrix(Matrix4x4 matrix)
        {
            var values = new double[16];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    values[row * 4 + column] = matrix[row, column];
            return values;
        }

        private byte[] ReadTopDown(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                sourceCamera.Render();
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                image.Apply(false, false);
                Color32[] pixels = image.GetPixels32();
                int width = target.width, height = target.height;
                var rgb = new byte[width * height * 3];
                for (int y = 0; y < height; y++)
                {
                    int sourceRow = (height - 1 - y) * width;
                    int targetIndex = y * width * 3;
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[sourceRow + x];
                        rgb[targetIndex++] = pixel.r;
                        rgb[targetIndex++] = pixel.g;
                        rgb[targetIndex++] = pixel.b;
                    }
                }
                return rgb;
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Destroy(image);
            }
        }

        private byte[] ReadCurrentRgb()
        {
            RenderTexture target = sourceCamera.targetTexture;
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                // Render now so edits made this frame are included in the saved image.
                sourceCamera.Render();
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                image.Apply(false, false);
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Destroy(image);
            }
        }
    }
}
