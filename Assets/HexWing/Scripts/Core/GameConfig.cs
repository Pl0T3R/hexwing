using System;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Every tuning number in one asset (Assets/HexWing/Resources/GameConfig.asset, created by
    /// HexWing > Build Game). Edit it in the Inspector, also while playing. If the asset is missing,
    /// these defaults are used. Ship, weapon and ability data get their own assets from M2 on.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "HexWing/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        public PerformanceSettings performance = new();
        public ComfortSettings comfort = new();
        public CockpitSettings cockpit = new();
        public ControlSettings controls = new();
        public FlightSettings flight = new();
        public ArenaSettings arena = new();
        public CourseSettings course = new();
        public HapticSettings haptics = new();
        public DebugSettings debug = new();

        static GameConfig _instance;

        public static GameConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameConfig>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogWarning("[HexWing] GameConfig asset missing, using defaults. Run HexWing > Build Game.");
                    _instance = CreateInstance<GameConfig>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;
    }

    [Serializable]
    public class PerformanceSettings
    {
        [Tooltip("Display refresh rate to ask for. 90 is the target; 72 is the floor.")]
        public float targetRefreshRate = 90f;
        public OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;
        public bool dynamicFoveation = true;
        [Tooltip("Frame time budget (ms) the readout compares against: 11.1 ms at 90 Hz.")]
        public float frameBudgetMs = 11.1f;
    }

    [Serializable]
    public class ComfortSettings
    {
        [Tooltip("Seconds of black fade at match start/end and around a recenter.")]
        public float fadeSeconds = 0.6f;
        [Tooltip("Fade used around a recenter (shorter, so it only hides the jump).")]
        public float recenterFadeSeconds = 0.15f;
        [Tooltip("Recenter once automatically this many seconds after start (0 = off).")]
        public float autoRecenterDelay = 0.5f;
        [Tooltip("Hold the left thumbstick click this long to recenter.")]
        public float recenterHoldSeconds = 0.4f;

        [Header("Vignette (darkens the edge of the view while the ship turns)")]
        public VignetteLevel vignette = VignetteLevel.Medium;
        [Tooltip("Ship turn rate (deg/s) where the vignette starts.")]
        public float vignetteStartRate = 25f;
        [Tooltip("Ship turn rate (deg/s) where the vignette is at full strength.")]
        public float vignetteFullRate = 90f;
        [Tooltip("How fast the vignette follows the turn rate (1/s).")]
        public float vignetteResponse = 6f;

        [Header("Auto-level (rolls the ship back to the arena's horizon)")]
        public bool autoLevel = true;
        [Tooltip("Seconds without roll input before auto-level starts.")]
        public float autoLevelDelay = 0.8f;
        [Tooltip("Auto-level roll speed (deg/s). Gentle, so the horizon never swings.")]
        public float autoLevelRate = 40f;

        [Header("Turn limit")]
        [Tooltip("Caps the pitch and yaw rate (deg/s). 0 = off (the ship's own rates).")]
        public float turnRateLimit = 0f;
        [Tooltip("Caps the roll rate (deg/s). 0 = off.")]
        public float rollRateLimit = 0f;
    }

    public enum VignetteLevel { Off, Low, Medium, High }

    [Serializable]
    public class CockpitSettings
    {
        [Tooltip("Eye point in the cockpit, relative to the ship root (m). The ship turns around this point.")]
        public Vector3 eyePoint = new(0f, 0.12f, -0.05f);
        [Tooltip("Inner radius of the ball cockpit (m).")]
        public float shellRadius = 1.05f;
        [Tooltip("Angle (degrees) from straight ahead to the corners of the hexagonal front window.")]
        public float windowHalfAngle = 52f;
    }

    [Serializable]
    public class DebugSettings
    {
        [Tooltip("Show the performance readout on the centre cockpit display.")]
        public bool performanceReadout = true;
        [Tooltip("Seconds between readout updates (the text is rebuilt only then).")]
        public float readoutInterval = 0.5f;
    }

    public enum ControlScheme { ControllerDirect, VirtualStick, Thumbsticks }

    [Serializable]
    public class ControlSettings
    {
        [Tooltip("A = controller tilt, B = grab-anywhere virtual stick and throttle (default), C = thumbsticks.")]
        public ControlScheme scheme = ControlScheme.VirtualStick;
        [Tooltip("Let Y cycle the control schemes (M2 test build; shown on the left display).")]
        public bool cycleWithY = true;
        [Tooltip("Swap hands: the left hand flies, the right hand throttles.")]
        public bool swapHands = false;
        public bool invertPitch = false;

        [Header("Response")]
        [Range(0f, 0.5f)] public float deadzone = 0.12f;
        [Tooltip("Response curve exponent: 1 = linear, 2 = soft centre, precise small corrections.")]
        [Range(1f, 3f)] public float curve = 1.6f;
        [Range(0.3f, 2f)] public float sensitivity = 1f;

        [Header("B: virtual stick and throttle")]
        [Tooltip("Hand movement (m) from where you grabbed for full pitch / yaw.")]
        public float stickRange = 0.09f;
        [Tooltip("Wrist twist (deg) from where you grabbed for full roll.")]
        public float twistRange = 40f;
        [Tooltip("Hand movement forward/back (m) for the throttle to go from 0 to 1.")]
        public float throttleRange = 0.18f;
        [Tooltip("Grip value that takes the stick; it is let go below 70% of this.")]
        public float gripThreshold = 0.55f;

        [Header("A: controller tilt")]
        [Tooltip("Controller tilt (deg) from the neutral pose for full pitch / yaw. Click the flying hand's thumbstick to set the neutral pose.")]
        public float tiltRange = 30f;

        [Header("A and C: thumbstick throttle")]
        [Tooltip("Throttle with the stick centred. Up = full, down = minimum, like Starfighter Assault.")]
        [Range(0f, 1f)] public float cruiseThrottle = 0.4f;
        [Tooltip("How fast the throttle follows the stick (1/s).")]
        public float throttleResponse = 3f;
    }

    [Serializable]
    public class FlightSettings
    {
        [Header("Speed (m/s). The ship never stalls: minimum speed is always above 0.")]
        public float minSpeed = 35f;
        public float maxSpeed = 150f;
        public float acceleration = 45f;
        public float deceleration = 60f;

        [Header("Turn rates (deg/s) at the 1.0x band")]
        public float pitchRate = 70f;
        public float yawRate = 50f;
        public float rollRate = 140f;
        [Tooltip("How fast turn rates follow the stick (1/s). Lower = smoother, heavier ship.")]
        public float angularResponse = 6f;
        [Tooltip("Seconds for the flight path to follow the nose (slight drift in turns).")]
        public float noseLag = 0.2f;

        [Header("Throttle bands: turn rate multiplier by throttle (the sweet spot)")]
        [Tooltip("Throttle range of the sweet spot (tightest turns).")]
        public Vector2 sweetSpot = new(0.2f, 0.5f);
        public float sweetSpotMultiplier = 1.4f;
        [Tooltip("Throttle above which the ship is boosting (fast, wide turns).")]
        public float boostFrom = 0.85f;
        public float boostMultiplier = 0.6f;
        [Tooltip("Throttle width over which bands blend, so the turn rate never jumps.")]
        public float bandBlend = 0.06f;

        [Header("Collisions")]
        [Tooltip("Radius (m) of the ship's collision sphere.")]
        public float collisionRadius = 2.5f;
        [Tooltip("Share of speed kept after scraping an obstacle.")]
        [Range(0f, 1f)] public float collisionSpeedKeep = 0.5f;
    }

    [Serializable]
    public class ArenaSettings
    {
        [Tooltip("Radius (m) of the playable sphere around the arena centre.")]
        public float radius = 3000f;
        [Tooltip("Seconds outside before the ship is returned to the start (destroyed from M3 on).")]
        public float outsideSeconds = 5f;
    }

    [Serializable]
    public class CourseSettings
    {
        public int rings = 16;
        [Tooltip("Random seed of the course; the same seed gives the same course (and comparable times).")]
        public int seed = 7;
        [Tooltip("Ring inner radius (m).")]
        public float ringRadius = 16f;
        public Vector2 spacing = new(260f, 420f);
        [Tooltip("Largest change of direction between rings (deg).")]
        public float maxTurn = 40f;
        [Tooltip("Largest change of height direction between rings (deg).")]
        public float maxClimb = 22f;
        [Tooltip("Seconds the result stays on screen before the course restarts.")]
        public float resultSeconds = 5f;
    }

    [Serializable]
    public class HapticPulse
    {
        [Range(0f, 1f)] public float frequency = 0.5f;
        [Range(0f, 1f)] public float amplitude = 0.5f;
        public float seconds = 0.05f;

        public HapticPulse() { }
        public HapticPulse(float frequency, float amplitude, float seconds)
        {
            this.frequency = frequency; this.amplitude = amplitude; this.seconds = seconds;
        }
    }

    [Serializable]
    public class HapticSettings
    {
        [Range(0f, 1f)] public float master = 1f;
        public HapticPulse ring = new(0.6f, 0.45f, 0.06f);
        public HapticPulse collision = new(0.3f, 1f, 0.25f);
        public HapticPulse boundsTick = new(0.8f, 0.35f, 0.03f);
        public HapticPulse grabStick = new(0.9f, 0.25f, 0.02f);
        [Tooltip("Constant light rumble while boosting (amplitude).")]
        [Range(0f, 1f)] public float boostRumble = 0.12f;
    }
}
