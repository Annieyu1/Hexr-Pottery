using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>
    /// One key press = one finger lifted and put down again ("tap"). Complete when the event fires
    /// at the moment the finger lands.
    /// </summary>
    public class PressInfo
    {
        public int finger;
        public float liftStart;         // finger started to move away from rest
        public float start;             // landed = the note (task clock)
        public float end = -1f;         // same as start once landed
        public float peakProgress;      // 1 = as far as the calibrated lift
        public float peakLiftMm;        // highest point of the lift, fingertip vs. its knuckle (mm)
        public float peakFlexDegrees;   // knuckle→tip vs. finger direction
        public float landSpeedMmS;      // fastest movement back down (mm/s), stand-in for "how hard"
        public string coActive = "";    // other fingers that lifted with it ("Ring+Little")
        public bool wholeHand;          // three or more fingers lifted together
        /// <summary>Highest raw progress of each other finger during this press (coupling not
        /// subtracted; own finger stays 0). Used for the individuation measure (docs/16 §3).</summary>
        public float[] otherPeak = new float[5];
        public bool Done => end >= 0f;
        /// <summary>Lift to landing (s).</summary>
        public float Seconds => start - liftStart;

        /// <summary>1 − mean raw peak of the four other fingers (0..1 each). 1 = the others stayed still.</summary>
        public float Individuation
        {
            get
            {
                float sum = 0f;
                for (int o = 0; o < 5; o++) if (o != finger) sum += Mathf.Clamp01(otherPeak[o]);
                return 1f - sum / 4f;
            }
        }
    }

    /// <summary>
    /// Finger taps from the patient's own tracked hand. The hand rests on the real table, so a
    /// finger cannot press down: a key press is the finger lifting a little and coming back down
    /// (2026-10-22). The note happens when it lands, which is also when the real fingertip touches
    /// the table.
    ///
    /// Per patient: calibration stores a rest pose and a lifted pose for each finger (fingertip
    /// relative to its knuckle, in the hand frame, so moving the whole hand does not count; key =
    /// finger, not where the tip lands). Progress = how far the finger has moved from rest toward
    /// its own lifted pose. Lifted above liftOn (and at least minLiftMeters), then back below
    /// landOff = one press. Whatever movement the patient did in calibration is learnt, so a
    /// patient who cannot lift a finger may hook it toward the palm instead.
    ///
    /// Other fingers (2026-10-25, docs/16 §2): calibration also records how far each finger drags
    /// the others along (coupling). That part is subtracted before the thresholds, only from a
    /// finger that is lower than the one dragging it. With SetExpected(.., strict) a finger that is
    /// not expected needs a higher lift (L2). Two fingers landing within landWindow: only the one
    /// that lifted higher counts, unless both are expected (chord). A second, uncorrected judgment
    /// (base thresholds on raw progress) runs alongside and is reported by RawPressed for the log.
    ///
    /// The source is Quest hand tracking (via TrackedHandFrame). In the Editor without a tracked
    /// hand, keys 1–5 tap thumb..little (simulation); simCoupling lets a held key lift its
    /// neighbours too, to try the coupling code.
    /// </summary>
    public class FingerPressDetector : MonoBehaviour
    {
        [SerializeField] private TrackedHandFrame frame;
        [Tooltip("Lifted when the finger has moved this fraction of its calibrated lift.")]
        [Range(0.2f, 0.9f)] [SerializeField] private float liftOn = 0.4f;
        [Tooltip("Landed (= key pressed) when it comes back below this fraction.")]
        [Range(0.05f, 0.8f)] [SerializeField] private float landOff = 0.2f;
        [Tooltip("Movement starts above this fraction (for the lift time).")]
        [Range(0.02f, 0.5f)] [SerializeField] private float moveStart = 0.1f;
        [Tooltip("A lift must also be at least this high (m), so tracking jitter is not a press. Floor: calibration raises it per finger to 3 × the jitter measured at rest.")]
        [SerializeField] private float minLiftMeters = 0.004f;
        [Tooltip("Per-finger minimum lift = this × the finger's jitter at rest (when higher than the floor).")]
        [SerializeField] private float noiseFactor = 3f;
        [Tooltip("A finger that moved less than this in calibration cannot press (m).")]
        [SerializeField] private float minRangeMeters = 0.008f;
        [Tooltip("The key shows down this long after a tap (s).")]
        [SerializeField] private float keyDownSeconds = 0.18f;
        [Tooltip("Low-pass on progress against tracking jitter (1/s).")]
        [SerializeField] private float smoothing = 20f;

        [Header("Other fingers")]
        [Tooltip("Subtract the movement a finger is dragged along by a higher one (coupling from calibration).")]
        [SerializeField] private bool compensateCoupling = true;
        [Tooltip("Strict mode (L2): a finger that is not expected must lift this far.")]
        [Range(0.3f, 1f)] [SerializeField] private float othersLiftOn = 0.7f;
        [Tooltip("Strict mode (L2): and at least this many times minLiftMeters.")]
        [SerializeField] private float othersMinLiftFactor = 2f;
        [Tooltip("Two fingers landing within this time: only the one that lifted higher counts (s).")]
        [SerializeField] private float landWindow = 0.2f;
        [Tooltip("Desktop simulation: a held key also lifts its neighbours by this fraction.")]
        [Range(0f, 0.9f)] [SerializeField] private float simCoupling = 0f;

        private const float SimRange = 0.02f;

        private readonly Vector3[] rest = new Vector3[5];
        private readonly Vector3[] dir = new Vector3[5];
        private readonly float[] range = new float[5];
        private readonly float[] noise = new float[5];      // jitter at rest (m, standard deviation)
        private readonly float[] minLift = new float[5];    // per finger: max(floor, noiseFactor × noise)
        private readonly bool[] lifted = new bool[5];
        private readonly float[] raw = new float[5];        // smoothed, coupling not subtracted
        private readonly float[] progress = new float[5];   // coupling subtracted
        private readonly float[] speed = new float[5];
        private readonly float[] moveFrom = new float[5];
        private readonly float[] downUntil = new float[5];
        private readonly int[] coMask = new int[5];
        private readonly PressInfo[] current = new PressInfo[5];
        private readonly float[,] coupling = new float[5, 5];   // [moving finger, dragged finger]
        private readonly Vector3[][] couplingPoses = new Vector3[5][];
        private readonly PressInfo[] held = new PressInfo[5];  // landed, waiting for a finger still lifted
        private readonly float[] lastFiredAt = new float[5];
        private readonly float[] lastFiredPeak = new float[5];
        private readonly bool[] rawLifted = new bool[5];
        private int expectedMask;
        private bool strictOthers;
        private float lastNow = float.NegativeInfinity;

        /// <summary>Time base for press times (the task's running clock, which stops while paused).</summary>
        public Func<float> Clock = () => Time.time;
        /// <summary>Simulation only: raw progress per finger instead of keys 1–5 (scripted tests).</summary>
        public Func<int, float> SimSource;

        /// <summary>A finger left the rest pose far enough (no note yet).</summary>
        public event Action<int> Lifted;
        /// <summary>A finger came back down: this is the key press.</summary>
        public event Action<int, PressInfo> Pressed;
        public event Action<int, PressInfo> Released;
        /// <summary>A landing that did not count because another finger landed with a higher lift.</summary>
        public event Action<int, PressInfo> Suppressed;
        /// <summary>Uncorrected judgment (no coupling subtraction, base thresholds): finger, landing time.</summary>
        public event Action<int, float> RawPressed;

        public bool Calibrated { get; private set; }
        public bool Simulating { get; private set; }
        public bool Tracked => frame != null && frame.IsValid && !frame.UsingStandIn;
        public TrackedHandFrame Frame => frame;

        public float Progress(int f) { return progress[f]; }
        public float RawProgress(int f) { return raw[f]; }
        public bool IsLifted(int f) { return lifted[f]; }
        /// <summary>The key is shown down for a moment after a tap.</summary>
        public bool IsDown(int f) { return Clock() < downUntil[f]; }
        public float RangeMeters(int f) { return range[f]; }
        public float NoiseMeters(int f) { return noise[f]; }
        public float MinLiftMeters(int f) { return minLift[f]; }
        public float MinRangeMeters => minRangeMeters;
        public bool Usable(int f) { return Simulating || range[f] >= minRangeMeters; }
        public Vector3 RestPose(int f) { return rest[f]; }
        public Vector3 PressPose(int f) { return rest[f] + dir[f]; }
        /// <summary>How far finger o moves (fraction of its own lift) when finger i lifts fully.</summary>
        public float Coupling(int i, int o) { return coupling[i, o]; }

        /// <summary>Fingertip relative to its knuckle, in the hand frame.</summary>
        public Vector3 Pose(int f)
        {
            return Quaternion.Inverse(frame.Rotation) * (frame.Tip(f) - frame.Knuckle(f));
        }

        /// <summary>Bend of one finger (deg): knuckle→tip against the hand's finger direction.</summary>
        public float FlexDegrees(int f)
        {
            if (frame == null || !frame.IsValid) return 0f;
            return Vector3.Angle(frame.Tip(f) - frame.Knuckle(f), frame.FingerDir);
        }

        /// <summary>The finger(s) the task wants now (bit per finger, 0 = none). Strict: the others need a higher lift.</summary>
        public void SetExpected(int fingerMask, bool strict)
        {
            expectedMask = fingerMask;
            strictOthers = strict;
        }

        public void ClearExpected() { SetExpected(0, false); }

        public void SetRest(Vector3[] poses)
        {
            for (int f = 0; f < 5; f++)
            {
                rest[f] = poses[f]; dir[f] = Vector3.zero; range[f] = 0f; couplingPoses[f] = null;
                noise[f] = 0f; minLift[f] = minLiftMeters;
                for (int o = 0; o < 5; o++) coupling[f, o] = 0f;
            }
            Calibrated = false;
            ReleaseAll();
        }

        /// <summary>Jitter of each fingertip while the hand rests still (m); sets the per-finger minimum lift.</summary>
        public void SetNoise(float[] sigma)
        {
            for (int f = 0; f < 5; f++)
            {
                noise[f] = sigma[f];
                minLift[f] = Mathf.Max(minLiftMeters, noiseFactor * sigma[f]);
            }
        }

        /// <summary>A saved calibration (lift directions and coupling) on top of today's rest pose and jitter.</summary>
        public void ApplySaved(Vector3[] lift, float[] couplingTable)
        {
            for (int f = 0; f < 5; f++)
            {
                dir[f] = lift[f];
                range[f] = lift[f].magnitude;
                for (int o = 0; o < 5; o++) coupling[f, o] = couplingTable[f * 5 + o];
            }
            Calibrated = true;
            ReleaseAll();
        }

        /// <summary>The finger's lifted pose (the furthest point of its calibration movement).</summary>
        public void SetPress(int f, Vector3 pose)
        {
            dir[f] = pose - rest[f];
            range[f] = dir[f].magnitude;
            Calibrated = true;
        }

        /// <summary>All fingers' poses at the moment finger f was furthest from rest in calibration.</summary>
        public void SetCouplingSample(int f, Vector3[] poses)
        {
            couplingPoses[f] = (Vector3[])poses.Clone();
        }

        /// <summary>After every finger's SetPress: turns the samples into the coupling table.</summary>
        public void FinishCalibration()
        {
            for (int i = 0; i < 5; i++)
                for (int o = 0; o < 5; o++)
                {
                    float c = 0f;
                    if (i != o && couplingPoses[i] != null && Usable(i) && Usable(o))
                        c = Mathf.Clamp01(Vector3.Dot(couplingPoses[i][o] - rest[o], dir[o]) / (range[o] * range[o]));
                    coupling[i, o] = c;
                }
        }

        public void SetSimulation(bool on)
        {
            Simulating = on;
            if (on)
            {
                Calibrated = true;
                for (int i = 0; i < 5; i++)
                {
                    range[i] = SimRange;
                    minLift[i] = minLiftMeters;
                    for (int o = 0; o < 5; o++) coupling[i, o] = Mathf.Abs(i - o) == 1 ? simCoupling : 0f;
                }
            }
            ReleaseAll();
        }

        /// <summary>Cancels lifts in progress (no press) and puts the keys up.</summary>
        public void ReleaseAll()
        {
            for (int f = 0; f < 5; f++)
            {
                lifted[f] = false;
                rawLifted[f] = false;
                current[f] = null;
                held[f] = null;
                raw[f] = 0f;
                progress[f] = 0f;
                speed[f] = 0f;
                downUntil[f] = 0f;
                lastFiredAt[f] = float.NegativeInfinity;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float now = Clock();
            // the task clock starts again at 0 for each run: old press times would block new presses
            if (now < lastNow) ReleaseAll();
            lastNow = now;
            if (Simulating) ReadSimulation();
            else if (!Calibrated || !Tracked)
            {
                // tracking lost mid-lift: no press; fingers that already landed still count
                for (int f = 0; f < 5; f++) { lifted[f] = false; rawLifted[f] = false; current[f] = null; }
                FlushHeld(now, true);
                return;
            }
            else
            {
                float k = 1f - Mathf.Exp(-smoothing * dt);
                for (int f = 0; f < 5; f++)
                {
                    float r = Usable(f) ? Vector3.Dot(Pose(f) - rest[f], dir[f]) / (range[f] * range[f]) : 0f;
                    raw[f] = Mathf.Lerp(raw[f], r, k);
                }
            }

            for (int o = 0; o < 5; o++)
            {
                float p = raw[o];
                if (compensateCoupling)
                {
                    // only a finger that is higher than o can have dragged it along
                    float drag = 0f;
                    for (int i = 0; i < 5; i++)
                        if (i != o && raw[i] > raw[o]) drag = Mathf.Max(drag, coupling[i, o] * raw[i]);
                    p -= drag;
                }
                speed[o] = dt > 0f ? (p - progress[o]) / dt : 0f;
                progress[o] = p;
            }

            JudgeRaw(now);
            for (int f = 0; f < 5; f++) Judge(f, now);
            FlushHeld(now, false);
        }

        private bool Expected(int f) { return (expectedMask & (1 << f)) != 0; }
        private bool Strict(int f) { return strictOthers && expectedMask != 0 && !Expected(f); }

        private void Judge(int f, float now)
        {
            if (!lifted[f])
            {
                if (progress[f] < moveStart) moveFrom[f] = now;
                float on = Strict(f) ? othersLiftOn : liftOn;
                float need = Strict(f) ? minLift[f] * othersMinLiftFactor : minLift[f];
                if (progress[f] > on && progress[f] * range[f] >= need)
                {
                    lifted[f] = true;
                    coMask[f] = 0;
                    current[f] = new PressInfo { finger = f, liftStart = moveFrom[f] };
                    Lifted?.Invoke(f);
                }
                if (!lifted[f]) return;
            }

            PressInfo i = current[f];
            i.peakProgress = Mathf.Max(i.peakProgress, progress[f]);
            i.peakLiftMm = Mathf.Max(i.peakLiftMm, progress[f] * range[f] * 1000f);
            i.peakFlexDegrees = Mathf.Max(i.peakFlexDegrees, FlexDegrees(f));
            i.landSpeedMmS = Mathf.Max(i.landSpeedMmS, -speed[f] * range[f] * 1000f);
            for (int o = 0; o < 5; o++)
            {
                if (o == f) continue;
                i.otherPeak[o] = Mathf.Max(i.otherPeak[o], raw[o]);
                if (progress[o] > liftOn * 0.8f) coMask[f] |= 1 << o;
            }

            if (progress[f] < landOff) Land(f, now);
        }

        /// <summary>The same press rule on raw progress with the base thresholds, for the log only.</summary>
        private void JudgeRaw(float now)
        {
            for (int f = 0; f < 5; f++)
            {
                if (!rawLifted[f])
                {
                    if (raw[f] > liftOn && raw[f] * range[f] >= minLift[f]) rawLifted[f] = true;
                }
                else if (raw[f] < landOff)
                {
                    rawLifted[f] = false;
                    RawPressed?.Invoke(f, now);
                }
            }
        }

        /// <summary>Two fingers compete unless both are expected (a chord).</summary>
        private bool Compete(int a, int b) { return !(Expected(a) && Expected(b)); }

        private void Land(int f, float now)
        {
            PressInfo i = current[f];
            lifted[f] = false;
            current[f] = null;
            if (i == null) return;
            var others = new System.Collections.Generic.List<string>();
            for (int o = 0; o < 5; o++)
                if ((coMask[f] & (1 << o)) != 0) others.Add(((HandFinger)o).ToString());
            i.coActive = string.Join("+", others);
            i.wholeHand = others.Count + 1 >= 3;
            i.start = now;
            i.end = now;

            for (int o = 0; o < 5; o++)
            {
                if (o == f || !Compete(f, o)) continue;
                // a finger that already counted a moment ago: this one only counts if it lifted higher
                if (now - lastFiredAt[o] <= landWindow && i.peakProgress <= lastFiredPeak[o]) { Suppress(f, i); return; }
                // a finger waiting already: the lower of the two drops out; the higher waits on
                // (FlushHeld counts it once no other finger is up)
                if (held[o] != null)
                {
                    if (i.peakProgress > held[o].peakProgress) { Suppress(o, held[o]); held[o] = null; }
                    else { Suppress(f, i); return; }
                }
            }
            // another finger is still up: wait for it (at most landWindow)
            for (int o = 0; o < 5; o++)
                if (o != f && lifted[o] && Compete(f, o)) { held[f] = i; return; }
            Fire(f, i);
        }

        private void FlushHeld(float now, bool all)
        {
            for (int f = 0; f < 5; f++)
            {
                if (held[f] == null) continue;
                bool waiting = false;
                for (int o = 0; o < 5; o++) if (o != f && lifted[o] && Compete(f, o)) waiting = true;
                if (all || !waiting || now - held[f].start >= landWindow)
                {
                    PressInfo i = held[f];
                    held[f] = null;
                    Fire(f, i);
                }
            }
        }

        private void Fire(int f, PressInfo i)
        {
            downUntil[f] = Clock() + keyDownSeconds;
            lastFiredAt[f] = i.start;
            lastFiredPeak[f] = i.peakProgress;
            Pressed?.Invoke(f, i);
            Released?.Invoke(f, i);
        }

        private void Suppress(int f, PressInfo i)
        {
            Suppressed?.Invoke(f, i);
        }

        /// <summary>Desktop: holding a key 1–5 = finger lifted, letting go = it lands.</summary>
        private void ReadSimulation()
        {
            if (SimSource != null)
            {
                for (int f = 0; f < 5; f++) raw[f] = SimSource(f);
                return;
            }
            for (int f = 0; f < 5; f++) raw[f] = 0f;
            for (int f = 0; f < 5; f++)
            {
                if (!SimHeld(f)) continue;
                raw[f] = 1f;
                if (f > 0) raw[f - 1] = Mathf.Max(raw[f - 1], simCoupling);
                if (f < 4) raw[f + 1] = Mathf.Max(raw[f + 1], simCoupling);
            }
        }

        private static bool SimHeld(int f)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            switch (f)
            {
                case 0: return k.digit1Key.isPressed;
                case 1: return k.digit2Key.isPressed;
                case 2: return k.digit3Key.isPressed;
                case 3: return k.digit4Key.isPressed;
                default: return k.digit5Key.isPressed;
            }
#else
            return false;
#endif
        }
    }
}
