using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Lifetime stats and the cockpit colours they unlock (cosmetic only, nothing that changes balance).
    /// Kills by the player are counted from every bolt and missile hit; wins and the training come from
    /// the modes' own saved records. The chosen colour replaces the ship class's accent colour.
    /// </summary>
    public static class Progression
    {
        const string KillsKey = SaveKeys.Kills;
        const string ColorKey = SaveKeys.CockpitColor;

        public struct Paint
        {
            public string Name;
            public Color Color;
            public string Unlock;
            public System.Func<bool> Unlocked;
        }

        public static int TotalKills => PlayerPrefs.GetInt(KillsKey, 0);

        /// <summary>0 = the ship class's own colour.</summary>
        public static readonly Paint[] Paints =
        {
            new() { Name = "CLASS", Color = Color.clear, Unlock = "", Unlocked = () => true },
            new() { Name = "WHITE", Color = new Color(0.9f, 0.94f, 1f), Unlock = "TRAINING", Unlocked = () => PlayerPrefs.GetInt(SaveKeys.TrainingDone, 0) == 1 },
            new() { Name = "GREEN", Color = new Color(0.33f, 1f, 0.6f), Unlock = "25 KILLS", Unlocked = () => TotalKills >= 25 },
            new() { Name = "VIOLET", Color = new Color(0.7f, 0.55f, 1f), Unlock = "100 KILLS", Unlocked = () => TotalKills >= 100 },
            new() { Name = "BLUE", Color = new Color(0.3f, 0.66f, 1f), Unlock = "WAVE 10", Unlocked = () => PlayerPrefs.GetInt(SaveKeys.SurvivalBestWave, 0) >= 10 },
            new() { Name = "RED", Color = new Color(1f, 0.35f, 0.29f), Unlock = "ESCORT", Unlocked = () => PlayerPrefs.GetInt(SaveKeys.EscortWon, 0) == 1 },
            new() { Name = "SILVER", Color = new Color(0.82f, 0.86f, 0.92f), Unlock = "TEAM DOGFIGHT", Unlocked = () => PlayerPrefs.GetInt(SaveKeys.DogfightWon, 0) == 1 },
            new() { Name = "GOLD", Color = new Color(1f, 0.82f, 0.29f), Unlock = "ASSAULT", Unlocked = () => PlayerPrefs.GetInt(SaveKeys.AssaultWon, 0) == 1 },
        };

        public static int SelectedPaint
        {
            get
            {
                int i = Mathf.Clamp(PlayerPrefs.GetInt(ColorKey, 0), 0, Paints.Length - 1);
                return Paints[i].Unlocked() ? i : 0;
            }
        }

        /// <summary>The cockpit accent colour: the chosen paint, or the class colour.</summary>
        public static Color Accent(ShipDefinition def) => SelectedPaint == 0 || def == null ? (def != null ? def.accent : Color.cyan) : Paints[SelectedPaint].Color;

        public static int UnlockedCount
        {
            get { int n = 0; foreach (var p in Paints) if (p.Unlocked()) n++; return n; }
        }

        public static void NextPaint()
        {
            int i = SelectedPaint;
            for (int step = 1; step <= Paints.Length; step++)
            {
                int j = (i + step) % Paints.Length;
                if (!Paints[j].Unlocked()) continue;
                PlayerPrefs.SetInt(ColorKey, j);
                PlayerPrefs.Save();
                return;
            }
        }

        /// <summary>Counts the player's kills (hooked to the laser and missile systems at start).</summary>
        public static void Hook()
        {
            if (LaserSystem.Instance != null) LaserSystem.Instance.Hit += (t, p, killed, by) => Count(killed, by);
            if (MissileSystem.Instance != null) MissileSystem.Instance.Hit += (t, killed, by) => Count(killed, by);
        }

        static void Count(bool killed, Damageable by)
        {
            if (!killed || by == null || PlayerShip.Instance == null || by != PlayerShip.Instance.Health) return;
            PlayerPrefs.SetInt(KillsKey, TotalKills + 1);   // saved with the next PlayerPrefs.Save (mode ends, settings)
        }
    }
}
