using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Discovers all EnemyControllers in the scene and triggers TriggerLose when any
    /// seeker catches the player.
    /// </summary>
    public class LoseConditionEvaluator : MonoBehaviour
    {
        void Start()
        {
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                enemy.OnPlayerCaught += HandlePlayerCaught;
        }

        void HandlePlayerCaught() => GameManager.Instance.TriggerLose();
    }
}
