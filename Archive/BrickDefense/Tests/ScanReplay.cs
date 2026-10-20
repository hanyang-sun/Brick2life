using System.IO.Compression;
using System.Text.Json;
using BrickDefense.Core;
using BrickDefense.Vision;

internal static class ScanReplay
{
    // Replay the actual saved pixels, palette and settings without Unity or manual sampling.
    public static void Run(string directory,bool expectRejected,string backgroundAt=null)
    {
        using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"result.json")));
        var root=report.RootElement;
        using var compressed=File.OpenRead(Path.Combine(directory,"source.rgb.gz"));
        using var gzip=new GZipStream(compressed,CompressionMode.Decompress); using var reader=new BinaryReader(gzip);
        int width=reader.ReadInt32(),height=reader.ReadInt32(); var pixels=new Rgb24[width*height];
        for (int i=0;i<pixels.Length;i++) pixels[i]=new Rgb24(reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
        Rgb24[] colors=root.GetProperty("calibration").GetProperty("colors").EnumerateArray().Select(c=>
            new Rgb24(c.GetProperty("r").GetByte(),c.GetProperty("g").GetByte(),c.GetProperty("b").GetByte())).ToArray();
        var settings=new PhotoScanSettings();
        foreach (var property in typeof(PhotoScanSettings).GetProperties())
        {
            string key=char.ToLowerInvariant(property.Name[0])+property.Name.Substring(1);
            if (root.GetProperty("settings").TryGetProperty(key,out var value))
                property.SetValue(settings,property.PropertyType==typeof(int)?(object)value.GetInt32():value.GetDouble());
        }
        var frame=new ImageFrame("saved-replay",width,height,pixels,DateTime.UnixEpoch);
        if (backgroundAt!=null)
        {
            int[] point=backgroundAt.Split(',').Select(int.Parse).ToArray();
            colors[0]=ColorCalibration.SamplePatch(frame,point[0],point[1]);
            Console.WriteLine($"REPLAY background override={colors[0].R},{colors[0].G},{colors[0].B}");
        }
        var calibration=new ColorCalibration(colors[0],colors.Skip(1).ToArray(),new[] {"core","wall","tower"});
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var scan=PlainBackgroundRecognizer.Recognize(new CaptureSet("saved-replay",frame),calibration,settings);
        Console.WriteLine($"SCAN_REPLAY {width}x{height}: {scan.Status}, bricks={scan.Bricks.Length}, ms={timer.ElapsedMilliseconds}, issues="+
            string.Join(",",scan.Issues.Select(i=>i.Code)));
        if (expectRejected)
        {
            if (scan.Status!=ResultStatus.Rejected || !scan.Issues.Any(i=>i.Code=="BRICK_CROPPED"))
                throw new Exception("A genuinely cropped saved photograph was accepted.");
        }
        else if (scan.Status!=ResultStatus.Success || scan.Bricks.Length!=10)
            throw new Exception("Complete original photo with the user's saved calibration was rejected or lost cubes.");
    }
}
