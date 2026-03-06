using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Game-over (lose) screen panel. Subscribes to the static GameManager.OnLose event.
    /// Uses a CanvasGroup for alpha-based show/hide (no SetActive calls).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GameOverUI : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()  => GameManager.OnLose += Show;
        private void OnDisable() => GameManager.OnLose -= Show;

        private void Show() => SetVisible(true);

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        // TODO: wire Retry, Main Menu buttons
    }
}
