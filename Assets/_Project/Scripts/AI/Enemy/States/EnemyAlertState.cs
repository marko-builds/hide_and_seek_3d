using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy stops, faces the last known player position, and scans for alertScanDuration
    /// before escalating to SearchState or returning to Patrol (GDD §3.3).
    ///
    /// Immediately escalates to ChaseState if suspicion hits Chase threshold while scanning.
    /// Skips the scan pause when EnemyController.Phase2SkipAlertScan is true.
    /// </summary>
    public class EnemyAlertState : BaseState
    {
        private readonly EnemyController _enemy;
        private float _scanTimer;
        private bool _facingComplete;

        public EnemyAlertState(EnemyController enemy) => _enemy = enemy;

        public override void Enter()
        {
            _enemy.Navigation.Stop();
            _scanTimer = _enemy.Phase2SkipAlertScan ? 0f : _enemy.Data.alertScanDuration;
            _facingComplete = false;
        }

        public override void Tick()
        {
            SeekState state = _enemy.Detection.SuspicionMeter.State;

            // Always escalate if suspicion reaches Chase
            if (state >= SeekState.Chase)
            {
                _enemy.ChangeState(new EnemyChaseState(_enemy));
                return;
            }

            // Fallen back below Alert threshold — return to patrol
            if (state < SeekState.Alert)
            {
                _enemy.ChangeState(new EnemyPatrolState(_enemy, resumeAtNearest: true));
                return;
            }

            // Phase 1: rotate to face LKP
            if (!_facingComplete)
            {
                _facingComplete = _enemy.Navigation.RotateToward(
                    _enemy.Detection.LastKnownPlayerPosition,
                    _enemy.Data.alertTurnSpeed);
                return;
            }

            // Phase 2: hold scan (or skip if Phase2SkipAlertScan)
            if (_scanTimer > 0f)
            {
                _scanTimer -= Time.deltaTime;
                return;
            }

            // Scan complete — escalate to Search
            _enemy.ChangeState(new EnemySearchState(_enemy, _enemy.Detection.LastKnownPlayerPosition));
        }
    }
}
