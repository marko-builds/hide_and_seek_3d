using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Subscribes to the NoiseEmitter event bus, filters by radius, and notifies
    /// the owning EnemyDetection component when a relevant noise is heard.
    /// </summary>
    public class NoiseListener : MonoBehaviour
    {
        [SerializeField] private float _hearingRadius = 10f;

        public event Action<NoiseEvent> OnNoiseHeard;

        private void OnEnable() => NoiseEmitter.OnNoiseEmitted += HandleNoise;
        private void OnDisable() => NoiseEmitter.OnNoiseEmitted -= HandleNoise;

        private void HandleNoise(NoiseEvent noiseEvent)
        {
            float distance = Vector3.Distance(transform.position, noiseEvent.WorldPosition);
            if (distance <= _hearingRadius)
                OnNoiseHeard?.Invoke(noiseEvent);
        }
    }
}
