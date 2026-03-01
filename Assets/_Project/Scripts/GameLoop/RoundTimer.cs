using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Counts down from GameRulesData.roundDuration. Raises tick and expiry events.
    /// </summary>
    public class RoundTimer : MonoBehaviour
    {
        [SerializeField] private GameRulesData _rules;

        public event Action<float> OnTimerTick;
        public event Action OnTimerExpired;

        public float Remaining { get; private set; }
        private bool _running;

        public void StartTimer()
        {
            Remaining = _rules.roundDuration;
            _running = true;
        }

        public void StopTimer() => _running = false;

        private void Update()
        {
            if (!_running) return;

            Remaining -= Time.deltaTime;
            OnTimerTick?.Invoke(Remaining);

            if (Remaining <= 0f)
            {
                _running = false;
                Remaining = 0f;
                OnTimerExpired?.Invoke();
            }
        }
    }
}
