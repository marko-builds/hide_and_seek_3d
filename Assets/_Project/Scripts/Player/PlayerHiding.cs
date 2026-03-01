using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Manages the player entering and exiting HidingSpots.
    /// Implements the IHideable data side; triggers animations via Animator.
    /// </summary>
    public class PlayerHiding : MonoBehaviour, IHideable
    {
        public bool IsHidden { get; private set; }
        public HidingSpot CurrentHidingSpot { get; private set; }

        public void EnterHidingSpot(HidingSpot spot)
        {
            if (IsHidden || spot == null) return;
            CurrentHidingSpot = spot;
            IsHidden = true;
            // TODO: move player to spot.AttachTransform, disable movement, trigger animation
        }

        public void ExitHidingSpot()
        {
            if (!IsHidden) return;
            // TODO: move player to spot exit offset, re-enable movement, trigger animation
            IsHidden = false;
            CurrentHidingSpot = null;
        }
    }
}
