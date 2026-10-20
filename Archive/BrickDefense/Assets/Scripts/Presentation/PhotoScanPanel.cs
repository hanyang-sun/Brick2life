using System;
using BrickDefense.Core;
using BrickDefense.Infrastructure;
using BrickDefense.Presentation;
using BrickDefense.Vision;
using BrickDefense.Workflow;
using UnityEngine;

namespace BrickDefense.Demo
{
    // 照片工作流界面：采样标定、显示证据并让用户确认数字地图。
    public sealed class PhotoScanPanel : MonoBehaviour
    {
        private static readonly string[] SampleNames={"背景","核心颜色","城墙颜色","防御塔颜色"};
        private readonly Color32[] samples=new Color32[4];
        private readonly bool[] sampled=new bool[4];
        private LevelSession levels;
        private PhotoScanSession analysis;
        private PhotoProfileStore profiles;
        private ScanReportStore reports;
        private LayoutReviewPanel reviewPanel;
        private ScanResult reviewSource;
        private LayoutEditSession reviewSession;
        private CameraCapture cameraCapture;
        private ImportedPhoto imported;
        private Texture2D photo;
        private bool calibrating;
        private int activeSample=-1;
        private string inputPath="",message="请导入照片或使用相机拍摄，确保全部积木完整入镜。";
        private Vector2 issueScroll;
        private ImageFrame frame=>analysis.Frame;
        private ScanResult scan=>analysis.Result;
        private bool busy=>analysis.IsBusy;
        private bool cameraStarting=>cameraCapture.IsStarting;
        private WebCamTexture webcam=>cameraCapture.Texture;
        public bool IsOpen { get; private set; }

        public void Initialize(LevelSession owner)
        {
            levels=owner; analysis=new PhotoScanSession(PhotoProfileStore.ReadSettings());
            reports=new ScanReportStore(Application.persistentDataPath);
            cameraCapture=gameObject.AddComponent<CameraCapture>(); cameraCapture.StatusChanged+=CameraStatus;
            reviewPanel=gameObject.AddComponent<LayoutReviewPanel>();
            reviewPanel.Initialize(owner,GetComponent<LevelView>());
            LoadProfile();
        }
        private void LoadProfile()
        {
            profiles=new PhotoProfileStore();
            Array.Clear(sampled,0,4); Array.Clear(samples,0,4);
            Color32[] stored=profiles.LoadCalibration();
            if (stored!=null) for (int i=0; i<4; i++) { samples[i]=stored[i]; sampled[i]=true; }
        }
        public void Open() { IsOpen=true; }
        private void Close() { reviewPanel.Close(); IsOpen=false; StopCamera(); activeSample=-1; }
        private void CameraStatus(string value) { message=ChineseUi.Message(value); }
        private void Import()
        {
            if(busy) return;
            try
            {
                string selected=PhotoFileSource.PickPath(inputPath);
                if(string.IsNullOrEmpty(selected)) { message="未选择新照片，当前照片和地图保持不变。"; return; }
                inputPath=selected; LoadPhoto(inputPath);
            }
            catch(Exception error) { message="照片选择失败："+ChineseUi.Message(error.Message); }
        }
        public void LoadPhoto(string path)
        {
            if (busy) return;
            try { ImportedPhoto loaded=PhotoFileSource.Load(path); SetFrame(loaded.Frame); imported=loaded; }
            catch (Exception e) { message="照片加载失败："+ChineseUi.Message(e.Message); }
        }
        private void SetFrame(ImageFrame value)
        {
            reviewPanel.Close(); reviewSource=null; reviewSession=null;
            StopCamera(); analysis.SetFrame(value); imported=null;
            if (photo!=null) Destroy(photo);
            photo=PhotoPixels.ToTexture(value);
            message=ReadyCalibration()?"照片已加载，请分析并核对地图，然后确认生成。":
                "请点击“颜色设置”，从照片中采样背景与三种角色颜色。";
        }
        private void StartCamera() { analysis.Invalidate(); activeSample=-1; cameraCapture.Begin(); }
        private void CapturePhoto()
        {
            try { SetFrame(cameraCapture.Capture()); }
            catch (Exception e) { message="拍摄失败："+ChineseUi.Message(e.Message); }
        }
        private void StopCamera() { if (cameraCapture!=null) cameraCapture.Stop(); }
        // 物理颜色编号与角色由四个采样槽位确定，识别器本身不推断游戏角色。
        private ColorCalibration Calibration()
        {
            Rgb24 ToRgb(Color32 c)=>new Rgb24(c.r,c.g,c.b);
            return new ColorCalibration(ToRgb(samples[0]),new[] {ToRgb(samples[1]),ToRgb(samples[2]),ToRgb(samples[3])},
                new[] {"physical-color-0","physical-color-1","physical-color-2"});
        }
        private bool ReadyCalibration()=>Array.TrueForAll(sampled,value=>value) && analysis.CalibrationReady(Calibration());
        // UI 事件入口允许异步分析；仅在对象和当前结果仍有效时更新界面。
        private async void Analyze()
        {
            if (frame==null || busy || !ReadyCalibration()) return;
            activeSample=-1; calibrating=false; message="正在分析照片……";
            try
            {
                await analysis.AnalyzeAsync(Calibration());
                if (this==null || scan==null) return;
                message=scan.Status==ResultStatus.Success?
                    $"识别到 {scan.Bricks.Length} 块积木。当前为相对布局，请核对检测框和数字地图。":
                    "识别有待修正项目。可以进入地图校对，补加、删除或移动积木。";
                try { reports.Save(new PhotoScanRecord { Frame=frame,Result=scan,Settings=analysis.Settings,
                    Calibration=samples,Imported=imported,ProcessingMilliseconds=analysis.ProcessingMilliseconds }); }
                catch (Exception e) { message+=" 扫描记录保存失败："+ChineseUi.Message(e.Message); }
            }
            catch (Exception e) { if (this!=null) message="识别失败："+ChineseUi.Message(e.Message); }
        }

        public void OpenReview()
        {
            if(scan==null || busy || levels.IsLocked) return;
            try
            {
                if(!ReferenceEquals(reviewSource,scan)) { reviewSession=analysis.BeginReview(); reviewSource=scan; }
                reviewPanel.Open(reviewSession,photo,samples,Close);
            }
            catch(Exception error) { message="无法打开校对："+ChineseUi.Message(error.Message); }
        }
        private SoftStrategyTheme theme;
        private Vector2 pageScroll;
        private Rect shownPhoto;
        private float uiScale;

        private void OnGUI()
        {
            if (!IsOpen || reviewPanel.IsOpen) return;
            theme??=new SoftStrategyTheme();
            Matrix4x4 oldMatrix=GUI.matrix; int oldDepth=GUI.depth;
            Color oldColor=GUI.color; bool oldEnabled=GUI.enabled;
            GUISkin oldSkin=GUI.skin;
            bool narrow=Screen.width/(float)Screen.height<1.1f;
            uiScale=Mathf.Min(1.5f,Mathf.Min(Screen.width/(narrow?560f:1000f),Screen.height/(narrow?800f:820f)));
            float width=Screen.width/uiScale,height=Screen.height/uiScale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1)); GUI.depth=-20; GUI.color=Color.white;
            try
            {
                GUI.skin=theme.ScrollSkin;
                // Opaque across the whole viewport, including gaps and rounded card corners.
                SoftStrategyTheme.Solid(new Rect(0,0,width,height),SoftStrategyTheme.Background);
                float bodyWidth=Mathf.Min(width-44,1320),left=(width-bodyWidth)/2;
                GUI.Label(new Rect(left,22,360,16),"将现实积木变成你的游戏关卡",theme.Eyebrow);
                GUI.Label(new Rect(left,44,360,32),"扫描积木",theme.Title);
                GUI.enabled=!busy;
                if (theme.Button(new Rect(left+bodyWidth-126,32,126,38),"返回地图")) Close();
                float x=left,slot=narrow?(bodyWidth-24)/4:112;
                if (theme.Button(new Rect(x,96,narrow?slot:144,36),"导入照片")) Import();
                x+=narrow?slot+8:152;
                GUI.enabled=!busy && !cameraStarting;
                if (theme.Button(new Rect(x,96,slot,36),"启动相机")) StartCamera();
                x+=slot+8;
                GUI.enabled=!busy && webcam!=null && webcam.width>16;
                if (theme.Button(new Rect(x,96,slot,36),"拍照")) CapturePhoto();
                x+=slot+8; GUI.enabled=!busy;
                if (theme.Button(new Rect(x,96,slot,36),"关闭相机")) StopCamera();
                float guideY=144;
                GUI.enabled=true;
                GUI.Label(new Rect(left,guideY,bodyWidth,30),
                    "单层、等大立方体，接近正上方拍摄；全部积木和至少一块独立积木应完整入镜。",theme.Small);
                float bodyY=guideY+36;
#if !UNITY_EDITOR && !UNITY_STANDALONE_WIN
                GUI.enabled=!busy;
                inputPath=GUI.TextField(new Rect(left,bodyY,bodyWidth,34),inputPath,theme.Field);
                bodyY+=46;
#endif
                GUI.enabled=true;
                if (narrow)
                {
                    float contentWidth=bodyWidth-20;
                    pageScroll=GUI.BeginScrollView(new Rect(left,bodyY,bodyWidth,height-bodyY-18),pageScroll,
                        new Rect(0,0,contentWidth,1190));
                    DrawPicture(new Rect(0,0,contentWidth,450));
                    DrawCalibration(new Rect(0,466,contentWidth,332));
                    DrawResult(new Rect(0,814,contentWidth,360));
                    GUI.EndScrollView();
                }
                else
                {
                    float bodyHeight=height-bodyY-22,rightWidth=Mathf.Clamp(bodyWidth*0.35f,326,408);
                    float rightX=left+bodyWidth-rightWidth;
                    DrawPicture(new Rect(left,bodyY,bodyWidth-rightWidth-18,bodyHeight));
                    DrawCalibration(new Rect(rightX,bodyY,rightWidth,332));
                    DrawResult(new Rect(rightX,bodyY+348,rightWidth,bodyHeight-348));
                }
            }
            finally { GUI.skin=oldSkin; GUI.color=oldColor; GUI.enabled=oldEnabled; GUI.matrix=oldMatrix; GUI.depth=oldDepth; }
        }

        private void DrawPicture(Rect area)
        {
            theme.Card(area);
            GUI.Label(new Rect(area.x+18,area.y+16,area.width-36,26),"01 / 照片预览",theme.Heading);
            Rect pictureArea=new Rect(area.x+16,area.y+54,area.width-32,area.height-148);
            SoftStrategyTheme.Round(pictureArea,new Color32(229,231,218,255),8);
            shownPhoto=default;
            if (webcam!=null && webcam.width>16) DrawCamera(pictureArea);
            else if (photo!=null)
            {
                Rect shown=Fit(pictureArea,photo.width,photo.height); shownPhoto=shown;
                GUI.color=Color.white;
                GUI.DrawTexture(shown,photo,ScaleMode.StretchToFill,false);
                DrawEvidence(shown);
                if (!busy && activeSample>=0 && Event.current.type==EventType.MouseDown && Event.current.button==0 && shown.Contains(Event.current.mousePosition))
                {
                    int px=Mathf.Clamp((int)((Event.current.mousePosition.x-shown.x)/shown.width*frame.Width),0,frame.Width-1);
                    int py=Mathf.Clamp((int)((Event.current.mousePosition.y-shown.y)/shown.height*frame.Height),0,frame.Height-1);
                    samples[activeSample]=SamplePatch(px,py); sampled[activeSample]=true; activeSample=-1; analysis.Invalidate();
                    message="采样已记录，请选择下一个角色，最后保存四种颜色。"; Event.current.Use();
                }
            }
            else GUI.Label(pictureArea,"一组积木，一张新地图。\n\n请导入照片，或使用相机拍摄。",theme.Centered);
            bool rejected=scan?.Status==ResultStatus.Rejected;
            Rect note=new Rect(area.x+16,area.yMax-78,area.width-32,62);
            SoftStrategyTheme.Round(note,rejected?SoftStrategyTheme.PaleDanger:SoftStrategyTheme.PaleSage,8);
            GUI.Label(new Rect(note.x+12,note.y+10,note.width-24,note.height-16),
                activeSample>=0?"请点击照片中平整、光照均匀的"+SampleNames[activeSample]+"区域。":message,theme.Small);
        }

        private void DrawCalibration(Rect area)
        {
            theme.Card(area);
            GUI.Label(new Rect(area.x+16,area.y+16,160,24),"02 / 角色颜色",theme.Heading);
            GUI.enabled=!busy;
            if (theme.Button(new Rect(area.xMax-128,area.y+12,112,32),calibrating?"正在取色":"颜色设置",selected:calibrating))
            { calibrating=true; analysis.Invalidate(); }
            GUI.Label(new Rect(area.x+16,area.y+45,area.width-32,18),ReadyCalibration()?"颜色已就绪，角色由你指定":"请采样背景与三种角色颜色",theme.Small);
            for (int i=0;i<4;i++)
            {
                GUI.enabled=!busy && calibrating && frame!=null && webcam==null;
                string sampleName=SampleNames[i];
                if (theme.Button(new Rect(area.x+16,area.y+74+i*38,area.width-86,30),
                    (activeSample==i?"请选取：":"采样 ")+sampleName,selected:activeSample==i)) activeSample=i;
                Rect swatch=new Rect(area.xMax-54,area.y+74+i*38,38,30);
                SoftStrategyTheme.Round(swatch,SoftStrategyTheme.Background,6);
                if (sampled[i]) SoftStrategyTheme.Solid(new Rect(swatch.x+3,swatch.y+3,swatch.width-6,swatch.height-6),samples[i]);
                else GUI.Label(swatch,"?",theme.Centered);
            }
            GUI.enabled=!busy && calibrating && ReadyCalibration();
            float half=(area.width-40)/2;
            if (theme.Button(new Rect(area.x+16,area.y+236,half,34),"保存颜色",true))
            { profiles.SaveCalibration(samples); calibrating=false; activeSample=-1; message="这组积木的颜色已保存，请分析照片。"; }
            GUI.enabled=!busy;
            if (theme.Button(new Rect(area.x+24+half,area.y+236,half,34),"重置颜色"))
            { Array.Clear(sampled,0,4); profiles.ResetCalibration(); analysis.Invalidate(); calibrating=true; activeSample=-1; }
            GUI.enabled=true;
            GUI.Label(new Rect(area.x+16,area.y+284,area.width-32,38),
                "请先采样干净背景，再采样积木顶面，避开阴影。颜色保存后可继续使用。",theme.Small);
        }

        private void DrawResult(Rect area)
        {
            theme.Card(area);
            GUI.Label(new Rect(area.x+16,area.y+14,area.width-32,26),"03 / 核对地图",theme.Heading);
            bool success=scan?.Status==ResultStatus.Success;
            theme.Badge(new Rect(area.x+16,area.y+44,144,22),busy?"正在分析……":scan==null?"等待分析":success?"请核对后使用":"需要调整",scan==null || success);
            float footerY=area.yMax-50;
            if (scan?.PhysicalCells.Length==100)
            {
                float side=scan.Issues.Length>0?Mathf.Min(112,Mathf.Max(80,area.height-204)):
                    Mathf.Min(144,Mathf.Max(80,area.height-158));
                DrawMap(new Rect(area.x+16,area.y+78,side,side));
                float textX=area.x+side+30,textWidth=area.width-side-46;
                GUI.Label(new Rect(textX,area.y+80,textWidth,90),$"{scan.Bricks.Length} 块积木\n\nK  核心\nW  城墙\nT  防御塔",theme.Small);
                if (scan.Issues.Length==0) GUI.Label(new Rect(textX,area.y+174,textWidth,40),"相对网格\n按积木尺寸定位",theme.Small);
                if (scan.Issues.Length>0)
                {
                    float issueY=area.y+Mathf.Max(174,side+92);
                    DrawIssues(new Rect(area.x+16,issueY,area.width-32,footerY-issueY-8));
                }
                else if (area.height>=314) GUI.Label(new Rect(area.x+16,area.y+side+90,area.width-32,footerY-area.y-side-100),
                    "进入地图校对后，可修改积木并确认实物真值。",theme.Small);
            }
            else if (scan?.Issues.Length>0)
                DrawIssues(new Rect(area.x+16,area.y+78,area.width-32,footerY-area.y-90));
            else GUI.Label(new Rect(area.x+16,area.y+78,area.width-32,footerY-area.y-90),
                "分析后将在这里显示 10×10 地图。\n\n先完成颜色标定，再分析并逐块核对。",theme.Centered);
            bool hasCore=scan!=null && Array.Exists(scan.Bricks,b=>b.ColorId=="physical-color-0");
            if (success && !hasCore) GUI.Label(new Rect(area.x+16,footerY-30,area.width-32,24),"未检测到核心，可在校对或设计时补加。",theme.Small);
            GUI.enabled=!busy && frame!=null && webcam==null && ReadyCalibration() && !calibrating;
            if (theme.Button(new Rect(area.x+16,footerY,118,34),busy?"正在分析……":"分析照片")) Analyze();
            GUI.enabled=!busy && scan!=null && !calibrating && webcam==null && !levels.IsLocked;
            if (theme.Button(new Rect(area.x+144,footerY,area.width-160,34),"进入地图校对",true)) OpenReview();
            GUI.enabled=true;
        }

        private void DrawIssues(Rect area)
        {
            issueScroll=GUI.BeginScrollView(area,issueScroll,new Rect(0,0,area.width-20,scan.Issues.Length*72));
            for (int i=0;i<scan.Issues.Length;i++)
            {
                GUI.Label(new Rect(0,i*72,area.width-26,18),$"问题 {i+1}",theme.Eyebrow);
                GUI.Label(new Rect(0,i*72+22,area.width-26,48),ChineseUi.Error(scan.Issues[i]),theme.Small);
            }
            GUI.EndScrollView();
        }

        private void DrawMap(Rect area)
        {
            float step=area.width/10;
            string[] ids=Calibration().ColorIds;
            foreach (PhysicalCellObservation cell in scan.PhysicalCells)
            {
                string symbol="."; Color color=new Color32(230,233,220,255);
                if (cell.State==PhysicalCellState.Uncertain) { symbol="?"; color=SoftStrategyTheme.Danger; }
                else if (cell.State==PhysicalCellState.Occupied)
                {
                    var brick=Array.Find(scan.Bricks,b=>b.BrickId==cell.BrickId);
                    int index=brick==null?-1:Array.IndexOf(ids,brick.ColorId);
                    symbol=index==0?"K":index==1?"W":index==2?"T":"?";
                    color=index>=0?samples[index+1]:SoftStrategyTheme.Danger;
                }
                var tile=new Rect(area.x+cell.C*step,area.y+cell.R*step,step-1,step-1);
                SoftStrategyTheme.Solid(tile,color);
                GUI.Label(tile,symbol,0.2126f*color.r+0.7152f*color.g+0.0722f*color.b>0.45f?theme.DarkCell:theme.LightCell);
            }
        }
        private Color32 SamplePatch(int x,int y)
        { Rgb24 color=analysis.SampleColor(x,y); return new Color32(color.R,color.G,color.B,255); }
        private void DrawEvidence(Rect shown)
        {
            if (scan==null) return;
            foreach (BrickObservation brick in scan.Bricks)
            {
                if (brick.Evidence.Length==0) continue;
                ImagePoint[] region=brick.Evidence[0].Region; if (region.Length!=4) continue;
                int index=Array.IndexOf(Calibration().ColorIds,brick.ColorId);
                DrawRegion(shown,region,index>=0?samples[index+1]:Color.yellow);
            }
            foreach (Issue issue in scan.Issues)
                if (issue.FrameId==frame.FrameId && issue.ImageRegion.Length==4) DrawRegion(shown,issue.ImageRegion,Color.red);
        }
        private void DrawRegion(Rect shown,ImagePoint[] region,Color color)
        {
            Color previous=GUI.color; GUI.color=color;
            float left=shown.x+(float)region[0].X/frame.Width*shown.width,top=shown.y+(float)region[0].Y/frame.Height*shown.height;
            float right=shown.x+(float)region[2].X/frame.Width*shown.width,bottom=shown.y+(float)region[2].Y/frame.Height*shown.height;
            GUI.DrawTexture(new Rect(left,top,right-left,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left,bottom-2,right-left,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left,top,2,bottom-top),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(right-2,top,2,bottom-top),Texture2D.whiteTexture);
            GUI.color=previous;
        }
        private void DrawCamera(Rect area)
        {
            int angle = webcam.videoRotationAngle;
            bool swapped = angle == 90 || angle == 270;
            Rect shown = Fit(area,swapped ? webcam.height : webcam.width,swapped ? webcam.width : webcam.height);
            Rect raw = swapped ? new Rect(shown.center.x-shown.height/2,shown.center.y-shown.width/2,shown.height,shown.width) : shown;
            Matrix4x4 old = GUI.matrix; GUIUtility.RotateAroundPivot(angle,shown.center);
            GUI.DrawTextureWithTexCoords(raw,webcam,new Rect(0,webcam.videoVerticallyMirrored ? 1 : 0,1,webcam.videoVerticallyMirrored ? -1 : 1));
            GUI.matrix = old;
        }
        private static Rect Fit(Rect area,int width,int height)
        { float factor = Mathf.Min(area.width/width,area.height/height); return new Rect(area.center.x-width*factor/2,area.center.y-height*factor/2,width*factor,height*factor); }
        private void OnDestroy()
        {
            analysis?.Invalidate();
            if (cameraCapture!=null) { cameraCapture.StatusChanged-=CameraStatus; cameraCapture.Stop(); }
            if (photo!=null) Destroy(photo);
            theme?.Dispose();
        }
    }
}
