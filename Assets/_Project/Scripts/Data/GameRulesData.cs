using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "GameRulesData", menuName = "HideAndSeek/Data/Game Rules Data")]
    public class GameRulesData : ScriptableObject
    {
        [Header("Round")]
        public float roundDuration = 180f;

        [Header("Win / Lose")]
        [Tooltip("Player wins by surviving until the timer expires.")]
        public bool winOnTimerExpiry = true;

        [Header("Enemies")]
        public int enemyCount = 1;
    }
}
