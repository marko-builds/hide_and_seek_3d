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
    }
}
