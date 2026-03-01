using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy pursues the player at full speed. Triggers the lose condition on catch.
    /// Transitions to SearchState if the player leaves sight.
    /// </summary>
    public class EnemyChaseState : BaseState
    {
        private readonly EnemyController _enemy;

        public EnemyChaseState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.chaseSpeed);
        }

        public override void Tick()
        {
            // TODO: update destination to player position each frame
            // TODO: check catch distance; raise OnPlayerCaught event
            // TODO: transition to Search if player lost from FOV
        }
    }
}
