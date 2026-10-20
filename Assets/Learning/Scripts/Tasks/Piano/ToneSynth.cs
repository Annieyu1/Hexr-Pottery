using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Makes soft piano-like tones in code (decaying harmonics), so no audio assets or licences
    /// are needed. Key 0..4 = C4..G4.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ToneSynth : MonoBehaviour
    {
        [SerializeField] private float volume = 0.5f;
        [SerializeField] private float lengthSeconds = 1.6f;

        private static readonly float[] Frequencies = { 261.63f, 293.66f, 329.63f, 349.23f, 392.00f };
        private static readonly float[] Harmonics = { 1f, 0.45f, 0.25f, 0.12f, 0.06f };
        private AudioClip[] clips;
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            clips = new AudioClip[Frequencies.Length];
            for (int i = 0; i < clips.Length; i++) clips[i] = MakeTone(Frequencies[i], "Tone_" + PianoSong.KeyNames[i]);
        }

        public void Play(int key)
        {
            if (clips == null || key < 0 || key >= clips.Length) return;
            source.PlayOneShot(clips[key], volume);
        }

        private AudioClip MakeTone(float freq, string name)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(lengthSeconds * rate);
            var data = new float[n];
            for (int s = 0; s < n; s++)
            {
                float t = s / (float)rate;
                float attack = Mathf.Clamp01(t / 0.005f);
                float v = 0f;
                for (int h = 0; h < Harmonics.Length; h++)
                    v += Harmonics[h] * Mathf.Exp(-t * (2.5f + 1.5f * h)) * Mathf.Sin(2f * Mathf.PI * freq * (h + 1) * t);
                data[s] = 0.35f * attack * v;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
