using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal static class OverlapChecker
    {
        private const string k_PrefScopeName = "LevelBuilder.OverlapChecker.Scope";
        private const string k_PrefToleranceName = "LevelBuilder.OverlapChecker.Tolerance";
        private const int k_SlownessThreshold = 500;

        private enum CheckScope { AllSceneObjects, SelectionOnly }

        private static List<GameObject> s_Offenders = new List<GameObject>();

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Check Overlaps")]
        private static void RunCheck()
        {
            CheckScope scope = (CheckScope)EditorPrefs.GetInt(k_PrefScopeName, 0);
            float tolerance = EditorPrefs.GetFloat(k_PrefToleranceName, 0.001f);

            // Gather candidates
            GameObject[] candidates = GatherCandidates(scope);

            if (candidates.Length > k_SlownessThreshold)
                Debug.LogWarning($"[OverlapChecker] Scene contains {candidates.Length} objects — O(n²) check may be slow.");

            s_Offenders.Clear();
            var offenderSet = new HashSet<GameObject>();

            for (int i = 0; i < candidates.Length; i++)
            {
                Bounds bi = LevelBuilderUtility.GetBounds(candidates[i]);
                bi.Expand(-tolerance);

                for (int j = i + 1; j < candidates.Length; j++)
                {
                    Bounds bj = LevelBuilderUtility.GetBounds(candidates[j]);
                    bj.Expand(-tolerance);

                    if (bi.Intersects(bj))
                    {
                        Debug.LogWarning(
                            $"[OverlapChecker] Overlap detected: '{candidates[i].name}' ↔ '{candidates[j].name}'",
                            candidates[i]);

                        offenderSet.Add(candidates[i]);
                        offenderSet.Add(candidates[j]);
                    }
                }
            }

            s_Offenders.AddRange(offenderSet);

            if (s_Offenders.Count == 0)
            {
                Debug.Log("[OverlapChecker] No overlaps found.");
            }
            else
            {
                Debug.LogWarning($"[OverlapChecker] Found {s_Offenders.Count} overlapping object(s). See Console for details.");
                // Register one-shot SceneView callback
                SceneView.duringSceneGui -= DrawOffenders;
                SceneView.duringSceneGui += DrawOffenders;
                SceneView.RepaintAll();
            }
        }

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Check Overlaps", true)]
        private static bool RunCheckValidate() => true;

        private static void DrawOffenders(SceneView sv)
        {
            if (s_Offenders.Count == 0)
            {
                SceneView.duringSceneGui -= DrawOffenders;
                return;
            }

            Handles.color = Color.red;
            foreach (var go in s_Offenders)
            {
                if (go == null) continue;
                Bounds b = LevelBuilderUtility.GetBounds(go);
                Handles.DrawWireCube(b.center, b.size);
            }
        }

        private static GameObject[] GatherCandidates(CheckScope scope)
        {
            if (scope == CheckScope.SelectionOnly)
                return Selection.gameObjects;

            // All scene objects with a MeshRenderer or Collider
            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var filtered = new List<GameObject>();
            foreach (var go in all)
            {
                if (go.GetComponent<MeshRenderer>() != null || go.GetComponent<Collider>() != null)
                    filtered.Add(go);
            }
            return filtered.ToArray();
        }
    }
}
