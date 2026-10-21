using System.Collections;
using System.Collections.Generic;
using HexRMeta.Learning;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Stage 2, the ball-sorting game. A round = put the three identical-looking balls into the
    /// Weak / Medium / Strong baskets; a task = 3 rounds.
    ///
    /// Task 1: every round uses 10 / 35 / 60 (shuffled). After it: the result (accuracy so far + time)
    /// and "Try one more time" → Easier (10 / 35 / 60 again) or Harder (3 of 15 / 30 / 45 / 60, picked
    /// at random every round). After that task: the result over all 6 rounds, then Replay (whole game
    /// again) or Back (Stage 1 from the start). The timer runs from the first round, stops on the
    /// result screens and while paused.
    /// </summary>
    public class StiffnessTrialManager : MonoBehaviour
    {
        public enum Difficulty { Easier, Harder }

        private const int RoundsPerTask = 3;
        private static readonly int[] EasyLevels = { 10, 35, 60 };
        private static readonly int[] HardPool = { 15, 30, 45, 60 };

        [Header("Balls")]
        [SerializeField] private StiffnessBall[] balls;

        [Header("Baskets")]
        [SerializeField] private BasketTrigger[] baskets;

        [Header("Step Card UI")]
        [Tooltip("Figma Step Card (green): instruction, progress, round and task results.")]
        [SerializeField] private StepCard card;

        [Header("Buttons (each group is shown on its own)")]
        [Tooltip("After task 1: Try one more time.")]
        [SerializeField] private GameObject tryAgainButtons;
        [Tooltip("Easier / Harder.")]
        [SerializeField] private GameObject difficultyButtons;
        [Tooltip("After task 2: Replay / Back.")]
        [SerializeField] private GameObject endButtons;

        [Header("Flow")]
        [SerializeField] private DemoFlowController demoFlow;

        [Header("Timing")]
        [Tooltip("How long a round's answers stay up before the next round.")]
        [SerializeField] private float roundResultTime = 2.5f;

        [Header("Debug")]
        [SerializeField] private int taskNumber;        // 1, 2
        [SerializeField] private int roundInTask;       // 1..3
        [SerializeField] private Difficulty difficulty;
        [SerializeField] private int roundsPlayed;
        [SerializeField] private int ballsCorrect;
        [SerializeField] private int ballsTotal;
        [SerializeField] private float elapsed;

        // Per basket: which ball is in it.
        private readonly Dictionary<StiffnessCategory, StiffnessBall> basketPlacements =
            new Dictionary<StiffnessCategory, StiffnessBall>();

        private bool roundActive;
        private bool timerRunning;

        private void Update()
        {
            if (timerRunning && !StagePause.IsPausedNow)
                elapsed += Time.deltaTime;
        }

        // ============================================================
        // GAME / TASK / ROUND
        // ============================================================

        /// <summary>Start the sorting game from task 1 (Stage 2 entry and Replay).</summary>
        public void BeginGame()
        {
            StopAllCoroutines();
            taskNumber = 0;
            roundsPlayed = 0;
            ballsCorrect = 0;
            ballsTotal = 0;
            elapsed = 0f;
            Debug.Log("[Trial] New game");
            StartTask(Difficulty.Easier);
        }

        private void StartTask(Difficulty newDifficulty)
        {
            taskNumber++;
            difficulty = newDifficulty;
            roundInTask = 0;
            Debug.Log($"[Trial] Task {taskNumber} ({difficulty})");
            StartCoroutine(NextRound());
        }

        private IEnumerator NextRound()
        {
            ShowButtons(null);
            roundActive = false;

            ResetBalls();
            // Let the baskets see the balls leave before the new round starts.
            yield return new WaitForFixedUpdate();
            ClearBaskets();

            roundInTask++;
            basketPlacements.Clear();

            foreach (StiffnessBall ball in balls)
                if (ball != null)
                    ball.HideAnswer();
            foreach (BasketTrigger basket in baskets)
                if (basket != null)
                    basket.ResetResultUI();

            ConfigureBalls();

            roundActive = true;
            timerRunning = true;
            UpdateHints();
            ShowProgressCard();
        }

        /// <summary>
        /// Easier: 10 / 35 / 60. Harder: 3 of 15 / 30 / 45 / 60. The softest of the three is Weak,
        /// the firmest Strong; positions are shuffled (Fisher–Yates).
        /// </summary>
        private void ConfigureBalls()
        {
            var values = new List<int>(difficulty == Difficulty.Easier ? EasyLevels : HardPool);
            Shuffle(values);
            if (values.Count > 3)
                values.RemoveRange(3, values.Count - 3);

            var sorted = new List<int>(values);
            sorted.Sort();

            for (int i = 0; i < balls.Length && i < values.Count; i++)
                balls[i].Configure(values[i], (StiffnessCategory)sorted.IndexOf(values[i]));

            Debug.Log($"[Trial] Task {taskNumber} round {roundInTask}: {balls[0].name}={values[0]}, " +
                      $"{balls[1].name}={values[1]}, {balls[2].name}={values[2]}");
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ============================================================
        // PLACEMENT
        // ============================================================

        public void ReportPlacement(StiffnessBall ball, StiffnessCategory basketCategory)
        {
            if (!roundActive || ball == null || StagePause.IsPausedNow)
                return;

            // A ball is only ever recorded in one basket.
            RemoveBallFromAnyBasket(ball);
            basketPlacements[basketCategory] = ball;

            Debug.Log($"[Trial] {ball.name} placed in {basketCategory}. Baskets filled = {basketPlacements.Count}/3");
            UpdateHints();
            ShowProgressCard();

            if (AllThreeBasketsFilled())
                StartCoroutine(EndRound());
        }

        public void ReportRemoval(StiffnessBall ball)
        {
            if (!roundActive || ball == null || StagePause.IsPausedNow)
                return;

            if (RemoveBallFromAnyBasket(ball))
                Debug.Log($"[Trial] {ball.name} removed.");

            UpdateHints();
            ShowProgressCard();
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

        // ============================================================
        // ROUND / TASK RESULTS
        // ============================================================

        private IEnumerator EndRound()
        {
            roundActive = false;
            UpdateHints();

            int correct = 0;
            foreach (BasketTrigger basket in baskets)
            {
                if (basket == null)
                    continue;
                bool ok = basketPlacements[basket.BasketCategory].CorrectCategory == basket.BasketCategory;
                basket.ShowResult(ok);
                if (ok) correct++;
            }
            foreach (StiffnessBall ball in balls)
                if (ball != null)
                    ball.ShowAnswer();

            roundsPlayed++;
            ballsCorrect += correct;
            ballsTotal += 3;
            Debug.Log($"[Trial Result] Task {taskNumber} round {roundInTask}: {correct}/3, so far {ballsCorrect}/{ballsTotal}");

            bool taskDone = roundInTask >= RoundsPerTask;
            if (taskDone)
                timerRunning = false;

            ShowCard($"Round {roundInTask} / {RoundsPerTask}", $"{correct} / 3 correct",
                     correct == 3 ? "Well done!" : "See the answers on the baskets", RoundsPerTask, roundInTask,
                     correct == 3 ? CardTone.Correct : CardTone.Answer);

            yield return StagePause.Wait(roundResultTime);

            if (taskDone)
                ShowTaskResult();
            else
                yield return NextRound();
        }

        /// <summary>Accuracy over every round so far (6 rounds after task 2) and the time.</summary>
        private void ShowTaskResult()
        {
            int percent = ballsTotal > 0 ? Mathf.RoundToInt(100f * ballsCorrect / ballsTotal) : 0;
            string hint = $"{ballsCorrect} / {ballsTotal} balls in {roundsPlayed} rounds\nTime {FormatTime(elapsed)}";
            ShowCard("Result", $"{percent}% correct", hint, RoundsPerTask, RoundsPerTask,
                     percent >= 100 ? CardTone.Correct : CardTone.Done);

            Debug.Log($"[Trial] Task {taskNumber} done: {ballsCorrect}/{ballsTotal} = {percent}%, time {FormatTime(elapsed)}");
            ShowButtons(taskNumber == 1 ? tryAgainButtons : endButtons);
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        // ============================================================
        // RESET (Menu)
        // ============================================================

        /// <summary>Menu: stop everything and put the balls home (Stage 2 may be hidden).</summary>
        public void ResetStage()
        {
            StopAllCoroutines();
            roundActive = false;
            timerRunning = false;
            basketPlacements.Clear();
            ResetBalls();
            ClearBaskets();
            foreach (StiffnessBall ball in balls)
                if (ball != null)
                    ball.HideAnswer();
            foreach (BasketTrigger basket in baskets)
                if (basket != null)
                    basket.ResetResultUI();
            ShowButtons(null);
            UpdateHints();
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

        // ============================================================
        // UI
        // ============================================================

        private void ShowProgressCard()
        {
            ShowCard($"Round {roundInTask} / {RoundsPerTask}", "Feel each ball", "Put it in the matching basket",
                     RoundsPerTask, basketPlacements.Count, CardTone.Normal);
        }

        private void ShowCard(string chip, string title, string hint, int segments, int filled, CardTone tone)
        {
            if (card != null)
                card.Show(chip, title, hint, Mathf.Min(filled, segments), tone);
            Debug.Log("[Stage2 UI] " + chip + " | " + title + " | " + hint.Replace("\n", " "));
        }

        private void ShowButtons(GameObject group)
        {
            if (tryAgainButtons != null) tryAgainButtons.SetActive(group == tryAgainButtons);
            if (difficultyButtons != null) difficultyButtons.SetActive(group == difficultyButtons);
            if (endButtons != null) endButtons.SetActive(group == endButtons);
        }

        /// <summary>The pulsing grab cue on every ball still waiting for a basket.</summary>
        private void UpdateHints()
        {
            foreach (StiffnessBall ball in balls)
            {
                var hint = ball != null ? ball.GetComponent<GrabHint>() : null;
                if (hint != null)
                    hint.SetHint(roundActive && !basketPlacements.ContainsValue(ball));
            }
        }

        // ============================================================
        // BUTTONS
        // ============================================================

        public void OnTryAgainPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow)
                return;
            ShowButtons(difficultyButtons);
            ShowCard("Choose", "One more time", "Easier: the same balls\nHarder: closer strengths", RoundsPerTask, 0, CardTone.Answer);
        }

        public void OnEasierPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow)
                return;
            StartTask(Difficulty.Easier);
        }

        public void OnHarderPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow)
                return;
            StartTask(Difficulty.Harder);
        }

        public void OnReplayPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow)
                return;
            BeginGame();
        }

        public void OnBackPressed(PointerEvent e)
        {
            if (StagePause.IsPausedNow)
                return;
            if (demoFlow != null)
                demoFlow.BackToMenu();
            else
                Debug.LogError("[Stage2] DemoFlowController is not assigned!");
        }
    }
}
