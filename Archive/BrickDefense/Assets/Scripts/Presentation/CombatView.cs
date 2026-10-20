using System.Collections.Generic;
using BrickDefense.Core;
using BrickDefense.Workflow;
using UnityEngine;

namespace BrickDefense.Presentation
{
    public sealed class CombatView : MonoBehaviour
    {
        private sealed class EnemyVisual
        {
            public Transform Root;
            public Renderer Renderer;
            public EnemyState State;
            public float FlashUntil;
        }
        private BattleSession battle;
        private Transform root;
        private GameObject rangeObject;
        private string selectedTower;
        private int shownRun=-1;
        private bool showing;
        private readonly Dictionary<int,EnemyVisual> enemies=new Dictionary<int,EnemyVisual>();
        private readonly List<int> removals=new List<int>();
        private Material enemyMaterial,hitMaterial,shotMaterial,rangeMaterial;
        public void Initialize(BattleSession owner)
        {
            battle=owner;
            enemyMaterial=Solid(SoftStrategyTheme.Danger);
            hitMaterial=Solid(new Color32(255,231,160,255));
            shotMaterial=Line(SoftStrategyTheme.Tower);
            rangeMaterial=Line(new Color(0.27f,0.44f,0.35f,0.8f));
            ReplaceRoot();
        }
        public static Vector3 World(double x,double y,float height=0) => new Vector3((float)x-9.5f,height,9.5f-(float)y);

        public void ShowShots(IReadOnlyList<ShotEvent> shots)
        {
            EnsureRun();
            foreach(ShotEvent shot in shots)
            {
                var lineObject=new GameObject("Tower shot"); lineObject.transform.SetParent(root);
                var line=lineObject.AddComponent<LineRenderer>(); line.sharedMaterial=shotMaterial;
                line.positionCount=2; line.startWidth=0.07f; line.endWidth=0.035f;
                line.SetPosition(0,World(shot.TowerX,shot.TowerY,1.45f));
                line.SetPosition(1,World(shot.TargetX,shot.TargetY,0.5f));
                Destroy(lineObject,0.14f);
                var impact=GameObject.CreatePrimitive(PrimitiveType.Sphere); impact.name=shot.Killed?"Enemy cleared":"Hit";
                impact.transform.SetParent(root); impact.transform.position=World(shot.TargetX,shot.TargetY,0.5f);
                impact.transform.localScale=Vector3.one*(shot.Killed?0.45f:0.18f);
                impact.GetComponent<Renderer>().sharedMaterial=hitMaterial;
                Destroy(impact.GetComponent<Collider>()); Destroy(impact,shot.Killed?0.22f:0.12f);
                if(enemies.TryGetValue(shot.EnemyId,out EnemyVisual visual)) visual.FlashUntil=Time.unscaledTime+0.12f;
            }
        }
        private void EnsureRun()
        {
            if(battle.State!=null && shownRun!=battle.RunId)
            { ReplaceRoot(); ClearSelection(); shownRun=battle.RunId; showing=true; }
        }
        private void LateUpdate()
        {
            if(battle==null) return;
            EnsureRun();
            GameState state=battle.State;
            if(state==null)
            { if(showing) { ReplaceRoot(); ClearSelection(); showing=false; } return; }
            var alive=new HashSet<int>();
            foreach(EnemyState enemy in state.Enemies)
            {
                alive.Add(enemy.Id);
                if(!enemies.TryGetValue(enemy.Id,out EnemyVisual visual))
                {
                    var obj=GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.name=$"Enemy {enemy.Id}";
                    obj.transform.SetParent(root); obj.transform.localScale=new Vector3(0.48f,0.42f,0.48f);
                    Destroy(obj.GetComponent<Collider>());
                    visual=new EnemyVisual {Root=obj.transform,Renderer=obj.GetComponent<Renderer>()}; enemies.Add(enemy.Id,visual);
                }
                visual.State=enemy; visual.Root.position=World(enemy.X,enemy.Y,0.45f);
                visual.Renderer.sharedMaterial=Time.unscaledTime<visual.FlashUntil?hitMaterial:enemyMaterial;
            }
            removals.Clear();
            foreach(var pair in enemies) if(!alive.Contains(pair.Key)) removals.Add(pair.Key);
            foreach(int id in removals) { Destroy(enemies[id].Root.gameObject); enemies.Remove(id); }
        }
        private void OnGUI()
        {
            if(battle?.State==null || Camera.main==null) return;
            int depth=GUI.depth; GUI.depth=20;
            try
            {
                foreach(EnemyVisual enemy in enemies.Values)
                {
                    Vector3 screen=Camera.main.WorldToScreenPoint(enemy.Root.position+Vector3.up*0.67f);
                    if(screen.z<=0) continue;
                    float width=Mathf.Clamp(Screen.height/(Camera.main.orthographicSize*2)*0.9f,14,32);
                    float height=Mathf.Clamp(width/6,3,5);
                    var rect=new Rect(screen.x-width/2,Screen.height-screen.y,width,height);
                    SoftStrategyTheme.Round(rect,SoftStrategyTheme.Paper,2);
                    SoftStrategyTheme.Round(new Rect(rect.x,rect.y,rect.width*enemy.State.Health/battle.Plan.EnemyHealth,height),
                        SoftStrategyTheme.Danger,2);
                }
            }
            finally { GUI.depth=depth; }
        }
        public void ToggleRange(LevelEntity tower,double range)
        {
            bool close=selectedTower==tower.BrickId;
            ClearSelection(); if(close) return;
            selectedTower=tower.BrickId; rangeObject=new GameObject("Selected tower range"); rangeObject.transform.SetParent(transform);
            var ring=rangeObject.AddComponent<LineRenderer>(); ring.sharedMaterial=rangeMaterial;
            ring.positionCount=96; ring.loop=true; ring.startWidth=ring.endWidth=0.06f;
            Vector3 center=World(tower.C*2+0.5,tower.R*2+0.5,0.13f);
            for(int i=0;i<96;i++)
            { float a=i*Mathf.PI*2/96; ring.SetPosition(i,center+new Vector3(Mathf.Cos(a)*(float)range,0,Mathf.Sin(a)*(float)range)); }
        }
        public void ClearSelection()
        { if(rangeObject!=null) { rangeObject.SetActive(false); Destroy(rangeObject); } rangeObject=null; selectedTower=null; }
        private void ReplaceRoot()
        {
            if(root!=null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root=new GameObject("Battle visuals").transform; root.SetParent(transform); enemies.Clear();
        }
        private static Material Solid(Color color)
        { var material=new Material(Shader.Find("Standard")); material.color=color; material.SetFloat("_Glossiness",0.08f); return material; }
        private static Material Line(Color color)
        { var material=new Material(Shader.Find("Sprites/Default")); material.color=color; return material; }
        private void OnDestroy()
        { foreach(Material material in new[] {enemyMaterial,hitMaterial,shotMaterial,rangeMaterial}) if(material!=null) Destroy(material); }
    }
}
