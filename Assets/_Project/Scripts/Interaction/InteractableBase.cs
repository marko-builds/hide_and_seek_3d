using UnityEngine;
using UnityEngine.Events;

namespace HideAndSeek
{
    /// <summary>
    /// Abstract base for all interactable world objects. Implements IInteractable and
    /// exposes an OnInteract UnityEvent for inspector-driven reactions.
    /// Concrete props inherit from this class.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] UnityEvent<PlayerController> _onInteract;

        public virtual bool CanInteract { get; protected set; } = true;

        public virtual void Interact(PlayerController interactor)
        {
			Debug.Log(CanInteract);
            if (!CanInteract) return;
            _onInteract.Invoke(interactor);
            OnInteracted(interactor);
        }

        /// <summary>Override to add concrete interaction logic without calling base.</summary>
        protected virtual void OnInteracted(PlayerController interactor) { }
    }
}
