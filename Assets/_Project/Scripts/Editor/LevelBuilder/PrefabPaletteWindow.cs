using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Editor
{
    internal class PrefabPaletteWindow : EditorWindow
    {
        private const float k_ThumbnailSize = 72f;
        private const float k_MaxDropDistance = 1000f;

        private LevelBuilderPaletteAsset _paletteAsset;
        private int _selectedCategory;
        private float _rotationSnap = 45f;
        private LayerMask _surfaceLayer = ~0;

        private GameObject _activePrefab;
        private float _placementYRotation;
        private Vector2 _paletteScroll;

        [MenuItem(LevelBuilderUtility.k_MenuRoot + "Prefab Palette")]
        private static void Open()
        {
            GetWindow<PrefabPaletteWindow>("Prefab Palette");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _activePrefab = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Prefab Palette", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Palette asset field
            var newAsset = (LevelBuilderPaletteAsset)EditorGUILayout.ObjectField(
                "Palette Asset", _paletteAsset, typeof(LevelBuilderPaletteAsset), false);

            if (newAsset != _paletteAsset)
            {
                _paletteAsset = newAsset;
                _selectedCategory = 0;
                _activePrefab = null;
            }

            _rotationSnap  = EditorGUILayout.FloatField("Rotation Snap (°)", _rotationSnap);
            _surfaceLayer  = EditorGUILayout.MaskField("Surface Layer", _surfaceLayer, UnityEditorInternal.InternalEditorUtility.layers);

            if (_paletteAsset == null || _paletteAsset.categories == null || _paletteAsset.categories.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign a Palette Asset with at least one category.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();

            // Category tabs
            string[] tabLabels = new string[_paletteAsset.categories.Count];
            for (int i = 0; i < _paletteAsset.categories.Count; i++)
                tabLabels[i] = _paletteAsset.categories[i].name;

            _selectedCategory = GUILayout.Toolbar(_selectedCategory, tabLabels);

            if (_selectedCategory >= _paletteAsset.categories.Count)
                _selectedCategory = 0;

            EditorGUILayout.Space();

            // Active prefab label
            if (_activePrefab != null)
            {
                EditorGUILayout.HelpBox($"Placing: {_activePrefab.name}  |  Scroll to rotate  |  ESC/RMB to cancel", MessageType.Info);
            }

            // Prefab grid
            var category = _paletteAsset.categories[_selectedCategory];
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);
            DrawPrefabGrid(category);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabGrid(LevelBuilderPaletteAsset.PrefabCategory category)
        {
            if (category.prefabs == null || category.prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No prefabs in this category.", MessageType.None);
                return;
            }

            float windowWidth = position.width - 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(windowWidth / (k_ThumbnailSize + 8f)));

            int col = 0;
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < category.prefabs.Count; i++)
            {
                var prefab = category.prefabs[i];
                if (prefab == null) continue;

                bool isActive = prefab == _activePrefab;

                EditorGUILayout.BeginVertical(GUILayout.Width(k_ThumbnailSize));

                // Thumbnail
                Texture2D preview = AssetPreview.GetAssetPreview(prefab);
                GUIStyle btnStyle = isActive
                    ? new GUIStyle(GUI.skin.button) { normal = { background = MakeColorTex(new Color(0.3f, 0.6f, 1f, 0.4f)) } }
                    : GUI.skin.button;

                if (GUILayout.Button(preview != null ? new GUIContent(preview) : new GUIContent(prefab.name),
                                     btnStyle, GUILayout.Width(k_ThumbnailSize), GUILayout.Height(k_ThumbnailSize)))
                {
                    _activePrefab = isActive ? null : prefab;
                    _placementYRotation = 0f;
                    SceneView.RepaintAll();
                }

                EditorGUILayout.LabelField(prefab.name, EditorStyles.centeredGreyMiniLabel,
                                           GUILayout.Width(k_ThumbnailSize));
                EditorGUILayout.EndVertical();

                col++;
                if (col >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (_activePrefab == null) return;

            Event e = Event.current;

            // ESC or right-click cancels placement
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _activePrefab = null;
                Repaint();
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                _activePrefab = null;
                Repaint();
                e.Use();
                return;
            }

            // Scroll to rotate
            if (e.type == EventType.ScrollWheel)
            {
                _placementYRotation += Mathf.Sign(e.delta.y) * _rotationSnap;
                e.Use();
                sv.Repaint();
            }

            // Raycast to find placement point
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hitSurface = Physics.Raycast(ray, out RaycastHit hit, k_MaxDropDistance, _surfaceLayer);

            if (hitSurface)
            {
                // Draw ghost cube at hit point
                Handles.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                Handles.DrawWireCube(hit.point + Vector3.up * 0.5f, Vector3.one);

                // Left-click to place
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    PlacePrefab(hit.point);
                    e.Use();
                }
            }

            // Keep scene view consuming mouse events so we don't lose focus
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        private void PlacePrefab(Vector3 point)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_activePrefab);
            instance.transform.rotation = Quaternion.Euler(0f, _placementYRotation, 0f);

            // Surface-snap: use bounds to land flush
            if (LevelBuilderUtility.TryGetSurfaceY(instance, _surfaceLayer, k_MaxDropDistance, out float surfaceY))
            {
                Bounds b = LevelBuilderUtility.GetBounds(instance);
                float halfHeight = instance.transform.position.y - b.min.y;
                instance.transform.position = new Vector3(point.x, surfaceY + halfHeight, point.z);
            }
            else
            {
                instance.transform.position = point;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Prefab");
            Selection.activeGameObject = instance;
        }

        private static Texture2D MakeColorTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
