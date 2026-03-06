using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "HideAndSeek/Data/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        // ── Visual Detection — F1 / F2 ────────────────────────────────────────────

        [Header("Visual Detection")]
        [Tooltip("Maximum visual range (seeker_max_range). Used by FOV check and distance_factor.")]
        public float detectionRange = 15f;

        [Tooltip("Total horizontal field-of-view angle in degrees. Half-angle is the cone boundary.")]
        public float fieldOfViewAngle = 90f;

        [Tooltip("Suspicion points/sec added at ideal conditions: point-blank, center FOV, fully lit, exposed (GDD F2 base_detection_rate).")]
        public float baseDetectionRate = 15f;

        [Tooltip("Exponent on distance_factor curve. 1 = linear drop-off, 2 = quadratic (safer at max range).")]
        public float distanceFalloffExponent = 2f;

        [Tooltip("Exponent on angle_factor curve. Higher = edge of FOV is safer.")]
        public float angleFalloffExponent = 2f;

        [Tooltip("Fraction of visual detection rate when player is crouching (crouch_stealth_multiplier, GDD V-5).")]
        [Range(0f, 1f)]
        public float crouchStealthMultiplier = 0.4f;

        [Tooltip("Visual detection rate multiplier when seeker is in Searching state (GDD F2 state_multiplier).")]
        public float searchingVisualDetectionMultiplier = 1.5f;

        [Tooltip("Range within which visual FOV check is skipped and full detection rate is applied regardless of angle.")]
        public float closeDetectionRange = 2f;

        // ── Suspicion State Thresholds — F5 ──────────────────────────────────────

        [Header("Suspicion Thresholds (Rising)")]
        [Tooltip("Suspicion > this → Unaware transitions to Alert.")]
        public float alertThreshold = 25f;

        [Tooltip("Suspicion > this → Alert transitions to Searching.")]
        public float searchingThreshold = 60f;

        [Tooltip("Suspicion > this → Searching transitions to Chase.")]
        public float chaseThreshold = 85f;

        [Header("Suspicion Thresholds (Falling / Hysteresis)")]
        [Tooltip("Suspicion < this → Alert reverts to Unaware. Must be less than alertThreshold.")]
        public float alertRevertThreshold = 15f;

        [Tooltip("Suspicion < this → Searching reverts to Alert. Must be less than searchingThreshold.")]
        public float searchingRevertThreshold = 50f;

        [Tooltip("Suspicion < this → Chase reverts to Searching. Must be less than chaseThreshold.")]
        public float chaseRevertThreshold = 75f;

        // ── Suspicion Decay — F4 ─────────────────────────────────────────────────

        [Header("Suspicion Decay")]
        [Tooltip("Suspicion points/sec lost while in Unaware state (fastest decay).")]
        public float decayRateUnaware = 8f;

        [Tooltip("Suspicion points/sec lost while in Alert state.")]
        public float decayRateAlert = 3f;

        [Tooltip("Suspicion points/sec lost while in Searching state.")]
        public float decayRateSearching = 1.5f;

        [Tooltip("Suspicion points/sec lost while in Chase state (after patience expires).")]
        public float decayRateChase = 0.5f;

        [Tooltip("Seconds of no detection input before decay begins (detection_cooldown_duration, GDD F4).")]
        public float detectionCooldownDuration = 1.5f;

        // ── Chase Auto-Rise & Patience — F7 ──────────────────────────────────────

        [Header("Chase State")]
        [Tooltip("Suspicion points/sec added automatically while in Chase, regardless of LoS (chase_auto_suspicion_rate, GDD F7).")]
        public float chaseAutoSuspicionRate = 5f;

        [Tooltip("Continuous seconds with no LoS and no audio before Chase auto-rise stops and decay begins (GDD Rule S-7).")]
        public float chaseLostPatienceSeconds = 3f;

        [Tooltip("Player must be within this range for the catch dwell timer to increment.")]
        public float catchDistance = 1.5f;

        [Tooltip("Seconds the player must remain within catchDistance to trigger Caught (catch_dwell_time, GDD F5).")]
        public float catchDwellTime = 0.15f;

        // ── Hiding Spot Proximity Penalty — F6 ───────────────────────────────────

        [Header("Hiding Spot Awareness")]
        [Tooltip("Radius around an occupied hiding spot within which Alert/Searching/Chase seekers accumulate suspicion (GDD F6).")]
        public float hidingSpotAwarenessRadius = 2.5f;

        [Tooltip("Max suspicion points/sec when seeker is at point-blank range of an occupied hiding spot (creeping_dread_rate, GDD F6).")]
        public float creepingDreadRate = 3f;

        // ── Audio Detection ───────────────────────────────────────────────────────

        [Header("Audio Detection")]
        [Tooltip("Minimum attenuated intensity required for this seeker to hear a NoiseEvent. Hard cap 0.95 (prevents div-by-zero in F3).")]
        [Range(0f, 0.95f)]
        public float hearingThreshold = 0.35f;

        [Tooltip("Radius within which NoiseListener receives events from NoiseEmitter.")]
        public float hearingRange = 10f;

        // ── Navigation ────────────────────────────────────────────────────────────

        [Header("Navigation")]
        [Tooltip("Patrol movement speed (m/s). Chase and search speeds are multipliers of this.")]
        public float patrolSpeed = 2f;
        [Tooltip("Speed used when investigating a noise position.")]
        public float investigateSpeed = 3f;
        [Tooltip("Radius within which the seeker considers a NavMesh waypoint reached.")]
        public float waypointArrivalThreshold = 0.3f;

        // ── Alert State ───────────────────────────────────────────────────────────

        [Header("Alert State")]
        [Tooltip("Rotation speed when turning to face a stimulus in Alert (deg/s, GDD §3.3).")]
        public float alertTurnSpeed = 90f;
        [Tooltip("Seconds the seeker holds still and scans before returning to patrol (GDD §3.3 alertScanDuration).")]
        public float alertScanDuration = 2.5f;

        // ── Searching State ───────────────────────────────────────────────────────

        [Header("Search State")]
        [Tooltip("Speed multiplier applied to patrolSpeed during Searching (searchSpeedMultiplier × patrolSpeed, GDD §3.4).")]
        public float searchSpeedMultiplier = 1.3f;
        [Tooltip("Rotation speed during the directional sweep (deg/s, GDD F-S2).")]
        public float searchTurnSpeed = 120f;
        [Tooltip("Seconds the seeker holds each sweep direction (sweepHoldDuration, GDD F-S2).")]
        public float sweepHoldDuration = 0.5f;
        [Tooltip("Number of equidistant facing directions in the sweep (GDD §3.4 Phase 2).")]
        public int searchSweepDirectionCount = 8;
        [Tooltip("How many nearby patrol waypoints to check after the directional sweep (GDD §3.4 Phase 3).")]
        public int searchWaypointCount = 2;

        // ── Chase State ───────────────────────────────────────────────────────────

        [Header("Chase State — Speed")]
        [Tooltip("Speed multiplier applied to patrolSpeed during Chase (chaseSpeedMultiplier × patrolSpeed, GDD §3.5).")]
        public float chaseSpeedMultiplier = 1.6f;
        [Tooltip("Seconds between NavMeshAgent.SetDestination calls in Chase to limit path recalculation cost (GDD AC-14).")]
        public float chaseNavUpdateInterval = 0.2f;

        // ── Timers ────────────────────────────────────────────────────────────────

        [Header("Timers")]
        public float idleWaitDuration = 2f;
        public float investigationLookDuration = 3f;
        public float lostSightDuration = 2f;
        public float patrolDwellDuration = 1.5f;
    }
}
