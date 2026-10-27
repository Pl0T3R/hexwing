using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Builds the default ship's cockpit from code (original design, low-poly faceted): a ball-shaped
    /// shell with a hexagonal front window and six thin struts to an inner hex ring (the centre stays
    /// clear for the crosshair), a front console with three displays, two side panels, a seat, a
    /// floor plate and a control stick and throttle lever. The ship's origin is the centre of the ball;
    /// +Z is forward. About 1.6k triangles in 6 draw calls (one per material).
    /// </summary>
    public static class CockpitFactory
    {
        /// <summary>Where the displays are, for the texts placed on them.</summary>
        public struct Displays
        {
            public Transform Center, Left, Right, SideLeft, SideRight;
            public Transform Stick, Throttle;
        }

        const int Sectors = 12;   // around the forward axis: hex corners and edge midpoints
        const int Rows = 10;      // from the window edge back to the rear pole

        public static Displays Build(Transform ship)
        {
            var cfg = GameConfig.Instance.cockpit;
            float r = cfg.shellRadius;
            var eye = cfg.eyePoint;
            var root = new GameObject("Cockpit").transform;
            root.SetParent(ship, false);

            MeshKit.Part("Shell", root, ShellMesh(r, cfg.windowHalfAngle, eye), Mats.Get(Mats.CockpitShell));
            MeshKit.Part("Window Frame", root, FrameMesh(r, cfg.windowHalfAngle), Mats.Get(Mats.CockpitFrame));
            MeshKit.Part("Accents", root, AccentMesh(r, cfg.windowHalfAngle), Mats.Get(Mats.Accent));
            MeshKit.Part("Interior", root, InteriorMesh(eye), Mats.Get(Mats.CockpitTrim));

            var d = new Displays
            {
                Center = Screen(root, "Display Center", new Vector3(0f, -0.2f, 0.52f), new Vector2(0.24f, 0.15f), eye),
                Left = Screen(root, "Display Left", new Vector3(-0.27f, -0.21f, 0.47f), new Vector2(0.2f, 0.14f), eye),
                Right = Screen(root, "Display Right", new Vector3(0.27f, -0.21f, 0.47f), new Vector2(0.2f, 0.14f), eye),
                SideLeft = Screen(root, "Display Side Left", new Vector3(-0.56f, -0.18f, 0.12f), new Vector2(0.18f, 0.13f), eye),
                SideRight = Screen(root, "Display Side Right", new Vector3(0.56f, -0.18f, 0.12f), new Vector2(0.18f, 0.13f), eye),
            };
            d.Stick = Control(root, "Stick", new Vector3(0.24f, -0.52f, 0.22f), 0.22f, 0.024f);
            d.Throttle = Control(root, "Throttle", new Vector3(-0.3f, -0.48f, 0.12f), 0.16f, 0.02f);
            return d;
        }

        // --- shell ------------------------------------------------------------------------

        /// <summary>
        /// The ball, open at the front. Each of the 12 sectors starts at the hexagonal window edge (corner
        /// or edge midpoint) and runs back to the rear pole in 10 rows, so the window edge is exact and
        /// the facets stay even. Faces point inward (toward the eye).
        /// </summary>
        static Mesh ShellMesh(float r, float windowHalfAngle, Vector3 eye)
        {
            var b = new MeshKit.Builder();
            var grid = new Vector3[Sectors, Rows + 1];
            for (int s = 0; s < Sectors; s++)
            {
                float phi = s * 360f / Sectors;
                float edge = WindowEdgeAngle(phi, windowHalfAngle);
                for (int k = 0; k <= Rows; k++)
                {
                    float theta = Mathf.Lerp(edge, 180f, k / (float)Rows);
                    grid[s, k] = SpherePoint(r, theta, phi);
                }
            }
            for (int s = 0; s < Sectors; s++)
            {
                int n = (s + 1) % Sectors;
                for (int k = 0; k < Rows; k++)
                {
                    b.TriToward(grid[s, k], grid[n, k], grid[n, k + 1], eye);
                    b.TriToward(grid[s, k], grid[n, k + 1], grid[s, k + 1], eye);
                }
            }
            // darker toward the floor and the rear: cheap baked shading in the vertex colors
            return b.ToMesh("Cockpit Shell", p => Shade(p, r));
        }

        /// <summary>A point on the ball: theta from the forward axis (+Z), phi around it (0 = right).</summary>
        static Vector3 SpherePoint(float r, float thetaDeg, float phiDeg)
        {
            float t = thetaDeg * Mathf.Deg2Rad, p = phiDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(t) * Mathf.Cos(p), Mathf.Sin(t) * Mathf.Sin(p), Mathf.Cos(t)) * r;
        }

        /// <summary>
        /// Angle from the forward axis to the hexagonal window edge in direction phi: a regular hexagon
        /// with corners at 0, 60, 120... degrees whose corners lie at windowHalfAngle.
        /// </summary>
        static float WindowEdgeAngle(float phiDeg, float windowHalfAngle)
        {
            float local = Mathf.Repeat(phiDeg, 60f) - 30f;   // -30..30 inside one hexagon edge
            float corner = Mathf.Tan(windowHalfAngle * Mathf.Deg2Rad);
            float apothem = corner * Mathf.Cos(30f * Mathf.Deg2Rad);
            float radius = apothem / Mathf.Cos(local * Mathf.Deg2Rad);
            return Mathf.Atan(radius) * Mathf.Rad2Deg;
        }

        static Color Shade(Vector3 p, float r)
        {
            float low = Mathf.InverseLerp(r, -r, p.y);      // 0 at the top, 1 at the bottom
            float back = Mathf.InverseLerp(r, -r, p.z);     // 0 at the front, 1 at the back
            float v = Mathf.Lerp(1f, 0.55f, low * 0.7f + back * 0.3f);
            return new Color(v, v, v, 1f);
        }

        // --- window frame -----------------------------------------------------------------

        /// <summary>Six beams along the hexagonal window edge, six thin struts to a small inner hex ring.</summary>
        static Mesh FrameMesh(float r, float windowHalfAngle)
        {
            var b = new MeshKit.Builder();
            var outer = new Vector3[6];
            var inner = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float phi = i * 60f;
                outer[i] = SpherePoint(r - 0.015f, windowHalfAngle, phi);
                inner[i] = SpherePoint(r - 0.015f, windowHalfAngle * 0.42f, phi);
            }
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                b.Prism(outer[i], outer[j], 0.04f, 0.04f, 4);         // the window edge
                b.Prism(inner[i], inner[j], 0.012f, 0.012f, 4);       // the inner ring
                b.Prism(outer[i], inner[i], 0.016f, 0.01f, 4);        // the struts
            }
            return b.ToMesh("Cockpit Window Frame");
        }

        /// <summary>Small glowing studs at the window corners and a strip under the window.</summary>
        static Mesh AccentMesh(float r, float windowHalfAngle)
        {
            var b = new MeshKit.Builder();
            for (int i = 0; i < 6; i++)
                b.Ico(SpherePoint(r - 0.05f, windowHalfAngle + 3f, i * 60f + 30f), Vector3.one * 0.012f);
            b.Box(new Vector3(0f, -0.11f, 0.6f), new Vector3(0.5f, 0.008f, 0.01f), Quaternion.Euler(-35f, 0f, 0f));
            return b.ToMesh("Cockpit Accents");
        }

        // --- interior -----------------------------------------------------------------------

        /// <summary>Console under the window, side panels, seat, floor plate.</summary>
        static Mesh InteriorMesh(Vector3 eye)
        {
            var b = new MeshKit.Builder();
            // front console: a slanted block under the window, the displays sit on its face
            b.Box(new Vector3(0f, -0.3f, 0.6f), new Vector3(0.95f, 0.22f, 0.26f), Quaternion.Euler(-35f, 0f, 0f), 0.85f);
            // side panels, turned toward the pilot
            b.Box(new Vector3(-0.6f, -0.26f, 0.12f), new Vector3(0.14f, 0.24f, 0.36f), Quaternion.Euler(0f, 0f, -25f));
            b.Box(new Vector3(0.6f, -0.26f, 0.12f), new Vector3(0.14f, 0.24f, 0.36f), Quaternion.Euler(0f, 0f, 25f));
            // seat: pan, back and headrest behind the eye point
            b.Box(new Vector3(0f, -0.6f, -0.12f), new Vector3(0.5f, 0.1f, 0.5f), Quaternion.identity);
            b.Box(new Vector3(0f, -0.15f, -0.42f), new Vector3(0.52f, 0.8f, 0.1f), Quaternion.Euler(-8f, 0f, 0f), 0.85f);
            b.Box(new Vector3(0f, 0.32f, -0.48f), new Vector3(0.3f, 0.18f, 0.08f), Quaternion.Euler(-8f, 0f, 0f));
            // floor plate
            b.Box(new Vector3(0f, -0.78f, 0.1f), new Vector3(0.9f, 0.04f, 0.9f), Quaternion.identity);
            return b.ToMesh("Cockpit Interior", p => Shade(p, 1f));
        }

        /// <summary>A dark display surface facing the eye, with an anchor for its text in front of it.</summary>
        static Transform Screen(Transform root, string name, Vector3 center, Vector2 size, Vector3 eye)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(root, false);
            anchor.localPosition = center;
            anchor.localRotation = Quaternion.LookRotation(center - eye, Vector3.up);   // +Z away from the eye

            var b = new MeshKit.Builder();
            b.Box(Vector3.zero, new Vector3(size.x, size.y, 0.01f), Quaternion.identity);
            MeshKit.Part("Screen", anchor, b.ToMesh(name + " Screen"), Mats.Get(Mats.Screen));
            return anchor;
        }

        /// <summary>A stick or lever: shaft and grip. Its transform is the pivot at the base (moved in M2).</summary>
        static Transform Control(Transform root, string name, Vector3 basePoint, float length, float radius)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = basePoint;
            var b = new MeshKit.Builder();
            b.Prism(Vector3.zero, new Vector3(0f, length, 0f), radius * 1.6f, radius, 6);
            b.Box(new Vector3(0f, length + 0.04f, 0f), new Vector3(radius * 2.4f, 0.09f, radius * 2.6f), Quaternion.identity, 0.8f);
            MeshKit.Part("Mesh", pivot, b.ToMesh(name + " Mesh"), Mats.Get(Mats.CockpitFrame));
            return pivot;
        }
    }
}
