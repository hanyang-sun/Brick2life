using System;
using System.Globalization;
using BrickDefense.Core;
using BrickDefense.Demo;
using BrickDefense.Workflow;
using UnityEngine;

namespace BrickDefense.Presentation
{
    public sealed class BattlePanel : MonoBehaviour
    {
        private BattleSession battle;
        private LevelSession levels;
        private CombatView combat;
        private PhotoScanPanel photo;
        private CombatConfig config;
        private SoftStrategyTheme theme;
        private readonly string[] values={"3","8","24","1.4","10","1.1"};
        private static readonly string[] Labels={"波数","每波敌人总数","敌人生命","速度（格/秒）","抵达核心的伤害","出怪间隔（秒）"};
        private string error="";
        public bool IsConfigOpen { get; private set; }
        public bool HasBattle => battle?.State!=null;
        public void Initialize(BattleSession owner,LevelSession map,CombatView display,PhotoScanPanel photos,CombatConfig settings)
        { battle=owner; levels=map; combat=display; photo=photos; config=settings; }

        private Rect Card(BoardUiLayout layout) => new Rect(layout.Width-286,114,264,layout.Narrow && HasBattle?116:140);
        private Rect ResultArea(BoardUiLayout layout)
        { float width=Mathf.Min(430,layout.Width-44); return new Rect((layout.Width-width)/2,(layout.Height-220)/2,width,220); }
        public bool ContainsPointer(Vector2 screenPoint)
        {
            if(IsConfigOpen) return true;
            var layout=new BoardUiLayout(Screen.width,Screen.height);
            Vector2 p=screenPoint/layout.Scale;
            return Card(layout).Contains(p) || (battle.State?.Finished==true && ResultArea(layout).Contains(p));
        }
        // A tap on a tower shows coverage. During a run every map tap leaves the spawn unchanged.
        public bool HandleMapCell(GridPoint cell)
        {
            LevelResult level=battle.Level??levels.Current?.Result;
            if(level==null) return HasBattle;
            foreach(LevelEntity entity in level.Entities)
                if(entity.Role==BrickRole.Tower && entity.C==cell.X/2 && entity.R==cell.Y/2)
                { combat.ToggleRange(entity,battle.Config?.TowerRange??config.TowerRange); return true; }
            combat.ClearSelection(); return HasBattle;
        }
        private void Update()
        { if(battle!=null) combat.ShowShots(battle.Advance(Math.Min(Time.unscaledDeltaTime,0.25))); }
        private void OnApplicationPause(bool paused) { if(paused) battle?.SetPaused(true); }

        private void OnGUI()
        {
            if(levels?.Current==null || photo.IsOpen) return;
            theme??=new SoftStrategyTheme();
            var layout=new BoardUiLayout(Screen.width,Screen.height);
            Matrix4x4 matrix=GUI.matrix; Color color=GUI.color; bool enabled=GUI.enabled; int depth=GUI.depth;
            GUI.depth=-10; GUI.matrix=Matrix4x4.Scale(new Vector3(layout.Scale,layout.Scale,1)); GUI.color=Color.white; GUI.enabled=true;
            try
            {
                if(IsConfigOpen) { DrawConfig(layout); return; }
                Rect card=Card(layout); theme.Card(card);
                GameState state=battle.State;
                if(state==null)
                {
                    GUI.Label(new Rect(card.x+14,card.y+12,236,17),"准备战斗",theme.Eyebrow);
                    GUI.Label(new Rect(card.x+14,card.y+34,236,30),$"已选 {levels.Current.Result.SpawnRoutes.Count} 个出生点",theme.Heading);
                    GUI.enabled=levels.Current.Result.Status==ResultStatus.Success;
                    if(theme.Button(new Rect(card.x+14,card.y+70,236,32),"设置战斗",true)) { IsConfigOpen=true; error=""; }
                    GUI.enabled=true;
                    GUI.Label(new Rect(card.x+14,card.y+112,236,18),"点选空格添加，再点一次取消。",theme.Small);
                    return;
                }
                GUI.Label(new Rect(card.x+14,card.y+12,236,17),state.Finished?
                    state.Phase==BattlePhase.Victory?"防守成功":"核心失守":battle.Paused?"战斗已暂停":"守住你的积木阵地",theme.Eyebrow);
                GUI.Label(new Rect(card.x+14,card.y+33,236,23),$"第 {state.Wave} / {battle.Plan.WaveCount} 波",theme.Heading);
                SoftStrategyTheme.Round(new Rect(card.x+14,card.y+62,236,9),SoftStrategyTheme.PaleDanger,4);
                SoftStrategyTheme.Round(new Rect(card.x+14,card.y+62,236f*state.CoreHealth/battle.Config.InitialCoreHealth,9),SoftStrategyTheme.Core,4);
                GUI.Label(new Rect(card.x+14,card.y+80,236,20),$"核心 {state.CoreHealth}/{battle.Config.InitialCoreHealth}　存活 {state.Enemies.Count}",theme.Body);
                if(!layout.Narrow) GUI.Label(new Rect(card.x+14,card.y+110,236,18),$"击杀 {state.Killed}　抵达核心 {state.Leaked}",theme.Small);
                DrawDock(layout,state);
                // Returning/retrying in DrawDock replaces the battle within this GUI event.
                if(state.Finished && ReferenceEquals(state,battle.State)) DrawResult(layout,state);
            }
            finally { GUI.matrix=matrix; GUI.color=color; GUI.enabled=enabled; GUI.depth=depth; }
        }

        private void DrawConfig(BoardUiLayout layout)
        {
            SoftStrategyTheme.Solid(new Rect(0,0,layout.Width,layout.Height),new Color(0.15f,0.22f,0.17f,0.38f));
            float width=Mathf.Min(520,layout.Width-44),height=554;
            var area=new Rect((layout.Width-width)/2,(layout.Height-height)/2,width,height); theme.Card(area);
            GUI.Label(new Rect(area.x+22,area.y+20,width-44,30),"制定防守计划",theme.Title);
            GUI.Label(new Rect(area.x+22,area.y+58,width-44,36),$"敌人从 {levels.Current.Result.SpawnRoutes.Count} 个出生点轮流出现。每波数量为所有入口的总数。",theme.Body);
            for(int i=0;i<values.Length;i++)
            {
                float y=area.y+106+i*48;
                GUI.Label(new Rect(area.x+22,y+8,width*0.53f-22,28),Labels[i],theme.Body);
                values[i]=GUI.TextField(new Rect(area.x+width*0.53f,y,width*0.47f-22,34),values[i],12,theme.Field);
            }
            GUI.Label(new Rect(area.x+22,area.y+402,width-44,34),
                $"塔伤害 {config.TowerDamage}，射程 {config.TowerRange:0.#} 格，攻击间隔 {config.TowerAttackInterval:0.##} 秒。核心生命 {config.InitialCoreHealth}。",theme.Small);
            GUI.Label(new Rect(area.x+22,area.y+438,width-44,38),string.IsNullOrEmpty(error)?
                "当前为试玩参数，建议用相同设置比较不同布局。":error,theme.Body);
            float slot=(width-54)/2;
            if(theme.Button(new Rect(area.x+22,area.y+492,slot,40),"返回")) IsConfigOpen=false;
            if(theme.Button(new Rect(area.x+32+slot,area.y+492,slot,40),"开始第一波",true)) StartFromFields();
        }
        private void StartFromFields()
        {
            int[] integers=new int[4]; int[] indices={0,1,2,4};
            for(int i=0;i<indices.Length;i++)
                if(!int.TryParse(values[indices[i]],NumberStyles.Integer,CultureInfo.InvariantCulture,out integers[i]))
                { error=Labels[indices[i]]+"：请输入整数。"; return; }
            if(!double.TryParse(values[3],NumberStyles.Float,CultureInfo.InvariantCulture,out double speed) ||
                !double.TryParse(values[5],NumberStyles.Float,CultureInfo.InvariantCulture,out double interval))
            { error="速度和出怪间隔：请输入数字，小数使用英文句点。"; return; }
            var plan=new WavePlan(integers[0],integers[1],integers[2],speed,integers[3],interval);
            if(!battle.TryStart(plan,config,out Issue[] issues)) { error=ChineseUi.Error(issues[0]); return; }
            IsConfigOpen=false; GUIUtility.keyboardControl=0;
        }
        private void DrawDock(BoardUiLayout layout,GameState state)
        {
            Rect dock=layout.Dock; theme.Card(dock);
            float slot=(dock.width-44)/3;
            string label=state.Finished?state.Phase==BattlePhase.Victory?"胜利！":"失败":
                battle.Paused?"继续战斗":state.CanStartWave?"开始下一波":state.Phase==BattlePhase.Spawning?
                $"出怪中 {state.SpawnedThisWave}/{battle.Plan.EnemiesPerWave}":"清除剩余敌人";
            GUI.enabled=!state.Finished;
            if(theme.Button(new Rect(dock.x+14,dock.y+14,slot,42),label,true))
            { if(battle.Paused) battle.SetPaused(false); else if(state.CanStartWave) battle.StartNextWave(); }
            GUI.enabled=true;
            if(theme.Button(new Rect(dock.x+22+slot,dock.y+14,slot,42),state.Finished?"重试当前布局":battle.Paused?"已暂停":"暂停"))
            { if(state.Finished) battle.Retry(out _); else battle.SetPaused(!battle.Paused); }
            if(theme.Button(new Rect(dock.x+30+slot*2,dock.y+14,slot,42),state.Finished?"返回布局":"结束本局"))
                battle.ReturnToLayout();
        }
        private void DrawResult(BoardUiLayout layout,GameState state)
        {
            Rect area=ResultArea(layout); theme.Card(area);
            GUI.Label(new Rect(area.x+22,area.y+20,area.width-44,32),
                state.Phase==BattlePhase.Victory?"你的阵地守住了！":"换一种布局再试试",theme.Title);
            int survived=state.Phase==BattlePhase.Victory?state.Wave:state.Wave-1;
            GUI.Label(new Rect(area.x+22,area.y+66,area.width-44,26),$"守住波数：{survived} / {battle.Plan.WaveCount}",theme.Body);
            GUI.Label(new Rect(area.x+22,area.y+102,area.width-44,26),$"击杀：{state.Killed}　抵达核心：{state.Leaked}",theme.Body);
            GUI.Label(new Rect(area.x+22,area.y+138,area.width-44,26),$"核心生命：{state.CoreHealth} / {battle.Config.InitialCoreHealth}",theme.Body);
            GUI.Label(new Rect(area.x+22,area.y+176,area.width-44,28),"可以重试，或返回布局重新扫描积木。",theme.Small);
        }
        private void OnDestroy() { theme?.Dispose(); }
    }
}
