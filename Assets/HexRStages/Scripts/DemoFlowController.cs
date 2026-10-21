using Oculus.Interaction;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Stage 1 → Stage 2, one root GameObject per stage. Menu (BackToMenu) restarts Stage 1.
    /// Stage 3 is kept but not entered for now.
    /// </summary>
    public class DemoFlowController : MonoBehaviour
    {
        [Header("Stage Roots")]
        [SerializeField] private GameObject stage1Root;
        [SerializeField] private GameObject stage2Root;
        [SerializeField] private GameObject stage3Root;

        [Header("Stage Controllers")]
        [SerializeField] private Stage1Controller stage1Controller;
        [SerializeField] private StiffnessTrialManager stage2Manager;

        private void Start()
        {
            ShowOnly(stage1Root);
        }

        public void GoToStage2()
        {
            ShowOnly(stage2Root);
            Debug.Log("[DemoFlow] Stage 2");
        }

        public void GoToStage3()
        {
            ShowOnly(stage3Root);
            Debug.Log("[DemoFlow] Stage 3");
        }

        /// <summary>No real menu yet: unpause, clear Stage 2, and start Stage 1 from the top.</summary>
        public void BackToMenu()
        {
            if (StagePause.Instance != null)
                StagePause.Instance.Resume();

            if (stage2Manager != null)
                stage2Manager.ResetStage();

            ShowOnly(stage1Root);

            if (stage1Controller != null)
                stage1Controller.ResetStage();

            Debug.Log("[DemoFlow] Menu → Stage 1");
        }

        private void ShowOnly(GameObject root)
        {
            if (stage1Root != null) stage1Root.SetActive(root == stage1Root);
            if (stage2Root != null) stage2Root.SetActive(root == stage2Root);
            if (stage3Root != null) stage3Root.SetActive(root == stage3Root);
        }

        // Poke buttons (PointableUnityEventWrapper.WhenSelect)
        public void GoToStage2(PointerEvent e) { GoToStage2(); }
        public void GoToStage3(PointerEvent e) { GoToStage3(); }
        public void BackToMenu(PointerEvent e) { BackToMenu(); }
    }
}
