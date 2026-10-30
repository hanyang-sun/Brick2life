using System;
using System.Collections.Generic;

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

    /// <summary>Hides registered projections for one synchronous capture, restoring each previous state.</summary>
    public static class ProjectionCaptureGate
    {
        private static readonly List<IProjectionGate> gates = new List<IProjectionGate>();
        public static IProjectionGate Current => gates.Count == 0 ? null : gates[gates.Count - 1];

        public static void Register(IProjectionGate gate)
        {
            if (gate != null && !gates.Contains(gate)) gates.Add(gate);
        }

        public static void Unregister(IProjectionGate gate)
        {
            gates.Remove(gate);
        }

        public static IDisposable Suppress() => new CaptureScope();

        private sealed class CaptureScope : IDisposable
        {
            private readonly List<IProjectionGate> hidden = new List<IProjectionGate>();
            public CaptureScope()
            {
                try
                {
                    foreach (IProjectionGate gate in gates.ToArray())
                        if (gate.IsProjectionVisible)
                        {
                            hidden.Add(gate);
                            gate.SetProjectionVisible(false);
                        }
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                for (int i = hidden.Count - 1; i >= 0; i--) hidden[i].SetProjectionVisible(true);
                hidden.Clear();
            }
        }
    }
}
