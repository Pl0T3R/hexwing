namespace HexWing
{
    /// <summary>
    /// PlayerPrefs keys of saved progress, in one place: the modes write them, the stats page and the
    /// colour unlocks read them. (Settings keys live in UserSettings, the scheme, ship and mode keys in
    /// the classes that own them.)
    /// </summary>
    public static class SaveKeys
    {
        public const string TrainingDone = "HexWing.TrainingDone";
        public const string SurvivalBestScore = "HexWing.Survival.BestScore";
        public const string SurvivalBestWave = "HexWing.Survival.BestWave";
        public const string AssaultBestScore = "HexWing.Assault.BestScore";
        public const string AssaultWon = "HexWing.Assault.Won";
        public const string EscortBestScore = "HexWing.Escort.BestScore";
        public const string EscortWon = "HexWing.Escort.Won";
        public const string Kills = "HexWing.Stats.Kills";
        public const string CockpitColor = "HexWing.CockpitColor";
    }
}
