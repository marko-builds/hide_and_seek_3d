using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "HideAndSeek/Data/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Detection")]
        public float fieldOfViewAngle = 90f;
        public float detectionRange = 15f;
        public float hearingRange = 10f;
        public float suspicionRate = 0.5f;
        public float suspicionDecayRate = 0.2f;

        [Header("Navigation")]
        public float patrolSpeed = 2f;
        public float chaseSpeed = 5f;
        public float investigateSpeed = 3f;

        [Header("Detection Thresholds")]
        public float closeDetectionRange = 2f;
        public float catchDistance = 1.5f;
        public float investigateSuspicionThreshold = 0.3f;

        [Header("Investigation")]
        public float investigationLookDuration = 3f;

        [Header("Chase")]
        public float lostSightDuration = 2f;

        [Header("Search")]
        public float searchSweepRadius = 5f;
        public int searchSweepCount = 3;

        [Header("Timers")]
        public float idleWaitDuration = 2f;
        public float searchTimeout = 10f;
    }
}
