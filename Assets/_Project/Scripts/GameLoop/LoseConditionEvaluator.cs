using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Discovers all EnemyControllers in the scene and triggers TriggerLose when any
    /// seeker catches the player.
    /// </summary>
    public class LoseConditionEvaluator : MonoBehaviour
    {
        private void Start()
        {
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                enemy.OnPlayerCaught += HandlePlayerCaught;
        }

        private void HandlePlayerCaught() => GameManager.Instance.TriggerLose();
    }
}
