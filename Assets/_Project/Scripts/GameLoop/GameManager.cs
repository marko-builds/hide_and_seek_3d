using System;
using UnityEngine;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// Cross-scene Singleton. Owns the top-level game FSM:
    /// Warmup → Playing → (Win | Lose).
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        public enum GameState { Warmup, Playing, Win, Lose }

        public event Action<GameState> OnGameStateChanged;

        public GameState CurrentState { get; private set; } = GameState.Warmup;

        public void StartRound()
        {
            ChangeState(GameState.Playing);
        }

        public void TriggerWin()
        {
            if (CurrentState != GameState.Playing) return;
            ChangeState(GameState.Win);
        }

        public void TriggerLose()
        {
            if (CurrentState != GameState.Playing) return;
            ChangeState(GameState.Lose);
        }

        private void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
