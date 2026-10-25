using System;
using System.Collections.Generic;
using Brick2Life.Core;

namespace Brick2Life.Vision
{
    // Fixed-grid top-face classifier. It never reads a ground-truth snapshot.
    // References: docs/research/fixed-grid-recognition-github.md
    public static class FixedGridRecognizer
    {
        private const double MinimumBrightness = 0.045;
        private const double EmptySaturation = 0.16;
        private const double MaximumHueDistance = 20.0 * Math.PI / 180.0;
        private const double MinimumHueMargin = 8.0 * Math.PI / 180.0;
        private const double DecisionFraction = 0.62;

        public static FrameObservation Recognize(BoardDefinition definition, RgbImage image, CameraProjection camera,
            VisionCalibration calibration, string frameId, string sessionId, long sequence, DateTimeOffset capturedAtUtc)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));
            if (image.Width != camera.Width || image.Height != camera.Height)
                return Reject(definition, calibration, frameId, sessionId, sequence, capturedAtUtc, "图像与相机尺寸不一致。");
            GridSample[][] samples = camera.BuildSamples(definition, calibration);
            return Recognize(definition, image, samples, calibration, frameId, sessionId, sequence, capturedAtUtc);
        }

        public static FrameObservation Recognize(BoardDefinition definition, RgbImage image, GridSample[][] samples,
            VisionCalibration calibration, string frameId, string sessionId, long sequence, DateTimeOffset capturedAtUtc)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));
            if (string.IsNullOrWhiteSpace(frameId) || string.IsNullOrWhiteSpace(sessionId) || sequence < 0)
                throw new ArgumentException("Frame identity is required.");
            if (image.Width < 32 || image.Height < 32 || image.Width > 4096 || image.Height > 4096)
                return Reject(definition, calibration, frameId, sessionId, sequence, capturedAtUtc, "图像尺寸无效。");
            if (samples == null || samples.Length != definition.CellCount)
                return Reject(definition, calibration, frameId, sessionId, sequence, capturedAtUtc, "采样与棋盘不一致。");
            foreach (ColorSwatch swatch in calibration.Colors)
                if (!definition.AllowsColor(swatch.Id))
                    return Reject(definition, calibration, frameId, sessionId, sequence, capturedAtUtc, "标定颜色不在棋盘定义中。");

            var palette = new Swatch[calibration.Colors.Count];
            for (int i = 0; i < palette.Length; i++)
            {
                ColorSwatch swatch = calibration.Colors[i];
                palette[i] = new Swatch(swatch.Id, Hue(swatch.Value.R / 255.0, swatch.Value.G / 255.0, swatch.Value.B / 255.0));
            }

            var cells = new ObservedCell[definition.CellCount];
            int unseen = 0;
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = ClassifyCell(image, samples[index], palette, out bool sawBoard);
                if (!sawBoard) unseen++;
            }
            if (unseen > definition.CellCount / 2)
                return Reject(definition, calibration, frameId, sessionId, sequence, capturedAtUtc, "棋盘大部分不在画面内。");
            return new FrameObservation(definition, frameId, sessionId, sequence, capturedAtUtc, calibration.CalibrationId, cells);
        }

        private static ObservedCell ClassifyCell(RgbImage image, GridSample[] samples, Swatch[] palette, out bool sawBoard)
        {
            sawBoard = false;
            if (samples == null || samples.Length == 0)
                return new ObservedCell(BoardCell.Unknown, 0, "没有顶面采样。");
            int inside = 0, empty = 0, unknown = 0;
            var votes = new int[palette.Length];
            foreach (GridSample sample in samples)
            {
                if (!sample.InFront || !image.TryRead(sample.PixelX, sample.PixelY, out byte r, out byte g, out byte b)) continue;
                inside++;
                int color = ClassifyPixel(r, g, b, palette);
                if (color >= 0) votes[color]++;
                else if (color == -1) empty++;
                else unknown++;
            }
            sawBoard = inside > 0;
            if (inside < samples.Length * 0.6)
                return new ObservedCell(BoardCell.Unknown, inside == 0 ? 0 : (double)inside / samples.Length, "顶面采样超出画面。");
            int needed = (int)Math.Ceiling(inside * DecisionFraction);
            int best = 0;
            for (int i = 1; i < votes.Length; i++) if (votes[i] > votes[best]) best = i;
            int colorVotes = votes.Length == 0 ? 0 : votes[best];
            if (colorVotes >= needed && colorVotes > empty && colorVotes > unknown)
                return new ObservedCell(new BoardCell(Occupancy.Occupied, palette[best].Id), (double)colorVotes / samples.Length);
            if (empty >= needed && empty > colorVotes && empty > unknown)
                return new ObservedCell(BoardCell.Empty, (double)empty / samples.Length);
            return new ObservedCell(BoardCell.Unknown, (double)Math.Max(colorVotes, empty) / samples.Length, "格内颜色不稳定。");
        }

        // -1 empty, -2 unknown, otherwise palette index.
        private static int ClassifyPixel(byte r, byte g, byte b, Swatch[] palette)
        {
            double red = r / 255.0, green = g / 255.0, blue = b / 255.0;
            double brightness = Math.Max(red, Math.Max(green, blue));
            double min = Math.Min(red, Math.Min(green, blue));
            if (brightness < MinimumBrightness) return -2;
            if (brightness <= 0 || (brightness - min) / brightness < EmptySaturation) return -1;
            double hue = Hue(red, green, blue);
            int best = -1, second = -1;
            double bestDistance = double.MaxValue, secondDistance = double.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                double distance = HueDistance(hue, palette[i].Hue);
                if (distance < bestDistance) { secondDistance = bestDistance; second = best; bestDistance = distance; best = i; }
                else if (distance < secondDistance) { secondDistance = distance; second = i; }
            }
            if (best < 0 || bestDistance > MaximumHueDistance) return -2;
            if (second >= 0 && secondDistance - bestDistance < MinimumHueMargin) return -2;
            return best;
        }

        // White-light scaling keeps hue, so a bright or shadowed top still matches its palette color.
        private static double Hue(double red, double green, double blue)
        {
            return Math.Atan2(Math.Sqrt(3) * (green - blue), 2 * red - green - blue);
        }

        private static double HueDistance(double first, double second)
        {
            double difference = Math.Abs(first - second);
            return difference > Math.PI ? 2 * Math.PI - difference : difference;
        }

        private static FrameObservation Reject(BoardDefinition definition, VisionCalibration calibration, string frameId,
            string sessionId, long sequence, DateTimeOffset capturedAtUtc, string reason)
        {
            return new FrameObservation(definition, frameId, sessionId, sequence, capturedAtUtc, calibration.CalibrationId, null, false, reason);
        }

        private sealed class Swatch
        {
            public readonly string Id;
            public readonly double Hue;
            public Swatch(string id, double hue) { Id = id; Hue = hue; }
        }
    }
}
