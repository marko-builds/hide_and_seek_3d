using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "SoundData", menuName = "HideAndSeek/Data/Sound Data")]
    public class SoundData : ScriptableObject
    {
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0.5f, 2f)]
        public float pitch = 1f;

        [Range(0f, 0.5f)]
        public float volumeVariance = 0.1f;

        [Range(0f, 0.5f)]
        public float pitchVariance = 0.1f;
    }
}
