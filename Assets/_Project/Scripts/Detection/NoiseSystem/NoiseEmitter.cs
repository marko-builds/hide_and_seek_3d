using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// MonoBehaviour that broadcasts NoiseEvents via a static event bus.
    /// Any NoiseListener in the scene can subscribe regardless of object hierarchy.
    /// </summary>
    public class NoiseEmitter : MonoBehaviour
    {
        public static event Action<NoiseEvent> OnNoiseEmitted;

        /// <summary>Broadcast a noise event to all active listeners.</summary>
        public static void Emit(NoiseEvent noiseEvent)
        {
            OnNoiseEmitted?.Invoke(noiseEvent);
        }
    }
}
