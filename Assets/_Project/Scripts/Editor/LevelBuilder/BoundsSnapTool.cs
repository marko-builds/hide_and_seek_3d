using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal class BoundsSnapTool : EditorWindow
    {
        private enum SnapFace { Left, Right, Up, Down, Forward, Back }
        private enum AnchorMode { FirstSelected, LargestBounds }

        private SnapFace _snapFace = SnapFace.Right;
        private AnchorMode _anchorMode = AnchorMode.FirstSelected;
        private float _padding = 0f;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Bounds Snap")]
        private static void Open()
        {
            GetWindow<BoundsSnapTool>("Bounds Snap");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bounds Snap Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _anchorMode = (AnchorMode)EditorGUILayout.EnumPopup("Anchor Mode", _anchorMode);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Snap Face");
            DrawFaceGrid();
            EditorGUILayout.Space();

            _padding = EditorGUILayout.FloatField("Padding", _padding);
            EditorGUILayout.Space();

            GUI.enabled = Selection.gameObjects.Length >= 2;
            if (GUILayout.Button("Snap"))
                SnapSelection();
            GUI.enabled = true;

            if (Selection.gameObjects.Length < 2)
                EditorGUILayout.HelpBox("Select at least 2 objects.", MessageType.Info);
        }

        private void DrawFaceGrid()
        {
            // 3-column grid: Left, [Up/Down row], Right with Forward/Back
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(_snapFace == SnapFace.Left, "Left", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
                _snapFace = SnapFace.Left;

            EditorGUILayout.BeginVertical();
            if (GUILayout.Toggle(_snapFace == SnapFace.Up, "Up", EditorStyles.miniButton))
                _snapFace = SnapFace.Up;
            if (GUILayout.Toggle(_snapFace == SnapFace.Forward, "Fwd", EditorStyles.miniButton))
                _snapFace = SnapFace.Forward;
            if (GUILayout.Toggle(_snapFace == SnapFace.Back, "Back", EditorStyles.miniButton))
                _snapFace = SnapFace.Back;
            if (GUILayout.Toggle(_snapFace == SnapFace.Down, "Down", EditorStyles.miniButton))
                _snapFace = SnapFace.Down;
            EditorGUILayout.EndVertical();

            if (GUILayout.Toggle(_snapFace == SnapFace.Right, "Right", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                _snapFace = SnapFace.Right;

            EditorGUILayout.EndHorizontal();
        }

        private void SnapSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length < 2)
            {
                Debug.LogWarning("[BoundsSnapTool] Select at least 2 objects.");
                return;
            }

            // Determine anchor
            GameObject anchor = ResolveAnchor(selection);

            // Collect non-anchor objects
            var targets = new System.Collections.Generic.List<Object>();
            foreach (var go in selection)
            {
                if (go != anchor)
                    targets.Add(go.transform);
            }

            Undo.RecordObjects(targets.ToArray(), "Bounds Snap");

            Bounds anchorBounds = LevelBuilderUtility.GetBounds(anchor);

            foreach (var go in selection)
            {
                if (go == anchor) continue;

                Bounds b = LevelBuilderUtility.GetBounds(go);
                Vector3 delta = ComputeSnapDelta(anchorBounds, b, _snapFace, _padding);
                go.transform.position += delta;
            }
        }

        private GameObject ResolveAnchor(GameObject[] selection)
        {
            if (_anchorMode == AnchorMode.FirstSelected)
                return selection[0];

            // Largest bounds by volume
            GameObject largest = selection[0];
            float maxVolume = LevelBuilderUtility.GetBounds(selection[0]).size.sqrMagnitude;

            for (int i = 1; i < selection.Length; i++)
            {
                float vol = LevelBuilderUtility.GetBounds(selection[i]).size.sqrMagnitude;
                if (vol > maxVolume)
                {
                    maxVolume = vol;
                    largest = selection[i];
                }
            }

            return largest;
        }

        private static Vector3 ComputeSnapDelta(Bounds anchor, Bounds target, SnapFace face, float padding)
        {
            switch (face)
            {
                case SnapFace.Right:
                    // Move target so its left face touches anchor's right face
                    return new Vector3(anchor.max.x + padding - target.min.x, 0f, 0f);
                case SnapFace.Left:
                    // Move target so its right face touches anchor's left face
                    return new Vector3(anchor.min.x - padding - target.max.x, 0f, 0f);
                case SnapFace.Up:
                    return new Vector3(0f, anchor.max.y + padding - target.min.y, 0f);
                case SnapFace.Down:
                    return new Vector3(0f, anchor.min.y - padding - target.max.y, 0f);
                case SnapFace.Forward:
                    return new Vector3(0f, 0f, anchor.max.z + padding - target.min.z);
                case SnapFace.Back:
                    return new Vector3(0f, 0f, anchor.min.z - padding - target.max.z);
                default:
                    return Vector3.zero;
            }
        }
    }
}
