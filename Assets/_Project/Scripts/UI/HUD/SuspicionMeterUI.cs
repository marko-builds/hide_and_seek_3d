using UnityEngine;
using UnityEngine.UI;

namespace HideAndSeek
{
    /// <summary>
    /// Reacts to SuspicionMeter.OnSuspicionChanged and fills a UI slider/image.
    /// </summary>
    public class SuspicionMeterUI : MonoBehaviour
    {
        [SerializeField] SuspicionMeter _suspicionMeter;
        [SerializeField] Slider _slider;

        void OnEnable() => _suspicionMeter.OnSuspicionChanged += HandleSuspicionChanged;
        void OnDisable() => _suspicionMeter.OnSuspicionChanged -= HandleSuspicionChanged;

        void HandleSuspicionChanged(float value) => _slider.value = value;
    }
}
