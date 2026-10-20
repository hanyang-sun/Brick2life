using System;
using BrickDefense.Vision;
using UnityEngine;

namespace BrickDefense.Infrastructure
{
    // 沿用无格纸标定键及四颜色数组，已保存的配置可直接加载。
    public sealed class PhotoProfileStore
    {
        private readonly string profileKey;
        public PhotoProfileStore(string profileKey="BrickDefense.PhotoCalibration.plain.v1") { this.profileKey=profileKey; }

        public Color32[] LoadCalibration()
        {
            try
            {
                var stored=JsonUtility.FromJson<CalibrationData>(PlayerPrefs.GetString(profileKey,""));
                return stored?.colors!=null && stored.colors.Length==4?(Color32[])stored.colors.Clone():null;
            }
            catch (Exception) { PlayerPrefs.DeleteKey(profileKey); return null; }
        }
        public void SaveCalibration(Color32[] colors)
        {
            if (colors==null || colors.Length!=4) throw new ArgumentException("Four calibration colors are required.");
            PlayerPrefs.SetString(profileKey,JsonUtility.ToJson(new CalibrationData { colors=(Color32[])colors.Clone() }));
            PlayerPrefs.Save();
        }
        public void ResetCalibration() { PlayerPrefs.DeleteKey(profileKey); PlayerPrefs.Save(); }
        public static PhotoScanSettings ReadSettings()
        {
            var asset=Resources.Load<TextAsset>("photo-scan-settings");
            return asset==null?new PhotoScanSettings():JsonUtility.FromJson<PhotoScanSettingsData>(asset.text).ToSettings();
        }
    }

    [Serializable] internal sealed class CalibrationData { public Color32[] colors; }
    [Serializable] internal sealed class PhotoScanSettingsData
    {
        public int maximumDetectionSide=1200;
        public double maximumColorDistance=0.24,minimumColorMargin=0.08,
            minimumCalibrationSeparation=0.32,minimumCellPixels=18,minimumColorBrightness=0.12;
        public PhotoScanSettings ToSettings() => new PhotoScanSettings { MaximumDetectionSide=maximumDetectionSide,
            MaximumColorDistance=maximumColorDistance,MinimumColorMargin=minimumColorMargin,
            MinimumCalibrationSeparation=minimumCalibrationSeparation,MinimumCellPixels=minimumCellPixels,
            MinimumColorBrightness=minimumColorBrightness };
        public static PhotoScanSettingsData From(PhotoScanSettings s) => new PhotoScanSettingsData { maximumDetectionSide=s.MaximumDetectionSide,
            maximumColorDistance=s.MaximumColorDistance,minimumColorMargin=s.MinimumColorMargin,
            minimumCalibrationSeparation=s.MinimumCalibrationSeparation,minimumCellPixels=s.MinimumCellPixels,
            minimumColorBrightness=s.MinimumColorBrightness };
    }
}
