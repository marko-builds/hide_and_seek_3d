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
        [SerializeField] float _hearingRadius = 10f;

        public event Action<NoiseEvent> OnNoiseHeard;

        void OnEnable() => NoiseEmitter.OnNoiseEmitted += HandleNoise;
        void OnDisable() => NoiseEmitter.OnNoiseEmitted -= HandleNoise;

        void HandleNoise(NoiseEvent noiseEvent)
        {
            float distance = Vector3.Distance(transform.position, noiseEvent.WorldPosition);
            if (distance <= _hearingRadius)
                OnNoiseHeard?.Invoke(noiseEvent);
        }
    }
}
