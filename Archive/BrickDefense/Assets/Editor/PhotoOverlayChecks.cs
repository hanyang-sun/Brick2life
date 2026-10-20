using System;
using System.IO;
using BrickDefense.Demo;
using BrickDefense.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrickDefense.Editor
{
    // Real Game-view screenshot: Camera.Render omits IMGUI and cannot catch this bug.
    [InitializeOnLoad]
    public static class PhotoOverlayChecks
    {
        private const string Running="BrickDefense.PhotoOverlayCheck.Running",Capture="BrickDefense.PhotoOverlayCheck.Capture";
        private static int stage,openedFrame;
        private static int resizedFrame=-10,resizeAttempts;
        private static double deadline;
        private static EditorWindow gameView;
        static PhotoOverlayChecks()
        { if (SessionState.GetBool(Running,false)) { deadline=EditorApplication.timeSinceStartup+60; EditorApplication.update+=Tick; } }
        public static void Verify()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if (!project.EndsWith("/.scratch/scan-rejection/unity-check",StringComparison.OrdinalIgnoreCase) &&
                !project.EndsWith("/.scratch/soft-strategy-ui/unity-check",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use an isolated scan-rejection or soft-strategy-ui verification project.");
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/Overlay")); Directory.CreateDirectory(directory);
            SessionState.SetString(Capture,Path.Combine(directory,"overlay-"+Guid.NewGuid().ToString("N")+".png"));
            SessionState.SetBool(Running,true); stage=0; resizeAttempts=0; resizedFrame=-10; deadline=EditorApplication.timeSinceStartup+60;
            EditorSceneManager.OpenScene("Assets/Scenes/BrickDefenseDemo.unity");
            Type gameType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if(gameType==null) throw new Exception("Editor has no Game-view window type.");
            foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if(window.GetType().Name=="GameView") window.Close();
            gameView=(EditorWindow)ScriptableObject.CreateInstance(gameType);
            gameView.titleContent=new GUIContent("Photo overlay check");
            gameView.ShowUtility(); gameView.position=new Rect(40,40,862,702); gameView.Focus();
            gameType.GetProperty("selectedSizeIndex").SetValue(gameView,0);
            EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup>deadline) throw new Exception("Overlay screenshot check timed out.");
                if(gameView==null)
                    foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                        if(window.GetType().Name=="GameView" && !window.docked) { gameView=window;break; }
                if(gameView==null) throw new Exception("Verification Game view disappeared.");
                foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if(window!=gameView && window.GetType().Name=="GameView") window.Close();
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                gameView.GetType().GetMethod("SetFocus",flags).Invoke(gameView,new object[] {true});
                if(gameView!=null) gameView.Repaint();
                if (!EditorApplication.isPlaying || Time.frameCount<3) return;
                if (stage==0)
                {
                    // Match the user's 862x678 Game area, accounting for editor chrome.
                    if(Math.Abs(Screen.width-862)>8 || Math.Abs(Screen.height-678)>8)
                    {
                        if(Time.frameCount<resizedFrame+3) return;
                        Vector2 target=(Vector2)gameView.GetType().GetProperty("targetRenderSize",flags).GetValue(gameView);
                        if (Math.Abs(target.x-862)<=8 && Math.Abs(target.y-678)<=8)
                        { resizedFrame=Time.frameCount; return; }
                        if(++resizeAttempts>10) throw new Exception($"Cannot set verification Game size: {Screen.width}x{Screen.height}.");
                        Rect position=gameView.position;
                        float density=target.x/position.width;
                        position.width+=(862-target.x)/density; position.height+=(678-target.y)/density;
                        gameView.position=position;gameView.Focus();resizedFrame=Time.frameCount;return;
                    }
                    var panel=UnityEngine.Object.FindFirstObjectByType<PhotoScanPanel>();
                    if (panel==null) throw new Exception("Scene has no photo panel.");
                    panel.LoadPhoto(Path.Combine(Application.dataPath,"../Tests/Fixtures/real-cropped-bricks-v1.jpg"));
                    panel.Open(); openedFrame=Time.frameCount; stage=1;
                    Debug.Log($"OVERLAY_OPENED: frame={openedFrame}, screen={Screen.width}x{Screen.height}");
                }
                else if (stage==1)
                {
                    if (Time.frameCount<openedFrame+3) return;
                    ScreenCapture.CaptureScreenshot(SessionState.GetString(Capture,"")); stage=2;
                    Debug.Log("OVERLAY_CAPTURE_REQUESTED");
                }
                else
                {
                    string path=SessionState.GetString(Capture,""); if (!File.Exists(path)) return;
                    var screenshot=new Texture2D(2,2,TextureFormat.RGB24,false);
                    try
                    {
                        if (!screenshot.LoadImage(File.ReadAllBytes(path))) throw new Exception("Cannot read Game-view capture.");
                        float scale=Mathf.Min(1.5f,Mathf.Min(screenshot.width/1000f,screenshot.height/820f));
                        float width=screenshot.width/scale,bodyWidth=Mathf.Min(width-44,1320);
                        float left=(width-bodyWidth)/2,rightWidth=Mathf.Clamp(bodyWidth*0.35f,326,408);
                        int minR=255,minG=255,minB=255,maxR=0,maxG=0,maxB=0,count=0;
                        // Gap between the calibration and result cards, above the underlying board.
                        for (float logicalX=left+bodyWidth-rightWidth+20;logicalX<left+bodyWidth-20;logicalX+=7)
                        {
                            int x=Mathf.RoundToInt(logicalX*scale),y=screenshot.height-1-Mathf.RoundToInt(520*scale);
                            Color32 color=screenshot.GetPixel(x,y);
                            minR=Math.Min(minR,color.r);maxR=Math.Max(maxR,color.r);
                            minG=Math.Min(minG,color.g);maxG=Math.Max(maxG,color.g);
                            minB=Math.Min(minB,color.b);maxB=Math.Max(maxB,color.b);count++;
                            if (Math.Abs(color.r-239)>3 || Math.Abs(color.g-237)>3 || Math.Abs(color.b-224)>3)
                                throw new Exception("The photo backdrop did not cover the board.");
                        }
                        int variation=Math.Max(maxR-minR,Math.Max(maxG-minG,maxB-minB));
                        Debug.Log($"OVERLAY_PIXEL_RANGE: {screenshot.width}x{screenshot.height}, samples={count}, variation={variation}");
                        if (variation>3) throw new Exception("The 3D map is visible through the photo panel background.");
                        Finish(0,"PHOTO_OVERLAY_CHECK_PASSED: opaque full-screen panel, real Game-view screenshot.");
                    }
                    finally { UnityEngine.Object.Destroy(screenshot); }
                }
            }
            catch (Exception e) { Finish(1,"PHOTO_OVERLAY_CHECK_FAILED: "+e.Message); }
        }
        private static void Finish(int code,string message)
        { SessionState.SetBool(Running,false);EditorApplication.update-=Tick;if(gameView!=null)gameView.Close();if(code==0)Debug.Log(message);else Debug.LogError(message);EditorApplication.Exit(code); }
    }
}
