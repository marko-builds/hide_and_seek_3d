using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// Scene-bound registry that tracks objective token collection (GDD Objective System).
    ///
    /// ObjectiveToken registers itself during Awake; registration is closed in Start
    /// (after all Awakes complete) so the total count is correct.
    ///
    /// Start() also calls GameManager.StartRound() if the game is still in Warmup,
    /// ensuring the round begins automatically once all tokens have registered.
    ///
    /// ExecutionOrder -100 ensures the registry is initialized before any token Awake.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ObjectiveRegistry : SceneSingleton<ObjectiveRegistry>
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires each time a token is collected. Passes the collecting player.</summary>
        public event Action<PlayerController> OnObjectiveCollected;

        /// <summary>Fires when the last token is collected.</summary>
        public event Action OnAllObjectivesCollected;

        // ── State ─────────────────────────────────────────────────────────────────

        public int TotalCount { get; private set; }
        public int CollectedCount { get; private set; }
        public bool AllCollected => CollectedCount >= TotalCount && TotalCount > 0;

        // ── Registration ─────────────────────────────────────────────────────────

        readonly List<ObjectiveToken> _tokens = new();
        bool _registrationClosed;

        /// <summary>Called by ObjectiveToken.Awake. Ignored after Start().</summary>
        public void Register(ObjectiveToken token)
        {
            if (_registrationClosed)
            {
                Debug.LogWarning($"[ObjectiveRegistry] Late registration of {token.name} ignored.", token);
                return;
            }
            if (!_tokens.Contains(token))
                _tokens.Add(token);
        }

        /// <summary>Called by ObjectiveToken when it is destroyed before collection.</summary>
        public void Unregister(ObjectiveToken token)
        {
            if (_tokens.Remove(token) && _registrationClosed)
                TotalCount = Mathf.Max(0, TotalCount - 1);
        }

        // ── Collection ────────────────────────────────────────────────────────────

        /// <summary>Called by ObjectiveToken.OnInteracted once the collection is valid.</summary>
        public void RegisterCollection(PlayerController collector)
        {
            CollectedCount++;
            OnObjectiveCollected?.Invoke(collector);

            if (AllCollected)
                OnAllObjectivesCollected?.Invoke();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        void Start()
        {
            _registrationClosed = true;
            TotalCount = _tokens.Count;

            if (GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameManager.GameState.Warmup)
            {
                GameManager.Instance.StartRound();
            }
        }
    }
}
