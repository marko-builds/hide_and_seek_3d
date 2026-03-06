using System;
using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// The level exit door / portal. Locked until all objective tokens are collected;
    /// unlocks when ObjectiveRegistry.OnAllObjectivesCollected fires (GDD Level-Exit System).
    ///
    /// Implements IChaseInteractable so PlayerInteractionSystem bypasses its Chase-blocking
    /// rule (GDD Rule LE-7: player must always be able to escape).
    ///
    /// CanInteract returns a computed value (unlocked AND game is Playing).
    /// The protected setter is intentionally a no-op to satisfy the base class contract.
    /// </summary>
    public class LevelExit : InteractableBase, IChaseInteractable
    {
        [SerializeField] private LevelExitData _data;

        /// <summary>Fires when the player successfully uses this exit.</summary>
        public event Action OnExitUsed;

        private bool _isUnlocked;

        // ── CanInteract override: computed, ignores the base auto-property ─────────

        public override bool CanInteract
        {
            get => _isUnlocked
                   && GameManager.Instance != null
                   && GameManager.Instance.CurrentState == GameManager.GameState.Playing;
            protected set { /* intentional no-op: state is derived, not stored */ }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (ObjectiveRegistry.Instance != null)
                ObjectiveRegistry.Instance.OnAllObjectivesCollected += Unlock;
        }

        private void OnDisable()
        {
            if (ObjectiveRegistry.Instance != null)
                ObjectiveRegistry.Instance.OnAllObjectivesCollected -= Unlock;
        }

        // ── Unlock ────────────────────────────────────────────────────────────────

        private void Unlock()
        {
            _isUnlocked = true;

            if (_data != null)
            {
                AudioManager.Instance?.Play(_data.unlockSFXKey);

                if (_data.unlockVFXPrefab != null)
                    Object.Instantiate(_data.unlockVFXPrefab, transform.position, Quaternion.identity);
            }
        }

        // ── Interaction ───────────────────────────────────────────────────────────

        protected override void OnInteracted(PlayerController interactor)
        {
            if (_data != null)
            {
                AudioManager.Instance?.Play(_data.exitSFXKey);

                if (_data.exitVFXPrefab != null)
                    Object.Instantiate(_data.exitVFXPrefab, transform.position, Quaternion.identity);
            }

            OnExitUsed?.Invoke();
            GameManager.Instance?.TriggerWin();
        }
    }
}
