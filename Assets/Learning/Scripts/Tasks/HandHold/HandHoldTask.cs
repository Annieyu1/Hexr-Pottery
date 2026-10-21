using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>
    /// T1 牵手 (docs/11 §3), using the patient's own tracked VR hand. UI follows the Figma
    /// "HexR Learning UI – Step Card" design (2026-10-19): one step card above the work area.
    ///
    ///   Step 1    The model hand waits over the waiting pad and beckons; a ghost hand + arrow show
    ///             where the patient's hand goes. A grip near the hand binds it to the patient's hand.
    ///   Step 2    Gentle hold, just feel it.        Step 2-1  "Now hold back with the same strength".
    ///   Step 3    Firm hold, just feel it.          Step 3-1  hold back again.
    ///   Step 4    Round x: 2, 3 or 4 light or firm squeezes; the patient answers on floating number
    ///             buttons ("Feel again" replays them). Correct = green card; wrong = red card and the
    ///             right number is squeezed once more, no second answer. After 5 rounds: One more / Finish.
    ///   Step 5    Well done! Congratulations! Task complete.
    ///
    /// Nothing moves by itself (design rule): no auto-assist. After tipAfterSeconds without holding
    /// the hand, a yellow Tip card appears; the therapist can Skip step. Whenever the patient lets go
    /// the model hand glides back to the waiting pad and the task stays in the same step.
    /// The glove cannot measure the patient's force, so steps 2-1/3-1 only log the grip curl.
    /// </summary>
    public class HandHoldTask : LearningTask
    {
        [Header("T1 hands")]
        [SerializeField] private TrackedHandFrame patientHand;
        [Tooltip("The model hand the patient holds.")]
        [SerializeField] private StylizedHand partnerHand;
        [Tooltip("Where the model hand waits for the patient (over the waiting pad).")]
        [SerializeField] private Transform partnerOffer;
        [SerializeField] private Transform partnerAway;

        [Header("T1 waiting pose (applied in Play to PartnerOffer / PartnerAway; tune live)")]
        [Tooltip("Fingers point this far below horizontal, like a hand held out across the table (deg). " +
                 "The hand pivots about its palm; the ghost hand, arrow and pad follow.")]
        [Range(-10f, 45f)] [SerializeField] private float offerPitchDegrees = 20f;
        [Tooltip("Turns the model hand about its fingers (deg). 0 = thumb up, palm facing the patient's palm; " +
                 "positive turns the model palm downward (so the patient's palm faces more upward).")]
        [Range(-60f, 60f)] [SerializeField] private float offerRollDegrees = 0f;
        [Tooltip("Raises the waiting hand (and its ghost / arrow) this much (m).")]
        [SerializeField] private float offerRaise = 0f;
        [Tooltip("Waiting pad on the desk; it slides to stay under the model palm. Found by name if empty.")]
        [SerializeField] private Transform waitingPad;

        [Header("T1 beckoning (while waiting to be held)")]
        [Tooltip("One open-close of the fingers takes this long (s).")]
        [SerializeField] private float beckonPeriod = 2.4f;
        [Tooltip("Finger curl range while beckoning (0 flat, 1 fist).")]
        [SerializeField] private float beckonOpenCurl = 0.08f;
        [SerializeField] private float beckonClosedCurl = 0.42f;
        [Tooltip("How far the hand bobs up and down (m). 0 keeps it lined up with the ghost hand.")]
        [SerializeField] private float beckonBob = 0f;
        [Tooltip("Ghost hand + arrow at the waiting pad, shown while the model hand waits there.")]
        [SerializeField] private HandGuide guide;

        [Header("T1 model hand curl while held (0 flat, 1 fist; lower = less clipping)")]
        [SerializeField] private float gentleCurl = 0.3f;
        [SerializeField] private float firmCurl = 0.45f;
        [SerializeField] private float squeezePeakLight = 0.42f;
        [SerializeField] private float squeezePeakFirm = 0.55f;

        [Header("T1 grip")]
        [Tooltip("Mean finger curl (deg) that counts as gripping. See TrackedHandFrame.GripCurlDegrees.")]
        [SerializeField] private float gripCurlDegrees = 50f;
        [Tooltip("Below this the grip is lost (lower than gripCurlDegrees so it does not flicker).")]
        [SerializeField] private float releaseCurlDegrees = 35f;
        [Tooltip("The grip must be lost this long before the model hand lets go (s).")]
        [SerializeField] private float releaseDebounceSeconds = 0.4f;
        [Tooltip("Model palm to patient palm closer than this = near enough to take hold (m).")]
        [SerializeField] private float grabDistance = 0.08f;
        [Tooltip("How tightly the held hand follows the patient's hand.")]
        [SerializeField] private float followSharpness = 12f;
        [Tooltip("Gap between the two palm surfaces when holding (m).")]
        [SerializeField] private float palmGap = 0.022f;
        [Tooltip("Model palm sits this far toward the patient's fingers (m).")]
        [SerializeField] private float palmForward = 0.02f;

        [Header("T1 touch events (docs/10 §5.9)")]
        [SerializeField] private HapticSpec holdLight = new HapticSpec
            { id = "HOLD_LIGHT", channels = GloveChannels.WholeHand, kind = HapticKind.Hold, intensity = 0.25f, speed = 0.2f };
        [SerializeField] private HapticSpec holdFirm = new HapticSpec
            { id = "HOLD_FIRM", channels = GloveChannels.WholeHand, kind = HapticKind.Hold, intensity = 0.65f, speed = 0.7f };
        [SerializeField] private HapticSpec squeezeLight = new HapticSpec
            { id = "SQUEEZE_LIGHT_N", channels = GloveChannels.WholeHand, kind = HapticKind.Squeeze, intensity = 0.4f,
              frequencyHz = 1f, count = 2, endIntensity = 0.25f };
        [Tooltip("Multi-channel pulses are capped at 0.7 by the SDK.")]
        [SerializeField] private HapticSpec squeezeFirm = new HapticSpec
            { id = "SQUEEZE_FIRM_N", channels = GloveChannels.WholeHand, kind = HapticKind.Squeeze, intensity = 0.7f,
              frequencyHz = 1f, count = 2, endIntensity = 0.25f };
        [SerializeField] private float letGoSpeed = 0.6f;

        [Header("T1 timing (s)")]
        [Tooltip("Steps 2 and 3: how long the patient feels the hold (counted only while holding).")]
        [SerializeField] private float feelSeconds = 5f;
        [Tooltip("Steps 2-1 and 3-1: how long the patient holds back.")]
        [SerializeField] private float matchSeconds = 5f;
        [SerializeField] private float previewSeconds = 2f;
        [SerializeField] private float feedbackSeconds = 2.5f;
        [Tooltip("Green 'Done' state shown at the end of a step (design: 1 s).")]
        [SerializeField] private float doneSeconds = 1f;
        [Tooltip("Yellow Tip card after this long without holding the hand (design: 8 s).")]
        [SerializeField] private float tipAfterSeconds = 8f;
        [SerializeField] private float glideSeconds = 1.2f;

        [Header("T1 step 4 (count the squeezes)")]
        [SerializeField] private int[] squeezeCounts = { 2, 3, 4 };
        [Tooltip("Rounds before One more / Finish appear.")]
        [SerializeField] private int minQuizRounds = 5;

        [Header("T1 UI")]
        [SerializeField] private StepCard card;
        [Tooltip("Start screen with the handshake animation, shown instead of the step card while idle.")]
        [SerializeField] private T1StartPanel startPanel;
        [SerializeField] private TipCard tip;
        [Tooltip("Number buttons 2 / 3 / 4 + Feel again.")]
        [SerializeField] private GameObject answerButtons;
        [Tooltip("One more / Finish.")]
        [SerializeField] private GameObject finishChoice;
        [Tooltip("Correct / wrong sounds (key 0..4 = C..G).")]
        [SerializeField] private ToneSynth synth;
        [Tooltip("Background music; it is turned down while squeezes are counted and answered.")]
        [SerializeField] private TaskBgm bgm;

        private const string ChipName = "Hand hold";
        private const string RegripHint = "Hold the hand again to continue";
        private const string TipText = "Put your hand where the ghost hand is";

        // grip / binding
        private bool engaged;            // from step 1 on: the grip is watched
        private bool attached;           // model hand bound to the patient's hand
        private bool gripping;
        private float lostFor;
        private bool beckoning;          // waiting to be held: fingers wave
        private float beckonTime;
        private Coroutine returning;     // gliding back to the waiting pad after the patient let go
        private Vector3 bobApplied;

        // waiting pose: the markers as built, and the values last applied
        private bool offerBaseSaved;
        private Pose offerBase, awayBase;
        private Vector3 padHome;
        private Vector3 appliedOffer = new Vector3(float.NaN, 0f, 0f);
        private bool quietRelease;       // answering / choosing: letting go needs no prompt
        private HapticSpec currentHold;  // pressure to (re)apply while held
        private float heldCurl = 0.3f;
        private string stepHint = "";
        private float unheldFor;
        private int releaseCount, tipCount;

        // step 4 / therapist
        private bool answerOpen, choiceOpen, feelAgain, wantFinish, skipRequested;
        private int answer = -1;
        private int quizTrials, quizCorrect;
        private readonly List<Vector2Int> bag = new List<Vector2Int>();   // x = count, y = 1 firm / 0 light

        private Vector3 partnerPalmLocal;

        protected override string TaskName => "T1_HandHold";
        protected override bool HideTrackedHandsInWatch => false;
        protected override string BodyEndReason => "completed";
        protected override string EndMessage => "";

        protected override void Start()
        {
            base.Start();
            if (patientHand != null) patientHand.SetSide(hand);
            partnerPalmLocal = partnerHand != null && partnerHand.Contact(5) != null
                ? partnerHand.transform.InverseTransformPoint(partnerHand.Contact(5).position) : Vector3.zero;
            ApplyOfferPose();
            ResetScene();
            ShowCard(ChipName, "Ready", "Press Start to begin", 0, CardTone.Normal);
            ShowStartPanel(true);
        }

        /// <summary>Idle: the start screen replaces the step card; once started, the card is back.</summary>
        private void ShowStartPanel(bool on)
        {
            if (startPanel == null) return;
            startPanel.Show(on);
            if (card != null) card.gameObject.SetActive(!on);
        }

        private void ResetScene()
        {
            engaged = attached = gripping = false;
            beckoning = false;
            bobApplied = Vector3.zero;
            if (returning != null) { StopCoroutine(returning); returning = null; }
            if (guide != null) guide.Show(false);
            quietRelease = answerOpen = choiceOpen = feelAgain = skipRequested = false;
            squeezing = countingPreview = false;
            currentHold = null;
            unheldFor = 0f;
            if (partnerHand != null)
            {
                Place(partnerHand, partnerAway);
                partnerHand.SetCurlAllImmediate(0.15f);
                partnerHand.SetVisible(false);
            }
            SetActive(answerButtons, false);
            SetActive(finishChoice, false);
            if (tip != null) tip.Hide();
            if (card != null) card.SetOutline(false);
        }

        // ------------------------------------------------------------------ flow

        protected override IEnumerator Intro()
        {
            ShowStartPanel(false);
            ResetScene();
            releaseCount = tipCount = quizTrials = quizCorrect = 0;
            bag.Clear();
            Step("intro");
            ShowCard(ChipName, "Welcome", "Today we will hold hands", 0, CardTone.Normal);
            yield return Wait(3f);
        }

        // Not used: Body() runs the steps instead of timed rounds.
        protected override IEnumerator RunRound(int roundIndex) { yield break; }

        protected override IEnumerator Body()
        {
            yield return Step1Take();
            yield return FeelAndMatch("2", "A gentle hold", "Feel a light press on your whole hand", "gentle", holdLight, gentleCurl);
            yield return FeelAndMatch("3", "A firm hold", "It feels stronger now", "firm", holdFirm, firmCurl);

            // step 4: back to the gentle hold as the base, then count the squeezes
            currentHold = holdLight;
            heldCurl = gentleCurl;
            if (attached) { player.Play(holdLight); partnerHand.SetCurlAll(heldCurl); }
            for (int r = 1; ; r++)
            {
                round = r;
                if (log != null) log.Round = r;
                yield return CountRound(r);
                if (r >= minQuizRounds)
                {
                    yield return AskOneMoreOrFinish();
                    if (wantFinish) break;
                }
            }

            Step("step5_well_done");
            ShowCard("Step 5 / 5", "Well done!", "Congratulations! Task complete", 5, CardTone.Normal);
        }

        private IEnumerator Step1Take()
        {
            BeginStep("step1_offer");
            ShowCard("Step 1 / 5", "Reach for the hand", "Move your hand toward the open hand", 1, CardTone.Normal);
            partnerHand.SetVisible(true);
            partnerHand.SetCurlAllImmediate(0.15f);
            yield return Glide(partnerHand, partnerAway, partnerOffer);
            engaged = true;
            StartBeckoning();

            float waited = 0f;
            while (!attached && !skipRequested)
            {
                if (State == TaskState.Running) waited += Time.deltaTime;
                yield return null;
            }
            Step(attached ? "step1_held" : "step1_skipped");
            log?.Trial("step1_take", -1, "", -1, -1, waited, attached ? "patient_reached" : "skipped");
            yield return ShowDone();
        }

        /// <summary>Steps 2 / 2-1 (gentle) and 3 / 3-1 (firm): feel the hold, then hold back the same.</summary>
        private IEnumerator FeelAndMatch(string n, string title, string feelHint, string strength, HapticSpec spec, float curl)
        {
            BeginStep("step" + n + "_" + strength + "_hold");
            int filled = int.Parse(n);
            ShowCard("Step " + n + " / 5", title, feelHint, filled, CardTone.Normal);
            yield return HoldFor(1f);
            currentHold = spec;
            heldCurl = curl;
            if (attached) { player.Play(spec); partnerHand.SetCurlAll(curl); }
            yield return HoldFor(feelSeconds);
            yield return ShowDone();

            BeginStep("step" + n + "-1_match_" + strength);
            ShowCard("Step " + n + "-1 / 5", title, "Now hold back with the same strength", filled, CardTone.Normal);
            float sum = 0f, t = 0f;
            int samples = 0;
            while (t < matchSeconds && !skipRequested)
            {
                if (State == TaskState.Running && attached)
                {
                    t += Time.deltaTime;
                    sum += patientHand.GripCurlDegrees;
                    samples++;
                }
                yield return null;
            }
            float mean = samples > 0 ? sum / samples : 0f;
            log?.Trial("match_" + strength, -1, strength, -1, -1, -1f,
                       skipRequested ? "skipped" : "mean_grip_curl_deg=" + mean.ToString("0.0", CultureInfo.InvariantCulture));
            yield return ShowDone();
        }

        private IEnumerator CountRound(int r)
        {
            if (bag.Count == 0) RefillBag();
            Vector2Int pick = bag[0];
            bag.RemoveAt(0);
            int count = pick.x;
            bool firm = pick.y == 1;
            string strength = firm ? "firm" : "light";

            BeginStep("step4_preview");
            ShowCard("Step 4 / 5", "Count the squeezes", "Keep your hand still and count · Round " + r, 4, CardTone.Normal);
            yield return WaitHeld();
            countingPreview = true;   // music already down before the first squeeze
            yield return HoldFor(previewSeconds);
            countingPreview = false;
            yield return Squeezes(count, firm, "step4_squeeze_" + count + "_" + strength);
            if (skipRequested) { log?.Trial("count_squeezes", count, strength, -1, -1, -1f, "skipped"); yield break; }

            // answer: letting go is fine here; Feel again replays the same squeezes
            float asked = 0f;
            int feelAgainCount = 0;
            while (true)
            {
                Step("step4_answer");
                answer = -1;
                feelAgain = false;
                quietRelease = true;
                answerOpen = true;
                SetActive(answerButtons, true);
                ShowCard("Step 4 / 5", "How many squeezes?", "Touch the number you felt", 4, CardTone.Normal);
                while (answer < 0 && !feelAgain && !skipRequested)
                {
                    if (State == TaskState.Running) asked += Time.deltaTime;
                    yield return null;
                }
                answerOpen = false;
                SetActive(answerButtons, false);
                quietRelease = false;
                if (!feelAgain) break;

                feelAgainCount++;
                log?.Trial("feel_again", count, strength, -1, -1, -1f, "");
                ShowCard("Step 4 / 5", "Count the squeezes", "Feel them again · Round " + r, 4, CardTone.Normal);
                yield return WaitHeld();
                yield return HoldFor(1f);
                yield return Squeezes(count, firm, "step4_feel_again_" + count + "_" + strength);
            }
            if (skipRequested) { log?.Trial("count_squeezes", count, strength, -1, -1, asked, "skipped"); yield break; }

            bool correct = answer == count;
            quizTrials++;
            if (correct) quizCorrect++;
            log?.Trial("count_squeezes", count, strength, answer, correct ? 1 : 0, asked,
                       feelAgainCount > 0 ? "feel_again=" + feelAgainCount : "");
            if (correct)
            {
                ShowCard("Step 4 / 5", "Yes, " + count + " squeezes", "Well done", 4, CardTone.Correct);
                PlayFeedbackSound(true);
                yield return Wait(feedbackSeconds);
            }
            else
            {
                // no second answer: the right number is squeezed once more, then the next round
                ShowCard("Step 4 / 5", "It was " + count + " squeezes", "Let's feel them again", 4, CardTone.Wrong);
                PlayFeedbackSound(false);
                yield return Wait(1.5f);
                yield return WaitHeld();
                yield return Squeezes(count, firm, "step4_show_again_" + count + "_" + strength);
                yield return Wait(1f);
            }
        }

        /// <summary>count squeezes on the held hand; if the patient lets go part way, repeat once they hold again.</summary>
        private IEnumerator Squeezes(int count, bool firm, string stepName)
        {
            squeezing = true;
            while (!skipRequested)
            {
                Step(stepName);
                HapticSpec sq = (firm ? squeezeFirm : squeezeLight).Clone();
                sq.count = count;
                player.Play(sq);
                bool cut = false;
                float period = 1f / Mathf.Max(0.1f, sq.frequencyHz), t = 0f, dur = count * period;
                float high = firm ? squeezePeakFirm : squeezePeakLight;
                while (t < dur)
                {
                    if (!attached || skipRequested) { cut = true; break; }
                    if (State == TaskState.Running) t += Time.deltaTime;
                    partnerHand.SetCurlAll(Mathf.Repeat(t / period, 1f) < 0.5f ? high : heldCurl);
                    yield return null;
                }
                if (!cut) break;
                if (skipRequested) break;
                log?.Trial("squeeze_interrupted", count, firm ? "firm" : "light", -1, -1, -1f, "patient_let_go");
                yield return WaitHeld();
                yield return HoldFor(1f);
            }
            if (attached) partnerHand.SetCurlAll(heldCurl);
            yield return Wait(0.6f);
            squeezing = false;
        }

        // Step 4 counts touches: keep the music low from the count preview through the answer.
        private bool squeezing, countingPreview;

        private IEnumerator AskOneMoreOrFinish()
        {
            Step("step4_choice");
            wantFinish = false;
            choiceOpen = true;
            quietRelease = true;
            SetActive(finishChoice, true);
            ShowCard("Step 4 / 5", "Great work!", "One more round, or finish?", 4, CardTone.Normal);
            while (choiceOpen && !skipRequested) yield return null;
            if (skipRequested) wantFinish = true;
            choiceOpen = false;
            quietRelease = false;
            SetActive(finishChoice, false);
        }

        private IEnumerator ShowDone()
        {
            if (skipRequested || card == null) yield break;
            card.SetTone(CardTone.Done);
            yield return Wait(doneSeconds);
        }

        private void BeginStep(string stepName)
        {
            skipRequested = false;
            Step(stepName);
        }

        private void RefillBag()
        {
            foreach (int c in squeezeCounts)
            {
                bag.Add(new Vector2Int(c, 0));
                bag.Add(new Vector2Int(c, 1));
            }
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }

        private void PlayFeedbackSound(bool correct)
        {
            if (!SoundOn || synth == null) return;
            if (correct) { synth.Play(2); StartCoroutine(PlayLater(4, 0.15f)); }
            else synth.Play(0);
        }

        private IEnumerator PlayLater(int key, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (synth != null) synth.Play(key);
        }

        // ------------------------------------------------------------------ card

        /// <summary>Shows a card; while the patient has let go, the hint keeps asking them to hold again.</summary>
        private void ShowCard(string chip, string title, string hint, int filled, CardTone tone)
        {
            stepHint = hint;
            if (card != null) card.Show(chip, title, NeedsRegrip ? RegripHint : hint, filled, tone);
        }

        private bool NeedsRegrip => engaged && !attached && !quietRelease;

        // ------------------------------------------------------------------ patient / therapist input (poke buttons / keys)

        public void Answer(int n)
        {
            if (!answerOpen || State != TaskState.Running) return;
            answer = n;
        }

        public void FeelAgain()
        {
            if (!answerOpen || State != TaskState.Running) return;
            feelAgain = true;
        }

        public void OneMore()
        {
            if (!choiceOpen || State != TaskState.Running) return;
            wantFinish = false;
            choiceOpen = false;
        }

        public void FinishTraining()
        {
            if (!choiceOpen || State != TaskState.Running) return;
            wantFinish = true;
            choiceOpen = false;
        }

        /// <summary>Therapist: leave the current step (a skipped round is logged as such).</summary>
        public void SkipStep()
        {
            if (State != TaskState.Running || skipRequested) return;
            skipRequested = true;
            log?.Trial("skip_step", -1, "", -1, -1, -1f, log.Step);
        }

        public void OnAnswer2(Oculus.Interaction.PointerEvent e) { Answer(2); }
        public void OnAnswer3(Oculus.Interaction.PointerEvent e) { Answer(3); }
        public void OnAnswer4(Oculus.Interaction.PointerEvent e) { Answer(4); }
        public void OnFeelAgain(Oculus.Interaction.PointerEvent e) { FeelAgain(); }
        public void OnOneMore(Oculus.Interaction.PointerEvent e) { OneMore(); }
        public void OnFinishTraining(Oculus.Interaction.PointerEvent e) { FinishTraining(); }
        public void OnSkipStep(Oculus.Interaction.PointerEvent e) { SkipStep(); }

        // ------------------------------------------------------------------ grip watching

        protected override void Update()
        {
            base.Update();
            if (bgm != null) bgm.Ducked = State == TaskState.Running && (squeezing || countingPreview || answerOpen);
            ApplyOfferPose();
            HandleKeys();
            UpdateTip();
            if (!engaged || State != TaskState.Running || patientHand == null) return;

            bool valid = patientHand.IsValid;
            float curl = valid ? patientHand.GripCurlDegrees : 0f;
            gripping = valid && curl > (gripping ? releaseCurlDegrees : gripCurlDegrees);

            if (attached)
            {
                lostFor = gripping ? 0f : lostFor + Time.deltaTime;
                if (lostFor >= releaseDebounceSeconds) LetGo();
            }
            else if (returning == null && gripping && Vector3.Distance(PartnerPalm, PalmSurface()) < grabDistance)
            {
                TakeHold();
            }
        }

        /// <summary>Design: after 8 s without holding the hand, a yellow Tip card and a yellow card outline.</summary>
        private void UpdateTip()
        {
            if (tip == null) return;
            if (State == TaskState.Paused) return;
            bool waiting = State == TaskState.Running && NeedsRegrip;
            unheldFor = waiting ? unheldFor + Time.deltaTime : 0f;
            bool show = waiting && unheldFor >= tipAfterSeconds;
            if (show && !tip.Visible)
            {
                tipCount++;
                log?.Trial("tip_shown", -1, "", -1, -1, unheldFor, log.Step);
                tip.Show(TipText);
            }
            else if (!show && tip.Visible) tip.Hide();
            if (card != null) card.SetOutline(show);
        }

        private void TakeHold()
        {
            attached = true;
            lostFor = 0f;
            if (returning != null) { StopCoroutine(returning); returning = null; }
            StopBeckoning();
            log?.Trial("grip", -1, "", -1, -1, -1f, "on");
            if (currentHold != null) player.Play(currentHold);
            partnerHand.SetCurlAll(heldCurl);
            if (card != null) card.SetHint(stepHint);
        }

        private void LetGo()
        {
            attached = false;
            releaseCount++;
            log?.Trial("grip", -1, "", -1, -1, -1f, "off");
            player.Release(GloveChannels.WholeHand, letGoSpeed, "RELEASE_BY_PATIENT");
            partnerHand.SetCurlAll(0.15f);
            returning = StartCoroutine(ReturnToPad());
            if (!quietRelease && card != null) card.SetHint(RegripHint);
        }

        /// <summary>Wait until the patient holds the model hand (or the therapist skips).</summary>
        private IEnumerator WaitHeld()
        {
            while ((!attached || State != TaskState.Running) && !skipRequested) yield return null;
        }

        /// <summary>Wait that only counts while the patient holds the hand (and not paused).</summary>
        private IEnumerator HoldFor(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skipRequested)
            {
                if (State == TaskState.Running && attached) t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>After the patient lets go: the model hand glides back to the waiting pad, then beckons.</summary>
        private IEnumerator ReturnToPad()
        {
            StopBeckoning();
            yield return Glide(partnerHand, null, partnerOffer);
            returning = null;
            StartBeckoning();
        }

        // ------------------------------------------------------------------ following the real hand

        private void LateUpdate()
        {
            Beckon();
            if (guide != null) guide.Show(beckoning && returning == null && State != TaskState.Ended);
            if (!attached || patientHand == null || !patientHand.IsValid || State == TaskState.Paused) return;
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Pose p = PartnerTarget();
            partnerHand.transform.SetPositionAndRotation(Vector3.Lerp(partnerHand.transform.position, p.position, k),
                                                         Quaternion.Slerp(partnerHand.transform.rotation, p.rotation, k));
        }

        private Vector3 PartnerPalm =>
            partnerHand.Contact(5) != null ? partnerHand.Contact(5).position : partnerHand.transform.position;

        /// <summary>Point just off the patient's palm where the model palm should touch.</summary>
        private Vector3 PalmSurface()
        {
            return patientHand.Palm + patientHand.PalmNormal * palmGap + patientHand.FingerDir * palmForward;
        }

        /// <summary>Palm to palm, fingers pointing back along the patient's hand.</summary>
        private Pose PartnerTarget()
        {
            Quaternion rot = patientHand.Rotation * Quaternion.Euler(180f, 0f, 0f);
            Vector3 pos = PalmSurface() - rot * (partnerPalmLocal * partnerHand.transform.localScale.x);
            return new Pose(pos, rot);
        }

        // ------------------------------------------------------------------ waiting pose (handshake angle)

        /// <summary>
        /// Tilts PartnerOffer / PartnerAway (fingers down by offerPitchDegrees, turned by offerRollDegrees,
        /// raised by offerRaise) about the model palm, and moves the ghost hand, arrow and pad with it.
        /// The markers in the scene stay as built; this runs in Play only, again whenever a value changes.
        /// </summary>
        private void ApplyOfferPose()
        {
            if (partnerOffer == null || partnerHand == null) return;
            var values = new Vector3(offerPitchDegrees, offerRollDegrees, offerRaise);
            if (offerBaseSaved && values == appliedOffer) return;
            if (!offerBaseSaved)
            {
                offerBase = new Pose(partnerOffer.position, partnerOffer.rotation);
                if (partnerAway != null) awayBase = new Pose(partnerAway.position, partnerAway.rotation);
                if (waitingPad == null && transform.parent != null) waitingPad = transform.parent.Find("WaitingPad");
                if (waitingPad != null) padHome = waitingPad.position;
                offerBaseSaved = true;
            }
            appliedOffer = values;

            Pose offer = TiltOffer(offerBase);
            partnerOffer.SetPositionAndRotation(offer.position, offer.rotation);
            if (partnerAway != null)
            {
                Pose away = TiltOffer(awayBase);
                partnerAway.SetPositionAndRotation(away.position, away.rotation);
            }
            Vector3 palmShift = OfferPalm(offer) - OfferPalm(offerBase);
            if (guide != null) guide.FollowOffer(offerBase, offer, palmShift);
            if (waitingPad != null) waitingPad.position = padHome + new Vector3(palmShift.x, 0f, palmShift.z);

            // live tuning: a hand already waiting at the pad moves with the marker
            if (beckoning && returning == null && !attached)
            {
                Place(partnerHand, partnerOffer);
                bobApplied = Vector3.zero;
            }
        }

        private Vector3 OfferPalm(Pose p)
        {
            return p.position + p.rotation * (partnerPalmLocal * partnerHand.transform.lossyScale.x);
        }

        private Pose TiltOffer(Pose b)
        {
            Vector3 palmL = partnerPalmLocal * partnerHand.transform.lossyScale.x;
            Vector3 fingers = b.rotation * Vector3.forward;
            Vector3 flat = Vector3.ProjectOnPlane(fingers, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f) flat = fingers;
            // about the horizontal axis across the fingers: a positive angle points the fingers down
            Quaternion r = Quaternion.AngleAxis(offerPitchDegrees, Vector3.Cross(Vector3.up, flat.normalized)) * b.rotation;
            r = Quaternion.AngleAxis(offerRollDegrees, r * Vector3.forward) * r;
            Vector3 palm = b.position + b.rotation * palmL + Vector3.up * offerRaise;
            return new Pose(palm - r * palmL, r);
        }

        // ------------------------------------------------------------------ beckoning (model hand invites the patient)

        private void StartBeckoning()
        {
            if (beckoning) return;
            beckoning = true;
            beckonTime = 0f;
        }

        private void StopBeckoning()
        {
            if (!beckoning) return;
            beckoning = false;
            partnerHand.transform.position -= bobApplied;
            bobApplied = Vector3.zero;
        }

        /// <summary>
        /// Fingers open and close in a slow wave from index to little (thumb barely). The optional bob
        /// is an offset added on top of wherever the hand is, so it never fights the glide back.
        /// </summary>
        private void Beckon()
        {
            if (!beckoning || partnerHand == null || State != TaskState.Running) return;
            beckonTime += Time.deltaTime;
            float w = 2f * Mathf.PI / Mathf.Max(0.5f, beckonPeriod);
            for (int f = 0; f < 5; f++)
            {
                float k = 0.5f - 0.5f * Mathf.Cos(beckonTime * w - f * 0.45f);
                float range = f == 0 ? 0.3f : 1f;
                partnerHand.SetCurl((HandFinger)f, Mathf.Lerp(beckonOpenCurl, beckonClosedCurl, k * range));
            }
            Vector3 bob = Vector3.up * (beckonBob * Mathf.Sin(beckonTime * w));
            partnerHand.transform.position += bob - bobApplied;
            bobApplied = bob;
        }

        // ------------------------------------------------------------------ keyboard (desktop testing)

        private void HandleKeys()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.digit2Key.wasPressedThisFrame) Answer(2);
            if (k.digit3Key.wasPressedThisFrame) Answer(3);
            if (k.digit4Key.wasPressedThisFrame) Answer(4);
            if (k.rKey.wasPressedThisFrame) FeelAgain();
            if (k.mKey.wasPressedThisFrame) OneMore();
            if (k.fKey.wasPressedThisFrame) FinishTraining();
            if (k.sKey.wasPressedThisFrame) SkipStep();
#endif
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Glide to b, from a (or from where the hand is when a is null).</summary>
        private IEnumerator Glide(StylizedHand h, Transform a, Transform b)
        {
            if (h == null || b == null) yield break;
            Vector3 p0 = a != null ? a.position : h.transform.position;
            Quaternion r0 = a != null ? a.rotation : h.transform.rotation;
            float t = 0f, dur = Mathf.Max(0.1f, glideSeconds);
            while (t < dur)
            {
                if (State == TaskState.Running) t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                h.transform.SetPositionAndRotation(Vector3.Lerp(p0, b.position, k), Quaternion.Slerp(r0, b.rotation, k));
                yield return null;
            }
        }

        private static void Place(StylizedHand h, Transform pose)
        {
            if (h != null && pose != null) h.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        protected override void FillSummary(LearningSessionSummary s)
        {
            s.quiz_trials = quizTrials;
            s.quiz_correct = quizCorrect;
            s.event_counts.Add("PATIENT_LET_GO=" + releaseCount);
            s.event_counts.Add("TIP_SHOWN=" + tipCount);
        }

        protected override void OnFinished()
        {
            bool completed = EndReason == "completed";
            ResetScene();
            if (completed)
            {
                if (SoundOn && synth != null)
                {
                    synth.Play(0);
                    StartCoroutine(PlayLater(2, 0.15f));
                    StartCoroutine(PlayLater(4, 0.3f));
                }
            }
            else ShowCard(ChipName, "Session ended", "Press Start to begin again", 0, CardTone.Normal);
        }
    }
}
