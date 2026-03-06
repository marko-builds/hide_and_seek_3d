using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Handles player locomotion. Reads events from PlayerInputHandler and drives a
    /// Rigidbody with camera-relative movement, smooth acceleration/deceleration,
    /// and rotation toward the direction of travel.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        // Inspector
        [SerializeField] PlayerData _data;
        [SerializeField] Camera _camera;   // Assign Main Camera (has CinemachineBrain)

        // Cached references (set in Awake)
        Rigidbody _rigidbody;
        PlayerInputHandler _input;

        // Runtime state — written by event handlers, read in FixedUpdate; no per-frame allocation
        Vector2 _moveInput;
        bool _isSprinting;
        bool _isCrouching;
        Vector3 _currentVelocity;   // XZ smoothed velocity; Y from gravity preserved separately

        const float MovingThreshold = 0.001f;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Returns true when the player is in the sprint stance.</summary>
        public bool IsSprinting => _isSprinting;

        /// <summary>Returns true when the player is in the crouch stance.</summary>
        public bool IsCrouching => _isCrouching;

        /// <summary>Returns true when the character has meaningful horizontal velocity.</summary>
        public bool IsMoving => _currentVelocity.sqrMagnitude > MovingThreshold;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputHandler>();
        }

        void OnEnable()
        {
            _input.OnMove += HandleMove;
            _input.OnSprintStarted += HandleSprintStarted;
            _input.OnSprintCancelled += HandleSprintCancelled;
            _input.OnCrouchStarted += HandleCrouchStarted;
            _input.OnCrouchCancelled += HandleCrouchCancelled;
        }

        void OnDisable()
        {
            _input.OnMove -= HandleMove;
            _input.OnSprintStarted -= HandleSprintStarted;
            _input.OnSprintCancelled -= HandleSprintCancelled;
            _input.OnCrouchStarted -= HandleCrouchStarted;
            _input.OnCrouchCancelled -= HandleCrouchCancelled;
        }

        void FixedUpdate()
        {
            Vector3 desiredVelocity = ComputeDesiredVelocity();
            SmoothVelocity(desiredVelocity);
            ApplyVelocityToRigidbody();
            RotateTowardMovementDirection();
        }

        // ── Private Methods ───────────────────────────────────────────────────────

        Vector3 ComputeDesiredVelocity()
        {
            if (_moveInput.sqrMagnitude < MovingThreshold)
                return Vector3.zero;

            Vector3 camForward = _camera.transform.forward;
            Vector3 camRight = _camera.transform.right;

            // Flatten and re-normalize so a pitched-down camera doesn't inject Y into movement
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // Guard against degenerate camera orientation (e.g. looking straight down)
            if (camForward.sqrMagnitude < MovingThreshold)
                camForward = Vector3.forward;

            Vector3 direction = (camRight * _moveInput.x + camForward * _moveInput.y).normalized;

            float speed = _isCrouching ? _data.crouchSpeed
                        : _isSprinting ? _data.sprintSpeed
                        : _data.walkSpeed;

            return direction * speed;
        }

        void SmoothVelocity(Vector3 desiredVelocity)
        {
            // Accelerate when gaining speed, decelerate when losing it
            float rate = desiredVelocity.sqrMagnitude > _currentVelocity.sqrMagnitude
                ? _data.acceleration
                : _data.deceleration;

            // MoveTowards gives linear feel and reaches target in finite time at a designer-tunable m/s²
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, rate * Time.fixedDeltaTime);
        }

        void ApplyVelocityToRigidbody()
        {
            // Preserve Y so gravity accumulates normally; only drive XZ from locomotion
            float yVelocity = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = new Vector3(_currentVelocity.x, yVelocity, _currentVelocity.z);
        }

        void RotateTowardMovementDirection()
        {
            // Early-out prevents LookRotation(zero) error and snap-to-forward when stopping
            if (_currentVelocity.sqrMagnitude < MovingThreshold)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(
                new Vector3(_currentVelocity.x, 0f, _currentVelocity.z), Vector3.up);

            // RotateTowards takes degrees/second, mapping directly to turnSpeed
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, _data.turnSpeed * Time.fixedDeltaTime);
        }

        // ── Input Event Handlers ──────────────────────────────────────────────────

        void HandleMove(Vector2 input) => _moveInput = input;

        void HandleSprintStarted()
        {
            if (_isCrouching) return;   // Crouch takes priority; can't sprint while crouching
            _isSprinting = true;
        }

        void HandleSprintCancelled() => _isSprinting = false;
        void HandleCrouchStarted()   => _isCrouching = true;
        void HandleCrouchCancelled() => _isCrouching = false;
    }
}
