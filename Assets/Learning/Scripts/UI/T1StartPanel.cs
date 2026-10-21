using TMPro;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// T1 start screen (prefab Assets/Learning/Prefabs/T1StartPanel.prefab): task chip, the
    /// HandshakeIntro animation, title, short text, info pills and a patient-facing Start button.
    /// HandHoldTask shows it while idle and hides it when the task starts. The prefab cannot
    /// reference the scene's task, so the Start button calls this component, which finds it.
    /// </summary>
    public class T1StartPanel : MonoBehaviour
    {
        [Tooltip("Found in the scene when empty.")]
        [SerializeField] private LearningTask task;
        [Tooltip("Pill text set from the task's hand (\"Right hand\" / \"Left hand\").")]
        [SerializeField] private TMP_Text handLabel;

        private void Awake()
        {
            if (task == null) task = FindAnyObjectByType<LearningTask>();
        }

        private void Start()
        {
            if (handLabel != null && task != null)
                handLabel.text = (task.AffectedHand == HandSide.Left ? "Left" : "Right") + " hand";
        }

        public void Show(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        public void StartTask()
        {
            if (task != null) task.BeginTask();
        }

        public void OnStartPressed(Oculus.Interaction.PointerEvent e) { StartTask(); }
    }
}
