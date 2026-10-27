using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Homing missiles and bombs (pool of 24): fly at a fixed speed, turn toward the target's lead point
    /// at a limited rate, explode within the fuse distance or when they hit something, and fizzle after
    /// their lifetime. A target that does an evasive maneuver breaks the lock of missiles already chasing
    /// it. Bombs have no target and a blast radius: they go off at the end of their fuse (or on contact)
    /// and damage every enemy nearby. Drawn instanced, with a small flash trail.
    /// </summary>
    public class MissileSystem : MonoBehaviour
    {
        public static MissileSystem Instance { get; private set; }

        public const int MaxMissiles = 24;

        struct Missile
        {
            public Vector3 Position, Direction;
            public float Life, TrailTimer, Damage, TurnScale, Speed, Blast;
            public Damageable Target, Owner;
            public Team Team;
        }

        readonly Missile[] _missiles = new Missile[MaxMissiles];
        readonly RaycastHit[] _hits = new RaycastHit[4];
        int _count;
        InstancedBatch _batch;

        public int Count => _count;
        public event System.Action<Damageable, bool, Damageable> Hit;

        void Awake()
        {
            Instance = this;
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -1.1f), new Vector3(0f, 0f, 0.9f), 0.22f, 0.22f, 6);
            b.Prism(new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 1.4f), 0.22f, 0.02f, 6, 0f, null, false);
            for (int i = 0; i < 4; i++)
                b.Box(Quaternion.Euler(0f, 0f, i * 90f + 45f) * new Vector3(0f, 0.32f, -0.9f), new Vector3(0.04f, 0.25f, 0.4f),
                      Quaternion.Euler(0f, 0f, i * 90f + 45f));
            _batch = new InstancedBatch(b.ToMesh("Missile"), Mats.Get(Mats.Missile), MaxMissiles);
        }

        /// <param name="damage">Negative = the config's missile damage.</param>
        /// <param name="speed">Negative = the config's missile speed.</param>
        /// <param name="lifetime">Negative = the config's missile lifetime.</param>
        /// <param name="blastRadius">Above 0: a bomb that damages everything of the other team in this radius.</param>
        public bool Launch(Vector3 position, Vector3 direction, Damageable target, Damageable owner, Team team,
                           float damage = -1f, float turnScale = 1f, float speed = -1f, float lifetime = -1f, float blastRadius = 0f)
        {
            if (_count >= MaxMissiles) return false;
            var cfg = GameConfig.Instance.combat;
            _missiles[_count++] = new Missile
            {
                Position = position,
                Direction = direction.normalized,
                Life = lifetime > 0f ? lifetime : cfg.missileLifetime,
                Target = target,
                Owner = owner,
                Team = team,
                Damage = damage >= 0f ? damage : cfg.missileDamage,
                TurnScale = turnScale,
                Speed = speed > 0f ? speed : cfg.missileSpeed,
                Blast = blastRadius,
            };
            return true;
        }

        /// <summary>Missiles chasing <paramref name="target"/> lose it and fly straight on.</summary>
        public void BreakLocks(Damageable target)
        {
            for (int i = 0; i < _count; i++)
                if (_missiles[i].Target == target) _missiles[i].Target = null;
        }

        public bool IsChasing(Damageable target)
        {
            for (int i = 0; i < _count; i++) if (_missiles[i].Target == target) return true;
            return false;
        }

        public void ClearAll() => _count = 0;

        void Update()
        {
            var cfg = GameConfig.Instance.combat;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < _count; i++)
            {
                ref var m = ref _missiles[i];
                m.Life -= dt;
                bool explode = false;
                Damageable victim = null;

                if (m.Target != null && (!m.Target.Alive || !m.Target.isActiveAndEnabled)) m.Target = null;
                if (m.Target != null)
                {
                    var to = m.Target.transform.position - m.Position;
                    float dist = to.magnitude;
                    if (dist <= cfg.missileFuse) { explode = true; victim = m.Target; }
                    else
                    {
                        float t = dist / m.Speed;
                        var aim = (to + m.Target.Velocity * t * 0.7f).normalized;
                        m.Direction = Vector3.RotateTowards(m.Direction, aim, cfg.missileTurnRate * m.TurnScale * Mathf.Deg2Rad * dt, 0f);
                    }
                }

                var step = m.Direction * (m.Speed * dt);
                if (!explode)
                {
                    int n = Physics.RaycastNonAlloc(m.Position, m.Direction, _hits, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < n; h++)
                    {
                        var d = Damageable.Of(_hits[h].collider);
                        if (d != null && d == m.Owner) continue;
                        explode = true;
                        victim = d;
                        m.Position = _hits[h].point;
                        break;
                    }
                }

                if (explode || m.Life <= 0f)
                {
                    bool bomb = m.Blast > 0f;
                    bool big = explode || bomb;
                    Effects.Instance?.Explode(m.Position, Vector3.zero, bomb ? m.Blast * 0.25f : big ? 4f : 2f, big ? 10 : 4);
                    ShipAudio.Instance?.PlayExplosion(m.Position, big ? 0.8f : 0.4f);
                    if (bomb) Blast(m);
                    else if (victim != null && victim.Team != m.Team)
                    {
                        victim.Damage(m.Damage, m.Position, m.Owner);
                        Hit?.Invoke(victim, !victim.Alive, m.Owner);
                    }
                    _missiles[i] = _missiles[--_count];
                    i--;
                    continue;
                }

                m.Position += step;
                m.TrailTimer -= dt;
                if (m.TrailTimer <= 0f)
                {
                    m.TrailTimer = 0.05f;
                    Effects.Instance?.SpawnFlash(m.Position - m.Direction * 1.4f, 0.9f, 0.35f, Effects.FlashKind.Impact);
                }
            }
        }

        /// <summary>Damage to everything of the other team within the blast radius, less toward the edge.</summary>
        void Blast(in Missile m)
        {
            var all = Damageable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var d = all[i];
                if (!d.Alive || d.Team == m.Team) continue;
                float dist = Vector3.Distance(d.transform.position, m.Position);
                if (dist > m.Blast) continue;
                d.Damage(m.Damage * Mathf.Lerp(1f, 0.4f, dist / m.Blast), m.Position, m.Owner);
                Hit?.Invoke(d, !d.Alive, m.Owner);
            }
        }

        void LateUpdate()
        {
            _batch.Clear();
            for (int i = 0; i < _count; i++)
                _batch.Add(Matrix4x4.TRS(_missiles[i].Position, Quaternion.LookRotation(_missiles[i].Direction), Vector3.one));
            _batch.Draw();
        }
    }
}
