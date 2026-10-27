using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player's ship (M1: a parked cockpit). Builds the cockpit, puts the camera rig at the eye point
    /// so the ship turns around the pilot's eyes (no sideways swing of the head when turning), and fills
    /// the displays. The rig is a child of the ship: the head stays the only thing that moves the view
    /// inside the cockpit.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerCockpit : MonoBehaviour
    {
        public static PlayerCockpit Instance { get; private set; }

        public OVRCameraRig Rig { get; private set; }
        public Transform Head => Rig != null ? Rig.centerEyeAnchor : transform;
        public CockpitFactory.Displays Displays { get; private set; }

        CockpitDisplay _center, _left, _right;
        float _nextReadout;

        void Awake()
        {
            Instance = this;
            Displays = CockpitFactory.Build(transform);

            Rig = FindFirstObjectByType<OVRCameraRig>();
            if (Rig == null) Debug.LogError("[HexWing] No OVRCameraRig in the scene. Run HexWing > Build Game.");
            else
            {
                Rig.transform.SetParent(transform, false);
                Rig.transform.localPosition = GameConfig.Instance.cockpit.eyePoint;
                Rig.transform.localRotation = Quaternion.identity;
            }

            var cyan = new Color(0.37f, 0.83f, 1f);
            _center = CockpitDisplay.Create(Displays.Center, cyan);
            _left = CockpitDisplay.Create(Displays.Left, cyan);
            _right = CockpitDisplay.Create(Displays.Right, cyan);
            _left.SetText("KADŁUB  100%\nOSŁONY  --");
            _right.SetText("RADAR\n(M3)");
        }

        void Start()
        {
            var fader = GetComponent<ScreenFader>();
            if (fader != null && Rig != null) fader.Attach(Rig.centerEyeAnchor);
        }

        void Update()
        {
            var debug = GameConfig.Instance.debug;
            if (Time.unscaledTime < _nextReadout) return;
            _nextReadout = Time.unscaledTime + debug.readoutInterval;
            var perf = PerformanceManager.Instance;
            _center.SetText(debug.performanceReadout && perf != null ? "HEX WING  M1\n" + perf.Readout : "HEX WING");
        }
    }
}
