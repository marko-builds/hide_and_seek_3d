using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Top-level player coordinator. Holds references to all player subsystems.
    /// Contains no gameplay logic itself — delegates everything to subsystems.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerHiding))]
    [RequireComponent(typeof(PlayerInteraction))]
    [RequireComponent(typeof(PlayerNoiseEmitter))]
    public class PlayerController : MonoBehaviour, IHideable, IDetectable
    {
        [field: SerializeField] public PlayerInputHandler InputHandler { get; private set; }
        [field: SerializeField] public PlayerMovement Movement { get; private set; }
        [field: SerializeField] public PlayerHiding Hiding { get; private set; }
        [field: SerializeField] public PlayerInteraction Interaction { get; private set; }
        [field: SerializeField] public PlayerNoiseEmitter NoiseEmitter { get; private set; }

        // IHideable
        public bool IsHidden => Hiding.IsHidden;
        public HidingSpot CurrentHidingSpot => Hiding.CurrentHidingSpot;

        // IDetectable
        [SerializeField] private DetectionProfile _detectionProfile = new();
        public DetectionProfile DetectionProfile => _detectionProfile;
        public void OnDetected() { /* TODO: raise caught event */ }

        private void Awake()
        {
            InputHandler = GetComponent<PlayerInputHandler>();
            Movement = GetComponent<PlayerMovement>();
            Hiding = GetComponent<PlayerHiding>();
            Interaction = GetComponent<PlayerInteraction>();
            NoiseEmitter = GetComponent<PlayerNoiseEmitter>();
        }
    }
}
