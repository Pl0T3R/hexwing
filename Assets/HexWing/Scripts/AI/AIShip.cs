using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// An AI fighter: hull and glow meshes, a hit sphere, <see cref="Damageable"/>, <see cref="ShipFlight"/>
    /// (no comfort options), <see cref="ShipWeapons"/> and an <see cref="AIPilot"/>. Made once and reused
    /// (pooled by the mode). Trails dark smoke when badly damaged; destroyed, it explodes and hides.
    /// </summary>
    public class AIShip : MonoBehaviour
    {
        public ShipMeshes.Kind Kind { get; private set; }
        public Damageable Health { get; private set; }
        public ShipFlight Flight { get; private set; }
        public ShipWeapons Weapons { get; private set; }
        public AIPilot Pilot { get; private set; }
        public bool Active => gameObject.activeSelf && Health.Alive;

        /// <summary>Raised when destroyed (this ship, who killed it).</summary>
        public event System.Action<AIShip, Damageable> Killed;

        FlightSettings _flightSettings;
        float _smokeTimer;
        AudioSource _engine;

        public static AIShip Create(Transform parent, ShipMeshes.Kind kind, int index)
        {
            var go = new GameObject($"{kind} {index + 1}");
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            bool ally = kind == ShipMeshes.Kind.Swallow;
            MeshKit.Part("Hull", go.transform, ShipMeshes.Hull(kind), Mats.Get(ally ? Mats.AllyHull : kind == ShipMeshes.Kind.Trident ? Mats.EliteHull : Mats.EnemyHull));
            MeshKit.Part("Glow", go.transform, ShipMeshes.Glow(kind), Mats.Get(ally ? Mats.Accent : kind == ShipMeshes.Kind.Trident ? Mats.RingNext : Mats.EnemyGlow));
            go.AddComponent<SphereCollider>().radius = ShipMeshes.Radius(kind);

            var ship = go.AddComponent<AIShip>();
            ship.Kind = kind;
            ship.Health = go.AddComponent<Damageable>();
            ship.Flight = go.AddComponent<ShipFlight>();
            ship.Weapons = go.AddComponent<ShipWeapons>();
            ship.Pilot = go.AddComponent<AIPilot>();
            ship.Flight.Pilot = ship.Pilot;
            ship.Flight.ApplyComfort = false;
            ship.Weapons.IsPlayer = false;
            ship.Health.Destroyed += ship.OnDestroyed;
            // engine hum: 3D with Doppler, so a close pass is heard going by
            var engine = go.AddComponent<AudioSource>();
            engine.loop = true;
            engine.spatialBlend = 1f;
            engine.rolloffMode = AudioRolloffMode.Linear;
            engine.minDistance = 15f;
            engine.maxDistance = 450f;
            engine.dopplerLevel = 0.7f;
            engine.volume = 0.3f;
            engine.playOnAwake = false;
            ship._engine = engine;
            return ship;
        }

        /// <summary>Puts the ship into play at a pose with a skill level (0 = rookie, 1 = ace).</summary>
        public void Spawn(Pose pose, Team team, float skill, float hull, float shields, bool wingman, int slot)
        {
            var ai = GameConfig.Instance.ai;
            var baseFlight = GameConfig.Instance.flight;
            _flightSettings ??= new FlightSettings();
            CopyFlight(baseFlight, _flightSettings);
            float turn = Mathf.Lerp(ai.turnScale.x, ai.turnScale.y, skill);
            if (Kind == ShipMeshes.Kind.Trident) { turn *= 0.9f; _flightSettings.maxSpeed *= 1.1f; }
            _flightSettings.pitchRate *= turn;
            _flightSettings.yawRate *= turn;
            Flight.SettingsOverride = _flightSettings;

            Health.Team = team;
            Health.DisplayName = Kind switch
            {
                ShipMeshes.Kind.Trident => "GROT",
                ShipMeshes.Kind.Swallow => "SKRZYDŁOWY",
                _ => "SZERSZEŃ",
            };
            Health.MaxHull = hull;
            Health.MaxShields = shields;
            Health.Targetable = true;
            Weapons.DamageScale = Mathf.Lerp(ai.damageScale.x, ai.damageScale.y, skill) * (team == Team.Player ? 0.8f : 1f);
            Weapons.FireRateScale = Kind == ShipMeshes.Kind.Trident ? 0.9f : 0.75f;
            Weapons.HasMissiles = Kind == ShipMeshes.Kind.Trident;

            gameObject.SetActive(true);
            Health.enabled = true;
            Health.Restore();
            Flight.ResetTo(pose.position, pose.rotation);
            Flight.Frozen = false;
            Weapons.ResetWeapons();
            Pilot.ResetPilot(skill, wingman, slot);
            _smokeTimer = 0f;
            if (ShipAudio.Instance != null && ShipAudio.Instance.EngineClip != null)
            {
                _engine.clip = ShipAudio.Instance.EngineClip;
                _engine.pitch = Kind == ShipMeshes.Kind.Trident ? 0.8f : Kind == ShipMeshes.Kind.Swallow ? 1.15f : 1.3f;
                _engine.time = Random.value * 0.9f;   // ships don't hum in unison
                _engine.Play();
            }
        }

        public void Despawn() => gameObject.SetActive(false);

        void Update()
        {
            // AudioSources ignore the time scale: hold the hum while a menu or focus loss pauses the game
            if (GamePause.Paused) { if (_engine.isPlaying) _engine.Pause(); }
            else if (!_engine.isPlaying && _engine.clip != null) _engine.UnPause();
            if (!Health.Alive || Health.Hull > Health.MaxHull * 0.5f) return;
            _smokeTimer -= Time.deltaTime;
            if (_smokeTimer > 0f) return;
            _smokeTimer = 0.08f;
            Effects.Instance?.SpawnFlash(transform.position - transform.forward * 4f, 1.6f, 0.9f, Effects.FlashKind.Smoke);
        }

        void OnDestroyed(Damageable by)
        {
            Effects.Instance?.Explode(transform.position, Flight.Velocity, Kind == ShipMeshes.Kind.Trident ? 6f : 4.5f, Kind == ShipMeshes.Kind.Trident ? 28 : 18);
            ShipAudio.Instance?.PlayExplosion(transform.position, 1f);
            Killed?.Invoke(this, by);
            gameObject.SetActive(false);
        }

        static void CopyFlight(FlightSettings from, FlightSettings to)
        {
            to.minSpeed = from.minSpeed; to.maxSpeed = from.maxSpeed;
            to.acceleration = from.acceleration; to.deceleration = from.deceleration;
            to.pitchRate = from.pitchRate; to.yawRate = from.yawRate; to.rollRate = from.rollRate;
            to.angularResponse = from.angularResponse; to.noseLag = from.noseLag;
            to.sweetSpot = from.sweetSpot; to.sweetSpotMultiplier = from.sweetSpotMultiplier;
            to.boostFrom = from.boostFrom; to.boostMultiplier = from.boostMultiplier; to.bandBlend = from.bandBlend;
            to.collisionRadius = from.collisionRadius; to.collisionSpeedKeep = from.collisionSpeedKeep;
        }
    }
}
