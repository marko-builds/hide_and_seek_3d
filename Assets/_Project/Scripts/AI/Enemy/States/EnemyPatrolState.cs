using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy walks a waypoint loop. Transitions to InvestigateState when noise or
    /// partial sight raises suspicion above a threshold.
    /// </summary>
    public class EnemyPatrolState : BaseState
    {
        private readonly EnemyController _enemy;
        private int _waypointIndex;

        public EnemyPatrolState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.patrolSpeed);
            GoToCurrentWaypoint();
        }

        public override void Tick()
        {
            float suspicion = _enemy.Detection.SuspicionMeter.Suspicion;

            if (suspicion >= 1f)
            {
                _enemy.ChangeState(new EnemyChaseState(_enemy));
                return;
            }

            if (suspicion >= _enemy.Data.investigateSuspicionThreshold)
            {
                _enemy.ChangeState(new EnemyInvestigateState(_enemy, _enemy.Detection.LastKnownPlayerPosition));
                return;
            }

            if (_enemy.Detection.ConsumePendingNoise(out Vector3 noisePos))
            {
                _enemy.ChangeState(new EnemyInvestigateState(_enemy, noisePos));
                return;
            }

            if (!HasWaypoints() || !_enemy.Navigation.IsAtDestination) return;
            _waypointIndex = (_waypointIndex + 1) % _enemy.Waypoints.Length;
            
            GoToCurrentWaypoint();
        }

        private void GoToCurrentWaypoint()
        {
            if (HasWaypoints())
                _enemy.Navigation.SetDestination(_enemy.Waypoints[_waypointIndex].position);
        }

        private bool HasWaypoints() => _enemy.Waypoints is { Length: > 0 };
    }
}
