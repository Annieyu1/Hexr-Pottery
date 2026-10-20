using System;
using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// T5 吃饭 (docs/10 §5.4): "eyes on the food, the hand keeps hold of the spoon".
    /// Uses the patient's own tracked VR hand. Nothing is answered or scored.
    ///
    /// One round: pick up the spoon (three-finger grip pressure, held all the time) → scoop soft
    /// porridge (soft scrape, slow light load) → eat → scoop a peanut (sharp click, quick firmer
    /// load) → eat → put the spoon down. Previews D1 (which three fingers), D2 (full/empty,
    /// soft/hard) and D3 (the scrape vibration).
    ///
    /// Every step is lenient: if the patient does not move, the object comes to the hand (spoon
    /// to the hand, bowl up to the spoon, food to the mouth), so patients who cannot lift the arm
    /// still feel the whole sequence.
    /// </summary>
    public class EatingTask : LearningTask
    {
        [Header("T5 objects")]
        [SerializeField] private TrackedHandFrame patientHand;
        [Tooltip("Spoon root; its origin is the grip point, +Z runs along the handle to the bowl.")]
        [SerializeField] private Transform spoon;
        [SerializeField] private Transform spoonTip;
        [Tooltip("The food on the spoon (shown when full).")]
        [SerializeField] private Renderer bite;
        [SerializeField] private Transform spoonRest;
        [SerializeField] private Transform bowl;
        [Tooltip("A point on the porridge surface, child of the bowl.")]
        [SerializeField] private Transform bowlSurface;
        [SerializeField] private float bowlRadius = 0.07f;
        [SerializeField] private GameObject peanuts;
        [SerializeField] private Material porridgeMaterial;
        [SerializeField] private Material peanutMaterial;
        [Tooltip("The eye camera (CenterEyeAnchor); the mouth is just below it.")]
        [SerializeField] private Transform head;

        [Header("T5 leniency")]
        [SerializeField] private float grabDistance = 0.07f;
        [SerializeField] private float mouthDistance = 0.13f;
        [Tooltip("If the patient has not done a step after this long, the object comes to them (s).")]
        [SerializeField] private float waitForPatientSeconds = 7f;
        [SerializeField] private float assistSpeed = 0.2f;
        [SerializeField] private float followSharpness = 14f;
        [Tooltip("Spoon handle direction in the hand frame (forward, a bit down toward the palm side).")]
        [SerializeField] private Vector3 gripDirection = new Vector3(0f, -0.35f, 1f);

        [Header("T5 touch events (docs/10 §5.9)")]
        [SerializeField] private HapticSpec grip = new HapticSpec
            { id = "TRIPOD_HOLD", channels = GloveChannels.Thumb | GloveChannels.Index | GloveChannels.Middle,
              kind = HapticKind.Hold, intensity = 0.4f, speed = 0.5f };
        [SerializeField] private HapticSpec scrape = new HapticSpec
            { id = "SCRAPE_BOWL", channels = GloveChannels.Thumb | GloveChannels.Index, kind = HapticKind.Vibrate,
              frequencyHz = 20f, intensity = 0.45f, seconds = 0.25f };
        [SerializeField] private HapticSpec fullSoft = new HapticSpec
            { id = "SPOON_FULL_SOFT", channels = GloveChannels.Thumb | GloveChannels.Index | GloveChannels.Middle,
              kind = HapticKind.Hold, intensity = 0.48f, speed = 0.2f };
        [SerializeField] private HapticSpec peanutClick = new HapticSpec
            { id = "PEANUT_CLICK", channels = GloveChannels.Thumb | GloveChannels.Index, kind = HapticKind.Squeeze,
              frequencyHz = 2f, count = 1, intensity = 0.7f, endIntensity = 0.55f };
        [SerializeField] private HapticSpec fullHard = new HapticSpec
            { id = "SPOON_FULL_HARD", channels = GloveChannels.Thumb | GloveChannels.Index | GloveChannels.Middle,
              kind = HapticKind.Hold, intensity = 0.55f, speed = 0.9f };
        [SerializeField] private HapticSpec letGo = new HapticSpec
            { id = "RELEASE_SPOON", channels = GloveChannels.Thumb | GloveChannels.Index | GloveChannels.Middle,
              kind = HapticKind.Release, speed = 0.4f };

        [SerializeField] private float pauseAfterBite = 2f;

        private bool spoonInHand;
        private Pose bowlHome;
        private Pose restPose;

        protected override string TaskName => "T5_Eating";
        protected override bool HideTrackedHandsInWatch => false;

        protected override void Start()
        {
            base.Start();
            if (patientHand != null) patientHand.SetSide(hand);
            if (bowl != null) bowlHome = new Pose(bowl.position, bowl.rotation);
            if (spoonRest != null) restPose = new Pose(spoonRest.position, spoonRest.rotation);
            PutSpoonAtRest();
            SetBite(null);
        }

        protected override IEnumerator Intro()
        {
            Step("intro");
            Say("Breakfast time.\nLook at your bowl; your fingers will feel the spoon.");
            yield return Wait(4f);
        }

        protected override IEnumerator RunRound(int r)
        {
            // 1 · pick up the spoon
            Step("pick_up");
            Say("Pick up the spoon.");
            string who = null;
            yield return UntilOrAssist(() => Vector3.Distance(PinchPoint(), spoon.position) < grabDistance,
                                       () => MoveTowardsPose(spoon, GripPose()), w => who = w);
            Step("grip_by_" + who);
            spoonInHand = true;
            player.Play(grip);
            Say("Three fingers hold the spoon.");
            yield return Wait(2f);

            // 2 · soft porridge
            yield return Bite(false);
            // 3 · a peanut: the hard contrast
            yield return Bite(true);

            // 4 · put it down
            Step("put_down");
            Say("Put the spoon down.");
            yield return UntilOrAssist(() => spoonTip.position.y < restPose.position.y + 0.04f, null, w => who = w);
            Step("put_down_by_" + who);
            spoonInHand = false;
            player.Play(letGo);
            yield return GlideTo(spoon, restPose, 0.8f);
            Say("Well done.");
            yield return Wait(3f);
        }

        private IEnumerator Bite(bool peanut)
        {
            string food = peanut ? "peanut" : "porridge";
            if (peanuts != null) peanuts.SetActive(peanut);
            Step("scoop_" + food);
            Say(peanut ? "Next: scoop a peanut.\nIt will feel hard and quick." : "Scoop some porridge.\nIt will feel soft.");

            // the bowl comes up to the spoon if the spoon does not go into the bowl
            string who = null;
            yield return UntilOrAssist(TipInFood, () => MoveTowardsPose(bowl, BowlUnderTip()), w => who = w);
            Step("scoop_" + food + "_by_" + who);

            if (peanut)
            {
                player.Play(peanutClick);
                yield return Wait(peanutClick.count / Mathf.Max(0.1f, peanutClick.frequencyHz));
                player.Play(fullHard);
            }
            else
            {
                player.Play(scrape);
                yield return Wait(scrape.seconds);
                player.StopVibration(scrape.channels, "SCRAPE_END");
                player.Play(fullSoft);
            }
            SetBite(peanut ? peanutMaterial : porridgeMaterial);
            Say(peanut ? "A peanut: firmer on your fingers." : "A full spoon: a little heavier.");
            yield return GlideTo(bowl, bowlHome, 0.8f);

            // eat
            Step("eat_" + food);
            Say("Bring it to your mouth.");
            yield return UntilOrAssist(() => Vector3.Distance(spoonTip.position, Mouth()) < mouthDistance,
                                       null, w => who = w);
            Step("eat_" + food + "_by_" + who);
            if (who == "assist") yield return FlyBiteToMouth();
            SetBite(null);
            player.Play(grip);       // empty spoon: back to the plain grip
            Say("Yum. The spoon is light again.");
            yield return Wait(pauseAfterBite);
        }

        // ------------------------------------------------------------------ the spoon follows the hand

        private void LateUpdate()
        {
            if (!spoonInHand || patientHand == null || !patientHand.IsValid || State == TaskState.Paused) return;
            Pose p = GripPose();
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            spoon.SetPositionAndRotation(Vector3.Lerp(spoon.position, p.position, k), Quaternion.Slerp(spoon.rotation, p.rotation, k));
        }

        private Vector3 PinchPoint()
        {
            return Vector3.Lerp(patientHand.Tip(0), patientHand.Tip(1), 0.5f);
        }

        private Pose GripPose()
        {
            Quaternion handRot = patientHand.Rotation;
            Vector3 dir = handRot * gripDirection.normalized;
            Vector3 up = handRot * Vector3.up;
            return new Pose(PinchPoint(), Quaternion.LookRotation(dir, up));
        }

        private bool TipInFood()
        {
            Vector3 tip = spoonTip.position;
            Vector3 c = bowl.position;
            Vector2 d = new Vector2(tip.x - c.x, tip.z - c.z);
            return d.magnitude < bowlRadius * 0.85f && tip.y < bowlSurface.position.y + 0.015f;
        }

        /// <summary>Bowl pose that puts the food surface just under the spoon tip.</summary>
        private Pose BowlUnderTip()
        {
            Vector3 surfaceOffset = bowlSurface.position - bowl.position;
            Vector3 target = spoonTip.position - surfaceOffset + Vector3.down * 0.01f;
            return new Pose(target, bowl.rotation);
        }

        private Vector3 Mouth()
        {
            if (head == null) return spoon.position + Vector3.up;
            return head.position - head.up * 0.08f + head.forward * 0.05f;
        }

        // ------------------------------------------------------------------ waiting and assisting

        /// <summary>
        /// Wait for the patient to make 'done' true. After waitForPatientSeconds, run 'assist' each
        /// frame (move the object toward the hand) until done; if there is no assist motion, the
        /// step simply completes. Reports "patient" or "assist".
        /// </summary>
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
                if (!assisting && waited >= waitForPatientSeconds)
                {
                    assisting = true;
                    Step("assist");
                    if (assist == null) { who("assist"); yield break; }
                }
                if (assisting) assist();
                yield return null;
            }
        }

        private void MoveTowardsPose(Transform t, Pose p)
        {
            t.SetPositionAndRotation(Vector3.MoveTowards(t.position, p.position, assistSpeed * Time.deltaTime),
                                     Quaternion.RotateTowards(t.rotation, p.rotation, 120f * Time.deltaTime));
        }

        private IEnumerator GlideTo(Transform t, Pose p, float seconds)
        {
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            float time = 0f;
            while (time < seconds)
            {
                if (State == TaskState.Running) time += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, time / seconds);
                t.SetPositionAndRotation(Vector3.Lerp(p0, p.position, k), Quaternion.Slerp(r0, p.rotation, k));
                yield return null;
            }
        }

        private IEnumerator FlyBiteToMouth()
        {
            if (bite == null) yield break;
            Transform b = bite.transform;
            Transform parent = b.parent;
            Vector3 local = b.localPosition;
            b.SetParent(null, true);
            Vector3 from = b.position;
            float time = 0f;
            while (time < 0.8f)
            {
                if (State == TaskState.Running) time += Time.deltaTime;
                b.position = Vector3.Lerp(from, Mouth(), Mathf.SmoothStep(0f, 1f, time / 0.8f));
                yield return null;
            }
            b.SetParent(parent, false);
            b.localPosition = local;
        }

        private void SetBite(Material m)
        {
            if (bite == null) return;
            bite.enabled = m != null;
            if (m != null) bite.sharedMaterial = m;
        }

        private void PutSpoonAtRest()
        {
            spoonInHand = false;
            if (spoon != null && spoonRest != null) spoon.SetPositionAndRotation(restPose.position, restPose.rotation);
        }

        protected override void OnFinished()
        {
            PutSpoonAtRest();
            SetBite(null);
            if (bowl != null) bowl.SetPositionAndRotation(bowlHome.position, bowlHome.rotation);
            if (peanuts != null) peanuts.SetActive(false);
        }
    }
}
