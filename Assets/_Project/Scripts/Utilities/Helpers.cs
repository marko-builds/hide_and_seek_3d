using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utilities
{
    public static class Helpers
    {
        public static WaitForSeconds GetWaitForSeconds(float seconds)
        {
            return WaitFor.Seconds(seconds);
        }
#if UNITY_EDITOR
        public static void QuitGame()
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }

        public static void ClearConsole()
        {
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method?.Invoke(new object(), null);
        }
#else
            Application.Quit();
#endif
    }
}