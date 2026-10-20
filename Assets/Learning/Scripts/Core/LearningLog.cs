using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HexRMeta.Learning
{
    [Serializable]
    public class LearningSessionSummary
    {
        public int schema_version = 1;
        public string session_id;
        public string participant_id;
        public string task;
        public string hand;
        public string mode;
        public string app_version;
        public string started_local;
        public float active_seconds;
        public int rounds_completed;
        public int pause_count;
        public string end_reason;
        public bool glove_connected_at_end;
        public List<string> assist_levels_used = new List<string>();
        // Stimulation time per channel (s): thumb, index, middle, ring, little, palm.
        public float[] stimulation_seconds = new float[GloveChannelUtil.Count];
        public List<string> event_counts = new List<string>();   // "ID=n"
        // Tasks that ask the patient something (T1 step 4): answered trials and correct answers.
        public int quiz_trials;
        public int quiz_correct;
    }

    /// <summary>
    /// Learning-phase log: one CSV row per touch event, flushed at once, plus session.json at the
    /// end. Learning is not scored; this records dose and what was felt (docs/10 §5.8).
    /// Files go to persistentDataPath/learning/<participant>_<task>_<time>/.
    /// </summary>
    public class LearningLog : IDisposable
    {
        public const string CsvHeader =
            "session_id,task,mode,assist,round,step,t_s,wall_local,event_id,kind,channels," +
            "intensity,speed,frequency_hz,count,end_intensity,seconds,glove_connected,air_pressure";

        private readonly StreamWriter csv;
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public string SessionId { get; }
        public string Folder { get; }

        // Context the task updates as it runs.
        public string Task;
        public string Mode;
        public string Assist;
        public int Round;
        public string Step = "";

        public LearningLog(string participantId, string task)
        {
            SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            Task = task;
            Folder = Path.Combine(Application.persistentDataPath, "learning",
                                  participantId + "_" + task + "_" + SessionId);
            Directory.CreateDirectory(Folder);
            csv = new StreamWriter(Path.Combine(Folder, "events.csv"), false, new UTF8Encoding(false));
            csv.WriteLine(CsvHeader);
            csv.Flush();
        }

        public void Event(string id, string kind, GloveChannels ch, float intensity, float speed, float freq,
                          int count, float endIntensity, float seconds, bool connected, int[] airPressure)
        {
            if (csv == null) return;
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(SessionId).Append(',')
              .Append(Task).Append(',')
              .Append(Mode).Append(',')
              .Append(Assist).Append(',')
              .Append(Round.ToString(c)).Append(',')
              .Append(Clean(Step)).Append(',')
              .Append(clock.Elapsed.TotalSeconds.ToString("0.000", c)).Append(',')
              .Append(DateTime.Now.ToString("HH:mm:ss.fff", c)).Append(',')
              .Append(Clean(id)).Append(',')
              .Append(kind).Append(',')
              .Append(GloveChannelUtil.ToLabel(ch)).Append(',')
              .Append(intensity.ToString("0.###", c)).Append(',')
              .Append(speed.ToString("0.###", c)).Append(',')
              .Append(freq.ToString("0.###", c)).Append(',')
              .Append(count.ToString(c)).Append(',')
              .Append(endIntensity.ToString("0.###", c)).Append(',')
              .Append(seconds.ToString("0.###", c)).Append(',')
              .Append(connected ? 1 : 0).Append(',')
              .Append(airPressure == null ? "" : string.Join("|", airPressure));
            csv.WriteLine(sb.ToString());
            csv.Flush();

            counts.TryGetValue(id, out int n);
            counts[id] = n + 1;
        }

        public const string TrialsHeader =
            "session_id,task,round,step,t_s,wall_local,item,stimulus_count,stimulus_strength,answer,correct,reaction_s,note";

        private StreamWriter trials;

        /// <summary>
        /// One row in trials.csv (created on first use): a question and the patient's answer, or a
        /// note about a step (e.g. grip curl while matching). Empty strings / -1 = not applicable.
        /// </summary>
        public void Trial(string item, int stimulusCount, string stimulusStrength, int answer, int correct,
                          float reactionSeconds, string note)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (trials == null)
            {
                trials = new StreamWriter(Path.Combine(Folder, "trials.csv"), false, new UTF8Encoding(false));
                trials.WriteLine(TrialsHeader);
            }
            var sb = new StringBuilder();
            sb.Append(SessionId).Append(',')
              .Append(Task).Append(',')
              .Append(Round.ToString(c)).Append(',')
              .Append(Clean(Step)).Append(',')
              .Append(clock.Elapsed.TotalSeconds.ToString("0.000", c)).Append(',')
              .Append(DateTime.Now.ToString("HH:mm:ss.fff", c)).Append(',')
              .Append(Clean(item)).Append(',')
              .Append(stimulusCount < 0 ? "" : stimulusCount.ToString(c)).Append(',')
              .Append(Clean(stimulusStrength)).Append(',')
              .Append(answer < 0 ? "" : answer.ToString(c)).Append(',')
              .Append(correct < 0 ? "" : correct.ToString(c)).Append(',')
              .Append(reactionSeconds < 0f ? "" : reactionSeconds.ToString("0.000", c)).Append(',')
              .Append(Clean(note));
            trials.WriteLine(sb.ToString());
            trials.Flush();
        }

        public void WriteSummary(LearningSessionSummary s)
        {
            foreach (KeyValuePair<string, int> kv in counts) s.event_counts.Add(kv.Key + "=" + kv.Value);
            File.WriteAllText(Path.Combine(Folder, "session.json"), JsonUtility.ToJson(s, true), new UTF8Encoding(false));
        }

        private static string Clean(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace(',', ';').Replace('\n', ' ');
        }

        public void Dispose()
        {
            csv?.Dispose();
            trials?.Dispose();
        }
    }
}
