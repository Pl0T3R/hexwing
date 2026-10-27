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
        public const string LaserPlayer = "LaserPlayer";       // player bolts: cyan
        public const string LaserEnemy = "LaserEnemy";         // enemy bolts: red-orange
        public const string FlashHot = "FlashHot";             // impacts and explosions
        public const string FlashCyan = "FlashCyan";           // player muzzle flashes
        public const string FlashShield = "FlashShield";       // shield hits (translucent)
        public const string Debris = "Debris";                 // explosion shards
        public const string Drone = "Drone";                   // target drone body: light grey
        public const string DroneCore = "DroneCore";           // target drone glowing band
        public const string Missile = "Missile";               // homing missiles
        public const string EnemyHull = "EnemyHull";           // enemy fighters: dark gunmetal
        public const string EliteHull = "EliteHull";           // enemy elite: near black
        public const string EnemyGlow = "EnemyGlow";           // enemy engines and sensors: red
        public const string AllyHull = "AllyHull";             // wingmen: light grey (glow uses Accent)
        public const string CapitalHull = "CapitalHull";       // the capital ship: dark steel
        public const string Generator = "ShieldGenerator";     // its shield generator domes: glowing blue
        public const string Asteroid = "Asteroid";             // rocks of the Belt map
        public const string MenuBack = "MenuBack";             // the hangar/pause menu panel: dark translucent
        public const string HudCyan = "HudCyan";               // holographic crosshair, enemy diamonds, lead marker
        public const string HudWhite = "HudWhite";             // target brackets while locking
        public const string HudAmber = "HudAmber";             // target brackets when locked

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
                Glass or Fade or Vignette or FlashShield or HudCyan or HudWhite or HudAmber or MenuBack => "HexWing/Transparent",
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
                LaserPlayer => Hex("#5FF2FF"),
                LaserEnemy => Hex("#FF4A2A"),
                FlashHot => Hex("#FFC266"),
                FlashCyan => Hex("#9FF4FF"),
                FlashShield => new Color(0.5f, 0.78f, 1f, 0.45f),
                Debris => Hex("#3A3F48"),
                Drone => Hex("#B8BEC6"),
                DroneCore => Hex("#FF5533"),
                Missile => Hex("#D8DDE3"),
                EnemyHull => Hex("#5A5F68"),
                EliteHull => Hex("#2E3138"),
                EnemyGlow => Hex("#FF3B2F"),
                AllyHull => Hex("#C9CED6"),
                CapitalHull => Hex("#4E535C"),
                Generator => Hex("#7FC8FF"),
                Asteroid => Hex("#6B5A4C"),
                MenuBack => new Color(0.02f, 0.04f, 0.07f, 0.78f),
                HudCyan => new Color(0.37f, 0.83f, 1f, 0.9f),
                HudWhite => new Color(1f, 1f, 1f, 0.9f),
                HudAmber => new Color(1f, 0.7f, 0.28f, 0.95f),
                _ => Color.white,
            };
            m.SetColor("_BaseColor", color);
            if (name is Accent or RingNext or LaserPlayer or LaserEnemy or FlashHot or FlashCyan or DroneCore or EnemyGlow) m.SetFloat("_Emission", 1f);
            if (name == Dust) m.SetFloat("_Emission", 0.6f);
            if (name == Generator) m.SetFloat("_Emission", 0.7f);
            if (name is Fade or Vignette)
            {
                m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                m.renderQueue = name == Fade ? 4000 : 3990;
            }
            return m;
        }
    }
}
