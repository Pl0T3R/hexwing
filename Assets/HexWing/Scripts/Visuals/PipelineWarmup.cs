using System.Reflection;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Shader warm-up: while the view is still black at start, draws every game material once, tiny and
    /// just in front of the eye, plain and instanced, for a few frames. Vulkan then builds the pipelines
    /// now instead of at the first laser, explosion or menu (which would hitch). Same idea as Stopklatka.
    /// </summary>
    public class PipelineWarmup : MonoBehaviour
    {
        const int Frames = 3;

        Material[] _materials;
        Mesh _mesh;
        int _left = Frames;
        readonly Matrix4x4[] _instanced = new Matrix4x4[2];

        void Start()
        {
            var fields = typeof(Mats).GetFields(BindingFlags.Public | BindingFlags.Static);
            var list = new System.Collections.Generic.List<Material>();
            foreach (var f in fields)
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                var name = (string)f.GetRawConstantValue();
                if (name == Mats.Sky) continue;   // the skybox draws every frame anyway
                list.Add(Mats.Get(name));
            }
            _materials = list.ToArray();
            var b = new MeshKit.Builder();
            b.Tetra(Vector3.zero, 0.5f);
            _mesh = b.ToMesh("Warmup");
            Debug.Log($"[HexWing] Warm-up: {_materials.Length} materials drawn plain and instanced.");
        }

        void LateUpdate()
        {
            if (_left <= 0 || _materials == null) { enabled = false; return; }
            _left--;
            var cockpit = PlayerCockpit.Instance;
            var eye = cockpit != null ? cockpit.Head : transform;
            var m = Matrix4x4.TRS(eye.position + eye.forward * 0.5f, Quaternion.identity, Vector3.one * 1e-6f);
            _instanced[0] = m;
            _instanced[1] = m;
            foreach (var mat in _materials)
            {
                if (mat == null) continue;
                var rp = new RenderParams(mat);
                Graphics.RenderMesh(rp, _mesh, 0, m);
                if (mat.enableInstancing) Graphics.RenderMeshInstanced(rp, _mesh, 0, _instanced, 2);
            }
        }
    }
}
