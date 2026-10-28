using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The bomber's turret drone: a small gun platform left where it was deployed. For a while it turns
    /// to the nearest enemy in range and fires at its lead point (with some aim error), then it
    /// self-destructs. It can be shot down. One at a time per owner; deploying again replaces it.
    /// </summary>
    public class TurretDrone : MonoBehaviour
    {
        static Mesh _mesh;

        Damageable _health, _owner;
        float _until, _nextShot, _nextPick;
        Damageable _target;
        System.Random _rng = new(23);

        public static TurretDrone Deploy(Vector3 position, Quaternion rotation, Damageable owner)
        {
            var cfg = GameConfig.Instance.abilities;
            if (_mesh == null)
            {
                var b = new MeshKit.Builder();
                b.Octahedron(Vector3.zero, 1.6f, 2.2f);
                b.Prism(new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 3.2f), 0.25f, 0.18f, 6);   // barrel
                for (int i = 0; i < 3; i++)
                {
                    var around = Quaternion.Euler(0f, 0f, i * 120f);
                    b.Box(around * new Vector3(0f, 1.8f, -0.4f), new Vector3(0.15f, 1.6f, 1.2f), around, 0.4f);
                }
                _mesh = b.ToMesh("Turret Drone");
            }
            var go = new GameObject("Turret Drone");
            go.transform.SetPositionAndRotation(position, rotation);
            MeshKit.Part("Mesh", go.transform, _mesh, Mats.Get(Mats.AllyHull));
            go.AddComponent<SphereCollider>().radius = 2.2f;
            var turret = go.AddComponent<TurretDrone>();
            turret._owner = owner;
            turret._health = go.AddComponent<Damageable>();
            turret._health.Team = owner != null ? owner.Team : Team.Player;
            turret._health.MaxHull = 60f;
            turret._health.DisplayName = "TURRET DRONE";
            turret._health.Restore();
            turret._health.Destroyed += _ =>
            {
                Effects.Instance?.Explode(go.transform.position, Vector3.zero, 3f, 10);
                ShipAudio.Instance?.PlayExplosion(go.transform.position, 0.6f);
                Destroy(go);
            };
            turret._until = Time.time + cfg.turretSeconds;
            return turret;
        }

        public void Remove() => Destroy(gameObject);

        void Update()
        {
            var cfg = GameConfig.Instance.abilities;
            if (Time.time >= _until)
            {
                Effects.Instance?.Explode(transform.position, Vector3.zero, 2f, 6);
                Destroy(gameObject);
                return;
            }
            if (Time.time >= _nextPick || _target == null || !_target.Alive)
            {
                _nextPick = Time.time + 0.5f;
                _target = Nearest(cfg.turretRange);
            }
            if (_target == null) return;

            var lead = _target.transform.position + _target.Velocity * (Vector3.Distance(transform.position, _target.transform.position) / GameConfig.Instance.combat.laserSpeed);
            var dir = (lead - transform.position).normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180f * Time.deltaTime);
            if (Time.time < _nextShot || Vector3.Angle(transform.forward, dir) > 6f) return;
            _nextShot = Time.time + 1f / cfg.turretFireRate;
            float err = cfg.turretAimError;
            var shot = Quaternion.Euler(Rand() * err, Rand() * err, 0f) * transform.forward;
            LaserSystem.Instance?.Fire(transform.position + transform.forward * 3.4f, shot, Vector3.zero, _owner != null ? _owner : _health, _health.Team, cfg.turretDamage);
            Effects.Instance?.SpawnFlash(transform.position + transform.forward * 3.6f, 0.8f, 0.06f, Effects.FlashKind.Muzzle);
        }

        float Rand() => (float)_rng.NextDouble() * 2f - 1f;

        Damageable Nearest(float range)
        {
            Damageable best = null;
            float bestSq = range * range;
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (!d.Alive || d.Team == _health.Team || d.Team == Team.Neutral || !d.Targetable) continue;
                float sq = (d.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }
    }
}
