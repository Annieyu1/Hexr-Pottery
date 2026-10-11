using System;
using TMPro;
using UnityEngine;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// The in-world control panel: Start, Pause, Resume, Replay, End, the timer and the summary.
    /// It owns no game logic. Buttons call the public On...Pressed methods (from a Poke button's
    /// unity event or a UI Button) and the controller listens to the events.
    /// </summary>
    public class ControlPanel : MonoBehaviour
    {
        [Header("One panel per state (any can be left empty)")]
        [SerializeField] private GameObject idlePanel;
        [SerializeField] private GameObject playingPanel;
        [SerializeField] private GameObject pausedPanel;
        [SerializeField] private GameObject endedPanel;

        [Header("Text")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text summaryText;

        public event Action StartRequested;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action EndRequested;
        public event Action ReplayRequested;

        // ---- button hooks ----
        public void OnStartPressed()  { StartRequested?.Invoke(); }
        public void OnPausePressed()  { PauseRequested?.Invoke(); }
        public void OnResumePressed() { ResumeRequested?.Invoke(); }
        public void OnEndPressed()    { EndRequested?.Invoke(); }
        public void OnReplayPressed() { ReplayRequested?.Invoke(); }

        // Same hooks with a PointerEvent argument, for buttons whose wrapper is a
        // PointableUnityEventWrapper (flat poke buttons). Pick the "Dynamic PointerEvent" entry.
        public void OnStartPressed(Oculus.Interaction.PointerEvent e)  { OnStartPressed(); }
        public void OnPausePressed(Oculus.Interaction.PointerEvent e)  { OnPausePressed(); }
        public void OnResumePressed(Oculus.Interaction.PointerEvent e) { OnResumePressed(); }
        public void OnEndPressed(Oculus.Interaction.PointerEvent e)    { OnEndPressed(); }
        public void OnReplayPressed(Oculus.Interaction.PointerEvent e) { OnReplayPressed(); }

        // ---- display ----
        public void ShowState(GameState state)
        {
            SetActive(idlePanel, state == GameState.Idle);
            SetActive(playingPanel, state == GameState.Playing);
            SetActive(pausedPanel, state == GameState.Paused);
            SetActive(endedPanel, state == GameState.Ended);
        }

        public void SetTimer(float seconds)
        {
            if (timerText != null) timerText.text = SessionClock.Format(seconds);
        }

        public void SetPrompt(string text)
        {
            if (promptText != null) promptText.text = text;
        }

        public void ShowSummary(SessionSummary s)
        {
            if (summaryText == null) return;
            float accuracy = s.trials_completed > 0 ? 100f * s.trials_correct / s.trials_completed : 0f;
            summaryText.text =
                "Time  " + SessionClock.Format(s.active_seconds) + "\n" +
                "Trials  " + s.trials_completed + "\n" +
                "Correct  " + accuracy.ToString("0") + "%";
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null) go.SetActive(on);
        }
    }
}
