using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// In the hangar, the selected ship class turns slowly on a lit pad to the right of the menu panel
    /// (the Blender-made Falcon, Hawk and Bison models, about 30 cm long there, inside the cockpit like the panel). Hidden in the pause menu
    /// and in flight; nothing is shown when the models are missing. Runs on real time (the game is paused
    /// in the menu).
    /// </summary>
    public class HangarShowcase : MonoBehaviour
    {
        const float Scale = 0.03f, TurnSpeed = 25f;
        static readonly Vector3 Offset = new(0.5f, -0.02f, 0.5f);   // from the eye: right of the panel, about 45 degrees

        Transform _root, _turntable;
        readonly Transform[] _ships = new Transform[3];
        int _shown = -1;

        void Start()
        {
            _root = new GameObject("Hangar Showcase").transform;
            _root.SetParent(transform, false);
            _root.localPosition = GameConfig.Instance.cockpit.eyePoint + Offset;
            _root.localRotation = Quaternion.Euler(0f, -45f, 0f);   // turned toward the pilot
            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(_root, false);
            string[] names = { "Falcon", "Hawk", "Bison" };
            for (int i = 0; i < names.Length; i++)
            {
                var art = ArtLibrary.Get(names[i]);
                if (art == null) continue;
                var ship = ArtLibrary.Attach(art, _turntable, Mats.Get(i == 0 ? Mats.PlumeCyan : i == 1 ? Mats.PlumeGreen : Mats.PlumeAmber));
                ship.localScale = Vector3.one * Scale;
                ship.gameObject.SetActive(false);
                _ships[i] = ship;
            }
            // the pad: a thin glowing ring under the ship
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, -0.08f, 0f), new Vector3(0f, -0.077f, 0f), 0.17f, 0.17f, 24);
            MeshKit.Part("Pad", _root, b.ToMesh("Hangar Pad"), Mats.Get(Mats.Accent));
            _root.gameObject.SetActive(false);
        }

        void Update()
        {
            var menu = GameMenu.Instance;
            bool show = menu != null && menu.Open && menu.InHangar && PlayerShip.Instance != null;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;
            int index = (int)PlayerShip.Instance.Definition.shipClass;
            if (index != _shown)
            {
                _shown = index;
                for (int i = 0; i < _ships.Length; i++)
                    if (_ships[i] != null) _ships[i].gameObject.SetActive(i == index);
            }
            _turntable.localRotation = Quaternion.Euler(0f, Time.unscaledTime * TurnSpeed, 0f);
        }
    }
}
