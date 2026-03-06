using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Defines how seeker difficulty escalates when Phase 2 begins.
    /// Assign to LevelPhaseManager in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "EscalationProfile", menuName = "HideAndSeek/Data/Escalation Profile")]
    public class EscalationProfile : ScriptableObject
    {
        [Header("Suspicion Floor")]
        [Tooltip("Minimum suspicion value seekers maintain in Phase 2. Prevents full cool-down. " +
                 "Matches alertThreshold so seekers never drop below Alert (GDD F4 extension).")]
        [Range(0f, 100f)]
        public float suspicionFloor = 60f;

        [Header("Speed")]
        [Tooltip("Multiplier applied on top of each state's base speed in Phase 2. " +
                 "E.g. 1.3 = 30% faster across all states.")]
        [Range(1f, 3f)]
        public float speedMultiplier = 1.3f;

        [Header("Alert State")]
        [Tooltip("When true, seekers skip the alertScanDuration pause and go straight to Search " +
                 "in Phase 2, making them react faster.")]
        public bool skipAlertScanInPhase2;
    }
}
