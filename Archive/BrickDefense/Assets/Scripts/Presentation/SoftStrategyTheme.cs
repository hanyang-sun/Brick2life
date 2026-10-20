using UnityEngine;

namespace BrickDefense.Presentation
{
    // Presentation-only palette. Physical photo colors never pass through this theme.
    internal sealed class SoftStrategyTheme
    {
        public static readonly Color Background=new Color32(239,237,224,255);
        public static readonly Color Paper=new Color32(252,251,243,255);
        public static readonly Color Ink=new Color32(48,65,56,255);
        public static readonly Color Muted=new Color32(109,122,109,255);
        public static readonly Color Sage=new Color32(67,111,89,255);
        public static readonly Color PaleSage=new Color32(221,230,209,255);
        public static readonly Color Core=new Color32(111,171,167,255);
        public static readonly Color Wall=new Color32(188,179,151,255);
        public static readonly Color Tower=new Color32(219,174,100,255);
        public static readonly Color Route=new Color32(71,115,91,255);
        public static readonly Color Danger=new Color32(171,74,58,255);
        public static readonly Color PaleDanger=new Color32(247,229,218,255);

        public GUIStyle Title { get; }=Text(24,Ink,true);
        public GUIStyle Heading { get; }=Text(17,Ink,true);
        public GUIStyle Body { get; }=Text(14,Ink,false,true);
        public GUIStyle Small { get; }=Text(12,Muted,false,true);
        public GUIStyle Eyebrow { get; }=Text(10,Muted,true);
        public GUIStyle Centered { get; }=Text(14,Muted,false,true,TextAnchor.MiddleCenter);
        public GUIStyle DarkCell { get; }=Text(11,Ink,true,false,TextAnchor.MiddleCenter);
        public GUIStyle LightCell { get; }=Text(11,Color.white,true,false,TextAnchor.MiddleCenter);
        public GUIStyle Field { get; }
        private readonly GUIStyle button=Text(14,Ink,true,false,TextAnchor.MiddleCenter);
        private readonly GUIStyle primary=Text(14,Color.white,true,false,TextAnchor.MiddleCenter);
        private readonly GUIStyle disabled=Text(14,Muted,true,false,TextAnchor.MiddleCenter);
        private readonly GUIStyle validBadge=Text(12,Sage,false,false,TextAnchor.MiddleCenter);
        private readonly GUIStyle errorBadge=Text(12,Danger,false,false,TextAnchor.MiddleCenter);
        private GUISkin scrollSkin;
        private Texture2D scrollTrack,scrollThumb;
        private static Font interfaceFont;
        public static Font InterfaceFont => interfaceFont!=null?interfaceFont:
            (interfaceFont=Resources.Load<Font>("Fonts/NotoSansSC-Regular"));

        // Construct during OnGUI to inherit control metrics; use the bundled Chinese font.
        public SoftStrategyTheme()
        {
            Field=new GUIStyle(GUI.skin.textField) { font=InterfaceFont,fontSize=14,padding=new RectOffset(10,10,8,8) };
            Field.normal.textColor=Ink; Field.focused.textColor=Ink;
            Field.normal.background=Texture2D.whiteTexture; Field.focused.background=Texture2D.whiteTexture;
        }

        public GUISkin ScrollSkin
        {
            get
            {
                if (scrollSkin!=null) return scrollSkin;
                scrollSkin=Object.Instantiate(GUI.skin); scrollSkin.hideFlags=HideFlags.HideAndDontSave;
                scrollSkin.font=InterfaceFont;
                scrollTrack=FlatTexture(new Color32(225,230,215,255)); scrollThumb=FlatTexture(new Color32(135,156,134,255));
                ScrollStyle(scrollSkin.verticalScrollbar,scrollTrack); scrollSkin.verticalScrollbar.fixedWidth=10;
                ScrollStyle(scrollSkin.verticalScrollbarThumb,scrollThumb); scrollSkin.verticalScrollbarThumb.fixedWidth=10;
                scrollSkin.verticalScrollbarUpButton=new GUIStyle(); scrollSkin.verticalScrollbarDownButton=new GUIStyle();
                ScrollStyle(scrollSkin.horizontalScrollbar,scrollTrack); scrollSkin.horizontalScrollbar.fixedHeight=10;
                ScrollStyle(scrollSkin.horizontalScrollbarThumb,scrollThumb); scrollSkin.horizontalScrollbarThumb.fixedHeight=10;
                scrollSkin.horizontalScrollbarLeftButton=new GUIStyle(); scrollSkin.horizontalScrollbarRightButton=new GUIStyle();
                return scrollSkin;
            }
        }

        private static Texture2D FlatTexture(Color color)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp };
            texture.SetPixels(new[] {color,color,color,color}); texture.Apply(false,true); return texture;
        }

        private static void ScrollStyle(GUIStyle style,Texture2D texture)
        {
            style.border=new RectOffset(); style.margin=new RectOffset(); style.padding=new RectOffset();
            foreach (GUIStyleState state in new[] {style.normal,style.hover,style.active,style.focused}) state.background=texture;
        }

        // 滚动条皮肤与纹理在运行时创建，面板销毁时需释放。
        public void Dispose()
        {
            if (scrollSkin!=null) Object.Destroy(scrollSkin);
            if (scrollTrack!=null) Object.Destroy(scrollTrack);
            if (scrollThumb!=null) Object.Destroy(scrollThumb);
        }

        private static GUIStyle Text(int size,Color color,bool bold=false,bool wrap=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            var style=new GUIStyle(GUI.skin.label) { font=InterfaceFont,fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,
                wordWrap=wrap,alignment=align,padding=new RectOffset(0,0,0,0),richText=false };
            foreach (GUIStyleState state in new[] {style.normal,style.hover,style.active,style.focused,
                style.onNormal,style.onHover,style.onActive,style.onFocused})
            { state.background=null; state.textColor=color; }
            return style;
        }

        public void Card(Rect area)
        {
            Round(new Rect(area.x,area.y+3,area.width,area.height),new Color(0.19f,0.25f,0.20f,0.09f),14);
            Round(area,Paper,14);
        }

        public bool Button(Rect area,string text,bool emphasized=false,bool selected=false)
        {
            bool enabled=GUI.enabled,hover=area.Contains(Event.current.mousePosition);
            Color fill=!enabled?new Color32(228,232,219,255):
                emphasized||selected?Sage:new Color32(234,237,224,255);
            if (enabled && hover) fill=Color.Lerp(fill,emphasized||selected?Ink:Color.white,0.16f);
            if (enabled && hover && GUIUtility.hotControl!=0) fill=Color.Lerp(fill,Ink,0.12f);
            Round(area,fill,8);
            return GUI.Button(area,text,!enabled?disabled:emphasized||selected?primary:button);
        }

        public void Badge(Rect area,string text,bool valid=true)
        {
            Round(area,valid?PaleSage:PaleDanger,area.height/2);
            GUI.Label(area,text,valid?validBadge:errorBadge);
        }

        public static void Round(Rect area,Color color,float radius=10)
        {
            Color previous=GUI.color; GUI.color=Color.white;
            GUI.DrawTexture(area,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
            GUI.color=previous;
        }

        public static void Solid(Rect area,Color color)
        {
            Color previous=GUI.color; GUI.color=color;
            GUI.DrawTexture(area,Texture2D.whiteTexture,ScaleMode.StretchToFill,false);
            GUI.color=previous;
        }
    }
}
