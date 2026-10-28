using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// A ship's guns, targeting and lock-on. Works for any pilot, through <see cref="ShipFlight.Controls"/>.
    ///
    /// - Lasers: two guns alternate at the fire rate; their bolts cross at the convergence distance.
    ///   Every shot adds heat; at full heat the guns lock for a few seconds (overheat), and heat drains
    ///   while not firing.
    /// - Target: the live enemy closest to the nose within the target cone and range (rechecked 10× a
    ///   second). Its lead point is where to aim so bolts meet it.
    /// - Lock-on: hold the lock button while the target is within the lock cone; after the lock time the
    ///   target is locked (tones while locking, a steady tone when locked). Release the button while
    ///   locked to fire a homing missile (cooldown). Losing the target resets the lock.
    ///
    /// For the player ship it also plays the haptics and sounds.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class ShipWeapons : MonoBehaviour
    {
        static readonly Vector3[] DefaultGuns = { new(-1.7f, -0.55f, 2.6f), new(1.7f, -0.55f, 2.6f) };
        Vector3[] _guns = DefaultGuns;

        public static readonly System.Collections.Generic.List<ShipWeapons> All = new();

        public bool IsPlayer = true;
        /// <summary>AI difficulty: laser damage and fire rate relative to the config.</summary>
        public float DamageScale = 1f;
        public float FireRateScale = 1f;
        /// <summary>Can lock on and fire missiles (the player, elite AI).</summary>
        public bool HasMissiles = true;
        /// <summary>Heat per shot relative to the config (ship class, cooling passive, targeting computer).</summary>
        public float HeatScale = 1f;
        /// <summary>Extra damage multiplier while an ability is active.</summary>
        public float DamageBoost = 1f;

        public float Heat { get; private set; }
        public bool Overheated => _lockoutLeft > 0f;
        public float LockoutLeft => _lockoutLeft;
        public Damageable Target { get; private set; }
        /// <summary>This ship's own hull and shields.</summary>
        public Damageable Self => _self;
        /// <summary>0..1 while locking; 1 = locked.</summary>
        public float LockProgress { get; private set; }
        public bool Locked => LockProgress >= 1f && Target != null;
        public float MissileCooldownLeft => Mathf.Max(0f, _missileReadyAt - Time.time);
        /// <summary>Where to aim at the target so bolts meet it (valid while <see cref="Target"/> is set).</summary>
        public Vector3 LeadPoint { get; private set; }

        public event System.Action<int> Fired;   // gun index
        public event System.Action MissileFired;

        ShipFlight _flight;
        Damageable _self;
        float _nextShot, _lockoutLeft, _missileReadyAt, _nextTargetCheck;
        int _gun;
        bool _lockHeld;
        int _lockTicks;
        System.Random _rng = new(3);

        void Awake()
        {
            _flight = GetComponent<ShipFlight>();
            _self = GetComponent<Damageable>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public void ResetWeapons()
        {
            Heat = 0f;
            _lockoutLeft = 0f;
            _missileReadyAt = 0f;
            LockProgress = 0f;
            Target = null;
            _lockHeld = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _flight == null || _flight.Frozen) return;
            if (_self != null && !_self.Alive) return;
            var cfg = GameConfig.Instance.combat;
            var c = _flight.Controls;

            // --- heat and guns ----------------------------------------------------------------
            if (_lockoutLeft > 0f)
            {
                _lockoutLeft -= dt;
                Heat = Mathf.Lerp(0f, 1f, _lockoutLeft / cfg.overheatLockout);
            }
            else if (!c.Fire) Heat = Mathf.Max(0f, Heat - cfg.coolRate * dt);

            if (c.Fire && _lockoutLeft <= 0f && Time.time >= _nextShot)
            {
                _nextShot = Time.time + 1f / (cfg.fireRate * Mathf.Max(0.05f, FireRateScale));
                Shoot(cfg);
                Heat += cfg.heatPerShot * HeatScale;
                if (Heat >= 1f)
                {
                    Heat = 1f;
                    _lockoutLeft = cfg.overheatLockout;
                    if (IsPlayer)
                    {
                        Haptics.Play(PlayerFlyHand, GameConfig.Instance.haptics.overheat);
                        ShipAudio.Instance?.PlayOverheat();
                    }
                }
            }

            // --- target and lead -------------------------------------------------------------------
            if (Time.time >= _nextTargetCheck || (Target != null && !Target.Alive))
            {
                _nextTargetCheck = Time.time + 0.1f;
                var best = FindTarget(cfg);
                // keep a lock while the locked target stays valid, even if another one is closer to the nose
                if (best != Target && !(Locked && Target != null && Target.Alive && InCone(Target, cfg.targetCone, cfg.targetRange)))
                {
                    Target = best;
                    LockProgress = 0f;
                    _lockTicks = 0;
                }
            }
            if (Target != null) LeadPoint = Lead(Target, cfg.laserSpeed);

            // --- lock-on and missiles -----------------------------------------------------------
            bool lockNow = c.Lock && HasMissiles;
            if (lockNow && Target != null && MissileCooldownLeft <= 0f)
            {
                if (LockProgress < 1f)
                {
                    if (InCone(Target, cfg.lockCone, cfg.targetRange))
                    {
                        LockProgress = Mathf.Min(1f, LockProgress + dt / cfg.lockSeconds);
                        int ticks = Mathf.FloorToInt(LockProgress * 6f);
                        if (IsPlayer && ticks > _lockTicks)
                        {
                            Haptics.Play(PlayerFlyHand, GameConfig.Instance.haptics.lockTick);
                            ShipAudio.Instance?.PlayLockTick(LockProgress);
                        }
                        _lockTicks = ticks;
                        if (LockProgress >= 1f && IsPlayer)
                        {
                            Haptics.Play(PlayerFlyHand, GameConfig.Instance.haptics.locked);
                            ShipAudio.Instance?.PlayLocked();
                        }
                    }
                    else LockProgress = Mathf.Max(0f, LockProgress - dt / cfg.lockSeconds);
                }
            }
            else if (!lockNow && _lockHeld && Locked) FireMissile(cfg);
            else if (!lockNow) { LockProgress = 0f; _lockTicks = 0; }
            _lockHeld = lockNow;
        }

        OVRInput.Controller PlayerFlyHand => TryGetComponent(out PlayerInput input) ? input.FlyHand : OVRInput.Controller.RTouch;

        void Shoot(CombatSettings cfg)
        {
            var local = _guns[_gun % _guns.Length];
            int side = local.x < 0f ? 0 : 1;   // left or right, for the sound and listeners
            var gun = transform.TransformPoint(local);
            var convergence = transform.position + transform.forward * cfg.convergence;
            var dir = (convergence - gun).normalized;
            if (cfg.spread > 0f)
                dir = Quaternion.Euler(Rand() * cfg.spread, Rand() * cfg.spread, 0f) * dir;
            var team = _self != null ? _self.Team : (IsPlayer ? Team.Player : Team.Enemy);
            LaserSystem.Instance?.Fire(gun, dir, _flight.Velocity, _self, team, cfg.laserDamage * DamageScale * DamageBoost);
            Effects.Instance?.SpawnFlash(gun + transform.forward * 0.6f, 0.9f, 0.06f, team == Team.Player ? Effects.FlashKind.Muzzle : Effects.FlashKind.Impact);
            Fired?.Invoke(side);
            if (IsPlayer)
            {
                Haptics.Play(PlayerFlyHand, GameConfig.Instance.haptics.fire);
                ShipAudio.Instance?.PlayLaser(side);
            }
            _gun = (_gun + 1) % _guns.Length;
        }

        float Rand() => (float)_rng.NextDouble() * 2f - 1f;

        /// <summary>
        /// Muzzle points in the ship's local space (the model's Gun sockets: the wingtip cannons). They
        /// fire in turn; null or empty keeps the default pair under the nose.
        /// </summary>
        public void SetGuns(Vector3[] points)
        {
            _guns = points != null && points.Length > 0 ? points : DefaultGuns;
            _gun = 0;
        }

        void FireMissile(CombatSettings cfg)
        {
            var team = _self != null ? _self.Team : Team.Player;
            var start = transform.TransformPoint(new Vector3(0f, -1.4f, 2f));
            if (MissileSystem.Instance == null || !MissileSystem.Instance.Launch(start, transform.forward, Target, _self, team)) return;
            _missileReadyAt = Time.time + cfg.missileCooldown;
            LockProgress = 0f;
            _lockTicks = 0;
            MissileFired?.Invoke();
            if (IsPlayer)
            {
                Haptics.Play(PlayerFlyHand, GameConfig.Instance.haptics.missile);
                ShipAudio.Instance?.PlayMissile();
            }
        }

        Damageable FindTarget(CombatSettings cfg)
        {
            var myTeam = _self != null ? _self.Team : Team.Player;
            Damageable best = null;
            float bestAngle = cfg.targetCone;
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d == _self || !d.Alive || !d.Targetable || d.Team == myTeam || d.Team == Team.Neutral) continue;
                var to = d.transform.position - transform.position;
                if (to.sqrMagnitude > cfg.targetRange * cfg.targetRange) continue;
                float angle = Vector3.Angle(transform.forward, to);
                if (angle < bestAngle) { bestAngle = angle; best = d; }
            }
            return best;
        }

        bool InCone(Damageable d, float cone, float range)
        {
            var to = d.transform.position - transform.position;
            return to.sqrMagnitude <= range * range && Vector3.Angle(transform.forward, to) <= cone;
        }

        /// <summary>
        /// Intercept point for a bolt fired now: bolts inherit the shooter's velocity, so solve in the
        /// shooter's frame with the target's relative velocity. Falls back to the target itself.
        /// </summary>
        public Vector3 Lead(Damageable target, float boltSpeed)
        {
            var p = target.transform.position - transform.position;
            var v = target.Velocity - (_flight != null ? _flight.Velocity : Vector3.zero);
            float a = Vector3.Dot(v, v) - boltSpeed * boltSpeed;
            float b = 2f * Vector3.Dot(p, v);
            float cc = Vector3.Dot(p, p);
            float disc = b * b - 4f * a * cc;
            if (disc < 0f || Mathf.Abs(a) < 1e-3f) return target.transform.position;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b - sq) / (2f * a), t2 = (-b + sq) / (2f * a);
            float t = Mathf.Min(t1, t2) > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            if (t <= 0f) return target.transform.position;
            return transform.position + p + v * t;
        }
    }
}
