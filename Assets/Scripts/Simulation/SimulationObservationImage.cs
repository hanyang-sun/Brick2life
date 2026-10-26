using System;

namespace Brick2Life.Simulation
{
    // Pixels are top-left RGB. The arrays are the public camera matrices, not ground truth.
    public sealed class SimulationObservationImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Rgb { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public double[] WorldToCamera { get; }
        public double[] Projection { get; }

        internal SimulationObservationImage(int width, int height, byte[] rgb, DateTimeOffset capturedAtUtc,
            double[] worldToCamera, double[] projection)
        {
            Width = width; Height = height; Rgb = rgb; CapturedAtUtc = capturedAtUtc;
            WorldToCamera = worldToCamera; Projection = projection;
        }
    }
}
