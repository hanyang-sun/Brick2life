using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BrickDefense.Core;
using BrickDefense.Demo;
using BrickDefense.Infrastructure;
using BrickDefense.Presentation;
using BrickDefense.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrickDefense.Editor
{
    [InitializeOnLoad]
    public static class LayoutReviewUiChecks
    {
        private const string Running="BrickDefense.ReviewUI.Running",Output="BrickDefense.ReviewUI.Output";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static EditorWindow game;
        private static BrickDefenseDemo demo;
        private static PhotoScanPanel photo;
        private static LayoutReviewPanel review;
        private static int stage,waitFrame;
        private static double deadline;
        private static string profileKey,selected,recordPath;
        private static GridPoint originalPoint;
        private static LevelContext retained;
        static LayoutReviewUiChecks()
        { if(SessionState.GetBool(Running,false)) { deadline=EditorApplication.timeSinceStartup+180; EditorApplication.update+=Tick; } }
        public static void Verify()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(!project.EndsWith("/.scratch/layout-review/unity-check",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run in the isolated layout-review verification project.");
            string output=Path.GetFullPath(Path.Combine(project,"../../../.scratch/layout-review/verification"));
            Directory.CreateDirectory(output); SessionState.SetString(Output,output); SessionState.SetBool(Running,true);
            EditorSceneManager.OpenScene("Assets/Scenes/BrickDefenseDemo.unity");
            foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) if(window.GetType().Name=="GameView") window.Close();
            game=(EditorWindow)ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            game.ShowUtility(); game.position=new Rect(24,24,1280,824); game.Focus();
            game.GetType().GetProperty("selectedSizeIndex").SetValue(game,0);
            deadline=EditorApplication.timeSinceStartup+180; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Timeout at stage "+stage);
                if(game==null) game=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="GameView" && !w.docked);
                if(game==null || !EditorApplication.isPlaying || Time.frameCount<Math.Max(5,waitFrame)) return;
                game.Repaint(); game.Focus();
                foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) if(window!=game && window.GetType().Name=="GameView") window.Close();
                demo??=UnityEngine.Object.FindFirstObjectByType<BrickDefenseDemo>();
                photo??=UnityEngine.Object.FindFirstObjectByType<PhotoScanPanel>();
                review??=UnityEngine.Object.FindFirstObjectByType<LayoutReviewPanel>();
                Require(demo!=null && photo!=null && review!=null,"Composition missing.");
                switch(stage)
                {
                    case 0:
                        if(!Size(1280,800)) return;
                        retained=demo.Session.Current; profileKey="BrickDefense.ReviewUI."+Guid.NewGuid().ToString("N");
                        Set(photo,"profiles",new PhotoProfileStore(profileKey));
                        Set(photo,"reports",new ScanReportStore(Path.Combine(SessionState.GetString(Output,""),"ScanReports")));
                        Set(review,"store",new LayoutReviewStore(Path.Combine(SessionState.GetString(Output,""),"ScanReports")));
                        photo.LoadPhoto(Fixture("real-unmarked-bricks-v1.jpg")); photo.Open();
                        Color32[] colors=Get<Color32[]>(photo,"samples");
                        colors[0]=new Color32(65,61,63,255); colors[1]=new Color32(251,251,251,255);
                        colors[2]=new Color32(253,97,48,255); colors[3]=new Color32(5,195,254,255);
                        bool[] ready=Get<bool[]>(photo,"sampled"); for(int i=0;i<4;i++) ready[i]=true;
                        Invoke(photo,"Analyze"); break;
                    case 1:
                        if(Analysis.IsBusy || Analysis.Result==null) return;
                        Require(Analysis.Result.Status==ResultStatus.Success && Analysis.Result.Bricks.Length==10,"Real photo analysis failed.");
                        Capture("photo-entry"); OpenReviewButton(); break;
                    case 2:
                        Require(review.IsOpen && review.Session.Statistics==null && ReferenceEquals(retained,demo.Session.Current),"Review changed the current level.");
                        Capture("review-physical-wide");
                        var first=review.Session.Bricks[0]; selected=first.BrickId; originalPoint=new GridPoint(first.C,first.R);
                        Cell(first.C,first.R); break;
                    case 3:
                        Require(Get<string>(review,"selectedId")==selected,"Grid selection failed."); Cell(0,0); break;
                    case 4:
                        Require(review.Session.Bricks.Single(b=>b.BrickId==selected).C==0,"Grid move failed.");
                        Click(new Vector2(Screen.width-308+16+55,118+370+17)); break;
                    case 5:
                        var restored=review.Session.Bricks.Single(b=>b.BrickId==selected);
                        Require(restored.C==originalPoint.X && restored.R==originalPoint.Y,"Undo button did not restore position.");
                        Rect check=Get<Rect>(review,"verificationArea"); Click(check.center+new Vector2(Screen.width-308,118)); break;
                    case 6:
                        Require(Get<bool>(review,"verifiedByPlayer"),"Physical confirmation checkbox inaccessible."); ActionButton(); break;
                    case 7:
                        Require(review.Session.Stage==LayoutEditStage.GameDesign && review.Session.Statistics.ActualCount==10 &&
                            review.Session.Statistics.RecognitionAccuracy==1 && review.Session.Statistics.PositionAccuracy==1,"Verified metrics failed.");
                        recordPath=Path.Combine(SessionState.GetString(Output,""),"ScanReports","Scans",review.Session.ScanId,"review-"+review.Session.ReviewId+".json");
                        Require(File.Exists(recordPath) && File.ReadAllText(recordPath).Contains("\"verified\""),"Review record missing.");
                        Capture("review-design-wide"); Click(new Vector2(Screen.width-308+16+119,118+112+17)); break;
                    case 8: Click(new Vector2(Screen.width-308+16+35,118+158+17)); break;
                    case 9: Cell(0,0); break;
                    case 10:
                        Require(review.Session.Bricks.Count==11 && review.Session.Statistics.ActualCount==10,"Design edit changed frozen truth or failed.");
                        Click(new Vector2(Screen.width-131,58)); break;
                    case 11:
                        Require(Get<bool>(review,"preview3d") && GameObject.Find("Physical bricks").transform.childCount==11,"3D draft preview failed.");
                        Capture("review-3d-preview");
                        break;
                    case 12:
                        foreach(var b in review.Session.Bricks.Where(b=>review.Session.RoleFor(b.ColorId)==BrickRole.Core).ToArray()) review.Session.Delete(b.BrickId);
                        Capture("review-3d-no-core"); ActionButton(); break;
                    case 13:
                        Require(review.IsOpen && ReferenceEquals(retained,demo.Session.Current) && Get<string>(review,"message").Contains("核心"),"No-core publication replaced the level.");
                        Require(review.Session.Statistics.ActualCount==10,"Core editing changed truth.");
                        review.Session.Add(0,0,BrickRole.Core); Click(new Vector2(Screen.width-131,58)); break;
                    case 14: ActionButton(); break;
                    case 15:
                        Require(!review.IsOpen && !photo.IsOpen && demo.Session.Current.Result.Entities.Length==10,"Edited level did not publish.");
                        string json=File.ReadAllText(recordPath);
                        Require(json.Contains("\"published\": true") && json.Contains("\"actual\": 10") && json.Contains("\"original\""),"Publication changed metrics or lost evidence.");
                        Require(File.Exists(Path.Combine(Path.GetDirectoryName(recordPath),"result.json")),"Raw scan report disappeared.");
                        Capture("review-published-map"); retained=demo.Session.Current;
                        photo.LoadPhoto(Fixture("real-cropped-bricks-v1.jpg")); photo.Open(); Invoke(photo,"Analyze"); break;
                    case 16:
                        if(Analysis.IsBusy || Analysis.Result==null) return;
                        Require(Analysis.Result.Status==ResultStatus.Rejected && Analysis.Result.Bricks.Length>0,"Cropped scan lost usable detections.");
                        OpenReviewButton(); break;
                    case 17:
                        Require(review.IsOpen && ReferenceEquals(retained,demo.Session.Current),"Partial scan replaced the active map.");
                        Capture("review-partial-draft"); break;
                    case 18:
                        if(!Size(540,800)) return; Capture("review-narrow-top"); break;
                    case 19:
                        Send(new Event {type=EventType.ScrollWheel,mousePosition=WindowPoint(new Vector2(Screen.width*0.65f,Screen.height*0.65f)),delta=new Vector2(0,100)}); break;
                    case 20:
                        Require(Get<Vector2>(review,"pageScroll").y>400,"Narrow page did not scroll.");
                        Capture("review-narrow-tools"); break;
                    case 21:
                        float scale=Get<float>(review,"uiScale"),height=Screen.height/scale;
                        Click(new Vector2(107,height-44)*scale); break;
                    case 22:
                        Require(!review.IsOpen && photo.IsOpen && ReferenceEquals(retained,demo.Session.Current),"Back button lost edits or changed the level.");
                        Finish(0,"LAYOUT_REVIEW_UI_CHECKS_PASSED: actual photo entry, grid select/move/undo, physical confirmation, JSON snapshots, design edits, coreless 3D preview, guarded publication, cropped draft and narrow scrolling."); return;
                }
                stage++; waitFrame=Time.frameCount+6;
            }
            catch(Exception e) { Finish(1,"LAYOUT_REVIEW_UI_CHECKS_FAILED: stage="+stage+" "+(e.InnerException??e)); }
        }
        private static PhotoScanSession Analysis=>Get<PhotoScanSession>(photo,"analysis");
        private static void OpenReviewButton()
        {
            float s=Get<float>(photo,"uiScale"),width=Screen.width/s,body=Mathf.Min(width-44,1320),right=Mathf.Clamp(body*0.35f,326,408);
            Click(new Vector2((width-body)/2+body-80,Screen.height/s-55)*s);
        }
        private static void ActionButton()=>Click(Get<Rect>(review,"actionArea").center*Get<float>(review,"uiScale"));
        private static void Cell(int c,int r)
        { Rect grid=Get<Rect>(review,"gridArea"); Click(new Vector2(grid.x+(c+0.5f)*grid.width/10,grid.y+(r+0.5f)*grid.height/10)*Get<float>(review,"uiScale")); }
        private static string Fixture(string file)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Tests/Fixtures",file));
        private static T Get<T>(object obj,string field)=>(T)obj.GetType().GetField(field,Private).GetValue(obj);
        private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
        private static void Invoke(object obj,string method)=>obj.GetType().GetMethod(method,Private).Invoke(obj,null);
        private static bool Size(int w,int h)
        {
            Vector2 target=(Vector2)game.GetType().GetProperty("targetRenderSize",Private).GetValue(game);
            if(Math.Abs(target.x-w)<8 && Math.Abs(target.y-h)<8 && Math.Abs(Screen.width-w)<8 && Math.Abs(Screen.height-h)<8) return true;
            Rect p=game.position; float density=target.x/p.width; p.width+=(w-target.x)/density; p.height+=(h-target.y)/density;
            game.position=p; waitFrame=Time.frameCount+4; return false;
        }
        private static Vector2 WindowPoint(Vector2 point)
        {
            float scale=(float)game.GetType().GetProperty("gameMouseScale",Private).GetValue(game);
            Vector2 offset=(Vector2)game.GetType().GetProperty("gameMouseOffset",Private).GetValue(game);
            return point/scale-offset;
        }
        private static void Click(Vector2 point)
        { var p=WindowPoint(point); Send(new Event {type=EventType.MouseDown,button=0,mousePosition=p}); Send(new Event {type=EventType.MouseUp,button=0,mousePosition=p}); }
        private static void Send(Event e) { game.Focus(); game.SendEvent(e); }
        private static void Capture(string file)
        { ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Output,""),file+".png")); Debug.Log("REVIEW_UI_CAPTURE: "+file+" "+Screen.width+"x"+Screen.height); }
        private static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        private static void Finish(int code,string message)
        {
            if(!string.IsNullOrEmpty(profileKey)) { PlayerPrefs.DeleteKey(profileKey); PlayerPrefs.Save(); }
            SessionState.SetBool(Running,false); EditorApplication.update-=Tick;
            if(code==0) Debug.Log(message); else Debug.LogError(message);
            if(game!=null) game.Close(); EditorApplication.Exit(code);
        }
    }
}
