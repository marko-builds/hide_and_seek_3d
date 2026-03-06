using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Listens for ObjectiveRegistry.OnAllObjectivesCollected and triggers the Phase 2
    /// escalation on all seekers (GDD Two-Phase Level Structure).
    ///
    /// Applies EscalationProfile values to each EnemyController via SeekerRegistry,
    /// fires the static OnPhaseChanged event, and plays the Phase2Start audio cue.
    ///
    /// Place one in the scene alongside ObjectiveRegistry and SeekerRegistry.
    /// The static event is nulled in Awake to clear stale listeners from prior scenes.
    /// </summary>
    public class LevelPhaseManager : MonoBehaviour
    {
        [SerializeField] EscalationProfile _escalationProfile;

        /// <summary>
        /// Fires when the level phase changes. Static so UI/audio can subscribe without
        /// holding a scene reference.
        /// </summary>
        public static event Action<LevelPhase> OnPhaseChanged;

        public LevelPhase CurrentPhase { get; private set; } = LevelPhase.Phase1_Find;

        void Awake()
        {
            // Clear stale cross-scene subscribers
            OnPhaseChanged = null;
        }

        void OnEnable()
        {
            if (ObjectiveRegistry.Instance != null)
                ObjectiveRegistry.Instance.OnAllObjectivesCollected += HandleAllObjectivesCollected;
        }

        void OnDisable()
        {
            if (ObjectiveRegistry.Instance != null)
                ObjectiveRegistry.Instance.OnAllObjectivesCollected -= HandleAllObjectivesCollected;
        }

        void HandleAllObjectivesCollected()
        {
            if (CurrentPhase == LevelPhase.Phase2_Escape) return;

            CurrentPhase = LevelPhase.Phase2_Escape;

            ApplyEscalationToSeekers();

            OnPhaseChanged?.Invoke(LevelPhase.Phase2_Escape);

            AudioManager.Instance?.Play(SoundID.Phase2Start);
        }

        void ApplyEscalationToSeekers()
        {
            if (_escalationProfile == null || SeekerRegistry.Instance == null) return;

            foreach (var seeker in SeekerRegistry.Instance.GetAll())
            {
                seeker.Phase2SuspicionFloor = _escalationProfile.suspicionFloor;
                seeker.Phase2SpeedMultiplier = _escalationProfile.speedMultiplier;
                seeker.Phase2SkipAlertScan = _escalationProfile.skipAlertScanInPhase2;
            }
        }
    }
}
