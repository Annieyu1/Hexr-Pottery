using System;

namespace HexRMeta.Pressure
{
    public enum Side
    {
        Left,
        Right
    }

    /// <summary>
    /// Every tunable number of the pressure demo in one place. Held by PressureGameController
    /// so it shows up in the Inspector; plain C# so the logic classes can use it without Unity.
    /// </summary>
    [Serializable]
    public class PressureSettings
    {
        // ---- Session ----
        public string participantId = "P000";
        public string hand = "affected";            // affected / unaffected / both
        [UnityEngine.Min(0f)]
        public float sessionLimitSeconds = 600f;    // 0 = no limit

        // ---- Stimulus ----
        [UnityEngine.Range(1, 6)]
        public int difficultyLevel = 3;             // index into deltaTable, 1-based
        public float[] baseStrengths = { 25f, 30f, 35f };
        public float[] deltaTable = { 25f, 18f, 12f, 8f, 5f, 3f };
        public float minStrength = 10f;             // HexR usable range
        public float maxStrength = 60f;
        public int maxSameSideRun = 3;
        public int randomSeed = -1;                 // -1 = time based

        // ---- Timing (seconds) ----
        public float confirmDwell = 0.5f;           // released inside the zone this long = answer
        public float returnDelay = 0.5f;            // released outside the zone, then goes home
        public float nextTrialDelay = 2f;           // feedback time
        public float hintIdleSeconds = 10f;         // no grab for this long = show hint
    }
}
