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
        // True when the virtual projection was left visible for this capture (interference sample).
        public bool ProjectionVisible { get; }

        internal SimulationObservationImage(int width, int height, byte[] rgb, DateTimeOffset capturedAtUtc,
            double[] worldToCamera, double[] projection, bool projectionVisible = false)
        {
            Width = width; Height = height; Rgb = rgb; CapturedAtUtc = capturedAtUtc;
            WorldToCamera = worldToCamera; Projection = projection; ProjectionVisible = projectionVisible;
        }
    }
}
