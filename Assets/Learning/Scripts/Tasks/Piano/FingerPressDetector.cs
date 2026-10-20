using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>One finger press, filled in while the finger is down. Done once it is released.</summary>
    public class PressInfo
    {
        public int finger;
        public float start;
        public float end = -1f;
        public float peakProgress;      // 1 = as far as the calibrated press
        public float peakFlexDegrees;   // knuckle→tip vs. finger direction
        public float peakSpeed;         // progress per second
        public string coActive = "";    // other fingers that moved with it ("Ring+Little")
        public bool Done => end >= 0f;
        public float Seconds => Done ? end - start : -1f;
    }

    /// <summary>
    /// Finger presses from the patient's own tracked hand, per patient. Calibration stores a rest
    /// pose and a "press" pose for each finger (fingertip relative to its knuckle, in the hand frame,
    /// so moving the whole hand does not count). Progress = how far the finger has moved from rest
    /// toward its own press pose; a press starts above pressOn and ends below pressOff.
    ///
    /// The source is Quest hand tracking (via TrackedHandFrame). FlexR bend data can replace Pose()
    /// later without changing the task. In the Editor without a tracked hand, keys 1–5 press
    /// thumb..little (simulation).
    /// </summary>
    public class FingerPressDetector : MonoBehaviour
    {
        [SerializeField] private TrackedHandFrame frame;
        [Tooltip("Press starts when the finger has moved this fraction of its calibrated press.")]
        [Range(0.2f, 0.9f)] [SerializeField] private float pressOn = 0.5f;
        [Tooltip("Press ends below this fraction (lower than pressOn, so it does not flicker).")]
        [Range(0.05f, 0.8f)] [SerializeField] private float pressOff = 0.25f;
        [Tooltip("A finger that moved less than this in calibration cannot press (m).")]
        [SerializeField] private float minRangeMeters = 0.008f;
        [Tooltip("Low-pass on progress against tracking jitter (1/s).")]
        [SerializeField] private float smoothing = 20f;

        private readonly Vector3[] rest = new Vector3[5];
        private readonly Vector3[] dir = new Vector3[5];
        private readonly float[] range = new float[5];
        private readonly bool[] down = new bool[5];
        private readonly float[] progress = new float[5];
        private readonly float[] speed = new float[5];
        private readonly PressInfo[] current = new PressInfo[5];

        /// <summary>Time base for press times (the task's running clock, which stops while paused).</summary>
        public Func<float> Clock = () => Time.time;

        public event Action<int, PressInfo> Pressed;
        public event Action<int, PressInfo> Released;

        public bool Calibrated { get; private set; }
        public bool Simulating { get; private set; }
        public bool Tracked => frame != null && frame.IsValid && !frame.UsingStandIn;
        public TrackedHandFrame Frame => frame;

        public float Progress(int f) { return progress[f]; }
        public bool IsDown(int f) { return down[f]; }
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

        public void ReleaseAll()
        {
            float now = Clock();
            for (int f = 0; f < 5; f++)
            {
                if (down[f]) End(f, now);
                progress[f] = 0f;
                speed[f] = 0f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float now = Clock();
            bool live = Simulating || (Calibrated && Tracked);
            if (!live)
            {
                for (int f = 0; f < 5; f++) if (down[f]) End(f, now);
                return;
            }

            float k = 1f - Mathf.Exp(-smoothing * dt);
            for (int f = 0; f < 5; f++)
            {
                float raw = 0f;
                if (Simulating) raw = SimDown(f) ? 1f : 0f;
                else if (Usable(f)) raw = Vector3.Dot(Pose(f) - rest[f], dir[f]) / (range[f] * range[f]);
                float p = Simulating ? raw : Mathf.Lerp(progress[f], raw, k);
                speed[f] = dt > 0f ? (p - progress[f]) / dt : 0f;
                progress[f] = p;
            }

            for (int f = 0; f < 5; f++)
            {
                if (!down[f] && progress[f] > pressOn)
                {
                    down[f] = true;
                    var others = new List<string>();
                    for (int o = 0; o < 5; o++)
                        if (o != f && progress[o] > pressOn * 0.8f) others.Add(((HandFinger)o).ToString());
                    current[f] = new PressInfo { finger = f, start = now, coActive = string.Join("+", others) };
                    Pressed?.Invoke(f, current[f]);
                }
                else if (down[f] && progress[f] < pressOff)
                {
                    End(f, now);
                }
                if (down[f] && current[f] != null)
                {
                    PressInfo i = current[f];
                    i.peakProgress = Mathf.Max(i.peakProgress, progress[f]);
                    i.peakSpeed = Mathf.Max(i.peakSpeed, speed[f]);
                    i.peakFlexDegrees = Mathf.Max(i.peakFlexDegrees, FlexDegrees(f));
                }
            }
        }

        private void End(int f, float now)
        {
            down[f] = false;
            PressInfo i = current[f];
            current[f] = null;
            if (i == null) return;
            i.end = now;
            Released?.Invoke(f, i);
        }

        private static bool SimDown(int f)
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
