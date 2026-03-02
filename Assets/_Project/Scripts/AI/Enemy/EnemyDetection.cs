using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Handles FOV-based sight and hearing detection. Updates SuspicionMeter
    /// each frame based on player visibility and noise events.
    /// </summary>
    [RequireComponent(typeof(FieldOfView))]
    [RequireComponent(typeof(NoiseListener))]
    [RequireComponent(typeof(SuspicionMeter))]
    public class EnemyDetection : MonoBehaviour
    {
        [SerializeField] private LayerMask _obstructionMask;
        [SerializeField] private Transform _player;

        public SuspicionMeter SuspicionMeter { get; private set; }
        public bool PlayerVisible => _playerVisible;
        public Transform Player => _player;
        public IDetectable PlayerDetectable => _playerDetectable;
        public Vector3 LastKnownPlayerPosition => _lastKnownPlayerPosition;

        /// <summary>Fires once when suspicion first reaches 1.0.</summary>
        public event Action OnPlayerSpotted;
        /// <summary>Fires once when a fully suspicious enemy first loses sight of the player.</summary>
        public event Action OnPlayerLost;
        /// <summary>Fires when a noise within hearing range is detected.</summary>
        public event Action<Vector3> OnNoiseDetected;

        private FieldOfView _fieldOfView;
        private NoiseListener _noiseListener;
        private EnemyData _data;
        private IHideable _playerHideable;
        private IDetectable _playerDetectable;

        private bool _playerVisible;
        private bool _noisePending;
        private Vector3 _lastHeardNoisePosition;
        private Vector3 _lastKnownPlayerPosition;
        private bool _wasFullySuspicious;
        private bool _wasPlayerVisible;

        private void Awake()
        {
            _fieldOfView = GetComponent<FieldOfView>();
            _noiseListener = GetComponent<NoiseListener>();
            SuspicionMeter = GetComponent<SuspicionMeter>();
            _data = GetComponent<EnemyController>().Data;

            if (_player != null)
            {
                _playerHideable = _player.GetComponent<IHideable>();
                _playerDetectable = _player.GetComponent<IDetectable>();
            }
        }

        private void OnEnable() => _noiseListener.OnNoiseHeard += HandleNoiseHeard;
        private void OnDisable() => _noiseListener.OnNoiseHeard -= HandleNoiseHeard;

        private void Update()
        {
            if (_player == null) return;

            bool isHidden = _playerHideable != null && _playerHideable.IsHidden;
            float dt = Time.deltaTime;
            float visibilityModifier = _playerDetectable?.DetectionProfile?.visibilityModifier ?? 1f;

            // Determine visibility: close range (instant) or FOV + LOS
            _playerVisible = false;
            if (!isHidden)
            {
                float dist = Vector3.Distance(transform.position, _player.position);
                if (dist <= _data.closeDetectionRange)
                {
                    _playerVisible = true;
                    SuspicionMeter.Increase(1f);
                }
                else if (_fieldOfView.IsInFieldOfView(_player.position) &&
                         LineOfSightChecker.HasLineOfSight(transform.position, _player.position, _obstructionMask))
                {
                    _playerVisible = true;
                }
            }

            // Update suspicion based on visibility
            if (_playerVisible)
            {
                SuspicionMeter.Increase(_data.suspicionRate * visibilityModifier * dt);
                _lastKnownPlayerPosition = _player.position;
            }
            else
            {
                SuspicionMeter.Decrease(_data.suspicionDecayRate * dt);
            }

            // Edge detect: OnPlayerSpotted (suspicion first reaches 1.0)
            bool isFullySuspicious = SuspicionMeter.Suspicion >= 1f;
            if (isFullySuspicious && !_wasFullySuspicious)
                OnPlayerSpotted?.Invoke();

            // Edge detect: OnPlayerLost (fully suspicious enemy first loses sight)
            if (_wasFullySuspicious && _wasPlayerVisible && !_playerVisible)
                OnPlayerLost?.Invoke();

            _wasFullySuspicious = isFullySuspicious;
            _wasPlayerVisible = _playerVisible;
        }

        /// <summary>
        /// Returns true and outputs the noise position if a noise is pending.
        /// Clears the pending flag on consumption so each noise is handled once.
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

        private void HandleNoiseHeard(NoiseEvent noiseEvent)
        {
            float noiseModifier = _playerDetectable?.DetectionProfile?.noiseModifier ?? 1f;
            SuspicionMeter.Increase(noiseEvent.Intensity * noiseModifier);
            _lastHeardNoisePosition = noiseEvent.WorldPosition;
            _noisePending = true;
            OnNoiseDetected?.Invoke(noiseEvent.WorldPosition);
        }
    }
}
