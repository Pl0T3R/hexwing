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
            _sb.Append("PRĘDKOŚĆ ").Append(Mathf.RoundToInt(_ship.Speed)).Append(" m/s\n");
            _sb.Append("CIĄG ").Append(Mathf.RoundToInt(_ship.Controls.Throttle * 100f)).Append("%  ").Append(BandLabel(_ship.CurrentBand)).Append('\n');
            _sb.Append("SKRĘT x").Append(_ship.TurnMultiplier.ToString("0.0"));
            if (_ship.AutoLeveling) _sb.Append("  POZIOM");
            if (_input != null && _input.Scheme == ControlScheme.VirtualStick)
                _sb.Append('\n').Append(_input.StickHeld ? "[DRĄŻEK]" : " drążek ").Append(' ').Append(_input.ThrottleHeld ? "[MANETKA]" : " manetka ");
            _cockpit.Left.SetText(_sb.ToString());
            _cockpit.Left.SetColor(_ship.CurrentBand == ShipFlight.Band.SweetSpot ? Amber : Cyan);
        }

        void CourseDisplay()
        {
            _sb.Clear();
            if (_bounds != null && _bounds.Outside)
            {
                _sb.Append("POZA STREFĄ!\nWRACAJ  ").Append(Mathf.CeilToInt(_bounds.SecondsLeft));
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
            if (DroneRange.Instance != null && DroneRange.Instance.isActiveAndEnabled) { RangeDisplay(DroneRange.Instance); return; }
            var course = RingsCourse.Instance;
            if (course == null || !course.isActiveAndEnabled) { _cockpit.Right.SetText(""); return; }
            switch (course.Phase)
            {
                case RingsCourse.State.Ready:
                    _sb.Append("TOR: ").Append(course.Count).Append(" PIERŚCIENI\nLEĆ PRZEZ PIERWSZY\n");
                    break;
                case RingsCourse.State.Running:
                    _sb.Append("PIERŚCIEŃ ").Append(course.Next + 1).Append('/').Append(course.Count).Append('\n');
                    _sb.Append("CZAS ").Append(FormatTime(course.Elapsed)).Append('\n');
                    if (course.SinceMissed < 2f) _sb.Append("MINIĘTY! ZAWRÓĆ\n");
                    break;
                default:
                    _sb.Append(course.NewBest ? "NOWY REKORD!\n" : "META\n");
                    _sb.Append("CZAS ").Append(FormatTime(course.LastTime)).Append('\n');
                    _sb.Append("ZDERZENIA ").Append(course.Collisions).Append('\n');
                    break;
            }
            if (course.BestTime > 0f) _sb.Append("REKORD ").Append(FormatTime(course.BestTime));
            _cockpit.Right.SetText(_sb.ToString());
            _cockpit.Right.SetColor(course.Phase == RingsCourse.State.Finished ? Amber : Cyan);
        }

        void SurvivalDisplay(Survival mode)
        {
            _sb.Clear();
            switch (mode.Phase)
            {
                case Survival.State.Intro:
                    _sb.Append("FALA ").Append(mode.Wave).Append('\n');
                    if (mode.EliteWave) _sb.Append("UWAGA: GROT!\n");
                    _sb.Append("WROGOWIE ").Append(mode.EnemiesLeft).Append('\n');
                    break;
                case Survival.State.Fighting:
                    _sb.Append("FALA ").Append(mode.Wave).Append("  WROGOWIE ").Append(mode.EnemiesLeft).Append('\n');
                    Threats();
                    break;
                default:
                    _sb.Append(mode.NewBest ? "NOWY REKORD!\n" : "KONIEC GRY\n");
                    _sb.Append("FALA ").Append(mode.Wave).Append("  ZESTRZELONE ").Append(mode.Kills).Append('\n');
                    break;
            }
            _sb.Append("WYNIK ").Append(mode.Score).Append("  SKRZ. ").Append(mode.WingmenAlive).Append('\n');
            _sb.Append("REKORD ").Append(mode.BestScore).Append(" (FALA ").Append(mode.BestWave).Append(')');
            _cockpit.Right.SetText(_sb.ToString());
            bool alert = mode.Phase != Survival.State.Fighting || _threat;
            _cockpit.Right.SetColor(_threat ? Red : alert ? Amber : Cyan);
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
                _sb.Append("RAKIETA! UNIK (X)\n");
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
                _sb.Append("NAMIERZA CIĘ!\n");
                _threat = true;
                ShipAudio.Instance?.PlayMissileWarning(false);
                return;
            }
        }

        void RangeDisplay(DroneRange range)
        {
            _sb.Clear();
            var target = _weapons != null ? _weapons.Target : null;
            if (Time.time - range.LastKillTime < 1.5f) _sb.Append("ZESTRZELONY!\n");
            else if (target != null)
            {
                _sb.Append("CEL ").Append(target.DisplayName).Append(' ')
                   .Append(Mathf.RoundToInt(Vector3.Distance(target.transform.position, transform.position))).Append(" m\n");
                if (_weapons.Locked) _sb.Append("NAMIERZONY\n");
                else if (_weapons.LockProgress > 0f) _sb.Append("NAMIERZANIE ").Append(Mathf.RoundToInt(_weapons.LockProgress * 100f)).Append("%\n");
            }
            else _sb.Append("SZUKAJ CELU\n");
            _sb.Append("ZESTRZELONE ").Append(range.Kills).Append('\n');
            _sb.Append("CELNOŚĆ ").Append(Mathf.RoundToInt(range.Accuracy * 100f)).Append("%  (").Append(range.Hits).Append('/').Append(range.Shots).Append(')');
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
                _sb.Append("KADŁUB ").Append(Mathf.CeilToInt(_health.Hull)).Append("  OSŁONY ").Append(Mathf.CeilToInt(_health.Shields)).Append('\n');
            }
            if (_weapons != null)
            {
                _sb.Append("DZIAŁA ");
                if (_weapons.Overheated) _sb.Append("PRZEGRZANE ").Append(_weapons.LockoutLeft.ToString("0.0")).Append(" s");
                else Bar(_weapons.Heat, 10);
                _sb.Append('\n').Append("RAKIETA ");
                if (_weapons.MissileCooldownLeft > 0f) _sb.Append(_weapons.MissileCooldownLeft.ToString("0.0")).Append(" s");
                else _sb.Append("GOTOWA");
            }
            if (_ship != null)
            {
                _sb.Append("  UNIK ");
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
            else if (slot.Active) _sb.Append("AKTYWNA");
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
            if (_input == null || (_input.Scheme == _shownScheme && ship == _shownShip)) return;   // these only change with the scheme or ship
            _shownScheme = _input.Scheme;
            _shownShip = ship;
            var cfg = GameConfig.Instance;
            _cockpit.SideLeft.SetText(PlayerInput.SchemeLabel(_shownScheme) + "\n" + SchemeHelp(_shownScheme, cfg.controls.swapHands) +
                                      (cfg.controls.cycleWithY ? "\nY: ZMIEŃ STER" : ""));
            _cockpit.SideRight.SetText(
                (player != null && player.Definition != null ? player.Definition.displayName + "  (" + ShipDefinition.PassiveLabel(player.Definition.passive) + ")\n" : "") +
                "WINIETA " + VignetteLabel(cfg.comfort.vignette) + "\n" +
                "POZIOMOWANIE " + (cfg.comfort.autoLevel ? "TAK" : "NIE") + "\n" +
                "LIMIT SKRĘTU " + (cfg.comfort.turnRateLimit > 0f ? Mathf.RoundToInt(cfg.comfort.turnRateLimit) + "°/s" : "BRAK") + "\n" +
                "MENU: RESTART, TRZYMAJ: TRYB\nTRZYMAJ L-GAŁKĘ: CENTRUJ\n" +
                "SPUST P: DZIAŁA  SPUST L: RAKIETA\nX: UNIK  A/B: ZDOLNOŚCI\nTRZYMAJ Y: STATEK");
        }

        static string SchemeHelp(ControlScheme s, bool swapped)
        {
            string fly = swapped ? "LEWY" : "PRAWY", other = swapped ? "PRAWY" : "LEWY";
            return s switch
            {
                ControlScheme.ControllerDirect => $"{fly} PAD: PRZECHYŁ = SKRĘT\n{fly} GAŁKA KLIK: ZERO\n{other} GAŁKA: CIĄG / BECZKA",
                ControlScheme.VirtualStick => $"{fly} CHWYT: DRĄŻEK\n{other} CHWYT: MANETKA\n{other} GAŁKA X: BECZKA",
                _ => $"{fly} GAŁKA: SKRĘT\n{other} GAŁKA: CIĄG / BECZKA",
            };
        }

        static string BandLabel(ShipFlight.Band b) => b switch
        {
            ShipFlight.Band.Slow => "WOLNO",
            ShipFlight.Band.SweetSpot => "OPTYMALNIE",
            ShipFlight.Band.Boost => "DOPALACZ",
            _ => "PRZELOT",
        };

        static string VignetteLabel(VignetteLevel v) => v switch
        {
            VignetteLevel.Off => "WYŁ.",
            VignetteLevel.Low => "SŁABA",
            VignetteLevel.High => "MOCNA",
            _ => "ŚREDNIA",
        };

        static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60;
            return m > 0 ? $"{m}:{s:00.0}" : $"{s:0.0} s";
        }
    }
}
