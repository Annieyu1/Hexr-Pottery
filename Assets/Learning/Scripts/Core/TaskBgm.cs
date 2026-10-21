using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Background music for a learning task. Volume follows the task, always with smooth fades:
    ///   scene start  fade in to the idle level (louder: the start screen)
    ///   Running      fade down to the running level
    ///   Ducked       quieter still while the task asks for it (T1: squeezes being counted, answering)
    ///   Paused       fade out; Resume fades back in
    ///   Ended        back to the idle level (finished screen)
    ///   TouchOnly    silent (that assist level has no sound at all)
    /// The track is not looped by the AudioSource: near its end it fades out and restarts with a fade
    /// in, so the loop point is never a hard cut.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class TaskBgm : MonoBehaviour
    {
        [SerializeField] private LearningTask task;

        [Header("Levels (0–1)")]
        [Tooltip("Before Start and after the task ends.")]
        [Range(0f, 1f)] [SerializeField] private float idleVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float runningVolume = 0.25f;
        [Tooltip("While the task needs the patient's full attention on touch (T1 step 4).")]
        [Range(0f, 1f)] [SerializeField] private float duckVolume = 0.06f;

        [Header("Fades (s)")]
        [SerializeField] private float startFadeSeconds = 3f;
        [Tooltip("Between idle / running / ducked / paused.")]
        [SerializeField] private float levelFadeSeconds = 1.5f;
        [Tooltip("Ducking in is quicker so the music is already quiet when the squeezes start.")]
        [SerializeField] private float duckFadeSeconds = 0.6f;
        [Tooltip("Fade out before the end of the track and in again after it restarts.")]
        [SerializeField] private float loopFadeSeconds = 2.5f;

        private AudioSource source;
        private float level;        // follows the task state
        private float loopGain;     // 0 → 1 → 0 around the track's restart
        private bool firstFade = true;

        /// <summary>Set by the task while it wants the music low.</summary>
        public bool Ducked { get; set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            if (task == null) task = GetComponentInParent<LearningTask>();
            if (task == null) task = FindAnyObjectByType<LearningTask>();
        }

        private void Start()
        {
            if (source.clip == null) { enabled = false; return; }
            level = 0f;
            loopGain = 1f;
            source.Play();
        }

        private void Update()
        {
            float target = Target();
            float seconds = firstFade ? startFadeSeconds : Ducked && target < level ? duckFadeSeconds : levelFadeSeconds;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, seconds));
            if (firstFade && Mathf.Approximately(level, target)) firstFade = false;

            // soft loop: fade out near the end, restart, fade in
            AudioClip clip = source.clip;
            float remaining = clip.length - source.time;
            if (!source.isPlaying && Application.isFocused) { source.time = 0f; source.Play(); loopGain = 0f; }
            if (remaining < loopFadeSeconds) loopGain = Mathf.Clamp01(remaining / Mathf.Max(0.05f, loopFadeSeconds));
            else if (source.time < loopFadeSeconds) loopGain = Mathf.Max(loopGain, Mathf.Clamp01(source.time / Mathf.Max(0.05f, loopFadeSeconds)));
            else loopGain = 1f;

            source.volume = level * loopGain;
        }

        private float Target()
        {
            if (task == null) return idleVolume;
            if (task.Assist == AssistLevel.TouchOnly) return 0f;
            switch (task.State)
            {
                case TaskState.Running: return Ducked ? duckVolume : runningVolume;
                case TaskState.Paused: return 0f;
                default: return idleVolume;   // Idle (start screen) and Ended
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (source == null) return;
            if (paused) source.Pause();
            else if (enabled) source.UnPause();
        }
    }
}
