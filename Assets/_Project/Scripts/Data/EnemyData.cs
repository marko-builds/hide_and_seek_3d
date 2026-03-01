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

        [Header("Timers")]
        public float idleWaitDuration = 2f;
        public float searchTimeout = 10f;
    }
}
