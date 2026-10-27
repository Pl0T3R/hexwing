using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Darkens the edges of the view while the ship turns fast (the main comfort tool for flight): a
    /// black funnel around the eye, clear in the middle and soft toward the edge. Its opening narrows and
    /// its strength grows with the ship's turn speed. The level (off / low / medium / high) sets how
    /// strong and how narrow it gets. It is drawn over everything except the fade.
    /// </summary>
    public class ComfortVignette : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // clear inside InnerAngle, fully dark from OuterAngle (degrees from straight ahead) at scale 1
        const float InnerAngle = 40f, OuterAngle = 70f;
        static readonly float[] RingAngles = { 34f, 40f, 47f, 54f, 61f, 70f, 85f, 110f, 150f };
        const int Segments = 32;
        const float Distance = 0.12f;

        ShipFlight _ship;
        Transform _funnel;
        MeshRenderer _renderer;
        Material _material;
        float _intensity;

        /// <summary>0..1, after smoothing (for the HUD and tests).</summary>
        public float Intensity => _intensity;

        public void Attach(Transform eye, ShipFlight ship)
        {
            _ship = ship;
            var go = MeshKit.Part("Comfort Vignette", eye, BuildMesh(), Mats.Get(Mats.Vignette));
            _funnel = go.transform;
            _renderer = go.GetComponent<MeshRenderer>();
            _material = new Material(Mats.Get(Mats.Vignette));
            _renderer.sharedMaterial = _material;
            _renderer.enabled = false;
        }

        void LateUpdate()
        {
            if (_renderer == null || _ship == null) return;
            var cfg = GameConfig.Instance.comfort;
            float target = cfg.vignette == VignetteLevel.Off ? 0f
                : Mathf.InverseLerp(cfg.vignetteStartRate, cfg.vignetteFullRate, _ship.TurnSpeed);
            float dt = Time.unscaledDeltaTime;
            _intensity = Mathf.Lerp(_intensity, target, 1f - Mathf.Exp(-cfg.vignetteResponse * dt));

            Level(cfg.vignette, out float maxAlpha, out float minScale);
            bool on = _intensity * maxAlpha > 0.01f;
            _renderer.enabled = on;
            if (!on) return;
            float s = Mathf.Lerp(1f, minScale, _intensity);
            _funnel.localScale = new Vector3(s, s, 1f);   // narrower in x/y = darker edge reaches further in
            _material.SetColor(BaseColorId, new Color(0f, 0f, 0f, _intensity * maxAlpha));
        }

        static void Level(VignetteLevel level, out float maxAlpha, out float minScale)
        {
            switch (level)
            {
                case VignetteLevel.Low: maxAlpha = 0.6f; minScale = 0.85f; break;
                case VignetteLevel.High: maxAlpha = 1f; minScale = 0.55f; break;
                default: maxAlpha = 0.85f; minScale = 0.7f; break;
            }
        }

        /// <summary>Rings of a funnel around +Z; vertex alpha 0 inside InnerAngle, 1 from OuterAngle.</summary>
        static Mesh BuildMesh()
        {
            var b = new MeshKit.Builder();
            for (int r = 0; r < RingAngles.Length - 1; r++)
            {
                for (int i = 0; i < Segments; i++)
                {
                    var a = Point(RingAngles[r], i);
                    var bb = Point(RingAngles[r + 1], i);
                    var c = Point(RingAngles[r + 1], i + 1);
                    var d = Point(RingAngles[r], i + 1);
                    b.Quad(a, bb, c, d, Vector3.zero);
                }
            }
            return b.ToMesh("Comfort Vignette", p =>
            {
                float angle = Vector3.Angle(Vector3.forward, p);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(InnerAngle, OuterAngle, angle));
                return new Color(1f, 1f, 1f, alpha);
            });
        }

        static Vector3 Point(float angleDeg, int segment)
        {
            float t = angleDeg * Mathf.Deg2Rad, phi = segment * Mathf.PI * 2f / Segments;
            return new Vector3(Mathf.Sin(t) * Mathf.Cos(phi), Mathf.Sin(t) * Mathf.Sin(phi), Mathf.Cos(t)) * Distance;
        }
    }
}
