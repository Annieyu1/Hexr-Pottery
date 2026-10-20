using System;
using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    public enum HapticKind { Hold, Release, Squeeze, Tap, Vibrate, StopVibration }

    /// <summary>
    /// One named touch event (docs/10 §5.9 "触觉事件库"). The same specs are meant to be reused
    /// by the discrimination phase later, so learning and judging share one vocabulary.
    /// Intensity and speed use the HexR 0.1–1 scale.
    /// </summary>
    [Serializable]
    public class HapticSpec
    {
        public string id = "EVENT";
        public GloveChannels channels = GloveChannels.WholeHand;
        public HapticKind kind = HapticKind.Hold;
        [Range(0.1f, 1f)] public float intensity = 0.4f;
        [Tooltip("Pressure rise/fall speed, 0.1 slow to 1 fast.")]
        [Range(0.1f, 1f)] public float speed = 0.5f;
        [Tooltip("Squeeze: pulse rate (0.1–2 Hz). Vibrate: frequency (0.1–40 Hz).")]
        public float frequencyHz = 1f;
        [Tooltip("Squeeze: number of pulses.")]
        public int count = 2;
        [Tooltip("Squeeze: pressure kept after the pulses.")]
        [Range(0.1f, 1f)] public float endIntensity = 0.25f;
        [Tooltip("Tap: how long the press lasts before release (s).")]
        public float seconds = 0.3f;

        public HapticSpec Clone() { return (HapticSpec)MemberwiseClone(); }
    }

    public enum ChannelMode { Off, Hold, Pulse, Vibrate }

    public struct ChannelState
    {
        public ChannelMode mode;
        public float intensity;
        public float frequencyHz;
        public float since;     // Time.time when this state began
    }

    /// <summary>
    /// The single entry point tasks use to touch the patient. It sends to the glove (when one is
    /// connected), keeps the commanded state of the six channels for the visual cues, counts
    /// stimulation time per channel (dose) and logs every event.
    /// </summary>
    public class HapticPlayer : MonoBehaviour
    {
        [SerializeField] private HandSide hand = HandSide.Right;
        [Tooltip("Send to the real glove when connected. Off = visual/log only.")]
        [SerializeField] private bool useGlove = true;

        private IGloveOutput output = new NullGloveOutput();
        private readonly ChannelState[] state = new ChannelState[GloveChannelUtil.Count];
        private readonly float[] onSeconds = new float[GloveChannelUtil.Count];
        private bool suspended;

        public LearningLog Log { get; set; }
        public HandSide Hand => hand;
        public bool GloveConnected => output.IsConnected;
        public float[] StimulationSeconds => onSeconds;

        /// <summary>Raised whenever any channel's commanded state changes.</summary>
        public event Action StateChanged;

        public ChannelState GetState(int channelIndex) { return state[channelIndex]; }

        public void SetHand(HandSide side)
        {
            hand = side;
            output = useGlove ? new HexRGloveOutput(hand) : (IGloveOutput)new NullGloveOutput();
        }

        private void Awake()
        {
            SetHand(hand);
        }

        private void Update()
        {
            if (suspended) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < state.Length; i++)
                if (state[i].mode != ChannelMode.Off) onSeconds[i] += dt;
        }

        private void OnDisable()
        {
            StopAll("disabled");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StopAll("app_paused");
        }

        // ------------------------------------------------------------------ API

        public void Play(HapticSpec spec)
        {
            if (spec == null) return;
            switch (spec.kind)
            {
                case HapticKind.Hold: Hold(spec.channels, spec.intensity, spec.speed, spec.id); break;
                case HapticKind.Release: Release(spec.channels, spec.speed, spec.id); break;
                case HapticKind.Squeeze:
                    Squeeze(spec.channels, spec.count, spec.frequencyHz, spec.intensity, spec.endIntensity, spec.id);
                    break;
                case HapticKind.Tap: Tap(spec.channels, spec.intensity, spec.seconds, spec.id); break;
                case HapticKind.Vibrate: Vibrate(spec.channels, spec.frequencyHz, spec.intensity, spec.id); break;
                case HapticKind.StopVibration: StopVibration(spec.channels, spec.id); break;
            }
        }

        public void Hold(GloveChannels ch, float intensity, float speed, string id)
        {
            StopSqueezeState();
            if (!suspended) output.Press(ch, intensity, speed);
            SetState(ch, ChannelMode.Hold, intensity, 0f);
            LogEvent(id, "hold", ch, intensity, speed, 0f, 0, 0f, 0f);
        }

        public void Release(GloveChannels ch, float speed, string id)
        {
            StopSqueezeState();
            if (!suspended) output.Release(ch, speed);
            SetState(ch, ChannelMode.Off, 0f, 0f);
            LogEvent(id, "release", ch, 0f, speed, 0f, 0, 0f, 0f);
        }

        /// <summary>count pulses at rateHz, then keep endIntensity (like "squeeze twice, keep holding").</summary>
        public void Squeeze(GloveChannels ch, int count, float rateHz, float intensity, float endIntensity, string id)
        {
            rateHz = Mathf.Clamp(rateHz, 0.1f, 2f);
            if (!suspended) output.Pulses(ch, rateHz, intensity, count, endIntensity);
            SetState(ch, ChannelMode.Pulse, intensity, rateHz);
            LogEvent(id, "squeeze", ch, intensity, 1f, rateHz, count, endIntensity, count / rateHz);
            StopSqueezeState();
            squeezeRoutine = StartCoroutine(AfterSqueeze(ch, count / rateHz, endIntensity));
        }

        private IEnumerator AfterSqueeze(GloveChannels ch, float seconds, float endIntensity)
        {
            yield return new WaitForSeconds(seconds);
            squeezeRoutine = null;
            SetState(ch, ChannelMode.Hold, endIntensity, 0f);
        }

        // A later hold/release replaces a squeeze still in progress, so its end state must not come back.
        private Coroutine squeezeRoutine;

        private void StopSqueezeState()
        {
            if (squeezeRoutine != null) StopCoroutine(squeezeRoutine);
            squeezeRoutine = null;
        }

        /// <summary>Short press then release (piano note).</summary>
        public void Tap(GloveChannels ch, float intensity, float seconds, string id)
        {
            if (!suspended) output.Press(ch, intensity, 1f);
            SetState(ch, ChannelMode.Hold, intensity, 0f);
            LogEvent(id, "tap", ch, intensity, 1f, 0f, 1, 0f, seconds);
            StartCoroutine(ReleaseAfter(ch, seconds));
        }

        private IEnumerator ReleaseAfter(GloveChannels ch, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!suspended) output.Release(ch, 1f);
            SetState(ch, ChannelMode.Off, 0f, 0f);
        }

        public void Vibrate(GloveChannels ch, float frequencyHz, float intensity, string id)
        {
            if (!suspended) output.Vibrate(ch, frequencyHz, intensity);
            SetState(ch, ChannelMode.Vibrate, intensity, frequencyHz);
            LogEvent(id, "vibrate", ch, intensity, 0f, frequencyHz, 0, 0f, 0f);
        }

        public void StopVibration(GloveChannels ch, string id)
        {
            if (!suspended) output.StopVibration(ch);
            SetState(ch, ChannelMode.Off, 0f, 0f);
            LogEvent(id, "stop_vibration", ch, 0f, 0f, 0f, 0, 0f, 0f);
        }

        /// <summary>Everything off, state cleared.</summary>
        public void StopAll(string reason)
        {
            StopAllCoroutines();
            squeezeRoutine = null;
            output.StopVibration(GloveChannels.WholeHand);
            output.Release(GloveChannels.WholeHand, 1f);
            bool anyOn = false;
            for (int i = 0; i < state.Length; i++) anyOn |= state[i].mode != ChannelMode.Off;
            SetState(GloveChannels.WholeHand, ChannelMode.Off, 0f, 0f);
            if (anyOn) LogEvent("STOP_ALL", reason, GloveChannels.WholeHand, 0f, 1f, 0f, 0, 0f, 0f);
        }

        /// <summary>Pause: the glove goes quiet but the commanded holds are remembered.</summary>
        public void Suspend()
        {
            if (suspended) return;
            suspended = true;
            output.StopVibration(GloveChannels.WholeHand);
            output.Release(GloveChannels.WholeHand, 1f);
        }

        /// <summary>Resume: re-send whatever was being held.</summary>
        public void ResumeHolds()
        {
            if (!suspended) return;
            suspended = false;
            for (int i = 0; i < state.Length; i++)
            {
                GloveChannels c = GloveChannelUtil.Order[i];
                if (state[i].mode == ChannelMode.Hold) output.Press(c, state[i].intensity, 0.5f);
                else if (state[i].mode == ChannelMode.Vibrate) output.Vibrate(c, state[i].frequencyHz, state[i].intensity);
            }
        }

        // ------------------------------------------------------------------ internals

        private void SetState(GloveChannels ch, ChannelMode mode, float intensity, float freq)
        {
            foreach (GloveChannels c in GloveChannelUtil.Split(ch))
            {
                int i = GloveChannelUtil.IndexOf(c);
                state[i] = new ChannelState { mode = mode, intensity = intensity, frequencyHz = freq, since = Time.time };
            }
            StateChanged?.Invoke();
        }

        private void LogEvent(string id, string kind, GloveChannels ch, float intensity, float speed,
                              float freq, int count, float endIntensity, float seconds)
        {
            Log?.Event(id, kind, ch, intensity, speed, freq, count, endIntensity, seconds,
                       output.IsConnected, output.ReadAirPressure());
        }
    }
}
