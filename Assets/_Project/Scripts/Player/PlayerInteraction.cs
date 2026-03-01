using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Detects nearby IInteractable objects via OverlapSphere and calls Interact()
    /// when the player presses the interact button.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private float _interactRadius = 1.5f;
        [SerializeField] private LayerMask _interactableMask;

        private PlayerInputHandler _input;
        private PlayerController _controller;

        // Pre-allocated result buffer — avoids per-frame allocation
        private readonly Collider[] _hitBuffer = new Collider[8];

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _controller = GetComponent<PlayerController>();
        }

        private void OnEnable() => _input.OnInteract += HandleInteract;
        private void OnDisable() => _input.OnInteract -= HandleInteract;

        private void HandleInteract()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, _interactRadius, _hitBuffer, _interactableMask);
            for (int i = 0; i < count; i++)
            {
                if (_hitBuffer[i].TryGetComponent<IInteractable>(out var interactable) && interactable.CanInteract)
                {
                    interactable.Interact(_controller);
                    break;
                }
            }
        }
    }
}
