using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// An AI pilot: flies a ship through the same <see cref="ShipControls"/> as the player, so it obeys the
    /// same flight model, heat and cooldowns. Decides about 10 times a second (staggered across ships),
    /// steers every frame.
    ///
    /// States:
    /// - Escort: no target; enemies head for the player, wingmen hold their spot beside the player.
    /// - Attack: chase the target, aim at its lead point (with an aim error that shrinks with skill), fire
    ///   in bursts when the lead point is inside the fire cone, use the sweet-spot throttle in hard turns
    ///   and boost to close distance. Elites lock on and fire missiles.
    /// - Extend: too close to the target: boost away for a moment, then come around again (no rams).
    /// - Evade: hit or chased by a missile: break hard to one side at full throttle, sometimes with an
    ///   evasive maneuver.
    /// Far from the arena centre, it turns back in; an obstacle straight ahead (asteroid, capital hull) makes
    /// it veer off along the surface for a moment.
    /// </summary>
    public class AIPilot : MonoBehaviour, IShipPilot
    {
        public enum State { Escort, Attack, Extend, Evade }

        public State Current { get; private set; }
        public Damageable Target { get; private set; }
        /// <summary>0 = rookie, 1 = ace (see <see cref="AISettings"/>).</summary>
        public float Skill { get; set; }
        public bool IsWingman { get; set; }
        /// <summary>Slot index among wingmen (left/right alternate).</summary>
        public int WingSlot { get; set; }

        ShipFlight _flight;
        ShipWeapons _weapons;
        Damageable _self;
        System.Random _rng;
        float _nextThink, _stateUntil, _burstUntil, _pauseUntil, _reactUntil, _repickAt, _lastHit = -99f;
        Vector3 _desired = Vector3.forward, _aimError, _breakDir, _avoidDir;
        float _avoidUntil;
        bool _fire, _lock, _evasive, _missileLocking;
        float _throttle = 0.5f;

        void Awake()
        {
            _flight = GetComponent<ShipFlight>();
            _weapons = GetComponent<ShipWeapons>();
            _self = GetComponent<Damageable>();
            _rng = new System.Random(GetInstanceID());
            if (_self != null) _self.Damaged += OnDamaged;
        }

        /// <summary>Fresh start for a pooled ship.</summary>
        public void ResetPilot(float skill, bool wingman, int slot)
        {
            Skill = Mathf.Clamp01(skill);
            IsWingman = wingman;
            WingSlot = slot;
            Current = State.Escort;
            Target = null;
            _nextThink = Time.time + (float)_rng.NextDouble() * 0.1f;   // stagger decisions across ships
            _stateUntil = _burstUntil = _pauseUntil = _reactUntil = _repickAt = 0f;
            _lastHit = -99f;
            _fire = _lock = _evasive = _missileLocking = false;
            _throttle = 0.5f;
            _avoidUntil = 0f;
            NewAimError();
        }

        void OnDamaged(float amount, Vector3 point, Damageable source)
        {
            _lastHit = Time.time;
            if (source != null && source.Alive && source.Team != _self.Team && (Target == null || Current == State.Escort)) Target = source;
        }

        float Lerp(Vector2 range) => Mathf.Lerp(range.x, range.y, Skill);

        public void ReadControls(ref ShipControls c)
        {
            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + 1f / Mathf.Max(1f, GameConfig.Instance.ai.thinkRate);
                Think();
            }
            Steer(ref c);
            c.Throttle = _throttle;
            c.Fire = _fire;
            c.Lock = _lock;
            c.Evasive = _evasive;
            _evasive = false;   // a press lasts one frame
        }

        // --- decisions (10 Hz) -----------------------------------------------------------------

        void Think()
        {
            var cfg = GameConfig.Instance.ai;
            var pos = transform.position;

            if (Target != null && (!Target.Alive || !Target.isActiveAndEnabled)) Target = null;
            if (Target == null || (Current == State.Escort && Time.time >= _repickAt))
            {
                var picked = PickTarget();
                if (picked != null && picked != Target) _reactUntil = Time.time + Lerp(cfg.reaction);   // a moment to react before firing
                Target = picked;
            }

            // obstacles (asteroids, the capital ship's hull) straight ahead: veer off along the surface
            float look = 120f + (_flight != null ? _flight.Speed : 0f);
            if (Physics.SphereCast(pos, 8f, transform.forward, out var hit, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform)
                && (Target == null || !hit.collider.transform.IsChildOf(Target.transform)))
            {
                _avoidUntil = Time.time + 0.8f;
                _avoidDir = (Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized + hit.normal * 0.8f).normalized;
            }

            // threats first
            bool missile = MissileSystem.Instance != null && MissileSystem.Instance.IsChasing(_self);
            bool hitRecently = Time.time - _lastHit < 0.3f;
            if (Current != State.Evade && (missile || hitRecently) && Chance(Lerp(cfg.evadeChance) * (missile ? 1.5f : 0.6f)))
            {
                Enter(State.Evade, 1.6f);
                _breakDir = (transform.right * (Rand() < 0f ? -1f : 1f) + transform.up * Rand() * 0.6f).normalized;
                if (missile || Chance(Skill)) _evasive = true;
            }

            if (Current == State.Evade || Current == State.Extend)
            {
                if (Time.time < _stateUntil) { _fire = false; return; }
                Current = Target != null ? State.Attack : State.Escort;
            }

            if (Target != null)
            {
                Current = State.Attack;
                float dist = Vector3.Distance(pos, Target.transform.position);
                if (dist < cfg.breakOffDistance && Vector3.Dot(transform.forward, Target.transform.position - pos) > 0f)
                {
                    Enter(State.Extend, cfg.extendSeconds);
                    _breakDir = (transform.forward + transform.right * ((float)_rng.NextDouble() - 0.5f) + transform.up * 0.3f).normalized;
                    _fire = false;
                    return;
                }
                if ((float)_rng.NextDouble() < 0.15f) NewAimError();
            }
            else Current = State.Escort;
        }

        void Enter(State s, float seconds)
        {
            Current = s;
            _stateUntil = Time.time + seconds;
            _reactUntil = Time.time + Lerp(GameConfig.Instance.ai.reaction);
        }

        Damageable PickTarget()
        {
            if (_self == null) return null;
            Damageable best = null;
            float bestScore = float.MaxValue;
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d == _self || !d.Alive || !d.Targetable || d.Team == _self.Team || d.Team == Team.Neutral) continue;
                var to = d.transform.position - transform.position;
                // prefer close targets in front; enemies like the player a little more
                float score = to.magnitude * (1.5f - 0.5f * Vector3.Dot(transform.forward, to.normalized));
                if (d == PlayerShip.Instance?.Health) score *= 0.8f;
                score *= d.TargetPriority;
                if (score < bestScore) { bestScore = score; best = d; }
            }
            _repickAt = Time.time + 2f;   // re-pick every couple of seconds at most
            return best;
        }

        void NewAimError()
        {
            float err = Lerp(GameConfig.Instance.ai.aimError);
            _aimError = new Vector3(Rand() * err, Rand() * err, 0f);
        }

        float Rand() => (float)_rng.NextDouble() * 2f - 1f;
        bool Chance(float p) => _rng.NextDouble() < p;

        // --- steering (every frame) -----------------------------------------------------------

        void Steer(ref ShipControls c)
        {
            var cfg = GameConfig.Instance.ai;
            var pos = transform.position;
            float arena = GameConfig.Instance.arena.radius;
            _fire = false;

            switch (Current)
            {
                case State.Evade:
                case State.Extend:
                    _desired = _breakDir;
                    _throttle = 1f;
                    break;
                case State.Attack when Target != null:
                {
                    var lead = _weapons != null ? _weapons.Lead(Target, GameConfig.Instance.combat.laserSpeed) : Target.transform.position;
                    var to = lead - pos;
                    float dist = to.magnitude;
                    _desired = Quaternion.Euler(_aimError) * (to / Mathf.Max(dist, 0.01f));
                    float off = Vector3.Angle(transform.forward, to);
                    // hard turn: sweet spot; lined up and far: boost; otherwise cruise
                    _throttle = off > 35f ? 0.35f : dist > 700f ? 0.95f : 0.6f;
                    if (off < Lerp(cfg.fireCone) && dist < cfg.fireRange && Time.time >= _reactUntil) Burst(cfg);
                    // elites: hold lock while roughly lined up, release when locked
                    if (_weapons != null && _weapons.HasMissiles)
                    {
                        if (_weapons.Locked) { _lock = false; _missileLocking = false; }
                        else if (off < GameConfig.Instance.combat.lockCone * 0.8f && _weapons.MissileCooldownLeft <= 0f) { _lock = true; _missileLocking = true; }
                        else if (_missileLocking && off > GameConfig.Instance.combat.lockCone * 1.5f) { _lock = false; _missileLocking = false; }
                    }
                    break;
                }
                default:
                    Escort();
                    break;
            }

            if (Time.time < _avoidUntil) { _desired = _avoidDir; _fire = false; }

            // stay in the arena
            if (pos.magnitude > arena * 0.85f) _desired = Vector3.Lerp(_desired, -pos.normalized, 0.8f).normalized;

            var local = transform.InverseTransformDirection(_desired);
            float yawErr = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitchErr = Mathf.Atan2(local.y, Mathf.Max(0.01f, new Vector2(local.x, local.z).magnitude)) * Mathf.Rad2Deg;
            if (local.z < 0f && Mathf.Abs(local.x) < 0.2f && Mathf.Abs(local.y) < 0.2f) pitchErr = 90f;   // straight behind: pull up and over
            c.Yaw = Mathf.Clamp(yawErr / 25f, -1f, 1f);
            c.Pitch = Mathf.Clamp(pitchErr / 20f, -1f, 1f);
            // bank into the turn like an aircraft (the AI doesn't mind the roll)
            float bankRight = -_flight.BankAngle();
            if (float.IsNaN(bankRight)) bankRight = 0f;
            float wantBank = Mathf.Clamp(yawErr * 1.5f, -60f, 60f);
            c.Roll = Mathf.Clamp((wantBank - bankRight) / 30f, -1f, 1f);
        }

        void Burst(AISettings cfg)
        {
            if (Time.time < _pauseUntil) return;
            if (_burstUntil <= 0f || Time.time > _burstUntil + 0.05f) _burstUntil = Time.time + cfg.burstSeconds;
            if (Time.time <= _burstUntil && !(_weapons != null && _weapons.Heat > 0.85f)) _fire = true;
            else
            {
                _pauseUntil = Time.time + Lerp(cfg.burstPause);
                _burstUntil = 0f;
            }
        }

        void Escort()
        {
            var player = PlayerShip.Instance;
            var pos = transform.position;
            if (player == null || !player.Health.Alive) { _desired = transform.forward; _throttle = 0.5f; return; }
            var pt = player.transform;
            if (IsWingman)
            {
                var offset = GameConfig.Instance.ai.wingOffset;
                offset.x *= WingSlot % 2 == 0 ? 1f : -1f;
                offset.z -= (WingSlot / 2) * 25f;
                var slot = pt.TransformPoint(offset) + pt.forward * 60f;   // aim a little ahead of the slot
                var to = slot - pos;
                _desired = to.sqrMagnitude > 1f ? to.normalized : pt.forward;
                float behind = Vector3.Dot(pt.TransformPoint(offset) - pos, pt.forward);
                _throttle = Mathf.Clamp01(0.5f + behind * 0.01f);
            }
            else
            {
                var to = pt.position - pos;
                _desired = to.normalized;
                _throttle = to.magnitude > 800f ? 0.95f : 0.6f;
            }
        }
    }
}
