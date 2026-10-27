using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The enemy capital ship "Kolos" (original design): a 560 m hexagonal hull with a tapered bow, a
    /// bridge tower, two glowing shield-generator domes on its back, six gun turrets on its flanks and a
    /// reactor in a trench at the stern. The objectives go in order:
    /// 1. shield generators (the reactor is protected while any stands),
    /// 2. turrets,
    /// 3. the reactor; destroying it sets off a chain of explosions and the ship breaks up.
    /// The hull itself can't be damaged: bolts stop on it and ships bounce off it. Only the current
    /// objective's parts can be locked on to.
    /// </summary>
    public class CapitalShip : MonoBehaviour
    {
        public enum Phase { Generators, Turrets, Reactor, Destroyed }

        static Mesh _hull, _glow, _dome, _turret;

        public Phase Current { get; private set; }
        public readonly List<Damageable> Generators = new();
        public readonly List<CapitalTurret> Turrets = new();
        public Damageable Reactor { get; private set; }

        public int GeneratorsLeft => CountAlive(Generators);
        public int TurretsLeft { get { int n = 0; foreach (var t in Turrets) if (t.Health.Alive) n++; return n; } }

        public event System.Action<Phase> PhaseChanged;
        /// <summary>A part was destroyed (for scoring): the part, who did it.</summary>
        public event System.Action<Damageable, Damageable> PartDestroyed;

        GameObject _body;
        readonly List<Collider> _hullColliders = new();

        public static CapitalShip Create(Transform parent)
        {
            EnsureMeshes();
            var go = new GameObject("Capital Ship Kolos");
            go.transform.SetParent(parent, false);
            var ship = go.AddComponent<CapitalShip>();
            ship.Build();
            return ship;
        }

        void Build()
        {
            _body = new GameObject("Body");
            _body.transform.SetParent(transform, false);
            MeshKit.Part("Hull", _body.transform, _hull, Mats.Get(Mats.CapitalHull));
            MeshKit.Part("Glow", _body.transform, _glow, Mats.Get(Mats.EnemyGlow));
            // hull colliders: absorb bolts, ships slide off
            HullBox(new Vector3(0f, 0f, -45f), new Vector3(110f, 100f, 430f));
            HullBox(new Vector3(0f, 0f, 225f), new Vector3(70f, 60f, 110f));
            HullBox(new Vector3(0f, 0f, -275f), new Vector3(130f, 90f, 30f));
            HullBox(new Vector3(0f, 72f, -150f), new Vector3(40f, 40f, 60f));
            HullBox(new Vector3(0f, 95f, -160f), new Vector3(60f, 12f, 30f));
            HullBox(new Vector3(-20f, 62f, -215f), new Vector3(6f, 22f, 60f));
            HullBox(new Vector3(20f, 62f, -215f), new Vector3(6f, 22f, 60f));

            foreach (var z in new[] { 90f, -60f })
            {
                var gen = Part($"Shield Generator {(z > 0f ? 1 : 2)}", new Vector3(0f, 68f, z), _dome, Mats.Get(Mats.Generator), 16f, 400f, "GENERATOR");
                Generators.Add(gen);
            }
            var turretSpots = new[]
            {
                new Vector3(48f, 34f, 140f), new Vector3(-48f, 34f, 140f),
                new Vector3(54f, 30f, 0f), new Vector3(-54f, 30f, 0f),
                new Vector3(48f, 34f, -120f), new Vector3(-48f, 34f, -120f),
            };
            for (int i = 0; i < turretSpots.Length; i++)
            {
                var health = Part($"Turret {i + 1}", turretSpots[i], _turret, Mats.Get(Mats.EnemyHull), 8f, 120f, "DZIAŁO");
                var turret = health.gameObject.AddComponent<CapitalTurret>();
                turret.Init(this, health, turretSpots[i].x > 0f ? Vector3.right : Vector3.left);
                Turrets.Add(turret);
            }
            Reactor = Part("Reactor", new Vector3(0f, 64f, -215f), null, Mats.Get(Mats.EnemyGlow), 12f, 800f, "REAKTOR");
            var core = new MeshKit.Builder();
            core.Ico(Vector3.zero, Vector3.one * 12f);
            MeshKit.Part("Core", Reactor.transform, core.ToMesh("Reactor Core"), Mats.Get(Mats.EnemyGlow));
        }

        void HullBox(Vector3 center, Vector3 size)
        {
            var box = _body.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            _hullColliders.Add(box);
        }

        Damageable Part(string name, Vector3 position, Mesh mesh, Material mat, float radius, float hull, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            if (mesh != null) MeshKit.Part("Mesh", go.transform, mesh, mat);
            go.AddComponent<SphereCollider>().radius = radius;
            var d = go.AddComponent<Damageable>();
            d.Team = Team.Enemy;
            d.MaxHull = hull;
            d.DisplayName = label;
            d.Restore();
            d.Destroyed += by => OnPartDestroyed(d, by);
            return d;
        }

        /// <summary>Everything whole again, generators first.</summary>
        public void ResetShip()
        {
            StopAllCoroutines();
            _body.SetActive(true);
            foreach (var c in _hullColliders) c.enabled = true;
            foreach (var g in Generators) Revive(g);
            foreach (var t in Turrets) { Revive(t.Health); t.ResetTurret(); }
            Revive(Reactor);
            SetPhase(Phase.Generators);
        }

        static void Revive(Damageable d)
        {
            d.gameObject.SetActive(true);
            d.enabled = true;
            d.Restore();
        }

        void SetPhase(Phase p)
        {
            Current = p;
            foreach (var g in Generators) g.Targetable = p == Phase.Generators;
            foreach (var t in Turrets) t.Health.Targetable = p == Phase.Turrets;
            Reactor.Targetable = p == Phase.Reactor;
            Reactor.Invulnerable = p != Phase.Reactor;
            Debug.Log($"[HexWing] Capital ship phase: {p}");
            PhaseChanged?.Invoke(p);
        }

        void OnPartDestroyed(Damageable part, Damageable by)
        {
            Effects.Instance?.Explode(part.transform.position, Vector3.zero, part == Reactor ? 14f : 8f, 24);
            ShipAudio.Instance?.PlayExplosion(part.transform.position, 1f);
            part.gameObject.SetActive(false);
            PartDestroyed?.Invoke(part, by);
            if (Current == Phase.Generators && GeneratorsLeft == 0) SetPhase(Phase.Turrets);
            else if (Current == Phase.Turrets && TurretsLeft == 0) SetPhase(Phase.Reactor);
            else if (part == Reactor) StartCoroutine(BreakUp());
        }

        IEnumerator BreakUp()
        {
            SetPhase(Phase.Destroyed);
            var rng = new System.Random(5);
            for (int i = 0; i < 16; i++)
            {
                var local = new Vector3(((float)rng.NextDouble() - 0.5f) * 100f, ((float)rng.NextDouble() - 0.5f) * 80f, Mathf.Lerp(-260f, 260f, i / 15f));
                var p = transform.TransformPoint(local);
                Effects.Instance?.Explode(p, Vector3.zero, 18f, 30);
                ShipAudio.Instance?.PlayExplosion(p, 1f);
                yield return new WaitForSeconds(0.25f);
            }
            Effects.Instance?.Explode(transform.position, Vector3.zero, 60f, 80);
            _body.SetActive(false);
            foreach (var t in Turrets) t.gameObject.SetActive(false);
            foreach (var g in Generators) g.gameObject.SetActive(false);
        }

        static int CountAlive(List<Damageable> list)
        {
            int n = 0;
            foreach (var d in list) if (d.Alive && d.gameObject.activeSelf) n++;
            return n;
        }

        // --- meshes (original design; ~1.5k triangles in all) ----------------------------------

        static void EnsureMeshes()
        {
            if (_hull != null) return;
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -260f), new Vector3(0f, 0f, 170f), 60f, 52f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, 170f), new Vector3(0f, 0f, 280f), 52f, 14f, 6, 0f, null, true);
            b.Box(new Vector3(0f, 0f, -275f), new Vector3(130f, 90f, 30f), Quaternion.identity, 0.85f);
            // ribs along the hull break up its long flanks
            for (int i = 0; i < 7; i++)
            {
                float z = -230f + i * 60f;
                b.Box(new Vector3(0f, 0f, z), new Vector3(124f, 8f, 6f), Quaternion.identity);
                b.Box(new Vector3(0f, 0f, z), new Vector3(8f, 114f, 6f), Quaternion.identity);
            }
            // bridge tower
            b.Box(new Vector3(0f, 72f, -150f), new Vector3(40f, 40f, 60f), Quaternion.identity, 0.75f);
            b.Box(new Vector3(0f, 95f, -160f), new Vector3(60f, 12f, 30f), Quaternion.identity);
            // reactor trench walls and the generator pedestals
            b.Box(new Vector3(-20f, 62f, -215f), new Vector3(6f, 22f, 60f), Quaternion.identity);
            b.Box(new Vector3(20f, 62f, -215f), new Vector3(6f, 22f, 60f), Quaternion.identity);
            foreach (var z in new[] { 90f, -60f })
                b.Prism(new Vector3(0f, 48f, z), new Vector3(0f, 58f, z), 20f, 16f, 6);
            _hull = b.ToMesh("Capital Hull", p => { float v = Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(-60f, 60f, p.y)); return new Color(v, v, v, 1f); });

            var g = new MeshKit.Builder();
            for (int i = -1; i <= 1; i++)
                g.Prism(new Vector3(i * 38f, 0f, -290f), new Vector3(i * 38f, 0f, -292f), 16f, 14f, 6);
            g.Box(new Vector3(0f, 95f, -145.5f), new Vector3(50f, 3f, 1f), Quaternion.identity);   // bridge window strip
            _glow = g.ToMesh("Capital Glow");

            var d = new MeshKit.Builder();
            d.Ico(Vector3.zero, new Vector3(16f, 12f, 16f));
            _dome = d.ToMesh("Shield Generator");

            var t = new MeshKit.Builder();
            t.Prism(new Vector3(0f, -4f, 0f), new Vector3(0f, 1f, 0f), 7f, 5f, 6);
            t.Box(new Vector3(0f, 3f, 0f), new Vector3(7f, 4f, 7f), Quaternion.identity, 0.7f);
            t.Box(new Vector3(-1.4f, 3f, 7f), new Vector3(0.8f, 0.8f, 10f), Quaternion.identity);
            t.Box(new Vector3(1.4f, 3f, 7f), new Vector3(0.8f, 0.8f, 10f), Quaternion.identity);
            _turret = t.ToMesh("Capital Turret");
        }
    }
}
