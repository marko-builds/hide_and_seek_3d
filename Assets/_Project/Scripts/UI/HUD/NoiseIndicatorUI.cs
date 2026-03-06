using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Reacts to PlayerNoiseEmitter and scales/fades a noise ring UI element
    /// to give the player feedback on how much noise they are making.
    /// </summary>
    public class NoiseIndicatorUI : MonoBehaviour
    {
        [SerializeField] PlayerNoiseEmitter _noiseEmitter;
        [SerializeField] RectTransform _noiseRing;
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] float _maxScale = 2f;

        void Update()
        {
            float noise = _noiseEmitter.CurrentNoiseLevel;
            float scale = Mathf.Lerp(1f, _maxScale, noise);
            _noiseRing.localScale = new Vector3(scale, scale, 1f);
            _canvasGroup.alpha = noise;
        }
    }
}
