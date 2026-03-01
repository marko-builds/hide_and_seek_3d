using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SelectChildren : EditorWindow
{
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        HandleKeyInput();
    }

    static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        HandleKeyInput();
    }

    static void HandleKeyInput()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.C && !e.control && !e.alt && !e.shift)
        {
            SelectChildrenOfSelection();
            e.Use();
        }
    }

    static void SelectChildrenOfSelection()
    {
        Transform[] selectedTransforms = Selection.transforms;
        if (selectedTransforms.Length == 0)
            return;

        List<GameObject> childObjects = new List<GameObject>();

        foreach (Transform parent in selectedTransforms)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                childObjects.Add(parent.GetChild(i).gameObject);
            }
        }

        if (childObjects.Count > 0)
        {
            Selection.objects = childObjects.ToArray();
        }
    }
}