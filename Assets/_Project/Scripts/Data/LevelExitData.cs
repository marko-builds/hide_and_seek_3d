using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Configuration for a LevelExit interactable (GDD Level-Exit System).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelExitData", menuName = "HideAndSeek/Data/Level Exit Data")]
    public class LevelExitData : ScriptableObject
    {
        [Header("Interaction")]
        [Tooltip("Seconds the player must hold the Interact input to use the exit.")]
        [Range(0f, 5f)]
        public float exitHoldDuration = 0.8f;

        [Tooltip("Prompt shown in the HUD before the exit is unlocked.")]
        public string lockedPromptLabel = "Find all tokens first";

        [Tooltip("Prompt shown in the HUD once the exit is unlocked.")]
        public string unlockedPromptLabel = "Hold [E] to escape";

        [Header("Feedback")]
        [Tooltip("Sound played when the exit unlocks (all objectives collected).")]
        public SoundID unlockSFXKey = SoundID.ExitUnlocked;

        [Tooltip("Sound played when the player successfully uses the exit.")]
        public SoundID exitSFXKey = SoundID.ExitUsed;

        [Tooltip("VFX spawned on the exit when it unlocks. Optional.")]
        public GameObject unlockVFXPrefab;

        [Tooltip("VFX spawned when the player exits. Optional.")]
        public GameObject exitVFXPrefab;
    }
}
