using System;

namespace Brick2Life.Simulation
{
    /// <summary>
    /// Single source of truth for "bricks are read-only right now". A running battle and the
    /// showcase display modes both lock editing, and neither can accidentally unlock the other.
    /// </summary>
    public static class BrickEditGate
    {
        private static bool battleActive;
        private static bool displayOnly;

        public static bool BattleActive => battleActive;
        public static bool DisplayOnly => displayOnly;
        public static bool Locked => battleActive || displayOnly;

        public static event Action Changed;

        public static void SetBattle(bool value)
        {
            if (battleActive == value) return;
            battleActive = value;
            Changed?.Invoke();
        }

        public static void SetDisplay(bool value)
        {
            if (displayOnly == value) return;
            displayOnly = value;
            Changed?.Invoke();
        }
    }
}
