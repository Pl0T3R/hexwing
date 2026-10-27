using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Materials made by the builder in Resources/Materials (so their shaders ship in the build).
    /// A missing asset is rebuilt from its shader with the same colors, so the game never shows pink.
    /// </summary>
    public static class Mats
    {
        public const string CockpitShell = "CockpitShell";     // inner shell panels, dark grey
        public const string CockpitFrame = "CockpitFrame";     // window frame and struts, near black metal
        public const string CockpitTrim = "CockpitTrim";       // seat, consoles, stick: mid grey
        public const string Accent = "AccentLight";            // glowing strips and buttons (emissive)
        public const string Screen = "DisplayScreen";          // dark display glass behind the texts
        public const string Glass = "WindowGlass";             // faint tint on the front window
        public const string Fade = "ScreenFade";               // black fade sphere, drawn over everything
        public const string Text = "DisplayText";              // TextMesh on cockpit displays
        public const string Sky = "SpaceSky";                  // procedural skybox
        public const string Vignette = "ComfortVignette";      // black edge darkening while turning, over everything
        public const string Ring = "CourseRing";               // course rings waiting their turn, and passed ones
        public const string RingNext = "CourseRingNext";       // the ring to fly through next (glows)
        public const string Dust = "SpaceDust";                // small specks around the ship that show speed

        static readonly Dictionary<string, Material> Cache = new();

        public static Material Get(string name)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>($"Materials/{name}");
            if (m == null)
            {
                Debug.LogWarning($"[HexWing] Material {name} missing, creating a fallback. Run HexWing > Build Game.");
                m = CreateFallback(name);
            }
            Cache[name] = m;
            return m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Material CreateFallback(string name)
        {
            string shaderName = name switch
            {
                Glass or Fade or Vignette => "HexWing/Transparent",
                Text => "HexWing/Text",
                Sky => "HexWing/SpaceSky",
                _ => "HexWing/Faceted",
            };
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name, enableInstancing = true };
            // the same colors as HexWingBuilder.CreateMaterials, without the finer settings
            var color = name switch
            {
                CockpitShell => Hex("#3A4048"),
                CockpitFrame => Hex("#1C2026"),
                CockpitTrim => Hex("#5A6270"),
                Accent => Hex("#5FD3FF"),
                Screen => Hex("#0B1117"),
                Glass => new Color(0.55f, 0.75f, 0.9f, 0.06f),
                Fade or Vignette => Color.black,
                Ring => Hex("#4A5868"),
                RingNext => Hex("#FFB347"),
                Dust => Hex("#9AA8B8"),
                _ => Color.white,
            };
            m.SetColor("_BaseColor", color);
            if (name is Accent or RingNext) m.SetFloat("_Emission", 1f);
            if (name == Dust) m.SetFloat("_Emission", 0.6f);
            if (name is Fade or Vignette)
            {
                m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                m.renderQueue = name == Fade ? 4000 : 3990;
            }
            return m;
        }
    }
}
