using TMPro;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Reacts to RoundTimer.OnTimerTick and updates the countdown text display.
    /// </summary>
    public class TimerUI : MonoBehaviour
    {
        [SerializeField] RoundTimer _roundTimer;
        [SerializeField] TMP_Text _timerText;

        void OnEnable() => _roundTimer.OnTimerTick += HandleTimerTick;
        void OnDisable() => _roundTimer.OnTimerTick -= HandleTimerTick;

        void HandleTimerTick(float remaining)
        {
            int minutes = (int)(remaining / 60f);
            int seconds = (int)(remaining % 60f);
            _timerText.SetText("{0}:{1:00}", minutes, seconds);
        }
    }
}
