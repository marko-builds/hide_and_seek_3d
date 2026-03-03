using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal static class SurfaceDropTool
    {
        private const float k_MaxDropDistance = 1000f;
        private static readonly LayerMask k_DefaultSurfaceLayer = ~0;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Surface Drop %&d")]
        private static void DropSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            Undo.RecordObjects(GetTransforms(selection), "Surface Drop");

            foreach (var go in selection)
            {
                if (LevelBuilderUtility.TryGetSurfaceY(go, k_DefaultSurfaceLayer, k_MaxDropDistance, out float surfaceY))
                {
                    Bounds bounds = LevelBuilderUtility.GetBounds(go);
                    float delta = surfaceY - bounds.min.y;
                    go.transform.position += new Vector3(0f, delta, 0f);
                }
                else
                {
                    Debug.LogWarning($"[SurfaceDropTool] No surface found below '{go.name}'.", go);
                }
            }
        }

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Surface Drop %&d", true)]
        private static bool DropSelectionValidate()
        {
            return Selection.gameObjects.Length > 0;
        }

        private static Transform[] GetTransforms(GameObject[] objects)
        {
            var transforms = new Transform[objects.Length];
            for (int i = 0; i < objects.Length; i++)
                transforms[i] = objects[i].transform;
            return transforms;
        }
    }
}
