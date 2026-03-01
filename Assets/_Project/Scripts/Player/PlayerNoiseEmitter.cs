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
            // TODO: read stance from PlayerMovement and compute noise level via PlayerData multipliers
            // NoiseEmitter.Emit(new NoiseEvent(transform.position, CurrentNoiseLevel, NoiseTag));
        }
    }
}
