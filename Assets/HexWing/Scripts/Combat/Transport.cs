using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// An allied cargo transport "Barka" (original design): a long hexagonal spine carrying four container
    /// blocks, a cab at the front and two engines. It follows the convoy route at a steady speed and is
    /// what the enemy wants to destroy (high AI target priority). Destroyed, it blows up and is gone.
    /// </summary>
    public class Transport : MonoBehaviour
    {
        static Mesh _hull, _glow;

        public Damageable Health { get; private set; }
        public float Distance { get; private set; }
        public bool Arrived { get; private set; }

        public event System.Action<Transport> Lost;

        Route _route;
        float _speed;

        public static Transport Create(Transform parent, int index)
        {
            EnsureMeshes();
            var go = new GameObject($"Transport {index + 1}");
            go.transform.SetParent(parent, false);
            MeshKit.Part("Hull", go.transform, _hull, Mats.Get(Mats.AllyHull));
            MeshKit.Part("Glow", go.transform, _glow, Mats.Get(Mats.Accent));
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(16f, 16f, 70f);
            var t = go.AddComponent<Transport>();
            t.Health = go.AddComponent<Damageable>();
            t.Health.Team = Team.Player;
            t.Health.DisplayName = "TRANSPORT";
            t.Health.TargetPriority = 0.6f;   // enemies go for the convoy
            t.Health.Destroyed += by => t.OnDestroyed();
            return t;
        }

        public void Begin(Route route, float startDistance, float speed, float hull, float shields)
        {
            _route = route;
            _speed = speed;
            Distance = startDistance;
            Arrived = false;
            gameObject.SetActive(true);
            Health.enabled = true;
            Health.MaxHull = hull;
            Health.MaxShields = shields;
            Health.Restore();
            Place();
        }

        void Update()
        {
            if (_route == null || !Health.Alive || Arrived) return;
            Distance += _speed * Time.deltaTime;
            if (Distance >= _route.Length) { Distance = _route.Length; Arrived = true; }
            Place();
        }

        void Place()
        {
            var dir = _route.DirectionAt(Distance);
            transform.SetPositionAndRotation(_route.PointAt(Distance), Quaternion.LookRotation(dir, Vector3.up));
            Health.Velocity = Arrived ? Vector3.zero : dir * _speed;
        }

        void OnDestroyed()
        {
            Effects.Instance?.Explode(transform.position, Health.Velocity, 12f, 40);
            ShipAudio.Instance?.PlayExplosion(transform.position, 1f);
            Lost?.Invoke(this);
            gameObject.SetActive(false);
        }

        static void EnsureMeshes()
        {
            if (_hull != null) return;
            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -34f), new Vector3(0f, 0f, 30f), 4f, 4f, 6, 0f, null, true);   // spine
            for (int i = 0; i < 4; i++)
            {
                float z = -24f + i * 13f;
                b.Box(new Vector3(0f, 5.5f, z), new Vector3(12f, 7f, 11f), Quaternion.identity);
                b.Box(new Vector3(0f, -5.5f, z), new Vector3(12f, 7f, 11f), Quaternion.identity);
            }
            b.Box(new Vector3(0f, 1.5f, 33f), new Vector3(8f, 7f, 8f), Quaternion.identity, 0.6f);   // cab
            b.Box(new Vector3(-6f, 0f, -36f), new Vector3(4f, 4f, 6f), Quaternion.identity);
            b.Box(new Vector3(6f, 0f, -36f), new Vector3(4f, 4f, 6f), Quaternion.identity);
            _hull = b.ToMesh("Transport Hull", p => { float v = Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(-9f, 9f, p.y)); return new Color(v, v, v, 1f); });
            var g = new MeshKit.Builder();
            g.Ico(new Vector3(-6f, 0f, -39.2f), new Vector3(1.6f, 1.6f, 0.4f));
            g.Ico(new Vector3(6f, 0f, -39.2f), new Vector3(1.6f, 1.6f, 0.4f));
            g.Box(new Vector3(0f, 3.6f, 37.1f), new Vector3(5f, 1.2f, 0.3f), Quaternion.identity);   // cab window strip
            _glow = g.ToMesh("Transport Glow");
        }
    }
}
