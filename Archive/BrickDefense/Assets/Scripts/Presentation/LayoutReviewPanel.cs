using System;
using System.Collections.Generic;
using BrickDefense.Core;
using BrickDefense.Infrastructure;
using BrickDefense.Workflow;
using UnityEngine;

namespace BrickDefense.Presentation
{
    // 单一草稿编辑入口。二维网格处理点选编辑，三维预览复用现有视图。
    public sealed class LayoutReviewPanel : MonoBehaviour
    {
        private LevelSession levels;
        private LevelView view;
        private Texture2D photo;
        private Color32[] colors;
        private Action published;
        private LayoutReviewStore store;
        private SoftStrategyTheme theme;
        private Vector2 pageScroll,controlScroll,listScroll;
        private string selectedId,message="对照照片逐块校对；移动同一块积木请使用选择/移动。";
        private int tool;
        private BrickRole paintRole=BrickRole.Wall;
        private bool verifiedByPlayer,preview3d;
        private float uiScale;
        private Rect gridArea;
        private Rect actionArea,verificationArea;
        private BoardTouchControls touchControls;
        private bool rotating;
        private Vector2 lastPointer;
        public bool IsOpen { get; private set; }
        public LayoutEditSession Session { get; private set; }

        public void Initialize(LevelSession owner,LevelView levelView)
        {
            levels=owner; view=levelView; store=new LayoutReviewStore(Application.persistentDataPath);
            touchControls=new BoardTouchControls(view.RotateView,view.ZoomView,p=>{},ContainsPointer);
        }
        public void Open(LayoutEditSession session,Texture2D sourcePhoto,Color32[] palette,Action onPublished)
        {
            if(levels.IsLocked) return;
            if(Session!=null) Session.Changed-=DraftChanged;
            Session=session; Session.Changed+=DraftChanged;
            photo=sourcePhoto; colors=(Color32[])palette.Clone(); published=onPublished;
            selectedId=null; verifiedByPlayer=false; preview3d=false; rotating=false;
            pageScroll=controlScroll=listScroll=Vector2.zero; IsOpen=true;
            message=session.OriginalStatus==ResultStatus.Rejected?
                "识别有待修正项目，已保留可用积木；请对照照片补加、删除或移动。":
                "请核对数量、颜色和位置。补加表示漏检，删除表示误检。";
        }
        public void Close()
        {
            IsOpen=false; rotating=false; touchControls?.Cancel();
            if(levels?.Current!=null) view.Display(levels.Current.Result);
        }
        private void DraftChanged()
        {
            if(!IsOpen) return;
            verifiedByPlayer=false;
            if(preview3d) view.DisplayDraft(Session.Bricks,Session.RoleFor);
        }
        private void TogglePreview()
        {
            preview3d=!preview3d; rotating=false; touchControls.Cancel();
            if(preview3d) view.DisplayDraft(Session.Bricks,Session.RoleFor);
            else if(levels.Current!=null) view.Display(levels.Current.Result);
        }

        private void OnGUI()
        {
            if(!IsOpen || Session==null) return;
            theme??=new SoftStrategyTheme();
            Matrix4x4 matrix=GUI.matrix; Color color=GUI.color; bool enabled=GUI.enabled;
            int depth=GUI.depth; GUISkin skin=GUI.skin;
            uiScale=Mathf.Min(1,Screen.width/540f); float width=Screen.width/uiScale,height=Screen.height/uiScale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1)); GUI.color=Color.white; GUI.enabled=true;
            GUI.depth=-25; GUI.skin=theme.ScrollSkin;
            try
            {
                if(!preview3d) SoftStrategyTheme.Solid(new Rect(0,0,width,height),SoftStrategyTheme.Background);
                var header=new Rect(22,16,width-44,86); theme.Card(header);
                GUI.Label(new Rect(40,28,width-268,18),Session.Stage==LayoutEditStage.PhysicalReview?"01 / 实物校对":"02 / 游戏布局设计",theme.Eyebrow);
                GUI.Label(new Rect(40,51,width-268,30),Session.Stage==LayoutEditStage.PhysicalReview?"对照照片，校对地图":"调整你的塔防关卡",theme.Heading);
                if(theme.Button(new Rect(width-222,38,182,40),preview3d?"返回编辑网格":"三维预览")) TogglePreview();
                float bodyHeight=Mathf.Max(80,height-232);
                if(preview3d)
                {
                    GUI.Label(new Rect(40,114,width-80,40),"预览尚未要求核心或道路。右键拖动 / 单指拖动旋转，滚轮 / 双指缩放。",theme.Small);
                    HandlePreviewInput();
                }
                else if(width>=1000)
                {
                    DrawPhoto(new Rect(22,118,232,bodyHeight));
                    float side=Mathf.Min(width-598,bodyHeight-54);
                    DrawGrid(new Rect(272,118,width-598,bodyHeight),side);
                    Rect sidebar=new Rect(width-308,118,286,bodyHeight);
                    controlScroll=GUI.BeginScrollView(sidebar,controlScroll,new Rect(0,0,266,660));
                    DrawControls(new Rect(0,0,266,660)); GUI.EndScrollView();
                }
                else
                {
                    float contentWidth=width-64,side=Mathf.Min(500,contentWidth-32),gridHeight=side+70;
                    pageScroll=GUI.BeginScrollView(new Rect(22,118,width-44,bodyHeight),pageScroll,new Rect(0,0,contentWidth,420+gridHeight+692));
                    DrawPhoto(new Rect(0,0,contentWidth,404));
                    DrawGrid(new Rect(0,420,contentWidth,gridHeight),side);
                    DrawControls(new Rect(0,436+gridHeight,contentWidth,660)); GUI.EndScrollView();
                }
                DrawFooter(new Rect(22,height-110,width-44,94));
            }
            finally { GUI.matrix=matrix; GUI.color=color; GUI.enabled=enabled; GUI.depth=depth; GUI.skin=skin; }
        }
        private void DrawPhoto(Rect area)
        {
            theme.Card(area);
            GUI.Label(new Rect(area.x+16,area.y+14,area.width-32,24),"原照片 / 人工核验依据",theme.Heading);
            var picture=new Rect(area.x+14,area.y+50,area.width-28,area.height-96);
            if(photo!=null) GUI.DrawTexture(picture,photo,ScaleMode.ScaleToFit,false);
            GUI.Label(new Rect(area.x+16,area.yMax-36,area.width-32,26),"方向固定：向右为列，向下为行",theme.Small);
        }
        private void DrawGrid(Rect area,float side)
        {
            theme.Card(area);
            GUI.Label(new Rect(area.x+16,area.y+14,area.width-32,25),$"10×10 草稿 / {Session.Bricks.Count} 块积木",theme.Heading);
            gridArea=new Rect(area.center.x-side/2,area.y+48,side,side);
            var bricks=Session.Bricks; float step=side/10;
            for(int r=0;r<10;r++) for(int c=0;c<10;c++)
            {
                BrickObservation brick=null; int count=0;
                foreach(var b in bricks) if(b.C==c && b.R==r) { if(brick==null || b.BrickId==selectedId) brick=b; count++; }
                var tile=new Rect(gridArea.x+c*step,gridArea.y+r*step,step-2,step-2);
                bool selected=brick?.BrickId==selectedId && brick!=null;
                Color fill=brick==null?SoftStrategyTheme.PaleSage:colors[(int)Session.RoleFor(brick.ColorId)+1];
                if(count>1) fill=SoftStrategyTheme.PaleDanger;
                SoftStrategyTheme.Round(tile,selected?SoftStrategyTheme.Sage:fill,4);
                if(selected) SoftStrategyTheme.Round(new Rect(tile.x+3,tile.y+3,tile.width-6,tile.height-6),fill,3);
                string label=brick==null?"":count>1?"!":RoleName(Session.RoleFor(brick.ColorId));
                if(!string.IsNullOrEmpty(label)) GUI.Label(tile,label,fill.grayscale>0.55?theme.DarkCell:theme.LightCell);
                if(GUI.Button(tile,GUIContent.none,GUIStyle.none)) EditCell(c,r,brick);
            }
        }
        private void EditCell(int c,int r,BrickObservation brick)
        {
            bool changed=false;
            if(tool==2) { if(brick!=null) changed=Session.Delete(brick.BrickId); }
            else if(tool==1) changed=brick==null?Session.Add(c,r,paintRole):Session.ChangeRole(brick.BrickId,paintRole);
            else if(brick!=null) { selectedId=brick.BrickId; message="已选中积木，点击空格移动；也可选择角色后点“改为该角色”。"; }
            else if(selectedId!=null) changed=Session.Move(selectedId,c,r);
            if(Session.LastIssue!=null) message=Session.LastIssue.Message;
            else if(changed) message="草稿已更新；可以撤销，确认前请继续对照照片。";
        }
        private void DrawControls(Rect area)
        {
            theme.Card(area); float x=area.x+16,w=area.width-32,y=area.y+16;
            GUI.Label(new Rect(x,y,w,24),Session.Stage==LayoutEditStage.PhysicalReview?"校对工具":"设计工具",theme.Heading); y+=34;
            GUI.Label(new Rect(x,y,w,52),Session.Stage==LayoutEditStage.PhysicalReview?
                "移动保留积木身份；补加记为漏检，删除记为误检。请按实物修正。":
                "实物真值已冻结。这里的增删、改色和移动仅改变游戏布局。",theme.Small); y+=62;
            string[] tools={"选择/移动","添加/改色","删除"}; float slot=(w-12)/3;
            for(int i=0;i<3;i++) if(theme.Button(new Rect(x+i*(slot+6),y,slot,34),tools[i],selected:tool==i)) tool=i;
            y+=46; slot=(w-12)/3;
            for(int i=0;i<3;i++) if(theme.Button(new Rect(x+i*(slot+6),y,slot,34),RoleName((BrickRole)i),selected:paintRole==(BrickRole)i)) paintRole=(BrickRole)i;
            y+=46;
            GUI.enabled=selectedId!=null;
            if(theme.Button(new Rect(x,y,w,32),"将选中积木改为："+RoleName(paintRole)))
            { Session.ChangeRole(selectedId,paintRole); message=Session.LastIssue?.Message??"已修改选中积木的颜色/角色。"; }
            GUI.enabled=true; y+=42;
            GUI.Label(new Rect(x,y,w,18),"积木列表（重叠或棋盘外积木也可选）",theme.Small); y+=24;
            var bricks=Session.Bricks;
            listScroll=GUI.BeginScrollView(new Rect(x,y,w,88),listScroll,new Rect(0,0,w-20,Mathf.Max(88,bricks.Count*30)));
            for(int i=0;i<bricks.Count;i++)
            {
                var b=bricks[i];
                if(theme.Button(new Rect(0,i*30,w-24,26),$"{i+1}. {RoleName(Session.RoleFor(b.ColorId))}  ({b.C},{b.R})",selected:b.BrickId==selectedId))
                { selectedId=b.BrickId; tool=0; message="已选中列表中的积木，请点空格移动。"; }
            }
            GUI.EndScrollView(); y+=100;
            GUI.enabled=Session.CanUndo;
            if(theme.Button(new Rect(x,y,(w-8)/2,34),"撤销一步")) { Session.Undo(); selectedId=null; message="已撤销。"; }
            GUI.enabled=true;
            if(theme.Button(new Rect(x+(w+8)/2,y,(w-8)/2,34),Session.Stage==LayoutEditStage.PhysicalReview?"恢复识别结果":"恢复实物布局"))
            { Session.Reset(); selectedId=null; message="已恢复，可用撤销取回修改。"; }
            y+=46; GUI.Label(new Rect(x,y,w,20),"整体移动 / 每次一格",theme.Small); y+=26;
            string[] moves={"左","右","上","下"}; int[] dc={-1,1,0,0},dr={0,0,-1,1}; slot=(w-18)/4;
            for(int i=0;i<4;i++) if(theme.Button(new Rect(x+i*(slot+6),y,slot,34),moves[i]))
            { Session.Translate(dc[i],dr[i]); message=Session.LastIssue?.Message??"整体位置已调整，相对位置关系保持。"; }
            y+=46;
            if(Session.Stage==LayoutEditStage.PhysicalReview)
            {
                verificationArea=new Rect(x,y,w,44);
                var tick=new Rect(x,y+8,22,22);
                SoftStrategyTheme.Round(tick,verifiedByPlayer?SoftStrategyTheme.Sage:SoftStrategyTheme.PaleSage,4);
                GUI.Label(tick,verifiedByPlayer?"✓":"",theme.LightCell);
                GUI.Label(new Rect(x+32,y,w-32,44),"我已逐块对照照片确认数量、颜色和位置",theme.Small);
                if(GUI.Button(verificationArea,GUIContent.none,GUIStyle.none)) verifiedByPlayer=!verifiedByPlayer;
            }
            else
            {
                var stats=Session.Statistics;
                GUI.Label(new Rect(x,y,w,54),$"识别正确率：{stats.RecognitionAccuracy:P1}\n格位恢复正确率：{stats.PositionAccuracy:P1}",theme.Body);
                y+=64;
                GUI.Label(new Rect(x,y,w,38),$"实物 {stats.ActualCount} 块 / 漏检 {stats.Missed} / 误检 {stats.FalsePositive}",theme.Small); y+=44;
                if(theme.Button(new Rect(x,y,w,32),"返回实物校对（重新确认）"))
                { Session.ReopenPhysical(); selectedId=null; verifiedByPlayer=false; message="已恢复确认的实物布局，请重新校对。"; }
            }
        }
        private void DrawFooter(Rect area)
        {
            theme.Card(area);
            float actionWidth=Mathf.Min(246,(area.width-38)*0.48f);
            GUI.Label(new Rect(area.x+16,area.y+8,area.width-32,36),message,theme.Small);
            if(theme.Button(new Rect(area.x+16,area.y+50,138,32),"返回照片")) Close();
            GUI.enabled=Session.Stage==LayoutEditStage.GameDesign || verifiedByPlayer;
            actionArea=new Rect(area.xMax-actionWidth-16,area.y+50,actionWidth,32);
            if(theme.Button(actionArea,
                Session.Stage==LayoutEditStage.PhysicalReview?"确认实物 · 记录准确率":"生成关卡",true)) Continue();
            GUI.enabled=true;
        }
        private void Continue()
        {
            try
            {
                if(Session.Stage==LayoutEditStage.PhysicalReview)
                {
                    if(!Session.ConfirmPhysical()) { message=Session.LastIssue.Message; return; }
                    store.Save(Session.CreateRecord());
                    message="实物校对记录已保存。现在可以自由调整游戏布局，随后生成关卡。"; return;
                }
                LayoutDefinition definition=Session.CreateLayout();
                bool hasCore=false;
                foreach(var b in definition.Layout.Bricks) if(Session.RoleFor(b.ColorId)==BrickRole.Core) hasCore=true;
                if(!hasCore) { message="生成关卡需要至少一个核心，请添加或修改积木角色。"; return; }
                if(levels.IsLocked) { message="请先结束当前战斗。"; return; }
                store.Save(Session.CreateRecord());
                if(!levels.TryLoad(definition)) { message="关卡尚不能生成，请检查布局与角色。编辑内容已保留。"; return; }
                store.Save(Session.CreateRecord(true)); Close(); published?.Invoke();
            }
            catch(Exception error) { message="处理失败，编辑内容已保留："+ChineseUi.Message(error.Message); }
        }
        private bool ContainsPointer(Vector2 p)
        {
            float scale=Mathf.Min(1,Screen.width/540f),height=Screen.height/scale;
            return p.y/scale<160 || p.y/scale>height-118;
        }
        private void HandlePreviewInput()
        {
            Event e=Event.current; Vector2 pointer=e.mousePosition*uiScale;
            if(Input.touchCount>0 || ContainsPointer(pointer)) return;
            if(e.type==EventType.ScrollWheel) { view.ZoomView(-e.delta.y/3f); e.Use(); }
            if(e.type==EventType.MouseDown && e.button==1) { rotating=true; lastPointer=pointer; e.Use(); }
            else if(rotating && e.type==EventType.MouseDrag && e.button==1) { view.RotateView(pointer-lastPointer); lastPointer=pointer; e.Use(); }
            else if(e.rawType==EventType.MouseUp || e.type==EventType.Ignore || e.type==EventType.MouseLeaveWindow) rotating=false;
        }
        private void Update()
        {
            if(!IsOpen || !preview3d) { touchControls?.Cancel(); return; }
            int count=Input.touchCount;
            touchControls.Process(count,count>0?Input.GetTouch(0):default,count>1?Input.GetTouch(1):default,new Vector2(Screen.width,Screen.height));
            if(!Input.GetMouseButton(1)) rotating=false;
        }
        private static string RoleName(BrickRole role)=>role==BrickRole.Core?"核心":role==BrickRole.Wall?"城墙":"防御塔";
        private void OnDisable() { if(IsOpen) Close(); }
        private void OnDestroy() { if(Session!=null) Session.Changed-=DraftChanged; theme?.Dispose(); }
    }
}
