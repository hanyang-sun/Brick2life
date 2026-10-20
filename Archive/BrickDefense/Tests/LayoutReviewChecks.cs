using BrickDefense.Core;
using BrickDefense.Vision;
using BrickDefense.Workflow;

internal static class LayoutReviewChecks
{
    public static async Task Run()
    {
        var raw=Scan(new[] {B("a",1,1,"core"),B("b",3,1,"tower"),B("c",2,3,"wall"),B("false",6,4,"tower")});
        var editor=new LayoutEditSession(raw,Rules());
        Check(!editor.Move("a",3,1) && editor.Bricks[0].C==1,"Moving onto another brick changed the draft.");
        Check(!editor.Translate(-2,0) && editor.Bricks[0].C==1,"Out-of-bounds translation changed the draft.");
        Check(editor.Move("b",4,1) && editor.Move("b",5,1) && editor.Undo() && Find(editor,"b").C==4,"Undo lost brick identity.");
        Check(editor.Delete("false") && editor.ChangeRole("b",BrickRole.Wall) && editor.Add(6,6,BrickRole.Wall),"Correction commands failed.");
        Check(editor.Translate(1,1) && editor.ConfirmPhysical(),"Physical review confirmation failed.");
        var stats=editor.Statistics;
        Check(stats.ActualCount==4 && stats.CorrectRecognition==2 && stats.CorrectPosition==2 &&
            stats.Missed==1 && stats.FalsePositive==1 && stats.WrongColor==1 && stats.WrongPosition==1 &&
            stats.RecognitionAccuracy==0.5 && stats.PositionAccuracy==0.5,"Independent color/grid metrics are wrong.");
        editor.ChangeRole("a",BrickRole.Tower); editor.Delete("c"); editor.Translate(1,0);
        var record=editor.CreateRecord();
        Check(ReferenceEquals(stats,editor.Statistics) && record.Verified.Length==4 && record.Designed.Length==3 &&
            record.Verified.Single(b=>b.BrickId=="a").ColorId=="core","Free design mutated frozen truth.");
        raw.Bricks[0]=B("changed-source",0,0,"wall");
        record.Original[0].Evidence[0].Region[0]=new ImagePoint(-1,-1);
        editor.Bricks[0].Evidence[0].Region[0]=new ImagePoint(-5,-5);
        Check(editor.Original.Bricks[0].BrickId=="a" && editor.CreateRecord().Original[0].Evidence[0].Region[0].X==10,
            "External arrays mutated source evidence.");
        editor.Reset(); Check(editor.Bricks.Count==4 && editor.Statistics.PositionAccuracy==0.5,"Design reset altered metrics.");
        editor.ReopenPhysical(); Check(editor.Statistics==null && editor.Stage==LayoutEditStage.PhysicalReview,"Reopening retained stale statistics.");
        Check(editor.ConfirmPhysical() && editor.Statistics.CorrectPosition==2,"Reconfirmation compared against an edited source.");

        var translated=new LayoutEditSession(Scan(new[] {B("p",1,1,"wall"),B("q",3,3,"core")}),Rules());
        Check(translated.Translate(2,1) && translated.ConfirmPhysical() && translated.Statistics.PositionAccuracy==1,
            "Whole-layout placement was counted as relative grid error.");
        var levels=new LevelSession(); levels.LoadExample(false); var previous=levels.Current;
        var noCore=new LayoutEditSession(Scan(new[] {B("wall-only",1,1,"wall")},ResultStatus.Rejected),Rules());
        Check(noCore.ConfirmPhysical(),"A missing-core draft was blocked before review.");
        Check(!levels.TryLoad(noCore.CreateLayout()) && ReferenceEquals(previous,levels.Current),"Invalid publication replaced the level.");
        Check(noCore.Add(5,5,BrickRole.Core) && levels.TryLoad(noCore.CreateLayout()) && noCore.Statistics.ActualCount==1,
            "A designed core failed to publish or changed truth.");
        Check(levels.Current.Result.Entities.Length==2,"Publication lost an edited brick.");
        var collision=new LayoutEditSession(Scan(new[] {B("one",2,2,"wall"),B("two",2,2,"core"),B("outside",11,3,"tower")},ResultStatus.Rejected),Rules());
        Check(!collision.ConfirmPhysical() && collision.Move("two",3,2) && collision.Move("outside",5,3) && collision.ConfirmPhysical(),
            "Recoverable collisions/outside detections could not be corrected.");

        var photo=new PhotoScanSession(new PhotoScanSettings());
        photo.SetFrame(PlainBackgroundChecks.ReadFixture());
        var calibration=new ColorCalibration(photo.SampleColor(650,850),new[] {photo.SampleColor(350,1380),photo.SampleColor(160,1040),photo.SampleColor(340,570)},
            new[] {"physical-color-0","physical-color-1","physical-color-2"});
        await photo.AnalyzeAsync(calibration); var real=photo.BeginReview();
        Check(real.Bricks.Count==10 && real.ConfirmPhysical() && real.Statistics.RecognitionAccuracy==1 && real.Statistics.PositionAccuracy==1,
            "Original JPG review round trip lost the ten real bricks.");
        photo.Invalidate(); ExpectInvalid(()=>photo.BeginReview());
        Console.WriteLine("LAYOUT_REVIEW_CHECKS_PASSED: independent metrics, stable IDs, corrections, translation, undo, frozen truth, evidence, recovery, publication and real-photo review.");
    }
    private static BrickObservation Find(LayoutEditSession editor,string id)=>editor.Bricks.Single(b=>b.BrickId==id);
    private static BrickObservation B(string id,int c,int r,string color)=>new(id,c,r,color,1,
        new[] {new ImageEvidence("review-frame",new ImagePoint(20,20),new[] {new ImagePoint(10,10),new ImagePoint(30,10),new ImagePoint(30,30),new ImagePoint(10,30)})});
    private static LevelRules Rules()=>new(new Dictionary<string,BrickRole> { {"core",BrickRole.Core},{"wall",BrickRole.Wall},{"tower",BrickRole.Tower} });
    private static ScanResult Scan(BrickObservation[] bricks,ResultStatus status=ResultStatus.Success)=>
        new("review-check",status,Array.Empty<PhysicalCellObservation>(),bricks,Array.Empty<Issue>());
    private static void ExpectInvalid(Action action)
    { try { action(); } catch(InvalidOperationException) { return; } throw new Exception("An invalid scan opened review."); }
    private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
}
