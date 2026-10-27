using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Fades the view to black and back (match start and end, recentering): a small black sphere around
    /// the eyes, drawn over everything. Comfort rule: transitions fade, the camera never moves on its own.
    /// Runs on real time, so it also works while the game is paused.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MeshRenderer _renderer;
        Material _material;
        float _alpha = 1f;   // starts black: the first fade-in reveals the cockpit
        Coroutine _running;

        public bool IsBlack => _alpha >= 0.999f;

        void Awake() => Instance = this;

        /// <summary>Puts the fade sphere on the eye anchor.</summary>
        public void Attach(Transform eye)
        {
            var b = new MeshKit.Builder();
            b.Ico(Vector3.zero, Vector3.one * 0.25f);   // 20 facets are plenty: it's a flat colour, culled off
            var go = MeshKit.Part("Screen Fade", eye, b.ToMesh("Fade Sphere"), Mats.Get(Mats.Fade));
            _renderer = go.GetComponent<MeshRenderer>();
            _material = new Material(Mats.Get(Mats.Fade));
            _renderer.sharedMaterial = _material;
            Apply();
        }

        public Coroutine FadeIn(float seconds) => FadeTo(0f, seconds);
        public Coroutine FadeOut(float seconds) => FadeTo(1f, seconds);

        public Coroutine FadeTo(float target, float seconds)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(target, seconds));
            return _running;
        }

        IEnumerator Run(float target, float seconds)
        {
            float start = _alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                Apply();
                yield return null;
            }
            _alpha = target;
            Apply();
            _running = null;
        }

        void Apply()
        {
            if (_renderer == null) return;
            _renderer.enabled = _alpha > 0.001f;
            _material.SetColor(BaseColorId, new Color(0f, 0f, 0f, _alpha));
        }
    }
}
