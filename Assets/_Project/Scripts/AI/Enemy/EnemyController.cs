using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Top-level enemy MonoBehaviour. Owns the StateMachine and coordinates
    /// EnemyDetection and EnemyNavigation subsystems.
    /// </summary>
    [RequireComponent(typeof(EnemyDetection))]
    [RequireComponent(typeof(EnemyNavigation))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyData _data;
        [SerializeField] private Transform[] _waypoints;

        public EnemyData Data => _data;
        public Transform[] Waypoints => _waypoints;
        public EnemyDetection Detection { get; private set; }
        public EnemyNavigation Navigation { get; private set; }

        public event Action OnPlayerCaught;
        public void NotifyPlayerCaught() => OnPlayerCaught?.Invoke();

        private StateMachine _stateMachine;

        private void Awake()
        {
            Detection = GetComponent<EnemyDetection>();
            Navigation = GetComponent<EnemyNavigation>();
            _stateMachine = new StateMachine();
        }

        private void Start()
        {
            _stateMachine.ChangeState(new EnemyIdleState(this));
        }

        private void Update()
        {
            _stateMachine.Tick();
        }

        public void ChangeState(IState newState) => _stateMachine.ChangeState(newState);
    }
}
