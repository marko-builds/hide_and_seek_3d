using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal class ComponentAuditTool : EditorWindow
    {
        private enum ScanScope { AllSceneObjects, SelectionOnly }

        [Serializable]
        private struct AuditRule
        {
            public string tag;
            public string componentTypeName;
            public string friendlyName;
        }

        private struct AuditViolation
        {
            public GameObject go;
            public AuditRule rule;
        }

        private ScanScope _scope = ScanScope.AllSceneObjects;
        private List<AuditRule> _rules = new List<AuditRule>
        {
            new AuditRule { tag = "Cover", componentTypeName = "UnityEngine.Collider", friendlyName = "Collider" }
        };

        private List<AuditViolation> _violations = new List<AuditViolation>();
        private ReorderableList _reorderableList;
        private Vector2 _violationsScroll;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Component Audit")]
        private static void Open()
        {
            GetWindow<ComponentAuditTool>("Component Audit");
        }

        private void OnEnable()
        {
            BuildReorderableList();
        }

        private void BuildReorderableList()
        {
            _reorderableList = new ReorderableList(_rules, typeof(AuditRule), true, true, true, true);

            _reorderableList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Audit Rules  (Tag → Required Component)");

            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var rule = _rules[index];
                float third = rect.width / 3f - 4f;
                float y = rect.y + 2f;
                float h = EditorGUIUtility.singleLineHeight;

                rule.tag               = EditorGUI.TextField(new Rect(rect.x, y, third, h), rule.tag);
                rule.componentTypeName = EditorGUI.TextField(new Rect(rect.x + third + 4f, y, third, h), rule.componentTypeName);
                rule.friendlyName      = EditorGUI.TextField(new Rect(rect.x + (third + 4f) * 2f, y, third, h), rule.friendlyName);
                _rules[index] = rule;
            };

            _reorderableList.onAddCallback = list =>
                _rules.Add(new AuditRule { tag = "Tag", componentTypeName = "UnityEngine.Component", friendlyName = "Component" });
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Component Audit Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scope = (ScanScope)EditorGUILayout.EnumPopup("Scan Scope", _scope);
            EditorGUILayout.Space();

            if (_reorderableList == null)
                BuildReorderableList();

            _reorderableList.DoLayoutList();
            EditorGUILayout.Space();

            if (GUILayout.Button("Run Audit"))
                RunAudit();

            EditorGUILayout.Space();

            // Violations list
            if (_violations.Count > 0)
            {
                EditorGUILayout.LabelField($"Violations ({_violations.Count})", EditorStyles.boldLabel);
                _violationsScroll = EditorGUILayout.BeginScrollView(_violationsScroll, GUILayout.MaxHeight(300f));

                foreach (var v in _violations)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"{v.go.name}  — missing {v.rule.friendlyName} [{v.rule.tag}]");
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = v.go;
                        EditorGUIUtility.PingObject(v.go);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else if (_violations.Count == 0 && Event.current.type == EventType.Repaint)
            {
                // Only show "no violations" after at least one audit has run
                // (tracked implicitly — empty list on first open is fine to show nothing)
            }
        }

        private void RunAudit()
        {
            _violations.Clear();

            GameObject[] candidates = GatherCandidates();

            foreach (var go in candidates)
            {
                foreach (var rule in _rules)
                {
                    if (string.IsNullOrEmpty(rule.tag) || !go.CompareTag(rule.tag))
                        continue;

                    Type type = ResolveType(rule.componentTypeName);
                    if (type == null)
                    {
                        Debug.LogWarning($"[ComponentAudit] Could not resolve type '{rule.componentTypeName}'.");
                        continue;
                    }

                    if (go.GetComponent(type) == null)
                    {
                        _violations.Add(new AuditViolation { go = go, rule = rule });
                        Debug.LogWarning(
                            $"[ComponentAudit] '{go.name}' (tag='{rule.tag}') is missing {rule.friendlyName} ({rule.componentTypeName}).",
                            go);
                    }
                }
            }

            if (_violations.Count == 0)
                Debug.Log("[ComponentAudit] No violations found.");

            Repaint();
        }

        private GameObject[] GatherCandidates()
        {
            if (_scope == ScanScope.SelectionOnly)
                return Selection.gameObjects;

            return UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // Direct lookup
            Type t = Type.GetType(typeName);
            if (t != null) return t;

            // Search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = assembly.GetType(typeName);
                if (t != null) return t;
            }

            return null;
        }
    }
}
