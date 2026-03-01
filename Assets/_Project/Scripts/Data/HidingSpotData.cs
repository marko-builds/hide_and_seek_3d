using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "HidingSpotData", menuName = "HideAndSeek/Data/Hiding Spot Data")]
    public class HidingSpotData : ScriptableObject
    {
        [Range(0f, 1f)]
        [Tooltip("0 = fully concealed, 1 = no concealment.")]
        public float concealmentModifier = 0.1f;

        public Vector3 exitOffset = new Vector3(1f, 0f, 0f);
    }
}
