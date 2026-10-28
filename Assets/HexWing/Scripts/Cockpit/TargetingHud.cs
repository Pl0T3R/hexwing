using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Holographic combat markers, seen through the canopy (the cockpit frame hides them, as it would a
    /// projection on the glass). Each marker sits at a fixed distance from the eye in the true direction
    /// of what it marks, so its size in view is constant and the eyes don't refocus between markers:
    /// - the crosshair (a hexagon) where the guns converge; it pulses on every hit,
    /// - a small diamond on every enemy in range ahead, a small white triangle on every wingman,
    /// - brackets on the current target: white, closing in while locking, amber and turning when locked,
    /// - the lead marker: aim the crosshair at it and the bolts meet the target,
    /// - a turning lock ring around a locked target.
    /// The markers are glowing glyphs from one atlas (Art/fx/make_fx.py, Hud shader); without the atlas the
    /// older flat outlines are drawn. All markers are instanced: 7 draw calls at most.
    /// </summary>
    public class TargetingHud : MonoBehaviour
    {
        // angular sizes (degrees across)
        const float CrosshairDeg = 2.2f, DiamondDeg = 1.1f, BracketDeg = 2.6f, LeadDeg = 0.9f;

        Transform _eye;
        ShipWeapons _weapons;
        Damageable _self;
        InstancedBatch _crosshair, _diamonds, _allies, _bracketWhite, _bracketAmber, _lead, _lockRing;
        float _hitPulse;

        void Start()
        {
            var cockpit = GetComponent<PlayerCockpit>();
            _eye = cockpit != null ? cockpit.Head : transform;
            _weapons = GetComponent<ShipWeapons>();
            _self = GetComponent<Damageable>();
            if (Resources.Load<Texture2D>("Textures/HUD_Glyphs") != null)
            {
                var cyan = Mats.Get(Mats.HudGlyphCyan);
                var white = Mats.Get(Mats.HudGlyphWhite);
                var amber = Mats.Get(Mats.HudGlyphAmber);
                _crosshair = new InstancedBatch(Glyph(0), cyan, 1);
                _diamonds = new InstancedBatch(Glyph(1), cyan, 64);
                _allies = new InstancedBatch(Glyph(2), white, 16);
                _bracketWhite = new InstancedBatch(Glyph(3), white, 1);
                _bracketAmber = new InstancedBatch(Glyph(3), amber, 1);
                _lead = new InstancedBatch(Glyph(4), cyan, 1);
                _lockRing = new InstancedBatch(Glyph(5), amber, 1);
            }
            else
            {
                var cyan = Mats.Get(Mats.HudCyan);
                _crosshair = new InstancedBatch(Outline(6, 0.5f, 0.06f, 30f), cyan, 1);
                _diamonds = new InstancedBatch(Outline(4, 0.5f, 0.1f, 0f), cyan, 64);
                _allies = new InstancedBatch(Outline(3, 0.5f, 0.12f, 90f), Mats.Get(Mats.HudWhite), 16);
                var bracket = Brackets();
                _bracketWhite = new InstancedBatch(bracket, Mats.Get(Mats.HudWhite), 1);
                _bracketAmber = new InstancedBatch(bracket, Mats.Get(Mats.HudAmber), 1);
                _lead = new InstancedBatch(Filled(6, 0.5f), cyan, 1);
                _lockRing = new InstancedBatch(Outline(16, 0.5f, 0.04f, 0f), Mats.Get(Mats.HudAmber), 1);
            }
            if (LaserSystem.Instance != null) LaserSystem.Instance.Hit += OnLaserHit;
            if (MissileSystem.Instance != null) MissileSystem.Instance.Hit += OnMissileHit;
        }

        void OnDestroy()
        {
            if (LaserSystem.Instance != null) LaserSystem.Instance.Hit -= OnLaserHit;
            if (MissileSystem.Instance != null) MissileSystem.Instance.Hit -= OnMissileHit;
        }

        void OnLaserHit(Damageable target, Vector3 point, bool killed, Damageable shooter) => OnHit(killed, shooter);
        void OnMissileHit(Damageable target, bool killed, Damageable shooter) => OnHit(killed, shooter);

        void OnHit(bool killed, Damageable shooter)
        {
            if (shooter == null || shooter != _self) return;
            _hitPulse = 1f;
            var hand = TryGetComponent(out PlayerInput input) ? input.FlyHand : OVRInput.Controller.RTouch;
            Haptics.Play(hand, killed ? GameConfig.Instance.haptics.kill : GameConfig.Instance.haptics.hitDealt);
            if (killed) ShipAudio.Instance?.PlayKill();
            else ShipAudio.Instance?.PlayHitDealt();
        }

        void LateUpdate()
        {
            if (_eye == null) return;
            var cfg = GameConfig.Instance.combat;
            float d = cfg.hudDistance;
            var eye = _eye.position;
            var up = transform.up;
            _crosshair.Clear(); _diamonds.Clear(); _allies.Clear(); _bracketWhite.Clear(); _bracketAmber.Clear(); _lead.Clear(); _lockRing.Clear();
            bool alive = _self == null || _self.Alive;

            if (alive)
            {
                _hitPulse = Mathf.Max(0f, _hitPulse - Time.deltaTime * 6f);
                var aim = transform.position + transform.forward * cfg.convergence;
                Place(_crosshair, eye, aim, d, CrosshairDeg * (1f + 0.35f * _hitPulse), up, 0f);

                var target = _weapons != null ? _weapons.Target : null;
                var all = Damageable.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var t = all[i];
                    if (t == _self || !t.Alive || !t.Targetable || t.Team == Team.Neutral || t == target) continue;
                    var to = t.transform.position - eye;
                    if (to.sqrMagnitude > cfg.targetRange * cfg.targetRange || Vector3.Dot(to, _eye.forward) <= 0f) continue;
                    if (t.Team == Team.Player) Place(_allies, eye, t.transform.position, d, DiamondDeg * 0.8f, up, 0f);   // wingmen: small white triangles
                    else Place(_diamonds, eye, t.transform.position, d, DiamondDeg, up, 0f);
                }

                if (target != null && target.Alive)
                {
                    float progress = _weapons.LockProgress;
                    if (_weapons.Locked)
                    {
                        Place(_bracketAmber, eye, target.transform.position, d, BracketDeg, up, 0f);
                        Place(_lockRing, eye, target.transform.position, d, BracketDeg * 1.25f, up, Time.time * 90f);
                    }
                    else
                        Place(_bracketWhite, eye, target.transform.position, d, BracketDeg * Mathf.Lerp(2f, 1f, progress), up, progress * 45f);
                    Place(_lead, eye, _weapons.LeadPoint, d, LeadDeg, up, 0f);
                }
            }

            _crosshair.Draw(); _diamonds.Draw(); _allies.Draw(); _bracketWhite.Draw(); _bracketAmber.Draw(); _lead.Draw(); _lockRing.Draw();
        }

        static void Place(InstancedBatch batch, Vector3 eye, Vector3 worldTarget, float distance, float degrees, Vector3 up, float spin)
        {
            var dir = worldTarget - eye;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            float size = 2f * distance * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad);
            var rot = Quaternion.LookRotation(dir, up) * Quaternion.Euler(0f, 0f, spin);
            batch.Add(Matrix4x4.TRS(eye + dir * distance, rot, Vector3.one * size));
        }

        // --- marker meshes (unit size, in the XY plane, facing -Z toward the eye) ---------------

        /// <summary>
        /// A quad showing one cell of the glyph atlas (4 x 2 cells). The glyph's main shape fills 77% of the
        /// cell, so the quad is 1.3 units across to keep the markers the size the outlines had.
        /// </summary>
        static Mesh Glyph(int cell)
        {
            const float h = 0.65f;
            float u0 = (cell % 4) * 0.25f, v1 = 1f - (cell / 4) * 0.5f, u1 = u0 + 0.25f, v0 = v1 - 0.5f;
            var m = new Mesh { name = "HUD Glyph " + cell };
            m.vertices = new[] { new Vector3(-h, -h, 0f), new Vector3(h, -h, 0f), new Vector3(h, h, 0f), new Vector3(-h, h, 0f) };
            m.uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        static Mesh Outline(int sides, float radius, float width, float startDeg)
        {
            var b = new MeshKit.Builder();
            for (int i = 0; i < sides; i++)
            {
                float a0 = (startDeg + i * 360f / sides) * Mathf.Deg2Rad, a1 = (startDeg + (i + 1) * 360f / sides) * Mathf.Deg2Rad;
                var o0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var o1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                var i0 = o0 * (1f - width / radius);
                var i1 = o1 * (1f - width / radius);
                b.Quad(o0, o1, i1, i0, new Vector3(0f, 0f, 1f));
            }
            return b.ToMesh("HUD Outline " + sides);
        }

        static Mesh Filled(int sides, float radius)
        {
            var b = new MeshKit.Builder();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                b.Tri(Vector3.zero, new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, new Vector3(0f, 0f, 1f));
            }
            return b.ToMesh("HUD Dot");
        }

        /// <summary>Four corner brackets of a unit square.</summary>
        static Mesh Brackets()
        {
            var b = new MeshKit.Builder();
            const float h = 0.5f, len = 0.18f, w = 0.05f;
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, 0f, i * 90f);
                var corner = rot * new Vector3(h, h, 0f);
                var along1 = rot * Vector3.left;
                var along2 = rot * Vector3.down;
                Bar(b, corner, along1 * len, rot * new Vector3(0f, -w, 0f));
                Bar(b, corner, along2 * len, rot * new Vector3(-w, 0f, 0f));
            }
            return b.ToMesh("HUD Brackets");
        }

        static void Bar(MeshKit.Builder b, Vector3 start, Vector3 along, Vector3 across)
        {
            b.Quad(start, start + along, start + along + across, start + across, new Vector3(0f, 0f, 1f));
        }
    }
}
