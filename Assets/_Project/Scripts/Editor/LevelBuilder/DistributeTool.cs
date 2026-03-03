using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal class DistributeTool : EditorWindow
    {
        private enum Axis { X, Y, Z }
        private enum SpacingMode { EqualGaps, EqualCenters, Custom }

        private Axis _axis = Axis.X;
        private SpacingMode _spacingMode = SpacingMode.EqualGaps;
        private float _customSpacing = 0.5f;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Distribute")]
        private static void Open()
        {
            GetWindow<DistributeTool>("Distribute");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Distribute Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _axis = (Axis)EditorGUILayout.EnumPopup("Axis", _axis);
            _spacingMode = (SpacingMode)EditorGUILayout.EnumPopup("Spacing Mode", _spacingMode);

            if (_spacingMode == SpacingMode.Custom)
                _customSpacing = EditorGUILayout.FloatField("Custom Spacing", _customSpacing);

            EditorGUILayout.Space();

            GUI.enabled = Selection.gameObjects.Length >= 3;
            if (GUILayout.Button("Distribute"))
                DistributeSelection();
            GUI.enabled = true;

            if (Selection.gameObjects.Length < 3)
                EditorGUILayout.HelpBox("Select at least 3 objects.", MessageType.Info);
        }

        private void DistributeSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length < 3)
            {
                Debug.LogWarning("[DistributeTool] Select at least 3 objects.");
                return;
            }

            // Sort by bounds center on chosen axis
            var sorted = new List<GameObject>(selection);
            sorted.Sort((a, b) =>
                GetAxisValue(LevelBuilderUtility.GetBounds(a).center, _axis)
                    .CompareTo(GetAxisValue(LevelBuilderUtility.GetBounds(b).center, _axis)));

            var transforms = new Object[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
                transforms[i] = sorted[i].transform;

            Undo.RecordObjects(transforms, "Distribute");

            int n = sorted.Count;

            switch (_spacingMode)
            {
                case SpacingMode.EqualGaps:
                    DistributeEqualGaps(sorted, n);
                    break;
                case SpacingMode.EqualCenters:
                    DistributeEqualCenters(sorted, n);
                    break;
                case SpacingMode.Custom:
                    DistributeCustom(sorted, n);
                    break;
            }
        }

        private void DistributeEqualGaps(List<GameObject> sorted, int n)
        {
            Bounds[] bounds = GetAllBounds(sorted);
            float firstMin = GetAxisValue(bounds[0].min, _axis);
            float lastMax  = GetAxisValue(bounds[n - 1].max, _axis);

            float sumSizes = 0f;
            for (int i = 0; i < n; i++)
                sumSizes += GetAxisValue(bounds[i].size, _axis);

            float totalRoom = lastMax - firstMin - sumSizes;
            float gap = (n > 1) ? totalRoom / (n - 1) : 0f;

            float cursor = firstMin;
            for (int i = 0; i < n; i++)
            {
                float size = GetAxisValue(bounds[i].size, _axis);
                float currentMin = GetAxisValue(bounds[i].min, _axis);
                float delta = cursor - currentMin;
                sorted[i].transform.position += AxisVector(_axis) * delta;
                cursor += size + gap;
            }
        }

        private void DistributeEqualCenters(List<GameObject> sorted, int n)
        {
            Bounds[] bounds = GetAllBounds(sorted);
            float firstCenter = GetAxisValue(bounds[0].center, _axis);
            float lastCenter  = GetAxisValue(bounds[n - 1].center, _axis);

            float step = (n > 1) ? (lastCenter - firstCenter) / (n - 1) : 0f;

            for (int i = 0; i < n; i++)
            {
                float targetCenter = firstCenter + i * step;
                float currentCenter = GetAxisValue(bounds[i].center, _axis);
                float delta = targetCenter - currentCenter;
                sorted[i].transform.position += AxisVector(_axis) * delta;
            }
        }

        private void DistributeCustom(List<GameObject> sorted, int n)
        {
            Bounds[] bounds = GetAllBounds(sorted);
            float cursor = GetAxisValue(bounds[0].min, _axis);

            for (int i = 0; i < n; i++)
            {
                float size = GetAxisValue(bounds[i].size, _axis);
                float currentMin = GetAxisValue(bounds[i].min, _axis);
                float delta = cursor - currentMin;
                sorted[i].transform.position += AxisVector(_axis) * delta;
                cursor += size + _customSpacing;
            }
        }

        private static Bounds[] GetAllBounds(List<GameObject> objects)
        {
            var result = new Bounds[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                result[i] = LevelBuilderUtility.GetBounds(objects[i]);
            return result;
        }

        private static float GetAxisValue(Vector3 v, Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return v.x;
                case Axis.Y: return v.y;
                case Axis.Z: return v.z;
                default:     return 0f;
            }
        }

        private static Vector3 AxisVector(Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return Vector3.right;
                case Axis.Y: return Vector3.up;
                case Axis.Z: return Vector3.forward;
                default:     return Vector3.zero;
            }
        }
    }
}
