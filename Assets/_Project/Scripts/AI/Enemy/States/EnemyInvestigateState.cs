using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy moves to the last known noise position and looks around.
    /// Transitions to Chase if the player is spotted, or back to Patrol on timeout.
    /// </summary>
    public class EnemyInvestigateState : BaseState
    {
        private readonly EnemyController _enemy;
        private readonly Vector3 _targetPosition;

        public EnemyInvestigateState(EnemyController enemy, Vector3 targetPosition)
        {
            _enemy = enemy;
            _targetPosition = targetPosition;
        }

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.investigateSpeed);
            _enemy.Navigation.SetDestination(_targetPosition);
        }

        public override void Tick()
        {
            // TODO: on arrival, look around (rotate through scan angles)
            // TODO: transition to Chase if player fully detected
            // TODO: transition to Patrol on search timeout
        }
    }
}
