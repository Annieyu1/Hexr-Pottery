using TMPro;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>Yellow "Tip" card shown to the patient (Figma section 3, "Tip (patient, after 8 s)").</summary>
    public class TipCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text message;

        public bool Visible => gameObject.activeSelf;

        public void Show(string text)
        {
            if (message != null) message.text = text;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
