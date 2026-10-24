using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>One note the patient was asked to play (L2 learn, L3 test).</summary>
    public class PianoNoteTrial
    {
        public int index;
        public int[] targets;           // keys 0..4
        public float onset;             // task clock: cue / beat time
        public float windowStart = float.NegativeInfinity, windowEnd = float.PositiveInfinity;   // L3 tempo
        public string song = "";
        public string cues = "";
        public int stageAtResponse = -1; // unused since 2026-10-21 (L2 cues no longer come in steps)
        public bool strayIgnored;       // L3 (2026-10-25): other fingers do not answer; only a timeout is a Miss

        public int firstKey = -1;
        public float firstTime = -1f;
        public PressInfo firstInfo;
        public PressInfo targetInfo;    // first press of a target finger (individuation)
        public int rawFirstKey = -1;    // first press by the uncorrected judgment (docs/16 §2)
        public string suppressed = "";  // fingers whose landing did not count (another landed higher)
        public int wrongPresses;        // L2: wrong keys; L3: stray presses by other fingers
        public int wholeHandPresses;    // L3: three or more fingers lifted together (no hit, no error)
        public string strayFingers = "";
        public float hitTime = -1f;     // L3: the target finger pressed
        public int hits;                // distinct target keys pressed
        private int hitMask;
        public bool answered;           // the right key(s) were pressed
        public bool correct;            // right first time (no other finger before it)
        public bool omitted;
        public bool skipped;            // Skip button
        public string rating = "";     // L3: Perfect / Very good / Nice / Miss
        public float closedAt = -1f;    // clock time the note ended (answered or omitted)

        /// <summary>A press while this note is waiting. Returns true once the note is finished.</summary>
        public bool Register(int key, PressInfo info, float t, int stage)
        {
            if (answered) return true;
            if (strayIgnored && info != null && info.wholeHand) { wholeHandPresses++; return false; }
            if (firstKey < 0) { firstKey = key; firstTime = t; firstInfo = info; stageAtResponse = stage; }
            bool isTarget = Array.IndexOf(targets, key) >= 0;
            if (isTarget)
            {
                if (targetInfo == null) targetInfo = info;
                if ((hitMask & (1 << key)) == 0) { hitMask |= 1 << key; hits++; }
            }
            else
            {
                wrongPresses++;
                if (strayIgnored && info != null)
                {
                    string name = ((HandFinger)info.finger).ToString();
                    strayFingers = strayFingers.Length == 0 ? name : strayFingers + "+" + name;
                }
            }

            if (hits >= targets.Length)
            {
                answered = true;
                hitTime = t;
                correct = wrongPresses == 0 && wholeHandPresses == 0;   // right first time
            }
            return answered;
        }

        /// <summary>Ready to write once the answering press has been released (or there was none).</summary>
        public bool ReadyToWrite(float now)
        {
            if (closedAt < 0f) return false;
            return firstInfo == null || firstInfo.Done || now - closedAt > 2f;
        }
    }

    [Serializable]
    public class PianoCalibration
    {
        public string hand;
        public bool simulated;
        public float[] range_mm = new float[5];
        public bool[] usable = new bool[5];
        public string[] rest_pose_mm = new string[5];
        public string[] press_pose_mm = new string[5];
        public float key_spacing_mm;
        public string keyboard_position;
        public float desk_shift_mm;     // VR rig moved up (+) / down (-) to line the real table up with the virtual desk
        // 5×5, row = finger lifted fully, column = how far that drags each finger (fraction of its own lift)
        public float[] coupling = new float[25];
    }

    [Serializable]
    public class PianoTestSummary
    {
        public string session_id;
        public string song;
        public string cues;
        public string timing;
        public float tempo;
        public int notes;
        public int correct;             // right first time (no other finger before the target)
        public int omitted;
        public float accuracy;          // right first time / notes (therapist only)
        public int hits;                // target finger pressed before the timeout
        public float completion;        // hits / notes (shown to the patient)
        public int stray_presses;       // other fingers that pressed while a note waited
        public int whole_hand;          // three or more fingers together (ignored)
        public float mean_reaction_s;
        public float mean_abs_timing_error_s;
        public float play_seconds;      // first cue to the end of the last note
        public int perfect;
        public int very_good;
        public int nice;
        public int miss;
        public int[] per_finger_trials = new int[5];
        public int[] per_finger_correct = new int[5];
        // 5×5, row = target finger, column = finger that pressed (thumb..little); omissions not counted.
        public int[] confusion = new int[25];
    }

    /// <summary>
    /// T2 files next to the learning log: piano_trials.csv (one row per asked note), flex.csv
    /// (finger movement over time), calibration.json, test_summary.json.
    /// </summary>
    public class PianoLog : IDisposable
    {
        public const string TrialsHeader =
            "session_id,level,song,round,note_index,target_keys,target_fingers,cues,timing,tempo,rating," +
            "onset_s,response_s,reaction_s,timing_error_s,response_key,response_finger,correct,wrong_presses,omitted,skipped," +
            "co_active,peak_progress,peak_lift_mm,peak_flex_deg,land_speed_mm_s,lift_start_s,lift_reaction_s,lift_s,glove_connected," +
            "raw_response_finger,suppressed,other_peak_thumb,other_peak_index,other_peak_middle,other_peak_ring,other_peak_little,individuation," +
            "hit,hit_s,stray_fingers,whole_hand";

        private readonly string folder;
        private readonly string sessionId;
        private StreamWriter trials;
        private StreamWriter flex;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public PianoLog(string folder, string sessionId)
        {
            this.folder = folder;
            this.sessionId = sessionId;
        }

        public void Trial(string level, string song, int round, PianoNoteTrial n, string timing, float tempo,
                          Func<int, int> fingerFor, bool glove)
        {
            if (trials == null)
            {
                trials = new StreamWriter(Path.Combine(folder, "piano_trials.csv"), false, new UTF8Encoding(false));
                trials.WriteLine(TrialsHeader);
            }
            var keys = new StringBuilder();
            var fingers = new StringBuilder();
            foreach (int k in n.targets)
            {
                if (keys.Length > 0) { keys.Append('+'); fingers.Append('+'); }
                keys.Append(PianoSong.KeyNames[k]);
                fingers.Append(((HandFinger)fingerFor(k)).ToString());
            }
            bool responded = n.firstKey >= 0;
            PressInfo p = n.firstInfo;
            bool tempoMode = !float.IsInfinity(n.windowStart);
            var sb = new StringBuilder();
            sb.Append(sessionId).Append(',')
              .Append(level).Append(',')
              .Append(Clean(song)).Append(',')
              .Append(round.ToString(C)).Append(',')
              .Append(n.index.ToString(C)).Append(',')
              .Append(keys).Append(',')
              .Append(fingers).Append(',')
              .Append(Clean(n.cues)).Append(',')
              .Append(timing).Append(',')
              .Append(tempo.ToString("0.##", C)).Append(',')
              .Append(Clean(n.rating)).Append(',')
              .Append(n.onset.ToString("0.000", C)).Append(',')
              .Append(responded ? n.firstTime.ToString("0.000", C) : "").Append(',')
              .Append(responded ? (n.firstTime - n.onset).ToString("0.000", C) : "").Append(',')
              .Append(responded && tempoMode ? (n.firstTime - n.onset).ToString("0.000", C) : "").Append(',')
              .Append(responded ? PianoSong.KeyNames[n.firstKey] : "").Append(',')
              .Append(responded ? ((HandFinger)fingerFor(n.firstKey)).ToString() : "").Append(',')
              .Append(n.correct ? 1 : 0).Append(',')
              .Append(n.wrongPresses.ToString(C)).Append(',')
              .Append(n.omitted ? 1 : 0).Append(',')
              .Append(n.skipped ? 1 : 0).Append(',')
              .Append(p != null ? Clean(p.coActive) : "").Append(',')
              .Append(p != null ? p.peakProgress.ToString("0.00", C) : "").Append(',')
              .Append(p != null ? p.peakLiftMm.ToString("0.0", C) : "").Append(',')
              .Append(p != null ? p.peakFlexDegrees.ToString("0.0", C) : "").Append(',')
              .Append(p != null ? p.landSpeedMmS.ToString("0", C) : "").Append(',')
              .Append(p != null ? p.liftStart.ToString("0.000", C) : "").Append(',')
              .Append(p != null ? (p.liftStart - n.onset).ToString("0.000", C) : "").Append(',')
              .Append(p != null && p.Done ? p.Seconds.ToString("0.000", C) : "").Append(',')
              .Append(glove ? 1 : 0).Append(',')
              .Append(n.rawFirstKey >= 0 ? ((HandFinger)fingerFor(n.rawFirstKey)).ToString() : "").Append(',')
              .Append(Clean(n.suppressed));
            // the other fingers while the target finger pressed, coupling not subtracted
            PressInfo ti = n.targetInfo;
            for (int f = 0; f < 5; f++)
                sb.Append(',').Append(ti != null && f != ti.finger ? ti.otherPeak[f].ToString("0.00", C) : "");
            sb.Append(',').Append(ti != null ? ti.Individuation.ToString("0.00", C) : "");
            bool hit = n.hitTime >= 0f;
            sb.Append(',').Append(hit ? 1 : 0)
              .Append(',').Append(hit ? (n.hitTime - n.onset).ToString("0.000", C) : "")
              .Append(',').Append(Clean(n.strayFingers))
              .Append(',').Append(n.wholeHandPresses.ToString(C));
            trials.WriteLine(sb.ToString());
            trials.Flush();
        }

        public void Flex(float t, string level, int noteIndex, bool tracked, FingerPressDetector d)
        {
            if (flex == null)
            {
                flex = new StreamWriter(Path.Combine(folder, "flex.csv"), false, new UTF8Encoding(false));
                flex.WriteLine("t_s,level,note_index,tracked," +
                               "progress_thumb,progress_index,progress_middle,progress_ring,progress_little," +
                               "flex_thumb,flex_index,flex_middle,flex_ring,flex_little");
            }
            var sb = new StringBuilder();
            sb.Append(t.ToString("0.000", C)).Append(',').Append(level).Append(',')
              .Append(noteIndex.ToString(C)).Append(',').Append(tracked ? 1 : 0);
            for (int f = 0; f < 5; f++) sb.Append(',').Append(d.Progress(f).ToString("0.00", C));
            for (int f = 0; f < 5; f++) sb.Append(',').Append(tracked ? d.FlexDegrees(f).ToString("0.0", C) : "");
            flex.WriteLine(sb.ToString());
        }

        public void WriteJson(string file, object data)
        {
            File.WriteAllText(Path.Combine(folder, file), JsonUtility.ToJson(data, true), new UTF8Encoding(false));
        }

        public static string Mm(Vector3 v)
        {
            v *= 1000f;
            return v.x.ToString("0.0", C) + " " + v.y.ToString("0.0", C) + " " + v.z.ToString("0.0", C);
        }

        private static string Clean(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace(',', ';').Replace('\n', ' ');
        }

        public void Dispose()
        {
            trials?.Dispose();
            flex?.Dispose();
            trials = null;
            flex = null;
        }
    }
}
