using System.Collections;
using UnityEngine;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// An interactable wardrobe the player can hide inside.
    /// Composites with a <see cref="HidingSpot"/> on the same GameObject for
    /// occupancy tracking and concealment data.
    ///
    /// Interaction behaviour:
    ///   Enter — opens door, waits for the door animation, snaps player inside, closes door.
    ///   Exit  — opens door, waits for the door animation, releases player to exit offset, closes door.
    ///
    /// Assign an Animator with a bool parameter "IsOpen" for door animation.
    /// If no Animator is assigned the door skips the wait and the snap is instant.
    /// </summary>
    [RequireComponent(typeof(HidingSpot))]
    public class Wardrobe : InteractableBase
    {
        [Header("Door Animation")]
        [SerializeField]
        Animator _animator;
        [SerializeField, Min(0f)] float _doorAnimationDuration = 0.4f;

        static readonly int IsOpenParam = Animator.StringToHash("IsOpen");

        HidingSpot _hidingSpot;
        bool _isTransitioning;

        void Awake()
        {
            _hidingSpot = GetComponent<HidingSpot>();
        }

        /// <summary>False while a door open/close sequence is in progress.</summary>
        public override bool CanInteract => !_isTransitioning;

        protected override void OnInteracted(PlayerController interactor)
        {
            bool playerIsInsideThis = interactor.Hiding.IsHidden
                                   && interactor.Hiding.CurrentHidingSpot == _hidingSpot;

            if (playerIsInsideThis)
            {
                StartCoroutine(ExitSequence(interactor));
            }
            else if (!_hidingSpot.IsOccupied)
            {
                StartCoroutine(EnterSequence(interactor));
            }
        }

        IEnumerator EnterSequence(PlayerController interactor)
        {
            _isTransitioning = true;
            SetDoorOpen(true);

            if (_animator != null)
                yield return WaitFor.Seconds(_doorAnimationDuration);

            if (_hidingSpot.TryEnter(interactor.Hiding))
                interactor.Hiding.EnterHidingSpot(_hidingSpot);

            SetDoorOpen(false);
            _isTransitioning = false;
        }

        IEnumerator ExitSequence(PlayerController interactor)
        {
            _isTransitioning = true;
            SetDoorOpen(true);

            if (_animator != null)
                yield return WaitFor.Seconds(_doorAnimationDuration);

            interactor.Hiding.ExitHidingSpot();

            SetDoorOpen(false);
            _isTransitioning = false;
        }

        void SetDoorOpen(bool open)
        {
            if (_animator != null)
                _animator.SetBool(IsOpenParam, open);
        }
    }
}
