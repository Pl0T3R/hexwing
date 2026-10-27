using System.Text;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Fills the cockpit displays (M2), a few times a second so the texts don't allocate every frame:
    /// - left: speed, throttle and its band (the sweet spot), the control scheme, auto-level,
    /// - right: the rings course (ring, time, best) or the out-of-bounds countdown in red,
    /// - centre: the performance readout,
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
        float _next;
        ControlScheme _shownScheme = (ControlScheme)(-1);

        void Start()
        {
            _cockpit = GetComponent<PlayerCockpit>();
            _ship = GetComponent<ShipFlight>();
            _input = GetComponent<PlayerInput>();
            _bounds = GetComponent<ArenaBounds>();
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
            var course = RingsCourse.Instance;
            if (course == null) { _cockpit.Right.SetText(""); return; }
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

        void PerfDisplay()
        {
            var debug = GameConfig.Instance.debug;
            var perf = PerformanceManager.Instance;
            _sb.Clear().Append("HEX WING  M2");
            if (debug.performanceReadout && perf != null) _sb.Append('\n').Append(perf.Readout);
            _cockpit.Center.SetText(_sb.ToString());
        }

        void HelpDisplays()
        {
            if (_input == null || _input.Scheme == _shownScheme) return;   // these only change with the scheme
            _shownScheme = _input.Scheme;
            var cfg = GameConfig.Instance;
            _cockpit.SideLeft.SetText(PlayerInput.SchemeLabel(_shownScheme) + "\n" + SchemeHelp(_shownScheme, cfg.controls.swapHands) +
                                      (cfg.controls.cycleWithY ? "\nY: ZMIEŃ STER" : ""));
            _cockpit.SideRight.SetText(
                "WINIETA " + VignetteLabel(cfg.comfort.vignette) + "\n" +
                "POZIOMOWANIE " + (cfg.comfort.autoLevel ? "TAK" : "NIE") + "\n" +
                "LIMIT SKRĘTU " + (cfg.comfort.turnRateLimit > 0f ? Mathf.RoundToInt(cfg.comfort.turnRateLimit) + "°/s" : "BRAK") + "\n" +
                "MENU: RESTART\nTRZYMAJ L-GAŁKĘ: CENTRUJ");
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
