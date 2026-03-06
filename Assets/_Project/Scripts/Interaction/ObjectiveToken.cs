using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// A collectible objective token. The player interacts with it to register a collection
    /// with ObjectiveRegistry (GDD Objective System).
    ///
    /// Self-registers with ObjectiveRegistry during Awake so the registry knows the total
    /// count by the time Start() closes registration.
    /// </summary>
    public class ObjectiveToken : InteractableBase
    {
        [SerializeField] private ObjectiveData _data;

        private bool _isCollected;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            CanInteract = true;
            ObjectiveRegistry.Instance?.Register(this);
        }

        private void OnDestroy()
        {
            if (!_isCollected)
                ObjectiveRegistry.Instance?.Unregister(this);
        }

        // ── Interaction ───────────────────────────────────────────────────────────

        protected override void OnInteracted(PlayerController interactor)
        {
            if (_isCollected) return;
            if (GameManager.Instance == null
                || GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            _isCollected = true;
            CanInteract = false;

            SpawnVFX();
            PlaySFX();

            ObjectiveRegistry.Instance?.RegisterCollection(interactor);

            gameObject.SetActive(false);
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void SpawnVFX()
        {
            if (_data == null || _data.collectionVFXPrefab == null) return;
            Object.Instantiate(_data.collectionVFXPrefab, transform.position, Quaternion.identity);
        }

        private void PlaySFX()
        {
            if (_data == null) return;
            AudioManager.Instance?.Play(_data.collectionSFXKey);
        }
    }
}
