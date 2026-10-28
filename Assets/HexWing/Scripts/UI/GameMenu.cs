using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The menus, on the holographic <see cref="MenuPanel"/>:
    /// - Hangar (at start and from the pause menu): start, mode, ship, colour, controls, comfort, stats.
    /// - Pause (the left menu button while flying): resume, restart, hangar, controls, comfort.
    /// - Controls, comfort and stats pages, each with a way back.
    /// Game time stops while a menu is open. Choices apply at once and are saved.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static GameMenu Instance { get; private set; }

        enum Page { Hangar, Pause, Controls, Comfort, Stats }

        public bool Open => _panel != null && _panel.Visible;
        public bool InHangar { get; private set; }

        MenuPanel _panel;
        PlayerInput _input;
        Page _page, _back;
        /// <summary>A started run is waiting behind the menu (so the hangar can go back to it).</summary>
        bool _runInProgress;
        bool _wasRestarting;

        void Awake() => Instance = this;

        void Start()
        {
            _panel = GetComponent<MenuPanel>();
            _input = GetComponent<PlayerInput>();
            if (GetComponent<HangarShowcase>() == null) gameObject.AddComponent<HangarShowcase>();   // the ship on its pad in the hangar
        }

        void Update()
        {
            // mode and ship changes apply after their fade: show the new labels once it's done
            bool restarting = ModeDirector.Instance != null && ModeDirector.Instance.Restarting;
            if (_wasRestarting && !restarting && Open) _panel.Refresh();
            _wasRestarting = restarting;

            if (!OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch) || restarting) return;
            if (!Open) ShowPause();
            else if (_page == Page.Pause) Close();
            else if (_page is Page.Controls or Page.Comfort or Page.Stats) Show(_back);
            else if (_page == Page.Hangar && _runInProgress) ShowPause();
        }

        /// <summary>Opens the hangar (called once the first mode is set up, and from the pause menu).</summary>
        public void ShowHangar()
        {
            InHangar = true;
            Show(Page.Hangar);
        }

        void ShowPause()
        {
            InHangar = false;
            Show(Page.Pause);
        }

        void Show(Page page)
        {
            _page = page;
            GamePause.Set(GamePause.Reason.Menu, true);
            if (_panel == null) _panel = GetComponent<MenuPanel>();
            switch (page)
            {
                case Page.Hangar: _panel.Show("HANGAR", HangarItems()); break;
                case Page.Pause: _panel.Show("PAUSED", PauseItems()); break;
                case Page.Controls: _panel.Show("CONTROLS", ControlItems()); break;
                case Page.Comfort: _panel.Show("COMFORT", ComfortItems()); break;
                default: _panel.Show("STATS", StatItems()); break;
            }
        }

        void Close()
        {
            _panel.Hide();
            InHangar = false;
            GamePause.Set(GamePause.Reason.Menu, false);
            _input?.SuppressUntilReleased();   // the trigger or A that chose the item must not fire or use an ability
        }

        void StartRun(string reason)
        {
            Close();
            _runInProgress = true;
            ModeDirector.Instance?.Restart(reason);
        }

        bool Busy => ModeDirector.Instance != null && ModeDirector.Instance.Restarting;

        void SubPage(Page page)
        {
            _back = _page;
            Show(page);
        }

        // --- pages ------------------------------------------------------------------------------------

        List<MenuEntry> HangarItems()
        {
            var d = ModeDirector.Instance;
            var player = PlayerShip.Instance;
            var items = new List<MenuEntry>();
            items.Add(new MenuEntry(() => "START: " + (d != null ? d.Current.Title : "?"), () => { if (!Busy) StartRun("start"); }).AsPrimary());
            if (_runInProgress) items.Add(new MenuEntry("BACK TO GAME", Close));
            items.AddRange(new List<MenuEntry>
            {
                new(() => "MODE: " + (d != null ? d.Current.Title : "?"), () =>
                {
                    if (Busy) return;
                    _runInProgress = false;   // switching resets the mode
                    d?.SelectNextMode();
                }),
                new(() => "SHIP: " + (player != null && player.Definition != null ? player.Definition.displayName : "?"), () =>
                {
                    if (Busy) return;
                    _runInProgress = false;
                    player?.SelectNextShip();
                }),
                new(() => "COLOUR: " + Progression.Paints[Progression.SelectedPaint].Name + "  (" + Progression.UnlockedCount + "/" + Progression.Paints.Length + ")",
                    () => { Progression.NextPaint(); player?.RefreshColor(); }),
                new("CONTROLS", () => SubPage(Page.Controls)),
                new("COMFORT", () => SubPage(Page.Comfort)),
                new("STATS", () => SubPage(Page.Stats)),
                new("QUIT GAME", Application.Quit),
            });
            return items;
        }

        List<MenuEntry> PauseItems()
        {
            return new List<MenuEntry>
            {
                new MenuEntry("RESUME", Close).AsPrimary(),
                new("RESTART", () => { if (!Busy) StartRun("restart from the menu"); }),
                new("HANGAR", ShowHangar),
                new("CONTROLS", () => SubPage(Page.Controls)),
                new("COMFORT", () => SubPage(Page.Comfort)),
                new("RECENTER VIEW", () => SeatedRecenter.Instance?.Recenter(withFade: false)),
            };
        }

        List<MenuEntry> ControlItems()
        {
            var c = GameConfig.Instance.controls;
            return new List<MenuEntry>
            {
                new(() => "SCHEME: " + (_input != null ? PlayerInput.SchemeLabel(_input.Scheme) : "?"),
                    () => _input?.SetScheme((ControlScheme)(((int)_input.Scheme + 1) % 3))),
                new(() => "FLYING HAND: " + (c.swapHands ? "LEFT" : "RIGHT"), UserSettings.ToggleSwapHands),
                new(() => "INVERT UP/DOWN: " + (c.invertPitch ? "ON" : "OFF"), UserSettings.ToggleInvertPitch),
                new(() => "INVERT LEFT/RIGHT: " + (c.invertYaw ? "ON" : "OFF"), UserSettings.ToggleInvertYaw),
                new(() => "INVERT ROLL: " + (c.invertRoll ? "ON" : "OFF"), UserSettings.ToggleInvertRoll),
                new(() => "STICK SIDEWAYS: " + (c.sideways == StickSideways.Yaw ? "TURN" : "ROLL"), UserSettings.ToggleSideways),
                new(() => "THROTTLE STICK: " + (c.throttleStick == ThrottleStickMode.Hold ? "HOLDS" : "SPRINGS BACK"), UserSettings.ToggleThrottleStick),
                new(() => "SENSITIVITY: " + c.sensitivity.ToString("0.0"), UserSettings.CycleSensitivity),
                new(() => "VIBRATION: " + Percent(GameConfig.Instance.haptics.master), UserSettings.CycleHaptics),
                new(() => "VOLUME: " + Percent(UserSettings.Volume), UserSettings.CycleVolume),
                new("BACK", () => Show(_back)),
            };
        }

        List<MenuEntry> ComfortItems()
        {
            var c = GameConfig.Instance.comfort;
            return new List<MenuEntry>
            {
                new("MOST COMFORTABLE", () => UserSettings.ApplyComfortPreset(0)),
                new("BALANCED", () => UserSettings.ApplyComfortPreset(1)),
                new("NO AIDS", () => UserSettings.ApplyComfortPreset(2)),
                new(() => "VIGNETTE: " + Vignette(c.vignette), UserSettings.CycleVignette),
                new(() => "AUTO-LEVEL: " + (c.autoLevel ? "ON" : "OFF"), UserSettings.ToggleAutoLevel),
                new(() => "BANK INTO TURNS: " + (c.bankIntoTurns ? "ON" : "OFF"), UserSettings.ToggleBankIntoTurns),
                new(() => "TURN LIMIT: " + (c.turnRateLimit > 0f ? Mathf.RoundToInt(c.turnRateLimit) + " °/s" : "NONE"), UserSettings.CycleTurnLimit),
                new(() => "EVADE: " + (GameConfig.Instance.combat.evasiveStyle == EvasiveStyle.Jink ? "SIDE JINK" : "BARREL ROLL"), UserSettings.CycleEvasive),
                new("BACK", () => Show(_back)),
            };
        }

        List<MenuEntry> StatItems()
        {
            return new List<MenuEntry>
            {
                new(() => "KILLS: " + Progression.TotalKills, null),
                new(() => "SURVIVAL: " + PlayerPrefs.GetInt(SaveKeys.SurvivalBestScore, 0) + " (WAVE " + PlayerPrefs.GetInt(SaveKeys.SurvivalBestWave, 0) + ")", null),
                new(() => "ASSAULT: " + PlayerPrefs.GetInt(SaveKeys.AssaultBestScore, 0), null),
                new(() => "ESCORT: " + PlayerPrefs.GetInt(SaveKeys.EscortBestScore, 0), null),
                new(() => "TEAM DOGFIGHT: " + PlayerPrefs.GetInt(SaveKeys.DogfightBestScore, 0), null),
                new(() => "TRAINING: " + (PlayerPrefs.GetInt(SaveKeys.TrainingDone, 0) == 1 ? "DONE" : "NOT YET"), null),
                new(() => "COLOURS: " + Progression.UnlockedCount + "/" + Progression.Paints.Length, null),
                new(() => "NEXT COLOUR: " + NextUnlock(), null),
                new("BACK", () => Show(_back)),
            };
        }

        static string NextUnlock()
        {
            foreach (var p in Progression.Paints) if (!p.Unlocked()) return p.Unlock;
            return "ALL UNLOCKED";
        }

        static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        static string Vignette(VignetteLevel v) => v switch
        {
            VignetteLevel.Off => "OFF",
            VignetteLevel.Low => "LOW",
            VignetteLevel.High => "HIGH",
            _ => "MEDIUM",
        };
    }
}
