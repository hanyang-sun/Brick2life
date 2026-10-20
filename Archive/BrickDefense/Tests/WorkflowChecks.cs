using BrickDefense.Core;
using BrickDefense.Vision;
using BrickDefense.Workflow;

internal static class WorkflowChecks
{
    public static async Task Run()
    {
        var session=new LevelSession(); int notifications=0;
        session.Changed+=context => { Check(ReferenceEquals(context,session.Current),"Notification exposed an incomplete context."); notifications++; };
        session.LoadExample(false);
        CheckReference(session);
        var colors=new Dictionary<string,BrickRole>
        { {"physical-color-0",BrickRole.Core},{"physical-color-1",BrickRole.Wall},{"physical-color-2",BrickRole.Tower} };
        var source=PhysicalScan();
        var definition=new LayoutDefinition(source,new LevelRules(colors),LayoutSource.Photograph);
        Check(session.TryLoad(definition),"Photo definition failed."); CheckReference(session);
        colors["physical-color-0"]=BrickRole.Wall;
        source.PhysicalCells[0]=new PhysicalCellObservation(0,0,PhysicalCellState.Uncertain);
        Check(session.Current.Definition.Rules.ColorRoleMap["physical-color-0"]==BrickRole.Core &&
            session.Current.Definition.Layout.PhysicalCells[0].State==PhysicalCellState.Empty,"Caller changed the active definition.");
        for (int i=0; i<3; i++)
        {
            session.LoadExample(true);
            Check(session.Current.Result.Entities.Length==5 && session.Current.Result.Issues.Any(issue=>issue.Code=="NO_ROUTE"),
                "Unreachable layout lost its preview.");
            session.LoadExample(false); CheckReference(session);
            Check(session.TryLoad(new LayoutDefinition(PhysicalScan(),PhysicalRules(),LayoutSource.Photograph)),"Photo reload failed.");
            CheckReference(session);
        }
        LevelContext previous=session.Current; int before=notifications;
        Check(!session.TryLoad(new LayoutDefinition(PhysicalScan(),ExampleScans.CreateDemoRules(),LayoutSource.Photograph)),
            "Mismatched rules were accepted.");
        Check(!session.TryLoad(new LayoutDefinition(source,PhysicalRules(),LayoutSource.Photograph)),"Uncertain layout was accepted.");
        Check(!session.TryLoad(null),"Null definition was accepted.");
        Check(ReferenceEquals(previous,session.Current) && notifications==before,"Failed input changed the active level.");
        Check(!session.SelectSpawn(new GridPoint(-1,0)) && ReferenceEquals(previous,session.Current),"Invalid spawn changed the level.");
        Check(session.SelectSpawn(new GridPoint(14,14)) && session.Current.Result.Issues.Any(i=>i.Code=="SPAWN_OCCUPIED") &&
            session.Current.Result.Entities.Length==9,"Occupied spawn lost the layout.");
        Check(session.SelectSpawn(new GridPoint(1,18)),"Valid spawn failed."); CheckReference(session);

        var photo=new PhotoScanSession(new PhotoScanSettings());
        ExpectInvalid(()=>photo.ConfirmLayout(),"Unanalyzed photo was confirmed.");
        photo.SetFrame(PlainBackgroundChecks.ReadFixture());
        var samples=new[] {photo.SampleColor(350,1380),photo.SampleColor(160,1040),photo.SampleColor(340,570)};
        var ids=new[] {"physical-color-0","physical-color-1","physical-color-2"};
        var calibration=new ColorCalibration(photo.SampleColor(650,850),samples,ids);
        ScanResult result=await photo.AnalyzeAsync(calibration);
        Check(result.Status==ResultStatus.Success && result.Bricks.Length==10 && !photo.IsBusy,"Photo workflow failed.");
        samples[0]=samples[1]; ids[0]="modified-after-analysis";
        LayoutDefinition confirmed=photo.ConfirmLayout();
        Check(confirmed.Rules.ColorRoleMap["physical-color-0"]==BrickRole.Core,"Confirmation used a later calibration.");
        Check(session.TryLoad(confirmed) && session.Current.Result.Status==ResultStatus.Success &&
            session.Current.Result.Entities.Length==10,"Confirmed photo could not be loaded.");
        string oldId=photo.ScanId;
        photo.SetFrame(PlainBackgroundChecks.ReadFixture());
        Check(photo.Result==null && photo.ScanId!=oldId,"A new photo retained an old result.");
        ExpectInvalid(()=>photo.ConfirmLayout(),"A new photo confirmed an earlier scan.");
        Console.WriteLine("Workflow checks passed: repeated input switching, atomic rollback, snapshots, spawn changes and original-photo confirmation.");
    }

    private static ScanResult PhysicalScan()
    {
        ScanResult source=ExampleScans.CreateReference();
        return new ScanResult("workflow-photo",ResultStatus.Success,source.PhysicalCells,source.Bricks.Select(b=>
            new BrickObservation(b.BrickId,b.C,b.R,b.ColorId==ExampleScans.CoreColor?"physical-color-0":
                b.ColorId==ExampleScans.WallColor?"physical-color-1":"physical-color-2")).ToArray(),Array.Empty<Issue>());
    }
    private static LevelRules PhysicalRules()=>new(new Dictionary<string,BrickRole>
    { {"physical-color-0",BrickRole.Core},{"physical-color-1",BrickRole.Wall},{"physical-color-2",BrickRole.Tower} });
    private static void CheckReference(LevelSession session)
    { Check(session.Current.Result.Status==ResultStatus.Success && session.Current.Result.Entities.Length==9 &&
        session.Current.Result.Route.Length-1==16,"Reference layout or route changed."); }
    private static void ExpectInvalid(Action action,string message)
    { try { action(); } catch (InvalidOperationException) { return; } throw new Exception(message); }
    private static void Check(bool value,string message) { if (!value) throw new Exception(message); }
}
