using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Manages the player entering and exiting HidingSpots.
    /// Disables PlayerMovement, freezes the Rigidbody, and teleports the player
    /// to the spot's attach transform. Reverses all of this on exit.
    /// </summary>
    public class PlayerHiding : MonoBehaviour, IHideable
    {
        public bool IsHidden { get; private set; }
        public HidingSpot CurrentHidingSpot { get; private set; }

        PlayerMovement _movement;
        Rigidbody _rigidbody;

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Snaps the player into <paramref name="spot"/>, freezes locomotion,
        /// and marks the player as hidden. Called by the hiding spot interactable.
        /// </summary>
        public void EnterHidingSpot(HidingSpot spot)
        {
            if (IsHidden || spot == null) return;

            CurrentHidingSpot = spot;
            IsHidden = true;

            _movement.enabled = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;

            Transform attach = spot.AttachTransform;
            _rigidbody.position = attach.position;
            _rigidbody.rotation = attach.rotation;
        }

        /// <summary>
        /// Moves the player to the spot's exit offset, restores locomotion,
        /// and marks the player as visible. Clears the spot's occupancy.
        /// </summary>
        public void ExitHidingSpot()
        {
            if (!IsHidden) return;

            Vector3 exitPos = CurrentHidingSpot.ExitPosition;

            IsHidden = false;
            CurrentHidingSpot.Exit();
            CurrentHidingSpot = null;

            _rigidbody.isKinematic = false;
            _rigidbody.position = exitPos;
            _movement.enabled = true;
        }
    }
}
