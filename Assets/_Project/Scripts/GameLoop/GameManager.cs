using System;
using UnityEngine;
using Utilities;

namespace HideAndSeek
{
    /// <summary>
    /// Cross-scene Singleton. Owns the top-level game FSM: Warmup → Playing → (Win | Lose).
    ///
    /// Static events (OnWin / OnLose) allow UI panels to subscribe without needing a
    /// direct reference to the instance, avoiding null-ref timing issues.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        public enum GameState { Warmup, Playing, Win, Lose }

        // ── Instance event (state changes) ────────────────────────────────────────

        public event Action<GameState> OnGameStateChanged;

        // ── Static events (UI convenience) ────────────────────────────────────────

        /// <summary>Fired when the game transitions to Win. Safe to subscribe before Instance is ready.</summary>
        public static event Action OnWin;

        /// <summary>Fired when the game transitions to Lose. Safe to subscribe before Instance is ready.</summary>
        public static event Action OnLose;

        // ── State ─────────────────────────────────────────────────────────────────

        public GameState CurrentState { get; private set; } = GameState.Warmup;

        // ── API ───────────────────────────────────────────────────────────────────

        public void StartRound()
        {
            if (CurrentState != GameState.Warmup) return;
            ChangeState(GameState.Playing);
        }

        public void TriggerWin()
        {
            if (CurrentState != GameState.Playing) return;
            OnWin?.Invoke();
            ChangeState(GameState.Win);
        }

        public void TriggerLose()
        {
            if (CurrentState != GameState.Playing) return;
            OnLose?.Invoke();
            ChangeState(GameState.Lose);
        }

        // ── Private ───────────────────────────────────────────────────────────────

        void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
