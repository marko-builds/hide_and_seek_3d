using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal class BoundsAlignTool : EditorWindow
    {
        private enum Axis { X, Y, Z }
        private enum AlignMode { Min, Center, Max }

        private Axis _axis = Axis.Y;
        private AlignMode _alignMode = AlignMode.Min;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Bounds Align")]
        private static void Open()
        {
            GetWindow<BoundsAlignTool>("Bounds Align");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bounds Align Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _axis = (Axis)EditorGUILayout.EnumPopup("Axis", _axis);
            _alignMode = (AlignMode)EditorGUILayout.EnumPopup("Align Mode", _alignMode);
            EditorGUILayout.Space();

            GUI.enabled = Selection.gameObjects.Length >= 2;
            if (GUILayout.Button("Align"))
                AlignSelection();
            GUI.enabled = true;

            if (Selection.gameObjects.Length < 2)
                EditorGUILayout.HelpBox("Select at least 2 objects.", MessageType.Info);
        }

        private void AlignSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length < 2)
            {
                Debug.LogWarning("[BoundsAlignTool] Select at least 2 objects.");
                return;
            }

            Bounds combined = LevelBuilderUtility.GetCombinedBounds(selection);
            float target = ResolveTarget(combined, _axis, _alignMode);

            var transforms = new Object[selection.Length];
            for (int i = 0; i < selection.Length; i++)
                transforms[i] = selection[i].transform;

            Undo.RecordObjects(transforms, "Bounds Align");

            foreach (var go in selection)
            {
                Bounds b = LevelBuilderUtility.GetBounds(go);
                float delta = target - GetEdgeValue(b, _axis, _alignMode);
                go.transform.position += AxisVector(_axis) * delta;
            }
        }

        private static float ResolveTarget(Bounds combined, Axis axis, AlignMode mode)
        {
            switch (mode)
            {
                case AlignMode.Min:    return GetAxisValue(combined.min, axis);
                case AlignMode.Center: return GetAxisValue(combined.center, axis);
                case AlignMode.Max:    return GetAxisValue(combined.max, axis);
                default:               return 0f;
            }
        }

        private static float GetEdgeValue(Bounds b, Axis axis, AlignMode mode)
        {
            switch (mode)
            {
                case AlignMode.Min:    return GetAxisValue(b.min, axis);
                case AlignMode.Center: return GetAxisValue(b.center, axis);
                case AlignMode.Max:    return GetAxisValue(b.max, axis);
                default:               return 0f;
            }
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
