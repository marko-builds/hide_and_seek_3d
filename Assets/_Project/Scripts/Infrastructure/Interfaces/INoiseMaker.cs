namespace HideAndSeek
{
    /// <summary>
    /// Implemented by entities that emit noise detectable by the enemy (Player, world objects).
    /// </summary>
    public interface INoiseMaker
    {
        float CurrentNoiseLevel { get; }
        string NoiseTag { get; }
    }
}
