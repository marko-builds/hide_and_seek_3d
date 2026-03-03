using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal static class LevelBuilderUtility
    {
        internal const string k_MenuRoot = "Tools/LevelBuilder/";

        /// <summary>
        /// Returns the tightest world-space Bounds for a GameObject.
        /// Priority: Renderer.bounds → Collider.bounds → point at transform.position.
        /// </summary>
        internal static Bounds GetBounds(GameObject go)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer != null)
                return renderer.bounds;

            var col = go.GetComponentInChildren<Collider>();
            if (col != null)
                return col.bounds;

            return new Bounds(go.transform.position, Vector3.zero);
        }

        /// <summary>
        /// Returns the combined world-space Bounds encapsulating all objects.
        /// </summary>
        internal static Bounds GetCombinedBounds(IEnumerable<GameObject> objects)
        {
            bool initialised = false;
            Bounds combined = default;

            foreach (var go in objects)
            {
                Bounds b = GetBounds(go);
                if (!initialised)
                {
                    combined = b;
                    initialised = true;
                }
                else
                {
                    combined.Encapsulate(b);
                }
            }

            return combined;
        }

        /// <summary>
        /// Raycasts downward from above the object's bounds and returns the world-space Y
        /// at which the object's bounds bottom should sit.
        /// Returns false if no surface is found within maxDistance.
        /// </summary>
        internal static bool TryGetSurfaceY(GameObject go, LayerMask layerMask,
                                            float maxDistance, out float surfaceY)
        {
            Bounds bounds = GetBounds(go);

            // Cast from the top of the bounds + a small offset upward so we don't start inside geometry.
            Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);
            float halfHeight = bounds.extents.y;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDistance + halfHeight * 2f + 0.1f, layerMask))
            {
                surfaceY = hit.point.y;
                return true;
            }

            surfaceY = 0f;
            return false;
        }
    }
}
