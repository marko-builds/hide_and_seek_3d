using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Computes visual detection each FixedUpdate and drives the seeker's
    /// <see cref="SuspicionMeter"/>. Implements GDD F1 (FOV gate) and F2 (visual suspicion
    /// delta formula) and F6 (hiding-spot proximity penalty).
    ///
    /// First-pass simplifications:
    ///   - light_factor = 1.0 (Light Source System is a stretch goal, GDD §3a Rule V-4).
    ///   - Audio detection passes audioSpike = 0 to SuspicionMeter (wired in task 1.2).
    /// </summary>
    [RequireComponent(typeof(FieldOfView))]
    [RequireComponent(typeof(NoiseListener))]
    [RequireComponent(typeof(SuspicionMeter))]
    public class EnemyDetection : MonoBehaviour
    {
        [SerializeField] LayerMask _obstructionMask;
        [SerializeField] PlayerController _player;

        // ── Public API ────────────────────────────────────────────────────────────

        public SuspicionMeter SuspicionMeter { get; private set; }

        /// <summary>True if a LoS raycast to the player succeeded last FixedUpdate tick.</summary>
        public bool PlayerVisible => _playerVisible;

        public Transform Player => _player != null ? _player.transform : null;
        public IDetectable PlayerDetectable => _playerDetectable;
        public Vector3 LastKnownPlayerPosition => _lastKnownPlayerPosition;

        /// <summary>Fires once when the seeker first enters Chase state.</summary>
        public event Action OnPlayerSpotted;
        /// <summary>Fires once when a chasing seeker first loses LoS.</summary>
        public event Action OnPlayerLost;
        /// <summary>Fires when a noise within hearing range is detected (position of origin).</summary>
        public event Action<Vector3> OnNoiseDetected;

        // ── Private ───────────────────────────────────────────────────────────────

        NoiseListener _noiseListener;
        FieldOfView _fov;
        EnemyData _data;
        IHideable _playerHideable;
        IDetectable _playerDetectable;
        PlayerMovement _playerMovement;

        bool _playerVisible;
        bool _noisePending;
        Vector3 _lastHeardNoisePosition;
        Vector3 _lastKnownPlayerPosition;
        bool _wasPlayerVisible;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Awake()
        {
            _noiseListener  = GetComponent<NoiseListener>();
            SuspicionMeter  = GetComponent<SuspicionMeter>();
            _fov            = GetComponent<FieldOfView>();
            _data           = GetComponent<EnemyController>().Data;

            if (_player != null)
            {
                _playerHideable    = _player;           // PlayerController implements IHideable
                _playerDetectable  = _player;           // PlayerController implements IDetectable
                _playerMovement    = _player.Movement;
            }

            // Mirror Chase-state transitions onto legacy events so EnemyChaseState still works
            SuspicionMeter.OnStateChanged += HandleSuspicionStateChanged;
        }

        void OnDestroy()
        {
            SuspicionMeter.OnStateChanged -= HandleSuspicionStateChanged;
        }

        void OnEnable()  => _noiseListener.OnNoiseHeard += HandleNoiseHeard;
        void OnDisable() => _noiseListener.OnNoiseHeard -= HandleNoiseHeard;

        // ── Detection per physics step ────────────────────────────────────────────

        void FixedUpdate()
        {
            if (_player == null) return;

            float dt = Time.fixedDeltaTime;
            Vector3 playerPos = _player.transform.position;
            bool isHidden = _playerHideable != null && _playerHideable.IsHidden;

            // ── Visual detection (F1+V-3 gate → F2 delta) ────────────────────────
            // Gate delegates to FieldOfView.IsInDetectionVolume which owns all FOV/range
            // logic including the GDD Rule V-3 close-range override.
            _playerVisible = false;
            float visualDeltaPerSec = 0f;
            bool hasLoS = false;

            if (!isHidden)
            {
                Vector3 toPlayer    = playerPos - transform.position;
                float distance      = toPlayer.magnitude;
                float angleToPlayer = Vector3.Angle(transform.forward, toPlayer.normalized);

                if (_fov.IsInDetectionVolume(playerPos))
                {
                    hasLoS = LineOfSightChecker.HasLineOfSight(
                        transform.position, playerPos, _obstructionMask);

                    if (hasLoS)
                    {
                        _playerVisible = true;
                        _lastKnownPlayerPosition = playerPos;
                        visualDeltaPerSec = ComputeVisualDelta(distance, angleToPlayer);
                    }
                }
            }

            // ── Hiding-spot proximity penalty (F6) ───────────────────────────────
            float proximityDeltaPerSec = 0f;
            if (isHidden
                && _playerHideable.CurrentHidingSpot != null
                && SuspicionMeter.State >= SeekState.Alert)
            {
                float spotDist = Vector3.Distance(
                    transform.position,
                    _playerHideable.CurrentHidingSpot.transform.position);

                if (spotDist <= _data.hidingSpotAwarenessRadius)
                {
                    float proximityFactor = 1f - (spotDist / _data.hidingSpotAwarenessRadius);
                    proximityDeltaPerSec = _data.creepingDreadRate * proximityFactor;
                }
            }

            // ── Catch radius check ───────────────────────────────────────────────
            bool playerInCatchRadius =
                Vector3.Distance(transform.position, playerPos) <= _data.catchDistance;

            // ── Drive SuspicionMeter ─────────────────────────────────────────────
            // audioSpike = 0 for first pass; wired in task 1.2
            SuspicionMeter.Tick(visualDeltaPerSec, 0f, hasLoS, proximityDeltaPerSec, playerInCatchRadius);

            _wasPlayerVisible = _playerVisible;
        }

        // ── Noise (stub for task 1.2) ─────────────────────────────────────────────

        /// <summary>
        /// Returns true and outputs the noise world position if a noise event is pending.
        /// Clears the pending flag — each noise event is consumed once.
        /// </summary>
        public bool ConsumePendingNoise(out Vector3 position)
        {
            if (_noisePending)
            {
                position = _lastHeardNoisePosition;
                _noisePending = false;
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// GDD F2 — visual suspicion delta in points/sec.
        /// light_factor = 1.0 (first pass; Light Source System is a P3 task).
        /// </summary>
        float ComputeVisualDelta(float distance, float angleToPlayer)
        {
            float maxRange = _data.detectionRange;
            float halfFov  = _data.fieldOfViewAngle * 0.5f;

            // Clamp angle to halfFov so deviation stays in [0, 1] even at closeDetectionRange
            float clampedAngle = Mathf.Min(angleToPlayer, halfFov);

            float distanceFactor = 1f - Mathf.Pow(distance / maxRange, _data.distanceFalloffExponent);
            float angleDeviation = halfFov > 0f ? clampedAngle / halfFov : 0f;
            float angleFactor    = 1f - Mathf.Pow(angleDeviation, _data.angleFalloffExponent);

            // light_factor = 1.0 — no light modifier for first pass
            bool isCrouching      = _playerMovement != null && _playerMovement.IsCrouching;
            float crouchModifier  = isCrouching ? _data.crouchStealthMultiplier : 1f;

            float stateMultiplier = SuspicionMeter.State == SeekState.Searching
                ? _data.searchingVisualDetectionMultiplier : 1f;

            return _data.baseDetectionRate
                   * distanceFactor
                   * angleFactor
                   * crouchModifier
                   * stateMultiplier;
        }

        void HandleSuspicionStateChanged(SeekState prev, SeekState next)
        {
            // Translate state transitions into the legacy events EnemyChaseState depends on
            if (next >= SeekState.Chase && prev < SeekState.Chase)
                OnPlayerSpotted?.Invoke();

            if (prev >= SeekState.Chase && next < SeekState.Chase && !_wasPlayerVisible)
                OnPlayerLost?.Invoke();
        }

        void HandleNoiseHeard(NoiseEvent noiseEvent)
        {
            // Audio spike is deferred to task 1.2; store position for Investigate transitions
            _lastHeardNoisePosition = noiseEvent.WorldPosition;
            _noisePending = true;
            OnNoiseDetected?.Invoke(noiseEvent.WorldPosition);
        }
    }
}
