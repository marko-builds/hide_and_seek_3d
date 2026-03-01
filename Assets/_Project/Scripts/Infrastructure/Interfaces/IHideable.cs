namespace HideAndSeek
{
    /// <summary>
    /// Implemented by entities that can occupy a HidingSpot (i.e. the Player).
    /// </summary>
    public interface IHideable
    {
        bool IsHidden { get; }
        HidingSpot CurrentHidingSpot { get; }
    }
}
