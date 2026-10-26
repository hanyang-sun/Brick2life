using System;
using Brick2Life.Core;

namespace Brick2Life.Vision
{
    public readonly struct GridSample
    {
        public int GridX { get; }
        public int GridY { get; }
        public double PixelX { get; }
        public double PixelY { get; }
        public bool InFront { get; }
        public GridSample(int gridX, int gridY, double pixelX, double pixelY, bool inFront)
        {
            GridX = gridX; GridY = gridY; PixelX = pixelX; PixelY = pixelY; InFront = inFront;
        }
    }

    // Row-major matrices: clip = projection * worldToCamera * (x,y,z,1).
    // Pixel centers follow the capture JSON: x = viewportX * width - 0.5, y = (1 - viewportY) * height - 0.5.
    public sealed class CameraProjection
    {
        private readonly double[] worldToCamera;
        private readonly double[] projection;
        public int Width { get; }
        public int Height { get; }

        public CameraProjection(int width, int height, double[] worldToCamera, double[] projection)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            this.worldToCamera = CopyMatrix(worldToCamera);
            this.projection = CopyMatrix(projection);
            Width = width; Height = height;
        }

        public bool TryProject(double x, double y, double z, out double pixelX, out double pixelY)
        {
            double[] eye = Multiply(worldToCamera, x, y, z);
            double[] clip = Multiply(projection, eye[0], eye[1], eye[2]);
            pixelX = 0; pixelY = 0;
            if (clip[3] <= 1e-8 || double.IsNaN(clip[3]) || double.IsInfinity(clip[3])) return false;
            double ndcX = clip[0] / clip[3], ndcY = clip[1] / clip[3];
            if (double.IsNaN(ndcX) || double.IsInfinity(ndcX) || double.IsNaN(ndcY) || double.IsInfinity(ndcY)) return false;
            pixelX = (ndcX * 0.5 + 0.5) * Width - 0.5;
            pixelY = (1.0 - (ndcY * 0.5 + 0.5)) * Height - 0.5;
            return true;
        }

        public GridSample[][] BuildSamples(BoardDefinition definition, VisionCalibration calibration)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));
            return CreateSamples(definition, calibration, TryProject);
        }

        public static void CheckSampleLayout(double surfaceY, double inset, int samplesPerAxis)
        {
            if (double.IsNaN(surfaceY) || double.IsInfinity(surfaceY)) throw new ArgumentOutOfRangeException(nameof(surfaceY));
            if (double.IsNaN(inset) || double.IsInfinity(inset) || inset <= 0 || inset >= 0.5)
                throw new ArgumentOutOfRangeException(nameof(inset));
            if (samplesPerAxis < 1 || samplesPerAxis > 8) throw new ArgumentOutOfRangeException(nameof(samplesPerAxis));
        }

        public static void TopPoint(BoardDefinition definition, int gridX, int gridY, int sampleX, int sampleY,
            int samplesPerAxis, double inset, double surfaceY, out double worldX, out double worldY, out double worldZ)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.Contains(gridX, gridY)) throw new ArgumentOutOfRangeException(nameof(gridX));
            CheckSampleLayout(surfaceY, inset, samplesPerAxis);
            if (sampleX < 0 || sampleY < 0 || sampleX >= samplesPerAxis || sampleY >= samplesPerAxis)
                throw new ArgumentOutOfRangeException(nameof(sampleX));
            double u = samplesPerAxis == 1 ? 0.5 : inset + (1 - 2 * inset) * sampleX / (double)(samplesPerAxis - 1);
            double v = samplesPerAxis == 1 ? 0.5 : inset + (1 - 2 * inset) * sampleY / (double)(samplesPerAxis - 1);
            worldX = (gridX + u) * definition.CellSize;
            worldZ = (gridY + v) * definition.CellSize;
            worldY = surfaceY;
        }

        public static GridSample[][] CreateSamples(BoardDefinition definition, VisionCalibration calibration, Projector project)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));
            if (project == null) throw new ArgumentNullException(nameof(project));
            CheckSampleLayout(calibration.SurfaceY, calibration.Inset, calibration.SamplesPerAxis);
            var samples = new GridSample[definition.CellCount][];
            int count = calibration.SamplesPerAxis;
            for (int y = 0; y < definition.Height; y++)
                for (int x = 0; x < definition.Width; x++)
                {
                    var cell = new GridSample[count * count];
                    int index = 0;
                    for (int row = 0; row < count; row++)
                        for (int column = 0; column < count; column++)
                        {
                            TopPoint(definition, x, y, column, row, count, calibration.Inset, calibration.SurfaceY,
                                out double worldX, out double worldY, out double worldZ);
                            bool inFront = project(worldX, worldY, worldZ, out double pixelX, out double pixelY);
                            cell[index++] = new GridSample(x, y, pixelX, pixelY, inFront);
                        }
                    samples[definition.Index(x, y)] = cell;
                }
            return samples;
        }

        public delegate bool Projector(double x, double y, double z, out double pixelX, out double pixelY);

        private static double[] CopyMatrix(double[] matrix)
        {
            if (matrix == null || matrix.Length != 16) throw new ArgumentException("A projection matrix needs 16 row-major values.");
            var copy = new double[16];
            for (int i = 0; i < 16; i++)
            {
                if (double.IsNaN(matrix[i]) || double.IsInfinity(matrix[i])) throw new ArgumentException("Matrix values must be finite.");
                copy[i] = matrix[i];
            }
            return copy;
        }

        private static double[] Multiply(double[] matrix, double x, double y, double z)
        {
            return new[]
            {
                matrix[0] * x + matrix[1] * y + matrix[2] * z + matrix[3],
                matrix[4] * x + matrix[5] * y + matrix[6] * z + matrix[7],
                matrix[8] * x + matrix[9] * y + matrix[10] * z + matrix[11],
                matrix[12] * x + matrix[13] * y + matrix[14] * z + matrix[15]
            };
        }
    }
}
