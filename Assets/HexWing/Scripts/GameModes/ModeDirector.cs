using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>A playable mode (rings course, drone range, later survival and the others).</summary>
    public interface IGameMode
    {
        string Title { get; }
        /// <summary>Where the player starts.</summary>
        Pose StartPose { get; }
        /// <summary>Called with the view black: set the mode up from scratch.</summary>
        void ResetRun();
    }

    /// <summary>
    /// Runs one mode at a time (each on its own child object, switched on and off) and every restart:
    /// fade out, clear bolts, missiles and effects, put the player back at the mode's start, reset the
    /// mode, fade in. The left menu button restarts; holding it for a second switches to the next mode.
    /// The last mode played is remembered.
    /// </summary>
    public class ModeDirector : MonoBehaviour
    {
        const string ModeKey = "HexWing.Mode";
        const float SwitchHold = 1f;

        public static ModeDirector Instance { get; private set; }

        public IGameMode Current { get; private set; }
        public string LastReason { get; private set; } = "";
        public bool Restarting => _restarting;

        MonoBehaviour[] _modes;
        int _index;
        bool _restarting;
        float _menuHeld;

        void Awake()
        {
            Instance = this;
            var found = GetComponentsInChildren<MonoBehaviour>(true);
            var list = new System.Collections.Generic.List<MonoBehaviour>();
            foreach (var m in found) if (m is IGameMode) list.Add(m);
            _modes = list.ToArray();
            _index = Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, Mathf.Max(0, _modes.Length - 1));
            for (int i = 0; i < _modes.Length; i++) _modes[i].gameObject.SetActive(false);
        }

        IEnumerator Start()
        {
            yield return null;   // the player ship and systems are set up in their Awake/Start
            if (_modes.Length == 0) { Debug.LogError("[HexWing] No game modes under ModeDirector. Run HexWing > Build Game."); yield break; }
            Activate(_index);
            MapTheme.Apply(Current is IMapMode first ? first.Map : Map.Orbit);
            PlayerShip.Instance?.Respawn(Current.StartPose);
            Current.ResetRun();
        }

        void Update()
        {
            if (OVRInput.Get(OVRInput.Button.Start, OVRInput.Controller.LTouch))
            {
                _menuHeld += Time.unscaledDeltaTime;
                if (_menuHeld >= SwitchHold)
                {
                    _menuHeld = float.NegativeInfinity;   // once per press
                    Switch();
                }
            }
            else
            {
                if (_menuHeld > 0f) Restart("przycisk menu");   // a short press
                _menuHeld = 0f;
            }
        }

        public void Switch()
        {
            if (_modes.Length < 2) return;
            Restart("zmiana trybu", (_index + 1) % _modes.Length);
        }

        public void Restart(string reason) => Restart(reason, _index);

        void Restart(string reason, int modeIndex)
        {
            if (_restarting || _modes.Length == 0) return;
            StartCoroutine(RestartRoutine(reason, modeIndex));
        }

        IEnumerator RestartRoutine(string reason, int modeIndex)
        {
            _restarting = true;
            LastReason = reason;
            Debug.Log($"[HexWing] Restart ({reason}).");
            var fader = ScreenFader.Instance;
            float t = GameConfig.Instance.comfort.fadeSeconds;
            if (fader != null) yield return fader.FadeOut(t);
            LaserSystem.Instance?.ClearAll();
            MissileSystem.Instance?.ClearAll();
            Effects.Instance?.ClearAll();
            if (modeIndex != _index) Activate(modeIndex);
            MapTheme.Apply(Current is IMapMode mapped ? mapped.Map : Map.Orbit);
            PlayerShip.Instance?.Respawn(Current.StartPose);
            Current.ResetRun();
            yield return null;
            if (fader != null) yield return fader.FadeIn(t);
            _restarting = false;
        }

        /// <summary>
        /// Brings the player back at a pose behind a fade without resetting the mode (reinforcements in
        /// the objective modes). Bolts and missiles near the player are not cleared; the mode decides when.
        /// </summary>
        public void RespawnPlayer(Pose pose, float delay)
        {
            if (_restarting) return;
            StartCoroutine(RespawnRoutine(pose, delay));
        }

        IEnumerator RespawnRoutine(Pose pose, float delay)
        {
            _restarting = true;
            yield return new WaitForSeconds(delay);
            var fader = ScreenFader.Instance;
            float t = GameConfig.Instance.comfort.fadeSeconds;
            if (fader != null) yield return fader.FadeOut(t);
            MissileSystem.Instance?.BreakLocks(PlayerShip.Instance != null ? PlayerShip.Instance.Health : null);
            PlayerShip.Instance?.Respawn(pose);
            yield return null;
            if (fader != null) yield return fader.FadeIn(t);
            _restarting = false;
        }

        void Activate(int index)
        {
            for (int i = 0; i < _modes.Length; i++) _modes[i].gameObject.SetActive(i == index);
            _index = index;
            Current = (IGameMode)_modes[index];
            PlayerPrefs.SetInt(ModeKey, index);
            PlayerPrefs.Save();
            Debug.Log($"[HexWing] Mode: {Current.Title}");
        }
    }
}
