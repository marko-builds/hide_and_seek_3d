using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Activates and deactivates HUD panels based on GameManager state changes.
    /// Reacts to events only — never drives gameplay logic.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [SerializeField] private GameObject _hudRoot;

        private void OnEnable() => GameManager.Instance.OnGameStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.Instance.OnGameStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameManager.GameState state)
        {
            _hudRoot.SetActive(state == GameManager.GameState.Playing);
        }
    }
}
