using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy stands still at spawn. Transitions to PatrolState after idleWaitDuration.
    /// Escalates immediately on stimuli.
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
            SeekState state = _enemy.Detection.SuspicionMeter.State;

            if (state >= SeekState.Chase)
            {
                _enemy.ChangeState(new EnemyChaseState(_enemy));
                return;
            }

            if (state >= SeekState.Alert)
            {
                _enemy.ChangeState(new EnemyAlertState(_enemy));
                return;
            }

            if (_enemy.Detection.ConsumePendingNoise(out _))
            {
                _enemy.ChangeState(new EnemyAlertState(_enemy));
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                _enemy.ChangeState(new EnemyPatrolState(_enemy));
        }
    }
}
