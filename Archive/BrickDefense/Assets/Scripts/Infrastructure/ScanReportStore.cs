using System;
using System.IO;
using BrickDefense.Core;
using BrickDefense.Vision;
using UnityEngine;

namespace BrickDefense.Infrastructure
{
    public sealed class PhotoScanRecord
    {
        public ImageFrame Frame { get; set; }
        public ScanResult Result { get; set; }
        public PhotoScanSettings Settings { get; set; }
        public Color32[] Calibration { get; set; }
        public ImportedPhoto Imported { get; set; }
        public double ProcessingMilliseconds { get; set; }
    }

    // 保存原图、归一化图和识别证据，供人工核对与实拍回归使用。
    public sealed class ScanReportStore
    {
        private readonly string root;
        public ScanReportStore(string root) { this.root=root; }
        public string Save(PhotoScanRecord record)
        {
            ImageFrame frame=record.Frame; ScanResult scan=record.Result;
            string id=scan.ScanId;
            if (string.IsNullOrWhiteSpace(id) || id=="." || id==".." || id!=Path.GetFileName(id) || id.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                throw new ArgumentException("Invalid scan record ID.");
            // 扫描编号来自结果，但仍须校验为单个文件名，避免写出 Scans 目录。
            string directory=Path.Combine(root,"Scans",id); Directory.CreateDirectory(directory);
            Texture2D normalized=PhotoPixels.ToTexture(frame);
            try { File.WriteAllBytes(Path.Combine(directory,"photo.png"),normalized.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(normalized); }
            if (record.Imported!=null)
                File.WriteAllBytes(Path.Combine(directory,"source"+record.Imported.Extension),record.Imported.OriginalBytes);
            var report=new ScanReport
            { scanId=id,frameId=frame.FrameId,width=frame.Width,height=frame.Height,capturedAtUtc=frame.CapturedAtUtc.ToString("O"),
                status=scan.Status.ToString(),processingMilliseconds=record.ProcessingMilliseconds,
                calibration=new CalibrationData { colors=(Color32[])record.Calibration.Clone() },
                issues=new ReportIssue[scan.Issues.Length],cells=new ReportCell[scan.PhysicalCells.Length],
                bricks=new ReportBrick[scan.Bricks.Length],boardToImage=scan.Board?.BoardToImage,corners=Points(scan.Board?.Corners),
                markerIds=scan.Board?.MarkerIds,settings=PhotoScanSettingsData.From(record.Settings),
                viewfinder=Points(frame.Viewfinder),orientation=frame.Orientation,inputMode="PlainBackground",
                coordinateBasis=scan.Board?.CoordinateBasis.ToString(),direction=scan.Board?.Direction,boardIsReliable=scan.Board?.IsReliable??false };
            for (int i=0; i<report.issues.Length; i++)
            { Issue issue=scan.Issues[i]; report.issues[i]=new ReportIssue { code=issue.Code,message=issue.Message,frameId=issue.FrameId,
                c=issue.PhysicalCell?.X??-1,r=issue.PhysicalCell?.Y??-1,region=Points(issue.ImageRegion) }; }
            for (int i=0; i<report.cells.Length; i++)
            { var cell=scan.PhysicalCells[i]; report.cells[i]=new ReportCell { c=cell.C,r=cell.R,state=cell.State.ToString(),brickId=cell.BrickId,confidence=cell.Confidence }; }
            for (int i=0; i<report.bricks.Length; i++)
            {
                var brick=scan.Bricks[i]; ImageEvidence evidence=brick.Evidence.Length==0?null:brick.Evidence[0];
                report.bricks[i]=new ReportBrick { c=brick.C,r=brick.R,brickId=brick.BrickId,colorId=brick.ColorId,confidence=brick.Confidence,
                    frameId=evidence?.FrameId,center=evidence==null?null:Points(new[] {evidence.Center})[0],region=Points(evidence?.Region) };
            }
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(report,true)); return directory;
        }

        private static ReportPoint[] Points(ImagePoint[] values)
        {
            if (values==null) return Array.Empty<ReportPoint>();
            var points=new ReportPoint[values.Length];
            for (int i=0; i<values.Length; i++) points[i]=new ReportPoint { x=values[i].X,y=values[i].Y };
            return points;
        }
        [Serializable] private sealed class ScanReport
        { public string scanId,frameId,capturedAtUtc,status,orientation,inputMode,coordinateBasis,direction;
            public bool boardIsReliable; public int width,height; public double processingMilliseconds;
            public CalibrationData calibration; public ReportCell[] cells; public ReportBrick[] bricks; public ReportIssue[] issues;
            public double[] boardToImage; public ReportPoint[] corners,viewfinder; public int[] markerIds; public PhotoScanSettingsData settings; }
        [Serializable] private sealed class ReportCell { public int c,r; public string state,brickId; public double confidence; }
        [Serializable] private sealed class ReportBrick { public int c,r; public string brickId,colorId,frameId; public double confidence; public ReportPoint center; public ReportPoint[] region; }
        [Serializable] private sealed class ReportIssue { public string code,message,frameId; public int c,r; public ReportPoint[] region; }
        [Serializable] private sealed class ReportPoint { public double x,y; }
    }
}
