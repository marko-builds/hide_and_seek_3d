using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy stands still and waits. Transitions to PatrolState after the idle timer elapses.
    /// </summary>
    public class EnemyIdleState : BaseState
    {
        private readonly EnemyController _enemy;
        private float _timer;

        public EnemyIdleState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _timer = _enemy.Data.idleWaitDuration;
            _enemy.Navigation.Stop();
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

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                _enemy.ChangeState(new EnemyPatrolState(_enemy));
        }
    }
}
