namespace BrickDefense.Core
{
    public readonly struct ImagePoint
    {
        public double X { get; }
        public double Y { get; }
        public ImagePoint(double x, double y) { X = x; Y = y; }
    }

    public readonly struct Rgb24
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public Rgb24(byte r, byte g, byte b) { R = r; G = g; B = b; }
        public int Grey => (299 * R + 587 * G + 114 * B + 500) / 1000;
    }

    // 当前照片只得到按积木边长估算的相对格位；保留原数值供记录兼容。
    public enum LayoutCoordinateBasis { RelativeBrickScale = 1 }

    // BoardToImage 记录相对 10×10 格到原图的估算关系；MarkerIds 保留报告字段。
    public sealed class BoardObservation
    {
        public ImagePoint[] Corners { get; }
        public double[] BoardToImage { get; }
        public int[] MarkerIds { get; }
        public double Confidence { get; }
        public bool IsReliable { get; }
        public LayoutCoordinateBasis CoordinateBasis { get; }
        public string Direction => "image-top-left; relative brick-scale layout";
        public BoardObservation(ImagePoint[] corners, double[] transform, int[] ids,
            double confidence, bool reliable, LayoutCoordinateBasis coordinateBasis = LayoutCoordinateBasis.RelativeBrickScale)
        { Corners = corners; BoardToImage = transform; MarkerIds = ids;
            Confidence = confidence; IsReliable = reliable; CoordinateBasis = coordinateBasis; }
    }

    public sealed class ImageEvidence
    {
        public string FrameId { get; }
        public ImagePoint Center { get; }
        public ImagePoint[] Region { get; }
        public ImageEvidence(string frameId, ImagePoint center, ImagePoint[] region)
        { FrameId = frameId; Center = center; Region = region; }
    }
}
