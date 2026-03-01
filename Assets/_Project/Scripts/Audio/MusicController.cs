using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Adaptive music controller. Reacts to SuspicionMeter events to layer tension
    /// tracks on top of the ambient music.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        [SerializeField] private SuspicionMeter _suspicionMeter;
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioSource _tensionSource;

        private void OnEnable() => _suspicionMeter.OnSuspicionChanged += HandleSuspicionChanged;
        private void OnDisable() => _suspicionMeter.OnSuspicionChanged -= HandleSuspicionChanged;

        private void HandleSuspicionChanged(float value)
        {
            _tensionSource.volume = value;
        }
    }
}
