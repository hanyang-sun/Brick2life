using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brick2Life.Vision
{
    public readonly struct RgbColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public RgbColor(byte r, byte g, byte b) { R = r; G = g; B = b; }

        public static RgbColor FromUnit(float r, float g, float b)
        {
            return new RgbColor(ToByte(r), ToByte(g), ToByte(b));
        }

        private static byte ToByte(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            int rounded = (int)Math.Round(value * 255.0);
            if (rounded < 0) return 0;
            if (rounded > 255) return 255;
            return (byte)rounded;
        }
    }

    public sealed class ColorSwatch
    {
        public string Id { get; }
        public RgbColor Value { get; }
        public ColorSwatch(string id, RgbColor value)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Color ID is required.", nameof(id));
            Id = id; Value = value;
        }
    }

    // Public palette and the brick-top plane. Occupancy is not part of calibration.
    public sealed class VisionCalibration
    {
        public const double DefaultSurfaceY = 1;
        public const double DefaultInset = 0.45;
        public const int DefaultSamplesPerAxis = 3;

        public string CalibrationId { get; }
        public RgbColor Background { get; }
        public double SurfaceY { get; }
        public double Inset { get; }
        public int SamplesPerAxis { get; }
        public IReadOnlyList<ColorSwatch> Colors { get; }

        public VisionCalibration(string calibrationId, RgbColor background, IEnumerable<ColorSwatch> colors,
            double surfaceY = DefaultSurfaceY, double inset = DefaultInset, int samplesPerAxis = DefaultSamplesPerAxis)
        {
            if (string.IsNullOrWhiteSpace(calibrationId)) throw new ArgumentException("Calibration ID is required.", nameof(calibrationId));
            CameraProjection.CheckSampleLayout(surfaceY, inset, samplesPerAxis);
            var owned = new List<ColorSwatch>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (colors != null)
                foreach (ColorSwatch color in colors)
                {
                    if (color == null || !ids.Add(color.Id)) throw new ArgumentException("Color swatches must be unique.", nameof(colors));
                    owned.Add(color);
                }
            if (owned.Count == 0) throw new ArgumentException("At least one brick color is required.", nameof(colors));
            CalibrationId = calibrationId; Background = background; SurfaceY = surfaceY; Inset = inset; SamplesPerAxis = samplesPerAxis;
            Colors = new ReadOnlyCollection<ColorSwatch>(owned);
        }
    }
}
