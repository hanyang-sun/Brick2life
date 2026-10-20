using System;
using BrickDefense.Core;
using UnityEngine;

namespace BrickDefense.Infrastructure
{
    public static class PhotoPixels
    {
        // Unity GetPixels32 is bottom-to-top. Normalize once, including webcam metadata.
        public static ImageFrame FromUnity(string frameId,Color32[] source,int width,int height,
            int clockwiseRotation = 0,bool verticallyMirrored = false)
        {
            int rotation = ((clockwiseRotation%360)+360)%360;
            if (rotation%90 != 0 || source == null || source.Length != width*(long)height)
                throw new ArgumentException("Invalid image orientation or pixel data.");
            int outputWidth = rotation == 90 || rotation == 270 ? height : width;
            int outputHeight = rotation == 90 || rotation == 270 ? width : height;
            var output = new Rgb24[source.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sy = verticallyMirrored ? y : height-1-y;
                    Color32 p = source[sy*width+x]; int ox = x, oy = y;
                    if (rotation == 90) { ox = height-1-y; oy = x; }
                    else if (rotation == 180) { ox = width-1-x; oy = height-1-y; }
                    else if (rotation == 270) { ox = y; oy = width-1-x; }
                    // Composite PNG transparency onto white rather than treating invisible RGB as evidence.
                    output[oy*outputWidth+ox] = new Rgb24((byte)((p.r*p.a+255*(255-p.a))/255),
                        (byte)((p.g*p.a+255*(255-p.a))/255),(byte)((p.b*p.a+255*(255-p.a))/255));
                }
            return new ImageFrame(frameId,outputWidth,outputHeight,output,DateTime.UtcNow);
        }

        // 预览时转换回 Unity 的底部起始行顺序，不改变识别用的 ImageFrame。
        public static Texture2D ToTexture(ImageFrame frame)
        {
            var texture = new Texture2D(frame.Width,frame.Height,TextureFormat.RGB24,false);
            var colors = new Color32[frame.Pixels.Length];
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                { Rgb24 p = frame.Pixels[y*frame.Width+x]; colors[(frame.Height-1-y)*frame.Width+x] = new Color32(p.R,p.G,p.B,255); }
            texture.SetPixels32(colors); texture.Apply(); return texture;
        }
    }
}
