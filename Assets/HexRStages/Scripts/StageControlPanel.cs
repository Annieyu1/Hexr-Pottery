using Oculus.Interaction;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Stages
{
    /// <summary>
    /// The always-visible buttons, outside the stage roots.
    /// Running: Pause + Menu. Paused: Start + Menu + "Paused".
    /// Menu goes back to the very start of Stage 1 (there is no real menu yet).
    /// </summary>
    public class StageControlPanel : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private DemoFlowController demoFlow;

        [Header("Buttons")]
        [SerializeField] private GameObject pauseButton;
        [SerializeField] private GameObject startButton;
        [SerializeField] private GameObject menuButton;

        [Header("UI")]
        [SerializeField] private GameObject pausedText;

        [Header("Debug")]
        [Tooltip("Editor keys: P pause / start, M menu.")]
        [SerializeField] private bool debugInput = true;

        private void OnEnable()
        {
            StagePause.PausedChanged += Refresh;
            Refresh(StagePause.IsPausedNow);
        }

        private void OnDisable()
        {
            StagePause.PausedChanged -= Refresh;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (!debugInput || Keyboard.current == null)
                return;

            Keyboard k = Keyboard.current;
            if (k.pKey.wasPressedThisFrame)
            {
                if (StagePause.IsPausedNow) Resume();
                else Pause();
            }
            if (k.mKey.wasPressedThisFrame) BackToMenu();
#endif
        }

        private void Refresh(bool paused)
        {
            if (pauseButton != null) pauseButton.SetActive(!paused);
            if (startButton != null) startButton.SetActive(paused);
            if (menuButton != null) menuButton.SetActive(true);
            if (pausedText != null) pausedText.SetActive(paused);
        }

        private void Pause()
        {
            if (StagePause.Instance != null)
                StagePause.Instance.Pause();
            else
                Debug.LogError("[ControlPanel] No StagePause in the scene.");
        }

        private void Resume()
        {
            if (StagePause.Instance != null)
                StagePause.Instance.Resume();
        }

        private void BackToMenu()
        {
            if (demoFlow != null)
                demoFlow.BackToMenu();
            else
                Debug.LogError("[ControlPanel] DemoFlowController is not assigned!");
        }

        // Poke buttons (PointableUnityEventWrapper.WhenSelect)
        public void OnPausePressed(PointerEvent e) { Pause(); }
        public void OnStartPressed(PointerEvent e) { Resume(); }
        public void OnMenuPressed(PointerEvent e) { BackToMenu(); }
    }
}
