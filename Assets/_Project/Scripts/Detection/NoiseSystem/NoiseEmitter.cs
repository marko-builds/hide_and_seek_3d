using System;

namespace HideAndSeek
{
    /// <summary>
    /// Static event bus for noise events. Any NoiseListener in the scene can subscribe
    /// regardless of object hierarchy. Has no instance state and requires no GameObject.
    /// </summary>
    public static class NoiseEmitter
    {
        public static event Action<NoiseEvent> OnNoiseEmitted;

        /// <summary>Broadcast a noise event to all active listeners.</summary>
        public static void Emit(NoiseEvent noiseEvent)
        {
            OnNoiseEmitted?.Invoke(noiseEvent);
        }
    }
}
