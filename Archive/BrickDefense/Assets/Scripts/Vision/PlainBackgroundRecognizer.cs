using System;
using System.Collections.Generic;
using BrickDefense.Core;
using BrickDefense.ThirdParty;

namespace BrickDefense.Vision
{
    // References and adaptations: docs/research/markerless-photo-github.md and scan-rejection-github.md.
    // Single-layer, similarly sized cubes on a distinguishable background; coordinates are relative.
    public static class PlainBackgroundRecognizer
    {
        public static bool CalibrationValid(ColorCalibration calibration, PhotoScanSettings settings)
        {
            if (calibration?.BrickColors == null || calibration.BrickColors.Length != 3 ||
                calibration.ColorIds == null || calibration.ColorIds.Length != 3) return false;
            var ids = new HashSet<string>();
            foreach (string id in calibration.ColorIds)
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) return false;
            var palette = Palette(calibration);
            for (int i = 1; i < 4; i++)
                if (NormalizedLab.From(palette[i]).Brightness < settings.MinimumColorBrightness) return false;
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                    if (Distance(palette[i],palette[j]) < settings.MinimumCalibrationSeparation) return false;
            return true;
        }

        // 无格纸入口只恢复单层积木的相对布局，不声明固定的实体格纸坐标。
        public static ScanResult Recognize(CaptureSet capture, ColorCalibration calibration, PhotoScanSettings settings = null)
        {
            settings = settings ?? new PhotoScanSettings();
            string id = capture?.ScanId;
            if (capture?.Frames == null || capture.Frames.Length != 1 || capture.Frames[0] == null || string.IsNullOrWhiteSpace(id))
                return Reject(id,"CAPTURE_INVALID","需要一张近似俯拍照片和扫描编号。");
            ImageFrame frame = capture.Frames[0];
            if (string.IsNullOrWhiteSpace(frame.FrameId) || frame.FrameId != capture.PrimaryFrameId ||
                frame.Width < 100 || frame.Height < 100 || frame.Width > 4096 || frame.Height > 4096 ||
                frame.Pixels == null || frame.Pixels.Length != (long)frame.Width * frame.Height)
                return Reject(id,"CAPTURE_INVALID","照片尺寸或像素无效。");
            if (!SettingsValid(settings)) return Reject(id,"SCAN_SETTINGS_INVALID","识别配置无效。");
            if (!CalibrationValid(calibration,settings))
                return Reject(id,"CALIBRATION_REQUIRED","请采样背景和三种积木颜色；背景与积木必须可区分。");

            int factor = Math.Max(1,(int)Math.Ceiling((double)Math.Max(frame.Width,frame.Height)/settings.MaximumDetectionSide));
            int width = (frame.Width + factor - 1)/factor, height = (frame.Height + factor - 1)/factor;
            var labels = new byte[width*height];
            Rgb24[] palette = Palette(calibration);
            double neutralForegroundFloor = NeutralForegroundFloor(palette,settings);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    labels[y*width+x] = Classify(frame.Pixels[y*factor*frame.Width+x*factor],palette,settings,neutralForegroundFloor);
            labels = Denoise(labels,width,height);
            // 颜色连通区域可能包含多块紧贴的同色积木，随后按估计边长拆分。
            var components = Components(labels,width,height);
            double minimumSide = Math.Max(8,settings.MinimumCellPixels/factor);
            var sizes = new List<double>();
            foreach (Region region in components)
                if (region.Color < 4 && region.ShortSide >= minimumSide && region.Fill >= 0.50 &&
                    region.LongSide/region.ShortSide <= 1.45)
                    sizes.Add(region.ShortSide);
            if (sizes.Count == 0) return Reject(id,"BRICK_SCALE_UNCERTAIN","找不到可估计边长的独立方块；请加入一个清晰独立方块并竖直俯拍。");
            sizes.Sort();
            // The median of near-square anchors tolerates smaller white tops and visible side faces.
            double edge = sizes[(sizes.Count-1)/2];
            var pieces = new List<Piece>();
            var issues = new List<Issue>();
            foreach (Region region in components)
            {
                if (region.Count < edge*edge*0.30 || region.ShortSide < edge*0.45) continue;
                ImagePoint[] regionBox = ImageRegion(region.MinX,region.MinY,region.MaxX+1,region.MaxY+1,factor,frame);
                if (region.MinX == 0 || region.MinY == 0 || region.MaxX == width-1 || region.MaxY == height-1)
                { issues.Add(new Issue("BRICK_CROPPED","积木或未知物体被画面边缘裁切；请完整拍入。",frameId:frame.FrameId,imageRegion:regionBox)); continue; }
                if (region.Color == 4)
                { issues.Add(new Issue("UNKNOWN_OBJECT_COLOR","发现未标定颜色的物体；请检查颜色采样或移走杂物。",frameId:frame.FrameId,imageRegion:regionBox)); continue; }
                int columns = Math.Max(1,(int)Math.Round(region.Width/edge));
                int rows = Math.Max(1,(int)Math.Round(region.Height/edge));
                if (columns > 10 || rows > 10 || Math.Abs(region.Width/columns-edge)/edge > 0.30 ||
                    Math.Abs(region.Height/rows-edge)/edge > 0.30)
                { issues.Add(new Issue("BRICK_GEOMETRY_UNCERTAIN","方块尺寸或方向不一致；请单层摆放并竖直俯拍。",frameId:frame.FrameId,imageRegion:regionBox)); continue; }
                // 每个候选小格单独计算颜色覆盖；L 形组件的空象限仍保持空白。
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        int left = region.MinX + c*region.Width/columns, right = region.MinX + (c+1)*region.Width/columns;
                        int top = region.MinY + r*region.Height/rows, bottom = region.MinY + (r+1)*region.Height/rows;
                        int support = 0;
                        for (int y = top; y < bottom; y++)
                            for (int x = left; x < right; x++) if (labels[y*width+x] == region.Color) support++;
                        double fill = (double)support/((right-left)*(bottom-top));
                        if (fill < 0.15) continue; // Empty quadrant of an L shape stays empty.
                        if (fill < 0.55)
                        { issues.Add(new Issue("BRICK_SPLIT_UNCERTAIN","相邻方块无法可靠拆分；请减少阴影、旋转或遮挡。",frameId:frame.FrameId,
                            imageRegion:ImageRegion(left,top,right,bottom,factor,frame))); continue; }
                        pieces.Add(new Piece { Color=region.Color, Left=left*factor,Top=top*factor,
                            Right=Math.Min(frame.Width-1,right*factor),Bottom=Math.Min(frame.Height-1,bottom*factor),Fill=fill });
                    }
            }
            if (pieces.Count == 0)
            {
                if(issues.Count==0) issues.Add(new Issue("BRICKS_NOT_FOUND","没有识别到完整积木；请检查标定和背景。"));
                return new ScanResult(id,ResultStatus.Rejected,Array.Empty<PhysicalCellObservation>(),Array.Empty<BrickObservation>(),issues.ToArray());
            }
            // 以检测结果的左上方为相对原点；这里没有从照片恢复真实格纸原点。
            double minX = double.MaxValue,minY = double.MaxValue;
            foreach (Piece p in pieces) { minX=Math.Min(minX,p.X); minY=Math.Min(minY,p.Y); }
            double unit = edge*factor;
            var cells = new PhysicalCellObservation[100];
            for (int i = 0; i < 100; i++) cells[i] = new PhysicalCellObservation(i%10,i/10,PhysicalCellState.Empty);
            var bricks = new List<BrickObservation>();
            foreach (Piece p in pieces)
            {
                int c = 1 + (int)Math.Round((p.X-minX)/unit), r = 1 + (int)Math.Round((p.Y-minY)/unit);
                string brickId = $"plain-brick-{r}-{c}";
                if(bricks.Exists(b=>b.BrickId==brickId)) brickId+="-"+bricks.Count;
                if(c>=10 || r>=10)
                    issues.Add(new Issue("RELATIVE_LAYOUT_TOO_LARGE","相对布局超过 10×10；请在校对时调整位置。",new GridPoint(c,r)));
                else if(cells[r*10+c].State!=PhysicalCellState.Empty)
                {
                    issues.Add(new Issue("RELATIVE_CELL_COLLISION","两个检测方块映射到同一格；请在校对时移动。",new GridPoint(c,r)));
                    cells[r*10+c]=new PhysicalCellObservation(c,r,PhysicalCellState.Uncertain);
                }
                else cells[r*10+c]=new PhysicalCellObservation(c,r,PhysicalCellState.Occupied,brickId,p.Fill);
                bricks.Add(new BrickObservation(brickId,c,r,calibration.ColorIds[p.Color-1],p.Fill,
                    new[] { new ImageEvidence(frame.FrameId,new ImagePoint(p.X,p.Y),new[] {
                        new ImagePoint(p.Left,p.Top),new ImagePoint(p.Right,p.Top),
                        new ImagePoint(p.Right,p.Bottom),new ImagePoint(p.Left,p.Bottom) }) }));
            }
            bricks.Sort((a,b) => a.R != b.R ? a.R.CompareTo(b.R) : a.C.CompareTo(b.C));
            // IsReliable=false 标识该仿射关系仅用于相对格位和证据展示。
            var basis = new BoardObservation(Array.Empty<ImagePoint>(),
                new[] {unit,0,minX-1.5*unit,0,unit,minY-1.5*unit,0,0,1},Array.Empty<int>(),0,false,
                LayoutCoordinateBasis.RelativeBrickScale);
            return new ScanResult(id,issues.Count==0?ResultStatus.Success:ResultStatus.Rejected,cells,bricks.ToArray(),issues.ToArray(),basis);
        }

        private static bool SettingsValid(PhotoScanSettings s) => s.MaximumDetectionSide >= 300 &&
            s.MaximumDetectionSide <= 2000 && s.MaximumColorDistance > 0 && s.MaximumColorDistance < 1 &&
            s.MinimumColorMargin > 0 && s.MinimumColorMargin < 1 &&
            s.MinimumCalibrationSeparation > 0 && s.MinimumCalibrationSeparation < 1 &&
            s.MinimumCellPixels >= 10 && !double.IsInfinity(s.MinimumCellPixels) &&
            s.MinimumColorBrightness > 0 && s.MinimumColorBrightness < 1;

        private static Rgb24[] Palette(ColorCalibration c) => new[] {c.Background,c.BrickColors[0],c.BrickColors[1],c.BrickColors[2]};
        private static double Saturation(Rgb24 c)
        { double max=Math.Max(c.R,Math.Max(c.G,c.B)); return max==0?0:(max-Math.Min(c.R,Math.Min(c.G,c.B)))/max; }
        private static double Distance(Rgb24 a,Rgb24 b)
        {
            NormalizedLab first=NormalizedLab.From(a),second=NormalizedLab.From(b);
            double chroma=NormalizedLab.Distance(first,second);
            // Retain absolute intensity for white/gray: brightness normalization alone makes them identical.
            double weight=Saturation(a)<0.20 && Saturation(b)<0.20?0.85:0.12;
            double brightness=(first.Brightness-second.Brightness)*weight;
            return Math.Sqrt(chroma*chroma+brightness*brightness);
        }
        private static double NeutralForegroundFloor(Rgb24[] palette,PhotoScanSettings settings)
        {
            double floor=double.MaxValue;
            for (int i=1;i<palette.Length;i++)
                if (Saturation(palette[i])<0.20)
                    floor=Math.Min(floor,NormalizedLab.From(palette[i]).Brightness-settings.MaximumColorDistance+settings.MinimumColorMargin*0.5);
            return floor==double.MaxValue?NormalizedLab.From(palette[0]).Brightness+0.28:floor;
        }
        private static byte Classify(Rgb24 pixel,Rgb24[] palette,PhotoScanSettings settings,double neutralForegroundFloor)
        {
            NormalizedLab descriptor=NormalizedLab.From(pixel),backgroundDescriptor=NormalizedLab.From(palette[0]);
            // A neutral background can be much brighter than its sampled patch. Keep neutral
            // foreground near its calibrated brightness; normalized chroma alone also matches white.
            if (Saturation(pixel)<0.20 && Saturation(palette[0])<0.20 &&
                descriptor.Brightness<neutralForegroundFloor &&
                NormalizedLab.Distance(descriptor,backgroundDescriptor)<=settings.MaximumColorDistance) return 0;
            double best=double.MaxValue,next=double.MaxValue; byte selected=0;
            for (byte i=0;i<4;i++)
            {
                double distance=Distance(pixel,palette[i]);
                if (distance<best) { next=best;best=distance;selected=i; } else if (distance<next) next=distance;
            }
            if (best<=settings.MaximumColorDistance && next-best>=settings.MinimumColorMargin) return selected;
            double bright=descriptor.Brightness,background=backgroundDescriptor.Brightness;
            return bright>0.25 && (Saturation(pixel)>0.30 || bright>background+0.28)?(byte)4:(byte)0;
        }
        private static ImagePoint[] ImageRegion(int left,int top,int right,int bottom,int factor,ImageFrame frame)
        {
            int x=Math.Min(frame.Width-1,left*factor),y=Math.Min(frame.Height-1,top*factor);
            int endX=Math.Min(frame.Width-1,right*factor),endY=Math.Min(frame.Height-1,bottom*factor);
            return new[] {new ImagePoint(x,y),new ImagePoint(endX,y),new ImagePoint(endX,endY),new ImagePoint(x,endY)};
        }
        private static byte[] Denoise(byte[] source,int width,int height)
        {
            var target=new byte[source.Length];
            for (int y=0;y<height;y++) for (int x=0;x<width;x++)
            {
                byte label=source[y*width+x]; if (label==0) continue;
                int count=0;
                for (int dy=-1;dy<=1;dy++) for (int dx=-1;dx<=1;dx++)
                    if (x+dx>=0 && y+dy>=0 && x+dx<width && y+dy<height && source[(y+dy)*width+x+dx]==label) count++;
                if (count>=5) target[y*width+x]=label;
            }
            return target;
        }
        private static List<Region> Components(byte[] labels,int width,int height)
        {
            var visited=new bool[labels.Length]; var queue=new int[labels.Length]; var regions=new List<Region>();
            for (int start=0;start<labels.Length;start++)
            {
                if (labels[start]==0 || visited[start]) continue;
                var region=new Region {Color=labels[start],MinX=width,MinY=height};
                int head=0,count=1;queue[0]=start;visited[start]=true;
                while (head<count)
                {
                    int index=queue[head++],x=index%width,y=index/width;
                    region.MinX=Math.Min(region.MinX,x);region.MinY=Math.Min(region.MinY,y);
                    region.MaxX=Math.Max(region.MaxX,x);region.MaxY=Math.Max(region.MaxY,y);
                    for (int direction=0;direction<4;direction++)
                    {
                        int nx=x+(direction==0?-1:direction==1?1:0),ny=y+(direction==2?-1:direction==3?1:0);
                        if (nx<0 || ny<0 || nx>=width || ny>=height) continue;
                        int next=ny*width+nx;
                        if (!visited[next] && labels[next]==region.Color) { visited[next]=true;queue[count++]=next; }
                    }
                }
                region.Count=count;regions.Add(region);
            }
            return regions;
        }
        private sealed class Region
        {
            public byte Color; public int MinX,MinY,MaxX,MaxY,Count;
            public int Width=>MaxX-MinX+1; public int Height=>MaxY-MinY+1;
            public double ShortSide=>Math.Min(Width,Height); public double LongSide=>Math.Max(Width,Height);
            public double Fill=>(double)Count/(Width*Height);
        }
        private sealed class Piece
        { public int Color,Left,Top,Right,Bottom; public double Fill; public double X=>(Left+Right)/2.0; public double Y=>(Top+Bottom)/2.0; }
        private static ScanResult Reject(string id,string code,string message) => new ScanResult(id,ResultStatus.Rejected,
            Array.Empty<PhysicalCellObservation>(),Array.Empty<BrickObservation>(),new[] {new Issue(code,message)});
    }
}
