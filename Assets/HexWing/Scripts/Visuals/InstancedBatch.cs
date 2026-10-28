using UnityEngine;
using UnityEngine.Rendering;

namespace HexWing
{
    /// <summary>
    /// One mesh and material drawn many times in one call (bolts, flashes, debris, HUD markers): fill it
    /// with matrices each frame, then <see cref="Draw"/>. No GameObjects, no per-frame allocations.
    /// The material must have GPU instancing on (the builder sets it on every material).
    /// </summary>
    public class InstancedBatch
    {
        public const int MaxInstances = 1023;

        readonly Mesh _mesh;
        readonly Matrix4x4[] _matrices;
        RenderParams _params;

        public int Count { get; private set; }
        public Material Material => _params.material;

        public InstancedBatch(Mesh mesh, Material material, int capacity)
        {
            _mesh = mesh;
            _matrices = new Matrix4x4[Mathf.Clamp(capacity, 1, MaxInstances)];
            _params = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 20000f),   // culling is per batch; the arena is small
            };
        }

        public void Clear() => Count = 0;

        public bool Add(in Matrix4x4 m)
        {
            if (Count >= _matrices.Length) return false;
            _matrices[Count++] = m;
            return true;
        }

        public void Draw()
        {
            if (Count > 0) Graphics.RenderMeshInstanced(_params, _mesh, 0, _matrices, Count);
        }
    }

    /// <summary>
    /// Like <see cref="InstancedBatch"/> for the effect sprites (Sprite.shader): each instance also has an
    /// age (0..1 through its flipbook) and a seed (its random turn), passed as per-instance arrays.
    /// </summary>
    public class SpriteBatch
    {
        static readonly int AgeId = Shader.PropertyToID("_Age");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static Mesh _quad;

        readonly Matrix4x4[] _matrices;
        readonly float[] _ages, _seeds;
        readonly MaterialPropertyBlock _props = new();
        RenderParams _params;

        public int Count { get; private set; }

        public SpriteBatch(Material material, int capacity)
        {
            int n = Mathf.Clamp(capacity, 1, InstancedBatch.MaxInstances);
            _matrices = new Matrix4x4[n];
            _ages = new float[n];
            _seeds = new float[n];
            _params = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 20000f),
                matProps = _props,
            };
        }

        public void Clear() => Count = 0;

        public void Add(Vector3 position, float size, float age, float seed)
        {
            if (Count >= _matrices.Length) return;
            _matrices[Count] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(size, size, size));
            _ages[Count] = age;
            _seeds[Count] = seed;
            Count++;
        }

        public void Draw()
        {
            if (Count == 0) return;
            _props.SetFloatArray(AgeId, _ages);
            _props.SetFloatArray(SeedId, _seeds);
            Graphics.RenderMeshInstanced(_params, Quad(), 0, _matrices, Count);
        }

        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "Sprite Quad" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return _quad;
        }
    }
}
