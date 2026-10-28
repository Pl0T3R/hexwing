using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The AI fighters' meshes, built once from code (original designs, low-poly faceted, +Z forward,
    /// about 9–13 m long). Each ship is two meshes: the hull and its glowing parts (engines, sensor).
    /// - Hornet (enemy fighter): a hexagonal pod with forward-swept, drooping wings and wingtip blades.
    /// - Trident (enemy elite): a heavier pod with three swept blade wings around it.
    /// - Swallow (ally): a slim fuselage with swept-back wings and two canted tail fins.
    /// 200–400 triangles each, so no LODs are needed (budget in PLAN.md: 4k at LOD0).
    /// </summary>
    public static class ShipMeshes
    {
        public enum Kind { Hornet, Trident, Swallow }

        static Mesh[] _hulls, _glows;

        public static Mesh Hull(Kind kind) { Ensure(); return _hulls[(int)kind]; }
        public static Mesh Glow(Kind kind) { Ensure(); return _glows[(int)kind]; }

        /// <summary>Collision radius (m) for lasers and missiles.</summary>
        public static float Radius(Kind kind) => kind == Kind.Swallow ? 4.6f : 6.2f;   // Interceptor wingmen / X-wing enemies

        static void Ensure()
        {
            if (_hulls != null) return;
            _hulls = new Mesh[3];
            _glows = new Mesh[3];
            Hornet(out _hulls[0], out _glows[0]);
            Trident(out _hulls[1], out _glows[1]);
            Swallow(out _hulls[2], out _glows[2]);
        }

        static void Hornet(out Mesh hull, out Mesh glow)
        {
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, 2f), 1.1f, 0.9f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, 4.5f), 0.9f, 0.1f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, -4f), 1.1f, 0.6f, 6, 0f, null, true);
            for (int side = -1; side <= 1; side += 2)
            {
                // forward-swept wing with a slight droop, a vertical blade at the tip
                b.Box(new Vector3(3.2f * side, -0.6f, 0.6f), new Vector3(5f, 0.15f, 2.2f), Quaternion.Euler(0f, -20f * side, -12f * side));
                b.Box(new Vector3(5.6f * side, -1.1f, 1.4f), new Vector3(0.2f, 1.6f, 2.6f), Quaternion.Euler(0f, -20f * side, 0f), 0.5f);
            }
            b.Box(new Vector3(0f, 1.2f, -1f), new Vector3(0.15f, 1.2f, 2.4f), Quaternion.Euler(-15f, 0f, 0f), 0.4f);   // dorsal spine
            hull = b.ToMesh("Hornet Hull", Shade);

            var g = new MeshKit.Builder();
            g.Ico(new Vector3(0f, 0f, -4.1f), new Vector3(0.7f, 0.7f, 0.3f));
            g.Ico(new Vector3(0f, 0.55f, 2.4f), new Vector3(0.35f, 0.25f, 0.6f));
            glow = g.ToMesh("Hornet Glow");
        }

        static void Trident(out Mesh hull, out Mesh glow)
        {
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 3.5f), 1.5f, 1.1f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, 3.5f), new Vector3(0f, 0f, 6.5f), 1.1f, 0.15f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, -5f), 1.5f, 0.9f, 6, 0f, null, true);
            for (int i = 0; i < 3; i++)
            {
                var around = Quaternion.Euler(0f, 0f, 90f + i * 120f);
                b.Box(around * new Vector3(0f, 3.4f, -0.8f), new Vector3(0.22f, 4.6f, 3.4f), around * Quaternion.Euler(-22f, 0f, 0f), 0.55f);
                b.Box(around * new Vector3(0f, 5.6f, 0.4f), new Vector3(0.5f, 0.5f, 3.2f), around, 1f);   // blade tip rail
            }
            hull = b.ToMesh("Trident Hull", Shade);

            var g = new MeshKit.Builder();
            g.Ico(new Vector3(0f, 0f, -5.1f), new Vector3(1.1f, 1.1f, 0.3f));
            for (int i = 0; i < 3; i++)
                g.Ico(Quaternion.Euler(0f, 0f, 90f + i * 120f) * new Vector3(0f, 1.25f, 3.6f), new Vector3(0.3f, 0.3f, 0.5f));
            glow = g.ToMesh("Trident Glow");
        }

        static void Swallow(out Mesh hull, out Mesh glow)
        {
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 3f), 0.85f, 0.7f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, 6f), 0.7f, 0.05f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, -4.6f), 0.85f, 0.6f, 6, 0f, null, true);
            b.Ico(new Vector3(0f, 0.6f, 2.2f), new Vector3(0.5f, 0.4f, 1.2f));   // canopy
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(2.9f * side, 0f, -1.4f), new Vector3(4.8f, 0.12f, 3.2f), Quaternion.Euler(0f, 25f * side, 0f));
                b.Box(new Vector3(1.0f * side, 1.2f, -3.4f), new Vector3(0.12f, 2f, 1.8f), Quaternion.Euler(-20f, 0f, -15f * side), 0.5f);
            }
            hull = b.ToMesh("Swallow Hull", Shade);

            var g = new MeshKit.Builder();
            for (int side = -1; side <= 1; side += 2)
                g.Ico(new Vector3(0.45f * side, 0f, -4.7f), new Vector3(0.4f, 0.4f, 0.25f));
            glow = g.ToMesh("Swallow Glow");
        }

        /// <summary>Baked shading: a little darker underneath and toward the tail.</summary>
        static Color Shade(Vector3 p)
        {
            float v = Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(-1.5f, 1.5f, p.y)) * Mathf.Lerp(0.85f, 1f, Mathf.InverseLerp(-5f, 4f, p.z));
            return new Color(v, v, v, 1f);
        }
    }
}
