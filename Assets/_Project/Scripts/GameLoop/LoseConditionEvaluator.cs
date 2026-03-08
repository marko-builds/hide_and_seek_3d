using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Subscribes to all registered seekers via <see cref="SeekerRegistry"/> and triggers
    /// <see cref="GameManager.TriggerLose"/> when any seeker catches the player.
    /// </summary>
    public class LoseConditionEvaluator : MonoBehaviour
    {
        void Start()
        {
            foreach (var enemy in SeekerRegistry.Instance.GetAll())
                enemy.OnPlayerCaught += HandlePlayerCaught;
        }

        void HandlePlayerCaught() => GameManager.Instance.TriggerLose();
    }
}
