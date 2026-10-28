using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The live parts of the cockpit's instruments (the Blender cockpit's scope pod and bar graph panel):
    /// - radar: every live ship and target within 2 km as a dot on the round scope, seen from above the ship
    ///   (forward is up), enemies red, the player's team green; farther ones sit on the rim,
    /// - bar graphs: throttle (cyan), gun heat (amber, red when overheated), shields (blue) and hull (green, red
    ///   below 30%), each a lit bar growing from the bottom of its slot.
    /// Drawn instanced in LateUpdate (at most seven draw calls). Positions come from the cockpit model's
    /// SOCKET_Radar and SOCKET_Gauges; without them (the code-built cockpit) nothing is drawn.
    /// </summary>
    public class CockpitInstrumentPanel : MonoBehaviour
    {
        const float RadarRange = 2000f;
        const float ScopeRadius = 0.046f;   // just inside the scope's outer range ring (0.049 m)
        const float BlipSize = 0.0055f;
        const float BarWidth = 0.016f, BarHeight = 0.058f, BarBottom = -0.025f;
        static readonly float[] BarX = { -0.066f, -0.022f, 0.022f, 0.066f };

        Transform _radar, _gauges, _self;
        Damageable _health;
        ShipWeapons _weapons;
        ShipFlight _flight;
        InstancedBatch _enemy, _ally, _cyan, _amber, _blue, _green, _red;

        void Start()
        {
            var art = ArtLibrary.Get("Cockpit");
            if (art == null || !art.TryGetSocket("Radar", out var radar) || !art.TryGetSocket("Gauges", out var gauges))
            {
                enabled = false;
                return;
            }
            _self = transform;
            var eye = GameConfig.Instance.cockpit.eyePoint;
            _radar = Anchor("Radar", radar, eye);
            _gauges = Anchor("Gauges", gauges, eye);
            _health = GetComponent<Damageable>();
            _weapons = GetComponent<ShipWeapons>();
            _flight = GetComponent<ShipFlight>();

            var b = new MeshKit.Builder();
            b.Box(Vector3.zero, Vector3.one, Quaternion.identity);
            var quad = b.ToMesh("Instrument Quad");
            _enemy = new InstancedBatch(quad, Mats.Get(Mats.LaserEnemy), 64);
            _ally = new InstancedBatch(quad, Mats.Get(Mats.LaserPlayer), 32);
            _cyan = new InstancedBatch(quad, Mats.Get(Mats.FlashCyan), 1);
            _amber = new InstancedBatch(quad, Mats.Get(Mats.RingNext), 1);
            _blue = new InstancedBatch(quad, Mats.Get(Mats.Generator), 1);
            _green = new InstancedBatch(quad, Mats.Get(Mats.LaserPlayer), 1);
            _red = new InstancedBatch(quad, Mats.Get(Mats.LaserEnemy), 2);
        }

        /// <summary>A point on the panel facing the pilot (+Z away from the eye), like the display anchors.</summary>
        Transform Anchor(string name, Vector3 local, Vector3 eye)
        {
            var t = new GameObject("Instrument " + name).transform;
            t.SetParent(transform, false);
            t.localPosition = local;
            t.localRotation = Quaternion.LookRotation(local - eye, Vector3.up);
            return t;
        }

        void LateUpdate()
        {
            _enemy.Clear(); _ally.Clear(); _cyan.Clear(); _amber.Clear(); _blue.Clear(); _green.Clear(); _red.Clear();
            Radar();
            Bars();
            _enemy.Draw(); _ally.Draw(); _cyan.Draw(); _amber.Draw(); _blue.Draw(); _green.Draw(); _red.Draw();
        }

        void Radar()
        {
            var inverse = Quaternion.Inverse(_self.rotation);
            var origin = _self.position;
            var toWorld = _radar.localToWorldMatrix;
            var size = new Vector3(BlipSize, BlipSize, 0.001f);
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d == null || d == _health || !d.Alive || !d.isActiveAndEnabled) continue;
                var rel = inverse * (d.transform.position - origin);
                var p = new Vector2(rel.x, rel.z) / RadarRange;
                float m = p.magnitude;
                if (m > 1f) p /= m;   // beyond the range: on the rim, in its direction
                var local = Matrix4x4.TRS(new Vector3(p.x * ScopeRadius, p.y * ScopeRadius, 0f), Quaternion.identity, size);
                (d.Team == Team.Enemy ? _enemy : _ally).Add(toWorld * local);
            }
        }

        void Bars()
        {
            if (_flight != null) Bar(0, _flight.Controls.Throttle, _cyan);
            if (_weapons != null) Bar(1, _weapons.Heat, _weapons.Overheated ? _red : _amber);
            if (_health != null)
            {
                Bar(2, _health.MaxShields > 0f ? _health.Shields / _health.MaxShields : 0f, _blue);
                float hull = _health.MaxHull > 0f ? _health.Hull / _health.MaxHull : 0f;
                Bar(3, hull, hull < 0.3f ? _red : _green);
            }
        }

        void Bar(int slot, float value, InstancedBatch batch)
        {
            value = Mathf.Clamp01(value);
            if (value < 0.01f) return;
            float h = BarHeight * value;
            var local = Matrix4x4.TRS(new Vector3(BarX[slot], BarBottom + h * 0.5f, 0f), Quaternion.identity, new Vector3(BarWidth, h, 0.001f));
            batch.Add(_gauges.localToWorldMatrix * local);
        }
    }
}
