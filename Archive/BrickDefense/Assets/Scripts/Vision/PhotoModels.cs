using System;
using System.Collections.Generic;
using BrickDefense.Core;

namespace BrickDefense.Core
{
    // Rows start at the top left. Pixels remain the normalized original, never the rectified image.
    public sealed class ImageFrame
    {
        public string FrameId { get; }
        public int Width { get; }
        public int Height { get; }
        public Rgb24[] Pixels { get; }
        public DateTime CapturedAtUtc { get; }
        public ImagePoint[] Viewfinder { get; }
        public string Orientation => "top-left-rgb";

        public ImageFrame(string frameId, int width, int height, Rgb24[] pixels,
            DateTime capturedAtUtc, ImagePoint[] viewfinder = null)
        {
            FrameId = frameId; Width = width; Height = height; Pixels = pixels;
            CapturedAtUtc = capturedAtUtc;
            Viewfinder = viewfinder ?? new[] { new ImagePoint(0, 0), new ImagePoint(width - 1, 0),
                new ImagePoint(width - 1, height - 1), new ImagePoint(0, height - 1) };
        }

        public Rgb24 Sample(ImagePoint point)
        {
            int x = (int)Math.Round(point.X), y = (int)Math.Round(point.Y);
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(point));
            return Pixels[y * Width + x];
        }
    }

    // 当前基础版只分析一帧；这里保留帧集合合同以便后续扩展多视角输入。
    public sealed class CaptureSet
    {
        public string ScanId { get; }
        public ImageFrame[] Frames { get; }
        public string PrimaryFrameId { get; }
        public CaptureSet(string scanId, ImageFrame frame)
        { ScanId = scanId; Frames = new[] { frame }; PrimaryFrameId = frame?.FrameId; }
    }

}

namespace BrickDefense.Vision
{
    // 颜色编号与角色的对应由用户采样顺序决定，不由 RGB 值硬编码。
    public sealed class ColorCalibration
    {
        public Rgb24 Background { get; }
        public Rgb24[] BrickColors { get; }
        public string[] ColorIds { get; }
        public ColorCalibration(Rgb24 background, Rgb24[] colors, string[] colorIds)
        { Background = background; BrickColors = colors; ColorIds = colorIds; }

        // 取 7×7 邻域各通道的中位数，降低单个反光或噪声像素的影响。
        public static Rgb24 SamplePatch(ImageFrame frame,int x,int y)
        {
            int count=0; var r=new byte[49]; var g=new byte[49]; var b=new byte[49];
            for (int dy=-3; dy<=3; dy++)
                for (int dx=-3; dx<=3; dx++)
                {
                    int px=Math.Max(0,Math.Min(frame.Width-1,x+dx)),py=Math.Max(0,Math.Min(frame.Height-1,y+dy));
                    Rgb24 p=frame.Pixels[py*frame.Width+px]; r[count]=p.R; g[count]=p.G; b[count++]=p.B;
                }
            Array.Sort(r); Array.Sort(g); Array.Sort(b); return new Rgb24(r[24],g[24],b[24]);
        }

        public LevelRules CreateRules()
        {
            return new LevelRules(new Dictionary<string, BrickRole>
            { { ColorIds[0], BrickRole.Core }, { ColorIds[1], BrickRole.Wall },
                { ColorIds[2], BrickRole.Tower } });
        }
    }

    // Development values. They must be calibrated on real photographs before release acceptance.
    public sealed class PhotoScanSettings
    {
        public int MaximumDetectionSide { get; set; } = 1200;
        public double MaximumColorDistance { get; set; } = 0.24;
        public double MinimumColorMargin { get; set; } = 0.08;
        public double MinimumCalibrationSeparation { get; set; } = 0.32;
        public double MinimumCellPixels { get; set; } = 18;
        public double MinimumColorBrightness { get; set; } = 0.12;
    }
}
