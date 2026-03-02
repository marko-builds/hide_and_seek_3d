using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy pursues the player at full speed. Triggers the lose condition on catch.
    /// Transitions to SearchState if the player leaves sight for too long.
    /// </summary>
    public class EnemyChaseState : BaseState
    {
        private readonly EnemyController _enemy;
        private float _lostSightTimer;

        public EnemyChaseState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.chaseSpeed);
            _lostSightTimer = 0f;
        }

        public override void Tick()
        {
            if (_enemy.Detection.PlayerVisible)
            {
                _lostSightTimer = 0f;
                _enemy.Navigation.SetDestination(_enemy.Detection.LastKnownPlayerPosition);

                float dist = Vector3.Distance(
                    _enemy.transform.position,
                    _enemy.Detection.Player.position);

                if (dist <= _enemy.Data.catchDistance)
                {
                    _enemy.NotifyPlayerCaught();
                    return;
                }
            }
            else
            {
                _lostSightTimer += Time.deltaTime;
                if (_lostSightTimer >= _enemy.Data.lostSightDuration)
                    _enemy.ChangeState(new EnemySearchState(_enemy, _enemy.Detection.LastKnownPlayerPosition));
            }
        }
    }
}
