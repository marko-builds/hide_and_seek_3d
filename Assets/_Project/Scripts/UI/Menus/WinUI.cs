using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Win screen panel. Subscribes to the static GameManager.OnWin event so it
    /// can react even if Awake order puts it ahead of GameManager instantiation.
    /// Uses a CanvasGroup for alpha-based show/hide (no SetActive calls).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class WinUI : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()  => GameManager.OnWin += Show;
        private void OnDisable() => GameManager.OnWin -= Show;

        private void Show() => SetVisible(true);

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        // TODO: wire Next Level, Main Menu buttons
    }
}
