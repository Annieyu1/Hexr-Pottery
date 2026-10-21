using System.Collections;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Stage 3: four groups of three peaches, sorted into Unripe / Ripe / Overripe by feel.
    /// Timer counts up from Start; each group is scored once all three zones hold a fruit.
    /// </summary>
    public class Stage3Controller : MonoBehaviour
    {
        public enum Stage3State
        {
            Ready,
            Playing,
            Result
        }

        [Header("Sorting Zones")]
        [SerializeField] private FruitSortingZone unripeZone;
        [SerializeField] private FruitSortingZone ripeZone;
        [SerializeField] private FruitSortingZone overripeZone;

        [Header("Fruit Groups")]
        [SerializeField] private GameObject group1;
        [SerializeField] private GameObject group2;
        [SerializeField] private GameObject group3;
        [SerializeField] private GameObject group4;

        [Header("Timer UI")]
        [SerializeField] private GameObject timerCanvas;
        [SerializeField] private TMP_Text timerText;

        [Header("Status UI")]
        [SerializeField] private GameObject statusCanvas;
        [SerializeField] private TMP_Text statusText;

        [Header("Final Result UI")]
        [SerializeField] private GameObject resultCanvas;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultScore;

        [Header("Stage UI")]
        [SerializeField] private GameObject startCanvas;

        [Header("Instruction")]
        [SerializeField] private GameObject instructionObject;

        [Header("Timing")]
        [Tooltip("After all three fruits are placed, wait this long before locking the answer.")]
        [SerializeField] private float confirmTime = 1.0f;
        [Tooltip("How long each group's result shows before the next group.")]
        [SerializeField] private float groupResultDisplayTime = 2.0f;

        [Header("Runtime")]
        [SerializeField] private int currentGroup = 1;
        [SerializeField] private int totalScore;
        [SerializeField] private bool evaluating;
        [SerializeField] private bool groupCompleted;
        [SerializeField] private float elapsedTime;

        private bool timerRunning;
        private Stage3State currentState;
        private int completedGroups;

        private void Start()
        {
            PrepareStage3();
        }

        private void PrepareStage3()
        {
            StopAllCoroutines();
            currentState = Stage3State.Ready;

            timerRunning = false;
            elapsedTime = 0f;

            currentGroup = 1;
            totalScore = 0;
            completedGroups = 0;
            evaluating = false;
            groupCompleted = false;

            ClearAllZones();

            if (startCanvas != null) startCanvas.SetActive(true);
            if (resultCanvas != null) resultCanvas.SetActive(false);
            if (timerCanvas != null) timerCanvas.SetActive(false);
            if (statusCanvas != null) statusCanvas.SetActive(false);
            if (instructionObject != null) instructionObject.SetActive(false);

            for (int g = 1; g <= 4; g++)
                SetGroupActive(g, false);

            UpdateTimerUI();
            Debug.Log("[Stage3] READY - Waiting for Start");
        }

        public void StartGame()
        {
            if (currentState != Stage3State.Ready)
                return;

            currentState = Stage3State.Playing;

            currentGroup = 1;
            totalScore = 0;
            completedGroups = 0;
            evaluating = false;
            groupCompleted = false;

            ClearAllZones();

            if (startCanvas != null) startCanvas.SetActive(false);
            if (timerCanvas != null) timerCanvas.SetActive(true);
            if (statusCanvas != null) statusCanvas.SetActive(false);
            if (resultCanvas != null) resultCanvas.SetActive(false);

            SetGroupActive(1, true);
            SetGroupActive(2, false);
            SetGroupActive(3, false);
            SetGroupActive(4, false);

            elapsedTime = 0f;
            timerRunning = true;
            UpdateTimerUI();

            StartCurrentGroup();
            Debug.Log("[Stage3] START");
        }

        private void Update()
        {
            if (currentState != Stage3State.Playing)
                return;

            // The timer keeps running while a group result is showing.
            if (timerRunning)
            {
                elapsedTime += Time.deltaTime;
                UpdateTimerUI();
            }

            if (groupCompleted || evaluating)
                return;

            if (AllZonesFilled())
                StartCoroutine(ConfirmAndEvaluate());
        }

        private void UpdateTimerUI()
        {
            if (timerText != null)
                timerText.text = FormatTime(elapsedTime);
        }

        private static string FormatTime(float time)
        {
            int totalSeconds = Mathf.FloorToInt(time);
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }

        private void StartCurrentGroup()
        {
            evaluating = false;
            groupCompleted = false;

            if (instructionObject != null) instructionObject.SetActive(true);
            if (statusCanvas != null) statusCanvas.SetActive(false);

            Debug.Log("[Stage3] Group " + currentGroup + " Started");
        }

        private bool AllZonesFilled()
        {
            if (unripeZone == null || ripeZone == null || overripeZone == null)
                return false;

            return unripeZone.GetCurrentFruit() != null
                && ripeZone.GetCurrentFruit() != null
                && overripeZone.GetCurrentFruit() != null;
        }

        private IEnumerator ConfirmAndEvaluate()
        {
            evaluating = true;

            if (statusText != null)
                statusText.text = "Group " + currentGroup + " / 4\nAll fruits placed...";

            // Give the user a moment to change their mind.
            yield return new WaitForSeconds(confirmTime);

            if (!AllZonesFilled())
            {
                evaluating = false;
                if (statusText != null)
                    statusText.text = "Group " + currentGroup + " / 4\nCompare and sort the fruits";
                yield break;
            }

            EvaluateCurrentGroup();
        }

        private void EvaluateCurrentGroup()
        {
            int groupScore = 0;

            FruitItem unripe = unripeZone.GetCurrentFruit();
            if (unripe != null && unripe.CorrectRipeness == FruitItem.Ripeness.Unripe) groupScore++;

            FruitItem ripe = ripeZone.GetCurrentFruit();
            if (ripe != null && ripe.CorrectRipeness == FruitItem.Ripeness.Ripe) groupScore++;

            FruitItem overripe = overripeZone.GetCurrentFruit();
            if (overripe != null && overripe.CorrectRipeness == FruitItem.Ripeness.Overripe) groupScore++;

            totalScore += groupScore;
            completedGroups++;
            groupCompleted = true;
            evaluating = false;

            if (instructionObject != null)
                instructionObject.SetActive(false);

            if (statusText != null)
                statusText.text = "Group " + currentGroup + " Complete\nScore: " + groupScore + " / 3";

            if (statusCanvas != null)
                statusCanvas.SetActive(true);

            Debug.Log("[Stage3] Group " + currentGroup + " Score: " + groupScore + " / 3");
            Debug.Log("[Stage3] Total Score: " + totalScore + " / 12");

            StartCoroutine(ContinueAfterGroup());
        }

        private IEnumerator ContinueAfterGroup()
        {
            yield return new WaitForSeconds(groupResultDisplayTime);

            if (currentGroup >= 4)
            {
                FinishStage3();
                yield break;
            }

            SetGroupActive(currentGroup, false);

            // Let the old fruits leave the zones.
            yield return new WaitForFixedUpdate();
            ClearAllZones();

            currentGroup++;
            SetGroupActive(currentGroup, true);

            // Let the new fruits' colliders enter physics.
            yield return new WaitForFixedUpdate();
            StartCurrentGroup();
        }

        private void SetGroupActive(int groupNumber, bool active)
        {
            GameObject group = groupNumber switch
            {
                1 => group1,
                2 => group2,
                3 => group3,
                4 => group4,
                _ => null
            };

            if (group != null)
                group.SetActive(active);
        }

        private void ClearAllZones()
        {
            if (unripeZone != null) unripeZone.ClearZone();
            if (ripeZone != null) ripeZone.ClearZone();
            if (overripeZone != null) overripeZone.ClearZone();
        }

        private void FinishStage3()
        {
            currentState = Stage3State.Result;
            timerRunning = false;
            UpdateTimerUI();

            int completedFruits = completedGroups * 3;
            Debug.Log("[Stage3] ACT COMPLETE");
            Debug.Log("[Stage3] Accuracy: " + totalScore + " / " + completedFruits);

            SetGroupActive(4, false);
            ClearAllZones();

            if (timerCanvas != null) timerCanvas.SetActive(false);
            if (statusCanvas != null) statusCanvas.SetActive(false);
            if (instructionObject != null) instructionObject.SetActive(false);
            if (resultCanvas != null) resultCanvas.SetActive(true);

            if (resultTitle != null)
                resultTitle.text = "ACT COMPLETE";

            if (resultScore != null)
                resultScore.text = "Accuracy\n" + totalScore + " / " + completedFruits + "\nTime\n" + FormatTime(elapsedTime);
        }

        /// <summary>Replay Stage 3 in place (the PICO version reloaded the scene).</summary>
        public void ReplayStage3()
        {
            foreach (GameObject group in new[] { group1, group2, group3, group4 })
            {
                if (group == null)
                    continue;

                group.SetActive(true);   // BallSafetyReset needs Awake to have run
                foreach (BallSafetyReset reset in group.GetComponentsInChildren<BallSafetyReset>(true))
                    reset.ResetNow();
            }

            PrepareStage3();
        }

        public void OnStartPressed(PointerEvent e) { StartGame(); }
        public void OnReplayPressed(PointerEvent e) { ReplayStage3(); }
    }
}
