namespace HideAndSeek
{
    /// <summary>
    /// Marker interface. Interactables that implement this bypass the PlayerInteractionSystem's
    /// rule that blocks interaction while the player is being chased (e.g. the LevelExit,
    /// which must remain usable even during Chase — GDD Rule LE-7).
    /// </summary>
    public interface IChaseInteractable { }
}
