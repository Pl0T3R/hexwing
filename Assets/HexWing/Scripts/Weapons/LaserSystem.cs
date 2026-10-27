using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Every laser bolt in the game: plain structs in a fixed pool (384), moved each frame and swept with
    /// a raycast over the distance flown (800 m/s is about 9 m per frame, so nothing is skipped). A hit
    /// damages the <see cref="Damageable"/> and leaves an impact flash. Bolts are drawn instanced, one
    /// draw call per team colour. Bolts never hit their own shooter.
    /// </summary>
    public class LaserSystem : MonoBehaviour
    {
        public static LaserSystem Instance { get; private set; }

        public const int MaxBolts = 384;

        struct Bolt
        {
            public Vector3 Position, Velocity;
            public float Life, Damage;
            public Damageable Owner;
            public Team Team;
        }

        readonly Bolt[] _bolts = new Bolt[MaxBolts];
        readonly RaycastHit[] _hits = new RaycastHit[8];
        int _count;
        InstancedBatch _player, _enemy;

        /// <summary>Raised when a bolt hits: target hit, point, whether it destroyed it. For hit markers.</summary>
        public event System.Action<Damageable, Vector3, bool, Damageable> Hit;

        public int Count => _count;

        void Awake()
        {
            Instance = this;
            var b = new MeshKit.Builder();
            b.Octahedron(Vector3.zero, 0.5f, 1f);   // stretched along z into a slim glowing dart
            var mesh = b.ToMesh("Laser Bolt");
            _player = new InstancedBatch(mesh, Mats.Get(Mats.LaserPlayer), MaxBolts);
            _enemy = new InstancedBatch(mesh, Mats.Get(Mats.LaserEnemy), MaxBolts);
        }

        public void Fire(Vector3 position, Vector3 direction, Vector3 inheritVelocity, Damageable owner, Team team, float damage)
        {
            var cfg = GameConfig.Instance.combat;
            if (_count >= MaxBolts) return;   // budget: drop the shot rather than grow
            _bolts[_count++] = new Bolt
            {
                Position = position,
                Velocity = inheritVelocity + direction * cfg.laserSpeed,
                Life = cfg.laserRange / cfg.laserSpeed,
                Damage = damage,
                Owner = owner,
                Team = team,
            };
        }

        public void ClearAll() => _count = 0;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var player = PlayerShip.Instance != null && PlayerShip.Instance.Health.Alive ? PlayerShip.Instance.transform.position : (Vector3?)null;
            for (int i = 0; i < _count; i++)
            {
                ref var bolt = ref _bolts[i];
                bolt.Life -= dt;
                var step = bolt.Velocity * dt;
                float dist = step.magnitude;
                bool remove = bolt.Life <= 0f;
                if (!remove && dist > 0f && Sweep(bolt.Position, step / dist, dist, bolt.Owner, out var hit, out var target))
                {
                    remove = true;
                    if (target != null && target.Team != bolt.Team)
                    {
                        bool shielded = target.Shields > 0f;
                        target.Damage(bolt.Damage, hit.point, bolt.Owner);
                        Effects.Instance?.SpawnFlash(hit.point, shielded ? 3f : 1.6f, 0.18f, shielded ? Effects.FlashKind.Shield : Effects.FlashKind.Impact);
                        Hit?.Invoke(target, hit.point, !target.Alive, bolt.Owner);
                    }
                    else Effects.Instance?.SpawnFlash(hit.point, 1.2f, 0.15f, Effects.FlashKind.Impact);
                }
                if (remove)
                {
                    _bolts[i] = _bolts[--_count];
                    i--;
                    continue;
                }
                if (player.HasValue && bolt.Team != Team.Player) Flyby(bolt.Position, step, player.Value);
                bolt.Position += step;
            }
        }

        /// <summary>An enemy bolt passing within 14 m of the player this frame (but not hitting): a hiss.</summary>
        static void Flyby(Vector3 from, Vector3 step, Vector3 player)
        {
            float t = Mathf.Clamp01(Vector3.Dot(player - from, step) / Mathf.Max(step.sqrMagnitude, 1e-4f));
            if (t <= 0f || t >= 1f) return;   // the closest point must be inside this frame's step
            var closest = from + step * t;
            if ((closest - player).sqrMagnitude < 14f * 14f) ShipAudio.Instance?.PlayFlyby(closest);
        }

        bool Sweep(Vector3 from, Vector3 dir, float dist, Damageable owner, out RaycastHit best, out Damageable target)
        {
            best = default;
            target = null;
            int n = Physics.RaycastNonAlloc(from, dir, _hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var d = Damageable.Of(_hits[i].collider);
                if (d != null && d == owner) continue;
                if (d != null && !d.Alive) continue;
                if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    best = _hits[i];
                    target = d;
                    found = true;
                }
            }
            return found;
        }

        void LateUpdate()
        {
            var cfg = GameConfig.Instance.combat;
            _player.Clear();
            _enemy.Clear();
            var scale = new Vector3(cfg.boltThickness, cfg.boltThickness, cfg.boltLength);
            for (int i = 0; i < _count; i++)
            {
                ref var bolt = ref _bolts[i];
                var dir = bolt.Velocity.normalized;
                // the bolt's head is at its position; the body trails behind it
                var m = Matrix4x4.TRS(bolt.Position - dir * (cfg.boltLength * 0.5f), Quaternion.LookRotation(dir), scale);
                if (bolt.Team == Team.Player) _player.Add(m); else _enemy.Add(m);
            }
            _player.Draw();
            _enemy.Draw();
        }
    }
}
