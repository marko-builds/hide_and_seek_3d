using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Holds per-entity modifiers that affect how easily it is detected by enemies.
    /// Referenced by IDetectable; owned by PlayerController.
    /// </summary>
    [System.Serializable]
    public class DetectionProfile
    {
        [Range(0f, 1f)]
        [Tooltip("1 = fully visible, 0 = invisible to sight-based detection.")]
        public float visibilityModifier = 1f;

        [Range(0f, 1f)]
        [Tooltip("1 = fully audible, 0 = silent.")]
        public float noiseModifier = 1f;
    }
}
