using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// A target drone (original design): a faceted hexagonal core with three swept fins and a glowing
    /// band. It flies a tilted loop at a steady speed, so hitting it needs the lead marker. Destroyed, it
    /// bursts into an explosion and debris and is hidden until <see cref="TargetDrone.Respawn"/>.
    /// About 170 triangles in 2 draw calls.
    /// </summary>
    public class TargetDrone : MonoBehaviour
    {
        static Mesh _body, _eye;

        public Damageable Health { get; private set; }

        Vector3 _center, _u, _w;
        float _radius, _speed, _angle;
        GameObject _visual;
        SphereCollider _collider;

        public static TargetDrone Create(Transform parent, int index)
        {
            EnsureMeshes();
            var cfg = GameConfig.Instance.drones;
            var go = new GameObject($"Drone {index + 1}");
            go.transform.SetParent(parent, false);
            var drone = go.AddComponent<TargetDrone>();
            drone._visual = new GameObject("Visual");
            drone._visual.transform.SetParent(go.transform, false);
            var art = ArtLibrary.Get("Drone");
            if (art != null) ArtLibrary.Attach(art, drone._visual.transform, null);
            else
            {
                MeshKit.Part("Body", drone._visual.transform, _body, Mats.Get(Mats.Drone));
                MeshKit.Part("Eye", drone._visual.transform, _eye, Mats.Get(Mats.DroneCore));
            }
            drone._collider = go.AddComponent<SphereCollider>();
            drone._collider.radius = cfg.radius;
            var health = go.AddComponent<Damageable>();
            health.Team = Team.Enemy;
            health.MaxHull = cfg.hull;
            health.MaxShields = cfg.shields;
            health.DisplayName = "DRONE";
            health.Restore();
            health.Destroyed += drone.OnDestroyed;
            drone.Health = health;
            return drone;
        }

        /// <summary>Starts a new loop around <paramref name="center"/> and makes the drone whole and visible.</summary>
        public void Respawn(Vector3 center, System.Random rng)
        {
            var cfg = GameConfig.Instance.drones;
            float radius = Mathf.Lerp(cfg.loopRadius.x, cfg.loopRadius.y, (float)rng.NextDouble());
            float speed = Mathf.Lerp(cfg.speed.x, cfg.speed.y, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            var tilt = Quaternion.Euler((float)rng.NextDouble() * 50f - 25f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 50f - 25f);
            Respawn(center, radius, speed, tilt, (float)rng.NextDouble() * Mathf.PI * 2f);
        }

        /// <summary>A loop of <paramref name="radius"/> at <paramref name="speed"/> (negative = the other way) in the plane given by <paramref name="tilt"/>.</summary>
        public void Respawn(Vector3 center, float radius, float speed, Quaternion tilt, float angle)
        {
            _center = center;
            _radius = Mathf.Max(1f, radius);
            _speed = speed;
            _angle = angle;
            _u = tilt * Vector3.right;
            _w = tilt * Vector3.forward;
            Health.Restore();
            _visual.SetActive(true);
            _collider.enabled = true;
            Health.enabled = true;
            Place(0f);
        }

        public void Hide()
        {
            _visual.SetActive(false);
            _collider.enabled = false;
            Health.enabled = false;
        }

        void Update()
        {
            if (!Health.Alive) return;
            Place(Time.deltaTime);
        }

        void Place(float dt)
        {
            _angle += _speed / _radius * dt;
            var pos = _center + (_u * Mathf.Cos(_angle) + _w * Mathf.Sin(_angle)) * _radius;
            var tangent = (-_u * Mathf.Sin(_angle) + _w * Mathf.Cos(_angle)) * Mathf.Sign(_speed);
            Health.Velocity = tangent * Mathf.Abs(_speed);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent, Vector3.Cross(_w, _u)));
        }

        void OnDestroyed(Damageable by)
        {
            Effects.Instance?.Explode(transform.position, Health.Velocity, 4f, 18);
            ShipAudio.Instance?.PlayExplosion(transform.position, 1f);
            Hide();   // out of the target list until it respawns
        }

        static void EnsureMeshes()
        {
            if (_body != null) return;
            var b = new MeshKit.Builder();
            // hexagonal core: a six-sided prism, pointed at both ends
            b.Prism(new Vector3(0f, 0f, -1.2f), new Vector3(0f, 0f, 1.2f), 1.6f, 1.6f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, 1.2f), new Vector3(0f, 0f, 2.6f), 1.6f, 0.15f, 6, 0f, null, false);
            b.Prism(new Vector3(0f, 0f, -1.2f), new Vector3(0f, 0f, -2.2f), 1.6f, 0.4f, 6, 0f, null, false);
            // three fins, swept back
            for (int i = 0; i < 3; i++)
            {
                var rot = Quaternion.Euler(0f, 0f, i * 120f + 90f);
                b.Box(rot * new Vector3(0f, 2.6f, -0.8f), new Vector3(0.18f, 2.4f, 1.8f), rot * Quaternion.Euler(-25f, 0f, 0f), 0.45f);
            }
            _body = b.ToMesh("Drone Body");
            var e = new MeshKit.Builder();
            e.Ico(new Vector3(0f, 0f, 1.2f), new Vector3(1.75f, 1.75f, 0.3f));   // a glowing band where the nose meets the core
            _eye = e.ToMesh("Drone Eye");
        }
    }
}
