using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// One tile of a menu: its text (asked again after every choice; "KEY: value" shows the key small above
    /// the value) and what choosing it does (null = a read-only tile, e.g. a stat).
    /// </summary>
    public class MenuEntry
    {
        public System.Func<string> Label;
        public System.Action Select;
        /// <summary>The main action of the page (START, RESUME): a wide amber tile across the panel.</summary>
        public bool Primary;

        public MenuEntry AsPrimary()
        {
            Primary = true;
            return this;
        }

        public MenuEntry(System.Func<string> label, System.Action select)
        {
            Label = label;
            Select = select;
        }

        public MenuEntry(string label, System.Action select) : this(() => label, select) { }
    }


    /// <summary>
    /// The holographic menu panel inside the cockpit, in the style of its instruments: dark glass with chamfered
    /// corners, a cyan frame and amber accents, and the entries as tiles in three columns (the page's main
    /// action as a wide amber tile on top). It floats 0.74 m from the eye, a little above eye level: in front
    /// of the window frame (the spokes and hub ring can't cut it) and above the scope pod on the console.
    /// Pointed at with a laser from the flying hand: the tile under the beam lights up, the trigger chooses
    /// it. The thumbstick moves between tiles and A chooses, for anyone who prefers not to point.
    /// The layers draw in a fixed order (Mats.MenuQueue). Runs on real time, so it works while paused.
    /// </summary>
    public class MenuPanel : MonoBehaviour
    {
        const int MaxTiles = 12, Columns = 3;
        const float Distance = 0.74f, Elevation = 12f;
        const float TileW = 0.2f, TileH = 0.058f, Gap = 0.012f, Margin = 0.03f, Header = 0.08f;
        const float GridW = Columns * TileW + (Columns - 1) * Gap, Width = GridW + 2f * Margin;
        const float PanelChamfer = 0.035f, TileChamfer = 0.014f;
        static readonly Color KeyColor = new(0.55f, 0.82f, 0.95f), KeyHot = new(1f, 0.78f, 0.4f);
        static readonly Color ValueColor = new(0.96f, 0.98f, 1f), PrimaryValue = new(1f, 0.88f, 0.62f);

        public bool Visible { get; private set; }

        class Tile
        {
            public Transform Root;
            public MeshFilter PlateMesh, EdgeMesh;
            public MeshRenderer Plate, Edge;
            public CockpitDisplay Key, Value;
            public Rect Rect;
        }

        Transform _panel, _beam, _dot;
        MeshFilter _back, _frame, _accents;
        CockpitDisplay _title, _hint;
        readonly Tile[] _tiles = new Tile[MaxTiles];
        readonly List<MenuEntry> _items = new();
        readonly Dictionary<int, Mesh[]> _panelMeshes = new();   // by height in mm: plate, frame, accents
        Mesh _plate, _plateWide, _edge, _edgeWide;
        Material _tile, _tileHot, _tilePrimary, _edgeDim, _edgeHot;
        float _height;
        int _hover = -1, _selected = -1, _colored = -2;
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
            var dir = Quaternion.Euler(-Elevation, 0f, 0f) * Vector3.forward;
            _panel = new GameObject("Menu Panel").transform;
            _panel.SetParent(transform, false);
            _panel.localPosition = eye + dir * Distance;
            _panel.localRotation = Quaternion.LookRotation(dir, Vector3.up);   // +Z away from the eye, facing it

            _tile = Mats.Get(Mats.MenuTile);
            _tileHot = Mats.Get(Mats.MenuTileHot);
            _tilePrimary = Mats.Get(Mats.MenuTilePrimary);
            _edgeDim = Mats.Get(Mats.MenuEdgeDim);
            _edgeHot = Mats.Get(Mats.MenuEdgeHot);
            _back = MeshKit.Part("Glass", _panel, null, Mats.Get(Mats.MenuBack)).GetComponent<MeshFilter>();
            _frame = MeshKit.Part("Frame", _panel, null, Mats.Get(Mats.MenuEdge)).GetComponent<MeshFilter>();
            _accents = MeshKit.Part("Accents", _panel, null, _edgeHot).GetComponent<MeshFilter>();

            var tile = TileOutline(TileW, TileH);
            var wide = TileOutline(GridW, TileH);
            _plate = Fill("Menu Tile", tile, 0.002f);
            _plateWide = Fill("Menu Tile Wide", wide, 0.002f);
            _edge = Outline("Menu Tile Edge", tile, 0.0022f, 0.001f);
            _edgeWide = Outline("Menu Tile Edge Wide", wide, 0.0022f, 0.001f);

            _title = CockpitDisplay.Create(Anchor("Title", _panel), new Color(1f, 0.72f, 0.3f), 0.03f, Mats.MenuTextQueue);
            _hint = CockpitDisplay.Create(Anchor("Hint", _panel), KeyColor, 0.0105f, Mats.MenuTextQueue);
            _hint.SetText("POINT + TRIGGER   ·   STICK + A");
            for (int i = 0; i < MaxTiles; i++)
            {
                var t = new Tile { Root = new GameObject($"Tile {i + 1}").transform };
                t.Root.SetParent(_panel, false);
                t.Plate = MeshKit.Part("Plate", t.Root, _plate, _tile).GetComponent<MeshRenderer>();
                t.PlateMesh = t.Plate.GetComponent<MeshFilter>();
                t.Edge = MeshKit.Part("Edge", t.Root, _edge, _edgeDim).GetComponent<MeshRenderer>();
                t.EdgeMesh = t.Edge.GetComponent<MeshFilter>();
                t.Key = CockpitDisplay.Create(Anchor("Key", t.Root), KeyColor, 0.0115f, Mats.MenuTextQueue);
                t.Value = CockpitDisplay.Create(Anchor("Value", t.Root), ValueColor, 0.019f, Mats.MenuTextQueue);
                _tiles[i] = t;
            }

            var beam = new MeshKit.Builder();
            beam.Box(new Vector3(0f, 0f, 0.5f), new Vector3(0.003f, 0.003f, 1f), Quaternion.identity);
            _beam = MeshKit.Part("Pointer Beam", transform, beam.ToMesh("Pointer Beam"), Mats.Get(Mats.HudCyan)).transform;
            var dot = new MeshKit.Builder();
            dot.Ico(Vector3.zero, Vector3.one * 0.008f);
            _dot = MeshKit.Part("Pointer Dot", transform, dot.ToMesh("Pointer Dot"), Mats.Get(Mats.HudWhite)).transform;
        }

        static Transform Anchor(string name, Transform parent)
        {
            var a = new GameObject(name).transform;
            a.SetParent(parent, false);
            return a;
        }

        public void Show(string title, List<MenuEntry> items)
        {
            _items.Clear();
            for (int i = 0; i < items.Count && i < MaxTiles; i++) _items.Add(items[i]);
            _title.SetText(title);
            Layout();
            _selected = FirstSelectable();   // a new page starts at its first tile, so a quick second press can't hit an unrelated one
            _colored = -2;
            Refresh();
            SetVisible(true);
        }

        public void Hide() => SetVisible(false);

        /// <summary>Places the tiles (three per row, a primary one alone in its row) and sizes the panel to fit.</summary>
        void Layout()
        {
            int row = 0, col = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Primary)
                {
                    if (col > 0) { row++; col = 0; }
                    _tiles[i].Rect = new Rect(-GridW / 2f, row, GridW, TileH);
                    row++;
                }
                else
                {
                    _tiles[i].Rect = new Rect(-GridW / 2f + col * (TileW + Gap), row, TileW, TileH);
                    if (++col == Columns) { row++; col = 0; }
                }
            }
            int rows = row + (col > 0 ? 1 : 0);
            float height = Margin * 0.6f + Header + Mathf.Max(1, rows) * (TileH + Gap) - Gap + Margin;
            float top = height / 2f - Margin * 0.6f - Header;
            _height = height;

            for (int i = 0; i < MaxTiles; i++)
            {
                var t = _tiles[i];
                bool used = i < _items.Count;
                t.Root.gameObject.SetActive(used);
                if (!used) continue;
                // Rect.y held the row: turn it into the tile's lower edge on the panel
                float y = top - t.Rect.y * (TileH + Gap) - TileH;
                t.Rect = new Rect(t.Rect.x, y, t.Rect.width, TileH);
                t.Root.localPosition = new Vector3(t.Rect.center.x, t.Rect.center.y, 0f);
                bool wide = t.Rect.width > TileW + 0.01f;
                t.PlateMesh.sharedMesh = wide ? _plateWide : _plate;
                t.EdgeMesh.sharedMesh = wide ? _edgeWide : _edge;
                t.Key.FitWidth(t.Rect.width - 0.02f);
                t.Value.FitWidth(t.Rect.width - 0.02f);
            }

            _title.transform.parent.localPosition = new Vector3(0f, height / 2f - 0.034f, 0f);
            _hint.transform.parent.localPosition = new Vector3(0f, height / 2f - 0.066f, 0f);
            int key = Mathf.RoundToInt(height * 1000f);
            if (!_panelMeshes.TryGetValue(key, out var meshes))
            {
                var outline = Octagon(Width, height, PanelChamfer);
                meshes = new[] { Fill("Menu Glass", outline, 0.004f), Outline("Menu Frame", outline, 0.004f, 0.003f), Accents(height) };
                _panelMeshes[key] = meshes;
            }
            _back.sharedMesh = meshes[0];
            _frame.sharedMesh = meshes[1];
            _accents.sharedMesh = meshes[2];
        }

        int FirstSelectable()
        {
            for (int i = 0; i < _items.Count; i++) if (_items[i].Select != null) return i;
            return -1;
        }

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
            for (int i = 0; i < _items.Count; i++)
            {
                var t = _tiles[i];
                string label = _items[i].Label() ?? "";
                int colon = label.IndexOf(": ", System.StringComparison.Ordinal);
                string key = colon > 0 ? label.Substring(0, colon) : "";
                string value = colon > 0 ? label.Substring(colon + 2) : label;
                t.Key.SetText(key);
                t.Value.SetText(value);
                // a lone value sits in the middle; with a key, the key on top and the value below
                t.Key.transform.parent.localPosition = new Vector3(0f, 0.0125f, 0f);
                t.Value.transform.parent.localPosition = new Vector3(0f, key.Length > 0 ? -0.006f : 0f, 0f);
            }
        }

        void Update()
        {
            if (!Visible || _cockpit == null || _cockpit.Rig == null) return;
            var hand = _input != null ? _input.FlyHand : OVRInput.Controller.RTouch;
            var anchor = hand == OVRInput.Controller.LTouch ? _cockpit.Rig.leftControllerAnchor : _cockpit.Rig.rightControllerAnchor;

            // laser: ray from the controller onto the panel's plane
            _hover = -1;
            float length = 1.5f;
            var plane = new Plane(-_panel.forward, _panel.position);
            var ray = new Ray(anchor.position, anchor.forward);
            bool onPanel = false;
            if (plane.Raycast(ray, out float enter) && enter > 0f && enter < 1.5f)
            {
                var hit = ray.GetPoint(enter);
                Vector2 local = _panel.InverseTransformPoint(hit);
                for (int i = 0; i < _items.Count; i++)
                    if (_items[i].Select != null && _tiles[i].Rect.Contains(local)) { _hover = i; break; }
                if (Mathf.Abs(local.x) <= Width / 2f && Mathf.Abs(local.y) <= _height / 2f)
                {
                    onPanel = true;
                    length = enter;
                    _dot.position = hit;
                }
            }
            _beam.SetPositionAndRotation(anchor.position, anchor.rotation);
            _beam.localScale = new Vector3(1f, 1f, length);
            _dot.gameObject.SetActive(onPanel);
            if (_hover >= 0 && _hover != _selected)
            {
                _selected = _hover;
                Haptics.Play(hand, GameConfig.Instance.haptics.lockTick);
            }

            // thumbstick: to the nearest tile in that direction
            var stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, hand);
            if (stick.magnitude > 0.7f && !_stickMoved)
            {
                _stickMoved = true;
                var dir = Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2(Mathf.Sign(stick.x), 0f) : new Vector2(0f, Mathf.Sign(stick.y));
                int next = Neighbour(dir);
                if (next >= 0)
                {
                    _selected = next;
                    Haptics.Play(hand, GameConfig.Instance.haptics.lockTick);
                }
            }
            else if (stick.magnitude < 0.3f) _stickMoved = false;

            if (_colored != _selected)
            {
                // only when the selection moves: setting a text colour rebuilds its mesh
                _colored = _selected;
                for (int i = 0; i < _items.Count; i++)
                {
                    var t = _tiles[i];
                    bool hot = i == _selected, primary = _items[i].Primary, active = _items[i].Select != null;
                    t.Plate.sharedMaterial = primary ? _tilePrimary : hot ? _tileHot : _tile;
                    t.Edge.sharedMaterial = hot || primary ? _edgeHot : _edgeDim;
                    t.Edge.enabled = active || primary;
                    t.Key.SetColor(hot ? KeyHot : KeyColor);
                    t.Value.SetColor(primary ? (hot ? Color.white : PrimaryValue) : ValueColor);
                }
            }

            bool trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, hand) > 0.6f;
            bool pressed = (trigger && !_trigger) || OVRInput.GetDown(OVRInput.Button.One, hand);
            _trigger = trigger;
            if (pressed && _selected >= 0 && _selected < _items.Count && _items[_selected].Select != null)
            {
                Haptics.Play(hand, GameConfig.Instance.haptics.grabStick);
                ShipAudio.Instance?.PlayHitDealt();
                _items[_selected].Select.Invoke();
                if (Visible) Refresh();
            }
        }

        /// <summary>The closest selectable tile in a direction from the selected one (sideways offsets count double).</summary>
        int Neighbour(Vector2 dir)
        {
            if (_selected < 0) return FirstSelectable();
            var from = _tiles[_selected].Rect.center;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _items.Count; i++)
            {
                if (i == _selected || _items[i].Select == null) continue;
                var d = _tiles[i].Rect.center - from;
                float along = Vector2.Dot(d, dir);
                if (along < 0.01f) continue;
                float score = along + 2f * Mathf.Abs(d.x * dir.y - d.y * dir.x);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // --- meshes (panel space: x right, y up, +z away from the eye) ---------------------------------

        static Vector2[] Octagon(float w, float h, float c) => new[]
        {
            new Vector2(-w / 2 + c, -h / 2), new Vector2(w / 2 - c, -h / 2), new Vector2(w / 2, -h / 2 + c), new Vector2(w / 2, h / 2 - c),
            new Vector2(w / 2 - c, h / 2), new Vector2(-w / 2 + c, h / 2), new Vector2(-w / 2, h / 2 - c), new Vector2(-w / 2, -h / 2 + c),
        };

        /// <summary>A tile: the top-left and bottom-right corners cut, like the cockpit's display frames.</summary>
        static Vector2[] TileOutline(float w, float h) => new[]
        {
            new Vector2(-w / 2, -h / 2), new Vector2(w / 2 - TileChamfer, -h / 2), new Vector2(w / 2, -h / 2 + TileChamfer),
            new Vector2(w / 2, h / 2), new Vector2(-w / 2 + TileChamfer, h / 2), new Vector2(-w / 2, h / 2 - TileChamfer),
        };

        static Mesh Fill(string name, Vector2[] pts, float z)
        {
            var v = new List<Vector3> { new(0f, 0f, z) };
            var t = new List<int>();
            for (int i = 0; i < pts.Length; i++)
            {
                v.Add(new Vector3(pts[i].x, pts[i].y, z));
                t.Add(0); t.Add(1 + (i + 1) % pts.Length); t.Add(1 + i);
            }
            return ToMesh(name, v, t);
        }

        /// <summary>A band of this width just inside a convex outline (counter-clockwise), mitred at the corners.</summary>
        static Mesh Outline(string name, Vector2[] pts, float width, float z)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            int n = pts.Length;
            for (int i = 0; i < n; i++)
            {
                var p = pts[i];
                var n0 = Inward(pts[(i + n - 1) % n], p);
                var n1 = Inward(p, pts[(i + 1) % n]);
                var miter = (n0 + n1).normalized;
                var inner = p + miter * (width / Mathf.Max(0.3f, Vector2.Dot(miter, n0)));
                v.Add(new Vector3(p.x, p.y, z));
                v.Add(new Vector3(inner.x, inner.y, z));
            }
            for (int i = 0; i < n; i++)
            {
                int a = 2 * i, b = 2 * ((i + 1) % n);
                t.Add(a); t.Add(a + 1); t.Add(b);
                t.Add(b); t.Add(a + 1); t.Add(b + 1);
            }
            return ToMesh(name, v, t);
        }

        static Vector2 Inward(Vector2 a, Vector2 b)
        {
            var e = (b - a).normalized;
            return new Vector2(-e.y, e.x);
        }

        /// <summary>The amber accents: a bar under the title and thick strips along two cut corners.</summary>
        static Mesh Accents(float height)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            float z = 0.0025f, y = height / 2f - Margin * 0.6f - Header + 0.008f;
            Strip(v, t, new Vector2(-0.09f, y), new Vector2(0.09f, y), 0.003f, z);
            float w = Width / 2f, h = height / 2f, c = PanelChamfer;
            Strip(v, t, new Vector2(-w, h - c), new Vector2(-w + c, h), 0.008f, z);
            Strip(v, t, new Vector2(w, -h + c), new Vector2(w - c, -h), 0.008f, z);
            return ToMesh("Menu Accents", v, t);
        }

        static void Strip(List<Vector3> v, List<int> t, Vector2 a, Vector2 b, float width, float z)
        {
            var n = Inward(a, b);
            // the band lies toward the panel's centre from the line a-b
            if (Vector2.Dot(n, -(a + b)) < 0f) n = -n;
            int i = v.Count;
            v.Add(new Vector3(a.x, a.y, z)); v.Add(new Vector3(b.x, b.y, z));
            var a2 = a + n * width;
            var b2 = b + n * width;
            v.Add(new Vector3(a2.x, a2.y, z)); v.Add(new Vector3(b2.x, b2.y, z));
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i + 1); t.Add(i + 2); t.Add(i + 3);
        }

        static Mesh ToMesh(string name, List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            var normals = new Vector3[v.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
            m.normals = normals;
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
