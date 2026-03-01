using UnityEngine;

namespace HideAndSeek
{
    /// <summary>
    /// World-placed concealment object. Defines the attach transform, concealment level,
    /// and the entry point for the IHideable contract.
    /// </summary>
    public class HidingSpot : MonoBehaviour
    {
        [SerializeField] private HidingSpotData _data;
        [SerializeField] private Transform _attachTransform;

        public Transform AttachTransform => _attachTransform;
        public float ConcealmentModifier => _data.concealmentModifier;
        public Vector3 ExitPosition => transform.position + _data.exitOffset;

        public bool IsOccupied { get; private set; }

        public bool TryEnter(IHideable occupant)
        {
            if (IsOccupied) return false;
            IsOccupied = true;
            return true;
        }

        public void Exit()
        {
            IsOccupied = false;
        }

        private void OnEnable() => HidingSpotRegistry.Instance?.Register(this);
        private void OnDisable() => HidingSpotRegistry.TryGetInstance()?.Unregister(this);
    }
}
