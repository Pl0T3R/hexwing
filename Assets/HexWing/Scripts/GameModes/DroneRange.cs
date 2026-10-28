using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Shooting practice: target drones fly loops ahead of the start. Each one destroyed is replaced a few
    /// seconds later somewhere else, so there is always something to shoot. Counts kills, hits and shots
    /// (accuracy) and missiles; no time limit. The drones don't shoot back (AI pilots come in M4).
    /// </summary>
    public class DroneRange : MonoBehaviour, IGameMode
    {
        public static DroneRange Instance { get; private set; }

        public string Title => "DRONE RANGE";
        public Pose StartPose => new(new Vector3(0f, 0f, -GameConfig.Instance.arena.radius * 0.3f), Quaternion.identity);

        public int Kills { get; private set; }
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        public int Missiles { get; private set; }
        public float Elapsed { get; private set; }
        public float LastKillTime { get; private set; } = -99f;
        public int Alive { get; private set; }

        TargetDrone[] _drones;
        System.Random _rng;
        ShipWeapons _weapons;

        void Awake() => Instance = this;

        void OnEnable()
        {
            var player = PlayerShip.Instance;
            _weapons = player != null ? player.GetComponent<ShipWeapons>() : null;
            if (_weapons != null) { _weapons.Fired += OnFired; _weapons.MissileFired += OnMissile; }
            if (LaserSystem.Instance != null) LaserSystem.Instance.Hit += OnLaserHit;
        }

        void OnDisable()
        {
            if (_weapons != null) { _weapons.Fired -= OnFired; _weapons.MissileFired -= OnMissile; }
            if (LaserSystem.Instance != null) LaserSystem.Instance.Hit -= OnLaserHit;
            StopAllCoroutines();
        }

        public void ResetRun()
        {
            if (_weapons == null) { OnDisable(); OnEnable(); }
            var cfg = GameConfig.Instance.drones;
            _rng = new System.Random(19);
            if (_drones == null || _drones.Length != cfg.count)
            {
                if (_drones != null) foreach (var d in _drones) if (d != null) Destroy(d.gameObject);
                _drones = new TargetDrone[cfg.count];
                for (int i = 0; i < cfg.count; i++)
                {
                    _drones[i] = TargetDrone.Create(transform, i);
                    var drone = _drones[i];
                    drone.Health.Destroyed += _ => OnDroneDestroyed(drone);
                }
            }
            StopAllCoroutines();
            foreach (var d in _drones) d.Respawn(RandomCenter(), _rng);
            Kills = Shots = Hits = Missiles = 0;
            Elapsed = 0f;
            LastKillTime = -99f;
            Alive = _drones.Length;
        }

        void Update()
        {
            Elapsed += Time.deltaTime;
        }

        Vector3 RandomCenter()
        {
            var cfg = GameConfig.Instance.drones;
            var start = StartPose.position;
            // ahead of the start, within a wide cone, so a new drone is usually findable
            var dir = Quaternion.Euler(Rand(-25f, 25f), Rand(-60f, 60f), 0f) * Vector3.forward;
            return start + dir * Rand(cfg.distance.x, cfg.distance.y);
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        void OnDroneDestroyed(TargetDrone drone)
        {
            Kills++;
            Alive--;
            LastKillTime = Time.time;
            StartCoroutine(RespawnLater(drone));
        }

        IEnumerator RespawnLater(TargetDrone drone)
        {
            yield return new WaitForSeconds(GameConfig.Instance.drones.respawnSeconds);
            drone.Respawn(RandomCenter(), _rng);
            Alive++;
        }

        void OnFired(int gun) => Shots++;
        void OnMissile() => Missiles++;

        void OnLaserHit(Damageable target, Vector3 point, bool killed, Damageable shooter)
        {
            if (shooter != null && shooter == PlayerShip.Instance?.Health) Hits++;
        }

        public float Accuracy => Shots > 0 ? Hits / (float)Shots : 0f;
    }
}
