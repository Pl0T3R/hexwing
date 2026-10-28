using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>A mode that wants to handle the player's death itself (e.g. show a result first).</summary>
    public interface IPlayerDeathHandler
    {
        void OnPlayerDestroyed(Damageable by);
    }

    /// <summary>
    /// Survival: waves of enemy fighters, each wave bigger and better flown; every fifth wave brings an
    /// elite that fires missiles. Two AI wingmen fly with you and come back at every wave. One life: when
    /// you are shot down the result stays up for a few seconds (score, wave, best), then the mode restarts.
    /// Enemies appear far ahead (never right behind you). Score: kills, elites and cleared waves; the best
    /// score and wave are kept on the headset.
    /// </summary>
    public class Survival : MonoBehaviour, IGameMode, IPlayerDeathHandler
    {
        const string BestScoreKey = SaveKeys.SurvivalBestScore;
        const string BestWaveKey = SaveKeys.SurvivalBestWave;
        const float SpawnInterval = 1.5f;
        const float ResultSeconds = 6f;

        public static Survival Instance { get; private set; }

        public enum State { Intro, Fighting, GameOver }

        public string Title => "SURVIVAL";
        public Pose StartPose => new(new Vector3(0f, 0f, -GameConfig.Instance.arena.radius * 0.3f), Quaternion.identity);

        public State Phase { get; private set; }
        public int Wave { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int BestScore { get; private set; }
        public int BestWave { get; private set; }
        public bool NewBest { get; private set; }
        /// <summary>Enemies still to beat in this wave (alive plus waiting to spawn).</summary>
        public int EnemiesLeft => _toSpawn + AliveEnemies;
        public int AliveEnemies { get; private set; }
        public int WingmenAlive { get; private set; }
        public float PhaseTime => Time.time - _phaseStart;
        public bool EliteWave { get; private set; }

        readonly List<AIShip> _hornets = new(), _tridents = new(), _wingmen = new();
        System.Random _rng;
        int _toSpawn, _eliteToSpawn;
        float _phaseStart, _nextSpawn;

        void Awake() => Instance = this;

        void OnDisable() => DespawnAll();

        public void ResetRun()
        {
            _rng = new System.Random();
            DespawnAll();
            Score = Kills = 0;
            Wave = 0;
            NewBest = false;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            BestWave = PlayerPrefs.GetInt(BestWaveKey, 0);
            NextWave();
        }

        void NextWave()
        {
            var cfg = GameConfig.Instance.survival;
            Wave++;
            EliteWave = cfg.eliteEvery > 0 && Wave % cfg.eliteEvery == 0;
            _toSpawn = cfg.firstWave + Mathf.RoundToInt(cfg.perWave * (Wave - 1));
            _eliteToSpawn = EliteWave ? 1 : 0;
            if (EliteWave) _toSpawn = Mathf.Max(0, _toSpawn - 1);
            SetPhase(State.Intro);
            SpawnWingmen();
            Debug.Log($"[HexWing] Survival wave {Wave}: {_toSpawn + _eliteToSpawn} enemies{(EliteWave ? " with an elite" : "")}, skill {Skill(false):F2}.");
        }

        void SetPhase(State s)
        {
            Phase = s;
            _phaseStart = Time.time;
        }

        float Skill(bool elite)
        {
            var cfg = GameConfig.Instance.survival;
            float s = Mathf.Clamp01((Wave - 1) / (float)Mathf.Max(1, cfg.fullSkillWave - 1));
            return Mathf.Clamp01(elite ? s + 0.25f : s);
        }

        void Update()
        {
            var cfg = GameConfig.Instance.survival;
            switch (Phase)
            {
                case State.Intro:
                    if (PhaseTime >= cfg.waveGap) SetPhase(State.Fighting);
                    break;
                case State.Fighting:
                    if ((_toSpawn > 0 || _eliteToSpawn > 0) && AliveEnemies < cfg.maxAlive && Time.time >= _nextSpawn)
                    {
                        _nextSpawn = Time.time + SpawnInterval;
                        // spawn in small groups at the start of a wave
                        int group = Mathf.Min(AliveEnemies == 0 ? 3 : 1, cfg.maxAlive - AliveEnemies);
                        for (int i = 0; i < group && (_toSpawn > 0 || _eliteToSpawn > 0); i++) SpawnEnemy();
                    }
                    if (_toSpawn == 0 && _eliteToSpawn == 0 && AliveEnemies == 0)
                    {
                        Score += cfg.scorePerWave;
                        NextWave();
                    }
                    break;
                case State.GameOver:
                    if (PhaseTime >= ResultSeconds) ModeDirector.Instance?.Restart("koniec gry");
                    break;
            }
        }

        // --- spawning ----------------------------------------------------------------------------

        void SpawnEnemy()
        {
            var cfg = GameConfig.Instance.survival;
            bool elite = _eliteToSpawn > 0 && (_toSpawn == 0 || _rng.NextDouble() < 0.4);
            var ship = elite ? Take(_tridents, ShipMeshes.Kind.Trident) : Take(_hornets, ShipMeshes.Kind.Hornet);
            if (ship == null) return;
            if (elite) _eliteToSpawn--; else _toSpawn--;
            ship.Spawn(EnemyPose(), Team.Enemy, Skill(elite), elite ? cfg.eliteHull : cfg.enemyHull, elite ? cfg.eliteShields : cfg.enemyShields, false, 0);
            AliveEnemies++;
        }

        Pose EnemyPose()
        {
            var cfg = GameConfig.Instance.survival;
            var player = PlayerShip.Instance != null ? PlayerShip.Instance.transform : transform;
            var dir = Quaternion.AngleAxis(Rand(-cfg.spawnCone, cfg.spawnCone), player.up)
                    * Quaternion.AngleAxis(Rand(-cfg.spawnCone * 0.4f, cfg.spawnCone * 0.4f), player.right) * player.forward;
            var pos = player.position + dir * Rand(cfg.spawnDistance.x, cfg.spawnDistance.y);
            float arena = GameConfig.Instance.arena.radius * 0.8f;
            if (pos.magnitude > arena) pos = pos.normalized * arena;   // keep spawns inside the arena
            return new Pose(pos, Quaternion.LookRotation(player.position - pos, Vector3.up));
        }

        void SpawnWingmen()
        {
            var cfg = GameConfig.Instance.survival;
            var player = PlayerShip.Instance;
            if (player == null) return;
            var pt = player.transform;
            WingmenAlive = 0;
            for (int i = 0; i < cfg.wingmen; i++)
            {
                while (_wingmen.Count <= i) _wingmen.Add(CreateShip(ShipMeshes.Kind.Swallow, _wingmen.Count));
                var w = _wingmen[i];
                if (w.Active) { WingmenAlive++; continue; }
                var offset = GameConfig.Instance.ai.wingOffset;
                offset.x *= i % 2 == 0 ? 1f : -1f;
                offset.z -= (i / 2) * 25f + 40f;   // a bit further back than the slot, so they fly in
                w.Spawn(new Pose(pt.TransformPoint(offset), pt.rotation), Team.Player, 0.6f, cfg.wingmanHull, cfg.wingmanShields, true, i);
                WingmenAlive++;
            }
        }

        AIShip Take(List<AIShip> pool, ShipMeshes.Kind kind)
        {
            foreach (var s in pool) if (!s.gameObject.activeSelf) return s;
            var cap = kind == ShipMeshes.Kind.Hornet ? GameConfig.Instance.survival.maxAlive : 3;
            if (pool.Count >= cap) return null;
            var ship = CreateShip(kind, pool.Count);
            pool.Add(ship);
            return ship;
        }

        AIShip CreateShip(ShipMeshes.Kind kind, int index)
        {
            var ship = AIShip.Create(transform, kind, index);
            ship.Killed += OnShipKilled;
            return ship;
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        void OnShipKilled(AIShip ship, Damageable by)
        {
            var cfg = GameConfig.Instance.survival;
            if (ship.Health.Team == Team.Player)
            {
                WingmenAlive = Mathf.Max(0, WingmenAlive - 1);
                return;
            }
            AliveEnemies = Mathf.Max(0, AliveEnemies - 1);
            if (Phase == State.GameOver) return;
            if (by != null && PlayerShip.Instance != null && by == PlayerShip.Instance.Health)
            {
                Kills++;
                Score += ship.Kind == ShipMeshes.Kind.Trident ? cfg.scorePerElite : cfg.scorePerKill;
            }
        }

        void DespawnAll()
        {
            foreach (var s in _hornets) s.Despawn();
            foreach (var s in _tridents) s.Despawn();
            foreach (var s in _wingmen) s.Despawn();
            AliveEnemies = WingmenAlive = 0;
            _toSpawn = _eliteToSpawn = 0;
        }

        public void OnPlayerDestroyed(Damageable by)
        {
            if (Phase == State.GameOver) return;
            SetPhase(State.GameOver);
            NewBest = Score > BestScore;
            if (NewBest) BestScore = Score;
            BestWave = Mathf.Max(BestWave, Wave);
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.SetInt(BestWaveKey, BestWave);
            PlayerPrefs.Save();
            Debug.Log($"[HexWing] Survival over: wave {Wave}, score {Score}, {Kills} kills{(NewBest ? ", new best" : "")}.");
        }
    }
}
