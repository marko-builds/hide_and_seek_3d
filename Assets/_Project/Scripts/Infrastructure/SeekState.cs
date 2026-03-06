namespace HideAndSeek
{
    /// <summary>
    /// Detection and behavioral state of a single seeker, ordered by ascending severity.
    /// Integer comparison is valid: <c>state >= SeekState.Alert</c> gates the hiding-spot
    /// proximity penalty (GDD F6).
    /// </summary>
    public enum SeekState
    {
        Unaware   = 0,
        Alert     = 1,
        Searching = 2,
        Chase     = 3,
        Caught    = 4,
    }
}
