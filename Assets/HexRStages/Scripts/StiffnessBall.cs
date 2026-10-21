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

        public int Stiffness => stiffness;
        public StiffnessCategory CorrectCategory => correctCategory;

        private void Awake()
        {
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
                answerLabelRoot.SetActive(true);

            Debug.Log($"[StiffnessBall] Reveal {name}: {correctCategory}");
        }

        public void HideAnswer()
        {
            if (answerLabelRoot != null)
                answerLabelRoot.SetActive(false);
        }
    }
}
