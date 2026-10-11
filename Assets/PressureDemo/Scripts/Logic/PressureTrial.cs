namespace HexRMeta.Pressure
{
    /// <summary>
    /// One trial: the stimulus (set up front) and the response (filled in afterwards).
    /// Plain data, no Unity types, so it can move into a shared Core later.
    /// </summary>
    public class PressureTrial
    {
        // ---- Stimulus ----
        public int index;
        public int difficulty;
        public float baseStrength;      // S
        public float comparisonStrength; // C = S + delta
        public float delta;
        public Side strongerSide;
        public float strengthLeft;
        public float strengthRight;

        // ---- Response ----
        public bool answered;
        public Side chosenSide;
        public bool correct;
        public int changedMindCount;

        // ---- Timing (seconds of active play time, pauses excluded) ----
        public float tTrialStart;
        public float tFirstGrab = -1f;
        public float tAnswer;
        public float ReactionSeconds => tFirstGrab < 0f ? -1f : tAnswer - tFirstGrab;

        // ---- Exploration ----
        public int grabsLeft;
        public int grabsRight;
        public float holdSecondsLeft;
        public float holdSecondsRight;

        // ---- Flags ----
        public bool hintShown;
        public int autoReturned;
        public bool disconnected;       // TODO: fill in once glove connection events are hooked up

        public string wallClockStartUtc;

        public Side Opposite(Side s) => s == Side.Left ? Side.Right : Side.Left;
    }
}
