using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// SceneSingleton that tracks all active HidingSpots in the current scene.
    /// HidingSpot registers/unregisters itself on enable/disable.
    /// </summary>
    public class HidingSpotRegistry : SceneSingleton<HidingSpotRegistry>
    {
        readonly List<HidingSpot> _spots = new List<HidingSpot>();

        public void Register(HidingSpot spot)
        {
            if (!_spots.Contains(spot))
                _spots.Add(spot);
        }

        public void Unregister(HidingSpot spot) => _spots.Remove(spot);

        /// <summary>Returns the nearest unoccupied HidingSpot to <paramref name="position"/>, or null.</summary>
        public HidingSpot FindNearest(Vector3 position)
        {
            HidingSpot nearest = null;
            float minDistance = float.MaxValue;

            foreach (var spot in _spots)
            {
                if (spot.IsOccupied) continue;
                float distance = Vector3.Distance(position, spot.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = spot;
                }
            }

            return nearest;
        }
    }
}
