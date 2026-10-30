using System;
using System.Collections.Generic;
using Brick2Life.Core;
using UnityEngine;

namespace Brick2Life.Simulation
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class SimulationBoardSession : MonoBehaviour
    {
        [Serializable] public struct PaletteEntry
        {
            public string colorId;
            public string label;
            public Material material;
        }
        [SerializeField] private Transform bricksRoot;
        [SerializeField] private PaletteEntry[] palette;
        private Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
        private BoardLayout layout;
        private readonly HashSet<GridPoint> reserved = new HashSet<GridPoint>();
        private string wallColor, towerColor;
        public bool DefenseEditing => wallColor != null;
        public string LastPlacementIssue { get; private set; }
        public event Action PlacementRulesChanged;
        public bool IsReserved(int x, int y) => reserved.Contains(new GridPoint(x, y));
        public bool IsPlayerColorAvailable(string colorId) => !DefenseEditing || colorId == wallColor || colorId == towerColor;
        public string PlayerColorLabel(string colorId, string original) => !DefenseEditing ? original :
            colorId == towerColor ? "防御塔（" + original + "）" : "城墙（" + original + "）";

        public void SetDefenseEditing(IEnumerable<GridPoint> cells, string wall, string tower)
        {
            if (string.IsNullOrWhiteSpace(wall) || string.IsNullOrWhiteSpace(tower) || wall == tower)
                throw new ArgumentException("墙与塔需要不同的公开调色板颜色。");
            reserved.Clear();
            foreach (GridPoint cell in cells)
            {
                if (!Definition.Contains(cell.X, cell.Y)) throw new ArgumentException("保留格不在棋盘内。");
                reserved.Add(cell);
            }
            wallColor = wall; towerColor = tower;
            PlacementRulesChanged?.Invoke();
        }
        private IReadOnlyList<PaletteEntry> readOnlyPalette = Array.Empty<PaletteEntry>();
        public IReadOnlyList<PaletteEntry> Palette => readOnlyPalette;
        public IReadOnlyList<BoardLayout.Brick> Bricks => RequireLayout().Bricks;
        public int Version => RequireLayout().Version;
        public BoardDefinition Definition => RequireLayout().Definition;
        public bool IsInitialized => layout != null;
        public bool IsEditingLocked { get; private set; }
        public event Action<bool> EditingLockChanged;

        public void SetEditingLocked(bool locked)
        {
            if (IsEditingLocked == locked) return;
            IsEditingLocked = locked;
            EditingLockChanged?.Invoke(locked);
        }

        private void Awake()
        {
            if (bricksRoot != null && palette != null) Initialize(bricksRoot, palette);
        }

        public void Initialize(Transform root, PaletteEntry[] entries)
        {
            if (layout != null) throw new InvalidOperationException("Board session is already initialized.");
            if (root == null || entries == null || entries.Length == 0) throw new ArgumentException("Board root and palette are required.");
            var colors = new List<string>();
            var materials = new HashSet<Material>();
            foreach (PaletteEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.label) || entry.material == null || !materials.Add(entry.material))
                    throw new ArgumentException("Palette entries require labels and distinct materials.");
                colors.Add(entry.colorId);
            }
            var candidate = new BoardLayout(new BoardDefinition(colorIds: colors));
            var candidateObjects = new Dictionary<string, GameObject>();
            foreach (Transform child in root)
            {
                Renderer renderer = child.GetComponent<Renderer>();
                int index = Array.FindIndex(entries, entry => renderer != null && entry.material == renderer.sharedMaterial);
                Vector3 position = child.position;
                int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.z);
                if (index < 0 || child.GetComponent<Collider>() == null ||
                    (position - new Vector3(x + 0.5f, 0.5f, y + 0.5f)).sqrMagnitude > 0.0001f ||
                    !candidate.TryAdd(x, y, entries[index].colorId, out BoardLayout.Brick brick))
                    throw new InvalidOperationException("Initial bricks must occupy valid, distinct unit cells with palette materials.");
                candidateObjects.Add(brick.BrickId, child.gameObject);
            }
            bricksRoot = root; palette = (PaletteEntry[])entries.Clone();
            readOnlyPalette = Array.AsReadOnly(palette);
            foreach (var pair in candidateObjects) objects.Add(pair.Key, pair.Value);
            layout = candidate;
        }
        private BoardLayout RequireLayout() => layout ?? throw new InvalidOperationException("Board session has not been initialized.");
        public BoardSnapshot Snapshot() => RequireLayout().Snapshot();
        public BoardLayout.Brick At(int x, int y) => RequireLayout().At(x, y);
        public bool CanPlace(int x, int y, string movingId = null)
        {
            LastPlacementIssue = IsReserved(x, y) ? "核心和出生点是系统保留格，请选择其他位置。" : "目标格已有积木或不在棋盘内。";
            bool allowed = !IsReserved(x, y) && RequireLayout().CanPlace(x, y, movingId);
            if (allowed) LastPlacementIssue = null;
            return allowed;
        }

        // Validate and prepare the complete view before replacing the live model. Local versions remain monotonic.
        public void ReplaceLayout(BoardSnapshot snapshot)
        {
            if (IsEditingLocked) throw new InvalidOperationException("战斗中布局已锁定，请结束本局或重试后再编辑。");
            var current = RequireLayout();
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!current.Definition.IsCompatible(snapshot.Definition)) throw new ArgumentException("Incompatible board definition.", nameof(snapshot));
            foreach (BoardCell cell in snapshot.Cells)
                if (!cell.IsKnown) throw new ArgumentException("Simulation requires a fully known layout.", nameof(snapshot));
            for (int y = 0; y < snapshot.Definition.Height; y++)
                for (int x = 0; x < snapshot.Definition.Width; x++)
                {
                    BoardCell cell = snapshot.At(x, y);
                    if (cell.Occupancy == Occupancy.Occupied && (IsReserved(x, y) || !IsPlayerColorAvailable(cell.ColorId)))
                        throw new ArgumentException("玩家积木不能占用系统目标，也只能使用墙和塔的颜色。", nameof(snapshot));
                }
            var candidate = new BoardLayout(new BoardSnapshot(current.Definition, checked(current.Version + 1), snapshot.Cells, SnapshotSource.Simulation));
            var prepared = new Dictionary<string, GameObject>();
            try
            {
                foreach (BoardLayout.Brick brick in candidate.Bricks)
                {
                    int index = Array.FindIndex(palette, entry => entry.colorId == brick.ColorId);
                    if (index < 0) throw new ArgumentException("Layout uses a color without a simulation material.", nameof(snapshot));
                    GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prepared.Add(brick.BrickId, instance);
                    instance.SetActive(false);
                    instance.name = "Brick_" + brick.ColorId;
                    instance.transform.SetParent(bricksRoot, false);
                    instance.transform.position = new Vector3(brick.GridX + 0.5f, 0.5f, brick.GridY + 0.5f);
                    instance.GetComponent<Renderer>().sharedMaterial = palette[index].material;
                }
            }
            catch
            {
                foreach (GameObject instance in prepared.Values) { instance.SetActive(false); Destroy(instance); }
                throw;
            }
            var previousObjects = objects;
            objects = prepared; layout = candidate;
            foreach (GameObject instance in previousObjects.Values) { instance.SetActive(false); Destroy(instance); }
            foreach (GameObject instance in objects.Values) instance.SetActive(true);
        }

        public bool TryAdd(int x, int y, string colorId, out BoardLayout.Brick added)
        {
            added = null;
            if (IsEditingLocked) return false;
            var model = RequireLayout();
            int index = Array.FindIndex(palette, entry => entry.colorId == colorId);
            if (!IsPlayerColorAvailable(colorId)) { LastPlacementIssue = "请选择城墙或防御塔。"; return false; }
            if (index < 0 || !CanPlace(x, y)) return false;
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                instance.name = "Brick_" + colorId;
                instance.transform.SetParent(bricksRoot, false);
                instance.transform.position = new Vector3(x + 0.5f, 0.5f, y + 0.5f);
                instance.GetComponent<Renderer>().sharedMaterial = palette[index].material;
                if (!model.TryAdd(x, y, colorId, out added)) throw new InvalidOperationException("Layout changed while adding a brick.");
                objects.Add(added.BrickId, instance);
                return true;
            }
            catch { instance.SetActive(false); Destroy(instance); throw; }
        }
        public bool TryDelete(string id)
        {
            if (IsEditingLocked) return false;
            if (id == null || !objects.TryGetValue(id, out GameObject instance) || !RequireLayout().TryDelete(id)) return false;
            objects.Remove(id); instance.SetActive(false); Destroy(instance);
            return true;
        }
        public bool TryMove(string id, int x, int y)
        {
            if (IsEditingLocked) return false;
            if (!CanPlace(x, y, id)) return false;
            if (!RequireLayout().TryMove(id, x, y)) return false;
            objects[id].transform.position = new Vector3(x + 0.5f, 0.5f, y + 0.5f);
            return true;
        }
    }
}
