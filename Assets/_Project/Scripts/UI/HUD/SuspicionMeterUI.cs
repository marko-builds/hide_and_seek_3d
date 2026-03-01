using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek
{
    /// <summary>
    /// Reacts to SuspicionMeter.OnSuspicionChanged and fills a UI slider/image.
    /// </summary>
    public class SuspicionMeterUI : MonoBehaviour
    {
        [SerializeField] private SuspicionMeter _suspicionMeter;
        [SerializeField] private Slider _slider;

        private void OnEnable() => _suspicionMeter.OnSuspicionChanged += HandleSuspicionChanged;
        private void OnDisable() => _suspicionMeter.OnSuspicionChanged -= HandleSuspicionChanged;

        private void HandleSuspicionChanged(float value) => _slider.value = value;
    }
}
