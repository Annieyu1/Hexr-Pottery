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
        public bool Done => end >= 0f;
        /// <summary>Lift to landing (s).</summary>
        public float Seconds => start - liftStart;
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
    /// The source is Quest hand tracking (via TrackedHandFrame); FlexR bend data can replace Pose()
    /// later. In the Editor without a tracked hand, keys 1–5 tap thumb..little (simulation).
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
        [Tooltip("A lift must also be at least this high (m), so tracking jitter is not a press.")]
        [SerializeField] private float minLiftMeters = 0.005f;
        [Tooltip("A finger that moved less than this in calibration cannot press (m).")]
        [SerializeField] private float minRangeMeters = 0.008f;
        [Tooltip("The key shows down this long after a tap (s).")]
        [SerializeField] private float keyDownSeconds = 0.18f;
        [Tooltip("Low-pass on progress against tracking jitter (1/s).")]
        [SerializeField] private float smoothing = 20f;

        private readonly Vector3[] rest = new Vector3[5];
        private readonly Vector3[] dir = new Vector3[5];
        private readonly float[] range = new float[5];
        private readonly bool[] lifted = new bool[5];
        private readonly float[] progress = new float[5];
        private readonly float[] speed = new float[5];
        private readonly float[] moveFrom = new float[5];
        private readonly float[] downUntil = new float[5];
        private readonly int[] coMask = new int[5];
        private readonly PressInfo[] current = new PressInfo[5];

        /// <summary>Time base for press times (the task's running clock, which stops while paused).</summary>
        public Func<float> Clock = () => Time.time;

        /// <summary>A finger left the rest pose far enough (no note yet).</summary>
        public event Action<int> Lifted;
        /// <summary>A finger came back down: this is the key press.</summary>
        public event Action<int, PressInfo> Pressed;
        public event Action<int, PressInfo> Released;

        public bool Calibrated { get; private set; }
        public bool Simulating { get; private set; }
        public bool Tracked => frame != null && frame.IsValid && !frame.UsingStandIn;
        public TrackedHandFrame Frame => frame;

        public float Progress(int f) { return progress[f]; }
        public bool IsLifted(int f) { return lifted[f]; }
        /// <summary>The key is shown down for a moment after a tap.</summary>
        public bool IsDown(int f) { return Clock() < downUntil[f]; }
        public float RangeMeters(int f) { return range[f]; }
        public bool Usable(int f) { return Simulating || range[f] >= minRangeMeters; }
        public Vector3 RestPose(int f) { return rest[f]; }
        public Vector3 PressPose(int f) { return rest[f] + dir[f]; }

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

        public void SetRest(Vector3[] poses)
        {
            for (int f = 0; f < 5; f++) { rest[f] = poses[f]; dir[f] = Vector3.zero; range[f] = 0f; }
            Calibrated = false;
            ReleaseAll();
        }

        /// <summary>The finger's lifted pose (the furthest point of its calibration movement).</summary>
        public void SetPress(int f, Vector3 pose)
        {
            dir[f] = pose - rest[f];
            range[f] = dir[f].magnitude;
            Calibrated = true;
        }

        public void SetSimulation(bool on)
        {
            Simulating = on;
            if (on) Calibrated = true;
            ReleaseAll();
        }

        /// <summary>Cancels lifts in progress (no press) and puts the keys up.</summary>
        public void ReleaseAll()
        {
            for (int f = 0; f < 5; f++)
            {
                lifted[f] = false;
                current[f] = null;
                progress[f] = 0f;
                speed[f] = 0f;
                downUntil[f] = 0f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float now = Clock();
            if (Simulating) { UpdateSimulation(now); return; }
            if (!Calibrated || !Tracked)
            {
                // tracking lost mid-lift: no press
                for (int f = 0; f < 5; f++) { lifted[f] = false; current[f] = null; }
                return;
            }

            float k = 1f - Mathf.Exp(-smoothing * dt);
            for (int f = 0; f < 5; f++)
            {
                float raw = Usable(f) ? Vector3.Dot(Pose(f) - rest[f], dir[f]) / (range[f] * range[f]) : 0f;
                float p = Mathf.Lerp(progress[f], raw, k);
                speed[f] = dt > 0f ? (p - progress[f]) / dt : 0f;
                progress[f] = p;
            }

            for (int f = 0; f < 5; f++)
            {
                if (!lifted[f])
                {
                    if (progress[f] < moveStart) moveFrom[f] = now;
                    if (progress[f] > liftOn && progress[f] * range[f] >= minLiftMeters)
                    {
                        lifted[f] = true;
                        coMask[f] = 0;
                        current[f] = new PressInfo { finger = f, liftStart = moveFrom[f] };
                        Lifted?.Invoke(f);
                    }
                    continue;
                }

                PressInfo i = current[f];
                i.peakProgress = Mathf.Max(i.peakProgress, progress[f]);
                i.peakLiftMm = Mathf.Max(i.peakLiftMm, progress[f] * range[f] * 1000f);
                i.peakFlexDegrees = Mathf.Max(i.peakFlexDegrees, FlexDegrees(f));
                i.landSpeedMmS = Mathf.Max(i.landSpeedMmS, -speed[f] * range[f] * 1000f);
                for (int o = 0; o < 5; o++)
                    if (o != f && progress[o] > liftOn * 0.8f) coMask[f] |= 1 << o;

                if (progress[f] < landOff) Land(f, now);
            }
        }

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
            i.start = now;
            i.end = now;
            downUntil[f] = now + keyDownSeconds;
            Pressed?.Invoke(f, i);
            Released?.Invoke(f, i);
        }

        /// <summary>Desktop: holding a key 1–5 = finger lifted, letting go = it lands.</summary>
        private void UpdateSimulation(float now)
        {
            for (int f = 0; f < 5; f++)
            {
                bool held = SimHeld(f);
                if (held && !lifted[f])
                {
                    lifted[f] = true;
                    coMask[f] = 0;
                    current[f] = new PressInfo { finger = f, liftStart = now, peakProgress = 1f };
                    Lifted?.Invoke(f);
                }
                progress[f] = held ? 1f : 0f;
                if (held)
                    for (int o = 0; o < 5; o++)
                        if (o != f && SimHeld(o)) coMask[f] |= 1 << o;
                if (!held && lifted[f]) Land(f, now);
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
