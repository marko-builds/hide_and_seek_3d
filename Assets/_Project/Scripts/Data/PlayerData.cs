using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "HideAndSeek/Data/Player Data")]
    public class PlayerData : ScriptableObject
    {
        [Header("Movement")]
        public float walkSpeed = 3f;
        public float sprintSpeed = 6f;
        public float crouchSpeed = 1.5f;

        [Header("Noise Multipliers")]
        public float walkNoiseMultiplier = 0.5f;
        public float sprintNoiseMultiplier = 1f;
        public float crouchNoiseMultiplier = 0.1f;
        public float idleNoiseMultiplier = 0f;
    }
}
