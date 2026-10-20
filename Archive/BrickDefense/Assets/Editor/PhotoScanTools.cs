using System;
using System.IO;
using System.Linq;
using BrickDefense.Core;
using BrickDefense.Infrastructure;
using BrickDefense.Vision;
using UnityEditor;
using UnityEngine;

namespace BrickDefense.Editor
{
    public static class PhotoScanTools
    {
        [MenuItem("Brick Defense/Verify Photo Integration")]
        public static void VerifyPhotoIntegration()
        {
            try
            {
                var imported=PhotoFileSource.Load(Path.Combine(Application.dataPath,"../Tests/Fixtures/real-unmarked-bricks-v1.jpg"));
                ImageFrame frame=imported.Frame;
                var settings=PhotoProfileStore.ReadSettings();
                var calibration=new ColorCalibration(new Rgb24(65,61,63),
                    new[] {new Rgb24(251,251,251),new Rgb24(253,97,48),new Rgb24(5,195,254)},
                    new[] {"physical-color-0","physical-color-1","physical-color-2"});
                ScanResult scan=PlainBackgroundRecognizer.Recognize(new CaptureSet("unity-image-check",frame),calibration,settings);
                if (scan.Status!=ResultStatus.Success || scan.Bricks.Length!=10 ||
                    scan.PhysicalCells.Count(c=>c.State==PhysicalCellState.Empty)!=90)
                    throw new Exception("Unity real-photo recognition failed.");
                LevelResult level=LevelBuilder.Build(GridMapper.Map(scan),calibration.CreateRules(),new GridPoint(1,18));
                if (level.Status!=ResultStatus.Success || level.Entities.Length!=10)
                    throw new Exception("Unity photo-to-level mapping failed.");
                Texture2D normalized=PhotoPixels.ToTexture(frame),decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    if (!decoded.LoadImage(normalized.EncodeToPNG())) throw new Exception("PNG decode failed.");
                    ImageFrame roundTrip=PhotoPixels.FromUnity("png-round-trip",decoded.GetPixels32(),decoded.width,decoded.height);
                    if (!roundTrip.Pixels.SequenceEqual(frame.Pixels)) throw new Exception("PNG round-trip changed pixels.");
                }
                finally { UnityEngine.Object.DestroyImmediate(normalized); UnityEngine.Object.DestroyImmediate(decoded); }
                Debug.Log("PHOTO_INTEGRATION_PASSED: real JPG decode, PNG round-trip, 10 bricks, 40 occupied cells and relative photo-to-level mapping.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }
    }
}
