using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The Blender-made models (Art/blender, exported to Resources/Models with their textures in
    /// Resources/Textures). A model is one FBX with:
    /// - "&lt;Name&gt;_LOD0", "_LOD1", "_LOD2": the hull at three levels of detail,
    /// - "&lt;Name&gt;_Glow": optional additive parts (engine plumes), drawn with a glow material,
    /// - "SOCKET_*" empties: named points (guns, engines, screens, attachment points).
    /// The hull material is Resources/Materials/Art_&lt;Name&gt; (HexWing/Hull, made by the builder); if it is
    /// missing, one is made at runtime from the textures. A missing model returns null, and callers keep
    /// their code-built meshes, so the game runs with or without the art.
    /// </summary>
    public static class ArtLibrary
    {
        public sealed class Model
        {
            public string Name;
            public Mesh[] Lods;
            public Mesh Glow;
            public Material Material;
            public readonly Dictionary<string, Vector3> Sockets = new();
            /// <summary>Every mesh of the file by object name (moving parts, extra pieces).</summary>
            public readonly Dictionary<string, Mesh> Meshes = new();

            public bool TryGetSocket(string name, out Vector3 local) => Sockets.TryGetValue(name, out local);
        }

        static readonly Dictionary<string, Model> Cache = new();
        static readonly HashSet<string> Missing = new();

        /// <summary>The model, or null when it isn't in the build (then use the code-built mesh).</summary>
        public static Model Get(string name)
        {
            if (!GameConfig.Instance.visuals.useArtModels) return null;
            if (Cache.TryGetValue(name, out var model)) return model;
            if (Missing.Contains(name)) return null;
            var prefab = Resources.Load<GameObject>("Models/" + name);
            if (prefab == null)
            {
                Missing.Add(name);
                return null;
            }

            model = new Model { Name = name };
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) model.Meshes[mf.name] = mf.sharedMesh;
            var lods = new List<Mesh>();
            for (int i = 0; i < 4; i++)
            {
                var mesh = FindMesh(prefab.transform, $"{name}_LOD{i}");
                if (mesh == null) break;
                lods.Add(mesh);
            }
            if (lods.Count == 0 && model.Meshes.Count == 0)
            {
                Debug.LogWarning($"[HexWing] Model {name} has no meshes; using the built-in one.");
                Missing.Add(name);
                return null;
            }
            model.Lods = lods.ToArray();
            model.Glow = FindMesh(prefab.transform, name + "_Glow");
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("SOCKET_"))
                    model.Sockets[t.name.Substring(7)] = prefab.transform.InverseTransformPoint(t.position);
            }
            model.Material = HullMaterial(name);
            Cache[name] = model;
            Debug.Log($"[HexWing] Art: {name}, {model.Lods.Length} LODs ({(model.Lods.Length > 0 ? Triangles(model.Lods[0]) : 0)} triangles at LOD0), {model.Meshes.Count} meshes, {model.Sockets.Count} sockets.");
            return model;
        }

        static Mesh FindMesh(Transform root, string name)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == name) return mf.sharedMesh;
            return null;
        }

        static int Triangles(Mesh m) => m != null ? (int)(m.GetIndexCount(0) / 3) : 0;

        static Material HullMaterial(string name)
        {
            var m = Resources.Load<Material>("Materials/Art_" + name);
            if (m != null) return m;
            var shader = Shader.Find("HexWing/Hull");
            if (shader == null)
            {
                Debug.LogWarning("[HexWing] HexWing/Hull shader missing; run HexWing > Build Game.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            m = new Material(shader) { name = "Art_" + name + " (runtime)", enableInstancing = true };
            var albedo = Resources.Load<Texture2D>($"Textures/{name}_Albedo");
            var mask = Resources.Load<Texture2D>($"Textures/{name}_Mask");
            var normal = Resources.Load<Texture2D>($"Textures/{name}_Normal");
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (mask != null) m.SetTexture("_MaskMap", mask);
            if (normal != null) m.SetTexture("_NormalMap", normal);
            Debug.LogWarning($"[HexWing] Material Art_{name} missing, made one at runtime. Run HexWing > Build Game.");
            return m;
        }

        /// <summary>
        /// Adds the model under parent: one child per LOD with a LODGroup (screen heights from
        /// GameConfig.visuals), and the glow parts with glowMaterial. Returns the LOD root.
        /// </summary>
        /// <summary>
        /// The model's Gun0.. sockets in firing order (upper right, upper left, lower right, lower left, so
        /// shots alternate sides), or null when it has none.
        /// </summary>
        public static Vector3[] GunPoints(Model model)
        {
            if (model == null) return null;
            var points = new List<Vector3>();
            for (int i = 0; model.TryGetSocket("Gun" + i, out var p); i++) points.Add(p);
            if (points.Count == 0) return null;
            points.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : b.x.CompareTo(a.x));
            return points.ToArray();
        }

        public static Transform Attach(Model model, Transform parent, Material glowMaterial, Material hullOverride = null)
        {
            var root = new GameObject(model.Name).transform;
            root.SetParent(parent, false);
            var material = hullOverride != null ? hullOverride : model.Material;
            var lods = new LOD[model.Lods.Length];
            var heights = GameConfig.Instance.visuals.lodScreenHeights;
            for (int i = 0; i < model.Lods.Length; i++)
            {
                var go = MeshKit.Part($"LOD{i}", root, model.Lods[i], material);
                float h = i < heights.Length ? heights[i] : 0.001f;
                if (i == model.Lods.Length - 1) h = Mathf.Min(h, 0.0005f);   // the last LOD stays visible far away
                lods[i] = new LOD(h, new Renderer[] { go.GetComponent<Renderer>() });
            }
            var group = root.gameObject.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
            if (model.Glow != null && glowMaterial != null) MeshKit.Part("Glow", root, model.Glow, glowMaterial);
            return root;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Cache.Clear();
            Missing.Clear();
        }
    }
}
