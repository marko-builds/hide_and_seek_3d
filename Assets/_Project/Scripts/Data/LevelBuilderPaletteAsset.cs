using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek
{
    [CreateAssetMenu(fileName = "LevelBuilderPalette", menuName = "HideAndSeek/Level Builder Palette")]
    public class LevelBuilderPaletteAsset : ScriptableObject
    {
        public List<PrefabCategory> categories = new List<PrefabCategory>();

        [Serializable]
        public class PrefabCategory
        {
            public string name = "New Category";
            public List<GameObject> prefabs = new List<GameObject>();
        }
    }
}
