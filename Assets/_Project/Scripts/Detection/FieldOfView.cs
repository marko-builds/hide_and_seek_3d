using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Computes whether a target falls within the enemy's field-of-view cone.
    /// Optionally renders the view cone mesh for debug/visual purposes.
    /// </summary>
    public class FieldOfView : MonoBehaviour
    {
        [SerializeField] EnemyData _data;

        /// <summary>
        /// Returns true if <paramref name="target"/> is within the FOV cone and within detection range.
        /// Does NOT check line-of-sight — combine with LineOfSightChecker for full detection.
        /// </summary>
        public bool IsInFieldOfView(Vector3 target)
        {
            Vector3 directionToTarget = (target - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > _data.fieldOfViewAngle * 0.5f) return false;

            float distance = Vector3.Distance(transform.position, target);
            return distance <= _data.detectionRange;
        }

        /// <summary>
        /// Returns true if <paramref name="target"/> is within the full detection volume:
        /// within detection range, and either within the FOV cone or inside the close-detection
        /// override radius (GDD Rule V-3 — close-range skips the FOV angle check).
        /// Does NOT check line-of-sight.
        /// </summary>
        public bool IsInDetectionVolume(Vector3 target)
        {
            float distance = Vector3.Distance(transform.position, target);
            if (distance > _data.detectionRange) return false;
            if (distance <= _data.closeDetectionRange) return true; // GDD Rule V-3

            Vector3 directionToTarget = (target - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            return angle <= _data.fieldOfViewAngle * 0.5f;
        }
    }
}
