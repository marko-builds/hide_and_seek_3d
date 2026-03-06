using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Adaptive music controller. Reacts to SuspicionMeter events to layer tension
    /// tracks on top of the ambient music.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        [SerializeField] SuspicionMeter _suspicionMeter;
        [SerializeField] AudioSource _ambientSource;
        [SerializeField] AudioSource _tensionSource;

        void OnEnable() => _suspicionMeter.OnSuspicionChanged += HandleSuspicionChanged;
        void OnDisable() => _suspicionMeter.OnSuspicionChanged -= HandleSuspicionChanged;

        void HandleSuspicionChanged(float value)
        {
            _tensionSource.volume = value;
        }
    }
}
