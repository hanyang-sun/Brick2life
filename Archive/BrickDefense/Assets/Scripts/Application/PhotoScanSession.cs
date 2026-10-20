using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BrickDefense.Core;
using BrickDefense.Vision;

namespace BrickDefense.Workflow
{
    // 管理无格纸照片的分析生命周期；只有与当前图像和标定一致的结果可确认。
    public sealed class PhotoScanSession
    {
        private int revision;
        private ColorCalibration analyzedCalibration;
        public ImageFrame Frame { get; private set; }
        public ScanResult Result { get; private set; }
        public string ScanId { get; private set; }
        public bool IsBusy { get; private set; }
        public double ProcessingMilliseconds { get; private set; }
        public PhotoScanSettings Settings { get; }
        public PhotoScanSession(PhotoScanSettings settings) { Settings=settings??throw new ArgumentNullException(nameof(settings)); }
        public bool CalibrationReady(ColorCalibration calibration) =>
            PlainBackgroundRecognizer.CalibrationValid(calibration,Settings);
        public Rgb24 SampleColor(int x,int y)
        {
            if (Frame==null) throw new InvalidOperationException("No photo is loaded.");
            return ColorCalibration.SamplePatch(Frame,x,y);
        }

        public void SetFrame(ImageFrame frame)
        {
            Frame=frame??throw new ArgumentNullException(nameof(frame));
            ScanId="scan-"+Guid.NewGuid().ToString("N"); Invalidate();
        }
        // 输入改变即递增版本，旧异步任务完成后也不能覆盖新的扫描状态。
        public void Invalidate() { revision++; Result=null; analyzedCalibration=null; }

        public async Task<ScanResult> AnalyzeAsync(ColorCalibration calibration)
        {
            if (IsBusy || Frame == null) throw new InvalidOperationException("A photo must be ready and idle.");
            if (!CalibrationReady(calibration)) throw new InvalidOperationException("Color calibration is incomplete.");
            Invalidate(); int version=revision; IsBusy=true;
            // 冻结分析时的标定，避免用户随后改色造成“结果与角色规则不一致”。
            var frozen = new ColorCalibration(calibration.Background,(Rgb24[])calibration.BrickColors.Clone(),(string[])calibration.ColorIds.Clone());
            var capture = new CaptureSet(ScanId,Frame); var timer=Stopwatch.StartNew();
            try
            {
                ScanResult result=await Task.Run(() => PlainBackgroundRecognizer.Recognize(capture,frozen,Settings));
                if (revision == version) { Result=result; analyzedCalibration=frozen; ProcessingMilliseconds=timer.Elapsed.TotalMilliseconds; }
                return result;
            }
            finally { IsBusy=false; }
        }

        public LayoutDefinition ConfirmLayout()
        {
            if (IsBusy || Result?.Status != ResultStatus.Success || analyzedCalibration == null)
                throw new InvalidOperationException("Only a completed successful scan can be confirmed.");
            return new LayoutDefinition(Result,analyzedCalibration.CreateRules(),LayoutSource.Photograph);
        }

        public LayoutEditSession BeginReview()
        {
            if(IsBusy || Result==null || analyzedCalibration==null)
                throw new InvalidOperationException("请先完成当前照片的分析。");
            return new LayoutEditSession(Result,analyzedCalibration.CreateRules());
        }
    }
}
