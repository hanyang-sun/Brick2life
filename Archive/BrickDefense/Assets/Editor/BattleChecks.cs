using System;
using System.IO;
using System.Reflection;
using BrickDefense.Core;
using BrickDefense.Demo;
using BrickDefense.Presentation;
using BrickDefense.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrickDefense.Editor
{
    [InitializeOnLoad]
    public static class BattleChecks
    {
        private const string Running="BrickDefense.BattleChecks.Running",Output="BrickDefense.BattleChecks.Output";
        private const string Multiple="BrickDefense.BattleChecks.MultipleSpawns";
        private const string Chinese="BrickDefense.BattleChecks.ChineseUi";
        private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static EditorWindow gameView;
        private static BrickDefenseDemo demo;
        private static BattlePanel panel;
        private static CombatView combat;
        private static LevelView view;
        private static PhotoScanPanel photo;
        private static int stage,waitFrame,oldRun;
        private static double deadline;
        private static LevelContext retained;
        private static Quaternion beforeDrag;
        private static Action afterCapture;
        private static object size,oldSizeType;
        private static int oldWidth,oldHeight;
        static BattleChecks()
        { if(SessionState.GetBool(Running,false)) { stage=SessionState.GetBool(Multiple,false)?-14:0; deadline=EditorApplication.timeSinceStartup+180; EditorApplication.update+=Tick; } }
        public static void Verify() => Run(false);
        public static void VerifyMultipleSpawns() => Run(true);
        public static void VerifyChineseUi() => Run(true,true);
        private static void Run(bool multiple,bool chinese=false)
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(!project.EndsWith("/.scratch/soft-strategy-ui/unity-check",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the isolated soft-strategy-ui verification project only.");
            string output=Path.GetFullPath(Path.Combine(project,chinese?"../../../.scratch/chinese-ui/verification":multiple?
                "../../../.scratch/multiple-spawns/verification":"../../../.scratch/combat-a/verification"));
            SessionState.SetBool(Multiple,multiple);
            SessionState.SetBool(Chinese,chinese);
            Directory.CreateDirectory(output); SessionState.SetString(Output,output); SessionState.SetBool(Running,true);
            EditorSceneManager.OpenScene("Assets/Scenes/BrickDefenseDemo.unity");
            foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if(window.GetType().Name=="GameView") window.Close();
            gameView=(EditorWindow)ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.ShowUtility(); gameView.position=new Rect(24,24,1280,864); gameView.Focus();
            gameView.GetType().GetProperty("selectedSizeIndex").SetValue(gameView,0);
            stage=multiple?-14:0; deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Battle check timeout at "+stage);
                if(gameView==null) foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if(window.GetType().Name=="GameView" && !window.docked) { gameView=window; break; }
                if(gameView==null || !EditorApplication.isPlaying || Time.frameCount<Math.Max(6,waitFrame)) return;
                gameView.GetType().GetMethod("SetFocus",Private).Invoke(gameView,new object[] {true}); gameView.Repaint();
                demo??=UnityEngine.Object.FindFirstObjectByType<BrickDefenseDemo>();
                panel??=UnityEngine.Object.FindFirstObjectByType<BattlePanel>();
                combat??=UnityEngine.Object.FindFirstObjectByType<CombatView>();
                view??=UnityEngine.Object.FindFirstObjectByType<LevelView>();
                photo??=UnityEngine.Object.FindFirstObjectByType<PhotoScanPanel>();
                Require(demo!=null && panel!=null && combat!=null && view!=null && photo!=null,"Missing scene modules.");
                if(afterCapture!=null)
                { Action action=afterCapture; afterCapture=null; action(); waitFrame=Time.frameCount+6; return; }
                switch(stage)
                {
                    case -14:
                        if(!Size(1280,800)) return;
                        if(SessionState.GetBool(Chinese,false))
                        {
                            CheckChineseFont();
                            Vector2 rightCard=new Vector2(Screen.width/Scale-156,132)*Scale;
                            Require(panel.ContainsPointer(rightCard) && !panel.ContainsPointer(new Vector2(154,368)*Scale),"Battle panel hit region did not move right.");
                            beforeDrag=Camera.main.transform.rotation;
                            Mouse(EventType.MouseDown,rightCard,1); Mouse(EventType.MouseDrag,rightCard+new Vector2(-140,30),1);
                            Mouse(EventType.MouseUp,rightCard+new Vector2(-140,30),1);
                        }
                        Click(Cell(new GridPoint(9,9))); break;
                    case -13:
                        if(SessionState.GetBool(Chinese,false)) Require(Quaternion.Angle(beforeDrag,Camera.main.transform.rotation)<0.1f,"Drag starting on right battle card moved camera.");
                        Require(Selected(new GridPoint(9,9)) && demo.Session.Current.Result.SpawnRoutes.Count==2,"Mouse click did not add second start.");
                        Click(Cell(new GridPoint(11,9))); break;
                    case -12:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==3 && GameObject.Find("Selected spawn 3")!=null,"Third start or its marker missing.");
                        Click(Cell(new GridPoint(11,9))); break;
                    case -11:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==2 && !Selected(new GridPoint(11,9)),"Click did not remove selected start.");
                        Click(Cell(new GridPoint(11,9))); break;
                    case -10:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==3,"Re-adding start failed.");
                        Click(Cell(new GridPoint(11,9))); break;
                    case -9:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==2,"Third start remained selected.");
                        Click(Cell(new GridPoint(9,9))); break;
                    case -8:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==1,"Second start remained selected.");
                        Click(Cell(new GridPoint(1,18))); break;
                    case -7:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==0 && demo.Session.Current.Result.Entities.Length==9,"Clearing starts removed geometry.");
                        Launch(); break;
                    case -6:
                        Require(!panel.IsConfigOpen && demo.Battle.State==null,"Zero starts enabled setup.");
                        Click(Cell(new GridPoint(9,9))); break;
                    case -5:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==1,"Adding after clear failed.");
                        Click(Cell(new GridPoint(11,9))); break;
                    case -4:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==2,"Second start after clear failed.");
                        Click(Cell(new GridPoint(1,18))); break;
                    case -3:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==3,"Third start after clear failed.");
                        Capture("multiple-starts-layout-wide"); afterCapture=()=>Click(Cell(new GridPoint(6,8))); break;
                    case -2:
                        Require(demo.Session.Current.Result.SpawnRoutes.Count==3 && demo.Session.SpawnSelectionIssue?.Code=="SPAWN_OCCUPIED","Occupied click changed selection or did not show reason.");
                        beforeDrag=Camera.main.transform.rotation;
                        Vector2 formerCard=new Vector2(154,368)*Scale;
                        Mouse(EventType.MouseDown,formerCard,1); Mouse(EventType.MouseDrag,formerCard+new Vector2(-120,30),1);
                        Mouse(EventType.MouseUp,formerCard+new Vector2(-120,30),1);
                        gameView.SendEvent(new Event {type=EventType.ScrollWheel,mousePosition=ToWindow(formerCard),delta=new Vector2(0,-3)}); break;
                    case -1:
                        Require(Quaternion.Angle(beforeDrag,Camera.main.transform.rotation)>5 && view.ZoomFactor>1,"Old left battle area still intercepts map input.");
                        view.ResetView(); break;
                    case 0:
                        if(!Size(1280,800)) return;
                        retained=demo.Session.Current; Launch(); break;
                    case 1:
                        Require(panel.IsConfigOpen,"Setup button did not open form."); Capture("battle-setup-wide");
                        afterCapture=()=> { Values()[0]="0"; FormStart(); }; break;
                    case 2:
                        Require(panel.IsConfigOpen && demo.Battle.State==null && !demo.Session.IsLocked,"Invalid plan started combat.");
                        Require(!string.IsNullOrEmpty((string)typeof(BattlePanel).GetField("error",Private).GetValue(panel)),"No field validation error.");
                        if(SessionState.GetBool(Chinese,false)) Require(((string)typeof(BattlePanel).GetField("error",Private).GetValue(panel)).Contains("波数"),"Validation error was not Chinese.");
                        Values()[0]="3"; FormStart(); break;
                    case 3:
                        Require(demo.Battle.State!=null && demo.Battle.State.Wave==1 && demo.Session.IsLocked,"Start button failed.");
                        if(SessionState.GetBool(Multiple,false)) Require(demo.Battle.Level.SpawnRoutes.Count==3,"Battle lost selected starts.");
                        demo.Battle.SetPaused(true); Click(Cell(new GridPoint(9,9))); break;
                    case 4:
                        Require(ReferenceEquals(retained,demo.Session.Current),"Battle click changed spawn.");
                        beforeDrag=Camera.main.transform.rotation;
                        Vector2 a=new Vector2(Screen.width*0.5f,Screen.height*0.52f),b=a+new Vector2(150,60);
                        Mouse(EventType.MouseDown,a,1); Mouse(EventType.MouseDrag,b,1); Mouse(EventType.MouseUp,b,1);
                        gameView.SendEvent(new Event {type=EventType.ScrollWheel,mousePosition=ToWindow(a),delta=new Vector2(0,-3)});
                        break;
                    case 5:
                        Require(Quaternion.Angle(beforeDrag,Camera.main.transform.rotation)>5,"Battle right drag failed.");
                        Require(Camera.main.orthographic && view.ZoomFactor>1,"Battle wheel lost zoom or projection.");
                        Require(ReferenceEquals(retained,demo.Session.Current),"Camera changed layout."); view.ResetView();
                        demo.Battle.SetPaused(false); var shots=demo.Battle.Advance(5); demo.Battle.SetPaused(true);
                        Require(demo.Battle.State.Enemies.Count>0,"No enemies for render check.");
                        if(shots.Count>0) combat.ShowShots(new[] {shots[shots.Count-1]});
                        Capture("battle-active-wide"); break;
                    case 6:
                        Require(GameObject.Find("Enemy "+demo.Battle.State.Enemies[0].Id)!=null,"Enemy display missing.");
                        Click(Cell(new GridPoint(10,10))); break;
                    case 7:
                        Require(GameObject.Find("Selected tower range")!=null,"Tower click did not show coverage.");
                        combat.ClearSelection(); demo.Battle.SetPaused(false); FinishWave();
                        Require(demo.Battle.State.Phase==BattlePhase.AwaitingNextWave,"Wave did not wait for next click.");
                        Capture("battle-next-wave-wide"); afterCapture=()=>Dock(0); break;
                    case 8:
                        Require(demo.Battle.State.Wave==2,"Next-wave button failed.");
                        FinishWave(); Require(demo.Battle.StartNextWave(),"Third-wave start failed."); FinishWave(); break;
                    case 9:
                        Require(demo.Battle.State.Phase==BattlePhase.Victory && !demo.Session.IsLocked,"Victory or unlock missing.");
                        Capture("battle-victory-wide"); oldRun=demo.Battle.RunId; afterCapture=()=>Dock(1); break;
                    case 10:
                        Require(demo.Battle.RunId==oldRun+1 && demo.Battle.State.Wave==1 && demo.Battle.State.Killed==0 &&
                            demo.Battle.State.CoreHealth==100 && demo.Session.IsLocked,"Retry button did not reset run.");
                        demo.Battle.SetPaused(true); if(!Size(540,800)) return; break;
                    case 11:
                        TouchCamera(); Capture("battle-active-narrow"); break;
                    case 12: Dock(2); break;
                    case 13:
                        Require(demo.Battle.State==null && !demo.Session.IsLocked,"End-run button did not return to layout.");
                        Dock(2); break;
                    case 14:
                        Require(photo.IsOpen,"Scan entry did not reopen after battle.");
                        if(SessionState.GetBool(Chinese,false))
                        { Capture("photo-entry-narrow"); afterCapture=()=>typeof(PhotoScanPanel).GetMethod("Close",Private).Invoke(photo,null); break; }
                        typeof(PhotoScanPanel).GetMethod("Close",Private).Invoke(photo,null); break;
                    case 15:
                        demo.Session.LoadExample(true); Launch(); break;
                    case 16:
                        Require(!panel.IsConfigOpen && demo.Battle.State==null,"Blocked map enabled battle.");
                        var scan=ExampleScans.Parse("no-tower-battle",new[] {"..........","..........","..........","..........","..........",
                            "..........","..........","..........","..........",".K........"});
                        Require(demo.Session.TryLoad(new LayoutDefinition(scan,ExampleScans.CreateDemoRules(),LayoutSource.Reference)),"No-tower map rejected.");
                        Launch(); break;
                    case 17: Require(panel.IsConfigOpen,"No-tower form missing."); FormStart(); break;
                    case 18:
                        Require(demo.Battle.State!=null,"No-tower battle did not start.");
                        for(int i=0;i<3 && !demo.Battle.State.Finished;i++)
                        { FinishWave(); if(!demo.Battle.State.Finished) Require(demo.Battle.StartNextWave(),"No-tower next wave failed."); }
                        break;
                    case 19:
                        Require(demo.Battle.State.Phase==BattlePhase.Defeat && demo.Battle.State.CoreHealth==0 && !demo.Session.IsLocked,"Defeat missing.");
                        Capture("battle-defeat-narrow"); break;
                    case 20: Dock(2); break;
                    case 21:
                        Require(demo.Battle.State==null && !demo.Session.IsLocked,"Result return failed.");
                        Require(GameObject.Find("Enemy 1")==null,"Return left enemy objects.");
                        if(SessionState.GetBool(Multiple,false)) { view.ResetView(); break; }
                        Finish(0,"BATTLE_UNITY_CHECKS_PASSED: actual setup/invalid-input/start/next-wave/retry/end/scan buttons; enemies, coverage, victory/defeat; battle mouse/touch camera and locked spawn; wide/narrow screenshots."); return;
                    case 22:
                        TouchTap(new GridPoint(9,9));
                        Require(Selected(new GridPoint(9,9)) && demo.Session.Current.Result.SpawnRoutes.Count==2,"Narrow tap did not add a start.");
                        TouchTap(new GridPoint(9,9));
                        Require(!Selected(new GridPoint(9,9)) && demo.Session.Current.Result.SpawnRoutes.Count==1,"Narrow tap did not remove a start.");
                        TouchTap(new GridPoint(8,9));
                        Require(Selected(new GridPoint(8,9)) && demo.Session.Current.Result.SpawnRoutes.Count==2,"Narrow second start failed.");
                        Capture("multiple-starts-layout-narrow"); afterCapture=()=>Launch(); break;
                    case 23:
                        Require(panel.IsConfigOpen,"Narrow multi-start setup missing."); FormStart(); break;
                    case 24:
                        Require(demo.Battle.Level?.SpawnRoutes.Count==2 && demo.Session.IsLocked,"Narrow multi-start battle failed.");
                        demo.Battle.SetPaused(false); demo.Battle.Advance(5); demo.Battle.SetPaused(true);
                        bool secondRoute=false;
                        foreach(EnemyState enemy in demo.Battle.State.Enemies) if(enemy.SpawnIndex==1) secondRoute=true;
                        Require(secondRoute && demo.Battle.State.SpawnedThisWave<=demo.Battle.Plan.EnemiesPerWave,"Second entrance did not produce enemies or count multiplied.");
                        Capture("multiple-starts-active-narrow"); afterCapture=()=>Dock(2); break;
                    case 25:
                        Require(demo.Battle.State==null && !demo.Session.IsLocked,"Multiple-start end did not release map.");
                        Finish(0,SessionState.GetBool(Chinese,false)?
                            "CHINESE_UI_CHECKS_PASSED: bundled glyph coverage, right battle setup/hit region, Chinese invalid input, wide/narrow screenshots, retained multi-entrance battle/retry/victory/defeat and narrow photo entry.":
                            "MULTIPLE_SPAWN_UNITY_CHECKS_PASSED: actual mouse add/remove/clear/occupied selection, disabled zero-start setup, numbered markers, old left battle region released; three-start wave/retry/lock, narrow touch toggle and two-start combat, retained mouse/touch gestures and victory/defeat."); return;
                }
                stage++; waitFrame=Time.frameCount+6;
            }
            catch(Exception e) { Finish(1,e.ToString()); }
        }
        private static void FinishWave()
        {
            for(int i=0;i<1000 && !demo.Battle.State.Finished && !demo.Battle.State.CanStartWave;i++) demo.Battle.Advance(0.1);
            Require(demo.Battle.State.Finished || demo.Battle.State.CanStartWave,"Wave timeout.");
        }
        private static string[] Values() => (string[])typeof(BattlePanel).GetField("values",Private).GetValue(panel);
        private static bool Selected(GridPoint point)
        { foreach(SpawnRoute route in demo.Session.Current.Result.SpawnRoutes) if(route.Spawn.Equals(point)) return true; return false; }
        private static void TouchTap(GridPoint point)
        {
            var input=demo.GetComponent<DemoUI>(); MethodInfo method=typeof(DemoUI).GetMethod("HandleTouches",Private);
            Vector2 pixel=Cell(point);
            void Frame(int count,Touch touch=default) => method.Invoke(input,new object[] {count,touch,default(Touch)});
            Touch touch=new Touch {fingerId=90,position=new Vector2(pixel.x,Screen.height-pixel.y),phase=TouchPhase.Began};
            Frame(0); Frame(1,touch); touch.phase=TouchPhase.Ended; Frame(1,touch); Frame(0);
        }
        private static float Scale => Mathf.Min(1.5f,Mathf.Min(Screen.width/(Screen.width/(float)Screen.height<0.95f?600f:900f),Screen.height/640f));
        private static void Launch() => Click(new Vector2(Screen.width/Scale-154,200)*Scale);
        private static void CheckChineseFont()
        {
            Font font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            Require(font!=null && font.dynamic,"Chinese font resource missing or not dynamic.");
            var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath("Assets/Resources/Fonts/NotoSansSC-Regular.otf");
            Require(importer.includeFontData,"Chinese font data is not included for players.");
            var characters=new System.Collections.Generic.HashSet<char>();
            foreach(string path in new[] {"DemoUI.cs","BattlePanel.cs","PhotoScanPanel.cs","ChineseUi.cs"})
                foreach(char c in File.ReadAllText(Path.Combine(Application.dataPath,"Scripts/Presentation",path)))
                    if(c>=0x3400 && c<=0x9fff) characters.Add(c);
            foreach(char c in characters) Require(font.HasCharacter(c),"Missing bundled Chinese glyph: "+c);
            Debug.Log("CHINESE_GLYPHS_VERIFIED: "+characters.Count+" characters / "+font.name);
        }
        private static void FormStart()
        {
            float width=Screen.width/Scale,height=Screen.height/Scale,formWidth=Mathf.Min(520,width-44),slot=(formWidth-54)/2;
            Click(new Vector2((width-formWidth)/2+32+slot*1.5f,(height-554)/2+512)*Scale);
        }
        private static void Dock(int index)
        {
            bool narrow=Screen.width/(float)Screen.height<0.95f;
            float width=Screen.width/Scale,height=Screen.height/Scale,dockWidth=narrow?width-44:700,slot=(dockWidth-44)/3;
            Click(new Vector2((width-dockWidth)/2+14+index*(slot+8)+slot/2,height-61)*Scale);
        }
        private static Vector2 Cell(GridPoint point)
        { Vector3 pixel=Camera.main.WorldToScreenPoint(CombatView.World(point.X,point.Y)); return new Vector2(pixel.x,Screen.height-pixel.y); }
        private static void TouchCamera()
        {
            var input=demo.GetComponent<DemoUI>(); MethodInfo method=typeof(DemoUI).GetMethod("HandleTouches",Private);
            void Frame(int count,Touch a=default,Touch b=default) => method.Invoke(input,new object[] {count,a,b});
            Touch At(int id,Vector2 p,TouchPhase phase,Vector2 delta=default) => new Touch {
                fingerId=id,position=new Vector2(p.x,Screen.height-p.y),phase=phase,deltaPosition=new Vector2(delta.x,-delta.y)};
            Quaternion angle=Camera.main.transform.rotation;
            Vector2 a=new Vector2(Screen.width*0.38f,Screen.height*0.55f),b=new Vector2(Screen.width*0.62f,Screen.height*0.55f);
            Frame(0); Frame(1,At(1,a,TouchPhase.Began)); Frame(1,At(1,a+new Vector2(65,20),TouchPhase.Moved,new Vector2(65,20)));
            Frame(0); Require(Quaternion.Angle(angle,Camera.main.transform.rotation)>5,"Battle touch drag failed.");
            float zoom=view.ZoomFactor;
            Frame(2,At(2,a,TouchPhase.Began),At(3,b,TouchPhase.Began));
            Frame(2,At(2,a-new Vector2(15,0),TouchPhase.Moved,new Vector2(-15,0)),At(3,b+new Vector2(15,0),TouchPhase.Moved,new Vector2(15,0)));
            Frame(0); Require(view.ZoomFactor>zoom && Camera.main.orthographic,"Battle pinch failed.");
            Require(ReferenceEquals(retained,demo.Session.Current),"Touch camera changed locked layout.");
        }
        private static bool Size(int width,int height)
        {
            if(size==null)
            {
                Type sizes=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
                object instance=sizes.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
                object group=sizes.GetProperty("currentGroup").GetValue(instance);
                size=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[] {0});
                Type type=size.GetType(); oldSizeType=type.GetProperty("sizeType").GetValue(size);
                oldWidth=(int)type.GetProperty("width").GetValue(size); oldHeight=(int)type.GetProperty("height").GetValue(size);
            }
            Type sizeType=size.GetType();
            if((int)sizeType.GetProperty("width").GetValue(size)!=width || (int)sizeType.GetProperty("height").GetValue(size)!=height)
            {
                sizeType.GetProperty("sizeType").SetValue(size,Enum.Parse(oldSizeType.GetType(),"FixedResolution"));
                sizeType.GetProperty("width").SetValue(size,width); sizeType.GetProperty("height").SetValue(size,height);
                gameView.position=new Rect(24,24,width,height+64); gameView.Focus(); gameView.Repaint(); waitFrame=Time.frameCount+6; return false;
            }
            return Screen.width==width && Screen.height==height;
        }
        private static Vector2 ToWindow(Vector2 pixel)
        {
            float scale=(float)gameView.GetType().GetProperty("gameMouseScale",Private).GetValue(gameView);
            Vector2 offset=(Vector2)gameView.GetType().GetProperty("gameMouseOffset",Private).GetValue(gameView);
            return pixel/scale-offset;
        }
        private static void Mouse(EventType type,Vector2 point,int button)
        { gameView.Focus(); gameView.SendEvent(new Event {type=type,mousePosition=ToWindow(point),button=button,clickCount=1}); }
        private static void Click(Vector2 point) { Mouse(EventType.MouseDown,point,0); Mouse(EventType.MouseUp,point,0); }
        private static void Capture(string name)
        { ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Output,""),name+".png")); Debug.Log("BATTLE_CAPTURE: "+name+" / "+demo.Battle.State?.Phase); }
        private static void Require(bool valid,string message) { if(!valid) throw new Exception(message+" (stage "+stage+")"); }
        private static void Finish(int code,string message)
        {
            SessionState.SetBool(Running,false); EditorApplication.update-=Tick;
            if(size!=null)
            {
                Type type=size.GetType(); type.GetProperty("sizeType").SetValue(size,oldSizeType);
                type.GetProperty("width").SetValue(size,oldWidth); type.GetProperty("height").SetValue(size,oldHeight);
            }
            if(code==0) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(code);
        }
    }
}
