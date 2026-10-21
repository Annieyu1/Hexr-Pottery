using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>Soft menu sounds made in code (no audio assets or licences): pop, whoosh, chime, page flip.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuSounds : MonoBehaviour
    {
        [SerializeField] private float volume = 0.35f;

        private AudioSource source;
        private AudioClip pop, tick, chime, open, flip;
        private const int Rate = 44100;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            pop = Tone("pop", 0.09f, 660f, 880f, 2, 40f);
            tick = Tone("tick", 0.05f, 1200f, 1200f, 1, 80f);
            open = Tone("open", 0.18f, 440f, 660f, 2, 18f);
            chime = MakeChime();
            flip = MakeFlip();
        }

        public void Pop() { Play(pop); }
        public void Tick() { Play(tick, 0.6f); }
        public void Open() { Play(open); }
        public void Chime() { Play(chime, 1.2f); }
        public void Flip() { Play(flip, 1.5f); }

        private void Play(AudioClip c, float gain = 1f)
        {
            if (source != null && c != null) source.PlayOneShot(c, volume * gain);
        }

        /// <summary>A short sine sweep with a soft attack and exponential decay.</summary>
        private static AudioClip Tone(string name, float seconds, float f0, float f1, int harmonics, float decay)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / seconds;
                float f = Mathf.Lerp(f0, f1, u);
                phase += 2f * Mathf.PI * f / Rate;
                float v = 0f;
                for (int h = 1; h <= harmonics; h++) v += Mathf.Sin(phase * h) / (h * h);
                data[i] = 0.5f * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * decay) * v;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Paper swish: low-passed noise that swells and fades, darker towards the end.</summary>
        private static AudioClip MakeFlip()
        {
            const float seconds = 0.6f;
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(1800f, 600f, u) / Rate);
                lp += a * ((float)rng.NextDouble() * 2f - 1f - lp);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u / 0.9f));
                data[i] = 0.6f * env * env * lp;
            }
            AudioClip clip = AudioClip.Create("flip", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C-E-G-C arpeggio, bell-like.</summary>
        private static AudioClip MakeChime()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float step = 0.11f, tail = 1.2f;
            int n = Mathf.CeilToInt((step * notes.Length + tail) * Rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 3.2f);
                    float v = Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[k] * 2.01f * t);
                    data[i] += 0.18f * env * v;
                }
            }
            AudioClip clip = AudioClip.Create("chime", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
