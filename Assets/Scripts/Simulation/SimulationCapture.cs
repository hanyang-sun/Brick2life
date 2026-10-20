using System;
using Brick2Life.Core;

namespace Brick2Life.Simulation
{
    // Evaluation/export envelope. Pass only the image plus agreed public calibration to B.
    public sealed class SimulationCapture
    {
        private readonly byte[] rgbPng;
        public string FrameId { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public int ImageWidth { get; }
        public int ImageHeight { get; }
        public BoardSnapshot GroundTruth { get; }
        public string GroundTruthJson { get; }
        internal SimulationCapture(string frameId, DateTimeOffset capturedAtUtc, int width, int height,
            byte[] png, BoardSnapshot truth, string json)
        {
            FrameId = frameId; CapturedAtUtc = capturedAtUtc; ImageWidth = width; ImageHeight = height;
            rgbPng = (byte[])png.Clone(); GroundTruth = truth; GroundTruthJson = json;
        }
        public byte[] CopyRgbPng() => (byte[])rgbPng.Clone();
    }
}
