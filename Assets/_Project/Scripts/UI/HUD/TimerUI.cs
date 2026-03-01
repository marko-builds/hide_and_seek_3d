using TMPro;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Reacts to RoundTimer.OnTimerTick and updates the countdown text display.
    /// </summary>
    public class TimerUI : MonoBehaviour
    {
        [SerializeField] private RoundTimer _roundTimer;
        [SerializeField] private TMP_Text _timerText;

        private void OnEnable() => _roundTimer.OnTimerTick += HandleTimerTick;
        private void OnDisable() => _roundTimer.OnTimerTick -= HandleTimerTick;

        private void HandleTimerTick(float remaining)
        {
            int minutes = (int)(remaining / 60f);
            int seconds = (int)(remaining % 60f);
            _timerText.SetText("{0}:{1:00}", minutes, seconds);
        }
    }
}
