using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Stage 2: three identical balls (Weak 10 / Medium 35 / Strong 60, shuffled every trial) go into the
    /// Weak / Medium / Strong baskets. Scored once all three baskets hold a ball; One more try starts a new trial.
    /// </summary>
    public class StiffnessTrialManager : MonoBehaviour
    {
        [Header("Balls")]
        [SerializeField] private StiffnessBall[] balls;

        [Header("Baskets")]
        [SerializeField] private BasketTrigger[] baskets;

        [Header("Instruction UI")]
        [SerializeField] private TMP_Text instructionText;

        [Header("Progress UI")]
        [SerializeField] private TMP_Text progressText;

        [Header("Result UI")]
        [SerializeField] private TMP_Text scoreText;

        [Header("Result Buttons")]
        [SerializeField] private GameObject resultButtons;

        // Per basket: which ball is in it.
        private readonly Dictionary<StiffnessCategory, StiffnessBall> basketPlacements =
            new Dictionary<StiffnessCategory, StiffnessBall>();

        private bool trialEvaluated;

        private void Start()
        {
            StartNewTrial();
        }

        // ============================================================
        // NEW TRIAL
        // ============================================================

        public void StartNewTrial()
        {
            basketPlacements.Clear();
            trialEvaluated = false;

            if (resultButtons != null)
                resultButtons.SetActive(false);

            if (balls == null || balls.Length != 3)
            {
                Debug.LogError("[Trial] Exactly 3 balls are required.");
                return;
            }

            if (baskets == null || baskets.Length != 3)
            {
                Debug.LogError("[Trial] Exactly 3 baskets are required.");
                return;
            }

            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                instructionText.text = "Feel each ball and put it in the matching basket";
            }

            if (progressText != null)
            {
                progressText.gameObject.SetActive(true);
                progressText.text = "PLACED 0 / 3";
            }

            if (scoreText != null)
            {
                scoreText.text = "";
                scoreText.gameObject.SetActive(false);
            }

            foreach (StiffnessBall ball in balls)
                if (ball != null)
                    ball.HideAnswer();

            foreach (BasketTrigger basket in baskets)
                if (basket != null)
                    basket.ResetResultUI();

            // New random stiffness per position every trial (Fisher–Yates).
            var levels = new List<(int value, StiffnessCategory category)>
            {
                (10, StiffnessCategory.Weak),
                (35, StiffnessCategory.Medium),
                (60, StiffnessCategory.Strong)
            };
            for (int i = levels.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (levels[i], levels[j]) = (levels[j], levels[i]);
            }

            for (int i = 0; i < balls.Length; i++)
                balls[i].Configure(levels[i].value, levels[i].category);

            Debug.Log($"[Trial] New trial: {balls[0].name}={levels[0].value}, " +
                      $"{balls[1].name}={levels[1].value}, {balls[2].name}={levels[2].value}");
        }

        // ============================================================
        // PLACEMENT
        // ============================================================

        public void ReportPlacement(StiffnessBall ball, StiffnessCategory basketCategory)
        {
            if (trialEvaluated || ball == null || StagePause.IsPausedNow)
                return;

            // A ball is only ever recorded in one basket.
            RemoveBallFromAnyBasket(ball);
            basketPlacements[basketCategory] = ball;

            Debug.Log($"[Trial] {ball.name} placed in {basketCategory}. Baskets filled = {basketPlacements.Count}/3");
            UpdateProgressUI();

            if (AllThreeBasketsFilled())
                EvaluateTrial();
        }

        public void ReportRemoval(StiffnessBall ball)
        {
            if (trialEvaluated || ball == null || StagePause.IsPausedNow)
                return;

            if (RemoveBallFromAnyBasket(ball))
                Debug.Log($"[Trial] {ball.name} removed.");

            UpdateProgressUI();
        }

        private bool RemoveBallFromAnyBasket(StiffnessBall ball)
        {
            foreach (var placement in basketPlacements)
            {
                if (placement.Value == ball)
                {
                    basketPlacements.Remove(placement.Key);
                    return true;
                }
            }
            return false;
        }

        private bool AllThreeBasketsFilled()
        {
            return basketPlacements.ContainsKey(StiffnessCategory.Weak)
                && basketPlacements.ContainsKey(StiffnessCategory.Medium)
                && basketPlacements.ContainsKey(StiffnessCategory.Strong);
        }

        private void UpdateProgressUI()
        {
            if (progressText != null)
                progressText.text = $"PLACED {basketPlacements.Count} / 3";
        }

        // ============================================================
        // EVALUATE
        // ============================================================

        private void EvaluateTrial()
        {
            if (trialEvaluated || !AllThreeBasketsFilled())
                return;

            bool weakCorrect = basketPlacements[StiffnessCategory.Weak].CorrectCategory == StiffnessCategory.Weak;
            bool mediumCorrect = basketPlacements[StiffnessCategory.Medium].CorrectCategory == StiffnessCategory.Medium;
            bool strongCorrect = basketPlacements[StiffnessCategory.Strong].CorrectCategory == StiffnessCategory.Strong;

            int correctCount = (weakCorrect ? 1 : 0) + (mediumCorrect ? 1 : 0) + (strongCorrect ? 1 : 0);

            trialEvaluated = true;

            if (instructionText != null)
                instructionText.gameObject.SetActive(false);

            if (progressText != null)
                progressText.gameObject.SetActive(false);

            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(true);
                scoreText.text = $"RESULT\n\n{correctCount} / 3 CORRECT";
            }

            if (resultButtons != null)
                resultButtons.SetActive(true);

            foreach (StiffnessBall ball in balls)
                if (ball != null)
                    ball.ShowAnswer();

            foreach (BasketTrigger basket in baskets)
            {
                if (basket == null)
                    continue;

                switch (basket.BasketCategory)
                {
                    case StiffnessCategory.Weak: basket.ShowResult(weakCorrect); break;
                    case StiffnessCategory.Medium: basket.ShowResult(mediumCorrect); break;
                    case StiffnessCategory.Strong: basket.ShowResult(strongCorrect); break;
                }
            }

            Debug.Log($"[Trial Result] Weak={weakCorrect}, Medium={mediumCorrect}, Strong={strongCorrect}, Score={correctCount}/3");
        }

        // ============================================================
        // REPLAY / RESET
        // ============================================================

        /// <summary>One more try: balls home, baskets cleared, new random stiffness.</summary>
        public void ReplayStage2()
        {
            if (!isActiveAndEnabled)
            {
                ResetStage();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(ReplayRoutine());
        }

        private IEnumerator ReplayRoutine()
        {
            ResetBalls();

            // Let the baskets see the balls leave before the new trial starts.
            yield return new WaitForFixedUpdate();

            ClearBaskets();
            StartNewTrial();
        }

        /// <summary>Menu: same as One more try, but at once (Stage 2 may be hidden).</summary>
        public void ResetStage()
        {
            StopAllCoroutines();
            ResetBalls();
            ClearBaskets();
            StartNewTrial();
        }

        private void ResetBalls()
        {
            foreach (StiffnessBall ball in balls)
            {
                var reset = ball != null ? ball.GetComponent<BallSafetyReset>() : null;
                if (reset != null)
                    reset.ResetNow();
            }
        }

        private void ClearBaskets()
        {
            foreach (BasketTrigger basket in baskets)
                if (basket != null)
                    basket.ClearBasket();
        }

        // One more try button
        public void OnOneMoreTryPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow || !trialEvaluated)
                return;

            ReplayStage2();
        }
    }
}
