using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy pursues the player at chase speed (GDD §3.5).
    ///
    /// NavMesh destination is updated every chaseNavUpdateInterval seconds to limit
    /// path recalculation cost (GDD AC-14).
    ///
    /// The catch is handled by SuspicionMeter.OnPlayerCaught via the catchDwellTime
    /// check — this state does not perform a separate distance test.
    ///
    /// Transitions:
    ///   suspicion drops below Chase threshold → EnemySearchState (via SuspicionMeter hysteresis)
    ///   suspicion >= Searching but < Chase    → EnemySearchState
    /// </summary>
    public class EnemyChaseState : BaseState
    {
        private readonly EnemyController _enemy;
        private float _navUpdateTimer;

        public EnemyChaseState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            float speed = _enemy.Data.patrolSpeed
                          * _enemy.Data.chaseSpeedMultiplier
                          * _enemy.Phase2SpeedMultiplier;
            _enemy.Navigation.SetSpeed(speed);
            _navUpdateTimer = 0f;

            // Immediately move toward last known position on enter
            _enemy.Navigation.SetDestination(_enemy.Detection.LastKnownPlayerPosition);
        }

        public override void Tick()
        {
            SeekState state = _enemy.Detection.SuspicionMeter.State;

            // SuspicionMeter hysteresis has dropped us out of Chase
            if (state < SeekState.Chase)
            {
                _enemy.ChangeState(new EnemySearchState(_enemy, _enemy.Detection.LastKnownPlayerPosition));
                return;
            }

            // Throttled nav update
            _navUpdateTimer -= Time.deltaTime;
            if (_navUpdateTimer <= 0f)
            {
                _navUpdateTimer = _enemy.Data.chaseNavUpdateInterval;
                _enemy.Navigation.SetDestination(_enemy.Detection.LastKnownPlayerPosition);
            }
        }
    }
}
