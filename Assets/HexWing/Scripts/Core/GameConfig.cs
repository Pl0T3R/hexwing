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
        public CombatSettings combat = new();
        public DroneSettings drones = new();
        public AISettings ai = new();
        public SurvivalSettings survival = new();
        public AbilitySettings abilities = new();
        public AssaultSettings assault = new();
        public EscortSettings escort = new();
        public DogfightSettings dogfight = new();
        public HapticSettings haptics = new();
        public VisualSettings visuals = new();
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
        [Tooltip("Bank into yaw turns (flight.coordinatedBank). Off keeps the horizon still in yaw turns.")]
        public bool bankIntoTurns = true;

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
        [Tooltip("Show the raw input (scheme, grips, controller poses, resulting axes) on the left side display.")]
        public bool inputReadout = true;
        [Tooltip("Log one [HexWing] Input: line per second while flying (to find control problems on the headset).")]
        public bool inputLog = true;
    }

    [Serializable]
    public class VisualSettings
    {
        [Tooltip("Use the Blender-made models (Resources/Models) where they exist; off = the old code-built meshes.")]
        public bool useArtModels = true;
        [Tooltip("Screen height (share of the view) below which each LOD hands over to the next: LOD0, LOD1, LOD2.")]
        public float[] lodScreenHeights = { 0.06f, 0.015f, 0.0005f };
    }

    public enum ControlScheme { ControllerDirect, VirtualStick, Thumbsticks }
    /// <summary>What moving the virtual stick (B) or tipping the controller (A) sideways does.</summary>
    public enum StickSideways { Yaw, Roll }
    /// <summary>Thumbstick throttle: Hold keeps the throttle where you leave it, Spring returns to cruise.</summary>
    public enum ThrottleStickMode { Hold, Spring }

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
        public bool invertYaw = false;
        public bool invertRoll = false;
        [Tooltip("Sideways stick (B) or sideways tip (A): Yaw turns the nose (the ship banks into the turn by itself), Roll rolls.")]
        public StickSideways sideways = StickSideways.Yaw;

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
        [Tooltip("Controller tilt (deg) from the neutral pose for full pitch / sideways. The neutral pose is set when the scheme starts and by clicking the flying hand's thumbstick.")]
        public float tiltRange = 30f;
        [Tooltip("Wrist twist (deg) from the neutral pose for full roll (A).")]
        public float tiltTwistRange = 45f;

        [Header("A and C: thumbstick throttle")]
        [Tooltip("Hold (default): pushing the stick up or down changes the throttle and it stays when you let go. Spring: centred = cruise, up = full, down = minimum.")]
        public ThrottleStickMode throttleStick = ThrottleStickMode.Hold;
        [Tooltip("Hold mode: throttle change per second with the stick pushed all the way.")]
        public float throttleStickRate = 0.6f;
        [Tooltip("Starting throttle, and the centre in Spring mode.")]
        [Range(0f, 1f)] public float cruiseThrottle = 0.4f;
        [Tooltip("Spring mode: how fast the throttle follows the stick (1/s).")]
        public float throttleResponse = 3f;

        [Header("Throttle detents (all schemes)")]
        [Tooltip("The throttle sticks to cruise and to the boost line when it comes within this distance, with a haptic click.")]
        public float detentWidth = 0.03f;
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
        [Tooltip("Extra nose lag (s) in the hardest turn at full speed: the ship slides a little through hard turns.")]
        public float driftLag = 0.25f;
        [Tooltip("Bank angle (deg) the ship rolls into when turning with yaw only, like a plane. 0 = off. The player's comfort presets can turn it off.")]
        public float coordinatedBank = 22f;

        [Header("Afterburner (player): full throttle burns it for extra speed")]
        [Tooltip("Top speed multiplier while the afterburner burns.")]
        public float afterburnerMultiplier = 1.45f;
        [Tooltip("Seconds of afterburner from full to empty.")]
        public float afterburnerSeconds = 4f;
        [Tooltip("Seconds to refill from empty, starting after the delay.")]
        public float afterburnerRefill = 8f;
        public float afterburnerRefillDelay = 1.5f;
        [Tooltip("Throttle at or above which the afterburner burns.")]
        [Range(0.9f, 1f)] public float afterburnerFrom = 0.98f;

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

    public enum EvasiveStyle { Jink, Roll }

    [Serializable]
    public class CombatSettings
    {
        [Header("Lasers")]
        [Tooltip("Bolt speed (m/s) on top of the ship's own velocity.")]
        public float laserSpeed = 800f;
        public float laserRange = 1500f;
        public float laserDamage = 12f;
        [Tooltip("Shots per second (the two guns alternate).")]
        public float fireRate = 8f;
        [Tooltip("Heat per shot; 1 = overheated.")]
        public float heatPerShot = 0.055f;
        [Tooltip("Heat lost per second while not firing.")]
        public float coolRate = 0.4f;
        [Tooltip("Seconds the guns are locked after overheating.")]
        public float overheatLockout = 3f;
        [Tooltip("Distance (m) where the two guns' bolts cross, in front of the crosshair.")]
        public float convergence = 400f;
        [Tooltip("Random spread (deg).")]
        public float spread = 0.12f;
        [Tooltip("Length (m) and thickness of a drawn bolt.")]
        public float boltLength = 7f;
        public float boltThickness = 0.28f;

        [Header("Targeting and lock-on")]
        [Tooltip("Targets within this angle of the nose get brackets and the lead marker.")]
        public float targetCone = 25f;
        public float targetRange = 1500f;
        [Tooltip("Hold the lock button with the target this close to the nose (deg) to lock.")]
        public float lockCone = 12f;
        public float lockSeconds = 1.2f;
        [Tooltip("Distance (m) from the eye where the holographic HUD markers are drawn.")]
        public float hudDistance = 30f;

        [Header("Missiles (fired by releasing the lock button while locked)")]
        public float missileSpeed = 260f;
        public float missileTurnRate = 110f;
        public float missileDamage = 60f;
        public float missileCooldown = 6f;
        public float missileLifetime = 6f;
        [Tooltip("Missile explodes when this close to its target (m).")]
        public float missileFuse = 9f;

        [Header("Player ship")]
        public float hull = 100f;
        public float shields = 60f;
        [Tooltip("Seconds without damage before shields recharge.")]
        public float shieldDelay = 4f;
        public float shieldRegen = 20f;
        [Tooltip("Hull damage of a head-on collision (scaled by how head-on it was).")]
        public float collisionDamage = 40f;

        [Header("Evasive (X)")]
        [Tooltip("Jink: a quick sideways hop, the view never rolls (comfortable default). Roll: a full 360° barrel roll.")]
        public EvasiveStyle evasiveStyle = EvasiveStyle.Jink;
        public float evasiveCooldown = 8f;
        public float jinkDistance = 15f;
        public float evasiveSeconds = 0.5f;
        [Tooltip("Seconds after an evasive during which missiles locked on you lose their lock.")]
        public float evasiveBreaksLock = 0.6f;
    }

    [Serializable]
    public class DroneSettings
    {
        public int count = 8;
        public float hull = 36f;
        public float shields = 0f;
        [Tooltip("Speed range (m/s) of the drones' patrol loops.")]
        public Vector2 speed = new(30f, 90f);
        [Tooltip("Radius range (m) of the patrol loops.")]
        public Vector2 loopRadius = new(80f, 220f);
        [Tooltip("Distance range (m) from the player's start where drones patrol.")]
        public Vector2 distance = new(350f, 1100f);
        [Tooltip("Seconds before a destroyed drone is replaced.")]
        public float respawnSeconds = 3f;
        [Tooltip("Collision radius (m) for lasers and missiles.")]
        public float radius = 4f;
    }

    [Serializable]
    public class AssaultSettings
    {
        [Tooltip("Seconds to destroy the capital ship (15 minutes).")]
        public float timeLimit = 900f;
        [Tooltip("Times the player can come back after being shot down (the team's ship pool).")]
        public int playerLives = 5;
        public float respawnDelay = 2f;
        [Tooltip("Enemy fighters kept in the air; more once the turrets are down.")]
        public int enemyFighters = 5;
        public float enemyRespawn = 6f;
        [Range(0f, 1f)] public float enemySkill = 0.5f;
        public int wingmen = 3;
        public float wingmanRespawn = 45f;
        public int scorePerKill = 100;
        public int scorePerGenerator = 500;
        public int scorePerTurret = 250;
        public int scoreReactor = 2000;
    }

    [Serializable]
    public class EscortSettings
    {
        public int transports = 3;
        public float transportSpeed = 30f;
        public float transportHull = 500f;
        public float transportShields = 150f;
        [Tooltip("Metres between transports in the column.")]
        public float spacing = 120f;
        [Tooltip("Ambushes when the lead transport has done this share of the route.")]
        public float[] ambushAt = { 0.15f, 0.42f, 0.7f };
        public int ambushBase = 4;
        [Range(0f, 1f)] public float enemySkill = 0.45f;
        public int playerLives = 5;
        public float respawnDelay = 2f;
        public int wingmen = 2;
        [Header("Asteroid field")]
        public int asteroids = 260;
        public int asteroidSeed = 4;
        public Vector2 asteroidSize = new(8f, 60f);
        [Tooltip("Asteroid-free tube (m) around the convoy's route.")]
        public float corridor = 110f;
        public int scorePerTransport = 1000;
        public int scorePerKill = 100;
    }

    [Serializable]
    public class DogfightSettings
    {
        [Tooltip("Ships per team, the player included (offline: the rest are bots; online in M7: players first).")]
        public int teamSize = 4;
        public int killsToWin = 30;
        [Tooltip("Seconds; when time runs out the team with more kills wins.")]
        public float timeLimit = 600f;
        public float playerRespawn = 3f;
        public float botRespawn = 5f;
        [Range(0f, 1f)] public float allySkill = 0.55f;
        [Range(0f, 1f)] public float enemySkill = 0.5f;
        [Tooltip("Distance (m) of each team's base from the arena centre.")]
        public float baseDistance = 1300f;
        public float spawnSpread = 120f;
        public float botHull = 70f;
        public float botShields = 40f;
        public int scorePerKill = 100;
        public int scoreWin = 1000;
    }

    /// <summary>The six abilities (PLAN.md section 7). Which ship has which is in its ShipDefinition.</summary>
    [Serializable]
    public class AbilitySettings
    {
        [Header("Targeting computer: no gun heat and more damage for a while")]
        public float targetingCooldown = 20f;
        public float targetingSeconds = 5f;
        public float targetingDamage = 1.3f;

        [Header("Pursuit missile: one fast-turning missile at the target, no lock needed")]
        public float pursuitCooldown = 14f;
        public float pursuitDamage = 80f;
        public float pursuitTurnScale = 1.7f;

        [Header("Homing missiles: hold the target in the lock cone, then three missiles")]
        public float homingCooldown = 12f;
        public float homingLockSeconds = 1.5f;
        public int homingCount = 3;
        public float homingDamage = 40f;

        [Header("Repair: hull back over a few seconds")]
        public float repairCooldown = 25f;
        public float repairAmount = 40f;
        public float repairSeconds = 3f;

        [Header("Bomb run: bombs dropped along the flight path, each with a blast")]
        public float bombCooldown = 18f;
        public int bombCount = 6;
        public float bombInterval = 0.18f;
        public float bombDamage = 70f;
        public float bombBlastRadius = 45f;
        public float bombSpeed = 90f;
        public float bombFuse = 1.6f;

        [Header("Turret drone: a gun turret left behind that shoots enemies")]
        public float turretCooldown = 22f;
        public float turretSeconds = 15f;
        public float turretRange = 700f;
        public float turretFireRate = 4f;
        public float turretDamage = 8f;
        [Tooltip("Aim error (deg) of the turret.")]
        public float turretAimError = 2.5f;
    }

    /// <summary>AI pilot skill: each value goes from the "rookie" end (skill 0) to the "ace" end (skill 1).</summary>
    [Serializable]
    public class AISettings
    {
        [Tooltip("Aim error (deg), rookie → ace.")]
        public Vector2 aimError = new(4.5f, 1.2f);
        [Tooltip("Seconds before reacting to a new situation, rookie → ace.")]
        public Vector2 reaction = new(0.7f, 0.2f);
        [Tooltip("Fires when the lead point is within this angle (deg), rookie → ace.")]
        public Vector2 fireCone = new(7f, 3.5f);
        public float fireRange = 900f;
        [Tooltip("Seconds of fire, then a pause (rookie → ace pause).")]
        public float burstSeconds = 1.1f;
        public Vector2 burstPause = new(1.2f, 0.4f);
        [Tooltip("Chance to break away when hit or chased, rookie → ace.")]
        public Vector2 evadeChance = new(0.25f, 0.75f);
        [Tooltip("Laser damage relative to the player's, rookie → ace (keeps early waves fair).")]
        public Vector2 damageScale = new(0.35f, 0.8f);
        [Tooltip("Turn rates relative to the player's ship, rookie → ace.")]
        public Vector2 turnScale = new(0.75f, 1.0f);
        [Tooltip("Breaks off when closer than this to its target (m), then comes around again.")]
        public float breakOffDistance = 140f;
        [Tooltip("Seconds of flying away before turning back in.")]
        public float extendSeconds = 2.2f;
        [Tooltip("Decisions per second (steering runs every frame).")]
        public float thinkRate = 10f;
        [Tooltip("Wingmen hold this offset from the player (m: right, up, back) when they have no target.")]
        public Vector3 wingOffset = new(45f, 8f, -35f);
    }

    [Serializable]
    public class SurvivalSettings
    {
        [Tooltip("Allied AI fighters flying with the player; they come back at every wave.")]
        public int wingmen = 2;
        [Tooltip("Enemies in wave n: first + perWave × (n − 1), up to max alive at once.")]
        public int firstWave = 3;
        public float perWave = 1f;
        public int maxAlive = 12;
        [Tooltip("Every n-th wave has an elite (tougher, faster, fires missiles).")]
        public int eliteEvery = 5;
        [Tooltip("Wave at which enemy skill reaches 1 (ace).")]
        public int fullSkillWave = 12;
        public float waveGap = 4f;
        public Vector2 spawnDistance = new(1300f, 1800f);
        [Tooltip("Spawns stay within this angle of the player's nose, so nobody appears right behind you.")]
        public float spawnCone = 70f;
        public float enemyHull = 48f;
        public float enemyShields = 0f;
        public float eliteHull = 130f;
        public float eliteShields = 60f;
        public float wingmanHull = 80f;
        public float wingmanShields = 40f;
        public int scorePerKill = 100;
        public int scorePerElite = 500;
        public int scorePerWave = 250;
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
        public HapticPulse fire = new(0.9f, 0.3f, 0.025f);
        public HapticPulse overheat = new(0.25f, 0.8f, 0.4f);
        public HapticPulse hitTaken = new(0.35f, 0.9f, 0.18f);
        public HapticPulse hitDealt = new(0.95f, 0.2f, 0.02f);
        public HapticPulse kill = new(0.6f, 0.6f, 0.12f);
        public HapticPulse lockTick = new(1f, 0.15f, 0.015f);
        public HapticPulse locked = new(0.8f, 0.4f, 0.08f);
        public HapticPulse missile = new(0.4f, 0.7f, 0.15f);
        public HapticPulse evasive = new(0.5f, 0.5f, 0.1f);
        public HapticPulse missileWarning = new(0.2f, 0.35f, 0.08f);
        public HapticPulse detent = new(0.9f, 0.35f, 0.02f);
        [Tooltip("Constant light rumble while boosting (amplitude).")]
        [Range(0f, 1f)] public float boostRumble = 0.12f;
        [Tooltip("Rumble while the afterburner burns (amplitude).")]
        [Range(0f, 1f)] public float afterburnerRumble = 0.28f;
    }
}
