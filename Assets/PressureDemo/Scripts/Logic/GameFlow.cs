using System;

namespace HexRMeta.Pressure
{
    public enum GameState
    {
        Idle,       // start menu
        Playing,
        Paused,
        Ended       // summary
    }

    public enum EndReason
    {
        None,
        TimeUp,
        Manual
    }

    /// <summary>
    /// The session state machine: Idle -> Playing <-> Paused -> Ended -> (Replay) Idle.
    /// Rejects transitions that make no sense, so the UI can call it freely.
    /// </summary>
    public class GameFlow
    {
        public GameState State { get; private set; } = GameState.Idle;
        public EndReason EndedBecause { get; private set; } = EndReason.None;
        public int PauseCount { get; private set; }

        public event Action<GameState, GameState> StateChanged;

        public bool Start()
        {
            if (State != GameState.Idle) return false;
            PauseCount = 0;
            EndedBecause = EndReason.None;
            Set(GameState.Playing);
            return true;
        }

        public bool Pause()
        {
            if (State != GameState.Playing) return false;
            PauseCount++;
            Set(GameState.Paused);
            return true;
        }

        public bool Resume()
        {
            if (State != GameState.Paused) return false;
            Set(GameState.Playing);
            return true;
        }

        public bool End(EndReason reason)
        {
            if (State != GameState.Playing && State != GameState.Paused) return false;
            EndedBecause = reason;
            Set(GameState.Ended);
            return true;
        }

        /// <summary>Back to the start menu for a fresh session.</summary>
        public bool Replay()
        {
            if (State != GameState.Ended) return false;
            Set(GameState.Idle);
            return true;
        }

        private void Set(GameState next)
        {
            GameState old = State;
            State = next;
            StateChanged?.Invoke(old, next);
        }
    }
}
