using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Game-over (lose) screen. Shown when GameManager transitions to Lose state.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        private void OnEnable() => GameManager.Instance.OnGameStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.Instance.OnGameStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameManager.GameState state)
        {
            gameObject.SetActive(state == GameManager.GameState.Lose);
        }

        // TODO: wire Retry, Main Menu buttons
    }
}
