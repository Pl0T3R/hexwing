using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The player's ship as a combatant: hull and shields from the config, a hit sphere, collision damage,
    /// feedback when hit (haptics on both hands, a crackle, the cockpit lights flash red) and death
    /// (explosion outside, fade, restart of the mode). Leaving the arena for too long destroys it.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class PlayerShip : MonoBehaviour
    {
        public static PlayerShip Instance { get; private set; }

        public Damageable Health { get; private set; }
        public int Deaths { get; private set; }
        public float LastHitTime { get; private set; } = -99f;

        ShipFlight _flight;
        ShipWeapons _weapons;
        PlayerInput _input;
        CockpitAlert _alert;

        void Awake()
        {
            Instance = this;
            var cfg = GameConfig.Instance.combat;
            Health = GetComponent<Damageable>();
            if (Health == null) Health = gameObject.AddComponent<Damageable>();
            Health.Team = Team.Player;
            Health.Targetable = true;
            Health.DisplayName = "GRACZ";
            Health.MaxHull = cfg.hull;
            Health.MaxShields = cfg.shields;
            Health.ShieldDelay = cfg.shieldDelay;
            Health.ShieldRegen = cfg.shieldRegen;
            Health.Restore();
            Health.Damaged += OnDamaged;
            Health.Destroyed += OnDestroyed;

            // a hit sphere a bit smaller than the flight collision sphere, centred on the ship
            var hitbox = new GameObject("Hitbox").AddComponent<SphereCollider>();
            hitbox.transform.SetParent(transform, false);
            hitbox.radius = GameConfig.Instance.flight.collisionRadius * 0.8f;

            _flight = GetComponent<ShipFlight>();
            _weapons = GetComponent<ShipWeapons>();
            _input = GetComponent<PlayerInput>();
            _alert = GetComponent<CockpitAlert>();
            if (_flight != null)
            {
                _flight.Collided += OnCollided;
                _flight.EvasiveStarted += _ =>
                {
                    Haptics.PlayBoth(GameConfig.Instance.haptics.evasive);
                    ShipAudio.Instance?.PlayEvasive();
                };
            }
            if (TryGetComponent(out ArenaBounds bounds)) bounds.OutOfBounds += () => Health.Damage(Health.Hull + Health.Shields + 1f, transform.position, null);
        }

        /// <summary>Back to full health at a pose, flying at cruise speed (the view is black).</summary>
        public void Respawn(Pose pose)
        {
            Health.Restore();
            _flight?.ResetTo(pose.position, pose.rotation);
            if (_flight != null) _flight.Frozen = false;
            _weapons?.ResetWeapons();
            _input?.ResetControls();
            _alert?.Clear();
        }

        void OnCollided(Vector3 point, float severity)
        {
            Haptics.PlayBoth(GameConfig.Instance.haptics.collision);
            ShipAudio.Instance?.PlayCollision(severity);
            float damage = GameConfig.Instance.combat.collisionDamage * severity * severity;
            if (damage > 1f) Health.Damage(damage, point, null);
        }

        void OnDamaged(float amount, Vector3 point, Damageable source)
        {
            LastHitTime = Time.time;
            Haptics.PlayBoth(GameConfig.Instance.haptics.hitTaken);
            bool shield = Health.Shields > 0f;
            ShipAudio.Instance?.PlayHitTaken(shield);
            _alert?.Flash(shield);
        }

        void OnDestroyed(Damageable by)
        {
            Deaths++;
            Debug.Log($"[HexWing] Player ship destroyed{(by != null ? " by " + by.DisplayName : "")}.");
            Effects.Instance?.Explode(transform.position + transform.forward * 6f, _flight != null ? _flight.Velocity : Vector3.zero, 3f, 12);
            ShipAudio.Instance?.PlayExplosion(transform.position, 1f);
            if (_flight != null) _flight.Frozen = true;   // the view stops moving; the fade hides the rest
            ModeDirector.Instance?.Restart("zniszczony");
        }
    }
}
