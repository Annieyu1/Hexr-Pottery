using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// A Stage 2 basket. A ball counts as placed as soon as it enters the trigger;
    /// one ball per basket. The info panel turns green / red when the trial is scored.
    /// </summary>
    public class BasketTrigger : MonoBehaviour
    {
        [Header("Basket")]
        [SerializeField] private StiffnessCategory basketCategory;
        [SerializeField] private StiffnessTrialManager trialManager;

        public StiffnessCategory BasketCategory => basketCategory;

        [Header("Result UI")]
        [SerializeField] private Renderer infoPanel;
        [SerializeField] private Color correctColor = new Color(0.25f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color wrongColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock block;
        private StiffnessBall ballInside;

        private void OnTriggerEnter(Collider other)
        {
            // Balls are frozen while paused; nothing should move in or out.
            if (StagePause.IsPausedNow)
                return;

            // Hands and other colliders have no StiffnessBall and are ignored.
            StiffnessBall ball = other.GetComponentInParent<StiffnessBall>();
            if (ball == null)
                return;

            // Same ball, several colliders.
            if (ballInside == ball)
                return;

            // One ball per basket.
            if (ballInside != null)
                return;

            ballInside = ball;
            Debug.Log($"[Basket] {ball.name} entered {basketCategory} basket.");

            if (trialManager != null)
                trialManager.ReportPlacement(ball, basketCategory);
            else
                Debug.LogWarning($"[Basket] {basketCategory}: Trial Manager is not assigned.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (StagePause.IsPausedNow)
                return;

            StiffnessBall ball = other.GetComponentInParent<StiffnessBall>();
            if (ball == null || ballInside != ball)
                return;

            if (trialManager != null)
                trialManager.ReportRemoval(ball);

            Debug.Log($"[Basket] {ball.name} left {basketCategory} basket.");
            ballInside = null;
        }

        public void ShowResult(bool correct)
        {
            SetPanelColor(correct ? correctColor : wrongColor);
        }

        public void ResetResultUI()
        {
            if (infoPanel == null)
                return;

            infoPanel.SetPropertyBlock(null);
        }

        /// <summary>Replay: forget the ball that was inside.</summary>
        public void ClearBasket()
        {
            ballInside = null;
        }

        private void SetPanelColor(Color color)
        {
            if (infoPanel == null)
                return;

            if (block == null)
                block = new MaterialPropertyBlock();

            infoPanel.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            infoPanel.SetPropertyBlock(block);
        }
    }
}
