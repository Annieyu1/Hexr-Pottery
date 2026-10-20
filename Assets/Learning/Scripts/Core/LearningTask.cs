using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>docs/10 §5.7: cues fade from "see and hear" to "touch only".</summary>
    public enum AssistLevel { SeeAndHear = 0, HearOnly = 1, TouchOnly = 2 }

    /// <summary>docs/10 §5.2: L-看 watch, L-跟 follow along, L-做 do it yourself.</summary>
    public enum LearningMode { Watch, Follow, Do }

    public enum TaskState { Idle, Running, Paused, Ended }

    /// <summary>
    /// Base for a learning-phase task. The patient only experiences touch; nothing is answered or
    /// scored. Subclasses write one round as a coroutine using Wait() and Say(); this class runs
    /// rounds until the time limit, handles pause / resume / end, the assist level and the log.
    /// </summary>
    public abstract class LearningTask : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] protected string participantId = "P000";
        [SerializeField] protected HandSide hand = HandSide.Right;
        [SerializeField] protected LearningMode mode = LearningMode.Watch;
        [SerializeField] protected AssistLevel assist = AssistLevel.SeeAndHear;
        [Tooltip("The task stops after this many minutes (rounds in progress finish first).")]
        [SerializeField] protected float minutes = 4f;
        [Tooltip("Start by itself this many seconds after the scene loads. Negative = wait for Start.")]
        [SerializeField] protected float autoStartDelay = -1f;

        [Header("References")]
        [SerializeField] protected HapticPlayer player;
        [SerializeField] protected TMP_Text caption;
        [SerializeField] protected TMP_Text status;
        [Tooltip("Objects shown only at the 'see and hear' level (glow dots, intensity bar, labels).")]
        [SerializeField] protected GameObject[] visualCues;
        [SerializeField] protected ChannelHighlights[] highlights;
        [SerializeField] protected SceneDimmer dimmer;
        [Tooltip("Tracked-hand objects to hide in Watch mode (e.g. LeftOVRHand / RightOVRHand).")]
        [SerializeField] protected GameObject[] trackedHands;

        [Header("Controls (shown per state)")]
        [SerializeField] private GameObject idleControls;
        [SerializeField] private GameObject runningControls;
        [SerializeField] private GameObject pausedControls;
        [SerializeField] private GameObject endedControls;

        protected LearningLog log;
        protected int round;
        private Coroutine runner;
        private float activeSeconds;
        private int pauseCount;
        private readonly List<string> assistUsed = new List<string>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        public TaskState State { get; private set; } = TaskState.Idle;
        public AssistLevel Assist => assist;
        public bool SoundOn => assist != AssistLevel.TouchOnly;
        protected abstract string TaskName { get; }

        /// <summary>Tasks that use the patient's own VR hand keep it visible.</summary>
        protected virtual bool HideTrackedHandsInWatch => true;

        // ------------------------------------------------------------------ lifecycle

        protected virtual void Start()
        {
            if (player != null) player.SetHand(hand);
            ApplyAssist();
            ShowControls();
            Say("Press Start when ready.");
            if (autoStartDelay >= 0f) StartCoroutine(AutoStart());
        }

        private IEnumerator AutoStart()
        {
            yield return new WaitForSeconds(autoStartDelay);
            if (State == TaskState.Idle) BeginTask();
        }

        protected virtual void Update()
        {
            if (State == TaskState.Running) activeSeconds += Time.deltaTime;
            HandleKeyboard();
            UpdateStatus();
        }

        private void OnDestroy()
        {
            if (State == TaskState.Running || State == TaskState.Paused) Finish("scene_closed");
        }

        // ------------------------------------------------------------------ controls

        public void BeginTask()
        {
            if (State == TaskState.Running || State == TaskState.Paused) return;
            log?.Dispose();
            log = new LearningLog(participantId, TaskName) { Mode = mode.ToString(), Assist = assist.ToString() };
            if (player != null) player.Log = log;
            round = 0;
            activeSeconds = 0f;
            pauseCount = 0;
            assistUsed.Clear();
            assistUsed.Add(assist.ToString());
            SetTrackedHandsHidden(mode == LearningMode.Watch && HideTrackedHandsInWatch);
            State = TaskState.Running;
            ShowControls();
            runner = StartCoroutine(RunAll());
        }

        public void PauseTask()
        {
            if (State != TaskState.Running) return;
            State = TaskState.Paused;
            pauseCount++;
            player?.Suspend();
            OnPaused(true);
            ShowControls();
        }

        public void ResumeTask()
        {
            if (State != TaskState.Paused) return;
            State = TaskState.Running;
            player?.ResumeHolds();
            OnPaused(false);
            ShowControls();
        }

        public void EndTask() { Finish("ended_by_user"); }

        public void CycleAssist()
        {
            assist = (AssistLevel)(((int)assist + 1) % 3);
            if (!assistUsed.Contains(assist.ToString())) assistUsed.Add(assist.ToString());
            if (log != null) log.Assist = assist.ToString();
            ApplyAssist();
        }

        public void SetAssist(AssistLevel level)
        {
            assist = level;
            if (log != null) log.Assist = assist.ToString();
            ApplyAssist();
        }

        // Poke buttons pass a PointerEvent (PointableUnityEventWrapper).
        public void OnStart(Oculus.Interaction.PointerEvent e) { BeginTask(); }
        public void OnPause(Oculus.Interaction.PointerEvent e) { PauseTask(); }
        public void OnResume(Oculus.Interaction.PointerEvent e) { ResumeTask(); }
        public void OnEnd(Oculus.Interaction.PointerEvent e) { EndTask(); }
        public void OnAssist(Oculus.Interaction.PointerEvent e) { CycleAssist(); }

        private void HandleKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.spaceKey.wasPressedThisFrame)
            {
                if (State == TaskState.Idle || State == TaskState.Ended) BeginTask();
                else if (State == TaskState.Running) PauseTask();
                else ResumeTask();
            }
            if (k.aKey.wasPressedThisFrame) CycleAssist();
            if (k.escapeKey.wasPressedThisFrame) EndTask();
#endif
        }

        // ------------------------------------------------------------------ running

        private IEnumerator RunAll()
        {
            yield return Intro();
            yield return Body();
            Finish(BodyEndReason);
        }

        /// <summary>Rounds until the time limit. Step-based tasks (T1) override this.</summary>
        protected virtual IEnumerator Body()
        {
            while (activeSeconds < minutes * 60f)
            {
                round++;
                if (log != null) log.Round = round;
                yield return RunRound(round);
            }
        }

        /// <summary>End reason logged when Body() returns by itself.</summary>
        protected virtual string BodyEndReason => "time_up";

        /// <summary>The reason the task ended (valid in OnFinished).</summary>
        protected string EndReason { get; private set; }

        /// <summary>Caption shown when the task ends.</summary>
        protected virtual string EndMessage => "Well done. That's all for this task.";

        /// <summary>The task finished normally (e.g. the patient pressed Finish).</summary>
        protected void CompleteTask() { Finish("completed"); }

        /// <summary>Subclasses add their own results to the session summary.</summary>
        protected virtual void FillSummary(LearningSessionSummary s) { }

        /// <summary>Optional opening, before the first round.</summary>
        protected virtual IEnumerator Intro() { yield break; }

        /// <summary>One round of the task. Use Wait() so pausing works.</summary>
        protected abstract IEnumerator RunRound(int roundIndex);

        /// <summary>Wait that stands still while paused.</summary>
        protected IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (State == TaskState.Running) t += Time.deltaTime;
                yield return null;
            }
        }

        protected void Say(string text)
        {
            if (caption != null) caption.text = text;
        }

        protected void Step(string name)
        {
            if (log != null) log.Step = name;
        }

        private void Finish(string reason)
        {
            if (State == TaskState.Ended || State == TaskState.Idle) return;
            if (runner != null) StopCoroutine(runner);
            State = TaskState.Ended;
            EndReason = reason;
            player?.StopAll(reason);
            OnFinished();
            SetTrackedHandsHidden(false);
            Say(EndMessage);
            ShowControls();

            if (log != null)
            {
                var s = new LearningSessionSummary
                {
                    session_id = log.SessionId,
                    participant_id = participantId,
                    task = TaskName,
                    hand = hand.ToString(),
                    mode = mode.ToString(),
                    app_version = Application.version,
                    started_local = log.SessionId,
                    active_seconds = activeSeconds,
                    rounds_completed = Mathf.Max(0, reason == "time_up" || reason == "completed" ? round : round - 1),
                    pause_count = pauseCount,
                    end_reason = reason,
                    glove_connected_at_end = player != null && player.GloveConnected,
                    assist_levels_used = new List<string>(assistUsed),
                };
                if (player != null) s.stimulation_seconds = (float[])player.StimulationSeconds.Clone();
                FillSummary(s);
                log.WriteSummary(s);
                Debug.Log("[Learning] " + TaskName + " log written to " + log.Folder);
                log.Dispose();
                log = null;
            }
        }

        /// <summary>Subclasses put their visuals back here (hands open, keys up...).</summary>
        protected virtual void OnFinished() { }

        protected virtual void OnPaused(bool paused) { }

        /// <summary>Subclasses apply level-specific cues here; call base.</summary>
        protected virtual void ApplyAssist()
        {
            bool see = assist == AssistLevel.SeeAndHear;
            if (visualCues != null)
                foreach (GameObject go in visualCues)
                    if (go != null) go.SetActive(see);
            if (highlights != null)
                foreach (ChannelHighlights h in highlights)
                    if (h != null) h.ShowCues = see;
            if (dimmer != null) dimmer.SetDimmed(assist == AssistLevel.TouchOnly);
        }

        private void ShowControls()
        {
            if (idleControls != null) idleControls.SetActive(State == TaskState.Idle);
            if (runningControls != null) runningControls.SetActive(State == TaskState.Running);
            if (pausedControls != null) pausedControls.SetActive(State == TaskState.Paused);
            if (endedControls != null) endedControls.SetActive(State == TaskState.Ended);
        }

        private void UpdateStatus()
        {
            if (status == null) return;
            int s = Mathf.FloorToInt(activeSeconds);
            string glove = player != null && player.GloveConnected ? "glove on" : "no glove";
            status.text = TaskName + "  |  " + State + "  |  round " + round + "  |  " +
                          (s / 60).ToString("00") + ":" + (s % 60).ToString("00") + "  |  " +
                          assist + "  |  " + glove;
        }

        private void SetTrackedHandsHidden(bool hidden)
        {
            if (hidden)
            {
                if (trackedHands == null) return;
                foreach (GameObject go in trackedHands)
                    if (go != null)
                        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                            if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
            }
            else
            {
                foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
                hiddenRenderers.Clear();
            }
        }
    }
}
