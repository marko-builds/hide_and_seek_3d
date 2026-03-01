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

        // TODO: inject waypoint list via constructor or ScriptableObject
        private int _waypointIndex;

        public EnemyPatrolState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.patrolSpeed);
            // TODO: set first waypoint destination
        }

        public override void Tick()
        {
            // TODO: advance to next waypoint on arrival
            // TODO: check suspicion; transition to Investigate if threshold crossed
            // TODO: transition to Chase if fully suspicious
        }
    }
}
