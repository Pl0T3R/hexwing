using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>One line of a menu: its text (asked again after every choice) and what choosing it does.</summary>
    public class MenuEntry
    {
        public System.Func<string> Label;
        public System.Action Select;

        public MenuEntry(System.Func<string> label, System.Action select)
        {
            Label = label;
            Select = select;
        }

        public MenuEntry(string label, System.Action select) : this(() => label, select) { }
    }

    /// <summary>
    /// A holographic menu panel seen through the canopy (1.5 m ahead of the eye, inside the window's clear
    /// centre), pointed at with a laser from the flying hand: the line under the beam lights up, the
    /// trigger chooses it. The thumbstick (up/down) and A work too, for anyone who prefers not to point.
    /// Runs on real time, so it works while the game is paused.
    /// </summary>
    public class MenuPanel : MonoBehaviour
    {
        const int MaxRows = 9;
        const float Width = 1.0f, Height = 0.86f, RowHeight = 0.075f, Distance = 1.5f;

        public bool Visible { get; private set; }

        Transform _panel, _beam, _dot, _highlight;
        CockpitDisplay _title;
        readonly CockpitDisplay[] _rows = new CockpitDisplay[MaxRows];
        readonly List<MenuEntry> _items = new();
        int _hover = -1, _selected, _colored = -2;
        bool _trigger, _stickMoved;
        PlayerCockpit _cockpit;
        PlayerInput _input;

        void Start()
        {
            _cockpit = GetComponent<PlayerCockpit>();
            _input = GetComponent<PlayerInput>();
            Build();
            SetVisible(false);
        }

        void Build()
        {
            var eye = GameConfig.Instance.cockpit.eyePoint;
            _panel = new GameObject("Menu Panel").transform;
            _panel.SetParent(transform, false);
            _panel.localPosition = eye + new Vector3(0f, -0.06f, Distance);
            _panel.localRotation = Quaternion.identity;   // +Z away from the eye, like the cockpit displays

            var back = new MeshKit.Builder();
            back.Quad(new Vector3(-Width / 2, -Height / 2, 0.01f), new Vector3(Width / 2, -Height / 2, 0.01f),
                      new Vector3(Width / 2, Height / 2, 0.01f), new Vector3(-Width / 2, Height / 2, 0.01f), new Vector3(0f, 0f, 1f));
            MeshKit.Part("Background", _panel, back.ToMesh("Menu Background"), Mats.Get(Mats.MenuBack));

            var bar = new MeshKit.Builder();
            bar.Quad(new Vector3(-Width / 2 + 0.03f, -RowHeight * 0.45f, 0.005f), new Vector3(Width / 2 - 0.03f, -RowHeight * 0.45f, 0.005f),
                     new Vector3(Width / 2 - 0.03f, RowHeight * 0.45f, 0.005f), new Vector3(-Width / 2 + 0.03f, RowHeight * 0.45f, 0.005f), new Vector3(0f, 0f, 1f));
            _highlight = MeshKit.Part("Highlight", _panel, bar.ToMesh("Menu Highlight"), Mats.Get(Mats.HudCyan)).transform;

            var titleAnchor = new GameObject("Title").transform;
            titleAnchor.SetParent(_panel, false);
            titleAnchor.localPosition = new Vector3(0f, Height / 2 - 0.06f, 0f);
            _title = CockpitDisplay.Create(titleAnchor, new Color(1f, 0.7f, 0.28f), 0.05f);
            for (int i = 0; i < MaxRows; i++)
            {
                var a = new GameObject($"Row {i + 1}").transform;
                a.SetParent(_panel, false);
                a.localPosition = new Vector3(0f, RowY(i), 0f);
                _rows[i] = CockpitDisplay.Create(a, Color.white, 0.04f);
            }

            var beam = new MeshKit.Builder();
            beam.Box(new Vector3(0f, 0f, 0.5f), new Vector3(0.004f, 0.004f, 1f), Quaternion.identity);
            _beam = MeshKit.Part("Pointer Beam", transform, beam.ToMesh("Pointer Beam"), Mats.Get(Mats.HudCyan)).transform;
            var dot = new MeshKit.Builder();
            dot.Ico(Vector3.zero, Vector3.one * 0.012f);
            _dot = MeshKit.Part("Pointer Dot", transform, dot.ToMesh("Pointer Dot"), Mats.Get(Mats.HudWhite)).transform;
        }

        static float RowY(int i) => Height / 2 - 0.15f - i * RowHeight;

        public void Show(string title, List<MenuEntry> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _title.SetText(title);
            _selected = 0;   // a new page starts at its first line, so a quick second press can't hit an unrelated row
            _colored = -2;
            Refresh();
            SetVisible(true);
        }

        public void Hide() => SetVisible(false);

        void SetVisible(bool on)
        {
            Visible = on;
            if (_panel != null) _panel.gameObject.SetActive(on);
            if (_beam != null) _beam.gameObject.SetActive(on);
            if (_dot != null) _dot.gameObject.SetActive(on);
            _trigger = true;   // a trigger held while the menu opens doesn't choose anything
        }

        /// <summary>Re-reads every label (after a choice changed something).</summary>
        public void Refresh()
        {
            for (int i = 0; i < MaxRows; i++)
                _rows[i].SetText(i < _items.Count ? _items[i].Label() : "");
        }

        void Update()
        {
            if (!Visible || _cockpit == null || _cockpit.Rig == null) return;
            var hand = _input != null ? _input.FlyHand : OVRInput.Controller.RTouch;
            var anchor = hand == OVRInput.Controller.LTouch ? _cockpit.Rig.leftControllerAnchor : _cockpit.Rig.rightControllerAnchor;

            // laser: ray from the controller onto the panel's plane
            _hover = -1;
            float length = 3f;
            var plane = new Plane(-_panel.forward, _panel.position);
            var ray = new Ray(anchor.position, anchor.forward);
            if (plane.Raycast(ray, out float enter) && enter > 0f)
            {
                var hit = ray.GetPoint(enter);
                var local = _panel.InverseTransformPoint(hit);
                if (Mathf.Abs(local.x) <= Width / 2 && Mathf.Abs(local.y) <= Height / 2)
                {
                    length = enter;
                    for (int i = 0; i < _items.Count && i < MaxRows; i++)
                        if (Mathf.Abs(local.y - RowY(i)) <= RowHeight / 2) { _hover = i; break; }
                    _dot.position = hit;
                }
            }
            _beam.SetPositionAndRotation(anchor.position, anchor.rotation);
            _beam.localScale = new Vector3(1f, 1f, length);
            _dot.gameObject.SetActive(length < 3f);
            if (_hover >= 0 && _hover != _selected)
            {
                _selected = _hover;
                Haptics.Play(hand, GameConfig.Instance.haptics.lockTick);
            }

            // thumbstick and A as an alternative
            float y = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, hand).y;
            if (Mathf.Abs(y) > 0.7f && !_stickMoved && _items.Count > 0)
            {
                _selected = (_selected + (y < 0f ? 1 : -1) + _items.Count) % _items.Count;
                _stickMoved = true;
                Haptics.Play(hand, GameConfig.Instance.haptics.lockTick);
            }
            else if (Mathf.Abs(y) < 0.3f) _stickMoved = false;

            _highlight.gameObject.SetActive(_selected >= 0 && _selected < _items.Count);
            if (_selected >= 0) _highlight.localPosition = new Vector3(0f, RowY(_selected), 0f);
            if (_colored != _selected)
            {
                // only when the selection moves: setting a TextMesh colour rebuilds its mesh
                _colored = _selected;
                for (int i = 0; i < MaxRows; i++) _rows[i].SetColor(i == _selected ? new Color(0.02f, 0.05f, 0.08f) : Color.white);
            }

            bool trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, hand) > 0.6f;
            bool pressed = (trigger && !_trigger) || OVRInput.GetDown(OVRInput.Button.One, hand);
            _trigger = trigger;
            if (pressed && _selected >= 0 && _selected < _items.Count)
            {
                Haptics.Play(hand, GameConfig.Instance.haptics.grabStick);
                ShipAudio.Instance?.PlayHitDealt();
                var item = _items[_selected];
                item.Select?.Invoke();
                if (Visible) Refresh();
            }
        }
    }
}
