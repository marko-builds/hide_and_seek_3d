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
        private readonly Vector3 _lastKnownPosition;
        private Vector3[] _sweepPoints;
        private int _sweepIndex;
        private float _timer;

        public EnemySearchState(EnemyController enemy, Vector3 lastKnownPosition)
        {
            _enemy = enemy;
            _lastKnownPosition = lastKnownPosition;
        }

        public override void Enter()
        {
            _timer = _enemy.Data.searchTimeout;
            _enemy.Navigation.SetSpeed(_enemy.Data.investigateSpeed);

            int count = _enemy.Data.searchSweepCount;
            float radius = _enemy.Data.searchSweepRadius;
            _sweepPoints = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                _sweepPoints[i] = _lastKnownPosition + new Vector3(circle.x, 0f, circle.y);
            }

            _sweepIndex = 0;
            _enemy.Navigation.SetDestination(_sweepPoints[0]);
        }

        public override void Tick()
        {
            if (_enemy.Detection.SuspicionMeter.Suspicion >= 1f)
            {
                _enemy.ChangeState(new EnemyChaseState(_enemy));
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _enemy.Detection.SuspicionMeter.Reset();
                _enemy.ChangeState(new EnemyPatrolState(_enemy));
                return;
            }

            if (_enemy.Navigation.IsAtDestination)
            {
                _sweepIndex = (_sweepIndex + 1) % _sweepPoints.Length;
                _enemy.Navigation.SetDestination(_sweepPoints[_sweepIndex]);
            }
        }
    }
}
