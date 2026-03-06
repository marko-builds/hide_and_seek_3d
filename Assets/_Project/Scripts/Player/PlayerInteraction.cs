using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Detects nearby IInteractable objects via OverlapSphere and calls Interact()
    /// when the player presses the interact button.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] float _interactRadius = 1.5f;
        [SerializeField] LayerMask _interactableMask;

        PlayerInputHandler _input;
        PlayerController _controller;

        // Pre-allocated result buffer — avoids per-frame allocation
        readonly Collider[] _hitBuffer = new Collider[8];

        void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _controller = GetComponent<PlayerController>();
        }

        void OnEnable() => _input.OnInteract += HandleInteract;
        void OnDisable() => _input.OnInteract -= HandleInteract;

        void HandleInteract()
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
