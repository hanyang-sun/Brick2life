using System;
using System.Linq;
using Brick2Life.Core;
using Brick2Life.Vision;

internal static class VisionChecks
{
    private static readonly DateTimeOffset When = new DateTimeOffset(2026, 10, 23, 2, 0, 0, TimeSpan.Zero);

    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception(message);
        }
        CheckProjection(Check);
        CheckRecognition(Check);
        CheckUnknownColor(Check);
        CheckPipeline(Check);
        CheckTemporalConfirmation(Check);
        CheckRejection(Check);
        Console.WriteLine($"VISION_CHECKS_PASSED: {assertions} assertions.");
    }

    private static void CheckProjection(Action<bool, string> check)
    {
        CameraProjection camera = Nadir(320, 320);
        check(camera.TryProject(5, 1, 5, out double centerX, out double centerY), "Board center is behind the camera.");
        check(Math.Abs(centerX - 159.5) < 0.01 && Math.Abs(centerY - 159.5) < 0.01, "Top center did not project to the image center.");
        check(camera.TryProject(0.5, 1, 5, out double leftX, out _), "Left cell is behind the camera.");
        check(leftX < centerX, "World +X did not increase image X.");
        check(camera.TryProject(5, 1, 10, out _, out double farY), "Far cell is behind the camera.");
        check(farY < centerY, "World +Z did not move toward the top of the image.");
    }

    private static void CheckRecognition(Action<bool, string> check)
    {
        BoardDefinition definition = Board();
        VisionCalibration calibration = Palette();
        CameraProjection camera = Nadir(320, 320);
        (int x, int y, RgbColor color)[] bricks =
        {
            (0, 0, Swatch("red")), (9, 9, Swatch("blue")), (0, 9, Swatch("yellow")), (9, 0, Swatch("green")),
            (5, 5, Scale(Swatch("red"), 0.55)), (3, 3, Swatch("red")), (4, 3, Swatch("red"))
        };
        RgbImage image = Paint(camera, definition, calibration, bricks);
        FrameObservation observation = Recognize(definition, calibration, camera, image, "corners");
        BoardSnapshot truth = Truth(definition, 1,
            (0, 0, "red"), (9, 9, "blue"), (0, 9, "yellow"), (9, 0, "green"), (5, 5, "red"), (3, 3, "red"), (4, 3, "red"));
        RecognitionScore score = RecognitionScore.Compare(observation, truth);
        check(score.ExactMatches == 100, "Fixed grid missed cells: " + string.Join("; ", score.Mismatches));
        check(observation.Cells[definition.Index(3, 3)].State.ColorId == "red" && observation.Cells[definition.Index(4, 3)].State.ColorId == "red",
            "Adjacent bricks of the same color were merged.");

        RgbImage shifted = Paint(camera, definition, calibration, (9, 9, Swatch("blue")), (9, 0, Swatch("green")));
        FrameObservation afterRemoval = Recognize(definition, calibration, camera, shifted, "without-left");
        check(afterRemoval.Cells[definition.Index(9, 9)].State.ColorId == "blue", "Removing the leftmost brick shifted another brick.");
        check(afterRemoval.Cells[definition.Index(0, 0)].State.Occupancy == Occupancy.Empty, "Removed cell was still occupied.");
        check(afterRemoval.Cells[definition.Index(9, 0)].State.ColorId == "green", "A remaining corner brick changed identity.");
    }

    private static void CheckUnknownColor(Action<bool, string> check)
    {
        BoardDefinition definition = Board();
        VisionCalibration calibration = Palette();
        CameraProjection camera = Nadir(320, 320);
        RgbImage image = Paint(camera, definition, calibration, (2, 2, new RgbColor(180, 40, 180)));
        FrameObservation observation = Recognize(definition, calibration, camera, image, "purple");
        check(observation.Accepted, "An unknown color rejected the whole frame.");
        check(observation.Cells[definition.Index(2, 2)].State.Occupancy == Occupancy.Unknown, "Uncalibrated purple was forced into the palette.");
        check(observation.Cells[definition.Index(0, 0)].State.Occupancy == Occupancy.Empty, "A distant empty cell became occupied.");
    }

    private static void CheckPipeline(Action<bool, string> check)
    {
        BoardDefinition definition = Board();
        VisionCalibration calibration = Palette();
        CameraProjection camera = Nadir(320, 320);
        var tracker = new LayoutChangeTracker(3);
        FrameObservation first = Recognize(definition, calibration, camera, Paint(camera, definition, calibration, (1, 1, Swatch("red")), (8, 8, Swatch("blue"))), "base");
        LayoutTrackStep baseline = Feed(tracker, first, 3);
        check(baseline.EstablishedBaseline && tracker.Events.Count == 0, "The first stable frame invented add events.");
        check(RecognitionScore.Compare(first, Truth(definition, 1, (1, 1, "red"), (8, 8, "blue"))).ExactMatches == 100, "Baseline image was not exact.");

        FrameObservation added = Recognize(definition, calibration, camera, Paint(camera, definition, calibration,
            (1, 1, Swatch("red")), (8, 8, Swatch("blue")), (4, 4, Swatch("green"))), "add");
        check(tracker.Observe(added).NewEvents.Count == 0 && tracker.Observe(added).NewEvents.Count == 0, "An add was emitted before three agreeing frames.");
        LayoutTrackStep add = tracker.Observe(added);
        check(add.NewEvents.Count == 1 && add.NewEvents[0].Kind == LayoutEventKind.Add && add.NewEvents[0].Cell.Equals(new GridPoint(4, 4)) &&
            add.NewEvents[0].ColorId == "green" && add.NewEvents[0].Timestamp == When, "Add event did not record the cell, color and time.");

        FrameObservation removed = Recognize(definition, calibration, camera, Paint(camera, definition, calibration, (8, 8, Swatch("blue")), (4, 4, Swatch("green"))), "remove");
        Feed(tracker, removed, 2);
        LayoutTrackStep remove = tracker.Observe(removed);
        check(remove.NewEvents.Count == 1 && remove.NewEvents[0].Kind == LayoutEventKind.Remove && remove.NewEvents[0].Cell.Equals(new GridPoint(1, 1)) &&
            remove.NewEvents[0].ColorId == "red", "Remove event did not name the vacated cell.");

        FrameObservation moved = Recognize(definition, calibration, camera, Paint(camera, definition, calibration, (2, 6, Swatch("blue")), (4, 4, Swatch("green"))), "move");
        Feed(tracker, moved, 2);
        LayoutTrackStep move = tracker.Observe(moved);
        check(move.NewEvents.Count == 1 && move.NewEvents[0].Kind == LayoutEventKind.Move && move.NewEvents[0].From.Equals(new GridPoint(8, 8)) &&
            move.NewEvents[0].Cell.Equals(new GridPoint(2, 6)) && move.NewEvents[0].ColorId == "blue" && !move.NewEvents[0].Ambiguous,
            "A unique color change was not reported as a move.");
        check(tracker.Events.Count == 3, "Confirmed events were dropped or duplicated.");
    }

    private static void CheckTemporalConfirmation(Action<bool, string> check)
    {
        BoardDefinition definition = Board();
        var tracker = new LayoutChangeTracker(3);
        FrameObservation stable = Manual(definition, Cells((2, 2, "red")));
        Feed(tracker, stable, 3);
        FrameObservation flicker = Manual(definition, Cells((2, 2, "red"), (3, 3, "blue")));
        check(tracker.Observe(flicker).NewEvents.Count == 0, "One disagreeing frame emitted an event.");
        LayoutTrackStep restored = Feed(tracker, stable, 3);
        check(restored.NewEvents.Count == 0 && tracker.Events.Count == 0, "A cancelled flicker changed the baseline.");

        FrameObservation recolored = Manual(definition, Cells((2, 2, "blue")));
        LayoutTrackStep recolor = Feed(tracker, recolored, 3);
        check(recolor.NewEvents.Count == 2
            && recolor.NewEvents.Any(item => item.Kind == LayoutEventKind.Remove && item.ColorId == "red" && item.Cell.Equals(new GridPoint(2, 2)))
            && recolor.NewEvents.Any(item => item.Kind == LayoutEventKind.Add && item.ColorId == "blue" && item.Cell.Equals(new GridPoint(2, 2)))
            && recolor.NewEvents.All(item => !item.Ambiguous && item.Kind != LayoutEventKind.Move),
            "An in-place recolor was called a move.");

        var ambiguous = new LayoutChangeTracker(1);
        Feed(ambiguous, Manual(definition, Cells((0, 0, "red"), (1, 0, "red"))), 1);
        LayoutTrackStep swap = ambiguous.Observe(Manual(definition, Cells((5, 5, "red"), (6, 6, "red"))));
        check(swap.NewEvents.Count == 4 && swap.NewEvents.All(item => item.Ambiguous && item.Kind != LayoutEventKind.Move),
            "Two same-color relocations were reported as determined moves.");
        BoardSnapshot before = Truth(definition, 1, (0, 0, "red"), (1, 0, "red"));
        BoardSnapshot after = Truth(definition, 2, (5, 5, "red"), (6, 6, "red"));
        check(BoardDelta.Between(before, after).HasAmbiguousMovement, "Core delta did not mark the same-color pairing as ambiguous.");
    }

    private static void CheckRejection(Action<bool, string> check)
    {
        BoardDefinition definition = Board();
        VisionCalibration calibration = Palette();
        CameraProjection camera = Nadir(320, 320);
        var tiny = new RgbImage(16, 16, new byte[16 * 16 * 3]);
        FrameObservation rejected = FixedGridRecognizer.Recognize(definition, tiny, camera, calibration, "tiny", "session", 0, When);
        check(!rejected.Accepted && rejected.RejectionReason.Contains("尺寸"), "A tiny image was accepted.");
        RecognitionScore score = RecognitionScore.Compare(rejected, Truth(definition, 1));
        check(score.ExactMatches == 0 && score.Mismatches[0].Contains("拒绝"), "Rejected frame was scored as a layout.");
        bool threw = false;
        try { new RgbImage(2, 2, new byte[3]); }
        catch (ArgumentException) { threw = true; }
        check(threw, "A short pixel buffer was accepted.");
    }

    private static LayoutTrackStep Feed(LayoutChangeTracker tracker, FrameObservation observation, int times)
    {
        LayoutTrackStep step = null;
        for (int i = 0; i < times; i++) step = tracker.Observe(observation);
        return step;
    }

    private static FrameObservation Recognize(BoardDefinition definition, VisionCalibration calibration, CameraProjection camera, RgbImage image, string frame)
    {
        return FixedGridRecognizer.Recognize(definition, image, camera, calibration, frame, "session", 1, When);
    }

    private static FrameObservation Manual(BoardDefinition definition, BoardCell[] cells)
    {
        var observed = new ObservedCell[cells.Length];
        for (int i = 0; i < cells.Length; i++) observed[i] = new ObservedCell(cells[i]);
        return new FrameObservation(definition, "manual-" + Guid.NewGuid().ToString("N"), "session", 1, When, "test-calibration", observed);
    }

    private static BoardCell[] Cells(params (int x, int y, string color)[] bricks)
    {
        BoardDefinition definition = Board();
        var cells = new BoardCell[definition.CellCount];
        for (int i = 0; i < cells.Length; i++) cells[i] = BoardCell.Empty;
        foreach ((int x, int y, string color) brick in bricks)
            cells[definition.Index(brick.x, brick.y)] = new BoardCell(Occupancy.Occupied, brick.color);
        return cells;
    }

    private static BoardSnapshot Truth(BoardDefinition definition, long version, params (int x, int y, string color)[] bricks)
    {
        return new BoardSnapshot(definition, version, Cells(bricks), SnapshotSource.Manual);
    }

    private static RgbImage Paint(CameraProjection camera, BoardDefinition definition, VisionCalibration calibration, params (int x, int y, RgbColor color)[] bricks)
    {
        var pixels = new byte[camera.Width * camera.Height * 3];
        for (int i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = calibration.Background.R; pixels[i + 1] = calibration.Background.G; pixels[i + 2] = calibration.Background.B;
        }
        var image = new RgbImage(camera.Width, camera.Height, pixels);
        foreach ((int x, int y, RgbColor color) brick in bricks) FillCell(image, camera, definition, brick.x, brick.y, calibration.SurfaceY, brick.color);
        return image;
    }

    private static void FillCell(RgbImage image, CameraProjection camera, BoardDefinition definition, int gridX, int gridY, double surfaceY, RgbColor color)
    {
        var xs = new double[4];
        var ys = new double[4];
        for (int i = 0; i < 4; i++)
        {
            double u = i == 1 || i == 2 ? 1 : 0;
            double v = i >= 2 ? 1 : 0;
            if (!camera.TryProject((gridX + u) * definition.CellSize, surfaceY, (gridY + v) * definition.CellSize, out xs[i], out ys[i]))
                throw new InvalidOperationException("A brick top corner is outside the test camera.");
        }
        int minX = Clamp((int)Math.Floor(Math.Min(Math.Min(xs[0], xs[1]), Math.Min(xs[2], xs[3]))) - 1, 0, image.Width - 1);
        int maxX = Clamp((int)Math.Ceiling(Math.Max(Math.Max(xs[0], xs[1]), Math.Max(xs[2], xs[3]))) + 1, 0, image.Width - 1);
        int minY = Clamp((int)Math.Floor(Math.Min(Math.Min(ys[0], ys[1]), Math.Min(ys[2], ys[3]))) - 1, 0, image.Height - 1);
        int maxY = Clamp((int)Math.Ceiling(Math.Max(Math.Max(ys[0], ys[1]), Math.Max(ys[2], ys[3]))) + 1, 0, image.Height - 1);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (Inside(x, y, xs[0], ys[0], xs[1], ys[1], xs[2], ys[2]) || Inside(x, y, xs[0], ys[0], xs[2], ys[2], xs[3], ys[3]))
                    image.SetPixel(x, y, color.R, color.G, color.B);
    }

    private static bool Inside(double px, double py, double ax, double ay, double bx, double by, double cx, double cy)
    {
        double denominator = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
        if (Math.Abs(denominator) < 1e-8) return false;
        double u = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / denominator;
        double v = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / denominator;
        return u >= -0.02 && v >= -0.02 && u + v <= 1.02;
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? value : value;

    private static CameraProjection Nadir(int width, int height)
    {
        double[] view = { 1, 0, 0, -5, 0, 0, 1, -5, 0, 1, 0, -12, 0, 0, 0, 1 };
        double cotangent = 1.0 / Math.Tan(50.0 * Math.PI / 180.0 * 0.5);
        double near = 0.3, far = 50, aspect = (double)width / height;
        double[] projection =
        {
            cotangent / aspect, 0, 0, 0,
            0, cotangent, 0, 0,
            0, 0, (near + far) / (near - far), 2 * near * far / (near - far),
            0, 0, -1, 0
        };
        return new CameraProjection(width, height, view, projection);
    }

    private static BoardDefinition Board() => new BoardDefinition(colorIds: new[] { "red", "green", "blue", "yellow" });

    private static VisionCalibration Palette()
    {
        return new VisionCalibration("test-calibration", new RgbColor(170, 170, 170), new[]
        {
            new ColorSwatch("red", Swatch("red")), new ColorSwatch("green", Swatch("green")),
            new ColorSwatch("blue", Swatch("blue")), new ColorSwatch("yellow", Swatch("yellow"))
        });
    }

    private static RgbColor Swatch(string color)
    {
        switch (color)
        {
            case "red": return new RgbColor(224, 64, 64);
            case "green": return new RgbColor(64, 224, 64);
            case "blue": return new RgbColor(64, 64, 224);
            case "yellow": return new RgbColor(242, 191, 38);
            default: throw new ArgumentException(color);
        }
    }

    private static RgbColor Scale(RgbColor color, double factor)
    {
        return new RgbColor(ScaleByte(color.R, factor), ScaleByte(color.G, factor), ScaleByte(color.B, factor));
    }

    private static byte ScaleByte(byte value, double factor)
    {
        int scaled = (int)Math.Round(value * factor);
        if (scaled < 0) return 0;
        if (scaled > 255) return 255;
        return (byte)scaled;
    }
}
