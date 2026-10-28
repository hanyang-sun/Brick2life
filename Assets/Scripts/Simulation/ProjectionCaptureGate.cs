using System;

namespace Brick2Life.Simulation
{
    /// <summary>
    /// A projection surface that can be hidden while the overhead camera takes a sample.
    /// Implemented by task C's projector; kept in the simulation assembly so the capture adapter
    /// never depends on the feedback assembly.
    /// </summary>
    public interface IProjectionGate
    {
        bool IsProjectionVisible { get; }
        void SetProjectionVisible(bool visible);
    }

    /// <summary>Single hand-off point between the projector and the capture adapter.</summary>
    public static class ProjectionCaptureGate
    {
        public static IProjectionGate Current { get; private set; }

        public static void Register(IProjectionGate gate)
        {
            if (gate != null) Current = gate;
        }

        public static void Unregister(IProjectionGate gate)
        {
            if (ReferenceEquals(Current, gate)) Current = null;
        }
    }
}
