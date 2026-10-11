using System;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// Produces the stimulus for each trial: a random base strength S, a comparison S + delta,
    /// and a random side for the stronger one (never more than maxSameSideRun in a row).
    /// Difficulty is fixed for the whole session (no adaptation yet).
    /// </summary>
    public class TrialGenerator
    {
        private readonly PressureSettings settings;
        private readonly Random rng;
        private Side lastSide;
        private int sameSideRun;
        private bool hasLast;

        public TrialGenerator(PressureSettings settings)
        {
            this.settings = settings;
            rng = settings.randomSeed >= 0 ? new Random(settings.randomSeed) : new Random();
        }

        public PressureTrial Next(int index)
        {
            int level = Math.Max(1, Math.Min(settings.difficultyLevel, settings.deltaTable.Length));
            float delta = settings.deltaTable[level - 1];

            float s = settings.baseStrengths[rng.Next(settings.baseStrengths.Length)];
            // Keep S + delta inside the glove's range.
            if (s + delta > settings.maxStrength)
                s = settings.maxStrength - delta;
            if (s < settings.minStrength)
                s = settings.minStrength;
            float c = s + delta;

            Side stronger = rng.Next(2) == 0 ? Side.Left : Side.Right;
            if (hasLast && sameSideRun >= settings.maxSameSideRun && stronger == lastSide)
                stronger = stronger == Side.Left ? Side.Right : Side.Left;

            if (hasLast && stronger == lastSide) sameSideRun++;
            else sameSideRun = 1;
            lastSide = stronger;
            hasLast = true;

            return new PressureTrial
            {
                index = index,
                difficulty = level,
                baseStrength = s,
                comparisonStrength = c,
                delta = delta,
                strongerSide = stronger,
                strengthLeft = stronger == Side.Left ? c : s,
                strengthRight = stronger == Side.Right ? c : s,
            };
        }
    }
}
