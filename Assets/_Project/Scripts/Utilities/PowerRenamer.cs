using System.Reflection;
using UnityEditor;
using UnityEngine;

public class PowerRenamer : MonoBehaviour
{
    [MenuItem("Edit/Toggle Rename #&r")]
    public static void Rename()
    {
        int count = 1;
        var selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0) return;

        foreach (var obj in selectedObjects)
        {
            var propInfo = obj.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
            if (propInfo == null) continue;

            var currentName = (string)propInfo.GetValue(obj, null);
            propInfo.SetValue(obj, "Shield_" + count++, null);
        }
    }
}