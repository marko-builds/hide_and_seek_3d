using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Tracks per-enemy suspicion in [0, 1]. Raises events that drive UI and state transitions.
    /// </summary>
    public class SuspicionMeter : MonoBehaviour
    {
        public event Action<float> OnSuspicionChanged;
        public event Action OnFullySuspicious;
        public event Action OnSuspicionCleared;

        [SerializeField] private EnemyData _data;

        private float _suspicion;

        public float Suspicion => _suspicion;

        public void Increase(float delta)
        {
            float prev = _suspicion;
            _suspicion = Mathf.Clamp01(_suspicion + delta);
            if (!Mathf.Approximately(_suspicion, prev))
                OnSuspicionChanged?.Invoke(_suspicion);
            if (_suspicion >= 1f)
                OnFullySuspicious?.Invoke();
        }

        public void Decrease(float delta)
        {
            float prev = _suspicion;
            _suspicion = Mathf.Clamp01(_suspicion - delta);
            if (!Mathf.Approximately(_suspicion, prev))
                OnSuspicionChanged?.Invoke(_suspicion);
            if (_suspicion <= 0f && prev > 0f)
                OnSuspicionCleared?.Invoke();
        }

        public void Reset()
        {
            _suspicion = 0f;
            OnSuspicionChanged?.Invoke(_suspicion);
            OnSuspicionCleared?.Invoke();
        }
    }
}
