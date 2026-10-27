using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Capital Ship Assault (PvE, map Orbit): destroy the capital ship "Kolos" within 15 minutes:
    /// shield generators, then turrets, then the reactor in the stern trench. Enemy fighters keep coming
    /// out of the capital ship (more once its turrets are down); three AI wingmen fly with you and are
    /// replaced over time. Your team has a pool of ships: each time you are shot down you come back at the
    /// start until the pool is empty. Win: the reactor blows. Lose: time runs out or the pool is empty.
    /// </summary>
    public class CapitalAssault : MonoBehaviour, IGameMode, IGuidedMode, IPlayerDeathHandler
    {
        const string BestKey = "HexWing.Assault.BestScore";
        const float ResultSeconds = 9f;

        public static CapitalAssault Instance { get; private set; }

        public enum State { Briefing, Fighting, Won, Lost }

        public string Title => "SZTURM";
        public Pose StartPose => new(new Vector3(0f, 120f, -1600f), Quaternion.LookRotation(new Vector3(0f, -0.06f, 1f)));

        public State Phase { get; private set; }
        public float TimeLeft { get; private set; }
        public int LivesLeft { get; private set; }
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public int Kills { get; private set; }
        public CapitalShip Ship { get; private set; }
        public float PhaseTime => Time.time - _phaseStart;
        public string LoseReason { get; private set; } = "";

        public Transform Pointer
        {
            get
            {
                if (Ship == null || Phase != State.Fighting) return null;
                switch (Ship.Current)
                {
                    case CapitalShip.Phase.Generators: return Nearest(Ship.Generators);
                    case CapitalShip.Phase.Turrets:
                        Transform best = null;
                        float bestSq = float.MaxValue;
                        foreach (var t in Ship.Turrets)
                        {
                            if (!t.Health.Alive || !t.gameObject.activeSelf) continue;
                            float sq = (t.transform.position - PlayerPos).sqrMagnitude;
                            if (sq < bestSq) { bestSq = sq; best = t.transform; }
                        }
                        return best;
                    case CapitalShip.Phase.Reactor: return Ship.Reactor.transform;
                    default: return null;
                }
            }
        }

        AIFleet _fleet;
        float _phaseStart, _nextEnemy, _nextWingman;

        Vector3 PlayerPos => PlayerShip.Instance != null ? PlayerShip.Instance.transform.position : Vector3.zero;

        void Awake() => Instance = this;

        void OnDisable() => _fleet?.DespawnAll();

        public void ResetRun()
        {
            var cfg = GameConfig.Instance.assault;
            if (_fleet == null)
            {
                _fleet = new AIFleet(transform);
                _fleet.Killed += OnFleetKill;
            }
            if (Ship == null)
            {
                Ship = CapitalShip.Create(transform);
                Ship.transform.SetPositionAndRotation(new Vector3(0f, 0f, 700f), Quaternion.LookRotation(Vector3.left));   // broadside to the player's approach
                Ship.PartDestroyed += OnPartDestroyed;
                Ship.PhaseChanged += OnShipPhase;
            }
            _fleet.DespawnAll();
            Ship.ResetShip();
            TimeLeft = cfg.timeLimit;
            LivesLeft = cfg.playerLives;
            Score = Kills = 0;
            LoseReason = "";
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
            SetPhase(State.Briefing);
            for (int i = 0; i < cfg.wingmen; i++) SpawnWingman(i, true);
            _nextEnemy = Time.time + 6f;
            _nextWingman = Time.time + cfg.wingmanRespawn;
        }

        void SetPhase(State s)
        {
            Phase = s;
            _phaseStart = Time.time;
        }

        void Update()
        {
            var cfg = GameConfig.Instance.assault;
            switch (Phase)
            {
                case State.Briefing:
                    if (PhaseTime > 6f) SetPhase(State.Fighting);
                    break;
                case State.Fighting:
                    TimeLeft -= Time.deltaTime;
                    if (TimeLeft <= 0f) { Lose("CZAS MINĄŁ"); break; }
                    int wanted = cfg.enemyFighters + (Ship.Current >= CapitalShip.Phase.Reactor ? 2 : 0);
                    if (Time.time >= _nextEnemy && _fleet.Alive(Team.Enemy) < wanted)
                    {
                        _nextEnemy = Time.time + cfg.enemyRespawn;
                        SpawnEnemy(Ship.Current >= CapitalShip.Phase.Turrets && _fleet.Alive(Team.Enemy) == 0);
                    }
                    if (Time.time >= _nextWingman)
                    {
                        _nextWingman = Time.time + cfg.wingmanRespawn;
                        for (int i = 0; i < cfg.wingmen; i++) if (!_fleet.IsWingmanAlive(i)) { SpawnWingman(i, false); break; }
                    }
                    break;
                case State.Won:
                case State.Lost:
                    if (PhaseTime >= ResultSeconds) ModeDirector.Instance?.Restart(Phase == State.Won ? "zwycięstwo" : "porażka");
                    break;
            }
        }

        void SpawnEnemy(bool elite)
        {
            var cfg = GameConfig.Instance.assault;
            // out of one of the capital ship's flanks (its local x), somewhere along its length
            float side = Random.value < 0.5f ? -1f : 1f;
            var local = new Vector3(side * 160f, Random.Range(-40f, 80f), Random.Range(-200f, 200f));
            var pos = Ship.transform.TransformPoint(local);
            var look = Quaternion.LookRotation((PlayerPos - pos).normalized, Vector3.up);
            var survival = GameConfig.Instance.survival;
            if (elite) _fleet.Spawn(ShipMeshes.Kind.Trident, new Pose(pos, look), Team.Enemy, cfg.enemySkill + 0.2f, survival.eliteHull, survival.eliteShields);
            else _fleet.Spawn(ShipMeshes.Kind.Hornet, new Pose(pos, look), Team.Enemy, cfg.enemySkill, survival.enemyHull, survival.enemyShields);
        }

        void SpawnWingman(int slot, bool atStart)
        {
            var survival = GameConfig.Instance.survival;
            var player = PlayerShip.Instance != null ? PlayerShip.Instance.transform : transform;
            var offset = GameConfig.Instance.ai.wingOffset;
            offset.x *= slot % 2 == 0 ? 1f : -1f;
            offset.z -= (slot / 2) * 25f + (atStart ? 0f : 300f);
            var basePose = atStart ? StartPose : new Pose(player.position, player.rotation);
            var pos = basePose.position + basePose.rotation * offset;
            _fleet.Spawn(ShipMeshes.Kind.Swallow, new Pose(pos, basePose.rotation), Team.Player, 0.6f, survival.wingmanHull, survival.wingmanShields, true, slot);
        }

        void OnFleetKill(AIShip ship, Damageable by)
        {
            if (ship.Health.Team != Team.Enemy || Phase != State.Fighting) return;
            if (IsPlayer(by)) { Kills++; Score += GameConfig.Instance.assault.scorePerKill; }
        }

        void OnPartDestroyed(Damageable part, Damageable by)
        {
            var cfg = GameConfig.Instance.assault;
            if (part == Ship.Reactor) Score += cfg.scoreReactor + Mathf.RoundToInt(TimeLeft);   // a second of time left = a point
            else if (Ship.Generators.Contains(part)) Score += cfg.scorePerGenerator;
            else Score += cfg.scorePerTurret;
        }

        void OnShipPhase(CapitalShip.Phase phase)
        {
            if (phase == CapitalShip.Phase.Destroyed && Phase == State.Fighting) Win();
            else if (phase != CapitalShip.Phase.Generators) ShipAudio.Instance?.PlayStep();
        }

        static bool IsPlayer(Damageable d) => d != null && PlayerShip.Instance != null && d == PlayerShip.Instance.Health;

        void Win()
        {
            SetPhase(State.Won);
            PlayerPrefs.SetInt("HexWing.Assault.Won", 1);   // unlocks a cockpit colour
            SaveBest();
            PlayerPrefs.Save();
            Debug.Log($"[HexWing] Assault won: score {Score}, {TimeLeft:F0} s left, {LivesLeft} lives left.");
        }

        void Lose(string reason)
        {
            LoseReason = reason;
            SetPhase(State.Lost);
            SaveBest();
            Debug.Log($"[HexWing] Assault lost ({reason}): score {Score}.");
        }

        void SaveBest()
        {
            if (Score <= BestScore) return;
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }

        public void OnPlayerDestroyed()
        {
            if (Phase == State.Won || Phase == State.Lost) return;
            if (LivesLeft <= 0) { Lose("BRAK STATKÓW"); return; }
            LivesLeft--;
            ModeDirector.Instance?.RespawnPlayer(StartPose, GameConfig.Instance.assault.respawnDelay);
        }

        Transform Nearest(System.Collections.Generic.List<Damageable> parts)
        {
            Transform best = null;
            float bestSq = float.MaxValue;
            foreach (var p in parts)
            {
                if (!p.Alive || !p.gameObject.activeSelf) continue;
                float sq = (p.transform.position - PlayerPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p.transform; }
            }
            return best;
        }
    }
}
