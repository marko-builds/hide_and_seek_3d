using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// Scene-bound registry of all active EnemyControllers. Allows systems such as
    /// LevelPhaseManager to iterate seekers without FindObjectsByType calls at runtime.
    ///
    /// EnemyController registers itself in Awake and unregisters in OnDestroy.
    /// ExecutionOrder -90 ensures registration is open before any enemy Awake runs.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class SeekerRegistry : SceneSingleton<SeekerRegistry>
    {
        readonly List<EnemyController> _seekers = new();

        /// <summary>Read-only view of all currently registered seekers.</summary>
        public IReadOnlyList<EnemyController> GetAll() => _seekers;

        /// <summary>Called by EnemyController.Awake.</summary>
        public void Register(EnemyController enemy)
        {
            if (!_seekers.Contains(enemy))
                _seekers.Add(enemy);
        }

        /// <summary>Called by EnemyController.OnDestroy.</summary>
        public void Unregister(EnemyController enemy) => _seekers.Remove(enemy);
    }
}
