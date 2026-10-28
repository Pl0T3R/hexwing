using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The set pieces of each map, so a map is more than its sky (Blender-made models; nothing is built
    /// when they are missing):
    /// - Orbit: the station "Dawn" just outside the arena as a landmark (3.3 km away, 700 m across), and
    ///   two wreck fields inside it (four torn cruiser sections each, with mesh colliders, and slowly
    ///   tumbling smaller debris with sphere colliders), above and below the middle where the modes play.
    /// - Belt: its asteroid field belongs to the Escort mode (AsteroidField); nothing extra yet.
    /// Shown by MapTheme.Apply; each map's pieces are built the first time and kept.
    /// </summary>
    public class MapSet : MonoBehaviour
    {
        struct Debris
        {
            public Vector3 Position;
            public Quaternion Rotation, Spin;
            public float Scale;
            public int Shape;
        }

        static MapSet _instance;
        GameObject _orbit;
        readonly List<Debris> _debris = new();
        InstancedBatch[] _debrisBatches;

        static readonly Vector3 StationPosition = new(-2600f, 400f, 2000f);
        static readonly Vector3[] WreckFields = { new(1300f, -700f, -900f), new(-1100f, 650f, -600f) };

        public static void Show(Map map)
        {
            if (_instance == null)
            {
                _instance = new GameObject("Map Set").AddComponent<MapSet>();
            }
            _instance.Apply(map);
        }

        void Apply(Map map)
        {
            if (map == Map.Orbit && _orbit == null) BuildOrbit();
            if (_orbit != null) _orbit.SetActive(map == Map.Orbit);
            enabled = map == Map.Orbit && _debrisBatches != null;
        }

        void BuildOrbit()
        {
            _orbit = new GameObject("Orbit Set");
            _orbit.transform.SetParent(transform, false);
            var station = ArtLibrary.Get("Station");
            if (station != null)
            {
                var root = ArtLibrary.Attach(station, _orbit.transform, null);
                root.localPosition = StationPosition;
                root.localRotation = Quaternion.Euler(8f, 35f, -4f);
                AddMeshCollider(root.gameObject, station, "Station");
                Debug.Log($"[HexWing] Map set: station at {StationPosition.magnitude:F0} m.");
            }

            var wrecks = ArtLibrary.Get("Wrecks");
            if (wrecks == null) return;
            int pieces = 0;
            while (wrecks.Meshes.ContainsKey($"Wrecks_{pieces}_LOD0")) pieces++;
            if (pieces == 0) return;
            var rng = new System.Random(41);
            foreach (var center in WreckFields)
            {
                for (int i = 0; i < pieces; i++)
                {
                    var go = new GameObject($"Wreck {i + 1}");
                    go.transform.SetParent(_orbit.transform, false);
                    go.transform.localPosition = center + new Vector3(R(rng), R(rng) * 0.5f, R(rng)) * 260f;
                    go.transform.localRotation = Quaternion.Euler(R(rng) * 180f, R(rng) * 180f, R(rng) * 180f);
                    var lods = new LOD[3];
                    for (int l = 0; l < 3; l++)
                    {
                        if (!wrecks.Meshes.TryGetValue($"Wrecks_{i}_LOD{l}", out var mesh)) mesh = wrecks.Meshes[$"Wrecks_{i}_LOD0"];
                        var part = MeshKit.Part($"LOD{l}", go.transform, mesh, wrecks.Material);
                        lods[l] = new LOD(l == 0 ? 0.08f : l == 1 ? 0.02f : 0.0005f, new Renderer[] { part.GetComponent<Renderer>() });
                    }
                    go.AddComponent<LODGroup>().SetLODs(lods);
                    if (wrecks.Meshes.TryGetValue($"Wrecks_{i}_LOD2", out var col)) go.AddComponent<MeshCollider>().sharedMesh = col;
                }
                // smaller debris: the same shapes at 4-12% scale, tumbling slowly
                for (int k = 0; k < 40; k++)
                {
                    _debris.Add(new Debris
                    {
                        Position = center + new Vector3(R(rng), R(rng) * 0.6f, R(rng)) * 420f,
                        Rotation = Quaternion.Euler(R(rng) * 180f, R(rng) * 180f, R(rng) * 180f),
                        Spin = Quaternion.Euler(R(rng) * 3f, R(rng) * 3f, R(rng) * 3f),
                        Scale = Mathf.Lerp(0.04f, 0.12f, (float)rng.NextDouble()),
                        Shape = rng.Next(pieces),
                    });
                }
            }
            _debrisBatches = new InstancedBatch[pieces];
            for (int i = 0; i < pieces; i++)
            {
                if (!wrecks.Meshes.TryGetValue($"Wrecks_{i}_LOD1", out var mesh)) mesh = wrecks.Meshes[$"Wrecks_{i}_LOD0"];
                _debrisBatches[i] = new InstancedBatch(mesh, wrecks.Material, 128);
            }
            var colliders = new GameObject("Debris Colliders");
            colliders.transform.SetParent(_orbit.transform, false);
            foreach (var d in _debris)
            {
                var sphere = colliders.AddComponent<SphereCollider>();
                sphere.center = d.Position;
                sphere.radius = 45f * d.Scale;   // about the piece's half length at that scale
            }
            Debug.Log($"[HexWing] Map set: {WreckFields.Length} wreck fields, {pieces} pieces and {_debris.Count / WreckFields.Length} debris each.");
        }

        static void AddMeshCollider(GameObject go, ArtLibrary.Model model, string name)
        {
            if (model.Meshes.TryGetValue($"{name}_LOD2", out var col) || model.Meshes.TryGetValue($"{name}_LOD1", out col))
                go.AddComponent<MeshCollider>().sharedMesh = col;
        }

        static float R(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        void LateUpdate()
        {
            if (_debrisBatches == null) return;
            foreach (var b in _debrisBatches) b.Clear();
            float dt = Time.deltaTime;
            for (int i = 0; i < _debris.Count; i++)
            {
                var d = _debris[i];
                d.Rotation = Quaternion.SlerpUnclamped(Quaternion.identity, d.Spin, dt) * d.Rotation;
                _debris[i] = d;
                _debrisBatches[d.Shape].Add(Matrix4x4.TRS(d.Position, d.Rotation, Vector3.one * d.Scale));
            }
            foreach (var b in _debrisBatches) b.Draw();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;
    }
}
