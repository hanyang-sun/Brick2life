using System.IO.Compression;
using BrickDefense.Core;
using BrickDefense.Vision;
using BrickDefense.Workflow;

internal static class ScanRejectionChecks
{
    // Exact user's palette from the saved failed scans, not the developer's chosen sampling points.
    public static ColorCalibration UserCalibration() => new ColorCalibration(new Rgb24(65,61,63),
        new[] {new Rgb24(251,251,251),new Rgb24(253,97,48),new Rgb24(5,195,254)},
        new[] {"physical-color-0","physical-color-1","physical-color-2"});

    public static void Run()
    {
        var original=PlainBackgroundChecks.ReadFixture();
        var complete=Recognize(original);
        AssertSuccess(complete,10,"Complete original with user's saved dark background sample");
        Check(complete.Bricks.Count(b=>b.ColorId=="physical-color-0")==1 && complete.Bricks.Count(b=>b.ColorId=="physical-color-1")==7 &&
            complete.Bricks.Count(b=>b.ColorId=="physical-color-2")==2,"User's role colors changed.");
        AssertSuccess(Recognize(NeutralGradient()),3,"Gray background brightness gradient");
        var cropped=Recognize(ReadCropped());
        Check(cropped.Status==ResultStatus.Rejected && cropped.Issues.Any(i=>i.Code=="BRICK_CROPPED"),
            "The screenshot's genuinely cropped photograph was accepted.");
        Check(cropped.Issues.All(i=>i.FrameId=="user-cropped-v1" && i.ImageRegion.Length==4 &&
            i.ImageRegion.All(p=>p.X>=0 && p.X<958 && p.Y>=0 && p.Y<938)),"Rejection lost its original image region.");
        Check(cropped.Issues.Where(i=>i.Code=="BRICK_CROPPED").All(i=>i.ImageRegion.Any(p=>p.X==0 || p.Y==0 || p.X==957 || p.Y==937)),
            "Crop evidence does not point to the frame boundary.");
        var levels=new LevelSession(); levels.LoadExample(false); LevelContext current=levels.Current;
        Check(!levels.TryLoad(new LayoutDefinition(cropped,UserCalibration().CreateRules(),LayoutSource.Photograph)) &&
            ReferenceEquals(current,levels.Current),"Rejected crop replaced the displayed level.");
        Console.WriteLine("Scan rejection checks passed: user's exact palette, bright gray background, genuine cropped JPG and retained active level.");
    }
    private static ScanResult Recognize(ImageFrame frame) => PlainBackgroundRecognizer.Recognize(new CaptureSet("rejection-check",frame),UserCalibration());
    private static void AssertSuccess(ScanResult scan,int count,string name)
    { Check(scan.Status==ResultStatus.Success && scan.Bricks.Length==count,name+": "+scan.Status+"; "+string.Join(",",scan.Issues.Select(i=>i.Code))); }
    private static ImageFrame NeutralGradient()
    {
        const int width=420,height=400; var pixels=new Rgb24[width*height];
        for (int y=0;y<height;y++) for (int x=0;x<width;x++)
        { byte value=(byte)(75+105*y/(height-1)); pixels[y*width+x]=new Rgb24(value,value,value); }
        Rgb24[] colors=UserCalibration().BrickColors;
        for (int i=0;i<3;i++) for (int y=80;y<140;y++) for (int x=50+i*110;x<110+i*110;x++) pixels[y*width+x]=colors[i];
        return new ImageFrame("neutral-gradient",width,height,pixels,DateTime.UnixEpoch);
    }
    public static ImageFrame ReadCropped()
    {
        using var file=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Fixtures","real-cropped-bricks-v1.rgb.gz"));
        using var gzip=new GZipStream(file,CompressionMode.Decompress); using var reader=new BinaryReader(gzip);
        int width=reader.ReadInt32(),height=reader.ReadInt32(); Check(width==958 && height==938,"Cropped fixture dimensions changed.");
        var pixels=new Rgb24[width*height];
        for (int i=0;i<pixels.Length;i++) pixels[i]=new Rgb24(reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
        return new ImageFrame("user-cropped-v1",width,height,pixels,DateTime.UnixEpoch);
    }
    private static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
}
