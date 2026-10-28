using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player's controls, in three schemes (PLAN.md section 2). The flying hand is the right one
    /// unless hands are swapped.
    ///
    /// A. Controller-direct: the flying controller is the stick. From its neutral pose (taken when the
    ///    scheme starts; click its thumbstick to take it again) tipping it up or down pitches, pointing it
    ///    left or right yaws, twisting the wrist rolls. The other stick gives throttle (Y) and roll (X).
    /// B. Virtual stick (default): hold the grip anywhere to take the stick. Moving the hand from where
    ///    you grabbed gives pitch (forward = nose down) and, sideways, yaw (or roll, controls.sideways);
    ///    twisting the wrist gives the other one. Letting go centres the stick. The other grip takes the
    ///    throttle lever the same way: push forward for more, pull back for less; it stays where you
    ///    leave it. The other stick's X also rolls.
    /// C. Thumbsticks: flying stick = pitch and yaw, other stick = throttle (Y) and roll (X).
    ///
    /// The throttle never changes when a control is let go: the lever stays, and the thumbstick
    /// throttle holds by default (controls.throttleStick). It clicks into notches at cruise and at the
    /// boost line.
    ///
    /// Poses come from <see cref="ControllerPose"/> (OVRInput, else Unity's XR devices) in the rig's
    /// tracking space, which is fixed to the cockpit, so turning the ship never moves the reference.
    /// Every axis goes through the deadzone and response curve. The left side display can show the raw
    /// input (debug.inputReadout), and one line per second goes to the log (debug.inputLog).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class PlayerInput : MonoBehaviour, IShipPilot
    {
        const string SchemeKey = "HexWing.ControlScheme";

        public ControlScheme Scheme { get; private set; }
        public bool StickHeld { get; private set; }
        public bool ThrottleHeld { get; private set; }
        /// <summary>The stick's deflection after shaping: x = pitch, y = yaw, z = roll (for the cockpit stick).</summary>
        public Vector3 Deflection { get; private set; }
        public float Throttle => _throttle;

        public OVRInput.Controller FlyHand => GameConfig.Instance.controls.swapHands ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        public OVRInput.Controller ThrottleHand => GameConfig.Instance.controls.swapHands ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;

        public event System.Action<ControlScheme> SchemeChanged;

        float _throttle;
        Vector3 _stickGrabPos;
        Quaternion _stickGrabRot = Quaternion.identity;
        float _throttleGrabZ, _throttleAtGrab;
        Quaternion _neutral = Quaternion.identity;
        bool _neutralSet;
        bool _throttleMoving;
        bool _flyTracked, _otherTracked;
        Vector3 _flyPos, _otherPos;
        Quaternion _flyRot = Quaternion.identity, _otherRot = Quaternion.identity;

        void Awake()
        {
            var cfg = GameConfig.Instance.controls;
            Scheme = (ControlScheme)PlayerPrefs.GetInt(SchemeKey, (int)cfg.scheme);
            _throttle = cfg.cruiseThrottle;
        }

        /// <summary>Back to the start state (course restart): cruise throttle, stick released.</summary>
        public void ResetControls()
        {
            _throttle = GameConfig.Instance.controls.cruiseThrottle;
            StickHeld = ThrottleHeld = false;
            _throttleMoving = false;
            Deflection = Vector3.zero;
        }

        public void SetScheme(ControlScheme scheme)
        {
            Scheme = scheme;
            PlayerPrefs.SetInt(SchemeKey, (int)scheme);
            PlayerPrefs.Save();
            StickHeld = ThrottleHeld = false;
            _neutralSet = false;   // A takes the neutral pose again on its first frame
            Debug.Log($"[HexWing] Control scheme {SchemeLabel(scheme)}");
            SchemeChanged?.Invoke(scheme);
        }

        public static string SchemeLabel(ControlScheme s) => s switch
        {
            ControlScheme.ControllerDirect => "A: PRZECHYŁ PADA",
            ControlScheme.VirtualStick => "B: WIRTUALNY DRĄŻEK",
            _ => "C: GAŁKI",
        };

        float _yHeld;
        bool _suppress;

        /// <summary>
        /// Ignores fire, lock, abilities and evasive until all of them have been let go (after a menu closes
        /// with the trigger or A still held, so the ship doesn't shoot or use an ability by itself).
        /// </summary>
        public void SuppressUntilReleased() => _suppress = true;

        /// <summary>
        /// The other hand's upper button (Y, or B with hands swapped): a short press cycles the control
        /// scheme, holding it for a second switches the ship class. The flying hand's buttons stay abilities.
        /// </summary>
        void Update()
        {
            var cfg = GameConfig.Instance.controls;
            if (GameMenu.Instance != null && GameMenu.Instance.Open) { _yHeld = 0f; return; }
            if (OVRInput.Get(OVRInput.Button.Two, ThrottleHand))
            {
                _yHeld += Time.unscaledDeltaTime;
                if (_yHeld >= 1f)
                {
                    _yHeld = float.NegativeInfinity;   // once per press
                    PlayerShip.Instance?.SelectNextShip();
                }
            }
            else
            {
                if (_yHeld > 0f && cfg.cycleWithY) SetScheme((ControlScheme)(((int)Scheme + 1) % 3));
                _yHeld = 0f;
            }
        }

        public void ReadControls(ref ShipControls c)
        {
            var cfg = GameConfig.Instance.controls;
            float dt = Time.deltaTime;
            Vector2 otherStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ThrottleHand);
            _flyTracked = ControllerPose.Get(FlyHand, out _flyPos, out _flyRot);
            _otherTracked = ControllerPose.Get(ThrottleHand, out _otherPos, out _otherRot);
            float pitch = 0f, yaw = 0f, roll = 0f;
            float before = _throttle;

            switch (Scheme)
            {
                case ControlScheme.ControllerDirect:
                {
                    // the flying controller is the stick: tip it up or down to pitch, point it left or right
                    // to yaw, twist the wrist to roll
                    if (!_neutralSet || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, FlyHand))
                    {
                        _neutral = _flyRot;
                        _neutralSet = _flyTracked;
                        if (_flyTracked) Haptics.Play(FlyHand, GameConfig.Instance.haptics.grabStick);
                    }
                    var delta = Quaternion.Inverse(_neutral) * _flyRot;
                    var f = delta * Vector3.forward;
                    var u = delta * Vector3.up;
                    pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg / cfg.tiltRange;   // tip up = nose up
                    yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg / cfg.tiltRange;
                    roll = Mathf.Atan2(u.x, u.y) * Mathf.Rad2Deg / cfg.tiltTwistRange;   // top to the right = roll right
                    roll += otherStick.x;
                    StickThrottle(otherStick.y, dt);
                    break;
                }
                case ControlScheme.VirtualStick:
                {
                    VirtualStick(ref pitch, ref yaw, ref roll);
                    roll += otherStick.x;
                    VirtualThrottle();
                    break;
                }
                default:
                {
                    Vector2 flyStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, FlyHand);
                    pitch = -flyStick.y;   // stick forward = nose down
                    yaw = flyStick.x;
                    roll = otherStick.x;
                    StickThrottle(otherStick.y, dt);
                    break;
                }
            }
            Detents(before);

            if (cfg.invertPitch) pitch = -pitch;
            if (cfg.invertYaw) yaw = -yaw;
            if (cfg.invertRoll) roll = -roll;
            c.Pitch = Shape(pitch);
            c.Yaw = Shape(yaw);
            c.Roll = Shape(roll);
            c.Throttle = _throttle;
            Deflection = new Vector3(c.Pitch, c.Yaw, c.Roll);

            // the same in every scheme: flying hand's trigger fires, the other trigger locks on
            // (releasing it while locked fires a missile), X evades; A/B are the abilities (M5)
            c.Fire = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, FlyHand) > 0.6f;
            c.Lock = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ThrottleHand) > 0.5f;
            c.Ability1 = OVRInput.Get(OVRInput.Button.One, FlyHand);
            c.Ability2 = OVRInput.Get(OVRInput.Button.Two, FlyHand);
            c.Evasive = OVRInput.Get(OVRInput.Button.One, ThrottleHand);
            if (_suppress)
            {
                bool any = c.Fire || c.Lock || c.Ability1 || c.Ability2 || c.Evasive;
                if (!any) _suppress = false;
                c.Fire = c.Lock = c.Ability1 = c.Ability2 = c.Evasive = false;
            }
            Diagnostics(otherStick, c);
        }

        void VirtualStick(ref float pitch, ref float yaw, ref float roll)
        {
            var cfg = GameConfig.Instance.controls;
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, FlyHand);
            bool held = StickHeld ? grip > cfg.gripThreshold * 0.7f : grip > cfg.gripThreshold;
            if (held && !StickHeld)
            {
                _stickGrabPos = _flyPos;
                _stickGrabRot = _flyRot;
                Haptics.Play(FlyHand, GameConfig.Instance.haptics.grabStick);
            }
            StickHeld = held;
            if (!held) return;   // a released stick springs back to the centre

            // movement in the frame of the cockpit (tracking space), so the stick works however the hand is turned
            var offset = _flyPos - _stickGrabPos;
            pitch = -offset.z / cfg.stickRange;   // push forward = nose down
            float sideways = offset.x / cfg.stickRange;
            var up = (Quaternion.Inverse(_stickGrabRot) * _flyRot) * Vector3.up;
            float twist = Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg / cfg.twistRange;   // twist clockwise (top to the right)
            if (cfg.sideways == StickSideways.Yaw) { yaw = sideways; roll = twist; }
            else { roll = sideways; yaw = twist; }
        }

        void VirtualThrottle()
        {
            var cfg = GameConfig.Instance.controls;
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ThrottleHand);
            bool held = ThrottleHeld ? grip > cfg.gripThreshold * 0.7f : grip > cfg.gripThreshold;
            float z = _otherPos.z;
            if (held && !ThrottleHeld)
            {
                _throttleGrabZ = z;
                _throttleAtGrab = _throttle;
                Haptics.Play(ThrottleHand, GameConfig.Instance.haptics.grabStick);
            }
            ThrottleHeld = held;
            // the lever only moves while held; letting go leaves it (and the speed) where it is
            if (held) _throttle = Mathf.Clamp01(_throttleAtGrab + (z - _throttleGrabZ) / cfg.throttleRange);
        }

        /// <summary>
        /// Thumbstick throttle. Hold (default): up speeds up, down slows down, and the throttle stays
        /// where you leave it. Spring (like Starfighter Assault): centred = cruise, up = full, down = minimum.
        /// </summary>
        void StickThrottle(float y, float dt)
        {
            var cfg = GameConfig.Instance.controls;
            y = Shape(y);
            if (cfg.throttleStick == ThrottleStickMode.Hold)
            {
                _throttle = Mathf.Clamp01(_throttle + y * cfg.throttleStickRate * dt);
                _throttleMoving = y != 0f;
                return;
            }
            float target = y >= 0f ? Mathf.Lerp(cfg.cruiseThrottle, 1f, y) : Mathf.Lerp(cfg.cruiseThrottle, 0f, -y);
            _throttle = Mathf.MoveTowards(_throttle, target, cfg.throttleResponse * dt);
            _throttleMoving = true;   // Spring mode follows the stick: never snap
        }

        /// <summary>
        /// Notches at cruise and at the boost line: a haptic click when the throttle passes one, and a
        /// throttle left close to one settles on it.
        /// </summary>
        void Detents(float before)
        {
            var cfg = GameConfig.Instance.controls;
            float boost = GameConfig.Instance.flight.boostFrom;
            float a = cfg.cruiseThrottle, b = boost;
            if (Crossed(before, _throttle, a) || Crossed(before, _throttle, b))
                Haptics.Play(ThrottleHand, GameConfig.Instance.haptics.detent);
            bool settling = Scheme == ControlScheme.VirtualStick ? !ThrottleHeld : !_throttleMoving;
            if (!settling || cfg.detentWidth <= 0f) return;
            if (Mathf.Abs(_throttle - a) < cfg.detentWidth) _throttle = a;
            else if (Mathf.Abs(_throttle - b) < cfg.detentWidth) _throttle = b;
        }

        static bool Crossed(float from, float to, float line) => (from < line && to >= line) || (from > line && to <= line);

        // --- input readout and log (to find control problems on the headset) ---------------------

        readonly System.Text.StringBuilder _sb = new(256);
        float _readoutAt, _logAt;

        /// <summary>Raw input and what came out of it, rebuilt four times a second.</summary>
        public string DebugText { get; private set; } = "";

        void Diagnostics(Vector2 otherStick, ShipControls c)
        {
            var dbg = GameConfig.Instance.debug;
            if (!dbg.inputReadout && !dbg.inputLog) return;
            float now = Time.unscaledTime;
            bool readout = dbg.inputReadout && now >= _readoutAt;
            bool log = dbg.inputLog && now >= _logAt;
            if (!readout && !log) return;

            Vector2 flyStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, FlyHand);
            _sb.Clear();
            _sb.Append("WEJŚCIE ").Append(SchemeLabel(Scheme).Substring(0, 1))
               .Append("  ŹRÓDŁO P:").Append(ControllerPose.Label(ControllerPose.LastSource(FlyHand)))
               .Append(" L:").Append(ControllerPose.Label(ControllerPose.LastSource(ThrottleHand))).Append('\n');
            _sb.Append("GRIP ").Append(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, FlyHand).ToString("0.00")).Append(StickHeld ? "*" : " ")
               .Append(" / ").Append(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ThrottleHand).ToString("0.00")).Append(ThrottleHeld ? "*" : " ").Append('\n');
            Pose(_flyTracked ? "P " : "P! ", _flyPos, _flyRot);
            Pose(_otherTracked ? "L " : "L! ", _otherPos, _otherRot);
            _sb.Append("GAŁKI ").Append(flyStick.x.ToString("0.0")).Append(',').Append(flyStick.y.ToString("0.0"))
               .Append(" / ").Append(otherStick.x.ToString("0.0")).Append(',').Append(otherStick.y.ToString("0.0")).Append('\n');
            _sb.Append("WYNIK p").Append(c.Pitch.ToString("0.00")).Append(" y").Append(c.Yaw.ToString("0.00"))
               .Append(" r").Append(c.Roll.ToString("0.00")).Append(" ciąg ").Append(Mathf.RoundToInt(c.Throttle * 100f)).Append('%');

            if (readout)
            {
                DebugText = _sb.ToString();
                _readoutAt = now + 0.25f;
            }
            if (log)
            {
                Debug.Log("[HexWing] Input: " + (readout ? DebugText : _sb.ToString()).Replace('\n', '|'));
                _logAt = now + 1f;
            }
        }

        void Pose(string label, Vector3 p, Quaternion r)
        {
            var e = r.eulerAngles;
            _sb.Append(label).Append(Mathf.RoundToInt(p.x * 100f)).Append(',').Append(Mathf.RoundToInt(p.y * 100f)).Append(',')
               .Append(Mathf.RoundToInt(p.z * 100f)).Append("cm ").Append(Mathf.RoundToInt(e.x)).Append('/').Append(Mathf.RoundToInt(e.y))
               .Append('/').Append(Mathf.RoundToInt(e.z)).Append("°\n");
        }

        /// <summary>Deadzone, response curve and sensitivity; keeps the sign, clamps to -1..1.</summary>
        static float Shape(float x)
        {
            var cfg = GameConfig.Instance.controls;
            float a = Mathf.Abs(x);
            if (a <= cfg.deadzone) return 0f;
            a = Mathf.Clamp01((a - cfg.deadzone) / (1f - cfg.deadzone));
            a = Mathf.Clamp01(Mathf.Pow(a, cfg.curve) * cfg.sensitivity);
            return x < 0f ? -a : a;
        }
    }
}
