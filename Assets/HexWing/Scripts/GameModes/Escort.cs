using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Escort (map Belt): three transports cross the asteroid belt along a cleared route; enemy fighters
    /// ambush them at three points on the way (the last ambush brings an elite). You and two wingmen keep
    /// them alive. Shot down, you come back near the convoy while your team's ship pool lasts.
    /// Win: at least one transport reaches the end. Lose: all transports destroyed, or no ships left.
    /// </summary>
    public class Escort : MonoBehaviour, IGameMode, IGuidedMode, IPlayerDeathHandler, IMapMode
    {
        const string BestKey = SaveKeys.EscortBestScore;
        const float ResultSeconds = 9f;

        public static Escort Instance { get; private set; }

        public enum State { Briefing, Underway, Won, Lost }

        public string Title => "ESKORTA";
        public Map Map => Map.Belt;
        public Pose StartPose => new(_route.PointAt(0f) + _route.DirectionAt(0f) * -150f + Vector3.up * 40f, Quaternion.LookRotation(_route.DirectionAt(0f)));

        public State Phase { get; private set; }
        public int TransportsAlive { get; private set; }
        public int TransportsTotal => _transports != null ? _transports.Length : 0;
        public float RouteProgress { get; private set; }
        public int Ambush { get; private set; }
        public int LivesLeft { get; private set; }
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public int Kills { get; private set; }
        public float PhaseTime => Time.time - _phaseStart;
        public string LoseReason { get; private set; } = "";
        public int EnemiesAlive => _fleet != null ? _fleet.Alive(Team.Enemy) : 0;

        /// <summary>The arrow points at the lead transport (or the nearest one under attack).</summary>
        public Transform Pointer => Lead != null ? Lead.transform : null;

        Transport Lead
        {
            get
            {
                if (_transports == null) return null;
                Transport best = null;
                foreach (var t in _transports)
                    if (t.gameObject.activeSelf && t.Health.Alive && (best == null || t.Distance > best.Distance)) best = t;
                return best;
            }
        }

        readonly Route _route = new(
            new Vector3(-2300f, 0f, -900f), new Vector3(-1200f, 120f, -250f), new Vector3(0f, -80f, 300f),
            new Vector3(1200f, 60f, 800f), new Vector3(2300f, 0f, 1150f));
        Transport[] _transports;
        AsteroidField _field;
        AIFleet _fleet;
        float _phaseStart;

        void Awake() => Instance = this;

        void OnDisable() => _fleet?.DespawnAll();

        public void ResetRun()
        {
            var cfg = GameConfig.Instance.escort;
            if (_field == null)
            {
                _field = new GameObject("Asteroid Field").AddComponent<AsteroidField>();
                _field.transform.SetParent(transform, false);
                _field.Build(_route, cfg);
            }
            if (_fleet == null)
            {
                _fleet = new AIFleet(transform);
                _fleet.Killed += OnFleetKill;
            }
            if (_transports == null || _transports.Length != cfg.transports)
            {
                if (_transports != null) foreach (var t in _transports) Destroy(t.gameObject);
                _transports = new Transport[cfg.transports];
                for (int i = 0; i < cfg.transports; i++)
                {
                    _transports[i] = Transport.Create(transform, i);
                    _transports[i].Lost += OnTransportLost;
                }
            }
            _fleet.DespawnAll();
            for (int i = 0; i < _transports.Length; i++)
                _transports[i].Begin(_route, cfg.spacing * (_transports.Length - 1 - i), cfg.transportSpeed, cfg.transportHull, cfg.transportShields);
            TransportsAlive = _transports.Length;
            Ambush = 0;
            LivesLeft = cfg.playerLives;
            Score = Kills = 0;
            LoseReason = "";
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
            for (int i = 0; i < cfg.wingmen; i++) SpawnWingman(i);
            SetPhase(State.Briefing);
            Debug.Log($"[HexWing] Escort: route {_route.Length:F0} m, {_transports.Length} transports at {cfg.transportSpeed} m/s.");
        }

        void SetPhase(State s)
        {
            Phase = s;
            _phaseStart = Time.time;
        }

        void Update()
        {
            var cfg = GameConfig.Instance.escort;
            var lead = Lead;
            RouteProgress = lead != null ? lead.Distance / _route.Length : RouteProgress;
            switch (Phase)
            {
                case State.Briefing:
                    if (PhaseTime > 6f) SetPhase(State.Underway);
                    break;
                case State.Underway:
                    if (Ambush < cfg.ambushAt.Length && RouteProgress >= cfg.ambushAt[Ambush])
                    {
                        SpawnAmbush(Ambush);
                        Ambush++;
                    }
                    foreach (var t in _transports)
                        if (t.Arrived && t.Health.Alive) { Win(); break; }
                    break;
                case State.Won:
                case State.Lost:
                    if (PhaseTime >= ResultSeconds) ModeDirector.Instance?.Restart(Phase == State.Won ? "zwycięstwo" : "porażka");
                    break;
            }
        }

        void SpawnAmbush(int index)
        {
            var cfg = GameConfig.Instance.escort;
            var survival = GameConfig.Instance.survival;
            var lead = Lead;
            if (lead == null) return;
            int count = cfg.ambushBase + index;
            var ahead = _route.PointAt(lead.Distance + 900f);
            var dir = _route.DirectionAt(lead.Distance + 900f);
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < count; i++)
            {
                // out of the rocks on both sides of the route, ahead of the convoy
                float s = i % 2 == 0 ? 1f : -1f;
                var pos = ahead + side * s * (250f + 40f * i) + Vector3.up * (i * 25f - 50f);
                var look = Quaternion.LookRotation((lead.transform.position - pos).normalized, Vector3.up);
                bool elite = index == cfg.ambushAt.Length - 1 && i == 0;
                if (elite) _fleet.Spawn(ShipMeshes.Kind.Trident, new Pose(pos, look), Team.Enemy, cfg.enemySkill + 0.2f, survival.eliteHull, survival.eliteShields);
                else _fleet.Spawn(ShipMeshes.Kind.Hornet, new Pose(pos, look), Team.Enemy, cfg.enemySkill + index * 0.1f, survival.enemyHull, survival.enemyShields);
            }
            ShipAudio.Instance?.PlayMissileWarning(false);
            Debug.Log($"[HexWing] Escort ambush {index + 1}: {count} enemies at {RouteProgress:P0} of the route.");
        }

        void SpawnWingman(int slot)
        {
            var survival = GameConfig.Instance.survival;
            var pose = StartPose;
            var offset = GameConfig.Instance.ai.wingOffset;
            offset.x *= slot % 2 == 0 ? 1f : -1f;
            _fleet.Spawn(ShipMeshes.Kind.Swallow, new Pose(pose.position + pose.rotation * offset, pose.rotation), Team.Player, 0.6f,
                         survival.wingmanHull, survival.wingmanShields, true, slot);
        }

        void OnFleetKill(AIShip ship, Damageable by)
        {
            if (ship.Health.Team != Team.Enemy || Phase == State.Won || Phase == State.Lost) return;
            if (by != null && PlayerShip.Instance != null && by == PlayerShip.Instance.Health)
            {
                Kills++;
                Score += GameConfig.Instance.escort.scorePerKill;
            }
        }

        void OnTransportLost(Transport t)
        {
            TransportsAlive = 0;
            foreach (var x in _transports) if (x.gameObject.activeSelf && x.Health.Alive) TransportsAlive++;
            Debug.Log($"[HexWing] Transport lost, {TransportsAlive} left.");
            if (TransportsAlive == 0 && Phase != State.Won && Phase != State.Lost) Lose("KONWÓJ ZNISZCZONY");
        }

        void Win()
        {
            int arrived = 0;
            foreach (var t in _transports) if (t.gameObject.activeSelf && t.Health.Alive) arrived++;
            Score += arrived * GameConfig.Instance.escort.scorePerTransport;
            SetPhase(State.Won);
            PlayerPrefs.SetInt(SaveKeys.EscortWon, 1);   // unlocks a cockpit colour
            SaveBest();
            PlayerPrefs.Save();
            Debug.Log($"[HexWing] Escort won: {arrived} transports through, score {Score}.");
        }

        void Lose(string reason)
        {
            LoseReason = reason;
            SetPhase(State.Lost);
            SaveBest();
            Debug.Log($"[HexWing] Escort lost ({reason}): score {Score}.");
        }

        void SaveBest()
        {
            if (Score <= BestScore) return;
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }

        public void OnPlayerDestroyed(Damageable by)
        {
            if (Phase == State.Won || Phase == State.Lost) return;
            if (LivesLeft <= 0) { Lose("BRAK STATKÓW"); return; }
            LivesLeft--;
            // back behind the convoy's lead ship, facing along the route
            var lead = Lead;
            float d = lead != null ? lead.Distance - 300f : 0f;
            var pose = new Pose(_route.PointAt(d) + Vector3.up * 60f, Quaternion.LookRotation(_route.DirectionAt(d)));
            ModeDirector.Instance?.RespawnPlayer(pose, GameConfig.Instance.escort.respawnDelay);
        }
    }
}
