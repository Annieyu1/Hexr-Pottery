using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    public enum PianoLevel { Listen = 1, Learn = 2, Test = 3 }
    public enum TouchCueKind { Vibrate, Press }
    public enum TestTiming { Wait, Tempo }

    /// <summary>What the menu's result screen shows after a run (one song).</summary>
    public class PianoRunResult
    {
        public PianoLevel level;
        public string song = "";
        public int notes;
        public int correct;            // right first time (L2, L3)
        public float accuracy = -1f;   // 0..1, -1 = not scored (L1)
        public float seconds;          // first note to the end of the song
        public bool completed;         // false = ended early (End button, scene closed)
    }

    /// <summary>
    /// T2 piano (docs/12), three levels in one scene. The keyboard lines up with the real table under
    /// the patient's own tracked hand (calibration step).
    ///   L1 Listen: keys go down by themselves and play a song; each note presses the matching finger
    ///      (passive haptic learning, docs/10 §5.4). Speed adjustable. Nothing to do.
    ///   L2 Learn: for each note the key and fingertip glow and the finger vibrates, together, until
    ///      the patient presses the right finger. A wrong press shows "Try again" and the cues stay on;
    ///      a note is never skipped by itself (Skip button). Wait mode.
    ///   L3 Test: the song once with only the cues the therapist left on; the first press is the
    ///      answer, rated Perfect / Very good / Nice / Miss from accuracy and reaction time. The end
    ///      card shows Congratulations, accuracy and play time.
    /// Cues (2026-10-21): visual (key + fingertip glow) and touch (vibration, strength adjustable),
    /// each can be switched off. Sound is no longer a cue: it only switches the piano's own sound.
    /// Right hand: C=thumb … G=little; left hand mirrored (C=little), same keyboard.
    /// </summary>
    public class PianoTask : LearningTask
    {
        private enum Cue { Touch, Visual }

        [Header("T2 objects")]
        [SerializeField] private PianoKeyboard keyboard;
        [SerializeField] private ToneSynth synth;
        [SerializeField] private TrackedHandFrame patientHand;
        [SerializeField] private FingerPressDetector detector;
        [SerializeField] private PianoFingerGlow fingerGlow;
        [SerializeField] private StepCard card;
        [Tooltip("L3: Perfect / Very good / Nice / Miss over the keyboard.")]
        [SerializeField] private TMP_Text ratingText;

        [Header("T2 therapist panel")]
        [SerializeField] private TMP_Text settingsText;
        [SerializeField] private TMP_Text[] levelLabels = new TMP_Text[3];
        [SerializeField] private TMP_Text soundLabel;
        [SerializeField] private TMP_Text visualLabel;
        [SerializeField] private TMP_Text touchLabel;

        [Header("T2 level and cues")]
        [SerializeField] private PianoLevel level = PianoLevel.Learn;
        [Tooltip("The piano's own sound (L1 song, L2/L3 the key the patient presses). Not a cue.")]
        [SerializeField] private bool pianoSound = true;
        [SerializeField] private bool cueVisual = true;
        [SerializeField] private bool cueTouch = true;
        [Tooltip("Glove strength for the L1 note press and the L2/L3 touch cue (HexR 0.1–1).")]
        [Range(0.1f, 1f)] [SerializeField] private float touchStrength = 0.5f;
        [SerializeField] private TouchCueKind touchCueKind = TouchCueKind.Vibrate;
        [SerializeField] private float vibrationHz = 25f;
        [Tooltip("Length of one touch cue (s).")]
        [SerializeField] private float cueSeconds = 0.4f;
        [Tooltip("Short press on the finger whenever the patient presses any key (the key's feel; not right/wrong).")]
        [SerializeField] private bool keyTouch;
        [Tooltip("Song speed for L1 and L3 tempo (1 = written tempo).")]
        [Range(0.5f, 1.5f)] [SerializeField] private float tempo = 1f;
        [Tooltip("Never faster than this between note onsets (s): pneumatic rise + 100 ms Bluetooth batching.")]
        [SerializeField] private float minNoteInterval = 0.6f;
        [SerializeField] private float listenPressSeconds = 0.3f;

        [Header("T2 learn (L2)")]
        [Tooltip("The touch cue repeats this often while the patient has not pressed the right key (s).")]
        [SerializeField] private float repeatCueSeconds = 3f;
        [Tooltip("How long \"Try again\" stays on the card after a wrong press (s).")]
        [SerializeField] private float tryAgainSeconds = 2f;
        [SerializeField] private float gapAfterNote = 0.4f;

        [Header("T2 test (L3)")]
        [SerializeField] private TestTiming testTiming = TestTiming.Wait;
        [SerializeField] private int testPasses = 1;
        [Tooltip("Wait mode: no press within this long = Miss (s).")]
        [SerializeField] private float missAfterSeconds = 10f;
        [Tooltip("Wait mode: correct and reaction time under this = Perfect (s). Placeholder, tune with patients.")]
        [SerializeField] private float perfectSeconds = 1.0f;
        [Tooltip("Wait mode: correct and reaction time under this = Very good (s); slower = Nice.")]
        [SerializeField] private float veryGoodSeconds = 2.0f;
        [Tooltip("Tempo mode: correct and |timing error| under this = Perfect (s).")]
        [SerializeField] private float perfectTiming = 0.15f;
        [Tooltip("Tempo mode: correct and |timing error| under this = Very good (s); later in the window = Nice.")]
        [SerializeField] private float veryGoodTiming = 0.30f;
        [SerializeField] private float ratingSeconds = 0.9f;

        [Header("T2 songs")]
        [SerializeField] private List<PianoSong> songs = new List<PianoSong>(PianoSong.Defaults());
        [SerializeField] private int songIndex = 1;

        [Header("T2 calibration and data")]
        [Tooltip("Hand still on the table this long to take the rest pose (s).")]
        [SerializeField] private float restSeconds = 3f;
        [SerializeField] private float fingerSeconds = 5f;
        [Tooltip("L1: the hand only needs to settle this long (s) — Listen asks nothing of the hand, so placing it is easy.")]
        [SerializeField] private float listenRestSeconds = 1f;
        [Tooltip("L1: the palm may drift this much while settling (m); L2/L3 use 1.5 cm.")]
        [SerializeField] private float listenStillMeters = 0.04f;
        [Tooltip("L1: start anyway after this long (s), with the hand where it is (or the keyboard where it was).")]
        [SerializeField] private float listenGiveUpSeconds = 6f;
        [Tooltip("Key tops sit this far below the tracked fingertip joints (finger pad, m).")]
        [SerializeField] private float padOffset = 0.008f;
        [SerializeField] private bool recordFlex = true;
        [SerializeField] private float flexHz = 30f;

        [Header("T2 hand on the keys")]
        [Tooltip("Ghost of the patient's hand where it should rest on the keys (shown while placing it, and when it wanders off).")]
        [SerializeField] private StylizedHand ghostHand;
        [Tooltip("While a song plays: pause when the hand leaves its place on the keys, go on when it is back.")]
        [SerializeField] private bool pauseWhenHandLeaves = true;
        [Tooltip("Palm further than this from its place = the hand has left (m).")]
        [SerializeField] private float leaveDistance = 0.08f;
        [Tooltip("...for this long (s). A lost hand (not tracked) counts as left after lostSeconds.")]
        [SerializeField] private float leaveSeconds = 0.6f;
        [SerializeField] private float lostSeconds = 1f;
        [Tooltip("Palm within this of its place = back (m), for returnSeconds.")]
        [SerializeField] private float returnDistance = 0.05f;
        [SerializeField] private float returnSeconds = 0.5f;

        [Header("T2 menu")]
        [Tooltip("Started from the menu: L1 / L2 play the chosen song once, then the menu shows the result. Off = rounds until the time limit.")]
        [SerializeField] private bool oneSongPerRun = true;
        [Tooltip("Fade to warm black that hides the rig shift when the real table is lined up (optional).")]
        [SerializeField] private ScreenFade fade;

        [Header("T2 desk alignment")]
        [Tooltip("After the rest pose, move the VR rig up/down so the real table lines up with the virtual desk.")]
        [SerializeField] private bool alignToDesk = true;
        [Tooltip("OVRCameraRig (moving it moves the whole tracking space, hands included).")]
        [SerializeField] private Transform rig;
        [Tooltip("World height of the virtual desk top (set by the scene builder).")]
        [SerializeField] private float deskTop = 0.771f;
        [Tooltip("Keyboard height below the key tops (the model's case bottom is 12 mm down).")]
        [SerializeField] private float keyboardDepth = 0.012f;
        [Tooltip("Larger corrections are ignored (tracking error, hand not on the table) (m).")]
        [SerializeField] private float maxDeskShift = 0.3f;

        private float clock;                 // stands still while paused
        private bool started;
        private bool recalibrate;
        private PianoLog pianoLog;
        private PianoNoteTrial waiting;      // L2 / L3 wait: the note being asked
        private readonly List<PianoNoteTrial> tempoNotes = new List<PianoNoteTrial>();
        private readonly List<PianoNoteTrial> pending = new List<PianoNoteTrial>();
        private readonly List<PianoNoteTrial> testTrials = new List<PianoNoteTrial>();
        private PianoSong current;
        private int noteIndex;
        private float nextFlex;
        private bool skipRequested;
        private float testStart = -1f;
        private float ratingUntil;
        private float tryAgainUntil = -1f;
        private int currentSeg;
        private float deskShift;             // total rig shift so far (m)
        private float runStart = -1f;        // clock at the first note of this run
        private int runNotes, runCorrect;
        // where the hand rests, in keyboard space (recorded at calibration; a default until then)
        private bool hasHandPlace;
        private Vector3 placePalm, placeIndex, placeMiddle, placeLittle;
        private bool inSong, autoPaused;
        private float awayTimer, backTimer;
        private MaterialPropertyBlock ghostBlock;
        private Renderer[] ghostRenderers;
        private Color ghostColor = new Color(0.25f, 0.76f, 0.79f, 0.35f);
        private float testPlaySeconds;

        /// <summary>Raised when a run ends (song done, or ended early). The menu shows the result.</summary>
        public event System.Action<PianoRunResult> RunEnded;
        public PianoRunResult LastResult { get; private set; }

        // settings the menu reads and changes
        public PianoLevel Level => level;
        public int SongIndex => songIndex;
        public IReadOnlyList<PianoSong> Songs => songs;
        public bool CueVisual => cueVisual;
        public bool CueTouch => cueTouch;
        public bool PianoSound => pianoSound;
        public float TouchStrength => touchStrength;
        public float Tempo => tempo;
        public PianoKeyboard PianoKeys => keyboard;
        public ToneSynth Synth => synth;

        public void SelectSong(int index)
        {
            if (songs.Count == 0 || State == TaskState.Running || State == TaskState.Paused) return;
            songIndex = Mathf.Clamp(index, 0, songs.Count - 1);
            ShowLabels(SelectedSong);
        }
        private readonly bool[] autoDown = new bool[5];
        private string panelCache;

        protected override string TaskName => "T2_Piano";
        protected override bool HideTrackedHandsInWatch => false;
        protected override string BodyEndReason => level == PianoLevel.Test || oneSongPerRun ? "completed" : "time_up";

        private PianoSong SelectedSong => songs.Count == 0 ? null : songs[Mathf.Clamp(songIndex, 0, songs.Count - 1)];

        // ------------------------------------------------------------------ lifecycle

        protected override void Start()
        {
            base.Start();
            started = true;
            if (detector != null)
            {
                detector.Clock = () => clock;
                detector.Pressed += OnFingerPressed;
                detector.Released += OnFingerReleased;
            }
            ShowLabels(SelectedSong);
            card?.Show("T2 Piano", "Press Start", LevelName(level), 0, CardTone.Normal);
            if (oneSongPerRun) card?.gameObject.SetActive(false);   // the menu is showing instead
            RefreshPanel();
        }

        protected override void Update()
        {
            base.Update();
            if (State == TaskState.Running) clock += Time.deltaTime;
            HandleT2Keys();
            UpdateKeysFromHand();
            FlushTrials(false);
            if (recordFlex && State == TaskState.Running && pianoLog != null && detector != null && clock >= nextFlex)
            {
                nextFlex = clock + 1f / Mathf.Max(1f, flexHz);
                pianoLog.Flex(clock, LevelTag, noteIndex, detector.Tracked || detector.Simulating, detector);
            }
            UpdateRatingPopup();
            WatchHand();
            PulseGhost();
            RefreshPanel();
        }

        protected override IEnumerator Intro()
        {
            pianoLog?.Dispose();
            pianoLog = log != null ? new PianoLog(log.Folder, log.SessionId) : null;
            testTrials.Clear();
            pending.Clear();
            HideRating();
            runStart = -1f; runNotes = 0; runCorrect = 0; testPlaySeconds = 0f;
            card?.gameObject.SetActive(true);
            clock = 0f;
            nextFlex = 0f;
            if (log != null) log.Mode = LevelTag;
            Step("settings:" + LevelTag + " cues=" + CueTag + " strength=" + touchStrength.ToString("0.0") +
                 " tempo=" + tempo.ToString("0.00"));
            if (detector != null && (!detector.Calibrated || recalibrate)) yield return Calibrate();
        }

        protected override IEnumerator Body()
        {
            if (level == PianoLevel.Test) return TestBody();
            return oneSongPerRun ? OneSong() : base.Body();
        }

        private IEnumerator OneSong()
        {
            round = 1;
            if (log != null) log.Round = round;
            yield return RunRound(1);
        }

        private IEnumerator TestBody()
        {
            for (int p = 0; p < Mathf.Max(1, testPasses); p++)
            {
                round++;
                if (log != null) log.Round = round;
                yield return TestSong(SelectedSong);
            }
        }

        protected override IEnumerator RunRound(int r)
        {
            if (songs.Count == 0) { yield return Wait(1f); yield break; }
            // L1 goes through the songs from the selected one; L2 practises the selected song.
            current = level == PianoLevel.Listen ? songs[(songIndex + r - 1) % songs.Count] : SelectedSong;
            ShowLabels(current);
            if (level == PianoLevel.Listen) yield return ListenSong(current);
            else yield return LearnSong(current);
        }

        // ------------------------------------------------------------------ calibration

        private IEnumerator Calibrate()
        {
            recalibrate = false;
            Step("calibrate");
            if (patientHand != null && patientHand.UsingStandIn)
            {
                // Desktop test: keys 1–5 stand in for thumb..little.
                detector.SetSimulation(true);
                PlaceKeyboardOnHand();
                WriteCalibration(true);
                card?.Show("Get ready", "Desktop test", "Keys 1-5 press thumb to little", 5, CardTone.Normal);
                yield return Wait(2f);
                yield break;
            }
            detector.SetSimulation(false);

            // 1) rest pose: hand on the table, still
            bool listen = level == PianoLevel.Listen;
            float needStill = listen ? listenRestSeconds : restSeconds;
            float tolerance = listen ? listenStillMeters : 0.015f;
            card?.Show("Get ready", "Rest your hand on the keys", "Relax, keep still", 0, CardTone.Normal);
            ShowGhost(true);
            var sum = new Vector3[5];
            var tipSum = new Vector3[5];
            Vector3 dirSum = Vector3.zero;
            Vector3 palmSum = Vector3.zero, idxSum = Vector3.zero, midSum = Vector3.zero, litSum = Vector3.zero;
            int samples = 0;
            float still = 0f, waited = 0f;
            Vector3 anchorPalm = Vector3.zero;
            bool hinted = false;
            while (still < needStill)
            {
                yield return null;
                if (State != TaskState.Running) continue;
                waited += Time.deltaTime;
                // L1 never gets stuck here: after a few seconds it starts with whatever it has
                if (listen && waited > listenGiveUpSeconds) break;
                if (!hinted && waited > 10f)
                {
                    hinted = true;
                    card?.SetHint("Put your hand flat, fingertips on the table");
                }
                if (!detector.Tracked) { still = 0f; samples = 0; continue; }
                Vector3 palm = patientHand.Palm;
                if (samples == 0 || (palm - anchorPalm).magnitude > tolerance)
                {
                    anchorPalm = palm;
                    still = 0f;
                    samples = 0;
                    for (int f = 0; f < 5; f++) { sum[f] = Vector3.zero; tipSum[f] = Vector3.zero; }
                    dirSum = Vector3.zero;
                    palmSum = idxSum = midSum = litSum = Vector3.zero;
                }
                still += Time.deltaTime;
                samples++;
                for (int f = 0; f < 5; f++) { sum[f] += detector.Pose(f); tipSum[f] += patientHand.Tip(f); }
                dirSum += patientHand.FingerDir;
                palmSum += patientHand.Palm;
                idxSum += patientHand.Knuckle(1);
                midSum += patientHand.Knuckle(2);
                litSum += patientHand.Knuckle(4);
            }
            if (samples == 0)
            {
                // L1 gave up without ever seeing the hand: keep the keyboard where it is and play
                Step("listen_hand_not_found");
                ShowGhost(false);
                card?.SetTone(CardTone.Done);
                yield return KeyWave(0.07f);
                yield break;
            }
            var restPose = new Vector3[5];
            var tipsByKey = new Vector3[5];
            for (int f = 0; f < 5; f++) restPose[f] = sum[f] / samples;
            for (int k = 0; k < 5; k++) tipsByKey[k] = tipSum[FingerFor(k)] / samples;
            // the rig shift is hidden behind a short fade to warm black
            bool fading = fade != null && alignToDesk && rig != null;
            if (fading) yield return fade.FadeOut(0.25f);
            float shiftBefore = deskShift;
            AlignRigToDesk(tipsByKey);
            detector.SetRest(restPose);
            keyboard?.Place(tipsByKey, padOffset, KeyFor(0), dirSum);
            RecordHandPlace(palmSum / samples, idxSum / samples, midSum / samples, litSum / samples, deskShift - shiftBefore);
            ShowGhost(false);
            if (fading) yield return fade.FadeIn(0.35f);
            card?.SetTone(CardTone.Done);
            yield return KeyWave(0.07f);
            yield return Wait(0.4f);
            if (level == PianoLevel.Listen) yield break;   // L1 only needs the keys under the hand

            // 2) each finger presses once, as far as is comfortable
            for (int f = 0; f < 5; f++)
            {
                int key = KeyFor(f);
                Step("calibrate_" + ((HandFinger)f));
                card?.Show("Get ready", "Press with your " + FingerWord(f), "As far as is comfortable, then relax", f + 1, CardTone.Normal);
                keyboard?.SetLit(key, true);
                fingerGlow?.Set(f, true);
                TouchCue(ChannelFor(f), "CAL_" + (HandFinger)f);
                Vector3 best = restPose[f];
                float bestDist = 0f, t = 0f;
                while (t < fingerSeconds)
                {
                    yield return null;
                    if (State != TaskState.Running) continue;
                    t += Time.deltaTime;
                    if (!detector.Tracked) continue;
                    Vector3 pose = detector.Pose(f);
                    float d = (pose - restPose[f]).magnitude;
                    if (d > bestDist) { bestDist = d; best = pose; }
                    if (t > 1f && bestDist > 0.015f && d < 0.4f * bestDist) break;   // pressed and relaxed again
                }
                detector.SetPress(f, best);
                keyboard?.SetLit(key, false);
                fingerGlow?.Set(f, false);
                if (log != null) log.Trial("calibrate_" + (HandFinger)f, -1, "", -1, -1, -1f,
                                           "range_mm=" + (bestDist * 1000f).ToString("0.0") + (detector.Usable(f) ? "" : " not_usable"));
                yield return Wait(0.5f);
            }
            WriteCalibration(false);
            card?.Show("Get ready", "Ready", "", 5, CardTone.Done);
            yield return Wait(1f);
        }

        /// <summary>
        /// The patient's fingers rest on the real table; shift the tracking space so that table sits
        /// where the keyboard rests on the virtual desk (key tops = desk top + keyboardDepth).
        /// tipsByKey is moved by the same amount.
        /// </summary>
        private void AlignRigToDesk(Vector3[] tipsByKey)
        {
            if (!alignToDesk || rig == null) return;
            float y = 0f;
            for (int k = 0; k < 5; k++) y += tipsByKey[k].y;
            float keyTop = y / 5f - padOffset;
            float shift = deskTop + keyboardDepth - keyTop;
            if (Mathf.Abs(shift) > maxDeskShift)
            {
                Debug.LogWarning("[Learning] T2 desk alignment skipped: shift " + shift.ToString("0.000") + " m is too large.");
                return;
            }
            rig.position += Vector3.up * shift;
            for (int k = 0; k < 5; k++) tipsByKey[k] += Vector3.up * shift;
            deskShift += shift;
            Step("desk_shift_mm=" + (shift * 1000f).ToString("0"));
        }

        /// <summary>Desktop / fallback: keys under the current (stand-in) fingertips.</summary>
        private void PlaceKeyboardOnHand()
        {
            if (keyboard == null || patientHand == null || !patientHand.IsValid) return;
            var tips = new Vector3[5];
            for (int k = 0; k < 5; k++) tips[k] = patientHand.Tip(FingerFor(k));
            keyboard.Place(tips, padOffset, KeyFor(0), patientHand.FingerDir);
        }

        private void WriteCalibration(bool simulated)
        {
            if (pianoLog == null || detector == null) return;
            var c = new PianoCalibration { hand = hand.ToString(), simulated = simulated };
            for (int f = 0; f < 5; f++)
            {
                c.range_mm[f] = detector.RangeMeters(f) * 1000f;
                c.usable[f] = detector.Usable(f);
                c.rest_pose_mm[f] = PianoLog.Mm(detector.RestPose(f));
                c.press_pose_mm[f] = PianoLog.Mm(detector.PressPose(f));
            }
            if (keyboard != null)
            {
                c.key_spacing_mm = keyboard.Spacing * 1000f;
                c.keyboard_position = PianoLog.Mm(keyboard.transform.position);
            }
            c.desk_shift_mm = deskShift * 1000f;
            pianoLog.WriteJson("calibration.json", c);
        }

        // ------------------------------------------------------------------ L1 listen

        private IEnumerator ListenSong(PianoSong song)
        {
            Step("listen:" + song.title);
            card?.Show(LevelName(PianoLevel.Listen), song.title, "Relax your hand and feel each note", 0, CardTone.Normal);
            yield return Wait(2f);
            inSong = true;
            List<PianoSong.Note> notes = song.Parse();
            for (int i = 0; i < notes.Count; i++)
            {
                noteIndex = i + 1;
                PianoSong.Note note = notes[i];
                float duration = NoteSeconds(song, note);
                SetProgress(i + 1, notes.Count);
                if (note.keys.Length == 0) { yield return Wait(duration); continue; }

                Step(song.title + "#" + noteIndex);
                if (runStart < 0f) runStart = clock;
                runNotes++;
                float pressFor = Mathf.Min(listenPressSeconds, duration * 0.6f);
                GloveChannels ch = GloveChannels.None;
                foreach (int k in note.keys)
                {
                    ch |= ChannelFor(FingerFor(k));
                    autoDown[k] = true;
                    if (cueVisual) { keyboard?.SetLit(k, true); fingerGlow?.Set(FingerFor(k), true); }
                    if (pianoSound) synth?.Play(k);
                }
                if (cueTouch) player.Tap(ch, touchStrength, pressFor, "TAP_" + KeyNames(note.keys));
                yield return Wait(pressFor);
                foreach (int k in note.keys)
                {
                    autoDown[k] = false;
                    keyboard?.SetLit(k, false);
                    fingerGlow?.Set(FingerFor(k), false);
                }
                yield return Wait(duration - pressFor);
            }
            inSong = false;
            card?.SetTone(CardTone.Done, "Done");
            yield return Wait(2.5f);
        }

        // ------------------------------------------------------------------ L2 learn

        private IEnumerator LearnSong(PianoSong song)
        {
            Step("learn:" + song.title);
            card?.Show(LevelName(PianoLevel.Learn), song.title, LearnHint(), 0, CardTone.Normal);
            yield return Wait(2f);
            inSong = true;
            List<PianoSong.Note> notes = song.Parse();
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].keys.Length == 0) continue;   // wait mode: rests are skipped
                noteIndex = i + 1;
                SetProgress(i + 1, notes.Count);
                Step(song.title + "#" + noteIndex);
                yield return LearnNote(notes[i]);
                yield return Wait(gapAfterNote);
            }
            inSong = false;
            card?.SetTone(CardTone.Done, "Done");
            yield return Wait(2f);
        }

        /// <summary>
        /// Glow + vibration together until the right key is pressed. A wrong press shows "Try again"
        /// and repeats the vibration; the note only ends on the right key or the Skip button.
        /// </summary>
        private IEnumerator LearnNote(PianoSong.Note note)
        {
            var n = new PianoNoteTrial { index = noteIndex, targets = note.keys, onset = clock, cues = CueTag };
            waiting = n;
            skipRequested = false;
            tryAgainUntil = -1f;
            if (runStart < 0f) runStart = clock;
            foreach (Cue c in CueOrder()) FireCue(c, note.keys);
            float lastTouch = clock;
            bool visualOn = cueVisual;

            while (!n.answered)
            {
                if (skipRequested) { n.omitted = true; n.skipped = true; break; }
                // cues switched on / off by the therapist while waiting
                if (cueVisual && !visualOn) FireCue(Cue.Visual, note.keys);
                visualOn = cueVisual;
                if (cueTouch && clock - lastTouch > repeatCueSeconds)
                {
                    lastTouch = clock;
                    FireCue(Cue.Touch, note.keys);
                }
                if (tryAgainUntil >= 0f && clock > tryAgainUntil)
                {
                    tryAgainUntil = -1f;
                    card?.Show(LevelName(PianoLevel.Learn), current != null ? current.title : "", LearnHint(), currentSeg, CardTone.Normal);
                }
                yield return null;
            }
            waiting = null;
            skipRequested = false;
            ClearCues(note.keys);
            Close(n);
            runNotes++;
            if (n.correct) runCorrect++;
        }

        /// <summary>L2 wrong key: say so, keep the glow, feel the right finger again.</summary>
        private void TryAgain(PianoNoteTrial n)
        {
            tryAgainUntil = clock + tryAgainSeconds;
            card?.Show(LevelName(PianoLevel.Learn), "Try again", LearnHint(), currentSeg, CardTone.Answer);
            if (cueTouch) FireCue(Cue.Touch, n.targets);
        }

        private string LearnHint()
        {
            if (cueTouch && cueVisual) return "Feel the finger, press the glowing key";
            if (cueTouch) return "Feel the finger, then press it";
            if (cueVisual) return "Press the glowing key";
            return "Play the next note";
        }

        // ------------------------------------------------------------------ L3 test

        private IEnumerator TestSong(PianoSong song)
        {
            if (song == null) yield break;
            current = song;
            ShowLabels(song);
            Step("test:" + song.title + " cues=" + CueTag + " " + testTiming);
            card?.Show(LevelName(PianoLevel.Test), song.title, "Play the song", 0, CardTone.Normal);
            yield return Wait(2f);
            inSong = true;
            List<PianoSong.Note> notes = song.Parse();

            if (testTiming == TestTiming.Wait)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    if (notes[i].keys.Length == 0) continue;
                    noteIndex = i + 1;
                    SetProgress(i + 1, notes.Count);
                    Step(song.title + "#" + noteIndex);
                    var n = new PianoNoteTrial
                    {
                        index = noteIndex, targets = notes[i].keys, onset = clock, cues = CueTag, firstPressDecides = true,
                    };
                    waiting = n;
                    skipRequested = false;
                    if (testStart < 0f) testStart = clock;
                    foreach (Cue c in CueOrder()) FireCue(c, n.targets);
                    while (!n.answered && !skipRequested && clock - n.onset < missAfterSeconds) yield return null;
                    if (!n.answered) { n.omitted = true; n.skipped = skipRequested; }
                    skipRequested = false;
                    waiting = null;
                    ClearCues(n.targets);
                    Rate(n);
                    Close(n);
                    testTrials.Add(n);
                    yield return Wait(Mathf.Max(gapAfterNote, ratingSeconds * 0.6f));
                }
            }
            else
            {
                // count-in, then the notes come on the beat; a press inside a note's window answers it
                for (int c = 3; c >= 1; c--)
                {
                    card?.Show(LevelName(PianoLevel.Test), c.ToString(), song.title, 0, CardTone.Normal);
                    yield return Wait(60f / Mathf.Max(10f, song.beatsPerMinute) / tempo);
                }
                card?.Show(LevelName(PianoLevel.Test), song.title, "Play the song", 0, CardTone.Normal);
                tempoNotes.Clear();
                testStart = clock;
                float prevHalf = 0.3f;
                for (int i = 0; i < notes.Count; i++)
                {
                    float duration = NoteSeconds(song, notes[i]);
                    if (notes[i].keys.Length > 0)
                    {
                        noteIndex = i + 1;
                        SetProgress(i + 1, notes.Count);
                        float half = Mathf.Max(0.3f, duration * 0.5f);
                        var n = new PianoNoteTrial
                        {
                            index = noteIndex, targets = notes[i].keys, onset = clock, cues = CueTag, firstPressDecides = true,
                            windowStart = clock - prevHalf, windowEnd = clock + half,
                        };
                        prevHalf = half;
                        tempoNotes.Add(n);
                        testTrials.Add(n);
                        foreach (Cue c in CueOrder()) FireCue(c, n.targets);
                        float lit = Mathf.Min(duration * 0.8f, 0.6f);
                        yield return Wait(lit);
                        ClearCues(n.targets);
                        yield return Wait(duration - lit);
                    }
                    else yield return Wait(duration);
                    CloseExpiredTempoNotes(false);
                }
                yield return Wait(prevHalf);
                CloseExpiredTempoNotes(true);
            }
            inSong = false;
            float playSeconds = testStart >= 0f ? clock - testStart : 0f;
            testStart = -1f;
            testPlaySeconds = playSeconds;
            WriteTestSummary(song, playSeconds);
            yield return Wait(ratingSeconds);
            HideRating();
            // the menu's result screen shows Congratulations, accuracy and time
            if (!oneSongPerRun)
            {
                int seconds = Mathf.RoundToInt(playSeconds);
                int acc = 0, n = 0;
                foreach (PianoNoteTrial t in testTrials) { n++; if (t.correct) acc++; }
                card?.Show(LevelName(PianoLevel.Test), "Congratulations!",
                           "Accuracy " + (n > 0 ? Mathf.RoundToInt(100f * acc / n) : 0) + "%  ·  Time " + seconds / 60 + ":" + (seconds % 60).ToString("00"),
                           5, CardTone.Done);
                yield return Wait(4f);
            }
        }

        private void CloseExpiredTempoNotes(bool all)
        {
            for (int i = tempoNotes.Count - 1; i >= 0; i--)
            {
                PianoNoteTrial n = tempoNotes[i];
                if (n.answered || all || clock > n.windowEnd)
                {
                    if (!n.answered) n.omitted = true;
                    if (n.rating.Length == 0) Rate(n);
                    Close(n);
                    tempoNotes.RemoveAt(i);
                }
            }
        }

        /// <summary>L3 data for test_summary.json. The patient sees accuracy and time on the end card.</summary>
        private PianoTestSummary WriteTestSummary(PianoSong song, float playSeconds)
        {
            var s = new PianoTestSummary
            {
                session_id = log != null ? log.SessionId : "", song = song.title, cues = CueTag,
                timing = testTiming.ToString(), tempo = tempo, play_seconds = playSeconds,
            };
            float rtSum = 0f, errSum = 0f;
            int rtN = 0, errN = 0;
            foreach (PianoNoteTrial n in testTrials)
            {
                s.notes++;
                if (n.correct) s.correct++;
                if (n.omitted) s.omitted++;
                switch (n.rating)
                {
                    case "Perfect": s.perfect++; break;
                    case "Very good": s.very_good++; break;
                    case "Nice": s.nice++; break;
                    default: s.miss++; break;
                }
                int tf = FingerFor(n.targets[0]);
                s.per_finger_trials[tf]++;
                if (n.correct) s.per_finger_correct[tf]++;
                if (n.firstKey >= 0)
                {
                    s.confusion[tf * 5 + FingerFor(n.firstKey)]++;
                    if (testTiming == TestTiming.Wait && n.correct) { rtSum += n.firstTime - n.onset; rtN++; }
                    if (testTiming == TestTiming.Tempo) { errSum += Mathf.Abs(n.firstTime - n.onset); errN++; }
                }
            }
            s.accuracy = s.notes > 0 ? s.correct / (float)s.notes : 0f;
            s.mean_reaction_s = rtN > 0 ? rtSum / rtN : -1f;
            s.mean_abs_timing_error_s = errN > 0 ? errSum / errN : -1f;
            pianoLog?.WriteJson("test_summary.json", s);
            Debug.Log("[Learning] T2 test: " + s.correct + "/" + s.notes + " correct, " + playSeconds.ToString("0.0") + " s, " +
                      "perfect " + s.perfect + ", very good " + s.very_good + ", nice " + s.nice + ", miss " + s.miss);
            return s;
        }

        // ------------------------------------------------------------------ L3 rating

        /// <summary>Perfect / Very good / Nice for a right first press (by reaction time or timing), else Miss.</summary>
        private void Rate(PianoNoteTrial n)
        {
            if (n.rating.Length > 0) return;
            if (!n.correct || n.firstKey < 0) n.rating = "Miss";
            else
            {
                bool tempoMode = !float.IsInfinity(n.windowStart);
                float t = tempoMode ? Mathf.Abs(n.firstTime - n.onset) : n.firstTime - n.onset;
                float perfect = tempoMode ? perfectTiming : perfectSeconds;
                float good = tempoMode ? veryGoodTiming : veryGoodSeconds;
                n.rating = t <= perfect ? "Perfect" : t <= good ? "Very good" : "Nice";
            }
            ShowRating(n.rating);
        }

        private void ShowRating(string rating)
        {
            if (ratingText == null) return;
            ratingText.text = rating + (rating == "Perfect" ? "!" : "");
            switch (rating)
            {
                case "Perfect": ratingText.color = StepCard.Teal; break;
                case "Very good": ratingText.color = StepCard.Green; break;
                case "Nice": ratingText.color = StepCard.Yellow; break;
                default: ratingText.color = StepCard.HintGrey; break;
            }
            ratingText.gameObject.SetActive(true);
            ratingText.transform.localScale = Vector3.one * 0.6f;
            ratingUntil = Time.time + ratingSeconds;
        }

        private void UpdateRatingPopup()
        {
            if (ratingText == null || !ratingText.gameObject.activeSelf) return;
            if (Time.time > ratingUntil) { HideRating(); return; }
            // quick pop: grows to full size in 0.15 s
            float k = Mathf.Clamp01(1f - (ratingUntil - Time.time - (ratingSeconds - 0.15f)) / 0.15f);
            ratingText.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k);
        }

        private void HideRating()
        {
            if (ratingText != null) ratingText.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ presses

        private void OnFingerPressed(int finger, PressInfo info)
        {
            if (State != TaskState.Running || level == PianoLevel.Listen) return;
            int key = KeyFor(finger);
            if (pianoSound) synth?.Play(key);
            if (keyTouch) player.Tap(ChannelFor(finger), touchStrength, 0.15f, "KEY_" + (HandFinger)finger);

            if (waiting != null)
            {
                PianoNoteTrial w = waiting;
                w.Register(key, info, clock, -1);
                if (level == PianoLevel.Learn && System.Array.IndexOf(w.targets, key) < 0) TryAgain(w);
                return;
            }
            if (level == PianoLevel.Test && testTiming == TestTiming.Tempo)
            {
                foreach (PianoNoteTrial n in tempoNotes)
                    if (!n.answered && clock >= n.windowStart && clock <= n.windowEnd)
                    {
                        if (n.Register(key, info, clock, -1)) Rate(n);
                        return;
                    }
            }
            // a press between notes is not a trial; it stays visible in flex.csv
        }

        private void OnFingerReleased(int finger, PressInfo info) { }

        /// <summary>Keys follow the patient's fingers (L2/L3) or the song (L1).</summary>
        private void UpdateKeysFromHand()
        {
            if (keyboard == null) return;
            for (int k = 0; k < 5; k++)
            {
                bool d = autoDown[k] || (level != PianoLevel.Listen && State == TaskState.Running &&
                                         detector != null && detector.IsDown(FingerFor(k)));
                keyboard.SetPressed(k, d);
            }
            // desktop stand-in hand shows the simulated presses
            if (detector != null && detector.Simulating && patientHand != null && patientHand.StandIn != null)
                for (int f = 0; f < 5; f++)
                    patientHand.StandIn.SetCurl((HandFinger)f, detector.IsDown(f) || autoDown[KeyFor(f)] ? 0.6f : 0.3f);
        }

        private void Close(PianoNoteTrial n)
        {
            if (n.closedAt >= 0f) return;
            n.closedAt = clock;
            n.song = current != null ? current.title : "";
            pending.Add(n);
        }

        private void FlushTrials(bool all)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PianoNoteTrial n = pending[i];
                if (!all && !n.ReadyToWrite(clock)) continue;
                pianoLog?.Trial(LevelTag, n.song, round, n, level == PianoLevel.Test ? testTiming.ToString() : "Wait",
                                tempo, FingerFor, player != null && player.GloveConnected);
                pending.RemoveAt(i);
                i--;
            }
        }

        // ------------------------------------------------------------------ cues

        private List<Cue> CueOrder()
        {
            var order = new List<Cue>(2);
            if (cueTouch) order.Add(Cue.Touch);
            if (cueVisual) order.Add(Cue.Visual);
            return order;
        }

        private void FireCue(Cue cue, int[] keys)
        {
            switch (cue)
            {
                case Cue.Touch:
                    GloveChannels ch = GloveChannels.None;
                    foreach (int k in keys) ch |= ChannelFor(FingerFor(k));
                    TouchCue(ch, "CUE_" + KeyNames(keys));
                    break;
                case Cue.Visual:
                    foreach (int k in keys) { keyboard?.SetLit(k, true); fingerGlow?.Set(FingerFor(k), true); }
                    break;
            }
        }

        private void TouchCue(GloveChannels ch, string id)
        {
            if (player == null) return;
            if (touchCueKind == TouchCueKind.Vibrate)
            {
                player.Vibrate(ch, vibrationHz, touchStrength, id);
                StartCoroutine(StopVibrationAfter(ch, cueSeconds, id));
            }
            else player.Tap(ch, touchStrength, cueSeconds, id);
        }

        private IEnumerator StopVibrationAfter(GloveChannels ch, float seconds, string id)
        {
            yield return new WaitForSeconds(seconds);
            // also while paused: the stopped state must not be re-sent on resume
            if (State != TaskState.Ended && State != TaskState.Idle) player.StopVibration(ch, id + "_END");
        }

        private void ClearCues(int[] keys)
        {
            foreach (int k in keys) { keyboard?.SetLit(k, false); fingerGlow?.Set(FingerFor(k), false); }
        }

        // ------------------------------------------------------------------ settings (therapist)

        public void SetLevel(PianoLevel l)
        {
            if (State == TaskState.Running || State == TaskState.Paused) return;   // change between runs
            level = l;
            card?.Show("T2 Piano", "Press Start", LevelName(level), 0, CardTone.Normal);
        }

        public void ToggleSound() { pianoSound = !pianoSound; }

        /// <summary>L2 / L3: move on from the current note (logged as skipped; a Miss in L3).</summary>
        public void SkipNote()
        {
            if (State == TaskState.Running && (waiting != null)) skipRequested = true;
        }

        public void ToggleVisual()
        {
            cueVisual = !cueVisual;
            if (!cueVisual) { keyboard?.ClearLights(); fingerGlow?.AllOff(); }
            if (waiting != null && State == TaskState.Running && level == PianoLevel.Learn && tryAgainUntil < 0f)
                card?.SetHint(LearnHint());
        }

        public void ToggleTouch()
        {
            cueTouch = !cueTouch;
            if (waiting != null && State == TaskState.Running && level == PianoLevel.Learn && tryAgainUntil < 0f)
                card?.SetHint(LearnHint());
        }
        public void ChangeStrength(float d) { touchStrength = Mathf.Clamp(Mathf.Round((touchStrength + d) * 10f) / 10f, 0.1f, 1f); }
        public void ChangeTempo(float d) { tempo = Mathf.Clamp(Mathf.Round((tempo + d) * 10f) / 10f, 0.5f, 1.5f); }

        public void NextSong()
        {
            if (songs.Count == 0) return;
            songIndex = (songIndex + 1) % songs.Count;
            if (State != TaskState.Running) ShowLabels(SelectedSong);
        }

        public void Recalibrate()
        {
            recalibrate = true;
            if (detector != null && State != TaskState.Running) detector.SetSimulation(false);
        }

        public void OnLevel1(Oculus.Interaction.PointerEvent e) { SetLevel(PianoLevel.Listen); }
        public void OnLevel2(Oculus.Interaction.PointerEvent e) { SetLevel(PianoLevel.Learn); }
        public void OnLevel3(Oculus.Interaction.PointerEvent e) { SetLevel(PianoLevel.Test); }
        public void OnToggleSound(Oculus.Interaction.PointerEvent e) { ToggleSound(); }
        public void OnToggleVisual(Oculus.Interaction.PointerEvent e) { ToggleVisual(); }
        public void OnToggleTouch(Oculus.Interaction.PointerEvent e) { ToggleTouch(); }
        public void OnStrengthDown(Oculus.Interaction.PointerEvent e) { ChangeStrength(-0.1f); }
        public void OnStrengthUp(Oculus.Interaction.PointerEvent e) { ChangeStrength(0.1f); }
        public void OnSlower(Oculus.Interaction.PointerEvent e) { ChangeTempo(-0.1f); }
        public void OnFaster(Oculus.Interaction.PointerEvent e) { ChangeTempo(0.1f); }
        public void OnNextSong(Oculus.Interaction.PointerEvent e) { NextSong(); }
        public void OnRecalibrate(Oculus.Interaction.PointerEvent e) { Recalibrate(); }
        public void OnSkip(Oculus.Interaction.PointerEvent e) { SkipNote(); }

        /// <summary>The Hints button cycles cue presets: glow + touch → touch only → glow only.</summary>
        protected override void ApplyAssist()
        {
            if (!started) return;
            bool touch = assist != AssistLevel.TouchOnly;
            bool visual = assist != AssistLevel.HearOnly;
            if (cueTouch != touch) ToggleTouch();
            if (cueVisual != visual) ToggleVisual();
        }

        private void HandleT2Keys()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.lKey.wasPressedThisFrame) SetLevel(level == PianoLevel.Test ? PianoLevel.Listen : level + 1);
            if (k.sKey.wasPressedThisFrame) SkipNote();
            if (k.mKey.wasPressedThisFrame) ToggleSound();
            if (k.vKey.wasPressedThisFrame) ToggleVisual();
            if (k.tKey.wasPressedThisFrame) ToggleTouch();
            if (k.minusKey.wasPressedThisFrame) ChangeStrength(-0.1f);
            if (k.equalsKey.wasPressedThisFrame) ChangeStrength(0.1f);
            if (k.leftBracketKey.wasPressedThisFrame) ChangeTempo(-0.1f);
            if (k.rightBracketKey.wasPressedThisFrame) ChangeTempo(0.1f);
            if (k.nKey.wasPressedThisFrame) NextSong();
            if (k.kKey.wasPressedThisFrame) Recalibrate();
#endif
        }

        private void RefreshPanel()
        {
            SetText(levelLabels.Length > 0 ? levelLabels[0] : null, (level == PianoLevel.Listen ? "> " : "") + "L1 Listen");
            SetText(levelLabels.Length > 1 ? levelLabels[1] : null, (level == PianoLevel.Learn ? "> " : "") + "L2 Learn");
            SetText(levelLabels.Length > 2 ? levelLabels[2] : null, (level == PianoLevel.Test ? "> " : "") + "L3 Test");
            SetText(soundLabel, "Sound " + (pianoSound ? "on" : "off"));
            SetText(visualLabel, "Visual " + (cueVisual ? "on" : "off"));
            SetText(touchLabel, "Touch " + (cueTouch ? "on" : "off"));
            if (settingsText == null) return;
            string cal = detector == null ? "-" : detector.Simulating ? "desktop keys 1-5" :
                         detector.Calibrated ? "calibrated" : "not calibrated";
            if (recalibrate) cal += " (again at Start)";
            string s = LevelName(level) + "  |  " + (SelectedSong != null ? SelectedSong.title : "-") +
                       "\nTouch " + touchStrength.ToString("0.0") + " " + touchCueKind + "  |  tempo x" + tempo.ToString("0.0") +
                       (level == PianoLevel.Test ? "  |  " + testTiming : "") + "  |  hand: " + cal +
                       "\nCues: " + CueTag + "  |  piano sound " + (pianoSound ? "on" : "off");
            if (s != panelCache) { panelCache = s; settingsText.text = s; }
        }

        private static void SetText(TMP_Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Which finger plays key 0..4 on this hand (the mapping is its own inverse).</summary>
        private int FingerFor(int key) { return hand == HandSide.Right ? key : 4 - key; }
        private int KeyFor(int finger) { return FingerFor(finger); }

        private static GloveChannels ChannelFor(int finger) { return GloveChannelUtil.Order[finger]; }

        private float NoteSeconds(PianoSong song, PianoSong.Note note)
        {
            float beat = 60f / Mathf.Max(10f, song.beatsPerMinute);
            return Mathf.Max(minNoteInterval, note.beats * beat / Mathf.Max(0.1f, tempo));
        }

        private void SetProgress(int done, int total)
        {
            if (card == null || total <= 0) return;
            int seg = Mathf.Clamp(Mathf.CeilToInt(done * 5f / total), 0, 5);
            currentSeg = seg;
            card.Show(LevelName(level), current != null ? current.title : "", CurrentHint(), seg, CardTone.Normal);
        }

        private string CurrentHint()
        {
            switch (level)
            {
                case PianoLevel.Listen: return "Relax your hand and feel each note";
                case PianoLevel.Learn: return LearnHint();
                default: return "Play the song";
            }
        }

        private void ShowLabels(PianoSong song)
        {
            if (keyboard == null) return;
            var text = new string[5];
            for (int k = 0; k < 5; k++)
                text[k] = song != null && song.showNumbers ? (FingerFor(k) + 1).ToString() : PianoSong.KeyNames[k];
            keyboard.SetLabels(text);
        }

        private string CueTag
        {
            get
            {
                var parts = new List<string>(2);
                if (cueTouch) parts.Add("touch");
                if (cueVisual) parts.Add("visual");
                return parts.Count == 0 ? "none" : string.Join("+", parts);
            }
        }

        private string LevelTag => "L" + (int)level + "_" + level;

        private static string LevelName(PianoLevel l)
        {
            switch (l)
            {
                case PianoLevel.Listen: return "Level 1 · Listen";
                case PianoLevel.Learn: return "Level 2 · Learn";
                default: return "Level 3 · Play";
            }
        }

        private static string FingerWord(int f)
        {
            switch (f)
            {
                case 0: return "thumb";
                case 1: return "index finger";
                case 2: return "middle finger";
                case 3: return "ring finger";
                default: return "little finger";
            }
        }

        private static string KeyNames(int[] keys)
        {
            var names = new List<string>(keys.Length);
            foreach (int k in keys) names.Add(PianoSong.KeyNames[k]);
            return string.Join("+", names);
        }

        protected override void FillSummary(LearningSessionSummary s)
        {
            if (level != PianoLevel.Test) return;
            s.quiz_trials = testTrials.Count;
            foreach (PianoNoteTrial n in testTrials) if (n.correct) s.quiz_correct++;
        }

        protected override void OnPaused(bool paused)
        {
            if (!paused && autoPaused) { autoPaused = false; ShowGhost(false); RestoreCard(); }
            if (paused) { keyboard?.ReleaseAll(); detector?.ReleaseAll(); }
        }

        protected override void OnFinished()
        {
            waiting = null;
            CloseExpiredTempoNotes(true);
            FlushTrials(true);
            pianoLog?.Dispose();
            pianoLog = null;
            for (int k = 0; k < 5; k++) autoDown[k] = false;
            inSong = false;
            autoPaused = false;
            ShowGhost(false);
            keyboard?.ReleaseAll();
            keyboard?.ClearLights();
            fingerGlow?.AllOff();
            HideRating();

            var r = new PianoRunResult { level = level, song = current != null ? current.title : "", completed = EndReason == "completed" };
            if (level == PianoLevel.Test)
            {
                foreach (PianoNoteTrial t in testTrials) { r.notes++; if (t.correct) r.correct++; }
                r.seconds = testPlaySeconds;
            }
            else
            {
                r.notes = runNotes;
                r.correct = runCorrect;
                r.seconds = runStart >= 0f ? clock - runStart : 0f;
            }
            if (level != PianoLevel.Listen && r.notes > 0) r.accuracy = r.correct / (float)r.notes;
            LastResult = r;

            if (oneSongPerRun) card?.gameObject.SetActive(false);
            else if (EndReason != "completed" || level != PianoLevel.Test)
                card?.Show(LevelName(level), "Well done!", "That's all for now", 5, CardTone.Done);
            RunEnded?.Invoke(r);
        }

        // ------------------------------------------------------------------ hand on the keys

        /// <summary>Remember where the hand rests, in keyboard space (so it follows the keyboard).</summary>
        private void RecordHandPlace(Vector3 palm, Vector3 index, Vector3 middle, Vector3 little, float rigShift)
        {
            if (keyboard == null) return;
            Vector3 up = Vector3.up * rigShift;      // the rig moved after the hand was measured
            Transform k = keyboard.transform;
            placePalm = k.InverseTransformPoint(palm + up);
            placeIndex = k.InverseTransformPoint(index + up);
            placeMiddle = k.InverseTransformPoint(middle + up);
            placeLittle = k.InverseTransformPoint(little + up);
            hasHandPlace = true;
        }

        /// <summary>Where the palm and knuckles should be (world). Before calibration: a hand resting on C..G.</summary>
        private void HandPlace(out Vector3 palm, out Vector3 index, out Vector3 middle, out Vector3 little)
        {
            Transform k = keyboard != null ? keyboard.transform : transform;
            if (hasHandPlace)
            {
                palm = k.TransformPoint(placePalm); index = k.TransformPoint(placeIndex);
                middle = k.TransformPoint(placeMiddle); little = k.TransformPoint(placeLittle);
                return;
            }
            float sp = keyboard != null ? keyboard.Spacing : 0.022f;
            Vector3 Knuckle(int finger) => k.TransformPoint(new Vector3((KeyFor(finger) - 2) * sp * 0.9f, 0.035f, -0.055f));
            palm = k.TransformPoint(new Vector3(0f, 0.035f, -0.085f));
            index = Knuckle(1); middle = Knuckle(2); little = Knuckle(4);
        }

        /// <summary>Show / hide the ghost hand, lined up palm-and-knuckles with the hand's place.</summary>
        private void ShowGhost(bool on)
        {
            if (ghostHand == null) return;
            ghostHand.gameObject.SetActive(on);
            if (!on) return;
            HandPlace(out Vector3 palm, out Vector3 index, out Vector3 middle, out Vector3 little);
            Transform g = ghostHand.transform;
            Quaternion now = Frame(ghostHand.Contact(5).position, ghostHand.Joint(HandFinger.Index, 0).position,
                                   ghostHand.Joint(HandFinger.Middle, 0).position, ghostHand.Joint(HandFinger.Little, 0).position);
            g.rotation = Frame(palm, index, middle, little) * Quaternion.Inverse(now) * g.rotation;
            g.position += palm - ghostHand.Contact(5).position;
            if (hasHandPlace || keyboard == null) return;
            // default place: the four fingertips rest on the key tops at the fingertip line
            Transform k = keyboard.transform;
            Vector3 tips = Vector3.zero;
            for (int f = 1; f < 5; f++) tips += k.InverseTransformPoint(ghostHand.Contact(f).position);
            tips /= 4f;
            g.position += k.TransformVector(new Vector3(0f, padOffset - tips.y, -tips.z));
        }

        /// <summary>Hand frame from palm and knuckles (the same rule for the ghost and the real hand).</summary>
        private static Quaternion Frame(Vector3 palm, Vector3 index, Vector3 middle, Vector3 little)
        {
            Vector3 f = middle - palm;
            Vector3 a = index - little;
            Vector3 up = Vector3.Cross(f, a);
            return f.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(f, up) : Quaternion.identity;
        }

        private void PulseGhost()
        {
            if (ghostHand == null || !ghostHand.gameObject.activeSelf) return;
            if (ghostRenderers == null) ghostRenderers = ghostHand.GetComponentsInChildren<Renderer>(true);
            if (ghostBlock == null) ghostBlock = new MaterialPropertyBlock();
            Color c = ghostColor;
            c.a *= 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
            ghostBlock.SetColor("_BaseColor", c);
            foreach (Renderer r in ghostRenderers) if (r != null) r.SetPropertyBlock(ghostBlock);
        }

        /// <summary>
        /// While a song plays: a hand that leaves its place (or is lost) pauses the song and shows the
        /// ghost hand; when the hand is back on the ghost, the song goes on by itself.
        /// </summary>
        private void WatchHand()
        {
            if (!pauseWhenHandLeaves || !inSong || detector == null || detector.Simulating || patientHand == null) return;
            HandPlace(out Vector3 palm, out _, out _, out _);
            bool tracked = detector.Tracked;
            float d = tracked ? Vector3.Distance(patientHand.Palm, palm) : float.PositiveInfinity;
            float dt = Time.unscaledDeltaTime;

            if (State == TaskState.Running && !autoPaused)
            {
                awayTimer = d > leaveDistance ? awayTimer + dt : 0f;
                if (awayTimer > (tracked ? leaveSeconds : lostSeconds))
                {
                    awayTimer = 0f;
                    backTimer = 0f;
                    autoPaused = true;
                    Step("hand_left");
                    PauseTask();
                    ShowGhost(true);
                    card?.Show(LevelName(level), "Rest your hand on the keys", "Put it back on the ghost hand", currentSeg, CardTone.Answer);
                }
            }
            else if (State == TaskState.Paused && autoPaused)
            {
                backTimer = d < returnDistance ? backTimer + dt : 0f;
                if (backTimer > returnSeconds)
                {
                    autoPaused = false;
                    Step("hand_back");
                    ShowGhost(false);
                    RestoreCard();
                    ResumeTask();
                }
            }
        }

        private void RestoreCard()
        {
            if (current != null) card?.Show(LevelName(level), current.title, CurrentHint(), currentSeg, CardTone.Normal);
        }

        /// <summary>The five playable keys light up left to right once (ready / celebrate).</summary>
        public IEnumerator KeyWave(float step)
        {
            if (keyboard == null) yield break;
            for (int k = 0; k < 5; k++)
            {
                keyboard.SetLit(k, true);
                yield return new WaitForSeconds(step);
                keyboard.SetLit(k, false);
            }
        }
    }
}
