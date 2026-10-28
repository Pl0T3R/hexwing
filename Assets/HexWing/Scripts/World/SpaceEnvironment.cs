using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The light of the scene without real-time lights: one sun direction shared by the faceted shader
    /// (two-tone shading), the hull shader (textured models: sun colour plus a sky/ground ambient) and the
    /// sky shader (the planet's lit side and the sun glow). Set once and whenever it changes; MapTheme
    /// sets each map's values.
    /// </summary>
    [ExecuteAlways]
    public class SpaceEnvironment : MonoBehaviour
    {
        static readonly int SunId = Shader.PropertyToID("_HexSunDirection");
        static readonly int SunColorId = Shader.PropertyToID("_HexSunColor");
        static readonly int SkyId = Shader.PropertyToID("_HexSkyColor");
        static readonly int GroundId = Shader.PropertyToID("_HexGroundColor");

        [Tooltip("Direction the sunlight comes from (toward the sun), in world space.")]
        public Vector3 sunDirection = new(0.45f, 0.55f, 0.7f);
        [Tooltip("Sunlight colour and strength on the textured models (linear).")]
        public Color sunColor = new(1.25f, 1.18f, 1.08f);
        [Tooltip("Ambient from above (the nebula side) and from below, on the textured models.")]
        public Color skyColor = new(0.11f, 0.13f, 0.2f);
        public Color groundColor = new(0.05f, 0.045f, 0.06f);

        Vector3 _applied;
        Color _appliedSun, _appliedSky, _appliedGround;

        void OnEnable() => Apply();

        void Update()
        {
            if (sunDirection != _applied || sunColor != _appliedSun || skyColor != _appliedSky || groundColor != _appliedGround) Apply();
        }

        void Apply()
        {
            _applied = sunDirection;
            _appliedSun = sunColor;
            _appliedSky = skyColor;
            _appliedGround = groundColor;
            var d = sunDirection.sqrMagnitude > 1e-6f ? sunDirection.normalized : Vector3.up;
            Shader.SetGlobalVector(SunId, new Vector4(d.x, d.y, d.z, 0f));
            Shader.SetGlobalColor(SunColorId, sunColor);
            Shader.SetGlobalColor(SkyId, skyColor);
            Shader.SetGlobalColor(GroundId, groundColor);
        }
    }
}
