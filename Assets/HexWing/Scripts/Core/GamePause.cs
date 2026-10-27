using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// One place that decides whether game time runs: paused while any reason holds (the app lost focus
    /// or the headset is off, a menu is open). Menus, fades and the cockpit texts run on real time.
    /// </summary>
    public static class GamePause
    {
        [System.Flags]
        public enum Reason { None = 0, Focus = 1, Menu = 2 }

        static Reason _reasons;

        public static bool Paused => _reasons != Reason.None;
        public static bool Has(Reason r) => (_reasons & r) != 0;

        public static void Set(Reason r, bool on)
        {
            var before = _reasons;
            _reasons = on ? _reasons | r : _reasons & ~r;
            if (before == _reasons) return;
            Time.timeScale = Paused ? 0f : 1f;
            if (Paused) Haptics.StopAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _reasons = Reason.None;
    }
}
