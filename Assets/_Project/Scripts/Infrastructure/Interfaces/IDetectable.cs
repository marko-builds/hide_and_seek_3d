namespace HideAndSeek
{
    /// <summary>
    /// Implemented by entities that can be detected by enemy AI (i.e. the Player).
    /// </summary>
    public interface IDetectable
    {
        DetectionProfile DetectionProfile { get; }
        void OnDetected();
    }
}
