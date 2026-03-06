using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Per-objective-token configuration: display name, icon, and feedback assets.
    /// Assign to ObjectiveToken in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "ObjectiveData", menuName = "HideAndSeek/Data/Objective Data")]
    public class ObjectiveData : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("Short label shown in the HUD objective counter.")]
        public string displayName = "Token";

        [Tooltip("Icon shown in the HUD for this objective type.")]
        public Sprite objectiveIconSprite;

        [Header("Feedback")]
        [Tooltip("Particle effect spawned at the token when collected. Optional.")]
        public GameObject collectionVFXPrefab;

        [Tooltip("Sound played when this token is collected.")]
        public SoundID collectionSFXKey = SoundID.ObjectiveCollect;
    }
}
