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
    }

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
}
