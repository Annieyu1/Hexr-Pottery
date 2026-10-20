using System;
using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// T11 拧瓶盖 (docs/10 §5.4, upgraded from the T11 backup): the gloved (affected) hand opens a
    /// cap on a container that stands in a holder on the table. Uses the patient's own tracked VR
    /// hand; twisting is measured from the real wrist rotation about the container's axis.
    /// Nothing is answered or scored.
    ///
    /// Felt sequence: fingers on the cap (pressure) → tight phase (pressure rises with the turn,
    /// a click every few degrees) → the seal breaks (short buzz, pressure drops) → loose phase
    /// (light pressure, soft clicks) → cap comes off. Rounds rotate container types for contrast:
    /// tight bottle cap / loose bottle cap (D2 strength, D4 click count) and a jar lid gripped by
    /// the whole hand (D1 which fingers).
    ///
    /// Lenient: if the patient does not reach the cap, the container comes to the fingers; if the
    /// patient does not turn, the cap turns by itself (the haptics play the same).
    /// </summary>
    public class TwistCapTask : LearningTask
    {
        [Serializable]
        public class Container
        {
            public string name = "Tight bottle";
            [Tooltip("Root that moves (holder + container + cap).")]
            public Transform root;
            [Tooltip("The cap; it turns about the container's up axis.")]
            public Transform cap;
            public float capRadius = 0.017f;
            [Tooltip("Fingers on the cap.")]
            public GloveChannels channels = GloveChannels.Thumb | GloveChannels.Index;
            [Tooltip("Degrees of turning before the seal breaks.")]
            public float sealDegrees = 150f;
            [Tooltip("Degrees after the seal until the cap comes off.")]
            public float looseDegrees = 270f;
            [Range(0.1f, 1f)] public float startPressure = 0.35f;
            [Range(0.1f, 1f)] public float peakPressure = 0.7f;
            [Range(0.1f, 1f)] public float loosePressure = 0.2f;
            [Tooltip("A click every this many degrees in the tight phase.")]
            public float clickEveryDegrees = 30f;
        }

        [Header("T11 objects")]
        [SerializeField] private TrackedHandFrame patientHand;
        [SerializeField] private Container[] containers = new Container[0];
        [Tooltip("Where a removed cap is put down (local to each container root).")]
        [SerializeField] private Vector3 capRestOffset = new Vector3(0.09f, 0f, -0.04f);

        [Header("T11 leniency")]
        [Tooltip("Fingers within this distance of the cap edge count as on the cap (m).")]
        [SerializeField] private float engageMargin = 0.045f;
        [SerializeField] private float waitForPatientSeconds = 7f;
        [Tooltip("No turning for this long while assisting is allowed → the cap turns by itself (s).")]
        [SerializeField] private float stallSeconds = 4f;
        [SerializeField] private float assistMoveSpeed = 0.2f;
        [SerializeField] private float assistTurnDegPerSec = 60f;
        [Tooltip("Thread pitch: the cap rises this much per full turn (m).")]
        [SerializeField] private float threadRisePerTurn = 0.003f;

        [Header("T11 touch")]
        [SerializeField] private float clickFrequencyHz = 25f;
        [Range(0.1f, 1f)] [SerializeField] private float clickIntensity = 0.6f;
        [SerializeField] private float clickSeconds = 0.12f;
        [SerializeField] private float minClickInterval = 0.3f;
        [SerializeField] private float sealFrequencyHz = 35f;
        [Range(0.1f, 1f)] [SerializeField] private float sealIntensity = 0.85f;
        [SerializeField] private float sealSeconds = 0.25f;
        [Tooltip("Only re-send the pressure when it changed this much (keeps Bluetooth quiet).")]
        [SerializeField] private float pressureStep = 0.05f;

        private Container current;
        private float progressDeg;          // total opening turn so far
        private float patientDeg, assistDeg;
        private float sentPressure;
        private float lastClickTime = -10f;
        private int clicksDone;
        private bool vibrating;
        private Pose[] homes;
        private Pose[] capHomes;

        protected override string TaskName => "T11_OpenBottle";
        protected override bool HideTrackedHandsInWatch => false;

        protected override void Start()
        {
            base.Start();
            if (patientHand != null) patientHand.SetSide(hand);
            homes = new Pose[containers.Length];
            capHomes = new Pose[containers.Length];
            for (int i = 0; i < containers.Length; i++)
            {
                Container c = containers[i];
                if (c.root != null) homes[i] = new Pose(c.root.position, c.root.rotation);
                if (c.cap != null) capHomes[i] = new Pose(c.cap.localPosition, c.cap.localRotation);
            }
            ShowOnly(-1);
        }

        protected override IEnumerator Intro()
        {
            Step("intro");
            Say("Let's open some lids.\nYour fingers will feel the cap turn.");
            yield return Wait(4f);
        }

        protected override IEnumerator RunRound(int r)
        {
            if (containers.Length == 0) { yield return Wait(1f); yield break; }
            int index = (r - 1) % containers.Length;
            current = containers[index];
            ShowOnly(index);
            ResetContainer(index);
            Step("start_" + current.name);
            Say(current.name + ".");
            yield return Wait(1.5f);

            // 1 · fingers on the cap (or the container comes to the fingers)
            Say((current.channels & GloveChannels.Palm) == 0
                ? "Hold the cap with your thumb and finger."
                : "Hold the lid with your whole hand.");
            string who = null;
            yield return UntilOrAssist(OnCap, () => MoveRootTowardsFingers(), w => who = w);
            Step("engage_by_" + who);
            sentPressure = current.startPressure;
            player.Hold(current.channels, sentPressure, 0.5f, "CAP_HOLD");
            Say("Now turn it to open.");

            // 2 · turn until it opens
            yield return Twist();

            // 3 · cap off
            Step("open_patient" + Mathf.RoundToInt(patientDeg) + "_assist" + Mathf.RoundToInt(assistDeg));
            player.Release(current.channels, 0.6f, "CAP_OFF");
            Say("Open! Well done.");
            yield return LiftCapOff();
            yield return Wait(2f);
            yield return GlideRootHome(index);
            yield return Wait(1.5f);
        }

        // ------------------------------------------------------------------ twisting

        private IEnumerator Twist()
        {
            progressDeg = patientDeg = assistDeg = 0f;
            clicksDone = 0;
            bool sealed_ = true;
            float total = current.sealDegrees + current.looseDegrees;
            float idle = 0f, waited = 0f;
            bool assisting = false;
            Vector3? lastAcross = null;

            while (progressDeg < total)
            {
                if (State != TaskState.Running) { lastAcross = null; yield return null; continue; }
                float dt = Time.deltaTime;
                float turned = 0f;

                // the patient's own turn: rotation of the hand about the container axis
                if (patientHand != null && patientHand.IsValid && OnCap())
                {
                    Vector3 axis = current.root.up;
                    Vector3 across = Vector3.ProjectOnPlane(patientHand.Knuckle(1) - patientHand.Knuckle(4), axis);
                    if (lastAcross.HasValue && across.sqrMagnitude > 1e-6f)
                    {
                        // Unity is left-handed: looking down the axis, opening (counter-clockwise) is negative.
                        float d = -Vector3.SignedAngle(lastAcross.Value, across, axis);
                        if (Mathf.Abs(d) < 45f) turned = d;      // ignore tracking jumps
                    }
                    lastAcross = across;
                }
                else lastAcross = null;

                if (turned > 0.2f) { patientDeg += turned; idle = 0f; }
                else idle += dt;
                waited += dt;

                if (!assisting && (idle >= stallSeconds && waited >= waitForPatientSeconds))
                {
                    assisting = true;
                    Step("turn_assist");
                    Say("The cap turns with you. Just feel it.");
                }
                if (assisting && turned <= 0.2f)
                {
                    float a = assistTurnDegPerSec * dt;
                    turned = a;
                    assistDeg += a;
                }
                // a real turn by the patient again: hand control back
                if (assisting && turned > 0.2f && idle == 0f) assisting = false;

                if (turned > 0f)
                {
                    progressDeg = Mathf.Min(total, progressDeg + turned);
                    TurnCap(turned);
                }

                // haptics follow the progress
                if (sealed_)
                {
                    float k = Mathf.Clamp01(progressDeg / Mathf.Max(1f, current.sealDegrees));
                    SetPressure(Mathf.Lerp(current.startPressure, current.peakPressure, k));
                    int clicksDue = Mathf.FloorToInt(progressDeg / Mathf.Max(5f, current.clickEveryDegrees));
                    if (clicksDue > clicksDone && Time.time - lastClickTime >= minClickInterval)
                    {
                        clicksDone = clicksDue;
                        yield return Click(clickIntensity, "CLICK_TIGHT");
                    }
                    if (progressDeg >= current.sealDegrees)
                    {
                        sealed_ = false;
                        Step("seal_break");
                        // after the buzz the pressure comes back at the loose level
                        sentPressure = current.loosePressure;
                        yield return Buzz(sealFrequencyHz, sealIntensity, sealSeconds, "SEAL_BREAK");
                        Say("It's loose now.");
                        clicksDone = 0;
                    }
                }
                else
                {
                    float loose = progressDeg - current.sealDegrees;
                    int clicksDue = Mathf.FloorToInt(loose / Mathf.Max(5f, current.clickEveryDegrees * 1.5f));
                    if (clicksDue > clicksDone && Time.time - lastClickTime >= minClickInterval)
                    {
                        clicksDone = clicksDue;
                        yield return Click(clickIntensity * 0.5f, "CLICK_LOOSE");
                    }
                }
                yield return null;
            }
        }

        private void TurnCap(float degrees)
        {
            if (current.cap == null) return;
            // counter-clockwise from above = negative about local up
            current.cap.Rotate(Vector3.up, -degrees, Space.Self);
            current.cap.localPosition += current.cap.parent.InverseTransformDirection(current.root.up) *
                                         (threadRisePerTurn * degrees / 360f);
        }

        private void SetPressure(float p)
        {
            if (Mathf.Abs(p - sentPressure) < pressureStep || vibrating) return;
            sentPressure = p;
            player.Hold(current.channels, p, 0.8f, "CAP_PRESSURE");
        }

        private IEnumerator Click(float intensity, string id)
        {
            lastClickTime = Time.time;
            yield return Buzz(clickFrequencyHz, intensity, clickSeconds, id);
        }

        /// <summary>A short vibration on the cap fingers, then the hold pressure comes back.</summary>
        private IEnumerator Buzz(float hz, float intensity, float seconds, string id)
        {
            vibrating = true;
            player.Vibrate(current.channels, hz, intensity, id);
            yield return Wait(seconds);
            player.Hold(current.channels, sentPressure, 1f, id + "_END");
            vibrating = false;
        }

        // ------------------------------------------------------------------ contact and assists

        /// <summary>Thumb and index tips (or the hand for a lid) close to the cap.</summary>
        private bool OnCap()
        {
            if (patientHand == null || !patientHand.IsValid || current == null || current.cap == null) return false;
            Vector3 c = current.cap.position;
            Vector3 pinch = Vector3.Lerp(patientHand.Tip(0), patientHand.Tip(1), 0.5f);
            return Vector3.Distance(pinch, c) < current.capRadius + engageMargin;
        }

        private void MoveRootTowardsFingers()
        {
            Vector3 pinch = Vector3.Lerp(patientHand.Tip(0), patientHand.Tip(1), 0.5f);
            Vector3 delta = pinch - current.cap.position;
            current.root.position = Vector3.MoveTowards(current.root.position, current.root.position + delta,
                                                        assistMoveSpeed * Time.deltaTime);
        }

        private IEnumerator UntilOrAssist(Func<bool> done, Action assist, Action<string> who)
        {
            float waited = 0f;
            bool assisting = false, askedForHand = false;
            while (true)
            {
                if (State != TaskState.Running) { yield return null; continue; }
                if (patientHand == null || !patientHand.IsValid)
                {
                    if (!askedForHand) { Say("Please hold your hand up where the headset can see it."); askedForHand = true; }
                    yield return null;
                    continue;
                }
                if (done()) { who(assisting ? "assist" : "patient"); yield break; }
                waited += Time.deltaTime;
                if (!assisting && waited >= waitForPatientSeconds) { assisting = true; Step("assist"); }
                if (assisting) assist?.Invoke();
                yield return null;
            }
        }

        private IEnumerator LiftCapOff()
        {
            Transform cap = current.cap;
            if (cap == null) yield break;
            Vector3 from = cap.position;
            Vector3 up = current.root.up;
            Vector3 to = current.root.TransformPoint(capRestOffset);
            float t = 0f;
            while (t < 1f)
            {
                if (State == TaskState.Running) t += Time.deltaTime / 1.2f;
                float k = Mathf.SmoothStep(0f, 1f, t);
                cap.position = Vector3.Lerp(from, to, k) + up * (Mathf.Sin(k * Mathf.PI) * 0.06f);
                yield return null;
            }
        }

        private IEnumerator GlideRootHome(int index)
        {
            Transform root = containers[index].root;
            Vector3 p0 = root.position;
            float t = 0f;
            while (t < 1f)
            {
                if (State == TaskState.Running) t += Time.deltaTime / 0.8f;
                root.position = Vector3.Lerp(p0, homes[index].position, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        private void ResetContainer(int i)
        {
            Container c = containers[i];
            if (c.root != null) c.root.SetPositionAndRotation(homes[i].position, homes[i].rotation);
            if (c.cap != null)
            {
                c.cap.localPosition = capHomes[i].position;
                c.cap.localRotation = capHomes[i].rotation;
            }
        }

        private void ShowOnly(int index)
        {
            for (int i = 0; i < containers.Length; i++)
                if (containers[i].root != null) containers[i].root.gameObject.SetActive(index < 0 || i == index);
        }

        protected override void OnFinished()
        {
            vibrating = false;
            for (int i = 0; i < containers.Length; i++) ResetContainer(i);
            ShowOnly(-1);
        }
    }
}
