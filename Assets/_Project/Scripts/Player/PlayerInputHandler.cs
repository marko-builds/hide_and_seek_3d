using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HideAndSeek
{
    /// <summary>
    /// Wraps InputSystem_Actions and publishes typed C# events.
    /// All other player components consume these events; none touch InputSystem directly.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        public event Action<Vector2> OnMove;
        public event Action<Vector2> OnLook;
        public event Action OnSprintStarted;
        public event Action OnSprintCancelled;
        public event Action OnCrouchStarted;
        public event Action OnCrouchCancelled;
        public event Action OnJump;
        public event Action OnInteract;
        public event Action OnAttack;

        private InputSystem_Actions _actions;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Move.performed += OnMovePerformed;
            _actions.Player.Move.canceled += OnMoveCanceled;
            _actions.Player.Look.performed += OnLookPerformed;
            _actions.Player.Sprint.started += OnSprintStartedHandler;
            _actions.Player.Sprint.canceled += OnSprintCancelledHandler;
            _actions.Player.Crouch.started += OnCrouchStartedHandler;
            _actions.Player.Crouch.canceled += OnCrouchCancelledHandler;
            _actions.Player.Jump.performed += OnJumpPerformed;
            _actions.Player.Interact.performed += OnInteractPerformed;
            _actions.Player.Attack.performed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            _actions.Player.Move.performed -= OnMovePerformed;
            _actions.Player.Move.canceled -= OnMoveCanceled;
            _actions.Player.Look.performed -= OnLookPerformed;
            _actions.Player.Sprint.started -= OnSprintStartedHandler;
            _actions.Player.Sprint.canceled -= OnSprintCancelledHandler;
            _actions.Player.Crouch.started -= OnCrouchStartedHandler;
            _actions.Player.Crouch.canceled -= OnCrouchCancelledHandler;
            _actions.Player.Jump.performed -= OnJumpPerformed;
            _actions.Player.Interact.performed -= OnInteractPerformed;
            _actions.Player.Attack.performed -= OnAttackPerformed;
            _actions.Player.Disable();
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx) => OnMove?.Invoke(ctx.ReadValue<Vector2>());
        private void OnMoveCanceled(InputAction.CallbackContext ctx) => OnMove?.Invoke(Vector2.zero);
        private void OnLookPerformed(InputAction.CallbackContext ctx) => OnLook?.Invoke(ctx.ReadValue<Vector2>());
        private void OnSprintStartedHandler(InputAction.CallbackContext ctx) => OnSprintStarted?.Invoke();
        private void OnSprintCancelledHandler(InputAction.CallbackContext ctx) => OnSprintCancelled?.Invoke();
        private void OnCrouchStartedHandler(InputAction.CallbackContext ctx) => OnCrouchStarted?.Invoke();
        private void OnCrouchCancelledHandler(InputAction.CallbackContext ctx) => OnCrouchCancelled?.Invoke();
        private void OnJumpPerformed(InputAction.CallbackContext ctx) => OnJump?.Invoke();
        private void OnInteractPerformed(InputAction.CallbackContext ctx) => OnInteract?.Invoke();
        private void OnAttackPerformed(InputAction.CallbackContext ctx) => OnAttack?.Invoke();
    }
}
