using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// A field of rocks for the Belt map, scattered from a seed around a route but never inside the clear
    /// tube along it (so the convoy's way is free; flying off the route means weaving between rocks). Each
    /// rock has a sphere collider (ships bounce off, bolts stop); all rocks draw instanced, one call per
    /// shape and level of detail. The rocks are the Blender-made Asteroids model (five textured shapes,
    /// three LODs picked by on-screen size) if it is in the build, else three code-built faceted shapes.
    /// </summary>
    public class AsteroidField : MonoBehaviour
    {
        struct Rock
        {
            public Vector3 Position;
            public Quaternion Rotation, Spin;
            public float Size;
            public int Shape;
        }

        readonly List<Rock> _rocks = new();
        InstancedBatch[] _batches;
        GameObject _colliders;
        int _shapes = 3, _lods = 1;
        Transform _eye;

        public int Count => _rocks.Count;

        /// <summary>Scatters the field (once; calling again with the same route keeps it).</summary>
        public void Build(Route route, EscortSettings cfg)
        {
            if (_rocks.Count > 0) return;
            var rng = new System.Random(cfg.asteroidSeed);
            if (_batches == null) CreateBatches();
            _colliders = new GameObject("Asteroid Colliders");
            _colliders.transform.SetParent(transform, false);

            float arena = GameConfig.Instance.arena.radius * 0.95f;
            int tries = 0;
            while (_rocks.Count < cfg.asteroids && tries++ < cfg.asteroids * 20)
            {
                // around the route: a random point on it, pushed out sideways
                float along = (float)rng.NextDouble() * route.Length;
                var center = route.PointAt(along);
                var offset = new Vector3(R(rng), R(rng) * 0.5f, R(rng)) * 700f;
                var pos = center + offset;
                float size = Mathf.Lerp(cfg.asteroidSize.x, cfg.asteroidSize.y, Mathf.Pow((float)rng.NextDouble(), 2.2f));   // many small, few big
                if (pos.magnitude > arena) continue;
                if (route.Distance(pos) < cfg.corridor + size) continue;
                var rock = new Rock
                {
                    Position = pos,
                    Rotation = Quaternion.Euler(R(rng) * 180f, R(rng) * 180f, R(rng) * 180f),
                    Spin = Quaternion.Euler(R(rng) * 4f, R(rng) * 4f, R(rng) * 4f),
                    Size = size,
                    Shape = rng.Next(_shapes),
                };
                _rocks.Add(rock);
                var col = _colliders.AddComponent<SphereCollider>();
                col.center = pos - transform.position;
                col.radius = size * 0.42f;
            }
            Debug.Log($"[HexWing] Asteroid field: {_rocks.Count} rocks, clear corridor {cfg.corridor:F0} m.");
        }

        static float R(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        void CreateBatches()
        {
            var art = ArtLibrary.Get("Asteroids");
            if (art != null && art.Meshes.ContainsKey("Asteroids_0_LOD0"))
            {
                int shapes = 0;
                while (art.Meshes.ContainsKey($"Asteroids_{shapes}_LOD0")) shapes++;
                _shapes = shapes;
                _lods = 3;
                _batches = new InstancedBatch[_shapes * _lods];
                for (int s = 0; s < _shapes; s++)
                    for (int l = 0; l < _lods; l++)
                    {
                        if (!art.Meshes.TryGetValue($"Asteroids_{s}_LOD{l}", out var mesh)) mesh = art.Meshes[$"Asteroids_{s}_LOD0"];
                        _batches[s * _lods + l] = new InstancedBatch(mesh, art.Material, 512);
                    }
                return;
            }
            _shapes = 3;
            _lods = 1;
            _batches = new InstancedBatch[3];
            for (int s = 0; s < 3; s++)
            {
                var b = new MeshKit.Builder();
                b.Ico(Vector3.zero, new Vector3(0.5f, 0.42f + s * 0.06f, 0.48f - s * 0.04f), 0.12f + s * 0.04f, new System.Random(30 + s));
                _batches[s] = new InstancedBatch(b.ToMesh($"Asteroid {s + 1}"), Mats.Get(Mats.Asteroid), 512);
            }
        }

        /// <summary>Level of detail from the rock's size over its distance (about its share of the view).</summary>
        int Lod(Vector3 position, float size)
        {
            if (_lods == 1 || _eye == null) return 0;
            float ratio = size / Mathf.Max(1f, Vector3.Distance(_eye.position, position));
            return ratio > 0.06f ? 0 : ratio > 0.015f ? 1 : 2;
        }

        void LateUpdate()
        {
            if (_batches == null) return;
            foreach (var b in _batches) b.Clear();
            if (_eye == null && PlayerCockpit.Instance != null) _eye = PlayerCockpit.Instance.Head;
            float dt = Time.deltaTime;
            for (int i = 0; i < _rocks.Count; i++)
            {
                var r = _rocks[i];
                // a slow tumble (visual only; colliders are spheres, so they don't care)
                r.Rotation = Quaternion.SlerpUnclamped(Quaternion.identity, r.Spin, dt) * r.Rotation;
                _rocks[i] = r;
                _batches[r.Shape * _lods + Lod(r.Position, r.Size)].Add(Matrix4x4.TRS(r.Position, r.Rotation, Vector3.one * r.Size));
            }
            foreach (var b in _batches) b.Draw();
        }
    }

    /// <summary>A path of straight legs through waypoints, measured in metres.</summary>
    public class Route
    {
        readonly Vector3[] _points;
        readonly float[] _distances;

        public float Length { get; }

        public Route(params Vector3[] points)
        {
            _points = points;
            _distances = new float[points.Length];
            for (int i = 1; i < points.Length; i++) _distances[i] = _distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            Length = _distances[^1];
        }

        public Vector3 PointAt(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            for (int i = 1; i < _points.Length; i++)
            {
                if (distance > _distances[i]) continue;
                float t = Mathf.InverseLerp(_distances[i - 1], _distances[i], distance);
                return Vector3.Lerp(_points[i - 1], _points[i], t);
            }
            return _points[^1];
        }

        /// <summary>Direction of travel at a distance, smoothed over 150 m so turns at waypoints are gradual.</summary>
        public Vector3 DirectionAt(float distance)
        {
            var d = PointAt(distance + 75f) - PointAt(distance - 75f);
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        }

        /// <summary>Shortest distance from a point to the route.</summary>
        public float Distance(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 1; i < _points.Length; i++)
            {
                var a = _points[i - 1];
                var ab = _points[i] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}
