using UnityEngine;

namespace HexWing
{
    public enum Map { Orbit, Belt }

    /// <summary>A mode that plays on a map other than the default orbit.</summary>
    public interface IMapMode
    {
        Map Map { get; }
    }

    /// <summary>
    /// The two maps' look: sky colours, the planet and the sun direction. Applied to a runtime copy of the
    /// sky material, so the asset never changes.
    /// - Orbit: a blue-grey planet below, a violet-teal nebula, the sun up and to the right (the default).
    /// - Belt: an amber gas giant to the side, a dusty red-brown nebula, a lower, warmer sun; the asteroid
    ///   field of the escort mode lives here.
    /// </summary>
    public static class MapTheme
    {
        public static Map Current { get; private set; } = Map.Orbit;

        static Material _sky, _source;
        static SpaceEnvironment _environment;

        public static void Apply(Map map)
        {
            Current = map;
            if (_source == null) _source = RenderSettings.skybox;
            if (_source == null) return;
            if (_sky == null) _sky = new Material(_source) { name = "SpaceSky (runtime)" };
            _sky.CopyPropertiesFromMaterial(_source);
            if (_environment == null) _environment = Object.FindFirstObjectByType<SpaceEnvironment>();

            if (map == Map.Belt)
            {
                _sky.SetColor("_NebulaColorA", new Color(0.22f, 0.08f, 0.04f));
                _sky.SetColor("_NebulaColorB", new Color(0.12f, 0.07f, 0.03f));
                _sky.SetColor("_SunColor", new Color(1f, 0.82f, 0.6f));
                _sky.SetVector("_PlanetDirection", new Vector4(0.85f, 0.1f, 0.5f, 0f));
                _sky.SetFloat("_PlanetRadius", 30f);
                _sky.SetColor("_PlanetColorA", new Color(0.62f, 0.38f, 0.16f));
                _sky.SetColor("_PlanetColorB", new Color(0.85f, 0.7f, 0.48f));
                _sky.SetColor("_AtmosphereColor", new Color(1f, 0.75f, 0.45f));
                if (_environment != null) _environment.sunDirection = new Vector3(-0.6f, 0.25f, 0.75f);
            }
            else if (_environment != null) _environment.sunDirection = new Vector3(0.45f, 0.55f, 0.7f);

            RenderSettings.skybox = _sky;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _sky = _source = null;
            _environment = null;
            Current = Map.Orbit;
        }
    }
}
