using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Two-phase search after losing the player (GDD §3.4):
    ///
    /// Phase 1 — MovingToLKP: Navigate to lastKnownPosition.
    /// Phase 2 — Sweeping: N equidistant facing directions from current forward,
    ///   rotating at searchTurnSpeed and holding each for sweepHoldDuration.
    ///   After the sweep, check up to searchWaypointCount nearby patrol waypoints
    ///   before returning to Patrol.
    ///
    /// A new noise event during Phase 2 restarts Phase 1 toward the noise position.
    /// Escalates to ChaseState if suspicion hits Chase threshold at any time.
    /// Descends to AlertState if suspicion drops back to Alert range.
    /// </summary>
    public class EnemySearchState : BaseState
    {
        readonly EnemyController _enemy;
        Vector3 _searchTarget;

        // Phase tracking
        enum Phase { MovingToLKP, Sweeping, CheckingWaypoints }

        Phase _phase;

        // Sweep
        int _sweepStep;
        float _sweepHoldTimer;
        Vector3 _sweepStartForward;

        // Nearby waypoint check
        int[] _nearbyWaypointIndices;
        int _waypointCheckIndex;

        public EnemySearchState(EnemyController enemy, Vector3 lastKnownPosition)
        {
            _enemy = enemy;
            _searchTarget = lastKnownPosition;
        }

        public override void Enter()
        {
            float speed = _enemy.Data.patrolSpeed
                          * _enemy.Data.searchSpeedMultiplier
                          * _enemy.Phase2SpeedMultiplier;
            _enemy.Navigation.SetSpeed(speed);

            StartMoveToTarget(_searchTarget);
        }

        public override void Tick()
        {
            SeekState seekState = _enemy.Detection.SuspicionMeter.State;

            if (seekState >= SeekState.Chase)
            {
                _enemy.ChangeState(new EnemyChaseState(_enemy));
                return;
            }

            // New noise restarts Phase 1 toward noise position
            if (_enemy.Detection.ConsumePendingNoise(out Vector3 noisePos))
            {
                _searchTarget = noisePos;
                StartMoveToTarget(_searchTarget);
                return;
            }

            switch (_phase)
            {
                case Phase.MovingToLKP:
                    TickMovingToLKP();
                    break;
                case Phase.Sweeping:
                    TickSweeping();
                    break;
                case Phase.CheckingWaypoints:
                    TickCheckingWaypoints();
                    break;
            }
        }

        // ── Phase 1 ───────────────────────────────────────────────────────────────

        void StartMoveToTarget(Vector3 target)
        {
            _phase = Phase.MovingToLKP;
            _enemy.Navigation.SetDestination(target);
        }

        void TickMovingToLKP()
        {
            if (_enemy.Navigation.IsNear(_searchTarget, _enemy.Data.waypointArrivalThreshold))
                StartSweep();
        }

        // ── Phase 2 — Directional Sweep ───────────────────────────────────────────

        void StartSweep()
        {
            _phase = Phase.Sweeping;
            _sweepStep = 0;
            _sweepHoldTimer = _enemy.Data.sweepHoldDuration;
            _sweepStartForward = _enemy.transform.forward;
            _enemy.Navigation.Stop();
        }

        void TickSweeping()
        {
            _sweepHoldTimer -= Time.deltaTime;
            if (_sweepHoldTimer > 0f)
            {
                // Rotate toward the current sweep direction
                Vector3 dir = SweepDirection(_sweepStep);
                _enemy.Navigation.RotateToward(
                    _enemy.transform.position + dir,
                    _enemy.Data.searchTurnSpeed);
                return;
            }

            _sweepStep++;
            if (_sweepStep >= _enemy.Data.searchSweepDirectionCount)
            {
                StartWaypointCheck();
                return;
            }

            _sweepHoldTimer = _enemy.Data.sweepHoldDuration;
        }

        /// <summary>Returns the world-space direction for sweep step <paramref name="step"/>.</summary>
        Vector3 SweepDirection(int step)
        {
            float angleDeg = step * (360f / _enemy.Data.searchSweepDirectionCount);
            return Quaternion.AngleAxis(angleDeg, Vector3.up) * _sweepStartForward;
        }

        // ── Phase 3 — Waypoint Check ──────────────────────────────────────────────

        void StartWaypointCheck()
        {
            _phase = Phase.CheckingWaypoints;
            _waypointCheckIndex = 0;
            _nearbyWaypointIndices = FindNearestWaypointIndices(_enemy.Data.searchWaypointCount);

            if (_nearbyWaypointIndices.Length == 0)
            {
                FinishSearch();
                return;
            }

            _enemy.Navigation.SetDestination(_enemy.Waypoints[_nearbyWaypointIndices[0]].position);
        }

        void TickCheckingWaypoints()
        {
            if (_waypointCheckIndex >= _nearbyWaypointIndices.Length)
            {
                FinishSearch();
                return;
            }

            Vector3 dest = _enemy.Waypoints[_nearbyWaypointIndices[_waypointCheckIndex]].position;
            if (!_enemy.Navigation.IsNear(dest, _enemy.Data.waypointArrivalThreshold)) return;

            _waypointCheckIndex++;
            if (_waypointCheckIndex < _nearbyWaypointIndices.Length)
                _enemy.Navigation.SetDestination(_enemy.Waypoints[_nearbyWaypointIndices[_waypointCheckIndex]].position);
        }

        void FinishSearch()
        {
            _enemy.Detection.SuspicionMeter.Reset();
            _enemy.ChangeState(new EnemyPatrolState(_enemy, resumeAtNearest: true));
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the indices of the <paramref name="count"/> nearest waypoints to the seeker,
        /// sorted by ascending distance (F-S1).
        /// </summary>
        int[] FindNearestWaypointIndices(int count)
        {
            var waypoints = _enemy.Waypoints;
            if (waypoints == null || waypoints.Length == 0)
                return System.Array.Empty<int>();

            count = Mathf.Min(count, waypoints.Length);
            int[] indices = new int[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++) indices[i] = i;

            // Partial insertion sort: move the 'count' smallest to the front
            Vector3 pos = _enemy.transform.position;
            for (int i = 0; i < count; i++)
            {
                int minIdx = i;
                float minDist = Vector3.SqrMagnitude(waypoints[indices[i]].position - pos);
                for (int j = i + 1; j < indices.Length; j++)
                {
                    float d = Vector3.SqrMagnitude(waypoints[indices[j]].position - pos);
                    if (d < minDist) { minDist = d; minIdx = j; }
                }
                (indices[i], indices[minIdx]) = (indices[minIdx], indices[i]);
            }

            int[] result = new int[count];
            System.Array.Copy(indices, result, count);
            return result;
        }
    }
}
