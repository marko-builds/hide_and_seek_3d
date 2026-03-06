using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy moves to the last known noise/sight position and looks around.
    /// Transitions to Chase if the player is spotted, or back to Patrol on timeout.
    /// </summary>
    public class EnemyInvestigateState : BaseState
    {
        readonly EnemyController _enemy;
        readonly Vector3 _targetPosition;
        float _lookTimer;
        bool _arrived;

        public EnemyInvestigateState(EnemyController enemy, Vector3 targetPosition)
        {
            _enemy = enemy;
            _targetPosition = targetPosition;
        }

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.investigateSpeed);
            _enemy.Navigation.SetDestination(_targetPosition);
            _lookTimer = _enemy.Data.investigationLookDuration;
            _arrived = false;
        }

        public override void Tick()
        {
            if (_enemy.Detection.SuspicionMeter.State >= SeekState.Chase)
            {
                _enemy.ChangeState(new EnemyChaseState(_enemy));
                return;
            }

            if (!_arrived)
            {
                if (_enemy.Navigation.IsAtDestination)
                    _arrived = true;
                return;
            }

            _lookTimer -= Time.deltaTime;
            if (_lookTimer <= 0f)
            {
                _enemy.Detection.SuspicionMeter.Reset();
                _enemy.ChangeState(new EnemyPatrolState(_enemy));
            }
        }
    }
}
