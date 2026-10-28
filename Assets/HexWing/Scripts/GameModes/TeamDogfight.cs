using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Team Dogfight, offline (map Orbit): you and three wingmen against four enemy fighters. Every ship
    /// shot down scores for the other team; the first team to 30 kills wins, or the team ahead when the
    /// 10 minutes run out (equal = draw). Everyone comes back at their team's base a few seconds after
    /// being shot down. This is M7 without the network: the slots of a <see cref="TeamRoster"/> are bots
    /// here and become players online.
    /// </summary>
    public class TeamDogfight : MonoBehaviour, IGameMode, IPlayerDeathHandler, IGuidedMode
    {
        const float ResultSeconds = 9f;

        public static TeamDogfight Instance { get; private set; }

        public enum State { Briefing, Fighting, Over }

        public string Title => "TEAM DOGFIGHT";
        public Pose StartPose => BasePose(Team.Player, 0);
        /// <summary>No special pointer: the arrow shows the target or the nearest enemy.</summary>
        public Transform Pointer => null;

        public State Phase { get; private set; }
        public int OurKills { get; private set; }
        public int TheirKills { get; private set; }
        public float TimeLeft { get; private set; }
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        /// <summary>+1 we won, -1 we lost, 0 draw (valid when the match is over).</summary>
        public int Result { get; private set; }
        public float PhaseTime => Time.time - _phaseStart;
        public TeamRoster Roster { get; } = new();

        AIFleet _fleet;
        float _phaseStart;

        void Awake() => Instance = this;

        void OnDisable() => _fleet?.DespawnAll();

        public void ResetRun()
        {
            var cfg = GameConfig.Instance.dogfight;
            if (_fleet == null)
            {
                _fleet = new AIFleet(transform);
                _fleet.Killed += OnFleetKill;
            }
            _fleet.DespawnAll();
            Roster.FillOffline(Mathf.Max(1, cfg.teamSize));
            OurKills = TheirKills = Score = 0;
            Result = 0;
            TimeLeft = cfg.timeLimit;
            BestScore = PlayerPrefs.GetInt(SaveKeys.DogfightBestScore, 0);
            int index = 0;
            foreach (var slot in Roster.Slots)
            {
                if (slot.Seat == TeamRoster.Seat.Bot) Spawn(slot, index);
                index++;
            }
            SetPhase(State.Briefing);
            Debug.Log($"[HexWing] Team dogfight: {cfg.teamSize} v {cfg.teamSize}, first to {cfg.killsToWin} or {cfg.timeLimit / 60f:F0} min.");
        }

        void SetPhase(State s)
        {
            Phase = s;
            _phaseStart = Time.time;
        }

        void Update()
        {
            var cfg = GameConfig.Instance.dogfight;
            switch (Phase)
            {
                case State.Briefing:
                    if (PhaseTime > 5f) SetPhase(State.Fighting);
                    break;
                case State.Fighting:
                    TimeLeft -= Time.deltaTime;
                    int i = 0;
                    foreach (var slot in Roster.Slots)
                    {
                        if (slot.Seat == TeamRoster.Seat.Bot && (slot.Ship == null || !slot.Ship.Active) && Time.time >= slot.RespawnAt)
                            Spawn(slot, i);
                        i++;
                    }
                    if (OurKills >= cfg.killsToWin || TheirKills >= cfg.killsToWin || TimeLeft <= 0f) End();
                    break;
                case State.Over:
                    if (PhaseTime >= ResultSeconds) ModeDirector.Instance?.Restart("koniec meczu");
                    break;
            }
        }

        /// <summary>Each team's base is on its side of the arena, the ships spread across a short line, nose to the centre.</summary>
        Pose BasePose(Team team, int index)
        {
            var cfg = GameConfig.Instance.dogfight;
            float side = team == Team.Player ? -1f : 1f;
            var basePos = new Vector3(0f, 60f * side, cfg.baseDistance * side);
            float spread = (index % Mathf.Max(1, cfg.teamSize) - (cfg.teamSize - 1) * 0.5f) * cfg.spawnSpread;
            var pos = basePos + new Vector3(spread, 0f, 0f);
            return new Pose(pos, Quaternion.LookRotation(new Vector3(0f, 0f, -side)));
        }

        void Spawn(TeamRoster.Slot slot, int index)
        {
            var cfg = GameConfig.Instance.dogfight;
            bool ally = slot.Team == Team.Player;
            slot.Ship = _fleet.Spawn(ally ? ShipMeshes.Kind.Swallow : ShipMeshes.Kind.Hornet, BasePose(slot.Team, index), slot.Team,
                                     ally ? cfg.allySkill : cfg.enemySkill, cfg.botHull, cfg.botShields, slot.WingSlot, slot.WingIndex);
            slot.Ship.Health.DisplayName = slot.Name;
        }

        void OnFleetKill(AIShip ship, Damageable by)
        {
            var slot = Roster.FindByShip(ship);
            if (slot == null) return;
            slot.Deaths++;
            slot.RespawnAt = Time.time + GameConfig.Instance.dogfight.botRespawn;
            slot.Ship = null;   // the pooled ship may come back for another slot
            Scored(slot.Team, by);
        }

        public void OnPlayerDestroyed(Damageable by)
        {
            var me = Roster.LocalPlayer;
            if (me != null) me.Deaths++;
            Scored(Team.Player, by);
            if (Phase != State.Over)
                ModeDirector.Instance?.RespawnPlayer(BasePose(Team.Player, 0), GameConfig.Instance.dogfight.playerRespawn);
        }

        /// <summary>A ship of <paramref name="victimTeam"/> went down: the other team scores, the killer's slot gets the kill.</summary>
        void Scored(Team victimTeam, Damageable by)
        {
            if (Phase == State.Over) return;
            if (victimTeam == Team.Player) TheirKills++; else OurKills++;
            var killer = Roster.FindByHealth(by);
            if (killer != null && killer.Team != victimTeam)
            {
                killer.Kills++;
                if (killer.Seat == TeamRoster.Seat.LocalPlayer) Score += GameConfig.Instance.dogfight.scorePerKill;
            }
        }

        void End()
        {
            Result = OurKills > TheirKills ? 1 : OurKills < TheirKills ? -1 : 0;
            if (Result > 0)
            {
                Score += GameConfig.Instance.dogfight.scoreWin;
                PlayerPrefs.SetInt(SaveKeys.DogfightWon, 1);   // unlocks a cockpit colour
            }
            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(SaveKeys.DogfightBestScore, BestScore);
            }
            PlayerPrefs.Save();
            SetPhase(State.Over);
            var me = Roster.LocalPlayer;
            Debug.Log($"[HexWing] Team dogfight over: {OurKills}:{TheirKills} ({(Result > 0 ? "won" : Result < 0 ? "lost" : "draw")}), " +
                      $"you {me?.Kills ?? 0} kills / {me?.Deaths ?? 0} deaths, score {Score}.");
        }
    }
}
