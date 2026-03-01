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

        public SuspicionMeter SuspicionMeter { get; private set; }

        private FieldOfView _fieldOfView;
        private NoiseListener _noiseListener;
        private EnemyData _data;
        private IDetectable _playerDetectable;

        private void Awake()
        {
            _fieldOfView = GetComponent<FieldOfView>();
            _noiseListener = GetComponent<NoiseListener>();
            SuspicionMeter = GetComponent<SuspicionMeter>();
            _data = GetComponent<EnemyController>().Data;
        }

        private void OnEnable() => _noiseListener.OnNoiseHeard += HandleNoiseHeard;
        private void OnDisable() => _noiseListener.OnNoiseHeard -= HandleNoiseHeard;

        private void Update()
        {
            // TODO: locate player via IDetectable reference, check FOV + LOS, update suspicion
        }

        private void HandleNoiseHeard(NoiseEvent noiseEvent)
        {
            // TODO: raise suspicion proportional to noise intensity
        }
    }
}
