using System;

namespace BrickDefense.Core
{
    public enum LayoutEditStage { PhysicalReview, GameDesign }

    // 人工确认的单次记录；不是算法置信度或正式数据集的汇总结果。
    public sealed class LayoutReviewStatistics
    {
        public int ActualCount { get; }
        public int CorrectRecognition { get; }
        public int CorrectPosition { get; }
        public int Missed { get; }
        public int FalsePositive { get; }
        public int WrongColor { get; }
        public int WrongPosition { get; }
        public double RecognitionAccuracy => (double)CorrectRecognition/ActualCount;
        public double PositionAccuracy => (double)CorrectPosition/ActualCount;
        public LayoutReviewStatistics(int actual,int recognition,int position,int missed,int falsePositive,int wrongColor,int wrongPosition)
        { ActualCount=actual; CorrectRecognition=recognition; CorrectPosition=position; Missed=missed;
            FalsePositive=falsePositive; WrongColor=wrongColor; WrongPosition=wrongPosition; }
    }

    public sealed class LayoutEditOperation
    {
        public string Kind { get; }
        public string BrickId { get; }
        public LayoutEditStage Stage { get; }
        public DateTime AtUtc { get; }
        public LayoutEditOperation(string kind,string brickId,LayoutEditStage stage)
        { Kind=kind; BrickId=brickId; Stage=stage; AtUtc=DateTime.UtcNow; }
    }

    public sealed class LayoutReviewRecord
    {
        public string ReviewId { get; }
        public string ScanId { get; }
        public ResultStatus OriginalStatus { get; }
        public BrickObservation[] Original { get; }
        public BrickObservation[] Verified { get; }
        public BrickObservation[] Designed { get; }
        public LayoutReviewStatistics Statistics { get; }
        public LayoutEditOperation[] Operations { get; }
        public DateTime ConfirmedAtUtc { get; }
        public bool Published { get; }
        public LayoutReviewRecord(string id,string scanId,ResultStatus status,BrickObservation[] original,
            BrickObservation[] verified,BrickObservation[] designed,LayoutReviewStatistics statistics,
            LayoutEditOperation[] operations,DateTime confirmed,bool published)
        { ReviewId=id; ScanId=scanId; OriginalStatus=status; Original=original; Verified=verified; Designed=designed;
            Statistics=statistics; Operations=operations; ConfirmedAtUtc=confirmed; Published=published; }
    }
}
