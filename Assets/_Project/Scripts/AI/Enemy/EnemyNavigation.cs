using UnityEngine;
using UnityEngine.AI;

namespace HideAndSeek
{
    /// <summary>
    /// Thin wrapper around NavMeshAgent. Enemy states drive movement through this
    /// component rather than accessing NavMeshAgent directly.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyNavigation : MonoBehaviour
    {
        NavMeshAgent _agent;

        public bool IsAtDestination => !_agent.pathPending
            && _agent.hasPath
            && _agent.remainingDistance <= _agent.stoppingDistance;

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public void SetDestination(Vector3 destination) => _agent.SetDestination(destination);

        public void SetSpeed(float speed) => _agent.speed = speed;

        public void Stop()
        {
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        /// <summary>Returns true if this transform is within <paramref name="threshold"/> metres of <paramref name="position"/>.</summary>
        public bool IsNear(Vector3 position, float threshold)
            => Vector3.Distance(transform.position, position) <= threshold;

        /// <summary>Rotates toward <paramref name="target"/> at <paramref name="degreesPerSecond"/> this frame. Returns true when facing within 1°.</summary>
        public bool RotateToward(Vector3 target, float degreesPerSecond)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return true;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, degreesPerSecond * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, targetRot) < 1f;
        }
    }
}
