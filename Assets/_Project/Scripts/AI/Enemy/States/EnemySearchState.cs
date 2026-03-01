using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy sweeps the area after losing sight of the player.
    /// Returns to PatrolState after the search timeout elapses.
    /// </summary>
    public class EnemySearchState : BaseState
    {
        private readonly EnemyController _enemy;
        private float _timer;

        public EnemySearchState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _timer = _enemy.Data.searchTimeout;
            _enemy.Navigation.SetSpeed(_enemy.Data.investigateSpeed);
            // TODO: pick random sweep positions around last known location
        }

        public override void Tick()
        {
            _timer -= Time.deltaTime;
            // TODO: move between sweep positions
            // TODO: transition to Chase if player re-detected
            if (_timer <= 0f)
                _enemy.ChangeState(new EnemyPatrolState(_enemy));
        }
    }
}
