using BrickDefense.Core;
using BrickDefense.Demo;
using BrickDefense.Workflow;
using UnityEngine;

namespace BrickDefense.Presentation
{
    // 地图主界面：按钮、鼠标和触摸输入最终都经 LevelSession 更新关卡。
    public sealed class DemoUI : MonoBehaviour
    {
        private LevelSession session;
        private LevelView view;
        private PhotoScanPanel photoPanel;
        private SoftStrategyTheme theme;
        private BattlePanel battles;
        private int orbitControl;
        private int suppressMouseUntil=-1;
        private Vector2 lastOrbitPointer;
        private BoardTouchControls touchControls;
        public void Initialize(LevelSession session,LevelView view,PhotoScanPanel photoPanel,BattlePanel battles=null)
        {
            this.session=session; this.view=view; this.photoPanel=photoPanel;
            this.battles=battles;
            touchControls=new BoardTouchControls(view.RotateView,view.ZoomView,SelectSpawn,
                p=>new BoardUiLayout(Screen.width,Screen.height).Contains(p) || battles?.ContainsPointer(p)==true);
        }

        // Legacy Input 可能把触摸再派发为鼠标事件，短暂屏蔽可避免重复选格。
        private bool SuppressMapMouse=>Input.touchCount>0 || Time.frameCount<=suppressMouseUntil;

        private void OnGUI()
        {
            if (session?.Current==null || photoPanel.IsOpen || battles?.IsConfigOpen==true) { CancelOrbit(); touchControls?.Cancel(); return; }
            int orbitId=GUIUtility.GetControlID("BrickDefense.BoardOrbit".GetHashCode(),FocusType.Passive);
            theme??=new SoftStrategyTheme();
            Matrix4x4 oldMatrix=GUI.matrix; Color oldColor=GUI.color; bool oldEnabled=GUI.enabled;
            Vector2 screenMouse=Event.current.mousePosition;
            var layout=new BoardUiLayout(Screen.width,Screen.height);
            bool narrow=layout.Narrow;
            float scale=layout.Scale,width=layout.Width,height=layout.Height;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1)); GUI.color=Color.white; GUI.enabled=true;
            try
            {
                LevelContext context=session.Current; LevelResult level=context.Result;
                bool valid=level.Status==ResultStatus.Success;
                Rect titleArea=layout.Title,statusArea=layout.Status,dock=layout.Dock,legend=layout.Legend;
                bool overUI=layout.Contains(screenMouse) || battles?.ContainsPointer(screenMouse)==true;
                if(Event.current.type==EventType.ScrollWheel && !overUI && !SuppressMapMouse && GUIUtility.hotControl==0)
                { view.ZoomView(-Event.current.delta.y/3f); Event.current.Use(); }
                HandleOrbit(Event.current,orbitId,screenMouse,overUI);
                view.DrawSpawnLabels(scale);
                theme.Card(titleArea);
                GUI.Label(new Rect(titleArea.x+18,32,240,14),"用积木搭建你的小小世界",theme.Eyebrow);
                GUI.Label(new Rect(titleArea.x+18,50,240,30),"积木塔防",theme.Title);
                theme.Card(statusArea);
                bool selectionError=session.SpawnSelectionIssue!=null;
                theme.Badge(new Rect(statusArea.x+14,30,134,22),selectionError?"未添加出生点":valid?"路线已就绪":"请选择出生点",valid && !selectionError);
                GUI.Label(new Rect(statusArea.x+14,59,236,21),selectionError?
                    SelectionHint(session.SpawnSelectionIssue.Code):valid?
                    $"{level.SpawnRoutes.Count} 个出生点 / {level.Entities.Length} 块积木":
                    level.SpawnRoutes.Count==0?"点选空格添加出生点。":
                    "取消红色出生点，或重新摆放开路。",theme.Small);
                theme.Card(legend);
                if (narrow)
                {
                    LegendItem(legend.x+18,legend.y+13,"核心",SoftStrategyTheme.Core);
                    LegendItem(legend.x+152,legend.y+13,"城墙",SoftStrategyTheme.Wall);
                    LegendItem(legend.x+286,legend.y+13,"防御塔",SoftStrategyTheme.Tower);
                    LegendItem(legend.x+422,legend.y+13,"路线",SoftStrategyTheme.Route);
                }
                else
                {
                    GUI.Label(new Rect(legend.x+16,legend.y+14,120,18),"积木角色",theme.Eyebrow);
                    LegendItem(legend.x+16,legend.y+41,"核心",SoftStrategyTheme.Core);
                    LegendItem(legend.x+16,legend.y+67,"城墙",SoftStrategyTheme.Wall);
                    LegendItem(legend.x+16,legend.y+93,"防御塔",SoftStrategyTheme.Tower);
                    LegendItem(legend.x+16,legend.y+119,"路线",SoftStrategyTheme.Route);
                }
                if(battles?.HasBattle!=true)
                {
                    theme.Card(dock);
                    float slot=(dock.width-44)/3;
                    if (theme.Button(new Rect(dock.x+14,dock.y+14,slot,42),"示例布局",
                        selected:context.Definition.Source==LayoutSource.Reference)) session.LoadExample(false);
                    if (theme.Button(new Rect(dock.x+22+slot,dock.y+14,slot,42),"封闭核心",
                        selected:context.Definition.Source==LayoutSource.Blocked)) session.LoadExample(true);
                    if (theme.Button(new Rect(dock.x+30+slot*2,dock.y+14,slot,42),"扫描积木",true)) photoPanel.Open();
                }
                if (!narrow) GUI.Label(new Rect((width-700)/2,height-19,700,17),battles?.HasBattle==true?
                    "点选防御塔查看射程，可转动和缩放视角观察战斗。":Application.isMobilePlatform?
                    "轻点空格添加出生点，再点一次取消；单指拖动转动视角，双指缩放。":
                    "点击空格添加或取消出生点；右键拖动转动视角，滚轮缩放。",theme.Small);
                Event current=Event.current;
                if (current.type==EventType.MouseDown && current.button==0 && GUIUtility.hotControl==0 && !overUI && !SuppressMapMouse)
                {
                    SelectSpawn(screenMouse); current.Use();
                }
            }
            finally { GUI.matrix=oldMatrix; GUI.color=oldColor; GUI.enabled=oldEnabled; }
        }

        // 右键拖动只在地图区域取得 IMGUI 控制权；离开窗口时主动释放。
        private void HandleOrbit(Event current,int id,Vector2 screenMouse,bool overUI)
        {
            if(SuppressMapMouse)
            {
                if(current.isMouse && current.button!=0) current.Use();
                return;
            }
            if (current.type==EventType.MouseDown && current.button==1 && !overUI && GUIUtility.hotControl==0)
            { orbitControl=id; GUIUtility.hotControl=id; lastOrbitPointer=screenMouse; current.Use(); }
            else if (orbitControl!=0 && GUIUtility.hotControl==orbitControl)
            {
                if (current.type==EventType.MouseDrag &&
                    (screenMouse.x<0 || screenMouse.y<0 || screenMouse.x>=Screen.width || screenMouse.y>=Screen.height))
                { CancelOrbit(); current.Use(); }
                else if (current.type==EventType.MouseDrag && current.button==1)
                { view.RotateView(screenMouse-lastOrbitPointer); lastOrbitPointer=screenMouse; current.Use(); }
                else if (current.rawType==EventType.MouseUp && current.button==1)
                { CancelOrbit(); current.Use(); }
                else if (current.type==EventType.MouseLeaveWindow || current.type==EventType.Ignore) CancelOrbit();
            }
            else if (overUI && current.button==1 &&
                (current.type==EventType.MouseDown || current.type==EventType.MouseDrag || current.type==EventType.MouseUp)) current.Use();
        }

        private void CancelOrbit()
        { if (orbitControl!=0 && GUIUtility.hotControl==orbitControl) GUIUtility.hotControl=0; orbitControl=0; }
        private void Update()
        {
            int count=Input.touchCount;
            HandleTouches(count,count>0?Input.GetTouch(0):default,count>1?Input.GetTouch(1):default);
            if (orbitControl==0) return;
            Vector3 pointer=Input.mousePosition;
            // A release outside the Game view may never arrive as an IMGUI MouseUp.
            if (!Input.GetMouseButton(1) || pointer.x<0 || pointer.y<0 || pointer.x>=Screen.width || pointer.y>=Screen.height)
                CancelOrbit();
        }
        private void HandleTouches(int count,Touch first,Touch second)
        {
            if(count>0) { suppressMouseUntil=Time.frameCount+1; CancelOrbit(); }
            if(session?.Current==null || photoPanel.IsOpen || battles?.IsConfigOpen==true)
            {
                touchControls?.Cancel();
                if(count==0) touchControls?.Process(0,default,default,new Vector2(Screen.width,Screen.height));
                return;
            }
            touchControls.Process(count,first,second,new Vector2(Screen.width,Screen.height));
        }
        private void SelectSpawn(Vector2 screenPosition)
        { if(view.TryPickCell(screenPosition,out GridPoint cell) && battles?.HandleMapCell(cell)!=true) session.ToggleSpawn(cell); }
        private static string SelectionHint(string code) => code=="NO_ROUTE"?"该格没有通往核心的路线。":
            code=="SPAWN_OCCUPIED"?"请选择空格添加出生点。":"请选择棋盘内的格子。";
        private void OnApplicationFocus(bool focused) { if (!focused) { CancelOrbit(); touchControls?.Cancel(); } }
        private void OnApplicationPause(bool paused) { if(paused) { CancelOrbit(); touchControls?.Cancel(); } }
        private void OnDisable() { CancelOrbit(); touchControls?.Cancel(); }

        private void LegendItem(float x,float y,string name,Color color)
        {
            SoftStrategyTheme.Round(new Rect(x,y+2,14,14),color,4);
            GUI.Label(new Rect(x+24,y,94,20),name,theme.Body);
        }
    }
}
