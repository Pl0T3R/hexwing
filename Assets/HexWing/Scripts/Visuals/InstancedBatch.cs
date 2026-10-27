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
}
