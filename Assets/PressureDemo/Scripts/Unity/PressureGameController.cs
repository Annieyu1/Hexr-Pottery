using System;
using System.IO;
using UnityEngine;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// Runs the pressure demo: two identical objects, one "Stronger" pad.
    ///
    ///   Idle --Start--> Playing <--Pause/Resume--> Paused
    ///   Playing/Paused --End or time up--> Ended --Replay--> Idle
    ///
    /// Each trial: Prepare -> Explore (grab as often as you like) -> Answer (release inside the
    /// pad, confirmed after a short dwell) -> Feedback -> next trial. The controller owns the
    /// order of events; the pieces (objects, zone, hint, panel, feedback) know nothing of each other.
    /// </summary>
    public class PressureGameController : MonoBehaviour
    {
        private enum Phase
        {
            None,
            Exploring,
            Feedback
        }

        [Header("Scene references")]
        [SerializeField] private PressureObject leftObject;
        [SerializeField] private PressureObject rightObject;
        [SerializeField] private AnswerZone zone;
        [SerializeField] private HintController hint;
        [SerializeField] private ControlPanel panel;
        [SerializeField] private FeedbackPresenter feedback;

        [Header("Settings")]
        [SerializeField] private PressureSettings settings = new PressureSettings();

        [Header("Debug")]
        [Tooltip("Objects can be grabbed before Start is pressed. For testing grabbing and glove " +
                 "pressure on their own; the answer zone and data logging stay off.")]
        [SerializeField] private bool allowGrabInIdle;

        private GameFlow flow;
        private SessionClock clock;
        private TrialGenerator generator;
        private TrialLogger logger;

        private PressureTrial trial;
        private Phase phase = Phase.None;
        private float feedbackTimer;
        private bool resumeAtNextTrial;

        private int trialIndex;
        private int completed;
        private int correctCount;
        private string startedUtc;

        private void Awake()
        {
            flow = new GameFlow();
            flow.StateChanged += OnFlowStateChanged;
        }

        private void OnEnable()
        {
            panel.StartRequested += HandleStart;
            panel.PauseRequested += HandlePause;
            panel.ResumeRequested += HandleResume;
            panel.EndRequested += HandleEnd;
            panel.ReplayRequested += HandleReplay;

            zone.Answered += HandleAnswered;
            leftObject.Grabbed += HandleGrabbed;
            rightObject.Grabbed += HandleGrabbed;
        }

        private void OnDisable()
        {
            panel.StartRequested -= HandleStart;
            panel.PauseRequested -= HandlePause;
            panel.ResumeRequested -= HandleResume;
            panel.EndRequested -= HandleEnd;
            panel.ReplayRequested -= HandleReplay;

            zone.Answered -= HandleAnswered;
            leftObject.Grabbed -= HandleGrabbed;
            rightObject.Grabbed -= HandleGrabbed;

            logger?.Dispose();
            logger = null;
        }

        private void Start()
        {
            ApplySettingsToScene();
            EnterIdle();
        }

        private void Update()
        {
            if (flow.State != GameState.Playing) return;

            clock.Tick(Time.unscaledDeltaTime);
            panel.SetTimer(clock.ElapsedSeconds);

            if (clock.LimitReached)
            {
                EndSession(EndReason.TimeUp);
                return;
            }

            if (phase == Phase.Feedback)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                if (feedbackTimer <= 0f)
                    BeginTrial(newStimulus: true);
            }
        }

        // ------------------------------------------------------------------
        // Control panel requests
        // ------------------------------------------------------------------

        private void HandleStart()
        {
            if (!flow.Start()) return;
            // Session set-up happens in OnFlowStateChanged(Idle -> Playing).
        }

        private void HandlePause()  { flow.Pause(); }
        private void HandleResume() { flow.Resume(); }
        private void HandleEnd()    { EndSession(EndReason.Manual); }
        private void HandleReplay() { flow.Replay(); }

        private void EndSession(EndReason reason)
        {
            flow.End(reason);
        }

        // ------------------------------------------------------------------
        // State changes
        // ------------------------------------------------------------------

        private void OnFlowStateChanged(GameState from, GameState to)
        {
            panel.ShowState(to);

            switch (to)
            {
                case GameState.Idle:
                    EnterIdle();
                    break;

                case GameState.Playing:
                    if (from == GameState.Idle) BeginSession();
                    else ResumePlaying();
                    break;

                case GameState.Paused:
                    EnterPaused();
                    break;

                case GameState.Ended:
                    FinishSession();
                    break;
            }
        }

        private void EnterIdle()
        {
            ApplySettingsToScene();
            LockEverything();
            if (allowGrabInIdle)
            {
                leftObject.SetLocked(false);
                rightObject.SetLocked(false);
            }
            phase = Phase.None;
            panel.ShowState(GameState.Idle);
            panel.SetTimer(0f);
            panel.SetPrompt("");
            feedback.Clear();
        }

        private void BeginSession()
        {
            ApplySettingsToScene();

            logger?.Dispose();
            string root = Path.Combine(Application.persistentDataPath, "pressure_sessions");
            logger = new TrialLogger(root, settings.participantId);

            generator = new TrialGenerator(settings);
            clock = new SessionClock(settings.sessionLimitSeconds);
            clock.Start();

            trialIndex = 0;
            completed = 0;
            correctCount = 0;
            startedUtc = DateTime.UtcNow.ToString("o");

            BeginTrial(newStimulus: true);
        }

        private void EnterPaused()
        {
            clock.Pause();
            hint.Stop();
            LockEverything();
            // A trial in progress restarts on resume; a finished one just moves on.
            resumeAtNextTrial = phase == Phase.Feedback;
        }

        private void ResumePlaying()
        {
            clock.Resume();
            if (resumeAtNextTrial)
                BeginTrial(newStimulus: true);
            else
                BeginTrial(newStimulus: false);     // same stimulus, fresh start
        }

        private void FinishSession()
        {
            clock.Stop();
            hint.Stop();
            LockEverything();

            // A trial still in progress is not logged; the summary says so.
            bool abandoned = phase == Phase.Exploring;
            phase = Phase.None;

            var summary = new SessionSummary
            {
                session_id = logger != null ? logger.SessionId : "",
                started_utc = startedUtc,
                participant_id = settings.participantId,
                hand = settings.hand,
                difficulty = settings.difficultyLevel,
                app_version = Application.version,
                active_seconds = clock.ElapsedSeconds,
                pause_count = flow.PauseCount,
                end_reason = flow.EndedBecause.ToString(),
                trials_completed = completed,
                trials_correct = correctCount,
                abandoned_trial = abandoned,
            };
            logger?.WriteSummary(summary, JsonUtility.ToJson(summary, true));
            panel.ShowSummary(summary);

            logger?.Dispose();
            logger = null;
        }

        // ------------------------------------------------------------------
        // Trial loop
        // ------------------------------------------------------------------

        private void BeginTrial(bool newStimulus)
        {
            if (newStimulus)
            {
                trialIndex++;
                trial = generator.Next(trialIndex);
            }

            ClearHighlights();
            feedback.Clear();

            leftObject.SetStrength(trial.strengthLeft);
            rightObject.SetStrength(trial.strengthRight);
            leftObject.ResetStats();
            rightObject.ResetStats();
            zone.ResetStats();

            trial.tTrialStart = clock.ElapsedSeconds;
            trial.tFirstGrab = -1f;
            trial.wallClockStartUtc = DateTime.UtcNow.ToString("o");

            // Both objects start every trial on their pedestals, then become grabbable.
            leftObject.SetLocked(true);
            rightObject.SetLocked(true);
            leftObject.SetLocked(false);
            rightObject.SetLocked(false);
            zone.SetActive(true);

            hint.BeginTrial(showImmediately: trialIndex == 1);
            panel.SetPrompt("Which one feels stronger?\nPut it in the Stronger area.");
            phase = Phase.Exploring;
        }

        private void HandleGrabbed(PressureObject obj)
        {
            if (phase != Phase.Exploring) return;
            if (trial.tFirstGrab < 0f)
                trial.tFirstGrab = clock.ElapsedSeconds;
            hint.NotifyGrab();
        }

        private void HandleAnswered(PressureObject chosen)
        {
            if (phase != Phase.Exploring || flow.State != GameState.Playing) return;

            trial.answered = true;
            trial.chosenSide = chosen.Side;
            trial.correct = chosen.Side == trial.strongerSide;
            trial.changedMindCount = zone.ChangedMindCount;
            trial.tAnswer = clock.ElapsedSeconds;
            trial.grabsLeft = leftObject.GrabCount;
            trial.grabsRight = rightObject.GrabCount;
            trial.holdSecondsLeft = leftObject.HoldSeconds;
            trial.holdSecondsRight = rightObject.HoldSeconds;
            trial.hintShown = hint.ShownCount > 0;
            trial.autoReturned = leftObject.AutoReturnCount + rightObject.AutoReturnCount;

            logger?.AppendTrial(trial);
            completed++;
            if (trial.correct) correctCount++;

            // Silence the glove and freeze the objects where they are for the feedback moment.
            hint.Stop();
            zone.SetActive(false);
            FreezeForFeedback();

            PressureObject stronger = trial.strongerSide == Side.Left ? leftObject : rightObject;
            if (trial.correct) feedback.ShowCorrect(chosen.transform);
            else feedback.ShowRetry(stronger);

            panel.SetPrompt("");
            feedbackTimer = settings.nextTrialDelay;
            phase = Phase.Feedback;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void ApplySettingsToScene()
        {
            leftObject.ReturnDelay = settings.returnDelay;
            rightObject.ReturnDelay = settings.returnDelay;
            zone.Configure(settings.confirmDwell);
            hint.Configure(settings.hintIdleSeconds);
        }

        /// <summary>Not grabbable, glove silent, objects home, zone off.</summary>
        private void LockEverything()
        {
            leftObject.SetLocked(true);
            rightObject.SetLocked(true);
            zone.SetActive(false);
            ClearHighlights();
        }

        /// <summary>
        /// During feedback the objects cannot be grabbed (no pressure) but stay put so the
        /// "correct one glows" cue is visible. They go home when the next trial begins.
        /// </summary>
        private void FreezeForFeedback()
        {
            leftObject.SetLockedKeepPose(true);
            rightObject.SetLockedKeepPose(true);
        }

        private void ClearHighlights()
        {
            leftObject.SetHighlight(false);
            rightObject.SetHighlight(false);
        }
    }
}
