using System.Collections;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Stages
{
    /// <summary>
    /// Stage 1.1: squeeze one ball twice (soft 10, then hard 60) and see the value.
    /// Stage 1.2: two balls slide out to the left (10) and right (60). Once both have been squeezed,
    /// the Weak / Strong labels pop up in front of them, and 2 s later Next appears.
    /// One squeeze = grab + release (GrabNotifier.Released). Every wait stops while the stage is paused.
    /// </summary>
    public class Stage1Controller : MonoBehaviour
    {
        public enum Stage1State
        {
            SoftReady,
            SoftResult,

            HardReady,
            HardResult,

            DifferencePrompt,

            Splitting,          // the two balls are moving apart
            ComparisonReady,    // Stage 1.2: squeeze both balls
            LabelsShown,        // both squeezed, Weak / Strong labels are up

            Completed           // Next is showing
        }

        [Header("Flow")]
        [SerializeField] private DemoFlowController demoFlow;

        [Header("Instruction UI")]
        [SerializeField] private TMP_Text instructionText;

        [Header("Result UI")]
        [SerializeField] private GameObject resultCanvas;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultValue;

        [Header("HexR Strength")]
        [SerializeField] private float softStrength = 10f;
        [SerializeField] private float hardStrength = 60f;

        [Header("Timing")]
        [SerializeField] private float resultDisplayTime = 1.0f;

        [Header("Stage 1.2 Balls")]
        [SerializeField] private GrabNotifier centerBall;
        [SerializeField] private GrabNotifier softBall;
        [SerializeField] private GrabNotifier hardBall;

        [Header("Ball Sets")]
        [SerializeField] private GameObject centralSet;
        [SerializeField] private GameObject softSet;
        [SerializeField] private GameObject hardSet;

        [Header("Stage 1.2 Ball Points")]
        [SerializeField] private Transform centerPoint;
        [SerializeField] private Transform softPoint;
        [SerializeField] private Transform hardPoint;

        [Header("Stage 1.2 UI")]
        [SerializeField] private GameObject softInfo;
        [SerializeField] private GameObject hardInfo;
        [SerializeField] private GameObject stage12Menu;

        [Header("Stage 1.2 Transition")]
        [SerializeField] private float differencePromptTime = 3f;
        [SerializeField] private float splitDuration = 0.5f;
        [SerializeField] private float labelPopDuration = 0.3f;
        [Tooltip("From the labels popping up to the Next button.")]
        [SerializeField] private float nextDelay = 2f;

        [Header("Debug")]
        [SerializeField] private Stage1State currentState;
        [Tooltip("Editor keys: Space squeeze the central ball, 1 / 2 squeeze soft / hard, T restart.")]
        [SerializeField] private bool debugInput = true;

        private bool softExperienced;
        private bool hardExperienced;
        private Vector3 softInfoScale = Vector3.one;
        private Vector3 hardInfoScale = Vector3.one;

        private void Awake()
        {
            if (softInfo != null) softInfoScale = softInfo.transform.localScale;
            if (hardInfo != null) hardInfoScale = hardInfo.transform.localScale;
        }

        private void OnEnable()
        {
            centerBall.Released += OnCenterReleased;
            softBall.Released += OnSoftReleased;
            hardBall.Released += OnHardReleased;
        }

        private void OnDisable()
        {
            centerBall.Released -= OnCenterReleased;
            softBall.Released -= OnSoftReleased;
            hardBall.Released -= OnHardReleased;
        }

        private void Start()
        {
            StartStage1();
        }

        // ========================================================
        // DEBUG INPUT
        // ========================================================

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (!debugInput || Keyboard.current == null)
                return;

            Keyboard k = Keyboard.current;
            if (k.spaceKey.wasPressedThisFrame) OnCenterReleased(centerBall);
            if (k.digit1Key.wasPressedThisFrame) OnSoftReleased(softBall);
            if (k.digit2Key.wasPressedThisFrame) OnHardReleased(hardBall);
            if (k.tKey.wasPressedThisFrame) ResetStage();
#endif
        }

        // ========================================================
        // START STAGE
        // ========================================================

        /// <summary>Menu: back to Stage 1.1 step 1, every ball at home.</summary>
        public void ResetStage()
        {
            Debug.Log("[Stage1] RESET");
            StartStage1();
        }

        public void StartStage1()
        {
            StopAllCoroutines();

            softExperienced = false;
            hardExperienced = false;

            if (stage12Menu != null) stage12Menu.SetActive(false);
            HideLabel(softInfo);
            HideLabel(hardInfo);

            // Stage 1.1 starts with only the central ball.
            centralSet.SetActive(true);
            softSet.SetActive(false);
            hardSet.SetActive(false);

            // Also gives back the split's kinematic hold if it was interrupted.
            ResetBall(centerBall);
            ResetBall(softBall);
            ResetBall(hardBall);

            currentState = Stage1State.SoftReady;
            UpdateHapticStrength();

            HideResult();
            ShowInstruction("Squeeze the object gently");

            Debug.Log("[Stage1] SOFT READY");
        }

        // ========================================================
        // STAGE 1.1: ONE SQUEEZE = GRAB + RELEASE
        // ========================================================

        private void OnCenterReleased(GrabNotifier ball)
        {
            if (StagePause.IsPausedNow)
                return;

            if (currentState == Stage1State.SoftReady)
                StartCoroutine(SoftSqueezed());
            else if (currentState == Stage1State.HardReady)
                StartCoroutine(HardSqueezed());
        }

        private IEnumerator SoftSqueezed()
        {
            currentState = Stage1State.SoftResult;
            ShowResult("WEAK", softStrength.ToString("0"));
            Debug.Log("[Stage1] SOFT RESULT");

            yield return StagePause.Wait(resultDisplayTime);

            currentState = Stage1State.HardReady;
            UpdateHapticStrength();
            HideResult();
            ShowInstruction("Squeeze the object again");

            Debug.Log("[Stage1] HARD READY");
        }

        private IEnumerator HardSqueezed()
        {
            currentState = Stage1State.HardResult;
            ShowResult("HARD", hardStrength.ToString("0"));
            Debug.Log("[Stage1] HARD RESULT");

            yield return StagePause.Wait(resultDisplayTime);

            currentState = Stage1State.DifferencePrompt;
            HideResult();
            ShowInstruction("Can you feel the difference?");
            Debug.Log("[Stage1] DIFFERENCE PROMPT");

            yield return StagePause.Wait(differencePromptTime);

            yield return SplitBalls();
        }

        // ========================================================
        // STAGE 1.1 → STAGE 1.2 TRANSITION
        // ========================================================

        private IEnumerator SplitBalls()
        {
            currentState = Stage1State.Splitting;
            UpdateHapticStrength();
            Debug.Log("[Stage1] Starting ball split transition");

            // SoftSet / HardSet = ball + platform, appearing together. The balls were reset to their
            // platform pose while hidden, so that is where the safety reset brings them back.
            softSet.SetActive(true);
            hardSet.SetActive(true);
            centralSet.SetActive(false);

            BallSafetyReset softReset = softBall.GetComponent<BallSafetyReset>();
            BallSafetyReset hardReset = hardBall.GetComponent<BallSafetyReset>();
            if (softReset != null) softReset.SetHeld(true);
            if (hardReset != null) hardReset.SetHeld(true);

            ShowInstruction("Feel the difference between the two balls");

            Vector3 startPosition = centerPoint.position;
            float elapsed = 0f;

            while (elapsed < splitDuration)
            {
                if (!StagePause.IsPausedNow)
                    elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / splitDuration));

                Place(softBall, Vector3.Lerp(startPosition, softPoint.position, t), softPoint.rotation);
                Place(hardBall, Vector3.Lerp(startPosition, hardPoint.position, t), hardPoint.rotation);

                yield return null;
            }

            Place(softBall, softPoint.position, softPoint.rotation);
            Place(hardBall, hardPoint.position, hardPoint.rotation);

            yield return new WaitForFixedUpdate();

            if (softReset != null) softReset.SetHeld(false);
            if (hardReset != null) hardReset.SetHeld(false);

            currentState = Stage1State.ComparisonReady;
            UpdateHapticStrength();

            Debug.Log("[Stage1] Stage 1.2 Comparison Ready");
        }

        // ========================================================
        // STAGE 1.2 EXPERIENCE
        // ========================================================

        private void OnSoftReleased(GrabNotifier ball) { RegisterExperience(true); }
        private void OnHardReleased(GrabNotifier ball) { RegisterExperience(false); }

        private void RegisterExperience(bool soft)
        {
            if (StagePause.IsPausedNow || currentState != Stage1State.ComparisonReady)
                return;

            if (soft) softExperienced = true;
            else hardExperienced = true;
            Debug.Log("[Stage1.2] " + (soft ? "SOFT" : "HARD") + " EXPERIENCED");

            if (softExperienced && hardExperienced)
                StartCoroutine(ShowLabels());
        }

        /// <summary>Both balls squeezed: Weak / Strong pop up, then Next. The balls can still be squeezed.</summary>
        private IEnumerator ShowLabels()
        {
            currentState = Stage1State.LabelsShown;
            Debug.Log("[Stage1.2] BOTH BALLS EXPERIENCED, labels up");

            if (softInfo != null) { softInfo.transform.localScale = Vector3.zero; softInfo.SetActive(true); }
            if (hardInfo != null) { hardInfo.transform.localScale = Vector3.zero; hardInfo.SetActive(true); }

            float elapsed = 0f;
            while (elapsed < labelPopDuration)
            {
                if (!StagePause.IsPausedNow)
                    elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / labelPopDuration));

                if (softInfo != null) softInfo.transform.localScale = softInfoScale * t;
                if (hardInfo != null) hardInfo.transform.localScale = hardInfoScale * t;

                yield return null;
            }

            if (softInfo != null) softInfo.transform.localScale = softInfoScale;
            if (hardInfo != null) hardInfo.transform.localScale = hardInfoScale;

            yield return StagePause.Wait(nextDelay);

            currentState = Stage1State.Completed;
            if (stage12Menu != null)
                stage12Menu.SetActive(true);

            Debug.Log("[Stage1.2] NEXT AVAILABLE");
        }

        // Next button
        public void OnNextPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow || currentState != Stage1State.Completed)
                return;

            if (demoFlow != null)
                demoFlow.GoToStage2();
            else
                Debug.LogError("[Stage1] DemoFlowController is not assigned!");
        }

        // ========================================================
        // HEXR
        // ========================================================

        private void UpdateHapticStrength()
        {
            switch (currentState)
            {
                case Stage1State.SoftReady:
                case Stage1State.SoftResult:
                    SetBallHaptic(centerBall, softStrength);
                    break;

                case Stage1State.HardReady:
                case Stage1State.HardResult:
                    SetBallHaptic(centerBall, hardStrength);
                    break;

                case Stage1State.Splitting:
                case Stage1State.ComparisonReady:
                case Stage1State.LabelsShown:
                case Stage1State.Completed:
                    SetBallHaptic(softBall, softStrength);
                    SetBallHaptic(hardBall, hardStrength);
                    break;
            }
        }

        private static void SetBallHaptic(GrabNotifier ball, float strength)
        {
            var haptic = ball != null ? ball.GetComponent<HapticStrength>() : null;
            if (haptic == null)
            {
                Debug.LogError("[HEXR] HapticStrength not found on " + (ball != null ? ball.name : "null"));
                return;
            }

            haptic.Strength = strength;
            Debug.Log("[HEXR] " + ball.name + " = " + strength);
        }

        // ========================================================
        // HELPERS
        // ========================================================

        private static void ResetBall(GrabNotifier ball)
        {
            var reset = ball != null ? ball.GetComponent<BallSafetyReset>() : null;
            if (reset != null)
                reset.ResetNow();
        }

        /// <summary>Moves a held (kinematic) ball; the Rigidbody pose too, as it interpolates.</summary>
        private static void Place(GrabNotifier ball, Vector3 position, Quaternion rotation)
        {
            ball.transform.SetPositionAndRotation(position, rotation);
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
            }
        }

        private static void HideLabel(GameObject label)
        {
            if (label != null)
                label.SetActive(false);
        }

        private void ShowInstruction(string message)
        {
            if (instructionText == null)
            {
                Debug.LogError("[Stage1] Instruction Text is NOT assigned!");
                return;
            }

            instructionText.gameObject.SetActive(true);
            instructionText.text = message;
            Debug.Log("[Stage1 UI] " + message);
        }

        private void ShowResult(string title, string value)
        {
            if (resultCanvas == null || resultTitle == null || resultValue == null)
            {
                Debug.LogError("[Stage1] Result UI reference missing!");
                return;
            }

            resultCanvas.SetActive(true);
            resultTitle.text = title;
            resultValue.text = value;
        }

        private void HideResult()
        {
            if (resultCanvas != null)
                resultCanvas.SetActive(false);
        }
    }
}
