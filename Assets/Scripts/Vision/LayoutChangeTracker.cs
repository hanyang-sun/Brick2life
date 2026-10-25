using System;
using System.Collections.Generic;
using Brick2Life.Core;

namespace Brick2Life.Vision
{
    public enum LayoutEventKind { Add, Remove, Move }

    public sealed class LayoutChangeEvent
    {
        public LayoutEventKind Kind { get; }
        public GridPoint Cell { get; }
        public GridPoint? From { get; }
        public string ColorId { get; }
        public DateTimeOffset Timestamp { get; }
        public bool Ambiguous { get; }

        public LayoutChangeEvent(LayoutEventKind kind, GridPoint cell, string colorId, DateTimeOffset timestamp, GridPoint? from = null, bool ambiguous = false)
        {
            if (!Enum.IsDefined(typeof(LayoutEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(colorId)) throw new ArgumentException("Color is required.", nameof(colorId));
            if (kind == LayoutEventKind.Move && from == null) throw new ArgumentException("A move needs a source cell.", nameof(from));
            if (kind != LayoutEventKind.Move && from != null) throw new ArgumentException("Only a move has a source cell.", nameof(from));
            Kind = kind; Cell = cell; From = from; ColorId = colorId; Timestamp = timestamp.ToUniversalTime(); Ambiguous = ambiguous;
        }

        public override string ToString()
        {
            string time = Timestamp.ToLocalTime().ToString("HH:mm:ss");
            string color = Label(ColorId);
            string cell = "(" + (Cell.X + 1) + "," + (Cell.Y + 1) + ")";
            if (Kind == LayoutEventKind.Move)
                return time + " 移动 (" + (From.Value.X + 1) + "," + (From.Value.Y + 1) + ")→" + cell + " " + color;
            string name = Kind == LayoutEventKind.Add ? "增加" : "删除";
            return time + " " + name + " " + cell + " " + color + (Ambiguous ? "（同色多块，未判定移动）" : "");
        }

        private static string Label(string colorId)
        {
            switch (colorId)
            {
                case "red": return "红";
                case "green": return "绿";
                case "blue": return "蓝";
                case "yellow": return "黄";
                default: return colorId;
            }
        }
    }

    public sealed class LayoutTrackStep
    {
        public bool Confirmable { get; }
        public bool EstablishedBaseline { get; }
        public IReadOnlyList<LayoutChangeEvent> NewEvents { get; }
        public IReadOnlyList<BoardCell> StableCells { get; }
        public int Streak { get; }
        public int RequiredFrames { get; }

        internal LayoutTrackStep(bool confirmable, bool establishedBaseline, IReadOnlyList<LayoutChangeEvent> newEvents,
            IReadOnlyList<BoardCell> stableCells, int streak, int requiredFrames)
        {
            Confirmable = confirmable; EstablishedBaseline = establishedBaseline; NewEvents = newEvents;
            StableCells = stableCells; Streak = streak; RequiredFrames = requiredFrames;
        }
    }

    // Confirms a layout only after several identical, fully known frames.
    // The first stable layout is a baseline and does not emit an add for every brick.
    public sealed class LayoutChangeTracker
    {
        private static readonly IReadOnlyList<LayoutChangeEvent> None = new LayoutChangeEvent[0];
        private readonly int required;
        private readonly List<LayoutChangeEvent> events = new List<LayoutChangeEvent>();
        private BoardDefinition definition;
        private BoardCell[] pending;
        private BoardCell[] stable;
        private int streak;

        public LayoutChangeTracker(int requiredFrames = 3)
        {
            if (requiredFrames < 1) throw new ArgumentOutOfRangeException(nameof(requiredFrames));
            required = requiredFrames;
        }

        public int RequiredFrames => required;
        public int Streak => streak;
        public bool HasBaseline => stable != null;
        public IReadOnlyList<LayoutChangeEvent> Events => events;

        public LayoutTrackStep Observe(FrameObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (definition != null && !definition.IsCompatible(observation.Definition))
                throw new ArgumentException("Board definition changed.");
            definition = observation.Definition;
            if (!observation.Accepted || observation.Cells.Count != definition.CellCount)
            {
                streak = 0;
                return Waiting();
            }
            var current = new BoardCell[observation.Cells.Count];
            for (int i = 0; i < current.Length; i++)
            {
                current[i] = observation.Cells[i].State;
                if (!current[i].IsKnown) { streak = 0; pending = null; return Waiting(); }
            }
            if (pending != null && Same(pending, current)) streak++;
            else streak = 1;
            pending = current;
            if (streak < required) return Waiting();
            if (stable == null)
            {
                stable = current;
                return new LayoutTrackStep(true, true, None, Clone(stable), streak, required);
            }
            if (Same(stable, current)) return new LayoutTrackStep(true, false, None, Clone(stable), streak, required);
            List<LayoutChangeEvent> changes = Diff(stable, current, definition, observation.CapturedAtUtc);
            stable = current;
            events.AddRange(changes);
            while (events.Count > 200) events.RemoveAt(0);
            return new LayoutTrackStep(true, false, changes, Clone(stable), streak, required);
        }

        private LayoutTrackStep Waiting()
        {
            return new LayoutTrackStep(false, false, None, stable == null ? null : Clone(stable), streak, required);
        }

        private static bool Same(BoardCell[] before, BoardCell[] after)
        {
            for (int i = 0; i < before.Length; i++)
                if (!before[i].SameContent(after[i])) return false;
            return true;
        }

        private static BoardCell[] Clone(BoardCell[] source)
        {
            var copy = new BoardCell[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static List<LayoutChangeEvent> Diff(BoardCell[] before, BoardCell[] after, BoardDefinition definition, DateTimeOffset timestamp)
        {
            var removed = new List<int>();
            var removedColor = new List<string>();
            var added = new List<int>();
            var addedColor = new List<string>();
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i].SameContent(after[i])) continue;
                if (before[i].Occupancy == Occupancy.Occupied) { removed.Add(i); removedColor.Add(before[i].ColorId); }
                if (after[i].Occupancy == Occupancy.Occupied) { added.Add(i); addedColor.Add(after[i].ColorId); }
            }
            var colors = new List<string>();
            Collect(colors, removedColor);
            Collect(colors, addedColor);
            colors.Sort(StringComparer.Ordinal);
            int width = definition.Width;
            var changes = new List<LayoutChangeEvent>();
            foreach (string color in colors)
            {
                var sources = Indexes(removed, removedColor, color);
                var destinations = Indexes(added, addedColor, color);
                bool uniqueMove = sources.Count == 1 && destinations.Count == 1 && sources[0] != destinations[0];
                if (uniqueMove)
                {
                    changes.Add(new LayoutChangeEvent(LayoutEventKind.Move, Point(destinations[0], width), color, timestamp, Point(sources[0], width)));
                    continue;
                }
                bool ambiguous = sources.Count > 0 && destinations.Count > 0;
                for (int i = 0; i < sources.Count; i++)
                    changes.Add(new LayoutChangeEvent(LayoutEventKind.Remove, Point(sources[i], width), color, timestamp, null, ambiguous));
                for (int i = 0; i < destinations.Count; i++)
                    changes.Add(new LayoutChangeEvent(LayoutEventKind.Add, Point(destinations[i], width), color, timestamp, null, ambiguous));
            }
            return changes;
        }

        private static void Collect(List<string> colors, List<string> values)
        {
            foreach (string value in values)
                if (!colors.Contains(value)) colors.Add(value);
        }

        private static List<int> Indexes(List<int> indexes, List<string> colors, string color)
        {
            var selected = new List<int>();
            for (int i = 0; i < indexes.Count; i++)
                if (colors[i] == color) selected.Add(indexes[i]);
            return selected;
        }

        private static GridPoint Point(int index, int width) => new GridPoint(index % width, index / width);
    }
}
