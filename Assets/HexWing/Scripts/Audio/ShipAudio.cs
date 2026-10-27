using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The game's sounds, synthesized at start (no audio files): an engine drone whose pitch and loudness
    /// follow the speed, a breathy boost layer, ring chimes, collision thuds, laser shots (left and right
    /// gun panned), overheat alarm, lock-on tones, missile launch, hits given and taken, kills, the evasive
    /// whoosh, and explosions placed in 3D (a small pool of sources). M8 replaces this with the full set.
    /// </summary>
    public class ShipAudio : MonoBehaviour
    {
        public static ShipAudio Instance { get; private set; }

        const int Rate = 48000;

        ShipFlight _ship;
        AudioSource _engine, _boost, _oneShots, _gunLeft, _gunRight;
        AudioClip _ring, _ringLast, _thud, _laser, _overheat, _lockTick, _locked, _missile, _explosion, _hitShield, _hitHull, _hitDealt, _kill, _evasive, _warnMissile, _warnLock, _ability, _denied;
        readonly AudioSource[] _world = new AudioSource[6];
        int _nextWorld;

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
            _laser = Clip("Laser", 0.18f, Laser);
            _overheat = Clip("Overheat", 0.6f, t => Square(t, t < 0.3f ? 620f : 470f) * 0.25f * Mathf.Clamp01((0.6f - t) * 20f));
            _lockTick = Clip("Lock Tick", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 1500f * t) * 0.3f * Mathf.Clamp01((0.05f - t) * 60f));
            _locked = Clip("Locked", 0.35f, t => Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.28f * Mathf.Clamp01((0.35f - t) * 30f));
            _missile = Clip("Missile", 1.1f, Missile);
            _explosion = Clip("Explosion", 1.6f, Explosion);
            _hitShield = Clip("Hit Shield", 0.3f, t => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 300f, t * 3f) * t) * 0.4f + Grit(t) * 0.25f) * Mathf.Exp(-t * 14f));
            _hitHull = Clip("Hit Hull", 0.4f, t => (Grit(t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.5f) * Mathf.Exp(-t * 10f));
            _hitDealt = Clip("Hit Dealt", 0.06f, t => Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.2f * Mathf.Exp(-t * 60f));
            _kill = Clip("Kill", 0.5f, t => Chime(t, 660f, 990f) + Chime(t - 0.08f, 990f, 1480f));
            _evasive = Clip("Evasive", 0.6f, t => Grit(t * 0.5f) * 0.35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.6f)));
            _warnMissile = Clip("Warn Missile", 0.09f, t => Square(t, 1250f) * 0.18f * Mathf.Clamp01((0.09f - t) * 80f));
            _warnLock = Clip("Warn Lock", 0.12f, t => Square(t, 700f) * 0.15f * Mathf.Clamp01((0.12f - t) * 60f));
            _ability = Clip("Ability", 0.4f, t => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(400f, 1200f, t / 0.4f) * t) * 0.3f) * Mathf.Clamp01((0.4f - t) * 10f) * Mathf.Clamp01(t * 200f));
            _denied = Clip("Denied", 0.18f, t => Square(t, 180f) * 0.15f * Mathf.Clamp01((0.18f - t) * 40f));
            _gunLeft = Source("Gun Left", null, 0.5f);
            _gunLeft.panStereo = -0.45f;
            _gunRight = Source("Gun Right", null, 0.5f);
            _gunRight.panStereo = 0.45f;
            for (int i = 0; i < _world.Length; i++)
            {
                var go = new GameObject("Audio World " + i);
                go.transform.SetParent(transform.parent, false);   // not on the ship: these stay where things blew up
                var a = go.AddComponent<AudioSource>();
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 30f;
                a.maxDistance = 1800f;
                a.dopplerLevel = 0f;
                a.playOnAwake = false;
                _world[i] = a;
            }
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
        public void PlayLaser(int gun) => (gun == 0 ? _gunLeft : _gunRight).PlayOneShot(_laser, 0.55f);
        public void PlayOverheat() => _oneShots.PlayOneShot(_overheat, 0.7f);
        public void PlayLockTick(float progress) => _oneShots.PlayOneShot(_lockTick, Mathf.Lerp(0.4f, 0.8f, progress));
        public void PlayLocked() => _oneShots.PlayOneShot(_locked, 0.7f);
        public void PlayMissile() => _oneShots.PlayOneShot(_missile, 0.8f);
        public void PlayHitTaken(bool shield) => _oneShots.PlayOneShot(shield ? _hitShield : _hitHull, 0.9f);
        public void PlayHitDealt() => _oneShots.PlayOneShot(_hitDealt, 0.5f);
        public void PlayKill() => _oneShots.PlayOneShot(_kill, 0.7f);
        public void PlayEvasive() => _oneShots.PlayOneShot(_evasive, 0.7f);
        public void PlayAbility() => _oneShots.PlayOneShot(_ability, 0.6f);
        public void PlayDenied() => _oneShots.PlayOneShot(_denied, 0.5f);
        public void PlayStep() => _oneShots.PlayOneShot(_ringLast, 0.5f);
        /// <summary>Called a few times a second while threatened: fast high beeps for a missile, slower low ones for a lock.</summary>
        public void PlayMissileWarning(bool missile) => _oneShots.PlayOneShot(missile ? _warnMissile : _warnLock, 0.5f);

        /// <summary>An explosion out in the world, heard from where it happened.</summary>
        public void PlayExplosion(Vector3 position, float size)
        {
            var a = _world[_nextWorld];
            _nextWorld = (_nextWorld + 1) % _world.Length;
            a.transform.position = position;
            a.pitch = Mathf.Lerp(1.3f, 0.8f, size);
            a.PlayOneShot(_explosion, Mathf.Lerp(0.5f, 1f, size));
        }

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

        static float Laser(float t)
        {
            // a falling zap with a short noisy attack
            float f = Mathf.Lerp(1800f, 380f, Mathf.Clamp01(t / 0.16f));
            float phase = 2f * Mathf.PI * (1800f * t - (1800f - 380f) * t * t / (2f * 0.16f));
            float env = Mathf.Exp(-t * 18f) * Mathf.Clamp01(t * 800f);
            return (Square(t, f * 0.5f) * 0.15f + Mathf.Sin(phase) * 0.35f + Grit(t) * 0.1f * Mathf.Exp(-t * 60f)) * env;
        }

        static float Missile(float t)
        {
            float env = Mathf.Clamp01(t * 30f) * Mathf.Exp(-t * 2.2f);
            return (Grit(t) * 0.5f + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 260f, t) * t) * 0.2f) * env;
        }

        static float Explosion(float t)
        {
            float env = Mathf.Clamp01(t * 200f) * Mathf.Exp(-t * 3f);
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(60f, 28f, Mathf.Clamp01(t)) * t);
            return Mathf.Clamp(boom * 0.7f + Grit(t) * 0.8f * Mathf.Exp(-t * 2f), -1f, 1f) * env;
        }

        static float Square(float t, float f) => Frac(t * f) < 0.5f ? 1f : -1f;

        /// <summary>Deterministic noise from time (hash), for one-shot clips.</summary>
        static float Grit(float t)
        {
            float x = Mathf.Sin(t * 12.9898f * 4800f) * 43758.5453f;
            return (x - Mathf.Floor(x)) * 2f - 1f;
        }

        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
