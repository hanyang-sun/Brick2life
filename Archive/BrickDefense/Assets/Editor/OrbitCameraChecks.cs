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
    // Integration check through the same Game-view mouse events used by players.
    [InitializeOnLoad]
    public static class OrbitCameraChecks
    {
        private const string Running="BrickDefense.OrbitCheck.Running",Output="BrickDefense.OrbitCheck.Output";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static EditorWindow gameView;
        private static BrickDefenseDemo demo;
        private static PhotoScanPanel panel;
        private static LevelView view;
        private static LevelContext retained;
        private static Quaternion pose,initial;
        private static int stage,waitFrame,resizeAttempts;
        private static double deadline;
        private static Action afterCapture;
        private static float fitSize;
        private static object verificationSize,originalSizeType;
        private static int originalWidth,originalHeight;

        static OrbitCameraChecks()
        { if(SessionState.GetBool(Running,false)) { deadline=EditorApplication.timeSinceStartup+120; EditorApplication.update+=Tick; } }

        public static void Verify()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(!project.EndsWith("/.scratch/soft-strategy-ui/unity-check",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run only in the isolated soft-strategy-ui verification project.");
            string output=Path.Combine(project,"Artifacts/Orbit",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output); SessionState.SetString(Output,output); SessionState.SetBool(Running,true);
            EditorSceneManager.OpenScene("Assets/Scenes/BrickDefenseDemo.unity");
            foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if(window.GetType().Name=="GameView") window.Close();
            gameView=(EditorWindow)ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.ShowUtility(); gameView.position=new Rect(24,24,1280,824); gameView.Focus();
            gameView.GetType().GetProperty("selectedSizeIndex").SetValue(gameView,0);
            stage=0; deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
        }

        private static Quaternion Rotation=>Camera.main.transform.rotation;
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Timeout at stage "+stage);
                if(gameView==null)
                    foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                        if(window.GetType().Name=="GameView" && !window.docked) { gameView=window; break; }
                Require(gameView!=null,"Missing Game view.");
                foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if(window!=gameView && window.GetType().Name=="GameView") window.Close();
                gameView.GetType().GetMethod("SetFocus",Private).Invoke(gameView,new object[] {true}); gameView.Repaint();
                if(!EditorApplication.isPlaying || Time.frameCount<Math.Max(4,waitFrame)) return;
                demo??=UnityEngine.Object.FindFirstObjectByType<BrickDefenseDemo>();
                panel??=UnityEngine.Object.FindFirstObjectByType<PhotoScanPanel>();
                view??=UnityEngine.Object.FindFirstObjectByType<LevelView>();
                Require(demo!=null && panel!=null && Camera.main!=null,"Scene incomplete.");
                if(afterCapture!=null)
                {
                    Action action=afterCapture; afterCapture=null; action(); waitFrame=Time.frameCount+5; return;
                }
                switch(stage)
                {
                    case 0:
                        if(!Size(1280,800)) return;
                        initial=Rotation; retained=demo.Session.Current;
                        Require(Quaternion.Angle(initial,Quaternion.Euler(55,-45,0))<0.1f,"Default tabletop angle failed.");
                        Framed(); Capture("map-tabletop");
                        afterCapture=()=>
                        {
                            Camera.main.orthographic=false;
                            Require(BrickSizeDifference()>1,"Depth comparison did not detect perspective.");
                            Drag(new Vector2(0.45f,0.50f),new Vector2(0.70f,0.57f));
                        }; break;
                    case 1:
                        Require(Quaternion.Angle(initial,Rotation)>30,"Right drag did not orbit."); Unchanged(); Framed(); Capture("map-orbited"); afterCapture=()=>CameraCommand(2); break;
                    case 2: Same(initial,"Reset did not restore view."); CameraCommand(0); break;
                    case 3:
                        Require(Math.Abs(Mathf.DeltaAngle(initial.eulerAngles.y,Rotation.eulerAngles.y)+45)<0.1f,"Turn-left command failed: "+Rotation.eulerAngles+", initial "+initial.eulerAngles+", viewport "+Screen.width+"x"+Screen.height);
                        CameraCommand(1); break;
                    case 4: Same(initial,"Turn-right command failed."); CameraCommand(3); break;
                    case 5:
                        Require(Math.Abs(initial.eulerAngles.x-Rotation.eulerAngles.x-10)<0.1f,"Lower command failed."); CameraCommand(4); break;
                    case 6:
                        Same(initial,"Higher command failed."); Unchanged();
                        pose=Rotation; DragPixels(StatusPoint(),StatusPoint()+new Vector2(-160,80));
                        DragPixels(StatusPoint(),StatusPoint()+new Vector2(12,0)); break;
                    case 7:
                        Same(pose,"Right drag started on status UI."); Unchanged();
                        Drag(new Vector2(0.5f,0.70f),new Vector2(0.5f,0.35f)); break;
                    case 8:
                        Require(Math.Abs(Rotation.eulerAngles.x-45)<0.1f,"Lower pitch limit failed."); Framed(); Capture("map-low-angle");
                        afterCapture=()=>Drag(new Vector2(0.5f,0.35f),new Vector2(0.5f,0.70f)); break;
                    case 9:
                        Require(Math.Abs(Rotation.eulerAngles.x-75)<0.1f,"Upper pitch limit failed."); Framed(); Capture("map-overhead"); afterCapture=()=>CameraCommand(2); break;
                    case 10:
                        Same(initial,"Reset after pitch limits failed.");
                        DragPixels(Pixel(new Vector2(0.5f,0.5f)),StatusPoint()); break;
                    case 11:
                        Require(Quaternion.Angle(initial,Rotation)>20,"Captured drag stopped when crossing UI."); Unchanged(); pose=Rotation;
                        SendMouse(EventType.MouseDrag,Pixel(new Vector2(0.6f,0.5f)),1); break;
                    case 12:
                        Same(pose,"Mouse-up did not end drag."); Pick(new GridPoint(9,9)); break;
                    case 13:
                        Require(Selected(new GridPoint(9,9)),"Rotated left-click picked wrong cell.");
                        Require(demo.Session.Current.Result.Status==ResultStatus.Success,"Rotated spawn lost route.");
                        retained=demo.Session.Current; pose=Rotation; MainButton(1); break;
                    case 14:
                        Require(demo.Session.Current.Result.Entities.Length==5 && demo.Session.Current.Result.Issues[0].Code=="NO_ROUTE","Blocked example failed.");
                        Same(pose,"Example switch reset the view."); MainButton(0); break;
                    case 15:
                        Require(demo.Session.Current.Result.Entities.Length==9 && demo.Session.Current.Result.Route.Length-1==16,"Reference example failed.");
                        Same(pose,"Reference switch reset the view."); retained=demo.Session.Current; MainButton(2); break;
                    case 16:
                        Require(panel.IsOpen,"Scan page did not open."); pose=Rotation;
                        Drag(new Vector2(0.4f,0.5f),new Vector2(0.6f,0.6f)); break;
                    case 17: Same(pose,"Photo page rotated the board."); Unchanged(); BackButton(); break;
                    case 18:
                        Require(!panel.IsOpen,"Back did not close photo page."); Same(pose,"Closing photo page changed view.");
                        SendMouse(EventType.MouseDown,Pixel(new Vector2(0.5f,0.5f)),1);
                        SendMouse(EventType.MouseDrag,Pixel(new Vector2(0.6f,0.55f)),1); break;
                    case 19: pose=Rotation; panel.Open(); break;
                    case 20: Same(pose,"Opening photo page moved camera."); BackButton(); break;
                    case 21: SendMouse(EventType.MouseDrag,Pixel(new Vector2(0.4f,0.5f)),1); break;
                    case 22:
                        Same(pose,"Photo page did not release drag capture."); SendMouse(EventType.MouseUp,Pixel(new Vector2(0.4f,0.5f)),1);
                        // Full revolution through the camera's programmatic commands.
                        for(int i=0;i<8;i++) CameraCommand(1); break;
                    case 23:
                        Same(pose,"Full revolution did not wrap angles."); Unchanged();
                        SendMouse(EventType.MouseDown,Pixel(new Vector2(0.5f,0.5f)),1);
                        SendMouse(EventType.MouseDrag,new Vector2(-10,Screen.height*0.5f),1); break;
                    case 24: SendMouse(EventType.MouseDrag,Pixel(new Vector2(0.6f,0.6f)),1); break;
                    case 25:
                        Same(pose,"Leaving window did not stop orbit."); SendMouse(EventType.MouseUp,Pixel(new Vector2(0.6f,0.6f)),1);
                        if(!Size(540,800)) return;
                        CameraCommand(1); break;
                    case 26:
                        Require(Quaternion.Angle(pose,Rotation)>40,"Narrow rotation command failed."); Framed(); Capture("map-narrow-orbited"); afterCapture=()=>CameraCommand(2); break;
                    case 27: Same(initial,"Narrow Reset failed."); Pick(new GridPoint(10,9)); break;
                    case 28:
                        Require(Selected(new GridPoint(10,9)),"Narrow rotated view picked wrong cell.");
                        Framed(); Capture("map-narrow-reset"); afterCapture=()=>Camera.main.orthographic=false; break;
                    case 29:
                        Framed();
                        if(!Size(1280,800)) return;
                        fitSize=Camera.main.orthographicSize; pose=Rotation; retained=demo.Session.Current;
                        Scroll(Pixel(new Vector2(0.5f,0.5f)),-3); break;
                    case 30:
                        ZoomIs(1.15f); Same(pose,"Scroll rotated camera."); Unchanged();
                        Scroll(StatusPoint(),-6); break;
                    case 31: ZoomIs(1.15f); CameraCommand(5); break;
                    case 32:
                        ZoomIs(1); CameraCommand(6); break;
                    case 33:
                        ZoomIs(1.15f); Scroll(Pixel(new Vector2(0.5f,0.5f)),-999); break;
                    case 34:
                        ZoomIs(2.5f); Projection(); Capture("map-zoomed"); afterCapture=()=>Pick(new GridPoint(9,9)); break;
                    case 35:
                        Require(Selected(new GridPoint(9,9)),"Zoomed picking failed.");
                        ZoomIs(2.5f); retained=demo.Session.Current; CameraCommand(0); break;
                    case 36:
                        ZoomIs(2.5f); Unchanged(); Scroll(Pixel(new Vector2(0.5f,0.5f)),999); break;
                    case 37:
                        ZoomIs(0.65f); Projection(); Capture("map-zoom-out"); afterCapture=()=>CameraCommand(2); break;
                    case 38:
                        ZoomIs(1); Same(initial,"Reset after zoom failed."); Framed();
                        RunTouchChecks(); break;
                    case 39:
                        Require(panel.IsOpen,"Touch checks did not open photo page.");
                        TouchFrame(TouchAt(70,Pixel(new Vector2(0.57f,0.60f)),TouchPhase.Moved));
                        Scroll(Pixel(new Vector2(0.5f,0.5f)),-6); ZoomIs(1);
                        Same(pose,"Photo page moved camera."); Unchanged(); BackButton(); break;
                    case 40:
                        Require(!panel.IsOpen,"Touch checks could not leave photo page.");
                        TouchFrame(TouchAt(70,CellPixel(new GridPoint(10,9)),TouchPhase.Ended));
                        Unchanged(); Same(pose,"Returning from photo page left a gesture."); TouchFrame();
                        demo.Session.LoadExample(false); view.ResetView();
                        Debug.Log("TOUCH_FRAME_CHECKS_PASSED: tap/jitter, drag, pinch with stationary finger and reordered IDs, transitions, UI, cancellation/focus/pause/photo and duplicate mouse suppression."); break;
                    case 41:
                        if(!Size(540,800)) return;
                        CameraCommand(6); break;
                    case 42:
                        Require(Math.Abs(view.ZoomFactor-1.15f)<0.001f,"Narrow zoom command failed."); CameraCommand(1); break;
                    case 43:
                        Require(Quaternion.Angle(initial,Rotation)>40,"Narrow zoomed orbit command failed.");
                        Vector2 cell=CellPixel(new GridPoint(10,9));
                        TouchFrame(TouchAt(80,cell,TouchPhase.Began));
                        TouchFrame(TouchAt(80,cell,TouchPhase.Ended)); TouchFrame();
                        Require(Selected(new GridPoint(10,9)),"Zoomed narrow touch picked wrong cell.");
                        Projection(); Capture("map-narrow-zoomed"); afterCapture=()=>CameraCommand(2); break;
                    case 44: Framed(); Require(Math.Abs(view.ZoomFactor-1)<0.001f,"Narrow Reset lost default zoom."); break;
                    default:
                        Framed();
                        Finish(0,"ORBIT_CAMERA_CHECKS_PASSED: wheel/camera commands and zoom bounds/reset/persistence; touch-frame tap/drag/pinch, pair transitions, UI/photo isolation, cancellation and duplicate mouse suppression; orthographic near/far sizes, orbit and wide/narrow zoomed picking."); return;
                }
                stage++; resizeAttempts=0; waitFrame=Time.frameCount+5;
            }
            catch(Exception e) { Finish(1,"ORBIT_CAMERA_CHECKS_FAILED: stage="+stage+" "+(e.InnerException??e)); }
        }

        private static void Unchanged() { Require(ReferenceEquals(retained,demo.Session.Current),"Camera changed level state."); }
        private static void Same(Quaternion expected,string message) { Require(Quaternion.Angle(expected,Rotation)<0.1f,message); }
        private static Vector2 Pixel(Vector2 fraction)=>new Vector2(fraction.x*Screen.width,fraction.y*Screen.height);
        private static float Scale=>Mathf.Min(1.5f,Mathf.Min(Screen.width/(Screen.width/(float)Screen.height<0.95f?600f:900f),Screen.height/640f));
        private static Vector2 StatusPoint()=>new Vector2(Screen.width/Scale-156,70)*Scale;
        private static bool Selected(GridPoint point)
        {
            foreach(SpawnRoute route in demo.Session.Current.Result.SpawnRoutes) if(route.Spawn.Equals(point)) return true;
            return false;
        }
        // Camera buttons were removed. These calls test the retained camera API; gestures use real events.
        private static void CameraCommand(int index)
        {
            switch(index)
            {
                case 0: view.TurnView(-45); break;
                case 1: view.TurnView(45); break;
                case 2: view.ResetView(); break;
                case 3: view.TurnView(0,-10); break;
                case 4: view.TurnView(0,10); break;
                case 5: view.ZoomView(-1); break;
                case 6: view.ZoomView(1); break;
            }
        }
        private static void MainButton(int index)
        {
            float width=Screen.width/Scale,height=Screen.height/Scale;
            float dockWidth=Screen.width/(float)Screen.height<0.95f?width-44:700,slot=(dockWidth-44)/3;
            Click(new Vector2((width-dockWidth)/2+14+slot/2+(slot+8)*index,height-61)*Scale);
        }
        private static void BackButton()
        {
            bool narrow=Screen.width/(float)Screen.height<1.1f;
            float scale=Mathf.Min(1.5f,Mathf.Min(Screen.width/(narrow?560f:1000f),Screen.height/(narrow?800f:820f)));
            float width=Screen.width/scale,body=Mathf.Min(width-44,1320);
            Click(new Vector2((width-body)/2+body-63,51)*scale);
        }
        private static void Pick(GridPoint point)
        { Click(CellPixel(point)); }
        private static Vector2 CellPixel(GridPoint point)
        {
            Vector3 p=Camera.main.WorldToScreenPoint(new Vector3(point.X-9.5f,0,9.5f-point.Y));
            return new Vector2(p.x,Screen.height-p.y);
        }
        private static void Projection()
        {
            Require(Camera.main.orthographic,"Projection changed to perspective.");
            Require(BrickSizeDifference()<0.01f,"Identical near/far bricks have different screen sizes.");
        }
        private static void ZoomIs(float factor)
        {
            Require(Math.Abs(view.ZoomFactor-factor)<0.001f,"Wrong zoom factor: "+view.ZoomFactor+", expected "+factor+", screen "+Screen.width+"x"+Screen.height);
            Require(Math.Abs(Camera.main.orthographicSize-fitSize/factor)<0.01f,"Zoom was lost or changed camera distance instead of size.");
            Projection();
        }
        private static void Scroll(Vector2 point,float amount)
        { Send(new Event { type=EventType.ScrollWheel,mousePosition=ToWindow(point),delta=new Vector2(0,amount) }); }
        private static Touch TouchAt(int id,Vector2 gui,TouchPhase phase,Vector2 delta=default)
        {
            return new Touch { fingerId=id,position=new Vector2(gui.x,Screen.height-gui.y),
                deltaPosition=new Vector2(delta.x,-delta.y),phase=phase };
        }
        private static void TouchFrame(params Touch[] touches)
        {
            var ui=demo.GetComponent<DemoUI>();
            typeof(DemoUI).GetMethod("HandleTouches",Private).Invoke(ui,new object[] {
                touches.Length,touches.Length>0?touches[0]:default,touches.Length>1?touches[1]:default });
        }
        private static void RunTouchChecks()
        {
            demo.Session.LoadExample(false); view.ResetView(); TouchFrame();
            Vector2 cell=CellPixel(new GridPoint(9,9)); LevelContext before=demo.Session.Current;
            TouchFrame(TouchAt(7,cell,TouchPhase.Began)); Click(cell);
            Require(ReferenceEquals(before,demo.Session.Current),"Touch selected on press or through simulated mouse.");
            TouchFrame(TouchAt(7,cell+Vector2.one,TouchPhase.Moved,Vector2.one));
            TouchFrame(TouchAt(7,cell+Vector2.one,TouchPhase.Ended));
            Require(Selected(new GridPoint(9,9)) && demo.Session.Current.Result.SpawnRoutes.Count==2,"Tap with jitter failed.");
            Click(CellPixel(new GridPoint(10,9)));
            Require(Selected(new GridPoint(9,9)) && demo.Session.Current.Result.SpawnRoutes.Count==2,"Touch release repeated mouse selection."); TouchFrame();

            retained=demo.Session.Current; Quaternion angle=Rotation;
            Vector2 a=Pixel(new Vector2(0.42f,0.52f)),b=Pixel(new Vector2(0.58f,0.52f));
            Vector2 drag=Pixel(new Vector2(0.57f,0.60f));
            TouchFrame(TouchAt(11,a,TouchPhase.Began));
            TouchFrame(TouchAt(11,drag,TouchPhase.Moved,drag-a));
            Require(Quaternion.Angle(angle,Rotation)>15,"Single-finger drag did not rotate.");
            TouchFrame(TouchAt(11,drag,TouchPhase.Ended)); Unchanged(); TouchFrame(); view.ResetView();
            angle=Rotation;

            TouchFrame(TouchAt(21,a,TouchPhase.Began));
            TouchFrame(TouchAt(21,a,TouchPhase.Stationary),TouchAt(22,b,TouchPhase.Began)); ZoomIs(1);
            Vector2 aWide=Pixel(new Vector2(0.38f,0.52f)),bWide=Pixel(new Vector2(0.62f,0.52f));
            TouchFrame(TouchAt(21,aWide,TouchPhase.Moved,aWide-a),TouchAt(22,b,TouchPhase.Stationary)); ZoomIs(1.25f);
            TouchFrame(TouchAt(22,bWide,TouchPhase.Moved,bWide-b),TouchAt(21,aWide,TouchPhase.Stationary)); ZoomIs(1.5f);
            Same(angle,"Pinch also rotated camera."); Unchanged();
            TouchFrame(TouchAt(21,a,TouchPhase.Moved,a-aWide),TouchAt(22,b,TouchPhase.Moved,b-bWide)); ZoomIs(1);
            TouchFrame(TouchAt(21,a,TouchPhase.Stationary));
            TouchFrame(TouchAt(21,drag,TouchPhase.Moved,drag-a));
            TouchFrame(TouchAt(21,drag,TouchPhase.Ended)); Same(angle,"Pinch to one finger jumped."); Unchanged(); TouchFrame();

            TouchFrame(TouchAt(31,StatusPoint(),TouchPhase.Began));
            TouchFrame(TouchAt(31,drag,TouchPhase.Moved)); TouchFrame(TouchAt(31,cell,TouchPhase.Ended));
            Same(angle,"UI-origin drag rotated map."); Unchanged(); TouchFrame();
            TouchFrame(TouchAt(32,a,TouchPhase.Began),TouchAt(33,StatusPoint(),TouchPhase.Began));
            TouchFrame(TouchAt(32,aWide,TouchPhase.Moved),TouchAt(33,bWide,TouchPhase.Moved));
            ZoomIs(1); Unchanged(); TouchFrame();

            TouchFrame(TouchAt(40,a,TouchPhase.Began),TouchAt(41,b,TouchPhase.Began),TouchAt(42,cell,TouchPhase.Began));
            TouchFrame(TouchAt(40,drag,TouchPhase.Moved)); Same(angle,"Three fingers became a drag."); TouchFrame();
            TouchFrame(TouchAt(50,a,TouchPhase.Began)); TouchFrame(TouchAt(51,drag,TouchPhase.Moved));
            TouchFrame(TouchAt(51,cell,TouchPhase.Ended)); Unchanged(); Same(angle,"Finger replacement jumped."); TouchFrame();
            TouchFrame(TouchAt(60,a,TouchPhase.Began)); TouchFrame(TouchAt(60,a,TouchPhase.Canceled));
            TouchFrame(TouchAt(60,cell,TouchPhase.Ended)); Unchanged(); TouchFrame();
            TouchFrame(TouchAt(61,a,TouchPhase.Began)); TouchFrame(TouchAt(61,new Vector2(-2,400),TouchPhase.Moved));
            TouchFrame(TouchAt(61,cell,TouchPhase.Ended)); Unchanged(); Same(angle,"Outside touch moved map."); TouchFrame();

            TouchFrame(TouchAt(62,a,TouchPhase.Began));
            typeof(DemoUI).GetMethod("OnApplicationFocus",Private).Invoke(demo.GetComponent<DemoUI>(),new object[] {false});
            TouchFrame(TouchAt(62,drag,TouchPhase.Moved)); TouchFrame(TouchAt(62,cell,TouchPhase.Ended));
            Unchanged(); Same(angle,"Focus loss left a gesture."); TouchFrame();
            TouchFrame(TouchAt(63,a,TouchPhase.Began));
            typeof(DemoUI).GetMethod("OnApplicationPause",Private).Invoke(demo.GetComponent<DemoUI>(),new object[] {true});
            TouchFrame(TouchAt(63,drag,TouchPhase.Moved)); TouchFrame(TouchAt(63,cell,TouchPhase.Ended));
            Unchanged(); Same(angle,"Pause left a gesture."); TouchFrame();

            TouchFrame(TouchAt(70,a,TouchPhase.Began)); panel.Open(); pose=angle;
        }
        private static void Framed()
        {
            Require(Camera.main.orthographic && Camera.main.transform.position.y>0,"Camera went below board.");
            Require(BrickSizeDifference()<0.01f,"Identical near/far bricks have different screen sizes.");
            Require(Vector3.Dot(Camera.main.transform.forward,-Camera.main.transform.position.normalized)>0.9999f,"Camera lost board center.");
            float bottomControls=(Screen.width/(float)Screen.height<0.95f?156:96)*Scale+4;
            foreach(float x in new[] {-10.325f,10.325f}) foreach(float z in new[] {-10.325f,10.325f}) foreach(float y in new[] {-0.61f,1.32f})
            {
                Vector3 p=Camera.main.WorldToViewportPoint(new Vector3(x,y,z));
                Require(p.x>0.01f && p.x<0.99f && p.y>0.01f && p.y<0.99f && p.z>0,"Board cropped by rotation or aspect.");
                Require(p.y*Screen.height>bottomControls,"Board hidden by bottom controls.");
            }
        }
        private static float BrickSizeDifference()
        {
            Vector3 forward=Camera.main.transform.forward;
            Vector3 direction=new Vector3(forward.x,0,forward.z).normalized;
            Vector3 near=-direction*8+Vector3.up*0.66f,far=direction*8+Vector3.up*0.66f;
            Require(Camera.main.WorldToScreenPoint(far).z-Camera.main.WorldToScreenPoint(near).z>3,
                "Near/far comparison needs different camera depths.");
            return Vector2.Distance(ProjectedBrickSize(near),ProjectedBrickSize(far));
        }
        private static Vector2 ProjectedBrickSize(Vector3 center)
        {
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
            foreach(int x in new[] {-1,1}) foreach(int y in new[] {-1,1}) foreach(int z in new[] {-1,1})
            {
                Vector3 p=Camera.main.WorldToScreenPoint(center+new Vector3(x*0.92f,y*0.65f,z*0.92f));
                Vector2 pixel=new Vector2(p.x,p.y); min=Vector2.Min(min,pixel); max=Vector2.Max(max,pixel);
            }
            return max-min;
        }
        private static bool Size(int width,int height)
        {
            if(verificationSize==null)
            {
                Type sizes=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
                object instance=sizes.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
                object group=sizes.GetProperty("currentGroup").GetValue(instance);
                verificationSize=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[] {0});
                Type type=verificationSize.GetType();
                originalSizeType=type.GetProperty("sizeType").GetValue(verificationSize);
                originalWidth=(int)type.GetProperty("width").GetValue(verificationSize);
                originalHeight=(int)type.GetProperty("height").GetValue(verificationSize);
            }
            Type sizeType=verificationSize.GetType();
            if((int)sizeType.GetProperty("width").GetValue(verificationSize)!=width ||
                (int)sizeType.GetProperty("height").GetValue(verificationSize)!=height)
            {
                // Modify the isolated process's built-in size in memory, without adding or saving preferences.
                sizeType.GetProperty("sizeType").SetValue(verificationSize,Enum.Parse(originalSizeType.GetType(),"FixedResolution"));
                sizeType.GetProperty("width").SetValue(verificationSize,width);
                sizeType.GetProperty("height").SetValue(verificationSize,height);
                gameView.position=new Rect(24,24,width,height+64); gameView.Focus(); gameView.Repaint();
                waitFrame=Time.frameCount+5; return false;
            }
            if(Screen.width==width && Screen.height==height) return true;
            if(++resizeAttempts>8) throw new Exception("Could not set fixed verification size: "+Screen.width+"x"+Screen.height);
            waitFrame=Time.frameCount+5; return false;
        }
        private static Vector2 ToWindow(Vector2 pixel)
        {
            float scale=(float)gameView.GetType().GetProperty("gameMouseScale",Private).GetValue(gameView);
            Vector2 offset=(Vector2)gameView.GetType().GetProperty("gameMouseOffset",Private).GetValue(gameView);
            return pixel/scale-offset;
        }
        private static void Send(Event current) { gameView.Focus(); gameView.SendEvent(current); }
        private static void SendMouse(EventType type,Vector2 point,int button)
        { Send(new Event { type=type,mousePosition=ToWindow(point),button=button,clickCount=1 }); }
        private static void Click(Vector2 point) { SendMouse(EventType.MouseDown,point,0); SendMouse(EventType.MouseUp,point,0); }
        private static void Drag(Vector2 from,Vector2 to) { DragPixels(Pixel(from),Pixel(to)); }
        private static void DragPixels(Vector2 from,Vector2 to)
        { SendMouse(EventType.MouseDown,from,1); SendMouse(EventType.MouseDrag,to,1); SendMouse(EventType.MouseUp,to,1); }
        private static void Capture(string name)
        { ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Output,""),name+".png")); Debug.Log("ORBIT_CAPTURE: "+name+" "+Screen.width+"x"+Screen.height); }
        private static void Require(bool valid,string message) { if(!valid) throw new Exception(message); }
        private static void Finish(int code,string message)
        {
            SessionState.SetBool(Running,false); EditorApplication.update-=Tick;
            if(verificationSize!=null)
            {
                Type type=verificationSize.GetType();
                type.GetProperty("sizeType").SetValue(verificationSize,originalSizeType);
                type.GetProperty("width").SetValue(verificationSize,originalWidth);
                type.GetProperty("height").SetValue(verificationSize,originalHeight);
            }
            if(code==0) Debug.Log(message); else Debug.LogError(message); EditorApplication.Exit(code);
        }
    }
}
