using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player's ship. Builds the cockpit, puts the camera rig at the eye point so the ship turns
    /// around the pilot's eyes (no sideways swing of the head when turning), and creates the display
    /// texts (<see cref="CockpitHud"/> fills them). The rig is a child of the ship: the head stays the only
    /// thing that moves the view inside the cockpit.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerCockpit : MonoBehaviour
    {
        public static PlayerCockpit Instance { get; private set; }

        public OVRCameraRig Rig { get; private set; }
        public Transform Head => Rig != null ? Rig.centerEyeAnchor : transform;
        public CockpitFactory.Displays Displays { get; private set; }

        public CockpitDisplay Center { get; private set; }
        public CockpitDisplay Left { get; private set; }
        public CockpitDisplay Right { get; private set; }
        public CockpitDisplay SideLeft { get; private set; }
        public CockpitDisplay SideRight { get; private set; }
        public CockpitDisplay Overhead { get; private set; }

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
            Center = CockpitDisplay.Create(Displays.Center, cyan);
            Left = CockpitDisplay.Create(Displays.Left, cyan);
            Right = CockpitDisplay.Create(Displays.Right, cyan);
            SideLeft = CockpitDisplay.Create(Displays.SideLeft, cyan, 0.011f);
            SideRight = CockpitDisplay.Create(Displays.SideRight, cyan, 0.011f);
            var overheadLine = new GameObject("Stats Line").transform;   // the text above the progress bar
            overheadLine.SetParent(Displays.Overhead, false);
            overheadLine.localPosition = new Vector3(0f, 0.011f, 0f);
            Overhead = CockpitDisplay.Create(overheadLine, new Color(1f, 0.78f, 0.42f), 0.022f);
            Overhead.FitWidth(0.42f);
            gameObject.AddComponent<CockpitInstrumentPanel>();   // radar blips and bar graphs on the Blender cockpit
        }

        void Start()
        {
            if (Rig == null) return;
            if (TryGetComponent(out ScreenFader fader)) fader.Attach(Rig.centerEyeAnchor);
            if (TryGetComponent(out ComfortVignette vignette)) vignette.Attach(Rig.centerEyeAnchor, GetComponent<ShipFlight>());
        }
    }
}
