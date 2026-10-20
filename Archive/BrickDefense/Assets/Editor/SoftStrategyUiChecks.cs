using System;
using System.IO;
using System.Reflection;
using BrickDefense.Core;
using BrickDefense.Demo;
using BrickDefense.Infrastructure;
using BrickDefense.Presentation;
using BrickDefense.Vision;
using BrickDefense.Workflow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrickDefense.Editor
{
    // Real Game-view events and screenshots; never runs against the user's working scene.
    [InitializeOnLoad]
    public static class SoftStrategyUiChecks
    {
        private const string Running="BrickDefense.SoftUI.Running",DirectoryKey="BrickDefense.SoftUI.Directory";
        private const string PlainOnly="BrickDefense.SoftUI.PlainOnly",PaletteBackup="BrickDefense.SoftUI.PaletteBackup",
            PaletteExisted="BrickDefense.SoftUI.PaletteExisted",SavedPalette="BrickDefense.PhotoCalibration.plain.v1";
        private static EditorWindow gameView;
        private static int stage,waitFrame,resizeAttempts;
        private static double deadline;
        private static BrickDefenseDemo demo;
        private static PhotoScanPanel panel;
        private static string profileKey;
        private static Rgb24 expectedSample;
        private static LevelContext retained;
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

        static SoftStrategyUiChecks()
        { if (SessionState.GetBool(Running,false)) { deadline=EditorApplication.timeSinceStartup+120; EditorApplication.update+=Tick; } }

        public static void Verify() => Run(false);
        public static void VerifyPlainOnly() => Run(true);
        private static void Run(bool plainOnly)
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if (!project.EndsWith("/.scratch/soft-strategy-ui/unity-check",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run in the isolated soft-strategy-ui verification project.");
            string output=plainOnly?Path.GetFullPath(Path.Combine(project,"../../../.scratch/plain-only-photo/verification")):
                Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/SoftUI",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(output); SessionState.SetString(DirectoryKey,output); SessionState.SetBool(Running,true);
            SessionState.SetBool(PlainOnly,plainOnly);
            if (plainOnly)
            {
                SessionState.SetBool(PaletteExisted,PlayerPrefs.HasKey(SavedPalette));
                SessionState.SetString(PaletteBackup,PlayerPrefs.GetString(SavedPalette,""));
                new PhotoProfileStore().SaveCalibration(ExistingPalette());
            }
            EditorSceneManager.OpenScene("Assets/Scenes/BrickDefenseDemo.unity");
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetType().Name=="GameView") window.Close();
            gameView=(EditorWindow)ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.ShowUtility(); gameView.position=new Rect(24,24,1280,824); gameView.Focus();
            gameView.GetType().GetProperty("selectedSizeIndex").SetValue(gameView,0);
            deadline=EditorApplication.timeSinceStartup+120; stage=0;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup>deadline) throw new Exception("UI check timed out at stage "+stage);
                if (gameView==null)
                    foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                        if (window.GetType().Name=="GameView" && !window.docked) { gameView=window;break; }
                Require(gameView!=null,"Verification Game view missing."); gameView.Repaint();
                // Only one render target: other Game views overwrite Screen dimensions and UI caches.
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (window!=gameView && window.GetType().Name=="GameView") window.Close();
                gameView.GetType().GetMethod("SetFocus",Private).Invoke(gameView,new object[] {true});
                if (!EditorApplication.isPlaying || Time.frameCount<Math.Max(4,waitFrame)) return;
                demo??=UnityEngine.Object.FindFirstObjectByType<BrickDefenseDemo>();
                panel??=UnityEngine.Object.FindFirstObjectByType<PhotoScanPanel>();
                Require(demo!=null && panel!=null,"Scene composition missing.");
                switch (stage)
                {
                    case 0:
                        if (!Size(1280,800)) return;
                        if (SessionState.GetBool(PlainOnly,false)) PhotoScanTools.VerifyPhotoIntegration();
                        Require(demo.Session.Current.Result.Entities.Length==9,"Reference missing."); Capture("map-reference"); break;
                    case 1: MainButton(1); break;
                    case 2:
                        Require(demo.Session.Current.Definition.Source==LayoutSource.Blocked && demo.Session.Current.Result.Entities.Length==5,
                            "Enclosed-core button did not load its example."); Capture("map-enclosed"); break;
                    case 3: MainButton(0); break;
                    case 4:
                        Require(demo.Session.Current.Definition.Source==LayoutSource.Reference,"Reference button failed; source="+demo.Session.Current.Definition.Source+", scanOpen="+panel.IsOpen);
                        Vector3 point=Camera.main.WorldToScreenPoint(new Vector3(-0.5f,0,-5.5f));
                        Click(new Vector2(point.x,Screen.height-point.y)); break;
                    case 5:
                        bool selected=false;
                        foreach(SpawnRoute entrance in demo.Session.Current.Result.SpawnRoutes)
                            if(entrance.Spawn.Equals(new GridPoint(9,15))) selected=true;
                        Require(selected,"Scaled map click selected another cell.");
                        Debug.Log("SOFT_UI_MAP_INPUT_PASSED: both buttons and projected cell (9,15)."); MainButton(2); break;
                    case 6:
                        Require(panel.IsOpen,"Scan button did not open photo page.");
                        if (SessionState.GetBool(PlainOnly,false))
                        {
                            Color32[] expected=ExistingPalette(),restored=Get<Color32[]>("samples");
                            bool[] ready=Get<bool[]>("sampled");
                            for (int i=0;i<4;i++) Require(ready[i] && restored[i].Equals(expected[i]),
                                "The default scan page did not restore the existing plain-photo palette.");
                            Debug.Log("PLAIN_DEFAULT_PROFILE_PASSED: existing background/core/wall/tower colors restored without selecting a mode.");
                        }
                        Capture("photo-empty"); break;
                    case 7:
                        panel.LoadPhoto(Fixture("real-unmarked-bricks-v1.jpg"));
                        profileKey="BrickDefense.SoftUI.Test."+Guid.NewGuid().ToString("N");
                        Set("profiles",new PhotoProfileStore(profileKey));
                        Set("reports",new ScanReportStore(Path.Combine(SessionState.GetString(DirectoryKey,""),"ScanReports")));
                        var colors=Get<Color32[]>("samples");
                        colors[0]=new Color32(65,61,63,255); colors[1]=new Color32(251,251,251,255);
                        colors[2]=new Color32(253,97,48,255); colors[3]=new Color32(5,195,254,255);
                        var sampled=Get<bool[]>("sampled"); for (int i=0;i<4;i++) sampled[i]=true;
                        break;
                    case 8: PhotoClick(new Vector2(RightX()+RightWidth()-72,208)); break;
                    case 9:
                        Require(Get<bool>("calibrating"),"Color setup button failed.");
                        PhotoClick(new Vector2(RightX()+100,307)); break;
                    case 10:
                        Require(Get<int>("activeSample")==1,"Core sample button failed.");
                        expectedSample=Get<PhotoScanSession>("analysis").SampleColor(331,1350);
                        Rect shown=Get<Rect>("shownPhoto");
                        Click(new Vector2(shown.x+331.5f/1080*shown.width,shown.y+1350.5f/2449*shown.height)*Get<float>("uiScale")); break;
                    case 11:
                        Color32 sampledColor=Get<Color32[]>("samples")[1];
                        Require(Get<int>("activeSample")==-1 && Math.Abs(sampledColor.r-expectedSample.R)<=2 &&
                            Math.Abs(sampledColor.g-expectedSample.G)<=2 && Math.Abs(sampledColor.b-expectedSample.B)<=2,
                            "Photo coordinates sampled the wrong pixels.");
                        PhotoClick(new Vector2(RightX()+80,433)); break;
                    case 12:
                        Require(!Get<bool>("calibrating"),"Save colors failed.");
                        Require(new PhotoProfileStore(profileKey).LoadCalibration()!=null,"Calibration was not saved."); AnalyzeButton(); break;
                    case 13:
                        if (Get<PhotoScanSession>("analysis").IsBusy || Result==null) return;
                        Require(Result.Status==ResultStatus.Success && Result.Bricks.Length==10,"UI analysis lost real-photo layout.");
                        if (SessionState.GetBool(PlainOnly,false))
                        {
                            string reportPath=Path.Combine(SessionState.GetString(DirectoryKey,""),"ScanReports","Scans",Result.ScanId,"result.json");
                            string report=File.ReadAllText(reportPath);
                            Require(report.Contains("PlainBackground") && report.Contains("RelativeBrickScale") && !report.Contains("MarkedBoard"),
                                "The default scan report did not record the relative plain-photo basis.");
                            Debug.Log("PLAIN_REPORT_PASSED: default input and relative coordinates persisted.");
                        }
                        Capture("photo-success"); break;
                    case 14:
                        CheckOpaque("photo-success"); CheckPhotoColor("photo-success"); ConfirmButton(); break;
                    case 15:
                        var review=UnityEngine.Object.FindFirstObjectByType<LayoutReviewPanel>();
                        Require(review!=null && review.IsOpen,"Photo confirmation did not open review.");
                        typeof(LayoutReviewPanel).GetField("store",Private).SetValue(review,new LayoutReviewStore(
                            Path.Combine(SessionState.GetString(DirectoryKey,""),"ScanReports")));
                        Require(review.Session.ConfirmPhysical(),"Real-photo physical review failed.");
                        typeof(LayoutReviewPanel).GetMethod("Continue",Private).Invoke(review,null);
                        Require(!panel.IsOpen && demo.Session.Current.Result.Entities.Length==10,"Confirm button failed.");
                        retained=demo.Session.Current; panel.LoadPhoto(Fixture("real-cropped-bricks-v1.jpg")); panel.Open(); break;
                    case 16: AnalyzeButton(); break;
                    case 17:
                        if (Get<PhotoScanSession>("analysis").IsBusy || Result==null) return;
                        Require(Result.Status==ResultStatus.Rejected && Result.Issues.Length>0,"Cropped input was accepted.");
                        Capture("photo-rejected"); break;
                    case 18: CheckOpaque("photo-rejected"); ConfirmButton(); break;
                    case 19:
                        var croppedReview=UnityEngine.Object.FindFirstObjectByType<LayoutReviewPanel>();
                        Require(panel.IsOpen && ReferenceEquals(retained,demo.Session.Current),"Cropped draft replaced the map before correction.");
                        if(croppedReview.IsOpen) croppedReview.Close();
                        if (!Size(862,678)) return; Capture("photo-small"); break;
                    case 20:
                        if (!Size(540,800)) return; Capture("photo-narrow-top"); break;
                    case 21: Send(new Event { type=EventType.ScrollWheel,mousePosition=ToWindow(new Vector2(Screen.width*0.7f,Screen.height*0.7f)),delta=new Vector2(0,100) }); break;
                    case 22:
                        Require(Get<Vector2>("pageScroll").y>400,"Narrow layout does not scroll to results.");
                        Capture("photo-narrow-bottom"); break;
                    case 23:
                        float s=Get<float>("uiScale"),w=Screen.width/s,bw=Mathf.Min(w-44,1320),left=(w-bw)/2;
                        PhotoClick(new Vector2(left+bw-63,51)); break;
                    case 24:
                        Require(!panel.IsOpen,"Narrow Back button inaccessible."); Capture("map-narrow"); break;
                    case 25:
                        if (!Size(862,678)) return; Capture("map-small"); break;
                    default:
                        Require(demo.Session.Current.Result.Entities.Length==10,"Layout lost during window resizing.");
                        Finish(0,SessionState.GetBool(PlainOnly,false)?
                            "PLAIN_ONLY_UI_CHECKS_PASSED: default saved palette, real photo without mode selection, calibration/save, 10 cubes, confirm/rejection, wide/small/narrow controls and scrolling.":
                            "SOFT_UI_CHECKS_PASSED: actual mouse controls, scaled spawn/photograph sampling, saved palette, analysis/confirm/rejection, opaque raw photos, wide/small/narrow screenshots and scrolling."); return;
                }
                stage++; resizeAttempts=0; waitFrame=Time.frameCount+5;
            }
            catch (Exception e) { Finish(1,"SOFT_UI_CHECKS_FAILED: stage="+stage+" "+(e.InnerException??e).Message); }
        }

        private static ScanResult Result=>Get<PhotoScanSession>("analysis").Result;
        private static Color32[] ExistingPalette()=>new[] {new Color32(65,61,63,255),new Color32(251,251,251,255),
            new Color32(253,97,48,255),new Color32(5,195,254,255)};
        private static string Fixture(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Tests/Fixtures",name));
        private static T Get<T>(string name)=>(T)typeof(PhotoScanPanel).GetField(name,Private).GetValue(panel);
        private static void Set(string name,object value)=>typeof(PhotoScanPanel).GetField(name,Private).SetValue(panel,value);
        private static bool Size(int width,int height)
        {
            Vector2 target=(Vector2)gameView.GetType().GetProperty("targetRenderSize",Private).GetValue(gameView);
            // Editor window borders vary by a few pixels; verify the actual rendered size.
            if (Math.Abs(target.x-width)<=8 && Math.Abs(target.y-height)<=8)
            {
                if (Math.Abs(Screen.width-width)<=8 && Math.Abs(Screen.height-height)<=8) return true;
                waitFrame=Time.frameCount+4; return false;
            }
            if (++resizeAttempts>8) throw new Exception("Cannot size Game view: "+Screen.width+"x"+Screen.height);
            Rect p=gameView.position; float density=target.x/p.width;
            p.width+=(width-target.x)/density; p.height+=(height-target.y)/density;
            gameView.position=p; gameView.Focus(); waitFrame=Time.frameCount+4; return false;
        }
        private static void MainButton(int index)
        {
            float s=Mathf.Min(1.5f,Mathf.Min(Screen.width/900f,Screen.height/640f)),w=Screen.width/s,h=Screen.height/s;
            float slot=656f/3;
            Click(new Vector2((w-700)/2+14+slot/2+(slot+8)*index,h-61)*s);
        }
        private static float PhotoWidth=>Screen.width/Get<float>("uiScale");
        private static float BodyWidth=>Mathf.Min(PhotoWidth-44,1320);
        private static float RightWidth()=>Mathf.Clamp(BodyWidth*0.35f,326,408);
        private static float RightX()=>(PhotoWidth-BodyWidth)/2+BodyWidth-RightWidth();
        private static void PhotoClick(Vector2 point)=>Click(point*Get<float>("uiScale"));
        private static void AnalyzeButton()=>PhotoClick(new Vector2(RightX()+75,Screen.height/Get<float>("uiScale")-55));
        private static void ConfirmButton()=>PhotoClick(new Vector2(RightX()+RightWidth()-80,Screen.height/Get<float>("uiScale")-55));
        private static Vector2 ToWindow(Vector2 point)
        {
            Type type=gameView.GetType();
            float scale=(float)type.GetProperty("gameMouseScale",Private).GetValue(gameView);
            Vector2 offset=(Vector2)type.GetProperty("gameMouseOffset",Private).GetValue(gameView);
            return point/scale-offset;
        }
        private static void Send(Event e) { gameView.Focus(); gameView.SendEvent(e); }
        private static void Click(Vector2 screen)
        {
            Vector2 window=ToWindow(screen);
            Send(new Event { type=EventType.MouseDown,mousePosition=window,button=0,clickCount=1 });
            Send(new Event { type=EventType.MouseUp,mousePosition=window,button=0,clickCount=1 });
        }
        private static string CapturePath(string name)=>Path.Combine(SessionState.GetString(DirectoryKey,""),name+".png");
        private static void Capture(string name)
        { ScreenCapture.CaptureScreenshot(CapturePath(name)); Debug.Log("SOFT_UI_CAPTURE: "+name+" "+Screen.width+"x"+Screen.height); }
        private static Texture2D ReadCapture(string name)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
            Require(File.Exists(CapturePath(name)) && texture.LoadImage(File.ReadAllBytes(CapturePath(name))),"Screenshot missing: "+name);
            return texture;
        }
        private static void CheckOpaque(string name)
        {
            var screenshot=ReadCapture(name);
            try
            {
                float s=Get<float>("uiScale");
                for (float x=RightX()+20;x<RightX()+RightWidth()-20;x+=9)
                {
                    Color32 c=screenshot.GetPixel(Mathf.RoundToInt(x*s),screenshot.height-1-Mathf.RoundToInt(520*s));
                    Require(Math.Abs(c.r-239)<=3 && Math.Abs(c.g-237)<=3 && Math.Abs(c.b-224)<=3,"3D board leaks through photo page.");
                }
            }
            finally { UnityEngine.Object.Destroy(screenshot); }
        }
        private static void CheckPhotoColor(string name)
        {
            var screenshot=ReadCapture(name);
            try
            {
                Rect shown=Get<Rect>("shownPhoto"); float s=Get<float>("uiScale");
                Color32 c=screenshot.GetPixel(Mathf.RoundToInt((shown.x+331.5f/1080*shown.width)*s),
                    screenshot.height-1-Mathf.RoundToInt((shown.y+1350.5f/2449*shown.height)*s));
                Require(Math.Abs(c.r-expectedSample.R)<=8 && Math.Abs(c.g-expectedSample.G)<=8 && Math.Abs(c.b-expectedSample.B)<=8,"Theme tinted the original photo.");
                Debug.Log("SOFT_UI_PHOTO_INPUT_PASSED: original pixel sample, persistence and untinted photo.");
            }
            finally { UnityEngine.Object.Destroy(screenshot); }
        }
        private static void Require(bool value,string message) { if (!value) throw new Exception(message); }
        private static void Finish(int code,string message)
        {
            if (!string.IsNullOrEmpty(profileKey)) { PlayerPrefs.DeleteKey(profileKey); PlayerPrefs.Save(); }
            if (SessionState.GetBool(PlainOnly,false))
            {
                if (SessionState.GetBool(PaletteExisted,false)) PlayerPrefs.SetString(SavedPalette,SessionState.GetString(PaletteBackup,""));
                else PlayerPrefs.DeleteKey(SavedPalette);
                PlayerPrefs.Save(); SessionState.SetBool(PlainOnly,false);
            }
            SessionState.SetBool(Running,false); EditorApplication.update-=Tick;
            if (gameView!=null) gameView.Close();
            if (code==0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(code);
        }
    }
}
