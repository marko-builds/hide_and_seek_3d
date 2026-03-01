using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// Listens for the player-caught event and triggers a loss via GameManager.
    /// </summary>
    public class LoseConditionEvaluator : MonoBehaviour
    {
        // TODO: subscribe to OnPlayerCaught event (raised by EnemyChaseState on catch)
        // private void HandlePlayerCaught() => GameManager.Instance.TriggerLose();
    }
}
