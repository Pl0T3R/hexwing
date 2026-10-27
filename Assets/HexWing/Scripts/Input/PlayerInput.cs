using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player's controls, in three schemes (PLAN.md section 2). The flying hand is the right one
    /// unless hands are swapped.
    ///
    /// A. Controller-direct: the flying controller's tilt from a neutral pose gives pitch and yaw (click
    ///    its thumbstick to set the neutral pose); the other stick gives throttle (Y) and roll (X).
    /// B. Virtual stick (default): hold the grip anywhere to take the stick. Moving the hand from where
    ///    you grabbed gives pitch (forward = nose down) and yaw (sideways); twisting the wrist rolls.
    ///    Letting go centres the stick. The other grip takes the throttle lever the same way: push
    ///    forward for more, pull back for less; it stays where you leave it. The other stick's X also rolls.
    /// C. Thumbsticks: flying stick = pitch and yaw, other stick = throttle (Y) and roll (X).
    ///
    /// Positions and rotations are in the rig's tracking space, which is fixed to the cockpit, so turning
    /// the ship never moves the reference. Every axis goes through the deadzone and response curve.
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
            Deflection = Vector3.zero;
        }

        public void SetScheme(ControlScheme scheme)
        {
            Scheme = scheme;
            PlayerPrefs.SetInt(SchemeKey, (int)scheme);
            PlayerPrefs.Save();
            StickHeld = ThrottleHeld = false;
            _neutralSet = false;
            Debug.Log($"[HexWing] Control scheme {SchemeLabel(scheme)}");
            SchemeChanged?.Invoke(scheme);
        }

        public static string SchemeLabel(ControlScheme s) => s switch
        {
            ControlScheme.ControllerDirect => "A: PRZECHYŁ PADA",
            ControlScheme.VirtualStick => "B: WIRTUALNY DRĄŻEK",
            _ => "C: GAŁKI",
        };

        void Update()
        {
            var cfg = GameConfig.Instance.controls;
            if (cfg.cycleWithY && OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
                SetScheme((ControlScheme)(((int)Scheme + 1) % 3));
        }

        public void ReadControls(ref ShipControls c)
        {
            var cfg = GameConfig.Instance.controls;
            float dt = Time.deltaTime;
            Vector2 otherStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ThrottleHand);
            float pitch = 0f, yaw = 0f, roll = 0f;

            switch (Scheme)
            {
                case ControlScheme.ControllerDirect:
                {
                    var rot = OVRInput.GetLocalControllerRotation(FlyHand);
                    if (!_neutralSet || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, FlyHand))
                    {
                        _neutral = rot;
                        _neutralSet = true;
                    }
                    var delta = Quaternion.Inverse(_neutral) * rot;
                    var f = delta * Vector3.forward;
                    pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg / cfg.tiltRange;   // tip up = nose up
                    yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg / cfg.tiltRange;
                    roll = otherStick.x;
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

            if (cfg.invertPitch) pitch = -pitch;
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
        }

        void VirtualStick(ref float pitch, ref float yaw, ref float roll)
        {
            var cfg = GameConfig.Instance.controls;
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, FlyHand);
            bool held = StickHeld ? grip > cfg.gripThreshold * 0.7f : grip > cfg.gripThreshold;
            var pos = OVRInput.GetLocalControllerPosition(FlyHand);
            var rot = OVRInput.GetLocalControllerRotation(FlyHand);
            if (held && !StickHeld)
            {
                _stickGrabPos = pos;
                _stickGrabRot = rot;
                Haptics.Play(FlyHand, GameConfig.Instance.haptics.grabStick);
            }
            StickHeld = held;
            if (!held) return;   // a released stick springs back to the centre

            var offset = pos - _stickGrabPos;
            pitch = -offset.z / cfg.stickRange;   // push forward = nose down
            yaw = offset.x / cfg.stickRange;
            var up = (Quaternion.Inverse(_stickGrabRot) * rot) * Vector3.up;
            roll = Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg / cfg.twistRange;   // twist clockwise (top to the right) = roll right
        }

        void VirtualThrottle()
        {
            var cfg = GameConfig.Instance.controls;
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ThrottleHand);
            bool held = ThrottleHeld ? grip > cfg.gripThreshold * 0.7f : grip > cfg.gripThreshold;
            float z = OVRInput.GetLocalControllerPosition(ThrottleHand).z;
            if (held && !ThrottleHeld)
            {
                _throttleGrabZ = z;
                _throttleAtGrab = _throttle;
                Haptics.Play(ThrottleHand, GameConfig.Instance.haptics.grabStick);
            }
            ThrottleHeld = held;
            if (held) _throttle = Mathf.Clamp01(_throttleAtGrab + (z - _throttleGrabZ) / cfg.throttleRange);
        }

        /// <summary>Stick throttle like Starfighter Assault: centred = cruise, up = full, down = minimum.</summary>
        void StickThrottle(float y, float dt)
        {
            var cfg = GameConfig.Instance.controls;
            y = Shape(y);
            float target = y >= 0f ? Mathf.Lerp(cfg.cruiseThrottle, 1f, y) : Mathf.Lerp(cfg.cruiseThrottle, 0f, -y);
            _throttle = Mathf.MoveTowards(_throttle, target, cfg.throttleResponse * dt);
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
