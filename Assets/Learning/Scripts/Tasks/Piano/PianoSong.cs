using System;
using System.Collections.Generic;
using System.Globalization;

namespace HexRMeta.Learning
{
    /// <summary>
    /// A five-finger song (notes C D E F G only, one finger per note, no hand shifts).
    /// Text format: notes separated by spaces, "|" bar lines are ignored.
    ///   E        one beat of E
    ///   E:2      two beats
    ///   C+E      chord (two fingers together)
    ///   -        rest, "-:2" two beats
    /// </summary>
    [Serializable]
    public class PianoSong
    {
        public string title = "Song";
        public string notes = "C D E F G";
        public float beatsPerMinute = 50f;
        [UnityEngine.Tooltip("Show these instead of note names over the keys (e.g. 1 2 3 4 5 for counting).")]
        public bool showNumbers;

        public struct Note
        {
            public int[] keys;      // 0..4 = C..G, empty = rest
            public float beats;
        }

        public List<Note> Parse()
        {
            var list = new List<Note>();
            foreach (string raw in notes.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw == "|") continue;
                string token = raw;
                float beats = 1f;
                int colon = token.IndexOf(':');
                if (colon >= 0)
                {
                    float.TryParse(token.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out beats);
                    if (beats <= 0f) beats = 1f;
                    token = token.Substring(0, colon);
                }
                var keys = new List<int>();
                if (token != "-")
                    foreach (string part in token.Split('+'))
                    {
                        int k = KeyIndex(part);
                        if (k >= 0) keys.Add(k);
                    }
                list.Add(new Note { keys = keys.ToArray(), beats = beats });
            }
            return list;
        }

        public static int KeyIndex(string name)
        {
            switch (name.Trim().ToUpperInvariant())
            {
                case "C": return 0;
                case "D": return 1;
                case "E": return 2;
                case "F": return 3;
                case "G": return 4;
                default: return -1;
            }
        }

        public static readonly string[] KeyNames = { "C", "D", "E", "F", "G" };

        // Public-domain melodies that fit C–G.
        public static PianoSong[] Defaults()
        {
            return new[]
            {
                new PianoSong
                {
                    title = "Counting 1 to 5", showNumbers = true, beatsPerMinute = 45f,
                    notes = "C D E F G -:2 G F E D C -:2",
                },
                new PianoSong
                {
                    title = "Ode to Joy",
                    notes = "E E F G | G F E D | C C D E | E:1.5 D:0.5 D:2 | " +
                            "E E F G | G F E D | C C D E | D:1.5 C:0.5 C:2",
                },
                new PianoSong
                {
                    title = "Mary Had a Little Lamb",
                    notes = "E D C D | E E E:2 | D D D:2 | E G G:2 | E D C D | E E E E | D D E D | C:4",
                },
                new PianoSong
                {
                    title = "Jingle Bells",
                    notes = "E E E:2 | E E E:2 | E G C:1.5 D:0.5 | E:4 | " +
                            "F F F:1.5 F:0.5 | F E E E | E D D E | D:2 G:2",
                },
            };
        }
    }
}
