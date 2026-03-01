using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Static helper for performing line-of-sight raycasts.
    /// Used by EnemyDetection and any other system that needs obstruction checks.
    /// </summary>
    public static class LineOfSightChecker
    {
        /// <summary>
        /// Returns true if <paramref name="origin"/> has unobstructed sight to <paramref name="target"/>.
        /// </summary>
        public static bool HasLineOfSight(Vector3 origin, Vector3 target, LayerMask obstructionMask)
        {
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            return !Physics.Raycast(origin, direction.normalized, distance, obstructionMask);
        }
    }
}
