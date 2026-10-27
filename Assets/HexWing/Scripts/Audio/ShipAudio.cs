using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player ship's sounds, synthesized at start (no audio files): an engine drone whose pitch and
    /// loudness follow the speed, a breathy boost layer, a chime for each ring (a brighter one for the
    /// last), and a thud for collisions. All 2D in the cockpit. M8 replaces this with the full sound set.
    /// </summary>
    public class ShipAudio : MonoBehaviour
    {
        public static ShipAudio Instance { get; private set; }

        const int Rate = 48000;

        ShipFlight _ship;
        AudioSource _engine, _boost, _oneShots;
        AudioClip _ring, _ringLast, _thud;

        void Awake()
        {
            Instance = this;
            _ship = GetComponent<ShipFlight>();
            _engine = Source("Engine", Loop("Engine Drone", EngineSample, 1f), 0.35f);
            _boost = Source("Boost", Loop("Boost Rush", BoostSample, 1f), 0f);
            _oneShots = Source("One Shots", null, 1f);
            _ring = Clip("Ring Chime", 0.45f, t => Chime(t, 880f, 1320f));
            _ringLast = Clip("Ring Chime Last", 0.9f, t => Chime(t, 880f, 1760f) + Chime(t - 0.12f, 1320f, 1980f));
            _thud = Clip("Collision Thud", 0.5f, Thud);
            _engine.Play();
            _boost.Play();
        }

        void Update()
        {
            if (_ship == null) return;
            var f = GameConfig.Instance.flight;
            float s = Mathf.InverseLerp(f.minSpeed, f.maxSpeed, _ship.Speed);
            _engine.pitch = Mathf.Lerp(0.75f, 1.45f, s);
            _engine.volume = Mathf.Lerp(0.22f, 0.42f, s);
            float boost = _ship.CurrentBand == ShipFlight.Band.Boost ? 1f : 0f;
            _boost.volume = Mathf.MoveTowards(_boost.volume, boost * 0.3f, Time.deltaTime * 0.8f);
            _boost.pitch = Mathf.Lerp(0.9f, 1.2f, s);
            Haptics.SetRumble(OVRInput.Controller.Touch, boost * GameConfig.Instance.haptics.boostRumble);
        }

        void OnDisable() => Haptics.SetRumble(OVRInput.Controller.Touch, 0f);

        public void PlayRing(bool last) => _oneShots.PlayOneShot(last ? _ringLast : _ring, 0.6f);
        public void PlayCollision(float severity) => _oneShots.PlayOneShot(_thud, Mathf.Lerp(0.4f, 1f, severity));

        AudioSource Source(string name, AudioClip clip, float volume)
        {
            var go = new GameObject("Audio " + name);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = clip != null;
            a.volume = volume;
            a.spatialBlend = 0f;
            a.playOnAwake = false;
            a.dopplerLevel = 0f;
            return a;
        }

        // --- synthesis ------------------------------------------------------------------------

        static AudioClip Loop(string name, System.Func<float, System.Random, float> sample, float seconds)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = sample(i / (float)Rate, rng);
            // crossfade the last 20 ms into the start so the loop has no click
            int fade = Rate / 50;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[n - fade + i] = Mathf.Lerp(data[n - fade + i], data[i], w);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Clip(string name, float seconds, System.Func<float, float> sample)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float _lowpass, _band;

        /// <summary>Two detuned saws (whole cycles per second, so the loop is seamless) plus a low rumble.</summary>
        static float EngineSample(float t, System.Random rng)
        {
            float saw1 = 2f * Frac(t * 55f) - 1f;
            float saw2 = 2f * Frac(t * 83f) - 1f;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            _lowpass += (noise - _lowpass) * 0.02f;
            float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t);
            return (saw1 * 0.25f + saw2 * 0.12f) * wobble + _lowpass * 0.9f;
        }

        /// <summary>Band-limited noise: the rush of air (well, plasma) when boosting.</summary>
        static float BoostSample(float t, System.Random rng)
        {
            float noise = (float)rng.NextDouble() * 2f - 1f;
            _band += (noise - _band) * 0.25f;
            return _band * 0.7f;
        }

        static float Chime(float t, float f1, float f2)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 400f);
            return (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.25f) * env;
        }

        static float Thud(float t)
        {
            float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 300f);
            float freq = Mathf.Lerp(90f, 40f, Mathf.Clamp01(t * 3f));
            float noise = Mathf.Sin(t * 12345.678f) * Mathf.Sin(t * 3456.7f);   // cheap deterministic grit
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f + noise * 0.3f * Mathf.Exp(-t * 25f)) * env;
        }

        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
