using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The light of the scene without real-time lights: one sun direction shared by the faceted shader
    /// (two-tone shading of ships and cockpit) and the sky shader (the planet's lit side and the sun glow).
    /// Set once and whenever it changes.
    /// </summary>
    [ExecuteAlways]
    public class SpaceEnvironment : MonoBehaviour
    {
        static readonly int SunId = Shader.PropertyToID("_HexSunDirection");

        [Tooltip("Direction the sunlight comes from (toward the sun), in world space.")]
        public Vector3 sunDirection = new(0.45f, 0.55f, 0.7f);

        Vector3 _applied;

        void OnEnable() => Apply();

        void Update()
        {
            if (sunDirection != _applied) Apply();
        }

        void Apply()
        {
            _applied = sunDirection;
            var d = sunDirection.sqrMagnitude > 1e-6f ? sunDirection.normalized : Vector3.up;
            Shader.SetGlobalVector(SunId, new Vector4(d.x, d.y, d.z, 0f));
        }
    }
}
