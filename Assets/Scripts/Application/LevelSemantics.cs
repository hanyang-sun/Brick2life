using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Brick2Life.Core;

namespace Brick2Life.Application
{
    // Gameplay meaning of a confirmed cell. Phase one keeps the legacy vocabulary: core, wall, tower.
    public enum GameRole { None = 0, Core = 1, Wall = 2, Tower = 3 }

    public enum LevelIssueKind { MissingCore, MultipleCores, UnmappedColor }

    public sealed class LevelIssue
    {
        public LevelIssueKind Kind { get; }
        public string Message { get; }
        internal LevelIssue(LevelIssueKind kind, string message) { Kind = kind; Message = message; }
        public override string ToString() => Message;
    }

    // Explicit color-to-role table. Unmapped colors are reported, never silently guessed.
    public sealed class LevelRuleSet
    {
        private readonly Dictionary<string, GameRole> roles;
        private readonly ReadOnlyDictionary<string, GameRole> readOnlyRoles;
        public IReadOnlyDictionary<string, GameRole> Rules => readOnlyRoles;

        public LevelRuleSet(IEnumerable<KeyValuePair<string, GameRole>> rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            roles = new Dictionary<string, GameRole>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Key)) throw new ArgumentException("Rule colors must be nonblank.");
                if (!Enum.IsDefined(typeof(GameRole), rule.Value) || rule.Value == GameRole.None)
                    throw new ArgumentException("Rules must map to a gameplay role other than None.");
                if (roles.ContainsKey(rule.Key)) throw new ArgumentException("Duplicate color rule: " + rule.Key);
                roles.Add(rule.Key, rule.Value);
            }
            readOnlyRoles = new ReadOnlyDictionary<string, GameRole>(roles);
        }

        public static LevelRuleSet Create(params KeyValuePair<string, GameRole>[] rules) => new LevelRuleSet(rules);

        public bool TryGetRole(string colorId, out GameRole role)
        {
            role = GameRole.None;
            return !string.IsNullOrWhiteSpace(colorId) && roles.TryGetValue(colorId, out role);
        }

        public GameRole RoleOf(string colorId) => TryGetRole(colorId, out GameRole role) ? role : GameRole.None;
    }

    public readonly struct LevelCell
    {
        public bool Occupied { get; }
        public string ColorId { get; }
        public GameRole Role { get; }
        internal LevelCell(bool occupied, string colorId, GameRole role)
        { Occupied = occupied; ColorId = colorId ?? string.Empty; Role = role; }
        public override string ToString() => Occupied ? $"{ColorId}:{Role}" : "empty";
    }

    // Immutable gameplay projection of one confirmed snapshot. Source version keeps it comparable to the board.
    public sealed class LevelState
    {
        public BoardDefinition Definition { get; }
        public long SourceVersion { get; }
        public IReadOnlyList<LevelCell> Cells { get; }
        public IReadOnlyList<LevelIssue> Issues { get; }
        public bool IsPlayable => Issues.Count == 0;

        private LevelState(BoardDefinition definition, long sourceVersion, List<LevelCell> cells, List<LevelIssue> issues)
        {
            Definition = definition; SourceVersion = sourceVersion;
            Cells = new ReadOnlyCollection<LevelCell>(cells);
            Issues = new ReadOnlyCollection<LevelIssue>(issues);
        }

        public LevelCell At(int x, int y) => Cells[Definition.Index(x, y)];

        public int Count(GameRole role)
        {
            int count = 0;
            foreach (LevelCell cell in Cells) if (cell.Role == role) count++;
            return count;
        }

        public string Describe() =>
            $"核心 {Count(GameRole.Core)} · 城墙 {Count(GameRole.Wall)} · 防御塔 {Count(GameRole.Tower)}";

        public static LevelState FromSnapshot(BoardSnapshot snapshot, LevelRuleSet rules)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var cells = new List<LevelCell>(snapshot.Definition.CellCount);
            var issues = new List<LevelIssue>();
            var unmapped = new List<string>();
            int cores = 0;
            foreach (BoardCell state in snapshot.Cells)
            {
                if (state.Occupancy != Occupancy.Occupied)
                {
                    cells.Add(new LevelCell(false, null, GameRole.None));
                    continue;
                }
                if (!rules.TryGetRole(state.ColorId, out GameRole role))
                {
                    cells.Add(new LevelCell(true, state.ColorId, GameRole.None));
                    if (!unmapped.Contains(state.ColorId)) unmapped.Add(state.ColorId);
                    continue;
                }
                if (role == GameRole.Core) cores++;
                cells.Add(new LevelCell(true, state.ColorId, role));
            }
            foreach (string color in unmapped)
                issues.Add(new LevelIssue(LevelIssueKind.UnmappedColor, "颜色 " + color + " 没有配置游戏角色。"));
            if (cores == 0) issues.Add(new LevelIssue(LevelIssueKind.MissingCore, "关卡缺少核心，暂时不能提交战斗。"));
            else if (cores > 1) issues.Add(new LevelIssue(LevelIssueKind.MultipleCores, "关卡有 " + cores + " 个核心，只允许一个。"));
            return new LevelState(snapshot.Definition, snapshot.Version, cells, issues);
        }
    }
}
