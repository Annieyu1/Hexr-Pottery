using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HexRMeta.Pressure
{
    [Serializable]
    public class SessionSummary
    {
        public int schema_version = 1;
        public string session_id;
        public string started_utc;
        public string participant_id;
        public string hand;
        public int difficulty;
        public string app_version;
        public float active_seconds;
        public int pause_count;
        public string end_reason;
        public int trials_completed;
        public int trials_correct;
        public bool abandoned_trial;    // a trial was in progress when the session ended
    }

    /// <summary>
    /// Writes one CSV row per trial (flushed immediately, so a crash loses nothing) and a
    /// session JSON at the end. Real participant data must never go into the repo.
    /// </summary>
    public class TrialLogger : IDisposable
    {
        public const string CsvHeader =
            "session_id,trial_index,difficulty,base_strength,comparison_strength,delta," +
            "strength_left,strength_right,stronger_side,chosen_side,correct,changed_mind_count," +
            "wall_start_utc,t_trial_start,t_first_grab,t_answer,rt_ms," +
            "grabs_left,grabs_right,hold_s_left,hold_s_right," +
            "hint_shown,auto_returned,disconnected";

        private readonly StreamWriter csv;

        public string SessionId { get; }
        public string Directory { get; }

        public TrialLogger(string rootDirectory, string participantId)
        {
            SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            Directory = Path.Combine(rootDirectory, participantId + "_" + SessionId);
            System.IO.Directory.CreateDirectory(Directory);

            csv = new StreamWriter(Path.Combine(Directory, "trials.csv"), false, new UTF8Encoding(false));
            csv.WriteLine(CsvHeader);
            csv.Flush();
        }

        public void AppendTrial(PressureTrial t)
        {
            csv.WriteLine(ToCsvRow(SessionId, t));
            csv.Flush();
        }

        public void WriteSummary(SessionSummary summary, string json)
        {
            File.WriteAllText(Path.Combine(Directory, "session.json"), json, new UTF8Encoding(false));
        }

        public static string ToCsvRow(string sessionId, PressureTrial t)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(sessionId).Append(',')
              .Append(t.index.ToString(c)).Append(',')
              .Append(t.difficulty.ToString(c)).Append(',')
              .Append(t.baseStrength.ToString("0.##", c)).Append(',')
              .Append(t.comparisonStrength.ToString("0.##", c)).Append(',')
              .Append(t.delta.ToString("0.##", c)).Append(',')
              .Append(t.strengthLeft.ToString("0.##", c)).Append(',')
              .Append(t.strengthRight.ToString("0.##", c)).Append(',')
              .Append(t.strongerSide).Append(',')
              .Append(t.chosenSide).Append(',')
              .Append(t.correct ? 1 : 0).Append(',')
              .Append(t.changedMindCount.ToString(c)).Append(',')
              .Append(t.wallClockStartUtc).Append(',')
              .Append(t.tTrialStart.ToString("0.000", c)).Append(',')
              .Append(t.tFirstGrab.ToString("0.000", c)).Append(',')
              .Append(t.tAnswer.ToString("0.000", c)).Append(',')
              .Append(t.ReactionSeconds < 0f ? "" : (t.ReactionSeconds * 1000f).ToString("0", c)).Append(',')
              .Append(t.grabsLeft.ToString(c)).Append(',')
              .Append(t.grabsRight.ToString(c)).Append(',')
              .Append(t.holdSecondsLeft.ToString("0.000", c)).Append(',')
              .Append(t.holdSecondsRight.ToString("0.000", c)).Append(',')
              .Append(t.hintShown ? 1 : 0).Append(',')
              .Append(t.autoReturned.ToString(c)).Append(',')
              .Append(t.disconnected ? 1 : 0);
            return sb.ToString();
        }

        public void Dispose()
        {
            csv?.Dispose();
        }
    }
}
