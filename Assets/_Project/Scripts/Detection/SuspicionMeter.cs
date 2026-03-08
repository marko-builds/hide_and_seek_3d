using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Per-seeker suspicion accumulator and detection state machine.
    /// Suspicion is in [0, 100]. Owns threshold evaluation, state transitions with
    /// hysteresis (GDD F5), detection cooldown (GDD F4), state-based decay (GDD F4),
    /// Chase auto-rise (GDD F7), Chase patience (GDD Rule S-7), and the Caught dwell
    /// check (GDD F5).
    ///
    /// Driven each FixedUpdate by <see cref="EnemyDetection"/> via <see cref="Tick"/>.
    /// All tuning values come from <see cref="EnemyData"/>.
    /// </summary>
    public class SuspicionMeter : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when suspicion changes. Value is normalized [0, 1] for UI.</summary>
        public event Action<float> OnSuspicionChanged;

        /// <summary>Fires on every SeekState change, passing (previousState, newState).</summary>
        public event Action<SeekState, SeekState> OnStateChanged;

        /// <summary>Fires once when the player is caught (dwell timer expires in Chase).</summary>
        public event Action OnPlayerCaught;

        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] EnemyData _data;

        // ── Public State ──────────────────────────────────────────────────────────

        /// <summary>Raw suspicion in [0, 100].</summary>
        public float Suspicion { get; private set; }

        /// <summary>Suspicion normalized to [0, 1] for UI.</summary>
        public float SuspicionNormalized => Suspicion / 100f;

        /// <summary>Current detection state.</summary>
        public SeekState State { get; private set; } = SeekState.Unaware;

        /// <summary>
        /// Minimum suspicion value enforced by the decay clamp. 0 in Phase 1.
        /// Set by LevelPhaseManager via EnemyController when Phase 2 begins (GDD F4 extension).
        /// </summary>
        public float Phase2SuspicionFloor { get; set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Awake()
        {
            // Auto-wire from EnemyController if not assigned in Inspector
            if (_data == null && TryGetComponent<EnemyController>(out var controller))
                _data = controller.Data;
        }

        // ── Runtime ───────────────────────────────────────────────────────────────

        float _detectionCooldownTimer;
        float _chaseNoInputTimer;   // Tracks consecutive no-LoS/no-audio seconds in Chase (F7)
        float _catchDwellTimer;     // Tracks time player has been within catch radius
        bool _caughtFired;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called each FixedUpdate by <see cref="EnemyDetection"/>.
        /// </summary>
        /// <param name="visualDeltaPerSec">Visual suspicion gain in points/sec from F2. 0 if no LoS or player hidden.</param>
        /// <param name="audioSpike">One-time audio suspicion spike in points for this frame (GDD F3). Pass 0 for first pass.</param>
        /// <param name="hasLoS">True if a LoS raycast succeeded this tick. Controls Chase patience reset.</param>
        /// <param name="proximityDeltaPerSec">Hiding-spot proximity penalty in points/sec (GDD F6). 0 if inactive.</param>
        /// <param name="playerInCatchRadius">True if player is within catchDistance this tick.</param>
        public void Tick(float visualDeltaPerSec, float audioSpike, bool hasLoS,
                         float proximityDeltaPerSec, bool playerInCatchRadius)
        {
            if (_caughtFired) return;

            float dt = Time.fixedDeltaTime;
            float suspicionAtTickStart = Suspicion;
            bool hasDetectionInput = visualDeltaPerSec > 0f || audioSpike > 0f || proximityDeltaPerSec > 0f;

            // ── Detection cooldown (F4) ──────────────────────────────────────────
            if (hasDetectionInput)
                _detectionCooldownTimer = _data.detectionCooldownDuration;
            else
                _detectionCooldownTimer = Mathf.Max(0f, _detectionCooldownTimer - dt);

            // ── Apply detection inputs ───────────────────────────────────────────
            Suspicion += visualDeltaPerSec * dt + audioSpike + proximityDeltaPerSec * dt;

            // ── Chase state: auto-rise and patience timer (F7) ───────────────────
            if (State == SeekState.Chase)
            {
                bool hasAnyInput = hasLoS || hasDetectionInput;
                if (hasAnyInput)
                {
                    // Active detection: reset patience, apply auto-rise
                    _chaseNoInputTimer = 0f;
                    Suspicion += _data.chaseAutoSuspicionRate * dt;
                }
                else
                {
                    _chaseNoInputTimer += dt;
                    if (_chaseNoInputTimer < _data.chaseLostPatienceSeconds)
                    {
                        // Patience window still open: auto-rise continues (edge case per GDD)
                        Suspicion += _data.chaseAutoSuspicionRate * dt;
                    }
                    // else: patience expired — auto-rise stops, decay branch below handles it
                }
            }
            else
            {
                _chaseNoInputTimer = 0f;
            }

            // ── Decay (F4): only when cooldown expired and no detection input ────
            if (!hasDetectionInput && _detectionCooldownTimer <= 0f)
            {
                if (State == SeekState.Chase)
                {
                    // Chase: decay only after patience expires (Rule S-7)
                    if (_chaseNoInputTimer >= _data.chaseLostPatienceSeconds)
                        Suspicion -= _data.decayRateChase * dt;
                }
                else
                {
                    Suspicion -= GetDecayRateForState(State) * dt;
                }
            }

            Suspicion = Mathf.Clamp(Suspicion, Phase2SuspicionFloor, 100f);

            // ── Catch dwell check ────────────────────────────────────────────────
            if (State == SeekState.Chase && playerInCatchRadius)
            {
                _catchDwellTimer += dt;
                if (_catchDwellTimer >= _data.catchDwellTime)
                {
                    _caughtFired = true;
                    SeekState prev = State;
                    State = SeekState.Caught;
                    OnStateChanged?.Invoke(prev, State);
                    OnPlayerCaught?.Invoke();
                    return;
                }
            }
            else
            {
                _catchDwellTimer = 0f;
            }

            EvaluateStateTransitions();

            if (!Mathf.Approximately(Suspicion, suspicionAtTickStart))
                OnSuspicionChanged?.Invoke(SuspicionNormalized);
        }

        /// <summary>
        /// Directly adds suspicion points and evaluates state transitions.
        /// Useful for unit tests and one-off injections outside the normal Tick cycle.
        /// Does not trigger decay or cooldown side-effects.
        /// </summary>
        public void AddSuspicion(float points)
        {
            float prev = Suspicion;
            Suspicion = Mathf.Clamp(Suspicion + points, 0f, 100f);
            EvaluateStateTransitions();
            if (!Mathf.Approximately(Suspicion, prev))
                OnSuspicionChanged?.Invoke(SuspicionNormalized);
        }

        /// <summary>
        /// Resets suspicion and state. Call on respawn or round restart.
        /// In Phase 2, suspicion is clamped to <see cref="Phase2SuspicionFloor"/> rather than 0
        /// so the escalation floor is honoured immediately without waiting for the next Tick.
        /// </summary>
        public void Reset()
        {
            float prevSuspicion = Suspicion;
            SeekState prevState = State;

            Suspicion = Phase2SuspicionFloor;
            _detectionCooldownTimer = 0f;
            _chaseNoInputTimer = 0f;
            _catchDwellTimer = 0f;
            _caughtFired = false;
            State = SeekState.Unaware;

            if (prevState != SeekState.Unaware)
                OnStateChanged?.Invoke(prevState, State);
            if (!Mathf.Approximately(prevSuspicion, Phase2SuspicionFloor))
                OnSuspicionChanged?.Invoke(SuspicionNormalized);
        }

        /// <summary>
        /// Injects the EnemyData dependency. Used by unit tests that cannot set
        /// serialized fields via the Inspector.
        /// </summary>
        public void Initialize(EnemyData data) => _data = data;

        // ── Private ───────────────────────────────────────────────────────────────

        void EvaluateStateTransitions()
        {
            if (_caughtFired) return;

            SeekState target = ComputeTargetState();
            if (target == State) return;

            SeekState prev = State;
            State = target;
            OnStateChanged?.Invoke(prev, State);
        }

        /// <summary>
        /// Computes the target SeekState from current suspicion and current state.
        ///
        /// Upward transitions are immediate and can skip intermediate states (GDD F5 edge case:
        /// "A large audio spike from Unaware adding 45 points lands at 65 — directly in Searching").
        /// Downward transitions apply hysteresis: falling threshold is lower than rising threshold.
        /// </summary>
        SeekState ComputeTargetState()
        {
            // Upward: strict greater-than, highest threshold checked first so states are never skipped
            if (Suspicion > _data.chaseThreshold)
                return SeekState.Chase;

            // Hysteresis: stay in Chase until suspicion drops below chaseRevertThreshold
            if (State == SeekState.Chase && Suspicion >= _data.chaseRevertThreshold)
                return SeekState.Chase;

            if (Suspicion > _data.searchingThreshold)
                return SeekState.Searching;

            // Hysteresis: stay in Searching until suspicion drops below searchingRevertThreshold
            if (State == SeekState.Searching && Suspicion >= _data.searchingRevertThreshold)
                return SeekState.Searching;

            if (Suspicion > _data.alertThreshold)
                return SeekState.Alert;

            // Hysteresis: stay in Alert until suspicion drops below alertRevertThreshold
            if (State == SeekState.Alert && Suspicion >= _data.alertRevertThreshold)
                return SeekState.Alert;

            return SeekState.Unaware;
        }

        float GetDecayRateForState(SeekState state) => state switch
        {
            SeekState.Unaware   => _data.decayRateUnaware,
            SeekState.Alert     => _data.decayRateAlert,
            SeekState.Searching => _data.decayRateSearching,
            SeekState.Chase     => _data.decayRateChase,
            _                   => 0f,
        };

    }
}
