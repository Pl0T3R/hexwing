using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Arcade flight, as in Starfighter Assault: the ship always flies forward and can't stall (minimum
    /// speed above 0); stick input sets turn rates that ease in and out; the flight path follows the nose
    /// with a short lag, which gives a slight drift in hard turns. Throttle changes how well the ship turns
    /// (bands, blended so nothing jumps): the sweet spot turns tightest, boost turns widest.
    ///
    /// The ship is the parent of the camera rig, with the eye point at the rig's origin, so the ship turns
    /// around the pilot's eyes (no sideways swing). No Rigidbody: the ship moves in Update and sweeps a
    /// sphere for collisions, sliding along what it hits.
    ///
    /// The same component flies AI ships (M4) with another <see cref="IShipPilot"/>.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class ShipFlight : MonoBehaviour
    {
        public enum Band { Slow, SweetSpot, Cruise, Boost }

        public IShipPilot Pilot { get; set; }
        /// <summary>Flight numbers for this ship (AI classes, later ship definitions); null = GameConfig.flight.</summary>
        public FlightSettings SettingsOverride { get; set; }
        /// <summary>Comfort options (turn limits, auto-level) are for the player's own view only; AI ships turn this off.</summary>
        public bool ApplyComfort { get; set; } = true;
        public FlightSettings Settings => SettingsOverride ?? GameConfig.Instance.flight;
        public ShipControls Controls => _controls;

        public float Speed { get; private set; }
        public Vector3 Velocity => _direction * Speed;
        /// <summary>Current turn rates (deg/s): x = pitch (+ up), y = yaw (+ right), z = roll (+ right).</summary>
        public Vector3 AngularRate => _rates;
        /// <summary>How fast the view is turning (deg/s), roll weighted a little less. Drives the vignette.</summary>
        public float TurnSpeed => Mathf.Sqrt(_rates.x * _rates.x + _rates.y * _rates.y + 0.5f * _rates.z * _rates.z)
                                  + (Evading && GameConfig.Instance.combat.evasiveStyle == EvasiveStyle.Roll ? 400f : 0f);
        public float TurnMultiplier { get; private set; } = 1f;
        public Band CurrentBand { get; private set; }
        public bool AutoLeveling { get; private set; }
        public bool Frozen { get; set; }
        public bool Evading => _evadeTime < _evadeDuration;
        public float EvasiveCooldownLeft => Mathf.Max(0f, _evadeReadyAt - Time.time);

        /// <summary>Raised when an evasive maneuver starts (sign: -1 left, +1 right).</summary>
        public event System.Action<float> EvasiveStarted;

        /// <summary>Raised on a collision: hit point and how hard (0..1).</summary>
        public event System.Action<Vector3, float> Collided;

        ShipControls _controls;
        Vector3 _rates;
        Vector3 _direction = Vector3.forward;
        float _sinceRoll;
        float _evadeTime = 1f, _evadeDuration = 0f, _evadeSign = 1f, _evadeReadyAt, _breakLockUntil;
        bool _evasiveHeld;
        Damageable _self;
        readonly RaycastHit[] _hits = new RaycastHit[4];

        void Awake()
        {
            if (Pilot == null) Pilot = GetComponent<IShipPilot>();
            _self = GetComponent<Damageable>();
            _direction = transform.forward;
            Speed = TargetSpeed(GameConfig.Instance.controls.cruiseThrottle);
        }

        /// <summary>Puts the ship at a pose, flying straight at cruise speed.</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _direction = transform.forward;
            _rates = Vector3.zero;
            _sinceRoll = 0f;
            _evadeTime = _evadeDuration = 0f;
            _evadeReadyAt = 0f;
            Speed = TargetSpeed(GameConfig.Instance.controls.cruiseThrottle);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Frozen) return;
            var f = Settings;
            var comfort = GameConfig.Instance.comfort;

            _controls = default;
            Pilot?.ReadControls(ref _controls);
            float throttle = Mathf.Clamp01(_controls.Throttle);

            TurnMultiplier = BandMultiplier(throttle, f);
            CurrentBand = BandOf(throttle, f);

            float pitchRate = f.pitchRate, yawRate = f.yawRate, rollRate = f.rollRate;
            if (ApplyComfort && comfort.turnRateLimit > 0f)
            {
                pitchRate = Mathf.Min(pitchRate, comfort.turnRateLimit);
                yawRate = Mathf.Min(yawRate, comfort.turnRateLimit);
            }
            if (ApplyComfort && comfort.rollRateLimit > 0f) rollRate = Mathf.Min(rollRate, comfort.rollRateLimit);

            var target = new Vector3(
                _controls.Pitch * pitchRate * TurnMultiplier,
                _controls.Yaw * yawRate * TurnMultiplier,
                _controls.Roll * rollRate);

            // auto-level: with no roll input for a moment, roll gently back to the arena's horizon
            _sinceRoll = _controls.Rolling ? 0f : _sinceRoll + dt;
            AutoLeveling = false;
            if (ApplyComfort && comfort.autoLevel && _sinceRoll >= comfort.autoLevelDelay)
            {
                float bank = BankAngle();
                if (!float.IsNaN(bank) && Mathf.Abs(bank) > 0.5f)
                {
                    target.z = Mathf.Clamp(bank * 2f, -comfort.autoLevelRate, comfort.autoLevelRate);
                    AutoLeveling = true;
                }
            }

            _rates = Vector3.Lerp(_rates, target, 1f - Mathf.Exp(-f.angularResponse * dt));
            // Unity: +x pitches the nose down and +z rolls left, so both are negated
            transform.rotation *= Quaternion.Euler(-_rates.x * dt, _rates.y * dt, -_rates.z * dt);

            Vector3 evadeStep = Evasive(dt);

            float targetSpeed = TargetSpeed(throttle);
            Speed = Mathf.MoveTowards(Speed, targetSpeed, (targetSpeed > Speed ? f.acceleration : f.deceleration) * dt);
            Speed = Mathf.Max(Speed, f.minSpeed * 0.5f);   // never stalls, even right after a collision

            float follow = f.noseLag > 0f ? 1f - Mathf.Exp(-dt / f.noseLag) : 1f;
            _direction = Vector3.Slerp(_direction, transform.forward, follow).normalized;
            Move(_direction * (Speed * dt) + evadeStep, f);
            if (_self != null) _self.Velocity = Velocity;
        }

        /// <summary>
        /// X: a quick sideways hop (jink, the view never rolls) or a full barrel roll, toward the side the
        /// stick points (right if centred). Breaks missile locks on this ship for a moment.
        /// </summary>
        Vector3 Evasive(float dt)
        {
            var cfg = GameConfig.Instance.combat;
            bool pressed = _controls.Evasive && !_evasiveHeld;
            _evasiveHeld = _controls.Evasive;
            if (pressed && !Evading && Time.time >= _evadeReadyAt)
            {
                float side = Mathf.Abs(_controls.Yaw) > 0.1f ? _controls.Yaw : _controls.Roll;
                _evadeSign = side < -0.1f ? -1f : 1f;
                _evadeDuration = Mathf.Max(0.1f, cfg.evasiveSeconds);
                _evadeTime = 0f;
                _evadeReadyAt = Time.time + cfg.evasiveCooldown;
                _breakLockUntil = Time.time + cfg.evasiveBreaksLock;
                EvasiveStarted?.Invoke(_evadeSign);
            }
            if (Time.time < _breakLockUntil && _self != null) MissileSystem.Instance?.BreakLocks(_self);
            if (!Evading) return Vector3.zero;

            float before = Mathf.SmoothStep(0f, 1f, _evadeTime / _evadeDuration);
            _evadeTime = Mathf.Min(_evadeDuration, _evadeTime + dt);
            float after = Mathf.SmoothStep(0f, 1f, _evadeTime / _evadeDuration);
            if (cfg.evasiveStyle == EvasiveStyle.Roll)
                transform.rotation *= Quaternion.Euler(0f, 0f, -_evadeSign * 360f * (after - before));
            return transform.right * (_evadeSign * cfg.jinkDistance * (after - before));
        }

        void Move(Vector3 step, FlightSettings f)
        {
            float distance = step.magnitude;
            if (distance < 1e-5f) return;
            var dir = step / distance;
            int n = Physics.SphereCastNonAlloc(transform.position, f.collisionRadius, dir, _hits, distance,
                                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;
                if (_hits[i].distance <= 0f && _hits[i].point == Vector3.zero) continue;   // started inside: ignore
                if (nearest < 0 || _hits[i].distance < _hits[nearest].distance) nearest = i;
            }
            if (nearest < 0)
            {
                transform.position += step;
                return;
            }

            var hit = _hits[nearest];
            transform.position += dir * Mathf.Max(0f, hit.distance - 0.05f);
            float severity = Mathf.Clamp01(-Vector3.Dot(dir, hit.normal));
            // slide along the surface, keep part of the speed, nudge away from it
            var slide = Vector3.ProjectOnPlane(_direction, hit.normal);
            _direction = slide.sqrMagnitude > 1e-4f ? slide.normalized : hit.normal;
            Speed = Mathf.Max(f.minSpeed * 0.5f, Speed * Mathf.Lerp(1f, f.collisionSpeedKeep, severity));
            transform.position += hit.normal * 0.1f;
            Collided?.Invoke(hit.point, severity);
        }

        /// <summary>
        /// Roll (deg, in this class's +right convention) that brings the wings level with the arena's
        /// horizon: negative when banked right. NaN when flying almost straight up or down.
        /// </summary>
        public float BankAngle()
        {
            var fwd = transform.forward;
            if (Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.94f) return float.NaN;
            var levelRight = Vector3.Cross(Vector3.up, fwd).normalized;
            // banked right, the right wing dips below levelRight and this angle is negative
            return Vector3.SignedAngle(levelRight, transform.right, fwd);
        }

        float TargetSpeed(float throttle)
        {
            var f = Settings;
            return Mathf.Lerp(f.minSpeed, f.maxSpeed, throttle);
        }

        /// <summary>Turn rate multiplier for a throttle setting: 1.0 cruise, more in the sweet spot, less when boosting.</summary>
        public static float BandMultiplier(float t, FlightSettings f)
        {
            float b = Mathf.Max(0.001f, f.bandBlend);
            float sweet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(f.sweetSpot.x - b, f.sweetSpot.x + b, t))
                          * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(f.sweetSpot.y - b, f.sweetSpot.y + b, t)));
            float boost = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(f.boostFrom - b, f.boostFrom + b, t));
            return 1f + (f.sweetSpotMultiplier - 1f) * sweet + (f.boostMultiplier - 1f) * boost;
        }

        public static Band BandOf(float t, FlightSettings f)
        {
            if (t >= f.boostFrom) return Band.Boost;
            if (t >= f.sweetSpot.x && t <= f.sweetSpot.y) return Band.SweetSpot;
            return t < f.sweetSpot.x ? Band.Slow : Band.Cruise;
        }
    }
}
