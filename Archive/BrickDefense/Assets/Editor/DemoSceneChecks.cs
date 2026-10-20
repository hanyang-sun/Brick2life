using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BrickDefense.Core;
using BrickDefense.Demo;
using BrickDefense.Workflow;
using BrickDefense.Infrastructure;
using BrickDefense.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrickDefense.Editor
{
    // Runs in a separate batch project: exercise Start and multiple UI entry points in Play Mode.
    [InitializeOnLoad]
    public static class DemoSceneChecks
    {
        private const string RunningKey = "BrickDefense.DemoSceneChecks.Running";
        private static int stage;
        private static double deadline;
        private static PhotoScanSession photo;
        private static Task<ScanResult> pendingPhoto;
        private static ImportedPhoto imported;
        private static Color32[] sampleColors;

        static DemoSceneChecks()
        {
            if (SessionState.GetBool(RunningKey,false))
            { deadline=EditorApplication.timeSinceStartup+45; EditorApplication.update+=Tick; }
        }

        public static void VerifyExamples()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
            EditorSceneManager.OpenScene("Assets/Scenes/BrickDefenseDemo.unity");
            SessionState.SetBool(RunningKey,true);
            stage=0; deadline=EditorApplication.timeSinceStartup+45;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play Mode check timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                var demo = UnityEngine.Object.FindFirstObjectByType<BrickDefenseDemo>();
                Require(demo != null,"Scene has no enabled BrickDefenseDemo.");
                if (stage == 0)
                {
                    CheckAssemblies();
                    CheckScene(demo,9,ResultStatus.Success,null,16,"initial-reference");
                    Require(demo.Session.TryLoad(PhotoDefinition()),"Photo scan was not applied.");
                }
                else if (stage == 1)
                {
                    CheckScene(demo,9,ResultStatus.Success,null,16,"photograph");
                    LoadExample(demo,false);
                }
                else if (stage == 2)
                {
                    CheckScene(demo,9,ResultStatus.Success,null,16,"reference-after-photo");
                    // Test each button directly after a photo, independently.
                    Require(demo.Session.TryLoad(PhotoDefinition()),"Second photo scan failed.");
                }
                else if (stage == 3)
                {
                    CheckScene(demo,9,ResultStatus.Success,null,16,"second-photograph");
                    LoadExample(demo,true);
                }
                else if (stage == 4)
                {
                    CheckScene(demo,5,ResultStatus.Rejected,"NO_ROUTE",0,"blocked-after-photo");
                    LoadExample(demo,false);
                }
                else if (stage == 5)
                {
                    CheckScene(demo,9,ResultStatus.Success,null,16,"reference-after-blocked");
                    imported=PhotoFileSource.Load(Path.Combine(Application.dataPath,"../Tests/Fixtures/real-unmarked-bricks-v1.jpg"));
                    photo=new PhotoScanSession(PhotoProfileStore.ReadSettings()); photo.SetFrame(imported.Frame);
                    Rgb24 background=photo.SampleColor(650,850);
                    var colors=new[] {photo.SampleColor(350,1380),photo.SampleColor(160,1040),photo.SampleColor(340,570)};
                    sampleColors=new[] {background}.Concat(colors).Select(c=>new Color32(c.R,c.G,c.B,255)).ToArray();
                    CheckProfileRoundTrip(sampleColors);
                    pendingPhoto=photo.AnalyzeAsync(new ColorCalibration(background,colors,
                        new[] {"physical-color-0","physical-color-1","physical-color-2"}));
                }
                else if (stage == 6)
                {
                    if (!pendingPhoto.IsCompleted) return;
                    ScanResult scan=pendingPhoto.GetAwaiter().GetResult();
                    Require(scan.Status==ResultStatus.Success && scan.Bricks.Length==10 &&
                        scan.PhysicalCells.Count(c=>c.State==PhysicalCellState.Empty)==90,"Imported original photo failed.");
                    Require(demo.Session.TryLoad(photo.ConfirmLayout()),"Confirmed original photo failed to load.");
                    var store=new ScanReportStore(Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/StoreCheck")));
                    string directory=store.Save(new PhotoScanRecord { Frame=photo.Frame,Result=scan,Settings=photo.Settings,
                        Calibration=sampleColors,Imported=imported,ProcessingMilliseconds=photo.ProcessingMilliseconds });
                    string json=File.ReadAllText(Path.Combine(directory,"result.json"));
                    Require(json.Contains(scan.ScanId) && json.Contains("minimumColorBrightness") && json.Contains("calibration") &&
                        json.Contains("physical-color-0"),"Scan report lost settings or color records.");
                    Require(File.ReadAllBytes(Path.Combine(directory,"source.jpg")).SequenceEqual(imported.OriginalBytes),"Saved original bytes changed.");
                    var normalized=PhotoFileSource.Load(Path.Combine(directory,"photo.png"));
                    Require(normalized.Frame.Pixels.SequenceEqual(imported.Frame.Pixels),"Normalized PNG orientation or pixels changed.");
                    Debug.Log("ARCHITECTURE_IO_PASSED: original file import, settings, profile round-trip, report and normalized PNG.");
                }
                else if (stage == 7)
                {
                    CheckScene(demo,10,ResultStatus.Success,null,demo.Session.Current.Result.Route.Length-1,"original-photograph-after-refactor");
                    LevelContext previous=demo.Session.Current;
                    Require(!demo.Session.TryLoad(new LayoutDefinition(PhysicalColorScan(),ExampleScans.CreateDemoRules(),LayoutSource.Photograph)) &&
                        ReferenceEquals(previous,demo.Session.Current),"Failed photo changed the displayed context.");
                    var panel=demo.GetComponent<PhotoScanPanel>();
                    string path=Path.Combine(Application.dataPath,"../Tests/Fixtures/real-unmarked-bricks-v1.jpg");
                    panel.LoadPhoto(path);
                    imported=PhotoFileSource.Load(path);
                    photo=new PhotoScanSession(PhotoProfileStore.ReadSettings());
                    photo.SetFrame(imported.Frame);
                    // Exact calibration recovered from the user's rejected scan.
                    Rgb24 background=new Rgb24(65,61,63);
                    var colors=new[] {new Rgb24(251,251,251),new Rgb24(253,97,48),new Rgb24(5,195,254)};
                    sampleColors=new[] {background}.Concat(colors).Select(c=>new Color32(c.R,c.G,c.B,255)).ToArray();
                    pendingPhoto=photo.AnalyzeAsync(new ColorCalibration(background,colors,
                        new[] {"physical-color-0","physical-color-1","physical-color-2"}));
                    deadline=EditorApplication.timeSinceStartup+45;
                }
                else if (stage == 8)
                {
                    if (!pendingPhoto.IsCompleted) return;
                    ScanResult scan=pendingPhoto.GetAwaiter().GetResult();
                    Require(scan.Status==ResultStatus.Success && scan.Bricks.Length==10 &&
                        scan.Bricks.Count(b=>b.ColorId=="physical-color-0")==1 &&
                        scan.Bricks.Count(b=>b.ColorId=="physical-color-1")==7 &&
                        scan.Bricks.Count(b=>b.ColorId=="physical-color-2")==2,"Original real JPG count or colors failed.");
                    Require(scan.Board.CoordinateBasis==LayoutCoordinateBasis.RelativeBrickScale && !scan.Board.IsReliable &&
                        scan.Board.MarkerIds.Length==0,"Plain JPG invented a marked board.");
                    Require(demo.Session.TryLoad(photo.ConfirmLayout()),"Confirmed real JPG did not load.");
                    var store=new ScanReportStore(Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/PlainStoreCheck")));
                    string directory=store.Save(new PhotoScanRecord { Frame=photo.Frame,Result=scan,Settings=photo.Settings,
                        Calibration=sampleColors,Imported=imported,ProcessingMilliseconds=photo.ProcessingMilliseconds });
                    string json=File.ReadAllText(Path.Combine(directory,"result.json"));
                    Require(json.Contains("PlainBackground") && json.Contains("RelativeBrickScale"),"Report lost input mode or relative basis.");
                    Require(File.ReadAllBytes(Path.Combine(directory,"source.jpg")).SequenceEqual(imported.OriginalBytes),"Original JPG was changed.");
                    Debug.Log("PLAIN_REAL_JPG_PASSED: 10 cubes (7 orange, 2 blue, 1 white), user calibration, relative evidence and report.");
                }
                else if (stage == 9)
                {
                    CheckScene(demo,10,ResultStatus.Success,null,demo.Session.Current.Result.Route.Length-1,"real-unmarked-photograph");
                    imported=PhotoFileSource.Load(Path.Combine(Application.dataPath,"../Tests/Fixtures/real-cropped-bricks-v1.jpg"));
                    photo.SetFrame(imported.Frame);
                    pendingPhoto=photo.AnalyzeAsync(new ColorCalibration(new Rgb24(sampleColors[0].r,sampleColors[0].g,sampleColors[0].b),
                        sampleColors.Skip(1).Select(c=>new Rgb24(c.r,c.g,c.b)).ToArray(),
                        new[] {"physical-color-0","physical-color-1","physical-color-2"}));
                }
                else if (stage == 10)
                {
                    if(!pendingPhoto.IsCompleted) return;
                    ScanResult scan=pendingPhoto.GetAwaiter().GetResult();
                    Require(scan.Status==ResultStatus.Rejected && scan.Issues.Any(i=>i.Code=="BRICK_CROPPED") &&
                        scan.Issues.All(i=>i.FrameId==photo.Frame.FrameId && i.ImageRegion.Length==4),"Cropped JPG lost refusal or image evidence.");
                    LevelContext previous=demo.Session.Current;
                    Require(!demo.Session.TryLoad(new LayoutDefinition(scan,PhysicalRules(),LayoutSource.Photograph)) &&
                        ReferenceEquals(previous,demo.Session.Current),"Cropped photo replaced the active map.");
                    var store=new ScanReportStore(Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/CroppedStoreCheck")));
                    string directory=store.Save(new PhotoScanRecord { Frame=photo.Frame,Result=scan,Settings=photo.Settings,
                        Calibration=sampleColors,Imported=imported,ProcessingMilliseconds=photo.ProcessingMilliseconds });
                    var saved=JsonUtility.FromJson<RejectionReport>(File.ReadAllText(Path.Combine(directory,"result.json")));
                    Require(saved.issues.Length==scan.Issues.Length,"Rejected report lost issues.");
                    for(int i=0;i<saved.issues.Length;i++)
                    {
                        Require(saved.issues[i].region.Length==4 && saved.issues[i].frameId==scan.Issues[i].FrameId,"Rejected report lost region metadata.");
                        for(int p=0;p<4;p++) Require(saved.issues[i].region[p].x==scan.Issues[i].ImageRegion[p].X &&
                            saved.issues[i].region[p].y==scan.Issues[i].ImageRegion[p].Y,"Rejected report changed original image coordinates.");
                    }
                    Debug.Log("CROPPED_REAL_JPG_PASSED: rejected, original image regions persisted, active map retained.");
                    demo.Session.LoadExample(false);
                }
                else
                {
                    CheckScene(demo,9,ResultStatus.Success,null,16,"reference-after-unmarked-photo");
                    Finish(0,"DEMO_SCENE_CHECKS_PASSED: default plain-photo workflow, original JPG and normalized PNG, examples, rendering, assemblies and persistence.");
                    return;
                }
                stage++;
            }
            catch (Exception e) { Finish(1,"DEMO_SCENE_CHECKS_FAILED: "+(e.InnerException??e).Message); }
        }

        [Serializable] private sealed class RejectionReport { public SavedIssue[] issues=Array.Empty<SavedIssue>(); }
        [Serializable] private sealed class SavedIssue { public string frameId=""; public SavedPoint[] region=Array.Empty<SavedPoint>(); }
        [Serializable] private sealed class SavedPoint { public double x=0,y=0; }

        private static void CheckAssemblies()
        {
            foreach (string name in new[] {"BrickDefense.Contracts","BrickDefense.Domain","BrickDefense.Vision","BrickDefense.Application","BrickDefense.Algorithms"})
            {
                var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name==name);
                Require(!assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("UnityEngine") || a.Name.StartsWith("UnityEditor")),
                    name+" depends on Unity.");
            }
            foreach (var type in new[] {typeof(BrickDefenseDemo),typeof(PhotoFileSource)})
                Require(!type.Assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("UnityEditor")),"Runtime module depends on UnityEditor.");
            Require(PhotoFileSource.EditorPicker!=null,"Editor file-picker adapter was not installed.");
            Debug.Log("ARCHITECTURE_ASSEMBLIES_PASSED: pure modules and runtime/editor separation.");
        }
        private static void CheckProfileRoundTrip(Color32[] colors)
        {
            var store=new PhotoProfileStore("BrickDefense.Check."+Guid.NewGuid().ToString("N"));
            try
            {
                store.SaveCalibration(colors);
                Color32[] restored=store.LoadCalibration();
                Require(restored!=null && restored.SequenceEqual(colors),"Stored color calibration changed.");
            }
            finally { store.ResetCalibration(); }
        }

        private static void CheckScene(BrickDefenseDemo demo,int expectedBricks,ResultStatus status,string issue,int steps,string name)
        {
            var level = demo.Session.Current.Result;
            // Destroy is deferred until frame end. Inspect the active replacement,
            // not an inactive old container still waiting for destruction.
            var roots = demo.transform.Cast<Transform>().Where(t=>t.name=="Physical bricks" && t.gameObject.activeInHierarchy).ToArray();
            Require(roots.Length==1,name+": expected one active brick container.");
            var root = roots[0];
            string issues = string.Join(",",level.Issues.Select(i => i.Code));
            Require(level.Entities.Length == expectedBricks && root != null && root.childCount == expectedBricks,
                name+": expected "+expectedBricks+" bricks, entities="+level.Entities.Length+
                ", rendered="+(root==null?-1:root.childCount)+", issues="+issues);
            Require(level.Status == status && (issue==null || level.Issues.Any(i => i.Code==issue)),name+": wrong status "+issues);
            Require(status == ResultStatus.Rejected ? level.Route.Length == 0 : level.Route.Length-1 == steps,name+": wrong route.");
            Require(demo.transform.Find("Navigation grid").childCount == 400,name+": navigation board is missing.");
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                Require(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.shader.isSupported && GeometryUtility.TestPlanesAABB(planes,renderer.bounds),
                    name+": a brick cannot be rendered by the scene camera.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) SaveView(name);
            Debug.Log("DEMO_STAGE_PASSED: "+name+" bricks="+root.childCount+" status="+level.Status);
        }

        private static void SaveView(string name)
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280,800,24);
            var pixels = new Texture2D(1280,800,TextureFormat.RGB24,false);
            RenderTexture oldActive = RenderTexture.active,oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply();
                string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts"));
                Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget; RenderTexture.active=oldActive;
                UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }

        private static void LoadExample(BrickDefenseDemo demo,bool blocked) => demo.Session.LoadExample(blocked);
        private static LayoutDefinition PhotoDefinition() => new LayoutDefinition(PhysicalColorScan(),PhysicalRules(),LayoutSource.Photograph);
        private static ScanResult PhysicalColorScan()
        {
            ScanResult example=ExampleScans.CreateReference();
            var bricks=example.Bricks.Select(b => new BrickObservation(b.BrickId,b.C,b.R,
                b.ColorId==ExampleScans.CoreColor ? "physical-color-0" : b.ColorId==ExampleScans.WallColor ?
                "physical-color-1" : "physical-color-2")).ToArray();
            return new ScanResult("switching-photo",ResultStatus.Success,example.PhysicalCells,bricks,Array.Empty<Issue>());
        }
        private static LevelRules PhysicalRules() => new LevelRules(new Dictionary<string,BrickRole>
        { {"physical-color-0",BrickRole.Core},{"physical-color-1",BrickRole.Wall},{"physical-color-2",BrickRole.Tower} });
        private static void Require(bool condition,string message) { if (!condition) throw new Exception(message); }
        private static void Finish(int exit,string message)
        {
            EditorApplication.update-=Tick; SessionState.SetBool(RunningKey,false);
            if (exit==0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(exit);
        }
    }
}
