namespace HideAndSeek
{
    /// <summary>
    /// Implemented by world objects the player can interact with (doors, lockers, etc.).
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract { get; }
        void Interact(PlayerController interactor);
    }
}
