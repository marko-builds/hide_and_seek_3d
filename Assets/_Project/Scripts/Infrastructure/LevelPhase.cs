namespace HideAndSeek
{
    /// <summary>The two phases of a hide-and-seek level (GDD Two-Phase Level Structure).</summary>
    public enum LevelPhase
    {
        /// <summary>Player searches for objective tokens. Seekers patrol normally.</summary>
        Phase1_Find,

        /// <summary>All tokens collected. Player must reach the exit. Seekers escalate.</summary>
        Phase2_Escape,
    }
}
