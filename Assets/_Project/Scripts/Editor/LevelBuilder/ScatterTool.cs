using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal class ScatterTool : EditorWindow
    {
        private List<GameObject> _prefabs = new List<GameObject>();
        private Transform _boundsSource;
        private Vector3 _manualCenter = Vector3.zero;
        private Vector3 _manualSize = new Vector3(10f, 5f, 10f);
        private int _count = 10;
        private Vector2 _yRotationRange = new Vector2(0f, 360f);
        private LayerMask _surfaceLayer = ~0;
        private bool _surfaceSnap = true;
        private bool _avoidOverlaps = true;
        private int _seed = 0;
        private Transform _parentTransform;

        private Vector2 _scrollPos;
        private SerializedObject _serializedObject;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Scatter")]
        private static void Open()
        {
            GetWindow<ScatterTool>("Scatter Tool");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scatter Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Prefabs list
            EditorGUILayout.LabelField("Prefabs", EditorStyles.miniBoldLabel);
            DrawPrefabList();
            EditorGUILayout.Space();

            // Zone
            EditorGUILayout.LabelField("Scatter Zone", EditorStyles.miniBoldLabel);
            _boundsSource = (Transform)EditorGUILayout.ObjectField("Bounds Source (optional)", _boundsSource, typeof(Transform), true);
            if (_boundsSource == null)
            {
                _manualCenter = EditorGUILayout.Vector3Field("Manual Center", _manualCenter);
                _manualSize   = EditorGUILayout.Vector3Field("Manual Size", _manualSize);
            }
            EditorGUILayout.Space();

            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.miniBoldLabel);
            _count          = EditorGUILayout.IntField("Count", _count);
            _yRotationRange = EditorGUILayout.Vector2Field("Y Rotation Range (min/max)", _yRotationRange);
            _surfaceSnap    = EditorGUILayout.Toggle("Surface Snap", _surfaceSnap);
            if (_surfaceSnap)
                _surfaceLayer = EditorGUILayout.MaskField("Surface Layer", _surfaceLayer, UnityEditorInternal.InternalEditorUtility.layers);
            _avoidOverlaps   = EditorGUILayout.Toggle("Avoid Overlaps", _avoidOverlaps);
            _seed            = EditorGUILayout.IntField("Seed (0 = random)", _seed);
            _parentTransform = (Transform)EditorGUILayout.ObjectField("Parent Transform", _parentTransform, typeof(Transform), true);

            EditorGUILayout.Space();

            bool hasPrefabs = _prefabs.Count > 0 && _prefabs.Exists(p => p != null);
            GUI.enabled = hasPrefabs;
            if (GUILayout.Button("Scatter"))
                Scatter();
            GUI.enabled = true;

            if (!hasPrefabs)
                EditorGUILayout.HelpBox("Add at least one prefab.", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabList()
        {
            for (int i = 0; i < _prefabs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _prefabs[i] = (GameObject)EditorGUILayout.ObjectField(_prefabs[i], typeof(GameObject), false);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    _prefabs.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Prefab"))
                _prefabs.Add(null);
        }

        private void Scatter()
        {
            // Collect valid prefabs
            var validPrefabs = new List<GameObject>();
            foreach (var p in _prefabs)
                if (p != null) validPrefabs.Add(p);

            if (validPrefabs.Count == 0)
            {
                Debug.LogWarning("[ScatterTool] No valid prefabs assigned.");
                return;
            }

            // Resolve zone
            Bounds zone = ResolveZone();

            // Seed
            Random.State savedState = Random.state;
            if (_seed != 0)
                Random.InitState(_seed);

            var placedBounds = new List<Bounds>();
            int placed = 0;
            int attempts = 0;
            int maxAttempts = _count * 3;

            while (placed < _count && attempts < maxAttempts)
            {
                attempts++;

                // Random XZ within zone, Y at zone top
                float x = Random.Range(zone.min.x, zone.max.x);
                float z = Random.Range(zone.min.z, zone.max.z);
                float y = zone.max.y;
                Vector3 position = new Vector3(x, y, z);

                // Surface snap
                if (_surfaceSnap)
                {
                    Ray ray = new Ray(new Vector3(x, zone.max.y + 0.1f, z), Vector3.down);
                    if (Physics.Raycast(ray, out RaycastHit hit, zone.size.y + 0.2f, _surfaceLayer))
                        position.y = hit.point.y;
                    else
                        continue;
                }

                // Pick random prefab
                GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
                float rotation = Random.Range(_yRotationRange.x, _yRotationRange.y);

                // Instantiate
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

                // Overlap check
                if (_avoidOverlaps)
                {
                    Bounds instanceBounds = LevelBuilderUtility.GetBounds(instance);
                    bool overlaps = false;
                    foreach (var existing in placedBounds)
                    {
                        if (existing.Intersects(instanceBounds))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (overlaps)
                    {
                        DestroyImmediate(instance);
                        continue;
                    }

                    placedBounds.Add(instanceBounds);
                }

                // Register undo and parent
                Undo.RegisterCreatedObjectUndo(instance, "Scatter");

                if (_parentTransform != null)
                    instance.transform.SetParent(_parentTransform, true);

                placed++;
            }

            if (_seed != 0)
                Random.state = savedState;

            Debug.Log($"[ScatterTool] Placed {placed}/{_count} objects ({attempts} attempts).");
        }

        private Bounds ResolveZone()
        {
            if (_boundsSource != null)
            {
                var renderer = _boundsSource.GetComponent<Renderer>();
                if (renderer != null) return renderer.bounds;

                var col = _boundsSource.GetComponent<Collider>();
                if (col != null) return col.bounds;
            }

            return new Bounds(_manualCenter, _manualSize);
        }
    }
}
