using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Enemy walks a waypoint loop at patrol speed.
    ///
    /// When constructed with <paramref name="resumeAtNearest"/> = true (after Search),
    /// starts at the waypoint closest to the seeker's current position (F-S1).
    ///
    /// Transitions:
    ///   suspicion >= Chase  → EnemyChaseState
    ///   suspicion >= Alert  → EnemyAlertState
    ///   noise received      → EnemyAlertState (toward noise position)
    /// </summary>
    public class EnemyPatrolState : BaseState
    {
        readonly EnemyController _enemy;
        readonly bool _resumeAtNearest;
        int _waypointIndex;
        float _dwellTimer;
        bool _dwelling;

        public EnemyPatrolState(EnemyController enemy, bool resumeAtNearest = false)
        {
            _enemy = enemy;
            _resumeAtNearest = resumeAtNearest;
        }

        public override void Enter()
        {
            _enemy.Navigation.SetSpeed(_enemy.Data.patrolSpeed * _enemy.Phase2SpeedMultiplier);
            _dwelling = false;
            _dwellTimer = 0f;

            if (!HasWaypoints()) return;

            _waypointIndex = _resumeAtNearest
                ? FindNearestWaypointIndex()
                : 0;

            GoToCurrentWaypoint();
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

            if (!HasWaypoints()) return;

            if (_dwelling)
            {
                _dwellTimer -= Time.deltaTime;
                if (_dwellTimer <= 0f)
                {
                    _dwelling = false;
                    _waypointIndex = (_waypointIndex + 1) % _enemy.Waypoints.Length;
                    GoToCurrentWaypoint();
                }
                return;
            }

            if (!_enemy.Navigation.IsNear(
                    _enemy.Waypoints[_waypointIndex].position,
                    _enemy.Data.waypointArrivalThreshold)) return;

            // Arrived — dwell then advance
            _dwelling = true;
            _dwellTimer = _enemy.Data.patrolDwellDuration;
        }

        void GoToCurrentWaypoint()
        {
            _enemy.Navigation.SetDestination(_enemy.Waypoints[_waypointIndex].position);
        }

        bool HasWaypoints()
        {
            if (_enemy.Waypoints is { Length: > 0 }) return true;
            Debug.LogWarning($"[EnemyPatrolState] {_enemy.name} has no patrol waypoints.", _enemy);
            return false;
        }

        /// <summary>Returns the index of the waypoint nearest to the seeker (F-S1).</summary>
        int FindNearestWaypointIndex()
        {
            var waypoints = _enemy.Waypoints;
            Vector3 pos = _enemy.transform.position;
            int best = 0;
            float bestDist = Vector3.SqrMagnitude(waypoints[0].position - pos);
            for (int i = 1; i < waypoints.Length; i++)
            {
                float d = Vector3.SqrMagnitude(waypoints[i].position - pos);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }
    }
}
