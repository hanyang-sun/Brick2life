using System;
using System.Collections.Generic;
using BrickDefense.Core;

namespace BrickDefense.Workflow
{
    // 编辑、撤销、真值冻结和提交均集中在此；Unity 视图只接收快照。
    public sealed class LayoutEditSession
    {
        private readonly ScanResult original;
        private readonly LevelRules rules;
        private readonly List<BrickObservation> draft=new List<BrickObservation>();
        private readonly List<BrickObservation[]> history=new List<BrickObservation[]>();
        private readonly List<LayoutEditOperation> operations=new List<LayoutEditOperation>();
        private BrickObservation[] verified;
        private DateTime confirmedAt;
        public string ReviewId { get; }=Guid.NewGuid().ToString("N");
        public string ScanId=>original.ScanId;
        public ResultStatus OriginalStatus=>original.Status;
        public LayoutEditStage Stage { get; private set; }
        public LayoutReviewStatistics Statistics { get; private set; }
        public Issue LastIssue { get; private set; }
        public bool CanUndo=>history.Count>0;
        public IReadOnlyList<BrickObservation> Bricks=>Array.AsReadOnly(Copy(draft));
        public ScanResult Original=>CopyScan(original);
        public event Action Changed;

        public LayoutEditSession(ScanResult scan,LevelRules sourceRules)
        {
            if(scan?.Bricks==null || string.IsNullOrWhiteSpace(scan.ScanId) || sourceRules?.ColorRoleMap==null)
                throw new ArgumentException("A completed scan and color rules are required.");
            var mapping=new Dictionary<string,BrickRole>();
            foreach(var pair in sourceRules.ColorRoleMap) mapping.Add(pair.Key,pair.Value);
            var roles=new HashSet<BrickRole>(mapping.Values);
            if(mapping.Count!=3 || !roles.Contains(BrickRole.Core) || !roles.Contains(BrickRole.Wall) || !roles.Contains(BrickRole.Tower))
                throw new ArgumentException("Three distinct roles are required.");
            var ids=new HashSet<string>();
            foreach(BrickObservation brick in scan.Bricks)
                if(brick==null || string.IsNullOrWhiteSpace(brick.BrickId) || !ids.Add(brick.BrickId))
                    throw new ArgumentException("Detection IDs must be unique.");
            original=CopyScan(scan); rules=new LevelRules(mapping); draft.AddRange(Copy(original.Bricks));
        }

        public BrickRole RoleFor(string colorId)=>rules.ColorRoleMap.TryGetValue(colorId,out BrickRole role)?role:BrickRole.Wall;
        public string ColorFor(BrickRole role)
        { foreach(var pair in rules.ColorRoleMap) if(pair.Value==role) return pair.Key; throw new ArgumentException("Unknown role."); }

        public bool Add(int c,int r,BrickRole role)
        {
            if(!Available(c,r,null)) return false;
            SaveUndo(); draft.Add(new BrickObservation("manual-"+Guid.NewGuid().ToString("N"),c,r,ColorFor(role),0));
            Commit("add",draft[draft.Count-1].BrickId); return true;
        }
        public bool Delete(string id)
        {
            int index=Index(id); if(index<0) return Fail("BRICK_MISSING","请选择一块积木。");
            SaveUndo(); draft.RemoveAt(index); Commit("delete",id); return true;
        }
        public bool ChangeRole(string id,BrickRole role)
        {
            int index=Index(id); if(index<0) return Fail("BRICK_MISSING","请选择一块积木。");
            BrickObservation brick=draft[index]; string color=ColorFor(role);
            if(brick.ColorId==color) { LastIssue=null; return true; }
            SaveUndo(); draft[index]=With(brick,brick.C,brick.R,color); Commit("color",id); return true;
        }
        public bool Move(string id,int c,int r)
        {
            int index=Index(id); if(index<0) return Fail("BRICK_MISSING","请选择一块积木。");
            if(!Available(c,r,id)) return false;
            BrickObservation brick=draft[index]; if(brick.C==c && brick.R==r) { LastIssue=null; return true; }
            SaveUndo(); draft[index]=With(brick,c,r,brick.ColorId); Commit("move",id); return true;
        }
        public bool Translate(int dc,int dr)
        {
            if(draft.Count==0) return Fail("DRAFT_EMPTY","请先添加积木。");
            if(dc==0 && dr==0) return true;
            foreach(BrickObservation brick in draft)
                if(!Inside(brick.C+dc,brick.R+dr)) return Fail("LAYOUT_OUT_OF_BOUNDS","整体移动会超出 10×10 棋盘，请先调整边缘积木。");
            SaveUndo();
            for(int i=0;i<draft.Count;i++) draft[i]=With(draft[i],draft[i].C+dc,draft[i].R+dr,draft[i].ColorId);
            Commit("translate "+dc+","+dr,null); return true;
        }
        public bool Undo()
        {
            if(history.Count==0) return false;
            draft.Clear(); draft.AddRange(history[history.Count-1]); history.RemoveAt(history.Count-1);
            Commit("undo",null); return true;
        }
        public void Reset()
        {
            SaveUndo(); draft.Clear(); draft.AddRange(Copy(Stage==LayoutEditStage.PhysicalReview?original.Bricks:verified));
            Commit("reset",null);
        }

        public bool ConfirmPhysical()
        {
            if(Stage!=LayoutEditStage.PhysicalReview) return Fail("REVIEW_ALREADY_CONFIRMED","请先返回实物校对。");
            if(!ValidDraft()) return false;
            verified=Copy(draft); Statistics=Compare(original.Bricks,verified); confirmedAt=DateTime.UtcNow;
            operations.Add(new LayoutEditOperation("confirm physical",null,Stage));
            Stage=LayoutEditStage.GameDesign; history.Clear(); LastIssue=null; Changed?.Invoke(); return true;
        }
        public void ReopenPhysical()
        {
            if(verified==null) return;
            draft.Clear(); draft.AddRange(Copy(verified)); Stage=LayoutEditStage.PhysicalReview;
            Statistics=null; history.Clear(); Commit("reopen physical",null);
        }
        public LayoutDefinition CreateLayout()
        {
            if(Stage!=LayoutEditStage.GameDesign || Statistics==null)
                throw new InvalidOperationException("请先确认实物校对结果。");
            if(!ValidDraft()) throw new InvalidOperationException(LastIssue.Message);
            var cells=new PhysicalCellObservation[100];
            for(int i=0;i<100;i++) cells[i]=new PhysicalCellObservation(i%10,i/10,PhysicalCellState.Empty);
            BrickObservation[] bricks=Copy(draft);
            foreach(var brick in bricks) cells[brick.R*10+brick.C]=new PhysicalCellObservation(brick.C,brick.R,PhysicalCellState.Occupied,brick.BrickId,brick.Confidence);
            // 编辑后格位已改变；原照片投影只保留在 Original，不能宣称适用于设计地图。
            return new LayoutDefinition(new ScanResult(ScanId,ResultStatus.Success,cells,bricks,Array.Empty<Issue>()),rules,LayoutSource.Photograph);
        }
        public LayoutReviewRecord CreateRecord(bool published=false)
        {
            if(Statistics==null || Stage!=LayoutEditStage.GameDesign) throw new InvalidOperationException("请先确认实物校对结果。");
            return new LayoutReviewRecord(ReviewId,ScanId,original.Status,Copy(original.Bricks),Copy(verified),Copy(draft),
                Statistics,operations.ToArray(),confirmedAt,published);
        }

        private bool ValidDraft()
        {
            if(draft.Count==0) return Fail("DRAFT_EMPTY","请对照照片补加积木后再确认。");
            var cells=new HashSet<GridPoint>();
            foreach(var brick in draft)
            {
                if(!Inside(brick.C,brick.R)) return Fail("LAYOUT_OUT_OF_BOUNDS","有积木位于棋盘外，请选中并移动到空格。",brick);
                if(!cells.Add(new GridPoint(brick.C,brick.R))) return Fail("LAYOUT_COLLISION","有积木重叠，请选中其中一块并移动。",brick);
                if(!rules.ColorRoleMap.ContainsKey(brick.ColorId)) return Fail("UNMAPPED_COLOR","请为积木选择核心、城墙或防御塔。",brick);
            }
            LastIssue=null; return true;
        }
        private bool Available(int c,int r,string excluded)
        {
            if(!Inside(c,r)) return Fail("LAYOUT_OUT_OF_BOUNDS","请选择 10×10 棋盘内的格子。");
            foreach(var brick in draft) if(brick.BrickId!=excluded && brick.C==c && brick.R==r)
                return Fail("LAYOUT_COLLISION","目标格已有积木，请先移开或删除。");
            return true;
        }
        private int Index(string id)=>draft.FindIndex(b=>b.BrickId==id);
        private static bool Inside(int c,int r)=>c>=0 && r>=0 && c<10 && r<10;
        private bool Fail(string code,string message,BrickObservation brick=null)
        { LastIssue=new Issue(code,message,brick==null?(GridPoint?)null:new GridPoint(brick.C,brick.R)); return false; }
        private void SaveUndo()
        { history.Add(Copy(draft)); if(history.Count>64) history.RemoveAt(0); }
        private void Commit(string kind,string id)
        { LastIssue=null; operations.Add(new LayoutEditOperation(kind,id,Stage)); Changed?.Invoke(); }
        private static BrickObservation With(BrickObservation brick,int c,int r,string color)=>
            new BrickObservation(brick.BrickId,c,r,color,brick.Confidence,CopyEvidence(brick.Evidence));
        private static BrickObservation[] Copy(IReadOnlyList<BrickObservation> bricks)
        { var copy=new BrickObservation[bricks.Count]; for(int i=0;i<copy.Length;i++) copy[i]=With(bricks[i],bricks[i].C,bricks[i].R,bricks[i].ColorId); return copy; }
        private static ImageEvidence[] CopyEvidence(ImageEvidence[] values)
        {
            var copy=new ImageEvidence[values.Length];
            for(int i=0;i<copy.Length;i++) copy[i]=new ImageEvidence(values[i].FrameId,values[i].Center,(ImagePoint[])values[i].Region.Clone());
            return copy;
        }
        private static ScanResult CopyScan(ScanResult scan)
        {
            BoardObservation b=scan.Board;
            var board=b==null?null:new BoardObservation((ImagePoint[])b.Corners.Clone(),(double[])b.BoardToImage.Clone(),(int[])b.MarkerIds.Clone(),b.Confidence,b.IsReliable,b.CoordinateBasis);
            var issues=new Issue[scan.Issues.Length];
            for(int i=0;i<issues.Length;i++) { Issue issue=scan.Issues[i]; issues[i]=new Issue(issue.Code,issue.Message,issue.PhysicalCell,issue.FrameId,(ImagePoint[])issue.ImageRegion.Clone()); }
            return new ScanResult(scan.ScanId,scan.Status,(PhysicalCellObservation[])scan.PhysicalCells.Clone(),Copy(scan.Bricks),issues,board);
        }
        private static LayoutReviewStatistics Compare(BrickObservation[] detected,BrickObservation[] truth)
        {
            var originalById=new Dictionary<string,BrickObservation>(); foreach(var b in detected) originalById.Add(b.BrickId,b);
            int detectedMinC=int.MaxValue,detectedMinR=int.MaxValue,truthMinC=int.MaxValue,truthMinR=int.MaxValue;
            foreach(var b in detected) { detectedMinC=Math.Min(detectedMinC,b.C); detectedMinR=Math.Min(detectedMinR,b.R); }
            foreach(var b in truth) { truthMinC=Math.Min(truthMinC,b.C); truthMinR=Math.Min(truthMinR,b.R); }
            int matched=0,color=0,position=0;
            foreach(var b in truth)
                if(originalById.TryGetValue(b.BrickId,out var source))
                {
                    matched++; if(source.ColorId==b.ColorId) color++;
                    if(source.C-detectedMinC==b.C-truthMinC && source.R-detectedMinR==b.R-truthMinR) position++;
                }
            return new LayoutReviewStatistics(truth.Length,color,position,truth.Length-matched,detected.Length-matched,matched-color,matched-position);
        }
    }
}
