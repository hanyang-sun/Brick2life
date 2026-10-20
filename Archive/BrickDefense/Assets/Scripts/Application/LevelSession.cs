using System;
using System.Collections.Generic;
using BrickDefense.Core;

namespace BrickDefense.Workflow
{
    public enum LayoutSource { Reference, Blocked, Photograph }

    // 布局、颜色规则和来源必须一起提交，避免示例与照片规则混用。
    public sealed class LayoutDefinition
    {
        public ScanResult Layout { get; }
        public LevelRules Rules { get; }
        public LayoutSource Source { get; }
        public LayoutDefinition(ScanResult layout,LevelRules rules,LayoutSource source)
        { Layout=layout; Rules=rules; Source=source; }
    }

    public sealed class LevelContext
    {
        public LayoutDefinition Definition { get; }
        public GridResult Grid { get; }
        public LevelResult Result { get; }
        public GridPoint Spawn => Result.Spawn;
        public string LayoutText => Definition.Source == LayoutSource.Blocked ? "Layout: surrounded core (must reject)" :
            Definition.Source == LayoutSource.Reference ? "Layout: reference example (9 bricks / 36 occupied cells)" :
            $"Layout: {(Definition.Layout.Board?.CoordinateBasis==LayoutCoordinateBasis.RelativeBrickScale?"relative photo":"photograph")} ({Definition.Layout.Bricks.Length} bricks / {Definition.Layout.Bricks.Length*4} occupied cells)";
        public string StatusText => Result.Status == ResultStatus.Success ?
            $"Routes ready: {Result.SpawnRoutes.Count} starts" :
            $"Rejected: {Result.Issues[0].Code} - {Result.Issues[0].Message}";
        internal LevelContext(LayoutDefinition definition,GridResult grid,LevelResult result)
        { Definition=definition; Grid=grid; Result=result; }
    }

    // A context is built completely before replacing Current or notifying its viewers.
    public sealed class LevelSession
    {
        public LevelContext Current { get; private set; }
        public bool IsLocked { get; private set; }
        public Issue SpawnSelectionIssue { get; private set; }
        internal void SetBattleLock(bool value) { IsLocked=value; if(value) SpawnSelectionIssue=null; }
        public event Action<LevelContext> Changed;

        public void LoadExample(bool blocked)
        {
            if(IsLocked) return;
            var definition = new LayoutDefinition(blocked ? ExampleScans.CreateBlockedCore() : ExampleScans.CreateReference(),
                ExampleScans.CreateDemoRules(),blocked ? LayoutSource.Blocked : LayoutSource.Reference);
            if (!TryLoad(definition)) throw new InvalidOperationException("The built-in example is invalid.");
        }

        // 先完成映射和验证，再一次性提交；失败输入不改变正在显示的关卡。
        public bool TryLoad(LayoutDefinition definition)
        {
            if(IsLocked) return false;
            if (definition?.Layout == null || definition.Rules?.ColorRoleMap == null) return false;
            GridResult grid = GridMapper.Map(definition.Layout);
            if (grid.Status != ResultStatus.Success) return false;
            // Take copies so a caller cannot later change the active color mapping or arrays.
            var rules = new Dictionary<string,BrickRole>();
            foreach (var pair in definition.Rules.ColorRoleMap) rules.Add(pair.Key,pair.Value);
            ScanResult source = definition.Layout;
            var layout = new ScanResult(source.ScanId,source.Status,(PhysicalCellObservation[])source.PhysicalCells.Clone(),
                (BrickObservation[])source.Bricks.Clone(),(Issue[])source.Issues.Clone(),source.Board);
            var frozen = new LayoutDefinition(layout,new LevelRules(rules),definition.Source);
            LevelResult result = LevelBuilder.Build(grid,frozen.Rules,new GridPoint(1,18));
            // Unreachable/occupied-spawn layouts remain previewable; malformed rules and missing cores do not replace the level.
            if (result.Entities.Length != grid.Placements.Length || result.CoreBrickIds.Length == 0) return false;
            foreach (Issue issue in result.Issues)
                if (issue.Code != "NO_ROUTE" && issue.Code != "SPAWN_OCCUPIED") return false;
            Commit(new LevelContext(frozen,grid,result)); return true;
        }

        // 只更换出生点时复用已验证的格位与规则，重新计算路线。
        public bool SelectSpawn(GridPoint spawn)
        {
            if(IsLocked) return false;
            if (Current == null || spawn.X < 0 || spawn.Y < 0 || spawn.X >= BoardSize.Navigation || spawn.Y >= BoardSize.Navigation)
                return false;
            LevelContext current = Current;
            LevelResult result = LevelBuilder.Build(current.Grid,current.Definition.Rules,spawn);
            Commit(new LevelContext(current.Definition,current.Grid,result)); return true;
        }

        public bool ToggleSpawn(GridPoint spawn)
        {
            if(IsLocked || Current==null) return false;
            var spawns=new List<GridPoint>();
            foreach(SpawnRoute route in Current.Result.SpawnRoutes) spawns.Add(route.Spawn);
            if(!spawns.Remove(spawn))
            {
                LevelResult candidate=LevelBuilder.Build(Current.Grid,Current.Definition.Rules,spawn);
                if(candidate.Status!=ResultStatus.Success)
                { SpawnSelectionIssue=candidate.Issues[0]; return false; }
                spawns.Add(spawn);
            }
            LevelResult result=LevelBuilder.Build(Current.Grid,Current.Definition.Rules,spawns);
            Commit(new LevelContext(Current.Definition,Current.Grid,result)); return true;
        }

        private void Commit(LevelContext context)
        { SpawnSelectionIssue=null; Current=context; Changed?.Invoke(context); }
    }
}
