using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityUtils
{
    public static class EditorExtensions
    {
        public static bool ConfirmOverwrite(this string path)
        {
            if (File.Exists(path))
            {
                return EditorUtility.DisplayDialog
                (
                    "File Exists",
                    "The file already exists at the specified path. Do you want to overwrite it?",
                    "Yes",
                    "No"
                );
            }

            return true;
        }


        public static string BrowseForFolder(this string defaultPath)
        {
            return EditorUtility.SaveFolderPanel
            (
                "Choose Save Path",
                defaultPath,
                ""
            );
        }


        public static void PingAndSelect(this Object asset)
        {
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}