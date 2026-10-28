using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Builds the player's cockpit. With the Blender-made model (Resources/Models/Cockpit and
    /// CockpitControls) the shell, frame, console, seat and details come from it; the display glasses,
    /// their anchors and the stick and throttle pivots are always placed here, so texts and instruments
    /// work either way. Without it, the original code-built cockpit (low-poly faceted): a ball-shaped
    /// shell with the Interceptor's round front window and eight spokes to a small hub ring (the centre stays
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
            /// <summary>The long strip above the window: mode stats, seen only with the head lifted.</summary>
            public Transform Overhead;
            public Transform Stick, Throttle;
        }

        /// <summary>Name of the renderer of the Blender-made cockpit (CockpitAlert tints its emission).</summary>
        public const string ArtName = "Cockpit Art";

        const int Sectors = 12;   // around the forward axis
        const int Rows = 10;      // from the window edge back to the rear pole

        public static Displays Build(Transform ship)
        {
            var cfg = GameConfig.Instance.cockpit;
            float r = cfg.shellRadius;
            var eye = cfg.eyePoint;
            var root = new GameObject("Cockpit").transform;
            root.SetParent(ship, false);

            // the Blender-made cockpit (Art/blender/models/cockpit.py) if it is in the build, else the code-built one
            var art = ArtLibrary.Get("Cockpit");
            if (art != null && art.Lods.Length > 0)
            {
                // its own material copy: CockpitAlert tints the emission (the light strips) per ship class and on hits
                var go = MeshKit.Part(ArtName, root, art.Lods[0], new Material(art.Material) { name = "Cockpit (lights)" });
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
            {
                MeshKit.Part("Shell", root, ShellMesh(r, cfg.windowHalfAngle, eye), Mats.Get(Mats.CockpitShell));
                MeshKit.Part("Window Frame", root, FrameMesh(r, cfg.windowHalfAngle), Mats.Get(Mats.CockpitFrame));
                MeshKit.Part("Accents", root, AccentMesh(r, cfg.windowHalfAngle), Mats.Get(Mats.Accent));
                MeshKit.Part("Interior", root, InteriorMesh(eye), Mats.Get(Mats.CockpitTrim));
            }

            var d = new Displays
            {
                Center = Screen(root, "Display Center", new Vector3(0f, -0.2f, 0.52f), new Vector2(0.24f, 0.15f), eye),
                Left = Screen(root, "Display Left", new Vector3(-0.27f, -0.21f, 0.47f), new Vector2(0.2f, 0.14f), eye),
                Right = Screen(root, "Display Right", new Vector3(0.27f, -0.21f, 0.47f), new Vector2(0.2f, 0.14f), eye),
                // the side displays lie flat on the side consoles' inner faces (Art/blender/models/cockpit.py, side_display)
                SideLeft = Screen(root, "Display Side Left", new Vector3(-0.5293f, -0.2039f, 0.205f), new Vector2(0.18f, 0.13f),
                                  Quaternion.LookRotation(new Vector3(-0.9063f, -0.4226f, 0f), new Vector3(-0.4226f, 0.9063f, 0f))),
                SideRight = Screen(root, "Display Side Right", new Vector3(0.5293f, -0.2039f, 0.205f), new Vector2(0.18f, 0.13f),
                                   Quaternion.LookRotation(new Vector3(0.9063f, -0.4226f, 0f), new Vector3(0.4226f, 0.9063f, 0f))),
                // 62 degrees up and 0.85 m from the eye, above the window (cockpit.py, OVERHEAD)
                Overhead = Screen(root, "Display Overhead", new Vector3(0f, 0.8705f, 0.3491f), new Vector2(0.44f, 0.075f), eye),
            };
            d.Stick = Control(root, "Stick", new Vector3(0.24f, -0.52f, 0.22f), 0.22f, 0.024f);
            d.Throttle = Control(root, "Throttle", new Vector3(-0.3f, -0.48f, 0.12f), 0.16f, 0.02f);
            return d;
        }

        // --- shell ------------------------------------------------------------------------

        /// <summary>
        /// The ball, open at the front. Each of the 12 sectors starts at the round window edge and runs back
        /// to the rear pole in 10 rows, so the facets stay even. Faces point inward (toward the eye).
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

        /// <summary>Angle from the forward axis to the round window edge (the Interceptor's window).</summary>
        static float WindowEdgeAngle(float phiDeg, float windowHalfAngle) => windowHalfAngle;

        static Color Shade(Vector3 p, float r)
        {
            float low = Mathf.InverseLerp(r, -r, p.y);      // 0 at the top, 1 at the bottom
            float back = Mathf.InverseLerp(r, -r, p.z);     // 0 at the front, 1 at the back
            float v = Mathf.Lerp(1f, 0.55f, low * 0.7f + back * 0.3f);
            return new Color(v, v, v, 1f);
        }

        // --- window frame -----------------------------------------------------------------

        /// <summary>The round window edge, eight spokes and the small hub ring (the Interceptor's window).</summary>
        static Mesh FrameMesh(float r, float windowHalfAngle)
        {
            const int Segments = 24;
            const float Hub = 15f;
            var b = new MeshKit.Builder();
            for (int i = 0; i < Segments; i++)
            {
                float a0 = i * 360f / Segments, a1 = (i + 1) * 360f / Segments;
                b.Prism(SpherePoint(r - 0.015f, windowHalfAngle, a0), SpherePoint(r - 0.015f, windowHalfAngle, a1), 0.04f, 0.04f, 4);
                b.Prism(SpherePoint(r - 0.015f, Hub, a0), SpherePoint(r - 0.015f, Hub, a1), 0.012f, 0.012f, 4);
            }
            for (int i = 0; i < 8; i++)
            {
                float phi = 22.5f + 45f * i;   // none straight up, down or sideways: the horizon stays clear
                b.Prism(SpherePoint(r - 0.015f, windowHalfAngle, phi), SpherePoint(r - 0.015f, Hub, phi), 0.016f, 0.01f, 4);
            }
            return b.ToMesh("Cockpit Window Frame");
        }

        /// <summary>Small glowing studs at the spokes' outer ends and a strip under the window.</summary>
        static Mesh AccentMesh(float r, float windowHalfAngle)
        {
            var b = new MeshKit.Builder();
            for (int i = 0; i < 8; i++)
                b.Ico(SpherePoint(r - 0.05f, windowHalfAngle + 3f, 22.5f + 45f * i), Vector3.one * 0.012f);
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
            // side consoles, their inner faces turned up toward the pilot (the side displays lie on them; as in the model)
            b.Box(new Vector3(-0.6f, -0.27f, 0.12f), new Vector3(0.16f, 0.26f, 0.42f), Quaternion.Euler(0f, 0f, 25f));
            b.Box(new Vector3(0.6f, -0.27f, 0.12f), new Vector3(0.16f, 0.26f, 0.42f), Quaternion.Euler(0f, 0f, -25f));
            // seat: pan, back and headrest behind the eye point
            b.Box(new Vector3(0f, -0.6f, -0.12f), new Vector3(0.5f, 0.1f, 0.5f), Quaternion.identity);
            b.Box(new Vector3(0f, -0.15f, -0.42f), new Vector3(0.52f, 0.8f, 0.1f), Quaternion.Euler(-8f, 0f, 0f), 0.85f);
            b.Box(new Vector3(0f, 0.32f, -0.48f), new Vector3(0.3f, 0.18f, 0.08f), Quaternion.Euler(-8f, 0f, 0f));
            // floor plate
            b.Box(new Vector3(0f, -0.78f, 0.1f), new Vector3(0.9f, 0.04f, 0.9f), Quaternion.identity);
            return b.ToMesh("Cockpit Interior", p => Shade(p, 1f));
        }

        /// <summary>A dark display surface facing the eye, with an anchor for its text in front of it.</summary>
        static Transform Screen(Transform root, string name, Vector3 center, Vector2 size, Vector3 eye) =>
            Screen(root, name, center, size, Quaternion.LookRotation(center - eye, Vector3.up));   // +Z away from the eye

        /// <summary>A dark display surface turned by rotation (+Z away from the pilot), with an anchor for its text.</summary>
        static Transform Screen(Transform root, string name, Vector3 center, Vector2 size, Quaternion rotation)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(root, false);
            anchor.localPosition = center;
            anchor.localRotation = rotation;

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
            var controls = ArtLibrary.Get("CockpitControls");
            if (controls != null && controls.Meshes.TryGetValue("CockpitControls_" + name, out var mesh))
            {
                MeshKit.Part("Mesh", pivot, mesh, controls.Material);
                return pivot;
            }
            var b = new MeshKit.Builder();
            b.Prism(Vector3.zero, new Vector3(0f, length, 0f), radius * 1.6f, radius, 6);
            b.Box(new Vector3(0f, length + 0.04f, 0f), new Vector3(radius * 2.4f, 0.09f, radius * 2.6f), Quaternion.identity, 0.8f);
            MeshKit.Part("Mesh", pivot, b.ToMesh(name + " Mesh"), Mats.Get(Mats.CockpitFrame));
            return pivot;
        }
    }
}
