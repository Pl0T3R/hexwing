using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player's own choices from the hangar menu (controls, comfort, haptics, volume, colour), kept in
    /// PlayerPrefs and laid over GameConfig at start. Only values the player changed are stored; everything
    /// else keeps the config's value. In the Editor the config asset is restored when play mode ends, so
    /// menu choices never change the asset.
    /// </summary>
    public static class UserSettings
    {
        const string Prefix = "HexWing.Setting.";

        public static readonly int[] TurnLimits = { 0, 90, 60, 45 };
        public static readonly float[] HapticLevels = { 1f, 0.5f, 0f };

#if UNITY_EDITOR
        static string _snapshot;
#endif

        public static float Volume { get; private set; } = 1f;

        /// <summary>Applies stored choices to the config (once at start).</summary>
        public static void Load()
        {
            var cfg = GameConfig.Instance;
#if UNITY_EDITOR
            if (_snapshot == null)
            {
                _snapshot = JsonUtility.ToJson(cfg);
                UnityEditor.EditorApplication.playModeStateChanged += RestoreOnExit;
            }
#endif
            cfg.controls.swapHands = GetBool("SwapHands", cfg.controls.swapHands);
            cfg.controls.invertPitch = GetBool("InvertPitch", cfg.controls.invertPitch);
            cfg.controls.sensitivity = PlayerPrefs.GetFloat(Prefix + "Sensitivity", cfg.controls.sensitivity);
            cfg.comfort.vignette = (VignetteLevel)PlayerPrefs.GetInt(Prefix + "Vignette", (int)cfg.comfort.vignette);
            cfg.comfort.autoLevel = GetBool("AutoLevel", cfg.comfort.autoLevel);
            cfg.comfort.turnRateLimit = PlayerPrefs.GetFloat(Prefix + "TurnLimit", cfg.comfort.turnRateLimit);
            cfg.combat.evasiveStyle = (EvasiveStyle)PlayerPrefs.GetInt(Prefix + "Evasive", (int)cfg.combat.evasiveStyle);
            cfg.haptics.master = PlayerPrefs.GetFloat(Prefix + "Haptics", cfg.haptics.master);
            Volume = PlayerPrefs.GetFloat(Prefix + "Volume", 1f);
            AudioListener.volume = Volume;
        }

#if UNITY_EDITOR
        static void RestoreOnExit(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode || _snapshot == null) return;
            JsonUtility.FromJsonOverwrite(_snapshot, GameConfig.Instance);
            _snapshot = null;
            UnityEditor.EditorApplication.playModeStateChanged -= RestoreOnExit;
        }
#endif

        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;
        static void SetBool(string key, bool value) { PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0); PlayerPrefs.Save(); }
        static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(Prefix + key, value); PlayerPrefs.Save(); }
        static void SetInt(string key, int value) { PlayerPrefs.SetInt(Prefix + key, value); PlayerPrefs.Save(); }

        // --- changes from the menu (each applies at once and is saved) ------------------------

        public static void ToggleSwapHands() { var c = GameConfig.Instance.controls; c.swapHands = !c.swapHands; SetBool("SwapHands", c.swapHands); }
        public static void ToggleInvertPitch() { var c = GameConfig.Instance.controls; c.invertPitch = !c.invertPitch; SetBool("InvertPitch", c.invertPitch); }

        public static void CycleSensitivity()
        {
            var c = GameConfig.Instance.controls;
            c.sensitivity = c.sensitivity >= 1.55f ? 0.6f : Mathf.Round((c.sensitivity + 0.2f) * 10f) / 10f;
            SetFloat("Sensitivity", c.sensitivity);
        }

        public static void CycleVignette()
        {
            var c = GameConfig.Instance.comfort;
            c.vignette = (VignetteLevel)(((int)c.vignette + 1) % 4);
            SetInt("Vignette", (int)c.vignette);
        }

        public static void ToggleAutoLevel() { var c = GameConfig.Instance.comfort; c.autoLevel = !c.autoLevel; SetBool("AutoLevel", c.autoLevel); }

        public static void CycleTurnLimit()
        {
            var c = GameConfig.Instance.comfort;
            int i = System.Array.IndexOf(TurnLimits, Mathf.RoundToInt(c.turnRateLimit));
            c.turnRateLimit = TurnLimits[(i + 1) % TurnLimits.Length];
            SetFloat("TurnLimit", c.turnRateLimit);
        }

        public static void CycleEvasive()
        {
            var c = GameConfig.Instance.combat;
            c.evasiveStyle = c.evasiveStyle == EvasiveStyle.Jink ? EvasiveStyle.Roll : EvasiveStyle.Jink;
            SetInt("Evasive", (int)c.evasiveStyle);
        }

        public static void CycleHaptics()
        {
            var h = GameConfig.Instance.haptics;
            int i = System.Array.IndexOf(HapticLevels, h.master);
            h.master = HapticLevels[(i + 1) % HapticLevels.Length];
            SetFloat("Haptics", h.master);
        }

        public static void CycleVolume()
        {
            Volume = Volume <= 0.05f ? 1f : Mathf.Round((Volume - 0.25f) * 4f) / 4f;
            AudioListener.volume = Volume;
            SetFloat("Volume", Volume);
        }

        /// <summary>Comfort presets: 0 = most comfortable, 1 = balanced, 2 = no aids.</summary>
        public static void ApplyComfortPreset(int preset)
        {
            var c = GameConfig.Instance.comfort;
            c.vignette = preset == 0 ? VignetteLevel.High : preset == 1 ? VignetteLevel.Medium : VignetteLevel.Off;
            c.autoLevel = preset < 2;
            c.turnRateLimit = preset == 0 ? 60f : 0f;
            GameConfig.Instance.combat.evasiveStyle = preset == 2 ? EvasiveStyle.Roll : EvasiveStyle.Jink;
            SetInt("Vignette", (int)c.vignette);
            SetBool("AutoLevel", c.autoLevel);
            SetFloat("TurnLimit", c.turnRateLimit);
            SetInt("Evasive", (int)GameConfig.Instance.combat.evasiveStyle);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Volume = 1f;
    }
}
