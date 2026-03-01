using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Win screen. Shown when GameManager transitions to Win state.
    /// </summary>
    public class WinUI : MonoBehaviour
    {
        private void OnEnable() => GameManager.Instance.OnGameStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.Instance.OnGameStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameManager.GameState state)
        {
            gameObject.SetActive(state == GameManager.GameState.Win);
        }

        // TODO: wire Next Level, Main Menu buttons
    }
}
