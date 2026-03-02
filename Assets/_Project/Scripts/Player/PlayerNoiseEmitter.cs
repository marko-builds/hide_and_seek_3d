using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// INoiseMaker implementation for the player. Derives noise level from
    /// current movement stance and feeds it to the NoiseSystem each frame.
    /// </summary>
    public class PlayerNoiseEmitter : MonoBehaviour, INoiseMaker
    {
        [SerializeField] private PlayerData _data;

        public float CurrentNoiseLevel { get; private set; }
        public string NoiseTag => "Player";

        private PlayerMovement _movement;

        private void Awake() => _movement = GetComponent<PlayerMovement>();

        private void Update()
        {
            float multiplier = _movement.IsSprinting ? _data.sprintNoiseMultiplier
                             : _movement.IsCrouching ? _data.crouchNoiseMultiplier
                             : _data.walkNoiseMultiplier;

            CurrentNoiseLevel = _movement.IsMoving ? multiplier : 0f;
            if (_movement.IsMoving)
                NoiseEmitter.Emit(new NoiseEvent(transform.position, CurrentNoiseLevel, NoiseTag));
        }
    }
}
