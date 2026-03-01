using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Immutable value type representing a noise emission in the world.
    /// Passed through the static noise event bus.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 WorldPosition;
        public readonly float Intensity;
        public readonly string SourceTag;

        public NoiseEvent(Vector3 worldPosition, float intensity, string sourceTag)
        {
            WorldPosition = worldPosition;
            Intensity = intensity;
            SourceTag = sourceTag;
        }
    }
}
