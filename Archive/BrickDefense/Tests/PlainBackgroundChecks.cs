using System.IO.Compression;
using BrickDefense.Core;
using BrickDefense.Vision;
using BrickDefense.Workflow;

internal static class PlainBackgroundChecks
{
    public static async Task Run()
    {
        ImageFrame frame=ReadFixture();
        var calibration=Calibration(frame);
        var scan=Recognize(frame,calibration);
        AssertReal(scan);
        var level=LevelBuilder.Build(GridMapper.Map(scan),calibration.CreateRules(),new GridPoint(1,18));
        Check(level.Status==ResultStatus.Success && level.Entities.Length==10,"Real photo did not produce a usable level.");
        // Independent manual annotations of the original photograph, not production grid coordinates.
        var centers=new[] {(340,570,"blue"),(350,1380,"white"),(160,1040,"orange"),
            (160,1200,"orange"),(160,1360,"orange"),(335,1510,"orange"),(500,1510,"orange"),
            (530,1020,"orange"),(700,1020,"orange"),(880,1510,"blue")};
        foreach (var expected in centers)
            Check(scan.Bricks.Count(b=>b.ColorId==expected.Item3 && Math.Abs(b.Evidence[0].Center.X-expected.Item1)<65 &&
                Math.Abs(b.Evidence[0].Center.Y-expected.Item2)<65)==1,"Real photo lost or duplicated an annotated cube.");
        AssertReal(Recognize(Half(frame),calibration));

        var session=new PhotoScanSession(new PhotoScanSettings());
        session.SetFrame(frame);
        Check(session.CalibrationReady(calibration),"Gray background and white cube could not be calibrated.");
        await session.AnalyzeAsync(calibration); AssertReal(session.Result);
        var active=new LevelSession(); active.LoadExample(false);
        Check(active.TryLoad(session.ConfirmLayout()) && active.Current.Result.Entities.Length==10,"Plain photo confirmation failed.");
        var swapped=new ColorCalibration(calibration.Background,new[] {calibration.BrickColors[2],calibration.BrickColors[1],calibration.BrickColors[0]},
            new[] {"blue","orange","white"});
        await session.AnalyzeAsync(swapped);
        AssertReal(session.Result);
        Check(active.TryLoad(session.ConfirmLayout()) && active.Current.Result.Entities.Count(e=>e.Role==BrickRole.Core)==2 &&
            active.Current.Result.Entities.Count(e=>e.Role==BrickRole.Tower)==1,"Color setup did not control the roles.");
        session.Invalidate();
        Check(session.Result==null,"Changing calibration retained a stale confirmation.");
        ExpectInvalid(()=>session.ConfirmLayout());

        var synthetic=Synthetic();
        var ideal=new ColorCalibration(new Rgb24(80,80,80),new[] {White,Orange,Blue},new[] {"white","orange","blue"});
        var clean=Recognize(synthetic,ideal); AssertReal(clean);
        var square=Recognize(Synthetic(square:true),ideal);
        Check(square.Status==ResultStatus.Success && square.Bricks.Length==9 && square.Bricks.Count(b=>b.ColorId=="orange")==6,
            "2x2 touching cubes merged or filled an empty cell.");
        var unknown=Synthetic(); Paint(unknown,40,860,90,90,new Rgb24(40,230,60));
        Check(Recognize(unknown,ideal).Issues.Any(i=>i.Code=="UNKNOWN_OBJECT_COLOR"),"Unknown-color cube was silently dropped.");
        var cropped=Synthetic(); Paint(cropped,0,160,90,90,Blue);
        Check(Recognize(cropped,ideal).Issues.Any(i=>i.Code=="BRICK_CROPPED"),"Cropped cube was accepted.");
        var none=Synthetic(); Array.Fill(none.Pixels,new Rgb24(80,80,80));
        Check(Recognize(none,ideal).Status==ResultStatus.Rejected,"Empty background became a valid layout.");
        Check(!session.CalibrationReady(null),"Missing calibration was accepted.");
        Check(PlainBackgroundRecognizer.Recognize(new CaptureSet("bad-settings",frame),calibration,
            new PhotoScanSettings { MinimumCellPixels=double.PositiveInfinity }).Issues.Any(i=>i.Code=="SCAN_SETTINGS_INVALID"),
            "Invalid object-size settings were accepted.");
        Console.WriteLine("Plain-background checks passed: default session uses real photo (10 cubes), scale, user-controlled roles, touching L/2x2, unknown/cropped objects and calibration invalidation.");
    }
    public static ImageFrame ReadFixture()
    {
        using var file=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Fixtures","real-unmarked-bricks-v1.rgb.gz"));
        using var gzip=new GZipStream(file,CompressionMode.Decompress); using var reader=new BinaryReader(gzip);
        int width=reader.ReadInt32(),height=reader.ReadInt32();
        Check(width==1080 && height==2449,"Original real fixture dimensions changed.");
        var pixels=new Rgb24[width*height];
        for (int i=0;i<pixels.Length;i++) pixels[i]=new Rgb24(reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
        return new ImageFrame("real-unmarked-v1",width,height,pixels,DateTime.UnixEpoch);
    }
    public static ColorCalibration Calibration(ImageFrame frame) => new ColorCalibration(ColorCalibration.SamplePatch(frame,650,850),
        new[] {ColorCalibration.SamplePatch(frame,350,1380),ColorCalibration.SamplePatch(frame,160,1040),ColorCalibration.SamplePatch(frame,340,570)},
        new[] {"white","orange","blue"});
    private static ScanResult Recognize(ImageFrame f,ColorCalibration c) => PlainBackgroundRecognizer.Recognize(new CaptureSet("plain-check",f),c);
    private static void AssertReal(ScanResult scan)
    {
        Check(scan.Status==ResultStatus.Success,"Plain photo failed: "+string.Join(";",scan.Issues.Select(i=>i.Code+":"+i.Message)));
        Check(scan.Bricks.Length==10 && scan.Bricks.Count(b=>b.ColorId=="orange")==7 &&
            scan.Bricks.Count(b=>b.ColorId=="white")==1 && scan.Bricks.Count(b=>b.ColorId=="blue")==2,"Wrong real-photo count or colors.");
        Check(scan.PhysicalCells.Length==100 && scan.PhysicalCells.Count(c=>c.State==PhysicalCellState.Empty)==90 &&
            scan.Bricks.Select(b=>(b.C,b.R)).Distinct().Count()==10,"Relative cells collided or lost objects.");
        Check(scan.Board.CoordinateBasis==LayoutCoordinateBasis.RelativeBrickScale && scan.Board.MarkerIds.Length==0 &&
            !scan.Board.IsReliable && scan.Bricks.All(b=>b.Evidence.Length==1 && b.Evidence[0].Region.Length==4),"Relative layout pretended to locate a marked board.");
    }
    private static ImageFrame Half(ImageFrame f)
    {
        int width=f.Width/2,height=f.Height/2; var pixels=new Rgb24[width*height];
        for (int y=0;y<height;y++) for (int x=0;x<width;x++) pixels[y*width+x]=f.Pixels[2*y*f.Width+2*x];
        return new ImageFrame("half-real-photo",width,height,pixels,DateTime.UnixEpoch);
    }
    private static readonly Rgb24 White=new Rgb24(245,245,245),Orange=new Rgb24(250,100,30),Blue=new Rgb24(10,185,235);
    private static ImageFrame Synthetic(bool square=false)
    {
        var pixels=new Rgb24[900*1100]; Array.Fill(pixels,new Rgb24(80,80,80));
        var frame=new ImageFrame("independent-plain",900,1100,pixels,DateTime.UnixEpoch);
        Paint(frame,540,135,90,90,Blue); Paint(frame,450,585,90,90,White); Paint(frame,720,675,90,90,Blue);
        if (square) Paint(frame,180,315,180,180,Orange);
        else { Paint(frame,180,315,90,270,Orange); Paint(frame,270,495,180,90,Orange); }
        Paint(frame,450,315,180,90,Orange);
        return frame;
    }
    private static void Paint(ImageFrame f,int left,int top,int width,int height,Rgb24 color)
    { for (int y=top;y<top+height;y++) for (int x=left;x<left+width;x++) f.Pixels[y*f.Width+x]=color; }
    private static void ExpectInvalid(Action action) { try {action();} catch (InvalidOperationException) {return;} throw new Exception("Stale plain scan was confirmed."); }
    private static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
}
