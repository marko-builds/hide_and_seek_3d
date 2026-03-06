using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Top-level enemy MonoBehaviour. Owns the StateMachine and coordinates
    /// EnemyDetection and EnemyNavigation subsystems. Exposes Phase 2 escalation
    /// properties that LevelPhaseManager sets when the phase transition occurs.
    /// </summary>
    [RequireComponent(typeof(EnemyDetection))]
    [RequireComponent(typeof(EnemyNavigation))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] EnemyData _data;
        [SerializeField] Transform[] _waypoints;

        public EnemyData Data => _data;
        public Transform[] Waypoints => _waypoints;
        public EnemyDetection Detection { get; private set; }
        public EnemyNavigation Navigation { get; private set; }

        // ── Phase 2 Escalation ────────────────────────────────────────────────────

        /// <summary>Proxy to SuspicionMeter.Phase2SuspicionFloor. Set by LevelPhaseManager.</summary>
        public float Phase2SuspicionFloor
        {
            get => Detection.SuspicionMeter.Phase2SuspicionFloor;
            set => Detection.SuspicionMeter.Phase2SuspicionFloor = value;
        }

        /// <summary>
        /// Speed multiplier applied on top of per-state speed during Phase 2. Default 1f (no boost).
        /// States multiply: patrolSpeed × stateMultiplier × Phase2SpeedMultiplier.
        /// </summary>
        public float Phase2SpeedMultiplier { get; set; } = 1f;

        /// <summary>When true, EnemyAlertState skips its scan pause and goes directly to Search.</summary>
        public bool Phase2SkipAlertScan { get; set; }

        // ── Player Caught ─────────────────────────────────────────────────────────

        /// <summary>Fires when SuspicionMeter confirms the player has been caught.</summary>
        public event Action OnPlayerCaught;

        StateMachine _stateMachine;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Awake()
        {
            Detection = GetComponent<EnemyDetection>();
            Navigation = GetComponent<EnemyNavigation>();
            _stateMachine = new StateMachine();

            SeekerRegistry.Instance?.Register(this);
        }

        void Start()
        {
            Detection.SuspicionMeter.OnPlayerCaught += HandleSuspicionCaught;
            _stateMachine.ChangeState(new EnemyPatrolState(this));
        }

        void OnDestroy()
        {
            SeekerRegistry.Instance?.Unregister(this);
            if (Detection != null)
                Detection.SuspicionMeter.OnPlayerCaught -= HandleSuspicionCaught;
        }

        void Update()
        {
            _stateMachine.Tick();
        }

        public void ChangeState(IState newState) => _stateMachine.ChangeState(newState);

        // ── Private ───────────────────────────────────────────────────────────────

        void HandleSuspicionCaught() => OnPlayerCaught?.Invoke();
    }
}
