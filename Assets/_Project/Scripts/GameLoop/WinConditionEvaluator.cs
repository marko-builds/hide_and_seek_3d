using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Listens to game events and triggers a win via GameManager when conditions are met.
    /// Currently: player wins by surviving until the round timer expires.
    /// </summary>
    public class WinConditionEvaluator : MonoBehaviour
    {
        [SerializeField] private RoundTimer _roundTimer;

        private void OnEnable() => _roundTimer.OnTimerExpired += HandleTimerExpired;
        private void OnDisable() => _roundTimer.OnTimerExpired -= HandleTimerExpired;

        private void HandleTimerExpired() => GameManager.Instance.TriggerWin();
    }
}
