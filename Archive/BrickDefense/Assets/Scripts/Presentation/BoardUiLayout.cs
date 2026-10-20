using UnityEngine;

namespace BrickDefense.Presentation
{
    // Shared by drawing and raw touch hit testing; positions are in GUI coordinates.
    internal readonly struct BoardUiLayout
    {
        public readonly bool Narrow;
        public readonly float Scale,Width,Height;
        public readonly Rect Title,Status,Dock,Legend;

        public BoardUiLayout(int screenWidth,int screenHeight)
        {
            Narrow=screenWidth/(float)screenHeight<0.95f;
            Scale=Mathf.Min(1.5f,Mathf.Min(screenWidth/(Narrow?600f:900f),screenHeight/640f));
            Width=screenWidth/Scale; Height=screenHeight/Scale;
            Title=new Rect(22,20,Narrow?260:286,74);
            Status=new Rect(Width-286,20,264,74);
            float dockWidth=Narrow?Width-44:700;
            Dock=new Rect((Width-dockWidth)/2,Height-96,dockWidth,74);
            Legend=new Rect(22,Narrow?Height-156:114,Narrow?Width-44:146,Narrow?46:151);
        }

        // 命中判断与绘制共用同一缩放布局，防止在按钮上启动地图手势。
        public bool Contains(Vector2 screenPosition)
        {
            Vector2 p=screenPosition/Scale;
            return Title.Contains(p) || Status.Contains(p) || Dock.Contains(p) || Legend.Contains(p);
        }
    }
}
