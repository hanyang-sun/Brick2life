using System;

namespace Brick2Life.Simulation
{
    // The editor supplies focus ownership; Player and headless tools allow input by default.
    public static class SimulationEditAccess
    {
        private static Func<bool> focusProvider;
        private static bool previous = true;
        public static bool CanEdit => focusProvider == null || focusProvider();
        public static event Action<bool> FocusChanged;

        public static void ConfigureFocus(Func<bool> provider)
        {
            focusProvider = provider;
            Refresh();
        }

        public static void Refresh()
        {
            bool allowed = CanEdit;
            if (allowed == previous) return;
            previous = allowed;
            FocusChanged?.Invoke(allowed);
        }
    }
}
