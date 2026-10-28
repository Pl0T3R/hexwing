using System.Text;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Fills the cockpit displays (M2), a few times a second so the texts don't allocate every frame:
    /// - left: speed, throttle and its band (the sweet spot), stick and lever, auto-level,
    /// - right: the mode (rings: ring, time, best; drones: target, kills, accuracy) or the out-of-bounds
    ///   countdown in red,
    /// - centre: hull, shields, gun heat, missile and evasive readiness, then the performance readout,
    /// - side left: how the current scheme works, side right: the comfort settings.
    /// </summary>
    public class CockpitHud : MonoBehaviour
    {
        const float Interval = 0.2f;

        static readonly Color Cyan = new(0.37f, 0.83f, 1f);
        static readonly Color Amber = new(1f, 0.7f, 0.28f);
        static readonly Color Red = new(1f, 0.25f, 0.2f);

        readonly StringBuilder _sb = new(256);
        PlayerCockpit _cockpit;
        ShipFlight _ship;
        PlayerInput _input;
        ArenaBounds _bounds;
        ShipWeapons _weapons;
        Damageable _health;
        AbilitySystem _abilities;
        int _shownShip = -1;
        float _next;
        ControlScheme _shownScheme = (ControlScheme)(-1);
        string _shownInput;

        void Start()
        {
            _cockpit = GetComponent<PlayerCockpit>();
            _ship = GetComponent<ShipFlight>();
            _input = GetComponent<PlayerInput>();
            _bounds = GetComponent<ArenaBounds>();
            _weapons = GetComponent<ShipWeapons>();
            _health = GetComponent<Damageable>();
            _abilities = GetComponent<AbilitySystem>();
        }

        void Update()
        {
            if (_cockpit == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Interval;
            FlightDisplay();
            CourseDisplay();
            PerfDisplay();
            HelpDisplays();
        }

        void FlightDisplay()
        {
            if (_ship == null) return;
            _sb.Clear();
            _sb.Append("SPEED ").Append(Mathf.RoundToInt(_ship.Speed)).Append(" m/s\n");
            _sb.Append("THROTTLE ").Append(Mathf.RoundToInt(_ship.Controls.Throttle * 100f)).Append("%  ").Append(BandLabel(_ship.CurrentBand)).Append('\n');
            _sb.Append("TURN x").Append(_ship.TurnMultiplier.ToString("0.0"));
            if (_ship.AutoLeveling) _sb.Append("  LEVELING");
            _sb.Append("\nAFTERBURNER ");
            Bar(_ship.AfterburnerFuel, 8);
            if (_ship.Afterburning) _sb.Append(" ON");
            if (_input != null && _input.Scheme == ControlScheme.VirtualStick)
                _sb.Append('\n').Append(_input.StickHeld ? "[STICK]" : " stick ").Append(' ').Append(_input.ThrottleHeld ? "[THROTTLE]" : " throttle ");
            _cockpit.Left.SetText(_sb.ToString());
            _cockpit.Left.SetColor(_ship.CurrentBand == ShipFlight.Band.SweetSpot ? Amber : Cyan);
        }

        void CourseDisplay()
        {
            _sb.Clear();
            if (_bounds != null && _bounds.Outside)
            {
                _sb.Append("OUT OF BOUNDS!\nTURN BACK  ").Append(Mathf.CeilToInt(_bounds.SecondsLeft));
                _cockpit.Right.SetText(_sb.ToString());
                _cockpit.Right.SetColor(Red);
                return;
            }
            if (Training.Instance != null && Training.Instance.isActiveAndEnabled)
            {
                _cockpit.Right.SetText(Training.Instance.Instruction());
                _cockpit.Right.SetColor(Training.Instance.Current == Training.Step.Done ? Amber : Cyan);
                return;
            }
            if (Survival.Instance != null && Survival.Instance.isActiveAndEnabled) { SurvivalDisplay(Survival.Instance); return; }
            if (CapitalAssault.Instance != null && CapitalAssault.Instance.isActiveAndEnabled) { AssaultDisplay(CapitalAssault.Instance); return; }
            if (Escort.Instance != null && Escort.Instance.isActiveAndEnabled) { EscortDisplay(Escort.Instance); return; }
            if (TeamDogfight.Instance != null && TeamDogfight.Instance.isActiveAndEnabled) { DogfightDisplay(TeamDogfight.Instance); return; }
            if (DroneRange.Instance != null && DroneRange.Instance.isActiveAndEnabled) { RangeDisplay(DroneRange.Instance); return; }
            var course = RingsCourse.Instance;
            if (course == null || !course.isActiveAndEnabled) { _cockpit.Right.SetText(""); return; }
            switch (course.Phase)
            {
                case RingsCourse.State.Ready:
                    _sb.Append("COURSE: ").Append(course.Count).Append(" RINGS\nFLY THROUGH THE FIRST\n");
                    break;
                case RingsCourse.State.Running:
                    _sb.Append("RING ").Append(course.Next + 1).Append('/').Append(course.Count).Append('\n');
                    _sb.Append("TIME ").Append(FormatTime(course.Elapsed)).Append('\n');
                    if (course.SinceMissed < 2f) _sb.Append("MISSED! TURN AROUND\n");
                    break;
                default:
                    _sb.Append(course.NewBest ? "NEW RECORD!\n" : "FINISH\n");
                    _sb.Append("TIME ").Append(FormatTime(course.LastTime)).Append('\n');
                    _sb.Append("COLLISIONS ").Append(course.Collisions).Append('\n');
                    break;
            }
            if (course.BestTime > 0f) _sb.Append("BEST ").Append(FormatTime(course.BestTime));
            _cockpit.Right.SetText(_sb.ToString());
            _cockpit.Right.SetColor(course.Phase == RingsCourse.State.Finished ? Amber : Cyan);
        }

        void SurvivalDisplay(Survival mode)
        {
            _sb.Clear();
            switch (mode.Phase)
            {
                case Survival.State.Intro:
                    _sb.Append("WAVE ").Append(mode.Wave).Append('\n');
                    if (mode.EliteWave) _sb.Append("WARNING: ELITE!\n");
                    _sb.Append("ENEMIES ").Append(mode.EnemiesLeft).Append('\n');
                    break;
                case Survival.State.Fighting:
                    _sb.Append("WAVE ").Append(mode.Wave).Append("  ENEMIES ").Append(mode.EnemiesLeft).Append('\n');
                    Threats();
                    break;
                default:
                    _sb.Append(mode.NewBest ? "NEW RECORD!\n" : "GAME OVER\n");
                    _sb.Append("WAVE ").Append(mode.Wave).Append("  KILLS ").Append(mode.Kills).Append('\n');
                    break;
            }
            _sb.Append("SCORE ").Append(mode.Score).Append("  WINGMEN ").Append(mode.WingmenAlive).Append('\n');
            _sb.Append("BEST ").Append(mode.BestScore).Append(" (WAVE ").Append(mode.BestWave).Append(')');
            _cockpit.Right.SetText(_sb.ToString());
            bool alert = mode.Phase != Survival.State.Fighting || _threat;
            _cockpit.Right.SetColor(_threat ? Red : alert ? Amber : Cyan);
        }

        void AssaultDisplay(CapitalAssault mode)
        {
            _sb.Clear();
            var ship = mode.Ship;
            switch (mode.Phase)
            {
                case CapitalAssault.State.Briefing:
                    _sb.Append("ASSAULT: COLOSSUS\nDESTROY THE GENERATORS,\nTURRETS, THEN THE REACTOR\n");
                    break;
                case CapitalAssault.State.Fighting:
                    if (ship != null)
                    {
                        switch (ship.Current)
                        {
                            case CapitalShip.Phase.Generators: _sb.Append("TARGET: GENERATORS ").Append(ship.GeneratorsLeft).Append('\n'); break;
                            case CapitalShip.Phase.Turrets: _sb.Append("TARGET: TURRETS ").Append(ship.TurretsLeft).Append('\n'); break;
                            case CapitalShip.Phase.Reactor: _sb.Append("TARGET: REACTOR ").Append(Mathf.CeilToInt(ship.Reactor.Hull)).Append('\n'); break;
                            default: _sb.Append("COLOSSUS GOING DOWN!\n"); break;
                        }
                    }
                    _sb.Append("TIME ").Append(Clock(mode.TimeLeft)).Append('\n');
                    Threats();
                    break;
                case CapitalAssault.State.Won:
                    _sb.Append("VICTORY!\nCOLOSSUS DESTROYED\n");
                    break;
                default:
                    _sb.Append("DEFEAT\n").Append(mode.LoseReason).Append('\n');
                    break;
            }
            _sb.Append("SHIPS ").Append(mode.LivesLeft).Append("  SCORE ").Append(mode.Score).Append('\n');
            _sb.Append("BEST ").Append(mode.BestScore);
            _cockpit.Right.SetText(_sb.ToString());
            _cockpit.Right.SetColor(_threat || mode.Phase == CapitalAssault.State.Lost ? Red : mode.Phase == CapitalAssault.State.Fighting ? Cyan : Amber);
        }

        void EscortDisplay(Escort mode)
        {
            _sb.Clear();
            switch (mode.Phase)
            {
                case Escort.State.Briefing:
                    _sb.Append("ESCORT\nLEAD THE CONVOY\nTHROUGH THE ASTEROID BELT\n");
                    break;
                case Escort.State.Underway:
                    _sb.Append("CONVOY ").Append(mode.TransportsAlive).Append('/').Append(mode.TransportsTotal)
                       .Append("  ROUTE ").Append(Mathf.RoundToInt(mode.RouteProgress * 100f)).Append("%\n");
                    if (mode.EnemiesAlive > 0) _sb.Append("AMBUSH! ENEMIES ").Append(mode.EnemiesAlive).Append('\n');
                    Threats();
                    break;
                case Escort.State.Won:
                    _sb.Append("CONVOY ARRIVED!\n");
                    break;
                default:
                    _sb.Append("DEFEAT\n").Append(mode.LoseReason).Append('\n');
                    break;
            }
            _sb.Append("SHIPS ").Append(mode.LivesLeft).Append("  SCORE ").Append(mode.Score).Append('\n');
            _sb.Append("BEST ").Append(mode.BestScore);
            _cockpit.Right.SetText(_sb.ToString());
            _cockpit.Right.SetColor(_threat || mode.Phase == Escort.State.Lost || mode.EnemiesAlive > 0 ? Red : mode.Phase == Escort.State.Underway ? Cyan : Amber);
        }

        void DogfightDisplay(TeamDogfight mode)
        {
            _sb.Clear();
            var cfg = GameConfig.Instance.dogfight;
            var me = mode.Roster.LocalPlayer;
            switch (mode.Phase)
            {
                case TeamDogfight.State.Briefing:
                    _sb.Append("TEAM DOGFIGHT ").Append(cfg.teamSize).Append(" VS ").Append(cfg.teamSize).Append("\nFIRST TO ").Append(cfg.killsToWin).Append(" KILLS\n");
                    break;
                case TeamDogfight.State.Fighting:
                    _sb.Append("US ").Append(mode.OurKills).Append(" : ").Append(mode.TheirKills).Append(" THEM\n");
                    _sb.Append("TIME ").Append(Clock(mode.TimeLeft)).Append('\n');
                    Threats();
                    break;
                default:
                    _sb.Append(mode.Result > 0 ? "WIN!" : mode.Result < 0 ? "LOSS" : "DRAW").Append("  ")
                       .Append(mode.OurKills).Append(':').Append(mode.TheirKills).Append('\n');
                    break;
            }
            if (me != null) _sb.Append("YOU ").Append(me.Kills).Append(" KILLS / ").Append(me.Deaths).Append(" DEATHS\n");
            _sb.Append("SCORE ").Append(mode.Score).Append("  BEST ").Append(mode.BestScore);
            _cockpit.Right.SetText(_sb.ToString());
            bool losing = mode.Phase == TeamDogfight.State.Over ? mode.Result < 0 : false;
            _cockpit.Right.SetColor(_threat || losing ? Red : mode.Phase == TeamDogfight.State.Fighting ? Cyan : Amber);
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        bool _threat;

        /// <summary>Missile chasing the player, or an enemy locking on: a warning line, beeps and a buzz.</summary>
        void Threats()
        {
            _threat = false;
            var me = _health;
            if (me == null || !me.Alive) return;
            if (MissileSystem.Instance != null && MissileSystem.Instance.IsChasing(me))
            {
                _sb.Append("MISSILE! EVADE (X)\n");
                _threat = true;
                ShipAudio.Instance?.PlayMissileWarning(true);
                Haptics.PlayBoth(GameConfig.Instance.haptics.missileWarning);
                return;
            }
            var all = ShipWeapons.All;
            for (int i = 0; i < all.Count; i++)
            {
                var w = all[i];
                if (w.IsPlayer || w.Target != me || w.LockProgress <= 0f) continue;
                _sb.Append("LOCKED ON YOU!\n");
                _threat = true;
                ShipAudio.Instance?.PlayMissileWarning(false);
                return;
            }
        }

        void RangeDisplay(DroneRange range)
        {
            _sb.Clear();
            var target = _weapons != null ? _weapons.Target : null;
            if (Time.time - range.LastKillTime < 1.5f) _sb.Append("SPLASH ONE!\n");
            else if (target != null)
            {
                _sb.Append("TARGET ").Append(target.DisplayName).Append(' ')
                   .Append(Mathf.RoundToInt(Vector3.Distance(target.transform.position, transform.position))).Append(" m\n");
                if (_weapons.Locked) _sb.Append("LOCKED\n");
                else if (_weapons.LockProgress > 0f) _sb.Append("LOCKING ").Append(Mathf.RoundToInt(_weapons.LockProgress * 100f)).Append("%\n");
            }
            else _sb.Append("FIND A TARGET\n");
            _sb.Append("KILLS ").Append(range.Kills).Append('\n');
            _sb.Append("ACCURACY ").Append(Mathf.RoundToInt(range.Accuracy * 100f)).Append("%  (").Append(range.Hits).Append('/').Append(range.Shots).Append(')');
            _cockpit.Right.SetText(_sb.ToString());
            _cockpit.Right.SetColor(Time.time - range.LastKillTime < 1.5f || (_weapons != null && _weapons.Locked) ? Amber : Cyan);
        }

        void PerfDisplay()
        {
            var debug = GameConfig.Instance.debug;
            var perf = PerformanceManager.Instance;
            _sb.Clear();
            if (_health != null)
            {
                _sb.Append("HULL ").Append(Mathf.CeilToInt(_health.Hull)).Append("  SHIELDS ").Append(Mathf.CeilToInt(_health.Shields)).Append('\n');
            }
            if (_weapons != null)
            {
                _sb.Append("GUNS ");
                if (_weapons.Overheated) _sb.Append("OVERHEATED ").Append(_weapons.LockoutLeft.ToString("0.0")).Append(" s");
                else Bar(_weapons.Heat, 10);
                _sb.Append('\n').Append("MISSILE ");
                if (_weapons.MissileCooldownLeft > 0f) _sb.Append(_weapons.MissileCooldownLeft.ToString("0.0")).Append(" s");
                else _sb.Append("READY");
            }
            if (_ship != null)
            {
                _sb.Append("  EVADE ");
                if (_ship.EvasiveCooldownLeft > 0f) _sb.Append(Mathf.CeilToInt(_ship.EvasiveCooldownLeft)).Append(" s");
                else _sb.Append("OK");
            }
            if (_abilities != null)
            {
                _sb.Append('\n');
                AbilityLine("A", _abilities.A);
                _sb.Append("  ");
                AbilityLine("B", _abilities.B);
            }
            if (debug.performanceReadout && perf != null) _sb.Append('\n').Append(perf.Readout);
            _cockpit.Center.SetText(_sb.ToString());
            bool danger = (_weapons != null && (_weapons.Overheated || _weapons.Heat > 0.8f)) ||
                          (_health != null && _health.Hull < _health.MaxHull * 0.3f);
            _cockpit.Center.SetColor(danger ? Red : Cyan);
        }

        void AbilityLine(string button, AbilitySystem.Slot slot)
        {
            _sb.Append(button).Append(':').Append(ShipDefinition.AbilityLabel(slot.Kind)).Append(' ');
            if (slot.Busy) _sb.Append(Mathf.RoundToInt(slot.Progress * 100f)).Append('%');
            else if (slot.Active) _sb.Append("ACTIVE");
            else if (slot.CooldownLeft > 0f) _sb.Append(Mathf.CeilToInt(slot.CooldownLeft)).Append('s');
            else _sb.Append("OK");
        }

        /// <summary>A text bar: filled '|' and empty '.' cells.</summary>
        void Bar(float value, int cells)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(value * cells), 0, cells);
            _sb.Append('[');
            for (int i = 0; i < cells; i++) _sb.Append(i < filled ? '|' : '.');
            _sb.Append(']');
        }

        void HelpDisplays()
        {
            var player = PlayerShip.Instance;
            int ship = player != null ? player.ShipIndex : 0;
            if (_input != null && GameConfig.Instance.debug.inputReadout)
            {
                // the raw input readout replaces the scheme help while it is on (debug.inputReadout)
                if (!ReferenceEquals(_input.DebugText, _shownInput))
                {
                    _shownInput = _input.DebugText;
                    _cockpit.SideLeft.SetText(_shownInput);
                }
            }
            if (_input == null || (_input.Scheme == _shownScheme && ship == _shownShip)) return;   // these only change with the scheme or ship
            _shownScheme = _input.Scheme;
            _shownShip = ship;
            var cfg = GameConfig.Instance;
            if (!cfg.debug.inputReadout)
                _cockpit.SideLeft.SetText(PlayerInput.SchemeLabel(_shownScheme) + "\n" + SchemeHelp(_shownScheme, cfg.controls.swapHands) +
                                          (cfg.controls.cycleWithY ? "\nY: CHANGE CONTROLS" : ""));
            _cockpit.SideRight.SetText(
                (player != null && player.Definition != null ? player.Definition.displayName + "  (" + ShipDefinition.PassiveLabel(player.Definition.passive) + ")\n" : "") +
                "VIGNETTE " + VignetteLabel(cfg.comfort.vignette) + "\n" +
                "AUTO-LEVEL " + (cfg.comfort.autoLevel ? "ON" : "OFF") + "\n" +
                "TURN LIMIT " + (cfg.comfort.turnRateLimit > 0f ? Mathf.RoundToInt(cfg.comfort.turnRateLimit) + "°/s" : "NONE") + "\n" +
                "MENU: PAUSE AND HANGAR\nHOLD L-STICK: RECENTER\n" + ButtonMap(cfg.controls.swapHands));
        }

        /// <summary>The buttons that are the same in every scheme, named for the hand that flies.</summary>
        static string ButtonMap(bool swapped) => swapped
            ? "L TRIGGER: GUNS  R TRIGGER: MISSILE\nA: EVADE  X/Y: ABILITIES\nB: CONTROLS, HOLD B: SHIP"
            : "R TRIGGER: GUNS  L TRIGGER: MISSILE\nX: EVADE  A/B: ABILITIES\nY: CONTROLS, HOLD Y: SHIP";

        static string SchemeHelp(ControlScheme s, bool swapped)
        {
            string fly = swapped ? "LEFT" : "RIGHT", other = swapped ? "RIGHT" : "LEFT";
            return s switch
            {
                ControlScheme.ControllerDirect => $"{fly} CONTROLLER = STICK: TILT / TURN\n{fly} STICK CLICK: RESET\n{other} STICK: THROTTLE / ROLL",
                ControlScheme.VirtualStick => $"{fly} GRIP: STICK\n{other} GRIP: THROTTLE\n{other} STICK X: ROLL",
                _ => $"{fly} STICK: TURN\n{other} STICK: THROTTLE / ROLL",
            };
        }

        static string BandLabel(ShipFlight.Band b) => b switch
        {
            ShipFlight.Band.Slow => "SLOW",
            ShipFlight.Band.SweetSpot => "SWEET SPOT",
            ShipFlight.Band.Boost => "BOOST",
            _ => "CRUISE",
        };

        static string VignetteLabel(VignetteLevel v) => v switch
        {
            VignetteLevel.Off => "OFF",
            VignetteLevel.Low => "LOW",
            VignetteLevel.High => "HIGH",
            _ => "MEDIUM",
        };

        static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60;
            return m > 0 ? $"{m}:{s:00.0}" : $"{s:0.0} s";
        }
    }
}
