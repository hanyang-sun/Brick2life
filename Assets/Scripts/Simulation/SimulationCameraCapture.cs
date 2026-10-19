using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Brick2Life.Simulation
{
    [DisallowMultipleComponent]
    public sealed class SimulationCameraCapture : MonoBehaviour
    {
        private const string FolderName = "SimulationCaptures";
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private SimulationBrickEditor brickEditor;
        [SerializeField] private Text statusLabel;

        public string LastSavedPath { get; private set; }

        public void SaveImage()
        {
            try
            {
                if (sourceCamera == null || sourceCamera.targetTexture == null || brickEditor == null)
                    throw new InvalidOperationException("请配置顶置相机、RenderTexture 和积木编辑器。");
                DateTime capturedAt = DateTime.UtcNow;
                string frameId = "rgb_" + capturedAt.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
                Matrix4x4 view = sourceCamera.worldToCameraMatrix, projection = sourceCamera.projectionMatrix;
                GroundTruth truth = Snapshot(frameId, capturedAt);
                byte[] png = ReadCurrentRgb();
                if (truth.layoutVersion != brickEditor.LayoutVersion)
                    throw new InvalidOperationException("拍摄期间积木状态发生变化，请重新保存。");
                if (view != sourceCamera.worldToCameraMatrix || projection != sourceCamera.projectionMatrix)
                    throw new InvalidOperationException("拍摄期间相机参数发生变化，请重新保存。");
                string directory = Application.isEditor
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName))
                    : Path.Combine(Application.persistentDataPath, FolderName);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, truth.imageFile);
                SavePair(path, png, Encoding.UTF8.GetBytes(JsonUtility.ToJson(truth, true)));
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

        private GroundTruth Snapshot(string frameId, DateTime capturedAt)
        {
            RenderTexture target = sourceCamera.targetTexture;
            var truth = new GroundTruth
            {
                frameId = frameId, imageFile = frameId + ".png",
                capturedAtUtc = capturedAt.ToString("O", CultureInfo.InvariantCulture),
                unityFrame = Time.frameCount, layoutVersion = brickEditor.LayoutVersion,
                imageWidth = target.width, imageHeight = target.height,
                cells = new GroundTruthCell[SimulationLayout.Size * SimulationLayout.Size]
            };
            truth.boardCorners = SnapshotBoardCorners(truth);
            for (int y = 0; y < SimulationLayout.Size; y++)
                for (int x = 0; x < SimulationLayout.Size; x++)
                    truth.cells[y * SimulationLayout.Size + x] = new GroundTruthCell { gridX = x, gridY = y };
            foreach (SimulationLayout.Brick brick in brickEditor.Bricks)
            {
                GroundTruthCell cell = truth.cells[brick.GridY * SimulationLayout.Size + brick.GridX];
                cell.occupied = true; cell.brickId = brick.BrickId;
                cell.colorId = brick.ColorId; cell.layer = brick.Layer;
            }
            return truth;
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

        private static void SavePair(string imagePath, byte[] png, byte[] json)
        {
            string jsonPath = Path.ChangeExtension(imagePath, ".json");
            var created = new List<string>();
            try
            {
                WriteNewFile(imagePath + ".tmp", png, created);
                WriteNewFile(jsonPath + ".tmp", json, created);
                File.Move(imagePath + ".tmp", imagePath); created.Add(imagePath);
                // Publish the completed JSON last so readers can use it to find a complete pair.
                File.Move(jsonPath + ".tmp", jsonPath); created.Add(jsonPath);
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
            {
                created.Add(path);
                file.Write(bytes, 0, bytes.Length);
            }
        }

        [Serializable] private sealed class GroundTruth
        {
            public int schemaVersion = 1;
            public string frameId, imageFile, capturedAtUtc;
            public int unityFrame, layoutVersion, imageWidth, imageHeight;
            public int gridWidth = SimulationLayout.Size, gridHeight = SimulationLayout.Size;
            public Vector3 worldOrigin = Vector3.zero;
            public float cellSize = 1;
            public string coordinateConvention = "gridX=+worldX; gridY=+worldZ; layer=+worldY; image pixels originate at top-left";
            public string cellOrder = "cells[gridY * gridWidth + gridX]";
            public string boardCornerOrder = "boundary intersections: (0,0), (gridWidth,0), (gridWidth,gridHeight), (0,gridHeight); worldY=0";
            public string boardCornerPixelConvention = "top-left origin; integer pixel centers; x=viewportX*imageWidth-0.5; y=(1-viewportY)*imageHeight-0.5";
            public BoardCorner[] boardCorners;
            public GroundTruthCell[] cells;
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
