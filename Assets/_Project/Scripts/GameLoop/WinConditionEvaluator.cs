using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Listens to game events and triggers a win via GameManager when conditions are met.
    /// Currently: player wins by surviving until the round timer expires.
    /// </summary>
    public class WinConditionEvaluator : MonoBehaviour
    {
        [SerializeField] RoundTimer _roundTimer;

        void OnEnable() => _roundTimer.OnTimerExpired += HandleTimerExpired;
        void OnDisable() => _roundTimer.OnTimerExpired -= HandleTimerExpired;

        void HandleTimerExpired() => GameManager.Instance.TriggerWin();
    }
}
