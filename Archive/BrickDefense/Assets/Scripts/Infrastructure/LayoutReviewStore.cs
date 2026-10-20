using System;
using System.IO;
using BrickDefense.Core;
using UnityEngine;

namespace BrickDefense.Infrastructure
{
    public sealed class LayoutReviewStore
    {
        private readonly string root;
        public LayoutReviewStore(string root) { this.root=root; }
        public string Save(LayoutReviewRecord record)
        {
            CheckName(record.ScanId); CheckName(record.ReviewId);
            string directory=Path.Combine(root,"Scans",record.ScanId); Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"review-"+record.ReviewId+".json");
            var stats=record.Statistics;
            var data=new ReviewData { schemaVersion=1,scanId=record.ScanId,reviewId=record.ReviewId,
                originalStatus=record.OriginalStatus.ToString(),confirmedAtUtc=record.ConfirmedAtUtc.ToString("O"),
                published=record.Published,manuallyVerified=true,coordinateConvention="relative: each layout min column=1, min row=1; no rotation fitting",
                original=Bricks(record.Original),verified=Bricks(record.Verified),designed=Bricks(record.Designed),
                statistics=new Statistics { actual=stats.ActualCount,correctRecognition=stats.CorrectRecognition,
                    correctPosition=stats.CorrectPosition,missed=stats.Missed,falsePositive=stats.FalsePositive,
                    wrongColor=stats.WrongColor,wrongPosition=stats.WrongPosition,
                    recognitionAccuracy=stats.RecognitionAccuracy,positionAccuracy=stats.PositionAccuracy },
                operations=new Operation[record.Operations.Length] };
            for(int i=0;i<data.operations.Length;i++)
            { var op=record.Operations[i]; data.operations[i]=new Operation { kind=op.Kind,brickId=op.BrickId,stage=op.Stage.ToString(),atUtc=op.AtUtc.ToString("O") }; }
            string pending=path+".tmp";
            File.WriteAllText(pending,JsonUtility.ToJson(data,true));
            if(File.Exists(path)) File.Replace(pending,path,null); else File.Move(pending,path);
            return path;
        }
        private static void CheckName(string name)
        {
            if(string.IsNullOrWhiteSpace(name) || name=="." || name==".." || name!=Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                throw new ArgumentException("Invalid review record ID.");
        }
        private static Brick[] Bricks(BrickObservation[] source)
        {
            var items=new Brick[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                var brick=source[i]; var evidence=new Evidence[brick.Evidence.Length];
                for(int j=0;j<evidence.Length;j++)
                {
                    var e=brick.Evidence[j]; var region=new Point[e.Region.Length];
                    for(int k=0;k<region.Length;k++) region[k]=new Point { x=e.Region[k].X,y=e.Region[k].Y };
                    evidence[j]=new Evidence { frameId=e.FrameId,center=new Point {x=e.Center.X,y=e.Center.Y},region=region };
                }
                items[i]=new Brick { brickId=brick.BrickId,c=brick.C,r=brick.R,colorId=brick.ColorId,confidence=brick.Confidence,evidence=evidence };
            }
            return items;
        }
        [Serializable] private sealed class ReviewData
        { public int schemaVersion; public string scanId,reviewId,originalStatus,confirmedAtUtc,coordinateConvention;
            public bool published,manuallyVerified; public Brick[] original,verified,designed; public Statistics statistics; public Operation[] operations; }
        [Serializable] private sealed class Statistics
        { public int actual,correctRecognition,correctPosition,missed,falsePositive,wrongColor,wrongPosition; public double recognitionAccuracy,positionAccuracy; }
        [Serializable] private sealed class Brick
        { public string brickId,colorId; public int c,r; public double confidence; public Evidence[] evidence; }
        [Serializable] private sealed class Evidence { public string frameId; public Point center; public Point[] region; }
        [Serializable] private sealed class Point { public double x,y; }
        [Serializable] private sealed class Operation { public string kind,brickId,stage,atUtc; }
    }
}
