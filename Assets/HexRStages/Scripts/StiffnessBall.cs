using TMPro;
using UnityEngine;

namespace HexRMeta.Stages
{
    public enum StiffnessCategory
    {
        Weak = 0,
        Medium = 1,
        Strong = 2
    }

    /// <summary>A Stage 2 ball: its stiffness drives the glove pressure, its label shows the answer.</summary>
    public class StiffnessBall : MonoBehaviour
    {
        [Header("Runtime State")]
        [SerializeField] private int stiffness;
        [SerializeField] private StiffnessCategory correctCategory;

        [Header("Answer UI")]
        [SerializeField] private GameObject answerLabelRoot;
        [SerializeField] private TMP_Text answerLabelText;
        [Tooltip("The answer stands this far in front of the ball (towards the patient), upright, so it covers neither the ball nor the basket sign.")]
        [SerializeField] private float answerForward = 0.075f;

        private Quaternion answerRotation = Quaternion.identity;

        public int Stiffness => stiffness;
        public StiffnessCategory CorrectCategory => correctCategory;

        private void Awake()
        {
            if (answerLabelRoot != null)
                answerRotation = answerLabelRoot.transform.rotation;   // built facing the patient
            HideAnswer();
        }

        public void Configure(int newStiffness, StiffnessCategory newCategory)
        {
            stiffness = newStiffness;
            correctCategory = newCategory;

            HideAnswer();

            var haptic = GetComponent<HapticStrength>();
            if (haptic != null)
                haptic.Strength = stiffness;
            else
                Debug.LogError("[Stage2 HEXR] HapticStrength missing on " + name);

            Debug.Log($"[StiffnessBall] {name}: Stiffness={stiffness}, Category={correctCategory}");
        }

        public void ShowAnswer()
        {
            if (answerLabelText != null)
                answerLabelText.text = correctCategory.ToString().ToUpperInvariant();

            if (answerLabelRoot != null)
            {
                // The label is a child of the ball, which may have rolled: put it back upright, in front.
                Vector3 towardPatient = -(answerRotation * Vector3.forward);
                answerLabelRoot.transform.SetPositionAndRotation(transform.position + towardPatient * answerForward, answerRotation);
                answerLabelRoot.SetActive(true);
            }

            Debug.Log($"[StiffnessBall] Reveal {name}: {correctCategory}");
        }

        public void HideAnswer()
        {
            if (answerLabelRoot != null)
                answerLabelRoot.SetActive(false);
        }
    }
}
