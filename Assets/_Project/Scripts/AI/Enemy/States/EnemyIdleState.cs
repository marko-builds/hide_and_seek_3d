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
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                _enemy.ChangeState(new EnemyPatrolState(_enemy));
        }
    }
}
