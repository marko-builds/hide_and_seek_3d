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
        private NavMeshAgent _agent;

        public bool IsAtDestination => !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;

        private void Awake() => _agent = GetComponent<NavMeshAgent>();

        public void SetDestination(Vector3 destination) => _agent.SetDestination(destination);

        public void SetSpeed(float speed) => _agent.speed = speed;

        public void Stop()
        {
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }
    }
}
