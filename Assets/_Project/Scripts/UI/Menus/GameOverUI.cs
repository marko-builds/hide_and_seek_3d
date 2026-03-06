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
        CanvasGroup _canvasGroup;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        void OnEnable()  => GameManager.OnLose += Show;
        void OnDisable() => GameManager.OnLose -= Show;

        void Show() => SetVisible(true);

        void SetVisible(bool visible)
        {
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        // TODO: wire Retry, Main Menu buttons
    }
}
