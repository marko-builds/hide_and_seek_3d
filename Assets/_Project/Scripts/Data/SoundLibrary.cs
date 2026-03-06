using System;
using UnityEngine;

namespace HideAndSeek
{
    public enum SoundID
    {
        // Player
        PlayerFootstepWalk,
        PlayerFootstepSprint,
        PlayerFootstepCrouch,
        PlayerHide,
        PlayerExitHide,

        // Enemy
        EnemyAlert,
        EnemyInvestigate,
        EnemyChase,
        EnemyLostTarget,

        // Objective
        ObjectiveCollect,

        // Level Exit
        ExitUnlocked,
        ExitUsed,

        // Phase Transition
        Phase2Start,

        // UI / Game
        RoundStart,
        RoundWin,
        RoundLose,
    }

    [Serializable]
    public struct SoundEntry
    {
        public SoundID id;
        public SoundData data;
    }

    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "HideAndSeek/Data/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [SerializeField] private SoundEntry[] _entries;

        private System.Collections.Generic.Dictionary<SoundID, SoundData> _lookup;

        public SoundData Get(SoundID id)
        {
            if (_lookup == null)
                BuildLookup();

            _lookup.TryGetValue(id, out var data);
            return data;
        }

        private void BuildLookup()
        {
            _lookup = new System.Collections.Generic.Dictionary<SoundID, SoundData>(_entries.Length);
            foreach (var entry in _entries)
                _lookup[entry.id] = entry.data;
        }
    }
}
