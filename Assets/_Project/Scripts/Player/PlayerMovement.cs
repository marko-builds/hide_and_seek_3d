using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Handles player locomotion. Reads events from PlayerInputHandler
    /// and drives a CharacterController using speeds from PlayerData.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private PlayerData _data;

        private CharacterController _controller;
        private PlayerInputHandler _input;

        private Vector2 _moveInput;
        private bool _isSprinting;
        private bool _isCrouching;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputHandler>();
        }

        private void OnEnable()
        {
            _input.OnMove += HandleMove;
            _input.OnSprintStarted += HandleSprintStarted;
            _input.OnSprintCancelled += HandleSprintCancelled;
            _input.OnCrouchStarted += HandleCrouchStarted;
            _input.OnCrouchCancelled += HandleCrouchCancelled;
        }

        private void OnDisable()
        {
            _input.OnMove -= HandleMove;
            _input.OnSprintStarted -= HandleSprintStarted;
            _input.OnSprintCancelled -= HandleSprintCancelled;
            _input.OnCrouchStarted -= HandleCrouchStarted;
            _input.OnCrouchCancelled -= HandleCrouchCancelled;
        }

        private void Update()
        {
            float speed = _isCrouching ? _data.crouchSpeed
                        : _isSprinting ? _data.sprintSpeed
                        : _data.walkSpeed;

            Vector3 move = new Vector3(_moveInput.x, 0f, _moveInput.y) * speed;
            _controller.SimpleMove(move);
        }

        private void HandleMove(Vector2 input) => _moveInput = input;
        private void HandleSprintStarted() => _isSprinting = true;
        private void HandleSprintCancelled() => _isSprinting = false;
        private void HandleCrouchStarted() => _isCrouching = true;
        private void HandleCrouchCancelled() => _isCrouching = false;
    }
}
